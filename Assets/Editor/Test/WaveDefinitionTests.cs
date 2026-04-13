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

        EnemyDefinition enemyDefinition = CreateEnemyDefinition("Norm1");
        CoreTokenData keptLowChanceToken = CreateToken<CoreTokenData>("keep_low", "Keep Low");
        CoreTokenData keptHighChanceToken = CreateToken<CoreTokenData>("keep_high", "Keep High");

        WaveEnemySpawnEntry entry = new(
            enemyDefinition,
            2,
            new[]
            {
                new EnemyBulletTokenDropEntry(null, 0.5f) { dropCount = 0 },
                new EnemyBulletTokenDropEntry(keptLowChanceToken, -0.3f) { dropCount = -2 },
                new EnemyBulletTokenDropEntry(keptHighChanceToken, 1.8f) { dropCount = 4 },
            });

        SetPrivateField(waveDefinition, "enemySpawns", new List<WaveEnemySpawnEntry> { entry });
        InvokePrivateMethod(waveDefinition, "OnValidate");

        Assert.That(waveDefinition.TryGetSpawnEntryAt(0, out WaveEnemySpawnEntry sanitizedEntry), Is.True);
        Assert.That(sanitizedEntry.enemyDefinition, Is.SameAs(enemyDefinition));
        Assert.That(sanitizedEntry.tokenDrops, Is.Not.Null);
        Assert.That(sanitizedEntry.tokenDrops, Has.Count.EqualTo(3));
        Assert.That(sanitizedEntry.tokenDrops[0].token, Is.Null);
        Assert.That(sanitizedEntry.tokenDrops[0].dropChance, Is.EqualTo(0.5f));
        Assert.That(sanitizedEntry.tokenDrops[0].dropCount, Is.EqualTo(1));
        Assert.That(sanitizedEntry.tokenDrops[1].token, Is.SameAs(keptLowChanceToken));
        Assert.That(sanitizedEntry.tokenDrops[1].dropChance, Is.EqualTo(0f));
        Assert.That(sanitizedEntry.tokenDrops[1].dropCount, Is.EqualTo(1));
        Assert.That(sanitizedEntry.tokenDrops[2].token, Is.SameAs(keptHighChanceToken));
        Assert.That(sanitizedEntry.tokenDrops[2].dropChance, Is.EqualTo(1f));
        Assert.That(sanitizedEntry.tokenDrops[2].dropCount, Is.EqualTo(4));
    }

    [Test]
    public void TryGetSpawnEntryAt_IgnoresEntriesWithoutEnemyDefinition()
    {
        WaveDefinition waveDefinition = ScriptableObject.CreateInstance<WaveDefinition>();
        createdObjects.Add(waveDefinition);

        EnemyDefinition validDefinition = CreateEnemyDefinition("Norm1");
        SetPrivateField(waveDefinition, "enemySpawns", new List<WaveEnemySpawnEntry>
        {
            new(null, 3),
            new(validDefinition, 2),
        });
        InvokePrivateMethod(waveDefinition, "OnValidate");

        Assert.That(waveDefinition.TotalSpawnCount, Is.EqualTo(2));
        Assert.That(waveDefinition.TryGetSpawnEntryAt(0, out WaveEnemySpawnEntry entry, out int entryIndex), Is.True);
        Assert.That(entry.enemyDefinition, Is.SameAs(validDefinition));
        Assert.That(entryIndex, Is.EqualTo(1));
    }

    [Test]
    public void PostWaveTokenSelectionPlan_PreservesAssignedReference()
    {
        WaveDefinition waveDefinition = ScriptableObject.CreateInstance<WaveDefinition>();
        CombatEntryTokenSelectionPlan selectionPlan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(waveDefinition);
        createdObjects.Add(selectionPlan);

        SetPrivateField(waveDefinition, "postWaveTokenSelectionPlan", selectionPlan);
        InvokePrivateMethod(waveDefinition, "OnValidate");

        Assert.That(waveDefinition.PostWaveTokenSelectionPlan, Is.SameAs(selectionPlan));
    }

    private EnemyDefinition CreateEnemyDefinition(string enemyId)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = enemyId;
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", enemyId);
        SetPrivateField(definition, "displayName", enemyId);
        return definition;
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
