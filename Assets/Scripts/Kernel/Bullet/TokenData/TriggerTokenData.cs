using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 声明后续 payload 在哪类运行时事件上执行。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Trigger Token", fileName = "TriggerToken")]
    public sealed class TriggerTokenData : BaseTokenData
    {
        [SerializeField] private SpellTriggerType triggerType = SpellTriggerType.OnHit;
        [SerializeField] private SpellTriggerParameterKind parameterKind = SpellTriggerParameterKind.None;
        [SerializeField, Min(0f)] private float defaultParameterValue = 1f;

        public SpellTriggerType TriggerType
        {
            get => triggerType;
            set => triggerType = value == SpellTriggerType.None ? SpellTriggerType.OnHit : value;
        }

        public SpellTriggerParameterKind ParameterKind
        {
            get => ResolveParameterKind();
            set => parameterKind = value;
        }

        public SpellTriggerParameterKind ConfiguredParameterKind
        {
            get => parameterKind;
            set => parameterKind = value;
        }

        public float DefaultParameterValue
        {
            get => ResolveDefaultParameterValue();
            set => defaultParameterValue = Mathf.Max(0f, value);
        }

        public bool ConsumesValueAsTriggerParameter => ParameterKind != SpellTriggerParameterKind.None;

        public SpellTriggerPointKind TriggerPointKind => triggerType switch
        {
            SpellTriggerType.OnTimer => SpellTriggerPointKind.ProjectilePosition,
            SpellTriggerType.OnExpire => SpellTriggerPointKind.ExpirePoint,
            SpellTriggerType.OnKill => SpellTriggerPointKind.DeathTargetPosition,
            SpellTriggerType.OnDistance => SpellTriggerPointKind.ProjectilePosition,
            SpellTriggerType.OnProximity => SpellTriggerPointKind.ProjectilePosition,
            _ => SpellTriggerPointKind.ImpactPoint,
        };

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Trigger);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (triggerType == SpellTriggerType.None)
            {
                triggerType = SpellTriggerType.OnHit;
            }

            SetTokenType(TokenType.Trigger);
            defaultParameterValue = Mathf.Max(0f, defaultParameterValue);
        }

        private SpellTriggerParameterKind ResolveParameterKind()
        {
            if (parameterKind != SpellTriggerParameterKind.None)
            {
                return parameterKind;
            }

            return triggerType switch
            {
                SpellTriggerType.OnTimer => SpellTriggerParameterKind.TimeSeconds,
                SpellTriggerType.OnDistance => SpellTriggerParameterKind.Distance,
                SpellTriggerType.OnProximity => SpellTriggerParameterKind.Radius,
                _ => SpellTriggerParameterKind.None,
            };
        }

        private float ResolveDefaultParameterValue()
        {
            if (parameterKind == SpellTriggerParameterKind.None &&
                ResolveParameterKind() == SpellTriggerParameterKind.None)
            {
                return 0f;
            }

            return Mathf.Max(0.01f, defaultParameterValue);
        }
    }
}
