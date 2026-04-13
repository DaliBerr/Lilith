using System;
using System.Collections;
using System.Collections.Generic;
using Kernel.Bullet;
using Kernel.MapGrid;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vocalith.EventSystem;
using Vocalith.Logging;

namespace Kernel.Quest
{
    /// <summary>
    /// 负责任务目录加载、激活扫描、进度跟踪、奖励发放和 HUD 通知。
    /// </summary>
    [DefaultExecutionOrder(-920)]
    [DisallowMultipleComponent]
    public sealed class QuestService : MonoBehaviour
    {
        private const string DefaultCatalogAddress = "Assets/Data/Quest/QuestCatalog";
        private const float DefaultActivationScanIntervalSeconds = 0.5f;

        private readonly List<QuestDefinitionData> questDefinitions = new();
        private readonly Dictionary<string, QuestDefinitionData> questDefinitionById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ActiveQuestRuntimeState> activeQuestStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PlaceableTokenData> tokenByAddress = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AsyncOperationHandle<PlaceableTokenData>> tokenHandleByAddress = new(StringComparer.Ordinal);
        private readonly HashSet<string> completedQuestIds = new(StringComparer.Ordinal);
        private readonly List<QuestActiveSnapshot> activeQuestSnapshots = new();

        private AsyncOperationHandle<TextAsset> activeCatalogHandle;
        private bool hasActiveCatalogHandle;
        private Coroutine bootRuntimeCoroutine;
        private Coroutine activationScanCoroutine;
        private IDisposable enemyDiedSubscription;
        private IDisposable combatVictorySubscription;
        private IDisposable bossEndedSubscription;
        private PlayerBulletTokenInventory currentInventory;
        private int sessionEnemyKillCount;
        private int sessionCombatVictoryCount;
        private int sessionBossKillCount;
        private bool hasRuntimeStarted;
        private bool isRuntimeBooting;
        private bool isRuntimeReady;

        public static QuestService Instance { get; private set; }

        [SerializeField] private string catalogAddress = DefaultCatalogAddress;
        [SerializeField, Min(0.1f)] private float activationScanIntervalSeconds = DefaultActivationScanIntervalSeconds;

        public bool IsRuntimeReady => isRuntimeReady;
        public event Action ActiveQuestsChanged;
        public event Action<QuestCompletedSnapshot> QuestCompleted;

        /// <summary>
        /// summary: 在首场景加载前创建唯一的任务服务实例，但不立刻开始任务目录加载。
        /// param: 无
        /// returns: 无
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            GetOrCreateInstance();
        }

