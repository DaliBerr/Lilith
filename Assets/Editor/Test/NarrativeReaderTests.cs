using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.EventSystem;
using Vocalith.UI;

public sealed class NarrativeReaderTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void Catalog_ParseValidJson_ReturnsEntriesAndChapters()
    {
        const string json = @"{
  ""entries"": [
    {
      ""id"": ""entry_a"",
      ""title"": ""Entry A"",
      ""chapters"": [
        { ""id"": ""chapter_a"", ""title"": ""Chapter A"", ""pages"": [""Left"", ""Right""] }
      ]
    }
  ]
}";

        bool success = NarrativeCatalogUtility.TryDeserializeCatalogJson(json, out NarrativeCatalogData catalog, out string errorMessage);

        Assert.That(success, Is.True, errorMessage);
        Assert.That(catalog.Entries.Count, Is.EqualTo(1));
        Assert.That(catalog.Entries[0].Id, Is.EqualTo("entry_a"));
        Assert.That(catalog.Entries[0].Chapters[0].Pages, Is.EqualTo(new[] { "Left", "Right" }));
    }

    [Test]
    public void Catalog_RejectsInvalidShapes()
    {
        AssertCatalogInvalid(@"{ ""entries"": [] }", "at least one entry");
        AssertCatalogInvalid(@"{ ""entries"": [ { ""id"": ""entry"" } ] }", "at least one chapter");
        AssertCatalogInvalid(@"{ ""entries"": [ { ""id"": ""entry"", ""chapters"": [ { ""id"": ""chapter"", ""pages"": [] } ] } ] }", "at least one non-empty page");
        AssertCatalogInvalid(@"{ ""entries"": [
  { ""id"": ""same"", ""chapters"": [ { ""id"": ""chapter"", ""pages"": [""A""] } ] },
  { ""id"": ""same"", ""chapters"": [ { ""id"": ""chapter"", ""pages"": [""B""] } ] }
] }", "duplicated");
    }

    [Test]
    public void Menu_RebuildView_SplitsEntriesAcrossColumns()
    {
        NarrativeCatalogService service = CreateService(CreateCatalog(3));
        NarrativeMenuUIScreen screen = CreateMenuScreen(out RectTransform leftRoot, out RectTransform rightRoot);
        SetNonPublicField(screen, "storyEntryPrefab", CreateStoryEntryPrefab());

        InvokeNonPublic(screen, "RebuildView");

        Assert.That(service.Catalog.Entries.Count, Is.EqualTo(3));
        Assert.That(screen.RuntimeEntryViews.Count, Is.EqualTo(3));
        Assert.That(leftRoot.childCount, Is.EqualTo(2));
        Assert.That(rightRoot.childCount, Is.EqualTo(1));
        Assert.That(screen.RuntimeEntryViews[0].TitleText.text, Is.EqualTo("Entry 1"));
        Assert.That(screen.RuntimeEntryViews[0].ProgressText.text, Is.EqualTo("2/2"));
        Assert.That(screen.RuntimeEntryViews[2].TitleText.text, Is.EqualTo("Entry 3"));
    }

    [Test]
    public void MenuPrefab_HasResponsiveLayoutFitterForBothColumnGrids()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Narrative Menu Panel.prefab");

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<ResponsiveLayoutGroupFitter>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("Panel/Main Content/Left Panel")?.GetComponent<GridLayoutGroup>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("Panel/Main Content/Right Panel")?.GetComponent<GridLayoutGroup>(), Is.Not.Null);
    }

    [Test]
    public void ContentPrefab_BindsNestedTextWhenScreenComponentIsAttached()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab");

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponents<Component>(), Does.Not.Contain(null));

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        createdObjects.Add(instance);

        NarrativeContentUIScreen screen = instance.GetComponent<NarrativeContentUIScreen>() ?? instance.AddComponent<NarrativeContentUIScreen>();
        TMP_Text titleText = instance.transform.Find("Main Content/Tittle/Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text leftText = instance.transform.Find("Main Content/Left Page Text/Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text rightText = instance.transform.Find("Main Content/Right Page Text/Text (TMP)")?.GetComponent<TMP_Text>();

        Assert.That(screen, Is.Not.Null);
        Assert.That(titleText, Is.Not.Null);
        Assert.That(leftText, Is.Not.Null);
        Assert.That(rightText, Is.Not.Null);

        InvokeNonPublic(screen, "OnInit");
        screen.Configure(CreateEntry("layout_audit", "Layout Audit", pageCount: 2, chapterCount: 1));

        Assert.That(titleText.text, Is.EqualTo("Layout Audit / Chapter 1"));
        Assert.That(leftText.text, Is.EqualTo("Page 1"));
        Assert.That(rightText.text, Is.EqualTo("Page 2"));
    }

    [Test]
    public void ContentPrefab_UsesLocalReadablePageLayoutWithoutResponsiveFitter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Narrative Content Panel.prefab");

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<ResponsiveLayoutGroupFitter>(), Is.Null);

        RectTransform title = prefab.transform.Find("Main Content/Tittle") as RectTransform;
        RectTransform leftPage = prefab.transform.Find("Main Content/Left Page Text") as RectTransform;
        RectTransform rightPage = prefab.transform.Find("Main Content/Right Page Text") as RectTransform;
        RectTransform previousButton = prefab.transform.Find("Main Content/Previous Page") as RectTransform;
        RectTransform nextButton = prefab.transform.Find("Main Content/Next Page") as RectTransform;
        RectTransform chapterPanel = prefab.transform.Find("Chapter Selection Panel") as RectTransform;
        TMP_Text titleText = prefab.transform.Find("Main Content/Tittle/Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text leftPageText = prefab.transform.Find("Main Content/Left Page Text/Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text previousLabel = prefab.transform.Find("Main Content/Previous Page/Button/Text (TMP)")?.GetComponent<TMP_Text>();
        TMP_Text nextLabel = prefab.transform.Find("Main Content/Next Page/Button/Text (TMP)")?.GetComponent<TMP_Text>();

        Assert.That(title, Is.Not.Null);
        Assert.That(leftPage, Is.Not.Null);
        Assert.That(rightPage, Is.Not.Null);
        Assert.That(previousButton, Is.Not.Null);
        Assert.That(nextButton, Is.Not.Null);
        Assert.That(chapterPanel, Is.Not.Null);
        Assert.That(titleText, Is.Not.Null);
        Assert.That(leftPageText, Is.Not.Null);
        Assert.That(previousLabel, Is.Not.Null);
        Assert.That(nextLabel, Is.Not.Null);
        Assert.That(title.anchorMin.x, Is.EqualTo(0.12f).Within(0.001f));
        Assert.That(title.anchorMax.x, Is.EqualTo(0.88f).Within(0.001f));
        Assert.That(leftPage.anchorMin.x, Is.GreaterThanOrEqualTo(0.10f));
        Assert.That(rightPage.anchorMax.x, Is.LessThanOrEqualTo(0.90f));
        Assert.That(leftPage.anchorMax.y, Is.LessThan(title.anchorMin.y));
        Assert.That(rightPage.anchorMax.y, Is.EqualTo(leftPage.anchorMax.y).Within(0.001f));
        Assert.That(previousButton.anchorMax.x - previousButton.anchorMin.x, Is.GreaterThanOrEqualTo(0.05f));
        Assert.That(nextButton.anchorMax.x - nextButton.anchorMin.x, Is.GreaterThanOrEqualTo(0.05f));
        Assert.That(chapterPanel.anchorMin.y, Is.GreaterThanOrEqualTo(previousButton.anchorMax.y));
        Assert.That(chapterPanel.anchorMax.y, Is.LessThanOrEqualTo(title.anchorMin.y));
        Assert.That(titleText.enableAutoSizing, Is.True);
        Assert.That(titleText.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        Assert.That(titleText.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(leftPageText.enableAutoSizing, Is.True);
        Assert.That(previousLabel.text, Is.EqualTo("<"));
        Assert.That(nextLabel.text, Is.EqualTo(">"));
    }

    [Test]
    public void EntryPrefabs_UseAutoSizingAndEllipsisForScreenshotScaleRange()
    {
        GameObject storyEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Story Entry.prefab");
        GameObject chapterEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Narrative/Chapter Entry.prefab");

        Assert.That(storyEntryPrefab, Is.Not.Null);
        Assert.That(chapterEntryPrefab, Is.Not.Null);

        TMP_Text storyTitle = storyEntryPrefab.transform.Find("Tittle")?.GetComponent<TMP_Text>();
        TMP_Text storyProgress = storyEntryPrefab.transform.Find("Progress")?.GetComponent<TMP_Text>();
        TMP_Text chapterText = chapterEntryPrefab.GetComponentInChildren<TMP_Text>(includeInactive: true);
        LayoutElement chapterLayout = chapterEntryPrefab.GetComponent<LayoutElement>();

        Assert.That(storyTitle, Is.Not.Null);
        Assert.That(storyProgress, Is.Not.Null);
        Assert.That(chapterText, Is.Not.Null);
        Assert.That(chapterLayout, Is.Not.Null);
        Assert.That(storyTitle.enableAutoSizing, Is.True);
        Assert.That(storyProgress.enableAutoSizing, Is.True);
        Assert.That(chapterText.enableAutoSizing, Is.True);
        Assert.That(storyTitle.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(storyProgress.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(chapterText.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        Assert.That(chapterLayout.preferredHeight, Is.GreaterThanOrEqualTo(34f));
    }

    [Test]
    public void Content_RebuildView_BuildsChaptersAndPagesByPair()
    {
        NarrativeContentUIScreen screen = CreateContentScreen(
            out RectTransform chapterRoot,
            out TMP_Text titleText,
            out TMP_Text leftText,
            out TMP_Text rightText,
            out Button previousButton,
            out Button nextButton,
            out GameObject startBattleRoot,
            out _);
        SetNonPublicField(screen, "chapterEntryPrefab", CreateChapterEntryPrefab());
        NarrativeEntryData entry = CreateEntry("entry", "Entry", pageCount: 3, chapterCount: 2);

        screen.Configure(entry);

        Assert.That(screen.RuntimeChapterViews.Count, Is.EqualTo(2));
        Assert.That(chapterRoot.childCount, Is.EqualTo(2));
        Assert.That(titleText.text, Is.EqualTo("Entry / Chapter 1"));
        Assert.That(leftText.text, Is.EqualTo("Page 1"));
        Assert.That(rightText.text, Is.EqualTo("Page 2"));
        Assert.That(previousButton.interactable, Is.False);
        Assert.That(nextButton.interactable, Is.True);
        Assert.That(startBattleRoot.activeSelf, Is.False);

        nextButton.onClick.Invoke();

        Assert.That(screen.ActivePagePairIndex, Is.EqualTo(1));
        Assert.That(leftText.text, Is.EqualTo("Page 3"));
        Assert.That(rightText.text, Is.EqualTo(string.Empty));
        Assert.That(previousButton.interactable, Is.True);
        Assert.That(nextButton.interactable, Is.False);
    }

    [Test]
    public void Content_LastChapter_StartBattleButtonPublishesEvent()
    {
        NarrativeContentUIScreen screen = CreateContentScreen(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out GameObject startBattleRoot,
            out Button startBattleButton);
        SetNonPublicField(screen, "chapterEntryPrefab", CreateChapterEntryPrefab());
        NarrativeEntryData entry = CreateEntry("entry", "Entry", pageCount: 2, chapterCount: 2);
        screen.Configure(entry);

        NarrativeStartBattleRequestedEvent? receivedEvent = null;
        IDisposable subscription = EventManager.eventBus.Subscribe<NarrativeStartBattleRequestedEvent>(evt => receivedEvent = evt);
        try
        {
            screen.RuntimeChapterViews[1].Button.onClick.Invoke();

            Assert.That(startBattleRoot.activeSelf, Is.True);
            startBattleButton.onClick.Invoke();

            Assert.That(receivedEvent.HasValue, Is.True);
            Assert.That(receivedEvent.Value.EntryId, Is.EqualTo("entry"));
            Assert.That(receivedEvent.Value.ChapterId, Is.EqualTo("chapter_2"));
        }
        finally
        {
            subscription.Dispose();
        }
    }

    private static void AssertCatalogInvalid(string json, string expectedErrorFragment)
    {
        bool success = NarrativeCatalogUtility.TryDeserializeCatalogJson(json, out NarrativeCatalogData catalog, out string errorMessage);
        Assert.That(success, Is.False);
        Assert.That(catalog, Is.Null);
        Assert.That(errorMessage, Does.Contain(expectedErrorFragment));
    }

    private NarrativeCatalogService CreateService(NarrativeCatalogData catalog)
    {
        NarrativeCatalogService service = NarrativeCatalogService.GetOrCreateInstance();
        createdObjects.Add(service.gameObject);
        Assert.That(service.TryUseCatalog(catalog, out string errorMessage), Is.True, errorMessage);
        return service;
    }

    private NarrativeCatalogData CreateCatalog(int entryCount)
    {
        NarrativeCatalogData catalog = new();
        for (int index = 0; index < entryCount; index++)
        {
            catalog.Entries.Add(CreateEntry($"entry_{index + 1}", $"Entry {index + 1}", pageCount: 2, chapterCount: 2));
        }

        return catalog;
    }

    private static NarrativeEntryData CreateEntry(string id, string title, int pageCount, int chapterCount)
    {
        NarrativeEntryData entry = new()
        {
            Id = id,
            Title = title,
            ShowStartBattleOnLastChapter = true,
            Chapters = new List<NarrativeChapterData>(),
        };

        for (int chapterIndex = 0; chapterIndex < chapterCount; chapterIndex++)
        {
            NarrativeChapterData chapter = new()
            {
                Id = $"chapter_{chapterIndex + 1}",
                Title = $"Chapter {chapterIndex + 1}",
                Pages = new List<string>(),
            };

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                chapter.Pages.Add($"Page {pageIndex + 1}");
            }

            entry.Chapters.Add(chapter);
        }

        return entry;
    }

    private NarrativeMenuUIScreen CreateMenuScreen(out RectTransform leftRoot, out RectTransform rightRoot)
    {
        GameObject root = CreateUiObject("Narrative Menu Panel");
        root.AddComponent<CanvasGroup>();
        NarrativeMenuUIScreen screen = root.AddComponent<NarrativeMenuUIScreen>();
        leftRoot = CreateUiObject("Left Panel", root.transform).GetComponent<RectTransform>();
        rightRoot = CreateUiObject("Right Panel", root.transform).GetComponent<RectTransform>();
        InvokeNonPublic(screen, "OnInit");
        return screen;
    }

    private NarrativeContentUIScreen CreateContentScreen(
        out RectTransform chapterRoot,
        out TMP_Text titleText,
        out TMP_Text leftText,
        out TMP_Text rightText,
        out Button previousButton,
        out Button nextButton,
        out GameObject startBattleRoot,
        out Button startBattleButton)
    {
        GameObject root = CreateUiObject("Narrative Content Panel");
        root.AddComponent<CanvasGroup>();
        NarrativeContentUIScreen screen = root.AddComponent<NarrativeContentUIScreen>();

        chapterRoot = CreateUiObject("Chapter Selection Panel", root.transform).GetComponent<RectTransform>();
        titleText = CreateTextObject("Tittle", root.transform);
        leftText = CreateTextObject("Left Page Text", root.transform);
        rightText = CreateTextObject("Right Page Text", root.transform);

        previousButton = CreateButtonObject("Previous Page", root.transform, out _);
        nextButton = CreateButtonObject("Next Page", root.transform, out _);
        startBattleRoot = CreateUiObject("Start Battle Button", root.transform);
        startBattleButton = startBattleRoot.AddComponent<Button>();
        startBattleRoot.SetActive(false);

        InvokeNonPublic(screen, "OnInit");
        return screen;
    }

    private GameObject CreateStoryEntryPrefab()
    {
        GameObject root = CreateUiObject("Story Entry Prefab");
        root.AddComponent<NarrativeStoryEntryView>();
        CreateButtonObject("Trigger Button", root.transform, out _);
        CreateTextObject("Progress", root.transform);
        CreateTextObject("Tittle", root.transform);
        return root;
    }

    private GameObject CreateChapterEntryPrefab()
    {
        GameObject root = CreateUiObject("Chapter Entry Prefab");
        root.AddComponent<NarrativeChapterEntryView>();
        CreateTextObject("Text (TMP)", root.transform);
        CreateButtonObject("Button", root.transform, out _);
        return root;
    }

    private TMP_Text CreateTextObject(string name, Transform parent)
    {
        GameObject textObject = CreateUiObject(name, parent);
        return textObject.AddComponent<TextMeshProUGUI>();
    }

    private Button CreateButtonObject(string name, Transform parent, out TMP_Text label)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Button button = buttonObject.AddComponent<Button>();
        label = CreateTextObject("Text (TMP)", buttonObject.transform);
        return button;
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

    private static void InvokeNonPublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, null);
    }

    private static void SetNonPublicField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} should exist.");
        field.SetValue(target, value);
    }
}
