using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerPlaneMovementTests
{
    private readonly List<Object> createdObjects = new();

    [Test]
    public void TryStartDash_ConsumesStaminaAndUsesCachedMovementDirection()
    {
        DestroyAllCameras();

        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        PlayerPlaneMovement movement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(movement, "dashDistance", 6f);
        SetPrivateField(movement, "dashDuration", 0.2f);
        SetPrivateField(movement, "dashStaminaCost", 25f);
        SetPrivateField(movement, "staminaMax", 100f);
        SetPrivateField(movement, "currentStamina", 100f);
        SetPrivateField(movement, "lastMoveDirection", Vector3.forward);

        bool started = InvokePrivateMethod<bool>(movement, "TryStartDash");

        Assert.That(started, Is.True);
        Assert.That(GetPrivateField<float>(movement, "currentStamina"), Is.EqualTo(75f).Within(0.0001f));
        Assert.That(GetPrivateField<float>(movement, "dashRemainingDistance"), Is.EqualTo(6f).Within(0.0001f));
        Assert.That(GetPrivateField<Vector3>(movement, "dashDirection"), Is.EqualTo(Vector3.forward).Using(Vector3EqualityComparerWithTolerance(0.0001f)));
    }

    [Test]
    public void GetMovementDelta_ConsumesDashDistanceAndEndsDash()
    {
        DestroyAllCameras();

        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        PlayerPlaneMovement movement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(movement, "dashDistance", 6f);
        SetPrivateField(movement, "dashDuration", 0.2f);
        SetPrivateField(movement, "dashStaminaCost", 25f);
        SetPrivateField(movement, "staminaMax", 100f);
        SetPrivateField(movement, "currentStamina", 100f);
        SetPrivateField(movement, "lastMoveDirection", Vector3.forward);

        bool started = InvokePrivateMethod<bool>(movement, "TryStartDash");
        Assert.That(started, Is.True);

        Vector3 dashDelta = InvokePrivateMethod<Vector3>(movement, "GetMovementDelta", 0.2f);
        Vector3 fallbackDelta = InvokePrivateMethod<Vector3>(movement, "GetMovementDelta", 0.2f);

        Assert.That(dashDelta, Is.EqualTo(new Vector3(0f, 0f, 6f)).Using(Vector3EqualityComparerWithTolerance(0.0001f)));
        Assert.That(fallbackDelta, Is.EqualTo(Vector3.zero).Using(Vector3EqualityComparerWithTolerance(0.0001f)));
        Assert.That(GetPrivateField<float>(movement, "dashRemainingDistance"), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void TryStartDash_RejectsWhenStaminaIsInsufficient()
    {
        DestroyAllCameras();

        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        PlayerPlaneMovement movement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(movement, "dashDistance", 6f);
        SetPrivateField(movement, "dashDuration", 0.2f);
        SetPrivateField(movement, "dashStaminaCost", 25f);
        SetPrivateField(movement, "staminaMax", 100f);
        SetPrivateField(movement, "currentStamina", 10f);

        bool started = InvokePrivateMethod<bool>(movement, "TryStartDash");

        Assert.That(started, Is.False);
        Assert.That(GetPrivateField<float>(movement, "currentStamina"), Is.EqualTo(10f).Within(0.0001f));
        Assert.That(GetPrivateField<float>(movement, "dashRemainingDistance"), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ResolveKinematicMovementDelta_BlocksBeforeWallUsingSkinWidth()
    {
        PlayerPlaneMovement movement = CreateMovementSubject(
            out _,
            out _,
            out _);

        GameObject wallObject = CreateGameObject("Wall");
        wallObject.transform.position = new Vector3(2f, 0f, 0f);
        BoxCollider wallCollider = wallObject.AddComponent<BoxCollider>();
        wallCollider.size = new Vector3(1f, 4f, 8f);

        SetPrivateField(movement, "movementSkinWidth", 0.1f);
        SetPrivateField(movement, "movementCollisionMask", new LayerMask { value = 1 << 0 });
        Physics.SyncTransforms();

        Vector3 resolvedDelta = InvokePrivateMethod<Vector3>(movement, "ResolveKinematicMovementDelta", new Vector3(2f, 0f, 0f));

        Assert.That(resolvedDelta.x, Is.EqualTo(0.9f).Within(0.05f));
        Assert.That(Mathf.Abs(resolvedDelta.z), Is.LessThan(0.001f));
    }

    [Test]
    public void ResolveKinematicMovementDelta_SlidesAlongWallForDashSizedDelta()
    {
        PlayerPlaneMovement movement = CreateMovementSubject(
            out _,
            out _,
            out _);

        GameObject wallObject = CreateGameObject("Wall");
        wallObject.transform.position = new Vector3(2f, 0f, 0f);
        BoxCollider wallCollider = wallObject.AddComponent<BoxCollider>();
        wallCollider.size = new Vector3(1f, 4f, 8f);

        SetPrivateField(movement, "movementSkinWidth", 0.1f);
        SetPrivateField(movement, "movementCollisionMask", new LayerMask { value = 1 << 0 });
        Physics.SyncTransforms();

        Vector3 resolvedDelta = InvokePrivateMethod<Vector3>(movement, "ResolveKinematicMovementDelta", new Vector3(4f, 0f, 4f));

        Assert.That(resolvedDelta.x, Is.LessThan(1.5f));
        Assert.That(resolvedDelta.z, Is.GreaterThan(3f));
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
        Assert.That(playerObject.transform.position.y, Is.EqualTo(18.5f));
    }

    private MapGridAuthoring CreateMapAuthoring(float planeY)
    {
        GameObject mapRoot = CreateGameObject("MapRoot");
        mapRoot.transform.position = new Vector3(0f, planeY, 0f);
        return mapRoot.AddComponent<MapGridAuthoring>();
    }

    private PlayerPlaneMovement CreateMovementSubject(out GameObject playerObject, out BoxCollider collider, out Rigidbody rigidbody)
    {
        playerObject = CreateGameObject("Player");
        playerObject.transform.position = Vector3.zero;
        rigidbody = playerObject.AddComponent<Rigidbody>();
        collider = playerObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 2f, 1f);
        PlayerPlaneMovement movement = playerObject.AddComponent<PlayerPlaneMovement>();
        SetPrivateField(movement, "targetRigidbody", rigidbody);
        SetPrivateField(movement, "groundingReferenceCollider", collider);
        Physics.SyncTransforms();
        return movement;
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

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return (T)method.Invoke(target, args);
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

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        return (T)field.GetValue(target);
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

    private static void DestroyAllCameras()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                Object.DestroyImmediate(cameras[i].gameObject);
            }
        }
    }

    private static IEqualityComparer<Vector3> Vector3EqualityComparerWithTolerance(float tolerance)
    {
        return new Vector3EqualityComparer(tolerance);
    }

    private sealed class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        private readonly float tolerance;

        public Vector3EqualityComparer(float tolerance)
        {
            this.tolerance = tolerance;
        }

        public bool Equals(Vector3 expected, Vector3 actual)
        {
            return (expected - actual).sqrMagnitude <= tolerance * tolerance;
        }

        public int GetHashCode(Vector3 obj)
        {
            return obj.GetHashCode();
        }
    }
}
