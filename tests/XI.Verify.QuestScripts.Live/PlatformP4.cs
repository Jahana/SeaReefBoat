using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests.Authoring;

// quest rework P4: the C# quest modules through the real registration. The module list (ZoneHost.ModuleInitLines, read as
// modules/init.txt) and the settings decide which C# module is enabled; an enabled one is logged and its quest is built from
// the patched definition and moved to the end of the container list (Lua's modifyInteractionEntry removes the container and
// adds it back, so the patched quest's entries come after every other container's in each list it shares: Chocobo's Wounds
// on Brutus, A Test of True Love on Carmelo); a module whose override the quest cannot take,
// that names no C# quest, or whose patched quest fails the validator, is refused (logged) and the quest registers as it is;
// a quest in CSHARP_QUESTS_OFF is left to its Lua; of two modules on one quest the later one's overrides apply. The
// overrides themselves are checked with the fake (XI.Verify.Quests), the module's behaviour against its Lua module by
// XI.Verify.QuestGolden.Live's module flows. The folder rules run on test-only modules under a folder of their own
// (RunP4Folders); every shipped module's condition and name are checked against its Lua file (PlatformP4b.cs).
internal static partial class Program
{
    private const string GustabergKey = "bastok.the_gustaberg_tour";
    private const string GustabergModule = "modules/era/lua/quests/bastok/the_gustaberg_tour.lua";

    private static void RunPlatformP4()
    {
        Console.WriteLine("quest rework P4: quest modules");
        var order = zone.ContainerOrder(lua);
        try
        {
            RunP4Enabling(order);
            RunP4Refusals();
            RunP4Placement();
            RunP4RewardRemoved();
            RunP4bConditions();
            Check("p4: every C# module applies to its quests (CheckQuestModules)", "", string.Join(" | ", zone.CheckQuestModules()));
        }
        finally
        {
            zone.ModuleInitLines = worldModuleLines_;
            zone.TestQuestModules.Clear();
            zone.CSharpQuestsOff.Remove(GustabergKey);
            Lua("xi.settings.main.RESTRICT_CONTENT = 0 xi.settings.main.ENABLE_ROV = 1");
            Lua("InteractionGlobal.reload(true)");
        }
        Check("p4: back to no module", "0", zone.EnabledQuestModules.Count.ToString());
    }

    // the modules: lines of a reload with these module lines
    private static List<string> P4Reload(params string[] lines)
    {
        zone.ModuleInitLines = lines;
        int mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        return log.Skip(mark).Where(l => l.StartsWith("modules: ", StringComparison.Ordinal)).ToList();
    }

    private static void RunP4Enabling(List<string> order)
    {
        Check("p4: the committed modules/init.txt (the worlds' default lines) enables no C# module", "0", zone.EnabledQuestModules.Count.ToString());

        var lines = P4Reload("era/lua/quests/bastok/the_gustaberg_tour.lua");
        Check("p4: listed, the condition false (RESTRICT_CONTENT 0): not enabled, logged",
              $"modules: era_quest_the_gustaberg_tour listed, not enabled: its condition does not hold ({GustabergModule})", string.Join(" | ", lines));
        Check("p4:   no labels, the quest its own", "null", zone.ModuleLabels(GustabergModule) == null ? "null" : "labels");

        Lua("xi.settings.main.RESTRICT_CONTENT = 1 xi.settings.main.ENABLE_ROV = 0");
        int mark = log.Count;
        lines = P4Reload("era/lua/quests/bastok/the_gustaberg_tour.lua");
        Check("p4: listed, xi.pre(ROV) true: enabled, the startup line", $"modules: era_quest_the_gustaberg_tour enabled ({GustabergModule})", string.Join(" | ", lines));
        Check("p4:   its override's label in the patched quest", $"{GustabergKey} 2/Hunting_Bear/onTrigger", string.Join(",", zone.ModuleLabels(GustabergModule)));
        Check("p4:   every C# quest registered, none refused", $"quests: {RegisteredCount} C# quests registered|",
              $"{Untimed(log.Skip(mark).LastOrDefault(IsRegistered))}|{string.Join(" | ", log.Skip(mark).Where(l => l.Contains("not registered")))}");
        Check("p4:   the container order: the patched quest moved to the end, the rest unchanged",
              string.Join("\n", order.Where(id => id != "Quest[1][45]").Append("Quest[1][45]")), string.Join("\n", zone.ContainerOrder(lua)));
        Check("p4:   Hunting_Bear's handlers: the quest's two sections, as before", "csharp:bastok.the_gustaberg_tour;csharp:bastok.the_gustaberg_tour",
              Owners(Zone.NorthGustaberg, "Hunting_Bear", "onTrigger", "Quest[1][45]"));

        var held = zone.EnabledQuestModules;
        P4Reload();
        Check("p4: EnabledQuestModules is a copy: one held across a pass keeps what it had", "1/0", $"{held.Count}/{zone.EnabledQuestModules.Count}");

        RunP4Folders(order);
    }

