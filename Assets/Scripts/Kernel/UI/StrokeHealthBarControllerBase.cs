using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.UI
{
    /// <summary>
    /// 为使用笔画格子的血条提供共享的分段渲染与动画逻辑。
    /// </summary>
    public abstract class StrokeHealthBarControllerBase : MonoBehaviour
    {
        private readonly List<StrokeRevealUIWord> runtimeCells = new();
        private Coroutine changeRoutine;
        private float displayedHealth;
        private float displayedMaxHealth;
        private bool hasSynchronized;

        protected abstract Transform CellRoot { get; }
        protected abstract GameObject HealthCellPrefab { get; }
        protected abstract float HpPerCell { get; }
        protected abstract float ChangeAnimationDuration { get; }

        /// <summary>
        /// 直接把共享血条同步到指定生命值，不播放过渡动画。
        /// </summary>
        /// <param name="currentHealth">当前生命值。</param>
        /// <param name="maxHealth">当前最大生命值。</param>
        /// <returns>无。</returns>
        protected void SetHealthInstant(float currentHealth, float maxHealth)
        {
            float visualMaxHealth = GetVisualMaxHealth(maxHealth, currentHealth, currentHealth);
            EnsureCellCount(GetRequiredCellCount(visualMaxHealth));

            displayedMaxHealth = visualMaxHealth;
            displayedHealth = Mathf.Clamp(currentHealth, 0f, displayedMaxHealth);
            hasSynchronized = true;

            ApplyDisplayedHealthToCells(displayedHealth, displayedMaxHealth);
        }

        /// <summary>
        /// 从当前显示生命平滑过渡到新的生命值。
        /// </summary>
        /// <param name="currentHealth">目标生命值。</param>
        /// <param name="maxHealth">目标最大生命值。</param>
        /// <returns>无。</returns>
        protected void SetHealthAnimated(float currentHealth, float maxHealth)
        {
            float startHealth = hasSynchronized ? displayedHealth : currentHealth;
            float visualMaxHealth = GetVisualMaxHealth(maxHealth, currentHealth, startHealth);
            float targetHealthValue = Mathf.Clamp(currentHealth, 0f, visualMaxHealth);

            EnsureCellCount(GetRequiredCellCount(visualMaxHealth));
            StopHealthAnimation();

            if (!hasSynchronized || ChangeAnimationDuration <= 0f)
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
        /// 把共享血条重置为空状态，并清零所有血格进度。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        protected void ResetHealthDisplay()
        {
            StopHealthAnimation();
            displayedHealth = 0f;
            displayedMaxHealth = 0f;
            hasSynchronized = false;
            ApplyDisplayedHealthToCells(0f, 0f);
        }

        /// <summary>
        /// 在派生类停用或销毁时停止共享血条动画。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        protected void HandleHealthBarDisabled()
        {
            StopHealthAnimation();
        }

        /// <summary>
        /// 规整共享血条配置，避免非法的血格容量或动画时长进入运行时。
        /// </summary>
        /// <param name="hpPerCell">每格承载生命值。</param>
        /// <param name="changeAnimationDuration">生命变化动画时长。</param>
        /// <returns>无。</returns>
        protected static void SanitizeSharedConfiguration(ref float hpPerCell, ref float changeAnimationDuration)
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

        /// <summary>
        /// 逐帧更新当前显示生命值，让扣血和回血都能平滑过渡。
        /// </summary>
        /// <param name="startHealth">动画起始生命值。</param>
        /// <param name="targetHealthValue">动画目标生命值。</param>
        /// <param name="visualMaxHealth">本次动画使用的视觉生命上限。</param>
        /// <returns>协程枚举器。</returns>
        private IEnumerator AnimateHealthChange(float startHealth, float targetHealthValue, float visualMaxHealth)
        {
            displayedMaxHealth = visualMaxHealth;
            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, ChangeAnimationDuration);

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

                float segmentStart = i * HpPerCell;
                float segmentEnd = Mathf.Min(segmentStart + HpPerCell, clampedMaxHealth);
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

            Transform cellRoot = CellRoot;
            if (cellRoot == null || HealthCellPrefab == null)
            {
                return;
            }

            while (runtimeCells.Count < requiredCellCount)
            {
                GameObject cellObject = Instantiate(HealthCellPrefab, cellRoot, false);
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

            Transform cellRoot = CellRoot;
            if (cellRoot == null)
            {
                return;
            }

            for (int i = 0; i < cellRoot.childCount; i++)
            {
                if (!cellRoot.GetChild(i).TryGetComponent(out StrokeRevealUIWord cell))
                {
                    continue;
                }

                runtimeCells.Add(cell);
            }
        }

        /// <summary>
        /// 初始化新创建的血格实例，确保其不会在启用时自行播放动画。
        /// </summary>
        /// <param name="cell">新创建的血格实例。</param>
        /// <param name="index">血格索引。</param>
        /// <returns>无。</returns>
        private static void PrepareRuntimeCell(StrokeRevealUIWord cell, int index)
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
        /// 计算当前视觉所需的最大生命上限，保证回血动画有足够的血格承载。
        /// </summary>
        /// <param name="maxHealth">目标最大生命值。</param>
        /// <param name="currentHealth">目标生命值。</param>
        /// <param name="currentDisplayedHealth">当前 UI 已显示的生命值。</param>
        /// <returns>视觉生命上限。</returns>
        private static float GetVisualMaxHealth(float maxHealth, float currentHealth, float currentDisplayedHealth)
        {
            return Mathf.Max(0f, maxHealth, currentHealth, currentDisplayedHealth);
        }

        /// <summary>
        /// 把生命上限换算成血格数量，至少返回 0。
        /// </summary>
        /// <param name="visualMaxHealth">当前视觉生命上限。</param>
        /// <returns>所需血格数量。</returns>
        private int GetRequiredCellCount(float visualMaxHealth)
        {
            if (visualMaxHealth <= 0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(visualMaxHealth / HpPerCell);
        }

        /// <summary>
        /// 停止当前血量变化协程，避免旧动画覆盖新状态。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void StopHealthAnimation()
        {
            if (changeRoutine == null)
            {
                return;
            }

            StopCoroutine(changeRoutine);
            changeRoutine = null;
        }
    }
}
