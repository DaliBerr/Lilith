using System;
using System.Collections.Generic;
using Kernel.Bullet;
using UnityEngine;

/// <summary>
/// 按序列波次统一配置普通波次掉落与波后奖励抽取计划。
/// </summary>
[CreateAssetMenu(menuName = "Lilith/Waves/Non-Boss Sequence Progression", fileName = "NonBossWaveSequenceProgression")]
public sealed class WaveSequenceProgressionConfig : ScriptableObject
{
    [Serializable]
    public struct WaveRewardEntry
    {
        [Min(1)] public int waveNumber;
        public List<EnemyBulletTokenDropEntry> tokenDrops;
        public CombatEntryTokenSelectionPlan postWaveTokenSelectionPlan;

        /// <summary>
        /// summary: 规范化单个波次奖励条目的波次编号与掉落集合。
        /// param: 无
        /// returns: 规范化后的波次奖励条目
        /// </summary>
        public WaveRewardEntry GetSanitized()
        {
            WaveRewardEntry sanitized = this;
            sanitized.waveNumber = Mathf.Max(1, sanitized.waveNumber);
            sanitized.tokenDrops = EnemyWaveConfig.SanitizeTokenDrops(sanitized.tokenDrops);
            return sanitized;
        }
    }

    [SerializeField] private List<EnemyBulletTokenDropEntry> defaultNonBossTokenDrops = new();
    [SerializeField] private List<WaveRewardEntry> rewardsByWave = new();

    private void OnValidate()
    {
        Sanitize();
    }

    /// <summary>
    /// summary: 按第 x 波解析普通波次敌人的掉落配置；若未命中则返回默认普通掉落。
    /// param: waveNumber 当前完成或即将生成的序列波次号（从 1 开始）
    /// returns: 本波普通敌人应使用的掉落配置列表
    /// </summary>
    public IReadOnlyList<EnemyBulletTokenDropEntry> ResolveNonBossTokenDrops(int waveNumber)
    {
        Sanitize();
        int resolvedWaveNumber = Mathf.Max(1, waveNumber);
        for (int i = 0; i < rewardsByWave.Count; i++)
        {
            WaveRewardEntry configuredEntry = rewardsByWave[i];
            if (configuredEntry.waveNumber != resolvedWaveNumber || !HasAssignedTokenDrop(configuredEntry.tokenDrops))
            {
                continue;
            }

            return configuredEntry.tokenDrops;
        }

        return defaultNonBossTokenDrops;
    }

    /// <summary>
    /// summary: 按第 x 波解析普通波次结束后的奖励抽取计划。
    /// param: waveNumber 当前完成的序列波次号（从 1 开始）
    /// returns: 命中配置时返回对应计划，否则返回 null
    /// </summary>
    public CombatEntryTokenSelectionPlan ResolveNonBossPostWaveSelectionPlan(int waveNumber)
    {
        Sanitize();
        int resolvedWaveNumber = Mathf.Max(1, waveNumber);
        for (int i = 0; i < rewardsByWave.Count; i++)
        {
            WaveRewardEntry configuredEntry = rewardsByWave[i];
            if (configuredEntry.waveNumber != resolvedWaveNumber)
            {
                continue;
            }

            return configuredEntry.postWaveTokenSelectionPlan;
        }

        return null;
    }

    private void Sanitize()
    {
        defaultNonBossTokenDrops = EnemyWaveConfig.SanitizeTokenDrops(defaultNonBossTokenDrops);
        rewardsByWave ??= new List<WaveRewardEntry>();
        for (int i = 0; i < rewardsByWave.Count; i++)
        {
            rewardsByWave[i] = rewardsByWave[i].GetSanitized();
        }
    }

    private static bool HasAssignedTokenDrop(IReadOnlyList<EnemyBulletTokenDropEntry> tokenDrops)
    {
        if (tokenDrops == null || tokenDrops.Count <= 0)
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
}
