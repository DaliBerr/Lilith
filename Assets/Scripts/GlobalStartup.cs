using System;
using System.Collections;
using System.Collections.Generic;
using Kernel.Audio;
using Kernel.Display;
using Kernel.GameState;
using Kernel.Quest;
using Kernel.UI;
using Kernel.Upgrade;
using Vocalith.Localization;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        private static readonly string[] DefaultDataLayerPreloadAddresses =
        {
            "Assets/Data/UI/OptionsCatalog",
            "Assets/Data/Quest/QuestCatalog",
            "Assets/Data/UI/SettlementPresentationCatalog",
            "Assets/Data/Story/Introduction",
            "Assets/Data/UI/HintCatalog",
            "Assets/Data/Story/DialogTest",
            "Assets/Data/Upgrades/PermanentUpgradeCatalog",
            "Assets/Data/Localization/StringTables/ui.zh-Hans-CN",
            "Assets/Data/Localization/StringTables/ui.en-US",
            "Assets/Data/Localization/JsonPatches/en-US/OptionsCatalog.en-US",
            "Assets/Data/Localization/JsonPatches/en-US/HintCatalog.en-US",
            "Assets/Data/Localization/JsonPatches/en-US/SettlementPresentationCatalog.en-US",
            "Assets/Data/Localization/JsonPatches/en-US/QuestCatalog.en-US",
            "Assets/Data/Localization/JsonPatches/en-US/PermanentUpgradeCatalog.en-US",
            "Assets/Data/Localization/JsonPatches/en-US/StorySequence.en-US",
            "Assets/Data/BulletTokens/Core/InitCore",
        };

        [SerializeField] private bool isEnableDevMode = true;
        [Header("Start Story")]
        [SerializeField] private string startStoryAddress = DefaultStartStoryAddress;
        [SerializeField] [Min(0f)] private float startStoryCharactersPerSecond = 24f;
        [SerializeField] [Min(0f)] private float startStoryLineHoldSeconds = 1.2f;

        private readonly List<AsyncOperationHandle<UnityEngine.Object>> preloadedDefHandles = new();
        private bool isBootCompleted;
        private bool isStartGameFlowRequested;
        private bool isLoadingMainScene;
        private bool hasHandedOffToMainScene;
        private bool hasLoadedAllDefs;
        private bool isLoadingAllDefs;
        private bool isStartFlowDataLoadCompleted;
        private float allDefsLoadProgress;

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
            StartupFlowBridge.Register(this, () => isBootCompleted, RequestStartGame, RequestEnterMainScene);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            UnsubscribeFromNarrativeSequence();
            ReleasePreloadedDefHandles();
            StartupFlowBridge.Unregister(this);

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

            StatusController.Initialize();
            ApplyConfiguredModeStatus(GlobalModeSettingsService.LoadMode(ResolveDefaultGameMode()));
            RuntimeSaveService.GetOrCreateInstance();

            StatusController.AddStatus(StatusList.GameLoadingStatus);
            yield return StartCoroutine(InitGlobal());
            yield return InitLanguage();
            LilithDisplaySettings.ApplyStoredDisplaySettings();
            LilithAudioSettings.ApplyStoredSettings();
            ApplyStoredUIScale();

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

        private static void ApplyStoredUIScale()
        {
            UIManager uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                GameDebug.LogWarning("[GlobalStartup] UIManager is missing. Stored UI scale cannot be applied.");
                return;
            }

            float uiScale = OptionsUIScreen.NormalizeUIScaleValue(
                PlayerPrefs.GetFloat(OptionsUIScreen.UIScalePlayerPrefsKey, uiManager.CurrentUIScale));
            uiManager.ApplyUIScale(uiScale);
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
            StartCoroutine(EnterMainSceneAfterDataLoadCo());
            return true;
        }

        /// <summary>
        /// summary: 新档开始时先把 Loading Panel 预压到后台，再播放开场剧情；剧情播放期间并行加载数据层。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator StartGameFlowCo()
        {
            if (UIManager.Instance == null)
            {
                GameDebug.LogError("[GlobalStartup] UIManager is missing. Story intro and loading screen will be skipped.");
                isLoadingMainScene = true;
                yield return StartCoroutine(LoadDefsForStartFlowCo(null));
                yield return StartCoroutine(LoadMainSceneCo());
                yield break;
            }

            isLoadingMainScene = true;
            EnsureGameLoadingStatus();
            UIScreen screenBehindLoading = UIManager.Instance.GetTopScreen();
            yield return UIManager.Instance.PrePushScreenAndWait<LoadingUIScreen>();
            LoadingUIScreen loadingScreen = UIManager.Instance.GetTopScreen(includeInactive: true) as LoadingUIScreen;
            SetLoadingProgress(loadingScreen, 0f);
            if (screenBehindLoading != null && screenBehindLoading.getAlpha() > 0f)
            {
                yield return screenBehindLoading.Hide(UIManager.Instance.defaultHide);
            }

            StartCoroutine(LoadDefsForStartFlowCo(loadingScreen));
            yield return UIManager.Instance.PushScreenAndWait<StoryTellerUIScreen>();

            StoryTellerUIScreen storyScreen = UIManager.Instance.GetTopScreen(includeInactive: true) as StoryTellerUIScreen;
            bool hasStoryScreen = storyScreen != null;
            StorySequenceResult storyResult = new(StorySequenceCompletionStatus.Failed, "Story intro was skipped.");

            if (!hasStoryScreen)
            {
                GameDebug.LogWarning("[GlobalStartup] StoryTellerUIScreen could not be shown. Falling back to Main scene.");
                if (!isStartFlowDataLoadCompleted && loadingScreen != null)
                {
                    yield return loadingScreen.Show(UIManager.Instance.defaultShow);
                }
            }
            else
            {
                StorySequenceParser parser = StorySequenceParser.Instance;
                if (parser == null)
                {
                    GameDebug.LogError("[GlobalStartup] StorySequenceParser is missing. Story intro will be skipped.");
                }
                else
                {
                    bool isStoryCompleted = false;

                    void HandleStoryCompleted(StorySequenceResult result)
                    {
                        storyResult = result;
                        isStoryCompleted = true;
                    }

                    parser.SequenceCompleted += HandleStoryCompleted;
                    try
                    {
                        if (!parser.TryPlay(CreateStartStoryRequest(storyScreen), out string errorMessage))
                        {
                            storyResult = new StorySequenceResult(StorySequenceCompletionStatus.Failed, errorMessage);
                            GameDebug.LogWarning($"[GlobalStartup] Failed to start story intro: {errorMessage}");
                        }
                        else
                        {
                            while (!isStoryCompleted)
                            {
                                yield return null;
                            }
                        }
                    }
                    finally
                    {
                        parser.SequenceCompleted -= HandleStoryCompleted;
                    }
                }
            }

            HandleStartStorySequenceCompleted(storyResult);

            if (UIManager.Instance != null && UIManager.Instance.GetTopScreen(includeInactive: true) is StoryTellerUIScreen)
            {
                if (isStartFlowDataLoadCompleted)
                {
                    yield return UIManager.Instance.PopScreenNoShowAndWait();
                }
                else
                {
                    yield return UIManager.Instance.PopScreenAndWait();
                }
            }

            while (!isStartFlowDataLoadCompleted)
            {
                yield return null;
            }

            SetLoadingProgress(loadingScreen, 1f);
            yield return StartCoroutine(LoadMainSceneCo());
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
        }

        /// <summary>
        /// summary: 预加载启动进入 Main 前需要的 Addressables 数据层资源，并按步骤回报进度。
        /// param name="reportProgress": 可选进度回调，取值范围 0 到 1
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator LoadAllDefsCoroutine(Action<float> reportProgress = null)
        {
            if (hasLoadedAllDefs)
            {
                ReportAllDefsProgress(1f, reportProgress);
                yield break;
            }

            while (isLoadingAllDefs)
            {
                ReportAllDefsProgress(allDefsLoadProgress, reportProgress);
                yield return null;
            }

            if (hasLoadedAllDefs)
            {
                ReportAllDefsProgress(1f, reportProgress);
                yield break;
            }

            isLoadingAllDefs = true;
            bool hasFailedLoad = false;
            List<string> preloadAddresses = BuildDataLayerPreloadAddressList();
            int totalStepCount = Mathf.Max(1, preloadAddresses.Count + 1);
            int completedStepCount = 0;

            ReleasePreloadedDefHandles();
            ReportAllDefsProgress(0f, reportProgress);

            try
            {
                PermanentUpgradeService upgradeService = PermanentUpgradeService.GetOrCreateInstance();
                if (upgradeService != null)
                {
                    yield return upgradeService.LoadCatalogIfNeededCo();
                }

                completedStepCount++;
                ReportAllDefsProgress((float)completedStepCount / totalStepCount, reportProgress);

                for (int i = 0; i < preloadAddresses.Count; i++)
                {
                    string address = preloadAddresses[i];
                    AsyncOperationHandle<UnityEngine.Object> handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
                    preloadedDefHandles.Add(handle);

                    while (!handle.IsDone)
                    {
                        float stepProgress = Mathf.Clamp01(handle.PercentComplete);
                        ReportAllDefsProgress((completedStepCount + stepProgress) / totalStepCount, reportProgress);
                        yield return null;
                    }

                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                    {
                        hasFailedLoad = true;
                        GameDebug.LogError($"[GlobalStartup] Failed to preload data asset at '{address}'.");
                        ReleasePreloadedDefHandle(handle);
                    }

                    completedStepCount++;
                    ReportAllDefsProgress((float)completedStepCount / totalStepCount, reportProgress);
                }

                hasLoadedAllDefs = !hasFailedLoad;
            }
            finally
            {
                isLoadingAllDefs = false;
                ReportAllDefsProgress(1f, reportProgress);
            }
        }

        private IEnumerator LoadDefsForStartFlowCo(LoadingUIScreen loadingScreen)
        {
            isStartFlowDataLoadCompleted = false;
            SetLoadingProgress(loadingScreen, 0f);
            yield return StartCoroutine(LoadAllDefsCoroutine(progress => SetLoadingProgress(loadingScreen, progress)));
            SetLoadingProgress(loadingScreen, 1f);
            isStartFlowDataLoadCompleted = true;
        }

        private IEnumerator EnterMainSceneAfterDataLoadCo()
        {
            LoadingUIScreen loadingScreen = null;
            EnsureGameLoadingStatus();

            if (UIManager.Instance == null)
            {
                GameDebug.LogWarning("[GlobalStartup] UIManager is missing. Loading screen cannot be shown.");
            }
            else
            {
                yield return UIManager.Instance.PushScreenAndWait<LoadingUIScreen>();
                loadingScreen = UIManager.Instance.GetTopScreen(includeInactive: true) as LoadingUIScreen;
            }

            yield return StartCoroutine(LoadDefsForStartFlowCo(loadingScreen));
            yield return StartCoroutine(LoadMainSceneCo());
        }

        private static List<string> BuildDataLayerPreloadAddressList()
        {
            List<string> addresses = new(DefaultDataLayerPreloadAddresses.Length);
            HashSet<string> uniqueAddresses = new(StringComparer.Ordinal);
            for (int i = 0; i < DefaultDataLayerPreloadAddresses.Length; i++)
            {
                string address = DefaultDataLayerPreloadAddresses[i]?.Trim();
                if (!string.IsNullOrEmpty(address) && uniqueAddresses.Add(address))
                {
                    addresses.Add(address);
                }
            }

            return addresses;
        }

        private static void SetLoadingProgress(LoadingUIScreen loadingScreen, float progress)
        {
            if (loadingScreen != null)
            {
                loadingScreen.SetProgress(progress);
            }
        }

        private void ReportAllDefsProgress(float progress, Action<float> reportProgress)
        {
            allDefsLoadProgress = Mathf.Clamp01(progress);
            reportProgress?.Invoke(allDefsLoadProgress);
        }

        private static void EnsureGameLoadingStatus()
        {
            if (!StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                StatusController.AddStatus(StatusList.GameLoadingStatus);
            }
        }

        private void ReleasePreloadedDefHandles()
        {
            for (int i = preloadedDefHandles.Count - 1; i >= 0; i--)
            {
                ReleasePreloadedDefHandle(preloadedDefHandles[i]);
            }

            preloadedDefHandles.Clear();
        }

        private void ReleasePreloadedDefHandle(AsyncOperationHandle<UnityEngine.Object> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            Addressables.Release(handle);
            preloadedDefHandles.Remove(handle);
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
                EnsureGameLoadingStatus();
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
        /// summary: 接收开场剧情的完成结果；成功时写入已读标记，失败或取消只记录日志。
        /// param name="result": 开场剧情的结束结果
        /// returns: 无
        /// </summary>
        private void HandleStartStorySequenceCompleted(StorySequenceResult result)
        {
            UnsubscribeFromNarrativeSequence();

            if (result.Status == StorySequenceCompletionStatus.Completed)
            {
                RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
                saveService?.SetStoryFlag(TutorialQuestConstants.IntroductionReadFlagId, true);
            }
            else if (result.Status == StorySequenceCompletionStatus.Failed)
            {
                GameDebug.LogWarning($"[GlobalStartup] Story intro failed: {result.ErrorMessage}");
            }
            else if (result.Status == StorySequenceCompletionStatus.Cancelled)
            {
                GameDebug.LogWarning("[GlobalStartup] Story intro was cancelled. Falling back to Main scene.");
            }
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
        private StorySequenceRequest CreateStartStoryRequest(StoryTellerUIScreen storyScreen)
        {
            int measuredCapacity = storyScreen != null ? storyScreen.EstimateStoryTextCapacity(260) : 0;
            return new StorySequenceRequest
            {
                Address = string.IsNullOrWhiteSpace(startStoryAddress) ? DefaultStartStoryAddress : startStoryAddress.Trim(),
                CharactersPerSecond = startStoryCharactersPerSecond,
                LineHoldSeconds = startStoryLineHoldSeconds,
                AllowDefaultSkipInput = true,
                MaxCharactersPerEntry = measuredCapacity,
                DisplayTextFitsPage = storyScreen != null ? storyScreen.DoesStoryTextFitPage : null
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
