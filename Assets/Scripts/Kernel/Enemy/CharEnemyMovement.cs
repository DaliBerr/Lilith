using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 控制文字敌人沿 XZ 平面持续朝向并追踪玩家。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharEnemyMovement : MonoBehaviour
{
    private const float MinimumDirectionSqrMagnitude = 0.0001f;

    [SerializeField] private Enemy enemyData;
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField, HideInInspector, FormerlySerializedAs("moveSpeed")] private float legacyMoveSpeed = 120f;
    [SerializeField, HideInInspector, FormerlySerializedAs("rotationSpeed")] private float legacyRotationSpeed = 540f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stoppingDistance")] private float legacyStoppingDistance = 1f;
    [SerializeField, HideInInspector] private bool hasMigratedLegacyMovementData;


    private void Awake()
    {
        ResolveMovementRigidbody();
        TryResolveEnemyData();
        MigrateLegacyMovementDataIfNeeded();
        TryResolveTargetPlayer();
    }

    /// <summary>
    /// summary: 无 Rigidbody 时按帧直接修改 Transform，让敌人持续朝向并追踪玩家。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            StopMovement();
            return;
        }

        if (targetRigidbody != null)
        {
            return;
        }

        MoveTowardsTarget(Time.deltaTime);
    }

    /// <summary>
    /// summary: 有 Rigidbody 时在 FixedUpdate 中推进移动和旋转。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void FixedUpdate()
    {
        if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
        {
            StopMovement();
            return;
        }

        if (targetRigidbody == null)
        {
            return;
        }

        MoveTowardsTarget(Time.fixedDeltaTime);
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
        if (!TryGetTargetDirection(out Vector3 direction))
        {
            return false;
        }

        ApplyRotation(GetTargetRotation(direction));
        return true;
    }

    private void OnDisable()
    {
        StopMovement();
    }

    private void OnValidate()
    {
        ResolveMovementRigidbody();
        TryResolveEnemyData();
        MigrateLegacyMovementDataIfNeeded();
    }

    /// <summary>
    /// summary: 读取当前目标方向并推进敌人移动；拿不到目标时停止平面速度。
    /// param: deltaTime 本次移动使用的时间步长
    /// returns: 无
    /// </summary>
    private void MoveTowardsTarget(float deltaTime)
    {
        if (deltaTime <= 0f || !TryGetTargetDirection(out Vector3 direction))
        {
            StopMovement();
            return;
        }

        ApplyRotation(GetNextRotation(GetCurrentRotation(), GetTargetRotation(direction), deltaTime));
        ApplyMovement(direction, deltaTime);
    }

    /// <summary>
    /// summary: 把目标方向转换成实际位移或刚体速度，统一支持 Transform 与 Rigidbody 两种模式。
    /// param: direction 当前应前进的世界方向
    /// param: deltaTime 本次移动使用的时间步长
    /// returns: 无
    /// </summary>
    private void ApplyMovement(Vector3 direction, float deltaTime)
    {
        if (!TryResolveEnemyData())
        {
            return;
        }

        float moveSpeed = enemyData.MoveSpeed;
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
    /// summary: 当敌人没有可追踪目标或已经进入停止距离时，清空平面速度避免滑行。
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
    /// summary: 解析玩家相对敌人的平面方向；进入停止半径时返回 false。
    /// param: direction 输出的归一化平面方向
    /// returns: 成功拿到有效方向时返回 true
    /// </summary>
    private bool TryGetTargetDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!TryResolveTargetPlayer() || !TryResolveEnemyData())
        {
            return false;
        }

        Vector3 targetOffset = targetPlayer.position - GetCurrentPosition();
        targetOffset.y = 0f;
        float stoppingDistance = enemyData.StoppingDistance;
        if (targetOffset.sqrMagnitude <= Mathf.Max(stoppingDistance * stoppingDistance, MinimumDirectionSqrMagnitude))
        {
            return false;
        }

        direction = targetOffset.normalized;
        return true;
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
