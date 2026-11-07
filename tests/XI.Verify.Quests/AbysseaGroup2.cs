// XI.Verify.Quests -- quest rework B3b Abyssea group 2 (A Sea Dog's Summons .. Fear of the Dark III): each definition
// validates clean and declares its steps (Death and Rebirth, Emissaries of God: 1; The Wyrm God, Meanwhile, Back on
// Abyssea: 1, 2; the rest none). Their branches are the flows' (tests/golden/flows/abyssea.*.flow), except what no
// trace can see:
// - A Sea Dog's Summons' only section: its check is the hour from 18:00 to 4:59 (user ruling 2026-11-04, kept Lua bugs:
//   the Lua's VanadielHour() >= 18 and VanadielHour() < 5 was never true). The check is pinned here on the fake at
//   each side of both bounds; the flow shows 12:00, 20:00 and 04:00.
// - The Wyrm God's 32001 finish (the flow waives it): its check compares the battlefieldWin local var with
//   xi.battlefield.id.THE_WYRM_GOD, which is nil, so no value of the var sets Prog 2.
// - The reply kinds no flow can show. A Progress beats an Event and equal kinds rotate, so a kind shows only against a
//   rival container or an NPC script. The flows show it at Joachim (his own onTrigger: 343, 344 and 345 talked to
//   twice) and at Prishe (203 / 204 rival in the_wyrm_god#rivals; 206 / 207 rotate). Halver, Curilla, Secodiand and
//   TR_Entrance have one container each and no script or default, and a zone-in's csid always plays: those are
//   pinned here, each the Lua's kind (so a later rival at one of them is caught here too). 206 and 207 are pinned as
//   well: a replaceDefault sets the priority whatever the kind (a progressEvent replaceDefault plays the same).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAbysseaGroup2()
    {
        Console.WriteLine("== quest rework B3b Abyssea group 2 ==");
        void One(string name, QuestScript quest, string steps)
        {
            Check($"abyssea g2:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(quest.Compile())));
            Check($"abyssea g2:{name} declares steps [{steps}]", steps,
                  string.Join(",", quest.Compile().DeclaredSteps));
        }
        One("A Sea Dog's Summons", new QC.Abyssea.ASeaDogsSummons(), "");
        One("Death and Rebirth", new QC.Abyssea.DeathAndRebirth(), "1");
        One("Emissaries of God", new QC.Abyssea.EmissariesOfGod(), "1");
        One("Beneath a Blood Red Sky", new QC.Abyssea.BeneathABloodRedSky(), "");
        One("The Wyrm God", new QC.Abyssea.TheWyrmGod(), "1,2");
        One("Meanwhile, Back on Abyssea", new QC.Abyssea.MeanwhileBackOnAbyssea(), "1,2");
        One("Lost Memories", new QC.Abyssea.LostMemories(), "");
        One("An Eye for Revenge", new QC.Abyssea.AnEyeForRevenge(), "");
        One("Fear of the Dark III", new QC.Abyssea.FearOfTheDarkIii(), "");

        // the statuses and steps each state matches, state by state (A Sea Dog's Summons' hour section included: its
        // accepted status is the Lua's)
        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => (m.AnyStatus ? "any" : m.Status.ToString()) + (m.Step is int st ? ":" + st : "")))));
        Check("abyssea g2:the states' statuses and steps",
              "Accepted | Accepted Accepted:1 | Accepted Accepted:1 | Accepted | Accepted Completed | " +
              "Accepted Accepted:1 Accepted:2 Completed | Available Available Accepted | " +
              "Available Available Accepted Completed | Available Accepted/Completed",
              string.Join(" | ", new QuestScript[] { new QC.Abyssea.ASeaDogsSummons(), new QC.Abyssea.DeathAndRebirth(),
                  new QC.Abyssea.EmissariesOfGod(), new QC.Abyssea.BeneathABloodRedSky(), new QC.Abyssea.TheWyrmGod(),
                  new QC.Abyssea.MeanwhileBackOnAbyssea(), new QC.Abyssea.LostMemories(),
                  new QC.Abyssea.AnEyeForRevenge(), new QC.Abyssea.FearOfTheDarkIii() }.Select(States)));

        RunAbysseaGroup2Unreachable();
        RunAbysseaGroup2Kinds();
    }

    // A Sea Dog's Summons' section (A_Sea_Dogs_Summons.lua:16-35): the zone-in's reply and the finish, each with its
    // zone, on the fake (accepted); the finish's calls with Complete() true; the check at 4, 5, 17 and 18. The Wyrm
    // God's 32001 (The_Wyrm_God.lua:48-59): accepted at Prog 1, with battlefieldWin unset, 0, 1 and 184: no call
    static void RunAbysseaGroup2Unreachable()
    {
        var sd = new QC.Abyssea.ASeaDogsSummons().Compile();
        var hs = sd.States[0].Handlers;
        var zi = hs.Single(h => h.Kind == QuestHandlerKind.OnZoneIn);
        var fin = hs.Single(h => h.Kind == QuestHandlerKind.OnEventFinish);
        var r = zi.Fixed ?? zi.Reply(new Fake { Status = QuestStatus.Accepted });
        var g = new Fake { Status = QuestStatus.Accepted, GiveResult = true };
        fin.Run(g);
        ushort hall = QC.Zone.HallOfTheGods;
        string reply = r == null ? "null" : QuestContextFake.Describe(r);
        Check("abyssea g2:A Sea Dog's Summons' section: the Hall of the Gods zone-in 6; its finish " +
              "completes the quest and adds Death and Rebirth",
              $"{hall}:Event(6) {hall}/6:Quest.Complete();Player.AddQuest(Abyssea,DEATH_AND_REBIRTH)",
              $"{zi.Zone}:{reply} {fin.Zone}/{fin.Id}:{string.Join(";", g.Calls)}");
        Check("abyssea g2:A Sea Dog's Summons' section holds from 18:00 to 4:59 (user ruling 2026-11-04: `or`, not " +
              "the Lua's `and`)", "4:y,5:n,17:n,18:y", string.Join(",", new[] { 4, 5, 17, 18 }.Select(h =>
                  sd.States[0].Matches.Any(m => m.When(new Fake { Status = QuestStatus.Accepted, Hour = h }))
                      ? h + ":y" : h + ":n")));

        var wg = new QC.Abyssea.TheWyrmGod().Compile();
        var fins = wg.States.SelectMany(s => s.Handlers).Where(h => h.Kind == QuestHandlerKind.OnEventFinish).ToList();
        var bf = fins.SingleOrDefault(h => h.Id == 32001);
        string Win(int? win)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 1 };
            if (win is int w) f.PlayerLocalVars["battlefieldWin"] = w;
            bf.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("abyssea g2:The Wyrm God's 32001 at Prog 1 (battlefieldWin unset, 0, 1, 184): never sets Prog 2",
              $"{QC.Zone.AbysseaEmpyrealParadox}/32001:|||",
              bf == null ? "no 32001 finish: " + string.Join(",", fins.Select(h => h.Id))
                         : $"{bf.Zone}/{bf.Id}:{Win(null)}|{Win(0)}|{Win(1)}|{Win(184)}");
    }

    // the reply kinds no flow can show (above), each reply as the fake state that offers it gives it
    static void RunAbysseaGroup2Kinds()
    {
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, int step = 0) => new Fake { Status = st, Step = step };
        ushort lt = QC.Zone.AbysseaLaTheine, ep = QC.Zone.AbysseaEmpyrealParadox, hall = QC.Zone.HallOfTheGods;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        var lm = new QC.Abyssea.LostMemories().Compile();
        var ey = new QC.Abyssea.AnEyeForRevenge().Compile();
        var fd = new QC.Abyssea.FearOfTheDarkIii().Compile();
        var wg = new QC.Abyssea.TheWyrmGod().Compile();
        var mw = new QC.Abyssea.MeanwhileBackOnAbyssea().Compile();
        var dr = new QC.Abyssea.DeathAndRebirth().Compile();
        var eg = new QC.Abyssea.EmissariesOfGod().Compile();
        var bn = new QC.Abyssea.BeneathABloodRedSky().Compile();
        string Halver = QC.Npc.AbysseaLaTheine.Halver, Curilla = QC.Npc.AbysseaLaTheine.Curilla,
               Secodiand = QC.Npc.AbysseaLaTheine.Secodiand, Prishe = QC.Npc.AbysseaEmpyrealParadox.Prishe;
        Fake Fame(int level)
        {
            var f = Fk(QuestStatus.Available);
            f.Fame[QC.FameArea.AbysseaLatheine] = level;
            return f;
        }
        Fake Lm(QuestStatus own, QuestStatus lost)
        {
            var f = Fk(own);
            f.QuestStatuses[$"{QuestLog.Abyssea}/LOST_MEMORIES"] = lost;
            return f;
        }
        Fake Eye() { var f = Fk(QuestStatus.Accepted); f.KeyItems[QC.KeyItem.LugarhoosEyeball] = true; return f; }
        Fake Items(QuestStatus st, int item, int qty) { var f = Fk(st); f.TradeItems[item] = qty; return f; }
        string Zi(QuestScriptDefinition d, ushort zone, Fake f) =>
            AU1Describe(AU3Up(d, f).Single(x => x.Kind == QuestHandlerKind.OnZoneIn && x.Zone == zone), f);
        Fake Hour(int step, int hour) { var f = Fk(QuestStatus.Accepted, step); f.Hour = hour; return f; }

        L("lm halver fame 4", AU3Npc(lm, Tr, lt, Halver, Fame(4)));
        L("lm halver fame 5", AU3Npc(lm, Tr, lt, Halver, Fame(5)));
        L("lm halver accepted", AU3Npc(lm, Tr, lt, Halver, Fk(QuestStatus.Accepted)));
        L("lm halver trade", AU3Npc(lm, Td, lt, Halver, Items(QuestStatus.Accepted, QC.Item.LambentScale, 2)));
        L("ey curilla lm accepted", AU3Npc(ey, Tr, lt, Curilla, Lm(QuestStatus.Available, QuestStatus.Accepted)));
        L("ey curilla lm completed", AU3Npc(ey, Tr, lt, Curilla, Lm(QuestStatus.Available, QuestStatus.Completed)));
        L("ey curilla no eyeball", AU3Npc(ey, Tr, lt, Curilla, Fk(QuestStatus.Accepted)));
        L("ey curilla eyeball", AU3Npc(ey, Tr, lt, Curilla, Eye()));
        L("ey curilla completed", AU3Npc(ey, Tr, lt, Curilla, Fk(QuestStatus.Completed)));
        L("fd secodiand available", AU3Npc(fd, Tr, lt, Secodiand, Fk(QuestStatus.Available)));
        L("fd secodiand accepted", AU3Npc(fd, Tr, lt, Secodiand, Fk(QuestStatus.Accepted)));
        L("fd secodiand completed", AU3Npc(fd, Tr, lt, Secodiand, Fk(QuestStatus.Completed)));
        L("fd secodiand trade", AU3Npc(fd, Td, lt, Secodiand, Items(QuestStatus.Accepted, QC.Item.ClionidWing, 3)));
        L("fd secodiand trade done",
          AU3Npc(fd, Td, lt, Secodiand, Items(QuestStatus.Completed, QC.Item.ClionidWing, 3)));
        L("wg tr_entrance", AU3Npc(wg, Tr, ep, QC.Npc.AbysseaEmpyrealParadox.TREntrance, Fk(QuestStatus.Accepted)));
        L("wg prishe 2", AU3Npc(wg, Tr, ep, Prishe, Fk(QuestStatus.Accepted, 2)));
        L("wg prishe done", AU3Npc(wg, Tr, ep, Prishe, Fk(QuestStatus.Completed)));
        L("mw prishe 2", AU3Npc(mw, Tr, ep, Prishe, Fk(QuestStatus.Accepted, 2)));
        L("mw prishe done", AU3Npc(mw, Tr, ep, Prishe, Fk(QuestStatus.Completed)));
        L("dr hall", Zi(dr, hall, Hour(1, 18)));
        L("eg hall", Zi(eg, hall, Hour(1, 4)));
        L("mw hall", Zi(mw, hall, Hour(1, 20)));
        L("bn paradox", Zi(bn, ep, Fk(QuestStatus.Accepted)));
        Check("abyssea g2:the reply kinds no flow can show",
              "lm halver fame 4=Event(162) ; lm halver fame 5=Event(165) ; lm halver accepted=Event(163) ; " +
              "lm halver trade=Event(164) ; ey curilla lm accepted=Event(189) ; ey curilla lm completed=Event(190) ; " +
              "ey curilla no eyeball=Event(191) ; ey curilla eyeball=Event(192) ; " +
              "ey curilla completed=ProgressEvent(193) ; fd secodiand available=Event(161) ; " +
              "fd secodiand accepted=Event(159) ; fd secodiand completed=Event(159) ; " +
              "fd secodiand trade=Event(160) ; " +
              "fd secodiand trade done=Event(160) ; wg tr_entrance=ProgressEvent(208) ; " +
              "wg prishe 2=ProgressEvent(203) ; wg prishe done=Event(206):replaceDefault() ; " +
              "mw prishe 2=ProgressEvent(204) ; mw prishe done=Event(207):replaceDefault() ; dr hall=Event(7) ; " +
              "eg hall=Event(8) ; mw hall=Event(9) ; bn paradox=Event(202)", string.Join(" ; ", lines));
    }
}
