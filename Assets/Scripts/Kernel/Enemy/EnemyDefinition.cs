using System;
using System.Collections.Generic;
using Kernel.Bullet;
using UnityEngine;

/// <summary>
/// 定义一种敌人的 prefab、行为开关和视觉表现。
/// </summary>
[CreateAssetMenu(menuName = "Lilith/Enemy/Enemy Definition", fileName = "EnemyDefinition")]
public sealed class EnemyDefinition : ScriptableObject
{
    private const float WaveGrowthPerCompletedWave = 0.04f;

    [Serializable]
    public struct DashMovementDefinition
    {
        [Min(0f)] public float triggerDistance;
        [Min(0f)] public float windupSeconds;
        [Min(1f)] public float dashSpeedMultiplier;
        [Min(0f)] public float dashDurationSeconds;
        [Min(0f)] public float dashCooldownSeconds;

        /// <summary>
        /// summary: 修正冲刺追踪配置中的非法取值，保证运行时状态机拿到可用参数。
        /// param: 无
        /// returns: 经过规范化后的冲刺追踪配置副本
        /// </summary>
        public DashMovementDefinition GetSanitized()
        {
            DashMovementDefinition sanitized = this;
            sanitized.triggerDistance = Mathf.Max(0f, sanitized.triggerDistance);
            sanitized.windupSeconds = Mathf.Max(0f, sanitized.windupSeconds);
            sanitized.dashSpeedMultiplier = Mathf.Max(1f, sanitized.dashSpeedMultiplier);
            sanitized.dashDurationSeconds = Mathf.Max(0f, sanitized.dashDurationSeconds);
            sanitized.dashCooldownSeconds = Mathf.Max(0f, sanitized.dashCooldownSeconds);
            return sanitized;
        }
    }

    [Serializable]
    public struct KeepDistanceMovementDefinition
    {
        [Min(0f)] public float preferredDistance;
        [Min(0f)] public float distanceTolerance;

        /// <summary>
        /// summary: 修正保持距离配置中的非法取值，避免负距离进入运行时。
        /// param: 无
        /// returns: 经过规范化后的保持距离配置副本
        /// </summary>
        public KeepDistanceMovementDefinition GetSanitized()
        {
            KeepDistanceMovementDefinition sanitized = this;
            sanitized.preferredDistance = Mathf.Max(0f, sanitized.preferredDistance);
            sanitized.distanceTolerance = Mathf.Max(0f, sanitized.distanceTolerance);
            return sanitized;
        }
    }

    [Serializable]
    public struct AggroOnHitMovementDefinition
    {
        [Min(1f)] public float aggroSpeedMultiplier;

        /// <summary>
        /// summary: 修正受击仇恨追击配置中的非法取值，确保加速倍率至少为 1。
        /// param: 无
        /// returns: 经过规范化后的受击追击配置副本
        /// </summary>
        public AggroOnHitMovementDefinition GetSanitized()
        {
            AggroOnHitMovementDefinition sanitized = this;
            sanitized.aggroSpeedMultiplier = Mathf.Max(1f, sanitized.aggroSpeedMultiplier);
            return sanitized;
        }
    }

    [Serializable]
    public struct OrbitTargetMovementDefinition
    {
        [Min(0f)] public float orbitRadius;
        [Min(0f)] public float orbitRadiusTolerance;
        [Min(0f)] public float orbitSpeedMultiplier;
        public bool clockwise;

        /// <summary>
        /// summary: 修正环绕目标配置中的非法取值，保证运行时调度器拿到稳定参数。
        /// param: 无
        /// returns: 经过规范化后的环绕目标配置副本
        /// </summary>
        public OrbitTargetMovementDefinition GetSanitized()
        {
            OrbitTargetMovementDefinition sanitized = this;
            sanitized.orbitRadius = Mathf.Max(0f, sanitized.orbitRadius);
            sanitized.orbitRadiusTolerance = Mathf.Max(0f, sanitized.orbitRadiusTolerance);
            sanitized.orbitSpeedMultiplier = Mathf.Max(0f, sanitized.orbitSpeedMultiplier);
            return sanitized;
        }
    }

    [Serializable]
    public struct EnemyVisualDefinition
    {
        public string glyphText;
        public Color glyphColor;
        public Sprite runeBaseSprite;
        public Color runeBaseTint;
        public Sprite groundShadowSprite;
        public Color groundShadowTint;

