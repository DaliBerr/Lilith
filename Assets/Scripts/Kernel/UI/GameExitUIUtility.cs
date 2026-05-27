using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Vocalith.Localization;
using Vocalith.Logging;
using Vocalith.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kernel.UI
{
    /// <summary>
    /// 主菜单退出游戏确认流程的统一入口。
    /// </summary>
    public static class GameExitUIUtility
    {
        private const string ConfirmMessageKey = "ui.exit.confirm_message";
        private const string ConfirmLabelKey = "ui.exit.confirm";
        private const string CancelLabelKey = "ui.common.cancel";
        private const string DefaultConfirmMessage = "确定要退出游戏吗？";
        private const string DefaultConfirmLabel = "退出游戏";
        private const string DefaultCancelLabel = "取消";

        /// <summary>
        /// summary: 打开或复用顶层 Info Popup，并配置为退出游戏确认弹窗。
        /// param name="uiManager": 当前可用的 UIManager 实例
        /// param name="callerName": 调用方名称，仅用于日志
        /// returns: 可供 StartCoroutine 等待的协程枚举器
        /// </summary>
        public static IEnumerator ShowExitConfirmation(UIManager uiManager, string callerName)
        {
            if (uiManager == null)
            {
                GameDebug.LogWarning($"[{callerName}] Unable to show exit confirmation: UIManager is missing.");
                yield break;
            }

            if (uiManager.GetTopModal() is PopUpUIScreen existingPopup)
            {
                ConfigureExitConfirmation(existingPopup, QuitApplication);
                yield break;
            }

            yield return uiManager.ShowModalAndWait<PopUpUIScreen>();
            if (uiManager.GetTopModal() is not PopUpUIScreen popup)
            {
                GameDebug.LogWarning($"[{callerName}] Failed to resolve exit confirmation popup instance.");
                yield break;
            }

            ConfigureExitConfirmation(popup, QuitApplication);
        }

        /// <summary>
        /// summary: 把现有 Info Popup 实例配置为退出游戏确认弹窗。
        /// param name="popup": 需要配置的 Info Popup 实例
        /// param name="onConfirmed": 玩家确认退出时执行的回调
        /// returns: 无
        /// </summary>
        public static void ConfigureExitConfirmation(PopUpUIScreen popup, UnityAction onConfirmed)
        {
            if (popup == null)
            {
                return;
            }

            popup.Configure(
                LocalizationManager.TranslateOrDefault(ConfirmMessageKey, DefaultConfirmMessage),
                onConfirm: onConfirmed,
                confirmLabel: LocalizationManager.TranslateOrDefault(ConfirmLabelKey, DefaultConfirmLabel),
                closeLabel: LocalizationManager.TranslateOrDefault(CancelLabelKey, DefaultCancelLabel));
        }

        /// <summary>
        /// summary: 执行退出游戏；编辑器中停止 Play，构建中退出应用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public static void QuitApplication()
        {
            GameDebug.Log("[GameExitUIUtility] Quit confirmed.");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
