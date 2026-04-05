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
    /// 背包单槽位视图，负责显示 token 文本并把拖拽事件转发给 BackPackUIScreen。
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

        private BackPackUIScreen owner;
        private BaseTokenData token;
        private bool isDragging;
        private BackPackSlotArea area;
        private int slotIndex;
        private RectTransform rectTransform;

        public BackPackSlotArea Area => area;
        public int SlotIndex => slotIndex;
        public BaseTokenData Token => token;
        public RectTransform SlotRectTransform => rectTransform;

        private void Awake()
        {
            EnsureLocalReferences();
            ApplyVisualState();
        }

        private void OnValidate()
        {
            EnsureLocalReferences();
            ApplyVisualState();
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
            owner = ownerScreen;
            area = slotArea;
            slotIndex = index;
            ApplyVisualState();
        }

        /// <summary>
        /// summary: 更新当前槽位承载的 token，并刷新显示文本。
        /// param: value 需要显示的新 token；空槽时传入 null
        /// returns: 无
        /// </summary>
        public void SetToken(BaseTokenData value)
        {
            token = value;
            RefreshText();
            ApplyVisualState();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner == null || token == null)
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
            owner?.NotifySlotDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResetDragPresentation();
            owner?.NotifySlotEndDrag(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            owner?.NotifySlotDrop(this);
        }

        /// <summary>
        /// summary: 强制恢复当前槽位的拖拽表现，确保关闭背包或异常中断时不会残留半透明和禁用射线状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void ResetDragPresentation()
        {
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
        /// summary: 根据当前 token 和拖拽状态更新槽位上的文本显示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshText()
        {
            if (tokenText == null)
            {
                return;
            }

            tokenText.text = token != null ? token.GetResolvedDisplayText() : string.Empty;
        }

        /// <summary>
        /// summary: 应用空槽、Spell Book 和拖拽中的基础视觉反馈，不额外引入复杂表现层。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ApplyVisualState()
        {
            if (background != null)
            {
                background.color = isDragging ? dragHighlightColor : (area == BackPackSlotArea.SpellBook ? spellBookIdleColor : inventoryIdleColor);
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

            canvasGroup.blocksRaycasts = shouldBlock;
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
            EnsureLocalReferences();
            if (source == null)
            {
                return;
            }

            SetToken(source.Token);
            SetArea(source.Area);
            CopySizeFrom(source.SlotRectTransform);
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
        private void CopySizeFrom(RectTransform sourceRect)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            if (sourceRect != null)
            {
                rectTransform.sizeDelta = sourceRect.rect.size;
            }
        }
    }
}
