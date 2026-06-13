using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

public sealed class ProfileManagementUIScreenTests
{
    private const string ProfilePopupPrefabPath = "Assets/Prefabs/UI/Profile/Profile Popup.prefab";

    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        DestroyExistingRuntimeSaveService();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        DeleteSaveDirectory();
    }

    [Test]
    public void OnInit_NewScrollContentWithoutSaves_ShowsDisabledEmptyStateRow()
    {
        PrepareCleanSaveEnvironment();
        CreateSaveService();
        ProfileManagementUIScreen screen = CreateProfileScreen(out RectTransform content, out RectTransform template);

        InvokeNonPublic(screen, "OnInit");

        Assert.That(content.childCount, Is.EqualTo(1));
        Assert.That(template.gameObject.activeSelf, Is.True);
        Assert.That(template.Find("Trigger Button").GetComponent<Button>().interactable, Is.False);
        Assert.That(template.Find("Delete Button").gameObject.activeSelf, Is.False);
        Assert.That(template.Find("Void Info").gameObject.activeSelf, Is.True);
        Assert.That(template.Find("Void Info/Text (TMP)").GetComponent<TMP_Text>().text, Is.EqualTo("暂无可加载存档。"));
    }

    [Test]
    public void OnInit_NewScrollContentWithExistingSaves_RendersOneRowPerExistingProfile()
    {
        PrepareCleanSaveEnvironment();
        RuntimeSaveService saveService = CreateSaveService();
        Assert.That(saveService.SelectProfileSlot(0, out _), Is.True);
        Assert.That(saveService.SelectProfileSlot(2, out _), Is.True);
        ProfileManagementUIScreen screen = CreateProfileScreen(out RectTransform content, out _);

        InvokeNonPublic(screen, "OnInit");

        Assert.That(content.childCount, Is.EqualTo(2));
        AssertExistingRow(content.GetChild(0));
        AssertExistingRow(content.GetChild(1));
    }

    [Test]
    public void ProfilePopupPrefab_UsesStableHeaderAnchorsWithoutResponsiveFitter()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ProfilePopupPrefabPath);
        try
        {
            RectTransform topPanel = FindRectTransform(prefabRoot, "Top Panel");
            RectTransform title = FindRectTransform(prefabRoot, "Tittle");
            RectTransform main = FindRectTransform(prefabRoot, "Main ");
            RectTransform mainContent = FindRectTransform(prefabRoot, "Main Content");
            RectTransform closeButton = FindRectTransform(prefabRoot, "Close Button");
            TMP_Text titleText = title.GetComponent<TMP_Text>();

            Assert.That(prefabRoot.GetComponentInChildren<ResponsiveLayoutGroupFitter>(true), Is.Null);
            Assert.That(topPanel.anchorMin.y, Is.EqualTo(0.91f).Within(0.001f));
            Assert.That(topPanel.anchorMax.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(main.anchorMax.y, Is.LessThanOrEqualTo(0.89f));
            Assert.That(mainContent.anchorMax.y, Is.LessThanOrEqualTo(0.89f));

            Assert.That(title.parent, Is.SameAs(topPanel));
            Assert.That(title.anchorMin.x, Is.EqualTo(0.02f).Within(0.001f));
            Assert.That(title.anchorMax.x, Is.EqualTo(0.78f).Within(0.001f));
            Assert.That(title.sizeDelta.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(titleText.margin.z, Is.EqualTo(0f).Within(0.001f));

            Assert.That(closeButton.parent, Is.SameAs(topPanel));
            Assert.That(closeButton.anchorMin.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(closeButton.anchorMax.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(closeButton.sizeDelta.x, Is.LessThanOrEqualTo(100f));
            Assert.That(closeButton.anchoredPosition.x, Is.EqualTo(-closeButton.sizeDelta.x * 0.5f).Within(0.001f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private ProfileManagementUIScreen CreateProfileScreen(out RectTransform content, out RectTransform template)
    {
        GameObject root = CreateUiObject("Profile Popup");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        ProfileManagementUIScreen screen = root.AddComponent<ProfileManagementUIScreen>();

        GameObject popup = CreateUiObject("Popup", root.transform);
        GameObject topPanel = CreateUiObject("Top Panel", popup.transform);
        CreateText("Tittle", topPanel.transform, "存档");
        GameObject closeButtonRoot = CreateUiObject("Close Button", topPanel.transform);
        GameObject closeButtonEdge = CreateUiObject("Edge", closeButtonRoot.transform);
        CreateButton("Button", closeButtonEdge.transform);

        GameObject main = CreateUiObject("Main ", popup.transform);
        GameObject viewport = CreateUiObject("Viewport", main.transform);
        content = CreateUiObject("Content", viewport.transform).GetComponent<RectTransform>();
        template = CreateProfileItem("Profile item Prefab", content.transform);

        return screen;
    }

    private RectTransform CreateProfileItem(string name, Transform parent)
    {
        GameObject item = CreateUiObject(name, parent);
        item.AddComponent<Image>();

        GameObject timePanel = CreateUiObject("Time Panel", item.transform);
        CreateText("Text (TMP)", timePanel.transform, "2026-01-01 00:00");

        GameObject contentPanel = CreateUiObject("Content Panel", item.transform);
        CreateUiObject("Image", contentPanel.transform).AddComponent<Image>();
        GameObject backpackInfo = CreateUiObject("Backpack Info", contentPanel.transform);
        CreateText("Text (TMP)", backpackInfo.transform, "Backpack");

        GameObject triggerButton = CreateButton("Trigger Button", item.transform);
        CreateText("Text (TMP)", triggerButton.transform, "Load");

        GameObject voidInfo = CreateUiObject("Void Info", item.transform);
        CreateText("Text (TMP)", voidInfo.transform, "Empty");
        voidInfo.SetActive(false);

        GameObject deleteButton = CreateButton("Delete Button", item.transform);
        CreateText("Text (TMP)", deleteButton.transform, "Delete");

        return item.GetComponent<RectTransform>();
    }

    private static RectTransform FindRectTransform(GameObject root, string objectName)
    {
        RectTransform transform = root
            .GetComponentsInChildren<RectTransform>(includeInactive: true)
            .FirstOrDefault(candidate => candidate.name == objectName);
        Assert.That(transform, Is.Not.Null, $"{objectName} should exist in the profile popup prefab.");
        return transform;
    }

    private GameObject CreateButton(string name, Transform parent)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        buttonObject.AddComponent<Image>();
        buttonObject.AddComponent<Button>();
        return buttonObject;
    }

    private TMP_Text CreateText(string name, Transform parent, string text)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        return label;
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

    private RuntimeSaveService CreateSaveService()
    {
        DestroyExistingRuntimeSaveService();
        GameObject saveObject = new("RuntimeSaveService");
        createdObjects.Add(saveObject);
        return saveObject.AddComponent<RuntimeSaveService>();
    }

    private static void AssertExistingRow(Transform row)
    {
        Assert.That(row.gameObject.activeSelf, Is.True);
        Assert.That(row.Find("Time Panel").gameObject.activeSelf, Is.True);
        Assert.That(row.Find("Content Panel").gameObject.activeSelf, Is.True);
        Assert.That(row.Find("Trigger Button").GetComponent<Button>().interactable, Is.True);
        Assert.That(row.Find("Delete Button").gameObject.activeSelf, Is.True);
        Assert.That(row.Find("Delete Button").GetComponent<Button>().interactable, Is.True);
        Assert.That(row.Find("Void Info").gameObject.activeSelf, Is.False);
    }

    private static void InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} should exist.");
        method.Invoke(target, arguments);
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

    private static void DestroyExistingRuntimeSaveService()
    {
        RuntimeSaveService existingService = Object.FindFirstObjectByType<RuntimeSaveService>();
        if (existingService != null)
        {
            Object.DestroyImmediate(existingService.gameObject);
        }
    }
}
