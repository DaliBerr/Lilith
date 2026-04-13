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

public sealed class BackPackUIScreenTests
{
    private readonly List<Object> createdObjects = new();

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
    public void NotifySlotHoverEnter_WithOccupiedSlot_ShowsHoverPreviewAndBindsToken()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("hover_fire", "Fire");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        PointerEventData hoverEventData = CreatePointerEventData(new Vector2(320f, 240f));
        screen.NotifySlotHoverEnter(inventorySlots[0], hoverEventData);

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview, Is.Not.Null);
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);
        Assert.That(hoverPreview.BoundToken, Is.SameAs(inventoryToken));
        Assert.That(hoverPreview.SelectButton, Is.Not.Null);
        Assert.That(hoverPreview.SelectButton.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void NotifySlotHoverMove_UpdatesHoverPreviewPosition()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("hover_move", "Move");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotHoverEnter(inventorySlots[0], CreatePointerEventData(new Vector2(120f, 120f)));

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        RectTransform hoverRect = hoverPreview.transform as RectTransform;
        Assert.That(hoverRect, Is.Not.Null);
        Vector2 before = hoverRect.anchoredPosition;

        screen.NotifySlotHoverMove(inventorySlots[0], CreatePointerEventData(new Vector2(420f, 220f)));
        Vector2 after = hoverRect.anchoredPosition;
        Assert.That(after, Is.Not.EqualTo(before));
    }

    [Test]
    public void NotifySlotHoverExit_HidesHoverPreview()
    {
        CoreTokenData inventoryToken = CreateToken<CoreTokenData>("hover_exit", "Exit");
        CreatePlayerWithState(inventoryToken, null);

        BackPackUIScreen screen = CreateBackPackUIScreen();
        InvokeNonPublic(screen, "OnInit");
        screen.RefreshFromCurrentPlayer();

        List<BackPackGridSlotView> inventorySlots = GetPrivateField<List<BackPackGridSlotView>>(screen, "inventorySlots");
        screen.NotifySlotHoverEnter(inventorySlots[0], CreatePointerEventData(new Vector2(220f, 180f)));

        BulletTokenSelectionView hoverPreview = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewView");
        Assert.That(hoverPreview.gameObject.activeSelf, Is.True);

        screen.NotifySlotHoverExit(inventorySlots[0]);
        Assert.That(hoverPreview.gameObject.activeSelf, Is.False);
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
        screen.NotifySlotHoverEnter(inventorySlots[0], CreatePointerEventData(new Vector2(200f, 200f)));

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

        CreatePlayer(out PlayerBulletTokenInventory inventory, out AttackFormulaLoadout loadout);
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
        Assert.That(loadout.Items.Count, Is.EqualTo(1));
        Assert.That(loadout.Items[0], Is.SameAs(inventoryLinked));
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

        CreatePlayer(out PlayerBulletTokenInventory inventory, out AttackFormulaLoadout loadout);
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
        Assert.That(loadout.Items.Count, Is.EqualTo(1));
        Assert.That(loadout.Items[0], Is.SameAs(inventoryLinked));
    }

    [Test]
    public void NotifySlotDrop_InventoryToSpellBook_DoesNotSwapWhenSpanMismatch()
    {
        LinkedTokenData inventoryLinked = CreateLinkedToken("inventory_linked_no_swap",
            CreateToken<CoreTokenData>("inventory_no_swap_core", "InventoryNoSwapCore"),
            CreateToken<ResultTokenData>("inventory_no_swap_result", "InventoryNoSwapResult"));
        CoreTokenData spellSingle = CreateToken<CoreTokenData>("spell_single_no_swap", "SpellSingleNoSwap");

        CreatePlayer(out PlayerBulletTokenInventory inventory, out AttackFormulaLoadout loadout);
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
        Assert.That(loadout.Items.Count, Is.EqualTo(1));
        Assert.That(loadout.Items[0], Is.SameAs(spellSingle));
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

    private BackPackUIScreen CreateBackPackUIScreen()
    {
        GameObject root = CreateUiObject("BackPackUI");
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

        RectTransform leftPanel = CreateUiObject("Left Panel", mainContent).GetComponent<RectTransform>();
        CreateUiObject("Preview Animation", leftPanel);

        RectTransform backPackGridPanel = CreateUiObject("BackPack Grid Panel", mainContent).GetComponent<RectTransform>();
        GameObject backPackGridObject = CreateUiObject("Grid", backPackGridPanel);
        backPackGridObject.AddComponent<GridLayoutGroup>();

        SetPrivateField(screen, "hoverPreviewPrefab", CreateHoverPreviewTemplate());

        return screen;
    }

    private BulletTokenSelectionView CreateHoverPreviewTemplate()
    {
        GameObject root = CreateUiObject("Hover Preview Template");
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

    private void CreatePlayerWithState(PlaceableTokenData inventoryItem, PlaceableTokenData spellBookItem)
    {
        CreatePlayer(out PlayerBulletTokenInventory inventory, out AttackFormulaLoadout loadout);
        if (inventoryItem != null)
        {
            Assert.That(inventory.TryPlaceItem(0, inventoryItem), Is.True);
        }

        if (spellBookItem != null)
        {
            loadout.SetItems(new[] { spellBookItem });
        }
    }

    private void CreatePlayer(out PlayerBulletTokenInventory inventory, out AttackFormulaLoadout loadout)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        inventory = player.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        loadout = player.AddComponent<AttackFormulaLoadout>();
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

    private static void AssertInventoryItem(PlayerBulletTokenInventory inventory, int anchorIndex, PlaceableTokenData expectedItem, int expectedSpan)
    {
        for (int i = 0; i < expectedSpan; i++)
        {
            TokenCellOccupancy cell = inventory.GetCell(anchorIndex + i);
            Assert.That(cell.item, Is.SameAs(expectedItem));
            Assert.That(cell.anchorIndex, Is.EqualTo(anchorIndex));
        }
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
}
