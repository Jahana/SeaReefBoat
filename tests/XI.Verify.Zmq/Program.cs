using System;
using System.Diagnostics;
using System.Threading;
using XI.World;

// XI.Verify.Zmq -- loopback gate for ZmqRouterChannel <-> ZmqDealerChannel (routing id == IPP raw, 2-frame router
// messages, bare dealer payloads). Exit 0 pass, 1 fail.
internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static bool WaitFor<T>(Func<(bool, T)> poll, out T value, int ms = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms)
        {
            var (ok, v) = poll();
            if (ok) { value = v; return true; }
            Thread.Sleep(5);
        }
        value = default;
        return false;
    }

    private static int Main()
    {
        const string ep = "tcp://127.0.0.1:38555";
        using var router = new ZmqRouterChannel(ep);
        Check("router bound", true, router.Opened);

        var mapA = new Ipp(0x0100007F, 54230);
        var mapB = new Ipp(0x0200007F, 54231);
        using var a = new ZmqDealerChannel(ep, mapA);
        using var b = new ZmqDealerChannel(ep, mapB);

        a.Send(new byte[] { 1, 2, 3 });
        Check("A->router received", true, WaitFor(() => (router.TryReceive(out var m), m), out IppMessage ma));
        Check("A ipp", mapA.ToString(), ma.Ipp.ToString());
        Check("A raw", mapA.Raw, ma.Ipp.Raw);
        Check("A payload", "1,2,3", string.Join(",", ma.Payload));

        b.Send(new byte[] { 9 });
        Check("B->router received", true, WaitFor(() => (router.TryReceive(out var m), m), out IppMessage mb));
        Check("B ipp", mapB.ToString(), mb.Ipp.ToString());

        router.Send(new IppMessage(mapB, new byte[] { 7, 7 }));
        Check("router->B", true, WaitFor(() => (b.TryReceive(out var p), p), out byte[] pb));
        Check("B payload", "7,7", string.Join(",", pb));
        Thread.Sleep(100);
        Check("A got nothing", false, a.TryReceive(out _));

        var big = new byte[100_000];
        new Random(1).NextBytes(big);
        a.Send(big);
        Check("big payload", true, WaitFor(() => (router.TryReceive(out var m), m), out IppMessage mbig));
        Check("big equal", true, mbig.Payload.AsSpan().SequenceEqual(big));

        router.Send(new IppMessage(new Ipp(0x0900007F, 1), new byte[] { 1 })); // unroutable: dropped, no throw
        Check("unroutable send ok", true, true);

        using var dup = new ZmqRouterChannel(ep);
        Check("second bind fails gracefully", false, dup.Opened);

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }
}
