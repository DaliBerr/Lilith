using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class UILayoutScreenshotBatcherTests
{
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
    public void ApplyResponsiveLayoutsForCapture_ScreenWithoutLocalFitter_DoesNotShrinkGridLayout()
    {
        RectTransform captureScope = CreateRect("Capture Scope", null, new Vector2(800f, 600f));
        captureScope.gameObject.AddComponent<Canvas>();
        captureScope.gameObject.AddComponent<CanvasScaler>();
        captureScope.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform screenRoot = CreateRect("Screen Root", captureScope, new Vector2(400f, 300f));
        screenRoot.gameObject.AddComponent<CanvasGroup>();
        screenRoot.gameObject.AddComponent<TestScreen>();

        RectTransform grid = CreateRect("Grid", screenRoot, new Vector2(180f, 120f));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(100f, 60f);
        layout.spacing = new Vector2(20f, 10f);
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            CreateRect($"Cell {i + 1:D2}", grid, new Vector2(100f, 60f));
        }

        InvokeApplyResponsiveLayoutsForCapture(captureScope, screenRoot.gameObject, "Screen");

        Assert.That(layout.cellSize.x, Is.EqualTo(100f));
        Assert.That(layout.spacing.x, Is.EqualTo(20f));
        Assert.That(ResolveGridRequiredWidth(layout, 3), Is.GreaterThan(grid.rect.width));
        Assert.That(captureScope.GetComponent<ResponsiveLayoutGroupFitter>(), Is.Null);
        Assert.That(screenRoot.GetComponent<ResponsiveLayoutGroupFitter>(), Is.Null);
    }

    [Test]
    public void ApplyResponsiveLayoutsForCapture_ScreenWithLocalFitter_ShrinksGridLayout()
    {
        RectTransform captureScope = CreateRect("Capture Scope", null, new Vector2(800f, 600f));
        captureScope.gameObject.AddComponent<Canvas>();
        captureScope.gameObject.AddComponent<CanvasScaler>();
        captureScope.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform screenRoot = CreateRect("Screen Root", captureScope, new Vector2(400f, 300f));
        screenRoot.gameObject.AddComponent<CanvasGroup>();
        screenRoot.gameObject.AddComponent<TestScreen>();
        screenRoot.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();

        RectTransform grid = CreateRect("Grid", screenRoot, new Vector2(180f, 120f));
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(100f, 60f);
        layout.spacing = new Vector2(20f, 10f);
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        for (int i = 0; i < 6; i++)
        {
            CreateRect($"Cell {i + 1:D2}", grid, new Vector2(100f, 60f));
        }

        InvokeApplyResponsiveLayoutsForCapture(captureScope, screenRoot.gameObject, "Screen");

        Assert.That(layout.cellSize.x, Is.LessThan(100f));
        Assert.That(ResolveGridRequiredWidth(layout, 3), Is.LessThanOrEqualTo(grid.rect.width + 0.01f));
        Assert.That(ResolveGridRequiredHeight(layout, 3, 6), Is.LessThanOrEqualTo(grid.rect.height + 0.01f));
    }

    [Test]
    public void ApplyResponsiveLayoutsForCapture_ScreenWithoutLocalFitter_DoesNotExpandVerticalScrollGrid()
    {
        RectTransform captureScope = CreateRect("Capture Scope", null, new Vector2(800f, 600f));
        captureScope.gameObject.AddComponent<Canvas>();
        captureScope.gameObject.AddComponent<CanvasScaler>();
        captureScope.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform screenRoot = CreateRect("Screen Root", captureScope, new Vector2(600f, 400f));
        screenRoot.gameObject.AddComponent<CanvasGroup>();
        screenRoot.gameObject.AddComponent<TestScreen>();

        RectTransform scrollView = CreateRect("Scroll View", screenRoot, new Vector2(420f, 240f));
        ScrollRect scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        RectTransform viewport = CreateRect("Viewport", scrollView, new Vector2(420f, 240f));
        RectTransform content = CreateRect("Content", viewport, new Vector2(320f, 640f));
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        GridLayoutGroup layout = content.gameObject.AddComponent<GridLayoutGroup>();
        content.gameObject.AddComponent<ContentSizeFitter>();
        layout.cellSize = new Vector2(100f, 100f);
        layout.spacing = new Vector2(20f, 20f);
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;

        for (int i = 0; i < 10; i++)
        {
            CreateRect($"Cell {i + 1:D2}", content, new Vector2(100f, 100f));
        }

        InvokeApplyResponsiveLayoutsForCapture(captureScope, screenRoot.gameObject, "Screen");

        Assert.That(layout.cellSize.x, Is.EqualTo(100f));
        Assert.That(layout.spacing.x, Is.EqualTo(20f));
        Assert.That(ResolveGridRequiredWidth(layout, 2), Is.LessThan(content.rect.width));
        Assert.That(captureScope.GetComponent<ResponsiveLayoutGroupFitter>(), Is.Null);
        Assert.That(screenRoot.GetComponent<ResponsiveLayoutGroupFitter>(), Is.Null);
    }

    [Test]
    public void ApplyResponsiveLayoutsForCapture_ScreenWithoutLocalFitter_DoesNotScaleHorizontalLayout()
    {
        RectTransform captureScope = CreateRect("Capture Scope", null, new Vector2(800f, 600f));
        captureScope.gameObject.AddComponent<Canvas>();
        captureScope.gameObject.AddComponent<CanvasScaler>();
        captureScope.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform screenRoot = CreateRect("Screen Root", captureScope, new Vector2(400f, 300f));
        screenRoot.gameObject.AddComponent<CanvasGroup>();
        screenRoot.gameObject.AddComponent<TestScreen>();

        RectTransform panel = CreateRect("Panel", screenRoot, new Vector2(200f, 80f));
        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        for (int i = 0; i < 3; i++)
        {
            LayoutElement element = CreateRect($"Item {i + 1:D2}", panel, new Vector2(100f, 50f)).gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 100f;
            element.preferredHeight = 50f;
        }

        InvokeApplyResponsiveLayoutsForCapture(captureScope, screenRoot.gameObject, "Screen");

        LayoutElement firstElement = panel.GetChild(0).GetComponent<LayoutElement>();
        Assert.That(layout.spacing, Is.EqualTo(20f));
        Assert.That(layout.padding.left, Is.EqualTo(10));
        Assert.That(layout.padding.top, Is.EqualTo(10));
        Assert.That(firstElement.preferredWidth, Is.EqualTo(100f));
        Assert.That(firstElement.preferredHeight, Is.EqualTo(50f));
        Assert.That(ResolveHorizontalRequiredWidth(layout), Is.GreaterThan(panel.rect.width));
    }

    [Test]
    public void ResolveTargetSet_BackPack_ReturnsOnlyBackPackUI()
    {
        Array targets = InvokeResolveTargetSet("backpack");

        Assert.That(targets.Length, Is.EqualTo(1));
        object target = targets.GetValue(0);
        Assert.That(ReadPrefabTargetField<string>(target, "Label"), Is.EqualTo("BackPackUI"));
        Assert.That(ReadPrefabTargetField<string>(target, "AssetPath"), Is.EqualTo("Assets/Prefabs/UI/Backpack/BackPackUI.prefab"));
    }

    [Test]
    public void ResolveTargetSet_MainUI_ReturnsOnlyMainUI()
    {
        Array targets = InvokeResolveTargetSet("main-ui");

        Assert.That(targets.Length, Is.EqualTo(1));
        object target = targets.GetValue(0);
        Assert.That(ReadPrefabTargetField<string>(target, "Label"), Is.EqualTo("MainUI"));
        Assert.That(ReadPrefabTargetField<string>(target, "AssetPath"), Is.EqualTo("Assets/Prefabs/UI/MainHUD/MainUI.prefab"));
    }

    [Test]
    public void ResolveTargetSet_Narrative_ReturnsNarrativeAndDialogScreens()
    {
        Array targets = InvokeResolveTargetSet("narrative");

        Assert.That(targets.Length, Is.EqualTo(3));
        Assert.That(ReadPrefabTargetField<string>(targets.GetValue(0), "AssetPath"), Is.EqualTo("Assets/Prefabs/UI/Narrative/Dialog UI.prefab"));
        Assert.That(ReadPrefabTargetField<string>(targets.GetValue(1), "AssetPath"), Is.EqualTo("Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab"));
        Assert.That(ReadPrefabTargetField<string>(targets.GetValue(2), "AssetPath"), Is.EqualTo("Assets/Prefabs/UI/Narrative/Narrative Menu Panel.prefab"));
    }

    [Test]
    public void ResolveTargetSet_TokenSelect_ReturnsOnlyTokenSelectPanel()
    {
        Array targets = InvokeResolveTargetSet("token-select");

        Assert.That(targets.Length, Is.EqualTo(1));
        object target = targets.GetValue(0);
        Assert.That(ReadPrefabTargetField<string>(target, "Label"), Is.EqualTo("Token_Select_Panel"));
        Assert.That(ReadPrefabTargetField<string>(target, "AssetPath"), Is.EqualTo("Assets/Prefabs/UI/TokenSelect/Token Select Panel.prefab"));
    }

    [Test]
    public void UIScaleOneScenarios_ContainOnlyResolutionVariantsAtUIScaleOne()
    {
        Array scenarios = ReadScenarioArray("UIScaleOneScenarios");

        Assert.That(scenarios.Length, Is.EqualTo(4));
        Assert.That(ReadScenarioField<string>(scenarios.GetValue(0), "Id"), Is.EqualTo("01_1920x1080_ui1"));
        Assert.That(ReadScenarioField<string>(scenarios.GetValue(1), "Id"), Is.EqualTo("02_2880x1800_16x10"));
        Assert.That(ReadScenarioField<string>(scenarios.GetValue(2), "Id"), Is.EqualTo("03_2560x1080_ultra"));
        Assert.That(ReadScenarioField<string>(scenarios.GetValue(3), "Id"), Is.EqualTo("04_1280x1024_5x4"));

        for (int i = 0; i < scenarios.Length; i++)
        {
            string description = ReadScenarioField<string>(scenarios.GetValue(i), "Description");
            Assert.That(description, Does.Contain("ui 1.0"));
            Assert.That(description, Does.Not.Contain("ui 0.6"));
            Assert.That(description, Does.Not.Contain("ui 1.5"));
        }
    }

    [Test]
    public void TryHydrateRuntimeContent_DialogUIScreen_AppliesScreenshotAuditSample()
    {
        RectTransform root = CreateRect("Dialog UI", null, new Vector2(800f, 260f));
        root.gameObject.AddComponent<DialogUIScreen>();
        RectTransform mainContent = CreateRect("Main Content", root, new Vector2(720f, 160f));
        TMP_Text dialogText = CreateRect("Text (TMP)", mainContent, new Vector2(680f, 120f)).gameObject.AddComponent<TextMeshProUGUI>();
        RectTransform infoPanel = CreateRect("Info Panel", root, new Vector2(180f, 72f));
        TMP_Text speakerText = CreateRect("Text (TMP)", infoPanel, new Vector2(160f, 48f)).gameObject.AddComponent<TextMeshProUGUI>();

        InvokeTryHydrateRuntimeContent(root.gameObject);

        Assert.That(dialogText.text, Does.Contain("截图审查样例"));
        Assert.That(speakerText.text, Is.EqualTo("莉莉丝"));
        Assert.That(infoPanel.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void TryHydrateRuntimeContent_NarrativeContentUIScreen_AppliesScreenshotAuditSample()
    {
        RectTransform root = CreateRect("Narrative Content Panel", null, new Vector2(960f, 540f));
        RectTransform chapterSelectionRoot = CreateRect("Chapter Selection Panel", root, new Vector2(240f, 420f));
        TMP_Text titleText = CreateRect("Tittle", root, new Vector2(600f, 52f)).gameObject.AddComponent<TextMeshProUGUI>();
        TMP_Text leftText = CreateRect("Left Page Text", root, new Vector2(260f, 320f)).gameObject.AddComponent<TextMeshProUGUI>();
        TMP_Text rightText = CreateRect("Right Page Text", root, new Vector2(260f, 320f)).gameObject.AddComponent<TextMeshProUGUI>();
        CreateRect("Previous Page", root, new Vector2(80f, 80f)).gameObject.AddComponent<Button>();
        CreateRect("Next Page", root, new Vector2(80f, 80f)).gameObject.AddComponent<Button>();
        GameObject startBattleRoot = CreateRect("Start Battle Button", root, new Vector2(240f, 80f)).gameObject;
        startBattleRoot.AddComponent<Button>();

        InvokeTryAttachScreenComponentForCapture(root.gameObject, "Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab");
        InvokeTryHydrateRuntimeContent(root.gameObject);

        Assert.That(root.GetComponent<NarrativeContentUIScreen>(), Is.Not.Null);
        Assert.That(titleText.text, Is.EqualTo("布局审查样例 / 正文分页"));
        Assert.That(leftText.text, Does.Contain("左页样例"));
        Assert.That(rightText.text, Does.Contain("右页样例"));
        Assert.That(chapterSelectionRoot.childCount, Is.EqualTo(1));
        Assert.That(chapterSelectionRoot.GetComponentsInChildren<NarrativeChapterEntryView>(includeInactive: true), Has.Length.EqualTo(1));
        Assert.That(startBattleRoot.activeSelf, Is.False);
    }

    [Test]
    public void TryHydrateRuntimeContent_MainUIScreen_AppliesScreenshotAuditSample()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/MainHUD/MainUI.prefab");
        try
        {
            InvokeTryHydrateRuntimeContent(root);

            List<string> visibleTexts = root
                .GetComponentsInChildren<TMP_Text>(includeInactive: false)
                .Select(text => text.text)
                .ToList();
            RectTransform questPanel = FindRectTransform(root, "Quest Panel");
            RectTransform notificationPanel = FindRectTransform(root, "Notification Panel");
            QuestEntryView[] questEntries = root.GetComponentsInChildren<QuestEntryView>(includeInactive: true);

            Assert.That(questPanel.gameObject.activeSelf, Is.True);
            Assert.That(notificationPanel.gameObject.activeSelf, Is.True);
            Assert.That(visibleTexts, Does.Contain("获得奖励"));
            Assert.That(visibleTexts.Any(text => text.Contains("截图审查样例")), Is.True);
            Assert.That(visibleTexts.Any(text => text.Contains("任务面板")), Is.True);
            Assert.That(questEntries.Count(entry => entry.name.StartsWith("Screenshot Quest Entry")), Is.GreaterThanOrEqualTo(2));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void TryHydrateRuntimeContent_BossInfoUIScreen_AppliesScreenshotAuditSample()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/MainHUD/Boss Info UI.prefab");
        try
        {
            InvokeTryHydrateRuntimeContent(root);

            BossInfoUIScreen screen = root.GetComponent<BossInfoUIScreen>();
            TMP_Text bossName = root.transform.Find("Boss Info/Text (TMP)")?.GetComponent<TMP_Text>();
            RectTransform hpBar = FindRectTransform(root, "Hp Bar");

            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.getAlpha(), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(bossName, Is.Not.Null);
            Assert.That(bossName.text, Is.EqualTo("霜锋·贰阶段"));
            Assert.That(hpBar.childCount, Is.GreaterThan(0));
            Assert.That(root.GetComponentsInChildren<BaseCharEnemyNorm1>(includeInactive: true), Has.Length.EqualTo(1));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void TryHydrateRuntimeContent_PauseUIScreen_AppliesEmbeddedOptionsAuditSample()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/System/Pause/PauseUI.prefab");
        try
        {
            InvokeTryHydrateRuntimeContent(root);

            List<string> visibleTexts = root
                .GetComponentsInChildren<TMP_Text>(includeInactive: false)
                .Select(text => text.text)
                .ToList();
            RectTransform content = FindRectTransform(root, "Content");
            RectTransform menuButton = FindRectTransform(root, "Menu Button");
            RectTransform settingsPanel = root.transform.Find("Content Safe Frame/Setting Panel/Settings ") as RectTransform;

            Assert.That(visibleTexts, Does.Contain("显示"));
            Assert.That(visibleTexts.Any(text => text.Contains("界面缩放")), Is.False);
            Assert.That(content.childCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(settingsPanel, Is.Not.Null);
            Assert.That(menuButton.parent, Is.SameAs(settingsPanel));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void TryHydrateRuntimeContent_OptionsUIScreen_AppliesScreenshotAuditSample()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Options/Setting Panel.prefab");
        try
        {
            InvokeTryHydrateRuntimeContent(root);

            List<string> visibleTexts = root
                .GetComponentsInChildren<TMP_Text>(includeInactive: false)
                .Select(text => text.text)
                .ToList();
            RectTransform content = FindRectTransform(root, "Content");

            Assert.That(visibleTexts, Does.Contain("显示"));
            Assert.That(visibleTexts, Does.Contain("分辨率"));
            Assert.That(visibleTexts.Any(text => text.Contains("界面缩放")), Is.False);
            Assert.That(visibleTexts, Does.Contain("全屏模式"));
            Assert.That(content.childCount, Is.GreaterThanOrEqualTo(2));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [Test]
    public void TryHydrateRuntimeContent_HintUIScreen_AppliesScreenshotAuditSample()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Hint/Hint UI.prefab");
        try
        {
            InvokeTryHydrateRuntimeContent(root);

            List<string> visibleTexts = root
                .GetComponentsInChildren<TMP_Text>(includeInactive: false)
                .Select(text => text.text)
                .ToList();
            RectTransform leftContent = FindRectTransform(root, "Content");

            Assert.That(visibleTexts, Does.Contain("帮助"));
            Assert.That(visibleTexts.Any(text => text.Contains("布局审查")), Is.True);
            Assert.That(visibleTexts.Any(text => text.Contains("Hint 正文")), Is.True);
            Assert.That(leftContent.childCount, Is.GreaterThanOrEqualTo(2));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 size)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        RectTransform rectTransform = gameObject.transform as RectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        if (parent != null)
        {
            rectTransform.SetParent(parent, false);
        }

        return rectTransform;
    }

    private static RectTransform FindRectTransform(GameObject root, string objectName)
    {
        RectTransform transform = root
            .GetComponentsInChildren<RectTransform>(includeInactive: true)
            .FirstOrDefault(candidate => candidate.name == objectName);
        Assert.That(transform, Is.Not.Null, $"{objectName} should exist.");
        return transform;
    }

    private static void InvokeApplyResponsiveLayoutsForCapture(RectTransform captureScope, GameObject instance, string layoutModeName)
    {
        Type batcherType = typeof(UILayoutScreenshotBatcher);
        Type layoutModeType = batcherType.GetNestedType("TargetLayoutMode", BindingFlags.NonPublic);
        MethodInfo method = batcherType.GetMethod("ApplyResponsiveLayoutsForCapture", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(layoutModeType, Is.Not.Null);
        Assert.That(method, Is.Not.Null);

        object layoutMode = Enum.Parse(layoutModeType, layoutModeName);
        method.Invoke(null, new object[] { captureScope, instance, layoutMode });
    }

    private static void InvokeTryHydrateRuntimeContent(GameObject instance)
    {
        MethodInfo method = typeof(UILayoutScreenshotBatcher).GetMethod("TryHydrateRuntimeContent", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        method.Invoke(null, new object[] { instance });
    }

    private static void InvokeTryAttachScreenComponentForCapture(GameObject instance, string assetPath)
    {
        MethodInfo method = typeof(UILayoutScreenshotBatcher).GetMethod("TryAttachScreenComponentForCapture", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        method.Invoke(null, new object[] { instance, assetPath });
    }

    private static Array InvokeResolveTargetSet(string value)
    {
        MethodInfo method = typeof(UILayoutScreenshotBatcher).GetMethod("ResolveTargetSet", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        return (Array)method.Invoke(null, new object[] { value });
    }

    private static Array ReadScenarioArray(string fieldName)
    {
        FieldInfo field = typeof(UILayoutScreenshotBatcher).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(field, Is.Not.Null);
        return (Array)field.GetValue(null);
    }

    private static T ReadPrefabTargetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static T ReadScenarioField<T>(object scenario, string fieldName)
    {
        FieldInfo field = scenario.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(scenario);
    }

    private static float ResolveGridRequiredWidth(GridLayoutGroup layout, int columns)
    {
        return layout.padding.horizontal
            + (layout.cellSize.x * columns)
            + (layout.spacing.x * Mathf.Max(0, columns - 1));
    }

    private static float ResolveGridRequiredHeight(GridLayoutGroup layout, int columns, int childCount)
    {
        int rowCount = Mathf.CeilToInt(childCount / (float)columns);
        return layout.padding.vertical
            + (layout.cellSize.y * rowCount)
            + (layout.spacing.y * Mathf.Max(0, rowCount - 1));
    }

    private static float ResolveHorizontalRequiredWidth(HorizontalLayoutGroup layout)
    {
        float requiredWidth = layout.padding.horizontal;
        for (int i = 0; i < layout.transform.childCount; i++)
        {
            RectTransform child = layout.transform.GetChild(i) as RectTransform;
            requiredWidth += LayoutUtility.GetPreferredSize(child, 0);
        }

        requiredWidth += layout.spacing * Mathf.Max(0, layout.transform.childCount - 1);
        return requiredWidth;
    }

    private sealed class TestScreen : UIScreen
    {
    }
}
