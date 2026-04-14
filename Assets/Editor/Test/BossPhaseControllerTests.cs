using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Vocalith.EventSystem;

public sealed class BossPhaseControllerTests
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
    public void Damaged_CrossesThreshold_SwitchesPhaseOnlyOnceAndPublishesEvent()
    {
        GameObject bossObject = CreateGameObject("Boss");
        BaseCharEnemyNorm1 boss = bossObject.AddComponent<BaseCharEnemyNorm1>();
        EnemyDefinitionBinder binder = bossObject.AddComponent<EnemyDefinitionBinder>();
        BossPhaseController phaseController = bossObject.AddComponent<BossPhaseController>();
        SetEnemyHealth(boss, 100f);

        EnemyDefinition phaseOneDefinition = CreateDefinition("boss_phase_1", "\u58f9");
        EnemyDefinition phaseTwoDefinition = CreateDefinition("boss_phase_2", "\u8d30");
        Assert.That(binder.ApplyDefinition(phaseOneDefinition), Is.True);

        Assert.That(phaseController.TryConfigure(boss, binder, phaseTwoDefinition, 0.5f), Is.True);

        int phaseChangeCount = 0;
        BossPhaseChangedEvent lastPhaseEvent = default;
        System.IDisposable subscription = EventManager.eventBus.Subscribe<BossPhaseChangedEvent>(evt =>
        {
            phaseChangeCount++;
            lastPhaseEvent = evt;
        });

        try
        {
            Assert.That(boss.TryApplyDamage(40f, out _, out _), Is.True);
            Assert.That(boss.Definition, Is.SameAs(phaseOneDefinition));
            Assert.That(phaseChangeCount, Is.EqualTo(0));

            Assert.That(boss.TryApplyDamage(10f, out _, out _), Is.True);
            Assert.That(boss.Definition, Is.SameAs(phaseTwoDefinition));
            Assert.That(phaseChangeCount, Is.EqualTo(1));
            Assert.That(lastPhaseEvent.boss, Is.SameAs(boss));
            Assert.That(lastPhaseEvent.phaseIndex, Is.EqualTo(2));
            Assert.That(lastPhaseEvent.phaseDisplayName, Is.EqualTo(phaseTwoDefinition.DisplayName));

            Assert.That(boss.TryApplyDamage(10f, out _, out _), Is.True);
            Assert.That(phaseChangeCount, Is.EqualTo(1));
        }
        finally
        {
            subscription.Dispose();
        }
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private EnemyDefinition CreateDefinition(string enemyId, string glyphText)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = enemyId;
        createdObjects.Add(definition);

        SetPrivateField(definition, "enemyId", enemyId);
        SetPrivateField(definition, "displayName", enemyId);
        SetPrivateField(definition, "movementKind", EnemyMovementKind.None);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.None);
        SetPrivateField(definition, "visual", new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = glyphText,
            glyphColor = Color.white,
        });
        return definition;
    }

    private static void SetEnemyHealth(BaseCharEnemyNorm1 enemy, float health)
    {
        SetPrivateField(enemy, "health", health);
        SetPrivateField(enemy, "currentHealth", health);
        SetPrivateField(enemy, "hasInitializedHealth", true);
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
