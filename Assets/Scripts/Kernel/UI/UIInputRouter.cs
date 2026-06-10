using System.Collections;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using Kernel.MapGrid;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        private bool isHandlingHint;
        private bool isHandlingDebugCollect;
        private bool isReturningToStartUpScene;
        private readonly HashSet<Transform> permanentUpgradeInteractorRoots = new();
        private readonly HashSet<Transform> narrativeReaderInteractorRoots = new();

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
        /// summary: 处理不走 InputAction 的调试快捷键（当前用于 O 键一键收取场景中全部 BulletToken pickup）。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void Update()
        {
            TryHandleDebugCollectAllBulletTokensShortcut();
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
        /// summary: 对接 UIControls 的 Interaction 动作；当玩家位于书本交互区内时切换对应界面开关。
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

            if (uiManager.GetTopScreen() is NarrativeMenuUIScreen or NarrativeContentUIScreen)
            {
                RequestCloseNarrativeReaderScreen();
                return;
            }

            if (HasAnyNarrativeReaderInteractor())
            {
                RequestOpenNarrativeReaderScreen();
                return;
            }

            if (!HasAnyPermanentUpgradeInteractor())
            {
                return;
            }

            RequestOpenPermanentUpgradeScreen();
        }

        /// <summary>
        /// summary: 对接 UIControls 的 Hint 动作，按当前 UI 栈开关 Hint 弹窗。
        /// param: context 当前输入回调上下文
        /// returns: 无
        /// </summary>
        public void OnHint(InputAction.CallbackContext context)
        {
            if (!context.performed || isHandlingHint || isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandleHintToggle());
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

        public void RegisterNarrativeReaderInteractor(Transform playerRoot)
        {
            if (playerRoot == null)
            {
                return;
            }

            narrativeReaderInteractorRoots.Add(playerRoot);
        }

        public void UnregisterNarrativeReaderInteractor(Transform playerRoot)
        {
            if (playerRoot == null)
            {
                return;
            }

            narrativeReaderInteractorRoots.Remove(playerRoot);
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
        /// summary: 供 PauseUI 的设置按钮调用，请求在暂停菜单上方打开设置弹窗。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestOpenPauseOptions()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandlePauseOptionsOpen());
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

        public void RequestOpenNarrativeReaderScreen()
        {
            if (isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandleNarrativeReaderScreenOpen());
        }

        public void RequestCloseNarrativeReaderScreen()
        {
            if (isHandlingBack)
            {
                return;
            }

            StartCoroutine(HandleNarrativeReaderScreenClose());
        }

        /// <summary>
        /// summary: 供背包按钮或其他入口复用的 Hint 开关请求。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestToggleHint()
        {
            if (isHandlingHint || isHandlingBack || isReturningToStartUpScene)
            {
                return;
            }

            StartCoroutine(HandleHintToggle());
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
        /// summary: 监听 O 键调试快捷键；仅在 DevMode 且非文本输入互锁时执行一键收取。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryHandleDebugCollectAllBulletTokensShortcut()
        {
            if (isHandlingDebugCollect || !StatusController.HasStatus(StatusList.DevModeStatus))
            {
                return;
            }

            InputActionManager inputManager = InputActionManager.Instance;
            if (inputManager != null && inputManager.IsTextEntryInterlockActive)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.oKey.wasPressedThisFrame)
            {
                return;
            }

            isHandlingDebugCollect = true;
            try
            {
                if (!TryGrantAllRegisteredBulletTokensToInventory(out int movedCount, out int skippedCount, out int registeredCount))
                {
                    return;
                }

                GameDebug.Log($"[UIInputRouter] Debug O granted {movedCount}/{registeredCount} registered BulletToken(s) into inventory. Skipped {skippedCount} token(s) due to capacity.");
            }
            finally
            {
                isHandlingDebugCollect = false;
            }
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
            narrativeReaderInteractorRoots.Clear();

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

                if (uiManager.GetTopModal() is HintUIScreen)
                {
                    yield return uiManager.PopModalAndWait();
                    if (uiManager.GetTopModal() is BackPackUIScreen)
                    {
                        yield break;
                    }

                    if (uiManager.GetTopModal() != null || uiManager.GetTopScreen() is not MainUIScreen)
                    {
                        yield break;
                    }

                    yield return uiManager.ShowModalAndWait<BackPackUIScreen>();
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
        /// summary: 在 Main/BackPack 上开关 Hint 弹窗；Hint 已在栈顶时会优先关闭。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandleHintToggle()
        {
            isHandlingHint = true;

            try
            {
                if (!CanToggleHint(out UIManager uiManager))
                {
                    yield break;
                }

                UIScreen topModal = uiManager.GetTopModal();
                if (topModal is HintUIScreen)
                {
                    yield return uiManager.PopModalAndWait();
                    yield break;
                }

                if (topModal is BackPackUIScreen)
                {
                    yield return uiManager.PopModalAndWait();
                    if (uiManager.GetTopModal() != null || uiManager.GetTopScreen() is not MainUIScreen)
                    {
                        yield break;
                    }

                    yield return uiManager.ShowModalAndWait<HintUIScreen>();
                    yield break;
                }

                if (topModal != null)
                {
                    yield break;
                }

                UIScreen topScreen = uiManager.GetTopScreen();
                if (topScreen is BackPackUIScreen)
                {
                    yield return uiManager.PopScreenAndWait();
                    if (uiManager.GetTopModal() != null || uiManager.GetTopScreen() is not MainUIScreen)
                    {
                        yield break;
                    }

                    yield return uiManager.ShowModalAndWait<HintUIScreen>();
                    yield break;
                }

                if (topScreen is MainUIScreen)
                {
                    yield return uiManager.ShowModalAndWait<HintUIScreen>();
                }
            }
            finally
            {
                isHandlingHint = false;
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
                if (topScreen is StartUpMenuUI)
                {
                    yield return GameExitUIUtility.ShowExitConfirmation(uiManager, nameof(UIInputRouter));
                    yield break;
                }

                if (topScreen is SettlementUIScreen)
                {
                    yield return CloseSettlementScreenAndResetRun(uiManager);
                    yield break;
                }

                if (topScreen is BackPackUIScreen
                    || topScreen is PauseUIScreen
                    || topScreen is UpdateUIScreen
                    || topScreen is NarrativeMenuUIScreen
                    || topScreen is NarrativeContentUIScreen)
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
        /// summary: 只在 PauseUIScreen 位于栈顶且没有其他 modal 时打开设置弹窗。
        /// param: 无
        /// returns: 用于协程等待的枚举器
        /// </summary>
        private IEnumerator HandlePauseOptionsOpen()
        {
            isHandlingBack = true;

            try
            {
                if (!CanOpenPauseOptions(out UIManager uiManager))
                {
                    yield break;
                }

                yield return uiManager.ShowModalAndWait<OptionsUIScreen>();
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

        private IEnumerator HandleNarrativeReaderScreenOpen()
        {
            isHandlingBack = true;

            try
            {
                if (!CanOpenNarrativeReaderScreen(out UIManager uiManager))
                {
                    yield break;
                }

                yield return uiManager.PushScreenAndWait<NarrativeMenuUIScreen>();
            }
            finally
            {
                isHandlingBack = false;
            }
        }

        private IEnumerator HandleNarrativeReaderScreenClose()
        {
            isHandlingBack = true;

            try
            {
                if (!CanCloseNarrativeReaderScreen(out UIManager uiManager))
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

            if (topModal is HintUIScreen)
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
        /// summary: 判断当前是否允许处理 Hint 开关，并返回可用的 UIManager。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 满足 Hint 切换条件时返回 true
        /// </summary>
        private static bool CanToggleHint(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            UIScreen topModal = uiManager.GetTopModal();
            if (topModal is HintUIScreen)
            {
                return true;
            }

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

            if (StatusController.HasStatus(StatusList.InPauseMenuStatus) || StatusController.HasStatus(StatusList.InMainMenuStatus))
            {
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
        /// summary: 判断当前是否允许从暂停菜单打开设置弹窗。
        /// param: uiManager 输出当前可用的 UI 管理器
        /// returns: 只有当 PauseUIScreen 位于栈顶且没有 modal 遮挡时返回 true
        /// </summary>
        private static bool CanOpenPauseOptions(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            return uiManager.GetTopModal() == null && uiManager.GetTopScreen() is PauseUIScreen;
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

        private static bool CanOpenNarrativeReaderScreen(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            if (uiManager.GetTopModal() != null)
            {
                return false;
            }

            if (uiManager.GetTopScreen() is NarrativeMenuUIScreen or NarrativeContentUIScreen)
            {
                return false;
            }

            if (uiManager.GetTopScreen() is not MainUIScreen)
            {
                return false;
            }

            return StatusController.HasStatus(StatusList.PlayingStatus) && FindFirstObjectByType<PlayerPlaneMovement>() != null;
        }

        private static bool CanCloseNarrativeReaderScreen(out UIManager uiManager)
        {
            if (!TryGetAvailableUIManager(out uiManager))
            {
                return false;
            }

            return uiManager.GetTopScreen() is NarrativeMenuUIScreen or NarrativeContentUIScreen;
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

        private bool HasAnyNarrativeReaderInteractor()
        {
            if (narrativeReaderInteractorRoots.Count == 0)
            {
                return false;
            }

            narrativeReaderInteractorRoots.RemoveWhere(root => root == null);
            return narrativeReaderInteractorRoots.Count > 0;
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

        /// <summary>
        /// summary: 扫描已注册在 TokenLibrary/SelectionPlan 中的 BulletToken，并尽可能直接写入玩家库存。
        /// param: movedCount 输出成功放入库存的 token 数量
        /// param: skippedCount 输出因背包容量不足而未放入的 token 数量
        /// param: registeredCount 输出本次可发放的已注册 token 总数
        /// returns: 成功解析到玩家库存时返回 true
        /// </summary>
        private static bool TryGrantAllRegisteredBulletTokensToInventory(out int movedCount, out int skippedCount, out int registeredCount)
        {
            movedCount = 0;
            skippedCount = 0;
            registeredCount = 0;

            PlayerBulletTokenInventory inventory = FindFirstObjectByType<PlayerBulletTokenInventory>();
            if (inventory == null)
            {
                GameDebug.LogWarning("[UIInputRouter] Debug O grant failed because PlayerBulletTokenInventory is missing.");
                return false;
            }

            List<PlaceableTokenData> registeredTokens = CollectRegisteredBulletTokens();
            registeredCount = registeredTokens.Count;
            if (registeredCount <= 0)
            {
                GameDebug.LogWarning("[UIInputRouter] Debug O found no registered BulletToken in TokenLibrary.");
                return true;
            }

            for (int i = 0; i < registeredTokens.Count; i++)
            {
                PlaceableTokenData token = registeredTokens[i];
                if (token == null || token is PickupTokenData)
                {
                    continue;
                }

                if (!inventory.TryAddItem(token, out _))
                {
                    skippedCount++;
                    continue;
                }

                movedCount++;
            }

            return true;
        }

        /// <summary>
        /// summary: 聚合当前工程中已注册的 BulletTokenLibrary 条目并去重。
        /// param: 无
        /// returns: 去重后的可发放 token 列表
        /// </summary>
        private static List<PlaceableTokenData> CollectRegisteredBulletTokens()
        {
            HashSet<int> seenTokenIds = new();
            List<PlaceableTokenData> tokens = new();

#if UNITY_EDITOR
            string[] libraryGuids = AssetDatabase.FindAssets("t:BulletTokenLibrary");
            for (int i = 0; i < libraryGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(libraryGuids[i]);
                BulletTokenLibrary library = AssetDatabase.LoadAssetAtPath<BulletTokenLibrary>(assetPath);
                AppendLibraryTokens(library, tokens, seenTokenIds);
            }
#endif

            BulletTokenLibrary[] loadedLibraries = Resources.FindObjectsOfTypeAll<BulletTokenLibrary>();
            for (int i = 0; i < loadedLibraries.Length; i++)
            {
                AppendLibraryTokens(loadedLibraries[i], tokens, seenTokenIds);
            }

            CombatEntryTokenSelectionPlan[] loadedPlans = Resources.FindObjectsOfTypeAll<CombatEntryTokenSelectionPlan>();
            for (int i = 0; i < loadedPlans.Length; i++)
            {
                AppendPlanLibraryTokens(loadedPlans[i], tokens, seenTokenIds);
            }

            return tokens;
        }

        /// <summary>
        /// summary: 把一个 BulletTokenLibrary 中的 token 追加到结果列表，并按实例去重。
        /// param: library 目标 token 库
        /// param: target 输出列表
        /// param: seenTokenIds 去重集合
        /// returns: 无
        /// </summary>
        private static void AppendLibraryTokens(BulletTokenLibrary library, List<PlaceableTokenData> target, ISet<int> seenTokenIds)
        {
            if (library == null || target == null || seenTokenIds == null)
            {
                return;
            }

            IReadOnlyList<PlaceableTokenData> entries = library.GetTokens();
            for (int i = 0; i < entries.Count; i++)
            {
                PlaceableTokenData token = entries[i];
                if (token == null || token is PickupTokenData)
                {
                    continue;
                }

                int instanceId = token.GetInstanceID();
                if (!seenTokenIds.Add(instanceId))
                {
                    continue;
                }

                target.Add(token);
            }
        }

        /// <summary>
        /// summary: 把一个 SelectionPlan 引用到的所有库中的 token 追加到结果列表，并按实例去重。
        /// param: plan 目标抽取计划
        /// param: target 输出列表
        /// param: seenTokenIds 去重集合
        /// returns: 无
        /// </summary>
        private static void AppendPlanLibraryTokens(CombatEntryTokenSelectionPlan plan, List<PlaceableTokenData> target, ISet<int> seenTokenIds)
        {
            if (plan == null || target == null || seenTokenIds == null)
            {
                return;
            }

            IReadOnlyList<CombatEntryTokenSelectionPlan.LibraryWeightEntry> entries = plan.LibraryEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                BulletTokenLibrary library = entries[i] != null ? entries[i].Library : null;
                AppendLibraryTokens(library, target, seenTokenIds);
            }
        }
    }
}
