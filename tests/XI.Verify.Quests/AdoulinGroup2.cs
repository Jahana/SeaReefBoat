// XI.Verify.Quests -- quest rework B3b Adoulin group 2 (A Certain Substitute Patrolman, Transporting,
// Flavors of Our Lives, The Longest Way Round...): each definition validates clean. Their branches are the flows'
// (tests/golden/flows/adoulin.*.flow), except what no trace can see:
// - The reply kinds at the NPCs with no script and no rival above Default in any flow: Clemmar, Virsaint, Dangueubert,
//   Nylene (Pioneer Registration's replaceDefault ranks below any Event), Vastran, Fostaig, Irate Destrier,
//   Nhili Uvolep, Ploh Trishbahk (23 SoA mission handlers in its order line, none current in a flow), Cunegonde,
//   Ndah Tolohjin, Vaulois, qm_sluice_gate_6, Berghent. The kinds at Rising Solstice, Zaoso, Shipilolo, Kongramm,
//   Lamaron and Dewalt show in the #kinds flows, Masad's 83 and 104 against The Man in Black's 134 (#masad_rival),
//   Chalvava's 8 and 9 against Calamity in the Kitchen's 344 / 345 and Arciela's Promise's 360 (#chalvava_rival); all
//   are pinned here, each the Lua's kind.
// - The patrol handlers with an NPC the table does not hold: patrolData's nil (the Lua's), which equals no Prog: no
//   event, no increment. With no NPC at all the Lua's npc:getName() raises; the C# answers nothing there too (its own
//   documented choice, NpcOffset): pinned as the C#'s.
// - The completions with quest:complete false (no item reward: never false in play): the patrol routes stay.
// - The offers that begin on any option, on options 0 .. 2 (the flows finish each on one or two).
// - Masad's Prog 0 trigger: the facet calls (Budding Prospects' Timer and mustZone up to SoA 11, none after).
// - Flavors' Harvesting Point trade on the fake: its calls (Trade.HelmOnTrade(1)) and its reply (the Blightberry); the
//   flows #harvest (ENABLE_HELM_WAIT's early return) and #raise (nothing found in zone 260) run it live.
// - Each quest's LegacyPath, its own Lua file (its slot in the containers list).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup2()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 2 ==");
        var quests = new QuestScript[] { new QC.Adoulin.ACertainSubstitutePatrolman(), new QC.Adoulin.Transporting(),
            new QC.Adoulin.FlavorsOfOurLives(), new QC.Adoulin.TheLongestWayRound() };
        foreach (var quest in quests)
        {
            string name = quest.GetType().Name;
            Check($"adoulin g2:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(quest.Compile())));
        }
        // (only Flavors' states match on Prog: b.Steps declares the values its finishes set)
        Check("adoulin g2:the declared steps", " |  | 1,2,3 | ",
              string.Join(" | ", quests.Select(q => string.Join(",", q.Compile().DeclaredSteps))));

        // the statuses (and Prog) each state matches, state by state
        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => (m.AnyStatus ? "any" : m.Status.ToString()) + (m.Step is int p ? "#" + p : "")))));
        Check("adoulin g2:the states' statuses",
              "Available Accepted | Available Accepted | " +
              "Available Accepted#0 Accepted#1 Accepted#2 Accepted#3 Accepted#3 Completed | Available Accepted",
              string.Join(" | ", quests.Select(States)));

        Check("adoulin g2:each quest's LegacyPath is its own Lua file",
              "A_Certain_Substitute_Patrolman ; Transporting ; Flavors_of_Our_Lives ; The_Longest_Way_Round",
              string.Join(" ; ", quests.Select(q => q.LegacyPath.Substring("scripts/quests/adoulin/".Length))));

        // the reward tables as the Lua's (Flavors' has no fame area);
        // fame / fameArea / bayld / exp / gil / item / title
        string Rw(QuestScript q)
        {
            var r = q.Compile().Reward;
            return $"{r.Fame?.ToString() ?? "-"}/{r.FameArea?.ToString() ?? "-"}/{r.Bayld}/{r.Exp}/" +
                   $"{r.Gil?.ToString() ?? "-"}/{r.Item?.ToString() ?? "-"}/{r.Title?.ToString() ?? "-"}";
        }
        int ad = QC.FameArea.Adoulin;
        Check("adoulin g2:the rewards",
              $"-/{ad}/500/1000/-/-/- | -/{ad}/300/1000/-/-/- | -/-/300/500/-/-/{QC.Title.PotationPathfinder} | " +
              $"-/{ad}/500/1000/-/-/-",
              string.Join(" | ", quests.Select(Rw)));

        RunAdoulinGroup2Patrols();
        RunAdoulinGroup2Kinds();
        RunAdoulinGroup2Finishes();
    }

    static Fake AD2Fake(QuestStatus st, int prog = 0, string npc = null)
    {
        var f = new Fake { Status = st, Step = prog, UniqueDay = 8000 };
        if (npc != null) f.Npc = new QuestNpcFake(1) { Name = npc };
        return f;
    }

    // the one finish of that zone and csid, run on the fake: its calls
    static string AD2Fin(QuestScriptDefinition d, ushort zone, int csid, Fake f)
    {
        var fin = d.States.SelectMany(s => s.Handlers)
            .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Zone == zone && h.Id == csid);
        f.Csid = csid;
        fin.Run(f);
        return string.Join(";", f.Calls);
    }

    // the two patrols: each NPC's event at its own Prog and nothing at the next; the finishes add 1 only at the NPC's
    // own Prog; an NPC the table does not hold, or none, answers nothing and adds nothing
    static void RunAdoulinGroup2Patrols()
    {
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger;
        var pd = new QC.Adoulin.ACertainSubstitutePatrolman().Compile();
        var lw = new QC.Adoulin.TheLongestWayRound().Compile();
        ushort west = QC.Zone.WesternAdoulin, east = QC.Zone.EasternAdoulin;
        string[] westNpcs = { QC.Npc.WesternAdoulin.Zaoso, QC.Npc.WesternAdoulin.Clemmar,
            QC.Npc.WesternAdoulin.Kongramm,
            QC.Npc.WesternAdoulin.Virsaint, QC.Npc.WesternAdoulin.Shipilolo, QC.Npc.WesternAdoulin.Dangueubert,
            QC.Npc.WesternAdoulin.Nylene };
        string[] eastNpcs = { QC.Npc.EasternAdoulin.Fostaig, QC.Npc.EasternAdoulin.Lamaron,
            QC.Npc.EasternAdoulin.IrateDestrier, QC.Npc.EasternAdoulin.NhiliUvolep, QC.Npc.EasternAdoulin.PlohTrishbahk,
            QC.Npc.EasternAdoulin.Cunegonde, QC.Npc.EasternAdoulin.NdahTolohjin };

        string Patrol(QuestScriptDefinition d, ushort zone, string[] npcs, int csid0) => string.Join(" ; ",
            npcs.Select((n, i) =>
                $"{n}:{AU3Npc(d, Tr, zone, n, AD2Fake(QuestStatus.Accepted, i, n))}" +
                $"/{AU3Npc(d, Tr, zone, n, AD2Fake(QuestStatus.Accepted, i + 1, n))}" +
                $"/{AD2Fin(d, zone, csid0 + i, AD2Fake(QuestStatus.Accepted, i, n))}" +
                $"/{AD2Fin(d, zone, csid0 + i, AD2Fake(QuestStatus.Accepted, i + 1, n))}"));
        Check("adoulin g2:the western patrol (own Prog / next Prog / finish at own / finish at next)",
              string.Join(" ; ", westNpcs.Select((n, i) =>
                  $"{n}:ProgressEvent({2553 + i})/null/Quest.IncrementVar(Prog,1)/")),
              Patrol(pd, west, westNpcs, 2553));
        Check("adoulin g2:the eastern patrol (own Prog / next Prog / finish at own / finish at next)",
              string.Join(" ; ", eastNpcs.Select((n, i) =>
                  $"{n}:ProgressEvent({2513 + i})/null/Quest.IncrementVar(Prog,1)/")),
              Patrol(lw, east, eastNpcs, 2513));

        // a finish run with another NPC's name: no offset, no increment (the Lua's nil); a trigger or finish with no
        // NPC: nothing either, the C#'s own choice (the Lua's npc:getName() raises on a nil npc); a handler that throws
        // shows as "threw <type>": the check fails cleanly instead of ending the run
        string Try(Func<string> f)
        {
            try { return f(); }
            catch (Exception e) { return "threw " + e.GetType().Name; }
        }
        Check("adoulin g2:the patrols with an NPC not in the table (the Lua's), or none (the C#'s choice)",
              "null | null | [] | [] | [] | []",
              string.Join(" | ",
                  Try(() => AU3Npc(pd, Tr, west, QC.Npc.WesternAdoulin.Zaoso, AD2Fake(QuestStatus.Accepted, 0))),
                  Try(() => AU3Npc(lw, Tr, east, QC.Npc.EasternAdoulin.Fostaig, AD2Fake(QuestStatus.Accepted, 0))),
                  Try(() => "[" + AD2Fin(pd, west, 2553,
                      AD2Fake(QuestStatus.Accepted, 0, QC.Npc.WesternAdoulin.RisingSolstice)) + "]"),
                  Try(() => "[" + AD2Fin(pd, west, 2553, AD2Fake(QuestStatus.Accepted, 0)) + "]"),
                  Try(() => "[" + AD2Fin(lw, east, 2513,
                      AD2Fake(QuestStatus.Accepted, 0, QC.Npc.EasternAdoulin.Vastran)) + "]"),
                  Try(() => "[" + AD2Fin(lw, east, 2513, AD2Fake(QuestStatus.Accepted, 0)) + "]")));
    }

    // the replies the patrols' traces cannot tell apart from another kind, and Transporting's and Flavors'
    static void RunAdoulinGroup2Kinds()
    {
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        ushort west = QC.Zone.WesternAdoulin, east = QC.Zone.EasternAdoulin, rala = QC.Zone.RalaWaterways,
               yahse = QC.Zone.YahseHuntingGrounds;
        var pd = new QC.Adoulin.ACertainSubstitutePatrolman().Compile();
        var tp = new QC.Adoulin.Transporting().Compile();
        var fl = new QC.Adoulin.FlavorsOfOurLives().Compile();
        var lw = new QC.Adoulin.TheLongestWayRound().Compile();
        Fake Soa(QuestStatus st, int mission, int prog = 0)
        {
            var f = AD2Fake(st, prog);
            f.CurrentMissions[QC.MissionLog.Soa] = mission;
            return f;
        }
        Fake Fame(int level)
        {
            var f = AD2Fake(QuestStatus.Available);
            f.Fame[QC.FameArea.Adoulin] = level;
            return f;
        }
        Fake Fl(QuestStatus st, int prog = 0, bool berry = false, int timer = 0)
        {
            var f = AD2Fake(st, prog);
            if (berry) f.KeyItems[QC.KeyItem.Blightberry] = true;
            f.Vars["Timer"] = timer;
            f.CurrentMissions[QC.MissionLog.Soa] = QC.Mission.Soa.LifeOnTheFrontier;
            return f;
        }
        int lotf = QC.Mission.Soa.LifeOnTheFrontier;
        string Rs = QC.Npc.WesternAdoulin.RisingSolstice, Va = QC.Npc.EasternAdoulin.Vastran,
               Vl = QC.Npc.WesternAdoulin.Vaulois, Ko = QC.Npc.WesternAdoulin.Kongramm,
               Sl = QC.Npc.RalaWaterways.qmsluicegate6, Be = QC.Npc.WesternAdoulin.Berghent,
               Ma = QC.Npc.WesternAdoulin.Masad, De = QC.Npc.WesternAdoulin.Dewalt, Ch = QC.Npc.RalaWaterways.Chalvava;

        L("pd offer", AU3Npc(pd, Tr, west, Rs, Soa(QuestStatus.Available, lotf)));
        L("pd prog 0", AU3Npc(pd, Tr, west, Rs, Soa(QuestStatus.Accepted, lotf, 0)));
        L("pd prog 7", AU3Npc(pd, Tr, west, Rs, Soa(QuestStatus.Accepted, lotf, 7)));
        L("lw offer", AU3Npc(lw, Tr, east, Va, Soa(QuestStatus.Available, lotf)));
        L("lw prog 6", AU3Npc(lw, Tr, east, Va, Soa(QuestStatus.Accepted, lotf, 6)));
        L("lw prog 7", AU3Npc(lw, Tr, east, Va, Soa(QuestStatus.Accepted, lotf, 7)));
        L("tp offer", AU3Npc(tp, Tr, west, Vl, Fame(2)));
        L("tp kongramm 0", AU3Npc(tp, Tr, west, Ko, AD2Fake(QuestStatus.Accepted, 0)));
        L("tp sluice 1", AU3Npc(tp, Tr, rala, Sl, AD2Fake(QuestStatus.Accepted, 1)));
        L("tp vaulois 2", AU3Npc(tp, Tr, west, Vl, AD2Fake(QuestStatus.Accepted, 2)));
        L("tp vaulois 0", AU3Npc(tp, Tr, west, Vl, AD2Fake(QuestStatus.Accepted, 0)));
        var refused = Fl(QuestStatus.Available);
        refused.PlayerLocalVars["Flavors_of_Our_Lives_Refused"] = 1;
        L("fl offer", AU3Npc(fl, Tr, west, Be, Fl(QuestStatus.Available)));
        L("fl refused", AU3Npc(fl, Tr, west, Be, refused));
        L("fl berghent 0", AU3Npc(fl, Tr, west, Be, Fl(QuestStatus.Accepted, 0)));
        L("fl masad 0", AU3Npc(fl, Tr, west, Ma, Fl(QuestStatus.Accepted, 0)));
        L("fl masad 1 timer 8001", AU3Npc(fl, Tr, west, Ma, Fl(QuestStatus.Accepted, 1, timer: 8001)));
        L("fl masad 1 timer 8000", AU3Npc(fl, Tr, west, Ma, Fl(QuestStatus.Accepted, 1, timer: 8000)));
        L("fl dewalt 1", AU3Npc(fl, Tr, west, De, Fl(QuestStatus.Accepted, 1)));
        L("fl dewalt 2", AU3Npc(fl, Tr, west, De, Fl(QuestStatus.Accepted, 2)));
        L("fl chalvava 2", AU3Npc(fl, Tr, rala, Ch, Fl(QuestStatus.Accepted, 2)));
        L("fl chalvava 3", AU3Npc(fl, Tr, rala, Ch, Fl(QuestStatus.Accepted, 3)));
        L("fl dewalt 3", AU3Npc(fl, Tr, west, De, Fl(QuestStatus.Accepted, 3)));
        L("fl berghent 3", AU3Npc(fl, Tr, west, Be, Fl(QuestStatus.Accepted, 3)));
        L("fl berghent 3 berry", AU3Npc(fl, Tr, west, Be, Fl(QuestStatus.Accepted, 3, berry: true)));
        L("fl completed", AU3Npc(fl, Tr, west, Be, Fl(QuestStatus.Completed)));
        Check("adoulin g2:the reply kinds",
              "pd offer=ProgressEvent(2550) ; pd prog 0=Event(2551) ; pd prog 7=ProgressEvent(2552) ; " +
              "lw offer=ProgressEvent(2510) ; lw prog 6=Event(2511) ; lw prog 7=ProgressEvent(2512) ; " +
              "tp offer=ProgressEvent(2590) ; tp kongramm 0=ProgressEvent(2592) ; tp sluice 1=ProgressEvent(2802) ; " +
              "tp vaulois 2=ProgressEvent(2591) ; tp vaulois 0=null ; fl offer=ProgressEvent(80) ; " +
              "fl refused=ProgressEvent(81) ; fl berghent 0=Event(82) ; fl masad 0=ProgressEvent(83) ; " +
              "fl masad 1 timer 8001=ProgressEvent(104) ; fl masad 1 timer 8000=Event(104):oncePerZone() ; " +
              "fl dewalt 1=ProgressEvent(85) ; fl dewalt 2=Event(105) ; fl chalvava 2=ProgressEvent(8) ; " +
              "fl chalvava 3=Event(9) ; fl dewalt 3=Event(105) ; fl berghent 3=Event(82) ; " +
              "fl berghent 3 berry=ProgressEvent(87) ; fl completed=Event(88)",
              string.Join(" ; ", lines));

        // the Harvesting Point trade on the fake (the flow #harvest runs it live): the helm call, then the Blightberry;
        // with the Blightberry held, no Prog 3 trade state is up
        var hv = Fl(QuestStatus.Accepted, 3);
        hv.TradeItems[QC.Item.Sickle] = 1;
        string reply = AU3Npc(fl, Td, yahse, QC.Npc.YahseHuntingGrounds.HarvestingPoint, hv);
        Check("adoulin g2:the Harvesting Point trade (calls / reply / handlers up with the Blightberry)",
              "Trade.HelmOnTrade(1) / KeyItem(" + QC.KeyItem.Blightberry + ") / 0",
              string.Join(";", hv.Calls) + " / " + reply + " / " +
              AU3Up(fl, Fl(QuestStatus.Accepted, 3, berry: true)).Count(h => h.Kind == Td));

        // Masad's Prog 0 trigger: Budding Prospects' Timer and mustZone up to SoA 11 (0, 11), none after (12)
        string Masad(int mission)
        {
            var f = Fl(QuestStatus.Accepted, 0);
            f.CurrentMissions[QC.MissionLog.Soa] = mission;
            AU3Npc(fl, Tr, west, Ma, f);
            return string.Join(";", f.Calls);
        }
        int soa = QC.MissionLog.Soa, bp = QC.Mission.Soa.BuddingProspects;
        Check("adoulin g2:Masad's Prog 0 trigger at SoA 0 / 11 / 12",
              $"Player.SetMissionVar({soa},{bp},Timer,8001);Player.SetMissionMustZone({soa},{bp});" +
              "Quest.SetVar(Timer,8001,0) | " +
              $"Player.SetMissionVar({soa},{bp},Timer,8001);Player.SetMissionMustZone({soa},{bp});" +
              "Quest.SetVar(Timer,8001,0) | Quest.SetVar(Timer,8001,0)",
              string.Join(" | ", Masad(0), Masad(bp), Masad(bp + 1)));
    }

    // the finishes: the offers on options 0 .. 2, the completions with quest:complete true and false
    static void RunAdoulinGroup2Finishes()
    {
        var pd = new QC.Adoulin.ACertainSubstitutePatrolman().Compile();
        var tp = new QC.Adoulin.Transporting().Compile();
        var fl = new QC.Adoulin.FlavorsOfOurLives().Compile();
        var lw = new QC.Adoulin.TheLongestWayRound().Compile();
        ushort west = QC.Zone.WesternAdoulin, east = QC.Zone.EasternAdoulin, rala = QC.Zone.RalaWaterways;
        string Opt(QuestScriptDefinition d, ushort zone, int csid, int option, QuestStatus st = QuestStatus.Available)
        {
            var f = AD2Fake(st);
            f.Option = option;
            return AD2Fin(d, zone, csid, f);
        }
        string Opts(QuestScriptDefinition d, ushort zone, int csid) =>
            string.Join(" / ", new[] { 0, 1, 2 }.Select(o => Opt(d, zone, csid, o)));
        int wr = QC.KeyItem.WesternAdoulinPatrolRoute, er = QC.KeyItem.EasternAdoulinPatrolRoute,
            mp = QC.KeyItem.MisdeliveredParcel;
        Check("adoulin g2:the offers on options 0 / 1 / 2",
              string.Join(" / ", Enumerable.Repeat($"Quest.Begin();Player.GiveKeyItem({wr})", 3)) + " | " +
              string.Join(" / ", Enumerable.Repeat($"Quest.Begin();Player.GiveKeyItem({er})", 3)) + " | " +
              string.Join(" / ", Enumerable.Repeat($"Quest.Begin();Player.GiveKeyItem({mp})", 3)) + " | " +
              "Player.SetLocalVar(Flavors_of_Our_Lives_Refused,1) / Quest.Begin() / " +
              "Player.SetLocalVar(Flavors_of_Our_Lives_Refused,1) | " +
              " / Quest.Begin() / ",
              string.Join(" | ", Opts(pd, west, 2550), Opts(lw, east, 2510), Opts(tp, west, 2590), Opts(fl, west, 80),
                  Opts(fl, west, 81)));

        string Done(QuestScriptDefinition d, ushort zone, int csid, bool complete)
        {
            var f = AD2Fake(QuestStatus.Accepted, 7);
            f.CompleteResult = complete;
            return AD2Fin(d, zone, csid, f);
        }
        Check("adoulin g2:the completions, completed / not",
              $"Quest.Complete();Player.DelKeyItem({wr});Text.Special({west}:KEYITEM_LOST,{wr}) | Quest.Complete() | " +
              $"Quest.Complete();Player.DelKeyItem({er});Text.Special({east}:LOST_KEYITEM,{er}) | Quest.Complete() | " +
              "Quest.Complete() | Quest.Complete() | Quest.Complete() | Quest.Complete()",
              string.Join(" | ", Done(pd, west, 2552, true), Done(pd, west, 2552, false), Done(lw, east, 2512, true),
                  Done(lw, east, 2512, false), Done(tp, west, 2591, true), Done(tp, west, 2591, false),
                  Done(fl, west, 87, true), Done(fl, west, 87, false)));

        Check("adoulin g2:the Prog finishes (Transporting 2592, 2802; Flavors 83, 85, 8)",
              $"Quest.SetStep(1) | Player.DelKeyItem({mp});Text.Special({rala}:KEYITEM_LOST,{mp});Quest.SetStep(2) | " +
              "Quest.SetStep(1) | Quest.SetStep(2) | Quest.SetStep(3)",
              string.Join(" | ", Opt(tp, west, 2592, 0, QuestStatus.Accepted),
                  Opt(tp, rala, 2802, 0, QuestStatus.Accepted),
                  Opt(fl, west, 83, 0, QuestStatus.Accepted), Opt(fl, west, 85, 0, QuestStatus.Accepted),
                  Opt(fl, rala, 8, 0, QuestStatus.Accepted)));
    }
}
