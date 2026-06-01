using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 描述一个行为词元如何改变投射物的发射方式。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Behavior Token", fileName = "BehaviorToken")]
    public sealed class BehaviorTokenData : BaseTokenData
    {
        [SerializeField] private AttackBehaviorType behaviorType = AttackBehaviorType.Straight;
        [SerializeField] private bool acceptsNumericValue;
        [SerializeField] private SpellValueParameterKind valueParameterKind = SpellValueParameterKind.None;
        [SerializeField, Min(1)] private int defaultProjectileCount = 1;
        [SerializeField, Min(0f)] private float spreadAngleStep = 10f;
        [SerializeField, Min(0f)] private float projectileDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float pierceLifetimeDistanceScalePerCount = 0.2f;

        public AttackBehaviorType BehaviorType
        {
            get => behaviorType;
            set => behaviorType = value;
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

        public int DefaultProjectileCount
        {
            get => defaultProjectileCount;
            set => defaultProjectileCount = Mathf.Max(1, value);
        }

        public float SpreadAngleStep
        {
            get => spreadAngleStep;
            set => spreadAngleStep = Mathf.Max(0f, value);
        }

        public float ProjectileDamageMultiplier
        {
            get => projectileDamageMultiplier;
            set => projectileDamageMultiplier = Mathf.Max(0f, value);
        }

        public float PierceLifetimeDistanceScalePerCount
        {
            get => pierceLifetimeDistanceScalePerCount;
            set => pierceLifetimeDistanceScalePerCount = Mathf.Max(0f, value);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Behavior);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Behavior);
            defaultProjectileCount = Mathf.Max(1, defaultProjectileCount);
            spreadAngleStep = Mathf.Max(0f, spreadAngleStep);
            projectileDamageMultiplier = Mathf.Max(0f, projectileDamageMultiplier);
            pierceLifetimeDistanceScalePerCount = Mathf.Max(0f, pierceLifetimeDistanceScalePerCount);
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

            return behaviorType == AttackBehaviorType.Spread ||
                   behaviorType == AttackBehaviorType.Bounce ||
                   behaviorType == AttackBehaviorType.Pierce
                ? SpellValueParameterKind.Count
                : SpellValueParameterKind.None;
        }
    }
}
