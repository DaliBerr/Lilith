using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Kernel.Upgrade;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class UpdateUIScreenTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingPermanentUpgradeService();
        DestroyExistingRuntimeSaveService();
        DestroyExistingWallet();

        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();
        DeleteSaveDirectory();
    }

    [Test]
    public void BuildTree_UsesActiveScrollContentAndCreatesNodesAndEdges()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PermanentUpgradeService upgradeService = CreateUpgradeService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(10, out _), Is.True);
        Assert.That(upgradeService.TryUseCatalog(CreateTreeCatalog(), out string errorMessage), Is.True, errorMessage);
        UpdateUIScreen screen = CreateUpdateScreen(out RectTransform contentRoot);
        GameObject nodePrefab = CreateNodePrefab();

        InvokeNonPublic(screen, "TryAutoBindReferences");
        InvokeNonPublic(screen, "BuildTree", upgradeService, nodePrefab);

        RectTransform boundContent = GetNonPublicField<RectTransform>(screen, "contentRoot");
        Assert.That(boundContent, Is.SameAs(contentRoot));
        Assert.That(contentRoot.sizeDelta, Is.EqualTo(new Vector2(500f, 300f)));

        RectTransform edgesLayer = contentRoot.Find("Edges Layer") as RectTransform;
        RectTransform nodesLayer = contentRoot.Find("Nodes Layer") as RectTransform;
        Assert.That(edgesLayer, Is.Not.Null);
        Assert.That(nodesLayer, Is.Not.Null);
        Assert.That(nodesLayer.childCount, Is.EqualTo(2));
        Assert.That(edgesLayer.childCount, Is.EqualTo(1));

        RectTransform rootNode = nodesLayer.Find("Upgrade Node 01 - damage_root") as RectTransform;
        RectTransform childNode = nodesLayer.Find("Upgrade Node 02 - damage_child") as RectTransform;
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(childNode, Is.Not.Null);
        Assert.That(rootNode.anchoredPosition, Is.EqualTo(new Vector2(50f, -60f)));
        Assert.That(rootNode.sizeDelta, Is.EqualTo(new Vector2(120f, 80f)));
        Assert.That(rootNode.GetComponent<UpgradeNodeView>().VisualState, Is.EqualTo(UpgradeNodeVisualState.Available));
        Assert.That(childNode.GetComponent<UpgradeNodeView>().VisualState, Is.EqualTo(UpgradeNodeVisualState.Locked));
        Assert.That(rootNode.Find("Tittle").GetComponent<TMP_Text>().text, Is.EqualTo("Root Damage"));
        Assert.That(rootNode.Find("Cost").GetComponent<TMP_Text>().text, Does.Contain("等级 0/1"));
        Assert.That(childNode.GetComponent<Button>().interactable, Is.False);

        RectTransform edgeRect = edgesLayer.GetChild(0) as RectTransform;
        Image edgeImage = edgeRect.GetComponent<Image>();
        Assert.That(edgeRect.anchoredPosition.x, Is.EqualTo(205f).Within(0.001f));
        Assert.That(edgeRect.anchoredPosition.y, Is.EqualTo(-105f).Within(0.001f));
        Assert.That(edgeRect.sizeDelta.x, Is.EqualTo(Mathf.Sqrt(190f * 190f + 10f * 10f)).Within(0.001f));
        Assert.That(edgeRect.sizeDelta.y, Is.EqualTo(9f).Within(0.001f));
        Assert.That(edgeImage.color.r, Is.EqualTo(1f).Within(0.001f));
        Assert.That(edgeImage.color.g, Is.EqualTo(0f).Within(0.001f));
        Assert.That(edgeImage.color.b, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void BuildTree_NodeClicksRespectPrerequisitesAndPurchaseAvailableNodes()
    {
        PrepareCleanSaveEnvironment();
        CreateWallet(initialCount: 0);
        RuntimeSaveService saveService = CreateSaveService();
        PermanentUpgradeService upgradeService = CreateUpgradeService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(PlayerRemnantWallet.TrySetCurrentRemnants(30, out _), Is.True);
        Assert.That(upgradeService.TryUseCatalog(CreateTreeCatalog(), out string errorMessage), Is.True, errorMessage);
        UpdateUIScreen screen = CreateUpdateScreen(out RectTransform contentRoot);
        GameObject nodePrefab = CreateNodePrefab();

        InvokeNonPublic(screen, "TryAutoBindReferences");
        InvokeNonPublic(screen, "BuildTree", upgradeService, nodePrefab);
        RectTransform nodesLayer = contentRoot.Find("Nodes Layer") as RectTransform;
        Button lockedChildButton = nodesLayer.Find("Upgrade Node 02 - damage_child").GetComponent<Button>();
        Button availableRootButton = nodesLayer.Find("Upgrade Node 01 - damage_root").GetComponent<Button>();

        screen.gameObject.SetActive(false);
        lockedChildButton.onClick.Invoke();

        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(30));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_child")), Is.EqualTo(0));

        availableRootButton.onClick.Invoke();

        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(20));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_root")), Is.EqualTo(1));

        InvokeNonPublic(screen, "BuildTree", upgradeService, nodePrefab);
        nodesLayer = contentRoot.Find("Nodes Layer") as RectTransform;
        Button unlockedChildButton = nodesLayer.Find("Upgrade Node 02 - damage_child").GetComponent<Button>();
        Assert.That(unlockedChildButton.interactable, Is.True);

        unlockedChildButton.onClick.Invoke();

        Assert.That(PlayerRemnantWallet.GetCurrentRemnants(), Is.EqualTo(0));
        Assert.That(saveService.GetLifetimeStat(PermanentUpgradeService.BuildLifetimeStatKey("damage_child")), Is.EqualTo(1));
    }

    [Test]
    public void Prefab_UpgradeSection_HasResponsiveGridFitterOnRoot()
    {
        const string prefabPath = "Assets/Prefabs/UI/Upgrade/Upgrage Section Prefab.prefab";
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(prefab, Is.Not.Null, $"{prefabPath} should exist.");

        ResponsiveLayoutGroupFitter[] fitters = prefab.GetComponentsInChildren<ResponsiveLayoutGroupFitter>(true);
        GridLayoutGroup[] grids = prefab.GetComponentsInChildren<GridLayoutGroup>(true);

        Assert.That(fitters, Has.Length.EqualTo(1));
        Assert.That(fitters[0].gameObject, Is.SameAs(prefab));
        Assert.That(grids, Has.Length.EqualTo(1));
        Assert.That(grids[0].transform.parent, Is.SameAs(prefab.transform));
    }

    private PermanentUpgradeCatalogData CreateTreeCatalog()
    {
        return new PermanentUpgradeCatalogData
        {
            CanvasSize = new PermanentUpgradeVector2Data { X = 500f, Y = 300f },
            Edges = new List<PermanentUpgradeEdgeData>
            {
                new()
                {
                    From = "damage_root",
                    To = "damage_child",
                    Color = "#FF0000",
                    Width = 9f,
                },
            },
            Sections = new List<PermanentUpgradeSectionData>
            {
                new()
                {
                    Id = "combat",
                    Title = "Combat",
                    Entries = new List<PermanentUpgradeEntryData>
                    {
                        new()
                        {
                            Id = "damage_root",
                            Title = "Root Damage",
                            CostRemnants = 10,
                            MaxLevel = 1,
                            Effects = new List<PermanentUpgradeEffectData>
                            {
                                CreateEffect(PermanentUpgradeStatId.OutgoingDamage, PermanentUpgradeStatOperation.AddMultiplier, 0.1f),
                            },
                            Requires = new List<string>(),
                            Position = new PermanentUpgradeVector2Data { X = 50f, Y = 60f },
                            Size = new PermanentUpgradeVector2Data { X = 120f, Y = 80f },
                            BackgroundColor = "#1F2937",
                            BorderColor = "#66E35F",
                            BorderWidth = 4f,
                        },
                        new()
                        {
                            Id = "damage_child",
                            Title = "Child Damage",
                            CostRemnants = 20,
                            MaxLevel = 1,
                            Effects = new List<PermanentUpgradeEffectData>
                            {
                                CreateEffect(PermanentUpgradeStatId.MoveSpeed, PermanentUpgradeStatOperation.AddFlat, 0.2f),
                            },
                            Requires = new List<string> { "damage_root" },
                            Position = new PermanentUpgradeVector2Data { X = 250f, Y = 60f },
                            Size = new PermanentUpgradeVector2Data { X = 100f, Y = 100f },
                            BackgroundColor = "#1F2937",
                            BorderColor = "#66E35F",
                            BorderWidth = 4f,
                        },
                    },
                },
            },
        };
    }

    private static PermanentUpgradeEffectData CreateEffect(
        PermanentUpgradeStatId statId,
        PermanentUpgradeStatOperation operation,
        float value)
    {
        return new PermanentUpgradeEffectData
        {
            StatId = statId,
            Operation = operation,
            Value = value,
        };
    }

    private UpdateUIScreen CreateUpdateScreen(out RectTransform contentRoot)
    {
        GameObject root = CreateUiObject("Upgrade UI Screen");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        UpdateUIScreen screen = root.AddComponent<UpdateUIScreen>();

        GameObject panel = CreateUiObject("Panel", root.transform);
        GameObject title = CreateUiObject("Titile", panel.transform);
        CreateText("Text (TMP)", title.transform, "永久升级");

        GameObject mainContent = CreateUiObject("Main Content", panel.transform);
        GameObject activeContentPanel = CreateUiObject("Content", mainContent.transform);
        GameObject scrollView = CreateUiObject("Scroll View", activeContentPanel.transform);
        scrollView.AddComponent<ScrollRect>();
        GameObject viewport = CreateUiObject("Viewport", scrollView.transform);
        contentRoot = CreateUiObject("Content", viewport.transform).GetComponent<RectTransform>();
        scrollView.GetComponent<ScrollRect>().content = contentRoot;

        return screen;
    }

    private GameObject CreateNodePrefab()
    {
        GameObject prefab = CreateUiObject("Upgrade Node Prefab");
        prefab.AddComponent<Image>();
        prefab.AddComponent<Button>();
        prefab.AddComponent<UpgradeNodeView>();
        CreateUiObject("Background", prefab.transform).AddComponent<Image>();
        CreateUiObject("Icon", prefab.transform).AddComponent<Image>();
        CreateText("Tittle", prefab.transform, string.Empty);
        CreateText("Cost", prefab.transform, string.Empty);
        return prefab;
    }

    private TMP_Text CreateText(string name, Transform parent, string text)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        return label;
    }

    private PlayerRemnantWallet CreateWallet(int initialCount)
    {
        DestroyExistingWallet();
        GameObject walletObject = CreateGameObject("Wallet");
        PlayerRemnantWallet wallet = walletObject.AddComponent<PlayerRemnantWallet>();
        wallet.ApplyLoadedRemnants(initialCount);
        return wallet;
    }

    private RuntimeSaveService CreateSaveService()
    {
        DestroyExistingRuntimeSaveService();
        GameObject saveObject = CreateGameObject("RuntimeSaveService");
        return saveObject.AddComponent<RuntimeSaveService>();
    }

    private PermanentUpgradeService CreateUpgradeService()
    {
        DestroyExistingPermanentUpgradeService();
        GameObject upgradeObject = CreateGameObject("PermanentUpgradeService");
        return upgradeObject.AddComponent<PermanentUpgradeService>();
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

    private static void InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, arguments);
    }

    private static T GetNonPublicField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        return (T)field.GetValue(target);
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

    private static void DestroyExistingWallet()
    {
        PlayerRemnantWallet existingWallet = Object.FindFirstObjectByType<PlayerRemnantWallet>();
        if (existingWallet != null)
        {
            Object.DestroyImmediate(existingWallet.gameObject);
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

    private static void DestroyExistingPermanentUpgradeService()
    {
        PermanentUpgradeService existingService = Object.FindFirstObjectByType<PermanentUpgradeService>();
        if (existingService != null)
        {
            Object.DestroyImmediate(existingService.gameObject);
        }
    }
}
