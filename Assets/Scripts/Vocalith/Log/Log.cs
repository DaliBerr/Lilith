
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Vocalith.Logging
{
    public enum LogLevel { Trace=0, Debug=1, Info=2, Warn=3, Error=4, Fatal=5 }

    public readonly struct LogEvent
    {
        public readonly DateTime UtcTime;
        public readonly LogLevel Level;
        public readonly string Message;
        public readonly Exception? Exception;
        public readonly string? Category;
        public readonly string Member;
        public readonly string File;
        public readonly int Line;
        public readonly int ThreadId;
        public readonly IReadOnlyDictionary<string, object>? Scope; // 结构化上下文

        public LogEvent(
            DateTime utc, LogLevel lvl, string msg, Exception? ex,
            string? cat, string member, string file, int line,
            int threadId, IReadOnlyDictionary<string, object>? scope)
        {
            UtcTime = utc; Level = lvl; Message = msg; Exception = ex;
            Category = cat; Member = member; File = file; Line = line;
            ThreadId = threadId; Scope = scope;
        }
    }

    public interface ILogSink
    {
        // 注意：可能在任意线程被调用；实现时自行做线程安全。
        void Emit(in LogEvent e);
        // 可选：关闭/刷新等资源收尾
        void Flush() { }
    }

    public static class Log
    {
        private static readonly object _gate = new();
        private static readonly List<ILogSink> _sinks = new();
        private static volatile LogLevel _minLevel = LogLevel.Info;

        // 去抖/合并：相同key在窗口内只首条输出，其余计数累加
        private static readonly ConcurrentDictionary<string, DedupEntry> _dedup = new();
        private struct DedupEntry { public DateTime last; public int suppressed; }

        // AsyncLocal 作用域上下文
        private static readonly AsyncLocal<ScopeStack?> _scope = new();
        private sealed class ScopeStack
        {
            public readonly ScopeStack? Parent;
            public readonly string Key; public readonly object Value;
            public ScopeStack(ScopeStack? parent, string key, object value)
            { Parent = parent; Key = key; Value = value; }
        }

        // 最近N条环缓冲（方便游戏内控制台/上报）
        private static readonly int _ringCap = 512;
        private static readonly LogEvent[] _ring = new LogEvent[_ringCap];
        private static int _ringWriteIdx = 0; // 原子递增取模即可

        public static LogLevel MinLevel { get => _minLevel; set => _minLevel = value; }

        public static void AddSink(ILogSink sink)
        {
            lock (_gate) _sinks.Add(sink);
        }
        public static void RemoveSink(ILogSink sink)
        {
            lock (_gate) _sinks.Remove(sink);
        }
        public static void FlushAll()
        {
            lock (_gate) foreach (var s in _sinks) s.Flush();
        }

        // —— 快捷方法（Trace/Debug 仅在编译或等级允许时输出）——
        [Conditional("DEBUG")]
        public static void Trace(string msg, params object[] args)
            => Write(LogLevel.Trace, null, null, msg, args);
        [Conditional("DEBUG")]
        public static void Debug(string msg, params object[] args)
            => Write(LogLevel.Debug, null, null, msg, args);

        public static void Info(string msg, params object[] args)
            => Write(LogLevel.Info, null, null, msg, args);
        public static void Warn(string msg, params object[] args)
            => Write(LogLevel.Warn, null, null, msg, args);
        public static void Error(string msg, params object[] args)
            => Write(LogLevel.Error, null, null, msg, args);
        public static void Fatal(string msg, params object[] args)
            => Write(LogLevel.Fatal, null, null, msg, args);

        public static void Exception(Exception ex, string? msg = null, params object[] args)
            => Write(LogLevel.Error, ex, null, msg ?? ex.Message, args);

        public static void Category(LogLevel level, string category, string msg, params object[] args)
            => Write(level, null, category, msg, args);

        public static void Assert(bool condition, string msgWhenFail, params object[] args)
        {
            if (!condition) Write(LogLevel.Error, null, "ASSERT", msgWhenFail, args);
        }

        // 去抖：同 key 在 interval 内只打印一次，其余计数；窗口结束时补一条汇总
        public static void InfoDedup(string key, TimeSpan interval, string msg, params object[] args)
            => WriteDedup(LogLevel.Info, key, interval, null, msg, args);

        // —— 作用域（结构化上下文）——
        public static IDisposable BeginScope(string key, object value)
        {
            var current = _scope.Value;
            _scope.Value = new ScopeStack(current, key, value);
            return new ScopeToken();
        }
        private sealed class ScopeToken : IDisposable
        {
            public void Dispose()
            {
                var curr = _scope.Value;
                if (curr != null) _scope.Value = curr.Parent;
            }
        }

        // —— 核心写入 —— //
        private static void Write(
            LogLevel level, Exception? ex, string? category,
            string msg, object[]? args,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (level < _minLevel) return;

            string formatted = (args is { Length: >0 }) ? SafeFormat(msg, args) : msg;
            var now = DateTime.UtcNow;

            // 收集作用域为字典（平铺 Key=Value，后写覆盖先写）
            var dict = BuildScopeDictionary();

            var ev = new LogEvent(
                now, level, formatted, ex, category,
                member, file, line, Thread.CurrentThread.ManagedThreadId, dict);

            // 写入环缓冲
            int idx = Interlocked.Increment(ref _ringWriteIdx);
            _ring[idx % _ringCap] = ev;

            // 广播给所有 sinks（做快照，避免锁内IO）
            ILogSink[] snapshot;
            lock (_gate) snapshot = _sinks.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i].Emit(ev);
        }

        private static void WriteDedup(
            LogLevel level, string key, TimeSpan interval, Exception? ex, string msg, params object[] args)
        {
            var now = DateTime.UtcNow;
            var entry = _dedup.AddOrUpdate(key,
                addValueFactory: _ => new DedupEntry { last = DateTime.MinValue, suppressed = 0 },
                updateValueFactory: (_, old) => old);

            var due = entry.last + interval;
            if (now >= due) {
                // 窗口已过：如果之前有抑制条数，先补一条汇总
                if (entry.suppressed > 0)
                    Write(level, null, "DE-DUP", $"[{key}] suppressed {entry.suppressed} similar messages in last {interval.TotalSeconds:0.#}s", Array.Empty<object>());

                // 输出当前
                Write(level, ex, null, msg, args);
                _dedup[key] = new DedupEntry { last = now, suppressed = 0 };
            } else {
                // 抑制，累加计数
                _dedup.AddOrUpdate(key,
                    _ => new DedupEntry { last = now, suppressed = 1 },
                    (_, old) => new DedupEntry { last = old.last, suppressed = old.suppressed + 1 });
            }
        }

        private static string SafeFormat(string fmt, object[] args)
        {
            try { return string.Format(fmt, args); }
            catch { return fmt + " " + string.Join(" | ", args); }
        }

        private static IReadOnlyDictionary<string, object>? BuildScopeDictionary()
        {
            var s = _scope.Value;
            if (s == null) return null;
            // 从根到叶汇总，后覆盖先
            var stack = new Stack<(string, object)>();
            for (var p = s; p != null; p = p.Parent) stack.Push((p.Key, p.Value));
            var dict = new Dictionary<string, object>(stack.Count);
            foreach (var (k,v) in stack) dict[k] = v;
            return dict;
        }

        // —— 获取环缓冲快照（例如游戏内控制台）——
        public static LogEvent[] SnapshotRecent(int maxCount = 128)
        {
            var buf = new List<LogEvent>(System.Math.Min(maxCount, _ringCap));
            int w = Volatile.Read(ref _ringWriteIdx);
            int count = System.Math.Min(maxCount, System.Math.Min(w, _ringCap));
            for (int i = count - 1; i >= 0; i--) // 逆序：最新→最旧
            {
                int idx = (w - i) % _ringCap; if (idx < 0) idx += _ringCap;
                buf.Add(_ring[idx]);
            }
            return buf.ToArray();
        }
    }
}
