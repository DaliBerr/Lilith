using UnityEngine;

/// <summary>
/// 让主相机只跟随玩家位置，保留相机自身旋转，避免玩家转向时带动镜头一起旋转。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PlayerFollowCamera : MonoBehaviour
{
    private const float MinimumOrthographicSize = 0.01f;

    [SerializeField] private Transform targetPlayer;
    [SerializeField] private Vector3 positionOffset = new(0f, 10f, 0f);
    [SerializeField, Min(MinimumOrthographicSize)] private float orthographicSize = 180f;
    [SerializeField] private bool snapOnEnable = true;

    private Camera cachedCamera;

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
        if (!TryResolveTargetPlayer())
        {
            return;
        }

        transform.position = targetPlayer.position + positionOffset;
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(MinimumOrthographicSize, orthographicSize);
        EnsureCameraReference();
        ApplyCameraSettings();
    }

    /// <summary>
    /// 根据当前正交相机配置修正镜头投影，避免运行时仍沿用整张地图 framing 的旧尺寸。
    /// </summary>
    /// <returns>无。</returns>
    private void ApplyCameraSettings()
    {
        if (!EnsureCameraReference())
        {
            return;
        }

        cachedCamera.orthographic = true;
        cachedCamera.orthographicSize = orthographicSize;
    }

    /// <summary>
    /// 在启用时立即把相机移动到玩家跟随位，避免第一帧先看到错误位置。
    /// </summary>
    /// <returns>无。</returns>
    private void SnapToTarget()
    {
        if (!TryResolveTargetPlayer())
        {
            return;
        }

        transform.position = targetPlayer.position + positionOffset;
    }

    /// <summary>
    /// 尝试解析当前要跟随的玩家；未显式绑定时回退到场景中的 PlayerPlaneMovement。
    /// </summary>
    /// <returns>成功找到玩家时返回 true。</returns>
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
    /// 缓存当前脚本所在物体上的 Camera 组件，供后续统一更新投影参数。
    /// </summary>
    /// <returns>成功拿到 Camera 组件时返回 true。</returns>
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
