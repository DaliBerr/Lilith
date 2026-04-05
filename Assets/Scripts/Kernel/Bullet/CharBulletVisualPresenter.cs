using TMPro;
using UnityEngine;

namespace Kernel.Bullet
{
/// <summary>
/// 为文字子弹补充阴影字、符文底座和短拖尾等表现层效果。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharBulletVisualPresenter : MonoBehaviour
{
    private const float DefaultGlyphSize = 16f;

    [Header("Bindings")]
    [SerializeField] private TMP_Text mainGlyph;
    [SerializeField] private TMP_Text shadowGlyph;
    [SerializeField] private SpriteRenderer coreBaseRenderer;
    [SerializeField] private SpriteRenderer resultBaseRenderer;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private CharBulletVisualLibrary visualLibrary;

    [Header("Shadow")]
    [SerializeField, Min(1f)] private float shadowScaleMultiplier = 1.08f;
    [SerializeField] private Vector3 shadowLocalOffset = new(0f, 0f, 0.03f);
    [SerializeField] private Color shadowTint = new(0f, 0f, 0f, 0.35f);

    [Header("Animation")]
    [SerializeField, Min(0f)] private float corePulseSpeed = 4f;
    [SerializeField, Min(0f)] private float corePulseAmplitude = 0.025f;
    [SerializeField, Min(0f)] private float trailLifetime = 0.08f;
    [SerializeField, Min(0f)] private float trailWidthScale = 0.18f;

    private CharBullet bullet;
    private Vector3 coreBaseRestScale = Vector3.one;
    private Vector3 resultBaseRestScale = Vector3.one;
    private Vector3 resultBaseRestEuler;
    private float resultRotationSpeed;
    private float resultPulseAmplitude;
    private float animationSeed;

    public TMP_Text MainGlyph => mainGlyph;
    public TMP_Text ShadowGlyph => shadowGlyph;
    public SpriteRenderer CoreBaseRenderer => coreBaseRenderer;
    public SpriteRenderer ResultBaseRenderer => resultBaseRenderer;
    public TrailRenderer TrailRenderer => trailRenderer;
    public CharBulletVisualLibrary VisualLibrary => visualLibrary;

    private void Awake()
    {
        animationSeed = Random.value * 100f;
        bullet = GetComponent<CharBullet>();
        TryCacheBindings();
        RefreshPreview();
    }

    private void OnValidate()
    {
        bullet = GetComponent<CharBullet>();
        TryCacheBindings();
        RefreshPreview();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AnimateRuneLayers(Time.time + animationSeed);
    }

    /// <summary>
    /// summary: 尝试缓存当前子弹层级中的主字、阴影字、底座与拖尾组件。
    /// param: overwriteExisting 为 true 时强制重新解析所有绑定
    /// returns: 至少拿到主字或阴影字任意一项时返回 true
    /// </summary>
    public bool TryCacheBindings(bool overwriteExisting = false)
    {
        if (overwriteExisting || mainGlyph == null)
        {
            mainGlyph = FindGlyph("Text/Glyph");
        }

        if (overwriteExisting || shadowGlyph == null)
        {
            shadowGlyph = FindGlyph("Text/GlyphShadow");
        }

        if (overwriteExisting || coreBaseRenderer == null)
        {
            coreBaseRenderer = FindRenderer("RuneBaseCore");
        }

        if (overwriteExisting || resultBaseRenderer == null)
        {
            resultBaseRenderer = FindRenderer("RuneBaseResult");
        }

        if (overwriteExisting || trailRenderer == null)
        {
            trailRenderer = FindTrailRenderer("Trail");
        }

        if (bullet == null)
        {
            bullet = GetComponent<CharBullet>();
        }

        return mainGlyph != null || shadowGlyph != null;
    }

    /// <summary>
    /// summary: 按当前编译结果刷新阴影字、符文底座与拖尾颜色，使 secondary visuals 始终跟随最终子弹外观。
    /// param: compiledAttack 当前发射使用的编译结果；为空时回退到 CharBullet 的当前攻击语义
    /// param: owningBullet 当前持有该 presenter 的文字子弹
    /// returns: 成功完成至少一次可见层同步时返回 true
    /// </summary>
    public bool ApplyCompiledAppearance(CompiledAttack compiledAttack, CharBullet owningBullet)
    {
        if (owningBullet != null)
        {
            bullet = owningBullet;
        }

        if (!TryCacheBindings())
        {
            return false;
        }

        AttackCoreType coreType = compiledAttack != null ? compiledAttack.CoreType : ResolveCurrentCoreType();
        AttackResultType resultType = compiledAttack != null ? compiledAttack.ResultType : ResolveCurrentResultType();
        Color resolvedColor = ResolvePrimaryColor(compiledAttack);

        ApplyPrimaryColor(resolvedColor);
        SyncShadowGlyph(resolvedColor);
        ApplyRuneBaseLayers(coreType, resultType, resolvedColor);
        ApplyTrail(coreType, resolvedColor);
        AnimateRuneLayers(Time.time + animationSeed);
        return true;
    }

    /// <summary>
    /// summary: 用当前主字和现有攻击语义刷新 editor 预览，不覆盖直接手动改过的主字内容。
    /// param: 无
    /// returns: 成功刷新 secondary visuals 时返回 true
    /// </summary>
    public bool RefreshPreview()
    {
        if (!TryCacheBindings())
        {
            return false;
        }

        Color previewColor = mainGlyph != null ? mainGlyph.color : Color.white;
        SyncShadowGlyph(previewColor);
        ApplyRuneBaseLayers(ResolveCurrentCoreType(), ResolveCurrentResultType(), previewColor);
        ApplyTrail(ResolveCurrentCoreType(), previewColor);
        return true;
    }

    private void ApplyPrimaryColor(Color resolvedColor)
    {
        if (mainGlyph != null)
        {
            mainGlyph.color = resolvedColor;
        }
    }

    private void SyncShadowGlyph(Color primaryColor)
    {
        if (mainGlyph == null || shadowGlyph == null)
        {
            return;
        }

        shadowGlyph.text = mainGlyph.text;
        shadowGlyph.font = mainGlyph.font;
        shadowGlyph.alignment = mainGlyph.alignment;
        shadowGlyph.fontSize = mainGlyph.fontSize;
        shadowGlyph.enableAutoSizing = false;
        shadowGlyph.rectTransform.anchorMin = mainGlyph.rectTransform.anchorMin;
        shadowGlyph.rectTransform.anchorMax = mainGlyph.rectTransform.anchorMax;
        shadowGlyph.rectTransform.pivot = mainGlyph.rectTransform.pivot;
        shadowGlyph.rectTransform.anchoredPosition = mainGlyph.rectTransform.anchoredPosition;
        shadowGlyph.rectTransform.sizeDelta = mainGlyph.rectTransform.sizeDelta;
        shadowGlyph.rectTransform.localRotation = mainGlyph.rectTransform.localRotation;
        shadowGlyph.rectTransform.localScale = Vector3.one * shadowScaleMultiplier;
        shadowGlyph.rectTransform.localPosition = shadowLocalOffset;
        shadowGlyph.color = BuildShadowColor(primaryColor);
    }

    private void ApplyRuneBaseLayers(AttackCoreType coreType, AttackResultType resultType, Color primaryColor)
    {
        float runeScaleFactor = ResolveGlyphWorldSize();

        if (coreBaseRenderer != null && visualLibrary != null && visualLibrary.TryGetCoreVisual(coreType, out CharBulletVisualLibrary.CoreVisualEntry coreVisual) && coreVisual.baseSprite != null)
        {
            coreBaseRenderer.enabled = true;
            coreBaseRenderer.sprite = coreVisual.baseSprite;
            coreBaseRenderer.color = BuildCoreBaseColor(primaryColor);
            coreBaseRestScale = Vector3.one * runeScaleFactor * coreVisual.baseScale;
            coreBaseRenderer.transform.localScale = coreBaseRestScale;
        }
        else if (coreBaseRenderer != null)
        {
            coreBaseRenderer.enabled = false;
            coreBaseRestScale = coreBaseRenderer.transform.localScale;
        }

        if (resultBaseRenderer != null && visualLibrary != null && visualLibrary.TryGetResultVisual(resultType, out CharBulletVisualLibrary.ResultVisualEntry resultVisual) && resultVisual.overlaySprite != null)
        {
            resultBaseRenderer.enabled = true;
            resultBaseRenderer.sprite = resultVisual.overlaySprite;
            resultBaseRenderer.color = BuildResultAccentColor(primaryColor, resultVisual.overlayAlpha);
            resultRotationSpeed = resultVisual.rotationSpeed;
            resultPulseAmplitude = resultVisual.pulseAmplitude;
            resultBaseRestScale = Vector3.one * runeScaleFactor * resultVisual.overlayScale;
            resultBaseRenderer.transform.localScale = resultBaseRestScale;
            resultBaseRestEuler = resultBaseRenderer.transform.localEulerAngles;
        }
        else if (resultBaseRenderer != null)
        {
            resultBaseRenderer.enabled = false;
            resultRotationSpeed = 0f;
            resultPulseAmplitude = 0f;
            resultBaseRestScale = resultBaseRenderer.transform.localScale;
            resultBaseRestEuler = resultBaseRenderer.transform.localEulerAngles;
        }
    }

    private void ApplyTrail(AttackCoreType coreType, Color primaryColor)
    {
        if (trailRenderer == null)
        {
            return;
        }

        float visualScaleFactor = ResolveVisualScaleFactor();
        trailRenderer.enabled = true;
        trailRenderer.time = trailLifetime;
        trailRenderer.widthMultiplier = visualScaleFactor * trailWidthScale;
        trailRenderer.startColor = primaryColor;
        trailRenderer.endColor = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f);

        if (visualLibrary != null && visualLibrary.TryGetCoreVisual(coreType, out CharBulletVisualLibrary.CoreVisualEntry coreVisual) && coreVisual.trailGradient != null)
        {
            trailRenderer.colorGradient = coreVisual.trailGradient;
        }
        else
        {
            trailRenderer.colorGradient = BuildFallbackTrailGradient(primaryColor);
        }
    }

