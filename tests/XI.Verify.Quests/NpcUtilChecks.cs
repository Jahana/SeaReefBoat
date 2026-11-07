// XI.Verify.Quests -- the shared npcUtil ports (src/XI.QuestContent/NpcUtil.cs, carry-quests-1) on the fake.
// NpcUtil.PickNewPosition is npcUtil.pickNewPosition, which The Missing Piece's qm5, Striking a Balance's blank and
// Test My Mettle's Jar call (each with its own trigger's NPC, allowCurrentPosition false); their flows replay it.
// These pin what no flow shows: the first randomInt(1, 0) (the engine answers 1 without drawing, so no trace sees it),
// the row found by its floored coordinates when the NPC stands off the row's exact numbers, and the two paths no
// caller takes (no NPC: no row matches, so row 1 counts as the current one; allowCurrentPosition: the first draw
// stands).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunNpcUtilChecks()
    {
        Console.WriteLine("\n== npcUtil: the shared ports (NpcUtil.cs) ==");
        double[][] table =
        {
            new[] { 10.0, 0.0, -20.0 },
            new[] { 30.5, 1.0, -40.0 },
            new[] { 25.08, 0.44, -107.3 },
            new[] { -5.0, 2.0, 7.0 },
            new[] { 60.0, -3.0, 12.5 },
        };
        string Pick(Fake f, IQuestNpc npc, bool allowCurrent, params int[] rolls)
        {
            foreach (int r in rolls) f.Rolls.Enqueue(r);
            var row = QC.NpcUtil.PickNewPosition(f, npc, table, allowCurrent);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join(",", row.Select(v => v.ToString(inv))) + "|" + string.Join(";", f.Calls);
        }

        // on row 3 off its numbers (25.9, 0.7, -107.1 floor to 25, 0, -108 as the row's do): row 3 is the current one,
        // so the draws 3 and 3 are drawn again and 5 stands
        var on3 = new Fake();
        var npc3 = new QuestNpcFake(1, on3.Calls) { XPos = 25.9, YPos = 0.7, ZPos = -107.1 };
        Check("npcutil:pickNewPosition: the first randomInt(1, 0), the row found by its floored coordinates, " +
              "a redraw while the draw is that row",
              "60,-3,12.5|Player.RandomInt(1,0);Player.RandomInt(1,5);Player.RandomInt(1,5);Player.RandomInt(1,5)",
              Pick(on3, npc3, false, 1, 3, 3, 5));

        // on no row: row 1 counts as the current one (the default), so a draw of 1 is drawn again
        var off = new Fake();
        var npcOff = new QuestNpcFake(1, off.Calls) { XPos = 100, YPos = 100, ZPos = 100 };
        Check("npcutil:pickNewPosition: on no row, row 1 is the current one",
              "30.5,1,-40|Player.RandomInt(1,0);Player.RandomInt(1,5);Player.RandomInt(1,5)",
              Pick(off, npcOff, false, 1, 1, 2));

        // the Lua's `npc and ...`: with no NPC no row matches, the same as on no row
        var none = new Fake();
        Check("npcutil:pickNewPosition: no NPC, row 1 is the current one",
              "30.5,1,-40|Player.RandomInt(1,0);Player.RandomInt(1,5);Player.RandomInt(1,5)",
              Pick(none, null, false, 1, 1, 2));

        // allowCurrentPosition: no row is looked for and nothing is drawn again: the first randomInt(1, 0) stands (1)
        var allow = new Fake();
        var npcAllow = new QuestNpcFake(1, allow.Calls) { XPos = 10, YPos = 0, ZPos = -20 };
        Check("npcutil:pickNewPosition: allowCurrentPosition, the first draw stands (row 1, the NPC's own)",
              "10,0,-20|Player.RandomInt(1,0)",
              Pick(allow, npcAllow, true, 1));
    }
}
