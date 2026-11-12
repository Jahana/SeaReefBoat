// XI.Verify.Quests -- quest rework C3 Batch F (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md,
// section 1): the facet members several lanes need, one check per member over the fakes. The live bridge is
// covered by the lanes' replays; these checks pin the fake's reads and records, which the lanes' fake-side tests
// rely on.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    internal static void RunC3Facets()
    {
        Console.WriteLine("\n== c3 F: the facet members several lanes need ==");
        RunC3FMob();
        RunC3FMobCalls();
        RunC3FView();
        RunC3FEngine();
        RunC3FPlayer();
        RunC3FMission();
        RunC3FReview2();
        RunC3N5Facets();
    }

    static string C3FCalls(BattlefieldMobFake m) => string.Join("|", m.Calls);

    // the mob's state members: reads of the fields, writes recorded and applied
    static void RunC3FMob()
    {
        var m = new BattlefieldMobFake { Id = 17, Name = "Mob", Race = 5, TargId = 300, Pool = 1234, MaxHP = 900,
                                         MaxMP = 70, HPP = 50, HP = 450, TP = 1500, Behavior = 3 };
        Check("c3 F: Mob.LocalVar reads 0 for a var never set, SetLocalVar stores a uint",
              "0|4000000000|Mob.SetLocalVar(x,4000000000)",
              m.LocalVar("x") + "|" + m.SetLocalVarThen("x", 4000000000u) + "|" + C3FCalls(m));
        m.Calls.Clear();
        m.ResetLocalVars();
        Check("c3 F: Mob.ResetLocalVars clears every var and records", "0|Mob.ResetLocalVars()",
              m.LocalVar("x") + "|" + C3FCalls(m));
        Check("c3 F: Mob.IsSpawned is false until the mob spawns", "False", m.IsSpawned.ToString());
        Check("c3 F: a never-spawned mob reads isDead true, isAlive false (live: not in the world is dead)",
              "False|True", m.IsAlive + "|" + m.IsDead);
        Check("c3 F: Mob.IsEngaged is false until it engages", "False", m.IsEngaged.ToString());
        Check("c3 F: Mob.IsMob defaults to true", "True", m.IsMob.ToString());
        m.Calls.Clear();
        m.Spawn();
        Check("c3 F: a spawned mob with hp reads alive; hp 0 reads dead", "True|False|False|True",
              new Func<string>(() =>
              {
                  var a = m.IsAlive + "|" + m.IsDead;
                  m.HP = 0;
                  var b = m.IsAlive + "|" + m.IsDead;
                  m.HP = 450;
                  m.HPP = 50;
                  m.TP = 1500; // a spawn zeroes tp
                  return a + "|" + b;
              })());
        Check("c3 F: IsAlive / IsDead overrides win over the derived state", "True|True",
              new Func<string>(() =>
              {
                  var o = new BattlefieldMobFake { AliveOverride = true, DeadOverride = true };
                  return o.IsAlive + "|" + o.IsDead;
              })());
        Check("c3 F: Mob.Spawn() with no respawn time spawns the mob", "True|Mob.Spawn(,)",
              m.IsSpawned + "|" + C3FCalls(m));
        var r = new BattlefieldMobFake();
        r.Spawn(30, 60);
        Check("c3 F: Mob.Spawn(despawn, respawn) with a respawn time does not spawn the mob (the engine's else branch)",
              "False|Mob.Spawn(30,60)", r.IsSpawned + "|" + C3FCalls(r));
        m.Calls.Clear();
        m.SetSpawn(1.5, 2, 3, 128);
        m.SetSpawn(4, 5, 6);
        Check("c3 F: Mob.SetSpawn records the point and a null rotation is 0",
              "Mob.SetSpawn(1.5,2,3,128)|Mob.SetSpawn(4,5,6,)|(4, 5, 6, 0)", C3FCalls(m) + "|" + m.SpawnPoint);
        m.Calls.Clear();
        m.SetPos(10, 20, 30, 64);
        var first = m.GetPos();
        m.SetPos(11, 21, 31);
        Check("c3 F: Mob.SetPos sets the position and a null rotation keeps the old one; GetPos reads it",
              "(10, 20, 30, 64)|(11, 21, 31, 64)|Mob.SetPos(10,20,30,64)|Mob.SetPos(11,21,31,)",
              first + "|" + m.GetPos() + "|" + C3FCalls(m));
        m.Calls.Clear();
        m.LookAt(1, 2, 3);
        Check("c3 F: Mob.LookAt takes a position (x, y, z)", "Mob.LookAt(1,2,3)", C3FCalls(m));
        Check("c3 F: Mob.HP reads the field", "450", m.HP.ToString());
        Check("c3 F: Mob.HPP reads the field", "50", m.HPP.ToString());
        Check("c3 F: Mob.MaxHP reads the field", "900", m.MaxHP.ToString());
        Check("c3 F: Mob.MaxMP reads the field", "70", m.MaxMP.ToString());
        m.Calls.Clear();
        m.SetHP(10);
        Check("c3 F: Mob.SetHP sets the hp and records", "10|Mob.SetHP(10)", m.HP + "|" + C3FCalls(m));
        Check("c3 F: Mob.SetHP clamps to 0..MaxHP, updates HPP (floor, min 1, 0 at 0) and marks the spawned mob dead at 0",
              "900|100|1|1|0|0|True",
              new Func<string>(() =>
              {
                  var h = new BattlefieldMobFake { MaxHP = 900, IsSpawned = true };
                  h.SetHP(5000);
                  var a = h.HP + "|" + h.HPP;
                  h.SetHP(1);
                  var b = h.HP + "|" + h.HPP;
                  h.SetHP(-4);
                  return a + "|" + b + "|" + h.HP + "|" + h.HPP + "|" + h.IsDead;
              })());
        m.Calls.Clear();
        m.SetMP(7);
        Check("c3 F: Mob.SetMP sets the mp and records", "7|Mob.SetMP(7)", m.MP + "|" + C3FCalls(m));
        Check("c3 F: Mob.TP reads a float", "1500", m.TP.ToString());
        m.Calls.Clear();
        m.SetTP(0);
        Check("c3 F: Mob.SetTP sets the tp and records", "0|Mob.SetTP(0)", m.TP + "|" + C3FCalls(m));
        Check("c3 F: Mob.GetBehavior reads the flags", "3", m.GetBehavior().ToString());
        m.Calls.Clear();
        m.SetBehavior(m.GetBehavior() | 8);
        Check("c3 F: Mob.SetBehavior stores the flags (setBehavior(bit.bor(getBehavior(), flag)))",
              "11|Mob.SetBehavior(11)", m.GetBehavior() + "|" + C3FCalls(m));
        Check("c3 F: Mob.Pool reads the field", "1234", m.Pool.ToString());
        Check("c3 F: Mob.Name reads the field", "Mob", m.Name);
        Check("c3 F: Mob.Race reads the field", "5", m.Race.ToString());
        Check("c3 F: Mob.TargId reads the field", "300", m.TargId.ToString());
        var bf = new BattlefieldViewFake { Id = 77 };
        Check("c3 F: Mob.GetBattlefieldID is -1 in none and the battlefield's id in one", "-1|77",
              m.GetBattlefieldID() + "|" + new Func<int>(() => { m.Battlefield = bf; return m.GetBattlefieldID(); })());
        Check("c3 F: Mob.GetBattlefield answers the battlefield", "True", (m.GetBattlefield() == bf).ToString());
    }

    // the mob's calls: each recorded as Mob.<Member>(<args>)
    static void RunC3FMobCalls()
    {
        var m = new BattlefieldMobFake { Id = 1 };
        var other = new BattlefieldMobFake { Id = 2 };
        var player = new BattlefieldPlayerFake { CharId = 9 };
        Check("c3 F: Mob.GetTarget answers the target, null for nil", "True|True",
              (m.GetTarget() == null) + "|" + C3FSetTarget(m, other));
        m.Engage(0);
        Check("c3 F: Mob.Engage(0) does nothing (the engine acts only above 0)", "False", m.IsEngaged.ToString());
        m.Engage(300);
        Check("c3 F: Mob.Engage finds only chars: a targid no char has does nothing", "False", m.IsEngaged.ToString());
        m.CharTargIds.Add(300);
        m.Engage(300);
        Check("c3 F: Mob.Engage(targId) of a char engages", "True", m.IsEngaged.ToString());
        m.Disengage();
        Check("c3 F: Mob.Disengage clears the engaged state", "False", m.IsEngaged.ToString());
        m.Calls.Clear();
        m.UpdateEnmity(player);
        m.UpdateEnmity(other);
        Check("c3 F: Mob.UpdateEnmity on a live mob engages for a char, not for a mob or a dead mob",
              "False|True|False",
              new Func<string>(() =>
              {
                  var lm = new BattlefieldMobFake { IsSpawned = true, HP = 5 };
                  lm.UpdateEnmity(other);
                  var a = lm.IsEngaged;
                  lm.UpdateEnmity(player);
                  var b = lm.IsEngaged;
                  var dm = new BattlefieldMobFake();
                  dm.UpdateEnmity(player);
                  return a + "|" + b + "|" + dm.IsEngaged;
              })());
        Check("c3 F: Mob.UpdateEnmity records a player or a mob target (the engine engages only a char)", "Mob.UpdateEnmity(9)|Mob.UpdateEnmity(2)", C3FCalls(m));
        m.Calls.Clear();
        m.AddEnmity(player, 1, 0);
        Check("c3 F: Mob.AddEnmity(entity, ce, ve)", "Mob.AddEnmity(9,1,0)", C3FCalls(m));
        m.Calls.Clear();
        m.ResetEnmity(other);
        Check("c3 F: Mob.ResetEnmity(entity)", "Mob.ResetEnmity(2)", C3FCalls(m));
        m.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = other, Ce = 5, Ve = 6, Active = true });
        var list = m.GetEnmityList();
        Check("c3 F: Mob.GetEnmityList answers each entry's entity, ce, ve, active and tameable", "1|2|5|6|True|False",
              $"{list.Count}|{list[0].Entity.Id}|{list[0].Ce}|{list[0].Ve}|{list[0].Active}|{list[0].Tameable}");
        m.Calls.Clear();
        m.UseMobAbility();
        m.UseMobAbility(1283);
        m.UseMobAbility(1283, player);
        m.UseMobAbility(1283, null);
        Check("c3 F: Mob.UseMobAbility in its three forms (none, skill, skill and target; a null target is nil)",
              "Mob.UseMobAbility()|Mob.UseMobAbility(1283)|Mob.UseMobAbility(1283,9)|Mob.UseMobAbility(1283,)",
              C3FCalls(m));
        m.Calls.Clear();
        m.SetMobAbilityEnabled(false);
        Check("c3 F: Mob.SetMobAbilityEnabled", "Mob.SetMobAbilityEnabled(False)", C3FCalls(m));
        m.Calls.Clear();
        m.SetMagicCastingEnabled(false);
        Check("c3 F: Mob.SetMagicCastingEnabled", "Mob.SetMagicCastingEnabled(False)", C3FCalls(m));
        m.Calls.Clear();
        m.SetAutoAttackEnabled(true);
        Check("c3 F: Mob.SetAutoAttackEnabled", "Mob.SetAutoAttackEnabled(True)", C3FCalls(m));
        m.Calls.Clear();
        m.SetMobMod(27, 4);
        Check("c3 F: Mob.SetMobMod(mobMod, value)", "Mob.SetMobMod(27,4)", C3FCalls(m));
        m.Calls.Clear();
        m.SetUnkillable(true);
        Check("c3 F: Mob.SetUnkillable", "Mob.SetUnkillable(True)", C3FCalls(m));
        m.Calls.Clear();
        m.SetUntargetable(true);
        Check("c3 F: Mob.SetUntargetable", "Mob.SetUntargetable(True)", C3FCalls(m));
        m.Calls.Clear();
        m.SetSpawnAnimation(4);
        Check("c3 F: Mob.SetSpawnAnimation", "Mob.SetSpawnAnimation(4)", C3FCalls(m));
        m.Calls.Clear();
        m.SetAnimation(55);
        Check("c3 F: Mob.SetAnimation", "Mob.SetAnimation(55)", C3FCalls(m));
        m.Calls.Clear();
        m.SetStatus(2);
        Check("c3 F: Mob.SetStatus", "Mob.SetStatus(2)", C3FCalls(m));
    }

    // the view: insertEntity answers the entity, getAllies the allies
    static void RunC3FView()
    {
        var view = new BattlefieldViewFake();
        var ally = new BattlefieldMobFake { Id = 5 };
        view.InsertResults[300] = ally;
        Check("c3 F: View.InsertEntity answers the entity it put in", "True|Battlefield.InsertEntity(300,True,True)",
              (view.InsertEntity(300, true, true) == ally) + "|" + view.Calls[0]);
        Check("c3 F: View.InsertEntity answers null for nil (a targid not found)", "True",
              (view.InsertEntity(301, false, true) == null).ToString());
        Check("c3 F: View.InsertEntity(ally = true) adds the mob to the allies on every call (live makes a mob per call); ally = false does not",
              "2|0",
              new Func<string>(() =>
              {
                  var v = new BattlefieldViewFake();
                  var x = new BattlefieldMobFake { Id = 8 };
                  var y = new BattlefieldMobFake { Id = 9 };
                  v.InsertResults[1] = x;
                  v.InsertResults[2] = y;
                  v.InsertEntity(1, true, true);
                  v.InsertEntity(1, true, true);
                  var n = v.Allies().Count;
                  v.InsertEntity(2, false, true);
                  return n + "|" + (v.Allies().Count - n);
              })());
        view.AllyList.Clear();
        view.AllyList.Add(ally);
        Check("c3 F: View.Allies answers the allies", "1|5", view.Allies().Count + "|" + view.Allies()[0].Id);
    }

    // the engine's globals
    static void RunC3FEngine()
    {
        var e = new BattlefieldEngineFake();
        var mob = new BattlefieldMobFake { Id = 44 };
        e.MobsById[44] = mob;
        Check("c3 F: Engine.SpawnMob answers the mob and spawns it", "True|True|SpawnMob(44)",
              (e.SpawnMob(44) == mob) + "|" + mob.IsSpawned + "|" + e.Calls[0]);
        Check("c3 F: Engine.SpawnMob answers null for nil", "True", (e.SpawnMob(45) == null).ToString());
        e.DespawnMob(44);
        Check("c3 F: Engine.DespawnMob despawns the mob", "False|DespawnMob(44)", mob.IsSpawned + "|" + e.Calls.Last());
        var player = new BattlefieldPlayerFake { CharId = 6 };
        e.PlayersById[6] = player;
        Check("c3 F: Engine.GetPlayerByID answers the player, null for nil", "True|True",
              (e.GetPlayerByID(6) == player) + "|" + (e.GetPlayerByID(7) == null));
        e.SystemTime = 4000000000u;
        Check("c3 F: Engine.GetSystemTime answers a uint32", "4000000000", e.GetSystemTime().ToString());
        e.BusyMobs.Add(44);
        Check("c3 F: Engine.IsEntityBusy answers per mob", "True|False",
              e.IsEntityBusy(mob) + "|" + e.IsEntityBusy(new BattlefieldMobFake { Id = 1 }));
        Check("c3 F: Engine.ProgressEvent answers a progress action, priority unset",
              "progress(32003,3,4;)", e.ProgressEvent(32003, 3, 4).ToString());
        Check("c3 F: an event or exit action's string shows its priority",
              "event(1,2;7)|exit(3;8)|event(1,2)",
              new BattlefieldActionFake { Csid = 1, Args = new[] { 2 }, Priority = 7 } + "|" +
              new BattlefieldActionFake { Csid = 3, Exit = true, Priority = 8 } + "|" +
              new BattlefieldActionFake { Csid = 1, Args = new[] { 2 } });
        Check("c3 F: Action.SetPriority sets the priority and answers the same action",
              "True|progress(32003,3,4;1001)",
              new Func<string>(() =>
              {
                  var a = e.ProgressEvent(32003, 3, 4);
                  return (a.SetPriority(1001) == a) + "|" + a;
              })());
        var npc = new BattlefieldNpcFake { Id = 9 };
        npc.SetAnimation(8);
        npc.SetAnimation(9);
        Check("c3 F: Npc.SetAnimation records each animation in order", "8,9", string.Join(",", npc.Animations));
        e.ZoneMobIds["100:DIABOLOS"] = 1000;
        e.ZoneNpcIds["100:TILE"] = 5000;
        Check("c3 F: Engine.ZoneMobId and ZoneNpcId answer the set id, null when unset",
              "1000|5000|True|True|True",
              e.ZoneMobId(100, "DIABOLOS") + "|" + e.ZoneNpcId(100, "TILE") + "|" +
              (e.ZoneMobId(100, "TILE") == null) + "|" + (e.ZoneNpcId(100, "DIABOLOS") == null) + "|" +
              (e.ZoneMobId(101, "DIABOLOS") == null));
    }

    // the player's members
    static void RunC3FPlayer()
    {
        var p = new BattlefieldPlayerFake { CharId = 3, MaxHP = 1200, MaxMP = 300 };
        Check("c3 F: Player.Party answers the player alone unless a test sets PartyList", "1|3",
              p.Party().Count + "|" + p.Party()[0].Id);
        var q = new BattlefieldPlayerFake { CharId = 4 };
        p.PartyList = new List<IBattlefieldPlayer> { p, q };
        Check("c3 F: Player.Party answers the party, the player among them", "3,4",
              string.Join(",", p.Party().Select(x => x.Id)));
        var pet = new BattlefieldMobFake { Id = 8 };
        Check("c3 F: Player.Pet answers null for nil and the pet when there is one", "True|True",
              (p.Pet() == null) + "|" + new Func<bool>(() => { p.PetMob = pet; return p.Pet() == pet; })());
        Check("c3 F: Player.MaxHP reads the field", "1200", p.MaxHP.ToString());
        Check("c3 F: Player.MaxMP reads the field", "300", p.MaxMP.ToString());
        p.SetTP(0);
        Check("c3 F: Player.SetTP sets the tp and records", "0|Player.SetTP(0)", p.TP + "|" + p.Calls.Last());
        Check("c3 F: Player.IsDead is false until a test sets it", "False|True",
              p.IsDead + "|" + new Func<bool>(() => { p.IsDead = true; return p.IsDead; })());
        p.AllowSendRaisePrompt();
        Check("c3 F: Player.AllowSendRaisePrompt records", "Player.AllowSendRaisePrompt()", p.Calls.Last());
        p.SendEntityUpdateToPlayer(pet, 1, 0);
        Check("c3 F: Player.SendEntityUpdateToPlayer(entity, entityUpdate, updateMask) records",
              "Player.SendEntityUpdateToPlayer(8,1,0)", p.Calls.Last());
        Check("c3 F: Player.MissionRankPoints answers a boolean per mission", "False|True",
              p.MissionRankPoints(5) + "|" + C3FRankMet(p, 5));
    }

    // the mission members: a mission var as the char var Mission[log][id]<name>
    static void RunC3FMission()
    {
        var p = new BattlefieldPlayerFake { ThisMission = (MissionLog.CoP, 14) };
        var m = p.Mission;
        Check("c3 F: Mission.IsVarBitsSet is false for a clear var", "False", m.IsVarBitsSet("Prog", 0).ToString());
        m.SetVarBit("Prog", 0);
        m.SetVarBit("Prog", 2);
        Check("c3 F: Mission.SetVarBit sets the bit and records; IsVarBitsSet reads every bit asked",
              "True|True|False|Mission.SetVarBit(Prog,0)|Mission.SetVarBit(Prog,2)",
              m.IsVarBitsSet("Prog", 0) + "|" + m.IsVarBitsSet("Prog", 0, 2) + "|" +
              m.IsVarBitsSet("Prog", 0, 1) + "|" + string.Join("|", p.Calls));
        p.Calls.Clear();
        m.SetVar("Count", 2);
        m.IncrementVar("Count", 3);
        Check("c3 F: Mission.IncrementVar raises the mission var and records",
              "5|Mission.SetVar(Count,2)|Mission.IncrementVar(Count,3)",
              m.Var("Count") + "|" + string.Join("|", p.Calls));
    }

    // the second review of the fakes against the live bindings (F-review-findings.md, "Re-review of 73ecf71b")
    static void RunC3FReview2()
    {
        // 1: spawn restores hp as live does (CMobEntity::Reset, hp = maxhp), and the rest of Spawn/ResetCombat
        var a = new BattlefieldMobFake { MaxHP = 450, MaxMP = 30 };
        a.Spawn();
        Check("c3 F: a spawned mob with only MaxHP set reads full hp and alive", "450|100|True|False",
              a.HP + "|" + a.HPP + "|" + a.IsAlive + "|" + a.IsDead);
        var b = new BattlefieldMobFake { MaxHP = 450, MaxMP = 30, IsSpawned = true, HP = 450, HPP = 100, MP = 3, TP = 900 };
        b.SetHP(0);
        b.Disengage();
        var eng = new BattlefieldEngineFake();
        eng.MobsById[7] = b;
        eng.DespawnMob(7);
        eng.SpawnMob(7);
        Check("c3 F: Engine.SpawnMob on a killed and despawned mob restores hp, mp and tp", "450|100|True|30|0",
              b.HP + "|" + b.HPP + "|" + b.IsAlive + "|" + b.MP + "|" + b.TP);
        var c = new BattlefieldMobFake { MaxHP = 450, IsSpawned = true, HP = 100, HPP = 22 };
        c.Spawn();
        Check("c3 F: Mob.Spawn on a mob already spawned changes nothing (live: already spawned)", "100|22",
              c.HP + "|" + c.HPP);
        var d = new BattlefieldMobFake { MaxHP = 450, IsEngaged = true, Target = a };
        d.Spawn();
        Check("c3 F: a spawn clears the engaged state and target (live ResetCombat)", "False|True",
              d.IsEngaged + "|" + (d.Target == null));

        // 2, 5, 6: updateEnmity as live: a char target (a player, or the char GetTarget answers as a mob facet),
        // raw hp > 0 and not dead, sets the target even when engaged
        var charAsMob = new BattlefieldMobFake { Id = 90, IsMob = false };
        var other = new BattlefieldMobFake { Id = 91 };
        var u = new BattlefieldMobFake { IsSpawned = true, HP = 5 };
        u.UpdateEnmity(charAsMob);
        Check("c3 F: Mob.UpdateEnmity engages for a char that GetTarget answers (an IBattlefieldMob, IsMob false)",
              "True|True", u.IsEngaged + "|" + (u.Target == charAsMob));
        u.UpdateEnmity(other);
        Check("c3 F: Mob.UpdateEnmity of a mob target leaves the target alone", "True", (u.Target == charAsMob).ToString());
        var second = new BattlefieldMobFake { Id = 92, IsMob = false };
        u.UpdateEnmity(second);
        Check("c3 F: Mob.UpdateEnmity on an engaged mob still sets the target (MobEngage sets BattleTarget first)",
              "True", (u.Target == second).ToString());
        var despawned = new BattlefieldMobFake { IsSpawned = false, HP = 40 };
        despawned.UpdateEnmity(charAsMob);
        Check("c3 F: Mob.UpdateEnmity engages a despawned mob with hp (live uses raw hp > 0 and DeathAtMs == 0)",
              "True", despawned.IsEngaged.ToString());
        var dead = new BattlefieldMobFake { IsSpawned = true, HP = 0 };
        dead.UpdateEnmity(charAsMob);
        Check("c3 F: Mob.UpdateEnmity does not engage a mob at hp 0", "False", dead.IsEngaged.ToString());

        // 3: setHP(0) on an unkillable mob leaves hp 1
        var k = new BattlefieldMobFake { MaxHP = 900, IsSpawned = true, HP = 900 };
        k.SetUnkillable(true);
        k.SetHP(0);
        Check("c3 F: Mob.SetHP(0) on an unkillable mob leaves hp 1 and alive", "1|1|True", k.HP + "|" + k.HPP + "|" + k.IsAlive);
        k.SetUnkillable(false);
        k.SetHP(0);
        Check("c3 F: Mob.SetHP(0) on a killable mob reads dead", "0|True", k.HP + "|" + k.IsDead);

        // 4: two insertEntity(ally) calls give two allies
        var v = new BattlefieldViewFake();
        var al = new BattlefieldMobFake { Id = 50 };
        v.InsertResults[3] = al;
        v.InsertEntity(3, true, true);
        v.InsertEntity(3, true, true);
        Check("c3 F: two View.InsertEntity(ally) calls give two allies", "2", v.Allies().Count.ToString());

        // other state-changing members against live
        var s = new BattlefieldMobFake { MaxMP = 50, MP = 10 };
        s.SetMP(500);
        var s2 = s.MP;
        s.SetMP(-5);
        Check("c3 F: Mob.SetMP clamps to 0..MaxMP as live (mp = 0 then addMP)", "50|0", s2 + "|" + s.MP);
        var lv = new BattlefieldMobFake { MainLevel = 40 };
        lv.SetMobLevel(80);
        Check("c3 F: Mob.SetMobLevel sets the main level as live", "80", lv.MainLevel.ToString());
        var g = new BattlefieldMobFake { IsSpawned = true, IsEngaged = true, Target = a };
        g.EnmityEntries.Add(new BattlefieldEnmityEntry { Entity = other, Ce = 1, Ve = 1, Active = true });
        g.Disengage();
        Check("c3 F: Mob.Disengage clears the target and the enmity list (MobControllerDisengage)", "False|True|0",
              g.IsEngaged + "|" + (g.Target == null) + "|" + g.GetEnmityList().Count);
        var en = new BattlefieldMobFake { Id = 1 };
        var chr = new BattlefieldMobFake { Id = 2, IsMob = false };
        en.CharTargIds.Add(300);
        en.CharTargets[300] = chr;
        en.Engage(300);
        Check("c3 F: Mob.Engage(targId) sets the target to the char (MobEngage sets BattleTarget)", "True|True",
              en.IsEngaged + "|" + (en.Target == chr));
    }

    // (C3 N5) the four IQuestPlayer members the rank 6-7 missions added: the fake records each call with its
    // arguments; the two pops answer PopResult
    static void RunC3N5Facets()
    {
        var f = new Fake();
        IQuestPlayer p = ((IQuestContext)f).Player;
        bool popped = p.PopFromQm(new[] { "MOB_A", "MOB_B", "MOB_C" }, 0, false);
        f.PopResult = false;
        bool refused = p.PopFromQm(new[] { "MOB_D" }, 900, true);
        Check("c3 N5 facets: PopFromQm(names, hide, claim) records Player.PopFromQm(<name>;<name>...,<hide>,<claim>) " +
              "and answers PopResult",
              "Player.PopFromQm(MOB_A;MOB_B;MOB_C,0,False)|True|Player.PopFromQm(MOB_D,900,True)|False",
              $"{f.Calls[0]}|{popped}|{f.Calls[1]}|{refused}");
        var g = new Fake();
        IQuestPlayer gp = ((IQuestContext)g).Player;
        bool series = gp.PopFromQmSeriesRadius("MOB_E", 2, false, true, 2);
        g.PopResult = false;
        bool none = gp.PopFromQmSeriesRadius("MOB_F", 3, true, false, 5);
        Check("c3 N5 facets: PopFromQmSeriesRadius(name, count, claim, look, radius) records " +
              "Player.PopFromQmSeriesRadius(<name>,<count>,<claim>,<look>,<radius>) and answers PopResult",
              "Player.PopFromQmSeriesRadius(MOB_E,2,False,True,2)|True|" +
              "Player.PopFromQmSeriesRadius(MOB_F,3,True,False,5)|False",
              $"{g.Calls[0]}|{series}|{g.Calls[1]}|{none}");
        var h = new Fake();
        IQuestPlayer hp = ((IQuestContext)h).Player;
        hp.MobLookAtPlayer("MOB_G", 1);
        hp.MobLookAtPlayer("MOB_H");
        Check("c3 N5 facets: MobLookAtPlayer(name, offset) records Player.MobLookAtPlayer(<name>,<offset>); offset " +
              "0 by default",
              "Player.MobLookAtPlayer(MOB_G,1)|Player.MobLookAtPlayer(MOB_H,0)", string.Join("|", h.Calls));
        var k = new Fake();
        IQuestPlayer kp = ((IQuestContext)k).Player;
        kp.MobUpdateClaim("MOB_I", 1);
        kp.MobUpdateClaim("MOB_J");
        Check("c3 N5 facets: MobUpdateClaim(name, offset) records Player.MobUpdateClaim(<name>,<offset>); offset 0 " +
              "by default",
              "Player.MobUpdateClaim(MOB_I,1)|Player.MobUpdateClaim(MOB_J,0)", string.Join("|", k.Calls));
    }

    static bool C3FSetTarget(BattlefieldMobFake m, IBattlefieldMob target)
    {
        m.Target = target;
        return m.GetTarget() == target;
    }

    static bool C3FRankMet(BattlefieldPlayerFake p, int mission)
    {
        p.RankPointsMet.Add(mission);
        return p.MissionRankPoints(mission);
    }

    static uint SetLocalVarThen(this BattlefieldMobFake m, string name, uint value)
    {
        m.SetLocalVar(name, value);
        return m.LocalVar(name);
    }
}
