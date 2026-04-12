using UnityEngine;

namespace Kernel.UI
{
    /// <summary>
    /// 提供可被其他 UI 控制器主动驱动的通用笔画血条组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrokeHealthBarController : StrokeHealthBarControllerBase
    {
        [SerializeField] private GameObject healthCellPrefab;
        [SerializeField, Min(0.01f)] private float hpPerCell = 20f;
        [SerializeField, Min(0f)] private float changeAnimationDuration = 0.2f;

        protected override Transform CellRoot => transform;
        protected override GameObject HealthCellPrefab => healthCellPrefab;
        protected override float HpPerCell => hpPerCell;
        protected override float ChangeAnimationDuration => changeAnimationDuration;

        private void OnDisable()
        {
            HandleHealthBarDisabled();
        }

        private void OnDestroy()
        {
            HandleHealthBarDisabled();
        }

        private void OnValidate()
        {
            SanitizeConfiguration();
        }

        /// <summary>
        /// 立即把当前血条同步到指定生命值，不播放过渡动画。
        /// </summary>
        /// <param name="currentHealth">当前生命值。</param>
        /// <param name="maxHealth">当前最大生命值。</param>
        /// <returns>无。</returns>
        public void SetHealthImmediate(float currentHealth, float maxHealth)
        {
            SetHealthInstant(currentHealth, maxHealth);
        }

        /// <summary>
        /// 从当前显示状态过渡到新的生命值。
        /// </summary>
        /// <param name="currentHealth">目标生命值。</param>
        /// <param name="maxHealth">目标最大生命值。</param>
        /// <returns>无。</returns>
        public void SetHealthAnimatedValue(float currentHealth, float maxHealth)
        {
            SetHealthAnimated(currentHealth, maxHealth);
        }

        /// <summary>
        /// 清空当前血条显示，并把现有血格进度全部重置为 0。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        public void ClearDisplay()
        {
            ResetHealthDisplay();
        }

        /// <summary>
        /// 规整通用血条的基础配置，避免非法值进入运行时。
        /// </summary>
        /// <param name="无">无。</param>
        /// <returns>无。</returns>
        private void SanitizeConfiguration()
        {
            SanitizeSharedConfiguration(ref hpPerCell, ref changeAnimationDuration);
        }
    }
}
