using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 表示拾取后增加残卷计数的掉落 token。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Pickups/Remnant Token", fileName = "RemnantToken")]
    public sealed class RemnantPickupTokenData : PickupTokenData
    {
        [SerializeField, Min(1)] private int remnantAmount = 1;

        public int RemnantAmount
        {
            get => Mathf.Max(1, remnantAmount);
            set => remnantAmount = Mathf.Max(1, value);
        }

        /// <summary>
        /// summary: 修正残卷数量配置，避免出现小于 1 的非法值。
        /// param: 无
        /// returns: 无
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            remnantAmount = Mathf.Max(1, remnantAmount);
        }
    }
}
