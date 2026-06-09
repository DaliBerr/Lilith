using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyMeleeAttackerTests
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
    public void TryPerformAttack_AppliesDamageRespectingCooldownAndRange()
    {
        GameObject playerObject = CreateGameObject("Player");
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 20f);
        SetPrivateField(playerHealth, "currentHealth", 20f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Enemy");
        enemyObject.transform.position = new Vector3(1f, 0f, 0f);
        BaseCharEnemyNorm1 enemyData = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        enemyData.ApplyWaveConfig(new EnemyWaveConfig(50f, 0f, 2f, 0.5f, 3f));
        EnemyMeleeAttacker attacker = enemyObject.AddComponent<EnemyMeleeAttacker>();

        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);

        Assert.That(InvokePrivateMethod<bool>(attacker, "TryPerformAttack", 0f), Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(17f));

        Assert.That(InvokePrivateMethod<bool>(attacker, "TryPerformAttack", 0.25f), Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(17f));

        Assert.That(InvokePrivateMethod<bool>(attacker, "TryPerformAttack", 0.5f), Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(14f));

        enemyObject.transform.position = new Vector3(5f, 0f, 0f);
        Assert.That(InvokePrivateMethod<bool>(attacker, "TryPerformAttack", 1f), Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(14f));

        Assert.That(playerHealth.TryApplyDamage(14f, out _, out bool isDead), Is.True);
        Assert.That(isDead, Is.True);
        Assert.That(InvokePrivateMethod<bool>(attacker, "TryPerformAttack", 2f), Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0f));
    }

    [Test]
    public void Update_ProfileActive_DoesNotRunAutonomousAttack()
    {
        GameObject playerObject = CreateGameObject("Player");
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 20f);
        SetPrivateField(playerHealth, "currentHealth", 20f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Enemy");
        enemyObject.transform.position = new Vector3(1f, 0f, 0f);
        enemyObject.AddComponent<EnemyStatusEffectController>();
        BaseCharEnemyNorm1 enemyData = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        enemyData.ApplyWaveConfig(new EnemyWaveConfig(50f, 0f, 2f, 0.5f, 3f));
        EnemyMeleeAttacker attacker = enemyObject.AddComponent<EnemyMeleeAttacker>();
        EnemyAIController aiController = enemyObject.AddComponent<EnemyAIController>();
        EnemyAIProfile profile = ScriptableObject.CreateInstance<EnemyAIProfile>();
        createdObjects.Add(profile);

        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(aiController.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(aiController.ApplyProfile(profile), Is.True);

        InvokePrivateMethod<object>(attacker, "Update");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(20f));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return (T)method.Invoke(target, args);
    }
}
