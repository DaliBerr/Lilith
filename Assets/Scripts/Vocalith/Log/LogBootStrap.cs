
using System.IO;
using UnityEngine;

namespace Vocalith.Logging
{
    /// <summary>
    /// summary: 游戏启动前初始化日志系统（配置输出目标）
    /// </summary>
    /// <returns>无</returns>
    public static class LogBootstrap
    {
        private static bool _initialized;
        private static readonly object InitGate = new();

        /// <summary>
        /// summary: 获取统一的默认日志文件路径。
        /// </summary>
        /// <returns>persistentDataPath/Logs/game.log。</returns>
        public static string GetDefaultLogPath()
        {
            return Path.Combine(Application.persistentDataPath, "Logs", "game.log");
        }

        /// <summary>
        /// summary: 确保日志系统只初始化一次。
        /// </summary>
        /// <param name="includeConsoleSink">是否附加控制台输出。</param>
        /// <returns>无。</returns>
        public static void EnsureInitialized(bool includeConsoleSink = true)
        {
            lock (InitGate)
            {
                if (_initialized)
                {
                    return;
                }

                Log.MinLevel = LogLevel.Debug;

                if (includeConsoleSink)
                {
                    Log.AddSink(new ConsoleSink());
                }

                Log.AddSink(new FileSink(GetDefaultLogPath()));
                _initialized = true;
            }
        }

        /// <summary>
        /// summary: 在加载首个场景前设置日志等级和输出目标
        /// </summary>
        /// <returns>无</returns>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            EnsureInitialized();

            // ❗注意：这里**不再**添加 UnitySink，
            // 这样 Log 的输出不会再回到 Unity Console，避免抢占双击跳转。
        }
    }
}
