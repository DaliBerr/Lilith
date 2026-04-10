using System;
using System.Collections.Generic;
using System.IO;
using Kernel.GameState;
using UnityEngine;
using Vocalith.EventSystem;
using Vocalith.Logging;
using Vocalith.Scribe;

/// <summary>
/// 提供运行时存档/读档入口，并订阅全局 Save/Load 事件请求。
/// </summary>
[DefaultExecutionOrder(-940)]
[DisallowMultipleComponent]
public sealed class RuntimeSaveService : MonoBehaviour
{
    private const string SaveFolderName = "Saves";
    private const string DefaultSlotName = "autosave";
    private const string SaveFileExtension = ".json";
    private const int SaveFileVersion = 1;

    private IDisposable saveRequestSubscription;
    private IDisposable loadRequestSubscription;

    public static RuntimeSaveService Instance { get; private set; }

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
    /// summary: 在首个场景加载前确保运行时 Save 服务已创建并订阅全局请求事件。
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

        InitializeScribeRegistries();
        SubscribeRequests();
    }

    private void OnDestroy()
    {
        UnsubscribeRequests();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// summary: 以指定槽位名把当前可保存状态写入磁盘 JSON 文件。
    /// param: slotName 目标存档槽位名；为空时自动回退到默认槽位
    /// param: savePath 输出实际写入的存档文件路径
    /// returns: 成功写入并完成 Scribe 序列化时返回 true
    /// </summary>
    public bool TrySave(string slotName, out string savePath)
    {
        InitializeScribeRegistries();

        string resolvedSlotName = ResolveSlotName(slotName);
        savePath = BuildSaveFilePath(resolvedSlotName);
        EnsureSaveDirectoryExists();

        List<ISaveItem> saveItems = BuildSaveItems();
        bool hasInitializedScribe = false;
        try
        {
            using FileStream stream = new(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Scribe.InitSaving(stream, SaveFileVersion);
            hasInitializedScribe = true;
            Scribe_Polymorph.LookList("saveItems", ref saveItems);
            return true;
        }
        catch (Exception exception)
        {
            GameDebug.LogError($"[RuntimeSaveService] Failed to save slot='{resolvedSlotName}' path='{savePath}'.\n{exception}");
            return false;
        }
        finally
        {
            if (hasInitializedScribe)
            {
                Scribe.FinalizeWriting();
            }
        }
    }

    /// <summary>
    /// summary: 以指定槽位名从磁盘读取 JSON 存档并回放所有 SaveItem。
    /// param: slotName 目标存档槽位名；为空时自动回退到默认槽位
    /// param: savePath 输出实际读取的存档文件路径
    /// returns: 成功读取并完成 Scribe 反序列化时返回 true
    /// </summary>
    public bool TryLoad(string slotName, out string savePath)
    {
        InitializeScribeRegistries();

        string resolvedSlotName = ResolveSlotName(slotName);
        savePath = BuildSaveFilePath(resolvedSlotName);
        if (!File.Exists(savePath))
        {
            GameDebug.LogWarning($"[RuntimeSaveService] Save file was not found for slot='{resolvedSlotName}' path='{savePath}'.");
            return false;
        }

        List<ISaveItem> saveItems = null;
        bool hasInitializedScribe = false;
        try
        {
            using FileStream stream = new(savePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Scribe.InitLoading(stream);
            hasInitializedScribe = true;
            Scribe_Polymorph.LookList("saveItems", ref saveItems);
            return saveItems != null;
        }
        catch (Exception exception)
        {
            GameDebug.LogError($"[RuntimeSaveService] Failed to load slot='{resolvedSlotName}' path='{savePath}'.\n{exception}");
            return false;
        }
        finally
        {
            if (hasInitializedScribe)
            {
                Scribe.FinalizeLoading();
            }
        }
    }

    /// <summary>
    /// summary: 对外提供静态存档入口；若服务未创建会自动拉起实例。
    /// param: slotName 目标存档槽位名
    /// param: savePath 输出实际写入的存档文件路径
    /// returns: 成功存档时返回 true
    /// </summary>
    public static bool TrySaveCurrentState(string slotName, out string savePath)
    {
        RuntimeSaveService service = EnsureInstance();
        if (service == null)
        {
            savePath = string.Empty;
            return false;
        }

        return service.TrySave(slotName, out savePath);
    }

    /// <summary>
    /// summary: 对外提供静态读档入口；若服务未创建会自动拉起实例。
    /// param: slotName 目标存档槽位名
    /// param: savePath 输出实际读取的存档文件路径
    /// returns: 成功读档时返回 true
    /// </summary>
    public static bool TryLoadCurrentState(string slotName, out string savePath)
    {
        RuntimeSaveService service = EnsureInstance();
        if (service == null)
        {
            savePath = string.Empty;
            return false;
        }

        return service.TryLoad(slotName, out savePath);
    }

    /// <summary>
    /// summary: 订阅全局 Save/Load 请求事件，并把请求转发到当前服务实例。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void SubscribeRequests()
    {
        saveRequestSubscription?.Dispose();
        loadRequestSubscription?.Dispose();

        saveRequestSubscription = EventManager.eventBus.Subscribe<EventList.SaveGameRequest>(HandleSaveRequest);
        loadRequestSubscription = EventManager.eventBus.Subscribe<EventList.LoadGameRequest>(HandleLoadRequest);
    }

    /// <summary>
    /// summary: 释放全局 Save/Load 事件订阅，避免对象销毁后残留委托。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void UnsubscribeRequests()
    {
        saveRequestSubscription?.Dispose();
        saveRequestSubscription = null;

        loadRequestSubscription?.Dispose();
        loadRequestSubscription = null;
    }

    /// <summary>
    /// summary: 处理外部发起的存档请求事件。
    /// param: request SaveGameRequest 请求体
    /// returns: 无
    /// </summary>
    private void HandleSaveRequest(EventList.SaveGameRequest request)
    {
        string slotName = ResolveSlotName(request.saveName);
        if (TrySave(slotName, out string savePath))
        {
            GameDebug.Log($"[RuntimeSaveService] Saved slot='{slotName}' path='{savePath}'.");
            return;
        }

        GameDebug.LogWarning($"[RuntimeSaveService] Save request failed for slot='{slotName}'.");
    }

    /// <summary>
    /// summary: 处理外部发起的读档请求事件。
    /// param: request LoadGameRequest 请求体
    /// returns: 无
    /// </summary>
    private void HandleLoadRequest(EventList.LoadGameRequest request)
    {
        string slotName = ResolveSlotName(request.loadName);
        if (TryLoad(slotName, out string savePath))
        {
            GameDebug.Log($"[RuntimeSaveService] Loaded slot='{slotName}' path='{savePath}'.");
            return;
        }

        GameDebug.LogWarning($"[RuntimeSaveService] Load request failed for slot='{slotName}'.");
    }

    /// <summary>
    /// summary: 初始化 Scribe 基础 codec 与当前项目允许反序列化的 SaveItem 类型。
    /// param: 无
    /// returns: 无
    /// </summary>
    private static void InitializeScribeRegistries()
    {
        ScribeBootstrap.InitializeDefaults();
        PolymorphRegistry.Register<SaveStatus>("StatusNames");
        PolymorphRegistry.Register<SavePlayerRemnants>("PlayerRemnants");
    }

    /// <summary>
    /// summary: 组装当前需要写入存档的 SaveItem 列表。
    /// param: 无
    /// returns: 当前帧要参与存档的 SaveItem 列表
    /// </summary>
    private static List<ISaveItem> BuildSaveItems()
    {
        return new List<ISaveItem>
        {
            new SaveStatus(),
            new SavePlayerRemnants(),
        };
    }

    /// <summary>
    /// summary: 清理并规范化槽位名，避免非法路径字符导致文件创建失败。
    /// param: slotName 外部传入的槽位名
    /// returns: 可用于构建文件路径的合法槽位名
    /// </summary>
    private static string ResolveSlotName(string slotName)
    {
        string candidate = string.IsNullOrWhiteSpace(slotName) ? DefaultSlotName : slotName.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            candidate = candidate.Replace(invalidChars[i], '_');
        }

        return string.IsNullOrWhiteSpace(candidate) ? DefaultSlotName : candidate;
    }

    /// <summary>
    /// summary: 读取当前项目在 persistentDataPath 下的存档目录路径。
    /// param: 无
    /// returns: 存档目录绝对路径
    /// </summary>
    private static string GetSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFolderName);
    }

    /// <summary>
    /// summary: 组合指定槽位名对应的存档文件绝对路径。
    /// param: slotName 规范化后的槽位名
    /// returns: 存档文件绝对路径
    /// </summary>
    private static string BuildSaveFilePath(string slotName)
    {
        return Path.Combine(GetSaveDirectoryPath(), slotName + SaveFileExtension);
    }

    /// <summary>
    /// summary: 确保存档目录存在，不存在时自动创建。
    /// param: 无
    /// returns: 无
    /// </summary>
    private static void EnsureSaveDirectoryExists()
    {
        string saveDirectoryPath = GetSaveDirectoryPath();
        if (!Directory.Exists(saveDirectoryPath))
        {
            Directory.CreateDirectory(saveDirectoryPath);
        }
    }

    /// <summary>
    /// summary: 获取可用的 RuntimeSaveService 实例；若场景中缺失会动态创建。
    /// param: 无
    /// returns: 可用的 RuntimeSaveService 实例
    /// </summary>
    private static RuntimeSaveService EnsureInstance()
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
}
