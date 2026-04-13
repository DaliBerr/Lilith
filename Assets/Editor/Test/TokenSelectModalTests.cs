using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VocalithRandom = Vocalith.Random;

public sealed class TokenSelectModalTests
{
    private const string SelectionPrefabPath = "Assets/Prefabs/UI/BulletToken Selection Prefab.prefab";
    private const string LibraryAssetPath = "Assets/Data/BulletTokens/BulletTokenLibrary.asset";

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
    public void SelectionView_BindsTextsAndClickInvokesOwnerCallback()
    {
        BulletTokenSelectionView view = CreateSelectionViewInstance();
        CoreTokenData token = CreateToken<CoreTokenData>("fire", "Fire", "Hot core");
        TokenSelectUIScreen screen = CreateScreenRoot();

        PlaceableTokenData selected = null;
        int selectionCount = 0;
        screen.SetCallbacks(value =>
        {
            selected = value;
            selectionCount++;
        });

        view.Bind(screen, token);

        Assert.That(view.TokenText.text, Is.EqualTo("Fire"));
        Assert.That(view.DescriptionText.text, Is.EqualTo("Hot core"));
        Assert.That(view.SelectButton.interactable, Is.True);
        Assert.That(GetSelectButtonText(view), Is.Not.Null);
        Assert.That(GetSelectButtonText(view).text, Is.Not.Empty);

        view.SelectButton.onClick.Invoke();

        Assert.That(selectionCount, Is.EqualTo(1));
        Assert.That(selected, Is.SameAs(token));
    }

    [Test]
    public void SelectionView_RebindDoesNotDuplicateClickListeners()
    {
        BulletTokenSelectionView view = CreateSelectionViewInstance();
        CoreTokenData firstToken = CreateToken<CoreTokenData>("fire", "Fire", "Hot core");
        CoreTokenData secondToken = CreateToken<CoreTokenData>("ice", "Ice", "Cold core");
        TokenSelectUIScreen firstScreen = CreateScreenRoot();

        view.Bind(firstScreen, firstToken);
        Assert.That(GetRuntimeListenerCount(view.SelectButton), Is.EqualTo(1));
        Assert.That(view.BoundToken, Is.SameAs(firstToken));
        Assert.That(view.TokenText.text, Is.EqualTo("Fire"));

        TokenSelectUIScreen secondScreen = CreateScreenRoot();
        view.Bind(secondScreen, secondToken);
        Assert.That(GetRuntimeListenerCount(view.SelectButton), Is.EqualTo(1));
        Assert.That(view.BoundToken, Is.SameAs(secondToken));
        Assert.That(view.TokenText.text, Is.EqualTo("Ice"));
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
    public void BulletTokenLibraryAsset_IsSeeded()
    {
        BulletTokenLibrary library = AssetDatabase.LoadAssetAtPath<BulletTokenLibrary>(LibraryAssetPath);
        Assert.That(library, Is.Not.Null);

        IReadOnlyList<PlaceableTokenData> tokens = library.GetTokens();
        Assert.That(tokens.Count, Is.GreaterThan(0));
        AssertUniqueInstanceIds(tokens);
    }

    [Test]
    public void TokenSelectPanelPrefab_WiresLibraryAndSelectionPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Token Select Panel.prefab");

        try
        {
            TokenSelectUIScreen screen = prefabRoot.GetComponent<TokenSelectUIScreen>();
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.BulletTokenLibrary, Is.Not.Null);
            Assert.That(screen.SelectionPrefab, Is.Not.Null);
            Assert.That(screen.SelectionPrefab.GetComponent<BulletTokenSelectionView>(), Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private TokenSelectUIScreen CreateScreenRoot(BulletTokenLibrary library = null, BulletTokenSelectionView prefab = null)
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

    private static TMP_Text GetSelectButtonText(BulletTokenSelectionView view)
    {
        return view.SelectButton != null ? view.SelectButton.GetComponentInChildren<TMP_Text>(true) : null;
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
}
