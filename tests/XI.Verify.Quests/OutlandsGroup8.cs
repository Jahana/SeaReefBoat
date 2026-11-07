// XI.Verify.Quests -- quest rework B3b Outlands group 8 (Twenty in Pirate Years, True Will; call-only): what the golden
// traces cannot show.
// - The reply kinds. Both quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an Event
//   and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no lookup action,
//   so no priority to tell them apart, and every trace replays the same with either kind. The Lua scripts call
//   player:startEvent, so each reply is pinned here as Event.
// - The replies that leave the rest to the script or the lookup: Leodarion answers nothing past his three arms (his
//   script has nothing after the call, so NoAction would look the same: the lookup's default 89 follows either way), and
//   qm3 answers NoAction after its pop or its box (its script has nothing after the call, so null would look the same).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup8()
    {
        Console.WriteLine("== quest rework B3b Outlands group 8: the call-mode reply kinds ==");
        OL8Twenty();
        OL8TrueWill();
    }

    static void OL8Twenty()
    {
        var def = new QC.Outlands.TwentyInPirateYears().Compile();
        string ryoma = QC.Npc.Norg.Ryoma;
        string ensetsu = QC.Npc.PortBastok.Ensetsu;
        var f = new Fake { Status = QuestStatus.Available };
        Check("outlands g8:Twenty in Pirate Years' replies are events (call mode): Ryoma's 133 / 134; Ensetsu's 247 / 262 / 263",
              "Event(133)|Event(134)|Event(247)|Event(262)|Event(263)",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: ryoma), f)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: ryoma), f)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: ensetsu), f)}|" +
              $"{OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: ensetsu), f)}|" +
              OLReply(OLHandler(def, 4, QuestHandlerKind.OnTrigger, entity: ensetsu), f));
    }

    static void OL8TrueWill()
    {
        var def = new QC.Outlands.TrueWill().Compile();
        string ryoma = QC.Npc.Norg.Ryoma;
        string leodarion = QC.Npc.Rabao.Leodarion;
        string qm3 = QC.Npc.YhoatorJungle.qm3;
        Fake Cs(int cs, bool largeBox = false)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.CharVars["trueWillCS"] = cs;
            if (largeBox) f.KeyItems[QC.KeyItem.LargeTrickBox] = true;
            return f;
        }
        var any = new Fake { Status = QuestStatus.Available };
        Check("outlands g8:True Will's replies are events (call mode): Ryoma's 136 / 137 / 138; Leodarion's 97 / 98 / 99, and " +
              "nothing past them (null: his script ends, the lookup's default follows)",
              "Event(136)|Event(137)|Event(138)|Event(97)|Event(98)|Event(99)|null",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: ryoma), any)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: ryoma), any)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: ryoma), any)}|" +
              $"{OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: leodarion), Cs(1))}|" +
              $"{OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: leodarion), Cs(2))}|" +
              $"{OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: leodarion), Cs(2, largeBox: true))}|" +
              OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: leodarion), Cs(3)));
        var pop = new Fake { Status = QuestStatus.Accepted };
        var box = new Fake { Status = QuestStatus.Accepted };
        box.CharVars["trueWillKilledNM"] = 1;
        Check("outlands g8:True Will's qm3 answers NoAction after its pop and after its box (its script has nothing after the " +
              "call); NOTHING_OUT_OF_ORDINARY otherwise",
              "NoAction()|NoAction()|MessageSpecial(124:NOTHING_OUT_OF_ORDINARY)",
              $"{OLReply(OLHandler(def, 4, QuestHandlerKind.OnTrigger, entity: qm3), pop)}|" +
              $"{OLReply(OLHandler(def, 4, QuestHandlerKind.OnTrigger, entity: qm3), box)}|" +
              OLReply(OLHandler(def, 5, QuestHandlerKind.OnTrigger, entity: qm3), any));
    }
}
