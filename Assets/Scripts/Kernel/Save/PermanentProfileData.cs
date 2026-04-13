using System;
using System.Collections.Generic;
using Kernel.Quest;

/// <summary>
/// 单个永久档槽位的根数据，只承载跨 run 保留的 meta progression。
/// </summary>
[Serializable]
public sealed class PermanentProfileData
{
    public const int CurrentDataVersion = 1;

    public int DataVersion = CurrentDataVersion;
    public long LastSavedUtcTicks;
    public int RemnantCount;
    public HashSet<string> UnlockedIds = CreateStringSet();
    public HashSet<string> StoryFlags = CreateStringSet();
    public Dictionary<string, int> LifetimeStats = CreateStatsDictionary();
    public HashSet<string> CompletedQuestIds = CreateStringSet();
    public Dictionary<string, ActiveQuestProgressSaveData> ActiveQuestProgressById = CreateQuestProgressDictionary();

    /// <summary>
    /// summary: 创建一个已完成规范化的默认永久档数据实例。
    /// param: 无
    /// returns: 可直接写入磁盘或应用到运行时的默认永久档
    /// </summary>
    public static PermanentProfileData CreateDefault()
    {
        PermanentProfileData profile = new();
        profile.Sanitize();
        return profile;
    }

    /// <summary>
    /// summary: 复制当前永久档数据，供 UI 只读显示或外部安全使用。
    /// param: 无
    /// returns: 当前永久档数据的深拷贝副本
    /// </summary>
    public PermanentProfileData Clone()
    {
        PermanentProfileData clone = new()
        {
            DataVersion = DataVersion,
            LastSavedUtcTicks = LastSavedUtcTicks,
            RemnantCount = RemnantCount,
            UnlockedIds = new HashSet<string>(UnlockedIds ?? CreateStringSet(), StringComparer.Ordinal),
            StoryFlags = new HashSet<string>(StoryFlags ?? CreateStringSet(), StringComparer.Ordinal),
            LifetimeStats = new Dictionary<string, int>(LifetimeStats ?? CreateStatsDictionary(), StringComparer.Ordinal),
            CompletedQuestIds = new HashSet<string>(CompletedQuestIds ?? CreateStringSet(), StringComparer.Ordinal),
            ActiveQuestProgressById = CloneQuestProgressDictionary(ActiveQuestProgressById),
        };

        clone.Sanitize();
        return clone;
    }

    /// <summary>
    /// summary: 规范化永久档字段，避免空集合、负数和非法键值进入运行时。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void Sanitize()
    {
        if (DataVersion <= 0)
        {
            DataVersion = CurrentDataVersion;
        }

        LastSavedUtcTicks = Math.Max(0L, LastSavedUtcTicks);
        RemnantCount = Math.Max(0, RemnantCount);
        UnlockedIds = SanitizeStringSet(UnlockedIds);
        StoryFlags = SanitizeStringSet(StoryFlags);
        LifetimeStats = SanitizeStats(LifetimeStats);
        CompletedQuestIds = SanitizeStringSet(CompletedQuestIds);
        ActiveQuestProgressById = SanitizeQuestProgressDictionary(ActiveQuestProgressById);
    }

    private static HashSet<string> CreateStringSet()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static Dictionary<string, int> CreateStatsDictionary()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private static Dictionary<string, ActiveQuestProgressSaveData> CreateQuestProgressDictionary()
    {
        return new Dictionary<string, ActiveQuestProgressSaveData>(StringComparer.Ordinal);
    }

    private static HashSet<string> SanitizeStringSet(IEnumerable<string> source)
    {
        HashSet<string> sanitized = CreateStringSet();
        if (source == null)
        {
            return sanitized;
        }

        foreach (string raw in source)
        {
            string value = raw != null ? raw.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(value))
            {
                sanitized.Add(value);
            }
        }

        return sanitized;
    }

    private static Dictionary<string, int> SanitizeStats(IDictionary<string, int> source)
    {
        Dictionary<string, int> sanitized = CreateStatsDictionary();
        if (source == null)
        {
            return sanitized;
        }

        foreach (KeyValuePair<string, int> pair in source)
        {
            string key = pair.Key != null ? pair.Key.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            sanitized[key] = Math.Max(0, pair.Value);
        }

        return sanitized;
    }

    private static Dictionary<string, ActiveQuestProgressSaveData> CloneQuestProgressDictionary(IDictionary<string, ActiveQuestProgressSaveData> source)
    {
        Dictionary<string, ActiveQuestProgressSaveData> clone = CreateQuestProgressDictionary();
        if (source == null)
        {
            return clone;
        }

        foreach (KeyValuePair<string, ActiveQuestProgressSaveData> pair in source)
        {
            string key = pair.Key != null ? pair.Key.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            ActiveQuestProgressSaveData progress = pair.Value != null ? pair.Value.Clone() : ActiveQuestProgressSaveData.CreateDefault();
            clone[key] = progress;
        }

        return clone;
    }

    private static Dictionary<string, ActiveQuestProgressSaveData> SanitizeQuestProgressDictionary(IDictionary<string, ActiveQuestProgressSaveData> source)
    {
        Dictionary<string, ActiveQuestProgressSaveData> sanitized = CreateQuestProgressDictionary();
        if (source == null)
        {
            return sanitized;
        }

        foreach (KeyValuePair<string, ActiveQuestProgressSaveData> pair in source)
        {
            string key = pair.Key != null ? pair.Key.Trim() : string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            ActiveQuestProgressSaveData progress = pair.Value != null ? pair.Value.Clone() : ActiveQuestProgressSaveData.CreateDefault();
            progress.Sanitize();
            sanitized[key] = progress;
        }

        return sanitized;
    }
}
