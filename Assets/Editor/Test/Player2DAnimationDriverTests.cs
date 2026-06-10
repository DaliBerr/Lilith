using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class Player2DAnimationDriverTests
{
    private const string SharedControllerPath = "Assets/Animation/SharedCharacter/Shared_4Dir.controller";

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
        try
        {
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            gameObject.AddComponent<CircleCollider2D>();
            Player2DMovementController movementController = gameObject.AddComponent<Player2DMovementController>();
            Animator animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = LoadSharedController();
            Character2DAnimatorDriver characterDriver = gameObject.AddComponent<Character2DAnimatorDriver>();
            Player2DMovementAnimatorDriver movementDriver = gameObject.AddComponent<Player2DMovementAnimatorDriver>();

            SetPrivateField(movementController, "body", body);
            InvokePrivateVoidMethod(movementController, "Awake");
            InvokePrivateVoidMethod(characterDriver, "Awake");
            InvokePrivateVoidMethod(movementDriver, "Awake");
            SetPrivateField(movementController, "lastAppliedVelocity", new Vector2(0f, 2f));

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
        }
    }

    private static RuntimeAnimatorController LoadSharedController()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SharedControllerPath);
        Assert.NotNull(controller);
        return controller;
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
