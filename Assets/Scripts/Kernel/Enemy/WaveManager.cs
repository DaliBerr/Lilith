using System;
using System.Collections.Generic;
using Kernel.Bullet;
using UnityEngine;
using Vocalith.EventSystem;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 按波次配置驱动敌人生成器，并在清空当前波后自动推进下一波。
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveManager : MonoBehaviour
{
    private const float NoScheduledTime = -1f;

    public event Action SequenceCompleted;
    public event Action<int, WaveDefinition, CombatEntryTokenSelectionPlan> WaveRewardSelectionRequested;

    [SerializeField] private EnemyGenerator enemyGenerator;
    [SerializeField] private List<WaveDefinition> waves = new();
    [SerializeField] private WaveSequenceProgressionConfig nonBossWaveSequenceProgression;
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField, Min(0f)] private float interWaveDelay = 2f;

    private readonly List<Enemy> aliveEnemies = new();
    private readonly List<int> spawnedCountsPerEntry = new();
    private int currentWaveIndex = -1;
    private int completedWaveCount;
    private int spawnedCountInCurrentWave;
    private int pendingNextWaveIndex = -1;
    private float nextSpawnTime = NoScheduledTime;
    private float nextWaveStartTime = NoScheduledTime;
    private bool isSequenceRunning;
    private bool hasCompletedSequence;
    private bool isAwaitingWaveRewardSelection;
    private VocalithRandom randomSource;
    private Enemy activeBossEnemy;
    private string activeBossDisplayName = string.Empty;

    public bool IsSequenceRunning => isSequenceRunning;

    public bool HasCompletedSequence => hasCompletedSequence;

    public int CompletedWaveCount => completedWaveCount;

    public bool IsAwaitingWaveRewardSelection => isAwaitingWaveRewardSelection;

    private void Awake()
    {
        EnsureRandomSource();
        TryResolveEnemyGenerator();
        SanitizeConfiguration();
    }

    private void OnEnable()
    {
        if (autoStartOnEnable)
        {
            TryStartSequence();
        }
    }

    private void OnDisable()
    {
        ClearActiveBossTracking(publishEndedEvent: activeBossEnemy != null, endedByDeath: false);
    }

    /// <summary>
    /// summary: 每帧推进当前波次的刷怪与结算逻辑。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        Tick(Time.time);
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
    }

    /// <summary>
    /// summary: 重置当前波次状态并从第一波开始启动整套波次序列。
    /// param: 无
    /// returns: 成功开始一个有效波次序列时返回 true
    /// </summary>
    public bool TryStartSequence()
    {
        EnsureRandomSource();
        SanitizeConfiguration();
        if (!TryResolveEnemyGenerator())
        {
            return false;
        }

        ClearActiveBossTracking(publishEndedEvent: activeBossEnemy != null, endedByDeath: false);
        aliveEnemies.Clear();
        spawnedCountsPerEntry.Clear();
        currentWaveIndex = -1;
        completedWaveCount = 0;
        spawnedCountInCurrentWave = 0;
        pendingNextWaveIndex = -1;
        nextSpawnTime = NoScheduledTime;
        nextWaveStartTime = NoScheduledTime;
        hasCompletedSequence = false;
        isSequenceRunning = true;
        isAwaitingWaveRewardSelection = false;
        enemyGenerator.TrySetCompletedWaveCount(0);

        if (TryStartNextValidWave(0, Time.time))
        {
            return true;
        }

        CompleteSequence();
        return false;
    }

    /// <summary>
    /// summary: 推进当前波的刷怪节奏，并在满足条件时切换到下一波或结束整个序列。
    /// param: currentTime 当前逻辑时钟
    /// returns: 无
    /// </summary>
    private void Tick(float currentTime)
    {
        if (!isSequenceRunning || hasCompletedSequence)
        {
            return;
        }

        PruneDeadEnemies();
        if (isAwaitingWaveRewardSelection)
        {
            return;
        }

        WaveDefinition currentWave = GetCurrentWave();
        if (currentWave == null)
        {
            if (!TryStartNextValidWave(currentWaveIndex + 1, currentTime))
            {
                CompleteSequence();
            }

            return;
        }

        TrySpawnCurrentWaveEnemy(currentWave, currentTime);
        TryAdvanceWave(currentWave, currentTime);
    }

    /// <summary>
    /// summary: 在当前波还未刷满时，按配置间隔继续生成下一名敌人。
    /// param: currentWave 当前正在执行的波次资产
    /// param: currentTime 当前逻辑时钟
    /// returns: 无
    /// </summary>
    private void TrySpawnCurrentWaveEnemy(WaveDefinition currentWave, float currentTime)
    {
        if (currentWave == null || spawnedCountInCurrentWave >= currentWave.TotalSpawnCount || currentTime < nextSpawnTime)
        {
            return;
        }

        if (!TryResolveNextSpawnEntry(currentWave, out WaveEnemySpawnEntry spawnEntry, out int entryIndex))
        {
            return;
        }

        IReadOnlyList<EnemyBulletTokenDropEntry> runtimeTokenDrops = ResolveRuntimeTokenDropsForSpawn(currentWave, spawnEntry);
        EnemyWaveConfig runtimeConfig = enemyGenerator.ResolveRuntimeConfig(spawnEntry.enemyDefinition, runtimeTokenDrops);
        if (!enemyGenerator.TrySpawnEnemy(spawnEntry.enemyDefinition, runtimeConfig, out Enemy spawnedEnemy))
        {
            return;
        }

        aliveEnemies.Add(spawnedEnemy);
        spawnedCountInCurrentWave++;
        if (entryIndex >= 0 && entryIndex < spawnedCountsPerEntry.Count)
        {
            spawnedCountsPerEntry[entryIndex]++;
        }

        TryStartBossEncounter(spawnEntry, spawnedEnemy);
        nextSpawnTime = currentTime + currentWave.SpawnIntervalSeconds;
    }

    /// <summary>
    /// summary: 当当前波已经刷满且场上敌人被清空后，等待固定延迟并自动切到下一波。
    /// param: currentWave 当前正在执行的波次资产
    /// param: currentTime 当前逻辑时钟
    /// returns: 无
    /// </summary>
    private void TryAdvanceWave(WaveDefinition currentWave, float currentTime)
    {
        if (currentWave == null || spawnedCountInCurrentWave < currentWave.TotalSpawnCount || aliveEnemies.Count > 0)
        {
            nextWaveStartTime = NoScheduledTime;
            return;
        }

        if (nextWaveStartTime < 0f)
        {
            nextWaveStartTime = currentTime + interWaveDelay;
            return;
        }

        if (currentTime < nextWaveStartTime)
        {
            return;
        }

        completedWaveCount++;
        enemyGenerator.TrySetCompletedWaveCount(completedWaveCount);
        if (TryPauseForWaveRewardSelection(currentWave))
        {
            return;
        }

        pendingNextWaveIndex = -1;
        if (!TryStartNextValidWave(currentWaveIndex + 1, currentTime))
        {
            CompleteSequence();
        }
    }

    /// <summary>
    /// summary: 在奖励选择完成后恢复波次推进；若已无下一波则直接结算整套序列。
    /// param: 无
    /// returns: 当前确实存在待恢复的奖励停顿时返回 true
    /// </summary>
    public bool TryContinueAfterWaveRewardSelection()
    {
        if (!isSequenceRunning || hasCompletedSequence || !isAwaitingWaveRewardSelection)
        {
            return false;
        }

        int nextWaveIndex = pendingNextWaveIndex;
        pendingNextWaveIndex = -1;
        isAwaitingWaveRewardSelection = false;
        nextWaveStartTime = NoScheduledTime;
        if (!TryStartNextValidWave(nextWaveIndex, Time.time))
        {
            CompleteSequence();
        }

        return true;
    }

    /// <summary>
    /// summary: 从给定索引开始查找下一份有效波次，并初始化本波运行时状态。
    /// param: startIndex 开始查找的波次数组索引
    /// param: currentTime 当前逻辑时钟
    /// returns: 成功切到下一份有效波次时返回 true
    /// </summary>
    private bool TryStartNextValidWave(int startIndex, float currentTime)
    {
        for (int i = Mathf.Max(0, startIndex); i < waves.Count; i++)
        {
            if (waves[i] == null || waves[i].TotalSpawnCount <= 0)
            {
                continue;
            }

            currentWaveIndex = i;
            spawnedCountInCurrentWave = 0;
            nextSpawnTime = currentTime;
            nextWaveStartTime = NoScheduledTime;
            InitializeWaveSpawnTracking(waves[i]);
            return true;
        }

        return false;
    }

    /// <summary>
    /// summary: 清理已经死亡或已销毁的敌人引用，避免波次卡在已不存在的对象上。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void PruneDeadEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = aliveEnemies[i];
            if (enemy == null || enemy.IsDead)
            {
                aliveEnemies.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// summary: 获取当前正在执行的有效波次资产；索引越界或空引用时返回 null。
    /// param: 无
    /// returns: 当前波次资产
    /// </summary>
    private WaveDefinition GetCurrentWave()
    {
        if (currentWaveIndex < 0 || currentWaveIndex >= waves.Count)
        {
            return null;
        }

        return waves[currentWaveIndex];
    }

    /// <summary>
    /// summary: 当 Inspector 未显式绑定生成器时，自动解析场景中的 EnemyGenerator。
    /// param: 无
    /// returns: 成功拿到波次使用的生成器时返回 true
    /// </summary>
    private bool TryResolveEnemyGenerator()
    {
        if (enemyGenerator != null)
        {
            return true;
        }

        enemyGenerator = FindFirstObjectByType<EnemyGenerator>();
        return enemyGenerator != null;
    }

    /// <summary>
    /// summary: 结束整套波次序列，并清理后续计时器。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void CompleteSequence()
    {
        bool shouldNotifyCompletion = isSequenceRunning && !hasCompletedSequence;
        isSequenceRunning = false;
        hasCompletedSequence = true;
        ClearActiveBossTracking(publishEndedEvent: activeBossEnemy != null, endedByDeath: false);
        spawnedCountsPerEntry.Clear();
        pendingNextWaveIndex = -1;
        nextSpawnTime = NoScheduledTime;
        nextWaveStartTime = NoScheduledTime;
        isAwaitingWaveRewardSelection = false;
        if (shouldNotifyCompletion)
        {
            SequenceCompleted?.Invoke();
            EventManager.eventBus.Publish(new CombatVictoryEvent(this, completedWaveCount));
        }
    }

    /// <summary>
    /// summary: 修正波次管理器的基础配置，避免非法延迟值进入运行时。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        interWaveDelay = Mathf.Max(0f, interWaveDelay);
    }

    /// <summary>
    /// summary: 解析当前要刷出的敌人应使用的掉落表；普通波次按第 x 波配置，Boss 波次只用自身内部掉落。
    /// param: currentWave 当前正在执行的波次资产
    /// param: spawnEntry 当前将要刷出的敌人条目
    /// returns: 当前敌人运行时应使用的掉落表
    /// </summary>
    private IReadOnlyList<EnemyBulletTokenDropEntry> ResolveRuntimeTokenDropsForSpawn(WaveDefinition currentWave, WaveEnemySpawnEntry spawnEntry)
    {
        if (currentWave != null && currentWave.IsBossWave)
        {
            return currentWave.ResolveRuntimeTokenDrops(spawnEntry.tokenDrops);
        }

        int waveNumber = Mathf.Max(1, completedWaveCount + 1);
        IReadOnlyList<EnemyBulletTokenDropEntry> baseDrops = ResolveNonBossWaveTokenDrops(waveNumber);
        if (!HasAssignedTokenDrop(baseDrops) && currentWave != null)
        {
            baseDrops = currentWave.ResolveRuntimeTokenDrops();
        }

        return ResolveTokenDropsWithEntryOverlay(currentWave, baseDrops, spawnEntry.tokenDrops);
    }

    /// <summary>
    /// summary: 按第 x 波读取普通波次掉落；若该波未单独配置则回退到默认普通波次掉落。
    /// param: waveNumber 当前要刷新的序列波次编号（从 1 开始）
    /// returns: 当前普通波次应使用的基础掉落表
    /// </summary>
    private IReadOnlyList<EnemyBulletTokenDropEntry> ResolveNonBossWaveTokenDrops(int waveNumber)
    {
        int resolvedWaveNumber = Mathf.Max(1, waveNumber);
        if (nonBossWaveSequenceProgression == null)
        {
            return Array.Empty<EnemyBulletTokenDropEntry>();
        }

        return nonBossWaveSequenceProgression.ResolveNonBossTokenDrops(resolvedWaveNumber);
    }

    /// <summary>
    /// summary: 按第 x 波解析普通波次结束后的奖励抽取计划；Boss 波次仍使用自身波次定义的计划。
    /// param: completedWave 刚刚完成的波次定义
    /// returns: 本次波后应使用的奖励计划，若无需奖励选择则返回 null
    /// </summary>
    private CombatEntryTokenSelectionPlan ResolvePostWaveSelectionPlan(WaveDefinition completedWave)
    {
        if (completedWave == null)
        {
            return null;
        }

        if (completedWave.IsBossWave)
        {
            return completedWave.PostWaveTokenSelectionPlan;
        }

        int waveNumber = Mathf.Max(1, completedWaveCount);
        CombatEntryTokenSelectionPlan sequencePlan = nonBossWaveSequenceProgression != null
            ? nonBossWaveSequenceProgression.ResolveNonBossPostWaveSelectionPlan(waveNumber)
            : null;

        return sequencePlan != null ? sequencePlan : completedWave.PostWaveTokenSelectionPlan;
    }

    /// <summary>
    /// summary: 在启用条目级额外掉落时，把条目掉落叠加到基础掉落；同 token 时优先使用条目配置。
    /// param: currentWave 当前正在执行的波次资产
    /// param: baseDrops 当前波次基础掉落表
    /// param: entryDrops 当前敌人条目的额外掉落
    /// returns: 最终运行时掉落表
    /// </summary>
    private static IReadOnlyList<EnemyBulletTokenDropEntry> ResolveTokenDropsWithEntryOverlay(
        WaveDefinition currentWave,
        IReadOnlyList<EnemyBulletTokenDropEntry> baseDrops,
        IReadOnlyList<EnemyBulletTokenDropEntry> entryDrops)
    {
        List<EnemyBulletTokenDropEntry> resolvedBaseDrops = EnemyWaveConfig.SanitizeTokenDrops(baseDrops);
        if (currentWave == null || !currentWave.ApplyEntrySpecificTokenDrops || entryDrops == null || entryDrops.Count <= 0)
        {
            return resolvedBaseDrops;
        }

        List<EnemyBulletTokenDropEntry> mergedDrops = new(resolvedBaseDrops);
        IReadOnlyList<EnemyBulletTokenDropEntry> sanitizedEntryDrops = EnemyWaveConfig.SanitizeTokenDrops(entryDrops);
        for (int i = 0; i < sanitizedEntryDrops.Count; i++)
        {
            EnemyBulletTokenDropEntry entryDrop = sanitizedEntryDrops[i];
            if (entryDrop.token == null)
            {
                continue;
            }

            int existingIndex = FindTokenDropIndex(mergedDrops, entryDrop.token);
            if (existingIndex < 0)
            {
                mergedDrops.Add(entryDrop);
                continue;
            }

            mergedDrops[existingIndex] = entryDrop;
        }

        return mergedDrops;
    }

    /// <summary>
    /// summary: 判断掉落列表里是否至少存在一个有效 token。
    /// param: tokenDrops 待检查的掉落列表
    /// returns: 存在有效 token 时返回 true
    /// </summary>
    private static bool HasAssignedTokenDrop(IReadOnlyList<EnemyBulletTokenDropEntry> tokenDrops)
    {
        if (tokenDrops == null)
        {
            return false;
        }

        for (int i = 0; i < tokenDrops.Count; i++)
        {
            if (tokenDrops[i].token != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// summary: 在掉落列表里查找目标 token 的索引。
    /// param: tokenDrops 目标掉落列表
    /// param: token 需要查找的 token
    /// returns: 找到返回索引，找不到返回 -1
    /// </summary>
    private static int FindTokenDropIndex(IReadOnlyList<EnemyBulletTokenDropEntry> tokenDrops, PlaceableTokenData token)
    {
        if (tokenDrops == null || token == null)
        {
            return -1;
        }

        for (int i = 0; i < tokenDrops.Count; i++)
        {
            if (tokenDrops[i].token == token)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// summary: 根据当前波的配置模式，解析下一只要生成的敌人条目。
    /// param: currentWave 当前正在执行的波次资产
    /// param: spawnEntry 输出的敌人刷新条目
    /// param: entryIndex 输出的原始波次条目索引
    /// returns: 成功拿到下一只敌人的刷新配置时返回 true
    /// </summary>
    private bool TryResolveNextSpawnEntry(WaveDefinition currentWave, out WaveEnemySpawnEntry spawnEntry, out int entryIndex)
    {
        spawnEntry = default;
        entryIndex = -1;
        if (currentWave == null)
        {
            return false;
        }

        if (currentWave.RandomizeEnemySpawns)
        {
            return currentWave.TryGetRandomSpawnEntry(spawnedCountsPerEntry, randomSource, out spawnEntry, out entryIndex);
        }

        return currentWave.TryGetSpawnEntryAt(spawnedCountInCurrentWave, out spawnEntry, out entryIndex);
    }

    /// <summary>
    /// summary: 为即将开始的波次准备每个敌人条目的运行时计数状态。
    /// param: wave 当前即将进入的波次资产
    /// returns: 无
    /// </summary>
    private void InitializeWaveSpawnTracking(WaveDefinition wave)
    {
        spawnedCountsPerEntry.Clear();
        if (wave == null)
        {
            return;
        }

        for (int i = 0; i < wave.SpawnEntryCount; i++)
        {
            spawnedCountsPerEntry.Add(0);
        }
    }

    /// <summary>
    /// summary: 确保波次系统持有可用的自定义随机源，用于本波随机抽敌人。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureRandomSource()
    {
        randomSource ??= new VocalithRandom();
    }

    /// <summary>
    /// summary: 当当前波配置了波后奖励计划时，暂停自动推进并通知外部先完成奖励选择。
    /// param name="currentWave": 当前已经清空的波次配置
    /// returns: 当前波需要先等待奖励选择时返回 true
    /// </summary>
    private bool TryPauseForWaveRewardSelection(WaveDefinition currentWave)
    {
        CombatEntryTokenSelectionPlan selectionPlan = ResolvePostWaveSelectionPlan(currentWave);
        if (currentWave == null || selectionPlan == null)
        {
            return false;
        }

        pendingNextWaveIndex = currentWaveIndex + 1;
        isAwaitingWaveRewardSelection = true;
        nextWaveStartTime = NoScheduledTime;
        WaveRewardSelectionRequested?.Invoke(currentWaveIndex, currentWave, selectionPlan);
        return true;
    }

    /// <summary>
    /// summary: 当当前刷怪条目声明为 Boss 时，注册当前 Boss 生命周期并向事件总线广播开战事件。
    /// param: spawnEntry 当前刷出的波次敌人条目
    /// param: spawnedEnemy 当前实际生成出的敌人实例
    /// returns: 无
    /// </summary>
    private void TryStartBossEncounter(WaveEnemySpawnEntry spawnEntry, Enemy spawnedEnemy)
    {
        if (!spawnEntry.IsBossEncounter || spawnedEnemy == null)
        {
            return;
        }

        if (activeBossEnemy != null && activeBossEnemy != spawnedEnemy)
        {
            return;
        }

        activeBossEnemy = spawnedEnemy;
        activeBossDisplayName = spawnEntry.ResolveBossDisplayName();
        activeBossEnemy.Died -= HandleActiveBossDied;
        activeBossEnemy.Died += HandleActiveBossDied;
        TryConfigureBossPhaseController(spawnEntry, activeBossEnemy);

        EventManager.eventBus.Publish(new BossEncounterStartedEvent(
            activeBossEnemy,
            activeBossDisplayName,
            activeBossEnemy.CurrentHealth,
            activeBossEnemy.MaxHealth));
    }

    /// <summary>
    /// summary: 当当前 Boss 死亡时，结束本次 Boss 遭遇并广播死亡驱动的结束事件。
    /// param: boss 当前死亡的 Boss 实例
    /// returns: 无
    /// </summary>
    private void HandleActiveBossDied(Enemy boss)
    {
        if (boss == null || boss != activeBossEnemy)
        {
            return;
        }

        ClearActiveBossTracking(publishEndedEvent: true, endedByDeath: true);
    }

    /// <summary>
    /// summary: 清理当前 Boss 生命周期绑定，并按需向事件总线广播一次遭遇结束事件。
    /// param: publishEndedEvent 是否广播结束事件
    /// param: endedByDeath 本次结束是否由 Boss 死亡触发
    /// returns: 无
    /// </summary>
    private void ClearActiveBossTracking(bool publishEndedEvent, bool endedByDeath)
    {
        Enemy bossToClear = activeBossEnemy;
        string displayName = activeBossDisplayName;
        if (bossToClear != null)
        {
            bossToClear.Died -= HandleActiveBossDied;
        }

        activeBossEnemy = null;
        activeBossDisplayName = string.Empty;

        if (publishEndedEvent && bossToClear != null)
        {
            EventManager.eventBus.Publish(new BossEncounterEndedEvent(bossToClear, displayName, endedByDeath));
        }
    }

    /// <summary>
    /// summary: 当 Boss 条目声明了二阶段定义时，为当前 Boss 挂接并配置阶段切换控制器。
    /// param: spawnEntry 当前 Boss 的波次条目配置
    /// param: spawnedBoss 当前实际生成出的 Boss 实例
    /// returns: 无
    /// </summary>
    private static void TryConfigureBossPhaseController(WaveEnemySpawnEntry spawnEntry, Enemy spawnedBoss)
    {
        if (spawnedBoss == null || !spawnEntry.HasBossPhaseTransition)
        {
            return;
        }

        if (!spawnedBoss.TryGetComponent(out EnemyDefinitionBinder binder) || binder == null)
        {
            return;
        }

        BossPhaseController phaseController = spawnedBoss.GetComponent<BossPhaseController>();
        if (phaseController == null)
        {
            phaseController = spawnedBoss.gameObject.AddComponent<BossPhaseController>();
        }

        phaseController.TryConfigure(
            spawnedBoss,
            binder,
            spawnEntry.bossPhaseTwoDefinition,
            spawnEntry.ResolveBossPhaseTransitionHealthRatio());
    }
}
