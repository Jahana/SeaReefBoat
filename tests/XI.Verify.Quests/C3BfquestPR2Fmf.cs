// XI.Verify.Quests -- quest rework C3 PR2: Full Moon Fountain's Waking the Beast on C#, the first battlefield on the
// callback seam. Its data against the Lua file's text, entryRequirement's parts, setupBattlefield, the phases of
// onBattlefieldTick and each part of their conditions, the five callbacks (armed, fired, captured), the kept enmity
// list, the coordinated two-hours and the loot (handleLoot's kept next(players) bug), over the fakes: the arms no
// trace decides alone.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using Fmf = XI.QuestContent.Battlefields.FullMoonFountain.WakingTheBeast;

internal static partial class Program
{
    // the FMF world over fakes: the 33 mobs of the three areas (area 1's main Carbuncle at 1000; offsets 0 to 4 the
    // Carbuncles, 5 to 10 the avatars), the initiator (7001) in the battlefield, the engine's queues
    sealed class FmfWorld
    {
        public const int Main = 1000, Taunt = 7777;
        public static readonly string[] Avatars =
            { "Garuda_Prime", "Ifrit_Prime", "Leviathan_Prime", "Ramuh_Prime", "Shiva_Prime", "Titan_Prime" };
        public readonly Fmf S = new Fmf();
        public readonly BattlefieldEngineFake E = new BattlefieldEngineFake();
        public readonly BattlefieldViewFake Bf = new BattlefieldViewFake { Id = 226, Area = 1 };
        public readonly BattlefieldPlayerFake P = new BattlefieldPlayerFake { CharId = 7001 };
        public readonly BattlefieldCallbacksFake Queues;
        public readonly Dictionary<int, BattlefieldMobFake> Mobs = new Dictionary<int, BattlefieldMobFake>();

        public FmfWorld()
        {
            Queues = new BattlefieldCallbacksFake(S, E);
            E.Callbacks = Queues;
            E.ZoneMobIds[$"{QC.Zone.FullMoonFountain}:CARBUNCLE_PRIME"] = Main;
            E.Texts[$"{QC.Zone.FullMoonFountain}:LEAVE_THE_AFFAIRS_OF_GODS_TO"] = Taunt;
            for (int i = 0; i < 33; i++)
            {
                int off = i % 11;
                var m = new BattlefieldMobFake
                {
                    Id = Main + i, MaxHP = 1000, Name = off <= 4 ? "Carbuncle_Prime" : Avatars[off - 5],
                    Battlefield = Bf,
                };
                Mobs[Main + i] = m;
                E.MobsById[Main + i] = m;
                E.MobById(Main + i);
            }
            Bf.InitiatorValue = (7001, "Init");
            Bf.PlayerList.Add(P);
            E.PlayersById[7001] = P;
            E.SystemTime = 1000;
        }

        public BattlefieldMobFake M(int offset) => Mobs[Main + offset];
        public BattlefieldContextFake Q() => new BattlefieldContextFake(S) { Engine = E, Battlefield = Bf };
        // a tick; an exception other than the port's own (InvalidOperationException, the Lua's raise) is kept for
        // the last check, so a broken arm fails a check rather than ending the run
        public void Tick()
        {
            try
            {
                S.OnBattlefieldTick(Q(), 0);
            }
            catch (Exception e) when (e is not InvalidOperationException)
            {
                fmfThrew_.Add(e.GetType().Name);
            }
        }
        // a mob alive at hpp, as SpawnMob leaves it, then hurt
        public BattlefieldMobFake Alive(int offset, int hpp)
        {
            var m = M(offset);
            m.IsSpawned = true;
            m.HP = hpp * 10;
            m.HPP = hpp;
            return m;
        }
        public string V(string name) => Bf.LocalVar(name).ToString(System.Globalization.CultureInfo.InvariantCulture);
        public string Calls(int offset) => string.Join(",", M(offset).Calls);
        public string BfCalls() => string.Join(",", Bf.Calls);
        public string ECalls() => string.Join(",", E.Calls);
        // the phase change a tick made: phase/numToSpawn/typeToSpawn/spawnTriggerTime
        public string Phase() => $"{V("phase")}/{V("numToSpawn")}/{V("typeToSpawn")}/{V("spawnTriggerTime")}";
    }

    // the exceptions a tick threw that the Lua would not raise
    static readonly List<string> fmfThrew_ = new List<string>();

