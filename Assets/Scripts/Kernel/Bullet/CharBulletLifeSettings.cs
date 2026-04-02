using System;
using UnityEngine;

/// <summary>
/// 文字子弹的生命周期配置，控制存活时间、飞行距离以及命中消耗。
/// </summary>
[Serializable]
public struct CharBulletLifeSettings
{
    [Min(0f)] public float maxLifetime;
    [Min(0f)] public float maxTravelDistance;
    [Min(1)] public int maxLife;
    [Min(1)] public int impactLifeCost;
    public LayerMask impactMask;

    /// <summary>
    /// summary: 对生命周期配置做最小安全修正，避免出现零生命或零命中消耗。
    /// param: 无
    /// returns: 可直接用于运行时的安全配置副本
    /// </summary>
    public CharBulletLifeSettings GetSanitized()
    {
        CharBulletLifeSettings sanitized = this;
        sanitized.maxLifetime = Mathf.Max(0f, sanitized.maxLifetime);
        sanitized.maxTravelDistance = Mathf.Max(0f, sanitized.maxTravelDistance);
        sanitized.maxLife = Mathf.Max(1, sanitized.maxLife);
        sanitized.impactLifeCost = Mathf.Max(1, sanitized.impactLifeCost);
        return sanitized;
    }
}
