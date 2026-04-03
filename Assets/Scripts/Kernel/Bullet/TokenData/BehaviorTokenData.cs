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
        [SerializeField, Min(1)] private int defaultProjectileCount = 1;
        [SerializeField, Min(0f)] private float spreadAngleStep = 10f;

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
        }
    }
}
