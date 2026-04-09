using System.Collections.Generic;
using Kernel;
using Kernel.MapGrid;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 控制文字敌人沿 XZ 平面执行追踪、冲刺、风筝与受击仇恨等移动策略。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharEnemyMovement : MonoBehaviour
{
    private enum DashMovementState
    {
        Chasing = 0,
        Windup = 1,
        Dashing = 2,
        Cooldown = 3,
    }

    private const float MinimumDirectionSqrMagnitude = 0.0001f;
    private const float PathWaypointReachDistance = 0.05f;
    private const int GridPathfindingCostScale = 100;

    private static readonly Vector2Int[] GridPathfindingNeighborOffsets =
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

    [SerializeField] private Enemy enemyData;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField] private Collider groundingReferenceCollider;
    [SerializeField, HideInInspector, FormerlySerializedAs("moveSpeed")] private float legacyMoveSpeed = 120f;
    [SerializeField, HideInInspector, FormerlySerializedAs("rotationSpeed")] private float legacyRotationSpeed = 540f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stoppingDistance")] private float legacyStoppingDistance = 1f;
    [SerializeField, HideInInspector] private bool hasMigratedLegacyMovementData;

    private DashMovementState dashState = DashMovementState.Chasing;
    private Vector3 dashDirection = Vector3.forward;
    private float dashWindupEndTime;
    private float dashEndTime;
    private float dashCooldownEndTime;
    private bool hasAggroOnHit;
    private bool isSubscribedToDamage;
    private List<Vector2Int> currentPathCells;
    private Vector2Int currentPathGoalCell;
    private int currentPathWaypointIndex;
    private bool hasCurrentPath;

    private void Awake()
    {
        ResolveMovementRigidbody();
        TryResolveEnemyData();
        MigrateLegacyMovementDataIfNeeded();
        TryResolveTargetPlayer();
        TryResolveTargetMapGrid();
        TryResolveGroundingReferenceCollider();
        EnsureGroundedRigidbodyConfiguration();
        TrySnapToGameplayPlane();
        EnsureDamageSubscription();
        ResetRuntimeState();
    }

    private void OnEnable()
    {
        EnsureDamageSubscription();
    }

    /// <summary>
    /// summary: 无 Rigidbody 时按帧推进当前移动状态机。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        if (targetRigidbody != null)
        {
            return;
        }

        TickMovement(Time.deltaTime, Time.time);
    }

    /// <summary>
    /// summary: 有 Rigidbody 时在 FixedUpdate 中推进当前移动状态机。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void FixedUpdate()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        TickMovement(Time.fixedDeltaTime, Time.time);
    }

    /// <summary>
    /// summary: 显式设置当前敌人的追踪目标，并立即把朝向对准目标方向。
    /// param: player 需要追踪的玩家 Transform
    /// returns: 传入目标有效时返回 true
    /// </summary>
    public bool TrySetTarget(Transform player)
    {
        if (player == null || IsOwnTransform(player))
        {
            return false;
        }

        ClearPathCache();
        targetPlayer = player;
        SnapTowardsTarget();
        return true;
    }

    /// <summary>
    /// summary: 让敌人立即朝向当前目标，不等待下一帧移动更新。
    /// param: 无
    /// returns: 成功拿到有效目标方向时返回 true
    /// </summary>
    public bool SnapTowardsTarget()
    {
        if (!TryGetFacingDirection(out Vector3 direction))
        {
            return false;
        }

        ApplyRotation(GetTargetRotation(direction));
        return true;
    }

    private void OnDisable()
    {
        RemoveDamageSubscription();
        ClearPathCache();
        StopMovement();
    }

    private void OnValidate()
    {
        ResolveMovementRigidbody();
        TryResolveEnemyData();
        MigrateLegacyMovementDataIfNeeded();
        TryResolveTargetMapGrid();
        TryResolveGroundingReferenceCollider();
        EnsureGroundedRigidbodyConfiguration();
        TrySnapToGameplayPlane();
    }

    /// <summary>
    /// summary: 统一推进当前敌人的移动状态机，并在需要时更新朝向与平面速度。
    /// param: deltaTime 本次移动使用的时间步长
    /// param: currentTime 当前逻辑时钟
    /// returns: 无
    /// </summary>
    private void TickMovement(float deltaTime, float currentTime)
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            StopMovement();
            return;
        }

        if (deltaTime <= 0f || !TryResolveEnemyData() || !TryResolveTargetPlayer())
        {
            StopMovement();
            return;
        }

        if (!ResolveMovement(currentTime, deltaTime, out Vector3 movementDirection, out float speedMultiplier))
        {
            StopMovement();
            return;
        }

        if (movementDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
        {
            Quaternion targetRotation = GetTargetRotation(movementDirection);
            ApplyRotation(GetNextRotation(GetCurrentRotation(), targetRotation, deltaTime));
        }
        else if (TryGetFacingDirection(out Vector3 facingDirection))
        {
            Quaternion targetRotation = GetTargetRotation(facingDirection);
            ApplyRotation(GetNextRotation(GetCurrentRotation(), targetRotation, deltaTime));
        }

        ApplyMovement(movementDirection, deltaTime, speedMultiplier);
    }

    /// <summary>
    /// summary: 根据当前敌人定义声明的 movement kind 解析本帧应执行的移动方向与倍率。
    /// param: currentTime 当前逻辑时钟
    /// param: deltaTime 本次移动使用的时间步长
    /// param: movementDirection 输出的平面移动方向
    /// param: speedMultiplier 输出的速度倍率
    /// returns: 当前帧需要平移时返回 true
    /// </summary>
    private bool ResolveMovement(float currentTime, float deltaTime, out Vector3 movementDirection, out float speedMultiplier)
    {
        movementDirection = Vector3.zero;
        speedMultiplier = 1f;

        switch (ResolveMovementKind())
        {
            case EnemyMovementKind.None:
                return false;

            case EnemyMovementKind.ChaseThenDash:
                return ResolveChaseThenDashMovement(currentTime, deltaTime, out movementDirection, out speedMultiplier);

            case EnemyMovementKind.KeepDistance:
                return ResolveKeepDistanceMovement(deltaTime, out movementDirection, out speedMultiplier);

            case EnemyMovementKind.AggroOnHit:
                return ResolveAggroOnHitMovement(deltaTime, out movementDirection, out speedMultiplier);

            case EnemyMovementKind.ChaseTarget:
            default:
                return ResolveChaseMovement(deltaTime, out movementDirection, out speedMultiplier);
        }
    }

    /// <summary>
    /// summary: 默认追踪行为下，按攻击距离或显式 stoppingDistance 接近玩家。
    /// param: movementDirection 输出的平面移动方向
    /// param: speedMultiplier 输出的速度倍率
    /// returns: 当前帧需要继续贴近玩家时返回 true
    /// </summary>
    private bool ResolveChaseMovement(float deltaTime, out Vector3 movementDirection, out float speedMultiplier)
    {
        speedMultiplier = 1f;
        float movementStepDistance = GetMovementStepDistance(speedMultiplier, deltaTime);
        return TryGetMovementDirectionTowardsTarget(ResolveDefaultChaseStoppingDistance(), movementStepDistance, out movementDirection);
    }

    /// <summary>
    /// summary: 风筝行为下，离得太远时接近玩家，离得太近时后退，落在距离带内则停住。
    /// param: deltaTime 本次移动使用的时间步长
    /// param: movementDirection 输出的平面移动方向
    /// param: speedMultiplier 输出的速度倍率
    /// returns: 当前帧需要平移以维持距离带时返回 true
    /// </summary>
    private bool ResolveKeepDistanceMovement(float deltaTime, out Vector3 movementDirection, out float speedMultiplier)
    {
        movementDirection = Vector3.zero;
        speedMultiplier = 1f;
        if (!TryGetPlanarTargetOffset(out Vector3 targetOffset, out float targetDistance))
        {
            return false;
        }

        EnemyDefinition.KeepDistanceMovementDefinition keepDistance = ResolveKeepDistanceMovementDefinition();
        float minDistance = Mathf.Max(0f, keepDistance.preferredDistance - keepDistance.distanceTolerance);
        float maxDistance = keepDistance.preferredDistance + keepDistance.distanceTolerance;
        float movementStepDistance = GetMovementStepDistance(speedMultiplier, deltaTime);
        if (targetDistance > maxDistance)
        {
            return TryGetMovementDirectionTowardsTarget(maxDistance, movementStepDistance, out movementDirection);
        }

        if (targetDistance < minDistance)
        {
            if (TryResolveCurrentCellPositions(out Vector2Int startCell, out _)
                && TryResolveKeepDistanceRetreatDirection(targetOffset, targetDistance, keepDistance.preferredDistance, startCell, movementStepDistance, out movementDirection))
            {
                return true;
            }

            ClearPathCache();
            movementDirection = GetRetreatDirection(targetOffset);
            return movementDirection.sqrMagnitude > MinimumDirectionSqrMagnitude;
        }

        ClearPathCache();
        return false;
    }

    /// <summary>
    /// summary: 受击仇恨行为下，敌人首次受击前保持静止，受击后永久进入加速追踪。
    /// param: movementDirection 输出的平面移动方向
    /// param: speedMultiplier 输出的速度倍率
    /// returns: 已进入仇恨状态且当前帧需要继续贴近玩家时返回 true
    /// </summary>
    private bool ResolveAggroOnHitMovement(float deltaTime, out Vector3 movementDirection, out float speedMultiplier)
    {
        speedMultiplier = ResolveAggroSpeedMultiplier();
        if (!hasAggroOnHit)
        {
            movementDirection = Vector3.zero;
            return false;
        }

        float movementStepDistance = GetMovementStepDistance(speedMultiplier, deltaTime);
        return TryGetMovementDirectionTowardsTarget(ResolveDefaultChaseStoppingDistance(), movementStepDistance, out movementDirection);
    }

    /// <summary>
    /// summary: 冲刺行为下按“追踪 -> 蓄力 -> 冲刺 -> 冷却追踪”的循环推进。
    /// param: currentTime 当前逻辑时钟
    /// param: movementDirection 输出的平面移动方向
    /// param: speedMultiplier 输出的速度倍率
    /// returns: 当前帧需要平移时返回 true
    /// </summary>
    private bool ResolveChaseThenDashMovement(float currentTime, float deltaTime, out Vector3 movementDirection, out float speedMultiplier)
    {
        EnemyDefinition.DashMovementDefinition dashMovement = ResolveDashMovementDefinition();
        movementDirection = Vector3.zero;
        speedMultiplier = 1f;

        if (dashState == DashMovementState.Windup)
        {
            if (currentTime < dashWindupEndTime)
            {
                return false;
            }

            dashState = DashMovementState.Dashing;
            dashEndTime = currentTime + dashMovement.dashDurationSeconds;
            if (TryGetMovementDirectionTowardsTarget(0f, 0f, out Vector3 pathDirection) && pathDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                dashDirection = pathDirection;
            }
            else if (TryGetFacingDirection(out Vector3 currentDirection))
            {
                dashDirection = currentDirection;
            }
            else
            {
                dashDirection = transform.forward;
            }
        }

        if (dashState == DashMovementState.Dashing)
        {
            if (currentTime < dashEndTime)
            {
                movementDirection = dashDirection.sqrMagnitude > MinimumDirectionSqrMagnitude ? dashDirection.normalized : transform.forward;
                speedMultiplier = dashMovement.dashSpeedMultiplier;
                return dashMovement.dashDurationSeconds > 0f;
            }

            dashState = DashMovementState.Cooldown;
            dashCooldownEndTime = currentTime + dashMovement.dashCooldownSeconds;
        }

        if (dashState == DashMovementState.Cooldown && currentTime >= dashCooldownEndTime)
        {
            dashState = DashMovementState.Chasing;
        }

        if (TryGetPlanarTargetOffset(out Vector3 targetOffset, out float targetDistance) &&
            dashMovement.triggerDistance > 0f &&
            targetDistance <= dashMovement.triggerDistance &&
            dashState == DashMovementState.Chasing)
        {
            dashState = DashMovementState.Windup;
            dashWindupEndTime = currentTime + dashMovement.windupSeconds;
            if (TryGetMovementDirectionTowardsTarget(0f, 0f, out Vector3 pathDirection) && pathDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                dashDirection = pathDirection;
            }
            else if (targetOffset.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                dashDirection = targetOffset.normalized;
            }
            else
            {
                dashDirection = transform.forward;
            }
            return false;
        }

        speedMultiplier = 1f;
        float movementStepDistance = GetMovementStepDistance(speedMultiplier, deltaTime);
        return TryGetMovementDirectionTowardsTarget(ResolveDefaultChaseStoppingDistance(), movementStepDistance, out movementDirection);
    }

    /// <summary>
    /// summary: 按当前地图格子路径朝玩家推进；当已经进入可停止范围时返回 false。
    /// param: minimumDistance 停止追近的最小平面距离
    /// param: movementStepDistance 本帧最多会前进的平面距离，用于提前掠过过近的路径点
    /// param: direction 输出的归一化平面方向
    /// returns: 当前仍需朝玩家移动时返回 true
    /// </summary>
    private bool TryGetMovementDirectionTowardsTarget(float minimumDistance, float movementStepDistance, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!TryGetPlanarTargetOffset(out Vector3 targetOffset, out float targetDistance))
        {
            return false;
        }

        if (!TryResolveCurrentCellPositions(out Vector2Int startCell, out Vector2Int goalCell))
        {
            return false;
        }

        if (startCell == goalCell)
        {
            if (targetDistance * targetDistance <= Mathf.Max(minimumDistance * minimumDistance, MinimumDirectionSqrMagnitude))
            {
                return false;
            }

            ClearPathCache();
            direction = targetOffset.normalized;
            return true;
        }

        if (!TryResolvePath(startCell, goalCell))
        {
            return false;
        }

        return TryGetCurrentPathDirection(GetCurrentPosition(), minimumDistance, movementStepDistance, targetOffset, targetDistance, out direction);
    }

    /// <summary>
    /// summary: 计算当前敌人在本帧允许前进的平面距离。
    /// param: speedMultiplier 当前移动倍率
    /// param: deltaTime 本帧使用的时间步长
    /// returns: 当前帧允许前进的世界平面距离
    /// </summary>
    private float GetMovementStepDistance(float speedMultiplier, float deltaTime)
    {
        if (!TryResolveEnemyData())
        {
            return 0f;
        }

        return enemyData.MoveSpeed * Mathf.Max(0f, speedMultiplier) * Mathf.Max(0f, deltaTime);
    }

    /// <summary>
    /// summary: 根据当前缓存路径和本帧移动距离，计算下一段应当前进的方向。
    /// param: currentPosition 当前敌人的世界位置
    /// param: minimumDistance 当前允许停止的最小平面距离
    /// param: movementStepDistance 本帧最多会前进的平面距离
    /// param: targetOffset 玩家相对敌人的平面偏移
    /// param: targetDistance 玩家相对敌人的平面距离
    /// param: direction 输出的归一化平面方向
    /// returns: 当前仍需继续朝路径前进时返回 true
    /// </summary>
    private bool TryGetCurrentPathDirection(Vector3 currentPosition, float minimumDistance, float movementStepDistance, Vector3 targetOffset, float targetDistance, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!hasCurrentPath || currentPathCells == null)
        {
            if (targetDistance * targetDistance <= Mathf.Max(minimumDistance * minimumDistance, MinimumDirectionSqrMagnitude))
            {
                return false;
            }

            direction = targetOffset.normalized;
            return true;
        }

        if (currentPathWaypointIndex >= currentPathCells.Count)
        {
            if (targetDistance * targetDistance <= Mathf.Max(minimumDistance * minimumDistance, MinimumDirectionSqrMagnitude))
            {
                return false;
            }

            direction = targetOffset.normalized;
            return true;
        }

        float waypointReachDistance = Mathf.Max(PathWaypointReachDistance, movementStepDistance);
        float waypointReachDistanceSqr = waypointReachDistance * waypointReachDistance;

        while (currentPathWaypointIndex < currentPathCells.Count)
        {
            Vector3 waypointPosition = GetPathWaypointWorldPosition(currentPathCells[currentPathWaypointIndex]);
            Vector3 waypointOffset = waypointPosition - currentPosition;
            waypointOffset.y = 0f;
            if (waypointOffset.sqrMagnitude > waypointReachDistanceSqr)
            {
                break;
            }

            currentPathWaypointIndex++;
        }

        if (currentPathWaypointIndex >= currentPathCells.Count)
        {
            if (targetDistance * targetDistance <= Mathf.Max(minimumDistance * minimumDistance, MinimumDirectionSqrMagnitude))
            {
                return false;
            }

            direction = targetOffset.normalized;
            return true;
        }

        if (GetRemainingPathDistance(currentPosition) <= minimumDistance)
        {
            return false;
        }

        Vector3 nextWaypointOffset = GetPathWaypointWorldPosition(currentPathCells[currentPathWaypointIndex]) - currentPosition;
        nextWaypointOffset.y = 0f;
        if (nextWaypointOffset.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            currentPathWaypointIndex++;
            return TryGetCurrentPathDirection(currentPosition, minimumDistance, movementStepDistance, targetOffset, targetDistance, out direction);
        }

        direction = nextWaypointOffset.normalized;
        return true;
    }

    /// <summary>
    /// summary: 风筝行为在过近时，先为远离玩家的目标点寻找可达格子，再沿当前缓存路径撤离。
    /// param: targetOffset 玩家相对敌人的平面偏移
    /// param: targetDistance 玩家相对敌人的平面距离
    /// param: preferredDistance 期望回到的中心距离
    /// param: startCell 敌人当前所在格子
    /// param: movementStepDistance 本帧最多会前进的平面距离
    /// param: direction 输出的归一化平面方向
    /// returns: 成功获得可执行的撤离方向时返回 true
    /// </summary>
    private bool TryResolveKeepDistanceRetreatDirection(Vector3 targetOffset, float targetDistance, float preferredDistance, Vector2Int startCell, float movementStepDistance, out Vector3 direction)
    {
        direction = Vector3.zero;
        Vector3 retreatDirection = GetRetreatDirection(targetOffset);
        Vector3 retreatWorldPosition = targetPlayer.position + (retreatDirection * preferredDistance);
        if (!TryFindKeepDistanceRetreatGoalCell(startCell, retreatWorldPosition, retreatDirection, out Vector2Int goalCell))
        {
            return false;
        }

        if (!TryResolvePath(startCell, goalCell))
        {
            return false;
        }

        return TryGetCurrentPathDirection(GetCurrentPosition(), 0f, movementStepDistance, -targetOffset, targetDistance, out direction);
    }

    /// <summary>
    /// summary: 在退让目标点周围寻找一个可达的地面格子，优先保留远离玩家的方向。
    /// param: startCell 敌人当前所在格子
    /// param: retreatWorldPosition 期望退让到的世界位置
    /// param: retreatDirection 远离玩家的平面方向
    /// param: goalCell 输出的可达目标格子
    /// returns: 成功找到可达目标格子时返回 true
    /// </summary>
    private bool TryFindKeepDistanceRetreatGoalCell(Vector2Int startCell, Vector3 retreatWorldPosition, Vector3 retreatDirection, out Vector2Int goalCell)
    {
        goalCell = default;
        if (!TryResolveTargetMapGrid())
        {
            return false;
        }

        Vector2Int desiredCell = GetClampedGridCellCoordinate(retreatWorldPosition);
        const int maxSearchRadius = 4;
        for (int radius = 0; radius <= maxSearchRadius; radius++)
        {
            bool foundCandidate = false;
            Vector2Int bestCandidateCell = default;
            float bestCandidateBias = float.NegativeInfinity;
            float bestCandidateDistanceSqr = float.MaxValue;

            int minX = Mathf.Max(0, desiredCell.x - radius);
            int maxX = Mathf.Min(targetMapGrid.GridWidth - 1, desiredCell.x + radius);
            int minY = Mathf.Max(0, desiredCell.y - radius);
            int maxY = Mathf.Min(targetMapGrid.GridHeight - 1, desiredCell.y + radius);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x - desiredCell.x), Mathf.Abs(y - desiredCell.y)) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidateCell = new(x, y);
                    if (candidateCell == startCell || !IsWalkableGridCell(targetMapGrid, candidateCell))
                    {
                        continue;
                    }

                    Vector3 candidateWorldPosition = targetMapGrid.GetCellWorldPosition(x, y);
                    Vector3 candidateOffsetFromPlayer = candidateWorldPosition - targetPlayer.position;
                    candidateOffsetFromPlayer.y = 0f;
                    float candidateBias = Vector3.Dot(candidateOffsetFromPlayer, retreatDirection);
                    float candidateDistanceSqr = (candidateWorldPosition - retreatWorldPosition).sqrMagnitude;
                    if (!foundCandidate ||
                        candidateBias > bestCandidateBias + 0.0001f ||
                        (Mathf.Abs(candidateBias - bestCandidateBias) <= 0.0001f && candidateDistanceSqr < bestCandidateDistanceSqr))
                    {
                        foundCandidate = true;
                        bestCandidateCell = candidateCell;
                        bestCandidateBias = candidateBias;
                        bestCandidateDistanceSqr = candidateDistanceSqr;
                    }
                }
            }

            if (foundCandidate && TryResolvePath(startCell, bestCandidateCell))
            {
                goalCell = bestCandidateCell;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// summary: 将一个世界点投影到当前地图的最近格子坐标，并在越界时夹到边缘。
    /// param: worldPoint 需要投影的世界位置
    /// returns: 当前地图内的最近格子坐标
    /// </summary>
    private Vector2Int GetClampedGridCellCoordinate(Vector3 worldPoint)
    {
        Vector3 localPoint = targetMapGrid.transform.InverseTransformPoint(worldPoint);
        float cellWidth = Mathf.Max(0.0001f, targetMapGrid.CellSize.x);
        float cellHeight = Mathf.Max(0.0001f, targetMapGrid.CellSize.y);
        int x = Mathf.Clamp(Mathf.FloorToInt((localPoint.x / cellWidth) + 0.5f), 0, targetMapGrid.GridWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt((localPoint.z / cellHeight) + 0.5f), 0, targetMapGrid.GridHeight - 1);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// summary: 计算 KeepDistance 在后退时应采用的平面方向。
    /// param: targetOffset 玩家相对敌人的平面偏移
    /// returns: 优先使用远离玩家的方向；若目标与敌人重合，则回退到当前朝向
    /// </summary>
    private Vector3 GetRetreatDirection(Vector3 targetOffset)
    {
        if (targetOffset.sqrMagnitude > MinimumDirectionSqrMagnitude)
        {
            Vector3 retreatDirection = -targetOffset.normalized;
            retreatDirection.y = 0f;
            return retreatDirection;
        }

        if (TryGetFacingDirection(out Vector3 facingDirection))
        {
            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                return -facingDirection.normalized;
            }
        }

        Vector3 fallbackDirection = -transform.forward;
        fallbackDirection.y = 0f;
        if (fallbackDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            fallbackDirection = Vector3.back;
        }

        return fallbackDirection.normalized;
    }

    /// <summary>
    /// summary: 仅当目标格子发生变化时重新计算并缓存当前路径。
    /// param: startCell 敌人当前所在格子
    /// param: goalCell 玩家当前所在格子
    /// returns: 成功拿到可跟随路径时返回 true
    /// </summary>
    private bool TryResolvePath(Vector2Int startCell, Vector2Int goalCell)
    {
        if (hasCurrentPath && currentPathCells != null && currentPathGoalCell == goalCell && currentPathWaypointIndex < currentPathCells.Count)
        {
            return true;
        }

        if (!TryFindGridPath(targetMapGrid, startCell, goalCell, out List<Vector2Int> pathCells))
        {
            ClearPathCache();
            return false;
        }

        currentPathCells = pathCells;
        currentPathGoalCell = goalCell;
        currentPathWaypointIndex = 0;
        hasCurrentPath = true;
        return true;
    }

    /// <summary>
    /// summary: 解析敌人与玩家当前分别落在哪个格子上。
    /// param: startCell 输出的敌人当前位置格子
    /// param: goalCell 输出的玩家当前位置格子
    /// returns: 两个位置都能投影到当前地图格子时返回 true
    /// </summary>
    private bool TryResolveCurrentCellPositions(out Vector2Int startCell, out Vector2Int goalCell)
    {
        startCell = default;
        goalCell = default;
        if (!TryResolveTargetMapGrid() || !TryResolveTargetPlayer())
        {
            return false;
        }

        if (!targetMapGrid.TryGetCellCoordinateFromWorldPoint(GetCurrentPosition(), out startCell))
        {
            return false;
        }

        return targetMapGrid.TryGetCellCoordinateFromWorldPoint(targetPlayer.position, out goalCell);
    }

    /// <summary>
    /// summary: 读取当前路径缓存里下一段路径点对应的世界坐标。
    /// param: cell 当前路径点所在的格子坐标
    /// returns: 当前路径点的世界坐标
    /// </summary>
    private Vector3 GetPathWaypointWorldPosition(Vector2Int cell)
    {
        return targetMapGrid.GetCellWorldPosition(cell.x, cell.y);
    }

    /// <summary>
    /// summary: 计算当前路径缓存剩余的世界距离。
    /// param: currentPosition 敌人当前世界位置
    /// returns: 距离路径终点的剩余世界距离
    /// </summary>
    private float GetRemainingPathDistance(Vector3 currentPosition)
    {
        if (!hasCurrentPath || currentPathCells == null || currentPathWaypointIndex >= currentPathCells.Count)
        {
            return 0f;
        }

        float remainingDistance = 0f;
        Vector3 previousPosition = currentPosition;
        for (int i = currentPathWaypointIndex; i < currentPathCells.Count; i++)
        {
            Vector3 waypointPosition = GetPathWaypointWorldPosition(currentPathCells[i]);
            remainingDistance += Vector3.Distance(previousPosition, waypointPosition);
            previousPosition = waypointPosition;
        }

        return remainingDistance;
    }

    /// <summary>
    /// summary: 清空当前路径缓存，供重新追踪或目标切换时重建路径。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ClearPathCache()
    {
        currentPathCells = null;
        currentPathGoalCell = default;
        currentPathWaypointIndex = 0;
        hasCurrentPath = false;
    }

    /// <summary>
    /// summary: 在地图网格上执行一次 A* 寻路，返回从起点到终点的格子路径。
    /// param: mapGrid 当前寻路所依赖的地图网格
    /// param: startCell 起点格子坐标
    /// param: goalCell 终点格子坐标
    /// param: pathCells 输出的格子路径
    /// returns: 成功找到可通行路径时返回 true
    /// </summary>
    private static bool TryFindGridPath(MapGridAuthoring mapGrid, Vector2Int startCell, Vector2Int goalCell, out List<Vector2Int> pathCells)
    {
        pathCells = null;
        if (mapGrid == null ||
            !mapGrid.IsValidGridCoordinate(startCell.x, startCell.y) ||
            !mapGrid.IsValidGridCoordinate(goalCell.x, goalCell.y))
        {
            return false;
        }

        if (!IsWalkableGridCell(mapGrid, startCell) || !IsWalkableGridCell(mapGrid, goalCell))
        {
            return false;
        }

        if (startCell == goalCell)
        {
            return false;
        }

        int width = mapGrid.GridWidth;
        int cellCount = width * mapGrid.GridHeight;
        int startIndex = ToGridPathIndex(startCell, width);
        int goalIndex = ToGridPathIndex(goalCell, width);
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
            int openSetPosition = FindBestGridPathOpenSetPosition(openSet, gCost, mapGrid, goalWorldPosition, width);
            int currentIndex = openSet[openSetPosition];
            if (currentIndex == goalIndex)
            {
                pathCells = ReconstructGridPath(cameFrom, startIndex, goalIndex, width);
                return pathCells.Count > 0;
            }

            openSet.RemoveAt(openSetPosition);
            openSetMask[currentIndex] = false;
            closedSet[currentIndex] = true;

            Vector2Int currentCell = ToGridPathCoordinates(currentIndex, width);
            Vector3 currentWorldPosition = mapGrid.GetCellWorldPosition(currentCell.x, currentCell.y);
            for (int directionIndex = 0; directionIndex < GridPathfindingNeighborOffsets.Length; directionIndex++)
            {
                Vector2Int offset = GridPathfindingNeighborOffsets[directionIndex];
                Vector2Int nextCell = currentCell + offset;
                if (!mapGrid.IsValidGridCoordinate(nextCell.x, nextCell.y))
                {
                    continue;
                }

                int nextIndex = ToGridPathIndex(nextCell, width);
                if (closedSet[nextIndex] || !IsWalkableGridCell(mapGrid, nextCell))
                {
                    continue;
                }

                bool isDiagonal = offset.x != 0 && offset.y != 0;
                if (isDiagonal && !CanMoveGridPathDiagonally(mapGrid, currentCell, offset))
                {
                    continue;
                }

                Vector3 nextWorldPosition = mapGrid.GetCellWorldPosition(nextCell.x, nextCell.y);
                int tentativeGCost = gCost[currentIndex] + GetGridPathStepCost(currentWorldPosition, nextWorldPosition);
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

    /// <summary>
    /// summary: 在候选开放列表里找到当前 fCost 最低的节点。
    /// param: openSet 当前开放列表的节点索引集合
    /// param: gCost 各节点当前累计代价
    /// param: mapGrid 当前地图网格
    /// param: goalWorldPosition 目标格子的世界坐标
    /// param: width 地图宽度
    /// returns: 最优节点在 openSet 中的位置索引
    /// </summary>
    private static int FindBestGridPathOpenSetPosition(List<int> openSet, int[] gCost, MapGridAuthoring mapGrid, Vector3 goalWorldPosition, int width)
    {
        int bestPosition = 0;
        int bestIndex = openSet[0];
        Vector2Int bestCell = ToGridPathCoordinates(bestIndex, width);
        int bestHCost = GetGridPathHeuristicCost(mapGrid.GetCellWorldPosition(bestCell.x, bestCell.y), goalWorldPosition);
        int bestFCost = gCost[bestIndex] + bestHCost;

        for (int i = 1; i < openSet.Count; i++)
        {
            int candidateIndex = openSet[i];
            Vector2Int candidateCell = ToGridPathCoordinates(candidateIndex, width);
            Vector3 candidateWorldPosition = mapGrid.GetCellWorldPosition(candidateCell.x, candidateCell.y);
            int candidateHCost = GetGridPathHeuristicCost(candidateWorldPosition, goalWorldPosition);
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

    /// <summary>
    /// summary: 判断 diagonal 移动时是否允许穿过当前格子角落。
    /// param: mapGrid 当前地图网格
    /// param: currentCell 当前格子坐标
    /// param: offset 目标方向偏移
    /// returns: 两个相邻正交格子都可通行时返回 true
    /// </summary>
    private static bool CanMoveGridPathDiagonally(MapGridAuthoring mapGrid, Vector2Int currentCell, Vector2Int offset)
    {
        Vector2Int horizontalCell = new(currentCell.x + offset.x, currentCell.y);
        Vector2Int verticalCell = new(currentCell.x, currentCell.y + offset.y);
        return IsWalkableGridCell(mapGrid, horizontalCell) && IsWalkableGridCell(mapGrid, verticalCell);
    }

    /// <summary>
    /// summary: 判断某个格子是否为可通行地面。
    /// param: mapGrid 当前地图网格
    /// param: cell 需要判断的格子坐标
    /// returns: 地面格子返回 true
    /// </summary>
    private static bool IsWalkableGridCell(MapGridAuthoring mapGrid, Vector2Int cell)
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

    /// <summary>
    /// summary: 计算单步移动代价。
    /// param: fromWorldPosition 当前格子的世界坐标
    /// param: toWorldPosition 相邻格子的世界坐标
    /// returns: 缩放后的移动代价
    /// </summary>
    private static int GetGridPathStepCost(Vector3 fromWorldPosition, Vector3 toWorldPosition)
    {
        return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(fromWorldPosition, toWorldPosition) * GridPathfindingCostScale));
    }

    /// <summary>
    /// summary: 计算启发式代价。
    /// param: fromWorldPosition 当前格子的世界坐标
    /// param: goalWorldPosition 目标格子的世界坐标
    /// returns: 缩放后的启发式代价
    /// </summary>
    private static int GetGridPathHeuristicCost(Vector3 fromWorldPosition, Vector3 goalWorldPosition)
    {
        return Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(fromWorldPosition, goalWorldPosition) * GridPathfindingCostScale));
    }

    /// <summary>
    /// summary: 从 cameFrom 数组重建格子路径。
    /// param: cameFrom 每个节点的前驱节点索引
    /// param: startIndex 起点索引
    /// param: goalIndex 终点索引
    /// param: width 地图宽度
    /// returns: 按起点之后顺序排列的格子路径
    /// </summary>
    private static List<Vector2Int> ReconstructGridPath(int[] cameFrom, int startIndex, int goalIndex, int width)
    {
        List<Vector2Int> pathCells = new();
        int currentIndex = goalIndex;

        while (currentIndex != -1 && currentIndex != startIndex)
        {
            pathCells.Add(ToGridPathCoordinates(currentIndex, width));
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

    /// <summary>
    /// summary: 把格子坐标转换为 row-major 索引。
    /// param: cell 当前格子坐标
    /// param: width 当前地图宽度
    /// returns: row-major 索引
    /// </summary>
    private static int ToGridPathIndex(Vector2Int cell, int width)
    {
        return (cell.y * width) + cell.x;
    }

    /// <summary>
    /// summary: 把 row-major 索引转换为格子坐标。
    /// param: index row-major 索引
    /// param: width 当前地图宽度
    /// returns: 对应的格子坐标
    /// </summary>
    private static Vector2Int ToGridPathCoordinates(int index, int width)
    {
        return new Vector2Int(index % width, index / width);
    }

    /// <summary>
    /// summary: 读取玩家相对敌人的平面方向，供停住时仍然保持朝向玩家。
    /// param: direction 输出的归一化平面朝向
    /// returns: 成功拿到有效目标方向时返回 true
    /// </summary>
    private bool TryGetFacingDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!TryGetPlanarTargetOffset(out Vector3 targetOffset, out _))
        {
            return false;
        }

        if (targetOffset.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return false;
        }

        direction = targetOffset.normalized;
        return true;
    }

    /// <summary>
    /// summary: 解析玩家相对敌人的平面偏移与距离。
    /// param: targetOffset 输出的玩家平面偏移
    /// param: targetDistance 输出的玩家平面距离
    /// returns: 成功解析到有效玩家目标时返回 true
    /// </summary>
    private bool TryGetPlanarTargetOffset(out Vector3 targetOffset, out float targetDistance)
    {
        targetOffset = Vector3.zero;
        targetDistance = 0f;
        if (!TryResolveTargetPlayer())
        {
            return false;
        }

        targetOffset = targetPlayer.position - GetCurrentPosition();
        targetOffset.y = 0f;
        targetDistance = targetOffset.magnitude;
        return true;
    }

    /// <summary>
    /// summary: 把目标方向转换成实际位移或刚体速度，统一支持 Transform 与 Rigidbody 两种模式。
    /// param: direction 当前应前进的世界方向
    /// param: deltaTime 本次移动使用的时间步长
    /// param: speedMultiplier 当前 movement kind 对基础移速施加的倍率
    /// returns: 无
    /// </summary>
    private void ApplyMovement(Vector3 direction, float deltaTime, float speedMultiplier)
    {
        if (!TryResolveEnemyData())
        {
            return;
        }

        float moveSpeed = enemyData.MoveSpeed * Mathf.Max(0f, speedMultiplier);
        Vector3 movementDelta = direction * moveSpeed * deltaTime;
        if (targetRigidbody == null)
        {
            transform.position += movementDelta;
            return;
        }

        if (targetRigidbody.isKinematic)
        {
            targetRigidbody.MovePosition(targetRigidbody.position + movementDelta);
            return;
        }

        Vector3 currentVelocity = targetRigidbody.linearVelocity;
        currentVelocity.x = direction.x * moveSpeed;
        currentVelocity.z = direction.z * moveSpeed;
        targetRigidbody.linearVelocity = currentVelocity;
    }

    /// <summary>
    /// summary: 当敌人当前帧不应继续移动时，清空动态刚体的平面速度避免滑行。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void StopMovement()
    {
        if (targetRigidbody == null || targetRigidbody.isKinematic)
        {
            return;
        }

        Vector3 currentVelocity = targetRigidbody.linearVelocity;
        currentVelocity.x = 0f;
        currentVelocity.z = 0f;
        targetRigidbody.linearVelocity = currentVelocity;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定目标时，尝试自动找到场景中的玩家平面移动组件。
    /// param: 无
    /// returns: 成功拿到追踪目标时返回 true
    /// </summary>
    private bool TryResolveTargetPlayer()
    {
        if (IsTargetPlayerReferenceValid())
        {
            return true;
        }

        PlayerPlaneMovement playerMovement = FindFirstObjectByType<PlayerPlaneMovement>();
        if (playerMovement == null)
        {
            return false;
        }

        if (targetPlayer != playerMovement.transform)
        {
            ClearPathCache();
        }

        targetPlayer = playerMovement.transform;
        return true;
    }

    /// <summary>
    /// summary: 仅接受挂在敌人自身根节点上的 Rigidbody；误绑到子节点时回退为 Transform 驱动。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ResolveMovementRigidbody()
    {
        if (IsMovementRigidbodyReferenceValid())
        {
            return;
        }

        Rigidbody selfRigidbody = GetComponent<Rigidbody>();
        targetRigidbody = selfRigidbody != null && selfRigidbody.transform == transform ? selfRigidbody : null;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定地图时，尝试自动解析当前场景中的 MapGridAuthoring。
    /// param: 无
    /// returns: 成功拿到地图平面来源时返回 true
    /// </summary>
    private bool TryResolveTargetMapGrid()
    {
        if (targetMapGrid != null)
        {
            return true;
        }

        MapGridAuthoring resolvedMapGrid = FindFirstObjectByType<MapGridAuthoring>();
        if (resolvedMapGrid == null)
        {
            return false;
        }

        if (targetMapGrid != resolvedMapGrid)
        {
            targetMapGrid = resolvedMapGrid;
            ClearPathCache();
        }

        return true;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定 grounded 参考 Collider 时，尝试自动在敌人层级内解析一个可用碰撞体。
    /// param: 无
    /// returns: 成功拿到 grounded 参考 Collider 时返回 true
    /// </summary>
    private bool TryResolveGroundingReferenceCollider()
    {
        if (groundingReferenceCollider != null && !groundingReferenceCollider.isTrigger)
        {
            return true;
        }

        return WorldHeightUtility.TryFindGroundingReferenceCollider(this, out groundingReferenceCollider);
    }

    /// <summary>
    /// summary: 统一规范 grounded 敌人刚体，确保角色始终停留在地图平面对应的世界高度上。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureGroundedRigidbodyConfiguration()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        WorldHeightUtility.TryConfigureGroundedRigidbody(targetRigidbody);
    }

    /// <summary>
    /// summary: 按 grounded 根节点契约把敌人抬到当前地图平面高度上。
    /// param: 无
    /// returns: 成功完成 grounded snap 时返回 true
    /// </summary>
    private bool TrySnapToGameplayPlane()
    {
        if (!TryResolveTargetMapGrid())
        {
            return false;
        }

        float planeY = targetMapGrid.WorldPlaneY;
        if (TryResolveGroundingReferenceCollider() &&
            WorldHeightUtility.TryGetGroundedRootPosition(transform, groundingReferenceCollider, planeY, out Vector3 groundedPosition))
        {
            transform.position = groundedPosition;
            if (targetRigidbody != null)
            {
                targetRigidbody.position = groundedPosition;
            }

            return true;
        }

        return WorldHeightUtility.TrySnapTransformToPlaneHeight(transform, planeY);
    }

    /// <summary>
    /// summary: 计算当前敌人应当面向的世界朝向。
    /// param: direction 当前需要朝向的世界方向
    /// returns: 对应的世界旋转
    /// </summary>
    private static Quaternion GetTargetRotation(Vector3 direction)
    {
        return Quaternion.LookRotation(direction, Vector3.up);
    }

    /// <summary>
    /// summary: 基于旋转速度限制计算本帧应应用的旋转结果。
    /// param: currentRotation 当前旋转
    /// param: targetRotation 目标旋转
    /// param: deltaTime 本次旋转使用的时间步长
    /// returns: 本帧应应用的旋转
    /// </summary>
    private Quaternion GetNextRotation(Quaternion currentRotation, Quaternion targetRotation, float deltaTime)
    {
        if (!TryResolveEnemyData())
        {
            return currentRotation;
        }

        float rotationSpeed = enemyData.RotationSpeed;
        if (rotationSpeed <= 0f)
        {
            return targetRotation;
        }

        return Quaternion.RotateTowards(currentRotation, targetRotation, rotationSpeed * deltaTime);
    }

    /// <summary>
    /// summary: 把旋转结果写回 Transform 或 Rigidbody。
    /// param: rotation 需要应用的世界旋转
    /// returns: 无
    /// </summary>
    private void ApplyRotation(Quaternion rotation)
    {
        if (targetRigidbody != null)
        {
            targetRigidbody.MoveRotation(rotation);
            return;
        }

        transform.rotation = rotation;
    }

    /// <summary>
    /// summary: 获取当前敌人用于移动和朝向计算的世界位置。
    /// param: 无
    /// returns: 当前敌人的世界位置
    /// </summary>
    private Vector3 GetCurrentPosition()
    {
        return targetRigidbody != null ? targetRigidbody.position : transform.position;
    }

    /// <summary>
    /// summary: 获取当前敌人用于旋转插值的世界旋转。
    /// param: 无
    /// returns: 当前敌人的世界旋转
    /// </summary>
    private Quaternion GetCurrentRotation()
    {
        return targetRigidbody != null ? targetRigidbody.rotation : transform.rotation;
    }

    /// <summary>
    /// summary: 统一建立对受击事件的订阅，供 AggroOnHit 这类行为在受击时切换状态。
    /// param: 无
    /// returns: 成功建立订阅时返回 true
    /// </summary>
    private bool EnsureDamageSubscription()
    {
        if (isSubscribedToDamage)
        {
            return true;
        }

        if (!TryResolveEnemyData())
        {
            return false;
        }

        enemyData.Damaged += HandleEnemyDamaged;
        isSubscribedToDamage = true;
        return true;
    }

    /// <summary>
    /// summary: 取消对受击事件的订阅，避免组件停用后残留委托。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void RemoveDamageSubscription()
    {
        if (!isSubscribedToDamage || enemyData == null)
        {
            isSubscribedToDamage = false;
            return;
        }

        enemyData.Damaged -= HandleEnemyDamaged;
        isSubscribedToDamage = false;
    }

    /// <summary>
    /// summary: 在敌人受击时更新 movement kind 对应的运行时状态。
    /// param: damagedEnemy 触发受击事件的敌人
    /// returns: 无
    /// </summary>
    private void HandleEnemyDamaged(Enemy damagedEnemy)
    {
        if (damagedEnemy == null || damagedEnemy != enemyData || ResolveMovementKind() != EnemyMovementKind.AggroOnHit)
        {
            return;
        }

        hasAggroOnHit = true;
    }

    /// <summary>
    /// summary: 重置 movement kind 依赖的运行时状态，供首次生成或重新启用时进入确定初始态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ResetRuntimeState()
    {
        dashState = DashMovementState.Chasing;
        dashDirection = transform.forward.sqrMagnitude > MinimumDirectionSqrMagnitude ? transform.forward.normalized : Vector3.forward;
        dashWindupEndTime = 0f;
        dashEndTime = 0f;
        dashCooldownEndTime = 0f;
        hasAggroOnHit = false;
        ClearPathCache();
    }

    private EnemyMovementKind ResolveMovementKind()
    {
        return enemyData != null && enemyData.Definition != null
            ? enemyData.Definition.MovementKind
            : EnemyMovementKind.ChaseTarget;
    }

    private float ResolveDefaultChaseStoppingDistance()
    {
        if (!TryResolveEnemyData())
        {
            return 0f;
        }

        return enemyData.AttackRange > 0f ? enemyData.AttackRange : enemyData.StoppingDistance;
    }

    private float ResolveAggroSpeedMultiplier()
    {
        if (enemyData == null || enemyData.Definition == null)
        {
            return 1f;
        }

        return Mathf.Max(1f, enemyData.Definition.AggroOnHitMovement.aggroSpeedMultiplier);
    }

    private EnemyDefinition.DashMovementDefinition ResolveDashMovementDefinition()
    {
        return enemyData != null && enemyData.Definition != null
            ? enemyData.Definition.DashMovement.GetSanitized()
            : new EnemyDefinition.DashMovementDefinition
            {
                triggerDistance = ResolveDefaultChaseStoppingDistance(),
                dashSpeedMultiplier = 1f,
            };
    }

    private EnemyDefinition.KeepDistanceMovementDefinition ResolveKeepDistanceMovementDefinition()
    {
        return enemyData != null && enemyData.Definition != null
            ? enemyData.Definition.KeepDistanceMovement.GetSanitized()
            : new EnemyDefinition.KeepDistanceMovementDefinition
            {
                preferredDistance = ResolveDefaultChaseStoppingDistance(),
                distanceTolerance = 0f,
            };
    }

    /// <summary>
    /// summary: 判断当前玩家目标引用是否有效，避免 prefab 把自己错误序列化成追踪目标。
    /// param: 无
    /// returns: 目标存在且不属于敌人自身层级时返回 true
    /// </summary>
    private bool IsTargetPlayerReferenceValid()
    {
        return targetPlayer != null && !IsOwnTransform(targetPlayer);
    }

    /// <summary>
    /// summary: 判断当前刚体引用是否真的挂在敌人自身根节点上。
    /// param: 无
    /// returns: 仅当 Rigidbody 属于当前 GameObject 时返回 true
    /// </summary>
    private bool IsMovementRigidbodyReferenceValid()
    {
        return targetRigidbody != null && targetRigidbody.transform == transform;
    }

    /// <summary>
    /// summary: 判断当前敌人数据引用是否有效，确保移动组件只读取同物体上的 Enemy 数据。
    /// param: 无
    /// returns: 引用存在且挂在当前 GameObject 上时返回 true
    /// </summary>
    private bool IsEnemyDataReferenceValid()
    {
        return enemyData != null && enemyData.transform == transform;
    }

    /// <summary>
    /// summary: 解析当前物体上实际挂载的 Enemy 派生组件，移动逻辑只依赖现有敌人数据。
    /// param: 无
    /// returns: 成功拿到 Enemy 数据组件时返回 true
    /// </summary>
    private bool TryResolveEnemyData()
    {
        if (IsEnemyDataReferenceValid())
        {
            return true;
        }

        enemyData = null;
        return TryGetComponent(out enemyData);
    }

    /// <summary>
    /// summary: 把旧版移动组件里序列化的运动参数迁移到新的 Enemy 数据组件中，但不会覆盖已自定义的敌人数据。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void MigrateLegacyMovementDataIfNeeded()
    {
        if (hasMigratedLegacyMovementData || !TryResolveEnemyData())
        {
            return;
        }

        if (enemyData is ILegacyEnemyMovementSettingsReceiver legacyReceiver)
        {
            legacyReceiver.TryApplyLegacyMovementSettingsIfNeeded(legacyMoveSpeed, legacyRotationSpeed, legacyStoppingDistance);
        }

        hasMigratedLegacyMovementData = true;
    }

    /// <summary>
    /// summary: 判断一个 Transform 是否属于当前敌人自身或其子节点。
    /// param: candidate 需要判断的 Transform
    /// returns: 属于当前敌人层级时返回 true
    /// </summary>
    private bool IsOwnTransform(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
