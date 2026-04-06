using UnityEngine;

/// <summary>
/// 固定使用斜俯视透视参数跟随玩家焦点，不继承玩家旋转。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PlayerFollowCamera : MonoBehaviour
{
    private const float MinimumDistance = 0.01f;
    private const float MinimumFieldOfView = 1f;
    private const float MaximumFieldOfView = 179f;
    private const float MinimumNearClipPlane = 0.01f;
    private const float MinimumClipPlaneGap = 0.1f;

    [SerializeField] private Transform targetPlayer;
    [SerializeField] private Vector3 focusOffset = new(0f, 8f, 0f);
    [SerializeField, Min(MinimumDistance)] private float distance = 260f;
    [SerializeField] private float pitch = 55f;
    [SerializeField] private float yaw = 35f;
    [SerializeField, Range(MinimumFieldOfView, MaximumFieldOfView)] private float fieldOfView = 35f;
    [SerializeField, Min(MinimumNearClipPlane)] private float nearClipPlane = 0.3f;
    [SerializeField, Min(MinimumNearClipPlane + MinimumClipPlaneGap)] private float farClipPlane = 4000f;
    [SerializeField] private bool snapOnEnable = true;

    private Camera cachedCamera;

    public Transform TargetPlayer => targetPlayer;
    public Vector3 FocusOffset => focusOffset;
    public Vector3 FocusWorldPoint => ResolveFocusWorldPoint();

    private void Awake()
    {
        EnsureCameraReference();
        ApplyCameraSettings();
    }

    private void OnEnable()
    {
        if (snapOnEnable)
        {
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        if (!TryGetCameraPose(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private void OnValidate()
    {
        distance = Mathf.Max(MinimumDistance, distance);
        fieldOfView = Mathf.Clamp(fieldOfView, MinimumFieldOfView, MaximumFieldOfView);
        nearClipPlane = Mathf.Max(MinimumNearClipPlane, nearClipPlane);
        farClipPlane = Mathf.Max(nearClipPlane + MinimumClipPlaneGap, farClipPlane);
        EnsureCameraReference();
        ApplyCameraSettings();
    }

    /// <summary>
    /// summary: 应用当前透视相机参数，避免沿用旧的正交投影设置。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ApplyCameraSettings()
    {
        if (!EnsureCameraReference())
        {
            return;
        }

        cachedCamera.orthographic = false;
        cachedCamera.fieldOfView = fieldOfView;
        cachedCamera.nearClipPlane = nearClipPlane;
        cachedCamera.farClipPlane = farClipPlane;
    }

    /// <summary>
    /// summary: 在启用时立即把相机移动到玩家焦点对应的透视跟随位，避免第一帧先看到错误位置。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SnapToTarget()
    {
        if (!TryGetCameraPose(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>
    /// summary: 计算当前相机应该采用的世界位置和旋转。
    /// param: position 输出的世界位置
    /// param: rotation 输出的世界旋转
    /// returns: 成功解析到玩家焦点时返回 true
    /// </summary>
    private bool TryGetCameraPose(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = default;

        if (!TryResolveTargetPlayer())
        {
            return false;
        }

        Vector3 focusPoint = ResolveFocusWorldPoint();
        rotation = Quaternion.Euler(pitch, yaw, 0f);
        position = focusPoint - (rotation * Vector3.forward * distance);
        return true;
    }

    /// <summary>
    /// summary: 获取当前镜头应该围绕的玩家焦点世界坐标。
    /// param: 无
    /// returns: 玩家根节点位置叠加焦点偏移后的世界坐标
    /// </summary>
    private Vector3 ResolveFocusWorldPoint()
    {
        return targetPlayer != null ? targetPlayer.position + focusOffset : transform.position + focusOffset;
    }

    /// <summary>
    /// summary: 尝试解析当前要跟随的玩家；未显式绑定时回退到场景中的 PlayerPlaneMovement。
    /// param: 无
    /// returns: 成功找到玩家时返回 true
    /// </summary>
    private bool TryResolveTargetPlayer()
    {
        if (targetPlayer != null)
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
    /// summary: 缓存当前脚本所在物体上的 Camera 组件，供后续统一更新投影参数。
    /// param: 无
    /// returns: 成功拿到 Camera 组件时返回 true
    /// </summary>
    private bool EnsureCameraReference()
    {
        if (cachedCamera != null)
        {
            return true;
        }

        cachedCamera = GetComponent<Camera>();
        return cachedCamera != null;
    }
}
