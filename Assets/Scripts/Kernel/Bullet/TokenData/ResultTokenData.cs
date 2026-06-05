using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 描述一个结果词元如何在命中后结算额外效果。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Result Token", fileName = "ResultToken")]
    public sealed class ResultTokenData : BaseTokenData
    {
        [SerializeField] private AttackResultType resultType = AttackResultType.DirectDamage;
        [SerializeField] private bool acceptsNumericValue;
        [SerializeField] private SpellValueParameterKind valueParameterKind = SpellValueParameterKind.None;
        [SerializeField, Min(0f)] private float defaultExplosionRadius;
        [SerializeField, Range(0f, 1f)] private float explosionDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float defaultEffectRadius;
        [SerializeField, Min(0)] private int defaultTriggerCount;
        [SerializeField, Min(0f)] private float effectDuration;
        [SerializeField, Range(0f, 1f)] private float childDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float defaultEffectStrength = 1f;
        [SerializeField, Min(0f)] private float areaTickSeconds = 0.5f;
        [SerializeField, Min(0f)] private float areaDamageMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float shieldDuration = 6f;
        [SerializeField] private SpellStatusApplication[] statusApplications = Array.Empty<SpellStatusApplication>();

        public AttackResultType ResultType
        {
            get => resultType;
            set => resultType = value;
        }

        public bool AcceptsNumericValue
        {
            get => acceptsNumericValue;
            set => acceptsNumericValue = value;
        }

        public SpellValueParameterKind ValueParameterKind
        {
            get => ResolveValueParameterKind();
            set => valueParameterKind = value;
        }

        public SpellValueParameterKind ConfiguredValueParameterKind
        {
            get => valueParameterKind;
            set => valueParameterKind = value;
        }

        public float DefaultExplosionRadius
        {
            get => defaultExplosionRadius;
            set => defaultExplosionRadius = Mathf.Max(0f, value);
        }

        public float ExplosionDamageMultiplier
        {
            get => explosionDamageMultiplier;
            set => explosionDamageMultiplier = Mathf.Clamp01(value);
        }

        public float DefaultEffectRadius
        {
            get => defaultEffectRadius;
            set => defaultEffectRadius = Mathf.Max(0f, value);
        }

        public int DefaultTriggerCount
        {
            get => defaultTriggerCount;
            set => defaultTriggerCount = Mathf.Max(0, value);
        }

        public float EffectDuration
        {
            get => effectDuration;
            set => effectDuration = Mathf.Max(0f, value);
        }

        public float ChildDamageMultiplier
        {
            get => childDamageMultiplier;
            set => childDamageMultiplier = Mathf.Clamp01(value);
        }

        public float DefaultEffectStrength
        {
            get => defaultEffectStrength;
            set => defaultEffectStrength = Mathf.Max(0f, value);
        }

        public float AreaTickSeconds
        {
            get => areaTickSeconds;
            set => areaTickSeconds = Mathf.Max(0f, value);
        }

        public float AreaDamageMultiplier
        {
            get => areaDamageMultiplier;
            set => areaDamageMultiplier = Mathf.Max(0f, value);
        }

        public float ShieldDuration
        {
            get => shieldDuration;
            set => shieldDuration = Mathf.Max(0f, value);
        }

        public IReadOnlyList<SpellStatusApplication> StatusApplications => statusApplications;

        public void SetStatusApplications(params SpellStatusApplication[] applications)
        {
            statusApplications = SpellStatusApplicationUtility.Sanitize(applications);
        }

        /// <summary>
        /// summary: 依据当前结果词元配置创建一份命中后二级效果载荷。
        /// param: 无
        /// returns: 当前 result 对应的爆炸、分裂或控制效果配置
        /// </summary>
        public ResultEffectPayload CreateResultEffects()
        {
            return new ResultEffectPayload
            {
                explosionRadius = defaultExplosionRadius,
                explosionDamageMultiplier = explosionDamageMultiplier,
                explosionDelaySeconds = effectDuration,
                effectRadius = defaultEffectRadius,
                splitProjectileCount = defaultTriggerCount,
                splitDamageMultiplier = childDamageMultiplier,
                controlTriggerCount = defaultTriggerCount,
                controlDuration = effectDuration,
                healingMultiplier = 1f,
                effectDuration = effectDuration,
                effectStrength = defaultEffectStrength,
                areaTickSeconds = areaTickSeconds,
                areaDamageMultiplier = areaDamageMultiplier,
                shieldDuration = shieldDuration,
                statusApplications = SpellStatusApplicationUtility.Sanitize(statusApplications),
            }.GetSanitized();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Result);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Result);
            defaultExplosionRadius = Mathf.Max(0f, defaultExplosionRadius);
            explosionDamageMultiplier = Mathf.Clamp01(explosionDamageMultiplier);
            defaultEffectRadius = Mathf.Max(0f, defaultEffectRadius);
            defaultTriggerCount = Mathf.Max(0, defaultTriggerCount);
            effectDuration = Mathf.Max(0f, effectDuration);
            childDamageMultiplier = Mathf.Clamp01(childDamageMultiplier);
            defaultEffectStrength = Mathf.Max(0f, defaultEffectStrength);
            areaTickSeconds = Mathf.Max(0f, areaTickSeconds);
            areaDamageMultiplier = Mathf.Max(0f, areaDamageMultiplier);
            shieldDuration = Mathf.Max(0f, shieldDuration);
            statusApplications = SpellStatusApplicationUtility.Sanitize(statusApplications);
        }

        private SpellValueParameterKind ResolveValueParameterKind()
        {
            if (!acceptsNumericValue)
            {
                return SpellValueParameterKind.None;
            }

            if (valueParameterKind != SpellValueParameterKind.None)
            {
                return valueParameterKind;
            }

            return resultType switch
            {
                AttackResultType.Split => SpellValueParameterKind.Count,
                AttackResultType.StatusEffect => SpellValueParameterKind.Count,
                AttackResultType.Drain => SpellValueParameterKind.Strength,
                AttackResultType.Shield => SpellValueParameterKind.Strength,
                AttackResultType.Push => SpellValueParameterKind.Strength,
                AttackResultType.Pull => SpellValueParameterKind.Strength,
                AttackResultType.Leave => SpellValueParameterKind.Duration,
                _ => SpellValueParameterKind.None,
            };
        }
    }
}
