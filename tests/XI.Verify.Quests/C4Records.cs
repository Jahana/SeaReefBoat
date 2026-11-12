// XI.Verify.Quests -- quest rework C4: the C# records, every one, against the Lua dump in tests/golden/roe_records.txt,
// byte for byte, for good: the running Lua wrote the dump before the port, and the Lua is gone.
using System;
using System.IO;
using System.Linq;
using XI.QuestContent.Roe;
using XI.Quests;

internal static partial class Program
{
    internal static void RunC4Records()
    {
        Console.WriteLine("\n== c4 records: the C# records against the Lua dump ==");
        string root = QuestsLuaRoot();
        Check("c4 records: the repo root found", "True", (root != null).ToString());
        if (root == null) return;
        var want = File.ReadAllText(Path.Combine(root, "tests", "golden", "roe_records.txt")).Replace("\r\n", "\n").Split('\n');
        var got = RoeRecordDump.Text(RoeRecords.All, RoeRecords.TimedSchedule).Split('\n');
        int first = Enumerable.Range(0, Math.Min(want.Length, got.Length)).Where(i => want[i] != got[i]).Select(i => i + 1).FirstOrDefault();
        Check("c4 records: every line as the Lua dump has it", "",
              first == 0 && want.Length == got.Length ? ""
              : first == 0 ? $"{want.Length} lines in the dump, {got.Length} here"
              : $"line {first}: dump [{want[first - 1]}] here [{got[first - 1]}]");
        Check("c4 records: 1081 records", "1081", RoeRecords.All.Count.ToString());
        Check("c4 records: in id order, each once", "",
              string.Join(",", RoeRecords.All.Zip(RoeRecords.All.Skip(1)).Where(p => p.First.Id >= p.Second.Id).Select(p => p.Second.Id)));
    }
}