    // the folder rules (QuestModuleList, the C++ server's) on test-only modules under a folder no shipped module lives in
    // (P4FolderPrefix): the shipped era/ folder gains a module with each port, so a check that listed it would read lines
    // that change with every port (and one filtered to a single module's lines would miss another's condition gone wrong:
    // PlatformP4b checks every shipped module's condition against its Lua). Matching is by the C# module's Lua file, so
    // the test modules need no file on disk. In the folder's path order: a (xi.pre(ROV), patches The Gustaberg Tour),
    // c (not xi.pre(ROV): off here), sub/b (no condition, patches A Test of True Love); the settings are RESTRICT_CONTENT 1,
    // ENABLE_ROV 0, so a and b are enabled and c is listed, not enabled
    private const string P4FolderPrefix = "custom/lua/p4_folder";

    private static void RunP4Folders(List<string> order)
    {
        const string totlKey = "bastok.a_test_of_true_love";
        string fileA = $"modules/{P4FolderPrefix}/a.lua", fileB = $"modules/{P4FolderPrefix}/sub/b.lua", fileC = $"modules/{P4FolderPrefix}/c.lua";
        Check("p4 folders: no shipped module lives under the test modules' folder (modules/custom/lua/p4_...)", "",
              string.Join(" | ", ShippedQuestModuleTypes().Select(t => t.Module)
                  .Where(m => m != null && m.LuaFile.StartsWith("modules/custom/lua/p4_", StringComparison.OrdinalIgnoreCase))
                  .Select(m => $"{m.Name} ({m.LuaFile})")));
        zone.TestQuestModules.Add(new P4LiveModule("p4_folder_a", fileA, s => QuestModule.Pre(s, QuestExpansion.Rov), m => m.Quest(GustabergKey)
            .State(2).Npc(Zone.NorthGustaberg, XI.QuestContent.Npc.NorthGustaberg.HuntingBear).WrapOnTrigger((q, b) => b(q))));
        zone.TestQuestModules.Add(new P4LiveModule("p4_folder_b", fileB, null, m => m.Quest(totlKey)
            .State(2).Npc(Zone.PortBastok, XI.QuestContent.Npc.PortBastok.Carmelo).WrapOnTrigger((q, b) => b(q))));
        zone.TestQuestModules.Add(new P4LiveModule("p4_folder_c", fileC, s => !QuestModule.Pre(s, QuestExpansion.Rov), m => m.Quest(GustabergKey)
            .State(3).Npc(Zone.NorthGustaberg, XI.QuestContent.Npc.NorthGustaberg.HuntingBear).WrapOnTrigger((q, b) => b(q))));
        string gustaberg = Lua("report('Quest[1][' .. xi.quest.id.bastok.THE_GUSTABERG_TOUR .. ']')");
        string totl = Lua("report('Quest[1][' .. xi.quest.id.bastok.A_TEST_OF_TRUE_LOVE .. ']')");
        string enabledA = $"modules: p4_folder_a enabled ({fileA})", enabledB = $"modules: p4_folder_b enabled ({fileB})";
        string offC = $"modules: p4_folder_c listed, not enabled: its condition does not hold ({fileC})";
        // the container list with these quests moved to the end, in this order (the rest in their order)
        string Moved(params string[] last) => string.Join("\n", order.Where(id => !last.Contains(id)).Concat(last));
        string Enabled() => string.Join(",", zone.EnabledQuestModules.Select(m => m.Name));
        try
        {
            var lines = P4Reload("# comment", P4FolderPrefix + "/");
            Check("p4 folders: a folder line (trailing /, after a comment) enables its modules whose condition holds, in path order; the other listed, not enabled",
                  $"{enabledA} | {offC} | {enabledB}|p4_folder_a,p4_folder_b", $"{string.Join(" | ", lines)}|{Enabled()}");
            Check("p4 folders:   the container order: the patched quests at the end, in the modules' order", Moved(gustaberg, totl), string.Join("\n", zone.ContainerOrder(lua)));
            Check("p4 folders:   each enabled module's labels; the one not enabled none",
                  $"{GustabergKey} 2/Hunting_Bear/onTrigger|{totlKey} 2/Carmelo/onTrigger|null",
                  $"{string.Join(",", zone.ModuleLabels(fileA))}|{string.Join(",", zone.ModuleLabels(fileB))}|{(zone.ModuleLabels(fileC) == null ? "null" : "labels")}");

            lines = P4Reload(P4FolderPrefix);
            Check("p4 folders: no trailing /: the same", $"{enabledA} | {offC} | {enabledB}|p4_folder_a,p4_folder_b", $"{string.Join(" | ", lines)}|{Enabled()}");

            lines = P4Reload(P4FolderPrefix + "/", P4FolderPrefix + "/sub/b.lua");
            Check("p4 folders: a file line loads before every folder line's files (moduleutils.cpp's expanded list), each module once",
                  $"{enabledB} | {enabledA} | {offC}|p4_folder_b,p4_folder_a", $"{string.Join(" | ", lines)}|{Enabled()}");
            Check("p4 folders:   the container order follows the modules' order", Moved(totl, gustaberg), string.Join("\n", zone.ContainerOrder(lua)));

            lines = P4Reload(P4FolderPrefix + "/sub");
            Check("p4 folders: a subfolder line enables only the modules under it", $"{enabledB}|p4_folder_b", $"{string.Join(" | ", lines)}|{Enabled()}");

            lines = P4Reload("CUSTOM/Lua/P4_Folder/");
            Check($"p4 folders: a folder line in other case: {(QuestModuleList.IgnoreCase ? "the same (Windows: names without case)" : "nothing (names with case)")}",
                  QuestModuleList.IgnoreCase ? $"{enabledA} | {offC} | {enabledB}|p4_folder_a,p4_folder_b" : "|",
                  $"{string.Join(" | ", lines)}|{Enabled()}");

            lines = P4Reload("#" + P4FolderPrefix, "# " + P4FolderPrefix + "/", P4FolderPrefix + "_x", "custom/lua/p4_fold", P4FolderPrefix + "/empty", P4FolderPrefix + "/a");
            Check("p4 folders: comments, a sibling folder, a part of a name, a folder with no module, a file without .lua: nothing",
                  "|0", $"{string.Join(" | ", lines)}|{zone.EnabledQuestModules.Count}");
            Check("p4 folders:   the container order as without modules", string.Join("\n", order), string.Join("\n", zone.ContainerOrder(lua)));

            zone.CSharpQuestsOff.Add(GustabergKey);
            lines = P4Reload(P4FolderPrefix);
            Check("p4 folders: a module's quest in CSHARP_QUESTS_OFF: left to its Lua, the module enabled with nothing to patch; the others as before",
                  $"modules: p4_folder_a: {GustabergKey} is in CSHARP_QUESTS_OFF; its Lua file is in charge and the C# overrides are not applied" +
                  $" | {enabledA} | {offC} | {enabledB}|p4_folder_a,p4_folder_b|",
                  $"{string.Join(" | ", lines)}|{Enabled()}|{string.Join(",", zone.ModuleLabels(fileA))}");
            Check("p4 folders:   only the other module's quest moved", Moved(totl), string.Join("\n", zone.ContainerOrder(lua)));
        }
        finally
        {
            zone.CSharpQuestsOff.Remove(GustabergKey);
            zone.TestQuestModules.Clear();
            P4Reload();
        }
    }

