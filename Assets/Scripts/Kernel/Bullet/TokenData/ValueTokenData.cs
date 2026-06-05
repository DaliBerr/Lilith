using UnityEngine;

namespace Kernel.Bullet
{
    public enum SpellValueParameterKind
    {
        None = 0,
        Count = 1,
        Radius = 2,
        Duration = 3,
        Strength = 4,
        TriggerParameter = 5,
    }

    public enum SpellValueMode
    {
        Number = 0,
        Multiplier = 1,
        ScalePreset = 2,
    }

    public enum SpellValueScalePreset
    {
        None = 0,
        Small = 1,
        Large = 2,
        Zero = 3,
    }

    /// <summary>
    /// 描述一个数值词元提供的单个数值载荷。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Value Token", fileName = "ValueToken")]
    public sealed class ValueTokenData : BaseTokenData
    {
        [SerializeField] private float numericValue = 1f;
        [SerializeField] private SpellValueMode valueMode = SpellValueMode.Number;
        [SerializeField] private SpellValueScalePreset scalePreset = SpellValueScalePreset.None;

        public float NumericValue
        {
            get => numericValue;
            set => numericValue = value;
        }

        public SpellValueMode ValueMode
        {
            get => valueMode;
            set => valueMode = value;
        }

        public SpellValueScalePreset ScalePreset
        {
            get => scalePreset;
            set => scalePreset = value;
        }

        public float ResolveNumericValue(SpellValueParameterKind parameterKind, float currentValue = 0f, bool allowZero = false)
        {
            float resolvedValue = valueMode switch
            {
                SpellValueMode.Multiplier => ResolveMultiplierValue(currentValue),
                SpellValueMode.ScalePreset => ResolveScalePresetValue(parameterKind, currentValue, allowZero),
                _ => numericValue,
            };

            if (!allowZero && resolvedValue <= 0f)
            {
                return parameterKind == SpellValueParameterKind.Count ? 1f : Mathf.Max(0f, resolvedValue);
            }

            return Mathf.Max(0f, resolvedValue);
        }

        public int ResolveCountValue(int currentValue = 0)
        {
            float numericCount = ResolveNumericValue(SpellValueParameterKind.Count, currentValue, allowZero: false);
            return Mathf.Max(1, Mathf.RoundToInt(numericCount));
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

        public int GetRoundedIntValue(SpellValueParameterKind parameterKind, int currentValue = 0)
        {
            return Mathf.RoundToInt(ResolveNumericValue(parameterKind, currentValue, allowZero: false));
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

        private float ResolveMultiplierValue(float currentValue)
        {
            float multiplier = Mathf.Approximately(numericValue, 0f) ? 1f : numericValue;
            return currentValue > 0f ? currentValue * multiplier : multiplier;
        }

        private float ResolveScalePresetValue(SpellValueParameterKind parameterKind, float currentValue, bool allowZero)
        {
            return scalePreset switch
            {
                SpellValueScalePreset.Small => parameterKind == SpellValueParameterKind.Count ? 1f : 0.5f,
                SpellValueScalePreset.Large => ResolveLargePresetValue(parameterKind, currentValue),
                SpellValueScalePreset.Zero => allowZero ? 0f : 1f,
                _ => numericValue,
            };
        }

        private static float ResolveLargePresetValue(SpellValueParameterKind parameterKind, float currentValue)
        {
            return parameterKind switch
            {
                SpellValueParameterKind.Count => currentValue > 0f ? currentValue + 2f : 5f,
                SpellValueParameterKind.Radius => currentValue > 0f ? currentValue * 1.5f : 5f,
                SpellValueParameterKind.Duration => currentValue > 0f ? currentValue * 1.5f : 5f,
                _ => currentValue > 0f ? currentValue * 1.5f : 5f,
            };
        }
    }
}