        /// <summary>
        /// summary: 规范化当前视觉定义中的文本与颜色字段。
        /// param: 无
        /// returns: 经过规范化后的视觉定义副本
        /// </summary>
        public EnemyVisualDefinition GetSanitized()
        {
            EnemyVisualDefinition sanitized = this;
            sanitized.glyphText ??= string.Empty;
            sanitized.glyphColor.a = Mathf.Clamp01(sanitized.glyphColor.a);
            sanitized.runeBaseTint.a = Mathf.Clamp01(sanitized.runeBaseTint.a);
            sanitized.groundShadowTint.a = Mathf.Clamp01(sanitized.groundShadowTint.a);
            return sanitized;
        }
    }

    [Serializable]
    public struct EnemyCombatDefinition
    {
        [Min(0f)] public float maxHealth;
        [Min(0f)] public float moveSpeed;
        [Min(0f)] public float attackRange;
        [Min(0f)] public float attackCooldown;
        [Min(0f)] public float attackDamage;
        [Range(0f, 1f)] public float damageReductionPercent;
        [Min(0f)] public float visualScaleMultiplier;

        /// <summary>
        /// summary: 修正敌人基础战斗数值中的非法取值，确保运行时解算拿到稳定配置。
        /// param: 无
        /// returns: 经过规范化后的基础战斗数值副本
        /// </summary>
        public EnemyCombatDefinition GetSanitized()
        {
            EnemyCombatDefinition sanitized = this;
            sanitized.maxHealth = SanitizePositiveValue(sanitized.maxHealth, 1f);
            sanitized.moveSpeed = SanitizeValue(sanitized.moveSpeed, 0f);
            sanitized.attackRange = SanitizeValue(sanitized.attackRange, 0f);
            sanitized.attackCooldown = SanitizeValue(sanitized.attackCooldown, 0f);
            sanitized.attackDamage = SanitizeValue(sanitized.attackDamage, 0f);
            sanitized.damageReductionPercent = Mathf.Clamp01(sanitized.damageReductionPercent);
            sanitized.visualScaleMultiplier = SanitizePositiveValue(sanitized.visualScaleMultiplier, 1f);
            return sanitized;
        }
    }

    [Serializable]
    public struct RangedBulletAttackDefinition
    {
        public CharBullet bulletPrefab;
        public List<PlaceableTokenData> formulaItems;
        public BulletTargetPolicy targetPolicy;

        /// <summary>
        /// summary: 修正远程词元攻击配置中的空列表，并按需清理空元素。
        /// param: removeNullFormulaItems 为 true 时会移除 formulaItems 中的空元素
        /// returns: 经过规范化后的远程词元攻击配置副本
        /// </summary>
        public RangedBulletAttackDefinition GetSanitized(bool removeNullFormulaItems = true)
        {
            RangedBulletAttackDefinition sanitized = this;
            sanitized.formulaItems ??= new List<PlaceableTokenData>();
            if (removeNullFormulaItems)
            {
                sanitized.formulaItems.RemoveAll(item => item == null);
            }

            return sanitized;
        }
    }

    [Serializable]
    public struct ExplosiveAttackDefinition
    {
        [Min(0f)] public float explosionRadius;
        [Min(0f)] public float windupSeconds;

        /// <summary>
        /// summary: 修正自爆攻击配置中的非法取值，保证运行时自爆行为可以安全执行。
        /// param: 无
        /// returns: 经过规范化后的自爆攻击配置副本
        /// </summary>
        public ExplosiveAttackDefinition GetSanitized()
        {
            ExplosiveAttackDefinition sanitized = this;
            sanitized.explosionRadius = SanitizeValue(sanitized.explosionRadius, 0f);
            sanitized.windupSeconds = SanitizeValue(sanitized.windupSeconds, 0f);
            return sanitized;
        }
    }

    [Serializable]
    public struct SummonSkillDefinition
    {
        public EnemyDefinition summonedEnemyDefinition;
        [Min(1)] public int minSummonCountPerCast;
        [Min(1)] public int maxSummonCountPerCast;
        [Min(0f)] public float summonRadius;
        [Min(0)] public int maxAliveSummons;

        /// <summary>
        /// summary: 修正召唤技能配置中的非法取值，并统一约束单次召唤数量区间。
        /// param: 无
        /// returns: 经过规范化后的召唤技能配置副本
        /// </summary>
        public SummonSkillDefinition GetSanitized()
        {
            SummonSkillDefinition sanitized = this;
            sanitized.minSummonCountPerCast = Mathf.Max(1, sanitized.minSummonCountPerCast);
            sanitized.maxSummonCountPerCast = Mathf.Max(sanitized.minSummonCountPerCast, sanitized.maxSummonCountPerCast);
            sanitized.summonRadius = Mathf.Max(0f, sanitized.summonRadius);
            sanitized.maxAliveSummons = Mathf.Max(0, sanitized.maxAliveSummons);
            return sanitized;
        }
    }

