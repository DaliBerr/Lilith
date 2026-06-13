using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Runtime.Serialization;
using Vocalith.Localization;

namespace Kernel.UI
{
    /// <summary>
    /// 剧情序列播放结果状态。
    /// </summary>
    public enum StorySequenceCompletionStatus
    {
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 单条剧情文本的显示模式。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StorySequenceDisplayMode
    {
        [EnumMember(Value = "replace")]
        Replace = 0,

        [EnumMember(Value = "append")]
        Append = 1
    }

    /// <summary>
    /// 剧情单条文本数据。
    /// </summary>
    [Serializable]
    public sealed class StorySequenceEntry
    {
        public string SpeakerId { get; set; }
        public string DisplayName { get; set; }
        public string Text { get; set; }
        public StorySequenceDisplayMode DisplayMode { get; set; } = StorySequenceDisplayMode.Replace;
    }

    /// <summary>
    /// 剧情序列数据根对象。
    /// </summary>
    [Serializable]
    public sealed class StorySequenceData
    {
        public List<StorySequenceEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// 发起剧情序列播放时使用的配置请求。
    /// </summary>
    public sealed class StorySequenceRequest
    {
        public string Address { get; set; }
        public float CharactersPerSecond { get; set; } = 24f;
        public float LineHoldSeconds { get; set; } = 1.2f;
        public bool AllowDefaultSkipInput { get; set; } = true;
        public int MaxCharactersPerEntry { get; set; }
        public Func<string, bool> DisplayTextFitsPage { get; set; }
        public bool WaitForAdvanceInputAfterEntryReveal { get; set; }
    }

    /// <summary>
    /// 对外广播的剧情播放快照。
    /// </summary>
    public readonly struct StorySequenceSnapshot
    {
        public StorySequenceSnapshot(
            string speakerId,
            string displayName,
            string fullText,
            int visibleCharacterCount,
            int entryIndex,
            int entryCount,
            bool isEntryFullyRevealed,
            bool shouldShowSkipButton)
        {
            SpeakerId = speakerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FullText = fullText ?? string.Empty;
            VisibleCharacterCount = Mathf.Max(0, visibleCharacterCount);
            EntryIndex = Mathf.Max(0, entryIndex);
            EntryCount = Mathf.Max(0, entryCount);
            IsEntryFullyRevealed = isEntryFullyRevealed;
            ShouldShowSkipButton = shouldShowSkipButton;
        }

        public string SpeakerId { get; }
        public string DisplayName { get; }
        public string FullText { get; }
        public int VisibleCharacterCount { get; }
        public int EntryIndex { get; }
        public int EntryCount { get; }
        public bool IsEntryFullyRevealed { get; }
        public bool ShouldShowSkipButton { get; }
    }

