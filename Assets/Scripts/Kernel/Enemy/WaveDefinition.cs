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
    private const float MinimumHealth = 1f;

    [SerializeField, Min(0f)] private float spawnIntervalSeconds = 1f;
    [SerializeField] private bool randomizeEnemySpawns;
    [SerializeField] private List<WaveEnemySpawnEntry> enemySpawns = new()
    {
        new WaveEnemySpawnEntry(null, 1, new EnemyWaveConfig(MinimumHealth, 120f, 16f, 0.75f, 5f))
    };

    public float SpawnIntervalSeconds => spawnIntervalSeconds;
    public bool RandomizeEnemySpawns => randomizeEnemySpawns;
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
            WaveEnemySpawnEntry entry = enemySpawns[i].GetSanitized();
            entry.enemyConfig = SanitizeEnemyConfig(entry.enemyConfig);
            enemySpawns[i] = entry;
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

    /// <summary>
    /// summary: 修正波次敌人数值配置，避免非法数值进入运行时。
    /// param: config 当前条目的敌人数值配置
    /// returns: 修正后的合法配置
    /// </summary>
    private static EnemyWaveConfig SanitizeEnemyConfig(EnemyWaveConfig config)
    {
        config.maxHealth = SanitizePositiveValue(config.maxHealth, MinimumHealth);
        config.moveSpeed = SanitizeValue(config.moveSpeed, 0f);
        config.attackRange = SanitizeValue(config.attackRange, 0f);
        config.attackCooldown = SanitizeValue(config.attackCooldown, 0f);
        config.attackDamage = SanitizeValue(config.attackDamage, 0f);
        config.tokenDrops = SanitizeTokenDrops(config.tokenDrops);
        return config;
    }

    /// <summary>
    /// summary: 修正掉落表里的概率与数量取值，但保留空 token 占位项，避免 Inspector 新增元素后被 OnValidate 立即删掉。
    /// param: tokenDrops 当前条目上序列化出来的掉落表
    /// returns: 保留原有条目顺序的掉落表副本
    /// </summary>
    private static List<EnemyBulletTokenDropEntry> SanitizeTokenDrops(IReadOnlyList<EnemyBulletTokenDropEntry> tokenDrops)
    {
        List<EnemyBulletTokenDropEntry> sanitizedDrops = new();
        if (tokenDrops == null)
        {
            return sanitizedDrops;
        }

        for (int i = 0; i < tokenDrops.Count; i++)
        {
            EnemyBulletTokenDropEntry entry = tokenDrops[i];
            entry.dropChance = Mathf.Clamp01(entry.dropChance);
            entry.dropCount = Mathf.Max(1, entry.dropCount);
            sanitizedDrops.Add(entry);
        }

        return sanitizedDrops;
    }

    /// <summary>
    /// summary: 修正必须为正数的波次配置字段，避免生成零血敌人。
    /// param: value 当前配置值
    /// param: fallbackValue 当值非法时回退使用的默认值
    /// returns: 合法正值原样返回，否则回退默认值
    /// </summary>
    private static float SanitizePositiveValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return fallbackValue;
        }

        return value;
    }

    /// <summary>
    /// summary: 修正允许为零的波次配置字段，避免 NaN、Infinity 和负数进入运行时。
    /// param: value 当前配置值
    /// param: fallbackValue 当值非法时回退使用的默认值
    /// returns: 合法非负值原样返回，否则回退默认值
    /// </summary>
    private static float SanitizeValue(float value, float fallbackValue)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            return fallbackValue;
        }

        return value;
    }
}
