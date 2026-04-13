using System.Collections;
using System.Collections.Generic;
using Kernel.GameState;
using Kernel.MapGrid;
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
        private readonly HashSet<Transform> permanentUpgradeInteractorRoots = new();

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
        /// summary: 对接 UIControls 的 Interaction 动作；当玩家位于永久升级触发区内时切换升级界面开关。
        /// param: context 当前输入回调上下文
        /// returns: 无
        /// </summary>
        public void OnInteraction(InputAction.CallbackContext context)
        {
            if (!context.performed || isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            if (!TryGetAvailableUIManager(out UIManager uiManager) || uiManager.GetTopModal() != null)
            {
                return;
            }

            if (uiManager.GetTopScreen() is UpdateUIScreen)
            {
                RequestClosePermanentUpgradeScreen();
                return;
            }

            if (!HasAnyPermanentUpgradeInteractor())
            {
                return;
            }

            RequestOpenPermanentUpgradeScreen();
        }

        /// <summary>
        /// summary: 标记某个玩家根节点进入了永久升级交互区，允许 Interaction 按键触发开关。
        /// param: playerRoot 当前进入交互区的玩家根节点
        /// returns: 无
        /// </summary>
        public void RegisterPermanentUpgradeInteractor(Transform playerRoot)
        {
            if (playerRoot == null)
            {
                return;
            }

            permanentUpgradeInteractorRoots.Add(playerRoot);
        }

        /// <summary>
        /// summary: 清理某个玩家根节点的永久升级交互资格，离开交互区后 Interaction 不再触发开关。
        /// param: playerRoot 当前离开交互区的玩家根节点
        /// returns: 无
        /// </summary>
        public void UnregisterPermanentUpgradeInteractor(Transform playerRoot)
        {
            if (playerRoot == null)
            {
                return;
            }

            permanentUpgradeInteractorRoots.Remove(playerRoot);
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
        /// summary: 供 PauseUI 的设置按钮调用；当前弹出统一的未实现提示窗口。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestOpenPauseOptions()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            if (!TryGetAvailableUIManager(out UIManager uiManager))
            {
                return;
            }

            StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                uiManager,
                nameof(UIInputRouter),
                "设置功能暂未实现，后续会在这里接入暂停菜单选项配置。"));
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
        /// summary: 供 Book trigger 调用，请求从 MainUIScreen 打开永久升级界面。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestOpenPermanentUpgradeScreen()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandlePermanentUpgradeScreenOpen());
        }

        /// <summary>
        /// summary: 请求关闭当前永久升级界面，供未来按钮或其他入口复用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClosePermanentUpgradeScreen()
        {
            if (isHandlingBack)
            {
                return;
            }

            StartCoroutine(HandlePermanentUpgradeScreenClose());
        }

        /// <summary>
        /// summary: 请求关闭当前结算界面，并在关闭后提交 run-end 永久档与回到 StartRoom。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestCloseSettlementScreen()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandleSettlementScreenClose());
        }

        /// <summary>
        /// summary: 销毁前移除输入回调，避免 InputSystem 留下失效订阅。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void OnDestroy()
        {
            UnbindInputCallbacks();
            permanentUpgradeInteractorRoots.Clear();

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

                if (uiManager.GetTopModal() is BackPackUIScreen)
                {
                    yield return uiManager.PopModalAndWait();
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

                yield return uiManager.ShowModalAndWait<BackPackUIScreen>();
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
                if (topScreen is SettlementUIScreen)
                {
                    yield return CloseSettlementScreenAndResetRun(uiManager);
                    yield break;
                }

                if (topScreen is BackPackUIScreen || topScreen is PauseUIScreen || topScreen is UpdateUIScreen)
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
        /// summary: 只在 MainUIScreen 位于栈顶时打开永久升级菜单，供 Book trigger 复用。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandlePermanentUpgradeScreenOpen()
        {
            isHandlingBack = true;

            try
            {
                if (!CanOpenPermanentUpgradeScreen(out UIManager uiManager))
                {
                    yield break;
                }

                yield return uiManager.PushScreenAndWait<UpdateUIScreen>();
            }
            finally
            {
                isHandlingBack = false;
            }
        }

        /// <summary>
        /// summary: 只在永久升级菜单位于栈顶时关闭它。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandlePermanentUpgradeScreenClose()
        {
            isHandlingBack = true;

            try
            {
                if (!CanClosePermanentUpgradeScreen(out UIManager uiManager))
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
        /// summary: 只在结算界面位于栈顶时关闭它，并在关闭后提交存档与重置本局状态。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandleSettlementScreenClose()
        {
            isHandlingBack = true;

            try
            {
                if (!CanCloseSettlementScreen(out UIManager uiManager))
                {
                    yield break;
                }

                yield return CloseSettlementScreenAndResetRun(uiManager);
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

            UIScreen topModal = uiManager.GetTopModal();
            if (topModal is BackPackUIScreen)
            {
                return true;
            }

            if (topModal != null)
            {
                return false;
            }

            UIScreen topScreen = uiManager.GetTopScreen();
            if (topScreen is BackPackUIScreen)
            {
                return true;
            }

            if (topScreen is not MainUIScreen)
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
        /// summary: 判断当前是否允许从 MainUIScreen 打开永久升级菜单。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 MainUIScreen 位于栈顶且当前处于 Playing 状态时返回 true
        /// </summary>
        private static bool CanOpenPermanentUpgradeScreen(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            if (uiManager.GetTopModal() != null)
            {
                return false;
            }

            if (uiManager.GetTopScreen() is UpdateUIScreen)
            {
                return false;
            }

            if (uiManager.GetTopScreen() is not MainUIScreen)
            {
                return false;
            }

            return StatusController.HasStatus(StatusList.PlayingStatus) && FindFirstObjectByType<PlayerPlaneMovement>() != null;
        }

        /// <summary>
        /// summary: 判断当前是否允许关闭永久升级菜单。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 UpdateUIScreen 位于栈顶时返回 true
        /// </summary>
        private static bool CanClosePermanentUpgradeScreen(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            return uiManager.GetTopScreen() is UpdateUIScreen;
        }

        /// <summary>
        /// summary: 判断当前是否允许关闭结算界面。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 SettlementUIScreen 位于栈顶时返回 true
        /// </summary>
        private static bool CanCloseSettlementScreen(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            return uiManager.GetTopModal() == null && uiManager.GetTopScreen() is SettlementUIScreen;
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
        /// summary: 判断当前是否至少有一个仍有效的玩家根节点处于永久升级交互区内。
        /// param: 无
        /// returns: 存在有效交互者时返回 true
        /// </summary>
        private bool HasAnyPermanentUpgradeInteractor()
        {
            if (permanentUpgradeInteractorRoots.Count == 0)
            {
                return false;
            }

            permanentUpgradeInteractorRoots.RemoveWhere(root => root == null);
            return permanentUpgradeInteractorRoots.Count > 0;
        }

        /// <summary>
        /// summary: 在原生返回路由关闭 modal 前，补充各界面自有的收尾逻辑。
        /// param: modal 即将被关闭的顶层界面
        /// returns: 无
        /// </summary>
        private static void PrepareModalForClose(UIScreen modal)
        {
            if (modal is DialogUIScreen)
            {
                // 对话 modal 被 Esc 关闭时，需要同时结束剧情序列，避免启动协程一直等待完成事件。
                StorySequenceParser.Instance?.StopCurrentSequence();
            }
        }

        /// <summary>
        /// summary: 执行结算页关闭后的完整收尾流程：弹栈、提交 run-end 永久档并回到 StartRoom。
        /// param name="uiManager": 当前可用的 UIManager
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private static IEnumerator CloseSettlementScreenAndResetRun(UIManager uiManager)
        {
            if (uiManager == null || uiManager.GetTopScreen() is not SettlementUIScreen)
            {
                yield break;
            }

            yield return uiManager.PopScreenAndWait();

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService != null && !saveService.CommitRunEndProfileState())
            {
                GameDebug.LogWarning("[UIInputRouter] Failed to commit run-end profile state after closing SettlementUIScreen.");
            }

            MapRunFlowController flowController = FindFirstObjectByType<MapRunFlowController>();
            if (flowController == null)
            {
                GameDebug.LogError("[UIInputRouter] MapRunFlowController is missing. Settlement close cannot return the player to StartRoom.");
                yield break;
            }

            if (!flowController.TryReturnToStartRoomAndResetRun(out string error))
            {
                GameDebug.LogError($"[UIInputRouter] {error}");
            }
        }
    }
}
