using System.Collections.Generic;
using UnityEngine;

namespace Kernel.MapGrid
{
    /// <summary>
    /// 基于地图格子的 A* 寻路器，用于在 Ground / Wall 布局上生成可跟随的格子路径。
    /// </summary>
    public static class GridPathfinder
    {
        private const int CostScale = 100;

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new(0, 1),
            new(1, 0),
            new(0, -1),
            new(-1, 0),
            new(1, 1),
            new(-1, 1),
            new(1, -1),
            new(-1, -1),
        };

        /// <summary>
        /// summary: 在当前地图网格上，从起点到目标点计算一条可通行路径。
        /// param: mapGrid 当前寻路所依赖的地图网格
        /// param: startCell 起点格子坐标
        /// param: goalCell 目标格子坐标
        /// returns: 成功找到路径时返回 true，pathCells 按起点之后的顺序返回
        /// </summary>
        public static bool TryFindPath(MapGridAuthoring mapGrid, Vector2Int startCell, Vector2Int goalCell, out List<Vector2Int> pathCells)
        {
            pathCells = null;
            if (mapGrid == null ||
                !mapGrid.IsValidGridCoordinate(startCell.x, startCell.y) ||
                !mapGrid.IsValidGridCoordinate(goalCell.x, goalCell.y))
            {
                return false;
            }

            if (!IsWalkableCell(mapGrid, startCell) || !IsWalkableCell(mapGrid, goalCell))
            {
                return false;
            }

            if (startCell == goalCell)
            {
                return false;
            }

            int width = mapGrid.GridWidth;
            int height = mapGrid.GridHeight;
            int cellCount = width * height;
            int startIndex = ToIndex(startCell, width);
            int goalIndex = ToIndex(goalCell, width);
            Vector3 goalWorldPosition = mapGrid.GetCellWorldPosition(goalCell.x, goalCell.y);

            int[] cameFrom = new int[cellCount];
            int[] gCost = new int[cellCount];
            bool[] closedSet = new bool[cellCount];
            bool[] openSetMask = new bool[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                cameFrom[i] = -1;
                gCost[i] = int.MaxValue;
            }

            List<int> openSet = new(Mathf.Min(cellCount, 64));
            openSet.Add(startIndex);
            openSetMask[startIndex] = true;
            gCost[startIndex] = 0;

            while (openSet.Count > 0)
            {
                int openSetPosition = FindBestOpenSetPosition(openSet, gCost, mapGrid, goalWorldPosition, width);
                int currentIndex = openSet[openSetPosition];
                if (currentIndex == goalIndex)
                {
                    pathCells = ReconstructPath(cameFrom, startIndex, goalIndex, width);
                    return pathCells.Count > 0;
                }

                openSet.RemoveAt(openSetPosition);
                openSetMask[currentIndex] = false;
                closedSet[currentIndex] = true;

                Vector2Int currentCell = ToCoordinates(currentIndex, width);
                Vector3 currentWorldPosition = mapGrid.GetCellWorldPosition(currentCell.x, currentCell.y);
                for (int directionIndex = 0; directionIndex < NeighborOffsets.Length; directionIndex++)
                {
                    Vector2Int offset = NeighborOffsets[directionIndex];
                    Vector2Int nextCell = currentCell + offset;
                    if (!mapGrid.IsValidGridCoordinate(nextCell.x, nextCell.y))
                    {
                        continue;
                    }

                    int nextIndex = ToIndex(nextCell, width);
                    if (closedSet[nextIndex] || !IsWalkableCell(mapGrid, nextCell))
                    {
                        continue;
                    }

                    bool isDiagonal = offset.x != 0 && offset.y != 0;
                    if (isDiagonal && !CanMoveDiagonally(mapGrid, currentCell, offset))
                    {
                        continue;
                    }

                    Vector3 nextWorldPosition = mapGrid.GetCellWorldPosition(nextCell.x, nextCell.y);
                    int tentativeGCost = gCost[currentIndex] + GetStepCost(currentWorldPosition, nextWorldPosition);
                    if (tentativeGCost >= gCost[nextIndex])
                    {
                        continue;
                    }

                    cameFrom[nextIndex] = currentIndex;
                    gCost[nextIndex] = tentativeGCost;
                    if (!openSetMask[nextIndex])
                    {
                        openSet.Add(nextIndex);
                        openSetMask[nextIndex] = true;
                    }
                }
            }

            return false;
        }

