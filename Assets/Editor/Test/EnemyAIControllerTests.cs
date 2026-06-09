using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyAIControllerTests
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
    public void EnemyAIActionDefinition_Evaluate_UsesDistanceHealthAndFriendCount()
    {
        EnemyAIActionDefinition action = CreateAction(
            EnemyAIActionKind.Movement,
            EnemyMovementKind.KeepDistance,
            EnemyAttackKind.None,
            EnemySkillKind.None,
            weight: 2f,
            cooldownSeconds: 0f,
            actionLockSeconds: 0f,
            new[]
            {
                CreateConsideration(EnemyAIConsiderationInput.DistanceToTarget, 0f, 10f, false, 0f, 1f),
                CreateConsideration(EnemyAIConsiderationInput.HealthRatio, 0f, 1f, true, 0f, 1f),
                CreateConsideration(EnemyAIConsiderationInput.NearbyAliveFriendCount, 0f, 4f, false, 0f, 1f),
            });
        EnemyAIContext context = new(
            null,
            null,
            null,
            null,
            null,
            Vector3.zero,
            Vector3.zero,
            distanceToTarget: 5f,
            healthRatio: 0.25f,
            hasTarget: true,
            targetAlive: true,
            targetInAttackRange: false,
            canMove: true,
            canAct: true,
            nearbyAliveFriendCount: 2);

        Assert.That(action.Evaluate(context), Is.EqualTo(0.375f).Within(0.0001f));
    }

    [Test]
    public void TickAI_SelectsHighestScoringExecutableMovementAction()
    {
        EnemyAIController controller = CreateController(out _, out _);
        EnemyAIProfile profile = CreateProfile(
            new[]
            {
                CreateMovementAction("chase", EnemyMovementKind.ChaseTarget, weight: 0.25f, cooldownSeconds: 0f),
                CreateMovementAction("kite", EnemyMovementKind.KeepDistance, weight: 2f, cooldownSeconds: 0f),
            },
            CreateMovementAction("fallback", EnemyMovementKind.ChaseTarget, weight: 1f, cooldownSeconds: 0f));

        Assert.That(controller.ApplyProfile(profile), Is.True);
        Assert.That(controller.TickAI(0f), Is.True);

        Assert.That(controller.TryGetMovementOverride(out EnemyMovementKind movementKind), Is.True);
        Assert.That(movementKind, Is.EqualTo(EnemyMovementKind.KeepDistance));
    }

    [Test]
    public void TickAI_SkipsCoolingActionAndUsesFallback()
    {
        EnemyAIController controller = CreateController(out _, out _);
        EnemyAIProfile profile = CreateProfile(
            new[]
            {
                CreateMovementAction("kite", EnemyMovementKind.KeepDistance, weight: 2f, cooldownSeconds: 10f),
            },
            CreateMovementAction("fallback", EnemyMovementKind.ChaseTarget, weight: 1f, cooldownSeconds: 0f));

        Assert.That(controller.ApplyProfile(profile), Is.True);
        Assert.That(controller.TickAI(0f), Is.True);
        Assert.That(controller.TryGetMovementOverride(out EnemyMovementKind firstKind), Is.True);
        Assert.That(firstKind, Is.EqualTo(EnemyMovementKind.KeepDistance));

        Assert.That(controller.TickAI(1f), Is.True);
        Assert.That(controller.TryGetMovementOverride(out EnemyMovementKind secondKind), Is.True);
        Assert.That(secondKind, Is.EqualTo(EnemyMovementKind.ChaseTarget));
    }

    [Test]
    public void EnemyAIContext_CountsNearbyAliveFriendsIgnoringSelfDeadAndFarEnemies()
    {
        BaseCharEnemyNorm1 self = CreateEnemy("Self", Vector3.zero, health: 10f);
        CreateEnemy("Nearby", new Vector3(2f, 0f, 0f), health: 10f);
        CreateEnemy("Dead", new Vector3(3f, 0f, 0f), health: 0f);
        CreateEnemy("Far", new Vector3(20f, 0f, 0f), health: 10f);

        EnemyAIContext context = EnemyAIContext.Create(self, null, null, perceptionRadius: 5f);

        Assert.That(context.NearbyAliveFriendCount, Is.EqualTo(1));
    }

    [Test]
    public void NormalEnemyAssets_ReferenceProfilesAndBossAssetsKeepProfileEmpty()
    {
        string[] normalEnemyPaths =
        {
            "Assets/Data/Enemies/群.asset",
            "Assets/Data/Enemies/迅.asset",
            "Assets/Data/Enemies/甲.asset",
            "Assets/Data/Enemies/爆.asset",
            "Assets/Data/Enemies/弦.asset",
            "Assets/Data/Enemies/锁.asset",
            "Assets/Data/Enemies/愈.asset",
            "Assets/Data/Enemies/召.asset",
        };

        for (int i = 0; i < normalEnemyPaths.Length; i++)
        {
            EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(normalEnemyPaths[i]);
            Assert.That(definition, Is.Not.Null, normalEnemyPaths[i]);
            Assert.That(definition.AIProfile, Is.Not.Null, normalEnemyPaths[i]);
        }

        EnemyDefinition bossPhaseOne = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Boss_Phase1.asset");
        EnemyDefinition bossPhaseTwo = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Boss_Phase2.asset");
        Assert.That(bossPhaseOne, Is.Not.Null);
        Assert.That(bossPhaseTwo, Is.Not.Null);
        Assert.That(bossPhaseOne.AIProfile, Is.Null);
        Assert.That(bossPhaseTwo.AIProfile, Is.Null);
    }

    private EnemyAIController CreateController(out BaseCharEnemyNorm1 enemy, out GameObject enemyObject)
    {
        enemyObject = CreateGameObject("Enemy");
        enemyObject.AddComponent<EnemyStatusEffectController>();
        enemyObject.AddComponent<CharEnemyMovement>();
        enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        SetEnemyHealth(enemy, 10f);
        enemy.TryBindDefinition(CreateDefinition());
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 5f, 0f, 1f));
        EnemyAIController controller = enemyObject.AddComponent<EnemyAIController>();
        Assert.That(controller.TryCacheBindings(overwriteExisting: true), Is.True);
        return controller;
    }

    private BaseCharEnemyNorm1 CreateEnemy(string name, Vector3 position, float health)
    {
        GameObject enemyObject = CreateGameObject(name);
        enemyObject.transform.position = position;
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        SetEnemyHealth(enemy, health);
        return enemy;
    }

    private EnemyDefinition CreateDefinition()
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", "TestEnemy");
        SetPrivateField(definition, "displayName", "TestEnemy");
        SetPrivateField(definition, "movementKind", EnemyMovementKind.ChaseTarget);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.None);
        return definition;
    }

    private EnemyAIProfile CreateProfile(
        IEnumerable<EnemyAIActionDefinition> actions,
        EnemyAIActionDefinition fallbackAction)
    {
        EnemyAIProfile profile = ScriptableObject.CreateInstance<EnemyAIProfile>();
        createdObjects.Add(profile);
        SetPrivateField(profile, "tickIntervalSeconds", 0.1f);
        SetPrivateField(profile, "perceptionRadius", 8f);
        SetPrivateField(profile, "actions", new List<EnemyAIActionDefinition>(actions));
        SetPrivateField(profile, "fallbackAction", fallbackAction);
        return profile;
    }

    private static EnemyAIActionDefinition CreateMovementAction(
        string actionId,
        EnemyMovementKind movementKind,
        float weight,
        float cooldownSeconds)
    {
        return CreateAction(
            EnemyAIActionKind.Movement,
            movementKind,
            EnemyAttackKind.None,
            EnemySkillKind.None,
            weight,
            cooldownSeconds,
            actionLockSeconds: 0f,
            new[] { CreateConsideration(EnemyAIConsiderationInput.Constant, 0f, 1f, false, 1f, 1f) },
            actionId);
    }

    private static EnemyAIActionDefinition CreateAction(
        EnemyAIActionKind actionKind,
        EnemyMovementKind movementKind,
        EnemyAttackKind attackKind,
        EnemySkillKind skillKind,
        float weight,
        float cooldownSeconds,
        float actionLockSeconds,
        IEnumerable<EnemyAIConsiderationDefinition> considerations,
        string actionId = "test_action")
    {
        EnemyAIActionDefinition action = default;
        action = SetStructField(action, "actionId", actionId);
        action = SetStructField(action, "actionKind", actionKind);
        action = SetStructField(action, "movementKind", movementKind);
        action = SetStructField(action, "attackKind", attackKind);
        action = SetStructField(action, "skillKind", skillKind);
        action = SetStructField(action, "skillSlotIndex", -1);
        action = SetStructField(action, "weight", weight);
        action = SetStructField(action, "cooldownSeconds", cooldownSeconds);
        action = SetStructField(action, "actionLockSeconds", actionLockSeconds);
        action = SetStructField(action, "considerations", new List<EnemyAIConsiderationDefinition>(considerations));
        return action;
    }

    private static EnemyAIConsiderationDefinition CreateConsideration(
        EnemyAIConsiderationInput input,
        float minInput,
        float maxInput,
        bool invert,
        float minScore,
        float maxScore)
    {
        EnemyAIConsiderationDefinition consideration = default;
        consideration = SetStructField(consideration, "input", input);
        consideration = SetStructField(consideration, "minInput", minInput);
        consideration = SetStructField(consideration, "maxInput", maxInput);
        consideration = SetStructField(consideration, "invert", invert);
        consideration = SetStructField(consideration, "minScore", minScore);
        consideration = SetStructField(consideration, "maxScore", maxScore);
        return consideration;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetEnemyHealth(BaseCharEnemyNorm1 enemy, float health)
    {
        SetPrivateField(enemy, "health", Mathf.Max(1f, health));
        SetPrivateField(enemy, "currentHealth", health);
        SetPrivateField(enemy, "hasInitializedHealth", true);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static TStruct SetStructField<TStruct, TValue>(TStruct value, string fieldName, TValue fieldValue)
        where TStruct : struct
    {
        object boxedValue = value;
        FieldInfo field = FindInstanceField(typeof(TStruct), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(boxedValue, fieldValue);
        return (TStruct)boxedValue;
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
