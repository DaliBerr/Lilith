using System;
using System.Collections.Generic;

namespace Kernel.Quest
{
    /// <summary>
    /// 持久化单个已激活任务的事件计数进度。
    /// </summary>
    [Serializable]
    public sealed class ActiveQuestProgressSaveData
    {
        public int EnemyKillCount;
        public int CombatVictoryCount;
        public int BossKillCount;

        /// <summary>
        /// summary: 创建一个全计数归零的默认任务进度对象。
        /// param: 无
        /// returns: 可直接写入永久档的默认任务进度
        /// </summary>
        public static ActiveQuestProgressSaveData CreateDefault()
        {
            ActiveQuestProgressSaveData progress = new();
            progress.Sanitize();
            return progress;
        }

        /// <summary>
        /// summary: 复制当前任务进度，供调用方安全读取或修改。
        /// param: 无
        /// returns: 当前任务进度的深拷贝副本
        /// </summary>
        public ActiveQuestProgressSaveData Clone()
        {
            ActiveQuestProgressSaveData clone = new()
            {
                EnemyKillCount = EnemyKillCount,
                CombatVictoryCount = CombatVictoryCount,
                BossKillCount = BossKillCount,
            };

            clone.Sanitize();
            return clone;
        }

        /// <summary>
        /// summary: 规整任务进度里的计数值，避免负数进入运行时和磁盘。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void Sanitize()
        {
            EnemyKillCount = Math.Max(0, EnemyKillCount);
            CombatVictoryCount = Math.Max(0, CombatVictoryCount);
            BossKillCount = Math.Max(0, BossKillCount);
        }

        /// <summary>
        /// summary: 判断当前任务进度与另一份进度是否在计数值上完全一致。
        /// param name="other": 需要比较的另一份任务进度
        /// returns: 三项计数全部一致时返回 true
        /// </summary>
        public bool ContentEquals(ActiveQuestProgressSaveData other)
        {
            return other != null
                && EnemyKillCount == other.EnemyKillCount
                && CombatVictoryCount == other.CombatVictoryCount
                && BossKillCount == other.BossKillCount;
        }
    }

    /// <summary>
    /// 描述一条需要写入永久档的 lifetime stat 增量。
    /// </summary>
    [Serializable]
    public sealed class QuestLifetimeStatDeltaData
    {
        public string Key = string.Empty;
        public int Delta;

        public QuestLifetimeStatDeltaData()
        {
        }

        public QuestLifetimeStatDeltaData(string key, int delta)
        {
            Key = key ?? string.Empty;
            Delta = delta;
            Sanitize();
        }

        /// <summary>
        /// summary: 复制当前 lifetime stat 增量对象。
        /// param: 无
        /// returns: 当前增量的深拷贝副本
        /// </summary>
        public QuestLifetimeStatDeltaData Clone()
        {
            QuestLifetimeStatDeltaData clone = new(Key, Delta);
            clone.Sanitize();
            return clone;
        }

        /// <summary>
        /// summary: 清理当前增量对象中的稳定键名字段。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void Sanitize()
        {
            Key = Key != null ? Key.Trim() : string.Empty;
        }
    }

    /// <summary>
    /// 聚合一次任务完成后需要原子写入永久档的奖励变更。
    /// </summary>
    public sealed class QuestCompletionWriteRequest
    {
        public int RemnantAmount;
        public List<string> UnlockIds { get; } = new();
        public List<string> StoryFlagIds { get; } = new();
        public List<QuestLifetimeStatDeltaData> LifetimeStatDeltas { get; } = new();

        /// <summary>
        /// summary: 规范化任务完成写盘请求，去重稳定标识并合并重复统计项。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void Sanitize()
        {
            RemnantAmount = Math.Max(0, RemnantAmount);
            SanitizeStringList(UnlockIds);
            SanitizeStringList(StoryFlagIds);
            SanitizeLifetimeStatDeltas(LifetimeStatDeltas);
        }

        private static void SanitizeStringList(List<string> values)
        {
            if (values == null)
            {
                return;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);
            for (int index = values.Count - 1; index >= 0; index--)
            {
                string sanitized = values[index] != null ? values[index].Trim() : string.Empty;
                if (string.IsNullOrEmpty(sanitized) || !seen.Add(sanitized))
                {
                    values.RemoveAt(index);
                    continue;
                }

                values[index] = sanitized;
            }
        }

        private static void SanitizeLifetimeStatDeltas(List<QuestLifetimeStatDeltaData> deltas)
        {
            if (deltas == null)
            {
                return;
            }

            Dictionary<string, int> merged = new(StringComparer.Ordinal);
            for (int i = 0; i < deltas.Count; i++)
            {
                QuestLifetimeStatDeltaData delta = deltas[i];
                if (delta == null)
                {
                    continue;
                }

                delta.Sanitize();
                if (string.IsNullOrEmpty(delta.Key) || delta.Delta == 0)
                {
                    continue;
                }

                merged.TryGetValue(delta.Key, out int currentValue);
                merged[delta.Key] = currentValue + delta.Delta;
            }

            deltas.Clear();
            foreach (KeyValuePair<string, int> pair in merged)
            {
                if (pair.Value != 0)
                {
                    deltas.Add(new QuestLifetimeStatDeltaData(pair.Key, pair.Value));
                }
            }
        }
    }

    /// <summary>
    /// 提供给 HUD 的已激活任务只读快照。
    /// </summary>
    public readonly struct QuestActiveSnapshot
    {
        public QuestActiveSnapshot(string questId, string text)
        {
            QuestId = questId ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string QuestId { get; }
        public string Text { get; }
    }

    /// <summary>
    /// 提供给 HUD 的任务完成通知。
    /// </summary>
    public readonly struct QuestCompletedSnapshot
    {
        public QuestCompletedSnapshot(string questId, string text)
        {
            QuestId = questId ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string QuestId { get; }
        public string Text { get; }
    }
}