        private static int FindBestOpenSetPosition(List<int> openSet, int[] gCost, MapGridAuthoring mapGrid, Vector3 goalWorldPosition, int width)
        {
            int bestPosition = 0;
            int bestIndex = openSet[0];
            Vector2Int bestCell = ToCoordinates(bestIndex, width);
            int bestHCost = GetHeuristicCost(mapGrid.GetCellWorldPosition(bestCell.x, bestCell.y), goalWorldPosition);
            int bestFCost = gCost[bestIndex] + bestHCost;

            for (int i = 1; i < openSet.Count; i++)
            {
                int candidateIndex = openSet[i];
                Vector2Int candidateCell = ToCoordinates(candidateIndex, width);
                Vector3 candidateWorldPosition = mapGrid.GetCellWorldPosition(candidateCell.x, candidateCell.y);
                int candidateHCost = GetHeuristicCost(candidateWorldPosition, goalWorldPosition);
                int candidateFCost = gCost[candidateIndex] + candidateHCost;

                if (candidateFCost < bestFCost || (candidateFCost == bestFCost && candidateHCost < bestHCost))
                {
                    bestPosition = i;
                    bestIndex = candidateIndex;
                    bestCell = candidateCell;
                    bestHCost = candidateHCost;
                    bestFCost = candidateFCost;
                }
            }

            return bestPosition;
        }

        private static bool CanMoveDiagonally(MapGridAuthoring mapGrid, Vector2Int currentCell, Vector2Int offset)
        {
            Vector2Int horizontalCell = new(currentCell.x + offset.x, currentCell.y);
            Vector2Int verticalCell = new(currentCell.x, currentCell.y + offset.y);
            return IsWalkableCell(mapGrid, horizontalCell) && IsWalkableCell(mapGrid, verticalCell);
        }

        private static bool IsWalkableCell(MapGridAuthoring mapGrid, Vector2Int cell)
        {
            if (!mapGrid.IsValidGridCoordinate(cell.x, cell.y) || !mapGrid.TryGetCell(cell, out GameObject cellObject) || cellObject == null)
            {
                return false;
            }

            if (cellObject.TryGetComponent(out CellData cellData) && cellData != null)
            {
                return cellData.SurfaceType == CellData.CellSurfaceType.Ground;
            }

            return cellObject.CompareTag(MapGridAuthoring.GroundTagName);
        }

        private static int GetStepCost(Vector3 fromWorldPosition, Vector3 toWorldPosition)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(fromWorldPosition, toWorldPosition) * CostScale));
        }

        private static int GetHeuristicCost(Vector3 fromWorldPosition, Vector3 goalWorldPosition)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(fromWorldPosition, goalWorldPosition) * CostScale));
        }

        private static List<Vector2Int> ReconstructPath(int[] cameFrom, int startIndex, int goalIndex, int width)
        {
            List<Vector2Int> pathCells = new();
            int currentIndex = goalIndex;

            while (currentIndex != -1 && currentIndex != startIndex)
            {
                pathCells.Add(ToCoordinates(currentIndex, width));
                currentIndex = cameFrom[currentIndex];
            }

            if (currentIndex != startIndex)
            {
                pathCells.Clear();
                return pathCells;
            }

            pathCells.Reverse();
            return pathCells;
        }

        private static int ToIndex(Vector2Int cell, int width)
        {
            return (cell.y * width) + cell.x;
        }

        private static Vector2Int ToCoordinates(int index, int width)
        {
            return new Vector2Int(index % width, index / width);
        }
    }
}