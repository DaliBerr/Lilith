using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainUIScreenTests
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
    public void AttackFormulaLoadout_ChangedEvent_FiresWhenLoadoutMutates()
    {
        GameObject owner = CreateGameObject("Loadout Owner");
        AttackFormulaLoadout loadout = owner.AddComponent<AttackFormulaLoadout>();
        CoreTokenData fireToken = CreateToken<CoreTokenData>("fire", "Fire");
        int changedCount = 0;

        loadout.Changed += () => changedCount++;
        loadout.SetTokens(new BaseTokenData[]
        {
            fireToken,
        });
        loadout.MarkDirty();

        Assert.That(changedCount, Is.EqualTo(2));
    }

    [Test]
    public void MainUIScreen_RebuildsSpellPanelFromNonNullTokens()
    {
        CoreTokenData fireToken = CreateToken<CoreTokenData>("fire", "Fire");
        ResultTokenData boomToken = CreateToken<ResultTokenData>("boom", "Boom");
        CreatePlayerWithLoadout(new BaseTokenData[]
        {
            fireToken,
            null,
            boomToken,
        });

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out BackPackGridSlotView templateSlot);
        InvokeNonPublic(screen, "OnInit");

        List<BackPackGridSlotView> visibleSlots = GetVisibleSpellSlots(spellPanel);

        Assert.That(screen.SpellPanel, Is.SameAs(spellPanel));
        Assert.That(screen.SpellSlotTemplate, Is.SameAs(templateSlot));
        Assert.That(templateSlot.gameObject.activeSelf, Is.False);
        Assert.That(visibleSlots.Count, Is.EqualTo(2));
        Assert.That(visibleSlots[0].Token, Is.SameAs(fireToken));
        Assert.That(visibleSlots[1].Token, Is.SameAs(boomToken));
        Assert.That(GetTokenText(visibleSlots[0]), Is.EqualTo("Fire"));
        Assert.That(GetTokenText(visibleSlots[1]), Is.EqualTo("Boom"));
    }

    [Test]
    public void MainUIScreen_RefreshesImmediatelyWhenLoadoutChanges()
    {
        CoreTokenData fireToken = CreateToken<CoreTokenData>("fire", "Fire");
        BehaviorTokenData spreadToken = CreateToken<BehaviorTokenData>("spread", "Spread");
        AttackFormulaLoadout loadout = CreatePlayerWithLoadout(new BaseTokenData[]
        {
            fireToken,
        });

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out _);
        InvokeNonPublic(screen, "OnInit");

        loadout.SetTokens(new BaseTokenData[]
        {
            fireToken,
            null,
            spreadToken,
        });

        List<BackPackGridSlotView> visibleSlots = GetVisibleSpellSlots(spellPanel);

        Assert.That(visibleSlots.Count, Is.EqualTo(2));
        Assert.That(visibleSlots[0].Token, Is.SameAs(fireToken));
        Assert.That(visibleSlots[1].Token, Is.SameAs(spreadToken));

        loadout.SetTokens(System.Array.Empty<BaseTokenData>());

        Assert.That(GetVisibleSpellSlots(spellPanel).Count, Is.EqualTo(0));
    }

    [Test]
    public void MainUIScreen_ExpandsLinkedItemsIntoAdjacentHudSlots()
    {
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", 1.5f,
            CreateToken<CoreTokenData>("fire", "Fire"),
            CreateToken<ResultTokenData>("hit", "Hit"));
        AttackFormulaLoadout loadout = CreatePlayerWithItems(new PlaceableTokenData[]
        {
            linked,
        });

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out _);
        InvokeNonPublic(screen, "OnInit");

        List<BackPackGridSlotView> visibleSlots = GetVisibleSpellSlots(spellPanel);

        Assert.That(visibleSlots.Count, Is.EqualTo(2));
        Assert.That(visibleSlots[0].ChainRole, Is.EqualTo(BackPackChainCellRole.ChainHead));
        Assert.That(visibleSlots[1].ChainRole, Is.EqualTo(BackPackChainCellRole.ChainTail));
        Assert.That(GetTokenText(visibleSlots[0]), Is.EqualTo("Fire"));
        Assert.That(GetTokenText(visibleSlots[1]), Is.EqualTo("Hit"));
        Assert.That(GetActiveLinkedOutlines(screen.gameObject).Count, Is.EqualTo(1));

        loadout.SetItems(System.Array.Empty<PlaceableTokenData>());
        Assert.That(GetVisibleSpellSlots(spellPanel).Count, Is.EqualTo(0));
        Assert.That(GetActiveLinkedOutlines(screen.gameObject).Count, Is.EqualTo(0));
    }

    private MainUIScreen CreateMainUIScreen(out RectTransform spellPanel, out BackPackGridSlotView templateSlot)
    {
        GameObject root = CreateUiObject("MainUI");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        MainUIScreen screen = root.AddComponent<MainUIScreen>();

        RectTransform topPanel = CreateUiObject("TopPanel", root.transform).GetComponent<RectTransform>();
        GameObject healthPanel = CreateUiObject("HP Bar", topPanel);
        healthPanel.AddComponent<Image>();
        CreateTextObject("Titlle", healthPanel.transform);

        GameObject bar = CreateUiObject("Bar", healthPanel.transform);
        bar.AddComponent<HorizontalLayoutGroup>();
        bar.AddComponent<PlayerHealthBarController>();

        GameObject spellPanelObject = CreateUiObject("Spell Panel", topPanel);
        spellPanelObject.AddComponent<Image>();
        spellPanelObject.AddComponent<GridLayoutGroup>();
        spellPanel = spellPanelObject.GetComponent<RectTransform>();
        templateSlot = CreateSpellSlot("BackPack Grid Prefab", spellPanelObject.transform);

        return screen;
    }

    private AttackFormulaLoadout CreatePlayerWithLoadout(IEnumerable<BaseTokenData> tokens)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        AttackFormulaLoadout loadout = player.AddComponent<AttackFormulaLoadout>();
        loadout.SetTokens(tokens);
        return loadout;
    }

    private AttackFormulaLoadout CreatePlayerWithItems(IEnumerable<PlaceableTokenData> items)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        AttackFormulaLoadout loadout = player.AddComponent<AttackFormulaLoadout>();
        loadout.SetItems(items);
        return loadout;
    }

    private BackPackGridSlotView CreateSpellSlot(string name, Transform parent)
    {
        GameObject slotObject = CreateUiObject(name, parent);
        slotObject.AddComponent<CanvasGroup>();
        slotObject.AddComponent<LayoutElement>();
        BackPackGridSlotView slotView = slotObject.AddComponent<BackPackGridSlotView>();

        GameObject background = CreateUiObject("Background", slotObject.transform);
        background.AddComponent<Image>();
        CreateTextObject("Text", slotObject.transform);

        return slotView;
    }

    private GameObject CreateTextObject(string name, Transform parent)
    {
        GameObject textObject = CreateUiObject(name, parent);
        textObject.AddComponent<TextMeshProUGUI>();
        return textObject;
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

    private LinkedTokenData CreateLinkedToken(string itemId, float damageMultiplier, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        token.ItemId = itemId;
        token.ConfiguredDamageMultiplier = damageMultiplier;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        createdObjects.Add(token);
        return token;
    }

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
    }

    private static List<BackPackGridSlotView> GetVisibleSpellSlots(RectTransform spellPanel)
    {
        List<BackPackGridSlotView> result = new();
        for (int i = 0; i < spellPanel.childCount; i++)
        {
            Transform child = spellPanel.GetChild(i);
            BackPackGridSlotView slotView = child.GetComponent<BackPackGridSlotView>();
            if (slotView != null && child.gameObject.activeSelf)
            {
                result.Add(slotView);
            }
        }

        return result;
    }

    private static string GetTokenText(BackPackGridSlotView slotView)
    {
        TMP_Text text = slotView.transform.Find("Text")?.GetComponent<TMP_Text>();
        return text != null ? text.text : string.Empty;
    }

    private static List<LinkedTokenOutlineView> GetActiveLinkedOutlines(GameObject root)
    {
        List<LinkedTokenOutlineView> result = new();
        LinkedTokenOutlineView[] outlines = root.GetComponentsInChildren<LinkedTokenOutlineView>(true);
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null && outlines[i].gameObject.activeSelf)
            {
                result.Add(outlines[i]);
            }
        }

        return result;
    }
}