    /// <summary>
    /// 对外广播的剧情播放完成结果。
    /// </summary>
    public readonly struct StorySequenceResult
    {
        public StorySequenceResult(StorySequenceCompletionStatus status, string errorMessage = null)
        {
            Status = status;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public StorySequenceCompletionStatus Status { get; }
        public string ErrorMessage { get; }
    }

    /// <summary>
    /// 统一负责剧情序列的加载、解析、逐字播放、跳过与完成回调。
    /// </summary>
    [DisallowMultipleComponent]
    public class StorySequenceParser : MonoBehaviour
    {
        private const string DefaultSpeakerId = "narrator";
        private const string CancelledMessage = "Story sequence was cancelled.";

        public static StorySequenceParser Instance { get; private set; }

        private Coroutine activeSequenceRoutine;
        private AsyncOperationHandle<TextAsset> activeTextAssetHandle;
        private bool hasActiveTextAssetHandle;
        private bool isPlaying;
        private bool skipCurrentEntryRequested;
        private bool skipToNextReplaceRequested;
        private bool advanceToNextEntryRequested;
        private bool advanceDisplayBlockRequested;
        private bool advanceDisplayBlockNormallyRequested;
        private bool finishAfterFinalBlockRequested;
        private bool waitForSkipConfirmationOnFinalBlock;
        private bool allowDefaultSkipInput;
        private bool shouldShowSkipButton;
        private int currentDisplayBlockEndIndex = -1;
        private StorySequenceSnapshot currentSnapshot;
        private bool hasCurrentSnapshot;

        public bool IsPlaying => isPlaying;

        public event Action<StorySequenceSnapshot> SnapshotChanged;
        public event Action<StorySequenceResult> SequenceCompleted;

        /// <summary>
        /// summary: 在首个场景加载前确保存在唯一的剧情序列服务对象。
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

            if (FindFirstObjectByType<StorySequenceParser>() != null)
            {
                return;
            }

            GameObject runtimeObject = new(nameof(StorySequenceParser));
            runtimeObject.AddComponent<StorySequenceParser>();
        }

        /// <summary>
        /// summary: 初始化剧情服务单例，并在运行时保持跨场景存活。
        /// param: 无
        /// returns: 无
        /// </summary>
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// summary: 统一处理默认跳过输入；当前支持空格和鼠标左键补完当前句，并在第一次使用后显示跳过按钮。
        /// param: 无
        /// returns: 无
        /// </summary>
        protected virtual void Update()
        {
            if (!isPlaying || !allowDefaultSkipInput)
            {
                return;
            }

            if (ShouldConsumeDefaultSkipInput())
            {
                NotifyAdvanceShortcutUsed();
                RequestSkipCurrentEntry();
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                if (isPlaying)
                {
                    StopCurrentSequence();
                }

                Instance = null;
            }
        }

        /// <summary>
        /// summary: 尝试启动一个新的剧情序列；当前若已有活动序列则会直接拒绝。
        /// param name="request": 本次播放请求
        /// param name="errorMessage": 启动失败时的错误原因
        /// returns: 请求被接受并开始执行时返回 true，否则返回 false
        /// </summary>
        public bool TryPlay(StorySequenceRequest request, out string errorMessage)
        {
            if (!TryBeginSequence(request, out errorMessage))
            {
                return false;
            }

            activeSequenceRoutine = StartCoroutine(PlaySequenceFromRequestCo(request));
            return true;
        }

        /// <summary>
        /// summary: 请求立刻补完当前句；不会跳过句间停留或直接进入下一句。
        /// param: 无
        /// returns: 无
        /// </summary>
        public virtual void RequestSkipCurrentEntry()
        {
            if (!isPlaying)
            {
                return;
            }

            skipCurrentEntryRequested = true;
        }

        /// <summary>
        /// summary: 请求直接跳过到下一条 replace 边界；若当前已是最后一个显示块，则会直接结束播放。
        /// param: 无
        /// returns: 无
        /// </summary>
        public virtual void RequestSkipToNextReplaceOrFinish()
        {
            if (!isPlaying)
            {
                return;
            }

            if (IsCurrentSnapshotAtFullyRevealedFinalDisplayBlock())
            {
                finishAfterFinalBlockRequested = true;
                return;
            }

            if (IsCurrentSnapshotAtFullyRevealedDisplayBlockBoundary())
            {
                advanceDisplayBlockRequested = true;
                return;
            }

            skipToNextReplaceRequested = true;
        }

        /// <summary>
        /// summary: 请求跳过当前显示页；若当前页还在逐字播放，会直接推进到下一页，最后一页已完整显示时结束播放。
        /// param: 无
        /// returns: 无
        /// </summary>
        public virtual void RequestSkipCurrentDisplayBlockOrFinish()
        {
            if (!isPlaying)
            {
                return;
            }

            if (IsCurrentSnapshotAtFullyRevealedFinalDisplayBlock())
            {
                finishAfterFinalBlockRequested = true;
                return;
            }

            if (IsCurrentSnapshotAtFullyRevealedDisplayBlockBoundary())
            {
                advanceDisplayBlockNormallyRequested = true;
                return;
            }

            skipToNextReplaceRequested = true;
            advanceDisplayBlockNormallyRequested = true;
        }

        /// <summary>
        /// summary: 请求推进到下一条对白；若当前条目尚未完整显示，则先立即补完当前条目。
        /// param: 无
        /// returns: 无
        /// </summary>
        public virtual void RequestAdvanceToNextEntryOrFinish()
        {
            if (!isPlaying)
            {
                return;
            }

            if (!hasCurrentSnapshot || !currentSnapshot.IsEntryFullyRevealed)
            {
                RequestSkipCurrentEntry();
                return;
            }

            if (currentSnapshot.EntryIndex >= currentSnapshot.EntryCount - 1)
            {
                finishAfterFinalBlockRequested = true;
                return;
            }

            advanceToNextEntryRequested = true;
        }

        /// <summary>
        /// summary: 主动停止当前剧情序列，并以 Cancelled 结果结束。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void StopCurrentSequence()
        {
            if (!isPlaying)
            {
                return;
            }

            if (activeSequenceRoutine != null)
            {
                StopCoroutine(activeSequenceRoutine);
                activeSequenceRoutine = null;
            }

            FinalizeSequence(new StorySequenceResult(StorySequenceCompletionStatus.Cancelled, CancelledMessage));
        }

        /// <summary>
        /// summary: 解析剧情 JSON 文本，并输出过滤空白条目后的标准化剧情数据。
        /// param name="jsonText": Addressables TextAsset 里的原始 JSON 文本
        /// param name="data": 输出的剧情序列数据
        /// param name="errorMessage": 解析或校验失败时的错误原因
        /// returns: 成功解析到至少一条有效条目时返回 true，否则返回 false
        /// </summary>
        public static bool TryParseJson(
            string jsonText,
            out StorySequenceData data,
            out string errorMessage,
            string localizationPatchId = null)
        {
            data = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Story JSON is empty.";
                return false;
            }

            StorySequenceData rawData;
            try
            {
                rawData = LocalizedJsonUtility.DeserializeLocalized<StorySequenceData>(
                    jsonText,
                    "StorySequence",
                    settings: null,
                    rootPatchId: localizationPatchId);
            }
            catch (JsonException exception)
            {
                errorMessage = $"Story JSON is invalid: {exception.Message}";
                return false;
            }

            if (rawData?.Entries == null)
            {
                errorMessage = "Story JSON must contain an entries array.";
                return false;
            }

            List<StorySequenceEntry> validEntries = new();
            for (int index = 0; index < rawData.Entries.Count; index++)
            {
                StorySequenceEntry rawEntry = rawData.Entries[index];
                if (rawEntry == null || string.IsNullOrWhiteSpace(rawEntry.Text))
                {
                    continue;
                }

                validEntries.Add(new StorySequenceEntry
                {
                    SpeakerId = NormalizeSpeakerId(rawEntry.SpeakerId),
                    DisplayName = NormalizeDisplayName(rawEntry.DisplayName),
                    Text = rawEntry.Text,
                    DisplayMode = rawEntry.DisplayMode
                });
            }

            if (validEntries.Count == 0)
            {
                errorMessage = "Story JSON does not contain any playable text entries.";
                return false;
            }

            data = new StorySequenceData
            {
                Entries = validEntries
            };
            return true;
        }

