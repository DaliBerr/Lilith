using TMPro;
using UnityEngine;

/// <summary>
/// 在敌人身上统一管理结果词命中反馈：默认白色、控制命中黄闪、治疗命中绿闪、眩晕期间金色常驻。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyResultVisualFeedback : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private CharEnemyVisualPresenter visualPresenter;
    [SerializeField] private EnemyStatusEffectController statusEffects;

    [Header("Palette")]
    [SerializeField] private Color baseWhiteColor = new(1f, 1f, 1f, 1f);
    [SerializeField] private Color controlHitPulseColor = new(1f, 0.92f, 0.18f, 1f);
    [SerializeField] private Color healingHitPulseColor = new(0.32f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color fullyControlledColor = new(1f, 0.8f, 0.12f, 1f);
    [SerializeField] private Color polymorphedColor = new(0.92f, 0.96f, 1f, 1f);

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float controlHitPulseDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float healingHitPulseDuration = 0.2f;

    private SpriteRenderer runeBaseRenderer;
    private SpriteRenderer groundShadowRenderer;
    private TMP_Text glyphText;

    private float runeBaseAlpha = 1f;
    private float groundShadowAlpha = 1f;
    private float glyphAlpha = 1f;

    private Color activePulseColor;
    private float pulseRemainingDuration;
    private float pulseTotalDuration;

    private void Awake()
    {
        TryCacheBindings();
        CaptureBaselineAlpha();
    }

    private void OnValidate()
    {
        TryCacheBindings();
        CaptureBaselineAlpha();
        controlHitPulseDuration = Mathf.Max(0.01f, controlHitPulseDuration);
        healingHitPulseDuration = Mathf.Max(0.01f, healingHitPulseDuration);
    }

    private void LateUpdate()
    {
        if (!TryCacheBindings())
        {
            return;
        }

        bool isStunned = statusEffects != null && statusEffects.IsStunned;
        bool isPolymorphed = statusEffects != null && statusEffects.IsPolymorphed;

        if (!isStunned && !isPolymorphed && pulseRemainingDuration > 0f)
        {
            pulseRemainingDuration = Mathf.Max(0f, pulseRemainingDuration - Time.deltaTime);
        }

        Color targetColor = ResolveCurrentColor(isStunned, isPolymorphed);
        ApplyColorToAll(targetColor);
    }

    /// <summary>
    /// summary: 通知当前敌人收到一次控制命中，触发短暂黄色脉冲。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void NotifyControlHitPulse()
    {
        StartPulse(controlHitPulseColor, controlHitPulseDuration);
    }

    /// <summary>
    /// summary: 通知当前敌人收到一次治疗命中，触发短暂绿色脉冲。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void NotifyHealingHitPulse()
    {
        StartPulse(healingHitPulseColor, healingHitPulseDuration);
    }

    /// <summary>
    /// summary: 尝试缓存当前敌人的可见层绑定，供颜色反馈直接写入。
    /// param: 无
    /// returns: 成功拿到至少一个可见层时返回 true
    /// </summary>
    public bool TryCacheBindings()
    {
        if (visualPresenter == null || visualPresenter.transform != transform)
        {
            visualPresenter = GetComponent<CharEnemyVisualPresenter>();
        }

        if (statusEffects == null || statusEffects.transform != transform)
        {
            statusEffects = GetComponent<EnemyStatusEffectController>();
        }

        if (visualPresenter != null)
        {
            visualPresenter.TryCacheBindings();
            runeBaseRenderer = visualPresenter.RuneBaseRenderer;
            groundShadowRenderer = visualPresenter.GroundShadowRenderer;
            glyphText = visualPresenter.GlyphText;
        }

        return runeBaseRenderer != null || groundShadowRenderer != null || glyphText != null;
    }

    private void StartPulse(Color pulseColor, float duration)
    {
        activePulseColor = pulseColor;
        pulseTotalDuration = Mathf.Max(0.01f, duration);
        pulseRemainingDuration = pulseTotalDuration;
    }

    private Color ResolveCurrentColor(bool isStunned, bool isPolymorphed)
    {
        if (isPolymorphed)
        {
            return polymorphedColor;
        }

        if (isStunned)
        {
            return fullyControlledColor;
        }

        if (pulseRemainingDuration > 0f)
        {
            float progress = 1f - (pulseRemainingDuration / Mathf.Max(0.01f, pulseTotalDuration));
            return Color.Lerp(activePulseColor, baseWhiteColor, Mathf.Clamp01(progress));
        }

        return baseWhiteColor;
    }

    private void ApplyColorToAll(Color targetColor)
    {
        if (runeBaseRenderer != null)
        {
            runeBaseRenderer.color = ComposeColorWithAlpha(targetColor, runeBaseAlpha);
        }

        if (groundShadowRenderer != null)
        {
            groundShadowRenderer.color = ComposeColorWithAlpha(targetColor, groundShadowAlpha);
        }

        if (glyphText != null)
        {
            glyphText.color = ComposeColorWithAlpha(targetColor, glyphAlpha);
        }
    }

    private void CaptureBaselineAlpha()
    {
        if (runeBaseRenderer != null)
        {
            runeBaseAlpha = Mathf.Clamp01(runeBaseRenderer.color.a);
        }

        if (groundShadowRenderer != null)
        {
            groundShadowAlpha = Mathf.Clamp01(groundShadowRenderer.color.a);
        }

        if (glyphText != null)
        {
            glyphAlpha = Mathf.Clamp01(glyphText.color.a);
        }
    }

    private static Color ComposeColorWithAlpha(Color rgbSource, float alpha)
    {
        Color resolved = rgbSource;
        resolved.a = Mathf.Clamp01(alpha);
        return resolved;
    }
}
