using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vocalith.EventSystem;

namespace Kernel.UI
{
    /// <summary>
    /// 根据玩家生命值驱动 MainUI 中的分段式血槽显示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthBarController : MonoBehaviour
    {
        [SerializeField] private GameObject healthCellPrefab;
        [SerializeField] private PlayerHealth targetHealth;
        [SerializeField, Min(0.01f)] private float hpPerCell = 20f;
        [SerializeField, Min(0f)] private float changeAnimationDuration = 0.2f;

        private readonly List<StrokeRevealUIWord> runtimeCells = new();
        private IDisposable healthChangedSubscription;
        private Coroutine changeRoutine;
        private float displayedHealth;
        private float displayedMaxHealth;
        private bool hasSynchronized;

        private void OnEnable()
        {
            SanitizeConfiguration();
            SubscribeToHealthEvents();
            TryResolveTargetHealth();
            SyncToCurrentHealthImmediate();
        }

        private void OnDisable()
        {
            StopChangeAnimation();
            DisposeHealthSubscription();
        }

        private void OnDestroy()
        {
            StopChangeAnimation();
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

            ApplyHealthInstant(targetHealth.CurrentHealth, targetHealth.MaxHealth);
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

            ApplyHealthAnimated(evt.currentHealth, evt.maxHealth);
        }

        /// <summary>
        /// 直接刷新血槽到指定生命值，用于初始化和无动画场景。
        /// </summary>
        /// <param name="currentHealth">当前生命值。</param>
        /// <param name="maxHealth">当前最大生命值。</param>
        /// <returns>无。</returns>
        private void ApplyHealthInstant(float currentHealth, float maxHealth)
        {
            float visualMaxHealth = GetVisualMaxHealth(maxHealth, currentHealth, currentHealth);
            EnsureCellCount(GetRequiredCellCount(visualMaxHealth));

            displayedMaxHealth = visualMaxHealth;
            displayedHealth = Mathf.Clamp(currentHealth, 0f, displayedMaxHealth);
            hasSynchronized = true;

            ApplyDisplayedHealthToCells(displayedHealth, displayedMaxHealth);
        }

        /// <summary>
        /// 从当前 UI 表现值过渡到新的生命值。
        /// </summary>
        /// <param name="currentHealth">目标生命值。</param>
        /// <param name="maxHealth">目标最大生命值。</param>
        /// <returns>无。</returns>
        private void ApplyHealthAnimated(float currentHealth, float maxHealth)
        {
            float startHealth = hasSynchronized ? displayedHealth : currentHealth;
            float visualMaxHealth = GetVisualMaxHealth(maxHealth, currentHealth, startHealth);
            float targetHealthValue = Mathf.Clamp(currentHealth, 0f, visualMaxHealth);

            EnsureCellCount(GetRequiredCellCount(visualMaxHealth));
            StopChangeAnimation();

            if (!hasSynchronized || changeAnimationDuration <= 0f)
            {
                displayedMaxHealth = visualMaxHealth;
                displayedHealth = targetHealthValue;
                hasSynchronized = true;
                ApplyDisplayedHealthToCells(displayedHealth, displayedMaxHealth);
                return;
            }

            changeRoutine = StartCoroutine(AnimateHealthChange(startHealth, targetHealthValue, visualMaxHealth));
        }

        /// <summary>
        /// 逐帧更新当前显示生命值，让扣血与回血都能平滑过渡。
        /// </summary>
        /// <param name="startHealth">动画起始生命值。</param>
        /// <param name="targetHealthValue">动画目标生命值。</param>
        /// <param name="visualMaxHealth">本次动画使用的视觉生命上限。</param>
        /// <returns>协程枚举器。</returns>
        private IEnumerator AnimateHealthChange(float startHealth, float targetHealthValue, float visualMaxHealth)
        {
            displayedMaxHealth = visualMaxHealth;
            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, changeAnimationDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                displayedHealth = Mathf.Lerp(startHealth, targetHealthValue, t);
                ApplyDisplayedHealthToCells(displayedHealth, displayedMaxHealth);
                yield return null;
            }

            displayedHealth = targetHealthValue;
            ApplyDisplayedHealthToCells(displayedHealth, displayedMaxHealth);
            hasSynchronized = true;
            changeRoutine = null;
        }

