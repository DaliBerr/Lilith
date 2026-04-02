using System;
using Kernel;
using Kernel.Bullet;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 使用 PlayerControls 的 Movement 输入在世界 XZ 平面移动玩家，并让玩家朝向鼠标投影点。
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

    [Header("Bullet")]
    [SerializeField] private CharBullet bulletPrefab;
    [SerializeField] private Transform bulletSpawnOrigin;
    [SerializeField] private Vector3 bulletSpawnLocalOffset = new(0f, 0f, 32f);
    [SerializeField, Min(MinimumFireInterval)] private float fireInterval = 0.12f;
    [SerializeField] private LayerMask aimRaycastMask = Physics.DefaultRaycastLayers;
    [SerializeField] private AttackSpec attackSpec = AttackSpec.CreateDefault();

    private float nextFireTime;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        SanitizeConfiguration();
    }

    /// <summary>
    /// summary: 无 Rigidbody 时，按帧直接修改 Transform 的 XZ，并处理射击输入。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
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
    /// summary: 把输入系统的二维向量转换成世界 XZ 平面的平面速度。
    /// param: 无
    /// returns: 当前期望的世界平面速度
    /// </summary>
    private Vector3 GetMovementVelocity()
    {
        Vector2 input = ReadMoveInput();
        input = Vector2.ClampMagnitude(input, 1f);

        float currentMoveSpeed = moveSpeed * (IsAccelerating() ? AcceleratedSpeedMultiplier : 1f);
        return new Vector3(input.x, 0f, input.y) * currentMoveSpeed;
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

        CharBullet bulletInstance = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
        bulletInstance.InitializeShot(transform, spawnPosition, bulletDirection, attackSpec);
        return true;
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

        Plane fallbackPlane = new(Vector3.up, GetRotationOrigin());
        if (!fallbackPlane.Raycast(ray, out float hitDistance))
        {
            return false;
        }

        aimPoint = ray.GetPoint(hitDistance);
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

        Plane movementPlane = new(Vector3.up, GetRotationOrigin());
        if (!movementPlane.Raycast(ray, out float hitDistance))
        {
            return false;
        }

        mouseWorldPoint = ray.GetPoint(hitDistance);
        return true;
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
        attackSpec = attackSpec.GetSanitized();
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
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
}