    private void AnimateRuneLayers(float timeValue)
    {
        if (coreBaseRenderer != null && coreBaseRenderer.enabled)
        {
            float corePulse = 1f + Mathf.Sin(timeValue * corePulseSpeed) * corePulseAmplitude;
            coreBaseRenderer.transform.localScale = coreBaseRestScale * corePulse;
        }

        if (resultBaseRenderer != null && resultBaseRenderer.enabled)
        {
            float resultPulse = 1f + Mathf.Sin(timeValue * (corePulseSpeed * 0.75f)) * resultPulseAmplitude;
            resultBaseRenderer.transform.localScale = resultBaseRestScale * resultPulse;
            Vector3 animatedEuler = resultBaseRestEuler;
            animatedEuler.z += timeValue * resultRotationSpeed;
            resultBaseRenderer.transform.localEulerAngles = animatedEuler;
        }
    }

    private AttackCoreType ResolveCurrentCoreType()
    {
        if (bullet != null)
        {
            if (HasMeaningfulCompiledAttack())
            {
                return bullet.CurrentCompiledAttack.CoreType;
            }

            return bullet.CurrentAttackSpec.coreType;
        }

        return AttackCoreType.Fire;
    }

    private AttackResultType ResolveCurrentResultType()
    {
        if (bullet != null)
        {
            if (HasMeaningfulCompiledAttack())
            {
                return bullet.CurrentCompiledAttack.ResultType;
            }

            return bullet.CurrentAttackSpec.resultType;
        }

        return AttackResultType.DirectDamage;
    }

