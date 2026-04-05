using System;
using System.Collections.Generic;
using Kernel;
using Kernel.UI;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一管理多个 InputSystem 自动生成的 *Controls（IInputActionCollection）。
/// - Awake：创建并注册全部 Controls
/// - EnableAll：场景加载/进入时统一启用
/// - UnloadAll：场景卸载/退出时统一禁用并释放（Disable + Dispose）
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class InputActionManager : MonoBehaviour
{
    public static InputActionManager Instance { get; private set; }

    // ====== 在这里放你的各种 Controls（把类型名替换成你项目里的生成类）======
    public PlayerControls Player { get; private set; }
    public UIControls UI { get; private set; }

    // ====================================================================

    private readonly List<IInputActionCollection> _collections = new();
    private readonly List<IDisposable> _disposables = new();
    private bool _actionsEnabled;
    private bool _initialized;
    private bool _manualEnableRequested = true;
    private bool _textEntryInterlockActive;
    private bool _unloaded;

    /// <summary>
    /// 是否已初始化（Awake 完成 new 并注册）。
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// 是否已卸载（UnloadAll 执行完毕）。
    /// </summary>
    public bool IsUnloaded => _unloaded;

    public bool IsTextEntryInterlockActive => _textEntryInterlockActive;

    /// <summary>
    /// summary: 在首个场景加载前确保场景中存在输入管理器实例。
    /// param: 无
    /// returns: 无
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
        {
            return;
        }

        if (FindFirstObjectByType<InputActionManager>() != null)
        {
            return;
        }

        var bootstrapObject = new GameObject(nameof(InputActionManager));
        bootstrapObject.AddComponent<InputActionManager>();
    }

    /// <summary>
    /// Awake：创建单例并初始化所有 Controls。
    /// </summary>
    /// <returns>无。</returns>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAllControls();
        EnableAll();
    }

    private void Update()
    {
        SetTextEntryInterlock(GlobalTextInputGuard.ShouldBlockExternalInput);
    }

    /// <summary>
    /// 初始化所有 Controls（只应在 Awake 调用一次）。
    /// </summary>
    /// <returns>无。</returns>
    private void InitializeAllControls()
    {
        if (_initialized) return;

        // 在这里统一 new + 注册
        Player = CreateAndRegister<PlayerControls>();
        // Animation = CreateAndRegister<AnimationControls>();
        // Building = CreateAndRegister<BuildingControls>();
        // Camera = CreateAndRegister<CameraControls>();
        // Dev = CreateAndRegister<DevControls>();
        // Factory = CreateAndRegister<FactoryControls>();
        // Map = CreateAndRegister<MapControls>();
        // Save = CreateAndRegister<SaveControls>();
        // Speed = CreateAndRegister<SpeedControls>();
        UI = CreateAndRegister<UIControls>();

        _initialized = true;
        _unloaded = false;
    }

    /// <summary>
    /// 创建并注册一个 Controls（用于批量 Enable/Disable/Dispose）。
    /// </summary>
    /// <typeparam name="T">自动生成的 Controls 类型。</typeparam>
    /// <returns>创建好的 Controls 实例。</returns>
    private T CreateAndRegister<T>()
        where T : class, IInputActionCollection, IDisposable, new()
    {
        var inst = new T();
        _collections.Add(inst);
        _disposables.Add(inst);
        return inst;
    }

    /// <summary>
    /// 场景加载/进入时统一启用全部 Controls。
    /// </summary>
    /// <returns>无。</returns>
    public void EnableAll()
    {
        if (!_initialized || _unloaded) return;
        _manualEnableRequested = true;
        RefreshEnabledState();
    }

    /// <summary>
    /// 场景暂停/离开时统一禁用全部 Controls（不释放资源）。
    /// </summary>
    /// <returns>无。</returns>
    public void DisableAll()
    {
        if (!_initialized) return;
        _manualEnableRequested = false;
        RefreshEnabledState();
    }

    /// <summary>
    /// summary: 设置文本输入互锁；激活期间除 Esc 外的外部快捷键 action maps 会统一停用。
    /// param: active 是否启用文本输入互锁
    /// returns: 无
    /// </summary>
    public void SetTextEntryInterlock(bool active)
    {
        if (_textEntryInterlockActive == active)
        {
            return;
        }

        _textEntryInterlockActive = active;
        RefreshEnabledState();
    }

    private void RefreshEnabledState()
    {
        if (!_initialized || _unloaded)
        {
            return;
        }

        bool shouldEnable = _manualEnableRequested && !_textEntryInterlockActive;
        if (_actionsEnabled == shouldEnable)
        {
            return;
        }

        for (int i = 0; i < _collections.Count; i++)
        {
            if (shouldEnable)
            {
                _collections[i].Enable();
            }
            else
            {
                _collections[i].Disable();
            }
        }

        _actionsEnabled = shouldEnable;
    }

    /// <summary>
    /// 对外接口：卸载全部 Controls（Disable + Dispose），防止泄漏与断言。
    /// </summary>
    /// <returns>无。</returns>
    public void UnloadAll()
    {
        if (!_initialized || _unloaded) return;

        // 先全部 Disable，避免 InputSystem 报 “Enable 未 Disable”
        DisableAll();

        // 再 Dispose 释放底层资源
        for (int i = _disposables.Count - 1; i >= 0; i--)
            _disposables[i].Dispose();

        _collections.Clear();
        _disposables.Clear();

        // 把引用清空（避免外部误用）
        Player = null;
        UI = null;
        // Building = null;
        // Factory = null;
        // Camera = null;
        // Whatever = null;

        _unloaded = true;
        _initialized = false;
        _actionsEnabled = false;
    }

    /// <summary>
    /// 兜底：对象销毁时确保已卸载，避免忘记调用 UnloadAll 导致断言。
    /// </summary>
    /// <returns>无。</returns>
    private void OnDestroy()
    {
        if (!_unloaded)
            UnloadAll();

        if (Instance == this)
            Instance = null;
    }
}
