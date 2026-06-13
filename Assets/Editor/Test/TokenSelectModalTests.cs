using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Vocalith.UI;
using VocalithRandom = Vocalith.Random;

public sealed class TokenSelectModalTests
{
    private const string SelectionPrefabPath = "Assets/Prefabs/UI/TokenSelect/BulletToken Selection Prefab.prefab";

    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void SampleChoices_ReturnsUniqueAndClampedTokens()
    {
        CoreTokenData fire = CreateToken<CoreTokenData>("fire", "Fire", "Fire core");
        CoreTokenData ice = CreateToken<CoreTokenData>("ice", "Ice", "Ice core");

        List<PlaceableTokenData> sample = BulletTokenSelectionSampler.SampleChoices(
            new PlaceableTokenData[]
            {
                fire,
                ice,
                fire,
                null,
                ice,
            },
            new VocalithRandom(12345),
            desiredCount: 3);

        Assert.That(sample.Count, Is.EqualTo(2));
        AssertUniqueInstanceIds(sample);
    }

    [Test]
    public void SampleChoices_IsDeterministicWithSeededRandom()
    {
        List<PlaceableTokenData> source = new()
        {
            CreateToken<CoreTokenData>("fire", "Fire", "Fire core"),
            CreateToken<CoreTokenData>("ice", "Ice", "Ice core"),
            CreateToken<CoreTokenData>("thunder", "Thunder", "Thunder core"),
            CreateToken<CoreTokenData>("edge", "Edge", "Edge core"),
            CreateToken<CoreTokenData>("wind", "Wind", "Wind core"),
        };

        List<PlaceableTokenData> first = BulletTokenSelectionSampler.SampleChoices(source, new VocalithRandom(24680));
        List<PlaceableTokenData> second = BulletTokenSelectionSampler.SampleChoices(source, new VocalithRandom(24680));

        Assert.That(first.Count, Is.EqualTo(second.Count));
        for (int i = 0; i < first.Count; i++)
        {
            Assert.That(first[i], Is.SameAs(second[i]));
        }
    }

    [Test]
    public void BulletTokenLibrary_SampleChoices_ExcludesZeroWeightTokens()
    {
        CoreTokenData fire = CreateToken<CoreTokenData>("fire", "Fire", "Fire core");
        CoreTokenData ice = CreateToken<CoreTokenData>("ice", "Ice", "Ice core");
        BulletTokenLibrary library = CreateRuntimeLibrary(fire, ice);
        library.SetTokenWeight(fire, 1f);
        library.SetTokenWeight(ice, 0f);

        for (int i = 0; i < 8; i++)
        {
            List<PlaceableTokenData> sample = library.SampleChoices(new VocalithRandom(1000 + i), desiredCount: 1);
            Assert.That(sample.Count, Is.EqualTo(1));
            Assert.That(sample[0], Is.SameAs(fire));
        }
    }

    [Test]
    public void BulletTokenLibrary_SampleChoices_IsDeterministicWithSeededRandom()
    {
        CoreTokenData fire = CreateToken<CoreTokenData>("fire", "Fire", "Fire core");
        CoreTokenData ice = CreateToken<CoreTokenData>("ice", "Ice", "Ice core");
        CoreTokenData thunder = CreateToken<CoreTokenData>("thunder", "Thunder", "Thunder core");
        CoreTokenData edge = CreateToken<CoreTokenData>("edge", "Edge", "Edge core");
        BulletTokenLibrary library = CreateRuntimeLibrary(fire, ice, thunder, edge);
        library.SetTokenWeight(fire, 6f);
        library.SetTokenWeight(ice, 3f);
        library.SetTokenWeight(thunder, 2f);
        library.SetTokenWeight(edge, 1f);

        List<PlaceableTokenData> first = library.SampleChoices(new VocalithRandom(24680), desiredCount: 3);
        List<PlaceableTokenData> second = library.SampleChoices(new VocalithRandom(24680), desiredCount: 3);

        Assert.That(first.Count, Is.EqualTo(second.Count));
        for (int i = 0; i < first.Count; i++)
        {
            Assert.That(first[i], Is.SameAs(second[i]));
        }
    }

