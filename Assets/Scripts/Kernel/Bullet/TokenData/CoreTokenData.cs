using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 描述一个核心词元如何建立攻击的基础模板。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Core Token", fileName = "CoreToken")]
    public sealed class CoreTokenData : BaseTokenData
    {
        [SerializeField] private AttackCoreType coreType = AttackCoreType.Fire;
        [SerializeField] private AttackValueType defaultValueType = AttackValueType.oneShot;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(1)] private int projectileLife = 1;
        [SerializeField, Min(1)] private int impactLifeCost = 1;
        [SerializeField, Min(0f)] private float projectileSpeed = 320f;
        [SerializeField, Min(0f)] private float maxLifetime = 2f;
        [SerializeField, Min(0f)] private float maxTravelDistance = 512f;
        [SerializeField] private LayerMask impactMask = Physics.DefaultRaycastLayers;
        [SerializeField] private string armoredEnemyId = string.Empty;
        [SerializeField, Min(1f)] private float armoredDamageMultiplier = 1f;
        [SerializeField, Min(0)] private int burnTriggerCount;
        [SerializeField, Min(0f)] private float burnDamagePerSecond;
        [SerializeField, Min(0f)] private float burnDuration;
        [SerializeField, Range(0f, 1f)] private float slowPercent;
        [SerializeField, Min(0f)] private float slowDuration;
        [SerializeField, Min(0)] private int thunderChainTargetCount;
        [SerializeField, Min(0f)] private float thunderChainRadius;
        [SerializeField, Min(0f)] private float thunderChainDamage;

        public AttackCoreType CoreType
        {
            get => coreType;
            set => coreType = value;
        }

        public AttackValueType DefaultValueType
        {
            get => defaultValueType;
            set => defaultValueType = value;
        }

        public float Damage
        {
            get => damage;
            set => damage = Mathf.Max(0f, value);
        }

        public int ProjectileLife
        {
            get => projectileLife;
            set => projectileLife = Mathf.Max(1, value);
        }

        public int ImpactLifeCost
        {
            get => impactLifeCost;
            set => impactLifeCost = Mathf.Max(1, value);
        }

        public float ProjectileSpeed
        {
            get => projectileSpeed;
            set => projectileSpeed = Mathf.Max(0f, value);
        }

        public float MaxLifetime
        {
            get => maxLifetime;
            set => maxLifetime = Mathf.Max(0f, value);
        }

        public float MaxTravelDistance
        {
            get => maxTravelDistance;
            set => maxTravelDistance = Mathf.Max(0f, value);
        }

        public LayerMask ImpactMask
        {
            get => impactMask;
            set => impactMask = value;
        }

        public string ArmoredEnemyId
        {
            get => armoredEnemyId;
            set => armoredEnemyId = value != null ? value.Trim() : string.Empty;
        }

        public float ArmoredDamageMultiplier
        {
            get => armoredDamageMultiplier;
            set => armoredDamageMultiplier = Mathf.Max(1f, value);
        }

        public int BurnTriggerCount
        {
            get => burnTriggerCount;
            set => burnTriggerCount = Mathf.Max(0, value);
        }

        public float BurnDamagePerSecond
        {
            get => burnDamagePerSecond;
            set => burnDamagePerSecond = Mathf.Max(0f, value);
        }

        public float BurnDuration
        {
            get => burnDuration;
            set => burnDuration = Mathf.Max(0f, value);
        }

        public float SlowPercent
        {
            get => slowPercent;
            set => slowPercent = Mathf.Clamp01(value);
        }

        public float SlowDuration
        {
            get => slowDuration;
            set => slowDuration = Mathf.Max(0f, value);
        }

        public int ThunderChainTargetCount
        {
            get => thunderChainTargetCount;
            set => thunderChainTargetCount = Mathf.Max(0, value);
        }

        public float ThunderChainRadius
        {
            get => thunderChainRadius;
            set => thunderChainRadius = Mathf.Max(0f, value);
        }

        public float ThunderChainDamage
        {
            get => thunderChainDamage;
            set => thunderChainDamage = Mathf.Max(0f, value);
        }

        /// <summary>
        /// summary: 依据当前核心词元配置创建一份攻击模板。
        /// param: 无
        /// returns: 可直接继续叠加行为与结果语义的基础攻击配置
        /// </summary>
        public AttackSpec CreateBaseAttackSpec()
        {
            return new AttackSpec
            {
                coreType = coreType,
                behaviorType = AttackBehaviorType.Straight,
                valueType = defaultValueType,
                resultType = AttackResultType.DirectDamage,
                damage = damage,
                projectileCount = 1,
                bounceCount = 0,
                chainCount = 0,
                pierceCount = 0,
                projectileLife = projectileLife,
                impactLifeCost = impactLifeCost,
                projectileSpeed = projectileSpeed,
                maxLifetime = maxLifetime,
                maxTravelDistance = maxTravelDistance,
                impactMask = impactMask,
            }.GetSanitized();
        }

        /// <summary>
        /// summary: 依据当前核心词元配置创建一份命中后二级效果载荷。
        /// param: 无
        /// returns: 当前 core 对应的 burn/slow/thunder/armor bonus 配置
        /// </summary>
        public CoreEffectPayload CreateCoreEffects()
        {
            return new CoreEffectPayload
            {
                armoredEnemyId = armoredEnemyId,
                armoredDamageMultiplier = armoredDamageMultiplier,
                burnTriggerCount = burnTriggerCount,
                burnDamagePerSecond = burnDamagePerSecond,
                burnDuration = burnDuration,
                slowPercent = slowPercent,
                slowDuration = slowDuration,
                thunderChainTargetCount = thunderChainTargetCount,
                thunderChainRadius = thunderChainRadius,
                thunderChainDamage = thunderChainDamage,
            }.GetSanitized();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Core);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Core);
            damage = Mathf.Max(0f, damage);
            projectileLife = Mathf.Max(1, projectileLife);
            impactLifeCost = Mathf.Max(1, impactLifeCost);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
            maxLifetime = Mathf.Max(0f, maxLifetime);
            maxTravelDistance = Mathf.Max(0f, maxTravelDistance);
            armoredEnemyId = armoredEnemyId != null ? armoredEnemyId.Trim() : string.Empty;
            armoredDamageMultiplier = Mathf.Max(1f, armoredDamageMultiplier);
            burnTriggerCount = Mathf.Max(0, burnTriggerCount);
            burnDamagePerSecond = Mathf.Max(0f, burnDamagePerSecond);
            burnDuration = Mathf.Max(0f, burnDuration);
            slowPercent = Mathf.Clamp01(slowPercent);
            slowDuration = Mathf.Max(0f, slowDuration);
            thunderChainTargetCount = Mathf.Max(0, thunderChainTargetCount);
            thunderChainRadius = Mathf.Max(0f, thunderChainRadius);
            thunderChainDamage = Mathf.Max(0f, thunderChainDamage);
        }
    }
}
