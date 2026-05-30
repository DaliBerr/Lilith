using System;
using UnityEngine;
using Vocalith.EventSystem;

/// <summary>
/// 请求当前可用 gameplay 相机播放一次屏幕震动。
/// </summary>
public readonly struct ScreenShakeRequestEvent
{
    public readonly object source;
    public readonly float amplitude;
    public readonly float duration;
    public readonly float frequency;

    public ScreenShakeRequestEvent(object source, float amplitude, float duration, float frequency = 0f)
    {
        this.source = source;
        this.amplitude = amplitude;
        this.duration = duration;
        this.frequency = frequency;
    }
}

public static class ScreenShakeSettings
{
    public const string PlayerPrefsKey = "Options.Gameplay.ScreenShake";

    public static bool IsEnabled()
    {
        return PlayerPrefs.GetInt(PlayerPrefsKey, 1) != 0;
    }
}

/// <summary>
/// 为相机脚本提供可序列化的屏幕震动状态与事件接入。
/// </summary>
[Serializable]
public sealed class ScreenShakeState
{
    private const float DefaultPlayerDamageAmplitude = 0.45f;
    private const float DefaultPlayerDamageDuration = 0.18f;
    private const float DefaultFrequency = 34f;
    private const float MinimumHealth = 0.01f;
    private const float SeedStep = 13.731f;
    private const float FullCircle = Mathf.PI * 2f;

    [SerializeField] private bool shakeOnPlayerDamage = true;
    [SerializeField, Min(0f)] private float playerDamageAmplitude = DefaultPlayerDamageAmplitude;
    [SerializeField, Min(0f)] private float playerDamageDuration = DefaultPlayerDamageDuration;
    [SerializeField, Min(0f)] private float maxAmplitude = 1.2f;
    [SerializeField, Min(0f)] private float frequency = DefaultFrequency;
    [SerializeField, Range(0f, 1f)] private float verticalWeight = 0.75f;

    [NonSerialized] private IDisposable playerHealthChangedSubscription;
    [NonSerialized] private IDisposable screenShakeRequestSubscription;
    [NonSerialized] private float activeAmplitude;
    [NonSerialized] private float activeDuration;
    [NonSerialized] private float activeElapsed;
    [NonSerialized] private float activeFrequency;
    [NonSerialized] private float activeSeed;
    [NonSerialized] private int requestSequence;

    public Vector3 CurrentOffset { get; private set; }
    public bool IsActive => activeDuration > 0f && activeElapsed < activeDuration && activeAmplitude > 0f;

    public void Enable()
    {
        Sanitize();
        playerHealthChangedSubscription ??= EventManager.eventBus.Subscribe<PlayerHealthChangedEvent>(HandlePlayerHealthChanged);
        screenShakeRequestSubscription ??= EventManager.eventBus.Subscribe<ScreenShakeRequestEvent>(HandleScreenShakeRequest);
    }

    public void Disable()
    {
        playerHealthChangedSubscription?.Dispose();
        playerHealthChangedSubscription = null;
        screenShakeRequestSubscription?.Dispose();
        screenShakeRequestSubscription = null;
        ResetRuntimeState();
    }

    public void Sanitize()
    {
        playerDamageAmplitude = SanitizeNonNegative(playerDamageAmplitude, DefaultPlayerDamageAmplitude);
        playerDamageDuration = SanitizeNonNegative(playerDamageDuration, DefaultPlayerDamageDuration);
        maxAmplitude = SanitizeNonNegative(maxAmplitude, Mathf.Max(DefaultPlayerDamageAmplitude, playerDamageAmplitude));
        frequency = SanitizeNonNegative(frequency, DefaultFrequency);
        verticalWeight = Mathf.Clamp01(verticalWeight);
    }

    public void RequestShake(float amplitude, float duration, float frequencyOverride = 0f)
    {
        Sanitize();
        if (!ScreenShakeSettings.IsEnabled())
        {
            ResetRuntimeState();
            return;
        }

        float resolvedAmplitude = Mathf.Clamp(SanitizeNonNegative(amplitude, 0f), 0f, maxAmplitude);
        float resolvedDuration = SanitizeNonNegative(duration, 0f);
        if (resolvedAmplitude <= 0f || resolvedDuration <= 0f)
        {
            return;
        }

        activeAmplitude = resolvedAmplitude;
        activeDuration = resolvedDuration;
        activeElapsed = 0f;
        activeFrequency = frequencyOverride > 0f ? frequencyOverride : frequency;
        activeSeed = ++requestSequence * SeedStep;
    }

    public Vector3 Tick(float deltaTime, Quaternion cameraRotation)
    {
        if (!ScreenShakeSettings.IsEnabled())
        {
            ResetRuntimeState();
            return CurrentOffset;
        }

        if (!IsActive)
        {
            CurrentOffset = Vector3.zero;
            return CurrentOffset;
        }

        activeElapsed = Mathf.Min(activeDuration, activeElapsed + Mathf.Max(0f, deltaTime));
        float progress = activeDuration > 0f ? Mathf.Clamp01(activeElapsed / activeDuration) : 1f;
        if (progress >= 1f)
        {
            ResetRuntimeState();
            return CurrentOffset;
        }

        float strength = activeAmplitude * (1f - progress);
        float sample = activeSeed + activeElapsed * activeFrequency;
        float horizontal = Mathf.Sin(sample * FullCircle);
        float vertical = Mathf.Sin((sample + 0.37f) * FullCircle * 1.37f) * verticalWeight;

        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;
        CurrentOffset = ((right * horizontal) + (up * vertical)) * strength;
        return CurrentOffset;
    }

    private void HandlePlayerHealthChanged(PlayerHealthChangedEvent evt)
    {
        if (!shakeOnPlayerDamage || evt.delta >= 0f)
        {
            return;
        }

        float damageRatio = Mathf.Clamp01(-evt.delta / Mathf.Max(MinimumHealth, evt.maxHealth));
        float amplitudeMultiplier = Mathf.Lerp(0.65f, 1.25f, damageRatio);
        RequestShake(playerDamageAmplitude * amplitudeMultiplier, playerDamageDuration);
    }

    private void HandleScreenShakeRequest(ScreenShakeRequestEvent evt)
    {
        RequestShake(evt.amplitude, evt.duration, evt.frequency);
    }

    private void ResetRuntimeState()
    {
        activeAmplitude = 0f;
        activeDuration = 0f;
        activeElapsed = 0f;
        activeFrequency = 0f;
        CurrentOffset = Vector3.zero;
    }

    private static float SanitizeNonNegative(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            return Mathf.Max(0f, fallback);
        }

        return value;
    }
}
