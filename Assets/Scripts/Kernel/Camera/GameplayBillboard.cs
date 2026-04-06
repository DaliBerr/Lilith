using UnityEngine;

/// <summary>
/// 让关键 gameplay 视觉层始终对齐主相机，保持透视镜头下的可读性。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool automaticFacingEnabled = true;

    public Camera TargetCamera => targetCamera;
    public bool AutomaticFacingEnabled => automaticFacingEnabled;

    private void OnEnable()
    {
        ApplyFacing();
    }

    private void LateUpdate()
    {
        ApplyFacing();
    }

    private void OnValidate()
    {
        ApplyFacing();
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
            return false;
        }

        transform.rotation = Quaternion.LookRotation(
            resolvedCamera.transform.forward,
            resolvedCamera.transform.up);
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
}
