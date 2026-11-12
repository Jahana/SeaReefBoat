// XI.Verify.Quests -- quest rework C3, branch Q4 (Quest battlefields): the three limit-break fights entered by a
// testimony, The Beast Within (Jade Sepulcher), Achieving True Power (Navukgo Execution Chamber) and Breaking the
// Bonds of Fate (Talacca Cove). The data of each against its Lua file's, the hooks each overrides, entryRequirement's
// two parts, setupBattlefield's playerLevel (which no trace shows) with and without the initiator, the group's
// allDeath, and the framework's onBattlefieldEnter under a Lua module's override (PR1: testimony_single_use).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunC3Q4()
    {
        Console.WriteLine("\n== c3 Q4: The Beast Within, Achieving True Power, Breaking the Bonds of Fate ==");
        var bw = new QC.Battlefields.JadeSepulcher.BeastWithin();
        var at = new QC.Battlefields.NavukgoExecutionChamber.AchievingTruePower();
        var bb = new QC.Battlefields.TalaccaCove.BreakingTheBondsOfFate();
        var all = new BattlefieldScript[] { bw, at, bb };
        Check("c3 Q4: the three battlefields validate clean, with their constants and LegacyPath files",
              "|battlefield.beast_within scripts/battlefields/Jade_Sepulcher/the_beast_within," +
              "battlefield.achieving_true_power scripts/battlefields/Navukgo_Execution_Chamber/achieving_true_power," +
              "battlefield.breaking_the_bonds_of_fate scripts/battlefields/Talacca_Cove/breaking_the_bonds_of_fate",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));

        Check("c3 Q4: the beast within's fields (no exp loss, no trusts, 1 player, MAX_LEVEL, 10 minutes, index 2, " +
              "_1v0, _1v1 to _1v3, Aht Urhgan's THE_BEAST_WITHIN at Quest[6][40]Prog 2, one group)",
              $"{QC.Zone.JadeSepulcher} {QC.Battlefield.BeastWithin} False False 1 MAX_LEVEL 600 2 0 _1v0 " +
              "_1v1,_1v2,_1v3 - AhtUrghan THE_BEAST_WITHIN Quest[6][40]Prog 2 False 1",
              C3Q3aFields(bw));
        Check("c3 Q4: achieving true power's fields (no exp loss, no trusts, 1 player, MAX_LEVEL, 10 minutes, index " +
              "3, _1s0, _1s1 to _1s3, Bastok's ACHIEVING_TRUE_POWER at Quest[1][85]Prog 1, one group)",
              $"{QC.Zone.NavukgoExecutionChamber} {QC.Battlefield.AchievingTruePower} False False 1 MAX_LEVEL 600 " +
              "3 0 _1s0 _1s1,_1s2,_1s3 - Bastok ACHIEVING_TRUE_POWER Quest[1][85]Prog 1 False 1",
              C3Q3aFields(at));
        Check("c3 Q4: breaking the bonds of fate's fields (no exp loss, no trusts, 1 player, MAX_LEVEL, 10 minutes, " +
              "index 3, _1l0, _1l1 to _1l3, Aht Urhgan's BREAKING_THE_BONDS_OF_FATE at Quest[6][41]Prog 1, one group)",
              $"{QC.Zone.TalaccaCove} {QC.Battlefield.BreakingTheBondsOfFate} False False 1 MAX_LEVEL 600 3 0 _1l0 " +
              "_1l1,_1l2,_1l3 - AhtUrghan BREAKING_THE_BONDS_OF_FATE Quest[6][41]Prog 1 False 1",
              C3Q3aFields(bb));

        // requiredItems = { <testimony>, wearMessage = ID.text.TESTIMONY_WEARS,
        //                   wornMessage = ID.text.TESTIMONY_IS_TORN }
        string Items(BattlefieldScript s)
        {
            var r = s.Data.RequiredItems;
            return $"{string.Join("/", r.Items)} {r.WearMessage?.Zone}:{r.WearMessage?.Name} " +
                   $"{r.WornMessage?.Zone}:{r.WornMessage?.Name} {r.Keep} {s.Data.AllowSubjob} {s.Data.LevelCap}";
        }
        Check("c3 Q4: each takes its job's testimony (worn on entry, torn when worn out, not kept), no support job, " +
              "no fixed level cap",
              $"{QC.Item.BlueMagesTestimony} {QC.Zone.JadeSepulcher}:TESTIMONY_WEARS " +
              $"{QC.Zone.JadeSepulcher}:TESTIMONY_IS_TORN False False 0|" +
              $"{QC.Item.PuppetmastersTestimony} {QC.Zone.NavukgoExecutionChamber}:TESTIMONY_WEARS " +
              $"{QC.Zone.NavukgoExecutionChamber}:TESTIMONY_IS_TORN False False 0|" +
              $"{QC.Item.CorsairsTestimony} {QC.Zone.TalaccaCove}:TESTIMONY_WEARS " +
              $"{QC.Zone.TalaccaCove}:TESTIMONY_IS_TORN False False 0",
              string.Join("|", all.Select(Items)));

        Check("c3 Q4: the groups: Raubahn (+1, +2) one to an area; Shamarhaan and the next id, two to an area " +
              "(+2/+3, +4/+5), superlinked; Qultada (+1, +2); each allDeath sets WON; each overrides " +
              "entryRequirement and setupBattlefield only",
              "RAUBAHN,RAUBAHN+1,RAUBAHN+2 False Battlefield.SetStatus(2) EntryRequirement,SetupBattlefield|" +
              "SHAMARHAAN/SHAMARHAAN+1,SHAMARHAAN+2/SHAMARHAAN+3,SHAMARHAAN+4/SHAMARHAAN+5 True " +
              "Battlefield.SetStatus(2) EntryRequirement,SetupBattlefield|" +
              "QULTADA,QULTADA+1,QULTADA+2 False Battlefield.SetStatus(2) EntryRequirement,SetupBattlefield",
              string.Join("|", all.Select(s => $"{C3Q3aMobs(s.Data.Groups[0])} {s.Data.Groups[0].Superlink} " +
                                               $"{C3Q3aWon(s, 0)} {C3Q1Hooks(s)}")));
        Check("c3 Q4: the groups set nothing else (no mobs by name, no flat ids, no superlink group, spawned, " +
              "stationary, death, randomDeath or setup)",
              "True|True|True",
              string.Join("|", all.Select(s =>
              {
                  var g = s.Data.Groups[0];
                  return (g.Mobs == null && g.MobIds == null && g.SuperlinkGroup == null && g.Spawned == null &&
                          g.Stationary == null && g.Death == null && g.RandomDeath == null && g.Setup == null &&
                          !g.IsParty && s.Data.EssentialMobs.Count == 0 && s.Data.Loot.Count == 0).ToString();
              })));

        RunC3Q4Entry(bw, QC.Job.Blu);
        RunC3Q4Entry(at, QC.Job.Pup);
        RunC3Q4Entry(bb, QC.Job.Cor);
        foreach (var s in all) RunC3Q4Setup(s);
        RunC3Q4Testimony(bw);
        RunC3Q4Worn(bw, QC.Job.Blu, "Quest[6][40]Prog", 2);
        RunC3Q4Worn(at, QC.Job.Pup, "Quest[1][85]Prog", 1);
        RunC3Q4Worn(bb, QC.Job.Cor, "Quest[6][41]Prog", 1);
    }

    // the C# framework's onEntryTrade (battlefield.lua:643) on the content alone in its zone: the testimony traded
    // by a registrant who meets every requirement, worn 3 of its 3 uses: TESTIMONY_IS_TORN with the item, no menu,
    // the content's requiredItems:worn; worn 2: the menu. At Jade Sepulcher no trace reaches this handler (_1v0's
    // trade is the Lua framework's, set up by Battlefield[1153], the zone's first content), so this pins The Beast
    // Within's arm; the other two zones' traces reach it as well
    static void RunC3Q4Worn(BattlefieldScript s, int job, string varName, int var)
    {
        var qd = (BattlefieldQuestData)s.Data;
        int item = s.Data.RequiredItems.Items[0];
        string Trade(int worn)
        {
            var (q, p) = C3Q1Ctx(s, s.Data.EntryNpcs[0]);
            p.MainJob = job;
            p.MainLevel = 75;
            p.QuestStatuses[$"{qd.QuestArea}/{qd.Quest}"] = QuestStatus.Accepted;
            p.CharVars[varName] = var;
            var trade = new BattlefieldPlayerFake();
            trade.TradeItems[item] = 1;
            q.Trade = trade;
            var e = (BattlefieldEngineFake)q.Engine;
            e.Texts[$"{s.Data.ZoneId}:TESTIMONY_IS_TORN"] = 300;
            // the bridge resolves the content's wornMessage name in its zone's text table
            var content = new BattlefieldContentFake(s) { WornMessage = e.Text(s.Data.ZoneId, "TESTIMONY_IS_TORN") };
            e.ByZone[s.Data.ZoneId] = new List<IBattlefieldContent> { content };
            e.Uses[item] = 3;
            p.WornUsesByItem[item] = worn;
            var action = BattlefieldFramework.OnEntryTrade(q);
            var said = p.Calls.Where(c => c.StartsWith("Player.MessageSpecial", StringComparison.Ordinal));
            return $"{(action == null ? "none" : "menu")} {string.Join(",", content.ReachedLabels)} " +
                   string.Join(",", said);
        }
        Check($"c3 Q4: {s.Key}: the C# framework's onEntryTrade: the testimony worn 3 of 3: TESTIMONY_IS_TORN with " +
              "the item, no menu (requiredItems:worn); worn 2 of 3: the menu",
              $"none requiredItems:worn Player.MessageSpecial(300,{item})|menu  ",
              $"{Trade(3)}|{Trade(2)}");
    }

    // entryRequirement: the job and the level (66 or more), each alone deciding
    static void RunC3Q4Entry(BattlefieldScript s, int job)
    {
        string Entry(int mainJob, int level, bool isRegistrant)
        {
            var (q, p) = C3Q1Ctx(s, "x");
            p.MainJob = mainJob;
            p.MainLevel = level;
            return s.EntryRequirement(q, isRegistrant).ToString();
        }
        int other = job == QC.Job.Blu ? QC.Job.Cor : QC.Job.Blu;
        Check($"c3 Q4: {s.Key}: entryRequirement: its job at 66 and 75 (registrant or not), not at 65, another job " +
              "(Warrior, or the next limit break's) at 75 not",
              "True True True False False False",
              $"{Entry(job, 66, true)} {Entry(job, 75, true)} {Entry(job, 75, false)} {Entry(job, 65, true)} " +
              $"{Entry(QC.Job.War, 75, true)} {Entry(other, 75, true)}");
    }

    // setupBattlefield: GetPlayerByID(the initiator's id); its level in playerLevel, or 99 with no such player
    static void RunC3Q4Setup(BattlefieldScript s)
    {
        string Setup(bool found, int level)
        {
            var bf = new BattlefieldViewFake { Id = s.Data.BattlefieldId, InitiatorValue = (41, "Initiator") };
            var q = new BattlefieldContextFake(s) { Battlefield = bf };
            var e = (BattlefieldEngineFake)q.Engine;
            var other = new BattlefieldPlayerFake { CharId = 40, MainLevel = 50 };
            e.PlayersById[40] = other;
            if (found) e.PlayersById[41] = new BattlefieldPlayerFake { CharId = 41, MainLevel = level };
            s.SetupBattlefield(q);
            return string.Join(",", bf.Calls);
        }
        Check($"c3 Q4: {s.Key}: setupBattlefield: the initiator's level in playerLevel (68, 75); no initiator " +
              "found: 99",
              "Battlefield.SetLocalVar(playerLevel,68)|Battlefield.SetLocalVar(playerLevel,75)|" +
              "Battlefield.SetLocalVar(playerLevel,99)",
              $"{Setup(true, 68)}|{Setup(true, 75)}|{Setup(false, 0)}");
    }

    // the framework's onBattlefieldEnter under a Lua module's override of Battlefield.onBattlefieldEnter: the override
    // first, its super the framework's own method, which runs once; with no override the framework alone (PR1)
    static void RunC3Q4Testimony(BattlefieldScript s)
    {
        int item = s.Data.RequiredItems.Items[0];
        string Enter(bool module)
        {
            var bf = new BattlefieldViewFake
            {
                Id = s.Data.BattlefieldId, ZoneId = s.Data.ZoneId, InitiatorValue = (7, "Me"),
            };
            var p = new BattlefieldPlayerFake { CharId = 7, ZoneId = s.Data.ZoneId };
            p.Items.Add(item);
            var q = new BattlefieldContextFake(s) { Player = p, Battlefield = bf };
            var e = (BattlefieldEngineFake)q.Engine;
            foreach (var name in new[] { "TESTIMONY_WEARS", "ENTERING_THE_BATTLEFIELD_FOR", "MEMBERS_OF_YOUR_PARTY",
                                         "TIME_LIMIT_FOR_THIS_BATTLE_IS" })
                e.Texts[$"{s.Data.ZoneId}:{name}"] = 100 + e.Texts.Count;
            e.Uses[item] = 3;
            var supers = new List<string>();
            if (module)
                // testimony_single_use.lua's loop: wear to totalUses - 1, then super
                e.ModuleOverrides["onBattlefieldEnter"] = (cq, super) =>
                {
                    for (int i = p.WornUses(item) + 1; i <= 2; i++) p.IncrementItemWear(item);
                    supers.Add(cq == q ? "same" : "other");
                    super(cq);
                };
            s.OnBattlefieldEnter(q);
            return string.Join(",", e.Calls) + "|" + string.Join(",", supers) + "|" + string.Join(",", p.Calls);
        }
        string Tail = $"Player.MessageSpecial(101,0,{s.Data.Index}),Player.MessageSpecial(102,0,0,0,1)," +
                      "Player.MessageSpecial(103,0,0,0,10)";
        Check("c3 Q4: the framework's onBattlefieldEnter with no module override: one wear, 3 shown, 2 left",
              $"||Player.SetLocalVar(battlefieldID,{s.Data.BattlefieldId}),Player.IncrementItemWear({item})," +
              $"Player.MessageSpecial(100,{item},3,2),{Tail}",
              Enter(false));
        Check("c3 Q4: under a module override (testimony_single_use's): the override first (two wears), its super " +
              "the framework's own method on the call's context, once: the third wear, 1 shown, 0 left",
              $"ModuleOverride(onBattlefieldEnter)|same|Player.IncrementItemWear({item})," +
              $"Player.IncrementItemWear({item}),Player.SetLocalVar(battlefieldID,{s.Data.BattlefieldId})," +
              $"Player.IncrementItemWear({item}),Player.MessageSpecial(100,{item},1,0),{Tail}",
              Enter(true));
    }
}
