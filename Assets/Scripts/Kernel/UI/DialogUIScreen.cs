using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// Dialog UI prefab 的运行时控制脚本，负责显示当前说话人与分页对白，并接收点击/键盘推进输入。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Dialog UI")]
    public sealed class DialogUIScreen : GameUIScreen, IPointerClickHandler
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text dialogText;
        [SerializeField] private GameObject speakerInfoRoot;
        [SerializeField] private TMP_Text speakerNameText;

        public override Status currentStatus { get; } = StatusList.InMainMenuStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            ResetDialogDisplay();
        }

        protected override void OnAfterShow()
        {
            SubscribeToStorySequence();
        }

        protected override void OnAfterHide()
        {
            UnsubscribeFromStorySequence();
            RemoveCurrentStatus();
            ResetDialogDisplay();
        }

        private void OnDestroy()
        {
            UnsubscribeFromStorySequence();
            RemoveCurrentStatus();
            ResetDialogDisplay();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        private void Update()
        {
            if (!isVisible || !IsAdvanceShortcutPressed())
            {
                return;
            }

            RequestAdvance();
        }

        [ContextMenu("Auto Bind Dialog UI")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 按 Dialog UI prefab 的固定层级自动补齐对白正文与说话人信息引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            dialogText ??= transform.Find("Main Content/Text (TMP)")?.GetComponent<TMP_Text>();
            speakerInfoRoot ??= transform.Find("Info Panel")?.gameObject;
            speakerNameText ??= transform.Find("Info Panel/Text (TMP)")?.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// summary: 订阅剧情播放器快照，并在已经存在快照时立即同步当前对话页。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SubscribeToStorySequence()
        {
            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                return;
            }

            parser.SnapshotChanged -= HandleStorySnapshotChanged;
            parser.SnapshotChanged += HandleStorySnapshotChanged;

            if (parser.TryGetCurrentSnapshot(out StorySequenceSnapshot snapshot))
            {
                ApplyStorySnapshot(snapshot);
            }
        }

        /// <summary>
        /// summary: 取消订阅剧情播放器快照，避免界面隐藏后继续收到对白更新。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnsubscribeFromStorySequence()
        {
            StorySequenceParser parser = StorySequenceParser.Instance;
            if (parser == null)
            {
                return;
            }

            parser.SnapshotChanged -= HandleStorySnapshotChanged;
        }

        /// <summary>
        /// summary: 处理剧情快照更新，把当前页文本与说话人文案同步到 Dialog UI。
        /// param name="snapshot": 当前对白快照
        /// returns: 无
        /// </summary>
        private void HandleStorySnapshotChanged(StorySequenceSnapshot snapshot)
        {
            ApplyStorySnapshot(snapshot);
        }

        /// <summary>
        /// summary: 将当前对白快照应用到 Dialog UI；说话人缺失时自动隐藏信息面板。
        /// param name="snapshot": 当前对白快照
        /// returns: 无
        /// </summary>
        private void ApplyStorySnapshot(StorySequenceSnapshot snapshot)
        {
            if (dialogText != null)
            {
                dialogText.text = snapshot.FullText ?? string.Empty;
                dialogText.maxVisibleCharacters = snapshot.IsEntryFullyRevealed
                    ? int.MaxValue
                    : Mathf.Max(0, snapshot.VisibleCharacterCount);
            }

            string speakerLabel = ResolveSpeakerLabel(snapshot);
            if (speakerNameText != null)
            {
                speakerNameText.text = speakerLabel;
            }

            SetSpeakerInfoVisible(!string.IsNullOrWhiteSpace(speakerLabel));
        }

        /// <summary>
        /// summary: 清理当前 Dialog UI 的说话人与正文显示，避免下次打开残留上一轮对白。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ResetDialogDisplay()
        {
            if (dialogText != null)
            {
                dialogText.text = string.Empty;
                dialogText.maxVisibleCharacters = int.MaxValue;
            }

            if (speakerNameText != null)
            {
                speakerNameText.text = string.Empty;
            }

            SetSpeakerInfoVisible(false);
        }

        /// <summary>
        /// summary: 处理对话界面的点击推进；当前统一按“补完当前句，否则进入下一句/结束”的对白规则执行。
        /// param name="eventData": Unity UI 指针点击事件
        /// returns: 无
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            RequestAdvance();
        }

        /// <summary>
        /// summary: 统一请求对白推进，交给 StorySequenceParser 处理逐字补完或翻到下一条。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static void RequestAdvance()
        {
            StorySequenceParser.Instance?.RequestAdvanceToNextEntryOrFinish();
        }

        /// <summary>
        /// summary: 优先使用 displayName，缺失时回退到 speakerId，保证多角色对话至少能显示一个识别标签。
        /// param name="snapshot": 当前对白快照
        /// returns: 当前应显示在说话人面板里的文案
        /// </summary>
        private static string ResolveSpeakerLabel(StorySequenceSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.DisplayName))
            {
                return snapshot.DisplayName;
            }

            return string.IsNullOrWhiteSpace(snapshot.SpeakerId) ? string.Empty : snapshot.SpeakerId;
        }

        /// <summary>
        /// summary: 切换说话人信息面板显隐；无说话人时隐藏整个面板避免出现空白条。
        /// param name="isVisible": 目标显隐状态
        /// returns: 无
        /// </summary>
        private void SetSpeakerInfoVisible(bool isVisible)
        {
            if (speakerInfoRoot == null)
            {
                return;
            }

            if (speakerInfoRoot.activeSelf != isVisible)
            {
                speakerInfoRoot.SetActive(isVisible);
            }
        }

        /// <summary>
        /// summary: 判断当前帧是否触发对白推进快捷键；当前支持空格与回车。
        /// param: 无
        /// returns: 当前帧触发推进快捷键时返回 true
        /// </summary>
        private static bool IsAdvanceShortcutPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.spaceKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        /// <summary>
        /// summary: 在对话界面关闭或销毁时移除 InMainMenu 状态，避免状态残留到后续场景。
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
