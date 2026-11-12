// XI.Verify.Quests -- quest rework C3, branch Q3a (Quest battlefields): the four Beyond Infinity arenas, Holy Crest,
// Return to the Depths, Apocalypse Nigh and Uninvited Guests. The data of each against its Lua file's, the hooks each
// overrides, Holy Crest's entryRequirement, the quest event and the setPos of the two onEventFinishWin overrides, and
// the allDeath of the groups that have one. The traces see the framework's arms; these see the fields they cannot.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static string C3Q3aFields(BattlefieldScript s)
    {
        var d = s.Data;
        var qd = (BattlefieldQuestData)d;
        var ki = d.RequiredKeyItems == null
            ? "-"
            : string.Join("/", d.RequiredKeyItems.Items.Select(k => string.Join("+", k.Ids))) +
              (d.RequiredKeyItems.OnlyInitiator ? "i" : "") + (d.RequiredKeyItems.Keep ? "k" : "") + ":" +
              d.RequiredKeyItems.Message + ":" + string.Join("+", d.RequiredKeyItems.MessageArgs ?? new int[0]) + ":" +
              d.RequiredKeyItems.DeleteMessage;
        return $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.AllowTrusts} {d.MaxPlayers} " +
               $"{d.LevelCapSetting ?? "-"} " +
               $"{d.TimeLimit} {d.Index} {d.Area} {string.Join(",", d.EntryNpcs)} " +
               $"{(d.ExitNpcs == null ? "-" : string.Join(",", d.ExitNpcs))} {ki} {qd.QuestArea} {qd.Quest} " +
               $"{qd.RequiredVar ?? "-"} {qd.RequiredValue} {qd.Repeatable} {d.Groups.Count}";
    }

    static string C3Q3aMobs(BattlefieldGroup g) =>
        string.Join(",", (g.AreaMobIds ?? new BattlefieldMobId[0][]).Select(a =>
            string.Join("/", a.Select(m => m.Offset == 0 ? m.Key : $"{m.Key}+{m.Offset}"))));

    static void RunC3Q3a()
    {
        Console.WriteLine("\n== c3 Q3a: the quest battlefields of Beyond Infinity, Holy Crest, Return to the Depths, " +
                          "Apocalypse Nigh, Uninvited Guests ==");
        var bi = new BattlefieldScript[]
        {
            new QC.Battlefields.BalgasDais.BeyondInfinityBalgasDais(),
            new QC.Battlefields.HorlaisPeak.BeyondInfinityHorlaisPeak(),
            new QC.Battlefields.QuBiaArena.BeyondInfinity(),
            new QC.Battlefields.WaughroonShrine.BeyondInfinityWaughroonShrine(),
        };
        var hc = new QC.Battlefields.GhelsbaOutpost.HolyCrest();
        var rd = new QC.Battlefields.MineShaft2716.ReturnToTheDepths();
        var an = new QC.Battlefields.EmpyrealParadox.ApocalypseNigh();
        var ug = new QC.Battlefields.MonarchLinn.UninvitedGuests();
        var all = bi.Concat(new BattlefieldScript[] { hc, rd, an, ug }).ToArray();
        Check("c3 Q3a: the eight battlefields validate clean, with their constants and LegacyPath files",
              "|battlefield.beyond_infinity_balgas_dais scripts/battlefields/Balgas_Dais/beyond_infinity," +
              "battlefield.beyond_infinity_horlais_peak scripts/battlefields/Horlais_Peak/beyond_infinity," +
              "battlefield.beyond_infinity scripts/battlefields/QuBia_Arena/beyond_infinity," +
              "battlefield.beyond_infinity_waughroon_shrine scripts/battlefields/Waughroon_Shrine/beyond_infinity," +
              "battlefield.holy_crest scripts/battlefields/Ghelsba_Outpost/holy_crest," +
              "battlefield.return_to_the_depths scripts/battlefields/Mine_Shaft_2716/return_to_the_depths," +
              "battlefield.apocalypse_nigh scripts/battlefields/Empyreal_Paradox/apocalypse_nigh," +
              "battlefield.uninvited_guests scripts/battlefields/Monarch_Linn/uninvited_guests",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));

        // Beyond Infinity: (zone, id, index, SOUL_GEM_REACTS, LOST_KEYITEM) of each arena, from its own zone's text
        var biCases = new (BattlefieldScript S, int Zone, int Id, int Index, object React, object Lost)[]
        {
            (bi[0], QC.Zone.BalgasDais, QC.Battlefield.BeyondInfinityBalgasDais, 20,
             QC.Text.BalgasDais.SOUL_GEM_REACTS, QC.Text.BalgasDais.LOST_KEYITEM),
            (bi[1], QC.Zone.HorlaisPeak, QC.Battlefield.BeyondInfinityHorlaisPeak, 20,
             QC.Text.HorlaisPeak.SOUL_GEM_REACTS, QC.Text.HorlaisPeak.LOST_KEYITEM),
            (bi[2], QC.Zone.QubiaArena, QC.Battlefield.BeyondInfinity, 21,
             QC.Text.QubiaArena.SOUL_GEM_REACTS, QC.Text.QubiaArena.LOST_KEYITEM),
            (bi[3], QC.Zone.WaughroonShrine, QC.Battlefield.BeyondInfinityWaughroonShrine, 21,
             QC.Text.WaughroonShrine.SOUL_GEM_REACTS, QC.Text.WaughroonShrine.LOST_KEYITEM),
        };
        foreach (var c in biCases)
        {
            Check($"c3 Q3a: {c.S.Key}: no exp loss, trusts, 6 players, MAX_LEVEL, 600 s, no area, BC_Entrance and " +
                  "Burning_Circle, the Soul Gem Clasp of the initiator alone (not kept, SOUL_GEM_REACTS " +
                  "with the clasp and the Soul Gem, LOST_KEYITEM), Jeuno's BEYOND_INFINITY with no var, one group",
                  $"{c.Zone} {c.Id} False True 6 MAX_LEVEL 600 {c.Index} 0 BC_Entrance Burning_Circle " +
                  $"{QC.KeyItem.SoulGemClasp}i:{c.React}:{QC.KeyItem.SoulGemClasp}+{QC.KeyItem.SoulGem}:{c.Lost} " +
                  "Jeuno BEYOND_INFINITY - 0 False 1",
                  C3Q3aFields(c.S));
            Check($"c3 Q3a: {c.S.Key}: Atori-Tutori, one to an area (+1, +2); allDeath sets WON; no other hook",
                  "ATORI_TUTORI,ATORI_TUTORI+1,ATORI_TUTORI+2 True Battlefield.SetStatus(2) ",
                  C3Q3aMobs(c.S.Data.Groups[0]) + " " + (c.S.Data.Groups[0].AllDeath != null) + " " +
                  C3Q3aWon(c.S, 0) + " " + C3Q1Hooks(c.S));
        }

        Check("c3 Q3a: holy crest's fields (6 players, 15 minutes, index 1, fixed area 1, Hut_Door, no exit NPC, " +
              "no key items, Sandoria's THE_HOLY_CREST, no var, one group of Cyranuce M Cutauleon)",
              $"{QC.Zone.GhelsbaOutpost} {QC.Battlefield.HolyCrest} False False 6 - 900 1 1 Hut_Door - - Sandoria " +
              "THE_HOLY_CREST - 0 False 1",
              C3Q3aFields(hc));
        Check("c3 Q3a: holy crest: the group is one list, its allDeath sets WON; overrides entryRequirement and " +
              "onEventFinishWin only",
              "CYRANUCE_M_CUTAULEON| True Battlefield.SetStatus(2) EntryRequirement,OnEventFinishWin",
              string.Join(",", hc.Data.Groups[0].MobIds.Select(m => m.Key)) + "|" +
              string.Join(",", (hc.Data.Groups[0].AreaMobIds ?? new BattlefieldMobId[0][]).Select(a => a.Length)) +
              " " + (hc.Data.Groups[0].AllDeath != null) + " " + C3Q3aWon(hc, 0) + " " + C3Q1Hooks(hc));

        // Holy Crest's entryRequirement: the quest completed, or the Dragon Curse Remedy (the framework's own check of
        // the quest status is a separate arm the traces see)
        string Entry(QuestStatus status, bool remedy)
        {
            var (q, p) = C3Q1Ctx(hc, "Hut_Door");
            p.QuestStatuses["Sandoria/THE_HOLY_CREST"] = status;
            if (remedy) p.KeyItems[QC.KeyItem.DragonCurseRemedy] = true;
            return hc.EntryRequirement(q, true).ToString();
        }
        Check("c3 Q3a: holy crest entryRequirement: completed alone; the remedy alone (accepted, available); " +
              "accepted with neither; available with neither",
              "True True True False False",
              $"{Entry(QuestStatus.Completed, false)} {Entry(QuestStatus.Accepted, true)} " +
              $"{Entry(QuestStatus.Available, true)} {Entry(QuestStatus.Accepted, false)} " +
              Entry(QuestStatus.Available, false));
        Check("c3 Q3a: holy crest onEventFinishWin hands xi.cs.questEvent the quest key, the csid and the option",
              "QuestEvent(sandoria.the_holy_crest,32001,7)|",
              C3Q1Event(hc));

        Check("c3 Q3a: return to the depths' fields (6 players, MAX_LEVEL, 30 minutes, index 1, _0d0, _0d1 to _0d3, " +
              "Bastok's RETURN_TO_THE_DEPTHS at Quest[1][78]Prog 9, one group)",
              $"{QC.Zone.MineShaft2716} {QC.Battlefield.ReturnToTheDepths} False False 6 MAX_LEVEL 1800 1 0 _0d0 " +
              "_0d1,_0d2,_0d3 - Bastok RETURN_TO_THE_DEPTHS Quest[1][78]Prog 9 False 1",
              C3Q3aFields(rd));
        Check("c3 Q3a: return to the depths: seven Twilotak to an area, superlinked, allDeath sets WON, no hook",
              "TWILOTAK/TWILOTAK+1/TWILOTAK+2/TWILOTAK+3/TWILOTAK+4/TWILOTAK+5/TWILOTAK+6," +
              "TWILOTAK+7/TWILOTAK+8/TWILOTAK+9/TWILOTAK+10/TWILOTAK+11/TWILOTAK+12/TWILOTAK+13," +
              "TWILOTAK+14/TWILOTAK+15/TWILOTAK+16/TWILOTAK+17/TWILOTAK+18/TWILOTAK+19/TWILOTAK+20 True True " +
              "Battlefield.SetStatus(2) ",
              C3Q3aMobs(rd.Data.Groups[0]) + " " + rd.Data.Groups[0].Superlink + " " +
              (rd.Data.Groups[0].AllDeath != null) + " " + C3Q3aWon(rd, 0) + " " + C3Q1Hooks(rd));

        Check("c3 Q3a: apocalypse nigh's fields (trusts, 6 players, MAX_LEVEL, 30 minutes, index 1, TR_Entrance, " +
              "Transcendental_Radiance, Jeuno's APOCALYPSE_NIGH at Quest[3][89]Prog 3, one group)",
              $"{QC.Zone.EmpyrealParadox} {QC.Battlefield.ApocalypseNigh} False True 6 MAX_LEVEL 1800 1 0 " +
              "TR_Entrance Transcendental_Radiance - Jeuno APOCALYPSE_NIGH Quest[3][89]Prog 3 False 1",
              C3Q3aFields(an));
        Check("c3 Q3a: apocalypse nigh: Kamlanaut and the id after it to an area, superlinked, allDeath sets WON; " +
              "overrides onEventFinishWin only",
              "KAMLANAUT/KAMLANAUT+1,KAMLANAUT+2/KAMLANAUT+3,KAMLANAUT+4/KAMLANAUT+5 True True " +
              "Battlefield.SetStatus(2) OnEventFinishWin",
              C3Q3aMobs(an.Data.Groups[0]) + " " + an.Data.Groups[0].Superlink + " " +
              (an.Data.Groups[0].AllDeath != null) + " " + C3Q3aWon(an, 0) + " " + C3Q1Hooks(an));
        {
            var (q, p) = C3Q1Ctx(an, "x");
            an.OnEventFinishWin(q, 32001, 5);
            Check("c3 Q3a: apocalypse nigh onEventFinishWin sets the char down at 540 0 -514, rotation 63, in its zone",
                  $"Player.SetPos(540,0,-514,63,{QC.Zone.EmpyrealParadox})|",
                  string.Join(",", p.Calls) + "|" + string.Join(",", ((BattlefieldEngineFake)q.Engine).Calls));
        }

        Check("c3 Q3a: uninvited guests' fields (18 players, 30 minutes, index 6, SD_Entrance, SD_BCNM_Exit_1 to 3, " +
              "the Monarch Linn Patrol Permit of every member, OtherAreas' UNINVITED_GUESTS at Quest[4][81]Prog 1, " +
              "two groups)",
              $"{QC.Zone.MonarchLinn} {QC.Battlefield.UninvitedGuests} False False 18 - 1800 6 0 SD_Entrance " +
              $"SD_BCNM_Exit_1,SD_BCNM_Exit_2,SD_BCNM_Exit_3 {QC.KeyItem.MonarchLinnPatrolPermit}::: OtherAreas " +
              "UNINVITED_GUESTS Quest[4][81]Prog 1 False 2",
              C3Q3aFields(ug));
        Check("c3 Q3a: uninvited guests: group 1 is Mammet-800 (+10, +20), group 2 eight Mammet-800 to an area " +
              "(+1.., +11.., +21..) not spawned; both in superlink group 1; no allDeath; no hook",
              "MAMMET_800,MAMMET_800+10,MAMMET_800+20 " +
              "MAMMET_800+1/MAMMET_800+2/MAMMET_800+3/MAMMET_800+4/MAMMET_800+5/MAMMET_800+6/MAMMET_800+7/" +
              "MAMMET_800+8," +
              "MAMMET_800+11/MAMMET_800+12/MAMMET_800+13/MAMMET_800+14/MAMMET_800+15/MAMMET_800+16/MAMMET_800+17/" +
              "MAMMET_800+18,MAMMET_800+21/MAMMET_800+22/MAMMET_800+23/MAMMET_800+24/MAMMET_800+25/MAMMET_800+26/" +
              "MAMMET_800+27/MAMMET_800+28 1 1 False False ",
              C3Q3aMobs(ug.Data.Groups[0]) + " " + C3Q3aMobs(ug.Data.Groups[1]) + " " +
              ug.Data.Groups[0].SuperlinkGroup + " " + ug.Data.Groups[1].SuperlinkGroup + " " +
              ug.Data.Groups[1].Spawned.GetValueOrDefault(true) + " " +
              (ug.Data.Groups[0].AllDeath != null || ug.Data.Groups[1].AllDeath != null) + " " + C3Q1Hooks(ug));
    }

    // what a group's allDeath did to the battlefield it was given
    static string C3Q3aWon(BattlefieldScript s, int group)
    {
        var bf = new BattlefieldViewFake { Id = s.Data.BattlefieldId };
        var q = new BattlefieldContextFake(s) { Battlefield = bf };
        s.Data.Groups[group].AllDeath(q, new BattlefieldMobFake { Id = 1 });
        return string.Join(",", bf.Calls);
    }
}
