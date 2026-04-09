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
    public struct RangedBulletAttackDefinition
    {
        public CharBullet bulletPrefab;
        public List<PlaceableTokenData> formulaItems;
        public BulletTargetPolicy targetPolicy;

        /// <summary>
        /// summary: 修正远程词元攻击配置中的空列表与空元素，确保编译链路可稳定遍历。
        /// param: 无
        /// returns: 经过规范化后的远程词元攻击配置副本
        /// </summary>
        public RangedBulletAttackDefinition GetSanitized()
        {
            RangedBulletAttackDefinition sanitized = this;
            sanitized.formulaItems ??= new List<PlaceableTokenData>();
            sanitized.formulaItems.RemoveAll(item => item == null);
            return sanitized;
        }
    }

    [Serializable]
    public struct SummonAttackDefinition
    {
        public EnemyDefinition summonedEnemyDefinition;
        public EnemyWaveConfig summonedEnemyConfig;
        [Min(1)] public int summonCountPerCast;
        [Min(0f)] public float summonRadius;
        [Min(0)] public int maxAliveSummons;

        /// <summary>
        /// summary: 修正召唤攻击配置中的非法取值，并统一清空召唤物掉落表。
        /// param: 无
        /// returns: 经过规范化后的召唤攻击配置副本
        /// </summary>
        public SummonAttackDefinition GetSanitized()
        {
            SummonAttackDefinition sanitized = this;
            sanitized.summonedEnemyConfig = sanitized.summonedEnemyConfig.GetSanitized(clearTokenDrops: true);
            sanitized.summonCountPerCast = Mathf.Max(1, sanitized.summonCountPerCast);
            sanitized.summonRadius = Mathf.Max(0f, sanitized.summonRadius);
            sanitized.maxAliveSummons = Mathf.Max(0, sanitized.maxAliveSummons);
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
    [SerializeField] private EnemyVisualDefinition visual = new()
    {
        glyphText = string.Empty,
        glyphColor = Color.white,
        runeBaseTint = new Color(0.92f, 0.94f, 0.98f, 0.45f),
        groundShadowTint = new Color(0f, 0f, 0f, 0.28f),
    };
    [SerializeField] private RangedBulletAttackDefinition rangedBulletAttack = new()
    {
        targetPolicy = BulletTargetPolicy.PlayerOnly,
    };
    [SerializeField] private SummonAttackDefinition summonAttack = new()
    {
        summonCountPerCast = 1,
        summonRadius = 12f,
        maxAliveSummons = 3,
    };

    public string EnemyId => string.IsNullOrWhiteSpace(enemyId) ? name : enemyId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EnemyId : displayName.Trim();
    public EnemyDefinitionBinder RuntimePrefabBinder => runtimePrefab;
    public GameObject RuntimePrefab => runtimePrefab != null ? runtimePrefab.gameObject : null;
    public EnemyMovementKind MovementKind => movementKind;
    public EnemyAttackKind AttackKind => attackKind;
    public DashMovementDefinition DashMovement => dashMovement;
    public KeepDistanceMovementDefinition KeepDistanceMovement => keepDistanceMovement;
    public AggroOnHitMovementDefinition AggroOnHitMovement => aggroOnHitMovement;
    public EnemyVisualDefinition Visual => visual;
    public RangedBulletAttackDefinition RangedBulletAttack => rangedBulletAttack;
    public SummonAttackDefinition SummonAttack => summonAttack;

    private void OnValidate()
    {
        enemyId = enemyId != null ? enemyId.Trim() : string.Empty;
        displayName = displayName != null ? displayName.Trim() : string.Empty;
        dashMovement = dashMovement.GetSanitized();
        keepDistanceMovement = keepDistanceMovement.GetSanitized();
        aggroOnHitMovement = aggroOnHitMovement.GetSanitized();
        visual = visual.GetSanitized();
        rangedBulletAttack = rangedBulletAttack.GetSanitized();
        summonAttack = summonAttack.GetSanitized();
    }
}

public enum EnemyMovementKind
{
    None = 0,
    ChaseTarget = 1,
    ChaseThenDash = 2,
    KeepDistance = 3,
    AggroOnHit = 4,
}

public enum EnemyAttackKind
{
    None = 0,
    MeleeContact = 1,
    RangedBulletToken = 2,
    SummonEnemy = 3,
}
