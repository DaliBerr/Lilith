using System;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using UnityEngine;
using Vocalith.EventSystem;

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
    public virtual float DisplacementWeight => 1f;
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
    /// summary: 对当前敌人结算一次治疗，并在不超上限前提下返回最新生命状态。
    /// param: healing 本次要恢复的生命值
    /// param: resultingHealth 本次治疗结算后的生命值
    /// param: isDead 本次治疗结算后是否已经死亡
    /// returns: 成功处理本次治疗时返回 true
    /// </summary>
    public abstract bool TryApplyHealing(float healing, out float resultingHealth, out bool isDead);

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
        EventManager.eventBus.Publish(new EnemyDiedEvent(this, ResolveDisplayName()));
        return true;
    }

    /// <summary>
    /// summary: 解析当前敌人在结算与日志中应展示的名称；优先使用定义里的 DisplayName。
    /// param: 无
    /// returns: 当前敌人的展示名称
    /// </summary>
    private string ResolveDisplayName()
    {
        if (enemyDefinition != null && !string.IsNullOrWhiteSpace(enemyDefinition.DisplayName))
        {
            return enemyDefinition.DisplayName;
        }

        return EnemyName;
    }
}

/// <summary>
/// 描述单个 Bullet Token 掉落项、独立掉落概率与命中后生成数量。
/// </summary>
[Serializable]
public struct EnemyBulletTokenDropEntry
{
    public PlaceableTokenData token;
    [Range(0f, 1f)] public float dropChance;
    [Min(1)] public int dropCount;

    public EnemyBulletTokenDropEntry(PlaceableTokenData token, float dropChance, int dropCount = 1)
    {
        this.token = token;
        this.dropChance = dropChance;
        this.dropCount = Mathf.Max(1, dropCount);
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
    [Range(0f, 1f)] public float damageReductionPercent;
    [Min(0f)] public float visualScaleMultiplier;
    [Min(0f)] public float explosionRadius;
    [Min(0f)] public float displacementWeight;
    public List<EnemyBulletTokenDropEntry> tokenDrops;

    public EnemyWaveConfig(float maxHealth, float moveSpeed, float attackRange, float attackCooldown, float attackDamage)
        : this(maxHealth, moveSpeed, attackRange, attackCooldown, attackDamage, 0f, 1f, 0f, 1f, null)
    {
    }

    public EnemyWaveConfig(
        float maxHealth,
        float moveSpeed,
        float attackRange,
        float attackCooldown,
        float attackDamage,
        IEnumerable<EnemyBulletTokenDropEntry> tokenDrops)
        : this(maxHealth, moveSpeed, attackRange, attackCooldown, attackDamage, 0f, 1f, 0f, 1f, tokenDrops)
    {
    }

    public EnemyWaveConfig(
        float maxHealth,
        float moveSpeed,
        float attackRange,
        float attackCooldown,
        float attackDamage,
        float damageReductionPercent,
        float visualScaleMultiplier,
        float explosionRadius,
        IEnumerable<EnemyBulletTokenDropEntry> tokenDrops)
        : this(maxHealth, moveSpeed, attackRange, attackCooldown, attackDamage, damageReductionPercent, visualScaleMultiplier, explosionRadius, 1f, tokenDrops)
    {
    }

    public EnemyWaveConfig(
        float maxHealth,
        float moveSpeed,
        float attackRange,
        float attackCooldown,
        float attackDamage,
        float damageReductionPercent,
        float visualScaleMultiplier,
        float explosionRadius,
        float displacementWeight,
        IEnumerable<EnemyBulletTokenDropEntry> tokenDrops)
    {
        this.maxHealth = maxHealth;
        this.moveSpeed = moveSpeed;
        this.attackRange = attackRange;
        this.attackCooldown = attackCooldown;
        this.attackDamage = attackDamage;
        this.damageReductionPercent = damageReductionPercent;
        this.visualScaleMultiplier = visualScaleMultiplier;
        this.explosionRadius = explosionRadius;
        this.displacementWeight = displacementWeight;
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
        sanitized.damageReductionPercent = Mathf.Clamp01(sanitized.damageReductionPercent);
        sanitized.visualScaleMultiplier = SanitizePositiveValue(sanitized.visualScaleMultiplier, 1f);
        sanitized.explosionRadius = SanitizeValue(sanitized.explosionRadius, 0f);
        sanitized.displacementWeight = SanitizePositiveValue(sanitized.displacementWeight, 1f);
        sanitized.tokenDrops = clearTokenDrops ? new List<EnemyBulletTokenDropEntry>() : SanitizeTokenDrops(sanitized.tokenDrops);
        return sanitized;
    }

    internal static List<EnemyBulletTokenDropEntry> SanitizeTokenDrops(IEnumerable<EnemyBulletTokenDropEntry> tokenDrops)
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
            sanitizedEntry.dropCount = Mathf.Max(1, sanitizedEntry.dropCount);
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
    public List<EnemyBulletTokenDropEntry> tokenDrops;
    public bool isBossEncounter;
    public string bossDisplayNameOverride;
    public EnemyDefinition bossPhaseTwoDefinition;
    [Range(0f, 1f)] public float bossPhaseTransitionHealthRatio;

    public WaveEnemySpawnEntry(
        EnemyDefinition enemyDefinition,
        int spawnCount,
        IEnumerable<EnemyBulletTokenDropEntry> tokenDrops = null,
        bool isBossEncounter = false,
        string bossDisplayNameOverride = "",
        EnemyDefinition bossPhaseTwoDefinition = null,
        float bossPhaseTransitionHealthRatio = 0.5f)
    {
        this.enemyDefinition = enemyDefinition;
        this.spawnCount = spawnCount;
        this.tokenDrops = tokenDrops != null ? new List<EnemyBulletTokenDropEntry>(tokenDrops) : new List<EnemyBulletTokenDropEntry>();
        this.isBossEncounter = isBossEncounter;
        this.bossDisplayNameOverride = bossDisplayNameOverride ?? string.Empty;
        this.bossPhaseTwoDefinition = bossPhaseTwoDefinition;
        this.bossPhaseTransitionHealthRatio = bossPhaseTransitionHealthRatio;
    }

    public bool IsBossEncounter => isBossEncounter;
    public bool HasBossPhaseTransition => isBossEncounter && bossPhaseTwoDefinition != null;

    /// <summary>
    /// summary: 规整当前波次敌人条目里的基础配置与 Boss 展示文案。
    /// param: 无
    /// returns: 经过规范化后的波次敌人条目副本
    /// </summary>
    public WaveEnemySpawnEntry GetSanitized()
    {
        WaveEnemySpawnEntry sanitized = this;
        sanitized.spawnCount = Mathf.Max(0, sanitized.spawnCount);
        sanitized.tokenDrops = EnemyWaveConfig.SanitizeTokenDrops(sanitized.tokenDrops);
        sanitized.bossDisplayNameOverride = sanitized.bossDisplayNameOverride != null
            ? sanitized.bossDisplayNameOverride.Trim()
            : string.Empty;
        sanitized.bossPhaseTransitionHealthRatio = Mathf.Clamp01(sanitized.bossPhaseTransitionHealthRatio);
        if (sanitized.bossPhaseTransitionHealthRatio <= 0f)
        {
            sanitized.bossPhaseTransitionHealthRatio = 0.5f;
        }

        if (!sanitized.isBossEncounter)
        {
            sanitized.bossPhaseTwoDefinition = null;
        }

        return sanitized;
    }

    /// <summary>
    /// summary: 解析当前 Boss UI 应展示的名称；未覆写时回退到敌人定义名。
    /// param: 无
    /// returns: 当前条目建议展示的 Boss 名称
    /// </summary>
    public string ResolveBossDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(bossDisplayNameOverride))
        {
            return bossDisplayNameOverride.Trim();
        }

