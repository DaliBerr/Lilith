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
        }
    }
}
