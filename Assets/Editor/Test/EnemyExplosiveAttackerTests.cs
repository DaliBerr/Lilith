using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyExplosiveAttackerTests
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
    public void TryTickExplosion_ChargesThenDamagesPlayerAndSelfDestructs()
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 0f, 8f);
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 10f);
        SetPrivateField(playerHealth, "currentHealth", 10f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Exploder");
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyExplosiveAttacker attacker = enemyObject.AddComponent<EnemyExplosiveAttacker>();
        EnemyDefinition definition = CreateExplosiveDefinition(18f, 0.8f);
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(16f, 39f, 18f, 0f, 2f));
        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);

        bool firstTick = InvokePrivateMethod<bool>(attacker, "TryTickExplosion", 0f);
        float healthAfterCharge = playerHealth.CurrentHealth;
        bool secondTick = InvokePrivateMethod<bool>(attacker, "TryTickExplosion", 0.79f);
        bool thirdTick = InvokePrivateMethod<bool>(attacker, "TryTickExplosion", 0.8f);

        Assert.That(firstTick, Is.False);
        Assert.That(healthAfterCharge, Is.EqualTo(10f));
        Assert.That(secondTick, Is.False);
        Assert.That(thirdTick, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(8f));
        Assert.That(enemy == null || enemy.Equals(null), Is.True);
    }

    [Test]
    public void TryTickExplosion_DoesNotDamagePlayerWhoLeavesExplosionRadius()
    {
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 0f, 8f);
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        SetPrivateField(playerHealth, "maxHealth", 10f);
        SetPrivateField(playerHealth, "currentHealth", 10f);
        SetPrivateField(playerHealth, "hasInitializedHealth", true);

        GameObject enemyObject = CreateGameObject("Exploder");
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyExplosiveAttacker attacker = enemyObject.AddComponent<EnemyExplosiveAttacker>();
        EnemyDefinition definition = CreateExplosiveDefinition(18f, 0.8f);
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(16f, 39f, 18f, 0f, 2f));
        Assert.That(attacker.TrySetTarget(playerObject.transform), Is.True);

        bool firstTick = InvokePrivateMethod<bool>(attacker, "TryTickExplosion", 0f);
        playerObject.transform.position = new Vector3(0f, 0f, 40f);
        bool secondTick = InvokePrivateMethod<bool>(attacker, "TryTickExplosion", 0.8f);

        Assert.That(firstTick, Is.False);
        Assert.That(secondTick, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(10f));
        Assert.That(enemy == null || enemy.Equals(null), Is.True);
    }

    private EnemyDefinition CreateExplosiveDefinition(float explosionRadius, float windupSeconds)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", "Exploder");
        SetPrivateField(definition, "displayName", "Exploder");
        SetPrivateField(definition, "movementKind", EnemyMovementKind.ChaseTarget);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.ProximityExplosion);
        SetPrivateField(definition, "explosiveAttack", new EnemyDefinition.ExplosiveAttackDefinition
        {
            explosionRadius = explosionRadius,
            windupSeconds = windupSeconds,
        });
        return definition;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        return (T)method.Invoke(target, args);
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
