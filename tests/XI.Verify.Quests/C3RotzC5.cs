// XI.Verify.Quests -- quest rework C3 C5: Zilart 1 to 9 (The New Frontier to Ro'Maeve) and their three fights (Temple
// of Uggalepih, Through the Quicksand Caves, Return to Delkfutt's Tower) on C#. The data of each against its Lua file's
// (as it stood at the recording), the two facet members C5 added (Npc.MessageText, Player.CompleteMissionRaw), and the
// arms a trace cannot see: the zone-in arms that duplicate a Zone.lua event, the mission:complete false arm, the
// bounds.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3RotzC5()
    {
        Console.WriteLine("\n== c3 C5: Zilart 1 to 9 and their three fights ==");
        RunC3C5Keys();
        RunC3C5Battlefields();
        RunC3C5Facets();
        RunC3C5Pins();
        RunC3C5Fixed();
    }

    // Every fixed reply of the nine missions (an NPC's ['Npc'] = mission:event(n) and the like): its state, zone, NPC,
    // reply kind and modifiers. Event and ProgressEvent send the same 0x032, and the mission's reply outranks
    // DefaultActions (priority 10) whatever its kind or ReplaceDefault (20, 100, 1000), so the traces tell these apart
    // only against another container's reply on the same NPC or on a second trigger's fallback; these checks pin them.
    static void RunC3C5Fixed()
    {
        string Fixed(MissionScript m) => string.Join(" ", m.Compile().States.SelectMany((s, i) =>
            s.Handlers.Where(h => h.Fixed != null).Select(h =>
                $"{i}/{h.Zone}/{h.Entity}={QuestContextFake.Describe(h.Fixed)}")));
        (string Name, int Fragment)[] pedestals =
        {
            ("Darkness", QC.KeyItem.DarkFragment), ("Earth", QC.KeyItem.EarthFragment),
            ("Fire", QC.KeyItem.FireFragment), ("Ice", QC.KeyItem.IceFragment), ("Light", QC.KeyItem.LightFragment),
            ("Lightning", QC.KeyItem.LightningFragment), ("Water", QC.KeyItem.WaterFragment),
            ("Wind", QC.KeyItem.WindFragment),
        };
        Check("c3 C5: The New Frontier's fixed replies: section 2's _700 5 and Comitiolus 6, each replacing the " +
              "default",
              "1/252/_700=Event(5):replaceDefault() 1/252/Comitiolus=Event(6):replaceDefault()",
              Fixed(new QC.Missions.Zilart.TheNewFrontier()));
        Check("c3 C5: Kazham's Chieftainess, The Temple of Uggalepih, Through the Quicksand Caves, Return to " +
              "Delkfutt's Tower and Ro'Maeve's fixed replies",
              "0/252/Gilgamesh=Event(7) 0/250/Jakoh_Wahcondalo=ProgressEvent(114)|" +
              "0/252/Gilgamesh=Event(8) 0/250/Jakoh_Wahcondalo=Event(115)|0/252/Gilgamesh=Event(12)|" +
              "0/252/Gilgamesh=Event(13)|0/252/_700=ProgressEvent(3)",
              string.Join("|", new MissionScript[]
              {
                  new QC.Missions.Zilart.KazhamsChieftainess(), new QC.Missions.Zilart.TheTempleOfUggalepih(),
                  new QC.Missions.Zilart.ThroughTheQuicksandCaves(), new QC.Missions.Zilart.ReturnToDelkfuttsTower(),
                  new QC.Missions.Zilart.Romaeve(),
              }.Select(Fixed)));
        Check("c3 C5: Headstone Pilgrimage's fixed replies: Gilgamesh 9; section 2's seven headstones, each " +
              "replacing the default",
              "0/252/Gilgamesh=Event(9) " + string.Join(" ", new[]
              {
                  "127:ALREADY_HAVE_ALL_FRAGS", "113:ZILART_MONUMENT", "203:ALREADY_HAVE_ALL_FRAGS",
                  "102:ALREADY_HAVE_ALL_FRAGS", "121:ZILART_MONUMENT", "125:ALREADY_HAVE_ALL_FRAGS",
                  "123:ZILART_MONUMENT",
              }.Select(t => $"1/{t.Split(':')[0]}/Cermet_Headstone=MessageSpecial({t}):replaceDefault()")),
              Fixed(new QC.Missions.Zilart.HeadstonePilgrimage()));
        Check("c3 C5: The Chamber of Oracles' fixed replies: the eight pedestals in sections 1 and 3",
              string.Join(" ", pedestals.Select(p =>
                  $"0/168/Pedestal_of_{p.Name}=MessageSpecial(168:PLACED_INTO_THE_PEDESTAL)"))
              + " " + string.Join(" ", pedestals.Select(p =>
                  $"2/168/Pedestal_of_{p.Name}=MessageSpecial(168:HAS_LOST_ITS_POWER,{p.Fragment})")),
              Fixed(new QC.Missions.Zilart.TheChamberOfOracles()));
    }

    static void RunC3C5Keys()
    {
        MissionScript[] all =
        {
            new QC.Missions.Zilart.TheNewFrontier(), new QC.Missions.Zilart.WelcomeTnorg(),
            new QC.Missions.Zilart.KazhamsChieftainess(), new QC.Missions.Zilart.TheTempleOfUggalepih(),
            new QC.Missions.Zilart.HeadstonePilgrimage(), new QC.Missions.Zilart.ThroughTheQuicksandCaves(),
            new QC.Missions.Zilart.TheChamberOfOracles(), new QC.Missions.Zilart.ReturnToDelkfuttsTower(),
            new QC.Missions.Zilart.Romaeve(),
        };
        Check("c3 C5: the nine missions' keys and Lua files",
              "mission.zilart.the_new_frontier scripts/missions/rotz/01_The_New_Frontier|" +
              "mission.zilart.welcome_tnorg scripts/missions/rotz/02_Welcome_to_Norg|" +
              "mission.zilart.kazhams_chieftainess scripts/missions/rotz/03_Kazhams_Chieftainess|" +
              "mission.zilart.the_temple_of_uggalepih scripts/missions/rotz/04_The_Temple_of_Uggalepih|" +
              "mission.zilart.headstone_pilgrimage scripts/missions/rotz/05_Headstone_Pilgrimage|" +
              "mission.zilart.through_the_quicksand_caves scripts/missions/rotz/06_Through_the_Quicksand_Caves|" +
              "mission.zilart.the_chamber_of_oracles scripts/missions/rotz/07_The_Chamber_of_Oracles|" +
              "mission.zilart.return_to_delkfutts_tower scripts/missions/rotz/08_Return_to_Delkfutts_Tower|" +
              "mission.zilart.romaeve scripts/missions/rotz/09_RoMaeve",
              string.Join("|", all.Select(m => $"{m.Key} {m.LegacyPath}")));
        Check("c3 C5: the missions validate clean", "",
              string.Join(" | ", all.SelectMany(m => QuestScriptValidator.Validate(m.Compile()))));
        Check("c3 C5: the rewards (key item, title, next mission), as the Lua tables have them",
              $"{QC.KeyItem.MapOfNorg},,4|,,6|{QC.KeyItem.SacrificialChamberKey},,8|" +
              $"{QC.KeyItem.DarkFragment},{QC.Title.BearerOfTheWisewomansHope},10|" +
              $",{QC.Title.BearerOfTheEightPrayers},12|,,14|{QC.KeyItem.PrismaticFragment},{QC.Title.Lightweaver},16|" +
              ",,18|,,20",
              string.Join("|", all.Select(m => m.Compile().Reward).Select(r =>
                  $"{r.KeyItem},{r.Title},{(r.NextLog == MissionLog.Zilart ? r.NextMission : -1)}")));
    }

    static void RunC3C5Battlefields()
    {
        var temple = new QC.Battlefields.SacrificialChamber.TempleOfUggalepih();
        var sands = new QC.Battlefields.ChamberOfOracles.ThroughTheQuicksandCaves();
        var tower = new QC.Battlefields.StellarFulcrum.ReturnToDelkfuttsTower();
        BattlefieldScript[] all = { temple, sands, tower };
        Check("c3 C5: the three fights validate clean, their keys and Lua files",
              "|battlefield.temple_of_uggalepih scripts/battlefields/Sacrificial_Chamber/temple_of_uggalepih|" +
              "|battlefield.through_the_quicksand_caves " +
              "scripts/battlefields/Chamber_of_Oracles/through_the_quicksand_caves|" +
              "|battlefield.return_to_delkfutts_tower scripts/battlefields/Stellar_Fulcrum/return_to_delkfutts_tower",
              string.Join("|", all.Select(s =>
                  $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key} {s.LegacyPath}")));
        Check("c3 C5: the Lua files' fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, time, " +
              "index, entry and exit NPCs, mission, status area and value, title, groups)",
              "163 128 False True True 6 MAX_LEVEL 1800 0 _4j0 _4j2,_4j3,_4j4 3/8 3 0 0 3|" +
              "168 192 False True True 6 MAX_LEVEL 1800 0 SC_Entrance Shimmering_Circle 3/12 3 0 0 1|" +
              "179 256 False True True 6 MAX_LEVEL 1800 0 _4z0 _4z1,_4z2,_4z3 3/16 3 1 " +
              $"{QC.Title.DestroyerOfAntiquity} 1",
              string.Join("|", all.Select(s => (BattlefieldMissionData)s.Data).Select(d =>
                  $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
                  $"{d.LevelCapSetting} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
                  $"{string.Join(",", d.ExitNpcs)} {(int)d.MissionArea}/{d.Mission} {(int?)d.MissionStatusArea} " +
                  $"{d.RequiredMissionStatus} {d.Title} {d.Groups.Count}")));
        string Groups(BattlefieldScript s) => string.Join("|", s.Data.Groups.Select(g =>
            string.Join(";", g.AreaMobIds.Select(a => a[0].Key + ":" + string.Join(",", a.Select(m => m.Offset)))) +
            $" superlink={g.Superlink} spawned={g.Spawned} allDeath={g.AllDeath != null}"));
        Check("c3 C5: Temple of Uggalepih's groups: the superlinked nine win; the Elementals spawn (spawned " +
              "unset); the Avatars wait (false)",
              "GRAVITON:0,1,2;GRAVITON:5,6,7;GRAVITON:10,11,12 superlink=True spawned= allDeath=True|" +
              "GRAVITON:3;GRAVITON:8;GRAVITON:13 superlink=False spawned= allDeath=False|" +
              "GRAVITON:4;GRAVITON:9;GRAVITON:14 superlink=False spawned=False allDeath=False",
              Groups(temple));
        Check("c3 C5: Through the Quicksand Caves' group: the nine Centurio V-III kin, three to an area, win",
              "CENTURIO_V_III:0,1,2;CENTURIO_V_III:3,4,5;CENTURIO_V_III:6,7,8 superlink=False spawned= allDeath=True",
              Groups(sands));
        Check("c3 C5: Return to Delkfutt's Tower's group: one Kamlanaut to an area wins",
              "KAMLANAUT:0;KAMLANAUT:1;KAMLANAUT:2 superlink=False spawned= allDeath=True", Groups(tower));
        Check("c3 C5: the three override no hook (the framework's BattlefieldMission)", "||",
              string.Join("|", all.Select(s => string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides)))));
    }

    static void RunC3C5Facets()
    {
        var f = new Fake();
        f.Npc = new QuestNpcFake(17, f.Calls);
        f.Npc.MessageText(QC.Text.YuhtungaJungle.THE_OPO_OPOS_ATTACK);
        Check("c3 C5: Npc.MessageText records the text",
              $"Npc.MessageText({QC.Text.YuhtungaJungle.THE_OPO_OPOS_ATTACK})",
              string.Join(",", f.Calls));

        int rov = QC.MissionLog.Rov, cursed = QC.Mission.Rov.TheCursedTemple;
        string Raw(int log, int current, int mission)
        {
            var g = new Fake();
            g.CurrentMissions[log] = current;
            g.CompleteMissionRaw(log, mission);
            return $"{string.Join(",", g.Calls)} {g.CurrentMission(log)} {g.HasCompletedMission(log, mission)}";
        }
        Check("c3 C5: Player.CompleteMissionRaw: RoV's current (id 102): current 0, no completed bit (id 64 or more)",
              $"Player.CompleteMissionRaw({rov},{cursed}) 0 False", Raw(rov, cursed, cursed));
        Check("c3 C5: Player.CompleteMissionRaw: a nation log's current below 64: current 65535, completed; Zilart: 0",
              $"Player.CompleteMissionRaw(1,3) 65535 True|Player.CompleteMissionRaw(3,8) 0 True",
              $"{Raw(1, 3, 3)}|{Raw(3, 8, 8)}");
        Check("c3 C5: Player.CompleteMissionRaw: CoP sets no completed bit; another current is left as it was",
              $"Player.CompleteMissionRaw(6,110) 0 False|Player.CompleteMissionRaw({rov},{cursed}) 5 False",
              $"{Raw(6, 110, 110)}|{Raw(rov, 5, cursed)}");
    }

    // a C5 mission's fake: the mission current (or another id), its status, the option
    static Fake C3C5Fake(int mission, int current, int status = 0, int option = 0)
    {
        var f = new Fake { ThisMission = (MissionLog.Zilart, mission), Option = option };
        f.CurrentMissions[QC.MissionLog.Zilart] = current;
        f.MissionStatuses[QC.MissionLog.Zilart] = status;
        return f;
    }

    static void RunC3C5Pins()
    {
        var zin = QuestHandlerKind.OnZoneIn;
        var fin = QuestHandlerKind.OnEventFinish;
        var trig = QuestHandlerKind.OnTrigger;

        // Return to Delkfutt's Tower: the 14 and 7 arms play what Middle's and Upper's own Zone.lua zone-ins play
        int rtdt = QC.Mission.Zilart.ReturnToDelkfuttsTower;
        var tower = new QC.Missions.Zilart.ReturnToDelkfuttsTower().Compile();
        string Lower(int prev, int option)
        {
            var f = C3C5Fake(rtdt, rtdt);
            f.PrevZone = prev;
            f.MissionVars[$"{QC.MissionLog.Zilart}/{rtdt}/Option"] = option;
            return C2MReply(C2MHandler(tower, 0, zin, QC.Zone.LowerDelkfuttsTower), f);
        }
        Check("c3 C5 tower: Lower Delkfutt's zone-in: from Middle 14 (bit 1 clear or set); else 15 while bit 1 is " +
              "clear, nothing once set (bit 0 alone does not count)",
              "Event(14)|Event(14)|Event(15)|null|Event(15)",
              $"{Lower(QC.Zone.MiddleDelkfuttsTower, 0)}|{Lower(QC.Zone.MiddleDelkfuttsTower, 2)}|" +
              $"{Lower(QC.Zone.QufimIsland, 0)}|{Lower(QC.Zone.QufimIsland, 2)}|{Lower(QC.Zone.QufimIsland, 1)}");
        string Fulcrum(int status, int prev)
        {
            var f = C3C5Fake(rtdt, rtdt, status);
            f.PrevZone = prev;
            return C2MReply(C2MHandler(tower, 0, zin, QC.Zone.StellarFulcrum), f);
        }
        int upper = QC.Zone.UpperDelkfuttsTower, other = QC.Zone.LowerDelkfuttsTower;
        Check("c3 C5 tower: the Stellar Fulcrum zone-in: status 0 plays 0 and 2 plays 17 from anywhere; 1 and 3 " +
              "play 7 from Upper Delkfutt's only",
              "Event(0)|Event(0)|Event(17)|Event(17)|Event(7)|Event(7)|null|null",
              $"{Fulcrum(0, upper)}|{Fulcrum(0, other)}|{Fulcrum(2, upper)}|{Fulcrum(2, other)}|" +
              $"{Fulcrum(1, upper)}|{Fulcrum(3, upper)}|{Fulcrum(1, other)}|{Fulcrum(3, other)}");

        // The Temple of Uggalepih: 8's finish tests mission:complete's result
        int temple = QC.Mission.Zilart.TheTempleOfUggalepih;
        var templeDef = new QC.Missions.Zilart.TheTempleOfUggalepih().Compile();
        string Eight(bool result, int rovCurrent)
        {
            var f = C3C5Fake(temple, temple, 2);
            f.CompleteResult = result;
            f.CurrentMissions[QC.MissionLog.Rov] = rovCurrent;
            return C2MRun(C2MHandler(templeDef, 0, fin, QC.Zone.SacrificialChamber, id: 8), f);
        }
        int rov = QC.MissionLog.Rov, cursed = QC.Mission.Rov.TheCursedTemple;
        Check("c3 C5 temple: 8's finish: a failed complete does nothing more; a done one takes the key, and moves " +
              "the RoV log on from The Cursed Temple only",
              $"Mission.Complete()|Mission.Complete(),Player.DelKeyItem({QC.KeyItem.SacrificialChamberKey})|" +
              $"Mission.Complete(),Player.DelKeyItem({QC.KeyItem.SacrificialChamberKey})," +
              $"Player.CompleteMissionRaw({rov},{cursed})," +
              $"Player.AddMission({rov},{QC.Mission.Rov.WisdomOfOurForefathers})",
              $"{Eight(false, cursed)}|{Eight(true, cursed - 1)}|{Eight(true, cursed)}");

        // Headstone Pilgrimage: the cooldown bound and the distance bound
        int hp = QC.Mission.Zilart.HeadstonePilgrimage;
        var hpDef = new QC.Missions.Zilart.HeadstonePilgrimage().Compile();
        string Cool(int now, int cooldown)
        {
            var f = C3C5Fake(hp, hp);
            f.Now = now;
            f.Npc = new QuestNpcFake(1, f.Calls);
            f.Npc.LocalVars["cooldown"] = cooldown;
            return C2MReply(C2MHandler(hpDef, 0, trig, QC.Zone.CapeTeriggan, QC.Npc.CapeTeriggan.CermetHeadstone), f);
        }
        int wind = QC.KeyItem.WindFragment;
        Check("c3 C5 headstone: Cape Teriggan's cooldown: past or equal plays 200, a second short 201",
              $"ProgressEvent(200,{wind})|ProgressEvent(200,{wind})|ProgressEvent(201,{wind})",
              $"{Cool(1001, 1000)}|{Cool(1000, 1000)}|{Cool(999, 1000)}");
        // the two-NM headstones: each NM alone up decides SOMETHING_BETTER (the traces kill one and ask at once, while
        // the dying NM may still count as spawned)
        string Pair(ushort zone, string npc, params string[] up)
        {
            var f = C3C5Fake(hp, hp);
            f.Now = 1000;
            f.Npc = new QuestNpcFake(1, f.Calls);
            foreach (string mob in up) f.SpawnedMobs.Add(mob + "+0");
            return C2MReply(C2MHandler(hpDef, 0, trig, zone, npc), f);
        }
        ushort bd = QC.Zone.BehemothsDominion, yj = QC.Zone.YuhtungaJungle;
        string bdNpc = QC.Npc.BehemothsDominion.CermetHeadstone, yjNpc = QC.Npc.YuhtungaJungle.CermetHeadstone;
        string bdBetter =
            QuestContextFake.Describe(QuestReply.MessageSpecial(QC.Text.BehemothsDominion.SOMETHING_BETTER));
        string yjBetter = QuestContextFake.Describe(QuestReply.MessageSpecial(QC.Text.YuhtungaJungle.SOMETHING_BETTER));
        Check("c3 C5 headstone: the NM pairs: none up plays 200; the Ancient or the Legendary Weapon alone, or both, " +
              "say SOMETHING_BETTER; likewise Tipha or Carthi alone, or both",
              $"ProgressEvent(200,{QC.KeyItem.LightningFragment})|{bdBetter}|{bdBetter}|{bdBetter}|" +
              $"ProgressEvent(200,{QC.KeyItem.FireFragment})|{yjBetter}|{yjBetter}|{yjBetter}",
              $"{Pair(bd, bdNpc)}|{Pair(bd, bdNpc, "ANCIENT_WEAPON")}|{Pair(bd, bdNpc, "LEGENDARY_WEAPON")}|" +
              $"{Pair(bd, bdNpc, "ANCIENT_WEAPON", "LEGENDARY_WEAPON")}|{Pair(yj, yjNpc)}|{Pair(yj, yjNpc, "TIPHA")}|" +
              $"{Pair(yj, yjNpc, "CARTHI")}|{Pair(yj, yjNpc, "TIPHA", "CARTHI")}");
        string Near(double dx, double dy)
        {
            var f = C3C5Fake(hp, hp, 0, 1);
            f.Npc = new QuestNpcFake(1, f.Calls) { XPos = 10, YPos = 50, ZPos = 20 };
            f.XPos = 10 + dx;
            f.YPos = 5;
            f.ZPos = 20;
            f.KeyItems[QC.KeyItem.FireFragment] = true;
            return C2MRun(C2MHandler(hpDef, 0, fin, QC.Zone.CloisterOfFrost, id: 200), f);
        }
        int ice = QC.KeyItem.IceFragment;
        Check("c3 C5 headstone: the Cloister of Frost's 200: 1.9 yalms off (the char's own height) gives the " +
              "fragment; 1.93 says MUST_MOVE_CLOSER (0, the fragment)",
              $"Player.AddKeyItem({ice}),Text.Special({QC.Text.CloisterOfFrost.KEYITEM_OBTAINED},{ice})|" +
              $"Text.Special({QC.Text.CloisterOfFrost.MUST_MOVE_CLOSER},0,{ice})",
              $"{Near(1.9, 0)}|{Near(1.93, 0)}");
    }
}
