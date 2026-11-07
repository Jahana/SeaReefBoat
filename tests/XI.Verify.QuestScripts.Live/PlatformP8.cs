using System;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P8 (C1): an OnZoneIn reply with text_table / flags reaches luautils::OnZoneIn as the table { id, textTable,
// flags }, as a Lua zone-in's return { 956, 0 } does: the container's onZoneIn handler and the NPC call's questZoneIn
// each return it (read in Lua: its type and fields), a plain id is still a number, and a login plays the table's
// cutscene with its text table (0x00A EventNo) and flags (EventMode)
internal static partial class Program
{
    private static void RunPlatformP8()
    {
        Console.WriteLine("quest rework P8: the OnZoneIn table reply");
        RunP8ContainerZoneIn();
        RunP8CalledZoneIn();
    }

    // a zone-in's return as Lua sees it: the number, or {a,b,c}#n (the table's fields and length), nil. %d, not tostring:
    // the server's tostring prints a negative number as its uint32 (tostring(-1) is 4294967295)
    private const string P8Shape = @"
        local function num(v) return type(v) == 'number' and string.format('%d', v) or tostring(v) end
        local function shape(r)
            if type(r) == 'table' then return string.format('{%s,%s,%s}#%d', num(r[1]), num(r[2]), num(r[3]), #r) end
            return num(r)
        end";

    // the reply the char var P8 picks: 1 a text table, 2 a text table and flags, 3 a plain id, 4 a field the engine does
    // not read there (refused)
    private static QuestReply P8Reply(IQuestContext q) => q.Player.CharVar("P8") switch
    {
        1 => QuestReply.Event(9997).TextTable(0),
        2 => QuestReply.Event(9997).TextTable(-1).Flags(0x13),
        3 => QuestReply.Event(9997),
        4 => QuestReply.Event(9997).CanSkip(),
        5 => QuestReply.Event(9997).TextTable(12),
        _ => null,
    };

    // 1. the container: InteractionLookup's onZoneIn[1] entry of the C# quest returns the table; a login plays it
    private static void RunP8ContainerZoneIn()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P8ZoneInQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        int loginMark = log.Count;
        Login();
        // the fixture's mission log is past RoV 1-1's 30035 and SoA 1-1's 22 (Fixture): its first login plays no event
        Check("zoneIn table: the fixture's first login in Bastok Markets plays no mission's zone-in cutscene", "",
              string.Join(" | ", log.Skip(loginMark).Where(l => l.StartsWith("event: ", StringComparison.Ordinal))));
        // the state RoV 1-1's finish leaves (mission:complete, then its reward's nextMission): Resonance current
        Check("  ... RoV 1-1 completed, Resonance current; Rumors from the West current, its status 7",
              $"true/{Mission.Rov.Resonance}/{Mission.Soa.RumorsFromTheWest}/7",
              Lua("report(tostring(player:hasCompletedMission(xi.mission.log_id.ROV, " +
                  "xi.mission.id.rov.RHAPSODIES_OF_VANADIEL))) " +
                  "report(player:getCurrentMission(xi.mission.log_id.ROV)) " +
                  "report(player:getCurrentMission(xi.mission.log_id.SOA)) " +
                  "report(player:getMissionStatus(xi.mission.log_id.SOA))").Replace(",", "/"));
        string Direct(int arm)
        {
            Lua($"player:setCharVar('P8', {arm})");
            return Lua(P8Shape + $@"
                for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].onZoneIn[1] or {{}}) do
                    if e.container and e.container.id == '{FixtureContainer}' then report(shape(e.handler(player, {Zone.PortBastok}))) return end
                end
                report('no entry')");
        }
        Check("zoneIn table: text_table 0: the table { 9997, 0, 0 } (OnZoneIn's flags default)", "{9997,0,0}#3", Direct(1));
        Check("  text_table -1 and flags 0x13: { 9997, -1, 19 }", "{9997,-1,19}#3", Direct(2));
        Check("  a plain id: still the number", "9997", Direct(3));
        int mark = log.Count;
        Check("  canSkip: refused, nothing returned", "nil", Direct(4));
        Check("    and logged", $"quests: {FixtureKey} onZoneIn: a zone-in takes text_table and flags only (the engine's OnZoneIn reads {{ id, textTable, flags }}), not canSkip",
              string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))));
        Check("  no reply: nil", "nil", Direct(0));

        // a login with each: the 0x00A's EventNo (the text table, the zone's id when -1), EventPara and EventMode
        string Played(int arm)
        {
            Lua($"player:setCharVar('P8', {arm})");
            Logout();
            Login();
            var p = pending_.LastOrDefault(x => Opcode(x) == 0x00A);
            if (p == null) return "no 0x00A";
            var b = p.Buffer;
            return $"{BitConverter.ToUInt16(b, 64)}/{BitConverter.ToUInt16(b, 100)}/{BitConverter.ToUInt16(b, 102)}";
        }
        Check("zoneIn table: a login at text_table 12: the 0x00A plays 9997 with text table 12, flags 0", "12/9997/0", Played(5));
        Check("  text_table -1 and flags 0x13: the zone's own table (235), flags 0x13", $"{Zone.BastokMarkets}/9997/19", Played(2));
        Check("  a plain id: the zone's table, flags 0 (as before)", $"{Zone.BastokMarkets}/9997/0", Played(3));
        Lua("player:setCharVar('P8', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    private sealed class P8ZoneInQuest : QuestScript
    {
        public P8ZoneInQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b) =>
            b.AnyState().OnZoneIn(Zone.BastokMarkets, P8Reply).OnEventFinish(Zone.BastokMarkets, 9997, q => { });
    }

    // 2. the NPC call: xi.cs.questZoneIn returns the table to the Zone.lua that called it (cs = ... or cs)
    private static void RunP8CalledZoneIn()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P8CalledZoneInQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        string Called(int arm)
        {
            Lua($"player:setCharVar('P8', {arm})");
            return Lua(P8Shape + $"\nreport(shape(xi.cs.questZoneIn(player, '{CallKey}', {Zone.PortBastok})))");
        }
        Check("questZoneIn table: text_table 0: { 9997, 0, 0 }", "{9997,0,0}#3", Called(1));
        Check("  text_table -1 and flags 0x13: { 9997, -1, 19 }", "{9997,-1,19}#3", Called(2));
        Check("  a plain id: the number", "9997", Called(3));
        int mark = log.Count;
        Check("  canSkip: nil", "nil", Called(4));
        Check("    and logged", $"quests: {CallKey} onZoneIn: a zone-in takes text_table and flags only (the engine's OnZoneIn reads {{ id, textTable, flags }}), not canSkip",
              string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))));
        Lua("player:setCharVar('P8', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // A Discerning Eye (bastok 71) as a call-only quest: a called mob death (as MobZoneCallQuest) and the zone-in
    private sealed class P8CalledZoneInQuest : QuestScript
    {
        public P8CalledZoneInQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b) =>
            b.State(QuestStatus.Available)
                .Npc(Zone.AltarRoom, XI.QuestContent.Npc.AltarRoom.YagudoAvatar).Called()
                .OnMobDeath(q => { })
                .OnZoneIn(Zone.BastokMarkets, P8Reply);
    }
}