    private bool HasMeaningfulCompiledAttack()
    {
        if (bullet == null || bullet.CurrentCompiledAttack == null)
        {
            return false;
        }

        return bullet.CurrentCompiledAttack.CanFire ||
               bullet.CurrentCompiledAttack.CoreType != AttackCoreType.None ||
               bullet.CurrentCompiledAttack.ResultType != AttackResultType.None;
    }

    private Color ResolvePrimaryColor(CompiledAttack compiledAttack)
    {
        if (compiledAttack != null && compiledAttack.HasTextColorOverride)
        {
            return compiledAttack.TextColor;
        }

        AttackCoreType coreType = compiledAttack != null ? compiledAttack.CoreType : ResolveCurrentCoreType();
        if (visualLibrary != null && visualLibrary.TryGetCoreVisual(coreType, out CharBulletVisualLibrary.CoreVisualEntry coreVisual))
        {
            return coreVisual.fallbackTint;
        }

        if (mainGlyph != null)
        {
            return mainGlyph.color;
        }

        return Color.white;
    }

    private float ResolveVisualScaleFactor()
    {
        float glyphSize = DefaultGlyphSize;
        if (mainGlyph != null)
        {
            RectTransform rectTransform = mainGlyph.rectTransform;
            if (rectTransform != null)
            {
                float width = rectTransform.rect.width;
                float height = rectTransform.rect.height;
                glyphSize = Mathf.Max(glyphSize, width > 0f ? width : rectTransform.sizeDelta.x, height > 0f ? height : rectTransform.sizeDelta.y);
            }
        }

        float sizeRatio = Mathf.Max(0.01f, glyphSize / DefaultGlyphSize);
        float scaleRatio = 1f;
        if (bullet != null && bullet.SizeTarget != null)
        {
            Vector3 localScale = bullet.SizeTarget.localScale;
            scaleRatio = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y), Mathf.Abs(localScale.z)));
        }

        return sizeRatio * scaleRatio;
    }

    private float ResolveGlyphWorldSize()
    {
        float glyphSize = DefaultGlyphSize;
        if (mainGlyph != null)
        {
            RectTransform rectTransform = mainGlyph.rectTransform;
            if (rectTransform != null)
            {
                float width = rectTransform.rect.width;
                float height = rectTransform.rect.height;
                glyphSize = Mathf.Max(glyphSize, width > 0f ? width : rectTransform.sizeDelta.x, height > 0f ? height : rectTransform.sizeDelta.y);
            }
        }

        float scaleRatio = 1f;
        if (bullet != null && bullet.SizeTarget != null)
        {
            Vector3 localScale = bullet.SizeTarget.localScale;
            scaleRatio = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y), Mathf.Abs(localScale.z)));
        }

        return glyphSize * scaleRatio;
    }

    private TMP_Text FindGlyph(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        if (target != null && target.TryGetComponent(out TMP_Text text))
        {
            return text;
        }

        return null;
    }

    private SpriteRenderer FindRenderer(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        if (target != null && target.TryGetComponent(out SpriteRenderer renderer))
        {
            return renderer;
        }

        return null;
    }

    private TrailRenderer FindTrailRenderer(string relativePath)
    {
        Transform target = transform.Find(relativePath);
        if (target != null && target.TryGetComponent(out TrailRenderer renderer))
        {
            return renderer;
        }

        return null;
    }

    private Color BuildShadowColor(Color primaryColor)
    {
        Color darkened = Color.Lerp(primaryColor, Color.black, 0.7f);
        darkened.a = shadowTint.a;
        return darkened;
    }

    private static Color BuildCoreBaseColor(Color primaryColor)
    {
        Color baseColor = Color.Lerp(primaryColor, Color.white, 0.15f);
        baseColor.a = 0.28f;
        return baseColor;
    }

    private static Color BuildResultAccentColor(Color primaryColor, float overlayAlpha)
    {
        Color accent = Color.Lerp(primaryColor, Color.white, 0.45f);
        accent.a = overlayAlpha;
        return accent;
    }

    private static Gradient BuildFallbackTrailGradient(Color primaryColor)
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(primaryColor, 0f),
                new GradientColorKey(Color.Lerp(primaryColor, Color.white, 0.35f), 0.45f),
                new GradientColorKey(primaryColor, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.25f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }
}
}
