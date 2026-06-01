using UnityEngine;

namespace Kernel.Bullet
{
    public enum SpellValueParameterKind
    {
        None = 0,
        Count = 1,
        Radius = 2,
        Duration = 3,
    }

    /// <summary>
    /// 描述一个数值词元提供的单个数值载荷。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Value Token", fileName = "ValueToken")]
    public sealed class ValueTokenData : BaseTokenData
    {
        [SerializeField] private float numericValue = 1f;

        public float NumericValue
        {
            get => numericValue;
            set => numericValue = value;
        }

        /// <summary>
        /// summary: 以最接近的整数形式返回当前数值词元，供发射数量等整型语义使用。
        /// param: 无
        /// returns: 四舍五入后的整数数值
        /// </summary>
        public int GetRoundedIntValue()
        {
            return Mathf.RoundToInt(numericValue);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Value);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetTokenType(TokenType.Value);
        }
    }
}
