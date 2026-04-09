using System;
using System.Collections.Generic;
using UnityEngine;
using Vocalith.Logging;

namespace Kernel.MapGrid
{
    [DefaultExecutionOrder(-950)]
    [DisallowMultipleComponent]
    public sealed class ArenaSeedMapGenerator : MonoBehaviour
    {
        [SerializeField] private int seed;
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private bool snapPlayerToNearestGroundCell = true;
        [SerializeField, Min(0)] private int borderWallThickness = 1;
        [SerializeField, Min(0)] private int obstacleCountMin = 6;
        [SerializeField, Min(0)] private int obstacleCountMax = 10;
        [SerializeField] private Vector2Int obstacleWidthRange = new(2, 5);
        [SerializeField] private Vector2Int obstacleHeightRange = new(2, 4);
        [SerializeField, Min(0)] private int edgeClearanceCells = 2;
        [SerializeField, Min(0)] private int playerSafeRadiusCells = 2;
        [SerializeField, Min(0)] private int spawnAnnulusHalfWidthCells = 1;
        [SerializeField, Min(1)] private int maxPlacementAttemptsPerObstacle = 24;

        private MapGridAuthoring targetMapGrid;
        private Transform targetPlayer;
        private EnemyGenerator targetEnemyGenerator;

        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        private void Awake()
        {
            if (!generateOnAwake)
            {
                return;
            }

            if (!TryGenerateAndApplyLayout(out string error))
            {
                GameDebug.LogError($"[ArenaSeedMapGenerator] {error}");
            }
        }

        private void OnValidate()
        {
            targetMapGrid = GetComponent<MapGridAuthoring>();
            SanitizeConfiguration();
        }

        /// <summary>
        /// summary: 使用当前 seed 和 Inspector 参数生成并应用整张地图布局；若启用配置则会随后吸附玩家出生格。
        /// param: error 构建布局、写入地图或吸附玩家失败时返回的错误信息
        /// returns: 成功完成整图生成、应用和可选玩家吸附时返回 true
        /// </summary>
        public bool TryGenerateAndApplyLayout(out string error)
        {
            return TryGenerateAndApplyLayout(snapPlayerToNearestGroundCell, out error);
        }

        /// <summary>
        /// summary: 使用当前 seed 和 Inspector 参数生成并应用整张地图布局，并可显式控制是否吸附玩家出生格。
        /// param: includePlayerSnap 为 true 时在布局应用完成后吸附玩家到最近地面格
        /// param: error 构建布局、写入地图或吸附玩家失败时返回的错误信息
        /// returns: 成功完成整图生成、应用和可选玩家吸附时返回 true
        /// </summary>
        public bool TryGenerateAndApplyLayout(bool includePlayerSnap, out string error)
        {
            error = null;
            if (!TryBuildLayout(out List<CellData.CellSurfaceType> layout, out error))
            {
                return false;
            }

            if (!targetMapGrid.TryApplySurfaceLayout(layout, out error))
            {
                return false;
            }

            if (!includePlayerSnap)
            {
                return true;
            }

            return TrySnapPlayerToNearestGroundCell(out error);
        }

        /// <summary>
        /// summary: 仅根据当前 seed 和 Inspector 参数构建一份 row-major 地图布局，不直接改写场景对象。
        /// param: layout 输出的 row-major 墙地布局
        /// param: error 构建输入不完整或生成失败时返回的错误信息
        /// returns: 成功得到完整布局时返回 true
        /// </summary>
        public bool TryBuildLayout(out List<CellData.CellSurfaceType> layout, out string error)
        {
            layout = null;
            error = null;

            if (!TryResolveMapGrid(out error))
            {
                return false;
            }

            if (!TryResolveTargetPlayer(out error))
            {
                return false;
            }

            Vector2Int playerReferenceCell = GetClosestValidCellCoordinate(targetPlayer.position);
            int? spawnAnnulusRadiusCells = TryResolveSpawnAnnulusRadiusCells(out int resolvedSpawnRadiusCells)
                ? resolvedSpawnRadiusCells
                : null;

            var settings = new ArenaSeedLayoutSettings(
                borderWallThickness,
                obstacleCountMin,
                obstacleCountMax,
                obstacleWidthRange,
                obstacleHeightRange,
                edgeClearanceCells,
                playerSafeRadiusCells,
                spawnAnnulusHalfWidthCells,
                maxPlacementAttemptsPerObstacle);

            return ArenaSeedLayoutBuilder.TryBuildLayout(
                new Vector2Int(targetMapGrid.GridWidth, targetMapGrid.GridHeight),
                seed,
                playerReferenceCell,
                spawnAnnulusRadiusCells,
                settings,
                out layout,
                out error);
        }

