#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// summary: 在点击播放时先执行一次手动刷新；若有脚本改动则等待编译完成，再自动进入播放模式。
/// param: 无。
/// return: 无。
/// </summary>
[InitializeOnLoad]
public static class CompileBeforePlay
{
    private const string PendingPlayKey = "CompileBeforePlay.PendingPlay";
    private const string SkipNextInterceptKey = "CompileBeforePlay.SkipNextIntercept";
    private const string PreparingPlayKey = "CompileBeforePlay.PreparingPlay";

    /// <summary>
    /// summary: 编辑器加载时注册相关事件。
    /// param: 无。
    /// return: 无。
    /// </summary>
    static CompileBeforePlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    /// <summary>
    /// summary: 在即将进入播放模式时，先拦截一次播放并执行手动刷新。
    /// param: state，播放模式切换状态。
    /// return: 无。
    /// </summary>
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        // 编译完成后自动重新进入播放时，放行这一次
        if (SessionState.GetBool(SkipNextInterceptKey, false))
        {
            SessionState.SetBool(SkipNextInterceptKey, false);
            return;
        }

        // 已经在处理“播放前刷新/编译”了，避免重复触发
        if (SessionState.GetBool(PreparingPlayKey, false))
            return;

        SessionState.SetBool(PreparingPlayKey, true);
        SessionState.SetBool(PendingPlayKey, true);

        // 取消这次立即进入播放
        EditorApplication.isPlaying = false;

        // 可选：先保存一下资源
        AssetDatabase.SaveAssets();

        // 关键：手动刷新资源数据库
        // 如果有脚本改动，Unity 会在这里自动触发重新编译
        AssetDatabase.Refresh();

        UnityEngine.Debug.Log("CompileBeforePlay：已在进入播放前触发手动刷新。");
    }

    /// <summary>
    /// summary: 持续检查刷新和编译是否完成，完成后自动重新进入播放模式。
    /// param: 无。
    /// return: 无。
    /// </summary>
    private static void OnEditorUpdate()
    {
        if (!SessionState.GetBool(PendingPlayKey, false))
            return;

        // 只要还在刷新或编译，就继续等待
        if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            return;

        SessionState.SetBool(PendingPlayKey, false);
        SessionState.SetBool(PreparingPlayKey, false);
        SessionState.SetBool(SkipNextInterceptKey, true);

        EditorApplication.isPlaying = true;
    }
}
#endif