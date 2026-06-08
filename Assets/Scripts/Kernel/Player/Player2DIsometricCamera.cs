using UnityEngine;

/// <summary>
/// Perspective camera follow used by the generated 2D isometric room debug flow.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class Player2DIsometricCamera : MonoBehaviour
{
    private const float MinimumFieldOfView = 1f;

    [SerializeField] private Transform target;
    [SerializeField, Min(MinimumFieldOfView)] private float orthographicSize = 43f;
    [SerializeField] private float zOffset = -10f;
    [SerializeField] private bool snapOnEnable = true;
    [SerializeField] private ScreenShakeState screenShake = new();

    private Camera cachedCamera;

    public Transform Target => target;
    public Vector3 CurrentScreenShakeOffset => EnsureScreenShakeState().CurrentOffset;

    private void Awake()
    {
        EnsureCameraReference();
        EnsureScreenShakeState();
        ApplyCameraSettings();
    }

    private void OnEnable()
    {
        EnsureCameraReference();
        EnsureScreenShakeState().Enable();
        ApplyCameraSettings();
        if (snapOnEnable)
        {
            SnapToTarget();
        }
    }

    private void OnDisable()
    {
        screenShake?.Disable();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x = target.position.x;
        position.y = target.position.y;
        position.z = target.position.z + zOffset;
        position += TickScreenShake(Quaternion.identity);
        transform.position = position;
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(MinimumFieldOfView, orthographicSize);
        EnsureScreenShakeState();
        EnsureCameraReference();
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = new Vector3(target.position.x, target.position.y, target.position.z + zOffset);
        transform.rotation = Quaternion.identity;
        ApplyCameraSettings();
    }

    public void RequestScreenShake(float amplitude, float duration, float frequency = 0f)
    {
        EnsureScreenShakeState().RequestShake(amplitude, duration, frequency);
    }

    private void ApplyCameraSettings()
    {
        if (!EnsureCameraReference())
        {
            return;
        }

        cachedCamera.orthographic = false;
        cachedCamera.fieldOfView = orthographicSize;
    }

    private bool EnsureCameraReference()
    {
        if (cachedCamera != null)
        {
            return true;
        }

        cachedCamera = GetComponent<Camera>();
        return cachedCamera != null;
    }

    private ScreenShakeState EnsureScreenShakeState()
    {
        screenShake ??= new ScreenShakeState();
        screenShake.Sanitize();
        return screenShake;
    }

    private Vector3 TickScreenShake(Quaternion cameraRotation)
    {
        return EnsureScreenShakeState().Tick(Time.unscaledDeltaTime, cameraRotation);
    }
}
