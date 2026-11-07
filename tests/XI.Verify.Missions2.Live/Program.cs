using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Missions2.Live -- live gate for the nation storylines and Rise of the Zilart driven end to end as a client
// drives them, through the mission interaction container (xi.mission, scripts/missions/sandoria, bastok, windurst, rotz):
// the 0x00A login and its zone-in cutscenes, 0x05B event ends and updates, 0x05C EVENTENDXZY (a battlefield's arena
// asked for), 0x015 moves, 0x01A Talk and Attack, 0x036 trades and 0x05E zone lines, and every step's effect: the
// mission log (chars.missions), the nation rank and rank points, the mission vars (char_vars), key items, items, gil,
// titles and the zone changes. The real scripts on the real MariaDB (xidb), chars 996001-996009. The walks between
// places the missions do not script are GM !pos warps, as the missions' headers give.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;
    private static readonly List<string> _log = new List<string>();

    private static uint CharId = 996001;
    private static readonly uint[] CharIds = { 996001, 996002, 996003, 996004 };
    private const int LogSandoria = 0, LogBastok = 1, LogWindurst = 2, LogZilart = 3;
    private const ushort SouthernSandoria = 230, NorthernSandoria = 231, PortSandoria = 232, Chateau = 233, RanperresTomb = 190,
                         GhelsbaOutpost = 140, LaTheine = 102, OrdellesCaves = 193, Davoi = 149,
                         Metalworks = 237, WindurstWoods = 241, HeavensTower = 242, BalgasDais = 146, Rulude = 243, UpperJeuno = 244,
                         LowerJeuno = 245, LowerDelkfutt = 184, AltarRoom = 152, MonasticCavern = 150, QulunDome = 148, FeiYin = 204,
                         QubiaArena = 206, ThroneRoom = 165, CastleZvahlBaileys = 161, WesternAltepa = 125, WestRonfaure = 100,
                         HorlaisPeak = 139, QuicksandCaves = 208, TempleOfUggalepih = 159, ValleyOfSorrows = 128, Xarcabard = 112,
                         BatalliaDowns = 105, PortBastok = 236, BastokMines = 234, ZeruhnMines = 172, DangrufWadi = 191, Konschtat = 108,
                         PortWindurst = 240, Giddeus = 145, Beadeaux = 147, Pashhow = 109, GusgenMines = 196,
                         Norg = 252, IfritsCauldron = 205, RoMaeve = 122, WaughroonShrine = 144, KuftalTunnel = 174, Rabao = 247,
                         GustavTunnel = 212, WindurstWaters = 238, WindurstWalls = 239, EastSarutabaruta = 116, TahrongiCanyon = 117,
                         InnerHorutoto = 192, OuterHorutoto = 194, MazeOfShakhrami = 198, CastleOztroja = 151, Toraimarai = 169, Kazham = 250,
                         BoyahdaTree = 153, FullMoonFountain = 170, ChamberOfOracles = 168,
                         SacrificialChamber = 163, StellarFulcrum = 179, HallOfTheGods = 251, RuAunGardens = 130, ShrineOfRuavitau = 178,
                         BehemothsDominion = 127, CapeTeriggan = 113, CloisterOfFrost = 203, Zitah = 121, YuhtungaJungle = 123,
                         LaLoffAmphitheater = 180, CelestialNexus = 181;
    private const ushort OrcishAxe = 16656, OrcishMailScales = 1112, FireCrystal = 4096, MythrilSand = 599, BronzeSword = 16535, Pickaxe = 605,
                         CrystalBass = 4528, DelkfuttKey = 549, SanDorianFlag = 181,
                         QuadavFetichHead = 606, QuadavFetichTorso = 607, QuadavFetichArms = 608, QuadavFetichLegs = 609, LizardEgg = 4362,
                         AspirKnife = 16509, HareMeat = 4358, FadedCrystal = 613, Glocolite = 563,
                         AdamanOre = 646, FragRock = 1160, BastokanFlag = 182,
                         CursedKey = 1143, CurseWand = 17437, GoobbueHumus = 1181, WindurstianFlag = 183, AshuraNecklace = 13134;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
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
            if (Environment.GetEnvironmentVariable("MISSIONS_DUMP_LOG") == "1") foreach (var l in _log.Distinct()) Console.WriteLine("  log: " + l);
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
            BoundValue.Of(CharId), BoundValue.Of($"missionstwo{CharId % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of($"Missiontwo{CharId % 100}"), BoundValue.Of(zoneId), BoundValue.Of(prevZone),
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

        // San d'Oria: a WAR 75 San d'Orian of rank 1 in Southern San d'Oria by Endracion, with the 1-1 and 1-2 items and
        // a stack of fire crystals
        Fixture(db, SouthernSandoria, SouthernSandoria, -108.5f, 1.0f, -34.0f, 0, (OrcishAxe, 1), (OrcishMailScales, 1), (FireCrystal, 12), (MythrilSand, 1));

        var log = _log;
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        // the scripts' Vana'diel clock (VanadielUniqueDay, the wall clock as CVanaTime's) on the host's test clock
        lua.SetGlobal("VanadielUniqueDay", l => { l.Push((double)VanaTime.CountDays(vanaTicks)); return 1; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_missions2");
            return string.Join(",", got);
        }
        // CLuaBaseEntity::addMission warns on a current mission other than 0 past the nation logs, and the Zilart log
        // starts at 0xFFFF (the CCharEntity constructor): The New Frontier's add after the Shadow Lord warns
        const string ZilartFirstAdd = "lua: addMission: overwriting current mission 65535 of log 3 with 0";
        var expectedWarnings = new HashSet<string> { ZilartFirstAdd };
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
        // the NPC of that name, a talkable one first (a cutscene-only twin shares the name)
        uint NpcId(string name) => zone.ZoneInfoOf(c.Zone).Npcs.Where(n => n.Name == name).OrderBy(n => n.Status == NpcEntity.StatusNormal ? 0 : 1).First().Id;
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
            c.Hp = c.MaxHp;
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
                // the packets so far sent (a setPos's 0x05B pending makes the client's position reports stale)
                s.PacketList.Clear();
                Send(0x015, 32, b =>
                {
                    BitConverter.GetBytes(m.X - 2.0f).CopyTo(b, C2sPos.OffX);
                    BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
                    BitConverter.GetBytes(m.Z).CopyTo(b, C2sPos.OffY);
                    b[C2sPos.OffDir] = 0;
                });
                for (int i = 0; m.OwnerId != c.Id && i < 30; i++)
                {
                    // the Attack again while the char is not engaged (refused within the engage lockout after the
                    // last kill, as a client's retry) or is on another target (an auto-target to a mob fighting it);
                    // the NMs hit back: the char kept on its feet (a GM's regen) so its swing claims
                    if (c.Animation != Anim.Attack || c.BattleTarget != m)
                        Send(0x01A, 28, b =>
                        {
                            BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                            BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
                        });
                    c.Hp = c.MaxHp;
                    // kept beside it (a roaming mob walks out of the swing's reach), facing it
                    float px = m.X - 2.0f, pz = m.Z;
                    byte dir = (byte)(short)(MathF.Atan2(m.Z - pz, m.X - px) * -(128.0f / MathF.PI));
                    Send(0x015, 32, b =>
                    {
                        BitConverter.GetBytes(px).CopyTo(b, C2sPos.OffX);
                        BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
                        BitConverter.GetBytes(pz).CopyTo(b, C2sPos.OffY);
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
        // (onBattlefieldWin) and its end, then the zone change the end asked for (rezone) or none, the char left in the
        // arena; the packets of the end kept in winPackets
        List<BasicPacket> winPackets = null;
        void WinBattlefield(uint webId, string where, bool endZoneIn = true, bool rezone = true)
        {
            var bf = EnterBattlefield(webId, where);
            if (bf == null) return;
            var spawned = bf.RequiredEnemyList.Concat(bf.AdditionalEnemyList).Select(m => m.PMob).Where(m => m.IsSpawned).ToList();
            foreach (var m in spawned) m.Hp = 0;
            for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
            Check($"  the mobs dead, won: onBattlefieldWin's cutscene 32001", "2/32001", $"{bf.Status}/{Cur()}");
            EventEnd(32001);
            winPackets = new List<BasicPacket>(s.PacketList);
            if (rezone) Rezone(endZoneIn);
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

        // a talk to the NPC of that name in the char's zone, the char by it first
        void TalkTo(string name)
        {
            var id = NpcId(name);
            Near(id);
            Talk(id);
        }
        // the event the scripts started checked, then finished with the option
        void Cs(string what, int csid, uint option = 0)
        {
            Check(what, csid.ToString(), Cur());
            EventEnd(csid, option);
        }
        // the nation rank and rank points
        string Rank() => Lua("report(player:getRank(player:getNation())) report(player:getRankPoints())");
        // a trade to the NPC of that name in the char's zone
        void TradeTo(string name, params (byte Slot, uint Qty)[] items)
        {
            var id = NpcId(name);
            Near(id);
            Trade(id, items);
        }
        byte Slot(ushort itemId) => byte.Parse(DbRow(db, "slot", $"FROM char_inventory WHERE charid = ? AND location = 0 AND itemId = {itemId} LIMIT 1"));
        // the gate guard's mission list (event 1009 / 2009 with the mission mask) and the mission picked from it
        void Accept(string guard, int listEv, uint option, string what)
        {
            TalkTo(guard);
            Check($"{guard}: the mission list, event {listEv}", $"{listEv}", Cur());
            EventEnd(listEv, option);
            Check($"  {what} picked (option {option})", "none", Cur());
        }

        // MISSIONS_FROM=bastok, windurst or zilart (a debugging run) starts at that storyline
        if (Environment.GetEnvironmentVariable("MISSIONS_FROM") == "bastok") goto Bastok;
        if (Environment.GetEnvironmentVariable("MISSIONS_FROM") == "windurst") goto Windurst;
        if (Environment.GetEnvironmentVariable("MISSIONS_FROM") == "zilart") goto Zilart;

        // ---- San d'Oria ----
        Console.WriteLine("San d'Oria: the login in Southern San d'Oria");
        Login();
        Check("in Southern San d'Oria, out of any event", $"{SouthernSandoria}/none", $"{c.Zone}/{Cur()}");
        Check("the San d'Oria log empty, rank 1 with no points", "65535,0/1,0", $"{Mission(LogSandoria)}/{Rank()}");
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 1-1 Smash the Orcish Scouts");
        TalkTo("Endracion");
        Check("Endracion, 1-1 never done: the mission's first offer, event 1000", $"032:{NpcId("Endracion")}:1000", Ev());
        EventEnd(1000, 0);
        Check("  option 0: Smash the Orcish Scouts current, YOU_ACCEPT_THE_MISSION", $"0,0/{Text(SouthernSandoria, "YOU_ACCEPT_THE_MISSION")}:0",
              $"{Mission(LogSandoria)}/{Special()}");
        Check("  chars.missions: San d'Oria current", "0/0", DbMission(LogSandoria));
        TalkTo("Endracion");
        Check("Endracion on the mission: ORIGINAL_MISSION_OFFSET (no event)", "none", Cur());
        TradeTo("Endracion", (Slot(OrcishAxe), 1));
        Cs("the Orcish Axe traded: event 1020", 1020);
        Check("  Smash the Orcish Scouts complete: 150 rank points, the axe taken, the log empty", "65535,0/1,150/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Qty(OrcishAxe)}/{DbMissionComplete(LogSandoria, 0)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria: crystals at rank 1 (Aravoge T.K.)");
        TradeTo("Aravoge_TK", (Slot(FireCrystal), 1));
        Check("a fire crystal at rank 1: refused (CONQUEST - 7), kept", $"12/1,150", $"{Qty(FireCrystal)}/{Rank()}");

        Console.WriteLine("San d'Oria 1-2 Bat Hunt");
        TalkTo("Endracion");
        Check("Endracion: the mission list 1009 with the mask (1-1 repeatable, 1-2 offered) and the repeat mask",
              $"034:{NpcId("Endracion")}:1009:{int.MaxValue - 3},0,0,0,0,1", Ev());
        EventEnd(1009, 101);
        Check("  Bat Hunt current", "1,0", Mission(LogSandoria));
        TalkTo("Endracion");
        Cs("Endracion before the tomb: event 1021", 1021);
        Warp(1.0f, 0.1f, -99.0f, RanperresTomb);
        Check("in King Ranperre's Tomb", RanperresTomb, c.Zone);
        TalkTo("Tombstone_Upper");
        Cs("the upper tombstone: cutscene 4", 4);
        Check("  Status 2", "1,2", Mission(LogSandoria));
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Cs("Endracion after the tomb: event 1022", 1022);
        TradeTo("Endracion", (Slot(OrcishMailScales), 1));
        Cs("the Orcish Mail Scales traded: event 1023", 1023);
        Check("  Bat Hunt complete: 350 rank points, the scales taken", "65535,0/1,350/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Qty(OrcishMailScales)}/{DbMissionComplete(LogSandoria, 1)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 1-3 Save the Children");
        Accept("Endracion", 1009, 102, "Save the Children");
        Check("  Save the Children current, Status 1", "2,1", Mission(LogSandoria));
        TalkTo("Endracion");
        Check("Endracion at Status 1: ORIGINAL_MISSION_OFFSET + 24 (no event)", "none", Cur());
        Warp(148.0f, 0.0f, 137.0f, NorthernSandoria);
        TalkTo("Arnau");
        Cs("Arnau: event 693", 693);
        Check("  Status 2", "2,2", Mission(LogSandoria));
        Warp(-162.0f, -11.0f, 76.0f, GhelsbaOutpost);
        Check("in Ghelsba Outpost", GhelsbaOutpost, c.Zone);
        int hutKey = Ki("ORCISH_HUT_KEY"), titleFodder = TitleId("FODDERCHIEF_FLAYER");
        WinBattlefield(NpcId("Hut_Door"), "the Hut Door (Save the Children)");
        Check("  the Orcish Hut Key, the title, Status 3", $"10/True/2,3", $"{DbKeyItem(hutKey)}/{DbTitle(titleFodder)}/{Mission(LogSandoria)}");
        TalkTo("Hut_Door");
        Cs("the Hut Door with the key: event 55", 55);
        Check("  the key taken, Status 4", "00/2,4", $"{DbKeyItem(hutKey).Substring(0, 1)}0/{Mission(LogSandoria)}");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Cs("Endracion: event 1004", 1004);
        Check("  Save the Children complete: rank 2 (setRank keeps the points), 1000 gil, Option 1", "65535,0/2,350/11000/1/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{Var($"Mission[{LogSandoria}][2]Option")}/{DbMissionComplete(LogSandoria, 2)}");
        Warp(148.0f, 0.0f, 137.0f, NorthernSandoria);
        TalkTo("Arnau");
        Cs("Arnau after the mission: event 694", 694);
        Check("  Option cleared", "-", Var($"Mission[{LogSandoria}][2]Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));


        Console.WriteLine("San d'Oria: crystals at rank 2 (Aravoge T.K.)");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TradeTo("Endracion", (Slot(FireCrystal), 1));
        Check("a crystal to the gate guard on no mission: event 1010 (mission not activated)", "1010", Cur());
        EventEnd(1010);
        TradeTo("Aravoge_TK", (Slot(FireCrystal), 1));
        Check("a fire crystal at rank 2: floor(4000 / (2 * 12 - 12)) = 333 rank points, the crystal taken", "11/2,683", $"{Qty(FireCrystal)}/{Rank()}");

        Console.WriteLine("San d'Oria 2-1 The Rescue Drill");
        TalkTo("Endracion");
        Check("Endracion: the mission list, 2-1 offered (683 of the 671.5 points it needs), 1-1 to 1-3 repeatable",
              $"034:{NpcId("Endracion")}:1009:{int.MaxValue - 15},0,0,0,0,7", Ev());
        EventEnd(1009, 3);
        Check("  The Rescue Drill current", "3,0", Mission(LogSandoria));
        Warp(-482.0f, -7.0f, 222.0f, LaTheine);
        Check("in La Theine Plateau", LaTheine, c.Zone);
        foreach (var (npc, ev, after) in new[] { ("Galaihaurat", 110, 2), ("Equesobillot", 101, 3), ("Deaufrain", 102, 4), ("Vicorpasse", 108, 5),
                                                 ("Laurisse", 106, 6), ("Narvecaint", 107, 7) })
        {
            TalkTo(npc);
            Cs($"{npc}: event {ev}", ev);
            Check($"  Status {after}", $"3,{after}", Mission(LogSandoria));
        }
        Warp(-70.0f, 1.0f, 605.0f, OrdellesCaves);
        TalkTo("Ruillont");
        Cs("Ruillont in Ordelle's Caves: event 1", 1);
        string drillOption = Var($"Mission[{LogSandoria}][3]Option");
        Check("  Status 8, the Option one of the three knights (1-3)", "3,8/True", $"{Mission(LogSandoria)}/{drillOption is "1" or "2" or "3"}");
        Warp(-482.0f, -7.0f, 222.0f, LaTheine);
        var (knight, swordEv) = drillOption switch { "1" => ("Galaihaurat", 114), "2" => ("Equesobillot", 112), _ => ("Deaufrain", 113) };
        TalkTo(knight);
        Cs($"{knight} (Option {drillOption}): event {swordEv}", swordEv);
        Check("  the Bronze Sword, Status 9", "1/3,9", $"{Qty(BronzeSword)}/{Mission(LogSandoria)}");
        Warp(-70.0f, 1.0f, 605.0f, OrdellesCaves);
        TradeTo("Ruillont", (Slot(BronzeSword), 1));
        Cs("the Bronze Sword to Ruillont: event 2", 2);
        Check("  the sword taken, Status 10", "0/3,10", $"{Qty(BronzeSword)}/{Mission(LogSandoria)}");
        Warp(-344.0f, 37.0f, 264.0f, LaTheine);
        int certificate = Ki("RESCUE_TRAINING_CERTIFICATE");
        TalkTo("Vicorpasse");
        Cs("Vicorpasse: event 115", 115);
        Check("  the Rescue Training Certificate, Status 11", "10/3,11", $"{DbKeyItem(certificate)}/{Mission(LogSandoria)}");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Cs("Endracion: event 1005", 1005);
        Check("  The Rescue Drill complete: 300 rank points, the certificate taken", "65535,0/2,983/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(certificate).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 3)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 2-2 The Davoi Report");
        TradeTo("Aravoge_TK", (Slot(FireCrystal), 1));
        Check("a crystal for 2-2's 1285 points", "2,1316", Rank());
        Accept("Endracion", 1009, 104, "The Davoi Report");
        Check("  The Davoi Report current", "4,0", Mission(LogSandoria));
        Warp(215.0f, 0.1f, -12.0f, Davoi);
        TalkTo("Zantaviat");
        Cs("Zantaviat in Davoi: event 100", 100);
        Check("  Status 1", "4,1", Mission(LogSandoria));
        int lostDoc = Ki("LOST_DOCUMENT"), davoiReport = Ki("TEMPLE_KNIGHTS_DAVOI_REPORT");
        var mark = zone.ZoneInfoOf(c.Zone).Npcs.First(n => n.Name == "!" && n.X > 210 && n.X < 212);
        Near(mark.Id);
        Talk(mark.Id);
        Check("the '!' at x 211: the Lost Document (no event), Status 2", $"none/10/4,2", $"{Cur()}/{DbKeyItem(lostDoc)}/{Mission(LogSandoria)}");
        TalkTo("Zantaviat");
        Cs("Zantaviat with the document: event 104", 104);
        Check("  the Temple Knights' Davoi Report for the Lost Document, Status 3", "0/10/4,3",
              $"{DbKeyItem(lostDoc).Substring(0, 1)}/{DbKeyItem(davoiReport)}/{Mission(LogSandoria)}");
        Warp(131.0f, -11.0f, 122.0f, NorthernSandoria);
        TalkTo("_6fc");
        Cs("the Papal Chambers' door (_6fc) with the report: event 695", 695);
        Check("  The Davoi Report complete: 350 rank points, the report taken", "65535,0/2,1666/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(davoiReport).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 4)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 2-3 Journey Abroad");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TradeTo("Aravoge_TK", (Slot(FireCrystal), 5));
        Check("five crystals for 2-3's 3204 points", "2,3331", Rank());
        Accept("Endracion", 1009, 5, "Journey Abroad");
        Check("  Journey Abroad current", "5,0", Mission(LogSandoria));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        int letter = Ki("LETTER_TO_THE_CONSULS_SANDORIA");
        TalkTo("Halver");
        Cs("Halver in the Chateau: event 505", 505);
        Check("  the Letter to the Consuls, Status 2", "10/5,2", $"{DbKeyItem(letter)}/{Mission(LogSandoria)}");
        Warp(23.7f, -17.4f, -41.4f, Metalworks);
        TalkTo("Savae_E_Paleade");
        Cs("Savae E Paleade in the Metalworks: event 204", 204);
        Check("  Journey to Bastok current (Journey Abroad removed), the letter taken, Status 3", "6,3/0",
              $"{Mission(LogSandoria)}/{DbKeyItem(letter).Substring(0, 1)}");
        TalkTo("Pius");
        Cs("Pius: event 355", 355);
        Check("  Status 4", "6,4", Mission(LogSandoria));
        TalkTo("Grohm");
        Cs("Grohm: event 423", 423);
        Check("  three pickaxes, Status 5", "3/6,5", $"{Qty(Pickaxe)}/{Mission(LogSandoria)}");
        TradeTo("Savae_E_Paleade", (Slot(MythrilSand), 1));
        Cs("an onz of Mythril Sand to Savae E Paleade: event 205", 205);
        Check("  Journey to Bastok complete, Journey Abroad again, Status 6, the sand taken", "5,6/True/0",
              $"{Mission(LogSandoria)}/{DbMissionComplete(LogSandoria, 6)}/{Qty(MythrilSand)}");
        Warp(-50.6f, -0.5f, -25.6f, WindurstWoods);
        TalkTo("Mourices");
        Cs("Mourices in Windurst Woods: event 462", 462);
        Check("  Journey to Windurst II current, Status 7", "9,7", Mission(LogSandoria));
        Warp(2.0f, 0.1f, 28.0f, HeavensTower);
        int darkKey = Ki("DARK_KEY"), crest = Ki("KINDRED_CREST"), kindredReport = Ki("KINDRED_REPORT");
        TalkTo("Kupipi");
        Cs("Kupipi in Heaven's Tower: event 242", 242);
        Check("  the Dark Key, Status 8", "10/9,8", $"{DbKeyItem(darkKey)}/{Mission(LogSandoria)}");
        Warp(299.0f, -123.0f, 343.0f, BalgasDais);
        Check("in Balga's Dais", BalgasDais, c.Zone);
        WinBattlefield(NpcId("BC_Entrance"), "Balga's Dais (Rank 2 mission)");
        Check("  the Kindred Crest for the Dark Key, Status 9", "10/0/9,9", $"{DbKeyItem(crest)}/{DbKeyItem(darkKey).Substring(0, 1)}/{Mission(LogSandoria)}");
        Warp(-50.6f, -0.5f, -25.6f, WindurstWoods);
        TalkTo("Mourices");
        Cs("Mourices with the crest: event 467", 467);
        Check("  Journey to Windurst II complete, Journey Abroad again, Status 11, the Kindred Report for the crest", "5,11/True/10/0",
              $"{Mission(LogSandoria)}/{DbMissionComplete(LogSandoria, 9)}/{DbKeyItem(kindredReport)}/{DbKeyItem(crest).Substring(0, 1)}");
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        int certified = TitleId("CERTIFIED_ADVENTURER"), advCert = Ki("ADVENTURERS_CERTIFICATE");
        TalkTo("Halver");
        Cs("Halver with the report: event 507", 507);
        Check("  Journey Abroad complete: rank 3, 3000 gil, the Adventurer's Certificate, Certified Adventurer, the report taken",
              $"65535,0/3/14000/10/True/0/True", $"{Mission(LogSandoria)}/{Lua("report(player:getRank(player:getNation()))")}/{Gil()}/{DbKeyItem(advCert)}/" +
              $"{DbTitle(certified)}/{DbKeyItem(kindredReport).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 5)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // crystals donated to the overseer (xi.conquest.overseerOnTrade): the char's fire crystals topped up to n (a GM
        // !additem, in stacks of 12), every stack traded
        void Donate(string overseer, int n)
        {
            int have = int.Parse(Qty(FireCrystal));
            for (int left = n - have; left > 0; left -= 12) Lua($"player:addItem({FireCrystal}, {Math.Min(left, 12)})");
            var stacks = new List<(byte, uint)>();
            var r = Sql(db, "SELECT slot, quantity FROM char_inventory WHERE charid = ? AND location = 0 AND itemId = ?", BoundValue.Of(CharId), BoundValue.Of(FireCrystal));
            while (r != null && r.Next()) stacks.Add((r.Get<byte>("slot"), r.Get<uint>("quantity")));
            TradeTo(overseer, stacks.ToArray());
        }

        Console.WriteLine("San d'Oria 3-1 Infiltrate Davoi");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 6);
        Check("six crystals at rank 3: 6 * floor(4000 / 24) for 3-1's 855.5 points", "3,996/0", $"{Rank()}/{Qty(FireCrystal)}");
        Accept("Endracion", 1009, 110, "Infiltrate Davoi");
        Check("  Infiltrate Davoi current", "10,0", Mission(LogSandoria));
        TalkTo("Endracion");
        Cs("Endracion: event 1029", 1029);
        Warp(-36.0f, -3.0f, 73.0f, Chateau);
        TalkTo("_6h0");
        Cs("the Prince Royal's door (_6h0): event 553", 553);
        Check("  Status 2", "10,2", Mission(LogSandoria));
        WarpIn(21.0f, 0.1f, -23.0f, Davoi);
        Check("in Davoi: the mission's zone-in cutscene 116", $"{Davoi}/116", $"{c.Zone}/{Cur()}");
        EventEnd(116);
        Check("  Status 3", "10,3", Mission(LogSandoria));
        int royalReport = Ki("ROYAL_KNIGHTS_DAVOI_REPORT");
        TalkTo("Quemaricond");
        Cs("Quemaricond (his path held): event 117", 117);
        Check("  the Royal Knights' Davoi Report, Status 4", "10/10,4", $"{DbKeyItem(royalReport)}/{Mission(LogSandoria)}");
        Warp(-36.0f, -3.0f, 73.0f, Chateau);
        TalkTo("_6h0");
        Cs("the Prince Royal's door with the report: event 554", 554);
        Check("  Infiltrate Davoi complete: 300 + 100 rank points, the report taken", "65535,0/3,1396/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(royalReport).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 10)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 3-2 The Crystal Spring");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 5);
        Check("five crystals for 3-2's 2166 points", "3,2226", Rank());
        Accept("Endracion", 1009, 111, "The Crystal Spring");
        Check("  The Crystal Spring current", "11,0", Mission(LogSandoria));
        Lua($"player:addItem({CrystalBass}, 1)");
        TradeTo("Endracion", (Slot(CrystalBass), 1));
        Cs("a Crystal Bass to Endracion: event 1030", 1030);
        Check("  the bass taken, Status 2", "0/11,2", $"{Qty(CrystalBass)}/{Mission(LogSandoria)}");
        TalkTo("Endracion");
        Cs("Endracion at Status 2: event 1031", 1031);
        // the Chateau's gate has no zone line in data/zones (northern_san_doria/zone.yaml): a GM !pos from Northern
        // San d'Oria, the prevZone the zone-in checks
        Warp(-241.0f, 7.0f, 57.9f, NorthernSandoria);
        WarpIn(0.0f, 0.0f, -30.0f, Chateau);
        Check("in the Chateau from Northern San d'Oria: the zone-in cutscene 555", $"{Chateau}/{NorthernSandoria}/555", $"{c.Zone}/{c.PrevZone}/{Cur()}");
        EventEnd(555);
        Check("  Status 3", "11,3", Mission(LogSandoria));
        TalkTo("Chalvatot");
        Cs("Chalvatot: event 556", 556);
        Check("  The Crystal Spring complete: 400 rank points", "65535,0/3,2626/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbMissionComplete(LogSandoria, 11)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 3-3 Appointment to Jeuno");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 6);
        Check("six crystals for 3-3's 3477 points", "3,3622", Rank());
        Accept("Endracion", 1009, 12, "Appointment to Jeuno");
        Check("  Appointment to Jeuno current", "12,0", Mission(LogSandoria));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver: event 508", 508);
        Check("  Status 2", "12,2", Mission(LogSandoria));
        int ambassadorLetter = Ki("LETTER_TO_THE_AMBASSADOR"), delkfuttKi = Ki("DELKFUTT_KEY");
        TalkTo("_6h4");
        Cs("the Great Hall's door (_6h4): event 537", 537);
        Check("  the Letter to the Ambassador, Status 3", "10/12,3", $"{DbKeyItem(ambassadorLetter)}/{Mission(LogSandoria)}");
        Warp(-32.0f, 9.0f, -47.0f, Rulude);
        TalkTo("Nelcabrit");
        Cs("Nelcabrit in Ru'Lude Gardens: event 42", 42);
        Check("  the letter taken, Status 4", "0/12,4", $"{DbKeyItem(ambassadorLetter).Substring(0, 1)}/{Mission(LogSandoria)}");
        Warp(634.0f, 16.0f, 20.0f, LowerDelkfutt);
        TalkTo("_541");
        Check("the Cermet Door with no key: THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY (no event)", "none", Cur());
        Lua($"player:addItem({DelkfuttKey}, 1)");
        TradeTo("_541", (Slot(DelkfuttKey), 1));
        Cs("the Delkfutt Key to the Cermet Door (_541): cutscene 0", 0);
        Check("  the key a key item now, the item taken, Status 5", "10/0/12,5", $"{DbKeyItem(delkfuttKi)}/{Qty(DelkfuttKey)}/{Mission(LogSandoria)}");
        Warp(-32.0f, 9.0f, -47.0f, Rulude);
        TalkTo("Nelcabrit");
        Cs("Nelcabrit at Status 5: event 140", 140);
        TalkTo("_6r5");
        Cs("the embassy door (_6r5): event 39", 39);
        Check("  Appointment to Jeuno complete: rank 4 with no points, 5000 gil", "65535,0/4,0/19000/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{DbMissionComplete(LogSandoria, 12)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 4-1 Magicite");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 20);
        Check("twenty crystals at rank 4: 20 * floor(4000 / 36) for Magicite's 2151 points", "4,2220", Rank());
        Warp(-32.0f, 9.0f, -47.0f, Rulude);
        TalkTo("Nelcabrit");
        Cs("Nelcabrit, the points met: event 45", 45);
        int permit = Ki("ARCHDUCAL_AUDIENCE_PERMIT"), aldoLetter = Ki("LETTER_TO_ALDO"), bell = Ki("SILVER_BELL"), torch = Ki("YAGUDO_TORCH");
        TalkTo("_6r5");
        Check("the embassy door: event 130 (no permit)", $"034:{NpcId("_6r5")}:130/130", $"{Ev()}/{Cur()}");
        EventEnd(130, 0);
        Check("  Magicite current, Status 1, the Archducal Audience Permit", "13,1/10", $"{Mission(LogSandoria)}/{DbKeyItem(permit)}");
        Warp(0.0f, -5.0f, 64.0f, Rulude);
        TalkTo("_6r9");
        Cs("the audience chamber (_6r9): event 128", 128);
        Check("  the Letter to Aldo, Status 2", "10/13,2", $"{DbKeyItem(aldoLetter)}/{Mission(LogSandoria)}");
        Warp(20.0f, 3.0f, -56.0f, LowerJeuno);
        TalkTo("Aldo");
        Check("Aldo: event 152 (no Silver Bell)", $"034:{NpcId("Aldo")}:152:0,1", Ev());
        EventEnd(152);
        Check("  the Silver Bell for the letter, Status 3", "10/0/13,3", $"{DbKeyItem(bell)}/{DbKeyItem(aldoLetter).Substring(0, 1)}/{Mission(LogSandoria)}");
        Warp(9.0f, 1.0f, 68.0f, UpperJeuno);
        TalkTo("Paya-Sabya");
        Cs("Paya-Sabya: event 80", 80);
        Check("  Option 1", "1", Var($"Mission[{LogSandoria}][13]Option"));
        Warp(-26.8f, 4.6f, -135.0f, LowerJeuno);
        TalkTo("Muckvix");
        Cs("Muckvix: event 184", 184);
        Check("  the Yagudo Torch, Option 2", "10/2", $"{DbKeyItem(torch)}/{Var($"Mission[{LogSandoria}][13]Option")}");
        WarpIn(-344.0f, 25.0f, 45.0f, AltarRoom);
        Check("in the Altar Room: the Fickblix zone-in cutscene 10000", $"{AltarRoom}/10000", $"{c.Zone}/{Cur()}");
        EventEnd(10000);
        Check("  Option cleared", "0", Var($"Mission[{LogSandoria}][13]Option") == "-" ? "0" : Var($"Mission[{LogSandoria}][13]Option"));
        int orastone = Ki("MAGICITE_ORASTONE"), optistone = Ki("MAGICITE_OPTISTONE"), aurastone = Ki("MAGICITE_AURASTONE");
        TalkTo("Magicite");
        Check("the Altar Room's Magicite: event 44 with the zone", $"034:{NpcId("Magicite")}:44:{AltarRoom}", Ev());
        EventEnd(44);
        Check("  the Magicite Orastone", "10", DbKeyItem(orastone));
        Warp(-160.0f, -8.0f, 10.0f, MonasticCavern);
        TalkTo("Magicite");
        Check("the Monastic Cavern's Magicite: event 0 with the zone", $"034:{NpcId("Magicite")}:0:{MonasticCavern}", Ev());
        EventEnd(0);
        Check("  the Magicite Optistone", "10", DbKeyItem(optistone));
        Warp(11.0f, 25.0f, -79.0f, QulunDome);
        TalkTo("Magicite");
        Check("Qulun Dome's Magicite, the last one: event 0 with the Lion's part (1, 46, 47)", $"034:{NpcId("Magicite")}:0:1,46,47", Ev());
        EventEnd(0);
        Check("  the Magicite Aurastone", "10", DbKeyItem(aurastone));
        Warp(0.0f, -5.0f, 64.0f, Rulude);
        int airshipPass = Ki("AIRSHIP_PASS"), titleWings = TitleId("HAVE_WINGS_WILL_FLY"), message = Ki("MESSAGE_TO_JEUNO_SANDORIA");
        TalkTo("_6r9");
        Check("the audience chamber with the three stones: event 60 (no Airship Pass)", $"034:{NpcId("_6r9")}:60", Ev());
        EventEnd(60);
        Check("  the stones taken, the Airship Pass, Have Wings, Will Fly, Status 4", "0/0/0/10/True/13,4",
              $"{DbKeyItem(orastone).Substring(0, 1)}/{DbKeyItem(optistone).Substring(0, 1)}/{DbKeyItem(aurastone).Substring(0, 1)}/" +
              $"{DbKeyItem(airshipPass)}/{DbTitle(titleWings)}/{Mission(LogSandoria)}");
        Warp(-32.0f, 9.0f, -47.0f, Rulude);
        TalkTo("Nelcabrit");
        Cs("Nelcabrit: event 36", 36);
        Check("  Magicite complete: rank 5, 10000 gil, the Message to Jeuno", "65535,0/5,0/29000/10/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{DbKeyItem(message)}/{DbMissionComplete(LogSandoria, 13)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 5-1 The Ruins of Fei'Yin");
        Warp(-241.0f, 7.0f, 57.9f, NorthernSandoria);
        WarpIn(0.0f, 0.0f, -30.0f, Chateau);
        Check("in the Chateau from Northern San d'Oria at rank 5: the zone-in cutscene 509", $"{Chateau}/509", $"{c.Zone}/{Cur()}");
        EventEnd(509);
        Check("  Status 8", "65535,8", Mission(LogSandoria));
        TalkTo("Halver");
        Check("Halver before the mission: SHALL_BEGIN_DISCUSSION (no event)", "none", Cur());
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Check("Endracion at Status 8: the mission list with only 5-1 (MAX_INT32 - 16384), the repeatables", $"034:{NpcId("Endracion")}:1009:{int.MaxValue - 16384},0,0,0,0,3095", Ev());
        EventEnd(1009, 14);
        Check("  The Ruins of Fei'Yin current, Status 9, the message taken", "14,9/0", $"{Mission(LogSandoria)}/{DbKeyItem(message).Substring(0, 1)}");
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        int newSeal = Ki("NEW_FEIYIN_SEAL"), burntSeal = Ki("BURNT_SEAL"), titleArchmage = TitleId("ARCHMAGE_ASSASSIN");
        TalkTo("Halver");
        Cs("Halver: event 533", 533);
        Check("  the New Fei'Yin Seal, Status 10", "10/14,10", $"{DbKeyItem(newSeal)}/{Mission(LogSandoria)}");
        WarpIn(243.0f, -24.0f, 62.0f, FeiYin);
        Check("in Fei'Yin: the zone-in cutscene 1", $"{FeiYin}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Check("  Status 11", "14,11", Mission(LogSandoria));
        Warp(-221.0f, -24.0f, 19.0f, QubiaArena);
        WinBattlefield(NpcId("BC_Entrance"), "Qu'Bia Arena (Rank 5 mission)");
        Check("  the Burnt Seal for the new seal, Archmage Assassin, Status 12", "10/0/True/14,12",
              $"{DbKeyItem(burntSeal)}/{DbKeyItem(newSeal).Substring(0, 1)}/{DbTitle(titleArchmage)}/{Mission(LogSandoria)}");
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver with the Burnt Seal: event 534", 534);
        Check("  The Ruins of Fei'Yin complete: 400 rank points, the seal taken", "65535,0/5,400/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(burntSeal).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 14)}");
        TalkTo("Curilla");
        Cs("Curilla after 5-1: event 545", 545);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the nation's 5-2 in the Throne Room (xi.mission.id.nation.SHADOW_LORD, 15 in each log): the battlefield from its
        // entry, phase 1 killed (32004), phase 2 held at 1 HP until his death animation, the win, the setPos's zone-in
        // cutscene 7 and the Shadow Fragment
        void FightShadowLord(int log)
        {
            var slBf = EnterBattlefield(NpcId("_4l1"), "the Throne Room (The Shadow Lord)");
            uint slOffset = uint.Parse(Lua("report(zones[xi.zone.THRONE_ROOM].mob.SHADOW_LORD_RANK_5_OFFSET)"));
            var slInfo = zone.ZoneInfoOf(c.Zone);
            var phase1 = slInfo.MobById[slOffset];
            Check("  phase 1 (the area's first Shadow Lord) up", "True", phase1.IsSpawned);
            KillByChar(slOffset);
            for (int i = 0; i < 10 && Cur() != "32004"; i++) Tick(400);
            // the event's entity is the eventPreparation's, the mob the char fought (luautils::OnMobDeath)
            Check("phase 1 dead: the group's death handler starts 32004 with the area", $"034:{slOffset}:32004:1/32004", $"{Ev()}/{Cur()}");
            EventEnd(32004);
            Tick(1000);
            Tick(1000);
            Tick(1000);
            var phase2 = slInfo.MobById[slOffset + 3];
            Check("  32004 finished (onEventFinishBattlefield): phase 1 despawned (3 s fade), phase 2 up and engaged on the char", "False/True/True",
                  $"{phase1.IsSpawned}/{phase2.IsSpawned}/{phase2.Engaged}");
            Check("  phase 2 unkillable, no auto attack", "True", Lua($"report(tostring(GetMobByID({slOffset + 3}):getUnkillable()))") == "true");
            Lua($"local mob = GetMobByID({slOffset + 3}) mob:takeDamage(mob:getHP() + 100, player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            Check("the killing blow: held at 1 HP (setUnkillable), the TAKE_DAMAGE listener stamps [ShadowLord]Defeated", "1/True",
                  $"{phase2.Hp}/{int.Parse(Lua($"report(GetMobByID({slOffset + 3}):getLocalVar('[ShadowLord]Defeated'))")) > 0}");
            // the scripts time phase 2 on GetSystemTime (the wall clock): the 9 seconds to his next tp move and the 12 of
            // the death animation are taken off the local vars rather than waited
            Lua($"GetMobByID({slOffset + 3}):setLocalVar('lastImplodeTime', GetSystemTime() - 10)");
            for (int i = 0; i < 10 && Lua($"report(GetMobByID({slOffset + 3}):getLocalVar('[ShadowLord]DefeatTime'))") == "0"; i++) Tick(1000);
            Check("  onMobFight: Shadow Lord Death used, [ShadowLord]DefeatTime stamped (GetSystemTime + 12)", "True",
                  Lua($"report(GetMobByID({slOffset + 3}):getLocalVar('[ShadowLord]DefeatTime'))") != "0");
            Lua($"GetMobByID({slOffset + 3}):setLocalVar('[ShadowLord]DefeatTime', GetSystemTime() - 1)");
            for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
            Check("  onBattlefieldTick: setUnkillable(false), setHP(0), the death, won: 32001", "2/32001/False", $"{slBf?.Status}/{Cur()}/{phase2.IsAlive}");
            int fragment = Ki("SHADOW_FRAGMENT"), titleBanisher = TitleId("SHADOW_BANISHER");
            EventEnd(32001);
            Rezone(false);
            Check("32001 finished: setPos in the Throne Room, the zone-in cutscene 7", $"{ThroneRoom}/7", $"{c.Zone}/{Cur()}");
            Check("  The New Frontier flagged (Zilart), PostBattle 1, Shadow Banisher", $"0/1/True",
                  $"{Lua("report(player:getCurrentMission(xi.mission.log_id.ZILART))")}/{Var($"Mission[{log}][15]PostBattle")}/{DbTitle(titleBanisher)}");
            EventEnd(7);
            Check("cutscene 7 finished: the Shadow Fragment, Status 4, PostBattle cleared", "10/15,4/-", $"{DbKeyItem(fragment)}/{Mission(log)}/{Var($"Mission[{log}][15]PostBattle")}");
        }

        Console.WriteLine("San d'Oria 5-2 The Shadow Lord");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 20);
        Check("twenty crystals at rank 5: 20 * floor(4000 / 48) for 5-2's 2018 points", "5,2060", Rank());
        Accept("Endracion", 1009, 15, "The Shadow Lord");
        Check("  The Shadow Lord current", "15,0", Mission(LogSandoria));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver: event 546", 546);
        TalkTo("_6h0");
        Cs("the Prince Royal's door: event 547", 547);
        Check("  Status 2", "15,2", Mission(LogSandoria));
        Warp(-109.0f, -6.0f, 0.1f, ThroneRoom);
        TalkTo("_4l1");
        Cs("the Throne Room's door (_4l1) at Status 2: the mission's event 6", 6);
        Check("  Status 3", "15,3", Mission(LogSandoria));
        FightShadowLord(LogSandoria);
        int fragment = Ki("SHADOW_FRAGMENT");
        Rezone();
        Check("  the zone's onEventFinish for 7: back to Castle Zvahl Baileys' entrance", $"{CastleZvahlBaileys}/380/-20", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver with the fragment: event 548", 548);
        Check("  The Shadow Lord complete: rank 6, 20000 gil, the fragment taken, hallEvent 1", "65535,0/6,0/49000/0/1/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{DbKeyItem(fragment).Substring(0, 1)}/{Var($"Mission[{LogSandoria}][15]hallEvent")}/{DbMissionComplete(LogSandoria, 15)}");
        TalkTo("_6h4");
        Cs("the Great Hall's door: event 61", 61);
        Check("  hallEvent cleared", "-", Var($"Mission[{LogSandoria}][15]hallEvent"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the zone's mob of that name in its zones[].mob table
        uint MobOf(string zoneName, string name) => uint.Parse(Lua($"report(zones[xi.zone.{zoneName}].mob.{name})"));
        // the char walks into a trigger area: the ZoningIn grace of xi.player.onGameIn waited out, the position update,
        // then the zone's trigger-area tick
        void Walk(float x, float y, float z)
        {
            Tick(3000);
            MoveTo(x, y, z);
            Tick(400);
        }
        string SVar(int mission, string name) => Var($"Mission[{LogSandoria}][{mission}]{name}");

        Console.WriteLine("San d'Oria 6-1 Leaute's Last Wishes");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 19);
        Check("nineteen crystals at rank 6 for 6-1's 1236 points", "6,1254", Rank());
        Accept("Endracion", 1009, 16, "Leaute's Last Wishes");
        Check("  Leaute's Last Wishes current", "16,0", Mission(LogSandoria));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver: event 25", 25);
        TalkTo("_6h4");
        Cs("the Great Hall's door: event 87", 87);
        Check("  Status 2", "16,2", Mission(LogSandoria));
        Warp(-262.4f, -10.2f, 51.2f, WesternAltepa);
        uint enamorado = MobOf("WESTERN_ALTEPA_DESERT", "SABOTENDER_ENAMORADO");
        int dreamrose = Ki("DREAMROSE");
        TalkTo("Dreamrose");
        Check("the Dreamrose: the Sabotender Enamorado spawned and claimed (FEEL_SOMETHING_PRICKLY)", $"none/True/{c.Id}",
              $"{Cur()}/{zone.ZoneInfoOf(c.Zone).MobById[enamorado].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[enamorado].OwnerId}");
        KillByChar(enamorado);
        Check("  the Enamorado killed: onMobDeath sets Progress 1", "1", SVar(16, "Progress"));
        TalkTo("Dreamrose");
        Check("the Dreamrose again: the Dreamrose key item, Status 3, Progress cleared", "10/16,3/-", $"{DbKeyItem(dreamrose)}/{Mission(LogSandoria)}/{SVar(16, "Progress")}");
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver with the Dreamrose: event 22", 22);
        Check("  Status 4", "16,4", Mission(LogSandoria));
        int paper = Ki("PIECE_OF_PAPER");
        Walk(-84.0f, -1.4f, 57.7f);
        Cs("the stairs to the garden (trigger area 2): event 111", 111);
        Check("  Leaute's Last Wishes complete: 600 rank points, the Piece of Paper, the Dreamrose taken", "65535,0/6,1854/10/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(paper)}/{DbKeyItem(dreamrose).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 16)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 6-2 Ranperre's Final Rest");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 21);
        Check("twenty-one crystals for 6-2's 3207 points", "6,3240", Rank());
        Accept("Endracion", 1009, 17, "Ranperre's Final Rest");
        Check("  Ranperre's Final Rest current", "17,0", Mission(LogSandoria));
        Warp(-36.0f, -3.0f, 73.0f, Chateau);
        TalkTo("_6h0");
        Cs("the Prince Royal's door: event 81", 81);
        Check("  Status 1", "17,1", Mission(LogSandoria));
        Warp(-37.0f, 4.8f, 20.0f, RanperresTomb);
        uint[] corrupted = { MobOf("KING_RANPERRES_TOMB", "CORRUPTED_YORGOS"), MobOf("KING_RANPERRES_TOMB", "CORRUPTED_SOFFEIL"), MobOf("KING_RANPERRES_TOMB", "CORRUPTED_ULBRIG") };
        TalkTo("_5a0");
        Check("the Heavy Stone Door (_5a0): popFromQM spawns the three Corrupted (SENSE_SOMETHING_EVIL)", "none/True,True,True",
              $"{Cur()}/{string.Join(",", corrupted.Select(id => zone.ZoneInfoOf(c.Zone).MobById[id].IsSpawned))}");
        foreach (var id in corrupted) KillByChar(id);
        Check("  the three killed: the last onMobDeath sets Status 2", "17,2", Mission(LogSandoria));
        MoveTo(-37.0f, 4.8f, 20.0f);
        Talk(NpcId("_5a0"));
        Check("the door from the near side (x > -39.019): event 6", "6", Cur());
        // the cutscene takes the char through the door: the client's position update on the far side
        MoveTo(-41.0f, 4.8f, 20.0f);
        EventEnd(6);
        Check("  finished past the door (x <= -39.019): Status 3", "17,3", Mission(LogSandoria));
        int book = Ki("ANCIENT_SAN_DORIAN_BOOK");
        TalkTo("Tombstone_Lower");
        Cs("the lower tombstone: event 8", 8);
        Check("  the Ancient San d'Orian Book for the Piece of Paper", "10/0", $"{DbKeyItem(book)}/{DbKeyItem(paper).Substring(0, 1)}");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Cs("Endracion with the book: event 1035", 1035);
        Check("  the book taken, Status 4, requiredToZone set", "0/17,4/1", $"{DbKeyItem(book).Substring(0, 1)}/{Mission(LogSandoria)}/{Lua("report(player:getLocalVar('Mission[0][17]requiredToZone'))")}");
        TalkTo("Endracion");
        Cs("Endracion before zoning: event 1037", 1037);
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Cs("Endracion after zoning (the local var gone): event 1039", 1039);
        Check("  Status 5", "17,5", Mission(LogSandoria));
        Warp(-36.0f, -3.0f, 73.0f, Chateau);
        TalkTo("_6h0");
        Cs("the Prince Royal's door: event 21", 21);
        Warp(-37.0f, 4.8f, 20.0f, RanperresTomb);
        TalkTo("_5a0");
        Cs("the Heavy Stone Door at Status 6: event 5", 5);
        Check("  Status 7", "17,7", Mission(LogSandoria));
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Cs("Endracion: event 1033", 1033);
        Check("  Ranperre's Final Rest complete: rank 7, 40000 gil", "65535,0/7,0/89000/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{DbMissionComplete(LogSandoria, 17)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 7-1 Prestige of the Papsque");
        Donate("Aravoge_TK", 22);
        Check("twenty-two crystals at rank 7 for 7-1's 1167 points", "7,1210", Rank());
        Accept("Endracion", 1009, 18, "Prestige of the Papsque");
        Check("  Prestige of the Papsque current", "18,0", Mission(LogSandoria));
        Warp(131.0f, -11.0f, 122.0f, NorthernSandoria);
        TalkTo("_6fc");
        Cs("the Papal Chambers: event 7", 7);
        Warp(-695.0f, -40.0f, 23.0f, WestRonfaure);
        uint dvogzog = MobOf("WEST_RONFAURE", "MARAUDER_DVOGZOG");
        int tablet = Ki("ANCIENT_SAN_DORIAN_TABLET");
        TalkTo("qm4");
        Check("the ??? in West Ronfaure: Marauder Dvogzog spawned and claimed", $"True/{c.Id}",
              $"{zone.ZoneInfoOf(c.Zone).MobById[dvogzog].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[dvogzog].OwnerId}");
        KillByChar(dvogzog);
        Check("  Dvogzog killed: the Stage local var", "1", Lua("report(player:getLocalVar('Mission[0][18]Stage'))"));
        TalkTo("qm4");
        Check("the ??? while his body lies there (isSpawned): nothing", "00/18,1", $"{DbKeyItem(tablet)}/{Mission(LogSandoria)}");
        for (int i = 0; i < 60 && zone.ZoneInfoOf(c.Zone).MobById[dvogzog].IsSpawned; i++) Tick(1000);
        Check("  the body gone (the death state's despawn)", "False", zone.ZoneInfoOf(c.Zone).MobById[dvogzog].IsSpawned);
        TalkTo("qm4");
        Check("the ??? again: the Ancient San d'Orian Tablet, Status 2", "10/18,2", $"{DbKeyItem(tablet)}/{Mission(LogSandoria)}");
        Warp(131.0f, -11.0f, 122.0f, NorthernSandoria);
        TalkTo("_6fc");
        Cs("the Papal Chambers with the tablet: event 8", 8);
        Check("  Prestige of the Papsque complete: 1000 rank points, the tablet taken", "65535,0/7,2210/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(tablet).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 18)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 7-2 The Secret Weapon");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 17);
        Check("seventeen crystals for 7-2's 3097 points", "7,3145", Rank());
        TalkTo("Endracion");
        Cs("Endracion, 7-2's points met: event 1041 (before the mission list)", 1041);
        Check("  Status 1 with no mission", "65535,1", Mission(LogSandoria));
        Warp(-90.0f, 1.0f, 70.0f, Chateau);
        Walk(-90.0f, 1.0f, 80.0f);
        Cs("the garden by Chalvatot (trigger area 1): event 0", 0);
        Check("  Status 2", "65535,2", Mission(LogSandoria));
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Accept("Endracion", 1009, 19, "The Secret Weapon");
        Check("  The Secret Weapon current, Status 2 kept", "19,2", Mission(LogSandoria));
        Warp(-509.0f, 158.0f, -211.0f, HorlaisPeak);
        int dowser = Ki("CRYSTAL_DOWSER");
        WinBattlefield(NpcId("BC_Entrance"), "Horlais Peak (The Secret Weapon)");
        Check("  the Crystal Dowser, Status 3", "10/19,3", $"{DbKeyItem(dowser)}/{Mission(LogSandoria)}");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        // the gil the kills dropped on the way kept apart
        int gil0 = int.Parse(Gil());
        TalkTo("Endracion");
        Cs("Endracion with the dowser: event 1043", 1043);
        Check("  The Secret Weapon complete: rank 8, 60000 gil, the dowser taken", $"65535,0/8,0/{gil0 + 60000}/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{DbKeyItem(dowser).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 19)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 8-1 Coming of Age");
        Donate("Aravoge_TK", 25);
        Check("twenty-five crystals at rank 8 for 8-1's 1136 points", "8,1175", Rank());
        Accept("Endracion", 1009, 20, "Coming of Age");
        Check("  Coming of Age current", "20,0", Mission(LogSandoria));
        WarpIn(2.0f, 0.1f, 2.0f, Chateau);
        Check("in the Chateau: the zone-in cutscene 116", $"{Chateau}/116", $"{c.Zone}/{Cur()}");
        EventEnd(116);
        TalkTo("Halver");
        Cs("Halver: event 58", 58);
        Check("  Status 2", "20,2", Mission(LogSandoria));
        Warp(573.0f, 9.0f, -500.0f, QuicksandCaves);
        uint valor = MobOf("QUICKSAND_CAVES", "VALOR"), honor = MobOf("QUICKSAND_CAVES", "HONOR");
        int amnio = Ki("DROPS_OF_AMNIO");
        TalkTo("Fountain_of_Kings");
        Check("the Fountain of Kings: Valor and Honor spawned", "True/True",
              $"{zone.ZoneInfoOf(c.Zone).MobById[valor].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[honor].IsSpawned}");
        KillByChar(valor);
        // the mission's Valor handler looks up Valor itself for Honor (local mobHonor = GetMobByID(...VALOR)), so the
        // first kill is enough
        Check("  Valor killed, Honor up: Status 3 already (the script's handler checks Valor twice)", "20,3", Mission(LogSandoria));
        KillByChar(honor);
        Check("  both killed: Status 3", "20,3", Mission(LogSandoria));
        TalkTo("Fountain_of_Kings");
        Check("the fountain again: the Drops of Amnio", "10", DbKeyItem(amnio));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver with the amnio: event 102", 102);
        long sysNow = long.Parse(Lua("report(GetSystemTime())"));
        long progress = long.TryParse(SVar(20, "Progress"), out var pv) ? pv : 0;
        Check("  Coming of Age complete: 800 rank points, the amnio taken, Progress the wall clock + 60", "65535,0/8,1975/0/True/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(amnio).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 20)}/{progress >= sysNow + 58 && progress <= sysNow + 60}");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        TalkTo("Endracion");
        Check("Endracion before the final cutscene: ORIGINAL_MISSION_OFFSET + 127 (no event)", "none", Cur());
        WarpIn(-241.0f, 7.0f, 57.9f, NorthernSandoria);
        Check("Northern San d'Oria within the minute: no cutscene", $"{NorthernSandoria}/none", $"{c.Zone}/{Cur()}");
        // the minute on the wall clock (GetSystemTime) the scripts wait: taken off the var rather than waited
        Lua($"player:setCharVar('Mission[{LogSandoria}][20]Progress', GetSystemTime() - 1)");
        WarpIn(-240.0f, 7.0f, 57.9f, NorthernSandoria);
        Check("Northern San d'Oria past the minute: the zone-in cutscene 16", $"{NorthernSandoria}/16", $"{c.Zone}/{Cur()}");
        EventEnd(16);
        Check("  Progress cleared", "-", SVar(20, "Progress"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 8-2 Lightbringer");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 21);
        Check("twenty-one crystals for 8-2's 2958 points", "8,2962", Rank());
        Accept("Endracion", 1009, 21, "Lightbringer");
        Check("  Lightbringer current", "21,0", Mission(LogSandoria));
        Warp(0.0f, -1.0f, 15.0f, Chateau);
        TalkTo("_6h4");
        Cs("the Great Hall's door: event 100", 100);
        TalkTo("Rahal");
        Cs("Rahal: event 106", 106);
        Check("  the Crystal Dowser, Status 2", "10/21,2", $"{DbKeyItem(dowser)}/{Mission(LogSandoria)}");
        TalkTo("Curilla");
        Cs("Curilla at Status 2: event 57", 57);
        Warp(-20.0f, 0.0f, 0.0f, TempleOfUggalepih);
        int key1 = Ki("PIECE_OF_A_BROKEN_KEY1"), key2 = Ki("PIECE_OF_A_BROKEN_KEY2"), key3 = Ki("PIECE_OF_A_BROKEN_KEY3");
        foreach (var (qm, ki, after) in new[] { ("qm_key1", key1, 3), ("qm_key2", key2, 4), ("qm_key3", key3, 5) })
        {
            TalkTo(qm);
            Check($"the Temple of Uggalepih's {qm}: the broken key piece, Status {after}", $"10/21,{after}", $"{DbKeyItem(ki)}/{Mission(LogSandoria)}");
        }
        uint nioA = MobOf("TEMPLE_OF_UGGALEPIH", "NIO_A"), nioHum = MobOf("TEMPLE_OF_UGGALEPIH", "NIO_HUM");
        TalkTo("_4fv");
        Check("the door (_4fv) with the three pieces: Nio-A and Nio-Hum spawned", "none/True/True",
              $"{Cur()}/{zone.ZoneInfoOf(c.Zone).MobById[nioA].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[nioHum].IsSpawned}");
        KillByChar(nioA);
        KillByChar(nioHum);
        Check("  both killed: Prog 1", "1", SVar(21, "Prog"));
        TalkTo("_4fv");
        Cs("the door again: event 65", 65);
        Check("  the pieces taken, Status 6", "0/0/0/21,6", $"{DbKeyItem(key1).Substring(0, 1)}/{DbKeyItem(key2).Substring(0, 1)}/{DbKeyItem(key3).Substring(0, 1)}/{Mission(LogSandoria)}");
        Warp(0.0f, -1.0f, 15.0f, Chateau);
        gil0 = int.Parse(Gil());
        TalkTo("_6h4");
        Cs("the Great Hall's door at Status 6: event 104", 104);
        Check("  Lightbringer complete: rank 9 with no points, 80000 gil, the dowser taken", $"65535,0/9,0/{gil0 + 80000}/0/True",
              $"{Mission(LogSandoria)}/{Rank()}/{Gil()}/{DbKeyItem(dowser).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 21)}");
        TalkTo("Rahal");
        Cs("Rahal just after (rank 9, no points): event 42 (replaceDefault)", 42);
        TalkTo("_6h0");
        Cs("the Prince Royal's door after Lightbringer: event 63", 63);
        Check("  Option bit 0", "1", SVar(21, "Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 9-1 Breaking Barriers");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 37);
        Check("thirty-seven crystals at rank 9 for 9-1's 1502 points", "9,1517", Rank());
        Accept("Endracion", 1009, 22, "Breaking Barriers");
        Check("  Breaking Barriers current", "22,0", Mission(LogSandoria));
        Warp(0.0f, -1.0f, 15.0f, Chateau);
        TalkTo("_6h4");
        Cs("the Great Hall's door: event 32", 32);
        Check("  Status 1", "22,1", Mission(LogSandoria));
        int titan = Ki("FIGURE_OF_TITAN"), garuda = Ki("FIGURE_OF_GARUDA"), leviathan = Ki("FIGURE_OF_LEVIATHAN");
        Warp(-11.0f, 0.0f, -80.0f, ValleyOfSorrows);
        TalkTo("qm_breaking_barriers");
        Cs("the Valley of Sorrows' ???: event 0", 0);
        Check("  the Figure of Titan, Status 2", "10/22,2", $"{DbKeyItem(titan)}/{Mission(LogSandoria)}");
        Warp(-340.0f, -24.0f, 30.0f, Xarcabard);
        TalkTo("qm5");
        Cs("Xarcabard's ???: event 12", 12);
        Check("  the Figure of Garuda, Status 3", "10/22,3", $"{DbKeyItem(garuda)}/{Mission(LogSandoria)}");
        Warp(-405.0f, -19.0f, -30.0f, BatalliaDowns);
        uint suparna = MobOf("BATALLIA_DOWNS", "SUPARNA"), fledgling = MobOf("BATALLIA_DOWNS", "SUPARNA_FLEDGLING");
        TalkTo("qm3");
        Check("Batallia Downs' ???: Suparna and the fledgling spawned", "True/True",
              $"{zone.ZoneInfoOf(c.Zone).MobById[suparna].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[fledgling].IsSpawned}");
        KillByChar(suparna);
        KillByChar(fledgling);
        Check("  both killed: Prog 1", "1", SVar(22, "Prog"));
        TalkTo("qm3");
        Cs("the ??? again: event 904", 904);
        Check("  the Figure of Leviathan, Status 4", "10/22,4", $"{DbKeyItem(leviathan)}/{Mission(LogSandoria)}");
        Warp(0.0f, -1.0f, 15.0f, Chateau);
        TalkTo("_6h4");
        Cs("the Great Hall's door with the three figures: event 76", 76);
        Check("  Breaking Barriers complete: 900 rank points, the figures taken", "65535,0/9,2417/000/True",
              $"{Mission(LogSandoria)}/{Rank()}/{DbKeyItem(titan).Substring(0, 1)}{DbKeyItem(garuda).Substring(0, 1)}{DbKeyItem(leviathan).Substring(0, 1)}/{DbMissionComplete(LogSandoria, 22)}");
        // the mission's after-completion section keys Curilla and Rahal with no zone table, so its events never match:
        // Curilla's own default
        TalkTo("Curilla");
        Cs("Curilla after 9-1: her default event 530 (the section's handlers sit outside a zone table)", 530);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("San d'Oria 9-2 The Heir to the Light");
        Warp(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Donate("Aravoge_TK", 29);
        Check("twenty-nine crystals for 9-2's 3574 points", "9,3606", Rank());
        Accept("Endracion", 1009, 23, "The Heir to the Light");
        Check("  The Heir to the Light current", "23,0", Mission(LogSandoria));
        WarpIn(-241.0f, 7.0f, 57.9f, NorthernSandoria);
        Check("in Northern San d'Oria: the zone-in cutscene 1", $"{NorthernSandoria}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        WarpIn(2.0f, 0.1f, 2.0f, Chateau);
        Check("in the Chateau at Status 1: the zone-in cutscene 10", $"{Chateau}/10", $"{c.Zone}/{Cur()}");
        EventEnd(10);
        Check("  Status 2", "23,2", Mission(LogSandoria));
        TalkTo("Perfaumand");
        Cs("Perfaumand at Status 2: event 2", 2);
        WarpIn(243.0f, -24.0f, 62.0f, FeiYin);
        Check("in Fei'Yin: the zone-in cutscene 23", $"{FeiYin}/23", $"{c.Zone}/{Cur()}");
        EventEnd(23);
        Check("  Status 3", "23,3", Mission(LogSandoria));
        Warp(-221.0f, -24.0f, 19.0f, QubiaArena);
        var heirBf = EnterBattlefield(NpcId("BC_Entrance"), "Qu'Bia Arena (Heir to the Light)");
        uint rojgnoj = MobOf("QUBIA_ARENA", "WARLORD_ROJGNOJ");
        var qInfo = zone.ZoneInfoOf(c.Zone);
        var phaseOne = Enumerable.Range(3, 11).Select(i => qInfo.MobById[rojgnoj + (uint)i]).ToList();
        Check("  phase 1: the eleven orcs up", "11", phaseOne.Count(m => m.IsSpawned));
        foreach (var m in phaseOne) m.Hp = 0;
        for (int i = 0; i < 30 && Cur() != "32004"; i++) Tick(400);
        Check("phase 1 dead: allDeath starts 32004 with the area (first viewing)", "32004", Cur());
        EventEnd(32004);
        var phaseTwo = Enumerable.Range(0, 3).Select(i => qInfo.MobById[rojgnoj + (uint)i]).ToList();
        Check("  32004 finished: Intro 1, phase 2 (Warlord Rojgnoj and his two) up, Prince Trion inserted as an ally, the char moved",
              "1/3/true/-398", $"{SVar(23, "Intro")}/{phaseTwo.Count(m => m.IsSpawned)}/" +
              $"{Lua("local t = nil for _, e in pairs(player:getBattlefield():getAllies()) do t = e end report(tostring(t ~= nil))")}/{MathF.Round(c.X)}");
        foreach (var m in phaseTwo) m.Hp = 0;
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("  phase 2 dead, won: 32001", "2/32001", $"{heirBf?.Status}/{Cur()}");
        EventEnd(32001);
        Check("32001 finished: Status 4", "23,4", Mission(LogSandoria));
        Rezone();
        WarpIn(-241.0f, 7.0f, 57.9f, NorthernSandoria);
        Check("in Northern San d'Oria at Status 4: the zone-in cutscene 0", $"{NorthernSandoria}/0", $"{c.Zone}/{Cur()}");
        EventEnd(0);
        Check("  Status 5", "23,5", Mission(LogSandoria));
        Warp(0.0f, -1.0f, 15.0f, Chateau);
        TalkTo("_6h4");
        Cs("the Great Hall's door at Status 5: event 8", 8);
        Warp(-37.0f, 4.8f, 20.0f, RanperresTomb);
        TalkTo("_5a0");
        Cs("the Heavy Stone Door at Status 6: event 14", 14);
        Check("  Status 7", "23,7", Mission(LogSandoria));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        int titleHeir = TitleId("SAN_DORIAN_ROYAL_HEIR");
        gil0 = int.Parse(Gil());
        TalkTo("Halver");
        Cs("Halver at Status 7: event 9", 9);
        Check("  The Heir to the Light complete: rank 10, 100000 gil, San d'Orian Royal Heir, the San d'Orian Flag, Option 1",
              $"65535,0/10/{gil0 + 100000}/True/1/1/True", $"{Mission(LogSandoria)}/{Lua("report(player:getRank(player:getNation()))")}/{Gil()}/{DbTitle(titleHeir)}/" +
              $"{Qty(SanDorianFlag)}/{SVar(23, "Option")}/{DbMissionComplete(LogSandoria, 23)}");
        Check("  the 8-2 Prince cutscene var cleared", "-", SVar(21, "Option"));
        TalkTo("Curilla");
        Cs("Curilla after 9-2: event 20", 20);
        WarpIn(-108.5f, 1.0f, -34.0f, SouthernSandoria);
        Check("in Southern San d'Oria with Option 1: the zone-in cutscene 0", $"{SouthernSandoria}/0", $"{c.Zone}/{Cur()}");
        EventEnd(0);
        Check("  Option cleared", "-", SVar(23, "Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Bastok ----
        Bastok:
        Console.WriteLine("Bastok: the login in Port Bastok by Argus");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[1];
        Fixture(db, PortBastok, PortBastok, 130.6f, 7.5f, -2.2f, 1, (QuadavFetichHead, 1), (QuadavFetichTorso, 1), (QuadavFetichArms, 1),
                (QuadavFetichLegs, 1), (LizardEgg, 1), (AspirKnife, 1), (HareMeat, 1));
        Login();
        Check("in Port Bastok, a Bastokan of rank 1 with an empty log", $"{PortBastok}/1/65535,0/1,0",
              $"{c.Zone}/{Lua("report(player:getNation())")}/{Mission(LogBastok)}/{Rank()}");
        string BVar(int mission, string name) => Var($"Mission[{LogBastok}][{mission}]{name}");
        void ToArgus() => Warp(130.6f, 7.5f, -2.2f, PortBastok);
        void ToMetalworks() => Warp(-12.0f, -12.0f, 1.0f, Metalworks);

        Console.WriteLine("Bastok 1-1 The Zeruhn Report");
        TalkTo("Argus");
        Check("Argus, 1-1 never done: the first offer, event 1000", $"032:{NpcId("Argus")}:1000", Ev());
        EventEnd(1000, 0);
        Check("  option 0: The Zeruhn Report current, YOU_ACCEPT_THE_MISSION", $"0,0/{Text(PortBastok, "YOU_ACCEPT_THE_MISSION")}:0", $"{Mission(LogBastok)}/{Special()}");
        TalkTo("Argus");
        Check("Argus on the mission: ORIGINAL_MISSION_OFFSET (no event)", "none", Cur());
        Warp(-58.0f, 8.0f, -331.0f, ZeruhnMines);
        int zeruhnReport = Ki("ZERUHN_REPORT");
        TalkTo("Makarim");
        Cs("Makarim in Zeruhn Mines: event 121", 121);
        Check("  the Zeruhn Report", "10", DbKeyItem(zeruhnReport));
        TalkTo("Makarim");
        Check("Makarim with the report: MAKARIM_DIALOG_I (no event)", "none", Cur());
        ToMetalworks();
        TalkTo("Naji");
        Check("Naji with the report: event 710 (param 1: the key item not seen yet)", $"034:{NpcId("Naji")}:710:1", Ev());
        EventEnd(710);
        Check("  The Zeruhn Report complete, the report taken", "65535,0/0/True", $"{Mission(LogBastok)}/{DbKeyItem(zeruhnReport).Substring(0, 1)}/{DbMissionComplete(LogBastok, 0)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 1-2 A Geological Survey");
        ToArgus();
        TalkTo("Argus");
        Check("Argus: the mission list 1001 (1-2 offered; 1-1 not on the list)", $"034:{NpcId("Argus")}:1001:{int.MaxValue - 2}", Ev());
        EventEnd(1001, 1);
        Check("  option 1: A Geological Survey current", "1,0", Mission(LogBastok));
        ToMetalworks();
        int blueTester = Ki("BLUE_ACIDITY_TESTER"), redTester = Ki("RED_ACIDITY_TESTER");
        TalkTo("Cid");
        Cs("Cid: event 503", 503);
        Check("  the Blue Acidity Tester", "10", DbKeyItem(blueTester));
        TalkTo("Cid");
        Cs("Cid with the blue tester: event 502", 502);
        Warp(-130.0f, 3.0f, 133.0f, DangrufWadi);
        Walk(-133.1f, 3.0f, 133.2f);
        var geyser = Of(0x039).LastOrDefault();
        Check("the I-8 geyser (trigger area 1): SendEntityVisualPacket, the geyser's 0x039 'kkj2'",
              $"{Lua("report(zones[xi.zone.DANGRUF_WADI].npc.GEYSER_OFFSET)")}/kkj2",
              geyser == null ? "none" : $"{BitConverter.ToUInt32(geyser.Buffer, 4)}/{System.Text.Encoding.ASCII.GetString(geyser.Buffer, 12, 4)}");
        Cs("  the zone's event 10", 10);
        Check("  the mission's onEventFinish: the Red Acidity Tester for the blue", "0/10", $"{DbKeyItem(blueTester).Substring(0, 1)}/{DbKeyItem(redTester)}");
        ToMetalworks();
        TalkTo("Cid");
        Cs("Cid with the red tester: event 504", 504);
        Check("  A Geological Survey complete, the tester taken", "65535,0/0/True", $"{Mission(LogBastok)}/{DbKeyItem(redTester).Substring(0, 1)}/{DbMissionComplete(LogBastok, 1)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 1-3 Fetichism");
        ToArgus();
        Accept("Argus", 1001, 2, "Fetichism");
        Check("  Fetichism current", "2,0", Mission(LogBastok));
        TradeTo("Argus", (Slot(QuadavFetichHead), 1), (Slot(QuadavFetichTorso), 1), (Slot(QuadavFetichArms), 1), (Slot(QuadavFetichLegs), 1));
        Cs("the four fetich pieces to Argus: event 1008", 1008);
        Check("  Fetichism complete: rank 2 (the 200 points set before the rank-up clears them), 1000 gil, the pieces taken", "65535,0/2,0/11000/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{Qty(QuadavFetichHead)}/{DbMissionComplete(LogBastok, 2)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 2-1 The Crystal Line");
        Donate("Flying_Axe_IM", 3);
        Check("three crystals to Flying Axe I.M. at rank 2 for 2-1's 671.5 points", "2,999", Rank());
        TalkTo("Argus");
        Check("Argus: the list with 2-1 offered, 1-3 repeatable", $"034:{NpcId("Argus")}:1001:{int.MaxValue - 4 - 8},0,0,0,0,4", Ev());
        EventEnd(1001, 3);
        Check("  The Crystal Line current", "3,0", Mission(LogBastok));
        ToMetalworks();
        TalkTo("Cid");
        Cs("Cid: event 505", 505);
        string crystalOf() => DbRow(db, "itemId", "FROM char_inventory WHERE charid = ? AND location = 0 AND itemId BETWEEN 4096 AND 4103 LIMIT 1");
        string given = crystalOf();
        Check("  option 0: one of the eight crystals, Status 1", "True/3,1", $"{given != "-"}/{Mission(LogBastok)}");
        Warp(222.0f, 19.0f, 298.0f, Konschtat);
        TradeTo("Telepoint", (Slot(ushort.Parse(given)), 1));
        Check("the crystal to Konschtat's Telepoint: a Faded Crystal for it", "1/-", $"{Qty(FadedCrystal)}/{crystalOf()}");
        ToMetalworks();
        int clReport = Ki("C_L_REPORT");
        TradeTo("Cid", (Slot(FadedCrystal), 1));
        Cs("the Faded Crystal to Cid: event 506", 506);
        Check("  the C.L. Report, the crystal taken", "10/0", $"{DbKeyItem(clReport)}/{Qty(FadedCrystal)}");
        TalkTo("Ayame");
        Cs("Ayame with the report: event 712", 712);
        Check("  The Crystal Line complete: 200 rank points, the report taken", "65535,0/2,1199/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{DbKeyItem(clReport).Substring(0, 1)}/{DbMissionComplete(LogBastok, 3)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 2-2 Wading Beasts");
        ToArgus();
        Donate("Flying_Axe_IM", 1);
        Accept("Argus", 1001, 4, "Wading Beasts");
        Check("  Wading Beasts current", "4,0", Mission(LogBastok));
        ToMetalworks();
        TradeTo("Alois", (Slot(LizardEgg), 1));
        Cs("a Lizard Egg to Alois: event 372", 372);
        Check("  Wading Beasts complete: 333 + 250 rank points, the egg taken", "65535,0/2,1782/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{Qty(LizardEgg)}/{DbMissionComplete(LogBastok, 4)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 2-3 The Emissary (Windurst first, then San d'Oria)");
        ToArgus();
        Donate("Flying_Axe_IM", 5);
        Check("five crystals for 2-3's 3204 points", "2,3447", Rank());
        Accept("Argus", 1001, 5, "The Emissary");
        ToMetalworks();
        int letterB = Ki("LETTER_TO_THE_CONSULS_BASTOK"), swordOffering = Ki("SWORD_OFFERING"), dullSword = Ki("DULL_SWORD");
        TalkTo("Naji");
        Check("Naji: event 713 (the first 2-3 of the three nations: param 8 = 1)", $"034:{NpcId("Naji")}:713:0,0,0,0,0,0,0,1", Ev());
        EventEnd(713);
        Check("  the Letter to the Consuls, Status 1", "10/5,1", $"{DbKeyItem(letterB)}/{Mission(LogBastok)}");
        Warp(-80.6f, -5.5f, 155.3f, PortWindurst);
        TalkTo("Melek");
        Cs("Melek in Port Windurst: event 48", 48);
        Check("  The Emissary (Windurst) current, Status 2", "7,2", Mission(LogBastok));
        WarpIn(2.0f, 0.1f, 28.0f, HeavensTower);
        Check("in Heaven's Tower at Status 2: the zone-in cutscene 42", $"{HeavensTower}/42", $"{c.Zone}/{Cur()}");
        EventEnd(42, 0, 1);
        Check("  an update: the mission's onEventUpdate answers (0x05C), still in 42", "True/42", $"{Of(0x05C).Count > 0}/{Cur()}");
        EventEnd(42);
        Check("  42 finished: Status 3", "7,3", Mission(LogBastok));
        TalkTo("Kupipi");
        Cs("Kupipi: event 239", 239);
        Check("  the Sword Offering, Status 4", "10/7,4", $"{DbKeyItem(swordOffering)}/{Mission(LogBastok)}");
        Warp(-80.6f, -5.5f, 155.3f, PortWindurst);
        TalkTo("Gold_Skull");
        Cs("Gold Skull with the offering: event 53", 53);
        Check("  the Dull Sword for the offering", "10/0", $"{DbKeyItem(dullSword)}/{DbKeyItem(swordOffering).Substring(0, 1)}");
        Warp(-179.0f, 16.0f, 153.0f, Giddeus);
        TalkTo("Uu_Zhoumo");
        Cs("Uu Zhoumo in Giddeus at Status 4: event 40", 40);
        TradeTo("Uu_Zhoumo", (Slot(AspirKnife), 1));
        Cs("an Aspir Knife to Uu Zhoumo: event 41", 41);
        Check("  the knife and the Dull Sword taken, Status 6", "0/0/7,6", $"{Qty(AspirKnife)}/{DbKeyItem(dullSword).Substring(0, 1)}/{Mission(LogBastok)}");
        Warp(-80.6f, -5.5f, 155.3f, PortWindurst);
        TalkTo("Melek");
        Cs("Melek at Status 6: event 55", 55);
        Check("  The Emissary (Windurst) complete, The Emissary again, Status 7", "5,7/True", $"{Mission(LogBastok)}/{DbMissionComplete(LogBastok, 7)}");
        Warp(49.0f, -2.0f, -10.0f, NorthernSandoria);
        TalkTo("Helaku");
        Check("Helaku at Status 7: event 537", "537", Cur());
        EventEnd(537, 0);
        Check("  option 0: The Emissary (San d'Oria II) current, Status 8, the letter taken", "8,8/0", $"{Mission(LogBastok)}/{DbKeyItem(letterB).Substring(0, 1)}");
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Cs("Halver: event 503", 503);
        Check("  Status 9", "8,9", Mission(LogBastok));
        Warp(-509.0f, 158.0f, -211.0f, HorlaisPeak);
        int crestB = Ki("KINDRED_CREST"), reportB = Ki("KINDRED_REPORT");
        WinBattlefield(NpcId("BC_Entrance"), "Horlais Peak (Rank 2 mission)");
        Check("  the Kindred Crest, Status 10", "10/8,10", $"{DbKeyItem(crestB)}/{Mission(LogBastok)}");
        Warp(49.0f, -2.0f, -10.0f, NorthernSandoria);
        TalkTo("Helaku");
        Cs("Helaku with the crest: event 545", 545);
        Check("  The Emissary (San d'Oria II) complete, The Emissary again, Status 11, the Kindred Report for the crest", "5,11/True/10/0",
              $"{Mission(LogBastok)}/{DbMissionComplete(LogBastok, 8)}/{DbKeyItem(reportB)}/{DbKeyItem(crestB).Substring(0, 1)}");
        TalkTo("Helaku");
        Cs("Helaku, both paths done with San d'Oria last: event 557", 557);
        ToMetalworks();
        TalkTo("Naji");
        Cs("Naji with the report: event 714", 714);
        Check("  The Emissary complete: rank 3, 3000 gil, the Adventurer's Certificate, Certified Adventurer", "65535,0/3,0/14000/10/True/True",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbKeyItem(Ki("ADVENTURERS_CERTIFICATE"))}/{DbTitle(TitleId("CERTIFIED_ADVENTURER"))}/{DbMissionComplete(LogBastok, 5)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 3-1 The Four Musketeers");
        ToArgus();
        Donate("Flying_Axe_IM", 6);
        Check("six crystals at rank 3 for 3-1's 855.5 points", "3,996", Rank());
        Accept("Argus", 1001, 10, "The Four Musketeers");
        ToMetalworks();
        TalkTo("Iron_Eater");
        Check("Iron Eater: event 715", "715", Cur());
        EventEnd(715, 0);
        Check("  Status 1", "10,1", Mission(LogBastok));
        WarpIn(-297.0f, 1.0f, 96.0f, Beadeaux);
        Check("in Beadeaux at Status 1: the zone-in cutscene 120", $"{Beadeaux}/120", $"{c.Zone}/{Cur()}");
        EventEnd(120);
        Check("  Status 2", "10,2", Mission(LogBastok));
        // the Copper Quadav: each kill counts (Status 2 to 22); a quadav killed again once it respawns
        var coppers = zone.ZoneInfoOf(c.Zone).Mobs.Where(m => m.Name == "Copper_Quadav").ToList();
        Check("  Beadeaux's Copper Quadav", "True", coppers.Count > 0);
        int kills = 0;
        for (int guard = 0; Mission(LogBastok) != "10,22" && guard < 200; guard++)
        {
            var m = coppers.FirstOrDefault(q => q.IsSpawned && q.IsAlive);
            if (m == null)
            {
                Lua($"SpawnMob({coppers[guard % coppers.Count].Id})");
                continue;
            }
            KillByChar(m.Id);
            kills++;
        }
        Check("  each claimed kill counted: twenty kills", "20", kills);
        Check("  twenty Copper Quadav killed: Status 22", "10,22", Mission(LogBastok));
        WarpIn(-297.0f, 1.0f, 96.0f, Beadeaux);
        Check("Beadeaux at Status 22: no cutscene", "none", Cur());
        TakeZoneLine(LineTo(Pashhow), false);
        Check("into Pashhow Marshlands from Beadeaux at Status 22: the zone-in cutscene 11", $"{Pashhow}/{Beadeaux}/11", $"{c.Zone}/{c.PrevZone}/{Cur()}");
        EventEnd(11);
        Check("  The Four Musketeers complete: 350 rank points; setPos to (578, 25, -376)", "65535,0/3,1346/True/578/-376",
              $"{Mission(LogBastok)}/{Rank()}/{DbMissionComplete(LogBastok, 10)}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 3-2 To the Forsaken Mines");
        ToArgus();
        Donate("Flying_Axe_IM", 5);
        Check("five crystals for 3-2's 2166 points", "3,2176", Rank());
        Accept("Argus", 1001, 11, "To the Forsaken Mines");
        Warp(83.0f, 0.0f, 28.0f, BastokMines);
        TalkTo("Davyad");
        Cs("Davyad in Bastok Mines: event 54", 54);
        Warp(206.0f, -60.0f, -99.0f, GusgenMines);
        uint moby = MobOf("GUSGEN_MINES", "BLIND_MOBY");
        TradeTo("qm2", (Slot(HareMeat), 1));
        Check("a slice of Hare Meat to Gusgen's ???: Blind Moby popped (popFromQM), the meat taken", "none/True/0",
              $"{Cur()}/{zone.ZoneInfoOf(c.Zone).MobById[moby].IsSpawned}/{Qty(HareMeat)}");
        KillByChar(moby);
        Check("  Blind Moby killed: its template's sure drop, the Glocolite", "1", Qty(Glocolite));
        ToArgus();
        TradeTo("Argus", (Slot(Glocolite), 1));
        Cs("the Glocolite to Argus: event 1010", 1010);
        Check("  To the Forsaken Mines complete: 400 rank points", "65535,0/3,2576/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{Qty(Glocolite)}/{DbMissionComplete(LogBastok, 11)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 3-3 Jeuno");
        Donate("Flying_Axe_IM", 6);
        Check("six crystals for 3-3's 3477 points", "3,3572", Rank());
        Accept("Argus", 1001, 12, "Jeuno");
        Warp(59.9f, -17.4f, -40.3f, Metalworks);
        TalkTo("Lucius");
        Cs("Lucius: event 322", 322);
        Check("  Status 1, the Letter to the Ambassador", "12,1/10", $"{Mission(LogBastok)}/{DbKeyItem(Ki("LETTER_TO_THE_AMBASSADOR"))}");
        Warp(3.0f, 9.0f, -74.0f, Rulude);
        TalkTo("Goggehn");
        Cs("Goggehn: event 41", 41);
        Warp(594.0f, 16.0f, -19.0f, LowerDelkfutt);
        Lua($"player:addItem({DelkfuttKey}, 1)");
        TradeTo("_542", (Slot(DelkfuttKey), 1));
        Cs("the Delkfutt Key to the Cermet Door (_542): cutscene 1", 1);
        Check("  the key item for the key, Status 3", "10/0/12,3", $"{DbKeyItem(Ki("DELKFUTT_KEY"))}/{Qty(DelkfuttKey)}/{Mission(LogBastok)}");
        Warp(18.0f, 8.0f, -72.0f, Rulude);
        TalkTo("_6r2");
        int gilJeuno = int.Parse(Gil());
        Cs("the embassy door (_6r2): event 38", 38);
        Check("  Jeuno complete: rank 4, 5000 gil", $"65535,0/4,0/{gilJeuno + 5000}/True", $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbMissionComplete(LogBastok, 12)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the fewest fire crystals that meet a mission's points (xi.mission.getMissionRankPoints), found with the
        // points set and put back, then donated: floor(4000 / (rank * 12 - 12)) each
        void DonateFor(string overseer, int missionId, string what)
        {
            int rank = int.Parse(Lua("report(player:getRank(player:getNation()))"));
            int before = int.Parse(Lua("report(player:getRankPoints())"));
            int per = rank > 1 ? 4000 / (rank * 12 - 12) : 0;
            int n = int.Parse(Lua($"local p = player:getRankPoints() local n = 0 " +
                                  $"while n < 100 and not xi.mission.getMissionRankPoints(player, {missionId}) do n = n + 1 player:setRankPoints(math.min(4000, p + n * {per})) end " +
                                  "player:setRankPoints(p) report(n)"));
            if (n > 0) Donate(overseer, n);
            Check($"{n} crystals for {what}'s points: {before} + {n} * {per}", $"{rank},{Math.Min(4000, before + n * per)}/true",
                  $"{Rank()}/{Lua($"report(tostring(xi.mission.getMissionRankPoints(player, {missionId})))")}");
        }

        Console.WriteLine("Bastok 4-1 Magicite");
        Warp(3.0f, 9.0f, -76.0f, Rulude);
        TalkTo("Goggehn");
        Cs("Goggehn before the points: event 4", 4);
        ToArgus();
        DonateFor("Flying_Axe_IM", 13, "Magicite");
        Warp(3.0f, 9.0f, -76.0f, Rulude);
        TalkTo("Goggehn");
        Cs("Goggehn, the points met: event 0", 0);
        int bPermit = Ki("ARCHDUCAL_AUDIENCE_PERMIT"), bAldoLetter = Ki("LETTER_TO_ALDO"), bBell = Ki("SILVER_BELL"), bTorch = Ki("YAGUDO_TORCH");
        TalkTo("_6r2");
        Check("the embassy door: event 129 (no permit: 0)", $"034:{NpcId("_6r2")}:129", Ev());
        EventEnd(129);
        Check("  Magicite current, the Archducal Audience Permit", "13,0/10", $"{Mission(LogBastok)}/{DbKeyItem(bPermit)}");
        // a plain event (Event priority) leaves the next talk to the NPC's own script (InteractionLookup's UseFallback
        // alternation), the one after back to the handler's
        TalkTo("Goggehn");
        Cs("Goggehn after his event 0: his own script's event 101 (the fallback's turn)", 101);
        TalkTo("Goggehn");
        Cs("  again, at Status 0: event 132", 132);
        Warp(0.0f, -5.0f, 64.0f, Rulude);
        TalkTo("_6r9");
        Cs("the audience chamber (_6r9): event 128", 128);
        Check("  the Letter to Aldo, Status 1", "10/13,1", $"{DbKeyItem(bAldoLetter)}/{Mission(LogBastok)}");
        Warp(20.0f, 3.0f, -56.0f, LowerJeuno);
        TalkTo("Aldo");
        Check("Aldo: event 152 (no Silver Bell)", $"034:{NpcId("Aldo")}:152:0,1", Ev());
        EventEnd(152);
        Check("  the Silver Bell for the letter, Status 2", "10/0/13,2", $"{DbKeyItem(bBell)}/{DbKeyItem(bAldoLetter).Substring(0, 1)}/{Mission(LogBastok)}");
        Warp(9.0f, 1.0f, 68.0f, UpperJeuno);
        TalkTo("Paya-Sabya");
        Cs("Paya-Sabya: event 80", 80);
        Check("  Option 1", "1", BVar(13, "Option"));
        Warp(-26.8f, 4.6f, -135.0f, LowerJeuno);
        TalkTo("Muckvix");
        Cs("Muckvix: event 184", 184);
        Check("  the Yagudo Torch, Option 2", "10/2", $"{DbKeyItem(bTorch)}/{BVar(13, "Option")}");
        WarpIn(-344.0f, 25.0f, 45.0f, AltarRoom);
        Check("in the Altar Room: the Fickblix zone-in cutscene 10000", $"{AltarRoom}/10000", $"{c.Zone}/{Cur()}");
        EventEnd(10000);
        Check("  Option cleared", "-", BVar(13, "Option"));
        int bOrastone = Ki("MAGICITE_ORASTONE"), bOptistone = Ki("MAGICITE_OPTISTONE"), bAurastone = Ki("MAGICITE_AURASTONE");
        TalkTo("Magicite");
        Check("the Altar Room's Magicite: event 44 with the zone", $"034:{NpcId("Magicite")}:44:{AltarRoom}", Ev());
        EventEnd(44);
        Warp(-160.0f, -8.0f, 10.0f, MonasticCavern);
        TalkTo("Magicite");
        Check("the Monastic Cavern's Magicite: event 0 with the zone", $"034:{NpcId("Magicite")}:0:{MonasticCavern}", Ev());
        EventEnd(0);
        Warp(11.0f, 25.0f, -79.0f, QulunDome);
        TalkTo("Magicite");
        Check("Qulun Dome's Magicite, the last one: event 0 with the Lion's part (1, 46, 47)", $"034:{NpcId("Magicite")}:0:1,46,47", Ev());
        EventEnd(0);
        Check("  the three stones", "10/10/10", $"{DbKeyItem(bOrastone)}/{DbKeyItem(bOptistone)}/{DbKeyItem(bAurastone)}");
        Warp(0.0f, -5.0f, 64.0f, Rulude);
        int bAirship = Ki("AIRSHIP_PASS"), bWings = TitleId("HAVE_WINGS_WILL_FLY"), bMessage = Ki("MESSAGE_TO_JEUNO_BASTOK");
        TalkTo("_6r9");
        Check("the audience chamber with the three stones: event 60 (no Airship Pass)", $"034:{NpcId("_6r9")}:60", Ev());
        EventEnd(60);
        Check("  the stones taken, the Airship Pass, Have Wings, Will Fly, Status 3", "0/0/0/10/True/13,3",
              $"{DbKeyItem(bOrastone).Substring(0, 1)}/{DbKeyItem(bOptistone).Substring(0, 1)}/{DbKeyItem(bAurastone).Substring(0, 1)}/" +
              $"{DbKeyItem(bAirship)}/{DbTitle(bWings)}/{Mission(LogBastok)}");
        Warp(18.0f, 8.0f, -72.0f, Rulude);
        TalkTo("_6r2");
        Check("the embassy door at Status 3: THE_CONSULATE_IS_AWAY (no event)", "none", Cur());
        Warp(3.0f, 9.0f, -76.0f, Rulude);
        int gilB = int.Parse(Gil());
        TalkTo("Goggehn");
        Cs("Goggehn at Status 3: event 35", 35);
        Check("  Magicite complete: rank 5, 10000 gil, the Message to Jeuno", $"65535,0/5,0/{gilB + 10000}/10/True",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbKeyItem(bMessage)}/{DbMissionComplete(LogBastok, 13)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 5-1 Darkness Rising");
        ToMetalworks();
        int bNewSeal = Ki("NEW_FEIYIN_SEAL"), bBurntSeal = Ki("BURNT_SEAL");
        TalkTo("Naji");
        Cs("Naji at rank 5: event 720, accepted (option 0)", 720);
        Check("  Darkness Rising current, the message taken, the New Fei'Yin Seal, Status 10", "14,10/0/10",
              $"{Mission(LogBastok)}/{DbKeyItem(bMessage).Substring(0, 1)}/{DbKeyItem(bNewSeal)}");
        WarpIn(243.0f, -24.0f, 62.0f, FeiYin);
        Check("in Fei'Yin: the zone-in cutscene 1", $"{FeiYin}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Check("  Status 11", "14,11", Mission(LogBastok));
        Warp(-221.0f, -24.0f, 19.0f, QubiaArena);
        WinBattlefield(NpcId("BC_Entrance"), "Qu'Bia Arena (Rank 5 mission)");
        Check("  the Burnt Seal for the new seal, Status 12", "10/0/14,12",
              $"{DbKeyItem(bBurntSeal)}/{DbKeyItem(bNewSeal).Substring(0, 1)}/{Mission(LogBastok)}");
        ToMetalworks();
        TalkTo("Naji");
        Cs("Naji with the Burnt Seal: event 722", 722);
        Check("  Darkness Rising complete: 600 rank points, the seal taken", "65535,0/5,600/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{DbKeyItem(bBurntSeal).Substring(0, 1)}/{DbMissionComplete(LogBastok, 14)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 5-2 Xarcabard, Land of Truths");
        ToArgus();
        DonateFor("Flying_Axe_IM", 15, "Xarcabard, Land of Truths");
        Accept("Argus", 1001, 15, "Xarcabard, Land of Truths");
        Check("  Xarcabard, Land of Truths current", "15,0", Mission(LogBastok));
        Warp(106.0f, -21.0f, 0.0f, Metalworks);
        TalkTo("Karst");
        Cs("Karst: event 602", 602);
        Check("  Status 2", "15,2", Mission(LogBastok));
        Warp(-109.0f, -6.0f, 0.1f, ThroneRoom);
        TalkTo("_4l1");
        Cs("the Throne Room's door (_4l1) at Status 2: the mission's event 6", 6);
        Check("  Status 3", "15,3", Mission(LogBastok));
        FightShadowLord(LogBastok);
        Rezone();
        Check("  the zone's onEventFinish for 7: back to Castle Zvahl Baileys' entrance", $"{CastleZvahlBaileys}/380/-20", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Warp(92.0f, -19.0f, 0.1f, Metalworks);
        gilB = int.Parse(Gil());
        TalkTo("_6ld");
        Cs("the President's office (_6ld) with the fragment: event 603", 603);
        Check("  Xarcabard, Land of Truths complete: rank 6, 20000 gil, the fragment taken", $"65535,0/6,0/{gilB + 20000}/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbKeyItem(Ki("SHADOW_FRAGMENT")).Substring(0, 1)}/{DbMissionComplete(LogBastok, 15)}");
        Warp(16.0f, 7.0f, -14.0f, BastokMines);
        TalkTo("Pavvke");
        Cs("Pavvke after 5-2: event 76", 76);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 6-1 Return of the Talekeeper");
        ToArgus();
        DonateFor("Flying_Axe_IM", 16, "Return of the Talekeeper");
        Accept("Argus", 1001, 16, "Return of the Talekeeper");
        Warp(-38.0f, 0.0f, 40.0f, BastokMines);
        TalkTo("Medicine_Eagle");
        Cs("Medicine Eagle: event 180", 180);
        Check("  Status 1", "16,1", Mission(LogBastok));
        Warp(-77.0f, 0.0f, 57.0f, ZeruhnMines);
        TalkTo("Drake_Fang");
        Cs("Drake Fang: event 200", 200);
        Check("  Status 2", "16,2", Mission(LogBastok));
        Warp(-328.0f, 0.0f, -110.0f, WesternAltepa);
        uint eSphinx = MobOf("WESTERN_ALTEPA_DESERT", "EASTERN_SPHINX"), wSphinx = MobOf("WESTERN_ALTEPA_DESERT", "WESTERN_SPHINX");
        var waInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("qm2");
        Check("the ???: the two sphinxes spawned, claimed by the char (updateClaim)", $"none/True/True/{c.Id}/{c.Id}",
              $"{Cur()}/{waInfo.MobById[eSphinx].IsSpawned}/{waInfo.MobById[wSphinx].IsSpawned}/{waInfo.MobById[eSphinx].OwnerId}/{waInfo.MobById[wSphinx].OwnerId}");
        KillByChar(eSphinx);
        Check("  the Eastern Sphinx killed, the western one up: no nmDefeated", "0", Lua("report(player:getLocalVar('Mission[1][16]nmDefeated'))"));
        KillByChar(wSphinx);
        Check("  both killed: nmDefeated", "1", Lua("report(player:getLocalVar('Mission[1][16]nmDefeated'))"));
        int moonpebble = Ki("ALTEPA_MOONPEBBLE");
        TalkTo("qm2");
        Check("the ??? again: the Altepa Moonpebble, Status 3", "10/16,3", $"{DbKeyItem(moonpebble)}/{Mission(LogBastok)}");
        Warp(71.0f, 7.0f, -11.0f, BastokMines);
        int rpB = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Tall_Mountain");
        Cs("Tall Mountain: event 182", 182);
        Check("  Return of the Talekeeper complete: 650 rank points, the moonpebble taken", $"65535,0/6,{Math.Min(4000, rpB + 650)}/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{DbKeyItem(moonpebble).Substring(0, 1)}/{DbMissionComplete(LogBastok, 16)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 6-2 The Pirate's Cove");
        ToArgus();
        DonateFor("Flying_Axe_IM", 17, "The Pirate's Cove");
        Accept("Argus", 1001, 17, "The Pirate's Cove");
        ToMetalworks();
        TalkTo("Naji");
        Cs("Naji: event 761", 761);
        Warp(122.0f, -8.0f, -12.0f, Norg);
        TalkTo("Gilgamesh");
        Cs("Gilgamesh in Norg: event 98", 98);
        Check("  Status 2", "17,2", Mission(LogBastok));
        Warp(172.0f, 0.0f, -25.0f, IfritsCauldron);
        uint salamander = MobOf("IFRITS_CAULDRON", "PIRATES_COVE_NMS");
        var icInfo = zone.ZoneInfoOf(c.Zone);
        Lua($"player:addItem({AdamanOre}, 1)");
        TradeTo("qm4", (Slot(AdamanOre), 1));
        Check("Adaman Ore to the ???: Salamander and Magma popped (popFromQM), Magma claimed, the ore taken", $"True/True/{c.Id}/0",
              $"{icInfo.MobById[salamander].IsSpawned}/{icInfo.MobById[salamander + 1].IsSpawned}/{icInfo.MobById[salamander + 1].OwnerId}/{Qty(AdamanOre)}");
        KillByChar(salamander + 1);
        KillByChar(salamander);
        Check("  Magma killed: its template's sure Frag Rock", "True", int.Parse(Qty(FragRock)) >= 1);
        Warp(122.0f, -8.0f, -12.0f, Norg);
        TradeTo("Gilgamesh", (Slot(FragRock), 1));
        Cs("the Frag Rock to Gilgamesh: event 99", 99);
        Check("  Status 3", "17,3", Mission(LogBastok));
        ToMetalworks();
        gilB = int.Parse(Gil());
        TalkTo("Naji");
        Cs("Naji at Status 3: event 762", 762);
        Check("  The Pirate's Cove complete: rank 7, 40000 gil", $"65535,0/7,0/{gilB + 40000}/True",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbMissionComplete(LogBastok, 17)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 7-1 The Final Image");
        ToArgus();
        DonateFor("Flying_Axe_IM", 18, "The Final Image");
        Accept("Argus", 1001, 18, "The Final Image");
        Warp(-12.0f, -11.0f, 2.0f, Metalworks);
        TalkTo("Cid");
        Cs("Cid: event 763", 763);
        Check("  Status 1", "18,1", Mission(LogBastok));
        Warp(-105.0f, -4.0f, -115.0f, RoMaeve);
        uint mokkurkalfi = MobOf("ROMAEVE", "MOKKURKALFI");
        var rmInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("qm2");
        Check("Ro'Maeve's ???: the two Mokkurkalfi popped (popFromQM, unclaimed)", "none/True/True/0",
              $"{Cur()}/{rmInfo.MobById[mokkurkalfi].IsSpawned}/{rmInfo.MobById[mokkurkalfi + 1].IsSpawned}/{rmInfo.MobById[mokkurkalfi].OwnerId}");
        KillByChar(mokkurkalfi);
        KillByChar(mokkurkalfi + 1);
        Check("  both killed: nmDefeated", "1", Lua("report(player:getLocalVar('Mission[1][18]nmDefeated'))"));
        // popFromQM hid the ??? (DISAPPEAR) until both have despawned (the death fade), plus its 300 s
        for (int i = 0; i < 30 && (rmInfo.MobById[mokkurkalfi].IsSpawned || rmInfo.MobById[mokkurkalfi + 1].IsSpawned); i++) Tick(1000);
        Check("  the ??? hidden while they fade out", "False/False/False",
              $"{rmInfo.MobById[mokkurkalfi].IsSpawned}/{rmInfo.MobById[mokkurkalfi + 1].IsSpawned}/{Npc(NpcId("qm2")).Status == NpcEntity.StatusNormal}");
        Tick(300_000);
        Tick(1000);
        Check("  back 300 s after the despawns", "True", Npc(NpcId("qm2")).Status == NpcEntity.StatusNormal);
        int cermet = Ki("REINFORCED_CERMET");
        TalkTo("qm2");
        Check("the ??? again: the Reinforced Cermet, Status 2", "10/18,2", $"{DbKeyItem(cermet)}/{Mission(LogBastok)}");
        Warp(-12.0f, -11.0f, 2.0f, Metalworks);
        rpB = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Cid");
        Cs("Cid at Status 2: event 764", 764);
        Check("  The Final Image complete: 700 rank points, the cermet taken", $"65535,0/7,{Math.Min(4000, rpB + 700)}/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{DbKeyItem(cermet).Substring(0, 1)}/{DbMissionComplete(LogBastok, 18)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 7-2 On My Way");
        ToArgus();
        DonateFor("Flying_Axe_IM", 19, "On My Way");
        Accept("Argus", 1001, 19, "On My Way");
        Warp(106.0f, -21.0f, 0.0f, Metalworks);
        TalkTo("Karst");
        Cs("Karst: event 765", 765);
        Warp(-159.0f, -10.5f, 16.0f, PortBastok);
        TalkTo("Hilda");
        Cs("Hilda: event 255", 255);
        Check("  Status 2", "19,2", Mission(LogBastok));
        Warp(-339.0f, 104.0f, -258.0f, WaughroonShrine);
        int werei = Ki("LETTER_FROM_WEREI");
        WinBattlefield(NpcId("BC_Entrance"), "Waughroon Shrine (On My Way)");
        Check("  the Letter from Werei, Status 3, Option 1", "10/19,3/1", $"{DbKeyItem(werei)}/{Mission(LogBastok)}/{BVar(19, "Option")}");
        Warp(106.0f, -21.0f, 0.0f, Metalworks);
        gilB = int.Parse(Gil());
        TalkTo("Karst");
        Check("Karst at Status 3: event 766 with the letter still to deliver (1)", $"034:{NpcId("Karst")}:766:1", Ev());
        EventEnd(766);
        Check("  On My Way complete: rank 8, 60000 gil, Stage 1, Option kept 1", $"65535,0/8,0/{gilB + 60000}/True/1/1",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbMissionComplete(LogBastok, 19)}/{BVar(19, "Stage")}/{BVar(19, "Option")}");
        ToArgus();
        TalkTo("Argus");
        Cs("Argus with the letter undelivered: event 1011 (no mission list)", 1011);
        Warp(53.0f, 0.0f, -37.0f, BastokMines);
        TalkTo("Gumbah");
        Cs("Gumbah: event 175", 175);
        Check("  GumbahDialog 1", "1", BVar(19, "GumbahDialog"));
        TalkTo("Gumbah");
        Cs("Gumbah again: event 177", 177);
        Check("  the letter delivered: Option cleared, the letter taken", "-/0", $"{BVar(19, "Option")}/{DbKeyItem(werei).Substring(0, 1)}");
        Warp(112.0f, -21.0f, -9.0f, Metalworks);
        TalkTo("_6lg");
        Cs("Cid's workshop door (_6lg) at Stage 1, the three quests not done: event 621", 621);
        Check("  Stage cleared", "-", BVar(19, "Stage"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 8-1 The Chains That Bind Us");
        ToArgus();
        DonateFor("Flying_Axe_IM", 20, "The Chains That Bind Us");
        Accept("Argus", 1001, 20, "The Chains That Bind Us");
        Warp(92.0f, -19.5f, 3.0f, Metalworks);
        TalkTo("Iron_Eater");
        Cs("Iron Eater: event 767 (option 0)", 767);
        Check("  Status 1", "20,1", Mission(LogBastok));
        Warp(-469.0f, -1.0f, 622.0f, QuicksandCaves);
        uint centurio = MobOf("QUICKSAND_CAVES", "CENTURIO_IV_VII"), triarius = MobOf("QUICKSAND_CAVES", "TRIARIUS_IV_XIV"),
             princeps = MobOf("QUICKSAND_CAVES", "PRINCEPS_IV_XLV");
        var qsInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("qm6");
        Check("the ???: the three antica spawned, claimed by the char", $"none/{c.Id}/{c.Id}/{c.Id}",
              $"{Cur()}/{qsInfo.MobById[centurio].OwnerId}/{qsInfo.MobById[triarius].OwnerId}/{qsInfo.MobById[princeps].OwnerId}");
        KillByChar(centurio);
        KillByChar(triarius);
        Check("  two of three: Status 1", "20,1", Mission(LogBastok));
        KillByChar(princeps);
        Check("  the third killed: Status 2", "20,2", Mission(LogBastok));
        TalkTo("qm6");
        Cs("the ??? at Status 2: event 11", 11);
        Check("  Status 3", "20,3", Mission(LogBastok));
        Warp(-533.0f, -1.0f, -412.0f, QuicksandCaves);
        TalkTo("qm4");
        Cs("the other ??? (qm4) at Status 3: event 10", 10);
        Check("  Status 4", "20,4", Mission(LogBastok));
        Warp(92.0f, -19.5f, 3.0f, Metalworks);
        rpB = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Iron_Eater");
        Cs("Iron Eater at Status 4: event 768", 768);
        Check("  The Chains That Bind Us complete: 1133 rank points", $"65535,0/8,{Math.Min(4000, rpB + 1133)}/True",
              $"{Mission(LogBastok)}/{Rank()}/{DbMissionComplete(LogBastok, 20)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 8-2 Enter the Talekeeper");
        ToArgus();
        DonateFor("Flying_Axe_IM", 21, "Enter the Talekeeper");
        Accept("Argus", 1001, 21, "Enter the Talekeeper");
        Warp(-77.0f, 0.0f, 57.0f, ZeruhnMines);
        TalkTo("Drake_Fang");
        Cs("Drake Fang: event 202", 202);
        Check("  Status 1", "21,1", Mission(LogBastok));
        Warp(-29.0f, -22.0f, -181.0f, KuftalTunnel);
        TalkTo("qm5");
        Cs("Kuftal's ??? (qm5): event 12, option 0", 12);
        Check("  Status 2", "21,2", Mission(LogBastok));
        uint talekeeper = MobOf("KUFTAL_TUNNEL", "TALEKEEPER_OFFSET");
        var ktInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("qm6");
        Check("the other ??? (qm6) at Status 2: the three ghosts spawned", "none/3",
              $"{Cur()}/{Enumerable.Range(0, 3).Count(i => ktInfo.MobById[talekeeper + (uint)i].IsSpawned)}");
        KillByChar(talekeeper);
        KillByChar(talekeeper + 1);
        Check("  two ghosts killed: Status 2", "21,2", Mission(LogBastok));
        KillByChar(talekeeper + 2);
        Check("  the third killed: Status 3", "21,3", Mission(LogBastok));
        int wood = Ki("OLD_PIECE_OF_WOOD");
        TalkTo("qm6");
        Cs("qm6 at Status 3, no ghost up: event 13", 13);
        Check("  the Old Piece of Wood, Status 4", "10/21,4", $"{DbKeyItem(wood)}/{Mission(LogBastok)}");
        Warp(-77.0f, 0.0f, 57.0f, ZeruhnMines);
        TalkTo("Drake_Fang");
        Cs("Drake Fang at Status 4: event 204", 204);
        Tick(400);
        Check("  Status 5, setPos in Zeruhn Mines to (23, 0, 4)", $"21,5/{ZeruhnMines}/23/4", $"{Mission(LogBastok)}/{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        gilB = int.Parse(Gil());
        TakeZoneLine(LineTo(BastokMines), false);
        Check("into Bastok Mines from Zeruhn Mines at Status 5: the zone-in cutscene 176", $"{BastokMines}/176", $"{c.Zone}/{Cur()}");
        EventEnd(176);
        Check("  Enter the Talekeeper complete: rank 9, 80000 gil, the wood taken", $"65535,0/9,0/{gilB + 80000}/0/True",
              $"{Mission(LogBastok)}/{Rank()}/{Gil()}/{DbKeyItem(wood).Substring(0, 1)}/{DbMissionComplete(LogBastok, 21)}");
        Warp(16.0f, 7.0f, -14.0f, BastokMines);
        TalkTo("Pavvke");
        Cs("Pavvke after 8-2: event 76", 76);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 9-1 The Salt of the Earth");
        ToArgus();
        DonateFor("Flying_Axe_IM", 22, "The Salt of the Earth");
        Accept("Argus", 1001, 22, "The Salt of the Earth");
        Warp(97.0f, -20.0f, 12.0f, Metalworks);
        TalkTo("Alois");
        Cs("Alois: event 773", 773);
        Warp(7.0f, 8.0f, 79.0f, Rabao);
        TalkTo("Dancing_Wolf");
        Cs("Dancing Wolf in Rabao: event 102", 102);
        Check("  Status 2", "22,2", Mission(LogBastok));
        Warp(-130.0f, 1.0f, 250.0f, GustavTunnel);
        uint gigaplasm = MobOf("GUSTAV_TUNNEL", "GIGAPLASM");
        var gtInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("qm2");
        Check("Gustav's ???: the Gigaplasm spawned, claimed by the char", $"none/True/{c.Id}",
              $"{Cur()}/{gtInfo.MobById[gigaplasm].IsSpawned}/{gtInfo.MobById[gigaplasm].OwnerId}");
        // each plasm's killer splits it in two (onMobDeath isKiller): 1 Gigaplasm, 2 Macroplasms, 4 Microplasms, 8 Nanoplasms
        var plasms = Enumerable.Range(0, 15).Select(i => gtInfo.MobById[gigaplasm + (uint)i]).ToList();
        int plasmKills = 0;
        for (int guard = 0; guard < 40; guard++)
        {
            var p = plasms.FirstOrDefault(m => m.IsSpawned && m.IsAlive);
            if (p == null) break;
            KillByChar(p.Id);
            plasmKills++;
        }
        Check("  the fifteen plasms killed: the last Nanoplasm's Status 3", "15/22,3", $"{plasmKills}/{Mission(LogBastok)}");
        int miraclesalt = Ki("MIRACLESALT");
        TalkTo("qm2");
        Check("the ??? at Status 3: the Miraclesalt", "10", DbKeyItem(miraclesalt));
        Warp(7.0f, 8.0f, 79.0f, Rabao);
        TalkTo("Dancing_Wolf");
        Cs("Dancing Wolf at Status 3: event 104", 104);
        Check("  Status 4", "22,4", Mission(LogBastok));
        Warp(97.0f, -20.0f, 12.0f, Metalworks);
        rpB = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Alois");
        Cs("Alois at Status 4: event 776", 776);
        Check("  The Salt of the Earth complete: 1500 rank points, Option 1", $"65535,0/9,{Math.Min(4000, rpB + 1500)}/True/1",
              $"{Mission(LogBastok)}/{Rank()}/{DbMissionComplete(LogBastok, 22)}/{BVar(22, "Option")}");
        Warp(114.0f, -20.0f, -7.0f, Metalworks);
        TalkTo("Franziska");
        Cs("Franziska at Option 1: event 777", 777);
        TalkTo("Franziska");
        Cs("Franziska at Option 2: event 779", 779);
        Check("  Option cleared", "-", BVar(22, "Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Bastok 9-2 Where Two Paths Converge");
        ToArgus();
        DonateFor("Flying_Axe_IM", 23, "Where Two Paths Converge");
        Accept("Argus", 1001, 23, "Where Two Paths Converge");
        Warp(92.0f, -19.5f, 3.0f, Metalworks);
        TalkTo("Iron_Eater");
        Cs("Iron Eater: event 780", 780);
        Check("  Status 1", "23,1", Mission(LogBastok));
        Warp(-109.0f, -6.0f, 0.1f, ThroneRoom);
        var zeidBf = EnterBattlefield(NpcId("_4l1"), "the Throne Room (Where Two Paths Converge)");
        uint zeid = MobOf("THRONE_ROOM", "ZEID_BCNM_OFFSET");
        var trInfo = zone.ZoneInfoOf(c.Zone);
        Check("  Zeid up, unkillable", "True/true", $"{trInfo.MobById[zeid].IsSpawned}/{Lua($"report(tostring(GetMobByID({zeid}):getUnkillable()))")}");
        FightTo(zeid, 60);
        for (int i = 0; i < 10 && Cur() != "32004"; i++) Tick(400);
        Check("Zeid under 70%: the COMBAT_TICK listener's 32004 (3, 3, 1, 3, 3, 3, 3, 3), Zeid despawned", "32004/3,3,1,3,3,3,3,3",
              $"{Cur()}/{Ev().Split(':').Last()}");
        EventEnd(32004);
        Tick(1000);
        Tick(1000);
        Tick(1000);
        var zeid2 = trInfo.MobById[zeid + 1];
        Check("  32004 finished (onEventFinishBattlefield): Zeid gone, Zeid (phase 2) up at his 60%, Volker inserted, the char moved",
              "False/True/60/true/-443", $"{trInfo.MobById[zeid].IsSpawned}/{zeid2.IsSpawned}/{zeid2.Hp * 100 / zeid2.MaxHp}/" +
              $"{Lua("local t = nil for _, e in pairs(player:getBattlefield():getAllies()) do t = e end report(tostring(t ~= nil))")}/{MathF.Round(c.X)}");
        KillByChar(zeid + 1);
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("  phase 2 killed, won: 32001", "2/32001", $"{zeidBf?.Status}/{Cur()}");
        EventEnd(32001);
        Check("32001 finished: Status 2", "23,2", Mission(LogBastok));
        Rezone();
        Warp(92.0f, -19.5f, 3.0f, Metalworks);
        int titleHero = TitleId("HERO_AMONG_HEROES");
        gilB = int.Parse(Gil());
        TalkTo("Iron_Eater");
        Cs("Iron Eater at Status 2: event 782", 782);
        Check("  Where Two Paths Converge complete: rank 10, 100000 gil, Hero Among Heroes, the Bastokan Flag",
              $"65535,0/10/{gilB + 100000}/True/1/True", $"{Mission(LogBastok)}/{Lua("report(player:getRank(player:getNation()))")}/{Gil()}/" +
              $"{DbTitle(titleHero)}/{Qty(BastokanFlag)}/{DbMissionComplete(LogBastok, 23)}");

        // ---- Windurst ----
        Windurst:
        Console.WriteLine("Windurst: the login in Windurst Woods by Rakoh Buuma");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[2];
        Fixture(db, WindurstWoods, WindurstWoods, 108.0f, -5.0f, -24.0f, 2);
        Login();
        Check("in Windurst Woods, a Windurstian of rank 1 with an empty log", $"{WindurstWoods}/2/65535,0/1,0",
              $"{c.Zone}/{Lua("report(player:getNation())")}/{Mission(LogWindurst)}/{Rank()}");
        string WVar(int mission, string name) => Var($"Mission[{LogWindurst}][{mission}]{name}");
        void ToRakoh() => Warp(108.0f, -5.0f, -24.0f, WindurstWoods);
        int gilW, rpW;

        Console.WriteLine("Windurst 1-1 The Horutoto Ruins Experiment");
        TalkTo("Rakoh_Buuma");
        Cs("Rakoh Buuma, the first offer: event 121", 121, 1);
        Check("  option 1: The Horutoto Ruins Experiment current, Status 1, New Buuma's Boomers Recruit", "0,1/True",
              $"{Mission(LogWindurst)}/{DbTitle(TitleId("NEW_BUUMAS_BOOMERS_RECRUIT"))}");
        Warp(-133.7f, -4.0f, 149.0f, PortWindurst);
        TalkTo("Hakkuru-Rinkuru");
        Cs("Hakkuru-Rinkuru: event 90", 90);
        Check("  Status 2", "0,2", Mission(LogWindurst));
        Warp(420.0f, -1.66f, -28.0f, InnerHorutoto);
        TalkTo("_5c5");
        Cs("the Inner Horutoto gate (_5c5): cutscene 42", 42);
        int gizmo = int.Parse(WVar(0, "RandomGizmo"));
        Check("  Status 3, RandomGizmo rolled 1 to 6", "0,3/True", $"{Mission(LogWindurst)}/{gizmo >= 1 && gizmo <= 6}");
        string[] gizmos = { "_5cp", "_5cq", "_5cr", "_5cs", "_5ct", "_5cu" };
        int wrong = gizmo % 6 + 1;
        TalkTo(gizmos[wrong - 1]);
        Cs($"a wrong Magical Gizmo (#{wrong}): its failure cutscene {47 + 2 * wrong}", 47 + 2 * wrong);
        Check("  GizmoExamined bit set", $"{1 << wrong}", WVar(0, "GizmoExamined"));
        TalkTo(gizmos[wrong - 1]);
        Check("  that one again: EXAMINED_RECEPTACLE (no event)", "none", Cur());
        int crackedOrb = Ki("CRACKED_MANA_ORB");
        TalkTo(gizmos[gizmo - 1]);
        Cs($"the rolled Magical Gizmo (#{gizmo}): its success cutscene {46 + 2 * gizmo}", 46 + 2 * gizmo);
        Check("  the Cracked Mana Orb, Status 4, the vars cleared", "10/0,4/-/-", $"{DbKeyItem(crackedOrb)}/{Mission(LogWindurst)}/{WVar(0, "GizmoExamined")}/{WVar(0, "RandomGizmo")}");
        Warp(-133.7f, -4.0f, 149.0f, PortWindurst);
        TalkTo("Hakkuru-Rinkuru");
        Check("Hakkuru-Rinkuru with the orb: event 94 (0, the orb)", $"034:{NpcId("Hakkuru-Rinkuru")}:94:0,{crackedOrb}", Ev());
        EventEnd(94);
        Check("  The Horutoto Ruins Experiment complete: 250 rank points, the orb taken", "65535,0/1,250/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbKeyItem(crackedOrb).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 0)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 1-2 The Heart of the Matter");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 1, "The Heart of the Matter");
        Check("  The Heart of the Matter current, Status 1", "1,1", Mission(LogWindurst));
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu: event 137", 137);
        string[] ordinals = { "FIRST", "SECOND", "THIRD", "FOURTH", "FIFTH", "SIXTH" };
        int[] darkOrbs = ordinals.Select(o => Ki($"{o}_DARK_MANA_ORB")).ToArray(), glowingOrbs = ordinals.Select(o => Ki($"{o}_GLOWING_MANA_ORB")).ToArray();
        Check("  the six dark Mana Orbs, Status 2", "101010101010/1,2", $"{string.Concat(darkOrbs.Select(DbKeyItem))}/{Mission(LogWindurst)}");
        Warp(262.0f, -17.0f, -457.0f, EastSarutabaruta);
        int starCharm = Ki("SOUTHEASTERN_STAR_CHARM");
        TalkTo("Pore-Ohre");
        Cs("Pore-Ohre in East Sarutabaruta: cutscene 46", 46);
        Check("  the Southeastern Star Charm, Status 3", "10/1,3", $"{DbKeyItem(starCharm)}/{Mission(LogWindurst)}");
        Warp(590.0f, -1.5f, -658.0f, OuterHorutoto);
        string[] gates = { "_5ee", "_5ef", "_5eg", "_5eh", "_5ei", "_5ej" };
        for (int i = 0; i < 6; i++)
        {
            TalkTo(gates[i]);
            Cs($"  gizmo {gates[i]} at Status 3: the place cutscene {58 + i}", 58 + i);
        }
        Check("  the six placed: OrbsPlaced 6, GizmoUsed bits 1-6, the dark orbs taken", "6/126/000000",
              $"{WVar(1, "OrbsPlaced")}/{WVar(1, "GizmoUsed")}/{string.Concat(darkOrbs.Select(k => DbKeyItem(k).Substring(0, 1)))}");
        TalkTo("_5e9");
        Cs("the gate (_5e9) with the six placed: cutscene 44", 44);
        Check("  the charm taken, Status 4, the place vars cleared", "0/1,4/-/-",
              $"{DbKeyItem(starCharm).Substring(0, 1)}/{Mission(LogWindurst)}/{WVar(1, "OrbsPlaced")}/{WVar(1, "GizmoUsed")}");
        for (int i = 0; i < 6; i++)
        {
            TalkTo(gates[i]);
            Cs($"  gizmo {gates[i]} at Status 4: the collect cutscene {46 + i}", 46 + i);
        }
        Check("  the six glowing orbs, Status 5, the collect vars cleared", "101010101010/1,5/-/-",
              $"{string.Concat(glowingOrbs.Select(DbKeyItem))}/{Mission(LogWindurst)}/{WVar(1, "OrbsCollected")}/{WVar(1, "GizmoEmpty")}");
        WarpIn(262.0f, -17.0f, -457.0f, EastSarutabaruta);
        Check("in East Sarutabaruta at Status 5: the zone-in cutscene 48", $"{EastSarutabaruta}/48", $"{c.Zone}/{Cur()}");
        EventEnd(48);
        Check("  the orbs stolen (taken), Status 6", "000000/1,6", $"{string.Concat(glowingOrbs.Select(k => DbKeyItem(k).Substring(0, 1)))}/{Mission(LogWindurst)}");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu at Status 6: event 143", 143);
        Check("  The Heart of the Matter complete: 500 rank points", "65535,0/1,500/True", $"{Mission(LogWindurst)}/{Rank()}/{DbMissionComplete(LogWindurst, 1)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 1-3 The Price of Peace");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 2, "The Price of Peace");
        Warp(11.0f, -9.75f, -195.0f, WindurstWaters);
        int food = Ki("FOOD_OFFERING"), drink = Ki("DRINK_OFFERING");
        TalkTo("Leepe-Hoppe");
        Cs("Leepe-Hoppe: event 140", 140);
        Check("  the food and drink offerings, Status 2", "10/10/2,2", $"{DbKeyItem(food)}/{DbKeyItem(drink)}/{Mission(LogWindurst)}");
        Warp(-21.0f, 0.3f, 143.0f, Giddeus);
        TalkTo("Laa_Mozi");
        Cs("Laa Mozi: cutscene 45", 45);
        Check("  the food offered, OfferingsTurnedIn 1", "0/1", $"{DbKeyItem(food).Substring(0, 1)}/{WVar(2, "OfferingsTurnedIn")}");
        TalkTo("Ghoo_Pakya");
        Cs("Ghoo Pakya: cutscene 49", 49);
        Check("  the drink offered: Status 3, the count cleared", "0/2,3/-", $"{DbKeyItem(drink).Substring(0, 1)}/{Mission(LogWindurst)}/{WVar(2, "OfferingsTurnedIn")}");
        Warp(20.0f, -10.0f, -190.0f, WindurstWaters);
        Walk(27.0f, -10.0f, -202.0f);
        Check("Windurst Waters' trigger area 1 at Status 3: event 146", "146", Cur());
        EventEnd(146);
        Check("  Status 4", "2,4", Mission(LogWindurst));
        ToRakoh();
        gilW = int.Parse(Gil());
        TalkTo("Rakoh_Buuma");
        Cs("Rakoh Buuma at Status 4: event 154", 154);
        Check("  The Price of Peace complete: rank 2, 1000 gil", $"65535,0/2,0/{gilW + 1000}/True",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbMissionComplete(LogWindurst, 2)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 2-1 Lost for Words");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 3, "Lost for Words");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 3, "Lost for Words");
        Warp(-28.0f, -5.25f, 106.0f, WindurstWaters);
        TalkTo("Tosuka-Porika");
        Cs("Tosuka-Porika: event 160", 160);
        Warp(27.0f, 2.0f, -33.0f, WindurstWoods);
        int coral = Ki("LAPIS_CORAL"), monocle = Ki("LAPIS_MONOCLE"), hideoutKey = Ki("HIDEOUT_KEY");
        TalkTo("Nanaa_Mihgo");
        Check("Nanaa Mihgo: event 165 (0, the coral, the monocle)", $"034:{NpcId("Nanaa_Mihgo")}:165:0,{coral},{monocle}", Ev());
        EventEnd(165, 1);
        int rock = int.Parse(WVar(3, "Rock") == "-" ? "0" : WVar(3, "Rock"));
        Check("  option 1: the Lapis Monocle, Rock rolled 0 to 7, Status 3", "10/True/3,3", $"{DbKeyItem(monocle)}/{rock >= 0 && rock <= 7}/{Mission(LogWindurst)}");
        Warp(40.0f, 19.0f, 213.0f, MazeOfShakhrami);
        uint fossil = uint.Parse(Lua("report(zones[xi.zone.MAZE_OF_SHAKHRAMI].npc.FOSSIL_ROCK_OFFSET)"));
        uint other = fossil + (uint)((rock + 1) % 8);
        Near(other);
        Talk(other);
        Check("  another Fossil Rock: JUST_A_ROCK (no event), Status 3", "none/3,3", $"{Cur()}/{Mission(LogWindurst)}");
        Near(fossil + (uint)rock);
        Talk(fossil + (uint)rock);
        Check("  the rolled Fossil Rock: the Lapis Coral, Status 4", "10/3,4", $"{DbKeyItem(coral)}/{Mission(LogWindurst)}");
        Warp(27.0f, 2.0f, -33.0f, WindurstWoods);
        TalkTo("Nanaa_Mihgo");
        Cs("Nanaa Mihgo with the coral: event 169", 169);
        Check("  the coral and monocle for the Hideout Key, Status 5, Rock cleared", "0/0/10/3,5/-",
              $"{DbKeyItem(coral).Substring(0, 1)}/{DbKeyItem(monocle).Substring(0, 1)}/{DbKeyItem(hideoutKey)}/{Mission(LogWindurst)}/{WVar(3, "Rock")}");
        Warp(-14.0f, -2.2f, 20.0f, InnerHorutoto);
        TalkTo("_5ca");
        Cs("the hideout door (_5ca): cutscene 46", 46);
        Check("  the key used, Status 6", "0/3,6", $"{DbKeyItem(hideoutKey).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(0.0f, -16.75f, 125.0f, WindurstWalls);
        TalkTo("_6n2");
        Cs("the door (_6n2) in Windurst Walls: event 337", 337);
        Check("  Status 7", "3,7", Mission(LogWindurst));
        Warp(-28.0f, -5.25f, 106.0f, WindurstWaters);
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Tosuka-Porika");
        Cs("Tosuka-Porika at Status 7: event 168", 168);
        Check("  Lost for Words complete: 300 rank points", $"65535,0/2,{rpW + 300}/True", $"{Mission(LogWindurst)}/{Rank()}/{DbMissionComplete(LogWindurst, 3)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 2-2 A Testing Time");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 4, "A Testing Time");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 4, "A Testing Time");
        Check("  A Testing Time current, Status 1, AurasteryMissionTaken", "4,1/1", $"{Mission(LogWindurst)}/{Var("AurasteryMissionTaken")}");
        Warp(168.0f, -1.25f, 159.0f, WindurstWaters);
        int doll = Ki("CREATURE_COUNTER_MAGIC_DOLL");
        TalkTo("Moreno-Toeno");
        Cs("Moreno-Toeno: event 182, accepted (option 2)", 182, 2);
        int endTime = int.Parse(WVar(4, "EndTime")), vanaNow = int.Parse(Lua("report(VanadielTime())"));
        Check("  the Creature Counter Magic Doll, Status 2, EndTime a Vana'diel day on", "10/4,2/True",
              $"{DbKeyItem(doll)}/{Mission(LogWindurst)}/{Math.Abs(endTime - vanaNow - int.Parse(Lua("report(xi.vanaTime.DAY)"))) <= 2}");
        Warp(-40.0f, 0.0f, 0.0f, TahrongiCanyon);
        string[] tahrongi = { "Air_Elemental", "Akbaba", "Barghest", "Canyon_Crawler", "Canyon_Rarab", "Earth_Elemental", "Ghost", "Goblin_Ambusher",
                              "Goblin_Archaeologist", "Goblin_Butcher", "Goblin_Digger", "Goblin_Thug", "Goblin_Tinkerer", "Goblin_Weaver", "Grenade",
                              "Habrok", "Herbage_Hunter", "Killer_Bee", "Poltergeist", "Pygmaioi", "Serpopard_Ishtar", "Skeleton_Sorcerer",
                              "Skeleton_Warrior", "Strolling_Sapling", "Wild_Dhalmel", "Yagudo_Acolyte", "Yagudo_Initiate", "Yagudo_Mendicant",
                              "Yagudo_Persecutor", "Yagudo_Piper", "Yagudo_Scribe", "Yara_Ma_Yha_Who" };
        var tcInfo = zone.ZoneInfoOf(c.Zone);
        var counted = tcInfo.Mobs.Where(m => tahrongi.Contains(m.Name) && m.IsSpawned && m.IsAlive).ToList();
        int tKills = 0;
        foreach (var m in counted)
        {
            if (WVar(4, "KillCount") == "30") break;
            if (!m.IsSpawned || !m.IsAlive) continue;
            KillByChar(m.Id);
            tKills++;
        }
        Check("  thirty of the canyon's listed mobs killed: KillCount 30", $"30/30", $"{tKills}/{WVar(4, "KillCount")}");
        Warp(168.0f, -1.25f, 159.0f, WindurstWaters);
        TalkTo("Moreno-Toeno");
        Check("Moreno-Toeno with most of the day left: event 183 (0, the hour, the hours left)", "183/24",
              $"{Cur()}/{Ev().Split(':').Last().Split(',')[2]}");
        EventEnd(183);
        // the day runs on VanadielTime (the wall clock): EndTime brought to within its last hour rather than waited
        Lua($"player:setCharVar('Mission[{LogWindurst}][4]EndTime', VanadielTime() + xi.vanaTime.HOUR / 2)");
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Moreno-Toeno");
        Check("  in the last hour with 30 kills: event 200 (0, the hour, 1, 30)", "200/1,30", $"{Cur()}/{string.Join(",", Ev().Split(':').Last().Split(',').Skip(2))}");
        EventEnd(200);
        Check("  A Testing Time complete: 300 rank points, the doll taken", $"65535,0/2,{rpW + 300}/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbKeyItem(doll).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 4)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 2-3 The Three Kingdoms (San d'Oria first, then Bastok)");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 5, "The Three Kingdoms");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 5, "The Three Kingdoms");
        Warp(2.0f, 0.1f, 30.0f, HeavensTower);
        int letterW = Ki("LETTER_TO_THE_CONSULS_WINDURST"), crestW = Ki("KINDRED_CREST"), reportW = Ki("KINDRED_REPORT");
        TalkTo("Kupipi");
        Check("Kupipi: event 95 (the letter; the Semih Lafihna cipher still to give: param 8 = 1)", $"034:{NpcId("Kupipi")}:95:0,0,0,{letterW},0,0,0,1", Ev());
        EventEnd(95);
        Check("  the Letter to the Consuls, Status 1, the cipher (trust quests on)", "10/5,1/true",
              $"{DbKeyItem(letterW)}/{Mission(LogWindurst)}/{Lua("report(tostring(player:hasItem(xi.item.CIPHER_OF_SEMIHS_ALTER_EGO)))")}");
        Warp(-56.0f, -2.6f, 38.0f, NorthernSandoria);
        TalkTo("Heruze-Moruze");
        Cs("Heruze-Moruze in Northern San d'Oria: event 582", 582);
        Check("  Status 2", "5,2", Mission(LogWindurst));
        TalkTo("Kasaroro");
        Cs("Kasaroro at Status 2: event 546", 546);
        Check("  The Three Kingdoms (San d'Oria) current, Status 3", "6,3", Mission(LogWindurst));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Check("Halver at Status 3: event 502 (the Halver cipher still to give: { [7] = 1 }, the 0-based eighth)", $"034:{NpcId("Halver")}:502:0,0,0,0,0,0,0,1", Ev());
        EventEnd(502);
        Check("  Status 4, the Halver cipher", "6,4/true", $"{Mission(LogWindurst)}/{Lua("report(tostring(player:hasItem(xi.item.CIPHER_OF_HALVERS_ALTER_EGO)))")}");
        Warp(-162.0f, -11.0f, 78.0f, GhelsbaOutpost);
        var vatgit = zone.ZoneInfoOf(c.Zone).Mobs.First(m => m.Name == "Warchief_Vatgit");
        if (!vatgit.IsSpawned) Lua($"SpawnMob({vatgit.Id})");
        KillByChar(vatgit.Id);
        Check("Warchief Vatgit killed in Ghelsba Outpost: Status 5", "False/6,5", $"{vatgit.IsAlive}/{Mission(LogWindurst)}");
        Warp(-72.0f, -2.6f, 36.0f, NorthernSandoria);
        TalkTo("Kasaroro");
        Cs("Kasaroro at Status 5: event 550", 550);
        Check("  The Three Kingdoms (San d'Oria) complete, The Three Kingdoms again, Status 6", "5,6/True", $"{Mission(LogWindurst)}/{DbMissionComplete(LogWindurst, 6)}");
        Warp(24.0f, -17.0f, 44.0f, Metalworks);
        TalkTo("Patt-Pott");
        Cs("Patt-Pott in the Metalworks at Status 6: event 256", 256);
        Check("  The Three Kingdoms (Bastok II) current, Status 8, the letter taken", "9,8/0", $"{Mission(LogWindurst)}/{DbKeyItem(letterW).Substring(0, 1)}");
        Warp(100.0f, -20.0f, -10.0f, Metalworks);
        TalkTo("Pius");
        Check("Pius at Status 8: event 355 (1)", $"034:{NpcId("Pius")}:355:1", Ev());
        EventEnd(355);
        Warp(-18.0f, -10.0f, -25.0f, Metalworks);
        TalkTo("Grohm");
        Check("Grohm at Status 9: event 426 (1)", $"034:{NpcId("Grohm")}:426:1", Ev());
        EventEnd(426);
        Check("  Status 10", "9,10", Mission(LogWindurst));
        Warp(-339.0f, 104.0f, -258.0f, WaughroonShrine);
        WinBattlefield(NpcId("BC_Entrance"), "Waughroon Shrine (Rank 2 mission)");
        Check("  the Kindred Crest, Status 11", "10/9,11", $"{DbKeyItem(crestW)}/{Mission(LogWindurst)}");
        Warp(24.0f, -17.0f, 44.0f, Metalworks);
        TalkTo("Patt-Pott");
        Cs("Patt-Pott with the crest: event 257", 257);
        Check("  The Three Kingdoms (Bastok II) complete, The Three Kingdoms again, Status 11, the Kindred Report for the crest", "5,11/True/10/0",
              $"{Mission(LogWindurst)}/{DbMissionComplete(LogWindurst, 9)}/{DbKeyItem(reportW)}/{DbKeyItem(crestW).Substring(0, 1)}");
        Warp(2.0f, 0.1f, 30.0f, HeavensTower);
        gilW = int.Parse(Gil());
        TalkTo("Kupipi");
        Check("Kupipi with the report: event 101 (0, 0, the certificate)", $"034:{NpcId("Kupipi")}:101:0,0,{Ki("ADVENTURERS_CERTIFICATE")}", Ev());
        EventEnd(101);
        Check("  The Three Kingdoms complete: rank 3, 3000 gil, the Adventurer's Certificate, Certified Adventurer, the report taken",
              $"65535,0/3,0/{gilW + 3000}/10/True/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbKeyItem(Ki("ADVENTURERS_CERTIFICATE"))}/{DbTitle(TitleId("CERTIFIED_ADVENTURER"))}/" +
              $"{DbKeyItem(reportW).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 5)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 3-1 To Each His Own Right");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 10, "To Each His Own Right");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 10, "To Each His Own Right");
        Warp(2.0f, 0.1f, 30.0f, HeavensTower);
        int bauble = Ki("STARWAY_STAIRWAY_BAUBLE");
        TalkTo("Kupipi");
        Check("Kupipi: event 103 (0, 0, the bauble)", $"034:{NpcId("Kupipi")}:103:0,0,{bauble}", Ev());
        EventEnd(103);
        Check("  the Starway Stairway Bauble, Status 1", "10/10,1", $"{DbKeyItem(bauble)}/{Mission(LogWindurst)}");
        Warp(2.0f, -47.0f, 16.0f, HeavensTower);
        TalkTo("Rhy_Epocan");
        Cs("Rhy Epocan: event 107", 107);
        Warp(-133.7f, -4.0f, 149.0f, PortWindurst);
        TalkTo("Hakkuru-Rinkuru");
        Cs("Hakkuru-Rinkuru at Status 2: event 147", 147);
        Check("  Status 3", "10,3", Mission(LogWindurst));
        // the two handles open the trap door on alternate days (VanadielDayOfTheYear's parity), the cutscene with it
        string handle = int.Parse(Lua("report(VanadielDayOfTheYear())")) % 2 == 0 ? "_47b" : "_47c";
        Warp(20.0f, -1.1f, -14.0f, CastleOztroja);
        MoveTo(20.0f, -1.087f, -14.0f);
        Tick(400);
        Talk(NpcId(handle));
        Cs($"the trap door's handle ({handle}) for this day, at Status 3: cutscene 43", 43);
        Check("  Status 4", "10,4", Mission(LogWindurst));
        Warp(2.0f, -47.0f, 16.0f, HeavensTower);
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Rhy_Epocan");
        Cs("Rhy Epocan at Status 4: event 114", 114);
        Check("  To Each His Own Right complete: 450 rank points", $"65535,0/3,{rpW + 450}/True", $"{Mission(LogWindurst)}/{Rank()}/{DbMissionComplete(LogWindurst, 10)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 3-2 Written in the Stars");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 11, "Written in the Stars");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 11, "Written in the Stars");
        Warp(15.0f, -26.25f, 20.0f, HeavensTower);
        int charm = Ki("CHARM_OF_LIGHT");
        TalkTo("Zubaba");
        Cs("Zubaba: event 121", 121);
        Check("  the Charm of Light, Status 1", "10/11,1", $"{DbKeyItem(charm)}/{Mission(LogWindurst)}");
        Warp(-330.0f, -1.9f, 144.0f, InnerHorutoto);
        TalkTo("_5ci");
        Check("the Inner Horutoto door (_5ci): event 41 (0, the charm)", $"034:{NpcId("_5ci")}:41:0,{charm}", Ev());
        EventEnd(41);
        Check("  the charm taken, Status 2", "0/11,2", $"{DbKeyItem(charm).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(15.0f, -26.25f, 20.0f, HeavensTower);
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Zubaba");
        Cs("Zubaba at Status 2: event 135", 135);
        Check("  Written in the Stars complete: 400 rank points and the first time's 100", $"65535,0/3,{rpW + 500}/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbMissionComplete(LogWindurst, 11)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 3-3 A New Journey");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 12, "A New Journey");
        ToRakoh();
        int summons = Ki("STAR_CRESTED_SUMMONS_1"), ambassadorW = Ki("LETTER_TO_THE_AMBASSADOR");
        Accept("Rakoh_Buuma", 114, 12, "A New Journey");
        Check("  A New Journey current, the Star Crested Summons", "12,0/10", $"{Mission(LogWindurst)}/{DbKeyItem(summons)}");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Cs("the Star Sibyl's door (_6q2): event 153", 153);
        Check("  the summons for the Letter to the Ambassador, Status 1", "0/10/12,1",
              $"{DbKeyItem(summons).Substring(0, 1)}/{DbKeyItem(ambassadorW)}/{Mission(LogWindurst)}");
        Warp(36.0f, 9.0f, -35.0f, Rulude);
        TalkTo("Pakh_Jatalfih");
        Cs("Pakh Jatalfih: event 43", 43);
        Check("  the letter taken, Status 2", "0/12,2", $"{DbKeyItem(ambassadorW).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(638.0f, 13.3f, 60.0f, LowerDelkfutt);
        Lua($"player:addItem({DelkfuttKey}, 1)");
        TradeTo("_540", (Slot(DelkfuttKey), 1));
        Cs("the Delkfutt Key to the Cermet Door (_540): cutscene 2", 2);
        Check("  the key item for the key, Status 3", "10/0/12,3", $"{DbKeyItem(Ki("DELKFUTT_KEY"))}/{Qty(DelkfuttKey)}/{Mission(LogWindurst)}");
        Warp(36.0f, 9.0f, -35.0f, Rulude);
        TalkTo("Pakh_Jatalfih");
        Cs("Pakh Jatalfih at Status 3: event 141", 141);
        Warp(32.0f, 7.5f, -19.0f, Rulude);
        gilW = int.Parse(Gil());
        TalkTo("_6r8");
        Cs("the embassy door (_6r8) at Status 4: event 40", 40);
        Check("  A New Journey complete: rank 4, 5000 gil", $"65535,0/4,0/{gilW + 5000}/True", $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbMissionComplete(LogWindurst, 12)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 4-1 Magicite");
        Warp(36.0f, 9.0f, -35.0f, Rulude);
        TalkTo("Pakh_Jatalfih");
        Cs("Pakh Jatalfih before the points: event 54", 54);
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 13, "Magicite");
        Warp(36.0f, 9.0f, -35.0f, Rulude);
        TalkTo("Pakh_Jatalfih");
        Cs("Pakh Jatalfih, the points met: event 50", 50);
        int wPermit = Ki("ARCHDUCAL_AUDIENCE_PERMIT"), wAldoLetter = Ki("LETTER_TO_ALDO"), wBell = Ki("SILVER_BELL"), wTorch = Ki("YAGUDO_TORCH");
        Warp(32.0f, 7.5f, -19.0f, Rulude);
        TalkTo("_6r8");
        Check("the embassy door (_6r8): event 131 (no permit: 0)", $"034:{NpcId("_6r8")}:131", Ev());
        EventEnd(131);
        Check("  Magicite current, Status 1, the Archducal Audience Permit", "13,1/10", $"{Mission(LogWindurst)}/{DbKeyItem(wPermit)}");
        Warp(0.0f, -5.0f, 64.0f, Rulude);
        TalkTo("_6r9");
        Cs("the audience chamber (_6r9): event 128", 128);
        Check("  the Letter to Aldo, Status 2", "10/13,2", $"{DbKeyItem(wAldoLetter)}/{Mission(LogWindurst)}");
        Warp(20.0f, 3.0f, -56.0f, LowerJeuno);
        TalkTo("Aldo");
        Check("Aldo: event 152 (no Silver Bell: 0, 1)", $"034:{NpcId("Aldo")}:152:0,1", Ev());
        EventEnd(152);
        Check("  the Silver Bell for the letter, Status 3", "10/0/13,3", $"{DbKeyItem(wBell)}/{DbKeyItem(wAldoLetter).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(9.0f, 1.0f, 68.0f, UpperJeuno);
        TalkTo("Paya-Sabya");
        Cs("Paya-Sabya: event 80", 80);
        Warp(-26.8f, 4.6f, -135.0f, LowerJeuno);
        TalkTo("Muckvix");
        Cs("Muckvix: event 184", 184);
        Check("  the Yagudo Torch, Option 2", "10/2", $"{DbKeyItem(wTorch)}/{WVar(13, "Option")}");
        WarpIn(-344.0f, 25.0f, 45.0f, AltarRoom);
        Check("in the Altar Room: the Fickblix zone-in cutscene 10000", $"{AltarRoom}/10000", $"{c.Zone}/{Cur()}");
        EventEnd(10000);
        int wOrastone = Ki("MAGICITE_ORASTONE"), wOptistone = Ki("MAGICITE_OPTISTONE"), wAurastone = Ki("MAGICITE_AURASTONE");
        TalkTo("Magicite");
        Check("the Altar Room's Magicite: event 44 with the zone", $"034:{NpcId("Magicite")}:44:{AltarRoom}", Ev());
        EventEnd(44);
        Warp(-160.0f, -8.0f, 10.0f, MonasticCavern);
        TalkTo("Magicite");
        Check("the Monastic Cavern's Magicite: event 0 with the zone", $"034:{NpcId("Magicite")}:0:{MonasticCavern}", Ev());
        EventEnd(0);
        Warp(11.0f, 25.0f, -79.0f, QulunDome);
        TalkTo("Magicite");
        Check("Qulun Dome's Magicite, the last one: event 0 with the Lion's part (1, the rosary, the necklace)",
              $"034:{NpcId("Magicite")}:0:1,{Ki("CORUSCANT_ROSARY")},{Ki("BLACK_MATINEE_NECKLACE")}", Ev());
        EventEnd(0);
        Check("  the three stones", "10/10/10", $"{DbKeyItem(wOrastone)}/{DbKeyItem(wOptistone)}/{DbKeyItem(wAurastone)}");
        Warp(0.0f, -5.0f, 64.0f, Rulude);
        TalkTo("_6r9");
        Check("the audience chamber with the three stones: event 60 (no Airship Pass)", $"034:{NpcId("_6r9")}:60", Ev());
        EventEnd(60);
        Check("  the stones taken, the Airship Pass, Have Wings, Will Fly, Status 4", "0/0/0/10/True/13,4",
              $"{DbKeyItem(wOrastone).Substring(0, 1)}/{DbKeyItem(wOptistone).Substring(0, 1)}/{DbKeyItem(wAurastone).Substring(0, 1)}/" +
              $"{DbKeyItem(Ki("AIRSHIP_PASS"))}/{DbTitle(TitleId("HAVE_WINGS_WILL_FLY"))}/{Mission(LogWindurst)}");
        Warp(36.0f, 9.0f, -35.0f, Rulude);
        gilW = int.Parse(Gil());
        int wMessage = Ki("MESSAGE_TO_JEUNO_WINDURST");
        TalkTo("Pakh_Jatalfih");
        Cs("Pakh Jatalfih at Status 4: event 37", 37);
        Check("  Magicite complete: rank 5, 10000 gil, the Message to Jeuno", $"65535,0/5,0/{gilW + 10000}/10/True",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbKeyItem(wMessage)}/{DbMissionComplete(LogWindurst, 13)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 5-1 The Final Seal");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        int wNewSeal = Ki("NEW_FEIYIN_SEAL"), wBurntSeal = Ki("BURNT_SEAL");
        TalkTo("_6q2");
        Cs("the Star Sibyl's door with the message: event 166, accepted (option 0)", 166);
        Check("  The Final Seal current, the message taken, the New Fei'Yin Seal, Status 10", "14,10/0/10",
              $"{Mission(LogWindurst)}/{DbKeyItem(wMessage).Substring(0, 1)}/{DbKeyItem(wNewSeal)}");
        WarpIn(243.0f, -24.0f, 62.0f, FeiYin);
        Check("in Fei'Yin: the zone-in cutscene 1", $"{FeiYin}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Warp(-221.0f, -24.0f, 19.0f, QubiaArena);
        WinBattlefield(NpcId("BC_Entrance"), "Qu'Bia Arena (Rank 5 mission)");
        Check("  the Burnt Seal for the new seal, Status 12", "10/0/14,12", $"{DbKeyItem(wBurntSeal)}/{DbKeyItem(wNewSeal).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Cs("the Star Sibyl's door with the Burnt Seal: event 192", 192);
        Check("  The Final Seal complete: 600 rank points, the seal taken", "65535,0/5,600/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbKeyItem(wBurntSeal).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 14)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 5-2 The Shadow Awaits");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 15, "The Shadow Awaits");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 15, "The Shadow Awaits");
        Check("  The Shadow Awaits current, the Star Crested Summons", "15,0/10", $"{Mission(LogWindurst)}/{DbKeyItem(summons)}");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Cs("the Star Sibyl's door with the summons: event 214", 214);
        Check("  Status 2, Star-Ordained Warrior, the summons taken", "15,2/True/0",
              $"{Mission(LogWindurst)}/{DbTitle(TitleId("STAR_ORDAINED_WARRIOR"))}/{DbKeyItem(summons).Substring(0, 1)}");
        Warp(-109.0f, -6.0f, 0.1f, ThroneRoom);
        TalkTo("_4l1");
        Cs("the Throne Room's door (_4l1) at Status 2: the mission's event 6", 6);
        FightShadowLord(LogWindurst);
        Rezone();
        Check("  the zone's onEventFinish for 7: back to Castle Zvahl Baileys' entrance", $"{CastleZvahlBaileys}/380/-20", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        WarpIn(0.0f, 0.1f, 30.0f, HeavensTower);
        Check("in Heaven's Tower with the fragment at Status 4: the zone-in cutscene 215", $"{HeavensTower}/215", $"{c.Zone}/{Cur()}");
        EventEnd(215);
        Check("  ReturnCS 1", "1", WVar(15, "ReturnCS"));
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        gilW = int.Parse(Gil());
        TalkTo("_6q2");
        Cs("the Star Sibyl's door with the fragment: event 216", 216);
        Check("  The Shadow Awaits complete: rank 6, 20000 gil, the fragment taken", $"65535,0/6,0/{gilW + 20000}/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbKeyItem(Ki("SHADOW_FRAGMENT")).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 15)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 6-1 Full Moon Fountain");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 16, "Full Moon Fountain");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 16, "Full Moon Fountain");
        Warp(-133.7f, -4.0f, 149.0f, PortWindurst);
        int swCharm = Ki("SOUTHWESTERN_STAR_CHARM");
        TalkTo("Hakkuru-Rinkuru");
        Check("Hakkuru-Rinkuru: event 456 (0, the charm)", $"034:{NpcId("Hakkuru-Rinkuru")}:456:0,{swCharm}", Ev());
        EventEnd(456);
        Check("  the Southwestern Star Charm, Status 1", "10/16,1", $"{DbKeyItem(swCharm)}/{Mission(LogWindurst)}");
        Warp(-289.0f, -1.65f, -658.0f, OuterHorutoto);
        uint jacks = MobOf("OUTER_HORUTOTO_RUINS", "FULL_MOON_FOUNTAIN_OFFSET");
        var ohInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("_5eb");
        Check("the gate (_5eb) at Status 1: the four Jacks spawned, claimed by the char", $"none/4/{c.Id}",
              $"{Cur()}/{Enumerable.Range(0, 4).Count(i => ohInfo.MobById[jacks + (uint)i].IsSpawned)}/{ohInfo.MobById[jacks].OwnerId}");
        for (uint i = 0; i < 3; i++) KillByChar(jacks + i);
        Check("  three killed: Status 1", "16,1", Mission(LogWindurst));
        KillByChar(jacks + 3);
        Check("  the fourth killed: Status 2", "16,2", Mission(LogWindurst));
        for (int i = 0; i < 30 && Enumerable.Range(0, 4).Any(k => ohInfo.MobById[jacks + (uint)k].IsSpawned); i++) Tick(1000);
        TalkTo("_5eb");
        Cs("the gate with the Jacks gone: event 68", 68);
        Check("  the charm taken, Status 3", "0/16,3", $"{DbKeyItem(swCharm).Substring(0, 1)}/{Mission(LogWindurst)}");
        WarpIn(-260.0f, 0.7f, -310.0f, FullMoonFountain);
        Check("in the Full Moon Fountain at Status 3: the zone-in cutscene 50", $"{FullMoonFountain}/50", $"{c.Zone}/{Cur()}");
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        EventEnd(50);
        Check("  Full Moon Fountain complete: 650 rank points", $"65535,0/6,{Math.Min(4000, rpW + 650)}/True", $"{Mission(LogWindurst)}/{Rank()}/{DbMissionComplete(LogWindurst, 16)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 6-2 Saintly Invitation");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 17, "Saintly Invitation");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 17, "Saintly Invitation");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        int invitation = Ki("HOLY_ONES_INVITATION"), balgaCert = Ki("BALGA_CHAMPION_CERTIFICATE"), oath = Ki("HOLY_ONES_OATH");
        TalkTo("_6q2");
        Cs("the Star Sibyl's door: event 310", 310);
        Check("  the Holy One's Invitation, Hero on Behalf of Windurst, Status 1", "10/True/17,1",
              $"{DbKeyItem(invitation)}/{DbTitle(TitleId("HERO_ON_BEHALF_OF_WINDURST"))}/{Mission(LogWindurst)}");
        Warp(317.0f, -28.0f, 340.0f, BalgasDais);
        WinBattlefield(NpcId("BC_Entrance"), "Balga's Dais (Saintly Invitation)");
        Check("  the Balga Champion Certificate for the invitation, Victor of the Balga Contest, Status 2", "10/0/True/17,2",
              $"{DbKeyItem(balgaCert)}/{DbKeyItem(invitation).Substring(0, 1)}/{DbTitle(TitleId("VICTOR_OF_THE_BALGA_CONTEST"))}/{Mission(LogWindurst)}");
        Warp(-100.0f, -61.0f, 147.0f, CastleOztroja);
        TalkTo("Kaa_Toru_the_Just");
        Check("Kaa Toru the Just: cutscene 45 (0, the oath)", $"034:{NpcId("Kaa_Toru_the_Just")}:45:0,{oath}", Ev());
        EventEnd(45);
        Check("  the Ashura Necklace, the oath for the certificate, Status 3", "1/10/0/17,3",
              $"{Qty(AshuraNecklace)}/{DbKeyItem(oath)}/{DbKeyItem(balgaCert).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        gilW = int.Parse(Gil());
        TalkTo("_6q2");
        Cs("the Star Sibyl's door with the oath: event 312", 312);
        Check("  Saintly Invitation complete: rank 7, 40000 gil, the oath taken", $"65535,0/7,0/{gilW + 40000}/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbKeyItem(oath).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 17)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 7-1 The Sixth Ministry");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 18, "The Sixth Ministry");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 18, "The Sixth Ministry");
        Warp(-28.0f, -5.25f, 106.0f, WindurstWaters);
        int optistery = Ki("OPTISTERY_RING");
        TalkTo("Tosuka-Porika");
        Check("Tosuka-Porika: event 715 (0, the ring)", $"034:{NpcId("Tosuka-Porika")}:715:0,{optistery}", Ev());
        EventEnd(715);
        Check("  the Optistery Ring, Status 1", "10/18,1", $"{DbKeyItem(optistery)}/{Mission(LogWindurst)}");
        Warp(144.0f, 13.0f, -16.0f, Toraimarai);
        uint tome = uint.Parse(Lua("report(zones[xi.zone.TORAIMARAI_CANAL].npc.TOME_OF_MAGIC_OFFSET)"));
        Near(tome + 4);
        Talk(tome + 4);
        Check("the fifth Tome of Magic at Status 1: event 69 (0, the ring)", $"034:{tome + 4}:69:0,{optistery}", Ev());
        EventEnd(69);
        Check("  Status 2", "18,2", Mission(LogWindurst));
        Near(tome);
        Talk(tome);
        Check("  the first tome: its own script's event 65 (65 + its offset)", "65", Cur());
        EventEnd(65);
        Warp(-28.0f, -5.25f, 106.0f, WindurstWaters);
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Tosuka-Porika");
        Cs("Tosuka-Porika at Status 2: event 724", 724);
        Check("  The Sixth Ministry complete: 700 rank points, the Blank Book of the Gods, the ring taken", $"65535,0/7,{Math.Min(4000, rpW + 700)}/10/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbKeyItem(Ki("BLANK_BOOK_OF_THE_GODS"))}/{DbKeyItem(optistery).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 18)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 7-2 Awakening of the Gods");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 19, "Awakening of the Gods");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 19, "Awakening of the Gods");
        Warp(11.0f, -9.75f, -195.0f, WindurstWaters);
        TalkTo("Leepe-Hoppe");
        Cs("Leepe-Hoppe: event 734", 734);
        Warp(13.0f, -4.7f, -155.0f, WindurstWaters);
        TalkTo("Kerutoto");
        Cs("Kerutoto at Status 1: event 736", 736);
        Check("  Status 2", "19,2", Mission(LogWindurst));
        Warp(29.0f, -12.0f, -174.0f, Kazham);
        TalkTo("Romaa_Mihgo");
        Cs("Romaa Mihgo in Kazham: event 266", 266);
        Warp(-23.0f, -4.0f, -21.0f, Kazham);
        TalkTo("Vanono");
        Cs("Vanono at Status 3: event 264", 264);
        Check("  Status 4", "19,4", Mission(LogWindurst));
        Warp(338.0f, -1.9f, 330.0f, TempleOfUggalepih);
        int bookOfGods = Ki("BOOK_OF_THE_GODS");
        Lua($"player:addItem({CursedKey}, 1)");
        TradeTo("_4fx", (Slot(CursedKey), 1));
        Check("a Cursed Key to the door (_4fx) from below it: cutscene 23 (0, 0, 1)", "23/0,0,1", $"{Cur()}/{Ev().Split(':').Last()}");
        EventEnd(23);
        Check("  the key taken, the blank book for the Book of the Gods, Status 5", "0/0/10/19,5",
              $"{Qty(CursedKey)}/{DbKeyItem(Ki("BLANK_BOOK_OF_THE_GODS")).Substring(0, 1)}/{DbKeyItem(bookOfGods)}/{Mission(LogWindurst)}");
        Warp(11.0f, -9.75f, -195.0f, WindurstWaters);
        gilW = int.Parse(Gil());
        TalkTo("Leepe-Hoppe");
        Cs("Leepe-Hoppe with the book: event 742", 742);
        Check("  Awakening of the Gods complete: rank 8, 60000 gil", $"65535,0/8,0/{gilW + 60000}/True",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbMissionComplete(LogWindurst, 19)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 8-1 Vain");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 20, "Vain");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 20, "Vain");
        Warp(168.0f, -1.25f, 159.0f, WindurstWaters);
        int starSeeker = Ki("STAR_SEEKER"), drainedSeeker = Ki("MAGIC_DRAINED_STAR_SEEKER");
        TalkTo("Moreno-Toeno");
        Check("Moreno-Toeno: event 752 (0, the Aurastery Ring)", $"034:{NpcId("Moreno-Toeno")}:752:0,{Ki("AURASTERY_RING")}", Ev());
        EventEnd(752);
        Check("  the Star Seeker, Fugitive Minister Bounty Hunter, Status 1", "10/True/20,1",
              $"{DbKeyItem(starSeeker)}/{DbTitle(TitleId("FUGITIVE_MINISTER_BOUNTY_HUNTER"))}/{Mission(LogWindurst)}");
        // the Star Seeker's zone-in cutscenes across the regions, each pointing the way with an update; East
        // Sarutabaruta's from Windurst Woods
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TakeZoneLine(LineTo(EastSarutabaruta), false);
        Check("into East Sarutabaruta from Windurst Woods at Status 1: the zone-in cutscene 52", $"{EastSarutabaruta}/{WindurstWoods}/52", $"{c.Zone}/{c.PrevZone}/{Cur()}");
        s.PacketList.Clear();
        EventEnd(52, 0, 1);
        Check("  an update: the mission's onEventUpdate answers (0x05C), still in 52", "True/52", $"{Of(0x05C).Count > 0}/{Cur()}");
        EventEnd(52);
        Check("  52 finished: still Status 1", "20,1", Mission(LogWindurst));
        WarpIn(0.0f, -27.0f, 65.0f, RoMaeve);
        Check("in Ro'Maeve at Status 1: the zone-in cutscene 3", $"{RoMaeve}/3", $"{c.Zone}/{Cur()}");
        EventEnd(3);
        TalkTo("QuHau_Spring");
        Cs("the Qu'Hau Spring: cutscene 2", 2);
        Check("  Status 2", "20,2", Mission(LogWindurst));
        Warp(187.0f, -3.4f, -115.0f, Davoi);
        TalkTo("Sedal-Godjal");
        Check("Sedal-Godjal with the Star Seeker: cutscene 118 (0, the wand, the seeker)", $"034:{NpcId("Sedal-Godjal")}:118:0,{CurseWand},{starSeeker}", Ev());
        EventEnd(118);
        Check("  the seeker drained, Status 3", "0/10/20,3", $"{DbKeyItem(starSeeker).Substring(0, 1)}/{DbKeyItem(drainedSeeker)}/{Mission(LogWindurst)}");
        Lua($"player:addItem({CurseWand}, 1)");
        TradeTo("Sedal-Godjal", (Slot(CurseWand), 1));
        Cs("a Curse Wand to Sedal-Godjal: cutscene 120", 120);
        Check("  the wand taken, Status 4", "0/20,4", $"{Qty(CurseWand)}/{Mission(LogWindurst)}");
        Warp(168.0f, -1.25f, 159.0f, WindurstWaters);
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        TalkTo("Moreno-Toeno");
        Cs("Moreno-Toeno at Status 4: event 758", 758);
        Check("  Vain complete: 750 rank points, the drained seeker taken", $"65535,0/8,{Math.Min(4000, rpW + 750)}/0/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbKeyItem(drainedSeeker).Substring(0, 1)}/{DbMissionComplete(LogWindurst, 20)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 8-2 The Jester Who'd Be King");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 21, "The Jester Who'd Be King");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 21, "The Jester Who'd Be King");
        int manustery = Ki("MANUSTERY_RING"), aurastery = Ki("AURASTERY_RING"), rhinostery = Ki("RHINOSTERY_RING"), orastery = Ki("ORASTERY_RING"),
            glove = Ki("GLOVE_OF_PERPETUAL_TWILIGHT");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Check("Apururu: event 588 (0, the Manustery Ring)", $"034:{NpcId("Apururu")}:588:0,{manustery}", Ev());
        EventEnd(588);
        Check("  the Manustery Ring, Status 1", "10/21,1", $"{DbKeyItem(manustery)}/{Mission(LogWindurst)}");
        Warp(-28.0f, -5.25f, 106.0f, WindurstWaters);
        TalkTo("Tosuka-Porika");
        Cs("Tosuka-Porika without the Optistery Ring: event 801", 801);
        Warp(187.0f, -3.4f, -115.0f, Davoi);
        TalkTo("Sedal-Godjal");
        Cs("Sedal-Godjal at Status 1: event 122", 122);
        Check("  two of the three rings: Status 1", "10/10/21,1", $"{DbKeyItem(optistery)}/{DbKeyItem(aurastery)}/{Mission(LogWindurst)}");
        Warp(-182.0f, -2.0f, 190.0f, FeiYin);
        TalkTo("_no4");
        Cs("Fei'Yin's door (_no4): cutscene 22", 22);
        Check("  the third ring: Status 2", "10/21,2", $"{DbKeyItem(rhinostery)}/{Mission(LogWindurst)}");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu at Status 2: event 601", 601);
        Warp(2.0f, 0.1f, 30.0f, HeavensTower);
        TalkTo("Kupipi");
        Cs("Kupipi at Status 3: event 326", 326);
        Check("  Status 4", "21,4", Mission(LogWindurst));
        Warp(-424.0f, -1.9f, 618.0f, OuterHorutoto);
        uint queens = MobOf("OUTER_HORUTOTO_RUINS", "JESTER_WHOD_BE_KING_OFFSET");
        var qhInfo = zone.ZoneInfoOf(c.Zone);
        TalkTo("_5e5");
        Check("the gate (_5e5) at Status 4: the two Queens spawned", "none/True/True",
              $"{Cur()}/{qhInfo.MobById[queens].IsSpawned}/{qhInfo.MobById[queens + 1].IsSpawned}");
        KillByChar(queens);
        KillByChar(queens + 1);
        Check("  both Queens killed: Status 5", "21,5", Mission(LogWindurst));
        TalkTo("_5e5");
        Cs("the gate at Status 5: event 71", 71);
        Check("  the Orastery Ring, Status 6", "10/21,6", $"{DbKeyItem(orastery)}/{Mission(LogWindurst)}");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu at Status 6: event 590", 590);
        Warp(122.0f, -3.0f, 116.0f, WindurstWalls);
        TalkTo("Shantotto");
        Check("Shantotto at Status 7: event 397 (the glove)", $"034:{NpcId("Shantotto")}:397:0,0,0,{glove}", Ev());
        EventEnd(397);
        Check("  the Glove of Perpetual Twilight for the five rings, Status 8", "10/00000/21,8",
              $"{DbKeyItem(glove)}/{string.Concat(new[] { manustery, optistery, aurastery, rhinostery, orastery }.Select(k => DbKeyItem(k).Substring(0, 1)))}/{Mission(LogWindurst)}");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu with the glove: event 592", 592);
        Check("  the glove taken, Status 9", "0/21,9", $"{DbKeyItem(glove).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(-226.0f, -1.9f, 95.0f, InnerHorutoto);
        TalkTo("_5cb");
        Cs("the Inner Horutoto door (_5cb) at Status 9: cutscene 75", 75);
        Check("  the Book of the Gods taken, Status 10", "0/21,10", $"{DbKeyItem(bookOfGods).Substring(0, 1)}/{Mission(LogWindurst)}");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        gilW = int.Parse(Gil());
        TalkTo("Apururu");
        Cs("Apururu at Status 10: event 609", 609);
        Check("  The Jester Who'd Be King complete: rank 9, 80000 gil, Option 1", $"65535,0/9,0/{gilW + 80000}/True/1",
              $"{Mission(LogWindurst)}/{Rank()}/{Gil()}/{DbMissionComplete(LogWindurst, 21)}/{WVar(21, "Option")}");
        Warp(122.0f, -3.0f, 116.0f, WindurstWalls);
        TalkTo("Shantotto");
        Cs("Shantotto after 8-2: event 399", 399);
        Check("  Option cleared", "-", WVar(21, "Option"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 9-1 Doll of the Dead");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 22, "Doll of the Dead");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 22, "Doll of the Dead");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu: event 619", 619);
        WarpIn(0.0f, 0.1f, 30.0f, HeavensTower);
        Check("in Heaven's Tower at Status 1: the zone-in cutscene 335", $"{HeavensTower}/335", $"{c.Zone}/{Cur()}");
        EventEnd(335);
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Cs("the Star Sibyl's door at Status 2: event 362", 362);
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu at Status 3: event 620", 620);
        Warp(-110.0f, -14.0f, 205.0f, WindurstWalls);
        TalkTo("Yoran-Oran");
        Check("Yoran-Oran at Status 4: event 439 (0, the jug, the humus)", $"034:{NpcId("Yoran-Oran")}:439:0,{Lua("report(xi.item.JUG_OF_HUMUS)")},{GoobbueHumus}", Ev());
        EventEnd(439);
        Check("  Status 5", "22,5", Mission(LogWindurst));
        Warp(82.0f, 8.6f, 142.0f, BoyahdaTree);
        int zonpa = Ki("LETTER_FROM_ZONPA_ZIPPA");
        Lua($"player:addItem({GoobbueHumus}, 1)");
        TradeTo("Mandragora_Warden", (Slot(GoobbueHumus), 1));
        Cs("Goobbue Humus to the Mandragora Warden: cutscene 13", 13);
        Check("  the humus taken, the Letter from Zonpa-Zippa, Status 6", "0/10/22,6", $"{Qty(GoobbueHumus)}/{DbKeyItem(zonpa)}/{Mission(LogWindurst)}");
        Warp(-12.0f, -2.75f, 18.0f, WindurstWoods);
        TalkTo("Apururu");
        Cs("Apururu with the letter: event 621", 621);
        Check("  the letter taken, Status 7", "0/22,7", $"{DbKeyItem(zonpa).Substring(0, 1)}/{Mission(LogWindurst)}");
        WarpIn(-260.0f, 0.7f, -310.0f, FullMoonFountain);
        Check("in the Full Moon Fountain at Status 7: the zone-in cutscene 61", $"{FullMoonFountain}/61", $"{c.Zone}/{Cur()}");
        rpW = int.Parse(Lua("report(player:getRankPoints())"));
        EventEnd(61);
        Check("  Doll of the Dead complete: 800 rank points, Guiding Star", $"65535,0/9,{Math.Min(4000, rpW + 800)}/True/True",
              $"{Mission(LogWindurst)}/{Rank()}/{DbTitle(TitleId("GUIDING_STAR"))}/{DbMissionComplete(LogWindurst, 22)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Windurst 9-2 Moon Reading");
        Warp(107.0f, -5.0f, -47.0f, WindurstWoods);
        DonateFor("Harara_WW", 23, "Moon Reading");
        ToRakoh();
        Accept("Rakoh_Buuma", 114, 23, "Moon Reading");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Cs("the Star Sibyl's door: event 384", 384);
        Check("  Status 1", "23,1", Mission(LogWindurst));
        int verseRomaeve = Ki("ANCIENT_VERSE_OF_ROMAEVE"), verseAltepa = Ki("ANCIENT_VERSE_OF_ALTEPA"), verseUggalepih = Ki("ANCIENT_VERSE_OF_UGGALEPIH");
        Warp(0.0f, -27.0f, 65.0f, RoMaeve);
        TalkTo("QuHau_Spring");
        Cs("the Qu'Hau Spring at Status 1: cutscene 4", 4);
        Warp(-237.0f, -1.0f, -19.0f, TempleOfUggalepih);
        TalkTo("qm_windy_9_2");
        Cs("Uggalepih's ??? at Status 1: cutscene 68", 68);
        Warp(-469.0f, -1.0f, 622.0f, QuicksandCaves);
        TakeZoneLine(LineTo(ChamberOfOracles), false);
        Check("into the Chamber of Oracles from the Quicksand Caves at Status 1: the zone-in cutscene 3", $"{ChamberOfOracles}/3", $"{c.Zone}/{Cur()}");
        EventEnd(3);
        Check("  the three Ancient Verses", "10/10/10", $"{DbKeyItem(verseRomaeve)}/{DbKeyItem(verseAltepa)}/{DbKeyItem(verseUggalepih)}");
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Cs("the Star Sibyl's door with the verses: event 385", 385);
        Check("  Status 2", "23,2", Mission(LogWindurst));
        Warp(-301.0f, 9.5f, -260.0f, FullMoonFountain);
        var moonBf = EnterBattlefield(NpcId("MS_Entrance"), "the Full Moon Fountain (Moon Reading)");
        uint aceOfCups = MobOf("FULL_MOON_FOUNTAIN", "ACE_OF_CUPS");
        var fmInfo = zone.ZoneInfoOf(c.Zone);
        var cards = Enumerable.Range(0, 4).Select(i => fmInfo.MobById[aceOfCups + (uint)i]).ToList();
        Check("  phase 1: the four cards up", "4", cards.Count(m => m.IsSpawned));
        // one at a time: each death's listener checks the others (allDeath once, on the last)
        foreach (var m in cards)
        {
            m.Hp = 0;
            Tick(1000);
        }
        for (int i = 0; i < 30 && Cur() != "32004"; i++) Tick(400);
        Check("phase 1 dead: allDeath starts 32004 (1, 0, 1, 0, 1)", "32004/1,0,1,0,1", $"{Cur()}/{Ev().Split(':').Last()}");
        EventEnd(32004);
        var phaseTwoCards = Enumerable.Range(4, 2).Select(i => fmInfo.MobById[aceOfCups + (uint)i]).ToList();
        Check("  32004 finished: phase 2 (the two) up, Ajido-Marujido inserted as an ally, the char moved", "2/true/340",
              $"{phaseTwoCards.Count(m => m.IsSpawned)}/" +
              $"{Lua("local t = nil for _, e in pairs(player:getBattlefield():getAllies()) do t = e end report(tostring(t ~= nil))")}/{MathF.Round(c.X)}");
        foreach (var m in phaseTwoCards) m.Hp = 0;
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("  phase 2 dead, won: 32001", "2/32001", $"{moonBf?.Status}/{Cur()}");
        EventEnd(32001);
        Check("32001 finished: the verses taken, Status 3", "000/23,3",
              $"{DbKeyItem(verseRomaeve).Substring(0, 1)}{DbKeyItem(verseAltepa).Substring(0, 1)}{DbKeyItem(verseUggalepih).Substring(0, 1)}/{Mission(LogWindurst)}");
        Rezone();
        Warp(0.0f, -51.5f, 38.0f, HeavensTower);
        TalkTo("_6q2");
        Check("the Star Sibyl's door at Status 3: event 386 (0, 0, the Orastery Ring)", $"034:{NpcId("_6q2")}:386:0,0,{orastery}", Ev());
        EventEnd(386);
        Check("  Status 4", "23,4", Mission(LogWindurst));
        Warp(2.0f, 0.1f, 30.0f, HeavensTower);
        TalkTo("Kupipi");
        Cs("Kupipi at Status 4: event 400 (important once)", 400);
        gilW = int.Parse(Gil());
        WarpIn(0.0f, -16.75f, 125.0f, WindurstWalls);
        Check("in Windurst Walls at Status 4: setPos by the Star Sibyl's tower, the zone-in cutscene 443", $"{WindurstWalls}/443/130",
              $"{c.Zone}/{Cur()}/{MathF.Round(c.Z)}");
        EventEnd(443);
        Check("  Moon Reading complete: rank 10, 100000 gil, the Windurstian Flag, Vestal Chamberlain",
              $"65535,0/10/{gilW + 100000}/1/True/True", $"{Mission(LogWindurst)}/{Lua("report(player:getRank(player:getNation()))")}/{Gil()}/" +
              $"{Qty(WindurstianFlag)}/{DbTitle(TitleId("VESTAL_CHAMBERLAIN"))}/{DbMissionComplete(LogWindurst, 23)}");

        // ---- Rise of the Zilart ----
        // a Bastokan of rank 6 past 5-2 (a GM's !setrank, !addmission and !completemission), The New Frontier current
        Zilart:
        Console.WriteLine("Zilart: a rank 6 Bastokan with The New Frontier");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[3];
        Fixture(db, PortBastok, PortBastok, 130.6f, 7.5f, -2.2f, 1);
        Login();
        Lua("player:setRank(6) player:addMission(xi.mission.log_id.BASTOK, xi.mission.id.bastok.XARCABARD_LAND_OF_TRUTHS) " +
            "player:completeMission(xi.mission.log_id.BASTOK, xi.mission.id.bastok.XARCABARD_LAND_OF_TRUTHS) " +
            "player:addMission(xi.mission.log_id.ZILART, xi.mission.id.zilart.THE_NEW_FRONTIER)");
        Check("  rank 6, 5-2 complete, The New Frontier current", "6/True/0,0",
              $"{Lua("report(player:getRank(player:getNation()))")}/{DbMissionComplete(LogBastok, 15)}/{Mission(LogZilart)}");
        string ZVar(int mission, string name) => Var($"Mission[{LogZilart}][{mission}]{name}");
        void ToNorg() => Warp(100.0f, -8.5f, -12.0f, Norg);

        Console.WriteLine("Zilart 1 The New Frontier");
        WarpIn(-25.0f, 1.1f, -38.0f, Norg);
        // zoneutils::AfterZoneIn, queued 4 s after the login
        for (int i = 0; i < 8 && Cur() != "1"; i++) Tick(1000);
        Check("in Norg with The New Frontier: afterZoneIn's event 1 (5, 0, 1751, ...)", $"1/5,0,1751", $"{Cur()}/{string.Join(",", Ev().Split(':').Last().Split(',').Take(3))}");
        EventEnd(1, 0, 1);
        Check("  an update (option 1, [ROZ]TalesBeginning 0): updateEvent(79, the Vana'diel time, ...) answered, still in 1", "True/1", $"{Of(0x05C).Count > 0}/{Cur()}");
        EventEnd(1, 0);
        Check("  option 0 the first time: [ROZ]TalesBeginning 1, The New Frontier still current", "1/0,0", $"{Var("[ROZ]TalesBeginning")}/{Mission(LogZilart)}");
        TalkTo("Tales_Beginning");
        Cs("the Tales' Beginning: event 293, option 1", 293, 1);
        Check("  option 1: event 1 again", "1", Cur());
        EventEnd(1, 0);
        int mapNorg = Ki("MAP_OF_NORG");
        Check("  event 1 finished with TalesBeginning set: The New Frontier complete, Welcome t'Norg current, the Map of Norg, the var cleared",
              "4,0/True/10/-", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 0)}/{DbKeyItem(mapNorg)}/{Var("[ROZ]TalesBeginning")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 2 Welcome t'Norg");
        ToNorg();
        TalkTo("_700");
        Check("the Norg door (_700): event 2", "2", Cur());
        EventEnd(2, 0, 1);
        EventEnd(2, 0);
        Check("  option 0: Welcome t'Norg complete, Kazham's Chieftainess current", "6,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 4)}");
        Warp(120.0f, -8.0f, -12.0f, Norg);
        TalkTo("Gilgamesh");
        Cs("Gilgamesh: event 7", 7);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 3 Kazham's Chieftainess");
        Warp(100.0f, -15.0f, -113.0f, Kazham);
        TalkTo("Jakoh_Wahcondalo");
        Cs("Jakoh Wahcondalo: event 114", 114);
        int chamberKey = Ki("SACRIFICIAL_CHAMBER_KEY");
        Check("  Kazham's Chieftainess complete, the Sacrificial Chamber Key, The Temple of Uggalepih current", "8,0/10/True",
              $"{Mission(LogZilart)}/{DbKeyItem(chamberKey)}/{DbMissionComplete(LogZilart, 6)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 4 The Temple of Uggalepih");
        Warp(300.0f, -1.85f, 352.0f, SacrificialChamber);
        WinBattlefield(NpcId("_4j0"), "the Sacrificial Chamber (The Temple of Uggalepih)", false, false);
        Rezone(false);
        Check("32001 finished: Status 1, setPos in the chamber, its zone-in cutscene 7", $"8,1/{SacrificialChamber}/7", $"{Mission(LogZilart)}/{c.Zone}/{Cur()}");
        EventEnd(7, 0, 1);
        EventEnd(7);
        Rezone(false);
        Check("7 finished: Status 2, setPos again, the zone-in cutscene 8", $"{SacrificialChamber}/8", $"{c.Zone}/{Cur()}");
        int darkFragment = Ki("DARK_FRAGMENT");
        EventEnd(8);
        Check("  The Temple of Uggalepih complete: the Dark Fragment, Bearer of the Wisewoman's Hope, the key taken, Headstone Pilgrimage current",
              "10,0/10/True/0/True", $"{Mission(LogZilart)}/{DbKeyItem(darkFragment)}/{DbTitle(TitleId("BEARER_OF_THE_WISEWOMANS_HOPE"))}/" +
              $"{DbKeyItem(chamberKey).Substring(0, 1)}/{DbMissionComplete(LogZilart, 8)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 5 Headstone Pilgrimage");
        // a headstone that gives its fragment outright: event 200 with the fragment, option 1 by it
        void PlainHeadstone(float x, float y, float z, ushort zoneId, string fragment)
        {
            Warp(x, y, z, zoneId);
            int ki = Ki(fragment);
            TalkTo("Cermet_Headstone");
            Check($"the Cermet Headstone in zone {zoneId}: event 200 ({fragment})", $"034:{NpcId("Cermet_Headstone")}:200:{ki}", Ev());
            EventEnd(200, 1);
            Check($"  option 1 by it: the {fragment}", "10", DbKeyItem(ki));
        }
        // a headstone guarded by NMs: event 200 spawns them claimed by the char, their deaths stamp the headstone's
        // cooldown, then 201 gives the fragment
        void GuardedHeadstone(float x, float y, float z, ushort zoneId, string fragment, string zoneName, params string[] nms)
        {
            Warp(x, y, z, zoneId);
            int ki = Ki(fragment);
            var info = zone.ZoneInfoOf(c.Zone);
            var ids = nms.Select(n => MobOf(zoneName, n)).ToList();
            TalkTo("Cermet_Headstone");
            Check($"the Cermet Headstone in zone {zoneId}: event 200 ({fragment})", $"034:{NpcId("Cermet_Headstone")}:200:{ki}", Ev());
            EventEnd(200, 1);
            Check($"  option 1 by it: {string.Join(", ", nms)} spawned, claimed by the char", string.Join("/", ids.Select(_ => $"True,{c.Id}")),
                  string.Join("/", ids.Select(i => $"{info.MobById[i].IsSpawned},{info.MobById[i].OwnerId}")));
            foreach (var id in ids) KillByChar(id);
            Check("  killed: the headstone's cooldown stamped", "True",
                  int.Parse(Lua($"report(GetNPCByID({NpcId("Cermet_Headstone")}):getLocalVar('cooldown'))")) > 0);
            TalkTo("Cermet_Headstone");
            Check("  the headstone within its cooldown: event 201", $"034:{NpcId("Cermet_Headstone")}:201:{ki}", Ev());
            EventEnd(201, 1);
            Check($"  option 1 by it: the {fragment}", "10", DbKeyItem(ki));
        }
        Warp(120.0f, -8.0f, -12.0f, Norg);
        TalkTo("Gilgamesh");
        Cs("Gilgamesh: event 9", 9);
        PlainHeadstone(566.0f, -1.2f, 608.0f, CloisterOfFrost, "ICE_FRAGMENT");
        PlainHeadstone(-174.0f, 37.9f, -508.0f, LaTheine, "WATER_FRAGMENT");
        PlainHeadstone(-114.0f, 10.0f, -220.0f, WesternAltepa, "EARTH_FRAGMENT");
        GuardedHeadstone(-76.0f, -5.3f, -89.0f, BehemothsDominion, "LIGHTNING_FRAGMENT", "BEHEMOTHS_DOMINION", "ANCIENT_WEAPON", "LEGENDARY_WEAPON");
        GuardedHeadstone(-111.0f, -8.8f, 454.0f, CapeTeriggan, "WIND_FRAGMENT", "CAPE_TERIGGAN", "AXESARION_THE_WANDERER");
        Check("  the Wind Fragment's 201 starts Wandering Souls", "True", Lua("report(tostring(player:getQuestStatus(xi.questLog.OUTLANDS, xi.quest.id.outlands.WANDERING_SOULS) == xi.questStatus.QUEST_ACCEPTED))") == "true");
        GuardedHeadstone(230.0f, -1.1f, 280.0f, Zitah, "LIGHT_FRAGMENT", "THE_SANCTUARY_OF_ZITAH", "DOOMED_PILGRIMS");
        Check("  the last but one: Headstone Pilgrimage still current", "10,0", Mission(LogZilart));
        GuardedHeadstone(492.0f, 19.7f, 303.0f, YuhtungaJungle, "FIRE_FRAGMENT", "YUHTUNGA_JUNGLE", "TIPHA", "CARTHI");
        Check("  the seventh fragment: Headstone Pilgrimage complete, Bearer of the Eight Prayers, Through the Quicksand Caves current", "12,0/True/True",
              $"{Mission(LogZilart)}/{DbTitle(TitleId("BEARER_OF_THE_EIGHT_PRAYERS"))}/{DbMissionComplete(LogZilart, 10)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 6 Through the Quicksand Caves");
        Warp(-218.0f, -1.9f, 23.0f, ChamberOfOracles);
        WinBattlefield(NpcId("SC_Entrance"), "the Chamber of Oracles (Through the Quicksand Caves)");
        Check("  Through the Quicksand Caves complete, The Chamber of Oracles current", "14,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 12)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 7 The Chamber of Oracles");
        string[] pedestals = { "Fire", "Darkness", "Earth", "Ice", "Light", "Lightning", "Water", "Wind" };
        int placed = 0;
        foreach (var p in pedestals)
        {
            TalkTo($"Pedestal_of_{p}");
            placed |= p switch { "Fire" => 1, "Darkness" => 2, "Earth" => 4, "Ice" => 8, "Light" => 16, "Lightning" => 32, "Water" => 64, _ => 128 };
            if (placed != 255) Check($"  the Pedestal of {p}: the fragment placed (no event), Status {placed}", $"none/14,{placed}", $"{Cur()}/{Mission(LogZilart)}");
        }
        Check("the eighth pedestal, Status 255: event 1", "1", Cur());
        EventEnd(1);
        Check("  The Chamber of Oracles complete: the fragments taken, the Prismatic Fragment, Lightweaver, Return to Delkfutt's Tower current",
              "16,0/00000000/10/True/True", $"{Mission(LogZilart)}/{string.Concat(new[] { "FIRE", "ICE", "WIND", "EARTH", "LIGHTNING", "WATER", "LIGHT", "DARK" }.Select(f => DbKeyItem(Ki($"{f}_FRAGMENT")).Substring(0, 1)))}/" +
              $"{DbKeyItem(Ki("PRISMATIC_FRAGMENT"))}/{DbTitle(TitleId("LIGHTWEAVER"))}/{DbMissionComplete(LogZilart, 14)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 8 Return to Delkfutt's Tower");
        Warp(20.0f, 3.0f, -56.0f, LowerJeuno);
        TalkTo("Aldo");
        Cs("Aldo: event 104", 104);
        Check("  Option bit 0", "1", ZVar(16, "Option"));
        WarpIn(-520.0f, -6.0f, 20.0f, StellarFulcrum);
        Check("in the Stellar Fulcrum at Status 0: the pre-battle zone-in cutscene 0", $"{StellarFulcrum}/0", $"{c.Zone}/{Cur()}");
        EventEnd(0, 0, 0);
        EventEnd(0);
        Check("  Status 1", "16,1", Mission(LogZilart));
        WinBattlefield(NpcId("_4z0"), "the Stellar Fulcrum (Return to Delkfutt's Tower)", false);
        Check("32001 finished: Status 2, setPos in the Fulcrum, the post-battle zone-in cutscene 17, Destroyer of Antiquity",
              $"16,2/{StellarFulcrum}/17/True", $"{Mission(LogZilart)}/{c.Zone}/{Cur()}/{DbTitle(TitleId("DESTROYER_OF_ANTIQUITY"))}");
        EventEnd(17);
        Check("  Return to Delkfutt's Tower complete, Ro'Maeve current", "18,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 16)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 9 Ro'Maeve");
        Warp(20.0f, 3.0f, -56.0f, LowerJeuno);
        TalkTo("Aldo");
        Cs("Aldo: event 84", 84);
        Check("  Option 1", "1", ZVar(18, "Option"));
        ToNorg();
        TalkTo("_700");
        Cs("the Norg door (_700): event 3, option 0", 3);
        Check("  Ro'Maeve complete, The Temple of Desolation current", "20,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 18)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 10 The Temple of Desolation");
        Warp(0.0f, -14.3f, 48.0f, HallOfTheGods);
        TalkTo("_6z0");
        Cs("the Hall of the Gods' gate (_6z0): event 1", 1);
        Check("  The Temple of Desolation complete: Sealer of the Portal of the Gods, The Hall of the Gods current", "22,0/True/True",
              $"{Mission(LogZilart)}/{DbTitle(TitleId("SEALER_OF_THE_PORTAL_OF_THE_GODS"))}/{DbMissionComplete(LogZilart, 20)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 11 The Hall of the Gods");
        TalkTo("_6z0");
        Check("the gate again: DEPRESSION_A_CLUE (no event)", "none", Cur());
        ToNorg();
        TalkTo("_700");
        Cs("the Norg door: event 169, option 0", 169);
        Check("  The Hall of the Gods complete, The Mithra and the Crystal current", "23,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 22)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 12 The Mithra and the Crystal");
        Warp(20.0f, 3.0f, -56.0f, LowerJeuno);
        TalkTo("Aldo");
        Check("Aldo: event 111 (0, 1, 2, ..., 4352, 4)", $"034:{NpcId("Aldo")}:111:0,1,2,0,0,0,4352,4", Ev());
        EventEnd(111);
        Warp(-1.0f, 8.0f, 77.0f, Rabao);
        TalkTo("Maryoh_Comyujah");
        Check("Maryoh Comyujah: event 81 (247, 1, 0, 708, 5, 1, 279, not declined)", $"034:{NpcId("Maryoh_Comyujah")}:81:247,1,0,708,5,1,279", Ev());
        EventEnd(81, 0);
        Check("  declined (option 0): Status 0, Option 1 (the decline remembered)", "23,0/1", $"{Mission(LogZilart)}/{ZVar(23, "Option")}");
        TalkTo("Maryoh_Comyujah");
        Check("Maryoh Comyujah again: event 81 with the decline (param 8 = 1)", $"034:{NpcId("Maryoh_Comyujah")}:81:247,1,0,708,5,1,279,1", Ev());
        EventEnd(81, 1);
        Check("  option 1: Status 1", "23,1", Mission(LogZilart));
        Warp(-506.0f, 17.4f, -418.0f, QuicksandCaves);
        uint vessel = MobOf("QUICKSAND_CAVES", "ANCIENT_VESSEL");
        var qcInfo = zone.ZoneInfoOf(c.Zone);
        var qm7 = Npc(NpcId("qm7"));
        MoveTo(qm7.X + 1.0f, qm7.Y, qm7.Z);
        Talk(qm7.Id);
        Cs("the ??? (qm7): event 12, option 1 within 1.5 yalms", 12, 1);
        Check("  the Ancient Vessel spawned, claimed by the char", $"True/{c.Id}", $"{qcInfo.MobById[vessel].IsSpawned}/{qcInfo.MobById[vessel].OwnerId}");
        KillByChar(vessel);
        Check("  killed: nmDefeated, its respawn stamp", "1/True",
              $"{Lua("report(player:getLocalVar('Mission[3][23]nmDefeated'))")}/{int.Parse(Lua($"report(GetMobByID({vessel}):getLocalVar('[AncientVessel]Respawn'))")) > 0}");
        int papyrus = Ki("SCRAP_OF_PAPYRUS"), cerulean = Ki("CERULEAN_CRYSTAL");
        MoveTo(qm7.X + 1.0f, qm7.Y, qm7.Z);
        Talk(qm7.Id);
        Cs("the ??? again: event 13, option 1", 13, 1);
        Check("  the Scrap of Papyrus", "10", DbKeyItem(papyrus));
        Warp(-1.0f, 8.0f, 77.0f, Rabao);
        TalkTo("Maryoh_Comyujah");
        Cs("Maryoh Comyujah with the papyrus: event 83", 83);
        Check("  the papyrus for the Cerulean Crystal, Status 2", "0/10/23,2", $"{DbKeyItem(papyrus).Substring(0, 1)}/{DbKeyItem(cerulean)}/{Mission(LogZilart)}");
        Warp(0.0f, -20.7f, 147.0f, HallOfTheGods);
        TalkTo("Shimmering_Circle");
        Cs("the Shimmering Circle with the crystal: event 3", 3);
        Check("  The Mithra and the Crystal complete, The Gate of the Gods current", "24,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 23)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 13 The Gate of the Gods");
        WarpIn(0.0f, -91.0f, -150.0f, RuAunGardens);
        Check("in Ru'Aun Gardens: the zone-in cutscene 51", $"{RuAunGardens}/51", $"{c.Zone}/{Cur()}");
        EventEnd(51);
        Check("  The Gate of the Gods complete, Ark Angels current", "26,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 24)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 14 Ark Angels");
        Warp(-38.0f, -1.6f, -148.0f, ShrineOfRuavitau);
        TalkTo("blank_divine_might");
        Check("the Shrine's blank (divine might): cutscene 53 (917, 1408, 1550)", $"034:{NpcId("blank_divine_might")}:53:917,1408,1550", Ev());
        EventEnd(53);
        Check("  Status 1", "26,1", Mission(LogZilart));
        string[] shards = { "SHARD_OF_APATHY", "SHARD_OF_COWARDICE", "SHARD_OF_ENVY", "SHARD_OF_ARROGANCE", "SHARD_OF_RAGE" };
        for (int i = 0; i < 5; i++)
        {
            Warp(-603.0f, -22.7f, 484.0f, LaLoffAmphitheater);
            WinBattlefield(NpcId($"qm1_{i + 1}"), $"La'Loff Amphitheater (Ark Angels {i + 1})");
            Check($"  Ark Angels {i + 1} won: the {shards[i]}", "10", DbKeyItem(Ki(shards[i])));
            // CBattlefield::Cleanup: the players left ten seconds after onBattlefieldLeave
            for (int k = 0; k < 20 && c.PBattlefield != null; k++) Tick(1000);
            if (i < 4) Check("  Ark Angels still current", "26,1", Mission(LogZilart));
        }
        Check("  the five shards: Ark Angels complete, The Sealed Shrine current", "27,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 26)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 15 The Sealed Shrine");
        ToNorg();
        TalkTo("_700");
        Cs("the Norg door: event 172, option 0", 172);
        Check("  Status 1", "27,1", Mission(LogZilart));
        WarpIn(-38.0f, -1.6f, -148.0f, ShrineOfRuavitau);
        Check("in the Shrine of Ru'Avitau at Status 1: the zone-in cutscene 51", $"{ShrineOfRuavitau}/51", $"{c.Zone}/{Cur()}");
        EventEnd(51);
        Check("  The Sealed Shrine complete, The Celestial Nexus current", "28,0/True", $"{Mission(LogZilart)}/{DbMissionComplete(LogZilart, 27)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Zilart 16 The Celestial Nexus");
        Warp(-580.0f, -8.0f, -32.0f, CelestialNexus);
        var nexusBf = EnterBattlefield(NpcId("_513"), "the Celestial Nexus");
        uint eald = MobOf("THE_CELESTIAL_NEXUS", "EALDNARCHE");
        var cnInfo = zone.ZoneInfoOf(c.Zone);
        var ealdnarche = cnInfo.MobById[eald];
        var exoplates = cnInfo.MobById[eald + 1];
        Check("  Eald'narche and the Exoplates up, both unkillable", "True/True/true/true",
              $"{ealdnarche.IsSpawned}/{exoplates.IsSpawned}/{Lua($"report(tostring(GetMobByID({eald}):getUnkillable()))")}/{Lua($"report(tostring(GetMobByID({eald + 1}):getUnkillable()))")}");
        // the char held on its feet for the fight (setUnkillable: CBattleEntity::addHP keeps it at 1 HP): the Exoplates'
        // phase shifts and its skill list's moves (fTP up to 7.0, a 1122 HP blow seen) and Eald'narche's ancient magic take
        // a lone WAR 75's 1359 HP in every run; a char dead before Phase Shift 3 disengages the Exoplates (no onMobFight,
        // so no shift, and its rest heals it off 1%), failing every check from Phase Shift 3 on
        Lua("player:setUnkillable(true)");
        // the Exoplates' three phase shifts: each at its HP threshold with 600 TP, the next animationSub two seconds on
        int[] thresholds = { 60, 30, 1 };
        for (int p = 0; p < 3; p++)
        {
            FightTo(eald + 1, thresholds[p]);
            // TP for the shift, topped up while it has not shifted and is idle (a TP move of its skill list may
            // spend it first; a top-up while the shift's two-second timer is pending would shift it again)
            string shifted = p == 0 ? "2058" : "2059";
            for (int i = 0; i < 60 && (p < 2 ? Lua($"report(GetMobByID({eald + 1}):getAnimationSub())") != $"{p + 1}" : exoplates.IsAlive); i++)
            {
                bool idle = Lua($"local m = GetMobByID({eald + 1}) report(tostring(m:getCurrentAction() ~= 34 and m:actionQueueEmpty()))") == "true";
                bool done = p < 2 && Lua($"report(GetMobByID({eald + 1}):getMobMod(xi.mobMod.SKILL_LIST))") == shifted;
                if (idle && !done && exoplates.Tp < 600) Lua($"GetMobByID({eald + 1}):setTP(1000)");
                Tick(500);
            }
            if (p < 2) Check($"  the Exoplates at {thresholds[p]}%: Phase Shift {p + 1}, animationSub {p + 1}", $"{p + 1}", Lua($"report(GetMobByID({eald + 1}):getAnimationSub())"));
        }
        Check("  Phase Shift 3 at 1%: setUnkillable(false), setHP(0), the Exoplates dead", "False", exoplates.IsAlive);
        for (int i = 0; i < 30 && exoplates.IsSpawned; i++) Tick(1000);
        Check("  the Exoplates despawned: Eald'narche killable, his shields gone", "False/false/false",
              $"{exoplates.IsSpawned}/{Lua($"report(tostring(GetMobByID({eald}):getUnkillable()))")}/{Lua($"report(tostring(GetMobByID({eald}):hasStatusEffect(xi.effect.PHYSICAL_SHIELD)))")}");
        KillByChar(eald);
        for (int i = 0; i < 10 && Cur() != "32004"; i++) Tick(400);
        Check("Eald'narche (phase 1) dead: the group's death handler starts 32004 with the area", "32004/1", $"{Cur()}/{Ev().Split(':').Last()}");
        EventEnd(32004);
        Tick(1000);
        Tick(1000);
        Tick(1000);
        var ealdTwo = cnInfo.MobById[eald + 2];
        Check("  32004 finished (onEventFinishBattlefield): phase 1 gone, Eald'narche (phase 2) up", "False/True", $"{ealdnarche.IsSpawned}/{ealdTwo.IsSpawned}");
        KillByChar(eald + 2);
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("  phase 2 killed, won: 32001", "2/32001", $"{nexusBf?.Status}/{Cur()}");
        EventEnd(32001);
        Lua("player:setUnkillable(false)");
        Rezone();
        Check("32001 finished: The Celestial Nexus complete, Burier of the Illusion, Awakening current, setPos to the Hall of the Gods",
              $"30,0/True/True/{HallOfTheGods}", $"{Mission(LogZilart)}/{DbTitle(TitleId("BURIER_OF_THE_ILLUSION"))}/{DbMissionComplete(LogZilart, 28)}/{c.Zone}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // Awakening ends in Apocalypse Nigh's Gilgamesh events 232/234 (a Chains of Promathia quest after CoP 8-4): its
        // two flags are driven here
        Console.WriteLine("Zilart 17 Awakening (its two cutscene flags)");
        WarpIn(100.0f, -8.5f, -12.0f, Norg);
        Check("in Norg with Awakening: the zone-in cutscene 176", $"{Norg}/176", $"{c.Zone}/{Cur()}");
        EventEnd(176);
        Check("  Status bit 0", "30,1", Mission(LogZilart));
        Warp(36.0f, -1.6f, -16.0f, LowerJeuno);
        TalkTo("_6tc");
        Cs("Lower Jeuno's _6tc: event 20", 20);
        Check("  Status bits 0 and 1 (3): Awakening waits on Apocalypse Nigh", "30,3", Mission(LogZilart));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
    }
}
