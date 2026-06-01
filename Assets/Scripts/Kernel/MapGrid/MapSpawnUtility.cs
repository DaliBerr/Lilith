using UnityEngine;

namespace Kernel.MapGrid
{
    /// <summary>
    /// 提供地图出生点、传送点与最近地面格解析的共享工具，供运行时 flow 和 seed 生成复用。
    /// </summary>
    public static class MapSpawnUtility
    {
        /// <summary>
        /// summary: 把任意世界坐标映射到目标地图中的最近合法格坐标；越界点会被 clamp 回网格范围内。
        /// param: mapGrid 目标地图网格
        /// param: worldPosition 需要映射的世界坐标
        /// returns: 目标地图中的合法格坐标；mapGrid 无效时返回默认值
        /// </summary>
        public static Vector2Int GetClosestValidCellCoordinate(MapGridAuthoring mapGrid, Vector3 worldPosition)
        {
            if (mapGrid == null)
            {
                return default;
            }

            Vector3 localPoint = mapGrid.transform.InverseTransformPoint(worldPosition);
            float cellWidth = Mathf.Max(0.0001f, mapGrid.CellSize.x);
            float cellHeight = Mathf.Max(0.0001f, mapGrid.CellSize.y);
            int x = Mathf.RoundToInt(localPoint.x / cellWidth);
            int y = Mathf.RoundToInt(localPoint.z / cellHeight);
            return new Vector2Int(
                Mathf.Clamp(x, 0, mapGrid.GridWidth - 1),
                Mathf.Clamp(y, 0, mapGrid.GridHeight - 1));
        }

        /// <summary>
        /// summary: 判断目标格当前是否是可站立的 Ground 格。
        /// param: mapGrid 当前要检查的地图网格
        /// param: coordinates 需要检查的格坐标
        /// returns: 命中已索引且表面为 Ground 的格子时返回 true
        /// </summary>
        public static bool IsGroundCell(MapGridAuthoring mapGrid, Vector2Int coordinates)
        {
            if (mapGrid == null ||
                !mapGrid.IsValidGridCoordinate(coordinates.x, coordinates.y) ||
                !mapGrid.TryGetCell(coordinates, out GameObject cellObject) ||
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

        /// <summary>
        /// summary: 从给定起点开始向外环搜索最近的可站立 Ground 格。
        /// param: mapGrid 当前要搜索的地图网格
        /// param: startCoordinates 搜索起点；越界时会先 clamp
        /// param: nearestGroundCoordinates 输出的最近 Ground 格坐标
        /// param: error 搜索失败时返回的错误信息
        /// returns: 成功找到最近 Ground 格时返回 true
        /// </summary>
        public static bool TryFindNearestGroundCoordinate(
            MapGridAuthoring mapGrid,
            Vector2Int startCoordinates,
            out Vector2Int nearestGroundCoordinates,
            out string error)
        {
            nearestGroundCoordinates = default;
            error = null;

            if (mapGrid == null)
            {
                error = "Map grid is missing.";
                return false;
            }

            Vector2Int clampedStart = new(
                Mathf.Clamp(startCoordinates.x, 0, mapGrid.GridWidth - 1),
                Mathf.Clamp(startCoordinates.y, 0, mapGrid.GridHeight - 1));

            if (IsGroundCell(mapGrid, clampedStart))
            {
                nearestGroundCoordinates = clampedStart;
                return true;
            }

            float cellWidth = Mathf.Max(0.0001f, mapGrid.CellSize.x);
            float cellHeight = Mathf.Max(0.0001f, mapGrid.CellSize.y);
            int maxRadius = Mathf.Max(mapGrid.GridWidth, mapGrid.GridHeight);
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                bool foundGround = false;
                float bestDistance = float.MaxValue;
                Vector2Int bestCoordinates = default;

                int minX = Mathf.Max(0, clampedStart.x - radius);
                int maxX = Mathf.Min(mapGrid.GridWidth - 1, clampedStart.x + radius);
                int minY = Mathf.Max(0, clampedStart.y - radius);
                int maxY = Mathf.Min(mapGrid.GridHeight - 1, clampedStart.y + radius);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (Mathf.Abs(x - clampedStart.x) != radius &&
                            Mathf.Abs(y - clampedStart.y) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = new(x, y);
                        if (!IsGroundCell(mapGrid, candidate))
                        {
                            continue;
                        }

                        float distance =
                            Mathf.Pow((x - clampedStart.x) * cellWidth, 2f) +
                            Mathf.Pow((y - clampedStart.y) * cellHeight, 2f);
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

            error = "Could not find any valid ground cell on the current map.";
            return false;
        }

        /// <summary>
        /// summary: 按 grounded 根节点契约把目标 Transform 吸附到最近 Ground 格中心，并同步刚体速度。
        /// param: mapGrid 当前要使用的地图网格
        /// param: target 需要被传送的目标根节点
        /// param: requestedCoordinates 希望传送到的目标格；不是 Ground 时会自动回退
        /// param: resolvedCoordinates 输出的最终 Ground 格坐标
        /// param: error 传送失败时返回的错误信息
        /// returns: 成功把目标传送到最近 Ground 格时返回 true
        /// </summary>
        public static bool TryTeleportToNearestGroundCell(
            MapGridAuthoring mapGrid,
            Transform target,
            Vector2Int requestedCoordinates,
            out Vector2Int resolvedCoordinates,
            out string error)
        {
            resolvedCoordinates = default;
            error = null;

            if (mapGrid == null)
            {
                error = "Map grid is missing.";
                return false;
            }

            if (target == null)
            {
                error = "Target transform is missing.";
                return false;
            }

            if (!TryFindNearestGroundCoordinate(mapGrid, requestedCoordinates, out resolvedCoordinates, out error))
            {
                return false;
            }

            Vector3 cellWorldPosition = mapGrid.GetCellWorldPosition(resolvedCoordinates.x, resolvedCoordinates.y);
            Vector3 snappedPosition = new(cellWorldPosition.x, mapGrid.WorldPlaneY, cellWorldPosition.z);
            if (WorldHeightUtility.TryFindGroundingReferenceCollider(target, out Collider referenceCollider) &&
                WorldHeightUtility.TryGetGroundedRootPosition(target, referenceCollider, mapGrid.WorldPlaneY, out Vector3 groundedPosition))
            {
                snappedPosition.y = groundedPosition.y;
            }

            target.position = snappedPosition;
            if (target.TryGetComponent(out Rigidbody targetRigidbody))
            {
                targetRigidbody.position = snappedPosition;
                if (!targetRigidbody.isKinematic)
                {
                    targetRigidbody.linearVelocity = Vector3.zero;
                    targetRigidbody.angularVelocity = Vector3.zero;
                }
            }

            return true;
        }
    }
}
