using System;
using System.IO;
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

// quest rework C2 Task 8 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section 3.3):
// the battlefield kind. Cactuar Suave (Chamber of Oracles) and Rank 2 Mission 2 (Waughroon Shrine) are C#: each is
// its zone's xi.battlefield.contents entry and in contentsByZone once, a proxy content table with the fields the
// engine reads. Cactuar Suave's file sorts first in its zone, so it takes the zone's events and SC_Entrance's hooks,
// which the Lua Cat Burglar Bares Fangs (the next one) would take without it; Rank 2 comes after the Lua 3,2,1... and
// takes only Burning_Circle's exit hook. A test-only C# Eye of the Storm (never in XI.QuestContent), whose Lua file
// stays, takes the Lua content's place at its index, or the slot of its LegacyPath with the file hidden;
// CSHARP_QUESTS_OFF leaves the Lua file in charge, and with no file logs that it stays in C#; a broken one with no
// file logs the no-fallback line.
internal static partial class Program
{
    private const string EyeKey = "battlefield.eye_of_the_storm";
    private const string EyeLuaFile = "scripts/battlefields/Chamber_of_Oracles/eye_of_the_storm";
    private const string EyeContainer = "Battlefield[198]";
    private const string NoSuchKey = "battlefield.no_such_battlefield";

    // Eye of the Storm's fields (the essential mobs and the loot left out); exitNpc: its exit NPC
    private sealed class SampleBattlefield : BattlefieldScript
    {
        private readonly string legacyPath_;

        public SampleBattlefield(string legacyPath = null, string exitNpc = "Shimmering_Circle",
                                 string constName = "EYE_OF_THE_STORM")
            : base(constName)
        {
            legacyPath_ = legacyPath;
            Data = new BattlefieldData
            {
                ZoneId = QC.Zone.ChamberOfOracles, BattlefieldId = QC.Battlefield.EyeOfTheStorm, MaxPlayers = 6,
                TimeLimit = 1800, Index = 6, EntryNpcs = new[] { "SC_Entrance" }, ExitNpcs = new[] { exitNpc },
                RequiredItems = new BattlefieldRequiredItems
                {
                    Items = new[] { QC.Item.LachesisOrb },
                    WearMessage = QC.Text.ChamberOfOracles.A_CRACK_HAS_FORMED,
                    WornMessage = QC.Text.ChamberOfOracles.ORB_IS_CRACKED,
                },
            };
        }

        public override string LegacyPath => legacyPath_;
        public override BattlefieldData Data { get; }
    }

