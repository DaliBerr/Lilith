using System;
using UnityEngine;
using Vocalith.EventSystem;

namespace Kernel.UI
{
    /// <summary>
    /// 根据玩家生命值驱动 MainUI 中的分段式血槽显示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthBarController : StrokeHealthBarControllerBase
    {
        [SerializeField] private GameObject healthCellPrefab;
        [SerializeField] private PlayerHealth targetHealth;
        [SerializeField, Min(0.01f)] private float hpPerCell = 20f;
        [SerializeField, Min(0f)] private float changeAnimationDuration = 0.2f;

        private IDisposable healthChangedSubscription;

        protected override Transform CellRoot => transform;
        protected override GameObject HealthCellPrefab => healthCellPrefab;
        protected override float HpPerCell => hpPerCell;
        protected override float ChangeAnimationDuration => changeAnimationDuration;

        private void OnEnable()
        {
            SanitizeConfiguration();
            SubscribeToHealthEvents();
            TryResolveTargetHealth();
            SyncToCurrentHealthImmediate();
        }

        private void OnDisable()
        {
            HandleHealthBarDisabled();
            DisposeHealthSubscription();
        }

        private void OnDestroy()
        {
            HandleHealthBarDisabled();
            DisposeHealthSubscription();
        }

        private void OnValidate()
        {
            SanitizeConfiguration();
        }

        /// <summary>
        /// 订阅玩家生命变化事件，保证血槽能随生命结算刷新。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void SubscribeToHealthEvents()
        {
            if (healthChangedSubscription != null)
            {
                return;
            }

            healthChangedSubscription = EventManager.eventBus.Subscribe<PlayerHealthChangedEvent>(HandlePlayerHealthChanged);
        }

        /// <summary>
        /// 释放当前的生命事件订阅。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void DisposeHealthSubscription()
        {
            healthChangedSubscription?.Dispose();
            healthChangedSubscription = null;
        }

        /// <summary>
        /// 优先使用显式配置，否则自动查找场景中的玩家生命组件。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>成功拿到生命组件时返回 true。</returns>
        private bool TryResolveTargetHealth()
        {
            if (targetHealth != null)
            {
                return true;
            }

            targetHealth = FindFirstObjectByType<PlayerHealth>();
            return targetHealth != null;
        }

        /// <summary>
        /// 立即把血槽同步到当前玩家生命值，不播放过渡动画。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void SyncToCurrentHealthImmediate()
        {
            if (!TryResolveTargetHealth())
            {
                return;
            }

            SetHealthInstant(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }

        /// <summary>
        /// 响应玩家生命事件，并按当前生命变化方向播放血槽过渡。
        /// </summary>
        /// <param name="evt">本次生命变化事件。</param>
        /// <returns>无。</returns>
        private void HandlePlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (evt.source == null)
            {
                return;
            }

            if (targetHealth == null)
            {
                targetHealth = evt.source;
            }

            if (evt.source != targetHealth)
            {
                return;
            }

            SetHealthAnimated(evt.currentHealth, evt.maxHealth);
        }

        /// <summary>
        /// 规整运行时配置，避免血格划分和动画时长出现非法值。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void SanitizeConfiguration()
        {
            SanitizeSharedConfiguration(ref hpPerCell, ref changeAnimationDuration);
        }
    }
}
