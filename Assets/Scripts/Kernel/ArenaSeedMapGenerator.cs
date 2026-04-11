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

        public bool GenerateOnAwake
        {
            get => generateOnAwake;
            set => generateOnAwake = value;
        }

        public bool SnapPlayerOnGenerate
        {
            get => snapPlayerToNearestGroundCell;
            set => snapPlayerToNearestGroundCell = value;
        }

        public ArenaSeedLayoutSettings LayoutSettings
        {
            get => new(
                borderWallThickness,
                obstacleCountMin,
                obstacleCountMax,
                obstacleWidthRange,
                obstacleHeightRange,
                edgeClearanceCells,
                playerSafeRadiusCells,
                spawnAnnulusHalfWidthCells,
                maxPlacementAttemptsPerObstacle);
            set
            {
                borderWallThickness = value.BorderWallThickness;
                obstacleCountMin = value.ObstacleCountMin;
                obstacleCountMax = value.ObstacleCountMax;
                obstacleWidthRange = value.ObstacleWidthRange;
                obstacleHeightRange = value.ObstacleHeightRange;
                edgeClearanceCells = value.EdgeClearanceCells;
                playerSafeRadiusCells = value.PlayerSafeRadiusCells;
                spawnAnnulusHalfWidthCells = value.SpawnAnnulusHalfWidthCells;
                maxPlacementAttemptsPerObstacle = value.MaxPlacementAttemptsPerObstacle;
                SanitizeConfiguration();
            }
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
            if (!TryResolveMapGrid(out error))
            {
                return false;
            }

            if (!TryResolveTargetPlayer(out error))
            {
                return false;
            }

            Vector2Int playerReferenceCell = MapSpawnUtility.GetClosestValidCellCoordinate(targetMapGrid, targetPlayer.position);
            return TryGenerateAndApplyLayout(playerReferenceCell, includePlayerSnap, out error);
        }

        /// <summary>
        /// summary: 使用显式指定的出生参考格生成并应用整张地图布局，并可选择是否把玩家吸附到最近地面格。
        /// param: playerReferenceCell 当前布局生成和刷怪保留区使用的参考格
        /// param: includePlayerSnap 为 true 时在布局应用完成后吸附玩家到最近地面格
        /// param: error 构建布局、写入地图或吸附玩家失败时返回的错误信息
        /// returns: 成功完成整图生成、应用和可选玩家吸附时返回 true
        /// </summary>
        public bool TryGenerateAndApplyLayout(Vector2Int playerReferenceCell, bool includePlayerSnap, out string error)
        {
            error = null;
            if (!TryBuildLayout(playerReferenceCell, out List<CellData.CellSurfaceType> layout, out error))
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

            return TrySnapPlayerToNearestGroundCell(playerReferenceCell, out error);
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

            Vector2Int playerReferenceCell = MapSpawnUtility.GetClosestValidCellCoordinate(targetMapGrid, targetPlayer.position);
            return TryBuildLayout(playerReferenceCell, out layout, out error);
        }

        /// <summary>
        /// summary: 仅根据显式指定的参考格和当前 seed / Inspector 参数构建一份 row-major 地图布局，不直接改写场景对象。
        /// param: playerReferenceCell 当前布局生成和刷怪保留区使用的参考格
        /// param: layout 输出的 row-major 墙地布局
        /// param: error 构建输入不完整或生成失败时返回的错误信息
        /// returns: 成功得到完整布局时返回 true
        /// </summary>
        public bool TryBuildLayout(Vector2Int playerReferenceCell, out List<CellData.CellSurfaceType> layout, out string error)
        {
            layout = null;
            error = null;

            if (!TryResolveMapGrid(out error))
            {
                return false;
            }

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

            Vector2Int startCoordinates = MapSpawnUtility.GetClosestValidCellCoordinate(targetMapGrid, targetPlayer.position);
            return TrySnapPlayerToNearestGroundCell(startCoordinates, out error);
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

        /// <summary>
        /// summary: 把当前玩家根节点吸附到指定起始格附近的最近有效地面格中心，并保持 grounded 高度契约。
        /// param: startCoordinates 当前吸附搜索使用的起始格
        /// param: error 找不到地图、玩家或有效地面格时返回的错误信息
        /// returns: 成功把玩家吸附到有效地面格时返回 true
        /// </summary>
        public bool TrySnapPlayerToNearestGroundCell(Vector2Int startCoordinates, out string error)
        {
            error = null;
            if (!TryResolveMapGrid(out error) || !TryResolveTargetPlayer(out error))
            {
                return false;
            }

            return MapSpawnUtility.TryTeleportToNearestGroundCell(targetMapGrid, targetPlayer, startCoordinates, out _, out error);
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
