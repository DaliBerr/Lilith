using System.Collections.Generic;
using Kernel.MapGrid;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Kernel.MapGrid.Editor.Tests
{
    public sealed class MapGridAuthoringTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = createdObjects.Count - 1; i >= 0; i--)
            {
                var createdObject = createdObjects[i];
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void GetCellLocalPosition_UsesXYPlane()
        {
            var authoring = CreateAuthoring();
            authoring.CellSize = new Vector2(1.5f, 2.25f);

            var localPosition = authoring.GetCellLocalPosition(2, 3);

            Assert.That(localPosition, Is.EqualTo(new Vector3(3f, 6.75f, 0f)));
        }

        [Test]
        public void TryGetCellCoordinateFromLocalPoint_UsesCenteredCellAnchors()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 3;
            authoring.CellSize = new Vector2(8f, 8f);

            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(0f, 0f, 0f), out var originCell), Is.True);
            Assert.That(originCell, Is.EqualTo(new Vector2Int(0, 0)));

            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(12f, 8f, 0f), out var centerCell), Is.True);
            Assert.That(centerCell, Is.EqualTo(new Vector2Int(2, 1)));

            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(-4.1f, 0f, 0f), out _), Is.False);
            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(28.1f, 0f, 0f), out _), Is.False);
        }

        [Test]
        public void ChunkCoordinates_RespectConfiguredChunkSize()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 5;
            authoring.GridHeight = 4;
            authoring.ChunkWidthInCells = 3;
            authoring.ChunkHeightInCells = 2;

            Assert.That(authoring.GetChunkCoordinate(4, 3), Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(authoring.GetLocalRowInChunk(3), Is.EqualTo(1));
            Assert.That(authoring.GetChunkCountX(), Is.EqualTo(2));
            Assert.That(authoring.GetChunkCountY(), Is.EqualTo(2));
        }

        [Test]
        public void GetGridLocalCenter_UsesCellCentersInsteadOfOuterCorner()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 3;
            authoring.CellSize = new Vector2(8f, 8f);

            var localCenter = authoring.GetGridLocalCenter();

            Assert.That(localCenter, Is.EqualTo(new Vector3(12f, 8f, 0f)));
        }

        [Test]
        public void ReplaceCellEntries_BuildsQueryableIndex()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 4;

            var cellA = CreateGameObject("Cell_A");
            var cellB = CreateGameObject("Cell_B");

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cellA),
                new CellEntry(1, 2, cellB),
            });

            Assert.That(authoring.ContainsCell(0, 0), Is.True);
            Assert.That(authoring.TryGetCell(new Vector2Int(1, 2), out var resolvedCell), Is.True);
            Assert.That(resolvedCell, Is.SameAs(cellB));
            Assert.That(authoring.GetCell(3, 3), Is.Null);
        }

        [Test]
        public void BuildStrokeCoordinates_InterpolatesAllVisitedCells()
        {
            var coordinates = MapGridEditorUtility.BuildStrokeCoordinates(new Vector2Int(0, 0), new Vector2Int(3, 2));

            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 2),
                },
                coordinates);
        }

        [Test]
        public void TryGetUniqueCellText_FailsWhenNoTextExists()
        {
            var cell = CreateCell("Cell_NoText", new Vector2Int(0, 0), textComponentCount: 0);

            var success = MapGridEditorUtility.TryGetUniqueCellText(cell, out _, out var error);

            Assert.That(success, Is.False);
            StringAssert.Contains("does not contain a TMP_Text", error);
        }

        [Test]
        public void TryGetUniqueCellText_FailsWhenMultipleTextsExist()
        {
            var cell = CreateCell("Cell_MultiText", new Vector2Int(0, 0), textComponentCount: 2);

            var success = MapGridEditorUtility.TryGetUniqueCellText(cell, out _, out var error);

            Assert.That(success, Is.False);
            StringAssert.Contains("requires exactly one", error);
        }

        [Test]
        public void TrySetCellText_UpdatesOnlyTheTargetCell()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            var cellA = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var cellB = CreateCell("Cell_B", new Vector2Int(1, 0), textComponentCount: 1);
            var textA = cellA.GetComponentInChildren<TMP_Text>(true);
            var textB = cellB.GetComponentInChildren<TMP_Text>(true);
            textA.text = "A";
            textB.text = "B";

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cellA),
                new CellEntry(1, 0, cellB),
            });

            var success = MapGridEditorUtility.TrySetCellText(authoring, new Vector2Int(1, 0), "Z", out var changed, out var error);

            Assert.That(success, Is.True, error);
            Assert.That(changed, Is.True);
            Assert.That(textA.text, Is.EqualTo("A"));
            Assert.That(textB.text, Is.EqualTo("Z"));
        }

        [Test]
        public void TrySetCellText_ClearsTextForEraserBehavior()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var text = cell.GetComponentInChildren<TMP_Text>(true);
            text.text = "Filled";
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell),
            });

            var success = MapGridEditorUtility.TrySetCellText(authoring, new Vector2Int(0, 0), string.Empty, out var changed, out var error);

            Assert.That(success, Is.True, error);
            Assert.That(changed, Is.True);
            Assert.That(text.text, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TryRebuildLookupFromEntries_FailsOnDuplicateCoordinates()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 4;

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(1, 1, CreateGameObject("Cell_1")),
                new CellEntry(1, 1, CreateGameObject("Cell_2")),
            });

            var success = authoring.TryRebuildLookupFromEntries(out var error);

            Assert.That(success, Is.False);
            StringAssert.Contains("Duplicate", error);
            Assert.That(authoring.ContainsCell(1, 1), Is.False);
        }

        [Test]
        public void TryRebuildLookupFromEntries_FailsOnOutOfBoundsCoordinates()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 2;

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(2, 0, CreateGameObject("Cell_OutOfBounds")),
            });

            var success = authoring.TryRebuildLookupFromEntries(out var error);

            Assert.That(success, Is.False);
            StringAssert.Contains("out of grid bounds", error);
        }

        private MapGridAuthoring CreateAuthoring()
        {
            var mapRoot = CreateGameObject("MapRoot");
            return mapRoot.AddComponent<MapGridAuthoring>();
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateCell(string name, Vector2Int coordinates, int textComponentCount)
        {
            var cell = CreateGameObject(name);
            var cellData = cell.AddComponent<CellData>();
            cellData.SetCoordinates(coordinates);

            for (var i = 0; i < textComponentCount; i++)
            {
                var textObject = new GameObject($"Text_{i}", typeof(RectTransform));
                createdObjects.Add(textObject);
                textObject.transform.SetParent(cell.transform, false);
                textObject.AddComponent<TextMeshPro>();
            }

            return cell;
        }
    }
}
