using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;

// Quest rework C2 Task 5: the harness's mission and battlefield keys, their pure parts, in the self-test (no database).
// The kinds' flow keys, Lua id constants, overlay, call keys and coverage; the live side runs in replay all (the
// startup gate over every container's LegacyPath) and in Tasks 6 to 9's records and replays
internal static partial class Program
{
    private static void SelfTestC2()
    {
        Console.WriteLine("quest rework C2: the harness for missions and battlefields");
        SelfTestC2Keys();
        SelfTestC2Overlay();
        SelfTestC2Calls();
        SelfTestC2Coverage();
    }

    private const string C2Head = "char zone=BastokMarkets pos=0,0,0\n";

    // the kinds' Lua id constants and flow keys; the selectors take them as quest keys
    private static void SelfTestC2Keys()
    {
        Check("c2 harness: LuaIdConstant mission", "xi.mission.id.cop.THE_RITES_OF_LIFE",
              LuaIdConstant("mission.cop.the_rites_of_life"));
        Check("c2 harness: LuaIdConstant battlefield", "xi.battlefield.id.CACTUAR_SUAVE",
              LuaIdConstant("battlefield.cactuar_suave"));
        Check("c2 harness: LuaIdConstant of a quest is as before", "xi.quest.id.bastok.GROCERIES",
              LuaIdConstant("bastok.groceries"));
        Check("c2 harness: LuaIdConstant of a mission of a log in mixed case (toau)",
              "xi.mission.id.toau.LAND_OF_SACRED_SERPENTS", LuaIdConstant("mission.toau.land_of_sacred_serpents"));
        try
        {
            Check("c2 harness: a flow key of a mission", "mission.cop.the_rites_of_life",
                  Flow.Parse(C2Head + "login\n", "m.flow", "mission.cop.the_rites_of_life#cop", null).Key);
            Check("c2 harness: a flow key of a battlefield", "battlefield.cactuar_suave",
                  Flow.Parse(C2Head + "login\n", "b.flow", "battlefield.cactuar_suave", null).Key);
        }
        catch (FormatException e)
        {
            Fail("c2 harness: a mission or battlefield flow parses", e.Message);
        }
        var all = new List<string>
        {
            "bastok.groceries", "battlefield.cactuar_suave", "battlefield.cactuar_suave#lose",
            "mission.cop.the_rites_of_life", "mission.cop.the_rites_of_life#cop",
        };
        string Sel(string sel) =>
            SelectFlows(all, sel, n => n, n => n.Split('#')[0], out var bad, out var why) is { } chosen
                ? string.Join(",", chosen) : $"refused {bad}: {why}";
        Check("c2 harness: a mission or battlefield key selects its flows, flow:<name> one",
              "mission.cop.the_rites_of_life,mission.cop.the_rites_of_life#cop,battlefield.cactuar_suave#lose",
              Sel("mission.cop.the_rites_of_life,flow:battlefield.cactuar_suave#lose"));
        Check("c2 harness: stable's children take a mission selector as given",
              "record mission.cop.the_rites_of_life --out d",
              string.Join(" ", StableChildArgs("record", "mission.cop.the_rites_of_life", "d", null)));
    }