    [Serializable]
    public struct EnemySkillSlotDefinition
    {
        public EnemySkillKind skillKind;
        [Min(0f)] public float cooldownSeconds;
        [Min(0f)] public float castRange;
        public SummonSkillDefinition summonSkill;

        /// <summary>
        /// summary: 修正单个技能槽中的非法值，保证运行时调度器拿到稳定配置。
        /// param: 无
        /// returns: 经过规范化后的技能槽配置副本
        /// </summary>
        public EnemySkillSlotDefinition GetSanitized()
        {
            EnemySkillSlotDefinition sanitized = this;
            sanitized.cooldownSeconds = Mathf.Max(0f, sanitized.cooldownSeconds);
            sanitized.castRange = Mathf.Max(0f, sanitized.castRange);
            sanitized.summonSkill = sanitized.summonSkill.GetSanitized();
            return sanitized;
        }

        /// <summary>
        /// summary: 优先返回技能槽自己的施法距离；未填写时回退到敌人通用攻击距离。
        /// param: fallbackCastRange 当前敌人可复用的通用攻击距离
        /// returns: 当前技能槽应使用的施法距离
        /// </summary>
        public float ResolveCastRange(float fallbackCastRange)
        {
            return castRange > 0f ? castRange : Mathf.Max(0f, fallbackCastRange);
        }
    }

    [Serializable]
    public struct EnemySkillCastingDefinition
    {
        [Min(1)] public int maxSkillCastsPerTick;

        /// <summary>
        /// summary: 修正技能调度配置中的非法值，保证每次调度至少允许尝试一个技能槽。
        /// param: 无
        /// returns: 经过规范化后的技能调度配置副本
        /// </summary>
        public EnemySkillCastingDefinition GetSanitized()
        {
            EnemySkillCastingDefinition sanitized = this;
            sanitized.maxSkillCastsPerTick = Mathf.Max(1, sanitized.maxSkillCastsPerTick);
            return sanitized;
        }
    }

    [SerializeField] private string enemyId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private EnemyDefinitionBinder runtimePrefab;
    [SerializeField] private EnemyMovementKind movementKind = EnemyMovementKind.ChaseTarget;
    [SerializeField] private EnemyAttackKind attackKind = EnemyAttackKind.MeleeContact;
    [SerializeField] private DashMovementDefinition dashMovement = new()
    {
        triggerDistance = 24f,
        windupSeconds = 0.25f,
        dashSpeedMultiplier = 3f,
        dashDurationSeconds = 0.3f,
        dashCooldownSeconds = 1f,
    };
    [SerializeField] private KeepDistanceMovementDefinition keepDistanceMovement = new()
    {
        preferredDistance = 24f,
        distanceTolerance = 4f,
    };
    [SerializeField] private AggroOnHitMovementDefinition aggroOnHitMovement = new()
    {
        aggroSpeedMultiplier = 1.5f,
    };
    [SerializeField] private OrbitTargetMovementDefinition orbitTargetMovement = new()
    {
        orbitRadius = 12f,
        orbitRadiusTolerance = 2f,
        orbitSpeedMultiplier = 1f,
    };
    [SerializeField] private EnemyVisualDefinition visual = new()
    {
        glyphText = string.Empty,
        glyphColor = Color.white,
        runeBaseTint = new Color(0.92f, 0.94f, 0.98f, 0.45f),
        groundShadowTint = new Color(0f, 0f, 0f, 0.28f),
    };
    [SerializeField] private EnemyCombatDefinition combat = new()
    {
        maxHealth = 20f,
        moveSpeed = 36f,
        attackRange = 16f,
        attackCooldown = 1f,
        attackDamage = 1f,
        damageReductionPercent = 0f,
        visualScaleMultiplier = 1f,
    };
    [SerializeField] private RangedBulletAttackDefinition rangedBulletAttack = new()
    {
        targetPolicy = BulletTargetPolicy.PlayerOnly,
    };
    [SerializeField] private ExplosiveAttackDefinition explosiveAttack = new()
    {
        explosionRadius = 18f,
        windupSeconds = 0.8f,
    };
    [SerializeField] private EnemySkillCastingDefinition skillCasting = new()
    {
        maxSkillCastsPerTick = 1,
    };
    [SerializeField] private List<EnemySkillSlotDefinition> skillSlots = new();

