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
    private const float AcceleratedSpeedMultiplier = 1.5f;
    private const float MinimumLookDirectionSqrMagnitude = 0.0001f;
    private const float MinimumFireInterval = 0.01f;
    private const float FireRaycastDistance = 10000f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Camera targetCamera;

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
    }

    /// <summary>
    /// summary: 无 Rigidbody 时，按帧直接修改 Transform 的 XZ，并处理射击输入。
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

        HandleFireInput();
        if (targetRigidbody != null)
        {
            return;
        }

        Vector3 delta = GetMovementDelta(Time.deltaTime);
        Vector3 position = transform.position;
        position.x += delta.x;
        position.z += delta.z;
        transform.position = position;

        RotateTowardsMouse(Time.deltaTime);
    }

    /// <summary>
    /// summary: 有 Rigidbody 时，在 FixedUpdate 中推进玩家并旋转朝向。
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

        if (targetRigidbody.isKinematic)
        {
            targetRigidbody.MovePosition(targetRigidbody.position + GetMovementDelta(Time.fixedDeltaTime));
        }
        else
        {
            ApplyDynamicRigidbodyVelocity();
        }

        RotateTowardsMouse(Time.fixedDeltaTime);
    }

    /// <summary>
    /// summary: 对动态 Rigidbody 直接写入平面速度，让碰撞阻挡由物理系统自然处理。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ApplyDynamicRigidbodyVelocity()
    {
        Vector3 currentVelocity = targetRigidbody.linearVelocity;
        Vector3 desiredVelocity = GetMovementVelocity();
        currentVelocity.x = desiredVelocity.x;
        currentVelocity.z = desiredVelocity.z;
        targetRigidbody.linearVelocity = currentVelocity;
    }

    /// <summary>
    /// summary: 把输入系统的二维向量转换成当前相机视角对应的世界平面速度。
    /// param: 无
    /// returns: 当前期望的世界平面速度
    /// </summary>
    private Vector3 GetMovementVelocity()
    {
        Vector2 input = ReadMoveInput();
        input = Vector2.ClampMagnitude(input, 1f);
        if (input.sqrMagnitude <= 0f)
        {
            return Vector3.zero;
        }

        float currentMoveSpeed = moveSpeed * (IsAccelerating() ? AcceleratedSpeedMultiplier : 1f);
        Vector3 planarDirection = GetPlanarMovementDirection(input);
        return planarDirection * currentMoveSpeed;
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
    /// summary: 根据目标平面速度和时间步长计算本帧位移。
    /// param: deltaTime 本次移动使用的时间步长
    /// returns: 本帧或本物理帧应当累加的世界位移
    /// </summary>
    private Vector3 GetMovementDelta(float deltaTime)
    {
        return GetMovementVelocity() * deltaTime;
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
    /// summary: 统一规范 grounded 玩家刚体，确保角色始终停留在地图平面对应的世界高度上。
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
    /// summary: 从 InputActionManager 读取 PlayerControls 的加速按钮状态。
    /// param: 无
    /// returns: 加速按钮当前按下时返回 true
    /// </summary>
    private static bool IsAccelerating()
    {
        InputActionManager inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return false;
        }

        return inputManager.Player.Movement.Accelerate.IsPressed();
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
    /// summary: 检查当前是否存在会阻断战斗输入的 UI；背包和暂停菜单打开时玩家的移动、转向与射击都会暂停。
    /// param: 无
    /// returns: 当前存在背包或暂停菜单状态时返回 true
    /// </summary>
    private static bool IsGameplayInputBlockedByUI()
    {
        return StatusController.HasStatus(StatusList.InBackPackStatus)
            || StatusController.HasStatus(StatusList.InPauseMenuStatus)
            || StatusController.HasStatus(StatusList.PausedStatus);
    }

    /// <summary>
    /// summary: 背包打开时清零刚体的平面速度，避免动态刚体沿用上一帧的惯性继续滑动。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void StopPlanarMotion()
    {
        if (targetRigidbody == null || targetRigidbody.isKinematic)
        {
            return;
        }

        Vector3 currentVelocity = targetRigidbody.linearVelocity;
        if (Mathf.Approximately(currentVelocity.x, 0f) && Mathf.Approximately(currentVelocity.z, 0f))
        {
            return;
        }

        currentVelocity.x = 0f;
        currentVelocity.z = 0f;
        targetRigidbody.linearVelocity = currentVelocity;
    }
}
