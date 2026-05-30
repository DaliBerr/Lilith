using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Player2DIsometricCameraTests
{
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
    public void SnapToTarget_ConfiguresOrthographicCameraAndFollowsTargetXY()
    {
        GameObject cameraObject = CreateGameObject("Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        Player2DIsometricCamera followCamera = cameraObject.AddComponent<Player2DIsometricCamera>();
        GameObject target = CreateGameObject("Target");
        target.transform.position = new Vector3(2f, 3f, 0f);

        followCamera.SetTarget(target.transform);
        followCamera.SnapToTarget();

        Assert.That(camera.orthographic, Is.True);
        Assert.That(camera.orthographicSize, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(cameraObject.transform.position.x, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(cameraObject.transform.position.y, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.0001f));
        Assert.That(followCamera.Target, Is.EqualTo(target.transform));
    }

    [Test]
    public void LateUpdate_AppliesRequestedScreenShakeAfterOrthographicFollow()
    {
        GameObject cameraObject = CreateGameObject("Camera");
        cameraObject.AddComponent<Camera>();
        Player2DIsometricCamera followCamera = cameraObject.AddComponent<Player2DIsometricCamera>();
        GameObject target = CreateGameObject("Target");
        target.transform.position = new Vector3(2f, 3f, 0f);

        followCamera.SetTarget(target.transform);
        PlayerPrefs.DeleteKey(ScreenShakeSettings.PlayerPrefsKey);
        followCamera.RequestScreenShake(0.5f, 0.5f, 1f);
        InvokePrivateVoid(followCamera, "LateUpdate");

        Vector3 expectedPosition = new(2f, 3f, -10f);
        float shakeDistance = Vector3.Distance(cameraObject.transform.position, expectedPosition);

        Assert.That(shakeDistance, Is.GreaterThan(0.001f));
        Assert.That(shakeDistance, Is.LessThan(1f));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InvokePrivateVoid(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, null);
    }
}