        /// <summary>
        /// summary: 把当前玩家根节点吸附到最近的有效地面格中心，并保持 grounded 高度契约。
        /// param: error 找不到地图、玩家或有效地面格时返回的错误信息
        /// returns: 成功把玩家吸附到有效地面格时返回 true
        /// </summary>
        public bool TrySnapPlayerToNearestGroundCell(out string error)
        {
            error = null;
            if (!TryResolveMapGrid(out error) || !TryResolveTargetPlayer(out error))
            {
                return false;
            }

            Vector2Int startCoordinates = GetClosestValidCellCoordinate(targetPlayer.position);
            if (!TryFindNearestGroundCoordinate(startCoordinates, out Vector2Int targetCoordinates, out error))
            {
                return false;
            }

            Vector3 cellWorldPosition = targetMapGrid.GetCellWorldPosition(targetCoordinates.x, targetCoordinates.y);
            Vector3 snappedPosition = new(cellWorldPosition.x, targetMapGrid.WorldPlaneY, cellWorldPosition.z);
            if (WorldHeightUtility.TryFindGroundingReferenceCollider(targetPlayer, out Collider referenceCollider) &&
                WorldHeightUtility.TryGetGroundedRootPosition(targetPlayer, referenceCollider, targetMapGrid.WorldPlaneY, out Vector3 groundedPosition))
            {
                snappedPosition.y = groundedPosition.y;
            }

            targetPlayer.position = snappedPosition;
            if (targetPlayer.TryGetComponent(out Rigidbody targetRigidbody))
            {
                targetRigidbody.position = snappedPosition;
                targetRigidbody.linearVelocity = Vector3.zero;
                targetRigidbody.angularVelocity = Vector3.zero;
            }

            return true;
        }

        /// <summary>
        /// summary: 为编辑器预览生成一个新的稳定 seed，供下一次布局预览复用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RandomizeSeed()
        {
            seed = Guid.NewGuid().GetHashCode();
        }

        private bool TryResolveMapGrid(out string error)
        {
            error = null;
            if (targetMapGrid == null)
            {
                targetMapGrid = GetComponent<MapGridAuthoring>();
            }

            if (targetMapGrid == null)
            {
                targetMapGrid = FindFirstObjectByType<MapGridAuthoring>();
            }

            if (targetMapGrid != null)
            {
                return true;
            }

            error = "ArenaSeedMapGenerator requires a MapGridAuthoring to build layouts.";
            return false;
        }

        private bool TryResolveTargetPlayer(out string error)
        {
            error = null;
            if (targetPlayer == null)
            {
                PlayerPlaneMovement playerMovement = FindFirstObjectByType<PlayerPlaneMovement>();
                if (playerMovement != null)
                {
                    targetPlayer = playerMovement.transform;
                }
            }

            if (targetPlayer != null)
            {
                return true;
            }

            error = "ArenaSeedMapGenerator could not find a PlayerPlaneMovement in the active scene.";
            return false;
        }

        private bool TryResolveSpawnAnnulusRadiusCells(out int spawnAnnulusRadiusCells)
        {
            spawnAnnulusRadiusCells = 0;
            if (targetEnemyGenerator == null)
            {
                targetEnemyGenerator = FindFirstObjectByType<EnemyGenerator>();
            }

            if (targetEnemyGenerator == null || !TryResolveMapGrid(out _))
            {
                return false;
            }

            float averageCellSize = Mathf.Max(0.0001f, (targetMapGrid.CellSize.x + targetMapGrid.CellSize.y) * 0.5f);
            spawnAnnulusRadiusCells = Mathf.RoundToInt(targetEnemyGenerator.SpawnDistance / averageCellSize);
            return spawnAnnulusRadiusCells > 0;
        }

