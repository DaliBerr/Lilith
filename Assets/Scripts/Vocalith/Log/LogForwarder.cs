
using UnityEngine;

namespace Vocalith.Logging
{
        /// <summary>
    /// summary: 负责把Unity原生Debug日志转发进自定义Log系统
    /// </summary>
    /// <returns>无</returns>
    public static class UnityLogForwarder
    {
        /// <summary>
        /// summary: 注册Unity日志回调，在首个场景加载前生效
        /// </summary>
        /// <returns>无</returns>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // 防止重复订阅（禁用域重载时有用）
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;
        }

        /// <summary>
        /// summary: Unity日志回调，将Debug日志转发到Log系统
        /// </summary>
        /// <param name="condition">Unity输出的日志内容</param>
        /// <param name="stackTrace">Unity提供的堆栈字符串</param>
        /// <param name="type">Unity日志类型(Log/Warning/Error等)</param>
        /// <returns>无</returns>
        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // ❗这里千万不要再调用 Debug.Log，否则会产生环形调用
            switch (type)
            {
                case LogType.Warning:
                    Log.Warn("Unity: {0}\n{1}", condition, stackTrace);
                    break;

                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    Log.Error("Unity: {0}\n{1}", condition, stackTrace);
                    break;

                default: // LogType.Log
                    Log.Info("Unity: {0}", condition);
                    break;
            }
        }
    }
}