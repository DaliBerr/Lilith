using System;
using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 按波次配置驱动敌人生成器，并在清空当前波后自动推进下一波。
/// </summary>
[DisallowMultipleComponent]
public sealed class WaveManager : MonoBehaviour
{
    private const float NoScheduledTime = -1f;

    public event Action SequenceCompleted;

    [SerializeField] private EnemyGenerator enemyGenerator;
    [SerializeField] private List<WaveDefinition> waves = new();
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField, Min(0f)] private float interWaveDelay = 2f;

    private readonly List<Enemy> aliveEnemies = new();
    private readonly List<int> spawnedCountsPerEntry = new();
    private int currentWaveIndex = -1;
    private int spawnedCountInCurrentWave;
    private float nextSpawnTime = NoScheduledTime;
    private float nextWaveStartTime = NoScheduledTime;
    private bool isSequenceRunning;
    private bool hasCompletedSequence;
    private VocalithRandom randomSource;

    public bool IsSequenceRunning => isSequenceRunning;

    public bool HasCompletedSequence => hasCompletedSequence;

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

        aliveEnemies.Clear();
        spawnedCountsPerEntry.Clear();
        currentWaveIndex = -1;
        spawnedCountInCurrentWave = 0;
        nextSpawnTime = NoScheduledTime;
        nextWaveStartTime = NoScheduledTime;
        hasCompletedSequence = false;
        isSequenceRunning = true;

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

        if (!enemyGenerator.TrySpawnEnemy(spawnEntry.enemyDefinition, spawnEntry.enemyConfig, out Enemy spawnedEnemy))
        {
            return;
        }

        aliveEnemies.Add(spawnedEnemy);
        spawnedCountInCurrentWave++;
        if (entryIndex >= 0 && entryIndex < spawnedCountsPerEntry.Count)
        {
            spawnedCountsPerEntry[entryIndex]++;
        }

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

        if (!TryStartNextValidWave(currentWaveIndex + 1, currentTime))
        {
            CompleteSequence();
        }
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
        spawnedCountsPerEntry.Clear();
        nextSpawnTime = NoScheduledTime;
        nextWaveStartTime = NoScheduledTime;
        if (shouldNotifyCompletion)
        {
            SequenceCompleted?.Invoke();
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
}
