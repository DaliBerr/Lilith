using System;
using System.Collections;
using System.Collections.Generic;
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
using Vocalith.UI;
using Object = UnityEngine.Object;

public sealed class OptionsUIScreenTests
{
    private const string SettingPanelPrefabPath = "Assets/Prefabs/UI/Options/Setting Panel.prefab";
    private const string OptionsPrefabAddress = "Assets/Prefabs/UI/Options/Setting Panel";

    private readonly List<Object> createdObjects = new();

    [SetUp]
    public void SetUp()
    {
        StatusController.Initialize();
        ResetUIManagerInstance();
    }

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

    [UnityTest]
    public IEnumerator RequestClose_WhenOptionsIsTopModal_ReleasesUIManagerNavigationLock()
    {
        UIManager uiManager = CreateUIManager();
        OptionsUIScreen screen = CreateOptionsScreen(uiManager);
        PushModal(uiManager, screen);

        screen.RequestClose();

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        for (int i = 0; i < 30 && uiManager.IsNavigating(); i++)
        {
            yield return null;
        }

        Assert.That(
            uiManager.IsNavigating(),
            Is.False,
            $"Options close should not leave UIManager navigation locked. ModalCount={GetModalCount(uiManager)}, HasPopUp={StatusController.HasStatus(StatusList.PopUpStatus)}, ScreenActive={screen != null && screen.gameObject.activeInHierarchy}");
        Assert.That(StatusController.HasStatus(StatusList.PopUpStatus), Is.False, "Options close should remove PopUp status.");
        Assert.That(screen == null || !screen.gameObject.activeInHierarchy, Is.True, "Options screen should be hidden or destroyed.");
    }

    [UnityTest]
    public IEnumerator ClearAllScreensAndModals_WhenModalCloseIsInterrupted_DestroysPoppedModal()
    {
        UIManager uiManager = CreateUIManager();
        SlowHideUIScreen screen = CreateSlowHideScreen(uiManager);
        PushModal(uiManager, screen);

        uiManager.CloseModal(screen);

        for (int i = 0; i < 5 && (!screen.HideStarted || GetModalCount(uiManager) > 0); i++)
        {
            yield return null;
        }

        Assert.That(screen.HideStarted, Is.True, "The modal should be inside its hide transition before clear-all interrupts it.");
        Assert.That(GetModalCount(uiManager), Is.EqualTo(0), "The closing modal should already be popped from the modal stack.");

        uiManager.ClearAllScreensAndModals();

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        for (int i = 0; i < 30 && uiManager.IsNavigating(); i++)
        {
            yield return null;
        }

        Assert.That(
            screen == null || !screen.gameObject.activeInHierarchy,
            Is.True,
            "Clear-all should destroy or deactivate a modal that was popped before its hide transition completed.");
        Assert.That(uiManager.IsNavigating(), Is.False, "Clear-all should release the navigation lock after cleaning interrupted transitions.");
        Assert.That(GetModalCount(uiManager), Is.EqualTo(0));
    }