        private Vector2Int GetClosestValidCellCoordinate(Vector3 worldPosition)
        {
            Vector3 localPoint = targetMapGrid.transform.InverseTransformPoint(worldPosition);
            float cellWidth = Mathf.Max(0.0001f, targetMapGrid.CellSize.x);
            float cellHeight = Mathf.Max(0.0001f, targetMapGrid.CellSize.y);
            int x = Mathf.RoundToInt(localPoint.x / cellWidth);
            int y = Mathf.RoundToInt(localPoint.z / cellHeight);
            return new Vector2Int(
                Mathf.Clamp(x, 0, targetMapGrid.GridWidth - 1),
                Mathf.Clamp(y, 0, targetMapGrid.GridHeight - 1));
        }

        private bool TryFindNearestGroundCoordinate(Vector2Int startCoordinates, out Vector2Int nearestGroundCoordinates, out string error)
        {
            nearestGroundCoordinates = default;
            error = null;

            if (IsGroundCell(startCoordinates))
            {
                nearestGroundCoordinates = startCoordinates;
                return true;
            }

            float cellWidth = Mathf.Max(0.0001f, targetMapGrid.CellSize.x);
            float cellHeight = Mathf.Max(0.0001f, targetMapGrid.CellSize.y);
            int maxRadius = Mathf.Max(targetMapGrid.GridWidth, targetMapGrid.GridHeight);
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool foundGround = false;
                float bestDistance = float.MaxValue;
                Vector2Int bestCoordinates = default;

                int minX = Mathf.Max(0, startCoordinates.x - radius);
                int maxX = Mathf.Min(targetMapGrid.GridWidth - 1, startCoordinates.x + radius);
                int minY = Mathf.Max(0, startCoordinates.y - radius);
                int maxY = Mathf.Min(targetMapGrid.GridHeight - 1, startCoordinates.y + radius);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (Mathf.Abs(x - startCoordinates.x) != radius &&
                            Mathf.Abs(y - startCoordinates.y) != radius)
                        {
                            continue;
                        }

                        var candidate = new Vector2Int(x, y);
                        if (!IsGroundCell(candidate))
                        {
                            continue;
                        }

                        float distance =
                            Mathf.Pow((x - startCoordinates.x) * cellWidth, 2f) +
                            Mathf.Pow((y - startCoordinates.y) * cellHeight, 2f);
                        if (foundGround && distance >= bestDistance)
                        {
                            continue;
                        }

                        foundGround = true;
                        bestDistance = distance;
                        bestCoordinates = candidate;
                    }
                }

                if (!foundGround)
                {
                    continue;
                }

                nearestGroundCoordinates = bestCoordinates;
                return true;
            }

            error = "ArenaSeedMapGenerator could not find any valid ground cell on the current map.";
            return false;
        }

        private bool IsGroundCell(Vector2Int coordinates)
        {
            if (!targetMapGrid.IsValidGridCoordinate(coordinates.x, coordinates.y) ||
                !targetMapGrid.TryGetCell(coordinates, out GameObject cellObject) ||
                cellObject == null)
            {
                return false;
            }

            if (cellObject.TryGetComponent(out CellData cellData) && cellData != null)
            {
                return cellData.SurfaceType == CellData.CellSurfaceType.Ground;
            }

            return cellObject.CompareTag(MapGridAuthoring.GroundTagName);
        }

        private void SanitizeConfiguration()
        {
            borderWallThickness = Mathf.Max(0, borderWallThickness);
            obstacleCountMin = Mathf.Max(0, obstacleCountMin);
            obstacleCountMax = Mathf.Max(obstacleCountMin, obstacleCountMax);
            obstacleWidthRange = SanitizeRange(obstacleWidthRange);
            obstacleHeightRange = SanitizeRange(obstacleHeightRange);
            edgeClearanceCells = Mathf.Max(0, edgeClearanceCells);
            playerSafeRadiusCells = Mathf.Max(0, playerSafeRadiusCells);
            spawnAnnulusHalfWidthCells = Mathf.Max(0, spawnAnnulusHalfWidthCells);
            maxPlacementAttemptsPerObstacle = Mathf.Max(1, maxPlacementAttemptsPerObstacle);
        }

        private static Vector2Int SanitizeRange(Vector2Int range)
        {
            int min = Mathf.Max(1, Mathf.Min(range.x, range.y));
            int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2Int(min, max);
        }
    }
}
