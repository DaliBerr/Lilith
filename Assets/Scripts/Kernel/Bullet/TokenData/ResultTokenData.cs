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
        [SerializeField, Min(0f)] private float defaultExplosionRadius;

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

        public float DefaultExplosionRadius
        {
            get => defaultExplosionRadius;
            set => defaultExplosionRadius = Mathf.Max(0f, value);
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
        }
    }
}
