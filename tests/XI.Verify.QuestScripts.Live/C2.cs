using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Persistence;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

// quest rework C2 Task 3 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, sections 3.2
// and 3.7): a test-only C# mission, the RoV fixture mission at Naji (never in XI.QuestContent), registered through
// ZoneHost.TestQuestScripts while its Lua fixture (Fixtures/C3_Mission_Rov.lua) is listed. It takes the Lua
// Mission container's place (Mission[13][60]) at its index; with the Lua file hidden it takes the file's slot by its
// LegacyPath; CSHARP_QUESTS_OFF leaves the Lua file in charge, and with no file logs that it stays in C#; a broken one
// with no file logs the no-fallback line. In play its section checks read the current mission and the status from
// Mission's check arguments, and its q.Mission.Complete() leaves the char (missions blob, rank points, char_vars)
// as the Lua Mission:complete with the same reward leaves another, on RoV and on CoP (whose status
// Mission:complete keeps)
internal static partial class Program
{
    // the test missions' keys: constants no Lua file or C# mission names (RunTestKeysFree holds them so). Their Lua
    // containers are Fixtures/C3_Mission_Rov.lua and C3_Mission_Cop.lua, so no lane's port moves them
    private const string SampleKey = "mission.rov.the_endless_sky";
    private const string SampleLuaFile = "scripts/missions/rov/QSL_Mission_Fixture";
    private const string SampleSourceFile = "tests/XI.Verify.QuestScripts.Live/Fixtures/C3_Mission_Rov.lua";
    private const string SampleContainer = "Mission[13][60]";
    private const string CopSampleKey = "mission.cop.descendants_of_a_line_lost";
    private const string CopSampleLuaFile = "scripts/missions/cop/QSL_Mission_Fixture";
    private const string CopSampleSourceFile = "tests/XI.Verify.QuestScripts.Live/Fixtures/C3_Mission_Cop.lua";
    private const string CopSampleContainer = "Mission[6][335]";

    // the RoV fixture mission at Naji (Metalworks): status 0 plays 1000 and sets status 1; status 1 plays 1001 and
    // completes with 400 rank points. legacyPath: its LegacyPath (null: none, the Lua container's slot)
    private sealed class SampleMission : MissionScript
    {
        private readonly string legacyPath_;
        private readonly ushort zone_;

        public SampleMission(string legacyPath = null, ushort zone = QC.Zone.Metalworks)
            : base(MissionLog.RoV, "THE_ENDLESS_SKY")
        {
            legacyPath_ = legacyPath;
            zone_ = zone;
        }

