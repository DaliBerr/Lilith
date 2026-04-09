using System.Collections.Generic;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

public sealed class GridPathfinderTests
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
    public void TryFindPath_RoutesAroundWallBarrier()
    {
        MapGridAuthoring mapGrid = CreateMapAuthoring(5, 3, Vector2.one);
        SetCellTag(mapGrid, 1, 1, MapGridAuthoring.WallTagName);
        SetCellTag(mapGrid, 2, 1, MapGridAuthoring.WallTagName);
        SetCellTag(mapGrid, 3, 1, MapGridAuthoring.WallTagName);

        bool success = GridPathfinder.TryFindPath(mapGrid, new Vector2Int(0, 1), new Vector2Int(4, 1), out List<Vector2Int> pathCells);

        Assert.That(success, Is.True);
        Assert.That(pathCells, Is.Not.Null);
        Assert.That(pathCells, Is.Not.Empty);
        Assert.That(pathCells[0].y, Is.Not.EqualTo(1));
        Assert.That(pathCells[^1], Is.EqualTo(new Vector2Int(4, 1)));

        foreach (Vector2Int cell in pathCells)
        {
            Assert.That(cell, Is.Not.EqualTo(new Vector2Int(1, 1)));
            Assert.That(cell, Is.Not.EqualTo(new Vector2Int(2, 1)));
            Assert.That(cell, Is.Not.EqualTo(new Vector2Int(3, 1)));
        }
    }

    private MapGridAuthoring CreateMapAuthoring(int width, int height, Vector2 cellSize)
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
                cell.tag = MapGridAuthoring.GroundTagName;
                cell.transform.position = authoring.GetCellWorldPosition(x, y);
                entries.Add(new CellEntry(x, y, cell));
            }
        }

        authoring.ReplaceCellEntries(entries);
        return authoring;
    }

    private void SetCellTag(MapGridAuthoring mapGrid, int x, int y, string tag)
    {
        Assert.That(mapGrid.TryGetCell(new Vector2Int(x, y), out GameObject cellObject), Is.True);
        cellObject.tag = tag;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}