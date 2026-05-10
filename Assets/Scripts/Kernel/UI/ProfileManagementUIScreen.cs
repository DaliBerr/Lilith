using System;
using System.Globalization;
using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vocalith.Localization;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// Profile Popup prefab 的运行时控制脚本，负责展示四个固定存档栏位。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Profile Popup")]
    public sealed class ProfileManagementUIScreen : GameUIScreen
    {
        [Serializable]
        private sealed class ProfileSlotView
        {
            [SerializeField] private RectTransform root;
            [SerializeField] private Image rootImage;
            [SerializeField] private GameObject timePanel;
            [SerializeField] private TMP_Text timeText;
            [SerializeField] private GameObject contentPanel;
            [SerializeField] private Button triggerButton;
            [SerializeField] private Button deleteButton;
            [SerializeField] private GameObject voidInfo;

            [NonSerialized] private Color rootDefaultColor = Color.white;
            [NonSerialized] private Color deleteDefaultColor = Color.white;
            [NonSerialized] private bool hasCapturedDefaultColors;

            public Button TriggerButton => triggerButton;
            public Button DeleteButton => deleteButton;

            public void TryAutoBind(Transform slotRoot)
            {
                if (slotRoot == null)
                {
                    return;
                }

                root ??= slotRoot as RectTransform;
                rootImage ??= slotRoot.GetComponent<Image>();
                timePanel ??= slotRoot.Find("Time Panel")?.gameObject;
                timeText ??= slotRoot.Find("Time Panel/Text (TMP)")?.GetComponent<TMP_Text>();
                contentPanel ??= slotRoot.Find("Content Panel")?.gameObject;
                triggerButton ??= slotRoot.Find("Trigger Button")?.GetComponent<Button>();
                deleteButton ??= slotRoot.Find("Delete Button")?.GetComponent<Button>();
                voidInfo ??= slotRoot.Find("Void Info")?.gameObject;

                CaptureDefaultColors();
            }

            public void Apply(ProfileSlotSummary summary, bool isSelected, bool isDeleteConfirmArmed, Color selectedColor, Color deleteConfirmColor)
            {
                if (root != null && !root.gameObject.activeSelf)
                {
                    root.gameObject.SetActive(true);
                }

                SetObjectActive(timePanel, summary.HasProfile);
                SetObjectActive(contentPanel, summary.HasProfile);
                SetObjectActive(deleteButton != null ? deleteButton.gameObject : null, summary.HasProfile);
                SetObjectActive(voidInfo, !summary.HasProfile);

                if (rootImage != null)
                {
                    rootImage.color = isSelected ? selectedColor : rootDefaultColor;
                }

                if (deleteButton != null && deleteButton.image != null)
                {
                    deleteButton.image.color = isDeleteConfirmArmed ? deleteConfirmColor : deleteDefaultColor;
                }

                if (timeText != null)
                {
                    timeText.text = summary.HasProfile
                        ? FormatSaveTime(summary.LastSavedUtcTicks)
                        : string.Empty;
                }
            }

            private void CaptureDefaultColors()
            {
                if (hasCapturedDefaultColors)
                {
                    return;
                }

                if (rootImage != null)
                {
                    rootDefaultColor = rootImage.color;
                }

                if (deleteButton != null && deleteButton.image != null)
                {
                    deleteDefaultColor = deleteButton.image.color;
                }

                hasCapturedDefaultColors = true;
            }

            private static void SetObjectActive(GameObject target, bool isActive)
            {
                if (target != null && target.activeSelf != isActive)
                {
                    target.SetActive(isActive);
                }
            }
        }

        [Header("Layout")]
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button topCloseButton;
        [SerializeField] private RectTransform mainContent;
        [SerializeField] private Color selectedSlotColor = new Color(0.78f, 0.89f, 1f, 1f);
        [SerializeField] private Color deleteConfirmColor = Color.red;
        [SerializeField] private ProfileSlotView[] slotViews = new ProfileSlotView[SavePathUtility.ProfileSlotCount];

        private UnityAction[] triggerButtonCallbacks = Array.Empty<UnityAction>();
        private UnityAction[] deleteButtonCallbacks = Array.Empty<UnityAction>();
        private int selectedSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
        private int pendingDeleteSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
        private bool isHandlingSlotAction;

        public override Status currentStatus { get; } = StatusList.PopUpStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindButtonCallbacks();
            RefreshView();
        }

        protected override void OnAfterHide()
        {
            ClearTransientState();
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindButtonCallbacks();
            ClearTransientState();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Profile Popup Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 根据当前 Profile Popup prefab 的固定层级自动补齐常用引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            popupRoot ??= transform.Find("Popup") as RectTransform;
            topPanel ??= popupRoot?.Find("Top Panel") as RectTransform;
            titleText ??= popupRoot?.Find("Top Panel/Tittle")?.GetComponent<TMP_Text>();
            titleText ??= popupRoot?.Find("Top Panel/Title")?.GetComponent<TMP_Text>();
            topCloseButton ??= popupRoot?.Find("Top Panel/Close Button/Edge/Button")?.GetComponent<Button>();
            mainContent ??= popupRoot?.Find("Main Content") as RectTransform;

            EnsureSlotViews();

            for (int slotIndex = 0; slotIndex < slotViews.Length; slotIndex++)
            {
                slotViews[slotIndex] ??= new ProfileSlotView();
                Transform slotRoot = mainContent != null && slotIndex < mainContent.childCount
                    ? mainContent.GetChild(slotIndex)
                    : null;

                slotViews[slotIndex].TryAutoBind(slotRoot);
            }
        }

        /// <summary>
        /// summary: 绑定顶部关闭按钮和四个栏位的触发按钮。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            BindButton(topCloseButton, HandleCloseClicked);
            EnsureCallbackArrays();

            for (int slotIndex = 0; slotIndex < slotViews.Length; slotIndex++)
            {
                ProfileSlotView slotView = slotViews[slotIndex];
                if (slotView == null)
                {
                    continue;
                }

                BindButton(slotView.TriggerButton, triggerButtonCallbacks[slotIndex]);
                BindButton(slotView.DeleteButton, deleteButtonCallbacks[slotIndex]);
            }
        }

        /// <summary>
        /// summary: 清理 Profile 弹窗按钮回调，避免对象销毁后残留委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            UnbindButton(topCloseButton, HandleCloseClicked);
            if (slotViews == null)
            {
                return;
            }

            EnsureCallbackArrays();
            for (int slotIndex = 0; slotIndex < slotViews.Length; slotIndex++)
            {
                ProfileSlotView slotView = slotViews[slotIndex];
                if (slotView == null)
                {
                    continue;
                }

                UnbindButton(slotView.TriggerButton, triggerButtonCallbacks[slotIndex]);
                UnbindButton(slotView.DeleteButton, deleteButtonCallbacks[slotIndex]);
            }
        }

        /// <summary>
        /// summary: 刷新四个固定栏位的可视状态；存在存档时显示时间，否则显示 Void Info。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshView()
        {
            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            {
                titleText.text = LocalizationManager.TranslateOrDefault("ui.profile.title", "Profile");
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null)
            {
                GameDebug.LogWarning("[ProfileManagementUIScreen] RuntimeSaveService is missing.");
                return;
            }

            ProfileSlotSummary[] summaries = saveService.GetSlotSummaries();
            NormalizePendingDeleteState(summaries);

            int maxCount = Math.Min(slotViews.Length, summaries.Length);
            for (int slotIndex = 0; slotIndex < maxCount; slotIndex++)
            {
                bool isSelected = selectedSlotIndex == slotIndex;
                bool isDeleteConfirmArmed = pendingDeleteSlotIndex == slotIndex && summaries[slotIndex].HasProfile;
                slotViews[slotIndex]?.Apply(summaries[slotIndex], isSelected, isDeleteConfirmArmed, selectedSlotColor, deleteConfirmColor);
            }
        }

        /// <summary>
        /// summary: 响应某个栏位的开始按钮；第一次点击只选中，第二次点击同一栏位才真正进入。
        /// param name="slotIndex": 被点击的栏位索引
        /// returns: 无
        /// </summary>
        private void HandleSlotTriggered(int slotIndex)
        {
            if (isHandlingSlotAction)
            {
                return;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null)
            {
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.save_unavailable", "存档服务不可用。")));
                return;
            }

            GlobalStartup startup = GlobalStartup.Instance;
            if (startup == null || !startup.IsBootCompleted)
            {
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.boot_not_ready", "启动流程尚未完成，请稍后再试。")));
                return;
            }

            if (selectedSlotIndex != slotIndex)
            {
                selectedSlotIndex = slotIndex;
                ClearDeleteConfirmationState();
                RefreshView();
                return;
            }

            ClearDeleteConfirmationState();
            RefreshView();

            isHandlingSlotAction = true;
            if (!saveService.SelectProfileSlot(slotIndex, out bool isNewSlot))
            {
                isHandlingSlotAction = false;
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.slot_init_failed", "当前栏位初始化失败。")));
                return;
            }

            RefreshView();

            bool requestAccepted = isNewSlot
                ? startup.RequestStartGame()
                : startup.RequestEnterMainScene();

            if (!requestAccepted)
            {
                isHandlingSlotAction = false;
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.enter_game_failed", "当前无法进入游戏，请稍后再试。")));
                return;
            }

            HandleCloseClicked();
        }

        /// <summary>
        /// summary: 响应某个栏位的删除按钮；第一次点击进入确认态，第二次点击才实际删除。
        /// param name="slotIndex": 被点击的栏位索引
        /// returns: 无
        /// </summary>
        private void HandleDeleteSlotClicked(int slotIndex)
        {
            if (isHandlingSlotAction)
            {
                return;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null)
            {
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.save_unavailable", "存档服务不可用。")));
                return;
            }

            if (pendingDeleteSlotIndex != slotIndex)
            {
                pendingDeleteSlotIndex = slotIndex;
                RefreshView();
                return;
            }

            isHandlingSlotAction = true;
            bool deleteSuccess = saveService.DeleteProfileSlot(slotIndex);
            isHandlingSlotAction = false;

            if (deleteSuccess && selectedSlotIndex == slotIndex)
            {
                selectedSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
            }

            ClearDeleteConfirmationState();
            RefreshView();

            if (!deleteSuccess)
            {
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.delete_failed", "删除存档失败。")));
            }
        }

        /// <summary>
        /// summary: 关闭当前 Profile 界面；当前仅作为 modal 使用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            if (ui == null)
            {
                return;
            }

            ui.CloseModal(this);
        }

        /// <summary>
        /// summary: 关闭按钮的统一入口，复用当前 modal 关闭路径。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleCloseClicked()
        {
            RequestClose();
        }

        /// <summary>
        /// summary: 把 UTC ticks 格式化为“绝对时间 + 相对时间”文案。
        /// param name="utcTicks": 最近一次保存时间的 UTC ticks
        /// returns: 可直接赋值给 TMP_Text 的本地时间字符串
        /// </summary>
        private static string FormatSaveTime(long utcTicks)
        {
            if (utcTicks <= 0L)
            {
                return LocalizationManager.TranslateOrDefault("ui.profile.unknown_time", "Unknown Time");
            }

            DateTime utcTime = new DateTime(utcTicks, DateTimeKind.Utc);
            DateTime localTime = utcTime.ToLocalTime();
            TimeSpan elapsed = DateTime.UtcNow - utcTime;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            return $"{localTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} {FormatRelativeElapsed(elapsed)}";
        }

        /// <summary>
        /// summary: 根据经过时间自动选择“秒前/分钟前/小时前/天前”。
        /// param name="elapsed": 距离当前的时间差
        /// returns: 相对时间文案
        /// </summary>
        private static string FormatRelativeElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalDays >= 1d)
            {
                return LocalizationManager.TranslateFormatOrDefault(
                    "ui.profile.elapsed_days",
                    "{0}天前",
                    Math.Max(1, (int)Math.Floor(elapsed.TotalDays)));
            }

            if (elapsed.TotalHours >= 1d)
            {
                return LocalizationManager.TranslateFormatOrDefault(
                    "ui.profile.elapsed_hours",
                    "{0}小时前",
                    Math.Max(1, (int)Math.Floor(elapsed.TotalHours)));
            }

            if (elapsed.TotalMinutes >= 1d)
            {
                return LocalizationManager.TranslateFormatOrDefault(
                    "ui.profile.elapsed_minutes",
                    "{0}分钟前",
                    Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes)));
            }

            return LocalizationManager.TranslateFormatOrDefault(
                "ui.profile.elapsed_seconds",
                "{0}秒前",
                Math.Max(1, (int)Math.Floor(elapsed.TotalSeconds)));
        }

        /// <summary>
        /// summary: 为四个栏位准备稳定的点击回调缓存，避免重复创建闭包或移除失败。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureCallbackArrays()
        {
            if (triggerButtonCallbacks.Length != SavePathUtility.ProfileSlotCount)
            {
                triggerButtonCallbacks = new UnityAction[SavePathUtility.ProfileSlotCount];
            }

            if (deleteButtonCallbacks.Length != SavePathUtility.ProfileSlotCount)
            {
                deleteButtonCallbacks = new UnityAction[SavePathUtility.ProfileSlotCount];
            }

            for (int slotIndex = 0; slotIndex < SavePathUtility.ProfileSlotCount; slotIndex++)
            {
                int capturedSlotIndex = slotIndex;
                triggerButtonCallbacks[slotIndex] ??= () => HandleSlotTriggered(capturedSlotIndex);
                deleteButtonCallbacks[slotIndex] ??= () => HandleDeleteSlotClicked(capturedSlotIndex);
            }
        }

        /// <summary>
        /// summary: 确保 slotViews 数组长度固定为四个栏位。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureSlotViews()
        {
            if (slotViews == null || slotViews.Length != SavePathUtility.ProfileSlotCount)
            {
                slotViews = new ProfileSlotView[SavePathUtility.ProfileSlotCount];
            }
        }

        /// <summary>
        /// summary: 清理删除确认和处理中状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ClearTransientState()
        {
            selectedSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
            pendingDeleteSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
            isHandlingSlotAction = false;
        }

        /// <summary>
        /// summary: 当当前待确认删除的栏位已经不存在或无效时，自动清理删除确认状态。
        /// param name="summaries": 当前栏位快照
        /// returns: 无
        /// </summary>
        private void NormalizePendingDeleteState(ProfileSlotSummary[] summaries)
        {
            if (!SavePathUtility.IsValidProfileSlotIndex(pendingDeleteSlotIndex))
            {
                pendingDeleteSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
                return;
            }

            if (summaries == null || pendingDeleteSlotIndex >= summaries.Length || !summaries[pendingDeleteSlotIndex].HasProfile)
            {
                pendingDeleteSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
            }
        }

        /// <summary>
        /// summary: 清空删除确认状态并恢复普通按钮颜色。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ClearDeleteConfirmationState()
        {
            pendingDeleteSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
        }

        /// <summary>
        /// summary: 为单个按钮安全绑定点击事件，避免重复注册。
        /// param name="button": 目标按钮
        /// param name="callback": 目标回调
        /// returns: 无
        /// </summary>
        private static void BindButton(Button button, UnityAction callback)
        {
            if (button == null || callback == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }

        /// <summary>
        /// summary: 为单个按钮安全移除点击事件。
        /// param name="button": 目标按钮
        /// param name="callback": 目标回调
        /// returns: 无
        /// </summary>
        private static void UnbindButton(Button button, UnityAction callback)
        {
            if (button == null || callback == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
        }

        /// <summary>
        /// summary: 在弹窗关闭或销毁时移除 PopUp 状态，避免状态残留影响输入路由。
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
