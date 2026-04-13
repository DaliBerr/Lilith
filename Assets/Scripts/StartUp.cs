using System.Collections;
using Kernel.Quest;
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
        private const string DefaultOpeningGuideAddress = "Assets/Data/Story/DialogTest";

        public static Startup Instance { get; private set; }

        [Header("Opening Guide")]
        [SerializeField] private bool playOpeningGuideOnNewProfile = true;
        [SerializeField] private string openingGuideAddress = DefaultOpeningGuideAddress;
        [SerializeField] [Min(0f)] private float openingGuideCharactersPerSecond = 36f;
        [SerializeField] [Min(0f)] private float openingGuideLineHoldSeconds = 0f;
        [SerializeField] [Min(1)] private int openingGuideMaxCharactersPerEntry = 260;

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
            QuestService.GetOrCreateInstance()?.BeginRuntime();

            if (StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                StatusController.RemoveStatus(StatusList.GameLoadingStatus);
            }

            yield return StartCoroutine(PlayOpeningGuideIfNeededCo());

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

        /// <summary>
        /// summary: 若当前档位是新档，则在 MainUIScreen 上叠加开场引导对话 modal；结束后关闭该 modal。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator PlayOpeningGuideIfNeededCo()
        {
            if (!ShouldPlayOpeningGuide())
            {
                yield break;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null || !saveService.TryConsumePendingOpeningGuideOnMainSceneEntry())
            {
                yield break;
            }

            if (UIManager.Instance == null)
            {
                GameDebug.LogWarning("[Startup] UIManager is missing. Opening guide will be skipped.");
                yield break;
            }

            yield return UIManager.Instance.ShowModalAndWait<DialogUIScreen>();
            if (UIManager.Instance.GetTopModal() is not DialogUIScreen dialogScreen)
            {
                GameDebug.LogWarning("[Startup] DialogUIScreen could not be shown. Opening guide will be skipped.");
                yield break;
            }

            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                GameDebug.LogWarning("[Startup] StorySequenceParser is missing. Opening guide will be skipped.");
                yield return UIManager.Instance.PopModalAndWait();
                yield break;
            }

            bool isCompleted = false;
            StorySequenceResult guideResult = default;

            void HandleGuideCompleted(StorySequenceResult result)
            {
                guideResult = result;
                isCompleted = true;
            }

            parser.SequenceCompleted += HandleGuideCompleted;
            try
            {
                if (!parser.TryPlay(CreateOpeningGuideRequest(), out string errorMessage))
                {
                    GameDebug.LogWarning($"[Startup] Failed to start opening guide dialog: {errorMessage}");
                    yield return UIManager.Instance.PopModalAndWait();
                    yield break;
                }

                while (!isCompleted)
                {
                    yield return null;
                }
            }
            finally
            {
                parser.SequenceCompleted -= HandleGuideCompleted;
            }

            if (guideResult.Status == StorySequenceCompletionStatus.Failed)
            {
                GameDebug.LogWarning($"[Startup] Opening guide dialog failed: {guideResult.ErrorMessage}");
            }
            else if (guideResult.Status == StorySequenceCompletionStatus.Cancelled)
            {
                GameDebug.LogWarning("[Startup] Opening guide dialog was cancelled.");
            }

            if (UIManager.Instance.GetTopModal() == dialogScreen)
            {
                yield return UIManager.Instance.PopModalAndWait();
            }
        }

        /// <summary>
        /// summary: 判断当前是否允许在 Main 场景首进时播放开场引导对话。
        /// param: 无
        /// returns: 功能启用且对话地址有效时返回 true
        /// </summary>
        private bool ShouldPlayOpeningGuide()
        {
            return playOpeningGuideOnNewProfile && !string.IsNullOrWhiteSpace(openingGuideAddress);
        }

        /// <summary>
        /// summary: 构造 Main 场景开场引导对话请求，使用分页对白并等待玩家手动推进。
        /// param: 无
        /// returns: 可直接提交给 StorySequenceParser 的请求对象
        /// </summary>
        private StorySequenceRequest CreateOpeningGuideRequest()
        {
            return new StorySequenceRequest
            {
                Address = string.IsNullOrWhiteSpace(openingGuideAddress) ? DefaultOpeningGuideAddress : openingGuideAddress.Trim(),
                CharactersPerSecond = openingGuideCharactersPerSecond,
                LineHoldSeconds = openingGuideLineHoldSeconds,
                AllowDefaultSkipInput = false,
                MaxCharactersPerEntry = Mathf.Max(1, openingGuideMaxCharactersPerEntry),
                WaitForAdvanceInputAfterEntryReveal = true
            };
        }
    }
}
