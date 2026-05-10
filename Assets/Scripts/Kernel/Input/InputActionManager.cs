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
    private const string PlayerBindingOverridesPrefsKey = "Options.Input.PlayerBindingOverrides";
    private const string UIBindingOverridesPrefsKey = "Options.Input.UIBindingOverrides";

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
    private InputActionRebindingExtensions.RebindingOperation _activeRebindOperation;

    /// <summary>
    /// 是否已初始化（Awake 完成 new 并注册）。
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// 是否已卸载（UnloadAll 执行完毕）。
    /// </summary>
    public bool IsUnloaded => _unloaded;

    public bool IsTextEntryInterlockActive => _textEntryInterlockActive;
    public bool IsRebinding => _activeRebindOperation != null;

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
        LoadSavedBindingOverrides();

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

        CancelActiveRebind();

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

    public bool TryGetBindingInfo(
        string collectionName,
        string actionMapName,
        string actionName,
        string bindingName,
        int bindingIndexOverride,
        out string effectivePath,
        out string defaultPath,
        out string displayString,
        out string error)
    {
        effectivePath = string.Empty;
        defaultPath = string.Empty;
        displayString = string.Empty;

        if (!TryResolveBinding(
                collectionName,
                actionMapName,
                actionName,
                bindingName,
                bindingIndexOverride,
                out InputAction action,
                out int bindingIndex,
                out error))
        {
            return false;
        }

        InputBinding binding = action.bindings[bindingIndex];
        effectivePath = binding.effectivePath ?? string.Empty;
        defaultPath = binding.path ?? string.Empty;
        displayString = string.IsNullOrEmpty(effectivePath)
            ? string.Empty
            : action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        return true;
    }

    public bool TryApplyBindingOverride(
        string collectionName,
        string actionMapName,
        string actionName,
        string bindingName,
        int bindingIndexOverride,
        string bindingPath,
        out string error)
    {
        if (!TryResolveBinding(
                collectionName,
                actionMapName,
                actionName,
                bindingName,
                bindingIndexOverride,
                out InputAction action,
                out int bindingIndex,
                out error))
        {
            return false;
        }

        string trimmedPath = bindingPath != null ? bindingPath.Trim() : string.Empty;
        string defaultPath = action.bindings[bindingIndex].path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPath) || string.Equals(trimmedPath, defaultPath, StringComparison.Ordinal))
        {
            action.RemoveBindingOverride(bindingIndex);
            return true;
        }

        action.ApplyBindingOverride(bindingIndex, trimmedPath);
        return true;
    }

    public bool TryStartInteractiveRebind(
        string collectionName,
        string actionMapName,
        string actionName,
        string bindingName,
        int bindingIndexOverride,
        string requiredControlPath,
        string expectedControlType,
        Action<bool, string, string> onFinished,
        out string error)
    {
        if (_activeRebindOperation != null)
        {
            error = "已有按键绑定正在等待输入。";
            return false;
        }

        if (!TryResolveBinding(
                collectionName,
                actionMapName,
                actionName,
                bindingName,
                bindingIndexOverride,
                out InputAction action,
                out int bindingIndex,
                out error))
        {
            return false;
        }

        bool actionMapWasEnabled = action.actionMap.enabled;
        if (actionMapWasEnabled)
        {
            action.actionMap.Disable();
        }

        string selectedPath = null;
        InputActionRebindingExtensions.RebindingOperation operation = null;
        operation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithActionEventNotificationsBeingSuppressed()
            .OnApplyBinding((_, path) => selectedPath = path)
            .OnCancel(_ => FinishInteractiveRebind(false, null, "按键绑定已取消。"))
            .OnComplete(_ => FinishInteractiveRebind(true, selectedPath, null));

        if (!string.IsNullOrWhiteSpace(requiredControlPath))
        {
            operation.WithControlsHavingToMatchPath(requiredControlPath.Trim());
        }

        if (!string.IsNullOrWhiteSpace(expectedControlType))
        {
            operation.WithExpectedControlType(expectedControlType.Trim());
        }

        _activeRebindOperation = operation;
        try
        {
            operation.Start();
        }
        catch (Exception exception)
        {
            _activeRebindOperation = null;
            operation.Dispose();
            if (actionMapWasEnabled)
            {
                action.actionMap.Enable();
            }

            error = $"无法开始按键绑定: {exception.Message}";
            return false;
        }

        error = null;
        return true;

        void FinishInteractiveRebind(bool succeeded, string path, string finishError)
        {
            if (_activeRebindOperation == operation)
            {
                _activeRebindOperation = null;
            }

            operation.Dispose();
            if (actionMapWasEnabled)
            {
                action.actionMap.Enable();
            }

            onFinished?.Invoke(succeeded, path, finishError);
        }
    }

    public void CancelActiveRebind()
    {
        if (_activeRebindOperation == null)
        {
            return;
        }

        _activeRebindOperation.Cancel();
    }

    public void SaveBindingOverrides()
    {
        SaveBindingOverrides(Player?.asset, PlayerBindingOverridesPrefsKey);
        SaveBindingOverrides(UI?.asset, UIBindingOverridesPrefsKey);
        PlayerPrefs.Save();
    }

    public static string FormatBindingPathForDisplay(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "未绑定";
        }

        string display = InputControlPath.ToHumanReadableString(
            path.Trim(),
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
        return string.IsNullOrWhiteSpace(display) ? path.Trim() : display;
    }

    private void LoadSavedBindingOverrides()
    {
        LoadBindingOverrides(Player?.asset, PlayerBindingOverridesPrefsKey);
        LoadBindingOverrides(UI?.asset, UIBindingOverridesPrefsKey);
    }

    private static void LoadBindingOverrides(InputActionAsset asset, string prefsKey)
    {
        if (asset == null || !PlayerPrefs.HasKey(prefsKey))
        {
            return;
        }

        string overridesJson = PlayerPrefs.GetString(prefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(overridesJson))
        {
            asset.LoadBindingOverridesFromJson(overridesJson);
        }
    }

    private static void SaveBindingOverrides(InputActionAsset asset, string prefsKey)
    {
        if (asset == null)
        {
            return;
        }

        PlayerPrefs.SetString(prefsKey, asset.SaveBindingOverridesAsJson());
    }

    private bool TryResolveBinding(
        string collectionName,
        string actionMapName,
        string actionName,
        string bindingName,
        int bindingIndexOverride,
        out InputAction action,
        out int bindingIndex,
        out string error)
    {
        action = null;
        bindingIndex = -1;

        if (!TryResolveActionAsset(collectionName, out InputActionAsset asset, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionMapName) || string.IsNullOrWhiteSpace(actionName))
        {
            error = "按键配置缺少 actionMap 或 action。";
            return false;
        }

        InputActionMap actionMap = asset.FindActionMap(actionMapName.Trim(), false);
        if (actionMap == null)
        {
            error = $"找不到 InputActionMap: {actionMapName}";
            return false;
        }

        action = actionMap.FindAction(actionName.Trim(), false);
        if (action == null)
        {
            error = $"找不到 InputAction: {actionMapName}/{actionName}";
            return false;
        }

        if (!TryResolveBindingIndex(action, bindingName, bindingIndexOverride, out bindingIndex))
        {
            error = $"找不到绑定: {actionMapName}/{actionName}/{bindingName}";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryResolveActionAsset(string collectionName, out InputActionAsset asset, out string error)
    {
        asset = null;
        string normalizedName = collectionName != null ? collectionName.Trim() : string.Empty;
        if (string.Equals(normalizedName, "Player", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedName, "PlayerControls", StringComparison.OrdinalIgnoreCase))
        {
            asset = Player?.asset;
        }
        else if (string.Equals(normalizedName, "UI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedName, "UIControls", StringComparison.OrdinalIgnoreCase))
        {
            asset = UI?.asset;
        }

        if (asset != null)
        {
            error = null;
            return true;
        }

        error = $"找不到 InputAction 集合: {collectionName}";
        return false;
    }

    private static bool TryResolveBindingIndex(
        InputAction action,
        string bindingName,
        int bindingIndexOverride,
        out int bindingIndex)
    {
        bindingIndex = -1;
        if (action == null)
        {
            return false;
        }

        if (bindingIndexOverride >= 0)
        {
            if (bindingIndexOverride < action.bindings.Count)
            {
                bindingIndex = bindingIndexOverride;
                return true;
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(bindingName))
        {
            string trimmedName = bindingName.Trim();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (string.Equals(action.bindings[i].name, trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    bindingIndex = i;
                    return true;
                }
            }

            return false;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (!action.bindings[i].isComposite)
            {
                bindingIndex = i;
                return true;
            }
        }

        return false;
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
