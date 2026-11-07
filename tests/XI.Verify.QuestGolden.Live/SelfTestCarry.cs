using System;
using System.Linq;

// the carry-harness-1 fixes (TODO's golden harness rows): their pure parts, in the self-test (no database)
internal static partial class Program
{
    private static void SelfTestCarryHarness()
    {
        Console.WriteLine("golden harness carries");
        SelfTestTempDirs();
        SelfTestDirtyScripts();
    }

    // a killed run's temp folder beside the repo: named with its process id (TempDirName), swept once that process is
    // gone; a folder of an older name is never swept
    private static void SelfTestTempDirs()
    {
        string overlay = TempDirName("_golden-tmp-"), startup = TempDirName("_startup-");
        Check("  temp dirs: a new folder's name gives this process's id, an overlay's and a startup's alike",
              $"{Environment.ProcessId}|{Environment.ProcessId}", $"{TempDirOwner(overlay)}|{TempDirOwner(startup)}");
        var others = new[]
        {
            "_golden-tmp-1a2b3c4d", "_other-12-1a2b3c4d", "_startup-12-1a2b3c4", "_startup-12-1A2B3C4D",
        };
        Check("  temp dirs: an older run's name (no process id), another prefix or a bad tail gives none",
              "-|-|-|-", string.Join("|", others.Select(n => TempDirOwner(n)?.ToString() ?? "-")));
    }

    // a record at HEAD stamps lua-at HEAD: into tests/golden, refused while scripts/ has uncommitted changes (git
    // status --porcelain); into another folder it goes on
    private static void SelfTestDirtyScripts()
    {
        const string Tail =
            ": the trace would stamp HEAD over Lua that HEAD does not have; commit (or stash) them first";
        const string Doorman = " M scripts/quests/bastok/The_Doorman.lua\n";
        string one = DirtyScriptsRefusal(Doorman, true);
        string six = DirtyScriptsRefusal(" M scripts/a.lua\r\n?? scripts/b.lua\n D scripts/c.lua\n M scripts/d.lua\n" +
                                         " M scripts/e.lua\n M scripts/f.lua\n", true);
        Check("  record at HEAD into tests/golden: a clean scripts/ goes on; one uncommitted change or more refuses " +
              "it, naming five",
              "-|lua-at HEAD, but scripts/ has 1 uncommitted change (M scripts/quests/bastok/The_Doorman.lua)" + Tail +
              "|lua-at HEAD, but scripts/ has 6 uncommitted changes (M scripts/a.lua, ?? scripts/b.lua, " +
              "D scripts/c.lua, M scripts/d.lua, M scripts/e.lua, ...)" + Tail,
              $"{DirtyScriptsRefusal("", true) ?? "-"}|{one}|{six}");
        // the draw-search tools (tools/golden_*_draws.py, golden_in_the_name_of_science.py) record instrumented,
        // uncommitted Lua at HEAD with --out <tmp>, and stable's children record into a temp folder
        Check("  record at HEAD outside tests/golden (--out): an uncommitted scripts/ goes on", "-|-",
              $"{DirtyScriptsRefusal(Doorman, false) ?? "-"}|{DirtyScriptsRefusal("", false) ?? "-"}");
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "golden-out");
        Check("  record folder: tests/golden in any spelling is the checked-in one; a temp --out is not",
              "True|True|True|False",
              $"{IsGoldenDir(GoldenDir)}|{IsGoldenDir(GoldenDir + "/")}|{IsGoldenDir(GoldenDir.ToUpperInvariant())}|" +
              $"{IsGoldenDir(tmp)}");
    }
}
