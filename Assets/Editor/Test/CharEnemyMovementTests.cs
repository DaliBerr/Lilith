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
        playerObject.transform.position = new Vector3(10f, 0f, 0f);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.AggroOnHit, EnemyAttackKind.None);
        SetPrivateField(definition, "aggroOnHitMovement", new EnemyDefinition.AggroOnHitMovementDefinition
        {
            aggroSpeedMultiplier = 2f,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(movement, "Awake");

        Vector3 initialPosition = enemyObject.transform.position;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);
        Assert.That(enemyObject.transform.position, Is.EqualTo(initialPosition));

        enemy.TryApplyDamage(1f, out _, out _);
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0.25f);
        Assert.That(enemyObject.transform.position.x, Is.GreaterThan(initialPosition.x));
    }

    [Test]
    public void TickMovement_KeepDistance_ApproachesAndRetreatsBasedOnDistanceBand()
    {
        BaseCharEnemyNorm1 enemy = CreateEnemy(out CharEnemyMovement movement, out GameObject enemyObject);
        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(10f, 0f, 0f);

        EnemyDefinition definition = CreateDefinition(EnemyMovementKind.KeepDistance, EnemyAttackKind.None);
        SetPrivateField(definition, "keepDistanceMovement", new EnemyDefinition.KeepDistanceMovementDefinition
        {
            preferredDistance = 5f,
            distanceTolerance = 1f,
        });
        enemy.TryBindDefinition(definition);
        enemy.ApplyWaveConfig(new EnemyWaveConfig(10f, 5f, 0f, 0f, 0f));
        Assert.That(movement.TrySetTarget(playerObject.transform), Is.True);
        InvokePrivateMethod(movement, "Awake");

        enemyObject.transform.position = Vector3.zero;
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0f);
        Assert.That(enemyObject.transform.position.x, Is.GreaterThan(0f));

        enemyObject.transform.position = new Vector3(7f, 0f, 0f);
        InvokePrivateMethod(movement, "TickMovement", 0.2f, 0.25f);
        Assert.That(enemyObject.transform.position.x, Is.LessThan(7f));
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
