// XI.Verify.Quests -- quest rework C3 N4: the two battlefields (Qu'Bia Arena's Rank 5 Mission, the Throne Room's
// Shadow Lord) on C#, one fake check per arm no packet shows.
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunC3N4Rank5Mission()
    {
        var s = new QC.Battlefields.QuBiaArena.Rank5Mission();
        var d = (BattlefieldMissionData)s.Data;
        int qa = QC.Zone.QubiaArena;
        Check("c3 N4 rank 5 mission: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.Rank5Mission}|scripts/battlefields/QuBia_Arena/rank_5_mission",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c3 N4 rank 5 mission: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, " +
              "MAX_LEVEL, time, index, entry and exit NPCs, mission area nil, mission 14, status 11, title, groups)",
              $"206 512 False True True 6 MAX_LEVEL 900 0 BC_Entrance Burning_Circle null 14 11 " +
              $"{QC.Title.ArchmageAssassin} 3",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} {(d.MissionArea == null ? "null" : d.MissionArea.ToString())} " +
              $"{d.Mission} {d.RequiredMissionStatus} {d.Title} {d.Groups.Count}");
        string Ids(BattlefieldGroup g) =>
            string.Join(";", g.AreaMobIds.Select(a => string.Join(",", a.Select(x => x.Offset))));
        Check("c3 N4 rank 5 mission: the groups: the Archlich (0, 7, 14) with allDeath; the Sorcerers (+1 +2 per " +
              "area); the Warriors (+3 to +6 per area) not spawned; all superlink group 1",
              "0;7;14 True null 1|1,2;8,9;15,16 False null 1|3,4,5,6;10,11,12,13;17,18,19,20 False False 1",
              string.Join("|", d.Groups.Select(g =>
                  $"{Ids(g)} {g.AllDeath != null} {(g.Spawned == null ? "null" : g.Spawned.ToString())} " +
                  $"{g.SuperlinkGroup}")));
        Check("c3 N4 rank 5 mission: every group's mobs are ARCHLICH_TABERQUOAN offsets",
              "True",
              d.Groups.All(g => g.AreaMobIds.All(a => a.All(x => x.Key == QC.Mob.QubiaArena.ARCHLICH_TABERQUOAN)))
                  .ToString());
        Check("c3 N4 rank 5 mission: overrides entryRequirement and onBattlefieldTick",
              "EntryRequirement,OnBattlefieldTick",
              string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides)));

        string Entry(int nation, bool registrant, bool seal = false, params string[] completed)
        {
            var (q, p) = C2BContext(s, "BC_Entrance");
            p.NationId = nation;
            if (seal) p.KeyItems[QC.KeyItem.NewFeiyinSeal] = true;
            foreach (var c in completed) p.CompletedMissions.Add(c);
            return s.EntryRequirement(q, registrant).ToString();
        }
        Check("c3 N4 rank 5 mission: entryRequirement: the nation's mission 14 completed (San d'Oria, Bastok, " +
              "Windurst; registrant or not); the New Fei'Yin Seal; neither; another log's 14; the nation's 13 or 15",
              "True True True True True True False False False False",
              $"{Entry(0, true, completed: "0/14")} {Entry(1, false, completed: "1/14")} " +
              $"{Entry(2, true, completed: "2/14")} {Entry(0, false, true)} {Entry(2, true, true)} " +
              $"{Entry(1, true, true, "0/14")} {Entry(1, true)} {Entry(1, true, completed: "0/14")} " +
              $"{Entry(1, true, completed: "1/13")} {Entry(1, false, completed: "1/15")}");

        // the Archlich's allDeath
        const int archlich = 17621014;
        string Death(int baseId, params int[] up)
        {
            var e = new BattlefieldEngineFake();
            for (int i = 0; i <= 7; i++)
            {
                if (i == 4 && !up.Contains(-4)) continue; // +4 missing (GetMobByID nil) unless asked for
                var m = new BattlefieldMobFake { Id = baseId + i, IsSpawned = up.Contains(i), HP = 1, MaxHP = 1 };
                e.MobsById[m.Id] = m;
            }
            var view = new BattlefieldViewFake { Area = 1 };
            var p1 = new BattlefieldPlayerFake();
            var p2 = new BattlefieldPlayerFake();
            view.PlayerList.AddRange(new IBattlefieldPlayer[] { p1, p2 });
            d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = view, Engine = e },
                                 (IBattlefieldMob)e.MobsById[baseId]);
            return $"{string.Join(",", p1.Calls)}/{string.Join(",", p2.Calls)}/{string.Join(",", e.Calls)}/" +
                   string.Join(",", view.Calls);
        }
        Check("c3 N4 rank 5 mission: allDeath: each add (+1 to +6) still spawned is dropped from every player's " +
              "client (ENTITY_DESPAWN 4, UPDATE_NONE 0) and despawned; adds not spawned, missing (+4) and +7 " +
              "(spawned, the next area's Archlich) untouched; then WON",
              $"Player.SendEntityUpdateToPlayer({archlich + 1},4,0),Player.SendEntityUpdateToPlayer(" +
              $"{archlich + 6},4,0)/" +
              $"Player.SendEntityUpdateToPlayer({archlich + 1},4,0),Player.SendEntityUpdateToPlayer(" +
              $"{archlich + 6},4,0)/" +
              $"DespawnMob({archlich + 1}),DespawnMob({archlich + 6})/Battlefield.SetStatus(2)|" +
              $"Player.SendEntityUpdateToPlayer({archlich + 4},4,0)/" +
              $"Player.SendEntityUpdateToPlayer({archlich + 4},4,0)/" +
              $"DespawnMob({archlich + 4})/Battlefield.SetStatus(2)|///Battlefield.SetStatus(2)",
              $"{Death(archlich, 0, 1, 6, 7)}|" +
              $"{Death(archlich, -4, 4)}|" +
              $"{Death(archlich, 0, 7)}");

        // onBattlefieldTick
        (BattlefieldContextFake Q, BattlefieldEngineFake E, BattlefieldViewFake V, BattlefieldMobFake[] M) Arena(
            int area, int status, uint now, double next, bool target, bool lich = true, params int[] up)
        {
            var e = new BattlefieldEngineFake { SystemTime = now };
            e.ZoneMobIds[$"{qa}:{QC.Mob.QubiaArena.ARCHLICH_TABERQUOAN}"] = archlich;
            int baseId = archlich + (area - 1) * 7;
            var mobs = new BattlefieldMobFake[7];
            for (int i = 0; i <= 6; i++)
            {
                if (i == 0 && !lich) continue;
                if (i == 5 && up.Contains(-5)) continue; // +5 missing
                mobs[i] = new BattlefieldMobFake { Id = baseId + i, IsSpawned = i < 3 || up.Contains(i), HP = 1,
                                                   MaxHP = 1 };
                e.MobsById[mobs[i].Id] = mobs[i];
            }
            if (target && mobs[0] != null)
                mobs[0].Target = new BattlefieldMobFake { Id = 99, TargId = 1099 };
            var view = new BattlefieldViewFake { Id = 512, ZoneId = qa, Area = area, Status = status };
            if (next != 0) view.LocalVars["nextWarriorRespawn"] = next;
            return (new BattlefieldContextFake(s) { Battlefield = view, Engine = e }, e, view, mobs);
        }
        string Tick(int area, int status, uint now, double next, bool target, bool lich = true, params int[] up)
        {
            var (q, e, v, mobs) = Arena(area, status, now, next, target, lich, up);
            s.OnBattlefieldTick(q, 1);
            return string.Join(",", v.Calls.Where(c => c.Contains("nextWarriorRespawn"))) + "/" +
                   string.Join(";", mobs.Skip(3).Select(m => m == null ? "-" : string.Join(",", m.Calls)));
        }
        int lk = BattlefieldConst.StatusLocked;
        string spawn(int id) => $"Mob.Spawn(,),Mob.AddEnmity(99,1,0),Mob.Engage(1099)";
        Check("c3 N4 rank 5 mission: tick, locked, past nextWarriorRespawn, the Archlich with a target: the next " +
              "pulse ten seconds on, and per spawn point (+3/+5, +4/+6) the first Warrior not up spawns, gets one " +
              "enmity on the target and engages it; +3 up: +5; all up: only the pulse; +5 missing and +3 up: none " +
              "there",
              $"Battlefield.SetLocalVar(nextWarriorRespawn,1010)/{spawn(3)};{spawn(4)};;|" +
              $"Battlefield.SetLocalVar(nextWarriorRespawn,1010)/;{spawn(4)};{spawn(5)};|" +
              "Battlefield.SetLocalVar(nextWarriorRespawn,1010)/;;;|" +
              $"Battlefield.SetLocalVar(nextWarriorRespawn,1010)/;;-;{spawn(6)}",
              $"{Tick(1, lk, 1000, 0, true)}|{Tick(1, lk, 1000, 0, true, true, 3)}|" +
              $"{Tick(1, lk, 1000, 0, true, true, 3, 4, 5, 6)}|{Tick(1, lk, 1000, 0, true, true, 3, 4, -5)}");
        Check("c3 N4 rank 5 mission: tick at the respawn time itself pulses (area 2, base + 7); before it, not " +
              "locked (open, won), without a target, without the Archlich: nothing",
              $"Battlefield.SetLocalVar(nextWarriorRespawn,1010)/{spawn(3)};{spawn(4)};;|/;;;|/;;;|/;;;|/;;;|/;;;",
              $"{Tick(2, lk, 1000, 1000, true)}|{Tick(1, lk, 999, 1000, true)}|" +
              $"{Tick(1, BattlefieldConst.StatusOpen, 1000, 0, true)}|" +
              $"{Tick(1, BattlefieldConst.StatusWon, 1000, 0, true)}|{Tick(1, lk, 1000, 0, false)}|" +
              $"{Tick(1, lk, 1000, 0, true, false)}");
    }

    static void RunC3N4ShadowLordBattle()
    {
        var s = new QC.Battlefields.ThroneRoom.ShadowLordBattle();
        var d = (BattlefieldMissionData)s.Data;
        int tr = QC.Zone.ThroneRoom;
        Check("c3 N4 shadow lord: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.ShadowLordBattle}|scripts/battlefields/Throne_Room/shadow_lord_battle",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c3 N4 shadow lord: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, " +
              "time, index, entry and exit NPCs, mission area nil, mission 15, status 3, title, groups)",
              $"165 160 False True True 6 MAX_LEVEL 1800 0 _4l1 _4l2,_4l3,_4l4 null 15 3 " +
              $"{QC.Title.ShadowBanisher} 2",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} {(d.MissionArea == null ? "null" : d.MissionArea.ToString())} " +
              $"{d.Mission} {d.RequiredMissionStatus} {d.Title} {d.Groups.Count}");
        Check("c3 N4 shadow lord: phase 1 (0, 1, 2) with death; phase 2 (3, 4, 5) not spawned with death; no " +
              "allDeath, no superlink",
              "0;1;2 True False null null|3;4;5 True False False null",
              string.Join("|", d.Groups.Select(g =>
                  $"{string.Join(";", g.AreaMobIds.Select(a => string.Join(",", a.Select(x => x.Offset))))} " +
                  $"{g.Death != null} {g.AllDeath != null} {(g.Spawned == null ? "null" : g.Spawned.ToString())} " +
                  $"{(g.SuperlinkGroup == null ? "null" : g.SuperlinkGroup.ToString())}")));
        Check("c3 N4 shadow lord: overrides onEventFinishBattlefield and onBattlefieldTick",
              "OnBattlefieldTick,OnEventFinishBattlefield",
              string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides).OrderBy(x => x)));

        // the deaths
        var view = new BattlefieldViewFake { Area = 3 };
        var p1 = new BattlefieldPlayerFake();
        var p2 = new BattlefieldPlayerFake();
        view.PlayerList.AddRange(new IBattlefieldPlayer[] { p1, p2 });
        d.Groups[0].Death(new BattlefieldContextFake(s) { Battlefield = view }, null, 1);
        var view2 = new BattlefieldViewFake { Area = 1 };
        d.Groups[1].Death(new BattlefieldContextFake(s) { Battlefield = view2 }, null, 1);
        Check("c3 N4 shadow lord: phase 1's death: every player plays 32004 (the area, 3); phase 2's: WON",
              "Player.StartEvent(32004,3)|Player.StartEvent(32004,3)|Battlefield.SetStatus(2)|",
              $"{string.Join(",", p1.Calls)}|{string.Join(",", p2.Calls)}|{string.Join(",", view2.Calls)}|" +
              string.Join(",", view.Calls));

        // onEventFinishBattlefield
        const int offset = 17453060;
        string Phase(int area, bool up, bool missing = false)
        {
            var e = new BattlefieldEngineFake();
            e.ZoneMobIds[$"{tr}:{QC.Mob.ThroneRoom.SHADOW_LORD_RANK_5_OFFSET}"] = offset;
            int phase2 = offset + area + 2;
            BattlefieldMobFake mob = null;
            if (!missing)
            {
                mob = new BattlefieldMobFake { Id = phase2, HP = 1, MaxHP = 1 };
                if (up) mob.SpawnInWorld();
                e.MobsById[phase2] = mob;
            }
            var p = new BattlefieldPlayerFake
            {
                CharId = 7, CurrentBattlefield = new BattlefieldViewFake { Area = area },
            };
            s.OnEventFinishBattlefield(new BattlefieldContextFake(s) { Player = p, Engine = e }, 32004, 0);
            return string.Join(",", e.Calls) + "/" + (mob == null ? "-" : string.Join(",", mob.Calls));
        }
        Check("c3 N4 shadow lord: onEventFinishBattlefield: phase 2 (offset + area + 2) not up: phase 1 (offset + " +
              "area - 1) despawned, phase 2 spawned with the player's enmity (areas 1, 3); phase 2 up: nothing; " +
              "phase 2 missing (GetMobByID nil): despawn and spawn, no enmity",
              $"DespawnMob({offset}),SpawnMob({offset + 3})/Mob.UpdateEnmity(7)|" +
              $"DespawnMob({offset + 2}),SpawnMob({offset + 5})/Mob.UpdateEnmity(7)|/|" +
              $"DespawnMob({offset}),SpawnMob({offset + 3})/-",
              $"{Phase(1, false)}|{Phase(3, false)}|{Phase(2, true)}|{Phase(1, false, true)}");

        // onBattlefieldTick
        string Tick(int area, int status, uint now, uint defeated, uint defeatTime, bool alive = true,
                    bool missing = false)
        {
            var e = new BattlefieldEngineFake { SystemTime = now };
            e.ZoneMobIds[$"{tr}:{QC.Mob.ThroneRoom.SHADOW_LORD_RANK_5_OFFSET}"] = offset;
            var mob = new BattlefieldMobFake { Id = offset + area + 2, IsSpawned = alive, HP = 1, MaxHP = 1 };
            if (defeated != 0) mob.LocalVars["[ShadowLord]Defeated"] = defeated;
            if (defeatTime != 0) mob.LocalVars["[ShadowLord]DefeatTime"] = defeatTime;
            if (!missing) e.MobsById[mob.Id] = mob;
            var v = new BattlefieldViewFake { Id = 160, ZoneId = tr, Area = area, Status = status };
            s.OnBattlefieldTick(new BattlefieldContextFake(s) { Battlefield = v, Engine = e }, 1);
            return string.Join(",", mob.Calls.Where(c => c.Contains("Unkillable") || c.Contains("SetHP")));
        }
        int lk = BattlefieldConst.StatusLocked;
        const string kill = "Mob.SetUnkillable(False),Mob.SetHP(0)";
        Check("c3 N4 shadow lord: tick, locked, phase 2 alive and Defeated set: at DefeatTime and after: killable, " +
              "HP 0; before it: nothing; DefeatTime unset: Defeated + 30 (at it, one before)",
              $"{kill}|{kill}||{kill}|",
              $"{Tick(1, lk, 2000, 1000, 2000)}|{Tick(2, lk, 2001, 1000, 2000)}|{Tick(1, lk, 1999, 1000, 2000)}|" +
              $"{Tick(3, lk, 1030, 1000, 0)}|{Tick(1, lk, 1029, 1000, 0)}");
        Check("c3 N4 shadow lord: tick: Defeated unset, phase 2 not alive, missing, not locked (open, won): nothing",
              "|||||",
              $"{Tick(1, lk, 5000, 0, 2000)}|{Tick(1, lk, 5000, 1000, 2000, false)}|" +
              $"{Tick(1, lk, 5000, 1000, 2000, true, true)}|" +
              $"{Tick(1, BattlefieldConst.StatusOpen, 5000, 1000, 2000)}|" +
              $"{Tick(1, BattlefieldConst.StatusWon, 5000, 1000, 2000)}|");
    }
}
