// XI.Verify.Quests -- quest rework C3, branch Q3c (Quest battlefields): Requiem of Sin and Tango with a Tracker in
// Boneyard Gully, and Waking Dreams in The Shrouded Maw. The data of each against its Lua file's, the hooks each
// overrides, the entry checks, Waking Dreams' speech value and win, and the arms the traces cannot see: the
// Shikarees' skillchain states (Shared/ShikareeSkillchain), where a trace reaches only IDLE and SOLO, and the Diremite
// pits' tile and respawn keys (Shared/DiremitePits).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // the zone mob ids of the Shikarees: Z, Y, X are consecutive (Z + 1 = Y, Z + 2 = X) as the AI's loops read them
    const int C3Q3cZ = 2000;
    const int C3Q3cY = 2001;
    const int C3Q3cX = 2002;
    // the text ids of the Shikarees' lines
    const int C3Q3cTextZ = 7731;
    const int C3Q3cTextY = 7742;
    const int C3Q3cTextX = 7753;

    static void RunC3Q3c()
    {
        Console.WriteLine("\n== c3 Q3c: Requiem of Sin, Tango with a Tracker, Waking Dreams ==");
        var ros = new QC.Battlefields.BoneyardGully.RequiemOfSin();
        var twt = new QC.Battlefields.BoneyardGully.TangoWithATracker();
        var wd = new QC.Battlefields.TheShroudedMaw.WakingDreams();
        var all = new BattlefieldScript[] { ros, twt, wd };
        Check("c3 Q3c: the three battlefields validate clean, with their constants and LegacyPath files",
              "|battlefield.requiem_of_sin scripts/battlefields/Boneyard_Gully/requiem_of_sin," +
              "battlefield.tango_with_a_tracker scripts/battlefields/Boneyard_Gully/tango_with_a_tracker," +
              "battlefield.waking_dreams scripts/battlefields/The_Shrouded_Maw/waking_dreams",
              string.Join(" | ", all.SelectMany(BattlefieldScriptValidator.Validate)) + "|" +
              string.Join(",", all.Select(s => $"{s.Key} {s.LegacyPath}")));

        Check("c3 Q3c: requiem of sin's fields (6 players, 30 minutes, index 6, _081, _082 _084 _086, either letter " +
              "kept, BURSTS_INTO_FLAMES, the other-areas quest REQUIEM_OF_SIN, three groups)",
              $"{QC.Zone.BoneyardGully} {QC.Battlefield.RequiemOfSin} False False 6 - 1800 6 0 _081 _082,_084,_086 " +
              $"{QC.KeyItem.LetterFromShikareeY}+{QC.KeyItem.LetterFromTheMithranTrackers}:" +
              "8:BURSTS_INTO_FLAMES:: OtherAreas REQUIEM_OF_SIN - 0 False 3",
              C3Q3aFields(ros));
        Check("c3 Q3c: requiem of sin's key item: the Letter from Shikaree Y or the Letter from the Mithran " +
              "Trackers, either one, with BURSTS_INTO_FLAMES",
              $"{QC.KeyItem.LetterFromShikareeY}+{QC.KeyItem.LetterFromTheMithranTrackers} False False",
              C3Q3cKeyItems(ros));
        Check("c3 Q3c: requiem of sin's groups: the three Shikarees of an area (offsets 0, 6, 12), the +4 mob and " +
              "the +3 mob (not spawned) of Z; all superlinked in group 1; only the first has allDeath",
              "SHIKAREE_Z_ROS/SHIKAREE_Y_ROS_TWT/SHIKAREE_X_ROS_TWT," +
              "SHIKAREE_Z_ROS+6/SHIKAREE_Y_ROS_TWT+6/SHIKAREE_X_ROS_TWT+6," +
              "SHIKAREE_Z_ROS+12/SHIKAREE_Y_ROS_TWT+12/SHIKAREE_X_ROS_TWT+12 " +
              "SHIKAREE_Z_ROS+4,SHIKAREE_Z_ROS+10,SHIKAREE_Z_ROS+16 " +
              "SHIKAREE_Z_ROS+3,SHIKAREE_Z_ROS+9,SHIKAREE_Z_ROS+15 1 1 1 True False False False " +
              "Battlefield.SetLocalVar(cutsceneTimer,5),Battlefield.SetStatus(2)",
              string.Join(" ", ros.Data.Groups.Select(C3Q3aMobs)) + " " +
              string.Join(" ", ros.Data.Groups.Select(g => g.SuperlinkGroup)) + " " +
              string.Join(" ", ros.Data.Groups.Select(g => g.AllDeath != null)) + " " +
              ros.Data.Groups[2].Spawned.GetValueOrDefault(true) + " " + C3Q3cAllDeath(ros));
        Check("c3 Q3c: requiem of sin's armoury crates: Z + 5, + 11, + 17",
              "SHIKAREE_Z_ROS+5,SHIKAREE_Z_ROS+11,SHIKAREE_Z_ROS+17",
              string.Join(",", ros.Data.ArmouryCrates.Select(c => $"{c.Id.Key}+{c.Id.Offset}")));
        Check("c3 Q3c: requiem of sin's loot: four groups, the first rolled twice, every item and weight",
              $"2:{QC.Item.None}x7500,{QC.Item.PotOfViridianUrushi}x3500,{QC.Item.PieceOfCassiaLumber}x2500," +
              $"{QC.Item.DragonBone}x2500,{QC.Item.SquareOfEltoroLeather}x2500,{QC.Item.SquareOfGalateia}x2500," +
              $"{QC.Item.SquareOfKejusuSatin}x2500|:{QC.Item.None}x7500,{QC.Item.XsKnife}x500," +
              $"{QC.Item.YsScythe}x500,{QC.Item.ZsTrident}x500,{QC.Item.LacquerTreeLog}x50," +
              $"{QC.Item.PhoenixFeather}x50,{QC.Item.PhilosophersStone}x50|:{QC.Item.None}x750," +
              $"{QC.Item.LacquerTreeLog}x50,{QC.Item.PhoenixFeather}x50,{QC.Item.PhilosophersStone}x50|" +
              $":{QC.Item.None}x800,{QC.Item.PetrifiedLog}x40,{QC.Item.DivineLog}x40," +
              $"{QC.Item.SquareOfShiningCloth}x40,{QC.Item.SquareOfRaxa}x40,{QC.Item.SlabOfGranite}x40",
              C3Q3cLoot(ros));
        Check("c3 Q3c: requiem of sin overrides checkRequirements and onBattlefieldTick only",
              "CheckRequirements,OnBattlefieldTick", C3Q1Hooks(ros));

        Check("c3 Q3c: tango with a tracker's fields (6 players, 30 minutes, index 5, _081, _082 _084 _086, the " +
              "Letter from Shikaree X from the initiator only, BURSTS_INTO_FLAMES, the other-areas quest " +
              "TANGO_WITH_A_TRACKER, two groups)",
              $"{QC.Zone.BoneyardGully} {QC.Battlefield.TangoWithATracker} False False 6 - 1800 5 0 _081 " +
              $"_082,_084,_086 {QC.KeyItem.LetterFromShikareeX}i:8:BURSTS_INTO_FLAMES:: OtherAreas " +
              "TANGO_WITH_A_TRACKER - 0 False 2",
              C3Q3aFields(twt));
        Check("c3 Q3c: tango with a tracker's key item: the Letter from Shikaree X alone, from the initiator only, " +
              "with BURSTS_INTO_FLAMES",
              $"{QC.KeyItem.LetterFromShikareeX} True False",
              C3Q3cKeyItems(twt));
        Check("c3 Q3c: tango with a tracker's groups: Y and X of an area (offsets 0, 6, 12) and X's rabbit (+2, +8, " +
              "+14), both in superlink group 1; only the first has allDeath, which sets WON",
              "SHIKAREE_Y_ROS_TWT/SHIKAREE_X_ROS_TWT,SHIKAREE_Y_ROS_TWT+6/SHIKAREE_X_ROS_TWT+6," +
              "SHIKAREE_Y_ROS_TWT+12/SHIKAREE_X_ROS_TWT+12 " +
              "SHIKAREE_X_ROS_TWT+2,SHIKAREE_X_ROS_TWT+8,SHIKAREE_X_ROS_TWT+14 1 1 True False Battlefield.SetStatus(2)",
              C3Q3aMobs(twt.Data.Groups[0]) + " " + C3Q3aMobs(twt.Data.Groups[1]) + " " +
              twt.Data.Groups[0].SuperlinkGroup + " " + twt.Data.Groups[1].SuperlinkGroup + " " +
              (twt.Data.Groups[0].AllDeath != null) + " " + (twt.Data.Groups[1].AllDeath != null) + " " +
              C3Q3aWon(twt, 0));
        Check("c3 Q3c: tango with a tracker overrides checkRequirements and onBattlefieldTick only",
              "CheckRequirements,OnBattlefieldTick", C3Q1Hooks(twt));

        Check("c3 Q3c: waking dreams' fields (no experience lost, 6 players, 30 minutes, index 2, MC_Entrance, " +
              "Memento_Circle, the Vial of Dream Incense kept, Windurst's WAKING_DREAMS, two groups)",
              $"{QC.Zone.TheShroudedMaw} {QC.Battlefield.WakingDreams} False False 6 - 1800 2 0 MC_Entrance " +
              $"Memento_Circle {QC.KeyItem.VialOfDreamIncense}k::: Windurst WAKING_DREAMS - 0 False 2",
              C3Q3aFields(wd));
        Check("c3 Q3c: waking dreams' groups: the Diabolos of an area (+0, +7, +14) with allDeath setting WON; its " +
              "six Diremites (+1.., +8.., +15..), superlinked",
              "DIABOLOS_WD,DIABOLOS_WD+7,DIABOLOS_WD+14 " +
              "DIABOLOS_WD+1/DIABOLOS_WD+2/DIABOLOS_WD+3/DIABOLOS_WD+4/DIABOLOS_WD+5/DIABOLOS_WD+6," +
              "DIABOLOS_WD+8/DIABOLOS_WD+9/DIABOLOS_WD+10/DIABOLOS_WD+11/DIABOLOS_WD+12/DIABOLOS_WD+13," +
              "DIABOLOS_WD+15/DIABOLOS_WD+16/DIABOLOS_WD+17/DIABOLOS_WD+18/DIABOLOS_WD+19/DIABOLOS_WD+20 " +
              "True False False True Battlefield.SetStatus(2)",
              C3Q3aMobs(wd.Data.Groups[0]) + " " + C3Q3aMobs(wd.Data.Groups[1]) + " " +
              (wd.Data.Groups[0].AllDeath != null) + " " + wd.Data.Groups[0].Superlink + " " +
              (wd.Data.Groups[1].AllDeath != null) + " " + wd.Data.Groups[1].Superlink + " " + C3Q3aWon(wd, 0));
        Check("c3 Q3c: waking dreams overrides checkSkipCutscene, onEntryEventUpdate, onBattlefieldWin, " +
              "onEventFinishWin, setupBattlefield and onBattlefieldTick",
              "CheckSkipCutscene,OnBattlefieldTick,OnBattlefieldWin,OnEntryEventUpdate,OnEventFinishWin," +
              "SetupBattlefield", C3Q1Hooks(wd));

        RunC3Q3cRequirements(ros, twt);
        RunC3Q3cWaking(wd);
        RunC3Q3cChain(ros, twt);
    }

    static string C3Q3cKeyItems(BattlefieldScript s)
    {
        var k = s.Data.RequiredKeyItems;
        return string.Join(" ", k.Items.Select(i => string.Join("+", i.Ids))) + " " + k.OnlyInitiator + " " + k.Keep;
    }

    static string C3Q3cAllDeath(BattlefieldScript s)
    {
        var bf = new BattlefieldViewFake { Id = s.Data.BattlefieldId };
        var e = new BattlefieldEngineFake();
        var q = new BattlefieldContextFake(s) { Battlefield = bf, Engine = e };
        s.Data.Groups[0].AllDeath(q, new BattlefieldMobFake { Id = 1 });
        return string.Join(",", bf.Calls.Concat(e.Calls));
    }

    static string C3Q3cLoot(BattlefieldScript s) =>
        string.Join("|", s.Data.Loot.Select(g =>
            $"{g.Quantity}:" + string.Join(",", g.Entries.Select(e => $"{e.ItemId}x{e.Weight}"))));

    // checkRequirements: the entry NPC check first, then the letters (and, for the party of Tango with a Tracker, the
    // quest completed); the quest status of the entry is not read
    static void RunC3Q3cRequirements(BattlefieldScript ros, BattlefieldScript twt)
    {
        bool Req(BattlefieldScript s, string npc, bool registrant, int[] keyItems, bool twtDone = false,
            QuestStatus? rosStatus = null)
        {
            var (q, p) = C3Q1Ctx(s, npc);
            foreach (int k in keyItems) p.KeyItems[k] = true;
            if (twtDone) p.CompletedQuests.Add("OtherAreas/TANGO_WITH_A_TRACKER");
            if (rosStatus != null) p.QuestStatuses["OtherAreas/REQUIEM_OF_SIN"] = rosStatus.Value;
            return s.CheckRequirements(q, registrant);
        }
        string Join(params bool[] results) => string.Join(" ", results.Select(b => b.ToString()));
        var none = new int[0];
        var y = new[] { QC.KeyItem.LetterFromShikareeY };
        var mithran = new[] { QC.KeyItem.LetterFromTheMithranTrackers };
        var both = y.Concat(mithran).ToArray();
        var x = new[] { QC.KeyItem.LetterFromShikareeX };
        Check("c3 Q3c: requiem of sin's entry: the Letter from Shikaree Y alone, the Mithran letter alone and both " +
              "enter; neither does not; the letter at an exit NPC does not; the quest status is not read",
              "True True True False False True",
              Join(Req(ros, "_081", true, y), Req(ros, "_081", true, mithran), Req(ros, "_081", true, both),
                  Req(ros, "_081", true, none), Req(ros, "_082", true, y),
                  Req(ros, "_081", true, mithran, false, QuestStatus.Available)));
        Check("c3 Q3c: requiem of sin's entry is the same for the party (not the registrant): either letter, " +
              "else refused",
              "True True False False",
              Join(Req(ros, "_081", false, y), Req(ros, "_081", false, mithran), Req(ros, "_081", false, none),
                  Req(ros, "_086", false, mithran)));
        Check("c3 Q3c: tango with a tracker's registrant needs the Letter from Shikaree X at the entry NPC (the " +
              "completed quest does not do)",
              "True False False False",
              Join(Req(twt, "_081", true, x), Req(twt, "_081", true, none, true), Req(twt, "_081", true, none),
                  Req(twt, "_082", true, x)));
        Check("c3 Q3c: tango with a tracker's party enters with the letter or with the quest completed, at the " +
              "entry NPC only",
              "True True False False False",
              Join(Req(twt, "_081", false, x), Req(twt, "_081", false, none, true), Req(twt, "_081", false, none),
                  Req(twt, "_082", false, x), Req(twt, "_084", false, none, true)));
    }

    // Waking Dreams: the speech value (csParam8 and the win's ninth argument), the win, the finish, the tiles and the
    // Diremite respawn
    static void RunC3Q3cWaking(BattlefieldScript wd)
    {
        var data = (BattlefieldQuestData)wd.Data;
        string Speech(bool quest, bool dawn)
        {
            var (q, p) = C3Q1Ctx(wd, "MC_Entrance");
            if (quest) p.CompletedQuests.Add("Windurst/WAKING_DREAMS");
            if (dawn) p.CompletedMissions.Add($"{QC.MissionLog.Cop}/{QC.Mission.Cop.Dawn}");
            data.CsParam8 = 99;
            wd.OnEntryEventUpdate(q, 32000, 33);
            return data.CsParam8.ToString();
        }
        Check("c3 Q3c: waking dreams' entry update sets csParam8 to 0 (neither), 4 (Dawn completed), 5 (the quest " +
              "completed, with or without Dawn)",
              "0 4 5 5", $"{Speech(false, false)} {Speech(false, true)} {Speech(true, false)} {Speech(true, true)}");

        string Win(QuestStatus? status, bool quest, bool dawn)
        {
            var (q, p) = C3Q1Ctx(wd, "x");
            p.ZoneId = QC.Zone.TheShroudedMaw;
            var bf = new BattlefieldViewFake { Id = data.BattlefieldId, Area = 2, TimeInside = 55 };
            bf.SetRecord("rec", 77, 3);
            q.Battlefield = bf;
            if (status != null) p.QuestStatuses["Windurst/WAKING_DREAMS"] = status.Value;
            if (quest) p.CompletedQuests.Add("Windurst/WAKING_DREAMS");
            if (dawn) p.CompletedMissions.Add($"{QC.MissionLog.Cop}/{QC.Mission.Cop.Dawn}");
            wd.OnBattlefieldWin(q);
            return string.Join(",", p.Calls);
        }
        string Ev(int speech) => $"Player.StartEvent(32001,2,77,3,55,{QC.Zone.TheShroudedMaw},2,0,{speech})";
        Check("c3 Q3c: waking dreams' win: battlefieldWin set only while the quest is accepted; the event 32001 " +
              "(area, clear time, party size, time inside, zone, index 2, 0, speech) in every case",
              $"Player.SetLocalVar(battlefieldWin,{data.BattlefieldId}),{Ev(0)}|{Ev(0)}|{Ev(5)}|{Ev(4)}|{Ev(0)}",
              $"{Win(QuestStatus.Accepted, false, false)}|{Win(QuestStatus.Available, false, false)}|" +
              $"{Win(QuestStatus.Completed, true, false)}|{Win(null, false, true)}|{Win(null, false, false)}");
        {
            var (q, p) = C3Q1Ctx(wd, "x");
            wd.OnEventFinishWin(q, 32001, 7);
            Check("c3 Q3c: waking dreams' finish takes the vial, gives the Whisper of Dreams and the Heir to the " +
                  "Realm of Dreams title",
                  $"Player.DelKeyItem({QC.KeyItem.VialOfDreamIncense})," +
                  $"Player.GiveKeyItem({QC.KeyItem.WhisperOfDreams})," +
                  $"Player.AddTitle({QC.Title.HeirToTheRealmOfDreams})",
                  string.Join(",", p.Calls));
        }
        Check("c3 Q3c: waking dreams never offers to skip the cutscene",
              "False", wd.CheckSkipCutscene(C3Q1Ctx(wd, "x").Q).ToString());

        string Setup(int area)
        {
            var (q, _) = C3Q1Ctx(wd, "MC_Entrance");
            var e = (BattlefieldEngineFake)q.Engine;
            e.ZoneNpcIds[$"{QC.Zone.TheShroudedMaw}:DARKNESS_NAMED_TILE_OFFSET"] = 5000;
            var tiles = new Dictionary<int, BattlefieldNpcFake>();
            for (int id = 4990; id < 5040; id++) e.Npcs[id] = tiles[id] = new BattlefieldNpcFake { Id = id };
            q.Battlefield = new BattlefieldViewFake { Id = data.BattlefieldId, ZoneId = data.ZoneId, Area = area };
            wd.SetupBattlefield(q);
            return string.Join(",", tiles.Where(t => t.Value.Animations.Count > 0)
                .Select(t => $"{t.Key}:{string.Join("+", t.Value.Animations)}"));
        }
        Check("c3 Q3c: waking dreams' setupBattlefield closes the area's eight tiles (offset + 8 * (area - 1)) " +
              "with animation 9",
              "5000:9,5001:9,5002:9,5003:9,5004:9,5005:9,5006:9,5007:9|" +
              "5008:9,5009:9,5010:9,5011:9,5012:9,5013:9,5014:9,5015:9|" +
              "5016:9,5017:9,5018:9,5019:9,5020:9,5021:9,5022:9,5023:9", $"{Setup(1)}|{Setup(2)}|{Setup(3)}");

        string Tick(int area)
        {
            var (q, _) = C3Q1Ctx(wd, "MC_Entrance");
            var e = (BattlefieldEngineFake)q.Engine;
            e.ZoneMobIds[$"{QC.Zone.TheShroudedMaw}:DIABOLOS_WD"] = 3000;
            e.SystemTime = 2000;
            q.Battlefield = new BattlefieldViewFake { Id = data.BattlefieldId, ZoneId = data.ZoneId, Area = area };
            foreach (int id in new[] { 3001, 3008, 3015 })
            {
                var m = new BattlefieldMobFake { Id = id, IsSpawned = false };
                m.LocalVars["deathTime"] = 1000;
                e.MobsById[id] = m;
            }
            try { wd.OnBattlefieldTick(q, 1); }
            catch (Exception ex) { return "THREW " + ex.GetType().Name; }
            return string.Join(",", e.Calls);
        }
        Check("c3 Q3c: waking dreams' onBattlefieldTick walks the Diremites of DIABOLOS_WD (3001, 3008, 3015 by area)",
              "SpawnMob(3001)|SpawnMob(3008)|SpawnMob(3015)", $"{Tick(1)}|{Tick(2)}|{Tick(3)}");
    }

    // an arena of Shikarees: the battlefield's scState and scLeader set, the mobs of the area added by base id (the id
    // of the first area; the area's stride of 6 is added), a foe (9000) as the target of every mob that has one
    sealed class C3Q3cArena
    {
        public readonly BattlefieldContextFake Q;
        public readonly BattlefieldViewFake Bf;
        public readonly BattlefieldEngineFake E;
        public readonly BattlefieldMobFake Foe = new BattlefieldMobFake { Id = 9000, IsMob = false, HP = 100 };
        readonly BattlefieldScript script;
        readonly int offset;

        public C3Q3cArena(BattlefieldScript script, int area, int state, int leaderBase)
        {
            this.script = script;
            offset = (area - 1) * 6;
            var (q, _) = C3Q1Ctx(script, "x");
            Q = q;
            E = (BattlefieldEngineFake)q.Engine;
            int zone = script.Data.ZoneId;
            E.ZoneMobIds[$"{zone}:SHIKAREE_Z_ROS"] = C3Q3cZ;
            E.ZoneMobIds[$"{zone}:SHIKAREE_Y_ROS_TWT"] = C3Q3cY;
            E.ZoneMobIds[$"{zone}:SHIKAREE_X_ROS_TWT"] = C3Q3cX;
            E.Texts[$"{zone}:SHIKAREE_Z_OFFSET"] = C3Q3cTextZ;
            E.Texts[$"{zone}:SHIKAREE_Y_OFFSET"] = C3Q3cTextY;
            E.Texts[$"{zone}:SHIKAREE_X_OFFSET"] = C3Q3cTextX;
            Bf = new BattlefieldViewFake { Id = script.Data.BattlefieldId, ZoneId = zone, Area = area };
            Bf.LocalVars["scState"] = state;
            Bf.LocalVars["scLeader"] = leaderBase == 0 ? 0 : leaderBase + offset;
            Q.Battlefield = Bf;
        }

        public BattlefieldMobFake Add(int baseId, int tp = 0, bool alive = true, bool target = true)
        {
            var m = new BattlefieldMobFake
            {
                Id = baseId + offset, IsSpawned = alive, HP = alive ? 100 : 0, MaxHP = 100, TP = tp, Battlefield = Bf,
            };
            if (target) m.Target = Foe;
            E.MobsById[m.Id] = m;
            return m;
        }

        public BattlefieldMobFake this[int baseId] => (BattlefieldMobFake)E.MobsById[baseId + offset];

        IEnumerable<BattlefieldMobFake> Mobs() =>
            E.MobsById.Values.Cast<BattlefieldMobFake>().OrderBy(m => m.Id);

        // the tick, then every call it made: the battlefield's local vars first, then each mob's (not its local vars)
        public string Tick()
        {
            try { script.OnBattlefieldTick(Q, 1); }
            catch (Exception ex) { return "THREW " + ex.GetType().Name; }
            // (the base tick's own calls on the battlefield, such as its wipe time, are not the AI's)
            var calls = Bf.Calls.Where(c => c.StartsWith("Battlefield.SetLocalVar("))
                .Select(c => c.Replace("Battlefield.SetLocalVar(", "bf(")).ToList();
            foreach (var m in Mobs())
            {
                calls.AddRange(m.Calls.Where(c => !c.StartsWith("Mob.SetLocalVar"))
                    .Select(c => $"{m.Id}:{c.Substring(4)}"));
            }

            return string.Join(";", calls);
        }

        // the battlefield's state and leader, then scSkill/scMessage/scNextID of each mob that has a skill
        public string State() =>
            string.Join(" ", new[] { $"s{Bf.LocalVar("scState")} l{Bf.LocalVar("scLeader")}" }.Concat(
                Mobs().Where(m => m.LocalVar("scSkill") > 0)
                    .Select(m => $"{m.Id}:{m.LocalVar("scSkill")}/{m.LocalVar("scMessage")}/" +
                                 $"{m.LocalVar("scNextID")}")));
    }

    // the Shikarees' skillchain AI, state by state (Shared/ShikareeSkillchain)
    static void RunC3Q3cChain(BattlefieldScript ros, BattlefieldScript twt)
    {
        const int Z = C3Q3cZ, Y = C3Q3cY, X = C3Q3cX;
        const string Sep = " | ";

        // IDLE: the first Shikaree (in slot order) alive with 1000 TP or more leads and says its line + 1
        string Idle(BattlefieldScript s, int area, params (int Id, int Tp, bool Alive)[] mobs)
        {
            var a = new C3Q3cArena(s, area, 0, 0);
            foreach (var m in mobs) a.Add(m.Id, m.Tp, m.Alive);
            return a.Tick();
        }
        Check("c3 Q3c: requiem of sin's IDLE: the first of Z, Y, X alive with 1000 TP leads (state READY, its id, " +
              "its line + 1); 999 TP, dead mobs and no TP lead nobody; area 2's ids are + 6",
              "bf(scState,1);bf(scLeader,2000);2000:MessageText(7732)" + Sep +
              "bf(scState,1);bf(scLeader,2001);2001:MessageText(7743)" + Sep +
              "bf(scState,1);bf(scLeader,2002);2002:MessageText(7754)" + Sep +
              "bf(scState,1);bf(scLeader,2000);2000:MessageText(7732)" + Sep +
              "bf(scState,1);bf(scLeader,2007);2007:MessageText(7743)" + Sep + Sep,
              string.Join(Sep,
                  Idle(ros, 1, (Z, 1000, true), (Y, 1000, true), (X, 0, true)),
                  Idle(ros, 1, (Z, 999, true), (Y, 1000, true), (X, 1000, true)),
                  Idle(ros, 1, (Z, 0, true), (Y, 0, true), (X, 1000, true)),
                  Idle(ros, 1, (Z, 5000, true), (Y, 1000, true), (X, 1000, true)),
                  Idle(ros, 2, (Z, 0, true), (Y, 1000, true), (X, 0, true)),
                  Idle(ros, 1, (Z, 0, true), (Y, 0, true), (X, 0, true)),
                  Idle(ros, 1, (Z, 5000, false), (Y, 0, true), (X, 0, true))));
        Check("c3 Q3c: tango with a tracker's IDLE: Y before X leads, X alone leads, the id after X (2003) never does",
              "bf(scState,1);bf(scLeader,2001);2001:MessageText(7743)" + Sep +
              "bf(scState,1);bf(scLeader,2002);2002:MessageText(7754)" + Sep + Sep +
              "bf(scState,1);bf(scLeader,2007);2007:MessageText(7743)",
              string.Join(Sep,
                  Idle(twt, 1, (Y, 1000, true), (X, 1000, true)),
                  Idle(twt, 1, (Y, 0, true), (X, 1000, true)),
                  Idle(twt, 1, (Y, 0, true), (X, 0, true), (X + 1, 5000, true)),
                  Idle(twt, 2, (Y, 1000, true), (X, 0, true))));

        // READY: every mob alive at 1000 TP (in Tango, both alive), the leader alive with a target: the chain is set
        string Ready(BattlefieldScript s, int area, int leader, params (int Id, int Tp)[] mobs)
        {
            var a = new C3Q3cArena(s, area, 1, leader);
            foreach (var m in mobs) a.Add(m.Id, m.Tp);
            a.Tick();
            return a.State();
        }
        Check("c3 Q3c: requiem of sin's READY with all three alive: Z leads Skewer, Spiral Hell, Evisceration; Y " +
              "leads Guillotine, Shadowstitch, Wheeling Thrust; X leads Evisceration, Vorpal Scythe, Impulse Drive; " +
              "the leader's line + 2, the others' + 3, each mob's next id (0 for the last)",
              "s2 l2000 2000:118/7733/2001 2001:104/7745/2002 2002:25/7756/0" + Sep +
              "s2 l2001 2000:119/7734/0 2001:102/7744/2002 2002:18/7756/2000" + Sep +
              "s2 l2002 2000:120/7734/0 2001:101/7745/2000 2002:25/7755/2001" + Sep +
              "s2 l2007 2006:119/7734/0 2007:102/7744/2008 2008:18/7756/2006",
              string.Join(Sep,
                  Ready(ros, 1, Z, (Z, 1000), (Y, 1000), (X, 1000)),
                  Ready(ros, 1, Y, (Z, 1000), (Y, 1000), (X, 1000)),
                  Ready(ros, 1, X, (Z, 1000), (Y, 1000), (X, 1000)),
                  Ready(ros, 2, Y, (Z, 1000), (Y, 1000), (X, 1000))));
        string Pair(BattlefieldScript s, int leader, int other, int dead)
        {
            var a = new C3Q3cArena(s, 1, 1, leader);
            a.Add(leader, 1000);
            a.Add(other, 1000);
            a.Add(dead, 1000, false);
            a.Tick();
            return a.State();
        }
        Check("c3 Q3c: requiem of sin's READY with two alive, by the other alive mob: X leads with Y Evisceration, " +
              "Vorpal Scythe, with Z Dancing Edge, Penta Thrust; Y leads with X Spiral Hell, Shadowstitch, with Z " +
              "Spiral Hell, Impulse Drive; Z leads with X Wheeling Thrust, Shark Bite, with Y Skewer, Spiral Hell",
              "s2 l2002 2001:101/7745/0 2002:25/7755/2001" + Sep +
              "s2 l2002 2000:116/7734/0 2002:23/7755/2000" + Sep +
              "s2 l2001 2001:104/7744/2002 2002:18/7756/0" + Sep +
              "s2 l2001 2000:120/7734/0 2001:104/7744/2000" + Sep +
              "s2 l2000 2000:119/7733/2002 2002:24/7756/0" + Sep +
              "s2 l2000 2000:118/7733/2001 2001:104/7745/0",
              string.Join(Sep,
                  Pair(ros, X, Y, Z), Pair(ros, X, Z, Y), Pair(ros, Y, X, Z), Pair(ros, Y, Z, X),
                  Pair(ros, Z, X, Y), Pair(ros, Z, Y, X)));
        Check("c3 Q3c: tango with a tracker's READY: X leads Evisceration, Vorpal Scythe; Y leads Spiral Hell, " +
              "Shadowstitch; one Shikaree under 1000 TP waits",
              "s2 l2002 2001:101/7745/0 2002:25/7755/2001" + Sep +
              "s2 l2001 2001:104/7744/2002 2002:18/7756/0" + Sep + "s1 l2001" + Sep + "s1 l2002",
              string.Join(Sep,
                  Ready(twt, 1, X, (Y, 1000), (X, 1000)),
                  Ready(twt, 1, Y, (Y, 1000), (X, 1000)),
                  Ready(twt, 1, Y, (Y, 999), (X, 1000)),
                  Ready(twt, 1, X, (Y, 1000), (X, 999))));
        Check("c3 Q3c: requiem of sin's READY waits when a Shikaree is under 1000 TP, when no leader is set, when " +
              "the leader has no target or no battlefield",
              "s1 l2000" + Sep + "s1 l0" + Sep + "s1 l2000" + Sep + "s1 l2000",
              string.Join(Sep,
                  Ready(ros, 1, Z, (Z, 1000), (Y, 999), (X, 1000)),
                  Ready(ros, 1, 0, (Z, 1000), (Y, 1000), (X, 1000)),
                  NoTarget(ros), NoBattlefield(ros)));
        string NoTarget(BattlefieldScript s)
        {
            var a = new C3Q3cArena(s, 1, 1, Z);
            a.Add(Z, 1000, true, false);
            a.Add(Y, 1000);
            a.Add(X, 1000);
            return a.Tick() + a.State();
        }
        string NoBattlefield(BattlefieldScript s)
        {
            var a = new C3Q3cArena(s, 1, 1, Z);
            a.Add(Z, 1000).Battlefield = null;
            a.Add(Y, 1000);
            a.Add(X, 1000);
            return a.Tick() + a.State();
        }
        {
            var a = new C3Q3cArena(ros, 1, 1, Z);
            a.Add(Z, 1000);
            a.Add(Y, 1000);
            a.Add(X, 1000);
            string tick = a.Tick();
            Check("c3 Q3c: requiem of sin's READY calls: the battlefield to STARTING; each mob its magic off, the " +
                  "followers (not the leader) set on the leader's target",
                  "bf(scState,2);2000:SetMagicCastingEnabled(False);2001:SetMagicCastingEnabled(False);" +
                  "2001:UpdateEnmity(9000);2002:SetMagicCastingEnabled(False);2002:UpdateEnmity(9000)", tick);
        }
        {
            // the leader died while READY: the reset, then (the state is IDLE again) a new leader is looked for
            var a = new C3Q3cArena(ros, 1, 1, Z);
            a.Add(Z, 1000, false);
            a.Add(Y, 1000);
            a.Add(X, 0);
            Check("c3 Q3c: requiem of sin's READY with a dead leader resets (the battlefield IDLE, no leader, the " +
                  "alive mobs' magic on) and then picks a new leader in the same tick",
                  "bf(scState,0);bf(scLeader,0);bf(scState,1);bf(scLeader,2001);" +
                  "2001:SetMagicCastingEnabled(True);2001:MessageText(7743);2002:SetMagicCastingEnabled(True)",
                  a.Tick());
        }

        // STARTING: the leader (alive, not busy, with a target and a skill) uses it on its target
        string Start(BattlefieldScript s, int area, int leader, Action<C3Q3cArena> setup)
        {
            var a = new C3Q3cArena(s, area, 2, leader);
            a.Add(leader == 0 ? Z : leader, 0);
            a.Add(Y, 0);
            a.Add(X, 0);
            setup(a);
            return a.Tick();
        }
        Check("c3 Q3c: requiem of sin's STARTING: the leader uses its skill on its target (area 2 by its id); a busy " +
              "leader, one with no target or one with no skill does nothing",
              "2000:UseMobAbility(118,9000)" + Sep + "2007:UseMobAbility(119,9000)" + Sep + Sep + Sep,
              string.Join(Sep,
                  Start(ros, 1, Z, a => a[Z].LocalVars["scSkill"] = 118),
                  Start(ros, 2, Y, a => a[Y].LocalVars["scSkill"] = 119),
                  Start(ros, 1, Z, a => { a[Z].LocalVars["scSkill"] = 118; a.E.BusyMobs.Add(Z); }),
                  Start(ros, 1, Z, a => { a[Z].LocalVars["scSkill"] = 118; a[Z].Target = null; }),
                  Start(ros, 1, Z, a => { })));
        Check("c3 Q3c: requiem of sin's STARTING with a dead or unknown leader resets (IDLE, no leader, the alive " +
              "mobs' magic on): all three Shikarees of the area",
              "bf(scState,0);bf(scLeader,0);2001:SetMagicCastingEnabled(True);2002:SetMagicCastingEnabled(True)" + Sep +
              "bf(scState,0);bf(scLeader,0);2000:SetMagicCastingEnabled(True);2001:SetMagicCastingEnabled(True);" +
              "2002:SetMagicCastingEnabled(True)",
              string.Join(Sep,
                  Start(ros, 1, Z, a => a[Z].IsSpawned = false),
                  StartUnknown(ros)));
        string StartUnknown(BattlefieldScript s)
        {
            var a = new C3Q3cArena(s, 1, 2, 0);
            a.Bf.LocalVars["scLeader"] = 9999;
            a.Add(Z, 0);
            a.Add(Y, 0);
            a.Add(X, 0);
            return a.Tick();
        }
        {
            // Tango's reset loop runs 1 to 2 from Y: X and the id after X (Requiem's +3 mob) are reset, Y is not
            var a = new C3Q3cArena(twt, 1, 2, 0);
            a.Bf.LocalVars["scLeader"] = 9999;
            a.Add(Y, 0);
            a.Add(X, 0);
            a.Add(X + 1, 0);
            Check("c3 Q3c: tango with a tracker's reset (kept Lua loop 1 to 2 from Y): X and the id after X are " +
                  "reset, Y is not",
                  "bf(scState,0);bf(scLeader,0);2002:SetMagicCastingEnabled(True);2003:SetMagicCastingEnabled(True)",
                  a.Tick());
        }

        // EXECUTING: the first alive, not busy mob with a skill and a pending time that has come uses it, on the
        // leader's target (the mob's own when the leader has none); all skills used: reset
        string Exec(BattlefieldScript s, uint now, Action<C3Q3cArena> setup)
        {
            var a = new C3Q3cArena(s, 1, 3, Z);
            a.Add(Z, 0);
            a.Add(Y, 0);
            a.Add(X, 0);
            a.E.SystemTime = now;
            setup(a);
            return a.Tick();
        }
        void Pending(C3Q3cArena a, int id, uint skill, uint time)
        {
            a[id].LocalVars["scSkill"] = skill;
            a[id].LocalVars["scPendingTime"] = time;
        }
        Check("c3 Q3c: requiem of sin's EXECUTING: a pending mob whose time has come uses its skill on the leader's " +
              "target after being set on it; the first of Z, Y, X goes first",
              "2001:UpdateEnmity(9000);2001:UseMobAbility(104,9000)" + Sep +
              "2000:UpdateEnmity(9000);2000:UseMobAbility(118,9000)" + Sep +
              "2001:UpdateEnmity(9000);2001:UseMobAbility(104,9000)",
              string.Join(Sep,
                  Exec(ros, 2000, a => Pending(a, Y, 104, 1000)),
                  Exec(ros, 2000, a => { Pending(a, Y, 104, 1000); Pending(a, Z, 118, 1000); }),
                  Exec(ros, 1000, a => Pending(a, Y, 104, 1000))));
        Check("c3 Q3c: requiem of sin's EXECUTING waits for a pending time not yet come, one of 0 and a busy mob (no " +
              "skill is used and the chain is not complete)",
              Sep + Sep,
              string.Join(Sep,
                  Exec(ros, 999, a => Pending(a, Y, 104, 1000)),
                  Exec(ros, 2000, a => Pending(a, Y, 104, 0)),
                  Exec(ros, 2000, a => { Pending(a, Y, 104, 1000); a.E.BusyMobs.Add(Y); })));
        Check("c3 Q3c: requiem of sin's EXECUTING when the leader has no target: the pending mob uses its skill on " +
              "its own target without being set on one; with none at all it does nothing",
              "2001:UseMobAbility(104,9000)" + Sep,
              string.Join(Sep,
                  Exec(ros, 2000, a => { Pending(a, Y, 104, 1000); a[Z].Target = null; }),
                  Exec(ros, 2000, a => { Pending(a, Y, 104, 1000); a[Z].Target = null; a[Y].Target = null; })));
        Check("c3 Q3c: requiem of sin's EXECUTING is complete when no alive mob has a skill (dead ones are not " +
              "looked at): the reset",
              "bf(scState,0);bf(scLeader,0);2000:SetMagicCastingEnabled(True);2001:SetMagicCastingEnabled(True);" +
              "2002:SetMagicCastingEnabled(True)" + Sep +
              "bf(scState,0);bf(scLeader,0);2000:SetMagicCastingEnabled(True);2002:SetMagicCastingEnabled(True)",
              string.Join(Sep,
                  Exec(ros, 2000, a => { }),
                  Exec(ros, 2000, a => { a[Y].IsSpawned = false; a[Y].LocalVars["scSkill"] = 104; })));
        Check("c3 Q3c: tango with a tracker's EXECUTING: the pending X uses its skill on the leader's target",
              "2002:UpdateEnmity(9000);2002:UseMobAbility(18,9000)",
              ExecTwt(twt));
        string ExecTwt(BattlefieldScript s)
        {
            var a = new C3Q3cArena(s, 1, 3, Y);
            a.Add(Y, 0);
            a.Add(X, 0);
            a.E.SystemTime = 2000;
            Pending(a, X, 18, 1000);
            return a.Tick();
        }

        // SOLO: one Shikaree left (a dead one is not counted): its abilities on, the reset, state SOLO; again: nothing
        string Solo(BattlefieldScript s, int state, int alive, params int[] extra)
        {
            var a = new C3Q3cArena(s, 1, state, 0);
            foreach (int id in new[] { Z, Y, X })
            {
                if (s == twt && id == Z) continue;
                a.Add(id, 1000, id == alive);
            }

            foreach (int id in extra) a.Add(id, 1000);
            return a.Tick();
        }
        Check("c3 Q3c: requiem of sin's last Shikaree alone: its abilities on, the reset of the alive ones, state " +
              "SOLO, whatever the state was; in SOLO it does nothing",
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2001:SetMobAbilityEnabled(True);" +
              "2001:SetMagicCastingEnabled(True)" + Sep +
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2002:SetMobAbilityEnabled(True);" +
              "2002:SetMagicCastingEnabled(True)" + Sep + Sep,
              string.Join(Sep,
                  Solo(ros, 0, Y),
                  Solo(ros, 2, X),
                  Solo(ros, 4, Y),
                  Solo(ros, 0, 0)));
        Check("c3 Q3c: tango with a tracker's last Shikaree alone: X resets itself and the id after X; Y alone is " +
              "not reset by it (the kept loop)",
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2002:SetMobAbilityEnabled(True);" +
              "2002:SetMagicCastingEnabled(True);2003:SetMagicCastingEnabled(True)" + Sep +
              "bf(scState,0);bf(scLeader,0);bf(scState,4);2001:SetMobAbilityEnabled(True)",
              string.Join(Sep,
                  Solo(twt, 0, X, X + 1),
                  Solo(twt, 0, Y)));

        // pins found by the mutant run (survivors of the first replay and of the checks)
        // K35 / K36: a reset clears each alive mob's skill and pending time (and its message and next id)
        string LocalsAfterReset(BattlefieldScript s)
        {
            var a = new C3Q3cArena(s, 1, 2, 0);
            a.Bf.LocalVars["scLeader"] = 9999;
            foreach (int id in new[] { Z, Y, X })
            {
                a.Add(id, 0);
                a[id].LocalVars["scSkill"] = 118;
                a[id].LocalVars["scPendingTime"] = 5;
                a[id].LocalVars["scMessage"] = 7;
                a[id].LocalVars["scNextID"] = 3;
            }

            a.Tick();
            return string.Join(",", new[] { Z, Y, X }.Select(id =>
                $"{a[id].LocalVar("scSkill")}/{a[id].LocalVar("scPendingTime")}/{a[id].LocalVar("scMessage")}/" +
                $"{a[id].LocalVar("scNextID")}"));
        }
        Check("c3 Q3c: requiem of sin's reset clears each alive mob's skill, pending time, message and next id",
              "0/0/0/0,0/0/0/0,0/0/0/0", LocalsAfterReset(ros));

        // K13: the two-mob chain is found by the other alive mob's base id, in the second area too
        {
            var a = new C3Q3cArena(ros, 2, 1, Y);
            a.Add(Y, 1000);
            a.Add(X, 1000);
            a.Add(Z, 1000, false);
            a.Tick();
            Check("c3 Q3c: requiem of sin's READY with two alive in area 2 finds the chain by the other mob's base id",
                  "s2 l2007 2007:104/7744/2008 2008:18/7756/0", a.State());
        }

        // K61: a mob with a pending time but no skill is not a pending mob (nothing is used, the chain completes)
        Check("c3 Q3c: requiem of sin's EXECUTING does not use a skill of 0 for a mob with a pending time: the reset",
              "bf(scState,0);bf(scLeader,0);2000:SetMagicCastingEnabled(True);2001:SetMagicCastingEnabled(True);" +
              "2002:SetMagicCastingEnabled(True)",
              Exec(ros, 2000, a => Pending(a, Y, 0, 1000)));

        // K67: the pending mob uses its skill on the leader's target, not on its own
        {
            var a = new C3Q3cArena(ros, 1, 3, Z);
            a.Add(Z, 0);
            a.Add(Y, 0);
            a.Add(X, 0);
            a.E.SystemTime = 2000;
            a[Y].Target = new BattlefieldMobFake { Id = 9001, IsMob = false, HP = 100 };
            Pending(a, Y, 104, 1000);
            Check("c3 Q3c: requiem of sin's EXECUTING uses the pending skill on the leader's target, not the mob's own",
                  "2001:UpdateEnmity(9000);2001:UseMobAbility(104,9000)", a.Tick());
        }

        // KX04: a leader with no battlefield ends the tick quietly (the NoBattlefield check above adds the tick's own
        // result, which is "THREW ..." if the guard is gone)
        // KX22: a skill of 1 is a skill
        Check("c3 Q3c: requiem of sin's STARTING: a leader whose skill is 1 uses it",
              "2000:UseMobAbility(1,9000)", Start(ros, 1, Z, a => a[Z].LocalVars["scSkill"] = 1));
        // KX30: the dead-leader reset is for READY only; a dead leader in STARTING is reset by StartSkillChain, and no
        // new leader is looked for in that tick even when a Shikaree has 1000 TP
        Check("c3 Q3c: requiem of sin's STARTING with a dead leader resets once and picks no new leader in the tick",
              "bf(scState,0);bf(scLeader,0);2001:SetMagicCastingEnabled(True);2002:SetMagicCastingEnabled(True)",
              Start(ros, 1, Z, a => { a[Z].IsSpawned = false; a[Y].TP = 1000; }));
        // KX13 / RX03: Requiem's reset covers Z, Y, X and not the id after X (Z + 3)
        Check("c3 Q3c: requiem of sin's reset leaves the id after X (Z + 3) alone",
              "bf(scState,0);bf(scLeader,0);2001:SetMagicCastingEnabled(True);2002:SetMagicCastingEnabled(True)",
              Start(ros, 1, Z, a => { a[Z].IsSpawned = false; a.Add(X + 1, 0); }));
        // KX13 / TX04: Tango's reset covers the id after X and not the one after that (X + 2)
        {
            var a = new C3Q3cArena(twt, 1, 2, 0);
            a.Bf.LocalVars["scLeader"] = 9999;
            a.Add(Y, 0);
            a.Add(X, 0);
            a.Add(X + 1, 0);
            a.Add(X + 2, 0);
            Check("c3 Q3c: tango with a tracker's reset stops at the id after X: X + 2 is left alone",
                  "bf(scState,0);bf(scLeader,0);2002:SetMagicCastingEnabled(True);2003:SetMagicCastingEnabled(True)",
                  a.Tick());
        }
        // RX02: Requiem counts three slots; an alive mob at Z + 3 is not a Shikaree of the fight (two alive: the
        // two-mob chain of Z with Y)
        Check("c3 Q3c: requiem of sin's READY counts the mobs of Z, Y, X only: Z and Y alive with a mob at Z + 3 " +
              "still use the two-mob chain",
              "s2 l2000 2000:118/7733/2001 2001:104/7745/0",
              Ready(ros, 1, Z, (Z, 1000), (Y, 1000), (X + 1, 1000)));
    }
}
