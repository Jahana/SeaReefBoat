// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 11 (call-only: The Wayward Automaton, Operation Teatime): each
// definition validates clean; the replies' kinds, which no flow can show. In an NPC call the bridge renders an Event
// and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no
// priority), and the scripts ignore the call's result where the quest answers with a message or nothing (qm9, qm10:
// the call is their whole onTrigger), so a NoAction, a MessageSpecial and a null look alike in play; the fake keeps
// them apart. Each pin is the Lua's: player:startEvent(id) with no arguments, messageSpecial(ID.text.X) with none.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup11()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 11: Iruki-Waraki, Dnegan, qm9 / qm10 (call-only) ==");
        var wa = new QC.AhtUrhgan.TheWaywardAutomaton().Compile();
        var ot = new QC.AhtUrhgan.OperationTeatime().Compile();
        foreach (var (name, def) in new[] { ("The Wayward Automaton", wa), ("Operation Teatime", ot) })
        {
            Check($"ahturhgan g11:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"ahturhgan g11:{name} is call-only", "True", def.CallOnly.ToString());
        }

        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        // a PUP (or a WAR) of the level, AF1_QUEST_LEVEL 40 and AF2_QUEST_LEVEL 50 as the settings have them
        Fake Fk(QuestStatus st, int job = QC.Job.Pup, int lvl = 40, QuestStatus nsa = QuestStatus.Completed,
                QuestStatus wayward = QuestStatus.Available, params (string, int)[] vars)
        {
            var f = new Fake { Status = st, MainJob = job, MainLevel = lvl };
            f.MainSettings["AF1_QUEST_LEVEL"] = 40;
            f.MainSettings["AF2_QUEST_LEVEL"] = 50;
            f.QuestStatuses["AhtUrghan/NO_STRINGS_ATTACHED"] = nsa;
            f.QuestStatuses["AhtUrghan/THE_WAYWARD_AUTOMATON"] = wayward;
            foreach (var (n, v) in vars) f.CharVars[n] = v;
            return f;
        }
        // the reply, then what the handler did on the way (qm9's message and spawn)
        string R(QuestScriptDefinition def, QuestHandlerKind kind, ushort zone, string npc, Fake f)
        {
            string r = AU3Npc(def, kind, zone, npc, f);
            return f.Calls.Count == 0 ? r : r + "+" + string.Join("+", f.Calls);
        }
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        ushort Aw = QC.Zone.AhtUrhganWhitegate, Ns = QC.Zone.Nashmau, Cm = QC.Zone.CaedarvaMire;
        string Iruki = QC.Npc.AhtUrhganWhitegate.IrukiWaraki, Dnegan = QC.Npc.Nashmau.Dnegan;
        const string WP = "TheWaywardAutomatonProgress", NM = "TheWaywardAutomatonNM";
        const string TI = "OperationTeaTimeProgress", TP = "OperationTeatimeProgress";

        L("wa iruki 774", R(wa, Tr, Aw, Iruki, Fk(QuestStatus.Available)));
        L("wa iruki 775", R(wa, Tr, Aw, Iruki, Fk(QuestStatus.Accepted, vars: (WP, 1))));
        L("wa iruki 776", R(wa, Tr, Aw, Iruki, Fk(QuestStatus.Accepted, vars: (WP, 3))));
        L("wa iruki 777 pup", R(wa, Tr, Aw, Iruki, Fk(QuestStatus.Completed)));
        L("wa iruki 777 war", R(wa, Tr, Aw, Iruki, Fk(QuestStatus.Completed, job: QC.Job.War, lvl: 75)));
        L("wa dnegan 1", R(wa, Tr, Ns, Dnegan, Fk(QuestStatus.Accepted, vars: (WP, 1))));
        L("wa dnegan 2", R(wa, Tr, Ns, Dnegan, Fk(QuestStatus.Available, vars: (WP, 2))));
        string Qm9 = QC.Npc.CaedarvaMire.qm9;
        L("wa qm9 14", R(wa, Tr, Cm, Qm9, Fk(QuestStatus.Accepted, vars: new[] { (WP, 2), (NM, 1) })));
        L("wa qm9 spawn", R(wa, Tr, Cm, Qm9, Fk(QuestStatus.Accepted, vars: (WP, 2))));
        var up = Fk(QuestStatus.Accepted, vars: (WP, 2));
        up.SpawnedMobs.Add(QC.Mob.CaedarvaMire.CAEDARVA_TOAD + "+0");
        L("wa qm9 up", R(wa, Tr, Cm, Qm9, up));
        L("wa qm9 slipped", R(wa, Tr, Cm, Qm9, Fk(QuestStatus.Accepted, vars: (WP, 3))));

        Fake Trade(int prog)
        {
            var f = Fk(QuestStatus.Accepted, vars: (TI, prog));
            f.TradeItems[QC.Item.FlaskOfSleepingPotion] = 1;
            f.TradeItems[QC.Item.CupOfChai] = 1;
            return f;
        }
        L("ot iruki trade", R(ot, Td, Aw, Iruki, Trade(1)));
        L("ot iruki 778", R(ot, Tr, Aw, Iruki, Fk(QuestStatus.Available, lvl: 50, wayward: QuestStatus.Completed)));
        L("ot iruki 779", R(ot, Tr, Aw, Iruki, Fk(QuestStatus.Accepted, vars: (TI, 1))));
        L("ot iruki 781", R(ot, Tr, Aw, Iruki, Fk(QuestStatus.Accepted, vars: (TI, 2))));
        L("ot iruki 777", R(ot, Tr, Aw, Iruki, Fk(QuestStatus.Completed)));
        var day = Fk(QuestStatus.Accepted, vars: (TP, 2));
        day.UniqueDay = 8000;
        L("ot dnegan 290", R(ot, Tr, Ns, Dnegan, day));
        L("ot qm10 15", R(ot, Tr, Cm, QC.Npc.CaedarvaMire.qm10, Fk(QuestStatus.Accepted, vars: (TP, 3))));
        L("ot qm10 nothing", R(ot, Tr, Cm, QC.Npc.CaedarvaMire.qm10, Fk(QuestStatus.Accepted, vars: (TP, 2))));
        Check("ahturhgan g11:the reply kinds no flow can show",
              "wa iruki 774=Event(774) ; wa iruki 775=Event(775) ; wa iruki 776=Event(776) ; " +
              "wa iruki 777 pup=Event(777) ; wa iruki 777 war=Event(777) ; wa dnegan 1=Event(289) ; " +
              "wa dnegan 2=Event(289) ; wa qm9 14=Event(14) ; " +
              "wa qm9 spawn=NoAction()+Text.Special(79:HIDEOUS_BEAST_EMERGES)" +
              "+Player.SpawnMob(CAEDARVA_TOAD,0,True) ; " +
              "wa qm9 up=null ; wa qm9 slipped=MessageSpecial(79:SOMEONE_SLIPPED) ; ot iruki trade=Event(780) ; " +
              "ot iruki 778=Event(778) ; ot iruki 779=Event(779) ; ot iruki 781=Event(781) ; " +
              "ot iruki 777=Event(777) ; ot dnegan 290=Event(290) ; ot qm10 15=Event(15) ; " +
              "ot qm10 nothing=MessageSpecial(79:NOTHING_OUT_OF_ORDINARY)",
              string.Join(" ; ", lines));
    }
}
