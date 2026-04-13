using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyStatusEffectControllerTests
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
    public void RegisterFireHit_TriggersBurnOnThirdHitAndResetsCounter()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out EnemyStatusEffectController controller);

        Assert.That(controller.RegisterFireHit(3, 3f, 2f), Is.False);
        Assert.That(controller.RegisterFireHit(3, 3f, 2f), Is.False);
        Assert.That(controller.FireHitCount, Is.EqualTo(2));

        bool triggered = controller.RegisterFireHit(3, 3f, 2f);
        InvokePrivateMethod(controller, "TickBurn", 1f);

        Assert.That(triggered, Is.True);
        Assert.That(controller.FireHitCount, Is.EqualTo(0));
        Assert.That(controller.IsBurning, Is.True);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(17f).Within(0.0001f));
    }

    [Test]
    public void ApplySlow_ChangesSpeedMultiplierAndRecoversAfterDuration()
    {
        CreateEnemy(out EnemyStatusEffectController controller);

        bool applied = controller.ApplySlow(0.25f, 1.5f);
        Assert.That(applied, Is.True);
        Assert.That(controller.MovementSpeedMultiplier, Is.EqualTo(0.75f).Within(0.0001f));

        InvokePrivateMethod(controller, "TickSlow", 1.5f);

        Assert.That(controller.IsSlowed, Is.False);
        Assert.That(controller.MovementSpeedMultiplier, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void RegisterControlHit_TriggersStunAndRecoversAfterDuration()
    {
        CreateEnemy(out EnemyStatusEffectController controller);

        for (int i = 0; i < 4; i++)
        {
            Assert.That(controller.RegisterControlHit(5, 1f), Is.False);
        }

        bool triggered = controller.RegisterControlHit(5, 1f);
        Assert.That(triggered, Is.True);
        Assert.That(controller.ControlHitCount, Is.EqualTo(0));
        Assert.That(controller.IsStunned, Is.True);
        Assert.That(controller.CanMove, Is.False);
        Assert.That(controller.CanAct, Is.False);

        InvokePrivateMethod(controller, "TickStun", 1f);

        Assert.That(controller.IsStunned, Is.False);
        Assert.That(controller.CanMove, Is.True);
        Assert.That(controller.CanAct, Is.True);
    }

    private BaseCharEnemyNorm1 CreateEnemy(out EnemyStatusEffectController controller)
    {
        GameObject enemyObject = new("Enemy");
        createdObjects.Add(enemyObject);
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        controller = enemyObject.AddComponent<EnemyStatusEffectController>();
        SetPrivateField(enemy, "health", 20f);
        SetPrivateField(enemy, "currentHealth", 20f);
        SetPrivateField(enemy, "hasInitializedHealth", true);
        return enemy;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
