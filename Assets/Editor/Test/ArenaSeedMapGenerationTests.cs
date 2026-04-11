using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kernel.MapGrid.Editor.Tests
{
    public sealed class ArenaSeedMapGenerationTests
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
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ArenaSeedLayoutBuilder_SameSeedAndInputs_ProducesIdenticalLayout()
        {
            ArenaSeedLayoutSettings settings = CreateSettings();

            bool leftSuccess = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(16, 10), 42, new Vector2Int(8, 5), 4, settings, out List<CellData.CellSurfaceType> leftLayout, out string leftError);
            bool rightSuccess = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(16, 10), 42, new Vector2Int(8, 5), 4, settings, out List<CellData.CellSurfaceType> rightLayout, out string rightError);

            Assert.That(leftSuccess, Is.True, leftError);
            Assert.That(rightSuccess, Is.True, rightError);
            CollectionAssert.AreEqual(leftLayout, rightLayout);
        }

        [Test]
        public void ArenaSeedLayoutBuilder_DifferentSeeds_ProduceDifferentWallCoordinates()
        {
            ArenaSeedLayoutSettings settings = CreateSettings(
                obstacleCountMin: 10,
                obstacleCountMax: 10,
                edgeClearanceCells: 1,
                playerSafeRadiusCells: 1,
                spawnAnnulusHalfWidthCells: 0);

            bool leftSuccess = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(24, 16), 11, new Vector2Int(12, 8), null, settings, out List<CellData.CellSurfaceType> leftLayout, out string leftError);
            bool rightSuccess = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(24, 16), 19, new Vector2Int(12, 8), null, settings, out List<CellData.CellSurfaceType> rightLayout, out string rightError);

            Assert.That(leftSuccess, Is.True, leftError);
            Assert.That(rightSuccess, Is.True, rightError);
            CollectionAssert.AreNotEqual(GetWallCoordinates(leftLayout, 24), GetWallCoordinates(rightLayout, 24));
        }

        [Test]
        public void ArenaSeedLayoutBuilder_DensitySettingsIncreaseWallCoverage()
        {
            Vector2Int gridSize = new(24, 16);
            Vector2Int playerCell = new(12, 8);
            ArenaSeedLayoutSettings openSettings = CreateSettings(obstacleCountMin: 0, obstacleCountMax: 0, spawnAnnulusHalfWidthCells: 0);
            ArenaSeedLayoutSettings denseSettings = CreateSettings(
                obstacleCountMin: 12,
                obstacleCountMax: 12,
                obstacleWidthRange: new Vector2Int(3, 3),
                obstacleHeightRange: new Vector2Int(3, 3),
                edgeClearanceCells: 1,
                playerSafeRadiusCells: 1,
                spawnAnnulusHalfWidthCells: 0);

            for (int seed = 0; seed < 64; seed++)
            {
                bool openSuccess = ArenaSeedLayoutBuilder.TryBuildLayout(gridSize, seed, playerCell, null, openSettings, out List<CellData.CellSurfaceType> openLayout, out string openError);
                bool denseSuccess = ArenaSeedLayoutBuilder.TryBuildLayout(gridSize, seed, playerCell, null, denseSettings, out List<CellData.CellSurfaceType> denseLayout, out string denseError);

                Assert.That(openSuccess, Is.True, openError);
                Assert.That(denseSuccess, Is.True, denseError);

                if (CountCells(denseLayout, CellData.CellSurfaceType.Wall) > CountCells(openLayout, CellData.CellSurfaceType.Wall))
                {
                    return;
                }
            }

            Assert.Fail("Expected denser tuning parameters to produce more wall cells than the open baseline.");
        }

        [Test]
        public void ArenaSeedLayoutBuilder_BorderWallsAlwaysApplied()
        {
            ArenaSeedLayoutSettings settings = CreateSettings(obstacleCountMin: 0, obstacleCountMax: 0);

            bool success = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(8, 6), 5, new Vector2Int(4, 3), null, settings, out List<CellData.CellSurfaceType> layout, out string error);

            Assert.That(success, Is.True, error);
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == 7 || y == 5;
                    CellData.CellSurfaceType surface = layout[ArenaSeedLayoutBuilder.GetRowMajorIndex(x, y, 8)];
                    Assert.That(surface, Is.EqualTo(isBorder ? CellData.CellSurfaceType.Wall : CellData.CellSurfaceType.Ground));
                }
            }
        }

        [Test]
        public void ArenaSeedLayoutBuilder_PlayerSafeZoneRemainsGround()
        {
            ArenaSeedLayoutSettings settings = CreateSettings(playerSafeRadiusCells: 2);
            Vector2Int playerCell = new(8, 5);

            bool success = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(16, 10), 73, playerCell, 4, settings, out List<CellData.CellSurfaceType> layout, out string error);

            Assert.That(success, Is.True, error);
            AssertGroundDisk(layout, 16, playerCell, 2);
        }

        [Test]
        public void ArenaSeedLayoutBuilder_SpawnAnnulusRemainsGroundWhenProvided()
        {
            ArenaSeedLayoutSettings settings = CreateSettings(spawnAnnulusHalfWidthCells: 1);
            Vector2Int playerCell = new(12, 8);

            bool success = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(24, 16), 101, playerCell, 5, settings, out List<CellData.CellSurfaceType> layout, out string error);

            Assert.That(success, Is.True, error);
            AssertGroundAnnulus(layout, 24, 16, playerCell, 5, 1);
        }

        [Test]
        public void ArenaSeedLayoutBuilder_DisconnectedObstaclePlacementIsRejected()
        {
            ArenaSeedLayoutSettings settings = CreateSettings(
                obstacleCountMin: 1,
                obstacleCountMax: 1,
                obstacleWidthRange: new Vector2Int(1, 1),
                obstacleHeightRange: new Vector2Int(3, 3),
                edgeClearanceCells: 0,
                playerSafeRadiusCells: 0,
                spawnAnnulusHalfWidthCells: 0,
                maxPlacementAttemptsPerObstacle: 1);

            bool foundRejectedSeed = false;
            for (int seed = 0; seed < 256; seed++)
            {
                bool success = ArenaSeedLayoutBuilder.TryBuildLayout(new Vector2Int(6, 5), seed, new Vector2Int(1, 2), 2, settings, out List<CellData.CellSurfaceType> layout, out string error);
                Assert.That(success, Is.True, error);

                int wallCount = CountCells(layout, CellData.CellSurfaceType.Wall);
                if (wallCount != GetBorderWallCount(6, 5, 1))
                {
                    continue;
                }

                foundRejectedSeed = true;
                Assert.That(AllGroundCellsReachable(layout, 6, 5, new Vector2Int(1, 2)), Is.True);
                break;
            }

            Assert.That(foundRejectedSeed, Is.True, "Expected at least one seed to produce a rejected obstacle placement.");
        }

        [Test]
        public void TryApplySurfaceLayout_RejectsWrongLength()
        {
            MapGridAuthoring authoring = CreateAuthoring(2, 2);
            CreateIndexedCellGrid(authoring, 2, 2);

            bool success = authoring.TryApplySurfaceLayout(
                new[]
                {
                    CellData.CellSurfaceType.Ground,
                    CellData.CellSurfaceType.Wall,
                },
                out string error);

            Assert.That(success, Is.False);
            StringAssert.Contains("expects 4", error);
        }

        [Test]
        public void TryApplySurfaceLayout_AppliesValidRowMajorLayout()
        {
            MapGridAuthoring authoring = CreateAuthoring(2, 2);
            CreateIndexedCellGrid(authoring, 2, 2, out CellData[,] cells);

            bool success = authoring.TryApplySurfaceLayout(
                new[]
                {
                    CellData.CellSurfaceType.Wall,
                    CellData.CellSurfaceType.Ground,
                    CellData.CellSurfaceType.Ground,
                    CellData.CellSurfaceType.Wall,
                },
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(cells[0, 0].SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Wall));
            Assert.That(cells[1, 0].SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Ground));
            Assert.That(cells[0, 1].SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Ground));
            Assert.That(cells[1, 1].SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Wall));
            Assert.That(cells[0, 0].IsSurfacePresentationCurrent(), Is.True);
            Assert.That(cells[1, 1].IsSurfacePresentationCurrent(), Is.True);
        }

        [Test]
        public void ArenaSeedMapGenerator_TrySnapPlayerToNearestGroundCell_UsesNearestGroundCell()
        {
            MapGridAuthoring authoring = CreateAuthoring(5, 5);
            CreateIndexedCellGrid(authoring, 5, 5);
            Transform player = CreatePlayer(authoring.GetCellWorldPosition(2, 2));
            CreateEnemyGenerator();
            ArenaSeedMapGenerator generator = CreateSeedGenerator(authoring);

            CellData.CellSurfaceType[] layout = CreateFilledLayout(5, 5, CellData.CellSurfaceType.Wall);
            layout[ArenaSeedLayoutBuilder.GetRowMajorIndex(3, 2, 5)] = CellData.CellSurfaceType.Ground;
            bool layoutSuccess = authoring.TryApplySurfaceLayout(layout, out string layoutError);
            Assert.That(layoutSuccess, Is.True, layoutError);

            bool success = generator.TrySnapPlayerToNearestGroundCell(out string error);

            Assert.That(success, Is.True, error);
            Vector3 expectedPosition = authoring.GetCellWorldPosition(3, 2);
            Assert.That(player.position.x, Is.EqualTo(expectedPosition.x).Within(0.001f));
            Assert.That(player.position.z, Is.EqualTo(expectedPosition.z).Within(0.001f));
        }

        [Test]
        public void ArenaSeedMapGenerator_PreviewPath_IsStablePerSeedAndChangesWhenSeedChanges()
        {
            MapGridAuthoring authoring = CreateAuthoring(24, 16);
            CreateIndexedCellGrid(authoring, 24, 16);
            CreatePlayer(authoring.GetCellWorldPosition(12, 8));
            CreateEnemyGenerator();
            ArenaSeedMapGenerator generator = CreateSeedGenerator(authoring);
            generator.Seed = 314159;

            bool firstSuccess = generator.TryGenerateAndApplyLayout(out string firstError);
            Assert.That(firstSuccess, Is.True, firstError);
            List<Vector2Int> firstWalls = GetWallCoordinates(authoring);
            int firstWallCount = firstWalls.Count;

            bool secondSuccess = generator.TryGenerateAndApplyLayout(out string secondError);
            Assert.That(secondSuccess, Is.True, secondError);
            List<Vector2Int> secondWalls = GetWallCoordinates(authoring);

            Assert.That(secondWalls.Count, Is.EqualTo(firstWallCount));
            CollectionAssert.AreEqual(firstWalls, secondWalls);

            int previousSeed = generator.Seed;
            do
            {
                generator.RandomizeSeed();
            }
            while (generator.Seed == previousSeed);

            bool thirdSuccess = generator.TryGenerateAndApplyLayout(out string thirdError);
            Assert.That(thirdSuccess, Is.True, thirdError);
            List<Vector2Int> thirdWalls = GetWallCoordinates(authoring);

            CollectionAssert.AreNotEqual(firstWalls, thirdWalls);
        }

        private ArenaSeedLayoutSettings CreateSettings(
            int borderWallThickness = 1,
            int obstacleCountMin = 6,
            int obstacleCountMax = 10,
            Vector2Int? obstacleWidthRange = null,
            Vector2Int? obstacleHeightRange = null,
            int edgeClearanceCells = 2,
            int playerSafeRadiusCells = 2,
            int spawnAnnulusHalfWidthCells = 1,
            int maxPlacementAttemptsPerObstacle = 24)
        {
            return new ArenaSeedLayoutSettings(
                borderWallThickness,
                obstacleCountMin,
                obstacleCountMax,
                obstacleWidthRange ?? new Vector2Int(2, 5),
                obstacleHeightRange ?? new Vector2Int(2, 4),
                edgeClearanceCells,
                playerSafeRadiusCells,
                spawnAnnulusHalfWidthCells,
                maxPlacementAttemptsPerObstacle);
        }

        private static CellData.CellSurfaceType[] CreateFilledLayout(int width, int height, CellData.CellSurfaceType surfaceType)
        {
            var layout = new CellData.CellSurfaceType[width * height];
            for (int index = 0; index < layout.Length; index++)
            {
                layout[index] = surfaceType;
            }

            return layout;
        }

        private static int CountCells(IReadOnlyList<CellData.CellSurfaceType> layout, CellData.CellSurfaceType surfaceType)
        {
            int count = 0;
            for (int index = 0; index < layout.Count; index++)
            {
                if (layout[index] == surfaceType)
                {
                    count++;
                }
            }

            return count;
        }

        private static int GetBorderWallCount(int width, int height, int borderThickness)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < borderThickness ||
                        y < borderThickness ||
                        x >= width - borderThickness ||
                        y >= height - borderThickness)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool AllGroundCellsReachable(IReadOnlyList<CellData.CellSurfaceType> layout, int width, int height, Vector2Int start)
        {
            int startIndex = ArenaSeedLayoutBuilder.GetRowMajorIndex(start.x, start.y, width);
            if (layout[startIndex] != CellData.CellSurfaceType.Ground)
            {
                return false;
            }

            int groundCellCount = CountCells(layout, CellData.CellSurfaceType.Ground);
            var visited = new bool[layout.Count];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited[startIndex] = true;
            int reachableCount = 1;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                TryVisit(current + Vector2Int.right);
                TryVisit(current + Vector2Int.left);
                TryVisit(current + Vector2Int.up);
                TryVisit(current + Vector2Int.down);
            }

            return reachableCount == groundCellCount;

            void TryVisit(Vector2Int next)
            {
                if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height)
                {
                    return;
                }

                int nextIndex = ArenaSeedLayoutBuilder.GetRowMajorIndex(next.x, next.y, width);
                if (visited[nextIndex] || layout[nextIndex] != CellData.CellSurfaceType.Ground)
                {
                    return;
                }

                visited[nextIndex] = true;
                reachableCount++;
                queue.Enqueue(next);
            }
        }

        private static void AssertGroundDisk(IReadOnlyList<CellData.CellSurfaceType> layout, int width, Vector2Int center, int radius)
        {
            int radiusSquared = radius * radius;
            int height = layout.Count / width;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - center.x;
                    int dy = y - center.y;
                    if ((dx * dx) + (dy * dy) > radiusSquared)
                    {
                        continue;
                    }

                    Assert.That(layout[ArenaSeedLayoutBuilder.GetRowMajorIndex(x, y, width)], Is.EqualTo(CellData.CellSurfaceType.Ground));
                }
            }
        }

        private static void AssertGroundAnnulus(
            IReadOnlyList<CellData.CellSurfaceType> layout,
            int width,
            int height,
            Vector2Int center,
            int annulusRadius,
            int halfWidth)
        {
            int outerRadius = annulusRadius + halfWidth;
            int innerRadius = Mathf.Max(0, annulusRadius - halfWidth);
            int outerRadiusSquared = outerRadius * outerRadius;
            int innerRadiusSquared = innerRadius * innerRadius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - center.x;
                    int dy = y - center.y;
                    int distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared < innerRadiusSquared || distanceSquared > outerRadiusSquared)
                    {
                        continue;
                    }

                    Assert.That(layout[ArenaSeedLayoutBuilder.GetRowMajorIndex(x, y, width)], Is.EqualTo(CellData.CellSurfaceType.Ground));
                }
            }
        }

        private static List<Vector2Int> GetWallCoordinates(IReadOnlyList<CellData.CellSurfaceType> layout, int width)
        {
            var coordinates = new List<Vector2Int>();
            for (int index = 0; index < layout.Count; index++)
            {
                if (layout[index] != CellData.CellSurfaceType.Wall)
                {
                    continue;
                }

                int x = index % width;
                int y = index / width;
                coordinates.Add(new Vector2Int(x, y));
            }

            return coordinates;
        }

        private static List<Vector2Int> GetWallCoordinates(MapGridAuthoring authoring)
        {
            var coordinates = new List<Vector2Int>();
            IReadOnlyList<CellEntry> cells = authoring.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                CellEntry entry = cells[i];
                if (entry?.CellObject == null || !entry.CellObject.TryGetComponent(out CellData cellData) || cellData == null)
                {
                    continue;
                }

                if (cellData.SurfaceType == CellData.CellSurfaceType.Wall)
                {
                    coordinates.Add(entry.Position);
                }
            }

            return coordinates;
        }

        private MapGridAuthoring CreateAuthoring(int width, int height, float cellSize = 10f)
        {
            GameObject mapRoot = CreateGameObject("MapRoot");
            MapGridAuthoring authoring = mapRoot.AddComponent<MapGridAuthoring>();
            authoring.GridWidth = width;
            authoring.GridHeight = height;
            authoring.CellSize = new Vector2(cellSize, cellSize);
            ConfigureCoordinateBinding(authoring);
            return authoring;
        }

        private ArenaSeedMapGenerator CreateSeedGenerator(MapGridAuthoring authoring)
        {
            return authoring.gameObject.AddComponent<ArenaSeedMapGenerator>();
        }

        private EnemyGenerator CreateEnemyGenerator()
        {
            GameObject enemyRoot = CreateGameObject("EnemyRoot");
            GameObject enemyParent = CreateChildObject(enemyRoot, "Enemy");
            GameObject generatorObject = CreateChildObject(enemyRoot, "EnemyGenerator");
            EnemyGenerator generator = generatorObject.AddComponent<EnemyGenerator>();
            SetPrivateField(generator, "spawnedEnemyParent", enemyParent.transform);
            return generator;
        }

        private Transform CreatePlayer(Vector3 position)
        {
            GameObject player = CreateGameObject("Player");
            player.transform.position = position;

            Rigidbody rigidbody = player.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            BoxCollider collider = player.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 8f, 8f);

            player.AddComponent<PlayerPlaneMovement>();
            return player.transform;
        }

        private void CreateIndexedCellGrid(MapGridAuthoring authoring, int width, int height)
        {
            CreateIndexedCellGrid(authoring, width, height, out _);
        }

        private void CreateIndexedCellGrid(MapGridAuthoring authoring, int width, int height, out CellData[,] cells)
        {
            cells = new CellData[width, height];
            var entries = new List<CellEntry>(width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CellData cell = CreateCell($"Cell_{x}_{y}", new Vector2Int(x, y));
                    cells[x, y] = cell;
                    entries.Add(new CellEntry(x, y, cell.gameObject));
                }
            }

            authoring.ReplaceCellEntries(entries);
        }

        private CellData CreateCell(string name, Vector2Int coordinates, CellData.CellSurfaceType surfaceType = CellData.CellSurfaceType.Ground)
        {
            GameObject cell = CreateGameObject(name);
            GameObject modelRoot = CreateChildObject(cell, "Model");
            GameObject wallModel = CreateChildObject(modelRoot, "wall Model");
            GameObject groundModel = CreateChildObject(modelRoot, "Ground Model");
            wallModel.SetActive(false);

            BoxCollider wallCollider = cell.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(10f, 10f, 10f);
            wallCollider.center = new Vector3(0f, 5f, 0f);

            BoxCollider groundCollider = cell.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(10f, 1f, 10f);
            groundCollider.center = new Vector3(0f, -0.5f, 0f);

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
                Assert.That(cellData.TryRefreshSurfacePresentation(), Is.True);
            }

            return cellData;
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

        private GameObject CreateChildObject(GameObject parent, string name)
        {
            GameObject child = CreateGameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var fieldInfo = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, $"Field '{fieldName}' was not found on '{target.GetType().Name}'.");
            fieldInfo.SetValue(target, value);
        }
    }
}
