using System.Collections.Generic;
using System.Reflection;
using Kernel.GameState;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class Player2DMovementControllerTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        StatusController.ClearStatus();
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
    public void TryStartDash_ConsumesStaminaAndEndsAfterDuration()
    {
        Player2DMovementController controller = CreateController(out _);
        SetPrivateField(controller, "dashDistance", 2.2f);
        SetPrivateField(controller, "dashDuration", 0.16f);
        SetPrivateField(controller, "dashStaminaCost", 25f);
        SetPrivateField(controller, "staminaMax", 100f);
        SetPrivateField(controller, "currentStamina", 100f);
        SetPrivateField(controller, "lastMoveDirection", Vector2.up);

        bool started = InvokePrivateMethod<bool>(controller, "TryStartDash");
        Vector2 firstVelocity = InvokeResolveVelocity(controller, 0.08f, out bool firstDashing);
        Vector2 secondVelocity = InvokeResolveVelocity(controller, 0.08f, out bool secondDashing);
        Vector2 finalVelocity = InvokeResolveVelocity(controller, 0.08f, out bool finalDashing);

        Assert.That(started, Is.True);
        Assert.That(GetPrivateField<float>(controller, "currentStamina"), Is.EqualTo(75f).Within(0.0001f));
        Assert.That(firstDashing, Is.True);
        Assert.That(secondDashing, Is.True);
        Assert.That(finalDashing, Is.False);
        Assert.That(firstVelocity.y, Is.EqualTo(13.75f).Within(0.0001f));
        Assert.That(secondVelocity.y, Is.EqualTo(13.75f).Within(0.0001f));
        Assert.That(finalVelocity, Is.EqualTo(Vector2.zero).Using(Vector2EqualityComparerWithTolerance(0.0001f)));
    }

    [Test]
    public void Update_WhenGameplayInputIsBlocked_StopsBodyVelocity()
    {
        Player2DMovementController controller = CreateController(out Rigidbody2D body);
        body.linearVelocity = new Vector2(4f, 2f);
        SetPrivateField(controller, "lastAppliedVelocity", body.linearVelocity);
        SetPrivateField(controller, "dashRemainingTime", 0.1f);

        StatusController.AddStatus(StatusList.PausedStatus);
        InvokePrivateVoidMethod(controller, "Update");

        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero).Using(Vector2EqualityComparerWithTolerance(0.0001f)));
        Assert.That(controller.TryGetMotion(out Vector2 velocity, out bool isDashing), Is.False);
        Assert.That(velocity, Is.EqualTo(Vector2.zero).Using(Vector2EqualityComparerWithTolerance(0.0001f)));
        Assert.That(isDashing, Is.False);
    }

    [Test]
    public void Update_ByDefault_DoesNotRotateFacingPivotTowardsMouse()
    {
        Mouse testMouse = InputSystem.AddDevice<Mouse>();
        try
        {
            Player2DMovementController controller = CreateController(out _);
            GameObject cameraObject = CreateGameObject("Facing Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Quaternion initialRotation = Quaternion.Euler(0f, 0f, 37f);
            controller.transform.rotation = initialRotation;
            SetPrivateField(controller, "targetCamera", camera);
            SetPrivateField(controller, "facingPivot", controller.transform);
            QueueMousePosition(testMouse, new Vector2(900f, 500f));

            InvokePrivateVoidMethod(controller, "Update");

            Assert.That(GetPrivateField<bool>(controller, "rotateTowardsMouse"), Is.False);
            Assert.That(Quaternion.Angle(initialRotation, controller.transform.rotation), Is.LessThan(0.0001f));
        }
        finally
        {
            InputSystem.RemoveDevice(testMouse);
        }
    }

    private Player2DMovementController CreateController(out Rigidbody2D body)
    {
        GameObject gameObject = CreateGameObject("Player2D");
        body = gameObject.AddComponent<Rigidbody2D>();
        gameObject.AddComponent<CircleCollider2D>();
        Player2DMovementController controller = gameObject.AddComponent<Player2DMovementController>();
        SetPrivateField(controller, "body", body);
        InvokePrivateVoidMethod(controller, "Awake");
        return controller;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Vector2 InvokeResolveVelocity(Player2DMovementController controller, float deltaTime, out bool isDashing)
    {
        MethodInfo method = FindInstanceMethod(controller.GetType(), "ResolveVelocity");
        Assert.That(method, Is.Not.Null);
        object[] args = { deltaTime, null };
        Vector2 velocity = (Vector2)method.Invoke(controller, args);
        isDashing = (bool)args[1];
        return velocity;
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return (T)method.Invoke(target, args);
    }

    private static void InvokePrivateVoidMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static void QueueMousePosition(Mouse mouse, Vector2 position)
    {
        Assert.That(mouse, Is.Not.Null);
        mouse.MakeCurrent();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        InputSystem.Update();
        Assert.That(Mouse.current, Is.SameAs(mouse));
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

    private static MethodInfo FindInstanceMethod(System.Type type, string methodName)
    {
        while (type != null)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static IEqualityComparer<Vector2> Vector2EqualityComparerWithTolerance(float tolerance)
    {
        return new Vector2EqualityComparer(tolerance);
    }

    private sealed class Vector2EqualityComparer : IEqualityComparer<Vector2>
    {
        private readonly float tolerance;

        public Vector2EqualityComparer(float tolerance)
        {
            this.tolerance = tolerance;
        }

        public bool Equals(Vector2 expected, Vector2 actual)
        {
            return (expected - actual).sqrMagnitude <= tolerance * tolerance;
        }

        public int GetHashCode(Vector2 obj)
        {
            return obj.GetHashCode();
        }
    }
}
