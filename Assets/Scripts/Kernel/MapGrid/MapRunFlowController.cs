using System;
using System.Collections;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.Quest;
using Kernel.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vocalith.EventSystem;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.MapGrid
{
    public enum RunFlowState
    {
        InStartRoom,
        EnteringCombat,
        InCombat,
        ShowingSettlement,
        ReturningToStartRoom,
    }

    /// <summary>
    /// 负责在同一场景中的起始房间与战斗地图之间切换，并驱动单局战斗的进入与结算返回。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRunFlowController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerPlaneMovement targetPlayer;
        [SerializeField] private MapGridAuthoring startRoomMapGrid;
        [SerializeField] private MapGridAuthoring combatMapGrid;
        [SerializeField] private ArenaSeedMapGenerator combatSeedGenerator;
        [SerializeField] private EnemyGenerator enemyGenerator;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private Transform runtimeEnemyContainer;
        [SerializeField] private Transform runtimePickupContainer;

        [Header("Coordinates")]
        [SerializeField] private Vector2Int startRoomSpawnCell = Vector2Int.zero;
        [SerializeField] private Vector2Int startRoomReturnCell = Vector2Int.zero;
        [SerializeField] private Vector2Int combatEntryCell = Vector2Int.zero;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float postRunReturnDelay = 0.5f;

        [Header("Token Selection")]
        [SerializeField] private CombatEntryTokenSelectionPlan initialCombatTokenSelectionPlan;

        [Header("Tutorial Teleporter Entry Reward")]
        [SerializeField] private PlaceableTokenData tutorialReturnTokenOverride;
        [SerializeField] private string tutorialReturnTokenAddress = TutorialQuestConstants.InitCoreTokenAddress;

        private readonly Dictionary<string, int> runHarvestCounts = new(StringComparer.Ordinal);
        private readonly List<string> runHarvestOrder = new();
        private readonly Dictionary<string, int> defeatedEnemyCounts = new(StringComparer.Ordinal);
        private readonly List<string> defeatedEnemyOrder = new();

        private IDisposable playerDiedSubscription;
        private IDisposable combatVictorySubscription;
        private IDisposable enemyDiedSubscription;
        private IDisposable bossEndedSubscription;
        private IDisposable rewardCollectedSubscription;
        private SettlementSnapshot currentSettlementSnapshot;
        private readonly CombatRunTimer combatRunTimer = new();
        private int completedWaveCount;
        private int defeatedBossCount;
        private bool hasPresentedSettlementThisRun;
        private Coroutine tokenSelectionRoutine;
        private Vocalith.Random tokenSelectionRandom;
        private WaveManager subscribedWaveManager;
        private RunFlowState currentState = RunFlowState.InStartRoom;
        private AsyncOperationHandle<PlaceableTokenData> tutorialReturnTokenHandle;
        private bool hasTutorialReturnTokenHandle;
        private PlaceableTokenData cachedTutorialReturnToken;

        public RunFlowState CurrentState => currentState;
        public bool IsCombatTimerRunning => combatRunTimer.IsRunning;
        public double CombatTimerElapsedSeconds => combatRunTimer.GetElapsedSeconds(Time.timeAsDouble);

        private void Awake()
        {
            SanitizeConfiguration();
            TryResolveReferences(out _);
        }

        private void OnEnable()
        {
            SubscribeToRunEvents();
            RefreshWaveManagerSubscription();
        }

        private void Start()
        {
            if (!TryInitializeStartRoom(out string error))
            {
                GameDebug.LogError($"[MapRunFlowController] {error}");
            }
        }

        private void OnDisable()
        {
            CancelPendingTokenSelection();
            RefreshWaveManagerSubscription(clearOnly: true);
            DisposeRunSubscriptions();
            ReleaseTutorialReturnTokenHandle();
        }

        private void OnValidate()
        {
            SanitizeConfiguration();
        }

        /// <summary>
        /// summary: 当玩家在起始房间触发传送装置时，开始准备新一局战斗并把玩家送入战斗区。
        /// param: 无
        /// returns: 成功开始进入战斗流程时返回 true
        /// </summary>
        public bool TryEnterCombatRun()
        {
            if (currentState != RunFlowState.InStartRoom || tokenSelectionRoutine != null)
            {
                return false;
            }

            if (!TryResolveReferences(out string error))
            {
                GameDebug.LogError($"[MapRunFlowController] {error}");
                return false;
            }

            if (TryStartCombatRunImmediately(out error))
            {
                return true;
            }

            GameDebug.LogError($"[MapRunFlowController] {error}");
            return false;
        }

        /// <summary>
        /// summary: 在关闭结算页后返回 StartRoom，并重置本局的运行时战斗状态。
        /// param name="error": 返回起始房间或重置运行时对象失败时输出的错误信息
        /// returns: 成功回到起始房间并完成重置时返回 true
        /// </summary>
        public bool TryReturnToStartRoomAndResetRun(out string error)
        {
            currentState = RunFlowState.ReturningToStartRoom;
            if (!TryReturnPlayerToStartRoom(out error))
            {
                return false;
            }

            RestorePlayerRuntimeState();
            ResetRunTracking();
            currentState = RunFlowState.InStartRoom;
            return true;
        }

        /// <summary>
        /// summary: 读取当前待展示的结算快照；仅在本局已经产出结算数据时返回 true。
        /// param name="snapshot": 输出当前待展示的结算快照
        /// returns: 当前存在待展示的结算快照时返回 true
        /// </summary>
        public bool TryGetSettlementSnapshot(out SettlementSnapshot snapshot)
        {
            snapshot = currentSettlementSnapshot;
            return snapshot != null;
        }

        /// <summary>
        /// summary: 读取当前战斗计时快照；迁移期仅供外部入口显式调用。
        /// param: 无
        /// returns: 当前战斗计时快照
        /// </summary>
        public CombatRunTimerSnapshot GetCombatTimerSnapshot()
        {
            return GetCombatTimerSnapshot(Time.timeAsDouble);
        }

        /// <summary>
        /// summary: 使用指定逻辑时间读取当前战斗计时快照，便于测试或自定义时钟调用。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 当前战斗计时快照
        /// </summary>
        public CombatRunTimerSnapshot GetCombatTimerSnapshot(double currentTimeSeconds)
        {
            return combatRunTimer.GetSnapshot(currentTimeSeconds);
        }

        /// <summary>
        /// summary: 显式开始战斗计时；已经运行时不会重置起点。
        /// param: 无
        /// returns: 本次成功进入计时运行态时返回 true
        /// </summary>
        public bool TryStartCombatTimer()
        {
            return TryStartCombatTimer(Time.timeAsDouble);
        }

        /// <summary>
        /// summary: 使用指定逻辑时间显式开始战斗计时；已经运行时不会重置起点。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 本次成功进入计时运行态时返回 true
        /// </summary>
        public bool TryStartCombatTimer(double currentTimeSeconds)
        {
            return combatRunTimer.TryStart(currentTimeSeconds);
        }

        /// <summary>
        /// summary: 显式重新开始战斗计时，并清除上一轮停止结果。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RestartCombatTimer()
        {
            RestartCombatTimer(Time.timeAsDouble);
        }

        /// <summary>
        /// summary: 使用指定逻辑时间显式重新开始战斗计时，并清除上一轮停止结果。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 无
        /// </summary>
        public void RestartCombatTimer(double currentTimeSeconds)
        {
            combatRunTimer.Restart(currentTimeSeconds);
        }

        /// <summary>
        /// summary: 显式停止战斗计时，并记录胜利、玩家死亡或取消等停止原因。
        /// param name="stopReason": 本次停止原因
        /// param name="snapshot": 停止后或当前已有的计时快照
        /// returns: 本次确实把计时器从运行态切到停止态时返回 true
        /// </summary>
        public bool TryStopCombatTimer(CombatRunTimerStopReason stopReason, out CombatRunTimerSnapshot snapshot)
        {
            return TryStopCombatTimer(stopReason, Time.timeAsDouble, out snapshot);
        }

        /// <summary>
        /// summary: 使用指定逻辑时间显式停止战斗计时，并记录胜利、玩家死亡或取消等停止原因。
        /// param name="stopReason": 本次停止原因
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// param name="snapshot": 停止后或当前已有的计时快照
        /// returns: 本次确实把计时器从运行态切到停止态时返回 true
        /// </summary>
        public bool TryStopCombatTimer(
            CombatRunTimerStopReason stopReason,
            double currentTimeSeconds,
            out CombatRunTimerSnapshot snapshot)
        {
            return combatRunTimer.TryStop(currentTimeSeconds, stopReason, out snapshot);
        }

        /// <summary>
        /// summary: 清空当前战斗计时状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void ResetCombatTimer()
        {
            combatRunTimer.Reset();
        }

        /// <summary>
        /// summary: 当玩家成功进入 teleporter 后，若教程链已经完成则往背包补发一个 InitCore。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void TryGrantTutorialEntryTokenAfterTeleporterTriggered()
        {
            if (!ShouldGrantTutorialEntryToken())
            {
                return;
            }

            if (!TryResolveTutorialEntryToken(out PlaceableTokenData tutorialEntryToken, out string resolveError))
            {
                GameDebug.LogWarning($"[MapRunFlowController] Failed to resolve tutorial entry token: {resolveError}");
                return;
            }

            if (!TryAddSelectedTokenToInventory(tutorialEntryToken, out string grantError))
            {
                GameDebug.LogWarning($"[MapRunFlowController] Failed to grant tutorial entry token after entering teleporter: {grantError}");
            }
        }

        /// <summary>
        /// summary: 在场景启动时把玩家绑定到起始房间地图，并吸附到起始房间出生格。
        /// param name="error": 初始化失败时返回的错误信息
        /// returns: 成功进入起始房间待机态时返回 true
        /// </summary>
        private bool TryInitializeStartRoom(out string error)
        {
            ResetRunTracking();
            currentState = RunFlowState.InStartRoom;
            return TryMovePlayerToMap(startRoomMapGrid, startRoomSpawnCell, out error);
        }

        /// <summary>
        /// summary: 清理旧战斗残留、生成新的战斗布局，并把玩家切换到战斗地图的初始位置。
        /// param name="error": 进入战斗流程失败时返回的错误信息
        /// returns: 成功完成进入战斗前的场景准备时返回 true
        /// </summary>
        private bool TryPrepareCombatArena(out string error)
        {
            error = null;
            if (!TryResolveReferences(out error))
            {
                return false;
            }

            ClearRuntimeChildren(runtimeEnemyContainer);
            ClearRuntimeChildren(runtimePickupContainer);

            if (!enemyGenerator.TrySetTarget(targetPlayer.transform) ||
                !enemyGenerator.TrySetTargetMapGrid(combatMapGrid))
            {
                error = "EnemyGenerator could not bind the player or combat map.";
                return false;
            }

            combatSeedGenerator.RandomizeSeed();
            if (!combatSeedGenerator.TryGenerateAndApplyLayout(combatEntryCell, includePlayerSnap: false, out error))
            {
                return false;
            }

            if (!TryMovePlayerToMap(combatMapGrid, combatEntryCell, out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 在战斗地图准备完毕后启动 WaveManager，并把当前流程状态切到 InCombat。
        /// param name="error": 启动波次失败时返回的错误信息
        /// returns: 成功进入战斗进行态时返回 true
        /// </summary>
        private bool TryStartCombatSequenceAndEnterState(out string error)
        {
            error = null;
            if (!waveManager.TryStartSequence())
            {
                error = "WaveManager failed to start a combat sequence.";
                return false;
            }

            currentState = RunFlowState.InCombat;
            return true;
        }

        /// <summary>
        /// summary: 在进入战斗后弹出初始 Token Select 面板；完成选择后才真正启动第一波。
        /// param name="uiManager": 当前可用的 UI 管理器实例
        /// param name="selectionLibrary": 初始抽取阶段要展示的 token 库
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator HandleInitialCombatSelectionCo(
            UIManager uiManager,
            BulletTokenLibrary selectionLibrary,
            SpellBookRewardLibrary spellBookSelectionLibrary)
        {
            RunRewardOption selectedReward = RunRewardOption.None;
            bool hasResolvedSelection = false;
            bool selectionAccepted = false;

            try
            {
                yield return TokenSelectUIUtility.ShowRewardSelectModal(
                    uiManager,
                    nameof(MapRunFlowController),
                    reward =>
                    {
                        selectedReward = reward;
                        selectionAccepted = reward.IsValid;
                        hasResolvedSelection = true;
                    },
                    () =>
                    {
                        selectionAccepted = false;
                        hasResolvedSelection = true;
                    });

                if (uiManager.GetTopModal() is not TokenSelectUIScreen tokenSelectScreen)
                {
                    GameDebug.LogError("[MapRunFlowController] Failed to resolve the initial combat token selection modal.");
                }
                else
                {
                    tokenSelectScreen.SetRewardLibraries(selectionLibrary, spellBookSelectionLibrary);

                    while (!hasResolvedSelection)
                    {
                        yield return null;
                    }

                    if (selectionAccepted && selectedReward.IsValid && !TryApplySelectedReward(selectedReward, out string rewardError))
                    {
                        GameDebug.LogError($"[MapRunFlowController] {rewardError}");
                    }
                }

                if (!TryStartCombatSequenceAndEnterState(out string error))
                {
                    GameDebug.LogError($"[MapRunFlowController] {error}");
                    AbortCombatEntry();
                }
            }
            finally
            {
                tokenSelectionRoutine = null;
            }
        }

        /// <summary>
        /// summary: 在波次奖励停顿期间弹出 Token Select 面板；玩家完成选择后恢复波次推进。
        /// param name="uiManager": 当前可用的 UI 管理器实例
        /// param name="selectionLibrary": 本次波后奖励要展示的 token 库
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator HandleWaveRewardSelectionCo(
            UIManager uiManager,
            BulletTokenLibrary selectionLibrary,
            SpellBookRewardLibrary spellBookSelectionLibrary)
        {
            RunRewardOption selectedReward = RunRewardOption.None;
            bool hasResolvedSelection = false;
            bool selectionAccepted = false;

            try
            {
                yield return TokenSelectUIUtility.ShowRewardSelectModal(
                    uiManager,
                    nameof(MapRunFlowController),
                    reward =>
                    {
                        selectedReward = reward;
                        selectionAccepted = reward.IsValid;
                        hasResolvedSelection = true;
                    },
                    () =>
                    {
                        selectionAccepted = false;
                        hasResolvedSelection = true;
                    });

                if (uiManager.GetTopModal() is not TokenSelectUIScreen tokenSelectScreen)
                {
                    GameDebug.LogError("[MapRunFlowController] Failed to resolve the wave reward token selection modal.");
                    yield break;
                }

                tokenSelectScreen.SetRewardLibraries(selectionLibrary, spellBookSelectionLibrary);

                while (!hasResolvedSelection)
                {
                    yield return null;
                }

                if (selectionAccepted && selectedReward.IsValid && !TryApplySelectedReward(selectedReward, out string error))
                {
                    GameDebug.LogError($"[MapRunFlowController] {error}");
                }
            }
            finally
            {
                tokenSelectionRoutine = null;
                if (waveManager != null && waveManager.IsAwaitingWaveRewardSelection)
                {
                    waveManager.TryContinueAfterWaveRewardSelection();
                }
            }
        }

        /// <summary>
        /// summary: 在本局结束后，把玩家和活动地图切回起始房间并清理本局残留。
        /// param name="error": 返回起始房间失败时返回的错误信息
        /// returns: 成功回到起始房间时返回 true
        /// </summary>
        private bool TryReturnPlayerToStartRoom(out string error)
        {
            ClearRuntimeChildren(runtimeEnemyContainer);
            ClearRuntimeChildren(runtimePickupContainer);
            bool success = TryMovePlayerToMap(startRoomMapGrid, startRoomReturnCell, out error);
            if (success)
            {
                currentState = RunFlowState.InStartRoom;
            }

            return success;
        }

        /// <summary>
        /// summary: 把玩家绑定到指定地图并吸附到最近 Ground 格。
        /// param name="targetMap": 当前要切换到的活动地图
        /// param name="requestedCoordinates": 希望进入的目标格坐标
        /// param name="error": 切图或传送失败时返回的错误信息
        /// returns: 成功完成地图切换与传送时返回 true
        /// </summary>
        private bool TryMovePlayerToMap(MapGridAuthoring targetMap, Vector2Int requestedCoordinates, out string error)
        {
            error = null;
            if (!TryResolveReferences(out error))
            {
                return false;
            }

            if (targetMap == null)
            {
                error = "Target map grid is missing.";
                return false;
            }

            if (!targetPlayer.TrySetTargetMapGrid(targetMap))
            {
                error = $"PlayerPlaneMovement could not bind map '{targetMap.name}'.";
                return false;
            }

            if (!MapSpawnUtility.TryTeleportToNearestGroundCell(targetMap, targetPlayer.transform, requestedCoordinates, out _, out error))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 订阅对局结算所需的事件源，包括玩家死亡、胜利、敌人死亡、Boss 结算和长期收益拾取。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void SubscribeToRunEvents()
        {
            if (playerDiedSubscription == null)
            {
                playerDiedSubscription = EventManager.eventBus.Subscribe<PlayerDiedEvent>(HandlePlayerDied);
            }

            if (combatVictorySubscription == null)
            {
                combatVictorySubscription = EventManager.eventBus.Subscribe<CombatVictoryEvent>(HandleCombatVictory);
            }

            if (enemyDiedSubscription == null)
            {
                enemyDiedSubscription = EventManager.eventBus.Subscribe<EnemyDiedEvent>(HandleEnemyDied);
            }

            if (bossEndedSubscription == null)
            {
                bossEndedSubscription = EventManager.eventBus.Subscribe<BossEncounterEndedEvent>(HandleBossEncounterEnded);
            }

            if (rewardCollectedSubscription == null)
            {
                rewardCollectedSubscription = EventManager.eventBus.Subscribe<RunRewardCollectedEvent>(HandleRunRewardCollected);
            }
        }

        private void DisposeRunSubscriptions()
        {
            playerDiedSubscription?.Dispose();
            playerDiedSubscription = null;

            combatVictorySubscription?.Dispose();
            combatVictorySubscription = null;

            enemyDiedSubscription?.Dispose();
            enemyDiedSubscription = null;

            bossEndedSubscription?.Dispose();
            bossEndedSubscription = null;

            rewardCollectedSubscription?.Dispose();
            rewardCollectedSubscription = null;
        }

        private void HandlePlayerDied(PlayerDiedEvent evt)
        {
            if (!isActiveAndEnabled || currentState != RunFlowState.InCombat || !IsTrackedPlayerHealth(evt.source))
            {
                return;
            }

            completedWaveCount = waveManager != null ? Mathf.Max(completedWaveCount, waveManager.CompletedWaveCount) : completedWaveCount;
            BeginSettlementPresentation(SettlementOutcome.Defeat);
        }

        private void HandleCombatVictory(CombatVictoryEvent evt)
        {
            if (!isActiveAndEnabled || currentState != RunFlowState.InCombat)
            {
                return;
            }

            completedWaveCount = Mathf.Max(completedWaveCount, evt.completedWaveCount);
            BeginSettlementPresentation(SettlementOutcome.Victory);
        }

        private void HandleEnemyDied(EnemyDiedEvent evt)
        {
            if (currentState != RunFlowState.InCombat)
            {
                return;
            }

            AccumulateCount(defeatedEnemyCounts, defeatedEnemyOrder, evt.displayName, 1);
        }

        private void HandleBossEncounterEnded(BossEncounterEndedEvent evt)
        {
            if (currentState != RunFlowState.InCombat || !evt.endedByDeath)
            {
                return;
            }

            defeatedBossCount++;
        }

        private void HandleRunRewardCollected(RunRewardCollectedEvent evt)
        {
            if (currentState != RunFlowState.InCombat)
            {
                return;
            }

            AccumulateCount(runHarvestCounts, runHarvestOrder, evt.displayName, evt.count);
        }

        private void BeginSettlementPresentation(SettlementOutcome outcome)
        {
            if (hasPresentedSettlementThisRun)
            {
                return;
            }

            hasPresentedSettlementThisRun = true;
            currentState = RunFlowState.ShowingSettlement;
            currentSettlementSnapshot = CreateSettlementSnapshot(outcome);
            if (!Application.isPlaying && UIManager.Instance == null)
            {
                return;
            }

            StartCoroutine(PresentSettlementScreenCo());
        }

        private SettlementSnapshot CreateSettlementSnapshot(SettlementOutcome outcome)
        {
            return new SettlementSnapshot(
                outcome,
                completedWaveCount,
                defeatedBossCount,
                BuildEntries(runHarvestCounts, runHarvestOrder),
                BuildEntries(defeatedEnemyCounts, defeatedEnemyOrder));
        }

        private IEnumerator PresentSettlementScreenCo()
        {
            UIManager uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                GameDebug.LogError("[MapRunFlowController] UIManager is missing. Settlement screen cannot be shown.");
                yield break;
            }

            yield return HideBossInfoOverlayCo(uiManager);

            while (uiManager.IsNavigating())
            {
                yield return null;
            }

            while (uiManager.GetTopModal() != null)
            {
                yield return uiManager.PopModalAndWait();
            }

            while (uiManager.GetTopScreen() != null && uiManager.GetTopScreen() is not MainUIScreen)
            {
                yield return uiManager.PopScreenAndWait();
            }

            if (uiManager.GetTopScreen() is SettlementUIScreen)
            {
                yield break;
            }

            yield return uiManager.PushScreenAndWait<SettlementUIScreen>();
        }

        /// <summary>
        /// summary: 结算展示前立即收起 BossInfo Overlay，避免它与结算窗同时可见。
        /// param name="uiManager": 当前可用的 UI 管理器实例
        /// returns: 用于等待 Overlay 收起的协程枚举器
        /// </summary>
        private static IEnumerator HideBossInfoOverlayCo(UIManager uiManager)
        {
            if (uiManager == null || !uiManager.TryGetOverlay<BossInfoUIScreen>(out BossInfoUIScreen bossInfo) || bossInfo == null)
            {
                yield break;
            }

            yield return bossInfo.Hide(0f);
        }

        private void RestorePlayerRuntimeState()
        {
            if (targetPlayer == null)
            {
                return;
            }

            PlayerHealth playerHealth = targetPlayer.GetComponent<PlayerHealth>() ?? targetPlayer.GetComponentInChildren<PlayerHealth>(true);
            playerHealth?.RestoreFullHealth();

            PlayerBulletTokenInventory inventory = targetPlayer.GetComponent<PlayerBulletTokenInventory>() ?? targetPlayer.GetComponentInChildren<PlayerBulletTokenInventory>(true);
            inventory?.ResetToStartingTokens();

            SpellBookLoadout spellBookLoadout = targetPlayer.GetComponent<SpellBookLoadout>() ?? targetPlayer.GetComponentInChildren<SpellBookLoadout>(true);
            spellBookLoadout?.ResetToStartingItems();
        }

        private void ResetRunTracking()
        {
            runHarvestCounts.Clear();
            runHarvestOrder.Clear();
            defeatedEnemyCounts.Clear();
            defeatedEnemyOrder.Clear();
            completedWaveCount = 0;
            defeatedBossCount = 0;
            hasPresentedSettlementThisRun = false;
            currentSettlementSnapshot = null;
        }

        /// <summary>
        /// summary: 当玩家进入 teleporter 且已经记录过 teleporter 触发标记时，往背包补发一个 InitCore。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static bool ShouldGrantTutorialEntryToken()
        {
            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            return saveService != null
                && saveService.HasSelectedProfileSlot
                && saveService.HasStoryFlag(TutorialQuestConstants.TeleporterTriggeredFlagId);
        }

        /// <summary>
        /// summary: 解析 teleporter 进入时要补发的固定教程 token；优先使用 Inspector override，其次回退到 Addressables 地址。
        /// param name="token": 输出解析到的 token 资产
        /// param name="error": 解析失败时输出的错误原因
        /// returns: 成功解析到有效 token 时返回 true
        /// </summary>
        private bool TryResolveTutorialEntryToken(out PlaceableTokenData token, out string error)
        {
            token = tutorialReturnTokenOverride;
            error = null;
            if (token != null)
            {
                return true;
            }

            if (cachedTutorialReturnToken != null)
            {
                token = cachedTutorialReturnToken;
                return true;
            }

            string trimmedAddress = tutorialReturnTokenAddress != null ? tutorialReturnTokenAddress.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmedAddress))
            {
                error = "Tutorial return token address is empty.";
                return false;
            }

            if (!hasTutorialReturnTokenHandle)
            {
                tutorialReturnTokenHandle = Addressables.LoadAssetAsync<PlaceableTokenData>(trimmedAddress);
                hasTutorialReturnTokenHandle = true;
            }

            tutorialReturnTokenHandle.WaitForCompletion();
            if (tutorialReturnTokenHandle.Status != AsyncOperationStatus.Succeeded || tutorialReturnTokenHandle.Result == null)
            {
                error = $"Addressables failed to load tutorial return token at '{trimmedAddress}'.";
                ReleaseTutorialReturnTokenHandle();
                return false;
            }

            cachedTutorialReturnToken = tutorialReturnTokenHandle.Result;
            token = cachedTutorialReturnToken;
            return true;
        }

        /// <summary>
        /// summary: 释放教程回房奖励 token 的 Addressables 句柄，避免场景卸载后泄漏。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ReleaseTutorialReturnTokenHandle()
        {
            cachedTutorialReturnToken = null;
            if (!hasTutorialReturnTokenHandle)
            {
                return;
            }

            if (tutorialReturnTokenHandle.IsValid())
            {
                Addressables.Release(tutorialReturnTokenHandle);
            }

            tutorialReturnTokenHandle = default;
            hasTutorialReturnTokenHandle = false;
        }

        /// <summary>
        /// summary: 根据本次波后奖励计划，抽取要展示的 token 库。
        /// param name="selectionPlan": 本次波后奖励使用的抽取计划
        /// param name="selectionLibrary": 输出本次波后奖励要使用的 token 库
        /// returns: 成功解析到有效 token 库时返回 true
        /// </summary>
        private bool TryResolveWaveRewardSelectionLibrary(CombatEntryTokenSelectionPlan selectionPlan, out BulletTokenLibrary selectionLibrary)
        {
            if (TryResolveWaveRewardSelectionLibraries(selectionPlan, out selectionLibrary, out SpellBookRewardLibrary spellBookSelectionLibrary))
            {
                return selectionLibrary != null && spellBookSelectionLibrary == null;
            }

            selectionLibrary = null;
            return false;
        }

        private bool TryResolveWaveRewardSelectionLibraries(
            CombatEntryTokenSelectionPlan selectionPlan,
            out BulletTokenLibrary selectionLibrary,
            out SpellBookRewardLibrary spellBookSelectionLibrary)
        {
            selectionLibrary = null;
            spellBookSelectionLibrary = null;
            if (selectionPlan == null)
            {
                return false;
            }

            tokenSelectionRandom ??= new Vocalith.Random(unchecked(GetInstanceID() ^ Environment.TickCount));
            return selectionPlan.TrySampleRewardLibrary(tokenSelectionRandom, out selectionLibrary, out spellBookSelectionLibrary);
        }

        /// <summary>
        /// summary: 根据控制器上单独配置的初始抽取计划，解析进入战斗后第一轮选择要展示的 token 库。
        /// param name="selectionLibrary": 输出进入战斗时要展示的 token 库
        /// returns: 成功解析到有效 token 库时返回 true
        /// </summary>
        private bool TryResolveInitialCombatSelectionLibrary(out BulletTokenLibrary selectionLibrary)
        {
            if (TryResolveInitialCombatSelectionLibraries(out selectionLibrary, out SpellBookRewardLibrary spellBookSelectionLibrary))
            {
                return selectionLibrary != null && spellBookSelectionLibrary == null;
            }

            selectionLibrary = null;
            return false;
        }

        private bool TryResolveInitialCombatSelectionLibraries(
            out BulletTokenLibrary selectionLibrary,
            out SpellBookRewardLibrary spellBookSelectionLibrary)
        {
            selectionLibrary = null;
            spellBookSelectionLibrary = null;
            if (initialCombatTokenSelectionPlan == null)
            {
                return false;
            }

            tokenSelectionRandom ??= new Vocalith.Random(unchecked(GetInstanceID() ^ Environment.TickCount));
            return initialCombatTokenSelectionPlan.TrySampleRewardLibrary(tokenSelectionRandom, out selectionLibrary, out spellBookSelectionLibrary);
        }

        /// <summary>
        /// summary: 维持旧版同步进入战斗链路；无须选择 token 时直接复用这条路径。
        /// param name="error": 进入战斗流程失败时返回的错误信息
        /// returns: 成功开始进入战斗流程时返回 true
        /// </summary>
        private bool TryStartCombatRunImmediately(out string error)
        {
            ResetRunTracking();
            currentState = RunFlowState.EnteringCombat;
            if (!TryPrepareCombatArena(out error))
            {
                AbortCombatEntry();
                return false;
            }

            if (TryResolveInitialCombatSelectionLibraries(
                out BulletTokenLibrary initialSelectionLibrary,
                out SpellBookRewardLibrary initialSpellBookSelectionLibrary))
            {
                UIManager uiManager = UIManager.Instance;
                if (uiManager != null)
                {
                    tokenSelectionRoutine = StartCoroutine(HandleInitialCombatSelectionCo(
                        uiManager,
                        initialSelectionLibrary,
                        initialSpellBookSelectionLibrary));
                    error = null;
                    return true;
                }

                GameDebug.LogError("[MapRunFlowController] UIManager is missing. Skipping the initial combat token selection modal.");
            }

            if (TryStartCombatSequenceAndEnterState(out error))
            {
                return true;
            }

            AbortCombatEntry();
            return false;
        }

        private bool TryApplySelectedReward(RunRewardOption selectedReward, out string error)
        {
            switch (selectedReward.Kind)
            {
                case RunRewardOptionKind.Token:
                    return TryAddSelectedTokenToInventory(selectedReward.Token, out error);
                case RunRewardOptionKind.SpellBook:
                    return TryEquipSelectedSpellBook(selectedReward.SpellBook, out error);
                default:
                    error = "Selected reward is missing.";
                    return false;
            }
        }

        /// <summary>
        /// summary: 把玩家在初始或波后奖励选择中选中的 token 写入背包首个合法空位。
        /// param name="selectedToken": 当前选中的 token
        /// param name="error": 写入失败时返回的错误信息
        /// returns: 成功写入背包时返回 true
        /// </summary>
        private bool TryAddSelectedTokenToInventory(PlaceableTokenData selectedToken, out string error)
        {
            error = null;
            if (!TryResolveReferences(out error))
            {
                return false;
            }

            if (selectedToken == null)
            {
                error = "Selected token is missing.";
                return false;
            }

            PlayerBulletTokenInventory inventory = targetPlayer.GetComponent<PlayerBulletTokenInventory>() ?? targetPlayer.GetComponentInChildren<PlayerBulletTokenInventory>(true);
            if (inventory == null)
            {
                error = "PlayerBulletTokenInventory is missing.";
                return false;
            }

            inventory.EnsureInitialized();
            if (!inventory.TryAddItem(selectedToken, out _))
            {
                error = $"Player inventory has no valid space for token '{selectedToken.name}'.";
                return false;
            }

            EventManager.eventBus.Publish(RewardNotificationEvent.FromToken(selectedToken));
            return true;
        }

        private bool TryEquipSelectedSpellBook(SpellBookData selectedSpellBook, out string error)
        {
            error = null;
            if (!TryResolveReferences(out error))
            {
                return false;
            }

            if (selectedSpellBook == null)
            {
                error = "Selected spell book is missing.";
                return false;
            }

            SpellBookLoadout spellBookLoadout = targetPlayer.GetComponent<SpellBookLoadout>() ?? targetPlayer.GetComponentInChildren<SpellBookLoadout>(true);
            if (spellBookLoadout == null)
            {
                error = "SpellBookLoadout is missing.";
                return false;
            }

            spellBookLoadout.SetSpellBook(selectedSpellBook);
            EventManager.eventBus.Publish(RewardNotificationEvent.FromSpellBook(selectedSpellBook));
            return true;
        }

        private void HandleWaveRewardSelectionRequested(int waveIndex, WaveDefinition completedWave, CombatEntryTokenSelectionPlan selectionPlan)
        {
            if (!isActiveAndEnabled || currentState != RunFlowState.InCombat)
            {
                waveManager?.TryContinueAfterWaveRewardSelection();
                return;
            }

            if (tokenSelectionRoutine != null)
            {
                GameDebug.LogWarning($"[MapRunFlowController] Ignoring duplicate wave reward selection request for wave index {waveIndex}.");
                return;
            }

            if (!TryResolveWaveRewardSelectionLibraries(
                selectionPlan,
                out BulletTokenLibrary selectionLibrary,
                out SpellBookRewardLibrary spellBookSelectionLibrary))
            {
                waveManager?.TryContinueAfterWaveRewardSelection();
                return;
            }

            UIManager uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                GameDebug.LogError("[MapRunFlowController] UIManager is missing. Cannot show the wave reward token selection modal.");
                waveManager?.TryContinueAfterWaveRewardSelection();
                return;
            }

            tokenSelectionRoutine = StartCoroutine(HandleWaveRewardSelectionCo(uiManager, selectionLibrary, spellBookSelectionLibrary));
        }

        private bool IsTrackedPlayerHealth(PlayerHealth playerHealth)
        {
            if (playerHealth == null || targetPlayer == null)
            {
                return false;
            }

            return playerHealth.transform.root == targetPlayer.transform.root;
        }

        private static IReadOnlyList<SettlementCountEntry> BuildEntries(
            IReadOnlyDictionary<string, int> counts,
            IReadOnlyList<string> order)
        {
            if (counts == null || order == null || order.Count <= 0)
            {
                return Array.Empty<SettlementCountEntry>();
            }

            List<SettlementCountEntry> entries = new(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                string key = order[i];
                if (!counts.TryGetValue(key, out int count) || count <= 0)
                {
                    continue;
                }

                entries.Add(new SettlementCountEntry(key, count));
            }

            return entries;
        }

        private static void AccumulateCount(
            IDictionary<string, int> counts,
            ICollection<string> order,
            string displayName,
            int delta)
        {
            if (counts == null || order == null || string.IsNullOrWhiteSpace(displayName) || delta <= 0)
            {
                return;
            }

            string key = displayName.Trim();
            if (!counts.TryGetValue(key, out int currentCount))
            {
                counts[key] = delta;
                order.Add(key);
                return;
            }

            counts[key] = currentCount + delta;
        }

        private bool TryResolveReferences(out string error)
        {
            error = null;
            targetPlayer ??= FindFirstObjectByType<PlayerPlaneMovement>();
            startRoomMapGrid ??= GameObject.Find("StartRoomMapRoot")?.GetComponent<MapGridAuthoring>();
            combatMapGrid ??= GameObject.Find("CombatMapRoot")?.GetComponent<MapGridAuthoring>();
            combatSeedGenerator ??= combatMapGrid != null ? combatMapGrid.GetComponent<ArenaSeedMapGenerator>() : null;
            enemyGenerator ??= FindFirstObjectByType<EnemyGenerator>();
            waveManager ??= FindFirstObjectByType<WaveManager>();
            runtimeEnemyContainer ??= GameObject.Find("Enemy")?.transform.Find("RuntimeEnemies");
            runtimePickupContainer ??= GameObject.Find("BulletTokenPickup_Authoring")?.transform.Find("RuntimePickups");

            if (targetPlayer == null)
            {
                error = "PlayerPlaneMovement is missing.";
                return false;
            }

            if (startRoomMapGrid == null)
            {
                error = "StartRoomMapRoot is missing a MapGridAuthoring component.";
                return false;
            }

            if (combatMapGrid == null)
            {
                error = "CombatMapRoot is missing a MapGridAuthoring component.";
                return false;
            }

            if (combatSeedGenerator == null)
            {
                error = "CombatMapRoot is missing ArenaSeedMapGenerator.";
                return false;
            }

            if (enemyGenerator == null)
            {
                error = "EnemyGenerator is missing.";
                return false;
            }

            if (waveManager == null)
            {
                error = "WaveManager is missing.";
                return false;
            }

            if (runtimeEnemyContainer == null)
            {
                error = "RuntimeEnemies container is missing under Enemy.";
                return false;
            }

            if (runtimePickupContainer == null)
            {
                error = "RuntimePickups container is missing under BulletTokenPickup_Authoring.";
                return false;
            }

            return true;
        }

        private void RefreshWaveManagerSubscription(bool clearOnly = false)
        {
            WaveManager nextWaveManager = clearOnly ? null : waveManager;
            if (!clearOnly && nextWaveManager == null)
            {
                nextWaveManager = FindFirstObjectByType<WaveManager>();
                waveManager = nextWaveManager;
            }

            if (subscribedWaveManager == nextWaveManager)
            {
                return;
            }

            if (subscribedWaveManager != null)
            {
                subscribedWaveManager.WaveRewardSelectionRequested -= HandleWaveRewardSelectionRequested;
            }

            subscribedWaveManager = nextWaveManager;
            if (subscribedWaveManager != null)
            {
                subscribedWaveManager.WaveRewardSelectionRequested += HandleWaveRewardSelectionRequested;
            }
        }

        private void AbortCombatEntry()
        {
            TryReturnPlayerToStartRoom(out _);
            ResetRunTracking();
            currentState = RunFlowState.InStartRoom;
        }

        private static void ClearRuntimeChildren(Transform container)
        {
            if (container == null)
            {
                return;
            }

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }
            }
        }

        private void CancelPendingTokenSelection()
        {
            if (tokenSelectionRoutine == null)
            {
                return;
            }

            StopCoroutine(tokenSelectionRoutine);
            tokenSelectionRoutine = null;
            if (waveManager != null && waveManager.IsAwaitingWaveRewardSelection)
            {
                waveManager.TryContinueAfterWaveRewardSelection();
            }
        }

        private void SanitizeConfiguration()
        {
            postRunReturnDelay = Mathf.Max(0f, postRunReturnDelay);
            tutorialReturnTokenAddress = string.IsNullOrWhiteSpace(tutorialReturnTokenAddress)
                ? TutorialQuestConstants.InitCoreTokenAddress
                : tutorialReturnTokenAddress.Trim();
        }
    }
}
