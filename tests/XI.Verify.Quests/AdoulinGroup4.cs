// XI.Verify.Quests -- quest rework B3b Adoulin group 4 (Dances with Luopans, the Geomancer unlock; Lerene's Lament):
// each definition validates clean and declares the steps it sets. Their branches are the flows'
// (tests/golden/flows/adoulin.dances_with_luopans*.flow, adoulin.lerenes_lament.flow), except what no trace can see:
// - The reply kinds. Sylvie and Lerene have no script (defaults 38 and 25, rank Default); the Ergon Loci none. Each
//   NPC has one container up at a time. A Progress beats an Event and equal kinds rotate, so no trace tells a
//   Progress from an Event here. All are pinned, each the Lua's kind; the offer is down below ADVANCED_JOB_LEVEL.
// - The healing hooks' numbers: the random wait's bounds (3 healing ticks, at most 8 minutes), RestUntil and the
//   timer's milliseconds (the flows wait a day of wall clock and 481 s, past any wait), the rest timer's edges
//   (RestUntil unset, the second before it, the second of it); the exports' names and module.
// - Sylvie's completed trigger clearing MatreBellPayment, and 37's finish clearing it before the option is read
//   (each is followed by the other before any read, so no flow tells); 37's finish on a payment neither 1 nor 2.
// - The Ergon Locus with the soil held: no giveKeyItem (npcUtil.giveKeyItem skips a held key item: no trace tells).
// - Lerene's 24 with quest:complete false (no item reward: never false in play): no trade taken, no key item.
// - Each quest's LegacyPath, its own Lua file (its slot in the containers list, and the module the exports claim);
//   the rewards.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup4()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 4 ==");
        var quests = new QuestScript[] { new QC.Adoulin.DancesWithLuopans(), new QC.Adoulin.LerenesLament() };
        foreach (var quest in quests)
        {
            string name = quest.GetType().Name;
            Check($"adoulin g4:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(quest.Compile())));
        }
        Check("adoulin g4:the steps each quest sets", "1 | ",
              string.Join(" | ", quests.Select(q => string.Join(",", q.Compile().DeclaredSteps))));

        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => m.AnyStatus ? "any" : m.Status.ToString()))));
        Check("adoulin g4:the states' statuses", "Available Accepted Completed | Available Accepted",
              string.Join(" | ", quests.Select(States)));

        Check("adoulin g4:each quest's LegacyPath is its own Lua file", "Dances_with_Luopans ; Lerenes_Lament",
              string.Join(" ; ", quests.Select(q => q.LegacyPath.Substring("scripts/quests/adoulin/".Length))));

        // fame / fameArea / bayld / exp / gil / item (no fame field: npcUtil.completeQuest's 30, as the traces show);
        // Dances with Luopans has no reward table
        string Rw(QuestScript q)
        {
            var r = q.Compile().Reward;
            if (r == null) return "none";
            return $"{r.Fame?.ToString() ?? "-"}/{r.FameArea}/{r.Bayld}/{r.Exp?.ToString() ?? "-"}/" +
                   $"{r.Gil?.ToString() ?? "-"}/{r.Item?.ToString() ?? "-"}";
        }
        Check("adoulin g4:the rewards", $"none | -/{QC.FameArea.Adoulin}/500/-/-/-",
              string.Join(" | ", quests.Select(Rw)));

        // the healing effect's hooks: two exports of the quest's own module (null: its LegacyPath), and the callbacks
        var dw = quests[0].Compile();
        Check("adoulin g4:Dances' exports (global -> callback, module) and callbacks",
              "xi.dancesWithLuopans.onHealing->onHealing,(LegacyPath) ; " +
              "xi.dancesWithLuopans.onEffectLose->onEffectLose,(LegacyPath) | restTimer,onHealing,onEffectLose",
              string.Join(" ; ", dw.Exports.Select(e =>
                  $"{e.Global}->{e.Callback.Name},{e.Module ?? "(LegacyPath)"}")) +
              " | " + string.Join(",", dw.Callbacks.Select(c => c.Name)));

        RunAdoulinGroup4Kinds();
        RunAdoulinGroup4Hooks();
        RunAdoulinGroup4Finishes();
    }

    static Fake AD4Fake(QuestStatus st, int prog = 0)
    {
        var f = new Fake { Status = st, MainLevel = 99 };
        f.MainSettings["ADVANCED_JOB_LEVEL"] = 30;
        if (prog != 0) f.Step = prog;
        return f;
    }

    // every reply of the two, each as the fake state that offers it gives it
    static void RunAdoulinGroup4Kinds()
    {
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        ushort wa = QC.Zone.WesternAdoulin, ork = QC.Zone.OuterRakaznar;
        var dw = new QC.Adoulin.DancesWithLuopans().Compile();
        var ll = new QC.Adoulin.LerenesLament().Compile();
        string Sylvie = QC.Npc.WesternAdoulin.Sylvie, Lerene = QC.Npc.OuterRakaznar.Lerene;
        Fake With(Fake f, Action<Fake> set)
        {
            set(f);
            return f;
        }

        L("dw offer", AU3Npc(dw, Tr, wa, Sylvie, AD4Fake(QuestStatus.Available)));
        L("dw declined", AU3Npc(dw, Tr, wa, Sylvie,
                                With(AD4Fake(QuestStatus.Available), f => f.Vars["Declined"] = 1)));
        var low = With(AD4Fake(QuestStatus.Available), f =>
        {
            f.MainLevel = 98;
            f.MainSettings["ADVANCED_JOB_LEVEL"] = 99;
        });
        L("dw offer below the level (states up)", AU3Up(dw, low).Count().ToString());
        var at = With(AD4Fake(QuestStatus.Available), f => f.MainSettings["ADVANCED_JOB_LEVEL"] = 99);
        L("dw offer at the level", AU3Npc(dw, Tr, wa, Sylvie, at));
        L("dw prog 0", AU3Npc(dw, Tr, wa, Sylvie, AD4Fake(QuestStatus.Accepted)));
        L("dw luopan", AU3Npc(dw, Tr, wa, Sylvie,
                              With(AD4Fake(QuestStatus.Accepted), f => f.KeyItems[QC.KeyItem.Luopan] = true)));
        L("dw prog 1", AU3Npc(dw, Tr, wa, Sylvie,
                              With(AD4Fake(QuestStatus.Accepted, 1), f => f.KeyItems[QC.KeyItem.Luopan] = true)));
        L("dw trade", AU3Npc(dw, Td, wa, Sylvie, With(AD4Fake(QuestStatus.Accepted), f =>
        {
            f.KeyItems[QC.KeyItem.FistfulOfHomelandSoil] = true;
            f.TradeItems[QC.Item.PetrifiedLog] = 1;
        })));
        L("dw locus", AU3Npc(dw, Tr, QC.Zone.LaTheinePlateau, QC.Npc.LaTheinePlateau.ErgonLocus,
                             With(AD4Fake(QuestStatus.Accepted), f => f.Zone = QC.Zone.LaTheinePlateau)));
        L("dw geo", AU3Npc(dw, Tr, wa, Sylvie, With(AD4Fake(QuestStatus.Completed), f => f.MainJob = QC.Job.Geo)));
        L("dw war", AU3Npc(dw, Tr, wa, Sylvie, AD4Fake(QuestStatus.Completed)));
        L("ll offer", AU3Npc(ll, Tr, ork, Lerene, AD4Fake(QuestStatus.Available)));
        L("ll accepted", AU3Npc(ll, Tr, ork, Lerene, AD4Fake(QuestStatus.Accepted)));
        L("ll trade", AU3Npc(ll, Td, ork, Lerene, With(AD4Fake(QuestStatus.Accepted), f =>
            f.TradeItems[QC.Item.SquareOfAncestralCloth] = 2)));
        Check("adoulin g4:the reply kinds",
              "dw offer=ProgressEvent(31) ; dw declined=ProgressEvent(32) ; dw offer below the level (states up)=0 ; " +
              "dw offer at the level=ProgressEvent(31) ; dw prog 0=ProgressEvent(33) ; dw luopan=ProgressEvent(35) ; " +
              "dw prog 1=ProgressEvent(36) ; dw trade=ProgressEvent(34) ; dw locus=null ; " +
              "dw geo=Event(37):replaceDefault() ; dw war=Event(39):replaceDefault() ; ll offer=ProgressEvent(22) ; " +
              "ll accepted=Event(23) ; ll trade=ProgressEvent(24)",
              string.Join(" ; ", lines));

        // Sylvie's completed trigger clears MatreBellPayment before 37 (a Geomancer without the bell), nothing before
        // 39
        var geo = With(AD4Fake(QuestStatus.Completed), f => f.MainJob = QC.Job.Geo);
        AU3Npc(dw, Tr, wa, Sylvie, geo);
        var bell = With(AD4Fake(QuestStatus.Completed), f =>
        {
            f.MainJob = QC.Job.Geo;
            f.Items.Add(QC.Item.MatreBell);
        });
        string withBell = AU3Npc(dw, Tr, wa, Sylvie, bell);
        Check("adoulin g4:Sylvie, completed: MatreBellPayment cleared before 37; with the bell 39, nothing set",
              "Quest.SetLocalVar(MatreBellPayment,0) | Event(39):replaceDefault(): ",
              $"{string.Join(";", geo.Calls)} | {withBell}: {string.Join(";", bell.Calls)}");

        // the Ergon Locus of the char's nation with the soil held: no giveKeyItem call (npcUtil.giveKeyItem would skip
        // a held key item anyway, so no trace tells the soil check from its absence)
        var soil = With(AD4Fake(QuestStatus.Accepted), f =>
        {
            f.Zone = QC.Zone.LaTheinePlateau;
            f.NationId = 0;
            f.KeyItems[QC.KeyItem.FistfulOfHomelandSoil] = true;
        });
        string soilReply = AU3Npc(dw, Tr, QC.Zone.LaTheinePlateau, QC.Npc.LaTheinePlateau.ErgonLocus, soil);
        Check("adoulin g4:the Ergon Locus, the soil held: no call", "null: ",
              $"{soilReply}: {string.Join(";", soil.Calls)}");

        // 37's finish: the payment cleared before the option is read (the next trigger clears it too, so no trace
        // tells); a payment that is neither 1 nor 2 (no update sets one) gives the bell and takes nothing
        string Bell(int payment, int option)
        {
            var fin = dw.States.SelectMany(st => st.Handlers)
                .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 37);
            var f = AD4Fake(QuestStatus.Completed);
            f.MainJob = QC.Job.Geo;
            f.QuestLocalVars["MatreBellPayment"] = payment;
            f.Option = option;
            fin.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("adoulin g4:37's finish: payment 1 on 0, payment 3 on 1",
              "Quest.SetLocalVar(MatreBellPayment,0) | " +
              $"Quest.SetLocalVar(MatreBellPayment,0);Player.GiveItems({QC.Item.MatreBell}x1)",
              $"{Bell(1, 0)} | {Bell(3, 1)}");
    }

    // the healing effect's hooks (the exports' callbacks) and the rest timer, on the fake
    static void RunAdoulinGroup4Hooks()
    {
        var dw = new QC.Adoulin.DancesWithLuopans().Compile();
        QuestCallback Cb(string name) => dw.Callbacks.Single(c => c.Name == name);
        ushort ceizak = QC.Zone.CeizakBattlegrounds;
        Fake Rest(int locus, int prog, int tickDelay, int roll)
        {
            var f = AD4Fake(QuestStatus.Accepted, prog);
            f.Zone = ceizak;
            f.Definition = dw;
            f.Now = 1000;
            f.MapSettings["HEALING_TICK_DELAY"] = tickDelay;
            if (locus != 0) f.QuestLocalVars["LocusArea"] = locus;
            f.Rolls.Enqueue(roll);
            return f;
        }
        string Heal(int locus, int prog, int tickDelay, int roll)
        {
            var f = Rest(locus, prog, tickDelay, roll);
            Cb("onHealing").Run(f);
            return string.Join(";", f.Calls);
        }
        Check("adoulin g4:onHealing: LocusArea 1, Prog 0: the wait 3 ticks .. 480 s, ENERGIES_COURSE, RestUntil, " +
              "the timer",
              $"Player.RandomInt(30,480);Text.Special({ceizak}:ENERGIES_COURSE);Quest.SetLocalVar(RestUntil,1100);" +
              "Player.Timer(100000,restTimer) | " +
              $"Player.RandomInt(450,480);Text.Special({ceizak}:ENERGIES_COURSE);Quest.SetLocalVar(RestUntil,1450);" +
              "Player.Timer(450000,restTimer) | " +
              $"Player.RandomInt(480,480);Text.Special({ceizak}:ENERGIES_COURSE);Quest.SetLocalVar(RestUntil,1480);" +
              "Player.Timer(480000,restTimer) |  | ",
              string.Join(" | ", Heal(1, 0, 10, 100), Heal(1, 0, 150, 450), Heal(1, 0, 200, 480), Heal(0, 0, 10, 100),
                          Heal(1, 1, 10, 100)));

        var lose = Rest(1, 0, 10, 0);
        lose.QuestLocalVars["RestUntil"] = 1100;
        Cb("onEffectLose").Run(lose);
        Check("adoulin g4:onEffectLose: RestUntil 0", "Quest.SetLocalVar(RestUntil,0)", string.Join(";", lose.Calls));

        string Timer(int restUntil, int now)
        {
            var f = Rest(1, 0, 10, 0);
            if (restUntil != 0) f.QuestLocalVars["RestUntil"] = restUntil;
            f.Now = now;
            Cb("restTimer").Run(f);
            return string.Join(";", f.Calls);
        }
        Check("adoulin g4:the rest timer: RestUntil 0 nothing; a second before it nothing; at it MYSTICAL_WARMTH, " +
              "Prog 1",
              $" |  | Text.Special({ceizak}:MYSTICAL_WARMTH);Quest.SetLocalVar(RestUntil,0);Quest.SetStep(1) | " +
              $"Text.Special({ceizak}:MYSTICAL_WARMTH);Quest.SetLocalVar(RestUntil,0);Quest.SetStep(1)",
              string.Join(" | ", Timer(0, 5000), Timer(1100, 1099), Timer(1100, 1100), Timer(1100, 1101)));

        // the timer armed by onHealing fires on the first tick strictly past its milliseconds (the fake's clock)
        var armed = Rest(1, 0, 10, 100);
        Cb("onHealing").Run(armed);
        armed.Now = 1100;
        Check("adoulin g4:onHealing's timer: not at 100000 ms, at 100001 ms (then Prog 1)", "0|1|1",
              $"{armed.FireTimers(100000)}|{armed.FireTimers(100001)}|{armed.Step}");
    }

    // Lerene's 24 with quest:complete true and false (false never happens in play: no item reward)
    static void RunAdoulinGroup4Finishes()
    {
        string Fin(bool complete)
        {
            var fin = new QC.Adoulin.LerenesLament().Compile().States.SelectMany(s => s.Handlers)
                .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 24);
            var f = AD4Fake(QuestStatus.Accepted);
            f.CompleteResult = complete;
            fin.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("adoulin g4:Lerene's 24, completed / not",
              $"Player.GiveKeyItem({QC.KeyItem.LerenesPaten});" +
              $"Text.Special({QC.Zone.OuterRakaznar}:YOU_HAVE_LEARNED,{QC.KeyItem.Pulverizing});Quest.Complete();" +
              $"Trade.Confirm();Player.AddKeyItem({QC.KeyItem.Pulverizing}) | " +
              $"Player.GiveKeyItem({QC.KeyItem.LerenesPaten});" +
              $"Text.Special({QC.Zone.OuterRakaznar}:YOU_HAVE_LEARNED,{QC.KeyItem.Pulverizing});Quest.Complete()",
              $"{Fin(true)} | {Fin(false)}");
    }
}