        /// <summary>
        /// summary: 提供当前快照给同程序集内的 UI 视图做即时同步，避免晚订阅后漏掉第一帧显示。
        /// param name="snapshot": 输出当前快照
        /// returns: 当前存在可用快照时返回 true，否则返回 false
        /// </summary>
        internal bool TryGetCurrentSnapshot(out StorySequenceSnapshot snapshot)
        {
            snapshot = currentSnapshot;
            return hasCurrentSnapshot;
        }

        /// <summary>
        /// summary: 校验并登记一轮新的剧情序列请求；当前实现固定拒绝并行序列。
        /// param name="request": 本次播放请求
        /// param name="errorMessage": 校验失败时的错误原因
        /// returns: 校验通过并已进入 playing 状态时返回 true
        /// </summary>
        protected bool TryBeginSequence(StorySequenceRequest request, out string errorMessage)
        {
            errorMessage = null;

            if (request == null)
            {
                errorMessage = "Story sequence request is null.";
                return false;
            }

            if (isPlaying)
            {
                errorMessage = "A story sequence is already playing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Address))
            {
                errorMessage = "Story sequence address is empty.";
                return false;
            }

            isPlaying = true;
            skipCurrentEntryRequested = false;
            skipToNextReplaceRequested = false;
            advanceToNextEntryRequested = false;
            advanceDisplayBlockRequested = false;
            advanceDisplayBlockNormallyRequested = false;
            finishAfterFinalBlockRequested = false;
            waitForSkipConfirmationOnFinalBlock = false;
            allowDefaultSkipInput = request.AllowDefaultSkipInput;
            shouldShowSkipButton = false;
            currentDisplayBlockEndIndex = -1;
            hasCurrentSnapshot = false;
            currentSnapshot = default;
            return true;
        }

        /// <summary>
        /// summary: 处理一次完整的“加载 Addressables -> 解析 JSON -> 播放序列”流程。
        /// param name="request": 本次播放请求
        /// returns: 用于协程等待的枚举器
        /// </summary>
        protected IEnumerator PlaySequenceFromRequestCo(StorySequenceRequest request)
        {
            StorySequenceData loadedData = null;
            string loadError = null;

            yield return LoadSequenceDataCo(request, data => loadedData = data, error => loadError = error);

            if (loadedData == null || loadedData.Entries == null || loadedData.Entries.Count == 0)
            {
                activeSequenceRoutine = null;
                FinalizeSequence(new StorySequenceResult(
                    StorySequenceCompletionStatus.Failed,
                    string.IsNullOrWhiteSpace(loadError) ? "Story sequence failed to load." : loadError));
                yield break;
            }

            loadedData = PrepareDataForPlayback(loadedData, request);
            yield return PlaySequenceDataCo(loadedData, request);

            activeSequenceRoutine = null;
            if (isPlaying)
            {
                FinalizeSequence(new StorySequenceResult(StorySequenceCompletionStatus.Completed));
            }
        }

        /// <summary>
        /// summary: 从请求地址加载并解析剧情数据；默认实现使用 Addressables TextAsset。
        /// param name="request": 本次播放请求
        /// param name="onLoaded": 成功时接收标准化剧情数据的回调
        /// param name="onError": 失败时接收错误原因的回调
        /// returns: 用于协程等待的枚举器
        /// </summary>
        protected virtual IEnumerator LoadSequenceDataCo(
            StorySequenceRequest request,
            Action<StorySequenceData> onLoaded,
            Action<string> onError)
        {
            string address = request.Address.Trim();
            activeTextAssetHandle = Addressables.LoadAssetAsync<TextAsset>(address);
            hasActiveTextAssetHandle = true;

            yield return activeTextAssetHandle;

            if (activeTextAssetHandle.Status != AsyncOperationStatus.Succeeded || activeTextAssetHandle.Result == null)
            {
                onError?.Invoke($"Addressables failed to load TextAsset at '{address}'.");
                ReleaseActiveTextHandle();
                yield break;
            }

            if (!TryParseJson(activeTextAssetHandle.Result.text, out StorySequenceData data, out string errorMessage, address))
            {
                onError?.Invoke(errorMessage);
                ReleaseActiveTextHandle();
                yield break;
            }

            onLoaded?.Invoke(data);
            ReleaseActiveTextHandle();
        }

        /// <summary>
        /// summary: 根据标准化剧情数据逐句广播快照，并负责逐字推进与句间停留。
        /// param name="data": 已标准化的剧情数据
        /// param name="request": 当前播放请求
        /// returns: 用于协程等待的枚举器
        /// </summary>
        protected IEnumerator PlaySequenceDataCo(StorySequenceData data, StorySequenceRequest request)
        {
            int entryCount = data.Entries.Count;
            float charactersPerSecond = Mathf.Max(0f, request.CharactersPerSecond);
            float lineHoldSeconds = Mathf.Max(0f, request.LineHoldSeconds);
            string accumulatedDisplayText = string.Empty;
            bool revealUpcomingBlockImmediately = false;

            for (int index = 0; index < entryCount; index++)
            {
                StorySequenceEntry entry = data.Entries[index];
                string entryText = entry.Text ?? string.Empty;
                string prefixText = BuildPrefixText(accumulatedDisplayText, entry);
                string fullText = string.Concat(prefixText, entryText);
                int prefixVisibleCharacterCount = prefixText.Length;
                int totalCharacterCount = Mathf.Max(0, entryText.Length);
                int visibleCharacterCount = 0;
                float revealedCharacters = 0f;
                int blockEndIndex = FindDisplayBlockEndIndex(data.Entries, index);
                bool skipCurrentBlockRequested = revealUpcomingBlockImmediately || TryConsumeSkipToNextReplaceRequest();
                revealUpcomingBlockImmediately = false;
                currentDisplayBlockEndIndex = blockEndIndex;

                skipCurrentEntryRequested = false;
                PublishSnapshot(CreateSnapshot(entry, fullText, prefixVisibleCharacterCount, index, entryCount, false));

                if (!skipCurrentBlockRequested && totalCharacterCount > 0 && charactersPerSecond > 0f)
                {
                    while (visibleCharacterCount < totalCharacterCount)
                    {
                        if (TryConsumeSkipToNextReplaceRequest())
                        {
                            skipCurrentBlockRequested = true;
                            break;
                        }

                        if (skipCurrentEntryRequested)
                        {
                            break;
                        }

                        revealedCharacters += charactersPerSecond * Mathf.Max(0f, GetPlaybackDeltaTime());
                        int nextVisibleCharacterCount = Mathf.Clamp(Mathf.FloorToInt(revealedCharacters), 0, totalCharacterCount);
                        if (nextVisibleCharacterCount != visibleCharacterCount)
                        {
                            visibleCharacterCount = nextVisibleCharacterCount;
                            PublishSnapshot(CreateSnapshot(
                                entry,
                                fullText,
                                prefixVisibleCharacterCount + visibleCharacterCount,
                                index,
                                entryCount,
                                false));
                        }

                        yield return null;
                    }
                }

                skipCurrentEntryRequested = false;
                PublishSnapshot(CreateSnapshot(entry, fullText, fullText.Length, index, entryCount, true));
                accumulatedDisplayText = fullText;

                if (request.WaitForAdvanceInputAfterEntryReveal)
                {
                    while (isPlaying)
                    {
                        if (TryConsumeSkipToNextReplaceRequest())
                        {
                            skipCurrentBlockRequested = true;
                            break;
                        }

                        if (finishAfterFinalBlockRequested && index >= entryCount - 1)
                        {
                            activeSequenceRoutine = null;
                            FinalizeSequence(new StorySequenceResult(StorySequenceCompletionStatus.Completed));
                            yield break;
                        }

                        if (advanceToNextEntryRequested)
                        {
                            break;
                        }

                        yield return null;
                    }

                    if (!isPlaying)
                    {
                        yield break;
                    }

                    if (TryConsumeAdvanceToNextEntryRequest())
                    {
                        continue;
                    }
                }

                float elapsed = 0f;
                bool startNextBlockNormally = false;
                while (!skipCurrentBlockRequested && elapsed < lineHoldSeconds)
                {
                    if (TryConsumeSkipToNextReplaceRequest())
                    {
                        skipCurrentBlockRequested = true;
                        break;
                    }

                    if (TryConsumeAdvanceDisplayBlockNormallyRequest())
                    {
                        startNextBlockNormally = true;
                        index = blockEndIndex;
                        break;
                    }

                    if (TryConsumeAdvanceDisplayBlockRequest())
                    {
                        revealUpcomingBlockImmediately = true;
                        index = blockEndIndex;
                        break;
                    }

                    elapsed += Mathf.Max(0f, GetPlaybackDeltaTime());
                    yield return null;
                }

                if (startNextBlockNormally || revealUpcomingBlockImmediately)
                {
                    continue;
                }

                if (!skipCurrentBlockRequested && TryConsumeAdvanceDisplayBlockNormallyRequest())
                {
                    index = blockEndIndex;
                    continue;
                }

                if (!skipCurrentBlockRequested && TryConsumeAdvanceDisplayBlockRequest())
                {
                    revealUpcomingBlockImmediately = true;
                    index = blockEndIndex;
                    continue;
                }

                if (!skipCurrentBlockRequested && TryConsumeSkipToNextReplaceRequest())
                {
                    skipCurrentBlockRequested = true;
                }

                if (skipCurrentBlockRequested)
                {
                    if (blockEndIndex > index)
                    {
                        for (int appendIndex = index + 1; appendIndex <= blockEndIndex; appendIndex++)
                        {
                            StorySequenceEntry appendEntry = data.Entries[appendIndex];
                            string appendEntryText = appendEntry.Text ?? string.Empty;
                            string appendPrefixText = BuildPrefixText(accumulatedDisplayText, appendEntry);
                            string appendFullText = string.Concat(appendPrefixText, appendEntryText);
                            currentDisplayBlockEndIndex = blockEndIndex;
                            PublishSnapshot(CreateSnapshot(
                                appendEntry,
                                appendFullText,
                                appendFullText.Length,
                                appendIndex,
                                entryCount,
                                true));
                            accumulatedDisplayText = appendFullText;
                        }
                    }

                    skipCurrentEntryRequested = false;
                    if (blockEndIndex >= entryCount - 1)
                    {
                        waitForSkipConfirmationOnFinalBlock = true;
                        break;
                    }

                    bool revealNextBlockImmediately = false;
                    while (isPlaying)
                    {
                        if (TryConsumeAdvanceDisplayBlockNormallyRequest())
                        {
                            break;
                        }

                        if (TryConsumeAdvanceDisplayBlockRequest())
                        {
                            revealNextBlockImmediately = true;
                            break;
                        }

                        yield return null;
                    }

                    if (!isPlaying)
                    {
                        yield break;
                    }

                    revealUpcomingBlockImmediately = revealNextBlockImmediately;
                    index = blockEndIndex;
                }
            }

            if (!waitForSkipConfirmationOnFinalBlock)
            {
                yield break;
            }

            while (isPlaying && !finishAfterFinalBlockRequested)
            {
                yield return null;
            }

            if (isPlaying)
            {
                activeSequenceRoutine = null;
                FinalizeSequence(new StorySequenceResult(StorySequenceCompletionStatus.Completed));
            }
        }

        /// <summary>
        /// summary: 默认跳过输入的读取入口，当前支持空格键和鼠标左键。
        /// param: 无
        /// returns: 当前帧需要补完当前句时返回 true
        /// </summary>
        protected virtual bool ShouldConsumeDefaultSkipInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool isSpacePressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            bool isLeftMousePressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
            return isSpacePressed || isLeftMousePressed;
        }

