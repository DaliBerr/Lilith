using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;

public sealed class WaveDefinitionTests
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
    public void OnValidate_SanitizesTokenDropsAndPreservesEmptyInspectorSlots()
    {
        WaveDefinition waveDefinition = ScriptableObject.CreateInstance<WaveDefinition>();
        createdObjects.Add(waveDefinition);

        CoreTokenData keptLowChanceToken = CreateToken<CoreTokenData>("keep_low", "Keep Low");
        CoreTokenData keptHighChanceToken = CreateToken<CoreTokenData>("keep_high", "Keep High");

        WaveEnemySpawnEntry entry = new(
            "CharEnemy",
            2,
            new EnemyWaveConfig(
                40f,
                80f,
                10f,
                0.5f,
                3f,
                new[]
                {
                    new EnemyBulletTokenDropEntry(null, 0.5f),
                    new EnemyBulletTokenDropEntry(keptLowChanceToken, -0.3f),
                    new EnemyBulletTokenDropEntry(keptHighChanceToken, 1.8f),
                }));

        SetPrivateField(waveDefinition, "enemySpawns", new List<WaveEnemySpawnEntry> { entry });
        InvokePrivateMethod(waveDefinition, "OnValidate");

        Assert.That(waveDefinition.TryGetSpawnEntryAt(0, out WaveEnemySpawnEntry sanitizedEntry), Is.True);
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops, Is.Not.Null);
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops, Has.Count.EqualTo(3));
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops[0].token, Is.Null);
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops[0].dropChance, Is.EqualTo(0.5f));
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops[1].token, Is.SameAs(keptLowChanceToken));
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops[1].dropChance, Is.EqualTo(0f));
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops[2].token, Is.SameAs(keptHighChanceToken));
        Assert.That(sanitizedEntry.enemyConfig.tokenDrops[2].dropChance, Is.EqualTo(1f));
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
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
}
