#nullable enable
using System.Diagnostics;
using UnityEngine;

namespace Vocalith.Logging
{
    /// <summary>
    /// summary: 游戏内统一使用的调试日志封装
    /// return: 无
    /// </summary>
    public static class GameDebug
    {
        /// <summary>
        /// summary: 输出普通调试日志，仅在编辑器或开发版中生效
        /// param: message 要输出的内容
        /// return: 无
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void Log(object? message)
        {
            UnityEngine.Debug.Log(message);
        }

        /// <summary>
        /// summary: 输出格式化调试日志，仅在编辑器或开发版中生效
        /// param: format 格式化字符串
        /// param: args   参数列表
        /// return: 无
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void LogFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogFormat(format, args);
        }

        /// <summary>
        /// summary: 输出警告调试日志，仅在编辑器或开发版中生效
        /// param: message 要输出的内容
        /// return: 无
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void LogWarning(object? message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        /// <summary>
        /// summary: 输出格式化警告日志，仅在编辑器或开发版中生效
        /// param: format 格式化字符串
        /// param: args   参数列表
        /// return: 无
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void LogWarningFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogWarningFormat(format, args);
        }

        /// <summary>
        /// summary: 输出错误调试日志，仅在编辑器或开发版中生效
        /// param: message 要输出的内容
        /// return: 无
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void LogError(object? message)
        {
            UnityEngine.Debug.LogError(message);
        }

        /// <summary>
        /// summary: 输出格式化错误日志，仅在编辑器或开发版中生效
        /// param: format 格式化字符串
        /// param: args   参数列表
        /// return: 无
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [HideInCallstack]
        public static void LogErrorFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }
    }
}
