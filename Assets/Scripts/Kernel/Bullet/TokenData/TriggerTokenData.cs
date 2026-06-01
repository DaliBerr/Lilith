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

        public SpellTriggerType TriggerType
        {
            get => triggerType;
            set => triggerType = value == SpellTriggerType.None ? SpellTriggerType.OnHit : value;
        }

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
        }
    }
}
