using System;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using UnityEngine;

/// <summary>
/// 定义敌人运行时数据契约，具体数据由派生类自行持有。
/// </summary>
[DisallowMultipleComponent]
public abstract class Enemy : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private EnemyDefinition enemyDefinition;

    private bool hasRaisedDeathNotification;

    public EnemyDefinition Definition => enemyDefinition;
    public string EnemyName => enemyDefinition != null ? enemyDefinition.EnemyId : GetType().Name;
    public abstract float MoveSpeed { get; }
    public abstract float RotationSpeed { get; }
    public abstract float StoppingDistance { get; }
    public abstract float AttackRange { get; }
    public abstract float AttackCooldown { get; }
    public abstract float AttackDamage { get; }
    public abstract float MaxHealth { get; }
    public abstract float CurrentHealth { get; }
    public bool IsDead => CurrentHealth <= 0f;
    public event Action<Enemy> Damaged;
    public event Action<Enemy> Died;

    /// <summary>
    /// summary: 对当前敌人施加一次伤害，并在生命归零时进入死亡状态。
    /// param: damage 本次要扣减的生命值
    /// param: remainingHealth 本次伤害结算后的剩余生命值
    /// param: isDead 本次伤害结算后是否已经死亡
    /// returns: 成功处理本次伤害时返回 true
    /// </summary>
    public abstract bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead);

    /// <summary>
    /// summary: 把当前敌人实例绑定到一个敌人定义资产上，供行为与调试读取稳定标识。
    /// param: definition 当前实例应持有的敌人定义
    /// returns: 传入定义有效时返回 true
    /// </summary>
    public bool TryBindDefinition(EnemyDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        enemyDefinition = definition;
        return true;
    }

    /// <summary>
    /// summary: 在敌人进入新的有效生命周期时，重置一次性死亡通知状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    protected void ResetDeathNotificationState()
    {
        hasRaisedDeathNotification = false;
    }

    /// <summary>
    /// summary: 向外部订阅者广播一次受击通知，供仇恨、受击反馈等行为订阅。
    /// param: 无
    /// returns: 无
    /// </summary>
    protected void NotifyDamaged()
    {
        Damaged?.Invoke(this);
    }

    /// <summary>
    /// summary: 向外部订阅者广播一次死亡通知；同一生命周期内只会成功发出一次。
    /// param: 无
    /// returns: 首次成功广播时返回 true
    /// </summary>
    protected bool TryNotifyDied()
    {
        if (hasRaisedDeathNotification)
        {
            return false;
        }

        hasRaisedDeathNotification = true;
        Died?.Invoke(this);
        return true;
    }
}

/// <summary>
/// 描述单个 Bullet Token 掉落项以及它的独立掉落概率。
/// </summary>
[Serializable]
public struct EnemyBulletTokenDropEntry
{
    public PlaceableTokenData token;
    [Range(0f, 1f)] public float dropChance;

    public EnemyBulletTokenDropEntry(PlaceableTokenData token, float dropChance)
    {
        this.token = token;
        this.dropChance = dropChance;
    }
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
    public List<EnemyBulletTokenDropEntry> tokenDrops;

    public EnemyWaveConfig(float maxHealth, float moveSpeed, float attackRange, float attackCooldown, float attackDamage)
        : this(maxHealth, moveSpeed, attackRange, attackCooldown, attackDamage, null)
    {
    }

    public EnemyWaveConfig(
        float maxHealth,
        float moveSpeed,
        float attackRange,
        float attackCooldown,
        float attackDamage,
        IEnumerable<EnemyBulletTokenDropEntry> tokenDrops)
    {
        this.maxHealth = maxHealth;
        this.moveSpeed = moveSpeed;
        this.attackRange = attackRange;
        this.attackCooldown = attackCooldown;
        this.attackDamage = attackDamage;
        this.tokenDrops = tokenDrops != null ? new List<EnemyBulletTokenDropEntry>(tokenDrops) : new List<EnemyBulletTokenDropEntry>();
    }

    /// <summary>
    /// summary: 修正当前敌人数值配置中的非法取值，并可选清空掉落表供召唤物复用。
    /// param: clearTokenDrops 为 true 时输出配置中不会保留任何 token 掉落项
    /// returns: 经过规范化后的运行时敌人数值配置副本
    /// </summary>
    public EnemyWaveConfig GetSanitized(bool clearTokenDrops = false)
    {
        EnemyWaveConfig sanitized = this;
        sanitized.maxHealth = SanitizePositiveValue(sanitized.maxHealth, 1f);
        sanitized.moveSpeed = SanitizeValue(sanitized.moveSpeed, 0f);
        sanitized.attackRange = SanitizeValue(sanitized.attackRange, 0f);
        sanitized.attackCooldown = SanitizeValue(sanitized.attackCooldown, 0f);
        sanitized.attackDamage = SanitizeValue(sanitized.attackDamage, 0f);
        sanitized.tokenDrops = clearTokenDrops ? new List<EnemyBulletTokenDropEntry>() : SanitizeTokenDrops(sanitized.tokenDrops);
        return sanitized;
    }

    private static List<EnemyBulletTokenDropEntry> SanitizeTokenDrops(IEnumerable<EnemyBulletTokenDropEntry> tokenDrops)
    {
        List<EnemyBulletTokenDropEntry> sanitizedDrops = new();
        if (tokenDrops == null)
        {
            return sanitizedDrops;
        }

        foreach (EnemyBulletTokenDropEntry entry in tokenDrops)
        {
            EnemyBulletTokenDropEntry sanitizedEntry = entry;
            sanitizedEntry.dropChance = Mathf.Clamp01(sanitizedEntry.dropChance);
            sanitizedDrops.Add(sanitizedEntry);
        }

        return sanitizedDrops;
    }

    private static float SanitizeValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            return fallbackValue;
        }

        return value;
    }

    private static float SanitizePositiveValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return fallbackValue;
        }

        return value;
    }
}

/// <summary>
/// 描述单个波次里一种敌人的刷新数量与运行时覆写配置。
/// </summary>
[System.Serializable]
public struct WaveEnemySpawnEntry
{
    public EnemyDefinition enemyDefinition;
    [Min(0)] public int spawnCount;
    public EnemyWaveConfig enemyConfig;

    public WaveEnemySpawnEntry(EnemyDefinition enemyDefinition, int spawnCount, EnemyWaveConfig enemyConfig)
    {
        this.enemyDefinition = enemyDefinition;
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

/// <summary>
/// 为敌人运行时行为提供统一的 UI/暂停阻断判断，避免移动和攻击各自维护一套条件。
/// </summary>
public static class EnemyGameplayPauseGuard
{
    /// <summary>
    /// summary: 判断当前是否存在会暂停敌人行为的 UI 或游戏状态。
    /// param: 无
    /// returns: 当暂停菜单、背包或显式 Paused 状态存在时返回 true
    /// </summary>
    public static bool ShouldSuspendEnemyActions()
    {
        return StatusController.HasStatus(StatusList.InBackPackStatus)
            || StatusController.HasStatus(StatusList.InPauseMenuStatus)
            || StatusController.HasStatus(StatusList.PausedStatus);
    }
}
