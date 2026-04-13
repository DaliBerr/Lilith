using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using Kernel.Quest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        [SerializeField] private BackPackAttackPreviewController attackPreviewController;

        [Header("Hover Preview")]
        [SerializeField] private BulletTokenSelectionView hoverPreviewPrefab;
        [SerializeField] private Vector2 hoverPreviewScreenOffset = new(28f, -28f);

        [Header("Linked Outline")]
        [SerializeField] private Color linkedOutlineColor = new(1f, 0.84f, 0.35f, 0.95f);
        [SerializeField, Min(1f)] private float linkedOutlineThickness = 4f;
        [SerializeField] private Vector2 linkedOutlinePadding = new(6f, 6f);

        private readonly List<BackPackGridSlotView> inventorySlots = new(InventorySlotCount);
        private readonly List<BackPackGridSlotView> spellBookSlots = new(SpellBookSlotCount);
        private readonly List<TokenCellOccupancy> spellBookCells = new(SpellBookSlotCount);
        private readonly List<LinkedTokenOutlineView> inventoryLinkedOutlines = new();
        private readonly List<LinkedTokenOutlineView> spellBookLinkedOutlines = new();

        private PlayerPlaneMovement currentPlayer;
        private PlayerBulletTokenInventory currentInventory;
        private AttackFormulaLoadout currentLoadout;
        private BackPackGridSlotView activeDragSource;
        private PlaceableTokenData activeDragItem;
        private int activeDragSourceAnchorIndex = -1;
        private int activeDragGrabOffset;
        private BackPackSlotArea activeDragSourceArea;
        private RectTransform dragPreviewLayer;
        private BackPackDragPreviewView dragPreviewView;
        private RectTransform inventoryLinkedOutlineLayer;
        private RectTransform spellBookLinkedOutlineLayer;
        private LinkedTokenOutlineView dragLinkedOutlineView;
        private Vector2 dragPreviewScreenOffset;
        private Vector2 dragLinkedOutlineScreenOffset;
        private BulletTokenSelectionView hoverPreviewView;
        private BackPackGridSlotView activeHoverSlot;
        private PlaceableTokenData activeHoverItem;

        public override Status currentStatus { get; } = StatusList.InBackPackStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            EnsureSpellBookCellsInitialized();
            BindStaticSpellBookSlots();
            EnsureInventorySlotsBuilt();
            EnsureDragPreviewLayer();
            EnsureLinkedOutlineLayers();
            ApplyBackPackGridLayoutDefaults();
            HideHoverPreview();
        }

        protected override void OnBeforeShow()
        {
            RefreshFromCurrentPlayer();
            TryMarkStoryFlag(TutorialQuestConstants.BackpackOpenedFlagId);
        }

        protected override void OnAfterHide()
        {
            ReleaseBindings();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            ApplyBackPackGridLayoutDefaults();
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
            ApplyBackPackGridLayoutDefaults();
        }

        /// <summary>
        /// summary: 重新解析场景中的玩家对象，并把库存与 Spell Book 刷到当前背包界面。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RefreshFromCurrentPlayer()
        {
            CancelActiveDragSession();
            HideHoverPreview();
            TryAutoBindReferences();
            ApplyBackPackGridLayoutDefaults();
            EnsureSpellBookCellsInitialized();
            BindStaticSpellBookSlots();
            EnsureInventorySlotsBuilt();

            if (!TryResolvePlayerBindings())
            {
                ClearAllSlots();
                attackPreviewController?.ClearPreview();
                return;
            }

            currentInventory.EnsureInitialized();
            PopulateSpellBookFromLoadout();
            RefreshInventorySlots();
            RefreshSpellBookSlots();
            RefreshAttackPreview();
        }

        /// <summary>
        /// summary: 供后续输入层调用，请求关闭当前背包界面；优先走 modal 关闭路径，兼容旧的 screen 打开方式。
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
                ui.CloseModal(this);
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        /// <summary>
        /// summary: 从槽位视图开始一次拖拽操作，记录来源 item 与抓取偏移。
        /// param: source 拖拽来源槽位
        /// param: eventData 当前拖拽事件数据
        /// returns: 无
        /// </summary>
        public void NotifySlotBeginDrag(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (source == null || source.Item == null)
            {
                return;
            }

            CancelActiveDragSession();
            HideHoverPreview();
            activeDragSource = source;
            activeDragItem = source.Item;
            activeDragSourceAnchorIndex = source.AnchorIndex >= 0 ? source.AnchorIndex : source.SlotIndex - source.LocalOffset;
            activeDragGrabOffset = source.LocalOffset;
            activeDragSourceArea = source.Area;
            ShowDragPreview(source, eventData);
            RefreshLinkedOutlines();
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
        /// summary: 由目标槽位在接收 drop 时调用，尝试按 item 级规则完成整件移动。
        /// param: target 实际接收到 drop 的目标槽位
        /// returns: 无
        /// </summary>
        public void NotifySlotDrop(BackPackGridSlotView target)
        {
            if (activeDragSource == null || activeDragItem == null || target == null)
            {
                return;
            }

            int targetAnchorIndex = target.SlotIndex - activeDragGrabOffset;
            if (targetAnchorIndex < 0)
            {
                return;
            }

            bool changed = activeDragSourceArea switch
            {
                BackPackSlotArea.Inventory when target.Area == BackPackSlotArea.Inventory => TryMoveInventoryItem(targetAnchorIndex),
                BackPackSlotArea.Inventory when target.Area == BackPackSlotArea.SpellBook => TryMoveInventoryItemToSpellBook(targetAnchorIndex),
                BackPackSlotArea.SpellBook when target.Area == BackPackSlotArea.Inventory => TryMoveSpellBookItemToInventory(targetAnchorIndex),
                BackPackSlotArea.SpellBook when target.Area == BackPackSlotArea.SpellBook => TryMoveSpellBookItem(targetAnchorIndex),
                _ => false,
            };

            if (!changed)
            {
                return;
            }

            RefreshInventorySlots();
            RefreshSpellBookSlots();
            if (activeDragSourceArea == BackPackSlotArea.SpellBook || target.Area == BackPackSlotArea.SpellBook)
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
            activeDragItem = null;
            activeDragSourceAnchorIndex = -1;
            activeDragGrabOffset = 0;
            activeDragSourceArea = BackPackSlotArea.Inventory;
            dragPreviewScreenOffset = Vector2.zero;
            dragLinkedOutlineScreenOffset = Vector2.zero;
            RefreshLinkedOutlines();
        }

        /// <summary>
        /// summary: 响应槽位悬停进入事件，显示当前槽位承载 token 的效果预览。
        /// param: source 当前悬停进入的槽位
        /// param: eventData 当前指针事件数据
        /// returns: 无
        /// </summary>
        public void NotifySlotHoverEnter(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (activeDragSource != null)
            {
                HideHoverPreview();
                return;
            }

            ShowHoverPreview(source, eventData);
        }

        /// <summary>
        /// summary: 响应槽位悬停移动事件，驱动预览卡片跟随当前鼠标位置。
        /// param: source 当前悬停中的槽位
        /// param: eventData 当前指针事件数据
        /// returns: 无
        /// </summary>
        public void NotifySlotHoverMove(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (source == null || source != activeHoverSlot || activeDragSource != null)
            {
                return;
            }

            if (source.Item == null)
            {
                HideHoverPreview();
                return;
            }

            if (hoverPreviewView == null || !hoverPreviewView.gameObject.activeSelf)
            {
                ShowHoverPreview(source, eventData);
                return;
            }

            if (activeHoverItem != source.Item)
            {
                BindHoverPreviewToken(source.Item);
            }

            MoveHoverPreview(ResolveHoverScreenPosition(source, eventData), ResolvePointerEventCamera(eventData));
        }

        /// <summary>
        /// summary: 响应槽位悬停离开事件，隐藏当前预览卡片。
        /// param: source 当前离开的槽位
        /// returns: 无
        /// </summary>
        public void NotifySlotHoverExit(BackPackGridSlotView source)
        {
            if (source == null || source != activeHoverSlot)
            {
                return;
            }

            HideHoverPreview();
        }

        private bool TryMoveInventoryItem(int targetAnchorIndex)
        {
            if (currentInventory == null || activeDragItem == null)
            {
                return false;
            }

            if (currentInventory.TryMoveItem(activeDragSourceAnchorIndex, targetAnchorIndex))
            {
                return true;
            }

            if (!TryGetSingleInventoryConflict(targetAnchorIndex, activeDragItem, activeDragSourceAnchorIndex, out PlaceableTokenData targetItem, out int targetItemAnchorIndex))
            {
                return false;
            }

            return TrySwapInventoryItems(activeDragSourceAnchorIndex, targetAnchorIndex, activeDragItem, targetItemAnchorIndex, targetItem);
        }

        private bool TryMoveInventoryItemToSpellBook(int targetAnchorIndex)
        {
            if (currentInventory == null || activeDragItem == null)
            {
                return false;
            }

            if (TryPlaceSpellBookItem(targetAnchorIndex, activeDragItem))
            {
                if (!currentInventory.TryRemoveItemAtCell(activeDragSourceAnchorIndex, out PlaceableTokenData removedItem, out _))
                {
                    return false;
                }

                if (removedItem != activeDragItem)
                {
                    currentInventory.TryPlaceItem(activeDragSourceAnchorIndex, removedItem);
                    return false;
                }

                BackPackTokenLayoutUtility.WriteItem(spellBookCells, targetAnchorIndex, removedItem);
                return true;
            }

            if (!TryGetSingleSpellBookConflict(targetAnchorIndex, activeDragItem, -1, out PlaceableTokenData targetItem, out int targetItemAnchorIndex))
            {
                return false;
            }

            return TrySwapInventoryAndSpellBook(activeDragSourceAnchorIndex, targetAnchorIndex, activeDragItem, targetItemAnchorIndex, targetItem);
        }

        private bool TryMoveSpellBookItemToInventory(int targetAnchorIndex)
        {
            if (currentInventory == null || activeDragItem == null)
            {
                return false;
            }

            if (currentInventory.CanPlaceItem(targetAnchorIndex, activeDragItem))
            {
                if (!TryRemoveSpellBookItemAtCell(activeDragSourceAnchorIndex, out PlaceableTokenData removedItem, out int sourceAnchorIndex))
                {
                    return false;
                }

                if (!currentInventory.TryPlaceItem(targetAnchorIndex, removedItem))
                {
                    BackPackTokenLayoutUtility.WriteItem(spellBookCells, sourceAnchorIndex, removedItem);
                    return false;
                }

                return true;
            }

            if (!TryGetSingleInventoryConflict(targetAnchorIndex, activeDragItem, -1, out PlaceableTokenData targetItem, out int targetItemAnchorIndex))
            {
                return false;
            }

            return TrySwapSpellBookAndInventory(activeDragSourceAnchorIndex, targetAnchorIndex, activeDragItem, targetItemAnchorIndex, targetItem);
        }

        private bool TryMoveSpellBookItem(int targetAnchorIndex)
        {
            if (activeDragItem == null)
            {
                return false;
            }

            if (activeDragSourceAnchorIndex == targetAnchorIndex)
            {
                return true;
            }

            if (TryPlaceSpellBookItem(targetAnchorIndex, activeDragItem, activeDragSourceAnchorIndex))
            {
                BackPackTokenLayoutUtility.ClearItem(spellBookCells, activeDragSourceAnchorIndex);
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, targetAnchorIndex, activeDragItem);
                return true;
            }

            if (!TryGetSingleSpellBookConflict(targetAnchorIndex, activeDragItem, activeDragSourceAnchorIndex, out PlaceableTokenData targetItem, out int targetItemAnchorIndex))
            {
                return false;
            }

            return TrySwapSpellBookItems(activeDragSourceAnchorIndex, targetAnchorIndex, activeDragItem, targetItemAnchorIndex, targetItem);
        }

        private bool TryPlaceSpellBookItem(int targetAnchorIndex, PlaceableTokenData item, int anchorIndexToIgnore = -1)
        {
            return BackPackTokenLayoutUtility.CanPlaceItem(spellBookCells, targetAnchorIndex, SpellBookSlotCount, item, anchorIndexToIgnore);
        }

        private bool TryRemoveSpellBookItemAtCell(int index, out PlaceableTokenData item, out int anchorIndex)
        {
            item = null;
            anchorIndex = -1;
            if (index < 0 || index >= spellBookCells.Count)
            {
                return false;
            }

            TokenCellOccupancy cell = spellBookCells[index];
            if (!cell.IsOccupied)
            {
                return false;
            }

            item = cell.item;
            anchorIndex = cell.anchorIndex;
            BackPackTokenLayoutUtility.ClearItem(spellBookCells, anchorIndex);
            return true;
        }

        private bool TryGetSingleInventoryConflict(int targetAnchorIndex, PlaceableTokenData movingItem, int anchorIndexToIgnore, out PlaceableTokenData conflictItem, out int conflictAnchorIndex)
        {
            conflictItem = null;
            conflictAnchorIndex = -1;
            if (currentInventory == null)
            {
                return false;
            }

            int span = ResolveItemSpan(movingItem);
            if (!IsPlacementInBounds(targetAnchorIndex, span, InventorySlotCount, PlayerBulletTokenInventory.Columns))
            {
                return false;
            }

            for (int i = 0; i < span; i++)
            {
                TokenCellOccupancy cell = currentInventory.GetCell(targetAnchorIndex + i);
                if (!cell.IsOccupied || cell.anchorIndex == anchorIndexToIgnore)
                {
                    continue;
                }

                if (conflictItem == null)
                {
                    conflictItem = cell.item;
                    conflictAnchorIndex = cell.anchorIndex;
                    continue;
                }

                if (cell.anchorIndex != conflictAnchorIndex || cell.item != conflictItem)
                {
                    conflictItem = null;
                    conflictAnchorIndex = -1;
                    return false;
                }
            }

            return conflictItem != null && conflictAnchorIndex >= 0;
        }

        private bool TryGetSingleSpellBookConflict(int targetAnchorIndex, PlaceableTokenData movingItem, int anchorIndexToIgnore, out PlaceableTokenData conflictItem, out int conflictAnchorIndex)
        {
            conflictItem = null;
            conflictAnchorIndex = -1;
            int span = ResolveItemSpan(movingItem);
            if (!IsPlacementInBounds(targetAnchorIndex, span, SpellBookSlotCount, SpellBookSlotCount))
            {
                return false;
            }

            for (int i = 0; i < span; i++)
            {
                TokenCellOccupancy cell = spellBookCells[targetAnchorIndex + i];
                if (!cell.IsOccupied || cell.anchorIndex == anchorIndexToIgnore)
                {
                    continue;
                }

                if (conflictItem == null)
                {
                    conflictItem = cell.item;
                    conflictAnchorIndex = cell.anchorIndex;
                    continue;
                }

                if (cell.anchorIndex != conflictAnchorIndex || cell.item != conflictItem)
                {
                    conflictItem = null;
                    conflictAnchorIndex = -1;
                    return false;
                }
            }

            return conflictItem != null && conflictAnchorIndex >= 0;
        }

        private bool TrySwapInventoryItems(int sourceAnchorIndex, int targetAnchorIndex, PlaceableTokenData sourceItem, int conflictAnchorIndex, PlaceableTokenData conflictItem)
        {
            if (currentInventory == null || sourceItem == null || conflictItem == null)
            {
                return false;
            }

            if (ResolveItemSpan(sourceItem) != ResolveItemSpan(conflictItem))
            {
                return false;
            }

            if (!currentInventory.TryRemoveItemAtCell(sourceAnchorIndex, out PlaceableTokenData removedSourceItem, out int removedSourceAnchorIndex))
            {
                return false;
            }

            if (removedSourceItem != sourceItem || removedSourceAnchorIndex != sourceAnchorIndex)
            {
                currentInventory.TryPlaceItem(sourceAnchorIndex, removedSourceItem);
                return false;
            }

            if (!currentInventory.TryRemoveItemAtCell(conflictAnchorIndex, out PlaceableTokenData removedConflictItem, out int removedConflictAnchorIndex))
            {
                currentInventory.TryPlaceItem(sourceAnchorIndex, removedSourceItem);
                return false;
            }

            if (removedConflictItem != conflictItem || removedConflictAnchorIndex != conflictAnchorIndex)
            {
                currentInventory.TryPlaceItem(sourceAnchorIndex, removedSourceItem);
                if (removedConflictItem != null)
                {
                    currentInventory.TryPlaceItem(conflictAnchorIndex, removedConflictItem);
                }

                return false;
            }

            if (!currentInventory.TryPlaceItem(sourceAnchorIndex, removedConflictItem))
            {
                currentInventory.TryPlaceItem(sourceAnchorIndex, removedSourceItem);
                currentInventory.TryPlaceItem(conflictAnchorIndex, removedConflictItem);
                return false;
            }

            if (currentInventory.TryPlaceItem(targetAnchorIndex, removedSourceItem))
            {
                return true;
            }

            currentInventory.TryRemoveItemAtCell(sourceAnchorIndex, out _, out _);
            currentInventory.TryPlaceItem(sourceAnchorIndex, removedSourceItem);
            currentInventory.TryPlaceItem(conflictAnchorIndex, removedConflictItem);
            return false;
        }

        private bool TrySwapInventoryAndSpellBook(int inventorySourceAnchorIndex, int spellBookTargetAnchorIndex, PlaceableTokenData inventoryItem, int spellBookConflictAnchorIndex, PlaceableTokenData spellBookConflictItem)
        {
            if (currentInventory == null || inventoryItem == null || spellBookConflictItem == null)
            {
                return false;
            }

            if (ResolveItemSpan(inventoryItem) != ResolveItemSpan(spellBookConflictItem))
            {
                return false;
            }

            if (!currentInventory.TryRemoveItemAtCell(inventorySourceAnchorIndex, out PlaceableTokenData removedInventoryItem, out int removedInventoryAnchorIndex))
            {
                return false;
            }

            if (removedInventoryItem != inventoryItem || removedInventoryAnchorIndex != inventorySourceAnchorIndex)
            {
                currentInventory.TryPlaceItem(inventorySourceAnchorIndex, removedInventoryItem);
                return false;
            }

            if (!TryRemoveSpellBookItemAtCell(spellBookConflictAnchorIndex, out PlaceableTokenData removedSpellBookItem, out int removedSpellBookAnchorIndex))
            {
                currentInventory.TryPlaceItem(inventorySourceAnchorIndex, removedInventoryItem);
                return false;
            }

            if (removedSpellBookItem != spellBookConflictItem || removedSpellBookAnchorIndex != spellBookConflictAnchorIndex)
            {
                currentInventory.TryPlaceItem(inventorySourceAnchorIndex, removedInventoryItem);
                if (removedSpellBookItem != null && removedSpellBookAnchorIndex >= 0)
                {
                    BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
                }

                return false;
            }

            if (!currentInventory.TryPlaceItem(inventorySourceAnchorIndex, removedSpellBookItem))
            {
                currentInventory.TryPlaceItem(inventorySourceAnchorIndex, removedInventoryItem);
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
                return false;
            }

            if (TryPlaceSpellBookItem(spellBookTargetAnchorIndex, removedInventoryItem))
            {
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, spellBookTargetAnchorIndex, removedInventoryItem);
                return true;
            }

            currentInventory.TryRemoveItemAtCell(inventorySourceAnchorIndex, out _, out _);
            currentInventory.TryPlaceItem(inventorySourceAnchorIndex, removedInventoryItem);
            BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
            return false;
        }

        private bool TrySwapSpellBookAndInventory(int spellBookSourceAnchorIndex, int inventoryTargetAnchorIndex, PlaceableTokenData spellBookItem, int inventoryConflictAnchorIndex, PlaceableTokenData inventoryConflictItem)
        {
            if (currentInventory == null || spellBookItem == null || inventoryConflictItem == null)
            {
                return false;
            }

            if (ResolveItemSpan(spellBookItem) != ResolveItemSpan(inventoryConflictItem))
            {
                return false;
            }

            if (!TryRemoveSpellBookItemAtCell(spellBookSourceAnchorIndex, out PlaceableTokenData removedSpellBookItem, out int removedSpellBookAnchorIndex))
            {
                return false;
            }

            if (removedSpellBookItem != spellBookItem || removedSpellBookAnchorIndex != spellBookSourceAnchorIndex)
            {
                if (removedSpellBookItem != null && removedSpellBookAnchorIndex >= 0)
                {
                    BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
                }

                return false;
            }

            if (!currentInventory.TryRemoveItemAtCell(inventoryConflictAnchorIndex, out PlaceableTokenData removedInventoryItem, out int removedInventoryAnchorIndex))
            {
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
                return false;
            }

            if (removedInventoryItem != inventoryConflictItem || removedInventoryAnchorIndex != inventoryConflictAnchorIndex)
            {
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
                if (removedInventoryItem != null)
                {
                    currentInventory.TryPlaceItem(inventoryConflictAnchorIndex, removedInventoryItem);
                }

                return false;
            }

            if (!TryPlaceSpellBookItem(removedSpellBookAnchorIndex, removedInventoryItem))
            {
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
                currentInventory.TryPlaceItem(inventoryConflictAnchorIndex, removedInventoryItem);
                return false;
            }

            BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedInventoryItem);
            if (currentInventory.TryPlaceItem(inventoryTargetAnchorIndex, removedSpellBookItem))
            {
                return true;
            }

            BackPackTokenLayoutUtility.ClearItem(spellBookCells, removedSpellBookAnchorIndex);
            BackPackTokenLayoutUtility.WriteItem(spellBookCells, removedSpellBookAnchorIndex, removedSpellBookItem);
            currentInventory.TryPlaceItem(inventoryConflictAnchorIndex, removedInventoryItem);
            return false;
        }

        private bool TrySwapSpellBookItems(int sourceAnchorIndex, int targetAnchorIndex, PlaceableTokenData sourceItem, int conflictAnchorIndex, PlaceableTokenData conflictItem)
        {
            if (sourceItem == null || conflictItem == null || sourceAnchorIndex == conflictAnchorIndex)
            {
                return false;
            }

            if (ResolveItemSpan(sourceItem) != ResolveItemSpan(conflictItem))
            {
                return false;
            }

            BackPackTokenLayoutUtility.ClearItem(spellBookCells, sourceAnchorIndex);
            BackPackTokenLayoutUtility.ClearItem(spellBookCells, conflictAnchorIndex);

            if (!TryPlaceSpellBookItem(sourceAnchorIndex, conflictItem))
            {
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, sourceAnchorIndex, sourceItem);
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, conflictAnchorIndex, conflictItem);
                return false;
            }

            BackPackTokenLayoutUtility.WriteItem(spellBookCells, sourceAnchorIndex, conflictItem);
            if (TryPlaceSpellBookItem(targetAnchorIndex, sourceItem))
            {
                BackPackTokenLayoutUtility.WriteItem(spellBookCells, targetAnchorIndex, sourceItem);
                return true;
            }

            BackPackTokenLayoutUtility.ClearItem(spellBookCells, sourceAnchorIndex);
            BackPackTokenLayoutUtility.WriteItem(spellBookCells, sourceAnchorIndex, sourceItem);
            BackPackTokenLayoutUtility.WriteItem(spellBookCells, conflictAnchorIndex, conflictItem);
            return false;
        }

        private static int ResolveItemSpan(PlaceableTokenData item)
        {
            return item != null && item.SlotSpan > 0 ? item.SlotSpan : 1;
        }

        private static bool IsPlacementInBounds(int anchorIndex, int span, int capacity, int columns)
        {
            if (anchorIndex < 0 || span <= 0 || capacity <= 0 || columns <= 0)
            {
                return false;
            }

            int endIndex = anchorIndex + span - 1;
            if (endIndex >= capacity)
            {
                return false;
            }

            return (anchorIndex / columns) == (endIndex / columns);
        }

        /// <summary>
        /// summary: 运行时创建一个专用拖拽预览层，并始终保持在背包界面最上方。
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

        private void EnsureLinkedOutlineLayers()
        {
            EnsureLinkedOutlineLayer(ref inventoryLinkedOutlineLayer, backPackGridPanel, "InventoryLinkedOutlineLayer");
            RectTransform spellLayerParent = spellBook != null ? spellBook.parent as RectTransform : topPanel;
            EnsureLinkedOutlineLayer(ref spellBookLinkedOutlineLayer, spellLayerParent, "SpellBookLinkedOutlineLayer");
        }

        private void EnsureLinkedOutlineLayer(ref RectTransform layer, RectTransform parent, string layerName)
        {
            if (parent == null)
            {
                layer = null;
                return;
            }

            if (layer == null)
            {
                GameObject layerObject = new(layerName, typeof(RectTransform), typeof(CanvasGroup));
                layerObject.layer = gameObject.layer;
                layer = layerObject.GetComponent<RectTransform>();
                layer.SetParent(parent, false);
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.offsetMin = Vector2.zero;
                layer.offsetMax = Vector2.zero;
                layer.localScale = Vector3.one;

                CanvasGroup canvasGroup = layerObject.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            layer.SetAsLastSibling();
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

        private void EnsureDragLinkedOutlineView()
        {
            if (dragLinkedOutlineView != null)
            {
                return;
            }

            EnsureDragPreviewLayer();
            if (dragPreviewLayer == null)
            {
                return;
            }

            dragLinkedOutlineView = LinkedTokenOutlineView.CreateRuntime("BackPack Linked Drag Outline", dragPreviewLayer, gameObject.layer);
            dragLinkedOutlineView.gameObject.SetActive(false);
        }

        private void EnsureHoverPreviewView()
        {
            if (hoverPreviewView != null)
            {
                return;
            }

            EnsureDragPreviewLayer();
            if (dragPreviewLayer == null || hoverPreviewPrefab == null)
            {
                return;
            }

            hoverPreviewView = Instantiate(hoverPreviewPrefab, dragPreviewLayer, false);
            hoverPreviewView.name = "BackPack Hover Preview";
            SetLayerRecursively(hoverPreviewView.gameObject, gameObject.layer);

            RectTransform previewRect = hoverPreviewView.transform as RectTransform;
            if (previewRect != null)
            {
                previewRect.anchorMin = new Vector2(0.5f, 0.5f);
                previewRect.anchorMax = new Vector2(0.5f, 0.5f);
                previewRect.pivot = new Vector2(0.5f, 0.5f);
                previewRect.anchoredPosition = Vector2.zero;
            }

            CanvasGroup canvasGroup = hoverPreviewView.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = hoverPreviewView.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            Graphic[] graphics = hoverPreviewView.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].raycastTarget = false;
                }
            }

            if (hoverPreviewView.SelectButton != null)
            {
                hoverPreviewView.SelectButton.gameObject.SetActive(false);
            }

            hoverPreviewView.gameObject.SetActive(false);
        }

        private void ShowHoverPreview(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (source == null || source.Item == null)
            {
                HideHoverPreview();
                return;
            }

            EnsureHoverPreviewView();
            if (hoverPreviewView == null)
            {
                return;
            }

            activeHoverSlot = source;
            BindHoverPreviewToken(source.Item);
            hoverPreviewView.gameObject.SetActive(true);
            RectTransform previewRect = hoverPreviewView.transform as RectTransform;
            if (previewRect != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(previewRect);
            }

            hoverPreviewView.transform.SetAsLastSibling();
            MoveHoverPreview(ResolveHoverScreenPosition(source, eventData), ResolvePointerEventCamera(eventData));
        }

        private void MoveHoverPreview(Vector2 screenPosition, Camera eventCamera)
        {
            if (hoverPreviewView == null || dragPreviewLayer == null || !hoverPreviewView.gameObject.activeSelf)
            {
                return;
            }

            RectTransform previewRect = hoverPreviewView.transform as RectTransform;
            if (previewRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragPreviewLayer, screenPosition + hoverPreviewScreenOffset, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            previewRect.anchoredPosition = ClampHoverPreviewLocalPosition(previewRect, localPoint);
            previewRect.SetAsLastSibling();
        }

        private Vector2 ClampHoverPreviewLocalPosition(RectTransform previewRect, Vector2 targetLocalPosition)
        {
            if (dragPreviewLayer == null || previewRect == null)
            {
                return targetLocalPosition;
            }

            Rect layerRect = dragPreviewLayer.rect;
            Rect previewRectBounds = previewRect.rect;
            Vector2 previewPivot = previewRect.pivot;

            float minX = layerRect.xMin + previewRectBounds.width * previewPivot.x;
            float maxX = layerRect.xMax - previewRectBounds.width * (1f - previewPivot.x);
            float minY = layerRect.yMin + previewRectBounds.height * previewPivot.y;
            float maxY = layerRect.yMax - previewRectBounds.height * (1f - previewPivot.y);
            if (minX > maxX || minY > maxY)
            {
                return targetLocalPosition;
            }

            return new Vector2(
                Mathf.Clamp(targetLocalPosition.x, minX, maxX),
                Mathf.Clamp(targetLocalPosition.y, minY, maxY));
        }

        private void BindHoverPreviewToken(PlaceableTokenData token)
        {
            if (hoverPreviewView == null)
            {
                return;
            }

            activeHoverItem = token;
            hoverPreviewView.Bind(null, token);
            if (hoverPreviewView.SelectButton != null)
            {
                hoverPreviewView.SelectButton.gameObject.SetActive(false);
            }
        }

        private void HideHoverPreview()
        {
            if (hoverPreviewView != null)
            {
                hoverPreviewView.gameObject.SetActive(false);
            }

            activeHoverSlot = null;
            activeHoverItem = null;
        }

        private void RefreshHoverPreviewFromActiveSlot()
        {
            if (activeHoverSlot == null || hoverPreviewView == null || !hoverPreviewView.gameObject.activeSelf)
            {
                return;
            }

            if (activeDragSource != null)
            {
                HideHoverPreview();
                return;
            }

            PlaceableTokenData currentItem = activeHoverSlot.Item;
            if (currentItem == null)
            {
                HideHoverPreview();
                return;
            }

            if (currentItem != activeHoverItem)
            {
                BindHoverPreviewToken(currentItem);
            }
        }

        private Vector2 ResolveHoverScreenPosition(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (eventData != null)
            {
                return eventData.position;
            }

            RectTransform slotRect = source != null ? source.SlotRectTransform : null;
            if (slotRect == null)
            {
                return Vector2.zero;
            }

            return LinkedTokenOutlineView.GetScreenCenter(slotRect, null);
        }

        private static Camera ResolvePointerEventCamera(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return null;
            }

            return eventData.enterEventCamera != null ? eventData.enterEventCamera : eventData.pressEventCamera;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
            {
                return;
            }

            target.layer = layer;
            Transform targetTransform = target.transform;
            for (int i = 0; i < targetTransform.childCount; i++)
            {
                SetLayerRecursively(targetTransform.GetChild(i).gameObject, layer);
            }
        }

        private void ShowDragPreview(BackPackGridSlotView source, PointerEventData eventData)
        {
            EnsureDragPreviewView();
            if (dragPreviewView == null)
            {
                return;
            }

            dragPreviewView.gameObject.SetActive(true);
            IReadOnlyList<BackPackGridSlotView> sourceSlots = activeDragSourceArea == BackPackSlotArea.SpellBook ? spellBookSlots : inventorySlots;
            if (activeDragItem != null
                && activeDragItem.SlotSpan > 1
                && TryGetOutlineSlotRange(sourceSlots, activeDragSourceAnchorIndex, activeDragItem.SlotSpan, out BackPackGridSlotView firstSlot, out BackPackGridSlotView lastSlot))
            {
                dragPreviewView.InitializeFromSlotRange(source, firstSlot.SlotRectTransform, lastSlot.SlotRectTransform);
                Camera previewCamera = eventData != null ? eventData.pressEventCamera : null;
                Vector2 itemCenterScreen = LinkedTokenOutlineView.GetScreenCenter(firstSlot.SlotRectTransform, lastSlot.SlotRectTransform, previewCamera);
                Vector2 grabbedCenterScreen = LinkedTokenOutlineView.GetScreenCenter(source.SlotRectTransform, previewCamera);
                dragPreviewScreenOffset = itemCenterScreen - grabbedCenterScreen;
            }
            else
            {
                dragPreviewView.InitializeFromSlot(source);
                dragPreviewScreenOffset = Vector2.zero;
            }

            dragPreviewView.transform.SetAsLastSibling();
            ShowDragOutlinePreview(source, eventData);
            if (dragLinkedOutlineView != null && dragLinkedOutlineView.gameObject.activeSelf)
            {
                dragLinkedOutlineView.transform.SetAsLastSibling();
                dragPreviewView.transform.SetAsLastSibling();
            }

            if (eventData != null)
            {
                MoveDragPreview(eventData.position, eventData.pressEventCamera);
            }
        }

        private void MoveDragPreview(Vector2 screenPosition, Camera eventCamera)
        {
            if (dragPreviewView == null || dragPreviewLayer == null)
            {
                return;
            }

            dragPreviewView.UpdateScreenPosition(dragPreviewLayer, screenPosition + dragPreviewScreenOffset, eventCamera);
            if (dragLinkedOutlineView != null && dragLinkedOutlineView.gameObject.activeSelf)
            {
                dragLinkedOutlineView.UpdateScreenPosition(dragPreviewLayer, screenPosition + dragLinkedOutlineScreenOffset, eventCamera);
            }
        }

        private void HideDragPreview()
        {
            if (dragPreviewView != null)
            {
                dragPreviewView.gameObject.SetActive(false);
            }

            if (dragLinkedOutlineView != null)
            {
                dragLinkedOutlineView.gameObject.SetActive(false);
            }
        }

        private void ShowDragOutlinePreview(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (activeDragItem == null || activeDragItem.SlotSpan <= 1)
            {
                if (dragLinkedOutlineView != null)
                {
                    dragLinkedOutlineView.gameObject.SetActive(false);
                }

                return;
            }

            EnsureDragLinkedOutlineView();
            if (dragLinkedOutlineView == null || dragPreviewLayer == null)
            {
                return;
            }

            IReadOnlyList<BackPackGridSlotView> sourceSlots = activeDragSourceArea == BackPackSlotArea.SpellBook ? spellBookSlots : inventorySlots;
            if (!TryGetOutlineSlotRange(sourceSlots, activeDragSourceAnchorIndex, activeDragItem.SlotSpan, out BackPackGridSlotView firstSlot, out BackPackGridSlotView lastSlot))
            {
                dragLinkedOutlineView.gameObject.SetActive(false);
                return;
            }

            dragLinkedOutlineView.ApplyStyle(linkedOutlineColor, linkedOutlineThickness);
            dragLinkedOutlineView.FitToSlots(dragPreviewLayer, firstSlot.SlotRectTransform, lastSlot.SlotRectTransform, linkedOutlinePadding);
            dragLinkedOutlineView.gameObject.SetActive(true);

            Camera previewCamera = eventData != null ? eventData.pressEventCamera : null;
            Vector2 itemCenterScreen = LinkedTokenOutlineView.GetScreenCenter(firstSlot.SlotRectTransform, lastSlot.SlotRectTransform, previewCamera);
            Vector2 grabbedCenterScreen = LinkedTokenOutlineView.GetScreenCenter(source.SlotRectTransform, previewCamera);
            dragLinkedOutlineScreenOffset = itemCenterScreen - grabbedCenterScreen;
        }

        private void CancelActiveDragSession()
        {
            HideHoverPreview();
            if (activeDragSource != null)
            {
                activeDragSource.ResetDragPresentation();
            }

            HideDragPreview();
            activeDragSource = null;
            activeDragItem = null;
            activeDragSourceAnchorIndex = -1;
            activeDragGrabOffset = 0;
            activeDragSourceArea = BackPackSlotArea.Inventory;
            dragLinkedOutlineScreenOffset = Vector2.zero;
            RefreshLinkedOutlines();
        }

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
            attackPreviewController ??= GetComponent<BackPackAttackPreviewController>();
        }

        private void EnsureSpellBookCellsInitialized()
        {
            while (spellBookCells.Count < SpellBookSlotCount)
            {
                spellBookCells.Add(TokenCellOccupancy.Empty);
            }

            if (spellBookCells.Count > SpellBookSlotCount)
            {
                spellBookCells.RemoveRange(SpellBookSlotCount, spellBookCells.Count - SpellBookSlotCount);
            }
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
                TokenCellOccupancy cell = currentInventory != null ? currentInventory.GetCell(i) : TokenCellOccupancy.Empty;
                inventorySlots[i].SetOccupancy(cell);
            }

            RefreshInventoryLinkedOutlines();
            RefreshHoverPreviewFromActiveSlot();
        }

        private void RefreshSpellBookSlots()
        {
            EnsureSpellBookCellsInitialized();
            for (int i = 0; i < spellBookSlots.Count; i++)
            {
                TokenCellOccupancy cell = i < spellBookCells.Count ? spellBookCells[i] : TokenCellOccupancy.Empty;
                spellBookSlots[i].SetOccupancy(cell);
            }

            RefreshSpellBookLinkedOutlines();
            RefreshHoverPreviewFromActiveSlot();
        }

        /// <summary>
        /// summary: 用当前 loadout 的 item 顺序填充 Spell Book，并把超过 5 格的历史 item 尝试回填到背包库存。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void PopulateSpellBookFromLoadout()
        {
            EnsureSpellBookCellsInitialized();
            int storedOverflowCount = BackPackTokenLayoutUtility.PopulateSpellBookCells(currentLoadout != null ? currentLoadout.Items : null, spellBookCells, currentInventory, out int droppedOverflowCount);
            if (storedOverflowCount > 0)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Returned {storedOverflowCount} overflow item(s) from Spell Book to inventory because they no longer fit the 5-slot width.");
            }

            if (droppedOverflowCount > 0)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Dropped {droppedOverflowCount} overflow item(s) because the player inventory has no continuous free span.");
            }

            SyncSpellBookToLoadout();
        }

        /// <summary>
        /// summary: 收集 Spell Book 当前的锚点 item，并在顺序发生变化时实时写回 AttackFormulaLoadout。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SyncSpellBookToLoadout()
        {
            if (currentLoadout == null)
            {
                return;
            }

            List<PlaceableTokenData> nextItems = BackPackTokenLayoutUtility.BuildCompactLoadoutItems(spellBookCells);
            if (BackPackTokenLayoutUtility.SequenceEquals(currentLoadout.Items, nextItems))
            {
                return;
            }

            currentLoadout.SetItems(nextItems);
            if (currentLoadout.TryGetCompiledAttack(out _))
            {
                TryMarkStoryFlag(TutorialQuestConstants.SpellBookCompiledFlagId);
            }

            RefreshAttackPreview();
        }

        /// <summary>
        /// summary: 把一次已达成的新手引导节点写入永久剧情标记；未选档时静默跳过。
        /// param name="storyFlagId": 需要写入的稳定剧情标记
        /// returns: 无
        /// </summary>
        private static void TryMarkStoryFlag(string storyFlagId)
        {
            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            saveService?.SetStoryFlag(storyFlagId, true);
        }

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

        private void ReleaseBindings()
        {
            CancelActiveDragSession();
            attackPreviewController?.ClearPreview();
            if (currentInventory != null)
            {
                currentInventory.Changed -= HandleInventoryChanged;
            }

            currentPlayer = null;
            currentInventory = null;
            currentLoadout = null;
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private void HandleInventoryChanged()
        {
            RefreshInventorySlots();
        }

        private void ClearAllSlots()
        {
            CancelActiveDragSession();
            attackPreviewController?.ClearPreview();
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                inventorySlots[i].SetOccupancy(TokenCellOccupancy.Empty);
            }

            BackPackTokenLayoutUtility.ClearCells(spellBookCells);
            RefreshSpellBookSlots();
        }

        private void ApplyBackPackGridLayoutDefaults()
        {
            if (backPackGrid == null)
            {
                return;
            }

            GridLayoutGroup gridLayout = backPackGrid.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                return;
            }

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = PlayerBulletTokenInventory.Columns;
        }

        private void RefreshLinkedOutlines()
        {
            EnsureLinkedOutlineLayers();
            RefreshInventoryLinkedOutlines();
            RefreshSpellBookLinkedOutlines();
        }

        private void RefreshInventoryLinkedOutlines()
        {
            RefreshLinkedOutlineLayer(backPackGrid, inventoryLinkedOutlineLayer, inventoryLinkedOutlines, inventorySlots, BackPackSlotArea.Inventory);
        }

        private void RefreshSpellBookLinkedOutlines()
        {
            RefreshLinkedOutlineLayer(spellBook, spellBookLinkedOutlineLayer, spellBookLinkedOutlines, spellBookSlots, BackPackSlotArea.SpellBook);
        }

        private void RefreshLinkedOutlineLayer(
            RectTransform slotContainer,
            RectTransform outlineLayer,
            List<LinkedTokenOutlineView> outlineViews,
            IReadOnlyList<BackPackGridSlotView> slots,
            BackPackSlotArea area)
        {
            if (outlineLayer == null || slotContainer == null || slots == null)
            {
                SetLinkedOutlineViewsVisible(outlineViews, 0);
                return;
            }

            outlineLayer.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer);

            int visibleCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                BackPackGridSlotView slot = slots[i];
                if (slot == null || slot.Item == null || !slot.IsAnchor || slot.Item.SlotSpan <= 1)
                {
                    continue;
                }

                if (ShouldSuppressLinkedOutline(area, slot))
                {
                    continue;
                }

                if (!TryGetOutlineSlotRange(slots, slot.AnchorIndex, slot.Item.SlotSpan, out BackPackGridSlotView firstSlot, out BackPackGridSlotView lastSlot))
                {
                    continue;
                }

                LinkedTokenOutlineView outlineView = GetOrCreateLinkedOutlineView(outlineViews, visibleCount, outlineLayer, $"{area} Linked Outline");
                outlineView.ApplyStyle(linkedOutlineColor, linkedOutlineThickness);
                outlineView.FitToSlots(outlineLayer, firstSlot.SlotRectTransform, lastSlot.SlotRectTransform, linkedOutlinePadding);
                outlineView.gameObject.SetActive(true);
                visibleCount++;
            }

            SetLinkedOutlineViewsVisible(outlineViews, visibleCount);
        }

        private bool ShouldSuppressLinkedOutline(BackPackSlotArea area, BackPackGridSlotView slot)
        {
            return activeDragItem != null
                && activeDragItem.SlotSpan > 1
                && activeDragSourceArea == area
                && activeDragSourceAnchorIndex == slot.AnchorIndex;
        }

        private LinkedTokenOutlineView GetOrCreateLinkedOutlineView(List<LinkedTokenOutlineView> outlineViews, int index, RectTransform parent, string namePrefix)
        {
            while (outlineViews.Count <= index)
            {
                LinkedTokenOutlineView outlineView = LinkedTokenOutlineView.CreateRuntime($"{namePrefix} {outlineViews.Count + 1:D2}", parent, gameObject.layer);
                outlineView.gameObject.SetActive(false);
                outlineViews.Add(outlineView);
            }

            return outlineViews[index];
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

        private static bool TryGetOutlineSlotRange(IReadOnlyList<BackPackGridSlotView> slots, int anchorIndex, int span, out BackPackGridSlotView firstSlot, out BackPackGridSlotView lastSlot)
        {
            firstSlot = null;
            lastSlot = null;
            if (slots == null || anchorIndex < 0 || span <= 1)
            {
                return false;
            }

            int endIndex = anchorIndex + span - 1;
            if (anchorIndex >= slots.Count || endIndex >= slots.Count)
            {
                return false;
            }

            firstSlot = slots[anchorIndex];
            lastSlot = slots[endIndex];
            return firstSlot != null && lastSlot != null && firstSlot.SlotRectTransform != null && lastSlot.SlotRectTransform != null;
        }

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

        private void RefreshAttackPreview()
        {
            if (attackPreviewController == null)
            {
                return;
            }

            CompiledAttack compiledAttack = currentLoadout != null ? currentLoadout.CurrentCompiledAttack : null;
            attackPreviewController.RefreshPreview(currentPlayer, compiledAttack);
        }
    }
}
