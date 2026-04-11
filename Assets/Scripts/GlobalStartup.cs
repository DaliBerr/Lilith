using System.Collections;
using Kernel.GameState;
using Kernel.UI;
using Vocalith.Localization;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Kernel
{
    /// <summary>
    /// 启动场景的全局启动器，负责初始化跨场景系统并把流程切到 Main 场景。
    /// </summary>
    public sealed class GlobalStartup : MonoBehaviour
    {
        private const string DefaultStartStoryAddress = "Assets/Data/Story/Introduction";
        public static GlobalStartup Instance { get; private set; }

        private const string MainSceneName = "Main";

        [SerializeField] private bool isEnableDevMode = true;
        [Header("Start Story")]
        [SerializeField] private string startStoryAddress = DefaultStartStoryAddress;
        [SerializeField] [Min(0f)] private float startStoryCharactersPerSecond = 24f;
        [SerializeField] [Min(0f)] private float startStoryLineHoldSeconds = 1.2f;

        private bool isBootCompleted;
        private bool isStartGameFlowRequested;
        private bool isLoadingMainScene;
        private bool hasHandedOffToMainScene;

        public bool IsBootCompleted => isBootCompleted;

        public static class LoggingInit
        {
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            public static void Init()
            {
                LogBootstrap.EnsureInitialized();
                Log.Info("Log bootstrap ok (pid={0})", System.Diagnostics.Process.GetCurrentProcess().Id);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            UnsubscribeFromNarrativeSequence();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Start()
        {
            yield return StartCoroutine(Boot());
        }

        /// <summary>
        /// summary: 初始化全局系统，并在启动菜单场景中通过 UIManager 压入启动菜单。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator Boot()
        {
            isBootCompleted = false;

            yield return InitLanguage();
            StatusController.Initialize();
            ApplyConfiguredModeStatus(GlobalModeSettingsService.LoadMode(ResolveDefaultGameMode()));
            RuntimeSaveService.GetOrCreateInstance();

            StatusController.AddStatus(StatusList.GameLoadingStatus);
            yield return StartCoroutine(InitGlobal());

            if (StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                StatusController.RemoveStatus(StatusList.GameLoadingStatus);
            }

            yield return StartCoroutine(ShowStartUpMenu());
            isBootCompleted = true;
        }

        /// <summary>
        /// summary: 通过持久化 UIManager 压入启动菜单界面，让菜单状态由 StartUpMenuUI 自己维护。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator ShowStartUpMenu()
        {
            if (UIManager.Instance == null)
            {
                GameDebug.LogError("[GlobalStartup] UIManager is missing. StartUp menu cannot be pushed.");
                yield break;
            }

            if (UIManager.Instance.GetTopScreen() is StartUpMenuUI)
            {
                yield break;
            }

            yield return UIManager.Instance.PushScreenAndWait<StartUpMenuUI>();
        }

        /// <summary>
        /// summary: 响应启动菜单的开始请求，先显示剧情介绍界面并启动剧情服务；若任一步失败则直接回退到 Main 场景。
        /// param: 无
        /// returns: 请求被接受并开始执行时返回 true，否则返回 false
        /// </summary>
        public bool RequestStartGame()
        {
            if (!isBootCompleted || isStartGameFlowRequested || isLoadingMainScene || hasHandedOffToMainScene)
            {
                return false;
            }

            isStartGameFlowRequested = true;
            StartCoroutine(StartGameFlowCo());
            return true;
        }

        /// <summary>
        /// summary: 响应启动菜单的开始请求，卸载启动菜单后切到 Main 场景。
        /// param: 无
        /// returns: 无
        /// </summary>
        public bool RequestEnterMainScene()
        {
            if (!isBootCompleted || isLoadingMainScene || hasHandedOffToMainScene)
            {
                return false;
            }

            isLoadingMainScene = true;
            StartCoroutine(LoadMainSceneCo());
            return true;
        }

        /// <summary>
        /// summary: 在 StartUp 菜单后压入剧情介绍界面，并交给 StorySequenceParser 播放默认开场剧情。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator StartGameFlowCo()
        {
            if (UIManager.Instance == null)
            {
                GameDebug.LogError("[GlobalStartup] UIManager is missing. Story intro will be skipped.");
                RequestEnterMainScene();
                yield break;
            }

            yield return UIManager.Instance.PushScreenAndWait<StoryTellerUIScreen>();

            if (UIManager.Instance.GetTopScreen() is not StoryTellerUIScreen)
            {
                GameDebug.LogWarning("[GlobalStartup] StoryTellerUIScreen could not be shown. Falling back to Main scene.");
                RequestEnterMainScene();
                yield break;
            }

            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                GameDebug.LogError("[GlobalStartup] StorySequenceParser is missing. Story intro will be skipped.");
                RequestEnterMainScene();
                yield break;
            }

            UnsubscribeFromNarrativeSequence();
            parser.SequenceCompleted += HandleStartStorySequenceCompleted;

            if (!parser.TryPlay(CreateStartStoryRequest(), out string errorMessage))
            {
                GameDebug.LogWarning($"[GlobalStartup] Failed to start story intro: {errorMessage}");
                UnsubscribeFromNarrativeSequence();
                RequestEnterMainScene();
            }
        }

        /// <summary>
        /// summary: 由 Main 场景的 Startup 在场景内容启动完成后回调，通知全局启动器结束交接。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void NotifyMainSceneStartupComplete()
        {
            if (hasHandedOffToMainScene)
            {
                return;
            }

            hasHandedOffToMainScene = true;
            Destroy(gameObject);
        }

        /// <summary>
        /// summary: 初始化本地化管理器，等待异步加载完成。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator InitLanguage()
        {
            var task = LocalizationManager.InitializeAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                GameDebug.LogError($"InitLanguage failed: {task.Exception}");
            }
        }

        /// <summary>
        /// summary: 初始化跨场景保留的全局系统，例如 Addressables 与 Def 预加载。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator InitGlobal()
        {
            var initHandle = Addressables.InitializeAsync();
            yield return initHandle;

            yield return StartCoroutine(LoadAllDefsCoroutine());
        }

        /// <summary>
        /// summary: 预留所有 Def 与数据库的统一加载入口。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator LoadAllDefsCoroutine()
        {
            yield return null;
        }

        /// <summary>
        /// summary: 从启动菜单切到 Main 场景，并等待 Main 场景自己的 Startup 接手。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator LoadMainSceneCo()
        {
            UnsubscribeFromNarrativeSequence();

            if (StatusController.HasStatus(StatusList.InMainMenuStatus))
            {
                StatusController.RemoveStatus(StatusList.InMainMenuStatus);
            }

            if (!StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                StatusController.AddStatus(StatusList.GameLoadingStatus);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClearAllScreensAndModals();
                while (UIManager.Instance.IsNavigating())
                {
                    yield return null;
                }
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                GameDebug.LogError($"[GlobalStartup] Failed to load scene '{MainSceneName}'.");
                StatusController.RemoveStatus(StatusList.GameLoadingStatus);
                isLoadingMainScene = false;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            for (int frame = 0; frame < 120 && Startup.Instance == null; frame++)
            {
                yield return null;
            }

            if (Startup.Instance == null)
            {
                GameDebug.LogError("[GlobalStartup] Main scene Startup is missing.");
                StatusController.RemoveStatus(StatusList.GameLoadingStatus);
            }

            isLoadingMainScene = false;
        }

        /// <summary>
        /// summary: 接收开场剧情的完成结果；当前只要剧情正常结束、失败或取消，都会继续进入 Main 场景。
        /// param name="result": 开场剧情的结束结果
        /// returns: 无
        /// </summary>
        private void HandleStartStorySequenceCompleted(StorySequenceResult result)
        {
            UnsubscribeFromNarrativeSequence();

            if (result.Status == StorySequenceCompletionStatus.Failed)
            {
                GameDebug.LogWarning($"[GlobalStartup] Story intro failed: {result.ErrorMessage}");
            }
            else if (result.Status == StorySequenceCompletionStatus.Cancelled)
            {
                GameDebug.LogWarning("[GlobalStartup] Story intro was cancelled. Falling back to Main scene.");
            }

            RequestEnterMainScene();
        }

        /// <summary>
        /// summary: 清理对当前 narrative 序列完成事件的订阅，避免 GlobalStartup 销毁后残留委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static void UnsubscribeFromNarrativeSequence()
        {
            GlobalStartup startup = Instance;
            if (startup == null)
            {
                return;
            }

            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                return;
            }

            parser.SequenceCompleted -= startup.HandleStartStorySequenceCompleted;
        }

        /// <summary>
        /// summary: 构造开场 storyteller 的播放请求，保持当前旁白逐字播放行为不变。
        /// param: 无
        /// returns: 可直接提交给 StorySequenceParser 的请求对象
        /// </summary>
        private StorySequenceRequest CreateStartStoryRequest()
        {
            return new StorySequenceRequest
            {
                Address = string.IsNullOrWhiteSpace(startStoryAddress) ? DefaultStartStoryAddress : startStoryAddress.Trim(),
                CharactersPerSecond = startStoryCharactersPerSecond,
                LineHoldSeconds = startStoryLineHoldSeconds,
                AllowDefaultSkipInput = true
            };
        }

        /// <summary>
        /// summary: 把 Inspector 中的默认开关转换为首次启动时使用的全局模式默认值。
        /// param: 无
        /// returns: 首次启动应写入 global-mode.json 的默认模式
        /// </summary>
        private GameMode ResolveDefaultGameMode()
        {
            return isEnableDevMode ? GameMode.Dev : GameMode.Normal;
        }

        /// <summary>
        /// summary: 按全局模式配置在状态栈中只保留一个模式状态，避免 Dev/Normal 同时存在。
        /// param name="mode": 当前需要应用的模式配置
        /// returns: 无
        /// </summary>
        private static void ApplyConfiguredModeStatus(GameMode mode)
        {
            if (StatusController.HasStatus(StatusList.DevModeStatus))
            {
                StatusController.RemoveStatus(StatusList.DevModeStatus);
            }

            if (StatusController.HasStatus(StatusList.NormalModeStatus))
            {
                StatusController.RemoveStatus(StatusList.NormalModeStatus);
            }

            StatusController.AddStatus(mode == GameMode.Dev ? StatusList.DevModeStatus : StatusList.NormalModeStatus);
        }
    }
}
