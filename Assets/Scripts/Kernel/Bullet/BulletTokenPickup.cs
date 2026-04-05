using TMPro;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 场景中的 Bullet Token 拾取物；玩家接触后尝试写入背包。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletTokenPickup : MonoBehaviour
    {
        [SerializeField] private BaseTokenData token;
        [SerializeField] private TMP_Text glyphText;
        [SerializeField] private Collider triggerCollider;

        private bool isCollected;

        public BaseTokenData Token => token;
        public bool IsCollected => isCollected;

        private void Awake()
        {
            TryCacheBindings();
            EnsureTriggerColliderConfiguration();
            RefreshDisplay();
        }

        private void OnValidate()
        {
            TryCacheBindings();
            EnsureTriggerColliderConfiguration();
            RefreshDisplay();
        }

        /// <summary>
        /// summary: 为当前 pickup 设置需要展示与发放的 token。
        /// param: value 当前 pickup 持有的 token
        /// returns: token 有效并成功刷新显示时返回 true
        /// </summary>
        public bool TrySetToken(BaseTokenData value)
        {
            if (value == null)
            {
                return false;
            }

            token = value;
            RefreshDisplay();
            return true;
        }

        /// <summary>
        /// summary: 当玩家碰到 pickup 时，尝试把 token 放入玩家库存；背包满则保留在场景中。
        /// param: other 本次进入触发器的碰撞体
        /// returns: 无
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (isCollected || token == null || other == null || !TryResolveInventory(other, out PlayerBulletTokenInventory inventory))
            {
                return;
            }

            if (!inventory.TryAddToken(token, out _))
            {
                return;
            }

            isCollected = true;
            DestroySelf();
        }

        /// <summary>
        /// summary: 解析 pickup 需要更新的 TMP 文本和触发器引用。
        /// param: 无
        /// returns: 成功拿到任一可用引用时返回 true
        /// </summary>
        private bool TryCacheBindings()
        {
            if (glyphText == null)
            {
                glyphText = GetComponent<TMP_Text>();
            }

            if (glyphText == null)
            {
                glyphText = GetComponentInChildren<TMP_Text>(includeInactive: true);
            }

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }

            return glyphText != null || triggerCollider != null;
        }

        /// <summary>
        /// summary: 把 pickup 当前持有的 token 文本同步到视觉表现；空 token 时显示为空。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshDisplay()
        {
            if (glyphText == null)
            {
                return;
            }

            glyphText.text = token != null ? token.GetResolvedDisplayText() : string.Empty;
        }

        /// <summary>
        /// summary: 保证 pickup 使用的碰撞体始终是 trigger，以便玩家刚体进入时触发拾取。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureTriggerColliderConfiguration()
        {
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// summary: 从进入触发器的碰撞体向上解析玩家库存组件。
        /// param: other 本次进入触发器的碰撞体
        /// param: inventory 输出解析到的玩家库存组件
        /// returns: 成功拿到玩家库存时返回 true
        /// </summary>
        private static bool TryResolveInventory(Collider other, out PlayerBulletTokenInventory inventory)
        {
            inventory = other.GetComponentInParent<PlayerBulletTokenInventory>();
            return inventory != null;
        }

        /// <summary>
        /// summary: 统一销毁当前 pickup；在 EditMode 测试下立即销毁，运行时保持普通 Destroy。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void DestroySelf()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

            if (gameObject != null)
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