        /// <summary>
        /// summary: 默认使用非缩放时间作为逐字播放的时间步长，便于测试替换为固定值。
        /// param: 无
        /// returns: 本帧用于剧情播放的 deltaTime
        /// </summary>
        protected virtual float GetPlaybackDeltaTime()
        {
            return Time.unscaledDeltaTime;
        }

        /// <summary>
        /// summary: 统一广播剧情快照，并缓存最后一次快照给晚订阅的视图做即时同步。
        /// param name="snapshot": 需要广播的剧情快照
        /// returns: 无
        /// </summary>
        protected void PublishSnapshot(StorySequenceSnapshot snapshot)
        {
            currentSnapshot = snapshot;
            hasCurrentSnapshot = true;
            SnapshotChanged?.Invoke(snapshot);
        }

        /// <summary>
        /// summary: 统一结束当前序列，清理输入/句柄/快照状态，并广播完成结果。
        /// param name="result": 当前序列的结束结果
        /// returns: 无
        /// </summary>
        private void FinalizeSequence(StorySequenceResult result)
        {
            ReleaseActiveTextHandle();
            isPlaying = false;
            allowDefaultSkipInput = false;
            skipCurrentEntryRequested = false;
            skipToNextReplaceRequested = false;
            advanceToNextEntryRequested = false;
            advanceDisplayBlockRequested = false;
            advanceDisplayBlockNormallyRequested = false;
            finishAfterFinalBlockRequested = false;
            waitForSkipConfirmationOnFinalBlock = false;
            shouldShowSkipButton = false;
            currentDisplayBlockEndIndex = -1;
            hasCurrentSnapshot = false;
            currentSnapshot = default;

            SequenceCompleted?.Invoke(result);
        }

