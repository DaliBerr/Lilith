using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using UnityEngine;
using UnityEngine.EventSystems;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// BackPackUI 的运行时控制器，负责生成 48 格背包槽位、接管 Spell Book 编译入口，并同步玩家库存与 loadout。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/BackPackUI")]
    public sealed class BackPackUIScreen : GameUIScreen
    {
        private const int InventorySlotCount = PlayerBulletTokenInventory.Capacity;
        private const int SpellBookSlotCount = 5;

        [Header("Layout")]
        [SerializeField] private RectTransform mainContent;
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform spellBook;
        [SerializeField] private RectTransform leftPanel;
        [SerializeField] private RectTransform previewAnimation;
        [SerializeField] private RectTransform backPackGridPanel;
        [SerializeField] private RectTransform backPackGrid;
        [SerializeField] private BackPackGridSlotView slotPrefab;

        private readonly List<BackPackGridSlotView> inventorySlots = new(InventorySlotCount);
        private readonly List<BackPackGridSlotView> spellBookSlots = new(SpellBookSlotCount);
        private readonly BaseTokenData[] spellBookBuffer = new BaseTokenData[SpellBookSlotCount];

        private PlayerPlaneMovement currentPlayer;
        private PlayerBulletTokenInventory currentInventory;
        private AttackFormulaLoadout currentLoadout;
        private BackPackGridSlotView activeDragSource;
        private BaseTokenData activeDragToken;
        private RectTransform dragPreviewLayer;
        private BackPackDragPreviewView dragPreviewView;

        public override Status currentStatus { get; } = StatusList.InBackPackStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindStaticSpellBookSlots();
            EnsureInventorySlotsBuilt();
            EnsureDragPreviewLayer();
        }

        protected override void OnBeforeShow()
        {
            RefreshFromCurrentPlayer();
        }

        protected override void OnAfterHide()
        {
            ReleaseBindings();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        private void OnDestroy()
        {
            ReleaseBindings();
            RemoveCurrentStatus();
        }

        [ContextMenu("Auto Bind BackPack UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 供后续输入层调用，重新解析场景中的玩家对象，并把库存与 Spell Book 刷到当前背包界面。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RefreshFromCurrentPlayer()
        {
            CancelActiveDragSession();
            TryAutoBindReferences();
            BindStaticSpellBookSlots();
            EnsureInventorySlotsBuilt();

            if (!TryResolvePlayerBindings())
            {
                ClearAllSlots();
                return;
            }

            currentInventory.EnsureInitialized();
            RefreshInventorySlots();
            PopulateSpellBookFromLoadout();
        }

        /// <summary>
        /// summary: 供后续输入层调用，请求关闭当前背包界面。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            CancelActiveDragSession();
            if (ui == null)
            {
                return;
            }

            if (ui.GetTopModal() == this)
            {
                ui.CloseTopModal();
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        /// <summary>
        /// summary: 从槽位视图开始一次拖拽操作，仅记录来源并等待目标槽位回调。
        /// param: source 拖拽来源槽位
        /// returns: 无
        /// </summary>
        public void NotifySlotBeginDrag(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (source == null)
            {
                return;
            }

            BaseTokenData sourceToken = GetSlotToken(source);
            if (sourceToken == null)
            {
                return;
            }

            CancelActiveDragSession();
            activeDragSource = source;
            activeDragToken = sourceToken;
            ShowDragPreview(source, eventData);
        }

        /// <summary>
        /// summary: 由源槽位在拖拽过程中持续回调，用当前鼠标位置驱动运行时预览副本跟手移动。
        /// param: source 本次拖拽的源槽位
        /// param: eventData 当前拖拽事件数据
        /// returns: 无
        /// </summary>
        public void NotifySlotDrag(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (source == null || source != activeDragSource || dragPreviewView == null || !dragPreviewView.gameObject.activeSelf || eventData == null)
            {
                return;
            }

            MoveDragPreview(eventData.position, eventData.pressEventCamera);
        }

        /// <summary>
        /// summary: 由目标槽位在接收 drop 时调用，按照背包与 Spell Book 的交换规则落地数据改动。
        /// param: target 实际接收到 drop 的目标槽位
        /// returns: 无
        /// </summary>
        public void NotifySlotDrop(BackPackGridSlotView target)
        {
            if (activeDragSource == null || target == null || activeDragSource == target)
            {
                return;
            }

            BaseTokenData sourceToken = activeDragToken;
            if (sourceToken == null)
            {
                return;
            }

            BaseTokenData targetToken = GetSlotToken(target);
            SetSlotToken(activeDragSource, targetToken);
            SetSlotToken(target, sourceToken);
            if (activeDragSource.Area == BackPackSlotArea.SpellBook || target.Area == BackPackSlotArea.SpellBook)
            {
                SyncSpellBookToLoadout();
            }
        }

        /// <summary>
        /// summary: 结束一次拖拽生命周期；若没有有效 drop，则当前数据保持原样不变。
        /// param: source 本次拖拽的源槽位
        /// returns: 无
        /// </summary>
        public void NotifySlotEndDrag(BackPackGridSlotView source)
        {
            if (activeDragSource != null && activeDragSource != source)
            {
                return;
            }

            HideDragPreview();
            activeDragSource = null;
            activeDragToken = null;
        }

        /// <summary>
        /// summary: 运行时创建一个专用拖拽预览层，并始终保持在背包界面最上方，不复用 Preview Animation 面板。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureDragPreviewLayer()
        {
            if (dragPreviewLayer != null)
            {
                dragPreviewLayer.SetAsLastSibling();
                return;
            }

            RectTransform screenRoot = transform as RectTransform;
            if (screenRoot == null)
            {
                return;
            }

            GameObject layerObject = new("DragPreviewLayer", typeof(RectTransform), typeof(CanvasGroup));
            layerObject.layer = gameObject.layer;
            dragPreviewLayer = layerObject.GetComponent<RectTransform>();
            dragPreviewLayer.SetParent(screenRoot, false);
            dragPreviewLayer.anchorMin = Vector2.zero;
            dragPreviewLayer.anchorMax = Vector2.one;
            dragPreviewLayer.offsetMin = Vector2.zero;
            dragPreviewLayer.offsetMax = Vector2.zero;
            dragPreviewLayer.localScale = Vector3.one;
            dragPreviewLayer.SetAsLastSibling();

            CanvasGroup layerCanvasGroup = layerObject.GetComponent<CanvasGroup>();
            layerCanvasGroup.alpha = 1f;
            layerCanvasGroup.blocksRaycasts = false;
            layerCanvasGroup.interactable = false;
        }

        /// <summary>
        /// summary: 确保存在一个可复用的完整槽位拖拽预览实例；它只负责显示，不参与交互。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureDragPreviewView()
        {
            if (dragPreviewView != null)
            {
                return;
            }

            EnsureDragPreviewLayer();
            if (dragPreviewLayer == null || slotPrefab == null)
            {
                return;
            }

            GameObject previewObject = Instantiate(slotPrefab.gameObject, dragPreviewLayer);
            previewObject.name = "BackPack Drag Preview";
            previewObject.layer = gameObject.layer;

            BackPackGridSlotView previewSlotView = previewObject.GetComponent<BackPackGridSlotView>();
            if (previewSlotView != null)
            {
                previewSlotView.enabled = false;
            }

            dragPreviewView = previewObject.GetComponent<BackPackDragPreviewView>() ?? previewObject.AddComponent<BackPackDragPreviewView>();
            previewObject.SetActive(false);
        }

        /// <summary>
        /// summary: 按当前源槽位生成或刷新拖拽预览，并立即定位到本次拖拽起点。
        /// param: source 当前拖拽源槽位
        /// param: eventData 当前拖拽事件数据
        /// returns: 无
        /// </summary>
        private void ShowDragPreview(BackPackGridSlotView source, PointerEventData eventData)
        {
            EnsureDragPreviewView();
            if (dragPreviewView == null)
            {
                return;
            }

            dragPreviewView.gameObject.SetActive(true);
            dragPreviewView.InitializeFromSlot(source);
            dragPreviewView.transform.SetAsLastSibling();
            if (eventData != null)
            {
                MoveDragPreview(eventData.position, eventData.pressEventCamera);
            }
        }

        /// <summary>
        /// summary: 按当前鼠标位置更新拖拽预览的 anchoredPosition，保证预览副本在背包界面根节点下稳定跟手。
        /// param: screenPosition 当前鼠标屏幕坐标
        /// param: eventCamera 当前 UI 事件相机；Overlay 模式下允许为空
        /// returns: 无
        /// </summary>
        private void MoveDragPreview(Vector2 screenPosition, Camera eventCamera)
        {
            if (dragPreviewView == null || dragPreviewLayer == null)
            {
                return;
            }

            dragPreviewView.UpdateScreenPosition(dragPreviewLayer, screenPosition, eventCamera);
        }

        /// <summary>
        /// summary: 隐藏当前拖拽预览实例，不销毁对象以便下一次拖拽复用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HideDragPreview()
        {
            if (dragPreviewView == null)
            {
                return;
            }

            dragPreviewView.gameObject.SetActive(false);
        }

        /// <summary>
        /// summary: 强制结束当前拖拽会话，恢复源槽位射线状态并清理预览，避免关闭界面或刷新时残留中间态。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void CancelActiveDragSession()
        {
            if (activeDragSource != null)
            {
                activeDragSource.ResetDragPresentation();
            }

            HideDragPreview();
            activeDragSource = null;
            activeDragToken = null;
        }

        /// <summary>
        /// summary: 按当前 BackPackUI prefab 的层级自动补齐常用字段，减少手动拖拽成本。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            mainContent ??= transform.Find("MainContent") as RectTransform;
            if (mainContent == null)
            {
                return;
            }

            topPanel ??= mainContent.Find("Top Panel") as RectTransform;
            spellBook ??= topPanel?.Find("Spell Book") as RectTransform;
            leftPanel ??= mainContent.Find("Left Panel") as RectTransform;
            previewAnimation ??= leftPanel?.Find("Preview Animation") as RectTransform;
            backPackGridPanel ??= mainContent.Find("BackPack Grid Panel") as RectTransform;
            backPackGrid ??= backPackGridPanel?.Find("Grid") as RectTransform;
            slotPrefab ??= ResolveTemplateSlot();
        }

        /// <summary>
        /// summary: 绑定 prefab 中预放的 5 个 Spell Book 槽位，不在运行时额外创建这部分结构。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindStaticSpellBookSlots()
        {
            spellBookSlots.Clear();
            if (spellBook == null)
            {
                return;
            }

            for (int i = 0; i < spellBook.childCount; i++)
            {
                BackPackGridSlotView slotView = spellBook.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView == null)
                {
                    continue;
                }

                slotView.Initialize(this, BackPackSlotArea.SpellBook, spellBookSlots.Count);
                spellBookSlots.Add(slotView);
            }

            if (spellBookSlots.Count != SpellBookSlotCount)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Spell Book expects {SpellBookSlotCount} static slots, but found {spellBookSlots.Count}.");
            }
        }

        /// <summary>
        /// summary: 运行时确保背包区域存在准确的 48 个槽位；数量不对时会整组重建。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureInventorySlotsBuilt()
        {
            inventorySlots.Clear();
            if (backPackGrid == null || slotPrefab == null)
            {
                return;
            }

            bool needRebuild = backPackGrid.childCount != InventorySlotCount;
            if (!needRebuild)
            {
                for (int i = 0; i < backPackGrid.childCount; i++)
                {
                    if (backPackGrid.GetChild(i).GetComponent<BackPackGridSlotView>() == null)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                RebuildInventorySlots();
            }

            for (int i = 0; i < backPackGrid.childCount; i++)
            {
                BackPackGridSlotView slotView = backPackGrid.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView == null)
                {
                    continue;
                }

                slotView.Initialize(this, BackPackSlotArea.Inventory, inventorySlots.Count);
                inventorySlots.Add(slotView);
            }
        }

        /// <summary>
        /// summary: 当玩家对象存在时，缓存其移动、库存与编译 loadout 组件，并同步库存刷新事件。
        /// param: 无
        /// returns: 成功解析到玩家、库存和 loadout 时返回 true
        /// </summary>
        private bool TryResolvePlayerBindings()
        {
            currentPlayer = FindAnyObjectByType<PlayerPlaneMovement>();
            if (currentPlayer == null)
            {
                ReleaseBindings();
                GameDebug.LogWarning("[BackPackUIScreen] No PlayerPlaneMovement was found in the active scene.");
                return false;
            }

            PlayerBulletTokenInventory inventory = currentPlayer.GetComponent<PlayerBulletTokenInventory>();
            AttackFormulaLoadout loadout = currentPlayer.GetComponent<AttackFormulaLoadout>();
            if (inventory == null || loadout == null)
            {
                ReleaseBindings();
                GameDebug.LogWarning("[BackPackUIScreen] Player is missing PlayerBulletTokenInventory or AttackFormulaLoadout.");
                return false;
            }

            if (currentInventory != inventory)
            {
                if (currentInventory != null)
                {
                    currentInventory.Changed -= HandleInventoryChanged;
                }

                currentInventory = inventory;
                currentInventory.Changed += HandleInventoryChanged;
            }

            currentLoadout = loadout;
            return true;
        }

        /// <summary>
        /// summary: 把当前玩家库存同步到 48 个背包槽位视图。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshInventorySlots()
        {
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                BaseTokenData token = currentInventory != null ? currentInventory.GetToken(i) : null;
                inventorySlots[i].SetToken(token);
            }
        }

        /// <summary>
        /// summary: 用当前 loadout 的顺序填充 Spell Book，并把超过 5 个的历史 token 尝试回填到背包库存。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void PopulateSpellBookFromLoadout()
        {
            for (int i = 0; i < spellBookBuffer.Length; i++)
            {
                spellBookBuffer[i] = null;
            }

            int droppedOverflowCount = 0;
            BackPackTokenLayoutUtility.PopulateSpellBookSlots(currentLoadout != null ? currentLoadout.Tokens : null, spellBookBuffer, currentInventory, out droppedOverflowCount);
            for (int i = 0; i < spellBookSlots.Count && i < spellBookBuffer.Length; i++)
            {
                spellBookSlots[i].SetToken(spellBookBuffer[i]);
            }

            if (droppedOverflowCount > 0)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Dropped {droppedOverflowCount} overflow token(s) because the player inventory is full.");
            }

            RefreshInventorySlots();
            SyncSpellBookToLoadout();
        }

        /// <summary>
        /// summary: 收集 Spell Book 当前的非空 token，并在顺序发生变化时实时写回 AttackFormulaLoadout。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SyncSpellBookToLoadout()
        {
            if (currentLoadout == null)
            {
                return;
            }

            List<BaseTokenData> nextTokens = BackPackTokenLayoutUtility.BuildCompactLoadoutTokens(GetSpellBookTokenSnapshot());
            if (BackPackTokenLayoutUtility.SequenceEquals(currentLoadout.Tokens, nextTokens))
            {
                return;
            }

            currentLoadout.SetTokens(nextTokens);
        }

        /// <summary>
        /// summary: 复制当前 Spell Book 五个槽位中的 token 快照，用于编译同步与测试友好的规则复用。
        /// param: 无
        /// returns: 当前 Spell Book 的有序 token 快照
        /// </summary>
        private List<BaseTokenData> GetSpellBookTokenSnapshot()
        {
            List<BaseTokenData> snapshot = new(spellBookSlots.Count);
            for (int i = 0; i < spellBookSlots.Count; i++)
            {
                snapshot.Add(spellBookSlots[i].Token);
            }

            return snapshot;
        }

        /// <summary>
        /// summary: 从静态 Spell Book 槽位中推导一个可用于 runtime 克隆的模板槽位。
        /// param: 无
        /// returns: 找到的槽位模板；未找到时返回 null
        /// </summary>
        private BackPackGridSlotView ResolveTemplateSlot()
        {
            if (spellBook == null)
            {
                return null;
            }

            for (int i = 0; i < spellBook.childCount; i++)
            {
                BackPackGridSlotView slotView = spellBook.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView != null)
                {
                    return slotView;
                }
            }

            return null;
        }

        /// <summary>
        /// summary: 清空现有 runtime 槽位并重建固定 48 个背包格子。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RebuildInventorySlots()
        {
            CancelActiveDragSession();
            for (int i = backPackGrid.childCount - 1; i >= 0; i--)
            {
                DestroyChild(backPackGrid.GetChild(i).gameObject);
            }

            for (int i = 0; i < InventorySlotCount; i++)
            {
                BackPackGridSlotView slotView = Instantiate(slotPrefab, backPackGrid);
                slotView.name = $"BackPack Slot {i + 1:D2}";
                slotView.Initialize(this, BackPackSlotArea.Inventory, i);
            }
        }

        /// <summary>
        /// summary: 根据槽位所属区域读取当前 token，统一背包和 Spell Book 的交换实现。
        /// param: slot 需要读取的槽位
        /// returns: 槽位中的 token；无效时返回 null
        /// </summary>
        private BaseTokenData GetSlotToken(BackPackGridSlotView slot)
        {
            if (slot == null)
            {
                return null;
            }

            return slot.Area == BackPackSlotArea.Inventory ? currentInventory?.GetToken(slot.SlotIndex) : slot.Token;
        }

        /// <summary>
        /// summary: 根据槽位所属区域写入 token；写入 Spell Book 时直接改视图，写入背包时回写库存组件。
        /// param: slot 需要写入的槽位
        /// param: token 需要写入的 token；传入 null 表示清空
        /// returns: 无
        /// </summary>
        private void SetSlotToken(BackPackGridSlotView slot, BaseTokenData token)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.Area == BackPackSlotArea.Inventory)
            {
                currentInventory?.SetToken(slot.SlotIndex, token);
                return;
            }

            if (slot.SlotIndex >= 0 && slot.SlotIndex < spellBookSlots.Count)
            {
                spellBookSlots[slot.SlotIndex].SetToken(token);
            }
        }

        /// <summary>
        /// summary: 在玩家对象缺失或背包关闭时清理缓存引用与事件订阅，避免状态残留。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ReleaseBindings()
        {
            CancelActiveDragSession();
            if (currentInventory != null)
            {
                currentInventory.Changed -= HandleInventoryChanged;
            }

            currentPlayer = null;
            currentInventory = null;
            currentLoadout = null;
        }

        /// <summary>
        /// summary: 显式移除 BackPack 状态，补齐 GameUIScreen 只加不减的现有行为。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        /// <summary>
        /// summary: 在玩家库存发生变化时刷新 48 个背包槽位，不重新构建 Spell Book。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleInventoryChanged()
        {
            RefreshInventorySlots();
        }

        /// <summary>
        /// summary: 把界面上的所有背包与 Spell Book 槽位重置为空，供缺少玩家引用时退化显示使用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ClearAllSlots()
        {
            CancelActiveDragSession();
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                inventorySlots[i].SetToken(null);
            }

            for (int i = 0; i < spellBookSlots.Count; i++)
            {
                spellBookSlots[i].SetToken(null);
            }
        }

        /// <summary>
        /// summary: 统一销毁一个运行时子节点，兼容 Play Mode 与 Edit Mode。
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
