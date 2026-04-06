using System;
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

    private const string WallModelPath = "Model/wall Model";
    private const string GroundModelPath = "Model/Ground Model";

    public enum CellSurfaceType
    {
        Ground,
        Wall,
    }

    [SerializeField] private int gridX;
    [SerializeField] private int gridY;
    [SerializeField] private CellSurfaceType surfaceType = CellSurfaceType.Ground;
    [SerializeField] private Collider wallCollider;
    [SerializeField] private Collider groundCollider;
    [SerializeField] private Transform wallModelRoot;
    [SerializeField] private Transform groundModelRoot;
    [SerializeField] private Transform movementTarget;
    [SerializeField] private Rigidbody movementRigidbody;

    public int GridX => gridX;
    public int GridY => gridY;
    public Vector2Int Coordinates => new(gridX, gridY);
    public CellSurfaceType SurfaceType => surfaceType;

    public Collider WallCollider
    {
        get
        {
            CacheSurfaceBindings();
            return wallCollider;
        }
    }

    public Collider GroundCollider
    {
        get
        {
            CacheSurfaceBindings();
            return groundCollider;
        }
    }

    public Transform WallModelRoot
    {
        get
        {
            CacheSurfaceBindings();
            return wallModelRoot;
        }
    }

    public Transform GroundModelRoot
    {
        get
        {
            CacheSurfaceBindings();
            return groundModelRoot;
        }
    }

    public Collider ManagedCollider
    {
        get
        {
            CacheSurfaceBindings();
            return ResolveManagedColliderForSurface(surfaceType);
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
    /// summary: 尝试缓存当前 cell 的墙体/地面 Collider 与模型节点引用。
    /// param: overwriteExisting 为 true 时即使已有引用也会重新解析
    /// returns: 至少解析出当前表面状态对应的 Collider 时返回 true
    /// </summary>
    public bool TryCacheSurfaceBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || !IsColliderReferenceValid(wallCollider) || !IsColliderReferenceValid(groundCollider))
        {
            ResolvePreferredSurfaceColliders(out Collider resolvedWallCollider, out Collider resolvedGroundCollider);
            if (overwriteExisting || !IsColliderReferenceValid(wallCollider))
            {
                wallCollider = resolvedWallCollider;
            }

            if (overwriteExisting || !IsColliderReferenceValid(groundCollider))
            {
                groundCollider = resolvedGroundCollider;
            }
        }

        if (overwriteExisting || !IsModelReferenceValid(wallModelRoot))
        {
            wallModelRoot = FindPreferredModelRoot(WallModelPath);
        }

        if (overwriteExisting || !IsModelReferenceValid(groundModelRoot))
        {
            groundModelRoot = FindPreferredModelRoot(GroundModelPath);
        }

        return ResolveManagedColliderForSurface(surfaceType) != null;
    }

    /// <summary>
    /// summary: 尝试缓存当前表面状态对应的受控 Collider。
    /// param: overwriteExisting 为 true 时强制重新解析墙体/地面绑定
    /// returns: 当前表面状态存在可用 Collider 时返回 true
    /// </summary>
    public bool TryCacheManagedCollider(bool overwriteExisting = false)
    {
        return TryCacheSurfaceBindings(overwriteExisting) && ManagedCollider != null;
    }

    /// <summary>
    /// summary: 按当前 SurfaceType 同步墙体/地面模型、Collider 和 tag 表现。
    /// param: syncTags 为 false 时只同步模型和 Collider，不改 tag
    /// returns: 成功应用当前表面表现时返回 true
    /// </summary>
    public bool TryRefreshSurfacePresentation(bool syncTags = true)
    {
        if (!TryCacheSurfaceBindings())
        {
            return false;
        }

        bool isWall = surfaceType == CellSurfaceType.Wall;
        Collider activeCollider = ResolveManagedColliderForSurface(surfaceType);
        if (activeCollider == null)
        {
            return false;
        }

        SetGameObjectActive(wallModelRoot, isWall);
        SetGameObjectActive(groundModelRoot, !isWall);

        SetColliderEnabledState(wallCollider, isWall);
        SetColliderEnabledState(groundCollider, !isWall);

        if (!syncTags)
        {
            return true;
        }

        string targetTag = isWall ? Kernel.MapGrid.MapGridAuthoring.WallTagName : Kernel.MapGrid.MapGridAuthoring.GroundTagName;
        return TryApplyTag(gameObject, targetTag) &&
               TryApplyTag(activeCollider.gameObject, targetTag);
    }

    /// <summary>
    /// summary: 切换当前 cell 的墙地表面状态，并立即刷新模型、Collider 与 tag。
    /// param: nextSurfaceType 目标表面状态
    /// returns: 成功切换并完成表现同步时返回 true
    /// </summary>
    public bool TrySetSurfaceType(CellSurfaceType nextSurfaceType)
    {
        CellSurfaceType previousSurfaceType = surfaceType;
        surfaceType = nextSurfaceType;
        if (TryRefreshSurfacePresentation())
        {
            return true;
        }

        surfaceType = previousSurfaceType;
        TryRefreshSurfacePresentation();
        return false;
    }

    /// <summary>
    /// summary: 检查当前 SurfaceType 对应的模型、Collider 与 tag 是否已经同步完成。
    /// param: 无
    /// returns: 当前表现和 SurfaceType 完全一致时返回 true
    /// </summary>
    public bool IsSurfacePresentationCurrent()
    {
        if (!TryCacheSurfaceBindings())
        {
            return false;
        }

        Collider activeCollider = ResolveManagedColliderForSurface(surfaceType);
        if (activeCollider == null)
        {
            return false;
        }

        bool isWall = surfaceType == CellSurfaceType.Wall;
        string targetTag = isWall ? Kernel.MapGrid.MapGridAuthoring.WallTagName : Kernel.MapGrid.MapGridAuthoring.GroundTagName;

        return HasExpectedActiveState(wallModelRoot, isWall) &&
               HasExpectedActiveState(groundModelRoot, !isWall) &&
               HasExpectedColliderState(wallCollider, isWall) &&
               HasExpectedColliderState(groundCollider, !isWall) &&
               HasExpectedTag(gameObject, targetTag) &&
               HasExpectedTag(activeCollider.gameObject, targetTag);
    }

    /// <summary>
    /// summary: 设置当前激活表面对应 Collider 的启用状态。
    /// param: enabled 目标启用状态
    /// returns: 成功找到当前激活 Collider 并完成修改时返回 true
    /// </summary>
    public bool SetColliderEnabled(bool enabled)
    {
        if (!TryCacheManagedCollider())
        {
            return false;
        }

        ManagedCollider.enabled = enabled;
        return true;
    }

    /// <summary>
    /// summary: 绑定当前 cell 的移动目标和受控 Rigidbody。
    /// param: target 需要被移动的目标 Transform；传 null 时恢复为 cell 根节点
    /// param: rigidbody 可选的受控 Rigidbody；通常由 Inspector 或运行时显式指定
    /// returns: 绑定成功时返回 true
    /// </summary>
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
    /// summary: 尝试缓存当前 cell 的移动目标与受控 Rigidbody。
    /// param: overwriteExisting 为 true 时重新解析当前引用
    /// returns: 成功解析到有效移动目标时返回 true
    /// </summary>
    public bool TryCacheMovementReferences(bool overwriteExisting = false)
    {
        bool hasValidTarget = IsMovementTargetReferenceValid();
        if (overwriteExisting || !hasValidTarget)
        {
            movementTarget = FindPreferredMovementTarget();
            hasValidTarget = IsMovementTargetReferenceValid();
        }

        if (overwriteExisting || !IsMovementRigidbodyReferenceValid())
        {
            movementRigidbody = FindPreferredMovementRigidbody();
        }

        return hasValidTarget && (movementRigidbody == null || IsMovementRigidbodyReferenceValid());
    }

    /// <summary>
    /// summary: 设置当前 cell 移动目标的世界坐标。
    /// param: worldPosition 目标世界坐标
    /// returns: 成功解析移动目标并完成设置时返回 true
    /// </summary>
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
    /// summary: 设置当前 cell 移动目标的局部坐标。
    /// param: localPosition 目标局部坐标
    /// returns: 成功解析移动目标并完成设置时返回 true
    /// </summary>
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
    /// summary: 按指定空间平移当前 cell 的移动目标。
    /// param: translation 本次平移向量
    /// param: relativeTo 平移使用的参考空间
    /// returns: 成功解析移动目标并完成平移时返回 true
    /// </summary>
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
    /// summary: 设置当前 cell 受控 Rigidbody 的线速度。
    /// param: velocity 目标线速度
    /// returns: 成功解析受控 Rigidbody 并完成设置时返回 true
    /// </summary>
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
    /// summary: 停止当前 cell 受控 Rigidbody 的线速度与角速度。
    /// param: includeAngularVelocity 是否一并清零角速度
    /// returns: 成功解析受控 Rigidbody 并完成停止时返回 true
    /// </summary>
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
    /// summary: 设置当前 cell 的网格坐标。
    /// param: x 网格 X 坐标
    /// param: y 网格 Y 坐标
    /// returns: 无
    /// </summary>
    public void SetCoordinates(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    /// <summary>
    /// summary: 设置当前 cell 的网格坐标。
    /// param: coordinates 目标网格坐标
    /// returns: 无
    /// </summary>
    public void SetCoordinates(Vector2Int coordinates)
    {
        SetCoordinates(coordinates.x, coordinates.y);
    }

    /// <summary>
    /// summary: 读取当前 cell 的网格坐标。
    /// param: 无
    /// returns: 当前 cell 的网格坐标
    /// </summary>
    public Vector2Int GetCoordinates()
    {
        return new Vector2Int(gridX, gridY);
    }

    private void Reset()
    {
        surfaceType = CellSurfaceType.Ground;
        TryCacheSurfaceBindings(overwriteExisting: true);
        TryCacheMovementReferences(overwriteExisting: true);
        SanitizeStaticMapRigidbody();
        TryRefreshSurfacePresentation(syncTags: false);
    }

    private void OnValidate()
    {
        TryCacheSurfaceBindings(overwriteExisting: true);
        TryCacheMovementReferences(overwriteExisting: true);
        SanitizeStaticMapRigidbody();
        TryRefreshSurfacePresentation(syncTags: false);
    }

    private void CacheSurfaceBindings()
    {
        if (IsColliderReferenceValid(wallCollider) &&
            IsColliderReferenceValid(groundCollider) &&
            IsModelReferenceValid(wallModelRoot) &&
            IsModelReferenceValid(groundModelRoot))
        {
            return;
        }

        TryCacheSurfaceBindings(overwriteExisting: true);
    }

    private void CacheMovementReferences()
    {
        bool hasValidTarget = IsMovementTargetReferenceValid();
        bool hasValidRigidbody = IsMovementRigidbodyReferenceValid();
        if (hasValidTarget && (movementRigidbody == null || hasValidRigidbody))
        {
            return;
        }

        TryCacheMovementReferences(overwriteExisting: true);
    }

    private void SanitizeStaticMapRigidbody()
    {
        Rigidbody rootRigidbody = GetComponent<Rigidbody>();
        if (rootRigidbody == null)
        {
            return;
        }

        rootRigidbody.useGravity = false;
        rootRigidbody.isKinematic = true;
    }

    private Collider ResolveManagedColliderForSurface(CellSurfaceType targetSurfaceType)
    {
        return targetSurfaceType == CellSurfaceType.Wall ? wallCollider : groundCollider;
    }

    private static bool TryApplyTag(GameObject target, string tagName)
    {
        if (target == null)
        {
            return false;
        }

        if (string.Equals(target.tag, tagName, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            target.tag = tagName;
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static bool HasExpectedTag(GameObject target, string expectedTag)
    {
        return target != null && string.Equals(target.tag, expectedTag, StringComparison.Ordinal);
    }

    private static void SetGameObjectActive(Transform target, bool active)
    {
        if (target == null || target.gameObject.activeSelf == active)
        {
            return;
        }

        target.gameObject.SetActive(active);
    }

    private static bool HasExpectedActiveState(Transform target, bool active)
    {
        return target == null || target.gameObject.activeSelf == active;
    }

    private static void SetColliderEnabledState(Collider target, bool enabled)
    {
        if (target == null || target.enabled == enabled)
        {
            return;
        }

        target.enabled = enabled;
    }

    private static bool HasExpectedColliderState(Collider target, bool enabled)
    {
        return target == null || target.enabled == enabled;
    }

    private bool IsColliderReferenceValid(Collider candidate)
    {
        return candidate != null &&
               candidate.transform != null &&
               IsTransformInsideCell(candidate.transform);
    }

    private bool IsModelReferenceValid(Transform candidate)
    {
        return candidate != null && IsTransformInsideCell(candidate);
    }

    private bool IsMovementTargetReferenceValid()
    {
        return movementTarget != null && IsTransformInsideCell(movementTarget);
    }

    private bool IsMovementRigidbodyReferenceValid()
    {
        return movementRigidbody != null &&
               movementRigidbody.transform != null &&
               movementRigidbody.transform != transform &&
               IsTransformInsideCell(movementRigidbody.transform) &&
               (!IsMovementTargetReferenceValid() ||
                movementRigidbody.transform == movementTarget ||
                movementRigidbody.transform.IsChildOf(movementTarget));
    }

    private void ResolvePreferredSurfaceColliders(out Collider resolvedWallCollider, out Collider resolvedGroundCollider)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        Collider tallestCollider = null;
        Collider shortestCollider = null;
        float tallestHeight = float.MinValue;
        float shortestHeight = float.MaxValue;
        int validColliderCount = 0;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null || !IsTransformInsideCell(candidate.transform))
            {
                continue;
            }

            validColliderCount++;
            float candidateHeight = GetColliderHeight(candidate);
            if (tallestCollider == null || candidateHeight > tallestHeight)
            {
                tallestCollider = candidate;
                tallestHeight = candidateHeight;
            }

            if (shortestCollider == null || candidateHeight < shortestHeight)
            {
                shortestCollider = candidate;
                shortestHeight = candidateHeight;
            }
        }

        if (validColliderCount <= 0)
        {
            resolvedWallCollider = null;
            resolvedGroundCollider = null;
            return;
        }

        if (validColliderCount == 1 || ReferenceEquals(tallestCollider, shortestCollider))
        {
            if (IsGroundLikeCollider(shortestCollider))
            {
                resolvedWallCollider = null;
                resolvedGroundCollider = shortestCollider;
                return;
            }

            resolvedWallCollider = tallestCollider;
            resolvedGroundCollider = null;
            return;
        }

        resolvedWallCollider = tallestCollider;
        resolvedGroundCollider = shortestCollider;
    }

    private static float GetColliderHeight(Collider collider)
    {
        return collider switch
        {
            BoxCollider boxCollider => Mathf.Abs(boxCollider.size.y * boxCollider.transform.lossyScale.y),
            CapsuleCollider capsuleCollider => Mathf.Abs(capsuleCollider.height * capsuleCollider.transform.lossyScale.y),
            SphereCollider sphereCollider => Mathf.Abs(sphereCollider.radius * 2f * sphereCollider.transform.lossyScale.y),
            _ => collider.bounds.size.y,
        };
    }

    private static bool IsGroundLikeCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Vector2 footprint = GetColliderFootprint(collider);
        float smallestHorizontalExtent = Mathf.Min(footprint.x, footprint.y);
        if (smallestHorizontalExtent <= 0f)
        {
            return false;
        }

        return GetColliderHeight(collider) <= (smallestHorizontalExtent * 0.25f);
    }

    private static Vector2 GetColliderFootprint(Collider collider)
    {
        return collider switch
        {
            BoxCollider boxCollider => new Vector2(
                Mathf.Abs(boxCollider.size.x * boxCollider.transform.lossyScale.x),
                Mathf.Abs(boxCollider.size.z * boxCollider.transform.lossyScale.z)),
            CapsuleCollider capsuleCollider => new Vector2(
                Mathf.Abs(capsuleCollider.radius * 2f * capsuleCollider.transform.lossyScale.x),
                Mathf.Abs(capsuleCollider.radius * 2f * capsuleCollider.transform.lossyScale.z)),
            SphereCollider sphereCollider => new Vector2(
                Mathf.Abs(sphereCollider.radius * 2f * sphereCollider.transform.lossyScale.x),
                Mathf.Abs(sphereCollider.radius * 2f * sphereCollider.transform.lossyScale.z)),
            _ => new Vector2(collider.bounds.size.x, collider.bounds.size.z),
        };
    }

    private Transform FindPreferredModelRoot(string relativePath)
    {
        Transform namedTransform = transform.Find(relativePath);
        if (namedTransform != null)
        {
            return namedTransform;
        }

        return null;
    }

    private Transform FindPreferredMovementTarget()
    {
        for (int i = 0; i < PreferredMovementChildNames.Length; i++)
        {
            Transform namedChild = transform.Find(PreferredMovementChildNames[i]);
            if (namedChild != null)
            {
                return namedChild;
            }
        }

        return transform;
    }

    private Rigidbody FindPreferredMovementRigidbody()
    {
        if (!IsMovementTargetReferenceValid() || movementTarget == transform)
        {
            return null;
        }

        return movementTarget.GetComponentInChildren<Rigidbody>(includeInactive: true);
    }

    private bool IsTransformInsideCell(Transform candidate)
    {
        return candidate != null && (candidate == transform || candidate.IsChildOf(transform));
    }
}
