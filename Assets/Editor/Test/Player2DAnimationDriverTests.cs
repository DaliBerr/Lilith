using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class Player2DAnimationDriverTests
{
    private const string SharedControllerPath = "Assets/Animation/SharedCharacter/Shared_4Dir.controller";
    private const string FormalControllerPath = "Assets/Art/Player/PlayerAnimator/PlayerAnimator.controller";

    private Mouse testMouse;

    [TearDown]
    public void TearDown()
    {
        if (testMouse != null)
        {
            InputSystem.RemoveDevice(testMouse);
            testMouse = null;
        }
    }

    [Test]
    public void Character2DAnimatorDriver_SetMovementThenIdle_KeepsLastFacing()
    {
        GameObject gameObject = new("Character2DAnimatorDriverTest");
        try
        {
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadSharedController();
            Character2DAnimatorDriver driver = gameObject.AddComponent<Character2DAnimatorDriver>();
            InvokePrivateVoidMethod(driver, "Awake");

            driver.SetMovement(new Vector2(3f, 0f));

            Assert.That(driver.IsMoving, Is.True);
            Assert.That(driver.FacingDirection.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("MoveX"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(3f).Within(0.0001f));
            Assert.That(animator.GetBool("IsMoving"), Is.True);

            driver.SetIdle();

            Assert.That(driver.IsMoving, Is.False);
            Assert.That(driver.FacingDirection.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("MoveX"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(animator.GetBool("IsMoving"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void Player2DMovementAnimatorDriver_UsesMotionAndPreservesIdleFacing()
    {
        GameObject gameObject = new("Player2DMovementAnimatorDriverTest");
        GameObject cameraObject = new("Player2DMovementAnimatorDriverCamera");
        try
        {
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<CircleCollider2D>();
            Player2DMovementController movementController = gameObject.AddComponent<Player2DMovementController>();
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadSharedController();
            Character2DAnimatorDriver characterDriver = gameObject.AddComponent<Character2DAnimatorDriver>();
            Player2DMovementAnimatorDriver movementDriver = gameObject.AddComponent<Player2DMovementAnimatorDriver>();
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            SetPrivateField(movementController, "body", body);
            SetPrivateField(movementController, "targetCamera", camera);
            InvokePrivateVoidMethod(movementController, "Awake");
            InvokePrivateVoidMethod(characterDriver, "Awake");
            InvokePrivateVoidMethod(movementDriver, "Awake");
            SetPrivateField(movementController, "lastAppliedVelocity", new Vector2(0f, 2f));
            QueueMousePosition(new Vector2(500f, 900f));

            InvokePrivateVoidMethod(movementDriver, "Update");

            Assert.That(characterDriver.IsMoving, Is.True);
            Assert.That(characterDriver.FacingDirection.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(animator.GetBool("IsMoving"), Is.True);

            SetPrivateField(movementController, "lastAppliedVelocity", Vector2.zero);
            SetPrivateField(movementController, "wasDashingLastStep", false);
            SetPrivateField(movementController, "dashRemainingTime", 0f);

            InvokePrivateVoidMethod(movementDriver, "Update");

            Assert.That(characterDriver.IsMoving, Is.False);
            Assert.That(characterDriver.FacingDirection.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(animator.GetBool("IsMoving"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void Player2DMovementAnimatorDriver_UsesMouseFacingForFormalAnimatorWhileMoving()
    {
        GameObject gameObject = new("Player2DFormalAnimatorDriverTest");
        GameObject cameraObject = new("Player2DFormalAnimatorCamera");
        try
        {
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<CircleCollider2D>();
            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            Player2DMovementController movementController = gameObject.AddComponent<Player2DMovementController>();
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadFormalController();
            Character2DAnimatorDriver characterDriver = gameObject.AddComponent<Character2DAnimatorDriver>();
            Player2DMovementAnimatorDriver movementDriver = gameObject.AddComponent<Player2DMovementAnimatorDriver>();
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            SetPrivateField(movementController, "body", body);
            SetPrivateField(movementController, "targetCamera", camera);
            InvokePrivateVoidMethod(movementController, "Awake");
            InvokePrivateVoidMethod(characterDriver, "Awake");
            InvokePrivateVoidMethod(movementDriver, "Awake");
            SetPrivateField(movementController, "lastAppliedVelocity", new Vector2(-2f, 0f));
            QueueMousePosition(new Vector2(900f, 500f));

            InvokePrivateVoidMethod(movementDriver, "Update");

            Assert.That(characterDriver.IsMoving, Is.True);
            Assert.That(characterDriver.FacingDirection.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(characterDriver.Movement.x, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(animator.GetBool("IsMoving"), Is.True);
            Assert.That(animator.GetBool("IsFacingBack"), Is.False);
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(spriteRenderer.flipX, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void Player2DMovementAnimatorDriver_FlipsFormalAnimatorWhenMouseFacesLeft()
    {
        GameObject gameObject = new("Player2DFormalAnimatorLeftFlipTest");
        GameObject cameraObject = new("Player2DFormalAnimatorLeftFlipCamera");
        try
        {
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<CircleCollider2D>();
            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            Player2DMovementController movementController = gameObject.AddComponent<Player2DMovementController>();
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadFormalController();
            Character2DAnimatorDriver characterDriver = gameObject.AddComponent<Character2DAnimatorDriver>();
            Player2DMovementAnimatorDriver movementDriver = gameObject.AddComponent<Player2DMovementAnimatorDriver>();
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            SetPrivateField(movementController, "body", body);
            SetPrivateField(movementController, "targetCamera", camera);
            InvokePrivateVoidMethod(movementController, "Awake");
            InvokePrivateVoidMethod(characterDriver, "Awake");
            InvokePrivateVoidMethod(movementDriver, "Awake");
            SetPrivateField(movementController, "lastAppliedVelocity", new Vector2(2f, 0f));
            QueueMousePosition(new Vector2(100f, 500f));

            InvokePrivateVoidMethod(movementDriver, "Update");

            Assert.That(characterDriver.IsMoving, Is.True);
            Assert.That(characterDriver.FacingDirection.x, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(characterDriver.Movement.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(animator.GetBool("IsMoving"), Is.True);
            Assert.That(animator.GetBool("IsFacingBack"), Is.False);
            Assert.That(spriteRenderer.flipX, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void Player2DMovementAnimatorDriver_MouseAboveAndBelowTogglesFormalFacingBack()
    {
        GameObject gameObject = new("Player2DFormalFacingBackTest");
        GameObject cameraObject = new("Player2DFormalFacingBackCamera");
        try
        {
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<CircleCollider2D>();
            gameObject.AddComponent<SpriteRenderer>();
            Player2DMovementController movementController = gameObject.AddComponent<Player2DMovementController>();
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadFormalController();
            Character2DAnimatorDriver characterDriver = gameObject.AddComponent<Character2DAnimatorDriver>();
            Player2DMovementAnimatorDriver movementDriver = gameObject.AddComponent<Player2DMovementAnimatorDriver>();
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            SetPrivateField(movementController, "body", body);
            SetPrivateField(movementController, "targetCamera", camera);
            InvokePrivateVoidMethod(movementController, "Awake");
            InvokePrivateVoidMethod(characterDriver, "Awake");
            InvokePrivateVoidMethod(movementDriver, "Awake");

            QueueMousePosition(new Vector2(500f, 900f));
            InvokePrivateVoidMethod(movementDriver, "Update");

            Assert.That(characterDriver.FacingDirection.y, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(animator.GetBool("IsFacingBack"), Is.True);

            QueueMousePosition(new Vector2(500f, 100f));
            InvokePrivateVoidMethod(movementDriver, "Update");

            Assert.That(characterDriver.FacingDirection.y, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(animator.GetBool("IsFacingBack"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void Character2DAnimatorDriver_DodgeTriggerIsGatedByDashStart()
    {
        GameObject gameObject = new("Character2DDodgeGateTest");
        try
        {
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadFormalController();
            Character2DAnimatorDriver driver = gameObject.AddComponent<Character2DAnimatorDriver>();
            InvokePrivateVoidMethod(driver, "Awake");

            driver.SetDashing(true);

            Assert.That(GetPrivateField<bool>(driver, "wasDashing"), Is.True);

            driver.SetDashing(true);

            Assert.That(GetPrivateField<bool>(driver, "wasDashing"), Is.True);

            driver.SetDashing(false);

            Assert.That(GetPrivateField<bool>(driver, "wasDashing"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static RuntimeAnimatorController LoadSharedController()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SharedControllerPath);
        Assert.NotNull(controller);
        return controller;
    }

    private static RuntimeAnimatorController LoadFormalController()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FormalControllerPath);
        Assert.NotNull(controller);
        return controller;
    }

    private void QueueMousePosition(Vector2 position)
    {
        testMouse ??= InputSystem.AddDevice<Mouse>();
        testMouse.MakeCurrent();
        InputSystem.QueueStateEvent(testMouse, new MouseState { position = position });
        InputSystem.Update();
        Assert.That(Mouse.current, Is.SameAs(testMouse));
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

    private static void InvokePrivateVoidMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, args);
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
}
