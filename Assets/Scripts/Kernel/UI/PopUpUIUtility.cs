using System.Collections;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 统一封装通用信息弹窗的打开与复用逻辑，避免多个入口重复实现相同协程。
    /// </summary>
    public static class PopUpUIUtility
    {
        /// <summary>
        /// summary: 打开或复用顶层 Info Popup，并写入新的提示文本与按钮文案。
        /// param name="uiManager": 当前可用的 UIManager 实例
        /// param name="callerName": 调用方名称，仅用于日志
        /// param name="message": 需要显示在弹窗中的正文文本
        /// param name="confirmLabel": 确认按钮文案
        /// param name="closeLabel": 关闭按钮文案
        /// returns: 可供 StartCoroutine 等待的协程枚举器
        /// </summary>
        public static IEnumerator ShowInfoPopup(
            UIManager uiManager,
            string callerName,
            string message,
            string confirmLabel = "知道了",
            string closeLabel = "关闭")
        {
            if (uiManager == null)
            {
                GameDebug.LogWarning($"[{callerName}] Unable to show popup: UIManager is missing.");
                yield break;
            }

            if (uiManager.GetTopModal() is PopUpUIScreen existingPopup)
            {
                existingPopup.Configure(
                    message,
                    confirmLabel: confirmLabel,
                    closeLabel: closeLabel);
                yield break;
            }

            yield return uiManager.ShowModalAndWait<PopUpUIScreen>();
            if (uiManager.GetTopModal() is not PopUpUIScreen popup)
            {
                GameDebug.LogWarning($"[{callerName}] Failed to resolve popup instance.");
                yield break;
            }

            popup.Configure(
                message,
                confirmLabel: confirmLabel,
                closeLabel: closeLabel);
        }
    }
}
