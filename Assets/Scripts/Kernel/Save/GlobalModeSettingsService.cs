using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vocalith.Logging;
using UnityEngine;

/// <summary>
/// 负责把 DevMode/NormalMode 与存档槽位摘要持久化到独立的全局 JSON 文件。
/// </summary>
public static class GlobalModeSettingsService
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter() }
    };

    private static GlobalModeSettingsData currentSettings;
    private static bool hasLoadedSettings;

    /// <summary>
    /// summary: 清空静态缓存，避免域重载后沿用旧的全局模式配置。
    /// param: 无
    /// returns: 无
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        currentSettings = null;
        hasLoadedSettings = false;
    }

    /// <summary>
    /// summary: 返回当前生效的全局模式；若尚未加载则按默认 Normal 模式补齐。
    /// param: 无
    /// returns: 当前缓存中的全局模式
    /// </summary>
    public static GameMode GetMode()
    {
        return LoadMode(GameMode.Normal);
    }

    /// <summary>
    /// summary: 读取全局模式配置；文件缺失时会使用给定默认值创建新文件。
    /// param name="defaultMode": 首次启动或文件缺失时使用的默认模式
    /// param name="forceReload": 为 true 时无视缓存强制重新读取磁盘
    /// returns: 当前生效的全局模式
    /// </summary>
    public static GameMode LoadMode(GameMode defaultMode = GameMode.Normal, bool forceReload = false)
    {
        if (forceReload)
        {
            hasLoadedSettings = false;
            currentSettings = null;
        }

        if (hasLoadedSettings && currentSettings != null)
        {
            return currentSettings.SelectedMode;
        }

        SavePathUtility.EnsureSaveDirectoryExists();
        string filePath = SavePathUtility.GetGlobalModeFilePath();

        if (File.Exists(filePath) && TryReadSettings(filePath, out GlobalModeSettingsData loadedSettings))
        {
            currentSettings = loadedSettings;
            hasLoadedSettings = true;
            return currentSettings.SelectedMode;
        }

        currentSettings = GlobalModeSettingsData.CreateDefault(defaultMode);
        hasLoadedSettings = true;
        SaveMode();
        return currentSettings.SelectedMode;
    }

    /// <summary>
    /// summary: 把当前缓存中的全局模式写回磁盘。
    /// param: 无
    /// returns: 成功写入 global-mode.json 时返回 true
    /// </summary>
    public static bool SaveMode()
    {
        LoadMode();
        string filePath = SavePathUtility.GetGlobalModeFilePath();

        try
        {
            SavePathUtility.EnsureSaveDirectoryExists();
            currentSettings.Sanitize();
            string json = JsonConvert.SerializeObject(currentSettings, SerializerSettings);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception exception)
        {
            GameDebug.LogError($"[GlobalModeSettingsService] Failed to save global mode path='{filePath}'.\n{exception}");
            return false;
        }
    }

    /// <summary>
    /// summary: 更新当前全局模式并立即写回磁盘。
    /// param name="mode": 需要写入的新模式
    /// returns: 模式发生变化时返回 true
    /// </summary>
    public static bool SetMode(GameMode mode)
    {
        LoadMode(mode);
        if (currentSettings.SelectedMode == mode)
        {
            return false;
        }

        currentSettings.SelectedMode = mode;
        currentSettings.Sanitize();
        SaveMode();
        return true;
    }

    /// <summary>
    /// summary: 返回当前全局模式配置的安全副本，供 UI 或测试只读使用。
    /// param: 无
    /// returns: 当前全局模式配置的深拷贝
    /// </summary>
    public static GlobalModeSettingsData GetSettingsSnapshot()
    {
        LoadMode();
        return currentSettings.Clone();
    }

    /// <summary>
    /// summary: 返回存档栏位摘要的安全副本，供 UI 只读展示。
    /// param: 无
    /// returns: 栏位状态的深拷贝数组
    /// </summary>
    public static ProfileSlotStateData[] GetProfileSlotStatesSnapshot()
    {
        LoadMode();
        return CloneProfileSlots(currentSettings.ProfileSlots);
    }

    /// <summary>
    /// summary: 更新指定栏位的全局摘要，并立即写回磁盘。
    /// param name="slotIndex": 目标栏位索引，使用 0 起始的非负整数
    /// param name="hasProfile": 该栏位当前是否存在存档
    /// param name="lastSavedUtcTicks": 最近一次写盘的 UTC ticks；栏位为空时会被清零
    /// param name="lastOpenedUtcTicks": 最近一次打开该栏位的 UTC ticks；传入负数时保留现有打开时间
    /// returns: 摘要发生变化时返回 true
    /// </summary>
    public static bool SetProfileSlotState(int slotIndex, bool hasProfile, long lastSavedUtcTicks, long lastOpenedUtcTicks = -1L)
    {
        LoadMode();
        if (!SavePathUtility.IsValidProfileSlotIndex(slotIndex))
        {
            return false;
        }

        EnsureProfileSlotCapacity(slotIndex + 1);
        ProfileSlotStateData slotState = currentSettings.ProfileSlots[slotIndex] ?? ProfileSlotStateData.CreateDefault();

        bool sanitizedHasProfile = hasProfile;
        long sanitizedSavedTicks = sanitizedHasProfile ? Math.Max(0L, lastSavedUtcTicks) : 0L;
        long sanitizedOpenedTicks = 0L;
        if (sanitizedHasProfile)
        {
            sanitizedOpenedTicks = lastOpenedUtcTicks >= 0L
                ? Math.Max(0L, lastOpenedUtcTicks)
                : Math.Max(0L, slotState.LastOpenedUtcTicks);
        }

        if (slotState.HasProfile == sanitizedHasProfile
            && slotState.LastSavedUtcTicks == sanitizedSavedTicks
            && slotState.LastOpenedUtcTicks == sanitizedOpenedTicks)
        {
            return false;
        }

        slotState.HasProfile = sanitizedHasProfile;
        slotState.LastSavedUtcTicks = sanitizedSavedTicks;
        slotState.LastOpenedUtcTicks = sanitizedOpenedTicks;
        slotState.Sanitize();
        currentSettings.ProfileSlots[slotIndex] = slotState;
        currentSettings.Sanitize();
        SaveMode();
        return true;
    }

    private static void EnsureProfileSlotCapacity(int requiredCount)
    {
        int targetCount = Math.Max(SavePathUtility.DefaultProfileSlotCount, requiredCount);
        if (currentSettings.ProfileSlots != null && currentSettings.ProfileSlots.Length >= targetCount)
        {
            return;
        }

        ProfileSlotStateData[] expandedSlots = CreateDefaultProfileSlots(targetCount);
        if (currentSettings.ProfileSlots != null)
        {
            int copyCount = Math.Min(currentSettings.ProfileSlots.Length, expandedSlots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                expandedSlots[i] = (currentSettings.ProfileSlots[i] ?? ProfileSlotStateData.CreateDefault()).Clone();
            }
        }

        currentSettings.ProfileSlots = expandedSlots;
    }

    private static bool TryReadSettings(string filePath, out GlobalModeSettingsData settings)
    {
        settings = null;
        try
        {
            string json = File.ReadAllText(filePath);
            settings = JsonConvert.DeserializeObject<GlobalModeSettingsData>(json, SerializerSettings);
            settings ??= GlobalModeSettingsData.CreateDefault(GameMode.Normal);
            settings.Sanitize();
            return true;
        }
        catch (Exception exception)
        {
            GameDebug.LogWarning($"[GlobalModeSettingsService] Failed to load global mode path='{filePath}'. A new file will be created.\n{exception}");
            settings = null;
            return false;
        }
    }

    private static ProfileSlotStateData[] CloneProfileSlots(ProfileSlotStateData[] source)
    {
        ProfileSlotStateData[] clone = CreateDefaultProfileSlots(source?.Length ?? SavePathUtility.DefaultProfileSlotCount);
        if (source == null)
        {
            return clone;
        }

        int maxCount = Math.Min(source.Length, clone.Length);
        for (int i = 0; i < maxCount; i++)
        {
            clone[i] = (source[i] ?? ProfileSlotStateData.CreateDefault()).Clone();
        }

        return clone;
    }

    private static ProfileSlotStateData[] CreateDefaultProfileSlots(int count = SavePathUtility.DefaultProfileSlotCount)
    {
        int slotCount = Math.Max(SavePathUtility.DefaultProfileSlotCount, count);
        ProfileSlotStateData[] slots = new ProfileSlotStateData[slotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = ProfileSlotStateData.CreateDefault();
        }

        return slots;
    }
}

