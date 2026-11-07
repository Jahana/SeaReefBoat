// Port of world/world_engine.cpp + world_application.cpp: wires the IPC router and world systems together and
// owns the two periodic jobs (time server tick, IPC queue pump). The scheduler is replaced by a plain loop
// (Run) so tests can drive Tick()/Pump() directly.

using System;
using System.Threading;
using XI.Persistence;

namespace XI.World
{
    public sealed class WorldEngine
    {
        public static readonly TimeSpan TimeServerTickInterval = TimeSpan.FromMilliseconds(2400); // kTimeServerTickInterval
        public static readonly TimeSpan IpcPumpInterval = TimeSpan.FromMilliseconds(100);         // kIPCPumpInterval

        public IpcServer Ipc { get; }
        public ConquestSystem Conquest { get; }
        public TimeServer Time { get; }

        public WorldEngine(IDatabase db, IWorldChannel channel, ZoneSettings zoneSettings, DailyTallySettings dailyTally,
                           Func<long> unixMs = null, Action<string> log = null)
        {
            IpcServer ipc = null;
            // The conquest system broadcasts through the router; the router dispatches conquest events to it.
            Conquest = new ConquestSystem(db, m => ipc.BroadcastMessage(m), log);
            var systems = new WorldSystems { Conquest = Conquest };
            Ipc = ipc = new IpcServer(db, channel, zoneSettings, systems, log);
            Time = new TimeServer(Conquest, db, dailyTally, unixMs, log);
        }

        public void Tick() => Time.Tick();
        public void Pump() => Ipc.HandleIncomingMessages();

        // Blocks until cancelled, running both intervals.
        public void Run(CancellationToken ct)
        {
            var nextTick = DateTime.UtcNow + TimeServerTickInterval;
            while (!ct.IsCancellationRequested)
            {
                Pump();
                if (DateTime.UtcNow >= nextTick)
                {
                    Tick();
                    nextTick += TimeServerTickInterval;
                }
                ct.WaitHandle.WaitOne(IpcPumpInterval);
            }
        }
    }
}
