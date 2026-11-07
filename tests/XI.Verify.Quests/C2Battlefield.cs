// XI.Verify.Quests -- quest rework C2 Task 8: BattlefieldScript, the framework port (BattlefieldFramework) and the two
// plain battlefields, Cactuar Suave and Rank 2 Mission 2. The data of each against its Lua file's, the framework's
// requirement check on the Clotho Orb, every arm of the two skip-cutscene checks (the 32000 update keeps only number
// params, so a trace cannot see the boolean), Rank 2's entry requirement, and the framework arms the traces reach
// (the win's event and its battlefieldWin var, the essential mobs' end, the crate's loot, the exit tick count, the
// hook rules of Battlefield:register).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC2Battlefields()
    {
        Console.WriteLine(
            "\n== c2 battlefields: BattlefieldScript, the framework, Cactuar Suave and Rank 2 Mission 2 ==");
        RunC2CactuarSuave();
        RunC2Rank2Mission2();
        RunC2BattlefieldFramework();
        RunC2SaveTheChildrenBattlefield();
        RunC2TrialByEarthBattlefield();
        RunC2BattlefieldSubclassArms();
    }

    // the loot groups as "item:weight[:amount],... | ...", the groups in order
    static string C2BLoot(BattlefieldData d) =>
        string.Join(" | ", d.Loot.Select(g =>
            string.Join(",", g.Entries.Select(e => $"{e.ItemId}:{e.Weight}" + (e.Amount is { } a ? $":{a}" : ""))) +
            (g.Quantity is { } q ? $"x{q}" : "")));

    // a context of the battlefield's hook over fakes: a player in the zone, the npc by name, the trade (null: none)
    static (BattlefieldContextFake Q, BattlefieldPlayerFake Player) C2BContext(BattlefieldScript s, string npc,
                                                                            Dictionary<int, int> trade = null)
    {
        var player = new BattlefieldPlayerFake { ZoneId = s.Data.ZoneId };
        BattlefieldPlayerFake tradeFake = null;
        if (trade != null)
        {
            tradeFake = new BattlefieldPlayerFake();
            foreach (var kv in trade) tradeFake.TradeItems[kv.Key] = kv.Value;
        }
        var q = new BattlefieldContextFake(s)
        {
            Player = player, Npc = new BattlefieldNpcFake { Id = 1, Name = npc }, Trade = tradeFake,
        };
        return (q, player);
    }

    static void RunC2CactuarSuave()
    {
        var s = new QC.Battlefields.ChamberOfOracles.CactuarSuave();
        var d = s.Data;
        Check("c2 cactuar: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.CactuarSuave}|scripts/battlefields/Chamber_of_Oracles/cactuar_suave",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c2 cactuar: the Lua file's fields (zone, id, players, time, index, entry and exit NPCs)",
              "168 197 6 1800 5 SC_Entrance Shimmering_Circle",
              $"{d.ZoneId} {d.BattlefieldId} {d.MaxPlayers} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              string.Join(",", d.ExitNpcs));
        Check("c2 cactuar: Battlefield:new's defaults (no area, title, xp, cap; subjob, wipe grace, exp loss, timer; " +
              "no trusts, mission, debuff cleanup; delay 5; no key items; no crates)",
              "0 0 0 0 True True True True False False False 5 True True",
              $"{d.Area} {d.Title} {d.GrantXP} {d.LevelCap} {d.AllowSubjob} {d.HasWipeGrace} {d.CanLoseExp} " +
              $"{d.ShowTimer} {d.AllowTrusts} {d.IsMission} {d.CleanupDebuffs} {d.DelayToExit} " +
              $"{d.RequiredKeyItems == null} {d.ArmouryCrates == null}");
        Check("c2 cactuar: the Clotho Orb, worn with A_CRACK_HAS_FORMED, refused worn with ORB_IS_CRACKED",
              "1175 168:A_CRACK_HAS_FORMED 168:ORB_IS_CRACKED False",
              $"{string.Join(",", d.RequiredItems.Items)} {d.RequiredItems.WearMessage} " +
              $"{d.RequiredItems.WornMessage} " +
              $"{d.RequiredItems.Keep}");
        Check("c2 cactuar: the essential mobs, one group; no other groups", "Sabotender_Campeon,Sabotender_Amante|0",
              $"{string.Join(";", d.EssentialMobs.Select(m => string.Join(",", m)))}|{d.Groups.Count}");
        // the Lua file's content.loot, item ids and weights as the file has them
        Check("c2 cactuar: the nine loot groups with their weights",
              "65535:10000:24000 | 916:10000 | 1236:10000 | 17999:1875,18089:1875,17246:1875,17790:1875,658:625," +
              "837:625,836:625,1110:625 | 15152:2500,19025:2000,19024:1500,19026:1500,4174:1250,4175:1250 | " +
              "0:1500,18048:1500,18372:1500,17997:1500,17577:1500,4902:500,4613:500,4748:500,4659:500,4774:500 | " +
              "645:550,737:560,644:560,738:550,702:560,700:560,703:550,942:550,823:560,830:560,1132:550,887:550," +
              "902:550,866:550,895:560,1465:560,4172:560,4173:560 | 0:3600,645:400,737:400,644:400,738:400,702:400," +
              "700:400,703:400,942:400,823:400,830:400,1132:400,887:400,902:400,866:400,895:400,1465:400 | " +
              "0:3000,1110:435,836:435,658:435,837:435,942:1400,844:2450,1132:1400",
              C2BLoot(d));
        Check("c2 cactuar: overrides no hook; its coverage labels",
              "|essentialMobs:1,requiredItems:wear,requiredItems:worn,loot,exit:Shimmering_Circle",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));

        // the framework's checkRequirements (battlefield.lua:581) on the orb
        string Req(bool registrant, string npc = "SC_Entrance", Dictionary<int, int> trade = null)
        {
            var (q, _) = C2BContext(s, npc, trade);
            return s.CheckRequirements(q, registrant).ToString();
        }
        Check("c2 cactuar: checkRequirements refuses a registrant with no trade, trading another item, trading the " +
              "orb with more; accepts the orb alone, a helper with no trade; refuses another NPC",
              "False False False True True False",
              $"{Req(true)} {Req(true, trade: new() { [QC.Item.CactuarNeedle] = 1 })} " +
              $"{Req(true, trade: new() { [QC.Item.ClothoOrb] = 1, [QC.Item.CactuarNeedle] = 1 })} " +
              $"{Req(true, trade: new() { [QC.Item.ClothoOrb] = 1 })} {Req(false)} " +
              Req(true, "BC_Entrance", new() { [QC.Item.ClothoOrb] = 1 }));
        var (skipQ, _) = C2BContext(s, "SC_Entrance");
        Check("c2 cactuar: checkSkipCutscene is the framework's (false)", "False",
              s.CheckSkipCutscene(skipQ).ToString());
    }

    static void RunC2Rank2Mission2()
    {
        var s = new QC.Battlefields.WaughroonShrine.Rank2Mission2();
        var d = s.Data;
        Check("c2 rank2: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.Rank2Mission2}|scripts/battlefields/Waughroon_Shrine/rank_2_mission",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c2 rank2: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, entry and exit NPCs, essential mobs)",
              "144 64 False True True 6 MAX_LEVEL 900 0 BC_Entrance Burning_Circle Dark_Dragon,Seeker 0 0",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} " +
              $"{string.Join(";", d.EssentialMobs.Select(m => string.Join(",", m)))} {d.Loot.Count} {d.Groups.Count}");
        Check("c2 rank2: overrides entryRequirement and checkSkipCutscene; its coverage labels",
              "CheckSkipCutscene,EntryRequirement|hook:CheckSkipCutscene,hook:EntryRequirement,essentialMobs:1," +
              "exit:Burning_Circle",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));

        int sandoria = QC.MissionLog.Sandoria, windurst = QC.MissionLog.Windurst;
        int jtb2 = QC.Mission.Sandoria.JourneyToBastok2, ttkb2 = QC.Mission.Windurst.TheThreeKingdomsBastok2;
        BattlefieldContextFake Q(int nation, int sCurrent = 0, int wCurrent = 0, int status = 0,
                                 params string[] completed)
        {
            var (q, p) = C2BContext(s, "BC_Entrance");
            p.NationId = nation;
            p.CurrentMissions[sandoria] = sCurrent;
            p.CurrentMissions[windurst] = wCurrent;
            p.MissionStatuses[nation] = status;
            foreach (var c in completed) p.CompletedMissions.Add(c);
            return q;
        }
        string Skip(BattlefieldContextFake q) => s.CheckSkipCutscene(q).ToString();
        // battlefield checkSkipCutscene: either completed; or the nation's status above 9 with either current
        Check("c2 rank2: checkSkipCutscene: Windurst's 2-2 Bastok completed; San d'Oria's completed; neither",
              "True True False",
              $"{Skip(Q(windurst, completed: $"{windurst}/{ttkb2}"))} " +
              $"{Skip(Q(sandoria, completed: $"{sandoria}/{jtb2}"))} " +
              Skip(Q(sandoria)));
        Check("c2 rank2: checkSkipCutscene: status 10 with Windurst's current; with San d'Oria's current; status 9 " +
              "with it current; status 10 with neither current",
              "True True False False",
              $"{Skip(Q(windurst, wCurrent: ttkb2, status: 10))} {Skip(Q(sandoria, sCurrent: jtb2, status: 10))} " +
              $"{Skip(Q(sandoria, sCurrent: jtb2, status: 9))} {Skip(Q(sandoria, status: 10))}");
        Check("c2 rank2: checkSkipCutscene reads the nation's status, not the current mission's log (a Windurstian " +
              "on San d'Oria's 2-2 at status 10 skips only with Windurst's status above 9)",
              "False True",
              $"{Skip(Q(windurst, sCurrent: jtb2, status: 0))} {Skip(Q(windurst, sCurrent: jtb2, status: 10))}");

        string Entry(BattlefieldContextFake q, bool registrant) => s.EntryRequirement(q, registrant).ToString();
        Check("c2 rank2: entryRequirement: the registrant on either mission at status 10; at status 9; a helper with " +
              "the nation's mission 5 completed; a registrant with only that; a helper with neither",
              "True True False True False False",
              $"{Entry(Q(sandoria, sCurrent: jtb2, status: 10), true)} " +
              $"{Entry(Q(windurst, wCurrent: ttkb2, status: 10), true)} " +
              $"{Entry(Q(sandoria, sCurrent: jtb2, status: 9), true)} " +
              $"{Entry(Q(sandoria, completed: $"{sandoria}/5"), false)} " +
              $"{Entry(Q(sandoria, completed: $"{sandoria}/5"), true)} {Entry(Q(sandoria), false)}");
        var (rq, _) = C2BContext(s, "BC_Entrance");
        ((BattlefieldPlayerFake)rq.Player).CurrentMissions[sandoria] = jtb2;
        ((BattlefieldPlayerFake)rq.Player).MissionStatuses[0] = 10;
        Check("c2 rank2: checkRequirements reaches the override (no items, no trade); refuses a trade", "True False",
              $"{s.CheckRequirements(rq, true)} " +
              s.CheckRequirements(C2BWithTrade(rq, s), true));
    }

    // the context with a trade of one Cactuar Needle
    static BattlefieldContextFake C2BWithTrade(BattlefieldContextFake q, BattlefieldScript s)
    {
        var trade = new BattlefieldPlayerFake();
        trade.TradeItems[QC.Item.CactuarNeedle] = 1;
        return new BattlefieldContextFake(s) { Player = q.Player, Npc = q.Npc, Trade = trade };
    }

    static void RunC2BattlefieldFramework()
    {
        var s = new QC.Battlefields.ChamberOfOracles.CactuarSuave();
        var bf = new BattlefieldViewFake { Id = 197, Area = 2, TimeInside = 300, RecordValue = ("Meme", 250, 3) };
        var (q, p) = C2BContext(s, "Shimmering_Circle");
        q.Battlefield = bf;
        p.CharId = 7;
        s.OnBattlefieldLeave(q, BattlefieldConst.LeaveWon);
        Check("c2 framework: a win (leavecode 2) sets battlefieldWin to the battlefield id, then plays 32001 with " +
              "the area, the record, the time inside, the zone and the index",
              "Player.SetLocalVar(battlefieldWin,197),Player.StartEvent(32001,2,250,3,300,168,5,0)",
              string.Join(",", p.Calls));
        p.Calls.Clear();
        s.OnBattlefieldLeave(q, BattlefieldConst.LeaveLost);
        s.OnBattlefieldLeave(q, BattlefieldConst.LeaveExit);
        Check("c2 framework: a loss plays 32002 with the empty lossEventParams; an exit nothing",
              "Player.StartEvent(32002,{})", string.Join(",", p.Calls));

        // the essential group's end (handleAllMonstersDefeated) with no crate: the exit delay and the win
        var bf2 = new BattlefieldViewFake { Id = 197 };
        var (q2, _) = C2BContext(s, "SC_Entrance");
        q2.Battlefield = bf2;
        s.HandleAllMonstersDefeated(q2, null);
        Check("c2 framework: all essential mobs dead with no crate: cutsceneTimer 5, then status WON",
              "Battlefield.SetLocalVar(cutsceneTimer,5),Battlefield.SetStatus(2)", string.Join(",", bf2.Calls));
        // with the engine's crate
        var bf3 = new BattlefieldViewFake { Id = 197, ArmouryCrate = 900 };
        var (q3, _) = C2BContext(s, "SC_Entrance");
        q3.Battlefield = bf3;
        var engine = (BattlefieldEngineFake)q3.Engine;
        engine.Npcs[900] = new BattlefieldNpcFake { Id = 900, TargId = 33, Name = "Armoury_Crate" };
        s.HandleAllMonstersDefeated(q3, null);
        Check("c2 framework: all essential mobs dead with the crate: inserted, shown, its trigger listened to",
              "Battlefield.InsertEntity(33,False,True)|ShowCrate(900),AddCrateListener(900)",
              $"{string.Join(",", bf3.Calls)}|{string.Join(",", engine.Calls)}");

        // the crate opened: the loot rolls (gil split over the players with the moghancement bonus), then the win
        var a = new BattlefieldPlayerFake { CharId = 1 };
        var b = new BattlefieldPlayerFake { CharId = 2 };
        a.Mods[BattlefieldConst.ModMoghancementGilBonusP] = 10;
        var bf4 = new BattlefieldViewFake { Id = 197 };
        bf4.PlayerList.Add(a);
        bf4.PlayerList.Add(b);
        a.CurrentBattlefield = bf4;
        var q4 = new BattlefieldContextFake(s) { Player = a };
        var e4 = (BattlefieldEngineFake)q4.Engine;
        e4.Loot.Add((QC.Item.Gil, 24000));
        e4.Loot.Add((QC.Item.CactuarNeedle, null));
        var crate = new BattlefieldNpcFake { Id = 900 };
        s.HandleOpenArmouryCrate(q4, crate);
        Check("c2 framework: the crate opened: one selectFromLootGroups, the gil split (24000 * 1.1 / 2, in " +
              "doubles as the Lua) to each, the item to the first player's pool, then WON and cutsceneTimer 5; the " +
              "loot label reached",
              "OpenCrate(900),SelectFromLootGroups,GiveCurrency(1,gil,13200.000000000002,True)," +
              "GiveCurrency(2,gil,13200.000000000002,True)|" +
              "Player.AddTreasure(916,900)|Battlefield.SetStatus(2),Battlefield.SetLocalVar(cutsceneTimer,5)|loot",
              $"{string.Join(",", e4.Calls)}|{string.Join(",", a.Calls)}|{string.Join(",", bf4.Calls)}|" +
              string.Join(",", q4.ReachedLabels));
        Check("c2 framework: the crate's loot rolls draw from self.loot", "True",
              (e4.LootTables.Count == 1 && ReferenceEquals(e4.LootTables[0], q4.Self.Loot)).ToString());

        // an override passing a pool of its own (as automaton_assault.lua:151 and pulling_the_strings.lua:202 do)
        // draws from that pool, not from self.loot
        var own = new C2OwnPoolBattlefield();
        var ownPlayer = new BattlefieldPlayerFake { CharId = 3 };
        var bfOwn = new BattlefieldViewFake { Id = 197 };
        bfOwn.PlayerList.Add(ownPlayer);
        ownPlayer.CurrentBattlefield = bfOwn;
        var qOwn = new BattlefieldContextFake(own) { Player = ownPlayer };
        var eOwn = (BattlefieldEngineFake)qOwn.Engine;
        own.HandleOpenArmouryCrate(qOwn, new BattlefieldNpcFake { Id = 901 });
        bool givenPool = eOwn.LootTables.Count == 1 &&
                         ReferenceEquals(eOwn.LootTables[0].Groups, C2OwnPoolBattlefield.Pool);
        Check("c2 framework: an override's own pool: selectFromLootGroups given that pool, its item to the pool",
              "True|Player.AddTreasure(4242,901)", $"{givenPool}|{string.Join(",", ownPlayer.Calls)}");

        // the exit ticks: a won battlefield with cutsceneTimer 5 counts down and cleans up on the tick it reads 0
        var bf5 = new BattlefieldViewFake { Id = 197, Status = BattlefieldConst.StatusWon };
        bf5.LocalVars["cutsceneTimer"] = 5;
        bf5.PlayerList.Add(new BattlefieldPlayerFake { ZoneId = 168 });
        var q5 = new BattlefieldContextFake(s) { Battlefield = bf5 };
        int ticks = 0;
        while (!bf5.Calls.Contains("Battlefield.Cleanup(True)") && ticks < 20)
        {
            s.OnBattlefieldTick(q5, ticks);
            ticks++;
        }
        Check("c2 framework: cutsceneTimer 5 cleans up on the 6th tick, the timer then at -1", "6 -1",
              $"{ticks} {bf5.LocalVar("cutsceneTimer")}");

        // the entry update (onEntryEventUpdate) for the registrant trading the orb: registers, enters (the
        // battlefield then its own, so the initiator's block runs), keeps the orb (it is worn, not traded away),
        // registers the party member in the zone, and replies with the index, the record and the skip flag
        var bf6 = new BattlefieldViewFake
        {
            Id = 197, InitiatorValue = (7, "Me"), RecordValue = ("Rec", 300, 3),
        };
        var (q6, p6) = C2BContext(s, "SC_Entrance");
        p6.CharId = 7;
        p6.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
        p6.EffectPowers[BattlefieldConst.EffectBattlefield] = 197;
        p6.EnterTarget = bf6;
        var member6 = new BattlefieldPlayerFake { CharId = 8, ZoneId = s.Data.ZoneId };
        var away6 = new BattlefieldPlayerFake { CharId = 9, ZoneId = s.Data.ZoneId + 1 };
        p6.Members = new List<IBattlefieldPlayer> { p6, member6, away6 };
        int entered = s.OnEntryEventUpdate(q6, 32000, (5 << 4) | 1);
        Check("c2 framework: the entry update registers area 1, keeps the worn orb (no tradeComplete), registers " +
              "the member in the zone, and replies CUTSCENE with index 5, the record and no skip",
              "1|Player.RegisterBattlefield(197,1,7),Player.EnterBattlefield()," +
              "Player.UpdateEvent(2,5,0,300,3,False,0,0)," +
              "Player.UpdateEventString(Rec)|" +
              "Player.CopyStatusEffect(197),Player.RegisterBattlefield(197,1,7)|",
              $"{entered}|{string.Join(",", p6.Calls)}|{string.Join(",", member6.Calls)}|" +
              string.Join(",", away6.Calls));

        // the hook rules of Battlefield:register (battlefield.lua:481)
        var earlier = new List<(int, IReadOnlyList<string>, bool)>();
        var first = BattlefieldFramework.RegisterHooks(earlier, 197, new[] { "SC_Entrance" });
        earlier.Add((197, new[] { "SC_Entrance" }, true));
        var second = BattlefieldFramework.RegisterHooks(earlier, 200, new[] { "SC_Entrance" });
        var other = BattlefieldFramework.RegisterHooks(earlier, 201, new[] { "Other_Entrance" });
        var reload = BattlefieldFramework.RegisterHooks(earlier, 197, new[] { "SC_Entrance" });
        Check("c2 framework: register's hooks: the first takes both; a later one with the same entry neither; with " +
              "another entry the entry hooks only; a reload of the first both",
              "(True, True) (False, False) (False, True) (True, True)", $"{first} {second} {other} {reload}");
    }
}

