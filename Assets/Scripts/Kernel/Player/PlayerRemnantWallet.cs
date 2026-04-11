using System;
using UnityEngine;

/// <summary>
/// 管理玩家当前持有的残卷数量，并提供跨场景可访问的计数入口。
/// </summary>
[DefaultExecutionOrder(-950)]
[DisallowMultipleComponent]
public sealed class PlayerRemnantWallet : MonoBehaviour
{
    private const int DefaultStartingRemnants = 0;

    [SerializeField, Min(0)] private int startingRemnants = DefaultStartingRemnants;

    private bool hasInitialized;
    private int remnantCount;

    public static PlayerRemnantWallet Instance { get; private set; }

    public event Action<int, int> Changed;

    public int CurrentRemnants
    {
        get
        {
            EnsureInitialized();
            return remnantCount;
        }
    }

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
    /// summary: 在首场景加载前确保残卷钱包实例存在，供掉落拾取链路直接访问。
    /// param: 无
    /// returns: 无
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EnsureInstance();
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
        SanitizeConfiguration();
        EnsureInitialized();
    }

    private void OnValidate()
    {
        SanitizeConfiguration();
        if (!Application.isPlaying)
        {
            remnantCount = startingRemnants;
            hasInitialized = true;
            return;
        }

        remnantCount = Mathf.Max(0, remnantCount);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// summary: 读取当前全局残卷数量；若实例缺失会自动创建。
    /// param: 无
    /// returns: 当前残卷数量
    /// </summary>
    public static int GetCurrentRemnants()
    {
        PlayerRemnantWallet instance = EnsureInstance();
        return instance != null ? instance.CurrentRemnants : 0;
    }

    /// <summary>
    /// summary: 增加全局残卷数量；若实例缺失会自动创建。
    /// param: amount 需要增加的残卷数量
    /// param: resultingCount 输出增加后的残卷总数
    /// returns: 成功增加时返回 true
    /// </summary>
    public static bool TryAddCurrentRemnants(int amount, out int resultingCount)
    {
        PlayerRemnantWallet instance = EnsureInstance();
        if (instance == null)
        {
            resultingCount = 0;
            return false;
        }

        return instance.TryAddRemnants(amount, out resultingCount);
    }

    /// <summary>
    /// summary: 覆盖全局残卷数量；若实例缺失会自动创建。
    /// param: amount 需要写入的残卷数量
    /// param: resultingCount 输出写入后的残卷总数
    /// returns: 数值发生变化时返回 true
    /// </summary>
    public static bool TrySetCurrentRemnants(int amount, out int resultingCount)
    {
        PlayerRemnantWallet instance = EnsureInstance();
        if (instance == null)
        {
            resultingCount = 0;
            return false;
        }

        return instance.TrySetRemnants(amount, out resultingCount);
    }

    /// <summary>
    /// summary: 增加当前实例持有的残卷数量。
    /// param: amount 需要增加的残卷数量
    /// param: resultingCount 输出增加后的残卷总数
    /// returns: 成功增加时返回 true
    /// </summary>
    public bool TryAddRemnants(int amount, out int resultingCount)
    {
        RuntimeSaveService.EnsureProfileLoaded();
        EnsureInitialized();
        resultingCount = remnantCount;
        if (amount <= 0)
        {
            return false;
        }

        int previousCount = remnantCount;
        long nextCount = (long)remnantCount + amount;
        remnantCount = nextCount >= int.MaxValue ? int.MaxValue : (int)nextCount;
        resultingCount = remnantCount;
        RuntimeSaveService.GetOrCreateInstance()?.SetRemnantCount(remnantCount);
        PublishChanged(previousCount);
        return true;
    }

    /// <summary>
    /// summary: 覆盖当前实例持有的残卷数量。
    /// param: amount 需要写入的残卷数量
    /// param: resultingCount 输出写入后的残卷总数
    /// returns: 数值发生变化时返回 true
    /// </summary>
    public bool TrySetRemnants(int amount, out int resultingCount)
    {
        RuntimeSaveService.EnsureProfileLoaded();
        return TrySetRemnantsInternal(amount, out resultingCount, persistProfile: true);
    }

    /// <summary>
    /// summary: 用永久档中的遗珍数量覆盖当前钱包，但不触发反向写档。
    /// param name="amount": 需要从永久档同步到钱包的遗珍数量
    /// returns: 数值发生变化时返回 true
    /// </summary>
    public bool ApplyLoadedRemnants(int amount)
    {
        return TrySetRemnantsInternal(amount, out _, persistProfile: false);
    }

    /// <summary>
    /// summary: 覆盖当前实例持有的残卷数量，并按需要回写永久档。
    /// param name="amount": 需要写入的残卷数量
    /// param name="resultingCount": 输出写入后的残卷总数
    /// param name="persistProfile": 是否把本次变化同步回永久档
    /// returns: 数值发生变化时返回 true
    /// </summary>
    private bool TrySetRemnantsInternal(int amount, out int resultingCount, bool persistProfile)
    {
        EnsureInitialized();
        int sanitizedAmount = Mathf.Max(0, amount);
        resultingCount = sanitizedAmount;
        if (remnantCount == sanitizedAmount)
        {
            return false;
        }

        int previousCount = remnantCount;
        remnantCount = sanitizedAmount;
        if (persistProfile)
        {
            RuntimeSaveService.GetOrCreateInstance()?.SetRemnantCount(remnantCount);
        }

        PublishChanged(previousCount);
        return true;
    }

    /// <summary>
    /// summary: 确保运行时数量已经初始化；首次访问时会应用 Inspector 里的起始值。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureInitialized()
    {
        if (hasInitialized)
        {
            return;
        }

        SanitizeConfiguration();
        RuntimeSaveService runtimeSaveService = RuntimeSaveService.Instance;
        if (runtimeSaveService != null && runtimeSaveService.HasLoadedProfile)
        {
            remnantCount = runtimeSaveService.GetCurrentRemnantCount();
        }
        else
        {
            remnantCount = startingRemnants;
        }

        hasInitialized = true;
    }

    /// <summary>
    /// summary: 修正残卷钱包的序列化配置，避免负数起始值进入运行时。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SanitizeConfiguration()
    {
        startingRemnants = Mathf.Max(0, startingRemnants);
    }

    /// <summary>
    /// summary: 广播残卷数量变化事件，供 UI 或统计逻辑订阅。
    /// param: previousCount 变更前数量
    /// returns: 无
    /// </summary>
    private void PublishChanged(int previousCount)
    {
        Changed?.Invoke(previousCount, remnantCount);
    }

    /// <summary>
    /// summary: 获取当前可用的钱包实例；若场景中不存在则动态创建一个。
    /// param: 无
    /// returns: 可用的钱包实例；创建失败时返回 null
    /// </summary>
    private static PlayerRemnantWallet EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PlayerRemnantWallet existing = FindFirstObjectByType<PlayerRemnantWallet>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject bootstrapObject = new(nameof(PlayerRemnantWallet));
        return bootstrapObject.AddComponent<PlayerRemnantWallet>();
    }
}
