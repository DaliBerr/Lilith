using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// MainUI 的运行时模板脚本，负责暴露 HUD 常用引用，并同步顶部 spell panel 的只读展示。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/MainUI")]
    public class MainUIScreen : GameUIScreen
    {
        [Header("Layout")]
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform healthPanel;
        [SerializeField] private TMP_Text healthTitleText;
        [SerializeField] private RectTransform healthBarRoot;
        [SerializeField] private PlayerHealthBarController healthBarController;
        [SerializeField] private RectTransform spellPanel;
        [SerializeField] private BackPackGridSlotView spellSlotTemplate;
        [SerializeField] private RectTransform pauseButtonRoot;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text pauseButtonText;

        [Header("Linked Outline")]
        [SerializeField] private Color linkedOutlineColor = new(1f, 0.84f, 0.35f, 0.95f);
        [SerializeField, Min(1f)] private float linkedOutlineThickness = 4f;
        [SerializeField] private Vector2 linkedOutlinePadding = new(6f, 6f);

        private readonly List<BackPackGridSlotView> runtimeSpellSlots = new();
        private readonly List<LinkedTokenOutlineView> runtimeSpellOutlines = new();
        private PlayerPlaneMovement currentPlayer;
        private AttackFormulaLoadout currentLoadout;
        private bool hasLoggedMissingSpellTemplate;
        private RectTransform spellLinkedOutlineLayer;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        public RectTransform TopPanel => topPanel;
        public RectTransform HealthPanel => healthPanel;
        public TMP_Text HealthTitleText => healthTitleText;
        public RectTransform HealthBarRoot => healthBarRoot;
        public PlayerHealthBarController HealthBarController => healthBarController;
        public RectTransform SpellPanel => spellPanel;
        public BackPackGridSlotView SpellSlotTemplate => spellSlotTemplate;
        public RectTransform PauseButtonRoot => pauseButtonRoot;
        public Button PauseButton => pauseButton;
        public TMP_Text PauseButtonText => pauseButtonText;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindButtonCallbacks();
            RefreshSpellPanel();
        }

        protected override void OnBeforeShow()
        {
            RefreshSpellPanel();
        }

        protected override void OnAfterHide()
        {
            ReleaseLoadoutBinding();
            ClearRuntimeSpellSlots();
        }

        private void OnDestroy()
        {
            UnbindButtonCallbacks();
            ReleaseLoadoutBinding();
            ClearRuntimeSpellSlots();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Main UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// 按当前 MainUI prefab 的层级自动补齐常用字段，减少手动拖拽成本。
        /// </summary>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
            topPanel ??= transform.Find("TopPanel") as RectTransform;
            if (topPanel == null)
            {
                return;
            }

            healthPanel ??= topPanel.Find("HP Bar") as RectTransform;
            if (healthPanel != null)
            {
                healthTitleText ??= healthPanel.Find("Titlle")?.GetComponent<TMP_Text>();
                healthBarRoot ??= healthPanel.Find("Bar") as RectTransform;
                if (healthBarRoot != null)
                {
                    healthBarController ??= healthBarRoot.GetComponent<PlayerHealthBarController>();
                }
            }

            spellPanel ??= topPanel.Find("Spell Panel") as RectTransform;
            if (spellPanel != null && (spellSlotTemplate == null || !spellSlotTemplate.transform.IsChildOf(spellPanel)))
            {
                spellSlotTemplate = ResolveSpellSlotTemplate(spellPanel);
            }

            pauseButtonRoot ??= ResolvePauseButtonRoot(topPanel);
            if (pauseButtonRoot != null)
            {
                pauseButton ??= pauseButtonRoot.GetComponentInChildren<Button>(true);
                if (pauseButton != null)
                {
                    pauseButtonText ??= pauseButton.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        /// <summary>
        /// summary: 把 MainUI 的暂停按钮接到统一的 UIInputRouter 暂停入口。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            if (pauseButton == null)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(HandlePauseButtonClicked);
            pauseButton.onClick.AddListener(HandlePauseButtonClicked);
        }

        /// <summary>
        /// summary: 清理 MainUI 暂停按钮事件，避免对象销毁后残留无效委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            if (pauseButton == null)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(HandlePauseButtonClicked);
        }

        /// <summary>
        /// summary: 点击暂停按钮时，通过 UIInputRouter 请求打开暂停菜单。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandlePauseButtonClicked()
        {
            UIInputRouter.Instance?.RequestOpenPauseMenu();
        }

        /// <summary>
        /// summary: 重新解析玩家 loadout，并按当前 token 顺序刷新 HUD 顶部的 Spell Panel。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshSpellPanel()
        {
            TryAutoBindReferences();
            if (!PrepareSpellSlotTemplate())
            {
                ClearRuntimeSpellSlots();
                return;
            }

            if (!TryResolveLoadoutBinding())
            {
                ClearRuntimeSpellSlots();
                return;
            }

            RebuildSpellSlots();
        }

        /// <summary>
        /// summary: 使用 MainUI prefab 中预放的首个 BackPack Grid Prefab 作为 runtime 克隆模板。
        /// param: 无
        /// returns: 找到并完成初始化时返回 true
        /// </summary>
        private bool PrepareSpellSlotTemplate()
        {
            if (spellPanel == null)
            {
                return false;
            }

            spellSlotTemplate ??= ResolveSpellSlotTemplate(spellPanel);
            if (spellSlotTemplate == null)
            {
                if (!hasLoggedMissingSpellTemplate)
                {
                    GameDebug.LogWarning("[MainUIScreen] Spell Panel is missing a BackPackGridSlotView template child.");
                    hasLoggedMissingSpellTemplate = true;
                }

                return false;
            }

            hasLoggedMissingSpellTemplate = false;
            EnsureSpellOutlineLayer();
            spellSlotTemplate.InitializeDisplayOnly(BackPackSlotArea.SpellBook);
            spellSlotTemplate.SetToken(null);
            if (spellSlotTemplate.gameObject.activeSelf)
            {
                spellSlotTemplate.gameObject.SetActive(false);
            }

            return true;
        }

        /// <summary>
        /// summary: 解析场景中的玩家与 AttackFormulaLoadout，并维护 HUD 订阅的事件绑定。
        /// param: 无
        /// returns: 成功拿到可观察的 loadout 时返回 true
        /// </summary>
        private bool TryResolveLoadoutBinding()
        {
            PlayerPlaneMovement resolvedPlayer = FindAnyObjectByType<PlayerPlaneMovement>();
            if (resolvedPlayer == null)
            {
                ReleaseLoadoutBinding();
                return false;
            }

            AttackFormulaLoadout resolvedLoadout = resolvedPlayer.GetComponent<AttackFormulaLoadout>();
            if (resolvedLoadout == null)
            {
                ReleaseLoadoutBinding();
                return false;
            }

            currentPlayer = resolvedPlayer;
            if (currentLoadout == resolvedLoadout)
            {
                return true;
            }

            if (currentLoadout != null)
            {
                currentLoadout.Changed -= HandleLoadoutChanged;
            }

            currentLoadout = resolvedLoadout;
            currentLoadout.Changed += HandleLoadoutChanged;
            return true;
        }

        /// <summary>
        /// summary: 释放当前 loadout 事件订阅，避免界面销毁后继续收到刷新回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ReleaseLoadoutBinding()
        {
            if (currentLoadout != null)
            {
                currentLoadout.Changed -= HandleLoadoutChanged;
            }

            currentPlayer = null;
            currentLoadout = null;
        }

        /// <summary>
        /// summary: 按当前 loadout 中的非空 token 顺序重建 Spell Panel 的可见槽位，不显示空占位。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RebuildSpellSlots()
        {
            ClearRuntimeSpellSlots();
            if (spellPanel == null || spellSlotTemplate == null || currentLoadout == null)
            {
                return;
            }

            IReadOnlyList<PlaceableTokenData> items = currentLoadout.Items;
            if (items == null)
            {
                return;
            }

            int visibleIndex = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PlaceableTokenData item = items[i];
                if (item == null)
                {
                    continue;
                }

                int anchorIndex = visibleIndex;
                int span = Mathf.Max(1, item.SlotSpan);
                for (int offset = 0; offset < span; offset++)
                {
                    BackPackGridSlotView slotView = Instantiate(spellSlotTemplate, spellPanel);
                    slotView.gameObject.SetActive(true);
                    slotView.name = $"Spell Slot {visibleIndex + 1:D2}";
                    slotView.InitializeDisplayOnly(BackPackSlotArea.SpellBook);
                    slotView.SetOccupancy(new TokenCellOccupancy(item, anchorIndex, offset, offset == 0));
                    runtimeSpellSlots.Add(slotView);
                    visibleIndex++;
                }
            }

            RefreshSpellOutlines();
        }

        /// <summary>
        /// summary: 清理 Spell Panel 下当前由 MainUIScreen 运行时克隆出的槽位实例。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ClearRuntimeSpellSlots()
        {
            for (int i = runtimeSpellSlots.Count - 1; i >= 0; i--)
            {
                DestroyChild(runtimeSpellSlots[i] != null ? runtimeSpellSlots[i].gameObject : null);
            }

            runtimeSpellSlots.Clear();
            SetLinkedOutlineViewsVisible(runtimeSpellOutlines, 0);
        }

        /// <summary>
        /// summary: 当 loadout 内容被背包或其他系统改写后，立即刷新 HUD 顶部展示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleLoadoutChanged()
        {
            RefreshSpellPanel();
        }

        /// <summary>
        /// 兼容当前 MainUI prefab 中已有的暂停按钮节点命名。
        /// </summary>
        /// <param name="root">顶部面板根节点。</param>
        /// <returns>暂停按钮根节点；未找到时返回 null。</returns>
        private static RectTransform ResolvePauseButtonRoot(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            return root.Find("Pause Btn") as RectTransform
                ?? root.Find("Pause Button") as RectTransform;
        }

        /// <summary>
        /// summary: 解析 Spell Panel 下首个带 BackPackGridSlotView 的子节点，作为运行时模板来源。
        /// param: root Spell Panel 根节点
        /// returns: 可复用的模板槽位；未找到时返回 null
        /// </summary>
        private static BackPackGridSlotView ResolveSpellSlotTemplate(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                BackPackGridSlotView slotView = root.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView != null)
                {
                    return slotView;
                }
            }

            return null;
        }

        private void EnsureSpellOutlineLayer()
        {
            RectTransform parent = spellPanel != null ? spellPanel.parent as RectTransform : topPanel;
            if (parent == null)
            {
                spellLinkedOutlineLayer = null;
                return;
            }

            if (spellLinkedOutlineLayer == null)
            {
                GameObject layerObject = new("MainSpellLinkedOutlineLayer", typeof(RectTransform), typeof(CanvasGroup));
                layerObject.layer = gameObject.layer;
                spellLinkedOutlineLayer = layerObject.GetComponent<RectTransform>();
                spellLinkedOutlineLayer.SetParent(parent, false);
                spellLinkedOutlineLayer.anchorMin = Vector2.zero;
                spellLinkedOutlineLayer.anchorMax = Vector2.one;
                spellLinkedOutlineLayer.offsetMin = Vector2.zero;
                spellLinkedOutlineLayer.offsetMax = Vector2.zero;
                spellLinkedOutlineLayer.localScale = Vector3.one;

                CanvasGroup canvasGroup = layerObject.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            spellLinkedOutlineLayer.SetAsLastSibling();
        }

        private void RefreshSpellOutlines()
        {
            EnsureSpellOutlineLayer();
            if (spellPanel == null || spellLinkedOutlineLayer == null)
            {
                SetLinkedOutlineViewsVisible(runtimeSpellOutlines, 0);
                return;
            }

            spellLinkedOutlineLayer.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(spellPanel);

            int visibleCount = 0;
            for (int i = 0; i < runtimeSpellSlots.Count; i++)
            {
                BackPackGridSlotView slot = runtimeSpellSlots[i];
                if (slot == null || slot.Item == null || !slot.IsAnchor || slot.Item.SlotSpan <= 1)
                {
                    continue;
                }

                int endIndex = slot.AnchorIndex + slot.Item.SlotSpan - 1;
                if (endIndex < 0 || endIndex >= runtimeSpellSlots.Count)
                {
                    continue;
                }

                BackPackGridSlotView endSlot = runtimeSpellSlots[endIndex];
                if (endSlot == null || slot.SlotRectTransform == null || endSlot.SlotRectTransform == null)
                {
                    continue;
                }

                LinkedTokenOutlineView outlineView = GetOrCreateLinkedOutlineView(visibleCount);
                outlineView.ApplyStyle(linkedOutlineColor, linkedOutlineThickness);
                outlineView.FitToSlots(spellLinkedOutlineLayer, slot.SlotRectTransform, endSlot.SlotRectTransform, linkedOutlinePadding);
                outlineView.gameObject.SetActive(true);
                visibleCount++;
            }

            SetLinkedOutlineViewsVisible(runtimeSpellOutlines, visibleCount);
        }

        private LinkedTokenOutlineView GetOrCreateLinkedOutlineView(int index)
        {
            while (runtimeSpellOutlines.Count <= index)
            {
                LinkedTokenOutlineView outlineView = LinkedTokenOutlineView.CreateRuntime($"Main Spell Linked Outline {runtimeSpellOutlines.Count + 1:D2}", spellLinkedOutlineLayer, gameObject.layer);
                outlineView.gameObject.SetActive(false);
                runtimeSpellOutlines.Add(outlineView);
            }

            return runtimeSpellOutlines[index];
        }

        private static void SetLinkedOutlineViewsVisible(List<LinkedTokenOutlineView> outlineViews, int visibleCount)
        {
            if (outlineViews == null)
            {
                return;
            }

            for (int i = 0; i < outlineViews.Count; i++)
            {
                if (outlineViews[i] != null)
                {
                    outlineViews[i].gameObject.SetActive(i < visibleCount);
                }
            }
        }

        /// <summary>
        /// summary: 统一销毁一个运行时 spell 槽位实例，兼容 Play Mode 与 Edit Mode。
        /// param: child 需要销毁的子节点对象
        /// returns: 无
        /// </summary>
        private static void DestroyChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
