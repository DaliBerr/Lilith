using UnityEngine;

namespace Kernel.Bullet
{
    public enum PayloadBoundaryKind
    {
        Start = 0,
        End = 1,
    }

    /// <summary>
    /// 用显式 token 标记 trigger payload 的开始或结束位置。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Payload Boundary Token", fileName = "PayloadBoundaryToken")]
    public sealed class PayloadBoundaryTokenData : BaseTokenData
    {
        [SerializeField] private PayloadBoundaryKind boundaryKind = PayloadBoundaryKind.Start;

        public PayloadBoundaryKind BoundaryKind
        {
            get => boundaryKind;
            set
            {
                boundaryKind = value;
                ApplyTokenType();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyTokenType();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyTokenType();
        }

        private void ApplyTokenType()
        {
            SetTokenType(boundaryKind == PayloadBoundaryKind.Start
                ? TokenType.PayloadStart
                : TokenType.PayloadEnd);
        }
    }
}