        public override string LegacyPath => legacyPath_;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 400 });
            b.State(current: true, status: 0).Npc(zone_, "Naji").OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(zone_, 1000, q => q.Mission.SetStatus(1));
            b.State(current: true, status: 1).Npc(zone_, "Naji").OnTrigger(QuestReply.Event(1001))
                .OnEventFinish(zone_, 1001, q => q.Mission.Complete());
        }
    }

    // the CoP fixture mission at Naji: status 3 plays 1002 and completes, its reward the next mission
    // (The Mothercrystals)
    private sealed class SampleCopMission : MissionScript
    {
        public SampleCopMission() : base(MissionLog.CoP, "DESCENDANTS_OF_A_LINE_LOST") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { NextLog = MissionLog.CoP, NextMission = QC.Mission.Cop.TheMothercrystals });
            b.State(current: true, status: 3).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1002))
                .OnEventFinish(QC.Zone.Metalworks, 1002, q => q.Mission.Complete());
        }
    }

    private static void RunC2()
    {
        Console.WriteLine("quest rework C2: the mission kind");
        RunC2Registration();
        RunC2Complete();
        RunC2Calls();
        // quest rework C2 Task 8: the battlefield kind (C2Battlefields.cs)
        RunC2Battlefields();
        RunC3Prep();
        // quest rework C3 PR1: testimony_single_use on a C# battlefield (C3Testimony.cs)
        RunC3Testimony();
        // quest rework C3 PR2: a C# battlefield's deferred callbacks (C3Callbacks.cs)
        RunC3PR2Live();
        zone.TestQuestScripts.Clear();
        NewWorld();
    }

    // the file of the container with the id, as the lookup's handler entries hold it ('-' for none)
    private static string ContainerFile(string containerId) => Lua($@"
        local file = '-'
        for _, z in pairs(InteractionGlobal.lookup.data) do
            for _, second in pairs(z) do
                for _, list in pairs(type(second) == 'table' and second or {{}}) do
                    for _, e in ipairs(type(list) == 'table' and list or {{}}) do
                        if type(e) == 'table' and e.container and e.container.id == '{containerId}' then
                            file = tostring(e.container.filename)
                        end
                    end
                end
            end
        end
        report(file)");

    private static void RunC2Registration()
    {
        string root = Path.GetDirectoryName(dataDir_);
        Check("c2 mission: the Lua file is there", "True",
            File.Exists(Path.Combine(root, SampleSourceFile)).ToString());
        NewWorld();
        var luaOrder = zone.ContainerOrder(lua);
        int luaAt = luaOrder.IndexOf(SampleContainer);
        Check($"c2 mission: {SampleContainer} is the Lua file's", $"True {SampleLuaFile}",
            $"{luaAt >= 0} {ContainerFile(SampleContainer)}");

        int mark = log.Count;
        zone.TestQuestScripts.Add(new SampleMission());
        Lua("InteractionGlobal.reload(true)");
        var order = zone.ContainerOrder(lua);
        Check($"c2 mission: registered, the container at {SampleContainer}'s index is the C# mission's",
            $"{luaAt} csharp:{SampleKey}", $"{order.IndexOf(SampleContainer)} {ContainerFile(SampleContainer)}");
        Check("  the list is otherwise the same", string.Join("\n", luaOrder), string.Join("\n", order));
        Check("  CSharpQuests.live has it", SampleKey,
            Lua($"report(tostring(CSharpQuests.live['{SampleContainer}']))"));
        Check($"  Naji's onTrigger entries of {SampleContainer} are the C# mission's two sections",
            $"csharp:{SampleKey};csharp:{SampleKey}", Owners(QC.Zone.Metalworks, "Naji", "onTrigger", SampleContainer));
        Check("  no quests: line", "",
            string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: ") && !IsRegistered(l))));

        // the Lua file hidden: the C# mission takes the file's slot by its LegacyPath
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new SampleMission(SampleLuaFile));
        Lua("InteractionGlobal.reload(true)");
        order = zone.ContainerOrder(lua);
        Check("c2 mission: the Lua file hidden: inserted at its LegacyPath's slot, the list as with the file",
            $"{luaAt} csharp:{SampleKey} True", $"{order.IndexOf(SampleContainer)} {ContainerFile(SampleContainer)} " +
            $"{order.SequenceEqual(luaOrder)}");

        // a broken mission with no Lua file: the no-fallback line, the key in QuestLoadFailures, no container
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new SampleMission(SampleLuaFile, QC.Zone.BastokMines));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 mission: a broken one with no Lua file: the no-fallback line",
            $"quests: C# mission {SampleKey} failed to load and has no Lua fallback: " +
            $"no NPC or mob named Naji in zone {QC.Zone.BastokMines}",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains("no Lua fallback"))));
        Check("  QuestLoadFailures has the key, no container has its id", $"{SampleKey} False",
            $"{string.Join(",", zone.QuestLoadFailures)} {zone.ContainerOrder(lua).Contains(SampleContainer)}");

        // CSHARP_QUESTS_OFF with the Lua file there: the file stays in charge, the key is known
        NewWorld(SampleKey);
        zone.TestQuestScripts.Add(new SampleMission());
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 mission: CSHARP_QUESTS_OFF with the Lua file: the Lua container stays, nothing logged",
            $"{SampleLuaFile} ", $"{ContainerFile(SampleContainer)} " +
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(SampleKey))));

        // CSHARP_QUESTS_OFF with no Lua file: logged, it stays in C#
        NewWorld(new[] { SampleLuaFile }, false, SampleKey);
        zone.TestQuestScripts.Add(new SampleMission(SampleLuaFile));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 mission: CSHARP_QUESTS_OFF with no Lua file: logged, it stays in C#",
            $"quests: {SampleKey} in CSHARP_QUESTS_OFF has no Lua file; it stays in C#/csharp:{SampleKey}",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(SampleKey))) + "/" +
            ContainerFile(SampleContainer));
        zone.TestQuestScripts.Clear();
    }

    // what a mission's completion leaves on the char, read from the database after the logout: the missions blob,
    // char_profile's rank columns, the char_vars and the char_mission_state rows (but their updated_at)
    private const string RankColumns = "rank_points, rank_sandoria, rank_bastok, rank_windurst";

    private static string C2Snapshot()
    {
        var vars = new List<string>();
        var r = Sql(db, "SELECT varname, value FROM char_vars WHERE charid = ? ORDER BY varname",
                    BoundValue.Of(CharId));
        while (r != null && r.Next()) vars.Add($"{r.Get<string>("varname")}={r.Get<string>("value")}");
        var rows = new List<string>();
        try
        {
            r = Sql(db, "SELECT area, current, status, completed_mask, progress FROM char_mission_state " +
                        "WHERE charid = ? ORDER BY area", BoundValue.Of(CharId));
            while (r != null && r.Next())
            {
                var cols = new List<string>();
                for (uint i = 0; i < r.ColumnCount(); i++) cols.Add(r.Get<string>(i) ?? "null");
                rows.Add(string.Join("|", cols));
            }
        }
        catch (Exception e)
        {
            rows.Add("unreadable: " + e.Message);
        }
        return $"missions={Convert.ToHexString(DbBlob("missions"))} | " +
               $"profile={DbRow(RankColumns, "FROM char_profile WHERE charid = ?")} | " +
               $"vars={string.Join(",", vars)} | rows={string.Join(",", rows)}";
    }

    // the C# mission's Complete() against Mission:complete on two fresh fixture chars at Naji, on the RoV fixture
    // (status 1, the reward's 400 rank points) and the CoP fixture (status 3, kept; the reward's next mission), each
    // mission's Option var set before
    private static void RunC2Complete()
    {
        NewWorld();
        zone.TestQuestScripts.Add(new SampleMission());
        zone.TestQuestScripts.Add(new SampleCopMission());
        Lua("InteractionGlobal.reload(true)");
        Check($"c2 mission: the CoP sample replaces {CopSampleContainer}", $"csharp:{CopSampleKey}",
            ContainerFile(CopSampleContainer));

        foreach (var (label, ml, id, status, reward, csid) in new[]
                 {
                     ("rov", 13, QC.Mission.Rov.TheEndlessSky, 1, "{ rankPoints = 400 }", 1001),
                     ("cop", 6, QC.Mission.Cop.DescendantsOfALineLost, 3,
                      $"{{ nextMission = {{ 6, {QC.Mission.Cop.TheMothercrystals} }} }}", 1002),
                 })
        {
            // the log's fresh current mission (CoP: Ancient Flames Beckon, RoV: Resonance) dropped first: addMission
            // logs an overwrite. A RoV char also gets a current Bastok mission (Geological Survey): with none,
            // Metalworks' 1000 finish at Naji would begin The Zeruhn Report (its state for a Bastokan with no Bastok
            // mission), and that would change the Bastok log on the C# side only
            string Setup(int s) =>
                (ml == 6 ? $"player:delMission(6, {QC.Mission.Cop.AncientFlamesBeckon}) " :
                 $"player:delMission(13, {QC.Mission.Rov.Resonance}) " +
                 $"player:addMission(1, {QC.Mission.Bastok.GeologicalSurvey}) ") +
                $"player:addMission({ml}, {id}) player:setMissionStatus({ml}, {s}) " +
                $"player:setCharVar('Mission[{ml}][{id}]Option', 2)";

            uint luaChar = CharIds[0], csChar = CharIds[1];
            Cleanup(db, luaChar);
            Fixture(luaChar, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
            Login();
            Lua(Setup(status));
            string done =
                Lua($"local m = Mission:new({ml}, {id}) m.reward = {reward} report(tostring(m:complete(player)))");
            Logout();
            string luaSide = C2Snapshot();

            Cleanup(db, csChar);
            Fixture(csChar, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
            Login();
            uint naji = NpcId("Naji");
            if (label == "rov")
            {
                // status 0: the first state plays 1000, its finish sets status 1 (q.Mission.SetStatus)
                Lua(Setup(0));
                TalkTo("Naji");
                Check("c2 mission in play: at status 0 Naji plays 1000", $"{naji}:1000", EvId());
                Finish();
                Check("  its finish sets status 1", "1", Lua($"report(tostring(player:getMissionStatus({ml})))"));
            }
            else Lua(Setup(status));
            TalkTo("Naji");
            Check($"c2 mission in play ({label}): at status {status} Naji plays {csid}", $"{naji}:{csid}", EvId());
            Finish();
            Logout();
            string csSide = C2Snapshot();
            Check($"c2 mission ({label}): Mission:complete answered true", "true", done);
            Check($"c2 mission ({label}): Complete() leaves the char as Mission:complete does", luaSide, csSide);
            Check($"  ({label}) the status after it (CoP keeps it)", ml == 6 ? $"{status}" : "0", StatusOf(ml));
            Check($"  ({label}) the mission's vars cleared, the rank points", $"False {(ml == 13 ? 400 : 0)}",
                $"{csSide.Contains($"Mission[{ml}][{id}]")} " +
                DbRow("rank_points", "FROM char_profile WHERE charid = ?"));
            Cleanup(db, luaChar);
            Cleanup(db, csChar);
        }
    }

    // the log's whole status as the blob holds it (statusUpper << 16 | statusLower) for the char CharId
    private static string StatusOf(int log)
    {
        var blob = DbBlob("missions");
        int b = log * 70;
        if (blob.Length < b + 6) return "-";
        uint upper = (uint)(blob[b + 2] | blob[b + 3] << 8), lower = (uint)(blob[b + 4] | blob[b + 5] << 8);
        return (upper << 16 | lower).ToString();
    }

    // ---- quest rework C2 Task 4: the mission calls in place --------------------------------------------------------

    // the test-only NPC script whose mission branches are the calls (xi.cs.mission, xi.cs.missionEvent)
    private const string C2CallFixture = "tests/XI.Verify.QuestScripts.Live/Fixtures/C2_Mission_Call.lua";

    // the RoV fixture mission as a call-only mission at Gerbaum (Bastok Mines): status 0 plays 1000, its finish
    // sets status 1; status 1 plays 1001, and the zone-in of status 1 is 1002. Its finish of 32001 in Riverne - Site
    // #B01 is for the scan only (a battlefield folder named with '_' for the zone name's '-')
    private sealed class CallMission : MissionScript
    {
        public CallMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b) =>
            C2CallMissionBody.Build(b, last => last.OnEventFinish(QC.Zone.RiverneSiteB01, 32001, q => { }));
    }

    // xi.cs.mission, missionEvent and missionZoneIn on a call-only test mission through the fixture NPC script; the
    // unknown key's line; the quest and mission calls keep to their own kind; the call-site scan of zones/ and
    // battlefields/, full and narrow
    private static void RunC2Calls()
    {
        string root = Path.GetDirectoryName(dataDir_);
        NewWorld();
        zone.TestQuestScripts.Add(new CallMission());
        int mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c2 mission call: xi.cs.mission, missionEvent and missionZoneIn are functions",
            "function/function/function",
            Lua("report(type(xi.cs.mission) .. '/' .. type(xi.cs.missionEvent) .. '/' .. " +
                "type(xi.cs.missionZoneIn))"));
        Check($"c2 mission call: a call-only mission registers as a call; {SampleContainer} stays the Lua file's",
            $"True||{SampleLuaFile}",
            $"{zone.IsNpcCallQuest(SampleKey)}|" +
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains(SampleKey))) +
            $"|{ContainerFile(SampleContainer)}");

        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, QC.Zone.BastokMines, 43.882f, 0.750f, -108.629f, 0);
        Login();
        uint gerbaum = NpcId(QC.Npc.BastokMines.Gerbaum);
        string fixture = Path.Combine(root, C2CallFixture).Replace('\\', '/');
        string Trigger()
        {
            Collect();
            return Lua($"local e = dofile('{fixture}') report(tostring(e.onTrigger(player, GetNPCByID({gerbaum}))))");
        }
        string EventFinish(int csid) =>
            Lua($"local e = dofile('{fixture}') " +
                $"report(tostring(e.onEventFinish(player, {csid}, 0, GetNPCByID({gerbaum}))))");
        string Status() => Lua("report(tostring(player:getMissionStatus(13)))");
        string ZoneIn() =>
            Lua($"report(tostring(xi.cs.missionZoneIn(player, '{SampleKey}', {QC.Zone.BastokMarkets})))");
        Lua($"player:delMission(13, {QC.Mission.Rov.Resonance}) " +
            $"player:addMission(13, {QC.Mission.Rov.TheEndlessSky}) player:setMissionStatus(13, 0)");

        Check("c2 mission call: current at status 0: xi.cs.mission acted (true)", "true", Trigger());
        Check("  Gerbaum plays 1000 through player:startEvent", $"{id}:1000", EvId());
        Finish();
        Check("  no zone-in at status 0", "nil", ZoneIn());
        Check("  xi.cs.missionEvent 1000 ran the finish: true, status 1", "true/1", EventFinish(1000) + "/" + Status());
        Check("  xi.cs.missionEvent 1001 has no handler: false", "false", EventFinish(1001));
        Check("c2 mission call: status 1: true, 1001", $"true/{id}:1001", Trigger() + "/" + EvId());
        Finish();
        Check("  xi.cs.missionZoneIn at status 1: 1002", "1002", ZoneIn());

        // another current mission: nothing matches, the script's own branch plays
        Lua($"player:delMission(13, {QC.Mission.Rov.TheEndlessSky})");
        Check("c2 mission call: not current: false, the script's fallback 22", $"false/{id}:22",
            Trigger() + "/" + EvId());
        Finish();
        Check("  missionZoneIn not current: nil", "nil", ZoneIn());

        // an unknown key: false, the line at most once a minute
        string Unknown() =>
            Lua($"report(tostring(xi.cs.mission(player, GetNPCByID({gerbaum}), 'mission.bastok.nope', 'onTrigger')))");
        mark = log.Count;
        string first = Unknown(), second = Unknown();
        Check("c2 mission call: an unknown key: false twice", "false/false", first + "/" + second);
        Check("  the line is logged once", "quests: xi.cs.mission: unknown key mission.bastok.nope",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains("unknown key"))));
        Check("  missionEvent and missionZoneIn of it: false, nil", "false/nil",
            Lua("report(tostring(xi.cs.missionEvent(player, nil, 'mission.bastok.nope', 1000, 0)))") + "/" +
            Lua("report(tostring(xi.cs.missionZoneIn(player, 'mission.bastok.nope', 0)))"));
        Check("  and still once", "1",
            log.Skip(mark).Count(l => l.Contains("unknown key mission.bastok.nope")).ToString());
        now += 61000;
        mark = log.Count;
        Unknown();
        Check("  and again after 60 s of NowMs", "1",
            log.Skip(mark).Count(l => l.Contains("unknown key mission.bastok.nope")).ToString());

        // each call keeps to its kind: a quest key is no mission's, the mission key no quest's
        mark = log.Count;
        Check("c2 mission call: xi.cs.mission with a quest key, xi.cs.quest with the mission key: false, false",
            "false/false",
            Lua($"report(tostring(xi.cs.mission(player, GetNPCByID({gerbaum}), 'sandoria.trial_by_ice', " +
                "'onTrigger')))") + "/" +
            Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({gerbaum}), '{SampleKey}', 'onTrigger')))"));
        Check("  each logged as its call's unknown key",
            $"quests: xi.cs.mission: unknown key sandoria.trial_by_ice | quests: xi.cs.quest: unknown key {SampleKey}",
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains("unknown key"))));
        Logout();
        Cleanup(db, id);

        // the call sites in a scripts tree: zones/ and battlefields/
        string scan = Path.Combine(Path.GetTempPath(), "c2_calls_" + Guid.NewGuid().ToString("N"));
        try
        {
            string npcs = Path.Combine(scan, "zones", "Bastok_Mines", "npcs");
            string mines = Path.Combine(scan, "battlefields", "Bastok_Mines");
            string balgas = Path.Combine(scan, "battlefields", "Balgas_Dais");
            Directory.CreateDirectory(npcs);
            Directory.CreateDirectory(mines);
            Directory.CreateDirectory(balgas);
            // the folder of Riverne - Site #B01 (zone_settings: Riverne-Site_B01) is named with '_'
            string riverne = Path.Combine(scan, "battlefields", "Riverne_Site_B01");
            Directory.CreateDirectory(riverne);
            string riverneLua = Path.Combine(riverne, "storms_of_fate.lua");
            File.WriteAllText(riverneLua, $"xi.cs.missionEvent(player, npc, '{SampleKey}', csid, option)\n");
            string gerbaumLua = Path.Combine(npcs, "Gerbaum.lua"), probeLua = Path.Combine(mines, "c2_probe.lua");
            string balgasLua = Path.Combine(balgas, "rank_2_mission.lua");
            File.WriteAllText(gerbaumLua,
                $"if xi.cs.mission(player, npc, '{SampleKey}', 'onTrigger') then return end\n");
            File.WriteAllText(Path.Combine(scan, "zones", "Bastok_Mines", "Zone.lua"),
                $"return xi.cs.missionZoneIn(player, '{SampleKey}', prevZone)\n");
            File.WriteAllText(probeLua, $"xi.cs.missionEvent(player, npc, '{SampleKey}', csid, option)\n");
            string Scan(bool narrow) => string.Join(" | ", OwnCalls(zone.ValidateNpcCalls(scan, narrow)));
            Check("c2 mission call scan: the calls in place (an NPC, Zone.lua, battlefields of the zones): clean",
                "", Scan(false));
            File.WriteAllText(balgasLua,
                $"xi.cs.missionEvent(player, npc, '{SampleKey}', csid, option)\n" +
                "xi.cs.mission(player, npc, 'mission.bastok.nope', 'onTrigger')\n");
            Check("c2 mission call scan: a battlefield's mission calls with no Called() handler there are reported",
                $"battlefields/Balgas_Dais/rank_2_mission.lua: {SampleKey} has no handlers for rank_2_mission | " +
                "battlefields/Balgas_Dais/rank_2_mission.lua: unknown key mission.bastok.nope", Scan(false));
            File.WriteAllText(balgasLua,
                $"xi.cs.missionZoneIn(player, '{SampleKey}', prevZone)\n" +
                $"xi.cs.quest(player, npc, '{SampleKey}', 'onTrigger')\n");
            Check("c2 mission call scan: missionZoneIn outside Zone.lua, and the mission key in xi.cs.quest",
                "battlefields/Balgas_Dais/rank_2_mission.lua: xi.cs.missionZoneIn outside Zone.lua " +
                "(its onZoneIn is the only zone-in) | " +
                $"battlefields/Balgas_Dais/rank_2_mission.lua: unknown key {SampleKey}", Scan(false));
            // the narrow scan (the startup's) reads the called NPC's script and the battlefields of the mission's zone
            File.WriteAllText(gerbaumLua, "xi.cs.mission(player, npc, 'mission.bastok.nope', 'onTrigger')\n");
            File.WriteAllText(probeLua, "xi.cs.missionEvent(player, npc, 'mission.bastok.nope', csid, option)\n");
            File.WriteAllText(riverneLua, "xi.cs.missionEvent(player, npc, 'mission.bastok.nope', csid, option)\n");
            Check("c2 mission call scan: narrow: the called NPC's script and its zones' battlefields, not Balgas",
                "zones/Bastok_Mines/npcs/Gerbaum.lua: unknown key mission.bastok.nope | " +
                "battlefields/Bastok_Mines/c2_probe.lua: unknown key mission.bastok.nope | " +
                "battlefields/Riverne_Site_B01/storms_of_fate.lua: unknown key mission.bastok.nope | " +
                $"{SampleKey}: Bastok_Mines/Gerbaum has handlers but its script never calls the mission", Scan(true));
        }
        finally
        {
            Directory.Delete(scan, true);
        }
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }
}
