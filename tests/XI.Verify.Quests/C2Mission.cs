// XI.Verify.Quests -- quest rework C2 Task 3: MissionScript, the second container kind. A test-only mission (Bastok 1-1
// at Naji, never in XI.QuestContent) compiles clean; its states match as a Lua Mission section check (the log's current
// mission and its status); q.Mission records its calls on the fake, whose Complete leaves the char as Mission:complete
// does (the status reset, except on CoP); the validator keeps a mission's states mission states; and MissionLog's names
// are xi.mission.area's (the mission keys' log names). Task 4: a mission's Called() handlers follow the quest calls'
// rules.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // Bastok 1-1 at Naji (Metalworks): status 0 plays 1000 and sets status 1; status 1 plays 1001 and completes
    sealed class SampleMission : MissionScript
    {
        public SampleMission() : base(MissionLog.Bastok, "THE_ZERUHN_REPORT") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 400 });
            b.State(current: true, status: 0).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(QC.Zone.Metalworks, 1000, q => q.Mission.SetStatus(1));
            b.State(current: true, status: 1).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001))
                .OnEventFinish(QC.Zone.Metalworks, 1001, q => q.Mission.Complete());
        }
    }

    // a mission state with a quest state chained after it, and a Called() handler for an NPC that has container
    // handlers: both refused
    sealed class MixedMission : MissionScript
    {
        public MixedMission() : base(MissionLog.Bastok, "THE_ZERUHN_REPORT") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State().Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1))
                .State(QuestStatus.Accepted).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(2));
            b.State(current: false).Npc(QC.Zone.Metalworks, "Naji").Called().OnTrigger(QuestReply.Event(3));
        }
    }

    // quest rework C2 Task 4: Bastok 1-1 as a call-only mission at Gerbaum (Bastok Mines): status 0 plays 1000 through
    // xi.cs.mission, its finish (xi.cs.missionEvent) sets status 1; status 1 plays 1001; the zone-in of status 1 is
    // 1002
    sealed class CallMission : MissionScript
    {
        public CallMission() : base(MissionLog.Bastok, "THE_ZERUHN_REPORT") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true, status: 0).Npc(QC.Zone.BastokMines, QC.Npc.BastokMines.Gerbaum).Called()
                .OnTrigger(QuestReply.Event(1000))
                .OnEventFinish(QC.Zone.BastokMines, 1000, q => q.Mission.SetStatus(1));
            b.State(current: true, status: 1).Npc(QC.Zone.BastokMines, QC.Npc.BastokMines.Gerbaum).Called()
                .OnTrigger(QuestReply.Event(1001))
                .OnZoneIn(QC.Zone.BastokMines, q => QuestReply.Event(1002));
        }
    }

    // a Called() mission handler with a KeyItem reply: refused, as in a quest's NPC call
    sealed class CallKeyItemMission : MissionScript
    {
        public CallKeyItemMission() : base(MissionLog.Bastok, "THE_ZERUHN_REPORT") { }

        protected override void Build(MissionScriptBuilder b) =>
            b.State().Npc(QC.Zone.BastokMines, QC.Npc.BastokMines.Gerbaum).Called().OnTrigger(QuestReply.KeyItem(1));
    }

    internal static void RunC2Mission()
    {
        Console.WriteLine("\n== c2 mission: MissionScript, q.Mission ==");
        int bastok = (int)MissionLog.Bastok, cop = (int)MissionLog.CoP;
        var zeruhn = (MissionLog.Bastok, QC.Mission.Bastok.TheZeruhnReport);

        var mission = new SampleMission();
        var def = mission.Compile();
        Check("c2 mission: clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
        Check("c2 mission: kind and key", $"Mission {QC.MissionKey.Bastok.TheZeruhnReport}",
            $"{mission.Kind} {mission.Key}");
        Check("c2 mission: the reward is the declared one", "400", def.Reward.RankPoints.ToString());

        var f = new Fake { ThisMission = zeruhn };
        f.CurrentMissions[bastok] = QC.Mission.Bastok.TheZeruhnReport;
        Check("c2 mission: state 1 matches at status 0", "True", def.States[0].IsMissionMatch(f).ToString());
        Check("c2 mission: state 2 does not at status 0", "False", def.States[1].IsMissionMatch(f).ToString());
        def.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish).Run(f);
        Check("c2 mission: SetStatus", "Mission.SetStatus(1)", string.Join(",", f.Calls));
        Check("c2 mission:   the status is then 1, and state 2 matches", "1 True",
            $"{f.MissionStatuses[bastok]} {def.States[1].IsMissionMatch(f)}");

        var other = new Fake { ThisMission = zeruhn };
        other.CurrentMissions[bastok] = QC.Mission.Bastok.TheZeruhnReport + 1;
        Check("c2 mission: another current mission matches neither state", "False False",
            $"{def.States[0].IsMissionMatch(other)} {def.States[1].IsMissionMatch(other)}");

        var g = new Fake { ThisMission = zeruhn };
        g.MissionStatuses[bastok] = 1;
        g.CurrentMissions[bastok] = QC.Mission.Bastok.TheZeruhnReport;
        g.MissionVars[$"{bastok}/{QC.Mission.Bastok.TheZeruhnReport}/Option"] = 2;
        def.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish).Run(g);
        Check("c2 mission: Complete", "Mission.Complete()", string.Join(",", g.Calls));
        Check("c2 mission:   completed, not current, status 0, vars cleared", "True False 0 0",
            $"{g.HasCompletedMission(bastok, QC.Mission.Bastok.TheZeruhnReport)} {g.Mission.IsCurrent} " +
            $"{g.MissionStatuses[bastok]} {g.MissionVars.Count}");

        // CoP (log 6): Mission:complete keeps the status
        var h = new Fake { ThisMission = (MissionLog.CoP, QC.Mission.Cop.TheRitesOfLife) };
        h.CurrentMissions[cop] = QC.Mission.Cop.TheRitesOfLife;
        h.MissionStatuses[cop] = 5;
        Check("c2 mission: CoP Complete keeps the status", "True 5",
            $"{h.Mission.Complete()} {h.MissionStatuses[cop]}");
        var raw = new Fake { ThisMission = zeruhn };
        raw.CurrentMissions[bastok] = QC.Mission.Bastok.TheZeruhnReport;
        raw.MissionStatuses[bastok] = 3;
        bool rawDone = raw.Mission.CompleteRaw();
        Check("c2 mission: CompleteRaw completes only (the status kept)", "Mission.CompleteRaw() True True 3",
            $"{string.Join(",", raw.Calls)} {rawDone} " +
            $"{raw.HasCompletedMission(bastok, QC.Mission.Bastok.TheZeruhnReport)} {raw.MissionStatuses[bastok]}");

        var facets = new Fake { ThisMission = zeruhn };
        facets.Mission.Begin();
        facets.Mission.SetStatusAt(3, 1);
        facets.Mission.SetVar("Option", 4);
        facets.Mission.SetMustZone();
        facets.Mission.SetLocalVar("L", 7);
        facets.Player.SetRankPoints(120);
        facets.Player.SetRank(2);
        Check("c2 mission: the facet's calls",
            "Mission.Begin(),Mission.SetStatusAt(3,1),Mission.SetVar(Option,4),Mission.SetMustZone()," +
            "Mission.SetLocalVar(L,7),Player.SetRankPoints(120),Player.SetRank(2)", string.Join(",", facets.Calls));
        Check("c2 mission:   and their reads", "True 48 3 4 True 7 120",
            $"{facets.Mission.IsCurrent} {facets.Mission.Status} {facets.Mission.StatusAt(1)} " +
            $"{facets.Mission.Var("Option")} " +
            $"{facets.Mission.MustZone} {facets.Mission.LocalVar("L")} {facets.Player.RankPoints}");
        facets.Mission.Cleanup();
        Check("c2 mission:   Cleanup clears the mission's vars", "0", facets.Mission.Var("Option").ToString());

        string Throws(Func<object> read)
        {
            try
            {
                return read()?.ToString() ?? "null";
            }
            catch (InvalidOperationException e)
            {
                return "throws: " + e.Message;
            }
        }
        Check("c2 mission: a mission's q.Quest throws",
            "throws: a mission's context has no quest (q.Quest): its own is q.Mission",
            Throws(() => ((IQuestContext)f).Quest));
        Check("c2 mission: a quest's q.Mission is null", "null", Throws(() => ((IQuestContext)new Fake()).Mission));

        Check("c2 mission: the validator refuses a quest state, and a Called() and a container handler for one NPC",
            "state 2 has a quest match: a mission's states are b.State(current, status, when) | " +
            $"Naji in zone {QC.Zone.Metalworks} has both Called() and container handlers",
            string.Join(" | ", QuestScriptValidator.Validate(new MixedMission().Compile())));

        // quest rework C2 Task 4: a mission's Called() handlers are bridged (xi.cs.mission), under the quest calls'
        // rules
        var call = new CallMission().Compile();
        Check("c2 mission call: a call-only mission is clean", "",
            string.Join(" | ", QuestScriptValidator.Validate(call)));
        Check("c2 mission call:   call-only, its state-level handlers called", "True True True",
            $"{call.Core.CallOnly} {call.Core.HasCalled} " +
            $"{call.States.SelectMany(st => st.Handlers).Where(x => x.Entity == null).All(call.Core.IsCalled)}");
        Check("c2 mission call: a KeyItem reply in a Called() handler is refused",
            "Gerbaum OnTrigger: a KeyItem reply is not allowed in an NPC call",
            string.Join(" | ", QuestScriptValidator.Validate(new CallKeyItemMission().Compile())));

        string root = QuestsLuaRoot();
        if (root == null)
        {
            Bad("c2 mission: scripts/globals/missions.lua not found");
            return;
        }
        string lua = File.ReadAllText(Path.Combine(root, "scripts", "globals", "missions.lua"));
        var logIds = Regex.Matches(lua, @"^\s+([A-Z]+)\s*=\s*(\d+),", RegexOptions.Multiline)
            .Take(15).ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
        var areas = Regex.Matches(lua, @"\[xi\.mission\.log_id\.([A-Z]+)\]\s*=\s*'([a-z]+)'")
            .ToDictionary(m => logIds[m.Groups[1].Value], m => m.Groups[2].Value);
        Check("c2 mission: MissionLog's names in lower case are xi.mission.area's (the keys' log names)",
            string.Join(",", areas.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")),
            string.Join(",", Enum.GetValues<MissionLog>().Select(l => $"{(int)l}={l.ToString().ToLowerInvariant()}")));
    }
}
