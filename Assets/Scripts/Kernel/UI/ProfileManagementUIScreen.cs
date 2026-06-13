using System;
using System.Collections.Generic;
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
    /// Profile Popup prefab 的运行时控制脚本，负责展示可加载的已有存档。
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
            [SerializeField] private TMP_Text voidText;

            [NonSerialized] private Color rootDefaultColor = Color.white;
            [NonSerialized] private Color deleteDefaultColor = Color.white;
            [NonSerialized] private bool hasCapturedDefaultColors;

            public RectTransform Root => root;
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
                voidText ??= slotRoot.Find("Void Info/Text (TMP)")?.GetComponent<TMP_Text>();

                CaptureDefaultColors();
            }

            public void Apply(ProfileSlotSummary summary, bool isSelected, bool isDeleteConfirmArmed, Color selectedColor, Color deleteConfirmColor)
            {
                SetObjectActive(root != null ? root.gameObject : null, true);
                SetObjectActive(timePanel, summary.HasProfile);
                SetObjectActive(contentPanel, summary.HasProfile);
                SetObjectActive(deleteButton != null ? deleteButton.gameObject : null, summary.HasProfile);
                SetObjectActive(voidInfo, !summary.HasProfile);

                if (triggerButton != null)
                {
                    triggerButton.interactable = summary.HasProfile;
                }

                if (deleteButton != null)
                {
                    deleteButton.interactable = summary.HasProfile;
                }

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
                        ? FormatProfileTime(summary.LastOpenedOrSavedUtcTicks)
                        : string.Empty;
                }
            }

            public void ApplyEmptyState(string message)
            {
                SetObjectActive(root != null ? root.gameObject : null, true);
                SetObjectActive(timePanel, false);
                SetObjectActive(contentPanel, false);
                SetObjectActive(deleteButton != null ? deleteButton.gameObject : null, false);
                SetObjectActive(voidInfo, true);

                if (triggerButton != null)
                {
                    triggerButton.interactable = false;
                }

                if (deleteButton != null)
                {
                    deleteButton.interactable = false;
                }

                if (rootImage != null)
                {
                    rootImage.color = rootDefaultColor;
                }

                if (timeText != null)
                {
                    timeText.text = string.Empty;
                }

                if (voidText != null)
                {
                    voidText.text = message;
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
        [SerializeField] private RectTransform slotListContent;
        [SerializeField] private RectTransform slotTemplate;
        [SerializeField] private Color selectedSlotColor = new(0.78f, 0.89f, 1f, 1f);
        [SerializeField] private Color deleteConfirmColor = Color.red;

        private readonly List<ProfileSlotView> renderedSlotViews = new();
        private readonly List<UnityAction> triggerButtonCallbacks = new();
        private readonly List<UnityAction> deleteButtonCallbacks = new();
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
        /// summary: 根据当前 Profile Popup prefab 的滚动列表层级自动补齐常用引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            popupRoot ??= FindInContentSafeFrame("Popup") as RectTransform;
            topPanel ??= popupRoot?.Find("Top Panel") as RectTransform;
            titleText ??= popupRoot?.Find("Top Panel/Tittle")?.GetComponent<TMP_Text>();
            titleText ??= popupRoot?.Find("Top Panel/Title")?.GetComponent<TMP_Text>();
            if (topCloseButton == null)
            {
                topCloseButton = FindButton(popupRoot, "Top Panel/Close Button");
            }

            slotListContent ??= popupRoot?.Find("Main /Viewport/Content") as RectTransform;
            slotListContent ??= popupRoot?.Find("Main/Viewport/Content") as RectTransform;
            slotListContent ??= popupRoot?.Find("Main Content") as RectTransform;

            if (slotTemplate == null && slotListContent != null && slotListContent.childCount > 0)
            {
                slotTemplate = slotListContent.GetChild(0) as RectTransform;
            }
        }

        private static Button FindButton(Transform root, string relativePath)
        {
            Transform target = root != null ? root.Find(relativePath) : null;
            if (target == null)
            {
                return null;
            }

            Button button = target.GetComponent<Button>();
            return button != null ? button : target.GetComponentInChildren<Button>(true);
        }

        /// <summary>
        /// summary: 绑定顶部关闭按钮；存档条目按钮会在刷新列表时动态绑定。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            BindButton(topCloseButton, HandleCloseClicked);
        }

        /// <summary>
        /// summary: 清理 Profile 弹窗按钮回调，避免对象销毁后残留委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            UnbindButton(topCloseButton, HandleCloseClicked);
            UnbindRenderedSlotCallbacks();
        }

        /// <summary>
        /// summary: 刷新可滚动列表；只展示当前磁盘上已有的存档。
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

            ProfileSlotSummary[] summaries = saveService.GetExistingSlotSummaries();
            NormalizeTransientSlotState(summaries);
            RebuildSlotRows(summaries);
        }

        /// <summary>
        /// summary: 响应某个已有存档栏位；第一次点击只选中，第二次点击同一栏位才真正加载。
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

            if (!StartupFlowBridge.HasStartup || !StartupFlowBridge.IsBootCompleted)
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
            if (!saveService.SelectExistingProfileSlot(slotIndex))
            {
                isHandlingSlotAction = false;
                RefreshView();
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(ProfileManagementUIScreen),
                    LocalizationManager.TranslateOrDefault("ui.profile.slot_missing", "该存档已不存在。")));
                return;
            }

            bool requestAccepted = StartupFlowBridge.RequestEnterMainScene();
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
        /// param name="utcTicks": 最近一次打开时间的 UTC ticks；旧档缺少打开时间时会回退到保存时间
        /// returns: 可直接赋值给 TMP_Text 的本地时间字符串
        /// </summary>
        private static string FormatProfileTime(long utcTicks)
        {
            if (utcTicks <= 0L)
            {
                return LocalizationManager.TranslateOrDefault("ui.profile.unknown_time", "Unknown Time");
            }

            DateTime utcTime = new(utcTicks, DateTimeKind.Utc);
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

        private void RebuildSlotRows(ProfileSlotSummary[] summaries)
        {
            EnsureRenderedSlotViewCount(Math.Max(1, summaries?.Length ?? 0));
            UnbindRenderedSlotCallbacks();

            if (renderedSlotViews.Count == 0)
            {
                GameDebug.LogWarning("[ProfileManagementUIScreen] Profile slot template is missing.");
                return;
            }

            int summaryCount = summaries?.Length ?? 0;
            for (int i = 0; i < renderedSlotViews.Count; i++)
            {
                ProfileSlotView slotView = renderedSlotViews[i];
                if (slotView?.Root != null)
                {
                    slotView.Root.gameObject.SetActive(i < Math.Max(1, summaryCount));
                }
            }

            if (summaryCount == 0)
            {
                renderedSlotViews[0].ApplyEmptyState(LocalizationManager.TranslateOrDefault("ui.profile.no_saves", "暂无可加载存档。"));
                return;
            }

            for (int i = 0; i < summaryCount; i++)
            {
                ProfileSlotSummary summary = summaries[i];
                ProfileSlotView slotView = renderedSlotViews[i];
                bool isSelected = selectedSlotIndex == summary.SlotIndex;
                bool isDeleteConfirmArmed = pendingDeleteSlotIndex == summary.SlotIndex && summary.HasProfile;
                slotView.Apply(summary, isSelected, isDeleteConfirmArmed, selectedSlotColor, deleteConfirmColor);

                int capturedSlotIndex = summary.SlotIndex;
                UnityAction triggerCallback = () => HandleSlotTriggered(capturedSlotIndex);
                UnityAction deleteCallback = () => HandleDeleteSlotClicked(capturedSlotIndex);
                triggerButtonCallbacks.Add(triggerCallback);
                deleteButtonCallbacks.Add(deleteCallback);
                BindButton(slotView.TriggerButton, triggerCallback);
                BindButton(slotView.DeleteButton, deleteCallback);
            }
        }

        private void EnsureRenderedSlotViewCount(int requiredCount)
        {
            if (slotTemplate == null)
            {
                TryAutoBindReferences();
            }

            if (slotTemplate == null)
            {
                return;
            }

            if (renderedSlotViews.Count == 0)
            {
                ProfileSlotView templateView = new();
                templateView.TryAutoBind(slotTemplate);
                renderedSlotViews.Add(templateView);
            }

            while (renderedSlotViews.Count < requiredCount)
            {
                RectTransform cloneRoot = Instantiate(slotTemplate, slotListContent != null ? slotListContent : slotTemplate.parent);
                cloneRoot.gameObject.name = $"{slotTemplate.gameObject.name} ({renderedSlotViews.Count + 1})";
                ProfileSlotView slotView = new();
                slotView.TryAutoBind(cloneRoot);
                renderedSlotViews.Add(slotView);
            }
        }

        private void UnbindRenderedSlotCallbacks()
        {
            int triggerCount = Math.Min(triggerButtonCallbacks.Count, renderedSlotViews.Count);
            for (int i = 0; i < triggerCount; i++)
            {
                UnbindButton(renderedSlotViews[i].TriggerButton, triggerButtonCallbacks[i]);
            }

            int deleteCount = Math.Min(deleteButtonCallbacks.Count, renderedSlotViews.Count);
            for (int i = 0; i < deleteCount; i++)
            {
                UnbindButton(renderedSlotViews[i].DeleteButton, deleteButtonCallbacks[i]);
            }

            triggerButtonCallbacks.Clear();
            deleteButtonCallbacks.Clear();
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
        /// summary: 当当前选中或待删除栏位已经不存在时，自动清理临时状态。
        /// param name="summaries": 当前已有存档快照
        /// returns: 无
        /// </summary>
        private void NormalizeTransientSlotState(ProfileSlotSummary[] summaries)
        {
            if (!ContainsSlot(summaries, selectedSlotIndex))
            {
                selectedSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
            }

            if (!ContainsSlot(summaries, pendingDeleteSlotIndex))
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

        private static bool ContainsSlot(ProfileSlotSummary[] summaries, int slotIndex)
        {
            if (!SavePathUtility.IsValidProfileSlotIndex(slotIndex) || summaries == null)
            {
                return false;
            }

            for (int i = 0; i < summaries.Length; i++)
            {
                if (summaries[i].SlotIndex == slotIndex && summaries[i].HasProfile)
                {
                    return true;
                }
            }

            return false;
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