        /// <summary>
        /// 按当前显示生命值把每一格血量换算为整格归一化进度。
        /// </summary>
        /// <param name="currentDisplayedHealth">当前显示生命值。</param>
        /// <param name="visualMaxHealth">当前视觉生命上限。</param>
        /// <returns>无。</returns>
        private void ApplyDisplayedHealthToCells(float currentDisplayedHealth, float visualMaxHealth)
        {
            float clampedMaxHealth = Mathf.Max(0f, visualMaxHealth);

            for (int i = 0; i < runtimeCells.Count; i++)
            {
                StrokeRevealUIWord cell = runtimeCells[i];
                if (cell == null)
                {
                    continue;
                }

                float segmentStart = i * hpPerCell;
                float segmentEnd = Mathf.Min(segmentStart + hpPerCell, clampedMaxHealth);
                float segmentCapacity = segmentEnd - segmentStart;
                float normalizedProgress = 0f;

                if (segmentCapacity > 0f)
                {
                    float filledHealth = Mathf.Clamp(currentDisplayedHealth - segmentStart, 0f, segmentCapacity);
                    normalizedProgress = filledHealth / segmentCapacity;
                }

                cell.SetNormalizedProgress(normalizedProgress, stopPlayback: false);
            }
        }

        /// <summary>
        /// 保证运行时血格数量足以覆盖当前最大生命区间。
        /// </summary>
        /// <param name="requiredCellCount">期望血格数量。</param>
        /// <returns>无。</returns>
        private void EnsureCellCount(int requiredCellCount)
        {
            RebuildRuntimeCellCache();

            if (requiredCellCount <= runtimeCells.Count)
            {
                return;
            }

            if (healthCellPrefab == null)
            {
                return;
            }

            while (runtimeCells.Count < requiredCellCount)
            {
                GameObject cellObject = Instantiate(healthCellPrefab, transform, false);
                if (!cellObject.TryGetComponent(out StrokeRevealUIWord cell))
                {
                    Destroy(cellObject);
                    return;
                }

                PrepareRuntimeCell(cell, runtimeCells.Count);
                runtimeCells.Add(cell);
            }
        }

        /// <summary>
        /// 当缓存丢失或对象被外部销毁时，从当前层级重建血格缓存。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void RebuildRuntimeCellCache()
        {
            runtimeCells.RemoveAll(cell => cell == null);
            if (runtimeCells.Count > 0)
            {
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                if (!transform.GetChild(i).TryGetComponent(out StrokeRevealUIWord cell))
                {
                    continue;
                }

                runtimeCells.Add(cell);
            }
        }

        /// <summary>
        /// 初始化新创建的单格血条实例，确保其不会自行播放动画。
        /// </summary>
        /// <param name="cell">新创建的血格实例。</param>
        /// <param name="index">血格索引。</param>
        /// <returns>无。</returns>
        private void PrepareRuntimeCell(StrokeRevealUIWord cell, int index)
        {
            if (cell == null)
            {
                return;
            }

            cell.name = $"Health_{index + 1}";
            cell.playOnEnable = StrokeRevealUIWord.AutoPlayMode.None;
            cell.Stop();
            cell.SetNormalizedProgress(0f, stopPlayback: false);

            if (cell.transform is RectTransform rectTransform)
            {
                rectTransform.localScale = Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.anchoredPosition3D = Vector3.zero;
            }
        }

        /// <summary>
        /// 计算当前视觉所需的最大生命上限，保证回血时有足够的血格承载动画。
        /// </summary>
        /// <param name="maxHealth">事件或目标生命提供的最大生命值。</param>
        /// <param name="currentHealth">目标生命值。</param>
        /// <param name="currentDisplayedHealth">当前 UI 已显示的生命值。</param>
        /// <returns>视觉生命上限。</returns>
        private float GetVisualMaxHealth(float maxHealth, float currentHealth, float currentDisplayedHealth)
        {
            return Mathf.Max(0f, maxHealth, currentHealth, currentDisplayedHealth);
        }

        /// <summary>
        /// 将生命上限换算成血格数量，至少返回 0。
        /// </summary>
        /// <param name="visualMaxHealth">当前视觉生命上限。</param>
        /// <returns>所需血格数量。</returns>
        private int GetRequiredCellCount(float visualMaxHealth)
        {
            if (visualMaxHealth <= 0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(visualMaxHealth / hpPerCell);
        }

        /// <summary>
        /// 停止当前血量变化协程，避免旧动画覆盖新状态。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void StopChangeAnimation()
        {
            if (changeRoutine == null)
            {
                return;
            }

            StopCoroutine(changeRoutine);
            changeRoutine = null;
        }

        /// <summary>
        /// 规整运行时配置，避免血格划分和动画时长出现非法值。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void SanitizeConfiguration()
        {
            if (float.IsNaN(hpPerCell) || float.IsInfinity(hpPerCell) || hpPerCell <= 0f)
            {
                hpPerCell = 20f;
            }

            if (float.IsNaN(changeAnimationDuration) || float.IsInfinity(changeAnimationDuration) || changeAnimationDuration < 0f)
            {
                changeAnimationDuration = 0.2f;
            }
        }
    }
}
