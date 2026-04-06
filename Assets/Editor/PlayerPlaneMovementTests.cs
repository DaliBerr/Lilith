using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerPlaneMovementTests
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
    public void TryProjectRayOntoGameplayPlane_UsesMapPlaneHeightInsteadOfPlayerRootHeight()
    {
        MapGridAuthoring authoring = CreateMapAuthoring(12f);
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 40f, 0f);
        PlayerPlaneMovement movement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(movement, "targetMapGrid", authoring);

        Ray ray = new(new Vector3(6f, 50f, 8f), Vector3.down);

        bool success = InvokePrivateMethodWithOutVector3(movement, "TryProjectRayOntoGameplayPlane", ray, out Vector3 worldPoint);

        Assert.That(success, Is.True);
        Assert.That(worldPoint.y, Is.EqualTo(12f));
    }

    [Test]
    public void TrySnapToGameplayPlane_RaisesPlayerRootByReferenceColliderHeight()
    {
        MapGridAuthoring authoring = CreateMapAuthoring(10f);
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = Vector3.zero;
        Rigidbody rigidbody = playerObject.AddComponent<Rigidbody>();
        BoxCollider collider = playerObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(15f, 15f, 15f);
        PlayerPlaneMovement movement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(movement, "targetMapGrid", authoring);
        SetPrivateField(movement, "targetRigidbody", rigidbody);
        SetPrivateField(movement, "groundingReferenceCollider", collider);
        playerObject.transform.position = Vector3.zero;
        rigidbody.position = Vector3.zero;

        bool success = InvokePrivateMethod(movement, "TrySnapToGameplayPlane");

        Assert.That(success, Is.True);
        Assert.That(playerObject.transform.position.y, Is.EqualTo(17.5f));
    }

    private MapGridAuthoring CreateMapAuthoring(float planeY)
    {
        GameObject mapRoot = CreateGameObject("MapRoot");
        mapRoot.transform.position = new Vector3(0f, planeY, 0f);
        return mapRoot.AddComponent<MapGridAuthoring>();
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static bool InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return (bool)method.Invoke(target, null);
    }

    private static bool InvokePrivateMethodWithOutVector3(object target, string methodName, Ray ray, out Vector3 worldPoint)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        object[] args = { ray, null };
        bool success = (bool)method.Invoke(target, args);
        worldPoint = (Vector3)args[1];
        return success;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static FieldInfo FindInstanceField(System.Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }
}