/// <summary>
/// 全局游戏模式定义，仅用于独立全局模式配置。
/// </summary>
public enum GameMode
{
    Normal = 0,
    Dev = 1,
}

/// <summary>
/// 全局模式配置 JSON 的根数据。
/// </summary>
[Serializable]
public sealed class GlobalModeSettingsData
{
    public const int CurrentDataVersion = 1;

    public int DataVersion = CurrentDataVersion;
    public GameMode SelectedMode = GameMode.Normal;
    public ProfileSlotStateData[] ProfileSlots = CreateDefaultProfileSlots();

    /// <summary>
    /// summary: 创建一个已完成规范化的默认全局模式配置。
    /// param name="defaultMode": 需要写入的默认模式
    /// returns: 默认全局模式配置实例
    /// </summary>
    public static GlobalModeSettingsData CreateDefault(GameMode defaultMode)
    {
        GlobalModeSettingsData data = new()
        {
            SelectedMode = defaultMode
        };

        data.Sanitize();
        return data;
    }

    /// <summary>
    /// summary: 复制当前全局模式配置，供只读调用方安全使用。
    /// param: 无
    /// returns: 当前全局模式配置的深拷贝
    /// </summary>
    public GlobalModeSettingsData Clone()
    {
        GlobalModeSettingsData clone = new()
        {
            DataVersion = DataVersion,
            SelectedMode = SelectedMode,
            ProfileSlots = CloneProfileSlots(ProfileSlots)
        };

        clone.Sanitize();
        return clone;
    }

