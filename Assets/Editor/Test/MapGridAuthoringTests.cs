using System.Collections.Generic;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kernel.MapGrid.Editor.Tests
{
    public sealed class MapGridAuthoringTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                GameObject createdObject = createdObjects[i];
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void GetCellLocalPosition_UsesXZPlane()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.CellSize = new Vector2(1.5f, 2.25f);

            Vector3 localPosition = authoring.GetCellLocalPosition(2, 3);

            Assert.That(localPosition, Is.EqualTo(new Vector3(3f, 0f, 6.75f)));
        }

        [Test]
        public void TryGetCellCoordinateFromLocalPoint_UsesCenteredCellAnchorsOnXZ()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 3;
            authoring.CellSize = new Vector2(8f, 8f);

            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(0f, 0f, 0f), out Vector2Int originCell), Is.True);
            Assert.That(originCell, Is.EqualTo(new Vector2Int(0, 0)));

            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(12f, 0f, 8f), out Vector2Int centerCell), Is.True);
            Assert.That(centerCell, Is.EqualTo(new Vector2Int(2, 1)));

            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(-4.1f, 0f, 0f), out _), Is.False);
            Assert.That(authoring.TryGetCellCoordinateFromLocalPoint(new Vector3(28.1f, 0f, 0f), out _), Is.False);
        }

        [Test]
        public void ChunkCoordinates_RespectConfiguredChunkSize()
        {
            MapGridAuthoring authoring = CreateAuthoring();
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
        public void GetGridLocalCenter_UsesCellCentersOnXZ()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 3;
            authoring.CellSize = new Vector2(8f, 8f);

            Vector3 localCenter = authoring.GetGridLocalCenter();

            Assert.That(localCenter, Is.EqualTo(new Vector3(12f, 0f, 8f)));
        }

        [Test]
        public void ReplaceCellEntries_BuildsQueryableIndex()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 4;

            GameObject cellA = CreateGameObject("Cell_A");
            GameObject cellB = CreateGameObject("Cell_B");

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cellA),
                new CellEntry(1, 2, cellB),
            });

            Assert.That(authoring.ContainsCell(0, 0), Is.True);
            Assert.That(authoring.TryGetCell(new Vector2Int(1, 2), out GameObject resolvedCell), Is.True);
            Assert.That(resolvedCell, Is.SameAs(cellB));
            Assert.That(authoring.GetCell(3, 3), Is.Null);
        }

        [Test]
        public void BuildStrokeCoordinates_InterpolatesAllVisitedCells()
        {
            List<Vector2Int> coordinates = MapGridEditorUtility.BuildStrokeCoordinates(new Vector2Int(0, 0), new Vector2Int(3, 2));

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
            List<Vector2Int> coordinates = MapGridEditorUtility.BuildRectangleCoordinates(new Vector2Int(1, 1), new Vector2Int(3, 2));

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
        public void TrySetCellSurfaceType_UpdatesOnlyTheTargetCell()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            CellData cellA = CreateCell("Cell_A", new Vector2Int(0, 0));
            CellData cellB = CreateCell("Cell_B", new Vector2Int(1, 0));
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cellA.gameObject),
                new CellEntry(1, 0, cellB.gameObject),
            });

            bool success = MapGridEditorUtility.TrySetCellSurfaceType(authoring, new Vector2Int(1, 0), CellData.CellSurfaceType.Wall, out bool changed, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(changed, Is.True);
            Assert.That(cellA.SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Ground));
            Assert.That(cellB.SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Wall));
            Assert.That(cellA.GroundCollider.enabled, Is.True);
            Assert.That(cellA.WallCollider.enabled, Is.False);
            Assert.That(cellB.GroundCollider.enabled, Is.False);
            Assert.That(cellB.WallCollider.enabled, Is.True);
        }

        [Test]
        public void TrySetCellColliderEnabled_TogglesOnlyActiveCollider()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            CellData cell = CreateCell("Cell_A", new Vector2Int(0, 0), CellData.CellSurfaceType.Wall);
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell.gameObject),
            });

            bool disableSuccess = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), false, out bool disableChanged, out string disableError);

            Assert.That(disableSuccess, Is.True, disableError);
            Assert.That(disableChanged, Is.True);
            Assert.That(cell.WallCollider.enabled, Is.False);
            Assert.That(cell.GroundCollider.enabled, Is.False);

            bool enableSuccess = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), true, out bool enableChanged, out string enableError);

            Assert.That(enableSuccess, Is.True, enableError);
            Assert.That(enableChanged, Is.True);
            Assert.That(cell.WallCollider.enabled, Is.True);
            Assert.That(cell.GroundCollider.enabled, Is.False);
        }

        [Test]
        public void TrySetCellColliderEnabled_FailsWhenCellDataIsMissing()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            GameObject cell = CreateGameObject("Cell_A");
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell),
            });

            bool success = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), true, out _, out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains("CellData component", error);
        }

        [Test]
        public void TrySetCellColliderEnabled_FailsWhenManagedColliderIsMissing()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 1;
            authoring.GridHeight = 1;

            CellData cell = CreateCell("Cell_A", new Vector2Int(0, 0), includeGroundCollider: false);
            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, cell.gameObject),
            });

            bool success = MapGridEditorUtility.TrySetCellColliderEnabled(authoring, new Vector2Int(0, 0), true, out _, out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains("managed Collider", error);
        }

        [Test]
        public void RectanglePaintWall_UpdatesOnlyCellsInsideTheSelection()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            CreateIndexedCellGrid(authoring, 3, 3, out CellData[,] cells);

            ApplyRectangleSurface(authoring, new Vector2Int(0, 1), new Vector2Int(1, 2), CellData.CellSurfaceType.Wall);

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    bool isInside = IsInsideRectangle(x, y, new Vector2Int(0, 1), new Vector2Int(1, 2));
                    Assert.That(cells[x, y].SurfaceType, Is.EqualTo(isInside ? CellData.CellSurfaceType.Wall : CellData.CellSurfaceType.Ground));
                }
            }
        }

        [Test]
        public void RectanglePaintGround_UpdatesOnlyCellsInsideTheSelection()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            CreateIndexedCellGrid(authoring, 3, 3, out CellData[,] cells, CellData.CellSurfaceType.Wall);

            ApplyRectangleSurface(authoring, new Vector2Int(1, 0), new Vector2Int(2, 1), CellData.CellSurfaceType.Ground);

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    bool isInside = IsInsideRectangle(x, y, new Vector2Int(1, 0), new Vector2Int(2, 1));
                    Assert.That(cells[x, y].SurfaceType, Is.EqualTo(isInside ? CellData.CellSurfaceType.Ground : CellData.CellSurfaceType.Wall));
                }
            }
        }

        [Test]
        public void CellData_SurfaceSwitch_SyncsModelsCollidersAndTags()
        {
            CellData cell = CreateCell("Cell_A", new Vector2Int(0, 0));

            Assert.That(cell.SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Ground));
            Assert.That(cell.GroundModelRoot.gameObject.activeSelf, Is.True);
            Assert.That(cell.WallModelRoot.gameObject.activeSelf, Is.False);
            Assert.That(cell.GroundCollider.enabled, Is.True);
            Assert.That(cell.WallCollider.enabled, Is.False);
            Assert.That(cell.ManagedCollider, Is.SameAs(cell.GroundCollider));

            bool success = cell.TrySetSurfaceType(CellData.CellSurfaceType.Wall);

            Assert.That(success, Is.True);
            Assert.That(cell.GroundModelRoot.gameObject.activeSelf, Is.False);
            Assert.That(cell.WallModelRoot.gameObject.activeSelf, Is.True);
            Assert.That(cell.GroundCollider.enabled, Is.False);
            Assert.That(cell.WallCollider.enabled, Is.True);
            Assert.That(cell.ManagedCollider, Is.SameAs(cell.WallCollider));
            Assert.That(cell.gameObject.tag, Is.EqualTo(MapGridAuthoring.WallTagName));
            Assert.That(cell.ManagedCollider.gameObject.tag, Is.EqualTo(MapGridAuthoring.WallTagName));
        }

        [Test]
        public void CellData_TrySetWorldPosition_MovesCellRootByDefault()
        {
            CellData cell = CreateCell("Cell_A", new Vector2Int(0, 0));
            Vector3 targetPosition = new Vector3(3f, 4f, 5f);

            bool success = cell.TrySetWorldPosition(targetPosition);

            Assert.That(success, Is.True);
            Assert.That(cell.transform.position, Is.EqualTo(targetPosition));
            Assert.That(cell.MovementTarget, Is.SameAs(cell.transform));
        }

        [Test]
        public void CellData_TryBindMovementTarget_UsesChildTransformForTranslation()
        {
            CellData cell = CreateCell("Cell_A", new Vector2Int(0, 0));
            GameObject movementObject = CreateChildObject(cell.gameObject, "Movement");

            bool bindSuccess = cell.TryBindMovementTarget(movementObject.transform);
            bool translateSuccess = cell.TryTranslate(new Vector3(2f, 0f, 0f), Space.Self);

            Assert.That(bindSuccess, Is.True);
            Assert.That(translateSuccess, Is.True);
            Assert.That(movementObject.transform.localPosition, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(cell.transform.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CellData_TrySetLinearVelocity_UsesBoundRigidbody()
        {
            CellData cell = CreateCell("Cell_A", new Vector2Int(0, 0));
            GameObject movementObject = CreateChildObject(cell.gameObject, "Movement", typeof(Rigidbody));
            Rigidbody movementRigidbody = movementObject.GetComponent<Rigidbody>();

            bool bindSuccess = cell.TryBindMovementTarget(movementObject.transform, movementRigidbody);
            bool velocitySuccess = cell.TrySetLinearVelocity(new Vector3(1f, 2f, 3f));
            bool stopSuccess = cell.TryStopMovement();

            Assert.That(bindSuccess, Is.True);
            Assert.That(velocitySuccess, Is.True);
            Assert.That(cell.MovementTarget, Is.SameAs(movementObject.transform));
            Assert.That(cell.MovementRigidbody, Is.SameAs(movementRigidbody));
            Assert.That(stopSuccess, Is.True);
        }

        [Test]
        public void TryInitializeCellSurfaceCache_BuildsRuntimeCacheAndLeavesDirtySetEmpty()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            CellData groundCell = CreateCell("Cell_Ground", new Vector2Int(0, 0), CellData.CellSurfaceType.Ground);
            CellData wallCell = CreateCell("Cell_Wall", new Vector2Int(1, 0), CellData.CellSurfaceType.Wall);

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, groundCell.gameObject),
                new CellEntry(1, 0, wallCell.gameObject),
            });

            bool success = authoring.TryInitializeCellSurfaceCache(out string error);

            Assert.That(success, Is.True, error);
            Assert.That(authoring.IsCellSurfaceCacheInitialized, Is.True);
            Assert.That(authoring.CellSurfaceCacheCount, Is.EqualTo(2));
            Assert.That(authoring.DirtyCellSurfaceCount, Is.Zero);
        }

        [Test]
        public void TryRefreshGroundWallState_NormalizesTagsAndPresentationFromSurfaceType()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            CellData wallCell = CreateCell("Cell_Wall", new Vector2Int(0, 0), CellData.CellSurfaceType.Wall);
            CellData groundCell = CreateCell("Cell_Ground", new Vector2Int(1, 0), CellData.CellSurfaceType.Ground);

            wallCell.GroundModelRoot.gameObject.SetActive(true);
            wallCell.WallModelRoot.gameObject.SetActive(false);
            wallCell.GroundCollider.enabled = true;
            wallCell.WallCollider.enabled = false;
            wallCell.gameObject.tag = MapGridAuthoring.GroundTagName;

            groundCell.GroundModelRoot.gameObject.SetActive(false);
            groundCell.WallModelRoot.gameObject.SetActive(true);
            groundCell.GroundCollider.enabled = false;
            groundCell.WallCollider.enabled = true;
            groundCell.gameObject.tag = MapGridAuthoring.WallTagName;

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, wallCell.gameObject),
                new CellEntry(1, 0, groundCell.gameObject),
            });

            bool success = authoring.TryRefreshGroundWallState(out string error);

            Assert.That(success, Is.True, error);
            Assert.That(wallCell.IsSurfacePresentationCurrent(), Is.True);
            Assert.That(groundCell.IsSurfacePresentationCurrent(), Is.True);
            Assert.That(wallCell.gameObject.tag, Is.EqualTo(MapGridAuthoring.WallTagName));
            Assert.That(groundCell.gameObject.tag, Is.EqualTo(MapGridAuthoring.GroundTagName));
        }

        [Test]
        public void TryRefreshDirtyGroundWallState_UpdatesOnlyMarkedCells()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 1;

            CellData leftCell = CreateCell("Cell_Left", new Vector2Int(0, 0), CellData.CellSurfaceType.Ground);
            CellData rightCell = CreateCell("Cell_Right", new Vector2Int(1, 0), CellData.CellSurfaceType.Wall);

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(0, 0, leftCell.gameObject),
                new CellEntry(1, 0, rightCell.gameObject),
            });

            bool fullRefreshSuccess = authoring.TryRefreshGroundWallState(out string fullRefreshError);
            Assert.That(fullRefreshSuccess, Is.True, fullRefreshError);

            leftCell.GroundCollider.enabled = false;
            leftCell.GroundModelRoot.gameObject.SetActive(false);
            leftCell.WallModelRoot.gameObject.SetActive(true);
            leftCell.gameObject.tag = MapGridAuthoring.WallTagName;

            bool markDirtySuccess = authoring.TryMarkCellSurfaceDirty(new Vector2Int(0, 0), out string markDirtyError);
            Assert.That(markDirtySuccess, Is.True, markDirtyError);

            bool dirtyRefreshSuccess = authoring.TryRefreshDirtyGroundWallState(out int refreshedCellCount, out string dirtyRefreshError);

            Assert.That(dirtyRefreshSuccess, Is.True, dirtyRefreshError);
            Assert.That(refreshedCellCount, Is.EqualTo(1));
            Assert.That(authoring.DirtyCellSurfaceCount, Is.Zero);
            Assert.That(leftCell.IsSurfacePresentationCurrent(), Is.True);
            Assert.That(rightCell.IsSurfacePresentationCurrent(), Is.True);
        }

        [Test]
        public void TryRebuildLookupFromEntries_FailsOnDuplicateCoordinates()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 4;
            authoring.GridHeight = 4;

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(1, 1, CreateGameObject("Cell_1")),
                new CellEntry(1, 1, CreateGameObject("Cell_2")),
            });

            bool success = authoring.TryRebuildLookupFromEntries(out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains("Duplicate", error);
            Assert.That(authoring.ContainsCell(1, 1), Is.False);
        }

        [Test]
        public void TryRebuildLookupFromEntries_FailsOnOutOfBoundsCoordinates()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 2;

            authoring.ReplaceCellEntries(new[]
            {
                new CellEntry(2, 0, CreateGameObject("Cell_OutOfBounds")),
            });

            bool success = authoring.TryRebuildLookupFromEntries(out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains("out of grid bounds", error);
        }

        [Test]
        public void GenerateGrid_PlacesCellsOnXZPlane()
        {
            MapGridAuthoring authoring = CreateAuthoring();
            authoring.GridWidth = 2;
            authoring.GridHeight = 2;
            authoring.CellSize = new Vector2(24f, 24f);
            authoring.ChunkWidthInCells = 2;
            authoring.ChunkHeightInCells = 2;
            authoring.DefaultCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Map/Cell3D.prefab");
            ConfigureCoordinateBinding(authoring);

            bool success = MapGridEditorUtility.GenerateGrid(authoring, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(authoring.TryGetCell(new Vector2Int(1, 1), out GameObject cellObject), Is.True);
            Assert.That(cellObject.transform.localPosition, Is.EqualTo(new Vector3(24f, 0f, 24f)));
            Assert.That(cellObject.GetComponent<CellData>().SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Ground));
        }

        private MapGridAuthoring CreateAuthoring()
        {
            GameObject mapRoot = CreateGameObject("MapRoot");
            MapGridAuthoring authoring = mapRoot.AddComponent<MapGridAuthoring>();
            ConfigureCoordinateBinding(authoring);
            return authoring;
        }

        private void ConfigureCoordinateBinding(MapGridAuthoring authoring)
        {
            authoring.CoordinateBinding.ComponentTypeName = nameof(CellData);
            authoring.CoordinateBinding.SetCoordinatesMethodName = nameof(CellData.SetCoordinates);
            authoring.CoordinateBinding.GetCoordinatesMethodName = nameof(CellData.GetCoordinates);
            authoring.CoordinateBinding.XMemberName = string.Empty;
            authoring.CoordinateBinding.YMemberName = string.Empty;
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private CellData CreateCell(
            string name,
            Vector2Int coordinates,
            CellData.CellSurfaceType surfaceType = CellData.CellSurfaceType.Ground,
            bool includeWallCollider = true,
            bool includeGroundCollider = true)
        {
            GameObject cell = CreateGameObject(name);
            GameObject modelRoot = CreateChildObject(cell, "Model");
            GameObject wallModel = CreateChildObject(modelRoot, "wall Model");
            GameObject groundModel = CreateChildObject(modelRoot, "Ground Model");
            wallModel.SetActive(false);

            if (includeWallCollider)
            {
                BoxCollider wallCollider = cell.AddComponent<BoxCollider>();
                wallCollider.size = new Vector3(20f, 20f, 20f);
                wallCollider.center = new Vector3(0f, 10f, 0f);
            }

            if (includeGroundCollider)
            {
                BoxCollider groundCollider = cell.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(20f, 1f, 20f);
                groundCollider.center = new Vector3(0f, -0.5f, 0f);
            }

            Rigidbody rigidbody = cell.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            CellData cellData = cell.AddComponent<CellData>();
            cellData.SetCoordinates(coordinates);
            if (surfaceType == CellData.CellSurfaceType.Wall)
            {
                Assert.That(cellData.TrySetSurfaceType(CellData.CellSurfaceType.Wall), Is.True);
            }
            else
            {
                Assert.That(cellData.TryRefreshSurfacePresentation(), Is.EqualTo(includeGroundCollider));
            }

            return cellData;
        }

        private void CreateIndexedCellGrid(MapGridAuthoring authoring, int width, int height, out CellData[,] cells, CellData.CellSurfaceType defaultSurface = CellData.CellSurfaceType.Ground)
        {
            authoring.GridWidth = width;
            authoring.GridHeight = height;

            cells = new CellData[width, height];
            var entries = new List<CellEntry>(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CellData cell = CreateCell($"Cell_{x}_{y}", new Vector2Int(x, y), defaultSurface);
                    cells[x, y] = cell;
                    entries.Add(new CellEntry(x, y, cell.gameObject));
                }
            }

            authoring.ReplaceCellEntries(entries);
        }

        private void ApplyRectangleSurface(MapGridAuthoring authoring, Vector2Int start, Vector2Int end, CellData.CellSurfaceType surfaceType)
        {
            List<Vector2Int> coordinates = MapGridEditorUtility.BuildRectangleCoordinates(start, end);
            for (int i = 0; i < coordinates.Count; i++)
            {
                bool success = MapGridEditorUtility.TrySetCellSurfaceType(authoring, coordinates[i], surfaceType, out _, out string error);
                Assert.That(success, Is.True, error);
            }
        }

        private static bool IsInsideRectangle(int x, int y, Vector2Int start, Vector2Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        private GameObject CreateChildObject(GameObject parent, string name, params System.Type[] components)
        {
            GameObject child = components == null || components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
            createdObjects.Add(child);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
