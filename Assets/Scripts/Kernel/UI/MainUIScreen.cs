using System.Collections;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.GameState;
using Kernel.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.EventSystem;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// MainUI 的运行时模板脚本，负责暴露 HUD 常用引用，并同步顶部 spell panel 的只读展示。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/MainUI")]
    public class MainUIScreen : GameUIScreen
    {
        [Header("Layout")]
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform healthPanel;
        [SerializeField] private TMP_Text healthTitleText;
        [SerializeField] private RectTransform healthBarRoot;
        [SerializeField] private PlayerHealthBarController healthBarController;
        [SerializeField] private RectTransform spellPanel;
        [SerializeField] private BackPackGridSlotView spellSlotTemplate;
        [SerializeField] private RectTransform pauseButtonRoot;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text pauseButtonText;

        [Header("Quest Panel")]
        [SerializeField] private RectTransform questPanel;
        [SerializeField] private RectTransform questListRoot;
        [SerializeField] private QuestEntryView questEntryPrefab;
        [SerializeField, Min(0.01f)] private float questEntryFadeDuration = 0.18f;

        [Header("Danger Edge")]
        [SerializeField] private RectTransform dangerEdge;
        [SerializeField] private Image dangerEdgeImage;
        [SerializeField, Range(0f, 1f)] private float dangerHealthRatioThreshold = 0.2f;
        [SerializeField, Range(0f, 1f)] private float dangerVisibleAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float dangerFlashAlpha = 0.35f;
        [SerializeField, Min(0.01f)] private float dangerFlashDuration = 0.16f;

        [Header("Objective Arrow")]
        [SerializeField] private ObjectiveArrowView objectiveArrowView;

        [Header("Reward Notification")]
        [SerializeField] private RectTransform notificationPanel;
        [SerializeField] private CanvasGroup notificationCanvasGroup;
        [SerializeField] private TMP_Text notificationTitleText;
        [SerializeField] private TMP_Text notificationDescriptionText;
        [SerializeField] private Image notificationImage;
        [SerializeField, Min(0f)] private float notificationDisplaySeconds = 2f;
        [SerializeField, Min(0f)] private float notificationFadeSeconds = 0.18f;

        [Header("Linked Outline")]
        [SerializeField] private Color linkedOutlineColor = new(1f, 0.84f, 0.35f, 0.95f);
        [SerializeField, Min(1f)] private float linkedOutlineThickness = 4f;
        [SerializeField] private Vector2 linkedOutlinePadding = new(6f, 6f);

        private readonly List<BackPackGridSlotView> runtimeSpellSlots = new();
        private readonly List<LinkedTokenOutlineView> runtimeSpellOutlines = new();
        private readonly Dictionary<string, QuestEntryView> runtimeQuestEntries = new(System.StringComparer.Ordinal);
        private readonly Dictionary<string, Coroutine> questEntryFadeCoroutines = new(System.StringComparer.Ordinal);
        private PlayerPlaneMovement currentPlayer;
        private SpellBookLoadout currentLoadout;
        private bool hasLoggedMissingSpellTemplate;
        private RectTransform spellLinkedOutlineLayer;
        private System.IDisposable playerHealthChangedSubscription;
        private System.IDisposable rewardNotificationSubscription;
        private PlayerHealth currentPlayerHealth;
        private Coroutine dangerEdgeFlashCoroutine;
        private Coroutine notificationCoroutine;
        private bool isDangerEdgeVisible;
        private QuestService currentQuestService;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        public RectTransform TopPanel => topPanel;
        public RectTransform HealthPanel => healthPanel;
        public TMP_Text HealthTitleText => healthTitleText;
        public RectTransform HealthBarRoot => healthBarRoot;
        public PlayerHealthBarController HealthBarController => healthBarController;
        public RectTransform SpellPanel => spellPanel;
        public BackPackGridSlotView SpellSlotTemplate => spellSlotTemplate;
        public RectTransform PauseButtonRoot => pauseButtonRoot;
        public Button PauseButton => pauseButton;
        public TMP_Text PauseButtonText => pauseButtonText;
        public RectTransform QuestPanel => questPanel;
        public RectTransform QuestListRoot => questListRoot;
        public QuestEntryView QuestEntryPrefab => questEntryPrefab;
        public RectTransform DangerEdge => dangerEdge;
        public Image DangerEdgeImage => dangerEdgeImage;
        public ObjectiveArrowView ObjectiveArrowView => objectiveArrowView;
        public RectTransform NotificationPanel => notificationPanel;
        public CanvasGroup NotificationCanvasGroup => notificationCanvasGroup != null
            ? notificationCanvasGroup
            : notificationPanel != null ? notificationPanel.GetComponent<CanvasGroup>() : null;
        public TMP_Text NotificationTitleText => notificationTitleText != null
            ? notificationTitleText
            : notificationPanel != null ? notificationPanel.Find("Tittle/Text (TMP)")?.GetComponent<TMP_Text>() : null;
        public TMP_Text NotificationDescriptionText => notificationDescriptionText != null
            ? notificationDescriptionText
            : notificationPanel != null ? notificationPanel.Find("Description/Text (TMP)")?.GetComponent<TMP_Text>() : null;
        public Image NotificationImage => notificationImage != null
            ? notificationImage
            : notificationPanel != null ? notificationPanel.Find("Image")?.GetComponent<Image>() : null;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            SanitizeDangerEdgeConfiguration();
            SanitizeNotificationConfiguration();
            BindButtonCallbacks();
            RefreshSpellPanel();
            RefreshQuestEntries();
            ResetDangerEdgeDisplay();
            ResetNotificationDisplay();
            ui?.EnsureOverlay<BossInfoUIScreen>();
        }

        protected override void OnBeforeShow()
        {
            RefreshSpellPanel();
            SubscribeToQuestEvents();
            RefreshQuestEntries();
            SubscribeToPlayerHealthEvents();
            SyncDangerEdgeToCurrentHealth();
            SubscribeToRewardNotifications();
        }

        protected override void OnAfterHide()
        {
            DisposeQuestSubscriptions();
            ClearRuntimeQuestEntries();
            ReleaseLoadoutBinding();
            ClearRuntimeSpellSlots();
            currentPlayerHealth = null;
            DisposePlayerHealthSubscription();
            DisposeRewardNotificationSubscription();
            objectiveArrowView?.ClearTarget();
            ResetDangerEdgeDisplay();
            ResetNotificationDisplay();
        }

        private void OnDestroy()
        {
            UnbindButtonCallbacks();
            DisposeQuestSubscriptions();
            ClearRuntimeQuestEntries();
            ReleaseLoadoutBinding();
            ClearRuntimeSpellSlots();
            currentPlayerHealth = null;
            DisposePlayerHealthSubscription();
            DisposeRewardNotificationSubscription();
            objectiveArrowView?.ClearTarget();
            ResetDangerEdgeDisplay();
            ResetNotificationDisplay();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            SanitizeDangerEdgeConfiguration();
            SanitizeNotificationConfiguration();
        }

        [ContextMenu("Auto Bind Main UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// 按当前 MainUI prefab 的层级自动补齐常用字段，减少手动拖拽成本。
        /// </summary>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
            if (dangerEdgeImage != null && dangerEdge == null)
            {
                dangerEdge = dangerEdgeImage.rectTransform;
            }

            dangerEdge ??= transform.Find("Danger Edge") as RectTransform;
            if (dangerEdge != null)
            {
                dangerEdgeImage ??= dangerEdge.GetComponent<Image>();
            }

            notificationPanel ??= transform.Find("Notification Panel") as RectTransform;
            if (notificationPanel != null)
            {
                notificationCanvasGroup ??= notificationPanel.GetComponent<CanvasGroup>();
                notificationTitleText ??= notificationPanel.Find("Tittle/Text (TMP)")?.GetComponent<TMP_Text>();
                notificationTitleText ??= notificationPanel.Find("Title/Text (TMP)")?.GetComponent<TMP_Text>();
                notificationDescriptionText ??= notificationPanel.Find("Description/Text (TMP)")?.GetComponent<TMP_Text>();
                notificationImage ??= notificationPanel.Find("Image")?.GetComponent<Image>();
            }

            if (objectiveArrowView == null)
            {
                Transform arrowPanel = transform.Find("Arrow Panel");
                if (arrowPanel != null)
                {
                    objectiveArrowView = arrowPanel.GetComponent<ObjectiveArrowView>();
                }
            }

            topPanel ??= transform.Find("TopPanel") as RectTransform;
            if (topPanel == null)
            {
                return;
            }

            healthPanel ??= topPanel.Find("HP Bar") as RectTransform;
            if (healthPanel != null)
            {
                healthTitleText ??= healthPanel.Find("Titlle")?.GetComponent<TMP_Text>();
                healthBarRoot ??= healthPanel.Find("Bar") as RectTransform;
                if (healthBarRoot != null)
                {
                    healthBarController ??= healthBarRoot.GetComponent<PlayerHealthBarController>();
                }
            }

            spellPanel ??= topPanel.Find("Spell Panel") as RectTransform;
            if (spellPanel != null && (spellSlotTemplate == null || !spellSlotTemplate.transform.IsChildOf(spellPanel)))
            {
                spellSlotTemplate = ResolveSpellSlotTemplate(spellPanel);
            }

            questPanel ??= transform.Find("Quest Panel") as RectTransform;
            if (questPanel != null)
            {
                questListRoot ??= questPanel.Find("Quests") as RectTransform;
            }

            pauseButtonRoot ??= ResolvePauseButtonRoot(topPanel);
            if (pauseButtonRoot != null)
            {
                pauseButton ??= pauseButtonRoot.GetComponentInChildren<Button>(true);
                if (pauseButton != null)
                {
                    pauseButtonText ??= pauseButton.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        /// <summary>
        /// summary: 把 MainUI 的暂停按钮接到统一的 UIInputRouter 暂停入口。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            if (pauseButton == null)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(HandlePauseButtonClicked);
            pauseButton.onClick.AddListener(HandlePauseButtonClicked);
        }

        /// <summary>
        /// summary: 清理 MainUI 暂停按钮事件，避免对象销毁后残留无效委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            if (pauseButton == null)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(HandlePauseButtonClicked);
        }

        /// <summary>
        /// summary: 点击暂停按钮时，通过 UIInputRouter 请求打开暂停菜单。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandlePauseButtonClicked()
        {
            UIInputRouter.Instance?.RequestOpenPauseMenu();
        }

        /// <summary>
        /// summary: 重新解析玩家 loadout，并按当前 token 顺序刷新 HUD 顶部的 Spell Panel。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshSpellPanel()
        {
            TryAutoBindReferences();
            if (!PrepareSpellSlotTemplate())
            {
                ClearRuntimeSpellSlots();
                return;
            }

            if (!TryResolveLoadoutBinding())
            {
                ClearRuntimeSpellSlots();
                return;
            }

            RebuildSpellSlots();
        }

        /// <summary>
        /// summary: 使用 MainUI prefab 中预放的首个 BackPack Grid Prefab 作为 runtime 克隆模板。
        /// param: 无
        /// returns: 找到并完成初始化时返回 true
        /// </summary>
        private bool PrepareSpellSlotTemplate()
        {
            if (spellPanel == null)
            {
                return false;
            }

            spellSlotTemplate ??= ResolveSpellSlotTemplate(spellPanel);
            if (spellSlotTemplate == null)
            {
                if (!hasLoggedMissingSpellTemplate)
                {
                    GameDebug.LogWarning("[MainUIScreen] Spell Panel is missing a BackPackGridSlotView template child.");
                    hasLoggedMissingSpellTemplate = true;
                }

                return false;
            }

            hasLoggedMissingSpellTemplate = false;
            EnsureSpellOutlineLayer();
            spellSlotTemplate.InitializeDisplayOnly(BackPackSlotArea.SpellBook);
            spellSlotTemplate.SetToken(null);
            if (spellSlotTemplate.gameObject.activeSelf)
            {
                spellSlotTemplate.gameObject.SetActive(false);
            }

            return true;
        }

        /// <summary>
        /// summary: 解析场景中的玩家与 SpellBookLoadout，并维护 HUD 订阅的事件绑定。
        /// param: 无
        /// returns: 成功拿到可观察的 loadout 时返回 true
        /// </summary>
        private bool TryResolveLoadoutBinding()
        {
            PlayerPlaneMovement resolvedPlayer = FindAnyObjectByType<PlayerPlaneMovement>();
            if (resolvedPlayer == null)
            {
                ReleaseLoadoutBinding();
                return false;
            }

            SpellBookLoadout resolvedLoadout = resolvedPlayer.GetComponent<SpellBookLoadout>();
            if (resolvedLoadout == null)
            {
                ReleaseLoadoutBinding();
                return false;
            }

            currentPlayer = resolvedPlayer;
            currentPlayerHealth = ResolvePlayerHealthFromPlayer(resolvedPlayer);
            if (currentLoadout == resolvedLoadout)
            {
                return true;
            }

            if (currentLoadout != null)
            {
                currentLoadout.Changed -= HandleLoadoutChanged;
            }

            currentLoadout = resolvedLoadout;
            currentLoadout.Changed += HandleLoadoutChanged;
            return true;
        }

        /// <summary>
        /// summary: 释放当前 loadout 事件订阅，避免界面销毁后继续收到刷新回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ReleaseLoadoutBinding()
        {
            if (currentLoadout != null)
            {
                currentLoadout.Changed -= HandleLoadoutChanged;
            }

            currentPlayer = null;
            currentLoadout = null;
        }

        /// <summary>
        /// summary: 按当前 loadout 中的非空 token 顺序重建 Spell Panel 的可见槽位，不显示空占位。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RebuildSpellSlots()
        {
            ClearRuntimeSpellSlots();
            if (spellPanel == null || spellSlotTemplate == null || currentLoadout == null)
            {
                return;
            }

            IReadOnlyList<PlaceableTokenData> items = currentLoadout.ExecutionItems;
            if (items == null)
            {
                return;
            }

            int visibleIndex = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PlaceableTokenData item = items[i];
                if (item == null)
                {
                    continue;
                }

                int anchorIndex = visibleIndex;
                int span = Mathf.Max(1, item.SlotSpan);
                for (int offset = 0; offset < span; offset++)
                {
                    BackPackGridSlotView slotView = Instantiate(spellSlotTemplate, spellPanel);
                    slotView.gameObject.SetActive(true);
                    slotView.name = $"Spell Slot {visibleIndex + 1:D2}";
                    slotView.InitializeDisplayOnly(BackPackSlotArea.SpellBook);
                    slotView.SetOccupancy(new TokenCellOccupancy(item, anchorIndex, offset, offset == 0));
                    runtimeSpellSlots.Add(slotView);
                    visibleIndex++;
                }
            }

            RefreshSpellOutlines();
        }

        /// <summary>
        /// summary: 清理 Spell Panel 下当前由 MainUIScreen 运行时克隆出的槽位实例。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ClearRuntimeSpellSlots()
        {
            for (int i = runtimeSpellSlots.Count - 1; i >= 0; i--)
            {
                DestroyChild(runtimeSpellSlots[i] != null ? runtimeSpellSlots[i].gameObject : null);
            }

            runtimeSpellSlots.Clear();
            SetLinkedOutlineViewsVisible(runtimeSpellOutlines, 0);
        }

        /// <summary>
        /// summary: 当 loadout 内容被背包或其他系统改写后，立即刷新 HUD 顶部展示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleLoadoutChanged()
        {
            RefreshSpellPanel();
        }

        /// <summary>
        /// summary: 订阅任务服务的激活列表与完成通知，驱动 Quest Panel 的运行时刷新。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SubscribeToQuestEvents()
        {
            QuestService service = QuestService.GetOrCreateInstance();
            if (currentQuestService == service)
            {
                return;
            }

            DisposeQuestSubscriptions();
            currentQuestService = service;
            if (currentQuestService == null)
            {
                return;
            }

            currentQuestService.ActiveQuestsChanged += HandleActiveQuestsChanged;
            currentQuestService.QuestCompleted += HandleQuestCompleted;
        }

        /// <summary>
        /// summary: 释放 Quest Panel 对任务服务的事件订阅。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void DisposeQuestSubscriptions()
        {
            if (currentQuestService != null)
            {
                currentQuestService.ActiveQuestsChanged -= HandleActiveQuestsChanged;
                currentQuestService.QuestCompleted -= HandleQuestCompleted;
                currentQuestService = null;
            }
        }

        /// <summary>
        /// summary: 当任务激活列表变化时，重建或更新 Quest Panel 下的条目实例。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleActiveQuestsChanged()
        {
            RefreshQuestEntries();
        }

        /// <summary>
        /// summary: 当任务完成时，为对应条目播放一次淡出并在结束后销毁该条目。
        /// param name="snapshot": 刚完成任务的只读通知
        /// returns: 无
        /// </summary>
        private void HandleQuestCompleted(QuestCompletedSnapshot snapshot)
        {
            if (!runtimeQuestEntries.TryGetValue(snapshot.QuestId, out QuestEntryView entryView) || entryView == null)
            {
                return;
            }

            if (questEntryFadeCoroutines.ContainsKey(snapshot.QuestId))
            {
                return;
            }

            questEntryFadeCoroutines[snapshot.QuestId] = StartCoroutine(PlayQuestEntryFadeOutCo(snapshot.QuestId, entryView));
        }

        /// <summary>
        /// summary: 按任务服务当前快照同步 Quest Panel 条目，只创建缺失条目并移除非激活条目。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshQuestEntries()
        {
            TryAutoBindReferences();
            QuestService service = currentQuestService ?? QuestService.Instance;
            IReadOnlyList<QuestActiveSnapshot> snapshots = service != null ? service.GetActiveQuestSnapshots() : null;
            SetQuestPanelVisible(snapshots != null && snapshots.Count > 0);

            if (questListRoot == null || questEntryPrefab == null)
            {
                return;
            }

            HashSet<string> activeQuestIds = new(System.StringComparer.Ordinal);
            if (snapshots != null)
            {
                for (int index = 0; index < snapshots.Count; index++)
                {
                    QuestActiveSnapshot snapshot = snapshots[index];
                    activeQuestIds.Add(snapshot.QuestId);
                    if (questEntryFadeCoroutines.ContainsKey(snapshot.QuestId))
                    {
                        continue;
                    }

                    if (!runtimeQuestEntries.TryGetValue(snapshot.QuestId, out QuestEntryView entryView) || entryView == null)
                    {
                        entryView = Instantiate(questEntryPrefab, questListRoot);
                        entryView.name = $"Quest Entry - {snapshot.QuestId}";
                        runtimeQuestEntries[snapshot.QuestId] = entryView;
                    }

                    if (entryView.CanvasGroup != null)
                    {
                        entryView.CanvasGroup.alpha = 1f;
                    }

                    entryView.gameObject.SetActive(true);
                    entryView.SetText(snapshot.Text);
                }
            }

            List<string> staleQuestIds = new();
            foreach (KeyValuePair<string, QuestEntryView> pair in runtimeQuestEntries)
            {
                if (!activeQuestIds.Contains(pair.Key) && !questEntryFadeCoroutines.ContainsKey(pair.Key))
                {
                    staleQuestIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < staleQuestIds.Count; index++)
            {
                DestroyQuestEntry(staleQuestIds[index]);
            }
        }

        /// <summary>
        /// summary: 按当前任务状态控制 Quest Panel 的整体显隐。
        /// param name="visible": Quest Panel 是否显示
        /// returns: 无
        /// </summary>
        private void SetQuestPanelVisible(bool visible)
        {
            if (questPanel == null)
            {
                return;
            }

            questPanel.gameObject.SetActive(visible);
        }

        /// <summary>
        /// summary: 清理 Quest Panel 下所有运行时条目和正在执行的淡出协程。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ClearRuntimeQuestEntries()
        {
            foreach (KeyValuePair<string, Coroutine> pair in questEntryFadeCoroutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            questEntryFadeCoroutines.Clear();

            List<string> questIds = new(runtimeQuestEntries.Keys);
            for (int index = 0; index < questIds.Count; index++)
            {
                DestroyQuestEntry(questIds[index]);
            }

            runtimeQuestEntries.Clear();
        }

        /// <summary>
        /// summary: 播放单个任务条目的淡出动画，结束后销毁对应 runtime 实例。
        /// param name="questId": 当前正在淡出的任务标识
        /// param name="entryView": 需要执行淡出的 Quest Entry 视图
        /// returns: 淡出协程
        /// </summary>
        private IEnumerator PlayQuestEntryFadeOutCo(string questId, QuestEntryView entryView)
        {
            CanvasGroup canvasGroup = entryView != null ? entryView.CanvasGroup : null;
            if (canvasGroup != null)
            {
                float startAlpha = canvasGroup.alpha;
                float elapsed = 0f;
                while (elapsed < questEntryFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = questEntryFadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / questEntryFadeDuration);
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                    yield return null;
                }

                canvasGroup.alpha = 0f;
            }

            questEntryFadeCoroutines.Remove(questId);
            DestroyQuestEntry(questId);
            RefreshQuestEntries();
        }

        /// <summary>
        /// summary: 销毁单个 Quest Entry 运行时实例，并清理内部索引。
        /// param name="questId": 需要移除的任务标识
        /// returns: 无
        /// </summary>
        private void DestroyQuestEntry(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return;
            }

            if (runtimeQuestEntries.TryGetValue(questId, out QuestEntryView entryView) && entryView != null)
            {
                DestroyChild(entryView.gameObject);
            }

            runtimeQuestEntries.Remove(questId);
            questEntryFadeCoroutines.Remove(questId);
        }

        /// <summary>
        /// summary: 订阅玩家生命变化事件，驱动 Danger Edge 的显隐与受击闪烁。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SubscribeToPlayerHealthEvents()
        {
            if (playerHealthChangedSubscription != null)
            {
                return;
            }

            playerHealthChangedSubscription = EventManager.eventBus.Subscribe<PlayerHealthChangedEvent>(HandlePlayerHealthChanged);
        }

        /// <summary>
        /// summary: 释放 Danger Edge 的生命事件订阅。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void DisposePlayerHealthSubscription()
        {
            playerHealthChangedSubscription?.Dispose();
            playerHealthChangedSubscription = null;
        }

        private void SubscribeToRewardNotifications()
        {
            rewardNotificationSubscription ??= EventManager.eventBus.Subscribe<RewardNotificationEvent>(HandleRewardNotification);
        }

        private void DisposeRewardNotificationSubscription()
        {
            rewardNotificationSubscription?.Dispose();
            rewardNotificationSubscription = null;
        }

        private void HandleRewardNotification(RewardNotificationEvent evt)
        {
            ShowRewardNotification(evt.title, evt.description);
        }

        private void ShowRewardNotification(string title, string description)
        {
            TryAutoBindReferences();
            if (notificationPanel == null)
            {
                return;
            }

            if (notificationTitleText != null)
            {
                notificationTitleText.text = string.IsNullOrWhiteSpace(title) ? "获得奖励" : title;
            }

            if (notificationDescriptionText != null)
            {
                notificationDescriptionText.richText = true;
                notificationDescriptionText.text = description ?? string.Empty;
            }

            if (notificationImage != null)
            {
                notificationImage.sprite = null;
                notificationImage.enabled = false;
                notificationImage.gameObject.SetActive(false);
            }

            StopNotificationCoroutine();
            SetNotificationVisible(true);
            if (isActiveAndEnabled)
            {
                notificationCoroutine = StartCoroutine(PlayNotificationAutoHideCo());
            }
        }

        private IEnumerator PlayNotificationAutoHideCo()
        {
            if (notificationDisplaySeconds > 0f)
            {
                float displayElapsed = 0f;
                while (displayElapsed < notificationDisplaySeconds)
                {
                    displayElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (notificationCanvasGroup == null || notificationFadeSeconds <= 0f)
            {
                SetNotificationAlpha(0f);
                SetNotificationVisible(false);
                notificationCoroutine = null;
                yield break;
            }

            float startAlpha = notificationCanvasGroup.alpha;
            float fadeElapsed = 0f;
            while (fadeElapsed < notificationFadeSeconds)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(fadeElapsed / notificationFadeSeconds);
                SetNotificationAlpha(Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }

            SetNotificationAlpha(0f);
            SetNotificationVisible(false);
            notificationCoroutine = null;
        }

        private void ResetNotificationDisplay()
        {
            StopNotificationCoroutine();
            SetNotificationAlpha(0f);
            SetNotificationVisible(false);
        }

        private void StopNotificationCoroutine()
        {
            if (notificationCoroutine == null)
            {
                return;
            }

            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        private void SetNotificationVisible(bool visible)
        {
            if (notificationPanel != null)
            {
                notificationPanel.gameObject.SetActive(visible);
            }

            SetNotificationAlpha(visible ? 1f : 0f);
        }

        private void SetNotificationAlpha(float alpha)
        {
            if (notificationCanvasGroup == null)
            {
                return;
            }

            notificationCanvasGroup.alpha = Mathf.Clamp01(alpha);
            notificationCanvasGroup.blocksRaycasts = false;
            notificationCanvasGroup.interactable = false;
        }

        /// <summary>
        /// summary: 在界面显示时按当前生命值立即刷新 Danger Edge 的可见状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SyncDangerEdgeToCurrentHealth()
        {
            if (!TryResolveTargetPlayerHealth())
            {
                SetDangerEdgeVisible(false);
                return;
            }

            UpdateDangerEdgeState(currentPlayerHealth.CurrentHealth, currentPlayerHealth.MaxHealth, triggerFlashOnDamage: false);
        }

        /// <summary>
        /// summary: 解析当前 HUD 需要跟踪的玩家生命组件。
        /// param: 无
        /// returns: 成功解析到玩家生命组件时返回 true
        /// </summary>
        private bool TryResolveTargetPlayerHealth()
        {
            if (currentPlayerHealth != null)
            {
                return true;
            }

            currentPlayerHealth = ResolvePlayerHealthFromPlayer(currentPlayer);
            if (currentPlayerHealth != null)
            {
                return true;
            }

            currentPlayerHealth = FindAnyObjectByType<PlayerHealth>();
            return currentPlayerHealth != null;
        }

        /// <summary>
        /// summary: 生命值变化后刷新 Danger Edge；低血已显示时再次受击会触发一次闪烁。
        /// param: evt 本次生命变化事件
        /// returns: 无
        /// </summary>
        private void HandlePlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (!IsTrackedPlayerHealth(evt.source))
            {
                return;
            }

            currentPlayerHealth = evt.source;
            bool tookDamage = evt.delta < 0f;
            UpdateDangerEdgeState(evt.currentHealth, evt.maxHealth, tookDamage);
        }

        /// <summary>
        /// summary: 判断本次事件是否来自当前 MainUI 跟踪的玩家生命组件。
        /// param: playerHealth 事件携带的生命组件
        /// returns: 命中当前跟踪目标时返回 true
        /// </summary>
        private bool IsTrackedPlayerHealth(PlayerHealth playerHealth)
        {
            if (playerHealth == null)
            {
                return false;
            }

            if (currentPlayerHealth != null)
            {
                return playerHealth == currentPlayerHealth;
            }

            if (currentPlayer != null)
            {
                return playerHealth.transform.root == currentPlayer.transform.root;
            }

            return true;
        }

        /// <summary>
        /// summary: 根据生命百分比决定 Danger Edge 显示状态，并按需触发受击闪烁。
        /// param: currentHealth 当前生命值
        /// param: maxHealth 当前最大生命值
        /// param: triggerFlashOnDamage 本次是否需要触发受击闪烁
        /// returns: 无
        /// </summary>
        private void UpdateDangerEdgeState(float currentHealth, float maxHealth, bool triggerFlashOnDamage)
        {
            float safeMaxHealth = Mathf.Max(0f, maxHealth);
            float healthRatio = safeMaxHealth > 0f ? Mathf.Clamp01(currentHealth / safeMaxHealth) : 1f;
            bool shouldShowDanger = safeMaxHealth > 0f && healthRatio <= dangerHealthRatioThreshold;
            bool wasVisible = isDangerEdgeVisible;

            SetDangerEdgeVisible(shouldShowDanger);
            if (shouldShowDanger && wasVisible && triggerFlashOnDamage)
            {
                PlayDangerEdgeFlash();
            }
        }

        /// <summary>
        /// summary: 统一控制 Danger Edge 的显示与基础透明度。
        /// param: visible 目标是否显示
        /// returns: 无
        /// </summary>
        private void SetDangerEdgeVisible(bool visible)
        {
            isDangerEdgeVisible = visible;
            if (dangerEdgeImage == null)
            {
                return;
            }

            dangerEdgeImage.enabled = visible;
            StopDangerEdgeFlash(resetToVisibleAlpha: false);
            ApplyDangerEdgeAlpha(visible ? dangerVisibleAlpha : 0f);
        }

        /// <summary>
        /// summary: 触发一次 Danger Edge 透明度闪烁动画。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void PlayDangerEdgeFlash()
        {
            if (!isDangerEdgeVisible || dangerEdgeImage == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            StopDangerEdgeFlash(resetToVisibleAlpha: true);
            dangerEdgeFlashCoroutine = StartCoroutine(PlayDangerEdgeFlashCoroutine());
        }

        /// <summary>
        /// summary: 执行一次低血边缘从常亮到暗、再回到常亮的闪烁过渡。
        /// param: 无
        /// returns: 闪烁协程
        /// </summary>
        private IEnumerator PlayDangerEdgeFlashCoroutine()
        {
            float halfDuration = Mathf.Max(0.01f, dangerFlashDuration * 0.5f);
            yield return LerpDangerEdgeAlpha(dangerVisibleAlpha, dangerFlashAlpha, halfDuration);
            yield return LerpDangerEdgeAlpha(dangerFlashAlpha, dangerVisibleAlpha, halfDuration);
            dangerEdgeFlashCoroutine = null;
        }

        /// <summary>
        /// summary: 在指定时长内线性插值 Danger Edge 的透明度。
        /// param: from 起始透明度
        /// param: to 目标透明度
        /// param: duration 插值时长
        /// returns: 插值协程
        /// </summary>
        private IEnumerator LerpDangerEdgeAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                ApplyDangerEdgeAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            ApplyDangerEdgeAlpha(to);
        }

        /// <summary>
        /// summary: 停止当前正在执行的 Danger Edge 闪烁协程。
        /// param: resetToVisibleAlpha 为 true 且当前可见时会恢复到常亮透明度
        /// returns: 无
        /// </summary>
        private void StopDangerEdgeFlash(bool resetToVisibleAlpha)
        {
            if (dangerEdgeFlashCoroutine != null)
            {
                StopCoroutine(dangerEdgeFlashCoroutine);
                dangerEdgeFlashCoroutine = null;
            }

            if (resetToVisibleAlpha && isDangerEdgeVisible)
            {
                ApplyDangerEdgeAlpha(dangerVisibleAlpha);
            }
        }

        /// <summary>
        /// summary: 把 Danger Edge 立即复位到默认隐藏状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ResetDangerEdgeDisplay()
        {
            StopDangerEdgeFlash(resetToVisibleAlpha: false);
            SetDangerEdgeVisible(false);
        }

        /// <summary>
        /// summary: 实际写入 Danger Edge Image 的透明度。
        /// param: alpha 目标透明度
        /// returns: 无
        /// </summary>
        private void ApplyDangerEdgeAlpha(float alpha)
        {
            if (dangerEdgeImage == null)
            {
                return;
            }

            Color color = dangerEdgeImage.color;
            color.a = Mathf.Clamp01(alpha);
            dangerEdgeImage.color = color;
        }

        /// <summary>
        /// summary: 统一规整 Danger Edge 的可调参数，避免阈值和时长非法。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SanitizeDangerEdgeConfiguration()
        {
            dangerHealthRatioThreshold = Mathf.Clamp01(dangerHealthRatioThreshold);
            dangerVisibleAlpha = Mathf.Clamp01(dangerVisibleAlpha);
            dangerFlashAlpha = Mathf.Clamp01(dangerFlashAlpha);
            dangerFlashDuration = Mathf.Max(0.01f, dangerFlashDuration);
            if (dangerFlashAlpha > dangerVisibleAlpha)
            {
                dangerFlashAlpha = dangerVisibleAlpha;
            }
        }

        private void SanitizeNotificationConfiguration()
        {
            notificationDisplaySeconds = Mathf.Max(0f, notificationDisplaySeconds);
            notificationFadeSeconds = Mathf.Max(0f, notificationFadeSeconds);
        }

        /// <summary>
        /// summary: 从玩家移动根节点解析其绑定的 PlayerHealth 组件。
        /// param: player 玩家移动组件
        /// returns: 找到的 PlayerHealth；未找到时返回 null
        /// </summary>
        private static PlayerHealth ResolvePlayerHealthFromPlayer(PlayerPlaneMovement player)
        {
            if (player == null)
            {
                return null;
            }

            return player.GetComponent<PlayerHealth>()
                ?? player.GetComponentInChildren<PlayerHealth>(true);
        }

        /// <summary>
        /// 兼容当前 MainUI prefab 中已有的暂停按钮节点命名。
        /// </summary>
        /// <param name="root">顶部面板根节点。</param>
        /// <returns>暂停按钮根节点；未找到时返回 null。</returns>
        private static RectTransform ResolvePauseButtonRoot(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            return root.Find("Pause Btn") as RectTransform
                ?? root.Find("Pause Button") as RectTransform;
        }

        /// <summary>
        /// summary: 解析 Spell Panel 下首个带 BackPackGridSlotView 的子节点，作为运行时模板来源。
        /// param: root Spell Panel 根节点
        /// returns: 可复用的模板槽位；未找到时返回 null
        /// </summary>
        private static BackPackGridSlotView ResolveSpellSlotTemplate(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                BackPackGridSlotView slotView = root.GetChild(i).GetComponent<BackPackGridSlotView>();
                if (slotView != null)
                {
                    return slotView;
                }
            }

            return null;
        }

        private void EnsureSpellOutlineLayer()
        {
            RectTransform parent = spellPanel != null ? spellPanel.parent as RectTransform : topPanel;
            if (parent == null)
            {
                spellLinkedOutlineLayer = null;
                return;
            }

            if (spellLinkedOutlineLayer == null)
            {
                GameObject layerObject = new("MainSpellLinkedOutlineLayer", typeof(RectTransform), typeof(CanvasGroup));
                layerObject.layer = gameObject.layer;
                spellLinkedOutlineLayer = layerObject.GetComponent<RectTransform>();
                spellLinkedOutlineLayer.SetParent(parent, false);
                spellLinkedOutlineLayer.anchorMin = Vector2.zero;
                spellLinkedOutlineLayer.anchorMax = Vector2.one;
                spellLinkedOutlineLayer.offsetMin = Vector2.zero;
                spellLinkedOutlineLayer.offsetMax = Vector2.zero;
                spellLinkedOutlineLayer.localScale = Vector3.one;

                CanvasGroup canvasGroup = layerObject.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            spellLinkedOutlineLayer.SetAsLastSibling();
        }

        private void RefreshSpellOutlines()
        {
            EnsureSpellOutlineLayer();
            if (spellPanel == null || spellLinkedOutlineLayer == null)
            {
                SetLinkedOutlineViewsVisible(runtimeSpellOutlines, 0);
                return;
            }

            spellLinkedOutlineLayer.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(spellPanel);

            int visibleCount = 0;
            for (int i = 0; i < runtimeSpellSlots.Count; i++)
            {
                BackPackGridSlotView slot = runtimeSpellSlots[i];
                if (slot == null || slot.Item == null || !slot.IsAnchor || slot.Item.SlotSpan <= 1)
                {
                    continue;
                }

                int endIndex = slot.AnchorIndex + slot.Item.SlotSpan - 1;
                if (endIndex < 0 || endIndex >= runtimeSpellSlots.Count)
                {
                    continue;
                }

                BackPackGridSlotView endSlot = runtimeSpellSlots[endIndex];
                if (endSlot == null || slot.SlotRectTransform == null || endSlot.SlotRectTransform == null)
                {
                    continue;
                }

                LinkedTokenOutlineView outlineView = GetOrCreateLinkedOutlineView(visibleCount);
                outlineView.ApplyStyle(linkedOutlineColor, linkedOutlineThickness);
                outlineView.FitToSlots(spellLinkedOutlineLayer, slot.SlotRectTransform, endSlot.SlotRectTransform, linkedOutlinePadding);
                outlineView.gameObject.SetActive(true);
                visibleCount++;
            }

            SetLinkedOutlineViewsVisible(runtimeSpellOutlines, visibleCount);
        }

        private LinkedTokenOutlineView GetOrCreateLinkedOutlineView(int index)
        {
            while (runtimeSpellOutlines.Count <= index)
            {
                LinkedTokenOutlineView outlineView = LinkedTokenOutlineView.CreateRuntime($"Main Spell Linked Outline {runtimeSpellOutlines.Count + 1:D2}", spellLinkedOutlineLayer, gameObject.layer);
                outlineView.gameObject.SetActive(false);
                runtimeSpellOutlines.Add(outlineView);
            }

            return runtimeSpellOutlines[index];
        }

        private static void SetLinkedOutlineViewsVisible(List<LinkedTokenOutlineView> outlineViews, int visibleCount)
        {
            if (outlineViews == null)
            {
                return;
            }

            for (int i = 0; i < outlineViews.Count; i++)
            {
                if (outlineViews[i] != null)
                {
                    outlineViews[i].gameObject.SetActive(i < visibleCount);
                }
            }
        }

        /// <summary>
        /// summary: 统一销毁一个运行时 spell 槽位实例，兼容 Play Mode 与 Edit Mode。
        /// param: child 需要销毁的子节点对象
        /// returns: 无
        /// </summary>
        private static void DestroyChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
