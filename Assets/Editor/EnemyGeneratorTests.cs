using System.Collections.Generic;
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

    private static void SetPrivateField<T>(EnemyGenerator generator, string fieldName, T value)
    {
        var field = typeof(EnemyGenerator).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(generator, value);
    }
}
