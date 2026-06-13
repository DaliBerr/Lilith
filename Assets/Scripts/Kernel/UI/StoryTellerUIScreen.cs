using Kernel.GameState;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// Startup 菜单后的世界观介绍面板，只负责显示 StorySequenceParser 推送的剧情快照。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Storyteller Panel")]
    public sealed class StoryTellerUIScreen : GameUIScreen, IPointerClickHandler
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private GameObject skipButtonRoot;
        [SerializeField] private Button skipButton;

        public override Status currentStatus { get; } = StatusList.InMainMenuStatus;

        internal bool DoesStoryTextFitPage(string text)
        {
            TryAutoBindReferences();
            return StoryTextPageUtility.DoesTextFitPage(storyText, text);
        }

        internal int EstimateStoryTextCapacity(int fallbackCapacity)
        {
            TryAutoBindReferences();
            return StoryTextPageUtility.EstimateTextElementCapacity(storyText, fallbackCapacity);
        }

        protected override void OnInit()
        {
            TryAutoBindReferences();
            ResetStoryDisplay();
        }

        protected override void OnAfterShow()
        {
            SubscribeToStorySequence();
        }

        protected override void OnAfterHide()
        {
            UnsubscribeFromStorySequence();
            RemoveCurrentStatus();
            ResetStoryDisplay();
        }

        private void OnDestroy()
        {
            UnsubscribeFromStorySequence();
            RemoveCurrentStatus();
            ResetStoryDisplay();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind StoryTeller Panel")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 自动绑定剧情文本组件，优先使用 prefab 约定的 Text 子节点。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            storyText ??= FindInContentSafeFrame("Text")?.GetComponent<TMP_Text>();
            storyText ??= GetComponentInChildren<TMP_Text>(true);
            skipButtonRoot ??= FindInContentSafeFrame("Skip Button")?.gameObject;
            skipButton ??= FindInContentSafeFrame("Skip Button/Button")?.GetComponent<Button>();
        }

        /// <summary>
        /// summary: 订阅剧情播放器的快照事件，并在已有快照时立即同步当前显示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SubscribeToStorySequence()
        {
            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                BindSkipButton();
                return;
            }

            parser.SnapshotChanged -= HandleStorySnapshotChanged;
            parser.SnapshotChanged += HandleStorySnapshotChanged;
            BindSkipButton();

            if (parser.TryGetCurrentSnapshot(out StorySequenceSnapshot snapshot))
            {
                ApplyStorySnapshot(snapshot);
            }
        }

        /// <summary>
        /// summary: 取消订阅剧情播放器快照，避免界面隐藏后继续收到显示更新。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnsubscribeFromStorySequence()
        {
            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                UnbindSkipButton();
                return;
            }

            parser.SnapshotChanged -= HandleStorySnapshotChanged;
            UnbindSkipButton();
        }

        /// <summary>
        /// summary: 接收剧情播放器的快照广播，并刷新当前文本显示。
        /// param name="snapshot": 当前剧情显示快照
        /// returns: 无
        /// </summary>
        private void HandleStorySnapshotChanged(StorySequenceSnapshot snapshot)
        {
            ApplyStorySnapshot(snapshot);
        }

        /// <summary>
        /// summary: 将剧情快照应用到当前 TMP 文本组件，只负责正文与可见字符数的显示。
        /// param name="snapshot": 当前剧情显示快照
        /// returns: 无
        /// </summary>
        private void ApplyStorySnapshot(StorySequenceSnapshot snapshot)
        {
            if (storyText == null)
            {
                SetSkipButtonVisible(snapshot.ShouldShowSkipButton);
                return;
            }

            storyText.text = snapshot.FullText ?? string.Empty;
            storyText.maxVisibleCharacters = snapshot.IsEntryFullyRevealed
                ? int.MaxValue
                : Mathf.Max(0, snapshot.VisibleCharacterCount);
            SetSkipButtonVisible(snapshot.ShouldShowSkipButton);
        }

        /// <summary>
        /// summary: 清空当前文本显示状态，避免界面下次显示时残留上一轮内容。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ResetStoryDisplay()
        {
            if (storyText == null)
            {
                SetSkipButtonVisible(false);
                return;
            }

            storyText.text = string.Empty;
            storyText.maxVisibleCharacters = int.MaxValue;
            SetSkipButtonVisible(false);
        }

        /// <summary>
        /// summary: 绑定跳过按钮点击事件，点击后直接请求剧情服务快进到下一条 replace 或结束播放。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindSkipButton()
        {
            if (skipButton == null)
            {
                return;
            }

            skipButton.onClick.RemoveListener(HandleSkipButtonClicked);
            skipButton.onClick.AddListener(HandleSkipButtonClicked);
        }

        /// <summary>
        /// summary: 解除跳过按钮点击事件，避免界面关闭后残留监听。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindSkipButton()
        {
            if (skipButton == null)
            {
                return;
            }

            skipButton.onClick.RemoveListener(HandleSkipButtonClicked);
        }

        /// <summary>
        /// summary: 响应剧情界面的跳过按钮，直接让剧情播放器快进到下一条 replace 或结束。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleSkipButtonClicked()
        {
            StorySequenceParser.Instance?.RequestSkipCurrentDisplayBlockOrFinish();
        }

        /// <summary>
        /// summary: 响应面板本体点击，按 Story 对话的“继续/结束”规则推进到下一步。
        /// param name="eventData": Unity UI 指针点击事件
        /// returns: 无
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            StorySequenceParser.Instance?.RequestAdvanceToNextEntryOrFinish();
        }

        /// <summary>
        /// summary: 控制跳过按钮根节点显隐；当前默认隐藏，首次手动推进后再显示。
        /// param name="isVisible": 是否显示跳过按钮
        /// returns: 无
        /// </summary>
        private void SetSkipButtonVisible(bool isVisible)
        {
            if (skipButtonRoot == null)
            {
                return;
            }

            if (skipButtonRoot.activeSelf != isVisible)
            {
                skipButtonRoot.SetActive(isVisible);
            }
        }

        /// <summary>
        /// summary: 在剧情面板关闭或销毁时移除 InMainMenu 状态，避免状态残留影响后续场景。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }
    }
}
