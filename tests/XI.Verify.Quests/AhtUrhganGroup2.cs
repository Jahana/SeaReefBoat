// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 2 (Luck of the Draw, Equipped for All Occasions, Navigating the
// Unfriendly Seas, Against All Odds, Breaking the Bonds of Fate, The Die is Cast, with the era module
// navigating_the_unfriendly_seas): what no trace can see.
// - The reply kinds no flow can show (the mutants' structural survivors; every other kind is pinned by a trace, the
//   #kinds flows among them). A kind only ranks the lookup's actions for the same NPC talk or trade; these NPCs have no
//   script of their own (no fallback: the lookup runs the action whatever its rank) and no other line of that
//   interaction that ranks Event or above, the rest of their rivals being the zone's DefaultActions (below Event):
//   * Mafwahb (Whitegate): Luck of the Draw's 548 (Prog 1) and 647 (Prog 2) are his only lines; his default is the same
//     Event 647, so Prog 3's nil (shown by #edges as 647) must stay nil: a 647 there would look the same;
//   * qm6 (Arrapago): Luck of the Draw's 211 is a Progress cutscene; the cutscene flag only locks the char out of
//     area-of-effect targeting, which no trace records;
//   * Talacca's qm1 (Luck of the Draw's 2), Jijiroon (The Die is Cast's 245), Arrapago's qm9 (its 212 and 213; Aydeewa's
//     qm9 is another NPC): one quest line each;
//   * the trades: Leleroon's 283 (Navigating; Crafted Corsair Artifact only talks to him) and qm6's 235 (Breaking the
//     Bonds of Fate; no other quest trades with qm6);
//   * the Leypoint's 508 (Navigating): its other talk line, Olduum's, is a messageSpecial (below Event); Fist of the
//     People and Olduum otherwise only trade there.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup2()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 2: the COR chain and The Die is Cast; the reply kinds no trace sees ==");
        var luck = new QC.AhtUrhgan.LuckOfTheDraw().Compile();
        var equipped = new QC.AhtUrhgan.EquippedForAllOccasions().Compile();
        var nav = new QC.AhtUrhgan.NavigatingTheUnfriendlySeas().Compile();
        var aao = new QC.AhtUrhgan.AgainstAllOdds().Compile();
        var bonds = new QC.AhtUrhgan.BreakingTheBondsOfFate().Compile();
        var die = new QC.AhtUrhgan.TheDieIsCast().Compile();
        foreach (var (name, def) in new[] { ("Luck of the Draw", luck), ("Equipped for All Occasions", equipped),
                     ("Navigating the Unfriendly Seas", nav), ("Against All Odds", aao),
                     ("Breaking the Bonds of Fate", bonds), ("The Die is Cast", die) })
            Check($"ahturhgan g2:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        var gauge = AU2Acc(0);
        gauge.TradeItems[QC.Item.Hydrogauge] = 1;
        var testimony = AU2Acc(0);
        testimony.MainJob = QC.Job.Cor;
        testimony.MainLevel = 66;
        testimony.TradeItems[QC.Item.CorsairsTestimony] = 1;
        Check("ahturhgan g2:the reply kinds no flow can show, as the Lua has them: Mafwahb's 548 (Prog 1) a Progress, 647 (Prog 2) an Event, nil at Prog 3; qm6's 211 a Progress cutscene; Talacca qm1's 2, Leleroon's trade 283, qm6's trade 235, the Leypoint's 508, Jijiroon's 245, Arrapago qm9's 212 and 213 Progress",
              "ProgressEvent(548)|Event(647)|null|ProgressCutscene(211)|ProgressEvent(2)|ProgressEvent(283)|ProgressEvent(235,{[7]=1})|" +
              "ProgressEvent(508)|ProgressEvent(245)|ProgressEvent(212)|ProgressEvent(213)",
              string.Join("|",
                  AU1Reply(luck, QuestHandlerKind.OnTrigger, QC.Npc.AhtUrhganWhitegate.Mafwahb, AU2Acc(1)),
                  AU1Reply(luck, QuestHandlerKind.OnTrigger, QC.Npc.AhtUrhganWhitegate.Mafwahb, AU2Acc(2)),
                  AU1Reply(luck, QuestHandlerKind.OnTrigger, QC.Npc.AhtUrhganWhitegate.Mafwahb, AU2Acc(3)),
                  AU1Reply(luck, QuestHandlerKind.OnTrigger, QC.Npc.ArrapagoReef.qm6, AU2Acc(2)),
                  AU1Reply(luck, QuestHandlerKind.OnTrigger, QC.Npc.TalaccaCove.qm1, AU2Acc(3)),
                  AU1Reply(nav, QuestHandlerKind.OnTrade, QC.Npc.Nashmau.Leleroon, gauge),
                  AU1Reply(bonds, QuestHandlerKind.OnTrade, QC.Npc.ArrapagoReef.qm6, testimony),
                  AU1Reply(nav, QuestHandlerKind.OnTrigger, QC.Npc.WajaomWoodlands.Leypoint, AU2Acc(2)),
                  AU2Step(die, 1, QC.Npc.Nashmau.Jijiroon),
                  AU2Step(die, 2, QC.Npc.ArrapagoReef.qm9),
                  AU2Step(die, 4, QC.Npc.ArrapagoReef.qm9)));
    }

    static Fake AU2Acc(int prog) => new Fake { Status = QuestStatus.Accepted, Step = prog };

    // the talk reply of the quest's accepted state for that Prog (The Die is Cast has one state per Prog), on the fake
    static string AU2Step(QuestScriptDefinition def, int step, string npc) =>
        AU1Describe(def.States.Where(s => s.Matches.Any(m => m.Status == QuestStatus.Accepted && m.Step == step))
            .SelectMany(s => s.Handlers).Single(x => x.Kind == QuestHandlerKind.OnTrigger && x.Entity == npc), AU2Acc(step));
}
