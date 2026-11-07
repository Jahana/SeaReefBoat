using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XI.Bots
{
    public sealed class SwarmConfig
    {
        public Endpoints Endpoints = new Endpoints();
        public int Bots = 20;
        public int FirstIndex = 0;
        public string AccountPrefix = "xibot";      // accounts xibot000, xibot001, ...
        public string NamePrefix = "Xibot";         // chars Xibotaaa, Xibotaab, ...
        public string Password = "xibotpass";
        public int TickMs = 300;                    // the client's datagram cadence
        public int ReplyTimeoutMs = 1000;
        public int LoginWithinSeconds = 30;
        public int SoakSeconds = 60;                // walking about and chatter between the checks and the zone laps
        public int Laps = 1;                        // zone line round trips
        public int StartSpreadMs = 2000;            // the bots start within this window
        public Action<string> Log = Console.WriteLine;
    }

    // N bots at once, with rendezvous points so the checks that need everyone (seeing, chat, /sea) run when everyone
    // is there. A bot that fails a step it needs drops out and nobody waits for it.
    public sealed class Swarm
    {
        public readonly SwarmConfig Config;
        public readonly List<Bot> Bots = new List<Bot>();
        public readonly ConcurrentBag<double> RoundTrips = new ConcurrentBag<double>();
        public readonly ConcurrentBag<double> LobbyMs = new ConcurrentBag<double>();
        public readonly ConcurrentBag<double> ZoneMs = new ConcurrentBag<double>();
        private readonly Dictionary<string, HashSet<int>> _arrived = new Dictionary<string, HashSet<int>>();
        private readonly object _logLock = new object();
        private const int SyncLimit = 32;        // zone_entities.cpp CHARACTER_SYNC_LIMIT_MAX
        private const int SearchListLimit = 40;  // data_loader.cpp GetPlayersList: visibleResults < 40

        // the route a lap takes: from the home zone's line to the away zone and back by its line
        public sealed class Route
        {
            public ushort Home, Away;
            public string Out, Back;
            public (float X, float Y, float Z) OutLineNear, BackLineNear;
        }

        // West Ronfaure (100) <-> Northern San d'Oria (231): zone.yaml z2s2 / z6f2
        public static readonly Route Ronfaure = new Route
        {
            Home = 100, Away = 231, Out = "z2s2", Back = "z6f2",
            OutLineNear = (-162f, -65.5f, 378f), BackLineNear = (-246f, 4.6f, 46f),
        };

        public Swarm(SwarmConfig config)
        {
            Config = config;
            for (int i = 0; i < config.Bots; i++)
            {
                int n = config.FirstIndex + i;
                Bots.Add(new Bot(this, n, $"{config.AccountPrefix}{n:000}", config.Password, config.NamePrefix + Letters(n)));
            }
        }

        // 0 -> aaa, 1 -> aab, ...: char names are letters only
        public static string Letters(int n)
        {
            var c = new char[3];
            for (int i = 2; i >= 0; i--)
            {
                c[i] = (char)('a' + n % 26);
                n /= 26;
            }
            return new string(c);
        }

        public void Log(string s)
        {
            lock (_logLock) Config.Log?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] {s}");
        }

        // ---------------------------------------------------------------- rendezvous

        public async Task Meet(Bot b, string point, TimeSpan within, CancellationToken ct)
        {
            lock (_arrived)
            {
                if (!_arrived.TryGetValue(point, out var set)) _arrived[point] = set = new HashSet<int>();
                set.Add(b.Index);
            }
            bool all = await b.TickUntil(() => Everyone(point), within, ct);
            if (!all) b.Check($"rendezvous {point}", false, $"waiting for {string.Join(",", Missing(point))}");
        }

        private bool Everyone(string point)
        {
            lock (_arrived) return Bots.Where(x => x.Alive).All(x => _arrived[point].Contains(x.Index));
        }

        private IEnumerable<string> Missing(string point)
        {
            lock (_arrived) return Bots.Where(x => x.Alive && !_arrived[point].Contains(x.Index)).Select(x => x.CharName).ToList();
        }

        // ---------------------------------------------------------------- the phases

        private async Task Each(Func<Bot, Task> run, CancellationToken ct)
        {
            var tasks = Bots.Select(async b =>
            {
                await Task.Delay((b.Index - Config.FirstIndex) * Config.StartSpreadMs / Math.Max(1, Config.Bots), ct);
                try { await run(b); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { b.Fail("cancelled"); }
                catch (Exception e) { b.Fail($"{e.GetType().Name}: {e.Message}"); }
            }).ToList();
            await Task.WhenAll(tasks);
        }

        // accounts and chars through auth create and the lobby's 0x22 / 0x21
        public Task Create(CancellationToken ct) => Each(b => b.CreateAccountAndChar(ct), ct);

        // the whole session: login, the checks with everyone in zone, a soak, the zone laps, the logout
        public Task Play(CancellationToken ct) => Each(b => PlayOne(b, ct), ct);

        private async Task PlayOne(Bot b, CancellationToken ct)
        {
            b.Alive = true;
            await b.LogInToGame(ct);
            if (!b.Alive) return;
            var route = b.Zone == Ronfaure.Home ? Ronfaure : null;
            ushort home = b.Zone;
            Log($"[{b.CharName}] in zone {b.Zone} at ({b.X:0.0}, {b.Y:0.0}, {b.Z:0.0}) on {b.Map.Server}");
            await Meet(b, "in zone", TimeSpan.FromSeconds(Config.LoginWithinSeconds + 60), ct);

            // the others in the same zone: every 0x00D, or the nearest CHARACTER_SYNC_LIMIT_MAX (32) of them
            // (zone_entities.cpp SpawnPCs spawns no more for one char)
            var others = Bots.Where(o => o != b && o.Alive && o.Zone == home && o.CharId != 0).ToList();
            int sync = Math.Min(others.Count, SyncLimit);
            int Seen() => others.Count(o => b.SeenPcs.Contains(o.CharId));
            bool seen = await b.TickUntil(() => Seen() >= sync, TimeSpan.FromSeconds(30), ct);
            b.Check("sees the others (0x00D)", seen, $"{Seen()}/{others.Count}, {sync} expected");
            await Meet(b, "seen", TimeSpan.FromSeconds(60), ct);

            // say, shout, and a tell to the next bot round the ring
            var ring = Bots.Where(o => o.Alive && o.Zone == home).OrderBy(o => o.Index).ToList();
            int me = ring.IndexOf(b);
            var next = ring[(me + 1) % ring.Count];
            var prev = ring[(me + ring.Count - 1) % ring.Count];
            var peers = ring.Where(o => o != b).ToList();
            b.Say(0, $"say from {b.CharName}");
            b.Say(1, $"shout from {b.CharName}");
            if (next != b) b.Tell(next.CharName, $"tell from {b.CharName}");
            await Meet(b, "chatted", TimeSpan.FromSeconds(60), ct);
            bool Heard(byte kind, string from) { lock (b.Chat) return b.Chat.Any(c => c.Kind == kind && c.From == from); }
            bool chatOk = await b.TickUntil(() => (prev == b || Heard(3, prev.CharName)) && peers.All(o => Heard(0, o.CharName) && Heard(1, o.CharName)),
                                            TimeSpan.FromSeconds(20), ct);
            b.Check("tell received", prev == b || Heard(3, prev.CharName), $"from {prev.CharName}");
            b.Check("say received", peers.All(o => Heard(0, o.CharName)), $"{peers.Count(o => Heard(0, o.CharName))}/{peers.Count}");
            b.Check("shout received", peers.All(o => Heard(1, o.CharName)), $"{peers.Count(o => Heard(1, o.CharName))}/{peers.Count}");

            // an emote each
            b.Emote((byte)(1 + b.Index % 8));
            await Meet(b, "emoted", TimeSpan.FromSeconds(60), ct);
            bool emotes = await b.TickUntil(() => peers.All(o => b.EmotesFrom.Contains(o.CharId)), TimeSpan.FromSeconds(15), ct);
            b.Check("emotes seen (0x05A)", emotes, $"{peers.Count(o => b.EmotesFrom.Contains(o.CharId))}/{peers.Count}");

            // /sea in the zone, while the map session keeps ticking
            var search = SearchClient.SearchZone(Bot.IpText(b.Selection.SearchIp), b.Selection.SearchPort, home, (uint)(0x5EA00000 + b.Index), ct);
            await b.TickUntil(() => search.IsCompleted, TimeSpan.FromSeconds(15), ct);
            if (search.IsCompletedSuccessfully)
            {
                // every one of them, or 40 when there are more (data_loader.cpp GetPlayersList's visibleResults) and
                // the total of them all
                var names = search.Result.Select(f => f.Name).ToHashSet();
                var all = peers.Select(o => o.CharName).Append(b.CharName).ToHashSet();
                int listed = Math.Min(all.Count, SearchListLimit);
                b.Check("/sea finds the others", names.IsSubsetOf(all) && names.Count == listed && search.Result.Total == all.Count,
                        $"{names.Count(all.Contains)}/{all.Count} listed (+self {names.Contains(b.CharName)}), {listed} expected, total {search.Result.Total}");
            }
            else b.Check("/sea finds the others", false, search.Exception?.InnerException?.Message ?? "no answer within 15 s");
            await Meet(b, "searched", TimeSpan.FromSeconds(60), ct);

            // the soak: walking about, now and then a word or an emote
            var soakUntil = DateTime.UtcNow.AddSeconds(Config.SoakSeconds);
            var rng = new Random(b.Index);
            float cx = b.X, cy = b.Y, cz = b.Z;
            while (DateTime.UtcNow < soakUntil && b.Alive && b.InZone)
            {
                await b.Wander(cx, cy, cz, 12f, 1, ct);
                switch (rng.Next(6))
                {
                    case 0: b.Say(0, $"soak {b.CharName} {rng.Next(1000)}"); break;
                    case 1: b.Emote((byte)(1 + rng.Next(8))); break;
                    case 2: if (next != b) b.Tell(next.CharName, $"soak tell {rng.Next(1000)}"); break;
                }
                for (int i = 0; i < 1 + rng.Next(5); i++) await b.Tick(ct);
            }
            b.Check("in zone through the soak", b.Alive && b.InZone && b.ZoneOut == null, b.Failure ?? $"zone out {b.ZoneOut}");
            await Meet(b, "soaked", TimeSpan.FromSeconds(60), ct);

            // the zone laps
            if (route == null) b.Check("zone lap", true, $"no route from zone {home}: skipped");
            for (int lap = 0; route != null && lap < Config.Laps && b.Alive; lap++)
            {
                await b.WalkTo(route.OutLineNear.X, route.OutLineNear.Y, route.OutLineNear.Z, ct);
                if (!await b.TakeZoneLine(route.Out, route.Away, ct)) { b.Fail($"zone line {route.Out}"); break; }
                await Meet(b, $"away {lap + 1}", TimeSpan.FromSeconds(60), ct);
                // a char new to the city saw its arrival cutscene and stands where it put it: walk back to the line
                await b.WalkTo(route.BackLineNear.X, route.BackLineNear.Y, route.BackLineNear.Z, ct);
                await b.Wander(route.BackLineNear.X, route.BackLineNear.Y, route.BackLineNear.Z, 4f, 2, ct);
                b.Say(0, $"away {b.CharName}");
                await b.Tick(ct);
                if (!await b.TakeZoneLine(route.Back, route.Home, ct)) { b.Fail($"zone line {route.Back}"); break; }
                await Meet(b, $"home {lap + 1}", TimeSpan.FromSeconds(60), ct);
            }
            if (!b.Alive) return;

            await b.LogOut(ct);
        }

        // ---------------------------------------------------------------- the numbers

        public static double Percentile(IEnumerable<double> values, double p)
        {
            var v = values.OrderBy(x => x).ToList();
            if (v.Count == 0) return double.NaN;
            int i = (int)Math.Ceiling(p / 100.0 * v.Count) - 1;
            return v[Math.Clamp(i, 0, v.Count - 1)];
        }

        public string Summary()
        {
            var rt = RoundTrips.ToList();
            return $"round trips {rt.Count}: p50 {Percentile(rt, 50):0.0} ms, p99 {Percentile(rt, 99):0.0} ms, max {(rt.Count > 0 ? rt.Max() : double.NaN):0.0} ms; " +
                   $"lobby p50 {Percentile(LobbyMs, 50):0} ms; zone change p50 {Percentile(ZoneMs, 50):0} ms p99 {Percentile(ZoneMs, 99):0} ms; " +
                   $"timeouts {Bots.Sum(b => b.Map?.Timeouts ?? 0)}, undecipherable {Bots.Sum(b => b.Map?.BadDatagrams ?? 0)}";
        }
    }
}
