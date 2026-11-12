// XI.Verify.Quests -- quest rework C3 C3c: Mine Shaft #2716's A Century of Hardship on C#, on the callback seam (PR2).
// Its data against the Lua file's text (as it stood at the recording, aecc8ebf), the char's DEATH listener (armed on
// entry, removed on leave) and each arm of its body no trace reaches (a char's DEATH listener gets no killer), each arm
// of onBattlefieldTick alone (Bugbby gone, the restores, the HP-threshold command, the party-wipe reset, the 90%
// threshold on both sides, the global timer and its parts), handleMoblinCommand's command types, refusals and
// acceptances with each roll's bounds, and the six callbacks fired, over the fakes.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using Coh = XI.QuestContent.Battlefields.MineShaft2716.CenturyOfHardship;

internal static partial class Program
{
    // the Century of Hardship world over fakes: the 15 mobs of the three areas (area 1's Movamuq at 2000; offsets 0 to
    // 3 the Moblins, 4 Bugbby; the area stride 5), the zone's dialogue texts, the engine's queues, GetSystemTime 1000
    sealed class CohWorld
    {
        public const int Mova = 2000;
        public const int MovaText = 7828, ChekText = 7836, TrikText = 7844, SwipText = 7852, BugText = 7859;
        public readonly Coh S = new Coh();
        public readonly BattlefieldEngineFake E = new BattlefieldEngineFake();
        public readonly BattlefieldViewFake Bf;
        public readonly BattlefieldCallbacksFake Queues;
        public readonly Dictionary<int, BattlefieldMobFake> Mobs = new Dictionary<int, BattlefieldMobFake>();
        public readonly BattlefieldMobFake Char = new BattlefieldMobFake { Id = 7001, IsMob = false };
        public readonly int Area;

        public CohWorld(int area = 1)
        {
            Area = area;
            Bf = new BattlefieldViewFake { Id = QC.Battlefield.CenturyOfHardship, Area = area };
            Queues = new BattlefieldCallbacksFake(S, E);
            E.Callbacks = Queues;
            int z = QC.Zone.MineShaft2716;
            E.ZoneMobIds[$"{z}:MOVAMUQ"] = Mova;
            E.ZoneMobIds[$"{z}:CHEKOCHUK"] = Mova + 1;
            E.ZoneMobIds[$"{z}:TRIKOTRAK"] = Mova + 2;
            E.ZoneMobIds[$"{z}:SWIPOSTIK"] = Mova + 3;
            E.ZoneMobIds[$"{z}:BUGBBY"] = Mova + 4;
            E.Texts[$"{z}:MOVAMUQ_DIALOGUE"] = MovaText;
            E.Texts[$"{z}:CHEKOCHUK_DIALOGUE"] = ChekText;
            E.Texts[$"{z}:TRIKOTRAK_DIALOGUE"] = TrikText;
            E.Texts[$"{z}:SWIPOSTIK_DIALOGUE"] = SwipText;
            E.Texts[$"{z}:BUGBBY_DIALOGUE"] = BugText;
            for (int i = 0; i < 15; i++)
            {
                var m = new BattlefieldMobFake { Id = Mova + i, MaxHP = 1000, Battlefield = Bf };
                Mobs[Mova + i] = m;
                E.MobsById[Mova + i] = m;
                E.MobById(Mova + i);
            }
            E.SystemTime = 1000;
        }

        // a mob of this world's area by its area 1 offset (0 Movamuq .. 4 Bugbby)
        public BattlefieldMobFake M(int offset) => Mobs[Mova + (Area - 1) * 5 + offset];
        public BattlefieldContextFake Q() => new BattlefieldContextFake(S) { Engine = E, Battlefield = Bf };
        public void Tick() => S.OnBattlefieldTick(Q(), 0);
        // a mob alive at hpp, engaged or not, at a position, with a behaviour
        public BattlefieldMobFake Alive(int offset, int hpp = 100, bool engaged = true)
        {
            var m = M(offset);
            m.IsSpawned = true;
            m.HP = hpp * 10;
            m.HPP = hpp;
            m.IsEngaged = engaged;
            m.Pos = (offset, offset + 1, offset + 2, 0);
            m.Behavior = 1;
            return m;
        }
        // all five alive, Bugbby engaged at full HP
        public CohWorld All()
        {
            for (int i = 0; i <= 4; i++) Alive(i);
            return this;
        }
        public void Rolls(params int[] rolls)
        {
            foreach (var r in rolls) E.Rolls.Enqueue(r);
        }
        public string V(string name) => Bf.LocalVar(name).ToString(System.Globalization.CultureInfo.InvariantCulture);
        public string Calls(int offset) => string.Join(",", M(offset).Calls);
        public string Draws() => string.Join(",", E.Calls.Where(c => c.StartsWith("RandomInt")));
        public string Pending() => string.Join(",", Queues.Pending.Select(p =>
            $"{((BattlefieldMobFake)p.Entity).Id}:{p.Callback.Name}@{p.Deadline}" +
            (p.Captures.Length > 0 ? "(" + string.Join(";", p.Captures) + ")" : "")));
        public void ClearCalls()
        {
            foreach (var m in Mobs.Values) m.Calls.Clear();
            Bf.Calls.Clear();
            E.Calls.Clear();
        }
    }