internal static partial class Program
{
    // a mission battlefield's context over fakes: the San d'Oria log's current mission and status, 1-3 completed, the
    // char's nation, at the npc
    static (BattlefieldContextFake Q, BattlefieldPlayerFake P) C2BMission(BattlefieldScript s, int current,
                                                                          int status, bool completed = false,
                                                                          int nation = 0, string npc = "Hut_Door")
    {
        var (q, p) = C2BContext(s, npc);
        p.NationId = nation;
        p.CurrentMissions[QC.MissionLog.Sandoria] = current;
        p.MissionStatuses[QC.MissionLog.Sandoria] = status;
        if (completed) p.CompletedMissions.Add($"{QC.MissionLog.Sandoria}/{QC.Mission.Sandoria.SaveTheChildren}");
        return (q, p);
    }

    // a win (onBattlefieldWin) on the context: the player's calls
    static string C2BWin(BattlefieldScript s, BattlefieldContextFake q, BattlefieldPlayerFake p)
    {
        q.Battlefield = new BattlefieldViewFake
        {
            Id = s.Data.BattlefieldId, Area = 1, TimeInside = 300, RecordValue = ("Meme", 250, 3),
        };
        s.OnBattlefieldWin(q);
        return string.Join(",", p.Calls);
    }

    static void RunC2SaveTheChildrenBattlefield()
    {
        var s = new QC.Battlefields.GhelsbaOutpost.SaveTheChildren();
        var d = s.Data;
        var m = (BattlefieldMissionData)d;
        Check("c2 save the children bf: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.SaveTheChildren}|scripts/battlefields/Ghelsba_Outpost/save_the_children",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c2 save the children bf: the Lua file's fields (zone, id, mission, no exp loss, trusts, players, " +
              "MAX_LEVEL, time, index, area 1, Hut_Door, no exit NPC)",
              "140 32 True False True 6 MAX_LEVEL 600 0 1 Hut_Door True",
              $"{d.ZoneId} {d.BattlefieldId} {d.IsMission} {d.CanLoseExp} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {d.Area} {string.Join(",", d.EntryNpcs)} " +
              $"{d.ExitNpcs == null}");
        Check("c2 save the children bf: BattlefieldMission:new's fields (San d'Oria's 1-3, the status from the San " +
              "d'Oria log, the whole status word, required 2, skip defaulting to 2, no requiredVar)",
              "Sandoria 2 Sandoria - 2 2 - 0",
              $"{m.MissionArea} {m.Mission} {m.MissionStatusArea} {m.MissionStatus?.ToString() ?? "-"} " +
              $"{m.RequiredMissionStatus} {m.SkipStatus} {m.RequiredVar ?? "-"} {m.RequiredValue}");
        Check("c2 save the children bf: one group, the three Fodderchiefs by id, with its allDeath; no essential mobs",
              "FODDERCHIEF_VOKDEK,FODDERCHIEF_VOKDEK+1,FODDERCHIEF_VOKDEK+2|True|0|1",
              $"{string.Join(",", d.Groups[0].MobIds)}|{d.Groups[0].AllDeath != null}|{d.EssentialMobs.Count}|" +
              d.Groups.Count);
        Check("c2 save the children bf: overrides no hook; its coverage labels", "|group:1:allDeath",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));
        var bfDeath = new BattlefieldViewFake { Id = 32 };
        d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = bfDeath }, null);
        Check("c2 save the children bf: the group's allDeath: the battlefield WON", "Battlefield.SetStatus(2)",
              string.Join(",", bfDeath.Calls));

        const int stc = QC.Mission.Sandoria.SaveTheChildren, none = QC.Mission.Sandoria.None;
        string Req(bool registrant, int current, int status, bool completed = false, int nation = 0)
        {
            var (q, _) = C2BMission(s, current, status, completed, nation);
            return s.CheckRequirements(q, registrant).ToString();
        }
        // BattlefieldMission:checkRequirements (battlefield.lua:1425), the requiredMissionStatus arm
        Check("c2 save the children bf: checkRequirements, a registrant: 1-3 current at status 2; at 1; at 3; " +
              "another mission current at 2; 1-3 completed, not current",
              "True False False False False",
              $"{Req(true, stc, 2)} {Req(true, stc, 1)} {Req(true, stc, 3)} {Req(true, none, 2)} " +
              Req(true, none, 0, completed: true));
        Check("c2 save the children bf: checkRequirements, a helper: 1-3 completed; current at 4; at 2; at 1; neither",
              "True True True False False",
              $"{Req(false, none, 0, completed: true)} {Req(false, stc, 4)} {Req(false, stc, 2)} " +
              $"{Req(false, stc, 1)} " +
              Req(false, none, 0));
        var (bq, bp) = C2BMission(s, stc, 0, nation: 1);
        bp.MissionStatuses[QC.MissionLog.Bastok] = 2;
        var (bq2, bp2) = C2BMission(s, stc, 2, nation: 1);
        bp2.MissionStatuses[QC.MissionLog.Bastok] = 0;
        Check("c2 save the children bf: checkRequirements reads the San d'Oria log (missionArea, missionStatusArea), " +
              "not the nation's: a Bastokan with San d'Oria's 1-3 at 2 passes; at 0 with Bastok's status 2 fails",
              "True False", $"{s.CheckRequirements(bq2, true)} {s.CheckRequirements(bq, true)}");
        var (tq, _) = C2BMission(s, stc, 2);
        var (nq, _) = C2BMission(s, stc, 2, npc: "Other_Door");
        Check("c2 save the children bf: checkRequirements runs Battlefield's first: a trade, another NPC refused",
              "False False", $"{s.CheckRequirements(C2BWithTrade(tq, s), true)} {s.CheckRequirements(nq, true)}");

        string Skip(int current, int status, bool completed = false)
        {
            var (q, _) = C2BMission(s, current, status, completed);
            return s.CheckSkipCutscene(q).ToString();
        }
        // BattlefieldMission:checkSkipCutscene (battlefield.lua:1458): invisible to every trace (updateEvent keeps
        // only number params)
        Check("c2 save the children bf: checkSkipCutscene: 1-3 completed; current at status 3 (above skip 2); " +
              "current at 2; not current at 3",
              "True True False False",
              $"{Skip(none, 0, completed: true)} {Skip(stc, 3)} {Skip(stc, 2)} {Skip(none, 3)}");

        // BattlefieldMission:onBattlefieldWin (battlefield.lua:1470)
        string Win(int current, int status, bool completed = false)
        {
            var (q, p) = C2BMission(s, current, status, completed);
            return C2BWin(s, q, p);
        }
        Check("c2 save the children bf: the win, 1-3 current at status 2: battlefieldWin 32, then 32001 with " +
              "canSkipCS 0",
              "Player.SetLocalVar(battlefieldWin,32),Player.StartEvent(32001,1,250,3,300,140,0,0)", Win(stc, 2));
        Check("c2 save the children bf: the win, 1-3 completed and not current: no battlefieldWin, canSkipCS 1",
              "Player.StartEvent(32001,1,250,3,300,140,0,1)", Win(none, 0, completed: true));
        Check("c2 save the children bf: the win, current at 4 (above skip 2): battlefieldWin, canSkipCS 1; " +
              "completed and current again at 2: battlefieldWin, canSkipCS 1",
              "Player.SetLocalVar(battlefieldWin,32),Player.StartEvent(32001,1,250,3,300,140,0,1)|" +
              "Player.SetLocalVar(battlefieldWin,32),Player.StartEvent(32001,1,250,3,300,140,0,1)",
              $"{Win(stc, 4)}|{Win(stc, 2, completed: true)}");
        var (lq, lp) = C2BMission(s, stc, 2);
        lq.Battlefield = new BattlefieldViewFake
        {
            Id = 32, Area = 1, TimeInside = 300, RecordValue = ("Meme", 250, 3),
        };
        s.OnBattlefieldLeave(lq, BattlefieldConst.LeaveWon);
        Check("c2 save the children bf: Battlefield:onBattlefieldLeave's win reaches BattlefieldMission's",
              "Player.SetLocalVar(battlefieldWin,32),Player.StartEvent(32001,1,250,3,300,140,0,0)",
              string.Join(",", lp.Calls));
        var (fq, fp) = C2BMission(s, stc, 2);
        s.OnEventFinishWin(fq, 32001, 0);
        Check("c2 save the children bf: BattlefieldMission:onEventFinishWin, no title and no grantXP: nothing", "",
              string.Join(",", fp.Calls));
    }

    // a quest battlefield's context over fakes: Bastok's Trial by Earth's status, the fork held
    static (BattlefieldContextFake Q, BattlefieldPlayerFake P) C2BQuest(BattlefieldScript s, QuestStatus status,
                                                                        bool fork = true)
    {
        var (q, p) = C2BContext(s, "EP_Entrance");
        p.QuestStatuses[$"{QuestLog.Bastok}/TRIAL_BY_EARTH"] = status;
        if (status == QuestStatus.Completed) p.CompletedQuests.Add($"{QuestLog.Bastok}/TRIAL_BY_EARTH");
        if (fork) p.KeyItems[QC.KeyItem.TuningForkOfEarth] = true;
        return (q, p);
    }

    static void RunC2TrialByEarthBattlefield()
    {
        var s = new QC.Battlefields.CloisterOfTremors.TrialByEarth();
        var d = s.Data;
        var qd = (BattlefieldQuestData)d;
        Check("c2 trial by earth bf: validates clean, its key and LegacyPath",
              $"|{QC.BattlefieldKey.TrialByEarth}|scripts/battlefields/Cloister_of_Tremors/trial_by_earth",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check("c2 trial by earth bf: the Lua file's fields (zone, id, no exp loss, not a mission, players, time, " +
              "index, no area, entry and exit NPCs, the Tuning Fork of Earth)",
              "209 576 False False 6 1800 0 0 EP_Entrance Earth_Protocrystal 329",
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.MaxPlayers} {d.TimeLimit} {d.Index} " +
              $"{d.Area} {string.Join(",", d.EntryNpcs)} {string.Join(",", d.ExitNpcs)} " +
              string.Join(",", d.RequiredKeyItems.Items.Select(k => string.Join("/", k.Ids))));
        Check("c2 trial by earth bf: BattlefieldQuest:new's fields (Bastok's TRIAL_BY_EARTH, not repeatable, no " +
              "requiredVar)",
              "Bastok TRIAL_BY_EARTH False -", $"{qd.QuestArea} {qd.Quest} {qd.Repeatable} {qd.RequiredVar ?? "-"}");
        Check("c2 trial by earth bf: one group, Titan Prime by name, with its allDeath; no essential mobs",
              "Titan_Prime_TBE|True|0|1",
              $"{string.Join(",", d.Groups[0].Mobs)}|{d.Groups[0].AllDeath != null}|{d.EssentialMobs.Count}|" +
              d.Groups.Count);
        Check("c2 trial by earth bf: overrides no hook; its coverage labels",
              "|group:1:allDeath,requiredKeyItems,exit:Earth_Protocrystal",
              $"{string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides))}|" +
              string.Join(",", s.CoverageLabels()));

        string Req(bool registrant, QuestStatus status, bool fork = true)
        {
            var (q, _) = C2BQuest(s, status, fork);
            return s.CheckRequirements(q, registrant).ToString();
        }
        // BattlefieldQuest:checkRequirements (battlefield.lua:1534), the plain arm: accepted or more
        Check("c2 trial by earth bf: checkRequirements with the fork: accepted; completed; available; a helper " +
              "accepted, available; accepted without the fork",
              "True True False True False False",
              $"{Req(true, QuestStatus.Accepted)} {Req(true, QuestStatus.Completed)} " +
              $"{Req(true, QuestStatus.Available)} {Req(false, QuestStatus.Accepted)} " +
              $"{Req(false, QuestStatus.Available)} {Req(true, QuestStatus.Accepted, fork: false)}");
        string Skip(QuestStatus status)
        {
            var (q, _) = C2BQuest(s, status);
            return s.CheckSkipCutscene(q).ToString();
        }
        // BattlefieldQuest:checkSkipCutscene (battlefield.lua:1557): invisible to every trace
        Check("c2 trial by earth bf: checkSkipCutscene: completed; accepted; available", "True False False",
              $"{Skip(QuestStatus.Completed)} {Skip(QuestStatus.Accepted)} {Skip(QuestStatus.Available)}");
        string Win(QuestStatus status)
        {
            var (q, p) = C2BQuest(s, status);
            return C2BWin(s, q, p);
        }
        // BattlefieldQuest:onBattlefieldWin (battlefield.lua:1562), repeatable false
        Check("c2 trial by earth bf: the win: accepted: battlefieldWin 576, canSkipCS 0; completed (not " +
              "repeatable): none, canSkipCS 1; available: none, canSkipCS 1",
              "Player.SetLocalVar(battlefieldWin,576),Player.StartEvent(32001,1,250,3,300,209,0,0)|" +
              "Player.StartEvent(32001,1,250,3,300,209,0,1)|Player.StartEvent(32001,1,250,3,300,209,0,1)",
              $"{Win(QuestStatus.Accepted)}|{Win(QuestStatus.Completed)}|{Win(QuestStatus.Available)}");
        var (fq, fp) = C2BQuest(s, QuestStatus.Accepted);
        s.OnEventFinishWin(fq, 32001, 0);
        Check("c2 trial by earth bf: onEventFinishWin is Battlefield's (no title, no XP): nothing", "",
              string.Join(",", fp.Calls));
    }

    // the arms of BattlefieldMission and BattlefieldQuest no proof battlefield's data takes (Task 6's "Unproven
    // until C3"): requiredVar, the plain arm, a status position, the nation's log, title and grantXP, repeatable
    static void RunC2BattlefieldSubclassArms()
    {
        // BattlefieldMission with requiredVar (the CoP form), the mission in the nation's log (no missionArea)
        var v = new C2SubclassBattlefield(new BattlefieldMissionData
        {
            Mission = 5, RequiredVar = "[C2]Var", RequiredValue = 3,
        });
        BattlefieldContextFake V(int nation, int current, int var, bool completed = false)
        {
            var (q, p) = C2BContext(v, "C2_Entrance");
            p.NationId = nation;
            p.CurrentMissions[nation] = current;
            p.CharVars["[C2]Var"] = var;
            if (completed) p.CompletedMissions.Add($"{nation}/5");
            return q;
        }
        Check("c2 bf mission: the requiredVar arm, checkRequirements: a registrant current with the var at 3; at 4; " +
              "a helper current at 4; a helper completed; a registrant current at 2",
              "True False True True False",
              $"{v.CheckRequirements(V(2, 5, 3), true)} {v.CheckRequirements(V(2, 5, 4), true)} " +
              $"{v.CheckRequirements(V(2, 5, 4), false)} {v.CheckRequirements(V(2, 0, 0, completed: true), false)} " +
              v.CheckRequirements(V(2, 5, 2), true));
        Check("c2 bf mission: the requiredVar arm, checkSkipCutscene: current with the var at 4; at 3; not current " +
              "at 4; the mission read from the nation's log (Windurst's current, a San d'Orian)",
              "True False False False",
              $"{v.CheckSkipCutscene(V(2, 5, 4))} {v.CheckSkipCutscene(V(2, 5, 3))} " +
              $"{v.CheckSkipCutscene(V(2, 0, 4))} {C2BSkipOtherLog(v)}");
        var vw = V(2, 5, 4);
        var vw3 = V(2, 5, 3);
        Check("c2 bf mission: the requiredVar arm of the win's canSkipCS: current with the var at 4: battlefieldWin, " +
              "canSkipCS 1; at 3: canSkipCS 0",
              "Player.SetLocalVar(battlefieldWin,197),Player.StartEvent(32001,1,250,3,300,168,0,1)|" +
              "Player.SetLocalVar(battlefieldWin,197),Player.StartEvent(32001,1,250,3,300,168,0,0)",
              $"{C2BWin(v, vw, (BattlefieldPlayerFake)vw.Player)}|{C2BWin(v, vw3, (BattlefieldPlayerFake)vw3.Player)}");

        // the plain arm: neither requiredMissionStatus nor requiredVar
        var plain = new C2SubclassBattlefield(new BattlefieldMissionData
        {
            MissionArea = MissionLog.Zilart, Mission = 4,
        });
        BattlefieldContextFake P(int current, bool completed)
        {
            var (q, p) = C2BContext(plain, "C2_Entrance");
            p.CurrentMissions[(int)MissionLog.Zilart] = current;
            if (completed) p.CompletedMissions.Add($"{(int)MissionLog.Zilart}/4");
            return q;
        }
        Check("c2 bf mission: the plain arm: a registrant current; a registrant completed, not current; a helper " +
              "completed; skip completed; skip current (no status arm)",
              "True False True True False",
              $"{plain.CheckRequirements(P(4, false), true)} {plain.CheckRequirements(P(0, true), true)} " +
              $"{plain.CheckRequirements(P(0, true), false)} {plain.CheckSkipCutscene(P(0, true))} " +
              plain.CheckSkipCutscene(P(4, false)));

        // a status position (missionStatus), its status read from the nation's log, with its own skip status
        var pos = new C2SubclassBattlefield(new BattlefieldMissionData
        {
            MissionArea = MissionLog.CoP, Mission = 118, MissionStatus = 2, RequiredMissionStatus = 1,
            SkipMissionStatus = 3,
        });
        BattlefieldContextFake S(int field, int nation = 0)
        {
            var (q, p) = C2BContext(pos, "C2_Entrance");
            p.NationId = nation;
            p.CurrentMissions[(int)MissionLog.CoP] = 118;
            p.MissionStatuses[nation] = field << 8;
            return q;
        }
        Check("c2 bf mission: a status position: field 2 of the nation's status at 1 enters; at 0 not; skip at 4 " +
              "(above skipMissionStatus 3); not at 3; Bastok's status read for a Bastokan",
              "True False True False True",
              $"{pos.CheckRequirements(S(1), true)} {pos.CheckRequirements(S(0), true)} " +
              $"{pos.CheckSkipCutscene(S(4))} " +
              $"{pos.CheckSkipCutscene(S(3))} {pos.CheckRequirements(S(1, nation: 1), true)}");

        // BattlefieldMission:onEventFinishWin: the title, the XP once per JST midnight
        var xp = new C2SubclassBattlefield(new BattlefieldMissionData { Mission = 1, Title = 7, GrantXP = 500 });
        string Finish(int xpVar)
        {
            var (q, p) = C2BContext(xp, "C2_Entrance");
            p.Now = 1000;
            p.NextJstMidnight = 5000;
            p.CharVars["Battlefield[197]XP"] = xpVar;
            xp.OnEventFinishWin(q, 32001, 0);
            return string.Join(",", p.Calls);
        }
        Check("c2 bf mission: onEventFinishWin: the title; the XP when the var is not past now (then the var to the " +
              "next JST midnight); no XP when it is",
              "Player.AddTitle(7),Player.SetVar(Battlefield[197]XP,5000),Player.AddExp(500)|Player.AddTitle(7)",
              $"{Finish(1000)}|{Finish(1001)}");

        // BattlefieldQuest with repeatable (HEAD's battlefield.lua, the user's ruling of 2026-11-05)
        var rep = new C2SubclassBattlefield(new BattlefieldQuestData
        {
            QuestArea = QuestLog.Bastok, Quest = "TRIAL_BY_EARTH", Repeatable = true,
        });
        string RepWin(QuestStatus status)
        {
            var (q, p) = C2BQuest(rep, status);
            return C2BWin(rep, q, p);
        }
        Check("c2 bf quest: repeatable: the win completed: battlefieldWin, canSkipCS 1; accepted: battlefieldWin, " +
              "canSkipCS 0; available: none, canSkipCS 1",
              "Player.SetLocalVar(battlefieldWin,197),Player.StartEvent(32001,1,250,3,300,168,0,1)|" +
              "Player.SetLocalVar(battlefieldWin,197),Player.StartEvent(32001,1,250,3,300,168,0,0)|" +
              "Player.StartEvent(32001,1,250,3,300,168,0,1)",
              $"{RepWin(QuestStatus.Completed)}|{RepWin(QuestStatus.Accepted)}|{RepWin(QuestStatus.Available)}");

        // BattlefieldQuest with requiredVar
        var qv = new C2SubclassBattlefield(new BattlefieldQuestData
        {
            QuestArea = QuestLog.Bastok, Quest = "TRIAL_BY_EARTH", RequiredVar = "[C2]QVar", RequiredValue = 2,
        });
        string QReq(bool registrant, QuestStatus status, int var)
        {
            var (q, p) = C2BQuest(qv, status);
            p.CharVars["[C2]QVar"] = var;
            ((BattlefieldNpcFake)q.Npc).Name = "C2_Entrance";
            return qv.CheckRequirements(q, registrant).ToString();
        }
        Check("c2 bf quest: the requiredVar arm: a registrant accepted at 2; at 3; a helper accepted at 3; a helper " +
              "completed; a registrant available at 2",
              "True False True True False",
              $"{QReq(true, QuestStatus.Accepted, 2)} {QReq(true, QuestStatus.Accepted, 3)} " +
              $"{QReq(false, QuestStatus.Accepted, 3)} {QReq(false, QuestStatus.Completed, 0)} " +
              QReq(true, QuestStatus.Available, 2));

        // the Data type picks the class: a plain battlefield's win and skip are Battlefield's
        var plainBf = new C2SubclassBattlefield(new BattlefieldData());
        var (pq, pp) = C2BContext(plainBf, "C2_Entrance");
        Check("c2 bf: a plain BattlefieldData takes Battlefield's methods: skip false, the win's battlefieldWin " +
              "always and canSkipCS 0",
              "False|Player.SetLocalVar(battlefieldWin,197),Player.StartEvent(32001,1,250,3,300,168,0,0)",
              $"{plainBf.CheckSkipCutscene(pq)}|{C2BWin(plainBf, pq, pp)}");

        // the validator: a mission battlefield needs its mission (0 is a real id); mobIds' shape follows the area
        // (addGroups reads one list per area without a fixed area, one flat list with it)
        string Problems(BattlefieldData d) => string.Join(" | ",
            BattlefieldScriptValidator.Validate(new C2SubclassBattlefield(d)));
        BattlefieldGroup Ids() => new() { MobIds = new[] { new BattlefieldMobId("C2_MOB") } };
        BattlefieldGroup AreaIds() => new() { AreaMobIds = new[] { new[] { new BattlefieldMobId("C2_MOB") } } };
        Check("c2 bf validator: a mission battlefield without its mission refused; mission 0 accepted; flat mobIds " +
              "without a fixed area refused; with area 1 accepted; AreaMobIds with area 1 refused; without accepted",
              "no mission||group 1: mobIds without a fixed area (AreaMobIds, one list per area)||" +
              "group 1: AreaMobIds with a fixed area 1 (mobIds, one flat list)|",
              $"{Problems(new BattlefieldMissionData())}|{Problems(new BattlefieldMissionData { Mission = 0 })}|" +
              $"{Problems(new BattlefieldData { Groups = { Ids() } })}|" +
              $"{Problems(new BattlefieldData { Area = 1, Groups = { Ids() } })}|" +
              $"{Problems(new BattlefieldData { Area = 1, Groups = { AreaIds() } })}|" +
              Problems(new BattlefieldData { Groups = { AreaIds() } }));
    }

    // checkSkipCutscene of a mission with no missionArea for a San d'Orian whose Windurst log has the mission
    static bool C2BSkipOtherLog(BattlefieldScript s)
    {
        var (q, p) = C2BContext(s, "C2_Entrance");
        p.NationId = 0;
        p.CurrentMissions[2] = 5;
        p.CharVars["[C2]Var"] = 4;
        return s.CheckSkipCutscene(q);
    }
}

