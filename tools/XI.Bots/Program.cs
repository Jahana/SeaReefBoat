// XI.Bots -- bots that speak the real protocol to a running server, many at once: the auth port's JSON over TLS, the
// lobby's data and view sockets, the map's UDP session, the search server. See Swarm for what each bot does.
//
//   XI.Bots [phase=all|create|play] [key=value ...]
//     host=127.0.0.1 auth=55231 data=55230 view=55001   the lobby (what xiloader is pointed at); the map and search
//                                                       addresses come from the server
//     bots=20 first=0 accounts=xibot names=Xibot password=xibotpass
//     soak=60 laps=1 tick=300 timeout=1000 login=30 spread=2000
//
// The live server's ports (51220, 51240, 54001-54003, 54230, 54231) are refused, whatever the server says.
// Prints each bot's checks; the last line is "N passed, M failed" (exit 0 only when nothing failed).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using XI.Bots;

internal static class Program
{
    private static int Main(string[] args)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            if (eq > 0) o[a.Substring(0, eq)] = a.Substring(eq + 1);
            else
            {
                Console.WriteLine("usage: XI.Bots [phase=all|create|play] [host=.. auth=.. data=.. view=.. bots=N soak=s laps=n ...]");
                return 2;
            }
        }
        string Get(string k, string d) => o.TryGetValue(k, out var v) ? v : d;
        var cfg = new SwarmConfig
        {
            Endpoints = new Endpoints
            {
                Host = Get("host", "127.0.0.1"), Auth = int.Parse(Get("auth", "55231")),
                Data = int.Parse(Get("data", "55230")), View = int.Parse(Get("view", "55001")),
            },
            Bots = int.Parse(Get("bots", "20")), FirstIndex = int.Parse(Get("first", "0")),
            AccountPrefix = Get("accounts", "xibot"), NamePrefix = Get("names", "Xibot"), Password = Get("password", "xibotpass"),
            SoakSeconds = int.Parse(Get("soak", "60")), Laps = int.Parse(Get("laps", "1")), TickMs = int.Parse(Get("tick", "300")),
            ReplyTimeoutMs = int.Parse(Get("timeout", "1000")), LoginWithinSeconds = int.Parse(Get("login", "30")),
            StartSpreadMs = int.Parse(Get("spread", "2000")),
        };
        try { cfg.Endpoints.Validate(); }
        catch (InvalidOperationException e)
        {
            Console.WriteLine(e.Message);
            return 2;
        }

        string phase = Get("phase", "all");
        var swarm = new Swarm(cfg);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        if (phase is "all" or "create") swarm.Create(cts.Token).GetAwaiter().GetResult();
        if (phase is "all" or "play") swarm.Play(cts.Token).GetAwaiter().GetResult();

        int pass = 0, fail = 0;
        foreach (var b in swarm.Bots)
        {
            foreach (var (name, (ok, detail)) in b.Checks)
            {
                if (ok) pass++; else fail++;
                if (!ok) Console.WriteLine($"FAIL {b.CharName}: {name} {detail}");
            }
            if (b.Failure != null) Console.WriteLine($"     {b.CharName} stopped: {b.Failure}");
            b.Dispose();
        }
        Console.WriteLine(swarm.Summary());
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
