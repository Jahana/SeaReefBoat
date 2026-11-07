using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P7 (the platform needs the Other Areas drafts found): test quests of the fixture's key with a zone-wide
// OnZoneOut and an OnMobDeath that reads q.MobDeath and Player.AllianceCharVars; each read compared with what the Lua
// gets, field by field
internal static partial class Program
{
    private static void RunPlatformP7()
    {
        Console.WriteLine("quest rework P7: platform needs");
        RunP7ZoneOut();
        RunP7MobDeath();
        RunP7CalledMobDeath();
    }

    // 1. OnZoneOut: the C# quest's section holds onZoneOut = { [1] = handler } in the zone's table, where
    // InteractionLookup:onZoneOut finds it among the Lua containers' (the same lookup, the same order); luautils::OnZoneOut
    // runs it as the char leaves the zone (CZone::CharZoneOut: a warp, a logout). The char var P7 picks the arm: 1 counts
    // the run in P7_OUT, 2 throws
    private static void RunP7ZoneOut()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P7ZoneOutQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        string entry = Lua($@"
            local ids = {{}}
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].onZoneOut[1] or {{}}) do
                ids[#ids + 1] = e.container and e.container.id or '-'
            end
            report(table.concat(ids, ','))");
        Check("onZoneOut: the C# quest's handler sits in the zone's onZoneOut[1] list of the lookup", "True",
              entry.Split(',').Contains(FixtureContainer).ToString());
        Lua("player:setCharVar('P7', 1)");
        int mark = log.Count;
        Warp(-175.0f, -8.0f, -12.0f, Zone.PortBastok);
        Check("  a warp out of Bastok Markets runs it (P7_OUT 1), with no quests: line", "1|", $"{Var("P7_OUT")}|" +
              string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))));
        Warp(-296.0f, -16.0f, -157.0f, Zone.BastokMarkets);
        Check("  leaving another zone (Port Bastok) does not (still 1)", "1", Var("P7_OUT"));
        Logout();
        Check("  a logout in Bastok Markets runs it too (P7_OUT 2)", "2", Var("P7_OUT"));
        Login();
        Lua("player:setCharVar('P7', 2)");
        mark = log.Count;
        Warp(-175.0f, -8.0f, -12.0f, Zone.PortBastok);
        Check("  a handler that throws is logged; the zone-out goes on (the char in Port Bastok)",
              $"quests: {FixtureKey} onZoneOut: P7 throws|{Zone.PortBastok}",
              $"{string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: ")))}|{c.Zone}");
        Lua("player:setCharVar('P7', 0) player:setCharVar('P7_OUT', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // P7 1: leaving Bastok Markets adds 1 to P7_OUT; P7 2: it throws
    private sealed class P7ZoneOutQuest : QuestScript
    {
        public P7ZoneOutQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .OnZoneOut(Zone.BastokMarkets, q =>
                {
                    if (q.Player.CharVar("P7") == 2) throw new InvalidOperationException("P7 throws");
                    if (q.Player.CharVar("P7") == 1) q.Player.SetCharVar("P7_OUT", q.Player.CharVar("P7_OUT") + 1);
                });
        }
    }

    // 2. q.MobDeath: luautils::OnMobDeath's optParams as the C# handler reads them, field by field against the table Lua's
    // InteractionGlobal.onMobDeath got (a wrapper records it); a kill by the char in Carpenters' Landing, plain and with a
    // weapon skill's mark (weaponskillHit set after the damage, as weaponskills.lua sets it after takeDamage); then the
    // container's handler called directly with optParams tables of other shapes; Player.AllianceCharVars against Lua's
    // loop over getAlliance()
    private static void RunP7MobDeath()
    {
        uint id = CharIds[0];
        const string Funguar = "Marsh_Funguar";
        zone.TestQuestScripts.Add(new P7MobDeathQuest(Funguar));
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.CarpentersLanding, -414.6f, 0f, -363.5f, 0);
        Login();
        try
        {
            Lua(@"
                __p7deaths = {}
                __p7onMobDeath = InteractionGlobal.onMobDeath
                InteractionGlobal.onMobDeath = function(mob, player, opt, fallback)
                    if player then
                        __p7deaths[#__p7deaths + 1] = string.format('%s/%s/%s/%s/%s', tostring(opt.isKiller), tostring(opt.noKiller),
                            tostring(opt.isWeaponSkillKill), tostring(opt.weaponskillUsed), tostring(opt.weaponskillDamage))
                    end
                    return __p7onMobDeath(mob, player, opt, fallback)
                end");
            string Kill(string afterDamage)
            {
                P7MobDeathQuest.Seen.Clear();
                Lua("__p7deaths = {}");
                uint mob = Here.Mobs.First(m => m.Name == Funguar && m.IsSpawned && m.IsAlive).Id;
                Collect();
                // claimed by the char as popFromQM's claim does it (a second swing after a kill does not engage again)
                Lua($"local m = GetMobByID({mob}) m:updateClaim(player) m:takeDamage(m:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING) {afterDamage}");
                Tick(1000);
                Tick(1000);
                return $"{string.Join(",", P7MobDeathQuest.Seen)}|{Lua("report(table.concat(__p7deaths, ','))")}";
            }
            var plain = Kill("").Split('|');
            Check("mobDeath: a kill by the char: the C# handler's MobDeath is the optParams Lua's onMobDeath got", plain[1], plain[0].ToLowerInvariant());
            Check("  the killer, no weapon skill", "true/false/false/0/0", plain[1]);
            var ws = Kill("m:setLocalVar('weaponskillHit', bit.lshift(35, 24) + 1234)").Split('|');
            Check("mobDeath: a weapon skill's kill: the same, field by field", ws[1], ws[0].ToLowerInvariant());
            Check("  weaponskillUsed 35, weaponskillDamage 1234", "true/false/true/35/1234", ws[1]);

            // the container's handler called with other tables: a missing field reads false / 0; a truthy number is true (Lua's
            // `if optParams.isKiller`); no table: MobDeath null
            uint any = Here.Mobs.First(m => m.Name == Funguar).Id;
            string Direct(string opt)
            {
                P7MobDeathQuest.Seen.Clear();
                Lua($@"
                    for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.CARPENTERS_LANDING]['{Funguar}'].onMobDeath) do
                        if e.container and e.container.id == '{FixtureContainer}' then e.handler(GetMobByID({any}), player, {opt}) end
                    end");
                return string.Join(",", P7MobDeathQuest.Seen);
            }
            Check("mobDeath: called with { isKiller = true } only: the other fields false / 0", "True/False/False/0/0", Direct("{ isKiller = true }"));
            Check("  { isKiller = 1, noKiller = true }: Lua's truthiness (1 is true)", "True/True/False/0/0", Direct("{ isKiller = 1, noKiller = true }"));
            Check("  { isKiller = false, weaponskillUsed = 7.6 }: the number as the engine's llround reads it", "False/False/False/8/0",
                  Direct("{ isKiller = false, weaponskillUsed = 7.6 }"));
            Check("  nil: MobDeath null (a Lua handler reading optParams.isKiller would fail)", "null", Direct("nil"));

            // Player.AllianceCharVars: alone, the char's own var, as Lua's loop over getAlliance() reads it
            Lua("player:setCharVar('P7_ALLY', 6)");
            string lua = Lua("local t = {} for _, m in ipairs(player:getAlliance()) do t[#t + 1] = m:getCharVar('P7_ALLY') end report(table.concat(t, ','))");
            Direct("{ isKiller = true }");
            Check("allianceCharVars: alone, Lua's getAlliance() loop (the char's own var)", lua, P7MobDeathQuest.Allies);
            Check("  ... which is 6", "6", P7MobDeathQuest.Allies);
            Lua("player:setCharVar('P7_ALLY', 0)");
        }
        finally
        {
            Lua("if __p7onMobDeath then InteractionGlobal.onMobDeath = __p7onMobDeath end __p7onMobDeath = nil __p7deaths = nil");
            Logout();
            Cleanup(db, id);
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }
    }

    // the mob's OnMobDeath records q.MobDeath's fields ("null" without it) and the alliance's P7_ALLY
    private sealed class P7MobDeathQuest : QuestScript
    {
        public static readonly List<string> Seen = new();
        public static string Allies;
        private readonly string mob_;

        public P7MobDeathQuest(string mob) : base(FixtureLog, FixtureConst) => mob_ = mob;

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.CarpentersLanding, mob_).OnMobDeath(q =>
                {
                    var d = q.MobDeath;
                    Seen.Add(d == null ? "null" : $"{d.IsKiller}/{d.NoKiller}/{d.IsWeaponSkillKill}/{d.WeaponskillUsed}/{d.WeaponskillDamage}");
                    Allies = string.Join(",", q.Player.AllianceCharVars("P7_ALLY"));
                });
        }
    }

    // 3. an NPC call's mob death: xi.cs.quest(player, mob, key, 'onMobDeath'[, optParams]): the fifth argument is q.MobDeath,
    // null without it
    private static void RunP7CalledMobDeath()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P7CalledDeathQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        string Mob() => $"{{ getZoneID = function() return {Zone.AltarRoom} end, getName = function() return 'Yagudo_Avatar' end }}";
        string Death(string opt)
        {
            P7CalledDeathQuest.Seen = "-";
            string ran = Lua($"report(tostring(xi.cs.quest(player, {Mob()}, '{CallKey}', 'onMobDeath'{(opt == null ? "" : ", " + opt)})))");
            return $"{ran}|{P7CalledDeathQuest.Seen}";
        }
        Check("called mobDeath: optParams as the fifth argument is q.MobDeath", "true|False/False/True/9/99",
              Death("{ isKiller = false, noKiller = false, isWeaponSkillKill = true, weaponskillUsed = 9, weaponskillDamage = 99 }"));
        Check("  without it: null", "true|null", Death(null));
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // A Discerning Eye (the NPC-call probe's key) with a called mob death on Altar Room's Yagudo Avatar that records
    // q.MobDeath
    private sealed class P7CalledDeathQuest : QuestScript
    {
        public static string Seen = "-";

        public P7CalledDeathQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.AltarRoom, XI.QuestContent.Npc.AltarRoom.YagudoAvatar).Called()
                .OnMobDeath(q =>
                {
                    var d = q.MobDeath;
                    Seen = d == null ? "null" : $"{d.IsKiller}/{d.NoKiller}/{d.IsWeaponSkillKill}/{d.WeaponskillUsed}/{d.WeaponskillDamage}";
                });
        }
    }
}
