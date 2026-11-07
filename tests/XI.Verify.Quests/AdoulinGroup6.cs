// XI.Verify.Quests -- quest rework B3b Adoulin group 6 (call-only: Order Up, The Curious Case of Melvien,
// The Old Man and the Harpoon, Fertile Ground, Wayward Waypoints): each definition validates clean and is call-only;
// the replies' kinds, which no flow can show: in an NPC call the bridge renders an Event, a ProgressEvent and a
// PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no
// priority). Each pin is the Lua's: player:startEvent(id) with no arguments, or nothing (null: the script goes on to
// its next call or its own dialogue). The finishes' calls in the scripts' order: 2542's (the raw completeQuest, then
// exp, bayld and its message at the rates, the harpoon taken, fame), 2543's, 2540's, and the two key items with no
// message (2850, 79).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup6()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 6: Order Up's NPCs, Jorin, Shipilolo (call-only) ==");
        var ou = new QC.Adoulin.OrderUp().Compile();
        var me = new QC.Adoulin.TheCuriousCaseOfMelvien().Compile();
        var ha = new QC.Adoulin.TheOldManAndTheHarpoon().Compile();
        var fg = new QC.Adoulin.FertileGround().Compile();
        var ww = new QC.Adoulin.WaywardWaypoints().Compile();
        foreach (var (name, def) in new[]
                 {
                     ("Order Up", ou), ("The Curious Case of Melvien", me), ("The Old Man and the Harpoon", ha),
                     ("Fertile Ground", fg), ("Wayward Waypoints", ww),
                 })
        {
            Check($"adoulin g6:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"adoulin g6:{name} is call-only", "True", def.CallOnly.ToString());
        }

        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, int mask = 0, int ww = 0, params int[] keyItems)
        {
            var f = new Fake { Status = st };
            f.CharVars["Order_Up_NPCs"] = mask;
            f.CharVars["WW_Need_Shipilolo"] = ww;
            foreach (int k in keyItems) f.KeyItems[k] = true;
            return f;
        }
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger;
        ushort Wa = QC.Zone.WesternAdoulin;
        string R(QuestScriptDefinition def, string npc, Fake f) => AU3Npc(def, Tr, Wa, npc, f);

        // Order Up: each NPC, accepted without its bit; with it; available
        foreach (var (npc, bit) in new[]
                 {
                     (QC.Npc.WesternAdoulin.Terwok, 7), (QC.Npc.WesternAdoulin.Marjoirelle, 8),
                     (QC.Npc.WesternAdoulin.OkaQhantari, 9), (QC.Npc.WesternAdoulin.Grevan, 10),
                     (QC.Npc.WesternAdoulin.Mastan, 11),
                 })
        {
            L($"ou {npc}", R(ou, npc, Fk(QuestStatus.Accepted, mask: 0x7FFF & ~(1 << bit))));
            L($"ou {npc} bit", R(ou, npc, Fk(QuestStatus.Accepted, mask: 1 << bit)));
            L($"ou {npc} available", R(ou, npc, Fk(QuestStatus.Available)));
        }

        string Mastan = QC.Npc.WesternAdoulin.Mastan, Jorin = QC.Npc.WesternAdoulin.Jorin;
        string Shipilolo = QC.Npc.WesternAdoulin.Shipilolo;
        int Turn = QC.KeyItem.MelviensTurn, Death = QC.KeyItem.MelviensDeath;
        L("me 184", R(me, Mastan, Fk(QuestStatus.Accepted, keyItems: Turn)));
        L("me death", R(me, Mastan, Fk(QuestStatus.Accepted, keyItems: new[] { Turn, Death })));
        L("me no turn", R(me, Mastan, Fk(QuestStatus.Accepted)));
        L("me available", R(me, Mastan, Fk(QuestStatus.Available, keyItems: Turn)));

        int Broken = QC.KeyItem.BrokenHarpoon, Extravagant = QC.KeyItem.ExtravagantHarpoon;
        L("ha jorin 2540", R(ha, Jorin, Fk(QuestStatus.Available)));
        L("ha jorin 2541", R(ha, Jorin, Fk(QuestStatus.Accepted, keyItems: Broken)));
        L("ha jorin 2542", R(ha, Jorin, Fk(QuestStatus.Accepted, keyItems: Extravagant)));
        L("ha jorin completed", R(ha, Jorin, Fk(QuestStatus.Completed, keyItems: Extravagant)));
        L("ha shipilolo 2543", R(ha, Shipilolo, Fk(QuestStatus.Accepted, keyItems: Broken)));
        L("ha shipilolo no harpoon", R(ha, Shipilolo, Fk(QuestStatus.Accepted, keyItems: Extravagant)));
        L("ha shipilolo completed", R(ha, Shipilolo, Fk(QuestStatus.Completed, keyItems: Broken)));

        int Bottle = QC.KeyItem.BottleOfFertilizerX, Kit = QC.KeyItem.WaypointRecalibrationKit;
        L("fg 2850", R(fg, Shipilolo, Fk(QuestStatus.Accepted)));
        L("fg bottle", R(fg, Shipilolo, Fk(QuestStatus.Accepted, keyItems: Bottle)));
        L("fg available", R(fg, Shipilolo, Fk(QuestStatus.Available)));
        L("ww 79", R(ww, Shipilolo, Fk(QuestStatus.Accepted, ww: 1)));
        L("ww var 0", R(ww, Shipilolo, Fk(QuestStatus.Accepted, ww: 0)));
        L("ww kit", R(ww, Shipilolo, Fk(QuestStatus.Accepted, ww: 1, keyItems: Kit)));
        L("ww available", R(ww, Shipilolo, Fk(QuestStatus.Available, ww: 1)));

        string T = QC.Npc.WesternAdoulin.Terwok, Mj = QC.Npc.WesternAdoulin.Marjoirelle;
        string Ok = QC.Npc.WesternAdoulin.OkaQhantari, G = QC.Npc.WesternAdoulin.Grevan;
        Check("adoulin g6:the reply kinds no flow can show",
              $"ou {T}=Event(67) ; ou {T} bit=null ; ou {T} available=null ; " +
              $"ou {Mj}=Event(68) ; ou {Mj} bit=null ; ou {Mj} available=null ; " +
              $"ou {Ok}=Event(71) ; ou {Ok} bit=null ; ou {Ok} available=null ; " +
              $"ou {G}=Event(69) ; ou {G} bit=null ; ou {G} available=null ; " +
              $"ou {Mastan}=Event(70) ; ou {Mastan} bit=null ; ou {Mastan} available=null ; " +
              "me 184=Event(184) ; me death=null ; me no turn=null ; me available=null ; " +
              "ha jorin 2540=Event(2540) ; ha jorin 2541=Event(2541) ; ha jorin 2542=Event(2542) ; " +
              "ha jorin completed=null ; ha shipilolo 2543=Event(2543) ; ha shipilolo no harpoon=null ; " +
              "ha shipilolo completed=null ; fg 2850=Event(2850) ; fg bottle=null ; fg available=null ; " +
              "ww 79=Event(79) ; ww var 0=null ; ww kit=null ; ww available=null",
              string.Join(" ; ", lines));

        // the finishes, each run on a fake at the csid (and option), in the scripts' order
        string Finish(QuestScriptDefinition def, int csid, int option = 0, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(st => st.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == Wa && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, Definition = def };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("adoulin g6:2542's finish at EXP_RATE 1.125, BAYLD_RATE 1.375 (llround: half away from zero), in order",
              "Player.CompleteQuest(Adoulin,THE_OLD_MAN_AND_THE_HARPOON);Player.AddExp(563);" +
              "Player.AddCurrency(bayld,413);Text.Special(256:BAYLD_OBTAINED,413);" +
              $"Player.DelKeyItem({Extravagant});Player.AddFame({QC.FameArea.Adoulin},30)",
              Finish(ha, 2542, setup: f =>
              {
                  f.MainNumbers["EXP_RATE"] = 1.125;
                  f.MainNumbers["BAYLD_RATE"] = 1.375;
              }));
        Check("adoulin g6:2540's finish: begun, then the Broken Harpoon with its message",
              $"Quest.Begin();Player.GiveKeyItem({Broken})", Finish(ha, 2540));
        Check("adoulin g6:2543's finish: the Broken Harpoon taken, then the Extravagant Harpoon with its message",
              $"Player.DelKeyItem({Broken});Player.GiveKeyItem({Extravagant})", Finish(ha, 2543));
        Check("adoulin g6:184's finish: Melvien's Death on option 1 only",
              $"|Player.GiveKeyItem({Death})", Finish(me, 184, 0) + "|" + Finish(me, 184, 1));
        Check("adoulin g6:2850's and 79's finishes: the key items with no message",
              $"Player.AddKeyItem({Bottle})|Player.AddKeyItem({Kit});Player.SetCharVar(WW_Need_Shipilolo,0,0)",
              Finish(fg, 2850) + "|" + Finish(ww, 79));
    }
}