        /// <summary>
        /// summary: 释放当前活动的 Addressables TextAsset 句柄，避免加载失败或取消后泄漏。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ReleaseActiveTextHandle()
        {
            if (!hasActiveTextAssetHandle)
            {
                return;
            }

            if (activeTextAssetHandle.IsValid())
            {
                Addressables.Release(activeTextAssetHandle);
            }

            activeTextAssetHandle = default;
            hasActiveTextAssetHandle = false;
        }

        /// <summary>
        /// summary: 为当前条目构造标准化快照，统一处理 speaker 默认值与计数信息。
        /// param name="entry": 当前剧情条目
        /// param name="fullText": 当前条目的完整文本
        /// param name="visibleCharacterCount": 当前可见字符数
        /// param name="entryIndex": 当前条目索引
        /// param name="entryCount": 当前条目总数
        /// param name="isEntryFullyRevealed": 当前条目是否已完整显示
        /// returns: 构造好的剧情快照
        /// </summary>
        private StorySequenceSnapshot CreateSnapshot(
            StorySequenceEntry entry,
            string fullText,
            int visibleCharacterCount,
            int entryIndex,
            int entryCount,
            bool isEntryFullyRevealed)
        {
            return new StorySequenceSnapshot(
                entry?.SpeakerId ?? DefaultSpeakerId,
                entry?.DisplayName ?? string.Empty,
                fullText,
                visibleCharacterCount,
                entryIndex,
                entryCount,
                isEntryFullyRevealed,
                shouldShowSkipButton);
        }

        /// <summary>
        /// summary: 首次触发空格或鼠标左键时标记跳过按钮可见，并立即把最新状态同步给当前界面。
        /// param: 无
        /// returns: 无
        /// </summary>
        protected void NotifyAdvanceShortcutUsed()
        {
            if (shouldShowSkipButton)
            {
                return;
            }

            shouldShowSkipButton = true;
            if (hasCurrentSnapshot)
            {
                PublishSnapshot(CreateSnapshot(
                    currentSnapshot.SpeakerId,
                    currentSnapshot.DisplayName,
                    currentSnapshot.FullText,
                    currentSnapshot.VisibleCharacterCount,
                    currentSnapshot.EntryIndex,
                    currentSnapshot.EntryCount,
                    currentSnapshot.IsEntryFullyRevealed,
                    true));
            }
        }

        /// <summary>
        /// summary: 读取并清空“跳到下一条 replace”请求标记，避免同一请求在多个阶段重复消费。
        /// param: 无
        /// returns: 当前存在未消费的跳过块请求时返回 true
        /// </summary>
        private bool TryConsumeSkipToNextReplaceRequest()
        {
            if (!skipToNextReplaceRequested)
            {
                return false;
            }

            skipToNextReplaceRequested = false;
            return true;
        }

        /// <summary>
        /// summary: 读取并清空“从当前分段边界推进到下一分段”的请求标记。
        /// param: 无
        /// returns: 当前存在未消费的分段推进请求时返回 true
        /// </summary>
        private bool TryConsumeAdvanceDisplayBlockRequest()
        {
            if (!advanceDisplayBlockRequested)
            {
                return false;
            }

            advanceDisplayBlockRequested = false;
            return true;
        }

