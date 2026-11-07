using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XI.Common;

// Offline verifier for the Scheduler shell. Checks the deterministic pieces:
//   - IntervalArithmetic.NextDeadline  (the engine's deadline += period; if (< now) = now)
//   - IntervalArithmetic.JitterPhaseTicks (phase in [0, durationMs])
//   - SchedulerToken state machine (Stop idempotent, cancellation observed)
//   - two-context dispatch: main-thread affinity, worker offload, lazy Spawn vs eager Post, Delay
// The wall-clock *periodicity* of Interval over many cycles is a live-integration concern; the
// arithmetic that drives it is checked here.
internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static int Main()
    {
        CheckIntervalArithmetic();
        CheckTokenStateMachine();
        CheckDispatch();

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Check(string name, bool ok)
    {
        if (ok) _pass++;
        else { _fail++; Console.WriteLine("FAIL: " + name); }
    }

    private static void CheckEq<T>(string name, T actual, T expected)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected)) _pass++;
        else { _fail++; Console.WriteLine($"FAIL: {name} got '{actual}' want '{expected}'"); }
    }

    private static void CheckIntervalArithmetic()
    {
        const long periodTicks = 5000 * TimeSpan.TicksPerMillisecond; // 5s
        var baseDeadline = 1_000_000_000_000_000L;

        // On-time tick: now <= deadline+period -> plain advance.
        var nowOn = baseDeadline + periodTicks; // exactly at next deadline
        CheckEq("ia.ontime", IntervalArithmetic.NextDeadline(nowOn, baseDeadline, periodTicks), baseDeadline + periodTicks);

        // Slight early fire (now before the advanced deadline): stays on the grid.
        var nowEarly = baseDeadline + periodTicks / 2;
        CheckEq("ia.early", IntervalArithmetic.NextDeadline(nowEarly, baseDeadline, periodTicks), baseDeadline + periodTicks);

        // Overrun (now past deadline+period): resync to now, no burst.
        var nowLate = baseDeadline + 3 * periodTicks;
        CheckEq("ia.overrun_resync", IntervalArithmetic.NextDeadline(nowLate, baseDeadline, periodTicks), nowLate);

        // Exactly on the boundary: deadline+period == now -> not < now -> keeps grid value.
        var nowExact = baseDeadline + periodTicks;
        CheckEq("ia.boundary_eq", IntervalArithmetic.NextDeadline(nowExact, baseDeadline, periodTicks), nowExact);

        // Jitter phase always within [0, durationMs].
        for (long raw = -10; raw <= 10; raw++)
        {
            var p = IntervalArithmetic.JitterPhaseTicks(1000, raw);
            Check($"ia.jitter_range[{raw}]", p >= 0 && p <= 1000);
        }

        CheckEq("ia.jitter_zero_dur", IntervalArithmetic.JitterPhaseTicks(0, 42), 0L);
        // 25 % (10+1) = 25 % 11 = 3
        CheckEq("ia.jitter_wrap", IntervalArithmetic.JitterPhaseTicks(10, 25), 3L);
        // -1 % 11 = -1 -> +11 = 10
        CheckEq("ia.jitter_negative", IntervalArithmetic.JitterPhaseTicks(10, -1), 10L);
    }

    private static void CheckTokenStateMachine()
    {
        var t = new SchedulerToken();
        Check("token.live", !t.IsCancellationRequested);

        var ct = t.Token;
        t.Stop();
        Check("token.stopped", t.IsCancellationRequested);
        Check("token.ct_cancelled", ct.IsCancellationRequested);

        // Stop is idempotent and safe to call again (mirrors signal_.reset()).
        t.Stop();
        t.Stop();
        Check("token.stop_idempotent", t.IsCancellationRequested);

        // A stopped token's Token property reports cancelled (released signal path).
        Check("token.released_ct", t.Token.IsCancellationRequested);

        // Dispose implies stop (mirrors ~Token() { stop(); }).
        var t2 = new SchedulerToken();
        var ct2 = t2.Token;
        t2.Dispose();
        Check("token.dispose_stops", ct2.IsCancellationRequested);
    }

    private static void CheckDispatch()
    {
        using var sched = new Scheduler();

        // Eager post runs on the dedicated main thread.
        var mainHit = new ManualResetEventSlim(false);
        string mainThreadName = null;
        sched.PostToMain(() =>
        {
            mainThreadName = Thread.CurrentThread.Name;
            mainHit.Set();
        });
        Check("disp.main_executed", mainHit.Wait(2000));
        CheckEq("disp.main_thread_name", mainThreadName, "XI.MainThread");

        // Lazy spawn on main returns the value, executed on the main thread.
        var spawnMain = sched.SpawnOnMain(() => Thread.CurrentThread.Name);
        Check("disp.spawn_main_done", spawnMain.Wait(2000));
        CheckEq("disp.spawn_main_value", spawnMain.Result, "XI.MainThread");

        // Lazy spawn on worker does NOT run on the main thread (offloaded to pool).
        var spawnWorker = sched.SpawnOnWorker(() =>
        {
            Thread.Sleep(5);
            return 7 * 6;
        });
        Check("disp.spawn_worker_done", spawnWorker.Wait(2000));
        CheckEq("disp.spawn_worker_value", spawnWorker.Result, 42);

        // Eager post to worker executes off the main thread.
        var workerHit = new ManualResetEventSlim(false);
        string workerThreadName = null;
        sched.PostToWorker(() =>
        {
            workerThreadName = Thread.CurrentThread.Name;
            workerHit.Set();
        });
        Check("disp.worker_executed", workerHit.Wait(2000));
        Check("disp.worker_not_main", workerThreadName != "XI.MainThread");

        // Delay fires the action on the main thread after the duration.
        var delayHit = new ManualResetEventSlim(false);
        string delayThread = null;
        using var delayToken = sched.Delay(TimeSpan.FromMilliseconds(20), () =>
        {
            delayThread = Thread.CurrentThread.Name;
            delayHit.Set();
        });
        Check("disp.delay_fired", delayHit.Wait(2000));
        CheckEq("disp.delay_on_main", delayThread, "XI.MainThread");

        // A cancelled Delay must not fire.
        var cancelledHit = new ManualResetEventSlim(false);
        var cancelToken = sched.Delay(TimeSpan.FromMilliseconds(30), () => cancelledHit.Set());
        cancelToken.Stop();
        Check("disp.delay_cancelled", !cancelledHit.Wait(120));

        // CloseRequested reflects Stop().
        Check("disp.not_closed", !sched.CloseRequested);
        sched.Stop();
        Check("disp.closed_after_stop", sched.CloseRequested);

        // Posts after stop are dropped (no throw).
        var afterStop = new ManualResetEventSlim(false);
        try { sched.PostToMain(() => afterStop.Set()); } catch { /* tolerated */ }
        Check("disp.post_after_stop_dropped", !afterStop.Wait(80));
    }
}
