using Kernel.Bullet;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 标记背包槽位所在区域，便于背包与 Spell Book 之间执行不同的数据写回策略。
    /// </summary>
    public enum BackPackSlotArea
    {
        Inventory = 0,
        SpellBook = 1,
    }

    /// <summary>
    /// 标记当前格在连锁物件中的显示角色。
    /// </summary>
    public enum BackPackChainCellRole
    {
        Empty = 0,
        Single = 1,
        ChainHead = 2,
        ChainBody = 3,
        ChainTail = 4,
    }

    /// <summary>
    /// 背包单槽位视图，负责显示当前格上的视觉 token 并把拖拽事件转发给 BackPackUIScreen。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackPackGridSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("View")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text tokenText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("State Colors")]
        [SerializeField] private Color inventoryIdleColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color spellBookIdleColor = new(1f, 0.9f, 0.7f, 0.45f);
        [SerializeField] private Color dragHighlightColor = new(1f, 1f, 1f, 0.8f);
        [SerializeField] private Color chainHeadTint = new(1f, 0.97f, 0.84f, 1f);
        [SerializeField] private Color chainBodyTint = new(0.9f, 0.95f, 1f, 1f);
        [SerializeField] private Color chainTailTint = new(0.93f, 1f, 0.9f, 1f);

        private BackPackUIScreen owner;
        private TokenCellOccupancy occupancy;
        private bool isDragging;
        private bool isDisplayOnly;
        private BackPackSlotArea area;
        private int slotIndex;
        private RectTransform rectTransform;

        public BackPackSlotArea Area => area;
        public int SlotIndex => slotIndex;
        public PlaceableTokenData Item => occupancy.item;
        public int AnchorIndex => occupancy.anchorIndex;
        public int LocalOffset => occupancy.localOffset;
        public bool IsAnchor => occupancy.isAnchor;
        public BaseTokenData Token => occupancy.VisualToken;
        public RectTransform SlotRectTransform => rectTransform;
        public BackPackChainCellRole ChainRole => ResolveChainRole();

        private void Awake()
        {
            EnsureLocalReferences();
            SetRaycastBlocking(true);
            ApplyVisualState();
        }

        private void OnValidate()
        {
            EnsureLocalReferences();
        }

        /// <summary>
        /// summary: 由 BackPackUIScreen 在运行时绑定槽位所在区域、索引和所属屏幕。
        /// param: ownerScreen 当前背包屏幕实例
        /// param: slotArea 当前槽位所属区域
        /// param: index 当前槽位在所属区域内的索引
        /// returns: 无
        /// </summary>
        public void Initialize(BackPackUIScreen ownerScreen, BackPackSlotArea slotArea, int index)
        {
            EnsureLocalReferences();
            owner = ownerScreen;
            area = slotArea;
            slotIndex = index;
            isDisplayOnly = false;
            isDragging = false;
            SetRaycastBlocking(true);
            ApplyVisualState();
        }

        /// <summary>
        /// summary: 把槽位切到只读展示模式，供 Main HUD 等非拖拽场景复用同一份 prefab。
        /// param: slotArea 当前槽位需要显示成的区域风格
        /// returns: 无
        /// </summary>
        public void InitializeDisplayOnly(BackPackSlotArea slotArea)
        {
            EnsureLocalReferences();
            owner = null;
            area = slotArea;
            slotIndex = -1;
            isDisplayOnly = true;
            isDragging = false;
            SetRaycastBlocking(false);
            ApplyVisualState();
        }

        /// <summary>
        /// summary: 用新的占用信息刷新当前槽位显示。
        /// param: value 当前格的最新占用状态
        /// returns: 无
        /// </summary>
        public void SetOccupancy(TokenCellOccupancy value)
        {
            EnsureLocalReferences();
            occupancy = value;
            RefreshText();
            ApplyVisualState();
        }

        /// <summary>
        /// summary: 用单格基础 token 刷新当前槽位显示，兼容旧调用方。
        /// param: value 当前格需要显示的单格基础 token
        /// returns: 无
        /// </summary>
        public void SetToken(BaseTokenData value)
        {
            SetOccupancy(value != null ? new TokenCellOccupancy(value, slotIndex, 0, true) : TokenCellOccupancy.Empty);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isDisplayOnly || owner == null || !occupancy.IsOccupied)
            {
                return;
            }

            isDragging = true;
            SetRaycastBlocking(false);
            ApplyVisualState();
            owner.NotifySlotBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDisplayOnly)
            {
                return;
            }

            owner?.NotifySlotDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDisplayOnly)
            {
                return;
            }

            ResetDragPresentation();
            owner?.NotifySlotEndDrag(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (isDisplayOnly)
            {
                return;
            }

            owner?.NotifySlotDrop(this);
        }

        /// <summary>
        /// summary: 强制恢复当前槽位的拖拽表现，确保关闭背包或异常中断时不会残留半透明和禁用射线状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void ResetDragPresentation()
        {
            EnsureLocalReferences();
            isDragging = false;
            SetRaycastBlocking(true);
            ApplyVisualState();
        }

        /// <summary>
        /// summary: 自动补齐当前 prefab 中的背景、文本和透明度组件引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureLocalReferences()
        {
            rectTransform ??= transform as RectTransform;
            background ??= transform.Find("Background")?.GetComponent<Image>();
            tokenText ??= transform.Find("Text")?.GetComponent<TMP_Text>();
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null && Application.isPlaying)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (tokenText != null)
            {
                tokenText.raycastTarget = false;
            }
        }

        /// <summary>
        /// summary: 根据当前格上的视觉 token 更新文本显示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshText()
        {
            if (tokenText == null)
            {
                return;
            }

            tokenText.text = occupancy.VisualToken != null ? occupancy.VisualToken.GetResolvedDisplayText() : string.Empty;
        }

        /// <summary>
        /// summary: 应用空槽、连锁格和拖拽中的基础视觉反馈。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ApplyVisualState()
        {
            if (background != null)
            {
                background.color = isDragging ? dragHighlightColor : ResolveIdleColor();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = isDragging ? 0.6f : 1f;
            }
        }

        /// <summary>
        /// summary: 在拖拽生命周期内控制当前槽位是否继续拦截 EventSystem 射线，避免源槽位挡住目标槽位接收 drop。
        /// param: shouldBlock 当前是否继续阻挡射线
        /// returns: 无
        /// </summary>
        private void SetRaycastBlocking(bool shouldBlock)
        {
            if (canvasGroup == null)
            {
                return;
            }

            if (isDisplayOnly)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                return;
            }

            canvasGroup.blocksRaycasts = shouldBlock;
            canvasGroup.interactable = true;
        }

        private BackPackChainCellRole ResolveChainRole()
        {
            if (!occupancy.IsOccupied)
            {
                return BackPackChainCellRole.Empty;
            }

            int span = occupancy.item != null && occupancy.item.SlotSpan > 0 ? occupancy.item.SlotSpan : 1;
            if (span <= 1)
            {
                return BackPackChainCellRole.Single;
            }

            if (occupancy.localOffset <= 0)
            {
                return BackPackChainCellRole.ChainHead;
            }

            if (occupancy.localOffset >= span - 1)
            {
                return BackPackChainCellRole.ChainTail;
            }

            return BackPackChainCellRole.ChainBody;
        }

        private Color ResolveIdleColor()
        {
            Color baseColor = area == BackPackSlotArea.SpellBook ? spellBookIdleColor : inventoryIdleColor;
            return ChainRole switch
            {
                BackPackChainCellRole.ChainHead => MultiplyColor(baseColor, chainHeadTint),
                BackPackChainCellRole.ChainBody => MultiplyColor(baseColor, chainBodyTint),
                BackPackChainCellRole.ChainTail => MultiplyColor(baseColor, chainTailTint),
                _ => baseColor,
            };
        }

        private static Color MultiplyColor(Color left, Color right)
        {
            return new Color(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
        }
    }

    /// <summary>
    /// 运行时拖拽预览视图，负责显示一个不参与交互的跟手槽位副本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackPackDragPreviewView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text tokenText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Preview Colors")]
        [SerializeField] private Color inventoryPreviewColor = new(1f, 1f, 1f, 0.92f);
        [SerializeField] private Color spellBookPreviewColor = new(1f, 0.9f, 0.7f, 0.92f);
        [SerializeField] private bool hideTextForLinkedPreview = true;

        private RectTransform rectTransform;

        public RectTransform PreviewRectTransform => rectTransform;

        private void Awake()
        {
            EnsureLocalReferences();
        }

        /// <summary>
        /// summary: 用源槽位的数据刷新预览显示，并把尺寸同步为当前槽位大小。
        /// param: source 当前拖拽源槽位
        /// returns: 无
        /// </summary>
        public void InitializeFromSlot(BackPackGridSlotView source)
        {
            InitializeFromSlotRange(source, source != null ? source.SlotRectTransform : null, source != null ? source.SlotRectTransform : null);
        }

        /// <summary>
        /// summary: 用源槽位和整件首尾槽位的数据刷新预览显示，并把尺寸同步为整件跨度。
        /// param: source 当前拖拽源槽位
        /// param: firstSlot 当前整件首格 RectTransform
        /// param: lastSlot 当前整件末格 RectTransform
        /// returns: 无
        /// </summary>
        public void InitializeFromSlotRange(BackPackGridSlotView source, RectTransform firstSlot, RectTransform lastSlot)
        {
            EnsureLocalReferences();
            if (source == null)
            {
                return;
            }

            bool isLinkedPreview = source.Item != null && source.Item.SlotSpan > 1 && firstSlot != null && lastSlot != null && firstSlot != lastSlot;
            SetToken(isLinkedPreview && hideTextForLinkedPreview ? null : source.Token);
            SetArea(source.Area);
            CopyBoundsFrom(firstSlot != null ? firstSlot : source.SlotRectTransform, lastSlot != null ? lastSlot : source.SlotRectTransform);
        }

        /// <summary>
        /// summary: 按当前指针屏幕坐标更新预览位置，保证拖拽副本在背包界面根节点下稳定跟手。
        /// param: previewLayer 当前预览层根节点
        /// param: screenPosition 当前指针屏幕坐标
        /// param: eventCamera 当前 UI 事件相机；Overlay 模式下允许为空
        /// returns: 无
        /// </summary>
        public void UpdateScreenPosition(RectTransform previewLayer, Vector2 screenPosition, Camera eventCamera)
        {
            EnsureLocalReferences();
            if (previewLayer == null || rectTransform == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(previewLayer, screenPosition, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            rectTransform.anchoredPosition = localPoint;
        }

        /// <summary>
        /// summary: 强制把预览视图设置为不拦截射线的纯展示层，避免挡住真实槽位接收 drop。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureLocalReferences()
        {
            rectTransform ??= transform as RectTransform;
            background ??= transform.Find("Background")?.GetComponent<Image>();
            tokenText ??= transform.Find("Text")?.GetComponent<TMP_Text>();
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null && Application.isPlaying)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (background != null)
            {
                background.raycastTarget = false;
            }

            if (tokenText != null)
            {
                tokenText.raycastTarget = false;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.92f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        /// <summary>
        /// summary: 刷新预览上的 token 文本显示。
        /// param: value 需要显示的 token；为空时显示为空字符串
        /// returns: 无
        /// </summary>
        private void SetToken(BaseTokenData value)
        {
            if (tokenText == null)
            {
                return;
            }

            tokenText.text = value != null ? value.GetResolvedDisplayText() : string.Empty;
        }

        /// <summary>
        /// summary: 根据源槽位所在区域设置预览底色，使背包区和 Spell Book 区保持可区分。
        /// param: slotArea 当前拖拽源槽位所属区域
        /// returns: 无
        /// </summary>
        private void SetArea(BackPackSlotArea slotArea)
        {
            if (background == null)
            {
                return;
            }

            background.color = slotArea == BackPackSlotArea.SpellBook ? spellBookPreviewColor : inventoryPreviewColor;
        }

        /// <summary>
        /// summary: 把预览根节点锚点固定到中心，并同步成源槽位当前尺寸。
        /// param: sourceRect 当前源槽位根节点
        /// returns: 无
        /// </summary>
        private void CopyBoundsFrom(RectTransform firstRect, RectTransform lastRect)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            if (firstRect == null || lastRect == null)
            {
                return;
            }

            float width = Mathf.Abs(lastRect.position.x - firstRect.position.x) + Mathf.Max(firstRect.rect.width, lastRect.rect.width);
            float height = Mathf.Max(firstRect.rect.height, lastRect.rect.height);
            rectTransform.sizeDelta = new Vector2(width, height);
        }
    }
}
