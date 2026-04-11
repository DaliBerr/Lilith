using System;
using Kernel;
using Kernel.Bullet;
using Kernel.GameState;
using Kernel.MapGrid;
using UnityEngine;
using UnityEngine.InputSystem;
using Vocalith.Logging;

/// <summary>
/// 使用 PlayerControls 的 Movement 输入在当前相机视角对应的 gameplay plane 上移动玩家，并让玩家朝向鼠标投影点。
/// 同时负责根据点击地面的方向连续发射 CharBullet。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerPlaneMovement : MonoBehaviour
{
    private const float MinimumLookDirectionSqrMagnitude = 0.0001f;
    private const float MinimumFireInterval = 0.01f;
    private const float FireRaycastDistance = 10000f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField, Min(0f)] private float movementSkinWidth = 1f;
    [SerializeField] private LayerMask movementCollisionMask = Physics.DefaultRaycastLayers;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Camera targetCamera;

    [Header("Dash")]
    [SerializeField, Min(0f)] private float dashDistance = 6f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
    [SerializeField, Min(0f)] private float dashStaminaCost = 25f;
    [SerializeField, Min(0f)] private float staminaMax = 100f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 20f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 0.35f;

    [Header("Grounding")]
    [SerializeField] private MapGridAuthoring targetMapGrid;
    [SerializeField] private Collider groundingReferenceCollider;

    [Header("Bullet")]
    [SerializeField] private CharBullet bulletPrefab;
    [SerializeField] private Transform bulletSpawnOrigin;
    [SerializeField] private Vector3 bulletSpawnLocalOffset = new(0f, 0f, 32f);
    [SerializeField, Min(MinimumFireInterval)] private float fireInterval = 0.12f;
    [SerializeField] private LayerMask aimRaycastMask = Physics.DefaultRaycastLayers;
    [SerializeField] private AttackFormulaLoadout attackFormulaLoadout;

    private float nextFireTime;
    private CompiledAttack compiledAttackCache;
    private int compiledAttackRevision = -1;
    private int lastLoggedCompileFailureRevision = int.MinValue;
    private float currentStamina;
    private float staminaRegenResumeTime;
    private Vector3 lastMoveDirection = Vector3.forward;
    private Vector3 dashDirection = Vector3.forward;
    private float dashRemainingDistance;

    /// <summary>
    /// summary: 暴露当前玩家实际发射所使用的文字子弹 prefab，供背包预览等系统复用同一份表现资源。
    /// param: 无
    /// returns: 当前配置的文字子弹 prefab；未绑定时返回 null
    /// </summary>
    public CharBullet BulletPrefab => bulletPrefab;

    /// <summary>
    /// summary: 显式设置当前玩家移动与瞄准共用的地图网格，并立即重对齐到对应 gameplay plane。
    /// param: mapGrid 当前应绑定的地图网格
    /// returns: 成功绑定并完成平面对齐时返回 true
    /// </summary>
    public bool TrySetTargetMapGrid(MapGridAuthoring mapGrid)
    {
        if (mapGrid == null)
        {
            return false;
        }

        targetMapGrid = mapGrid;
        TryResolveGroundingReferenceCollider();
        EnsureGroundedRigidbodyConfiguration();
        return TrySnapToGameplayPlane();
    }

    private void Awake()
    {
        TryAutoAssignLoadout();
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        TryResolveTargetMapGrid();
        TryResolveGroundingReferenceCollider();
        EnsureGroundedRigidbodyConfiguration();
        TrySnapToGameplayPlane();
        SanitizeConfiguration();
        InitializeRuntimeDashState();
    }

    /// <summary>
    /// summary: 轮询输入、更新冲刺体力；若当前不存在 Rigidbody，则直接用运动学解算推进 Transform。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        if (IsGameplayInputBlockedByUI())
        {
            StopPlanarMotion();
            return;
        }

        UpdateStamina(Time.deltaTime);
        HandleFireInput();
        HandleDashInput();
        if (targetRigidbody != null)
        {
            return;
        }

        Vector3 delta = ResolveKinematicMovementDelta(GetMovementDelta(Time.deltaTime));
        Vector3 position = transform.position;
        position.x += delta.x;
        position.z += delta.z;
        transform.position = position;

        RotateTowardsMouse(Time.deltaTime);
    }

    /// <summary>
    /// summary: 使用运动学刚体在 FixedUpdate 中推进玩家、冲刺和旋转朝向。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void FixedUpdate()
    {
        if (IsGameplayInputBlockedByUI())
        {
            StopPlanarMotion();
            return;
        }

        if (targetRigidbody == null)
        {
            return;
        }

        Vector3 resolvedDelta = ResolveKinematicMovementDelta(GetMovementDelta(Time.fixedDeltaTime));
        targetRigidbody.MovePosition(targetRigidbody.position + resolvedDelta);
        RotateTowardsMouse(Time.fixedDeltaTime);
    }

    /// <summary>
    /// summary: 把输入系统的二维向量转换成当前相机视角对应的世界平面步行速度。
    /// param: 无
    /// returns: 当前期望的世界平面速度
    /// </summary>
    private Vector3 GetMovementVelocity()
    {
        if (!TryGetCurrentMoveDirection(out Vector3 planarDirection))
        {
            return Vector3.zero;
        }

        return planarDirection * moveSpeed;
    }

    /// <summary>
    /// summary: 把二维移动输入重映射到当前相机在 gameplay plane 上的 forward/right 基向量。
    /// param: input 已归一化或截断后的二维移动输入
    /// returns: 对应当前相机视角的世界平面方向；拿不到相机时回退到世界 XZ 方向
    /// </summary>
    private Vector3 GetPlanarMovementDirection(Vector2 input)
    {
        if (!TryGetPlanarCameraAxes(out Vector3 cameraRight, out Vector3 cameraForward))
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 moveDirection = (cameraRight * input.x) + (cameraForward * input.y);
        return Vector3.ClampMagnitude(moveDirection, 1f);
    }

    /// <summary>
    /// summary: 解析当前相机投影到水平面后的 right/forward，用于把移动输入转换成相对镜头的平面方向。
    /// param: cameraRight 输出的相机平面右方向
    /// param: cameraForward 输出的相机平面前方向
    /// returns: 成功从当前相机得到有效平面基向量时返回 true
    /// </summary>
    private bool TryGetPlanarCameraAxes(out Vector3 cameraRight, out Vector3 cameraForward)
    {
        cameraRight = Vector3.right;
        cameraForward = Vector3.forward;
        if (!TryGetTargetCamera(out Camera camera))
        {
            return false;
        }

        cameraForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (cameraForward.sqrMagnitude <= MinimumLookDirectionSqrMagnitude)
        {
            return false;
        }

        cameraForward.Normalize();
        cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;
        return true;
    }

    /// <summary>
    /// summary: 根据当前冲刺状态或目标平面速度和时间步长计算本帧位移。
    /// param: deltaTime 本次移动使用的时间步长
    /// returns: 本帧或本物理帧应当累加的世界位移
    /// </summary>
    private Vector3 GetMovementDelta(float deltaTime)
    {
        if (TryConsumeDashMovementDelta(deltaTime, out Vector3 dashDelta))
        {
            return dashDelta;
        }

        return GetMovementVelocity() * deltaTime;
    }

    /// <summary>
    /// summary: 解析运动学玩家位移，先阻挡正向穿透，再尝试沿碰撞面滑动。
    /// param name="desiredDelta": 本帧期望位移
    /// returns: 处理阻挡与滑墙后的最终安全位移
    /// </summary>
    private Vector3 ResolveKinematicMovementDelta(Vector3 desiredDelta)
    {
        desiredDelta.y = 0f;
        return ResolveKinematicMovementDeltaInternal(desiredDelta, 0);
    }

    /// <summary>
    /// summary: 递归解析运动学位移，支持一次主阻挡与有限次数滑墙修正。
    /// param name="desiredDelta": 当前待解析位移
    /// param name="depth": 当前递归深度
    /// returns: 当前层最终允许的安全位移
    /// </summary>
    private Vector3 ResolveKinematicMovementDeltaInternal(Vector3 desiredDelta, int depth)
    {
        const int maxSlideIterations = 2;

        float distance = desiredDelta.magnitude;
        if (distance <= 0f)
        {
            return Vector3.zero;
        }

        if (groundingReferenceCollider == null || targetRigidbody == null)
        {
            return desiredDelta;
        }

        Vector3 direction = desiredDelta / distance;
        RaycastHit[] hits = targetRigidbody.SweepTestAll(
            direction,
            distance + movementSkinWidth,
            QueryTriggerInteraction.Ignore);

        RaycastHit? nearestHit = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger)
            {
                continue;
            }

            if (IsTransformInsidePlayer(hit.collider.transform))
            {
                continue;
            }

            if (((1 << hit.collider.gameObject.layer) & movementCollisionMask.value) == 0)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        if (nearestHit == null)
        {
            return desiredDelta;
        }

        float allowedDistance = Mathf.Max(0f, nearestDistance - movementSkinWidth);
        Vector3 blockedMove = direction * allowedDistance;

        if (depth >= maxSlideIterations)
        {
            return blockedMove;
        }

        Vector3 remainingDelta = desiredDelta - blockedMove;
        remainingDelta.y = 0f;

        Vector3 slideDelta = Vector3.ProjectOnPlane(remainingDelta, nearestHit.Value.normal);
        slideDelta.y = 0f;

        if (slideDelta.sqrMagnitude <= 0.000001f)
        {
            return blockedMove;
        }

        return blockedMove + ResolveKinematicMovementDeltaInternal(slideDelta, depth + 1);
    }

    /// <summary>
    /// summary: 轮询射击输入，在按住鼠标左键时按固定间隔连续发射文字子弹。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void HandleFireInput()
    {
        if (!IsFiring() || Time.time < nextFireTime)
        {
            return;
        }

        if (!TryFireBullet())
        {
            return;
        }

        nextFireTime = Time.time + fireInterval;
    }

    /// <summary>
    /// summary: 轮询冲刺输入，在按下加速键时触发一次短距离冲刺，而不是持续提高移动速度。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void HandleDashInput()
    {
        if (!IsDashTriggered())
        {
            return;
        }

        TryStartDash();
    }

    /// <summary>
    /// summary: 启动一次隐藏体力消耗的冲刺；当前体力不足时直接失败。
    /// param: 无
    /// returns: 成功进入冲刺时返回 true
    /// </summary>
    private bool TryStartDash()
    {
        if (dashRemainingDistance > 0f || dashDistance <= 0f || dashDuration <= 0f || currentStamina < dashStaminaCost)
        {
            return false;
        }

        if (!TryGetDashDirection(out Vector3 resolvedDashDirection))
        {
            return false;
        }

        currentStamina = Mathf.Max(0f, currentStamina - dashStaminaCost);
        dashDirection = resolvedDashDirection;
        dashRemainingDistance = dashDistance;
        staminaRegenResumeTime = float.PositiveInfinity;
        return true;
    }

    /// <summary>
    /// summary: 若当前正处于冲刺，则按本帧时间步长消费一段固定距离。
    /// param: deltaTime 本次移动使用的时间步长
    /// param: dashDelta 输出的冲刺位移
    /// returns: 当前帧存在可消费的冲刺距离时返回 true
    /// </summary>
    private bool TryConsumeDashMovementDelta(float deltaTime, out Vector3 dashDelta)
    {
        dashDelta = Vector3.zero;
        if (dashRemainingDistance <= 0f || deltaTime <= 0f)
        {
            return false;
        }

        float dashSpeed = dashDistance / dashDuration;
        float stepDistance = Mathf.Min(dashRemainingDistance, dashSpeed * deltaTime);
        if (stepDistance <= 0f)
        {
            EndDash();
            return false;
        }

        dashRemainingDistance -= stepDistance;
        dashDelta = dashDirection * stepDistance;
        if (dashRemainingDistance <= 0f)
        {
            EndDash();
        }

        return true;
    }

    /// <summary>
    /// summary: 按固定速率恢复隐藏体力槽；冲刺期间和冲刺后的短暂延迟内不会恢复。
    /// param: deltaTime 本次恢复使用的时间步长
    /// returns: 无
    /// </summary>
    private void UpdateStamina(float deltaTime)
    {
        if (deltaTime <= 0f || currentStamina >= staminaMax || Time.time < staminaRegenResumeTime)
        {
            return;
        }

        currentStamina = Mathf.MoveTowards(currentStamina, staminaMax, staminaRegenPerSecond * deltaTime);
    }

    /// <summary>
    /// summary: 优先按当前移动输入获取冲刺方向；无法解析时回退到最近一次有效移动方向。
    /// param: dashDirection 输出的平面冲刺方向
    /// returns: 成功得到有效方向时返回 true
    /// </summary>
    private bool TryGetDashDirection(out Vector3 dashDirection)
    {
        if (TryGetCurrentMoveDirection(out Vector3 currentMoveDirection))
        {
            dashDirection = currentMoveDirection;
            return true;
        }

        dashDirection = lastMoveDirection;
        dashDirection.y = 0f;
        if (dashDirection.sqrMagnitude <= MinimumLookDirectionSqrMagnitude)
        {
            return false;
        }

        dashDirection.Normalize();
        return true;
    }

    /// <summary>
    /// summary: 读取当前帧的移动输入并转换成世界平面方向，同时缓存最后一次有效移动方向。
    /// param: movementDirection 输出的世界平面方向
    /// returns: 当前存在有效移动输入时返回 true
    /// </summary>
    private bool TryGetCurrentMoveDirection(out Vector3 movementDirection)
    {
        movementDirection = Vector3.zero;
        Vector2 input = ReadMoveInput();
        input = Vector2.ClampMagnitude(input, 1f);
        if (input.sqrMagnitude <= 0f)
        {
            return false;
        }

        movementDirection = GetPlanarMovementDirection(input);
        movementDirection.y = 0f;
        if (movementDirection.sqrMagnitude <= MinimumLookDirectionSqrMagnitude)
        {
            return false;
        }

        movementDirection.Normalize();
        lastMoveDirection = movementDirection;
        return true;
    }

    /// <summary>
    /// summary: 结束当前冲刺并重置体力恢复延迟。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EndDash()
    {
        if (!float.IsPositiveInfinity(staminaRegenResumeTime))
        {
            return;
        }

        dashRemainingDistance = 0f;
        staminaRegenResumeTime = Time.time + staminaRegenDelay;
    }

    /// <summary>
    /// summary: 生成并初始化一发文字子弹。
    /// param: 无
    /// returns: 成功生成并发射时返回 true
    /// </summary>
    private bool TryFireBullet()
    {
        if (bulletPrefab == null || !TryGetBulletDirection(out Vector3 spawnPosition, out Vector3 bulletDirection))
        {
            return false;
        }

        if (!TryResolveAttackForFiring(out CompiledAttack compiledAttack))
        {
            return false;
        }

        return AttackProjectileEmitter.Emit(bulletPrefab, transform, spawnPosition, bulletDirection, compiledAttack) > 0;
    }

    /// <summary>
    /// summary: 结合鼠标落点和发射点计算当前子弹的世界方向。
    /// param: spawnPosition 输出的出生点
    /// param: bulletDirection 输出的归一化发射方向
    /// returns: 成功得到有效方向时返回 true
    /// </summary>
    private bool TryGetBulletDirection(out Vector3 spawnPosition, out Vector3 bulletDirection)
    {
        spawnPosition = GetBulletSpawnPosition();
        bulletDirection = Vector3.zero;
        if (!TryGetBulletAimPoint(out Vector3 aimPoint))
        {
            return false;
        }

        Vector3 shotDirection = aimPoint - spawnPosition;
        shotDirection.y = 0f;
        if (shotDirection.sqrMagnitude <= MinimumLookDirectionSqrMagnitude)
        {
            return false;
        }

        bulletDirection = shotDirection.normalized;
        return true;
    }

    /// <summary>
    /// summary: 计算当前发射点世界坐标；优先使用显式指定锚点，否则回退到玩家自身并附加局部偏移。
    /// param: 无
    /// returns: 当前用于发射的世界坐标
    /// </summary>
    private Vector3 GetBulletSpawnPosition()
    {
        Transform spawnRoot = bulletSpawnOrigin != null ? bulletSpawnOrigin : transform;
        return spawnRoot.TransformPoint(bulletSpawnLocalOffset);
    }

    /// <summary>
    /// summary: 先尝试命中真实地面碰撞体，失败后再回退到玩家高度平面投影，得到当前射击目标点。
    /// param: aimPoint 输出的目标世界坐标
    /// returns: 成功得到目标点时返回 true
    /// </summary>
    private bool TryGetBulletAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;
        if (!TryCreateMouseRay(out Ray ray))
        {
            return false;
        }

        if (TryGetRaycastAimPoint(ray, out aimPoint))
        {
            return true;
        }

        if (!TryProjectRayOntoGameplayPlane(ray, out aimPoint))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// summary: 用真实碰撞体进行瞄准判定，并跳过玩家自身碰撞体。
    /// param: ray 当前鼠标射线
    /// param: aimPoint 输出的命中点
    /// returns: 命中有效地面或格子碰撞体时返回 true
    /// </summary>
    private bool TryGetRaycastAimPoint(Ray ray, out Vector3 aimPoint)
    {
        aimPoint = default;
        RaycastHit[] hits = Physics.RaycastAll(ray, FireRaycastDistance, aimRaycastMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, CompareRaycastDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || IsTransformInsidePlayer(hit.collider.transform))
            {
                continue;
            }

            aimPoint = hit.point;
            return true;
        }

        return false;
    }

    /// <summary>
    /// summary: 判断一个 Transform 是否属于玩家根节点自身或其子节点。
    /// param: candidate 需要判断的 Transform
    /// returns: 属于玩家层级时返回 true
    /// </summary>
    private bool IsTransformInsidePlayer(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }

    /// <summary>
    /// summary: 让玩家沿 Y 轴朝向鼠标在角色高度平面上的投影点。
    /// param: deltaTime 本次旋转使用的时间步长
    /// returns: 无
    /// </summary>
    private void RotateTowardsMouse(float deltaTime)
    {
        if (!TryGetTargetRotation(out Quaternion targetRotation))
        {
            return;
        }

        Quaternion currentRotation = targetRigidbody != null ? targetRigidbody.rotation : transform.rotation;
        Quaternion nextRotation = GetNextRotation(currentRotation, targetRotation, deltaTime);
        if (targetRigidbody != null)
        {
            targetRigidbody.MoveRotation(nextRotation);
            return;
        }

        transform.rotation = nextRotation;
    }

    /// <summary>
    /// summary: 计算鼠标在角色所在水平面上的投影方向，并转换为角色应有的朝向。
    /// param: targetRotation 输出的目标朝向
    /// returns: 成功拿到目标朝向时返回 true
    /// </summary>
    private bool TryGetTargetRotation(out Quaternion targetRotation)
    {
        targetRotation = Quaternion.identity;
        if (!TryGetMouseWorldPoint(out Vector3 mouseWorldPoint))
        {
            return false;
        }

        Vector3 forward = mouseWorldPoint - GetRotationOrigin();
        forward.y = 0f;
        if (forward.sqrMagnitude <= MinimumLookDirectionSqrMagnitude)
        {
            return false;
        }

        targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return true;
    }

    /// <summary>
    /// summary: 使用主相机或显式指定相机，把鼠标屏幕坐标投影到角色所在水平面。
    /// param: mouseWorldPoint 输出的鼠标世界坐标
    /// returns: 当相机和鼠标都可用且成功命中水平面时返回 true
    /// </summary>
    private bool TryGetMouseWorldPoint(out Vector3 mouseWorldPoint)
    {
        mouseWorldPoint = default;
        if (!TryCreateMouseRay(out Ray ray))
        {
            return false;
        }

        return TryProjectRayOntoGameplayPlane(ray, out mouseWorldPoint);
    }

    /// <summary>
    /// summary: 使用当前鼠标位置和目标相机创建一条屏幕射线。
    /// param: ray 输出的屏幕射线
    /// returns: 成功拿到相机和鼠标输入时返回 true
    /// </summary>
    private bool TryCreateMouseRay(out Ray ray)
    {
        ray = default;
        if (!TryGetTargetCamera(out Camera camera) || Mouse.current == null)
        {
            return false;
        }

        ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        return true;
    }

    /// <summary>
    /// summary: 获取当前角色用于朝向和瞄准计算的世界原点；有 Rigidbody 时优先使用刚体位置。
    /// param: 无
    /// returns: 角色当前用于旋转和瞄准计算的世界位置
    /// </summary>
    private Vector3 GetRotationOrigin()
    {
        return targetRigidbody != null ? targetRigidbody.position : transform.position;
    }

    /// <summary>
    /// summary: 获取当前用于鼠标投影的相机；未显式指定时尝试回退到 Main Camera。
    /// param: camera 输出的可用相机
    /// returns: 找到可用相机时返回 true
    /// </summary>
    private bool TryGetTargetCamera(out Camera camera)
    {
        camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null)
        {
            return false;
        }

        targetCamera = camera;
        return true;
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

        targetMapGrid = FindFirstObjectByType<MapGridAuthoring>();
        return targetMapGrid != null;
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定 grounded 参考 Collider 时，尝试自动在玩家层级内解析一个可用碰撞体。
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
    /// summary: 统一规范 grounded 玩家刚体，强制使用不受重力影响的运动学刚体。
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
        targetRigidbody.isKinematic = true;
    }

    /// <summary>
    /// summary: 按 grounded 根节点契约把玩家抬到当前地图平面高度上。
    /// param: 无
    /// returns: 成功完成 grounded snap 时返回 true
    /// </summary>
    private bool TrySnapToGameplayPlane()
    {
        float planeY = GetGameplayPlaneY();
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
    /// summary: 获取当前移动、瞄准和 grounded 逻辑应该使用的共享地图平面高度。
    /// param: 无
    /// returns: 共享地图平面的世界 Y；找不到地图时回退到玩家当前根节点高度
    /// </summary>
    private float GetGameplayPlaneY()
    {
        return TryResolveTargetMapGrid() ? targetMapGrid.WorldPlaneY : GetRotationOrigin().y;
    }

    /// <summary>
    /// summary: 用共享地图平面高度投影一条世界射线，供鼠标瞄准和回退平面命中使用。
    /// param: ray 当前需要投影的世界射线
    /// param: worldPoint 输出的命中世界坐标
    /// returns: 成功命中共享地图平面时返回 true
    /// </summary>
    private bool TryProjectRayOntoGameplayPlane(Ray ray, out Vector3 worldPoint)
    {
        return WorldHeightUtility.TryProjectRayOntoPlaneY(ray, GetGameplayPlaneY(), out worldPoint);
    }

    /// <summary>
    /// summary: 依据旋转速度上限，计算当前帧应该采用的朝向。
    /// param: currentRotation 当前朝向
    /// param: targetRotation 目标朝向
    /// param: deltaTime 本次旋转使用的时间步长
    /// returns: 本帧应应用的朝向
    /// </summary>
    private Quaternion GetNextRotation(Quaternion currentRotation, Quaternion targetRotation, float deltaTime)
    {
        if (rotationSpeed <= 0f)
        {
            return targetRotation;
        }

        return Quaternion.RotateTowards(currentRotation, targetRotation, rotationSpeed * deltaTime);
    }

    /// <summary>
    /// summary: 修正当前组件的发射参数，确保冷却和生命周期配置处于可运行范围。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        fireInterval = Mathf.Max(MinimumFireInterval, fireInterval);
        movementSkinWidth = Mathf.Max(0f, movementSkinWidth);
        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        dashStaminaCost = Mathf.Max(0f, dashStaminaCost);
        staminaMax = Mathf.Max(0f, staminaMax);
        staminaRegenPerSecond = Mathf.Max(0f, staminaRegenPerSecond);
        staminaRegenDelay = Mathf.Max(0f, staminaRegenDelay);
        currentStamina = Mathf.Clamp(currentStamina, 0f, staminaMax);
    }

    /// <summary>
    /// summary: 初始化每次实例化都应从满体力开始的隐藏冲刺状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void InitializeRuntimeDashState()
    {
        currentStamina = staminaMax;
        staminaRegenResumeTime = 0f;
        dashRemainingDistance = 0f;
        lastMoveDirection = Vector3.forward;
        dashDirection = lastMoveDirection;
    }

    private void OnValidate()
    {
        TryAutoAssignLoadout();
        TryResolveTargetMapGrid();
        TryResolveGroundingReferenceCollider();
        EnsureGroundedRigidbodyConfiguration();
        TrySnapToGameplayPlane();
        SanitizeConfiguration();
    }

    /// <summary>
    /// summary: 从词槽 loadout 读取并缓存最新编译结果。
    /// param: compiledAttack 输出的最终发射配置
    /// returns: 当前存在可执行攻击时返回 true
    /// </summary>
    private bool TryResolveAttackForFiring(out CompiledAttack compiledAttack)
    {
        compiledAttack = null;
        if (attackFormulaLoadout == null || !attackFormulaLoadout.HasTokens)
        {
            LogCompileFailureIfNeeded(null);
            return false;
        }

        if (compiledAttackCache == null || compiledAttackRevision != attackFormulaLoadout.Revision)
        {
            compiledAttackCache = attackFormulaLoadout.Recompile();
            compiledAttackRevision = attackFormulaLoadout.Revision;
        }

        compiledAttack = compiledAttackCache;
        if (compiledAttack != null && compiledAttack.CanFire)
        {
            lastLoggedCompileFailureRevision = int.MinValue;
            return true;
        }

        LogCompileFailureIfNeeded(compiledAttack);
        return false;
    }

    /// <summary>
    /// summary: 当同物体上挂有 loadout 组件时自动建立引用，减少场景接线遗漏。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void TryAutoAssignLoadout()
    {
        if (attackFormulaLoadout == null)
        {
            attackFormulaLoadout = GetComponent<AttackFormulaLoadout>();
        }
    }

    /// <summary>
    /// summary: 仅在 loadout 修订号变化时输出一次编译失败日志，避免按住开火时刷屏。
    /// param: compiledAttack 当前失败的编译结果
    /// returns: 无
    /// </summary>
    private void LogCompileFailureIfNeeded(CompiledAttack compiledAttack)
    {
        int currentRevision = attackFormulaLoadout != null ? attackFormulaLoadout.Revision : int.MinValue;
        if (currentRevision == lastLoggedCompileFailureRevision)
        {
            return;
        }

        lastLoggedCompileFailureRevision = currentRevision;
        if (compiledAttack == null)
        {
            if (attackFormulaLoadout == null)
            {
                GameDebug.LogWarning("[PlayerPlaneMovement] Attack formula loadout is missing. Firing is disabled until a loadout is assigned.");
                return;
            }

            if (!attackFormulaLoadout.HasTokens)
            {
                GameDebug.LogWarning("[PlayerPlaneMovement] Attack formula loadout is empty. Firing is disabled until at least one valid core formula is equipped.");
                return;
            }

            GameDebug.LogWarning("[PlayerPlaneMovement] Attack formula loadout failed to compile and produced no result.");
            return;
        }

        for (int i = 0; i < compiledAttack.Messages.Count; i++)
        {
            AttackCompileMessage message = compiledAttack.Messages[i];
            if (message.severity == AttackCompileMessageSeverity.Error)
            {
                GameDebug.LogError($"[PlayerPlaneMovement] Attack formula error: {message.message} token='{message.tokenId}'");
            }
            else if (message.severity == AttackCompileMessageSeverity.Warning)
            {
                GameDebug.LogWarning($"[PlayerPlaneMovement] Attack formula warning: {message.message} token='{message.tokenId}'");
            }
        }
    }

    /// <summary>
    /// summary: 比较两次射线命中的距离，用于筛选最近的真实地面命中点。
    /// param: left 左侧命中结果
    /// param: right 右侧命中结果
    /// returns: 标准 CompareTo 结果
    /// </summary>
    private static int CompareRaycastDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }

    /// <summary>
    /// summary: 从 InputActionManager 读取 PlayerControls 的移动输入。
    /// param: 无
    /// returns: 当前移动输入；当输入系统未准备好时返回零向量
    /// </summary>
    private static Vector2 ReadMoveInput()
    {
        InputActionManager inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return Vector2.zero;
        }

        return inputManager.Player.Movement.Move.ReadValue<Vector2>();
    }

    /// <summary>
    /// summary: 从 InputActionManager 读取 PlayerControls 的冲刺按钮触发状态。
    /// param: 无
    /// returns: 冲刺按钮在本帧按下时返回 true
    /// </summary>
    private static bool IsDashTriggered()
    {
        InputActionManager inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return false;
        }

        return inputManager.Player.Movement.Accelerate.WasPressedThisFrame();
    }

    /// <summary>
    /// summary: 从 InputActionManager 读取 PlayerControls 的射击按钮状态。
    /// param: 无
    /// returns: 射击按钮当前按下时返回 true
    /// </summary>
    private static bool IsFiring()
    {
        InputActionManager inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return false;
        }

        return inputManager.Player.Movement.Fire.IsPressed();
    }

    /// <summary>
    /// summary: 检查当前是否存在会阻断战斗输入的 UI；背包、暂停菜单和对话界面打开时玩家的移动、转向与射击都会暂停。
    /// param: 无
    /// returns: 当前存在会冻结战斗交互的状态时返回 true
    /// </summary>
    private static bool IsGameplayInputBlockedByUI()
    {
        return StatusController.HasStatus(StatusList.InBackPackStatus)
            || StatusController.HasStatus(StatusList.InPauseMenuStatus)
            || StatusController.HasStatus(StatusList.InDialogStatus)
            || StatusController.HasStatus(StatusList.PausedStatus);
    }

    /// <summary>
    /// summary: 当战斗输入被 UI 阻断时，停止当前冲刺，避免角色继续按上一帧的 dash 轨迹位移。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void StopPlanarMotion()
    {
        EndDash();
    }
}
