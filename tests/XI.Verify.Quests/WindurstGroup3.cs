// XI.Verify.Quests -- quest rework B3b Windurst group 3 (Blue Ribbon Blues, Toraimarai Turmoil, Waking Dreams, The
// Moonlit Path): the facet members the group added, on the fake; the reply modifiers no trace can see (a replaceDefault on
// an NPC nothing else answers sends the packets a plain event sends); and The Moonlit Path's Dark Mana Orb for a char of
// another nation, which no flow can set up (the `rank` statement sets the char's own nation's rank only).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup3()
    {
        Console.WriteLine("== quest rework B3b Windurst group 3: facets, reply modifiers, the orb's nation ==");
        RunWindurstGroup3Facets();
        RunWindurstGroup3Modifiers();
        RunWindurstGroup3Orb();
    }

    // player:hasSpell / addSpell with silentLog, NextJstDay(), JstMidnight(), player:getRank, player:hasJob, and
    // npcUtil.tradeHasExactly with quantities
    static void RunWindurstGroup3Facets()
    {
        var f = new Fake { JstDay = 1800025200, NextJstMidnight = 1800025201 };
        f.Ranks[2] = 10;
        f.UnlockedJobs.Add(QC.Job.Smn);
        string before = $"{f.Player.HasSpell(304)}/{f.Player.HasJob(QC.Job.Smn)}/{f.Player.HasJob(QC.Job.War)}/" +
                        $"{f.Player.NextJstDay}/{f.Player.JstMidnight}/{f.Player.Rank(2)}/{f.Player.Rank(1)}";
        f.Player.AddSpell(304, true);
        string after = $"{f.Player.HasSpell(304)}";

        var three = new Fake();
        three.TradeItems[QC.Item.StarmiteShell] = 3;
        var four = new Fake();
        four.TradeItems[QC.Item.StarmiteShell] = 4;
        var threeAndChip = new Fake();
        threeAndChip.TradeItems[QC.Item.StarmiteShell] = 3;
        threeAndChip.TradeItems[QC.Item.BoneChip] = 1;
        string trades = $"{three.Trade.HasExactly((QC.Item.StarmiteShell, 3))}/{four.Trade.HasExactly((QC.Item.StarmiteShell, 3))}/" +
                        $"{threeAndChip.Trade.HasExactly((QC.Item.StarmiteShell, 3))}/{three.Trade.HasExactly((QC.Item.StarmiteShell, 1), (QC.Item.StarmiteShell, 2))}";
        Check("windurst g3 facets:the fake answers HasSpell (Spells; AddSpell with silentLog adds and is recorded), HasJob (UnlockedJobs), NextJstDay, JstMidnight, Rank (1 for a nation not set); HasExactly with quantities is exact",
              "False/True/False/1800025200/1800025201/10/1|True|Player.AddSpell(304,True)|True/False/False/True",
              $"{before}|{after}|{string.Join(";", f.Calls)}|{trades}");
    }

    static string G3Reply(QuestScriptDefinition def, int state, string npc, Fake f) =>
        G2Reply(G2Handler(def, state, QuestHandlerKind.OnTrigger, npc), f);

    static void RunWindurstGroup3Modifiers()
    {
        var brb = new QC.Windurst.BlueRibbonBlues().Compile();
        var wd = new QC.Windurst.WakingDreams().Compile();

        // Blue Ribbon Blues' completed section: Kerutoto's 363, replaceDefault, while the mustZone holds (Kerutoto has no
        // script, so the trace cannot tell it from a plain event)
        var zoned = new Fake { Status = QuestStatus.Completed, MustZone = true };
        var notZoned = new Fake { Status = QuestStatus.Completed };
        Check("windurst g3 modifiers:Blue Ribbon Blues' completed 363 replaces the default, only with mustZone",
              "Event(363):replaceDefault()|True|False",
              $"{G3Reply(brb, 2, QC.Npc.WindurstWaters.Kerutoto, zoned)}|" +
              $"{brb.States[2].IsMatch(QuestStatus.Completed, 0, zoned)}|{brb.States[2].IsMatch(QuestStatus.Completed, 0, notZoned)}");

        // Waking Dreams' completed section: within the lockout 789 replaceDefault, at the lockout's edge (now == the date)
        // and after it, with Option 1 and the vial held, the plain 789; the trace's two 789s send the same packets
        var locked = new Fake { Status = QuestStatus.Completed, Now = 1800000000 };
        locked.CharVars["Darkness_Named_date"] = 1800000001;
        locked.KeyItems[QC.KeyItem.VialOfDreamIncense] = true;
        locked.Vars["Option"] = 1;
        var edge = new Fake { Status = QuestStatus.Completed, Now = 1800000000 };
        edge.CharVars["Darkness_Named_date"] = 1800000000;
        edge.KeyItems[QC.KeyItem.VialOfDreamIncense] = true;
        edge.Vars["Option"] = 1;
        Check("windurst g3 modifiers:Waking Dreams' completed 789 replaces the default within the lockout only",
              "Event(789):replaceDefault()|Event(789)",
              $"{G3Reply(wd, 2, QC.Npc.WindurstWaters.Kerutoto, locked)}|{G3Reply(wd, 2, QC.Npc.WindurstWaters.Kerutoto, edge)}");
    }

    // The Moonlit Path's 846 / 850 finish: the Dark Mana Orb needs a Windurst char (the nation half). A Bastok char at
    // Windurst rank 10 who completed The Promise gets none; the same char of Windurst gets it.
    static void RunWindurstGroup3Orb()
    {
        var tmp = new QC.Windurst.TheMoonlitPath().Compile();
        var finish846 = G2Handler(tmp, 1, QuestHandlerKind.OnEventFinish, null, 846);
        string Run(int nation)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Csid = 846, Option = 6, NationId = nation, JstDay = 1800025200 };
            f.Ranks[2] = 10;
            f.CompletedQuests.Add("Windurst/THE_PROMISE");
            finish846.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g3 orb:The Moonlit Path's turn-in gives the Dark Mana Orb to a Windurst char only (Windurst rank 10, The Promise)",
              "Player.GiveCurrency(gil,15000);Quest.Complete();Player.DelKeyItem(326);Quest.SetVar(Timer,1,1800025200)|" +
              "Player.GiveCurrency(gil,15000);Quest.Complete();Player.DelKeyItem(326);Quest.SetVar(Timer,1,1800025200);Player.GiveKeyItem(484)",
              $"{Run(1)}|{Run(2)}");
    }
}
