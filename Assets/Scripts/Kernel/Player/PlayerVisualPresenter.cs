using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 负责把玩家主字与地面尖角阴影视觉对齐到 grounded 碰撞体底边。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerVisualPresenter : MonoBehaviour
{
    private const string TextPath = "Text";
    private const string GlyphPath = "Text/Glyph";
    private const string GroundShadowPath = "GroundShadow";
    private const float MinimumPlanarDirectionSqrMagnitude = 0.0001f;
    private const float GroundShadowPitch = 90f;
    private const float GroundShadowYawOffset = 180f;

    private static readonly Quaternion FlatRotation = Quaternion.Euler(GroundShadowPitch, GroundShadowYawOffset, 0f);

    [Header("Bindings")]
    [SerializeField] private RectTransform textContainer;
    [SerializeField] private TMP_Text glyphText;
    [SerializeField] private BoxCollider groundingCollider;
    [SerializeField] private SpriteRenderer groundShadowRenderer;
    [SerializeField] private Transform planarFacingSource;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float textHeightFactor = 0.35f;
    [SerializeField, Min(0f)] private float shadowHeightOffset = 0.05f;
    [SerializeField, Min(0f)] private float shadowScaleMultiplier = 1.2f;

    [Header("Style")]
    [SerializeField] private Color glyphColor = new(0.92f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color shadowTint = new(0f, 0f, 0f, 0.28f);
    [SerializeField] private int shadowSortingOrder = -20;

    public RectTransform TextContainer => textContainer;
    public TMP_Text GlyphText => glyphText;
    public BoxCollider GroundingCollider => groundingCollider;
    public SpriteRenderer GroundShadowRenderer => groundShadowRenderer;
    public Transform PlanarFacingSource => planarFacingSource;

    private void Awake()
    {
        TryCacheBindings();
        RefreshPresentation();
    }

    private void Reset()
    {
        TryCacheBindings(overwriteExisting: true);
        RefreshPresentation();
    }

    private void OnValidate()
    {
        TryCacheBindings();
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        KeepGroundShadowWorldRotation();
    }

    /// <summary>
    /// summary: 尝试缓存玩家层级中的主字容器、主字文本、根碰撞体和地影渲染层引用。
    /// param name="overwriteExisting": 为 true 时强制重新解析全部绑定
    /// returns: 成功拿到所有关键绑定时返回 true
    /// </summary>
    public bool TryCacheBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || !IsRectTransformReferenceValid(textContainer))
        {
            textContainer = FindRectTransform(TextPath);
        }

        if (overwriteExisting || !IsGlyphReferenceValid(glyphText))
        {
            glyphText = FindGlyph(GlyphPath);
        }

        if (overwriteExisting || groundingCollider == null || groundingCollider.transform != transform)
        {
            groundingCollider = GetComponent<BoxCollider>();
        }

        if (overwriteExisting || !IsSpriteRendererReferenceValid(groundShadowRenderer))
        {
            groundShadowRenderer = FindSpriteRenderer(GroundShadowPath);
        }

        if (overwriteExisting || !IsPlanarFacingReferenceValid(planarFacingSource))
        {
            planarFacingSource = FindPlanarFacingSource();
        }

        return IsRectTransformReferenceValid(textContainer) &&
               IsGlyphReferenceValid(glyphText) &&
               groundingCollider != null &&
               IsSpriteRendererReferenceValid(groundShadowRenderer);
    }

    /// <summary>
    /// summary: 按 grounded 根碰撞体的底边重新布局玩家主字与尖角地影。
    /// param: 无
    /// returns: 成功完成一次完整视觉刷新时返回 true
    /// </summary>
    public bool RefreshPresentation()
    {
        shadowHeightOffset = Mathf.Max(0f, shadowHeightOffset);
        shadowScaleMultiplier = Mathf.Max(0f, shadowScaleMultiplier);
        shadowTint.a = Mathf.Clamp01(shadowTint.a);
        glyphColor.a = Mathf.Clamp01(glyphColor.a);

        if (!TryCacheBindings())
        {
            return false;
        }

        float groundYLocal = GetGroundYLocal();
        float planarSize = Mathf.Max(groundingCollider.size.x, groundingCollider.size.z);

        ApplyTextLayout(groundYLocal);
        ApplyShadowLayout(groundYLocal, planarSize);
        KeepGroundShadowWorldRotation();
        return true;
    }

    /// <summary>
    /// summary: 计算玩家 grounded 根碰撞体底边在根节点局部空间中的 Y 值。
    /// param: 无
    /// returns: 玩家站地底边对应的局部 Y
    /// </summary>
    public float GetGroundYLocal()
    {
        if (groundingCollider == null)
        {
            return 0f;
        }

        Vector3 bottomCenterWorld = groundingCollider.bounds.center;
        bottomCenterWorld.y = groundingCollider.bounds.min.y;
        return transform.InverseTransformPoint(bottomCenterWorld).y;
    }

    /// <summary>
    /// summary: 把玩家主字容器重排到 grounded 根碰撞体上方，并保持字形局部姿态为 identity。
    /// param name="groundYLocal": grounded 底边对应的局部 Y
    /// returns: 无
    /// </summary>
    private void ApplyTextLayout(float groundYLocal)
    {
        Vector3 nextLocalPosition = textContainer.localPosition;
        nextLocalPosition.x = 0f;
        nextLocalPosition.y = groundYLocal + groundingCollider.size.y * textHeightFactor;
        nextLocalPosition.z = 0f;
        textContainer.localPosition = nextLocalPosition;
        textContainer.localRotation = Quaternion.identity;
        textContainer.localScale = Vector3.one;

        RectTransform glyphRectTransform = glyphText.rectTransform;
        glyphRectTransform.localPosition = Vector3.zero;
        glyphRectTransform.localRotation = Quaternion.identity;
        glyphRectTransform.localScale = Vector3.one;
        glyphText.color = glyphColor;
    }

    /// <summary>
    /// summary: 把玩家尖角地影平放到地面，并按 grounded 平面尺寸统一缩放与着色。
    /// param name="groundYLocal": grounded 底边对应的局部 Y
    /// param name="planarSize": grounded 平面尺寸基准
    /// returns: 无
    /// </summary>
    private void ApplyShadowLayout(float groundYLocal, float planarSize)
    {
        groundShadowRenderer.transform.localPosition = new Vector3(0f, groundYLocal + shadowHeightOffset, 0f);
        groundShadowRenderer.transform.localRotation = FlatRotation;
        groundShadowRenderer.transform.localScale = Vector3.one * planarSize * shadowScaleMultiplier;
        groundShadowRenderer.color = shadowTint;
        groundShadowRenderer.sortingOrder = shadowSortingOrder;
        groundShadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        groundShadowRenderer.receiveShadows = false;
        groundShadowRenderer.allowOcclusionWhenDynamic = true;
    }

    /// <summary>
    /// summary: 将玩家地影锁定为世界空间的平放姿态，同时只保留玩家当前平面朝向对应的 yaw。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void KeepGroundShadowWorldRotation()
    {
        if (!IsSpriteRendererReferenceValid(groundShadowRenderer))
        {
            return;
        }

        Transform facingSource = planarFacingSource != null ? planarFacingSource : transform;
        Vector3 planarForward = Vector3.ProjectOnPlane(facingSource.forward, Vector3.up);
        if (planarForward.sqrMagnitude <= MinimumPlanarDirectionSqrMagnitude)
        {
            groundShadowRenderer.transform.rotation = FlatRotation;
            return;
        }

        planarForward.Normalize();
        float yaw = Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg;
        groundShadowRenderer.transform.rotation = Quaternion.Euler(GroundShadowPitch, yaw + GroundShadowYawOffset, 0f);
    }

    private RectTransform FindRectTransform(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        return target != null ? target.GetComponent<RectTransform>() : null;
    }

    private TMP_Text FindGlyph(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private SpriteRenderer FindSpriteRenderer(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        return target != null ? target.GetComponent<SpriteRenderer>() : null;
    }

    /// <summary>
    /// summary: 解析玩家视觉当前应跟随的平面朝向源；优先使用 AimPivot，缺失时回退到玩家根节点。
    /// param: 无
    /// returns: 可用于读取平面朝向的 Transform
    /// </summary>
    private Transform FindPlanarFacingSource()
    {
        Transform aimPivot = transform.Find("AimPivot");
        return aimPivot != null ? aimPivot : transform;
    }

    private bool IsRectTransformReferenceValid(RectTransform target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }

    private bool IsGlyphReferenceValid(TMP_Text target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }

    private bool IsSpriteRendererReferenceValid(SpriteRenderer target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }

    private bool IsPlanarFacingReferenceValid(Transform target)
    {
        return target != null && (target == transform || target.IsChildOf(transform));
    }
}