        /// <summary>
        /// summary: 读取并清空“进入下一显示块但不立刻补完”的请求标记。
        /// param: 无
        /// returns: 当前存在未消费的普通翻页请求时返回 true
        /// </summary>
        private bool TryConsumeAdvanceDisplayBlockNormallyRequest()
        {
            if (!advanceDisplayBlockNormallyRequested)
            {
                return false;
            }

            advanceDisplayBlockNormallyRequested = false;
            return true;
        }

        /// <summary>
        /// summary: 读取并清空“推进到下一条对白”请求标记，供对话 UI 的手动推进复用。
        /// param: 无
        /// returns: 当前存在未消费的对白推进请求时返回 true
        /// </summary>
        private bool TryConsumeAdvanceToNextEntryRequest()
        {
            if (!advanceToNextEntryRequested)
            {
                return false;
            }

            advanceToNextEntryRequested = false;
            return true;
        }

        /// <summary>
        /// summary: 判断当前快照是否已经完整显示到当前分段边界。
        /// param: 无
        /// returns: 当前快照已经完整显示当前分段末尾时返回 true
        /// </summary>
        private bool IsCurrentSnapshotAtFullyRevealedDisplayBlockBoundary()
        {
            if (!hasCurrentSnapshot || !currentSnapshot.IsEntryFullyRevealed)
            {
                return false;
            }

            if (currentDisplayBlockEndIndex < 0)
            {
                return false;
            }

            return currentSnapshot.EntryIndex >= currentDisplayBlockEndIndex;
        }

        /// <summary>
        /// summary: 判断当前快照是否已经完整显示到最终显示块的末尾；若是，则跳过按钮可直接结束序列。
        /// param: 无
        /// returns: 当前已经显示完最后一个显示块时返回 true
        /// </summary>
        private bool IsCurrentSnapshotAtFullyRevealedFinalDisplayBlock()
        {
            if (!IsCurrentSnapshotAtFullyRevealedDisplayBlockBoundary())
            {
                return false;
            }

            if (currentDisplayBlockEndIndex < currentSnapshot.EntryCount - 1)
            {
                return false;
            }

            return currentSnapshot.EntryIndex >= currentDisplayBlockEndIndex;
        }

        /// <summary>
        /// summary: 查找当前显示块的结束条目索引；块由一个 replace 起始，后续 append 条目组成。
        /// param name="entries": 当前剧情条目列表
        /// param name="currentIndex": 当前正在处理的条目索引
        /// returns: 当前显示块最后一条条目的索引
        /// </summary>
        private static int FindDisplayBlockEndIndex(IReadOnlyList<StorySequenceEntry> entries, int currentIndex)
        {
            int endIndex = currentIndex;
            for (int index = currentIndex + 1; index < entries.Count; index++)
            {
                StorySequenceEntry nextEntry = entries[index];
                if (nextEntry == null || nextEntry.DisplayMode == StorySequenceDisplayMode.Replace)
                {
                    break;
                }

                endIndex = index;
            }

            return endIndex;
        }

        /// <summary>
        /// summary: 基于当前状态重新构造剧情快照，供跳过按钮显隐这类 UI 状态刷新复用。
        /// param name="speakerId": 当前 speakerId
        /// param name="displayName": 当前显示名
        /// param name="fullText": 当前完整文本
        /// param name="visibleCharacterCount": 当前可见字符数
        /// param name="entryIndex": 当前条目索引
        /// param name="entryCount": 当前条目总数
        /// param name="isEntryFullyRevealed": 当前条目是否完整显示
        /// param name="isSkipButtonVisible": 当前是否显示跳过按钮
        /// returns: 构造好的剧情快照
        /// </summary>
        private static StorySequenceSnapshot CreateSnapshot(
            string speakerId,
            string displayName,
            string fullText,
            int visibleCharacterCount,
            int entryIndex,
            int entryCount,
            bool isEntryFullyRevealed,
            bool isSkipButtonVisible)
        {
            return new StorySequenceSnapshot(
                speakerId,
                displayName,
                fullText,
                visibleCharacterCount,
                entryIndex,
                entryCount,
                isEntryFullyRevealed,
                isSkipButtonVisible);
        }

        /// <summary>
        /// summary: 根据上一段已完成文本和当前条目的显示模式，构造当前句逐字播放前的前缀文本。
        /// param name="accumulatedDisplayText": 上一条完成后仍需保留在屏幕上的文本
        /// param name="entry": 当前剧情条目
        /// returns: 当前条目播放前应先展示的前缀文本
        /// </summary>
        private static string BuildPrefixText(string accumulatedDisplayText, StorySequenceEntry entry)
        {
            return BuildPrefixText(accumulatedDisplayText, entry?.DisplayMode ?? StorySequenceDisplayMode.Replace);
        }

        private static string BuildPrefixText(string accumulatedDisplayText, StorySequenceDisplayMode displayMode)
        {
            if (string.IsNullOrEmpty(accumulatedDisplayText))
            {
                return string.Empty;
            }

            return displayMode == StorySequenceDisplayMode.Append
                ? $"{accumulatedDisplayText}\n"
                : string.Empty;
        }

        /// <summary>
        /// summary: 规范化 speakerId；缺失时统一回退到 narrator。
        /// param name="speakerId": 原始 speakerId
        /// returns: 标准化后的 speakerId
        /// </summary>
        private static string NormalizeSpeakerId(string speakerId)
        {
            return string.IsNullOrWhiteSpace(speakerId) ? DefaultSpeakerId : speakerId.Trim();
        }