    [Test]
    public void OptionsUIScreenPrefabAttribute_UsesSettingPanelAddress()
    {
        UIPrefabAttribute attribute = typeof(OptionsUIScreen).GetCustomAttribute<UIPrefabAttribute>();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute.Path, Is.EqualTo(OptionsPrefabAddress));
    }

    [Test]
    public void SettingPanelPrefab_HasOptionsUIScreenAndRequiredReferences()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SettingPanelPrefabPath);
        try
        {
            OptionsUIScreen screen = prefabRoot.GetComponent<OptionsUIScreen>();
            RectTransform rootRect = prefabRoot.transform as RectTransform;

            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.PreservePrefabRootRectTransform, Is.True);
            Assert.That(rootRect, Is.Not.Null);
            Assert.That(rootRect.anchorMin.x, Is.EqualTo(-0.1f).Within(0.0001f));
            Assert.That(rootRect.anchorMax.x, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(GetPrivateField<RectTransform>(screen, "catalogRoot"), Is.Not.Null);
            Assert.That(GetPrivateField<RectTransform>(screen, "contentRoot"), Is.Not.Null);
            Assert.That(GetPrivateField<Button>(screen, "closeButton"), Is.Not.Null);
            Assert.That(GetPrivateField<GameObject>(screen, "catalogButtonTemplate"), Is.Not.Null);
            Assert.That(GetPrivateField<ScrollRect>(screen, "scrollRect"), Is.Not.Null);
            Assert.That(GetPrivateField<GameObject>(screen, "buttonPanel"), Is.Not.Null);
            Assert.That(GetPrivateField<GameObject>(screen, "entryTemplate"), Is.Null);
            Assert.That(GetPrivateField<string>(screen, "entryPrefabAddress"), Is.EqualTo("Assets/Prefabs/UI/Option Entry Entry"));
            Assert.That(GetPrivateField<Button>(screen, "cancelButton"), Is.Null);
            Button resetButton = GetPrivateField<Button>(screen, "resetButton");
            Button applyButton = GetPrivateField<Button>(screen, "applyButton");
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

    [UnityTest]
    public IEnumerator ToggleValueChange_AppliesImmediatelyWithoutApplyButton()
    {
        const string prefsKey = "Options.Tests.InstantToggle";
        PlayerPrefs.DeleteKey(prefsKey);

        UIManager uiManager = CreateUIManager();
        OptionsUIScreen screen = CreateOptionsScreen(uiManager);
        ConfigureMinimalOptionsView(screen, prefsKey);

        InvokePrivate(screen, "RebuildView");

        Toggle toggle = FindActiveToggle(screen.transform);
        Assert.That(toggle, Is.Not.Null);

        toggle.isOn = true;
        yield return null;

        Assert.That(PlayerPrefs.GetInt(prefsKey, 0), Is.EqualTo(1));
        Assert.That(HasPendingChanges(screen), Is.False, "Immediate apply should sync OriginalValue to CurrentValue.");

        PlayerPrefs.DeleteKey(prefsKey);
    }

    [UnityTest]
    public IEnumerator ApplyAndResetButtons_StageAndCommitChanges()
    {
        const string prefsKey = "Options.Tests.ApplyResetToggle";
        PlayerPrefs.DeleteKey(prefsKey);

        UIManager uiManager = CreateUIManager();
        OptionsUIScreen screen = CreateOptionsScreen(uiManager);
        ConfigureMinimalOptionsView(screen, prefsKey);
        (Button resetButton, Button applyButton) = ConfigureActionButtons(screen);

        InvokePrivate(screen, "RebuildView");

        Toggle toggle = FindActiveToggle(screen.transform);
        Assert.That(toggle, Is.Not.Null);
        Assert.That(applyButton.gameObject.activeSelf, Is.False);
        Assert.That(resetButton.gameObject.activeSelf, Is.False);

        toggle.isOn = true;
        yield return null;

        Assert.That(PlayerPrefs.GetInt(prefsKey, 0), Is.EqualTo(0));
        Assert.That(HasPendingChanges(screen), Is.True);
        Assert.That(applyButton.gameObject.activeSelf, Is.True);
        Assert.That(resetButton.gameObject.activeSelf, Is.True);

        applyButton.onClick.Invoke();
        yield return null;

        Assert.That(PlayerPrefs.GetInt(prefsKey, 0), Is.EqualTo(1));
        Assert.That(HasPendingChanges(screen), Is.False);
        Assert.That(applyButton.gameObject.activeSelf, Is.False);
        Assert.That(resetButton.gameObject.activeSelf, Is.True);

        resetButton.onClick.Invoke();
        yield return null;

        Assert.That(PlayerPrefs.GetInt(prefsKey, 0), Is.EqualTo(1));
        Assert.That(HasPendingChanges(screen), Is.True);
        Assert.That(applyButton.gameObject.activeSelf, Is.True);

        applyButton.onClick.Invoke();
        yield return null;

        Assert.That(PlayerPrefs.GetInt(prefsKey, 1), Is.EqualTo(0));
        Assert.That(HasPendingChanges(screen), Is.False);
        Assert.That(resetButton.gameObject.activeSelf, Is.False);

        PlayerPrefs.DeleteKey(prefsKey);
    }

    [Test]
    public void RefreshActionButtons_WithMissingActionButtons_DoesNotThrow()
    {
        UIManager uiManager = CreateUIManager();
        OptionsUIScreen screen = CreateOptionsScreen(uiManager);

        Assert.DoesNotThrow(() => InvokePrivate(screen, "RefreshActionButtons"));
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

    private OptionsUIScreen CreateOptionsScreen(UIManager uiManager)
    {
        GameObject root = CreateUiObject("Options", uiManager.layerModal);
        root.AddComponent<CanvasGroup>();
        OptionsUIScreen screen = root.AddComponent<OptionsUIScreen>();
        InvokeInit(screen, uiManager);
        return screen;
    }

    private void ConfigureMinimalOptionsView(OptionsUIScreen screen, string prefsKey)
    {
        RectTransform catalogRoot = CreateUiObject("Setting Button Panel", screen.transform).transform as RectTransform;
        GameObject catalogTemplate = CreateUiObject("Pause Panel Setting Button", catalogRoot);
        catalogTemplate.AddComponent<Button>();

        RectTransform settingsRoot = CreateUiObject("Settings ", screen.transform).transform as RectTransform;
        RectTransform contentRoot = CreateUiObject("Setting Panel", settingsRoot).transform as RectTransform;
        GameObject entryTemplate = CreateOptionEntryTemplate(contentRoot);

        SetPrivateField(screen, "catalogRoot", catalogRoot);
        SetPrivateField(screen, "contentRoot", contentRoot);
        SetPrivateField(screen, "catalogButtonTemplate", catalogTemplate);
        SetPrivateField(screen, "entryTemplate", entryTemplate);
        SetPrivateField(screen, "entryPrefab", entryTemplate);
        SetPrivateField(screen, "cancelButton", null);
        SetPrivateField(screen, "resetButton", null);
        SetPrivateField(screen, "applyButton", null);
        SetPrivateField(screen, "buttonPanel", null);
        SetPrivateField(screen, "catalog", new OptionsCatalogData
        {
            Categories = new List<OptionsCategoryData>
            {
                new()
                {
                    Id = "test",
                    Title = "Test",
                    Entries = new List<OptionsEntryData>
                    {
                        new()
                        {
                            Id = "instant_toggle",
                            Title = "Instant Toggle",
                            Mode = "toggle",
                            PlayerPrefsKey = prefsKey,
                            DefaultBool = false,
                        },
                    },
                },
            },
        });
        SetPrivateField(screen, "hasLoadedCatalog", true);
    }

    private (Button Reset, Button Apply) ConfigureActionButtons(OptionsUIScreen screen)
    {
        GameObject buttonPanel = CreateUiObject("Button Panel", screen.transform);
        Button resetButton = CreateUiObject("Reset", buttonPanel.transform).AddComponent<Button>();
        Button applyButton = CreateUiObject("Apply", buttonPanel.transform).AddComponent<Button>();

        SetPrivateField(screen, "buttonPanel", buttonPanel);
        SetPrivateField(screen, "resetButton", resetButton);
        SetPrivateField(screen, "applyButton", applyButton);
        InvokePrivate(screen, "BindButtons");
        return (resetButton, applyButton);
    }

    private GameObject CreateOptionEntryTemplate(Transform parent)
    {
        GameObject entry = CreateUiObject("Option Entry Entry", parent);
        CreateUiObject("Setting Text", entry.transform).AddComponent<TextMeshProUGUI>();

        GameObject toggleRoot = CreateUiObject("Toggle", entry.transform);
        GameObject toggleObject = CreateUiObject("Toggle", toggleRoot.transform);
        toggleObject.AddComponent<Toggle>();

        CreateUiObject("Slider", entry.transform);
        CreateUiObject("Dropdown", entry.transform);
        CreateUiObject("Button", entry.transform);
        return entry;
    }

    private static Toggle FindActiveToggle(Transform root)
    {
        Toggle[] toggles = root.GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i] != null && toggles[i].gameObject.activeInHierarchy)
            {
                return toggles[i];
            }
        }

        return null;
    }

    private static bool HasPendingChanges(OptionsUIScreen screen)
    {
        object states = GetPrivateField<object>(screen, "entryStates");
        PropertyInfo valuesProperty = states.GetType().GetProperty("Values");
        Assert.That(valuesProperty, Is.Not.Null);

        IEnumerable values = (IEnumerable)valuesProperty.GetValue(states);
        foreach (object state in values)
        {
            string originalValue = (string)state.GetType().GetProperty("OriginalValue").GetValue(state);
            string currentValue = (string)state.GetType().GetProperty("CurrentValue").GetValue(state);
            if (!string.Equals(originalValue, currentValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private SlowHideUIScreen CreateSlowHideScreen(UIManager uiManager)
    {
        GameObject root = CreateUiObject("Slow Hide Modal", uiManager.layerModal);
        root.AddComponent<CanvasGroup>();
        SlowHideUIScreen screen = root.AddComponent<SlowHideUIScreen>();
        InvokeInit(screen, uiManager);
        screen.gameObject.SetActive(true);
        screen.setAlpha(1f);
        return screen;
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

    private static void PushModal(UIManager uiManager, UIScreen screen)
    {
        GetModalStack(uiManager).Push(screen);
    }

    private static int GetModalCount(UIManager uiManager)
    {
        return GetModalStack(uiManager).Count;
    }

    private static Stack<UIScreen> GetModalStack(UIManager uiManager)
    {
        FieldInfo modalStackField = typeof(UIManager).GetField(
            "modalStack",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(modalStackField, Is.Not.Null);

        return (Stack<UIScreen>)modalStackField.GetValue(uiManager);
    }

    private static void InvokeInit(UIScreen screen, UIManager uiManager)
    {
        MethodInfo initMethod = typeof(UIScreen).GetMethod(
            "__Init",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initMethod, Is.Not.Null);

        initMethod.Invoke(screen, new object[] { uiManager });
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        method.Invoke(target, Array.Empty<object>());
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        return (T)field.GetValue(target);
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

    private sealed class SlowHideUIScreen : UIScreen
    {
        public bool HideStarted { get; private set; }

        public override IEnumerator Hide(float fade = 0.12f)
        {
            HideStarted = true;
            setAlpha(0.5f);
            yield return null;
            yield return null;
            gameObject.SetActive(false);
        }
    }
}
