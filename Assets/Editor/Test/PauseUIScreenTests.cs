using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kernel.GameState;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Vocalith.Localization;
using Vocalith.UI;
using Object = UnityEngine.Object;

public sealed class PauseUIScreenTests
{
    private const string PauseUIPrefabPath = "Assets/Prefabs/UI/System/Pause/PauseUI.prefab";
    private const string InfoPopupPrefabPath = "Assets/Prefabs/UI/System/Info Popup.prefab";

    private readonly List<Object> createdObjects = new();
    private Dictionary<string, string> savedLocalizationStrings;

    [SetUp]
    public void SetUp()
    {
        savedLocalizationStrings = new Dictionary<string, string>(GetStringTable());
        GetStringTable().Clear();
        StatusController.Initialize();
        ResetUIManagerInstance();
    }

    [TearDown]
    public void TearDown()
    {
        IDictionary<string, string> stringTable = GetStringTable();
        stringTable.Clear();
        foreach (KeyValuePair<string, string> entry in savedLocalizationStrings)
        {
            stringTable[entry.Key] = entry.Value;
        }

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        foreach (EventSystem eventSystem in Object.FindObjectsByType<EventSystem>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        createdObjects.Clear();
        StatusController.Initialize();
        ResetUIManagerInstance();
    }

    [Test]
    public void PauseUIPrefab_BindsEmbeddedSettingPanel()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PauseUIPrefabPath);
        try
        {
            PauseUIScreen screen = prefabRoot.GetComponent<PauseUIScreen>();

            Assert.That(screen, Is.Not.Null);
            Assert.That(GetPrivateField<GameObject>(screen, "settingPanel"), Is.Not.Null);
            Assert.That(GetPrivateField<OptionsUIScreen>(screen, "embeddedOptionsScreen"), Is.Not.Null);
            Assert.That(GetPrivateField<Button>(screen, "settingCloseButton"), Is.Not.Null);
            Assert.That(GetPrivateField<Button>(screen, "settingMenuButton"), Is.Not.Null);
            TMP_Text menuButtonText = GetPrivateField<TMP_Text>(screen, "settingMenuButtonText");
            Assert.That(menuButtonText, Is.Not.Null);
            Assert.That(menuButtonText.text, Is.EqualTo("返"));

            OptionsUIScreen embeddedOptions = GetPrivateField<OptionsUIScreen>(screen, "embeddedOptionsScreen");
            Assert.That(GetPrivateField<GameObject>(embeddedOptions, "buttonPanel"), Is.Not.Null);
            Button resetButton = GetPrivateField<Button>(embeddedOptions, "resetButton");
            Button applyButton = GetPrivateField<Button>(embeddedOptions, "applyButton");
            Assert.That(resetButton, Is.Not.Null);
            Assert.That(applyButton, Is.Not.Null);
            Assert.That(resetButton.GetComponentInChildren<TMP_Text>(true)?.text, Is.EqualTo("重置"));
            Assert.That(applyButton.GetComponentInChildren<TMP_Text>(true)?.text, Is.EqualTo("应用"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void PauseUIPrefab_MenuButtonStaysInsideHostPanel()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PauseUIPrefabPath);
        try
        {
            RectTransform menuButton = FindRectTransform(prefabRoot, "Menu Button");
            RectTransform settingPanel = FindRectTransform(prefabRoot, "Setting Panel");
            RectTransform settingsPanel = prefabRoot.transform.Find("Content Safe Frame/Setting Panel/Settings ") as RectTransform;
            TMP_Text menuText = menuButton.GetComponentInChildren<TMP_Text>(true);

            Assert.That(settingsPanel, Is.Not.Null);
            Assert.That(menuButton.parent, Is.SameAs(settingsPanel));
            Assert.That(menuButton.anchorMin.y, Is.GreaterThanOrEqualTo(0.04f));
            Assert.That(menuButton.anchorMax.y, Is.LessThanOrEqualTo(0.15f));
            Assert.That(menuButton.anchorMin.y, Is.LessThan(menuButton.anchorMax.y));
            Assert.That(settingPanel.anchorMin.y, Is.LessThanOrEqualTo(0.16f));
            Assert.That(settingsPanel.anchorMin.y, Is.LessThanOrEqualTo(0.10f));
            Assert.That(menuText, Is.Not.Null);
            Assert.That(menuText.enableAutoSizing, Is.True);
            Assert.That(menuText.fontSizeMax, Is.LessThanOrEqualTo(40f));
            Assert.That(menuText.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void InfoPopupPrefab_ButtonRowFitsWithinNarrowModal()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(InfoPopupPrefabPath);
        try
        {
            HorizontalLayoutGroup layout = prefabRoot
                .GetComponentsInChildren<HorizontalLayoutGroup>(true)
                .FirstOrDefault(candidate => candidate.name == "Button");
            Assert.That(layout, Is.Not.Null);

            RectTransform buttonRow = layout.transform as RectTransform;
            LayoutElement[] buttonElements = buttonRow.GetComponentsInChildren<LayoutElement>(true);

            Assert.That(layout.spacing, Is.LessThanOrEqualTo(24f));
            Assert.That(buttonElements.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(buttonElements.Select(element => element.minWidth), Is.All.LessThanOrEqualTo(180f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void OnInit_WithNoMainPanel_DoesNotThrow()
    {
        UIManager uiManager = CreateUIManager();
        GameObject root = CreateUiObject("PauseUI", uiManager.layerScreen);
        root.AddComponent<CanvasGroup>();
        PauseUIScreen screen = root.AddComponent<PauseUIScreen>();
        CreateEmbeddedSettingPanel(root.transform);

        Assert.DoesNotThrow(() => InvokeInit(screen));
    }

    [UnityTest]
    public IEnumerator EmbeddedOptionsRequestClose_InvokesHostCloseCallback()
    {
        UIManager uiManager = CreateUIManager();
        GameObject root = CreateUiObject("Setting Panel", uiManager.layerScreen);
        OptionsUIScreen screen = root.AddComponent<OptionsUIScreen>();
        int closeCount = 0;

        SetPrivateField(screen, "isEmbeddedInHost", true);
        SetPrivateField(screen, "embeddedCloseCallback", (Action)(() => closeCount++));
        screen.RequestClose();
        yield return null;

        Assert.That(closeCount, Is.EqualTo(1));
    }

    [Test]
    public void ReturnToStartUpConfirmation_ConfiguresCommonPopup()
    {
        LocalizationManager.RegisterString("ui.pause.return_start.confirm_message", "返回主菜单？");
        LocalizationManager.RegisterString("ui.pause.return_start.confirm", "回主菜单");
        LocalizationManager.RegisterString("ui.common.cancel", "继续游戏");
        GameObject root = CreateUiObject("PauseUI");
        PauseUIScreen screen = root.AddComponent<PauseUIScreen>();
        PopUpUIScreen popup = CreatePopup();
        InvokeNonPublic(popup, "OnInit");

        InvokeNonPublic(screen, "ConfigureReturnToStartUpConfirmation", popup);

        Assert.That(popup.InfoText.text, Is.EqualTo("返回主菜单？"));
        Assert.That(popup.ConfirmButtonText.text, Is.EqualTo("回主菜单"));
        Assert.That(popup.CloseButtonText.text, Is.EqualTo("继续游戏"));
    }

    private UIManager CreateUIManager()
    {
        GameObject root = CreateGameObject("UIManager");
        UIManager uiManager = root.AddComponent<UIManager>();
        uiManager.defaultHide = 0f;

        GameObject canvasObject = CreateUiObject("Root Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        uiManager.rootCanvas = canvas;
        uiManager.layerScreen = CreateLayer("Screen", canvasObject.transform);
        uiManager.layerModal = CreateLayer("Modal", canvasObject.transform);
        uiManager.layerOverlay = CreateLayer("Overlay", canvasObject.transform);
        uiManager.layerToast = CreateLayer("Toast", canvasObject.transform);

        return uiManager;
    }

    private void CreateEmbeddedSettingPanel(Transform parent)
    {
        GameObject settingPanel = CreateUiObject("Setting Panel", parent);
        settingPanel.AddComponent<OptionsUIScreen>();

        GameObject settings = CreateUiObject("Settings ", settingPanel.transform);
        CreateButton("Close Button", settings.transform);
        CreateButton("Menu Button", settings.transform);
    }

    private Button CreateButton(string name, Transform parent)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        return buttonObject.AddComponent<Button>();
    }

    private PopUpUIScreen CreatePopup()
    {
        GameObject root = CreateUiObject("Info Popup");
        root.AddComponent<CanvasGroup>();
        PopUpUIScreen popup = root.AddComponent<PopUpUIScreen>();

        GameObject topPanel = CreateUiObject("Top Panel", root.transform);
        CreateNestedButton("Close Button", topPanel.transform);

        GameObject mainContent = CreateUiObject("Main Content", root.transform);
        GameObject infoPanel = CreateUiObject("Info", mainContent.transform);
        CreateUiObject("Text", infoPanel.transform).AddComponent<TextMeshProUGUI>();

        GameObject buttonPanel = CreateUiObject("Button", mainContent.transform);
        CreateNestedButton("Confirm Buton", buttonPanel.transform);
        CreateNestedButton("Close Button", buttonPanel.transform);

        return popup;
    }

    private Button CreateNestedButton(string name, Transform parent)
    {
        GameObject buttonRoot = CreateUiObject(name, parent);
        GameObject edge = CreateUiObject("Edge", buttonRoot.transform);
        GameObject buttonObject = CreateUiObject("Button", edge.transform);
        buttonObject.AddComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();

        CreateUiObject("Text (TMP)", buttonObject.transform).AddComponent<TextMeshProUGUI>();
        return button;
    }

    private RectTransform CreateLayer(string name, Transform parent)
    {
        GameObject layer = CreateUiObject(name, parent);
        return layer.transform as RectTransform;
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

    private static void InvokeInit(UIScreen screen)
    {
        MethodInfo initMethod = typeof(PauseUIScreen).GetMethod(
            "OnInit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initMethod, Is.Not.Null);

        initMethod.Invoke(screen, Array.Empty<object>());
    }

    private static void InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        method.Invoke(target, arguments);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (T)field.GetValue(target);
    }

    private static RectTransform FindRectTransform(GameObject root, string name)
    {
        RectTransform rectTransform = root
            .GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(candidate => candidate.name == name);
        Assert.That(rectTransform, Is.Not.Null, $"{name} should exist in {root.name}.");
        return rectTransform;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        field.SetValue(target, value);
    }

    private static void ResetUIManagerInstance()
    {
        FieldInfo instanceField = typeof(UIManager).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(instanceField, Is.Not.Null);
        instanceField.SetValue(null, null);
    }

    private static IDictionary<string, string> GetStringTable()
    {
        FieldInfo field = typeof(LocalizationManager).GetField(
            "_stringTable",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (IDictionary<string, string>)field.GetValue(null);
    }
}