        /// <summary>
        /// summary: 规范化 displayName；缺失时统一视为“无显示名”。
        /// param name="displayName": 原始 displayName
        /// returns: 标准化后的 displayName
        /// </summary>
        private static string NormalizeDisplayName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        }

        /// <summary>
        /// summary: 按请求配置预处理可播放条目；会把超过当前页面容量的对白或 append 文本块拆成多个分页条目。
        /// param name="data": 原始剧情数据
        /// param name="request": 当前播放请求
        /// returns: 可直接交给播放器消费的条目数据
        /// </summary>
        private static StorySequenceData PrepareDataForPlayback(StorySequenceData data, StorySequenceRequest request)
        {
            if (data?.Entries == null || data.Entries.Count == 0 || request == null)
            {
                return data;
            }

            int maxCharactersPerPage = Mathf.Max(0, request.MaxCharactersPerEntry);
            Func<string, bool> displayTextFitsPage = request.DisplayTextFitsPage;
            if (maxCharactersPerPage <= 0 && displayTextFitsPage == null)
            {
                return data;
            }

            List<StorySequenceEntry> preparedEntries = new();
            string accumulatedDisplayText = string.Empty;
            for (int index = 0; index < data.Entries.Count; index++)
            {
                StorySequenceEntry entry = data.Entries[index];
                if (entry == null || string.IsNullOrEmpty(entry.Text))
                {
                    continue;
                }

                AppendPreparedEntries(preparedEntries, entry, maxCharactersPerPage, displayTextFitsPage, ref accumulatedDisplayText);
            }

            return new StorySequenceData
            {
                Entries = preparedEntries
            };
        }

        /// <summary>
        /// summary: 把单条对白按页面容量拆成多个可播放条目；溢出的后续分页一律改为 replace，确保单屏文本不会累积超限。
        /// param name="target": 输出条目列表
        /// param name="entry": 当前原始对白
        /// param name="maxCharactersPerPage": 单页允许的最大字符数
        /// param name="displayTextFitsPage": 当前 UI 页面测量函数
        /// param name="accumulatedDisplayText": 当前屏幕上已经累积的 append 文本
        /// returns: 无
        /// </summary>
        private static void AppendPreparedEntries(
            List<StorySequenceEntry> target,
            StorySequenceEntry entry,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage,
            ref string accumulatedDisplayText)
        {
            StorySequenceDisplayMode firstDisplayMode = ResolveFirstPreparedDisplayMode(
                accumulatedDisplayText,
                entry,
                maxCharactersPerPage,
                displayTextFitsPage);
            string prefixText = BuildPrefixText(accumulatedDisplayText, firstDisplayMode);
            List<string> chunks = SplitTextIntoChunks(entry.Text, prefixText, maxCharactersPerPage, displayTextFitsPage);

            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                StorySequenceDisplayMode displayMode = chunkIndex == 0 ? firstDisplayMode : StorySequenceDisplayMode.Replace;
                target.Add(new StorySequenceEntry
                {
                    SpeakerId = entry.SpeakerId,
                    DisplayName = entry.DisplayName,
                    Text = chunks[chunkIndex],
                    DisplayMode = displayMode
                });

                accumulatedDisplayText = string.Concat(BuildPrefixText(accumulatedDisplayText, displayMode), chunks[chunkIndex]);
            }
        }

        /// <summary>
        /// summary: 解析当前原始条目第一页应使用 append 还是 replace；若 append 后整屏会溢出，则从新页开始。
        /// param name="accumulatedDisplayText": 当前屏幕上已经累积的 append 文本
        /// param name="entry": 当前原始条目
        /// param name="maxCharactersPerPage": 单页允许的最大字符数
        /// param name="displayTextFitsPage": 当前 UI 页面测量函数
        /// returns: 第一页应使用的显示模式
        /// </summary>
        private static StorySequenceDisplayMode ResolveFirstPreparedDisplayMode(
            string accumulatedDisplayText,
            StorySequenceEntry entry,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage)
        {
            if (entry == null || entry.DisplayMode != StorySequenceDisplayMode.Append || string.IsNullOrEmpty(accumulatedDisplayText))
            {
                return entry?.DisplayMode ?? StorySequenceDisplayMode.Replace;
            }

            string appendedDisplayText = string.Concat(BuildPrefixText(accumulatedDisplayText, entry.DisplayMode), entry.Text ?? string.Empty);
            return DoesDisplayTextFitPage(appendedDisplayText, maxCharactersPerPage, displayTextFitsPage)
                ? StorySequenceDisplayMode.Append
                : StorySequenceDisplayMode.Replace;
        }

        /// <summary>
        /// summary: 按文本元素把一条对白切成若干分页，避免把代理对或组合字符从中间截断。
        /// param name="text": 原始对白文本
        /// param name="prefixText": 当前页在正文前保留的 append 前缀
        /// param name="maxCharactersPerPage": 单页允许的最大文本元素数量
        /// param name="displayTextFitsPage": 当前 UI 页面测量函数
        /// returns: 拆分后的分页文本列表
        /// </summary>
        private static List<string> SplitTextIntoChunks(
            string text,
            string prefixText,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage)
        {
            List<string> chunks = new();
            if (string.IsNullOrEmpty(text))
            {
                return chunks;
            }

            if (maxCharactersPerPage <= 0 && displayTextFitsPage == null)
            {
                chunks.Add(text);
                return chunks;
            }

            List<string> elements = GetTextElements(text);
            StringBuilder builder = new();
            int elementIndex = 0;
            string currentPrefix = prefixText ?? string.Empty;
            int currentPrefixCount = CountTextElements(currentPrefix);
            bool useMeasuredPageFit = displayTextFitsPage != null;
            while (elementIndex < elements.Count)
            {
                int remainingCount = elements.Count - elementIndex;
                int maxChunkCount = useMeasuredPageFit
                    ? remainingCount
                    : maxCharactersPerPage > 0
                    ? Mathf.Max(1, Mathf.Min(remainingCount, maxCharactersPerPage - currentPrefixCount))
                    : remainingCount;
                int chunkCount = ResolveLargestFittingChunkCount(elements, elementIndex, maxChunkCount, currentPrefix, maxCharactersPerPage, displayTextFitsPage);
                AppendTextElements(builder, elements, elementIndex, chunkCount);
                chunks.Add(builder.ToString());
                builder.Clear();
                elementIndex += chunkCount;
                currentPrefix = string.Empty;
                currentPrefixCount = 0;
            }

            return chunks;
        }

