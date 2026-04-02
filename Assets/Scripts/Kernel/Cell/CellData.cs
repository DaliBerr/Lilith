using UnityEngine;

[DisallowMultipleComponent]
public sealed class CellData : MonoBehaviour
{
    private static readonly string[] PreferredMovementChildNames =
    {
        "Movement",
        "MoveRoot",
        "Mover",
    };

    [SerializeField] private int gridX;
    [SerializeField] private int gridY;
    [SerializeField] private Collider managedCollider;
    [SerializeField] private Transform movementTarget;
    [SerializeField] private Rigidbody movementRigidbody;

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

    public Transform MovementTarget
    {
        get
        {
            CacheMovementReferences();
            return movementTarget != null ? movementTarget : transform;
        }
    }

    public Rigidbody MovementRigidbody
    {
        get
        {
            
            CacheMovementReferences();
            return movementRigidbody;
        }
    }

    public bool HasManagedCollider => ManagedCollider != null;
    public bool IsColliderEnabled => ManagedCollider != null && ManagedCollider.enabled;
    public bool HasMovementRigidbody => MovementRigidbody != null;

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
    /// 绑定当前 cell 的移动目标和受控 Rigidbody。
    /// </summary>
    /// <param name="target">需要被移动的目标 Transform；传 null 时恢复为 cell 根节点。</param>
    /// <param name="rigidbody">可选的受控 Rigidbody；通常由 Inspector 手动指定。</param>
    /// <returns>绑定成功时返回 true。</returns>
    public bool TryBindMovementTarget(Transform target, Rigidbody rigidbody = null)
    {
        if (target != null && !IsTransformInsideCell(target))
        {
            return false;
        }

        if (rigidbody != null)
        {
            if (target == null || (rigidbody.transform != target && !rigidbody.transform.IsChildOf(target)))
            {
                return false;
            }
        }

        movementTarget = target;
        movementRigidbody = rigidbody;
        return true;
    }

    /// <summary>
    /// 尝试缓存当前 cell 的移动目标与受控 Rigidbody。
    /// </summary>
    /// <param name="overwriteExisting">当引用无效时，是否重新校验并清理引用。</param>
    /// <returns>成功解析到有效移动目标时返回 true。</returns>
    public bool TryCacheMovementReferences(bool overwriteExisting = false)
    {
        var hasValidTarget = IsMovementTargetReferenceValid();
        var hasValidRigidbody = IsMovementRigidbodyReferenceValid();
        if (!overwriteExisting && hasValidTarget && hasValidRigidbody)
        {
            return true;
        }

        if (!hasValidTarget)
        {
            movementTarget = FindPreferredMovementTarget();
            hasValidTarget = IsMovementTargetReferenceValid();
        }

        if (overwriteExisting && movementRigidbody != null && !IsMovementRigidbodyReferenceValid())
        {
            movementRigidbody = null;
        }

        return hasValidTarget && (movementRigidbody == null || IsMovementRigidbodyReferenceValid());
    }

    /// <summary>
    /// 设置当前 cell 移动目标的世界坐标。
    /// </summary>
    /// <param name="worldPosition">目标世界坐标。</param>
    /// <returns>成功解析移动目标并完成设置时返回 true。</returns>
    public bool TrySetWorldPosition(Vector3 worldPosition)
    {
        if (!TryCacheMovementReferences())
        {
            return false;
        }

        MovementTarget.position = worldPosition;
        return true;
    }

    /// <summary>
    /// 设置当前 cell 移动目标的局部坐标。
    /// </summary>
    /// <param name="localPosition">目标局部坐标。</param>
    /// <returns>成功解析移动目标并完成设置时返回 true。</returns>
    public bool TrySetLocalPosition(Vector3 localPosition)
    {
        if (!TryCacheMovementReferences())
        {
            return false;
        }

        MovementTarget.localPosition = localPosition;
        return true;
    }

    /// <summary>
    /// 按指定空间平移当前 cell 的移动目标。
    /// </summary>
    /// <param name="translation">本次平移向量。</param>
    /// <param name="relativeTo">平移使用的参考空间。</param>
    /// <returns>成功解析移动目标并完成平移时返回 true。</returns>
    public bool TryTranslate(Vector3 translation, Space relativeTo = Space.World)
    {
        if (!TryCacheMovementReferences())
        {
            return false;
        }

        MovementTarget.Translate(translation, relativeTo);
        return true;
    }

    /// <summary>
    /// 设置当前 cell 受控 Rigidbody 的线速度。
    /// </summary>
    /// <param name="velocity">目标线速度。</param>
    /// <returns>成功解析受控 Rigidbody 并完成设置时返回 true。</returns>
    public bool TrySetLinearVelocity(Vector3 velocity)
    {
        if (!TryCacheMovementReferences() || movementRigidbody == null)
        {
            return false;
        }

        movementRigidbody.linearVelocity = velocity;
        return true;
    }

    /// <summary>
    /// 停止当前 cell 受控 Rigidbody 的线速度与角速度。
    /// </summary>
    /// <param name="includeAngularVelocity">是否一并清零角速度。</param>
    /// <returns>成功解析受控 Rigidbody 并完成停止时返回 true。</returns>
    public bool TryStopMovement(bool includeAngularVelocity = true)
    {
        if (!TryCacheMovementReferences() || movementRigidbody == null)
        {
            return false;
        }

        movementRigidbody.linearVelocity = Vector3.zero;
        if (includeAngularVelocity)
        {
            movementRigidbody.angularVelocity = Vector3.zero;
        }

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
        TryCacheMovementReferences(overwriteExisting: true);
    }

    private void OnValidate()
    {
        TryCacheManagedCollider(overwriteExisting: true);
        TryCacheMovementReferences(overwriteExisting: true);
    }

    private void CacheManagedColliderReference()
    {
        if (IsManagedColliderReferenceValid())
        {
            return;
        }

        TryCacheManagedCollider(overwriteExisting: true);
    }

    private void CacheMovementReferences()
    {
        var hasValidTarget = IsMovementTargetReferenceValid();
        var hasValidRigidbody = IsMovementRigidbodyReferenceValid();
        if (hasValidTarget && (movementRigidbody == null || hasValidRigidbody))
        {
            return;
        }

        TryCacheMovementReferences(overwriteExisting: true);
    }

    private bool IsManagedColliderReferenceValid()
    {
        return managedCollider != null && managedCollider.transform.IsChildOf(transform);
    }

    private bool IsMovementTargetReferenceValid()
    {
        return movementTarget != null && IsTransformInsideCell(movementTarget);
    }

    private bool IsMovementRigidbodyReferenceValid()
    {
        return movementRigidbody != null &&
               movementRigidbody.transform != null &&
               IsTransformInsideCell(movementRigidbody.transform) &&
               (!IsMovementTargetReferenceValid() ||
                movementRigidbody.transform == movementTarget ||
                movementRigidbody.transform.IsChildOf(movementTarget));
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

    private Transform FindPreferredMovementTarget()
    {
        for (var i = 0; i < PreferredMovementChildNames.Length; i++)
        {
            var namedChild = transform.Find(PreferredMovementChildNames[i]);
            if (namedChild != null)
            {
                return namedChild;
            }
        }

        return transform;
    }

    private bool IsTransformInsideCell(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