// a test battlefield over the data given (zone 168, id 197, index 0, entered at C2_Entrance): the arms of
// BattlefieldMission and BattlefieldQuest the proof battlefields leave untaken
internal sealed class C2SubclassBattlefield : BattlefieldScript
{
    public C2SubclassBattlefield(BattlefieldData data) : base("C2_SUBCLASS")
    {
        data.ZoneId = 168;
        data.BattlefieldId = 197;
        data.MaxPlayers = 6;
        data.TimeLimit = 600;
        data.EntryNpcs = new[] { "C2_Entrance" };
        Data = data;
    }

    public override BattlefieldData Data { get; }
}

// a battlefield whose crate's loot rolls draw from a pool of its own, as automaton_assault.lua:151 passes its
// lootPool to handleLootRolls
internal sealed class C2OwnPoolBattlefield : BattlefieldScript
{
    public C2OwnPoolBattlefield() : base("C2_OWN_POOL") { }

    public static readonly List<BattlefieldLootGroup> Pool = new() { new BattlefieldLootGroup { { 4242, 1000 } } };

    public override BattlefieldData Data { get; } = new BattlefieldData
    {
        ZoneId = 168, BattlefieldId = 197, Loot = { new BattlefieldLootGroup { { 1111, 1000 } } },
    };

    public override void HandleLootRolls(IBattlefieldContext q, IBattlefieldView battlefield,
                                         BattlefieldLootTable lootTable, IBattlefieldNpc npc, int gilBonusMod) =>
        q.Framework.HandleLootRolls(battlefield, new BattlefieldLootTable(Pool), npc, gilBonusMod);
}
