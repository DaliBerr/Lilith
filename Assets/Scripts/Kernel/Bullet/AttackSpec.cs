using System;
using UnityEngine;
namespace Kernel.Bullet
{
/// <summary>
/// 攻击核心类型，用于描述一次攻击最主要的语义载体。
/// </summary>
public enum AttackCoreType
{
    None = 0,
    Fire = 1,
    Ice = 2,
    Thunder = 3,
    Edge = 4,
    Light = 5,
    Shadow = 6,
    Toxin = 7,
    Arrow = 8,
    Rock = 9,
    Water = 10,
    Wind = 11,
    Sheep = 12,
    Riddle = 13,
}

/// <summary>
/// 攻击行为类型，用于描述攻击如何传播或命中目标。
/// </summary>
public enum AttackBehaviorType
{
    None = 0,
    Straight = 1, //命中后小时
    Spread = 2, //发散多条，命中后消失
    Bounce = 3, //命中后弹射，优先反弹目标，反弹次数用 bounceCount 表示
    Chain = 4, //命中后链式弹射，优先弹射目标，弹射次数用 chainCount 表示
    Orbit = 5, //环绕目标飞行，持续时间用 projectileLife 表示，命中后不消失
    Pierce = 6, //命中敌人后继续飞行，穿透次数用 pierceCount 表示
    Homing = 7, //持续追踪目标，命中后按结果词结算
    Stasis = 8, //停在发射点，持续时间用 behaviorParameter 表示
    Rush = 9, //飞行中逐渐加速，强度用 behaviorParameter 表示
    Slow = 10, //飞行中逐渐减速，强度用 behaviorParameter 表示
    Snake = 11, //沿初始方向蛇形摆动，摆幅强度用 behaviorParameter 表示
    Wander = 12, //沿当前方向确定性漂移，漂移强度用 behaviorParameter 表示
    Split = 13, //飞行期间均匀派生小弹，次数用 behaviorParameter 表示
    Spin = 14, //围绕施法者水平环绕，半径用 behaviorParameter 表示
}

/// <summary>
/// 数值词条类型，用于描述当前攻击主要依赖哪类数值表达。
/// </summary>
public enum AttackValueType
{
    None = 0,
    oneShot = 1, //单次伤害，适用于直射、发散、弹射等行为
    DamageOverTime = 2, //持续伤害，适用于环绕等行为
    Duration = 3, //持续时间，适用于状态效果等行为
    Stack = 4, //堆叠次数，适用于多重效果等行为

}

/// <summary>
/// 攻击结果类型，用于描述命中后产生的主要结果。
/// </summary>
public enum AttackResultType
{
    None = 0,
    DirectDamage = 1,
    Explosion = 2,
    StatusEffect = 3,
    SpawnChild = 4,
    Split = 5,
    Healing = 6,
    Drain = 7,
    Shield = 8,
    Leave = 9,
    Push = 10,
    Pull = 11,
    Confuse = 12,
}

/// <summary>
/// 汇总一次攻击的语义词条与运行时参数，作为子弹攻击配置的单一入口。
/// </summary>
[Serializable]
public struct AttackSpec
{
    public AttackCoreType coreType;
    public AttackBehaviorType behaviorType;
    public AttackValueType valueType;
    public AttackResultType resultType;

    [Min(0f)] public float damage;
    [Min(1)] public int projectileCount;
    [Min(0)] public int bounceCount;
    [Min(0)] public int chainCount;
    [Min(0)] public int pierceCount;
    [Min(1)] public int projectileLife;
    [Min(1)] public int impactLifeCost;
    [Min(0f)] public float projectileSpeed;
    [Min(0f)] public float maxLifetime;
    [Min(0f)] public float maxTravelDistance;
    [Min(0f)] public float behaviorParameter;
    public LayerMask impactMask;

    /// <summary>
    /// summary: 创建一份当前项目可直接用于文字子弹的默认攻击配置。
    /// param: 无
    /// returns: 带有默认伤害、速度与生命周期的攻击配置
    /// </summary>
    public static AttackSpec CreateDefault()
    {
        return new AttackSpec
        {
            coreType = AttackCoreType.Fire,
            behaviorType = AttackBehaviorType.Straight,
            valueType = AttackValueType.oneShot,
            resultType = AttackResultType.DirectDamage,
            damage = 1f,
            projectileCount = 1,
            bounceCount = 0,
            chainCount = 0,
            pierceCount = 0,
            projectileLife = 1,
            impactLifeCost = 1,
            projectileSpeed = 320f,
            maxLifetime = 2f,
            maxTravelDistance = 512f,
            behaviorParameter = 0f,
            impactMask = Physics.DefaultRaycastLayers,
        };
    }

    /// <summary>
    /// summary: 修正攻击配置中的非法值，并保证穿透次数不会超过子弹生命可承载的上限。
    /// param: 无
    /// returns: 可直接用于运行时的安全攻击配置副本
    /// </summary>
    public AttackSpec GetSanitized()
    {
        AttackSpec sanitized = this;
        sanitized.damage = Mathf.Max(0f, sanitized.damage);
        sanitized.projectileCount = Mathf.Max(1, sanitized.projectileCount);
        sanitized.bounceCount = Mathf.Max(0, sanitized.bounceCount);
        sanitized.chainCount = Mathf.Max(0, sanitized.chainCount);
        sanitized.pierceCount = Mathf.Max(0, sanitized.pierceCount);
        sanitized.projectileLife = Mathf.Max(
            1,
            Mathf.Max(
                sanitized.projectileLife,
                Mathf.Max(sanitized.pierceCount + 1, sanitized.bounceCount + 1)));
        sanitized.impactLifeCost = Mathf.Max(1, sanitized.impactLifeCost);
        sanitized.projectileSpeed = Mathf.Max(0f, sanitized.projectileSpeed);
        sanitized.maxLifetime = Mathf.Max(0f, sanitized.maxLifetime);
        sanitized.maxTravelDistance = Mathf.Max(0f, sanitized.maxTravelDistance);
        sanitized.behaviorParameter = Mathf.Max(0f, sanitized.behaviorParameter);
        return sanitized;
    }
}
}
