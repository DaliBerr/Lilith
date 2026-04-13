using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Vocalith.Logging;

/// <summary>
/// 提供四个固定永久档栏位的选择、加载、保存与删除入口。
/// </summary>
[DefaultExecutionOrder(-940)]
[DisallowMultipleComponent]
public sealed class RuntimeSaveService : MonoBehaviour
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented
    };

    private PermanentProfileData currentProfile = PermanentProfileData.CreateDefault();
    private int activeSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
    private bool hasLoadedProfile;
    private bool shouldShowOpeningGuideOnMainSceneEntry;

    public static RuntimeSaveService Instance { get; private set; }

    public bool HasLoadedProfile => hasLoadedProfile;
    public bool HasSelectedProfileSlot => SavePathUtility.IsValidProfileSlotIndex(activeSlotIndex);
    public int ActiveProfileSlotIndex => activeSlotIndex;

    /// <summary>
    /// summary: 清空静态实例引用，避免域重载后残留旧实例句柄。
    /// param: 无
    /// returns: 无
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    /// <summary>
    /// summary: 在首个场景加载前确保运行时永久档服务已创建。
    /// param: 无
    /// returns: 无
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        GetOrCreateInstance();
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
        currentProfile = PermanentProfileData.CreateDefault();
        hasLoadedProfile = false;
        activeSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
        shouldShowOpeningGuideOnMainSceneEntry = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// summary: 返回可用的永久档服务实例；若场景中缺失会自动创建。
    /// param: 无
    /// returns: 可用的 RuntimeSaveService 实例
    /// </summary>
    public static RuntimeSaveService GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        RuntimeSaveService existing = FindFirstObjectByType<RuntimeSaveService>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject bootstrapObject = new(nameof(RuntimeSaveService));
        return bootstrapObject.AddComponent<RuntimeSaveService>();
    }

    /// <summary>
    /// summary: 若当前已选中栏位但尚未载入永久档，则立即从磁盘加载。
    /// param: 无
    /// returns: 无
    /// </summary>
    public static void EnsureProfileLoaded()
    {
        RuntimeSaveService service = GetOrCreateInstance();
        service?.EnsureProfileLoadedInternal();
    }

    /// <summary>
    /// summary: 返回四个栏位的摘要视图；会顺手修正全局摘要与磁盘状态的偏差。
    /// param: 无
    /// returns: 长度固定为 4 的栏位摘要数组
    /// </summary>
    public ProfileSlotSummary[] GetSlotSummaries()
    {
        ProfileSlotStateData[] states = GlobalModeSettingsService.GetProfileSlotStatesSnapshot();
        ProfileSlotSummary[] summaries = new ProfileSlotSummary[SavePathUtility.ProfileSlotCount];

        for (int slotIndex = 0; slotIndex < summaries.Length; slotIndex++)
        {
            string filePath = SavePathUtility.GetProfileFilePath(slotIndex);
            bool hasProfile = File.Exists(filePath);
            long lastSavedUtcTicks = 0L;
            if (hasProfile)
            {
                long storedTicks = states != null && slotIndex < states.Length && states[slotIndex] != null
                    ? states[slotIndex].LastSavedUtcTicks
                    : 0L;

                lastSavedUtcTicks = ResolveSaveTimestamp(filePath, storedTicks);
            }

            GlobalModeSettingsService.SetProfileSlotState(slotIndex, hasProfile, lastSavedUtcTicks);
            summaries[slotIndex] = new ProfileSlotSummary(slotIndex, hasProfile, lastSavedUtcTicks);
        }

        return summaries;
    }

    /// <summary>
    /// summary: 选中一个存档栏位；若栏位为空则立即创建默认永久档并标记为新存档。
    /// param name="slotIndex": 目标栏位索引，使用 0 到 3
    /// param name="isNewSlot": 输出该栏位在本次选择前是否为空
    /// returns: 成功切换到目标栏位时返回 true
    /// </summary>
    public bool SelectProfileSlot(int slotIndex, out bool isNewSlot)
    {
        isNewSlot = false;
        if (!SavePathUtility.IsValidProfileSlotIndex(slotIndex))
        {
            GameDebug.LogWarning($"[RuntimeSaveService] Invalid profile slot index '{slotIndex}'.");
            return false;
        }

        activeSlotIndex = slotIndex;
        hasLoadedProfile = false;
        currentProfile = PermanentProfileData.CreateDefault();
        shouldShowOpeningGuideOnMainSceneEntry = false;

        string filePath = SavePathUtility.GetProfileFilePath(slotIndex);
        isNewSlot = !File.Exists(filePath);
        shouldShowOpeningGuideOnMainSceneEntry = isNewSlot;
        if (isNewSlot)
        {
            currentProfile = PermanentProfileData.CreateDefault();
            hasLoadedProfile = true;
            ApplyProfileToRuntime();
            return WriteProfileToDisk();
        }

        return LoadProfileInternal(forceReload: true);
    }

    /// <summary>
    /// summary: 删除指定栏位的永久档文件，并同步清空全局栏位摘要。
    /// param name="slotIndex": 目标栏位索引，使用 0 到 3
    /// returns: 删除成功或目标文件原本不存在时返回 true
    /// </summary>
    public bool DeleteProfileSlot(int slotIndex)
    {
        if (!SavePathUtility.IsValidProfileSlotIndex(slotIndex))
        {
            return false;
        }

        string filePath = SavePathUtility.GetProfileFilePath(slotIndex);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            GlobalModeSettingsService.SetProfileSlotState(slotIndex, hasProfile: false, lastSavedUtcTicks: 0L);
            if (activeSlotIndex == slotIndex)
            {
                activeSlotIndex = SavePathUtility.InvalidProfileSlotIndex;
                hasLoadedProfile = false;
                currentProfile = PermanentProfileData.CreateDefault();
                shouldShowOpeningGuideOnMainSceneEntry = false;
                ApplyProfileToRuntime();
            }

            return true;
        }
        catch (Exception exception)
        {
            GameDebug.LogError($"[RuntimeSaveService] Failed to delete profile slot '{slotIndex}'.\n{exception}");
            return false;
        }
    }

    /// <summary>
    /// summary: 读取当前已选中栏位的永久档；文件缺失时会自动创建默认档。
    /// param: 无
    /// returns: 获得可用 profile 后返回 true
    /// </summary>
    public bool LoadProfile()
    {
        return LoadProfileInternal(forceReload: false);
    }

    /// <summary>
    /// summary: 把当前内存中的永久档写回当前已选中栏位的 JSON 文件。
    /// param: 无
    /// returns: 成功写入当前栏位时返回 true
    /// </summary>
    public bool SaveProfile()
    {
        return CommitRunEndProfileState();
    }

    /// <summary>
    /// summary: 提交一局结束时的永久档快照；当前暂未接入到对局结束流程，预留给后续 run-end 回调。
    /// param: 无
    /// returns: 成功写入当前栏位时返回 true
    /// </summary>
    public bool CommitRunEndProfileState()
    {
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return false;
        }

        CaptureRuntimeStateIntoProfile();
        return WriteProfileToDisk();
    }

    /// <summary>
    /// summary: 强制从磁盘重新读取当前已选中栏位的永久档，并把永久数据重新应用到运行时。
    /// param: 无
    /// returns: 获得可用 profile 后返回 true
    /// </summary>
    public bool ReloadProfile()
    {
        return LoadProfileInternal(forceReload: true);
    }

    /// <summary>
    /// summary: 把当前已选中栏位的永久档重置为默认值并立即写回磁盘。
    /// param: 无
    /// returns: 成功写回默认 profile 时返回 true
    /// </summary>
    public bool ResetProfile()
    {
        if (!EnsureSelectedSlot())
        {
            return false;
        }

        currentProfile = PermanentProfileData.CreateDefault();
        hasLoadedProfile = true;
        bool saveSuccess = WriteProfileToDisk();
        ApplyProfileToRuntime();
        return saveSuccess;
    }

    /// <summary>
    /// summary: 返回当前永久档的安全副本，供 UI 与测试只读使用。
    /// param: 无
    /// returns: 当前永久档数据的深拷贝副本
    /// </summary>
    public PermanentProfileData GetProfileSnapshot()
    {
        return EnsureProfileLoadedInternal(applyToRuntime: false)
            ? currentProfile.Clone()
            : PermanentProfileData.CreateDefault();
    }

    /// <summary>
    /// summary: 判断当前所选档位在本次进入 Main 场景后是否还需要播放一次开场引导对话；消费后会自动清除标记。
    /// param: 无
    /// returns: 当前存在待消费的开场引导标记时返回 true
    /// </summary>
    public bool TryConsumePendingOpeningGuideOnMainSceneEntry()
    {
        if (!shouldShowOpeningGuideOnMainSceneEntry)
        {
            return false;
        }

        shouldShowOpeningGuideOnMainSceneEntry = false;
        return true;
    }

    /// <summary>
    /// summary: 返回当前已经加载到内存中的永久遗珍数量。
    /// param: 无
    /// returns: 当前永久遗珍总数
    /// </summary>
    public int GetCurrentRemnantCount()
    {
        return EnsureProfileLoadedInternal(applyToRuntime: false)
            ? currentProfile.RemnantCount
            : 0;
    }

    /// <summary>
    /// summary: 读取当前永久档中的单个 lifetime stat。
    /// param name="key": 需要查询的稳定键名
    /// returns: 当前 profile 里的统计值；不存在或无效键名返回 0
    /// </summary>
    public int GetLifetimeStat(string key)
    {
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return 0;
        }

        string trimmedKey = key != null ? key.Trim() : string.Empty;
        return !string.IsNullOrEmpty(trimmedKey) && currentProfile.LifetimeStats.TryGetValue(trimmedKey, out int value)
            ? Mathf.Max(0, value)
            : 0;
    }

    /// <summary>
    /// summary: 用来自运行时钱包的新遗珍总数覆盖当前永久档缓存，不立即写盘。
    /// param name="remnantCount": 当前运行时钱包的总遗珍数量
    /// returns: 数据发生变化时返回 true
    /// </summary>
    public bool SetRemnantCount(int remnantCount)
    {
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return false;
        }

        int sanitizedCount = Mathf.Max(0, remnantCount);
        if (currentProfile.RemnantCount == sanitizedCount)
        {
            return false;
        }

        currentProfile.RemnantCount = sanitizedCount;
        SynchronizeWalletRemnantsIfNeeded();
        return true;
    }

    /// <summary>
    /// summary: 按增量更新永久档缓存中的遗珍数量，不立即写盘。
    /// param name="amount": 需要增加的遗珍数量
    /// param name="resultingCount": 输出变化后的遗珍总数
    /// returns: 数量发生变化时返回 true
    /// </summary>
    public bool AddRemnants(int amount, out int resultingCount)
    {
        resultingCount = 0;
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return false;
        }

        resultingCount = currentProfile.RemnantCount;
        if (amount <= 0)
        {
            return false;
        }

        long nextCount = (long)currentProfile.RemnantCount + amount;
        currentProfile.RemnantCount = nextCount >= int.MaxValue ? int.MaxValue : (int)nextCount;
        resultingCount = currentProfile.RemnantCount;
        SynchronizeWalletRemnantsIfNeeded();
        return true;
    }

    /// <summary>
    /// summary: 在永久档中标记一个已解锁条目，并在变化后自动保存。
    /// param name="id": 需要标记为解锁的稳定标识
    /// returns: 首次成功解锁该标识时返回 true
    /// </summary>
    public bool Unlock(string id)
    {
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return false;
        }

        string trimmedId = id != null ? id.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmedId))
        {
            return false;
        }

        if (!currentProfile.UnlockedIds.Add(trimmedId))
        {
            return false;
        }

        PersistProfileMutation("unlock item");
        return true;
    }

    /// <summary>
    /// summary: 设置或清理一个剧情标记，并在变化后自动保存。
    /// param name="id": 剧情标记的稳定标识
    /// param name="value": true 表示写入标记，false 表示移除标记
    /// returns: 标记集合发生变化时返回 true
    /// </summary>
    public bool SetStoryFlag(string id, bool value)
    {
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return false;
        }

        string trimmedId = id != null ? id.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmedId))
        {
            return false;
        }

        bool changed = value
            ? currentProfile.StoryFlags.Add(trimmedId)
            : currentProfile.StoryFlags.Remove(trimmedId);

        if (!changed)
        {
            return false;
        }

        PersistProfileMutation("set story flag");
        return true;
    }

    /// <summary>
    /// summary: 增减一个永久统计值，并在变化后自动保存。
    /// param name="key": 统计项稳定键名
    /// param name="delta": 本次变化量
    /// param name="resultingValue": 输出变化后的统计值
    /// returns: 统计值发生变化时返回 true
    /// </summary>
    public bool IncrementLifetimeStat(string key, int delta, out int resultingValue)
    {
        resultingValue = 0;
        if (!EnsureProfileLoadedInternal(applyToRuntime: false))
        {
            return false;
        }

        string trimmedKey = key != null ? key.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmedKey))
        {
            return false;
        }

        currentProfile.LifetimeStats.TryGetValue(trimmedKey, out int currentValue);
        long nextValue = (long)currentValue + delta;
        int sanitizedValue = nextValue <= 0L ? 0 : nextValue >= int.MaxValue ? int.MaxValue : (int)nextValue;
        resultingValue = sanitizedValue;

        if (currentValue == sanitizedValue)
        {
            return false;
        }

        if (sanitizedValue == 0)
        {
            currentProfile.LifetimeStats.Remove(trimmedKey);
        }
        else
        {
            currentProfile.LifetimeStats[trimmedKey] = sanitizedValue;
        }

        PersistProfileMutation("increment lifetime stat");
        return true;
    }

    private bool EnsureProfileLoadedInternal(bool applyToRuntime = true)
    {
        if (hasLoadedProfile)
        {
            if (applyToRuntime)
            {
                ApplyProfileToRuntime();
            }

            return true;
        }

        return LoadProfileInternal(forceReload: false, applyToRuntime);
    }

    private bool LoadProfileInternal(bool forceReload, bool applyToRuntime = true)
    {
        if (!EnsureSelectedSlot())
        {
            return false;
        }

        if (hasLoadedProfile && !forceReload)
        {
            if (applyToRuntime)
            {
                ApplyProfileToRuntime();
            }

            return true;
        }

        SavePathUtility.EnsureSaveDirectoryExists();
        string filePath = GetActiveProfilePath();

        PermanentProfileData loadedProfile;
        if (File.Exists(filePath))
        {
            loadedProfile = TryReadProfile(filePath, out PermanentProfileData diskProfile)
                ? diskProfile
                : PermanentProfileData.CreateDefault();
        }
        else
        {
            loadedProfile = PermanentProfileData.CreateDefault();
        }

        currentProfile = loadedProfile ?? PermanentProfileData.CreateDefault();
        currentProfile.Sanitize();
        hasLoadedProfile = true;

        if (!File.Exists(filePath) || currentProfile.LastSavedUtcTicks <= 0L)
        {
            if (!WriteProfileToDisk())
            {
                return false;
            }
        }

        if (applyToRuntime)
        {
            ApplyProfileToRuntime();
        }

        return true;
    }

    private bool TryReadProfile(string filePath, out PermanentProfileData profile)
    {
        profile = null;
        try
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            profile = JsonConvert.DeserializeObject<PermanentProfileData>(json, SerializerSettings);
            profile ??= PermanentProfileData.CreateDefault();
            if (profile.LastSavedUtcTicks <= 0L)
            {
                profile.LastSavedUtcTicks = ResolveSaveTimestamp(filePath, 0L);
            }

            profile.Sanitize();
            GlobalModeSettingsService.SetProfileSlotState(activeSlotIndex, hasProfile: true, lastSavedUtcTicks: profile.LastSavedUtcTicks);
            return true;
        }
        catch (Exception exception)
        {
            GameDebug.LogWarning($"[RuntimeSaveService] Failed to load profile path='{filePath}'. The slot will be reset to default.\n{exception}");
            profile = null;
            return false;
        }
    }

    private void ApplyProfileToRuntime()
    {
        PlayerRemnantWallet wallet = PlayerRemnantWallet.Instance ?? FindFirstObjectByType<PlayerRemnantWallet>();
        if (wallet != null)
        {
            wallet.ApplyLoadedRemnants(currentProfile.RemnantCount);
        }
    }

    private void SynchronizeWalletRemnantsIfNeeded()
    {
        PlayerRemnantWallet wallet = PlayerRemnantWallet.Instance ?? FindFirstObjectByType<PlayerRemnantWallet>();
        if (wallet != null && wallet.CurrentRemnants != currentProfile.RemnantCount)
        {
            wallet.ApplyLoadedRemnants(currentProfile.RemnantCount);
        }
    }

    private void PersistProfileMutation(string reason)
    {
        if (!WriteProfileToDisk())
        {
            GameDebug.LogWarning($"[RuntimeSaveService] Failed to persist profile mutation '{reason}'. Runtime state will continue using in-memory data.");
        }
    }

    /// <summary>
    /// summary: 在写盘前从当前运行时对象抓取仍由业务组件持有的永久数据，避免旧值覆盖当前状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void CaptureRuntimeStateIntoProfile()
    {
        PlayerRemnantWallet wallet = PlayerRemnantWallet.Instance ?? FindFirstObjectByType<PlayerRemnantWallet>();
        if (wallet != null)
        {
            currentProfile.RemnantCount = wallet.CurrentRemnants;
        }

        currentProfile.Sanitize();
    }

    /// <summary>
    /// summary: 直接把 currentProfile 写入当前栏位 JSON，并同步更新全局栏位摘要。
    /// param: 无
    /// returns: 成功写入当前栏位时返回 true
    /// </summary>
    private bool WriteProfileToDisk()
    {
        if (!EnsureSelectedSlot())
        {
            return false;
        }

        string filePath = GetActiveProfilePath();
        try
        {
            SavePathUtility.EnsureSaveDirectoryExists();
            currentProfile.Sanitize();
            currentProfile.LastSavedUtcTicks = DateTime.UtcNow.Ticks;
            string json = JsonConvert.SerializeObject(currentProfile, SerializerSettings);
            File.WriteAllText(filePath, json, Encoding.UTF8);
            GlobalModeSettingsService.SetProfileSlotState(activeSlotIndex, hasProfile: true, lastSavedUtcTicks: currentProfile.LastSavedUtcTicks);
            return true;
        }
        catch (Exception exception)
        {
            GameDebug.LogError($"[RuntimeSaveService] Failed to save profile path='{filePath}'.\n{exception}");
            return false;
        }
    }

    private bool EnsureSelectedSlot()
    {
        if (SavePathUtility.IsValidProfileSlotIndex(activeSlotIndex))
        {
            return true;
        }

        GameDebug.LogWarning("[RuntimeSaveService] No profile slot has been selected yet.");
        return false;
    }

    private string GetActiveProfilePath()
    {
        return SavePathUtility.GetProfileFilePath(activeSlotIndex);
    }

    private static long ResolveSaveTimestamp(string filePath, long storedTicks)
    {
        if (storedTicks > 0L)
        {
            return storedTicks;
        }

        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
        return lastWriteUtc == default ? 0L : lastWriteUtc.Ticks;
    }
}

/// <summary>
/// 四个固定存档槽位在 UI 中展示用的只读摘要。
/// </summary>
public readonly struct ProfileSlotSummary
{
    public ProfileSlotSummary(int slotIndex, bool hasProfile, long lastSavedUtcTicks)
    {
        SlotIndex = slotIndex;
        HasProfile = hasProfile;
        LastSavedUtcTicks = lastSavedUtcTicks;
    }

    public int SlotIndex { get; }
    public bool HasProfile { get; }
    public long LastSavedUtcTicks { get; }
}
