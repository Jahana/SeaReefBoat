// XI.Verify.Quests -- quest rework C3 N6: the three battlefields (the Throne Room's Where Two Paths Converge, Qu'Bia
// Arena's The Heir to the Light, the Full Moon Fountain's Moon Reading) on fakes, one check per arm no packet shows:
// the groups, the setup and its listener, the deaths, the cutscene finish's bail-out, spawns and ally.
using System;
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunC3N6BattlefieldArms()
    {
        RunC3N6WhereTwoPathsBf();
        RunC3N6HeirBf();
        RunC3N6MoonReadingBf();
    }

    // a scenario's record, or "threw: <type>" when a mutant throws
    static string N6Try(Func<string> run)
    {
        try
        {
            return run();
        }
        catch (Exception e)
        {
            return $"threw: {e.GetType().Name}";
        }
    }

    static string N6Spawned(bool? spawned) => spawned == null ? "null" : spawned.ToString();

    static string N6Ids(BattlefieldGroup g) =>
        g.AreaMobIds == null
            ? string.Join(",", g.Mobs)
            : string.Join(";", g.AreaMobIds.Select(a => string.Join(",", a.Select(x => x.Offset))));

    static void RunC3N6WhereTwoPathsBf()
    {
        var s = new QC.Battlefields.ThroneRoom.WhereTwoPathsConverge();
        var d = (BattlefieldMissionData)s.Data;
        int tr = QC.Zone.ThroneRoom;
        Check("c3 N6 where two paths bf: the Lua file's fields (no exp loss, MAX_LEVEL, entry and exit NPCs, the " +
              "mission's log and id, status 1, skip at 4, groups)",
              $"False MAX_LEVEL _4l1 _4l2,_4l3,_4l4 1 {QC.Mission.Bastok.WhereTwoPathsConverge} 1 4 3",
              $"{d.CanLoseExp} {d.LevelCapSetting} {string.Join(",", d.EntryNpcs)} {string.Join(",", d.ExitNpcs)} " +
              $"{(int?)d.MissionArea} {d.Mission} {d.RequiredMissionStatus} {d.SkipMissionStatus} {d.Groups.Count}");
        Check("c3 N6 where two paths bf: the groups: Zeid (setup, spawned); Zeid_2 (death, not spawned); " +
              "Shadow_of_Rage (no handler, not spawned)",
              "Zeid True False False null|Zeid_2 False True False False|Shadow_of_Rage False False False False",
              string.Join("|", d.Groups.Select(g =>
                  $"{N6Ids(g)} {g.Setup != null} {g.Death != null} {g.AllDeath != null} {N6Spawned(g.Spawned)}")));

        // phase 1's setup and its COMBAT_TICK listener
        (BattlefieldMobFake Zeid, BattlefieldEngineFake E, BattlefieldCallbacksFake Queues, BattlefieldViewFake V,
         BattlefieldPlayerFake P1, BattlefieldPlayerFake P2) World()
        {
            var e = new BattlefieldEngineFake();
            var queues = new BattlefieldCallbacksFake(s, e);
            e.Callbacks = queues;
            var v = new BattlefieldViewFake { Area = 1 };
            var p1 = new BattlefieldPlayerFake { CharId = 1 };
            var p2 = new BattlefieldPlayerFake { CharId = 2 };
            v.PlayerList.AddRange(new IBattlefieldPlayer[] { p1, p2 });
            var zeid = new BattlefieldMobFake
            {
                Id = 500, MaxHP = 100, HP = 100, HPP = 100, IsSpawned = true, Battlefield = v, Callbacks = queues,
            };
            e.MobsById[500] = zeid;
            return (zeid, e, queues, v, p1, p2);
        }
        string Setup(bool twice) => N6Try(() =>
        {
            var w = World();
            d.Groups[0].Setup(new BattlefieldContextFake(s) { Engine = w.E }, new IBattlefieldMob[] { w.Zeid });
            if (twice)
                d.Groups[0].Setup(new BattlefieldContextFake(s) { Engine = w.E }, new IBattlefieldMob[] { w.Zeid });
            return string.Join(",", w.Zeid.Calls) + "/" +
                   string.Join(",", w.Queues.Listeners.Select(x => $"{x.Event}:{x.Identifier}:{x.Callback.Name}"));
        });
        const string setupCalls = "Mob.SetUntargetable(False),Mob.SetUnkillable(True)," +
                                  "Mob.RemoveListener(ZEID_COMBAT_TICK),Mob.AddListener(COMBAT_TICK,ZEID_COMBAT_TICK," +
                                  "zeidCombatTick)";
        Check("c3 N6 where two paths bf: Zeid's setup: targetable, unkillable, the ZEID_COMBAT_TICK listener " +
              "removed then added (COMBAT_TICK, zeidCombatTick); twice: the calls twice, one listener",
              $"{setupCalls}/COMBAT_TICK:ZEID_COMBAT_TICK:zeidCombatTick|" +
              $"{setupCalls},{setupCalls}/COMBAT_TICK:ZEID_COMBAT_TICK:zeidCombatTick",
              $"{Setup(false)}|{Setup(true)}");

        // the listener fired on combat ticks at the given HP percentages
        string Tick(uint changing, params int[] hpps) => N6Try(() =>
        {
            var w = World();
            d.Groups[0].Setup(new BattlefieldContextFake(s) { Engine = w.E }, new IBattlefieldMob[] { w.Zeid });
            w.Zeid.Calls.Clear();
            if (changing != 0) w.Zeid.LocalVars["ChangingPhase"] = changing;
            int ran = 0;
            foreach (int hpp in hpps)
            {
                w.Zeid.HPP = hpp;
                ran += w.Queues.Trigger(w.Zeid, "COMBAT_TICK");
            }
            return $"{ran}/{string.Join(",", w.Zeid.Calls)}/{string.Join(",", w.P1.Calls)}/" +
                   $"{string.Join(",", w.P2.Calls)}/{string.Join(",", w.E.Calls)}";
        });
        const string change = "Mob.SetLocalVar(ChangingPhase,1),Mob.SetUntargetable(True),Mob.SetUntargetable(True)/" +
                              "Player.StartEvent(32004,3,3,1,3,3,3,3,3)/Player.StartEvent(32004,3,3,1,3,3,3,3,3)/" +
                              "DespawnMob(500),DespawnMob(500)";
        Check("c3 N6 where two paths bf: the COMBAT_TICK listener: below 70% (69) once: ChangingPhase 1, then per " +
              "player (two) Zeid untargetable, 32004 (3, 3, 1, 3, 3, 3, 3, 3) and the despawn; fired again at 50: " +
              "nothing more; at 70: nothing; ChangingPhase already 1 at 10: nothing",
              $"1/{change}|2/{change}|1////|1////",
              $"{Tick(0, 69)}|{Tick(0, 69, 50)}|{Tick(0, 70)}|{Tick(1, 10)}");

        // phase 2's death
        string Death() => N6Try(() =>
        {
            var v = new BattlefieldViewFake { Area = 2 };
            d.Groups[1].Death(new BattlefieldContextFake(s) { Battlefield = v }, null, 1);
            return string.Join(",", v.Calls);
        });
        Check("c3 N6 where two paths bf: Zeid_2's death: WON",
              "Battlefield.SetStatus(2)", Death());

        // onEventFinishBattlefield
        const int offset = 17453000;
        string Finish(int area, bool zeid2Up, bool zeid2Missing = false, bool zeid1Missing = false,
                      bool volker = true) => N6Try(() =>
        {
            var e = new BattlefieldEngineFake();
            e.ZoneMobIds[$"{tr}:{QC.Mob.ThroneRoom.ZEID_BCNM_OFFSET}"] = offset;
            int zeidId = offset + (area - 1) * 4;
            if (!zeid1Missing)
                e.MobsById[zeidId] = new BattlefieldMobFake { Id = zeidId, MaxHP = 100, HP = 50, HPP = 50 };
            BattlefieldMobFake zeid2 = null;
            if (!zeid2Missing)
            {
                zeid2 = new BattlefieldMobFake { Id = zeidId + 1, MaxHP = 999, HP = 999 };
                if (zeid2Up) zeid2.SpawnInWorld();
                e.MobsById[zeidId + 1] = zeid2;
            }
            var v = new BattlefieldViewFake { Area = area };
            var ally = new BattlefieldMobFake { Id = 28 };
            if (volker) v.InsertResults[28] = ally;
            var p = new BattlefieldPlayerFake { CharId = 7, CurrentBattlefield = v };
            s.OnEventFinishBattlefield(new BattlefieldContextFake(s) { Player = p, Engine = e }, 32004, 0);
            return $"{string.Join(",", e.Calls)}/{(zeid2 == null ? "-" : string.Join(",", zeid2.Calls))}/" +
                   $"{string.Join(",", v.Calls)}/{string.Join(",", p.Calls)}/{string.Join(",", ally.Calls)}";
        });
        string Volker(string player, string spot) =>
            $"Battlefield.InsertEntity(28,True,True)/Player.SetPos({player})/" +
            $"Mob.SetSpawn({spot}),Mob.Spawn(,),Mob.SetMobLevel(75)";
        string v1 = Volker("-443,-167,-239,127", "-451.155,-167,-240.004,125");
        string v2 = Volker("-762.949,-407,-478.991,127", "-770.805,-407,-480.077,125");
        string v3 = Volker("-1082.787,-647,-718.976,127", "-1090.97,-647,-720.077,125");
        Check("c3 N6 where two paths bf: onEventFinishBattlefield: Zeid_2 (ZEID_BCNM_OFFSET + (area - 1) * 4 + 1) " +
              "not up: spawned, its HP floor(999 * 50%) = 499, Volker (targid 28) put in as an ally, the char and " +
              "Volker at the area's spots, Volker spawned at level 75 (areas 1, 2, 3)",
              $"SpawnMob({offset + 1})/Mob.SetHP(499)/{v1}|" +
              $"SpawnMob({offset + 5})/Mob.SetHP(499)/{v2}|" +
              $"SpawnMob({offset + 9})/Mob.SetHP(499)/{v3}",
              $"{Finish(1, false)}|{Finish(2, false)}|{Finish(3, false)}");
        Check("c3 N6 where two paths bf: onEventFinishBattlefield: Zeid_2 up: nothing (the bail-out); Zeid_2 " +
              "missing (nil): SpawnMob, no HP, Volker; Zeid (phase 1) missing: Zeid_2 spawned, no HP, Volker",
              $"////|SpawnMob({offset + 1})/-/{v1}|SpawnMob({offset + 5})//{v2}",
              $"{Finish(1, true)}|{Finish(1, false, true)}|{Finish(2, false, false, true)}");
    }

    static void RunC3N6HeirBf()
    {
        var s = new QC.Battlefields.QuBiaArena.HeirToTheLight();
        var d = (BattlefieldMissionData)s.Data;
        int qa = QC.Zone.QubiaArena;
        int heir = QC.Mission.Sandoria.TheHeirToTheLight;
        Check("c3 N6 heir bf: the Lua file's fields (no exp loss, mission, trusts, MAX_LEVEL, entry and exit NPCs, " +
              "the mission's log and id, the status log, groups)",
              $"False True True MAX_LEVEL BC_Entrance Burning_Circle 0 {heir} 0 2",
              $"{d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.LevelCapSetting} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} {(int?)d.MissionArea} {d.Mission} {(int?)d.MissionStatusArea} " +
              $"{d.Groups.Count}");
        Check("c3 N6 heir bf: the groups: phase 1 (+3 to +13, stride 14) superlinked with allDeath; phase 2 (+0 to " +
              "+2) not spawned, superlinked, with allDeath; all WARLORD_ROJGNOJ offsets",
              "3,4,5,6,7,8,9,10,11,12,13;17,18,19,20,21,22,23,24,25,26,27;31,32,33,34,35,36,37,38,39,40,41 True " +
              "True null|0,1,2;14,15,16;28,29,30 True True False|True",
              string.Join("|", d.Groups.Select(g =>
                  $"{N6Ids(g)} {g.Superlink} {g.AllDeath != null} {N6Spawned(g.Spawned)}")) + "|" +
              d.Groups.All(g => g.AreaMobIds.All(a => a.All(x => x.Key == QC.Mob.QubiaArena.WARLORD_ROJGNOJ))));

        // phase 1's allDeath: the players by Intro / completed / neither
        string AllDeath(int area, double arrival, int calls) => N6Try(() =>
        {
            var v = new BattlefieldViewFake { Area = area };
            if (arrival != 0) v.LocalVars["arrivalCS"] = arrival;
            var intro = new BattlefieldPlayerFake { CharId = 1 };
            intro.CharVars["Mission[0][23]Intro"] = 1;
            var done = new BattlefieldPlayerFake { CharId = 2 };
            done.CompletedMissions.Add($"0/{heir}");
            var other = new BattlefieldPlayerFake { CharId = 3 };
            other.CharVars["Mission[0][23]Intro"] = 2;
            other.CompletedMissions.Add($"1/{heir}");
            v.PlayerList.AddRange(new IBattlefieldPlayer[] { intro, done, other });
            for (int i = 0; i < calls; i++)
                d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = v }, null);
            return $"{string.Join(",", v.Calls)}/{string.Join(",", intro.Calls)}/{string.Join(",", done.Calls)}/" +
                   string.Join(",", other.Calls);
        });
        Check("c3 N6 heir bf: phase 1's allDeath: arrivalCS set to 1, then 32005 (the area, 0, 4, 0, 15) to the " +
              "player with Intro 1 and to the one with the mission completed, 32004 (the area, 0, 4) to the one " +
              "with neither (Intro 2, another log's 23); called twice: once; arrivalCS already 1: nothing",
              "Battlefield.SetLocalVar(arrivalCS,1)/Player.StartEvent(32005,2,0,4,0,15)/" +
              "Player.StartEvent(32005,2,0,4,0,15)/Player.StartEvent(32004,2,0,4)|" +
              "Battlefield.SetLocalVar(arrivalCS,1)/Player.StartEvent(32005,3,0,4,0,15)/" +
              "Player.StartEvent(32005,3,0,4,0,15)/Player.StartEvent(32004,3,0,4)|///",
              $"{AllDeath(2, 0, 1)}|{AllDeath(3, 0, 2)}|{AllDeath(1, 1, 1)}");

        string Won() => N6Try(() =>
        {
            var v = new BattlefieldViewFake { Area = 1 };
            d.Groups[1].AllDeath(new BattlefieldContextFake(s) { Battlefield = v }, null);
            return string.Join(",", v.Calls);
        });
        Check("c3 N6 heir bf: phase 2's allDeath: WON", "Battlefield.SetStatus(2)", Won());

        // onEventFinishBattlefield
        const int warlord = 17621000;
        string Finish(int area, int up) => N6Try(() =>
        {
            var e = new BattlefieldEngineFake();
            e.ZoneMobIds[$"{qa}:{QC.Mob.QubiaArena.WARLORD_ROJGNOJ}"] = warlord;
            int baseId = warlord + (area - 1) * 14;
            for (int i = 0; i <= 2; i++)
            {
                var m = new BattlefieldMobFake { Id = baseId + i, MaxHP = 10 };
                if (i == up) m.SpawnInWorld();
                e.MobsById[m.Id] = m;
            }
            var v = new BattlefieldViewFake { Area = area };
            var trion = new BattlefieldMobFake { Id = 75 };
            v.InsertResults[75] = trion;
            var p = new BattlefieldPlayerFake { CharId = 7, CurrentBattlefield = v };
            s.OnEventFinishBattlefield(new BattlefieldContextFake(s) { Player = p, Engine = e }, 32004, 0);
            return $"{string.Join(",", e.Calls)}/{string.Join(",", v.Calls)}/{string.Join(",", p.Calls)}/" +
                   string.Join(",", trion.Calls);
        });
        string Trion(string player, string spot) =>
            $"Battlefield.InsertEntity(75,True,True)/Player.SetPos({player})/" +
            $"Mob.SetSpawn({spot}),Mob.Spawn(,),Mob.SetMobLevel(75)";
        int b2 = warlord + 14, b3 = warlord + 28;
        Check("c3 N6 heir bf: onEventFinishBattlefield: none of phase 2's three (WARLORD_ROJGNOJ + (area - 1) * 14 " +
              "+ 0 to 2) up: the three spawned, Trion (targid 75) put in as an ally, the char and Trion at the " +
              "area's spots, Trion spawned at level 75 (areas 1, 2, 3)",
              $"SpawnMob({warlord}),SpawnMob({warlord + 1}),SpawnMob({warlord + 2})/" +
              Trion("-398.196,-201.625,416.836,68", "-399.92,-201.62,411.64,63") + "|" +
              $"SpawnMob({b2}),SpawnMob({b2 + 1}),SpawnMob({b2 + 2})/" +
              Trion("1.877,-1.625,16.935,68", "0.08,-1.62,11.64,63") + "|" +
              $"SpawnMob({b3}),SpawnMob({b3 + 1}),SpawnMob({b3 + 2})/" +
              Trion("401.832,198.375,-382.841,68", "400.1,198.37,-388.32,63"),
              $"{Finish(1, -1)}|{Finish(2, -1)}|{Finish(3, -1)}");
        Check("c3 N6 heir bf: onEventFinishBattlefield: one of the three up (+0, +1, +2): nothing (the bail-out)",
              "///|///|///",
              $"{Finish(1, 0)}|{Finish(2, 1)}|{Finish(3, 2)}");
    }

    static void RunC3N6MoonReadingBf()
    {
        var s = new QC.Battlefields.FullMoonFountain.MoonReading();
        var d = (BattlefieldMissionData)s.Data;
        int fmf = QC.Zone.FullMoonFountain;
        Check("c3 N6 moon reading bf: the Lua file's fields (no exp loss, mission, trusts, MAX_LEVEL, entry and exit " +
              "NPCs, the mission's log and id, the status log, groups)",
              $"False True True MAX_LEVEL MS_Entrance Moon_Spiral 2 {QC.Mission.Windurst.MoonReading} 2 2",
              $"{d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.LevelCapSetting} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} {(int?)d.MissionArea} {d.Mission} {(int?)d.MissionStatusArea} " +
              $"{d.Groups.Count}");
        Check("c3 N6 moon reading bf: the groups: phase 1 (+0 to +3, stride 6) superlinked with allDeath; phase 2 " +
              "(+4, +5) not spawned, superlinked, with allDeath; all ACE_OF_CUPS offsets",
              "0,1,2,3;6,7,8,9;12,13,14,15 True True null|4,5;10,11;16,17 True True False|True",
              string.Join("|", d.Groups.Select(g =>
                  $"{N6Ids(g)} {g.Superlink} {g.AllDeath != null} {N6Spawned(g.Spawned)}")) + "|" +
              d.Groups.All(g => g.AreaMobIds.All(a => a.All(x => x.Key == QC.Mob.FullMoonFountain.ACE_OF_CUPS))));

        string AllDeath(int calls) => N6Try(() =>
        {
            var v = new BattlefieldViewFake { Area = 2 };
            var p1 = new BattlefieldPlayerFake { CharId = 1 };
            var p2 = new BattlefieldPlayerFake { CharId = 2 };
            v.PlayerList.AddRange(new IBattlefieldPlayer[] { p1, p2 });
            for (int i = 0; i < calls; i++)
                d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = v }, null);
            return $"{string.Join(",", v.Calls)}/{string.Join(",", p1.Calls)}/{string.Join(",", p2.Calls)}";
        });
        const string cs = "Player.StartEvent(32004,1,0,1,0,1)";
        Check("c3 N6 moon reading bf: phase 1's allDeath: 32004 (1, 0, 1, 0, 1) to each player (area 2 too); " +
              "called twice: twice (no once guard)",
              $"/{cs}/{cs}|/{cs},{cs}/{cs},{cs}",
              $"{AllDeath(1)}|{AllDeath(2)}");

        string Won() => N6Try(() =>
        {
            var v = new BattlefieldViewFake { Area = 1 };
            d.Groups[1].AllDeath(new BattlefieldContextFake(s) { Battlefield = v }, null);
            return string.Join(",", v.Calls);
        });
        Check("c3 N6 moon reading bf: phase 2's allDeath: WON", "Battlefield.SetStatus(2)", Won());

        const int ace = 17578000;
        string Finish(int area, int up) => N6Try(() =>
        {
            var e = new BattlefieldEngineFake();
            e.ZoneMobIds[$"{fmf}:{QC.Mob.FullMoonFountain.ACE_OF_CUPS}"] = ace;
            int baseId = ace + (area - 1) * 6 + 4;
            for (int i = 0; i <= 1; i++)
            {
                var m = new BattlefieldMobFake { Id = baseId + i, MaxHP = 10 };
                if (i == up) m.SpawnInWorld();
                e.MobsById[m.Id] = m;
            }
            var v = new BattlefieldViewFake { Area = area };
            var ajido = new BattlefieldMobFake { Id = 33 };
            v.InsertResults[33] = ajido;
            var p = new BattlefieldPlayerFake { CharId = 7, CurrentBattlefield = v };
            s.OnEventFinishBattlefield(new BattlefieldContextFake(s) { Player = p, Engine = e }, 32004, 0);
            return $"{string.Join(",", e.Calls)}/{string.Join(",", v.Calls)}/{string.Join(",", p.Calls)}/" +
                   string.Join(",", ajido.Calls);
        });
        string Ajido(string player, string spot) =>
            $"Battlefield.InsertEntity(33,True,True)/Player.SetPos({player})/" +
            $"Mob.SetSpawn({spot}),Mob.Spawn(,),Mob.SetMobLevel(75)";
        Check("c3 N6 moon reading bf: onEventFinishBattlefield: neither of phase 2's two (ACE_OF_CUPS + (area - 1) " +
              "* 6 + 4, + 5) up: the two spawned, Ajido-Marujido (targid 33) put in as an ally, the char and Ajido " +
              "at the area's spots, Ajido spawned at level 75 (areas 1, 2, 3)",
              $"SpawnMob({ace + 4}),SpawnMob({ace + 5})/" +
              Ajido("340.22,48.557,-386.114,190", "340.117,48.752,-383.747,64") + "|" +
              $"SpawnMob({ace + 10}),SpawnMob({ace + 11})/" +
              Ajido("-59.877,10.577,13.853,190", "-59.98,10.752,16.22,64") + "|" +
              $"SpawnMob({ace + 16}),SpawnMob({ace + 17})/" +
              Ajido("-459.974,-51.423,373.86,190", "-379.826,-51.248,376.227,64"),
              $"{Finish(1, -1)}|{Finish(2, -1)}|{Finish(3, -1)}");
        Check("c3 N6 moon reading bf: onEventFinishBattlefield: one of the two up (+4, +5): nothing (the bail-out)",
              "///|///",
              $"{Finish(1, 0)}|{Finish(3, 1)}");
    }
}
