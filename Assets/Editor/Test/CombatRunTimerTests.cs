using Kernel.MapGrid;
using NUnit.Framework;

namespace Kernel.MapGrid.Editor.Tests
{
    public sealed class CombatRunTimerTests
    {
        [Test]
        public void TryStart_InitializesRunningTimer()
        {
            var timer = new CombatRunTimer();

            Assert.That(timer.TryStart(10d), Is.True);
            CombatRunTimerSnapshot snapshot = timer.GetSnapshot(12.5d);

            Assert.That(snapshot.HasStarted, Is.True);
            Assert.That(snapshot.IsRunning, Is.True);
            Assert.That(snapshot.StartedAtSeconds, Is.EqualTo(10d));
            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(2.5d).Within(0.0001d));
            Assert.That(snapshot.StopReason, Is.EqualTo(CombatRunTimerStopReason.None));
        }

        [Test]
        public void TryStart_WhenRunning_DoesNotResetStartTime()
        {
            var timer = new CombatRunTimer();

            Assert.That(timer.TryStart(5d), Is.True);
            Assert.That(timer.TryStart(20d), Is.False);

            CombatRunTimerSnapshot snapshot = timer.GetSnapshot(21d);
            Assert.That(snapshot.StartedAtSeconds, Is.EqualTo(5d));
            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(16d).Within(0.0001d));
        }

        [Test]
        public void TryStop_FreezesElapsedTimeAndReason()
        {
            var timer = new CombatRunTimer();
            timer.TryStart(3d);

            bool stopped = timer.TryStop(9.25d, CombatRunTimerStopReason.Victory, out CombatRunTimerSnapshot stoppedSnapshot);

            Assert.That(stopped, Is.True);
            Assert.That(stoppedSnapshot.IsRunning, Is.False);
            Assert.That(stoppedSnapshot.HasStopped, Is.True);
            Assert.That(stoppedSnapshot.StoppedAtSeconds, Is.EqualTo(9.25d));
            Assert.That(stoppedSnapshot.ElapsedSeconds, Is.EqualTo(6.25d).Within(0.0001d));
            Assert.That(stoppedSnapshot.StopReason, Is.EqualTo(CombatRunTimerStopReason.Victory));
            Assert.That(timer.GetSnapshot(30d).ElapsedSeconds, Is.EqualTo(6.25d).Within(0.0001d));
        }

        [Test]
        public void TryStop_WhenNotRunning_ReturnsCurrentSnapshot()
        {
            var timer = new CombatRunTimer();
            timer.TryStart(2d);
            timer.TryStop(7d, CombatRunTimerStopReason.PlayerDeath, out _);

            bool stoppedAgain = timer.TryStop(12d, CombatRunTimerStopReason.Victory, out CombatRunTimerSnapshot snapshot);

            Assert.That(stoppedAgain, Is.False);
            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(5d).Within(0.0001d));
            Assert.That(snapshot.StopReason, Is.EqualTo(CombatRunTimerStopReason.PlayerDeath));
        }

        [Test]
        public void Restart_ClearsPreviousStopResult()
        {
            var timer = new CombatRunTimer();
            timer.TryStart(1d);
            timer.TryStop(4d, CombatRunTimerStopReason.Cancelled, out _);

            timer.Restart(10d);
            CombatRunTimerSnapshot snapshot = timer.GetSnapshot(13d);

            Assert.That(snapshot.IsRunning, Is.True);
            Assert.That(snapshot.StartedAtSeconds, Is.EqualTo(10d));
            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(3d).Within(0.0001d));
            Assert.That(snapshot.StopReason, Is.EqualTo(CombatRunTimerStopReason.None));
        }

        [Test]
        public void Reset_ClearsTimerState()
        {
            var timer = new CombatRunTimer();
            timer.TryStart(1d);
            timer.TryStop(2d, CombatRunTimerStopReason.Victory, out _);

            timer.Reset();
            CombatRunTimerSnapshot snapshot = timer.GetSnapshot(100d);

            Assert.That(snapshot.HasStarted, Is.False);
            Assert.That(snapshot.IsRunning, Is.False);
            Assert.That(snapshot.ElapsedSeconds, Is.EqualTo(0d));
            Assert.That(snapshot.StopReason, Is.EqualTo(CombatRunTimerStopReason.None));
        }
    }
}