    // the overlay over scripts/missions, scripts/battlefields and the framework scripts/globals/battlefield.lua
    private static void SelfTestC2Overlay()
    {
        const string Bf = "scripts/battlefields/Chamber_of_Oracles/cactuar_suave.lua";
        const string Fw = "scripts/globals/battlefield.lua";
        var bfRecord = new[] { ("battlefield.cactuar_suave", Bf, "xi.battlefield.id.CACTUAR_SUAVE") };
        const string Pf = "scripts/globals/pathfind.lua";
        const string Uncalled = "-- the framework at the commit";
        var bfKeys = new[] { "battlefield.cactuar_suave" };
        // the framework unchanged since the commit: Overlay puts it among the changed files (WithBattlefieldFramework)
        var (taken, _) = OverlayPlan(WithBattlefieldFramework(new[] { (Bf, "old", "") }, bfKeys, () => Uncalled),
                                     bfRecord);
        Check("c2 harness: a battlefield record takes its file and the framework",
              $"{Bf},{Fw}", string.Join(",", taken));
        // its require of a changed file is followed as any taken file's: the pathfind it loads is the commit's
        (taken, _) = OverlayPlan(
            WithBattlefieldFramework(new[] { (Bf, "old", ""), (Pf, "pathfind then", "pathfind now") }, bfKeys,
                                     () => "require('scripts/globals/pathfind')"),
            bfRecord);
        Check("c2 harness: ... and, the framework unchanged, a changed file it requires (pathfind), in path order",
              $"{Bf},{Fw},{Pf}", string.Join(",", taken));
        int asked = 0;
        var asQuest = WithBattlefieldFramework(new[] { (Pf, "then", "now") }, new[] { "bastok.groceries" },
                                               () => { asked++; return Uncalled; });
        Check("c2 harness: ... a quest record's changed files get no framework (its text not read)",
              $"{Pf}/0", $"{string.Join(",", asQuest.Select(c => c.Rel))}/{asked}");
        (taken, _) = OverlayPlan(new[] { (Bf, "old", ""), (Fw, "framework then", "framework now") }, bfRecord);
        Check("c2 harness: ... the framework once, in the diff's order, when it changed too",
              $"{Bf},{Fw}", string.Join(",", taken));
        (taken, _) = OverlayPlan(new[] { (Fw, "framework then", "framework now") },
                                 new[] { ("bastok.groceries", (string)null, "xi.quest.id.bastok.GROCERIES") });
        Check("c2 harness: a quest record leaves the framework at HEAD", "", string.Join(",", taken));
        const string Mi = "scripts/missions/cop/1_1_The_Rites_of_Life.lua";
        const string Npc = "scripts/zones/Upper_Jeuno/npcs/Monberaux.lua";
        (taken, _) = OverlayPlan(
            new[]
            {
                (Fw, "framework then", "framework now"), (Mi, "local mission = Mission:new()", ""),
                (Npc, "if x == xi.mission.id.cop.THE_RITES_OF_LIFE then", "xi.cs.mission(player, npc, " +
                 "'mission.cop.the_rites_of_life', 'onTrigger')"),
            },
            new[] { ("mission.cop.the_rites_of_life", Mi, "xi.mission.id.cop.THE_RITES_OF_LIFE") });
        Check("c2 harness: a mission record takes its LegacyPath file and its NPC scripts, not the framework",
              $"{Mi},{Npc}", string.Join(",", taken));
        SelfTestC2OverlayOwners();
    }

    // Task 10 (the Task 5 carry): a taken file that is another C# container's LegacyPath file turns that container
    // off, and a battlefield gone off so takes the framework. Save the Children's two files name each other
    private static void SelfTestC2OverlayOwners()
    {
        const string StcBf = "scripts/battlefields/Ghelsba_Outpost/save_the_children.lua";
        const string StcMi = "scripts/missions/sandoria/1_3_Save_the_Children.lua";
        const string Fw = "scripts/globals/battlefield.lua";
        const string BfKey = "battlefield.save_the_children", MiKey = "mission.sandoria.save_the_children";
        const string ElvaanFile = "scripts/quests/bastok/The_Elvaan_Goldsmith.lua";
        const string WishFile = "scripts/quests/bastok/Wish_Upon_a_Star.lua";
        const string ElvaanKey = "bastok.the_elvaan_goldsmith", WishKey = "bastok.wish_upon_a_star";
        var owners = OverlayOwners(CSharpContainers());
        Check("c2 harness: the overlay's owners: each Save the Children file to its C# container; a quest's file to " +
              "its quest",
              $"{BfKey}/{MiKey}/{ElvaanKey}",
              $"{owners.GetValueOrDefault(StcBf)}/{owners.GetValueOrDefault(StcMi)}/" +
              owners.GetValueOrDefault(ElvaanFile));
        const string BfText = "    mission               = xi.mission.id.sandoria.SAVE_THE_CHILDREN,\n";
        const string MiText = "if player:getLocalVar('battlefieldWin') == xi.battlefield.id.SAVE_THE_CHILDREN then\n";
        const string FwText = "-- the framework at the commit";
        // a mission record whose one changed file is its battlefield's: both containers off, the framework taken
        var (_, taken, off) = OverlayPlanFor(
            new[] { (StcBf, BfText, "") },
            new[] { (MiKey, StcMi, "xi.mission.id.sandoria.SAVE_THE_CHILDREN") }, owners, () => FwText);
        Check("c2 harness: a mission record that takes its battlefield's file turns both C# containers off and " +
              "takes the framework",
              $"{StcBf},{Fw}|{MiKey},{BfKey}",
              $"{string.Join(",", taken)}|{string.Join(",", RecordOffKeys(MiKey, Array.Empty<string>(), off))}");
        // a battlefield record that takes its mission's file: both off
        (_, taken, off) = OverlayPlanFor(
            new[] { (StcBf, BfText, ""), (StcMi, MiText, "") },
            new[] { (BfKey, StcBf, "xi.battlefield.id.SAVE_THE_CHILDREN") }, owners, () => FwText);
        Check("c2 harness: a battlefield record that takes its mission's file turns both C# containers off",
              $"{StcBf},{Fw},{StcMi}|{BfKey},{MiKey}",
              $"{string.Join(",", taken)}|{string.Join(",", RecordOffKeys(BfKey, Array.Empty<string>(), off))}");
        // a record's own file is never its own owner's off entry, and a file no container owns adds nothing
        (_, _, off) = OverlayPlanFor(
            new[] { (StcMi, MiText, ""), ("scripts/zones/Ghelsba_Outpost/Zone.lua", MiText, "") },
            new[] { (MiKey, StcMi, "xi.mission.id.sandoria.SAVE_THE_CHILDREN") }, owners, () => FwText);
        Check("c2 harness: ... its own LegacyPath file and an unowned file turn no other container off",
              "", string.Join(",", off));
        // Task 10 of C3 Prep: a taken quest file turns its C# quest off too. Wish Upon a Star's file names The Elvaan
        // Goldsmith's constant here (test text), so a record of the Goldsmith takes it
        const string ElvaanText =
            "player:getQuestStatus(xi.questLog.BASTOK, xi.quest.id.bastok.THE_ELVAAN_GOLDSMITH)\n";
        (_, taken, off) = OverlayPlanFor(
            new[] { (WishFile, ElvaanText, "") },
            new[] { (ElvaanKey, ElvaanFile, "xi.quest.id.bastok.THE_ELVAAN_GOLDSMITH") }, owners, () => FwText);
        Check("c3 harness: a quest record that takes another ported quest's file turns that C# quest off",
              $"{WishFile}|{WishKey}", $"{string.Join(",", taken)}|{string.Join(",", off)}");
        // the record's own file: it is in keys, so it adds nothing to off
        (_, taken, off) = OverlayPlanFor(
            new[] { (ElvaanFile, ElvaanText, "") },
            new[] { (ElvaanKey, ElvaanFile, "xi.quest.id.bastok.THE_ELVAAN_GOLDSMITH") }, owners, () => FwText);
        Check("c3 harness: a quest record that takes its own file adds nothing to off",
              $"{ElvaanFile}|", $"{string.Join(",", taken)}|{string.Join(",", off)}");
    }