    // a tick that runs handleMoblinCommand through the HP-threshold arm (the global timer off: nextCommandTime 0, the
    // cycle on so the reset arm is skipped), for the Moblin at offset with the rolls given
    static CohWorld CohCommand(int moblin, int[] rolls, bool msUsed = false, bool target = true, int area = 1)
    {
        var w = new CohWorld(area).All();
        w.Bf.LocalVars["commandSequence"] = 1;
        w.Bf.LocalVars["triggerMoblinCommand"] = w.M(moblin).Id;
        if (msUsed) w.M(4).LocalVars["usedMightyStrikes"] = 1;
        if (target) w.M(moblin).Target = w.Char;
        w.M(4).Behavior = 2;
        w.Rolls(rolls);
        w.ClearCalls();
        w.Tick();
        return w;
    }

    // an exception out of the checks (a mutant that drops a guard) is a printed [FAIL], not a crash with no RESULT line
    internal static void RunC3CopC3c()
    {
        try
        {
            RunC3CopC3cChecks();
        }
        catch (Exception e)
        {
            Check("c3 C3c: the checks run to their end with no exception", "none", e.GetType().Name + ": " + e.Message);
        }
    }

    static void RunC3CopC3cChecks()
    {
        Console.WriteLine("\n== c3 C3c: Mine Shaft #2716's A Century of Hardship ==");
        var s = new Coh();
        var bd = (BattlefieldMissionData)s.Data;
        int z = QC.Zone.MineShaft2716;

        // ---- the data, against the Lua file's text
        Check("c3 C3c: validates clean, its key and Lua file",
              "|battlefield.century_of_hardship scripts/battlefields/Mine_Shaft_2716/century_of_hardship",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}");
        Check("c3 C3c: fields (zone, id, no exp loss, mission, trusts, 6 players, MAX_LEVEL, 30 minutes, index 0, " +
              "grantXP 1000, no title, entry _0d0, exits _0d1-_0d3, Three Paths, Louverance (2) at 8, one group)",
              $"{z} {QC.Battlefield.CenturyOfHardship} False True True 6 MAX_LEVEL 1800 0 1000 0 _0d0 " +
              $"_0d1,_0d2,_0d3 6/{QC.Mission.Cop.ThreePaths} 6 2 8 1",
              $"{bd.ZoneId} {bd.BattlefieldId} {bd.CanLoseExp} {bd.IsMission} {bd.AllowTrusts} {bd.MaxPlayers} " +
              $"{bd.LevelCapSetting} {bd.TimeLimit} {bd.Index} {bd.GrantXP} {bd.Title} " +
              $"{string.Join(",", bd.EntryNpcs)} {string.Join(",", bd.ExitNpcs)} {(int)bd.MissionArea}/{bd.Mission} " +
              $"{(int)bd.MissionStatusArea} {bd.MissionStatus} {bd.RequiredMissionStatus} {bd.Groups.Count}");
        string Ids(BattlefieldGroup g) => string.Join(";", g.AreaMobIds.Select(a =>
            string.Join(",", a.Select(o => o.Key + ":" + o.Offset))));
        Check("c3 C3c: the group: MOVAMUQ +0..+4, +5..+9, +10..+14, spawned, superlink (no superlink group), " +
              "allDeath only",
              "MOVAMUQ:0,MOVAMUQ:1,MOVAMUQ:2,MOVAMUQ:3,MOVAMUQ:4;MOVAMUQ:5,MOVAMUQ:6,MOVAMUQ:7,MOVAMUQ:8," +
              "MOVAMUQ:9;MOVAMUQ:10,MOVAMUQ:11,MOVAMUQ:12,MOVAMUQ:13,MOVAMUQ:14 True True  True False False",
              $"{Ids(bd.Groups[0])} {bd.Groups[0].Spawned.GetValueOrDefault(true)} {bd.Groups[0].Superlink} " +
              $"{bd.Groups[0].SuperlinkGroup} {bd.Groups[0].AllDeath != null} {bd.Groups[0].Death != null} " +
              $"{bd.Groups[0].RandomDeath != null}");
        var won = new BattlefieldViewFake();
        bd.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = won }, null);
        Check("c3 C3c: the group's allDeath sets the status to WON", "Battlefield.SetStatus(2)",
              string.Join(",", won.Calls));
        Check("c3 C3c: overrides onBattlefieldEnter, onBattlefieldLeave and onBattlefieldTick only",
              "OnBattlefieldEnter,OnBattlefieldLeave,OnBattlefieldTick", C3Q1Hooks(s));
        Check("c3 C3c: its six callbacks are coverage labels",
              "callback:bugbearRefusal,callback:deadBugbearReaction,callback:heavyWhisk,callback:mightyStrikes," +
              "callback:moblinInsult,callback:playerDeath",
              string.Join(",", s.CoverageLabels().Where(l => l.StartsWith("callback:")).OrderBy(l => l,
                  StringComparer.Ordinal)));
        Check("c3 C3c: mobDialogue as the Lua's (base text, the death line's offset, the refusal skill)",
              "MOVAMUQ:MOVAMUQ_DIALOGUE+6:1081 CHEKOCHUK:CHEKOCHUK_DIALOGUE+6:1099 " +
              "TRIKOTRAK:TRIKOTRAK_DIALOGUE+6:1082 " +
              "SWIPOSTIK:SWIPOSTIK_DIALOGUE+5:591 BUGBBY:BUGBBY_DIALOGUE+9:0",
              string.Join(" ", Coh.MobDialogue.Select(d => $"{d.Mob}:{d.Base.Name}+{d.Offset}:{d.MobSkill}")));
        Check("c3 C3c: the constants: NO_MOVE 65, NO_TURN 0x400, Heavy Whisk 358, Mighty Strikes 688, Goblin Dice " +
              "Heal 1099",
              "65 1024 358 688 1099",
              $"{Coh.MobModNoMove} {Coh.BehaviorNoTurn} {Coh.HeavyWhisk} {Coh.MightyStrikes1} {Coh.GoblinDiceHeal}");

        // ---- onBattlefieldEnter / onBattlefieldLeave: the framework's, then the char's DEATH listener
        {
            var w = new CohWorld();
            var p = new BattlefieldPlayerFake { CharId = 7001, ZoneId = z, Callbacks = w.Queues };
            w.Bf.InitiatorValue = (7001, "Init");
            w.E.Texts[$"{z}:ENTERING_THE_BATTLEFIELD_FOR"] = 1;
            w.E.Texts[$"{z}:MEMBERS_OF_YOUR_PARTY"] = 2;
            w.E.Texts[$"{z}:TIME_LIMIT_FOR_THIS_BATTLE_IS"] = 3;
            var cs = w.S;
            var wq = new BattlefieldContextFake(cs) { Engine = w.E, Battlefield = w.Bf, Player = p };
            cs.OnBattlefieldEnter(wq);
            string enter = string.Join(",", p.Calls.Where(c => c.Contains("AddListener") || c.Contains("Message")));
            string listeners =
                string.Join(",", w.Queues.Listeners.Select(x => $"{x.Event}/{x.Identifier}/{x.Callback}"));
            p.Calls.Clear();
            cs.OnBattlefieldLeave(wq, 1);
            Check("c3 C3c: onBattlefieldEnter runs the framework's (its three messages) and then adds the char's " +
                  "DEATH " +
                  "listener CENTURY_HARDSHIP_DEATH; onBattlefieldLeave removes it",
                  "Player.MessageSpecial(1,0,0),Player.MessageSpecial(2,0,0,0,6),Player.MessageSpecial(3,0,0,0,30)," +
                  "Player.AddListener(DEATH,CENTURY_HARDSHIP_DEATH,playerDeath)|DEATH/CENTURY_HARDSHIP_DEATH/" +
                  "playerDeath|0",
                  $"{enter}|{listeners}|{w.Queues.Listeners.Count}");
        }

        // ---- the DEATH listener's body: each arm alone (no trace has a killer: a char's DEATH gets none)
        string Death(Func<CohWorld, (object Killer, IBattlefieldView Bf)> setup, int area = 1)
        {
            var w = new CohWorld(area).All();
            var p = new BattlefieldPlayerFake { CharId = 7001, ZoneId = z, Callbacks = w.Queues };
            var (killer, bf) = setup(w);
            p.AsEntity = new BattlefieldMobFake { Id = 7001, IsMob = false, Battlefield = bf };
            p.AddListener("DEATH", "CENTURY_HARDSHIP_DEATH", w.S.Data.Callbacks.First(c => c.Name == "playerDeath"));
            w.ClearCalls();
            int ran;
            try
            {
                ran = killer == null ? w.Queues.Trigger(p, "DEATH") : w.Queues.Trigger(p, "DEATH", killer);
            }
            catch (Exception e)
            {
                return "threw " + e.GetType().Name;
            }
            return ran + ":" + string.Join(",", w.Mobs.Values.Where(m => m.Calls.Count > 0)
                .Select(m => m.Id + "=" + string.Join(";", m.Calls)));
        }
        Check("c3 C3c: the DEATH listener does nothing with no killer (every trace's case), a dead killer, or a " +
              "char in no battlefield",
              "1:|1:|1:",
              string.Join("|", Death(w => (null, w.Bf)), Death(w => { w.M(4).HP = 0; return (w.M(4), w.Bf); }),
                          Death(w => (w.M(4), null))));
        Check("c3 C3c: a killer gives its death line: Bugbby +9, Movamuq +6, Chekochuk +6, Trikotrak +6, Swipostik " +
              "+5; " +
              "the id less the area's offset ((area - 1) * 5), so area 2's Bugbby (2009) too",
              "1:2004=Mob.MessageText(7868)|1:2000=Mob.MessageText(7834)|1:2001=Mob.MessageText(7842)|" +
              "1:2002=Mob.MessageText(7850)|1:2003=Mob.MessageText(7857)|1:2009=Mob.MessageText(7868)",
              string.Join("|", Death(w => (w.M(4), w.Bf)), Death(w => (w.M(0), w.Bf)), Death(w => (w.M(1), w.Bf)),
                          Death(w => (w.M(2), w.Bf)), Death(w => (w.M(3), w.Bf)),
                          Death(w => (w.M(4), w.Bf), 2)));
        Check("c3 C3c: a killer not in mobDialogue (area 1's Bugbby read in area 2: 2004 - 5 is no key) gives nothing",
              "1:", Death(w =>
              {
                  var k = w.Mobs[CohWorld.Mova + 4];
                  k.IsSpawned = true;
                  k.HP = 1000;
                  return (k, w.Bf);
              }, 2));

        // ---- onBattlefieldTick: Bugbby gone or dead returns before anything (no draw, no restore, no trigger)
        {
            var w = new CohWorld();
            w.Bf.LocalVars["triggerMoblinCommand"] = CohWorld.Mova;
            w.Alive(0).LocalVars["inCommandSequence"] = 500;
            w.ClearCalls();
            w.Tick();
            string dead = $"{w.Calls(0)}/{w.Draws()}/{w.V("triggerMoblinCommand")}";
            w.Alive(4).HP = 0;
            w.ClearCalls();
            w.Tick();
            string dead2 = $"{w.Calls(0)}/{w.Draws()}/{w.V("triggerMoblinCommand")}";
            w.E.MobsById.Remove(CohWorld.Mova + 4);
            w.ClearCalls();
            w.Tick();
            Check("c3 C3c: a tick returns at once when Bugbby is not spawned, dead (HP 0) or nil: the Moblin's " +
                  "restore, " +
                  "the trigger (left set) and every draw skipped",
                  $"//{CohWorld.Mova}|//{CohWorld.Mova}|//{CohWorld.Mova}",
                  $"{dead}|{dead2}|{w.Calls(0)}/{w.Draws()}/{w.V("triggerMoblinCommand")}");
        }

        // ---- the restores: a Moblin's and Bugbby's, each part alone
        {
            string Restore(int offset, uint seq, uint now, bool alive = true)
            {
                var w = new CohWorld().All();
                w.E.SystemTime = now;
                w.Bf.LocalVars["commandSequence"] = 1;
                w.M(offset).LocalVars["inCommandSequence"] = seq;
                w.M(offset).Behavior = 1025;
                if (!alive) w.M(offset).HP = 0;
                w.ClearCalls();
                w.Tick();
                return w.Calls(offset);
            }
            Check("c3 C3c: a Moblin's restore (NO_MOVE 0, auto-attack and magic on, NO_TURN cleared, the var 0) runs " +
                  "when its inCommandSequence is set and GetSystemTime has reached it; not before, not at 0, not dead",
                  "Mob.SetMobMod(65,0),Mob.SetAutoAttackEnabled(True),Mob.SetMagicCastingEnabled(True)," +
                  "Mob.SetBehavior(1),Mob.SetLocalVar(inCommandSequence,0)|" +
                  "Mob.SetMobMod(65,0),Mob.SetAutoAttackEnabled(True),Mob.SetMagicCastingEnabled(True)," +
                  "Mob.SetBehavior(1),Mob.SetLocalVar(inCommandSequence,0)|||",
                  string.Join("|", Restore(2, 1000, 1000), Restore(3, 999, 1000), Restore(1, 1001, 1000),
                              Restore(0, 0, 1000), Restore(0, 999, 1000, alive: false)));
            Check("c3 C3c: the restore covers the four Moblins (offsets 0 to 3), not Bugbby's magic",
                  "5|5|5|5|4",
                  string.Join("|", Enumerable.Range(0, 5).Select(o => Restore(o, 999, 1000).Split("Mob.").Length - 1)));
            Check("c3 C3c: Bugbby's restore (NO_MOVE 0, auto-attack on, NO_TURN cleared, the var 0) at or past its " +
                  "time; not before, not at 0",
                  "Mob.SetMobMod(65,0),Mob.SetAutoAttackEnabled(True),Mob.SetBehavior(1)," +
                  "Mob.SetLocalVar(inCommandSequence,0)||",
                  string.Join("|", Restore(4, 1000, 1000), Restore(4, 1001, 1000), Restore(4, 0, 1000)));
        }

        // ---- the HP-threshold trigger: a live Moblin commands; a dead one does not; the var is cleared either way
        {
            var w = CohCommand(1, new[] { 1, 21, 0 });
            string live = $"{w.Calls(1).Contains("MessageText(7836)")}/{w.V("triggerMoblinCommand")}";
            var d = new CohWorld().All();
            d.Bf.LocalVars["commandSequence"] = 1;
            d.Bf.LocalVars["triggerMoblinCommand"] = CohWorld.Mova + 1;
            d.M(1).HP = 0;
            d.ClearCalls();
            d.Tick();
            Check("c3 C3c: triggerMoblinCommand: a live Moblin commands (Chekochuk's 7836), a dead one does not; the " +
                  "var goes back to 0 either way",
                  "True/0|/0/", $"{live}|{d.Calls(1)}/{d.V("triggerMoblinCommand")}/{d.Draws()}");
        }

        // ---- the party-wipe reset: Bugbby not engaged with the cycle on
        {
            string Reset(bool engaged, int sequence, params int[] rolls)
            {
                var w = new CohWorld().All();
                w.Rolls(rolls);
                w.M(4).IsEngaged = engaged;
                w.M(4).HPP = 50;
                w.Bf.LocalVars["commandSequence"] = sequence;
                w.Bf.LocalVars["nextCommandTime"] = 900;
                w.ClearCalls();
                w.Tick();
                return $"{w.V("commandSequence")},{w.V("nextCommandTime")},{w.Draws()},{w.Calls(0).Length > 0}";
            }
            Check("c3 C3c: the reset (commandSequence and nextCommandTime 0, then return) only when Bugbby is not " +
                  "engaged and the cycle is on; engaged, the timer commands; the cycle off, the 90% arm starts it " +
                  "(no " +
                  "command: Bugbby is not engaged)",
                  "0,0,,False|1,1060,RandomInt(1,4),RandomInt(1,100),RandomInt(1,100),RandomInt(0,5)," +
                  "RandomInt(30,60),True|1,1030,RandomInt(30,60),False",
                  string.Join("|", Reset(false, 1), Reset(true, 1, 1, 1, 21, 0, 60), Reset(false, 0)));
        }

        // ---- the 90% threshold: both sides, and the cycle already on
        {
            string Start(int hpp, int sequence = 0)
            {
                var w = new CohWorld().All();
                w.M(4).HPP = hpp;
                w.Bf.LocalVars["commandSequence"] = sequence;
                w.Rolls(45);
                w.ClearCalls();
                w.Tick();
                return $"{w.V("commandSequence")},{w.V("nextCommandTime")},{w.Draws()}";
            }
            Check("c3 C3c: Bugbby below 90% starts the cycle (commandSequence 1, nextCommandTime GetSystemTime + " +
                  "randomInt(30, 60)); at 90 not; with the cycle on not again",
                  "1,1045,RandomInt(30,60)|0,0,|1,0,",
                  string.Join("|", Start(89), Start(90), Start(50, 1)));
        }

        // ---- the global timer: each part alone, the pick among the live Moblins, the next time
        {
            string Timer(uint now, double next, bool engaged, int[] dead, int[] rolls)
            {
                var w = new CohWorld().All();
                w.E.SystemTime = now;
                w.M(4).IsEngaged = engaged;
                w.Bf.LocalVars["commandSequence"] = engaged ? 1 : 0;
                w.Bf.LocalVars["nextCommandTime"] = next;
                foreach (var o in dead) w.M(o).HP = 0;
                w.Rolls(rolls);
                w.ClearCalls();
                w.Tick();
                string who = string.Join("", Enumerable.Range(0, 4).Where(o => w.Calls(o).Contains("MessageText")));
                return $"{who},{w.V("nextCommandTime")}";
            }
            Check("c3 C3c: the timer commands when nextCommandTime is set and reached and Bugbby is engaged: the " +
                  "pick " +
                  "is randomInt(1, #alive) among the live Moblins in id order, then next = now + randomInt(30, 60)",
                  "0,1031|3,1060|2,1040|1,1035",
                  string.Join("|", Timer(1000, 1000, true, new int[0], new[] { 1, 1, 21, 0, 31 }),
                              Timer(1000, 999, true, new int[0], new[] { 4, 1, 21, 0, 60 }),
                              Timer(1000, 1000, true, new[] { 0, 1 }, new[] { 1, 1, 21, 0, 40 }),
                              Timer(1000, 1000, true, new[] { 0, 2, 3 }, new[] { 1, 1, 21, 0, 35 })));
            Check("c3 C3c: the timer does nothing before its time, at 0, with Bugbby not engaged (the reset off: " +
                  "cycle 0), or with every Moblin dead (next unchanged)",
                  ",1001|,0|,1000|,1000",
                  string.Join("|", Timer(1000, 1001, true, new int[0], new int[0]),
                              Timer(1000, 0, true, new int[0], new int[0]),
                              Timer(1000, 1000, false, new int[0], new int[0]),
                              Timer(1000, 1000, true, new[] { 0, 1, 2, 3 }, new int[0])));
        }

        // ---- handleMoblinCommand: the command, its type by the roll's bounds, Bugbby's answer
        {
            var w = CohCommand(0, new[] { 60, 21, 3 });
            Check("c3 C3c: a command: both inCommandSequence GetSystemTime + 3; the Moblin looks at Bugbby, " +
                  "auto-attack " +
                  "and magic off, NO_MOVE, NO_TURN added, its line (base + type - 1); Bugbby looks at it, " +
                  "auto-attack " +
                  "off, NO_MOVE, NO_TURN added, accepts (BUGBBY + randomInt(0, 5)); type 1 with a target: Bugbby " +
                  "disengages and takes the Moblin's target",
                  "Mob.SetLocalVar(inCommandSequence,1003),Mob.LookAt(4,5,6),Mob.SetAutoAttackEnabled(False)," +
                  "Mob.SetMagicCastingEnabled(False),Mob.SetMobMod(65,1),Mob.SetBehavior(1025),Mob.MessageText(7828)|" +
                  "Mob.SetLocalVar(inCommandSequence,1003),Mob.LookAt(0,1,2),Mob.SetAutoAttackEnabled(False)," +
                  "Mob.SetMobMod(65,1),Mob.SetBehavior(1026),Mob.MessageText(7862),Mob.Disengage()," +
                  "Mob.UpdateEnmity(7001)|RandomInt(1,100),RandomInt(1,100),RandomInt(0,5)|",
                  $"{w.Calls(0)}|{w.Calls(4)}|{w.Draws()}|{w.Pending()}");
            Check("c3 C3c: type 1 with no target: Bugbby accepts, nothing more",
                  "Mob.MessageText(7859)|",
                  string.Join(",", CohCommand(0, new[] { 1, 21, 0 }, target: false).M(4).Calls
                      .Where(c => c.Contains("MessageText") || c.Contains("Disengage") || c.Contains("Enmity"))) +
                  "|" + CohCommand(0, new[] { 1, 21, 0 }, target: false).Pending());
            string Type(int roll) => CohCommand(2, new[] { roll, 21, 0 }).Calls(2).Split(',').Last();
            Check("c3 C3c: the command type by the roll: 1..60 type 1, 61..90 type 2, 91..100 type 3 (Trikotrak's " +
                  "7844, 7845, 7846)",
                  "Mob.MessageText(7844)|Mob.MessageText(7845)|Mob.MessageText(7845)|Mob.MessageText(7846)|" +
                  "Mob.MessageText(7846)",
                  string.Join("|", Type(60), Type(61), Type(90), Type(91), Type(100)));
            Check("c3 C3c: type 2 accepted arms Bugbby's 2000 ms Heavy Whisk; type 3 accepted its 2000 ms Mighty " +
                  "Strikes",
                  $"{CohWorld.Mova + 4}:heavyWhisk@2000|{CohWorld.Mova + 4}:mightyStrikes@2000",
                  CohCommand(3, new[] { 61, 21, 0 }).Pending() + "|" + CohCommand(3, new[] { 91, 21, 0 }).Pending());
            Check("c3 C3c: Bugbby refuses at randomInt(1, 100) <= 20: a 1000 ms timer of its line (BUGBBY + 6 + " +
                  "randomInt(0, 1)) and the Moblin's 2000 ms insult (its base, its refusal skill); at 21 it accepts",
                  $"{CohWorld.Mova + 4}:bugbearRefusal@1000(7866),{CohWorld.Mova + 1}:moblinInsult@2000(7836;1099)|" +
                  $"{CohWorld.Mova + 4}:bugbearRefusal@1000(7865),{CohWorld.Mova + 1}:moblinInsult@2000(7836;1099)|",
                  CohCommand(1, new[] { 61, 20, 1 }).Pending() + "|" + CohCommand(1, new[] { 61, 1, 0 }).Pending() +
                  "|" + CohCommand(1, new[] { 1, 21, 0 }).Pending());
            Check("c3 C3c: type 3 with Mighty Strikes used is refused whatever the roll (forceRefusal); type 2 with " +
                  "it " +
                  "used and type 3 without are accepted; the refusal roll is drawn first in each",
                  $"{CohWorld.Mova + 4}:bugbearRefusal@1000(7865),{CohWorld.Mova + 3}:moblinInsult@2000(7852;591)" +
                  $"/RandomInt(1,100),RandomInt(1,100),RandomInt(0,1)|{CohWorld.Mova + 4}:heavyWhisk@2000|" +
                  $"{CohWorld.Mova + 4}:mightyStrikes@2000",
                  CohCommand(3, new[] { 91, 21, 0 }, msUsed: true).Pending() + "/" +
                  CohCommand(3, new[] { 91, 21, 0 }, msUsed: true).Draws() + "|" +
                  CohCommand(3, new[] { 61, 21, 0 }, msUsed: true).Pending() + "|" +
                  CohCommand(3, new[] { 91, 21, 0 }).Pending());
            Check("c3 C3c: area 2: the Moblin's base mob is its id less 5 (Movamuq +5's line 7828), Bugbby +5 answers",
                  "Mob.MessageText(7828)|Mob.MessageText(7859)",
                  CohCommand(0, new[] { 1, 21, 0 }, area: 2).Calls(0).Split(',').Last() + "|" +
                  CohCommand(0, new[] { 1, 21, 0 }, area: 2).Calls(4).Split(',')
                      .First(c => c.Contains("MessageText")));
        }

        // ---- the callbacks fired
        {
            string Fire(int moblin, int[] rolls, long at, params int[] after)
            {
                var w = CohCommand(moblin, rolls);
                w.ClearCalls();
                w.Rolls(after);
                w.Queues.Tick(at);
                return string.Join(",", w.Mobs.Values.Where(m => m.Calls.Count > 0)
                           .Select(m => m.Id + "=" + string.Join(";", m.Calls))) + "/" + w.Draws();
            }
            // the Moblin's calls alone when the 2000 ms timers fire
            string Insult(int moblin, params int[] after)
            {
                var w = CohCommand(moblin, new[] { 1, 20, 0 });
                w.ClearCalls();
                w.Rolls(after);
                w.Queues.Tick(2001);
                return $"{w.M(moblin).Id}={string.Join(";", w.M(moblin).Calls)}/{w.Draws()}";
            }
            Check("c3 C3c: the refusal fires: Bugbby's line past 1000 ms, then past 2000 ms the Moblin's insult " +
                  "(base " +
                  "+ 3) and its skill: Movamuq Frypan 1081, Trikotrak Smokebomb 1082, Swipostik Bomb Toss 591 with " +
                  "no " +
                  "draw, Chekochuk Goblin Dice 1099 + randomInt(0, 10)",
                  "2004=Mob.MessageText(7866)/|2000=Mob.MessageText(7831);Mob.UseMobAbility(1081)/|" +
                  "2002=Mob.MessageText(7847);Mob.UseMobAbility(1082)/|" +
                  "2003=Mob.MessageText(7855);Mob.UseMobAbility(591)/|" +
                  "2001=Mob.MessageText(7839);Mob.UseMobAbility(1106)/RandomInt(0,10)",
                  string.Join("|", Fire(0, new[] { 1, 20, 1 }, 1001), Insult(0), Insult(2), Insult(3),
                              Insult(1, 7)));
            Check("c3 C3c: the timers fire strictly past their delay: nothing at 1000 and 2000 ms",
                  "/|2004=Mob.MessageText(7865)/",
                  Fire(0, new[] { 1, 20, 0 }, 1000) + "|" + Fire(0, new[] { 1, 20, 0 }, 2000));
            Check("c3 C3c: Heavy Whisk fires as Bugbby's 358; Mighty Strikes as its 688, then usedMightyStrikes 1",
                  "2004=Mob.UseMobAbility(358)/|2004=Mob.UseMobAbility(688);Mob.SetLocalVar(usedMightyStrikes,1)/",
                  Fire(0, new[] { 61, 21, 0 }, 2001) + "|" + Fire(0, new[] { 91, 21, 0 }, 2001));
            // the dead-Bugbby reaction no tick arms (the waiver's callback), armed by hand: the Moblin's base + 4
            var w = new CohWorld().All();
            w.M(2).Timer(2000, w.S.Data.Callbacks.First(c => c.Name == "deadBugbearReaction"), CohWorld.TrikText);
            w.ClearCalls();
            w.Queues.Tick(2001);
            Check("c3 C3c: the dead-Bugbby reaction (armed by hand: no tick reaches its arm) says the Moblin's base " +
                  "+ 4",
                  "Mob.MessageText(7848)", w.Calls(2));
        }

        // ---- the dead-Bugbby arm is dead code: a trigger with Bugbby dead returns before handleMoblinCommand
        {
            var w = new CohWorld().All();
            w.M(4).HP = 0;
            w.Bf.LocalVars["triggerMoblinCommand"] = CohWorld.Mova;
            w.Bf.LocalVars["nextCommandTime"] = 1;
            w.ClearCalls();
            w.Tick();
            Check("c3 C3c: with Bugbby dead neither caller of handleMoblinCommand runs (the reaction's arm unreached)",
                  "|", $"{w.Calls(0)}|{w.Pending()}");
        }

        // ---- the dead-Bugbby arm itself, called as the Lua's local is (by reflection: no tick reaches it)
        {
            var w = new CohWorld().All();
            w.M(4).HP = 0;
            w.Rolls(1);
            w.ClearCalls();
            var hm = typeof(Coh).GetMethod("HandleMoblinCommand",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            hm!.Invoke(w.S, new object[] { w.Q(), w.M(2), w.M(4), 0 });
            Check("c3 C3c: handleMoblinCommand with Bugbby dead (called directly): the Moblin's command, then the " +
                  "reaction armed on the Moblin for 2000 ms with its base, Bugbby given no command",
                  "Mob.SetLocalVar(inCommandSequence,1003),Mob.LookAt(4,5,6),Mob.SetAutoAttackEnabled(False)," +
                  "Mob.SetMagicCastingEnabled(False),Mob.SetMobMod(65,1),Mob.SetBehavior(1025),Mob.MessageText(7844)," +
                  "Mob.Timer(2000,deadBugbearReaction)|Mob.SetLocalVar(inCommandSequence,1003)|" +
                  "2002:deadBugbearReaction@2000(7844)",
                  $"{w.Calls(2)}|{w.Calls(4)}|{w.Pending()}");
        }
    }
}
