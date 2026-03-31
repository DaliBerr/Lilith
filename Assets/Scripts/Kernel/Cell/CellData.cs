using UnityEngine;

[DisallowMultipleComponent]
public sealed class CellData : MonoBehaviour
{
    [SerializeField] private int gridX;
    [SerializeField] private int gridY;
    [SerializeField] private Collider managedCollider;

    public int GridX => gridX;
    public int GridY => gridY;
    public Vector2Int Coordinates => new(gridX, gridY);
    public Collider ManagedCollider
    {
        get
        {
            CacheManagedColliderReference();
            return managedCollider;
        }
    }

    public bool HasManagedCollider => ManagedCollider != null;
    public bool IsColliderEnabled => ManagedCollider != null && ManagedCollider.enabled;

    public enum CellLayer
    {
        Background,
        Interactive,
    }

    /// <summary>
    /// 尝试缓存当前 cell 受控的主 Collider。
    /// </summary>
    /// <param name="overwriteExisting">当现有引用无效时，是否重新查找。</param>
    /// <returns>找到并缓存 Collider 时返回 true。</returns>
    public bool TryCacheManagedCollider(bool overwriteExisting = false)
    {
        if (!overwriteExisting && IsManagedColliderReferenceValid())
        {
            return true;
        }

        managedCollider = FindPreferredManagedCollider();
        return managedCollider != null;
    }

    /// <summary>
    /// 设置当前 cell 主 Collider 的启用状态。
    /// </summary>
    /// <param name="enabled">目标启用状态。</param>
    /// <returns>成功找到受控 Collider 并完成设置时返回 true。</returns>
    public bool SetColliderEnabled(bool enabled)
    {
        if (!TryCacheManagedCollider())
        {
            return false;
        }

        managedCollider.enabled = enabled;
        return true;
    }

    /// <summary>
    /// 设置当前 cell 的网格坐标。
    /// </summary>
    /// <param name="x">网格 X 坐标。</param>
    /// <param name="y">网格 Y 坐标。</param>
    /// <returns>无。</returns>
    public void SetCoordinates(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    /// <summary>
    /// 设置当前 cell 的网格坐标。
    /// </summary>
    /// <param name="coordinates">目标网格坐标。</param>
    /// <returns>无。</returns>
    public void SetCoordinates(Vector2Int coordinates)
    {
        SetCoordinates(coordinates.x, coordinates.y);
    }

    /// <summary>
    /// 读取当前 cell 的网格坐标。
    /// </summary>
    /// <returns>当前 cell 的网格坐标。</returns>
    public Vector2Int GetCoordinates()
    {
        return new Vector2Int(gridX, gridY);
    }

    private void Reset()
    {
        TryCacheManagedCollider(overwriteExisting: true);
    }

    private void OnValidate()
    {
        TryCacheManagedCollider(overwriteExisting: true);
    }

    private void CacheManagedColliderReference()
    {
        if (IsManagedColliderReferenceValid())
        {
            return;
        }

        TryCacheManagedCollider(overwriteExisting: true);
    }

    private bool IsManagedColliderReferenceValid()
    {
        return managedCollider != null && managedCollider.transform.IsChildOf(transform);
    }

    private Collider FindPreferredManagedCollider()
    {
        var namedChild = transform.Find("Collider");
        if (namedChild != null && namedChild.TryGetComponent<Collider>(out var namedCollider))
        {
            return namedCollider;
        }

        var colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        for (var i = 0; i < colliders.Length; i++)
        {
            var candidate = colliders[i];
            if (candidate == null || candidate.transform == transform)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }
}
