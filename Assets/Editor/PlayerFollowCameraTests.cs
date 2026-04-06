using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerFollowCameraTests
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
    public void LateUpdate_AppliesPerspectivePoseFromConfiguredAnglesAndDistance()
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(12f, 7.5f, 18f);

        GameObject cameraObject = CreateGameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        PlayerFollowCamera followCamera = cameraObject.AddComponent<PlayerFollowCamera>();

        SetPrivateField(followCamera, "targetPlayer", playerObject.transform);
        SetPrivateField(followCamera, "focusOffset", new Vector3(0f, 8f, 0f));
        SetPrivateField(followCamera, "distance", 260f);
        SetPrivateField(followCamera, "pitch", 55f);
        SetPrivateField(followCamera, "yaw", 35f);
        SetPrivateField(followCamera, "fieldOfView", 35f);
        SetPrivateField(followCamera, "nearClipPlane", 0.3f);
        SetPrivateField(followCamera, "farClipPlane", 4000f);

        InvokePrivateVoid(followCamera, "Awake");
        InvokePrivateVoid(followCamera, "LateUpdate");

        Quaternion expectedRotation = Quaternion.Euler(55f, 35f, 0f);
        Vector3 expectedFocusPoint = playerObject.transform.position + new Vector3(0f, 8f, 0f);
        Vector3 expectedPosition = expectedFocusPoint - (expectedRotation * Vector3.forward * 260f);

        Assert.That(camera.orthographic, Is.False);
        Assert.That(camera.fieldOfView, Is.EqualTo(35f));
        Assert.That(camera.nearClipPlane, Is.EqualTo(0.3f));
        Assert.That(camera.farClipPlane, Is.EqualTo(4000f));
        Assert.That(cameraObject.transform.position, Is.EqualTo(expectedPosition).Using(Vector3EqualityComparerWithTolerance(0.001f)));
        Assert.That(Quaternion.Angle(cameraObject.transform.rotation, expectedRotation), Is.LessThan(0.001f));
        Assert.That(followCamera.FocusWorldPoint, Is.EqualTo(expectedFocusPoint).Using(Vector3EqualityComparerWithTolerance(0.001f)));
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

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static IEqualityComparer<Vector3> Vector3EqualityComparerWithTolerance(float tolerance)
    {
        return new Vector3ToleranceComparer(tolerance);
    }

    private sealed class Vector3ToleranceComparer : IEqualityComparer<Vector3>
    {
        private readonly float tolerance;

        public Vector3ToleranceComparer(float tolerance)
        {
            this.tolerance = tolerance;
        }

        public bool Equals(Vector3 left, Vector3 right)
        {
            return Vector3.Distance(left, right) <= tolerance;
        }

        public int GetHashCode(Vector3 obj)
        {
            return obj.GetHashCode();
        }
    }
}