    /// <summary>
    /// summary: 规范化全局模式配置，避免非法版本号或未知枚举值进入运行时。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void Sanitize()
    {
        if (DataVersion <= 0)
        {
            DataVersion = CurrentDataVersion;
        }

        if (!Enum.IsDefined(typeof(GameMode), SelectedMode))
        {
            SelectedMode = GameMode.Normal;
        }

        ProfileSlots = SanitizeProfileSlots(ProfileSlots);
    }

    private static ProfileSlotStateData[] CloneProfileSlots(ProfileSlotStateData[] source)
    {
        ProfileSlotStateData[] clone = CreateDefaultProfileSlots(source?.Length ?? SavePathUtility.DefaultProfileSlotCount);
        if (source == null)
        {
            return clone;
        }

        int maxCount = Math.Min(source.Length, clone.Length);
        for (int i = 0; i < maxCount; i++)
        {
            clone[i] = (source[i] ?? ProfileSlotStateData.CreateDefault()).Clone();
        }

        return clone;
    }

    private static ProfileSlotStateData[] SanitizeProfileSlots(ProfileSlotStateData[] source)
    {
        ProfileSlotStateData[] sanitized = CreateDefaultProfileSlots(source?.Length ?? SavePathUtility.DefaultProfileSlotCount);
        if (source == null)
        {
            return sanitized;
        }

        int maxCount = Math.Min(source.Length, sanitized.Length);
        for (int i = 0; i < maxCount; i++)
        {
            ProfileSlotStateData slot = source[i] ?? ProfileSlotStateData.CreateDefault();
            slot.Sanitize();
            sanitized[i] = slot;
        }

        return sanitized;
    }

    private static ProfileSlotStateData[] CreateDefaultProfileSlots(int count = SavePathUtility.DefaultProfileSlotCount)
    {
        int slotCount = Math.Max(SavePathUtility.DefaultProfileSlotCount, count);
        ProfileSlotStateData[] slots = new ProfileSlotStateData[slotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = ProfileSlotStateData.CreateDefault();
        }

        return slots;
    }
}

/// <summary>
/// 单个存档槽位的全局摘要。
/// </summary>
[Serializable]
public sealed class ProfileSlotStateData
{
    public bool HasProfile;
    public long LastSavedUtcTicks;
    public long LastOpenedUtcTicks;

    /// <summary>
    /// summary: 创建一个空栏位的默认摘要。
    /// param: 无
    /// returns: 默认栏位摘要
    /// </summary>
    public static ProfileSlotStateData CreateDefault()
    {
        ProfileSlotStateData data = new();
        data.Sanitize();
        return data;
    }

    /// <summary>
    /// summary: 复制当前栏位摘要，供只读调用方安全使用。
    /// param: 无
    /// returns: 当前栏位摘要的深拷贝
    /// </summary>
    public ProfileSlotStateData Clone()
    {
        ProfileSlotStateData clone = new()
        {
            HasProfile = HasProfile,
            LastSavedUtcTicks = LastSavedUtcTicks,
            LastOpenedUtcTicks = LastOpenedUtcTicks
        };

        clone.Sanitize();
        return clone;
    }

    /// <summary>
    /// summary: 规范化栏位摘要，避免空栏位携带脏时间戳。
    /// param: 无
    /// returns: 无
    /// </summary>
    public void Sanitize()
    {
        if (!HasProfile)
        {
            LastSavedUtcTicks = 0L;
            LastOpenedUtcTicks = 0L;
            return;
        }

        LastSavedUtcTicks = Math.Max(0L, LastSavedUtcTicks);
        LastOpenedUtcTicks = Math.Max(0L, LastOpenedUtcTicks);
    }
}