    [Test]
    public void SpellBookRewardLibrary_SampleChoices_ExcludesZeroWeightSpellBooks()
    {
        SpellBookData wideBook = CreateSpellBook("wide", "Wide Spellbook", 7, 0.4f, "7 slots");
        SpellBookData quickBook = CreateSpellBook("quick", "Quick Spellbook", 4, 0.12f, "4 slots");
        SpellBookRewardLibrary library = CreateRuntimeSpellBookLibrary(wideBook, quickBook);
        library.SetSpellBookWeight(wideBook, 1f);
        library.SetSpellBookWeight(quickBook, 0f);

        for (int i = 0; i < 8; i++)
        {
            List<SpellBookData> sample = library.SampleChoices(new VocalithRandom(2000 + i), desiredCount: 1);
            Assert.That(sample.Count, Is.EqualTo(1));
            Assert.That(sample[0], Is.SameAs(wideBook));
        }
    }

    [Test]
    public void SelectionView_RebindDoesNotDuplicateClickListeners()
    {
        BulletTokenSelectionView view = CreateSelectionViewInstance();
        CoreTokenData firstToken = CreateToken<CoreTokenData>("fire", "Fire", "Hot core");
        BehaviorTokenData secondToken = CreateToken<BehaviorTokenData>("spray", "Spray", "Spread behavior");
        TokenSelectUIScreen firstScreen = CreateScreenRoot();

        view.Bind(firstScreen, firstToken);
        Assert.That(GetRuntimeListenerCount(view.SelectButton), Is.EqualTo(1));
        Assert.That(view.BoundToken, Is.SameAs(firstToken));
        Assert.That(view.TokenText.text, Is.EqualTo("Fire"));
        Assert.That(view.CatalogText.text, Is.EqualTo("核心"));

        TokenSelectUIScreen secondScreen = CreateScreenRoot();
        view.Bind(secondScreen, secondToken);
        Assert.That(GetRuntimeListenerCount(view.SelectButton), Is.EqualTo(1));
        Assert.That(view.BoundToken, Is.SameAs(secondToken));
        Assert.That(view.TokenText.text, Is.EqualTo("Spray"));
        Assert.That(view.CatalogText.text, Is.EqualTo("行为"));
        AssertColorApproximately(view.RootImage.color, new Color(0.48f, 0.74f, 1f, 1f));
    }

    [Test]
    public void SelectionView_BindsLinkedToken_UsesFirstCompileTokenTypeForCatalog()
    {
        BulletTokenSelectionView view = CreateSelectionViewInstance();
        TokenSelectUIScreen screen = CreateScreenRoot();
        LinkedTokenData linkedToken = CreateLinkedToken(
            "linked_fire_boom",
            "Linked fire and boom",
            CreateToken<CoreTokenData>("fire", "Fire", "Fire core"),
            CreateToken<ResultTokenData>("boom", "Boom", "Boom result"));

        view.Bind(screen, linkedToken);

        Assert.That(view.TokenText.text, Is.EqualTo("FireBoom"));
        Assert.That(view.CatalogText.text, Is.EqualTo("核心"));
        AssertColorApproximately(view.RootImage.color, new Color(1f, 0.73f, 0.36f, 1f));
    }

