using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using XI.Common;

// Byte-for-byte diff of the C# VanaTime port against the compiled chrono oracle
// (csharp/oracle/vana_golden.txt). Each 'V' line is an Earth-seconds timestamp and the
// engine's authoritative accessor outputs; we recompute every field from `earth` and compare.
internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static readonly Regex Kv = new Regex(@"(\w+)=(\S+)");

    private static int Main(string[] argv)
    {
        var goldenPath = argv.Length > 0
            ? argv[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "oracle", "vana_golden.txt"));

        if (!File.Exists(goldenPath))
        {
            Console.WriteLine($"MISSING golden file: {goldenPath}");
            return 2;
        }

        var lines = File.ReadAllLines(goldenPath);
        var vectors = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("META"))
            {
                CheckMeta(line);
                continue;
            }

            if (!line.StartsWith("V "))
            {
                continue;
            }

            vectors++;
            CheckVector(line);
        }

        Console.WriteLine($"RESULT: {_pass} passed, {_fail} failed (vectors: {vectors})");
        return _fail == 0 ? 0 : 1;
    }

    private static Dictionary<string, string> ParseKv(string line)
    {
        var d = new Dictionary<string, string>();
        foreach (Match m in Kv.Matches(line))
        {
            d[m.Groups[1].Value] = m.Groups[2].Value;
        }

        return d;
    }

    private static long L(Dictionary<string, string> d, string k) => long.Parse(d[k], CultureInfo.InvariantCulture);
    private static ulong UL(Dictionary<string, string> d, string k) => ulong.Parse(d[k], CultureInfo.InvariantCulture);

    private static void Check(string name, bool ok)
    {
        if (ok) _pass++;
        else { _fail++; Console.WriteLine("FAIL: " + name); }
    }

    private static void CheckMeta(string line)
    {
        var d = ParseKv(line);
        var epoch = UL(d, "epoch");
        Check("meta.epoch", epoch == (ulong)VanaTime.EpochUnixSec);
        // Unit earth-second lengths (day=3456, week=27648, month=103680, year=1244160).
        Check("meta.day", Math.Abs(double.Parse(d["day"], CultureInfo.InvariantCulture) - 3456.0) < 1e-6);
        Check("meta.week", Math.Abs(double.Parse(d["week"], CultureInfo.InvariantCulture) - 27648.0) < 1e-6);
        Check("meta.month", Math.Abs(double.Parse(d["month"], CultureInfo.InvariantCulture) - 103680.0) < 1e-6);
        Check("meta.year", Math.Abs(double.Parse(d["year"], CultureInfo.InvariantCulture) - 1244160.0) < 1e-6);

        // Cross-check the tick constants reproduce those lengths.
        Check("const.day", VanaTime.DAY / VanaTime.TicksPerEarthSecond == 3456);
        Check("const.month", VanaTime.MONTH / VanaTime.TicksPerEarthSecond == 103680);
        Check("const.year", VanaTime.YEAR / VanaTime.TicksPerEarthSecond == 1244160);
    }

    private static void CheckVector(string line)
    {
        var d = ParseKv(line);
        var tag = line.Split(' ')[1];
        var earth = L(d, "earth");
        var ticks = VanaTime.EarthUnixSecToTicks(earth);
        var pfx = $"{tag}[{earth}]";

        CheckEq($"{pfx}.sec", VanaTime.GetSecond(ticks), (uint)L(d, "sec"));
        CheckEq($"{pfx}.min", VanaTime.GetMinute(ticks), (uint)L(d, "min"));
        CheckEq($"{pfx}.hour", VanaTime.GetHour(ticks), (uint)L(d, "hour"));
        CheckEq($"{pfx}.mday", VanaTime.GetMonthDay(ticks), (uint)L(d, "mday"));
        CheckEq($"{pfx}.mon", VanaTime.GetMonth(ticks), (uint)L(d, "mon"));
        CheckEq($"{pfx}.year", VanaTime.GetYear(ticks), (int)L(d, "year"));
        CheckEq($"{pfx}.wday", VanaTime.GetWeekDay(ticks), (uint)L(d, "wday"));
        CheckEq($"{pfx}.yday", VanaTime.GetYearDay(ticks), (uint)L(d, "yday"));
        CheckEq($"{pfx}.totd", (uint)VanaTime.GetTotd(ticks), (uint)L(d, "totd"));
        CheckEq($"{pfx}.moon_phase", VanaTime.MoonPhase(ticks), (uint)L(d, "moon_phase"));
        CheckEq($"{pfx}.moon_dir", VanaTime.MoonDirection(ticks), (byte)L(d, "moon_dir"));
        CheckEq($"{pfx}.rse_race", VanaTime.RseRace(ticks), (byte)L(d, "rse_race"));
        CheckEq($"{pfx}.rse_loc", VanaTime.RseLocation(ticks), (byte)L(d, "rse_loc"));
        CheckEq($"{pfx}.cdays", VanaTime.CountDays(ticks), (uint)UL(d, "cdays"));
        CheckEq($"{pfx}.cweeks", VanaTime.CountWeeks(ticks), (uint)UL(d, "cweeks"));
        CheckEq($"{pfx}.midnight", VanaTime.GetNextMidnightUnixSec(ticks), L(d, "midnight"));
    }

    private static void CheckEq<T>(string name, T actual, T expected)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected)) _pass++;
        else { _fail++; Console.WriteLine($"FAIL: {name} got '{actual}' want '{expected}'"); }
    }
}
