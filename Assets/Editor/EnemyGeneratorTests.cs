using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyGeneratorTests
{
    private readonly List<GameObject> createdObjects = new();

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
    public void TryGetSpawnPosition_ReturnsGroundTaggedCellOnly()
    {
        MapGridAuthoring authoring = CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = authoring.GetCellWorldPosition(32, 32);

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        bool success = generator.TryGetSpawnPosition(out Vector3 spawnPosition);

        Assert.That(success, Is.True);
        Assert.That(authoring.TryGetCellCoordinateFromWorldPoint(spawnPosition, out Vector2Int coordinate), Is.True);
        Assert.That(authoring.TryGetCell(coordinate, out GameObject cellObject), Is.True);
        Assert.That(cellObject.CompareTag(MapGridAuthoring.GroundTagName), Is.True);
    }

    [Test]
    public void TryGetSpawnPosition_ReturnsFalseWhenNoGroundTaggedCellCanBeRolled()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.WallTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 4);

        bool success = generator.TryGetSpawnPosition(out _);

        Assert.That(success, Is.False);
    }

    [Test]
    public void TrySpawnEnemy_AppliesWaveConfigWithoutMutatingPrefab()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        CharEnemyMovement enemyPrefab = CreateEnemyPrefab(100f);
        BaseCharEnemyNorm1 prefabEnemy = enemyPrefab.GetComponent<BaseCharEnemyNorm1>();
        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "charEnemyPrefab", enemyPrefab);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        EnemyWaveConfig config = new(250f, 90f, 18f, 0.75f, 12f);

        bool success = generator.TrySpawnEnemy(config, out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        Assert.That(spawnedEnemy, Is.TypeOf<BaseCharEnemyNorm1>());

        createdObjects.Add(spawnedEnemy.gameObject);
        BaseCharEnemyNorm1 spawnedBaseEnemy = (BaseCharEnemyNorm1)spawnedEnemy;
        Assert.That(spawnedBaseEnemy.MaxHealth, Is.EqualTo(250f));
        Assert.That(spawnedBaseEnemy.CurrentHealth, Is.EqualTo(250f));
        Assert.That(spawnedBaseEnemy.MoveSpeed, Is.EqualTo(90f));
        Assert.That(spawnedBaseEnemy.AttackRange, Is.EqualTo(18f));
        Assert.That(spawnedBaseEnemy.AttackCooldown, Is.EqualTo(0.75f));
        Assert.That(spawnedBaseEnemy.AttackDamage, Is.EqualTo(12f));
        Assert.That(spawnedBaseEnemy.StoppingDistance, Is.EqualTo(18f));

        Assert.That(prefabEnemy.MaxHealth, Is.EqualTo(100f));
        Assert.That(prefabEnemy.MoveSpeed, Is.EqualTo(120f));
        Assert.That(prefabEnemy.AttackRange, Is.EqualTo(0f));
        Assert.That(prefabEnemy.AttackCooldown, Is.EqualTo(0f));
        Assert.That(prefabEnemy.AttackDamage, Is.EqualTo(0f));
    }

    [Test]
    public void TrySpawnEnemy_ByEnemyName_UsesMatchingPrefabFromCatalog()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);
        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Transform player = CreateGameObject("Player").transform;
        player.position = new Vector3(32f, 0f, 32f);

        CharEnemyMovement basicPrefab = CreateEnemyPrefab(100f, "BasicEnemy");
        CharEnemyMovement fastPrefab = CreateEnemyPrefab(80f, "FastEnemy");
        Assert.That(generator.TrySetTarget(player), Is.True);
        SetPrivateField(generator, "charEnemyPrefab", basicPrefab);
        SetPrivateField(generator, "additionalEnemyPrefabs", new List<CharEnemyMovement> { fastPrefab });
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);

        EnemyWaveConfig config = new(120f, 160f, 12f, 0.4f, 9f);

        bool success = generator.TrySpawnEnemy("FastEnemy", config, out Enemy spawnedEnemy);

        Assert.That(success, Is.True);
        Assert.That(spawnedEnemy.EnemyName, Is.EqualTo("FastEnemy"));
        Assert.That(spawnedEnemy.MoveSpeed, Is.EqualTo(160f));
        Assert.That(spawnedEnemy.AttackDamage, Is.EqualTo(9f));
    }

    private MapGridAuthoring CreateMapAuthoring(int width, int height, Vector2 cellSize, string fillTag)
    {
        GameObject mapRoot = CreateGameObject("MapRoot");
        MapGridAuthoring authoring = mapRoot.AddComponent<MapGridAuthoring>();
        authoring.GridWidth = width;
        authoring.GridHeight = height;
        authoring.CellSize = cellSize;

        var entries = new List<CellEntry>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject cell = CreateGameObject($"Cell_{x}_{y}");
                cell.tag = fillTag;
                cell.transform.position = authoring.GetCellWorldPosition(x, y);
                entries.Add(new CellEntry(x, y, cell));
            }
        }

        authoring.ReplaceCellEntries(entries);
        return authoring;
    }

    private GameObject CreateGameObject(string name)
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private CharEnemyMovement CreateEnemyPrefab(float maxHealth, string enemyName = "EnemyPrefab")
    {
        GameObject enemyObject = CreateGameObject("EnemyPrefab");
        CharEnemyMovement movement = enemyObject.AddComponent<CharEnemyMovement>();
        BaseCharEnemyNorm1 enemyData = enemyObject.AddComponent<BaseCharEnemyNorm1>();
        SetPrivateField(enemyData, "enemyName", enemyName);
        SetPrivateField(enemyData, "health", maxHealth);
        return movement;
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