        if (enemyDefinition != null)
        {
            return enemyDefinition.DisplayName;
        }

        return string.Empty;
    }

    /// <summary>
    /// summary: 解析当前 Boss 条目应使用的生命阈值；非法值会回退到默认 50%。
    /// param: 无
    /// returns: 当前 Boss 阶段切换应使用的生命百分比阈值
    /// </summary>
    public float ResolveBossPhaseTransitionHealthRatio()
    {
        float resolvedRatio = Mathf.Clamp01(bossPhaseTransitionHealthRatio);
        return resolvedRatio > 0f ? resolvedRatio : 0.5f;
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
/// 为敌人技能执行器提供统一契约，供技能调度器按技能类型分发具体释放逻辑。
/// </summary>
public interface IEnemySkillCaster
{
    EnemySkillKind SkillKind { get; }

    /// <summary>
    /// summary: 尝试按给定技能槽配置执行一次技能释放。
    /// param: skillSlot 当前调度命中的技能槽配置
    /// returns: 技能实际成功释放时返回 true
    /// </summary>
    bool TryCastSkill(EnemyDefinition.EnemySkillSlotDefinition skillSlot);
}

/// <summary>
/// 为敌人运行时行为提供统一的 UI/暂停阻断判断，避免移动和攻击各自维护一套条件。
/// </summary>
public static class EnemyGameplayPauseGuard
{
    /// <summary>
    /// summary: 判断当前是否存在会暂停敌人行为的 UI 或游戏状态。
    /// param: 无
    /// returns: 当暂停菜单、背包、对话界面或显式 Paused 状态存在时返回 true
    /// </summary>
    public static bool ShouldSuspendEnemyActions()
    {
        return StatusController.HasStatus(StatusList.InBackPackStatus)
            || StatusController.HasStatus(StatusList.InHintStatus)
            || StatusController.HasStatus(StatusList.InUpgradeScreenStatus)
            || StatusController.HasStatus(StatusList.InPauseMenuStatus)
            || StatusController.HasStatus(StatusList.InSettlementScreenStatus)
            || StatusController.HasStatus(StatusList.InDialogStatus)
            || StatusController.HasStatus(StatusList.PausedStatus);
    }
}
