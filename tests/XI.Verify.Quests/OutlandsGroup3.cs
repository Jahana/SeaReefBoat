// XI.Verify.Quests -- quest rework B3b Outlands group 3 (Chasing Dreams, I'll Take the Big Box, Bugi Soden, Stop Your
// Whining): what the golden traces cannot show.
// - I'll Take the Big Box's Selbina zone-in past Prog 5: Prog 6 is not one of the quest's steps (1-5), so no flow sets it
//   (mutant b35, Prog == 5 -> >= 5); the check pins Prog 4 / 5 / 6. The #zonein flow plays Prog 4 (nothing) and the
//   table { 1101, -1, 0x13 } at Prog 5, and the 1101 finish's two arms with the reward (Norg fame 20, the Ninja Hakama).
// - Bugi Soden's 189 complete false: the reward is fame only, so npcUtil.completeQuest cannot fail and no flow reaches it.
// - The reply kinds of NPCs that only one of these quests answers and that have no script of their own (Rudolfo,
//   Zoriboh, Sohyon, the clams, Patient_Wheel's and Leodarion's and Ryoma's trades, Lufaise Meadows' zone-in, Yhoator's
//   qm2, Onzozo's qm1): with no rival and no script turn, a Progress and an Event reply send the same packets.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup3()
    {
        Console.WriteLine("== quest rework B3b Outlands group 3: Big Box's zone-in past Prog 5, complete false, lone NPCs' reply kinds ==");
        OL3ZoneIn();
        OL3CompleteFalse();
        OL3Kinds();
    }

    // a zone-in reply as the bridge returns it to luautils::OnZoneIn: the cutscene id or the table { id, textTable, flags }
    static string OL3ZoneInReply(QuestHandlerDecl h, Fake f)
    {
        var r = h.Fixed ?? h.Reply(f);
        return r == null ? "null" : QuestContextFake.DescribeZoneIn(r);
    }

    static Fake OL3Step(QuestStatus status, int step)
    {
        var f = new Fake { Status = status };
        f.Vars["Prog"] = step;
        return f;
    }

    static void OL3ZoneIn()
    {
        var def = new QC.Outlands.IllTakeTheBigBox().Compile();
        var h = OLHandler(def, 1, QuestHandlerKind.OnZoneIn, id: 1, zone: QC.Zone.Selbina);
        Check("outlands g3:Big Box's Selbina zone-in: the table { 1101, -1, RESET_CAMERA | NO_PCS | NO_NPCS } at Prog 5, not 4 or 6",
              "null|{1101,-1,19}|null",
              $"{OL3ZoneInReply(h, OL3Step(QuestStatus.Accepted, 4))}|{OL3ZoneInReply(h, OL3Step(QuestStatus.Accepted, 5))}|" +
              $"{OL3ZoneInReply(h, OL3Step(QuestStatus.Accepted, 6))}");
    }

    static void OL3CompleteFalse()
    {
        string Run(QuestHandlerDecl h, bool completes)
        {
            var f = new Fake { Status = QuestStatus.Accepted, CompleteResult = completes };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        var bs = new QC.Outlands.BugiSoden().Compile();
        Check("outlands g3:Bugi Soden's 189 complete false (not reachable: a fame-only reward): nothing more",
              "Quest.Complete()", Run(OLHandler(bs, 1, QuestHandlerKind.OnEventFinish, id: 189), false));
    }

    // the reply kinds, one check per NPC
    static void OL3Kinds()
    {
        var cd = new QC.Outlands.ChasingDreams().Compile();
        var bb = new QC.Outlands.IllTakeTheBigBox().Compile();
        var bs = new QC.Outlands.BugiSoden().Compile();
        var syw = new QC.Outlands.StopYourWhining().Compile();
        Fake At(int step, QuestStatus status = QuestStatus.Accepted) => OL3Step(status, step);

        Check("outlands g3:Rudolfo's reply kind: 117", "ProgressEvent(117)",
              OLReply(OLHandler(cd, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Rudolfo), new Fake()));

        var zor = OLHandler(cd, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Zoriboh);
        Check("outlands g3:Zoriboh's reply kinds: 119 / 120 / 121", "ProgressEvent(119)|Event(120)|ProgressEvent(121)",
              $"{OLReply(zor, At(0))}|{OLReply(zor, At(1))}|" +
              OLReply(OLHandler(cd, 2, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Zoriboh), At(9)));

        var soh = OLHandler(cd, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Sohyon);
        var soh3 = new Fake { Status = QuestStatus.Accepted };
        soh3.Vars["Prog"] = 4;
        soh3.KeyItems[QC.KeyItem.FlaskOfClamWater] = true;
        Check("outlands g3:Sohyon's reply kinds: 209 / 213 / 210", "ProgressEvent(209)|Event(213)|ProgressEvent(210)",
              $"{OLReply(soh, At(1))}|{OLReply(soh, At(2))}|" +
              OLReply(OLHandler(cd, 2, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Sohyon), soh3));

        var wheel = new Fake { Status = QuestStatus.Accepted };
        wheel.Vars["Prog"] = 6;
        wheel.TradeItems[QC.Item.EasternGem] = 5;
        Check("outlands g3:Patient_Wheel's trade reply kind: 323", "ProgressEvent(323)",
              OLReply(OLHandler(cd, 2, QuestHandlerKind.OnTrade, entity: QC.Npc.PortBastok.PatientWheel), wheel));

        Check("outlands g3:Lufaise Meadows' zone-in reply kind: 4", "Event(4)",
              OLReply(OLHandler(cd, 2, QuestHandlerKind.OnZoneIn, id: 1, zone: QC.Zone.LufaiseMeadows), At(8)));

        var clam = new Fake { Status = QuestStatus.Accepted };
        clam.Vars["Prog"] = 3;
        Check("outlands g3:a clam's reply kind: CLAM_EMPTY", $"MessageSpecial({QC.Zone.KorrolokaTunnel}:CLAM_EMPTY,{QC.KeyItem.WashusFlask})",
              OLReply(OLHandler(cd, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.KorrolokaTunnel._4t0), clam));

        var leo = new Fake { Status = QuestStatus.Accepted, TradeResult = true };
        leo.Vars["Prog"] = 2;
        Check("outlands g3:Leodarion's trade reply kind: 92", "ProgressEvent(92)",
              OLReply(OLHandler(bb, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Rabao.Leodarion), leo));

        var ryomaTrade = OLHandler(bs, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Norg.Ryoma);
        string RyomaTrade(int points)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.TradeItems[QC.Item.KodachiOfTrials] = 1;
            f.TradeWeaponskillPoints[0] = points;
            return OLReply(ryomaTrade, f);
        }
        Check("outlands g3:Ryoma's trade reply kinds: 186 / 187", "Event(186)|ProgressEvent(187)",
              $"{RyomaTrade(299)}|{RyomaTrade(300)}");

        var qm1 = new Fake { Status = QuestStatus.Accepted };
        qm1.PlayerLocalVars["killed_wsnm"] = 1;
        Check("outlands g3:Onzozo's qm1 reply kind: the annals", $"KeyItem({QC.KeyItem.AnnalsOfTruth})",
              OLReply(OLHandler(bs, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.LabyrinthOfOnzozo.qm1), qm1));

        var qm2 = new Fake { Status = QuestStatus.Accepted };
        qm2.KeyItems[QC.KeyItem.BarrelOfOpoOpoBrew] = true;
        Check("outlands g3:Yhoator's qm2 reply kind: TREE_FULL", $"MessageSpecial({QC.Zone.YhoatorJungle}:TREE_FULL)",
              OLReply(OLHandler(syw, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.YhoatorJungle.qm2), qm2));
    }
}