        private static int ResolveLargestFittingChunkCount(
            IReadOnlyList<string> elements,
            int startIndex,
            int maxChunkCount,
            string prefixText,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage)
        {
            bool useMeasuredPageFit = displayTextFitsPage != null;
            if (useMeasuredPageFit)
            {
                return ResolveLargestMeasuredChunkCount(
                    elements,
                    startIndex,
                    Mathf.Max(1, maxChunkCount),
                    prefixText,
                    maxCharactersPerPage,
                    displayTextFitsPage);
            }

            int low = 1;
            int high = Mathf.Max(1, maxChunkCount);
            int best = 0;

            while (low <= high)
            {
                int middle = (low + high) / 2;
                string candidateChunk = BuildTextElementsString(elements, startIndex, middle);
                string candidateDisplayText = string.Concat(prefixText ?? string.Empty, candidateChunk);
                if (DoesDisplayTextFitPage(candidateDisplayText, maxCharactersPerPage, displayTextFitsPage))
                {
                    best = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return best > 0 ? best : 1;
        }

        private static int ResolveLargestMeasuredChunkCount(
            IReadOnlyList<string> elements,
            int startIndex,
            int maxChunkCount,
            string prefixText,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage)
        {
            int upperBound = Mathf.Max(1, maxChunkCount);
            int probeSeedCount = ResolveLargestMeasuredProbeSeed(prefixText, maxCharactersPerPage, upperBound);

            if (DoesDisplayTextFitPage(
                string.Concat(prefixText ?? string.Empty, BuildTextElementsString(elements, startIndex, probeSeedCount)),
                maxCharactersPerPage,
                displayTextFitsPage))
            {
                return ResolveLargestFittingChunkCountInRange(
                    elements,
                    startIndex,
                    probeSeedCount,
                    upperBound,
                    prefixText,
                    maxCharactersPerPage,
                    displayTextFitsPage);
            }

            return ResolveLargestFittingChunkCountInRange(
                elements,
                startIndex,
                1,
                Mathf.Max(1, probeSeedCount - 1),
                prefixText,
                maxCharactersPerPage,
                displayTextFitsPage);
        }

        private static int ResolveLargestMeasuredProbeSeed(string prefixText, int maxCharactersPerPage, int upperBound)
        {
            if (maxCharactersPerPage <= 0)
            {
                return upperBound;
            }

            int prefixCount = CountTextElements(prefixText);
            int preferredCount = Mathf.Max(1, maxCharactersPerPage - prefixCount);
            return Mathf.Clamp(preferredCount, 1, upperBound);
        }

        private static int ResolveLargestFittingChunkCountInRange(
            IReadOnlyList<string> elements,
            int startIndex,
            int low,
            int high,
            string prefixText,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage)
        {
            int best = 0;
            int searchLow = Mathf.Max(1, low);
            int searchHigh = Mathf.Max(searchLow, high);

            while (searchLow <= searchHigh)
            {
                int middle = (searchLow + searchHigh) / 2;
                string candidateChunk = BuildTextElementsString(elements, startIndex, middle);
                string candidateDisplayText = string.Concat(prefixText ?? string.Empty, candidateChunk);
                if (DoesDisplayTextFitPage(candidateDisplayText, maxCharactersPerPage, displayTextFitsPage))
                {
                    best = middle;
                    searchLow = middle + 1;
                }
                else
                {
                    searchHigh = middle - 1;
                }
            }

            return best > 0 ? best : 1;
        }

        private static bool DoesDisplayTextFitPage(
            string displayText,
            int maxCharactersPerPage,
            Func<string, bool> displayTextFitsPage)
        {
            if (displayTextFitsPage != null)
            {
                return displayTextFitsPage(displayText ?? string.Empty);
            }

            if (maxCharactersPerPage > 0 && CountTextElements(displayText) > maxCharactersPerPage)
            {
                return false;
            }

            return true;
        }

        private static List<string> GetTextElements(string text)
        {
            List<string> elements = new();
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text ?? string.Empty);
            while (enumerator.MoveNext())
            {
                elements.Add(enumerator.GetTextElement());
            }

            return elements;
        }

        private static int CountTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static string BuildTextElementsString(IReadOnlyList<string> elements, int startIndex, int count)
        {
            StringBuilder builder = new();
            AppendTextElements(builder, elements, startIndex, count);
            return builder.ToString();
        }

        private static void AppendTextElements(StringBuilder builder, IReadOnlyList<string> elements, int startIndex, int count)
        {
            int endIndex = Mathf.Min(elements.Count, startIndex + count);
            for (int index = startIndex; index < endIndex; index++)
            {
                builder.Append(elements[index]);
            }
        }
    }
}
