using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/BackPackUI.prefab");

        try
        {
            GridLayoutGroup grid = prefabRoot.transform.Find("MainContent/BackPack Grid Panel/Grid")?.GetComponent<GridLayoutGroup>();

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

    private PlayerBulletTokenInventory CreateInventory()
    {
        GameObject gameObject = new("PlayerInventory");
        createdObjects.Add(gameObject);
        PlayerBulletTokenInventory inventory = gameObject.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        return inventory;
    }

    private BackPackGridSlotView CreateSlotView(string name)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        createdObjects.Add(root);
        BackPackGridSlotView slotView = root.AddComponent<BackPackGridSlotView>();

        GameObject background = new("Background", typeof(RectTransform), typeof(Image));
        createdObjects.Add(background);
        background.transform.SetParent(root.transform, false);

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
}
