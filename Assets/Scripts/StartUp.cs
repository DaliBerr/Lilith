using System.Collections;
using Kernel.GameState;
using Kernel.UI;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;

namespace Kernel
{
    /// <summary>
    /// Main 场景的本地启动器，只负责场景内游戏内容启动。
    /// </summary>
    public sealed class Startup : MonoBehaviour
    {
        public static Startup Instance { get; private set; }

        private bool isSceneBootCompleted;

        public bool IsSceneBootCompleted => isSceneBootCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Start()
        {
            yield return StartCoroutine(BootScene());
        }

        /// <summary>
        /// summary: 校验全局启动器已完成交接前置条件，然后执行 Main 场景自己的内容启动。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator BootScene()
        {
            if (!CanBootMainScene())
            {
                yield break;
            }

            yield return StartCoroutine(EnterMainUI());

            if (StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                StatusController.RemoveStatus(StatusList.GameLoadingStatus);
            }

            isSceneBootCompleted = true;
            GlobalStartup.Instance?.NotifyMainSceneStartupComplete();
        }

        /// <summary>
        /// summary: 只允许由 StartUp 场景里的 GlobalStartup 交接进入 Main 场景，防止直接单场景 Play。
        /// param: 无
        /// returns: 条件满足返回 true，否则返回 false
        /// </summary>
        private bool CanBootMainScene()
        {
            if (GlobalStartup.Instance == null)
            {
                Log.Error("[Startup] GlobalStartup is missing. Open StartUp scene first.");
                GameDebug.LogError("[Startup] GlobalStartup is missing. Open StartUp scene first.");
                return false;
            }

            if (!GlobalStartup.Instance.IsBootCompleted)
            {
                Log.Error("[Startup] GlobalStartup has not completed boot. Open StartUp scene first.");
                GameDebug.LogError("[Startup] GlobalStartup has not completed boot. Open StartUp scene first.");
                return false;
            }

            return true;
        }
        
        /// <summary>
        /// summary: 在 Main 场景内容启动完成后，把 MainUIScreen 压到持久化 UIManager 的栈顶。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator EnterMainUI()
        {
            if (UIManager.Instance == null)
            {
                GameDebug.LogError("[Startup] UIManager is missing. MainUIScreen cannot be pushed.");
                yield break;
            }

            if (UIManager.Instance.GetTopScreen() is MainUIScreen)
            {
                yield break;
            }

            yield return UIManager.Instance.PushScreenAndWait<MainUIScreen>();
        }
    }
}
