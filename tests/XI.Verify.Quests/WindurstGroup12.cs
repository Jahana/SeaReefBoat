// XI.Verify.Quests -- quest rework B3b Windurst group 12 (Lure of the Wildcat (Windurst), Acting in Good Faith,
// Eco-Warrior (Windurst) and its era module, Wondering Minstrel): what no trace can see. An Event and a ProgressEvent
// send the same 0x032 (0x034 with params); the kind shows only through the lookup's fallback, at an NPC whose script
// answers the same interaction, or beside a rival line. Gantineux, Lumomo, Ahko Mhalijikhari, the Eldieme qm1, the Maze
// qm2, Aramu-Paramu and Ruslan have no NPC script; Yung Yaam's script has onSpawn only; Jatan-Paratan's has no onTrade.
// None has a rival quest or mission line (Lua or C#), so no talk or trade there can show the kind: structurally
// equivalent in golden, the fake pins every reply's kind. Ruslan's completed line is pinned at each draw of
// math.randomInt(1, 3) (the golden flows draw nil, nil, 10011: 2 and 3 are not told apart there).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup12()
    {
        Console.WriteLine("== quest rework B3b Windurst group 12: the reply kinds at NPCs with no script, " +
                          "Ruslan's draw ==");
        RunWindurstGroup12Kinds();
        RunWindurstGroup12Ruslan();
    }

    // the reply of the state's onTrigger (or onTrade) at the entity (its fixed reply, or its function's on the fake),
    // described
    static string W12Reply(QuestScriptDefinition def, int state, string entity, Fake f,
                           QuestHandlerKind kind = QuestHandlerKind.OnTrigger)
    {
        var h = def.States[state].Handlers.Single(x => x.Kind == kind && x.Entity == entity);
        var r = h.Fixed ?? h.Reply(f);
        return r == null ? "null" : QuestContextFake.Describe(r);
    }

    static Fake W12Fake(QuestStatus status, int prog = 0, params int[] keyItems)
    {
        var f = new Fake { Status = status, Step = prog };
        foreach (var k in keyItems) f.KeyItems[k] = true;
        return f;
    }

    static void RunWindurstGroup12Kinds()
    {
        string gantineux = QC.Npc.WindurstWaters.Gantineux, lumomo = QC.Npc.WindurstWaters.Lumomo;
        string ahko = QC.Npc.MazeOfShakhrami.AhkoMhalijikhari, yung = QC.Npc.WindurstWaters.YungYaam;
        string aramu = QC.Npc.WindurstWaters.AramuParamu, ruslan = QC.Npc.LowerJeuno.Ruslan;

        var ag = new QC.Windurst.ActingInGoodFaith().Compile();
        Check("windurst g12 kinds:Acting in Good Faith keeps the Lua's kinds: Gantineux 10019 / 10020 / 10022 / " +
              "10021 / 10023 (replaceDefault), the qm1's 50",
              "ProgressEvent(10019)|Event(10020)|Event(10022)|ProgressEvent(10021)|Event(10023):replaceDefault()|" +
              "ProgressEvent(50)",
              string.Join("|",
                  W12Reply(ag, 0, gantineux, W12Fake(QuestStatus.Available)),
                  W12Reply(ag, 1, gantineux, W12Fake(QuestStatus.Accepted, 0, QC.KeyItem.SpiritIncense)),
                  W12Reply(ag, 1, gantineux, W12Fake(QuestStatus.Accepted, 0, QC.KeyItem.GantineuxsLetter)),
                  W12Reply(ag, 1, gantineux, W12Fake(QuestStatus.Accepted)),
                  W12Reply(ag, 2, gantineux, W12Fake(QuestStatus.Completed)),
                  W12Reply(ag, 1, QC.Npc.TheEldiemeNecropolis.qm1,
                           W12Fake(QuestStatus.Accepted, 0, QC.KeyItem.SpiritIncense))));

        var ew = new QC.Windurst.EcoWarrior().Compile();
        Fake Offer(QuestStatus status, string otherAccepted)
        {
            var f = W12Fake(status);
            f.Fame[QC.FameArea.Windurst] = 1;
            if (otherAccepted != null) f.QuestStatuses[otherAccepted] = QuestStatus.Accepted;
            return f;
        }
        Fake Restricted(int prog, params int[] keyItems)
        {
            var f = W12Fake(QuestStatus.Accepted, prog, keyItems);
            f.StatusEffects.Add(QC.Bastok.EcoWarrior.LevelRestriction);
            return f;
        }
        Check("windurst g12 kinds:Eco-Warrior keeps the Lua's kinds: Lumomo 823 / 818 (available and completed), " +
              "820 / 822; Ahko Mhalijikhari 65 / 64 / 62; the qm2's message and key item",
              "Event(823)|ProgressEvent(818)|Event(823)|ProgressEvent(818)|Event(820)|ProgressEvent(822)|" +
              "ProgressEvent(65)|ProgressEvent(64)|ProgressEvent(62)|MessageSpecial(198:OINTMENT_DRAWS_MONSTERS)|" +
              "KeyItem(" + QC.KeyItem.IndigestedMeat + ")",
              string.Join("|",
                  W12Reply(ew, 0, lumomo, Offer(QuestStatus.Available, "Bastok/ECO_WARRIOR")),
                  W12Reply(ew, 0, lumomo, Offer(QuestStatus.Available, null)),
                  W12Reply(ew, 2, lumomo, Offer(QuestStatus.Completed, "Sandoria/ECO_WARRIOR")),
                  W12Reply(ew, 2, lumomo, Offer(QuestStatus.Completed, null)),
                  W12Reply(ew, 1, lumomo, W12Fake(QuestStatus.Accepted)),
                  W12Reply(ew, 1, lumomo, W12Fake(QuestStatus.Accepted, 1, QC.KeyItem.IndigestedMeat)),
                  W12Reply(ew, 1, ahko, W12Fake(QuestStatus.Accepted, 1, QC.KeyItem.IndigestedMeat)),
                  W12Reply(ew, 1, ahko, Restricted(0)),
                  W12Reply(ew, 1, ahko, W12Fake(QuestStatus.Accepted)),
                  W12Reply(ew, 1, QC.Npc.MazeOfShakhrami.qm2, Restricted(0)),
                  W12Reply(ew, 1, QC.Npc.MazeOfShakhrami.qm2, W12Fake(QuestStatus.Accepted, 1))));

        var wm = new QC.Windurst.WonderingMinstrel().Compile();
        Fake Lumber(int prog)
        {
            var f = W12Fake(QuestStatus.Accepted, prog);
            f.TradeResult = true;
            return f;
        }
        Fake Zoning()
        {
            var f = W12Fake(QuestStatus.Completed);
            f.ZoningFlag = true;
            return f;
        }
        Check("windurst g12 kinds:Wondering Minstrel keeps the Lua's kinds: Yung Yaam 636 (available, accepted) / " +
              "643; Aramu-Paramu 683 / 684; Ruslan 10009 / 10010; Jatan-Paratan's trade 638",
              "Event(636)|Event(636)|Event(643)|Event(683)|Event(684)|" +
              "ProgressEvent(10009,0," + QC.Item.PieceOfRosewoodLumber + ")|" +
              "Event(10010,0," + QC.Item.PieceOfRosewoodLumber + ")|ProgressEvent(638)",
              string.Join("|",
                  W12Reply(wm, 0, yung, W12Fake(QuestStatus.Available)),
                  W12Reply(wm, 1, yung, W12Fake(QuestStatus.Accepted)),
                  W12Reply(wm, 2, yung, Zoning()),
                  W12Reply(wm, 1, aramu, W12Fake(QuestStatus.Accepted)),
                  W12Reply(wm, 2, aramu, Zoning()),
                  W12Reply(wm, 1, ruslan, W12Fake(QuestStatus.Accepted, 1)),
                  W12Reply(wm, 1, ruslan, W12Fake(QuestStatus.Accepted, 2)),
                  W12Reply(wm, 1, QC.Npc.WindurstWaters.JatanParatan, Lumber(2), QuestHandlerKind.OnTrade)));
    }

    static void RunWindurstGroup12Ruslan()
    {
        var wm = new QC.Windurst.WonderingMinstrel().Compile();
        string Draw(int roll)
        {
            var f = W12Fake(QuestStatus.Completed);
            f.Rolls.Enqueue(roll);
            return W12Reply(wm, 2, QC.Npc.LowerJeuno.Ruslan, f) + ";" + string.Join(";", f.Calls);
        }
        Check("windurst g12 ruslan:completed Ruslan draws math.randomInt(1, 3) once: 1 plays 10011, 2 and 3 nothing",
              "Event(10011);Player.RandomInt(1,3)|null;Player.RandomInt(1,3)|null;Player.RandomInt(1,3)",
              $"{Draw(1)}|{Draw(2)}|{Draw(3)}");
    }
}
