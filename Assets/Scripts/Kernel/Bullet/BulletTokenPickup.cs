using TMPro;
using UnityEngine;
using Vocalith.EventSystem;
using Vocalith.Localization;

namespace Kernel.Bullet
{
    /// <summary>
    /// 场景中的 Bullet Token 拾取物；玩家接触后尝试写入背包。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletTokenPickup : MonoBehaviour
    {
        [SerializeField] private PlaceableTokenData token;
        [SerializeField] private TMP_Text glyphText;
        [SerializeField] private Collider triggerCollider;
        [SerializeField, Min(0f)] private float ySpinDegreesPerSecond = 90f;

        private bool isCollected;

        public PlaceableTokenData Token => token;
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

        private void Update()
        {
            if (!Application.isPlaying || isCollected)
            {
                return;
            }

            RotateAroundYAxis(Time.deltaTime);
        }

        /// <summary>
        /// summary: 为当前 pickup 设置需要展示与发放的 token。
        /// param: value 当前 pickup 持有的 token
        /// returns: token 有效并成功刷新显示时返回 true
        /// </summary>
        public bool TrySetToken(PlaceableTokenData value)
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
            if (isCollected || token == null || other == null)
            {
                return;
            }

            if (TryCollectSpecialPickup(other))
            {
                return;
            }

            if (!TryResolveInventory(other, out PlayerBulletTokenInventory inventory))
            {
                return;
            }

            if (!inventory.TryAddItem(token, out _))
            {
                return;
            }

            ConsumePickup();
        }

        /// <summary>
        /// summary: 对需要立即结算效果的专用 pickup token 执行拾取处理。
        /// param: other 本次进入触发器的碰撞体
        /// returns: 已按专用规则处理并消费当前 pickup 时返回 true
        /// </summary>
        private bool TryCollectSpecialPickup(Collider other)
        {
            if (token is RemnantPickupTokenData remnantToken)
            {
                return TryCollectRemnantPickup(remnantToken, other);
            }

            if (token is HealingPickupTokenData healingToken)
            {
                return TryCollectHealingPickup(healingToken, other);
            }

            return false;
        }

        /// <summary>
        /// summary: 结算残卷拾取效果，命中玩家后直接增加全局残卷数量并消费 pickup。
        /// param: remnantToken 当前 pickup 绑定的残卷 token
        /// param: other 本次进入触发器的碰撞体
        /// returns: 成功结算并消费当前 pickup 时返回 true
        /// </summary>
        private bool TryCollectRemnantPickup(RemnantPickupTokenData remnantToken, Collider other)
        {
            if (remnantToken == null || !IsPlayerCollector(other))
            {
                return false;
            }

            if (!PlayerRemnantWallet.TryAddCurrentRemnants(remnantToken.RemnantAmount, out _))
            {
                return false;
            }

            EventManager.eventBus.Publish(new RunRewardCollectedEvent(
                LocalizationManager.TranslateOrDefault("reward.remnant", "残卷"),
                remnantToken.RemnantAmount));
            ConsumePickup();
            return true;
        }

        /// <summary>
        /// summary: 结算愈拾取效果，命中玩家后尝试回血并消费 pickup。
        /// param: healingToken 当前 pickup 绑定的治疗 token
        /// param: other 本次进入触发器的碰撞体
        /// returns: 成功命中玩家并消费当前 pickup 时返回 true
        /// </summary>
        private bool TryCollectHealingPickup(HealingPickupTokenData healingToken, Collider other)
        {
            if (healingToken == null || !TryResolvePlayerHealth(other, out PlayerHealth playerHealth))
            {
                return false;
            }

            playerHealth.TryApplyHealing(healingToken.HealingAmount, out _, out _);
            ConsumePickup();
            return true;
        }

        /// <summary>
        /// summary: 判断当前碰撞体是否属于玩家可拾取对象。
        /// param: other 本次进入触发器的碰撞体
        /// returns: 命中玩家相关组件时返回 true
        /// </summary>
        private static bool IsPlayerCollector(Collider other)
        {
            return other != null
                && (other.GetComponentInParent<PlayerHealth>() != null
                    || other.GetComponentInParent<PlayerBulletTokenInventory>() != null
                    || other.GetComponentInParent<PlayerPlaneMovement>() != null);
        }

        /// <summary>
        /// summary: 从进入触发器的碰撞体向上解析玩家生命组件。
        /// param: other 本次进入触发器的碰撞体
        /// param: playerHealth 输出解析到的玩家生命组件
        /// returns: 成功拿到玩家生命组件时返回 true
        /// </summary>
        private static bool TryResolvePlayerHealth(Collider other, out PlayerHealth playerHealth)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
            return playerHealth != null;
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
            string displayText = token != null ? token.GetPickupDisplayText() : string.Empty;
            TMP_Text[] displayTexts = GetComponentsInChildren<TMP_Text>(includeInactive: true);

            if (displayTexts == null || displayTexts.Length == 0)
            {
                return;
            }

            for (int i = 0; i < displayTexts.Length; i++)
            {
                if (displayTexts[i] != null)
                {
                    displayTexts[i].text = displayText;
                }
            }
        }

        /// <summary>
        /// summary: 让掉落 pickup 在运行时持续绕世界 Y 轴旋转，增加悬浮展示感。
        /// param: deltaTime 本次刷新使用的时间步长
        /// returns: 无
        /// </summary>
        private void RotateAroundYAxis(float deltaTime)
        {
            if (ySpinDegreesPerSecond <= 0f)
            {
                return;
            }

            transform.Rotate(Vector3.up, ySpinDegreesPerSecond * Mathf.Max(0f, deltaTime), Space.World);
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
        /// summary: 统一消费当前 pickup，标记为已收集并执行销毁。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ConsumePickup()
        {
            isCollected = true;
            DestroySelf();
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
