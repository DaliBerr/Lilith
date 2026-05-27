using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Kernel.GameState;
using Kernel.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Vocalith.UI;
using Object = UnityEngine.Object;

public sealed class OptionsUIScreenTests
{
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