    static void RunC3PR2Fmf()
    {
        fmfThrew_.Clear();
        Console.WriteLine("\n== c3 PR2 fmf: Full Moon Fountain's Waking the Beast on C# ==");
        var s = new Fmf();

        // the data, against the Lua file's text
        Check("c3 PR2 fmf: fields (no experience lost, 18 players, 30 minutes, index 2, MS_Entrance, Moon_Spiral, " +
              "the other-areas quest WAKING_THE_BEAST, repeatable, two groups)",
              $"{QC.Zone.FullMoonFountain} {QC.Battlefield.WakingTheBeastFullmoon} False False 18 - 1800 2 0 " +
              "MS_Entrance Moon_Spiral - OtherAreas WAKING_THE_BEAST - 0 True 2", C3Q3aFields(s));
        Check("c3 PR2 fmf: groups: the main Carbuncle of each area (+0, +11, +22), superlinked, spawned; the ten " +
              "after each, not spawned, superlinked, with a death callback",
              "CARBUNCLE_PRIME,CARBUNCLE_PRIME+11,CARBUNCLE_PRIME+22 " +
              string.Join(",", new[] { 1, 12, 23 }.Select(a => string.Join("/",
                  Enumerable.Range(a, 10).Select(o => $"CARBUNCLE_PRIME+{o}")))) +
              " 1 1 True False False True False False",
              C3Q3aMobs(s.Data.Groups[0]) + " " + C3Q3aMobs(s.Data.Groups[1]) + " " +
              s.Data.Groups[0].SuperlinkGroup + " " + s.Data.Groups[1].SuperlinkGroup + " " +
              s.Data.Groups[0].Spawned.GetValueOrDefault(true) + " " +
              s.Data.Groups[1].Spawned.GetValueOrDefault(true) +
              " " + (s.Data.Groups[0].Death != null) + " " + (s.Data.Groups[1].Death != null) + " " +
              (s.Data.Groups[0].AllDeath != null) + " " + (s.Data.Groups[1].AllDeath != null));
        Check("c3 PR2 fmf: the loot table and the two-hours by name, as the Lua's",
              $"{QC.Item.CarbunclesCuffs},{QC.Item.IfritsBow},{QC.Item.ShivasShotel},{QC.Item.TitansBaselarde}," +
              $"{QC.Item.GarudasSickle},{QC.Item.LeviathansCouse},{QC.Item.RamuhsMace} " +
              "Carbuncle_Prime=912,Garuda_Prime=875,Ifrit_Prime=848,Leviathan_Prime=866,Ramuh_Prime=893," +
              "Shiva_Prime=884,Titan_Prime=857",
              string.Join(",", Fmf.Loot) + " " +
              string.Join(",", Fmf.AvatarTwoHours.Select(kv => $"{kv.Key}={kv.Value}")));
        Check("c3 PR2 fmf: overrides entryRequirement, setupBattlefield and onBattlefieldTick only",
              "EntryRequirement,OnBattlefieldTick,SetupBattlefield", C3Q1Hooks(s));
        Check("c3 PR2 fmf: its five callbacks are coverage labels",
              "callback:avatarDeath,callback:avatarNormalAnimation,callback:avatarTaunt,callback:carbyDeath," +
              "callback:carbyGone",
              string.Join(",", s.CoverageLabels().Where(l => l.StartsWith("callback:"))));
        Check("c3 PR2 fmf: validates clean", "", string.Join("|", BattlefieldScriptValidator.Validate(s)));

        // entryRequirement: the registrant needs all seven key items; another char those or the quest completed
        int[] kis =
        {
            QC.KeyItem.EyeOfFlames, QC.KeyItem.EyeOfFrost, QC.KeyItem.EyeOfGales, QC.KeyItem.EyeOfStorms,
            QC.KeyItem.EyeOfTides, QC.KeyItem.EyeOfTremors, QC.KeyItem.RainbowResonator,
        };
        bool Entry(int missing, bool registrant, QuestStatus status, bool none = false)
        {
            var p = new BattlefieldPlayerFake { CharId = 1 };
            foreach (var k in kis) p.KeyItems[k] = !none && k != missing;
            p.QuestStatuses[$"{QuestLog.OtherAreas}/WAKING_THE_BEAST"] = status;
            var q = new BattlefieldContextFake(s) { Player = p };
            return s.EntryRequirement(q, registrant);
        }
        Check("c3 PR2 fmf: entryRequirement: the registrant with all seven; without each one alone, refused",
              "True False False False False False False False",
              Entry(0, true, QuestStatus.Accepted) + " " +
              string.Join(" ", kis.Select(k => Entry(k, true, QuestStatus.Accepted))));
        Check("c3 PR2 fmf: entryRequirement: the registrant completed without them refused; another char: completed " +
              "without them let in, accepted without one refused, accepted with all let in",
              "False True False True",
              $"{Entry(0, true, QuestStatus.Completed, none: true)} " +
              $"{Entry(0, false, QuestStatus.Completed, none: true)} " +
              $"{Entry(QC.KeyItem.EyeOfGales, false, QuestStatus.Accepted)} {Entry(0, false, QuestStatus.Accepted)}");

        // setupBattlefield: the shuffled order as one number, the avatars' vars reset, the main Carbuncle unkillable
        {
            var w = new FmfWorld();
            w.E.ShuffleResult = new[] { 3, 1, 2, 6, 5, 4 };
            w.M(7).LocalVars["wasDefeated"] = 1;
            w.M(7).LocalVars["phase"] = 3;
            w.S.SetupBattlefield(w.Q());
            Check("c3 PR2 fmf: setup: utils.shuffle({1..6}) concatenated (312654); each avatar's wasDefeated and " +
                  "phase set 0; the main Carbuncle unkillable; the Carbuncles' vars untouched",
                  "Shuffle(1,2,3,4,5,6)|312654|Mob.SetLocalVar(wasDefeated,0),Mob.SetLocalVar(phase,0)|0/0|" +
                  "Mob.SetUnkillable(True)|",
                  $"{w.ECalls()}|{w.V("avatarOrder")}|{w.Calls(7)}|{w.M(7).LocalVar("wasDefeated")}/" +
                  $"{w.M(7).LocalVar("phase")}|{w.Calls(0)}|{w.Calls(1)}");
            var w2 = new FmfWorld();
            w2.Bf.Area = 2;
            w2.S.SetupBattlefield(w2.Q());
            Check("c3 PR2 fmf: setup in area 2: its main Carbuncle (+11) and avatars (+16 to +21)",
                  "Mob.SetUnkillable(True)|Mob.SetLocalVar(wasDefeated,0),Mob.SetLocalVar(phase,0)|",
                  $"{w2.Calls(11)}|{w2.Calls(16)}|{w2.Calls(0)}");
            var w3 = new FmfWorld();
            w3.Bf.Area = 3;
            w3.S.SetupBattlefield(w3.Q());
            Check("c3 PR2 fmf: setup in area 3: its main Carbuncle (+22) and avatars (+27 to +32); area 2's untouched",
                  "Mob.SetUnkillable(True)|Mob.SetLocalVar(wasDefeated,0),Mob.SetLocalVar(phase,0)|" +
                  "Mob.SetLocalVar(wasDefeated,0),Mob.SetLocalVar(phase,0)||",
                  $"{w3.Calls(22)}|{w3.Calls(27)}|{w3.Calls(32)}|{w3.Calls(11)}|{w3.Calls(21)}");
            var w4 = new FmfWorld();
            w4.Bf.Area = 4;
            string area4;
            try
            {
                w4.Tick();
                area4 = "none";
            }
            catch (InvalidOperationException e)
            {
                area4 = e.Message;
            }
            Check("c3 PR2 fmf: an area past the three: mainCarbyIDbyArea[4] is nil, its arithmetic raises",
                  "attempt to perform arithmetic on a nil value (area 4)", area4);
        }

        // the phases: each condition, and each part of it alone
        string PhaseCase(double phase, int hpp, bool alive = true, bool noMain = false, double defeated = 0)
        {
            var w = new FmfWorld();
            w.Bf.LocalVars["phase"] = phase;
            w.Bf.LocalVars["avatarsDefeated"] = defeated;
            var main = w.Alive(0, hpp);
            if (!alive) main.AliveOverride = false;
            if (noMain) w.E.MobsById.Remove(FmfWorld.Main);
            w.Tick();
            return w.Phase();
        }
        Check("c3 PR2 fmf: phase 0 to 1 below 75%: at 74 (1 avatar in 5 s); at 75, dead, no main Carbuncle: no change",
              "1/1/1/1005 0/0/0/0 0/0/0/0 0/0/0/0",
              $"{PhaseCase(0, 74)} {PhaseCase(0, 75)} {PhaseCase(0, 74, alive: false)} " +
              $"{PhaseCase(0, 74, noMain: true)}");
        Check("c3 PR2 fmf: phase 1 to 2 at exactly one avatar defeated (the Carbuncle); 0 or 2: no change",
              "2/1/2/1005 1/0/0/0 1/0/0/0",
              $"{PhaseCase(1, 100, defeated: 1)} {PhaseCase(1, 100, defeated: 0)} {PhaseCase(1, 100, defeated: 2)}");
        Check("c3 PR2 fmf: phase 2 to 3 below 50% (2 avatars); at 50, dead, no main Carbuncle: no change",
              "3/2/1/1005 2/0/0/0 2/0/0/0 2/0/0/0",
              $"{PhaseCase(2, 49)} {PhaseCase(2, 50)} {PhaseCase(2, 49, alive: false)} " +
              $"{PhaseCase(2, 49, noMain: true)}");
        Check("c3 PR2 fmf: phase 3 to 4 at exactly three defeated; 2 or 4: no change",
              "4/1/2/1005 3/0/0/0 3/0/0/0",
              $"{PhaseCase(3, 100, defeated: 3)} {PhaseCase(3, 100, defeated: 2)} {PhaseCase(3, 100, defeated: 4)}");
        Check("c3 PR2 fmf: phase 4 to 5 below 25% (3 avatars); at 25, dead, no main Carbuncle: no change",
              "5/3/1/1005 4/0/0/0 4/0/0/0 4/0/0/0",
              $"{PhaseCase(4, 24)} {PhaseCase(4, 25)} {PhaseCase(4, 24, alive: false)} " +
              $"{PhaseCase(4, 24, noMain: true)}");
        Check("c3 PR2 fmf: phase 5 to 6 at exactly six defeated (five Carbuncles); 5: no change; phase 6 stays",
              "6/5/2/1005 5/0/0/0 6/0/0/0",
              $"{PhaseCase(5, 100, defeated: 6)} {PhaseCase(5, 100, defeated: 5)} {PhaseCase(6, 1, defeated: 6)}");
        Check("c3 PR2 fmf: a phase change below its threshold only: phase 0 at 49 changes to 1, not further",
              "1/1/1/1005", PhaseCase(0, 49));

        // the phase change of a Carbuncle phase: the enmity list kept, its hp, the despawn and the 7 s timer
        {
            var w = new FmfWorld();
            var main = w.Alive(0, 70);
            main.IsEngaged = true;
            main.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = w.P.AsEntity = new BattlefieldMobFake
            {
                Id = 7001, IsMob = false, Battlefield = w.Bf,
            }, Ce = 30, Ve = 12 });
            w.Bf.LocalVars["lastCoordTwoHour"] = 5;
            w.Tick();
            Check("c3 PR2 fmf: despawnCarby: mainCarbyHP, no aggro or link, disengaged, untargetable, the despawn " +
                  "animation, a 7 s timer; lastCoordTwoHour reset",
                  "700|0|Mob.SetMobMod(67,1),Mob.SetMobMod(69,1),Mob.Disengage(),Mob.SetUntargetable(True)," +
                  "Mob.SetAnimation(2),Mob.Timer(7000,carbyGone)",
                  $"{w.V("mainCarbyHP")}|{w.V("lastCoordTwoHour")}|{w.Calls(0)}");
            main.Calls.Clear();
            w.Queues.Tick(7000);
            string at7000 = w.Calls(0);
            w.Queues.Tick(7001);
            Check("c3 PR2 fmf: carbyGone fires past 7 s (not at 7000): DISAPPEAR, hp 0, targetable again",
                  "|Mob.SetStatus(2),Mob.SetHP(0),Mob.SetUntargetable(False)", $"{at7000}|{w.Calls(0)}");