    private sealed class P4LiveModule : QuestModule
    {
        private readonly Action<QuestModuleBuilder> build_;
        private readonly Func<IQuestSettings, bool> condition_;

        public P4LiveModule(string name, Action<QuestModuleBuilder> build) : this(name, $"modules/custom/lua/{name}.lua", null, build) { }

        // condition: null for none (enabled once listed)
        public P4LiveModule(string name, string luaFile, Func<IQuestSettings, bool> condition, Action<QuestModuleBuilder> build) : base(name, luaFile)
        {
            build_ = build;
            condition_ = condition;
        }

        public override bool Condition(IQuestSettings settings) => condition_ == null || condition_(settings);

        protected override void Build(QuestModuleBuilder m) => build_(m);
    }

    private static void RunP4Refusals()
    {
        zone.TestQuestModules.Add(new P4LiveModule("p4_live_missing", m => m.Quest(GustabergKey)
            .State(2).Npc(Zone.NorthGustaberg, XI.QuestContent.Npc.NorthGustaberg.HuntingBear).WrapOnTrade((q, b) => b(q))));
        int mark = log.Count;
        var lines = P4Reload("custom/lua/p4_live_missing.lua");
        Check("p4: an override of a handler the quest does not have: the module refused, with the handler",
              $"modules: p4_live_missing refused: {GustabergKey}: has no handler 2/Hunting_Bear/onTrade in zone {Zone.NorthGustaberg} to wrap", string.Join(" | ", lines));
        Check("p4:   the quest registered as it is", $"quests: {RegisteredCount} C# quests registered|0",
              $"{Untimed(log.Skip(mark).LastOrDefault(IsRegistered))}|{zone.EnabledQuestModules.Count}");

        zone.TestQuestModules.Add(new P4LiveModule("p4_live_no_quest", m => m.Quest("bastok.no_such_quest").State(1).ReplaceCheck(q => false)));
        lines = P4Reload("custom/lua/p4_live_no_quest.lua");
        Check("p4: a module naming no C# quest: refused", "modules: p4_live_no_quest refused: bastok.no_such_quest is not a C# quest", string.Join(" | ", lines));

        zone.TestQuestModules.Add(new P4LiveModule("p4_live_invalid", m => m.Quest(GustabergKey)
            .AddStates(b => b.State(XI.Quests.QuestStatus.Completed).Npc(Zone.NorthGustaberg, "Nobody_Here").OnTrigger(q => null))));
        lines = P4Reload("custom/lua/p4_live_invalid.lua");
        Check("p4: the validator on the patched quest (an added handler on no NPC): refused",
              $"modules: p4_live_invalid refused: {GustabergKey} patched: no NPC or mob named Nobody_Here in zone {Zone.NorthGustaberg}", string.Join(" | ", lines));

        // CheckQuestModules compiles each module under the settings of now and each xi.pre branch: an override that only a
        // restricted server's Build declares is checked too
        zone.TestQuestModules.Add(new P4LiveModule("p4_live_branch", m =>
        {
            if (m.Pre(QuestExpansion.Rov)) m.Quest(GustabergKey).State(9).ReplaceCheck(q => true);
            else m.Quest(GustabergKey).State(1).ReplaceCheck(q => true);
        }));
        Lua("xi.settings.main.RESTRICT_CONTENT = 0");
        Check("p4: CheckQuestModules: a Build's xi.pre branch the settings of now do not take is checked too",
              $"p4_live_branch: {GustabergKey}: has no state 9 (it has 3) for its check (with xi.pre true)",
              string.Join(" | ", zone.CheckQuestModules().Where(p => p.StartsWith("p4_live_branch", StringComparison.Ordinal))));
        Lua("xi.settings.main.RESTRICT_CONTENT = 1");
        Check("p4:   one the settings of now take: named without a branch", $"p4_live_branch: {GustabergKey}: has no state 9 (it has 3) for its check",
              string.Join(" | ", zone.CheckQuestModules().Where(p => p.StartsWith("p4_live_branch", StringComparison.Ordinal))));
        zone.TestQuestModules.RemoveAll(m => m.Name == "p4_live_branch");

        zone.TestQuestModules.Add(new P4LiveModule("p4_live_second", m => m.Quest(GustabergKey)
            .State(3).Npc(Zone.NorthGustaberg, XI.QuestContent.Npc.NorthGustaberg.HuntingBear).WrapOnTrigger((q, b) => b(q))));
        // two file lines load in line order (a folder line's files would come after every file line, as moduleutils.cpp
        // expands the list)
        lines = P4Reload(GustabergModule.Substring("modules/".Length), "custom/lua/p4_live_second.lua");
        Check("p4: two modules on one quest: the later one's overrides apply (as Lua's modifyInteractionEntry reloads the file)",
              $"modules: era_quest_the_gustaberg_tour enabled ({GustabergModule}) | modules: {GustabergKey} is patched by era_quest_the_gustaberg_tour and p4_live_second: " +
              "as on the Lua server, only p4_live_second's overrides apply (modifyInteractionEntry reloads the quest's file) | modules: p4_live_second enabled (modules/custom/lua/p4_live_second.lua)",
              string.Join(" | ", lines));
        Check("p4:   its labels; the earlier module's none", $"{GustabergKey} 3/Hunting_Bear/onTrigger|",
              $"{string.Join(",", zone.ModuleLabels("modules/custom/lua/p4_live_second.lua"))}|{string.Join(",", zone.ModuleLabels(GustabergModule) ?? new List<string>())}");
        zone.TestQuestModules.Clear();
    }

