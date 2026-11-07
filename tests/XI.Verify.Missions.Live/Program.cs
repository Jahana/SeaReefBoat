using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Missions.Live -- live gate for expansion missions driven end to end as a client drives them, through the
// mission and quest interaction containers (xi.mission / xi.quest, scripts/missions, scripts/quests/abyssea): the 0x00A
// login and its zone-in cutscenes (onZoneIn -> the missions' onZoneIn sections), 0x05B event ends and updates (0x05C
// replies), 0x05C EVENTENDXZY (a battlefield's arena asked for), 0x015 moves (into the zones' trigger areas), 0x01A
// Talk and Attack, 0x036 trades and 0x05E zone lines, and every step's effect: the mission log (chars.missions and the
// 0x056 mission pages), the mission and quest vars (char_vars), key items, items, gil, imperial standing, titles and
// the zone changes (setPos, xi.teleport.to). The real scripts on the real MariaDB (xidb), chars 995911-995915. The walks
// between places the missions do not script (and the Promyvion floors) are GM !pos warps, as the missions' headers give.
//
// Chains of Promathia (char 995913, from Qufim Island): 1-1 The Rites of Life (the Lower Delkfutt's Tower zone-in from
// Qufim, Tales' Beginning and its hidden cutscenes, enableEntities, the Tales' Beginning MISSION page; the Upper Jeuno
// zone-in; Monberaux and the Mysterious Amulet), 1-2 Below the Arks (Pherimociel, the Konschtat Shattered Telepoint, the
// Hall of Transference, the Large Apparatus to Promyvion - Dem, Ancient Flames Beckon won in the Spire of Dem: the Light
// of Dem, xi.teleport.to), 1-3 The Mothercrystals (La Theine's telepoint to Promyvion - Holla and its spire; Tahrongi's
// to the Hall's cutscene 155, Promyvion - Mea and its spire: the title, the teleport to Lufaise), 2-1 An Invitation
// West (the Lufaise and Tavnazian Safehold zone-ins), 2-2 The Lost City (Despachiaire, the Sewer Entrance), 2-3 Distant
// Beliefs (the Minotaur killed in Phomiuna Aqueducts: onMobDeath; the Wooden Ladder, the Ornate Gate's cutscene,
// Justinius), 2-4 An Eternal Melody (the Walnut Door, the Dilapidated Gate, the Safehold's trigger area), 2-5 Ancient
// Vows (the gate, the Riverne - Site #A01 zone-in, Ancient Vows won in Monarch Linn: the title, setPos).
//
// Treasures of Aht Urhgan (char 995914, in Aht Urhgan Whitegate): 1 Land of Sacred Serpents (trigger area 3, text table
// 0) through 12 Royal Puppeteer: the Supplies Package to Meyaada, Naja Salaheem (the badge, the title, the mercenary
// rank), the mustZone after the Mog Locker scam, Cacaroon's 1000 gil trade and the temple / teahouse trigger areas with
// the topic updates, Guilerme and the Chateau's zone-in, Ru'Lude Gardens' palace trigger area (Imperial Bronze Pieces),
// the Westerly Winds note and silver piece, the Alzadaal Undersea Ruins' trigger area, Imperial Schemes' Vana'diel day
// timer, and Pyopyoroon's Jody's Acid trade in Nashmau.
//
// Abyssea (char 995915, in Port Jeuno): A Journey Begins (flagged by xi.player.onGameIn, the Port Jeuno zone-in, the
// Traverser Stone and epoch), The Truth Beckons (the Konschtat Cavernous Maw into Abyssea - Konschtat, the hidden
// Visitant time), three zone quests (each maw's offer, its warp, the NM claimed and killed for its title, the exit
// maw, the zone-in cutscene with the zone's key item) and Dawn of Death (First Contact flagged).
//
// Chains of Promathia on (char 995911, from Port Bastok): 3-1 The Call of the Wyrmking through 7-5 The Warrior's Path:
// Cid and the trigger areas, The Road Forks' two paths (the Overgrown Ivy, Lioumere, the Mimeo jewel's timer), the
// battlefields Darkness Named, The Savage, Desires of Emptiness, A Century of Hardship, Head Wind, Flames for the Dead,
// One to be Feared (its three phases by the Airship Door) and The Warrior's Path, the Sacrarium, Pso'Xja's gates and
// Beaucedine's Iron Grates, Promyvion - Vahzl's fluxes, Three Paths' three status nibbles (the Disaster Idol), the
// Warders, the Cryptonberries, Boggelmann and Dalham, Cid's Vana'diel day wait, the zone-in to Al'Taieu.
//
// Treasures of Aht Urhgan on (char 995912, in Aht Urhgan Whitegate): 13 Lost Kingdom through 48 Eternal Mercenary: the
// Ashu Talif and Periqia instances, the dress-up updates, the Navukgo, Jade Sepulcher and Talacca Cove battlefields,
// Hazhalm, Wajaom, the Nyzul Isle runs (Path of Darkness, Nashmeira's Plea: Raubahn's reraises, Alexander) and
// Imperial Coronation's rings.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static uint CharId = 995913;
    private static readonly uint[] CharIds = { 995913, 995914, 995915, 995911, 995912 };
    private const int LogCoP = 6, LogToAU = 4;
    private const ushort Qufim = 126, LowerDelkfutt = 184, PortJeuno = 246, LowerJeuno = 245, UpperJeuno = 244, Rulude = 243;
    private const ushort Whitegate = 50, Konschtat = 108, HallOfTransference = 14, PromyvionHolla = 16, SpireOfHolla = 17, PromyvionDem = 18,
                         SpireOfDem = 19, PromyvionMea = 20, SpireOfMea = 21, LaTheine = 102, Tahrongi = 117, Lufaise = 24, TavnazianSafehold = 26;
    private const int RitesOfLife = 110, BelowTheArks = 118, TheMothercrystals = 128, AnInvitationWest = 138, TheLostCity = 218, DistantBeliefs = 228, AnEternalMelody = 238, AncientVows = 248, TheCallOfTheWyrmking = 258;
    private const int LandOfSacredSerpents = 0, ImmortalSentries = 1, PresidentSalaheem = 2, KnightOfGold = 3, ConfessionsOfRoyalty = 4,
                     EasterlyWinds = 5, WesterlyWinds = 6, AMercenaryLife = 7, UnderseaScouting = 8, AstralWaves = 9, ImperialSchemes = 10,
                     RoyalPuppeteer = 11, LostKingdom = 12, TheDolphinCrest = 13, TheBlackCoffin = 14, GhostsOfThePast = 15,
                     GuestsOfTheEmpire = 16, PassingGlory = 17, SweetsForTheSoul = 18, TeahouseTumult = 19, FindersKeepers = 20,
                     ShieldOfDiplomacy = 21, SocialGraces = 22, FoiledAmbition = 23, PlayingThePart = 24, SealOfTheSerpent = 25,
                     MisplacedNobility = 26, BastionOfKnowledge = 27, PuppetInPeril = 28, PrevalenceOfPirates = 29, ShadesOfVengeance = 30,
                     InTheBlood = 31, SentinelsHonor = 32, TestingTheWaters = 33, LegacyOfTheLost = 34, GazeOfTheSaboteur = 35, PathOfBlood = 36,
                     StirringsOfWar = 37, AlliedRumblings = 38, UnravelingReason = 39, LightOfJudgement = 40, PathOfDarkness = 41,
                     FangsOfTheLion = 42, NashmeirasPlea = 43, Ragnarok = 44, ImperialCoronation = 45, TheEmpressCrowned = 46, EternalMercenary = 47;
    private const uint TalesBeginning = 17531245, Monberaux = 17776641, Rosaline = 17776642, Pherimociel = 17772559;
    private const uint KonschtatTelepoint = 17220140, LaTheineTelepoint = 17195620, TahrongiTelepoint = 17257043, LargeApparatusDem = 16834571;
    private const uint Despachiaire = 16883721, Arquil = 16883809, SewerEntrance = 16883731, Justinius = 16883722;
    private const uint Minotaur = 16887889, LadderEast = 16888101, OrnateGate = 16888065;
    private const ushort Phomiuna = 27, Misareaux = 25, RiverneA01 = 30, MonarchLinn = 31, SouthGustaberg = 107;
    private const uint WalnutDoor = 16883740, DilapidatedGateEast = 16879936, DilapidatedGateWest = 16879938, SdEntrance = 16904338;
    private const uint WebDem = 16855147, WebHolla = 16846940, WebMea = 16863312;
    private const uint NajaSalaheem = 16982156, Cacaroon = 16982252, Nadeey = 16982257, Meyaada = 16999048, Guilerme = 17723397;
    private const ushort ArrapagoReef = 54, AlZahbi = 48, NorthernSandoria = 231, Chateau = 233, ImperialBronzePiece = 2184,
                         ImperialSilverPiece = 2185, JodysAcid = 2307, Alzadaal = 72, Nashmau = 53, CarmineChip = 1692;
    private const uint Pyopyoroon = 16994372;
    private const ushort PortBastok = 236, Metalworks = 237, SouthernSandoria = 230, CarpentersLanding = 2, WindurstWaters = 238, WindurstWalls = 239,
                         PortWindurst = 240, AttohwaChasm = 7, ShroudedMaw = 10, PsoXja = 9, RiverneB01 = 29, Sacrarium = 28, SealionsDen = 32,
                         PortSandoria = 232, Beaucedine = 111, PromyvionVahzl = 22, SpireOfVahzl = 23,
                         CaedarvaMire = 79, AshuTalif = 60, Aydeewa = 68, Navukgo = 64, ImperialMythrilPiece = 2186,
                         NyzulIsle = 77, UnderseaRuinsFireflies = 5348, BalrahnsRing = 15807, UlthalamsRing = 15808, JalzahnsRing = 15809, ImperialStandard = 129,
                         GloryCrown = 16070,
                         TalaccaCove = 57, Hazhalm = 78, Wajaom = 51, JadeSepulcher = 67, Periqia = 56, ImperialGoldPiece = 2187, CageOfDvuccaFireflies = 5346,
                         WindurstWoods = 241, BibikiBay = 4, OldtonMovalpolos = 11, MineShaft = 13, BatalliaDowns = 105, BoneyardGully = 8, BearclawPinnacle = 6, Selbina = 248, AlTaieu = 33,
                         Byrnie = 13740, HuXzoi = 34, RuHmet = 35, EmpyrealParadox = 36, Mhaura = 249, UleguerandRange = 5, Norg = 252, HallOfTheGods = 251, TavnazianRing = 14672, LeatherGloves = 12696, LeatherTrousers = 12824, LeatherHighboots = 12952;
    private const int AVesselWithoutACaptain = 318, TheRoadForks = 325, TendingAgedWounds = 350, DarknessNamed = 358, ShelteringDoubt = 368,
                      TheSavage = 418, TheSecretsOfWorship = 428, SlanderousUtterings = 438, TheEnduringTumultOfWar = 448, DesiresOfEmptiness = 518,
                      ThreePaths = 530, ForWhomTheVerseIsSung = 578, APlaceToReturn = 618, MoreQuestionsThanAnswers = 628, OneToBeFeared = 638,
                      ChainsAndBonds = 648, FlamesInTheDarkness = 718, FireInTheEyesOfMen = 728, CalmBeforeTheStorm = 738, TheWarriorsPath = 748,
                      GardenOfAntiquity = 800, AFateDecided = 818, WhenAngelsFall = 828, Dawn = 840, TheLastVerse = 850;
    private const ushort AbysseaKonschtat = 15, AbysseaLaTheine = 132, AbysseaTahrongi = 45;
    private const uint Joachim = 17784979, MawLaTheine = 17195667, MawKonschtat = 17220154, MawTahrongi = 17257077;
    private const uint Briareus = 17318446, Kukulkan = 16838872, Glavoid = 16961930;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    // a check with what to print beside a failure
    private static void Check(string name, object expected, object actual, string detail)
    {
        Check(name, expected, actual);
        if (!Equals(expected?.ToString(), actual?.ToString())) Console.WriteLine("  detail: " + detail);
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbRow(IDatabase db, string cols, string from)
    {
        var r = Sql(db, $"SELECT {cols} {from}", BoundValue.Of(CharId));
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
    }

    private static byte[] DbBlob(IDatabase db, string col)
    {
        var r = Sql(db, $"SELECT {col} FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.GetBlobBytes(col) ?? Array.Empty<byte>() : Array.Empty<byte>();
    }

    // every char table the logins, the scripts and the zone changes write
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var id in CharIds)
        {
            foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
    }

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);

        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            Cleanup(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // a Hume WAR 75 of the nation past its first login (NEW_ADVENTURER) at x / y / z in the zone, with 10000 gil and
    // the items from inventory slot 1 on
    private static void Fixture(IDatabase db, ushort zoneId, ushort prevZone, float x, float y, float z, byte nation, params (ushort Item, uint Qty)[] items)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of($"missionslive{CharId % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of($"Missionlive{CharId % 100}"), BoundValue.Of(zoneId), BoundValue.Of(prevZone),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of(nation), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,10000)", BoundValue.Of(CharId));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(CharId), BoundValue.Of((byte)(i + 1)), BoundValue.Of(items[i].Item), BoundValue.Of(items[i].Qty));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        // CoP: a WAR 75 in Qufim Island by the zone line to Lower Delkfutt's Tower
        Fixture(db, Qufim, Qufim, -285.0f, -26.7f, 320.0f, 1);

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        // the scripts' math.random, the status effects' and the mobs' rolls from one seed (LIVE_SEED, else a fresh
        // one), printed so that a failing run can be replayed
        int seed = int.TryParse(Environment.GetEnvironmentVariable("LIVE_SEED"), out int given)
            ? given : Environment.TickCount & int.MaxValue;
        Console.WriteLine($"seed {seed}");
        scripts.SeedRandom(seed);
        zone.SeedEffectRng(seed);
        zone.MobRng = new Random(seed);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        // the scripts' Vana'diel clock (VanadielUniqueDay, the wall clock as CVanaTime's) on the host's test clock
        lua.SetGlobal("VanadielUniqueDay", l => { l.Push((double)VanaTime.CountDays(vanaTicks)); return 1; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_missions");
            return string.Join(",", got);
        }
        // CLuaBaseEntity::addMission warns on a current mission other than 0 past the nation / Zilart logs, so on
        // CoP's first (the log starts at 101): that warning is expected
        const string CopFirstAdd = "lua: addMission: overwriting current mission 101 of log 6 with 110";
        var expectedWarnings = new HashSet<string> { CopFirstAdd };
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                      !expectedWarnings.Contains(l)).Distinct().ToList();

        MapSession s = null;
        CharData c = null;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);

        // the 0x00A login and 0x00C; the zone-in cutscene left open (the packets kept) or ended with 0x05B as the
        // client does
        List<BasicPacket> loginPackets = null;
        void Login(bool endZoneIn = true)
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            Send(0x00C, 12);
            loginPackets = new List<BasicPacket>(s.PacketList);
            if (!endZoneIn) return;
            for (int i = 0; i < 4 && c.IsInEvent; i++)
            {
                var ev = (ushort)c.CurrentEvent.EventId;
                Send(0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
            }
            s.PacketList.Clear();
        }
        // the zone's timers: the clock moved on, then a tick (trigger areas every 200 ms, afterZoneIn)
        void Tick(int ms = 1000)
        {
            now += ms;
            zone.Tick();
        }
        // GP_CLI_COMMAND_POS
        void MoveTo(float x, float y, float z)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
            });
        }
        NpcEntity Npc(uint id) => zone.ZoneInfoOf(c.Zone).NpcById[id];
        void Near(uint id)
        {
            var n = Npc(id);
            MoveTo(n.X + 1.5f, n.Y, n.Z);
        }
        // GP_CLI_COMMAND_ACTION Talk
        void Talk(uint id)
        {
            var n = Npc(id);
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(n.Id).CopyTo(b, 4);
                BitConverter.GetBytes(n.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
            });
        }
        // GP_CLI_COMMAND_EVENTEND: mode 0 ends the event, mode 1 asks for an update; the event's own entity (the NPC,
        // or the char for a cutscene of its own)
        void EventEnd(int eventId, uint option = 0, ushort mode = 0)
        {
            var (id, targ) = c.CurrentEvent?.TargetEntity switch
            {
                NpcEntity n => (n.Id, n.TargId),
                BattleEntity b => (b.Id, b.TargId),
                _ => (c.Id, c.TargId),
            };
            s.PacketList.Clear();
            Send(0x05B, 20, b =>
            {
                BitConverter.GetBytes(id).CopyTo(b, 4);
                BitConverter.GetBytes(option).CopyTo(b, 0x08);
                BitConverter.GetBytes(targ).CopyTo(b, 0x0C);
                BitConverter.GetBytes(mode).CopyTo(b, 0x0E);
                BitConverter.GetBytes((ushort)eventId).CopyTo(b, 0x12);
            });
        }
        // GP_CLI_COMMAND_ITEM_TRANSFER: (inventory slot, quantity) offers
        void Trade(uint id, params (byte Slot, uint Qty)[] items)
        {
            var n = Npc(id);
            s.PacketList.Clear();
            Send(0x036, 64, b =>
            {
                BitConverter.GetBytes(n.Id).CopyTo(b, 4);
                for (int i = 0; i < items.Length; i++)
                {
                    BitConverter.GetBytes(items[i].Qty).CopyTo(b, 8 + i * 4);
                    b[48 + i] = items[i].Slot;
                }
                BitConverter.GetBytes(n.TargId).CopyTo(b, 58);
                b[60] = (byte)items.Length;
            });
        }
        // GP_CLI_COMMAND_MAPRECT: the zone line at its origin, then the tick that zones the char and the next login
        void TakeZoneLine(string rect, bool endZoneIn = true)
        {
            var line = zone.ZoneInfoOf(c.Zone).ZoneLines[ZoneLine.PackId(rect)];
            MoveTo(line.OriginX, line.OriginY, line.OriginZ);
            Send(0x05E, 24, b => BitConverter.GetBytes(line.Id).CopyTo(b, 4));
            zone.Tick();
            Login(endZoneIn);
        }
        string LineTo(ushort to) => ZoneLine.IdText(zone.ZoneInfoOf(c.Zone).ZoneLines.Values.First(l => l.DestinationZone == to).Id);

        // a zone change the scripts asked for (setPos to another zone): the tick that zones the char, the next login
        void Rezone(bool endZoneIn = true)
        {
            Tick(400);
            Login(endZoneIn);
        }
        // the walk between places that are not the mission's: a GM !pos
        void Warp(float x, float y, float z, ushort zoneId)
        {
            Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
            Rezone();
        }

        // a zone change by a GM !pos that keeps the zone-in cutscene open
        void WarpIn(float x, float y, float z, ushort zoneId)
        {
            Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
            Rezone(false);
        }
        uint NpcId(string name) => zone.ZoneInfoOf(c.Zone).Npcs.First(n => n.Name == name).Id;
        // a mob the scripts spawned killed by the char: claimed first if it is not (the char's swing:
        // CBattleEntity::OnAttack's ClaimMob, the char kept facing it as the client does; onMobDeath gets a player
        // only through the claim), then the damage, the death on the next tick, the defeat (DistributeRewards:
        // onMobDeath) a second on
        void KillByChar(uint mobId)
        {
            ClaimBySwing(mobId);
            Lua($"local mob = GetMobByID({mobId}, player:getInstance()) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            Tick(1000);
            Tick(1000);
        }
        // a fight brought down to a HP percentage: claimed by the char's swing, the HP set, the ticks of its fight
        // (onMobFight) that follow
        void FightTo(uint mobId, int hpp)
        {
            ClaimBySwing(mobId);
            var m = (c.Instance?.Info ?? zone.ZoneInfoOf(c.Zone)).MobById[mobId];
            m.Hp = m.MaxHp * hpp / 100;
            Tick(1000);
            Tick(1000);
        }
        void ClaimBySwing(uint mobId)
        {
            var m = (c.Instance?.Info ?? zone.ZoneInfoOf(c.Zone)).MobById[mobId];
            if (m.OwnerId != c.Id)
            {
                Send(0x015, 32, b =>
                {
                    BitConverter.GetBytes(m.X - 2.0f).CopyTo(b, C2sPos.OffX);
                    BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
                    BitConverter.GetBytes(m.Z).CopyTo(b, C2sPos.OffY);
                    b[C2sPos.OffDir] = 0;
                });
                Send(0x01A, 28, b =>
                {
                    BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                    BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                    BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
                });
                for (int i = 0; m.OwnerId != c.Id && i < 30; i++)
                {
                    float dx = m.X - c.X, dz = m.Z - c.Z;
                    byte dir = (byte)(short)(MathF.Atan2(dz, dx) * -(128.0f / MathF.PI));
                    Send(0x015, 32, b =>
                    {
                        BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
                        BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                        BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
                        b[C2sPos.OffDir] = dir;
                    });
                    Tick(1000);
                }
            }
        }

        // the event packet the server sent: 0x032 / 0x034 as op:entity:event[:params]; from the given list (the login's)
        string Ev(List<BasicPacket> from = null)
        {
            var ev = (from ?? new List<BasicPacket>(s.PacketList)).LastOrDefault(p => Opcode(p) is 0x032 or 0x033 or 0x034);
            if (ev == null) return "none";
            var b = ev.Buffer;
            if (Opcode(ev) == 0x032) return $"032:{BitConverter.ToUInt32(b, 4)}:{BitConverter.ToUInt16(b, 12)}";
            if (Opcode(ev) == 0x033) return $"033:{BitConverter.ToUInt32(b, 4 + S2cEventStr.OffUniqueNo)}:{BitConverter.ToUInt16(b, 4 + S2cEventStr.OffEventPara)}";
            var pars = new List<string>();
            for (int i = 0; i < S2cEventNum.NumCount; i++) pars.Add(BitConverter.ToUInt32(b, 4 + S2cEventNum.OffNum + i * 4).ToString());
            while (pars.Count > 0 && pars[^1] == "0") pars.RemoveAt(pars.Count - 1);
            return $"034:{BitConverter.ToUInt32(b, 4 + S2cEventNum.OffUniqueNo)}:{BitConverter.ToUInt16(b, 4 + S2cEventNum.OffEventPara)}" +
                   (pars.Count > 0 ? ":" + string.Join(",", pars) : "");
        }
        // the event packet's text table (0x034's EventNum2)
        string EvTable(List<BasicPacket> from = null)
        {
            var ev = (from ?? new List<BasicPacket>(s.PacketList)).LastOrDefault(p => Opcode(p) == 0x034);
            return ev == null ? "none" : BitConverter.ToUInt16(ev.Buffer, 4 + S2cEventNum.OffEventNum2).ToString();
        }
        // the event's entity and id, whichever packet carried it
        string EvId()
        {
            var p = Ev().Split(':');
            return p.Length >= 3 ? $"{p[1]}:{p[2]}" : p[0];
        }
        string Cur() => c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "none";
        // chars.missions as the C++ missionlog_t[]: current, statusUpper, statusLower, complete[64]
        string DbMission(int logId)
        {
            var blob = DbBlob(db, "missions");
            int off = logId * CharData.MissionLogSize;
            if (blob.Length < off + CharData.MissionLogSize) return "-";
            return $"{BitConverter.ToUInt16(blob, off)}/{BitConverter.ToUInt16(blob, off + 2) << 16 | BitConverter.ToUInt16(blob, off + 4)}";
        }
        bool DbMissionComplete(int logId, int id)
        {
            var blob = DbBlob(db, "missions");
            int off = logId * CharData.MissionLogSize + 6 + id;
            return off < blob.Length && blob[off] != 0;
        }
        string Mission(int logId) => Lua($"report(player:getCurrentMission({logId})) report(player:getMissionStatus({logId}))");
        string Var(string name) => DbRow(db, "value", $"FROM char_vars WHERE charid = ? AND varname = '{name}'");
        string Qty(ushort itemId) => DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND itemId = {itemId}");
        string Gil() => DbRow(db, "quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0");
        // keyitems_t: per table of 512, the keyList bits then the seenList bits
        string DbKeyItem(int ki)
        {
            var k = DbBlob(db, "keyitems");
            int off = ki / 512 * 128 + ki % 512 / 8, bit = 1 << ki % 8;
            bool has = off < k.Length && (k[off] & bit) != 0, seen = off + 64 < k.Length && (k[off + 64] & bit) != 0;
            return $"{(has ? 1 : 0)}{(seen ? 1 : 0)}";
        }
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        int TitleId(string name) => int.Parse(Lua($"report(xi.title.{name})"));
        // the 0x02A TALKNUMWORK messages as message:param0
        string SpecialOf(List<BasicPacket> from) => string.Join(",", from.FindAll(p => Opcode(p) == 0x02A).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        string Special() => string.Join(",", Of(0x02A).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        string Text(ushort zoneId, string name) => Lua($"report(zones[{zoneId}].text.{name})");
        bool DbTitle(int title)
        {
            var t = DbBlob(db, "titles");
            return title / 8 < t.Length && (t[title / 8] & 1 << title % 8) != 0;
        }
        // the 0x056 MISSION page (port 0xFFFF): CoP current, CoP status, the Tales' Beginning flags
        string CopPage(List<BasicPacket> from = null)
        {
            var p = (from ?? Of(0x056)).LastOrDefault(q => Opcode(q) == 0x056 && BitConverter.ToUInt16(q.Buffer, 36) == 0xFFFF);
            return p == null ? "none" : $"{BitConverter.ToUInt32(p.Buffer, 16)}/{BitConverter.ToUInt32(p.Buffer, 20)}/{BitConverter.ToUInt16(p.Buffer, 26)}";
        }
        // the 0x056 ToAU pages: the Aht Urhgan offer page (port 0x80) carrying the current ToAU mission, and the
        // ToAU / WoTG complete page (port 0xD8)
        string ToauPages()
        {
            var cur = Of(0x056).LastOrDefault(q => BitConverter.ToUInt16(q.Buffer, 36) == 0x80);
            var done = Of(0x056).LastOrDefault(q => BitConverter.ToUInt16(q.Buffer, 36) == 0xD8);
            return $"{(cur == null ? "-" : BitConverter.ToUInt32(cur.Buffer, 24).ToString())}/{(done == null ? "-" : Convert.ToString(BitConverter.ToUInt32(done.Buffer, 4), 2))}";
        }

        // a mission battlefield: the entry NPC talked to (the battlefield's onEntryTrigger: event 32000), the arena
        // asked for in a GP_CLI_COMMAND_EVENTENDXZY (the menu index from [BCNM]MenuOptions, area 1), the cutscene
        // finished (entered), the spawned mobs' HP to 0 (allDeath: WON), the ticks to the win's cutscene 32001
        // (onBattlefieldWin) and its end (for a spire, xi.cop.helpers.spireEventFinish: the Light, the mission, the
        // teleport); the packets of the end kept in winPackets
        List<BasicPacket> winPackets = null;
        void WinBattlefield(uint webId, string where, bool endZoneIn = true)
        {
            var bf = EnterBattlefield(webId, where);
            if (bf == null) return;
            var spawned = bf.RequiredEnemyList.Concat(bf.AdditionalEnemyList).Select(m => m.PMob).Where(m => m.IsSpawned).ToList();
            foreach (var m in spawned) m.Hp = 0;
            for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
            Check($"  the mobs dead, won: onBattlefieldWin's cutscene 32001", "2/32001", $"{bf.Status}/{Cur()}");
            EventEnd(32001);
            winPackets = new List<BasicPacket>(s.PacketList);
            Rezone(endZoneIn);
        }
        // the entry NPC talked to, the arena asked for and its cutscene finished: the char's battlefield
        Battlefield EnterBattlefield(uint webId, string where)
        {
            var web = Npc(webId);
            Near(webId);
            Talk(webId);
            Check($"{where}: the entry NPC, event 32000", $"32000", Cur());
            uint menu = uint.Parse(Lua("report(player:getLocalVar('[BCNM]MenuOptions'))"));
            int index = menu == 0 ? -1 : System.Numerics.BitOperations.TrailingZeroCount(menu);
            Check($"  the battlefield on offer", "True", index >= 0);
            s.PacketList.Clear();
            Send(0x05C, 32, b =>
            {
                BitConverter.GetBytes(web.X).CopyTo(b, 4);
                BitConverter.GetBytes(web.Y).CopyTo(b, 8);
                BitConverter.GetBytes(web.Z).CopyTo(b, 12);
                BitConverter.GetBytes(web.Id).CopyTo(b, 16);
                BitConverter.GetBytes((uint)(index << 4 | 1)).CopyTo(b, 20);
                BitConverter.GetBytes(web.TargId).CopyTo(b, 24);
                BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
                b[30] = 1;
            });
            var bf = c.PBattlefield;
            Check($"  registered in area 1", "True/1", $"{bf != null}/{bf?.Area}");
            EventEnd(32000);
            Check($"  entered", "True/none", $"{bf != null && bf.IsEntered(c)}/{Cur()}");
            return bf;
        }

        // ---- Chains of Promathia ----
        Console.WriteLine("CoP: the login in Qufim Island");
        Login();
        Check("in Qufim Island, out of any event", $"{Qufim}/none", $"{c.Zone}/{Cur()}");
        Check("the CoP log at its start (101), nothing begun", "101,0", Mission(LogCoP));
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-1 The Rites of Life: the zone line to Lower Delkfutt's Tower");
        TakeZoneLine(LineTo(LowerDelkfutt), false);
        Check("in Lower Delkfutt's Tower from Qufim: the mission's onZoneIn cutscene 22 in the login", $"{LowerDelkfutt}/{Qufim}/22",
              $"{c.Zone}/{c.PrevZone}/{Cur()}");
        Check("  the 0x00A carries event 22 (the zone-in cutscene)", "22", Ev(loginPackets) == "none" ? Cur() : Cur());
        EventEnd(22, 0);
        Check("cutscene 22 declined (option 0): [COP]TalesBeginning 1, out of the event", "1/none", $"{Var("[COP]TalesBeginning")}/{Cur()}");
        Check("  sendPartialMissionLog: the MISSION page with CoP zeroed, the CoP Tales' Beginning flag (8)", "0/0/8", CopPage());
        Check("  enableEntities: a 0x077 for Tales' Beginning", "True", Of(0x077).Count > 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-1: Tales' Beginning");
        Near(TalesBeginning);
        Talk(TalesBeginning);
        Check("Tales' Beginning: event 53 with param 3", $"034:{TalesBeginning}:53:3/53", $"{Ev()}/{Cur()}");
        EventEnd(53, 4);
        // luautils::OnEventFinish runs with the finished event as the eventPreparation, so the new event keeps its NPC
        Check("event 53 option 4: cutscene 22 again (param 3 = 1), from Tales' Beginning", $"034:{TalesBeginning}:22:0,0,0,1/22", $"{Ev()}/{Cur()}");
        foreach (var (from, next) in new[] { (22, 36), (36, 37), (37, 38), (38, 39) })
        {
            EventEnd(from, (uint)(from == 22 ? 1 : 0));
            Check($"event {from} finished: hidden cutscene {next}", $"{next}", Cur());
        }
        EventEnd(39);
        Check("event 39 finished: The Rites of Life begun, Status 1, TalesBeginning cleared", $"{RitesOfLife},0/1/-/none",
              $"{Mission(LogCoP)}/{Var($"Mission[{LogCoP}][{RitesOfLife}]Status")}/{Var("[COP]TalesBeginning")}/{Cur()}");
        Check("  addMission's warning: the CoP log at 101, not 0", "True", log.Contains(CopFirstAdd));
        Check("  chars.missions: CoP current", $"{RitesOfLife}/0", DbMission(LogCoP));
        Check("  the MISSION page: CoP The Rites of Life, no Tales' flag", $"{RitesOfLife}/0/0", CopPage());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-1: to Upper Jeuno");
        TakeZoneLine(LineTo(Qufim));
        Check("in Qufim Island, no cutscene", $"{Qufim}", c.Zone);
        TakeZoneLine(LineTo(PortJeuno));
        TakeZoneLine(LineTo(LowerJeuno));
        TakeZoneLine(LineTo(UpperJeuno), false);
        Check("in Upper Jeuno: the mission's zone-in cutscene 2", $"{UpperJeuno}/2", $"{c.Zone}/{Cur()}");
        EventEnd(2);
        Check("cutscene 2 finished: Status 2", "2/none", $"{Var($"Mission[{LogCoP}][{RitesOfLife}]Status")}/{Cur()}");

        Console.WriteLine("CoP 1-1: Monberaux");
        Near(Monberaux);
        Talk(Monberaux);
        Check("Monberaux: event 10", $"032:{Monberaux}:10/10", $"{Ev()}/{Cur()}");
        EventEnd(10);
        Check("event 10 finished: event 206", "206", Cur());
        EventEnd(206);
        Check("event 206 finished: event 207", "207", Cur());
        int amulet = Ki("MYSTERIOUS_AMULET");
        EventEnd(207);
        Check("event 207 finished: The Rites of Life complete, Below the Arks current", $"{BelowTheArks},0/{BelowTheArks}/0",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the Mysterious Amulet, in chars.keyitems, the KEYITEM_OBTAINED message", $"10/{Text(UpperJeuno, "KEYITEM_OBTAINED")}:{amulet}",
              $"{DbKeyItem(amulet)}/{Special()}");
        Check("  the MISSION page: CoP Below the Arks", $"{BelowTheArks}/0/0", CopPage());
        Check("  hasCompletedMission(COP, THE_RITES_OF_LIFE)", "true",
              Lua($"report(tostring(player:hasCompletedMission({LogCoP}, {RitesOfLife})))"));
        Check("  the mission vars cleared", "-", Var($"Mission[{LogCoP}][{RitesOfLife}]Status"));
        Near(Rosaline);
        Talk(Rosaline);
        Check("Rosaline after The Rites of Life: event 97 (replaceDefault)", $"032:{Rosaline}:97", Ev());
        EventEnd(97);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-2 Below the Arks: Pherimociel in Ru'Lude Gardens");
        TakeZoneLine(LineTo(Rulude));
        Check("in Ru'Lude Gardens", Rulude, c.Zone);
        Near(Pherimociel);
        Talk(Pherimociel);
        Check("Pherimociel: event 24", $"032:{Pherimociel}:24/24", $"{Ev()}/{Cur()}");
        EventEnd(24);
        Check("event 24 finished: Status 1", "1", Var($"Mission[{LogCoP}][{BelowTheArks}]Status"));
        Talk(Pherimociel);
        Check("Pherimociel again: event 25", $"032:{Pherimociel}:25", Ev());
        EventEnd(25);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-2: the Shattered Telepoint in Konschtat Highlands");
        // the walk to Konschtat Highlands: a GM !pos to the telepoint (1_2_Below_the_Arks.lua's header)
        Warp(135.0f, 19.0f, 220.0f, Konschtat);
        Check("in Konschtat Highlands", Konschtat, c.Zone);
        Near(KonschtatTelepoint);
        Talk(KonschtatTelepoint);
        Check("the Shattered Telepoint: event 913, the first entry (param 2 = 1)", $"034:{KonschtatTelepoint}:913:0,0,1/913", $"{Ev()}/{Cur()}");
        EventEnd(913);
        Rezone(false);
        Check("event 913 finished (option 0): setPos into the Hall of Transference, its zone-in cutscene 108", $"{HallOfTransference}/{Konschtat}/108",
              $"{c.Zone}/{c.PrevZone}/{Cur()}");
        EventEnd(108);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-2: the Large Apparatus to Promyvion - Dem");
        Near(LargeApparatusDem);
        Talk(LargeApparatusDem);
        Check("the Large Apparatus (_0e5): event 160", $"032:{LargeApparatusDem}:160/160", $"{Ev()}/{Cur()}");
        EventEnd(160);
        Rezone(false);
        Check("event 160 finished: sendToPromyvionZone(DEM), Promyvion - Dem's zone-in cutscene 50", $"{PromyvionDem}/50", $"{c.Zone}/{Cur()}");
        EventEnd(50);
        Check("cutscene 50 finished: Option DEM (2)", "2", Var($"Mission[{LogCoP}][{BelowTheArks}]Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-2: Ancient Flames Beckon in the Spire of Dem");
        // the memory receptacles and fluxes of Promyvion: a GM !pos to the Spire of Dem's Web of Recollections
        Warp(0.0f, -3.8f, 240.0f, SpireOfDem);
        Check("in the Spire of Dem", SpireOfDem, c.Zone);
        int lightDem = Ki("LIGHT_OF_DEM"), lightHolla = Ki("LIGHT_OF_HOLLA"), lightMea = Ki("LIGHT_OF_MEA");
        WinBattlefield(WebDem, "Spire of Dem");
        Check("  the Light of Dem, in chars.keyitems, CANT_REMEMBER", $"10/{Text(SpireOfDem, "CANT_REMEMBER")}:{lightDem}",
              $"{DbKeyItem(lightDem)}/{SpecialOf(winPackets)}");
        Check("  Below the Arks complete, The Mothercrystals current", $"{TheMothercrystals},0/{TheMothercrystals}/0", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the MISSION page: CoP The Mothercrystals", $"{TheMothercrystals}/0/0", CopPage(winPackets));
        Check("  xi.teleport.to(EXITPROMDEM): Konschtat Highlands by the telepoint", $"{Konschtat}/136/220", $"{c.Zone}/{c.X}/{c.Z}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-3 The Mothercrystals: the Shattered Telepoint in La Theine Plateau to Promyvion - Holla");
        Near(KonschtatTelepoint);
        Talk(KonschtatTelepoint);
        Check("Konschtat's telepoint with the Light of Dem: event 913, no params", $"032:{KonschtatTelepoint}:913", Ev());
        EventEnd(913, 1);
        Warp(334.0f, 19.0f, -60.0f, LaTheine);
        Near(LaTheineTelepoint);
        Talk(LaTheineTelepoint);
        Check("La Theine's telepoint, one Light held: event 201, toPromyvion HOLLA (1)", $"032:{LaTheineTelepoint}:201/201/1",
              $"{Ev()}/{Cur()}/{c.LocalVars.GetValueOrDefault("toPromyvion")}");
        EventEnd(201);
        Rezone(false);
        Check("event 201 finished (option 0): sendToPromyvionZone(HOLLA), Promyvion - Holla's zone-in cutscene 51", $"{PromyvionHolla}/51", $"{c.Zone}/{Cur()}");
        EventEnd(51);
        Check("cutscene 51 finished: Option HOLLA (1)", "1", Var($"Mission[{LogCoP}][{TheMothercrystals}]Option"));
        Warp(0.0f, -3.8f, -240.0f, SpireOfHolla);
        WinBattlefield(WebHolla, "Spire of Holla");
        Check("  the Light of Holla; still The Mothercrystals", $"10/{TheMothercrystals},0", $"{DbKeyItem(lightHolla)}/{Mission(LogCoP)}");
        Check("  xi.teleport.to(EXITPROMHOLLA): La Theine Plateau", $"{LaTheine}/337", $"{c.Zone}/{c.X}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 1-3: the Shattered Telepoint in Tahrongi Canyon, the Hall of Transference to Promyvion - Mea");
        Warp(179.0f, 35.0f, 255.0f, Tahrongi);
        Near(TahrongiTelepoint);
        Talk(TahrongiTelepoint);
        Check("Tahrongi's telepoint, two Lights held: event 913, not a first entry", $"034:{TahrongiTelepoint}:913/913", $"{Ev()}/{Cur()}");
        EventEnd(913);
        Rezone(false);
        Check("event 913 finished: the Hall of Transference from Tahrongi, its zone-in cutscene 155, toPromyvion MEA (3)",
              $"{HallOfTransference}/{Tahrongi}/155/3", $"{c.Zone}/{c.PrevZone}/{Cur()}/{c.LocalVars.GetValueOrDefault("toPromyvion")}");
        EventEnd(155);
        Check("cutscene 155 finished: Status 1", "1", Var($"Mission[{LogCoP}][{TheMothercrystals}]Status"));
        Rezone(false);
        Check("sendToPromyvionZone(MEA): Promyvion - Mea's zone-in cutscene 52", $"{PromyvionMea}/52", $"{c.Zone}/{Cur()}");
        EventEnd(52);
        Check("cutscene 52 finished: Option MEA (3)", "3", Var($"Mission[{LogCoP}][{TheMothercrystals}]Option"));
        Warp(0.0f, -3.8f, 240.0f, SpireOfMea);
        int titleFollower = TitleId("ANCIENT_FLAME_FOLLOWER");
        WinBattlefield(WebMea, "Spire of Mea", false);
        Check("  the Light of Mea", "10", DbKeyItem(lightMea));
        Check("  The Mothercrystals complete, An Invitation West current", $"{AnInvitationWest},0/{AnInvitationWest}/0", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the reward: the Ancient Flame Follower title, in chars.titles", $"{titleFollower}/True",
              $"{Lua("report(player:getTitle())")}/{DbTitle(titleFollower)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-1 An Invitation West: Lufaise Meadows");
        Check("xi.teleport.to(LUFAISE): Lufaise Meadows, An Invitation West's zone-in cutscene 110", $"{Lufaise}/110", $"{c.Zone}/{Cur()}");
        EventEnd(110);
        Check("cutscene 110 finished: the Mysterious Amulet taken, KI_STOLEN, Status 1",
              $"00/{Text(Lufaise, "KI_STOLEN")}:0/1", $"{DbKeyItem(amulet)}/{Special()}/{Var($"Mission[{LogCoP}][{AnInvitationWest}]Status")}");
        TakeZoneLine("z0o2", false);
        Check("in the Tavnazian Safehold: the zone-in cutscene 101", $"{TavnazianSafehold}/101", $"{c.Zone}/{Cur()}");
        int titleDeadBody = TitleId("DEAD_BODY");
        EventEnd(101);
        Check("cutscene 101 finished: An Invitation West complete, The Lost City current", $"{TheLostCity},0/{TheLostCity}/0", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the reward: the Dead Body title", $"{titleDeadBody}/True", $"{Lua("report(player:getTitle())")}/{DbTitle(titleDeadBody)}");
        Check("  the MISSION page: CoP The Lost City", $"{TheLostCity}/0/0", CopPage());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-2 The Lost City: Despachiaire, the Sewer Entrance");
        Near(Despachiaire);
        Talk(Despachiaire);
        Check("Despachiaire: event 102", $"032:{Despachiaire}:102/102", $"{Ev()}/{Cur()}");
        EventEnd(102);
        Check("event 102 finished: Status 1", "1", Var($"Mission[{LogCoP}][{TheLostCity}]Status"));
        Talk(Despachiaire);
        Check("Despachiaire again: event 106 with param 1", $"034:{Despachiaire}:106:1", Ev());
        EventEnd(106);
        Near(Arquil);
        Talk(Arquil);
        Check("Arquil: event 291", $"032:{Arquil}:291", Ev());
        EventEnd(291);
        Near(SewerEntrance);
        Talk(SewerEntrance);
        Check("the Sewer Entrance (_0q1): event 103", $"032:{SewerEntrance}:103/103", $"{Ev()}/{Cur()}");
        EventEnd(103);
        Check("event 103 finished: The Lost City complete, Distant Beliefs current", $"{DistantBeliefs},0/{DistantBeliefs}/0", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the mission var cleared", "-", Var($"Mission[{LogCoP}][{TheLostCity}]Status"));
        Near(Despachiaire);
        Talk(Despachiaire);
        Check("Despachiaire after: event 106 (the after-completion section)", $"032:{Despachiaire}:106", Ev());
        EventEnd(106);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-3 Distant Beliefs: the Minotaur in Phomiuna Aqueducts");
        // the sewers to the Aqueducts: a GM !pos by the Minotaur (2_3_Distant_Beliefs.lua's header)
        Warp(57.0f, 0.5f, 304.0f, Phomiuna);
        Check("in Phomiuna Aqueducts", Phomiuna, c.Zone);
        var minotaur = zone.ZoneInfoOf(Phomiuna).MobById[Minotaur];
        Check("the Minotaur up", "True", minotaur.IsSpawned && minotaur.IsAlive);
        minotaur.Hp = 1;
        // the char's swings miss the Minotaur often (its level): a GM's accuracy so a minute of swings lands one
        Lua("player:addMod(xi.mod.ACC, 1000)");
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(minotaur.X - 2.0f).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(minotaur.Y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(minotaur.Z).CopyTo(b, C2sPos.OffY);
            b[C2sPos.OffDir] = 0;
        });
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(minotaur.Id).CopyTo(b, 4);
            BitConverter.GetBytes(minotaur.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
        });
        // the client keeps the char facing its target as the Minotaur moves (else "unable to see")
        for (int i = 0; minotaur.Hp > 0 && i < 60; i++)
        {
            float dx = minotaur.X - c.X, dz = minotaur.Z - c.Z;
            byte dir = (byte)(short)(MathF.Atan2(dz, dx) * -(128.0f / MathF.PI));
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            Tick(1000);
        }
        Tick(1000);
        Lua("player:delMod(xi.mod.ACC, 1000)");
        Check("the Minotaur killed by the char: onMobDeath (the mission's Minotaur section) set Status 1", "True/1",
              $"{minotaur.IsDead}/{Var($"Mission[{LogCoP}][{DistantBeliefs}]Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-3: the Wooden Ladder, the Ornate Gate");
        Warp(-158.8f, -1.8f, 60.0f, Phomiuna);
        Talk(LadderEast);
        Check("the Wooden Ladder from its foot: event 35", $"032:{LadderEast}:35/35", $"{Ev()}/{Cur()}");
        EventEnd(35);
        Check("event 35 finished: Status 2", "2", Var($"Mission[{LogCoP}][{DistantBeliefs}]Status"));
        Warp(-97.0f, -25.5f, 60.0f, Phomiuna);
        Talk(OrnateGate);
        Check("the Ornate Gate (_0r5): cutscene 36 (progressCutscene), the char locked", $"032:{OrnateGate}:36/36/{EventType.Cutscene}/True",
              $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}/{c.Locked}");
        EventEnd(36);
        Check("cutscene 36 finished: Status 3, unlocked", "3/False", $"{Var($"Mission[{LogCoP}][{DistantBeliefs}]Status")}/{c.Locked}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-3: Justinius in the Tavnazian Safehold");
        Warp(76.0f, -34.0f, 68.0f, TavnazianSafehold);
        Near(Justinius);
        Talk(Justinius);
        Check("Justinius: event 113", $"032:{Justinius}:113/113", $"{Ev()}/{Cur()}");
        EventEnd(113);
        Check("event 113 finished: Distant Beliefs complete, An Eternal Melody current", $"{AnEternalMelody},0/{AnEternalMelody}/0",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the MISSION page: CoP An Eternal Melody", $"{AnEternalMelody}/0/0", CopPage());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-4 An Eternal Melody: the Walnut Door, the Dilapidated Gate, the Safehold's trigger area");
        Talk(Justinius);
        Check("Justinius: event 125", $"032:{Justinius}:125", Ev());
        EventEnd(125);
        Near(WalnutDoor);
        Talk(WalnutDoor);
        Check("the Walnut Door (_0qa): event 104", $"032:{WalnutDoor}:104/104", $"{Ev()}/{Cur()}");
        EventEnd(104);
        Check("event 104 finished: the Mysterious Amulet back, KEYITEM_OBTAINED, Status 1",
              $"10/{Text(TavnazianSafehold, "KEYITEM_OBTAINED")}:{amulet}/1", $"{DbKeyItem(amulet)}/{Special()}/{Var($"Mission[{LogCoP}][{AnEternalMelody}]Status")}");
        Near(Justinius);
        Talk(Justinius);
        Check("Justinius: event 127", $"032:{Justinius}:127", Ev());
        EventEnd(127);
        Warp(258.0f, 7.3f, -438.0f, Misareaux);
        Near(DilapidatedGateEast);
        Talk(DilapidatedGateEast);
        Check("the Dilapidated Gate (_0p0): cutscene 5", $"032:{DilapidatedGateEast}:5/5/{EventType.Cutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(5);
        Check("cutscene 5 finished: Status 2", "2", Var($"Mission[{LogCoP}][{AnEternalMelody}]Status"));
        Warp(0.0f, -22.0f, 10.0f, TavnazianSafehold);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(0.0f, -22.0f, 22.0f);
        Tick(250);
        Check("trigger area 1 in the Safehold: event 105", $"032:{c.Id}:105/105", $"{Ev()}/{Cur()}");
        EventEnd(105);
        Check("event 105 finished: An Eternal Melody complete, Ancient Vows current", $"{AncientVows},0/{AncientVows}/0", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 2-5 Ancient Vows: the Dilapidated Gate, Riverne - Site #A01, Monarch Linn");
        Warp(-258.0f, -32.7f, 278.0f, Misareaux);
        Near(DilapidatedGateWest);
        Talk(DilapidatedGateWest);
        Check("the Dilapidated Gate (_0p2): event 6", $"032:{DilapidatedGateWest}:6/6", $"{Ev()}/{Cur()}");
        EventEnd(6);
        Check("event 6 finished: Status 1", "1", Var($"Mission[{LogCoP}][{AncientVows}]Status"));
        // the interaction framework alternates between its own actions and the NPC's script: DefaultActions.lua's
        // DOOR_CLOSED (a Default-priority action) first, then _0p2.lua
        Talk(DilapidatedGateWest);
        Check("the gate again: DefaultActions' DOOR_CLOSED, no event", $"none/{Text(Misareaux, "DOOR_CLOSED")}:0", $"{Ev()}/{Special()}");
        Talk(DilapidatedGateWest);
        Check("and again: its own script, cutscene 552 (past An Eternal Melody)", $"032:{DilapidatedGateWest}:552", Ev());
        EventEnd(552);
        // the gate's cutscene takes the client through: a GM !pos to the Site's entrance
        Lua("player:setPos(732.55, -32.5, -506.544, 90, xi.zone.RIVERNE_SITE_A01)");
        Rezone(false);
        Check("in Riverne - Site #A01 from Misareaux Coast: the zone-in cutscene 100", $"{RiverneA01}/{Misareaux}/100", $"{c.Zone}/{c.PrevZone}/{Cur()}");
        EventEnd(100);
        Check("cutscene 100 finished: Status 2", "2", Var($"Mission[{LogCoP}][{AncientVows}]Status"));
        // the Site's spatial displacements to Monarch Linn: a GM !pos by the entrance
        Warp(-37.0f, -3.3f, -540.0f, MonarchLinn);
        int titleTraveler = TitleId("TAVNAZIAN_TRAVELER");
        WinBattlefield(SdEntrance, "Ancient Vows", true);
        Check("  the win cutscene finished: Ancient Vows complete, The Call of the Wyrmking current",
              $"{TheCallOfTheWyrmking},0/{TheCallOfTheWyrmking}/0", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("  the battlefield's title: Tavnazian Traveler", $"True", DbTitle(titleTraveler).ToString());
        Check("  setPos to South Gustaberg", $"{SouthGustaberg}/694/-619", $"{c.Zone}/{c.X}/{c.Z}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Treasures of Aht Urhgan ----
        Console.WriteLine("ToAU: the login in Aht Urhgan Whitegate with the Boarding Permit");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[1];
        Fixture(db, Whitegate, Whitegate, 0.0f, -2.0f, -60.0f, 1);
        Login();
        int permit = Ki("BOARDING_PERMIT"), package = Ki("SUPPLIES_PACKAGE"), badge = Ki("PSC_WILDCAT_BADGE");
        Lua("player:addKeyItem(xi.keyItem.BOARDING_PERMIT)");
        Check("in Whitegate with the permit, Land of Sacred Serpents current", $"{Whitegate}/10/{LandOfSacredSerpents},0",
              $"{c.Zone}/{DbKeyItem(permit)}/{Mission(LogToAU)}");
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 1 Land of Sacred Serpents: trigger area 3 on Salaheem's Sentinels' platform");
        // xi.player.onGameIn's ZoningIn local var keeps the char out of trigger areas for its 2.5 s timer
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.5f, -56.0f);
        Tick(250);
        Check("trigger area 3 entered: event 3000 with text table 0", $"034:{c.Id}:3000/0/3000", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3000);
        Check("event 3000 finished: Land of Sacred Serpents complete, Immortal Sentries current", $"{ImmortalSentries},0/{ImmortalSentries}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, LandOfSacredSerpents)}");
        Check("  the Supplies Package", "10", DbKeyItem(package));
        Check("  0x056: the Aht Urhgan offer page (Immortal Sentries), the complete page (bit 0)", $"{ImmortalSentries}/1", ToauPages());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 2 Immortal Sentries: Naja Salaheem with the package");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: her default dialog, event 3001 with text table 0", $"034:{NajaSalaheem}:3001/0", $"{Ev()}/{EvTable()}");
        EventEnd(3001);
        Check("  still Immortal Sentries", $"{ImmortalSentries},0", Mission(LogToAU));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 2: the Supplies Package to Meyaada in Arrapago Reef");
        // the way through Bhaflau Thickets and the Ilrusi Atoll: a GM !pos (02_Immortal_Sentries.lua's header)
        Warp(22.0f, -7.9f, 570.0f, ArrapagoReef);
        Check("in Arrapago Reef", ArrapagoReef, c.Zone);
        Near(Meyaada);
        Talk(Meyaada);
        Check("Meyaada with the package: event 5", $"032:{Meyaada}:5/5", $"{Ev()}/{Cur()}");
        EventEnd(5, 1);
        Check("event 5 finished (option 1): the package handed over, 150 imperial standing", "00/150",
              $"{DbKeyItem(package)}/{Lua("report(player:getCurrency('imperial_standing'))")}");
        Check("  HAND_OVER_TO_IMMORTAL (the package), YOUR_IMPERIAL_STANDING",
              $"{Text(ArrapagoReef, "HAND_OVER_TO_IMMORTAL")}:{package},{Text(ArrapagoReef, "YOUR_IMPERIAL_STANDING")}:0", Special());
        Check("  char_points: imperial_standing", "150", DbRow(db, "imperial_standing", "FROM char_points WHERE charid = ?"));
        Talk(Meyaada);
        Check("Meyaada after: event 6", $"032:{Meyaada}:6", Ev());
        EventEnd(6);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 2: back to Naja Salaheem");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        int titlePrivate = TitleId("PRIVATE_SECOND_CLASS");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem without the package: event 3002 with text table 0", $"034:{NajaSalaheem}:3002/0/3002", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3002);
        Check("event 3002 finished: Immortal Sentries complete, President Salaheem current", $"{PresidentSalaheem},0/{PresidentSalaheem}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, ImmortalSentries)}");
        Check("  the PSC Wildcat Badge, the Private Second Class title", $"10/{titlePrivate}/True",
              $"{DbKeyItem(badge)}/{Lua("report(player:getTitle())")}/{DbTitle(titlePrivate)}");
        Check("  MEMBER_OF_SALAHEEMS_SENTINELS, KEYITEM_OBTAINED, ACCESS_TO_A_MOG_LOCKER",
              $"{Text(Whitegate, "MEMBER_OF_SALAHEEMS_SENTINELS")}:0,{Text(Whitegate, "KEYITEM_OBTAINED")}:{badge},{Text(Whitegate, "ACCESS_TO_A_MOG_LOCKER")}:0",
              Special());
        Check("  0x056: President Salaheem on the offer page, bits 0 and 1 on the complete page", $"{PresidentSalaheem}/11", ToauPages());
        Check("  the mercenary rank (xi.besieged.getMercenaryRank): 1", "1", Lua("report(xi.besieged.getMercenaryRank(player))"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 3 President Salaheem: the Mog Locker scam, a zone out, Not-Trion");
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 73", $"034:{NajaSalaheem}:73/0", $"{Ev()}/{EvTable()}");
        EventEnd(73);
        Check("event 73 finished: mission status 1, mustZone", $"{PresidentSalaheem},1/{PresidentSalaheem}/1/true",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {PresidentSalaheem})))")}");
        Talk(NajaSalaheem);
        Check("Naja Salaheem before zoning: event 3003 with the mercenary rank", $"034:{NajaSalaheem}:3003:1", Ev());
        EventEnd(3003);
        TakeZoneLine("z1e0");
        Check("in Al Zahbi", AlZahbi, c.Zone);
        TakeZoneLine(LineTo(Whitegate));
        Check("back in Whitegate: the mustZone cleared", $"{Whitegate}/false",
              $"{c.Zone}/{Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {PresidentSalaheem})))")}");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3020 (Not-Trion)", $"034:{NajaSalaheem}:3020/3020", $"{Ev()}/{Cur()}");
        EventEnd(3020);
        Check("event 3020 finished: President Salaheem complete, Knight of Gold current, the status reset", $"{KnightOfGold},0/{KnightOfGold}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, PresidentSalaheem)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 4 Knight of Gold: Cacaroon, 1000 gil, the Walahra Temple, the Shaharat Teahouse");
        Near(Cacaroon);
        Talk(Cacaroon);
        Check("Cacaroon: event 3035, mission status 0 in param 7", $"034:{Cacaroon}:3035/3035", $"{Ev()}/{Cur()}");
        EventEnd(3035, 1);
        Check("event 3035 finished (option 1): mission status 2", $"{KnightOfGold},2", Mission(LogToAU));
        Talk(Cacaroon);
        Check("Cacaroon: event 3036", $"034:{Cacaroon}:3036", Ev());
        EventEnd(3036);
        Trade(Cacaroon, (0, 999));
        Check("999 gil traded: no event", "none/none", $"{Ev()}/{Cur()}");
        Trade(Cacaroon, (0, 1000));
        Check("1000 gil traded: event 3022", $"034:{Cacaroon}:3022/3022", $"{Ev()}/{Cur()}");
        EventEnd(3022);
        Check("event 3022 finished: tradeComplete took the gil, mission status 3", $"9000/{KnightOfGold},3", $"{Gil()}/{Mission(LogToAU)}");
        Check("  chars.missions: the status word", $"{KnightOfGold}/3", DbMission(LogToAU));
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(80.0f, 0.0f, 40.0f);
        Tick(250);
        Check("trigger area 4 (the Walahra Temple): event 3024", $"034:{c.Id}:3024/3024", $"{Ev()}/{Cur()}");
        EventEnd(3024);
        Check("event 3024 finished: mission status 4", $"{KnightOfGold},4", Mission(LogToAU));
        Near(Nadeey);
        Talk(Nadeey);
        Check("Nadeey: event 3025", $"034:{Nadeey}:3025", Ev());
        EventEnd(3025);
        s.PacketList.Clear();
        MoveTo(80.0f, -6.0f, -130.0f);
        Tick(250);
        Check("trigger area 5 (the Shaharat Teahouse): event 3026", $"034:{c.Id}:3026/3026", $"{Ev()}/{Cur()}");
        string Topic(uint option)
        {
            EventEnd(3026, option, 1);
            var up = Of(0x05C);
            return up.Count == 0 ? "none" : $"{BitConverter.ToInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(0))},{BitConverter.ToInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(1))}";
        }
        Check("the update with option 10: updateEvent(topics 0b1000, 1) in a 0x05C", "8,1", Topic(10));
        Check("topics 1, 2, 3 picked: the bits gathered", "9,1/11,1/15,1", $"{Topic(1)}/{Topic(2)}/{Topic(3)}");
        int letter = Ki("RAILLEFALS_LETTER");
        EventEnd(3026);
        Check("event 3026 finished: Knight of Gold complete, Confessions of Royalty current", $"{ConfessionsOfRoyalty},0/{ConfessionsOfRoyalty}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, KnightOfGold)}");
        Check("  Raillefal's Letter", "10", DbKeyItem(letter));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 5 Confessions of Royalty: Guilerme in Northern San d'Oria, the Chateau");
        Warp(-4.5f, 0.0f, 97.0f, NorthernSandoria);
        // canEnterChateau: a char of another nation needs rank 3 or its nation's mission 5 on; with no nation mission
        // (0xFFFF) it may enter, so the char is a Bastokan of rank 1 on The Zeruhn Report
        Lua("player:addMission(xi.mission.log_id.BASTOK, xi.mission.id.bastok.THE_ZERUHN_REPORT)");
        Check("in Northern San d'Oria, a Bastokan of rank 1 on Bastok 1-1 (no way into the Chateau)", $"{NorthernSandoria}/1/1/0",
              $"{c.Zone}/{Lua("report(player:getNation())")}/{Lua("report(player:getRank(player:getNation()))")}/{Lua("report(player:getCurrentMission(xi.mission.log_id.BASTOK))")}");
        Near(Guilerme);
        Talk(Guilerme);
        Check("Guilerme: event 810", $"032:{Guilerme}:810/810", $"{Ev()}/{Cur()}");
        EventEnd(810, 1);
        Check("event 810 finished (option 1): Option 1", "1", Var($"Mission[{LogToAU}][{ConfessionsOfRoyalty}]Option"));
        Rezone(false);
        Check("setPos into Chateau d'Oraguille: the zone-in cutscene 563", $"{Chateau}/563", $"{c.Zone}/{Cur()}");
        EventEnd(563);
        Check("cutscene 563 finished: the letter taken, Confessions of Royalty complete, Easterly Winds current",
              $"00/{EasterlyWinds},0/{EasterlyWinds}/0/True",
              $"{DbKeyItem(letter)}/{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, ConfessionsOfRoyalty)}");
        Check("  the mission var cleared", "-", Var($"Mission[{LogToAU}][{ConfessionsOfRoyalty}]Option"));
        Rezone();
        Check("setPos out to Northern San d'Oria", $"{NorthernSandoria}/0/100", $"{c.Zone}/{c.X}/{c.Z}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 6 Easterly Winds: the palace entrance in Ru'Lude Gardens");
        Warp(0.0f, 3.0f, 20.0f, Rulude);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(0.0f, 3.0f, 50.0f);
        Tick(250);
        Check("trigger area 1: event 10094", $"032:{c.Id}:10094/10094", $"{Ev()}/{Cur()}");
        EventEnd(10094, 1);
        Check("event 10094 finished (option 1): 10 Imperial Bronze Pieces, ITEM_OBTAINED + 9 (the count)", $"10/{int.Parse(Text(Rulude, "ITEM_OBTAINED")) + 9}:{ImperialBronzePiece}",
              $"{DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND itemId = {ImperialBronzePiece}")}/{Special()}");
        Check("  Easterly Winds complete, Westerly Winds current", $"{WesterlyWinds},0/{WesterlyWinds}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, EasterlyWinds)}");
        Check("  0x056: Westerly Winds on the offer page, bits 0-5 on the complete page", $"{WesterlyWinds}/111111", ToauPages());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 7 Westerly Winds: the Shaharat Teahouse, Naja Salaheem");
        Warp(80.0f, -6.0f, -110.0f, Whitegate);
        int note = Ki("RAILLEFALS_NOTE"), titleAgent = TitleId("AGENT_OF_THE_ALLIED_FORCES");
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(80.0f, -6.0f, -130.0f);
        Tick(250);
        Check("trigger area 5: event 3027 with text table 0", $"034:{c.Id}:3027/0/3027", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3027);
        Check("event 3027 finished: Raillefal's Note, an Imperial Silver Piece, the Agent of the Allied Forces title, status 1",
              $"10/1/{titleAgent}/{WesterlyWinds},1", $"{DbKeyItem(note)}/{Qty(ImperialSilverPiece)}/{Lua("report(player:getTitle())")}/{Mission(LogToAU)}");
        Check("  KEYITEM_OBTAINED then ITEM_OBTAINED (the key item first, as retail)",
              $"{Text(Whitegate, "KEYITEM_OBTAINED")}:{note},{Text(Whitegate, "ITEM_OBTAINED")}:{ImperialSilverPiece}", Special());
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3028 with the mercenary rank", $"034:{NajaSalaheem}:3028:1,1/3028", $"{Ev()}/{Cur()}");
        EventEnd(3028, 2, 1);
        var upd = Of(0x05C);
        Check("the update with option 2 (the prince asked about): updateEvent(0, 1)", "1/0,1",
              $"{upd.Count}/{(upd.Count == 0 ? "" : $"{BitConverter.ToInt32(upd[^1].Buffer, 4 + S2cPendingnum.NumOffset(0))},{BitConverter.ToInt32(upd[^1].Buffer, 4 + S2cPendingnum.NumOffset(1))}")}");
        EventEnd(3028);
        Check("event 3028 finished: Westerly Winds complete, A Mercenary Life current, the note taken, the reward's silver piece",
              $"{AMercenaryLife},0/{AMercenaryLife}/0/True/00/2", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, WesterlyWinds)}/{DbKeyItem(note)}/{Qty(ImperialSilverPiece)}");
        Check("  mustZone for A Mercenary Life", "true", Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {AMercenaryLife})))"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 8 A Mercenary Life: Naja's dialog, a zone out, the Sentinels' platform");
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3029, dialog 2", $"034:{NajaSalaheem}:3029:1,1,0,0,0,0,0,2", Ev());
        EventEnd(3029);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.5f, -56.0f);
        Tick(250);
        Check("trigger area 3 before zoning: nothing", "none", Ev());
        TakeZoneLine("z1e0");
        TakeZoneLine(LineTo(Whitegate));
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.5f, -56.0f);
        Tick(250);
        Check("trigger area 3 after zoning: event 3050", $"034:{c.Id}:3050:0,1/3050", $"{Ev()}/{Cur()}");
        string Dialog(uint option)
        {
            EventEnd(3050, option, 1);
            var up = Of(0x05C);
            return up.Count == 0 ? "none" : $"{BitConverter.ToInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(0))}";
        }
        Check("topics 1 then 2: the dialog bits gathered", "1/3", $"{Dialog(1)}/{Dialog(2)}");
        EventEnd(3050);
        Check("event 3050 finished: A Mercenary Life complete, Undersea Scouting current", $"{UnderseaScouting},0/{UnderseaScouting}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 9 Undersea Scouting: the Alzadaal Undersea Ruins");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3051, dialog 1", $"034:{NajaSalaheem}:3051:1,1,0,0,0,0,0,1", Ev());
        EventEnd(3051);
        // the way down through Nashmau / Arrapago: a GM !pos to map 7
        Warp(440.0f, 0.0f, -530.0f, Alzadaal);
        Check("in the Alzadaal Undersea Ruins", Alzadaal, c.Zone);
        int compass = Ki("ASTRAL_COMPASS");
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(440.0f, 0.0f, -580.0f);
        Tick(250);
        Check("trigger area 23: event 1 with the mercenary rank", $"034:{c.Id}:1:1/1", $"{Ev()}/{Cur()}");
        EventEnd(1);
        Check("event 1 finished: Undersea Scouting complete, Astral Waves current, the Astral Compass",
              $"{AstralWaves},0/{AstralWaves}/0/10", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbKeyItem(compass)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 10 Astral Waves: Naja Salaheem");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3052", $"034:{NajaSalaheem}:3052:1,1/3052", $"{Ev()}/{Cur()}");
        EventEnd(3052, 1, 1);
        Check("the update with option 1: the mercenary rank", "1", Of(0x05C).Count == 0 ? "none" : BitConverter.ToInt32(Of(0x05C)[^1].Buffer, 4 + S2cPendingnum.NumOffset(0)).ToString());
        EventEnd(3052);
        int today = int.Parse(Lua("report(VanadielUniqueDay())"));
        Check("event 3052 finished: Astral Waves complete, Imperial Schemes current, its Timer tomorrow, mustZone",
              $"{ImperialSchemes},0/{ImperialSchemes}/0/{today + 1}/true",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Var($"Mission[{LogToAU}][{ImperialSchemes}]Timer")}/" +
              Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {ImperialSchemes})))"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 11 Imperial Schemes: the Sentinels' first platform, after a zone and a day");
        TakeZoneLine("z1e0");
        TakeZoneLine(LineTo(Whitegate));
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.6f, -55.0f);
        Tick(250);
        Check("trigger area 6 the same Vana'diel day: nothing", "none", Ev());
        vanaTicks += VanaTime.DAY;
        MoveTo(35.0f, -2.0f, -80.0f);
        Tick(250);
        MoveTo(35.0f, -6.6f, -55.0f);
        Tick(250);
        Check("trigger area 6 the next day: event 3070 with text table 0", $"034:{c.Id}:3070/0/3070", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3070, 2, 1);
        Check("the update with option 2: updateEvent(.., 4) in the eighth param", "4",
              Of(0x05C).Count == 0 ? "none" : BitConverter.ToInt32(Of(0x05C)[^1].Buffer, 4 + S2cPendingnum.NumOffset(7)).ToString());
        EventEnd(3070);
        Check("event 3070 finished: Imperial Schemes complete, Royal Puppeteer current, the vars cleared", $"{RoyalPuppeteer},0/{RoyalPuppeteer}/0/-",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Var($"Mission[{LogToAU}][{ImperialSchemes}]Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 12 Royal Puppeteer: Pyopyoroon in Nashmau, Jody's Acid");
        Warp(20.0f, 0.0f, 24.0f, Nashmau);
        Near(Pyopyoroon);
        Talk(Pyopyoroon);
        Check("Pyopyoroon: event 277", $"032:{Pyopyoroon}:277/277", $"{Ev()}/{Cur()}");
        EventEnd(277);
        Check("event 277 finished: mission status 1", $"{RoyalPuppeteer},1", Mission(LogToAU));
        Talk(Pyopyoroon);
        Check("Pyopyoroon again: event 278", $"032:{Pyopyoroon}:278", Ev());
        EventEnd(278);
        Lua($"player:addItem({JodysAcid})");
        byte acidSlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == JodysAcid).Slot;
        Trade(Pyopyoroon, (acidSlot, 1));
        int scent = Ki("VIAL_OF_SPECTRAL_SCENT");
        Check("Jody's Acid traded: event 279", $"032:{Pyopyoroon}:279/279", $"{Ev()}/{Cur()}");
        EventEnd(279);
        Check("event 279 finished: Royal Puppeteer complete, Lost Kingdom current, the acid gone, the Vial of Spectral Scent",
              $"{LostKingdom},0/{LostKingdom}/0/0/10", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Qty(JodysAcid)}/{DbKeyItem(scent)}");
        Check("  0x056: Lost Kingdom on the offer page, bits 0-11 on the complete page", $"{LostKingdom}/111111111111", ToauPages());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        // ---- Abyssea ----
        Console.WriteLine("Abyssea: the login in Port Jeuno");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[2];
        Fixture(db, PortJeuno, PortJeuno, -50.0f, 0.0f, -9.0f, 1);
        Login(false);
        string Quest(string name) => Lua($"report(player:getQuestStatus(xi.questLog.ABYSSEA, xi.quest.id.abyssea.{name}))");
        string QVar(string name, string v) => Var($"Quest[{Lua("report(xi.questLog.ABYSSEA)")}][{Lua($"report(xi.quest.id.abyssea.{name})")}]{v}");
        Check("xi.player.onGameIn: A Journey Begins flagged (ENABLE_ABYSSEA)", "1", Quest("A_JOURNEY_BEGINS"));
        // 0x00A runs luautils::OnZoneIn before OnGameIn: the quest's cutscene waits for the next zone-in
        Check("  no zone-in cutscene on this login", "none", Cur());
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));
        TakeZoneLine(LineTo(LowerJeuno));
        TakeZoneLine(LineTo(PortJeuno), false);
        Check("back in Port Jeuno: A Journey Begins' zone-in cutscene 324", $"{PortJeuno}/324", $"{c.Zone}/{Cur()}");
        EventEnd(324);
        Check("cutscene 324 finished: Prog 1", "1", QVar("A_JOURNEY_BEGINS", "Prog"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea A Journey Begins: Joachim");
        int stone1 = Ki("TRAVERSER_STONE1");
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: event 325", $"032:{Joachim}:325/325", $"{Ev()}/{Cur()}");
        EventEnd(325);
        Check("event 325 finished: A Journey Begins complete, The Truth Beckons accepted, the first Traverser Stone", "2/1/10",
              $"{Quest("A_JOURNEY_BEGINS")}/{Quest("THE_TRUTH_BECKONS")}/{DbKeyItem(stone1)}");
        Check("  setTraverserEpoch: the epoch set", "True", long.Parse(Lua("report(player:getTraverserEpoch())")) > 0);
        Talk(Joachim);
        Check("Joachim: The Truth Beckons' event 326", $"032:{Joachim}:326", Ev());
        EventEnd(326);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea The Truth Beckons: the Cavernous Maw in Konschtat Highlands");
        Warp(93.0f, -69.0f, -580.0f, Konschtat);
        Near(MawKonschtat);
        Talk(MawKonschtat);
        Check("the Cavernous Maw: event 107 (0, 1)", $"034:{MawKonschtat}:107:0,1/107", $"{Ev()}/{Cur()}");
        EventEnd(107, 1);
        Rezone();
        Check("event 107 finished (option 1): Abyssea - Konschtat, by the Searing Ward", $"{AbysseaKonschtat}/153/-840", $"{c.Zone}/{c.X}/{c.Z}");
        Check("  The Truth Beckons' onZoneIn: Prog 1", "1", QVar("THE_TRUTH_BECKONS", "Prog"));
        // afterZoneIn runs 4 s after the zone-in
        Tick(4500);
        Check("  xi.abyssea.afterZoneIn: 5 minutes of hidden Visitant time (icon 0)", "True/0",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.VISITANT)))") == "true"}/{Lua("report(player:getStatusEffect(xi.effect.VISITANT):getIcon())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea The Truth Beckons: back to Joachim");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        string cap = Lua("report(xi.abyssea.getTraverserCap(player))");
        Check("Joachim: event 327 with the traverser cap", $"034:{Joachim}:327:0,0,{cap}/327", $"{Ev()}/{Cur()}");
        EventEnd(327);
        Check("event 327 finished: The Truth Beckons complete, Dawn of Death accepted", "2/1", $"{Quest("THE_TRUTH_BECKONS")}/{Quest("DAWN_OF_DEATH")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // a zone quest: the Cavernous Maw's offer, its warp into Abyssea, the NM (a GM !spawnmob, the quest's header)
        // killed by the char, the way back through the maw, and the zone-in cutscene with the zone's key item
        void ZoneQuest(string quest, string title, ushort zoneId, uint maw, int offerEv, int warpEv, ushort abysseaId, uint nmId, int rewardEv)
        {
            Console.WriteLine($"Abyssea {quest}: the Cavernous Maw's offer");
            var mawNpc = zone.ZoneInfoOf(zoneId).NpcById[maw];
            Warp(mawNpc.X + 1.5f, mawNpc.Y, mawNpc.Z, zoneId);
            Talk(maw);
            Check($"the Cavernous Maw: the quest's event {offerEv}", $"032:{maw}:{offerEv}/{offerEv}", $"{Ev()}/{Cur()}");
            EventEnd(offerEv);
            Check("  accepted", "1", Quest(quest));
            Talk(maw);
            Check($"the maw again: its warp, event {warpEv}", $"034:{maw}:{warpEv}:0,1", Ev());
            EventEnd(warpEv, 1);
            Rezone();
            Check("  into Abyssea", abysseaId, c.Zone);
            var nm = zone.ZoneInfoOf(abysseaId).MobById[nmId];
            Lua($"SpawnMob({nmId})");
            Check("  the NM spawned", "True", nm.IsSpawned && nm.IsAlive);
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(nm.X - 2.0f).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(nm.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(nm.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = 0;
            });
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(nm.Id).CopyTo(b, 4);
                BitConverter.GetBytes(nm.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
            });
            // a level 90 NM outlasts a WAR 75: claimed by the char's first swing (CBattleEntity::OnAttack's ClaimMob),
            // then finished with one blow from the char
            // the client keeps the char facing its target as the NM moves (else "unable to see")
            for (int i = 0; nm.OwnerId != c.Id && i < 30; i++)
            {
                float dx = nm.X - c.X, dz = nm.Z - c.Z;
                byte dir = (byte)(short)(MathF.Atan2(dz, dx) * -(128.0f / MathF.PI));
                Send(0x015, 32, b =>
                {
                    BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
                    BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                    BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
                    b[C2sPos.OffDir] = dir;
                });
                Tick(1000);
            }
            Check("  claimed by the char's swing", $"{c.Id}", nm.OwnerId);
            Lua($"local mob = GetMobByID({nmId}) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            // the death on the next tick, the defeat (DistributeRewards: onMobDeath) a second on
            Tick(1000);
            Tick(1000);
            int titleId = TitleId(title);
            Check($"  the NM killed by the char: its onMobDeath gave the {title} title", "True/True", $"{nm.IsDead}/{DbTitle(titleId)}");
            // the walk back to the Searing Ward: a GM !pos by the exit maw, then the maw's event 200 out
            var exitMaw = zone.ZoneInfoOf(abysseaId).Npcs.First(n => n.Name == "Cavernous_Maw");
            Lua($"player:setPos({exitMaw.X + 1.5f}, {exitMaw.Y}, {exitMaw.Z}, 0)");
            Talk(exitMaw.Id);
            Check("  the exit maw: event 200", $"032:{exitMaw.Id}:200/200", $"{Ev()}/{Cur()}");
            EventEnd(200, 1);
            Rezone(false);
            int zoneKi = int.Parse(Lua("report(xi.abyssea.getZoneKIReward(player))"));
            Check($"event 200 finished (option 1): back out by the maw, the quest's zone-in cutscene {rewardEv}", $"{zoneId}/{rewardEv}", $"{c.Zone}/{Cur()}");
            EventEnd(rewardEv, 1, 1);
            var up = Of(0x05C);
            Check("  the update (option 1): updateEvent(the zone's key item)", $"{zoneKi}",
                  up.Count == 0 ? "none" : BitConverter.ToInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(0)).ToString());
            EventEnd(rewardEv);
            Check("  finished: the key item, the quest complete", $"10/2", $"{DbKeyItem(zoneKi)}/{Quest(quest)}");
            Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        }
        ZoneQuest("A_GOLDSTRUCK_GIGAS", "BRIAREUS_FELLER", LaTheine, MawLaTheine, 9, 218, AbysseaLaTheine, Briareus, 10);
        ZoneQuest("TO_PASTE_A_PEISTE", "KUKULKAN_DEFANGER", Konschtat, MawKonschtat, 0, 107, AbysseaKonschtat, Kukulkan, 1);
        ZoneQuest("MEGADRILE_MENACE", "GLAVOID_STAMPEDER", Tahrongi, MawTahrongi, 38, 100, AbysseaTahrongi, Glavoid, 39);

        Console.WriteLine("Abyssea Dawn of Death: three zone quests done, Joachim");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: event 333", $"032:{Joachim}:333/333", $"{Ev()}/{Cur()}");
        EventEnd(333);
        Check("event 333 finished: Dawn of Death complete, First Contact accepted", "2/1", $"{Quest("DAWN_OF_DEATH")}/{Quest("FIRST_CONTACT")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Chains of Promathia, chapters 3 and 4 ----
        Console.WriteLine("CoP 3-1 The Call of the Wyrmking: the login in Port Bastok, South Gustaberg's zone-in");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[3];
        // the scripts' earth clock (GetSystemTime) on the host's test clock from here on
        long earth0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), earthAt = now;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(earth0 + (now - earthAt) / 1000)); return 1; });
        Fixture(db, PortBastok, PortBastok, -126.0f, -6.0f, 10.0f, 0);
        Login();
        // the chapters before: !addmission 6 258 (3_1_The_Call_of_the_Wyrmking.lua's header)
        expectedWarnings.Add($"lua: addMission: overwriting current mission 101 of log 6 with {TheCallOfTheWyrmking}");
        Lua($"player:addMission({LogCoP}, {TheCallOfTheWyrmking})");
        Check("a San d'Orian in Port Bastok on The Call of the Wyrmking", $"{PortBastok}/0/{TheCallOfTheWyrmking},0",
              $"{c.Zone}/{Lua("report(player:getNation())")}/{Mission(LogCoP)}");
        Lua("player:setPos(694, -5.5, -619, 74, xi.zone.SOUTH_GUSTABERG)");
        Rezone(false);
        Check("in South Gustaberg: the mission's zone-in cutscene 906", $"{SouthGustaberg}/906", $"{c.Zone}/{Cur()}");
        EventEnd(906);
        Check("cutscene 906 finished: Status 1", "1", Var($"Mission[{LogCoP}][{TheCallOfTheWyrmking}]Status"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-1: Port Bastok's trigger area, Cid in the Metalworks");
        Warp(-104.0f, 0.0f, 10.0f, PortBastok);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(-104.0f, 0.0f, -10.0f);
        Tick(250);
        Check("trigger area 1 in Port Bastok: event 305", $"032:{c.Id}:305/305", $"{Ev()}/{Cur()}");
        EventEnd(305);
        Check("event 305 finished: Status 2", "2", Var($"Mission[{LogCoP}][{TheCallOfTheWyrmking}]Status"));
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        uint cid = NpcId("Cid");
        Near(cid);
        Talk(cid);
        Check("Cid: event 845", $"032:{cid}:845/845", $"{Ev()}/{Cur()}");
        EventEnd(845);
        // CoP's completion is its order (hasCompletedMission), not the complete flags (completeMission skips CoP's)
        Check("event 845 finished: The Call of the Wyrmking complete, A Vessel Without a Captain current",
              $"{AVesselWithoutACaptain},0/{AVesselWithoutACaptain}/0/true/False",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{Lua($"report(tostring(player:hasCompletedMission({LogCoP}, {TheCallOfTheWyrmking})))")}/" +
              $"{DbMissionComplete(LogCoP, TheCallOfTheWyrmking % 64)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-2 A Vessel Without a Captain: Cid, Neptune's Spire, Harnek, Ru'Lude Gardens");
        Talk(cid);
        Check("Cid: event 861", $"032:{cid}:861", Ev());
        EventEnd(861);
        Talk(cid);
        Check("Cid again in the same zone: not 861 (oncePerZone)", "False", Ev().EndsWith(":861"));
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Warp(35.0f, 0.0f, -13.0f, LowerJeuno);
        uint spireDoor = NpcId("_6tc"), harnek = NpcId("Harnek");
        Near(spireDoor);
        Talk(spireDoor);
        Check("the door of Neptune's Spire (_6tc): event 86", $"032:{spireDoor}:86/86", $"{Ev()}/{Cur()}");
        EventEnd(86);
        Check("event 86 finished: Status 1", "1", Var($"Mission[{LogCoP}][{AVesselWithoutACaptain}]Status"));
        Near(harnek);
        Talk(harnek);
        Check("Harnek: event 12", $"032:{harnek}:12", Ev());
        EventEnd(12);
        Warp(0.0f, 3.0f, 20.0f, Rulude);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(0.0f, 3.0f, 50.0f);
        Tick(250);
        // the nation a number (0): a 0x034 with its params zeroed; the Shadow Lord flag a boolean, not taken
        Check("trigger area 1 in Ru'Lude Gardens: event 65 (the nation, Shadow Lord not beaten)", $"034:{c.Id}:65/65", $"{Ev()}/{Cur()}");
        EventEnd(65);
        Check("event 65 finished: A Vessel Without a Captain complete, The Road Forks current", $"{TheRoadForks},0/{TheRoadForks}/0",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // The Road Forks' extended mission status: a nibble per path (xi.mission.status.COP)
        string PathOf(int pos) => Lua($"report(player:getMissionStatus({LogCoP}, {pos}))");
        Console.WriteLine("CoP 3-3 The Road Forks, the San d'Oria path: Northern San d'Oria");
        WarpIn(148.0f, 0.0f, 135.0f, NorthernSandoria);
        Check("in Northern San d'Oria: the path's zone-in cutscene 14", $"{NorthernSandoria}/14", $"{c.Zone}/{Cur()}");
        EventEnd(14);
        Check("cutscene 14 finished: the San d'Oria nibble 1 (setMissionStatus's position 0), the status word 1", "1/1",
              $"{PathOf(0)}/{Mission(LogCoP).Split(',')[1]}");
        Check("  chars.missions: the status word", $"{TheRoadForks}/1", DbMission(LogCoP));
        uint arnau = NpcId("Arnau"), chasalvige = NpcId("Chasalvige");
        Near(arnau);
        Talk(arnau);
        Check("Arnau: event 51", $"032:{arnau}:51/51", $"{Ev()}/{Cur()}");
        EventEnd(51);
        Near(chasalvige);
        Talk(chasalvige);
        Check("Chasalvige: event 38", $"032:{chasalvige}:38/38", $"{Ev()}/{Cur()}");
        EventEnd(38);
        Check("event 38 finished: the San d'Oria nibble 5", "5", PathOf(0));
        Talk(chasalvige);
        Check("Chasalvige again: event 6", $"032:{chasalvige}:6", Ev());
        EventEnd(6);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-3, the San d'Oria path: Guilloud and the Overgrown Ivy in Carpenters' Landing");
        Warp(-123.0f, -6.6f, -466.0f, CarpentersLanding);
        uint guilloud = NpcId("Guilloud"), ivyId = uint.Parse(Lua("report(zones[xi.zone.CARPENTERS_LANDING].mob.OVERGROWN_IVY)"));
        var ivy = zone.ZoneInfoOf(CarpentersLanding).MobById[ivyId];
        Near(guilloud);
        Talk(guilloud);
        Check("Guilloud: YOU_WISH_TO_KNOW_MISTALLE and SQUASH_ANOTHER_WORM (messageText), no event", "none/2",
              $"{Ev()}/{Of(0x036).Count}");
        Check("  the Overgrown Ivy spawned, claimed by the char (updateClaim)", $"True/{c.Id}", $"{ivy.IsSpawned && ivy.IsAlive}/{ivy.OwnerId}");
        Talk(guilloud);
        Check("Guilloud with the Ivy up: BEGONE_TRESPASSER (messageName: showText, a 0x02A from him)", $"none/{Text(CarpentersLanding, "BEGONE_TRESPASSER")}:0",
              $"{Ev()}/{Special()}");
        KillByChar(ivyId);
        Check("the Ivy killed by the char: its onMobDeath set ivyDefeated", "True/1",
              $"{ivy.IsDead}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{TheRoadForks}]ivyDefeated")}");
        Talk(guilloud);
        Check("Guilloud: cutscene 0 (a San d'Orian: the first param 1)", $"034:{guilloud}:0:1/0/{EventType.Cutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(0);
        Check("cutscene 0 finished: the San d'Oria nibble 9", "9", PathOf(0));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-3, the San d'Oria path: Hinaree in Southern San d'Oria");
        Warp(-301.0f, -10.2f, 100.0f, SouthernSandoria);
        uint hinaree = NpcId("Hinaree");
        Near(hinaree);
        Talk(hinaree);
        Check("Hinaree: event 23", $"032:{hinaree}:23/23", $"{Ev()}/{Cur()}");
        EventEnd(23);
        Check("event 23 finished: the San d'Oria nibble 14, the path done", "14", PathOf(0));
        Talk(hinaree);
        Check("Hinaree again: event 24", $"032:{hinaree}:24", Ev());
        EventEnd(24);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-3, the Windurst path: Windurst Waters and Walls");
        WarpIn(23.0f, -5.0f, -190.0f, WindurstWaters);
        Check("in Windurst Waters: the path's zone-in cutscene 871", $"{WindurstWaters}/871", $"{c.Zone}/{Cur()}");
        EventEnd(871);
        Check("cutscene 871 finished: the Windurst nibble 1, the San d'Oria nibble kept", "1/14", $"{PathOf(1)}/{PathOf(0)}");
        // the Windurst path's NPCs, in its order: (zone, NPC, the event, the nibble after)
        void PathNpc(ushort zoneId, string name, int ev, int nibble, float x = float.NaN, float y = 0, float z = 0)
        {
            if (c.Zone != zoneId) Warp(x, y, z, zoneId);
            uint id = NpcId(name);
            Near(id);
            Talk(id);
            Check($"{name}: event {ev}", $"{id}:{ev}/{ev}", $"{EvId()}/{Cur()}");
            EventEnd(ev);
            Check($"  event {ev} finished: the Windurst nibble {nibble}", $"{nibble}", PathOf(1));
        }
        PathNpc(WindurstWaters, "Ohbiru-Dohbiru", 872, 2);
        PathNpc(WindurstWalls, "Yoran-Oran", 469, 3, -109.0f, -14.0f, 200.0f);
        PathNpc(WindurstWaters, "Kyume-Romeh", 873, 5, -58.0f, -4.0f, 20.0f);
        int mirror = Ki("CRACKED_MIMEO_MIRROR"), jewel = Ki("MIMEO_JEWEL"), feather = Ki("MIMEO_FEATHER"), feather2 = Ki("SECOND_MIMEO_FEATHER"), feather3 = Ki("THIRD_MIMEO_FEATHER");
        PathNpc(WindurstWaters, "Honoi-Gomoi", 874, 6);
        Check("  the Cracked Mimeo Mirror", "10", DbKeyItem(mirror));
        PathNpc(WindurstWalls, "Yoran-Oran", 470, 8, -109.0f, -14.0f, 200.0f);
        Check("  the mirror taken", "00", DbKeyItem(mirror));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-3, the Windurst path: the Loose Sand, Lioumere and the Cradle of Rebirth in Attohwa Chasm");
        Warp(478.0f, 20.0f, 45.0f, AttohwaChasm);
        uint looseSand = NpcId("Loose_Sand"), cradle = NpcId("Cradle_of_Rebirth");
        uint lioumereId = uint.Parse(Lua("report(zones[xi.zone.ATTOHWA_CHASM].mob.LIOUMERE)"));
        var lioumere = zone.ZoneInfoOf(AttohwaChasm).MobById[lioumereId];
        Near(looseSand);
        Talk(looseSand);
        Check("the Loose Sand from 1.5 away: MUST_MOVE_CLOSER", $"none/{Text(AttohwaChasm, "MUST_MOVE_CLOSER")}:0", $"{Ev()}/{Special()}");
        var sand = Npc(looseSand);
        MoveTo(sand.X, sand.Y, sand.Z);
        Talk(looseSand);
        Check("the Loose Sand underfoot: Lioumere spawned and claimed, no event", $"none/True/{c.Id}", $"{Ev()}/{lioumere.IsSpawned && lioumere.IsAlive}/{lioumere.OwnerId}");
        KillByChar(lioumereId);
        Check("Lioumere killed by the char: Status 1", "True/1", $"{lioumere.IsDead}/{Var($"Mission[{LogCoP}][{TheRoadForks}]Status")}");
        MoveTo(sand.X, sand.Y, sand.Z);
        Talk(looseSand);
        Check("the Loose Sand again: the Mimeo Jewel, KEYITEM_OBTAINED", $"10/{Text(AttohwaChasm, "KEYITEM_OBTAINED")}:{jewel}", $"{DbKeyItem(jewel)}/{Special().Split(',')[0]}");
        // jewelTimer's first pass (30 minutes left): the offset - 1 message, then every 3 s (player:timer) a check
        Check("  jewelTimer: the first fade message, Option 1", $"{int.Parse(Text(AttohwaChasm, "MIMEO_JEWEL_OFFSET")) - 1}:{jewel}/1",
              $"{Special().Split(',').Last()}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{TheRoadForks}]Option")}");
        s.PacketList.Clear();
        now += 7 * 60 * 1000;
        for (int i = 0; i < 3; i++) Tick(1000);
        Check("  seven minutes on (the timer's next pass): the second fade message, Option 2",
              $"{Text(AttohwaChasm, "MIMEO_JEWEL_OFFSET")}:{jewel}/2", $"{Special()}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{TheRoadForks}]Option")}");
        Near(cradle);
        Talk(cradle);
        Check("the Cradle of Rebirth with the jewel: event 2", $"032:{cradle}:2/2", $"{Ev()}/{Cur()}");
        EventEnd(2);
        Check("event 2 finished: the jewel gone, the three Mimeo Feathers", "00/10/10/10",
              $"{DbKeyItem(jewel)}/{DbKeyItem(feather)}/{DbKeyItem(feather2)}/{DbKeyItem(feather3)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-3, the Windurst path: Yoran-Oran, Yujuju, Tosuka-Porika");
        PathNpc(WindurstWalls, "Yoran-Oran", 471, 9, -109.0f, -14.0f, 200.0f);
        Check("  the feathers taken", "00/00/00", $"{DbKeyItem(feather)}/{DbKeyItem(feather2)}/{DbKeyItem(feather3)}");
        PathNpc(PortWindurst, "Yujuju", 592, 11, 201.0f, -4.8f, 136.0f);
        PathNpc(WindurstWaters, "Tosuka-Porika", 875, 12, -26.0f, -6.0f, 100.0f);
        PathNpc(WindurstWalls, "Yoran-Oran", 472, 14, -109.0f, -14.0f, 200.0f);
        Check("both paths done: the status word 0xEE", $"{0xEE}", Mission(LogCoP).Split(',')[1]);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-3: Cid in the Metalworks");
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 847", $"032:{cid}:847/847", $"{Ev()}/{Cur()}");
        EventEnd(847);
        // Mission:complete keeps a CoP status word (the paths' nibbles)
        Check("event 847 finished: The Road Forks complete, Tending Aged Wounds current, the status word kept",
              $"{TendingAgedWounds},{0xEE}/{TendingAgedWounds}/{0xEE}", $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-4 Tending Aged Wounds: Cid, Lower Jeuno's zone-in, Neptune's Spire");
        Talk(cid);
        Check("Cid: event 862", $"032:{cid}:862", Ev());
        EventEnd(862);
        WarpIn(35.0f, 0.0f, -13.0f, LowerJeuno);
        Check("in Lower Jeuno: the mission's zone-in cutscene 70", $"{LowerJeuno}/70", $"{c.Zone}/{Cur()}");
        EventEnd(70);
        Check("cutscene 70 finished: Status 1", "1", Var($"Mission[{LogCoP}][{TendingAgedWounds}]Status"));
        Near(spireDoor);
        Talk(spireDoor);
        Check("the door of Neptune's Spire: event 22", $"032:{spireDoor}:22/22", $"{Ev()}/{Cur()}");
        EventEnd(22);
        Check("event 22 finished: Tending Aged Wounds complete, Darkness Named current, the mission var cleared",
              $"{DarknessNamed},{0xEE}/{DarknessNamed}/{0xEE}/-", $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{Var($"Mission[{LogCoP}][{TendingAgedWounds}]Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-5 Darkness Named: Jeuno's people, Monberaux and the amulet, Ghebi Damomohe's chip trade");
        uint aldo = NpcId("Aldo"), ghebi = NpcId("Ghebi_Damomohe"), sattal = NpcId("Sattal-Mansal");
        foreach (var (npc, ev) in new[] { (aldo, 7), (harnek, 13), (sattal, 11) })
        {
            Near(npc);
            Talk(npc);
            Check($"{Npc(npc).Name}: event {ev}", $"{npc}:{ev}", EvId());
            EventEnd(ev);
        }
        Lua("player:addKeyItem(xi.keyItem.MYSTERIOUS_AMULET)");
        int amuletKi = Ki("MYSTERIOUS_AMULET"), pass = Ki("PSOXJA_PASS");
        Warp(-42.0f, 0.0f, -4.0f, UpperJeuno);
        Near(Monberaux);
        Talk(Monberaux);
        Check("Monberaux: event 82", $"032:{Monberaux}:82/82", $"{Ev()}/{Cur()}");
        EventEnd(82);
        Check("event 82 finished: the amulet lent to Prishe (LEND_PRISHE_AMULET), Status 1",
              $"00/{Text(UpperJeuno, "LEND_PRISHE_AMULET")}:{amuletKi}/1", $"{DbKeyItem(amuletKi)}/{Special()}/{Var($"Mission[{LogCoP}][{DarknessNamed}]Status")}");
        Talk(Monberaux);
        Check("Monberaux again: event 3 (importantOnce)", $"032:{Monberaux}:3", Ev());
        EventEnd(3);
        Warp(-20.0f, 0.0f, -60.0f, LowerJeuno);
        Near(ghebi);
        Talk(ghebi);
        Check("Ghebi Damomohe: event 54", $"032:{ghebi}:54/54", $"{Ev()}/{Cur()}");
        EventEnd(54);
        Check("event 54 finished: Status 2", "2", Var($"Mission[{LogCoP}][{DarknessNamed}]Status"));
        Talk(ghebi);
        Check("Ghebi again: event 53", $"032:{ghebi}:53", Ev());
        EventEnd(53);
        Lua("player:addItem(xi.item.CARMINE_CHIP)");
        byte chipSlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == CarmineChip).Slot;
        Trade(ghebi, (chipSlot, 1));
        Check("a Carmine Chip traded: event 52 with the gil (GIL_RATE * 500)", $"034:{ghebi}:52:500/52", $"{Ev()}/{Cur()}");
        EventEnd(52);
        Check("event 52 finished: the chip taken, 500 gil, the Pso'Xja Pass, Status 3", $"0/10500/10/3",
              $"{Qty(CarmineChip)}/{Gil()}/{DbKeyItem(pass)}/{Var($"Mission[{LogCoP}][{DarknessNamed}]Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-5: The Shrouded Maw, Darkness Named won");
        // Pso'Xja's gates and the Shrouded Maw's way in: a GM !pos
        WarpIn(-300.0f, -1.5f, -220.0f, ShroudedMaw);
        Check("in the Shrouded Maw: the mission's zone-in cutscene 2", $"{ShroudedMaw}/2", $"{c.Zone}/{Cur()}");
        EventEnd(2);
        Check("cutscene 2 finished: Status 4 (the battlefield's requiredValue)", "4", Var($"Mission[{LogCoP}][{DarknessNamed}]Status"));
        int titleDreambreaker = TitleId("DREAMBREAKER"), titleTransient = TitleId("TRANSIENT_DREAMER");
        WinBattlefield(NpcId("MC_Entrance"), "Darkness Named");
        Check("  the win: the mission's 32001 section (battlefieldWin) set Status 5", "5", Var($"Mission[{LogCoP}][{DarknessNamed}]Status"));
        Check("  the titles: Transient Dreamer, Dreambreaker shown", $"True/True/{titleDreambreaker}",
              $"{DbTitle(titleTransient)}/{DbTitle(titleDreambreaker)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 3-5: Monberaux");
        Warp(-42.0f, 0.0f, -4.0f, UpperJeuno);
        Near(Monberaux);
        Talk(Monberaux);
        Check("Monberaux: event 75", $"032:{Monberaux}:75/75", $"{Ev()}/{Cur()}");
        EventEnd(75);
        Check("event 75 finished: Darkness Named complete, Sheltering Doubt current", $"{ShelteringDoubt},{0xEE}/{ShelteringDoubt}/{0xEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 4-1 Sheltering Doubt: the Tavnazian Safehold, the Dilapidated Gate");
        WarpIn(76.0f, -34.0f, 68.0f, TavnazianSafehold);
        Check("in the Tavnazian Safehold: the zone-in cutscene 107", $"{TavnazianSafehold}/107", $"{c.Zone}/{Cur()}");
        EventEnd(107);
        Check("cutscene 107 finished: Status 1", "1", Var($"Mission[{LogCoP}][{ShelteringDoubt}]Status"));
        Check("  Darkness Named's after-completion section: Justinius' default replaced (event 129)", "True",
              Lua($"report(tostring(player:hasCompletedMission({LogCoP}, {DarknessNamed})))") == "true");
        Near(Despachiaire);
        Talk(Despachiaire);
        Check("Despachiaire: event 108", $"032:{Despachiaire}:108/108", $"{Ev()}/{Cur()}");
        EventEnd(108);
        Near(Justinius);
        Talk(Justinius);
        Check("Justinius: event 109", $"032:{Justinius}:109/109", $"{Ev()}/{Cur()}");
        EventEnd(109);
        Check("event 109 finished: Status 3", "3", Var($"Mission[{LogCoP}][{ShelteringDoubt}]Status"));
        // two replaceDefault actions of one priority (Darkness Named's after-completion 129, this mission's 266): the
        // interaction framework cycles them in container order (scripts/missions sorted, 3-5 first)
        Talk(Justinius);
        Check("Justinius again: Darkness Named's event 129 first", $"032:{Justinius}:129", Ev());
        EventEnd(129);
        Talk(Justinius);
        Check("and again: this mission's event 266", $"032:{Justinius}:266", Ev());
        EventEnd(266);
        Warp(258.0f, 7.3f, -438.0f, Misareaux);
        Near(DilapidatedGateEast);
        Talk(DilapidatedGateEast);
        Check("the Dilapidated Gate (_0p0): cutscene 7", $"032:{DilapidatedGateEast}:7/7/{EventType.Cutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(7);
        Check("cutscene 7 finished: Sheltering Doubt complete, The Savage current", $"{TheSavage},{0xEE}/{TheSavage}/{0xEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 4-2 The Savage: the Dilapidated Gate to Riverne - Site #B01, Monarch Linn");
        Warp(-258.0f, -32.7f, 278.0f, Misareaux);
        Near(DilapidatedGateWest);
        Talk(DilapidatedGateWest);
        Check("the Dilapidated Gate (_0p2): cutscene 8", $"032:{DilapidatedGateWest}:8/8/{EventType.Cutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(8, 1);
        Check("cutscene 8 finished (option 1): Status 1", "1", Var($"Mission[{LogCoP}][{TheSavage}]Status"));
        Rezone();
        Check("  setPos into Riverne - Site #B01", $"{RiverneB01}/730/407", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        // the Site's spatial displacements down to Monarch Linn: a GM !pos (prevZone Site #B01, the battlefield's
        // entryRequirement)
        Warp(12.5f, 0.3f, -539.6f, MonarchLinn);
        Check("in Monarch Linn from Site #B01", $"{MonarchLinn}/{RiverneB01}", $"{c.Zone}/{c.PrevZone}");
        int titleMist = TitleId("MIST_MELTER"), titleUnderling = TitleId("NAGMOLADAS_UNDERLING");
        WinBattlefield(SdEntrance, "The Savage");
        Check("  the mission's 32001 section: Status 2; the Mist Melter title", "2/True",
              $"{Var($"Mission[{LogCoP}][{TheSavage}]Status")}/{DbTitle(titleMist)}");
        Warp(76.0f, -34.0f, 68.0f, TavnazianSafehold);
        Near(Justinius);
        Talk(Justinius);
        Check("Justinius: event 110", $"032:{Justinius}:110/110", $"{Ev()}/{Cur()}");
        EventEnd(110);
        Check("event 110 finished: The Savage complete, The Secrets of Worship current, the Nagmolada's Underling title",
              $"{TheSecretsOfWorship},{0xEE}/{TheSecretsOfWorship}/{0xEE}/{titleUnderling}/True",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{Lua("report(player:getTitle())")}/{DbTitle(titleUnderling)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 4-3 The Secrets of Worship: the Walnut Door, the Iron Gate to the Sacrarium");
        Near(WalnutDoor);
        Talk(WalnutDoor);
        Check("the Walnut Door (_0qa): event 111", $"032:{WalnutDoor}:111/111", $"{Ev()}/{Cur()}");
        EventEnd(111);
        Check("event 111 finished: Status 1", "1", Var($"Mission[{LogCoP}][{TheSecretsOfWorship}]Status"));
        uint parelbriaux = NpcId("Parelbriaux");
        Near(parelbriaux);
        Talk(parelbriaux);
        Check("Parelbriaux: event 137", $"{parelbriaux}:137", EvId());
        EventEnd(137);
        Warp(52.0f, -24.0f, 738.0f, Misareaux);
        uint ironGate = NpcId("_0p8");
        Near(ironGate);
        Talk(ironGate);
        Check("the Iron Gate (_0p8): cutscene 9", $"032:{ironGate}:9/9/{EventType.Cutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(9, 1);
        Check("cutscene 9 finished (option 1): Status 2", "2", Var($"Mission[{LogCoP}][{TheSecretsOfWorship}]Status"));
        Rezone();
        Check("  setPos into the Sacrarium", $"{Sacrarium}/-220/80", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 4-3: the Wooden Gate, Old Professor Mariselle, the Reliquiarium Key");
        uint woodenGate = NpcId("_0s8");
        var gate = Npc(woodenGate);
        MoveTo(gate.X - 1.5f, gate.Y, gate.Z);
        Talk(woodenGate);
        Check("the Wooden Gate (_0s8) from its west side: CANNOT_OPEN_SIDE", $"none/{Text(Sacrarium, "CANNOT_OPEN_SIDE")}:0", $"{Ev()}/{Special()}");
        MoveTo(gate.X + 1.5f, gate.Y, gate.Z);
        int reliquiarium = Ki("RELIQUIARIUM_KEY");
        Talk(woodenGate);
        Check("from the east: cutscene 6 with the key", $"034:{woodenGate}:6:0,{reliquiarium}/6", $"{Ev()}/{Cur()}");
        EventEnd(6);
        Check("cutscene 6 finished: Status 3", "3", Var($"Mission[{LogCoP}][{TheSecretsOfWorship}]Status"));
        // the Zone's onInitialize put the professor behind one of the six ???
        uint qmBase = uint.Parse(Lua("report(zones[xi.zone.SACRARIUM].npc.QM_MARISELLE_OFFSET)"));
        uint profId = uint.Parse(Lua("report(zones[xi.zone.SACRARIUM].mob.OLD_PROFESSOR_MARISELLE)"));
        uint profQm = Enumerable.Range(0, 6).Select(i => qmBase + (uint)i)
                                .First(id => Lua($"report(GetNPCByID({id}):getLocalVar('hasProfessorMariselle'))") == "1");
        uint otherQm = profQm == qmBase ? qmBase + 1 : qmBase;
        var prof = zone.ZoneInfoOf(Sacrarium).MobById[profId];
        Near(otherQm);
        Talk(otherQm);
        Check("a ??? without the professor: the drawer's messages (its own script), no spawn",
              $"{Text(Sacrarium, "DRAWER_OPEN")}:0,{Text(Sacrarium, "DRAWER_EMPTY")}:0/False", $"{Special()}/{prof.IsSpawned}");
        Near(profQm);
        Talk(profQm);
        // popFromQM's radius 2: the spawn point within 2 of the ??? (the professor may already have moved off it)
        Check("the professor's ???: EVIL_PRESENCE, Old Professor Mariselle popped at the ???", $"{Text(Sacrarium, "EVIL_PRESENCE")}:0/True/True",
              $"{Special()}/{prof.IsSpawned && prof.IsAlive}/{MathF.Sqrt(MathF.Pow(prof.SpawnX - Npc(profQm).X, 2) + MathF.Pow(prof.SpawnZ - Npc(profQm).Z, 2)) <= 2.01f}");
        // the framework's turn for the ???'s own script (DRAWER_SHUT, a messageSpecial: no gotMessage, which only
        // messageText sets), then the mission's lower action as well (DRAWER_SHUT again), as the C++
        Talk(profQm);
        Check("the ??? with the professor up: DRAWER_SHUT from its script, then from the mission", $"{Text(Sacrarium, "DRAWER_SHUT")}:0,{Text(Sacrarium, "DRAWER_SHUT")}:0",
              Special());
        KillByChar(profId);
        Check("the professor killed by the char: hasKilledMariselle", "True/1",
              $"{prof.IsDead}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{TheSecretsOfWorship}]hasKilledMariselle")}");
        Near(profQm);
        Talk(profQm);
        Check("the ??? again: the Reliquiarium Key, KEYITEM_OBTAINED", $"10/{Text(Sacrarium, "KEYITEM_OBTAINED")}:{reliquiarium}",
              $"{DbKeyItem(reliquiarium)}/{Special()}");
        MoveTo(gate.X + 1.5f, gate.Y, gate.Z);
        Talk(woodenGate);
        Check("the Wooden Gate with the key: cutscene 5", $"032:{woodenGate}:5/5", $"{Ev()}/{Cur()}");
        EventEnd(5);
        Check("cutscene 5 finished: The Secrets of Worship complete, Slanderous Utterings current", $"{SlanderousUtterings},{0xEE}/{SlanderousUtterings}/{0xEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Talk(woodenGate);
        Check("the gate after (the second section): startCutscene 110", $"032:{woodenGate}:110", Ev());
        EventEnd(110);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 4-4 Slanderous Utterings: the Safehold's trigger area 2, Sealion's Den");
        Warp(100.0f, -40.0f, -82.0f, TavnazianSafehold);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(108.0f, -40.0f, -82.0f);
        Tick(250);
        Check("trigger area 2 in the Safehold: event 112", $"032:{c.Id}:112/112", $"{Ev()}/{Cur()}");
        EventEnd(112);
        Check("event 112 finished: Status 1", "1", Var($"Mission[{LogCoP}][{SlanderousUtterings}]Status"));
        Near(Despachiaire);
        Talk(Despachiaire);
        Check("Despachiaire: event 132", $"032:{Despachiaire}:132", Ev());
        EventEnd(132);
        Warp(600.0f, 130.4f, 797.6f, SealionsDen);
        uint denDoor = NpcId("_0w0");
        Near(denDoor);
        Talk(denDoor);
        int titleLostOne = TitleId("THE_LOST_ONE");
        Check("the door (_0w0): event 13", $"032:{denDoor}:13/13", $"{Ev()}/{Cur()}");
        EventEnd(13);
        Check("event 13 finished: Slanderous Utterings complete, The Enduring Tumult of War current, The Lost One title",
              $"{TheEnduringTumultOfWar},{0xEE}/{TheEnduringTumultOfWar}/{0xEE}/{titleLostOne}/True",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{Lua("report(player:getTitle())")}/{DbTitle(titleLostOne)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-1 The Enduring Tumult of War: Despachiaire, Chasalvige, Anoki (the Option bits)");
        string OptionVar(int missionId) => Var($"Mission[{LogCoP}][{missionId}]Option");
        Warp(108.0f, -40.0f, -80.0f, TavnazianSafehold);
        Near(Despachiaire);
        Talk(Despachiaire);
        Check("Despachiaire: event 117", $"032:{Despachiaire}:117", Ev());
        EventEnd(117);
        Check("event 117 finished: Option bit 0", "1", OptionVar(TheEnduringTumultOfWar));
        Warp(96.0f, -0.5f, 131.0f, NorthernSandoria);
        Near(chasalvige);
        Talk(chasalvige);
        Check("Chasalvige: event 761", $"032:{chasalvige}:761", Ev());
        EventEnd(761);
        Talk(chasalvige);
        Check("Chasalvige again: event 764", $"032:{chasalvige}:764", Ev());
        EventEnd(764);
        Warp(-22.0f, -8.2f, 28.0f, PortSandoria);
        uint anoki = NpcId("Anoki");
        Near(anoki);
        Talk(anoki);
        Check("Anoki: event 724", $"032:{anoki}:724", Ev());
        EventEnd(724);
        Talk(anoki);
        Check("Anoki again: event 728", $"032:{anoki}:728", Ev());
        EventEnd(728);
        Check("the Option bits 0-3", "15", OptionVar(TheEnduringTumultOfWar));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-1: Port Bastok's zone-in, Cid");
        WarpIn(-126.0f, -6.0f, 10.0f, PortBastok);
        Check("in Port Bastok: the mission's zone-in cutscene 306", $"{PortBastok}/306", $"{c.Zone}/{Cur()}");
        EventEnd(306);
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 849", $"032:{cid}:849/849", $"{Ev()}/{Cur()}");
        EventEnd(849);
        Check("event 849 finished: Status 2", "2", Var($"Mission[{LogCoP}][{TheEnduringTumultOfWar}]Status"));
        Talk(cid);
        Check("Cid again: event 863", $"032:{cid}:863", Ev());
        EventEnd(863);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-1: Beaucedine Glacier's Iron Grate (F-7) into Pso'Xja, the Stone Gate's trap, Nunyunuwi");
        Warp(-340.0f, -95.0f, 156.0f, Beaucedine);
        uint grate = zone.ZoneInfoOf(Beaucedine).Npcs.Where(n => n.Name == "Iron_Grate").OrderBy(n => MathF.Abs(n.X + 340) + MathF.Abs(n.Z - 159)).First().Id;
        Talk(grate);
        // startOptionalCutscene's table form with no numbered params nor text table: a plain 0x032
        Check("the Iron Grate at F-7: optional cutscene 205", $"032:{grate}:205/205/{EventType.OptionalCutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(205, 1);
        Rezone(false);
        Check("option 1: setPos into Pso'Xja at x -300 from Beaucedine, the mission's zone-in cutscene 1", $"{PsoXja}/{Beaucedine}/-300/1",
              $"{c.Zone}/{c.PrevZone}/{c.X}/{Cur()}");
        EventEnd(1);
        Check("cutscene 1 finished: Status 3", "3", Var($"Mission[{LogCoP}][{TheEnduringTumultOfWar}]Status"));
        uint stoneGate = NpcId("_i98"), nunyunuwiId = uint.Parse(Lua("report(zones[xi.zone.PSOXJA].mob.NUNYUNUWI)"));
        var nunyunuwi = zone.ZoneInfoOf(PsoXja).MobById[nunyunuwiId];
        var stone = Npc(stoneGate);
        MoveTo(stone.X, stone.Y, stone.Z - 1.5f);
        Talk(stoneGate);
        Check("the Stone Gate (_i98): Nunyunuwi spawned and claimed, TRAP_ACTIVATES (messageName with the char's name), no event",
              $"none/True/{c.Id}/True", $"{Ev()}/{nunyunuwi.IsSpawned && nunyunuwi.IsAlive}/{nunyunuwi.OwnerId}/{Of(0x027).Count == 1}");
        KillByChar(nunyunuwiId);
        Check("Nunyunuwi killed by the char: Status 4", "True/4", $"{nunyunuwi.IsDead}/{Var($"Mission[{LogCoP}][{TheEnduringTumultOfWar}]Status")}");
        Talk(stoneGate);
        int gateEv = c.Z < 318 ? 69 : 70;
        Check("the Stone Gate again: its optional cutscene (by the side)", $"032:{stoneGate}:{gateEv}/{gateEv}/{EventType.OptionalCutscene}",
              $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(gateEv, 1);
        uint vahzlGate = NpcId("_i99");
        Near(vahzlGate);
        Talk(vahzlGate);
        Check("the gate to Promyvion - Vahzl (_i99): event 2", $"032:{vahzlGate}:2/2", $"{Ev()}/{Cur()}");
        int drained = Ki("MYSTERIOUS_AMULET_DRAINED"), lightVahzl = Ki("LIGHT_OF_VAHZL");
        EventEnd(2, 1);
        Rezone(false);
        Check("option 1: setPos into Promyvion - Vahzl, the mission's zone-in cutscene 50", $"{PromyvionVahzl}/50", $"{c.Zone}/{Cur()}");
        EventEnd(50);
        Check("cutscene 50 finished: the drained amulet, the Light of Vahzl, AMULET_RETURNED and LIGHT_OF_VAHZL",
              $"10/10/{Text(PromyvionVahzl, "AMULET_RETURNED")}:{amuletKi},{Text(PromyvionVahzl, "LIGHT_OF_VAHZL")}:{lightVahzl}",
              $"{DbKeyItem(drained)}/{DbKeyItem(lightVahzl)}/{Special()}");
        Check("  The Enduring Tumult of War complete, Desires of Emptiness current", $"{DesiresOfEmptiness},{0xEE}/{DesiresOfEmptiness}/{0xEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-2 Desires of Emptiness: the three memory fluxes of Promyvion - Vahzl");
        foreach (var (flux, mob, bit, cs) in new[] { ("_0mc", "PROPAGATOR", 0, 51), ("_0md", "SOLICITOR", 1, 52), ("_0m0", "PONDERER", 2, 53) })
        {
            uint fluxId = NpcId(flux), nmId = uint.Parse(Lua($"report(zones[xi.zone.PROMYVION_VAHZL].mob.{mob})"));
            var nm = zone.ZoneInfoOf(PromyvionVahzl).MobById[nmId];
            var f = Npc(fluxId);
            Lua($"player:setPos({f.X + 1.5f}, {f.Y}, {f.Z}, 0)");
            Talk(fluxId);
            Check($"the memory flux {flux}: {mob} spawned and claimed, ON_NM_SPAWN", $"{Text(PromyvionVahzl, "ON_NM_SPAWN")}:0/True/{c.Id}",
                  $"{Special()}/{nm.IsSpawned && nm.IsAlive}/{nm.OwnerId}");
            KillByChar(nmId);
            Check($"  {mob} killed: Option bit {bit}", "True", (int.Parse(OptionVar(DesiresOfEmptiness)) >> bit & 1) == 1);
            Talk(fluxId);
            Check($"  the flux again: cutscene {cs}", $"032:{fluxId}:{cs}/{cs}", $"{Ev()}/{Cur()}");
            EventEnd(cs);
            Check($"  cutscene {cs} finished: Option bit {bit + 3}", "True", (int.Parse(OptionVar(DesiresOfEmptiness)) >> (bit + 3) & 1) == 1);
        }
        Check("all three: Option 63, Status 1", "63/1", $"{OptionVar(DesiresOfEmptiness)}/{Var($"Mission[{LogCoP}][{DesiresOfEmptiness}]Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-2: the Spire of Vahzl, Desires of Emptiness won, Beaucedine Glacier");
        // the floors' memory fluxes up to the Spire: a GM !pos
        WarpIn(-0.0f, -2.0f, 293.6f, SpireOfVahzl);
        Check("in the Spire of Vahzl: the mission's zone-in cutscene 20", $"{SpireOfVahzl}/20", $"{c.Zone}/{Cur()}");
        EventEnd(20);
        Check("cutscene 20 finished: Status 2", "2", Var($"Mission[{LogCoP}][{DesiresOfEmptiness}]Status"));
        int titleFrozen = TitleId("FROZEN_DEAD_BODY");
        WinBattlefield(NpcId("_0n0"), "Desires of Emptiness", false);
        Check("  the win: the mission's 32001 section (Status 3, setPos), the Frozen Dead Body title", "3/True",
              $"{Var($"Mission[{LogCoP}][{DesiresOfEmptiness}]Status")}/{DbTitle(titleFrozen)}");
        Check("  in Beaucedine Glacier: the mission's zone-in cutscene 206", $"{Beaucedine}/-340/206", $"{c.Zone}/{c.X}/{Cur()}");
        EventEnd(206, 0, 1);
        var upd206 = Of(0x05C);
        Check("  the update: updateEvent(0, the Mysterious Amulet)", $"0,{amuletKi}",
              upd206.Count == 0 ? "none" : $"{BitConverter.ToInt32(upd206[^1].Buffer, 4 + S2cPendingnum.NumOffset(0))},{BitConverter.ToInt32(upd206[^1].Buffer, 4 + S2cPendingnum.NumOffset(1))}");
        EventEnd(206);
        Check("cutscene 206 finished: Status 4", "4", Var($"Mission[{LogCoP}][{DesiresOfEmptiness}]Status"));
        uint torino = NpcId("Torino-Samarino");
        Near(torino);
        Talk(torino);
        Check("Torino-Samarino: event 211", $"{torino}:211", EvId());
        EventEnd(211);
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 850", $"032:{cid}:850/850", $"{Ev()}/{Cur()}");
        EventEnd(850);
        Check("event 850 finished: Desires of Emptiness complete, Three Paths current", $"{ThreePaths},{0xEE}/{ThreePaths}/{0xEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // Three Paths' extended mission status: a nibble per path (xi.mission.status.COP LOUVERANCE 2, TENZEN 3,
        // ULMIA 4), 14 a path done; the Road Forks' two nibbles (0 and 1) kept at 14
        const int Louverance = 2, Tenzen = 3, Ulmia = 4;
        // a path's NPC: (the zone if not there, the NPC, the event, the path's nibble after)
        EventType? lastEvType = null;
        void ThreeStep(int path, string name, int ev, int after, ushort zoneId = 0, float x = 0, float y = 0, float z = 0)
        {
            if (zoneId != 0 && c.Zone != zoneId) Warp(x, y, z, zoneId);
            uint id = NpcId(name);
            Near(id);
            Talk(id);
            Check($"{name}: event {ev}", $"{id}:{ev}/{ev}", $"{EvId()}/{Cur()}");
            lastEvType = c.CurrentEvent?.Type;
            EventEnd(ev);
            Check($"  event {ev} finished: the path's nibble {after}", $"{after}", PathOf(path));
        }
        Console.WriteLine("CoP 5-3 Three Paths: Cid's reminder in Metalworks");
        Talk(cid);
        // getCidEventArg: no path done, 0 (a numbered param: 0x034)
        Check("Cid: the reminder event 851 (priority 995), the arg 0", $"034:{cid}:851/851", $"{Ev()}/{Cur()}");
        EventEnd(851);
        Check("event 851 finished: the cidOption local var 1", "1", $"{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{ThreePaths}]cidOption")}");
        Talk(cid);
        Check("Cid again: no reminder (cidOption 1), the default", "False", $"{Ev().Contains(":851")}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Louverance's path: Despachiaire, Perih Vashai, the Warmachine, Yoran-Oran");
        ThreeStep(Louverance, "Despachiaire", 118, 2, TavnazianSafehold, 108.0f, -40.0f, -83.0f);
        Check("  the Road Forks' nibbles kept", "14/14", $"{PathOf(0)}/{PathOf(1)}");
        // the mission's own reminder (a plain event 134) outranked by X Marks the Spot's offer (a progressEvent, its
        // check: CoP past Ancient Vows)
        ThreeStep(Louverance, "Despachiaire", 144, 2);
        ThreeStep(Louverance, "Perih_Vashai", 686, 3, WindurstWoods, 117.0f, -3.0f, 92.0f);
        ThreeStep(Louverance, "Perih_Vashai", 687, 3);
        ThreeStep(Louverance, "Warmachine", 33, 6, BibikiBay, -345.0f, -3.2f, -974.0f);
        Check("  the Warmachine's event a cutscene (progressCutscene)", $"{EventType.Cutscene}", $"{lastEvType}");
        ThreeStep(Louverance, "Yoran-Oran", 481, 6, WindurstWalls, -109.0f, -14.0f, 200.0f);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Louverance's path: Oldton Movalpolos, A Century of Hardship in Mine Shaft #2716");
        WarpIn(160.0f, 11.0f, -50.0f, OldtonMovalpolos);
        Check("in Oldton Movalpolos: the path's zone-in cutscene 1", $"{OldtonMovalpolos}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Check("cutscene 1 finished: Louverance's nibble 8", "8", PathOf(Louverance));
        // Tarnotik's way in (a Snow Lily) or the shaft's: a GM !pos
        Warp(-116.0f, -119.0f, -620.0f, MineShaft);
        uint shaftEntrance = NpcId("_0d0");
        WinBattlefield(shaftEntrance, "A Century of Hardship", false);
        Check("  the win: the mission's 32001 section (battlefieldWin, nibble 8): Louverance's nibble 9", "9", PathOf(Louverance));
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 852", $"{cid}:852/852", $"{EvId()}/{Cur()}");
        EventEnd(852);
        Check("event 852 finished: Louverance's nibble 11, cidOption 0", "11/0",
              $"{PathOf(Louverance)}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{ThreePaths}]cidOption")}");
        ThreeStep(Louverance, "Tarnotik", 34, 11, OldtonMovalpolos, 160.0f, 11.0f, -50.0f);
        Warp(-116.0f, -119.0f, -620.0f, MineShaft);
        Lua("player:addItem(xi.item.GOLD_KEY)");
        ushort goldKey = ushort.Parse(Lua("report(xi.item.GOLD_KEY)"));
        byte keySlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == goldKey).Slot;
        Near(shaftEntrance);
        Trade(shaftEntrance, (keySlot, 1));
        Check("a Gold Key traded to the Shaft Entrance: event 3", $"{shaftEntrance}:3/3", $"{EvId()}/{Cur()}");
        EventEnd(3);
        Check("event 3 finished: the key taken, Louverance's nibble 12", "0/12", $"{Qty(goldKey)}/{PathOf(Louverance)}");
        int titleCompanion = TitleId("COMPANION_OF_LOUVERANCE");
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        // getCidEventArg: no path done yet, 0
        Check("Cid: event 853", $"034:{cid}:853/853", $"{Ev()}/{Cur()}");
        EventEnd(853);
        Check("event 853 finished: Louverance's path done (14), the Companion of Louverance title, the mission still current",
              $"14/True/{ThreePaths}", $"{PathOf(Louverance)}/{DbTitle(titleCompanion)}/{Mission(LogCoP).Split(',')[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Tenzen's path: La Theine's ???, the Iron Grate (J-8) into Pso'Xja, the Avatar Gate");
        ThreeStep(Tenzen, "qm3", 203, 2, LaTheine, -178.0f, 8.8f, 254.0f);
        Warp(244.0f, 5.0f, -20.0f, Beaucedine);
        uint grateJ8 = zone.ZoneInfoOf(Beaucedine).Npcs.Where(n => n.Name == "Iron_Grate").OrderBy(n => MathF.Abs(n.X - 241) + MathF.Abs(n.Z + 20)).First().Id;
        Talk(grateJ8);
        Check("the Iron Grate at J-8: optional cutscene 200", $"032:{grateJ8}:200/200", $"{Ev()}/{Cur()}");
        EventEnd(200, 1);
        Rezone();
        Check("option 1: setPos into Pso'Xja at x 396", $"{PsoXja}/396", $"{c.Zone}/{c.X}");
        ThreeStep(Tenzen, "_09g", 3, 3);
        Check("  the Avatar Gate's event a cutscene (progressCutscene)", $"{EventType.Cutscene}", $"{lastEvType}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Tenzen's path: Monberaux, Pherimociel, Batallia Downs' ???");
        int envelope = Ki("ENVELOPE_FROM_MONBERAUX"), device = Ki("DELKFUTT_RECOGNITION_DEVICE");
        ThreeStep(Tenzen, "Monberaux", 74, 5, UpperJeuno, -40.0f, 0.0f, -2.0f);
        Check("  the Envelope from Monberaux", "10", DbKeyItem(envelope));
        ThreeStep(Tenzen, "Monberaux", 5, 5);
        ThreeStep(Tenzen, "Pherimociel", 58, 6, Rulude, -31.0f, 1.0f, 66.0f);
        ThreeStep(Tenzen, "Monberaux", 6, 6, UpperJeuno, -40.0f, 0.0f, -2.0f);
        ThreeStep(Tenzen, "qm4", 0, 6, BatalliaDowns, 424.0f, 8.5f, -163.0f);
        Check("  cutscene 0 finished: the Option var 1", "1", Var($"Mission[{LogCoP}][{ThreePaths}]Option"));
        ThreeStep(Tenzen, "qm4", 1, 8);
        Check("  the Delkfutt Recognition Device, the Option var gone", "10/-", $"{DbKeyItem(device)}/{Var($"Mission[{LogCoP}][{ThreePaths}]Option")}");
        ThreeStep(Tenzen, "Monberaux", 7, 8, UpperJeuno, -40.0f, 0.0f, -2.0f);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Tenzen's path: the Disaster Idol at Lower Delkfutt's Tower's Cermet Door");
        Warp(458.0f, -2.5f, 125.0f, LowerDelkfutt);
        uint door545 = NpcId("_545"), idolId = uint.Parse(Lua("report(zones[xi.zone.LOWER_DELKFUTTS_TOWER].mob.DISASTER_IDOL)"));
        var idol = zone.ZoneInfoOf(LowerDelkfutt).MobById[idolId];
        Near(door545);
        Talk(door545);
        Check("the Cermet Door (_545): the Disaster Idol popped (popFromQM, claimed), SOMETHING_HUGE_BEARING_DOWN, no event",
              $"none/True/{c.Id}/{Text(LowerDelkfutt, "SOMETHING_HUGE_BEARING_DOWN")}:0",
              $"{Ev()}/{idol.IsSpawned && idol.IsAlive}/{idol.OwnerId}/{Special()}");
        KillByChar(idolId);
        Check("the Idol killed by the char: its onMobDeath set hasKilled", "True/1",
              $"{idol.IsDead}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{ThreePaths}]hasKilled")}");
        Near(door545);
        Talk(door545);
        Check("the Cermet Door again: cutscene 25", $"{door545}:25/25", $"{EvId()}/{Cur()}");
        EventEnd(25);
        Check("cutscene 25 finished: Tenzen's nibble 9", "9", PathOf(Tenzen));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Tenzen's path: the Iron Grate (H-10) into Pso'Xja, the other Avatar Gate, Cid");
        Warp(60.0f, 5.0f, -356.0f, Beaucedine);
        uint grateH10 = zone.ZoneInfoOf(Beaucedine).Npcs.Where(n => n.Name == "Iron_Grate").OrderBy(n => MathF.Abs(n.X - 60) + MathF.Abs(n.Z + 359)).First().Id;
        Talk(grateH10);
        Check("the Iron Grate at H-10: optional cutscene 201", $"032:{grateH10}:201/201", $"{Ev()}/{Cur()}");
        EventEnd(201, 1);
        Rezone(false);
        Check("option 1: setPos into Pso'Xja at x 220 (getXPos() == 220): the path's zone-in cutscene 4", $"{PsoXja}/220/4",
              $"{c.Zone}/{c.X}/{Cur()}");
        EventEnd(4);
        Check("cutscene 4 finished: Tenzen's nibble 11", "11", PathOf(Tenzen));
        ThreeStep(Tenzen, "_09h", 5, 12);
        int titleTenzen = TitleId("TENZENS_ALLY");
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        // getCidEventArg: Louverance's path done, 1
        Check("Cid: event 854 with the arg 1 (Louverance's path done)", $"034:{cid}:854:1/854", $"{Ev()}/{Cur()}");
        EventEnd(854);
        Check("event 854 finished: Tenzen's path done (14), the Tenzen's Ally title, the mission still current",
              $"14/True/{ThreePaths}", $"{PathOf(Tenzen)}/{DbTitle(titleTenzen)}/{Mission(LogCoP).Split(',')[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Ulmia's path: Hinaree, Port San d'Oria, Chasalvige, Kerutoto, Yoran-Oran");
        ThreeStep(Ulmia, "Hinaree", 22, 2, SouthernSandoria, -301.0f, -10.2f, 100.0f);
        ThreeStep(Ulmia, "Hinaree", 25, 2);
        WarpIn(-6.0f, -8.0f, -120.0f, PortSandoria);
        Check("in Port San d'Oria: the path's zone-in cutscene 4", $"{PortSandoria}/4", $"{c.Zone}/{Cur()}");
        EventEnd(4);
        Check("cutscene 4 finished: Ulmia's nibble 3", "3", PathOf(Ulmia));
        ThreeStep(Ulmia, "Chasalvige", 762, 4, NorthernSandoria, 96.0f, -0.5f, 132.0f);
        ThreeStep(Ulmia, "Chasalvige", 763, 4);
        ThreeStep(Ulmia, "Kerutoto", 876, 6, WindurstWaters, 13.0f, -5.0f, -155.0f);
        ThreeStep(Ulmia, "Kerutoto", 882, 6);
        ThreeStep(Ulmia, "Yoran-Oran", 473, 7, WindurstWalls, -109.0f, -14.0f, 200.0f);
        ThreeStep(Ulmia, "Yoran-Oran", 479, 7);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 5-3, Ulmia's path: Head Wind in Boneyard Gully, Flames for the Dead in Bearclaw Pinnacle");
        int titleDelta = TitleId("DELTA_ENFORCER"), titleTrue = TitleId("TRUE_COMPANION_OF_LOUVERANCE");
        Warp(-617.0f, -1.5f, 505.0f, BoneyardGully);
        WinBattlefield(NpcId("_081"), "Head Wind", false);
        Check("  the win: the mission's 32001 section: Ulmia's nibble 8; the Delta Enforcer title", "8/True",
              $"{PathOf(Ulmia)}/{DbTitle(titleDelta)}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        // the arena left as the cleanup does it (CBattlefield::Cleanup: 10 s after the leave, the clearance lifted);
        // a char zoning out before that carries the effect into the next battlefield zone, an orphan there (kicked
        // 5 s on, no entry menu meanwhile)
        for (int i = 0; i < 30 && c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield); i++) Tick(1000);
        Check("  the battlefield cleaned up: the Battlefield effect lifted", "False", $"{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}");
        Warp(-718.0f, 8.0f, -440.0f, BearclawPinnacle);
        WinBattlefield(NpcId("Wind_Pillar_1"), "Flames for the Dead", false);
        Check("  the win: the mission's 32001 section: Ulmia's nibble 9; the True Companion of Louverance title", "9/True",
              $"{PathOf(Ulmia)}/{DbTitle(titleTrue)}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        int titleUlmia = TitleId("ULMIAS_SOULMATE"), titleTreader = TitleId("TREADER_OF_AN_ICY_PAST");
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        // getCidEventArg: Louverance's and Tenzen's paths done, 1 + 2
        Check("Cid: event 855 with the arg 3", $"034:{cid}:855:3/855", $"{Ev()}/{Cur()}");
        EventEnd(855);
        Check("event 855 finished: Ulmia's path done, the Ulmia's Soulmate title; all three: Three Paths complete (the Treader of an Icy Past title), For Whom the Verse Is Sung current, the status word kept",
              $"14/True/True/{ForWhomTheVerseIsSung},{0xEEEEE}/{ForWhomTheVerseIsSung}/{0xEEEEE}",
              $"{PathOf(Ulmia)}/{DbTitle(titleUlmia)}/{DbTitle(titleTreader)}/{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 6-1 For Whom the Verse Is Sung: Pherimociel, the Marble Bridge, Ru'Lude Gardens");
        Warp(-31.0f, 1.0f, 66.0f, Rulude);
        Near(Pherimociel);
        Talk(Pherimociel);
        Check("Pherimociel: event 10046", $"{Pherimociel}:10046/10046", $"{EvId()}/{Cur()}");
        EventEnd(10046);
        Check("event 10046 finished: Status 1", "1", Var($"Mission[{LogCoP}][{ForWhomTheVerseIsSung}]Status"));
        Talk(Pherimociel);
        Check("Pherimociel again: event 10052 (replaceDefault)", $"{Pherimociel}:10052", EvId());
        EventEnd(10052);
        Warp(-96.6f, -0.2f, 92.3f, UpperJeuno);
        uint marbleBridge = NpcId("_6s1");
        Near(marbleBridge);
        Talk(marbleBridge);
        Check("the Marble Bridge (_6s1): event 10011", $"{marbleBridge}:10011/10011", $"{EvId()}/{Cur()}");
        EventEnd(10011);
        Check("event 10011 finished: Status 2", "2", Var($"Mission[{LogCoP}][{ForWhomTheVerseIsSung}]Status"));
        WarpIn(0.0f, 3.0f, 20.0f, Rulude);
        Check("in Ru'Lude Gardens: the mission's zone-in cutscene 10047", $"{Rulude}/10047", $"{c.Zone}/{Cur()}");
        EventEnd(10047);
        Check("cutscene 10047 finished: For Whom the Verse Is Sung complete, A Place to Return current", $"{APlaceToReturn},{0xEEEEE}/{APlaceToReturn}/{0xEEEEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 6-2 A Place to Return: the palace's trigger area, the Warders at Misareaux Coast's Dilapidated Gate");
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(0.0f, 3.0f, 50.0f);
        Tick(250);
        Check("trigger area 1 in Ru'Lude Gardens: event 10048", $"{c.Id}:10048/10048", $"{EvId()}/{Cur()}");
        EventEnd(10048);
        Check("event 10048 finished: Status 1", "1", Var($"Mission[{LogCoP}][{APlaceToReturn}]Status"));
        Near(Pherimociel);
        Talk(Pherimociel);
        Check("Pherimociel: event 28", $"{Pherimociel}:28", EvId());
        EventEnd(28);
        Warp(260.0f, 7.3f, -437.0f, Misareaux);
        uint gate0p0 = NpcId("_0p0"), warder = uint.Parse(Lua("report(zones[xi.zone.MISAREAUX_COAST].mob.PM6_2_MOB_OFFSET)"));
        var warders = Enumerable.Range(0, 3).Select(i => zone.ZoneInfoOf(Misareaux).MobById[warder + (uint)i]).ToList();
        Near(gate0p0);
        Talk(gate0p0);
        Check("the Dilapidated Gate (_0p0): the three Warders spawned and claimed (updateClaim), no event", $"none/True/True",
              $"{Ev()}/{warders.All(w => w.IsSpawned && w.IsAlive)}/{warders.All(w => w.OwnerId == c.Id)}");
        Lua("player:setUnkillable(true)");
        for (int i = 0; i < 3; i++)
        {
            KillByChar(warder + (uint)i);
            Check($"  Warder {i} killed by the char: its onMobDeath set the Option bit {i}", $"True/{(1 << (i + 1)) - 1}",
                  $"{warders[i].IsDead}/{Var($"Mission[{LogCoP}][{APlaceToReturn}]Option")}");
        }
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Near(gate0p0);
        Talk(gate0p0);
        Check("the Dilapidated Gate again (Option 7): cutscene 10", $"{gate0p0}:10/10/{EventType.Cutscene}", $"{EvId()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(10);
        Check("cutscene 10 finished: A Place to Return complete, More Questions than Answers current, its vars gone",
              $"{MoreQuestionsThanAnswers},{0xEEEEE}/{MoreQuestionsThanAnswers}/{0xEEEEE}/-/-",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{Var($"Mission[{LogCoP}][{APlaceToReturn}]Status")}/{Var($"Mission[{LogCoP}][{APlaceToReturn}]Option")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 6-3 More Questions than Answers: Pherimociel, the palace door, Mathilde in Selbina");
        Warp(-31.0f, 1.0f, 66.0f, Rulude);
        Near(Pherimociel);
        Talk(Pherimociel);
        Check("Pherimociel: event 10049", $"{Pherimociel}:10049/10049", $"{EvId()}/{Cur()}");
        EventEnd(10049);
        Check("event 10049 finished: Status 1", "1", Var($"Mission[{LogCoP}][{MoreQuestionsThanAnswers}]Status"));
        Talk(Pherimociel);
        Check("Pherimociel again: event 10053", $"{Pherimociel}:10053", EvId());
        EventEnd(10053);
        uint palaceDoor = NpcId("_6r9");
        Near(palaceDoor);
        Talk(palaceDoor);
        Check("the palace door (_6r9): event 10050", $"{palaceDoor}:10050/10050", $"{EvId()}/{Cur()}");
        EventEnd(10050);
        Check("event 10050 finished: Status 2", "2", Var($"Mission[{LogCoP}][{MoreQuestionsThanAnswers}]Status"));
        Warp(12.0f, -7.3f, -9.0f, Selbina);
        uint mathilde = NpcId("Mathilde");
        Near(mathilde);
        Talk(mathilde);
        Check("Mathilde: event 10005", $"{mathilde}:10005/10005", $"{EvId()}/{Cur()}");
        EventEnd(10005);
        Check("event 10005 finished: More Questions than Answers complete, One to be Feared current", $"{OneToBeFeared},{0xEEEEE}/{OneToBeFeared}/{0xEEEEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 6-4 One to be Feared: Cid, Sealion's Den");
        string OtbfStatus() => Var($"Mission[{LogCoP}][{OneToBeFeared}]Status");
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 856", $"{cid}:856/856", $"{EvId()}/{Cur()}");
        EventEnd(856);
        Check("event 856 finished: Status 1", "1", OtbfStatus());
        WarpIn(612.0f, 132.7f, 776.9f, SealionsDen);
        Check("in Sealion's Den: the mission's zone-in cutscene 15", $"{SealionsDen}/15", $"{c.Zone}/{Cur()}");
        EventEnd(15, 0, 1);
        Check("  the update (option 0): updateEvent(0, 0, 0, 0, 32, 2, 0, 0)", "0,0,0,0,32,2,0,0", Upd(8));
        EventEnd(15);
        Check("cutscene 15 finished: Status 2", "2", OtbfStatus());
        uint sueleen = NpcId("Sueleen"), sealionGate = NpcId("_0w0");
        Near(sueleen);
        Talk(sueleen);
        Check("Sueleen: event 24", $"{sueleen}:24", EvId());
        EventEnd(24);
        Near(sealionGate);
        Talk(sealionGate);
        Check("the Iron Gate (_0w0): event 31", $"{sealionGate}:31/31", $"{EvId()}/{Cur()}");
        EventEnd(31);
        Check("event 31 finished: Status 3 (the battlefield's requiredVar)", "3", OtbfStatus());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 6-4: One to be Feared, the mammets, Omega and Ultima by the Airship Door");
        Lua("player:setUnkillable(true)");
        var otbf = EnterBattlefield(sealionGate, "One to be Feared");
        uint zeta = uint.Parse(Lua("report(zones[xi.zone.SEALIONS_DEN].mob.MAMMET_22_ZETA)"));
        uint door = uint.Parse(Lua("report(zones[xi.zone.SEALIONS_DEN].npc.AIRSHIP_DOOR_OFFSET)"));
        var mammets = Enumerable.Range(0, 5).Select(i => zone.ZoneInfoOf(SealionsDen).MobById[zeta + (uint)i]).ToList();
        var omega = zone.ZoneInfoOf(SealionsDen).MobById[zeta + 5];
        var ultima = zone.ZoneInfoOf(SealionsDen).MobById[zeta + 6];
        Check("  area 1: the five mammets up, Omega and Ultima not (spawned = false)", "1/True/False/False",
              $"{otbf?.Area}/{mammets.All(m => m.IsSpawned)}/{omega.IsSpawned}/{ultima.IsSpawned}");
        int titleBranded = TitleId("BRANDED_BY_LIGHTNING"), titleOmega = TitleId("OMEGA_OSTRACIZER"), titleUltima = TitleId("ULTIMA_UNDERTAKER");
        // one at a time: CBattlefield::handleDeath runs the group's allDeath on each death that finds the group dead
        foreach (var m in mammets)
        {
            m.Hp = 0;
            Tick(1000);
        }
        for (int i = 0; i < 10 && Cur() != "10"; i++) Tick(1000);
        Check("the mammets dead: their group's allDeath, event 10", "10", Cur());
        EventEnd(10);
        Check("event 10 finished: Branded by Lightning, the char back on the airship (area 1), battleCompleted 1", "True/-780/1",
              $"{DbTitle(titleBranded)}/{MathF.Round(c.X)}/{c.LocalVars.GetValueOrDefault("[OTBF]battleCompleted")}");
        // the Airship Door: the battlefield's exit NPC, its onExitTrigger (priority 1001)
        void AirshipDoor(int completed, int ev, ushort phase, MobEntity boss)
        {
            Near(door);
            Talk(door);
            Check($"the Airship Door: event 32003 (the door 1, battleCompleted {completed} * 2)", $"034:{door}:32003:1,{completed * 2}/32003",
                  $"{Ev()}/{Cur()}");
            EventEnd(32003, 100);
            // started in the door's onEventFinish: the event preparation's target, the door
            Check($"  option 100: onEventFinishExit's event {ev} (option - 99), the door's", $"034:{door}:{ev}:1/{ev}", $"{Ev()}/{Cur()}");
            EventEnd(ev, 0, 1);
            Check($"  the update (option 0): the phase's boss spawned", "True", $"{boss.IsSpawned && boss.IsAlive}");
            EventEnd(ev);
        }
        AirshipDoor(1, 1, 1, omega);
        omega.Hp = 0;
        for (int i = 0; i < 10 && Cur() != "11"; i++) Tick(1000);
        Check("Omega dead: its group's allDeath, event 11", "11", Cur());
        EventEnd(11);
        Check("event 11 finished: Omega Ostracizer, battleCompleted 2", "True/-780/2",
              $"{DbTitle(titleOmega)}/{MathF.Round(c.X)}/{c.LocalVars.GetValueOrDefault("[OTBF]battleCompleted")}");
        AirshipDoor(2, 2, 2, ultima);
        ultima.Hp = 0;
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(1000);
        Check("Ultima dead: WON, onBattlefieldWin's cutscene 32001", "2/32001", $"{otbf?.Status}/{Cur()}");
        EventEnd(32001);
        Check("  the mission's 32001 section: Status 4; the Ultima Undertaker title", "4/True", $"{OtbfStatus()}/{DbTitle(titleUltima)}");
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Rezone(false);
        Check("  setPos back into Sealion's Den: the mission's zone-in cutscene 33", $"{SealionsDen}/612/33", $"{c.Zone}/{MathF.Round(c.X)}/{Cur()}");
        EventEnd(33);
        Check("cutscene 33 finished: One to be Feared complete, Chains and Bonds current", $"{ChainsAndBonds},{0xEEEEE}/{ChainsAndBonds}/{0xEEEEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Rezone(false);
        Check("  setPos into Lufaise Meadows: Chains and Bonds' zone-in cutscene 111", $"{Lufaise}/407/111", $"{c.Zone}/{MathF.Round(c.X)}/{Cur()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-1 Chains and Bonds: the Ducal Guard's Ring, the Safehold, Sealion's Den");
        string MVar(int missionId, string v) => Var($"Mission[{LogCoP}][{missionId}]{v}");
        ushort ducalRing = ushort.Parse(Lua("report(xi.item.DUCAL_GUARDS_RING)"));
        EventEnd(111, 0, 1);
        Check("  the update: updateEvent(0, the Ducal Guard's Ring)", $"0,{ducalRing}", Upd(2));
        EventEnd(111);
        Check("cutscene 111 finished: the ring given, Status 1", "1/1", $"{Qty(ducalRing)}/{MVar(ChainsAndBonds, "Status")}");
        WarpIn(76.0f, -34.0f, 68.0f, TavnazianSafehold);
        Check("in the Tavnazian Safehold: the mission's zone-in cutscene 114", $"{TavnazianSafehold}/114", $"{c.Zone}/{Cur()}");
        EventEnd(114);
        Check("cutscene 114 finished: Status 2", "2", MVar(ChainsAndBonds, "Status"));
        Near(Justinius);
        Talk(Justinius);
        // the mission's plain event 135 outranked by Uninvited Guests' offer (a progressEvent: The Savage complete)
        Check("Justinius: Uninvited Guests' offer, event 570", $"{Justinius}:570", EvId());
        EventEnd(570);
        Near(SewerEntrance);
        Talk(SewerEntrance);
        Check("the Sewer Entrance (_0q1): event 116", $"{SewerEntrance}:116/116", $"{EvId()}/{Cur()}");
        EventEnd(116);
        Check("event 116 finished: the Option bit 1", "2", MVar(ChainsAndBonds, "Option"));
        WarpIn(612.0f, 132.7f, 776.9f, SealionsDen);
        Check("in Sealion's Den: the mission's zone-in cutscene 14", $"{SealionsDen}/14", $"{c.Zone}/{Cur()}");
        EventEnd(14);
        Check("cutscene 14 finished: the Option bit 2", "6", MVar(ChainsAndBonds, "Option"));
        Warp(111.0f, -41.0f, 38.0f, TavnazianSafehold);
        Near(WalnutDoor);
        Talk(WalnutDoor);
        Check("the Walnut Door (_0qa): event 115", $"{WalnutDoor}:115/115", $"{EvId()}/{Cur()}");
        EventEnd(115);
        Check("event 115 finished: Option 7, Chains and Bonds complete, Flames in the Darkness current",
              $"{FlamesInTheDarkness},{0xEEEEE}/{FlamesInTheDarkness}/{0xEEEEE}/-", $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{MVar(ChainsAndBonds, "Option")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-2 Flames in the Darkness: Misareaux's Dilapidated Gate, Sueleen, Ru'Lude, the Marble Bridge");
        Warp(-260.0f, -32.7f, 283.0f, Misareaux);
        Near(DilapidatedGateWest);
        Talk(DilapidatedGateWest);
        Check("the Dilapidated Gate (_0p2): cutscene 12", $"{DilapidatedGateWest}:12/12/{EventType.Cutscene}", $"{EvId()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(12);
        Check("cutscene 12 finished: Status 1", "1", MVar(FlamesInTheDarkness, "Status"));
        Warp(612.0f, 132.7f, 776.9f, SealionsDen);
        Near(sueleen);
        Talk(sueleen);
        Check("Sueleen: event 16", $"{sueleen}:16/16", $"{EvId()}/{Cur()}");
        EventEnd(16);
        Check("event 16 finished: Status 2", "2", MVar(FlamesInTheDarkness, "Status"));
        Warp(0.0f, 3.0f, 20.0f, Rulude);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(0.0f, 3.0f, 50.0f);
        Tick(250);
        Check("trigger area 1 in Ru'Lude Gardens: event 10051", $"{c.Id}:10051/10051", $"{EvId()}/{Cur()}");
        EventEnd(10051);
        Check("event 10051 finished: Status 3", "3", MVar(FlamesInTheDarkness, "Status"));
        Warp(-96.6f, -0.2f, 92.3f, UpperJeuno);
        Near(marbleBridge);
        Talk(marbleBridge);
        Check("the Marble Bridge (_6s1): event 10012", $"{marbleBridge}:10012/10012", $"{EvId()}/{Cur()}");
        int titleEshantarl = TitleId("ESHANTARLS_COMRADE_IN_ARMS");
        EventEnd(10012);
        Check("event 10012 finished: Flames in the Darkness complete (the Eshantarl's Comrade-in-Arms title), Fire in the Eyes of Men current",
              $"{FireInTheEyesOfMen},{0xEEEEE}/{FireInTheEyesOfMen}/{0xEEEEE}/True", $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{DbTitle(titleEshantarl)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-3 Fire in the Eyes of Men: the Shaft Entrance, Cid's day's wait");
        Warp(-116.0f, -119.0f, -620.0f, MineShaft);
        Near(shaftEntrance);
        Talk(shaftEntrance);
        Check("the Shaft Entrance (_0d0): event 4", $"{shaftEntrance}:4/4", $"{EvId()}/{Cur()}");
        EventEnd(4);
        Check("event 4 finished: Status 1", "1", MVar(FireInTheEyesOfMen, "Status"));
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 857", $"{cid}:857/857", $"{EvId()}/{Cur()}");
        EventEnd(857);
        Check("event 857 finished: the Timer the next Vana'diel day", $"{VanaTime.CountDays(vanaTicks) + 1}", MVar(FireInTheEyesOfMen, "Timer"));
        Talk(cid);
        Check("Cid the same day: event 858", $"{cid}:858/858", $"{EvId()}/{Cur()}");
        EventEnd(858);
        vanaTicks += VanaTime.DAY;
        Talk(cid);
        Check("Cid the next day: event 890", $"{cid}:890/890", $"{EvId()}/{Cur()}");
        int titlePrishe = TitleId("PRISHES_BUDDY");
        EventEnd(890);
        Check("event 890 finished: Fire in the Eyes of Men complete (the Prishe's Buddy title), Calm Before the Storm current",
              $"{CalmBeforeTheStorm},{0xEEEEE}/{CalmBeforeTheStorm}/{0xEEEEE}/True", $"{Mission(LogCoP)}/{DbMission(LogCoP)}/{DbTitle(titlePrishe)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-4 Calm Before the Storm: Boggelmann at Misareaux's _0p4");
        const int CidNibble = 6;
        // a GM's unkillable flag against the NMs, set again after each zoning (the char entity's, gone with it)
        Warp(-310.0f, -34.2f, 187.0f, Misareaux);
        Lua("player:setUnkillable(true)");
        uint gate0p4 = NpcId("_0p4"), boggelmannId = uint.Parse(Lua("report(zones[xi.zone.MISAREAUX_COAST].mob.BOGGELMANN)"));
        var boggelmann = zone.ZoneInfoOf(Misareaux).MobById[boggelmannId];
        Near(gate0p4);
        Talk(gate0p4);
        Check("the gate (_0p4): Boggelmann spawned and claimed, CREATURE_HAS_APPEARED, no event",
              $"none/True/{c.Id}/{Text(Misareaux, "CREATURE_HAS_APPEARED")}:0", $"{Ev()}/{boggelmann.IsSpawned && boggelmann.IsAlive}/{boggelmann.OwnerId}/{Special()}");
        KillByChar(boggelmannId);
        Check("Boggelmann killed by the char: nmMisareaux 1", "True/1",
              $"{boggelmann.IsDead}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{CalmBeforeTheStorm}]nmMisareaux")}");
        Near(gate0p4);
        Talk(gate0p4);
        // progressCutscene(13, { [1] = ring }): the table form, its key 1 the second param
        Check("the gate again: cutscene 13 (the Ducal Guard's Ring the second param)", $"034:{gate0p4}:13:0,{ducalRing}/13", $"{Ev()}/{Cur()}");
        EventEnd(13);
        int vessel = Ki("VESSEL_OF_LIGHT");
        Check("cutscene 13 finished: the Cid nibble's bit 0, the Vessel of Light", "1/10", $"{PathOf(CidNibble)}/{DbKeyItem(vessel)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-4: the Cryptonberries at Carpenters' Landing");
        Warp(118.0f, -5.5f, -390.0f, CarpentersLanding);
        Lua("player:setUnkillable(true)");
        uint qmCrypt = NpcId("qm_cryptonberries"), executor = uint.Parse(Lua("report(zones[xi.zone.CARPENTERS_LANDING].mob.CRYPTONBERRY_EXECUTOR)"));
        var berries = Enumerable.Range(0, 4).Select(i => zone.ZoneInfoOf(CarpentersLanding).MobById[executor + (uint)i]).ToList();
        Near(qmCrypt);
        Talk(qmCrypt);
        Check("the ??? : the Executor spawned and claimed, CRYPTONBERRY_EXECUTOR_POP, no event",
              $"none/True/{c.Id}/{Text(CarpentersLanding, "CRYPTONBERRY_EXECUTOR_POP")}:0",
              $"{Ev()}/{berries[0].IsSpawned && berries[0].IsAlive}/{berries[0].OwnerId}/{Special()}");
        // the char's swings (claimed already: updateClaim): its hits' enmity on the Executor (getCE > 0, setCE 0 at
        // the pop), its fight calls the three Assassins; a GM's accuracy so the swings land
        Lua("player:addMod(xi.mod.ACC, 1000)");
        var exec = berries[0];
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(exec.Id).CopyTo(b, 4);
            BitConverter.GetBytes(exec.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
        });
        for (int i = 0; i < 30 && !berries.Skip(1).All(b => b.IsSpawned); i++)
        {
            float dx = exec.X - c.X, dz = exec.Z - c.Z;
            byte dir = (byte)(short)(MathF.Atan2(dz, dx) * -(128.0f / MathF.PI));
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(exec.X - 1.5f).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(exec.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(exec.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            Tick(1000);
        }
        Check("  the char's enmity on the Executor: its onMobFight called the three Assassins (callPets)", "True",
              $"{berries.Skip(1).All(b => b.IsSpawned && b.IsAlive)}");
        // the Assassins first (engaged on the char: callPets' updateEnmity), each claimed by the char's swing; the
        // Executor last (a pet left roaming once its owner is dead is killed by callPets' ROAM_TICK listener), its
        // claim gone stale meanwhile (CMobController: the owner's PClaimedMob another mob for 3 s), taken back by a
        // swing (the GM's accuracy kept to the end)
        foreach (int i in new[] { 1, 2, 3, 0 })
        {
            if (!berries[i].IsAlive) continue;
            ClaimBySwing(executor + (uint)i);
            // an Assassin's Perfect Dodge (its 2hr) can keep every swing from landing: a GM's claim besides
            Lua($"local mob = GetMobByID({executor + (uint)i}) mob:updateClaim(player) " +
                "mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            Tick(1000);
            Tick(1000);
        }
        Lua("player:delMod(xi.mod.ACC, 1000)");
        Check("the four killed by the char: carpentersNm 15", "True/15",
              $"{berries.All(b => b.IsDead)}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{CalmBeforeTheStorm}]carpentersNm")}");
        Near(qmCrypt);
        Talk(qmCrypt);
        Check("the ??? again: cutscene 37", $"{qmCrypt}:37/37", $"{EvId()}/{Cur()}");
        EventEnd(37);
        Check("cutscene 37 finished: the Cid nibble's bit 1", "3", PathOf(CidNibble));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-4: Dalham at Bibiki Bay");
        Warp(98.0f, -44.7f, 940.5f, BibikiBay);
        Lua("player:setUnkillable(true)");
        uint qmDalham = NpcId("qm_dalham");
        var dalham = zone.ZoneInfoOf(BibikiBay).Mobs.First(m => m.Name == "Dalham");
        Near(qmDalham);
        Talk(qmDalham);
        Check("the ??? : Dalham (queryEntitiesByName) spawned and claimed, YOU_ARE_NOT_ALONE, no event",
              $"none/True/{c.Id}/{Text(BibikiBay, "YOU_ARE_NOT_ALONE")}:0", $"{Ev()}/{dalham.IsSpawned && dalham.IsAlive}/{dalham.OwnerId}/{Special()}");
        KillByChar(dalham.Id);
        Check("Dalham killed by the char: nmBibiki 1", "True/1",
              $"{dalham.IsDead}/{c.LocalVars.GetValueOrDefault($"Mission[{LogCoP}][{CalmBeforeTheStorm}]nmBibiki")}");
        Near(qmDalham);
        Talk(qmDalham);
        Check("the ??? again: cutscene 41", $"{qmDalham}:41/41", $"{EvId()}/{Cur()}");
        EventEnd(41);
        Check("cutscene 41 finished: the Cid nibble 7", "7", PathOf(CidNibble));
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-4: Cid's letters, Sueleen");
        int letters = Ki("LETTERS_FROM_ULMIA_AND_PRISHE");
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid: event 892", $"{cid}:892/892", $"{EvId()}/{Cur()}");
        EventEnd(892);
        Check("event 892 finished: the Letters from Ulmia and Prishe", "10", DbKeyItem(letters));
        Talk(cid);
        Check("Cid again: event 895 (once per zone)", $"{cid}:895", EvId());
        EventEnd(895);
        Talk(cid);
        Check("  and not again in the zone", "False", $"{EvId() == $"{cid}:895"}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Warp(612.0f, 132.7f, 776.9f, SealionsDen);
        Near(sueleen);
        Talk(sueleen);
        Check("Sueleen: event 17", $"{sueleen}:17/17", $"{EvId()}/{Cur()}");
        EventEnd(17);
        Check("event 17 finished: Calm Before the Storm complete, The Warrior's Path current", $"{TheWarriorsPath},{0x70EEEEE}/{TheWarriorsPath}/{0x70EEEEE}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 7-5 The Warrior's Path: the Iron Gate, the battlefield, Al'Taieu");
        Near(sueleen);
        Talk(sueleen);
        Check("Sueleen: event 28", $"{sueleen}:28", EvId());
        EventEnd(28);
        Near(sealionGate);
        Talk(sealionGate);
        Check("the Iron Gate (_0w0): event 32", $"{sealionGate}:32/32", $"{EvId()}/{Cur()}");
        EventEnd(32, 0, 1);
        Check("  the update (option 0): updateEvent(32, 0, 0, 0, 0, 0, 0, 0)", "32,0,0,0,0,0,0,0", Upd(8));
        EventEnd(32);
        Check("event 32 finished: Status 1 (the battlefield's requiredVar)", "1", MVar(TheWarriorsPath, "Status"));
        int titleUnquenchable = TitleId("UNQUENCHABLE_LIGHT"), titleChebukki = TitleId("THE_CHEBUKKIS_WORST_NIGHTMARE"),
            titleSeeker = TitleId("SEEKER_OF_THE_LIGHT");
        Lua("player:setUnkillable(true)");
        WinBattlefield(sealionGate, "The Warrior's Path", false);
        Check("  the win: both titles (onEventFinishWin), the mission's 32001 section: Status 2, setPos back into Sealion's Den: its zone-in cutscene 34",
              $"True/True/2/{SealionsDen}/34", $"{DbTitle(titleUnquenchable)}/{DbTitle(titleChebukki)}/{MVar(TheWarriorsPath, "Status")}/{c.Zone}/{Cur()}");
        EventEnd(34);
        Check("cutscene 34 finished: Status 3", "3", MVar(TheWarriorsPath, "Status"));
        Rezone(false);
        Check("  setPos into Al'Taieu: the mission's zone-in cutscene 1", $"{AlTaieu}/-422/1", $"{c.Zone}/{MathF.Round(c.X)}/{Cur()}");
        int lightAltaieu = Ki("LIGHT_OF_ALTAIEU");
        EventEnd(1);
        Check("cutscene 1 finished: The Warrior's Path complete (the Seeker of the Light title), Garden of Antiquity current, Option 1, SagheeraInteractions 7, AMULET_SHATTERED",
              $"{GardenOfAntiquity}/{GardenOfAntiquity}/True/1/7/True",
              $"{Mission(LogCoP).Split(',')[0]}/{DbMission(LogCoP).Split('/')[0]}/{DbTitle(titleSeeker)}/{MVar(TheWarriorsPath, "Option")}/{Var("SagheeraInteractions")}/{Special().Contains(Text(AlTaieu, "AMULET_SHATTERED") + ":")}");
        // the char's race (getStolenKeyItem): a light other than Al'Taieu's stolen and Al'Taieu's given, or Al'Taieu's itself stolen
        int stolen = int.Parse(Lua("report(player:getRace())")) <= 7 ? 1 : 0;
        Check("  the Light of Al'Taieu given unless it was the stolen one", stolen == 1 ? "10" : "00", DbKeyItem(lightAltaieu));
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Treasures of Aht Urhgan, from Lost Kingdom ----
        Console.WriteLine("ToAU 13 Lost Kingdom: the login in Aht Urhgan Whitegate");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[4];
        Fixture(db, Whitegate, Whitegate, 22.0f, -7.8f, -42.0f, 1);
        Login();
        // the missions before: !addmission 4 12 (13_Lost_Kingdom.lua's header), the Sentinels' badge and Royal
        // Puppeteer's vial
        Lua($"player:addMission({LogToAU}, {LostKingdom}) player:addKeyItem(xi.keyItem.PSC_WILDCAT_BADGE) player:addKeyItem(xi.keyItem.VIAL_OF_SPECTRAL_SCENT)");
        int vial = Ki("VIAL_OF_SPECTRAL_SCENT"), coin = Ki("EPHRAMADIAN_GOLD_COIN");
        Check("in Whitegate on Lost Kingdom with the vial, mercenary rank 1", $"{Whitegate}/{LostKingdom},0/10/1",
              $"{c.Zone}/{Mission(LogToAU)}/{DbKeyItem(vial)}/{Lua("report(xi.besieged.getMercenaryRank(player))")}");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3071 with the mercenary rank", $"034:{NajaSalaheem}:3071:1,1", Ev());
        EventEnd(3071);
        Warp(20.0f, 0.0f, 24.0f, Nashmau);
        Near(Pyopyoroon);
        Talk(Pyopyoroon);
        Check("Pyopyoroon: event 275", $"032:{Pyopyoroon}:275", Ev());
        EventEnd(275);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 13: Jazaraat's Headstone in Caedarva Mire");
        Warp(-389.0f, 6.0f, -567.0f, CaedarvaMire);
        uint headstone = NpcId("Jazaraats_Headstone"), jazaraatId = uint.Parse(Lua("report(zones[xi.zone.CAEDARVA_MIRE].mob.JAZARAAT)"));
        var jazaraat = zone.ZoneInfoOf(CaedarvaMire).MobById[jazaraatId];
        Near(headstone);
        Talk(headstone);
        Check("the headstone with the vial: cutscene 8", $"032:{headstone}:8/8/{EventType.Cutscene}", $"{Ev()}/{Cur()}/{c.CurrentEvent?.Type}");
        EventEnd(8);
        Check("cutscene 8 finished: mission status 1", $"{LostKingdom},1", Mission(LogToAU));
        Talk(headstone);
        // updateEnmity is the mob's PAI->Engage: engaged on the char, no enmity entry, no claim
        Check("the headstone again: Jazaraat spawned, engaged on the char (updateEnmity), no event or message", $"none/True/True/{c.Id}/0/",
              $"{Ev()}/{jazaraat.IsSpawned && jazaraat.IsAlive}/{jazaraat.Engaged}/{jazaraat.BattleTarget?.Id}/{jazaraat.OwnerId}/{Special()}");
        KillByChar(jazaraatId);
        Check("Jazaraat killed by the char: mission status 2", $"True/{LostKingdom},2", $"{jazaraat.IsDead}/{Mission(LogToAU)}");
        Talk(headstone);
        Check("the headstone: cutscene 9", $"032:{headstone}:9/9", $"{Ev()}/{Cur()}");
        EventEnd(9);
        Check("cutscene 9 finished: the vial gone, the reward's Ephramadian Gold Coin, Lost Kingdom complete, The Dolphin Crest current",
              $"00/10/{TheDolphinCrest},0/{TheDolphinCrest}/0/True",
              $"{DbKeyItem(vial)}/{DbKeyItem(coin)}/{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, LostKingdom)}");
        Talk(headstone);
        Check("the headstone after, the coin held: JAZARAATS_HEADSTONE", $"none/{Text(CaedarvaMire, "JAZARAATS_HEADSTONE")}:0", $"{Ev()}/{Special()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 14 The Dolphin Crest: Naja Salaheem");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3072", $"034:{NajaSalaheem}:3072:1,1/3072", $"{Ev()}/{Cur()}");
        EventEnd(3072);
        Check("event 3072 finished: The Dolphin Crest complete, The Black Coffin current", $"{TheBlackCoffin},0/{TheBlackCoffin}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, TheDolphinCrest)}");
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3073", $"034:{NajaSalaheem}:3073:1,1", Ev());
        EventEnd(3073);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 15 The Black Coffin: Arrapago Reef's trigger area, the queued events");
        Warp(-456.0f, -3.0f, -425.0f, ArrapagoReef);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(-458.0f, -3.0f, -405.0f);
        Tick(250);
        // startEvent(8), startEvent(34, hidden), progressEvent(35, hidden): the queue (CCharEntity::queueEvent), one
        // at a time as each ends
        Check("trigger area 1 with the coin: event 8 first", $"032:{c.Id}:8/8", $"{Ev()}/{Cur()}");
        EventEnd(8);
        Check("event 8 finished: the queued 34 (param 7 = 1, hidden)", $"034:{c.Id}:34:0,0,0,0,0,0,0,1/34/True",
              $"{Ev()}/{Cur()}/{c.CurrentEvent?.IsHidden}");
        EventEnd(34);
        Check("event 34 finished: the queued 35 (hidden)", $"032:{c.Id}:35/35/True", $"{Ev()}/{Cur()}/{c.CurrentEvent?.IsHidden}");
        EventEnd(35);
        Check("event 35 finished: mission status 1", $"{TheBlackCoffin},1/none", $"{Mission(LogToAU)}/{Cur()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 15: the Cutter, into the Ashu Talif (The Black Coffin, instance 6000)");
        uint cutter = NpcId("Cutter");
        Near(cutter);
        Talk(cutter);
        Check("the Cutter: xi.instance.onTrigger's event 221 (53, -6, 0, 99, 6), INSTANCE_ID 6000", $"034:{cutter}:221:53,{unchecked((uint)-6)},0,99,6/221/6000",
              $"{Ev()}/{Cur()}/{c.LocalVars.GetValueOrDefault("INSTANCE_ID")}");
        EventEnd(221, 0, 1);
        Check("the update: createInstance queued, INSTANCE_REQUESTED 2, no instance yet", "2/nil",
              $"{c.LocalVars.GetValueOrDefault("INSTANCE_REQUESTED")}/{Lua("report(tostring(player:getInstance()))")}");
        s.PacketList.Clear();
        Tick(250);
        var registration = Of(0x0BF).LastOrDefault();
        Check("loaded at the end of the tick: onInstanceCreatedCallback's setInstance, instanceEntry(the Cutter, 4)", $"6000/4/{cutter & 0xFFF}",
              $"{Lua("report(player:getInstance():getID())")}/{(registration == null ? "none" : $"{BitConverter.ToUInt16(registration.Buffer, 6)}/{BitConverter.ToUInt32(registration.Buffer, 12)}")}");
        Check("  onInstanceCreated: Gessho and the first wave's five crew spawned in the run", "6",
              Lua("local n = 0 for _, m in ipairs(player:getInstance():getMobs()) do if m:isSpawned() then n = n + 1 end end report(n)"));
        EventEnd(221, 4);
        Rezone();
        Check("event 221 finished (option 4): setPos into the Ashu Talif, in the run at its entry", $"{AshuTalif}/6000/0/-22/24",
              $"{c.Zone}/{c.Instance?.Id}/{c.X}/{c.Y}/{c.Z}");
        Check("  onInstanceZoneIn: the temporary item (5349)", "1",
              DbRow(db, "COUNT(*)", "FROM char_inventory WHERE charid = ? AND location = 3 AND itemId = 5349"));
        s.PacketList.Clear();
        Tick(500);
        Check("afterInstanceRegister: the coin fades (FADES_INTO_NOTHINGNESS), TIME_TO_COMPLETE 30", $"00/{Text(AshuTalif, "FADES_INTO_NOTHINGNESS")}:{coin},{Text(AshuTalif, "TIME_TO_COMPLETE")}:30",
              $"{DbKeyItem(coin)}/{Special()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 15: the two waves, the captain gives up");
        // a WAR 75 alone against both waves and the captain: kept standing (a GM's unkillable) while the run is fought
        Lua("player:setUnkillable(true)");
        uint crew = uint.Parse(Lua("report(zones[xi.zone.THE_ASHU_TALIF].mob.ASHU_CREW_OFFSET)"));
        uint captainId = uint.Parse(Lua("report(zones[xi.zone.THE_ASHU_TALIF].mob.ASHU_CAPTAIN_OFFSET)"));
        string Progress() => Lua("report(player:getInstance():getProgress())");
        for (uint i = 0; i < 5; i++) KillByChar(crew + i);
        Check("the first wave killed by the char: progress 5, onInstanceProgressUpdate spawned the second wave", "5/5",
              $"{Progress()}/{Lua($"local n = 0 for i = 0, 4 do if GetMobByID({captainId} + i, player:getInstance()):isSpawned() then n = n + 1 end end report(n)")}");
        var captain = c.Instance.Info.MobById[captainId];
        Check("  the captain untargetable on its perch", "True", captain.Untargetable);
        for (int i = 0; i < 16; i++) Tick(1000);
        Check("  twelve seconds on: the captain jumped down (jump 2), targetable two seconds after", "2/False/0/-22/13",
              $"{Lua($"report(GetMobByID({captainId}, player:getInstance()):getLocalVar('jump'))")}/{captain.Untargetable}/{captain.X}/{captain.Y}/{captain.Z}");
        for (uint i = 1; i < 5; i++) KillByChar(captainId + i);
        Check("the second wave's crew killed: progress 9", "9", Progress());
        // the char zoned in cleanly: the 35 s failsafe timer onInstanceCreatedCallback set went with the char that
        // zoned out (~CCharEntity takes its action queue)
        Check("  no failsafe setPos (the timer of the char that zoned out did not fire)", "False",
              log.Any(l => l.Contains("failed to cleanly transition into instance event")));
        // the captain engaged by the char's swing, brought to 15%: its onMobFight completes the run
        Lua($"GetMobByID({captainId}, player:getInstance()):updateEnmity(player)");
        captain.Hp = captain.MaxHp * 15 / 100;
        s.PacketList.Clear();
        for (int i = 0; i < 5 && Cur() != "102"; i++) Tick(1000);
        Check("the captain at 15% in its fight: instance:complete(), onInstanceComplete set mission status 2, event 102",
              $"true/{TheBlackCoffin},2/102", $"{Lua("report(tostring(player:getInstance():completed()))")}/{Mission(LogToAU)}/{Cur()}");
        EventEnd(102);
        Rezone(false);
        Check("event 102 finished: setPos(0, 0, 0) to Arrapago Reef, the mission's zone-in moved the char and cutscene 9", $"{ArrapagoReef}/{AshuTalif}/-444/-414/9",
              $"{c.Zone}/{c.PrevZone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Cur()}");
        EventEnd(9);
        Rezone(false);
        Check("cutscene 9 finished: mission status 3, setPos(0, 0, 0) to Nashmau, its zone-in moved the char and cutscene 281",
              $"{Nashmau}/{ArrapagoReef}/0/-24/281/{TheBlackCoffin},3", $"{c.Zone}/{c.PrevZone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Cur()}/{Mission(LogToAU)}");
        EventEnd(281);
        Check("cutscene 281 finished: The Black Coffin complete, Ghosts of the Past current", $"{GhostsOfThePast},0/{GhostsOfThePast}/0/True",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, TheBlackCoffin)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the 0x05C an update sent: its first params as a,b,...
        string Upd(int count)
        {
            var up = Of(0x05C);
            return up.Count == 0 ? "none" : string.Join(",", Enumerable.Range(0, count).Select(i => BitConverter.ToInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(i))));
        }
        Console.WriteLine("ToAU 16 Ghosts of the Past: Naja Salaheem's dress-up");
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())"); // a GM's heal after the fight
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3074, not properly dressed (param 7 = 0)", $"034:{NajaSalaheem}:3074:1,1/3074", $"{Ev()}/{Cur()}");
        EventEnd(3074, 1, 1);
        Check("the update with option 1: updateEvent(50, 1, 0, 0, 0, 1)", "50,1,0,0,0,1", Upd(6));
        EventEnd(3074, 10, 1);
        Check("option 10 (blue with gold trimming): najaDressUp bit 1, updateEvent(2, 1, 255, 0, 0, 0, 4095)", "2,1,255,0,0,0,4095", Upd(7));
        EventEnd(3074, 12, 1);
        Check("option 12 (pale colors with lace): bits 1 and 3 gathered, updateEvent(10, 1, 10, 0, 1, 1)", "10,1,10,0,1,1", Upd(6));
        EventEnd(3074, 2);
        // the finish's option 2 sets the next mission's status after complete
        Check("event 3074 finished (option 2): Ghosts of the Past complete, Guests of the Empire current at status 1",
              $"{GuestsOfTheEmpire},1/{GuestsOfTheEmpire}/1/True", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbMissionComplete(LogToAU, GhostsOfThePast)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 17 Guests of the Empire: dressed for the palace, the Imperial Whitegate");
        Talk(NajaSalaheem);
        Check("Naja Salaheem at status 1, not dressed: event 3080", $"034:{NajaSalaheem}:3080:1,1", Ev());
        EventEnd(3080);
        // GP_CLI_COMMAND_EQUIP_SET: the inventory slot into the equip slot
        void Equip(ushort itemId, byte equipSlot)
        {
            Lua($"player:addItem({itemId})");
            byte slot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == itemId).Slot;
            Send(0x050, 8, b => { b[4] = slot; b[5] = equipSlot; b[6] = (byte)Container.Inventory; });
        }
        Equip(Byrnie, 5);
        Equip(LeatherGloves, 6);
        Equip(LeatherTrousers, 7);
        Equip(LeatherHighboots, 8);
        Check("a Byrnie, leather gloves, trousers and highboots on (0x050): doRoyalPalaceArmorCheck", "true",
              Lua("report(tostring(require('scripts/zones/Aht_Urhgan_Whitegate/Shared').doRoyalPalaceArmorCheck(player)))"));
        Talk(NajaSalaheem);
        Check("Naja Salaheem, properly dressed: event 3076 (param 7 = 1)", $"034:{NajaSalaheem}:3076:1,0,0,0,0,0,0,1/3076", $"{Ev()}/{Cur()}");
        EventEnd(3076, 0);
        Check("event 3076 finished (option 0): mission status 2", $"{GuestsOfTheEmpire},2", Mission(LogToAU));
        Talk(NajaSalaheem);
        Check("Naja Salaheem at status 2: event 3077", $"034:{NajaSalaheem}:3077:1,0,0,0,0,0,0,1", Ev());
        EventEnd(3077);
        uint palaceGate = NpcId("Imperial_Whitegate");
        Near(palaceGate);
        int titleErrand = TitleId("OVJANGS_ERRAND_RUNNER");
        Talk(palaceGate);
        Check("the Imperial Whitegate, dressed with no weapon: event 3078", $"034:{palaceGate}:3078:0,1,0,0,0,0,0,1/3078", $"{Ev()}/{Cur()}");
        EventEnd(3078);
        Check("event 3078 finished: Guests of the Empire complete, Passing Glory current, an Imperial Mythril Piece, Ovjang's Errand Runner",
              $"{PassingGlory},0/{PassingGlory}/0/1/{titleErrand}", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Qty(ImperialMythrilPiece)}/{Lua("report(player:getTitle())")}");
        int todayToau = int.Parse(Lua("report(VanadielUniqueDay())"));
        // the script sets mustZone as a local var; getMustZone reads the char var, so it stays false (the day timer alone gates)
        Check("  the mustZone local var (getMustZone false), the Timer char var (tomorrow)", $"1/false/{todayToau + 1}",
              $"{c.LocalVars.GetValueOrDefault($"Mission[{LogToAU}][{PassingGlory}]mustZone")}/" +
              $"{Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {PassingGlory})))")}/{Var($"Mission[{LogToAU}][{PassingGlory}]Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 18 Passing Glory: a zone and a day, the Sentinels' second platform");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem before: event 3079, dialog 2 (param 8)", $"034:{NajaSalaheem}:3079:1,1,0,0,0,0,0,2", Ev());
        EventEnd(3079);
        Talk(NajaSalaheem);
        Check("Naja Salaheem again: dialog 0 (the alternate)", $"034:{NajaSalaheem}:3079:1,1", Ev());
        EventEnd(3079);
        TakeZoneLine("z1e0");
        TakeZoneLine(LineTo(Whitegate));
        vanaTicks += VanaTime.DAY;
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.5f, -56.0f);
        Tick(250);
        Check("trigger area 3 after the zone and the day: event 3090 with text table 0", $"034:{c.Id}:3090/0/3090", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3090, 1, 1);
        Check("the update with option 1: updateEvent(rank, 1, 0, 0, 0, 0, 1)", "1,1,0,0,0,0,1", Upd(7));
        EventEnd(3090);
        Check("event 3090 finished: Passing Glory complete, Sweets for the Soul current", $"{SweetsForTheSoul},0/{SweetsForTheSoul}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 19 Sweets for the Soul: Abquhbah, the Shaharat Teahouse");
        uint abquhbah = NpcId("Abquhbah");
        Near(abquhbah);
        Talk(abquhbah);
        Check("Abquhbah: event 3106 with text table 0", $"034:{abquhbah}:3106/0", $"{Ev()}/{EvTable()}");
        EventEnd(3106);
        Check("event 3106 finished: SeenAbquhbah", "1", Var($"Mission[{LogToAU}][{SweetsForTheSoul}]SeenAbquhbah"));
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3091", $"034:{NajaSalaheem}:3091:1,1", Ev());
        EventEnd(3091);
        int titleGuide = TitleId("KARABABAS_TOUR_GUIDE");
        Warp(80.0f, -6.0f, -110.0f, Whitegate);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(80.0f, -6.0f, -130.0f);
        Tick(250);
        Check("trigger area 5 (the teahouse): event 3092", $"034:{c.Id}:3092:1,1/3092", $"{Ev()}/{Cur()}");
        EventEnd(3092);
        Check("event 3092 finished: Sweets for the Soul complete, Teahouse Tumult current, Karababa's Tour Guide, the var cleared",
              $"{TeahouseTumult},0/{TeahouseTumult}/0/{titleGuide}/-", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua("report(player:getTitle())")}/" +
              Var($"Mission[{LogToAU}][{SweetsForTheSoul}]SeenAbquhbah"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 20 Teahouse Tumult: the Aydeewa Subterrane");
        WarpIn(356.5f, -0.4f, -179.6f, Aydeewa);
        Check("in the Aydeewa Subterrane: the mission's zone-in cutscene 10", $"{Aydeewa}/10", $"{c.Zone}/{Cur()}");
        EventEnd(10);
        Check("cutscene 10 finished: mission status 1", $"{TeahouseTumult},1", Mission(LogToAU));
        uint blank20 = NpcId("blank_toau20");
        Near(blank20);
        Talk(blank20);
        Check("the blank NPC (blank_toau20): event 11", $"034:{blank20}:11/11", $"{Ev()}/{Cur()}");
        EventEnd(11);
        Check("event 11 finished: Teahouse Tumult complete, Finders Keepers current", $"{FindersKeepers},0/{FindersKeepers}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 21 Finders Keepers: the Sentinels' second platform");
        Warp(35.0f, -6.5f, -70.0f, Whitegate);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.5f, -56.0f);
        Tick(250);
        Check("trigger area 3: event 3093", $"034:{c.Id}:3093:1,1/3093", $"{Ev()}/{Cur()}");
        EventEnd(3093);
        Check("event 3093 finished: Finders Keepers complete, Shield of Diplomacy current", $"{ShieldOfDiplomacy},0/{ShieldOfDiplomacy}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 22 Shield of Diplomacy: the Navukgo Execution Chamber, Khimaira 13");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3094", $"034:{NajaSalaheem}:3094:1,1", Ev());
        EventEnd(3094);
        WarpIn(-660.0f, -12.0f, -199.5f, Navukgo);
        Check("in the Navukgo Execution Chamber: the mission's zone-in cutscene 1", $"{Navukgo}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        uint navukgoDoor = NpcId("_1s0");
        Near(navukgoDoor);
        Talk(navukgoDoor);
        Check("the door (_1s0) at status 1: event 2", $"032:{navukgoDoor}:2/2", $"{Ev()}/{Cur()}");
        EventEnd(2);
        Check("event 2 finished: mission status 2 (the battlefield's requiredMissionStatus)", $"{ShieldOfDiplomacy},2", Mission(LogToAU));
        int titleBodyguard = TitleId("KARABABAS_BODYGUARD");
        WinBattlefield(navukgoDoor, "Shield of Diplomacy");
        Check("  the mission's 32001 section: Shield of Diplomacy complete, Social Graces current, Karababa's Bodyguard",
              $"{SocialGraces},0/{SocialGraces}/0/True", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbTitle(titleBodyguard)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 23 Social Graces: the Sentinels' second platform");
        Warp(35.0f, -6.5f, -70.0f, Whitegate);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(35.0f, -6.5f, -56.0f);
        Tick(250);
        Check("trigger area 3: event 3095", $"034:{c.Id}:3095:0,1,0,0,0,0,0,4/3095", $"{Ev()}/{Cur()}");
        EventEnd(3095, 1, 1);
        Check("the update with option 1: updateEvent(1, 1)", "1,1", Upd(2));
        EventEnd(3095);
        Check("event 3095 finished: Social Graces complete, Foiled Ambition current, its mustZone (a local var: getMustZone false) and Timer",
              $"{FoiledAmbition},0/{FoiledAmbition}/0/false/{int.Parse(Lua("report(VanadielUniqueDay())")) + 1}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {FoiledAmbition})))")}/" +
              Var($"Mission[{LogToAU}][{FoiledAmbition}]Timer"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the Sentinels' second platform (trigger area 3), walked onto from its north side
        void Platform()
        {
            Warp(35.0f, -6.5f, -70.0f, Whitegate);
            Tick(3000);
            s.PacketList.Clear();
            MoveTo(35.0f, -6.5f, -56.0f);
            Tick(250);
        }
        Console.WriteLine("ToAU 24 Foiled Ambition: Naja Salaheem, the platform the next day");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3096", $"034:{NajaSalaheem}:3096:1,1,0,0,0,1,0,2", Ev());
        EventEnd(3096);
        Platform();
        Check("trigger area 3 the same day: nothing (the Timer)", "none", Ev());
        vanaTicks += VanaTime.DAY;
        Platform();
        Check("trigger area 3 the next day: event 3097 with text table 0", $"034:{c.Id}:3097/0/3097", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3097, 0, 1);
        Check("the update: updateEvent(78, 1)", "78,1", Upd(2));
        int titleSecretAgent = TitleId("KARABABAS_SECRET_AGENT");
        EventEnd(3097);
        Check("event 3097 finished: Foiled Ambition complete, Playing the Part current, five Imperial Gold Pieces, Karababa's Secret Agent",
              $"{PlayingThePart},0/{PlayingThePart}/0/5/{titleSecretAgent}/{int.Parse(Lua("report(VanadielUniqueDay())")) + 1}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Qty(ImperialGoldPiece)}/{Lua("report(player:getTitle())")}/{Var($"Mission[{LogToAU}][{PlayingThePart}]Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 25 Playing the Part: Naja Salaheem's dialogs, the next day");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem the same day: event 3098, dialog 1", $"034:{NajaSalaheem}:3098:1,1,0,0,0,0,0,1", Ev());
        EventEnd(3098);
        Talk(NajaSalaheem);
        Check("again: dialog 2", $"034:{NajaSalaheem}:3098:1,1,0,0,0,0,0,2", Ev());
        EventEnd(3098);
        vanaTicks += VanaTime.DAY;
        Talk(NajaSalaheem);
        Check("the next day: event 3110", $"032:{NajaSalaheem}:3110/3110", $"{Ev()}/{Cur()}");
        EventEnd(3110);
        Check("event 3110 finished: Playing the Part complete, Seal of the Serpent current, the vars cleared", $"{SealOfTheSerpent},0/{SealOfTheSerpent}/0/-/-",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Var($"Mission[{LogToAU}][{PlayingThePart}]Timer")}/{Var($"Mission[{LogToAU}][{PlayingThePart}]Option")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 26 Seal of the Serpent: the Imperial Whitegate");
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3115", $"034:{NajaSalaheem}:3115:1,1", Ev());
        EventEnd(3115);
        Near(palaceGate);
        Talk(palaceGate);
        Check("the Imperial Whitegate with no weapon: event 3111", $"034:{palaceGate}:3111:0,1,1,0,0,0,0,128/3111", $"{Ev()}/{Cur()}");
        EventEnd(3111);
        Check("event 3111 finished: Seal of the Serpent complete, Misplaced Nobility current", $"{MisplacedNobility},0/{MisplacedNobility}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 27 Misplaced Nobility: the Aydeewa Subterrane");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3116", $"034:{NajaSalaheem}:3116:1,1", Ev());
        EventEnd(3116);
        Warp(356.5f, -0.4f, -179.6f, Aydeewa);
        Check("in the Aydeewa Subterrane, no zone-in cutscene", $"{Aydeewa}/none", $"{c.Zone}/{Cur()}");
        Near(blank20);
        Talk(blank20);
        Check("the blank NPC: event 12 (0, 4)", $"034:{blank20}:12:0,4/12", $"{Ev()}/{Cur()}");
        EventEnd(12);
        Check("event 12 finished: Misplaced Nobility complete, Bastion of Knowledge current", $"{BastionOfKnowledge},0/{BastionOfKnowledge}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 28 Bastion of Knowledge: the Walahra Temple");
        Warp(80.0f, 0.0f, 20.0f, Whitegate);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(80.0f, 0.0f, 40.0f);
        Tick(250);
        Check("trigger area 4: event 3112", $"034:{c.Id}:3112:1,1/3112", $"{Ev()}/{Cur()}");
        int titleAphmau = TitleId("APHMAUS_MERCENARY");
        EventEnd(3112);
        Check("event 3112 finished: Bastion of Knowledge complete, Puppet in Peril current, Aphmau's Mercenary", $"{PuppetInPeril},0/{PuppetInPeril}/0/{titleAphmau}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 29 Puppet in Peril: the Jade Sepulcher");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3117", $"034:{NajaSalaheem}:3117:1,1", Ev());
        EventEnd(3117);
        Warp(340.4f, -13.6f, -157.4f, JadeSepulcher);
        uint jadeDoor = NpcId("_1v0");
        Near(jadeDoor);
        Talk(jadeDoor);
        Check("the door (_1v0) at status 0: event 4 (0, 4)", $"034:{jadeDoor}:4:0,4/4", $"{Ev()}/{Cur()}");
        EventEnd(4);
        Check("event 4 finished: mission status 1 (the battlefield's requiredMissionStatus)", $"{PuppetInPeril},1", Mission(LogToAU));
        WinBattlefield(jadeDoor, "Puppet in Peril");
        Check("  the mission's 32001 section: Puppet in Peril complete, Prevalence of Pirates current",
              $"{PrevalenceOfPirates},0/{PrevalenceOfPirates}/0", $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 30 Prevalence of Pirates: Arrapago Reef");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3118", $"034:{NajaSalaheem}:3118:1,1", Ev());
        EventEnd(3118);
        WarpIn(-456.0f, -3.0f, -425.0f, ArrapagoReef);
        Check("in Arrapago Reef: the mission's zone-in cutscene 13", $"{ArrapagoReef}/13", $"{c.Zone}/{Cur()}");
        EventEnd(13);
        Check("cutscene 13 finished: mission status 1", $"{PrevalenceOfPirates},1", Mission(LogToAU));
        int periqiaPermit = Ki("PERIQIA_ASSAULT_AREA_ENTRY_PERMIT");
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(-458.0f, -3.0f, -405.0f);
        Tick(250);
        Check("trigger area 1 (the Cutter): event 14 (0, 4)", $"034:{c.Id}:14:0,4/14", $"{Ev()}/{Cur()}");
        EventEnd(14);
        Check("event 14 finished: Prevalence of Pirates complete, Shades of Vengeance current, the Periqia permit",
              $"{ShadesOfVengeance},0/{ShadesOfVengeance}/0/10", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbKeyItem(periqiaPermit)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 31 Shades of Vengeance: the Runic Seal into Periqia (instance 5600), the ten Lamiae");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3119", $"034:{NajaSalaheem}:3119:1,1", Ev());
        EventEnd(3119);
        Warp(-353.0f, -3.0f, -23.0f, CaedarvaMire);
        uint seal = NpcId("_273");
        Near(seal);
        Talk(seal);
        // no assault orders: xi.assault.onRunicTrigger falls to xi.instance.onTrigger's mission entry
        Check("the Runic Seal (_273): event 143 (79, -6, 0, 99, 3), INSTANCE_ID 5600", $"034:{seal}:143:79,{unchecked((uint)-6)},0,99,3/143/5600",
              $"{Ev()}/{Cur()}/{c.LocalVars.GetValueOrDefault("INSTANCE_ID")}");
        EventEnd(143, 0, 1);
        s.PacketList.Clear();
        Tick(250);
        var periqiaReg = Of(0x0BF).LastOrDefault();
        Check("the update (onAssaultUpdate: the level cap of option 0): the run loaded, instanceEntry(the seal, 4)", $"5600/4",
              $"{Lua("report(player:getInstance():getID())")}/{(periqiaReg == null ? "none" : BitConverter.ToUInt16(periqiaReg.Buffer, 6).ToString())}");
        EventEnd(143, 4);
        Rezone();
        Check("event 143 finished (option 4): in Periqia, in the run", $"{Periqia}/5600", $"{c.Zone}/{c.Instance?.Id}");
        s.PacketList.Clear();
        Tick(500);
        Check("afterInstanceRegister: the permit fades, the Dvucca fireflies", $"00/1", $"{DbKeyItem(periqiaPermit)}/{Qty(CageOfDvuccaFireflies)}");
        uint lamia = uint.Parse(Lua("report(zones[xi.zone.PERIQIA].mob.K23H1_LAMIA)"));
        Lua("player:setUnkillable(true)");
        for (uint i = 0; i < 10 && Cur() != "102"; i++)
        {
            KillByChar(lamia + i);
            // the despawn after the death (onMobDespawn: 1-4 progress)
            for (int t = 0; t < 20 && c.Instance.Info.MobById[lamia + i].IsSpawned; t++) Tick(1000);
        }
        Check("the Lamiae killed and despawned (onMobDespawn's progress) until 10: complete, mission status 1, event 102",
              $"True/{ShadesOfVengeance},1/102", $"{int.Parse(Lua("report(player:getInstance():getProgress())")) >= 10}/{Mission(LogToAU)}/{Cur()}");
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())"); // a GM's heal after the fight
        int titleNashmeira = TitleId("NASHMEIRAS_MERCENARY");
        EventEnd(102);
        Rezone(false);
        Check("event 102 finished: back in Caedarva Mire by the seal, the mission's zone-in cutscene 21", $"{CaedarvaMire}/{Periqia}/-253/21",
              $"{c.Zone}/{c.PrevZone}/{MathF.Round(c.X)}/{Cur()}");
        EventEnd(21);
        Check("cutscene 21 finished: Shades of Vengeance complete, In the Blood current, Nashmeira's Mercenary",
              $"{InTheBlood},0/{InTheBlood}/0/{titleNashmeira}", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 32 In the Blood: Naja Salaheem");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        int goldBefore = int.Parse(Qty(ImperialGoldPiece));
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3113", $"034:{NajaSalaheem}:3113:1,1/3113", $"{Ev()}/{Cur()}");
        EventEnd(3113);
        Check("event 3113 finished: In the Blood complete, Sentinels' Honor current, an Imperial Gold Piece, the Timer",
              $"{SentinelsHonor},0/{SentinelsHonor}/0/{goldBefore + 1}/{int.Parse(Lua("report(VanadielUniqueDay())")) + 1}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Qty(ImperialGoldPiece)}/{Var($"Mission[{LogToAU}][{SentinelsHonor}]Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 33 Sentinels' Honor: Naja Salaheem's dialogs, the next day");
        Talk(NajaSalaheem);
        Check("Naja Salaheem the same day: event 3120, dialog 0", $"034:{NajaSalaheem}:3120:1,1", Ev());
        EventEnd(3120);
        Talk(NajaSalaheem);
        Check("again: dialog 2", $"034:{NajaSalaheem}:3120:1,1,0,0,0,0,0,2", Ev());
        EventEnd(3120);
        vanaTicks += VanaTime.DAY;
        Talk(NajaSalaheem);
        Check("the next day: event 3130, dialog 2", $"034:{NajaSalaheem}:3130:1,1,0,0,0,0,0,2/3130", $"{Ev()}/{Cur()}");
        EventEnd(3130);
        Check("event 3130 finished: Sentinels' Honor complete, Testing the Waters current", $"{TestingTheWaters},0/{TestingTheWaters}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 34 Testing the Waters: the gold coin again from Jazaraat's Headstone, Arrapago Reef, Talacca Cove");
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3132", $"034:{NajaSalaheem}:3132:1,1", Ev());
        EventEnd(3132);
        // Lost Kingdom's after-completion section: the headstone's keyItem action gives the coin back
        Warp(-389.0f, 6.0f, -567.0f, CaedarvaMire);
        Near(headstone);
        s.PacketList.Clear();
        Talk(headstone);
        Check("Jazaraat's Headstone without the coin: the coin again (a keyItem action), KEYITEM_OBTAINED", $"10/{Text(CaedarvaMire, "KEYITEM_OBTAINED")}:{coin}",
              $"{DbKeyItem(coin)}/{Special()}");
        Warp(-456.0f, -3.0f, -425.0f, ArrapagoReef);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(-458.0f, -3.0f, -405.0f);
        Tick(250);
        Check("trigger area 1 with the coin: event 15", $"034:{c.Id}:15/15", $"{Ev()}/{Cur()}");
        EventEnd(15, 0);
        Check("event 15 finished with the wrong answer (option 0): setMustZone", "true",
              Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {TestingTheWaters})))"));
        MoveTo(-458.0f, -3.0f, -425.0f);
        Tick(250);
        s.PacketList.Clear();
        MoveTo(-458.0f, -3.0f, -405.0f);
        Tick(250);
        Check("trigger area 1 again before zoning: nothing", "none", Ev());
        Warp(20.0f, 0.0f, 24.0f, Nashmau);
        Warp(-456.0f, -3.0f, -425.0f, ArrapagoReef);
        Check("zoned: the mustZone cleared", "false", Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {TestingTheWaters})))"));
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(-458.0f, -3.0f, -405.0f);
        Tick(250);
        Check("trigger area 1 after zoning: event 15", $"034:{c.Id}:15/15", $"{Ev()}/{Cur()}");
        EventEnd(15, 1);
        Rezone(false);
        Check("event 15 finished (option 1): mission status 1, setPos into Talacca Cove, the mission's zone-in cutscene 106",
              $"{TalaccaCove}/106/{TestingTheWaters},1", $"{c.Zone}/{Cur()}/{Mission(LogToAU)}");
        int eye = Ki("PERCIPIENT_EYE"), titleTrove = TitleId("TREASURE_TROVE_TENDER");
        EventEnd(106);
        Check("cutscene 106 finished: Testing the Waters complete, Legacy of the Lost current, the coin gone, the Percipient Eye, Treasure Trove Tender",
              $"{LegacyOfTheLost},0/{LegacyOfTheLost}/0/00/10/{titleTrove}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbKeyItem(coin)}/{DbKeyItem(eye)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 35 Legacy of the Lost: Gessho in Talacca Cove");
        int titleMercy = TitleId("GESSHOS_MERCY");
        uint caveDoor = NpcId("_1l0");
        WinBattlefield(caveDoor, "Legacy of the Lost");
        Check("  the mission's 32001 section: Legacy of the Lost complete, Gaze of the Saboteur current, Gessho's Mercy",
              $"{GazeOfTheSaboteur},0/{GazeOfTheSaboteur}/0/True", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbTitle(titleMercy)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 36 Gaze of the Saboteur: the Hazhalm Testing Grounds");
        WarpIn(652.0f, -272.0f, -104.0f, Hazhalm);
        Check("in the Hazhalm Testing Grounds: the mission's zone-in cutscene 6", $"{Hazhalm}/6", $"{c.Zone}/{Cur()}");
        EventEnd(6);
        Check("cutscene 6 finished: mission status 1", $"{GazeOfTheSaboteur},1", Mission(LogToAU));
        uint hazhalmDoor = NpcId("_260");
        Near(hazhalmDoor);
        Talk(hazhalmDoor);
        Check("the door (_260): event 7", $"032:{hazhalmDoor}:7/7", $"{Ev()}/{Cur()}");
        EventEnd(7);
        Check("event 7 finished: the Percipient Eye gone, Gaze of the Saboteur complete, Path of Blood current",
              $"00/{PathOfBlood},0/{PathOfBlood}/0", $"{DbKeyItem(eye)}/{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 37 Path of Blood: the platform, a forced zone into Whitegate");
        Platform();
        Check("trigger area 3: event 3131", $"034:{c.Id}:3131:0,1/3131", $"{Ev()}/{Cur()}");
        EventEnd(3131, 0, 1);
        Check("the update: updateEvent(2, 1)", "2,1", Upd(2));
        EventEnd(3131);
        Rezone(false);
        // setPos with the zone it is in: a zoning all the same (CLuaBaseEntity::setPos zones on any zone argument);
        // SendToZone keeps the previous zone when the destination is the zone itself
        Check("event 3131 finished: mission status 1, setPos into Whitegate itself (a zoning, prevZone kept), the mission's zone-in cutscene 3220",
              $"{Whitegate}/{Hazhalm}/98/3220/{PathOfBlood},1", $"{c.Zone}/{c.PrevZone}/{MathF.Round(c.X)}/{Cur()}/{Mission(LogToAU)}");
        EventEnd(3220);
        Check("cutscene 3220 finished: Path of Blood complete, Stirrings of War current, its Timer", $"{StirringsOfWar},0/{StirringsOfWar}/0/{int.Parse(Lua("report(VanadielUniqueDay())")) + 1}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Var($"Mission[{LogToAU}][{StirringsOfWar}]Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 38 Stirrings of War: the teahouse the next day");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem the same day: the section's check fails, her default (no 3134)", "False", Ev().Contains(":3134"));
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        vanaTicks += VanaTime.DAY;
        Talk(NajaSalaheem);
        Check("the next day: event 3134", $"034:{NajaSalaheem}:3134:1,1", Ev());
        EventEnd(3134);
        Warp(80.0f, -6.0f, -110.0f, Whitegate);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(80.0f, -6.0f, -130.0f);
        Tick(250);
        Check("trigger area 5: event 3136 with text table 0", $"034:{c.Id}:3136/0/3136", $"{Ev()}/{EvTable()}/{Cur()}");
        EventEnd(3136);
        Check("event 3136 finished: Stirrings of War complete, Allied Rumblings current", $"{AlliedRumblings},0/{AlliedRumblings}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 39 Allied Rumblings: Ru'Lude Gardens' palace entrance");
        Warp(0.0f, 3.0f, 20.0f, Rulude);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(0.0f, 3.0f, 50.0f);
        Tick(250);
        Check("trigger area 1: event 10097", $"034:{c.Id}:10097/10097", $"{Ev()}/{Cur()}");
        EventEnd(10097);
        Check("event 10097 finished: Allied Rumblings complete, Unraveling Reason current, its Timer", $"{UnravelingReason},0/{UnravelingReason}/0/{int.Parse(Lua("report(VanadielUniqueDay())")) + 1}",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Var($"Mission[{LogToAU}][{UnravelingReason}]Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 40 Unraveling Reason: Pherimociel, the Wajaom Woodlands' three zone-ins");
        Near(Pherimociel);
        Talk(Pherimociel);
        Check("Pherimociel the same day: event 10099 (the ship not ready)", $"032:{Pherimociel}:10099", Ev());
        EventEnd(10099);
        Check("event 10099 finished: seenCS, setMustZone", "1/true",
              $"{c.LocalVars.GetValueOrDefault($"Mission[{LogToAU}][39]seenCS")}/{Lua($"report(tostring(xi.mission.getMustZone(player, {LogToAU}, {UnravelingReason})))")}");
        vanaTicks += VanaTime.DAY;
        Talk(Pherimociel);
        Check("Pherimociel the next day without zoning: nothing from the mission (no 10098, seenCS kept)", "False", Ev().Contains(":10098") || Ev().Contains(":10099"));
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        TakeZoneLine(LineTo(UpperJeuno));
        TakeZoneLine(LineTo(Rulude));
        Near(Pherimociel);
        Talk(Pherimociel);
        Check("zoned, the next day: event 10098 (the ship ready)", $"032:{Pherimociel}:10098/10098", $"{Ev()}/{Cur()}");
        EventEnd(10098, 1);
        Rezone(false);
        Check("option 1: mission status 1, setPos into the Wajaom Woodlands, its zone-in cutscene 11", $"{Wajaom}/11/{UnravelingReason},1",
              $"{c.Zone}/{Cur()}/{Mission(LogToAU)}");
        EventEnd(11, 0, 1);
        Check("  the update (option 0): updateEvent(0, 4, 0, 0, 0, 0, 0, 3)", "0,4,0,0,0,0,0,3", Upd(8));
        EventEnd(11);
        Rezone(false);
        Check("cutscene 11 finished: status 2, the Woodlands again, cutscene 21", $"{Wajaom}/21/{UnravelingReason},2", $"{c.Zone}/{Cur()}/{Mission(LogToAU)}");
        EventEnd(21);
        Rezone(false);
        Check("cutscene 21 finished: status 3, the Woodlands again, cutscene 22", $"{Wajaom}/22/{UnravelingReason},3", $"{c.Zone}/{Cur()}/{Mission(LogToAU)}");
        EventEnd(22);
        Check("cutscene 22 finished: Unraveling Reason complete, Light of Judgement current", $"{LightOfJudgement},0/{LightOfJudgement}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 41 Light of Judgement: Rodin-Comidin in Whitegate");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        uint rodin = NpcId("Rodin-Comidin");
        Near(rodin);
        Talk(rodin);
        Check("Rodin-Comidin: event 3137 with text table 0", $"034:{rodin}:3137/0/3137", $"{Ev()}/{EvTable()}/{Cur()}");
        int nyzulRoute = Ki("NYZUL_ISLE_ROUTE");
        EventEnd(3137, 0);
        Check("event 3137 finished (option 0): Light of Judgement complete, Path of Darkness current, the Nyzul Isle route",
              $"{PathOfDarkness},0/{PathOfDarkness}/0/10", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbKeyItem(nyzulRoute)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the Runic Seal (_20m) into Nyzul Isle: event 405 and its updates (createInstance, the run loaded at the end of
        // the tick, onInstanceCreatedCallback's instanceEntry and its updateEvent(405, 3, ...) ending the update loop),
        // 405 ended force-terminated (EVENT_CANCELLED_OPTION) into the entrance cutscene 116, its option 1 zoning in
        void EnterNyzul(ushort instanceId, uint p1, uint p2)
        {
            uint runicSeal = NpcId("_20m");
            Near(runicSeal);
            Talk(runicSeal);
            Check($"the Runic Seal (_20m): event 405 ({p1}, {(int)p2}, 0, 99, 5), INSTANCE_ID {instanceId}", $"034:{runicSeal}:405:{p1},{p2},0,99,5/405/{instanceId}",
                  $"{Ev()}/{Cur()}/{c.LocalVars.GetValueOrDefault("INSTANCE_ID")}");
            EventEnd(405, 0, 1);
            Check("  the update: createInstance queued (NYZUL_INSTANCE 1)", "1/nil",
                  $"{c.LocalVars.GetValueOrDefault("NYZUL_INSTANCE")}/{Lua("report(tostring(player:getInstance()))")}");
            s.PacketList.Clear();
            Tick(250);
            var reg = Of(0x0BF).LastOrDefault();
            Check("  loaded: instanceEntry(the seal, 4), updateEvent(405, 3, 3, 3, 3, 3, 3, 3)", $"{instanceId}/4/405,3,3,3,3,3,3",
                  $"{Lua("report(player:getInstance():getID())")}/{(reg == null ? "none" : BitConverter.ToUInt16(reg.Buffer, 6).ToString())}/{Upd(7)}");
            // (the script's own updateEvent(405, 3, ...): 405 in the first param)
            EventEnd(405, 1u << 30);
            Check("  405 ended force-terminated: the entrance cutscene 116 (2)", $"034:{runicSeal}:116:2/116", $"{Ev()}/{Cur()}");
            EventEnd(116, 1);
            Rezone();
            Check("  116 finished (option 1): in Nyzul Isle, in the run", $"{NyzulIsle}/{instanceId}", $"{c.Zone}/{c.Instance?.Id}");
            Tick(500);
        }

        Console.WriteLine("ToAU 42 Path of Darkness: Rodin-Comidin, the Alzadaal lamp, Nyzul Isle (instance 7700)");
        Near(rodin);
        Talk(rodin);
        Check("Rodin-Comidin with the route: event 3141 with text table 0", $"034:{rodin}:3141/0", $"{Ev()}/{EvTable()}");
        EventEnd(3141);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3148", $"034:{NajaSalaheem}:3148:1,1", Ev());
        EventEnd(3148);
        Warp(125.0f, -2.0f, 23.0f, Alzadaal);
        uint lamp = NpcId("blank_lamp");
        // npcs.yaml gives the lamp no status, so it loads as Disappear (as the C++'s kDefaultStatus) and a Talk to it is
        // refused: the mission's lamp is out of reach with the data as it stands. A GM's setStatus shows it.
        Near(lamp);
        Talk(lamp);
        Check("the blank lamp as loaded: Disappear, the Talk refused (no event)", $"{NpcEntity.StatusDisappear}/none", $"{Npc(lamp).Status}/{Ev()}");
        Lua($"GetNPCByID({lamp}):setStatus(xi.status.NORMAL)");
        Talk(lamp);
        Check("the blank lamp: event 6", $"032:{lamp}:6/6", $"{Ev()}/{Cur()}");
        EventEnd(6, 0);
        Check("event 6 finished (option 0): mission status 1 (the instance's registryRequirements)", $"{PathOfDarkness},1", Mission(LogToAU));
        EnterNyzul(7700, 58, unchecked((uint)-6));
        Check("  afterInstanceRegister: the route fades, the Undersea Ruins fireflies", $"00/1", $"{DbKeyItem(nyzulRoute)}/{Qty(UnderseaRuinsFireflies)}");
        uint amnaf = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].mob.AMNAF_BLU)"));
        uint psycheflayer = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].mob.AMNAF_PSYCHEFLAYER)"));
        string NyzulProgress() => Lua("report(player:getInstance():getProgress())");
        Lua("player:setUnkillable(true)");
        FightTo(amnaf, 45);
        for (int t = 0; t < 10 && c.Instance.Info.MobById[amnaf].IsSpawned; t++) Tick(1000);
        // at 50%: its onMobFight +10, onInstanceProgressUpdate (10) despawns it, its onMobDespawn +10
        Check("Amnaf (BLU) fought to 45%: +10, despawned by the progress update, +10 on its despawn", "20/False",
              $"{NyzulProgress()}/{c.Instance.Info.MobById[amnaf].IsSpawned}");
        // the stages between (Naja's escort, the lamps and the second Amnaf fight): a GM's setProgress to the last
        Lua("player:getInstance():setProgress(48)");
        Check("progress 48 (a GM's): Amnaf Psycheflayer spawned", "True", c.Instance.Info.MobById[psycheflayer].IsSpawned);
        KillByChar(psycheflayer);
        for (int t = 0; t < 20 && Cur() == "none" && c.Zone == NyzulIsle; t++) Tick(1000);
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())"); // a GM's heal after the fight
        Rezone(false);
        Check("Psycheflayer killed and despawned (+2): 50, complete, mission status 2, setPos(0, 0, 0) to Alzadaal, its zone-in cutscene 7",
              $"{Alzadaal}/7/{PathOfDarkness},2", $"{c.Zone}/{Cur()}/{Mission(LogToAU)}");
        int titleComrade = TitleId("NAJAS_COMRADE_IN_ARMS");
        EventEnd(7);
        Check("cutscene 7 finished: Path of Darkness complete, Fangs of the Lion current, Naja's Comrade-in-Arms",
              $"{FangsOfTheLion},0/{FangsOfTheLion}/0/{titleComrade}", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 43 Fangs of the Lion: the platform");
        Platform();
        Check("trigger area 3: event 3138", $"034:{c.Id}:3138:1,1/3138", $"{Ev()}/{Cur()}");
        EventEnd(3138, 0, 1);
        Check("the update: updateEvent(0, 1)", "0,1", Upd(2));
        int mirrorKi = Ki("MYTHRIL_MIRROR"), titleLoyalist = TitleId("NASHMEIRAS_LOYALIST");
        EventEnd(3138);
        Check("event 3138 finished: Fangs of the Lion complete, Nashmeira's Plea current, the Mythril Mirror, Nashmeira's Loyalist",
              $"{NashmeirasPlea},0/{NashmeirasPlea}/0/10/{titleLoyalist}", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{DbKeyItem(mirrorKi)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 44 Nashmeira's Plea: the lamp, Nyzul Isle (instance 7701), Raubahn's reraises, Razfahd, Alexander");
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem with the mirror: event 3149", $"034:{NajaSalaheem}:3149:1,1", Ev());
        EventEnd(3149);
        Warp(125.0f, -2.0f, 23.0f, Alzadaal);
        Near(lamp);
        Talk(lamp);
        Check("the blank lamp: event 8 (0, 4, 0, 0, 0, 1)", $"034:{lamp}:8:0,4,0,0,0,1/8", $"{Ev()}/{Cur()}");
        EventEnd(8, 0);
        Check("event 8 finished (option 0): mission status 1", $"{NashmeirasPlea},1", Mission(LogToAU));
        EnterNyzul(7701, 59, unchecked((uint)-10));
        uint raubahn = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].mob.RAUBAHN)"));
        uint razfahd = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].mob.RAZFAHD)"));
        uint alexander = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].mob.ALEXANDER)"));
        var raubahnMob = c.Instance.Info.MobById[raubahn];
        Lua("player:setUnkillable(true)");
        FightTo(razfahd, 45);
        Check("Razfahd fought to 45%: Perfect Defense's progress +1", "1/1", $"{NyzulProgress()}/{Lua($"report(GetMobByID({razfahd}, player:getInstance()):getLocalVar('perfectdef'))")}");
        for (int death = 1; death <= 3; death++)
        {
            KillByChar(raubahn);
            Check($"Raubahn's death {death}: its DEATH listener +1", $"{1 + death}", NyzulProgress());
            if (death == 3) break;
            // the listener's 12 s timer: back at full HP, its AI reset, the reraise counted
            for (int t = 0; t < 15 && raubahnMob.Hp == 0; t++) Tick(1000);
            Check($"  12 s on: Raubahn reraised (full HP, RERAISES {death})", $"True/{death}",
                  $"{raubahnMob.Hp == raubahnMob.MaxHp}/{Lua($"report(GetMobByID({raubahn}, player:getInstance()):getLocalVar('RERAISES'))")}");
        }
        Tick(1000);
        var alexanderMob = c.Instance.Info.MobById[alexander];
        Check("progress 4: Raubahn and Razfahd despawned, event 203, the char at the entry, Alexander spawned", $"203/True/-444",
              $"{Cur()}/{alexanderMob.IsSpawned}/{MathF.Round(c.X)}");
        if (c.IsInEvent) EventEnd(203);
        KillByChar(alexander);
        for (int t = 0; t < 20 && c.Zone == NyzulIsle; t++) Tick(1000);
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())"); // a GM's heal after the fight
        Rezone(false);
        Check("Alexander killed and despawned (+1): 5, complete, mission status 2, Alzadaal's zone-in cutscene 10",
              $"{Alzadaal}/10/{NashmeirasPlea},2", $"{c.Zone}/{Cur()}/{Mission(LogToAU)}");
        int titlePreventer = TitleId("PREVENTER_OF_RAGNAROK");
        EventEnd(10);
        Check("cutscene 10 finished: Nashmeira's Plea complete, Ragnarok current, Preventer of Ragnarok",
              $"{Ragnarok},0/{Ragnarok}/0/{titlePreventer}", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 45 Ragnarok: the platform");
        Platform();
        Check("trigger area 3: event 3139", $"034:{c.Id}:3139:0,1/3139", $"{Ev()}/{Cur()}");
        EventEnd(3139);
        Check("event 3139 finished: Ragnarok complete, Imperial Coronation current", $"{ImperialCoronation},0/{ImperialCoronation}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 46 Imperial Coronation: the Imperial Whitegate, the rings");
        Near(palaceGate);
        int title46 = int.Parse(Lua("report(player:getTitle())"));
        Talk(palaceGate);
        Check("the Imperial Whitegate, dressed with no weapon: event 3140 with the title", $"034:{palaceGate}:3140:0,{title46},0,0,0,0,0,1/3140", $"{Ev()}/{Cur()}");
        EventEnd(3140, 99, 1);
        Check("the update with option 99: the three rings offered", $"{BalrahnsRing},{UlthalamsRing},{JalzahnsRing}", Upd(3));
        EventEnd(3140, 1, 1);
        // setCharVar(name, value, expiry): the ring var, to expire at the next conquest tally
        long tally = long.Parse(Lua("report(NextConquestTally())"));
        Check("option 1: Balrahn's Ring, updateEvent(1, 1), the Ring var (NextConquestTally) with its expiry", $"1/1,1/{tally}/{tally}",
              $"{Qty(BalrahnsRing)}/{Upd(2)}/{Var($"Mission[{LogToAU}][45]Ring")}/{DbRow(db, "expiry", $"FROM char_vars WHERE charid = ? AND varname = 'Mission[{LogToAU}][45]Ring'")}");
        EventEnd(3140, 4, 1);
        Check("option 4: the Imperial Standard", $"1/1,1", $"{Qty(ImperialStandard)}/{Upd(2)}");
        EventEnd(3140);
        Check("event 3140 finished: Imperial Coronation complete, The Empress Crowned current", $"{TheEmpressCrowned},0/{TheEmpressCrowned}/0",
              $"{Mission(LogToAU)}/{DbMission(LogToAU)}");
        // Mission:complete's cleanup took the mission's vars, the Ring var with them: no scenario ring on record
        Talk(palaceGate);
        Check("the Imperial Whitegate after (the second section): event 3155 (the standard held, the Ring var cleaned up)", $"034:{palaceGate}:3155:0,1,0,0,0,0,0,1/-",
              $"{Ev()}/{Var($"Mission[{LogToAU}][45]Ring")}");
        EventEnd(3155);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 47 The Empress Crowned: the platform");
        Platform();
        Check("trigger area 3: event 3144", $"034:{c.Id}:3144:1,1/3144", $"{Ev()}/{Cur()}");
        EventEnd(3144, 0, 1);
        Check("the update: updateEvent(0, 1, 0, 0, 1, 1)", "0,1,0,0,1,1", Upd(6));
        int titleEternal = TitleId("ETERNAL_MERCENARY");
        EventEnd(3144);
        Check("event 3144 finished: The Empress Crowned complete, Eternal Mercenary current, the Glory Crown, the Eternal Mercenary title",
              $"{EternalMercenary},0/{EternalMercenary}/0/1/{titleEternal}", $"{Mission(LogToAU)}/{DbMission(LogToAU)}/{Qty(GloryCrown)}/{Lua("report(player:getTitle())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ToAU 48 Eternal Mercenary: Naja Salaheem");
        Warp(22.0f, -7.8f, -42.0f, Whitegate);
        Near(NajaSalaheem);
        Talk(NajaSalaheem);
        Check("Naja Salaheem: event 3151 (replaceDefault)", $"034:{NajaSalaheem}:3151:1,1", Ev());
        EventEnd(3151);
        Check("  Eternal Mercenary current, the last of the log", $"{EternalMercenary},0", Mission(LogToAU));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Chains of Promathia, chapter 8 ----
        Console.WriteLine("CoP 8-1 Garden of Antiquity: the login in Al'Taieu");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[3];
        Login();
        Check("the CoP char back in Al'Taieu on Garden of Antiquity, out of any event", $"{AlTaieu}/{GardenOfAntiquity},{0x70EEEEE}/none",
              $"{c.Zone}/{Mission(LogCoP)}/{Cur()}");
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));
        uint crystalHub = NpcId("_0x0");
        uint ruaern = uint.Parse(Lua("report(zones[xi.zone.ALTAIEU].mob.RUAERN)"));
        string Rubious() => Lua($"report(player:getMissionStatus({LogCoP}, xi.mission.status.COP.RUBIOUS)) report(player:getMissionStatus({LogCoP}, xi.mission.status.COP.CID))");
        Near(crystalHub);
        Talk(crystalHub);
        Check("the Crystalline Field (_0x0): cutscene 164", $"{crystalHub}:164/164", $"{EvId()}/{Cur()}");
        EventEnd(164);
        Check("cutscene 164 finished: Status 1", "1", MVar(GardenOfAntiquity, "Status"));
        Talk(crystalHub);
        Check("  the field again: nothing until the three towers (the default)", "False", $"{Cur() == "164" || Cur() == "100"}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // a tower's Rubious Crystal: the three Ru'Aerns popped (npcUtil.popFromQM, claimed by the char), killed by the
        // char (their onMobDeath counts them per tower), then the crystal again: its cutscene
        void Tower(string name, int option, string expectedCs, string bitsAfter)
        {
            Console.WriteLine($"CoP 8-1: the Rubious Crystal {name}");
            uint crystal = NpcId(name);
            uint first = ruaern + (crystal - NpcId("_0x1")) * 3;
            Near(crystal);
            Talk(crystal);
            var nms = Enumerable.Range(0, 3).Select(i => zone.ZoneInfoOf(AlTaieu).MobById[first + (uint)i]).ToList();
            Check($"{name}: An ominous shadow (OMINOUS_SHADOW), its three Ru'Aerns up and claimed", $"True/True/True",
                  $"{Special().Contains(Text(AlTaieu, "OMINOUS_SHADOW") + ":")}/{nms.All(m => m.IsSpawned && m.IsAlive)}/{nms.All(m => m.OwnerId == c.Id)}");
            Check("  triggerNpc and nmOffset on the char", $"{crystal}/{first}",
                  $"{Lua($"report(player:getLocalVar('Mission[{LogCoP}][{GardenOfAntiquity}]triggerNpc'))")}/{Lua($"report(player:getLocalVar('Mission[{LogCoP}][{GardenOfAntiquity}]nmOffset'))")}");
            Talk(crystal);
            Check("  the crystal again with them up: nothing", "none", Cur());
            foreach (var m in nms) KillByChar(m.Id);
            Check("  the three killed: nmDefeated's tower bit", "True",
                  $"{(int.Parse(Lua($"report(player:getLocalVar('Mission[{LogCoP}][{GardenOfAntiquity}]nmDefeated'))")) & 1 << option) != 0}");
            Near(crystal);
            Talk(crystal);
            int cs = int.Parse(expectedCs.Split(':')[2]);
            Check($"  the crystal: cutscene {cs}", string.Format(expectedCs, crystal) + $"/{cs}", $"{Ev()}/{Cur()}");
            EventEnd(cs);
            Check("  the tower's status bit (RUBIOUS, CID)", bitsAfter, Rubious());
            Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        }
        // CoP keeps the status nibbles across its missions (0x70EEEEE from The Warrior's Path): the CID nibble's bit 3
        // is the south tower's, the RUBIOUS nibble's bits 0-1 the east and west towers'
        Tower("_0x1", 0, "034:{0}:161", "0,15");
        Tower("_0x2", 2, "034:{0}:162:1", "2,15");
        Tower("_0x3", 1, "032:{0}:163", "3,15");

        Console.WriteLine("CoP 8-1: the Crystalline Field, the Grand Palace of Hu'Xzoi");
        Near(crystalHub);
        Talk(crystalHub);
        Check("the Crystalline Field with the three towers: cutscene 100", $"{crystalHub}:100/100", $"{EvId()}/{Cur()}");
        EventEnd(100, 1);
        Check("cutscene 100 finished (option 1): Status 2", "2", MVar(GardenOfAntiquity, "Status"));
        Rezone();
        Check("  setPos into the Grand Palace of Hu'Xzoi", $"{HuXzoi}/-20/-355", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        uint iya = NpcId("_iya"), iyb = NpcId("_iyb");
        Near(iya);
        Talk(iya);
        Check("the gate (_iya): event 1", $"{iya}:1/1", $"{EvId()}/{Cur()}");
        EventEnd(1);
        Check("event 1 finished: the Tavnazian Ring, Status 3", "1/3", $"{Qty(TavnazianRing)}/{MVar(GardenOfAntiquity, "Status")}");
        Talk(iya);
        Check("  the gate again: The gate does not respond (DOES_NOT_RESPOND)", "True/none",
              $"{Special().Contains(Text(HuXzoi, "DOES_NOT_RESPOND") + ":")}/{Cur()}");
        Near(iyb);
        Talk(iyb);
        Check("the gate (_iyb): event 2", $"{iyb}:2/2", $"{EvId()}/{Cur()}");
        EventEnd(2);
        Check("event 2 finished: Garden of Antiquity complete, A Fate Decided current", $"{AFateDecided},{0x3F0EEEEE}/{AFateDecided}",
              $"{Mission(LogCoP)}/{DbMission(LogCoP).Split('/')[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-2 A Fate Decided: Ixghrah at the gate (_iyq)");
        uint iyq = NpcId("_iyq");
        var ixghrah = zone.ZoneInfoOf(HuXzoi).MobById[uint.Parse(Lua("report(zones[xi.zone.GRAND_PALACE_OF_HUXZOI].mob.IXGHRAH)"))];
        Near(iya);
        Talk(iya);
        Check("the gate (_iya): The gate does not respond (A Fate Decided's, priority 1000)", "True/none",
              $"{Special().Contains(Text(HuXzoi, "DOES_NOT_RESPOND") + ":")}/{Cur()}");
        Near(iyq);
        Talk(iyq);
        Check("the gate (_iyq): Ixghrah spawned and claimed by the char, PRESENCE_HAS_DRAWN", "True/True/True",
              $"{ixghrah.IsSpawned && ixghrah.IsAlive}/{ixghrah.OwnerId == c.Id}/{Special().Contains(Text(HuXzoi, "PRESENCE_HAS_DRAWN") + ":")}");
        Lua("player:setUnkillable(true)");
        KillByChar(ixghrah.Id);
        Check("Ixghrah killed by the char: its onMobDeath, Status 1", "True/1", $"{ixghrah.IsDead}/{MVar(AFateDecided, "Status")}");
        Near(iyq);
        Talk(iyq);
        Check("the gate (_iyq): cutscene 3", $"{iyq}:3/3", $"{EvId()}/{Cur()}");
        EventEnd(3);
        Check("cutscene 3 finished: A Fate Decided complete, When Angels Fall current", $"{WhenAngelsFall}/{WhenAngelsFall}",
              $"{Mission(LogCoP).Split(',')[0]}/{DbMission(LogCoP).Split('/')[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-3 When Angels Fall: the Garden of Ru'Hmet's zone-in, the Ebon Panel");
        WarpIn(-419.995f, 0.0f, 248.483f, RuHmet);
        Check("in the Garden of Ru'Hmet: the mission's zone-in cutscene 201", $"{RuHmet}/201", $"{c.Zone}/{Cur()}");
        int amuletPrishe = Ki("MYSTERIOUS_AMULET_PRISHE");
        EventEnd(201);
        Check("cutscene 201 finished: the Mysterious Amulet (Prishe's), Status 1", "10/1", $"{DbKeyItem(amuletPrishe)}/{MVar(WhenAngelsFall, "Status")}");
        // the Hume's panel (_iz2: getRace 1 -> basic race 1)
        uint panel = NpcId("_iz2");
        Near(panel);
        Talk(panel);
        Check("the Ebon Panel (_iz2): event 202", $"{panel}:202/202", $"{EvId()}/{Cur()}");
        EventEnd(202);
        Check("event 202 finished: Status 2", "2", MVar(WhenAngelsFall, "Status"));
        foreach (var other in new[] { "Ebon_Panel_Elvaan", "Ebon_Panel_Mithra" })
        {
            uint o = NpcId(other);
            Near(o);
            Talk(o);
            Check($"  {other}: not the char's race's, nothing", "none", Cur());
        }
        var pnl = Npc(panel);
        MoveTo(pnl.X + 3.0f, pnl.Y, pnl.Z);
        Talk(panel);
        Check("the panel from 3 yalms: You must move closer (YOU_MUST_MOVE_CLOSER)", "True/none",
              $"{Special().Contains(Text(RuHmet, "YOU_MUST_MOVE_CLOSER") + ":")}/{Cur()}");
        int titleCrystal = TitleId("WARRIOR_OF_THE_CRYSTAL");
        Near(panel);
        Talk(panel);
        Check("the panel from 1.5 yalms: event 120", $"{panel}:120/120", $"{EvId()}/{Cur()}");
        EventEnd(120, 1);
        Check("event 120 finished (option 1): the Light of Vahzl, the Warrior of the Crystal title, Status 3", $"10/{titleCrystal}/3",
              $"{DbKeyItem(lightVahzl)}/{Lua("report(player:getTitle())")}/{MVar(WhenAngelsFall, "Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-3: the brands (_0zu, _0zv), the door (_0z0), When Angels Fall");
        int brandDawn = Ki("BRAND_OF_DAWN"), brandTwilight = Ki("BRAND_OF_TWILIGHT");
        foreach (var (name, ev, ki) in new[] { ("_0zu", 110, brandDawn), ("_0zv", 111, brandTwilight) })
        {
            uint b = NpcId(name);
            Near(b);
            Talk(b);
            Check($"{name}: cutscene {ev}", $"{b}:{ev}/{ev}", $"{EvId()}/{Cur()}");
            EventEnd(ev, 1);
            Check($"  option 1: the key item", "10", DbKeyItem(ki));
            Talk(b);
            Check("  and not again with it", "none", Cur());
        }
        uint gate0z0 = NpcId("_0z0");
        Near(gate0z0);
        Talk(gate0z0);
        Check("the door (_0z0): event 203", $"{gate0z0}:203/203", $"{EvId()}/{Cur()}");
        EventEnd(203);
        Check("event 203 finished: Status 4 (the battlefield's requiredVar)", "4", MVar(WhenAngelsFall, "Status"));
        var waf = EnterBattlefield(gate0z0, "When Angels Fall");
        uint ixzdei = uint.Parse(Lua("report(zones[xi.zone.THE_GARDEN_OF_RUHMET].mob.IXZDEI_RDM)"));
        var ixzdeis = Enumerable.Range(0, 4).Select(i => zone.ZoneInfoOf(RuHmet).MobById[ixzdei + (uint)i]).ToList();
        Check("  the four Ix'zdei up", "True", ixzdeis.All(m => m.IsSpawned && m.IsAlive));
        foreach (var m in ixzdeis) m.Hp = 0;
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("  the Ix'zdei dead: WON, onBattlefieldWin's cutscene 32001", "2/32001", $"{waf?.Status}/{Cur()}");
        EventEnd(32001);
        Check("  the mission's 32001 section: Status 5; onEventFinishWin's setPos in the zone", $"5/{RuHmet}/420/445",
              $"{MVar(WhenAngelsFall, "Status")}/{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        // the setPos's 0x05B sent (until then the client's position reports are stale: pendingPositionUpdate)
        s.PacketList.Clear();
        // CBattlefield::Cleanup lets the players go 10 s after onBattlefieldLeave
        for (int i = 0; i < 15 && c.PBattlefield != null; i++) Tick(1000);
        Check("  10 s on: out of the battlefield, its status effect gone", "False/False",
              $"{c.PBattlefield != null}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.BATTLEFIELD)))") == "true"}");
        Near(gate0z0);
        Talk(gate0z0);
        Check("the door after: event 205", $"{gate0z0}:205", EvId());
        EventEnd(205);
        uint zt = NpcId("_0zt");
        Near(zt);
        Talk(zt);
        Check("the door (_0zt): event 204", $"{zt}:204/204", $"{EvId()}/{Cur()}");
        EventEnd(204);
        Check("event 204 finished: Status 6", "6", MVar(WhenAngelsFall, "Status"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-3: Al'Taieu's zone-in");
        WarpIn(0.1f, -10.0f, -464.0f, AlTaieu);
        Check("in Al'Taieu: the mission's zone-in cutscene 165", $"{AlTaieu}/165", $"{c.Zone}/{Cur()}");
        EventEnd(165);
        Check("cutscene 165 finished: Prishe's amulet taken, RETURN_AMULET_TO_PRISHE (the Mysterious Amulet), When Angels Fall complete, Dawn current",
              $"00/True/{Dawn}/{Dawn}", $"{DbKeyItem(amuletPrishe)}/{Special().Contains($"{Text(AlTaieu, "RETURN_AMULET_TO_PRISHE")}:{amulet}")}/" +
              $"{Mission(LogCoP).Split(',')[0]}/{DbMission(LogCoP).Split('/')[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4 Dawn: the Garden of Ru'Hmet's portals (_0zy), trigger area 1 to the Empyreal Paradox");
        string DawnVar(string v) => MVar(Dawn, v);
        Warp(-419.995f, 0.0f, 248.483f, RuHmet);
        uint zy = NpcId("_0zy");
        var zyNpc = Npc(zy);
        MoveTo(zyNpc.X + 1.5f, zyNpc.Y, 355.0f);
        Talk(zy);
        Check("the portal (_0zy) from z 355 (the mission's section on Dawn): event 140", $"{zy}:140/140", $"{EvId()}/{Cur()}");
        EventEnd(140);
        MoveTo(zyNpc.X + 1.5f, zyNpc.Y, 365.0f);
        Talk(zy);
        Check("  from z 365: event 141", $"{zy}:141/141", $"{EvId()}/{Cur()}");
        EventEnd(141);
        MoveTo(-430.0f, -1.0f, 379.0f);
        Tick(3000);
        s.PacketList.Clear();
        MoveTo(-419.0f, -1.0f, 379.0f);
        Tick(400);
        Check("trigger area 1 (the zone's, on Dawn): event 101", $"{c.Id}:101/101", $"{EvId()}/{Cur()}");
        EventEnd(101, 1);
        Rezone();
        Check("event 101 finished (option 1): the Empyreal Paradox", $"{EmpyrealParadox}/540/-500", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        uint trEntrance = NpcId("TR_Entrance");
        Near(trEntrance);
        Talk(trEntrance);
        Check("the Transcendental Radiance (TR_Entrance): event 2", $"{trEntrance}:2/2", $"{EvId()}/{Cur()}");
        EventEnd(2);
        Check("event 2 finished: Status 1 (the battlefield's requiredVar)", "1", DawnVar("Status"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: Dawn, Promathia's two phases with Prishe and Selhteus");
        Lua("player:setUnkillable(true)");
        var dawnBf = EnterBattlefield(trEntrance, "Dawn");
        uint promathia = uint.Parse(Lua("report(zones[xi.zone.EMPYREAL_PARADOX].mob.PROMATHIA)"));
        var epInfo = zone.ZoneInfoOf(EmpyrealParadox);
        var prom1 = epInfo.MobById[promathia + (uint)(dawnBf.Area - 1) * 2];
        var prom2 = epInfo.MobById[promathia + (uint)(dawnBf.Area - 1) * 2 + 1];
        var allies = dawnBf.AllyList.ToList();
        Check("  area 1: Promathia up, the second phase not; setupBattlefield's allies (insertEntity 11 and 12) spawned at level 75, the initiator's race",
              "1/True/False/2/True/1", $"{dawnBf.Area}/{prom1.IsSpawned && prom1.IsAlive}/{prom2.IsSpawned}/{allies.Count}/" +
              $"{allies.All(a => a.IsSpawned && a.MLvl == 75)}/{Lua("report(player:getBattlefield():getLocalVar('initRace'))")}");
        FightTo(prom1.Id, 85);
        Check("  Promathia at 85%: onMobFight's Prishe reaction (prisheReact 1), the next Chains breakpoint 80", "1/80",
              $"{Lua("report(player:getBattlefield():getLocalVar('prisheReact'))")}/{Lua($"report(GetMobByID({prom1.Id}):getLocalVar('nextBreakpoint'))")}");
        KillByChar(prom1.Id);
        for (int i = 0; i < 10 && Cur() != "32004"; i++) Tick(1000);
        // luautils::OnMobDeath leaves the mob as the event preparation's target: the event is Promathia's
        Check("Promathia dead: its onMobDeath (phase 1), the group's allDeath: event 32004 (the area)", $"1/034:{prom1.Id}:32004:1/32004",
              $"{Lua("report(player:getBattlefield():getLocalVar('phase'))")}/{Ev()}/{Cur()}");
        EventEnd(32004);
        Check("event 32004 finished: onEventFinishBattlefield, the char by the second phase, Promathia's second form spawned", $"-520/494/True",
              $"{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{prom2.IsSpawned && prom2.IsAlive}");
        s.PacketList.Clear();
        int titleAverter = TitleId("AVERTER_OF_THE_APOCALYPSE");
        KillByChar(prom2.Id);
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("the second form dead: WON, onBattlefieldWin's cutscene 32001", "2/32001", $"{dawnBf.Status}/{Cur()}");
        EventEnd(32001);
        // dawn.lua's own onEventFinishWin (the setPos) replaces BattlefieldMission's, which gives the content's title:
        // no Averter of the Apocalypse
        Check("  the mission's 32001 section: Status 2; not the battlefield's title (its onEventFinishWin overridden)", $"2/False",
              $"{DawnVar("Status")}/{DbTitle(titleAverter)}");
        Rezone(false);
        Check("  onEventFinishWin's setPos into the Empyreal Paradox: the mission's zone-in cutscene 6", $"{EmpyrealParadox}/540/-514/6",
              $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Cur()}");
        EventEnd(6);
        Check("cutscene 6 finished: Status 3", "3", DawnVar("Status"));
        Rezone(false);
        Check("  setPos into the Empyreal Paradox again: the mission's zone-in cutscene 3", $"{EmpyrealParadox}/3", $"{c.Zone}/{Cur()}");
        int tear = Ki("TEAR_OF_ALTANA");
        EventEnd(3);
        Check("cutscene 3 finished: the Tear of Altana, Timer 1 (to JST midnight), Option 31, Status 4", "10/1/31/4",
              $"{DbKeyItem(tear)}/{DawnVar("Timer")}/{DawnVar("Option")}/{DawnVar("Status")}");
        long timerExpiry = long.Parse(DbRow(db, "expiry", $"FROM char_vars WHERE charid = ? AND varname = 'Mission[{LogCoP}][{Dawn}]Timer'"));
        Check("  the Timer's expiry: the next JST midnight (15:00 UTC)", "True/0",
              $"{timerExpiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds() && timerExpiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= 86400}/{(timerExpiry + 9 * 3600) % 86400}");
        Rezone();
        Check("  setPos into Al'Taieu", $"{AlTaieu}/0/-470", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: the Timer up to JST midnight");
        // a trigger area entered from outside it: the walk there, the zone's tick, the step in
        void EnterArea(ushort zoneId, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            if (c.Zone != zoneId) Warp(x0, y0, z0, zoneId);
            else MoveTo(x0, y0, z0);
            Tick(3000);
            s.PacketList.Clear();
            MoveTo(x1, y1, z1);
            Tick(250);
        }
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1 with the final cutscenes pending (Option 31): nothing", "none", Cur());
        Warp(-12.0f, -12.0f, 1.0f, Metalworks);
        Near(cid);
        Talk(cid);
        Check("Cid before the Timer is up: not Tenzen's cutscene 897", "False", $"{Cur() == "897"}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        // the earth clock past the next JST midnight: the host's (char_vars expiry) and the scripts' (JstMidnight)
        long earthShift = 0;
        zone.UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + earthShift;
        lua.SetGlobal("JstMidnight", l =>
        {
            long jst = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + earthShift + 9 * 3600;
            l.Push((double)(uint)((jst / 86400 + 1) * 86400 - 9 * 3600));
            return 1;
        });
        earthShift = timerExpiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
        Check("a JST midnight on: the Timer expired (getCharVar 0, the row deleted)", "0/-",
              $"{Lua($"report(player:getCharVar('Mission[{LogCoP}][{Dawn}]Timer'))")}/{DawnVar("Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: the final cutscenes, Tenzen (bit 4), Jabbos (bit 3), the Shikarees (bit 2)");
        Near(cid);
        Talk(cid);
        Check("Cid: Tenzen's cutscene 897", $"{cid}:897/897", $"{EvId()}/{Cur()}");
        EventEnd(897);
        Check("event 897 finished: Option bit 4 cleared", $"{31 & ~16}", DawnVar("Option"));
        WarpIn(160.9f, 11.0f, -55.7f, OldtonMovalpolos);
        Check("in Oldton Movalpolos: the Jabbos' zone-in cutscene 57", $"{OldtonMovalpolos}/57", $"{c.Zone}/{Cur()}");
        EventEnd(57);
        Check("cutscene 57 finished: Option bit 3 cleared", $"{15 & ~8}", DawnVar("Option"));
        WarpIn(17.5f, -16.0f, 88.8f, Mhaura);
        Check("in Mhaura: the Shikarees' zone-in cutscene 322", $"{Mhaura}/322", $"{c.Zone}/{Cur()}");
        EventEnd(322);
        Check("cutscene 322 finished: Option bit 2 cleared", $"{7 & ~4}", DawnVar("Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: the Chebukkis (bit 1), Chipmy-Popmy and the Warmachine");
        Warp(-183.0f, -2.8f, 73.9f, PortWindurst);
        uint chipmy = NpcId("Chipmy-Popmy");
        Near(chipmy);
        Talk(chipmy);
        Check("Chipmy-Popmy: event 619", $"{chipmy}:619/619", $"{EvId()}/{Cur()}");
        EventEnd(619);
        Check("event 619 finished: CProg 1", "1", DawnVar("CProg"));
        Warp(-345.2f, -3.2f, -976.6f, BibikiBay);
        uint warmachine = NpcId("Warmachine");
        Near(warmachine);
        Talk(warmachine);
        Check("the Warmachine: cutscene 43", $"{warmachine}:43/43", $"{EvId()}/{Cur()}");
        string redDrop = Lua("report(xi.item.RED_DROP)");
        EventEnd(43);
        Check("event 43 finished: a colored drop (RED_DROP + 0-7), Option bit 1 cleared, no drop owed", $"1/{3 & ~2}/-",
              $"{DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND itemId BETWEEN {redDrop} AND {int.Parse(redDrop) + 7}")}/" +
              $"{DawnVar("Option")}/{DawnVar("coloredDropId")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: Louverance (bit 0), Hinaree, Uleguerand Range, Southern San d'Oria's trigger area");
        Warp(-301.5f, -10.2f, 97.7f, SouthernSandoria);
        hinaree = NpcId("Hinaree");
        Near(hinaree);
        Talk(hinaree);
        Check("Hinaree: event 757", $"{hinaree}:757/757", $"{EvId()}/{Cur()}");
        EventEnd(757);
        Check("event 757 finished: LProg 1", "1", DawnVar("LProg"));
        WarpIn(379.0f, 23.0f, -62.6f, UleguerandRange);
        Check("in the Uleguerand Range: the zone-in cutscene 17", $"{UleguerandRange}/17", $"{c.Zone}/{Cur()}");
        EventEnd(17);
        Check("cutscene 17 finished: LProg 2", "2", DawnVar("LProg"));
        EnterArea(SouthernSandoria, -275.0f, -2.0f, 80.0f, -275.0f, -2.0f, 97.0f);
        Check("Southern San d'Oria's trigger area 1: event 758", $"{c.Id}:758/758", $"{EvId()}/{Cur()}");
        EventEnd(758);
        Check("event 758 finished: the Option bits all cleared", "-", DawnVar("Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: Ru'Lude Gardens' trigger area, the Jeuno palace door (_6s1), the rings");
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1: event 122", $"{c.Id}:122/122", $"{EvId()}/{Cur()}");
        EventEnd(122);
        Check("event 122 finished: Status 5", "5", DawnVar("Status"));
        Warp(-96.6f, -0.2f, 92.3f, UpperJeuno);
        uint s61 = NpcId("_6s1");
        Near(s61);
        Talk(s61);
        Check("the door (_6s1): event 129", $"{s61}:129/129", $"{EvId()}/{Cur()}");
        EventEnd(129);
        Check("event 129 finished: Status 6, firstRing 1", "6/1", $"{DawnVar("Status")}/{DawnVar("firstRing")}");
        ushort rajas = ushort.Parse(Lua("report(xi.item.RAJAS_RING)"));
        Talk(s61);
        Check("the door again: the ring offer, event 84 (firstRing)", $"{s61}:84/84", $"{EvId()}/{Cur()}");
        EventEnd(84, 4, 1);
        Check("  the update (option 4): updateEvent(the three rings)", $"{rajas},{Lua("report(xi.item.SATTVA_RING)")},{Lua("report(xi.item.TAMAS_RING)")}", Upd(3));
        EventEnd(84, 5);
        Check("event 84 finished (option 5): the Rajas Ring, firstRing cleared", "1/-", $"{Qty(rajas)}/{DawnVar("firstRing")}");
        Talk(s61);
        Check("  the door with a ring held: the mission passes, the door's own welcome (event 124)", $"{s61}:124/124", $"{EvId()}/{Cur()}");
        EventEnd(124);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("CoP 8-4: the Safehold's Walnut Door (_0qa), Lufaise Meadows' trigger area");
        Warp(111.0f, -41.0f, 41.0f, TavnazianSafehold);
        uint walnut = NpcId("_0qa");
        Near(walnut);
        Talk(walnut);
        Check("the Walnut Door (_0qa): event 543", $"{walnut}:543/543", $"{EvId()}/{Cur()}");
        EventEnd(543);
        Check("event 543 finished: Status 7", "7", DawnVar("Status"));
        int titleBanisher = TitleId("BANISHER_OF_EMPTINESS");
        EnterArea(Lufaise, 199.0f, -22.0f, 315.0f, 199.0f, -22.0f, 337.0f);
        Check("Lufaise Meadows' trigger area 1: cutscene 116", $"{c.Id}:116/116", $"{EvId()}/{Cur()}");
        EventEnd(116);
        Check("cutscene 116 finished: the Banisher of Emptiness title, Status 8", $"True/8/{Dawn}", $"{DbTitle(titleBanisher)}/{DawnVar("Status")}/{Mission(LogCoP).Split(',')[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Dawn's completion: Storms of Fate, Shadows of the Departed, Apocalypse Nigh (Jeuno quests) ----
        // the 0x036 TALKNUM messages (messageText) as their message ids
        string TalkNums() => string.Join(",", Of(0x036).Select(p => BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF));
        string JQuest(string name) => Lua($"report(player:getQuestStatus(xi.questLog.JEUNO, xi.quest.id.jeuno.{name}))");
        string JVar(int questId, string v) => Var($"Quest[3][{questId}]{v}");
        const int StormsOfFate = 86, ShadowsOfTheDeparted = 88, ApocalypseNigh = 89;
        Console.WriteLine("Storms of Fate: Ru'Lude Gardens' trigger area on Dawn (Status 8), Misareaux's _0p2, the Unstable Displacement");
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1: Storms of Fate's event 142", $"{c.Id}:142/142", $"{EvId()}/{Cur()}");
        EventEnd(142, 1);
        Check("event 142 finished (option 1): Storms of Fate accepted", "1", JQuest("STORMS_OF_FATE"));
        Warp(-259.0f, -30.0f, 270.0f, Misareaux);
        uint p02 = NpcId("_0p2");
        Near(p02);
        Talk(p02);
        Check("the Dilapidated Gate (_0p2): event 559", $"{p02}:559/559", $"{EvId()}/{Cur()}");
        EventEnd(559);
        Check("event 559 finished: Prog 1", "1", JVar(StormsOfFate, "Prog"));
        Warp(-612.8f, 1.75f, 690.0f, RiverneB01);
        uint displacement = NpcId("Unstable_Displacement");
        Near(displacement);
        Talk(displacement);
        Check("the Unstable Displacement: event 1", $"{displacement}:1/1", $"{EvId()}/{Cur()}");
        EventEnd(1);
        Check("event 1 finished: Prog 2 (the battlefield's requiredVar)", "2", JVar(StormsOfFate, "Prog"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Storms of Fate: Bahamut, his taunts and Megaflare");
        Lua("player:setUnkillable(true)");
        var sof = EnterBattlefield(displacement, "Storms of Fate");
        var bahamut = zone.ZoneInfoOf(RiverneB01).MobById[uint.Parse(Lua("report(zones[xi.zone.RIVERNE_SITE_B01].mob.BAHAMUT)"))];
        Check("  Bahamut up", "True", bahamut.IsSpawned && bahamut.IsAlive);
        string taunt = Text(RiverneB01, "BAHAMUT_TAUNT");
        ClaimBySwing(bahamut.Id);
        s.PacketList.Clear();
        bahamut.Hp = bahamut.MaxHp * 85 / 100;
        Tick(1000);
        Check("Bahamut at 85%: onMobFight's first taunt (messageText), his actions off, messagePlayed 1", $"True/1",
              $"{TalkNums().Split(',').Contains(taunt)}/{Lua($"report(GetMobByID({bahamut.Id}):getLocalVar('messagePlayed'))")}");
        for (int i = 0; i < 6 && Lua($"report(GetMobByID({bahamut.Id}):getLocalVar('phase'))") == "1"; i++) Tick(1000);
        Check("  the mob:timer's second taunt (+1), then Megaflare within 8 yalms: phase 2, messagePlayed 0", $"True/2/0",
              $"{TalkNums().Split(',').Contains((int.Parse(taunt) + 1).ToString())}/{Lua($"report(GetMobByID({bahamut.Id}):getLocalVar('phase'))")}/" +
              $"{Lua($"report(GetMobByID({bahamut.Id}):getLocalVar('messagePlayed'))")}");
        int whisper = Ki("WHISPER_OF_THE_WYRMKING"), titleConqueror = TitleId("CONQUEROR_OF_FATE");
        KillByChar(bahamut.Id);
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("Bahamut dead: WON, onBattlefieldWin's cutscene 32001", "2/32001", $"{sof?.Status}/{Cur()}");
        EventEnd(32001);
        Check("  the quest's 32001 section: the Whisper of the Wyrmking, Prog 3; the battlefield's title (BattlefieldQuest's onEventFinishWin)", "10/3/True",
              $"{DbKeyItem(whisper)}/{JVar(StormsOfFate, "Prog")}/{DbTitle(titleConqueror)}");
        s.PacketList.Clear();
        for (int i = 0; i < 15 && c.PBattlefield != null; i++) Tick(1000);
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1: event 143", $"{c.Id}:143/143", $"{EvId()}/{Cur()}");
        EventEnd(143);
        Check("event 143 finished: Storms of Fate complete, Shadows of the Departed's Timer the next Vana'diel day", $"2/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{JQuest("STORMS_OF_FATE")}/{JVar(ShadowsOfTheDeparted, "Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Shadows of the Departed: the slivers of the three Promyvions");
        // the Zilart missions done to Awakening, status 3: !addmission 3 (Shadows of the Departed's check); addMission
        // warns on the Zilart log's 0xFFFF (only the logs past it start at 0)
        expectedWarnings.Add("lua: addMission: overwriting current mission 65535 of log 3 with 30");
        Lua("player:addMission(xi.mission.log_id.ZILART, xi.mission.id.zilart.AWAKENING) player:setMissionStatus(xi.mission.log_id.ZILART, 3)");
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1 the same day: nothing", "none", Cur());
        vanaTicks += VanaTime.DAY;
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("  the next day: event 161", $"{c.Id}:161/161", $"{EvId()}/{Cur()}");
        int noteEshantarl = Ki("NOTE_WRITTEN_BY_ESHANTARL");
        EventEnd(161);
        Check("event 161 finished: the Note Written by Eshantarl, Shadows of the Departed accepted", "10/1", $"{DbKeyItem(noteEshantarl)}/{JQuest("SHADOWS_OF_THE_DEPARTED")}");
        int sliverHolla = Ki("PROMYVION_HOLLA_SLIVER");
        foreach (var (zoneId, npcName, x, y, z, ki) in new[] { (PromyvionDem, "_0id", 220.0f, -2.5f, 337.0f, sliverHolla + 1), (PromyvionHolla, "_0gc", 100.0f, -3.0f, -143.0f, sliverHolla),
                                                                (PromyvionMea, "_0k0", 260.0f, -2.5f, 177.0f, sliverHolla + 2) })
        {
            Warp(x, y, z, zoneId);
            uint n = NpcId(npcName);
            Near(n);
            Talk(n);
            Check($"{npcName}: the sliver (quest:keyItem), KEYITEM_OBTAINED", $"10/True", $"{DbKeyItem(ki)}/{Special().Contains($"{Text(zoneId, "KEYITEM_OBTAINED")}:{ki}")}");
            if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        }
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1 with the three slivers: event 162", $"{c.Id}:162/162", $"{EvId()}/{Cur()}");
        EventEnd(162);
        Check("event 162 finished: Shadows of the Departed complete, the slivers handed over (YOU_HAND_THE_THREE_SLIVERS), Apocalypse Nigh's Timer and mustZone",
              $"2/00/00/00/True/{VanaTime.CountDays(vanaTicks) + 1}/true",
              $"{JQuest("SHADOWS_OF_THE_DEPARTED")}/{DbKeyItem(sliverHolla)}/{DbKeyItem(sliverHolla + 1)}/{DbKeyItem(sliverHolla + 2)}/" +
              $"{Special().Contains(Text(Rulude, "YOU_HAND_THE_THREE_SLIVERS") + ":")}/{JVar(ApocalypseNigh, "Timer")}/" +
              $"{Lua("report(tostring(xi.quest.getMustZone(player, xi.questLog.JEUNO, xi.quest.id.jeuno.APOCALYPSE_NIGH)))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Apocalypse Nigh: Ru'Lude Gardens, Sealion's Den, the Grand Palace's gate, the Transcendental Radiance");
        vanaTicks += VanaTime.DAY;
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1 the next day without a zone (mustZone): nothing", "none", Cur());
        Warp(-96.6f, -0.2f, 92.3f, UpperJeuno);
        EnterArea(Rulude, 0.0f, 3.0f, 20.0f, 0.0f, 3.0f, 50.0f);
        Check("Ru'Lude Gardens' trigger area 1 the next day, after a zone: event 123", $"{c.Id}:123/123", $"{EvId()}/{Cur()}");
        EventEnd(123);
        Check("event 123 finished: Apocalypse Nigh accepted", "1", JQuest("APOCALYPSE_NIGH"));
        WarpIn(612.0f, 132.7f, 776.9f, SealionsDen);
        // the first onZoneIn cutscene of the handlers wins: The Warrior's Path's (its Option 1 since Al'Taieu) before the quest's
        Check("in Sealion's Den: The Warrior's Path's after-cutscene 18 first (Option 1)", $"{SealionsDen}/18/1", $"{c.Zone}/{Cur()}/{MVar(TheWarriorsPath, "Option")}");
        EventEnd(18);
        Check("cutscene 18 finished: Option cleared", "-", MVar(TheWarriorsPath, "Option"));
        WarpIn(612.0f, 132.7f, 776.9f, SealionsDen);
        Check("in Sealion's Den again: the quest's zone-in cutscene 29", $"{SealionsDen}/29", $"{c.Zone}/{Cur()}");
        EventEnd(29, 0, 1);
        Check("  the update (option 0): updateEvent(0, 0, 0, 0, 32, 3, 0, 0)", "0,0,0,0,32,3,0,0", Upd(8));
        EventEnd(29);
        Check("cutscene 29 finished: Prog 1", "1", JVar(ApocalypseNigh, "Prog"));
        Warp(-20.0f, 0.6f, -355.0f, HuXzoi);
        Near(iya);
        Talk(iya);
        Check("the gate (_iya): event 4", $"{iya}:4/4", $"{EvId()}/{Cur()}");
        EventEnd(4);
        Check("event 4 finished: Prog 2", "2", JVar(ApocalypseNigh, "Prog"));
        Rezone();
        Check("  setPos into the Garden of Ru'Hmet", $"{RuHmet}/-420/248", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        EnterArea(RuHmet, -430.0f, -1.0f, 379.0f, -419.0f, -1.0f, 379.0f);
        Check("trigger area 1: event 101", $"{c.Id}:101/101", $"{EvId()}/{Cur()}");
        EventEnd(101, 1);
        Rezone();
        Near(trEntrance);
        Talk(trEntrance);
        Check("the Transcendental Radiance: event 4", $"{trEntrance}:4/4", $"{EvId()}/{Cur()}");
        EventEnd(4);
        Check("event 4 finished: Prog 3 (the battlefield's requiredVar)", "3", JVar(ApocalypseNigh, "Prog"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Apocalypse Nigh: Kam'lanaut and Eald'narche");
        Lua("player:setUnkillable(true)");
        var apo = EnterBattlefield(trEntrance, "Apocalypse Nigh");
        uint kamlanaut = uint.Parse(Lua("report(zones[xi.zone.EMPYREAL_PARADOX].mob.KAMLANAUT)")) + (uint)((apo?.Area ?? 1) - 1) * 2;
        var kams = new[] { epInfo.MobById[kamlanaut], epInfo.MobById[kamlanaut + 1] };
        Check("  the area's pair up", $"1/True", $"{apo?.Area}/{kams.All(m => m.IsSpawned && m.IsAlive)}");
        foreach (var m in kams) KillByChar(m.Id);
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("the pair dead: WON, onBattlefieldWin's cutscene 32001", "2/32001", $"{apo?.Status}/{Cur()}");
        EventEnd(32001);
        Check("  the quest's 32001 section: Prog 4, the Timer the next day", $"4/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{JVar(ApocalypseNigh, "Prog")}/{JVar(ApocalypseNigh, "Timer")}");
        Rezone(false);
        Check("  onEventFinishWin's setPos into the Empyreal Paradox: the quest's zone-in cutscene 7", $"{EmpyrealParadox}/7", $"{c.Zone}/{Cur()}");
        EventEnd(7, 0, 1);
        Check("  the update (option 0): updateEvent(36, 1430550, 2964, 1013, 36, 1, 0, 0)", "36,1430550,2964,1013,36,1,0,0", Upd(8));
        int titleBreaker = TitleId("BREAKER_OF_THE_CHAINS");
        EventEnd(7);
        Check("cutscene 7 finished: the Breaker of the Chains title", "True", DbTitle(titleBreaker));
        Rezone();
        Check("  setPos into Al'Taieu", $"{AlTaieu}", c.Zone);
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Apocalypse Nigh: Aldo, Norg's _700, Gilgamesh: Dawn complete");
        Warp(20.0f, 3.0f, -58.0f, LowerJeuno);
        aldo = NpcId("Aldo");
        Near(aldo);
        Talk(aldo);
        Check("Aldo at rank 1: not the quest's event 10057", "False", $"{Cur() == "10057"}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        // a GM's !setrank 5 (Aldo's rank 5 condition)
        Lua("player:setRank(5)");
        Talk(aldo);
        Check("Aldo at rank 5: event 10057", $"{aldo}:10057/10057", $"{EvId()}/{Cur()}");
        EventEnd(10057);
        Check("event 10057 finished: Prog 5", "5", JVar(ApocalypseNigh, "Prog"));
        Warp(122.5f, -9.0f, -12.0f, Norg);
        uint gilgamesh = NpcId("Gilgamesh"), door700 = NpcId("_700");
        Near(door700);
        Talk(door700);
        Check("Norg's door (_700) before the Timer: event 235", $"{door700}:235/235", $"{EvId()}/{Cur()}");
        EventEnd(235);
        vanaTicks += VanaTime.DAY;
        Near(gilgamesh);
        Talk(gilgamesh);
        Check("Gilgamesh the next day without a zone (mustZone): not event 232", "False", $"{Cur() == "232"}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        // xi.player.onGameIn clears a mustZone set in another zone than the one entered: Norg to Norg keeps it
        Warp(122.5f, -9.0f, -12.0f, Norg);
        Near(gilgamesh);
        Talk(gilgamesh);
        Check("Gilgamesh after Norg's own zone line (the mustZone set in Norg kept): not event 232", "False/252",
              $"{Cur() == "232"}/{JVar(ApocalypseNigh, "mustZone")}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Warp(20.0f, 3.0f, -58.0f, LowerJeuno);
        Warp(122.5f, -9.0f, -12.0f, Norg);
        Near(gilgamesh);
        Talk(gilgamesh);
        Check("Gilgamesh after a zone elsewhere: event 232 (252, 6)", $"034:{gilgamesh}:232:252,6/232", $"{Ev()}/{Cur()}");
        EventEnd(232, 99, 1);
        ushort staticEarring = ushort.Parse(Lua("report(xi.item.STATIC_EARRING)"));
        Check("  the update (option 99): updateEvent(252, the four earrings)", $"252,{staticEarring},{staticEarring + 1},{staticEarring + 2},{staticEarring + 3}", Upd(5));
        EventEnd(232, 1);
        Check("event 232 finished (option 1): the Static Earring, Apocalypse Nigh complete; Dawn's 232 handler: Dawn complete, The Last Verse current",
              $"1/2/{TheLastVerse}/{TheLastVerse}", $"{Qty(staticEarring)}/{JQuest("APOCALYPSE_NIGH")}/{Mission(LogCoP).Split(',')[0]}/{DbMission(LogCoP).Split('/')[0]}");
        Talk(gilgamesh);
        Check("Gilgamesh after: event 233 (the quest's replaceDefault)", $"{gilgamesh}:233", EvId());
        EventEnd(233);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Abyssea, from First Contact ----
        Console.WriteLine("Abyssea First Contact: the login in Port Jeuno, the ??? in La Theine Plateau at night");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[2];
        Login();
        ushort ZoneOf(string name) => ushort.Parse(Lua($"report(xi.zone.{name})"));
        // the scripts' Vana'diel hour on the host's test clock from here on
        lua.SetGlobal("VanadielHour", l => { l.Push((double)VanaTime.GetHour(vanaTicks)); return 1; });
        Check("the Abyssea char back in Port Jeuno on First Contact, three zone quests done", $"{PortJeuno}/1/3",
              $"{c.Zone}/{Quest("FIRST_CONTACT")}/{new[] { "A_GOLDSTRUCK_GIGAS", "TO_PASTE_A_PEISTE", "MEGADRILE_MENACE" }.Count(q => Quest(q) == "2")}");
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));
        // 20:00 Vana'diel time
        vanaTicks += ((20 * VanaTime.HOUR - vanaTicks % VanaTime.DAY) % VanaTime.DAY + VanaTime.DAY) % VanaTime.DAY;
        Check("  the Vana'diel hour 20", "20", Lua("report(VanadielHour())"));
        Warp(-179.0f, 8.8f, 252.0f, LaTheine);
        uint laTheineQm = NpcId("qm3");
        Near(laTheineQm);
        Talk(laTheineQm);
        // First Contact's (and Tenuous Existence's, A Sea Dog's Summons') section check: the hour from 18:00 to 4:59
        // (user ruling 2026-11-04, kept Lua bugs: the Lua's `>= 18 and < 5` never held; the C# quests read an `or`)
        Check("the ??? (qm3) at 20:00: First Contact's event 11", "11", Cur());
        EventEnd(11);
        Check("event 11 finished: First Contact complete, An Officer and a Pirate accepted", "2/1",
              $"{Quest("FIRST_CONTACT")}/{Quest("AN_OFFICER_AND_A_PIRATE")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea An Officer and a Pirate, The Forbidden Frontier: Joachim");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: An Officer and a Pirate's event 334", $"{Joachim}:334/334", $"{EvId()}/{Cur()}");
        EventEnd(334);
        Check("event 334 finished: An Officer and a Pirate complete, Heart of Madness accepted", "2/1", $"{Quest("AN_OFFICER_AND_A_PIRATE")}/{Quest("HEART_OF_MADNESS")}");
        Talk(Joachim);
        Check("Joachim: The Forbidden Frontier's event 331 (one of its zone quests started)", $"{Joachim}:331/331", $"{EvId()}/{Cur()}");
        EventEnd(331);
        Check("event 331 finished: The Forbidden Frontier accepted", "1", Quest("THE_FORBIDDEN_FRONTIER"));
        Talk(Joachim);
        Check("Joachim: event 332 (its three zone quests done)", $"{Joachim}:332/332", $"{EvId()}/{Cur()}");
        EventEnd(332);
        Check("event 332 finished: The Forbidden Frontier complete", "2", Quest("THE_FORBIDDEN_FRONTIER"));
        Talk(Joachim);
        Check("Joachim with three zone quests (Heart of Madness wants five): not event 335", "False", $"{Cur() == "335"}");
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the other six zone quests (Heroes of Abyssea's and Scars of Abyssea's) and the two meta quests
        ushort altepa = ZoneOf("ABYSSEA_ALTEPA"), uleguerand = ZoneOf("ABYSSEA_ULEGUERAND"), grauberg = ZoneOf("ABYSSEA_GRAUBERG"),
               attohwa = ZoneOf("ABYSSEA_ATTOHWA"), vunkerl = ZoneOf("ABYSSEA_VUNKERL"), misareauxA = ZoneOf("ABYSSEA_MISAREAUX");
        ZoneQuest("A_BEAKED_BLUSTERER", "BENNU_DEPLUMER", SouthGustaberg, 17216200, 0, 914, altepa, 17670554, 1);
        Console.WriteLine("Abyssea Heroes of Abyssea: Joachim");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: Heroes of Abyssea's event 340 (A Beaked Blusterer started)", $"{Joachim}:340/340", $"{EvId()}/{Cur()}");
        EventEnd(340);
        Check("event 340 finished: Heroes of Abyssea accepted", "1", Quest("HEROES_OF_ABYSSEA"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        ZoneQuest("A_MAN_EATING_MITE", "RESHEPH_ERADICATOR", ZoneOf("XARCABARD"), 17236347, 58, 204, uleguerand, 17813913, 59);
        ZoneQuest("AN_ULCEROUS_URAGNITE", "AMPHITRITE_SHUCKER", ZoneOf("NORTH_GUSTABERG"), 17212109, 0, 908, grauberg, 17818054, 1);

        Console.WriteLine("Abyssea Heart of Madness: five zone quests, Joachim");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: Heart of Madness's event 335 (five zone quests)", $"{Joachim}:335/335", $"{EvId()}/{Cur()}");
        EventEnd(335);
        Check("event 335 finished: Heart of Madness complete, Tenuous Existence accepted", "2/1", $"{Quest("HEART_OF_MADNESS")}/{Quest("TENUOUS_EXISTENCE")}");
        Talk(Joachim);
        Check("Joachim: Heroes of Abyssea's event 341 (its three done)", $"{Joachim}:341/341", $"{EvId()}/{Cur()}");
        EventEnd(341);
        Check("event 341 finished: Heroes of Abyssea complete", "2", Quest("HEROES_OF_ABYSSEA"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea Tenuous Existence: the ??? at night, Joachim");
        // a day on from First Contact's 20:00 talk
        vanaTicks += 24 * VanaTime.HOUR;
        Warp(-179.0f, 8.8f, 252.0f, LaTheine);
        Near(laTheineQm);
        Talk(laTheineQm);
        Check("the ??? at 20:00: Tenuous Existence's event 12 (the same hour window)", "20/12", $"{Lua("report(VanadielHour())")}/{Cur()}");
        EventEnd(12);
        Check("event 12 finished: Tenuous Existence at Prog 1", "1", QVar("TENUOUS_EXISTENCE", "Prog"));
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: event 336", $"{Joachim}:336/336", $"{EvId()}/{Cur()}");
        EventEnd(336);
        Check("event 336 finished: Tenuous Existence complete, Champions of Abyssea accepted", "2/1", $"{Quest("TENUOUS_EXISTENCE")}/{Quest("CHAMPIONS_OF_ABYSSEA")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        ZoneQuest("A_FLUTTERY_FIEND", "ITZPAPALOTL_DECLAWER", ZoneOf("BUBURIMU_PENINSULA"), 17261213, 62, 61, attohwa, 17658277, 63);
        Console.WriteLine("Abyssea Scars of Abyssea: Joachim");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        // two progress events on offer: the interaction framework cycles them, a talk each
        var joachimEvents = new List<string>();
        for (int i = 0; i < 2 && c.IsInEvent; i++)
        {
            joachimEvents.Add(Cur());
            EventEnd(c.CurrentEvent.EventId);
            Talk(Joachim);
        }
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Check("Joachim twice: Scars of Abyssea's event 337 (A Fluttery Fiend started), Champions of Abyssea's 342 (seven zone quests)", "337,342",
              string.Join(",", joachimEvents.OrderBy(e => e)));
        Check("  Scars of Abyssea accepted, Champions of Abyssea complete, A Sea Dog's Summons accepted", "1/2/1",
              $"{Quest("SCARS_OF_ABYSSEA")}/{Quest("CHAMPIONS_OF_ABYSSEA")}/{Quest("A_SEA_DOGS_SUMMONS")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        ZoneQuest("THE_BEAST_OF_BASTORE", "SEDNA_TUSKBREAKER", ZoneOf("JUGNER_FOREST"), 17203935, 48, 47, vunkerl, 17666500, 49);

        Console.WriteLine("Abyssea A Sea Dog's Summons: the Hall of the Gods at night");
        vanaTicks += 24 * VanaTime.HOUR;
        WarpIn(0.0f, -12.0f, 48.0f, HallOfTheGods);
        Check("in the Hall of the Gods at 20:00: the quest's zone-in cutscene 6 (the same hour window)", $"{HallOfTheGods}/20/6",
              $"{c.Zone}/{Lua("report(VanadielHour())")}/{Cur()}");
        EventEnd(6);
        Check("event 6 finished: A Sea Dog's Summons complete, Death and Rebirth accepted", "2/1",
              $"{Quest("A_SEA_DOGS_SUMMONS")}/{Quest("DEATH_AND_REBIRTH")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        ZoneQuest("A_DELECTABLE_DEMON", "CIREIN_CROIN_HARPOONER", ZoneOf("VALKURM_DUNES"), 17199772, 56, 55, misareauxA, 17662476, 57);
        Console.WriteLine("Abyssea Scars of Abyssea, Death and Rebirth: the nine zone quests, Joachim, the Hall of the Gods");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        joachimEvents.Clear();
        for (int i = 0; i < 2 && c.IsInEvent; i++)
        {
            joachimEvents.Add(Cur());
            EventEnd(c.CurrentEvent.EventId);
            Talk(Joachim);
        }
        if (c.IsInEvent) EventEnd(c.CurrentEvent.EventId);
        Check("Joachim twice: Scars of Abyssea's event 338 and Death and Rebirth's 343; Scars complete, Death and Rebirth Prog 1", "338,343/2/1",
              $"{string.Join(",", joachimEvents.OrderBy(e => e))}/{Quest("SCARS_OF_ABYSSEA")}/{QVar("DEATH_AND_REBIRTH", "Prog")}");
        vanaTicks += 24 * VanaTime.HOUR;
        WarpIn(0.0f, -12.0f, 48.0f, HallOfTheGods);
        Check("in the Hall of the Gods at 20:00: Death and Rebirth's zone-in cutscene 7", $"{HallOfTheGods}/7", $"{c.Zone}/{Cur()}");
        EventEnd(7);
        Check("cutscene 7 finished: Death and Rebirth complete, Emissaries of God accepted", "2/1", $"{Quest("DEATH_AND_REBIRTH")}/{Quest("EMISSARIES_OF_GOD")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea Emissaries of God: the six Caturae");
        // a Caturae NM (a GM's !spawnmob, the quest's header) killed by the char in its Abyssea zone: its onMobDeath's title
        void Caturae(string name, ushort abysseaId, float x, float y, float z, uint mobId, string title)
        {
            Warp(x, y, z, abysseaId);
            var nm = zone.ZoneInfoOf(abysseaId).MobById[mobId];
            Lua($"SpawnMob({mobId})");
            Check($"{name} spawned in zone {abysseaId}", "True", nm.IsSpawned && nm.IsAlive);
            int t = TitleId(title);
            KillByChar(mobId);
            Check($"  killed by the char: its onMobDeath gave the {title} title", "True/True", $"{nm.IsDead}/{DbTitle(t)}");
            Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        }
        Lua("player:setUnkillable(true)");
        Caturae("Iratham", AbysseaTahrongi, -24.0f, 44.0f, -678.0f, 16961945, "IRATHAM_CAPTURER");
        Caturae("Kutharei", misareauxA, 670.0f, -15.0f, 318.0f, 17662497, "KUTHAREI_UNHORSER");
        Caturae("Sippoy", vunkerl, -351.0f, -46.0f, 699.0f, 17666511, "SIPPOY_CAPTURER");
        Caturae("Yaanei", attohwa, -140.0f, 20.0f, -181.0f, 17658292, "YAANEI_CRASHER");
        Caturae("Rani", altepa, 432.0f, 0.0f, 321.0f, 17670551, "RANI_DECROWNER");
        Caturae("Raja", grauberg, -555.0f, 31.0f, -760.0f, 17818051, "RAJA_REGICIDE");
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");

        Console.WriteLine("Abyssea Emissaries of God: Joachim, the Hall of the Gods");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim with the six titles: event 344", $"{Joachim}:344/344", $"{EvId()}/{Cur()}");
        int discernment = Ki("ABYSSITE_OF_DISCERNMENT");
        EventEnd(344);
        Check("event 344 finished: the Abyssite of Discernment, Prog 1", "10/1", $"{DbKeyItem(discernment)}/{QVar("EMISSARIES_OF_GOD", "Prog")}");
        vanaTicks += 24 * VanaTime.HOUR;
        WarpIn(0.0f, -12.0f, 48.0f, HallOfTheGods);
        Check("in the Hall of the Gods at 20:00: the quest's zone-in cutscene 8", $"{HallOfTheGods}/8", $"{c.Zone}/{Cur()}");
        EventEnd(8);
        Check("cutscene 8 finished: Emissaries of God complete, Beneath a Blood Red Sky accepted", "2/1",
              $"{Quest("EMISSARIES_OF_GOD")}/{Quest("BENEATH_A_BLOOD_RED_SKY")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea Beneath a Blood Red Sky: Qufim Island's Transcendental Radiance");
        ushort abysseaEp = ZoneOf("ABYSSEA_EMPYREAL_PARADOX");
        int crimson = Ki("CRIMSON_TRAVERSER_STONE");
        string Cruor() => Lua("report(player:getCurrency('cruor'))");
        string HeldStones() => Lua("report(xi.abyssea.getHeldTraverserStones(player))");
        Warp(-256.0f, -20.0f, 225.0f, Qufim);
        uint radiance = NpcId("Transcendental_Radiance");
        Near(radiance);
        string cruor0 = Cruor(), stones0 = HeldStones();
        Talk(radiance);
        Check($"the Transcendental Radiance with {cruor0} cruor (10000 wanted): nothing", "none", Cur());
        // the cruor a GM's !addcurrency gives (kills in Abyssea give none: charutils' cruor drop is a C++ TODO)
        Lua("player:addCurrency('cruor', 10000)");
        string cruor1 = Cruor();
        Talk(radiance);
        Check("with 10000 more: event 47 (0, 0, the cruor, 10000)", $"034:{radiance}:47:0,0,{cruor1},10000/47", $"{Ev()}/{Cur()}");
        EventEnd(47, 0, 1);
        Check("  the update: updateEvent(7, 1, the cruor, 10000, 0, 848559, 4095, 131186)", $"7,1,{cruor1},10000,0,848559,4095,131186", Upd(8));
        EventEnd(47, 1);
        Check("event 47 finished (option 1): 10000 cruor spent, the Crimson Traverser Stone, a traverser stone spent",
              $"{int.Parse(cruor1) - 10000}/10/{int.Parse(stones0) - 1}", $"{Cruor()}/{DbKeyItem(crimson)}/{HeldStones()}");
        Rezone(false);
        Check("  setPos into Abyssea - Empyreal Paradox: the quest's zone-in cutscene 202", $"{abysseaEp}/202", $"{c.Zone}/{Cur()}");
        EventEnd(202);
        Check("cutscene 202 finished: Beneath a Blood Red Sky complete, The Wyrm God accepted", "2/1", $"{Quest("BENEATH_A_BLOOD_RED_SKY")}/{Quest("THE_WYRM_GOD")}");
        Tick(4500);
        Check("  the zone's afterZoneIn: Visitant status with its icon", "True/" + Lua("report(xi.effect.VISITANT)"),
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.VISITANT)))") == "true"}/{Lua("report(player:getStatusEffect(xi.effect.VISITANT):getIcon())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea The Wyrm God: the Transcendental Radiance (TR_Entrance), Prishe");
        uint abyTr = NpcId("TR_Entrance"), prishe = NpcId("Prishe");
        Near(abyTr);
        Talk(abyTr);
        Check("TR_Entrance: event 208", $"{abyTr}:208/208", $"{EvId()}/{Cur()}");
        EventEnd(208);
        Check("event 208 finished: Prog 1", "1", QVar("THE_WYRM_GOD", "Prog"));
        Talk(abyTr);
        Check("TR_Entrance again: no The Wyrm God battlefield (no battlefield script registers it)", "none", Cur());
        // the battlefield's win (its 32001 section: Prog 2) by a GM
        Lua("xi.quest.setVar(player, xi.questLog.ABYSSEA, xi.quest.id.abyssea.THE_WYRM_GOD, 'Prog', 2)");
        Near(prishe);
        Talk(prishe);
        Check("Prishe: event 203", $"{prishe}:203/203", $"{EvId()}/{Cur()}");
        EventEnd(203);
        Check("event 203 finished: The Wyrm God complete, Meanwhile, Back on Abyssea accepted", "2/1",
              $"{Quest("THE_WYRM_GOD")}/{Quest("MEANWHILE_BACK_ON_ABYSSEA")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea Meanwhile, Back on Abyssea: Joachim, the Hall of the Gods, Prishe");
        var exitMawEp = zone.ZoneInfoOf(abysseaEp).Npcs.First(n => n.Name == "Cavernous_Maw");
        Near(exitMawEp.Id);
        Talk(exitMawEp.Id);
        Check("the exit maw: event 200", $"{exitMawEp.Id}:200/200", $"{EvId()}/{Cur()}");
        EventEnd(200, 1);
        Rezone();
        Check("event 200 finished (option 1): Qufim Island", $"{Qufim}", c.Zone);
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        Near(Joachim);
        Talk(Joachim);
        Check("Joachim: event 345", $"{Joachim}:345/345", $"{EvId()}/{Cur()}");
        EventEnd(345);
        Check("event 345 finished: Prog 1", "1", QVar("MEANWHILE_BACK_ON_ABYSSEA", "Prog"));
        vanaTicks += 24 * VanaTime.HOUR;
        WarpIn(0.0f, -12.0f, 48.0f, HallOfTheGods);
        Check("in the Hall of the Gods at 20:00: the quest's zone-in cutscene 9", $"{HallOfTheGods}/9", $"{c.Zone}/{Cur()}");
        EventEnd(9);
        Check("cutscene 9 finished: Prog 2", "2", QVar("MEANWHILE_BACK_ON_ABYSSEA", "Prog"));
        Warp(-256.0f, -20.0f, 225.0f, Qufim);
        Near(radiance);
        Talk(radiance);
        Check("the Transcendental Radiance with the Crimson stone: event 46 (the quest done)", $"034:{radiance}:46:0,0,{Cruor()},10000/46", $"{Ev()}/{Cur()}");
        string cruor2 = Cruor();
        EventEnd(46, 1);
        Rezone();
        Check("event 46 finished (option 1): Abyssea - Empyreal Paradox, nothing spent", $"{abysseaEp}/{cruor2}", $"{c.Zone}/{Cruor()}");
        Near(prishe);
        Talk(prishe);
        Check("Prishe: event 204", $"{prishe}:204/204", $"{EvId()}/{Cur()}");
        int cosmos = Ki("ABYSSITE_OF_THE_COSMOS");
        EventEnd(204);
        Check("event 204 finished: Meanwhile, Back on Abyssea complete (the Abyssite of the Cosmos), A Moonlight Requite accepted", "2/10/1",
              $"{Quest("MEANWHILE_BACK_ON_ABYSSEA")}/{DbKeyItem(cosmos)}/{Quest("A_MOONLIGHT_REQUITE")}");
        Talk(prishe);
        Check("Prishe after: event 207 (the quest's replaceDefault)", $"{prishe}:207", EvId());
        EventEnd(207);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Abyssea's systems: traverser stones, the warp NPC, cruor, the Prospector, atma, lights, the Pyxis ----
        Console.WriteLine("Abyssea: Joachim's traverser stones (the Traverser Stone menu), a day and a half on");
        string Stones() => Lua("report(player:getAvailableTraverserStones()) report(player:getClaimedTraverserStones()) report(xi.abyssea.getHeldTraverserStones(player))");
        Warp(-50.0f, 0.0f, -9.0f, PortJeuno);
        string stonesBefore = Stones();
        // the earth clock 41 hours on: two more stones (one each 20 hours from the epoch)
        long epoch = long.Parse(Lua("report(player:getTraverserEpoch())"));
        earthShift = epoch + 41 * 3600 + 60 - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string[] sb = Stones().Split(',');
        Check("41 hours after the epoch: two stones accrued (20 hours each) less the one claimed", $"{2 - int.Parse(stonesBefore.Split(',')[1])}", sb[0]);
        Near(Joachim);
        Talk(Joachim);
        int heldBefore = int.Parse(sb[2]), stoneCap = int.Parse(Lua("report(xi.abyssea.getTraverserCap(player))"));
        Check("Joachim (no quest left for him): the traverser menu, event 328 (0, available, held, eligible 0, 1, 1, 1, 3)",
              $"034:{Joachim}:328:0,{sb[0]},{heldBefore},0,1,1,1,3/328", $"{Ev()}/{Cur()}");
        EventEnd(328, 3, 1);
        Check("  the update (option 3): updateEvent(0, 0, 0, 0, the minutes to the next stone)", "True",
              Upd(5).StartsWith("0,0,0,0,") && int.Parse(Upd(5).Split(',')[4]) is > 0 and <= 1200);
        EventEnd(328, 6);
        int claimed = Math.Min(stoneCap - heldBefore, int.Parse(sb[0]));
        Check($"event 328 finished (option 6): {claimed} stones claimed (the cap {stoneCap}), the key items, OBTAINED_NUM_KEYITEMS",
              $"{int.Parse(sb[0]) - claimed},{int.Parse(sb[1]) + claimed},{heldBefore + claimed}", Stones());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea: Horst's maw warp for 200 cruor");
        uint horst = NpcId("Horst");
        Lua("player:addCurrency('cruor', 1000)");
        Near(horst);
        string cruorBeforeWarp = Cruor();
        Talk(horst);
        Check("Horst: event 339 (The Truth Beckons done: 2, the cruor, the unlocked maws 7/7/7)", $"034:{horst}:339:2,{cruorBeforeWarp},7,7,7/339", $"{Ev()}/{Cur()}");
        // bit 8 set for a teleport, the selection in bits 2-5: 2 = Tahrongi Canyon
        EventEnd(339, 1u << 8 | 3u << 2);
        Rezone();
        Check("event 339 finished (Tahrongi's maw): 200 cruor spent, by Tahrongi Canyon's maw", $"{int.Parse(cruorBeforeWarp) - 200}/{Tahrongi}/-28/-680",
              $"{Cruor()}/{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea: into Abyssea - Konschtat; the Cruor Prospector");
        Warp(93.0f, -69.0f, -580.0f, Konschtat);
        Near(MawKonschtat);
        Talk(MawKonschtat);
        EventEnd(107, 1);
        Rezone();
        Check("the maw (option 1): Abyssea - Konschtat", $"{AbysseaKonschtat}", c.Zone);
        Tick(4500);
        Lua("player:addCurrency('cruor', 5000)");
        // the NMs' loot fills the 30 slots: a GM's bigger bag (changeContainerSize, as the Mog House's moogle gives)
        Check("the inventory full of the NMs' loot", "0", Lua("report(player:getFreeSlotsCount())"));
        Lua("player:changeContainerSize(xi.inventoryLocation.INVENTORY, 20)");
        uint prospector = NpcId("Cruor_Prospector");
        Near(prospector);
        Talk(prospector);
        string cr = Cruor();
        Check("the Cruor Prospector: event 2002 (the cruor)", $"034:{prospector}:2002:{cr}/2002", $"{Ev()}/{Cur()}");
        ushort forbiddenKey = ushort.Parse(Lua("report(xi.item.FORBIDDEN_KEY)")), lucidPotion = ushort.Parse(Lua("report(xi.item.LUCID_POTION_I)"));
        // the item menu (1), selection 16 (the Forbidden Key), two of them
        EventEnd(2002, 1u | 16u << 16 | 2u << 24);
        Check("event 2002 finished (two Forbidden Keys): 1000 cruor spent", $"2/{int.Parse(cr) - 1000}", $"{Qty(forbiddenKey)}/{Cruor()}");
        Talk(prospector);
        EventEnd(2002, 2u | 1u << 16);
        Check("  the temporary items (2), selection 1: a Lucid Potion I in the temporary items, 80 cruor", $"True/{int.Parse(cr) - 1080}",
              $"{c.Items.Any(i => i.Location == Container.TempItems && i.ItemId == lucidPotion)}/{Cruor()}");
        int mapKonschtat = Ki("MAP_OF_ABYSSEA_KONSCHTAT");
        Talk(prospector);
        EventEnd(2002, 3u | 1u << 16);
        Check("  the key items (3), selection 1: the Map of Abyssea - Konschtat, 3500 cruor", $"10/{int.Parse(cr) - 4580}", $"{DbKeyItem(mapKonschtat)}/{Cruor()}");
        Talk(prospector);
        EventEnd(2002, 4u | 6u << 16);
        Check("  the enhancements (4), selection 6: Abyssea HP (+20, the Max HP Boost icon), 50 cruor", $"True/{int.Parse(cr) - 4630}",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.ABYSSEA_HP)))") == "true"}/{Cruor()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea: the Veridical Conflux #01, the Conflux Surveyor");
        uint conflux1 = NpcId("Veridical_Conflux_#01");
        Near(conflux1);
        Talk(conflux1);
        cr = Cruor();
        Check("the Veridical Conflux #01 not yet activated: event 2132 (the costs packed, 0, bit 0, 2, the cruor)",
              $"034:{conflux1}:2132:{50 | 100 << 16},{150 | 200 << 16},{250 | 300 << 16},{350 | 400 << 16},0,0,2,{cr}/2132", $"{Ev()}/{Cur()}");
        EventEnd(2132, 0, 1);
        Check("  the update: updateEvent(1, 0, ...)", "1,0", Upd(2));
        EventEnd(2132, 1);
        Check("event 2132 finished (option 1): activated (char_unlocks.abyssea_conflux), 50 cruor", $"1/{int.Parse(cr) - 50}/True",
              $"{Lua("report(player:getTeleport(xi.teleport.type.ABYSSEA_CONFLUX, 0))")}/{Cruor()}/" +
              $"{DbRow(db, "HEX(abyssea_conflux)", "FROM char_unlocks WHERE charid = ?").StartsWith("01")}");
        Talk(conflux1);
        Check("  again: event 2132 activated (the mask less its own bit, 9, 1, the cruor)", $"034:{conflux1}:2132:{50 | 100 << 16},{150 | 200 << 16},{250 | 300 << 16},{350 | 400 << 16},0,9,1,{Cruor()}",
              Ev());
        EventEnd(2132, 0);
        uint surveyor = NpcId("Conflux_Surveyor");
        Near(surveyor);
        Talk(surveyor);
        string held = HeldStones();
        Check("the Conflux Surveyor with the hidden Visitant time: event 2001 (0, 0, the stored time 0, the stones held)", $"034:{surveyor}:2001:0,0,0,{held}/2001",
              $"{Ev()}/{Cur()}");
        // option 2 with one stone in the upper bits: 30 minutes of Visitant status
        EventEnd(2001, 2u | 1u << 16);
        string vis = Lua("local e = player:getStatusEffect(xi.effect.VISITANT) report(e:getIcon()) report(math.floor(e:getTimeRemaining() / 1000))");
        Check("event 2001 finished (a stone): Visitant status with its icon, about 30 minutes, the stone spent", $"{Lua("report(xi.effect.VISITANT)")}/True/{int.Parse(held) - 1}",
              $"{vis.Split(',')[0]}/{int.Parse(vis.Split(',')[1]) is >= 1795 and <= 1805}/{HeldStones()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea: Kukulkan from his ???, his atma, the Atma Infusionist");
        int noxiousFang = Ki("ATMA_OF_THE_NOXIOUS_FANG");
        Check("the Kukulkan killed for To Paste a Peiste: his atma to the claimer (abyssea_weakness's DEATH listener, [ClaimedBy] from ATTACKED)", "10", DbKeyItem(noxiousFang));
        Lua("player:delKeyItem(xi.keyItem.ATMA_OF_THE_NOXIOUS_FANG)");
        // his pop key items, the other NMs' drops: a GM's !addkeyitem
        Lua("player:addKeyItem(xi.keyItem.TATTERED_HIPPOGRYPH_WING) player:addKeyItem(xi.keyItem.CRACKED_WIVRE_HORN) player:addKeyItem(xi.keyItem.MUCID_AHRIMAN_EYEBALL)");
        uint qmKukulkan = NpcId("qm_kukulkan");
        Near(qmKukulkan);
        Talk(qmKukulkan);
        int wing = Ki("TATTERED_HIPPOGRYPH_WING");
        Check("the ??? with the three key items: event 1020 (them)", $"034:{qmKukulkan}:1020:{wing},{Ki("CRACKED_WIVRE_HORN")},{Ki("MUCID_AHRIMAN_EYEBALL")}/1020", $"{Ev()}/{Cur()}");
        EventEnd(1020, 1);
        var kukulkan = zone.ZoneInfoOf(AbysseaKonschtat).MobById[Kukulkan];
        Check("event 1020 finished (option 1): the key items taken (LOST_KEYITEM), Kukulkan up by the char, claimed, [ClaimedBy]", $"00/True/True/{c.Id}",
              $"{DbKeyItem(wing)}/{Special().Contains($"{Text(AbysseaKonschtat, "LOST_KEYITEM")}:{wing}")}/{kukulkan.IsSpawned && kukulkan.OwnerId == c.Id}/" +
              $"{Lua($"report(GetMobByID({Kukulkan}):getLocalVar('[ClaimedBy]'))")}");
        Check("  abyssea_weakness's SPAWN listener: his red and blue weaknesses picked", "True",
              Lua($"report(GetMobByID({Kukulkan}):getLocalVar('[RedWeakness]'))") != "0" && Lua($"report(GetMobByID({Kukulkan}):getLocalVar('[BlueWeakness]'))") != "0");
        Lua("player:setUnkillable(true)");
        KillByChar(Kukulkan);
        Check("Kukulkan killed: his atma (PLAYER_KEYITEM_OBTAINED)", $"10/True", $"{DbKeyItem(noxiousFang)}/{Special().Contains($"{Text(AbysseaKonschtat, "PLAYER_KEYITEM_OBTAINED")}:")}");
        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        uint infusionist = NpcId("Atma_Infusionist");
        Near(infusionist);
        Talk(infusionist);
        cr = Cruor();
        Check("the Atma Infusionist: event 2003 (the cruor, the lunar abyssites' free slots)", $"2003/{cr}", $"{Cur()}/{Ev().Split(':')[3].Split(',')[0]}");
        EventEnd(2003, 0, 1);
        Check("  the update: updateEvent(no atma to reinfuse, the history)", "0", Upd(1));
        int atmaIndex = int.Parse(Lua("local keys = {} for k in pairs(xi.atma.atmaMods) do table.insert(keys, k) end table.sort(keys) " +
                                      "for i, k in ipairs(keys) do if k == xi.keyItem.ATMA_OF_THE_NOXIOUS_FANG then report(i) end end"));
        short subtle0 = c.GetMod(Mod.SUBTLE_BLOW);
        EventEnd(2003, 1u | (uint)atmaIndex << 16);
        Check("event 2003 finished (infuse the Noxious Fang): ATMA (power the key item, subType slot 1), its mods (Subtle Blow +40), 100 cruor, ATMA_INFUSED",
              $"{noxiousFang}/{subtle0 + 40}/{int.Parse(cr) - 100}/True",
              $"{Lua("report(player:getStatusEffect(xi.effect.ATMA, 1):getPower())")}/{c.GetMod(Mod.SUBTLE_BLOW)}/{Cruor()}/{Special().Contains(Text(AbysseaKonschtat, "ATMA_INFUSED") + ":")}");
        Talk(infusionist);
        EventEnd(2003, 2u | 1u << 16);
        Check("  purge slot 1 (option 2): the atma gone, its mods with it, ATMA_PURGED", $"False/{subtle0}/True",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.ATMA)))") == "true"}/{c.GetMod(Mod.SUBTLE_BLOW)}/{Special().Contains(Text(AbysseaKonschtat, "ATMA_PURGED") + ":")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Abyssea: the zone's mobs killed, the lights and the Sturdy Pyxis (drop_lights, spawn_pyxis)");
        // the regular mobs of the zone's light table, killed by the char (a physical death: pearlescent light, 40 % for
        // most), until a Sturdy Pyxis came out of one and a light was gained
        var lightMobs = new HashSet<string> { "Razorback", "Gunge_Slug", "Hoary_Ragwort", "Highland_Rafflesia", "Licorice", "Mesa_Wivre", "Shadow_Lizard",
                                              "Shadow_Funguar", "Sods_Limule", "Trotting_Sapling", "Deep_Eye", "Morboling", "Ypotryll", "Viridis_Wyvern", "Qaitu" };
        var konschtatInfo = zone.ZoneInfoOf(AbysseaKonschtat);
        var pyxides = konschtatInfo.Npcs.Where(n => n.Name == "Sturdy_Pyxis").ToList();
        // the zone's npcs.yaml has a few of them out from the start (status normal, no chest data)
        var outAtLoad = pyxides.Where(n => n.Status == NpcEntity.StatusNormal).Select(n => n.Id).ToHashSet();
        Check("the zone's Sturdy Pyxis entities, most hidden", "True", pyxides.Count > outAtLoad.Count);
        string Pearl() => Lua("report(xi.abyssea.getLightsTable(player)[xi.abyssea.lightType.PEARL])");
        NpcEntity chest = null;
        int kills = 0, pearl0 = int.Parse(Pearl()), pearl = pearl0;
        bool emits = false;
        string emitsMsg = Text(AbysseaKonschtat, "BODY_EMITS_OFFSET");
        // the kills stop at the first chest: it removes itself 180 s after it spawns (SetPyxisData's npc:timer), so the
        // light, if not gained by then, is killed for after the chest is opened
        var lightKills = konschtatInfo.Mobs.Where(m => lightMobs.Contains(m.Name) && !m.IsNotorious && m.IsSpawned && m.IsAlive).Take(60).ToList();
        int nextKill = 0;
        void KillForLight(Func<bool> done)
        {
            Lua("player:setUnkillable(true)");
            for (; nextKill < lightKills.Count && !done(); nextKill++)
            {
                var m = lightKills[nextKill];
                if (!m.IsSpawned || !m.IsAlive) continue;
                s.PacketList.Clear();
                KillByChar(m.Id);
                kills++;
                int before = pearl;
                pearl = int.Parse(Pearl());
                if (pearl > before) emits |= Special().Contains(emitsMsg + ":0");
                chest ??= pyxides.FirstOrDefault(n => n.Status == NpcEntity.StatusNormal && !outAtLoad.Contains(n.Id));
            }
            Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        }
        KillForLight(() => chest != null);
        Check("  a Sturdy Pyxis came out of a kill (SetPyxisData: status normal, the char's, animation sub 12)", $"True/{c.Id}/12",
              $"{chest != null}/{(chest == null ? "-" : Lua($"report(GetNPCByID({chest.Id}):getLocalVar('PLAYERID'))"))}/{(chest == null ? "-" : Lua($"report(GetNPCByID({chest.Id}):getAnimationSub())"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        if (chest != null)
        {
            Console.WriteLine("Abyssea: the Sturdy Pyxis opened with a Forbidden Key");
            Near(chest.Id);
            int tier = int.Parse(Lua($"report(GetNPCByID({chest.Id}):getLocalVar('TIER'))"));
            int dropType = int.Parse(Lua($"report(GetNPCByID({chest.Id}):getLocalVar('DROPTYPE'))"));
            Talk(chest.Id);
            Check($"the locked chest (tier {tier}, drop type {dropType}): its locked event {2003 + tier}", $"{2003 + tier}", Cur());
            EventEnd(c.IsInEvent ? c.CurrentEvent.EventId : 0);
            byte pyxisKeySlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == forbiddenKey).Slot;
            string keyOpenMsg = Text(AbysseaKonschtat, "TRADE_KEY_OPEN");
            // on a failure: the keys held before the trade (the two bought: no mob or chest of the path drops one) and
            // after it, and what the engine logged meanwhile (a tradeComplete that kept the key logs why; onPyxisTrade
            // opens the chest and says TRADE_KEY_OPEN whatever tradeComplete returns, as on the C++)
            int keysBefore = int.Parse(Qty(forbiddenKey));
            s.PacketList.Clear();
            int logFrom = log.Count;
            Trade(chest.Id, (pyxisKeySlot, 1));
            string KeyRows() => string.Join(",", c.Items.Where(i => i.ItemId == forbiddenKey)
                .Select(i => $"{i.Location}:{i.Slot}:{i.Quantity}"));
            Check("a Forbidden Key traded: the key taken, TRADE_KEY_OPEN (messageName, 2490), the chest open (animation sub 13)", "1/True/13",
                  $"{Qty(forbiddenKey)}/{Of(0x027).Any(p => (BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF) == ushort.Parse(keyOpenMsg) && BitConverter.ToUInt32(p.Buffer, 16) == 2490)}/{Lua($"report(GetNPCByID({chest.Id}):getAnimationSub())")}",
                  $"before={keysBefore} slot={pyxisKeySlot} keys (location:slot:qty)=[{KeyRows()}] " +
                  $"log: {string.Join(" | ", log.Skip(logFrom))}");
            // the item drops wait in the chest: its unlocked event, then option 999 leaves it (TIER x 10 cruor)
            if (dropType is 1 or 2 or 3 or 4 or 5)
            {
                string cruorBefore = Cruor();
                Talk(chest.Id);
                Check($"the open chest holding items (drop type {dropType}): its unlocked event {2003 + tier + 64}", $"{2003 + tier + 64}", Cur());
                EventEnd(2003 + tier + 64, 999);
                Check("  option 999 (leave it): removeChest's TIER x 10 cruor, CRUOR_OBTAINED", $"{int.Parse(cruorBefore) + tier * 10}/True",
                      $"{Cruor()}/{Special().Contains(Text(AbysseaKonschtat, "CRUOR_OBTAINED") + ":")}");
            }
            for (int i = 0; i < 6 && chest.Status == NpcEntity.StatusNormal; i++) Tick(1000);
            Check("  the chest gone a few seconds on (removeChest's npc:timer: DISAPPEAR, SPAWNSTATUS 0)", "False/0",
                  $"{chest.Status == NpcEntity.StatusNormal}/{Lua($"report(GetNPCByID({chest.Id}):getLocalVar('SPAWNSTATUS'))")}");
            Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        }
        KillForLight(() => pearl > pearl0);
        Check($"{kills} kills: pearlescent light gained (the death listener's DropLights: addPlayerLights, BODY_EMITS_OFFSET + 0)", "True/True",
              $"{pearl > pearl0}/{emits}");

        Console.WriteLine("Abyssea: a Dominion Op (Abyssea - Grauberg's DSgt_Cornelia, the Dominion Tactician)");
        Warp(-15.5f, 0.6f, -482.0f, grauberg);
        uint cornelia = NpcId("DSgt_Cornelia"), tactician = NpcId("Dominion_Tactician");
        string Notes() => Lua("report(player:getCurrency('dominion_note'))");
        string notes0 = Notes();
        string OpVar() => Lua("report(player:getCharVar('activeDominionOp'))");
        Near(cornelia);
        Talk(cornelia);
        Check("DSgt_Cornelia with no op: event 501 (the zone's influence, her op mask 6606, the notes)", $"034:{cornelia}:501:0,0,0,0,6606{(notes0 == "0" ? "" : $",0,{notes0}")}/501",
              $"{Ev()}/{Cur()}");
        EventEnd(501, 2 | 1 << 4, 1);
        Check("  the update (2 | op 1 << 4): updateEvent(5 targets, ..., op 588)", "5,0,0,0,0,0,0,588", Upd(8));
        EventEnd(501, 2 | 1 << 4);
        Check("event 501 finished (2 | op 1 << 4): Dominion Op #01 accepted, activeDominionOp 588, DOMINION_SIGNED_ON", $"1/588/True",
              $"{Quest("DOMINION_OP_01_GRAUBERG")}/{OpVar()}/{Special().Contains(Text(grauberg, "DOMINION_SIGNED_ON") + ":")}");
        Talk(cornelia);
        Check("DSgt_Cornelia with the op on: event 501 (op 588, 5 targets, 0 done, status 1)", $"034:{cornelia}:501:588,5,0,0,0,0,0,1", Ev());
        EventEnd(501);
        // no mob script calls dominionOnMobDeath (its targets are unscripted): a GM's five calls for the kills
        for (int i = 0; i < 5; i++) Lua("xi.abyssea.dominionOnMobDeath(nil, player, 588)");
        Check("five designated targets (dominionOnMobDeath): the op's progress var 5", "5", Lua("report(player:getCharVar('Quest[8][' .. xi.quest.id.abyssea.DOMINION_OP_01_GRAUBERG .. ']Prog'))"));
        string cruorOp = Cruor();
        Talk(cornelia);
        Check("DSgt_Cornelia with the targets done: event 501 (op 588, 5, 5, status 2)", $"034:{cornelia}:501:588,5,5,0,0,0,0,2", Ev());
        EventEnd(501, 8);
        Check("event 501 finished (option 8): the op complete, 200 cruor, 100 Dominion notes (CRUOR_TOTAL, OBTAINS_DOMINION_NOTES), activeDominionOp 0",
              $"2/{int.Parse(cruorOp) + 200}/{int.Parse(notes0) + 100}/True/True/0",
              $"{Quest("DOMINION_OP_01_GRAUBERG")}/{Cruor()}/{Notes()}/{Special().Contains(Text(grauberg, "CRUOR_TOTAL") + ":200")}/" +
              $"{Special().Contains(Text(grauberg, "OBTAINS_DOMINION_NOTES") + ":100")}/{OpVar()}");
        Talk(cornelia);
        EventEnd(501, 2 | 2 << 4);
        Check("op #02 signed on (op 589)", "1/589", $"{Quest("DOMINION_OP_02_GRAUBERG")}/{OpVar()}");
        Talk(cornelia);
        EventEnd(501, 3);
        Check("  then cancelled (option 3): the quest dropped, CANCELED_OBJECTIVE, activeDominionOp 0", "0/True/0",
              $"{Quest("DOMINION_OP_02_GRAUBERG")}/{Special().Contains(Text(grauberg, "CANCELED_OBJECTIVE") + ":")}/{OpVar()}");
        // the Dominion Tactician: an Increscent Shade (300 notes) with a GM's 200 more notes
        Lua("player:addCurrency('dominion_note', 200)");
        string notes1 = Notes();
        ushort shade = ushort.Parse(Lua("report(xi.item.INCRESCENT_SHADE)"));
        int shades = int.Parse(Qty(shade));
        Near(tactician);
        Talk(tactician);
        Check("the Dominion Tactician: event 120 with the notes", $"034:{tactician}:120:{notes1}", Ev());
        EventEnd(120, 1 | 10 << 8);
        Check("event 120 finished (item 10, the Increscent Shade): 300 notes spent", $"{shades + 1}/{int.Parse(notes1) - 300}", $"{Qty(shade)}/{Notes()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        if (Environment.GetEnvironmentVariable("MISSIONS_DUMP_LOG") == "1") foreach (var l in log.Distinct()) Console.WriteLine("  log: " + l);
    }
}
