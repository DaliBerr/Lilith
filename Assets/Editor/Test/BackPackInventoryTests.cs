using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class BackPackInventoryTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void Inventory_AlwaysInitializesToFixedCapacity()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();

        inventory.EnsureInitialized();

        Assert.That(inventory.Slots.Count, Is.EqualTo(PlayerBulletTokenInventory.Capacity));
        Assert.That(inventory.FindFirstEmptySlot(), Is.EqualTo(0));
    }

    [Test]
    public void Inventory_AllowsDuplicateTokenReferences()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        CoreTokenData duplicateToken = CreateToken<CoreTokenData>("dup", "Dup");

        inventory.SetToken(0, duplicateToken);
        inventory.SetToken(1, duplicateToken);

        Assert.That(inventory.GetToken(0), Is.SameAs(duplicateToken));
        Assert.That(inventory.GetToken(1), Is.SameAs(duplicateToken));
        Assert.That(inventory.GetCell(0).anchorIndex, Is.EqualTo(0));
        Assert.That(inventory.GetCell(1).anchorIndex, Is.EqualTo(1));
    }

    [Test]
    public void Inventory_SwapSlots_ExchangesOccupiedAndEmptyEntries()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        CoreTokenData leftToken = CreateToken<CoreTokenData>("left", "Left");

        inventory.SetToken(0, leftToken);
        bool swapped = inventory.SwapSlots(0, 5);

        Assert.That(swapped, Is.True);
        Assert.That(inventory.GetToken(0), Is.Null);
        Assert.That(inventory.GetToken(5), Is.SameAs(leftToken));
    }

    [Test]
    public void Inventory_CanOnlyPlaceLinkedItemIntoContinuousSameRowSpace()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));

        Assert.That(inventory.CanPlaceItem(0, linked), Is.True);
        Assert.That(inventory.CanPlaceItem(7, linked), Is.False, "Linked item should not wrap across rows.");

        inventory.SetToken(1, CreateToken<CoreTokenData>("blocker", "Block"));
        Assert.That(inventory.CanPlaceItem(0, linked), Is.False, "Existing occupied cell should block placement.");
    }

    [Test]
    public void SpellBookLayout_CompactsAnchoredItemsBeforeWritingLoadout()
    {
        CoreTokenData firstToken = CreateToken<CoreTokenData>("fire", "Fire");
        LinkedTokenData linked = CreateLinkedToken("linked_core_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("core", "Core"),
            CreateToken<ResultTokenData>("hit", "Hit"));
        ResultTokenData thirdToken = CreateToken<ResultTokenData>("boom", "Boom");

        List<TokenCellOccupancy> cells = CreateCells(5);
        BackPackTokenLayoutUtility.WriteItem(cells, 0, firstToken);
        BackPackTokenLayoutUtility.WriteItem(cells, 1, linked);
        BackPackTokenLayoutUtility.WriteItem(cells, 3, thirdToken);

        List<PlaceableTokenData> compacted = BackPackTokenLayoutUtility.BuildCompactLoadoutItems(cells);

        Assert.That(compacted.Count, Is.EqualTo(3));
        Assert.That(compacted[0], Is.SameAs(firstToken));
        Assert.That(compacted[1], Is.SameAs(linked));
        Assert.That(compacted[2], Is.SameAs(thirdToken));
    }

    [Test]
    public void SpellBookLayout_OverflowItemsSpillIntoInventoryByWholeItem()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        PlaceableTokenData[] loadoutItems =
        {
            CreateToken<CoreTokenData>("token_0", "T0"),
            CreateLinkedToken("linked_1", "L1", 1.5f, CreateToken<CoreTokenData>("a", "A"), CreateToken<ResultTokenData>("b", "B")),
            CreateToken<CoreTokenData>("token_2", "T2"),
            CreateLinkedToken("linked_3", "L3", 2f, CreateToken<CoreTokenData>("c", "C"), CreateToken<ResultTokenData>("d", "D")),
        };

        List<TokenCellOccupancy> spellBookCells = CreateCells(5);
        int storedOverflowCount = BackPackTokenLayoutUtility.PopulateSpellBookCells(loadoutItems, spellBookCells, inventory, out int droppedOverflowCount);

        Assert.That(storedOverflowCount, Is.EqualTo(1));
        Assert.That(droppedOverflowCount, Is.EqualTo(0));
        Assert.That(spellBookCells[0].item, Is.SameAs(loadoutItems[0]));
        Assert.That(spellBookCells[1].item, Is.SameAs(loadoutItems[1]));
        Assert.That(spellBookCells[2].item, Is.SameAs(loadoutItems[1]));
        Assert.That(spellBookCells[3].item, Is.SameAs(loadoutItems[2]));
        Assert.That(spellBookCells[4].IsOccupied, Is.False);
        Assert.That(inventory.GetCell(0).item, Is.SameAs(loadoutItems[3]));
        Assert.That(inventory.GetCell(1).item, Is.SameAs(loadoutItems[3]));
    }

    [Test]
    public void Inventory_TryAddItem_ReturnsFalseWhenOnlyFragmentedSpaceRemains()
    {
        PlayerBulletTokenInventory inventory = CreateInventory();
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));
        CoreTokenData filler = CreateToken<CoreTokenData>("filler", "Fill");

        for (int i = 0; i < PlayerBulletTokenInventory.Capacity; i++)
        {
            if ((i % PlayerBulletTokenInventory.Columns) == PlayerBulletTokenInventory.Columns - 1)
            {
                continue;
            }

            Assert.That(inventory.SetToken(i, filler), Is.True);
        }

        bool added = inventory.TryAddItem(linked, out int insertedIndex);

        Assert.That(added, Is.False);
        Assert.That(insertedIndex, Is.EqualTo(-1));
    }

    [Test]
    public void Prefab_BackPackGridUsesFixedEightColumns()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Backpack/BackPackUI.prefab");

        try
        {
            GridLayoutGroup grid = prefabRoot.transform.Find("Grids Preview Panel/BackPack Grid Panel/Grid")?.GetComponent<GridLayoutGroup>();

            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(PlayerBulletTokenInventory.Columns));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void Prefab_BackPackUI_WiresHoverPreviewPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Backpack/BackPackUI.prefab");

        try
        {
            BackPackUIScreen screen = prefabRoot.GetComponent<BackPackUIScreen>();
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.PreservePrefabRootRectTransform, Is.True);

            RectTransform mainContent = GetPrivateField<RectTransform>(screen, "mainContent");
            Assert.That(mainContent, Is.EqualTo(prefabRoot.transform as RectTransform));

            BulletTokenSelectionView hoverPreviewPrefab = GetPrivateField<BulletTokenSelectionView>(screen, "hoverPreviewPrefab");
            Assert.That(hoverPreviewPrefab, Is.Not.Null);
            Assert.That(hoverPreviewPrefab.GetComponent<BulletTokenSelectionView>(), Is.Not.Null);

            TMP_Text spellDescriptionText = GetPrivateField<TMP_Text>(screen, "spellDescriptionText");
            Assert.That(spellDescriptionText, Is.Not.Null);
            Assert.That(spellDescriptionText.richText, Is.True);

            TextAsset spellDescriptionCatalogJson = GetPrivateField<TextAsset>(screen, "spellDescriptionCatalogJson");
            Assert.That(spellDescriptionCatalogJson, Is.Not.Null);
            Assert.That(spellDescriptionCatalogJson.name, Is.EqualTo("SpellDescriptionCatalog"));

            Vector2 hoverPreviewScreenOffset = GetPrivateField<Vector2>(screen, "hoverPreviewScreenOffset");
            Assert.That(hoverPreviewScreenOffset.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(hoverPreviewScreenOffset.y, Is.EqualTo(0f).Within(0.0001f));

            float hoverPreviewScale = GetPrivateField<float>(screen, "hoverPreviewScale");
            Assert.That(hoverPreviewScale, Is.EqualTo(0.7f).Within(0.0001f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void Prefab_BackPackGridPrefab_UsesBorderOnlyTypeColorMode()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Shared/BackPack Grid Prefab.prefab");

        try
        {
            BackPackGridSlotView slot = prefabRoot.GetComponent<BackPackGridSlotView>();
            Assert.That(slot, Is.Not.Null);

            BackPackSlotTypeColorDrawMode drawMode = GetPrivateField<BackPackSlotTypeColorDrawMode>(slot, "typeColorDrawMode");
            Image typeColorBorder = GetPrivateField<Image>(slot, "typeColorBorder");
            Transform background = prefabRoot.transform.Find("Background");
            Transform text = prefabRoot.transform.Find("Text");

            Assert.That(drawMode, Is.EqualTo(BackPackSlotTypeColorDrawMode.BorderOnly));
            Assert.That(typeColorBorder, Is.Not.Null);
            Assert.That(typeColorBorder.name, Is.EqualTo("Type Border"));
            Assert.That(typeColorBorder.GetComponent<FixedPixelSlicedImage>(), Is.Not.Null);
            Assert.That(typeColorBorder.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(typeColorBorder.fillCenter, Is.False);
            Assert.That(typeColorBorder.raycastTarget, Is.False);
            Assert.That(background, Is.Not.Null);
            Assert.That(text, Is.Not.Null);
            Assert.That(background.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(typeColorBorder.transform.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(text.GetSiblingIndex(), Is.EqualTo(2));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void SlotView_ReflectsChainRoleAndVisualToken()
    {
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", "FireHit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"),
            CreateToken<ValueTokenData>("value", "2"));
        BackPackGridSlotView head = CreateSlotView("Head");
        BackPackGridSlotView body = CreateSlotView("Body");
        BackPackGridSlotView tail = CreateSlotView("Tail");

        head.InitializeDisplayOnly(BackPackSlotArea.Inventory);
        head.SetOccupancy(new TokenCellOccupancy(linked, 0, 0, true));
        body.InitializeDisplayOnly(BackPackSlotArea.Inventory);
        body.SetOccupancy(new TokenCellOccupancy(linked, 0, 1, false));
        tail.InitializeDisplayOnly(BackPackSlotArea.Inventory);
        tail.SetOccupancy(new TokenCellOccupancy(linked, 0, 2, false));

        Assert.That(head.ChainRole, Is.EqualTo(BackPackChainCellRole.ChainHead));
        Assert.That(body.ChainRole, Is.EqualTo(BackPackChainCellRole.ChainBody));
        Assert.That(tail.ChainRole, Is.EqualTo(BackPackChainCellRole.ChainTail));
        Assert.That(GetSlotText(head), Is.EqualTo("Fire"));
        Assert.That(GetSlotText(body), Is.EqualTo("Hit"));
        Assert.That(GetSlotText(tail), Is.EqualTo("2"));
    }

    [Test]
    public void SlotView_UsesDistinctBackgroundTintForDifferentTokenTypes()
    {
        BackPackGridSlotView slot = CreateSlotView("TypeTint");
        slot.InitializeDisplayOnly(BackPackSlotArea.Inventory);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<CoreTokenData>("core", "Core"), 0, 0, true));
        Color coreColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<BehaviorTokenData>("behavior", "Behavior"), 0, 0, true));
        Color behaviorColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<ResultTokenData>("result", "Result"), 0, 0, true));
        Color resultColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<ValueTokenData>("value", "2"), 0, 0, true));
        Color valueColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<ModifierTokenData>("modifier", "Mod"), 0, 0, true));
        Color modifierColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<MulticastTokenData>("multicast", "Dual"), 0, 0, true));
        Color multicastColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<TriggerTokenData>("trigger", "Hit"), 0, 0, true));
        Color triggerColor = GetSlotBackgroundColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<PayloadBoundaryTokenData>("payload", "Start"), 0, 0, true));
        Color payloadColor = GetSlotBackgroundColor(slot);

        AssertColorApproximately(coreColor, new Color(1f, 0.82f, 0.62f, 0.35f));
        AssertColorApproximately(behaviorColor, new Color(0.66f, 0.82f, 1f, 0.35f));
        AssertColorApproximately(resultColor, new Color(1f, 0.68f, 0.68f, 0.35f));
        AssertColorApproximately(valueColor, new Color(0.68f, 0.93f, 0.68f, 0.35f));
        AssertColorApproximately(modifierColor, new Color(0.8f, 0.7f, 1f, 0.35f));
        AssertColorApproximately(multicastColor, new Color(1f, 0.88f, 0.55f, 0.35f));
        AssertColorApproximately(triggerColor, new Color(0.58f, 0.94f, 0.96f, 0.35f));
        AssertColorApproximately(payloadColor, new Color(1f, 0.74f, 0.88f, 0.35f));
    }

    [Test]
    public void SlotView_BorderOnlyDrawsTokenTintOnBorderAndKeepsBackgroundBaseColor()
    {
        BackPackGridSlotView slot = CreateSlotView("BorderTint", createTypeBorder: true);
        Image typeColorBorder = slot.transform.Find("Type Border")?.GetComponent<Image>();
        SetPrivateField(slot, "typeColorDrawMode", BackPackSlotTypeColorDrawMode.BorderOnly);
        SetPrivateField(slot, "typeColorBorder", typeColorBorder);
        slot.InitializeDisplayOnly(BackPackSlotArea.Inventory);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<CoreTokenData>("core", "Core"), 0, 0, true));
        Color coreBackgroundColor = GetSlotBackgroundColor(slot);
        Color coreBorderColor = GetSlotTypeBorderColor(slot);

        slot.SetOccupancy(new TokenCellOccupancy(CreateToken<BehaviorTokenData>("behavior", "Behavior"), 0, 0, true));
        Color behaviorBackgroundColor = GetSlotBackgroundColor(slot);
        Color behaviorBorderColor = GetSlotTypeBorderColor(slot);

        slot.SetOccupancy(TokenCellOccupancy.Empty);
        Color emptyBackgroundColor = GetSlotBackgroundColor(slot);
        Color emptyBorderColor = GetSlotTypeBorderColor(slot);

        AssertColorApproximately(coreBackgroundColor, new Color(1f, 1f, 1f, 0.35f));
        AssertColorApproximately(behaviorBackgroundColor, new Color(1f, 1f, 1f, 0.35f));
        AssertColorApproximately(emptyBackgroundColor, new Color(1f, 1f, 1f, 0.35f));
        AssertColorApproximately(coreBorderColor, new Color(1f, 0.82f, 0.62f, 1f));
        AssertColorApproximately(behaviorBorderColor, new Color(0.66f, 0.82f, 1f, 1f));
        AssertColorApproximately(emptyBorderColor, Color.clear);
    }

    private PlayerBulletTokenInventory CreateInventory()
    {
        GameObject gameObject = new("PlayerInventory");
        createdObjects.Add(gameObject);
        PlayerBulletTokenInventory inventory = gameObject.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        return inventory;
    }

    private BackPackGridSlotView CreateSlotView(string name, bool createTypeBorder = false)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        createdObjects.Add(root);
        BackPackGridSlotView slotView = root.AddComponent<BackPackGridSlotView>();

        GameObject background = new("Background", typeof(RectTransform), typeof(Image));
        createdObjects.Add(background);
        background.transform.SetParent(root.transform, false);

        if (createTypeBorder)
        {
            GameObject typeBorder = new("Type Border", typeof(RectTransform), typeof(Image));
            createdObjects.Add(typeBorder);
            typeBorder.transform.SetParent(root.transform, false);
        }

        GameObject text = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        createdObjects.Add(text);
        text.transform.SetParent(root.transform, false);
        return slotView;
    }

    private List<TokenCellOccupancy> CreateCells(int count)
    {
        List<TokenCellOccupancy> cells = new(count);
        for (int i = 0; i < count; i++)
        {
            cells.Add(TokenCellOccupancy.Empty);
        }

        return cells;
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

    private LinkedTokenData CreateLinkedToken(string itemId, string pickupDisplay, float damageMultiplier, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        token.ItemId = itemId;
        token.PickupDisplayTextOverride = pickupDisplay;
        token.ConfiguredDamageMultiplier = damageMultiplier;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        createdObjects.Add(token);
        return token;
    }

    private static string GetSlotText(BackPackGridSlotView slotView)
    {
        TMP_Text text = slotView.transform.Find("Text")?.GetComponent<TMP_Text>();
        return text != null ? text.text : string.Empty;
    }

    private static Color GetSlotBackgroundColor(BackPackGridSlotView slotView)
    {
        Image background = slotView.transform.Find("Background")?.GetComponent<Image>();
        Assert.That(background, Is.Not.Null);
        return background.color;
    }

    private static Color GetSlotTypeBorderColor(BackPackGridSlotView slotView)
    {
        Image typeColorBorder = slotView.transform.Find("Type Border")?.GetComponent<Image>();
        Assert.That(typeColorBorder, Is.Not.Null);
        return typeColorBorder.color;
    }

    private static void AssertColorApproximately(Color actual, Color expected, float tolerance = 0.0001f)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        field.SetValue(target, value);
    }
}
