using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.MapGrid
{
    public readonly struct ArenaSeedLayoutSettings
    {
        public ArenaSeedLayoutSettings(
            int borderWallThickness,
            int obstacleCountMin,
            int obstacleCountMax,
            Vector2Int obstacleWidthRange,
            Vector2Int obstacleHeightRange,
            int edgeClearanceCells,
            int playerSafeRadiusCells,
            int spawnAnnulusHalfWidthCells,
            int maxPlacementAttemptsPerObstacle)
        {
            BorderWallThickness = Mathf.Max(0, borderWallThickness);
            ObstacleCountMin = Mathf.Max(0, obstacleCountMin);
            ObstacleCountMax = Mathf.Max(ObstacleCountMin, obstacleCountMax);
            ObstacleWidthRange = SanitizeRange(obstacleWidthRange);
            ObstacleHeightRange = SanitizeRange(obstacleHeightRange);
            EdgeClearanceCells = Mathf.Max(0, edgeClearanceCells);
            PlayerSafeRadiusCells = Mathf.Max(0, playerSafeRadiusCells);
            SpawnAnnulusHalfWidthCells = Mathf.Max(0, spawnAnnulusHalfWidthCells);
            MaxPlacementAttemptsPerObstacle = Mathf.Max(1, maxPlacementAttemptsPerObstacle);
        }

        public int BorderWallThickness { get; }
        public int ObstacleCountMin { get; }
        public int ObstacleCountMax { get; }
        public Vector2Int ObstacleWidthRange { get; }
        public Vector2Int ObstacleHeightRange { get; }
        public int EdgeClearanceCells { get; }
        public int PlayerSafeRadiusCells { get; }
        public int SpawnAnnulusHalfWidthCells { get; }
        public int MaxPlacementAttemptsPerObstacle { get; }

        private static Vector2Int SanitizeRange(Vector2Int range)
        {
            int min = Mathf.Max(1, Mathf.Min(range.x, range.y));
            int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2Int(min, max);
        }
    }

    public static class ArenaSeedLayoutBuilder
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        /// <summary>
        /// summary: 基于固定网格、seed 与保留区规则构建一张 row-major 墙地布局。
        /// param: gridSize 目标地图的宽高，单位为 cell
        /// param: seed 当前布局使用的稳定随机种子
        /// param: playerReferenceCell 玩家出生参考 cell；若传入越界值会自动 clamp
        /// param: spawnAnnulusRadiusCells 可选的刷怪环半径，单位为 cell；传 null 时不保留刷怪环
        /// param: settings 当前 seed 地图生成使用的参数集合
        /// param: layout 输出的 row-major 表面布局，按 y 外层、x 内层排列
        /// param: error 输入校验失败时返回的错误信息
        /// returns: 成功构建布局时返回 true
        /// </summary>
        public static bool TryBuildLayout(
            Vector2Int gridSize,
            int seed,
            Vector2Int playerReferenceCell,
            int? spawnAnnulusRadiusCells,
            ArenaSeedLayoutSettings settings,
            out List<CellData.CellSurfaceType> layout,
            out string error)
        {
            layout = null;
            error = null;

            if (gridSize.x <= 0 || gridSize.y <= 0)
            {
                error = "Grid size must be greater than zero.";
                return false;
            }

            Vector2Int clampedPlayerCell = ClampToGrid(playerReferenceCell, gridSize);
            layout = CreateGroundLayout(gridSize.x * gridSize.y);
            bool[] reservedGround = new bool[layout.Count];

            ApplyBorderWalls(layout, gridSize, settings.BorderWallThickness);
            Vector2Int resolvedPlayerCell = FindNearestGroundCell(layout, gridSize, clampedPlayerCell);
            ReserveGroundDisk(layout, reservedGround, gridSize, resolvedPlayerCell, settings.PlayerSafeRadiusCells);
            if (spawnAnnulusRadiusCells.HasValue && spawnAnnulusRadiusCells.Value >= 0)
            {
                ReserveGroundAnnulus(
                    layout,
                    reservedGround,
                    gridSize,
                    resolvedPlayerCell,
                    spawnAnnulusRadiusCells.Value,
                    settings.SpawnAnnulusHalfWidthCells);
            }

            PlaceObstacles(layout, reservedGround, gridSize, resolvedPlayerCell, seed, settings);
            return true;
        }

        /// <summary>
        /// summary: 计算 row-major 布局里某个网格坐标对应的线性索引。
        /// param: x 目标 cell 的 X 坐标
        /// param: y 目标 cell 的 Y 坐标
        /// param: width 当前布局的网格宽度
        /// returns: 目标坐标在 row-major 布局中的线性索引
        /// </summary>
        public static int GetRowMajorIndex(int x, int y, int width)
        {
            return (y * width) + x;
        }

        private static List<CellData.CellSurfaceType> CreateGroundLayout(int cellCount)
        {
            var layout = new List<CellData.CellSurfaceType>(cellCount);
            for (int index = 0; index < cellCount; index++)
            {
                layout.Add(CellData.CellSurfaceType.Ground);
            }

            return layout;
        }

        private static Vector2Int ClampToGrid(Vector2Int coordinates, Vector2Int gridSize)
        {
            return new Vector2Int(
                Mathf.Clamp(coordinates.x, 0, gridSize.x - 1),
                Mathf.Clamp(coordinates.y, 0, gridSize.y - 1));
        }

        private static void ApplyBorderWalls(IList<CellData.CellSurfaceType> layout, Vector2Int gridSize, int borderWallThickness)
        {
            if (borderWallThickness <= 0)
            {
                return;
            }

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    if (x >= borderWallThickness &&
                        y >= borderWallThickness &&
                        x < gridSize.x - borderWallThickness &&
                        y < gridSize.y - borderWallThickness)
                    {
                        continue;
                    }

                    layout[GetRowMajorIndex(x, y, gridSize.x)] = CellData.CellSurfaceType.Wall;
                }
            }
        }

        private static void ReserveGroundDisk(
            IList<CellData.CellSurfaceType> layout,
            IList<bool> reservedGround,
            Vector2Int gridSize,
            Vector2Int center,
            int radiusCells)
        {
            int clampedRadius = Mathf.Max(0, radiusCells);
            int radiusSquared = clampedRadius * clampedRadius;

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    int dx = x - center.x;
                    int dy = y - center.y;
                    if ((dx * dx) + (dy * dy) > radiusSquared)
                    {
                        continue;
                    }

                    int index = GetRowMajorIndex(x, y, gridSize.x);
                    if (layout[index] == CellData.CellSurfaceType.Wall)
                    {
                        continue;
                    }

                    reservedGround[index] = true;
                    layout[index] = CellData.CellSurfaceType.Ground;
                }
            }
        }

        private static void ReserveGroundAnnulus(
            IList<CellData.CellSurfaceType> layout,
            IList<bool> reservedGround,
            Vector2Int gridSize,
            Vector2Int center,
            int annulusRadiusCells,
            int halfWidthCells)
        {
            int outerRadius = Mathf.Max(0, annulusRadiusCells + Mathf.Max(0, halfWidthCells));
            int innerRadius = Mathf.Max(0, annulusRadiusCells - Mathf.Max(0, halfWidthCells));
            int outerRadiusSquared = outerRadius * outerRadius;
            int innerRadiusSquared = innerRadius * innerRadius;

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    int dx = x - center.x;
                    int dy = y - center.y;
                    int distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared < innerRadiusSquared || distanceSquared > outerRadiusSquared)
                    {
                        continue;
                    }

                    int index = GetRowMajorIndex(x, y, gridSize.x);
                    if (layout[index] == CellData.CellSurfaceType.Wall)
                    {
                        continue;
                    }

                    reservedGround[index] = true;
                    layout[index] = CellData.CellSurfaceType.Ground;
                }
            }
        }

        private static void PlaceObstacles(
            List<CellData.CellSurfaceType> layout,
            IReadOnlyList<bool> reservedGround,
            Vector2Int gridSize,
            Vector2Int playerReferenceCell,
            int seed,
            ArenaSeedLayoutSettings settings)
        {
            int interiorMinX = settings.BorderWallThickness + settings.EdgeClearanceCells;
            int interiorMinY = settings.BorderWallThickness + settings.EdgeClearanceCells;
            int interiorMaxX = gridSize.x - settings.BorderWallThickness - settings.EdgeClearanceCells - 1;
            int interiorMaxY = gridSize.y - settings.BorderWallThickness - settings.EdgeClearanceCells - 1;
            if (interiorMinX > interiorMaxX || interiorMinY > interiorMaxY)
            {
                return;
            }

            int availableWidth = interiorMaxX - interiorMinX + 1;
            int availableHeight = interiorMaxY - interiorMinY + 1;
            int minObstacleWidth = Mathf.Min(settings.ObstacleWidthRange.x, availableWidth);
            int maxObstacleWidth = Mathf.Min(settings.ObstacleWidthRange.y, availableWidth);
            int minObstacleHeight = Mathf.Min(settings.ObstacleHeightRange.x, availableHeight);
            int maxObstacleHeight = Mathf.Min(settings.ObstacleHeightRange.y, availableHeight);
            if (maxObstacleWidth <= 0 || maxObstacleHeight <= 0)
            {
                return;
            }

            var random = new VocalithRandom(seed);
            int targetObstacleCount = NextInclusive(random, settings.ObstacleCountMin, settings.ObstacleCountMax);
            for (int obstacleIndex = 0; obstacleIndex < targetObstacleCount; obstacleIndex++)
            {
                for (int attempt = 0; attempt < settings.MaxPlacementAttemptsPerObstacle; attempt++)
                {
                    int obstacleWidth = NextInclusive(random, minObstacleWidth, maxObstacleWidth);
                    int obstacleHeight = NextInclusive(random, minObstacleHeight, maxObstacleHeight);
                    int minX = NextInclusive(random, interiorMinX, interiorMaxX - obstacleWidth + 1);
                    int minY = NextInclusive(random, interiorMinY, interiorMaxY - obstacleHeight + 1);
                    if (!TryPlaceObstacle(layout, reservedGround, gridSize, playerReferenceCell, minX, minY, obstacleWidth, obstacleHeight))
                    {
                        continue;
                    }

                    break;
                }
            }
        }

        private static bool TryPlaceObstacle(
            IList<CellData.CellSurfaceType> layout,
            IReadOnlyList<bool> reservedGround,
            Vector2Int gridSize,
            Vector2Int playerReferenceCell,
            int minX,
            int minY,
            int obstacleWidth,
            int obstacleHeight)
        {
            var changedIndices = new List<int>(obstacleWidth * obstacleHeight);
            for (int y = minY; y < minY + obstacleHeight; y++)
            {
                for (int x = minX; x < minX + obstacleWidth; x++)
                {
                    int index = GetRowMajorIndex(x, y, gridSize.x);
                    if (reservedGround[index] || layout[index] == CellData.CellSurfaceType.Wall)
                    {
                        return false;
                    }

                    changedIndices.Add(index);
                }
            }

            for (int i = 0; i < changedIndices.Count; i++)
            {
                layout[changedIndices[i]] = CellData.CellSurfaceType.Wall;
            }

            if (AreAllReservedGroundCellsReachable(layout, reservedGround, gridSize, playerReferenceCell))
            {
                return true;
            }

            for (int i = 0; i < changedIndices.Count; i++)
            {
                layout[changedIndices[i]] = CellData.CellSurfaceType.Ground;
            }

            return false;
        }

        private static bool AreAllReservedGroundCellsReachable(
            IList<CellData.CellSurfaceType> layout,
            IReadOnlyList<bool> reservedGround,
            Vector2Int gridSize,
            Vector2Int playerReferenceCell)
        {
            int startIndex = GetRowMajorIndex(playerReferenceCell.x, playerReferenceCell.y, gridSize.x);
            if (layout[startIndex] != CellData.CellSurfaceType.Ground)
            {
                return false;
            }

            bool hasReservedGround = false;
            for (int index = 0; index < layout.Count; index++)
            {
                if (!reservedGround[index])
                {
                    continue;
                }

                if (layout[index] != CellData.CellSurfaceType.Ground)
                {
                    return false;
                }

                hasReservedGround = true;
            }

            if (!hasReservedGround)
            {
                return true;
            }

            var visited = new bool[layout.Count];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(playerReferenceCell);
            visited[startIndex] = true;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int directionIndex = 0; directionIndex < CardinalDirections.Length; directionIndex++)
                {
                    Vector2Int next = current + CardinalDirections[directionIndex];
                    if (next.x < 0 || next.y < 0 || next.x >= gridSize.x || next.y >= gridSize.y)
                    {
                        continue;
                    }

                    int nextIndex = GetRowMajorIndex(next.x, next.y, gridSize.x);
                    if (visited[nextIndex] || layout[nextIndex] != CellData.CellSurfaceType.Ground)
                    {
                        continue;
                    }

                    visited[nextIndex] = true;
                    queue.Enqueue(next);
                }
            }

            for (int index = 0; index < reservedGround.Count; index++)
            {
                if (reservedGround[index] && !visited[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2Int FindNearestGroundCell(
            IReadOnlyList<CellData.CellSurfaceType> layout,
            Vector2Int gridSize,
            Vector2Int desiredCell)
        {
            int desiredIndex = GetRowMajorIndex(desiredCell.x, desiredCell.y, gridSize.x);
            if (layout[desiredIndex] != CellData.CellSurfaceType.Wall)
            {
                return desiredCell;
            }

            int maxRadius = Mathf.Max(gridSize.x, gridSize.y);
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int y = desiredCell.y - radius; y <= desiredCell.y + radius; y++)
                {
                    for (int x = desiredCell.x - radius; x <= desiredCell.x + radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x - desiredCell.x), Mathf.Abs(y - desiredCell.y)) != radius)
                        {
                            continue;
                        }

                        if (x < 0 || y < 0 || x >= gridSize.x || y >= gridSize.y)
                        {
                            continue;
                        }

                        int index = GetRowMajorIndex(x, y, gridSize.x);
                        if (layout[index] != CellData.CellSurfaceType.Wall)
                        {
                            return new Vector2Int(x, y);
                        }
                    }
                }
            }

            return desiredCell;
        }

        private static int NextInclusive(VocalithRandom random, int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            return random.Next(minInclusive, maxInclusive + 1);
        }
    }
}