        /// <summary>
        /// summary: 清空静态单例引用，避免域重载后残留旧实例句柄。
        /// param: 无
        /// returns: 无
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
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
            activationScanIntervalSeconds = Mathf.Max(0.1f, activationScanIntervalSeconds);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            StopRuntimeCoroutines();
            UnsubscribeFromRuntimeSources();
            ReleaseCatalogHandle();
            ReleaseResolvedTokens();
        }

        /// <summary>
        /// summary: 获取当前可用的任务服务实例；若场景中缺失则自动创建。
        /// param: 无
        /// returns: 可用的 QuestService 实例
        /// </summary>
        public static QuestService GetOrCreateInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            QuestService existing = FindFirstObjectByType<QuestService>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject bootstrapObject = new(nameof(QuestService));
            return bootstrapObject.AddComponent<QuestService>();
        }

        /// <summary>
        /// summary: 在 Main 场景本地启动完成后开始任务系统运行时初始化。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void BeginRuntime()
        {
            if (hasRuntimeStarted || isRuntimeBooting || !isActiveAndEnabled)
            {
                return;
            }

            bootRuntimeCoroutine = StartCoroutine(BootRuntimeCo());
        }

        /// <summary>
        /// summary: 返回当前所有已激活任务的只读快照列表，按目录顺序排列。
        /// param: 无
        /// returns: 当前可见的已激活任务快照只读列表
        /// </summary>
        public IReadOnlyList<QuestActiveSnapshot> GetActiveQuestSnapshots()
        {
            return activeQuestSnapshots;
        }

        private IEnumerator BootRuntimeCo()
        {
            isRuntimeBooting = true;
            try
            {
                RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
                if (saveService == null || !saveService.HasSelectedProfileSlot)
                {
                    GameDebug.LogWarning("[QuestService] RuntimeSaveService is unavailable or no profile slot is selected. Quest runtime will stay disabled.");
                    yield break;
                }

                RuntimeSaveService.EnsureProfileLoaded();
                if (!TryResolveRuntimePrerequisites(out string errorMessage))
                {
                    GameDebug.LogError($"[QuestService] {errorMessage}");
                    yield break;
                }

                activeCatalogHandle = Addressables.LoadAssetAsync<TextAsset>(catalogAddress.Trim());
                hasActiveCatalogHandle = true;
                yield return activeCatalogHandle;

                if (activeCatalogHandle.Status != AsyncOperationStatus.Succeeded || activeCatalogHandle.Result == null)
                {
                    GameDebug.LogError($"[QuestService] Failed to load quest catalog at '{catalogAddress}'.");
                    yield break;
                }

                if (!QuestCatalogJsonUtility.TryDeserializeCatalogJson(activeCatalogHandle.Result.text, out QuestCatalogData catalog, out errorMessage))
                {
                    GameDebug.LogError($"[QuestService] {errorMessage}");
                    yield break;
                }

                Dictionary<string, PlaceableTokenData> resolvedTokens = new(StringComparer.Ordinal);
                IReadOnlyCollection<string> tokenAddresses = QuestCatalogJsonUtility.CollectTokenAddresses(catalog);
                foreach (string tokenAddress in tokenAddresses)
                {
                    AsyncOperationHandle<PlaceableTokenData> tokenHandle = Addressables.LoadAssetAsync<PlaceableTokenData>(tokenAddress);
                    yield return tokenHandle;
                    if (tokenHandle.Status != AsyncOperationStatus.Succeeded || tokenHandle.Result == null)
                    {
                        if (tokenHandle.IsValid())
                        {
                            Addressables.Release(tokenHandle);
                        }

                        GameDebug.LogError($"[QuestService] Failed to load quest token at '{tokenAddress}'.");
                        yield break;
                    }

                    resolvedTokens[tokenAddress] = tokenHandle.Result;
                    tokenHandleByAddress[tokenAddress] = tokenHandle;
                }

                if (!TryUseCatalog(catalog, resolvedTokens, out errorMessage))
                {
                    GameDebug.LogError($"[QuestService] {errorMessage}");
                    yield break;
                }

                SubscribeToRuntimeSources();
                StartActivationScan();
                hasRuntimeStarted = true;
            }
            finally
            {
                ReleaseCatalogHandle();
                bootRuntimeCoroutine = null;
                isRuntimeBooting = false;
            }
        }

        /// <summary>
        /// summary: 用一份已解析好的目录和 token 解析表切换当前任务服务状态。
        /// param name="catalog": 需要采用的任务目录
        /// param name="resolvedTokens": 目录里所有 token 地址对应的已加载资产
        /// param name="errorMessage": 采用失败时的错误信息
        /// returns: 成功切换到新目录时返回 true
        /// </summary>
        private bool TryUseCatalog(QuestCatalogData catalog, IReadOnlyDictionary<string, PlaceableTokenData> resolvedTokens, out string errorMessage)
        {
            errorMessage = null;
            if (catalog == null)
            {
                errorMessage = "Quest catalog is null.";
                return false;
            }

            questDefinitions.Clear();
            questDefinitionById.Clear();
            activeQuestStates.Clear();
            completedQuestIds.Clear();
            activeQuestSnapshots.Clear();
            tokenByAddress.Clear();
            sessionEnemyKillCount = 0;
            sessionCombatVictoryCount = 0;
            sessionBossKillCount = 0;

            if (resolvedTokens != null)
            {
                foreach (KeyValuePair<string, PlaceableTokenData> pair in resolvedTokens)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null)
                    {
                        tokenByAddress[pair.Key] = pair.Value;
                    }
                }
            }

            IReadOnlyCollection<string> referencedTokenAddresses = QuestCatalogJsonUtility.CollectTokenAddresses(catalog);
            foreach (string address in referencedTokenAddresses)
            {
                if (!tokenByAddress.ContainsKey(address))
                {
                    errorMessage = $"Quest catalog is missing a resolved token asset for address '{address}'.";
                    return false;
                }
            }

            if (catalog.Quests != null)
            {
                for (int index = 0; index < catalog.Quests.Count; index++)
                {
                    QuestDefinitionData questDefinition = catalog.Quests[index];
                    if (questDefinition == null)
                    {
                        continue;
                    }

                    questDefinitions.Add(questDefinition);
                    questDefinitionById[questDefinition.Id] = questDefinition;
                }
            }

            RestoreQuestStateFromSave();
            isRuntimeReady = true;
            ScanForNewlyAvailableQuests();
            EvaluateAllActiveQuestCompletions();
            RebuildActiveQuestSnapshots();
            ActiveQuestsChanged?.Invoke();
            return true;
        }

        private bool TryResolveRuntimePrerequisites(out string errorMessage)
        {
            errorMessage = null;
            if (!Application.isPlaying)
            {
                errorMessage = "Quest runtime only supports Play Mode.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(catalogAddress))
            {
                errorMessage = "Quest catalog address is empty.";
                return false;
            }

            activationScanIntervalSeconds = Mathf.Max(0.1f, activationScanIntervalSeconds);
            return true;
        }

        private void RestoreQuestStateFromSave()
        {
            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            PermanentProfileData profile = saveService != null ? saveService.GetProfileSnapshot() : null;
            if (profile == null)
            {
                return;
            }

            if (profile.CompletedQuestIds != null)
            {
                foreach (string completedQuestId in profile.CompletedQuestIds)
                {
                    if (!string.IsNullOrEmpty(completedQuestId))
                    {
                        completedQuestIds.Add(completedQuestId);
                    }
                }
            }

            if (profile.ActiveQuestProgressById == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ActiveQuestProgressSaveData> pair in profile.ActiveQuestProgressById)
            {
                if (string.IsNullOrEmpty(pair.Key) || completedQuestIds.Contains(pair.Key))
                {
                    continue;
                }

                if (!questDefinitionById.TryGetValue(pair.Key, out QuestDefinitionData definition))
                {
                    continue;
                }

                ActiveQuestProgressSaveData progress = pair.Value != null ? pair.Value.Clone() : ActiveQuestProgressSaveData.CreateDefault();
                activeQuestStates[pair.Key] = CreateRuntimeState(definition, progress);
            }
        }

        private void SubscribeToRuntimeSources()
        {
            if (enemyDiedSubscription == null)
            {
                enemyDiedSubscription = EventManager.eventBus.Subscribe<EnemyDiedEvent>(HandleEnemyDied);
            }

            if (combatVictorySubscription == null)
            {
                combatVictorySubscription = EventManager.eventBus.Subscribe<CombatVictoryEvent>(HandleCombatVictory);
            }

            if (bossEndedSubscription == null)
            {
                bossEndedSubscription = EventManager.eventBus.Subscribe<BossEncounterEndedEvent>(HandleBossEncounterEnded);
            }

            TryResolveInventoryBinding();
        }

        private void UnsubscribeFromRuntimeSources()
        {
            enemyDiedSubscription?.Dispose();
            enemyDiedSubscription = null;

            combatVictorySubscription?.Dispose();
            combatVictorySubscription = null;

            bossEndedSubscription?.Dispose();
            bossEndedSubscription = null;

            if (currentInventory != null)
            {
                currentInventory.Changed -= HandleInventoryChanged;
                currentInventory = null;
            }
        }

        private void StartActivationScan()
        {
            if (activationScanCoroutine != null)
            {
                StopCoroutine(activationScanCoroutine);
            }

            activationScanCoroutine = StartCoroutine(ActivationScanCo());
        }

        private IEnumerator ActivationScanCo()
        {
            WaitForSecondsRealtime waitInstruction = new(activationScanIntervalSeconds);
            while (true)
            {
                TryResolveInventoryBinding();
                ScanForNewlyAvailableQuests();
                EvaluateAllActiveQuestCompletions();
                yield return waitInstruction;
            }
        }

        private void StopRuntimeCoroutines()
        {
            if (bootRuntimeCoroutine != null)
            {
                StopCoroutine(bootRuntimeCoroutine);
                bootRuntimeCoroutine = null;
            }

            if (activationScanCoroutine != null)
            {
                StopCoroutine(activationScanCoroutine);
                activationScanCoroutine = null;
            }
        }

        private void TryResolveInventoryBinding()
        {
            PlayerBulletTokenInventory resolvedInventory = FindFirstObjectByType<PlayerBulletTokenInventory>();
            if (currentInventory == resolvedInventory)
            {
                return;
            }

            if (currentInventory != null)
            {
                currentInventory.Changed -= HandleInventoryChanged;
            }

            currentInventory = resolvedInventory;
            if (currentInventory != null)
            {
                currentInventory.EnsureInitialized();
                currentInventory.Changed += HandleInventoryChanged;
            }
        }

        private void HandleEnemyDied(EnemyDiedEvent evt)
        {
            sessionEnemyKillCount++;
            ApplyProgressDelta(QuestConditionKind.EnemyKillCountAtLeast, progress => progress.EnemyKillCount++);
            ScanForNewlyAvailableQuests();
        }

        private void HandleCombatVictory(CombatVictoryEvent evt)
        {
            sessionCombatVictoryCount++;
            ApplyProgressDelta(QuestConditionKind.CombatVictoryCountAtLeast, progress => progress.CombatVictoryCount++);
            ScanForNewlyAvailableQuests();
        }

        private void HandleBossEncounterEnded(BossEncounterEndedEvent evt)
        {
            if (!evt.endedByDeath)
            {
                return;
            }

            sessionBossKillCount++;
            ApplyProgressDelta(QuestConditionKind.BossKillCountAtLeast, progress => progress.BossKillCount++);
            ScanForNewlyAvailableQuests();
        }

        private void HandleInventoryChanged()
        {
            ScanForNewlyAvailableQuests();
            EvaluateAllActiveQuestCompletions();
        }

        private void ApplyProgressDelta(QuestConditionKind completionKind, Action<ActiveQuestProgressSaveData> apply)
        {
            if (!isRuntimeReady || apply == null || activeQuestStates.Count <= 0)
            {
                return;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            List<string> activeQuestIds = new(activeQuestStates.Keys);
            for (int index = 0; index < activeQuestIds.Count; index++)
            {
                string questId = activeQuestIds[index];
                if (!activeQuestStates.TryGetValue(questId, out ActiveQuestRuntimeState state) || !state.UsesCompletionCondition(completionKind))
                {
                    continue;
                }

                apply(state.Progress);
                state.Progress.Sanitize();
                saveService?.TrySetActiveQuestProgress(questId, state.Progress);
            }

            EvaluateAllActiveQuestCompletions();
        }

        /// <summary>
        /// summary: 扫描当前未完成且未激活的任务，把前置条件已满足的任务加入激活列表。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ScanForNewlyAvailableQuests()
        {
            if (!isRuntimeReady)
            {
                return;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null)
            {
                return;
            }

            bool changed = false;
            for (int index = 0; index < questDefinitions.Count; index++)
            {
                QuestDefinitionData definition = questDefinitions[index];
                if (definition == null || completedQuestIds.Contains(definition.Id) || activeQuestStates.ContainsKey(definition.Id))
                {
                    continue;
                }

                if (!AreConditionsSatisfied(definition.Prerequisites, null, useSessionCounters: true))
                {
                    continue;
                }

                ActiveQuestProgressSaveData progress = ActiveQuestProgressSaveData.CreateDefault();
                if (!saveService.TrySetActiveQuestProgress(definition.Id, progress))
                {
                    GameDebug.LogWarning($"[QuestService] Failed to persist activation for quest '{definition.Id}'.");
                    continue;
                }

                activeQuestStates[definition.Id] = CreateRuntimeState(definition, progress);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            RebuildActiveQuestSnapshots();
            EvaluateAllActiveQuestCompletions();
            ActiveQuestsChanged?.Invoke();
        }

        private void EvaluateAllActiveQuestCompletions()
        {
            if (!isRuntimeReady || activeQuestStates.Count <= 0)
            {
                return;
            }

            List<string> activeQuestIds = new(activeQuestStates.Keys);
            for (int index = 0; index < activeQuestIds.Count; index++)
            {
                TryCompleteQuest(activeQuestIds[index]);
            }
        }

        private void TryCompleteQuest(string questId)
        {
            if (!activeQuestStates.TryGetValue(questId, out ActiveQuestRuntimeState state))
            {
                return;
            }

            if (!AreConditionsSatisfied(state.Definition.Completion, state.Progress, useSessionCounters: false))
            {
                return;
            }

            if (!TryBuildQuestCompletionRewards(state.Definition, out QuestCompletionWriteRequest writeRequest, out List<PlaceableTokenData> inventoryRewards, out string errorMessage))
            {
                GameDebug.LogError($"[QuestService] Failed to resolve completion rewards for quest '{questId}': {errorMessage}");
                return;
            }

            if (!CanFitInventoryRewards(inventoryRewards))
            {
                return;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null || !saveService.TryCompleteQuest(questId, writeRequest, out errorMessage))
            {
                GameDebug.LogError($"[QuestService] Failed to persist quest completion for '{questId}': {errorMessage}");
                return;
            }

            PlayerBulletTokenInventory targetInventory = currentInventory;
            for (int index = 0; index < inventoryRewards.Count; index++)
            {
                PlaceableTokenData rewardItem = inventoryRewards[index];
                if (targetInventory == null || rewardItem == null)
                {
                    continue;
                }

                targetInventory.EnsureInitialized();
                if (!targetInventory.TryAddItem(rewardItem, out _))
                {
                    GameDebug.LogError($"[QuestService] Quest '{questId}' reward '{rewardItem.name}' failed after preflight. The quest is already marked as completed.");
                }
            }

            activeQuestStates.Remove(questId);
            completedQuestIds.Add(questId);
            RebuildActiveQuestSnapshots();
            QuestCompleted?.Invoke(new QuestCompletedSnapshot(state.Definition.Id, state.Definition.Text));
            ActiveQuestsChanged?.Invoke();
        }

        private bool TryBuildQuestCompletionRewards(
            QuestDefinitionData definition,
            out QuestCompletionWriteRequest writeRequest,
            out List<PlaceableTokenData> inventoryRewards,
            out string errorMessage)
        {
            writeRequest = new QuestCompletionWriteRequest();
            inventoryRewards = new List<PlaceableTokenData>();
            errorMessage = null;

            if (definition?.Rewards == null)
            {
                return true;
            }

            for (int index = 0; index < definition.Rewards.Count; index++)
            {
                QuestRewardData reward = definition.Rewards[index];
                if (reward == null)
                {
                    continue;
                }

                switch (reward.Kind)
                {
                    case QuestRewardKind.InventoryToken:
                        if (!tokenByAddress.TryGetValue(reward.TokenAddress, out PlaceableTokenData rewardToken) || rewardToken == null)
                        {
                            errorMessage = $"reward token '{reward.TokenAddress}' is not loaded.";
                            return false;
                        }

                        inventoryRewards.Add(rewardToken);
                        break;

                    case QuestRewardKind.Remnants:
                        writeRequest.RemnantAmount += reward.Amount;
                        break;

                    case QuestRewardKind.UnlockId:
                        writeRequest.UnlockIds.Add(reward.UnlockId);
                        break;

                    case QuestRewardKind.StoryFlagSet:
                        writeRequest.StoryFlagIds.Add(reward.StoryFlagId);
                        break;

                    case QuestRewardKind.LifetimeStatDelta:
                        writeRequest.LifetimeStatDeltas.Add(new QuestLifetimeStatDeltaData(reward.LifetimeStatKey, reward.Value));
                        break;
                }
            }

            writeRequest.Sanitize();
            return true;
        }

        private bool CanFitInventoryRewards(IReadOnlyList<PlaceableTokenData> inventoryRewards)
        {
            if (inventoryRewards == null || inventoryRewards.Count <= 0)
            {
                return true;
            }

            TryResolveInventoryBinding();
            if (currentInventory == null)
            {
                return false;
            }

            bool[] occupied = new bool[PlayerBulletTokenInventory.Capacity];
            for (int cellIndex = 0; cellIndex < occupied.Length; cellIndex++)
            {
                occupied[cellIndex] = currentInventory.GetCell(cellIndex).IsOccupied;
            }

            for (int rewardIndex = 0; rewardIndex < inventoryRewards.Count; rewardIndex++)
            {
                PlaceableTokenData rewardItem = inventoryRewards[rewardIndex];
                if (!TrySimulateRewardPlacement(occupied, rewardItem))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySimulateRewardPlacement(bool[] occupied, PlaceableTokenData rewardItem)
        {
            if (occupied == null || rewardItem == null)
            {
                return false;
            }

            int slotSpan = Mathf.Max(1, rewardItem.SlotSpan);
            for (int anchorIndex = 0; anchorIndex < occupied.Length; anchorIndex++)
            {
                int endIndex = anchorIndex + slotSpan - 1;
                if (endIndex >= occupied.Length)
                {
                    break;
                }

                if (anchorIndex / PlayerBulletTokenInventory.Columns != endIndex / PlayerBulletTokenInventory.Columns)
                {
                    continue;
                }

                bool canPlace = true;
                for (int offset = 0; offset < slotSpan; offset++)
                {
                    if (occupied[anchorIndex + offset])
                    {
                        canPlace = false;
                        break;
                    }
                }

                if (!canPlace)
                {
                    continue;
                }

                for (int offset = 0; offset < slotSpan; offset++)
                {
                    occupied[anchorIndex + offset] = true;
                }

                return true;
            }

            return false;
        }

        private bool AreConditionsSatisfied(IReadOnlyList<QuestConditionData> conditions, ActiveQuestProgressSaveData progress, bool useSessionCounters)
        {
            if (conditions == null || conditions.Count <= 0)
            {
                return true;
            }

            for (int index = 0; index < conditions.Count; index++)
            {
                if (!IsConditionSatisfied(conditions[index], progress, useSessionCounters))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsConditionSatisfied(QuestConditionData condition, ActiveQuestProgressSaveData progress, bool useSessionCounters)
        {
            if (condition == null)
            {
                return false;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            switch (condition.Kind)
            {
                case QuestConditionKind.StoryFlagSet:
                    return saveService != null && saveService.HasStoryFlag(condition.StoryFlagId);

                case QuestConditionKind.LifetimeStatAtLeast:
                    return saveService != null && saveService.GetLifetimeStat(condition.LifetimeStatKey) >= condition.Value;

                case QuestConditionKind.RemnantsAtLeast:
                    return ResolveCurrentRemnantCount(saveService) >= condition.Value;

                case QuestConditionKind.InventoryContainsToken:
                    return CountInventoryItemsByAddress(condition.TokenAddress) >= 1;

                case QuestConditionKind.InventoryTokenCountAtLeast:
                    return CountInventoryItemsByAddress(condition.TokenAddress) >= condition.Value;

                case QuestConditionKind.EnemyKillCountAtLeast:
                    return (useSessionCounters ? sessionEnemyKillCount : progress?.EnemyKillCount ?? 0) >= condition.Value;

                case QuestConditionKind.CombatVictoryCountAtLeast:
                    return (useSessionCounters ? sessionCombatVictoryCount : progress?.CombatVictoryCount ?? 0) >= condition.Value;

                case QuestConditionKind.BossKillCountAtLeast:
                    return (useSessionCounters ? sessionBossKillCount : progress?.BossKillCount ?? 0) >= condition.Value;

                default:
                    return false;
            }
        }

        private int CountInventoryItemsByAddress(string tokenAddress)
        {
            if (string.IsNullOrEmpty(tokenAddress) || !tokenByAddress.TryGetValue(tokenAddress, out PlaceableTokenData targetToken) || targetToken == null)
            {
                return 0;
            }

            TryResolveInventoryBinding();
            if (currentInventory == null)
            {
                return 0;
            }

            int count = 0;
            for (int cellIndex = 0; cellIndex < PlayerBulletTokenInventory.Capacity; cellIndex++)
            {
                TokenCellOccupancy cell = currentInventory.GetCell(cellIndex);
                if (cell.IsOccupied && cell.isAnchor && cell.item == targetToken)
                {
                    count++;
                }
            }

            return count;
        }

        private static int ResolveCurrentRemnantCount(RuntimeSaveService saveService)
        {
            PlayerRemnantWallet wallet = PlayerRemnantWallet.Instance;
            if (wallet != null)
            {
                return wallet.CurrentRemnants;
            }

            return saveService != null ? saveService.GetCurrentRemnantCount() : 0;
        }

        private void RebuildActiveQuestSnapshots()
        {
            activeQuestSnapshots.Clear();
            for (int index = 0; index < questDefinitions.Count; index++)
            {
                QuestDefinitionData definition = questDefinitions[index];
                if (definition != null && activeQuestStates.ContainsKey(definition.Id))
                {
                    activeQuestSnapshots.Add(new QuestActiveSnapshot(definition.Id, definition.Text));
                }
            }
        }

        private ActiveQuestRuntimeState CreateRuntimeState(QuestDefinitionData definition, ActiveQuestProgressSaveData progress)
        {
            return new ActiveQuestRuntimeState(
                definition,
                progress ?? ActiveQuestProgressSaveData.CreateDefault(),
                QuestCatalogJsonUtility.UsesCompletionCondition(definition, QuestConditionKind.EnemyKillCountAtLeast),
                QuestCatalogJsonUtility.UsesCompletionCondition(definition, QuestConditionKind.CombatVictoryCountAtLeast),
                QuestCatalogJsonUtility.UsesCompletionCondition(definition, QuestConditionKind.BossKillCountAtLeast));
        }

        private void ReleaseCatalogHandle()
        {
            if (hasActiveCatalogHandle)
            {
                if (activeCatalogHandle.IsValid())
                {
                    Addressables.Release(activeCatalogHandle);
                }

                activeCatalogHandle = default;
                hasActiveCatalogHandle = false;
            }
        }

        private void ReleaseResolvedTokens()
        {
            foreach (KeyValuePair<string, AsyncOperationHandle<PlaceableTokenData>> pair in tokenHandleByAddress)
            {
                if (pair.Value.IsValid())
                {
                    Addressables.Release(pair.Value);
                }
            }

            tokenHandleByAddress.Clear();
            tokenByAddress.Clear();
        }

        private sealed class ActiveQuestRuntimeState
        {
            public ActiveQuestRuntimeState(
                QuestDefinitionData definition,
                ActiveQuestProgressSaveData progress,
                bool tracksEnemyKills,
                bool tracksCombatVictories,
                bool tracksBossKills)
            {
                Definition = definition;
                Progress = progress != null ? progress.Clone() : ActiveQuestProgressSaveData.CreateDefault();
                TracksEnemyKills = tracksEnemyKills;
                TracksCombatVictories = tracksCombatVictories;
                TracksBossKills = tracksBossKills;
            }

            public QuestDefinitionData Definition { get; }
            public ActiveQuestProgressSaveData Progress { get; }
            public bool TracksEnemyKills { get; }
            public bool TracksCombatVictories { get; }
            public bool TracksBossKills { get; }

            public bool UsesCompletionCondition(QuestConditionKind kind)
            {
                return kind switch
                {
                    QuestConditionKind.EnemyKillCountAtLeast => TracksEnemyKills,
                    QuestConditionKind.CombatVictoryCountAtLeast => TracksCombatVictories,
                    QuestConditionKind.BossKillCountAtLeast => TracksBossKills,
                    _ => false
                };
            }
        }
    }
}