    [Test]
    public void SelectionView_BindsSpellProgramTokenTypes_ShowsCatalogLabels()
    {
        BulletTokenSelectionView view = CreateSelectionViewInstance();
        TokenSelectUIScreen screen = CreateScreenRoot();
        ModifierTokenData modifier = CreateToken<ModifierTokenData>("modifier_haste", "Haste", "Modify the next token");
        MulticastTokenData multicast = CreateToken<MulticastTokenData>("dual_cast", "Dual", "Cast two projectiles");
        TriggerTokenData trigger = CreateToken<TriggerTokenData>("on_hit", "Hit", "Run payload on hit");

        view.Bind(screen, modifier);
        Assert.That(view.CatalogText.text, Is.EqualTo("修饰"));
        AssertColorApproximately(view.RootImage.color, new Color(0.74f, 0.62f, 1f, 1f));

        view.Bind(screen, multicast);
        Assert.That(view.CatalogText.text, Is.EqualTo("多重"));
        AssertColorApproximately(view.RootImage.color, new Color(1f, 0.84f, 0.42f, 1f));

        view.Bind(screen, trigger);
        Assert.That(view.CatalogText.text, Is.EqualTo("触发"));
        AssertColorApproximately(view.RootImage.color, new Color(0.42f, 0.92f, 0.95f, 1f));
    }

    [Test]
    public void SelectionView_BindsSpellBookReward_ShowsSpellBookCatalog()
    {
        BulletTokenSelectionView view = CreateSelectionViewInstance();
        TokenSelectUIScreen screen = CreateScreenRoot();
        SpellBookData spellBook = CreateSpellBook(
            "quick",
            "Quick Spellbook",
            4,
            0.12f,
            "4 槽，快速冷却，适合短链构筑。");

        view.Bind(screen, RunRewardOption.FromSpellBook(spellBook));

        Assert.That(view.BoundToken, Is.Null);
        Assert.That(view.BoundReward.Kind, Is.EqualTo(RunRewardOptionKind.SpellBook));
        Assert.That(view.BoundReward.SpellBook, Is.SameAs(spellBook));
        Assert.That(view.TokenText.text, Is.EqualTo("Quick Spellbook"));
        Assert.That(view.DescriptionText.text, Is.EqualTo("4 槽，快速冷却，适合短链构筑。"));
        Assert.That(view.CatalogText.text, Is.EqualTo("法术书"));
        AssertColorApproximately(view.RootImage.color, new Color(0.48f, 0.86f, 0.68f, 1f));
    }

