using System.Collections;
using Kernel.GameState;
using Vocalith.UI;

namespace Kernel.UI
{
    public abstract class GameUIScreen : UIScreen
    {
        public abstract Status currentStatus { get; }

        protected override void OnManagerInitialized()
        {
            EnsureCurrentStatus();
        }

        public override IEnumerator Show(float fade = 0.15f)
        {
            EnsureCurrentStatus();
            yield return base.Show(fade);
        }

        /// <summary>
        /// 确保当前界面声明的游戏状态已进入状态栈，避免切屏后输入路由失配。
        /// </summary>
        /// <param name="none">无</param>
        /// <returns>无</returns>
        protected void EnsureCurrentStatus()
        {
            if (!StatusController.HasStatus(currentStatus))
            {
                StatusController.AddStatus(currentStatus);
            }
        }
    }
}