    // the battlefield ids of xi.battlefield.contentsByZone[zone], in order
    private static string ZoneContents(ushort zoneId) => Lua($@"
        local ids = {{}}
        for _, c in ipairs(xi.battlefield.contentsByZone[{zoneId}] or {{}}) do
            ids[#ids + 1] = tostring(c.battlefieldId)
        end
        report(table.concat(ids, ','))");

    // the files of a content's hooks: the lookup's entries of zone/second/third that the container with the id owns
    private static string Hooks(ushort zoneId, string second, string third, int id) =>
        Owners(zoneId, second, third, $"Battlefield[{id}]");

    private static void RunC2Battlefields()
    {
        Console.WriteLine("quest rework C2: the battlefield kind");
        ushort coo = QC.Zone.ChamberOfOracles, ws = QC.Zone.WaughroonShrine;
        int cactuar = QC.Battlefield.CactuarSuave, rank2 = QC.Battlefield.Rank2Mission2;
        string cKey = QC.BattlefieldKey.CactuarSuave, rKey = QC.BattlefieldKey.Rank2Mission2;
        string root = Path.GetDirectoryName(dataDir_);
        NewWorld();
        Check("c2 battlefield: the two Lua files are gone", "False False",
            $"{File.Exists(Path.Combine(root, "scripts/battlefields/Chamber_of_Oracles/cactuar_suave.lua"))} " +
            File.Exists(Path.Combine(root, "scripts/battlefields/Waughroon_Shrine/rank_2_mission.lua")));
        Check("c2 battlefield: contents[197] and contents[64] are the C# proxies; CSharpQuests.live has both",
            $"csharp:{cKey} csharp:{rKey} {cKey} {rKey}",
            Lua($"report(tostring(xi.battlefield.contents[{cactuar}].filename) .. ' ' .. " +
                $"tostring(xi.battlefield.contents[{rank2}].filename) .. ' ' .. " +
                $"tostring(CSharpQuests.live['Battlefield[{cactuar}]']) .. ' ' .. " +
                $"tostring(CSharpQuests.live['Battlefield[{rank2}]']))"));
        string coos = ZoneContents(coo), wss = ZoneContents(ws);
        Check("  each in its zone's contentsByZone once; Cactuar Suave first in Chamber of Oracles", "True 1 1",
            $"{coos.StartsWith($"{cactuar},")} {coos.Split(',').Count(x => x == $"{cactuar}")} " +
            wss.Split(',').Count(x => x == $"{rank2}"));
        Check("  the fields the engine reads (Rank 2: levelCap MAX_LEVEL, a mission, no exp loss, trusts, 6, 900)",
            "99 true false true 6 900 true",
            Lua($"local c = xi.battlefield.contents[{rank2}] report(tostring(c.levelCap) .. ' ' .. " +
                "tostring(c.isMission) .. ' ' .. tostring(c.canLoseExp) .. ' ' .. tostring(c.allowTrusts) .. ' ' .. " +
                "tostring(c.maxPlayers) .. ' ' .. tostring(c.timeLimit) .. ' ' .. tostring(c.showTimer))"));
        Check("  Cactuar Suave's requiredItems and tradeItems as Battlefield:new leaves them",
            $"1 {QC.Item.ClothoOrb} true true {QC.Item.ClothoOrb} 9",
            Lua($"local c = xi.battlefield.contents[{cactuar}] report(#c.requiredItems .. ' ' .. " +
                "tostring(c.requiredItems[1]) .. ' ' .. tostring(c.requiredItems.wearMessage ~= nil) .. ' ' .. " +
                "tostring(c.requiredItems.wornMessage ~= nil) .. ' ' .. tostring(c.tradeItems[1]) .. ' ' .. #c.loot)"));
        Check("c2 battlefield: Cactuar Suave takes Chamber of Oracles' events and SC_Entrance's hooks, the next Lua " +
              "one (Cat Burglar) none; both keep their exit hooks",
            $"csharp:{cKey}|csharp:{cKey}|csharp:{cKey}|true||||nil|csharp:{cKey}|" +
            "scripts/battlefields/Chamber_of_Oracles/cat_burglar_bares_fangs",
            $"{Hooks(coo, "SC_Entrance", "onTrigger", cactuar)}|{Hooks(coo, "SC_Entrance", "onTrade", cactuar)}|" +
            $"{Hooks(coo, "onEventUpdate", "32000", cactuar)}|" +
            Lua($"report(tostring(xi.battlefield.contents[{cactuar}].hasListeners))") + "|" +
            $"{Hooks(coo, "SC_Entrance", "onTrigger", 200)}|{Hooks(coo, "onEventUpdate", "32000", 200)}|" +
            $"{Hooks(coo, "onEventFinish", "32001", 200)}|" +
            Lua("report(tostring(xi.battlefield.contents[200].hasListeners))") + "|" +
            $"{Hooks(coo, "Shimmering_Circle", "onTrigger", cactuar)}|" +
            Hooks(coo, "Shimmering_Circle", "onTrigger", 200));
        Check("c2 battlefield: Rank 2 after the Lua 3,2,1...: Burning_Circle's exit hook only, BC_Entrance and the " +
              "events stay 3,2,1...'s",
            $"csharp:{rKey}|||nil|scripts/battlefields/Waughroon_Shrine/3_2_1|" +
            "scripts/battlefields/Waughroon_Shrine/3_2_1",
            $"{Hooks(ws, "Burning_Circle", "onTrigger", rank2)}|{Hooks(ws, "BC_Entrance", "onTrigger", rank2)}|" +
            $"{Hooks(ws, "onEventUpdate", "32000", rank2)}|" +
            Lua($"report(tostring(xi.battlefield.contents[{rank2}].hasListeners))") + "|" +
            $"{Hooks(ws, "BC_Entrance", "onTrigger", 69)}|{Hooks(ws, "onEventUpdate", "32000", 69)}");

        // each battlefield's exit hook is its own: the Lua ones' sections keep Battlefield.onExitTrigger (a C# hook
        // written into another content's section too would play the same, so the lookup's owners cannot tell)
        Check("c2 battlefield: the Lua 3,2,1... and Cat Burglar keep Battlefield.onExitTrigger; the C# ones' are " +
              "not it",
            "true true false false",
            Lua("local function exit(id, zone, npc) " +
                "return tostring(xi.battlefield.contents[id].sections[1][zone][npc]" +
                ".onTrigger == Battlefield.onExitTrigger) end " +
                $"report(exit(69, {ws}, 'Burning_Circle') .. ' ' .. exit(200, {coo}, 'Shimmering_Circle') .. ' ' .. " +
                $"exit({rank2}, {ws}, 'Burning_Circle') .. ' ' .. exit({cactuar}, {coo}, 'Shimmering_Circle'))"));

        // Task 9: the BattlefieldMission and BattlefieldQuest proxies carry their class's fields
        int stc = QC.Battlefield.SaveTheChildren, tbe = QC.Battlefield.TrialByEarth;
        Check("c2 battlefield: Task 9's three Lua files are gone", "False False False",
            $"{File.Exists(Path.Combine(root, "scripts/battlefields/Ghelsba_Outpost/save_the_children.lua"))} " +
            $"{File.Exists(Path.Combine(root, "scripts/battlefields/Cloister_of_Tremors/trial_by_earth.lua"))} " +
            File.Exists(Path.Combine(root, "scripts/missions/sandoria/1_3_Save_the_Children.lua")));
        Check("c2 battlefield: Save the Children's proxy: C#, BattlefieldMission:new's fields (isMission, no exp " +
              "loss, San d'Oria's 2, the status from log 0, required 2, skip 2, no missionStatus or requiredVar), " +
              "area 1, levelCap MAX_LEVEL",
            $"csharp:{QC.BattlefieldKey.SaveTheChildren} true false 0 2 0 2 2 nil nil 1 99",
            Lua($"local c = xi.battlefield.contents[{stc}] report(tostring(c.filename) .. ' ' .. " +
                "tostring(c.isMission) .. ' ' .. tostring(c.canLoseExp) .. ' ' .. tostring(c.missionArea) .. ' ' .. " +
                "tostring(c.mission) .. ' ' .. tostring(c.missionStatusArea) .. ' ' .. " +
                "tostring(c.requiredMissionStatus) .. ' ' .. tostring(c.skipMissionStatus) .. ' ' .. " +
                "tostring(c.missionStatus) .. ' ' .. tostring(c.requiredVar) .. ' ' .. tostring(c.area) .. ' ' .. " +
                "tostring(c.levelCap))"));
        Check("c2 battlefield: Trial by Earth's proxy: C#, BattlefieldQuest:new's fields (Bastok's quest 61, not " +
              "repeatable, isQuest), no exp loss, not a mission, the Tuning Fork of Earth",
            $"csharp:{QC.BattlefieldKey.TrialByEarth} 1 61 false true false false {QC.KeyItem.TuningForkOfEarth}",
            Lua($"local c = xi.battlefield.contents[{tbe}] report(tostring(c.filename) .. ' ' .. " +
                "tostring(c.questArea) .. ' ' .. tostring(c.quest) .. ' ' .. tostring(c.repeatable) .. ' ' .. " +
                "tostring(c.isQuest) .. ' ' .. tostring(c.canLoseExp) .. ' ' .. tostring(c.isMission) .. ' ' .. " +
                "tostring(c.requiredKeyItems[1]))"));

        // a reload: the zones' lists and hooks are the same
        Lua("InteractionGlobal.reload(true)");
        Check("c2 battlefield: after a reload the zones' contents and hooks are the same",
            $"{coos}|{wss}|csharp:{cKey}|",
            $"{ZoneContents(coo)}|{ZoneContents(ws)}|{Hooks(coo, "SC_Entrance", "onTrigger", cactuar)}|" +
            Hooks(coo, "SC_Entrance", "onTrigger", 200));

        // the test Eye of the Storm with its Lua file there: at the Lua content's index, the list otherwise the same
        Check("c2 battlefield: Eye of the Storm's Lua file is there", "True",
            File.Exists(Path.Combine(root, EyeLuaFile + ".lua")).ToString());
        var luaOrder = zone.ContainerOrder(lua);
        int luaAt = luaOrder.IndexOf(EyeContainer);
        int mark = log.Count;
        zone.TestQuestScripts.Add(new SampleBattlefield());
        Lua("InteractionGlobal.reload(true)");
        var order = zone.ContainerOrder(lua);
        Check("c2 battlefield: registered, the container at Battlefield[198]'s index is the C# one",
            $"True {luaAt} csharp:{EyeKey}",
            $"{luaAt >= 0} {order.IndexOf(EyeContainer)} {ContainerFile(EyeContainer)}");
        Check("  the list is otherwise the same", string.Join("\n", luaOrder), string.Join("\n", order));
        Check("  contents[198] is it, in contentsByZone once; the zone's other hooks unchanged",
            $"csharp:{EyeKey} 1 csharp:{cKey}|csharp:{EyeKey}",
            Lua($"report(tostring(xi.battlefield.contents[198].filename))") + " " +
            ZoneContents(coo).Split(',').Count(x => x == "198") + " " +
            $"{Hooks(coo, "SC_Entrance", "onTrigger", cactuar)}|{Hooks(coo, "Shimmering_Circle", "onTrigger", 198)}");
        Check("  no quests: line", "",
            string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: ") && !IsRegistered(l))));

        // the Lua file hidden: the C# one takes the file's slot by its LegacyPath
        NewWorld(new[] { EyeLuaFile }, false);
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile));
        Lua("InteractionGlobal.reload(true)");
        order = zone.ContainerOrder(lua);
        Check("c2 battlefield: the Lua file hidden: inserted at its LegacyPath's slot, the list as with the file",
            $"{luaAt} csharp:{EyeKey} True", $"{order.IndexOf(EyeContainer)} {ContainerFile(EyeContainer)} " +
            $"{order.SequenceEqual(luaOrder)}");

        // a broken one with no Lua file: the no-fallback line, the key in QuestLoadFailures, no container
        NewWorld(new[] { EyeLuaFile }, false);
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile, "No_Such_Circle"));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 battlefield: a broken one with no Lua file: the no-fallback line",
            $"quests: C# battlefield {EyeKey} failed to load and has no Lua fallback: " +
            $"no NPC No_Such_Circle in zone {coo}",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains("no Lua fallback"))));
        Check("  QuestLoadFailures has the key, no container has its id", $"{EyeKey} False",
            $"{string.Join(",", zone.QuestLoadFailures)} {zone.ContainerOrder(lua).Contains(EyeContainer)}");

        // an unknown constant with no Lua file at its LegacyPath: the no-fallback line, the key counted
        NewWorld(new[] { EyeLuaFile }, false);
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile, constName: "NO_SUCH_BATTLEFIELD"));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 battlefield: an unknown constant with no Lua file: the no-fallback line, the key in " +
              "QuestLoadFailures",
            $"quests: C# battlefield {NoSuchKey} failed to load and has no Lua fallback: unknown battlefield " +
            $"constant|{NoSuchKey}",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(NoSuchKey))) + "|" +
            string.Join(",", zone.QuestLoadFailures));
        // with the Lua file at its LegacyPath there: not registered, nothing counted
        NewWorld();
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile, constName: "NO_SUCH_BATTLEFIELD"));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 battlefield: an unknown constant with the Lua file there: not registered, nothing counted",
            $"quests: {NoSuchKey} not registered: unknown battlefield constant|",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(NoSuchKey))) + "|" +
            string.Join(",", zone.QuestLoadFailures));

        // two classes with the key and no Lua file: the first registered, the second's no-fallback line
        NewWorld(new[] { EyeLuaFile }, false);
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile));
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        string duplicateLine = $"quests: C# battlefield {EyeKey} failed to load and has no Lua fallback: " +
                               "another C# container (";
        Check("c2 battlefield: a second class with the key and no Lua file: the no-fallback line, the key counted, " +
              "the first in C#",
            $"1 True|{EyeKey}|csharp:{EyeKey}",
            $"{log.Skip(mark).Count(l => l.Contains("no Lua fallback"))} " +
            $"{log.Skip(mark).Any(l => l.StartsWith(duplicateLine, StringComparison.Ordinal))}|" +
            $"{string.Join(",", zone.QuestLoadFailures)}|{ContainerFile(EyeContainer)}");

        // the shared crate callback with no C# callback pending: logged, the crate stays shut (false)
        NewWorld();
        mark = log.Count;
        lua.PushRef(zone.openCrateCallback_);
        lua.Call(0, 1);
        bool opened = lua.ToBoolean(-1);
        lua.Pop(1);
        Check("c2 battlefield: the crate callback with none pending: logged, false",
            "quests: a C# battlefield crate callback ran with no callback pending; the crate stays shut|False",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains("crate callback"))) + $"|{opened}");

        // CSHARP_QUESTS_OFF with the Lua file there: the file stays in charge
        NewWorld(EyeKey);
        zone.TestQuestScripts.Add(new SampleBattlefield());
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 battlefield: CSHARP_QUESTS_OFF with the Lua file: the Lua content stays, nothing logged",
            $"{EyeLuaFile} {EyeLuaFile} ", $"{ContainerFile(EyeContainer)} " +
            Lua("report(tostring(xi.battlefield.contents[198].filename))") + " " +
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(EyeKey))));

        // CSHARP_QUESTS_OFF with no Lua file: logged, it stays in C#
        NewWorld(new[] { EyeLuaFile }, false, EyeKey);
        zone.TestQuestScripts.Add(new SampleBattlefield(EyeLuaFile));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 battlefield: CSHARP_QUESTS_OFF with no Lua file: logged, it stays in C#",
            $"quests: {EyeKey} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#/csharp:{EyeKey}",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(EyeKey))) + "/" + ContainerFile(EyeContainer));

        // CSHARP_QUESTS_OFF for Cactuar Suave (no Lua file): it stays in C#
        NewWorld(cKey);
        Check("c2 battlefield: CSHARP_QUESTS_OFF for Cactuar Suave: it stays in C# (its file is gone)",
            $"csharp:{cKey}", Lua($"report(tostring(xi.battlefield.contents[{cactuar}].filename))"));
        zone.TestQuestScripts.Clear();
        NewWorld();
    }
}
