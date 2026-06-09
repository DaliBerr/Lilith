using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class CharEnemyMovementTests
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
    public void TryApplyDamage_RaisesDamagedEvent()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out _, out _);
        int damageEventCount = 0;
        enemy.Damaged += _ => damageEventCount++;

        bool success = enemy.TryApplyDamage(1f, out float remainingHealth, out bool isDead);

        Assert.That(success, Is.True);
        Assert.That(remainingHealth, Is.EqualTo(9f));
        Assert.That(isDead, Is.False);
        Assert.That(damageEventCount, Is.EqualTo(1));
    }

    [Test]
    public void TickMovement_AggroOnHit_RemainsIdleUntilDamagedThenChases()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        GameObject playerObject = CreateGameObject("Player");
        MapGridAuthoring mapGrid = CreateMapAuthoring(32, 32, Vector2.one);
        enemyObject.transform.position = mapGrid.GetCellWorldPosition(10, 10);
        playerObject.transform.position = mapGrid.GetCellWorldPosition(20, 10);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.AggroOnHit, EnemyAttackKind.None);
        SetPrivateField(definition, "aggroOnHitMovement", new EnemyDefinition.AggroOnHitMovementDefinition
        {
            aggroSpeedMultiplier = 2f,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(movement.TrySetTargetMapGrid(mapGrid), Is.True);
        InvokePrivateMethod(movement, "Awake");

        Vector3 initialPosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);
        Assert.That(enemyObject.transform.position, Is.EqualTo(initialPosition));

        enemy.TryApplyDamage(1f, out _, out _);
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0.25f);
        Assert.That(enemyObject.transform.position.x, Is.GreaterThan(initialPosition.x));
    }

    [Test]
    public void TickMovement_UsesAIMovementOverrideWhenProfileIsActive()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        EnemyAIController aiController = enemyObject.AddComponent<EnemyAIController>();
        GameObject playerObject = CreateGameObject("Player");
        MapGridAuthoring mapGrid = CreateMapAuthoring(32, 32, Vector2.one);
        enemyObject.transform.position = mapGrid.GetCellWorldPosition(10, 10);
        playerObject.transform.position = mapGrid.GetCellWorldPosition(20, 10);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.AggroOnHit, EnemyAttackKind.None);
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(movement.TrySetTargetMapGrid(mapGrid), Is.True);
        InvokePrivateMethod(movement, "Awake");

        Vector3 idlePosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);
        Assert.That(enemyObject.transform.position, Is.EqualTo(idlePosition));

        EnemyAIProfile profile = ScriptableObject.CreateInstance<EnemyAIProfile>();
        createdObjects.Add(profile);
        Assert.That(aiController.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(aiController.ApplyProfile(profile), Is.True);
        Assert.That(aiController.TickAI(0.1f), Is.True);

        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0.2f);

        Assert.That(enemyObject.transform.position.x, Is.GreaterThan(idlePosition.x));
    }

    [Test]
    public void TickMovement_KeepDistance_ApproachesAndRetreatsBasedOnDistanceBand()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        GameObject playerObject = CreateGameObject("Player");
        MapGridAuthoring mapGrid = CreateMapAuthoring(32, 32, Vector2.one);
        enemyObject.transform.position = mapGrid.GetCellWorldPosition(10, 10);
        playerObject.transform.position = mapGrid.GetCellWorldPosition(20, 10);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.KeepDistance, EnemyAttackKind.None);
        SetPrivateField(definition, "keepDistanceMovement", new EnemyDefinition.KeepDistanceMovementDefinition
        {
            preferredDistance = 5f,
            distanceTolerance = 1f,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        Assert.That(movement.TrySetTargetMapGrid(mapGrid), Is.True);
        InvokePrivateMethod(movement, "Awake");

        enemyObject.transform.position = mapGrid.GetCellWorldPosition(10, 10);
        Vector3 firstStartPosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);
        Assert.That(enemyObject.transform.position.x, Is.GreaterThan(firstStartPosition.x));

        enemyObject.transform.position = mapGrid.GetCellWorldPosition(17, 10);
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0.25f);
        Assert.That(enemyObject.transform.position.x, Is.LessThan(mapGrid.GetCellWorldPosition(17, 10).x));
    }

    [Test]
    public void TickMovement_KeepDistance_RoutesAroundWallBarrierWhenRetreating()
    {
        MapGridAuthoring mapGrid = CreateMapAuthoring(5, 5, Vector2.one);
        SetCellTag(mapGrid, 2, 1, MapGridAuthoring.WallTagName);
        SetCellTag(mapGrid, 2, 2, MapGridAuthoring.WallTagName);
        SetCellTag(mapGrid, 2, 3, MapGridAuthoring.WallTagName);

        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        GameObject playerObject = CreateGameObject("Player");
        enemyObject.transform.position = mapGrid.GetCellWorldPosition(3, 2);
        playerObject.transform.position = mapGrid.GetCellWorldPosition(4, 2);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.KeepDistance, EnemyAttackKind.None);
        SetPrivateField(definition, "keepDistanceMovement", new EnemyDefinition.KeepDistanceMovementDefinition
        {
            preferredDistance = 2f,
            distanceTolerance = 0f,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(movement, "Awake");

        Vector3 startPosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);

        Vector3 endPosition = enemyObject.transform.position;
        Assert.That(Mathf.Abs(endPosition.x - startPosition.x), Is.LessThan(0.01f));
        Assert.That(Mathf.Abs(endPosition.z - startPosition.z), Is.GreaterThan(0.01f));
    }

    [Test]
    public void TickMovement_FollowNearestEnemyKeepDistance_TracksNearestEnemyAndKeepsDistanceBand()
    {
        MapGridAuthoring mapGrid = CreateMapAuthoring(32, 32, Vector2.one);

        BaseCharEnemyNorm1 followerEnemy = CreateEnemy(out CharEnemyMovement followerMovement, out GameObject followerObject);
        BaseCharEnemyNorm1 nearestEnemy = CreateEnemy(out _, out GameObject nearestEnemyObject);
        BaseCharEnemyNorm1 fartherEnemy = CreateEnemy(out _, out GameObject fartherEnemyObject);
        SetEnemyHealth(followerEnemy, 10f);
        SetEnemyHealth(nearestEnemy, 10f);
        SetEnemyHealth(fartherEnemy, 10f);

        followerObject.transform.position = mapGrid.GetCellWorldPosition(10, 10);
        nearestEnemyObject.transform.position = mapGrid.GetCellWorldPosition(14, 10);
        fartherEnemyObject.transform.position = mapGrid.GetCellWorldPosition(22, 10);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.FollowNearestEnemyKeepDistance, EnemyAttackKind.None);
        SetPrivateField(definition, "keepDistanceMovement", new EnemyDefinition.KeepDistanceMovementDefinition
        {
            preferredDistance = 6f,
            distanceTolerance = 1f,
        });
        followerEnemy.TryBindDefinition(definition);
        followerEnemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(followerMovement.TrySetTargetMapGrid(mapGrid), Is.True);
        InvokePrivateMethod(followerMovement, "Awake");

        Vector3 firstStart = followerObject.transform.position;
        InvokePrivateMethod(followerMovement, "TickMovement", 0.2f, 0f);
        Assert.That(followerObject.transform.position.x, Is.LessThan(firstStart.x));

        followerObject.transform.position = mapGrid.GetCellWorldPosition(4, 10);
        InvokePrivateMethod(followerMovement, "TickMovement", 0.2f, 0.3f);
        Assert.That(followerObject.transform.position.x, Is.GreaterThan(mapGrid.GetCellWorldPosition(4, 10).x));
    }

    [Test]
    public void TickMovement_OrbitTarget_UsesAnExplicitEnemyTargetTransform()
    {
        BaseCharEnemyNorm1 orbitTargetEnemy = CreateEnemy(out _, out GameObject orbitTargetObject);
        SetEnemyHealth(orbitTargetEnemy, 10f);
        orbitTargetObject.transform.position = new Vector3(5f, 0f, 0f);

        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        enemyObject.transform.position = Vector3.zero;

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.OrbitTarget, EnemyAttackKind.None);
        SetPrivateField(definition, "orbitTargetMovement", new EnemyDefinition.OrbitTargetMovementDefinition
        {
            orbitRadius = 5f,
            orbitRadiusTolerance = 0.25f,
            orbitSpeedMultiplier = 1f,
            clockwise = true,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 10f, 0f, 0f, 0f));
        Assert.That(movement.TrySetOrbitTarget(orbitTargetObject.transform), Is.True);
        InvokePrivateMethod(movement, "Awake");

        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);

        Assert.That(enemyObject.transform.position.z, Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(enemyObject.transform.position.x), Is.LessThan(0.01f));
    }

    [Test]
    public void TickMovement_BossSmartRoam_SwitchesStrafeDirectionOnConfiguredInterval()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        GameObject playerObject = CreateGameObject("Player");
        enemyObject.transform.position = Vector3.zero;
        playerObject.transform.position = new Vector3(10f, 0f, 0f);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.BossSmartRoam, EnemyAttackKind.None);
        SetPrivateField(definition, "bossSmartRoamMovement", new EnemyDefinition.BossSmartRoamMovementDefinition
        {
            preferredDistance = 10f,
            distanceTolerance = 0.5f,
            strafeSpeedMultiplier = 1f,
            radialCorrectionStrength = 0f,
            approachSpeedMultiplier = 1f,
            retreatSpeedMultiplier = 1f,
            sideSwitchIntervalSeconds = 0.2f,
            startClockwise = true,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 12f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(movement, "Awake");

        Vector3 firstStartPosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);
        Vector3 firstDelta = enemyObject.transform.position - firstStartPosition;

        Vector3 secondStartPosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0.25f);
        Vector3 secondDelta = enemyObject.transform.position - secondStartPosition;

        Assert.That(firstDelta.z, Is.LessThan(0f));
        Assert.That(secondDelta.z, Is.GreaterThan(0f));
    }

    [Test]
    public void TickMovement_ChaseThenDash_TransitionsThroughWindupDashAndCooldown()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(0f, 0f, 6f);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.ChaseThenDash, EnemyAttackKind.None);
        SetPrivateField(definition, "dashMovement", new EnemyDefinition.DashMovementDefinition
        {
            triggerDistance = 8f,
            windupSeconds = 0.2f,
            dashSpeedMultiplier = 3f,
            dashDurationSeconds = 0.2f,
            dashCooldownSeconds = 0.4f,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(movement, "Awake");

        InvokePrivateMethod(movement, "TickMovement", 0.1f, 0f);
        Assert.That(GetPrivateField<object>(movement, "dashState").ToString(), Is.EqualTo("Windup"));

        InvokePrivateMethod(movement, "TickMovement", 0.1f, 0.3f);
        Assert.That(GetPrivateField<object>(movement, "dashState").ToString(), Is.EqualTo("Dashing"));
        Assert.That(enemyObject.transform.position.z, Is.GreaterThan(0f));

        InvokePrivateMethod(movement, "TickMovement", 0.1f, 0.6f);
        Assert.That(GetPrivateField<object>(movement, "dashState").ToString(), Is.EqualTo("Cooldown"));
    }

    private BaseCharEnemyNorm1 CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject)
    {
        enemyObject = CreateGameObject("Enemy");
        enemyObject.tag = "Enemy_Object";
        enemyObject.AddComponent<BoxCollider>().size = new Vector3(6f, 6f, 6f);
        movement = enemyObject.AddComponent<CharEnemyMovement>();
        BaseCharEnemyNorm1 enemy = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        SetEnemyHealth(enemy, 10f);
        return enemy;
    }

    private EnemyDefinition CreateDefinition(EnemyMovementKind movementKind, EnemyAttackKind attackKind)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = "TestEnemyDefinition";
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", "TestEnemy");
        SetPrivateField(definition, "displayName", "TestEnemy");
        SetPrivateField(definition, "movementKind", movementKind);
        SetPrivateField(definition, "attackKind", attackKind);
        SetPrivateField(definition, "visual", new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = "测",
            glyphColor = Color.white,
        });
        return definition;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private MapGridAuthoring CreateMapAuthoring(int width, int height, Vector2 cellSize)
    {
        GameObject mapObject = CreateGameObject("MapRoot");
        MapGridAuthoring mapGrid = mapObject.AddComponent<MapGridAuthoring>();
        mapGrid.GridWidth = width;
        mapGrid.GridHeight = height;
        mapGrid.CellSize = cellSize;

        var cells = new List<CellEntry>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject cellObject = CreateGameObject($"Cell_{x}_{y}");
                cellObject.tag = MapGridAuthoring.GroundTagName;
                cellObject.transform.position = mapGrid.GetCellWorldPosition(x, y);
                cells.Add(new CellEntry(x, y, cellObject));
            }
        }

        mapGrid.ReplaceCellEntries(cells);
        return mapGrid;
    }

    private static void SetCellTag(MapGridAuthoring mapGrid, int x, int y, string tag)
    {
        Assert.That(mapGrid.TryGetCell(new Vector2Int(x, y), out GameObject cellObject), Is.True);
        cellObject.tag = tag;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindInstanceMethod(target.GetType(), methodName);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, args);
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

    private static void SetEnemyHealth(BaseCharEnemyNorm1 enemy, float health)
    {
        SetPrivateField(enemy, "health", health);
        SetPrivateField(enemy, "currentHealth", health);
        SetPrivateField(enemy, "hasInitializedHealth", true);
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
