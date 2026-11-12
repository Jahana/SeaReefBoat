// XI.Verify.Quests -- quest rework C2 Task 1: the shared container core. Every C# quest is a ContainerScript of kind
// Quest, its Key is the key the bridge gave it before C2 (KeyIn with its log's area as scripts/globals/quests.lua's
// xi.quest.area has it, or its hidden key), and QuestScript.AreaName, which the bridge and the golden harness now read
// in place of xi.quest.area, holds the names quests.lua gives.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    internal static void RunC2Core()
    {
        Console.WriteLine("\n== c2 core: ContainerScript under QuestScript ==");
        string root = QuestsLuaRoot();
        if (root == null)
        {
            Bad("c2 core: scripts/globals/quests.lua not found above " + AppContext.BaseDirectory);
            return;
        }
        // xi.quest.area as the Lua reads it, the expected side of the key and area checks ("?" for a log it lacks)
        var luaAreas = LuaQuestAreas(root);
        string LuaArea(QuestLog log) => luaAreas.GetValueOrDefault(log, "?");

        var all = typeof(XI.QuestContent.Zone).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ContainerScript).IsAssignableFrom(t)).ToList();
        var quests = all.Where(t => typeof(QuestScript).IsAssignableFrom(t)).ToList();
        Check("c2 core: every QuestScript is a ContainerScript of kind Quest", quests.Count.ToString(),
            all.Count(t => ((ContainerScript)Activator.CreateInstance(t)).Kind == ContainerKind.Quest).ToString());
        Check("c2 core: the keys are what QuestKeyRegistry gives", "",
            string.Join(" | ", quests.Select(t => (QuestScript)Activator.CreateInstance(t))
                .Where(q => q.Key != q.ExpectedKeyForTest(LuaArea)).Select(q => q.GetType().Name)));
        // the logs 0 to 11 and the hidden log 255 ("nil" where a side has no area)
        var logs = Enumerable.Range(0, 12).Append((int)QuestScript.HiddenLog).Select(n => (QuestLog)n).ToList();
        Check("c2 core: QuestScript.AreaName is xi.quest.area of scripts/globals/quests.lua",
            string.Join(",", logs.Select(l => luaAreas.GetValueOrDefault(l, "nil"))),
            string.Join(",", logs.Select(l => QuestScript.AreaName(l) ?? "nil")));

        Check("c2 keys: mission key", "mission.cop.the_rites_of_life", XI.QuestContent.MissionKey.Cop.TheRitesOfLife);
        Check("c2 keys: battlefield key", "battlefield.cactuar_suave", XI.QuestContent.BattlefieldKey.CactuarSuave);
        Check("c2 keys: rule and generated agree", "mission.bastok.the_zeruhn_report",
            ContainerKeys.Mission("Bastok", "THE_ZERUHN_REPORT"));
        Check("c2 keys: battlefield rule and generated agree", "battlefield.rank_2_mission_2",
            ContainerKeys.Battlefield("RANK_2_MISSION_2"));
        Check("c2 keys: no mission key equals a quest key", "",
            string.Join(",", ConstStrings(typeof(XI.QuestContent.MissionKey))
                .Intersect(ConstStrings(typeof(XI.QuestContent.QuestKey)))));
        Check("c2 keys: every mission key is a mission key", "",
            string.Join(",", ConstStrings(typeof(XI.QuestContent.MissionKey)).Where(k => !ContainerKeys.IsMission(k))));
        Check("c2 keys: every battlefield key is a battlefield key", "",
            string.Join(",", ConstStrings(typeof(XI.QuestContent.BattlefieldKey))
                .Where(k => !ContainerKeys.IsBattlefield(k))));
        Check("c4 keys: IsRoe, a key no quest, mission or battlefield has", "True/False/False",
              $"{ContainerKeys.IsRoe("roe.rewards")}/{ContainerKeys.IsRoe("mission.cop.the_rites_of_life")}/" +
              ConstStrings(typeof(XI.QuestContent.QuestKey)).Any(ContainerKeys.IsRoe));
    }

    // the const string fields of a generated key class, nested classes included
    private static List<string> ConstStrings(Type type)
    {
        var result = type.GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()).ToList();
        foreach (var nested in type.GetNestedTypes()) result.AddRange(ConstStrings(nested));
        return result;
    }
}
