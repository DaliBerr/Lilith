using UnityEngine;

/// <summary>
/// 定义敌人运行时数据契约，具体数据由派生类自行持有。
/// </summary>
[DisallowMultipleComponent]
public abstract class Enemy : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string enemyName = "Enemy";

    public string EnemyName => string.IsNullOrWhiteSpace(enemyName) ? GetType().Name : enemyName.Trim();
    public abstract float MoveSpeed { get; }
    public abstract float RotationSpeed { get; }
    public abstract float StoppingDistance { get; }
    public abstract float AttackRange { get; }
    public abstract float AttackCooldown { get; }
    public abstract float AttackDamage { get; }
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
/// 用于描述单波敌人的运行时覆写配置。
/// </summary>
[System.Serializable]
public struct EnemyWaveConfig
{
    [Min(0f)] public float maxHealth;
    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float attackRange;
    [Min(0f)] public float attackCooldown;
    [Min(0f)] public float attackDamage;

    public EnemyWaveConfig(float maxHealth, float moveSpeed, float attackRange, float attackCooldown, float attackDamage)
    {
        this.maxHealth = maxHealth;
        this.moveSpeed = moveSpeed;
        this.attackRange = attackRange;
        this.attackCooldown = attackCooldown;
        this.attackDamage = attackDamage;
    }
}

/// <summary>
/// 描述单个波次里一种敌人的刷新数量与运行时覆写配置。
/// </summary>
[System.Serializable]
public struct WaveEnemySpawnEntry
{
    public string enemyName;
    [Min(0)] public int spawnCount;
    public EnemyWaveConfig enemyConfig;

    public WaveEnemySpawnEntry(string enemyName, int spawnCount, EnemyWaveConfig enemyConfig)
    {
        this.enemyName = enemyName;
        this.spawnCount = spawnCount;
        this.enemyConfig = enemyConfig;
    }
}

/// <summary>
/// 为需要承接旧版移动参数迁移的敌人类型提供可选扩展契约。
/// </summary>
public interface ILegacyEnemyMovementSettingsReceiver
{
    bool TryApplyLegacyMovementSettingsIfNeeded(float legacyMoveSpeed, float legacyRotationSpeed, float legacyStoppingDistance);
}

/// <summary>
/// 允许敌人在生成后接收当前波次的运行时配置。
/// </summary>
public interface IEnemyWaveConfigReceiver
{
    /// <summary>
    /// summary: 把当前波次给出的敌人配置应用到自身运行时数据。
    /// param: config 当前波次指定的敌人数值配置
    /// returns: 无
    /// </summary>
    void ApplyWaveConfig(EnemyWaveConfig config);
}
