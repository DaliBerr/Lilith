using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using Kernel.Quest;
using TMPro;
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
        private const int AuxiliaryGridSlotCount = 10;
        private const int AuxiliaryGridColumnCount = 2;
        private const int DefaultSpellBookSlotCount = 5;

        [Header("Layout")]
        [SerializeField] private RectTransform mainContent;
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform spellBook;
        [SerializeField] private RectTransform leftPanel;
        [SerializeField] private RectTransform previewAnimation;
        [SerializeField] private RectTransform backPackGridPanel;
        [SerializeField] private RectTransform backPackGrid;
        [SerializeField] private RectTransform bookGridPanel;
        [SerializeField] private RectTransform bookGrid;
        [SerializeField] private RectTransform specialItemGridPanel;
        [SerializeField] private RectTransform specialItemGrid;
        [SerializeField] private Button hintButton;
        [SerializeField] private BackPackGridSlotView slotPrefab;
        [SerializeField] private BackPackAttackPreviewController attackPreviewController;
        [SerializeField] private TMP_Text spellDescriptionText;
        [SerializeField] private TextAsset spellDescriptionCatalogJson;

        [Header("Hover Preview")]
        [SerializeField] private BulletTokenSelectionView hoverPreviewPrefab;
        [SerializeField] private Vector2 hoverPreviewScreenOffset = Vector2.zero;
        [SerializeField, Min(0.01f)] private float hoverPreviewScale = 0.7f;

        [Header("Linked Outline")]
        [SerializeField] private Color linkedOutlineColor = new(1f, 0.84f, 0.35f, 0.95f);
        [SerializeField, Min(1f)] private float linkedOutlineThickness = 4f;
        [SerializeField] private Vector2 linkedOutlinePadding = new(6f, 6f);

        private readonly List<BackPackGridSlotView> inventorySlots = new(InventorySlotCount);
        private readonly List<BackPackGridSlotView> bookSlots = new(AuxiliaryGridSlotCount);
        private readonly List<BackPackGridSlotView> specialItemSlots = new(AuxiliaryGridSlotCount);
        private readonly List<BackPackGridSlotView> spellBookSlots = new(DefaultSpellBookSlotCount);
        private readonly List<TokenCellOccupancy> spellBookCells = new(DefaultSpellBookSlotCount);
        private readonly List<LinkedTokenOutlineView> inventoryLinkedOutlines = new();
        private readonly List<LinkedTokenOutlineView> spellBookLinkedOutlines = new();

        private PlayerPlaneMovement currentPlayer;
        private PlayerBulletTokenInventory currentInventory;
        private SpellBookLoadout currentLoadout;
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
        private TextAsset cachedSpellDescriptionCatalogJson;
        private SpellDescriptionCatalogData cachedSpellDescriptionCatalog;

        public override Status currentStatus { get; } = StatusList.InBackPackStatus;
        public override bool PreservePrefabRootRectTransform => true;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindButtonCallbacks();
            EnsureSpellBookCellsInitialized();
            BindStaticSpellBookSlots();
            EnsureInventorySlotsBuilt();
            EnsureAuxiliarySlotsBuilt();
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
            UnbindButtonCallbacks();
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
            EnsureAuxiliarySlotsBuilt();

            if (!TryResolvePlayerBindings())
            {
                ClearAllSlots();
                attackPreviewController?.ClearPreview();
                ClearSpellDescription();
                return;
            }

            EnsureSpellBookCellsInitialized();
            BindStaticSpellBookSlots();
            currentInventory.EnsureInitialized();
            PopulateSpellBookFromLoadout();
            RefreshInventorySlots();
            RefreshSpellBookSlots();
            RefreshAttackPreview();
            RefreshSpellDescription();
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
        /// summary: 响应槽位点击事件，切换当前槽位承载 token 的详细预览。
        /// param: source 当前被点击的槽位
        /// param: eventData 当前指针事件数据
        /// returns: 无
        /// </summary>
        public void NotifySlotClick(BackPackGridSlotView source, PointerEventData eventData)
        {
            if (activeDragSource != null)
            {
                HideHoverPreview();
                return;
            }

            if (source == null)
            {
                return;
            }

            if (source == activeHoverSlot && hoverPreviewView != null && hoverPreviewView.gameObject.activeSelf)
            {
                HideHoverPreview();
                return;
            }

            if (source.Item == null)
            {
                HideHoverPreview();
                return;
            }

            ShowHoverPreview(source, eventData);
        }

        /// <summary>
        /// summary: 响应槽位悬停移动事件；当前详细卡片锁定在槽位中心，只在这里做存活校验与内容刷新。
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
                return;
            }

            if (activeHoverItem != source.Item)
            {
                BindHoverPreviewToken(source.Item);
            }
        }

        /// <summary>
        /// summary: 保留给旧调用方的悬停离开入口；详细预览现由点击控制，不再因移出槽位自动关闭。
        /// param: source 当前离开的槽位
        /// returns: 无
        /// </summary>
        public void NotifySlotHoverExit(BackPackGridSlotView source)
        {
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
            return BackPackTokenLayoutUtility.CanPlaceItem(spellBookCells, targetAnchorIndex, GetSpellBookSlotCount(), item, anchorIndexToIgnore);
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
            int spellBookSlotCount = GetSpellBookSlotCount();
            if (!IsPlacementInBounds(targetAnchorIndex, span, spellBookSlotCount, spellBookSlotCount))
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
                previewRect.pivot = new Vector2(0f, 1f);
                previewRect.anchoredPosition = Vector2.zero;
                float previewScale = Mathf.Max(0.01f, hoverPreviewScale);
                previewRect.localScale = new Vector3(previewScale, previewScale, 1f);
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
            Vector3 previewScale = previewRect.localScale;
            float previewWidth = previewRectBounds.width * Mathf.Abs(previewScale.x);
            float previewHeight = previewRectBounds.height * Mathf.Abs(previewScale.y);

            float minX = layerRect.xMin + previewWidth * previewPivot.x;
            float maxX = layerRect.xMax - previewWidth * (1f - previewPivot.x);
            float minY = layerRect.yMin + previewHeight * previewPivot.y;
            float maxY = layerRect.yMax - previewHeight * (1f - previewPivot.y);
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

            MoveHoverPreview(ResolveHoverScreenPosition(activeHoverSlot, null), null);
        }

        private Vector2 ResolveHoverScreenPosition(BackPackGridSlotView source, PointerEventData eventData)
        {
            RectTransform slotRect = source != null ? source.SlotRectTransform : null;
            if (slotRect == null)
            {
                return Vector2.zero;
            }

            return LinkedTokenOutlineView.GetScreenCenter(slotRect, ResolvePointerEventCamera(eventData));
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
            mainContent ??= transform as RectTransform;

            topPanel ??= FindRectTransform(mainContent, "Top Panel");
            spellBook ??= FindRectTransform(topPanel, "Spell Book");
            leftPanel ??= FindRectTransform(mainContent, "Left Panel");
            previewAnimation ??= FindRectTransform(leftPanel, "Preview Animation");
            backPackGridPanel ??= FindRectTransform(mainContent, "BackPack Grid Panel");
            backPackGrid = ResolveBackPackGridContainer(backPackGridPanel, backPackGrid);
            bookGridPanel ??= FindRectTransform(mainContent, "Book Grid");
            bookGrid = ResolveBackPackGridContainer(bookGridPanel, bookGrid);
            specialItemGridPanel ??= FindRectTransform(mainContent, "Special_Item Grid");
            specialItemGrid = ResolveBackPackGridContainer(specialItemGridPanel, specialItemGrid);
            hintButton ??= FindComponent<Button>(topPanel, "Hint Button");
            slotPrefab ??= ResolveTemplateSlot();
            attackPreviewController ??= GetComponent<BackPackAttackPreviewController>();
            spellDescriptionText ??= FindDescriptionText(mainContent);
        }

        private static RectTransform ResolveBackPackGridContainer(RectTransform gridPanel, RectTransform currentGrid)
        {
            RectTransform resolvedGrid = TryResolveScrollContentGrid(gridPanel);
            resolvedGrid ??= TryResolveGridLayoutRect(gridPanel, "Grid Content");
            resolvedGrid ??= TryResolveGridLayoutRect(gridPanel, "Grid");
            resolvedGrid ??= currentGrid;
            return resolvedGrid;
        }

        private static RectTransform TryResolveScrollContentGrid(RectTransform gridPanel)
        {
            if (gridPanel == null)
            {
                return null;
            }

            ScrollRect scrollRect = gridPanel.GetComponentInChildren<ScrollRect>(includeInactive: true);
            if (scrollRect == null)
            {
                return null;
            }

            RectTransform content = scrollRect.content;
            if (content == null)
            {
                return null;
            }

            return content.GetComponent<GridLayoutGroup>() != null ? content : null;
        }

        private static RectTransform TryResolveGridLayoutRect(Transform root, string targetName)
        {
            RectTransform rectTransform = FindRectTransform(root, targetName);
            return rectTransform != null && rectTransform.GetComponent<GridLayoutGroup>() != null ? rectTransform : null;
        }

        private static TMP_Text FindDescriptionText(Transform root)
        {
            RectTransform panel = FindRectTransform(root, "Description Panel");
            return panel != null ? panel.GetComponentInChildren<TMP_Text>(includeInactive: true) : null;
        }

        private static RectTransform FindRectTransform(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            if (root is RectTransform rootRectTransform && root.name == targetName)
            {
                return rootRectTransform;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName)
                {
                    return child as RectTransform;
                }

                RectTransform match = FindRectTransform(child, targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponent<T>(Transform root, string targetName) where T : Component
        {
            RectTransform rectTransform = FindRectTransform(root, targetName);
            return rectTransform != null ? rectTransform.GetComponent<T>() : null;
        }

        /// <summary>
        /// summary: 绑定背包顶部 Hint 按钮到统一输入路由，保持与 Tab 快捷键行为一致。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            if (hintButton == null)
            {
                return;
            }

            hintButton.onClick.RemoveListener(HandleHintButtonClicked);
            hintButton.onClick.AddListener(HandleHintButtonClicked);
        }

        /// <summary>
        /// summary: 清理背包顶部 Hint 按钮事件，避免销毁后残留无效回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            if (hintButton == null)
            {
                return;
            }

            hintButton.onClick.RemoveListener(HandleHintButtonClicked);
        }

        /// <summary>
        /// summary: 点击背包 Hint 按钮时请求切换 Hint 弹窗。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static void HandleHintButtonClicked()
        {
            UIInputRouter.Instance?.RequestToggleHint();
        }

        private void EnsureSpellBookCellsInitialized()
        {
            int spellBookSlotCount = GetSpellBookSlotCount();
            while (spellBookCells.Count < spellBookSlotCount)
            {
                spellBookCells.Add(TokenCellOccupancy.Empty);
            }

            if (spellBookCells.Count > spellBookSlotCount)
            {
                spellBookCells.RemoveRange(spellBookSlotCount, spellBookCells.Count - spellBookSlotCount);
            }
        }

        private int GetSpellBookSlotCount()
        {
            return currentLoadout != null ? Mathf.Max(1, currentLoadout.SlotCount) : DefaultSpellBookSlotCount;
        }

        /// <summary>
        /// summary: 绑定 prefab 中预放的 Spell Book 槽位，并按当前法术书槽位数显示可用格。
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

            int spellBookSlotCount = GetSpellBookSlotCount();
            int staticSlotCount = 0;
            for (int i = 0; i < spellBook.childCount; i++)
            {
                BackPackGridSlotView slotView = spellBook.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView == null)
                {
                    continue;
                }

                bool isAvailable = staticSlotCount < spellBookSlotCount;
                slotView.gameObject.SetActive(isAvailable);
                if (!isAvailable)
                {
                    staticSlotCount++;
                    continue;
                }

                slotView.Initialize(this, BackPackSlotArea.SpellBook, spellBookSlots.Count);
                spellBookSlots.Add(slotView);
                staticSlotCount++;
            }

            if (spellBookSlots.Count < spellBookSlotCount)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Spell Book expects {spellBookSlotCount} static slots, but found {spellBookSlots.Count}.");
            }
        }

        /// <summary>
        /// summary: 运行时确保背包区域存在准确的 48 个槽位；数量不对时会整组重建，并优先使用 ScrollRect content 作为容器。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureInventorySlotsBuilt()
        {
            EnsureRuntimeSlotGridBuilt(
                backPackGrid,
                inventorySlots,
                InventorySlotCount,
                "BackPack Slot",
                BackPackSlotArea.Inventory,
                initializeDisplayOnly: false);
        }

        /// <summary>
        /// summary: 运行时确保 Book 与 Special Item 两个滚动区都存在准确的 2x5 展示槽位，并复用背包格 prefab。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureAuxiliarySlotsBuilt()
        {
            EnsureRuntimeSlotGridBuilt(
                bookGrid,
                bookSlots,
                AuxiliaryGridSlotCount,
                "Book Slot",
                BackPackSlotArea.Inventory,
                initializeDisplayOnly: true);
            EnsureRuntimeSlotGridBuilt(
                specialItemGrid,
                specialItemSlots,
                AuxiliaryGridSlotCount,
                "Special Item Slot",
                BackPackSlotArea.Inventory,
                initializeDisplayOnly: true);
        }

        /// <summary>
        /// summary: 当玩家对象存在时，缓存其移动、库存与法术书 loadout 组件，并同步库存刷新事件。
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
            SpellBookLoadout loadout = currentPlayer.GetComponent<SpellBookLoadout>();
            if (inventory == null || loadout == null)
            {
                ReleaseBindings();
                GameDebug.LogWarning("[BackPackUIScreen] Player is missing PlayerBulletTokenInventory or SpellBookLoadout.");
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
        /// summary: 用当前法术书 loadout 的装备 item 顺序填充 Spell Book，并把超过槽位上限的历史 item 尝试回填到背包库存。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void PopulateSpellBookFromLoadout()
        {
            EnsureSpellBookCellsInitialized();
            int storedOverflowCount = BackPackTokenLayoutUtility.PopulateSpellBookCells(currentLoadout != null ? currentLoadout.EquippedItems : null, spellBookCells, currentInventory, out int droppedOverflowCount);
            if (storedOverflowCount > 0)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Returned {storedOverflowCount} overflow item(s) from Spell Book to inventory because they no longer fit the {GetSpellBookSlotCount()}-slot width.");
            }

            if (droppedOverflowCount > 0)
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Dropped {droppedOverflowCount} overflow item(s) because the player inventory has no continuous free span.");
            }

            SyncSpellBookToLoadout();
        }

        /// <summary>
        /// summary: 收集 Spell Book 当前的锚点 item，并在顺序发生变化时实时写回 SpellBookLoadout。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SyncSpellBookToLoadout()
        {
            if (currentLoadout == null)
            {
                ClearSpellDescription();
                return;
            }

            List<PlaceableTokenData> nextItems = BackPackTokenLayoutUtility.BuildCompactLoadoutItems(spellBookCells);
            if (BackPackTokenLayoutUtility.SequenceEquals(currentLoadout.EquippedItems, nextItems))
            {
                return;
            }

            currentLoadout.SetItems(nextItems);
            if (currentLoadout.TryGetCompiledProgram(out _))
            {
                TryMarkStoryFlag(TutorialQuestConstants.SpellBookCompiledFlagId);
            }

            RefreshAttackPreview();
            RefreshSpellDescription();
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

        private void EnsureRuntimeSlotGridBuilt(
            RectTransform slotContainer,
            List<BackPackGridSlotView> slotViews,
            int expectedSlotCount,
            string slotNamePrefix,
            BackPackSlotArea slotArea,
            bool initializeDisplayOnly)
        {
            slotViews.Clear();
            if (slotContainer == null || slotPrefab == null)
            {
                return;
            }

            bool needRebuild = slotContainer.childCount != expectedSlotCount;
            if (!needRebuild)
            {
                for (int i = 0; i < slotContainer.childCount; i++)
                {
                    if (slotContainer.GetChild(i).GetComponent<BackPackGridSlotView>() == null)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                RebuildRuntimeSlotGrid(slotContainer, expectedSlotCount, slotNamePrefix, slotArea, initializeDisplayOnly);
            }

            BindRuntimeSlotGrid(slotContainer, slotViews, slotArea, initializeDisplayOnly);
        }

        private void RebuildRuntimeSlotGrid(
            RectTransform slotContainer,
            int slotCount,
            string slotNamePrefix,
            BackPackSlotArea slotArea,
            bool initializeDisplayOnly)
        {
            CancelActiveDragSession();
            for (int i = slotContainer.childCount - 1; i >= 0; i--)
            {
                DestroyChild(slotContainer.GetChild(i).gameObject);
            }

            for (int i = 0; i < slotCount; i++)
            {
                BackPackGridSlotView slotView = Instantiate(slotPrefab, slotContainer);
                slotView.name = $"{slotNamePrefix} {i + 1:D2}";
                if (initializeDisplayOnly)
                {
                    slotView.InitializeDisplayOnly(slotArea);
                    slotView.SetOccupancy(TokenCellOccupancy.Empty);
                    continue;
                }

                slotView.Initialize(this, slotArea, i);
            }
        }

        private void BindRuntimeSlotGrid(
            RectTransform slotContainer,
            List<BackPackGridSlotView> slotViews,
            BackPackSlotArea slotArea,
            bool initializeDisplayOnly)
        {
            for (int i = 0; i < slotContainer.childCount; i++)
            {
                BackPackGridSlotView slotView = slotContainer.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView == null)
                {
                    continue;
                }

                if (initializeDisplayOnly)
                {
                    slotView.InitializeDisplayOnly(slotArea);
                    slotView.SetOccupancy(TokenCellOccupancy.Empty);
                }
                else
                {
                    slotView.Initialize(this, slotArea, slotViews.Count);
                }

                slotViews.Add(slotView);
            }
        }

        private void ReleaseBindings()
        {
            CancelActiveDragSession();
            attackPreviewController?.ClearPreview();
            ClearSpellDescription();
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
            ClearSpellDescription();
        }

        private void ApplyBackPackGridLayoutDefaults()
        {
            ApplyGridLayoutDefaults(backPackGrid, PlayerBulletTokenInventory.Columns);
            ApplyGridLayoutDefaults(bookGrid, AuxiliaryGridColumnCount);
            ApplyGridLayoutDefaults(specialItemGrid, AuxiliaryGridColumnCount);
        }

        private static void ApplyGridLayoutDefaults(RectTransform gridRoot, int columnCount)
        {
            if (gridRoot == null)
            {
                return;
            }

            GridLayoutGroup gridLayout = gridRoot.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                return;
            }

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columnCount;
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

            CompiledSpellProgram compiledProgram = currentLoadout != null ? currentLoadout.CurrentCompiledProgram : null;
            attackPreviewController.RefreshPreview(currentPlayer, compiledProgram);
        }

        private void RefreshSpellDescription()
        {
            TryAutoBindReferences();
            if (spellDescriptionText == null)
            {
                return;
            }

            spellDescriptionText.richText = true;
            if (currentLoadout == null)
            {
                spellDescriptionText.text = string.Empty;
                return;
            }

            spellDescriptionText.text = SpellDescriptionGenerator.GenerateRichText(
                currentLoadout.CurrentCompiledProgram,
                currentLoadout.ExecutionItems,
                ResolveSpellDescriptionCatalog(),
                currentLoadout.SpellBook);
        }

        private void ClearSpellDescription()
        {
            TryAutoBindReferences();
            if (spellDescriptionText == null)
            {
                return;
            }

            spellDescriptionText.richText = true;
            spellDescriptionText.text = string.Empty;
        }

        private SpellDescriptionCatalogData ResolveSpellDescriptionCatalog()
        {
            if (spellDescriptionCatalogJson == null)
            {
                cachedSpellDescriptionCatalogJson = null;
                cachedSpellDescriptionCatalog = null;
                return null;
            }

            if (cachedSpellDescriptionCatalogJson == spellDescriptionCatalogJson)
            {
                return cachedSpellDescriptionCatalog;
            }

            cachedSpellDescriptionCatalogJson = spellDescriptionCatalogJson;
            if (!SpellDescriptionCatalogData.TryDeserializeJson(spellDescriptionCatalogJson.text, out cachedSpellDescriptionCatalog, out string errorMessage))
            {
                GameDebug.LogWarning($"[BackPackUIScreen] Failed to parse spell description catalog '{spellDescriptionCatalogJson.name}': {errorMessage}");
                cachedSpellDescriptionCatalog = null;
            }

            return cachedSpellDescriptionCatalog;
        }
    }
}
