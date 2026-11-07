// XI.Verify.Quests -- quest rework B3b San d'Oria group 15 (call-only: The Holy Crest, A Craftsman's Work,
// Knight Stalker): each definition validates clean and is call-only; the replies' kinds, which no flow can show: in an
// NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent
// (ZoneHost.QuestCalls.cs RenderCallReply), and NoAction as nothing at all. Each pin is the Lua's:
// player:startEvent(id) with no arguments, nothing started (Rahal's accepted arm at a Progress its sub-arms miss), or
// no arm (null). The finishes' calls in the scripts' order: 32001 (the battlefield's win), Miaux's 70 and the
// qm15's 67, with and without room.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunSandoriaGroup15()
    {
        Console.WriteLine(
            "== quest rework B3b San d'Oria group 15: The Holy Crest, A Craftsman's Work, Knight Stalker " +
            "(call-only) ==");
        var hc = new QC.Sandoria.TheHolyCrest().Compile();
        var cw = new QC.Sandoria.ACraftsmansWork().Compile();
        var ks = new QC.Sandoria.KnightStalker().Compile();
        var defs = new[] { ("The Holy Crest", hc), ("A Craftsman's Work", cw), ("Knight Stalker", ks) };
        foreach (var (name, def) in defs)
        {
            Check($"sandoria g15:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"sandoria g15:{name} is call-only", "True", def.CallOnly.ToString());
        }

        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        ushort Port = QC.Zone.PortSanDoria, North = QC.Zone.NorthernSanDoria, South = QC.Zone.SouthernSanDoria;
        ushort Chateau = QC.Zone.ChateauDoraguille, Temple = QC.Zone.TempleOfUggalepih;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger;
        int drg = QC.Job.Drg, challenge = QC.KeyItem.ChallengeToTheRoyalKnights;
        Fake Char(QuestStatus st, int job = 0, int level = 75, params (string Name, int Value)[] vars)
        {
            var f = new Fake { Status = st, MainJob = job, MainLevel = level };
            f.MainNumbers["ADVANCED_JOB_LEVEL"] = 30;
            f.MainNumbers["AF1_QUEST_LEVEL"] = 40;
            foreach (var (n, v) in vars) f.CharVars[n] = v;
            return f;
        }
        Fake Quotas(Fake f)
        {
            f.QuestStatuses["Sandoria/CHASING_QUOTAS"] = QuestStatus.Completed;
            return f;
        }
        Fake With(Fake f, int keyItem)
        {
            f.KeyItems[keyItem] = true;
            return f;
        }
        string R(QuestScriptDefinition def, ushort zone, string npc, Fake f) => AU3Npc(def, Tr, zone, npc, f);

        L("hc arminibit", R(hc, Port, QC.Npc.PortSanDoria.Arminibit, Char(QuestStatus.Available, level: 30)));
        L("hc ceraulian", R(hc, Port, QC.Npc.PortSanDoria.Ceraulian, Char(QuestStatus.Available, level: 30)));
        L("hc morjean", R(hc, North, QC.Npc.NorthernSanDoria.Morjean,
                          Char(QuestStatus.Available, vars: ("TheHolyCrest_Event", 2))));
        L("hc morjean 3", R(hc, North, QC.Npc.NorthernSanDoria.Morjean,
                            Char(QuestStatus.Accepted, vars: ("TheHolyCrest_Event", 3))));

        string Miaux = QC.Npc.NorthernSanDoria.Miaux;
        L("cw 73", R(cw, North, Miaux, Char(QuestStatus.Available, drg, 40)));
        L("cw 71", R(cw, North, Miaux, Char(QuestStatus.Available, drg, 40, ("has_seen_drgaf1_quest_already", 1))));
        L("cw 69", R(cw, North, Miaux, Char(QuestStatus.Accepted, drg, 40)));
        L("cw 70", R(cw, North, Miaux, With(Char(QuestStatus.Accepted, drg, 40), QC.KeyItem.AltepaPolishingStone)));
        L("cw completed", R(cw, North, Miaux, Char(QuestStatus.Completed, drg, 40)));

        string Rahal = QC.Npc.ChateauDoraguille.Rahal, Ceraulian = QC.Npc.PortSanDoria.Ceraulian;
        const string P = "KnightStalker_Progress";
        L("ks 121", R(ks, Chateau, Rahal, Quotas(Char(QuestStatus.Available, drg))));
        L("ks 120", R(ks, Chateau, Rahal,
                      Quotas(Char(QuestStatus.Available, drg, vars: ("KnightStalker_Declined", 1)))));
        L("ks 119", R(ks, Chateau, Rahal, Char(QuestStatus.Accepted, drg)));
        L("ks 78", R(ks, Chateau, Rahal, With(Char(QuestStatus.Accepted, drg, vars: (P, 1)), challenge)));
        L("ks 69", R(ks, Chateau, Rahal, With(Char(QuestStatus.Accepted, drg, vars: (P, 2)), challenge)));
        L("ks 110", R(ks, Chateau, Rahal, With(Char(QuestStatus.Accepted, drg, vars: (P, 3)), challenge)));
        L("ks rahal 4", R(ks, Chateau, Rahal, With(Char(QuestStatus.Accepted, drg, vars: (P, 4)), challenge)));
        L("ks rahal 1", R(ks, Chateau, Rahal, Char(QuestStatus.Accepted, drg, vars: (P, 1))));
        L("ks rahal completed", R(ks, Chateau, Rahal, Quotas(Char(QuestStatus.Completed, drg))));
        L("ks 16", R(ks, Port, Ceraulian, Quotas(Char(QuestStatus.Available))));
        L("ks 19", R(ks, Port, Ceraulian, Char(QuestStatus.Accepted)));
        L("ks 23", R(ks, Port, Ceraulian, Char(QuestStatus.Accepted, vars: (P, 1))));
        L("ks 20", R(ks, Port, Ceraulian, With(Char(QuestStatus.Accepted, vars: (P, 1)), challenge)));
        L("ks 22", R(ks, Port, Ceraulian, Char(QuestStatus.Completed, vars: ("KnightStalker_Option1", 1))));
        L("ks 21", R(ks, Port, Ceraulian, Char(QuestStatus.Completed)));
        L("ks ceraulian", R(ks, Port, Ceraulian, Char(QuestStatus.Available)));
        L("ks 63", R(ks, South, QC.Npc.SouthernSanDoria.Balasiel, Char(QuestStatus.Accepted, vars: (P, 2))));
        L("ks balasiel 3", R(ks, South, QC.Npc.SouthernSanDoria.Balasiel, Char(QuestStatus.Accepted, vars: (P, 3))));
        L("ks 67", R(ks, Temple, QC.Npc.TempleOfUggalepih.qm15,
                     Char(QuestStatus.Accepted, vars: ("KnightStalker_Kill", 1))));
        L("ks qm15", R(ks, Temple, QC.Npc.TempleOfUggalepih.qm15, Char(QuestStatus.Accepted)));
        Check("sandoria g15:the reply kinds no flow can show",
              "hc arminibit=Event(24) ; hc ceraulian=Event(24) ; hc morjean=Event(65) ; hc morjean 3=null ; " +
              "cw 73=Event(73) ; cw 71=Event(71) ; cw 69=Event(69) ; cw 70=Event(70) ; cw completed=null ; " +
              "ks 121=Event(121) ; ks 120=Event(120) ; ks 119=Event(119) ; ks 78=Event(78) ; ks 69=Event(69) ; " +
              "ks 110=Event(110) ; ks rahal 4=NoAction() ; ks rahal 1=NoAction() ; ks rahal completed=null ; " +
              "ks 16=Event(16) ; ks 19=Event(19) ; ks 23=Event(23) ; ks 20=Event(20) ; ks 22=Event(22) ; " +
              "ks 21=Event(21) ; ks ceraulian=null ; ks 63=Event(63) ; ks balasiel 3=null ; ks 67=Event(67) ; " +
              "ks qm15=null",
              string.Join(" ; ", lines));

        // the finishes, each run on a fake at the csid (and option), in the scripts' order
        string Finish(QuestScriptDefinition def, ushort zone, int csid, int option = 0, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(st => st.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, Definition = def };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        int remedy = QC.KeyItem.DragonCurseRemedy;
        ushort Ghelsba = QC.Zone.GhelsbaOutpost;
        Check("sandoria g15:32001's finish, option 5 with the remedy: npcUtil.completeQuest's steps, in order",
              $"Player.DelKeyItem({remedy});Player.UnlockJob({drg});Player.SetPetName(1,6);" +
              "Text.Special(140:YOU_CAN_NOW_BECOME_A_DRAGOON);" +
              $"Player.GiveKeyItem({QC.KeyItem.JobGestureDragoon});Player.AddFame({QC.FameArea.Sandoria},20);" +
              $"Player.AddTitle({QC.Title.HeirToTheHolyCrest});Player.SetCharVar(TheHolyCrest_Event,0,0);" +
              "Player.CompleteQuest(Sandoria,THE_HOLY_CREST)",
              Finish(hc, Ghelsba, 32001, 5, f => f.KeyItems[remedy] = true));
        Check("sandoria g15:32001's finish, option 0 with the remedy, option 5 without: nothing", "|",
              Finish(hc, Ghelsba, 32001, 0, f => f.KeyItems[remedy] = true) + "|" + Finish(hc, Ghelsba, 32001, 5));
        Check("sandoria g15:70's finish, with room and without, in order",
              $"Player.GiveItem({QC.Item.Peregrine});Player.SetCharVar(aCraftsmanWork,0,0);" +
              $"Player.DelKeyItem({QC.KeyItem.AltepaPolishingStone});Player.AddFame({QC.FameArea.Sandoria},20);" +
              $"Player.CompleteQuest(Sandoria,A_CRAFTSMANS_WORK)|Player.GiveItem({QC.Item.Peregrine})",
              Finish(cw, North, 70) + "|" + Finish(cw, North, 70, setup: f => f.GiveResult = false));
        Check("sandoria g15:67's finish, with room and without, in order",
              $"Player.GiveItem({QC.Item.DrachenArmet});Player.AddTitle({QC.Title.ParagonOfDragoonExcellence});" +
              "Player.SetCharVar(KnightStalker_Kill,0,0);Player.SetCharVar(KnightStalker_Progress,0,0);" +
              $"Player.CompleteQuest(Sandoria,KNIGHT_STALKER);Player.DelKeyItem({challenge});" +
              "Player.SetCharVar(KnightStalker_Option1,1,0);Player.SetCharVar(KnightStalker_Option2,1,0)|" +
              $"Player.GiveItem({QC.Item.DrachenArmet})",
              Finish(ks, Temple, 67) + "|" + Finish(ks, Temple, 67, setup: f => f.GiveResult = false));
    }
}
