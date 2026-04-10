using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 表示拾取后尝试恢复玩家生命的掉落 token。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Pickups/Healing Token", fileName = "HealingToken")]
    public sealed class HealingPickupTokenData : PickupTokenData
    {
        [SerializeField, Min(0.01f)] private float healingAmount = 10f;

        public float HealingAmount
        {
            get => Mathf.Max(0.01f, healingAmount);
            set => healingAmount = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// summary: 修正治疗量配置，避免出现小于等于 0 的非法值。
        /// param: 无
        /// returns: 无
        /// </summary>
        protected override void OnValidate()
        {
            base.OnValidate();
            healingAmount = Mathf.Max(0.01f, healingAmount);
        }
    }
}
