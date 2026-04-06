using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 负责把文字敌人的主字、底座和地面阴影视觉对齐到 grounded 碰撞体底边。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharEnemyVisualPresenter : MonoBehaviour
{
    private const string TextPath = "Text";
    private const string GlyphPath = "Text/Glyph";
    private const string ColliderPath = "Collider";
    private const string RuneBaseCorePath = "RuneBaseCore";
    private const string GroundShadowPath = "GroundShadow";

    private static readonly Quaternion FlatRotation = Quaternion.Euler(90f, 0f, 0f);

    [Header("Bindings")]
    [SerializeField] private RectTransform textContainer;
    [SerializeField] private TMP_Text glyphText;
    [SerializeField] private BoxCollider groundingCollider;
    [SerializeField] private SpriteRenderer runeBaseRenderer;
    [SerializeField] private SpriteRenderer groundShadowRenderer;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float textHeightFactor = 0.35f;
    [SerializeField, Min(0f)] private float baseHeightOffset = 0.15f;
    [SerializeField, Min(0f)] private float shadowHeightOffset = 0.05f;
    [SerializeField, Min(0f)] private float baseScaleMultiplier = 1f;
    [SerializeField, Min(0f)] private float shadowScaleMultiplier = 1.15f;

    [Header("Style")]
    [SerializeField] private Color baseTint = new(0.92f, 0.94f, 0.98f, 0.45f);
    [SerializeField] private Color shadowTint = new(0f, 0f, 0f, 0.28f);
    [SerializeField] private int baseSortingOrder = -10;
    [SerializeField] private int shadowSortingOrder = -20;

    public RectTransform TextContainer => textContainer;
    public TMP_Text GlyphText => glyphText;
    public BoxCollider GroundingCollider => groundingCollider;
    public SpriteRenderer RuneBaseRenderer => runeBaseRenderer;
    public SpriteRenderer GroundShadowRenderer => groundShadowRenderer;

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

    /// <summary>
    /// summary: 尝试缓存敌人 prefab 中的主字容器、地面碰撞体和地面视觉层引用。
    /// param: overwriteExisting 为 true 时强制重新解析所有引用
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

        if (overwriteExisting || !IsGroundingColliderReferenceValid(groundingCollider))
        {
            groundingCollider = FindGroundingCollider(ColliderPath);
        }

        if (overwriteExisting || !IsSpriteRendererReferenceValid(runeBaseRenderer))
        {
            runeBaseRenderer = FindSpriteRenderer(RuneBaseCorePath);
        }

        if (overwriteExisting || !IsSpriteRendererReferenceValid(groundShadowRenderer))
        {
            groundShadowRenderer = FindSpriteRenderer(GroundShadowPath);
        }

        return IsRectTransformReferenceValid(textContainer) &&
               IsGlyphReferenceValid(glyphText) &&
               IsGroundingColliderReferenceValid(groundingCollider) &&
               IsSpriteRendererReferenceValid(runeBaseRenderer) &&
               IsSpriteRendererReferenceValid(groundShadowRenderer);
    }

    /// <summary>
    /// summary: 按 grounded 参考 BoxCollider 的底边重新布局主字、底座和平放阴影。
    /// param: 无
    /// returns: 成功完成一次完整的视觉合同刷新时返回 true
    /// </summary>
    public bool RefreshPresentation()
    {
        if (!TryCacheBindings())
        {
            return false;
        }

        float groundYLocal = GetGroundYLocal();
        float planarSize = Mathf.Max(groundingCollider.size.x, groundingCollider.size.z);

        ApplyTextLayout(groundYLocal);
        ApplyFlatLayer(runeBaseRenderer, groundYLocal + baseHeightOffset, planarSize * baseScaleMultiplier, baseTint, baseSortingOrder);
        ApplyFlatLayer(groundShadowRenderer, groundYLocal + shadowHeightOffset, planarSize * shadowScaleMultiplier, shadowTint, shadowSortingOrder);
        return true;
    }

    /// <summary>
    /// summary: 计算 grounded 参考 BoxCollider 底边在敌人根节点局部空间中的 Y 值。
    /// param: 无
    /// returns: 敌人站地底边对应的局部 Y
    /// </summary>
    public float GetGroundYLocal()
    {
        if (!IsGroundingColliderReferenceValid(groundingCollider))
        {
            return 0f;
        }

        Vector3 bottomCenterWorld = groundingCollider.bounds.center;
        bottomCenterWorld.y = groundingCollider.bounds.min.y;
        return transform.InverseTransformPoint(bottomCenterWorld).y;
    }

    /// <summary>
    /// summary: 把敌人定义里的视觉样式写入当前主字、底座和地影渲染层。
    /// param: visual 当前实例应使用的视觉定义
    /// returns: 成功完成视觉样式与 grounded 布局同步时返回 true
    /// </summary>
    public bool ApplyVisualDefinition(EnemyDefinition.EnemyVisualDefinition visual)
    {
        if (!TryCacheBindings())
        {
            return false;
        }

        glyphText.color = visual.glyphColor;
        runeBaseRenderer.sprite = visual.runeBaseSprite;
        groundShadowRenderer.sprite = visual.groundShadowSprite;
        baseTint = visual.runeBaseTint;
        shadowTint = visual.groundShadowTint;
        return RefreshPresentation();
    }

    private void ApplyTextLayout(float groundYLocal)
    {
        Vector3 nextLocalPosition = textContainer.localPosition;
        nextLocalPosition.x = 0f;
        nextLocalPosition.y = groundYLocal + groundingCollider.size.y * textHeightFactor;
        nextLocalPosition.z = 0f;
        textContainer.localPosition = nextLocalPosition;
        textContainer.localRotation = Quaternion.identity;
        textContainer.localScale = Vector3.one;

        RectTransform glyphRect = glyphText.rectTransform;
        glyphRect.localPosition = Vector3.zero;
        glyphRect.localRotation = Quaternion.identity;
        glyphRect.localScale = Vector3.one;
    }

    private static void ApplyFlatLayer(SpriteRenderer renderer, float localY, float planarScale, Color tint, int sortingOrder)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.transform.localPosition = new Vector3(0f, localY, 0f);
        renderer.transform.localRotation = FlatRotation;
        renderer.transform.localScale = Vector3.one * planarScale;
        renderer.color = tint;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = true;
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

    private BoxCollider FindGroundingCollider(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        if (target != null && target.TryGetComponent(out BoxCollider explicitCollider))
        {
            return explicitCollider;
        }

        return GetComponentInChildren<BoxCollider>(includeInactive: true);
    }

    private SpriteRenderer FindSpriteRenderer(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        return target != null ? target.GetComponent<SpriteRenderer>() : null;
    }

    private bool IsRectTransformReferenceValid(RectTransform target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }

    private bool IsGlyphReferenceValid(TMP_Text target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }

    private bool IsGroundingColliderReferenceValid(BoxCollider target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }

    private bool IsSpriteRendererReferenceValid(SpriteRenderer target)
    {
        return target != null && target.transform != null && target.transform.IsChildOf(transform);
    }
}
