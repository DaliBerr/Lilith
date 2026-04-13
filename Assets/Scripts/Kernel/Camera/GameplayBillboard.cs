using UnityEngine;

/// <summary>
/// 让关键 gameplay 视觉层始终对齐主相机，保持透视镜头下的可读性。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayBillboard : MonoBehaviour
{
    private const float MinimumMotionSqrMagnitude = 0.0001f;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool automaticFacingEnabled = true;

    [Header("Motion")]
    [SerializeField] private bool motionOffsetEnabled = true;
    [SerializeField] private Transform motionSource;
    [SerializeField] private PlayerPlaneMovement playerMovementSource;
    [SerializeField, Min(0f)] private float movementSwayAmplitude = 0.75f;
    [SerializeField, Min(0f)] private float movementSwayFrequency = 5.5f;
    [SerializeField, Min(0f)] private float movementSwayBlendSpeed = 8f;
    [SerializeField, Min(0.01f)] private float movementSpeedForMaxSway = 168f;
    [SerializeField, Min(0f)] private float dashPulseAmplitude = 1.2f;
    [SerializeField, Min(0.01f)] private float dashPulseDuration = 0.3f;

    public Camera TargetCamera => targetCamera;
    public bool AutomaticFacingEnabled => automaticFacingEnabled;

    private Vector3 anchorLocalPosition;
    private Vector3 anchorWorldPosition;
    private Vector3 lastMotionSourcePosition;
    private float swayPhase;
    private float swayBlend;
    private float dashPulseElapsed = float.PositiveInfinity;
    private bool wasDashingLastFrame;
    private bool hasCachedAnchorPose;

    private void OnEnable()
    {
        TryResolveMotionBindings();
        SanitizeConfiguration();
        CacheAnchorPose(forceRefresh: true);
        SyncMotionSourcePosition();
        ApplyFacing();
    }

    private void LateUpdate()
    {
        ApplyFacing();
    }

    private void OnValidate()
    {
        TryResolveMotionBindings();
        CacheAnchorPose(forceRefresh: true);
        SanitizeConfiguration();
        ApplyFacing();
    }

    private void OnDisable()
    {
        ApplyAnchorPosition(Vector3.zero);
    }

    /// <summary>
    /// summary: 把当前视觉节点旋转到与目标相机一致的朝向，让文本和 sprite 平面保持与屏幕平行。
    /// param: 无
    /// returns: 成功解析到目标相机并完成旋转时返回 true
    /// </summary>
    public bool ApplyFacing()
    {
        if (!automaticFacingEnabled || !TryResolveTargetCamera(out Camera resolvedCamera))
        {
            ApplyAnchorPosition(Vector3.zero);
            return false;
        }

        transform.rotation = Quaternion.LookRotation(
            resolvedCamera.transform.forward,
            resolvedCamera.transform.up);
        ApplyAnchorPosition(ResolveMotionOffset(resolvedCamera.transform));
        return true;
    }

    /// <summary>
    /// summary: 解析当前需要对齐的目标相机；未显式绑定时优先回退到 Main Camera。
    /// param: resolvedCamera 输出的目标相机
    /// returns: 成功拿到相机时返回 true
    /// </summary>
    private bool TryResolveTargetCamera(out Camera resolvedCamera)
    {
        resolvedCamera = targetCamera != null ? targetCamera : Camera.main;
        if (resolvedCamera == null)
        {
            resolvedCamera = FindAnyObjectByType<Camera>();
        }

        if (resolvedCamera == null)
        {
            return false;
        }

        targetCamera = resolvedCamera;
        return true;
    }

    /// <summary>
    /// summary: 缓存当前 billboard 的基准锚点；后续所有晃动都会相对这个局部位置叠加，避免漂移。
    /// param name="forceRefresh": 为 true 时强制用当前 Transform 重新记录锚点
    /// returns: 成功记录到当前锚点时返回 true
    /// </summary>
    private bool CacheAnchorPose(bool forceRefresh)
    {
        if (hasCachedAnchorPose && !forceRefresh)
        {
            return true;
        }

        anchorLocalPosition = transform.localPosition;
        anchorWorldPosition = transform.position;
        hasCachedAnchorPose = true;
        return true;
    }

    /// <summary>
    /// summary: 解析当前 billboard 应绑定的运动来源；玩家字优先跟随父节点和父层级中的 PlayerPlaneMovement。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void TryResolveMotionBindings()
    {
        if (motionSource == null && transform.parent != null)
        {
            motionSource = transform.parent;
        }

        if (playerMovementSource == null)
        {
            playerMovementSource = GetComponentInParent<PlayerPlaneMovement>();
        }
    }

    /// <summary>
    /// summary: 把当前物体恢复到基准锚点位置，并在其上叠加世界空间偏移。
    /// param name="worldOffset": 本帧需要附加到锚点上的世界空间位移
    /// returns: 无
    /// </summary>
    private void ApplyAnchorPosition(Vector3 worldOffset)
    {
        if (!CacheAnchorPose(forceRefresh: false))
        {
            return;
        }

        transform.position = GetAnchorWorldPosition() + worldOffset;
    }

    /// <summary>
    /// summary: 计算当前锚点在世界空间中的位置；有父节点时始终以初始局部锚点跟随父节点。
    /// param: 无
    /// returns: 当前锚点对应的世界坐标
    /// </summary>
    private Vector3 GetAnchorWorldPosition()
    {
        if (transform.parent != null)
        {
            return transform.parent.TransformPoint(anchorLocalPosition);
        }

        return anchorWorldPosition;
    }

    /// <summary>
    /// summary: 根据玩家移动或冲刺状态生成本帧附加在 billboard 上的世界空间位移。
    /// param name="cameraTransform": 当前对齐使用的相机 Transform
    /// returns: 本帧的综合晃动位移
    /// </summary>
    private Vector3 ResolveMotionOffset(Transform cameraTransform)
    {
        if (!motionOffsetEnabled)
        {
            ResetMotionState();
            return Vector3.zero;
        }

        Vector3 planarVelocity = ResolvePlanarVelocity(out bool isDashing);
        float deltaTime = Application.isPlaying ? Time.deltaTime : 0f;
        UpdateMotionState(planarVelocity, isDashing, deltaTime);
        if (swayBlend <= 0f && dashPulseElapsed >= dashPulseDuration)
        {
            return Vector3.zero;
        }

        GetMotionAxes(planarVelocity, cameraTransform, out Vector3 forwardAxis, out Vector3 sideAxis);
        float swayOffset = Mathf.Sin(swayPhase) * movementSwayAmplitude * swayBlend;
        float dashOffset = EvaluateDashPulseOffset();
        return (sideAxis * swayOffset) + (forwardAxis * dashOffset);
    }

    /// <summary>
    /// summary: 优先读取玩家移动组件的显式运动信息；没有玩家驱动时回退到运动来源位置差分。
    /// param name="isDashing": 输出的当前是否处于冲刺脉冲窗口
    /// returns: 当前可用于表现层的世界平面速度
    /// </summary>
    private Vector3 ResolvePlanarVelocity(out bool isDashing)
    {
        isDashing = false;
        if (playerMovementSource != null)
        {
            if (playerMovementSource.TryGetBillboardMotion(out Vector3 playerVelocity, out isDashing))
            {
                return Vector3.ProjectOnPlane(playerVelocity, Vector3.up);
            }

            return Vector3.zero;
        }

        return ResolveFallbackPlanarVelocity();
    }

    /// <summary>
    /// summary: 对没有玩家运动组件的 billboard，用绑定来源在世界空间中的位移差分估算平面速度。
    /// param: 无
    /// returns: 当前运动来源的世界平面速度
    /// </summary>
    private Vector3 ResolveFallbackPlanarVelocity()
    {
        if (!Application.isPlaying || motionSource == null)
        {
            SyncMotionSourcePosition();
            return Vector3.zero;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 currentPosition = motionSource.position;
        Vector3 planarVelocity = (currentPosition - lastMotionSourcePosition) / deltaTime;
        lastMotionSourcePosition = currentPosition;
        planarVelocity.y = 0f;
        return planarVelocity;
    }

    /// <summary>
    /// summary: 按当前速度和冲刺状态推进晃动动画的内部相位与包络。
    /// param name="planarVelocity": 当前驱动 billboard 的世界平面速度
    /// param name="isDashing": 当前是否正处于冲刺状态
    /// param name="deltaTime": 本次更新使用的时间步长
    /// returns: 无
    /// </summary>
    private void UpdateMotionState(Vector3 planarVelocity, bool isDashing, float deltaTime)
    {
        float targetSwayBlend = 0f;
        float planarSpeed = planarVelocity.magnitude;
        if (planarSpeed > MinimumMotionSqrMagnitude)
        {
            targetSwayBlend = Mathf.Clamp01(planarSpeed / movementSpeedForMaxSway);
        }

        if (!Application.isPlaying)
        {
            swayBlend = targetSwayBlend;
            swayPhase = targetSwayBlend > 0f ? Mathf.PI * 0.5f : 0f;
            dashPulseElapsed = float.PositiveInfinity;
            wasDashingLastFrame = isDashing;
            return;
        }

        swayBlend = Mathf.MoveTowards(swayBlend, targetSwayBlend, movementSwayBlendSpeed * deltaTime);
        if (swayBlend > 0f)
        {
            swayPhase += deltaTime * movementSwayFrequency * Mathf.PI * 2f;
            if (swayPhase > Mathf.PI * 2f)
            {
                swayPhase -= Mathf.PI * 2f;
            }
        }

        if (isDashing && !wasDashingLastFrame)
        {
            dashPulseElapsed = 0f;
        }

        wasDashingLastFrame = isDashing;
        if (dashPulseElapsed < dashPulseDuration)
        {
            dashPulseElapsed += deltaTime;
        }
    }

    /// <summary>
    /// summary: 解析晃动应该使用的前后轴与左右轴；优先使用当前运动方向，静止时回退到相机平面方向。
    /// param name="planarVelocity": 当前世界平面速度
    /// param name="cameraTransform": 当前对齐使用的相机 Transform
    /// param name="forwardAxis": 输出的前后轴
    /// param name="sideAxis": 输出的左右轴
    /// returns: 无
    /// </summary>
    private void GetMotionAxes(Vector3 planarVelocity, Transform cameraTransform, out Vector3 forwardAxis, out Vector3 sideAxis)
    {
        forwardAxis = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        if (forwardAxis.sqrMagnitude <= MinimumMotionSqrMagnitude)
        {
            forwardAxis = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        }

        if (forwardAxis.sqrMagnitude <= MinimumMotionSqrMagnitude)
        {
            forwardAxis = Vector3.forward;
        }

        forwardAxis.Normalize();
        sideAxis = Vector3.Cross(Vector3.up, forwardAxis);
        if (sideAxis.sqrMagnitude <= MinimumMotionSqrMagnitude)
        {
            sideAxis = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up);
            if (sideAxis.sqrMagnitude <= MinimumMotionSqrMagnitude)
            {
                sideAxis = Vector3.right;
            }
        }

        sideAxis.Normalize();
    }

    /// <summary>
    /// summary: 生成一次短促的冲刺前后脉冲；先向前探出，再轻微回拉到锚点。
    /// param: 无
    /// returns: 当前帧的前后位移量
    /// </summary>
    private float EvaluateDashPulseOffset()
    {
        if (!Application.isPlaying ||
            dashPulseAmplitude <= 0f ||
            dashPulseDuration <= 0f ||
            dashPulseElapsed >= dashPulseDuration)
        {
            return 0f;
        }

        float normalizedTime = Mathf.Clamp01(dashPulseElapsed / dashPulseDuration);
        float envelope = 1f - normalizedTime;
        return Mathf.Sin(normalizedTime * Mathf.PI * 2f) * envelope * dashPulseAmplitude;
    }

    /// <summary>
    /// summary: 让来源位移差分从当前帧开始计，避免首次启用时把历史位移误算成高速晃动。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SyncMotionSourcePosition()
    {
        lastMotionSourcePosition = motionSource != null ? motionSource.position : transform.position;
    }

    /// <summary>
    /// summary: 把内部晃动状态清零；用于关闭动态偏移时立即恢复静止锚点。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ResetMotionState()
    {
        swayPhase = 0f;
        swayBlend = 0f;
        dashPulseElapsed = float.PositiveInfinity;
        wasDashingLastFrame = false;
        SyncMotionSourcePosition();
    }

    /// <summary>
    /// summary: 规范当前动态晃动参数，避免负值或无效速度阈值导致动画异常。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        movementSwayAmplitude = Mathf.Max(0f, movementSwayAmplitude);
        movementSwayFrequency = Mathf.Max(0f, movementSwayFrequency);
        movementSwayBlendSpeed = Mathf.Max(0f, movementSwayBlendSpeed);
        movementSpeedForMaxSway = Mathf.Max(0.01f, movementSpeedForMaxSway);
        dashPulseAmplitude = Mathf.Max(0f, dashPulseAmplitude);
        dashPulseDuration = Mathf.Max(0.01f, dashPulseDuration);
    }
}
