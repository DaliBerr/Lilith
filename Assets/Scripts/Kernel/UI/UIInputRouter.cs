using System.Collections;
using Kernel.GameState;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Kernel.UI
{
    /// <summary>
    /// 统一消费 UIControls 的界面级输入，并把返回键与背包键路由到当前 UI 栈。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class UIInputRouter : MonoBehaviour, UIControls.IMainSceneUIActions
    {
        public static UIInputRouter Instance { get; private set; }

        private const string StartUpSceneName = "StartUp";

        private UIControls boundControls;
        private bool callbacksBound;
        private bool isHandlingBack;
        private bool isHandlingBackpack;
        private bool isReturningToStartUpScene;

        /// <summary>
        /// summary: 在首个场景加载前确保场景中存在 UI 输入路由实例。
        /// param: 无
        /// returns: 无
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (Instance != null)
            {
                return;
            }

            if (FindFirstObjectByType<UIInputRouter>() != null)
            {
                return;
            }

            GameObject bootstrapObject = new(nameof(UIInputRouter));
            bootstrapObject.AddComponent<UIInputRouter>();
        }

        /// <summary>
        /// summary: 初始化单例并保持路由器跨场景存活。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryBindInputCallbacks();
        }

        /// <summary>
        /// summary: 等待 InputActionManager 完成初始化后再绑定 UIControls 回调。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator Start()
        {
            while (!TryBindInputCallbacks())
            {
                yield return null;
            }
        }

        /// <summary>
        /// summary: 对接 UIControls 的背包动作，只在 performed 阶段执行一次切换。
        /// param: context 当前输入回调上下文
        /// returns: 无
        /// </summary>
        public void OnBackpack(InputAction.CallbackContext context)
        {
            if (!context.performed || isHandlingBackpack)
            {
                return;
            }

            StartCoroutine(HandleBackpackToggle());
        }

        /// <summary>
        /// summary: 对接 UIControls 的 Router 动作，用于处理 Esc 返回链路。
        /// param: context 当前输入回调上下文
        /// returns: 无
        /// </summary>
        public void OnRouter(InputAction.CallbackContext context)
        {
            if (!context.performed || isHandlingBack)
            {
                return;
            }

            StartCoroutine(HandleBack());
        }

        /// <summary>
        /// summary: 供 MainUI 的暂停按钮调用，请求打开暂停菜单。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestOpenPauseMenu()
        {
            if (isHandlingBack)
            {
                return;
            }

            StartCoroutine(HandlePauseMenuOpen());
        }

        /// <summary>
        /// summary: 供 PauseUI 的恢复按钮调用，请求关闭当前暂停菜单。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClosePauseMenu()
        {
            if (isHandlingBack)
            {
                return;
            }

            StartCoroutine(HandlePauseMenuClose());
        }

        /// <summary>
        /// summary: 供 PauseUI 的设置按钮调用；当前先保留统一的未实现提示入口。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestOpenPauseOptions()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            GameDebug.LogWarning("[UIInputRouter] Pause options is not implemented yet.");
        }

        /// <summary>
        /// summary: 供 PauseUI 的返回按钮调用，请求清空当前战斗 UI 并切回 StartUp 场景。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestReturnToStartUpScene()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandleReturnToStartUpScene());
        }

        /// <summary>
        /// summary: 销毁前移除输入回调，避免 InputSystem 留下失效订阅。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void OnDestroy()
        {
            UnbindInputCallbacks();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// summary: 从 InputActionManager 持有的唯一 UIControls 实例上注册当前路由器。
        /// param: 无
        /// returns: 成功绑定回调时返回 true
        /// </summary>
        private bool TryBindInputCallbacks()
        {
            if (callbacksBound)
            {
                return true;
            }

            InputActionManager inputManager = InputActionManager.Instance;
            if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.UI == null)
            {
                return false;
            }

            boundControls = inputManager.UI;
            boundControls.MainSceneUI.AddCallbacks(this);
            callbacksBound = true;
            return true;
        }

        /// <summary>
        /// summary: 从已绑定的 UIControls 上移除当前路由器的回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindInputCallbacks()
        {
            if (!callbacksBound || boundControls == null)
            {
                return;
            }

            boundControls.MainSceneUI.RemoveCallbacks(this);
            boundControls = null;
            callbacksBound = false;
        }

        /// <summary>
        /// summary: 根据当前顶层 UI 和状态切换背包界面，忽略菜单态、暂停态和过渡期误触。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandleBackpackToggle()
        {
            isHandlingBackpack = true;

            try
            {
                if (!CanToggleBackpack(out UIManager uiManager))
                {
                    yield break;
                }

                if (uiManager.GetTopScreen() is BackPackUIScreen)
                {
                    yield return uiManager.PopScreenAndWait();
                    yield break;
                }

                if (uiManager.GetTopScreen() is not MainUIScreen)
                {
                    yield break;
                }

                yield return uiManager.PushScreenAndWait<BackPackUIScreen>();
            }
            finally
            {
                isHandlingBackpack = false;
            }
        }

        /// <summary>
        /// summary: 根据当前 UI 栈处理返回键，优先关 modal，其次关闭背包或暂停菜单。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandleBack()
        {
            isHandlingBack = true;

            try
            {
                if (!TryGetAvailableUIManager(out UIManager uiManager))
                {
                    yield break;
                }

                UIScreen topModal = uiManager.GetTopModal();
                if (topModal != null)
                {
                    PrepareModalForClose(topModal);
                    yield return uiManager.PopModalAndWait();
                    yield break;
                }

                UIScreen topScreen = uiManager.GetTopScreen();
                if (topScreen is BackPackUIScreen || topScreen is PauseUIScreen)
                {
                    yield return uiManager.PopScreenAndWait();
                    yield break;
                }

                if (topScreen is MainUIScreen && StatusController.HasStatus(StatusList.PlayingStatus))
                {
                    yield return uiManager.PushScreenAndWait<PauseUIScreen>();
                }
            }
            finally
            {
                isHandlingBack = false;
            }
        }

        /// <summary>
        /// summary: 只在 MainUIScreen 位于栈顶时打开暂停菜单，供按钮点击与输入路由共用。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandlePauseMenuOpen()
        {
            isHandlingBack = true;

            try
            {
                if (!CanOpenPauseMenu(out UIManager uiManager))
                {
                    yield break;
                }

                yield return uiManager.PushScreenAndWait<PauseUIScreen>();
            }
            finally
            {
                isHandlingBack = false;
            }
        }

        /// <summary>
        /// summary: 只在 PauseUIScreen 位于栈顶时关闭暂停菜单，供恢复按钮复用。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandlePauseMenuClose()
        {
            isHandlingBack = true;

            try
            {
                if (!CanClosePauseMenu(out UIManager uiManager))
                {
                    yield break;
                }

                yield return uiManager.PopScreenAndWait();
            }
            finally
            {
                isHandlingBack = false;
            }
        }

        /// <summary>
        /// summary: 从暂停菜单直接返回 StartUp 场景；先清理当前 UI 栈，再触发场景切换。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandleReturnToStartUpScene()
        {
            isHandlingBack = true;
            isReturningToStartUpScene = true;

            try
            {
                if (!CanReturnToStartUpScene(out UIManager uiManager))
                {
                    yield break;
                }

                if (StatusController.HasStatus(StatusList.InPauseMenuStatus))
                {
                    StatusController.RemoveStatus(StatusList.InPauseMenuStatus);
                }

                if (!StatusController.HasStatus(StatusList.GameLoadingStatus))
                {
                    StatusController.AddStatus(StatusList.GameLoadingStatus);
                }

                uiManager.ClearAllScreensAndModals();
                while (uiManager.IsNavigating())
                {
                    yield return null;
                }

                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(StartUpSceneName, LoadSceneMode.Single);
                if (loadOperation == null)
                {
                    GameDebug.LogError($"[UIInputRouter] Failed to load scene '{StartUpSceneName}'.");
                    if (StatusController.HasStatus(StatusList.GameLoadingStatus))
                    {
                        StatusController.RemoveStatus(StatusList.GameLoadingStatus);
                    }

                    yield break;
                }

                while (!loadOperation.isDone)
                {
                    yield return null;
                }
            }
            finally
            {
                isReturningToStartUpScene = false;
                isHandlingBack = false;
            }
        }

        /// <summary>
        /// summary: 判断当前是否允许处理背包快捷键，并返回可用的 UIManager。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 满足战斗态切换背包条件时返回 true
        /// </summary>
        private static bool CanToggleBackpack(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            if (uiManager.GetTopModal() != null)
            {
                return false;
            }

            if (uiManager.GetTopScreen() is BackPackUIScreen)
            {
                return true;
            }

            if (uiManager.GetTopScreen() is not MainUIScreen)
            {
                return false;
            }

            if (!StatusController.HasStatus(StatusList.PlayingStatus))
            {
                return false;
            }

            if (StatusController.HasStatus(StatusList.InMainMenuStatus) || StatusController.HasStatus(StatusList.InPauseMenuStatus))
            {
                return false;
            }

            if (FindFirstObjectByType<PlayerPlaneMovement>() == null)
            {
                GameDebug.LogWarning("[UIInputRouter] Backpack toggle was requested, but no PlayerPlaneMovement exists in the active scene.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 判断当前是否允许从 MainUIScreen 打开暂停菜单。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 MainUIScreen 位于栈顶且当前处于 Playing 状态时返回 true
        /// </summary>
        private static bool CanOpenPauseMenu(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            if (uiManager.GetTopModal() != null || uiManager.GetTopScreen() is not MainUIScreen)
            {
                return false;
            }

            return StatusController.HasStatus(StatusList.PlayingStatus);
        }

        /// <summary>
        /// summary: 判断当前是否允许关闭暂停菜单。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 PauseUIScreen 位于栈顶时返回 true
        /// </summary>
        private static bool CanClosePauseMenu(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            return uiManager.GetTopScreen() is PauseUIScreen;
        }

        /// <summary>
        /// summary: 判断当前是否允许从暂停菜单直接返回 StartUp 场景。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 PauseUIScreen 位于栈顶且没有 modal 遮挡时返回 true
        /// </summary>
        private static bool CanReturnToStartUpScene(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            if (uiManager.GetTopModal() != null)
            {
                return false;
            }

            return uiManager.GetTopScreen() is PauseUIScreen;
        }

        /// <summary>
        /// summary: 统一校验当前是否有可用于 UI 路由的 UIManager，且它不处于导航过渡中。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 可安全执行 UI 路由时返回 true
        /// </summary>
        private static bool TryGetAvailableUIManager(out UIManager uiManager)
        {
            uiManager = UIManager.Instance;
            return uiManager != null && !uiManager.IsNavigating();
        }

        /// <summary>
        /// summary: 在原生返回路由关闭 modal 前，补充各界面自有的收尾逻辑。
        /// param: modal 即将被关闭的顶层界面
        /// returns: 无
        /// </summary>
        private static void PrepareModalForClose(UIScreen modal)
        {
        }
    }
}
