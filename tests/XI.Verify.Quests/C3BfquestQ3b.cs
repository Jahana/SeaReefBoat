// XI.Verify.Quests -- quest rework C3, branch Q3b (Quest battlefields): Storms of Fate, Omens, Moment of Truth and
// Puppetmaster Blues. The data of each against its Lua file's, the hooks each overrides, and the arms the traces
// cannot see: Storms' heal (HP, MP, TP and the pet), Omens' death count and flan spawn (a party of seven, the guards),
// Moment of Truth's skip flag and ninth argument, Puppetmaster Blues' registrant split.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    const int C3Q3bFlan = 1000;

    static void RunC3Q3b()
    {
        Console.WriteLine("\n== c3 Q3b: Storms of Fate, Omens, Moment of Truth, Puppetmaster Blues ==");
        var sf = new QC.Battlefields.RiverneSiteB01.StormsOfFate();
        var om = new QC.Battlefields.NavukgoExecutionChamber.Omens();
        var mt = new QC.Battlefields.JadeSepulcher.MomentOfTruth();
        var pb = new QC.Battlefields.TalaccaCove.PuppetmasterBlues();
        var all = new BattlefieldScript[] { sf, om, mt, pb };
        Check("c3 Q3b: the four battlefields validate clean, with their constants and LegacyPath files",
              "|battlefield.storms_of_fate scripts/battlefields/Riverne_Site_B01/storms_of_fate," +
              "battlefield.omens scripts/battlefields/Navukgo_Execution_Chamber/omens," +
              "battlefield.moment_of_truth scripts/battlefields/Jade_Sepulcher/moment_of_truth," +
              "battlefield.puppetmaster_blues scripts/battlefields/Talacca_Cove/puppetmaster_blues",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));

        Check("c3 Q3b: storms of fate's fields (trusts, 18 players, MAX_LEVEL, 30 minutes, index 0, fixed area 1, " +
              "Unstable_Displacement, SD_BCNM_Exit, Jeuno's STORMS_OF_FATE at Quest[3][86]Prog 2, one group)",
              $"{QC.Zone.RiverneSiteB01} {QC.Battlefield.StormsOfFate} False True 18 MAX_LEVEL 1800 0 1 " +
              "Unstable_Displacement SD_BCNM_Exit - Jeuno STORMS_OF_FATE Quest[3][86]Prog 2 False 1",
              C3Q3aFields(sf));
        Check("c3 Q3b: storms of fate: the Conqueror of Fate title; Bahamut alone, allDeath sets WON; overrides " +
              "battlefieldEntry only",
              $"{QC.Title.ConquerorOfFate} BAHAMUT 1 True Battlefield.SetStatus(2) BattlefieldEntry",
              $"{((BattlefieldQuestData)sf.Data).Title} " +
              string.Join(",", sf.Data.Groups[0].MobIds.Select(m => m.Offset == 0 ? m.Key : $"{m.Key}+{m.Offset}")) +
              $" {sf.Data.Groups[0].MobIds.Length} {sf.Data.Groups[0].AllDeath != null} {C3Q3aWon(sf, 0)} " +
              C3Q1Hooks(sf));

        Check("c3 Q3b: omens' fields (18 players, no trusts, no level cap, 30 minutes, index 2, _1s0, _1s1 to " +
              "_1s3, Aht Urhgan's OMENS at Quest[6][22]Prog 0, two groups)",
              $"{QC.Zone.NavukgoExecutionChamber} {QC.Battlefield.Omens} False False 18 - 1800 2 0 _1s0 " +
              "_1s1,_1s2,_1s3 - AhtUrghan OMENS Quest[6][22]Prog 0 False 2",
              C3Q3aFields(om));
        Check("c3 Q3b: omens' groups: the first flan of each area; five added flans to an area, not spawned; both in " +
              "superlink group 1 with a death handler and no allDeath; overrides battlefieldEntry only",
              "IMMORTAL_FLAN,IMMORTAL_FLAN+7,IMMORTAL_FLAN+14 " +
              "IMMORTAL_FLAN+1/IMMORTAL_FLAN+2/IMMORTAL_FLAN+3/IMMORTAL_FLAN+4/IMMORTAL_FLAN+5," +
              "IMMORTAL_FLAN+8/IMMORTAL_FLAN+9/IMMORTAL_FLAN+10/IMMORTAL_FLAN+11/IMMORTAL_FLAN+12," +
              "IMMORTAL_FLAN+15/IMMORTAL_FLAN+16/IMMORTAL_FLAN+17/IMMORTAL_FLAN+18/IMMORTAL_FLAN+19 " +
              "1 1 True True False BattlefieldEntry",
              C3Q3aMobs(om.Data.Groups[0]) + " " + C3Q3aMobs(om.Data.Groups[1]) + " " +
              om.Data.Groups[0].SuperlinkGroup + " " + om.Data.Groups[1].SuperlinkGroup + " " +
              (om.Data.Groups[0].Death != null) + " " + (om.Data.Groups[1].Death != null) + " " +
              om.Data.Groups[1].Spawned.GetValueOrDefault(true) + " " + C3Q1Hooks(om));

        Check("c3 Q3b: moment of truth's fields (6 players, no trusts, 30 minutes, index 3, _1v0, _1v1 to _1v3, " +
              "Aht Urhgan's MOMENT_OF_TRUTH at Quest[6][30]Prog 4, one group)",
              $"{QC.Zone.JadeSepulcher} {QC.Battlefield.MomentOfTruth} False False 6 - 1800 3 0 _1v0 " +
              "_1v1,_1v2,_1v3 - AhtUrghan MOMENT_OF_TRUTH Quest[6][30]Prog 4 False 1",
              C3Q3aFields(mt));
        Check("c3 Q3b: moment of truth: six Shadowhand Kajeel-Ja to an area (+0.., +6.., +12..), allDeath sets WON; " +
              "overrides onBattlefieldWin only",
              string.Join("/", Enumerable.Range(0, 6).Select(i => i == 0 ? "SHADOWHAND_KAJEEL_JA" :
                  $"SHADOWHAND_KAJEEL_JA+{i}")) + "," +
              string.Join("/", Enumerable.Range(6, 6).Select(i => $"SHADOWHAND_KAJEEL_JA+{i}")) + "," +
              string.Join("/", Enumerable.Range(12, 6).Select(i => $"SHADOWHAND_KAJEEL_JA+{i}")) +
              " True Battlefield.SetStatus(2) OnBattlefieldWin",
              C3Q3aMobs(mt.Data.Groups[0]) + " " + (mt.Data.Groups[0].AllDeath != null) + " " + C3Q3aWon(mt, 0) +
              " " + C3Q1Hooks(mt));

        Check("c3 Q3b: puppetmaster blues' fields (6 players, MAX_LEVEL, 30 minutes, index 2, _1l0, _1l1 to _1l3, " +
              "the Toggle Switch and Valkeng's Memory Chip kept, Aht Urhgan's PUPPETMASTER_BLUES, no var, one group)",
              $"{QC.Zone.TalaccaCove} {QC.Battlefield.PuppetmasterBlues} False False 6 MAX_LEVEL 1800 2 0 _1l0 " +
              $"_1l1,_1l2,_1l3 {QC.KeyItem.ToggleSwitch}/{QC.KeyItem.ValkengsMemoryChip}k::: AhtUrghan " +
              "PUPPETMASTER_BLUES - 0 False 1",
              C3Q3aFields(pb));
        Check("c3 Q3b: puppetmaster blues: Valkeng (+1, +2), allDeath sets WON; overrides checkRequirements only",
              "VALKENG,VALKENG+1,VALKENG+2 True Battlefield.SetStatus(2) CheckRequirements",
              C3Q3aMobs(pb.Data.Groups[0]) + " " + (pb.Data.Groups[0].AllDeath != null) + " " + C3Q3aWon(pb, 0) +
              " " + C3Q1Hooks(pb));

        RunC3Q3bHeal(sf);
        RunC3Q3bOmens(om);
        RunC3Q3bMoment(mt);
        RunC3Q3bPuppet(pb);
    }

    // Storms' healCharacter: a living char gets its HP and MP filled and its TP emptied, and so does its pet
    static void RunC3Q3bHeal(BattlefieldScript sf)
    {
        string Heal(bool alive, bool withPet)
        {
            var (q, p) = C3Q1Ctx(sf, "x");
            p.IsAlive = alive;
            p.MaxHP = 1500;
            p.MaxMP = 700;
            var pet = new BattlefieldMobFake { Id = 5, MaxHP = 900, MaxMP = 300 };
            if (withPet) p.PetMob = pet;
            sf.BattlefieldEntry(q);
            return string.Join(",", p.Calls) + "|" + string.Join(",", pet.Calls);
        }
        Check("c3 Q3b: storms of fate heals a living char: HP to its max, MP to its max, TP to 0",
              "Player.SetHP(1500),Player.SetMP(700),Player.SetTP(0)|", Heal(true, false));
        Check("c3 Q3b: storms of fate heals the pet of a living char the same way (its own maxima)",
              "Player.SetHP(1500),Player.SetMP(700),Player.SetTP(0)|Mob.SetHP(900),Mob.SetMP(300),Mob.SetTP(0)",
              Heal(true, true));
        Check("c3 Q3b: storms of fate heals a dead char and its pet not at all",
              "|", Heal(false, true));
    }

    // Omens: handleDeath (the win once no flan up to numSpawned of the area is spawned and alive) and the spawn of one
    // more flan for every three players entering after the first
    static (BattlefieldContextFake Q, BattlefieldViewFake Bf, BattlefieldEngineFake E) C3Q3bOmensCtx(
        BattlefieldScript om, int area)
    {
        var (q, _) = C3Q1Ctx(om, "_1s0");
        var bf = new BattlefieldViewFake { Id = om.Data.BattlefieldId, Area = area };
        var e = new BattlefieldEngineFake();
        e.ZoneMobIds[$"{QC.Zone.NavukgoExecutionChamber}:IMMORTAL_FLAN"] = C3Q3bFlan;
        q.Battlefield = bf;
        q.Engine = e;
        return (q, bf, e);
    }

    static BattlefieldMobFake C3Q3bFlanAt(BattlefieldEngineFake e, int id, bool spawned, int hp = 100)
    {
        var m = new BattlefieldMobFake { Id = id, IsSpawned = spawned, MaxHP = 100, HP = hp };
        e.MobsById[id] = m;
        return m;
    }

    static void RunC3Q3bOmens(BattlefieldScript om)
    {
        // what handleDeath did: the status set (2 is WON) or nothing
        string Death(int area, int numSpawned, Action<BattlefieldEngineFake> setup)
        {
            var (q, bf, e) = C3Q3bOmensCtx(om, area);
            bf.LocalVars["numSpawned"] = numSpawned;
            setup(e);
            om.Data.Groups[0].Death(q, new BattlefieldMobFake { Id = 1 }, 1);
            om.Data.Groups[1].Death(q, new BattlefieldMobFake { Id = 1 }, 1);
            return string.Join(",", bf.Calls);
        }
        Check("c3 Q3b: omens' death: the area's first flan alive, spawned: no win; dead: WON, from either group",
              "||Battlefield.SetStatus(2),Battlefield.SetStatus(2)",
              Death(1, 0, e => C3Q3bFlanAt(e, C3Q3bFlan, true)) + "||" +
              Death(1, 0, e => C3Q3bFlanAt(e, C3Q3bFlan, true, 0)));
        Check("c3 Q3b: omens' death: area 2 counts from the flan 7 on (1007 alive: no win; only 1000 alive: WON)",
              "|Battlefield.SetStatus(2),Battlefield.SetStatus(2)",
              Death(2, 0, e => C3Q3bFlanAt(e, C3Q3bFlan + 7, true)) + "|" +
              Death(2, 0, e => C3Q3bFlanAt(e, C3Q3bFlan, true)));
        Check("c3 Q3b: omens' death: area 3 counts from the flan 14 on",
              "|Battlefield.SetStatus(2),Battlefield.SetStatus(2)",
              Death(3, 0, e => C3Q3bFlanAt(e, C3Q3bFlan + 14, true)) + "|" +
              Death(3, 0, e => C3Q3bFlanAt(e, C3Q3bFlan + 7, true)));
        Check("c3 Q3b: omens' death: numSpawned 1 counts the second flan (alive: no win), numSpawned 0 does not",
              "|Battlefield.SetStatus(2),Battlefield.SetStatus(2)",
              Death(1, 1, e =>
              {
                  C3Q3bFlanAt(e, C3Q3bFlan, true, 0);
                  C3Q3bFlanAt(e, C3Q3bFlan + 1, true);
              }) + "|" +
              Death(1, 0, e =>
              {
                  C3Q3bFlanAt(e, C3Q3bFlan, true, 0);
                  C3Q3bFlanAt(e, C3Q3bFlan + 1, true);
              }));
        Check("c3 Q3b: omens' death: a flan counted but absent from the zone, one not spawned (not dead) and one " +
              "dead each leave the win to the others",
              "Battlefield.SetStatus(2),Battlefield.SetStatus(2)|Battlefield.SetStatus(2),Battlefield.SetStatus(2)|" +
              "Battlefield.SetStatus(2),Battlefield.SetStatus(2)",
              Death(1, 2, e => C3Q3bFlanAt(e, C3Q3bFlan, true, 0)) + "|" +
              Death(1, 0, e =>
              {
                  var m = C3Q3bFlanAt(e, C3Q3bFlan, false);
                  m.IsDead = false;
              }) + "|" +
              Death(1, 0, e => C3Q3bFlanAt(e, C3Q3bFlan, true, 0)));

        // battlefieldEntry: players inside, the area, the flans present, then what it spawned and the numSpawned set
        string Entry(int players, int area, Action<BattlefieldEngineFake> setup)
        {
            var (q, bf, e) = C3Q3bOmensCtx(om, area);
            for (int i = 0; i < players; i++) bf.PlayerList.Add(new BattlefieldPlayerFake());
            setup(e);
            // a flan the zone lacks is skipped: an entry that throws (a null flan spawned) is a failed check
            try { om.BattlefieldEntry(q); }
            catch (Exception ex) { return "THREW " + ex.GetType().Name; }
            return string.Join(",", e.MobsById.Values.OfType<BattlefieldMobFake>().SelectMany(m => m.Calls)) + "/" +
                   string.Join(",", bf.Calls);
        }
        void Flans(BattlefieldEngineFake e)
        {
            for (int i = 0; i < 21; i++) C3Q3bFlanAt(e, C3Q3bFlan + i, false);
        }
        Check("c3 Q3b: omens' entry: one player or two or three: no flan spawned",
              "/ / /", Entry(1, 1, Flans) + " " + Entry(2, 1, Flans) + " " + Entry(3, 1, Flans));
        Check("c3 Q3b: omens' entry: five players (four after the first): no flan spawned",
              "/", Entry(5, 1, Flans));
        Check("c3 Q3b: omens' entry: four players spawn the second flan and set numSpawned 1; seven the third, 2",
              "Mob.Spawn(,)/Battlefield.SetLocalVar(numSpawned,1) Mob.Spawn(,)/Battlefield.SetLocalVar(numSpawned,2)",
              Entry(4, 1, Flans) + " " + Entry(7, 1, Flans));
        Check("c3 Q3b: omens' entry: four players in area 2 spawn flan 8 (1008), in area 3 flan 15 (1015)",
              "1008 1015",
              string.Join(" ", new[] { 2, 3 }.Select(a =>
              {
                  var (q, bf, e) = C3Q3bOmensCtx(om, a);
                  for (int i = 0; i < 4; i++) bf.PlayerList.Add(new BattlefieldPlayerFake());
                  Flans(e);
                  om.BattlefieldEntry(q);
                  var up = e.MobsById.Values.OfType<BattlefieldMobFake>().Where(m => m.IsSpawned).ToList();
                  return up.Count == 1 ? up[0].Id.ToString() : "spawned " + up.Count;
              })));
        Check("c3 Q3b: omens' entry: a flan already spawned is not spawned again and numSpawned is not set; a flan " +
              "the zone lacks is skipped",
              "/ /",
              Entry(4, 1, e =>
              {
                  Flans(e);
                  e.MobsById[C3Q3bFlan + 1] = new BattlefieldMobFake { Id = C3Q3bFlan + 1, IsSpawned = true };
              }) + " " + Entry(4, 1, e => { }));
    }

    // Moment of Truth's onBattlefieldWin: battlefieldWin for an accepted quest only, the skip flag 1 for a completed
    // quest only, the ninth argument 2
    static void RunC3Q3bMoment(BattlefieldScript mt)
    {
        string Win(QuestStatus status)
        {
            var (q, p) = C3Q1Ctx(mt, "x");
            var bf = new BattlefieldViewFake { Id = 4242, Area = 2, TimeInside = 77 };
            bf.SetRecord("rec", 123, 4);
            q.Battlefield = bf;
            p.QuestStatuses["AhtUrghan/MOMENT_OF_TRUTH"] = status;
            mt.OnBattlefieldWin(q);
            return string.Join(",", p.Calls) + "|" + p.PlayerLocalVar("battlefieldWin");
        }
        int zone = QC.Zone.JadeSepulcher;
        Check("c3 Q3b: moment of truth's win, an accepted quest: battlefieldWin set to the battlefield id, the " +
              "event 32001 (area, clear time, party size, time inside, zone, index, skip flag 0, 2)",
              $"Player.SetLocalVar(battlefieldWin,4242),Player.StartEvent(32001,2,123,4,77,{zone},3,0,2)|4242",
              Win(QuestStatus.Accepted));
        Check("c3 Q3b: moment of truth's win, a completed quest: no battlefieldWin, the skip flag 1",
              $"Player.StartEvent(32001,2,123,4,77,{zone},3,1,2)|0", Win(QuestStatus.Completed));
        Check("c3 Q3b: moment of truth's win, an available quest: no battlefieldWin, the skip flag 0",
              $"Player.StartEvent(32001,2,123,4,77,{zone},3,0,2)|0", Win(QuestStatus.Available));
    }

    // Puppetmaster Blues' checkRequirements: the registrant goes through the framework (key items, quest), the rest of
    // the party only through isValidEntry (the entry NPC)
    static void RunC3Q3bPuppet(BattlefieldScript pb)
    {
        string Req(bool registrant, string npc, bool toggle, bool chip, QuestStatus status, bool trade = false)
        {
            var (q, p) = C3Q1Ctx(pb, npc);
            if (trade) q.Trade = new BattlefieldPlayerFake();
            p.QuestStatuses["AhtUrghan/PUPPETMASTER_BLUES"] = status;
            if (toggle) p.KeyItems[QC.KeyItem.ToggleSwitch] = true;
            if (chip) p.KeyItems[QC.KeyItem.ValkengsMemoryChip] = true;
            return pb.CheckRequirements(q, registrant).ToString();
        }
        Check("c3 Q3b: puppetmaster blues' registrant: the accepted quest with both key items passes; either key " +
              "item missing fails; the quest available fails",
              "True False False False",
              $"{Req(true, "_1l0", true, true, QuestStatus.Accepted)} " +
              $"{Req(true, "_1l0", false, true, QuestStatus.Accepted)} " +
              $"{Req(true, "_1l0", true, false, QuestStatus.Accepted)} " +
              Req(true, "_1l0", true, true, QuestStatus.Available));
        // the registrant flag reaches the framework: it refuses a registrant that trades for a fight that takes no
        // trade (battlefield.lua:581), and not a member
        Check("c3 Q3b: puppetmaster blues' registrant is told so: a trade is refused for it, and not for a member",
              "False True",
              $"{Req(true, "_1l0", true, true, QuestStatus.Accepted, true)} " +
              Req(false, "_1l0", true, true, QuestStatus.Accepted, true));
        Check("c3 Q3b: puppetmaster blues' other members: the entry NPC alone passes (no key items, no quest); " +
              "another NPC fails",
              "True True False",
              $"{Req(false, "_1l0", false, false, QuestStatus.Available)} " +
              $"{Req(false, "_1l0", true, false, QuestStatus.Completed)} " +
              Req(false, "x", true, true, QuestStatus.Accepted));
    }
}
