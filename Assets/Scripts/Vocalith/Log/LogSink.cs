// File: YourStudio.Verse/Core/LogSinks.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Vocalith;
using UnityEngine;
namespace Vocalith.Logging
{
    public sealed class ConsoleSink : ILogSink
    {
        public void Emit(in LogEvent e)
        {
            var sb = FormatCommon(e);
            if (e.Exception != null) sb.AppendLine().Append(e.Exception);
            Console.WriteLine(sb.ToString());
        }

        private static StringBuilder FormatCommon(in LogEvent e)
        {
            var t = e.UtcTime.ToLocalTime().ToString("HH:mm:ss.fff");
            var sb = new StringBuilder(256);
            sb.Append('[').Append(t).Append("] [").Append(e.Level).Append(']');
            if (!string.IsNullOrEmpty(e.Category)) sb.Append(" [").Append(e.Category).Append(']');
            if (e.Scope is { Count: >0 }) {
                sb.Append(" {");
                bool first = true;
                foreach (var kv in e.Scope) {
                    if (!first) sb.Append(", ");
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                    first = false;
                }
                sb.Append('}');
            }
            sb.Append(' ').Append(e.Message)
              .Append("  <").Append(Path.GetFileName(e.File)).Append(':').Append(e.Line)
              .Append(" @").Append(e.Member).Append(" T").Append(e.ThreadId).Append('>');
            return sb;
        }
    }

    // 仅在 Unity 下可用（防止 Editor 外部编译器报错）
#if UNITY_EDITOR
    
    public sealed class UnitySink : ILogSink
    {
        public void Emit(in LogEvent e)
        {
            string msg = $"[{e.Level}] {e.Message}";
            if (e.Scope is { Count: >0 }) msg += " " + string.Join(", ", e.Scope);
            if (e.Exception != null) msg += "\n" + e.Exception;
            switch (e.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info: Debug.Log(msg); break;
                case LogLevel.Warn: Debug.LogWarning(msg); break;
                default: Debug.LogError(msg); break;
            }
        }
    }
#endif

    public sealed class FileSink : ILogSink, IDisposable
    {
        private const int MaxRetainedEntries = 10;
        private static readonly Regex EntryHeaderRegex = new(
            @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[[^\]]+\]",
            RegexOptions.Compiled);

        private readonly object _gate = new();
        private readonly string _path;
        private readonly long _rollSizeBytes;
        private readonly List<string> _entries;

        public FileSink(string path, long rollSizeBytes = 8 * 1024 * 1024)
        {
            _path = path;
            _rollSizeBytes = rollSizeBytes;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _entries = LoadExistingEntries(path);
            TrimEntries();
            PersistEntries();
        }

        public void Emit(in LogEvent e)
        {
            var t = e.UtcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
            var cat = string.IsNullOrEmpty(e.Category) ? "" : $" [{e.Category}]";
            var scope = (e.Scope is { Count: >0 }) ? " {" + string.Join(", ", e.Scope) + "}" : "";
            string line = $"{t} [{e.Level}]{cat}{scope} {e.Message} <{Path.GetFileName(e.File)}:{e.Line}@{e.Member}>\n";
            if (e.Exception != null) line += e.Exception + "\n";
            lock (_gate)
            {
                _entries.Add(line);
                TrimEntries();
                PersistEntries();
                TryRoll();
            }
        }

        public void Flush() { }

        /// <summary>
        /// summary: 读取并按日志头切分已有日志条目。
        /// </summary>
        /// <param name="path">日志文件路径。</param>
        /// <returns>按时间顺序排列的日志条目列表。</returns>
        private static List<string> LoadExistingEntries(string path)
        {
            var entries = new List<string>();
            if (!File.Exists(path))
            {
                return entries;
            }

            using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var current = new StringBuilder();

            while (reader.ReadLine() is { } line)
            {
                if (IsEntryHeader(line) && current.Length > 0)
                {
                    entries.Add(current.ToString());
                    current.Clear();
                }

                current.Append(line).Append('\n');
            }

            if (current.Length > 0)
            {
                entries.Add(current.ToString());
            }

            return entries;
        }

        /// <summary>
        /// summary: 判断一行文本是否为日志条目的起始头。
        /// </summary>
        /// <param name="line">待检查的日志行。</param>
        /// <returns>是条目头时返回 true。</returns>
        private static bool IsEntryHeader(string line)
        {
            return EntryHeaderRegex.IsMatch(line);
        }

        /// <summary>
        /// summary: 将内存中的日志条目裁剪到最近的固定数量。
        /// </summary>
        /// <returns>无。</returns>
        private void TrimEntries()
        {
            if (_entries.Count <= MaxRetainedEntries)
            {
                return;
            }

            _entries.RemoveRange(0, _entries.Count - MaxRetainedEntries);
        }

        /// <summary>
        /// summary: 将当前条目列表完整写回日志文件。
        /// </summary>
        /// <returns>无。</returns>
        private void PersistEntries()
        {
            using var writer = new StreamWriter(new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                NewLine = "\n"
            };

            foreach (var entry in _entries)
            {
                writer.Write(entry);
            }
        }

        private void TryRoll()
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var fileInfo = new FileInfo(_path);
            if (fileInfo.Length < _rollSizeBytes)
            {
                return;
            }

            string rolled = _path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (File.Exists(rolled))
            {
                File.Delete(rolled);
            }

            File.Move(_path, rolled);
            PersistEntries();
        }

        public void Dispose() { }
    }
}
