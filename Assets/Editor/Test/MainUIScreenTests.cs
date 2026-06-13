using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.Quest;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.EventSystem;
using Vocalith.UI;

public sealed class MainUIScreenTests
{
    private const string MainUIPrefabPath = "Assets/Prefabs/UI/MainHUD/MainUI.prefab";

    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingQuestService();

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
    public void SpellBookLoadout_ChangedEvent_FiresWhenLoadoutMutates()
    {
        GameObject owner = CreateGameObject("Loadout Owner");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
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

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out BackPackGridSlotView templateSlot, out _);
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
        SpellBookLoadout loadout = CreatePlayerWithLoadout(new BaseTokenData[]
        {
            fireToken,
        });

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out _, out _);
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
        SpellBookLoadout loadout = CreatePlayerWithItems(new PlaceableTokenData[]
        {
            linked,
        });

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out _, out _);
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

    [Test]
    public void MainUIScreen_ShowsSpellBookFixedItemsInHud()
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout loadout = player.AddComponent<SpellBookLoadout>();
        CoreTokenData fixedCore = CreateToken<CoreTokenData>("fixed_fire", "Fire");
        BehaviorTokenData spread = CreateToken<BehaviorTokenData>("spread", "Spread");
        SpellBookData spellBook = CreateSpellBook("hud_book", slotCount: 1);
        spellBook.SetFixedCastItems(new PlaceableTokenData[] { fixedCore });
        loadout.SetSpellBook(spellBook);
        loadout.SetItems(new PlaceableTokenData[] { spread });

        MainUIScreen screen = CreateMainUIScreen(out RectTransform spellPanel, out _, out _);
        InvokeNonPublic(screen, "OnInit");

        List<BackPackGridSlotView> visibleSlots = GetVisibleSpellSlots(spellPanel);

        Assert.That(visibleSlots.Count, Is.EqualTo(2));
        Assert.That(visibleSlots[0].Token, Is.SameAs(fixedCore));
        Assert.That(visibleSlots[1].Token, Is.SameAs(spread));
    }

    [Test]
    public void MainUIScreen_RebuildsQuestPanelFromActiveQuestSnapshots()
    {
        QuestService questService = CreateQuestService();
        SetQuestSnapshots(questService, new[]
        {
            new QuestActiveSnapshot("quest_1", "First quest"),
            new QuestActiveSnapshot("quest_2", "Second quest")
        });

        MainUIScreen screen = CreateMainUIScreen(out _, out _, out RectTransform questListRoot);
        SetNonPublicField(screen, "currentQuestService", questService);
        SetNonPublicField(screen, "questEntryPrefab", CreateQuestEntryTemplate("Quest Entry Template"));

        InvokeNonPublic(screen, "OnInit");

        QuestEntryView[] entries = questListRoot.GetComponentsInChildren<QuestEntryView>(true);

        Assert.That(screen.QuestPanel, Is.Not.Null);
        Assert.That(screen.QuestListRoot, Is.SameAs(questListRoot));
        Assert.That(entries.Length, Is.EqualTo(2));
        Assert.That(entries[0].QuestText.text, Is.EqualTo("First quest"));
        Assert.That(entries[1].QuestText.text, Is.EqualTo("Second quest"));
    }

    [Test]
    public void MainUIScreen_QuestCompletionFadeDestroysRuntimeEntry()
    {
        QuestService questService = CreateQuestService();
        SetQuestSnapshots(questService, new[]
        {
            new QuestActiveSnapshot("quest_done", "Fade me")
        });

        MainUIScreen screen = CreateMainUIScreen(out _, out _, out _);
        SetNonPublicField(screen, "currentQuestService", questService);
        SetNonPublicField(screen, "questEntryPrefab", CreateQuestEntryTemplate("Quest Entry Template"));
        SetNonPublicField(screen, "questEntryFadeDuration", 0f);

        InvokeNonPublic(screen, "OnInit");

        Dictionary<string, QuestEntryView> runtimeEntries = GetNonPublicField<Dictionary<string, QuestEntryView>>(screen, "runtimeQuestEntries");
        Assert.That(runtimeEntries.ContainsKey("quest_done"), Is.True);

        SetQuestSnapshots(questService, System.Array.Empty<QuestActiveSnapshot>());
        IEnumerator fade = InvokeNonPublic<IEnumerator>(screen, "PlayQuestEntryFadeOutCo", "quest_done", runtimeEntries["quest_done"]);
        while (fade.MoveNext())
        {
        }

        Assert.That(runtimeEntries.ContainsKey("quest_done"), Is.False);
    }

    [Test]
    public void MainUIScreen_AutoBindsObjectiveArrowView()
    {
        MainUIScreen screen = CreateMainUIScreen(out _, out _, out _);

        InvokeNonPublic(screen, "OnInit");

        Assert.That(screen.ObjectiveArrowView, Is.Not.Null);
        Assert.That(screen.ObjectiveArrowView.name, Is.EqualTo("Arrow Panel"));
    }

    [Test]
    public void MainUIScreen_OnAfterHideClearsObjectiveArrowTarget()
    {
        MainUIScreen screen = CreateMainUIScreen(out _, out _, out _);
        InvokeNonPublic(screen, "OnInit");
        GameObject target = CreateGameObject("Target");

        screen.ObjectiveArrowView.Bind(null, target.transform);
        Assert.That(GetSerializedObjectReference<Transform>(screen.ObjectiveArrowView, "targetTransform"), Is.SameAs(target.transform));

        InvokeNonPublic(screen, "OnAfterHide");

        Assert.That(GetSerializedObjectReference<Transform>(screen.ObjectiveArrowView, "targetTransform"), Is.Null);
    }

    [Test]
    public void MainUIScreen_RewardNotificationEvent_ShowsNotificationPanelWithoutImage()
    {
        MainUIScreen screen = CreateMainUIScreen(out _, out _, out _);
        InvokeNonPublic(screen, "OnInit");
        InvokeNonPublic(screen, "OnBeforeShow");

        EventManager.eventBus.Publish(new RewardNotificationEvent("火", "已收入背包", RewardNotificationKind.Token));

        Assert.That(screen.NotificationPanel.gameObject.activeSelf, Is.True);
        Assert.That(screen.NotificationTitleText.text, Is.EqualTo("火"));
        Assert.That(screen.NotificationDescriptionText.text, Is.EqualTo("已收入背包"));
        Assert.That(screen.NotificationCanvasGroup.alpha, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(screen.NotificationCanvasGroup.blocksRaycasts, Is.False);
        Assert.That(screen.NotificationImage.gameObject.activeSelf, Is.False);

        EventManager.eventBus.Publish(new RewardNotificationEvent("疾风书", "已装备", RewardNotificationKind.SpellBook));

        Assert.That(screen.NotificationTitleText.text, Is.EqualTo("疾风书"));
        Assert.That(screen.NotificationDescriptionText.text, Is.EqualTo("已装备"));

        InvokeNonPublic(screen, "OnAfterHide");
    }

    [Test]
    public void MainUIScreen_RewardNotificationAutoHide_HidesPanel()
    {
        MainUIScreen screen = CreateMainUIScreen(out _, out _, out _);
        SetNonPublicField(screen, "notificationDisplaySeconds", 0f);
        SetNonPublicField(screen, "notificationFadeSeconds", 0f);
        InvokeNonPublic(screen, "OnInit");
        InvokeNonPublic(screen, "OnBeforeShow");

        EventManager.eventBus.Publish(new RewardNotificationEvent("火", "已收入背包", RewardNotificationKind.Token));
        IEnumerator autoHide = InvokeNonPublic<IEnumerator>(screen, "PlayNotificationAutoHideCo");
        while (autoHide.MoveNext())
        {
        }

        Assert.That(screen.NotificationPanel.gameObject.activeSelf, Is.False);
        Assert.That(screen.NotificationCanvasGroup.alpha, Is.EqualTo(0f).Within(0.0001f));

        InvokeNonPublic(screen, "OnAfterHide");
    }

    [Test]
    public void MainUIScreen_OnAfterHideStopsListeningToRewardNotifications()
    {
        MainUIScreen screen = CreateMainUIScreen(out _, out _, out _);
        InvokeNonPublic(screen, "OnInit");
        InvokeNonPublic(screen, "OnBeforeShow");
        InvokeNonPublic(screen, "OnAfterHide");

        EventManager.eventBus.Publish(new RewardNotificationEvent("火", "已收入背包", RewardNotificationKind.Token));

        Assert.That(screen.NotificationPanel.gameObject.activeSelf, Is.False);
        Assert.That(screen.NotificationTitleText.text, Is.Empty);
    }

    [Test]
    public void MainUIPrefabContainsHiddenObjectiveArrowView()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainUIPrefabPath);
        try
        {
            MainUIScreen screen = root.GetComponent<MainUIScreen>();
            Assert.That(screen, Is.Not.Null);

            ObjectiveArrowView arrowView = GetSerializedObjectReference<ObjectiveArrowView>(screen, "objectiveArrowView");
            Assert.That(arrowView, Is.Not.Null);
            Assert.That(arrowView.name, Is.EqualTo("Arrow Panel"));

            RectTransform panelRoot = GetSerializedObjectReference<RectTransform>(arrowView, "panelRoot");
            Assert.That(panelRoot, Is.Not.Null);
            Assert.That(panelRoot.name, Is.EqualTo("Arrow Panel"));

            Image panelImage = panelRoot.GetComponent<Image>();
            Assert.That(panelImage, Is.Not.Null);
            Assert.That(panelImage.raycastTarget, Is.False);

            RectTransform arrowRect = GetSerializedObjectReference<RectTransform>(arrowView, "arrowRect");
            Assert.That(arrowRect, Is.Not.Null);
            Assert.That(arrowRect.name, Is.EqualTo("Arrow"));
            Assert.That(arrowRect.gameObject.activeSelf, Is.False);
            Assert.That(arrowRect.sizeDelta.x, Is.EqualTo(64f).Within(0.001f));
            Assert.That(arrowRect.sizeDelta.y, Is.EqualTo(64f).Within(0.001f));
            Assert.That(arrowRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(arrowRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));

            Image arrowImage = GetSerializedObjectReference<Image>(arrowView, "arrowImage");
            Assert.That(arrowImage, Is.Not.Null);
            Assert.That(arrowImage.raycastTarget, Is.False);
            Assert.That(arrowImage.preserveAspect, Is.True);
            Assert.That(arrowImage.sprite, Is.Not.Null);
            StringAssert.StartsWith("ObjectiveArrow", arrowImage.sprite.name);

            InvokeNonPublic(screen, "AutoBindTemplate");

            RectTransform notificationPanel = screen.NotificationPanel;
            Assert.That(notificationPanel, Is.Not.Null);
            Assert.That(notificationPanel.name, Is.EqualTo("Notification Panel"));
            Assert.That(notificationPanel.gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("TopPanel"), Is.Null);
            Assert.That(screen.TopPanel, Is.Not.Null);
            Assert.That(screen.TopPanel.name, Is.EqualTo("Player Info Panel"));
            Assert.That(screen.HealthPanel, Is.Not.Null);
            Assert.That(screen.HealthPanel.name, Is.EqualTo("Hp bar"));
            Assert.That(screen.HealthTitleText, Is.Null);
            Assert.That(screen.HealthPanel.Find("Titlle"), Is.Null);
            Assert.That(screen.HealthBarRoot, Is.Not.Null);
            Assert.That(screen.HealthBarRoot.name, Is.EqualTo("Bar"));
            Assert.That(screen.HealthBarRoot.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(screen.HealthBarRoot.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(screen.HealthBarRoot.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(screen.SpellPanel, Is.Not.Null);
            Assert.That(screen.SpellPanel.name, Is.EqualTo("Spell"));
            Assert.That(screen.SpellPanel.Find("Book"), Is.Not.Null);
            Assert.That(screen.SpellPanel.Find("Book").GetComponentInChildren<BackPackGridSlotView>(true), Is.Not.Null);
            Assert.That(screen.SpellSlotTemplate, Is.Not.Null);
            Assert.That(screen.SpellSlotTemplate.transform.IsChildOf(screen.SpellPanel), Is.True);
            Assert.That(screen.SpellSlotTemplate.GetComponent<BackPackGridSlotView>(), Is.Not.Null);

            CanvasGroup notificationCanvasGroup = screen.NotificationCanvasGroup;
            Assert.That(notificationCanvasGroup, Is.Not.Null);
            Assert.That(notificationCanvasGroup.alpha, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(notificationCanvasGroup.blocksRaycasts, Is.False);
            Assert.That(notificationCanvasGroup.interactable, Is.False);

            TMP_Text notificationTitle = screen.NotificationTitleText;
            TMP_Text notificationDescription = screen.NotificationDescriptionText;
            Image notificationImage = screen.NotificationImage;
            Assert.That(notificationTitle, Is.Not.Null);
            Assert.That(notificationDescription, Is.Not.Null);
            Assert.That(notificationImage, Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void MainUIPrefab_HasResponsiveLayoutFitterForSpellGrid()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainUIPrefabPath);
        try
        {
            ResponsiveLayoutGroupFitter[] fitters = root.GetComponentsInChildren<ResponsiveLayoutGroupFitter>(true);
            Assert.That(fitters.Length, Is.EqualTo(1));
            Assert.That(fitters[0].transform, Is.SameAs(root.transform));

            Transform spellPanel = root.transform.Find("Content Safe Frame/Player Info Panel/Panel/Spell");
            Assert.That(spellPanel, Is.Not.Null);
            Assert.That(spellPanel.GetComponent<GridLayoutGroup>(), Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void MainUIPrefab_UsesLocalQuestAndNotificationLayoutWithoutLinearFitter()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainUIPrefabPath);
        GameObject questEntryRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/MainHUD/Quest Entry.prefab");
        try
        {
            RectTransform questPanel = root.transform.Find("Content Safe Frame/Quest Panel") as RectTransform;
            RectTransform quests = root.transform.Find("Content Safe Frame/Quest Panel/Quests") as RectTransform;
            TMP_Text questTitleText = root.transform.Find("Content Safe Frame/Quest Panel/Tittle/Text (TMP)")?.GetComponent<TMP_Text>();
            RectTransform notificationPanel = root.transform.Find("Content Safe Frame/Notification Panel") as RectTransform;
            TMP_Text notificationTitle = root.transform.Find("Content Safe Frame/Notification Panel/Tittle/Text (TMP)")?.GetComponent<TMP_Text>();
            TMP_Text notificationDescription = root.transform.Find("Content Safe Frame/Notification Panel/Description/Text (TMP)")?.GetComponent<TMP_Text>();
            TMP_Text questEntryText = questEntryRoot.transform.Find("Quest Text")?.GetComponent<TMP_Text>();
            LayoutElement questEntryLayout = questEntryRoot.GetComponent<LayoutElement>();

            Assert.That(questPanel, Is.Not.Null);
            Assert.That(quests, Is.Not.Null);
            Assert.That(notificationPanel, Is.Not.Null);
            Assert.That(questPanel.anchorMin.x, Is.LessThanOrEqualTo(0.75f));
            Assert.That(questPanel.anchorMax.x, Is.LessThanOrEqualTo(0.99f));
            Assert.That(questPanel.anchorMax.x - questPanel.anchorMin.x, Is.GreaterThanOrEqualTo(0.2f));
            Assert.That(quests.GetComponent<ContentSizeFitter>(), Is.Null);
            Assert.That(questTitleText, Is.Not.Null);
            Assert.That(questTitleText.enableAutoSizing, Is.True);
            Assert.That(questTitleText.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(questTitleText.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));

            Assert.That(notificationPanel.anchorMax.x, Is.GreaterThan(notificationPanel.anchorMin.x));
            Assert.That(notificationPanel.anchorMax.y, Is.GreaterThan(notificationPanel.anchorMin.y));
            Assert.That(notificationTitle, Is.Not.Null);
            Assert.That(notificationDescription, Is.Not.Null);
            Assert.That(notificationTitle.enableAutoSizing, Is.True);
            Assert.That(notificationDescription.enableAutoSizing, Is.True);
            Assert.That(notificationTitle.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(notificationDescription.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));

            Assert.That(questEntryText, Is.Not.Null);
            Assert.That(questEntryText.enableAutoSizing, Is.True);
            Assert.That(questEntryText.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(questEntryLayout, Is.Not.Null);
            Assert.That(questEntryLayout.preferredHeight, Is.GreaterThanOrEqualTo(50f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(questEntryRoot);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private MainUIScreen CreateMainUIScreen(out RectTransform spellPanel, out BackPackGridSlotView templateSlot, out RectTransform questListRoot)
    {
        GameObject root = CreateUiObject("MainUI");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        MainUIScreen screen = root.AddComponent<MainUIScreen>();

        GameObject playerInfoPanel = CreateUiObject("Player Info Panel", root.transform);
        playerInfoPanel.AddComponent<Image>();
        playerInfoPanel.AddComponent<HorizontalLayoutGroup>();
        playerInfoPanel.AddComponent<ContentSizeFitter>();

        GameObject avatar = CreateUiObject("Avator", playerInfoPanel.transform);
        avatar.AddComponent<Image>();
        avatar.AddComponent<LayoutElement>();

        GameObject panel = CreateUiObject("Panel", playerInfoPanel.transform);
        panel.AddComponent<Image>();
        panel.AddComponent<LayoutElement>();
        panel.AddComponent<VerticalLayoutGroup>();
        panel.AddComponent<ContentSizeFitter>();

        GameObject healthPanel = CreateUiObject("Hp bar", panel.transform);
        healthPanel.AddComponent<Image>();
        healthPanel.AddComponent<LayoutElement>();
        healthPanel.AddComponent<VerticalLayoutGroup>();
        healthPanel.AddComponent<ContentSizeFitter>();

        GameObject bar = CreateUiObject("Bar", healthPanel.transform);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = Vector2.zero;
        bar.AddComponent<HorizontalLayoutGroup>();
        bar.AddComponent<PlayerHealthBarController>();

        GameObject spellPanelObject = CreateUiObject("Spell", panel.transform);
        spellPanelObject.AddComponent<Image>();
        spellPanelObject.AddComponent<LayoutElement>();
        spellPanelObject.AddComponent<GridLayoutGroup>();
        spellPanelObject.AddComponent<ContentSizeFitter>();
        spellPanel = spellPanelObject.GetComponent<RectTransform>();

        GameObject book = CreateUiObject("Book", spellPanelObject.transform);
        book.AddComponent<Image>();
        CreateSpellSlot("Book Slot", book.transform);

        templateSlot = CreateSpellSlot("Runtime Spell Template", spellPanelObject.transform);

        GameObject power = CreateUiObject("Power", panel.transform);
        power.AddComponent<Image>();
        power.AddComponent<LayoutElement>();

        GameObject questPanel = CreateUiObject("Quest Panel", root.transform);
        questPanel.AddComponent<Image>();
        GameObject questTitle = CreateUiObject("Tittle", questPanel.transform);
        questTitle.AddComponent<Image>();
        CreateTextObject("Text (TMP)", questTitle.transform);
        GameObject quests = CreateUiObject("Quests", questPanel.transform);
        quests.AddComponent<Image>();
        quests.AddComponent<VerticalLayoutGroup>();
        quests.AddComponent<ContentSizeFitter>();
        questListRoot = quests.GetComponent<RectTransform>();

        CreateObjectiveArrowView(root.transform);
        CreateNotificationPanel(root.transform);

        return screen;
    }

    private void CreateNotificationPanel(Transform parent)
    {
        GameObject panelObject = CreateUiObject("Notification Panel", parent);
        panelObject.AddComponent<Image>();
        CanvasGroup canvasGroup = panelObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject titleRoot = CreateUiObject("Tittle", panelObject.transform);
        titleRoot.AddComponent<Image>();
        CreateTextObject("Text (TMP)", titleRoot.transform).GetComponent<TMP_Text>().text = string.Empty;

        GameObject descriptionRoot = CreateUiObject("Description", panelObject.transform);
        descriptionRoot.AddComponent<Image>();
        CreateTextObject("Text (TMP)", descriptionRoot.transform).GetComponent<TMP_Text>().text = string.Empty;

        GameObject imageObject = CreateUiObject("Image", panelObject.transform);
        imageObject.AddComponent<Image>();
        panelObject.SetActive(false);
    }

    private ObjectiveArrowView CreateObjectiveArrowView(Transform parent)
    {
        GameObject panelObject = CreateUiObject("Arrow Panel", parent);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.raycastTarget = true;
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject arrowObject = CreateUiObject("Arrow", panelObject.transform);
        RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(64f, 64f);
        Image arrowImage = arrowObject.AddComponent<Image>();
        arrowImage.raycastTarget = true;
        arrowObject.SetActive(false);

        ObjectiveArrowView arrowView = panelObject.AddComponent<ObjectiveArrowView>();
        SetNonPublicField(arrowView, "panelRoot", panelRect);
        SetNonPublicField(arrowView, "arrowRect", arrowRect);
        SetNonPublicField(arrowView, "arrowImage", arrowImage);
        return arrowView;
    }

    private QuestEntryView CreateQuestEntryTemplate(string name)
    {
        GameObject templateObject = CreateUiObject(name);
        templateObject.AddComponent<Image>();
        CanvasGroup canvasGroup = templateObject.AddComponent<CanvasGroup>();
        templateObject.AddComponent<LayoutElement>();
        QuestEntryView entryView = templateObject.AddComponent<QuestEntryView>();
        TMP_Text questText = CreateTextObject("Quest Text", templateObject.transform).GetComponent<TMP_Text>();
        SetNonPublicField(entryView, "questText", questText);
        SetNonPublicField(entryView, "canvasGroup", canvasGroup);
        templateObject.SetActive(false);
        return entryView;
    }

    private QuestService CreateQuestService()
    {
        DestroyExistingQuestService();
        GameObject questObject = CreateGameObject("QuestService");
        return questObject.AddComponent<QuestService>();
    }

    private SpellBookLoadout CreatePlayerWithLoadout(IEnumerable<BaseTokenData> tokens)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout loadout = player.AddComponent<SpellBookLoadout>();
        loadout.SetTokens(tokens);
        return loadout;
    }

    private SpellBookLoadout CreatePlayerWithItems(IEnumerable<PlaceableTokenData> items)
    {
        GameObject player = CreateGameObject("Player");
        player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout loadout = player.AddComponent<SpellBookLoadout>();
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

    private SpellBookData CreateSpellBook(string spellBookId, int slotCount)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        spellBook.SpellBookId = spellBookId;
        spellBook.DisplayName = spellBookId;
        spellBook.SlotCount = slotCount;
        createdObjects.Add(spellBook);
        return spellBook;
    }

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
    }

    private static T InvokeNonPublic<T>(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        return (T)method.Invoke(target, arguments);
    }

    private static void SetQuestSnapshots(QuestService service, IEnumerable<QuestActiveSnapshot> snapshots)
    {
        List<QuestActiveSnapshot> runtimeSnapshots = GetNonPublicField<List<QuestActiveSnapshot>>(service, "activeQuestSnapshots");
        runtimeSnapshots.Clear();
        runtimeSnapshots.AddRange(snapshots);
    }

    private static T GetNonPublicField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        return (T)field.GetValue(target);
    }

    private static void SetNonPublicField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        field.SetValue(target, value);
    }

    private static T GetSerializedObjectReference<T>(Object target, string propertyName)
        where T : Object
    {
        SerializedObject serializedObject = new(target);
        return serializedObject.FindProperty(propertyName).objectReferenceValue as T;
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

    private static void DestroyExistingQuestService()
    {
        QuestService existingService = Object.FindFirstObjectByType<QuestService>();
        if (existingService != null)
        {
            Object.DestroyImmediate(existingService.gameObject);
        }
    }
}
