using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.Quest;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class BackPackUIScreenTests
{
    private readonly List<Object> createdObjects = new();

    [Test]
    public void ResponsiveLayoutGroupFitter_ShrinksInventorySlotsToFitGridRect()
    {
        BackPackUIScreen screen = CreateBackPackUIScreen();
        RectTransform grid = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View/Viewport/Grid Content") as RectTransform;
        Assert.That(grid, Is.Not.Null);
        grid.anchorMin = new Vector2(0.5f, 0.5f);
        grid.anchorMax = new Vector2(0.5f, 0.5f);
        grid.sizeDelta = new Vector2(480f, 360f);

        InvokeNonPublic(screen, "OnInit");
        ResponsiveLayoutGroupFitter fitter = screen.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();
        fitter.SetRoot(screen.transform as RectTransform);
        fitter.FitNow();

        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(layout.constraintCount, Is.EqualTo(PlayerBulletTokenInventory.Columns));
        Assert.That(layout.cellSize.x, Is.LessThan(100f));
        Assert.That(ResolveGridRequiredWidth(layout), Is.LessThanOrEqualTo(grid.rect.width + 0.01f));
        Assert.That(ResolveGridRequiredHeight(layout), Is.LessThanOrEqualTo(grid.rect.height + 0.01f));
    }

    [Test]
    public void OnInit_BuildsBookAndSpecialItemDisplayGridsWithTenSlots()
    {
        BackPackUIScreen screen = CreateBackPackUIScreen();

        InvokeNonPublic(screen, "OnInit");

        RectTransform bookGrid = GetPrivateField<RectTransform>(screen, "bookGrid");
        RectTransform specialItemGrid = GetPrivateField<RectTransform>(screen, "specialItemGrid");
        List<BackPackGridSlotView> bookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "bookSlots");
        List<BackPackGridSlotView> specialItemSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "specialItemSlots");
        Assert.That(bookGrid, Is.Not.Null);
        Assert.That(specialItemGrid, Is.Not.Null);
        Assert.That(bookGrid.childCount, Is.EqualTo(10));
        Assert.That(specialItemGrid.childCount, Is.EqualTo(10));
        Assert.That(bookSlots.Count, Is.EqualTo(10));
        Assert.That(specialItemSlots.Count, Is.EqualTo(10));

        GridLayoutGroup bookLayout = bookGrid.GetComponent<GridLayoutGroup>();
        GridLayoutGroup specialItemLayout = specialItemGrid.GetComponent<GridLayoutGroup>();
        Assert.That(bookLayout, Is.Not.Null);
        Assert.That(specialItemLayout, Is.Not.Null);
        Assert.That(bookLayout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(specialItemLayout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
        Assert.That(bookLayout.constraintCount, Is.EqualTo(2));
        Assert.That(specialItemLayout.constraintCount, Is.EqualTo(2));
    }

    [TearDown]
    public void TearDown()
    {
        DestroyExistingRuntimeSaveService();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        DeleteSaveDirectory();
    }

    [Test]
    public void RefreshFromCurrentPlayer_ShowsLinkedOutlinesForInventoryAndSpellBook()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("inventory_linked",
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));
        LinkedTokenData spellLinked = CreateLinkedToken("spell_linked",
            CreateToken<CoreTokenData>("ice", "Ice"),
            CreateToken<ResultTokenData>("boom", "Boom"));
        CreatePlayerWithState(inventoryLinked, spellLinked);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        Assert.That(GetActiveOutlineCount(screen, "inventoryLinkedOutlines"), Is.EqualTo(1));
        Assert.That(GetActiveOutlineCount(screen, "spellBookLinkedOutlines"), Is.EqualTo(1));
    }

    [Test]
    public void RefreshFromCurrentPlayer_BindsSpellBookLoadoutSlotCount()
    {
        CoreTokenData firstToken = CreateToken<CoreTokenData>("slot_count_first", "First");
        CoreTokenData secondToken = CreateToken<CoreTokenData>("slot_count_second", "Second");
        CoreTokenData overflowToken = CreateToken<CoreTokenData>("slot_count_overflow", "Overflow");
        CreatePlayer(out _, out SpellBookLoadout loadout);
        SpellBookData spellBook = CreateSpellBook(slotCount: 2);
        loadout.SetSpellBook(spellBook);
        loadout.SetItems(new PlaceableTokenData[]
        {
            firstToken,
            secondToken,
            overflowToken,
        });

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> spellBookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "spellBookSlots");
        Assert.That(spellBookSlots.Count, Is.EqualTo(2));
        Assert.That(spellBookSlots[0].Item, Is.SameAs(firstToken));
        Assert.That(spellBookSlots[1].Item, Is.SameAs(secondToken));
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(2));
    }

    [Test]
    public void NotifySlotBeginDrag_WithLinkedItem_ShowsDragOutlinePreview()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("inventory_linked",
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));
        CreatePlayerWithState(inventoryLinked, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        Assert.That(inventorySlots.Count, Is.GreaterThan(0));

        screen.NotifySlotBeginDrag(inventorySlots[0], null);

        LinkedTokenOutlineView dragOutline = GetPrivateField<LinkedTokenOutlineView>(screen, "dragLinkedOutlineView");
        BackPackDragPreviewView dragPreview = GetPrivateField<BackPackDragPreviewView>(screen, "dragPreviewView");
        Assert.That(dragOutline, Is.Not.Null);
        Assert.That(dragOutline.gameObject.activeSelf, Is.True);
        Assert.That(dragPreview, Is.Not.Null);
        Assert.That(dragPreview.gameObject.activeSelf, Is.True);
        Assert.That(dragPreview.PreviewRectTransform.rect.width, Is.GreaterThan(inventorySlots[0].SlotRectTransform.rect.width));

        screen.NotifySlotEndDrag(inventorySlots[0]);
        Assert.That(dragOutline.gameObject.activeSelf, Is.False);
        Assert.That(dragPreview.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void NotifySlotClick_WithOccupiedSlot_ShowsHoverPreviewAndBindsToken()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("hover_fire", "Fire");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        PointerEventData hoverEventData = CreatePointerEventData(new Vector2(320f, 240f));
        screen.NotifySlotClick(inventorySlots[0], hoverEventData);

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview, Is.Not.Null);
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);
        Assert.That(hoverPreview.BoundToken, Is.SameAs(inventoryToken));
        Assert.That(hoverPreview.SelectButton, Is.Not.Null);
        Assert.That(hoverPreview.SelectButton.gameObject.activeSelf, Is.False);
        RectTransform hoverRect = hoverPreview.transform as RectTransform;
        Assert.That(hoverRect, Is.Not.Null);
        Assert.That(hoverRect.pivot.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(hoverRect.pivot.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(hoverRect.localScale.x, Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(hoverRect.localScale.y, Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(hoverRect.localScale.z, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void NotifySlotClick_SameSlotTwice_HidesHoverPreview()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("click_toggle", "Toggle");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        PointerEventData clickEventData = CreatePointerEventData(new Vector2(240f, 200f));
        screen.NotifySlotClick(inventorySlots[0], clickEventData);

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);

        screen.NotifySlotClick(inventorySlots[0], clickEventData);
        Assert.That(hoverPreview.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void NotifySlotClick_DifferentOccupiedSlot_RebindsHoverPreview()
    {
        CoreTokenData firstToken = CreateToken<CoreTokenData>("click_switch_a", "SwitchA");
        CoreTokenData secondToken = CreateToken<CoreTokenData>("click_switch_b", "SwitchB");
        CreatePlayer(out PlayerBulletTokenInventory inventory, out _);
        Assert.That(inventory.TryPlaceItem(0, firstToken), Is.True);
        Assert.That(inventory.TryPlaceItem(1, secondToken), Is.True);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotClick(inventorySlots[0], CreatePointerEventData(new Vector2(120f, 120f)));

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview.BoundToken, Is.SameAs(firstToken));

        screen.NotifySlotClick(inventorySlots[1], CreatePointerEventData(new Vector2(360f, 220f)));
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);
        Assert.That(hoverPreview.BoundToken, Is.SameAs(secondToken));
    }

    [Test]
    public void InventorySlotPointerClick_WithScrollRectStillOpensHoverPreview()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_click", "SlotClick");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        BackPackGridSlotView slot = inventorySlots[0];
        PointerEventData clickEventData = CreatePointerEventData(new Vector2(220f, 180f));
        clickEventData.button = PointerEventData.InputButton.Left;

        slot.OnPointerClick(clickEventData);

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview, Is.Not.Null);
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);
        Assert.That(hoverPreview.BoundToken, Is.SameAs(inventoryToken));
    }

    [Test]
    public void InventorySlotQuickDrag_RoutesToScrollRectInsteadOfItemDrag()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_scroll", "SlotScroll");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TrackingScrollRect scrollRect = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View")?.GetComponent<TrackingScrollRect>();
        Assert.That(scrollRect, Is.Not.Null);
        ConfigureInventoryScrollArea(scrollRect);

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        BackPackGridSlotView slot = inventorySlots[0];
        PointerEventData dragEventData = CreatePointerEventData(new Vector2(120f, 120f));
        dragEventData.button = PointerEventData.InputButton.Left;
        dragEventData.pressPosition = dragEventData.position;
        dragEventData.delta = new Vector2(0f, -80f);
        dragEventData.position += dragEventData.delta;

        slot.OnPointerDown(dragEventData);
        slot.OnInitializePotentialDrag(dragEventData);
        slot.OnBeginDrag(dragEventData);
        slot.OnDrag(dragEventData);
        slot.OnEndDrag(dragEventData);

        Assert.That(IsHoldProgressRingVisible(slot), Is.False);
        Assert.That(scrollRect.InitializePotentialDragCallCount, Is.EqualTo(1));
        Assert.That(scrollRect.BeginDragCallCount, Is.EqualTo(1));
        Assert.That(scrollRect.DragCallCount, Is.EqualTo(1));
        Assert.That(scrollRect.EndDragCallCount, Is.EqualTo(1));
        Assert.That(GetPrivateField<BackPackGridSlotView>(screen, "activeDragSource"), Is.Null);
        BackPackDragPreviewView dragPreview = GetPrivateField<BackPackDragPreviewView>(screen, "dragPreviewView");
        Assert.That(dragPreview == null || !dragPreview.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void InventorySlotPointerDown_ShowsHoldProgressRingAtZeroFill()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_hold_ring", "HoldRing");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TrackingScrollRect scrollRect = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View")?.GetComponent<TrackingScrollRect>();
        Assert.That(scrollRect, Is.Not.Null);
        ConfigureInventoryScrollArea(scrollRect);

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        BackPackGridSlotView slot = inventorySlots[0];
        PointerEventData pointerEventData = CreatePointerEventData(new Vector2(180f, 160f));
        pointerEventData.button = PointerEventData.InputButton.Left;

        slot.OnPointerDown(pointerEventData);

        Image fillImage = GetHoldProgressFillImage(slot);
        Assert.That(IsHoldProgressRingVisible(slot), Is.True);
        Assert.That(fillImage, Is.Not.Null);
        Assert.That(fillImage.raycastTarget, Is.False);
        Assert.That(fillImage.type, Is.EqualTo(Image.Type.Filled));
        Assert.That(fillImage.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
        Assert.That(fillImage.fillAmount, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void InventorySlotLongPressProgress_FillsThenHidesWhenItemDragStarts()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_hold_ring_drag", "HoldRingDrag");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TrackingScrollRect scrollRect = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View")?.GetComponent<TrackingScrollRect>();
        Assert.That(scrollRect, Is.Not.Null);
        ConfigureInventoryScrollArea(scrollRect);

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        BackPackGridSlotView slot = inventorySlots[0];
        PointerEventData dragEventData = CreatePointerEventData(new Vector2(180f, 160f));
        dragEventData.button = PointerEventData.InputButton.Left;

        slot.OnPointerDown(dragEventData);
        SetPrivateField(slot, "pointerDownTime", Time.unscaledTime - 1f);
        InvokeNonPublic(slot, "Update");

        Image fillImage = GetHoldProgressFillImage(slot);
        Assert.That(fillImage.fillAmount, Is.EqualTo(1f).Within(0.0001f));

        slot.OnBeginDrag(dragEventData);

        Assert.That(GetPrivateField<BackPackGridSlotView>(screen, "activeDragSource"), Is.SameAs(slot));
        Assert.That(IsHoldProgressRingVisible(slot), Is.False);

        slot.OnEndDrag(dragEventData);
    }

    [Test]
    public void InventorySlotPointerUp_HidesHoldProgressRingWithoutDragging()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_hold_ring_up", "HoldRingUp");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TrackingScrollRect scrollRect = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View")?.GetComponent<TrackingScrollRect>();
        Assert.That(scrollRect, Is.Not.Null);
        ConfigureInventoryScrollArea(scrollRect);

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        BackPackGridSlotView slot = inventorySlots[0];
        PointerEventData pointerEventData = CreatePointerEventData(new Vector2(180f, 160f));
        pointerEventData.button = PointerEventData.InputButton.Left;

        slot.OnPointerDown(pointerEventData);
        Assert.That(IsHoldProgressRingVisible(slot), Is.True);

        slot.OnPointerUp(pointerEventData);

        Assert.That(IsHoldProgressRingVisible(slot), Is.False);
    }

    [Test]
    public void SpellBookSlotPointerDown_DoesNotShowHoldProgressRing()
    {
        CoreTokenData spellBookToken = CreateToken<CoreTokenData>("slot_spell_no_hold_ring", "SpellNoHoldRing");
        CreatePlayerWithState(null, spellBookToken);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> spellBookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "spellBookSlots");
        BackPackGridSlotView slot = spellBookSlots[0];
        PointerEventData pointerEventData = CreatePointerEventData(new Vector2(180f, 160f));
        pointerEventData.button = PointerEventData.InputButton.Left;

        slot.OnPointerDown(pointerEventData);

        Assert.That(IsHoldProgressRingVisible(slot), Is.False);
    }

    [Test]
    public void InventorySlotLongPressDrag_PrefersItemDragOverScrollRect()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_long_press_drag", "SlotLongPress");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TrackingScrollRect scrollRect = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View")?.GetComponent<TrackingScrollRect>();
        Assert.That(scrollRect, Is.Not.Null);
        ConfigureInventoryScrollArea(scrollRect);

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        BackPackGridSlotView slot = inventorySlots[0];
        PointerEventData dragEventData = CreatePointerEventData(new Vector2(180f, 160f));
        dragEventData.button = PointerEventData.InputButton.Left;

        slot.OnPointerDown(dragEventData);
        SetPrivateField(slot, "pointerDownTime", Time.unscaledTime - 1f);
        slot.OnInitializePotentialDrag(dragEventData);
        slot.OnBeginDrag(dragEventData);

        BackPackDragPreviewView dragPreview = GetPrivateField<BackPackDragPreviewView>(screen, "dragPreviewView");
        Assert.That(GetPrivateField<BackPackGridSlotView>(screen, "activeDragSource"), Is.SameAs(slot));
        Assert.That(dragPreview.gameObject.activeSelf, Is.True);
        Assert.That(scrollRect.BeginDragCallCount, Is.EqualTo(0));
        Assert.That(scrollRect.DragCallCount, Is.EqualTo(0));
        Assert.That(scrollRect.EndDragCallCount, Is.EqualTo(0));

        slot.OnEndDrag(dragEventData);
    }

    [Test]
    public void InventorySlotLongPressDrag_CanDropIntoSpellBook()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("slot_drag_to_spellbook", "DragToSpellBook");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TrackingScrollRect scrollRect = screen.transform.Find("MainContent/BackPack Grid Panel/Scroll View")?.GetComponent<TrackingScrollRect>();
        Assert.That(scrollRect, Is.Not.Null);
        ConfigureInventoryScrollArea(scrollRect);

        PlayerBulletTokenInventory inventory = Object.FindFirstObjectByType<PlayerBulletTokenInventory>();
        SpellBookLoadout loadout = Object.FindFirstObjectByType<SpellBookLoadout>();
        Assert.That(inventory, Is.Not.Null);
        Assert.That(loadout, Is.Not.Null);

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        List<BackPackGridSlotView> spellBookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "spellBookSlots");
        BackPackGridSlotView sourceSlot = inventorySlots[0];
        BackPackGridSlotView targetSlot = spellBookSlots[0];
        PointerEventData dragEventData = CreatePointerEventData(new Vector2(180f, 160f));
        dragEventData.button = PointerEventData.InputButton.Left;

        sourceSlot.OnPointerDown(dragEventData);
        SetPrivateField(sourceSlot, "pointerDownTime", Time.unscaledTime - 1f);
        sourceSlot.OnInitializePotentialDrag(dragEventData);
        sourceSlot.OnBeginDrag(dragEventData);
        targetSlot.OnDrop(dragEventData);
        sourceSlot.OnEndDrag(dragEventData);

        Assert.That(scrollRect.BeginDragCallCount, Is.EqualTo(0));
        Assert.That(inventory.GetCell(0).IsOccupied, Is.False);
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.EquippedItems[0], Is.SameAs(inventoryToken));
    }

    [Test]
    public void NotifySlotHoverMove_KeepsHoverPreviewLockedInPlace()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("hover_move", "Move");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotClick(inventorySlots[0], CreatePointerEventData(new Vector2(120f, 120f)));

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        RectTransform hoverRect = hoverPreview.transform as RectTransform;
        Assert.That(hoverRect, Is.Not.Null);

        Vector2 before = hoverRect.anchoredPosition;

        screen.NotifySlotHoverMove(inventorySlots[0], CreatePointerEventData(new Vector2(0f, 100f)));
        Vector2 after = hoverRect.anchoredPosition;
        Assert.That(after.x, Is.EqualTo(before.x).Within(0.01f));
        Assert.That(after.y, Is.EqualTo(before.y).Within(0.01f));
    }

    [Test]
    public void NotifySlotClick_KeepsHoverPreviewInsidePreviewLayer()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("hover_clamp", "Clamp");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotClick(inventorySlots[0], CreatePointerEventData(new Vector2(10000f, 10000f)));

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        RectTransform hoverRect = hoverPreview.transform as RectTransform;
        RectTransform dragPreviewLayer = GetPrivateField<RectTransform>(screen, "dragPreviewLayer");
        Assert.That(hoverRect, Is.Not.Null);
        Assert.That(dragPreviewLayer, Is.Not.Null);

        Assert.That(hoverRect.anchoredPosition.x, Is.InRange(dragPreviewLayer.rect.xMin, dragPreviewLayer.rect.xMax));
        Assert.That(hoverRect.anchoredPosition.y, Is.InRange(dragPreviewLayer.rect.yMin, dragPreviewLayer.rect.yMax));
    }

    [Test]
    public void NotifySlotBeginDrag_HidesHoverPreview()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("hover_drag_hide",
            CreateToken<CoreTokenData>("hover_drag_core", "Core"),
            CreateToken<ResultTokenData>("hover_drag_result", "Result"));
        CreatePlayerWithState(inventoryLinked, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotClick(inventorySlots[0], CreatePointerEventData(new Vector2(200f, 200f)));

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);

        screen.NotifySlotBeginDrag(inventorySlots[0], CreatePointerEventData(new Vector2(200f, 200f)));
        Assert.That(hoverPreview.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void NotifySlotDrop_InventoryToInventory_SwapsEqualSpanLinkedItems()
    {
        LinkedTokenData sourceLinked = CreateLinkedToken("source_linked",
            CreateToken<CoreTokenData>("source_core", "SourceCore"),
            CreateToken<ResultTokenData>("source_result", "SourceResult"));
        LinkedTokenData targetLinked = CreateLinkedToken("target_linked",
            CreateToken<CoreTokenData>("target_core", "TargetCore"),
            CreateToken<ResultTokenData>("target_result", "TargetResult"));

        CreatePlayer(out PlayerBulletTokenInventory inventory, out _);
        Assert.That(inventory.TryPlaceItem(0, sourceLinked), Is.True);
        Assert.That(inventory.TryPlaceItem(2, targetLinked), Is.True);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotBeginDrag(inventorySlots[0], null);
        screen.NotifySlotDrop(inventorySlots[2]);
        screen.NotifySlotEndDrag(inventorySlots[0]);

        AssertInventoryItem(inventory, 0, targetLinked, 2);
        AssertInventoryItem(inventory, 2, sourceLinked, 2);
    }

    [Test]
    public void NotifySlotDrop_InventoryToInventory_DoesNotSwapWhenSpanMismatch()
    {
        LinkedTokenData sourceLinked = CreateLinkedToken("source_linked_mismatch",
            CreateToken<CoreTokenData>("source_mismatch_core", "SourceCore"),
            CreateToken<ResultTokenData>("source_mismatch_result", "SourceResult"));
        CoreTokenData targetSingle = CreateToken<CoreTokenData>("target_single", "TargetSingle");

        CreatePlayer(out PlayerBulletTokenInventory inventory, out _);
        Assert.That(inventory.TryPlaceItem(0, sourceLinked), Is.True);
        Assert.That(inventory.TryPlaceItem(2, targetSingle), Is.True);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotBeginDrag(inventorySlots[0], null);
        screen.NotifySlotDrop(inventorySlots[2]);
        screen.NotifySlotEndDrag(inventorySlots[0]);

        AssertInventoryItem(inventory, 0, sourceLinked, 2);
        Assert.That(inventory.GetCell(2).item, Is.SameAs(targetSingle));
        Assert.That(inventory.GetCell(3).IsOccupied, Is.False);
    }

    [Test]
    public void NotifySlotDrop_InventoryToSpellBook_SwapsEqualSpanLinkedItems()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("inventory_linked_swap",
            CreateToken<CoreTokenData>("inventory_swap_core", "InventoryCore"),
            CreateToken<ResultTokenData>("inventory_swap_result", "InventoryResult"));
        LinkedTokenData spellLinked = CreateLinkedToken("spell_linked_swap",
            CreateToken<CoreTokenData>("spell_swap_core", "SpellCore"),
            CreateToken<ResultTokenData>("spell_swap_result", "SpellResult"));

        CreatePlayer(out PlayerBulletTokenInventory inventory, out SpellBookLoadout loadout);
        Assert.That(inventory.TryPlaceItem(0, inventoryLinked), Is.True);
        loadout.SetItems(new[] { spellLinked });

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        List<BackPackGridSlotView> spellBookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "spellBookSlots");
        screen.NotifySlotBeginDrag(inventorySlots[0], null);
        screen.NotifySlotDrop(spellBookSlots[0]);
        screen.NotifySlotEndDrag(inventorySlots[0]);

        AssertInventoryItem(inventory, 0, spellLinked, 2);
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.EquippedItems[0], Is.SameAs(inventoryLinked));
    }

    [Test]
    public void NotifySlotDrop_SpellBookToInventory_SwapsEqualSpanLinkedItems()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("inventory_linked_swap_back",
            CreateToken<CoreTokenData>("inventory_swap_back_core", "InventoryBackCore"),
            CreateToken<ResultTokenData>("inventory_swap_back_result", "InventoryBackResult"));
        LinkedTokenData spellLinked = CreateLinkedToken("spell_linked_swap_back",
            CreateToken<CoreTokenData>("spell_swap_back_core", "SpellBackCore"),
            CreateToken<ResultTokenData>("spell_swap_back_result", "SpellBackResult"));

        CreatePlayer(out PlayerBulletTokenInventory inventory, out SpellBookLoadout loadout);
        Assert.That(inventory.TryPlaceItem(0, inventoryLinked), Is.True);
        loadout.SetItems(new[] { spellLinked });

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        List<BackPackGridSlotView> spellBookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "spellBookSlots");
        screen.NotifySlotBeginDrag(spellBookSlots[0], null);
        screen.NotifySlotDrop(inventorySlots[0]);
        screen.NotifySlotEndDrag(spellBookSlots[0]);

        AssertInventoryItem(inventory, 0, spellLinked, 2);
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.EquippedItems[0], Is.SameAs(inventoryLinked));
    }

    [Test]
    public void NotifySlotDrop_InventoryToSpellBook_DoesNotSwapWhenSpanMismatch()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("inventory_linked_no_swap",
            CreateToken<CoreTokenData>("inventory_no_swap_core", "InventoryNoSwapCore"),
            CreateToken<ResultTokenData>("inventory_no_swap_result", "InventoryNoSwapResult"));
        CoreTokenData spellSingle = CreateToken<CoreTokenData>("spell_single_no_swap", "SpellSingleNoSwap");

        CreatePlayer(out PlayerBulletTokenInventory inventory, out SpellBookLoadout loadout);
        Assert.That(inventory.TryPlaceItem(0, inventoryLinked), Is.True);
        loadout.SetItems(new[] { spellSingle });

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        List<BackPackGridSlotView> spellBookSlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "spellBookSlots");
        screen.NotifySlotBeginDrag(inventorySlots[0], null);
        screen.NotifySlotDrop(spellBookSlots[0]);
        screen.NotifySlotEndDrag(inventorySlots[0]);

        AssertInventoryItem(inventory, 0, inventoryLinked, 2);
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.EquippedItems[0], Is.SameAs(spellSingle));
    }

    [Test]
    public void OnBeforeShow_PersistsBackpackOpenedStoryFlag()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        CreatePlayerWithState(null, null);
        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        InvokeNonPublic(screen, "OnBeforeShow");

        Assert.That(saveService.HasStoryFlag(TutorialQuestConstants.BackpackOpenedFlagId), Is.True);
    }

    [Test]
    public void SyncSpellBookToLoadout_WithCompilableSpellBook_PersistsCompiledStoryFlag()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);

        CreatePlayerWithState(null, null);
        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        CoreTokenData coreToken = CreateToken<CoreTokenData>("tutorial_core", "TutorialCore");
        List<TokenCellOccupancy> spellBookCells = GetPrivateField<List<TokenCellOccupancy>>(screen, "spellBookCells");
        spellBookCells[0] = new TokenCellOccupancy(coreToken, 0, 0, true);

        InvokeNonPublic(screen, "SyncSpellBookToLoadout");

        Assert.That(saveService.HasStoryFlag(TutorialQuestConstants.SpellBookCompiledFlagId), Is.True);
    }

    [Test]
    public void RefreshFromCurrentPlayer_WritesSpellDescriptionText()
    {
        CoreTokenData coreToken = CreateToken<CoreTokenData>("spell_description_fire", "Fire");
        coreToken.CoreType = AttackCoreType.Fire;
        CreatePlayerWithState(null, coreToken);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TMP_Text spellDescriptionText = GetPrivateField<TMP_Text>(screen, "spellDescriptionText");
        Assert.That(spellDescriptionText, Is.Not.Null);
        Assert.That(spellDescriptionText.richText, Is.True);
        Assert.That(spellDescriptionText.text, Does.Contain("<color=#FF7A3D>"));
        Assert.That(spellDescriptionText.text, Does.Contain("火"));
    }

    [Test]
    public void RefreshFromCurrentPlayer_UsesSpellDescriptionCatalogJson()
    {
        CoreTokenData coreToken = CreateToken<CoreTokenData>("spell_description_catalog_fire", "Fire");
        coreToken.CoreType = AttackCoreType.Fire;
        CreatePlayerWithState(null, coreToken);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        TextAsset catalogJson = new(CreateCustomSpellDescriptionCatalogJson())
        {
            name = "Custom Spell Description Catalog",
        };
        createdObjects.Add(catalogJson);
        SetPrivateField(screen, "spellDescriptionCatalogJson", catalogJson);

        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TMP_Text spellDescriptionText = GetPrivateField<TMP_Text>(screen, "spellDescriptionText");
        Assert.That(spellDescriptionText.text, Does.Contain("焰"));
        Assert.That(spellDescriptionText.text, Does.Contain("文案表起咒"));
        Assert.That(spellDescriptionText.text, Does.Contain("穿表而出"));
        Assert.That(spellDescriptionText.text, Does.Contain("表中伤"));
    }

    [Test]
    public void SyncSpellBookToLoadout_RefreshesSpellDescriptionText()
    {
        CreatePlayerWithState(null, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TMP_Text spellDescriptionText = GetPrivateField<TMP_Text>(screen, "spellDescriptionText");
        string before = spellDescriptionText.text;

        CoreTokenData iceToken = CreateToken<CoreTokenData>("spell_description_ice", "Ice");
        iceToken.CoreType = AttackCoreType.Ice;
        List<TokenCellOccupancy> spellBookCells = GetPrivateField<List<TokenCellOccupancy>>(screen, "spellBookCells");
        spellBookCells[0] = new TokenCellOccupancy(iceToken, 0, 0, true);

        InvokeNonPublic(screen, "SyncSpellBookToLoadout");

        Assert.That(spellDescriptionText.text, Is.Not.EqualTo(before));
        Assert.That(spellDescriptionText.text, Does.Contain("冰"));
        Assert.That(spellDescriptionText.text, Does.Contain("<color=#FF7A3D>"));
    }

    [Test]
    public void ClearAllSlots_ClearsSpellDescriptionText()
    {
        CoreTokenData coreToken = CreateToken<CoreTokenData>("spell_description_clear", "Fire");
        CreatePlayerWithState(null, coreToken);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        TMP_Text spellDescriptionText = GetPrivateField<TMP_Text>(screen, "spellDescriptionText");
        Assert.That(spellDescriptionText.text, Is.Not.Empty);

        InvokeNonPublic(screen, "ClearAllSlots");

        Assert.That(spellDescriptionText.text, Is.Empty);
    }

    [Test]
    public void RefreshFromCurrentPlayer_WithoutPlayerClearsSpellDescriptionText()
    {
        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");

        TMP_Text spellDescriptionText = GetPrivateField<TMP_Text>(screen, "spellDescriptionText");
        spellDescriptionText.text = "旧法术描述";

        screen.RefreshFromCurrentPlayer();

        Assert.That(spellDescriptionText.text, Is.Empty);
    }

    private BackPackUIScreen CreateBackPackUIScreen()
    {
        GameObject root = CreateUiObject("BackPackUI");
        (root.transform as RectTransform).sizeDelta = new Vector2(800f, 600f);
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        BackPackUIScreen screen = root.AddComponent<BackPackUIScreen>();

        RectTransform mainContent = CreateUiObject("MainContent", root.transform).GetComponent<RectTransform>();
        RectTransform topPanel = CreateUiObject("Top Panel", mainContent).GetComponent<RectTransform>();
        GameObject spellBookObject = CreateUiObject("Spell Book", topPanel);
        spellBookObject.AddComponent<HorizontalLayoutGroup>();
        for (int i = 0; i < 5; i++)
        {
            CreateSlotView($"Spell Slot {i + 1:D2}", spellBookObject.transform);
        }

        GameObject descriptionPanel = CreateUiObject("Description Panel", mainContent);
        GameObject descriptionText = CreateUiObject("Text (TMP)", descriptionPanel.transform);
        descriptionText.AddComponent<TextMeshProUGUI>();

        RectTransform leftPanel = CreateUiObject("Left Panel", mainContent).GetComponent<RectTransform>();
        CreateUiObject("Preview Animation", leftPanel);

        RectTransform backPackGridPanel = CreateUiObject("BackPack Grid Panel", mainContent).GetComponent<RectTransform>();
        GameObject scrollViewObject = CreateUiObject("Scroll View", backPackGridPanel);
        scrollViewObject.AddComponent<Image>();
        TrackingScrollRect scrollRect = scrollViewObject.AddComponent<TrackingScrollRect>();
        GameObject viewportObject = CreateUiObject("Viewport", scrollViewObject.transform);
        viewportObject.AddComponent<Image>();
        viewportObject.AddComponent<Mask>().showMaskGraphic = false;
        GameObject gridContentObject = CreateUiObject("Grid Content", viewportObject.transform);
        gridContentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridContentObject.AddComponent<GridLayoutGroup>();
        scrollRect.viewport = viewportObject.transform as RectTransform;
        scrollRect.content = gridContentObject.transform as RectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        RectTransform bookSpecialGridPanel = CreateUiObject("Book/Special_Item Grid Panel", mainContent).GetComponent<RectTransform>();
        CreateAuxiliaryScrollGrid("Book Grid", bookSpecialGridPanel);
        CreateAuxiliaryScrollGrid("Special_Item Grid", bookSpecialGridPanel);

        SetPrivateField(screen, "hoverPreviewPrefab", CreateHoverPreviewTemplate());

        return screen;
    }

    private void CreateAuxiliaryScrollGrid(string rootName, Transform parent)
    {
        GameObject gridRoot = CreateUiObject(rootName, parent);
        GameObject scrollViewObject = CreateUiObject("Scroll View", gridRoot.transform);
        scrollViewObject.AddComponent<Image>();
        ScrollRect scrollRect = scrollViewObject.AddComponent<ScrollRect>();
        GameObject viewportObject = CreateUiObject("Viewport", scrollViewObject.transform);
        viewportObject.AddComponent<Image>();
        viewportObject.AddComponent<Mask>().showMaskGraphic = false;
        GameObject contentObject = CreateUiObject("Content", viewportObject.transform);
        contentObject.AddComponent<GridLayoutGroup>();
        contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewportObject.transform as RectTransform;
        scrollRect.content = contentObject.transform as RectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
    }

    private BulletTokenSelectionView CreateHoverPreviewTemplate()
    {
        GameObject root = CreateUiObject("Hover Preview Template");
        (root.transform as RectTransform).sizeDelta = new Vector2(460f, 500f);
        root.AddComponent<CanvasGroup>();
        BulletTokenSelectionView selectionView = root.AddComponent<BulletTokenSelectionView>();

        GameObject tokenRoot = CreateUiObject("Token", root.transform);
        GameObject tokenText = CreateUiObject("Text", tokenRoot.transform);
        tokenText.AddComponent<TextMeshProUGUI>();

        GameObject descriptionRoot = CreateUiObject("Description", root.transform);
        GameObject descriptionText = CreateUiObject("Text", descriptionRoot.transform);
        descriptionText.AddComponent<TextMeshProUGUI>();

        GameObject buttonObject = CreateUiObject("Button", root.transform);
        buttonObject.AddComponent<Image>();
        buttonObject.AddComponent<Button>();
        GameObject buttonText = CreateUiObject("Text", buttonObject.transform);
        buttonText.AddComponent<TextMeshProUGUI>();

        return selectionView;
    }

    private PointerEventData CreatePointerEventData(Vector2 position)
    {
        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = CreateGameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        PointerEventData eventData = new(eventSystem)
        {
            position = position,
        };

        return eventData;
    }

    private static void ConfigureInventoryScrollArea(ScrollRect scrollRect)
    {
        Assert.That(scrollRect, Is.Not.Null);
        RectTransform viewport = scrollRect.viewport;
        RectTransform content = scrollRect.content;
        Assert.That(viewport, Is.Not.Null);
        Assert.That(content, Is.Not.Null);
        viewport.sizeDelta = new Vector2(320f, 240f);
        content.sizeDelta = new Vector2(320f, 960f);
    }

    private void CreatePlayerWithState(PlaceableTokenData inventoryItem, PlaceableTokenData spellBookItem)
    {
        CreatePlayer(out PlayerBulletTokenInventory inventory, out SpellBookLoadout loadout);
        if (inventoryItem != null)
        {
            Assert.That(inventory.TryPlaceItem(0, inventoryItem), Is.True);
        }

        if (spellBookItem != null)
        {
            loadout.SetItems(new[] { spellBookItem });
        }
    }

    private void CreatePlayer(out PlayerBulletTokenInventory inventory, out SpellBookLoadout loadout)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        inventory = player.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        loadout = player.AddComponent<SpellBookLoadout>();
    }

    private SpellBookData CreateSpellBook(int slotCount)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        createdObjects.Add(spellBook);
        spellBook.SpellBookId = $"test_book_{slotCount}";
        spellBook.DisplayName = $"Test Book {slotCount}";
        spellBook.SlotCount = slotCount;
        return spellBook;
    }

    private RuntimeSaveService CreateSaveService()
    {
        DestroyExistingRuntimeSaveService();
        GameObject saveObject = CreateGameObject("RuntimeSaveService");
        return saveObject.AddComponent<RuntimeSaveService>();
    }

    private BackPackGridSlotView CreateSlotView(string name, Transform parent)
    {
        GameObject root = CreateUiObject(name, parent);
        root.AddComponent<CanvasGroup>();
        root.AddComponent<LayoutElement>();
        BackPackGridSlotView slotView = root.AddComponent<BackPackGridSlotView>();

        GameObject background = CreateUiObject("Background", root.transform);
        background.AddComponent<Image>();
        GameObject text = CreateUiObject("Text", root.transform);
        text.AddComponent<TextMeshProUGUI>();

        return slotView;
    }

    private GameObject CreateUiObject(string name, Transform parent = null)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private LinkedTokenData CreateLinkedToken(string itemId, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        token.ItemId = itemId;
        token.ConfiguredDamageMultiplier = 1.5f;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        createdObjects.Add(token);
        return token;
    }

    private static int GetActiveOutlineCount(object target, string fieldName)
    {
        List<LinkedTokenOutlineView> outlines = GetPrivateField<List<LinkedTokenOutlineView>>(target, fieldName);
        int count = 0;
        for (int i = 0; i < outlines.Count; i++)
        {
            if (outlines[i] != null && outlines[i].gameObject.activeSelf)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsHoldProgressRingVisible(BackPackGridSlotView slot)
    {
        Transform ring = slot != null ? slot.transform.Find("Hold Progress Ring") : null;
        return ring != null && ring.gameObject.activeSelf;
    }

    private static Image GetHoldProgressFillImage(BackPackGridSlotView slot)
    {
        Transform fill = slot != null ? slot.transform.Find("Hold Progress Ring/Fill") : null;
        return fill != null ? fill.GetComponent<Image>() : null;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        field.SetValue(target, value);
    }

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
    }

    private static float ResolveGridRequiredWidth(GridLayoutGroup layout)
    {
        return layout.padding.horizontal
            + (layout.cellSize.x * PlayerBulletTokenInventory.Columns)
            + (layout.spacing.x * Mathf.Max(0, PlayerBulletTokenInventory.Columns - 1));
    }

    private static float ResolveGridRequiredHeight(GridLayoutGroup layout)
    {
        int rowCount = Mathf.CeilToInt(PlayerBulletTokenInventory.Capacity / (float)PlayerBulletTokenInventory.Columns);
        return layout.padding.vertical
            + (layout.cellSize.y * rowCount)
            + (layout.spacing.y * Mathf.Max(0, rowCount - 1));
    }

    private static void AssertInventoryItem(PlayerBulletTokenInventory inventory, int anchorIndex, PlaceableTokenData expectedItem, int expectedSpan)
    {
        for (int i = 0; i < expectedSpan; i++)
        {
            TokenCellOccupancy cell = inventory.GetCell(anchorIndex + i);
            Assert.That(cell.item, Is.SameAs(expectedItem));
            Assert.That(cell.anchorIndex, Is.EqualTo(anchorIndex));
        }
    }

    private static string CreateCustomSpellDescriptionCatalogJson()
    {
        return @"
{
  ""coreLabels"": [
    { ""coreType"": ""Fire"", ""label"": ""焰"" }
  ],
  ""mainSentenceTemplates"": [
    ""{core}自文案表起咒，{behavior}，{result}。""
  ],
  ""behaviorPhrases"": [
    {
      ""behaviorType"": ""Straight"",
      ""phraseTemplates"": [
        ""<behavior>穿表而出</behavior>""
      ]
    }
  ],
  ""resultPhrases"": [
    {
      ""resultType"": ""DirectDamage"",
      ""phraseTemplates"": [
        ""留下<result>表中伤</result>""
      ]
    }
  ]
}";
    }

    private static void PrepareCleanSaveEnvironment()
    {
        DeleteSaveDirectory();
        GlobalModeSettingsService.LoadMode(GameMode.Normal, forceReload: true);
    }

    private static void DeleteSaveDirectory()
    {
        string saveDirectoryPath = Path.Combine(Application.persistentDataPath, "Saves");
        if (Directory.Exists(saveDirectoryPath))
        {
            Directory.Delete(saveDirectoryPath, recursive: true);
        }
    }

    private static void DestroyExistingRuntimeSaveService()
    {
        RuntimeSaveService existingService = Object.FindFirstObjectByType<RuntimeSaveService>();
        if (existingService != null)
        {
            Object.DestroyImmediate(existingService.gameObject);
        }
    }

    private sealed class TrackingScrollRect : ScrollRect
    {
        public int InitializePotentialDragCallCount { get; private set; }
        public int BeginDragCallCount { get; private set; }
        public int DragCallCount { get; private set; }
        public int EndDragCallCount { get; private set; }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            InitializePotentialDragCallCount++;
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            BeginDragCallCount++;
            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            DragCallCount++;
            base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            EndDragCallCount++;
            base.OnEndDrag(eventData);
        }
    }
}