    // the mission calls are calls of a key, as the quest calls
    private static void SelfTestC2Calls()
    {
        const string Text = "xi.cs.mission(player, npc, 'mission.cop.a', 'onTrigger')\n" +
                            "xi.cs.missionEvent(player, npc, \"mission.cop.b\", csid, option)\n" +
                            "return xi.cs.missionZoneIn(player, 'mission.rov.c', prevZone)\n" +
                            "-- xi.cs.mission(player, npc, 'mission.cop.commented', 'onTrigger')\n" +
                            "xi.cs.quest(player, npc, 'bastok.d', 'onTrigger')\n";
        Check("c2 harness: the keys of the mission calls, as the quest calls'",
              "bastok.d,mission.cop.a,mission.cop.b,mission.rov.c", string.Join(",", CalledKeys(Text)));
        Check("c2 harness: a script calls a mission's key; not in a comment",
              "True/False", $"{CallsQuest(Text, "mission.rov.c")}/{CallsQuest(Text, "mission.cop.commented")}");
    }

    // coverage counts a mission's or a battlefield's handlers as a quest's
    private static void SelfTestC2Coverage()
    {
        const string M = "mission.cop.the_rites_of_life";
        var cov = new Coverage();
        cov.Ran.Add(M);
        cov.Declared[M] = new List<string> { "1/-/onZoneIn", "2/Monberaux/onTrigger" };
        cov.Calls[M] = new Dictionary<string, LabelCalls>
        {
            ["1/-/onZoneIn"] = new LabelCalls { Count = 1, Traces = { M } },
        };
        Flow f = null;
        try
        {
            f = Flow.Parse(C2Head + "login\n", "m.flow", M, null);
        }
        catch (FormatException e)
        {
            Fail("c2 harness: the coverage flow parses", e.Message);
            return;
        }
        Check("c2 harness: coverage of a mission: a handler no trace reaches fails; a C# battlefield with no " +
              "flow fails",
              "FAIL coverage battlefield.cactuar_suave: no flows|" +
              $"FAIL coverage {M}: 2/Monberaux/onTrigger reached by no trace",
              string.Join("|", CoverageResults(cov, new List<Flow> { f }, new[] { M, "battlefield.cactuar_suave" })));
        // every kind's: each quest's key, and one key per C# container of any kind
        var keys = CSharpContainerKeys();
        Check("c2 harness: the every-container coverage list holds each C# quest's key and one key per container",
              "True/True",
              $"{CSharpQuestKeys().All(keys.Contains)}/{keys.Distinct().Count() == CSharpContainers().Count}");
    }
}
