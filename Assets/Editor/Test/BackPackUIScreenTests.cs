using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BackPackUIScreenTests
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

        return screen;
    }

    private void CreatePlayerWithState(PlaceableTokenData inventoryItem, PlaceableTokenData spellBookItem)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        PlayerBulletTokenInventory inventory = player.AddComponent<PlayerBulletTokenInventory>();
        inventory.EnsureInitialized();
        if (inventoryItem != null)
        {
            Assert.That(inventory.TryPlaceItem(0, inventoryItem), Is.True);
        }

        AttackFormulaLoadout loadout = player.AddComponent<AttackFormulaLoadout>();
        if (spellBookItem != null)
        {
            loadout.SetItems(new[] { spellBookItem });
        }
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

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
    }
}
