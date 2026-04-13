using UnityEngine;
using System.Collections.Generic;
using Kernel.Bullet;
using VocalithRandom = Vocalith.Random;

/// <summary>
/// 描述单个波次的刷怪节奏，以及多个敌人的刷新条目。
/// </summary>
[CreateAssetMenu(menuName = "Lilith/Waves/Wave Definition", fileName = "WaveDefinition")]
public sealed class WaveDefinition : ScriptableObject
{
    private const float MinimumSpawnIntervalSeconds = 0.05f;

    [SerializeField, Min(0f)] private float spawnIntervalSeconds = 1f;
    [SerializeField] private bool randomizeEnemySpawns;
    [SerializeField] private CombatEntryTokenSelectionPlan postWaveTokenSelectionPlan;
    [SerializeField] private List<WaveEnemySpawnEntry> enemySpawns = new()
    {
        new WaveEnemySpawnEntry(null, 1)
    };

    public float SpawnIntervalSeconds => spawnIntervalSeconds;
    public bool RandomizeEnemySpawns => randomizeEnemySpawns;
    public CombatEntryTokenSelectionPlan PostWaveTokenSelectionPlan => postWaveTokenSelectionPlan;
    public int SpawnEntryCount => enemySpawns != null ? enemySpawns.Count : 0;
    public int TotalSpawnCount
    {
        get
        {
            if (enemySpawns == null)
            {
                return 0;
            }

            int totalCount = 0;
            for (int i = 0; i < enemySpawns.Count; i++)
            {
                WaveEnemySpawnEntry entry = enemySpawns[i];
                if (!IsEntryValid(entry))
                {
                    continue;
                }

                totalCount += Mathf.Max(0, entry.spawnCount);
            }

            return totalCount;
        }
    }

    private void OnValidate()
    {
        spawnIntervalSeconds = Mathf.Max(MinimumSpawnIntervalSeconds, spawnIntervalSeconds);
        if (enemySpawns == null)
        {
            enemySpawns = new List<WaveEnemySpawnEntry>();
        }

        for (int i = 0; i < enemySpawns.Count; i++)
        {
            enemySpawns[i] = enemySpawns[i].GetSanitized();
        }
    }

    /// <summary>
    /// summary: 按波次条目和数量展开后的顺序，返回某一只敌人对应的刷新配置。
    /// param: spawnIndex 当前波内从 0 开始的扁平化刷怪索引
    /// param: entry 输出的敌人刷新条目
    /// returns: 成功拿到有效刷新条目时返回 true
    /// </summary>
    public bool TryGetSpawnEntryAt(int spawnIndex, out WaveEnemySpawnEntry entry)
    {
        return TryGetSpawnEntryAt(spawnIndex, out entry, out _);
    }

    /// <summary>
    /// summary: 按波次条目和数量展开后的顺序，返回某一只敌人对应的刷新配置和条目索引。
    /// param: spawnIndex 当前波内从 0 开始的扁平化刷怪索引
    /// param: entry 输出的敌人刷新条目
    /// param: entryIndex 输出的原始波次条目索引
    /// returns: 成功拿到有效刷新条目时返回 true
    /// </summary>
    public bool TryGetSpawnEntryAt(int spawnIndex, out WaveEnemySpawnEntry entry, out int entryIndex)
    {
        entry = default;
        entryIndex = -1;
        if (spawnIndex < 0 || enemySpawns == null)
        {
            return false;
        }

        int traversedCount = 0;
        for (int i = 0; i < enemySpawns.Count; i++)
        {
            WaveEnemySpawnEntry currentEntry = enemySpawns[i];
            if (!IsEntryValid(currentEntry))
            {
                continue;
            }

            traversedCount += currentEntry.spawnCount;
            if (spawnIndex < traversedCount)
            {
                entry = currentEntry;
                entryIndex = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// summary: 在当前波允许随机刷新时，按各条目的剩余数量加权随机选出下一只敌人。
    /// param: spawnedCountsPerEntry 当前波每个条目已经成功刷出的数量
    /// param: random 当前波使用的随机源
    /// param: entry 输出的敌人刷新条目
    /// param: entryIndex 输出的原始波次条目索引
    /// returns: 成功选出仍有剩余配额的敌人条目时返回 true
    /// </summary>
    public bool TryGetRandomSpawnEntry(IReadOnlyList<int> spawnedCountsPerEntry, VocalithRandom random, out WaveEnemySpawnEntry entry, out int entryIndex)
    {
        entry = default;
        entryIndex = -1;
        if (enemySpawns == null || random == null)
        {
            return false;
        }

        int totalRemainingCount = 0;
        for (int i = 0; i < enemySpawns.Count; i++)
        {
            WaveEnemySpawnEntry currentEntry = enemySpawns[i];
            if (!IsEntryValid(currentEntry))
            {
                continue;
            }

            totalRemainingCount += GetRemainingSpawnCount(currentEntry, spawnedCountsPerEntry, i);
        }

        if (totalRemainingCount <= 0)
        {
            return false;
        }

        int rolledIndex = random.Next(totalRemainingCount);
        for (int i = 0; i < enemySpawns.Count; i++)
        {
            WaveEnemySpawnEntry currentEntry = enemySpawns[i];
            if (!IsEntryValid(currentEntry))
            {
                continue;
            }

            int remainingCount = GetRemainingSpawnCount(currentEntry, spawnedCountsPerEntry, i);
            if (remainingCount <= 0)
            {
                continue;
            }

            if (rolledIndex < remainingCount)
            {
                entry = currentEntry;
                entryIndex = i;
                return true;
            }

            rolledIndex -= remainingCount;
        }

        return false;
    }

    /// <summary>
    /// summary: 判断一个波次敌人条目是否具备有效的敌人定义和数量。
    /// param: entry 需要检查的波次敌人条目
    /// returns: 定义非空且数量大于零时返回 true
    /// </summary>
    private static bool IsEntryValid(WaveEnemySpawnEntry entry)
    {
        return entry.enemyDefinition != null && entry.spawnCount > 0;
    }

    /// <summary>
    /// summary: 计算某个波次条目还剩多少只敌人尚未刷出。
    /// param: entry 目标波次敌人条目
    /// param: spawnedCountsPerEntry 当前波每个条目已经刷出的数量
    /// param: entryIndex 目标条目在配置列表中的索引
    /// returns: 非负的剩余刷怪数量
    /// </summary>
    private static int GetRemainingSpawnCount(WaveEnemySpawnEntry entry, IReadOnlyList<int> spawnedCountsPerEntry, int entryIndex)
    {
        int spawnedCount = 0;
        if (spawnedCountsPerEntry != null && entryIndex >= 0 && entryIndex < spawnedCountsPerEntry.Count)
        {
            spawnedCount = Mathf.Max(0, spawnedCountsPerEntry[entryIndex]);
        }

        return Mathf.Max(0, entry.spawnCount - spawnedCount);
    }

}