    // the coverage of a module's Reward and RemoveState through the registration (Northward's reward, A Hard Day's Knight's
    // table.remove(quest.sections, 3)): their labels, the reward reached when the quest completes with it, the removed
    // state reached when a check of the quest runs while its own matches would have held (not before)
    private static void RunP4RewardRemoved()
    {
        const string file = "modules/custom/lua/p4_live_reward.lua";
        zone.TestQuestModules.Add(new P4LiveModule("p4_live_reward", m => m.Quest(GustabergKey)
            .Reward(new QuestReward { Gil = 1234 })
            .RemoveState(3)));
        var lines = P4Reload("custom/lua/p4_live_reward.lua");
        Check("p4 reward/removed: the module enabled", $"modules: p4_live_reward enabled ({file})", string.Join(" | ", lines));
        Check("p4 reward/removed:   its labels: the removed state, the reward",
              $"{GustabergKey} 3/-/removed,{GustabergKey} reward", string.Join(",", zone.ModuleLabels(file)));
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, Zone.NorthGustaberg, -233.9f, 41.465f, 426.5f, 0);
        Login();
        var reached = new List<string>();
        zone.HandlerCalled = (k, label) => { if (k == XI.Zone.ZoneHost.ModuleCoverageKey(file)) reached.Add(label); };
        try
        {
            uint bear = NpcId(XI.QuestContent.Npc.NorthGustaberg.HuntingBear);
            Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.THE_GUSTABERG_TOUR)");
            int gil = int.Parse(Lua("report(tostring(player:getGil()))"));
            Lua($"InteractionGlobal.onEventFinish(player, 22, 0, GetNPCByID({bear}), function() end)");
            Check("p4 reward/removed: the finish completes the quest with the module's reward; reward reached, the removed state not yet",
                  $"true/1234/{GustabergKey} reward",
                  $"{Lua("report(tostring(player:hasCompletedQuest(xi.questLog.BASTOK, xi.quest.id.bastok.THE_GUSTABERG_TOUR)))")}/" +
                  $"{int.Parse(Lua("report(tostring(player:getGil()))")) - gil}/{string.Join(",", reached.Distinct())}");
            reached.Clear();
            // the event Hunting_Bear starts for the completed quest (state 3's 23 with the quest's own sections), "none"
            string Bear()
            {
                Lua($"InteractionGlobal.onTrigger(player, GetNPCByID({bear}), function() end)");
                string ev = c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "none";
                for (int i = 0; i < 4 && c.IsInEvent; i++)
                {
                    var running = (ushort)c.CurrentEvent.EventId;
                    Send(0x05B, 20, b => BitConverter.GetBytes(running).CopyTo(b, 0x12));
                }
                Collect();
                return ev;
            }
            string with = Bear();
            Check("p4 reward/removed: completed, Hunting_Bear: state 3 is gone (no 23), its removal reached",
                  $"True/{GustabergKey} 3/-/removed", $"{with != "23"}/{string.Join(",", reached.Distinct())}");
            zone.TestQuestModules.Clear();
            P4Reload();
            reached.Clear();
            Check("p4 reward/removed:   without the module the quest's own state 3 plays 23, and nothing is the module's", "23/",
                  $"{Bear()}/{string.Join(",", reached)}");
        }
        finally
        {
            zone.HandlerCalled = null;
            Logout();
            Cleanup(db, id);
            zone.TestQuestModules.Clear();
        }
    }

    // the container ids of the lookup's list for the entity's handler kind, in order ('default' for a default action)
    private static string ListIds(ushort zoneId, string entity, string kind) => Lua($@"
        local ids = {{}}
        local z = InteractionGlobal.lookup.data[{zoneId}]
        local list = z and z['{entity}'] and z['{entity}']['{kind}'] or {{}}
        for _, e in ipairs(list) do ids[#ids + 1] = e.container and tostring(e.container.id) or 'default' end
        report(table.concat(ids, ','))");

    // a patched quest that shares an NPC with another C# quest (A Test of True Love and Love and Ice on Carmelo): with the
    // module its entries move after the other quest's, as modifyInteractionEntry's removeContainer / addContainer leave
    // them, and its container to the end of the list; without it the list is as before
    private static void RunP4Placement()
    {
        const string key = "bastok.a_test_of_true_love";
        const string loveAndIce = "bastok.love_and_ice";
        string id = Lua("report('Quest[1][' .. xi.quest.id.bastok.A_TEST_OF_TRUE_LOVE .. ']')");
        string other = Lua("report('Quest[1][' .. xi.quest.id.bastok.LOVE_AND_ICE .. ']')");
        P4Reload();
        string before = ListIds(Zone.PortBastok, "Carmelo", "onTrigger");
        var orderBefore = zone.ContainerOrder(lua);
        Check("p4 placement: both quests are C# and share Carmelo's onTrigger list, the patched one not last",
              "True/True/True/True",
              $"{CSharpLive(key, id)}/{CSharpLive(loveAndIce, other)}/{before.Split(',').Contains(other)}/{before.Split(',').Last() != id}");
        zone.TestQuestModules.Add(new P4LiveModule("p4_live_carmelo", m => m.Quest(key)
            .State(2).Npc(Zone.PortBastok, XI.QuestContent.Npc.PortBastok.Carmelo).WrapOnTrigger((q, b) => b(q))));
        var lines = P4Reload("custom/lua/p4_live_carmelo.lua");
        Check("p4 placement:   the module enabled", "modules: p4_live_carmelo enabled (modules/custom/lua/p4_live_carmelo.lua)", string.Join(" | ", lines));
        var ids = before.Split(',');
        Check("p4 placement:   Carmelo's list: the patched quest's entries after every other's (Lua's remove and add)",
              string.Join(",", ids.Where(x => x != id).Concat(ids.Where(x => x == id))), ListIds(Zone.PortBastok, "Carmelo", "onTrigger"));
        Check("p4 placement:   the container list: the patched quest at the end, the rest in their order",
              string.Join("\n", orderBefore.Where(x => x != id).Append(id)), string.Join("\n", zone.ContainerOrder(lua)));
        zone.TestQuestModules.Clear();
        P4Reload();
        Check("p4 placement: no module: Carmelo's list and the container list as before", $"{before}|True",
              $"{ListIds(Zone.PortBastok, "Carmelo", "onTrigger")}|{orderBefore.SequenceEqual(zone.ContainerOrder(lua))}");
    }

    // whether the container id is the quest's in C# (CSharpQuests.live names it)
    private static bool CSharpLive(string key, string containerId) =>
        Lua($"report(tostring(CSharpQuests.live['{containerId}'] == '{key}'))") == "true";
}
