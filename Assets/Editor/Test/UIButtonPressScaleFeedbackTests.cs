using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Vocalith.UI;
using Object = UnityEngine.Object;

public sealed class UIButtonPressScaleFeedbackTests
{
    private GameObject root;
    private EventSystem eventSystem;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Test Root");
        eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }

        if (eventSystem != null)
        {
            Object.DestroyImmediate(eventSystem.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator PointerDownAndUp_ScalesAndRestoresButton()
    {
        Button button = CreateButton("Button");
        button.transform.localScale = new Vector3(2f, 1.5f, 1f);
        UIButtonPressScaleFeedback feedback = button.gameObject.AddComponent<UIButtonPressScaleFeedback>();
        MakeFeedbackInstant(feedback);
        PointerEventData eventData = new(eventSystem);

        ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, eventData, ExecuteEvents.pointerDownHandler);

        AssertScale(button.transform.localScale, new Vector3(1.88f, 1.41f, 0.94f));

        ExecuteEvents.Execute<IPointerUpHandler>(button.gameObject, eventData, ExecuteEvents.pointerUpHandler);

        AssertScale(button.transform.localScale, new Vector3(2f, 1.5f, 1f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator PointerUp_RestoresButtonScale()
    {
        Button button = CreateButton("Button");
        UIButtonPressScaleFeedback feedback = button.gameObject.AddComponent<UIButtonPressScaleFeedback>();
        MakeFeedbackInstant(feedback);
        PointerEventData eventData = new(eventSystem);

        ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        AssertScale(button.transform.localScale, Vector3.one * 0.94f);

        feedback.OnPointerUp(eventData);
        AssertScale(button.transform.localScale, Vector3.one);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Cancel_RestoresButtonScale()
    {
        Button button = CreateButton("Button");
        UIButtonPressScaleFeedback feedback = button.gameObject.AddComponent<UIButtonPressScaleFeedback>();
        MakeFeedbackInstant(feedback);
        PointerEventData eventData = new(eventSystem);

        ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        AssertScale(button.transform.localScale, Vector3.one * 0.94f);

        feedback.OnCancel(new BaseEventData(eventSystem));
        AssertScale(button.transform.localScale, Vector3.one);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Disable_RestoresButtonScale()
    {
        Button button = CreateButton("Button");
        UIButtonPressScaleFeedback feedback = button.gameObject.AddComponent<UIButtonPressScaleFeedback>();
        MakeFeedbackInstant(feedback);
        PointerEventData eventData = new(eventSystem);

        ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        AssertScale(button.transform.localScale, Vector3.one * 0.94f);

        InvokeFeedbackOnDisable(feedback);
        AssertScale(button.transform.localScale, Vector3.one);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PointerDown_WhenButtonIsNotInteractable_DoesNotScale()
    {
        Button button = CreateButton("Button");
        button.interactable = false;
        UIButtonPressScaleFeedback feedback = button.gameObject.AddComponent<UIButtonPressScaleFeedback>();
        MakeFeedbackInstant(feedback);
        PointerEventData eventData = new(eventSystem);

        ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        yield return null;

        AssertScale(button.transform.localScale, Vector3.one);
    }

    [UnityTest]
    public IEnumerator AutoInstaller_AddsFeedbackToExistingAndRuntimeButtons()
    {
        GameObject canvasObject = new("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        UIButtonPressScaleAutoInstaller installer = canvasObject.AddComponent<UIButtonPressScaleAutoInstaller>();
        Button existingButton = CreateButton("Existing Button", canvasObject.transform);

        installer.SetRoot(canvas);

        Assert.That(existingButton.GetComponent<UIButtonPressScaleFeedback>(), Is.Not.Null);

        Button runtimeButton = CreateButton("Runtime Button", canvasObject.transform);
        InvokeInstallerUpdate(installer);

        Assert.That(runtimeButton.GetComponent<UIButtonPressScaleFeedback>(), Is.Not.Null);
        yield return null;
    }

    private Button CreateButton(string name, Transform parent = null)
    {
        GameObject target = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        target.transform.SetParent(parent != null ? parent : root.transform, false);
        return target.GetComponent<Button>();
    }

    private static void MakeFeedbackInstant(UIButtonPressScaleFeedback feedback)
    {
        SetPrivateFloat(feedback, "pressDuration", 0f);
        SetPrivateFloat(feedback, "releaseDuration", 0f);
    }

    private static void SetPrivateFloat(UIButtonPressScaleFeedback feedback, string fieldName, float value)
    {
        typeof(UIButtonPressScaleFeedback)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(feedback, value);
    }

    private static void InvokeInstallerUpdate(UIButtonPressScaleAutoInstaller installer)
    {
        typeof(UIButtonPressScaleAutoInstaller)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(installer, null);
    }

    private static void InvokeFeedbackOnDisable(UIButtonPressScaleFeedback feedback)
    {
        typeof(UIButtonPressScaleFeedback)
            .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(feedback, null);
    }

    private static void AssertScale(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
    }
}
