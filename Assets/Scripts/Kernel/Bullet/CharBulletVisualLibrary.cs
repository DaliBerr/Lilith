using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
/// <summary>
/// 按核心词与结果词提供文字子弹的表现层配置。
/// </summary>
[CreateAssetMenu(fileName = "CharBulletVisualLibrary", menuName = "Lilith/Bullet/Char Bullet Visual Library")]
public sealed class CharBulletVisualLibrary : ScriptableObject
{
    [Serializable]
    public struct CoreVisualEntry
    {
        public AttackCoreType coreType;
        public Sprite baseSprite;
        public Color fallbackTint;
        [Min(0f)] public float baseScale;
        public Gradient trailGradient;
    }

    [Serializable]
    public struct ResultVisualEntry
    {
        public AttackResultType resultType;
        public Sprite overlaySprite;
        [Min(0f)] public float overlayScale;
        [Range(0f, 1f)] public float overlayAlpha;
        public float rotationSpeed;
        [Min(0f)] public float pulseAmplitude;
    }

    [SerializeField] private List<CoreVisualEntry> coreVisuals = new();
    [SerializeField] private List<ResultVisualEntry> resultVisuals = new();

    public IReadOnlyList<CoreVisualEntry> CoreVisuals => coreVisuals;
    public IReadOnlyList<ResultVisualEntry> ResultVisuals => resultVisuals;

    private void OnValidate()
    {
        SanitizeEntries();
    }

    /// <summary>
    /// summary: 按核心词类型查找对应的基础符文配置。
    /// param: coreType 当前需要查询的核心词类型
    /// param: entry 输出的核心表现配置
    /// returns: 找到匹配配置时返回 true
    /// </summary>
    public bool TryGetCoreVisual(AttackCoreType coreType, out CoreVisualEntry entry)
    {
        for (int i = 0; i < coreVisuals.Count; i++)
        {
            if (coreVisuals[i].coreType == coreType)
            {
                entry = coreVisuals[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// summary: 按结果词类型查找对应的覆盖符文配置。
    /// param: resultType 当前需要查询的结果词类型
    /// param: entry 输出的结果表现配置
    /// returns: 找到匹配配置时返回 true
    /// </summary>
    public bool TryGetResultVisual(AttackResultType resultType, out ResultVisualEntry entry)
    {
        for (int i = 0; i < resultVisuals.Count; i++)
        {
            if (resultVisuals[i].resultType == resultType)
            {
                entry = resultVisuals[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    private void SanitizeEntries()
    {
        for (int i = 0; i < coreVisuals.Count; i++)
        {
            CoreVisualEntry entry = coreVisuals[i];
            entry.baseScale = Mathf.Max(0f, entry.baseScale);
            entry.fallbackTint.a = Mathf.Clamp01(entry.fallbackTint.a);
            coreVisuals[i] = entry;
        }

        for (int i = 0; i < resultVisuals.Count; i++)
        {
            ResultVisualEntry entry = resultVisuals[i];
            entry.overlayScale = Mathf.Max(0f, entry.overlayScale);
            entry.overlayAlpha = Mathf.Clamp01(entry.overlayAlpha);
            entry.pulseAmplitude = Mathf.Max(0f, entry.pulseAmplitude);
            resultVisuals[i] = entry;
        }
    }
}
}
