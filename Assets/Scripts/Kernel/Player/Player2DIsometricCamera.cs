using UnityEngine;

/// <summary>
/// Orthographic camera follow used by the generated 2D isometric room debug flow.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class Player2DIsometricCamera : MonoBehaviour
{
    private const float MinimumOrthographicSize = 0.01f;

    [SerializeField] private Transform target;
    [SerializeField, Min(MinimumOrthographicSize)] private float orthographicSize = 6f;
    [SerializeField] private float zOffset = -10f;
    [SerializeField] private bool snapOnEnable = true;

    private Camera cachedCamera;

    public Transform Target => target;

    private void Awake()
    {
        EnsureCameraReference();
        ApplyCameraSettings();
    }

    private void OnEnable()
    {
        EnsureCameraReference();
        ApplyCameraSettings();
        if (snapOnEnable)
        {
            SnapToTarget();
        }
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
        transform.position = position;
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(MinimumOrthographicSize, orthographicSize);
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

    private void ApplyCameraSettings()
    {
        if (!EnsureCameraReference())
        {
            return;
        }

        cachedCamera.orthographic = true;
        cachedCamera.orthographicSize = orthographicSize;
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
}