            // the next Carbuncle phase: the main one again at the kept hp, the kept list applied
            w.E.SystemTime = 1006;
            w.Bf.LocalVars["phase"] = 2;
            w.Bf.LocalVars["spawnTriggerTime"] = 1005;
            w.Bf.LocalVars["numToSpawn"] = 1;
            w.Bf.LocalVars["typeToSpawn"] = 2;
            main.Calls.Clear();
            w.Tick();
            Check("c3 PR2 fmf: spawnCarby: spawned, hp the kept 700, its DEATH listener, aggro and link back, the " +
                  "kept enmity list applied; the spawn vars reset",
                  "SpawnMob(1000)|Mob.SetHP(700),Mob.AddListener(DEATH,WTB_SET_DEATH_VARS,carbyDeath)," +
                  "Mob.SetMobMod(67,0),Mob.SetMobMod(69,0),Mob.AddEnmity(7001,1,0),Mob.UpdateEnmity(7001)|0/0/0",
                  $"{string.Join(",", w.E.Calls.Where(c => c.StartsWith("SpawnMob")))}|{w.Calls(0)}|" +
                  $"{w.V("spawnTriggerTime")}/{w.V("numToSpawn")}/{w.V("typeToSpawn")}");
            w.Queues.Trigger(main, "DEATH");
            Check("c3 PR2 fmf: carbyDeath sets the Carbuncle's wasDefeated", "1",
                  main.LocalVar("wasDefeated").ToString());
        }

        // the wait for a spawn: strictly past the trigger time; nothing else runs while it waits
        {
            var w = new FmfWorld();
            w.Alive(0, 10);
            w.Bf.LocalVars["spawnTriggerTime"] = 1000;
            w.Bf.LocalVars["numToSpawn"] = 1;
            w.Bf.LocalVars["typeToSpawn"] = 2;
            w.Tick();
            Check("c3 PR2 fmf: a pending spawn at its trigger time (not past it) waits; no phase check runs",
                  "0/1/2/1000|", $"{w.Phase()}|{string.Join(",", w.E.Calls.Where(c => c.StartsWith("SpawnMob")))}");
        }
        string SpawnCase(int type, int num, bool noMain = false)
        {
            var w = new FmfWorld();
            w.E.SystemTime = 1001;
            w.Alive(0, 50);
            w.M(0).SetUnkillable(true);
            w.M(0).Calls.Clear();
            w.Bf.LocalVars["avatarOrder"] = 654321;
            w.Bf.LocalVars["spawnTriggerTime"] = 1000;
            w.Bf.LocalVars["numToSpawn"] = num;
            w.Bf.LocalVars["typeToSpawn"] = type;
            if (noMain) w.E.MobsById.Remove(FmfWorld.Main);
            w.Tick();
            return string.Join(",", w.E.Calls.Where(c => c.StartsWith("SpawnMob"))) + "|" +
                   string.Join(",", w.M(0).Calls.Where(c => c.Contains("Unkillable"))) + "|" + w.Phase();
        }
        Check("c3 PR2 fmf: the five Carbuncles: +0 to +4 spawned, the main one killable; one: the main alone",
              "SpawnMob(1000),SpawnMob(1001),SpawnMob(1002),SpawnMob(1003),SpawnMob(1004)|Mob.SetUnkillable(False)|" +
              "0/0/0/0 SpawnMob(1000)||0/0/0/0", $"{SpawnCase(2, 5)} {SpawnCase(2, 1)}");
        Check("c3 PR2 fmf: avatars without a main Carbuncle: none spawned (its position is the main's), the vars " +
              "reset; a type of 0: nothing spawned, reset",
              "||0/0/0/0 ||0/0/0/0", $"{SpawnCase(1, 1, noMain: true)} {SpawnCase(0, 1)}");

        // spawnElementalAvatars: the order's last digits, the arms, the position, the taunt on the last one
        {
            var w = new FmfWorld();
            w.E.SystemTime = 1001;
            w.Alive(0, 50).Pos = (10, 20, 30, 64);
            w.Bf.LocalVars["phase"] = 3;
            w.Bf.LocalVars["avatarOrder"] = 312654;
            w.Bf.LocalVars["spawnTriggerTime"] = 1000;
            w.Bf.LocalVars["numToSpawn"] = 2;
            w.Bf.LocalVars["typeToSpawn"] = 1;
            // x then z offset of each, then the last one's taunt roll
            foreach (var r in new[] { -3, 3, 2, -1, 1 }) w.E.Rolls.Enqueue(r);
            w.Tick();
            Check("c3 PR2 fmf: two avatars: the order's last digit (4: +8), then the next (5: +9); each with the " +
                  "summoning animation before the spawn, its phase, its DEATH listener, the 3 s normal animation, " +
                  "its place by the main Carbuncle; the last one's taunt on a roll of 1; the order cut to 3126",
                  "Mob.SetSpawnAnimation(1),Mob.SetLocalVar(phase,3)," +
                  "Mob.AddListener(DEATH,WTB_SET_DEATH_VARS,avatarDeath),Mob.Queue(3000,avatarNormalAnimation)," +
                  "Mob.SetPos(7,20,33,64)|Mob.SetSpawnAnimation(1),Mob.SetLocalVar(phase,3)," +
                  "Mob.AddListener(DEATH,WTB_SET_DEATH_VARS,avatarDeath),Mob.Queue(3000,avatarNormalAnimation)," +
                  "Mob.SetPos(12,20,29,64),Mob.Queue(2000,avatarTaunt)|3126|" +
                  "SpawnMob(1008),SpawnMob(1009)|RandomInt(-3,3),RandomInt(-3,3),RandomInt(-3,3),RandomInt(-3,3)," +
                  "RandomInt(1,2)|0/0/0",
                  $"{w.Calls(8)}|{w.Calls(9)}|{w.V("avatarOrder")}|" +
                  string.Join(",", w.E.Calls.Where(c => c.StartsWith("SpawnMob"))) + "|" +
                  string.Join(",", w.E.Calls.Where(c => c.StartsWith("RandomInt"))) + "|" +
                  $"{w.V("spawnTriggerTime")}/{w.V("numToSpawn")}/{w.V("typeToSpawn")}");

            // the callbacks: the normal animation and the taunt (queued), the DEATH listener on the captured
            // battlefield
            w.M(8).Calls.Clear();
            w.M(9).Calls.Clear();
            w.Bf.PlayerList.Add(null);
            w.Queues.Tick(2000);
            string at2000 = $"{w.Calls(9)}|{string.Join(",", w.P.Calls.Where(c => c.Contains("ShowText")))}";
            w.Queues.Tick(2001);
            string at2001 = string.Join(",", w.P.Calls.Where(c => c.Contains("ShowText")));
            w.Queues.Tick(3001);
            Check("c3 PR2 fmf: the taunt past 2 s: each player of the avatar's battlefield shown its line (a nil " +
                  "skipped); the normal spawn animation past 3 s",
                  $"||Player.ShowText(1009,{FmfWorld.Taunt})|Mob.SetSpawnAnimation(0)|Mob.SetSpawnAnimation(0)",
                  $"{at2000}|{at2001}|{w.Calls(8)}|{w.Calls(9)}");
            var other = new BattlefieldViewFake { Id = 226, Area = 1 };
            w.M(8).Battlefield = other;
            w.Bf.LocalVars["avatarsDefeated"] = 2;
            w.Queues.Trigger(w.M(8), "DEATH");
            Check("c3 PR2 fmf: avatarDeath: the avatar's wasDefeated, avatarsDefeated + 1 on the battlefield " +
                  "captured at the spawn", "1|3|0", $"{w.M(8).LocalVar("wasDefeated")}|{w.V("avatarsDefeated")}|" +
                                       $"{other.LocalVar("avatarsDefeated")}");
        }
        {
            var w = new FmfWorld();
            w.E.SystemTime = 1001;
            w.Alive(0, 50);
            w.Bf.LocalVars["phase"] = 1;
            w.Bf.LocalVars["avatarOrder"] = 3;
            w.Bf.LocalVars["spawnTriggerTime"] = 1000;
            w.Bf.LocalVars["numToSpawn"] = 1;
            w.Bf.LocalVars["typeToSpawn"] = 1;
            foreach (var r in new[] { 0, 0, 2 }) w.E.Rolls.Enqueue(r);
            w.Tick();
            Check("c3 PR2 fmf: one avatar (digit 3: +7), a taunt roll of 2: no taunt; the order 0",
                  "SpawnMob(1007)|False|0", string.Join(",", w.E.Calls.Where(c => c.StartsWith("SpawnMob"))) + "|" +
                                            w.Calls(7).Contains("avatarTaunt") + "|" + w.V("avatarOrder"));
        }

        // applyEnmity: the entities still in the battlefield, the highest (ce + ve, the last of equals) the target
        {
            var w = new FmfWorld();
            var a = new BattlefieldMobFake { Id = 7001, IsMob = false, Battlefield = w.Bf };
            var b = new BattlefieldMobFake { Id = 7002, IsMob = false, Battlefield = w.Bf };
            var gone = new BattlefieldMobFake { Id = 7003, IsMob = false };
            var dead = w.Alive(5, 50);
            dead.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = a, Ce = 10, Ve = 5 });
            dead.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = gone, Ce = 100, Ve = 100 });
            dead.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = b, Ce = 15, Ve = 0 });
            dead.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = null, Ce = 500, Ve = 0 });
            // the list is kept by area: the death in area 2's battlefield is not area 1's
            var area2 = new BattlefieldViewFake { Id = 226, Area = 2 };
            w.S.Data.Groups[1].Death(new BattlefieldContextFake(w.S) { Engine = w.E, Battlefield = area2 }, dead, 1);
            w.E.SystemTime = 1001;
            w.Alive(0, 50);
            void SpawnMain()
            {
                w.Bf.LocalVars["spawnTriggerTime"] = 1000;
                w.Bf.LocalVars["numToSpawn"] = 1;
                w.Bf.LocalVars["typeToSpawn"] = 2;
                w.M(0).Calls.Clear();
                w.Tick();
            }
            SpawnMain();
            string area1Before = string.Join(",", w.M(0).Calls.Where(c => c.Contains("Enmity")));
            w.S.Data.Groups[1].Death(w.Q(), dead, 1);
            SpawnMain();
            Check("c3 PR2 fmf: the group's death keeps the dead mob's enmity list for its area (area 2's not applied " +
                  "in area 1); on the next spawn the two in the battlefield added, the later of equal totals (15) " +
                  "the target; one gone and a nil skipped",
                  "|Mob.AddEnmity(7001,1,0),Mob.AddEnmity(7002,1,0),Mob.UpdateEnmity(7002)",
                  area1Before + "|" + string.Join(",", w.M(0).Calls.Where(c => c.Contains("Enmity"))));
        }

        // the coordinated two-hours of an avatar phase: the first at a defeated avatar or one below 10%, then every
        // 3 minutes; only the phase's alive and engaged avatars use it
        string Avatar2h(Action<FmfWorld> set, double last = 0, uint now = 2000)
        {
            var w = new FmfWorld();
            w.E.SystemTime = now;
            w.Bf.LocalVars["phase"] = 3;
            w.Bf.LocalVars["avatarsDefeated"] = 1;
            w.Bf.LocalVars["lastCoordTwoHour"] = last;
            w.Alive(0, 100);
            // +6 alive and engaged in phase 3 (Ifrit), +7 alive not engaged, +8 alive engaged of phase 1
            var six = w.Alive(6, 50);
            six.IsEngaged = true;
            six.LocalVars["phase"] = 3;
            w.Alive(7, 50).LocalVars["phase"] = 3;
            var eight = w.Alive(8, 50);
            eight.IsEngaged = true;
            eight.LocalVars["phase"] = 1;
            set(w);
            w.Tick();
            return string.Join(",", Enumerable.Range(5, 6).SelectMany(o => w.M(o).Calls
                       .Where(c => c.StartsWith("Mob.UseMobAbility")).Select(c => $"{o}:{c}"))) + "|" +
                   w.V("lastCoordTwoHour");
        }
        Check("c3 PR2 fmf: avatar two-hours: a defeated avatar of the phase triggers (+6's Ifrit 848; +7 not " +
              "engaged, +8 of phase 1: no); dead not defeated, defeated of another phase: no trigger",
              "6:Mob.UseMobAbility(848)|2000 |0 |0",
              Avatar2h(w => { w.M(5).LocalVars["phase"] = 3; w.M(5).LocalVars["wasDefeated"] = 1; }) + " " +
              Avatar2h(w => w.M(5).LocalVars["phase"] = 3) + " " +
              Avatar2h(w => { w.M(5).LocalVars["phase"] = 1; w.M(5).LocalVars["wasDefeated"] = 1; }));
        Check("c3 PR2 fmf: avatar two-hours: alive below 10% triggers, at 10% not; a defeated one still alive (not " +
              "dead) not",
              "6:Mob.UseMobAbility(848)|2000 |0 |0",
              Avatar2h(w => w.Alive(9, 9).LocalVars["phase"] = 3) + " " +
              Avatar2h(w => w.Alive(9, 10).LocalVars["phase"] = 3) + " " +
              Avatar2h(w =>
              {
                  var m = w.Alive(9, 50);
                  m.LocalVars["phase"] = 3;
                  m.LocalVars["wasDefeated"] = 1;
              }));
        Check("c3 PR2 fmf: avatar two-hours again: 181 s after the last (not 180), with no new trigger",
              "|1000 6:Mob.UseMobAbility(848)|1181",
              Avatar2h(w => { }, last: 1000, now: 1180) + " " + Avatar2h(w => { }, last: 1000, now: 1181));
        Check("c3 PR2 fmf: an avatar named off the table: useMobAbility(nil)",
              "6:Mob.UseMobAbility()|2000",
              Avatar2h(w =>
              {
                  w.M(6).Name = "Odin";
                  w.Alive(9, 9).LocalVars["phase"] = 3;
              }));

        // phase 6: the Carbuncles' two-hours and the win
        string Final(Action<FmfWorld> set, double last = 0, uint now = 2000, int status = 0)
        {
            var w = new FmfWorld();
            w.E.SystemTime = now;
            w.Bf.LocalVars["phase"] = 6;
            w.Bf.LocalVars["lastCoordTwoHour"] = last;
            w.Bf.Status = status;
            for (int o = 0; o <= 4; o++) w.Alive(o, 50).IsEngaged = o != 2;
            set(w);
            w.Tick();
            return string.Join(",", Enumerable.Range(0, 5).SelectMany(o => w.M(o).Calls
                       .Where(c => c.StartsWith("Mob.UseMobAbility")).Select(c => $"{o}:{c}"))) + "|" +
                   w.V("lastCoordTwoHour") + "|" + w.Bf.Status + "|" +
                   string.Join(",", w.P.Calls.Where(c => c.Contains("AddTreasure")));
        }
        Check("c3 PR2 fmf: Carbuncle two-hours: one below 10% triggers on the engaged alive ones (912; +2 not " +
              "engaged); at 10% not; a defeated dead one triggers, a dead one not defeated not",
              "0:Mob.UseMobAbility(912),1:Mob.UseMobAbility(912),3:Mob.UseMobAbility(912),4:Mob.UseMobAbility(912)" +
              "|2000|0| |0|0| 0:Mob.UseMobAbility(912),1:Mob.UseMobAbility(912),3:Mob.UseMobAbility(912)|2000|0| " +
              "|0|0|",
              Final(w => w.Alive(4, 9).IsEngaged = true) + " " + Final(w => w.Alive(4, 10)) + " " +
              Final(w => { w.M(4).IsSpawned = false; w.M(4).LocalVars["wasDefeated"] = 1; }) + " " +
              Final(w => w.M(4).IsSpawned = false));
        Check("c3 PR2 fmf: Carbuncle two-hours again 181 s after the last (not 180)",
              "|1000|0| 0:Mob.UseMobAbility(912),1:Mob.UseMobAbility(912),3:Mob.UseMobAbility(912)," +
              "4:Mob.UseMobAbility(912)|1181|0|",
              Final(w => { }, last: 1000, now: 1180) + " " + Final(w => { }, last: 1000, now: 1181));
        Check("c3 PR2 fmf: all five dead: WON and the loot for the initiator (40, 20, 10 per cent: rolls 40, 21, " +
              "10 give the first and third; items 3 and 7); already WON: no loot again",
              $"|0|2|Player.AddTreasure({QC.Item.ShivasShotel},),Player.AddTreasure({QC.Item.RamuhsMace},) |0|2|",
              Final(w =>
              {
                  for (int o = 0; o <= 4; o++) w.M(o).IsSpawned = false;
                  foreach (var r in new[] { 40, 3, 21, 10, 7 }) w.E.Rolls.Enqueue(r);
              }) + " " +
              Final(w =>
              {
                  for (int o = 0; o <= 4; o++) w.M(o).IsSpawned = false;
                  foreach (var r in new[] { 1, 1, 1, 1, 1, 1 }) w.E.Rolls.Enqueue(r);
              }, status: 2));
        {
            var w = new FmfWorld();
            w.Bf.LocalVars["phase"] = 6;
            foreach (var r in new[] { 1, 4, 1, 5, 1, 6 }) w.E.Rolls.Enqueue(r);
            w.Tick();
            Check("c3 PR2 fmf: the loot rolls: 1 to 100 for each pool, then 1 to 7 for the item (math.randomInt)",
                  "RandomInt(1,100),RandomInt(1,7),RandomInt(1,100),RandomInt(1,7),RandomInt(1,100),RandomInt(1,7)",
                  string.Join(",", w.E.Calls.Where(c => c.StartsWith("RandomInt"))));
        }
        Check("c3 PR2 fmf: one Carbuncle alive: no win", "0",
              Final(w => { for (int o = 0; o <= 3; o++) w.M(o).IsSpawned = false; }).Split('|')[2]);

        // handleLoot's kept bug: next(players) is the first key (1), not a player
        string Loot(bool initiatorIn, bool players, params int[] rolls)
        {
            var w = new FmfWorld();
            w.Bf.LocalVars["phase"] = 6;
            if (!initiatorIn) w.Bf.InitiatorValue = (999, "Gone");
            if (!players) w.Bf.PlayerList.Clear();
            foreach (var r in rolls) w.E.Rolls.Enqueue(r);
            try
            {
                w.Tick();
            }
            catch (InvalidOperationException e)
            {
                return "threw:" + e.Message;
            }
            return $"{w.Bf.Status}:{w.E.Calls.Count(c => c.StartsWith("RandomInt"))}:" +
                   string.Join(",", w.P.Calls.Where(c => c.Contains("AddTreasure")));
        }
        Check("c3 PR2 fmf: loot with the initiator gone: rolls of no success give nothing; the first success indexes " +
              "next(players)'s number and raises (kept Lua bug); no players: no roll",
              "2:3: threw:attempt to index local 'playerToAddTreasure' (a number value) 2:0:",
              $"{Loot(false, true, 41, 21, 11)} {Loot(false, true, 41, 20)} {Loot(true, false)}");

        // the checks outside a phase change: an avatar phase checks its two-hours only, phase 6 both, others none
        {
            var w = new FmfWorld();
            w.Alive(0, 80);
            for (int o = 1; o <= 4; o++) w.M(o).IsSpawned = false;
            w.Bf.LocalVars["lastCoordTwoHour"] = 1;
            w.E.SystemTime = 5000;
            w.Tick();
            Check("c3 PR2 fmf: phase 0 above 75%: neither the two-hours (lastCoordTwoHour kept) nor the win",
                  "1|0", $"{w.V("lastCoordTwoHour")}|{w.Bf.Status}");
        }
        Check("c3 PR2 fmf: no tick threw anything the Lua would not raise", "", string.Join(",", fmfThrew_));
    }
}
