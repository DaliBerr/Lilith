using System;
using System.Collections;
using Kernel.Bullet;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 统一封装 Token Select 弹窗的打开与复用逻辑。
    /// </summary>
    public static class TokenSelectUIUtility
    {
        /// <summary>
        /// summary: 打开或复用顶层 Token Select 弹窗，并接上新的选择 / 取消回调。
        /// param name="uiManager": 当前可用的 UIManager 实例
        /// param name="callerName": 调用方名称，仅用于日志
        /// param name="onSelected": 选中某个 token 后执行的回调
        /// param name="onCancelled": 用户关闭弹窗时执行的可选回调
        /// returns: 可供 StartCoroutine 等待的协程枚举器
        /// </summary>
        public static IEnumerator ShowTokenSelectModal(
            UIManager uiManager,
            string callerName,
            Action<PlaceableTokenData> onSelected,
            Action onCancelled = null)
        {
            if (uiManager == null)
            {
                GameDebug.LogWarning($"[{callerName}] Unable to show token select modal: UIManager is missing.");
                yield break;
            }

            if (uiManager.GetTopModal() is TokenSelectUIScreen existingModal)
            {
                existingModal.SetCallbacks(onSelected, onCancelled);
                yield break;
            }

            yield return uiManager.ShowModalAndWait<TokenSelectUIScreen>();
            if (uiManager.GetTopModal() is not TokenSelectUIScreen tokenSelectScreen)
            {
                GameDebug.LogWarning($"[{callerName}] Failed to resolve token select modal instance.");
                yield break;
            }

            tokenSelectScreen.SetCallbacks(onSelected, onCancelled);
        }
    }
}
