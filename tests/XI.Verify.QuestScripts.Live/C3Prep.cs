using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Persistence;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

// quest rework C3 Prep (docs/superpowers/specs/2026-11-06-quest-rework-c3-lanes-design.md, the Prep section): the
// platform carries on the live bridge, on the fixture keys of C2.cs
internal static partial class Program
{
    // the sample key at Naji: at status 1 plays 1001, its finish completes with the reward
    private sealed class RewardMission : MissionScript
    {
        private readonly MissionReward reward_;

        public RewardMission(MissionReward reward) : base(MissionLog.RoV, "THE_ENDLESS_SKY") => reward_ = reward;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(reward_);
            b.State(current: true, status: 1).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001))
                .OnEventFinish(QC.Zone.Metalworks, 1001, q => q.Mission.Complete());
        }
    }

    private static void RunC3Prep()
    {
        Console.WriteLine("quest rework C3 Prep");
        RunC3Rewards();
        RunC3Callbacks();
        RunC3Texts();
        RunC3Groups();
        RunC3Proxy();
        RunC3MissionPatches();
        RunC3Reloads();
        RunC3Registration();
        RunC3Calls();
        RunC3Members();
        RunC3AreaPatches();
        RunC3Loot();
        RunC3SetMobSpawn();
        zone.TestQuestScripts.Clear();
        NewWorld();
    }

    // the sample constant, or none Lua knows
    private sealed class ConstMission : MissionScript
    {
        private readonly string legacyPath_;

        public ConstMission(string legacyPath, string constName = "THE_ENDLESS_SKY")
            : base(MissionLog.RoV, constName) => legacyPath_ = legacyPath;

        public override string LegacyPath => legacyPath_;

        protected override void Build(MissionScriptBuilder b) =>
            b.State(current: true).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001));
    }

    // Compile throws as a reflection call would: the message the line gives is the inner one
    private sealed class ThrowingMission : MissionScript
    {
        public ThrowingMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        public override string LegacyPath => SampleLuaFile;

        protected override void Build(MissionScriptBuilder b) =>
            throw new System.Reflection.TargetInvocationException(new InvalidOperationException("built wrong"));
    }

    private static void RunC3Registration()
    {
        const string NoKey = "mission.rov.no_such_mission";
        // an unknown constant with no Lua file: the no-fallback line, the key counted
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new ConstMission(SampleLuaFile, "NO_SUCH_MISSION"));
        int mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 registration: an unknown mission constant with no Lua file: the no-fallback line, counted",
              $"quests: C# mission {NoKey} failed to load and has no Lua fallback: unknown mission constant|{NoKey}",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(NoKey))) + "|" +
              string.Join(",", zone.QuestLoadFailures));

        // with the Lua file at its LegacyPath: not registered, nothing counted
        NewWorld();
        zone.TestQuestScripts.Add(new ConstMission(SampleLuaFile, "NO_SUCH_MISSION"));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 registration: an unknown mission constant with the Lua file there: not registered, nothing counted",
              $"quests: {NoKey} not registered: unknown mission constant|",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(NoKey))) + "|" +
              string.Join(",", zone.QuestLoadFailures));

        // two classes of a key and no Lua file: the first registered, the second's line, the key counted
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new ConstMission(SampleLuaFile));
        zone.TestQuestScripts.Add(new ConstMission(SampleLuaFile));
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        string duplicate = $"quests: C# mission {SampleKey} failed to load and has no Lua fallback: " +
                           "another C# mission (";
        Check("c3 registration: a second class of a mission key and no Lua file: the line, counted, the first in C#",
              $"True|{SampleKey}|csharp:{SampleKey}",
              $"{log.Skip(mark).Any(l => l.StartsWith(duplicate, StringComparison.Ordinal))}|" +
              $"{string.Join(",", zone.QuestLoadFailures)}|{ContainerFile(SampleContainer)}");

        // with the Lua file there: not registered, nothing counted
        NewWorld();
        zone.TestQuestScripts.Add(new ConstMission(SampleLuaFile));
        zone.TestQuestScripts.Add(new ConstMission(SampleLuaFile));
        mark = log.Count;
        string notRegistered = $"quests: {SampleKey} not registered: another C# mission (";
        Lua("InteractionGlobal.reload(true)");
        Check("c3 registration: a second class with the Lua file there: not registered, nothing counted",
              "True|", $"{log.Skip(mark).Any(l => l.StartsWith(notRegistered, StringComparison.Ordinal))}|" +
              string.Join(",", zone.QuestLoadFailures));
        zone.TestQuestScripts.Clear();

        // a test mission's Compile throwing goes through InnerMessage
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new ThrowingMission());
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 registration: a mission whose Compile throws: the inner message",
              $"quests: C# mission {SampleKey} failed to load and has no Lua fallback: built wrong",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("no Lua fallback"))));
        zone.TestQuestScripts.Clear();

        // the count line
        NewWorld();
        int missions = typeof(XI.QuestContent.Zone).Assembly.GetTypes()
            .Count(t => t.IsClass && !t.IsAbstract && typeof(MissionScript).IsAssignableFrom(t));
        int battlefields = typeof(XI.QuestContent.Zone).Assembly.GetTypes()
            .Count(t => t.IsClass && !t.IsAbstract && typeof(BattlefieldScript).IsAssignableFrom(t));
        Check("c3 registration: the count line covers the three kinds",
              $"{RegisteredCount}/{missions}/{battlefields}", RegisteredKinds(log.Last(IsRegistered)));
        zone.TestQuestScripts.Add(new SampleMission());
        zone.TestQuestScripts.Add(new SampleBattlefield());
        Lua("InteractionGlobal.reload(true)");
        Check("  and counts a test mission and a test battlefield",
              $"{RegisteredCount}/{missions + 1}/{battlefields + 1}", RegisteredKinds(log.Last(IsRegistered)));
        zone.TestQuestScripts.Clear();
    }

    // what the rewards leave beyond C2Snapshot: gil, key items, the title, bayld and the rank (read in play); the item
    // count and the char_exp row are read from the database after the logout (C3DbRead)
    private static string C3RewardRead() =>
        Lua("report(table.concat({ player:getGil(), " +
            "tostring(player:hasKeyItem(" + QC.KeyItem.SkyBluePome + ")), " +
            "tostring(player:hasKeyItem(" + QC.KeyItem.SunYellowPome + ")), player:getTitle(), " +
            "player:getCurrency('bayld'), player:getRank(player:getNation()) }, '|'))");

    private static string C3DbRead() => $"{Qty(QC.Item.ImperialGoldPiece)} | {C3ExpRow()}";

    // the char_exp row without its charid column (the two sides are two chars)
    private static string C3ExpRow()
    {
        var r = Sql(db, "SELECT * FROM char_exp WHERE charid = ?", BoundValue.Of(CharId));
        var cols = new List<string>();
        while (r != null && r.Next())
            for (uint i = 1; i < r.ColumnCount(); i++) cols.Add(r.Get<string>(i) ?? "null");
        return string.Join("|", cols);
    }

    // the reward field of the container with the id, as tostring(reward<path>) in the lookup's entries
    private static string RewardField(string containerId, string path) => Lua($@"
        local value = 'no container'
        for _, z in pairs(InteractionGlobal.lookup.data) do
            for _, second in pairs(z) do
                for _, list in pairs(type(second) == 'table' and second or {{}}) do
                    for _, e in ipairs(type(list) == 'table' and list or {{}}) do
                        if type(e) == 'table' and e.container and e.container.id == '{containerId}' then
                            value = tostring(e.container.reward{path})
                        end
                    end
                end
            end
        end
        report(value)");

    // Mission:complete with the Lua table against the C# Complete() with the reward, on two fresh chars at Naji
    private static void CompareReward(string label, MissionReward reward, string luaReward)
    {
        NewWorld();
        zone.TestQuestScripts.Add(new RewardMission(reward));
        Lua("InteractionGlobal.reload(true)");
        // Metalworks' IDs have no BAYLD_OBTAINED: both sides read this one, or npcUtil's message raises
        Lua("zones[xi.zone.METALWORKS].text.BAYLD_OBTAINED = 6451");
        int ml = (int)MissionLog.RoV, id = QC.Mission.Rov.TheEndlessSky;
        // as C2's rov Setup: the fresh RoV mission dropped, and a current Bastok mission so no csid of Metalworks
        // begins The Zeruhn Report on one side only
        string setup = $"player:delMission(13, {QC.Mission.Rov.Resonance}) " +
                       $"player:addMission(1, {QC.Mission.Bastok.GeologicalSurvey}) " +
                       $"player:addMission({ml}, {id}) player:setMissionStatus({ml}, 1)";

        uint luaChar = CharIds[0], csChar = CharIds[1];
        Cleanup(db, luaChar);
        Fixture(luaChar, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        Lua(setup);
        string done = Lua($"local m = Mission:new({ml}, {id}) m.reward = {luaReward} " +
                          "report(tostring(m:complete(player)))");
        string luaRead = C3RewardRead();
        Logout();
        string luaSide = C2Snapshot() + " | " + C3DbRead();

        Cleanup(db, csChar);
        Fixture(csChar, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        Lua(setup);
        TalkTo("Naji");
        Finish();
        string csRead = C3RewardRead();
        Logout();
        string csSide = C2Snapshot() + " | " + C3DbRead();

        Check($"c3 reward ({label}): Mission:complete answered true", "true", done);
        Check($"c3 reward ({label}): the char as the Lua table leaves it", luaRead, csRead);
        Check($"  ({label}) the blob, the rows and the exp as the Lua table leaves them", luaSide, csSide);
        Cleanup(db, luaChar);
        Cleanup(db, csChar);
    }

    private static void RunC3Rewards()
    {
        int gold = QC.Item.ImperialGoldPiece, sky = QC.KeyItem.SkyBluePome, sun = QC.KeyItem.SunYellowPome;
        CompareReward("a table item and a table keyItem",
            new MissionReward
            {
                Items = new[] { new MissionRewardItem(gold, 5) }, KeyItems = new[] { sky, sun },
            },
            $"{{ item = {{ {{ {gold}, 5 }} }}, keyItem = {{ {sky}, {sun} }} }}");
        CompareReward("every number key",
            new MissionReward
            {
                Item = gold, KeyItem = sky, Gil = 500, Bayld = 100, Exp = 1000, Title = 1, RankPoints = 300, Rank = 2,
            },
            $"{{ item = {gold}, keyItem = {sky}, gil = 500, bayld = 100, exp = 1000, title = 1, rankPoints = 300, " +
            "rank = 2 }");
        CompareReward("itemParams silent",
            new MissionReward { Item = gold, ItemParams = new QuestItemParams { Silent = true } },
            $"{{ item = {gold}, itemParams = {{ silent = true }} }}");

        NewWorld();
        zone.TestQuestScripts.Add(new RewardMission(new MissionReward
        {
            Gil = 0, Items = new MissionRewardItem[] { gold, new(gold, 2) },
            ItemParams = new QuestItemParams { FromTrade = true },
        }));
        Lua("InteractionGlobal.reload(true)");
        Check("c3 reward: the table's keys: 0 is a key, null is not; a plain item and an { id, quantity } in one list",
              $"0|nil|{gold}|2|true", string.Join("|", new[]
              {
                  RewardField(SampleContainer, ".gil"), RewardField(SampleContainer, ".title"),
                  RewardField(SampleContainer, ".item[1]"), RewardField(SampleContainer, ".item[2][2]"),
                  RewardField(SampleContainer, ".itemParams.fromTrade"),
              }));
    }

    private static readonly List<string> c3Ran_ = new();

    // the sample key with its file's LegacyPath: Naji at status 0 arms a player timer of 1000 ms; the callback reads
    // the status through q.Mission and sets it to 7; xi.c3prep.wake runs the same callback. broken: Naji in a zone she
    // is not in, so the registration refuses it
    private sealed class CallbackMission : MissionScript
    {
        private readonly bool broken_;

        public CallbackMission(bool broken = false) : base(MissionLog.RoV, "THE_ENDLESS_SKY") => broken_ = broken;

        public override string LegacyPath => SampleLuaFile;

        protected override void Build(MissionScriptBuilder b)
        {
            var wake = b.Callback("wake", q =>
            {
                c3Ran_.Add($"wake:{q.Mission.Status}");
                q.Mission.SetStatus(7);
            });
            b.Export("xi.c3prep.wake", wake);
            ushort zone = broken_ ? QC.Zone.BastokMines : QC.Zone.Metalworks;
            b.State(current: true, status: 0).Npc(zone, "Naji").OnTrigger(q =>
            {
                q.Player.Timer(1000, wake);
                return QuestReply.Event(1000);
            });
        }
    }

    private static void RunC3Callbacks()
    {
        int ml = (int)MissionLog.RoV, mission = QC.Mission.Rov.TheEndlessSky;
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new CallbackMission());
        Lua("InteractionGlobal.reload(true)");
        Check("c3 callback: the labels hold the callback", "True",
              zone.HandlerLabels(SampleKey).Contains("0/-/callback:wake").ToString());
        Check("c3 callback: the export is a function, the deleted file's module claimed", "function/true",
              Lua($"report(type(xi.c3prep.wake) .. '/' .. tostring(package.loaded['{SampleLuaFile}'] ~= nil))"));
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        // the fresh character's RoV mission dropped, as C2's rov Setup does
        Lua($"player:delMission({ml}, {QC.Mission.Rov.Resonance}) " +
            $"player:addMission({ml}, {mission}) player:setMissionStatus({ml}, 0)");
        c3Ran_.Clear();
        TalkTo("Naji");
        Finish();
        Tick(1000);
        string atDeadline = string.Join(",", c3Ran_);
        Tick(1);
        Check("c3 callback: a mission's timer runs past its deadline, with q.Mission", "|wake:0|7",
              $"{atDeadline}|{string.Join(",", c3Ran_)}|{Lua($"report(tostring(player:getMissionStatus({ml})))")}");
        c3Ran_.Clear();
        Lua("xi.c3prep.wake(player)");
        Check("c3 callback: the export runs it with the player", "wake:7", string.Join(",", c3Ran_));
        Logout();
        Cleanup(db, id);

        // a broken mission's export is a no-op that logs once
        NewWorld(new[] { SampleLuaFile }, false);
        zone.TestQuestScripts.Add(new CallbackMission(broken: true));
        Lua("InteractionGlobal.reload(true)");
        int mark = log.Count;
        Lua("xi.c3prep.wake(nil) xi.c3prep.wake(nil)");
        Check("c3 callback: refused: the global is a no-op, logged once",
              $"quests: {SampleKey} refused: xi.c3prep.wake not run",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains("xi.c3prep.wake"))));
        zone.TestQuestScripts.Clear();
    }

    // Eye of the Storm's constant with no Lua file needed: test data only
    private static BattlefieldData EyeData() => new BattlefieldData
    {
        ZoneId = QC.Zone.ChamberOfOracles, BattlefieldId = QC.Battlefield.EyeOfTheStorm, MaxPlayers = 6,
        TimeLimit = 1800, Index = 6, EntryNpcs = new[] { "SC_Entrance" }, ExitNpcs = new[] { "Shimmering_Circle" },
    };

    // battlefieldEntry sends each message form, then reads the text named by probe
    private sealed class TextBattlefield : BattlefieldScript
    {
        private readonly ZoneText probe_;

        public TextBattlefield(ZoneText probe) : base("EYE_OF_THE_STORM") => probe_ = probe;

        public override BattlefieldData Data { get; } = EyeData();

        public override void BattlefieldEntry(IBattlefieldContext q)
        {
            var mob = q.Battlefield.Mobs(true, false)[0];
            int id = q.Text.Id(QC.Text.ChamberOfOracles.A_CRACK_HAS_FORMED);
            q.Player.MessageSpecial(id, 7);
            q.Player.ShowText(mob, id + 1);
            mob.MessageText(id + 2);
            q.Text.Id(probe_);
        }
    }

    private const string C3EntryCall = @"
        local said = {}
        local mob = { messageText = function(self, target, id)
            said[#said + 1] = 'mob ' .. tostring(target == self) .. ' ' .. id
        end }
        local player = {
            messageSpecial = function(self, id, a) said[#said + 1] = 'special ' .. id .. ' ' .. a end,
            showText = function(self, target, id) said[#said + 1] = 'show ' .. tostring(target == mob) .. ' ' .. id end,
        }
        local battlefield = { getMobs = function(self, required, additional) return { mob } end }
        local c = xi.battlefield.contents[198]
        c:battlefieldEntry(player, battlefield)
        report(table.concat(said, '|'))";

    private static string c3SetupSeen_ = "-";

    private sealed class GroupBattlefield : BattlefieldScript
    {
        public GroupBattlefield() : base("EYE_OF_THE_STORM")
        {
            var d = EyeData();
            d.EssentialMobs.Add(new BattlefieldEssentialMobs { Mobs = new[] { "Queen_Jelly" }, Spawned = false });
            d.Groups.Add(new BattlefieldGroup
            {
                Mobs = new[] { "Add" }, SuperlinkGroup = 2,
                Setup = (q, mobs) => c3SetupSeen_ = $"{q.Script.Key} {mobs.Count} {mobs[0].Id}",
            });
            Data = d;
        }

        public override BattlefieldData Data { get; }
    }

    private static void RunC3Groups()
    {
        NewWorld();
        zone.TestQuestScripts.Add(new GroupBattlefield());
        Lua("InteractionGlobal.reload(true)");
        Check("c3 group: the proxy's groups: content.groups then the essential one, superlinkGroup and spawned as set",
              "2 Add 2 nil Queen_Jelly false true",
              Lua("local g = xi.battlefield.contents[198].groups report(#g .. ' ' .. g[1].mobs[1] .. ' ' .. " +
                  "tostring(g[1].superlinkGroup) .. ' ' .. tostring(g[1].spawned) .. ' ' .. g[2].mobs[1] .. ' ' .. " +
                  "tostring(g[2].spawned) .. ' ' .. tostring(g[2].superlink))"));
        Lua("xi.battlefield.contents[198].groups[1].setup(nil, { { getID = function(self) return 77 end } })");
        Check("c3 group: setup(battlefield, mobs) runs with the mobs", $"{QC.BattlefieldKey.EyeOfTheStorm} 1 77",
              c3SetupSeen_);
        // a fight in progress keeps the closures of the pass it began in (the C++ holds the sol::function): the essential
        // group's allDeath taken before a reload runs after it, reaches its content (the new pass's) and ends the fight
        Lua("c3_stale_all_death = xi.battlefield.contents[198].groups[2].allDeath");
        Lua("InteractionGlobal.reload(true)");
        Check("c3 group: a stale allDeath after reload(true) reaches the content: the fight is WON",
              $"{BattlefieldConst.StatusWon}",
              Lua(@"local status = 'none'
                local bf = {
                    getArmouryCrate = function(self) return 0 end,
                    setLocalVar = function(self, name, v) end,
                    setStatus = function(self, s) status = s end,
                }
                c3_stale_all_death(bf, {})
                report(tostring(status))"));
        zone.TestQuestScripts.Clear();
    }

    private static readonly List<string> c3Events_ = new();

    // positions as pulling_the_plug.lua has them, an AreaMobIds group as the Ark Angels' files read it, and
    // one_to_be_feared.lua's section handlers
    private sealed class ProxyBattlefield : BattlefieldScript
    {
        public ProxyBattlefield(string field = "positions") : base("EYE_OF_THE_STORM")
        {
            var d = EyeData();
            d.ProxyFields[field] = new object[]
            {
                new object[]
                {
                    new object[] { new Dictionary<string, object> { ["x"] = -249.45, ["y"] = 60.75, ["z"] = -8.71 } },
                },
            };
            d.Groups.Add(new BattlefieldGroup
            {
                AreaMobIds = new[]
                {
                    new[] { new BattlefieldMobId("C3_MOB") },
                    new[] { new BattlefieldMobId("C3_MOB", 7) },
                },
            });
            d.OnEventUpdate[1] = (q, csid, option) => c3Events_.Add($"update {csid} {option} {q.Npc?.Id ?? 0}");
            d.OnEventFinish[10] = (q, csid, option) => c3Events_.Add($"finish {csid} {option}");
            Data = d;
        }

        public override BattlefieldData Data { get; }
    }

    private static void RunC3Proxy()
    {
        string key = QC.BattlefieldKey.EyeOfTheStorm;
        ushort coo = QC.Zone.ChamberOfOracles;
        NewWorld();
        // the group's mob key: test data, in this world's IDs table only
        Lua($"zones[{coo}].mob.C3_MOB = 1000");
        zone.TestQuestScripts.Add(new ProxyBattlefield());
        Lua("InteractionGlobal.reload(true)");
        Check("c3 proxy: content.positions[area][i][n] as Memory_Receptacle_Red.lua reads it", "-249.45 60.75 -8.71",
              Lua("local p = xi.battlefield.contents[198].positions[1][1][1] " +
                  "report(p.x .. ' ' .. p.y .. ' ' .. p.z)"));
        Check("c3 proxy: content.groups[i]['mobIds'][area][1] as the Ark Angels' files read it", "1000 1007",
              Lua("local m = xi.battlefield.contents[198].groups[1]['mobIds'] report(m[1][1] .. ' ' .. m[2][1])"));
        Check("c3 proxy: the section's handlers sit beside the zone's hooks in the lookup",
              $"csharp:{key}|csharp:{key}",
              $"{Hooks(coo, "onEventUpdate", "1", 198)}|{Hooks(coo, "onEventFinish", "10", 198)}");
        c3Events_.Clear();
        Lua($"local s = xi.battlefield.contents[198].sections[1][{coo}] " +
            "s.onEventUpdate[1](nil, 1, 4, { getID = function(self) return 9 end }) " +
            "s.onEventFinish[10](nil, 10, 2, nil)");
        Check("c3 proxy: each runs with (player, csid, option, npc)", "update 1 4 9,finish 10 2",
              string.Join(",", c3Events_));

        NewWorld();
        Lua($"zones[{coo}].mob.C3_MOB = 1000");
        zone.TestQuestScripts.Add(new ProxyBattlefield("getVar"));
        int mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 proxy: a framework's name is refused, the Lua file left in charge",
              $"quests: {key} not registered: proxy field getVar is a framework field",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(key))));
        Check("c3 proxy: the refused battlefield leaves the Lua file in charge", EyeLuaFile,
              Lua("report(tostring(xi.battlefield.contents[198].filename))"));
        zone.TestQuestScripts.Clear();
    }

    private static void RunC3Texts()
    {
        string eye = QC.BattlefieldKey.EyeOfTheStorm;
        NewWorld();
        zone.TestQuestScripts.Add(new TextBattlefield(QC.Text.ChamberOfOracles.ORB_IS_CRACKED));
        Lua("InteractionGlobal.reload(true)");
        int id = TextId(QC.Zone.ChamberOfOracles, "A_CRACK_HAS_FORMED");
        Check("c3 text: the hook's three forms reach their Lua calls, the text id from the zone's table",
              $"special {id} 7|show true {id + 1}|mob true {id + 2}", Lua(C3EntryCall));

        NewWorld();
        zone.TestQuestScripts.Add(new TextBattlefield(new ZoneText(QC.Zone.Metalworks, "ITEM_OBTAINED")));
        Lua("InteractionGlobal.reload(true)");
        int mark = log.Count;
        Lua(C3EntryCall);
        Check("c3 text: a text of another zone: the hook's line",
              $"quests: {eye} battlefieldEntry: text {QC.Zone.Metalworks}:ITEM_OBTAINED is not of the battlefield's " +
              $"zone {QC.Zone.ChamberOfOracles}",
              string.Join(" | ", log.Skip(mark).Where(l => l.Contains(eye))));
        zone.TestQuestScripts.Clear();
    }

    // the sample key at Naji: status 0 plays 1000, its finish sets status 1; status 1 plays 1001
    private sealed class PatchedMission : MissionScript
    {
        public PatchedMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true, status: 0).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1000))
                .OnEventFinish(QC.Zone.Metalworks, 1000, q => q.Mission.SetStatus(1));
            b.State(current: true, status: 1).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001));
        }
    }

    // a state each for the SoA and the WotG gate
    private sealed class GateMission : MissionScript
    {
        public GateMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true, when: q => q.Player.SoaImprimaturGate(10))
                .Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001));
            b.State(current: true, when: q => q.Player.WotgMeetsMissionReqs(3))
                .Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1002));
        }
    }

    // the first event Naji plays for the sample mission with its status at 0 or 1; the fixture char logged out after
    private static string NajiPlays(int status)
    {
        uint id = CharIds[0];
        int ml = (int)MissionLog.RoV, mission = QC.Mission.Rov.TheEndlessSky;
        Cleanup(db, id);
        Fixture(id, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        // the fresh character's RoV mission dropped first, as C2's rov Setup does: addMission logs an overwrite
        Lua($"player:delMission({ml}, {QC.Mission.Rov.Resonance}) " +
            $"player:addMission({ml}, {mission}) player:setMissionStatus({ml}, {status})");
        uint naji = NpcId("Naji");
        TalkTo("Naji");
        string played = EvId().Replace($"{naji}:", "");
        FinishIfEvent();
        Logout();
        Cleanup(db, id);
        return played;
    }

    // !reloadbattlefield with printToPlayer caught; the Lua files are on disk at their own paths, so nothing is mapped
    private static string ReloadBattlefield(string name) => Lua($@"
        local cmd = dofile('scripts/commands/reloadbattlefield.lua')
        local said = {{}}
        local player = {{ printToPlayer = function(self, msg) said[#said + 1] = msg end }}
        local ok, err = pcall(cmd.onTrigger, player, '{name}')
        if not ok then error(err) end
        report(table.concat(said, ' | '))");

    private static void RunC3Reloads()
    {
        ushort coo = QC.Zone.ChamberOfOracles;
        string cactuar = QC.BattlefieldKey.CactuarSuave;
        NewWorld();
        Check("c3 reload: !reloadbattlefield on a C# battlefield: refused, the GM told how",
              $"Battlefield 'cactuar_suave' runs in C# ({cactuar}); !reloadinteraction 1 " +
              "(InteractionGlobal.reload(true)) reloads it.", ReloadBattlefield("cactuar_suave"));
        Check("c3 reload: a Lua battlefield reloads as before",
              $"Battlefield 'eye_of_the_storm' at '{EyeLuaFile}' has been reloaded.",
              ReloadBattlefield("eye_of_the_storm"));
        Check("c3 reload: an unknown name as before",
              "Unable to find battlefield 'no_such' to reload. | !reloadbattlefield <lua-file-name>",
              ReloadBattlefield("no_such"));

        // the test Eye of the Storm over its Lua file, then switched off by a reload(true); no discovered battlefield,
        // so the Eye is the only C# one of its zone and only the last pass's zones bring that zone's hooks back
        NewWorld(null, true);
        Lua("InteractionGlobal.reload(true)");
        string luaZone = ZoneContents(coo);
        zone.TestQuestScripts.Add(new SampleBattlefield());
        Lua("InteractionGlobal.reload(true)");
        Check("c3 reload: the C# one in place", $"csharp:{EyeKey}",
              Lua("report(tostring(xi.battlefield.contents[198].filename))"));
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
        Check("c3 reload: switched off by reload(true): the Lua content is back, the zone's list as it was, no proxy",
              $"{EyeLuaFile}|{luaZone}|0",
              Lua("report(tostring(xi.battlefield.contents[198].filename))") + $"|{ZoneContents(coo)}|" +
              Lua($"local n = 0 for _, c in ipairs(xi.battlefield.contentsByZone[{coo}]) do " +
                  "if c.filename == 'csharp:" + EyeKey + "' then n = n + 1 end end report(tostring(n))"));
        Check("c3 reload:   its hooks are the Lua file's", EyeLuaFile,
              Hooks(coo, "Shimmering_Circle", "onTrigger", 198));

        // CSHARP_QUESTS off drops every proxy of the world: a battlefield with no Lua file has no content left
        zone.SkipDiscoveredQuests = false;
        Lua("InteractionGlobal.reload(true)");
        zone.CSharpQuestsEnabled = false;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 reload: CSHARP_QUESTS off: a gone Lua file's battlefield has no content, in no zone list", "nil|0",
              Lua($"report(tostring(xi.battlefield.contents[{QC.Battlefield.CactuarSuave}]))") + "|" +
              ZoneContents(coo).Split(',').Count(x => x == $"{QC.Battlefield.CactuarSuave}"));
        zone.CSharpQuestsEnabled = true;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 reload: back on: its proxy again", $"csharp:{cactuar}",
              Lua($"report(tostring(xi.battlefield.contents[{QC.Battlefield.CactuarSuave}].filename))"));
    }

    private static void RunC3MissionPatches()
    {
        string key = QC.MissionKey.Rov.TheEndlessSky;
        NewWorld();
        zone.TestQuestScripts.Add(new PatchedMission());
        zone.TestQuestModules.Add(new P4LiveModule("c3_live_mission", m =>
        {
            m.Mission(key).State(1).WrapCheck((q, b) => b(q) && q.Player.CharVar("[C3]Open") == 1);
            m.Mission(key).State(2).Npc(QC.Zone.Metalworks, "Naji").ReplaceOnTrigger(q => QuestReply.Event(1009));
        }));
        var lines = P4Reload("custom/lua/c3_live_mission.lua");
        Check("c3 patch: the module enabled, the mission patched",
              "modules: c3_live_mission enabled (modules/custom/lua/c3_live_mission.lua)", string.Join(" | ", lines));
        Check("c3 patch: the labels", $"{key} 1/-/check,{key} 2/Naji/onTrigger",
              string.Join(",", zone.ModuleLabels("modules/custom/lua/c3_live_mission.lua")));
        Check("c3 patch: the patched mission last in the list", SampleContainer,
              zone.ContainerOrder(lua).Last(id => id.StartsWith("Mission[", StringComparison.Ordinal)));
        Check("c3 patch: the replaced trigger plays, the state's status still its own", "1009", NajiPlays(1));
        Check("c3 patch: the wrapped check's guard fails: Naji's own default, not 1000", "False",
              (NajiPlays(0) == "1000").ToString());
        zone.TestQuestModules.Clear();

        // a module naming a mission no C# class has
        zone.TestQuestModules.Add(new P4LiveModule("c3_live_missing", m =>
            m.Mission("mission.rov.no_such_mission").State(1).ReplaceCheck(q => false)));
        lines = P4Reload("custom/lua/c3_live_missing.lua");
        Check("c3 patch: a module naming no C# mission: refused",
              "modules: c3_live_missing refused: mission.rov.no_such_mission is not a C# mission",
              string.Join(" | ", lines));
        zone.TestQuestModules.Clear();
        zone.TestQuestScripts.Clear();

        // the helper-overriding modules stay Lua: the C# check calls the global, so the override runs
        NewWorld();
        zone.TestQuestScripts.Add(new GateMission());
        Lua("InteractionGlobal.reload(true)");
        Check("c3 patch: without the modules both gates are shut", "False",
              (NajiPlays(0) is "1001" or "1002").ToString());
        worldModuleLines_ = new[] { "custom/lua/soa_remove_imprimatur_gate.lua" };
        NewWorld();
        zone.TestQuestScripts.Add(new GateMission());
        zone.LoadLuaModules();
        Lua("InteractionGlobal.reload(true)");
        Check("c3 patch: soa_remove_imprimatur_gate.lua opens the SoA gate", "1001", NajiPlays(0));
        worldModuleLines_ = new[] { "custom/lua/wotg_decouple_story_missions_from_nation_quests.lua" };
        NewWorld();
        zone.TestQuestScripts.Add(new GateMission());
        zone.LoadLuaModules();
        Lua("InteractionGlobal.reload(true)");
        Check("c3 patch: wotg_decouple_story_missions_from_nation_quests.lua opens the WotG gate", "1002",
              NajiPlays(0));
        worldModuleLines_ = CommittedModuleLines;
        zone.TestQuestScripts.Clear();

        // npcUtil.completeMission overridden, as mission_wardrobe_unlocks.lua does: q.Mission.Complete() goes
        // through it
        NewWorld();
        zone.TestQuestScripts.Add(new RewardMission(new MissionReward { RankPoints = 10 }));
        Lua("InteractionGlobal.reload(true)");
        Lua("local old = npcUtil.completeMission " +
            "npcUtil.completeMission = function(player, logId, missionId, params) " +
            "C3Completed = logId .. '/' .. missionId return old(player, logId, missionId, params) end");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        Lua($"player:delMission({(int)MissionLog.RoV}, {QC.Mission.Rov.Resonance}) " +
            $"player:addMission({(int)MissionLog.RoV}, {QC.Mission.Rov.TheEndlessSky}) " +
            $"player:setMissionStatus({(int)MissionLog.RoV}, 1)");
        TalkTo("Naji");
        Finish();
        Check("c3 patch: Complete() reaches an override of npcUtil.completeMission",
              $"{(int)MissionLog.RoV}/{QC.Mission.Rov.TheEndlessSky}", Lua("report(tostring(C3Completed))"));
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
    }

    private static string c3Seen_ = "-";

    // the sample key as a call-only mission at Gerbaum: a trade of one Copper Ore, a mob's death and an event update,
    // each through its call; an optParams field in the death
    private sealed class CallKindsMission : MissionScript
    {
        public CallKindsMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true).Npc(QC.Zone.BastokMines, QC.Npc.BastokMines.Gerbaum).Called()
                .OnTrade(q => q.Trade.HasExactly(640) ? QuestReply.Event(1003) : null)
                .OnEventUpdate(QC.Zone.BastokMines, 1003, q => c3Seen_ = $"update {q.Option} {q.Mission.Status}");
            b.State(current: true).Npc(QC.Zone.AltarRoom, QC.Npc.AltarRoom.YagudoAvatar).Called()
                .OnMobDeath(q => c3Seen_ = $"death {q.MobDeath.IsKiller} {q.Mission.Status}");
        }
    }

    private static void RunC3Calls()
    {
        int ml = (int)MissionLog.RoV, mission = QC.Mission.Rov.TheEndlessSky;

        // the switches: a call-only mission has no Lua container to fall back to
        const string Line = "quests: call-only quests not registered (CSHARP_QUESTS / CSHARP_QUESTS_OFF): ";
        int mark = 0;
        string CallOnlyLine() =>
            string.Join(" | ", log.Skip(mark).Where(l => l.Contains("call-only quests not registered")));
        NewWorld(SampleKey);
        zone.TestQuestScripts.Add(new CallMission());
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        Check("c3 call: CSHARP_QUESTS_OFF naming a call-only mission: named in the line, no call registered",
              $"{Line}{SampleKey}|False", $"{CallOnlyLine()}|{zone.IsNpcCallQuest(SampleKey)}");
        NewWorld();
        zone.TestQuestScripts.Add(new CallMission());
        zone.CSharpQuestsEnabled = false;
        mark = log.Count;
        Lua("InteractionGlobal.reload(true)");
        // the global switch lists every call-only key, not only the one named
        Check("c3 call: CSHARP_QUESTS off: the line lists the key too, no call registered", "True|False",
              $"{CallOnlyLine().StartsWith(Line) && CallOnlyLine().Contains($" {SampleKey},")}|" +
              $"{zone.IsNpcCallQuest(SampleKey)}");
        zone.CSharpQuestsEnabled = true;
        zone.TestQuestScripts.Clear();

        NewWorld();
        zone.TestQuestScripts.Add(new CallKindsMission());
        Lua("InteractionGlobal.reload(true)");
        uint id = CharIds[0];
        Cleanup(db, id);
        Fixture(id, QC.Zone.BastokMines, 43.882f, 0.750f, -108.629f, 0);
        Login();
        Lua($"player:delMission({ml}, {QC.Mission.Rov.Resonance}) " +
            $"player:addMission({ml}, {mission}) player:setMissionStatus({ml}, 4)");
        uint gerbaum = NpcId(QC.Npc.BastokMines.Gerbaum);
        string Trade(int qty) =>
            Lua("local t = { getSlotCount = function() return 1 end, getItemId = function() return 640 end, " +
                $"getItemQty = function() return {qty} end, confirmItem = function() end }}; " +
                $"report(tostring(xi.cs.mission(player, GetNPCByID({gerbaum}), '{SampleKey}', 'onTrade', t)))");
        Collect();
        Check("c3 call: xi.cs.mission 'onTrade': the right trade acted and plays 1003", $"true/{id}:1003",
              Trade(1) + "/" + EvId());
        Check("c3 call: 'update' reaches the Called() OnEventUpdate with q.Mission", "true|update 5 4",
              Lua($"report(tostring(xi.cs.missionEvent(player, GetNPCByID({gerbaum}), '{SampleKey}', 1003, 5, " +
                  "'update')))") + $"|{c3Seen_}");
        Finish();
        Check("c3 call: the wrong trade: false", "false", Trade(2));
        string mob = $"{{ getZoneID = function() return {QC.Zone.AltarRoom} end, " +
                     "getName = function() return 'Yagudo_Avatar' end }";
        c3Seen_ = "-";
        Check("c3 call: 'onMobDeath': the fifth argument is q.MobDeath, with q.Mission", "true|death False 4",
              Lua($"report(tostring(xi.cs.mission(player, {mob}, '{SampleKey}', 'onMobDeath', " +
                  "{ isKiller = false })))") + $"|{c3Seen_}");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
    }

    private static string c3Member_ = "-";

    // each q.Mission member not run by another check, on the sample key at Naji: event 1000's finish runs the writes,
    // event 1001's the reads
    private sealed class MembersMission : MissionScript
    {
        public MembersMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: null).Npc(QC.Zone.Metalworks, "Naji")
                .OnTrigger(q => QuestReply.Event(q.Mission.IsCurrent ? 1001 : 1000))
                .OnEventFinish(QC.Zone.Metalworks, 1000, q =>
                {
                    q.Mission.Begin();
                    q.Mission.SetStatusAt(3, 1);
                    q.Mission.SetVar("Option", 4);
                    q.Mission.SetVar("Timed", 1, 2000000000);
                    q.Mission.SetMustZone();
                    q.Mission.SetLocalVar("Local", 7);
                })
                .OnEventFinish(QC.Zone.Metalworks, 1001, q =>
                {
                    c3Member_ = $"{q.Mission.Status} {q.Mission.StatusAt(1)} {q.Mission.Var("Option")} " +
                                $"{q.Mission.MustZone} {q.Mission.LocalVar("Local")}";
                    q.Player.SetRankPoints(120);
                    q.Player.SetRank(2);
                    q.Mission.Cleanup();
                    q.Mission.CompleteRaw();
                });
        }
    }

    // the same steps through the Lua Mission methods, for the comparison: each line is the Lua call the member's
    // comment in LiveMissionProgress names (Mission:begin, Container:setVar and the rest)
    private const string C3MemberWrites = @"
        local m = Mission:new(13, 60)
        m:begin(player)
        player:setMissionStatus(13, 3, 1)
        m:setVar(player, 'Option', 4)
        m:setVar(player, 'Timed', 1, 2000000000)
        m:setMustZone(player)
        m:setLocalVar(player, 'Local', 7)";

    private const string C3MemberFinish = @"
        local m = Mission:new(13, 60)
        local read = table.concat({ player:getMissionStatus(13), player:getMissionStatus(13, 1),
            m:getVar(player, 'Option'), tostring(m:getMustZone(player)), m:getLocalVar(player, 'Local') }, ' ')
        player:setRankPoints(120)
        player:setRank(2)
        m:cleanup(player)
        player:completeMission(13, 60)
        report(read)";

    // the fresh RoV mission dropped, as C2's Setup does (addMission logs an overwrite), and a current Bastok mission
    // (Geological Survey): with none, Metalworks' 1000 finish at Naji would begin The Zeruhn Report on the C# side only
    private static string C3MemberSetup =>
        $"player:delMission(13, {QC.Mission.Rov.Resonance}) player:addMission(1, {QC.Mission.Bastok.GeologicalSurvey})";

    // the expiry column of the sample mission's Timed var; Cleanup deletes the row, so this is read before it
    private static string C3TimedExpiry()
    {
        var r = Sql(db, "SELECT expiry FROM char_vars WHERE charid = ? AND varname = 'Mission[13][60]Timed'",
                    BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>("expiry") : "no row";
    }

    private static void RunC3Members()
    {
        NewWorld();
        zone.TestQuestScripts.Add(new MembersMission());
        Lua("InteractionGlobal.reload(true)");
        uint luaChar = CharIds[0], csChar = CharIds[1];
        Cleanup(db, luaChar);
        Fixture(luaChar, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        Lua(C3MemberSetup);
        Lua(C3MemberWrites);
        string luaExpiry = C3TimedExpiry();
        string luaRead = Lua(C3MemberFinish);
        Logout();
        string luaSide = C2Snapshot();

        Cleanup(db, csChar);
        Fixture(csChar, QC.Zone.Metalworks, 64.0f, -14.0f, -4.0f, 0);
        Login();
        Lua(C3MemberSetup);
        TalkTo("Naji");
        Finish();
        string csExpiry = C3TimedExpiry();
        TalkTo("Naji");
        Finish();
        Logout();
        string csSide = C2Snapshot();
        Check("c3 member: SetVar with an expiry writes the Lua's expiry", "2000000000/2000000000",
              $"{luaExpiry}/{csExpiry}");
        Check("c3 member: the reads through q.Mission are the Lua Mission's", luaRead,
              c3Member_.Replace("True", "true"));
        Check("c3 member: Begin, SetStatusAt, SetVar with expiry, SetMustZone, SetRank, SetRankPoints, Cleanup and " +
              "CompleteRaw leave the char as the Lua methods do", luaSide, csSide);
        Cleanup(db, luaChar);
        Cleanup(db, csChar);
        zone.TestQuestScripts.Clear();
    }

    // Bastok Markets area 906, away from the other checks' areas: the sample mission's enter sets C3_AREA to 1
    private sealed class AreaMission : MissionScript
    {
        public const int Area = 906;

        public AreaMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b) => b.State(current: true)
            .OnTriggerAreaEnter(QC.Zone.BastokMarkets, Area, q => q.Player.SetLocalVar("C3_AREA", 1));
    }

    // the char walks into area 906 with the sample mission current: C3_AREA, as the mission's handler and any patch
    // left it
    private static string C3AreaEnter()
    {
        uint id = CharIds[0];
        int ml = (int)MissionLog.RoV;
        Cleanup(db, id);
        Fixture(id, QC.Zone.BastokMarkets, -296.0f, -16.0f, -107.0f, 0);
        Login();
        Lua($"player:delMission({ml}, {QC.Mission.Rov.Resonance}) " +
            $"player:addMission({ml}, {QC.Mission.Rov.TheEndlessSky})");
        Lua("GetZone(xi.zone.BASTOK_MARKETS):registerCuboidTriggerArea(" +
            $"{AreaMission.Area}, -282, -18, -109, -278, -14, -105)");
        // past xi.player.onGameIn's 2.5 s ZoningIn: no trigger area is entered while it is set
        Tick(3000);
        WalkTo(-280.0f, -16.0f, -107.0f);
        string seen = Lua("report(tostring(player:getLocalVar('C3_AREA')))");
        Finish();
        Logout();
        Cleanup(db, id);
        return seen;
    }

    // a mission patch replacing a state's onTriggerAreaEnter, and one wrapping it
    private static void RunC3AreaPatches()
    {
        string key = QC.MissionKey.Rov.TheEndlessSky;
        ushort markets = QC.Zone.BastokMarkets;
        NewWorld();
        zone.TestQuestScripts.Add(new AreaMission());
        Lua("InteractionGlobal.reload(true)");
        Check("c3 patch: the area enter without a patch", "1", C3AreaEnter());

        NewWorld();
        zone.TestQuestScripts.Add(new AreaMission());
        zone.TestQuestModules.Add(new P4LiveModule("c3_live_area_replace", m => m.Mission(key).State(1)
            .ReplaceOnTriggerAreaEnter(markets, AreaMission.Area, q => q.Player.SetLocalVar("C3_AREA", 2))));
        var lines = P4Reload("custom/lua/c3_live_area_replace.lua");
        Check("c3 patch: the area module enabled",
              "modules: c3_live_area_replace enabled (modules/custom/lua/c3_live_area_replace.lua)",
              string.Join(" | ", lines));
        Check("c3 patch: the area label", $"{key} 1/-/onTriggerAreaEnter:{AreaMission.Area}",
              string.Join(",", zone.ModuleLabels("modules/custom/lua/c3_live_area_replace.lua")));
        Check("c3 patch: ReplaceOnTriggerAreaEnter runs in place of the mission's enter", "2", C3AreaEnter());
        zone.TestQuestModules.Clear();

        NewWorld();
        zone.TestQuestScripts.Add(new AreaMission());
        zone.TestQuestModules.Add(new P4LiveModule("c3_live_area_wrap", m => m.Mission(key).State(1)
            .WrapOnTriggerAreaEnter(markets, AreaMission.Area, (q, b) =>
            {
                b(q);
                q.Player.SetLocalVar("C3_AREA", q.Player.LocalVar("C3_AREA") + 10);
            })));
        P4Reload("custom/lua/c3_live_area_wrap.lua");
        Check("c3 patch: WrapOnTriggerAreaEnter runs the mission's enter, then its own step", "11", C3AreaEnter());
        zone.TestQuestModules.Clear();
        zone.TestQuestScripts.Clear();
    }

    private static string c3Loot_ = "-";

    private sealed class LootBattlefield : BattlefieldScript
    {
        public LootBattlefield() : base("EYE_OF_THE_STORM") { }

        public override BattlefieldData Data { get; } = EyeData();

        public override void BattlefieldEntry(IBattlefieldContext q) =>
            c3Loot_ = ReferenceEquals(q.Self.Loot, q.Self.Loot).ToString();
    }

    private static void RunC3Loot()
    {
        NewWorld();
        zone.TestQuestScripts.Add(new LootBattlefield());
        Lua("InteractionGlobal.reload(true)");
        Lua("local c = xi.battlefield.contents[198] c:battlefieldEntry({}, {})");
        Check("c3 loot: self.loot read twice in a call is one table", "True", c3Loot_);
        zone.TestQuestScripts.Clear();
    }
}
