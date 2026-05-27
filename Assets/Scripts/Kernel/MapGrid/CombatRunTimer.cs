using System;

namespace Kernel.MapGrid
{
    /// <summary>
    /// 战斗计时停止原因。
    /// </summary>
    public enum CombatRunTimerStopReason
    {
        None = 0,
        Victory = 1,
        PlayerDeath = 2,
        Cancelled = 3,
        Manual = 4,
    }

    /// <summary>
    /// 战斗计时器的只读快照。
    /// </summary>
    public readonly struct CombatRunTimerSnapshot
    {
        public static CombatRunTimerSnapshot Empty { get; } = new(
            hasStarted: false,
            isRunning: false,
            startedAtSeconds: 0d,
            stoppedAtSeconds: 0d,
            elapsedSeconds: 0d,
            stopReason: CombatRunTimerStopReason.None);

        public CombatRunTimerSnapshot(
            bool hasStarted,
            bool isRunning,
            double startedAtSeconds,
            double stoppedAtSeconds,
            double elapsedSeconds,
            CombatRunTimerStopReason stopReason)
        {
            HasStarted = hasStarted;
            IsRunning = isRunning;
            StartedAtSeconds = SanitizeSeconds(startedAtSeconds);
            StoppedAtSeconds = SanitizeSeconds(stoppedAtSeconds);
            ElapsedSeconds = SanitizeSeconds(elapsedSeconds);
            StopReason = stopReason;
        }

        public bool HasStarted { get; }
        public bool IsRunning { get; }
        public bool HasStopped => HasStarted && !IsRunning && StopReason != CombatRunTimerStopReason.None;
        public double StartedAtSeconds { get; }
        public double StoppedAtSeconds { get; }
        public double ElapsedSeconds { get; }
        public TimeSpan Elapsed => TimeSpan.FromSeconds(ElapsedSeconds);
        public CombatRunTimerStopReason StopReason { get; }

        private static double SanitizeSeconds(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;
        }
    }

    /// <summary>
    /// 保存单局战斗计时状态；迁移期由外部显式调用开始和停止 API。
    /// </summary>
    public sealed class CombatRunTimer
    {
        private double startedAtSeconds;
        private double stoppedAtSeconds;
        private double stoppedElapsedSeconds;
        private bool hasStarted;
        private bool isRunning;
        private CombatRunTimerStopReason stopReason;

        public bool HasStarted => hasStarted;
        public bool IsRunning => isRunning;
        public CombatRunTimerStopReason StopReason => stopReason;

        /// <summary>
        /// summary: 尝试从当前时间开始计时；已经运行时不会重置起点。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 成功进入运行态时返回 true
        /// </summary>
        public bool TryStart(double currentTimeSeconds)
        {
            if (isRunning)
            {
                return false;
            }

            startedAtSeconds = SanitizeSeconds(currentTimeSeconds);
            stoppedAtSeconds = 0d;
            stoppedElapsedSeconds = 0d;
            hasStarted = true;
            isRunning = true;
            stopReason = CombatRunTimerStopReason.None;
            return true;
        }

        /// <summary>
        /// summary: 重新开始计时，并清除上一次停止结果。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 无
        /// </summary>
        public void Restart(double currentTimeSeconds)
        {
            startedAtSeconds = SanitizeSeconds(currentTimeSeconds);
            stoppedAtSeconds = 0d;
            stoppedElapsedSeconds = 0d;
            hasStarted = true;
            isRunning = true;
            stopReason = CombatRunTimerStopReason.None;
        }

        /// <summary>
        /// summary: 尝试停止正在运行的计时器，并输出停止后的冻结快照。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// param name="reason": 本次停止原因
        /// param name="snapshot": 停止后或当前已有的计时快照
        /// returns: 本次确实把运行态切到停止态时返回 true
        /// </summary>
        public bool TryStop(
            double currentTimeSeconds,
            CombatRunTimerStopReason reason,
            out CombatRunTimerSnapshot snapshot)
        {
            if (!isRunning)
            {
                snapshot = GetSnapshot(currentTimeSeconds);
                return false;
            }

            stoppedAtSeconds = SanitizeSeconds(currentTimeSeconds);
            stoppedElapsedSeconds = Math.Max(0d, stoppedAtSeconds - startedAtSeconds);
            isRunning = false;
            stopReason = NormalizeStopReason(reason);
            snapshot = GetSnapshot(stoppedAtSeconds);
            return true;
        }

        /// <summary>
        /// summary: 清空计时状态，回到尚未开始的初始状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void Reset()
        {
            startedAtSeconds = 0d;
            stoppedAtSeconds = 0d;
            stoppedElapsedSeconds = 0d;
            hasStarted = false;
            isRunning = false;
            stopReason = CombatRunTimerStopReason.None;
        }

        /// <summary>
        /// summary: 获取当前计时快照；运行中会按传入时间计算实时经过秒数，停止后保持冻结值。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 当前计时快照
        /// </summary>
        public CombatRunTimerSnapshot GetSnapshot(double currentTimeSeconds)
        {
            if (!hasStarted)
            {
                return CombatRunTimerSnapshot.Empty;
            }

            double elapsedSeconds = isRunning
                ? Math.Max(0d, SanitizeSeconds(currentTimeSeconds) - startedAtSeconds)
                : stoppedElapsedSeconds;

            return new CombatRunTimerSnapshot(
                hasStarted,
                isRunning,
                startedAtSeconds,
                stoppedAtSeconds,
                elapsedSeconds,
                stopReason);
        }

        /// <summary>
        /// summary: 读取当前已经过的秒数。
        /// param name="currentTimeSeconds": 当前逻辑时间，单位秒
        /// returns: 当前已经过秒数
        /// </summary>
        public double GetElapsedSeconds(double currentTimeSeconds)
        {
            return GetSnapshot(currentTimeSeconds).ElapsedSeconds;
        }

        private static CombatRunTimerStopReason NormalizeStopReason(CombatRunTimerStopReason reason)
        {
            return reason == CombatRunTimerStopReason.None ? CombatRunTimerStopReason.Manual : reason;
        }

        private static double SanitizeSeconds(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;
        }
    }
}
