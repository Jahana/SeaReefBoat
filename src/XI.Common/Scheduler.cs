using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace XI.Common
{
    // Cancellation token for recurring or delayed tasks. Mirrors Scheduler::Token
    // (src/common/scheduler.h): stop() emits cancellation and is idempotent; the
    // underlying signal is released on stop/dispose, and destruction implies stop().
    public sealed class SchedulerToken : IDisposable
    {
        private CancellationTokenSource _cts;

        public SchedulerToken()
        {
            _cts = new CancellationTokenSource();
        }

        public CancellationToken Token => _cts?.Token ?? AlreadyCancelled;

        private static readonly CancellationTokenSource CancelledSource = CreateCancelled();
        private static CancellationTokenSource CreateCancelled()
        {
            var c = new CancellationTokenSource();
            c.Cancel();
            return c;
        }

        private static CancellationToken AlreadyCancelled => CancelledSource.Token;

        public void Stop()
        {
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts != null)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                cts.Dispose();
            }
        }

        public bool IsCancellationRequested => _cts == null || _cts.IsCancellationRequested;

        public void Dispose() => Stop();
    }

    // Pure interval-scheduling arithmetic, factored out of the wall-clock loop so it is
    // deterministic and testable. Reproduces intervalOnMainThread's grid exactly:
    //   deadline += period
    //   if (deadline < now) deadline = now   // overran a whole period -> resync, no burst
    public static class IntervalArithmetic
    {
        // Next deadline (in ticks) given current deadline and the observed "now" (ticks),
        // advancing by one period with the engine's overrun resync.
        public static long NextDeadline(long nowTicks, long deadlineTicks, long periodTicks)
        {
            var next = deadlineTicks + periodTicks;
            if (next < nowTicks)
            {
                next = nowTicks;
            }

            return next;
        }

        // The main-thread interval seeds its grid with a random phase in [0, durationMs]
        // (xirand::GetRandomNumber<int64_t>(0, durationMs)), so a batch of timers created
        // together does not fire in lockstep. This clamps a supplied raw value into range.
        public static long JitterPhaseTicks(long durationMs, long rawRandom)
        {
            if (durationMs <= 0)
            {
                return 0;
            }

            var r = rawRandom % (durationMs + 1);
            if (r < 0)
            {
                r += durationMs + 1;
            }

            return r; // milliseconds of phase
        }
    }

    // Functional C# shell of the ASIO Scheduler. Two execution contexts:
    //   * main context  : a single dedicated thread + work queue (zone/server logic affinity)
    //   * worker context : the .NET thread pool (offloaded/blocking work, e.g. DB per db::preparedStmt)
    // It preserves the engine's lazy-vs-eager split and cancellation semantics. Real wall-clock
    // timer firing is exercised in integration, not the offline gate; the deterministic
    // interval arithmetic lives in IntervalArithmetic and is covered there.
    public sealed class Scheduler : IDisposable
    {
        private readonly BlockingCollection<Action> _mainQueue = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
        private readonly Thread _mainThread;
        private readonly int _workerThreads;
        private long _closeRequested; // 0/1 via Interlocked

        public Scheduler()
        : this(DefaultWorkerThreads())
        {
        }

        public Scheduler(int workerThreads)
        {
            _workerThreads = Math.Max(1, workerThreads);
            _mainThread = new Thread(MainLoop) { IsBackground = true, Name = "XI.MainThread" };
            _mainThread.Start();
        }

        // std::max(1, hardware_concurrency - 1)
        public static int DefaultWorkerThreads()
        {
            var hc = Environment.ProcessorCount;
            return Math.Max(1, hc > 0 ? hc - 1 : 1);
        }

        public bool CloseRequested => Volatile.Read(ref _closeRequested) != 0;

        // Eager fire-and-forget on the main context (postToMainThread).
        public void PostToMain(Action action)
        {
            if (CloseRequested) return;
            _mainQueue.Add(() => Safe(action));
        }

        // Eager fire-and-forget on the worker pool (postToWorkerThread).
        public void PostToWorker(Action action)
        {
            if (CloseRequested) return;
            ThreadPool.QueueUserWorkItem(_ => Safe(action));
        }

        // Lazy: runs on the main context, returns a Task with the result (spawnOnMainThread).
        public Task<T> SpawnOnMain<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _mainQueue.Add(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            return tcs.Task;
        }

        // Lazy: runs on the worker pool, returns a Task (spawnOnWorkerThread; the db:: path).
        public Task<T> SpawnOnWorker<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            return tcs.Task;
        }

        // Runs once after the duration on the main context (delayOnMainThread). Returns a Token.
        public SchedulerToken Delay(TimeSpan duration, Action action)
        {
            var token = new SchedulerToken();
            var timer = new Timer(_ =>
            {
                if (!CloseRequested && !token.IsCancellationRequested)
                {
                    _mainQueue.Add(() => Safe(action));
                }
            }, null, Timeout.Infinite, Timeout.Infinite);
            token.Token.Register(() => timer.Dispose());
            timer.Change(duration, TimeSpan.FromMilliseconds(-1));
            return token;
        }

        // Repeats on a fixed period on the main context (intervalOnMainThread) with the
        // engine's random-phase seed and overrun-resync. Returns a Token.
        public SchedulerToken Interval(TimeSpan period, Action action, long? phaseMs = null)
        {
            var token = new SchedulerToken();
            var periodMs = (long)period.TotalMilliseconds;
            var phaseMsValue = phaseMs ?? IntervalArithmetic.JitterPhaseTicks(periodMs, Environment.TickCount64);
            Timer timer = null;
            timer = new Timer(_ =>
            {
                if (CloseRequested || token.IsCancellationRequested)
                {
                    timer?.Dispose();
                    return;
                }

                _mainQueue.Add(() => Safe(action));

                var now = DateTime.UtcNow.Ticks;
                var nextDeadline = IntervalArithmetic.NextDeadline(now, CurrentDeadline(token, period), periodMs * TimeSpan.TicksPerMillisecond);
                SetDeadline(token, nextDeadline);
                var due = new TimeSpan(Math.Max(0, nextDeadline - DateTime.UtcNow.Ticks));
                timer.Change(due, TimeSpan.FromMilliseconds(-1));
            }, null, Timeout.Infinite, Timeout.Infinite);

            var firstDue = new TimeSpan(phaseMsValue * TimeSpan.TicksPerMillisecond);
            SetDeadline(token, DateTime.UtcNow.Ticks + firstDue.Ticks);
            token.Token.Register(() => timer.Dispose());
            timer.Change(firstDue, TimeSpan.FromMilliseconds(-1));
            return token;
        }

        // Blocking run of the main event loop until stopped (Scheduler::run).
        public void Run()
        {
            while (!_mainQueue.IsCompleted)
            {
                try
                {
                    var work = _mainQueue.Take(CancellationToken.None);
                    work();
                }
                catch (InvalidOperationException)
                {
                    break; // queue completed
                }
            }
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
            {
                _mainQueue.CompleteAdding();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void MainLoop()
        {
            foreach (var work in _mainQueue.GetConsumingEnumerable())
            {
                try { work(); }
                catch
                {
                    // FatalOnException: a fire-and-forget exception must not kill the loop thread
                    // here; the port logs-and-continues. The C++ engine routes to std::terminate.
                }
            }
        }

        private static void Safe(Action a)
        {
            try { a(); }
            catch { /* swallow on the dispatcher; see FatalOnException note */ }
        }

        // deadline storage keyed by token identity (kept trivial: a field on the callback closure
        // is not possible with a plain Action, so we track it in a small conditional table).
        private static long CurrentDeadline(SchedulerToken token, TimeSpan period)
        {
            return _deadlines.TryGetValue(token, out var d) ? d : DateTime.UtcNow.Ticks;
        }

        private static void SetDeadline(SchedulerToken token, long ticks)
        {
            _deadlines[token] = ticks;
        }

        private static readonly ConcurrentDictionary<SchedulerToken, long> _deadlines = new ConcurrentDictionary<SchedulerToken, long>();
    }
}
