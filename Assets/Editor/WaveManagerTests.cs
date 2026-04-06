using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class WaveManagerTests
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
    public void Tick_SpawnsWaveByQuotaAndAdvancesAfterEnemiesAreCleared()
    {
        CreateMapAuthoring(64, 64, Vector2.one, MapGridAuthoring.GroundTagName);

        GameObject playerObject = CreateGameObject("Player");
        playerObject.transform.position = new Vector3(32f, 0f, 32f);

        GameObject enemyParent = CreateGameObject("SpawnedEnemies");
        GameObject sharedEnemyPrefab = CreateEnemyPrefab();
        EnemyDefinition basicDefinition = CreateEnemyDefinition("BasicEnemy", sharedEnemyPrefab, "甲");
        EnemyDefinition fastDefinition = CreateEnemyDefinition("FastEnemy", sharedEnemyPrefab, "迅");

        EnemyGenerator generator = CreateGameObject("EnemyGenerator").AddComponent<EnemyGenerator>();
        Assert.That(generator.TrySetTarget(playerObject.transform), Is.True);
        SetPrivateField(generator, "spawnDistance", 4f);
        SetPrivateField(generator, "maxGroundSpawnRolls", 8);
        SetPrivateField(generator, "spawnedEnemyParent", enemyParent.transform);

        WaveDefinition waveOne = CreateWaveDefinition(
            0.5f,
            new WaveEnemySpawnEntry(basicDefinition, 1, new EnemyWaveConfig(40f, 80f, 10f, 1f, 2f)),
            new WaveEnemySpawnEntry(fastDefinition, 2, new EnemyWaveConfig(60f, 140f, 12f, 0.75f, 4f)));
        WaveDefinition waveTwo = CreateWaveDefinition(
            0.5f,
            new WaveEnemySpawnEntry(fastDefinition, 1, new EnemyWaveConfig(75f, 160f, 14f, 0.5f, 6f)));

        WaveManager waveManager = CreateGameObject("WaveManager").AddComponent<WaveManager>();
        SetPrivateField(waveManager, "enemyGenerator", generator);
        SetPrivateField(waveManager, "waves", new List<WaveDefinition> { waveOne, waveTwo });
        SetPrivateField(waveManager, "autoStartOnEnable", false);
        SetPrivateField(waveManager, "interWaveDelay", 1f);

        Assert.That(waveManager.TryStartSequence(), Is.True);

        InvokePrivateMethod<object>(waveManager, "Tick", 0f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(1));
        Assert.That(enemyParent.transform.GetChild(0).GetComponent<Enemy>().EnemyName, Is.EqualTo("BasicEnemy"));

        InvokePrivateMethod<object>(waveManager, "Tick", 0.25f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(1));

        InvokePrivateMethod<object>(waveManager, "Tick", 0.5f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(2));
        Assert.That(enemyParent.transform.GetChild(1).GetComponent<Enemy>().EnemyName, Is.EqualTo("FastEnemy"));

        InvokePrivateMethod<object>(waveManager, "Tick", 1f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(3));
        Assert.That(enemyParent.transform.GetChild(2).GetComponent<Enemy>().EnemyName, Is.EqualTo("FastEnemy"));

        InvokePrivateMethod<object>(waveManager, "Tick", 1.25f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(3), "Current wave should not advance before active enemies are cleared.");

        DestroyAllChildrenImmediate(enemyParent.transform);
        InvokePrivateMethod<object>(waveManager, "Tick", 1.25f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

        InvokePrivateMethod<object>(waveManager, "Tick", 1.75f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

        InvokePrivateMethod<object>(waveManager, "Tick", 2.25f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

        InvokePrivateMethod<object>(waveManager, "Tick", 3.26f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(1));
        Assert.That(enemyParent.transform.GetChild(0).GetComponent<Enemy>().EnemyName, Is.EqualTo("FastEnemy"));

        DestroyAllChildrenImmediate(enemyParent.transform);
        InvokePrivateMethod<object>(waveManager, "Tick", 3.5f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(0));

        InvokePrivateMethod<object>(waveManager, "Tick", 4.5f);
        InvokePrivateMethod<object>(waveManager, "Tick", 10f);
        Assert.That(enemyParent.transform.childCount, Is.EqualTo(0), "Completed sequence should stop spawning new enemies.");
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

    private GameObject CreateEnemyPrefab()
    {
        GameObject enemyObject = CreateGameObject("EnemyPrefab");
        enemyObject.AddComponent<CharEnemyMovement>();
        enemyObject.AddComponent<EnemyMeleeAttacker>();
        enemyObject.AddComponent<EnemyDefinitionBinder>();
        enemyObject.AddComponent<BaseCharEnemyNorm1>();
        return enemyObject;
    }

    private EnemyDefinition CreateEnemyDefinition(string enemyId, GameObject runtimePrefab, string glyphText)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = enemyId;
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", enemyId);
        SetPrivateField(definition, "displayName", enemyId);
        SetPrivateField(definition, "runtimePrefab", runtimePrefab);
        SetPrivateField(definition, "movementKind", EnemyMovementKind.ChaseTarget);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.MeleeContact);
        SetPrivateField(definition, "visual", new EnemyDefinition.EnemyVisualDefinition
        {
            glyphText = glyphText,
            glyphColor = Color.white,
            runeBaseTint = new Color(0.92f, 0.94f, 0.98f, 0.45f),
            groundShadowTint = new Color(0f, 0f, 0f, 0.28f),
        });
        return definition;
    }

    private WaveDefinition CreateWaveDefinition(float spawnIntervalSeconds, params WaveEnemySpawnEntry[] enemySpawns)
    {
        WaveDefinition waveDefinition = ScriptableObject.CreateInstance<WaveDefinition>();
        createdObjects.Add(waveDefinition);
        SetPrivateField(waveDefinition, "spawnIntervalSeconds", spawnIntervalSeconds);
        SetPrivateField(waveDefinition, "enemySpawns", new List<WaveEnemySpawnEntry>(enemySpawns));
        return waveDefinition;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private void DestroyAllChildrenImmediate(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
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
}
