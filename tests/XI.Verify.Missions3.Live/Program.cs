using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Missions3.Live -- live gate for the remaining mission logs driven end to end as a client drives them,
// through the mission interaction container (xi.mission, scripts/missions/wotg, soa, rov, acp, amk, asa, tvr): Wings
// of the Goddess, Seekers of Adoulin, Rhapsodies of Vana'diel and the add-on scenarios. The 0x00A login and its
// zone-in cutscenes, 0x05B event ends and updates, 0x05C EVENTENDXZY (a battlefield's arena asked for), 0x015 moves,
// 0x01A Talk and Attack, 0x036 trades and 0x05E zone lines, and every step's effect: the mission log
// (chars.missions), the mission vars (char_vars), key items, items, gil, titles, teleports and the zone changes. The
// real scripts on the real MariaDB (xidb), chars 996031-996039. The walks between places the missions do not script
// are GM !pos warps, and the Crystal War quests that gate Wings of the Goddess are GM !completequest, as the
// missions' headers give. The Voracious Resurgence's mission files (and A Shantotto Ascension's 6 to 15, A Crystalline
// Prophecy's 2 to 12 mission files) are TODO stubs: nothing of them to drive.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;
    private static readonly List<string> _log = new List<string>();

    private static uint CharId = 996031;
    private static readonly uint[] CharIds = { 996031, 996032, 996033, 996034, 996035, 996036 };
    private const ushort BatalliaDowns = 105, RolanberryFields = 110, SauromugueChampaign = 120, BatalliaDownsS = 84, RolanberryFieldsS = 91,
                         SauromugueChampaignS = 98, SouthernSandoriaS = 80, EastRonfaureS = 81, JugnerForestS = 82, LaVauleS = 85,
                         SouthernSandoria = 230, UpperJeuno = 244, JugnerForest = 104, BastokMarketsS = 87, WindurstWatersS = 94,
                         BeaucedineGlacierS = 136, XarcabardS = 137, Chateau = 233, EastRonfaure = 101, CastleZvahlBaileysS = 138,
                         ThroneRoomS = 156, GraubergS = 89, WalkOfEchoes = 182, VunkerlInletS = 83, NorthGustabergS = 88;
    private const ushort LowerJeuno = 245, NorthernSandoria = 231, DangrufWadi = 191, CeizakBattlegrounds = 261, WesternAdoulin = 256,
                         EasternAdoulin = 257, CirdasCaverns = 270, YorciaWeald = 263, RalaWaterways = 258, CelenniaMemorialLibrary = 284,
                         RalaWaterwaysU = 259, ForetDeHennetiel = 262, KamihrDrifts = 267,
                         MountKamihr = 282, WohGates = 273, OuterRaKaznar = 274, Leafallia = 281;
    private const ushort LynxMeat = 5667, GoldBeastcoin = 748, NyumomoDoll = 1706, CernunnosBulb = 2728, JeunoanFlag = 184, AnglersCassoulet = 5704,
                         GnoleClaw = 2521, Lilac = 956, SquareOfFoulard = 2705, OxbloodOrb = 2743, Elixir = 4145, PunchBug = 2977,
                         RiceVinegar = 1652, GroundWasabi = 5164, HolyBasil = 1590, MoonshadeEarring = 11697;
    private const int LogWotg = 5, LogSoa = 12, LogRov = 13, LogAcp = 9, LogAmk = 10, LogAsa = 11, LogTvr = 14;

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
            BoundValue.Of(CharId), BoundValue.Of($"missionsthree{CharId % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of($"Missionthree{CharId % 100}"), BoundValue.Of(zoneId), BoundValue.Of(prevZone),
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

        // Wings of the Goddess: a WAR 75 San d'Orian by Batallia Downs' Cavernous Maw, with the quiz's three items and a
        // Cernunnos Bulb
        Fixture(db, BatalliaDowns, BatalliaDowns, -48.0f, 0.1f, 433.0f, 0, (LynxMeat, 1), (GoldBeastcoin, 1), (NyumomoDoll, 1), (CernunnosBulb, 1),
                (AnglersCassoulet, 1), (GnoleClaw, 1), (Lilac, 1), (PunchBug, 1), (RiceVinegar, 1), (GroundWasabi, 1), (HolyBasil, 1));

        var log = _log;
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
        // the scripts' wall clock (GetSystemTime) moved on by the seconds the games want
        long unixOffset = 0;
        scripts.UnixNowMs = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + unixOffset * 1000;
        // the scripts' Vana'diel hour (VanadielHour, Ploh Trishbahk's dinner time) on the host's test clock
        scripts.VanaTicksNow = () => vanaTicks;
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        // the scripts' Vana'diel clock (VanadielUniqueDay, the wall clock as CVanaTime's) on the host's test clock
        lua.SetGlobal("VanadielUniqueDay", l => { l.Push((double)VanaTime.CountDays(vanaTicks)); return 1; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_missions3");
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

        uint MobOf(string zoneName, string name) => uint.Parse(Lua($"report(zones[xi.zone.{zoneName}].mob.{name})"));
        // a Crystal War quest completed (the GM !completequest the WotG headers give)
        void CompleteCw(string quest) => Lua($"player:completeQuest(xi.questLog.CRYSTAL_WAR, xi.quest.id.crystalWar.{quest})");
        string GVar(int mission, string name) => Var($"Mission[{LogWotg}][{mission}]{name}");
        bool HasTeleport(string type, int bit) => Lua($"report(tostring(player:hasTeleport(xi.teleport.type.{type}, {bit})))") == "true";
        // the event in progress finished with option 0, whatever it is (an NPC's own default)
        void EndAny()
        {
            if (c.IsInEvent) EventEnd(int.Parse(Cur()));
        }
        // the 0x05C an update sent: its eight params, the trailing zeros trimmed
        string Upd()
        {
            var up = Of(0x05C);
            if (up.Count == 0) return "none";
            var pars = Enumerable.Range(0, 8).Select(i => BitConverter.ToUInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(i)).ToString()).ToList();
            while (pars.Count > 0 && pars[^1] == "0") pars.RemoveAt(pars.Count - 1);
            return string.Join(",", pars);
        }

        // ---- Wings of the Goddess ----
        Console.WriteLine("Wings of the Goddess: the login in Batallia Downs");
        Login();
        Check("in Batallia Downs, out of any event", $"{BatalliaDowns}/none", $"{c.Zone}/{Cur()}");
        Check("the WotG log at Cavernous Maws (the CCharEntity default 0)", "0,0", Mission(LogWotg));
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 1 Cavernous Maws");
        TalkTo("Cavernous_Maw");
        Check("the Maw: the mission's event 500 with the maw's index 0", $"034:{NpcId("Cavernous_Maw")}:500", Ev());
        EventEnd(500);
        Rezone();
        Check("  Cavernous Maws complete: the Pure White Feather, Back to the Beginning current", "1,0/10/True",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("PURE_WHITE_FEATHER"))}/{DbMissionComplete(LogWotg, 0)}");
        // gotoRandomMaw: one of the three past maws (math.randomInt), its PAST_MAW bit set
        int mawBit = c.Zone switch { BatalliaDownsS => 0, RolanberryFieldsS => 1, SauromugueChampaignS => 2, _ => -1 };
        Check("  sent to a random past maw (Batallia, Rolanberry or Sauromugue [S]) with its teleport bit", "True/True",
              $"{mawBit >= 0}/{mawBit >= 0 && HasTeleport("PAST_MAW", mawBit)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 2 Back to the Beginning");
        ushort pastZone = c.Zone;
        TalkTo("Cavernous_Maw");
        Check("the past maw before a Crystal War quest: the maw's own warp offer, not the mission's 701", "True", Cur() != "701" && Cur() != "none");
        EndAny();
        CompleteCw("CLAWS_OF_THE_GRIFFON");
        TalkTo("Cavernous_Maw");
        Check("Claws of the Griffon done: the mission's event 701 with the maw's index and 1", $"034:{NpcId("Cavernous_Maw")}:701:{mawBit},1", Ev());
        EventEnd(701);
        Rezone();
        ushort presentZone = pastZone switch { BatalliaDownsS => BatalliaDowns, RolanberryFieldsS => RolanberryFields, _ => SauromugueChampaign };
        Check("  Back to the Beginning complete: the Lightsworm, Cait Sith current, back to the present maw", $"2,0/10/{presentZone}",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("LIGHTSWORM"))}/{c.Zone}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 3 Cait Sith");
        Warp(320.0f, -60.0f, 500.0f, EastRonfaureS);
        Check("in East Ronfaure [S]", EastRonfaureS, c.Zone);
        TakeZoneLine(LineTo(SouthernSandoriaS), false);
        Check("into Southern San d'Oria [S] from East Ronfaure [S]: the zone-in cutscene 67", $"{SouthernSandoriaS}/67", $"{c.Zone}/{Cur()}");
        EventEnd(67);
        Check("  Cait Sith complete: Cait Sith's Assistant, The Queen of the Dance current", "3,0/True",
              $"{Mission(LogWotg)}/{DbTitle(TitleId("CAIT_SITHS_ASSISTANT"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 4 The Queen of the Dance");
        TalkTo("Lion_Springs");
        Check("Lion Springs before a second Crystal War quest: not the mission's 68", "True", Cur() != "68");
        EndAny();
        CompleteCw("BURDEN_OF_SUSPICION");
        TalkTo("Lion_Springs");
        Check("Burden of Suspicion done: Lion Springs' event 68 with the caps' params",
              $"034:{NpcId("Lion_Springs")}:68:80,4224267,1756,{uint.MaxValue - 1540096},{uint.MaxValue - 1239549952},427798150,3,4095", Ev());
        EventEnd(68);
        Check("  Status 1", "3,1", Mission(LogWotg));
        TalkTo("Lion_Springs");
        Cs("Lion Springs at Status 1: event 69", 69);
        Warp(-58.7f, 0.0f, 102.0f, UpperJeuno);
        TalkTo("Turlough");
        Cs("Turlough: event 10172", 10172);
        Check("  the Mayakov Show Ticket, Status 2", "3,2/10", $"{Mission(LogWotg)}/{DbKeyItem(Ki("MAYAKOV_SHOW_TICKET"))}");
        Warp(96.0f, 0.0f, 104.0f, SouthernSandoriaS);
        TalkTo("Lion_Springs");
        Check("Lion Springs with the ticket: event 70", $"034:{NpcId("Lion_Springs")}:70:1,0,2964,0,66453367,8366690,4095,131140", Ev());
        EventEnd(70);
        Rezone(false);
        Check("  Status 3, set back into the zone: the zone-in cutscene 152", $"3,3/{SouthernSandoriaS}/152", $"{Mission(LogWotg)}/{c.Zone}/{Cur()}");
        EventEnd(152, 1, 1);
        Check("  152's update (option 1): updateEvent(1, 0, 1756, 0, 66453367, 8366690, 4095, 131140)",
              "1,0,1756,0,66453367,8366690,4095,131140", Upd());
        EventEnd(152);
        Rezone(false);
        Check("  Status 4, set back again: the zone-in cutscene 153", "3,4/153", $"{Mission(LogWotg)}/{Cur()}");
        EventEnd(153, 1, 1);
        Check("  153's update: updateEvent(1, 0, 1756, 0, 66451386, 11124900, 4095, 131140)",
              "1,0,1756,0,66451386,11124900,4095,131140", Upd());
        EventEnd(153);
        Check("  The Queen of the Dance complete, the ticket taken, While the Cat is Away current", "4,0/00",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("MAYAKOV_SHOW_TICKET"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 5 While the Cat is Away");
        TakeZoneLine(LineTo(EastRonfaureS), false);
        Check("into East Ronfaure [S] from Southern San d'Oria [S]: the zone-in cutscene 7", $"{EastRonfaureS}/7", $"{c.Zone}/{Cur()}");
        EventEnd(7, 0, 1);
        Check("  7's update: the nations' quest flags (San d'Oria's done) and RoV's", "1,0,0,0,0,7733257", Upd());
        EventEnd(7);
        Check("  While the Cat is Away complete, A Timeswept Butterfly current", "5,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 6 A Timeswept Butterfly");
        Warp(-116.0f, -8.0f, -520.0f, JugnerForestS);
        TakeZoneLine(LineTo(LaVauleS), false);
        Check("into La Vaule [S] from Jugner Forest [S]: the zone-in cutscene 1", $"{LaVauleS}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Check("  A Timeswept Butterfly complete, Purple, The New Black current", "6,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 7 Purple, The New Black");
        Warp(-114.4f, -3.6f, -178.0f, LaVauleS);
        TalkTo("_2d1");
        Check("the Reinforced Gateway: event 2", $"034:{NpcId("_2d1")}:2:85,3456,{uint.MaxValue - 207616},1525,0,19550816", Ev());
        EventEnd(2);
        Check("  Status 1", "6,1", Mission(LogWotg));
        WinBattlefield(NpcId("_2d1"), "Purple, The New Black (Galarhigg)", false);
        Check("  won: Status 2, set to La Vaule [S]'s west: the zone-in cutscene 6", $"6,2/{LaVauleS}/6", $"{Mission(LogWotg)}/{c.Zone}/{Cur()}");
        EventEnd(6);
        Check("  Purple, The New Black complete, In the Name of the Father current", "7,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 8 In the Name of the Father");
        Warp(96.0f, 0.0f, 104.0f, SouthernSandoriaS);
        CompleteCw("FIRE_IN_THE_HOLE");
        TalkTo("Lion_Springs");
        Check("Lion Springs: event 85 with the campaign allegiance (none: 0)", $"034:{NpcId("Lion_Springs")}:85:0,13,0,1,3,4,8,3", Ev());
        EventEnd(85);
        Check("  In the Name of the Father complete, Dancers in Distress current", "8,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 9 Dancers in Distress");
        TalkTo("Lion_Springs");
        Check("Lion Springs: CONCERNED_FOR_WOUNDED", $"none/{Text(SouthernSandoriaS, "CONCERNED_FOR_WOUNDED")}:0", $"{Cur()}/{Special()}");
        TalkTo("Raustigne");
        Check("Raustigne: event 86", $"034:{NpcId("Raustigne")}:86:0,10,1,1,3,4,8,3", Ev());
        EventEnd(86);
        Check("  Status 1", "8,1", Mission(LogWotg));
        Warp(46.4f, 0.1f, 316.0f, JugnerForestS);
        TalkTo("Elegant_Footprints");
        Check("the Elegant Footprints: the quiz, event 2", "2", Cur());
        for (uint page = 1; page <= 18; page += 3) EventEnd(2, page, 1);
        EventEnd(2, 19, 1);
        var quizUpd = Upd();
        int choice = int.Parse(GVar(8, "Option"));
        Check("  the quiz's end (option 19): the random item (1-3) kept in Option and sent as the update's eighth param",
              $"True/0,0,0,0,0,0,0,{choice}", $"{choice >= 1 && choice <= 3}/{quizUpd}");
        EventEnd(2);
        Check("  Status 2, a zone required (mustZone: the zone id)", $"8,2/{JugnerForestS}", $"{Mission(LogWotg)}/{GVar(8, "mustZone")}");
        ushort[] quizItems = { LynxMeat, GoldBeastcoin, NyumomoDoll };
        TradeTo("Elegant_Footprints", (Slot(quizItems[choice - 1]), 1));
        Check("  the item traded before a zone: refused (getMustZone), kept", "none/1", $"{Cur()}/{Qty(quizItems[choice - 1])}");
        TalkTo("Elegant_Footprints");
        Check("  the Footprints: LILISETTE_IS_PREPARING with the item", $"{Text(JugnerForestS, "LILISETTE_IS_PREPARING")}:{quizItems[choice - 1]}", Special());
        // mustZone clears on a zone into another zone (xi.player.onGameIn's ]mustZone sweep keeps the same zone's)
        Warp(46.4f, 0.1f, 316.0f, JugnerForest);
        Warp(46.4f, 0.1f, 316.0f, JugnerForestS);
        TradeTo("Elegant_Footprints", (Slot(quizItems[choice - 1]), 1));
        Check("  zoned, the item traded: event 3 with the choice", $"034:{NpcId("Elegant_Footprints")}:3:0,0,0,0,0,0,0,{choice}", Ev());
        EventEnd(3);
        Check("  Dancers in Distress complete, the item taken, Daughter of a Knight current", "9,0/0", $"{Mission(LogWotg)}/{Qty(quizItems[choice - 1])}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 10 Daughter of a Knight");
        Warp(-84.4f, -6.9f, 89.0f, SouthernSandoria);
        TalkTo("Amaura");
        Cs("Amaura: event 935", 935);
        Check("  Status 1", "9,1", Mission(LogWotg));
        TalkTo("Amaura");
        Cs("Amaura at Status 1: event 936 (once: Option 1)", 936);
        TalkTo("Amaura");
        Check("  again: not 936", "1/False", $"{GVar(9, "Option")}/{Cur() == "936"}");
        EndAny();
        TradeTo("Amaura", (Slot(CernunnosBulb), 1));
        Cs("the Cernunnos Bulb traded: event 937", 937);
        Check("  Status 2, the bulb kept", "9,2/1", $"{Mission(LogWotg)}/{Qty(CernunnosBulb)}");
        Warp(-510.5f, 7.6f, 287.0f, JugnerForestS);
        TalkTo("Humus-rich_Earth");
        Check("the past Humus-rich Earth: IDEAL_PLACE_TO_PLANT_ITEM", $"{Text(JugnerForestS, "IDEAL_PLACE_TO_PLANT_ITEM")}:{CernunnosBulb}", Special());
        TradeTo("Humus-rich_Earth", (Slot(CernunnosBulb), 1));
        Check("  the bulb planted: YOU_PLANT_ITEM, the bulb taken, Status 3",
              $"{Text(JugnerForestS, "YOU_PLANT_ITEM")}:{CernunnosBulb}/0/9,3", $"{Special()}/{Qty(CernunnosBulb)}/{Mission(LogWotg)}");
        TalkTo("Humus-rich_Earth");
        Check("  then: ITEM_IS_PLANTED_HERE", $"{Text(JugnerForestS, "ITEM_IS_PLANTED_HERE")}:{CernunnosBulb}", Special());
        Warp(-510.5f, 7.6f, 287.0f, JugnerForest);
        TalkTo("Humus-rich_Earth");
        Check("the present Humus-rich Earth: event 33", $"034:{NpcId("Humus-rich_Earth")}:33:104,0,0,53", Ev());
        EventEnd(33);
        Check("  Status 4", "9,4", Mission(LogWotg));
        TalkTo("Humus-rich_Earth");
        uint cernunnos = MobOf("JUGNER_FOREST", "CERNUNNOS");
        var jfInfo = zone.ZoneInfoOf(c.Zone);
        Check("  again: Cernunnos spawned and claimed, DRAWN_UNWANTED_ATTENTION",
              $"True/{c.Id}/{Text(JugnerForest, "DRAWN_UNWANTED_ATTENTION")}:0", $"{jfInfo.MobById[cernunnos].IsSpawned}/{jfInfo.MobById[cernunnos].OwnerId}/{Special()}");
        TalkTo("Humus-rich_Earth");
        Check("  while it is up: SENSE_UNUSUAL_PRESENCE", $"{Text(JugnerForest, "SENSE_UNUSUAL_PRESENCE")}:0", Special());
        KillByChar(cernunnos);
        Check("  Cernunnos killed: the cernunnosDefeated local var", "1", Lua("report(player:getLocalVar('cernunnosDefeated'))"));
        TalkTo("Humus-rich_Earth");
        Check("  the Earth: event 34", $"034:{NpcId("Humus-rich_Earth")}:34:104,300,200,100,0,1648", Ev());
        EventEnd(34);
        Check("  the Cernunnos Resin, Status 5", "10/9,5", $"{DbKeyItem(Ki("CERNUNNOS_RESIN"))}/{Mission(LogWotg)}");
        TalkTo("Humus-rich_Earth");
        Check("  then: DELIVER_TO_AMAURE", $"{Text(JugnerForest, "DELIVER_TO_AMAURE")}:0", Special());
        Warp(-84.4f, -6.9f, 89.0f, SouthernSandoria);
        TalkTo("Amaura");
        Check("Amaura with the resin: event 939", $"034:{NpcId("Amaura")}:939:0,0,0,31,0,0,1", Ev());
        EventEnd(939);
        Check("  the resin taken, Status 6, the Timer the next Vana'diel day", $"00/9,6/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{DbKeyItem(Ki("CERNUNNOS_RESIN"))}/{Mission(LogWotg)}/{GVar(9, "Timer")}");
        TalkTo("Amaura");
        Cs("Amaura before the day and a zone: event 940", 940);
        TalkTo("Amaura");
        Check("  again in the zone: not 940 (amauraOption)", "False", Cur() == "940");
        EndAny();
        vanaTicks += VanaTime.DAY;
        Warp(-84.4f, -6.9f, 89.0f, SouthernSandoriaS);
        Warp(-84.4f, -6.9f, 89.0f, SouthernSandoria);
        TalkTo("Amaura");
        Cs("the next day, zoned: event 941", 941);
        Check("  Daughter of a Knight complete: the Bottle of Treant Tonic, A Spoonful of Sugar current", "10,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("BOTTLE_OF_TREANT_TONIC"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 11 A Spoonful of Sugar");
        Warp(4.0f, -2.0f, 42.5f, SouthernSandoriaS);
        TalkTo("Raustigne");
        Check("Raustigne: event 87", $"034:{NpcId("Raustigne")}:87:0,7,0,0,0,0,3,4095", Ev());
        EventEnd(87);
        Check("  A Spoonful of Sugar complete, Affairs of State current", "11,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 12 Affairs of State (Bastok first)");
        Warp(-205.3f, -8.0f, 25.0f, BastokMarketsS);
        TalkTo("Radford");
        Check("Radford: event 175", $"034:{NpcId("Radford")}:175:2,27,0,0,0,0,1,4095", Ev());
        EventEnd(175);
        Check("  Status 1, WindurstFirst unset", "11,1/-", $"{Mission(LogWotg)}/{GVar(11, "WindurstFirst")}");
        TalkTo("Radford");
        Check("Radford again (Bastok first): the reminder 177", $"034:{NpcId("Radford")}:177:87,55,0,0,0,0,1,4095", Ev());
        EventEnd(177);
        Check("  still Status 1", "11,1", Mission(LogWotg));
        Warp(138.6f, -2.1f, 60.0f, WindurstWatersS);
        TalkTo("Velda-Galda");
        Check("Velda-Galda second: event 179 (the letter handed over)", $"034:{NpcId("Velda-Galda")}:179:0,0,1", Ev());
        EventEnd(179);
        Check("  Affairs of State complete: Count Borel's Letter, Borne by the Wind current", "12,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("COUNT_BORELS_LETTER"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 13 Borne by the Wind");
        TalkTo("Velda-Galda");
        Cs("Velda-Galda: event 180", 180);
        Check("  no progress", "12,0", Mission(LogWotg));
        Warp(-447.2f, -1.8f, 340.0f, SauromugueChampaignS);
        TalkTo("Bulwark_Gate");
        Check("the Bulwark Gate: event 5", $"034:{NpcId("Bulwark_Gate")}:5:98,0,2963,0,0,0,1", Ev());
        EventEnd(5);
        Check("  Borne by the Wind complete: the Underpass Hatch Key, A Nation on the Brink current", "13,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("UNDERPASS_HATCH_KEY"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 14 A Nation on the Brink");
        TalkTo("Bulwark_Gate");
        Check("the Bulwark Gate with the key: SURRENDER_CEREMONY_HASTE", $"none/{Text(SauromugueChampaignS, "SURRENDER_CEREMONY_HASTE")}:0", $"{Cur()}/{Special()}");
        TalkTo("Bulwark_Gate");
        Check("  once per zone: then the gate's own", "False", Special().StartsWith(Text(SauromugueChampaignS, "SURRENDER_CEREMONY_HASTE") + ":"));
        EndAny();
        Warp(314.1f, -1.2f, -183.0f, BatalliaDownsS);
        TalkTo("Underpass_Hatch");
        Check("the Underpass Hatch: event 3", $"034:{NpcId("Underpass_Hatch")}:3:24,5,0,120", Ev());
        EventEnd(3);
        Check("  Status 1", "13,1", Mission(LogWotg));
        // Everbloom Hollow's event 10000 (the instance cleared) is the script's assumption, the instance is not
        // scripted (scripts/zones/Everbloom_Hollow/instances has only Doomvoid): Status 2 is a GM's
        Lua("player:setMissionStatus(xi.mission.log_id.WOTG, 2)");
        WarpIn(302.7f, -1.0f, -174.4f, BatalliaDownsS);
        Check("Status 2 (GM): into Batallia Downs [S], the zone-in cutscene 4", $"{BatalliaDownsS}/4", $"{c.Zone}/{Cur()}");
        EventEnd(4, 1, 1);
        Check("  4's update: updateEvent(allegiance 0, 0, 1756)", "0,0,1756", Upd());
        EventEnd(4);
        Rezone(false);
        Check("  Status 3, set back: 20", "13,3/20", $"{Mission(LogWotg)}/{Cur()}");
        EventEnd(20);
        Rezone(false);
        Check("  Status 4: 21", "13,4/21", $"{Mission(LogWotg)}/{Cur()}");
        EventEnd(21);
        Rezone(false);
        Check("  Status 5: 22", "13,5/22", $"{Mission(LogWotg)}/{Cur()}");
        EventEnd(22);
        Check("  A Nation on the Brink complete: Battle of Jeuno Veteran, a Jeunoan Flag, Crossroads of Time current",
              "14,0/True/1/-", $"{Mission(LogWotg)}/{DbTitle(TitleId("BATTLE_OF_JEUNO_VETERAN"))}/{Qty(JeunoanFlag)}/{GVar(13, "Unclaimed")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 15 Crossroads of Time");
        Warp(320.0f, -60.0f, 500.0f, EastRonfaureS);
        TakeZoneLine(LineTo(SouthernSandoriaS));
        Check("from East Ronfaure [S] before a third Crystal War quest: no zone-in cutscene", "14,0", Mission(LogWotg));
        CompleteCw("HONOR_UNDER_FIRE");
        Warp(320.0f, -60.0f, 500.0f, EastRonfaureS);
        TakeZoneLine(LineTo(SouthernSandoriaS), false);
        Check("Honor Under Fire done: the zone-in cutscene 145", $"{SouthernSandoriaS}/145", $"{c.Zone}/{Cur()}");
        EventEnd(145);
        Check("  Crossroads of Time complete, Sandswept Memories current", "15,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 16 Sandswept Memories");
        TalkTo("Lion_Springs");
        Check("Lion Springs: event 146", $"034:{NpcId("Lion_Springs")}:146:0,0,0,0,0,0,0,4095", Ev());
        EventEnd(146);
        Check("  Sandswept Memories complete, Northland Exposure current", "16,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 17 Northland Exposure");
        WarpIn(-24.0f, -60.0f, -74.0f, BeaucedineGlacierS);
        Check("into Beaucedine Glacier [S]: the zone-in cutscene 13", $"{BeaucedineGlacierS}/13", $"{c.Zone}/{Cur()}");
        EventEnd(13);
        Check("  Northland Exposure complete: the Shadow Bug, Traitor in the Midst current", "17,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("SHADOW_BUG"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 18 Traitor in the Midst (the five pawprint games)");
        TalkTo("Regal_Pawprints_1");
        Check("Regal Pawprints 1 before the games: nothing of the mission's", "False", Cur() == "14");
        EndAny();
        for (int paw = 3; paw <= 7; paw++)
        {
            TalkTo($"Regal_Pawprints_{paw}");
            Check($"Regal Pawprints {paw}: the game, event {paw + 5}", $"{paw + 5}", Cur());
            EventEnd(paw + 5, 1, 1);
            if (paw == 3)
            {
                // the game's start time (option 1's update: GetSystemTime) and an end at once: under the 20 s
                EventEnd(8, 1);
                Check("  pawprint 3 ended with option 1 at once: under its 20 seconds, no bit", "-", GVar(17, "Status"));
                TalkTo("Regal_Pawprints_3");
                EventEnd(8, 1, 1);
            }
            // the first three games want 20 seconds between the update and the end (the script's GetSystemTime)
            if (paw <= 5) unixOffset += 20;
            EventEnd(paw + 5, 1);
            Check($"  ended with option 1{(paw <= 5 ? " 20 s on" : "")}: Status bit {paw - 3}", $"{(1 << (paw - 2)) - 1}", GVar(17, "Status"));
        }
        TalkTo("Regal_Pawprints_3");
        Check("  a won game's pawprints: nothing", "False", Cur() == "8");
        EndAny();
        TalkTo("Regal_Pawprints_1");
        Check("Regal Pawprints 1 with Status 31: event 14", $"034:{NpcId("Regal_Pawprints_1")}:14:136,300,200,100,0,9306117", Ev());
        EventEnd(14);
        Check("  Traitor in the Midst complete, Betrayal at Beaucedine current", "18,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 19 Betrayal at Beaucedine");
        TalkTo("Regal_Pawprints_2");
        Check("Regal Pawprints 2: event 15", $"034:{NpcId("Regal_Pawprints_2")}:15:136,300,200,100,0,9306122", Ev());
        EventEnd(15);
        Check("  Status 1", "1", GVar(18, "Status"));
        TalkTo("Regal_Pawprints_2");
        uint halphas = uint.Parse(Lua("report(player:getZone():queryEntitiesByName('Count_Halphas')[1]:getID())"));
        var bgInfo = zone.ZoneInfoOf(c.Zone);
        Check("  again: Count Halphas (queryEntitiesByName) spawned and claimed, UNWANTED_ATTENTION",
              $"True/{c.Id}/{Text(BeaucedineGlacierS, "UNWANTED_ATTENTION")}:0", $"{bgInfo.MobById[halphas].IsSpawned}/{bgInfo.MobById[halphas].OwnerId}/{Special()}");
        TalkTo("Regal_Pawprints_2");
        Check("  while he is up, the char not engaged: UNUSUAL_PRESENCE", $"{Text(BeaucedineGlacierS, "UNUSUAL_PRESENCE")}:0", Special());
        KillByChar(halphas);
        Check("  Count Halphas killed (the mission's onMobDeath): Status 2", "2", GVar(18, "Status"));
        TalkTo("Regal_Pawprints_2");
        Check("  the pawprints: event 16", $"034:{NpcId("Regal_Pawprints_2")}:16:0,300,200,100", Ev());
        EventEnd(16);
        Rezone(false);
        Check("  Status 3, set back into the zone: the zone-in cutscene 30", $"3/{BeaucedineGlacierS}/30", $"{GVar(18, "Status")}/{c.Zone}/{Cur()}");
        EventEnd(30);
        Check("  Betrayal at Beaucedine complete, its vars cleared, On Thin Ice current", "19,0/-", $"{Mission(LogWotg)}/{GVar(18, "Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 20 On Thin Ice");
        Warp(4.0f, -2.0f, 42.5f, SouthernSandoriaS);
        TalkTo("Raustigne");
        Check("Raustigne: event 147", $"034:{NpcId("Raustigne")}:147:0,16,1", Ev());
        EventEnd(147);
        Check("  On Thin Ice complete, Proof of Valor current", "20,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 21 Proof of Valor (the North-bound Petition)");
        int petitionKi = Ki("NORTH_BOUND_PETITION");
        // Boy and the Beast (a Crystal War quest, open since Claws of the Griffon) has Raustigne's progressEvent 55,
        // which outranks the mission's message: its talk first, the mission's handler run all the same (its
        // MUST_GATHER_SIGNATURES and setMustZone are side effects of the handler, not of its returned action)
        TalkTo("Raustigne");
        Check("Raustigne: Boy and the Beast's event 55 outranks the mission's message, whose handler still ran",
              $"55/{Text(SouthernSandoriaS, "MUST_GATHER_SIGNATURES")}:20/{SouthernSandoriaS}", $"{Cur()}/{Special()}/{GVar(20, "mustZone")}");
        EventEnd(55);
        Warp(4.0f, -2.0f, 42.5f, EastRonfaureS);
        Warp(4.0f, -2.0f, 42.5f, SouthernSandoriaS);
        TalkTo("Raustigne");
        Check("  zoned: MUST_GATHER_SIGNATURES, CURRENT_PETITIONS (0), a zone required again",
              $"none/{Text(SouthernSandoriaS, "MUST_GATHER_SIGNATURES")}:20,{Text(SouthernSandoriaS, "CURRENT_PETITIONS")}:0/{SouthernSandoriaS}",
              $"{Cur()}/{Special()}/{GVar(20, "mustZone")}");
        // the ten petitioners of one signature each (event, Status bit)
        (string Npc, int Ev, int Bit)[] petitioners =
        {
            ("Aissaville", 123, 0), ("Illeuse", 115, 1), ("Andagge", 121, 2), ("Daigraffeaux", 118, 3), ("Louxiard", 119, 4),
            ("Machionage", 120, 5), ("Elnonde", 116, 6), ("Loillie", 117, 7), ("Remiotte", 124, 8), ("Mailleronce", 122, 9),
        };
        int sigs = 0, statusBits = 0;
        foreach (var (npc, ev, bit) in petitioners)
        {
            TalkTo(npc);
            Check($"{npc}: event {ev}", $"{ev}", Cur());
            EventEnd(ev);
            sigs++;
            statusBits |= 1 << bit;
            Check($"  a signature: Status bit {bit}, HAVE_GATHERED_SIGNATURE ({sigs})",
                  $"{statusBits}/{sigs}/{Text(SouthernSandoriaS, "HAVE_GATHERED_SIGNATURE")}:{petitionKi}", $"{GVar(20, "Status")}/{GVar(20, "Option")}/{Special()}");
        }
        TalkTo("Aissaville");
        Check("  a signed petitioner: nothing of the mission's", "False", Cur() == "123");
        EndAny();
        // Farouel's quiz: the right options 1, 4, 8, then 9 (3 rounds * 3) asks for the score
        TalkTo("Farouel");
        Check("Farouel: the quiz, event 125", "125", Cur());
        EventEnd(125, 1, 1);
        EventEnd(125, 4, 1);
        EventEnd(125, 8, 1);
        EventEnd(125, 9, 1);
        Check("  three right answers: updateEvent(numCorrect 3 + 3 = 6)", "6", Upd());
        EventEnd(125, 1);
        // the finish reads numCorrect with mission:getVar (a char var) where the rounds kept a local var: the quiz's
        // signatures are 0 (the script's own), its bit set
        Check("  finished: Status bit 10, no signatures (getVar of the local numCorrect)", $"{statusBits | 1 << 10}/{sigs}",
              $"{GVar(20, "Status")}/{GVar(20, "Option")}");
        statusBits |= 1 << 10;
        // Eumielle wants an Angler's Cassoulet after her first talk
        TradeTo("Eumielle", (Slot(AnglersCassoulet), 1));
        Check("Eumielle: the cassoulet before her talk: refused", "none/1", $"{Cur()}/{Qty(AnglersCassoulet)}");
        TalkTo("Eumielle");
        Cs("Eumielle: event 131 (Remind bit 0)", 131);
        TalkTo("Eumielle");
        Cs("  again: event 141", 141);
        TradeTo("Eumielle", (Slot(AnglersCassoulet), 1));
        Cs("  the cassoulet: event 135", 135);
        sigs += 12;
        statusBits |= 1 << 14;
        Check("  12 signatures, Status bit 14, the cassoulet taken", $"{statusBits}/{sigs}/0", $"{GVar(20, "Status")}/{GVar(20, "Option")}/{Qty(AnglersCassoulet)}");
        // Rongelouts N Distaud: a Gnole Claw, option 1: 35 signatures
        TalkTo("Rongelouts_N_Distaud");
        Cs("Rongelouts N Distaud: event 137", 137);
        TalkTo("Rongelouts_N_Distaud");
        Cs("  again: event 143", 143);
        TradeTo("Rongelouts_N_Distaud", (Slot(GnoleClaw), 1));
        Cs("  the Gnole Claw: event 144, option 1", 144, 1);
        sigs += 35;
        statusBits |= 1 << 18;
        Check("  35 signatures, Status bit 18, the claw taken", $"{statusBits}/{sigs}/0", $"{GVar(20, "Status")}/{GVar(20, "Option")}/{Qty(GnoleClaw)}");
        TalkTo("Rongelouts_N_Distaud");
        Cs("  signed: event 150 (once per zone)", 150);
        TalkTo("Raustigne");
        Check("Raustigne with 57 signatures before a zone: nothing (mustZone)", "False", Cur() == "148");
        EndAny();
        Warp(4.0f, -2.0f, 42.5f, EastRonfaureS);
        Warp(4.0f, -2.0f, 42.5f, SouthernSandoriaS);
        TalkTo("Raustigne");
        Check("  zoned: event 148 with the signatures", $"034:{NpcId("Raustigne")}:148:0,{sigs}", Ev());
        EventEnd(148, 0);
        Check("  option 0: a zone required again", $"20,0/{SouthernSandoriaS}", $"{Mission(LogWotg)}/{GVar(20, "mustZone")}");
        Warp(4.0f, -2.0f, 42.5f, EastRonfaureS);
        Warp(4.0f, -2.0f, 42.5f, SouthernSandoriaS);
        TalkTo("Raustigne");
        EventEnd(148, 1);
        // itemRewards walked with pairs(): the first reward of a requirement met (57: the Square of Foulard (41) or the
        // Oxblood Orb (30), by the table's order)
        string reward = Qty(SquareOfFoulard) == "1" ? "foulard" : Qty(OxbloodOrb) == "1" ? "oxblood" : "none";
        Check("  option 1: Proof of Valor complete, a reward of 57 signatures, A Sanguinary Prelude current", "21,0/True",
              $"{Mission(LogWotg)}/{reward != "none"}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 22 A Sanguinary Prelude");
        WarpIn(-24.0f, -60.0f, -74.0f, BeaucedineGlacierS);
        Check("into Beaucedine Glacier [S]: the zone-in cutscene 17", $"{BeaucedineGlacierS}/17", $"{c.Zone}/{Cur()}");
        EventEnd(17);
        Check("  A Sanguinary Prelude complete: the Aroma Bug, Dungeons and Dancers current", "22,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("AROMA_BUG"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 23 Dungeons and Dancers");
        TalkTo("Regal_Pawprints_8");
        Check("Regal Pawprints 8: event 20", $"034:{NpcId("Regal_Pawprints_8")}:20:36,5,1756", Ev());
        EventEnd(20);
        Check("  Status 1", "1", GVar(22, "Status"));
        // Regal Pawprints 1's entry (event 25, the Aroma Bug given) wants the char without the Aroma Bug, which the
        // previous mission's reward gives: never offered
        TalkTo("Regal_Pawprints_1");
        Check("Regal Pawprints 1 with the Aroma Bug: not event 25", "False", Cur() == "25");
        EndAny();
        // Everbloom Hollow's event 10000 is the script's assumption (no instance scripted): Status 2 is a GM's
        Lua("player:setCharVar('Mission[5][22]Status', 2)");
        WarpIn(-148.1f, -61.3f, -176.6f, BeaucedineGlacierS);
        Check("Status 2 (GM): the zone-in cutscene 18", "18", Cur());
        EventEnd(18);
        Check("  Dungeons and Dancers complete: the Umbra Bug, Distorter of Time current", "23,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("UMBRA_BUG"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 24 Distorter of Time");
        TalkTo("Regal_Pawprints_1");
        Check("Regal Pawprints 1 with the Umbra Bug (the previous reward): not event 26", "False", Cur() == "26");
        EndAny();
        // Ruhotz Silvermines' event 10000: no instance scripted, Status 1 is a GM's
        Lua("player:setCharVar('Mission[5][23]Status', 1)");
        WarpIn(51.6f, -41.2f, 98.7f, BeaucedineGlacierS);
        Check("Status 1 (GM): the zone-in cutscene 19", "19", Cur());
        EventEnd(19);
        Check("  Distorter of Time complete, The Will of the World current", "24,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 25 The Will of the World");
        Warp(4.0f, -2.0f, 42.5f, SouthernSandoriaS);
        TalkTo("Raustigne");
        Check("Raustigne: event 149", $"034:{NpcId("Raustigne")}:149:0,5,2964,0,0,0,1,4095", Ev());
        EventEnd(149);
        Check("  The Will of the World complete, Fate in Haze current", "25,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 26 Fate in Haze");
        TalkTo("Lion_Springs");
        Check("Lion Springs before a fourth Crystal War quest: not 151", "False", Cur() == "151");
        EndAny();
        CompleteCw("BLOOD_OF_HEROES");
        TalkTo("Rholont");
        Check("Rholont, allegiance 0: not 656", "False", Cur() == "656");
        EndAny();
        TalkTo("Lion_Springs");
        Check("Blood of Heroes done: Lion Springs' event 151", $"034:{NpcId("Lion_Springs")}:151:0,0,1", Ev());
        EventEnd(151);
        Rezone(false);
        Check("  Status 1, set back: the zone-in cutscene 154", "1/154", $"{GVar(25, "Status")}/{Cur()}");
        EventEnd(154, 1, 1);
        Check("  154's update: updateEvent(0, 0, 1756)", "0,0,1756", Upd());
        EventEnd(154);
        Rezone(false);
        Check("  Status 2: 155", "2/155", $"{GVar(25, "Status")}/{Cur()}");
        EventEnd(155, 1, 1);
        Check("  155's update: updateEvent(0, 0, 1756)", "0,0,1756", Upd());
        EventEnd(155);
        Check("  Fate in Haze complete, The Scent of Battle current", "26,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 27 The Scent of Battle");
        Warp(-447.2f, -1.8f, 340.0f, SauromugueChampaignS);
        TalkTo("Bulwark_Gate");
        Check("the Bulwark Gate: event 9", $"034:{NpcId("Bulwark_Gate")}:9:98,5,0,0,{uint.MaxValue - 1611137536},{uint.MaxValue - 41990201},3,1", Ev());
        EventEnd(9);
        Check("  The Scent of Battle complete, Another World current", "27,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 28 Another World");
        Warp(86.0f, -1.0f, -40.0f, EastRonfaure);
        TakeZoneLine(LineTo(SouthernSandoria), false);
        Check("into Southern San d'Oria from East Ronfaure: the zone-in cutscene 945", $"{SouthernSandoria}/945", $"{c.Zone}/{Cur()}");
        EventEnd(945);
        Check("  Prog 1", "1", GVar(27, "Prog"));
        Warp(2.0f, 0.1f, 2.0f, Chateau);
        TalkTo("Halver");
        Check("Halver: event 566", $"034:{NpcId("Halver")}:566:233,23,1756,0,0,0,1,1", Ev());
        EventEnd(566);
        Check("  Another World complete, A Hawk in Repose current", "28,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 29 A Hawk in Repose");
        Warp(149.7f, -5.1f, -393.0f, BatalliaDowns);
        TradeTo("Weathered_Gravestone", (Slot(Lilac), 1));
        Check("the Lilac before the gravestone's talk: refused", "none/1", $"{Cur()}/{Qty(Lilac)}");
        TalkTo("Weathered_Gravestone");
        Check("the Weathered Gravestone: event 502", $"034:{NpcId("Weathered_Gravestone")}:502:105,2,300000,0,0,0,1,22262", Ev());
        EventEnd(502);
        TradeTo("Weathered_Gravestone", (Slot(Lilac), 1));
        Check("  the Lilac: event 503", $"034:{NpcId("Weathered_Gravestone")}:503:0,23,1753,0,0,0,1,3871", Ev());
        EventEnd(503);
        Check("  A Hawk in Repose complete, the Lilac taken, The Battle of Xarcabard current", "29,0/0", $"{Mission(LogWotg)}/{Qty(Lilac)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 30 The Battle of Xarcabard");
        WarpIn(-106.1f, -25.5f, -50.0f, XarcabardS);
        Check("into Xarcabard [S]: the zone-in cutscene 17", $"{XarcabardS}/17", $"{c.Zone}/{Cur()}");
        EventEnd(17);
        TalkTo("Rally_Point_Red");
        Check("Rally Point: Red: event 18", $"034:{NpcId("Rally_Point_Red")}:18:0,5,0,0,0,0,0,1", Ev());
        EventEnd(18);
        Check("  The Battle of Xarcabard complete, Prelude to a Storm current", "30,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the 0x027 TALKNUMWORK2 messages (messageName) as their message ids
        string MsgNames() => string.Join(",", Of(0x027).Select(p => (BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF).ToString()));
        // the char's look as the scripts' equipment-model updates give it: face, head, body, hands, main, sub
        string LookParams() => Lua("local e = player:getEquipmentModelIds() report(player:getFace()) report(e.head) report(e.body) report(e.hands) report(e.main) report(e.sub)");

        Console.WriteLine("WotG 31 Prelude to a Storm");
        Warp(-106.1f, -25.5f, -50.0f, XarcabardS);
        TalkTo("Rally_Point_Red");
        Check("Rally Point: Red: REQUIRED_TO_DELIVER (messageName), no event", $"none/{Text(XarcabardS, "REQUIRED_TO_DELIVER")}", $"{Cur()}/{MsgNames()}");
        Warp(54.0f, -23.4f, -201.0f, XarcabardS);
        TalkTo("Rally_Point_Green");
        Check("Rally Point: Green: event 21", $"034:{NpcId("Rally_Point_Green")}:21:137,300,200,100,0,6553620", Ev());
        EventEnd(21);
        Check("  the Magelight Signal Flare, Status 1", "10/1", $"{DbKeyItem(Ki("MAGELIGHT_SIGNAL_FLARE"))}/{GVar(30, "Status")}");
        TalkTo("Rally_Point_Green");
        Check("  again with the flare: HELP_FEDERATION_PREPARE", $"none/{Text(XarcabardS, "HELP_FEDERATION_PREPARE")}", $"{Cur()}/{MsgNames()}");
        Lua("player:delKeyItem(xi.keyItem.MAGELIGHT_SIGNAL_FLARE)");
        TalkTo("Rally_Point_Green");
        Check("  the flare lost (spent on the instance's entry): event 25", $"034:{NpcId("Rally_Point_Green")}:25:137,23,2963", Ev());
        EventEnd(25);
        Check("  the flare again", "10", DbKeyItem(Ki("MAGELIGHT_SIGNAL_FLARE")));
        // Ghoyu's Reverie's event 10000 (the instance cleared, Status 2 and the reward tier in Option) is the script's
        // assumption, no instance scripted: both a GM's
        Lua("player:setCharVar('Mission[5][30]Status', 2) player:setCharVar('Mission[5][30]Option', 1)");
        WarpIn(96.8f, -23.9f, -277.9f, XarcabardS);
        Check("Status 2 (GM): into Xarcabard [S], the zone-in cutscene 22", "22", Cur());
        EventEnd(22, 13, 1);
        Check("  22's update (option 13): updateEvent(0, 23, 1756, 0, 0, 0, 0, 2)", "0,23,1756,0,0,0,0,2", Upd());
        EventEnd(22);
        Check("  Prelude to a Storm complete: the reward of Option 1 (an Elixir), Storm's Crescendo current", "31,0/1",
              $"{Mission(LogWotg)}/{Qty(Elixir)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 32 Storm's Crescendo");
        TalkTo("Rally_Point_Green");
        Check("Rally Point: Green: FEDERATION_COMPLETE", $"none/{Text(XarcabardS, "FEDERATION_COMPLETE")}", $"{Cur()}/{MsgNames()}");
        TalkTo("Rally_Point_Blue");
        Check("Rally Point: Blue: event 19", $"034:{NpcId("Rally_Point_Blue")}:19:0,23,1756,400,67108863,659255957,4905", Ev());
        EventEnd(19);
        Check("  the Alchemical Signal Flare, Status 1", "10/1", $"{DbKeyItem(Ki("ALCHEMICAL_SIGNAL_FLARE"))}/{GVar(31, "Status")}");
        TalkTo("Rally_Point_Blue");
        Check("  again with the flare: HELP_REPUBLIC_PREPARE", $"none/{Text(XarcabardS, "HELP_REPUBLIC_PREPARE")}", $"{Cur()}/{MsgNames()}");
        Lua("player:delKeyItem(xi.keyItem.ALCHEMICAL_SIGNAL_FLARE)");
        TalkTo("Rally_Point_Blue");
        Cs("  the flare lost, the Timer passed: event 26", 26);
        Check("  the flare again", "10", DbKeyItem(Ki("ALCHEMICAL_SIGNAL_FLARE")));
        Lua("player:delKeyItem(xi.keyItem.ALCHEMICAL_SIGNAL_FLARE) player:setCharVar('Mission[5][31]Timer', VanadielUniqueDay() + 1)");
        TalkTo("Rally_Point_Blue");
        Check("  the flare lost before the Timer: BASTOKAN_OFFICER_GONE", $"none/{Text(XarcabardS, "BASTOKAN_OFFICER_GONE")}", $"{Cur()}/{MsgNames()}");
        // Ghoyu's Reverie's event 10000: no instance scripted, Status 2 a GM's
        Lua("player:setCharVar('Mission[5][31]Status', 2)");
        WarpIn(138.5f, -16.2f, -30.4f, XarcabardS);
        Check("Status 2 (GM): the zone-in cutscene 20", "20", Cur());
        EventEnd(20);
        Check("  Status 3", "3", GVar(31, "Status"));
        Warp(-106.1f, -25.5f, -50.0f, XarcabardS);
        TalkTo("Rally_Point_Blue");
        Check("Rally Point: Blue at Status 3: COMPLETED_TASKS", $"none/{Text(XarcabardS, "COMPLETED_TASKS")}", $"{Cur()}/{MsgNames()}");
        TalkTo("Rally_Point_Red");
        Check("Rally Point: Red: event 23", $"034:{NpcId("Rally_Point_Red")}:23:137,5", Ev());
        EventEnd(23);
        Rezone(false);
        Check("  Status 4, set back: the zone-in cutscene 39", "4/39", $"{GVar(31, "Status")}/{Cur()}");
        EventEnd(39);
        Check("  Storm's Crescendo complete, Into the Beast's Maw current", "32,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 33 Into the Beast's Maw");
        TalkTo("Rally_Point_Red");
        Check("Rally Point: Red: JOIN_ALLIED_FORCE", $"none/{Text(XarcabardS, "JOIN_ALLIED_FORCE")}:0", $"{Cur()}/{Special()}");
        WarpIn(179.4f, -24.1f, 98.0f, CastleZvahlBaileysS);
        Check("into Castle Zvahl Baileys [S]: the zone-in cutscene 1", $"{CastleZvahlBaileysS}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Check("  Status 1", "1", GVar(32, "Status"));
        TalkTo("Peculiar_Glint");
        Check("the Peculiar Glint: event 2", $"034:{NpcId("Peculiar_Glint")}:2:53,23,1756,0,0,16384001", Ev());
        EventEnd(2);
        Rezone(false);
        Check("  Status 2, set back: the zone-in cutscene 14", "2/14", $"{GVar(32, "Status")}/{Cur()}");
        EventEnd(14, 5, 1);
        Check("  14's update (option 5): updateEvent(138, 23, 1756)", "138,23,1756", Upd());
        EventEnd(14);
        Rezone(false);
        Check("  Status 3: the zone-in cutscene 15", "3/15", $"{GVar(32, "Status")}/{Cur()}");
        EventEnd(15);
        Check("  the Distress Signal Flare, Status 4", "10/4", $"{DbKeyItem(Ki("DISTRESS_SIGNAL_FLARE"))}/{GVar(32, "Status")}");
        TalkTo("Ornate_Block");
        Check("the Ornate Block with the flare: nothing of the mission's", "False", Cur() == "11");
        EndAny();
        Lua("player:delKeyItem(xi.keyItem.DISTRESS_SIGNAL_FLARE)");
        TalkTo("Ornate_Block");
        Check("  the flare lost: event 11", $"034:{NpcId("Ornate_Block")}:11:138", Ev());
        EventEnd(11, 1);
        Check("  option 1: the flare again", "10", DbKeyItem(Ki("DISTRESS_SIGNAL_FLARE")));
        // Ghoyu's Reverie's event 10000: no instance scripted, Status 5 a GM's
        Lua("player:setCharVar('Mission[5][32]Status', 5)");
        WarpIn(180.0f, -24.0f, 94.2f, CastleZvahlBaileysS);
        Check("Status 5 (GM): the zone-in cutscene 3", "3", Cur());
        EventEnd(3);
        Check("  Into the Beast's Maw complete, The Hunter Ensnared current", "33,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 34 The Hunter Ensnared");
        Warp(-106.1f, -25.5f, -50.0f, XarcabardS);
        TalkTo("Rally_Point_Red");
        Check("Rally Point: Red: event 24", $"034:{NpcId("Rally_Point_Red")}:24:137,23,1756,0,65863679,8553890,4095", Ev());
        EventEnd(24);
        Check("  The Hunter Ensnared complete, Flight of the Lion current", "34,0", Mission(LogWotg));

        Console.WriteLine("WotG 35 Flight of the Lion");
        Warp(-447.2f, -1.8f, 340.0f, SauromugueChampaignS);
        TalkTo("Bulwark_Gate");
        Check("the Bulwark Gate: event 10", $"034:{NpcId("Bulwark_Gate")}:10:98,300,200,100,0,9240578", Ev());
        EventEnd(10);
        Check("  Flight of the Lion complete, Fall of the Hawk current", "35,0", Mission(LogWotg));

        Console.WriteLine("WotG 36 Fall of the Hawk");
        WarpIn(179.4f, -24.1f, 98.0f, CastleZvahlBaileysS);
        Check("into Castle Zvahl Baileys [S]: the zone-in cutscene 4", "4", Cur());
        EventEnd(4);
        Check("  Fall of the Hawk complete, Darkness Descends current", "36,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 37 Darkness Descends");
        WarpIn(19.0f, 0.0f, -4.5f, ThroneRoomS);
        Check("into the Throne Room [S]: the zone-in cutscene 8", $"{ThroneRoomS}/8", $"{c.Zone}/{Cur()}");
        EventEnd(8, 5, 1);
        Check("  8's update (option 5): updateEvent(1, 23, 1756, 470, 470, 848)", "1,23,1756,470,470,848", Upd());
        EventEnd(8);
        Check("  Status 1", "1", GVar(36, "Status"));
        // the Darkness Descends battlefield is not scripted (scripts/battlefields has no Throne Room [S]): its 32001
        // (battlefieldWin) never comes, Status 2 a GM's
        Lua("player:setCharVar('Mission[5][36]Status', 2)");
        WarpIn(-90.9f, -5.75f, 0.0f, ThroneRoomS);
        Check("Status 2 (GM): the zone-in cutscene 10", "10", Cur());
        EventEnd(10, 5, 1);
        Check("  10's update: updateEvent(156, the face, the head, body, hands, main and sub models)",
              string.Join(",", ("156," + LookParams()).Split(',').Reverse().SkipWhile(p => p == "0").Reverse()), Upd());
        EventEnd(10);
        Check("  Darkness Descends complete, Adieu, Lilisette current", "37,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 38 Adieu, Lilisette");
        Warp(96.0f, 0.0f, 104.0f, SouthernSandoriaS);
        TalkTo("Lion_Springs");
        Check("Lion Springs before a fifth Crystal War quest: not 166", "False", Cur() == "166");
        EndAny();
        CompleteCw("BONDS_OF_MYTHRIL");
        TalkTo("Lion_Springs");
        Check("Bonds of Mythril done: event 166 with the allegiance (0)", $"034:{NpcId("Lion_Springs")}:166", Ev());
        EventEnd(166);
        Check("  Adieu, Lilisette complete, By the Fading Light current", "38,0", Mission(LogWotg));

        Console.WriteLine("WotG 39 By the Fading Light");
        Warp(-106.1f, -25.5f, -50.0f, XarcabardS);
        TalkTo("Rally_Point_Red");
        Check("Rally Point: Red: event 41", $"034:{NpcId("Rally_Point_Red")}:41:137,0,0,7", Ev());
        EventEnd(41);
        Check("  By the Fading Light complete, Edge of Existence current", "39,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 40 Edge of Existence");
        Warp(-48.0f, 0.1f, 433.0f, BatalliaDowns);
        TalkTo("Cavernous_Maw");
        Check("Batallia Downs' maw: event 3", $"034:{NpcId("Cavernous_Maw")}:3:0,0,0,0,0,0,0,1", Ev());
        EventEnd(3);
        // the finish's xi.maws.goToMaw(player, xi.zone.BATALLIA_DOWNS) passes a zone id where goToMaw wants a maw
        // table (maw.dest): the script's own error, after the mission's complete, caught by the interaction
        // framework's handler pcall ("Error running handler")
        Check("  Edge of Existence complete, Her Memories current; goToMaw's error (a zone id for the maw), the char left in place",
              $"40,0/True/{BatalliaDowns}",
              $"{Mission(LogWotg)}/{log.Any(l => l.Contains("Error running handler") && l.Contains("attempt to index local 'maw' (a number value)"))}/{c.Zone}");
        Tick(400);

        Console.WriteLine("WotG 41 Her Memories (Operation Cupid, then Azure Footfalls with all four fragments)");
        // the first two fragments from their quests (Malign Maladies, Carnelian Footfalls / Homecoming Queen) a GM's
        Lua("player:addKeyItem(xi.keyItem.LARGE_MEMORY_FRAGMENT1) player:addKeyItem(xi.keyItem.LARGE_MEMORY_FRAGMENT2)");
        Warp(-116.0f, -8.0f, -520.0f, JugnerForestS);
        TakeZoneLine(LineTo(BatalliaDownsS), false);
        Check("Operation Cupid: into Batallia Downs [S] from Jugner Forest [S], the zone-in cutscene 23", $"{BatalliaDownsS}/23", $"{c.Zone}/{Cur()}");
        EventEnd(23);
        Check("  the quest accepted", "1", Lua("report(player:getQuestStatus(xi.questLog.CRYSTAL_WAR, xi.quest.id.crystalWar.HER_MEMORIES_OPERATION_CUPID))"));
        Warp(-447.2f, -1.8f, 340.0f, SauromugueChampaignS);
        TalkTo("Bulwark_Gate");
        Cs("  the Bulwark Gate: event 11", 11);
        TalkTo("Bulwark_Gate");
        Cs("  again: event 12 (once per zone)", 12);
        Warp(206.0f, -32.0f, 314.0f, VunkerlInletS);
        TradeTo("Leadavox", (Slot(RiceVinegar), 1), (Slot(GroundWasabi), 1), (Slot(HolyBasil), 1));
        Cs("  Leadavox, the vinegar, wasabi and basil: event 4", 4);
        Check("  the Pot of Martial Relish, the three taken", "10/0/0/0",
              $"{DbKeyItem(Ki("POT_OF_MARTIAL_RELISH"))}/{Qty(RiceVinegar)}/{Qty(GroundWasabi)}/{Qty(HolyBasil)}");
        Warp(-447.2f, -1.8f, 340.0f, SauromugueChampaignS);
        TalkTo("Bulwark_Gate");
        Cs("  the Bulwark Gate with the relish: event 13", 13);
        Check("  the relish taken", "00", DbKeyItem(Ki("POT_OF_MARTIAL_RELISH")));
        Warp(-196.5f, 8.0f, 361.0f, RolanberryFieldsS);
        TakeZoneLine(LineTo(BatalliaDownsS), false);
        Check("  into Batallia Downs [S] from Rolanberry Fields [S]: 24", "24", Cur());
        EventEnd(24);
        Check("  Operation Cupid complete: the third fragment, Her Memories still current (three of four)", "10/40,0",
              $"{DbKeyItem(Ki("LARGE_MEMORY_FRAGMENT3"))}/{Mission(LogWotg)}");
        // Azure Footfalls wants the Bastok allegiance
        Lua("player:setCampaignAllegiance(2)");
        Warp(-400.0f, 40.0f, -90.4f, NorthGustabergS);
        TakeZoneLine(LineTo(BastokMarketsS), false);
        Check("Azure Footfalls: into Bastok Markets [S] from North Gustaberg [S], the zone-in cutscene 358", $"{BastokMarketsS}/358", $"{c.Zone}/{Cur()}");
        EventEnd(358);
        TalkTo("Adelbrecht");
        Cs("  Adelbrecht: event 359 (the quest accepted)", 359);
        Warp(206.3f, -20.8f, 667.0f, NorthGustabergS);
        TalkTo("Gebhardt");
        Cs("  Gebhardt: event 14", 14);
        Warp(-400.0f, 40.0f, -88.0f, NorthGustabergS);
        TalkTo("Roderich");
        Cs("  Roderich: event 15", 15);
        Warp(-515.0f, 38.0f, 581.0f, NorthGustabergS);
        TalkTo("Barricade");
        Cs("  the Barricade: event 16", 16);
        Warp(-325.8f, -12.6f, -75.0f, BastokMarketsS);
        TalkTo("Adelbrecht");
        Cs("  Adelbrecht: event 360", 360);
        Check("  Azure Footfalls complete, the fourth fragment: REPORT_TO_CAIT_SITH, Her Memories complete, Forget Me Not current",
              $"10/{Text(BastokMarketsS, "REPORT_TO_CAIT_SITH")}/41,0/True", $"{DbKeyItem(Ki("LARGE_MEMORY_FRAGMENT4"))}/{MsgNames()}/{Mission(LogWotg)}/{DbMissionComplete(LogWotg, 40)}");
        Lua("player:setCampaignAllegiance(0)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 42 Forget Me Not");
        Warp(-48.0f, 0.1f, 433.0f, BatalliaDowns);
        TalkTo("Cavernous_Maw");
        Check("Batallia Downs' maw: event 4", $"034:{NpcId("Cavernous_Maw")}:4:0,1,2964,1700,43", Ev());
        EventEnd(4);
        Rezone(false);
        Check("  Status 1, Option the maw's zone, set to Grauberg [S]: the zone-in cutscene 33", $"1/{BatalliaDowns}/{GraubergS}/33",
              $"{GVar(41, "Status")}/{GVar(41, "Option")}/{c.Zone}/{Cur()}");
        EventEnd(33, 1, 1);
        Check("  33's update: updateEvent(12, 23, 1756)", "12,23,1756", Upd());
        EventEnd(33);
        Rezone();
        Check("  Status 2, back to the maw's past side (Batallia Downs [S]): on the zone-in, Forget Me Not complete, the four fragments taken",
              $"{BatalliaDownsS}/42,0/00/00/00/00", $"{c.Zone}/{Mission(LogWotg)}/{DbKeyItem(Ki("LARGE_MEMORY_FRAGMENT1"))}/{DbKeyItem(Ki("LARGE_MEMORY_FRAGMENT2"))}/{DbKeyItem(Ki("LARGE_MEMORY_FRAGMENT3"))}/{DbKeyItem(Ki("LARGE_MEMORY_FRAGMENT4"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 43 Pillar of Hope");
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("the Veridical Conflux: event 28 (no weapons, not repeated)", $"034:{NpcId("Veridical_Conflux")}:28:89,23,1743", Ev());
        EventEnd(28, 0);
        Check("  option 0: Option 1", "42,0/1", $"{Mission(LogWotg)}/{GVar(42, "Option")}");
        TalkTo("Veridical_Conflux");
        Check("  again: event 28 repeated", $"034:{NpcId("Veridical_Conflux")}:28:89,23,1743,1", Ev());
        EventEnd(28, 1);
        Check("  option 1: Pillar of Hope complete, Glimmer of Life current with its Timer and a zone required",
              $"43,0/{VanaTime.CountDays(vanaTicks) + 1}/{GraubergS}", $"{Mission(LogWotg)}/{GVar(43, "Timer")}/{GVar(43, "mustZone")}");

        Console.WriteLine("WotG 44 Glimmer of Life");
        TalkTo("Veridical_Conflux");
        Check("the Conflux before a zone and a day: not 34", "False", Cur() == "34");
        EndAny();
        vanaTicks += VanaTime.DAY;
        Warp(-142.3f, -6.7f, 583.0f, BatalliaDownsS);
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("  zoned, the next day: event 34", $"034:{NpcId("Veridical_Conflux")}:34:89,23,1756", Ev());
        EventEnd(34);
        Check("  Glimmer of Life complete, Time Slips Away current", "44,0", Mission(LogWotg));

        Console.WriteLine("WotG 45 Time Slips Away");
        TalkTo("Veridical_Conflux");
        Cs("the Conflux: event 40", 40);
        TradeTo("Veridical_Conflux", (Slot(PunchBug), 1));
        Check("  a Punch Bug: event 35", $"034:{NpcId("Veridical_Conflux")}:35:64,23,1756", Ev());
        EventEnd(35);
        Check("  Time Slips Away complete: the Bottled Punch Bug, the bug taken, When Wills Collide current", "45,0/10/0",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("BOTTLED_PUNCH_BUG"))}/{Qty(PunchBug)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 46 When Wills Collide");
        TalkTo("Veridical_Conflux");
        Check("the Conflux with the Bottled Punch Bug: event 29", $"034:{NpcId("Veridical_Conflux")}:29:89,6", Ev());
        EventEnd(29, 99);
        Rezone();
        Check("  option 99: to the Walk of Echoes", WalkOfEchoes, c.Zone);
        Warp(-700.0f, -20.25f, -305.0f, WalkOfEchoes);
        TalkTo("_521");
        Check("the Ornate Door (_521): event 1", "1", Cur());
        EventEnd(1, 5, 1);
        Check("  1's update (option 5): updateEvent(119, the face and models, 1)", $"119,{LookParams()},1", Upd());
        EventEnd(1);
        Check("  Status 1", "1", GVar(45, "Status"));
        // the battlefield is not scripted (scripts/battlefields has no Walk of Echoes): its 32001 never comes, Status 2
        // a GM's
        Lua("player:setCharVar('Mission[5][45]Status', 2)");
        WarpIn(-700.1f, -17.6f, -331.9f, WalkOfEchoes);
        Check("Status 2 (GM): the zone-in cutscene 2", "2", Cur());
        EventEnd(2, 5, 1);
        Check("  2's update: updateEvent(182, the face and models)",
              string.Join(",", ("182," + LookParams()).Split(',').Reverse().SkipWhile(p => p == "0").Reverse()), Upd());
        EventEnd(2);
        Check("  When Wills Collide complete, Whispers of Dawn current", "46,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 47 Whispers of Dawn");
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("the Conflux: event 26 (outranks When Wills Collide's completed event 29)", $"034:{NpcId("Veridical_Conflux")}:26:89,23,1756,0,0,8323073", Ev());
        EventEnd(26, 1);
        Check("  option 1: Whispers of Dawn complete, A Dreamy Interlude's Timer and a zone required", $"47,0/{VanaTime.CountDays(vanaTicks) + 1}/{GraubergS}",
              $"{Mission(LogWotg)}/{GVar(47, "Timer")}/{GVar(47, "mustZone")}");

        Console.WriteLine("WotG 48 A Dreamy Interlude");
        TalkTo("Veridical_Conflux");
        Check("the Conflux before a zone and a day: event 27 (once per zone)", $"034:{NpcId("Veridical_Conflux")}:27:89,12354,59449,120,0,8323089", Ev());
        EventEnd(27);
        TalkTo("Veridical_Conflux");
        Check("  again: not 27", "False", Cur() == "27");
        EndAny();
        vanaTicks += VanaTime.DAY;
        Warp(-142.3f, -6.7f, 583.0f, BatalliaDownsS);
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("  zoned, the next day: event 36", $"034:{NpcId("Veridical_Conflux")}:36:89,1,596904,1000,0,8323089", Ev());
        EventEnd(36);
        Check("  A Dreamy Interlude complete, Cait in the Woods current", "48,0", Mission(LogWotg));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 49 Cait in the Woods");
        Warp(221.9f, -49.2f, 174.0f, EastRonfaureS);
        TalkTo("blank_cait");
        Check("Cait Sith (blank_cait): event 16", $"034:{NpcId("blank_cait")}:16:81,300,200,100,0,7405585,0,22292", Ev());
        EventEnd(16);
        Check("  Cait in the Woods complete: the Ronfaure Dawndrop, Fork in the Road current", "49,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("RONFAURE_DAWNDROP"))}");

        Console.WriteLine("WotG 50 Fork in the Road (the seven dawndrops)");
        (ushort Zone, float X, float Y, float Z, int Ev, string Ki)[] forks =
        {
            (JugnerForestS, 46.3f, 0.2f, 307.0f, 7, "JUGNER_DAWNDROP"), (LaVauleS, 295.0f, 3.5f, -215.0f, 5, "LA_VAULE_DAWNDROP"),
            (SouthernSandoriaS, 89.3f, 1.0f, 97.8f, 176, "SAN_DORIA_DAWNDROP"), (BeaucedineGlacierS, -69.9f, -39.9f, -190.8f, 29, "BEAUCEDINE_DAWNDROP"),
            (XarcabardS, -108.3f, -25.6f, -51.0f, 42, "XARCABARD_DAWNDROP"), (ThroneRoomS, 19.0f, 0.0f, -2.5f, 15, "THRONE_ROOM_DAWNDROP"),
            (WalkOfEchoes, -702.3f, 0.4f, -479.7f, 10, "WALK_OF_ECHOES_DAWNDROP"),
        };
        for (int i = 0; i < forks.Length; i++)
        {
            var f = forks[i];
            Warp(f.X, f.Y, f.Z, f.Zone);
            TalkTo("blank_fork");
            Check($"zone {f.Zone}'s blank_fork: event {f.Ev}", $"{f.Ev}", Cur());
            EventEnd(f.Ev);
            if (i < forks.Length - 1)
            {
                Check($"  the {f.Ki}", "10/49,0", $"{DbKeyItem(Ki(f.Ki))}/{Mission(LogWotg)}");
                TalkTo("blank_fork");
                Check("  again: nothing of the mission's", "False", Cur() == $"{f.Ev}");
                EndAny();
            }
        }
        Check("  the seventh: GATHERED_DAWNDROPS_LIGHT, RETRACED_ALL_JUNCTIONS, the dawndrops taken (the Ronfaure one too), Fork in the Road complete: the Primal Glow",
              $"{Text(WalkOfEchoes, "KEYITEM_OBTAINED")}:{Ki("WALK_OF_ECHOES_DAWNDROP")},{Text(WalkOfEchoes, "GATHERED_DAWNDROPS_LIGHT")}:{Ki("PRIMAL_GLOW")}," +
              $"{Text(WalkOfEchoes, "KEYITEM_OBTAINED")}:{Ki("PRIMAL_GLOW")}/{Text(WalkOfEchoes, "RETRACED_ALL_JUNCTIONS")}/00/00/00/50,0/10",
              $"{Special()}/{MsgNames()}/{DbKeyItem(Ki("JUGNER_DAWNDROP"))}/{DbKeyItem(Ki("WALK_OF_ECHOES_DAWNDROP"))}/{DbKeyItem(Ki("RONFAURE_DAWNDROP"))}/{Mission(LogWotg)}/{DbKeyItem(Ki("PRIMAL_GLOW"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 51 Maiden of the Dusk");
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("the Conflux: event 38", $"034:{NpcId("Veridical_Conflux")}:38:89,23,1756", Ev());
        EventEnd(38, 0);
        Check("  declined: Option 1", "1", GVar(50, "Option"));
        TalkTo("Veridical_Conflux");
        Check("  again: event 38 with the decline", $"034:{NpcId("Veridical_Conflux")}:38:89,23,1756,0,0,0,1", Ev());
        EventEnd(38, 1);
        Rezone(false);
        Check("  accepted: Status 1, to the Walk of Echoes: the zone-in cutscene 4", $"1/{WalkOfEchoes}/4", $"{GVar(50, "Status")}/{c.Zone}/{Cur()}");
        EventEnd(4);
        Warp(-700.0f, -20.25f, -305.0f, WalkOfEchoes);
        TalkTo("_521");
        Cs("the Ornate Door at Status 2: event 5", 5);
        Check("  Status 3", "3", GVar(50, "Status"));
        // the Primal Glow lost (the battlefield's entry would take it): the Conflux's replacement, 44, then 45 until
        // the next day
        Lua("player:delKeyItem(xi.keyItem.PRIMAL_GLOW)");
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("the Conflux without the Primal Glow: event 44", $"034:{NpcId("Veridical_Conflux")}:44:89,23,1756,0,68032378,209748094,1", Ev());
        EventEnd(44);
        Check("  the Primal Glow again, a Timer the next day", $"10/{VanaTime.CountDays(vanaTicks) + 1}", $"{DbKeyItem(Ki("PRIMAL_GLOW"))}/{GVar(50, "Timer")}");
        Lua("player:delKeyItem(xi.keyItem.PRIMAL_GLOW)");
        // two Event actions now: When Wills Collide's completed 29 and this 45, cycled (performNextAction): 29 first
        TalkTo("Veridical_Conflux");
        Cs("  lost again before the day: When Wills Collide's completed event 29 first (the cycle of two Event actions)", 29);
        TalkTo("Veridical_Conflux");
        Cs("  again: event 45", 45);
        Lua("player:addKeyItem(xi.keyItem.PRIMAL_GLOW)");
        // the Maiden of the Dusk battlefield is not scripted: its 32001 never comes, Status 4 a GM's
        Lua("player:setCharVar('Mission[5][50]Status', 4)");
        WarpIn(-700.1f, -17.6f, -331.9f, WalkOfEchoes);
        Check("Status 4 (GM): the zone-in cutscene 6", "6", Cur());
        EventEnd(6, 6, 1);
        Check("  6's update (option 6): updateEvent(1, 300, 200, 100, 182, 1)", "1,300,200,100,182,1", Upd());
        EventEnd(6);
        Rezone(false);
        Check("  Status 5: 7", "5/7", $"{GVar(50, "Status")}/{Cur()}");
        EventEnd(7, 5, 1);
        Check("  7's update: updateEvent(1, 300, 200, 100, 73, 545, 278)", "1,300,200,100,73,545,278", Upd());
        EventEnd(7);
        Rezone(false);
        Check("  Status 6: 8", "6/8", $"{GVar(50, "Status")}/{Cur()}");
        EventEnd(8, 5, 1);
        Check("  8's update: updateEvent(1, 300, 200, 100)", "1,300,200,100", Upd());
        EventEnd(8);
        Rezone(false);
        Check("  Status 7: 9", "7/9", $"{GVar(50, "Status")}/{Cur()}");
        EventEnd(9);
        Check("  Maiden of the Dusk complete: the Moonshade Earring key item, Where It All Began current", "51,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("MOONSHADE_EARRING"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 52 Where It All Began");
        Warp(96.0f, 0.0f, 104.0f, SouthernSandoriaS);
        TalkTo("Lion_Springs");
        Check("Lion Springs: event 175", $"034:{NpcId("Lion_Springs")}:175:80,72", Ev());
        EventEnd(175);
        Check("  Where It All Began complete: the Wedding Invitation, A Token of Troth current", "52,0/10",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("WEDDING_INVITATION"))}");

        Console.WriteLine("WotG 53 A Token of Troth");
        Warp(-447.2f, -1.8f, 340.0f, SauromugueChampaignS);
        TalkTo("Bulwark_Gate");
        Check("the Bulwark Gate unarmed: event 115", $"034:{NpcId("Bulwark_Gate")}:115:98,5,1756,0,0,1", Ev());
        EventEnd(115);
        TalkTo("Bulwark_Gate");
        Check("  Status 1: event 116", $"034:{NpcId("Bulwark_Gate")}:116:98,5,1756,0,0,0,1", Ev());
        EventEnd(116);
        Check("  A Token of Troth complete: the invitation taken, Lest We Forget current at its Status 1", "53,0/00/1",
              $"{Mission(LogWotg)}/{DbKeyItem(Ki("WEDDING_INVITATION"))}/{GVar(53, "Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("WotG 54 Lest We Forget (the Moonshade Earring)");
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        TalkTo("Veridical_Conflux");
        Check("the Conflux: event 39", $"034:{NpcId("Veridical_Conflux")}:39:89,23,1756", Ev());
        // the augment picks: bits 1-4 the first (index 2: ATK+4), bits 17-20 the second (index 3: Occ. dmg. bonus by TP)
        uint augOpt = 2u << 1 | 3u << 17;
        EventEnd(39, augOpt, 1);
        Check("  the update: the two augments' params ((3 << 27) + (25 << 16) + 770, (4 << 11) + 352)",
              $"{(3u << 27) + (25u << 16) + 770},{(4u << 11) + 352}", Upd());
        EventEnd(39, augOpt);
        // the augments go to addItem as its 3rd-6th args, which CLuaBaseEntity::addItem's second format (id, quantity,
        // silent) never reads: the earring comes unaugmented
        Check("  finished: the Moonshade Earring (its augment args dropped by addItem: none on it), ITEM_OBTAINED, Status 0 (cleared)",
              $"1/0,0,0,0/{Text(GraubergS, "ITEM_OBTAINED")}:{MoonshadeEarring}/-",
              $"{Qty(MoonshadeEarring)}/{Lua($"local it = player:findItem({MoonshadeEarring}) local a, b = it:getAugment(0), it:getAugment(1) report(a[1]) report(a[2]) report(b[1]) report(b[2])")}/{Special()}/{GVar(53, "Status")}");
        // CLuaBaseEntity::addItem's return is the CItem given (the fishing contest, Soul Plates and mannequins set its
        // exdata), nil when none
        Check("  addItem's return: the item given (its id, slot and quantity); nil for an unknown id", $"{Elixir},true,1,nil",
              Lua($"local it = player:addItem({Elixir}, 1, true) report(it:getID()) report(tostring(it:getSlotID() > 0)) report(it:getQuantity()) report(tostring(player:addItem(65000)))"));
        Lua($"player:delItem({Elixir}, 2)");
        expectedWarnings.Add("lua: AddItem: Item <65000> is not found in a database");
        // the earring lost (dropped), a zone: afterZoneIn's Option 1, the qm_reset's event 46 back to Maiden of the Dusk
        Lua($"player:delItem({MoonshadeEarring}, 1)");
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        Tick(4100);
        Check("  the earring gone, zoned: the reset's local Option", "1", Lua("report(player:getLocalVar('Mission[5][53]Option'))"));
        TalkTo("qm_reset");
        Check("the qm_reset: event 46", $"034:{NpcId("qm_reset")}:46:89,23,1756,0,9467,16940788", Ev());
        EventEnd(46, 0);
        Check("  option 0: a zone required", $"{GraubergS}", GVar(53, "mustZone"));
        TalkTo("qm_reset");
        Check("  again before a zone: nothing", "False", Cur() == "46");
        EndAny();
        Warp(-142.3f, -6.7f, 583.0f, BatalliaDownsS);
        Warp(-142.3f, -6.7f, 583.0f, GraubergS);
        Tick(4100);
        TalkTo("qm_reset");
        Cs("  zoned: event 46 again", 46, 1);
        Check("  option 1: the last four missions removed, Maiden of the Dusk current at Status 2, the Primal Glow, YOU_HAVE_RETRACED_RIVER",
              $"50,0/False/False/2/10/{Text(GraubergS, "YOU_HAVE_RETRACED_RIVER")}:0",
              $"{Mission(LogWotg)}/{DbMissionComplete(LogWotg, 50)}/{DbMissionComplete(LogWotg, 52)}/{GVar(50, "Status")}/{DbKeyItem(Ki("PRIMAL_GLOW"))}/{Special()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Seekers of Adoulin ----
        Console.WriteLine("Seekers of Adoulin: the login in Lower Jeuno by Darcia");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[1];
        Fixture(db, LowerJeuno, LowerJeuno, -36.0f, -1.0f, -13.0f, 0);
        Login();
        string AVar(int mission, string name) => Var($"Mission[{LogSoa}][{mission}]{name}");
        int SoaId(string name) => int.Parse(Lua($"report(xi.mission.id.soa.{name})"));
        string Bayld() => Lua("report(player:getCurrency('bayld'))");
        // a GM's !completemission, !setmissionstatus 0 and !addmission past a mission the scripts cannot complete
        void GmSkip(int log, int next)
        {
            Lua($"player:completeMission({log}, player:getCurrentMission({log})) player:setMissionStatus({log}, 0) player:addMission({log}, {next})");
        }
        // a walk into a trigger area: the zone-in's ZoningIn local var gone (xi.player.onGameIn's 2.5 s timer), the char
        // moved to its middle, the zone's trigger-area tick
        void Enter(float x, float y, float z)
        {
            Tick(2600);
            MoveTo(x, y, z);
            Tick(400);
        }
        Check("in Lower Jeuno, the SoA log at Rumors from the West (the CCharEntity default 0)", $"{LowerJeuno}/0,0", $"{c.Zone}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 1-1 Rumors from the West");
        // Rhapsodies of Vana'diel 1-1 (the RoV log's default 0) has its zone-in 30035 in the nation cities too, and comes
        // first in the onZoneIn lookup: it plays (and completes RoV 1-1), SoA's optional one on the next zone-in
        WarpIn(148.0f, 0.0f, 137.0f, NorthernSandoria);
        Check("into Northern San d'Oria: RoV 1-1's zone-in 30035 first", $"{NorthernSandoria}/30035", $"{c.Zone}/{Cur()}");
        EventEnd(30035);
        Check("  Rhapsodies of Vana'diel complete, Resonance current", $"{Lua("report(xi.mission.id.rov.RESONANCE)")},0", Mission(LogRov));
        WarpIn(148.0f, 0.0f, 137.0f, NorthernSandoria);
        Check("an optional cutscene: into Northern San d'Oria again, SoA's zone-in 878", "878", Cur());
        EventEnd(878);
        Check("  Status bit 0 (seen)", "0,1", Mission(LogSoa));
        Warp(148.0f, 0.0f, 137.0f, NorthernSandoria);
        Check("  again: not shown", "none", Cur());
        Warp(-36.0f, -1.0f, -13.0f, LowerJeuno);
        TalkTo("Darcia");
        Check("Darcia: event 10117 (dungeon info and asking for work turned off: 8 + 32)", $"034:{NpcId("Darcia")}:10117:0,40", Ev());
        EventEnd(10117, 0, 1);
        Check("  the update: updateEvent(hasEnoughGil 0: under 1,000,000)", "True/", $"{Of(0x05C).Count > 0}/{Upd()}");
        EventEnd(10117, 1);
        Check("  option 1: Rumors from the West complete, the Geomagnetron, The Geomagnetron current", "1,0/10",
              $"{Mission(LogSoa)}/{DbKeyItem(Ki("GEOMAGNETRON"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 1-2 The Geomagnetron");
        TalkTo("Darcia");
        Check("Darcia: event 10117 (1, 32)", $"034:{NpcId("Darcia")}:10117:1,32", Ev());
        EventEnd(10117, 0);
        Warp(0.0f, 0.0f, 0.0f, DangrufWadi);
        TalkTo("Geomagnetic_Fount");
        Check("Dangruf Wadi's Geomagnetic Fount: GEOMAGNETRON_ATTUNED, the SOA var 1", $"{Text(DangrufWadi, "GEOMAGNETRON_ATTUNED")}:0/1",
              $"{Special()}/{Var("SOA")}");
        TalkTo("Geomagnetic_Fount");
        Check("  again: already attuned (+2)", $"{int.Parse(Text(DangrufWadi, "GEOMAGNETRON_ATTUNED")) + 2}:0", Special());
        Warp(-36.0f, -1.0f, -13.0f, LowerJeuno);
        TalkTo("Darcia");
        Cs("Darcia with the attuned Geomagnetron: event 10118", 10118);
        Check("  The Geomagnetron complete: the Geomagnetron again, the Adoulinian Charter Permit, the SOA var cleared, Onward to Adoulin current",
              $"{SoaId("ONWARD_TO_ADOULIN")},0/10/10/-", $"{Mission(LogSoa)}/{DbKeyItem(Ki("GEOMAGNETRON"))}/{DbKeyItem(Ki("ADOULINIAN_CHARTER_PERMIT"))}/{Var("SOA")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 1-3 Onward to Adoulin");
        TalkTo("Waypoint");
        Cs("the Waypoint: event 10120, option 1", 10120, 1);
        Rezone(false);
        Check("  Status 1, to Ceizak Battlegrounds: the zone-in cutscene 3", $"{CeizakBattlegrounds}/3", $"{c.Zone}/{Cur()}");
        EventEnd(3);
        Check("  Onward to Adoulin complete, Heartwings and the Kindhearted current", $"{SoaId("HEARTWINGS_AND_THE_KINDHEARTED")},0", Mission(LogSoa));

        Console.WriteLine("SoA 1-4 Heartwings and the Kindhearted");
        WarpIn(-86.0f, 3.3f, 20.0f, WesternAdoulin);
        Check("into Western Adoulin: the zone-in cutscene 2", $"{WesternAdoulin}/2", $"{c.Zone}/{Cur()}");
        EventEnd(2);
        // Western Adoulin's own onEventFinish completes Heartwings for cs 2 too (the fallback runs after the mission's
        // handler): its completeMission and addMission warn, the mission already moved on
        Check("  Heartwings and the Kindhearted complete, Pioneer Registration current; the zone's own second completion warns",
              $"{SoaId("PIONEER_REGISTRATION")},0/lua: completeMission: 5 on log 12 is not the current mission,lua: addMission: overwriting current mission 6 of log 12 with 6",
              $"{Mission(LogSoa)}/{string.Join(",", ScriptErrors())}");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);

        Console.WriteLine("SoA 1-5 Pioneer Registration");
        TalkTo("Brenton");
        Cs("Brenton: event 3", 3);
        Check("  Pioneer Registration complete: 1000 bayld, the Map of Adoulin, the Pioneer's Badge, Life on the Frontier current",
              $"1000/10/10/{SoaId("LIFE_ON_THE_FRONTIER")},0", $"{Bayld()}/{DbKeyItem(Ki("MAP_OF_ADOULIN"))}/{DbKeyItem(Ki("PIONEERS_BADGE"))}/{Mission(LogSoa)}");
        TalkTo("Berstrogus");
        Check("  then Berstrogus' replaced default: event 587", "587", Cur());
        EndAny();
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 1-6 Life on the Frontier (the imprimatur gate)");
        // xi.soa.helpers.imprimaturGate(player, 10): 0 imprimaturs spent (a TODO) against 100 - fame level * 10, and the
        // fame level tops out at 9 (xi.data.fame.rankPoints): 10 > 0 at any fame, Brenton's event 4 never offered
        Lua("player:setFame(xi.fameArea.ADOULIN, 613)");
        TalkTo("Brenton");
        Check("Brenton at Adoulin fame 9: no event 4 (the gate is never met)", "9/False",
              $"{Lua("report(player:getFameLevel(xi.fameArea.ADOULIN))")}/{Cur() == "4"}");
        EndAny();
        GmSkip(LogSoa, SoaId("MEETING_OF_THE_MINDS"));
        Lua("player:addKeyItem(xi.keyItem.DINNER_INVITATION)");
        Check("  Meeting of the Minds a GM's (with Life on the Frontier's Dinner Invitation)", $"{SoaId("MEETING_OF_THE_MINDS")},0", Mission(LogSoa));

        Console.WriteLine("SoA 1-7 Meeting of the Minds");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk at 12:00 (not dinner time): event 1501", "12/1501", $"{Lua("report(VanadielHour())")}/{Cur()}");
        EventEnd(1501);
        vanaTicks += 4 * VanaTime.HOUR;
        TalkTo("Ploh_Trishbahk");
        Check("  at 16:00 (15-22): event 1500", "16/1500", $"{Lua("report(VanadielHour())")}/{Cur()}");
        EventEnd(1500);
        Check("  Meeting of the Minds complete, the invitation taken, Arciela Appears Again current", $"{SoaId("ARCIELA_APPEARS_AGAIN")},0/00",
              $"{Mission(LogSoa)}/{DbKeyItem(Ki("DINNER_INVITATION"))}");
        vanaTicks += 20 * VanaTime.HOUR;

        Console.WriteLine("SoA 1-8 Arciela Appears Again");
        // imprimaturGate(player, 20): 100 - 9 * 20 <= 0, met at fame 5 and up
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 6, option 0 (declined)", 6, 0);
        // the finish sets Status 1 on a decline, then completes the mission anyway (its unconditional complete)
        Check("  declined: Status 1 set, then Arciela Appears Again complete all the same, Budding Prospects current",
              $"{SoaId("BUDDING_PROSPECTS")},0/True", $"{Mission(LogSoa)}/{Lua($"report(tostring(player:hasCompletedMission({LogSoa}, {SoaId("ARCIELA_APPEARS_AGAIN")})))") == "true"}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-1 Budding Prospects");
        TalkTo("Levil");
        Cs("Levil: event 102", 102);
        Warp(-28.2f, -0.65f, -90.0f, WesternAdoulin);
        TalkTo("Masad");
        Check("Masad before Flavors of Our Lives: not event 8", "False", Cur() == "8");
        EndAny();
        Lua("player:completeQuest(xi.questLog.ADOULIN, xi.quest.id.adoulin.FLAVORS_OF_OUR_LIVES)");
        TalkTo("Masad");
        Cs("  Flavors of Our Lives done (GM): event 8", 8);
        Check("  Budding Prospects complete, The Light Shining in Your Eyes current", $"{SoaId("THE_LIGHT_SHINING_IN_YOUR_EYES")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-1-1 The Light Shining in Your Eyes");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 9", 9);
        Check("  complete, The Heirloom current", $"{SoaId("THE_HEIRLOOM")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-1-2 The Heirloom");
        TalkTo("Levil");
        Cs("Levil: event 103", 103);
        Warp(80.0f, -40.15f, -63.8f, EasternAdoulin);
        Enter(100.58f, -40.15f, -63.83f);
        Cs("Eastern Adoulin's trigger area 1 (by Ploh Trishbahk): event 1502", 1502);
        Check("  The Heirloom complete, An Aimless Journey current", $"{SoaId("AN_AIMLESS_JOURNEY")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-2 An Aimless Journey");
        Warp(-170.0f, 10.0f, 60.0f, CirdasCaverns);
        Enter(-140.0f, 10.0f, 60.0f);
        Cs("Cirdas Caverns' trigger area 1 (the Ergon Locus): event 12", 12);
        Check("  An Aimless Journey complete, Ortharsyne current", $"{SoaId("ORTHARSYNE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-2-1 Ortharsyne");
        WarpIn(-175.1f, 1.7f, 385.7f, YorciaWeald);
        Check("into Yorcia Weald: the zone-in cutscene 1", $"{YorciaWeald}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1);
        Check("  Ortharsyne complete, In the Presence of Royalty current", $"{SoaId("IN_THE_PRESENCE_OF_ROYALTY")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-2-2 In the Presence of Royalty");
        TalkTo("Pellucid_Afflusion");
        Check("the Pellucid Afflusion without Yorcia's Tear: not event 2", "False", Cur() == "2");
        EndAny();
        TalkTo("Ergon_Locus_qm");
        Check("the Ergon Locus ???: Yorcia's Tear (the keyItem action)", $"none/10/{Text(YorciaWeald, "KEYITEM_OBTAINED")}:{Ki("YORCIAS_TEAR")}",
              $"{Cur()}/{DbKeyItem(Ki("YORCIAS_TEAR"))}/{Special()}");
        TalkTo("Pellucid_Afflusion");
        Check("the Pellucid Afflusion: event 2", $"034:{NpcId("Pellucid_Afflusion")}:2:263", Ev());
        EventEnd(2);
        Check("  In the Presence of Royalty complete: Rosulatia's Pome, Queen's Confidante, The Twin World Trees current with its Timer",
              $"{SoaId("THE_TWIN_WORLD_TREES")},0/10/True/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Mission(LogSoa)}/{DbKeyItem(Ki("ROSULATIAS_POME"))}/{DbTitle(TitleId("QUEENS_CONFIDANTE"))}/{AVar(SoaId("THE_TWIN_WORLD_TREES"), "Timer")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-3 The Twin World Trees");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 127", 127);
        Warp(-60.0f, -0.15f, 30.7f, EasternAdoulin);
        Enter(-80.2f, -0.15f, 30.7f);
        Check("Eastern Adoulin's trigger area 2 (by Oscairn) before the Timer's day: nothing", "none", Cur());
        vanaTicks += VanaTime.DAY;
        MoveTo(-60.0f, -0.15f, 30.7f);
        Tick(400);
        Enter(-80.2f, -0.15f, 30.7f);
        Cs("  the next day: event 1503", 1503);
        Check("  The Twin World Trees complete, Honor and Audacity current", $"{SoaId("HONOR_AND_AUDACITY")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-4 Honor and Audacity");
        WarpIn(260.0f, -5.8f, 62.0f, RalaWaterways);
        Check("into Rala Waterways: the zone-in cutscene 342", $"{RalaWaterways}/342", $"{c.Zone}/{Cur()}");
        EventEnd(342);
        Check("  Honor and Audacity complete, The Watergarden Coliseum current", $"{SoaId("THE_WATERGARDEN_COLISEUM")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-4-1 The Watergarden Coliseum");
        TalkTo("Yeggha_Dolashi");
        Cs("Yeggha Dolashi: event 0", 0);
        Check("  complete, Friction and Fissures current with its Timer", $"{SoaId("FRICTION_AND_FISSURES")},0/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Mission(LogSoa)}/{AVar(SoaId("FRICTION_AND_FISSURES"), "Timer")}");

        Console.WriteLine("SoA 2-5 Friction and Fissures");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil before the Timer: event 127", 127);
        vanaTicks += VanaTime.DAY;
        TalkTo("Levil");
        Cs("  the next day: event 123", 123);
        Check("  Friction and Fissures complete, The Celennia Memorial Library current", $"{SoaId("THE_CELENNIA_MEMORIAL_LIBRARY")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-5-1 The Celennia Memorial Library");
        Warp(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        TalkTo("Yefafa");
        Check("Yefafa: event 1", $"034:{NpcId("Yefafa")}:1:284", Ev());
        EventEnd(1);
        TalkTo("History");
        Check("History: event 1003", $"034:{NpcId("History")}:1003:1", Ev());
        EventEnd(1003, 1, 1);
        Check("  the password shown (option 1): Status 1, updateEvent(284)", $"{SoaId("THE_CELENNIA_MEMORIAL_LIBRARY")},1/284", $"{Mission(LogSoa)}/{Upd()}");
        EventEnd(1003);
        // Levil reads getMissionStatus(areaId, missionId): a status position of 21 (> 7: "position 21 is invalid",
        // 0) as CLuaBaseEntity::getMissionStatus answers it, so his reminder 124, never the progress 125
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Check("Levil at Status 1: getMissionStatus(12, 21)'s invalid position, 0: the reminder 124, not 125",
              $"124/lua: getMissionStatus: position {SoaId("THE_CELENNIA_MEMORIAL_LIBRARY")} is invalid", $"{Cur()}/{ScriptErrors().LastOrDefault()}");
        EventEnd(124);
        expectedWarnings.Add($"lua: getMissionStatus: position {SoaId("THE_CELENNIA_MEMORIAL_LIBRARY")} is invalid");
        GmSkip(LogSoa, SoaId("FOR_WHOM_DO_WE_TOIL"));
        Check("  For Whom Do We Toil? a GM's", $"{SoaId("FOR_WHOM_DO_WE_TOIL")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-5-2 For Whom Do We Toil?");
        TalkTo("Levil");
        Cs("Levil: event 126", 126);
        WarpIn(-559.5f, -7.5f, 60.0f, RalaWaterways);
        Check("into Rala Waterways at Status 0: the zone-in cutscene 356", "356", Cur());
        EventEnd(356);
        Check("  Status 1", $"{SoaId("FOR_WHOM_DO_WE_TOIL")},1", Mission(LogSoa));
        TalkTo("Sluice_Gate_6");
        Cs("Sluice Gate 6: event 343, option 1 (a wrong password)", 343, 1);
        Check("  failCount 1", "1", AVar(SoaId("FOR_WHOM_DO_WE_TOIL"), "failCount"));
        Warp(-94.5f, -0.65f, -49.0f, EasternAdoulin);
        TalkTo("Wegellion");
        Check("Wegellion: event 1504", $"034:{NpcId("Wegellion")}:1504:257,9,3", Ev());
        EventEnd(1504);
        Warp(-559.5f, -7.5f, 60.0f, RalaWaterways);
        TalkTo("Sluice_Gate_6");
        Cs("Sluice Gate 6: event 343, option 0 (HGSI twice)", 343, 0);
        Check("  For Whom Do We Toil? complete: the Note Detailing Seditious Plans, Aiming for Ygnas current",
              $"{SoaId("AIMING_FOR_YGNAS")},0/10", $"{Mission(LogSoa)}/{DbKeyItem(Ki("NOTE_DETAILING_SEDITIOUS_PLANS"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-6 Aiming for Ygnas");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1505", $"034:{NpcId("Ploh_Trishbahk")}:1505:257", Ev());
        EventEnd(1505);
        Check("  complete, Calamity in the Kitchen current", $"{SoaId("CALAMITY_IN_THE_KITCHEN")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-6-1 Calamity in the Kitchen");
        Warp(-318.0f, -1.0f, -316.0f, RalaWaterways);
        TalkTo("Chalvava");
        Cs("Chalvava: event 344, option 0", 344, 0);
        TalkTo("Chalvava");
        Cs("  Status 1: event 345, option 1", 345, 1);
        Check("  complete: the Box of Adoulinian Tomatoes, Arciela's Promise current", $"{SoaId("ARCIELAS_PROMISE")},0/10",
              $"{Mission(LogSoa)}/{DbKeyItem(Ki("BOX_OF_ADOULINIAN_TOMATOES"))}");

        Console.WriteLine("SoA 2-6-2 Arciela's Promise");
        TalkTo("Chalvava");
        Cs("Chalvava: event 360 (once per zone)", 360);
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1506", $"034:{NpcId("Ploh_Trishbahk")}:1506:257,23,2964", Ev());
        EventEnd(1506);
        Check("  complete, Predators and Prey current", $"{SoaId("PREDATOR_AND_PREY")},0", Mission(LogSoa));

        Console.WriteLine("SoA 2-7 Predators and Prey");
        Warp(-559.5f, -7.5f, 60.0f, RalaWaterways);
        TalkTo("Sluice_Gate_6");
        Cs("Sluice Gate 6: event 352", 352);
        Check("  complete, Behind the Sluices current", $"{SoaId("BEHIND_THE_SLUICES")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-7-1 Behind the Sluices (the Antiquated Sluice Gate, instance 25900)");
        TalkTo("Sluice_Gate_6");
        Check("Sluice Gate 6 without the crank: PERHAPS_THE_WISEST", $"none/{Text(RalaWaterways, "PERHAPS_THE_WISEST")}:{Ki("WATERWAY_FACILITY_CRANK")}", $"{Cur()}/{Special()}");
        Warp(-150.2f, -7.1f, 31.0f, RalaWaterways);
        TalkTo("Storage_Container");
        Check("the Storage Container: the Waterway Facility Crank", "10", DbKeyItem(Ki("WATERWAY_FACILITY_CRANK")));
        Warp(-559.5f, -7.5f, 60.0f, RalaWaterways);
        TalkTo("Sluice_Gate_6");
        Check("Sluice Gate 6 with the crank: event 359", $"034:{NpcId("Sluice_Gate_6")}:359:258,102", Ev());
        EventEnd(359);
        TalkTo("Sluice_Gate_6");
        Cs("  Status 1: event 361, option 1", 361, 1);
        Check("  Status 2", $"{SoaId("BEHIND_THE_SLUICES")},2", Mission(LogSoa));
        uint sluice = NpcId("Antiquated_Sluice_Gate");
        Near(sluice);
        Talk(sluice);
        Check("the Antiquated Sluice Gate: xi.instance.onTrigger's event 5511 (258, 8), INSTANCE_ID 25900", $"034:{sluice}:5511:258,8/25900",
              $"{Ev()}/{c.LocalVars.GetValueOrDefault("INSTANCE_ID")}");
        EventEnd(5511, 0, 1);
        Check("  the update: createInstance queued", "2/nil", $"{c.LocalVars.GetValueOrDefault("INSTANCE_REQUESTED")}/{Lua("report(tostring(player:getInstance()))")}");
        s.PacketList.Clear();
        Tick(250);
        var sluiceReg = Of(0x0BF).LastOrDefault();
        Check("  loaded at the end of the tick: setInstance, instanceEntry(the gate, 4)", "25900/4",
              $"{Lua("report(player:getInstance():getID())")}/{(sluiceReg == null ? "none" : BitConverter.ToUInt16(sluiceReg.Buffer, 6).ToString())}");
        uint arcielaBts = MobOf("RALA_WATERWAYS_U", "ARCIELA_BTS");
        // onInstanceCreated spawns ARCIELA_BTS + 0..5: Arciela, the Keeper, Mistdagger and the two Briars; + 5 is
        // Zurko-Bazurko of the open zone (the script's TODO: "SpawnMob: mob <17838151> not found")
        Check("  onInstanceCreated: Arciela and her four foes spawned in the run", "5",
              Lua("local n = 0 for _, m in ipairs(player:getInstance():getMobs()) do if m:isSpawned() then n = n + 1 end end report(n)"));
        EventEnd(5511, 8);
        Rezone();
        Check("event 5511 finished (option 8): into Rala Waterways [U], in the run", $"{RalaWaterwaysU}/25900", $"{c.Zone}/{c.Instance?.Id}");
        s.PacketList.Clear();
        Tick(500);
        Check("  afterInstanceRegister: the crank lost (KEYITEM_LOST)", $"00/{Text(RalaWaterwaysU, "KEYITEM_LOST")}:{Ki("WATERWAY_FACILITY_CRANK")}",
              $"{DbKeyItem(Ki("WATERWAY_FACILITY_CRANK"))}/{Special()}");
        Lua("player:setUnkillable(true)");
        for (uint i = 1; i <= 4; i++) KillByChar(arcielaBts + i);
        for (int i = 0; i < 10 && Cur() != "1000"; i++) Tick(1000);
        Check("the four foes dead: onInstanceTimeUpdate's instance:complete(), onInstanceComplete's event 1000 and Status 3",
              $"true/1000/{SoaId("BEHIND_THE_SLUICES")},3", $"{Lua("report(tostring(player:getInstance():completed()))")}/{Cur()}/{Mission(LogSoa)}");
        // the run's onInstanceTimeUpdate keeps counting the four dead every second and calls instance:complete() again
        // (CInstance::CheckTime runs it while the run has not failed): onInstanceComplete's startEvent(1000) again,
        // refused while 1000 is on (StartEventHelper's "already triggered")
        Check("  a second on: complete() again, its startEvent(1000) refused while in 1000", "True",
              ScriptErrors().Contains($"lua: {c.Name} tried to start event 1000 while already in it"));
        expectedWarnings.Add($"lua: {c.Name} tried to start event 1000 while already in it");
        EventEnd(1000);
        Lua("player:setUnkillable(false)");
        // nothing scripted leads out of the run: a GM's warp back to Rala Waterways
        WarpIn(-529.4f, -7.0f, 60.0f, RalaWaterways);
        Check("back in Rala Waterways at Status 3: the zone-in cutscene 353", $"{RalaWaterways}/353", $"{c.Zone}/{Cur()}");
        EventEnd(353);
        Check("  Behind the Sluices complete, The Leafkin Monarch current", $"{SoaId("THE_LEAFKIN_MONARCH")},0", Mission(LogSoa));
        TalkTo("Sluice_Gate_6");
        Cs("  Sluice Gate 6 then: the replaced default 361", 361);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 2-7-2 The Leafkin Monarch");
        Warp(80.0f, -40.15f, -63.8f, EasternAdoulin);
        // StartEventHelper takes Boost off the char as an event starts
        Lua("player:addStatusEffect(xi.effect.BOOST, { origin = player, power = 1, duration = 180 })");
        Enter(100.58f, -40.15f, -63.83f);
        Check("Eastern Adoulin's trigger area 1: event 1507, the char's Boost gone with its start", "1507/false",
              $"{Cur()}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.BOOST)))")}");
        EventEnd(1507);
        Check("  The Leafkin Monarch complete, Yggdrasil current with its Timer", $"{SoaId("YGGDRASIL")},0/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Mission(LogSoa)}/{AVar(SoaId("YGGDRASIL"), "Timer")}");

        Console.WriteLine("SoA 2-7-3 Yggdrasil");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Check("Levil before the Timer: event 126", $"034:{NpcId("Levil")}:126:238", Ev());
        EventEnd(126);
        vanaTicks += VanaTime.DAY;
        TalkTo("Levil");
        Check("  the next day (imprimaturGate 30: met at fame 9): event 129", $"034:{NpcId("Levil")}:129:268", Ev());
        EventEnd(129, 0);
        TalkTo("Levil");
        Check("  declined: event 129 with the decline", $"034:{NpcId("Levil")}:129:268,1", Ev());
        EventEnd(129, 1);
        Check("  accepted: Yggdrasil complete, Return of the Exorcist current", $"{SoaId("RETURN_OF_THE_EXORCIST")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 3-1 Return of the Exorcist");
        TalkTo("Levil");
        Cs("Levil: event 138", 138);
        Enter(20.5f, 0.0f, 148.0f);
        Check("Western Adoulin's trigger area 1 (the airship docks' approach): event 130", $"034:{c.Id}:130:256,23,2964", Ev());
        EventEnd(130);
        Check("  Return of the Exorcist complete, The Merciless One current", $"{SoaId("THE_MERCILESS_ONE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-1-1 The Merciless One");
        Warp(80.0f, -40.15f, -63.8f, EasternAdoulin);
        Enter(100.58f, -40.15f, -63.83f);
        Cs("Eastern Adoulin's trigger area 1: event 1509", 1509);
        Check("  The Merciless One complete, A Curse from the Past current", $"{SoaId("A_CURSE_FROM_THE_PAST")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-1-2 A Curse from the Past");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1519", 1519);
        TalkTo("Ploh_Trishbahk");
        Cs("  again: event 1520 (once per zone)", 1520);
        Warp(68.0f, -40.15f, 12.7f, EasternAdoulin);
        TalkTo("Rigobertine");
        Cs("Rigobertine: event 1521", 1521);
        Check("  the Eternal Flame, Status 1", $"10/{SoaId("A_CURSE_FROM_THE_PAST")},1", $"{DbKeyItem(Ki("ETERNAL_FLAME"))}/{Mission(LogSoa)}");
        TalkTo("Rigobertine");
        Cs("  again: event 1522", 1522);
        TalkTo("Behsa_Alehgo");
        Cs("Behsa Alehgo with the flame: event 1523", 1523);
        Warp(17.4f, 1.0f, -19.2f, WesternAdoulin);
        TalkTo("Fontis_Xanira");
        Check("Fontis Xanira: the Vial of Untainted Holy Water (keyItem action), still Status 1", $"10/{SoaId("A_CURSE_FROM_THE_PAST")},1",
              $"{DbKeyItem(Ki("VIAL_OF_UNTAINTED_HOLY_WATER"))}/{Mission(LogSoa)}");
        Warp(115.2f, 32.0f, 175.9f, WesternAdoulin);
        TalkTo("Sunrise_Beacon");
        Check("the Sunrise Beacon with the vial: the Piece of a Stone Wall, Status 2", $"10/{SoaId("A_CURSE_FROM_THE_PAST")},2",
              $"{DbKeyItem(Ki("PIECE_OF_A_STONE_WALL"))}/{Mission(LogSoa)}");
        Warp(50.9f, -40.0f, -88.9f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk at Status 2: event 1524", 1524);
        TalkTo("Erminold");
        Cs("Erminold: event 1525", 1525);
        Check("  A Curse from the Past complete: the Weather Vane Wings, The Purgation current", $"{SoaId("THE_PURGATION")},0/10",
              $"{Mission(LogSoa)}/{DbKeyItem(Ki("WEATHER_VANE_WINGS"))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 3-1-3 The Purgation");
        TalkTo("Erminold");
        Cs("Erminold: the replaced default 1526", 1526);
        Warp(80.0f, -40.15f, -63.8f, EasternAdoulin);
        Enter(100.58f, -40.15f, -63.83f);
        Cs("Eastern Adoulin's trigger area 1: event 1510", 1510);
        Check("  The Purgation complete: the four key items taken, The Key current with its Timer", $"{SoaId("THE_KEY")},0/00/00/00/00/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Mission(LogSoa)}/{DbKeyItem(Ki("ETERNAL_FLAME"))}/{DbKeyItem(Ki("VIAL_OF_UNTAINTED_HOLY_WATER"))}/{DbKeyItem(Ki("PIECE_OF_A_STONE_WALL"))}/{DbKeyItem(Ki("WEATHER_VANE_WINGS"))}/{AVar(SoaId("THE_KEY"), "Timer")}");

        Console.WriteLine("SoA 3-1-4 The Key");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil before the Timer: event 138", 138);
        vanaTicks += VanaTime.DAY;
        TalkTo("Levil");
        Cs("  the next day: event 131", 131);
        Check("  The Key complete, The Princess's Dilemma current", $"{SoaId("THE_PRINCESSS_DILEMMA")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-2 The Princess's Dilemma");
        TalkTo("Levil");
        Cs("Levil: event 140", 140);
        WarpIn(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        Check("into the Celennia Memorial Library: the zone-in cutscene 2", $"{CelenniaMemorialLibrary}/2", $"{c.Zone}/{Cur()}");
        EventEnd(2, 3, 1);
        Check("  2's update (option 3): updateEvent(5, 1, 1756)", "5,1,1756", Upd());
        EventEnd(2, 4, 1);
        Check("  option 4: updateEvent(0, 511900, 1756)", "0,511900,1756", Upd());
        EventEnd(2, 1);
        Check("  finished with option 1: not complete", $"{SoaId("THE_PRINCESSS_DILEMMA")},0", Mission(LogSoa));
        WarpIn(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        Check("  zoned in again: 2 again", "2", Cur());
        EventEnd(2, 2);
        Check("  option 2: The Princess's Dilemma complete, Dark Clouds Ahead current", $"{SoaId("DARK_CLOUDS_AHEAD")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-2-1 Dark Clouds Ahead");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 132", 132);
        Check("  complete, The Smallest of Favors current with its Timer", $"{SoaId("THE_SMALLEST_OF_FAVORS")},0/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Mission(LogSoa)}/{AVar(SoaId("THE_SMALLEST_OF_FAVORS"), "Timer")}");

        Console.WriteLine("SoA 3-2-2 The Smallest of Favors");
        TalkTo("Levil");
        Cs("Levil: event 141", 141);
        vanaTicks += VanaTime.DAY;
        Warp(-170.0f, 0.0f, 0.0f, WesternAdoulin);
        TakeZoneLine(LineTo(CeizakBattlegrounds), false);
        Check("into Ceizak Battlegrounds from Western Adoulin, the Timer passed: the zone-in cutscene 20", $"{CeizakBattlegrounds}/20", $"{c.Zone}/{Cur()}");
        EventEnd(20);
        Check("  The Smallest of Favors complete, Summoned by Spirits current", $"{SoaId("SUMMONED_BY_SPIRITS")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 3-3 Summoned by Spirits");
        WarpIn(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        Check("into the Celennia Memorial Library: the zone-in cutscene 3", "3", Cur());
        EventEnd(3);
        Check("  Summoned by Spirits complete, Evil Entities current", $"{SoaId("EVIL_ENTITIES")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-3-1 Evil Entities");
        Warp(429.9f, 0.4f, 176.8f, CeizakBattlegrounds);
        TalkTo("Elmric");
        Cs("Elmric: event 23", 23);
        Check("  Evil Entities complete, Adoulin Calling current", $"{SoaId("ADOULIN_CALLING")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-3-2 Adoulin Calling");
        TalkTo("Elmric");
        Cs("Elmric: event 24", 24);
        Warp(-41.7f, -0.15f, -36.3f, EasternAdoulin);
        TalkTo("Door_Boarding_House");
        Cs("the Boarding House door: event 1511", 1511);
        Check("  Adoulin Calling complete, The Disappearance of Nyline current", $"{SoaId("THE_DISAPPEARANCE_OF_NYLINE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-3-3 The Disappearance of Nyline");
        TalkTo("Door_Boarding_House");
        Cs("the Boarding House door: event 1512 (Option 1)", 1512);
        TalkTo("Door_Boarding_House");
        Check("  again: not 1512", "False", Cur() == "1512");
        EndAny();
        Warp(500.0f, 0.0f, 157.6f, ForetDeHennetiel);
        Enter(529.9f, 0.0f, 157.6f);
        Cs("Foret de Hennetiel's cylindrical trigger area 1 (the Ergon Locus): event 517", 517);
        Check("  The Disappearance of Nyline complete, Shared Consciousness current", $"{SoaId("SHARED_CONSCIOUSNESS")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-3-4 Shared Consciousness");
        string bayldBefore = Bayld();
        Warp(-41.7f, -0.15f, -36.3f, EasternAdoulin);
        TalkTo("Door_Boarding_House");
        Cs("the Boarding House door: event 1513", 1513);
        Check("  complete: 1000 bayld, Sunshine Cadet, Clear Skies current with its Timer",
              $"{int.Parse(bayldBefore) + 1000}/True/{SoaId("CLEAR_SKIES")},0/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Bayld()}/{DbTitle(TitleId("SUNSHINE_CADET"))}/{Mission(LogSoa)}/{AVar(SoaId("CLEAR_SKIES"), "Timer")}");

        Console.WriteLine("SoA 3-3-5 Clear Skies");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil before the Timer: event 141", 141);
        vanaTicks += VanaTime.DAY;
        TalkTo("Levil");
        Cs("  the next day: event 133", 133);
        Check("  Clear Skies complete: 100 bayld, The Man in Black current", $"{int.Parse(bayldBefore) + 1100}/{SoaId("THE_MAN_IN_BLACK")},0", $"{Bayld()}/{Mission(LogSoa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 3-4 The Man in Black (Teodor's dice game)");
        TalkTo("Levil");
        Cs("Levil: event 142", 142);
        Warp(-28.2f, -0.65f, -90.0f, WesternAdoulin);
        TalkTo("Masad");
        Check("Masad: the game, event 134 (0: the first)", $"034:{NpcId("Masad")}:134", Ev());
        // a shot (option 0): the char's die (1-6) and Teodor's answer (a shot, a stay: math.randomInt), turn 1
        EventEnd(134, 0, 1);
        var shot = Upd().Split(',');
        Check("  a shot (option 0): updateEvent(0, turn 1, the char's roll 1-6, ...)", "True/1/True",
              $"{shot.Length >= 3 && shot[0] == "0"}/{(shot.Length >= 2 ? shot[1] : "-")}/{shot.Length >= 3 && int.Parse(shot[2]) is >= 1 and <= 6}");
        Check("  the char's shot kept (playerShots, three bits a shot), the turn 2", "True/2",
              $"{int.Parse(Lua("report(player:getLocalVar('playerShots'))")) is >= 1 and <= 6}/{Lua("report(player:getLocalVar('turnNumber'))")}");
        // the game lost (option 1): To the Victor's Status 1, The Man in Black complete
        EventEnd(134, 1);
        Check("  finished with option 1 (lost): The Man in Black complete, To the Victor current at its Status 1",
              $"{SoaId("TO_THE_VICTOR")},0/1", $"{Mission(LogSoa)}/{AVar(SoaId("TO_THE_VICTOR"), "Status")}");

        Console.WriteLine("SoA 3-4-1 To the Victor");
        TalkTo("Masad");
        Check("Masad: the game again, event 134 with To the Victor's Status 1", $"034:{NpcId("Masad")}:134:1", Ev());
        EventEnd(134, 2, 1);
        Check("  a peek (option 2): updateEvent(0, turn 1, 0, 2, 1, 0, 0, 4)", "0,1,0,2,1,0,0,4", Upd());
        EventEnd(134, 0);
        Check("  finished with option 0 (won): the 153 that follows", "153", Cur());
        EventEnd(153);
        Check("  To the Victor complete: Boomy and Busty, An Extraordinary Gentleman current", $"True/{SoaId("AN_EXTRAORDINARY_GENTLEMAN")},0",
              $"{DbTitle(TitleId("BOOMY_AND_BUSTY"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 3-4-2 An Extraordinary Gentleman");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Check("Levil: event 135", $"034:{NpcId("Levil")}:135:256", Ev());
        EventEnd(135);
        Check("  complete, The Order's Treasures current", $"{SoaId("THE_ORDERS_TREASURES")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-4-3 The Order's Treasures");
        TalkTo("Levil");
        Cs("Levil: event 143", 143);
        WarpIn(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        Check("into the library: the zone-in cutscene 4", "4", Cur());
        EventEnd(4);
        Check("  complete, August's Heirloom current", $"{SoaId("AUGUSTS_HEIRLOOM")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-4-4 August's Heirloom");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1514", 1514);
        Check("  complete, Beauty and the Beast current", $"{SoaId("BEAUTY_AND_THE_BEAST")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 3-5 Beauty and the Beast");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 5063 (important once)", 5063);
        Warp(429.9f, 0.4f, 176.8f, CeizakBattlegrounds);
        TalkTo("Signs_of_a_Struggle");
        Cs("the Signs of a Struggle: event 21", 21);
        Check("  complete: the Pristine Hair Ribbon, Wildcat with a Gold Pelt current", $"10/{SoaId("WILDCAT_WITH_A_GOLD_PELT")},0",
              $"{DbKeyItem(Ki("PRISTINE_HAIR_RIBBON"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 3-5-1 Wildcat with a Gold Pelt");
        Warp(80.0f, -40.15f, -63.8f, EasternAdoulin);
        Enter(100.58f, -40.15f, -63.83f);
        Cs("Eastern Adoulin's trigger area 1: event 1515", 1515);
        Check("  complete, In Search of Arciela current", $"{SoaId("IN_SEARCH_OF_ARCIELA")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-5-2 In Search of Arciela");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1516", 1516);
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 137", 137);
        Check("  complete, Looking for Leads current", $"{SoaId("LOOKING_FOR_LEADS")},0", Mission(LogSoa));

        Console.WriteLine("SoA 3-5-3 Looking for Leads");
        TalkTo("Levil");
        Cs("Levil: event 144", 144);
        Warp(-94.5f, -0.65f, -60.0f, EasternAdoulin);
        Enter(-94.48f, -0.65f, -51.17f);
        Cs("Eastern Adoulin's trigger area 3 (by Wegellion): event 1517", 1517);
        Check("  complete: the Tintinnabulum, Drifting Northwest current", $"10/{SoaId("DRIFTING_NORTHWEST")},0",
              $"{DbKeyItem(Ki("TINTINNABULUM"))}/{Mission(LogSoa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 3-6 Drifting Northwest");
        WarpIn(-344.0f, 40.7f, -381.1f, KamihrDrifts);
        Check("into Kamihr Drifts: the zone-in cutscene 29", $"{KamihrDrifts}/29", $"{c.Zone}/{Cur()}");
        EventEnd(29);
        Check("  complete: the Tintinnabulum taken (LOST_KEYITEM), Kumhau current", $"00/{Text(KamihrDrifts, "LOST_KEYITEM")}:{Ki("TINTINNABULUM")}/{SoaId("KUMHAU_THE_FLASHFROST_NAAKUAL")},0",
              $"{DbKeyItem(Ki("TINTINNABULUM"))}/{Special()}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 3-6-1 Kumhau, the Flashfrost Naakual");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 145 (Option 1)", 145);
        TalkTo("Levil");
        Cs("  again: event 152", 152);
        Warp(-347.3f, 40.3f, -379.8f, KamihrDrifts);
        TalkTo("Crawling_Cave");
        Cs("the Crawling Cave: event 30", 30);
        Rezone(false);
        Check("  Status 1, to Ceizak Battlegrounds: the zone-in cutscene 27", $"{CeizakBattlegrounds}/27", $"{c.Zone}/{Cur()}");
        EventEnd(27, 1, 1);
        Check("  27's update: updateEvent(109, 0, 100, 100, -72772, 80, 1203, 196654)", $"109,0,100,100,{uint.MaxValue - 72772},80,1203,196654", Upd());
        EventEnd(27);
        Rezone(false);
        Check("  Status 2, back to Kamihr Drifts: the zone-in cutscene 31", $"{KamihrDrifts}/31", $"{c.Zone}/{Cur()}");
        EventEnd(31);
        Check("  Kumhau complete: the Aureate Ball of Fur, Soul Siphon current", $"10/{SoaId("SOUL_SIPHON")},0",
              $"{DbKeyItem(Ki("AUREATE_BALL_OF_FUR"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 3-6-2 Soul Siphon");
        Warp(215.4f, 39.0f, -444.4f, KamihrDrifts);
        TalkTo("Hollowed_Pathway");
        Cs("the Hollowed Pathway: event 32, option 0", 32, 0);
        Rezone(false);
        Check("  Status 1, to Cirdas Caverns: the zone-in cutscene 29", $"{CirdasCaverns}/29", $"{c.Zone}/{Cur()}");
        EventEnd(29);
        Rezone(false);
        Check("  Status 2, back to Kamihr Drifts: the zone-in completes Soul Siphon (no cutscene), Stonewalled current",
              $"{KamihrDrifts}/none/{SoaId("STONEWALLED")},0", $"{c.Zone}/{Cur()}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 3-6-3 Stonewalled");
        Warp(-347.3f, 40.3f, -379.8f, KamihrDrifts);
        Lua("player:delKeyItem(xi.keyItem.AUREATE_BALL_OF_FUR)");
        TalkTo("Crawling_Cave");
        Check("the Crawling Cave without the Aureate Ball of Fur: it again (npcUtil.giveKeyItem), no event", "none/10",
              $"{Cur()}/{DbKeyItem(Ki("AUREATE_BALL_OF_FUR"))}");
        // Cirdas Caverns [U]'s event 1000 (the fight cleared) is the script's assumption, no instance scripted there:
        // Status 1 a GM's
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("STONEWALLED")}]Status', 1)");
        WarpIn(-100.7f, 29.7f, 216.2f, CirdasCaverns);
        Check("Status 1 (GM): into Cirdas Caverns, the zone-in cutscene 30", "30", Cur());
        EventEnd(30, 1, 1);
        Check("  30's update: updateEvent(200, 0, 200, 100, -365347, 1957, 44223)", $"200,0,200,100,{uint.MaxValue - 365347},1957,44223", Upd());
        EventEnd(30);
        Rezone();
        Check("  Stonewalled complete: the Soul Siphon, set to Kamihr Drifts, Salvation current", $"10/{KamihrDrifts}/{SoaId("SALVATION")},0",
              $"{DbKeyItem(Ki("SOUL_SIPHON"))}/{c.Zone}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 3-6-4 Salvation");
        Warp(-347.3f, 40.3f, -379.8f, KamihrDrifts);
        TalkTo("Crawling_Cave");
        Check("the Crawling Cave: event 33", "33", Cur());
        EventEnd(33, 2, 1);
        Check("  33's update (option 2): updateEvent(4, 1)", "4,1", Upd());
        EventEnd(33, 3);
        Check("  option 3: the Soul Siphon taken (LOST_KEYITEM), Salvation complete, Glimmer of Portent current with its Timer",
              $"00/{Text(KamihrDrifts, "LOST_KEYITEM")}:{Ki("SOUL_SIPHON")}/{SoaId("GLIMMER_OF_PORTENT")},0/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{DbKeyItem(Ki("SOUL_SIPHON"))}/{Special()}/{Mission(LogSoa)}/{AVar(SoaId("GLIMMER_OF_PORTENT"), "Timer")}");

        Console.WriteLine("SoA 3-6-5 Glimmer of Portent");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil before the Timer: event 152", 152);
        vanaTicks += VanaTime.DAY;
        TalkTo("Levil");
        Check("  the next day: event 154", $"034:{NpcId("Levil")}:154:256,0,255", Ev());
        EventEnd(154, 1);
        Check("  Glimmer of Portent complete, Into the Fire current", $"{SoaId("INTO_THE_FIRE")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-1 ...Into the Fire");
        TalkTo("Levil");
        Cs("Levil: event 161", 161);
        Warp(-32.0f, 0.0f, 24.0f, WesternAdoulin);
        TalkTo("Kipligg");
        Check("Kipligg: event 155", $"034:{NpcId("Kipligg")}:155:256,932,256", Ev());
        EventEnd(155);
        Check("  complete, Melvien de Malecroix current", $"{SoaId("MELVIEN_DE_MALECROIX")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-1-1 Melvien de Malecroix");
        TalkTo("Kipligg");
        Cs("Kipligg: event 162", 162);
        Warp(85.6f, 30.5f, 182.6f, WesternAdoulin);
        TalkTo("Port_Storage");
        // the set of goods to procure: math.randomInt over the five, kept in Courier Catastrophe's Option
        int set = int.Parse(AVar(SoaId("COURIER_CATASTROPHE"), "Option"));
        string[] goods = { "EFT_SKIN", "LOCK_OF_MANTICORE_HAIR", "BUFFALO_HORN", "SQUARE_OF_MANTICORE_LEATHER", "SQUARE_OF_BUFFALO_LEATHER" };
        uint[] goodsQty = { 5, 4, 1, 1, 1 };
        ushort good = ushort.Parse(Lua($"report(xi.item.{goods[set - 1]})"));
        Check("the Port Storage: event 156 with the five goods and the random set (1-5) and its count",
              $"True/034:{NpcId("Port_Storage")}:156:{string.Join(",", goods.Select(g => Lua($"report(xi.item.{g})")))},{good},{goodsQty[set - 1]}",
              $"{set is >= 1 and <= 5}/{Ev()}");
        EventEnd(156);
        Check("  complete, Courier Catastrophe current", $"{SoaId("COURIER_CATASTROPHE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-1-2 Courier Catastrophe");
        TalkTo("Port_Storage");
        Check("the Port Storage: TASKED_WITH_PROCURING with the set's item and count", $"{Text(WesternAdoulin, "TASKED_WITH_PROCURING")}:0", Special());
        Lua($"player:addItem({good}, {goodsQty[set - 1]})");
        TradeTo("Port_Storage", (Slot(good), goodsQty[set - 1]));
        Check("  the goods traded: event 167", $"034:{NpcId("Port_Storage")}:167:8,0,5", Ev());
        EventEnd(167);
        Check("  Courier Catastrophe complete, the goods taken, Done and Delivered current", $"{SoaId("DONE_AND_DELIVERED")},0/0", $"{Mission(LogSoa)}/{Qty(good)}");

        Console.WriteLine("SoA 4-1-3 Done and Delivered");
        Warp(-32.0f, 0.0f, 24.0f, WesternAdoulin);
        TalkTo("Kipligg");
        Check("Kipligg: event 157", $"034:{NpcId("Kipligg")}:157:256,99,3", Ev());
        EventEnd(157);
        Check("  complete, Ministerial Whispers current", $"{SoaId("MINISTERIAL_WHISPERS")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-1-4 Ministerial Whispers");
        TalkTo("Kipligg");
        Cs("Kipligg: event 163 (priority 101 over his own)", 163);
        string bayld41 = Bayld();
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 158", 158);
        Check("  complete: 500 bayld, A Day in the Life of a Pioneer current with its Timer and a zone required",
              $"{int.Parse(bayld41) + 500}/{SoaId("A_DAY_IN_THE_LIFE_OF_A_PIONEER")},0/{VanaTime.CountDays(vanaTicks) + 1}/{WesternAdoulin}",
              $"{Bayld()}/{Mission(LogSoa)}/{AVar(SoaId("A_DAY_IN_THE_LIFE_OF_A_PIONEER"), "Timer")}/{AVar(SoaId("A_DAY_IN_THE_LIFE_OF_A_PIONEER"), "mustZone")}");

        Console.WriteLine("SoA 4-1-5 A Day in the Life of a Pioneer");
        TalkTo("Levil");
        Cs("Levil before a zone and the day: event 164", 164);
        vanaTicks += VanaTime.DAY;
        Warp(-87.2f, 3.35f, 14.7f, EasternAdoulin);
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Check("  zoned, the next day: event 159", $"034:{NpcId("Levil")}:159:0,0,292,1", Ev());
        EventEnd(159);
        Check("  complete, Lighting the Way current", $"{SoaId("LIGHTING_THE_WAY")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-2 Lighting the Way");
        TalkTo("Levil");
        Cs("Levil: event 165", 165);
        Warp(-13.5f, -1.0f, 486.9f, KamihrDrifts);
        TalkTo("Alpine_Trail");
        Cs("the Alpine Trail: event 44, option 1", 44, 1);
        Rezone(false);
        Check("  Status 1, to Mount Kamihr: the zone-in cutscene 1", $"{MountKamihr}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1, 1, 1);
        Check("  1's update: updateEvent(0, 23, 1756, 0, 0, 1)", "0,23,1756,0,0,1", Upd());
        EventEnd(1);
        Rezone();
        Check("  Status 2, back to Kamihr Drifts: Lighting the Way complete on the zone-in, Knower of Untruths, Sajj'aka current",
              $"{KamihrDrifts}/True/{SoaId("SAJJAKA")},0", $"{c.Zone}/{DbTitle(TitleId("KNOWER_OF_UNTRUTHS"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-2-1 Sajj'aka");
        WarpIn(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        Check("into the library: the zone-in cutscene 5", "5", Cur());
        EventEnd(5, 1, 1);
        Check("  5's update (option 1): updateEvent(0, 2339983, 1756)", "0,2339983,1756", Upd());
        EventEnd(5);
        Check("  complete, Studying Up current", $"{SoaId("STUDYING_UP")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-2-2 Studying Up");
        Warp(-13.5f, -1.0f, 486.9f, KamihrDrifts);
        TalkTo("Alpine_Trail");
        Cs("the Alpine Trail: event 45, option 1", 45, 1);
        Rezone(false);
        Check("  Status 1, to Mount Kamihr: the zone-in cutscene 2", $"{MountKamihr}/2", $"{c.Zone}/{Cur()}");
        EventEnd(2, 1, 1);
        Check("  2's update: updateEvent(10, 0, 1756, 740, 0, 1)", "10,0,1756,740,0,1", Upd());
        EventEnd(2);
        Rezone();
        Check("  Status 2, back to Kamihr Drifts: Studying Up complete on the zone-in, A Vow of Truth current",
              $"{KamihrDrifts}/{SoaId("A_VOW_OF_TRUTH")},0", $"{c.Zone}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-2-3 A Vow of Truth");
        WarpIn(185.8f, 27.3f, 238.7f, WohGates);
        Check("into Woh Gates: the zone-in cutscene 7", $"{WohGates}/7", $"{c.Zone}/{Cur()}");
        EventEnd(7);
        Check("  complete, Darrcuiln current", $"{SoaId("DARRCUILN")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-2-4 Darrcuiln");
        TalkTo("Darkened_Crevice");
        Cs("the Darkened Crevice: event 8", 8);
        Check("  complete: the Tuft of Golden Fur, The Gates current", $"10/{SoaId("THE_GATES")},0",
              $"{DbKeyItem(Ki("TUFT_OF_GOLDEN_FUR"))}/{Mission(LogSoa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-3 The Gates");
        Lua("player:delKeyItem(xi.keyItem.TUFT_OF_GOLDEN_FUR)");
        TalkTo("Large_Animal_Track");
        Check("the Large Animal Track without the Tuft of Golden Fur: it again, no event", "none/10", $"{Cur()}/{DbKeyItem(Ki("TUFT_OF_GOLDEN_FUR"))}");
        // Cirdas Caverns [U]'s event 1000 (the fight cleared) is the script's assumption, no instance scripted: Status 1 a
        // GM's
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("THE_GATES")}]Status', 1)");
        WarpIn(182.1f, 30.0f, 240.2f, WohGates);
        Check("Status 1 (GM): into Woh Gates, the zone-in cutscene 9", "9", Cur());
        EventEnd(9);
        Check("  The Gates complete: Ulbukan Understudy, Morimar current", $"True/{SoaId("MORIMAR")},0", $"{DbTitle(TitleId("ULBUKAN_UNDERSTUDY"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-3-1 Morimar");
        Warp(-13.5f, -1.0f, 486.9f, KamihrDrifts);
        TalkTo("Alpine_Trail");
        Cs("the Alpine Trail: event 46, option 1", 46, 1);
        Rezone(false);
        Check("  Status 1, to Mount Kamihr: the zone-in cutscene 3", $"{MountKamihr}/3", $"{c.Zone}/{Cur()}");
        EventEnd(3, 1, 1);
        Check("  3's update: updateEvent(0, 2848833, 1756, 0, 0, 1)", "0,2848833,1756,0,0,1", Upd());
        EventEnd(3);
        Rezone();
        Check("  Morimar complete: Sajj'aka's Protective Ward, back to Kamihr Drifts, A New Force Arises current",
              $"10/{KamihrDrifts}/{SoaId("A_NEW_FORCE_ARISES")},0", $"{DbKeyItem(Ki("SAJJAKAS_PROTECTIVE_WARD"))}/{c.Zone}/{Mission(LogSoa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-3-2 A New Force Arises (the three scales)");
        Warp(197.0f, 58.0f, -18.0f, OuterRaKaznar);
        TalkTo("_7mw");
        Check("Outer Ra'Kaznar's _7mw: event 51", $"034:{NpcId("_7mw")}:51:274,300,200,100,{uint.MaxValue - 307959},234,582330,8", Ev());
        EventEnd(51);
        Check("  the Sunkissed Scale, HAVE_FOUND_SCALES (1)", $"10/{Text(OuterRaKaznar, "KEYITEM_OBTAINED")}:{Ki("SUNKISSED_SCALE")},{Text(OuterRaKaznar, "HAVE_FOUND_SCALES")}:1",
              $"{DbKeyItem(Ki("SUNKISSED_SCALE"))}/{Special()}");
        WarpIn(-115.6f, -2.15f, -93.0f, CelenniaMemorialLibrary);
        Check("into the library with one scale: the zone-in cutscene 6", "6", Cur());
        EventEnd(6);
        Check("  Option bit 0", "1", AVar(SoaId("A_NEW_FORCE_ARISES"), "Option"));
        Warp(-13.5f, -1.0f, 486.9f, KamihrDrifts);
        TalkTo("Alpine_Trail");
        Cs("the Alpine Trail with one scale: event 55, option 1", 55, 1);
        Rezone(false);
        Check("  to Mount Kamihr (not three scales): the zone-in cutscene 4", $"{MountKamihr}/4", $"{c.Zone}/{Cur()}");
        EventEnd(4, 1, 1);
        Check("  4's update: updateEvent(8, 23, 1756, 0, 0, 1)", "8,23,1756,0,0,1", Upd());
        EventEnd(4);
        Rezone();
        Check("  Option bit 1, back to Kamihr Drifts", $"3/{KamihrDrifts}", $"{AVar(SoaId("A_NEW_FORCE_ARISES"), "Option")}/{c.Zone}");
        TalkTo("Alpine_Trail");
        Check("  the Alpine Trail again (bit 1 set): not 55", "False", Cur() == "55");
        EndAny();
        Warp(-496.0f, -178.0f, 338.0f, OuterRaKaznar);
        TalkTo("Effigy_of_Sealing_1");
        Check("the Effigy of Sealing 1: event 52", $"034:{NpcId("Effigy_of_Sealing_1")}:52:274,300,200,100,239663,663,250000", Ev());
        EventEnd(52);
        Warp(-424.0f, -178.0f, -374.0f, OuterRaKaznar);
        TalkTo("Effigy_of_Sealing_2");
        Check("the Effigy of Sealing 2: event 53", $"034:{NpcId("Effigy_of_Sealing_2")}:53:274,300,200,100,289440,1681,568030", Ev());
        EventEnd(53);
        Check("  the third scale: HAVE_FOUND_SCALES (3), SOOTHING_SIGH_FALLS",
              $"{Text(OuterRaKaznar, "KEYITEM_OBTAINED")}:{Ki("STARBLESSED_SCALE")},{Text(OuterRaKaznar, "HAVE_FOUND_SCALES")}:3,{Text(OuterRaKaznar, "SOOTHING_SIGH_FALLS")}:0", Special());
        Warp(-13.5f, -1.0f, 486.9f, KamihrDrifts);
        TalkTo("Alpine_Trail");
        Check("the Alpine Trail with the three scales: event 56", $"034:{NpcId("Alpine_Trail")}:56:267,80,6,3,0,1,5", Ev());
        EventEnd(56, 1);
        Rezone(false);
        Check("  to Mount Kamihr: the zone-in cutscene 5", $"{MountKamihr}/5", $"{c.Zone}/{Cur()}");
        EventEnd(5, 1, 1);
        Check("  5's update: updateEvent(282, 2935914, 1756, 0, 0, 1)", "282,2935914,1756,0,0,1", Upd());
        EventEnd(5);
        string lost = Text(MountKamihr, "LOST_KEYITEM");
        Check("  the scales taken (LOST_KEYITEM x3), A New Force Arises complete: the World Tree Sapling, The Sacred Sapling current",
              $"{lost}:{Ki("SUNKISSED_SCALE")},{lost}:{Ki("MOONTOUCHED_SCALE")},{lost}:{Ki("STARBLESSED_SCALE")},{Text(MountKamihr, "KEYITEM_OBTAINED")}:{Ki("WORLD_TREE_SAPLING")}/00/{SoaId("THE_SACRED_SAPLING")},0",
              $"{Special()}/{DbKeyItem(Ki("MOONTOUCHED_SCALE"))}/{Mission(LogSoa)}");
        Rezone();
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-3-3 The Sacred Sapling");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1528", $"034:{NpcId("Ploh_Trishbahk")}:1528:257,0,3,0,25163255,6221159,4095", Ev());
        EventEnd(1528);
        Check("  complete, Tree Grafting current", $"{SoaId("TREE_GRAFTING")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-3-4 Tree Grafting");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1530", 1530);
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Check("Levil: event 160", $"034:{NpcId("Levil")}:160:256,0,1,1,0,0,0,4", Ev());
        EventEnd(160);
        Check("  complete, A Shrouded Canopy current", $"{SoaId("A_SHROUDED_CANOPY")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-3-5 A Shrouded Canopy");
        TalkTo("Levil");
        Cs("Levil: event 166", 166);
        WarpIn(-27.2f, -2.0f, 31.5f, Leafallia);
        Check("into Leafallia: the zone-in cutscene 2", $"{Leafallia}/2", $"{c.Zone}/{Cur()}");
        EventEnd(2);
        Check("  complete, Leafallia current", $"{SoaId("LEAFALLIA")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-3-6 Leafallia");
        TalkTo("Aged_Stump");
        Check("the Aged Stump: event 4", $"034:{NpcId("Aged_Stump")}:4:281,2955693,1756,0,58029558,583532,4095,131184", Ev());
        EventEnd(4);
        Check("  complete, Rosulatia's Promise current", $"{SoaId("ROSULATIAS_PROMISE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-3-7 Rosulatia's Promise");
        TalkTo("Heroic_Footprints");
        Check("the Heroic Footprints: event 3", $"034:{NpcId("Heroic_Footprints")}:3:0,2957690,1756,0,58029558,583232,4095,131184", Ev());
        EventEnd(3, 1, 1);
        Check("  3's update: updateEvent(3, 0, 1756, 0, 58029558, 583232, 4905, 131184)", "3,0,1756,0,58029558,583232,4905,131184", Upd());
        EventEnd(3);
        Check("  complete: the World Tree Sapling taken (LOST_KEYITEM), The Lightsland current",
              $"00/{Text(Leafallia, "LOST_KEYITEM")}:{Ki("WORLD_TREE_SAPLING")}/{SoaId("THE_LIGHTSLAND")},0",
              $"{DbKeyItem(Ki("WORLD_TREE_SAPLING"))}/{Special()}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-3-8 The Lightsland");
        TalkTo("Heroic_Footprints");
        Check("the Heroic Footprints: event 5", $"034:{NpcId("Heroic_Footprints")}:5:281,0,1756,0,58029558,583232,4095,131184", Ev());
        EventEnd(5);
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1529", $"034:{NpcId("Ploh_Trishbahk")}:1529:257,16744959,{uint.MaxValue - 1023},0,0,1228,327", Ev());
        EventEnd(1529, 4, 1);
        Check("  1529's update (option 4)", $"257,0,5,{uint.MaxValue - 1415568507},{uint.MaxValue - 964509701},201326591,3,4", Upd());
        EventEnd(1529, 2, 1);
        Check("  option 2: updateEvent(2)", "2", Upd());
        EventEnd(1529, 1);
        // the finish's addExp(500 * xi.settings.EXP_RATE): EXP_RATE lives in xi.settings.main, the product of a nil is the
        // script's own error, after the reward and the complete
        ushort refuge = ushort.Parse(Lua("report(xi.item.ADOULINS_REFUGE)"));
        Check("  option 1: Adoulin's Refuge, The Lightsland complete, The Light of Dawn Comes current with its Timer; addExp's nil EXP_RATE errors",
              $"1/{SoaId("THE_LIGHT_OF_DAWN_COMES")},0/{VanaTime.CountDays(vanaTicks) + 1}/True",
              $"{Qty(refuge)}/{Mission(LogSoa)}/{AVar(SoaId("THE_LIGHT_OF_DAWN_COMES"), "Timer")}/{log.Any(l => l.Contains("Error running handler") && l.Contains("EXP_RATE"))}");

        Console.WriteLine("SoA 4-3-9 The Light of Dawn Comes from the East");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil before the Timer: event 166", 166);
        vanaTicks += VanaTime.DAY;
        TalkTo("Levil");
        Check("  the next day: event 177", $"034:{NpcId("Levil")}:177:256,0,255,255,255,255,255", Ev());
        EventEnd(177);
        Check("  complete, Cries from the Deep current", $"{SoaId("CRIES_FROM_THE_DEEP")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-4 Cries from the Deep");
        TalkTo("Levil");
        Cs("Levil: event 180", 180);
        Warp(80.0f, -40.15f, -63.8f, EasternAdoulin);
        Enter(100.58f, -40.15f, -63.83f);
        Check("Eastern Adoulin's trigger area 1: event 1532", $"034:{c.Id}:1532:0,15827327,{uint.MaxValue - 703},579,89,207,1998,4", Ev());
        EventEnd(1532);
        Rezone(false);
        Check("  Status 1, set back: the zone-in cutscene 1550", "1550", Cur());
        EventEnd(1550);
        Check("  Cries from the Deep complete, Seeds of Doubt current", $"{SoaId("SEEDS_OF_DOUBT")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-4-1 Seeds of Doubt");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1540", 1540);
        Warp(-318.0f, -1.0f, -316.0f, RalaWaterways);
        TalkTo("Chalvava");
        Check("Chalvava: event 367", $"034:{NpcId("Chalvava")}:367:258,3033627,1756,{uint.MaxValue - 6266},20780,121,554049", Ev());
        EventEnd(367);
        Check("  complete, The Tomatoes of Wrath current", $"{SoaId("THE_TOMATOES_OF_WRATH")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-4-2 The Tomatoes of Wrath");
        TalkTo("Chalvava");
        Cs("Chalvava: event 370 (Option 1)", 370);
        TalkTo("Chalvava");
        Check("  again: not 370", "False", Cur() == "370");
        EndAny();
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1533", $"034:{NpcId("Ploh_Trishbahk")}:1533:257,935,127,0,4063,1999,1989", Ev());
        EventEnd(1533);
        Check("  complete, A Grave Mistake current", $"{SoaId("A_GRAVE_MISTAKE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-4-3 A Grave Mistake");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk before the three: event 1541", 1541);
        string GraveStatus() => AVar(SoaId("A_GRAVE_MISTAKE"), "Status");
        Warp(315.0f, -5.8f, -298.0f, RalaWaterways);
        TalkTo("Sainene");
        Cs("Sainene: event 372 (Status bit 0)", 372);
        TalkTo("Sainene");
        Cs("  again: event 320", 320);
        Warp(-427.5f, -1.0f, -46.2f, RalaWaterways);
        TalkTo("Stout_Weir");
        Cs("the Stout Weir: event 371 (bit 2, the Broken Fuse)", 371);
        TalkTo("Stout_Weir");
        Cs("  again with the fuse: event 373", 373);
        Warp(-18.0f, -6.5f, 39.4f, RalaWaterways);
        TalkTo("Waterways_Overlook");
        Check("the Waterways Overlook: bit 1, A_QUICK_GLANCE_REVEALS; Status 7, the Broken Fuse",
              $"none/{Text(RalaWaterways, "A_QUICK_GLANCE_REVEALS")}:0/7/10", $"{Cur()}/{Special()}/{GraveStatus()}/{DbKeyItem(Ki("BROKEN_FUSE"))}");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk at Status 7: event 1534", $"034:{NpcId("Ploh_Trishbahk")}:1534:257,0,5,0,67108863,1081558,4095", Ev());
        EventEnd(1534);
        Check("  complete: the fuse taken (LOST_KEYITEM), An Emergency Convocation current with a zone required",
              $"00/{Text(EasternAdoulin, "LOST_KEYITEM")}:{Ki("BROKEN_FUSE")}/{SoaId("AN_EMERGENCY_CONVOCATION")},0/{EasternAdoulin}",
              $"{DbKeyItem(Ki("BROKEN_FUSE"))}/{Special()}/{Mission(LogSoa)}/{AVar(SoaId("AN_EMERGENCY_CONVOCATION"), "mustZone")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-4-4 An Emergency Convocation");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk before a zone: event 1542", 1542);
        Warp(100.6f, -40.2f, -61.8f, WesternAdoulin);
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Cs("  zoned: event 1535", 1535);
        Rezone(false);
        Check("  Status 1, set back: the zone-in cutscene 1551", "1551", Cur());
        EventEnd(1551, 1, 1);
        Check("  1551's update: updateEvent(0, 0, 1, 975, 4063, 1999, 1999, 4)", "0,0,1,975,4063,1999,1999,4", Upd());
        EventEnd(1551);
        Rezone(false);
        Check("  Status 2: 1552", "1552", Cur());
        EventEnd(1552);
        Check("  An Emergency Convocation complete, Balamor current with its Timer", $"{SoaId("BALAMOR_THE_DEATHBORNE_XOL")},0/{VanaTime.CountDays(vanaTicks) + 1}",
              $"{Mission(LogSoa)}/{AVar(SoaId("BALAMOR_THE_DEATHBORNE_XOL"), "Timer")}");

        Console.WriteLine("SoA 4-5 Balamor, the Deathborne Xol");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk before the Timer: event 1543", 1543);
        vanaTicks += VanaTime.DAY;
        TalkTo("Ploh_Trishbahk");
        Check("  the next day: event 1536", $"034:{NpcId("Ploh_Trishbahk")}:1536:257,23,2964,0,87,203,1995", Ev());
        EventEnd(1536);
        Check("  complete, Anagnorisis current", $"{SoaId("ANAGNORISIS")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-5-1 Anagnorisis");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk with Adoulin's Refuge: event 1545", 1545);
        TradeTo("Ploh_Trishbahk", (Slot(refuge), 1));
        Check("  the Refuge traded: event 1544 with it", $"034:{NpcId("Ploh_Trishbahk")}:1544:{refuge},23,2964,975,4063,1999,1999,4", Ev());
        EventEnd(1544);
        Check("  Anagnorisis complete, the Refuge taken, Just the Thing current", $"{SoaId("JUST_THE_THING")},0/0", $"{Mission(LogSoa)}/{Qty(refuge)}");

        Console.WriteLine("SoA 4-5-2 Just the Thing");
        Warp(-91.9f, -2.1f, -89.5f, CelenniaMemorialLibrary);
        TalkTo("Andreine");
        Check("Andreine: event 7", $"034:{NpcId("Andreine")}:7:284,1316886,2963,28635", Ev());
        EventEnd(7);
        Check("  complete: the Suffering Sacchariferous, Sugarcoated Salvation current", $"10/{SoaId("SUGARCOATED_SALVATION")},0",
              $"{DbKeyItem(Ki("SUFFERING_SACCHARIFEROUS"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-5-3 Sugarcoated Salvation");
        TalkTo("Andreine");
        Cs("Andreine: event 8", 8);
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1537", 1537);
        Check("  complete: the Sacchariferous taken (LOST_KEYITEM), the Sepulcher Ensign, Arciela's Resolve current",
              $"00/10/{SoaId("ARCIELAS_RESOLVE")},0", $"{DbKeyItem(Ki("SUFFERING_SACCHARIFEROUS"))}/{DbKeyItem(Ki("SEPULCHER_ENSIGN"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-5-4 Arciela's Resolve");
        TalkTo("Ploh_Trishbahk");
        Cs("Ploh Trishbahk: event 1546", 1546);
        Warp(317.0f, -7.5f, -300.0f, RalaWaterways);
        TalkTo("Royal_Sepulcher");
        Check("the Royal Sepulcher: event 368", $"034:{NpcId("Royal_Sepulcher")}:368:258,3223117,1756,0,20558,1452,309278,8", Ev());
        EventEnd(368);
        Check("  complete, Balamor's Ruse current", $"{SoaId("BALAMORS_RUSE")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 4-6 Balamor's Ruse (the Consummate Simulacrum)");
        // the waterway mobs' onMobDeath: a 20% (math.randomInt) Consummate Simulacrum for the killer's party; killed
        // until it drops
        var rwInfo = zone.ZoneInfoOf(c.Zone);
        string[] ruseMobs = { "Duskbringer_Bat", "New_Moon_Bats", "Skittering_Spider", "Spoutdrenched_Toad", "Waterway_Pugil" };
        var candidates = rwInfo.Mobs.Where(m => ruseMobs.Contains(m.Name) && m.IsSpawned).Take(40).ToList();
        int kills = 0;
        Lua("player:setUnkillable(true)");
        foreach (var m in candidates)
        {
            if (DbKeyItem(Ki("CONSUMMATE_SIMULACRUM")) == "10") break;
            if (!m.IsSpawned || !m.IsAlive) continue;
            KillByChar(m.Id);
            kills++;
        }
        Lua("player:setUnkillable(false)");
        Check($"  the waterway mobs killed by the char ({kills} of {candidates.Count}): the Consummate Simulacrum", "10", DbKeyItem(Ki("CONSUMMATE_SIMULACRUM")));
        // Rala Waterways [U]'s event 1000 (the fight cleared) is the script's assumption, no instance scripted: Status 1
        // a GM's
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("BALAMORS_RUSE")}]Status', 1)");
        WarpIn(310.0f, -5.8f, -299.8f, RalaWaterways);
        Check("Status 1 (GM): into Rala Waterways, the zone-in cutscene 369", "369", Cur());
        EventEnd(369);
        Check("  Balamor's Ruse complete, The Charlatan current", $"{SoaId("THE_CHARLATAN")},0", Mission(LogSoa));

        Console.WriteLine("SoA 4-6-1 The Charlatan");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1538", $"034:{NpcId("Ploh_Trishbahk")}:1538:257,16744959,{uint.MaxValue - 1},0,0,1487,1997", Ev());
        EventEnd(1538, 4, 1);
        Check("  1538's update (option 4): updateEvent(0, 0, 65535, 900)", "0,0,65535,900", Upd());
        EventEnd(1538, 3);
        ushort graceP1 = ushort.Parse(Lua("report(xi.item.ARCIELAS_GRACE_P1)"));
        Check("  option 3: Arciela's Grace +1, The Charlatan complete, Royal Blessings current", $"1/{SoaId("ROYAL_BLESSINGS")},0",
              $"{Qty(graceP1)}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 4-6-2 Royal Blessings");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Check("Levil: event 178", $"034:{NpcId("Levil")}:178:256,0,1,0,0,0,0,4", Ev());
        EventEnd(178);
        Check("  complete, Arboreal Rumors current", $"{SoaId("ARBOREAL_RUMORS")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        const ushort RaKaznarInnerCourt = 276, RaKaznarTurris = 277;
        Console.WriteLine("SoA 5-1 Arboreal Rumors");
        TalkTo("Levil");
        Cs("Levil: event 183", 183);
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1539", $"034:{NpcId("Ploh_Trishbahk")}:1539:257,8", Ev());
        EventEnd(1539);
        Check("  complete: the Hastily Scribbled Note, Arciela's Missive current", $"10/{SoaId("ARCIELAS_MISSIVE")},0",
              $"{DbKeyItem(Ki("HASTILY_SCRIBBLED_NOTE"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 5-1-1 Arciela's Missive");
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1554", $"034:{NpcId("Ploh_Trishbahk")}:1554:257,2621,2964,0,{uint.MaxValue - 806879872},{uint.MaxValue - 2013266312},3", Ev());
        EventEnd(1554);
        TalkTo("Ploh_Trishbahk");
        Check("  again (Option 1): not 1554", "False", Cur() == "1554");
        EndAny();
        Warp(115.2f, 32.0f, 175.9f, WesternAdoulin);
        TalkTo("Sunrise_Beacon");
        Check("the Sunrise Beacon, the note not yet read: not 179", "False", Cur() == "179");
        EndAny();
        int note = Ki("HASTILY_SCRIBBLED_NOTE");
        Send(0x064, 76, b =>
        {
            BitConverter.GetBytes(c.Id).CopyTo(b, 4);
            BitConverter.GetBytes(1u << note % 32).CopyTo(b, 8 + note % 512 / 32 * 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, 72);
            BitConverter.GetBytes((ushort)(note / 512)).CopyTo(b, 74);
        });
        TalkTo("Sunrise_Beacon");
        Check("  the note read in the key item menu (0x064): event 179", $"034:{NpcId("Sunrise_Beacon")}:179:256,3930,5,5,65306623,1589031,4095,131116", Ev());
        EventEnd(179);
        // delKeyItem clears the owned bit only (charutils::delKeyItem), the seen bit stays
        Check("  complete: the note taken (KEYITEM_LOST, still seen), Heroes Unite current",
              $"01/{Text(WesternAdoulin, "KEYITEM_LOST")}:{note}/{SoaId("HEROES_UNITE")},0", $"{DbKeyItem(note)}/{Special()}/{Mission(LogSoa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 5-1-2 Heroes Unite");
        Warp(-87.2f, 3.35f, 14.7f, WesternAdoulin);
        TalkTo("Levil");
        Cs("Levil: event 40", 40);
        Warp(-13.5f, -1.0f, 486.9f, KamihrDrifts);
        TalkTo("Alpine_Trail");
        Check("the Alpine Trail: event 60", $"034:{NpcId("Alpine_Trail")}:60:267,300,200,100,4063,1999,1999", Ev());
        EventEnd(60, 1);
        Rezone(false);
        Check("  option 1, Status 1, to Mount Kamihr: the zone-in cutscene 6", $"{MountKamihr}/6", $"{c.Zone}/{Cur()}");
        EventEnd(6, 1, 1);
        Check("  6's update: updateEvent(10, 23, 1756, 0, 0, 1)", "10,23,1756,0,0,1", Upd());
        EventEnd(6);
        Rezone();
        Check("  Status 2, back to Kamihr Drifts: Heroes Unite complete on the zone-in, A Portent Most Ominous current",
              $"{KamihrDrifts}/{SoaId("A_PORTENT_MOST_OMINOUS")},0", $"{c.Zone}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 5-1-3 A Portent Most Ominous");
        Warp(-27.2f, -2.0f, 31.5f, Leafallia);
        TalkTo("Aged_Stump");
        Check("the Aged Stump: event 10", $"034:{NpcId("Aged_Stump")}:10:281,3447009,1756,0,13991935,5668390,4095,131184", Ev());
        EventEnd(10);
        Check("  complete: the Sky Blue and Sun Yellow Pomes, Yggdrasil Beckons current", $"10/10/{SoaId("YGGDRASIL_BECKONS")},0",
              $"{DbKeyItem(Ki("SKY_BLUE_POME"))}/{DbKeyItem(Ki("SUN_YELLOW_POME"))}/{Mission(LogSoa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 5-2 Yggdrasil Beckons");
        string YggStatus() => AVar(SoaId("YGGDRASIL_BECKONS"), "Status");
        Lua("player:delKeyItem(xi.keyItem.SKY_BLUE_POME)");
        TalkTo("Aged_Stump");
        Check("the Aged Stump, the Sky Blue Pome lost: given again, no event", "none/10/10",
              $"{Cur()}/{DbKeyItem(Ki("SKY_BLUE_POME"))}/{DbKeyItem(Ki("SUN_YELLOW_POME"))}");
        Warp(0.0f, 0.0f, 0.0f, YorciaWeald);
        TalkTo("Wavering_Flux");
        Check("Yorcia Weald's Wavering Flux: event 3", $"034:{NpcId("Wavering_Flux")}:3:263,0,200,100,379558,1978,544958", Ev());
        EventEnd(3);
        Check("  Status bit 0", "1", YggStatus());
        TalkTo("Wavering_Flux");
        Check("  again: not 3", "False", Cur() == "3");
        EndAny();
        // Yorcia Weald [U]'s event 1000 (Ashrakk beaten) is the script's assumption, no instance scripted: bit 1 a GM's
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("YGGDRASIL_BECKONS")}]Status', 3)");
        WarpIn(34.0f, 1.124f, 372.0f, YorciaWeald);
        Check("bit 1 (GM): into Yorcia Weald, the zone-in cutscene 4", "4", Cur());
        EventEnd(4);
        Check("  Ashrakk's Blood Sigil, not complete without Dhokmak's", $"10/{SoaId("YGGDRASIL_BECKONS")},0",
              $"{DbKeyItem(Ki("ASHRAKKS_BLOOD_SIGIL"))}/{Mission(LogSoa)}");
        Warp(300.0f, 30.0f, -79.0f, CirdasCaverns);
        TalkTo("Wavering_Flux");
        Check("Cirdas Caverns' Wavering Flux: event 34", $"034:{NpcId("Wavering_Flux")}:34:270,0,20,100", Ev());
        EventEnd(34);
        Check("  Status bit 2", "7", YggStatus());
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("YGGDRASIL_BECKONS")}]Status', 15)");
        WarpIn(300.0f, 29.998f, -79.0f, CirdasCaverns);
        Check("bit 3 (GM, Cirdas Caverns [U] unscripted): into Cirdas Caverns, the zone-in cutscene 36", "36", Cur());
        EventEnd(36);
        Check("  Dhokmak's Blood Sigil with Ashrakk's: Yggdrasil Beckons complete, Returning to the Trees current",
              $"10/{SoaId("RETURNING_TO_THE_TREES")},0", $"{DbKeyItem(Ki("DHOKMAKS_BLOOD_SIGIL"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 5-2-1 Returning to the Trees");
        WarpIn(-27.2f, -2.0f, 31.5f, Leafallia);
        Check("into Leafallia: the zone-in cutscene 11", "11", Cur());
        EventEnd(11);
        Check("  complete, The Key to the Turris current", $"{SoaId("THE_KEY_TO_THE_TURRIS")},0", Mission(LogSoa));

        Console.WriteLine("SoA 5-2-2 The Key to the Turris");
        Warp(0.0f, 0.0f, 0.0f, RaKaznarTurris);
        TalkTo("Ominous_Postern");
        Check("the Ominous Postern: event 1", $"034:{NpcId("Ominous_Postern")}:1:0,3530831,2964,627,277,1", Ev());
        EventEnd(1);
        Rezone(false);
        Check("  Status 1, to Kamihr Drifts: the zone-in cutscene 58", $"{KamihrDrifts}/58", $"{c.Zone}/{Cur()}");
        EventEnd(58);
        Check("  complete, Teodor's Summons current", $"{SoaId("TEODORS_SUMMONS")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 5-3 Teodor's Summons");
        TalkTo("Alpine_Trail");
        Check("the Alpine Trail: event 61", $"034:{NpcId("Alpine_Trail")}:61:267,0,0,0,144546,{uint.MaxValue - 1728},635049", Ev());
        EventEnd(61, 1);
        Rezone(false);
        Check("  option 1, Status 1, to Mount Kamihr: the zone-in cutscene 7", $"{MountKamihr}/7", $"{c.Zone}/{Cur()}");
        EventEnd(7, 1, 1);
        Check("  7's update: updateEvent(10, 23, 1756, 0, 0, 1)", "10,23,1756,0,0,1", Upd());
        EventEnd(7);
        Rezone();
        Check("  complete, back to Kamihr Drifts: the Ash Runic Board, The Seventh Guardian current",
              $"{KamihrDrifts}/10/{SoaId("THE_SEVENTH_GUARDIAN")},0", $"{c.Zone}/{DbKeyItem(Ki("ASH_RUNIC_BOARD"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 5-3-1 The Seventh Guardian");
        Warp(-318.0f, -1.0f, -316.0f, RalaWaterways);
        TalkTo("Yeggha_Dolashi");
        Check("Yeggha Dolashi: event 374", $"034:{NpcId("Yeggha_Dolashi")}:374:258,3642015,1756,0,{uint.MaxValue - 133696},{uint.MaxValue - 796},433102,8", Ev());
        EventEnd(374, 1, 1);
        Check("  374's update (option 1)", $"1,0,1756,0,{uint.MaxValue - 796},433102,8", Upd());
        EventEnd(374);
        Check("  complete, Watery Grave current", $"{SoaId("WATERY_GRAVE")},0", Mission(LogSoa));

        Console.WriteLine("SoA 5-3-2 Watery Grave");
        // afterZoneIn: the Ash Runic Board given (the Blank board taken) when not held, Status 0 and the Timer passed
        Lua("player:delKeyItem(xi.keyItem.ASH_RUNIC_BOARD)");
        Warp(-318.0f, -1.0f, -316.0f, CelenniaMemorialLibrary);
        Warp(-318.0f, -1.0f, -316.0f, RalaWaterways);
        Tick(4100);
        Check("the board lost, into Rala Waterways: given again after the zone-in", "10", DbKeyItem(Ki("ASH_RUNIC_BOARD")));
        // Rala Waterways [U]'s event 1000 is the script's assumption, no instance scripted: Status 1 a GM's
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("WATERY_GRAVE")}]Status', 1)");
        WarpIn(256.0f, -5.768f, 60.0f, RalaWaterways);
        Check("Status 1 (GM): into Rala Waterways, the zone-in cutscene 375", "375", Cur());
        EventEnd(375);
        Check("  complete: the Aged Undying Naakual Crest, Teodor's Blood Sigil, the Crystallized Psyche (THREE_BLOOD_SIGILS_PULSE); Blood for Blood current",
              $"10/10/10/True/{SoaId("BLOOD_FOR_BLOOD")},0",
              $"{DbKeyItem(Ki("AGED_UNDYING_NAAKUAL_CREST"))}/{DbKeyItem(Ki("TEODORS_BLOOD_SIGIL"))}/{DbKeyItem(Ki("CRYSTALLIZED_PSYCHE"))}/" +
              $"{Special().Contains($"{Text(RalaWaterways, "THREE_BLOOD_SIGILS_PULSE")}:")}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 5-3-3 Blood for Blood");
        Warp(0.0f, 0.0f, 0.0f, RaKaznarTurris);
        TalkTo("Ominous_Postern");
        Check("the Ominous Postern: event 2", $"034:{NpcId("Ominous_Postern")}:2:277,3823678,1756,0,277,1", Ev());
        EventEnd(2);
        Check("  complete, Reckoning current", $"{SoaId("RECKONING")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 5-4 Reckoning");
        Lua("player:delKeyItem(xi.keyItem.CRYSTALLIZED_PSYCHE)");
        Warp(0.0f, 0.0f, 0.0f, RaKaznarInnerCourt);
        Tick(4100);
        Check("the Crystallized Psyche lost, into Ra'Kaznar Inner Court: given again after the zone-in", "10", DbKeyItem(Ki("CRYSTALLIZED_PSYCHE")));
        // the battlefield's 32001 (no Ra'Kaznar Turris battlefield scripted): Status 1 a GM's
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("RECKONING")}]Status', 1)");
        WarpIn(132.2f, 39.75f, 20.0f, RaKaznarTurris);
        Check("Status 1 (GM): into Ra'Kaznar Turris, the zone-in cutscene 3", "3", Cur());
        EventEnd(3);
        Check("  complete: the Awakened Crystallized Psyche, Abomination current", $"10/{SoaId("ABOMINATION")},0",
              $"{DbKeyItem(Ki("AWAKENED_CRYSTALLIZED_PSYCHE"))}/{Mission(LogSoa)}");

        Console.WriteLine("SoA 5-4-1 Abomination");
        Lua("player:delKeyItem(xi.keyItem.AWAKENED_CRYSTALLIZED_PSYCHE)");
        Warp(0.0f, 0.0f, 0.0f, RaKaznarInnerCourt);
        Tick(4100);
        Check("the Awakened Psyche lost, into Ra'Kaznar Inner Court: given again after the zone-in", "10", DbKeyItem(Ki("AWAKENED_CRYSTALLIZED_PSYCHE")));
        Lua($"player:setCharVar('Mission[{LogSoa}][{SoaId("ABOMINATION")}]Status', 1)");
        WarpIn(132.2f, 39.75f, 20.0f, RaKaznarTurris);
        Check("Status 1 (GM): into Ra'Kaznar Turris, the zone-in cutscene 4", "4", Cur());
        EventEnd(4);
        Rezone(false);
        Check("  Status 2, to Ceizak Battlegrounds: the zone-in cutscene 29", $"{CeizakBattlegrounds}/29", $"{c.Zone}/{Cur()}");
        EventEnd(29);
        Check("  complete, Undying Light current", $"{SoaId("UNDYING_LIGHT")},0", Mission(LogSoa));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("SoA 5-5 Undying Light");
        TakeZoneLine(LineTo(WesternAdoulin), false);
        Check("from Ceizak Battlegrounds through the gate: Western Adoulin's zone-in cutscene 182", $"{WesternAdoulin}/182", $"{c.Zone}/{Cur()}");
        EventEnd(182, 1, 1);
        Check("  182's update (option 1)", $"5,0,{uint.MaxValue - 1},975,4063,1487,1999,4", Upd());
        EventEnd(182, 9, 1);
        Check("  option 9", "0,0,1,0,0,0,1107628388,4", Upd());
        EventEnd(182);
        Check("  Status 1", "1", AVar(SoaId("UNDYING_LIGHT"), "Status"));
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk: event 1547", $"034:{NpcId("Ploh_Trishbahk")}:1547:0,23,2964,0,33554431,138785268,3967", Ev());
        EventEnd(1547);
        Rezone(false);
        Check("  Status 2, set back: the zone-in cutscene 1549", "1549", Cur());
        EventEnd(1549);
        Check("  Undying Light complete, The Light Within current with its Timer and Status 1",
              $"{SoaId("THE_LIGHT_WITHIN")},0/{VanaTime.CountDays(vanaTicks) + 1}/1",
              $"{Mission(LogSoa)}/{AVar(SoaId("THE_LIGHT_WITHIN"), "Timer")}/{AVar(SoaId("THE_LIGHT_WITHIN"), "Status")}");

        Console.WriteLine("SoA 5-5-1 The Light Within");
        string LightStatus() => AVar(SoaId("THE_LIGHT_WITHIN"), "Status");
        WarpIn(427.9f, 0.5f, 178.2f, CeizakBattlegrounds);
        Check("into Ceizak Battlegrounds before the Timer: no cutscene", "none", Cur());
        vanaTicks += VanaTime.DAY;
        WarpIn(427.9f, 0.5f, 178.2f, CeizakBattlegrounds);
        Check("  the next day: the zone-in cutscene 30", "30", Cur());
        EventEnd(30, 2, 1);
        Check("  30's update (option 2)", $"10,0,100,100,{uint.MaxValue - 68382},80,629273", Upd());
        EventEnd(30, 9, 1);
        Check("  option 9", "0,300,200,100,65955,80,629609", Upd());
        EventEnd(30);
        Rezone(false);
        Check("  Status 2: the Pristine Hair Ribbon taken (KEYITEM_LOST), Arciela's Skirt; to Leafallia, the zone-in cutscene 13",
              $"00/10/{Leafallia}/13", $"{DbKeyItem(Ki("PRISTINE_HAIR_RIBBON"))}/{DbKeyItem(Ki("ARCIELAS_SKIRT"))}/{c.Zone}/{Cur()}");
        EventEnd(13, 0, 1);
        Check("  13's update (option 0)", "281,3899944,1,0,6815078,4654893,4095,393328", Upd());
        EventEnd(13);
        Rezone();
        Check("  Status 3, back to Ceizak Battlegrounds", $"{CeizakBattlegrounds}/3", $"{c.Zone}/{LightStatus()}");
        Warp(100.6f, -40.2f, -61.8f, EasternAdoulin);
        TalkTo("Ploh_Trishbahk");
        Check("Ploh Trishbahk at Status 3: event 1555 (repeat 1)", $"034:{NpcId("Ploh_Trishbahk")}:1555:257,23,2964,0,1,7719,4095", Ev());
        EventEnd(1555, 19, 1);
        Check("  1555's update (option 19)", "19,23,2964,2,2,200,2537,8449", Upd());
        EventEnd(1555, 19);
        Check("  option 19: Status 4", "4", LightStatus());
        TalkTo("Ploh_Trishbahk");
        Check("  again: event 1555 (repeat 2)", $"034:{NpcId("Ploh_Trishbahk")}:1555:257,23,2964,0,2,7719,4095", Ev());
        EventEnd(1555, 13, 1);
        Check("  option 13", "14,6,0,0,1,0,4480,12190", Upd());
        EventEnd(1555, 5, 1);
        Check("  option 5", "5,0,2,4000,1,0,0,4", Upd());
        ushort renaye = ushort.Parse(Lua("report(xi.item.RENAYE_RING)"));
        ushort garb = ushort.Parse(Lua("report(xi.item.COUNCILORS_GARB)"));
        ushort cuffs = ushort.Parse(Lua("report(xi.item.COUNCILORS_CUFFS)"));
        EventEnd(1555, 5);
        Check("  option 5: the Renaye Ring, the Councilor's Garb and Cuffs; Status reset, The Light Within still current (never completed)",
              $"1/1/1/-/{SoaId("THE_LIGHT_WITHIN")},0", $"{Qty(renaye)}/{Qty(garb)}/{Qty(cuffs)}/{LightStatus()}/{Mission(LogSoa)}");
        TalkTo("Ploh_Trishbahk");
        Check("  again: not 1555", "False", Cur() == "1555");
        EndAny();
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));


        // ---- Rhapsodies of Vana'diel ------------------------------------------------------------------------------
        Console.WriteLine("Rhapsodies of Vana'diel: the login in Lower Jeuno");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[2];
        Fixture(db, LowerJeuno, LowerJeuno, -36.0f, -1.0f, -13.0f, 0);
        Login();
        const ushort Selbina = 248, Norg = 252, KonschtatHighlands = 108, LaTheinePlateau = 102, QufimIsland = 126, EschaZitah = 288,
                     SeaSerpentGrotto = 176, YuhtungaJungle = 123;
        int RovId(string name) => int.Parse(Lua($"report(xi.mission.id.rov.{name})"));
        string RVar(int mission, string name) => Var($"Mission[{LogRov}][{mission}]{name}");
        // the other logs' zone-in cutscenes kept out of the way, as scripts/tests/missions/rov.lua does
        Lua("player:addMission(xi.mission.log_id.ASA, xi.mission.id.asa.BURGEONING_DREAD) " +
            "player:addMission(xi.mission.log_id.ACP, xi.mission.id.acp.A_CRYSTALLINE_PROPHECY_FIN) " +
            "player:addMission(xi.mission.log_id.SOA, xi.mission.id.soa.ABOMINATION)");
        // addMission's overwriting warnings (the ACP log at its first mission, as CCharEntity has it)
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        Check("in Lower Jeuno, the RoV log at Rhapsodies of Vana'diel (the CCharEntity default 0)", $"{LowerJeuno}/0,0", $"{c.Zone}/{Mission(LogRov)}");

        Console.WriteLine("RoV 1-1 Rhapsodies of Vana'diel");
        WarpIn(-90.0f, 1.0f, -50.0f, SouthernSandoria);
        Check("into Southern San d'Oria: the zone-in cutscene 30035", $"{SouthernSandoria}/30035", $"{c.Zone}/{Cur()}");
        EventEnd(30035);
        Check("  complete, Resonance current", $"{RovId("RESONANCE")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-2 Resonance");
        WarpIn(17.0f, -3.0f, 52.0f, Selbina);
        Check("into Selbina: the zone-in cutscene 176", $"{Selbina}/176", $"{c.Zone}/{Cur()}");
        EventEnd(176);
        Check("  complete, Emissary from the Seas current with the Selbina path (status 1)", $"{RovId("EMISSARY_FROM_THE_SEAS")},1", Mission(LogRov));

        Console.WriteLine("RoV 1-3 Emissary from the Seas");
        TalkTo("Naillina");
        Check("Naillina on the Selbina path: event 14", $"034:{NpcId("Naillina")}:14:0,0,0,0,0,0,0,1", Ev());
        EventEnd(14, 0);
        Check("  option 0: not complete", $"{RovId("EMISSARY_FROM_THE_SEAS")},1", Mission(LogRov));
        TalkTo("Naillina");
        Cs("  again: option 1", 14, 1);
        Check("  complete, the path kept: Set Free current at status 1", $"{RovId("SET_FREE")},1", Mission(LogRov));

        Console.WriteLine("RoV 1-4 Set Free");
        // An Explorer's Footsteps' 40 (a progressEvent) outranks the mission's 181 while that quest is available (level 10
        // and up): the char at level 9 for the talk
        TalkTo("Abelard");
        Check("Abelard at 75: An Explorer's Footsteps' 40", "40", Cur());
        EndAny();
        Lua("player:setLevel(9)");
        TalkTo("Abelard");
        Cs("  at 9: event 181 (important once)", 181);
        TalkTo("Abelard");
        Check("  again: not 181", "False", Cur() == "181");
        EndAny();
        Lua("player:setLevel(75)");
        ushort pollen = ushort.Parse(Lua("report(xi.item.CLUMP_OF_BEE_POLLEN)"));
        Lua($"player:addItem({pollen}, 3)");
        Check("  no support job unlocked (hasJob(0))", "false", Lua("report(tostring(player:hasJob(0)))"));
        TradeTo("Abelard", (Slot(pollen), 3));
        Check("the three Clumps of Bee Pollen traded: event 178", $"034:{NpcId("Abelard")}:178", Ev());
        EventEnd(178);
        Check("  complete: the pollen taken, Gilgamesh's Introductory Letter, The Beginning current at status 1",
              $"0/10/{RovId("THE_BEGINNING")},1", $"{Qty(pollen)}/{DbKeyItem(Ki("GILGAMESHS_INTRODUCTORY_LETTER"))}/{Mission(LogRov)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 1-5 The Beginning");
        TalkTo("Pacomart");
        Check("Pacomart: event 179", $"032:{NpcId("Pacomart")}:179", Ev());
        EventEnd(179, 1);
        Rezone();
        Check("  option 1: to Norg", $"{Norg}", $"{c.Zone}");
        TalkTo("Comitiolus");
        Check("Comitiolus: DIDYA_GET_BUMPED (priority 1005)", $"none/{Text(Norg, "DIDYA_GET_BUMPED")}", $"{Cur()}/{Special().Split(':')[0]}");
        TalkTo("_700");
        Check("the door to Gilgamesh: event 276", $"032:{NpcId("_700")}:276", Ev());
        EventEnd(276);
        Check("  complete: the Reisenjima Sanctorium Orb, Flames of Prayer current", $"10/{RovId("FLAMES_OF_PRAYER")},0",
              $"{DbKeyItem(Ki("REISENJIMA_SANCTORIUM_ORB"))}/{Mission(LogRov)}");

        Console.WriteLine("RoV 1-6 Flames of Prayer");
        TalkTo("_700");
        Check("the door: event 277 (rank below 3: param 7 = 0)", $"032:{NpcId("_700")}:277", Ev());
        EventEnd(277);
        Check("  complete: the Rhapsody in White, The Path Untraveled current", $"10/{RovId("THE_PATH_UNTRAVELED")},0",
              $"{DbKeyItem(Ki("RHAPSODY_IN_WHITE"))}/{Mission(LogRov)}");

        Console.WriteLine("RoV 1-7 The Path Untraveled");
        TalkTo("_700");
        Check("the door: DOOR_IS_LOCKED", $"none/{Text(Norg, "DOOR_IS_LOCKED")}", $"{Cur()}/{Special().Split(':')[0]}");
        Warp(-260.0f, 7.0f, -300.0f, LaTheinePlateau);
        TalkTo("Shattered_Telepoint");
        Check("La Theine's Shattered Telepoint: event 14 (no Celestial Nexus: param 7 = 0)", $"032:{NpcId("Shattered_Telepoint")}:14", Ev());
        EventEnd(14);
        Check("  complete, At the Heaven's Door current", $"{RovId("AT_THE_HEAVENS_DOOR")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-8 At the Heaven's Door");
        Warp(-250.0f, -20.0f, 300.0f, QufimIsland);
        TalkTo("Undulating_Confluence");
        Check("Qufim's Undulating Confluence: event 63", $"032:{NpcId("Undulating_Confluence")}:63", Ev());
        EventEnd(63);
        Check("  complete, The Lion's Roar current", $"{RovId("THE_LIONS_ROAR")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-9 The Lion's Roar");
        uint ophiotaurus = MobOf("QUFIM_ISLAND", "OPHIOTAURUS");
        TalkTo("Undulating_Confluence");
        Check("the Confluence: Ophiotaurus popped, SENSE_OF_FOREBODING", $"True/{Text(QufimIsland, "SENSE_OF_FOREBODING")}",
              $"{zone.ZoneInfoOf(c.Zone).MobById[ophiotaurus].IsSpawned}/{Special().Split(':')[0]}");
        // a Message (below Event priority) alternates with the NPC's own onTrigger: its Escha - Zi'Tah offer
        TalkTo("Undulating_Confluence");
        Cs("  again: the Confluence's own event 65 (the alternation), declined", 65);
        TalkTo("Undulating_Confluence");
        Check("  again while it is up: WAIT_A_BIT_LONGER", $"{Text(QufimIsland, "WAIT_A_BIT_LONGER")}", Special().Split(':')[0]);
        Lua("player:setUnkillable(true)");
        KillByChar(ophiotaurus);
        Lua("player:setUnkillable(false)");
        Check("  Ophiotaurus killed by the char: complete, Eddies of Despair I current", $"{RovId("EDDIES_OF_DESPAIR_I")},0", Mission(LogRov));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 1-10 Eddies of Despair I");
        TalkTo("Undulating_Confluence");
        Check("the Confluence: event 64", $"032:{NpcId("Undulating_Confluence")}:64", Ev());
        EventEnd(64);
        Rezone(false);
        Check("  xi.teleport.to Escha - Zi'Tah: the zone-in cutscene 1", $"{EschaZitah}/1", $"{c.Zone}/{Cur()}");
        EventEnd(1, 1, 1);
        Check("  1's update (option 1): updateEvent with no Celestial Nexus (all 0)", "", Upd());
        EventEnd(1);
        Check("  complete, A Land After Time current", $"{RovId("A_LAND_AFTER_TIME")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-11 A Land After Time");
        ushort cipherLion = ushort.Parse(Lua("report(xi.item.CIPHER_OF_LIONS_ALTER_EGO_II)"));
        Warp(-260.0f, 7.0f, -300.0f, LaTheinePlateau);
        TalkTo("Shattered_Telepoint");
        Check("La Theine's Shattered Telepoint: event 15 (the zone, rank below 6)", $"034:{NpcId("Shattered_Telepoint")}:15:{LaTheinePlateau}", Ev());
        Check("  the rank 6 block remembered for 1-13", "1", Var("Mission[13][30]wasBlocked"));
        EventEnd(15);
        Check("  complete: the Cipher of Lion's alter ego II, the Rhapsody in Umber, Fate's Call current", $"1/10/{RovId("FATES_CALL")},0",
              $"{Qty(cipherLion)}/{DbKeyItem(Ki("RHAPSODY_IN_UMBER"))}/{Mission(LogRov)}");

        Console.WriteLine("RoV 1-12 Fate's Call");
        WarpIn(-90.0f, 1.0f, -50.0f, SouthernSandoria);
        Check("into Southern San d'Oria at rank 1: no 30036", "False", Cur() == "30036");
        EndAny();
        Lua("player:setRank(6)");
        WarpIn(-90.0f, 1.0f, -50.0f, LowerJeuno);
        EndAny();
        WarpIn(-90.0f, 1.0f, -50.0f, SouthernSandoria);
        Check("  at rank 6: the zone-in cutscene 30036", "30036", Cur());
        EventEnd(30036);
        Check("  complete, What Lies Beyond current", $"{RovId("WHAT_LIES_BEYOND")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-13 What Lies Beyond");
        Warp(-19.0f, 1.0f, -58.0f, Norg);
        TalkTo("Comitiolus");
        Cs("Comitiolus: event 6", 6);
        TalkTo("_700");
        Check("the door: event 278 with the rank 6 block (params[5] = 1, the table 0-based)", $"034:{NpcId("_700")}:278:0,0,0,0,0,1", Ev());
        EventEnd(278);
        Check("  complete, The Ties that Bind current", $"{RovId("THE_TIES_THAT_BIND")},0", Mission(LogRov));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 1-14 The Ties that Bind");
        Warp(0.0f, 0.0f, 0.0f, SeaSerpentGrotto);
        TalkTo("qm7");
        Check("Sea Serpent Grotto's qm7: event 34", $"032:{NpcId("qm7")}:34", Ev());
        EventEnd(34);
        Check("  complete, Impurity current", $"{RovId("IMPURITY")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-15 Impurity");
        Warp(0.0f, 0.0f, 0.0f, YuhtungaJungle);
        TalkTo("qm11");
        Check("Yuhtunga Jungle's qm11: event 212", $"032:{NpcId("qm11")}:212", Ev());
        EventEnd(212);
        Check("  complete, The Lost Avatar current", $"{RovId("THE_LOST_AVATAR")},0", Mission(LogRov));

        Console.WriteLine("RoV 1-16 The Lost Avatar");
        uint siren = MobOf("YUHTUNGA_JUNGLE", "SIREN");
        TalkTo("qm11");
        Check("qm11: the Siren popped, SENSE_OF_FOREBODING", $"True/{Text(YuhtungaJungle, "SENSE_OF_FOREBODING")}",
              $"{zone.ZoneInfoOf(c.Zone).MobById[siren].IsSpawned}/{Special().Split(':')[0]}");
        TalkTo("qm11");
        Check("  again while it is up: NOW_IS_NOT_THE_TIME", $"{Text(YuhtungaJungle, "NOW_IS_NOT_THE_TIME")}", Special().Split(':')[0]);
        Lua("player:setUnkillable(true)");
        KillByChar(siren);
        Lua("player:setUnkillable(false)");
        Check("  the Siren killed by the char: status 1", $"{RovId("THE_LOST_AVATAR")},1", Mission(LogRov));
        TalkTo("qm11");
        Check("qm11 at status 1: event 213", $"032:{NpcId("qm11")}:213", Ev());
        EventEnd(213);
        Check("  complete: the Rhapsody in Azure, Volto Oscuro current", $"10/{RovId("VOLTO_OSCURO")},0",
              $"{DbKeyItem(Ki("RHAPSODY_IN_AZURE"))}/{Mission(LogRov)}");

        Console.WriteLine("RoV 1-17 Volto Oscuro");
        ushort cipherZeid = ushort.Parse(Lua("report(xi.item.CIPHER_OF_ZEIDS_ALTER_EGO_II)"));
        Warp(-19.0f, 1.0f, -58.0f, Norg);
        TalkTo("_700");
        Check("the door: event 279 (Tenzen not met: param 7 = 0)", $"032:{NpcId("_700")}:279", Ev());
        EventEnd(279);
        Check("  complete: the Cipher of Zeid's alter ego II, Ring My Bell current", $"1/{RovId("RING_MY_BELL")},0",
              $"{Qty(cipherZeid)}/{Mission(LogRov)}");

        Console.WriteLine("RoV 1-18 Ring My Bell");
        // Tenzen away on A Vessel Without a Captain (xi.rhapsodies.unavailability): 283
        Lua("player:addMission(xi.mission.log_id.COP, xi.mission.id.cop.A_VESSEL_WITHOUT_A_CAPTAIN)");
        TalkTo("_700");
        Check("the door, Tenzen unavailable (CoP A Vessel Without a Captain): event 283", $"032:{NpcId("_700")}:283", Ev());
        EventEnd(283);
        Check("  not complete", $"{RovId("RING_MY_BELL")},0", Mission(LogRov));
        Lua("player:addMission(xi.mission.log_id.COP, xi.mission.id.cop.THE_ROAD_FORKS)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        TalkTo("_700");
        Check("  CoP at The Road Forks: event 284 (Tenzen 1, Prishe 1)", $"034:{NpcId("_700")}:284:1,1", Ev());
        EventEnd(284);
        Check("  complete, Spirits Awoken current", $"{RovId("SPIRITS_AWOKEN")},0", Mission(LogRov));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        const ushort LowerDelkfuttsTower = 184, RuludeGardens = 243, PortJeuno = 246, AhtUrhganWhitegate = 50;
        Console.WriteLine("RoV 2-1 Spirits Awoken");
        Warp(-250.0f, -20.0f, 300.0f, QufimIsland);
        TakeZoneLine(LineTo(LowerDelkfuttsTower), false);
        Check("from Qufim Island into Lower Delkfutt's Tower: the zone-in cutscene 51", $"{LowerDelkfuttsTower}/51", $"{c.Zone}/{Cur()}");
        EventEnd(51, 1, 1);
        Check("  51's update (option 1): CoP past A Vessel Without a Captain (1)", "0,1", Upd());
        EventEnd(51);
        Check("  complete, Crashing Waves current", $"{RovId("CRASHING_WAVES")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-2 Crashing Waves");
        ushort cipherTenzen = ushort.Parse(Lua("report(xi.item.CIPHER_OF_TENZENS_ALTER_EGO_II)"));
        // the inventory filled first (Bronze Swords, one a slot): the Mystic Retriever path
        Lua("while player:getFreeSlotsCount() > 0 do player:addItem(xi.item.BRONZE_SWORD) end");
        Warp(0.0f, 2.0f, 20.0f, RuludeGardens);
        Enter(0.0f, 3.0f, 50.0f);
        // the four 0 params given: 0x034 (StartEventHelper keeps every number passed, CCharEntity sends EVENTNUM for any)
        Check("Rulude Gardens' trigger area 1 (the palace entrance), CoP before Darkness Named: event 10244", $"034:{c.Id}:10244", Ev());
        EventEnd(10244);
        Check("  no free slot: MYSTIC_RETRIEVER with the cipher, Retrieve 1, not complete",
              $"{Text(RuludeGardens, "MYSTIC_RETRIEVER")}:{cipherTenzen}/1/{RovId("CRASHING_WAVES")},0",
              $"{Special()}/{RVar(RovId("CRASHING_WAVES"), "Retrieve")}/{Mission(LogRov)}");
        Lua("player:delItem(xi.item.BRONZE_SWORD, 1)");
        TalkTo("Mystic_Retriever");
        Check("the Mystic Retriever with a slot free: the cipher, Crashing Waves complete, Call to Serve current",
              $"1/{RovId("CALL_TO_SERVE")},0", $"{Qty(cipherTenzen)}/{Mission(LogRov)}");
        Lua("while player:hasItem(xi.item.BRONZE_SWORD) do player:delItem(xi.item.BRONZE_SWORD, 1) end");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 2-3 Call to Serve");
        ushort cipherPrishe = ushort.Parse(Lua("report(xi.item.CIPHER_OF_PRISHES_ALTER_EGO_II)"));
        WarpIn(-30.0f, 0.0f, 0.0f, PortJeuno);
        Check("into Port Jeuno: the zone-in cutscene 399", $"{PortJeuno}/399", $"{c.Zone}/{Cur()}");
        EventEnd(399, 1, 1);
        Check("  399's update (option 1): Darkness Named not completed", "0,0,1,1", Upd());
        EventEnd(399);
        Check("  a slot free: the cipher, complete, Numbering Days current", $"1/{RovId("NUMBERING_DAYS")},0", $"{Qty(cipherPrishe)}/{Mission(LogRov)}");

        Console.WriteLine("RoV 2-4 Numbering Days");
        Warp(0.0f, 0.0f, 0.0f, UpperJeuno);
        TalkTo("_6s1");
        Check("Upper Jeuno's _6s1: event 10221", $"034:{NpcId("_6s1")}:10221:0,0,1", Ev());
        EventEnd(10221);
        Check("  complete, Inescapable Binds current", $"{RovId("INESCAPABLE_BINDS")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-5 Inescapable Binds");
        // the section's check asks for the Boarding Permit (a GM's here; The Road to Aht Urhgan's reward)
        Lua("player:addKeyItem(xi.keyItem.BOARDING_PERMIT)");
        WarpIn(-100.0f, 0.0f, 50.0f, AhtUrhganWhitegate);
        Check("into Aht Urhgan Whitegate with the permit: the zone-in cutscene 165", $"{AhtUrhganWhitegate}/165", $"{c.Zone}/{Cur()}");
        EventEnd(165, 0, 1);
        Check("  165's update: no ToAU or CoP progress", "", Upd());
        EventEnd(165);
        Check("  complete, Ever Forward current", $"{RovId("EVER_FORWARD")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-7 Ever Forward");
        ushort cipherNash = ushort.Parse(Lua("report(xi.item.CIPHER_OF_NASHMEIRAS_ALTER_EGO_II)"));
        TalkTo("Imperial_Whitegate");
        Check("the Imperial Whitegate before Royal Puppeteer: UNABLE_TO_PROGRESS_ROV", $"none/{Text(AhtUrhganWhitegate, "UNABLE_TO_PROGRESS_ROV")}",
              $"{Cur()}/{Special().Split(':')[0]}");
        Lua("player:addMission(xi.mission.log_id.TOAU, xi.mission.id.toau.ROYAL_PUPPETEER)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        WarpIn(-90.0f, 1.0f, -50.0f, SouthernSandoria);
        Check("ToAU at Royal Puppeteer, into Southern San d'Oria: the zone-in cutscene 30039", "30039", Cur());
        EventEnd(30039);
        Check("  Ever Forward complete, Aphmau's Light current", $"True/{RovId("APHMAUS_LIGHT")},0",
              $"{Lua($"report(tostring(player:hasCompletedMission({LogRov}, {RovId("EVER_FORWARD")})))") == "true"}/{Mission(LogRov)}");

        Console.WriteLine("RoV 2-9 Aphmau's Light");
        Warp(-100.0f, 0.0f, 50.0f, AhtUrhganWhitegate);
        TalkTo("Imperial_Whitegate");
        Check("the Imperial Whitegate: event 166 (text table 0: EVENTNUM)", $"034:{NpcId("Imperial_Whitegate")}:166/0", $"{Ev()}/{EvTable()}");
        EventEnd(166);
        Check("  complete: the Cipher of Nashmeira's alter ego II, Reunited current", $"1/{RovId("REUNITED")},0",
              $"{Qty(cipherNash)}/{Mission(LogRov)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        const ushort AlzadaalUnderseaRuins = 72, TheShroudedMaw = 10;
        Console.WriteLine("RoV 2-10 Reunited");
        TalkTo("Nadeey");
        Check("Nadeey: event 167 with the Astral Candescence (text table 0)",
              $"034:{NpcId("Nadeey")}:167{(Lua("report(xi.besieged.getAstralCandescence())") is var ac && ac != "0" ? ":" + ac : "")}/0", $"{Ev()}/{EvTable()}");
        EventEnd(167);
        Check("  complete, Take Wing current", $"{RovId("TAKE_WING")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-11 Take Wing");
        Enter(80.0f, -6.0f, -130.0f);
        Check("Whitegate's trigger area 5 (the Shaharat Teahouse), The Warrior's Path not done: event 168", $"034:{c.Id}:168/0", $"{Ev()}/{EvTable()}");
        EventEnd(168);
        Check("  complete, Prime Number current", $"{RovId("PRIME_NUMBER")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-12 Prime Number");
        WarpIn(0.0f, 0.0f, 0.0f, AlzadaalUnderseaRuins);
        Check("into Alzadaal Undersea Ruins: the zone-in cutscene 124", $"{AlzadaalUnderseaRuins}/124", $"{c.Zone}/{Cur()}");
        EventEnd(124);
        Check("  complete with Option 1, From the Ruins current", $"1/{RovId("FROM_THE_RUINS")},0", $"{RVar(RovId("PRIME_NUMBER"), "Option")}/{Mission(LogRov)}");
        WarpIn(0.0f, 0.0f, 0.0f, TheShroudedMaw);
        Check("into the Shrouded Maw with Option 1: the zone-in cutscene 12", $"{TheShroudedMaw}/12", $"{c.Zone}/{Cur()}");
        EventEnd(12);
        Check("  Option cleared", "-", RVar(RovId("PRIME_NUMBER"), "Option"));

        Console.WriteLine("RoV 2-13 From the Ruins");
        // param 1 is hasCompletedMission's boolean: a table entry that is not a number reads 0 (sol's get_or<uint32>)
        Lua("player:addMission(xi.mission.log_id.WOTG, xi.mission.id.wotg.THE_WILL_OF_THE_WORLD) " +
            "player:completeMission(xi.mission.log_id.WOTG, xi.mission.id.wotg.THE_WILL_OF_THE_WORLD)");
        Warp(-100.0f, 0.0f, 50.0f, AhtUrhganWhitegate);
        TalkTo("Imperial_Whitegate");
        Check("the Imperial Whitegate, The Will of the World completed: event 169 (the boolean param read 0)",
              $"true/034:{NpcId("Imperial_Whitegate")}:169/0",
              $"{Lua($"report(tostring(player:hasCompletedMission({LogWotg}, xi.mission.id.wotg.THE_WILL_OF_THE_WORLD)))")}/{Ev()}/{EvTable()}");
        EventEnd(169);
        Check("  complete: the Rhapsody in Crimson, Cauterize current", $"10/{RovId("CAUTERIZE")},0",
              $"{DbKeyItem(Ki("RHAPSODY_IN_CRIMSON"))}/{Mission(LogRov)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 2-14 Cauterize");
        Warp(0.0f, 0.0f, 0.0f, BatalliaDowns);
        TalkTo("qm_maw");
        Check("Batallia's qm_maw, Cait Sith unavailable (WotG Cavernous Maws): UNABLE_TO_PROGRESS", $"none/{Text(BatalliaDowns, "UNABLE_TO_PROGRESS")}:0",
              $"{Cur()}/{Special()}");
        Lua("player:addMission(xi.mission.log_id.WOTG, xi.mission.id.wotg.FORK_IN_THE_ROAD)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        TalkTo("qm_maw");
        Check("  WotG at Fork in the Road: event 20 (param 1 = 1)", $"034:{NpcId("qm_maw")}:20:0,1,0,{uint.MaxValue - 1},436377,183,624793,21299", Ev());
        EventEnd(20);
        Check("  complete: the Lightsworm; Uncertain Destinations added and completed, Ganged Up On current",
              $"10/True/{RovId("GANGED_UP_ON")},0",
              $"{DbKeyItem(Ki("LIGHTSWORM"))}/{Lua($"report(tostring(player:hasCompletedMission({LogRov}, {RovId("UNCERTAIN_DESTINATIONS")})))") == "true"}/{Mission(LogRov)}");

        Console.WriteLine("RoV 2-16 Ganged Up On");
        ushort cipherLilisette = ushort.Parse(Lua("report(xi.item.CIPHER_OF_LILISETTES_ALTER_EGO_II)"));
        WarpIn(-90.0f, 1.0f, -50.0f, SouthernSandoriaS);
        Check("into Southern San d'Oria [S], Lilisette available: the zone-in cutscene 183", $"{SouthernSandoriaS}/183", $"{c.Zone}/{Cur()}");
        EventEnd(183, 1, 1);
        Check("  183's update (option 1): WotG at Fork in the Road, at or past Fate in Haze's id (1, 1)", "1,1", Upd());
        EventEnd(183);
        Check("  a slot free: BONDS_STRENGTHENED, the cipher (ITEM_OBTAINED); complete, Sacrifice current",
              $"{Text(SouthernSandoriaS, "BONDS_STRENGTHENED")}:3,{Text(SouthernSandoriaS, "ITEM_OBTAINED")}:{cipherLilisette}/1/{RovId("SACRIFICE")},0", $"{Special()}/{Qty(cipherLilisette)}/{Mission(LogRov)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        const ushort MisareauxCoast = 25, TempleOfUggalepih = 159;
        Console.WriteLine("RoV 2-17 Sacrifice");
        WarpIn(0.0f, 0.0f, 0.0f, WalkOfEchoes);
        Check("into the Walk of Echoes, WotG at Fork in the Road (not before Fate in Haze): no 26", "False", Cur() == "26");
        EndAny();
        TalkTo("_521");
        Check("the Walk's _521: event 27", $"034:{NpcId("_521")}:27:0,0,0,3000", Ev());
        EventEnd(27);
        Rezone(false);
        Check("  Status 2, to Grauberg [S]: the zone-in cutscene 47", $"{GraubergS}/47", $"{c.Zone}/{Cur()}");
        EventEnd(47);
        Check("  complete, Somber Dreams current", $"{RovId("SOMBER_DREAMS")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-18 Somber Dreams");
        string SomberStatus() => RVar(RovId("SOMBER_DREAMS"), "Status");
        TalkTo("qm_cetus");
        uint cetus = uint.Parse(Lua("report(player:getZone():queryEntitiesByName('Cetus')[1]:getID())"));
        Check("Grauberg [S]'s qm_cetus: Cetus spawned and claimed, SENSE_OF_FOREBODING",
              $"True/{c.Id}/{Text(GraubergS, "SENSE_OF_FOREBODING")}",
              $"{zone.ZoneInfoOf(c.Zone).MobById[cetus].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[cetus].OwnerId}/{Special().Split(':')[0]}");
        Lua("player:setUnkillable(true)");
        KillByChar(cetus);
        Lua("player:setUnkillable(false)");
        Check("  Cetus killed by the char: Status 1, Abyssal Purveyor", "1/True", $"{SomberStatus()}/{DbTitle(TitleId("ABYSSAL_PURVEYOR"))}");
        TalkTo("qm_cetus");
        Cs("qm_cetus at Status 1: event 48", 48);
        Check("  Status 2", "2", SomberStatus());
        TalkTo("qm_conflux");
        Check("qm_conflux: event 49", $"034:{NpcId("qm_conflux")}:49:89,23,2964,0,0,6881300", Ev());
        EventEnd(49, 99);
        Rezone(false);
        Check("  option 99, to the Walk of Echoes from Grauberg [S]: the zone-in cutscene 28", $"{WalkOfEchoes}/28", $"{c.Zone}/{Cur()}");
        EventEnd(28, 1, 1);
        Check("  28's update (option 1): Maiden of the Dusk not completed (0)", "", Upd());
        EventEnd(28, 1);
        Check("  option 1: complete, Of Light and Darkness current", $"{RovId("OF_LIGHT_AND_DARKNESS")},0", Mission(LogRov));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 2-19 Of Light and Darkness");
        Warp(-19.0f, 1.0f, -58.0f, Norg);
        TalkTo("_700");
        Check("Norg's door: event 285", $"032:{NpcId("_700")}:285", Ev());
        EventEnd(285);
        Check("  complete, Temporary Farewells current", $"{RovId("TEMPORARY_FAREWELLS")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-20 Temporary Farewells");
        Warp(0.0f, 0.0f, 0.0f, MisareauxCoast);
        TalkTo("qm_rov2_20");
        Check("Misareaux Coast's qm_rov2_20: event 15", $"034:{NpcId("qm_rov2_20")}:15:25,23,2964", Ev());
        EventEnd(15);
        Check("  complete, Brushing Up current", $"{RovId("BRUSHING_UP")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-21 Brushing Up");
        TalkTo("qm_rov2_20");
        Check("qm_rov2_20: event 16", $"034:{NpcId("qm_rov2_20")}:16:25", Ev());
        EventEnd(16, 0);
        Check("  option 0: not complete", $"{RovId("BRUSHING_UP")},0", Mission(LogRov));
        TalkTo("qm_rov2_20");
        Cs("  again: option 2 (the Zaru Soba)", 16, 2);
        Check("  complete, Keep On Giving current with Option 1", $"{RovId("KEEP_ON_GIVING")},0/1",
              $"{Mission(LogRov)}/{RVar(RovId("KEEP_ON_GIVING"), "Option")}");

        Console.WriteLine("RoV 2-22 Keep On Giving");
        TalkTo("qm_rov2_20");
        Check("qm_rov2_20: event 19 with the Option", $"034:{NpcId("qm_rov2_20")}:19:1", Ev());
        EventEnd(19);
        ushort soba = ushort.Parse(Lua("report(xi.item.SERVING_OF_ZARU_SOBA)"));
        ushort stewpot = ushort.Parse(Lua("report(xi.item.BEEF_STEWPOT)"));
        Lua($"player:addItem({soba}) player:addItem({stewpot})");
        TradeTo("qm_rov2_20", (Slot(stewpot), 1));
        Check("  the Beef Stewpot traded (not the Option's): no event", "none", Cur());
        TradeTo("qm_rov2_20", (Slot(soba), 1));
        Check("  the Serving of Zaru Soba traded: event 17 (no Celestial Nexus: 1)", $"034:{NpcId("qm_rov2_20")}:17:1,23", Ev());
        EventEnd(17);
        Check("  complete, the soba taken, the stewpot kept; Past Imperfect current", $"0/1/{RovId("PAST_IMPERFECT")},0",
              $"{Qty(soba)}/{Qty(stewpot)}/{Mission(LogRov)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("RoV 2-23 Past Imperfect");
        Warp(-19.0f, 1.0f, -58.0f, Norg);
        TalkTo("_700");
        Check("Norg's door, Tenzen available, ZM4 not done: event 286 (0)", $"034:{NpcId("_700")}:286", Ev());
        EventEnd(286);
        Check("  complete, The Cursed Temple current", $"{RovId("THE_CURSED_TEMPLE")},0", Mission(LogRov));

        Console.WriteLine("RoV 2-24 The Cursed Temple");
        Warp(0.0f, 0.0f, 0.0f, TempleOfUggalepih);
        TalkTo("_4fx");
        Check("Uggalepih's _4fx before ZM4: UNABLE_TO_PROGRESS_MISSION (4), no mission event",
              $"True/{Text(TempleOfUggalepih, "UNABLE_TO_PROGRESS_MISSION")}:4", $"{Cur() != "94"}/{Special().Split(',')[0]}");
        EndAny();
        Lua("player:addMission(xi.mission.log_id.ZILART, xi.mission.id.zilart.THE_TEMPLE_OF_UGGALEPIH) " +
            "player:completeMission(xi.mission.log_id.ZILART, xi.mission.id.zilart.THE_TEMPLE_OF_UGGALEPIH)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        WarpIn(0.0f, 0.0f, 0.0f, TempleOfUggalepih);
        Check("ZM4 completed (GM): into the temple, the zone-in cutscene 93", "93", Cur());
        EventEnd(93);
        Check("  Status 1", "1", RVar(RovId("THE_CURSED_TEMPLE"), "Status"));
        TalkTo("_4fx");
        Check("_4fx: event 94", $"034:{NpcId("_4fx")}:94:1,3,340159,{uint.MaxValue - 10},323822,{uint.MaxValue - 1813},640938", Ev());
        EventEnd(94);
        // the reward's nextMission then the script's own addMission of it: addMission's warning (as C++'s)
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        Check("  complete; Wisdom of Our Forefathers added and completed, Where Divinities Collide current (unscripted)",
              $"True/{RovId("WHERE_DIVINITIES_COLLIDE")},0",
              $"{Lua($"report(tostring(player:hasCompletedMission({LogRov}, {RovId("WISDOM_OF_OUR_FOREFATHERS")})))") == "true"}/{Mission(LogRov)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));


        // ---- A Crystalline Prophecy ---------------------------------------------------------------------------------
        // ACP 1 is a mission script; the rest are the zone and NPC scripts' legacy ones (the mission files 2-12 are
        // TODO stubs)
        Console.WriteLine("A Crystalline Prophecy: the login in Upper Jeuno");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[3];
        Fixture(db, UpperJeuno, UpperJeuno, -40.0f, 0.0f, 20.0f, 0);
        Login();
        const ushort FeiYin = 204, QuBiaArena = 206, RoMaeve = 122, HallOfTheGods = 251;
        int AcpId(string name) => int.Parse(Lua($"report(xi.mission.id.acp.{name})"));
        // the other logs' zone-in cutscenes kept out of the way
        Lua($"player:addMission(xi.mission.log_id.ROV, {RovId("WHERE_DIVINITIES_COLLIDE")}) " +
            "player:addMission(xi.mission.log_id.ASA, xi.mission.id.asa.BURGEONING_DREAD) " +
            "player:addMission(xi.mission.log_id.SOA, xi.mission.id.soa.ABOMINATION) " +
            "player:addMission(xi.mission.log_id.COP, xi.mission.id.cop.THE_ROAD_FORKS)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        Check("in Upper Jeuno, the ACP log at A Crystalline Prophecy", $"{UpperJeuno}/{AcpId("A_CRYSTALLINE_PROPHECY")},0", $"{c.Zone}/{Mission(LogAcp)}");
        // a mob of those names killed by the char until done (its onMobDeath's roll), the mob spawned again by the
        // zone's SpawnMob when it is down
        int KillUntil(Func<MobEntity, bool> which, Func<bool> done, int max = 150)
        {
            var info = zone.ZoneInfoOf(c.Zone);
            var ids = info.Mobs.Where(which).Select(m => m.Id).ToList();
            int kills = 0;
            Lua("player:setUnkillable(true)");
            for (int i = 0; ids.Count > 0 && kills < max && i < max * 4 && !done(); i++)
            {
                var m = info.MobById[ids[i % ids.Count]];
                if (!m.IsSpawned) Lua($"SpawnMob({m.Id})");
                if (!m.IsSpawned || !m.IsAlive) { Tick(1000); continue; }
                KillByChar(m.Id);
                kills++;
            }
            Lua("player:setUnkillable(false)");
            return kills;
        }

        Console.WriteLine("ACP 1 A Crystalline Prophecy");
        WarpIn(0.0f, 0.0f, 0.0f, LowerJeuno);
        Check("into Lower Jeuno at level 75: the zone-in cutscene 10094", $"{LowerJeuno}/10094", $"{c.Zone}/{Cur()}");
        EventEnd(10094, 1, 1);
        Check("  10094's update (option 1): Verena and Sibyl both in it (0, 0)", "", Upd());
        EventEnd(10094);
        Check("  complete, The Echo Awakens current", $"{AcpId("THE_ECHO_AWAKENS")},0", Mission(LogAcp));

        Console.WriteLine("ACP 2 The Echo Awakens (Qufim Island's qm3)");
        Warp(-120.3f, -19.5f, 306.7f, QufimIsland);
        Lua("player:addItem(xi.item.SEEDSPALL_LUX) player:addItem(xi.item.SEEDSPALL_LUNA) player:addItem(xi.item.SEEDSPALL_ASTRUM)");
        ushort lux = ushort.Parse(Lua("report(xi.item.SEEDSPALL_LUX)")), luna = ushort.Parse(Lua("report(xi.item.SEEDSPALL_LUNA)")),
               astrum = ushort.Parse(Lua("report(xi.item.SEEDSPALL_ASTRUM)"));
        TradeTo("qm3", (Slot(lux), 1), (Slot(luna), 1), (Slot(astrum), 1));
        Check("the Seedspall Lux, Luna and Astrum traded to qm3: event 31", $"032:{NpcId("qm3")}:31", Ev());
        EventEnd(31);
        Check("  The Echo Awakens complete, the three taken, Gatherer of Light I current", $"0/0/0/{AcpId("GATHERER_OF_LIGHT_I")},0",
              $"{Qty(lux)}/{Qty(luna)}/{Qty(astrum)}/{Mission(LogAcp)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ACP 3 Gatherer of Light I (the goblins' key items, the three NMs' Seedspalls)");
        (ushort Zone, string ZoneName, string Food, string Nm, string NmId, string Seed)[] gatherers =
        {
            (BatalliaDowns, "BATALLIA_DOWNS", "BOWL_OF_BLAND_GOBLIN_SALAD", "Vegnix_Greenthumb", "VEGNIX_GREENTHUMB", "SEEDSPALL_ROSEUM"),
            (RolanberryFields, "ROLANBERRY_FIELDS", "JUG_OF_GREASY_GOBLIN_JUICE", "Chuglix_Berrypaws", "CHUGLIX_BERRYPAWS", "SEEDSPALL_CAERULUM"),
            (SauromugueChampaign, "SAUROMUGUE_CHAMPAIGN", "CHUNK_OF_SMOKED_GOBLIN_GRUB", "Dribblix_Greasemaw", "DRIBBLIX_GREASEMAW", "SEEDSPALL_VIRIDIS"),
        };
        foreach (var g in gatherers)
        {
            Warp(0.0f, 0.0f, 0.0f, g.Zone);
            TalkTo("qm1");
            Check($"{g.Nm}'s qm1 without the {g.Food}: NOTHING_HAPPENS", $"{Text(g.Zone, "NOTHING_HAPPENS")}", Special().Split(':')[0]);
            // the goblins' onMobDeath: a 15% (math.randomInt) key item for ACP at The Echo Awakens or past it
            int gk = KillUntil(m => m.Name.StartsWith("Goblin_"), () => DbKeyItem(Ki(g.Food)) == "10");
            Check($"  the goblins killed by the char ({gk}): the {g.Food}", "10", DbKeyItem(Ki(g.Food)));
            uint nm = MobOf(g.ZoneName, g.NmId);
            TalkTo("qm1");
            Check($"  qm1 with it: {g.Nm} spawned and claimed", $"True/{c.Id}",
                  $"{zone.ZoneInfoOf(c.Zone).MobById[nm].IsSpawned}/{zone.ZoneInfoOf(c.Zone).MobById[nm].OwnerId}");
            Lua("player:setUnkillable(true)");
            KillByChar(nm);
            Lua("player:setUnkillable(false)");
            Check($"  {g.Nm} killed by the char: the {g.Seed}", "10", DbKeyItem(Ki(g.Seed)));
        }
        Warp(-120.3f, -19.5f, 306.7f, QufimIsland);
        TalkTo("qm3");
        Check("qm3 with the three Seedspalls: event 32", $"032:{NpcId("qm3")}:32", Ev());
        EventEnd(32);
        Check("  Gatherer of Light I complete, the Seedspalls taken, Gatherer of Light II current", $"00/00/00/{AcpId("GATHERER_OF_LIGHT_II")},0",
              $"{DbKeyItem(Ki("SEEDSPALL_ROSEUM"))[0]}0/{DbKeyItem(Ki("SEEDSPALL_CAERULUM"))[0]}0/{DbKeyItem(Ki("SEEDSPALL_VIRIDIS"))[0]}0/{Mission(LogAcp)}");

        Console.WriteLine("ACP 4 Gatherer of Light II (the Seed Mandragoras not scripted)");
        TalkTo("qm3");
        Check("qm3: SEED_MANDY 1 (the battle's stand-in), no event", "none/1", $"{Cur()}/{Var("SEED_MANDY")}");
        TalkTo("qm3");
        Check("  again: event 34, SEED_MANDY cleared", $"032:{NpcId("qm3")}:34/-", $"{Ev()}/{Var("SEED_MANDY")}");
        EventEnd(34);
        Check("  Gatherer of Light II complete, Those Who Lurk in Shadows I current", $"{AcpId("THOSE_WHO_LURK_IN_SHADOWS_I")},0", Mission(LogAcp));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ACP 5 Those Who Lurk in Shadows I (Fei'Yin)");
        WarpIn(0.0f, 0.0f, 0.0f, FeiYin);
        Check("into Fei'Yin: the zone-in cutscene 29", $"{FeiYin}/29", $"{c.Zone}/{Cur()}");
        EventEnd(29);
        Check("  complete, Those Who Lurk in Shadows II current", $"{AcpId("THOSE_WHO_LURK_IN_SHADOWS_II")},0", Mission(LogAcp));

        Console.WriteLine("ACP 6 Those Who Lurk in Shadows II (the nine Seed Afterglows)");
        uint glow0 = uint.Parse(Lua("report(zones[xi.zone.FEIYIN].npc.AFTERGRLOW_OFFSET)"));
        void TalkGlow(int i) { Near(glow0 + (uint)i); Talk(glow0 + (uint)i); }
        TalkGlow(0);
        Check("the first Afterglow: YOU_REACH_OUT_TO_THE_LIGHT, the Mark of Seed effect, THE_LIGHT_DWINDLES, a zone required",
              $"{Text(FeiYin, "YOU_REACH_OUT_TO_THE_LIGHT")}:0,{Text(FeiYin, "THE_LIGHT_DWINDLES")}:0/true/true/1/1",
              $"{Special()}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.MARK_OF_SEED)))")}/{Lua("report(tostring(player:needToZone()))")}/" +
              $"{Var("SEED_AFTERGLOW_MASK")}/{Var("SEED_AFTERGLOW_INTENSITY")}");
        TalkGlow(0);
        Check("  again (its bit set): SOFTLY_SHIMMERING_LIGHT", $"{Text(FeiYin, "SOFTLY_SHIMMERING_LIGHT")}", Special().Split(':')[0]);
        for (int i = 2; i < 8; i++) TalkGlow(i);
        Check("the next six with the effect: EVEN_GREATER_INTENSITY with the offset, intensity 7", $"{Text(FeiYin, "EVEN_GREATER_INTENSITY")}:7/7/253",
              $"{Special()}/{Var("SEED_AFTERGLOW_INTENSITY")}/{Var("SEED_AFTERGLOW_MASK")}");
        // the second Afterglow (offset 1) has no status in data/zones/FeiYin/npcs.yaml (disappear, the default, as the
        // C++ dataset's): only eight can be talked to and the ninth's event never comes; shown by a GM's !npcstatus
        Check("  the second Afterglow hidden in the zone data", $"{NpcEntity.StatusDisappear}", $"{Npc(glow0 + 1).Status}");
        Lua($"GetNPCByID({glow0 + 1}):setStatus(xi.status.NORMAL)");
        TalkGlow(1);
        Check("  shown (GM): EVEN_GREATER_INTENSITY with its offset, intensity 8", $"{Text(FeiYin, "EVEN_GREATER_INTENSITY")}:1/8/255",
              $"{Special()}/{Var("SEED_AFTERGLOW_INTENSITY")}/{Var("SEED_AFTERGLOW_MASK")}");
        TalkGlow(8);
        Check("the ninth: event 28", $"032:{glow0 + 8}:28", Ev());
        EventEnd(28, 100);
        // delStatusEffectSilent still runs the effect's onEffectLose (CStatusEffectContainer::RemoveStatusEffect): the key
        // item not yet given, MARK_OF_SEED_HAS_VANISHED first, the afterglow vars cleared
        Check("  option 100: the effect gone (MARK_OF_SEED_HAS_VANISHED), SCINTILLATING_BURST_OF_LIGHT, the Mark of Seed key item",
              $"{Text(FeiYin, "MARK_OF_SEED_HAS_VANISHED")}:0,{Text(FeiYin, "SCINTILLATING_BURST_OF_LIGHT")}:0,{Text(FeiYin, "KEYITEM_OBTAINED")}:{Ki("MARK_OF_SEED")}/10/false/-",
              $"{Special()}/{DbKeyItem(Ki("MARK_OF_SEED"))}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.MARK_OF_SEED)))")}/{Var("SEED_AFTERGLOW_MASK")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        // Qu'Bia Arena's Burning Circle has its event 5 commented out ("Temp disabled pending fixes for the BCNM mobs"):
        // the mission a GM's
        Lua($"player:completeMission({LogAcp}, {AcpId("THOSE_WHO_LURK_IN_SHADOWS_II")}) player:addMission({LogAcp}, {AcpId("THOSE_WHO_LURK_IN_SHADOWS_III")})");

        Console.WriteLine("ACP 7 Those Who Lurk in Shadows III (Qu'Bia Arena)");
        Warp(-221.0f, -24.0f, 19.0f, QuBiaArena);
        WinBattlefield(NpcId("BC_Entrance"), "Those Who Lurk in Shadows");
        Check("  won: Remember Me in Your Dreams current, the Ivory Key, LastIvoryKey at JST midnight",
              $"{AcpId("REMEMBER_ME_IN_YOUR_DREAMS")},0/10/{Lua("report(JstMidnight())")}",
              $"{Mission(LogAcp)}/{DbKeyItem(Ki("IVORY_KEY"))}/{Var("LastIvoryKey")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ACP 8 Remember Me in Your Dreams (Ro'Maeve to the Hall of the Gods)");
        Warp(0.0f, 0.0f, 0.0f, RoMaeve);
        TakeZoneLine(LineTo(HallOfTheGods), false);
        Check("from Ro'Maeve into the Hall of the Gods: the zone-in cutscene 5", $"{HallOfTheGods}/5", $"{c.Zone}/{Cur()}");
        EventEnd(5);
        Check("  complete, Born of Her Nightmares current", $"{AcpId("BORN_OF_HER_NIGHTMARES")},0", Mission(LogAcp));

        Console.WriteLine("ACP 9 Born of Her Nightmares (Qufim Island to Lower Delkfutt's Tower)");
        Warp(-250.0f, -20.0f, 300.0f, QufimIsland);
        TakeZoneLine(LineTo(LowerDelkfuttsTower), false);
        Check("from Qufim Island into Lower Delkfutt's Tower: the zone-in cutscene 34", $"{LowerDelkfuttsTower}/34", $"{c.Zone}/{Cur()}");
        EventEnd(34);
        Check("  complete, Banishing the Echo current", $"{AcpId("BANISHING_THE_ECHO")},0", Mission(LogAcp));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ACP 12 A Crystalline Prophecy Fin: Squintrox Dryeyes' Crimson Key, the Tenshodo coffer");
        // Banishing the Echo and Ode of Life Bestowing have no script (TODO stubs, no battlefield): a GM's
        Lua($"player:completeMission({LogAcp}, {AcpId("BANISHING_THE_ECHO")}) player:addMission({LogAcp}, {AcpId("ODE_OF_LIFE_BESTOWING")}) " +
            $"player:completeMission({LogAcp}, {AcpId("ODE_OF_LIFE_BESTOWING")}) player:addMission({LogAcp}, {AcpId("A_CRYSTALLINE_PROPHECY_FIN")})");
        Warp(-100.0f, -1.0f, 11.9f, PortJeuno);
        TalkTo("Squintrox_Dryeyes");
        var sq = Ev().Split(':');
        Check("Squintrox Dryeyes: event 323, the Crimson Key on offer (its bit 1 clear in the excluded mask)", $"{NpcId("Squintrox_Dryeyes")}:323/0",
              $"{sq[1]}:{sq[2]}/{(sq.Length > 3 ? uint.Parse(sq[3].Split(',')[0]) : 0) & 2}");
        EndAny();
        Lua("player:addItem(xi.item.SEEDSPALL_LUX) player:addItem(xi.item.SEEDSPALL_LUNA) player:addItem(xi.item.SEEDSPALL_ASTRUM)");
        TradeTo("Squintrox_Dryeyes", (Slot(lux), 1), (Slot(luna), 1), (Slot(astrum), 1));
        Check("  the three Seedspalls traded: DRYEYES_2, the Crimson Key, LastCrimsonKey at JST midnight, the three taken",
              $"True/10/{Lua("report(JstMidnight())")}/0", $"{Special().StartsWith($"{Text(PortJeuno, "DRYEYES_2")}:")}/{DbKeyItem(Ki("CRIMSON_KEY"))}/{Var("LastCrimsonKey")}/{Qty(lux)}");
        Lua("player:addItem(xi.item.SEEDSPALL_LUX) player:addItem(xi.item.SEEDSPALL_LUNA) player:addItem(xi.item.SEEDSPALL_ASTRUM)");
        TradeTo("Squintrox_Dryeyes", (Slot(lux), 1), (Slot(luna), 1), (Slot(astrum), 1));
        Check("  again with the key held: DRYEYES_3 with it, the three kept", $"{Text(PortJeuno, "DRYEYES_3")}:{Ki("CRIMSON_KEY")}/1",
              $"{Special()}/{Qty(lux)}");
        Warp(41.2f, 3.9f, -51.0f, LowerJeuno);
        TalkTo("Treasure_Coffer_Tenshodo");
        var tc = Ev().Split(':');
        Check("the Tenshodo Treasure Coffer: event 10099 (of the ACP keys the Crimson and Ivory Keys' bits clear, the ACP page on)",
              $"{NpcId("Treasure_Coffer_Tenshodo")}:10099/220/0", $"{tc[1]}:{tc[2]}/{tc[3].Split(',')[0]}/{uint.Parse(tc[3].Split(',')[3]) & 2}");
        EventEnd(10099, 1);
        // the prize rolled (math.randomInt) from the Crimson Key's list; its augments rolled too, but addItem's format 2
        // takes no augments (CLuaBaseEntity::addItem): the item plain
        var won = Special().Split(',')[0].Split(':');
        ushort prize = won.Length == 2 ? ushort.Parse(won[1]) : (ushort)0;
        Check("  option 1: a prize (ITEM_OBTAINED), in the inventory without augments, the key taken",
              $"{Text(LowerJeuno, "ITEM_OBTAINED")}/1/0,0/0", $"{won[0]}/{Qty(prize)}/" +
              $"{Lua($"local a = player:findItem({prize}):getAugment(0) report(a[1]) report(a[2])")}/{DbKeyItem(Ki("CRIMSON_KEY"))[0]}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));


        // ---- A Moogle Kupo d'Etat -----------------------------------------------------------------------------------
        Console.WriteLine("A Moogle Kupo d'Etat: the login in Southern San d'Oria");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[4];
        Fixture(db, LowerJeuno, LowerJeuno, -36.0f, -1.0f, -13.0f, 0);
        Login();
        const ushort WindurstWalls = 239, WestSarutabaruta = 115, ZeruhnMines = 172;
        int AmkId(string name) => int.Parse(Lua($"report(xi.mission.id.amk.{name})"));
        // the other logs' zone-in cutscenes kept out of the way (ACP 1 played on the Lower Jeuno login)
        Lua($"player:addMission(xi.mission.log_id.ROV, {RovId("WHERE_DIVINITIES_COLLIDE")}) " +
            "player:addMission(xi.mission.log_id.ASA, xi.mission.id.asa.BURGEONING_DREAD) " +
            "player:addMission(xi.mission.log_id.SOA, xi.mission.id.soa.ABOMINATION) " +
            "player:addMission(xi.mission.log_id.COP, xi.mission.id.cop.THE_ROAD_FORKS) " +
            "player:changeContainerSize(xi.inv.INVENTORY, 50)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        Check("the AMK log at A Moogle Kupo d'Etat", $"{AmkId("A_MOOGLE_KUPO_DETAT")},0", Mission(LogAmk));
        NpcEntity MhMoogle() => zone.ZoneInfoOf(c.Zone).Npcs.First(n => n.Name == "Moogle" && n.Z == 1.5f && (n.Look[1] & 0xFF) == 0x52);
        void IntoMogHouse()
        {
            Warp(-90.0f, 1.0f, -50.0f, SouthernSandoria);
            TakeZoneLine("zmr0");
            EndAny();
        }
        // a HELM point of that name traded its tool until done: the tools topped up, the point waited for while hidden
        // (moved), the helm wait (ENABLE_HELM_WAIT's 3 s of GetSystemTime) gone by before each
        int HelmUntil(string point, string toolName, Func<bool> done, int max = 400)
        {
            ushort tool = ushort.Parse(Lua($"report(xi.item.{toolName})"));
            int n = 0;
            for (int i = 0; n < max && i < max * 3 && !done(); i++)
            {
                if (Qty(tool) == "0") Lua($"player:addItem({tool}, 12)");
                var pt = zone.ZoneInfoOf(c.Zone).Npcs.FirstOrDefault(p => p.Name == point && p.Status == NpcEntity.StatusNormal);
                if (pt == null) { Tick(5000); continue; }
                Near(pt.Id);
                unixOffset += 4;
                Trade(pt.Id, (Slot(tool), 1));
                EndAny();
                n++;
            }
            return n;
        }

        Console.WriteLine("AMK 1 A Moogle Kupo d'Etat");
        IntoMogHouse();
        Check("into the mog house in the home nation", $"{SouthernSandoria}/{CharId}", $"{c.Zone}/{c.MoghouseId}");
        var mhMoogle = MhMoogle();
        Near(mhMoogle.Id);
        Talk(mhMoogle.Id);
        Check("the Moogle: event 30023", $"032:{mhMoogle.Id}:30023", Ev());
        EventEnd(30023);
        Check("  complete, Drenched! It Began with a Raindrop current", $"{AmkId("DRENCHED_IT_BEGAN_WITH_A_RAINDROP")},0", Mission(LogAmk));

        Console.WriteLine("AMK 2 Drenched! It Began with a Raindrop");
        ushort orcish = ushort.Parse(Lua("report(xi.item.ORCISH_PLATE_ARMOR)")), backscale = ushort.Parse(Lua("report(xi.item.QUADAV_BACKSCALE)")),
               caulk = ushort.Parse(Lua("report(xi.item.YAGUDO_CAULK)"));
        Lua($"player:addItem({orcish}) player:addItem({backscale}) player:addItem({caulk})");
        Trade(mhMoogle.Id, (Slot(orcish), 1), (Slot(backscale), 1));
        Check("the Moogle, two of the three traded: no 30024", "False", Cur() == "30024");
        EndAny();
        Trade(mhMoogle.Id, (Slot(orcish), 1), (Slot(backscale), 1), (Slot(caulk), 1));
        Check("  the three: event 30024", $"032:{mhMoogle.Id}:30024", Ev());
        EventEnd(30024);
        Check("  complete, the three taken, Hasten! In a Jam in Jeuno? current", $"0/0/0/{AmkId("HASTEN_IN_A_JAM_IN_JEUNO")},0",
              $"{Qty(orcish)}/{Qty(backscale)}/{Qty(caulk)}/{Mission(LogAmk)}");

        Console.WriteLine("AMK 3 Hasten! In a Jam in Jeuno?");
        Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
        TalkTo("Inconspicuous_Door");
        Check("Upper Jeuno's Inconspicuous Door: event 10178", $"032:{NpcId("Inconspicuous_Door")}:10178", Ev());
        EventEnd(10178);
        Check("  complete, Welcome to My Decrepit Domicile current", $"{AmkId("WELCOME_TO_MY_DECREPIT_DOMICILE")},0", Mission(LogAmk));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("AMK 4 Welcome to My Decrepit Domicile (HELM's 5% key items)");
        TalkTo("Inconspicuous_Door");
        Cs("the door without the Sturdy Metal Strip: the reminder 10186", 10186);
        (ushort Zone, string Point, string Tool, string Ki, int Before, int After, int Reminder)[] helms =
        {
            (ZeruhnMines, "Mining_Point", "PICKAXE", "STURDY_METAL_STRIP", 10179, 1, 10187),
            (EastRonfaure, "Logging_Point", "HATCHET", "PIECE_OF_RUGGED_TREE_BARK", 10180, 2, 10188),
            (WestSarutabaruta, "Harvesting_Point", "SICKLE", "SAVORY_LAMB_ROAST", 10181, 0, 0),
        };
        foreach (var h in helms)
        {
            Warp(0.0f, 0.0f, 0.0f, h.Zone);
            // xi.helm.initZone hid the points for their respawnTime (30 s) at the zone's start
            Tick(31000);
            int tries = HelmUntil(h.Point, h.Tool, () => DbKeyItem(Ki(h.Ki)) == "10");
            Check($"  {h.Point} in {zone.ZoneInfoOf(c.Zone).Name} traded its {h.Tool} ({tries}): the {h.Ki} (xi.amk.helpers.helmTrade)", "10", DbKeyItem(Ki(h.Ki)));
            Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
            TalkTo("Inconspicuous_Door");
            Check($"  the door with it: event {h.Before}", $"032:{NpcId("Inconspicuous_Door")}:{h.Before}", Ev());
            EventEnd(h.Before);
            if (h.Reminder != 0)
            {
                Check($"  taken, status {h.After}", $"0/{AmkId("WELCOME_TO_MY_DECREPIT_DOMICILE")},{h.After}", $"{DbKeyItem(Ki(h.Ki))[0]}/{Mission(LogAmk)}");
                TalkTo("Inconspicuous_Door");
                Cs($"  again: the next reminder {h.Reminder}", h.Reminder);
            }
        }
        Check("  Welcome to My Decrepit Domicile complete, the roast taken, Curses! A Horrifically Harrowing Hex current",
              $"0/{AmkId("CURSES_A_HORRIFICALLY_HARROWING_HEX")},0", $"{DbKeyItem(Ki("SAVORY_LAMB_ROAST"))[0]}/{Mission(LogAmk)}");
        // the HELM yields and the tools out of the inventory (the later digs need a free slot for their greens)
        Lua("for slot = 1, 80 do local it = player:getStorageItem(0, slot, 255) if it then player:delItem(it:getID(), it:getQuantity()) end end");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("AMK 5 Curses! A Horrifically Harrowing Hex");
        Warp(0.0f, 0.0f, 0.0f, WindurstWalls);
        TalkTo("Shantotto");
        Check("Windurst Walls' Shantotto: event 506", $"032:{NpcId("Shantotto")}:506", Ev());
        EventEnd(506);
        Check("  complete, An Errand! The Professor's Price current", $"{AmkId("AN_ERRAND_THE_PROFESSORS_PRICE")},0", Mission(LogAmk));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        const ushort OuterHorutotoRuins = 194;
        void Act(ActionId id, uint mountId = 0)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(c.Id).CopyTo(b, 4);
                BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)id).CopyTo(b, C2sAction.OffActionID);
                BitConverter.GetBytes(mountId).CopyTo(b, C2sAction.OffMount);
            });
        }
        Console.WriteLine("AMK 6 An Errand! The Professor's Price");
        TalkTo("Shantotto");
        Cs("Shantotto: event 508", 508);
        Warp(0.0f, 0.0f, 0.0f, OuterHorutotoRuins);
        TalkTo("qm1");
        Check("Outer Horutoto's qm1 without an orb: IF_HAD_ORBS with the four",
              $"{Text(OuterHorutotoRuins, "IF_HAD_ORBS")}:{Ki("ORB_OF_SWORDS")}", Special());
        // the Cardians' onMobDeath (xi.amk.helpers.cardianOrbDrop): the mob's one roll of (3 x level + 30 x party - 10)
        // in 1000 for the suit's orb
        foreach (var suit in new[] { "Swords", "Cups", "Batons", "Coins" })
        {
            string orb = $"ORB_OF_{suit.ToUpperInvariant()}";
            int ok = KillUntil(m => m.Name.EndsWith($"_of_{suit}") && !m.Name.StartsWith("Custom"), () => DbKeyItem(Ki(orb)) == "10", 400);
            Check($"  the {suit} Cardians killed by the char ({ok}): the {orb}", "10", DbKeyItem(Ki(orb)));
        }
        TalkTo("qm1");
        Check("qm1 with the four orbs: event 100", $"032:{NpcId("qm1")}:100", Ev());
        uint cardian0 = uint.Parse(Lua("report(zones[xi.zone.OUTER_HORUTOTO_RUINS].mob.CUSTOM_CARDIAN_OFFSET)"));
        EventEnd(100, 1);
        var horuInfo = zone.ZoneInfoOf(c.Zone);
        var customs = Enumerable.Range(0, 15).Select(i => horuInfo.MobById[cardian0 + (uint)i]).ToList();
        Check("  option 1: the confrontation (the four orbs taken: five Custom Cardians, the CONFRONTATION effect on both sides)",
              "00/00/00/00/5/True/True",
              $"{DbKeyItem(Ki("ORB_OF_SWORDS"))[0]}0/{DbKeyItem(Ki("ORB_OF_CUPS"))[0]}0/{DbKeyItem(Ki("ORB_OF_BATONS"))[0]}0/{DbKeyItem(Ki("ORB_OF_COINS"))[0]}0/" +
              $"{customs.Count(m => m.IsSpawned)}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.CONFRONTATION)))") == "true"}/" +
              $"{Lua($"report(tostring(GetMobByID({cardian0}):hasStatusEffect(xi.effect.CONFRONTATION)))") == "true"}");
        Check("  their SDT mods: the orbs' immunities lifted (0)", "0/0/0/0",
              Lua($"local m = GetMobByID({cardian0}) report(m:getMod(xi.mod.SLASH_SDT)) report(m:getMod(xi.mod.IMPACT_SDT)) report(m:getMod(xi.mod.PIERCE_SDT)) report(m:getMod(xi.mod.UDMGMAGIC))").Replace(",", "/"));
        Lua("player:setUnkillable(true)");
        foreach (var m in customs.Where(m => m.IsSpawned).ToList()) KillByChar(m.Id);
        Lua("player:setUnkillable(false)");
        for (int i = 0; i < 5 && DbKeyItem(Ki("RIPE_STARFRUIT")) != "10"; i++) Tick(2400);
        Check("  the five killed: won (the qm's 2.4 s check): the Ripe Starfruit, the Peach Coral Key, the effect gone",
              "10/10/False", $"{DbKeyItem(Ki("RIPE_STARFRUIT"))}/{DbKeyItem(Ki("PEACH_CORAL_KEY"))}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.CONFRONTATION)))") == "true"}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        Tick(31000);
        TalkTo("qm1");
        Check("qm1 with the starfruit: CANNOT_ENTER_BATTLEFIELD", $"{Text(OuterHorutotoRuins, "CANNOT_ENTER_BATTLEFIELD")}:{Ki("RIPE_STARFRUIT")}", Special());
        Warp(0.0f, 0.0f, 0.0f, WindurstWalls);
        TalkTo("Shantotto");
        Check("Shantotto with it: event 507 (the gil, enough)", $"034:{NpcId("Shantotto")}:507:0,{Gil()}", Ev());
        EventEnd(507, 0);
        Check("  option 0 (no pay): a zone required", "true", Lua("report(tostring(player:needToZone()))"));
        TalkTo("Shantotto");
        Check("  again before a zone: not 507", "False", Cur() == "507");
        EndAny();
        string gil6 = Gil();
        // the mustZone keeps the zone id: cleared by xi.player.onGameIn in another zone only
        Warp(0.0f, 0.0f, 0.0f, (ushort)238);
        Warp(0.0f, 0.0f, 0.0f, WindurstWalls);
        TalkTo("Shantotto");
        Cs("  zoned: 507 again, option 1 (pay)", 507, 1);
        Check("  5000 gil paid, the starfruit taken, status 1", $"{long.Parse(gil6) - 5000}/0/{AmkId("AN_ERRAND_THE_PROFESSORS_PRICE")},1",
              $"{Gil()}/{DbKeyItem(Ki("RIPE_STARFRUIT"))[0]}/{Mission(LogAmk)}");
        TalkTo("Shantotto");
        Cs("Shantotto at status 1: event 509", 509);
        Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
        TalkTo("Inconspicuous_Door");
        string digZone = Var("Mission[10][6]diggingZone");
        // the zone's event as Ev() renders the 0x034's params (trailing zeros dropped): La Theine Plateau's is 0
        string digEvent = Lua($"report(xi.amk.helpers.digSites[{digZone}].eventID)");
        string digParams = digEvent == "0" ? "" : ":" + digEvent;
        Check("the Inconspicuous Door: event 10182 with the digging zone's (math.randomInt of thirteen) event",
              $"034:{NpcId("Inconspicuous_Door")}:10182{digParams}", Ev());
        EventEnd(10182);
        Check("  complete, Shock! Arrant Abuse of Authority current", $"{AmkId("SHOCK_ARRANT_ABUSE_OF_AUTHORITY")},0", Mission(LogAmk));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("AMK 7 Shock! Arrant Abuse of Authority (the chocobo dig)");
        TalkTo("Inconspicuous_Door");
        Check("the door: event 10189 with the digging zone's event",
              $"034:{NpcId("Inconspicuous_Door")}:10189{digParams}", Ev());
        EndAny();
        ushort dz = ushort.Parse(digZone);
        if (dz == LaTheinePlateau)
        {
            // La Theine Plateau's Zone.lua never calls tryRandomlyPlaceDiggingLocation: its spot stays 0, the dig errors
            Console.WriteLine("  (the dig zone is La Theine Plateau: no spot placed by its zone-in, a GM's other zone)");
            Lua($"player:setCharVar('Mission[10][6]diggingZone', {JugnerForest})");
            dz = JugnerForest;
        }
        Lua("player:addKeyItem(xi.keyItem.CHOCOBO_COMPANION) player:addItem(xi.item.BUNCH_OF_GYSAHL_GREENS, 12)");
        Warp(0.0f, 0.0f, 0.0f, dz);
        int spot = int.Parse(Lua("report(player:getLocalVar('Mission[10][6]diggingSpot'))"));
        Check($"  into the dig zone {dz}: a spot placed by its zone-in (1-10)", "True", spot >= 1 && spot <= 10);
        float spotX = float.Parse(Lua($"report(xi.amk.helpers.digSites[{dz}].spots[{spot}].x)"), System.Globalization.CultureInfo.InvariantCulture);
        float spotZ = float.Parse(Lua($"report(xi.amk.helpers.digSites[{dz}].spots[{spot}].z)"), System.Globalization.CultureInfo.InvariantCulture);
        Act(ActionId.Mount, 0);
        Check("  mounted on the chocobo", "True", c.StatusEffects.HasStatusEffect(EffectId.Mounted));
        MoveTo(spotX + 30.0f, c.Y, spotZ);
        unixOffset += 61;
        Act(ActionId.ChocoboDig);
        Check("  a dig 30 yalms off the spot: AMK_DIGGING_OFFSET + 6 (the pull, west), + 2 (11-50'), no chest",
              $"True/True/0", $"{Special().Contains($"{int.Parse(Text(dz, "AMK_DIGGING_OFFSET")) + 6}:")}/" +
              $"{Special().Contains($"{int.Parse(Text(dz, "AMK_DIGGING_OFFSET")) + 2}:")}/{DbKeyItem(Ki("MOLDY_WORM_EATEN_CHEST"))[0]}");
        MoveTo(spotX + 1.0f, c.Y, spotZ);
        unixOffset += 61;
        Act(ActionId.ChocoboDig);
        Check("  a dig on the spot: the Moldy Worm-Eaten Chest", "10", DbKeyItem(Ki("MOLDY_WORM_EATEN_CHEST")));
        Act(ActionId.Dismount);
        Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
        TalkTo("Inconspicuous_Door");
        Check("the door with the chest: event 10183", $"032:{NpcId("Inconspicuous_Door")}:10183", Ev());
        EventEnd(10183);
        Check("  complete, the chest taken, Lender Beware! Read the Fine Print current", $"0/{AmkId("LENDER_BEWARE_READ_THE_FINE_PRINT")},0",
              $"{DbKeyItem(Ki("MOLDY_WORM_EATEN_CHEST"))[0]}/{Mission(LogAmk)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("AMK 8 Lender Beware! Read the Fine Print");
        Warp(0.0f, 0.0f, 0.0f, SeaSerpentGrotto);
        TalkTo("Shady_Sconce");
        Check("Sea Serpent Grotto's Shady Sconce: event 19", $"034:{NpcId("Shady_Sconce")}:19:176", Ev());
        EventEnd(19, 1);
        Check("  option 1: status 1", $"{AmkId("LENDER_BEWARE_READ_THE_FINE_PRINT")},1", Mission(LogAmk));
        TalkTo("Shady_Sconce");
        Cs("  again: event 22", 22);
        TalkTo("Waterfall_Basin");
        Cs("the Waterfall Basin: event 20", 20);
        Check("  status 2", $"{AmkId("LENDER_BEWARE_READ_THE_FINE_PRINT")},2", Mission(LogAmk));
        Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
        TalkTo("Inconspicuous_Door");
        Cs("the door at status 2: event 10184", 10184);
        Check("  complete, Rescue! A Moogle's Labor of Love current", $"{AmkId("RESCUE_A_MOOGLES_LABOR_OF_LOVE")},0", Mission(LogAmk));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        const ushort QuicksandCaves = 208, ChamberOfOracles = 168, CastleZvahlBaileys = 161, CastleZvahlKeep = 162, ThroneRoom = 165,
                     BeaucedineGlacier = 111, Xarcabard = 112;
        string AmkLocal(string name) => Lua($"report(player:getLocalVar('Mission[{LogAmk}][{AmkId("A_CHALLENGE_YOU_COULD_BE_A_WINNER")}]{name}'))");
        Console.WriteLine("AMK 9 Rescue! A Moogle's Labor of Love (Quicksand Caves' markers)");
        Warp(0.0f, 0.0f, 0.0f, QuicksandCaves);
        TalkTo("Goblin_Geologist");
        string markerSet = Var("Mission[10][8]markerSet");
        Check("the Goblin Geologist: event 100 (no map, the marker set: math.randomInt of ten)",
              $"True/034:{NpcId("Goblin_Geologist")}:100:0,0,{markerSet}", $"{int.Parse(markerSet) is >= 1 and <= 10}/{Ev()}");
        EventEnd(100);
        Check("  progress 1", "1", Var("Mission[10][8]progress"));
        var amkQms = Lua("for _, id in ipairs(zones[xi.zone.QUICKSAND_CAVES].npc.QM_AMK) do report(id) end").Split(',').Select(uint.Parse).ToList();
        int stones = 0, nothing = 0;
        foreach (var qm in amkQms)
        {
            Near(qm);
            Talk(qm);
            if (Special().StartsWith($"{Text(QuicksandCaves, "KEYITEM_OBTAINED")}:")) stones++;
            else nothing++;
            EndAny();
        }
        int stone0 = Ki("STONE_OF_SURYA");
        Check($"the twenty ??? (the set's nine markers): the nine stones", "9/11/True", $"{stones}/{nothing}/" +
              $"{Enumerable.Range(0, 9).All(i => DbKeyItem(stone0 + i) == "10")}");
        TalkTo("Goblin_Geologist");
        Check("the Geologist with the nine: event 100 (1)", $"034:{NpcId("Goblin_Geologist")}:100:1", Ev());
        EventEnd(100);
        Check("  the stones taken, the marker set cleared, the Navaratna Talisman", "True/-/10",
              $"{Enumerable.Range(0, 9).All(i => DbKeyItem(stone0 + i)[0] == '0')}/{Var("Mission[10][8]markerSet")}/{DbKeyItem(Ki("NAVARATNA_TALISMAN"))}");
        TalkTo("Goblin_Geologist");
        Check("  again: GRANT_YOU_EASY_ENTRANCE", $"{Text(QuicksandCaves, "GRANT_YOU_EASY_ENTRANCE")}:{Ki("NAVARATNA_TALISMAN")}", Special());
        EndAny();
        Warp(0.0f, 0.0f, 0.0f, ChamberOfOracles);
        TalkTo("Shimmering_Circle");
        Cs("the Chamber of Oracles' Shimmering Circle: event 5", 5);
        Check("  complete, Roar! A Cat Burglar Bares Her Fangs current", $"{AmkId("ROAR_A_CAT_BURGLAR_BARES_HER_FANGS")},0", Mission(LogAmk));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("AMK 10 Roar! A Cat Burglar Bares Her Fangs (its script a copy of AMK 8's)");
        Warp(0.0f, 0.0f, 0.0f, SeaSerpentGrotto);
        TalkTo("Shady_Sconce");
        Cs("the Shady Sconce: event 19, option 1", 19, 1);
        TalkTo("Waterfall_Basin");
        Cs("the Waterfall Basin: event 20", 20);
        Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
        TalkTo("Inconspicuous_Door");
        Cs("the door at status 2: event 10184", 10184);
        Check("  complete, Relief! A Triumphant Return current", $"{AmkId("RELIEF_A_TRIUMPHANT_RETURN")},0", Mission(LogAmk));

        Console.WriteLine("AMK 11 Relief! A Triumphant Return");
        TalkTo("Inconspicuous_Door");
        Cs("the door: event 10185", 10185);
        Check("  complete, Joy! Summoned to a Fabulous Fete current", $"{AmkId("JOY_SUMMONED_TO_A_FABULOUS_FETE")},0", Mission(LogAmk));

        Console.WriteLine("AMK 12 Joy! Summoned to a Fabulous Fete");
        WarpIn(0.0f, 0.0f, 0.0f, CastleZvahlBaileys);
        Check("into Castle Zvahl Baileys: the zone-in cutscene 88", $"{CastleZvahlBaileys}/88", $"{c.Zone}/{Cur()}");
        EventEnd(88);
        Check("  complete, A Challenge! You Could Be a Winner current", $"{AmkId("A_CHALLENGE_YOU_COULD_BE_A_WINNER")},0", Mission(LogAmk));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("AMK 13 A Challenge! You Could Be a Winner");
        string Prog13() => Var($"Mission[{LogAmk}][{AmkId("A_CHALLENGE_YOU_COULD_BE_A_WINNER")}]progress");
        TalkTo("Shadowy_Pillar");
        Check("the Shadowy Pillar: event 100 (3)", $"034:{NpcId("Shadowy_Pillar")}:100:3", Ev());
        EventEnd(100);
        Check("  progress 1", "1", Prog13());
        Console.WriteLine(" puzzle 1: the pips");
        Warp(0.0f, 0.0f, 0.0f, BeaucedineGlacier);
        TalkTo("Lonely_Evergreen");
        Cs("Beaucedine's Lonely Evergreen: event 504, option 1", 504, 1);
        Check("  a zone required (the puzzle on)", "true", Lua("report(tostring(player:needToZone()))"));
        TalkTo("Lonely_Evergreen");
        Cs("  again: event 503", 503);
        TalkTo("Goblin_Grenadier");
        Cs("the Goblin Grenadier without the Northlands map: event 509", 509);
        Lua("player:addKeyItem(xi.keyItem.MAP_OF_THE_NORTHLANDS_AREA)");
        TalkTo("Goblin_Grenadier");
        Check("  with the map (GM): event 508", $"034:{NpcId("Goblin_Grenadier")}:508:{Ki("MAP_OF_THE_NORTHLANDS_AREA")}", Ev());
        EventEnd(508);
        string pipSet = AmkLocal("[p1]pipSet");
        Check("  the pip set (math.randomInt 1-10)", "True", int.Parse(pipSet) is >= 1 and <= 10);
        TalkTo("Northwestern_Pip");
        var pip = Npc(NpcId("Northwestern_Pip"));
        Check("the Northwestern Pip: event 510 with its position and the set's element",
              $"034:{pip.Id}:510:{(uint)(int)(pip.X * 1000)},{(uint)(int)(pip.Z * 1000)},{(uint)(int)(pip.Y * 1000)},{Lua($"report(xi.amk.helpers.pipSets[{int.Parse(pipSet) - 1}][1])")},{Ki("MAP_OF_THE_NORTHLANDS_AREA")}",
              Ev());
        EndAny();
        // the Grenadier's handler: messageSpecial(text.GRENADIER_DAY_HINT, ...) before its 507, but Beaucedine Glacier's
        // IDs.lua has no GRENADIER_DAY_HINT: a nil message id, the handler's error (caught by the framework), no 507
        int handlerErrors = log.Count(l => l.Contains("Error running handler"));
        TalkTo("Goblin_Grenadier");
        Check("the Grenadier with the set: the handler errors (no GRENADIER_DAY_HINT in IDs.lua), no 507",
              "True/False", $"{log.Count(l => l.Contains("Error running handler")) > handlerErrors}/{Cur() == "507"}");
        EndAny();
        // 507's option 5 (the answer right, no hint) a GM's: the Pocket Mogbomb, the zone requirement lifted
        Lua("player:addKeyItem(xi.keyItem.POCKET_MOGBOMB) player:needToZone(false)");
        TalkTo("Lonely_Evergreen");
        Check("the Evergreen with the mogbomb: event 502", $"034:{NpcId("Lonely_Evergreen")}:502:{Ki("POCKET_MOGBOMB")},{Ki("TRIVIA_CHALLENGE_KUPON")}", Ev());
        EventEnd(502);
        Check("  the mogbomb taken, the Trivia Challenge Kupon, progress 2", "0/10/2",
              $"{DbKeyItem(Ki("POCKET_MOGBOMB"))[0]}/{DbKeyItem(Ki("TRIVIA_CHALLENGE_KUPON"))}/{Prog13()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine(" puzzle 2: the trivia (Xarcabard)");
        Warp(0.0f, 0.0f, 0.0f, Xarcabard);
        string[] stoogeNames = { "Option_One", "Option_Two", "Option_Three" };
        string stoogeName = "Option_One";
        for (int q = 1; q <= 4; q++)
        {
            TalkTo(stoogeName);
            Check($"  question {q} at {stoogeName}: event 200 (progress {q})", $"200/{q}",
                  $"{Cur()}/{(Ev().Split(':') is var ep && ep.Length > 3 ? ep[3].Split(',')[0] : "-")}");
            if (q < 4)
            {
                EventEnd(200, 1, 1);
                string correct = AmkLocal("[p2]correctStooge");
                Check($"    the update: the answers, the next (right) stooge set", "True", correct is "1" or "2" or "3" && Upd() != "none");
                EventEnd(200, 1);
                stoogeName = stoogeNames[int.Parse(correct) - 1];
            }
            else EventEnd(200, 2);
        }
        Check("  the fourth won (option 2): the Gauntlet Challenge Kupon, the trivia kupon taken, progress 3", "10/0/3",
              $"{DbKeyItem(Ki("GAUNTLET_CHALLENGE_KUPON"))}/{DbKeyItem(Ki("TRIVIA_CHALLENGE_KUPON"))[0]}/{Prog13()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine(" puzzle 3: the gauntlet (Castle Zvahl Baileys)");
        Warp(0.0f, 0.0f, 0.0f, CastleZvahlBaileys);
        TalkTo("Shadowy_Pillar");
        Check("the Shadowy Pillar with the kupon: event 100 (1)", $"034:{NpcId("Shadowy_Pillar")}:100:1", Ev());
        EventEnd(100, 1);
        Check("  option 1: a zone required, level restriction, invisible, deodorize, sneak", "true/true/true/true/true",
              Lua("report(tostring(player:needToZone())) report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION))) " +
                  "report(tostring(player:hasStatusEffect(xi.effect.INVISIBLE))) report(tostring(player:hasStatusEffect(xi.effect.DEODORIZE))) " +
                  "report(tostring(player:hasStatusEffect(xi.effect.SNEAK)))").Replace(",", "/"));
        TalkTo("Flame_of_Fate");
        Check("the Flame of Fate within the eight minutes: event 101 (2), the restriction lifted", $"034:{NpcId("Flame_of_Fate")}:101:2/false",
              $"{Ev()}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))")}");
        EventEnd(101, 1);
        Check("  option 1: the Festival Souvenir Kupon, the gauntlet kupon taken, progress 4", "10/0/4",
              $"{DbKeyItem(Ki("FESTIVAL_SOUVENIR_KUPON"))}/{DbKeyItem(Ki("GAUNTLET_CHALLENGE_KUPON"))[0]}/{Prog13()}");
        TalkTo("Flame_of_Fate");
        Cs("  again: event 101 (3)", 101);

        Console.WriteLine(" puzzle 4: the cohorts (Castle Zvahl Keep)");
        Warp(0.0f, 0.0f, 0.0f, CastleZvahlKeep);
        TalkTo("Ominous_Pillar");
        Check("the Ominous Pillar: event 100 (1, the cohort: math.randomInt of four)", "100/1", $"{Cur()}/{Ev().Split(':')[3].Split(',')[0]}");
        EventEnd(100, 1);
        int cohort = int.Parse(Var($"Mission[{LogAmk}][{AmkId("A_CHALLENGE_YOU_COULD_BE_A_WINNER")}]cohortIdx"));
        Check("  the cohort kept (1-4)", "True", cohort is >= 1 and <= 4);
        var crags = Lua("for _, id in ipairs(zones[xi.zone.CASTLE_ZVAHL_KEEP].npc.CRAGGY_PILLAR) do report(id) end").Split(',').Select(uint.Parse).ToList();
        // the Craggy Pillars' cohorts in order: Kupignol (1), Kupert (3), Kupuckl (2), Kupatete (0)
        int[] cragCohort = { 1, 3, 2, 0 };
        int wrong = Enumerable.Range(0, 4).First(i => cragCohort[i] != cohort - 1), right = Enumerable.Range(0, 4).First(i => cragCohort[i] == cohort - 1);
        Near(crags[wrong]);
        Talk(crags[wrong]);
        Check("  a wrong Craggy Pillar: its event, result 2", $"{101 + wrong}/2", $"{Cur()}/{Ev().Split(':')[3].Split(',')[0]}");
        EventEnd(101 + wrong, 0);
        Near(crags[right]);
        Talk(crags[right]);
        Check("  the right one: its event, result 1", $"{101 + right}/1", $"{Cur()}/{Ev().Split(':')[3].Split(',')[0]}");
        EventEnd(101 + right, 1);
        Check("  option 1: the Mega Bonanza Kupon, the souvenir kupon taken, progress 5", "10/0/5",
              $"{DbKeyItem(Ki("MEGA_BONANZA_KUPON"))}/{DbKeyItem(Ki("FESTIVAL_SOUVENIR_KUPON"))[0]}/{Prog13()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine(" the Throne Room");
        Warp(0.0f, 0.0f, 0.0f, ThroneRoom);
        TalkTo("_4l1");
        Check("the Throne Room's _4l1: event 4 (165)", $"034:{NpcId("_4l1")}:4:165", Ev());
        EventEnd(4);
        Check("  complete, Smash! A Malevolent Menace current", $"{AmkId("SMASH_A_MALEVOLENT_MENACE")},0", Mission(LogAmk));

        Console.WriteLine("AMK 14 Smash! A Malevolent Menace");
        Warp(-40.0f, 0.0f, 20.0f, UpperJeuno);
        TalkTo("Inconspicuous_Door");
        Cs("the door: event 10185", 10185);
        Check("  complete (no next mission)", "True", Lua($"report(tostring(player:hasCompletedMission({LogAmk}, {AmkId("SMASH_A_MALEVOLENT_MENACE")})))") == "true");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));


        // ---- A Shantotto Ascension ----------------------------------------------------------------------------------
        Console.WriteLine("A Shantotto Ascension: the login in Windurst Waters");
        if (s != null) zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[5];
        const ushort WindurstWaters = 238, PortWindurst = 240, BastokMines = 234, CloisterOfFlames = 207;
        Fixture(db, WindurstWaters, WindurstWaters, -40.0f, -5.0f, 120.0f, 0);
        Login();
        int AsaId(string name) => int.Parse(Lua($"report(xi.mission.id.asa.{name})"));
        string SVar(int mission, string name) => Var($"Mission[{LogAsa}][{mission}]{name}");
        // the other logs' zone-in cutscenes kept out of the way
        Lua($"player:addMission(xi.mission.log_id.ROV, {RovId("WHERE_DIVINITIES_COLLIDE")}) " +
            "player:addMission(xi.mission.log_id.SOA, xi.mission.id.soa.ABOMINATION) " +
            "player:addMission(xi.mission.log_id.ACP, xi.mission.id.acp.A_CRYSTALLINE_PROPHECY_FIN) " +
            "player:addMission(xi.mission.log_id.COP, xi.mission.id.cop.THE_ROAD_FORKS)");
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        Check("in Windurst Waters, the ASA log at A Shantotto Ascension", $"{WindurstWaters}/{AsaId("A_SHANTOTTO_ASCENSION")},0", $"{c.Zone}/{Mission(LogAsa)}");

        Console.WriteLine("ASA 1 A Shantotto Ascension");
        TakeZoneLine(LineTo(WindurstWalls), false);
        Check("from Windurst Waters into Windurst Walls: the zone-in cutscene 510", $"{WindurstWalls}/510", $"{c.Zone}/{Cur()}");
        EventEnd(510);
        Check("  510 finished: event 514 started", "514", Cur());
        EventEnd(514);
        Check("  complete, Burgeoning Dread current", $"{AsaId("BURGEONING_DREAD")},0", Mission(LogAsa));

        Console.WriteLine("ASA 2 Burgeoning Dread");
        Warp(-40.0f, -5.0f, 120.0f, WindurstWaters);
        TakeZoneLine(LineTo(WestSarutabaruta), false);
        Check("from Windurst Waters into West Sarutabaruta: the zone-in cutscene 62", $"{WestSarutabaruta}/62", $"{c.Zone}/{Cur()}");
        EventEnd(62, 0, 1);
        // West Sarutabaruta's Zone.lua keeps its pre-framework copy (ASA_Status, completeMission / addMission on 62's
        // finish): onEventFinish runs the mission's handler and the zone's, the second completion warns (as C++'s)
        string kit = SVar(AsaId("THAT_WHICH_CURDLES_BLOOD"), "Option");
        Check("  62's update: the enfeeblement kit (2779 + math.randomInt(0, 3)), kept in That Which Curdles Blood's Option",
              $"True/{kit}", $"{int.Parse(kit) is >= 2779 and <= 2782}/{Upd()}");
        EventEnd(62);
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);
        Check("  complete, That Which Curdles Blood current; the zone's ASA_Status (the update's option)", $"{AsaId("THAT_WHICH_CURDLES_BLOOD")},0/-",
              $"{Mission(LogAsa)}/{Var("ASA_Status")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ASA 3 That Which Curdles Blood");
        string[] kits = { "ENFEEBLEMENT_KIT_OF_POISON", "ENFEEBLEMENT_KIT_OF_BLINDNESS", "ENFEEBLEMENT_KIT_OF_SLEEP", "ENFEEBLEMENT_KIT_OF_SILENCE" };
        string[] potions = { "FLASK_OF_POISON_POTION", "FLASK_OF_BLINDING_POTION", "FLASK_OF_SLEEPING_POTION", "FLASK_OF_SILENCING_POTION" };
        int kitIdx = Array.FindIndex(kits, k => Lua($"report(xi.item.{k})") == kit);
        Check("  the kit one of the four", "True", kitIdx >= 0);
        string It(string name) => Lua($"report(xi.item.{name})");
        Warp(0.0f, 0.0f, 0.0f, PortWindurst);
        TalkTo("Kuroido-Moido");
        Check("Port Windurst's Kuroido-Moido: event 858 with the kit's materials",
              $"034:{NpcId("Kuroido-Moido")}:858:{kit},{It("SHEET_OF_BAST_PARCHMENT")},0,{It("INFERIOR_COCOON")},{It(potions[Math.Max(kitIdx, 0)])},{It("EARTH_CRYSTAL")}", Ev());
        EventEnd(858);
        Warp(-90.0f, 1.0f, -50.0f, SouthernSandoria);
        TalkTo("Faulpie");
        Check("Southern San d'Oria's Faulpie: event 944",
              $"034:{NpcId("Faulpie")}:944:{kit},{It("SHEET_OF_FINE_PARCHMENT")},{It("SHEET_OF_PARCHMENT")},{It("SHEET_OF_PARCHMENT")},{It("PUMICE_STONE")},{It("DARK_CRYSTAL")}", Ev());
        EventEnd(944);
        Warp(0.0f, 0.0f, 0.0f, BastokMines);
        TalkTo("Abd-al-Raziq");
        Check("Bastok Mines' Abd-al-Raziq: event 590",
              $"034:{NpcId("Abd-al-Raziq")}:590:{kit},{It("JAR_OF_ENCHANTED_INK")},{It("JAR_OF_BLACK_INK")},{It("DARK_CRYSTAL")},{It("VIAL_OF_MAGICKED_BLOOD")},{It("DARK_CRYSTAL")}", Ev());
        EventEnd(590);
        // the kit is a synthesis (not driven here): a GM's
        Lua($"player:addItem({kit})");
        Warp(-120.3f, -19.5f, 306.7f, QufimIsland);
        ushort otherKit = ushort.Parse(Lua($"report(xi.item.{kits[(kitIdx + 1) % 4]})"));
        Lua($"player:addItem({otherKit})");
        TradeTo("Trodden_Snow", (Slot(otherKit), 1));
        Check("Qufim's Trodden Snow, another kit traded: no event", "none", Cur());
        // the handler's npcUtil.tradeMatches(trade, { asaKit, 1 }) takes each entry as an { id, qty } table: entry[1] on a
        // number, the handler's error (caught by the framework), no 44 for any kit
        int tradeErrors = log.Count(l => l.Contains("Error running handler") && l.Contains("npc_util"));
        TradeTo("Trodden_Snow", (Slot(ushort.Parse(kit)), 1));
        Check("  the Option's kit traded: the handler errors (tradeMatches given a flat list), no 44", "True/False",
              $"{log.Count(l => l.Contains("Error running handler") && l.Contains("npc_util")) > tradeErrors}/{Cur() == "44"}");
        EndAny();
        // 44's finish a GM's: the mission's reward (the six seals), the kit taken
        Lua($"player:delItem({kit}, 1) player:completeMission({LogAsa}, {AsaId("THAT_WHICH_CURDLES_BLOOD")}) " +
            "for _, k in ipairs({ xi.keyItem.DOMINAS_SCARLET_SEAL, xi.keyItem.DOMINAS_CERULEAN_SEAL, xi.keyItem.DOMINAS_EMERALD_SEAL, " +
            "xi.keyItem.DOMINAS_AMBER_SEAL, xi.keyItem.DOMINAS_VIOLET_SEAL, xi.keyItem.DOMINAS_AZURE_SEAL }) do player:addKeyItem(k) end " +
            $"player:addMission({LogAsa}, {AsaId("SUGAR_COATED_DIRECTIVE")})");
        string[] seals = { "DOMINAS_SCARLET_SEAL", "DOMINAS_CERULEAN_SEAL", "DOMINAS_EMERALD_SEAL", "DOMINAS_AMBER_SEAL", "DOMINAS_VIOLET_SEAL", "DOMINAS_AZURE_SEAL" };
        Check("  (GM) complete: the kit taken, the six Domina seals, Sugar-Coated Directive current", $"0/True/{AsaId("SUGAR_COATED_DIRECTIVE")},0",
              $"{Qty(ushort.Parse(kit))}/{seals.All(k => DbKeyItem(Ki(k)) == "10")}/{Mission(LogAsa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ASA 4 Sugar-Coated Directive (the Cloister of Flames)");
        Warp(0.0f, 0.0f, 0.0f, CloisterOfFlames);
        var fp = NpcId("FP_Entrance");
        Near(fp);
        Talk(fp);
        Check("the FP Entrance with the Scarlet Seal: POWER_STYMIES, Ifrit 1", $"{Text(CloisterOfFlames, "POWER_STYMIES")}:{Ki("DOMINAS_SCARLET_SEAL")}/1",
              $"{Special()}/{SVar(AsaId("SUGAR_COATED_DIRECTIVE"), "Ifrit")}");
        EndAny();
        WinBattlefield(fp, "Sugar-Coated Directive (Ifrit Prime)", rezone: false);
        Check("  won: the seal kept, battlefieldWin set", $"10/{Lua("report(xi.battlefield.id.SUGAR_COATED_DIRECTIVE_CLOISTER_OF_FLAMES)")}",
              $"{DbKeyItem(Ki("DOMINAS_SCARLET_SEAL"))}/{Lua("report(player:getLocalVar('battlefieldWin'))")}");
        Near(fp);
        Talk(fp);
        Check("the FP Entrance after the win: event 2", $"032:{fp}:2", Ev());
        EventEnd(2);
        // the event's finish sits in the NPC's table (['FP_Entrance'].onEventFinish), which the interaction framework
        // never dispatches (onEventFinish is zone-wide only): no seal attached, no counterseal
        Check("  finished: nothing (the NPC-level onEventFinish not dispatched): the seal kept, no Scarlet Counterseal", "10/00",
              $"{DbKeyItem(Ki("DOMINAS_SCARLET_SEAL"))}/{DbKeyItem(Ki("SCARLET_COUNTERSEAL"))}");
        // three counterseals a GM's
        Lua("player:addKeyItem(xi.keyItem.SCARLET_COUNTERSEAL) player:addKeyItem(xi.keyItem.AZURE_COUNTERSEAL) player:addKeyItem(xi.keyItem.EMERALD_COUNTERSEAL)");
        string gil4 = Gil();
        Warp(-120.3f, -19.5f, 306.7f, QufimIsland);
        TalkTo("Trodden_Snow");
        Check("the Trodden Snow with three counterseals (GM): event 45", $"032:{NpcId("Trodden_Snow")}:45", Ev());
        EventEnd(45);
        Check("  3000 gil, the seals and counterseals taken, Enemy of the Empire I current",
              $"{long.Parse(gil4) + 3000}/True/{AsaId("ENEMY_OF_THE_EMPIRE_I")},0",
              $"{Gil()}/{seals.Concat(new[] { "SCARLET_COUNTERSEAL", "AZURE_COUNTERSEAL", "EMERALD_COUNTERSEAL" }).All(k => DbKeyItem(Ki(k))[0] == '0')}/{Mission(LogAsa)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ASA 5 Enemy of the Empire I (Andrause's soul plates)");
        Warp(-19.0f, 1.0f, -58.0f, Norg);
        TalkTo("Andrause");
        string AsaVar(string n) => SVar(AsaId("ENEMY_OF_THE_EMPIRE_I"), n);
        int m1 = int.Parse(AsaVar("MobOne")), m2 = int.Parse(AsaVar("MobTwo")), m3 = int.Parse(AsaVar("MobThree"));
        Check("Andrause: event 237, three distinct mobs of twenty-three (utils.uniqueRandomTable)", "237/True",
              $"{Cur()}/{new[] { m1, m2, m3 }.Distinct().Count() == 3 && new[] { m1, m2, m3 }.All(m => m is >= 1 and <= 23)}");
        EventEnd(237, 4, 1);
        Check("  option 4: updateEvent(Soultrapper, Blank Soul Plate, 2477)", $"{It("SOULTRAPPER")},{It("BLANK_SOUL_PLATE")},2477", Upd());
        EventEnd(237, 5, 1);
        Check("  option 5: AndrauseBuy 1", "1", AsaVar("AndrauseBuy"));
        string gil5 = Gil();
        EventEnd(237);
        Check("  finished: 800 gil, the Soultrapper, the soul plate pickup time (JST midnight)",
              $"{long.Parse(gil5) - 800}/1/{Lua("report(JstMidnight())")}", $"{Gil()}/{Qty(ushort.Parse(It("SOULTRAPPER")))}/{AsaVar("Soulplate")}");
        TalkTo("Andrause");
        Check("  again: event 238 (three plates to go)", $"034:{NpcId("Andrause")}:238:3", Ev());
        EndAny();
        // the plates as the Soultrapper writes them (xi.znm.soultrapper.onItemUse's setExData; the mobs a GM's): the
        // signatures of Enemy of the Empire I's mobList
        string[] signatures = { "Bigclaw", "BrookSahagin", "RiparianSahagin", "RivuletSahagin", "RoyalLeech", "UndeadBats", "GrottoPugil", "SeaBonze",
                                "BlubberEyes", "BogSahagin", "MarshSahagin", "RockCrab", "RazorjawPugil", "SahaginParasite", "SwampSahagin", "DevilManta",
                                "DireBat", "LagoonSahagin", "DeltaSahagin", "CoastalSahagin", "Mousse", "RobberCrab", "ShoreSahagin" };
        ushort soulPlate = ushort.Parse(It("SOUL_PLATE"));
        void Plate(string sig) => Lua($"local p = player:addItem({{ id = {soulPlate}, silent = true }}) p:setExData({{ signature = '{sig}', zoneId = 176, level = 40 }})");
        int plateErrors = log.Count(l => l.Contains("Error running handler") && l.Contains("npc_util"));
        Plate(signatures[m1 - 1]);
        TradeTo("Andrause", (Slot(soulPlate), 1));
        // the handler's npcUtil.tradeMatches(trade, { xi.item.SOUL_PLATE, 1 }) (a flat list, as That Which Curdles Blood's):
        // entry[1] on a number, the handler's error, no 241 (nor 242) for any plate
        Check("  a requested mob's plate traded: the handler errors (tradeMatches given a flat list), no 241", "True/none/-",
              $"{log.Count(l => l.Contains("Error running handler") && l.Contains("npc_util")) > plateErrors}/{Cur()}/{AsaVar("Plates")}");
        EndAny();
        Lua($"player:delItem({soulPlate}, 1)");
        // the three trades' var updates a GM's, 241 started by a GM after each: its updates and finish the script's
        int[] wanted = { m1, m2, m3 };
        string[] mobVars = { "MobOne", "MobTwo", "MobThree" };
        for (int i = 0; i < 3; i++)
        {
            Lua($"player:setCharVar('Mission[{LogAsa}][{AsaId("ENEMY_OF_THE_EMPIRE_I")}]{mobVars[i]}', 99) " +
                $"player:setCharVar('Mission[{LogAsa}][{AsaId("ENEMY_OF_THE_EMPIRE_I")}]Plates', {i + 1}) " +
                $"player:setCharVar('Mission[{LogAsa}][{AsaId("ENEMY_OF_THE_EMPIRE_I")}]Found', {wanted[i]}) player:startEvent(241)");
            EventEnd(241, 0, 1);
            Check($"  (GM) the plate of {signatures[wanted[i] - 1]} ({i + 1} traded): 241's update (0, {3 - i} to go)", $"0,{3 - i}", Upd());
            EventEnd(241);
            if (i < 2) Check("    not complete yet", $"{AsaId("ENEMY_OF_THE_EMPIRE_I")},0", Mission(LogAsa));
        }
        Check("  the third's 241: complete, the Black Book, Enemy of the Empire II current (a TODO stub, as the rest of ASA)",
              $"10/{AsaId("ENEMY_OF_THE_EMPIRE_II")},0", $"{DbKeyItem(Ki("BLACK_BOOK"))}/{Mission(LogAsa)}");
        TalkTo("Andrause");
        Check("  Andrause with the Black Book: event 240", "240", Cur());
        EndAny();
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
    }
}
