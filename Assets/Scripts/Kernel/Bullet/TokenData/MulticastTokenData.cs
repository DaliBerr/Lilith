using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 声明当前 CastBlock 需要从右侧收集多少个可执行 projectile 节点。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Multicast Token", fileName = "MulticastToken")]
    public sealed class MulticastTokenData : BaseTokenData
    {
        [SerializeField, Min(2)] private int castCount = 2;
        [SerializeField] private SpellCastPattern castPattern = SpellCastPattern.Simultaneous;
        [SerializeField, Min(0f)] private float sequentialIntervalSeconds = 0.12f;
        [SerializeField, Min(0f)] private float patternAngleStep = 18f;

        public int CastCount
        {
            get => Mathf.Max(2, castCount);
            set => castCount = Mathf.Max(2, value);
        }

        public SpellCastPattern CastPattern
        {
            get => castPattern;
            set => castPattern = value;
        }

        public float SequentialIntervalSeconds
        {
            get => Mathf.Max(0f, sequentialIntervalSeconds);
            set => sequentialIntervalSeconds = Mathf.Max(0f, value);
        }

        public float PatternAngleStep
        {
            get => Mathf.Max(0f, patternAngleStep);
            set => patternAngleStep = Mathf.Max(0f, value);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetTokenType(TokenType.Multicast);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            castCount = Mathf.Max(2, castCount);
            sequentialIntervalSeconds = Mathf.Max(0f, sequentialIntervalSeconds);
            patternAngleStep = Mathf.Max(0f, patternAngleStep);
            SetTokenType(TokenType.Multicast);
        }
    }
}