    [Test]
    public void TokenSelectUIScreen_BuildsCardsFromSpellProgramTokenLibrary()
    {
        PlaceableTokenData[] rewards =
        {
            CreateToken<CoreTokenData>("fire", "Fire", "Hot core"),
            CreateToken<ValueTokenData>("value_2", "2", "Numeric value"),
            CreateToken<ModifierTokenData>("modifier_haste", "Haste", "Modify the next token"),
            CreateToken<MulticastTokenData>("dual_cast", "Dual", "Cast two projectiles"),
            CreateToken<TriggerTokenData>("on_hit", "Hit", "Run payload on hit"),
        };
        BulletTokenLibrary library = CreateRuntimeLibrary(rewards);
        TokenSelectUIScreen screen = CreateScreenRoot(library);
        screen.SetChoiceCountOverride(rewards.Length);
        screen.SetSelectionRandom(new VocalithRandom(12));

        InvokeNonPublic(screen, "OnInit");

        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(rewards.Length));
        AssertCardTypesInclude(
            screen.RuntimeCards,
            TokenType.Core,
            TokenType.Value,
            TokenType.Modifier,
            TokenType.Multicast,
            TokenType.Trigger);
    }

    [Test]
    public void TokenSelectUIScreen_BuildsCardsFromMixedRewardLibraries()
    {
        CoreTokenData fire = CreateToken<CoreTokenData>("fire", "Fire", "Hot core");
        BulletTokenLibrary tokenLibrary = CreateRuntimeLibrary(fire);
        SpellBookData wideBook = CreateSpellBook("wide", "Wide Spellbook", 7, 0.4f, "7 slots");
        SpellBookData quickBook = CreateSpellBook("quick", "Quick Spellbook", 4, 0.12f, "4 slots");
        SpellBookRewardLibrary spellBookLibrary = CreateRuntimeSpellBookLibrary(wideBook, quickBook);
        TokenSelectUIScreen screen = CreateScreenRoot(tokenLibrary, spellBookLibrary: spellBookLibrary);
        screen.SetChoiceCountOverride(3);
        screen.SetSelectionRandom(new VocalithRandom(12));

        InvokeNonPublic(screen, "OnInit");

        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(3));
        Assert.That(ContainsRewardKind(screen.RuntimeCards, RunRewardOptionKind.Token), Is.True);
        Assert.That(ContainsRewardKind(screen.RuntimeCards, RunRewardOptionKind.SpellBook), Is.True);
        Assert.That(ContainsSpellBook(screen.RuntimeCards, wideBook), Is.True);
        Assert.That(ContainsSpellBook(screen.RuntimeCards, quickBook), Is.True);
    }

    [Test]
    public void TokenSelectUIScreen_RewardCallbackReceivesSpellBookReward()
    {
        SpellBookData quickBook = CreateSpellBook("quick", "Quick Spellbook", 4, 0.12f, "4 slots");
        SpellBookRewardLibrary spellBookLibrary = CreateRuntimeSpellBookLibrary(quickBook);
        TokenSelectUIScreen screen = CreateScreenRoot(spellBookLibrary: spellBookLibrary);
        RunRewardOption selectedReward = RunRewardOption.None;
        screen.SetRewardCallbacks(reward => selectedReward = reward);
        screen.SetChoiceCountOverride(1);
        screen.SetSelectionRandom(new VocalithRandom(3));

        InvokeNonPublic(screen, "OnInit");
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(1));

        screen.RuntimeCards[0].SelectButton.onClick.Invoke();

        Assert.That(selectedReward.Kind, Is.EqualTo(RunRewardOptionKind.SpellBook));
        Assert.That(selectedReward.SpellBook, Is.SameAs(quickBook));
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(0));
    }

    [Test]
    public void TokenSelectUIScreen_BuildsThreeCardsByDefault()
    {
        BulletTokenLibrary library = CreateRuntimeLibrary(
            CreateToken<CoreTokenData>("fire", "Fire", "Hot core"),
            CreateToken<CoreTokenData>("ice", "Ice", "Cold core"),
            CreateToken<CoreTokenData>("thunder", "Thunder", "Storm core"),
            CreateToken<CoreTokenData>("edge", "Edge", "Sharp core"),
            CreateToken<ResultTokenData>("boom", "Boom", "Explosion result"));

        TokenSelectUIScreen screen = CreateScreenRoot(library);
        screen.SetSelectionRandom(new VocalithRandom(1));

        InvokeNonPublic(screen, "OnInit");

        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(3));
        Assert.That(screen.MainContent.childCount, Is.EqualTo(3));
        AssertAllCardsHaveBoundTokens(screen.RuntimeCards);
    }

    [Test]
    public void TokenSelectUIScreen_ClearsRuntimeChildrenOnHide()
    {
        BulletTokenLibrary library = CreateRuntimeLibrary(
            CreateToken<CoreTokenData>("fire", "Fire", "Hot core"),
            CreateToken<CoreTokenData>("ice", "Ice", "Cold core"),
            CreateToken<CoreTokenData>("thunder", "Thunder", "Storm core"));

        TokenSelectUIScreen screen = CreateScreenRoot(library);
        screen.SetChoiceCountOverride(3);
        screen.SetSelectionRandom(new VocalithRandom(2));

        InvokeNonPublic(screen, "OnInit");
        Assert.That(screen.MainContent.childCount, Is.EqualTo(3));

        InvokeNonPublic(screen, "OnAfterHide");

        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(0));
        Assert.That(screen.MainContent.childCount, Is.EqualTo(0));
    }

    [Test]
    public void TokenSelectUIScreen_RebuildsWithoutLeakingListeners()
    {
        BulletTokenLibrary library = CreateRuntimeLibrary(
            CreateToken<CoreTokenData>("fire", "Fire", "Hot core"),
            CreateToken<CoreTokenData>("ice", "Ice", "Cold core"),
            CreateToken<CoreTokenData>("thunder", "Thunder", "Storm core"),
            CreateToken<CoreTokenData>("edge", "Edge", "Sharp core"));

        TokenSelectUIScreen screen = CreateScreenRoot(library);
        int selectionCount = 0;
        PlaceableTokenData selected = null;
        screen.SetCallbacks(value =>
        {
            selected = value;
            selectionCount++;
        });
        screen.SetChoiceCountOverride(1);
        screen.SetSelectionRandom(new VocalithRandom(3));

        InvokeNonPublic(screen, "OnInit");
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(1));

        BulletTokenSelectionView firstCard = screen.RuntimeCards[0];
        PlaceableTokenData firstToken = firstCard.BoundToken;
        firstCard.SelectButton.onClick.Invoke();
        Assert.That(selectionCount, Is.EqualTo(1));
        Assert.That(selected, Is.SameAs(firstToken));
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(0));

        screen.SetCallbacks(value =>
        {
            selected = value;
            selectionCount++;
        });
        screen.SetSelectionRandom(new VocalithRandom(4));
        screen.SetChoiceCountOverride(1);

        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(1));
        BulletTokenSelectionView secondCard = screen.RuntimeCards[0];
        secondCard.SelectButton.onClick.Invoke();

        Assert.That(selectionCount, Is.EqualTo(2));
        Assert.That(selected, Is.SameAs(secondCard.BoundToken));
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(0));
    }

    [Test]
    public void TokenSelectUIScreen_RequestCloseWithoutSelection_InvokesCancelCallback()
    {
        BulletTokenLibrary library = CreateRuntimeLibrary(
            CreateToken<CoreTokenData>("fire", "Fire", "Hot core"),
            CreateToken<CoreTokenData>("ice", "Ice", "Cold core"));

        TokenSelectUIScreen screen = CreateScreenRoot(library);
        int selectionCount = 0;
        int cancelCount = 0;
        screen.SetCallbacks(_ => selectionCount++, () => cancelCount++);
        screen.SetChoiceCountOverride(1);
        screen.SetSelectionRandom(new VocalithRandom(5));

        InvokeNonPublic(screen, "OnInit");
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(1));

        screen.RequestClose();

        Assert.That(selectionCount, Is.EqualTo(0));
        Assert.That(cancelCount, Is.EqualTo(1));
        Assert.That(screen.RuntimeCards.Count, Is.EqualTo(0));
    }

    [Test]
    public void TokenSelectUIScreen_LogsWarningWhenLibraryMissing()
    {
        BulletTokenSelectionView prefab = CreateSelectionViewInstance();
        TokenSelectUIScreen screen = CreateScreenRoot(null, prefab);

        LogAssert.Expect(LogType.Warning, "[TokenSelectUIScreen] BulletTokenLibrary is missing.");
        InvokeNonPublic(screen, "OnInit");
    }

    [Test]
    public void TokenSelectUIScreen_LogsWarningWhenPrefabMissing()
    {
        BulletTokenLibrary library = CreateRuntimeLibrary(
            CreateToken<CoreTokenData>("fire", "Fire", "Hot core"));
        TokenSelectUIScreen screen = CreateScreenRoot(library);

        LogAssert.Expect(LogType.Warning, "[TokenSelectUIScreen] Selection prefab is missing.");
        screen.SetSelectionPrefab(null);
        InvokeNonPublic(screen, "OnInit");
    }

    [Test]
    public void TokenSelectPanelPrefab_WiresLibraryAndSelectionPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab");

        try
        {
            TokenSelectUIScreen screen = prefabRoot.GetComponent<TokenSelectUIScreen>();
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.BulletTokenLibrary, Is.Not.Null);
            Assert.That(screen.SelectionPrefab, Is.Not.Null);
            Assert.That(screen.SelectionPrefab.GetComponent<BulletTokenSelectionView>(), Is.Not.Null);
            Assert.That(screen.MainContent.GetComponent<TokenSelectPanelLayoutFitter>(), Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void TokenSelectPanelPrefab_UsesLocalFitterWithoutGlobalResponsiveFitter()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab");

        try
        {
            TokenSelectUIScreen screen = prefabRoot.GetComponent<TokenSelectUIScreen>();
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.MainContent, Is.Not.Null);

            Assert.That(prefabRoot.GetComponentsInChildren<ResponsiveLayoutGroupFitter>(true), Is.Empty);
            Assert.That(screen.MainContent.GetComponent<ContentSizeFitter>(), Is.Null);
            Assert.That(screen.MainContent.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            Assert.That(screen.MainContent.GetComponent<TokenSelectPanelLayoutFitter>(), Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void TokenSelectPanelLayoutFitter_ConstrainsCardsByAvailableWidthAndHeight()
    {
        RectTransform parent = CreateUiObject("Parent").GetComponent<RectTransform>();
        parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1000f);
        parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 600f);

        RectTransform content = CreateUiObject("Main Content", parent).GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0.1f, 0.2f);
        content.anchorMax = new Vector2(0.9f, 0.8f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layoutGroup = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 50f;
        layoutGroup.padding = new RectOffset(40, 40, 20, 20);
        content.gameObject.AddComponent<TokenSelectPanelLayoutFitter>();

        for (int i = 0; i < 3; i++)
        {
            RectTransform card = CreateUiObject($"Card {i + 1}", content).GetComponent<RectTransform>();
            LayoutElement layoutElement = card.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 460f;
            AspectRatioFitter aspectRatioFitter = card.gameObject.AddComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            aspectRatioFitter.aspectRatio = 0.92f;
        }

        TokenSelectPanelLayoutFitter fitter = content.GetComponent<TokenSelectPanelLayoutFitter>();
        fitter.FitNow();

        float maxAnchoredHeight = parent.rect.height * Mathf.Abs(content.anchorMax.y - content.anchorMin.y);
        float requiredWidth = ResolveHorizontalRequiredWidth(layoutGroup);
        float cardWidth = LayoutUtility.GetPreferredWidth(content.GetChild(0) as RectTransform);
        float cardHeight = cardWidth / 0.92f;

        Assert.That(requiredWidth, Is.LessThanOrEqualTo(content.rect.width + 0.5f));
        Assert.That(content.rect.height, Is.LessThanOrEqualTo(maxAnchoredHeight + 0.5f));
        Assert.That(cardHeight + layoutGroup.padding.vertical, Is.LessThanOrEqualTo(content.rect.height + 0.5f));
        Assert.That(cardWidth, Is.LessThan(460f));
    }

    [Test]
    public void BulletTokenSelectionPrefab_SeparatesCatalogAndDescriptionAtSmallScale()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SelectionPrefabPath);

        try
        {
            AspectRatioFitter aspectRatioFitter = prefabRoot.GetComponent<AspectRatioFitter>();
            Assert.That(aspectRatioFitter, Is.Not.Null);
            Assert.That(aspectRatioFitter.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.WidthControlsHeight));
            Assert.That(aspectRatioFitter.aspectRatio, Is.EqualTo(0.92f).Within(0.0001f));

            RectTransform catalog = FindDirectChild<RectTransform>(prefabRoot.transform, "Catalog");
            RectTransform token = FindDirectChild<RectTransform>(prefabRoot.transform, "Token");
            RectTransform description = FindDirectChild<RectTransform>(prefabRoot.transform, "Description");

            Assert.That(description.anchorMax.y, Is.LessThanOrEqualTo(catalog.anchorMin.y - 0.04f));
            Assert.That(catalog.anchorMax.y, Is.LessThanOrEqualTo(token.anchorMin.y));

            TMPro.TextMeshProUGUI catalogText = catalog.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            Assert.That(catalogText, Is.Not.Null);
            Assert.That(catalogText.enableAutoSizing, Is.True);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private TokenSelectUIScreen CreateScreenRoot(
        BulletTokenLibrary library = null,
        BulletTokenSelectionView prefab = null,
        SpellBookRewardLibrary spellBookLibrary = null)
    {
        GameObject root = CreateUiObject("Token Select Panel");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();

        TokenSelectUIScreen screen = root.AddComponent<TokenSelectUIScreen>();
        RectTransform mainContent = CreateUiObject("Main Content", root.transform).GetComponent<RectTransform>();
        mainContent.gameObject.AddComponent<Image>();
        mainContent.gameObject.AddComponent<HorizontalLayoutGroup>();

        if (library != null)
        {
            screen.SetBulletTokenLibrary(library);
        }

        if (spellBookLibrary != null)
        {
            screen.SetSpellBookRewardLibrary(spellBookLibrary);
        }

        prefab ??= GetSelectionPrefabTemplate();
        if (prefab != null)
        {
            screen.SetSelectionPrefab(prefab);
        }

        return screen;
    }

    private BulletTokenSelectionView CreateSelectionViewInstance()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SelectionPrefabPath);
        Assert.That(prefabAsset, Is.Not.Null, "BulletToken Selection Prefab should exist.");

        GameObject clone = UnityEngine.Object.Instantiate(prefabAsset);
        createdObjects.Add(clone);

        BulletTokenSelectionView view = clone.GetComponent<BulletTokenSelectionView>();
        Assert.That(view, Is.Not.Null, "BulletToken Selection Prefab should carry BulletTokenSelectionView.");
        return view;
    }

    private BulletTokenSelectionView GetSelectionPrefabTemplate()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SelectionPrefabPath);
        Assert.That(prefabAsset, Is.Not.Null, "BulletToken Selection Prefab should exist.");

        BulletTokenSelectionView prefab = prefabAsset.GetComponent<BulletTokenSelectionView>();
        Assert.That(prefab, Is.Not.Null, "BulletToken Selection Prefab should carry BulletTokenSelectionView.");
        return prefab;
    }

    private BulletTokenLibrary CreateRuntimeLibrary(params PlaceableTokenData[] tokens)
    {
        BulletTokenLibrary library = ScriptableObject.CreateInstance<BulletTokenLibrary>();
        createdObjects.Add(library);
        library.SetTokens(tokens);
        return library;
    }

    private SpellBookRewardLibrary CreateRuntimeSpellBookLibrary(params SpellBookData[] spellBooks)
    {
        SpellBookRewardLibrary library = ScriptableObject.CreateInstance<SpellBookRewardLibrary>();
        createdObjects.Add(library);
        library.SetSpellBooks(spellBooks);
        return library;
    }

    private SpellBookData CreateSpellBook(
        string spellBookId,
        string displayName,
        int slotCount,
        float castCooldownSeconds,
        string selectionDescription)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        spellBook.SpellBookId = spellBookId;
        spellBook.DisplayName = displayName;
        spellBook.SlotCount = slotCount;
        spellBook.CastCooldownSeconds = castCooldownSeconds;
        spellBook.CastsPerActivation = 1;
        spellBook.SelectionDescription = selectionDescription;
        spellBook.name = spellBookId;
        createdObjects.Add(spellBook);
        return spellBook;
    }

    private T CreateToken<T>(string tokenId, string displayText, string description) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.Description = description;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private LinkedTokenData CreateLinkedToken(string itemId, string description, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        token.ItemId = itemId;
        token.Description = description;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        createdObjects.Add(token);
        return token;
    }

    private static void AssertCardTypesInclude(IReadOnlyList<BulletTokenSelectionView> cards, params TokenType[] expectedTypes)
    {
        List<TokenType> actualTypes = new();
        for (int i = 0; i < cards.Count; i++)
        {
            Assert.That(cards[i].BoundToken, Is.Not.Null);
            actualTypes.Add(ResolvePrimaryTokenType(cards[i].BoundToken));
        }

        for (int i = 0; i < expectedTypes.Length; i++)
        {
            Assert.That(actualTypes.Contains(expectedTypes[i]), Is.True, $"Expected Token Select sample to include {expectedTypes[i]}.");
        }
    }

    private static TokenType ResolvePrimaryTokenType(PlaceableTokenData token)
    {
        if (token is BaseTokenData baseToken)
        {
            return baseToken.TokenType;
        }

        List<BaseTokenData> compileTokens = new();
        token?.AppendCompileTokens(compileTokens);
        for (int i = 0; i < compileTokens.Count; i++)
        {
            if (compileTokens[i] != null && compileTokens[i].TokenType != TokenType.None)
            {
                return compileTokens[i].TokenType;
            }
        }

        return TokenType.None;
    }

    private static int GetRuntimeListenerCount(Button button)
    {
        Assert.That(button, Is.Not.Null);

        FieldInfo callsField = null;
        FieldInfo[] fields = typeof(UnityEngine.Events.UnityEventBase).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType.Name == "InvokableCallList" || fields[i].FieldType.FullName == "UnityEngine.Events.InvokableCallList")
            {
                callsField = fields[i];
                break;
            }
        }

        Assert.That(callsField, Is.Not.Null, "UnityEventBase should expose an InvokableCallList field.");
        object invokableList = callsField.GetValue(button.onClick);
        Assert.That(invokableList, Is.Not.Null, "Button.onClick should expose an InvokableCallList.");

        PropertyInfo countProperty = invokableList.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(countProperty, Is.Not.Null, "InvokableCallList should expose Count.");
        return (int)countProperty.GetValue(invokableList);
    }

    private static void AssertAllCardsHaveBoundTokens(IReadOnlyList<BulletTokenSelectionView> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Assert.That(cards[i], Is.Not.Null);
            Assert.That(cards[i].BoundToken, Is.Not.Null);
            Assert.That(cards[i].TokenText.text, Is.Not.Empty);
        }
    }

    private static bool ContainsRewardKind(IReadOnlyList<BulletTokenSelectionView> cards, RunRewardOptionKind kind)
    {
        if (cards == null)
        {
            return false;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].BoundReward.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpellBook(IReadOnlyList<BulletTokenSelectionView> cards, SpellBookData spellBook)
    {
        if (cards == null || spellBook == null)
        {
            return false;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].BoundReward.SpellBook == spellBook)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertColorApproximately(Color actual, Color expected, float tolerance = 0.0001f)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
    }

    private static void AssertUniqueInstanceIds(IReadOnlyList<PlaceableTokenData> tokens)
    {
        HashSet<int> seenInstanceIds = new();
        for (int i = 0; i < tokens.Count; i++)
        {
            PlaceableTokenData token = tokens[i];
            Assert.That(token, Is.Not.Null);
            Assert.That(seenInstanceIds.Add(token.GetInstanceID()), Is.True, $"Token {token.name} appears more than once.");
        }
    }

    private static float ResolveHorizontalRequiredWidth(HorizontalLayoutGroup layout)
    {
        float requiredWidth = layout.padding.horizontal;
        for (int i = 0; i < layout.transform.childCount; i++)
        {
            RectTransform child = layout.transform.GetChild(i) as RectTransform;
            requiredWidth += LayoutUtility.GetPreferredWidth(child);
        }

        requiredWidth += layout.spacing * Mathf.Max(0, layout.transform.childCount - 1);
        return requiredWidth;
    }

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
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

    private static T FindDirectChild<T>(Transform parent, string childName) where T : Component
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                T component = child.GetComponent<T>();
                Assert.That(component, Is.Not.Null, $"{childName} should have {typeof(T).Name}.");
                return component;
            }
        }

        Assert.Fail($"Expected direct child '{childName}' under '{parent.name}'.");
        return null;
    }
}
