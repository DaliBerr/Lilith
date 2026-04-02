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
        public void BuildRectangleCoordinates_ReturnsInclusiveAreaInRowMajorOrder()
        {
            var coordinates = MapGridEditorUtility.BuildRectangleCoordinates(new Vector2Int(1, 1), new Vector2Int(3, 2));

            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 2),
                    new Vector2Int(3, 2),
                },
                coordinates);
        }

        [Test]
        public void BuildRectangleCoordinates_NormalizesReverseDraggedCorners()
        {
            var coordinates = MapGridEditorUtility.BuildRectangleCoordinates(new Vector2Int(3, 2), new Vector2Int(1, 1));

            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(2, 1),
                    new Vector2Int(3, 1),
                    new Vector2Int(1, 2),
                    new Vector2Int(2, 2),
                    new Vector2Int(3, 2),
                },
                coordinates);
        }

        [Test]
        public void BuildRectangleCoordinates_ReturnsSingleCoordinateForSingleCellSelection()
        {
            var coordinates = MapGridEditorUtility.BuildRectangleCoordinates(new Vector2Int(2, 2), new Vector2Int(2, 2));

            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(2, 2),
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
            Assert.That(cell.GetComponent<CellData>().IsColliderEnabled, Is.False);
        }

        [Test]
        public void TrySetCellText_EnablesColliderWhenTextIsFilled()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var cellData = cell.GetComponent<CellData>();
            Assert.That(cellData.SetColliderEnabled(false), Is.True);

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell),
            });

            var success = MapGridEditorUtility.TrySetCellText(authoring, new Vector2Int(0, 0), "Filled", out var changed, out var error);

            Assert.That(success, Is.True, error);
            Assert.That(changed, Is.True);
            Assert.That(cellData.IsColliderEnabled, Is.True);
        }

        [Test]
        public void TrySetCellColliderEnabled_TogglesColliderWithoutChangingText()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var text = cell.GetComponentInChildren<TMP_Text>(true);
            text.text = "Wall";

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell),
            });

            var disableSuccess = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), false, out var disableChanged, out var disableError);

            Assert.That(disableSuccess, Is.True, disableError);
            Assert.That(disableChanged, Is.True);
            Assert.That(cell.GetComponent<CellData>().IsColliderEnabled, Is.False);
            Assert.That(text.text, Is.EqualTo("Wall"));

            var enableSuccess = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), true, out var enableChanged, out var enableError);

            Assert.That(enableSuccess, Is.True, enableError);
            Assert.That(enableChanged, Is.True);
            Assert.That(cell.GetComponent<CellData>().IsColliderEnabled, Is.True);
            Assert.That(text.text, Is.EqualTo("Wall"));
        }

        [Test]
        public void TrySetCellColliderEnabled_FailsWhenCellDataIsMissing()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1, includeCellData: false);
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell),
            });

            var success = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), true, out _, out var error);

            Assert.That(success, Is.False);
            StringAssert.Contains("CellData component", error);
        }

        [Test]
        public void TrySetCellColliderEnabled_FailsWhenManagedColliderIsMissing()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1, includeManagedCollider: false);
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell),
            });

            var success = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), true, out _, out var error);

            Assert.That(success, Is.False);
            StringAssert.Contains("managed Collider", error);
        }

        [Test]
        public void RectangleFillText_UpdatesOnlyCellsInsideTheSelection()
        {
            var authoring = CreateAuthoring();
            CreateIndexedCellGrid(authoring, 3, 3, out var texts, out var cells);

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    Assert.That(cells[x, y].SetColliderEnabled(false), Is.True);
                }
            }

            ApplyRectangleText(authoring, new Vector2Int(0, 1), new Vector2Int(1, 2), "Filled");

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var isInside = IsInsideRectangle(x, y, new Vector2Int(0, 1), new Vector2Int(1, 2));
                    Assert.That(texts[x, y].text, Is.EqualTo(isInside ? "Filled" : string.Empty));
                    Assert.That(cells[x, y].IsColliderEnabled, Is.EqualTo(isInside));
                }
            }
        }

        [Test]
        public void RectangleEraseText_ClearsOnlyCellsInsideTheSelection()
        {
            var authoring = CreateAuthoring();
            CreateIndexedCellGrid(authoring, 3, 3, out var texts, out var cells);

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    texts[x, y].text = "Wall";
                    Assert.That(cells[x, y].SetColliderEnabled(true), Is.True);
                }
            }

            ApplyRectangleText(authoring, new Vector2Int(1, 0), new Vector2Int(2, 1), string.Empty);

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var isInside = IsInsideRectangle(x, y, new Vector2Int(1, 0), new Vector2Int(2, 1));
                    Assert.That(texts[x, y].text, Is.EqualTo(isInside ? string.Empty : "Wall"));
                    Assert.That(cells[x, y].IsColliderEnabled, Is.EqualTo(!isInside));
                }
            }
        }

        [Test]
        public void RectangleSetColliderState_ChangesOnlyCellsInsideTheSelection()
        {
            var authoring = CreateAuthoring();
            CreateIndexedCellGrid(authoring, 3, 3, out var texts, out var cells);

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    texts[x, y].text = $"Cell_{x}_{y}";
                    Assert.That(cells[x, y].SetColliderEnabled(false), Is.True);
                }
            }

            ApplyRectangleCollider(authoring, new Vector2Int(1, 1), new Vector2Int(2, 2), true);

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var isInside = IsInsideRectangle(x, y, new Vector2Int(1, 1), new Vector2Int(2, 2));
                    Assert.That(texts[x, y].text, Is.EqualTo($"Cell_{x}_{y}"));
                    Assert.That(cells[x, y].IsColliderEnabled, Is.EqualTo(isInside));
                }
            }
        }

        [Test]
        public void CellData_TrySetWorldPosition_MovesCellRootByDefault()
        {
            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var cellData = cell.GetComponent<CellData>();
            var targetPosition = new Vector3(3f, 4f, 5f);

            var success = cellData.TrySetWorldPosition(targetPosition);

            Assert.That(success, Is.True);
            Assert.That(cell.transform.position, Is.EqualTo(targetPosition));
            Assert.That(cellData.MovementTarget, Is.SameAs(cell.transform));
        }

        [Test]
        public void CellData_TryBindMovementTarget_UsesChildTransformForTranslation()
        {
            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var movementObject = CreateChildObject(cell, "Movement");
            var cellData = cell.GetComponent<CellData>();

            var bindSuccess = cellData.TryBindMovementTarget(movementObject.transform);
            var translateSuccess = cellData.TryTranslate(new Vector3(2f, 0f, 0f), Space.Self);

            Assert.That(bindSuccess, Is.True);
            Assert.That(translateSuccess, Is.True);
            Assert.That(movementObject.transform.localPosition, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(cell.transform.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CellData_TrySetLinearVelocity_UsesBoundRigidbody()
        {
            var cell = CreateCell("Cell_A", new Vector2Int(0, 0), textComponentCount: 1);
            var movementObject = CreateChildObject(cell, "Movement", typeof(Rigidbody));
            var movementRigidbody = movementObject.GetComponent<Rigidbody>();
            var cellData = cell.GetComponent<CellData>();

            var bindSuccess = cellData.TryBindMovementTarget(movementObject.transform, movementRigidbody);
            var velocitySuccess = cellData.TrySetLinearVelocity(new Vector3(1f, 2f, 3f));
            var stopSuccess = cellData.TryStopMovement();

            Assert.That(bindSuccess, Is.True);
            Assert.That(velocitySuccess, Is.True);
            Assert.That(movementRigidbody.linearVelocity, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(stopSuccess, Is.True);
            Assert.That(movementRigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(movementRigidbody.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DisableEmptyTextColliders_DisablesOnlyCellsWithoutTextContent()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 3;
            authoring.GridHeight = 1;

            var filledCell = CreateCell("Cell_Filled", new Vector2Int(0, 0), textComponentCount: 1);
            var emptyCell = CreateCell("Cell_Empty", new Vector2Int(1, 0), textComponentCount: 1);
            var noTextCell = CreateCell("Cell_NoText", new Vector2Int(2, 0), textComponentCount: 0);

            filledCell.GetComponentInChildren<TMP_Text>(true).text = "Wall";
            emptyCell.GetComponentInChildren<TMP_Text>(true).text = "   ";

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, filledCell),
                new CellEntry(1, 0, emptyCell),
                new CellEntry(2, 0, noTextCell),
            });

            var success = MapGridEditorUtility.DisableEmptyTextColliders(authoring, out var error);

            Assert.That(success, Is.True, error);
            Assert.That(filledCell.GetComponent<CellData>().IsColliderEnabled, Is.True);
            Assert.That(emptyCell.GetComponent<CellData>().IsColliderEnabled, Is.False);
            Assert.That(noTextCell.GetComponent<CellData>().IsColliderEnabled, Is.False);
        }

        [Test]
        public void TryInitializeCellSurfaceCache_BuildsRuntimeCacheAndLeavesDirtySetEmpty()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            var textCell = CreateCell("Cell_Text", new Vector2Int(0, 0), textComponentCount: 1);
            var noTextCell = CreateCell("Cell_NoText", new Vector2Int(1, 0), textComponentCount: 0);

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, textCell),
                new CellEntry(1, 0, noTextCell),
            });

            var success = authoring.TryInitializeCellSurfaceCache(out var error);

            Assert.That(success, Is.True, error);
            Assert.That(authoring.IsCellSurfaceCacheInitialized, Is.True);
            Assert.That(authoring.CellSurfaceCacheCount, Is.EqualTo(2));
            Assert.That(authoring.DirtyCellSurfaceCount, Is.Zero);
        }

        [Test]
        public void TryRefreshGroundWallState_AssignsTagsAndColliderStateFromCellText()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 3;
            authoring.GridHeight = 1;

            var wallCell = CreateCell("Cell_Wall", new Vector2Int(0, 0), textComponentCount: 1);
            var groundCell = CreateCell("Cell_Ground", new Vector2Int(1, 0), textComponentCount: 1);
            var noTextCell = CreateCell("Cell_NoText", new Vector2Int(2, 0), textComponentCount: 0);

            wallCell.GetComponentInChildren<TMP_Text>(true).text = "#";
            groundCell.GetComponentInChildren<TMP_Text>(true).text = "   ";
            Assert.That(groundCell.GetComponent<CellData>().SetColliderEnabled(true), Is.True);
            Assert.That(noTextCell.GetComponent<CellData>().SetColliderEnabled(true), Is.True);

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, wallCell),
                new CellEntry(1, 0, groundCell),
                new CellEntry(2, 0, noTextCell),
            });

            var success = authoring.TryRefreshGroundWallState(out var error);

            Assert.That(success, Is.True, error);
            Assert.That(wallCell.tag, Is.EqualTo("Wall"));
            Assert.That(wallCell.GetComponent<CellData>().ManagedCollider.gameObject.tag, Is.EqualTo("Wall"));
            Assert.That(wallCell.GetComponent<CellData>().IsColliderEnabled, Is.True);

            Assert.That(groundCell.tag, Is.EqualTo("Ground"));
            Assert.That(groundCell.GetComponent<CellData>().ManagedCollider.gameObject.tag, Is.EqualTo("Ground"));
            Assert.That(groundCell.GetComponent<CellData>().IsColliderEnabled, Is.False);

            Assert.That(noTextCell.tag, Is.EqualTo("Ground"));
            Assert.That(noTextCell.GetComponent<CellData>().ManagedCollider.gameObject.tag, Is.EqualTo("Ground"));
            Assert.That(noTextCell.GetComponent<CellData>().IsColliderEnabled, Is.False);
        }

        [Test]
        public void TryRefreshDirtyGroundWallState_UpdatesOnlyMarkedCells()
        {
            var authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            var leftCell = CreateCell("Cell_Left", new Vector2Int(0, 0), textComponentCount: 1);
            var rightCell = CreateCell("Cell_Right", new Vector2Int(1, 0), textComponentCount: 1);
            var leftText = leftCell.GetComponentInChildren<TMP_Text>(true);
            var rightText = rightCell.GetComponentInChildren<TMP_Text>(true);
            leftText.text = "A";
            rightText.text = "B";

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, leftCell),
                new CellEntry(1, 0, rightCell),
            });

            var fullRefreshSuccess = authoring.TryRefreshGroundWallState(out var fullRefreshError);
            Assert.That(fullRefreshSuccess, Is.True, fullRefreshError);

            leftText.text = "   ";
            var markDirtySuccess = authoring.TryMarkCellSurfaceDirty(new Vector2Int(0, 0), out var markDirtyError);
            Assert.That(markDirtySuccess, Is.True, markDirtyError);

            var dirtyRefreshSuccess = authoring.TryRefreshDirtyGroundWallState(out var refreshedCellCount, out var dirtyRefreshError);

            Assert.That(dirtyRefreshSuccess, Is.True, dirtyRefreshError);
            Assert.That(refreshedCellCount, Is.EqualTo(1));
            Assert.That(authoring.DirtyCellSurfaceCount, Is.Zero);

            Assert.That(leftCell.tag, Is.EqualTo("Ground"));
            Assert.That(leftCell.GetComponent<CellData>().ManagedCollider.gameObject.tag, Is.EqualTo("Ground"));
            Assert.That(leftCell.GetComponent<CellData>().IsColliderEnabled, Is.False);

            Assert.That(rightCell.tag, Is.EqualTo("Wall"));
            Assert.That(rightCell.GetComponent<CellData>().ManagedCollider.gameObject.tag, Is.EqualTo("Wall"));
            Assert.That(rightCell.GetComponent<CellData>().IsColliderEnabled, Is.True);
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

        private GameObject CreateCell(
            string name,
            Vector2Int coordinates,
            int textComponentCount,
            bool includeManagedCollider = true,
            bool includeCellData = true)
        {
            var cell = CreateGameObject(name);
            if (includeCellData)
            {
                var cellData = cell.AddComponent<CellData>();
                cellData.SetCoordinates(coordinates);
            }

            for (var i = 0; i < textComponentCount; i++)
            {
                var textObject = new GameObject($"Text_{i}", typeof(RectTransform));
                createdObjects.Add(textObject);
                textObject.transform.SetParent(cell.transform, false);
                textObject.AddComponent<TextMeshPro>();
            }

            if (includeManagedCollider)
            {
                var colliderObject = new GameObject("Collider");
                createdObjects.Add(colliderObject);
                colliderObject.transform.SetParent(cell.transform, false);
                colliderObject.AddComponent<BoxCollider>();
            }

            return cell;
        }

        private void CreateIndexedCellGrid(MapGridAuthoring authoring, int width, int height, out TMP_Text[,] texts, out CellData[,] cells)
        {
            authoring.GridWidth = width;
            authoring.GridHeight = height;

            texts = new TMP_Text[width, height];
            cells = new CellData[width, height];
            var entries = new List<CellEntry>(width * height);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = CreateCell($"Cell_{x}_{y}", new Vector2Int(x, y), textComponentCount: 1);
                    texts[x, y] = cell.GetComponentInChildren<TMP_Text>(true);
                    cells[x, y] = cell.GetComponent<CellData>();
                    entries.Add(new CellEntry(x, y, cell));
                }
            }

            authoring.ReplaceCellEntries(entries);
        }

        private void ApplyRectangleText(MapGridAuthoring authoring, Vector2Int start, Vector2Int end, string text)
        {
            var coordinates = MapGridEditorUtility.BuildRectangleCoordinates(start, end);
            for (var i = 0; i < coordinates.Count; i++)
            {
                var success = MapGridEditorUtility.TrySetCellText(authoring, coordinates[i], text, out _, out var error);
                Assert.That(success, Is.True, error);
            }
        }

        private void ApplyRectangleCollider(MapGridAuthoring authoring, Vector2Int start, Vector2Int end, bool enabled)
        {
            var coordinates = MapGridEditorUtility.BuildRectangleCoordinates(start, end);
            for (var i = 0; i < coordinates.Count; i++)
            {
                var success = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, coordinates[i], enabled, out _, out var error);
                Assert.That(success, Is.True, error);
            }
        }

        private static bool IsInsideRectangle(int x, int y, Vector2Int start, Vector2Int end)
        {
            var minX = Mathf.Min(start.x, end.x);
            var maxX = Mathf.Max(start.x, end.x);
            var minY = Mathf.Min(start.y, end.y);
            var maxY = Mathf.Max(start.y, end.y);
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        private GameObject CreateChildObject(GameObject parent, string name, params System.Type[] components)
        {
            var child = components == null || components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
            createdObjects.Add(child);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
