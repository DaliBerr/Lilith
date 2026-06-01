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

        public int CastCount
        {
            get => Mathf.Max(2, castCount);
            set => castCount = Mathf.Max(2, value);
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
            SetTokenType(TokenType.Multicast);
        }
    }
}
