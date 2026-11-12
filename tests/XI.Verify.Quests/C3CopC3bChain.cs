// XI.Verify.Quests -- quest rework C3 C3b: Head Wind's use of the shared Shikaree skillchain
// (Shared/ShikareeSkillchain). The head_wind.lua's configuration (area stride 5, three slots, the reset over Z, Y, X,
// the message offsets and the chain tables) is seen by no trace past the IDLE and SOLO states, so each part is pinned
// here: the tick is run on fake mobs whose base ids are the Shikarees' (Z 2000, Y 2001, X 2002), in area 1 and in
// area 2 (ids + 5).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    const int C3bZ = 2000;
    const int C3bY = 2001;
    const int C3bX = 2002;

    // an arena of Head Wind's Shikarees: the battlefield's scState and scLeader set (the leader by base id), the mobs
    // of the area added by base id (the area's stride of 5 is added), a foe (9000) as the target of every mob
    sealed class C3bArena
    {
        public readonly BattlefieldContextFake Q;
        public readonly BattlefieldViewFake Bf;
        public readonly BattlefieldEngineFake E;
        readonly BattlefieldScript script;
        readonly int offset;

        public C3bArena(BattlefieldScript script, int area, int state, int leaderBase)
        {
            this.script = script;
            offset = (area - 1) * 5;
            var (q, _) = C3Q1Ctx(script, "x");
            Q = q;
            E = (BattlefieldEngineFake)q.Engine;
            int zone = script.Data.ZoneId;
            E.ZoneMobIds[$"{zone}:{QC.Mob.BoneyardGully.SHIKAREE_Z_HW}"] = C3bZ;
            E.ZoneMobIds[$"{zone}:{QC.Mob.BoneyardGully.SHIKAREE_Y_HW}"] = C3bY;
            E.ZoneMobIds[$"{zone}:{QC.Mob.BoneyardGully.SHIKAREE_X_HW}"] = C3bX;
            E.Texts[$"{zone}:SHIKAREE_Z_OFFSET"] = 7731;
            E.Texts[$"{zone}:SHIKAREE_Y_OFFSET"] = 7742;
            E.Texts[$"{zone}:SHIKAREE_X_OFFSET"] = 7753;
            Bf = new BattlefieldViewFake { Id = script.Data.BattlefieldId, ZoneId = zone, Area = area };
            Bf.LocalVars["scState"] = state;
            Bf.LocalVars["scLeader"] = leaderBase == 0 ? 0 : leaderBase + offset;
            Q.Battlefield = Bf;
        }

        public BattlefieldMobFake Add(int baseId, int tp = 0, bool alive = true)
        {
            var m = new BattlefieldMobFake
            {
                Id = baseId + offset, IsSpawned = alive, HP = alive ? 100 : 0, MaxHP = 100, TP = tp, Battlefield = Bf,
                Target = new BattlefieldMobFake { Id = 9000, IsMob = false, HP = 100 },
            };
            E.MobsById[m.Id] = m;
            return m;
        }

        public BattlefieldMobFake this[int baseId] => (BattlefieldMobFake)E.MobsById[baseId + offset];

        // the tick, then every call it made: the battlefield's local vars first, then each mob's (not its local vars)
        public string Tick()
        {
            try { script.OnBattlefieldTick(Q, 1); }
            catch (Exception ex) { return "THREW " + ex.GetType().Name; }
            var calls = Bf.Calls.Where(c => c.StartsWith("Battlefield.SetLocalVar("))
                .Select(c => c.Replace("Battlefield.SetLocalVar(", "bf(")).ToList();
            foreach (var m in E.MobsById.Values.Cast<BattlefieldMobFake>().OrderBy(m => m.Id))
            {
                calls.AddRange(m.Calls.Where(c => !c.StartsWith("Mob.SetLocalVar"))
                    .Select(c => $"{m.Id}:{c.Substring(4)}"));
            }

            return string.Join(";", calls);
        }

        // the battlefield's state and leader, then scSkill/scMessage/scNextID of each mob that has a skill
        public string State() =>
            string.Join(" ", new[] { $"s{Bf.LocalVar("scState")} l{Bf.LocalVar("scLeader")}" }.Concat(
                E.MobsById.Values.Cast<BattlefieldMobFake>().OrderBy(m => m.Id).Where(m => m.LocalVar("scSkill") > 0)
                    .Select(m =>
                        $"{m.Id}:{m.LocalVar("scSkill")}/{m.LocalVar("scMessage")}/{m.LocalVar("scNextID")}")));
    }

    static void RunC3C3bChain()
    {
        var s = new QC.Battlefields.BoneyardGully.HeadWind();
        const int Z = C3bZ, Y = C3bY, X = C3bX;
        const string Sep = " | ";

        string Idle(int area, params (int Id, int Tp, bool Alive)[] mobs)
        {
            var a = new C3bArena(s, area, 0, 0);
            foreach (var m in mobs) a.Add(m.Id, m.Tp, m.Alive);
            return a.Tick();
        }
        Check("c3 C3b: Head Wind's IDLE: the first of Z, Y, X alive with 1000 TP leads (state READY, its id, its " +
              "line + 1), X as the third slot too; 999 TP and dead mobs lead nobody; area 2's ids are + 5",
              "bf(scState,1);bf(scLeader,2000);2000:MessageText(7732)" + Sep +
              "bf(scState,1);bf(scLeader,2001);2001:MessageText(7743)" + Sep +
              "bf(scState,1);bf(scLeader,2002);2002:MessageText(7754)" + Sep +
              "bf(scState,1);bf(scLeader,2006);2006:MessageText(7743)" + Sep + Sep,
              string.Join(Sep,
                  Idle(1, (Z, 1000, true), (Y, 1000, true), (X, 0, true)),
                  Idle(1, (Z, 999, true), (Y, 1000, true), (X, 1000, true)),
                  Idle(1, (Z, 0, true), (Y, 0, true), (X, 1000, true)),
                  Idle(2, (Z, 0, true), (Y, 1000, true), (X, 0, true)),
                  Idle(1, (Z, 5000, false), (Y, 0, true), (X, 0, true)),
                  Idle(1, (Z, 0, true), (Y, 999, true), (X, 0, true))));

        string Ready(int area, int leader, params (int Id, int Tp)[] mobs)
        {
            var a = new C3bArena(s, area, 1, leader);
            foreach (var m in mobs) a.Add(m.Id, m.Tp);
            a.Tick();
            return a.State();
        }
        Check("c3 C3b: Head Wind's READY with all three alive: Z leads Skewer, Spiral Hell, Evisceration; Y leads " +
              "Guillotine, Shadowstitch, Wheeling Thrust; X leads Evisceration, Vorpal Scythe, Impulse Drive; the " +
              "leader's line + 2, the others' + 3, each mob's next id (0 for the last); area 2 is + 5",
              "s2 l2000 2000:118/7733/2001 2001:104/7745/2002 2002:25/7756/0" + Sep +
              "s2 l2001 2000:119/7734/0 2001:102/7744/2002 2002:18/7756/2000" + Sep +
              "s2 l2002 2000:120/7734/0 2001:101/7745/2000 2002:25/7755/2001" + Sep +
              "s2 l2006 2005:119/7734/0 2006:102/7744/2007 2007:18/7756/2005",
              string.Join(Sep,
                  Ready(1, Z, (Z, 1000), (Y, 1000), (X, 1000)),
                  Ready(1, Y, (Z, 1000), (Y, 1000), (X, 1000)),
                  Ready(1, X, (Z, 1000), (Y, 1000), (X, 1000)),
                  Ready(2, Y, (Z, 1000), (Y, 1000), (X, 1000))));
        string Pair(int area, int leader, int other, int dead)
        {
            var a = new C3bArena(s, area, 1, leader);
            a.Add(leader, 1000);
            a.Add(other, 1000);
            a.Add(dead, 1000, false);
            a.Tick();
            return a.State();
        }
        Check("c3 C3b: Head Wind's READY with two alive, by the other alive mob: X leads with Y Evisceration, " +
              "Vorpal Scythe, with Z Dancing Edge, Penta Thrust; Y leads with X Spiral Hell, Shadowstitch, with Z " +
              "Spiral Hell, Impulse Drive; Z leads with X Wheeling Thrust, Shark Bite, with Y Skewer, Spiral Hell; " +
              "in area 2 the chain is found by the other mob's base id",
              "s2 l2002 2001:101/7745/0 2002:25/7755/2001" + Sep +
              "s2 l2002 2000:116/7734/0 2002:23/7755/2000" + Sep +
              "s2 l2001 2001:104/7744/2002 2002:18/7756/0" + Sep +
              "s2 l2001 2000:120/7734/0 2001:104/7744/2000" + Sep +
              "s2 l2000 2000:119/7733/2002 2002:24/7756/0" + Sep +
              "s2 l2000 2000:118/7733/2001 2001:104/7745/0" + Sep +
              "s2 l2006 2006:104/7744/2007 2007:18/7756/0",
              string.Join(Sep,
                  Pair(1, X, Y, Z), Pair(1, X, Z, Y), Pair(1, Y, X, Z), Pair(1, Y, Z, X), Pair(1, Z, X, Y),
                  Pair(1, Z, Y, X), Pair(2, Y, X, Z)));

        // a reset (the leader died while READY) clears each of Z, Y and X, and nothing after X (Z + 3)
        {
            var a = new C3bArena(s, 1, 1, 0);
            a.Bf.LocalVars["scLeader"] = 9999;
            foreach (int id in new[] { Z, Y, X, X + 1 })
            {
                a.Add(id, 0);
                a[id].LocalVars["scSkill"] = 118;
                a[id].LocalVars["scMessage"] = 7;
                a[id].LocalVars["scNextID"] = 3;
            }

            string calls = a.Tick();
            Check("c3 C3b: Head Wind's reset (the leader gone) turns magic casting back on for Z, Y and X and clears " +
                  "their skill, message and next id, and leaves the id after X alone",
                  "bf(scState,0);bf(scLeader,0);2000:SetMagicCastingEnabled(True);2001:SetMagicCastingEnabled(True);" +
                  "2002:SetMagicCastingEnabled(True)|0/0/0,0/0/0,0/0/0,118/7/3",
                  calls + "|" + string.Join(",", new[] { Z, Y, X, X + 1 }.Select(id =>
                      $"{a[id].LocalVar("scSkill")}/{a[id].LocalVar("scMessage")}/{a[id].LocalVar("scNextID")}")));
        }

        // SOLO: one Shikaree left (a dead one is not counted): its abilities on, the reset, state SOLO; in area 2 too
        string Solo(int area, int state, int alive)
        {
            var a = new C3bArena(s, area, state, 0);
            foreach (int id in new[] { Z, Y, X }) a.Add(id, 1000, id == alive);
            return a.Tick();
        }
        Check("c3 C3b: Head Wind's last Shikaree alone (Z, Y or X, in either area): its abilities on, the reset, " +
              "state SOLO; in SOLO it does nothing",
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2000:SetMobAbilityEnabled(True);" +
              "2000:SetMagicCastingEnabled(True)" + Sep +
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2002:SetMobAbilityEnabled(True);" +
              "2002:SetMagicCastingEnabled(True)" + Sep +
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2006:SetMobAbilityEnabled(True);" +
              "2006:SetMagicCastingEnabled(True)" + Sep,
              string.Join(Sep, Solo(1, 0, Z), Solo(1, 2, X), Solo(2, 0, Y), Solo(1, 4, Y)));
    }
}