    public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? name : enemyId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EnemyId : displayName.Trim();
    public EnemyDefinitionBinder RuntimePrefabBinder => runtimePrefab;
    public GameObject RuntimePrefab => runtimePrefab != null ? runtimePrefab.gameObject : null;
    public EnemyMovementKind MovementKind => movementKind;
    public EnemyAttackKind AttackKind => attackKind;
    public DashMovementDefinition DashMovement => dashMovement;
    public KeepDistanceMovementDefinition KeepDistanceMovement => keepDistanceMovement;
    public AggroOnHitMovementDefinition AggroOnHitMovement => aggroOnHitMovement;
    public OrbitTargetMovementDefinition OrbitTargetMovement => orbitTargetMovement;
    public EnemyVisualDefinition Visual => visual;
    public EnemyCombatDefinition Combat => combat.GetSanitized();
    public RangedBulletAttackDefinition RangedBulletAttack => rangedBulletAttack;
    public ExplosiveAttackDefinition ExplosiveAttack => explosiveAttack.GetSanitized();
    public EnemySkillCastingDefinition SkillCasting => skillCasting.GetSanitized();
    public IReadOnlyList<EnemySkillSlotDefinition> SkillSlots => ResolveSkillSlots();

    private void OnValidate()
    {
        enemyId = enemyId != null ? enemyId.Trim() : string.Empty;
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        dashMovement = dashMovement.GetSanitized();
        keepDistanceMovement = keepDistanceMovement.GetSanitized();
        aggroOnHitMovement = aggroOnHitMovement.GetSanitized();
        orbitTargetMovement = orbitTargetMovement.GetSanitized();
        visual = visual.GetSanitized();
        combat = combat.GetSanitized();
        rangedBulletAttack = rangedBulletAttack.GetSanitized(removeNullFormulaItems: false);
        explosiveAttack = explosiveAttack.GetSanitized();
        skillCasting = skillCasting.GetSanitized();
        skillSlots ??= new List<EnemySkillSlotDefinition>();
        for (int i = 0; i < skillSlots.Count; i++)
        {
            skillSlots[i] = skillSlots[i].GetSanitized();
        }
    }

    /// <summary>
    /// summary: 把定义资产里的基础战斗数值按已清波次数解算成当前战斗应使用的运行时配置。
    /// param: completedWaveCount 当前战斗里已经清空的波次数量
    /// param: tokenDrops 当前波次条目额外声明的掉落表
    /// returns: 当前定义在本次战斗层级下的运行时敌人数值
    /// </summary>
    public EnemyWaveConfig ResolveRuntimeConfig(int completedWaveCount, IReadOnlyList<EnemyBulletTokenDropEntry> tokenDrops = null)
    {
        EnemyCombatDefinition sanitizedCombat = Combat;
        ExplosiveAttackDefinition sanitizedExplosiveAttack = ExplosiveAttack;
        int resolvedCompletedWaveCount = Mathf.Max(0, completedWaveCount);
        float growthMultiplier = 1f + (resolvedCompletedWaveCount * WaveGrowthPerCompletedWave);
        List<EnemyBulletTokenDropEntry> resolvedTokenDrops = EnemyWaveConfig.SanitizeTokenDrops(tokenDrops);
        return new EnemyWaveConfig(
            sanitizedCombat.maxHealth * growthMultiplier,
            sanitizedCombat.moveSpeed * growthMultiplier,
            sanitizedCombat.attackRange * growthMultiplier,
            sanitizedCombat.attackCooldown,
            sanitizedCombat.attackDamage * growthMultiplier,
            sanitizedCombat.damageReductionPercent,
            sanitizedCombat.visualScaleMultiplier,
            sanitizedExplosiveAttack.explosionRadius * growthMultiplier,
            resolvedTokenDrops).GetSanitized();
    }

    /// <summary>
    /// summary: 解析当前定义应暴露给运行时的技能槽列表。
    /// param: 无
    /// returns: 当前定义的可执行技能槽集合
    /// </summary>
    private IReadOnlyList<EnemySkillSlotDefinition> ResolveSkillSlots()
    {
        return skillSlots != null ? skillSlots : Array.Empty<EnemySkillSlotDefinition>();
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

public enum EnemyMovementKind
{
    None = 0,
    ChaseTarget = 1,
    ChaseThenDash = 2,
    KeepDistance = 3,
    AggroOnHit = 4,
    OrbitTarget = 5,
}

public enum EnemyAttackKind
{
    None = 0,
    MeleeContact = 1,
    RangedBulletToken = 2,
    ProximityExplosion = 3,
}

public enum EnemySkillKind
{
    None = 0,
    SummonEnemy = 1,
}
