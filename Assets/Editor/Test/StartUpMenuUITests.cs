using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class StartUpMenuUITests
{
    private static readonly Color HoverTextColor = new Color(78f / 255f, 69f / 255f, 60f / 255f, 1f);

    private readonly List<Object> createdObjects = new();

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

        createdObjects.Clear();
    }

    [Test]
    public void OnInit_CurrentButtonLayout_ShowsButtonsAndConfiguresHoverVisuals()
    {
        StartUpMenuUI screen = CreateStartUpMenuUI(out List<Button> buttons, out List<Image> backgrounds, out List<TMP_Text> labels);
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].gameObject.SetActive(false);
        }

        InvokeNonPublic(screen, "OnInit");

        for (int i = 0; i < buttons.Count; i++)
        {
            Assert.That(buttons[i].gameObject.activeSelf, Is.True);
            Assert.That(buttons[i].interactable, Is.True);
            Assert.That(buttons[i].transition, Is.EqualTo(Selectable.Transition.None));
            AssertColor(backgrounds[i].color, new Color(1f, 1f, 1f, 0f));
            AssertColor(labels[i].color, Color.white);
        }
    }

    [Test]
    public void HoverFeedback_PointerEnterAndExit_TogglesBackgroundAndTextColor()
    {
        StartUpMenuUI screen = CreateStartUpMenuUI(out List<Button> buttons, out List<Image> backgrounds, out List<TMP_Text> labels);
        InvokeNonPublic(screen, "OnInit");
        EventSystem eventSystem = CreateEventSystem();
        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = Vector2.zero,
        };

        for (int i = 0; i < buttons.Count; i++)
        {
            ExecuteEvents.Execute<IPointerEnterHandler>(buttons[i].gameObject, eventData, ExecuteEvents.pointerEnterHandler);
            AssertColor(backgrounds[i].color, Color.white);
            AssertColor(labels[i].color, HoverTextColor);

            ExecuteEvents.Execute<IPointerExitHandler>(buttons[i].gameObject, eventData, ExecuteEvents.pointerExitHandler);
            AssertColor(backgrounds[i].color, new Color(1f, 1f, 1f, 0f));
            AssertColor(labels[i].color, Color.white);
        }
    }

    [Test]
    public void SetButtonsInteractable_AppliesToLoadButtonToo()
    {
        StartUpMenuUI screen = CreateStartUpMenuUI(out List<Button> buttons, out _, out _);
        InvokeNonPublic(screen, "OnInit");

        InvokeNonPublic(screen, "SetButtonsInteractable", false);

        for (int i = 0; i < buttons.Count; i++)
        {
            Assert.That(buttons[i].interactable, Is.False);
        }
    }

    [Test]
    public void OnInit_CurrentLayout_BindsSealPanelImage()
    {
        StartUpMenuUI screen = CreateStartUpMenuUI(out _, out _, out _);
        Image sealImage = screen.transform.Find("Seal Panel").GetComponent<Image>();

        InvokeNonPublic(screen, "OnInit");

        Assert.That(GetNonPublicField<Image>(screen, "sealImage"), Is.SameAs(sealImage));
    }

    [Test]
    public void OnInit_BindsStartAndLoadToSeparateHandlers()
    {
        StartUpMenuUI screen = CreateStartUpMenuUI(out List<Button> buttons, out _, out _);

        InvokeNonPublic(screen, "OnInit");

        List<string> startListeners = GetRuntimeListenerMethodNames(buttons[0].onClick);
        List<string> loadListeners = GetRuntimeListenerMethodNames(buttons[1].onClick);

        Assert.That(startListeners, Does.Contain("HandleStartButtonClicked"));
        Assert.That(startListeners, Does.Not.Contain("HandleLoadButtonClicked"));
        Assert.That(loadListeners, Does.Contain("HandleLoadButtonClicked"));
        Assert.That(loadListeners, Does.Not.Contain("HandleStartButtonClicked"));
    }

    private StartUpMenuUI CreateStartUpMenuUI(out List<Button> buttons, out List<Image> backgrounds, out List<TMP_Text> labels)
    {
        GameObject root = CreateUiObject("StartUp UI Prefab");
        root.AddComponent<Image>();
        root.AddComponent<CanvasGroup>();
        StartUpMenuUI screen = root.AddComponent<StartUpMenuUI>();

        GameObject buttonPanel = CreateUiObject("Button Panel", root.transform);
        buttonPanel.AddComponent<VerticalLayoutGroup>();

        buttons = new List<Button>();
        backgrounds = new List<Image>();
        labels = new List<TMP_Text>();
        CreateMenuButton("Start", buttonPanel.transform, buttons, backgrounds, labels);
        CreateMenuButton("Load", buttonPanel.transform, buttons, backgrounds, labels);
        CreateMenuButton("Settings", buttonPanel.transform, buttons, backgrounds, labels);
        CreateMenuButton("Quit", buttonPanel.transform, buttons, backgrounds, labels);

        GameObject sealPanel = CreateUiObject("Seal Panel", root.transform);
        sealPanel.AddComponent<Image>();

        return screen;
    }

    private void CreateMenuButton(
        string name,
        Transform parent,
        List<Button> buttons,
        List<Image> backgrounds,
        List<TMP_Text> labels)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.3f, 0.4f, 0.75f);
        Button button = buttonObject.AddComponent<Button>();

        GameObject labelObject = CreateUiObject("Text (TMP)", buttonObject.transform);
        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.color = Color.black;

        buttons.Add(button);
        backgrounds.Add(image);
        labels.Add(label);
    }

    private EventSystem CreateEventSystem()
    {
        GameObject eventSystemObject = CreateGameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        return eventSystem;
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

    private static List<string> GetRuntimeListenerMethodNames(UnityEvent unityEvent)
    {
        List<string> methodNames = new();
        FieldInfo callsField = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callsField, Is.Not.Null, "UnityEventBase.m_Calls should exist.");
        object calls = callsField.GetValue(unityEvent);
        FieldInfo runtimeCallsField = calls.GetType().GetField("m_RuntimeCalls", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(runtimeCallsField, Is.Not.Null, "InvokableCallList.m_RuntimeCalls should exist.");
        IList runtimeCalls = (IList)runtimeCallsField.GetValue(calls);

        foreach (object runtimeCall in runtimeCalls)
        {
            FieldInfo delegateField = runtimeCall.GetType().GetField("Delegate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(delegateField, Is.Not.Null, "InvokableCall.Delegate should exist.");
            if (delegateField.GetValue(runtimeCall) is System.Delegate callback)
            {
                foreach (System.Delegate invocation in callback.GetInvocationList())
                {
                    methodNames.Add(invocation.Method.Name);
                }
            }
        }

        return methodNames;
    }

    private static void AssertColor(Color actual, Color expected)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }
}
