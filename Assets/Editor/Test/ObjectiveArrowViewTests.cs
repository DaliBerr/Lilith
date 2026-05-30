using System.Collections.Generic;
using System.Reflection;
using Kernel.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ObjectiveArrowViewTests
{
    private readonly List<GameObject> createdObjects = new();
    private readonly List<RenderTexture> createdRenderTextures = new();

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

        for (int i = createdRenderTextures.Count - 1; i >= 0; i--)
        {
            if (createdRenderTextures[i] != null)
            {
                createdRenderTextures[i].Release();
                Object.DestroyImmediate(createdRenderTextures[i]);
            }
        }

        createdObjects.Clear();
        createdRenderTextures.Clear();
    }

    [Test]
    public void BindWithMissingCameraHidesArrow()
    {
        ObjectiveArrowView view = CreateView(null, out _, out RectTransform arrowRect, out _);
        GameObject target = CreateObject("Target");
        arrowRect.gameObject.SetActive(true);

        view.Bind(null, target.transform);
        InvokeLateUpdate(view);

        Assert.That(arrowRect.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void OnscreenTargetPlacesArrowNearCenterAndRotatesTowardIt()
    {
        Camera camera = CreateCamera();
        ObjectiveArrowView view = CreateView(camera, out _, out RectTransform arrowRect, out _);
        GameObject target = CreateObject("Target");
        target.transform.position = new Vector3(4f, 0f, 0f);

        view.Bind(camera, target.transform);
        InvokeLateUpdate(view);

        Assert.That(arrowRect.gameObject.activeSelf, Is.True);
        Assert.That(arrowRect.localPosition.x, Is.EqualTo(96f).Within(1f));
        Assert.That(arrowRect.localPosition.y, Is.EqualTo(0f).Within(1f));
        Assert.That(NormalizeAngle(arrowRect.localEulerAngles.z), Is.EqualTo(0f).Within(1f));
    }

    [Test]
    public void OffscreenTargetClampsArrowInsidePanelAndRotatesTowardTarget()
    {
        Camera camera = CreateCamera();
        ObjectiveArrowView view = CreateView(camera, out _, out RectTransform arrowRect, out _);
        GameObject target = CreateObject("Target");
        target.transform.position = new Vector3(20f, 0f, 0f);

        view.Bind(camera, target.transform);
        InvokeLateUpdate(view);

        Assert.That(arrowRect.gameObject.activeSelf, Is.True);
        Assert.That(arrowRect.localPosition.x, Is.EqualTo(320f).Within(1f));
        Assert.That(arrowRect.localPosition.y, Is.EqualTo(0f).Within(1f));
        Assert.That(NormalizeAngle(arrowRect.localEulerAngles.z), Is.EqualTo(0f).Within(1f));
    }

    [Test]
    public void ClearTargetHidesArrowAndRemovesTargetBinding()
    {
        Camera camera = CreateCamera();
        ObjectiveArrowView view = CreateView(camera, out _, out RectTransform arrowRect, out _);
        GameObject target = CreateObject("Target");

        view.Bind(camera, target.transform);
        view.ClearTarget();

        Assert.That(arrowRect.gameObject.activeSelf, Is.False);
        Assert.That(GetSerializedObjectReference<Transform>(view, "targetTransform"), Is.Null);
    }

    private ObjectiveArrowView CreateView(
        Camera camera,
        out RectTransform panelRect,
        out RectTransform arrowRect,
        out Image arrowImage)
    {
        GameObject canvasObject = CreateUIObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;

        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(800f, 600f);

        GameObject panelObject = CreateUIObject("Arrow Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelRect = (RectTransform)panelObject.transform;
        panelRect.sizeDelta = new Vector2(800f, 600f);

        GameObject arrowObject = CreateUIObject("Arrow");
        arrowObject.transform.SetParent(panelObject.transform, false);
        arrowRect = (RectTransform)arrowObject.transform;
        arrowRect.anchorMin = new Vector2(0f, 1f);
        arrowRect.anchorMax = new Vector2(0f, 1f);
        arrowRect.sizeDelta = new Vector2(64f, 64f);
        arrowImage = arrowObject.AddComponent<Image>();

        ObjectiveArrowView view = panelObject.AddComponent<ObjectiveArrowView>();
        SerializedObject serializedObject = new(view);
        serializedObject.FindProperty("panelRoot").objectReferenceValue = panelRect;
        serializedObject.FindProperty("arrowRect").objectReferenceValue = arrowRect;
        serializedObject.FindProperty("arrowImage").objectReferenceValue = arrowImage;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private Camera CreateCamera()
    {
        GameObject cameraObject = CreateObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderTexture renderTexture = new(800, 600, 16);
        createdRenderTextures.Add(renderTexture);
        camera.targetTexture = renderTexture;
        return camera;
    }

    private static void InvokeLateUpdate(ObjectiveArrowView view)
    {
        MethodInfo lateUpdate = typeof(ObjectiveArrowView).GetMethod(
            "LateUpdate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(lateUpdate, Is.Not.Null);
        lateUpdate.Invoke(view, null);
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private static T GetSerializedObjectReference<T>(Object target, string propertyName)
        where T : Object
    {
        SerializedObject serializedObject = new(target);
        return serializedObject.FindProperty(propertyName).objectReferenceValue as T;
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateUIObject(string name)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
