// XI.Verify.Quests -- quest rework C3, branch Q1 (Quest battlefields): the fourteen quest battlefields of the Cloisters'
// trials and the small fights (Trial by Fire/Ice/Wind/Lightning/Water, their trial-size twins and Earth's, The Puppet
// Master, The Moonlit Path, A Thief in Norg?!). The data of each against its Lua file's, the hooks each overrides,
// xi.cs.questEvent (the engine's QuestEvent) behind the Frost, Gales, Storms and Tides trials and the trial-size
// files, the trial-size entry requirement, Earth's inline onEventFinishWin and the two registrant-only
// checkRequirements. The traces see the quest's own steps; these see the arms no flow reaches.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3Bfquest()
    {
        Console.WriteLine("\n== c3 Q1: the quest battlefields of the Cloisters' trials and the small fights ==");
        RunC3Q1Trials();
        RunC3Q1TrialSizes();
        RunC3Q1Earth();
        RunC3Q1SmallFights();
        RunC3Q2Waking();
        RunC3Q2bCarbuncle();
        RunC3Q3a();
        RunC3Q3b();
        RunC3Q3c();
        RunC3Q4();
        RunC3PR2();
        RunC3PR2Fmf();
    }

    static string C3Q1Fields(BattlefieldScript s)
    {
        var d = s.Data;
        var qd = (BattlefieldQuestData)d;
        var items = d.RequiredItems == null ? "-" : string.Join("/", d.RequiredItems.Items) + (d.RequiredItems.Keep ? "k" : "");
        var kis = d.RequiredKeyItems == null
            ? "-"
            : string.Join("/", d.RequiredKeyItems.Items.Select(k => string.Join("+", k.Ids))) +
              (d.RequiredKeyItems.Keep ? "k" : "");
        return $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.MaxPlayers} {d.LevelCap} {d.TimeLimit} {d.Index} " +
               $"{string.Join(",", d.EntryNpcs)} {string.Join(",", d.ExitNpcs)} {items} {kis} {qd.QuestArea} " +
               $"{qd.Quest} {qd.RequiredVar ?? "-"} {d.Groups.Count}";
    }

    static string C3Q1Hooks(BattlefieldScript s) => string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides));

    static (BattlefieldContextFake Q, BattlefieldPlayerFake P) C3Q1Ctx(BattlefieldScript s, string npc)
    {
        var player = new BattlefieldPlayerFake { ZoneId = s.Data.ZoneId };
        var q = new BattlefieldContextFake(s)
        {
            Player = player, Npc = new BattlefieldNpcFake { Id = 1, Name = npc },
        };
        return (q, player);
    }

    // the quest key each trial's onEventFinishWin hands xi.cs.questEvent, with the csid and option it was given
    static string C3Q1Event(BattlefieldScript s)
    {
        var (q, p) = C3Q1Ctx(s, "x");
        s.OnEventFinishWin(q, 32001, 7);
        return string.Join(",", ((BattlefieldEngineFake)q.Engine).Calls) + "|" + string.Join(",", p.Calls);
    }

    static void RunC3Q1Trials()
    {
        var all = new BattlefieldScript[]
        {
            new QC.Battlefields.CloisterOfFlames.TrialByFire(), new QC.Battlefields.CloisterOfFrost.TrialByIce(),
            new QC.Battlefields.CloisterOfGales.TrialByWind(), new QC.Battlefields.CloisterOfStorms.TrialByLightning(),
            new QC.Battlefields.CloisterOfTides.TrialByWater(),
        };
        Check("c3 Q1: the five trials validate clean, with their constants and LegacyPath files",
              "|battlefield.trial_by_fire scripts/battlefields/Cloister_of_Flames/trial_by_fire," +
              "battlefield.trial_by_ice scripts/battlefields/Cloister_of_Frost/trial_by_ice," +
              "battlefield.trial_by_wind scripts/battlefields/Cloister_of_Gales/trial_by_wind," +
              "battlefield.trial_by_lightning scripts/battlefields/Cloister_of_Storms/trial_by_lightning," +
              "battlefield.trial_by_water scripts/battlefields/Cloister_of_Tides/trial_by_water",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));
        foreach (var s in all)
        {
            var d = s.Data;
            var qd = (BattlefieldQuestData)d;
            Check($"c3 Q1: {s.Key}: no exp loss, 6 players, no cap, 1800 s, index 0, no area, one fork (kept off), " +
                  "one group with its allDeath, repeatable off",
                  "False 6 0 1800 0 0 1 1 True False",
                  $"{d.CanLoseExp} {d.MaxPlayers} {d.LevelCap} {d.TimeLimit} {d.Index} {d.Area} " +
                  $"{d.RequiredKeyItems.Items.Count} {d.Groups.Count} {d.Groups[0].AllDeath != null} {qd.Repeatable}");
        }
        // the hooks each overrides: Fire none, the other four onEventFinishWin
        Check("c3 Q1: the trials' overrides: Fire none; Ice, Wind, Lightning, Water onEventFinishWin",
              "|OnEventFinishWin|OnEventFinishWin|OnEventFinishWin|OnEventFinishWin",
              string.Join("|", all.Select(C3Q1Hooks)));
        // xi.cs.questEvent(player, npc, key, csid, option): the quest's key, the csid and option unchanged, no player call
        Check("c3 Q1: the trials' onEventFinishWin is xi.cs.questEvent with the quest's key, the csid and the option",
              "|QuestEvent(sandoria.trial_by_ice,32001,7)|QuestEvent(outlands.trial_by_wind,32001,7)|" +
              "QuestEvent(otherAreas.trial_by_lightning,32001,7)|QuestEvent(outlands.trial_by_water,32001,7)",
              string.Join("|", all.Select(s => C3Q1Event(s).Split('|')[0])));
        Check("c3 Q1: the Fire trial's onEventFinishWin is the framework's: no quest call",
              "", C3Q1Event(all[0]).Split('|')[0]);
    }

    static void RunC3Q1TrialSizes()
    {
        var all = new BattlefieldScript[]
        {
            new QC.Battlefields.CloisterOfFlames.TrialSizeTrialByFire(),
            new QC.Battlefields.CloisterOfFrost.TrialSizeTrialByIce(),
            new QC.Battlefields.CloisterOfGales.TrialSizeTrialByWind(),
            new QC.Battlefields.CloisterOfStorms.TrialSizeTrialByLightning(),
            new QC.Battlefields.CloisterOfTides.TrialSizeTrialByWater(),
            new QC.Battlefields.CloisterOfTremors.TrialSizeTrialByEarth(),
        };
        Check("c3 Q1: the six trial-size battlefields validate clean",
              "", string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)));
        Check("c3 Q1: the trial-size LegacyPath files (the dashes of the Lua names)",
              "scripts/battlefields/Cloister_of_Flames/trial-size_trial_by_fire," +
              "scripts/battlefields/Cloister_of_Frost/trial-size_trial_by_ice," +
              "scripts/battlefields/Cloister_of_Gales/trial-size_trial_by_wind," +
              "scripts/battlefields/Cloister_of_Storms/trial-size_trial_by_lightning," +
              "scripts/battlefields/Cloister_of_Tides/trial-size_trial_by_water," +
              "scripts/battlefields/Cloister_of_Tremors/trial-size_trial_by_earth",
              string.Join(",", all.Select(s => s.LegacyPath)));
        // the index of each (the Lua's): Fire 1, Ice 2, Wind 2, Lightning 2, Water 1, Earth 2
        Check("c3 Q1: the trial-size fields: no exp loss, 1 player, level cap 20, 900 s, the index, the mini fork " +
              "(taken), one group with its allDeath",
              "False 1 20 900 1 False 1 True|False 1 20 900 2 False 1 True|False 1 20 900 2 False 1 True|" +
              "False 1 20 900 2 False 1 True|False 1 20 900 1 False 1 True|False 1 20 900 2 False 1 True",
              string.Join("|", all.Select(s =>
              {
                  var d = s.Data;
                  return $"{d.CanLoseExp} {d.MaxPlayers} {d.LevelCap} {d.TimeLimit} {d.Index} {d.RequiredItems.Keep} " +
                         $"{d.Groups.Count} {d.Groups[0].AllDeath != null}";
              })));
        Check("c3 Q1: the trial-size overrides: entryRequirement and onEventFinishWin in all six (Earth's inline)",
              "EntryRequirement,OnEventFinishWin|EntryRequirement,OnEventFinishWin|EntryRequirement,OnEventFinishWin|" +
              "EntryRequirement,OnEventFinishWin|EntryRequirement,OnEventFinishWin|EntryRequirement,OnEventFinishWin",
              string.Join("|", all.Select(C3Q1Hooks)));
        string Entry(BattlefieldScript s, int job, int level)
        {
            var (q, p) = C3Q1Ctx(s, "x");
            p.MainJob = job;
            p.MainLevel = level;
            return s.EntryRequirement(q, true).ToString();
        }
        Check("c3 Q1: the trial-size entry requirement: a Summoner of 20; of 19; a Summoner of 75; a Red Mage of 20",
              "True False True False",
              $"{Entry(all[0], QC.Job.Smn, 20)} {Entry(all[0], QC.Job.Smn, 19)} {Entry(all[1], QC.Job.Smn, 75)} " +
              Entry(all[5], QC.Job.Rdm, 20));
        Check("c3 Q1: the trial-size quest events: Fire, Ice, Wind, Lightning, Water hand xi.cs.questEvent their keys",
              "QuestEvent(outlands.trial_size_trial_by_fire,32001,7)|QuestEvent(sandoria.trial_size_trial_by_ice,32001,7)|" +
              "QuestEvent(outlands.trial_size_trial_by_wind,32001,7)|" +
              "QuestEvent(otherAreas.trial_size_trial_by_lightning,32001,7)|" +
              "QuestEvent(outlands.trial_size_trial_by_water,32001,7)",
              string.Join("|", all.Take(5).Select(s => C3Q1Event(s).Split('|')[0])));
    }

    static void RunC3Q1Earth()
    {
        var s = new QC.Battlefields.CloisterOfTremors.TrialSizeTrialByEarth();
        var qd = (BattlefieldQuestData)s.Data;
        Check("c3 Q1: trial-size earth: the quest is Bastok's TRIAL_SIZE_TRIAL_BY_EARTH at 209",
              $"Bastok TRIAL_SIZE_TRIAL_BY_EARTH {QC.Zone.CloisterOfTremors}",
              $"{qd.QuestArea} {qd.Quest} {qd.ZoneId}");
        string Win(bool knowsTitan, bool hasScroll)
        {
            var (q, p) = C3Q1Ctx(s, "x");
            ((BattlefieldEngineFake)q.Engine).Texts["209:TITAN_UNLOCKED"] = 7777;
            if (knowsTitan) p.Spells.Add(QC.Spell.Titan);
            if (hasScroll) p.Items.Add(QC.Item.ScrollOfInstantWarp);
            s.OnEventFinishWin(q, 32001, 0);
            return string.Join(",", p.Calls);
        }
        int titan = QC.Spell.Titan, scroll = QC.Item.ScrollOfInstantWarp;
        Check("c3 Q1: trial-size earth onEventFinishWin: neither: the spell and its text, the scroll, 20 fame, " +
              "the quest",
              $"Player.AddSpell({titan}),Player.MessageSpecial(7777,0,0,1),Player.GiveItem({scroll})," +
              "Player.AddFame(1,20),Player.CompleteQuest(Bastok,TRIAL_SIZE_TRIAL_BY_EARTH)",
              Win(false, false));
        Check("c3 Q1: trial-size earth onEventFinishWin: Titan known, scroll held: only the fame and the quest",
              "Player.AddFame(1,20),Player.CompleteQuest(Bastok,TRIAL_SIZE_TRIAL_BY_EARTH)", Win(true, true));
        Check("c3 Q1: trial-size earth onEventFinishWin: Titan known, no scroll: the scroll, no spell",
              $"Player.GiveItem({scroll}),Player.AddFame(1,20),Player.CompleteQuest(Bastok,TRIAL_SIZE_TRIAL_BY_EARTH)",
              Win(true, false));
        Check("c3 Q1: trial-size earth onEventFinishWin: scroll held, Titan not known: the spell, no scroll",
              $"Player.AddSpell({titan}),Player.MessageSpecial(7777,0,0,1),Player.AddFame(1,20)," +
              "Player.CompleteQuest(Bastok,TRIAL_SIZE_TRIAL_BY_EARTH)",
              Win(false, true));
    }

    static void RunC3Q1SmallFights()
    {
        var pm = new QC.Battlefields.CloisterOfTremors.PuppetMaster();
        var mp = new QC.Battlefields.FullMoonFountain.MoonlitPath();
        var tn = new QC.Battlefields.WaughroonShrine.ThiefInNorg();
        Check("c3 Q1: the three small fights validate clean, their constants and LegacyPath files",
              "|battlefield.puppet_master scripts/battlefields/Cloister_of_Tremors/puppet_master," +
              "battlefield.moonlit_path scripts/battlefields/Full_Moon_Fountain/moonlit_path," +
              "battlefield.thief_in_norg scripts/battlefields/Waughroon_Shrine/thief_in_norg",
              string.Join(" | ", new BattlefieldScript[] { pm, mp, tn }.SelectMany(BattlefieldScriptValidator.Validate)) +
              "|" + string.Join(",", new BattlefieldScript[] { pm, mp, tn }.Select(s => $"{s.Key} {s.LegacyPath}")));
        Check("c3 Q1: puppet master's fields (zone, id, no exp loss, 18 players, 1800 s, index 1, NPCs, the Earth " +
              "Pendulum kept, no key items, Windurst's quest, the var, one group)",
              $"209 {QC.Battlefield.PuppetMaster} False 18 0 1800 1 EP_Entrance Earth_Protocrystal " +
              $"{QC.Item.EarthPendulum}k - Windurst THE_PUPPET_MASTER Quest[2][81]Prog 1",
              C3Q1Fields(pm));
        Check("c3 Q1: moonlit path's fields (6 players, index 0, the Moon Bauble kept, no items, Windurst's quest, " +
              "no var, one group)",
              $"{QC.Zone.FullMoonFountain} {QC.Battlefield.MoonlitPath} False 6 0 1800 0 MS_Entrance Moon_Spiral - " +
              $"{QC.KeyItem.MoonBauble}k Windurst THE_MOONLIT_PATH - 1",
              C3Q1Fields(mp));
        Check("c3 Q1: a thief in norg's fields (BattlefieldQuest:new: exp loss off, 6 players, index 4, the Banishing Charm kept, " +
              "Outlands' quest, the var, three groups)",
              $"{QC.Zone.WaughroonShrine} {QC.Battlefield.ThiefInNorg} False 6 0 1800 4 BC_Entrance Burning_Circle " +
              $"{QC.Item.BanishingCharm}k - Outlands A_THIEF_IN_NORG Quest[5][142]Prog 3",
              C3Q1Fields(tn));
        Check("c3 Q1: the requirement values: puppet master 1, thief in norg 6",
              "1 6", $"{((BattlefieldQuestData)pm.Data).RequiredValue} {((BattlefieldQuestData)tn.Data).RequiredValue}");
        Check("c3 Q1: overrides: puppet master and thief in norg checkRequirements; moonlit path none",
              "CheckRequirements||CheckRequirements", $"{C3Q1Hooks(pm)}|{C3Q1Hooks(mp)}|{C3Q1Hooks(tn)}");
        Check("c3 Q1: each group's mob lists: one list per area (GALGALIM; FENRIR_PRIME; the Gaki's three groups)",
              "GALGALIM,GALGALIM+1,GALGALIM+2|FENRIR_PRIME,FENRIR_PRIME+1,FENRIR_PRIME+2|" +
              "GAKI/GAKI+1/GAKI+2,GAKI+5/GAKI+6/GAKI+7,GAKI+10/GAKI+11/GAKI+12|" +
              "GAKI+3,GAKI+8,GAKI+13|GAKI+4,GAKI+9,GAKI+14",
              string.Join("|", new[]
              {
                  pm.Data.Groups[0], mp.Data.Groups[0], tn.Data.Groups[0], tn.Data.Groups[1], tn.Data.Groups[2],
              }.Select(g => string.Join(",", g.AreaMobIds.Select(a => string.Join("/", a.Select(m =>
                  m.Offset == 0 ? m.Key : $"{m.Key}+{m.Offset}")))))));
        Check("c3 Q1: only the first Gaki group's death wins", "True False False",
              string.Join(" ", tn.Data.Groups.Select(g => (g.AllDeath != null).ToString())));
        string Spawned(BattlefieldGroup g) => g.Spawned.HasValue ? g.Spawned.Value.ToString() : "null";
        Check("c3 Q1: thief in norg's spawned flags: the Gaki and the elemental unset, the avatar false",
              "null null False", string.Join(" ", tn.Data.Groups.Select(Spawned)));

        (BattlefieldContextFake Q, BattlefieldPlayerFake P) Reg(BattlefieldScript s, string npc, bool accepted, int var,
                                                               int item, string varName)
        {
            var (q, p) = C3Q1Ctx(s, npc);
            var qd = (BattlefieldQuestData)s.Data;
            p.QuestStatuses[$"{qd.QuestArea}/{qd.Quest}"] = accepted ? QuestStatus.Accepted : QuestStatus.Available;
            p.CharVars[varName] = var;
            if (item != 0)
            {
                // the registrant trades the item (keep: it is not consumed on entry)
                var trade = new BattlefieldPlayerFake();
                trade.TradeItems[item] = 1;
                q.Trade = trade;
            }
            return (q, p);
        }
        // the registrant: BattlefieldQuest.checkRequirements (the item, accepted at exactly the value); a helper: only
        // isValidEntry (the entry NPC's name), whatever it holds
        string Pm(bool registrant, string npc, bool accepted, int var, int item) =>
            pm.CheckRequirements(Reg(pm, npc, accepted, var, item, "Quest[2][81]Prog").Q, registrant).ToString();
        Check("c3 Q1: puppet master checkRequirements: the registrant with the pendulum, accepted, var 1; var 2; " +
              "not accepted",
              "True False False",
              $"{Pm(true, "EP_Entrance", true, 1, QC.Item.EarthPendulum)} " +
              $"{Pm(true, "EP_Entrance", true, 2, QC.Item.EarthPendulum)} " +
              Pm(true, "EP_Entrance", false, 1, QC.Item.EarthPendulum));
        Check("c3 Q1: puppet master checkRequirements: a helper at the entry NPC with nothing: true; at another NPC: " +
              "false",
              "True False",
              $"{Pm(false, "EP_Entrance", false, 0, 0)} {Pm(false, "Earth_Protocrystal", false, 0, 0)}");
        string Tn(bool registrant, string npc, bool accepted, int var, int item) =>
            tn.CheckRequirements(Reg(tn, npc, accepted, var, item, "Quest[5][142]Prog").Q, registrant).ToString();
        Check("c3 Q1: a thief in norg checkRequirements: the registrant with the charm, accepted, var 6; var 5; " +
              "no charm",
              "True False False",
              $"{Tn(true, "BC_Entrance", true, 6, QC.Item.BanishingCharm)} " +
              $"{Tn(true, "BC_Entrance", true, 5, QC.Item.BanishingCharm)} " +
              Tn(true, "BC_Entrance", true, 6, 0));
        Check("c3 Q1: a thief in norg checkRequirements: a helper at the entry NPC with nothing: true; at another " +
              "NPC: false",
              "True False",
              $"{Tn(false, "BC_Entrance", false, 0, 0)} {Tn(false, "Burning_Circle", false, 0, 0)}");
        string Mp(bool registrant, bool accepted, bool bauble)
        {
            var (q, p) = Reg(mp, "MS_Entrance", accepted, 0, 0, "x");
            if (bauble) p.KeyItems[QC.KeyItem.MoonBauble] = true;
            return mp.CheckRequirements(q, registrant).ToString();
        }
        Check("c3 Q1: moonlit path checkRequirements is BattlefieldQuest's: accepted with the bauble; without; " +
              "not accepted; a helper accepted with it",
              "True False False True",
              $"{Mp(true, true, true)} {Mp(true, true, false)} {Mp(true, false, true)} {Mp(false, true, true)}");
    }

    // quest rework C3, Q2a: the six Cloisters' Waking the Beast battlefields (one abstract base, six classes)
    static void RunC3Q2Waking()
    {
        var all = new BattlefieldScript[]
        {
            new QC.Battlefields.CloisterOfFlames.WakingTheBeast(), new QC.Battlefields.CloisterOfFrost.WakingTheBeast(),
            new QC.Battlefields.CloisterOfGales.WakingTheBeast(), new QC.Battlefields.CloisterOfStorms.WakingTheBeast(),
            new QC.Battlefields.CloisterOfTides.WakingTheBeast(),
            new QC.Battlefields.CloisterOfTremors.WakingTheBeast(),
        };
        Check("c3 Q2: the six Waking the Beast battlefields validate clean, with their constants and LegacyPath files",
              "|battlefield.waking_the_beast_cloister_of_flames " +
              "scripts/battlefields/Cloister_of_Flames/waking_the_beast," +
              "battlefield.waking_the_beast_cloister_of_frost " +
              "scripts/battlefields/Cloister_of_Frost/waking_the_beast," +
              "battlefield.waking_the_beast_cloister_of_gales " +
              "scripts/battlefields/Cloister_of_Gales/waking_the_beast," +
              "battlefield.waking_the_beast_cloister_of_storms " +
              "scripts/battlefields/Cloister_of_Storms/waking_the_beast," +
              "battlefield.waking_the_beast_cloister_of_tides " +
              "scripts/battlefields/Cloister_of_Tides/waking_the_beast," +
              "battlefield.waking_the_beast_cloister_of_tremors " +
              "scripts/battlefields/Cloister_of_Tremors/waking_the_beast",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));
        // zone, battlefield id, index, entry NPC, exit NPC, the two group lists: the Lua files' values
        Check("c3 Q2: the six files' zone, id, index, NPCs and mobs",
              $"{QC.Zone.CloisterOfFlames} 546 2 FP_Entrance Fire_Protocrystal Ifrit_Prime_WTB " +
              "Ifrit_Prime_WTB+Fire_Elemental|" +
              $"{QC.Zone.CloisterOfFrost} 483 3 IP_Entrance Ice_Protocrystal Shiva_Prime_WTB " +
              "Shiva_Prime_WTB+Ice_Elemental|" +
              $"{QC.Zone.CloisterOfGales} 419 3 WP_Entrance Wind_Protocrystal Garuda_Prime_WTB " +
              "Garuda_Prime_WTB+Air_Elemental|" +
              $"{QC.Zone.CloisterOfStorms} 451 3 LP_Entrance Lightning_Protocrystal Ramuh_Prime_WTB " +
              "Ramuh_Prime_WTB+Thunder_Elemental|" +
              $"{QC.Zone.CloisterOfTides} 610 2 WP_Entrance Water_Protocrystal Leviathan_Prime_WTB " +
              "Leviathan_Prime_WTB+Water_Elemental|" +
              $"{QC.Zone.CloisterOfTremors} 579 3 EP_Entrance Earth_Protocrystal Titan_Prime_WTB " +
              "Titan_Prime_WTB+Earth_Elemental",
              string.Join("|", all.Select(s =>
              {
                  var d = s.Data;
                  return $"{d.ZoneId} {d.BattlefieldId} {d.Index} {string.Join(",", d.EntryNpcs)} " +
                         $"{string.Join(",", d.ExitNpcs)} {string.Join("+", d.Groups[0].Mobs)} " +
                         string.Join("+", d.Groups[1].Mobs);
              })));
        Check("c3 Q2: the shared fields: no exp loss, 18 players, no cap, 1800 s, the quest, repeatable, no items, " +
              "no var, two groups",
              string.Join("|", all.Select(_ => "False 18 0 1800 OtherAreas WAKING_THE_BEAST True - - - 2")),
              string.Join("|", all.Select(s =>
              {
                  var d = s.Data;
                  var qd = (BattlefieldQuestData)d;
                  return $"{d.CanLoseExp} {d.MaxPlayers} {d.LevelCap} {d.TimeLimit} {qd.QuestArea} {qd.Quest} " +
                         $"{qd.Repeatable} {(d.RequiredItems == null ? "-" : "items")} " +
                         $"{(d.RequiredKeyItems == null ? "-" : "keyitems")} {qd.RequiredVar ?? "-"} {d.Groups.Count}";
              })));
        Check("c3 Q2: group 1 (the avatar alone) has the allDeath, not party, not superlinked; group 2 is party and " +
              "superlinked with no callback",
              string.Join("|", all.Select(_ => "True False False/False True True")),
              string.Join("|", all.Select(s =>
              {
                  var g1 = s.Data.Groups[0];
                  var g2 = s.Data.Groups[1];
                  return $"{g1.AllDeath != null} {g1.IsParty} {g1.Superlink}/" +
                         $"{g2.AllDeath != null} {g2.IsParty} {g2.Superlink}";
              })));
        Check("c3 Q2: each file overrides entryRequirement alone",
              string.Join("|", all.Select(_ => "EntryRequirement")),
              string.Join("|", all.Select(C3Q1Hooks)));
        Check("c3 Q2: each file's coverage labels: its exit NPC, the avatar group's allDeath, " +
              "the entryRequirement hook",
              string.Join("|", new[] { "Fire", "Ice", "Wind", "Lightning", "Water", "Earth" }.Select(e =>
                  $"exit:{e}_Protocrystal,group:1:allDeath,hook:EntryRequirement")),
              string.Join("|", all.Select(s => string.Join(",",
                  s.CoverageLabels().OrderBy(x => x, StringComparer.Ordinal)))));

        // group 1's allDeath: the four ids after the avatar's, the live ones set to 0 hp, then WON
        string Death(int avatarId, int deadId, int missingId)
        {
            var bf = new BattlefieldViewFake { Id = 546 };
            var q = new BattlefieldContextFake(all[0]) { Battlefield = bf };
            var fakes = new Dictionary<int, BattlefieldMobFake>();
            // the avatar itself (offset 0) is in the engine too: the loop starts at the first elemental, so it is
            // untouched
            for (int i = -1; i <= 5; i++)
            {
                int id = avatarId + i;
                if (id == missingId) continue;
                fakes[id] = new BattlefieldMobFake { Id = id, MaxHP = 100, HP = 100, IsSpawned = true };
                ((BattlefieldEngineFake)q.Engine).MobsById[id] = fakes[id];
            }
            if (deadId != 0) fakes[deadId].AliveOverride = false;
            try
            {
                all[0].Data.Groups[0].AllDeath(q, new BattlefieldMobFake { Id = avatarId });
            }
            catch (System.Exception ex)
            {
                return "threw " + ex.GetType().Name;
            }
            return string.Join(",", fakes.OrderBy(kv => kv.Key).Where(kv => kv.Value.Calls.Count > 0)
                       .Select(kv => $"{kv.Key - avatarId}:{string.Join(";", kv.Value.Calls)}")) +
                   "|" + string.Join(",", bf.Calls);
        }
        Check("c3 Q2: allDeath: the four after the avatar die (not the one before or after them), then WON",
              "1:Mob.SetHP(0),2:Mob.SetHP(0),3:Mob.SetHP(0),4:Mob.SetHP(0)|Battlefield.SetStatus(2)",
              Death(100, 0, 0));
        Check("c3 Q2: allDeath: an elemental already dead is left alone (isAlive)",
              "1:Mob.SetHP(0),2:Mob.SetHP(0),4:Mob.SetHP(0)|Battlefield.SetStatus(2)", Death(100, 103, 0));
        Check("c3 Q2: allDeath: a missing elemental (nil) is skipped, the others die, WON",
              "1:Mob.SetHP(0),3:Mob.SetHP(0),4:Mob.SetHP(0)|Battlefield.SetStatus(2)", Death(100, 0, 102));

        // entryRequirement: the registrant needs the resonator; the others the resonator or a completed quest
        string Req(bool registrant, bool resonator, QuestStatus status)
        {
            var (q, p) = C3Q1Ctx(all[0], "FP_Entrance");
            p.QuestStatuses["OtherAreas/WAKING_THE_BEAST"] = status;
            if (resonator) p.KeyItems[QC.KeyItem.RainbowResonator] = true;
            return all[0].EntryRequirement(q, registrant).ToString();
        }
        const QuestStatus acc = QuestStatus.Accepted, done = QuestStatus.Completed;
        Check("c3 Q2: entryRequirement, the registrant: the resonator alone decides (accepted with it; accepted " +
              "without; completed without; completed with)",
              "True False False True",
              $"{Req(true, true, acc)} {Req(true, false, acc)} {Req(true, false, done)} {Req(true, true, done)}");
        Check("c3 Q2: entryRequirement, a helper: the resonator or completed alone suffices (accepted with it; " +
              "completed without; accepted without; available without)",
              "True True False False",
              $"{Req(false, true, acc)} {Req(false, false, done)} {Req(false, false, acc)} " +
              Req(false, false, QuestStatus.Available));
    }

    // quest rework C3, Q2b: Class Reunion (Frost) and the two Carbuncle Debacles (Gales, Storms)
    static void RunC3Q2bCarbuncle()
    {
        var cr = new QC.Battlefields.CloisterOfFrost.ClassReunion();
        var cg = new QC.Battlefields.CloisterOfGales.CarbuncleDebacle();
        var cs = new QC.Battlefields.CloisterOfStorms.CarbuncleDebacle();
        var all = new BattlefieldScript[] { cr, cg, cs };
        Check("c3 Q2b: the three battlefields validate clean, with their constants and LegacyPath files",
              "|battlefield.class_reunion scripts/battlefields/Cloister_of_Frost/class_reunion," +
              "battlefield.carbuncle_debacle_cloister_of_gales " +
              "scripts/battlefields/Cloister_of_Gales/carbuncle_debacle," +
              "battlefield.carbuncle_debacle_cloister_of_storms " +
              "scripts/battlefields/Cloister_of_Storms/carbuncle_debacle",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));
        Check("c3 Q2b: class reunion's fields (zone, id, no exp loss, 6 players, 1800 s, index 1, NPCs, the Ice " +
              "Pendulum kept, no key items, Windurst's quest, the var, one group)",
              $"{QC.Zone.CloisterOfFrost} {QC.Battlefield.ClassReunion} False 6 0 1800 1 IP_Entrance " +
              $"Ice_Protocrystal {QC.Item.IcePendulum}k - Windurst CLASS_REUNION ClassReunionProgress 1",
              C3Q1Fields(cr));
        Check("c3 Q2b: gales' fields (18 players, the Wind Pendulum, the var)",
              $"{QC.Zone.CloisterOfGales} {QC.Battlefield.CarbuncleDebacleCloisterOfGales} False 18 0 1800 1 " +
              $"WP_Entrance Wind_Protocrystal {QC.Item.WindPendulum}k - Windurst CARBUNCLE_DEBACLE " +
              "CarbuncleDebacleProgress 1",
              C3Q1Fields(cg));
        Check("c3 Q2b: storms' fields (18 players, the Lightning Pendulum, the var)",
              $"{QC.Zone.CloisterOfStorms} {QC.Battlefield.CarbuncleDebacleCloisterOfStorms} False 18 0 1800 1 " +
              $"LP_Entrance Lightning_Protocrystal {QC.Item.LightningPendulum}k - Windurst CARBUNCLE_DEBACLE " +
              "CarbuncleDebacleProgress 1",
              C3Q1Fields(cs));
        Check("c3 Q2b: the battlefield ids (481, 417, 449) and the required values (5, 6, 3)",
              "481 417 449 5 6 3",
              $"{QC.Battlefield.ClassReunion} {QC.Battlefield.CarbuncleDebacleCloisterOfGales} " +
              $"{QC.Battlefield.CarbuncleDebacleCloisterOfStorms} " +
              string.Join(" ", all.Select(s => ((BattlefieldQuestData)s.Data).RequiredValue)));
        Check("c3 Q2b: overrides: checkRequirements and onEventFinishWin, and no other hook",
              "CheckRequirements,OnEventFinishWin|CheckRequirements,OnEventFinishWin|" +
              "CheckRequirements,OnEventFinishWin",
              string.Join("|", all.Select(C3Q1Hooks)));
        Check("c3 Q2b: each group's mob lists: one list per area (DRYAD 6 each; OGMIOS one each; " +
              "LIGHTNING_GREMLIN two each)",
              "DRYAD/DRYAD+1/DRYAD+2/DRYAD+3/DRYAD+4/DRYAD+5,DRYAD+6/DRYAD+7/DRYAD+8/DRYAD+9/DRYAD+10/DRYAD+11," +
              "DRYAD+12/DRYAD+13/DRYAD+14/DRYAD+15/DRYAD+16/DRYAD+17|OGMIOS,OGMIOS+1,OGMIOS+2|" +
              "LIGHTNING_GREMLIN/LIGHTNING_GREMLIN+1,LIGHTNING_GREMLIN+2/LIGHTNING_GREMLIN+3," +
              "LIGHTNING_GREMLIN+4/LIGHTNING_GREMLIN+5",
              string.Join("|", all.Select(s => string.Join(",", s.Data.Groups[0].AreaMobIds.Select(a =>
                  string.Join("/", a.Select(m => m.Offset == 0 ? m.Key : $"{m.Key}+{m.Offset}")))))));
        Check("c3 Q2b: one group each, with an allDeath", "1 1 1 True True True",
              $"{cr.Data.Groups.Count} {cg.Data.Groups.Count} {cs.Data.Groups.Count} " +
              string.Join(" ", all.Select(s => (s.Data.Groups[0].AllDeath != null).ToString())));
        Check("c3 Q2b: the group's allDeath sets WON (2)",
              "Battlefield.SetStatus(2)|Battlefield.SetStatus(2)|Battlefield.SetStatus(2)",
              string.Join("|", all.Select(s =>
              {
                  var bf = new BattlefieldViewFake { Id = s.Data.BattlefieldId };
                  var q = new BattlefieldContextFake(s) { Battlefield = bf };
                  s.Data.Groups[0].AllDeath(q, new BattlefieldMobFake { Id = 1 });
                  return string.Join(",", bf.Calls);
              })));

        // the registrant: BattlefieldQuest.checkRequirements; a helper: only isValidEntry (the entry NPC's name)
        (BattlefieldContextFake Q, BattlefieldPlayerFake P) Reg(BattlefieldScript s, string npc, bool accepted, int var,
                                                               int item)
        {
            var (q, p) = C3Q1Ctx(s, npc);
            var qd = (BattlefieldQuestData)s.Data;
            p.QuestStatuses[$"{qd.QuestArea}/{qd.Quest}"] = accepted ? QuestStatus.Accepted : QuestStatus.Available;
            p.CharVars[qd.RequiredVar] = var;
            if (item != 0)
            {
                var trade = new BattlefieldPlayerFake();
                trade.TradeItems[item] = 1;
                q.Trade = trade;
            }
            return (q, p);
        }
        string Cr(BattlefieldScript s, bool registrant, string npc, bool accepted, int var, int item) =>
            s.CheckRequirements(Reg(s, npc, accepted, var, item).Q, registrant).ToString();
        var cases = new (BattlefieldScript, string, string, int, int)[]
        {
            (cr, "IP_Entrance", "Ice_Protocrystal", 5, QC.Item.IcePendulum),
            (cg, "WP_Entrance", "Wind_Protocrystal", 6, QC.Item.WindPendulum),
            (cs, "LP_Entrance", "Lightning_Protocrystal", 3, QC.Item.LightningPendulum),
        };
        foreach (var (s, en, ex, val, item) in cases)
        {
            Check($"c3 Q2b: {s.Key} checkRequirements, the registrant: the pendulum, accepted, the var; the var one " +
                  "short; the var one over; not accepted; no pendulum",
                  "True False False False False",
                  $"{Cr(s, true, en, true, val, item)} {Cr(s, true, en, true, val - 1, item)} " +
                  $"{Cr(s, true, en, true, val + 1, item)} {Cr(s, true, en, false, val, item)} " +
                  Cr(s, true, en, true, val, 0));
            Check($"c3 Q2b: {s.Key} checkRequirements, a helper at the entry NPC with nothing: true; at the exit " +
                  "NPC: false",
                  "True False", $"{Cr(s, false, en, false, 0, 0)} {Cr(s, false, ex, false, 0, 0)}");
        }

        // onEventFinishWin: the var + 1 only at exactly the value; Gales also takes the Daze Breaker Charm whatever
        string Win(BattlefieldScript s, int var, bool charm)
        {
            var (q, p) = C3Q1Ctx(s, "x");
            p.CharVars["CarbuncleDebacleProgress"] = var;
            p.CharVars["ClassReunionProgress"] = var;
            if (charm) p.KeyItems[QC.KeyItem.DazeBreakerCharm] = true;
            s.OnEventFinishWin(q, 32001, 0);
            return string.Join(",", p.Calls);
        }
        int dbc = QC.KeyItem.DazeBreakerCharm;
        Check("c3 Q2b: class reunion onEventFinishWin: the var 5 -> 6; 4 and 6 untouched; no key item taken",
              "Player.SetCharVar(ClassReunionProgress,6,0)||",
              $"{Win(cr, 5, true)}|{Win(cr, 4, true)}|{Win(cr, 6, true)}");
        Check("c3 Q2b: gales onEventFinishWin: the var 6 -> 7 and the charm; 5, 7 and 9: the charm only",
              $"Player.SetCharVar(CarbuncleDebacleProgress,7,0),Player.DelKeyItem({dbc})|Player.DelKeyItem({dbc})|" +
              $"Player.DelKeyItem({dbc})|Player.DelKeyItem({dbc})",
              $"{Win(cg, 6, true)}|{Win(cg, 5, true)}|{Win(cg, 7, true)}|{Win(cg, 9, false)}");
        Check("c3 Q2b: storms onEventFinishWin: the var 3 -> 4; 2 and 4 untouched; no key item taken",
              "Player.SetCharVar(CarbuncleDebacleProgress,4,0)||",
              $"{Win(cs, 3, true)}|{Win(cs, 2, true)}|{Win(cs, 4, true)}");
    }
}
