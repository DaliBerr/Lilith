using UnityEngine;

/// <summary>
/// 定义敌人运行时数据契约，具体数据由派生类自行持有。
/// </summary>
[DisallowMultipleComponent]
public abstract class Enemy : MonoBehaviour
{
    public abstract float MoveSpeed { get; }
    public abstract float RotationSpeed { get; }
    public abstract float StoppingDistance { get; }
    public abstract float MaxHealth { get; }
    public abstract float CurrentHealth { get; }
    public bool IsDead => CurrentHealth <= 0f;

    /// <summary>
    /// summary: 对当前敌人施加一次伤害，并在生命归零时进入死亡状态。
    /// param: damage 本次要扣减的生命值
    /// param: remainingHealth 本次伤害结算后的剩余生命值
    /// param: isDead 本次伤害结算后是否已经死亡
    /// returns: 成功处理本次伤害时返回 true
    /// </summary>
    public abstract bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead);
}

/// <summary>
/// 为需要承接旧版移动参数迁移的敌人类型提供可选扩展契约。
/// </summary>
public interface ILegacyEnemyMovementSettingsReceiver
{
    bool TryApplyLegacyMovementSettingsIfNeeded(float legacyMoveSpeed, float legacyRotationSpeed, float legacyStoppingDistance);
}
