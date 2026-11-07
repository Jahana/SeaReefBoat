using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Content6.Live -- live gate for the Escha / Reisenjima open world, the Adoulin-era doors and confluxes and the
// ENMs, driven as a client drives them through the real Lua scripts: Escha - Zi'Tah, Escha - Ru'Aun and Reisenjima
// (scripts/globals/teleports/eschan_portals.lua: the Eschan Portals and Ethereal Ingresses, their unlock bits and
// char_unlocks, the droplet ???s and their temporary items, the silt; the Undulating Confluence; an unscripted Geas Fete
// ???; the Eschan Gargouilles and Naga Raja's Hundred Fists, draw-in and title), Mamook's Incursion doors, the Walk of
// Echoes' Veridical Confluxes, and the ENMs (scripts/globals/enm.lua's timer NPCs; Zebada, Jakaka, Twinkbrix, Ghebi
// Damomohe, Venessa and Morangeart with their key items, trades, dice and cooldowns; Brothers won with its crate,
// Bionic Bug's Bugboy, Test Your Mite's level cap, TP drain and time-out, Like the Wind's experimental levels, Fire in the
// Sky's Razon phases). Geas Fete's NM pops, Wildskeeper Reives, Delve, Incursion itself and Vagary have no scripts (their
// NMs and ???s are zone data only). The 0x00A login, 0x015 moves, 0x01A Talk and Attack, 0x036 trades, 0x05B event ends
// and updates, 0x05C the arena asked for, the zone's ticks; the event, message, chat and BATTLE2 packets, char vars, key
// items, items, currencies, char_unlocks, char_exp. The real scripts on the real MariaDB (xidb), chars 996101-996109.
// The randomness is forced through math.randomInt queues (filtered to the script files that roll), the clock pinned.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static uint CharId = 996101;
    private const uint EschaId = 996101, EchoesId = 996102, EnmId = 996103, ShaftId = 996104, MiteId = 996105, VenessaId = 996106, RazonId = 996107;
    private static readonly uint[] CharIds = { 996101, 996102, 996103, 996104, 996105, 996106, 996107, 996108, 996109 };
    private const ushort EschaZiTah = 288, EschaRuAun = 289, Reisenjima = 291, QufimIsland = 126, WalkOfEchoes = 182, XarcabardS = 137,
                         UleguerandRange = 5, BearclawPinnacle = 6, AttohwaChasm = 7, BoneyardGully = 8, UpperJeuno = 244, OldtonMovalpolos = 11,
                         MineShaft2716 = 13, Mamook = 65, JadeSepulcher = 67, GraubergS = 89, SouthernSandoria = 230, LowerJeuno = 245, TheShroudedMaw = 10, RuLudeGardens = 243, TavnazianSafehold = 26, MonarchLinn = 31;
    private const byte Windurst = 2;
    private const byte War = 1;

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

    private static byte[] DbBlob(IDatabase db, string col, string table = "chars")
    {
        var r = Sql(db, $"SELECT {col} FROM {table} WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.GetBlobBytes(col) ?? Array.Empty<byte>() : Array.Empty<byte>();
    }

    // every char table the logins, the scripts and the zone changes write
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        // the tables chars' char_insert trigger fills, by name (an aborted run must not leave them behind)
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
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

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

    // a BATTLE2's first result's additional effect block: kind / param / message
    private static (int Add, int AddParam, int AddMsg) FirstAddEffect(BasicPacket p)
    {
        int at = 186 + 85;
        if (U(p, at, 1) == 0) return (0, 0, 0);
        return ((int)U(p, at + 1, 6), (int)U(p, at + 11, 17), (int)U(p, at + 28, 10));
    }

    private static readonly string[] JobColumns = { "", "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng" };

    // a Hume of the nation and the job at level `lvl` at x / y / z in the zone, with 100000 gil and the items from
    // inventory slot 1 on
    private static void Fixture(IDatabase db, uint id, ushort zoneId, float x, float y, float z, byte nation, byte job, byte lvl,
                                params (ushort Item, uint Qty)[] items)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of($"content6live{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Csixlive{id % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of(nation), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, ?, ?)", BoundValue.Of(id), BoundValue.Of(job), BoundValue.Of(lvl));
        Sql(db, $"REPLACE INTO char_jobs(charid, {JobColumns[job]}) VALUES (?, ?)", BoundValue.Of(id), BoundValue.Of(lvl));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,100000)", BoundValue.Of(id));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(id), BoundValue.Of((byte)(i + 1)), BoundValue.Of(items[i].Item), BoundValue.Of(items[i].Qty));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        long earth0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long EarthNow() => earth0 + (now - 1_000_000) / 1000;
        zone.UnixNow = EarthNow;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        scripts.VanaTicksNow = () => vanaTicks;
        // the wall clock VanadielTime reads (the ENM cooldowns) on the harness's time
        scripts.UnixNowMs = () => earth0 * 1000 + (now - 1_000_000);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)EarthNow()); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_content6");
            return string.Join(",", got);
        }
        var expectedWarnings = new HashSet<string>();
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                      !expectedWarnings.Contains(l)).Distinct().ToList();
        // the errors so far taken as expected (a script bug the check before named), returned joined
        string TakeErrors()
        {
            var e = ScriptErrors();
            foreach (var l in e) expectedWarnings.Add(l);
            return string.Join(" | ", e);
        }

        // the scripts' math.randomInt / math.random, queued (the engine's own when the queue is empty); with a filter set
        // only the calls from the script files it names draw from the queue (the other zones the host keeps ticking roll
        // too: a chocobo in San d'Oria, a goblin bounty hunter)
        lua.DoString(@"
            verifyInts, verifyRandoms, verifyFilter = {}, {}, nil
            local engineInt, engineRandom = math.randomInt, math.random
            local function fromFilter()
                if not verifyFilter then return true end
                local info = debug.getinfo(3, 'S')
                if info == nil then return false end
                for file in string.gmatch(verifyFilter, '[^,]+') do
                    if string.find(info.short_src, file, 1, true) ~= nil then return true end
                end
                return false
            end
            verifyIntArgs = {}
            math.randomInt = function(lo, hi)
                if verifyFilter and fromFilter() then table.insert(verifyIntArgs, lo .. '-' .. hi) end
                if #verifyInts > 0 and fromFilter() then return table.remove(verifyInts, 1) end
                return engineInt(lo, hi)
            end
            math.random = function(...)
                if #verifyRandoms > 0 and fromFilter() then return table.remove(verifyRandoms, 1) end
                return engineRandom(...)
            end", "=verify_content6_random");
        void Ints(params int[] v) => lua.DoString($"verifyInts = {{ {string.Join(", ", v)} }}", "=verify_content6_ints");
        void IntsFrom(string file, params int[] v)
        {
            lua.DoString($"verifyFilter = '{file}'", "=verify_content6_filter");
            Ints(v);
        }
        string QueuesLeft() => Lua("report(#verifyInts .. '/' .. #verifyRandoms)");

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

        // the 0x00A login and 0x00C; the zone-in cutscene ended with 0x05B as the client does
        void Login(bool endEvents = true)
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            Send(0x00C, 12);
            for (int i = 0; i < 4 && endEvents && c.IsInEvent; i++)
            {
                var ev = (ushort)c.CurrentEvent.EventId;
                Send(0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
            }
            s.PacketList.Clear();
        }
        void Tick(int ms = 1000)
        {
            now += ms;
            zone.Tick();
        }
        // GP_CLI_COMMAND_POS
        void MoveTo(float x, float y, float z, byte dir = 0)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
        }
        NpcEntity Npc(uint id) => zone.ZoneInfoOf(c.Zone).NpcById[id];
        uint NpcId(string name) => zone.ZoneInfoOf(c.Zone).Npcs.First(n => n.Name == name).Id;
        uint MobId(string name) => zone.ZoneInfoOf(c.Zone).Mobs.First(m => m.Name == name).Id;
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
        void TalkTo(string name)
        {
            var id = NpcId(name);
            Near(id);
            Talk(id);
        }
        // GP_CLI_COMMAND_EVENTEND: mode 0 ends the event, mode 1 asks for an update
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
        // GP_CLI_COMMAND_ITEM_TRANSFER: (inventory slot, quantity) offers; slot 0 is the gil
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
        void TradeTo(string name, params (byte Slot, uint Qty)[] items)
        {
            var id = NpcId(name);
            Near(id);
            Trade(id, items);
        }
        // a zone change the scripts asked for (setPos to another zone): the tick that zones the char, the next login
        void Rezone(bool endEvents = true)
        {
            Tick(400);
            Login(endEvents);
        }
        // a GM's !pos to another zone
        void WarpTo(ushort zoneId, float x, float y, float z)
        {
            Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
            Rezone();
            MoveTo(x, y, z);
            s.PacketList.Clear();
        }

        // the event packet the server sent: 0x032 / 0x034 as op:entity:event[:params]
        string Ev()
        {
            var ev = new List<BasicPacket>(s.PacketList).LastOrDefault(p => Opcode(p) is 0x032 or 0x034);
            if (ev == null) return "none";
            var b = ev.Buffer;
            if (Opcode(ev) == 0x032) return $"032:{BitConverter.ToUInt32(b, 4)}:{BitConverter.ToUInt16(b, 12)}";
            var pars = new List<string>();
            for (int i = 0; i < S2cEventNum.NumCount; i++) pars.Add(BitConverter.ToUInt32(b, 4 + S2cEventNum.OffNum + i * 4).ToString());
            while (pars.Count > 0 && pars[^1] == "0") pars.RemoveAt(pars.Count - 1);
            return $"034:{BitConverter.ToUInt32(b, 4 + S2cEventNum.OffUniqueNo)}:{BitConverter.ToUInt16(b, 4 + S2cEventNum.OffEventPara)}" +
                   (pars.Count > 0 ? ":" + string.Join(",", pars) : "");
        }
        string EvStr(uint id, int ev, params long[] ps)
        {
            var l = ps.Select(x => ((uint)x).ToString()).ToList();
            while (l.Count > 0 && l[^1] == "0") l.RemoveAt(l.Count - 1);
            if (l.Count == 0) return ps.Length == 0 ? $"032:{id}:{ev}" : $"034:{id}:{ev}";
            return $"034:{id}:{ev}:" + string.Join(",", l);
        }
        // the 0x05C updates' params (each update one entry, trailing zeros dropped)
        string Upds() => string.Join(" | ", Of(0x05C).Select(p =>
        {
            var pars = Enumerable.Range(0, 8).Select(i => BitConverter.ToUInt32(p.Buffer, 4 + S2cPendingnum.NumOffset(i)).ToString()).ToList();
            while (pars.Count > 0 && pars[^1] == "0") pars.RemoveAt(pars.Count - 1);
            return string.Join(",", pars);
        }));
        string Cur() => c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "none";
        // the 0x02A TALKNUMWORK messages as message:param0[:param1..]
        string SpecialOf(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x02A).Select(p =>
        {
            var pars = Enumerable.Range(0, 4).Select(i => BitConverter.ToUInt32(p.Buffer, 8 + i * 4)).ToList();
            while (pars.Count > 1 && pars[^1] == 0) pars.RemoveAt(pars.Count - 1);
            return $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:" + string.Join(":", pars);
        }));
        string Special() => SpecialOf(s.PacketList);
        int Text(ushort zoneId, string name) => int.Parse(Lua($"report(zones[{zoneId}].text.{name})"));
        string Var(string name) => Lua($"report(player:getCharVar('{name}'))");
        string DbVar(string name) => DbRow(db, "value", $"FROM char_vars WHERE charid = ? AND varname = '{name}'");
        bool HasKi(string name) => Lua($"report(tostring(player:hasKeyItem(xi.keyItem.{name})))") == "true";
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        ushort ItemId(string name) => ushort.Parse(Lua($"report(xi.item.{name})"));
        string Currency(string name) => Lua($"report(player:getCurrency('{name}'))");
        uint Qty(Container loc, ushort itemId) => (uint)c.Items.Where(i => i.Location == loc && i.ItemId == itemId).Sum(i => i.Quantity);
        byte Slot(ushort itemId) => c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == itemId && i.Quantity > 0).OrderBy(i => i.Slot).First().Slot;
        string DbQty(ushort itemId, int loc = 0) => DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND location = {loc} AND itemId = {itemId}");
        // keyitems_t: per table of 512, the keyList bits then the seenList bits
        string DbKeyItem(int ki)
        {
            var k = DbBlob(db, "keyitems");
            int off = ki / 512 * 128 + ki % 512 / 8, bit = 1 << ki % 8;
            bool has = off < k.Length && (k[off] & bit) != 0, seen = off + 64 < k.Length && (k[off + 64] & bit) != 0;
            return $"{(has ? 1 : 0)}{(seen ? 1 : 0)}";
        }
        string MobLocal(uint id, string name) => Lua($"report(GetMobByID({id}):getLocalVar('{name}'))");
        void Attack(MobEntity m) => Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(m.Id).CopyTo(b, 4);
            BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
        });
        byte Facing(float fromX, float fromZ, float toX, float toZ) => (byte)(short)(MathF.Atan2(toZ - fromZ, toX - fromX) * -(128.0f / MathF.PI));
        // the char's swing claims the mob (the char kept beside it and facing it as the client does)
        void ClaimBySwing(MobEntity m)
        {
            MoveTo(m.X - 2.0f, m.Y, m.Z, Facing(m.X - 2.0f, m.Z, m.X, m.Z));
            Attack(m);
            for (int i = 0; m.OwnerId != c.Id && i < 30; i++)
            {
                if (c.AttackTarget != m) Attack(m);
                MoveTo(m.X - 2.0f, m.Y, m.Z, Facing(m.X - 2.0f, m.Z, m.X, m.Z));
                Tick(1000);
            }
        }
        // a mob claimed by the char's swing, then the char's blow for all its HP (takeDamage with the char as the
        // attacker): the death on the next tick, the defeat (onMobDeath, the battlefield's allDeath) a second on
        void KillByDamage(MobEntity m)
        {
            ClaimBySwing(m);
            Lua($"local mob = GetMobByID({m.Id}) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            for (int i = 0; i < 5; i++) Tick(400);
            Lua("player:setHP(player:getMaxHP())");
        }
        // chars.titles: the bit of the title
        bool DbTitle(int title)
        {
            var t = DbBlob(db, "titles");
            return title / 8 < t.Length && (t[title / 8] & 1 << title % 8) != 0;
        }

        // ---- Escha - Zi'Tah: the Eschan Portals ----
        Console.WriteLine("Escha - Zi'Tah: Eschan Portal #1, the first touch (its bit unlocked, no other to warp to)");
        Fixture(db, EschaId, EschaZiTah, -344.0f, 1.6f, -180.0f, Windurst, War, 99);
        CharId = EschaId;
        Login();
        Check("in Escha - Zi'Tah", EschaZiTah, c.Zone);
        Lua("player:addCurrency('escha_silt', 200)");
        Check("200 escha silt (a GM's !addcurrency): char_points", "200/200", $"{Currency("escha_silt")}/{DbRow(db, "escha_silt", "FROM char_points WHERE charid = ?")}");
        uint portal1 = NpcId("Eschan_Portal_#1"), portal2 = NpcId("Eschan_Portal_#2");
        TalkTo("Eschan_Portal_#1");
        // startEvent(9100, 0, mask, zone, portal, lock, silt, cost): one portal of the zone unlocked: the mask is bit 0 alone
        Check("event 9100: mask bit 0 (one portal: the menu's own), Zi'Tah, portal 0, lock 1 (just unlocked), 200 silt, the cost 50",
              EvStr(portal1, 9100, 0, 1, EschaZiTah, 0, 1, 200, 50), Ev());
        Check("  addTeleport(ESCHAN_PORTAL, 0): getTeleport 1, char_unlocks.eschan_portals 1", "1/1",
              $"{Lua("report(player:getTeleport(xi.teleport.type.ESCHAN_PORTAL))")}/{DbRow(db, "eschan_portals", "FROM char_unlocks WHERE charid = ?")}");
        EventEnd(9100, 0);
        Check("  option 0 (no warp): no silt spent, the event over", "200/none", $"{Currency("escha_silt")}/{Cur()}");

        Console.WriteLine("Escha - Zi'Tah: Eschan Portal #2 (a second portal: the menu shows both), a warp paid in silt");
        TalkTo("Eschan_Portal_#2");
        Check("event 9100: mask bits 0-1 (3), portal 1, lock 1, 200 silt", EvStr(portal2, 9100, 0, 3, EschaZiTah, 1, 1, 200, 50), Ev());
        EventEnd(9100, 1);
        Check("  option 1 (a warp): 50 silt spent (getPortalCost: no Vorseal Luck+), char_points", "150/150/none",
              $"{Currency("escha_silt")}/{DbRow(db, "escha_silt", "FROM char_points WHERE charid = ?")}/{Cur()}");
        TalkTo("Eschan_Portal_#1");
        Check("Portal #1 again: already unlocked, lock 0", EvStr(portal1, 9100, 0, 3, EschaZiTah, 0, 0, 150, 50), Ev());
        uint cancelled = uint.Parse(Lua("report(utils.EVENT_CANCELLED_OPTION)"));
        EventEnd(9100, cancelled);
        Check("  cancelled (EVENT_CANCELLED_OPTION): no silt spent", "150", Currency("escha_silt"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Escha - Zi'Tah: a Geas Fete ??? (no script: nothing happens) and the Undulating Confluence");
        var qm = zone.ZoneInfoOf(EschaZiTah).Npcs.First(n => n.Name == "qm");
        Near(qm.Id);
        Talk(qm.Id);
        Check($"the ??? {qm.Name} (no scripts/zones/Escha_ZiTah/npcs/{qm.Name}.lua): no event, no message, the client released (0x052)", "none/none//1",
              $"{Ev()}/{Cur()}/{Special()}/{Of(0x052).Count}");
        uint confluence = NpcId("Undulating_Confluence");
        TalkTo("Undulating_Confluence");
        Check("the Undulating Confluence: event 4", EvStr(confluence, 4), Ev());
        EventEnd(4, 1);
        Rezone();
        Check("  option 1: xi.teleport.to(QUFIM_CONFLUENCE): Qufim Island at (-203, -20, 81)", $"{QufimIsland}/-203/-20/81",
              $"{c.Zone}/{c.X:F0}/{c.Y:F0}/{c.Z:F0}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Escha - Ru'Aun: the droplet ??? and the portals ----
        Console.WriteLine("Escha - Ru'Aun: Eschan Portal #1 with no portal and no droplet (the blinding light)");
        NpcEntity NpcIn(ushort zoneId, string name) => zone.ZoneInfoOf(zoneId).Npcs.First(n => n.Name == name);
        var ruPortal1 = NpcIn(EschaRuAun, "Eschan_Portal_#1");
        WarpTo(EschaRuAun, ruPortal1.X + 1.5f, ruPortal1.Y, ruPortal1.Z);
        Check("in Escha - Ru'Aun", EschaRuAun, c.Zone);
        Talk(ruPortal1.Id);
        // zones[ESCHA_RUAUN].text has no BLINDING_LIGHT_OBSCURES_YOUR_VISION: messageSpecial(nil) is a Lua error (sol's
        // uint16 from nil in C++), the trigger stops before its return and no event starts
        Check("no Ru'Aun portal unlocked, no droplet: BLINDING_LIGHT_OBSCURES_YOUR_VISION is nil in IDs.lua (a script bug): no event, no message",
              "nil/none/none/", $"{Lua("report(tostring(zones[xi.zone.ESCHA_RUAUN].text.BLINDING_LIGHT_OBSCURES_YOUR_VISION))")}/{Ev()}/{Cur()}/{Special()}");
        string blinding = TakeErrors();
        Check("  the trigger's Lua error logged (messageSpecial's nil message id)", "True", blinding.Contains("Eschan_Portal_#1") || blinding.Contains("messageSpecial"));
        // charutils::SaveTeleport binds the uint32 eschanPortal into the eschan_portals BLOB (MariaDB keeps the decimal
        // text, "3") and LoadChar's extractFromBlob reads those bytes back as the uint32: after a zone change the mask
        // is the text's ASCII ('3' = 0x33). A C++ bug the host keeps; the bits still never reach Ru'Aun's 8-22 here
        uint Reloaded(uint mask)
        {
            var b = new byte[4];
            var t = System.Text.Encoding.ASCII.GetBytes(mask.ToString());
            Array.Copy(t, b, Math.Min(4, t.Length));
            return BitConverter.ToUInt32(b, 0);
        }
        uint mask = Reloaded(3);
        Check("  the portal not unlocked; the mask reloaded from char_unlocks' text \"3\" (0x33: a C++ SaveTeleport / extractFromBlob bug)",
              $"{mask}/3", $"{Lua("report(player:getTeleport(xi.teleport.type.ESCHAN_PORTAL))")}/{DbRow(db, "eschan_portals", "FROM char_unlocks WHERE charid = ?")}");

        Console.WriteLine("Escha - Ru'Aun: the droplet ??? (a Clump of Eschan Droplets, a temporary item)");
        ushort eschanDroplets = ItemId("CLUMP_OF_ESCHAN_DROPLETS");
        TalkTo("qm_droplet");
        Check("qm_droplet: addTempItem: the droplets in the temporary items (container 3), char_inventory location 3", "1/1",
              $"{Qty(Container.TempItems, eschanDroplets)}/{DbQty(eschanDroplets, 3)}");
        Check("  the item list packet for the temporary bag (0x020 ITEM_ATTR, container 3)", "True",
              Of(0x020).Any(p => p.Buffer[14] == 3 && BitConverter.ToUInt16(p.Buffer, 12) == eschanDroplets));
        TalkTo("qm_droplet");
        Check("again: NOTHING_OUT_OF_ORDINARY, still one", $"{Text(EschaRuAun, "NOTHING_OUT_OF_ORDINARY")}:0/1",
              $"{Special()}/{Qty(Container.TempItems, eschanDroplets)}");

        Console.WriteLine("Escha - Ru'Aun: Eschan Portal #1 with the droplet (unlocked, the droplet's warp)");
        Near(ruPortal1.Id);
        Talk(ruPortal1.Id);
        // lock 2 (the droplet) + 1 (just unlocked); one Ru'Aun portal: the mask bit 8 alone
        Check("event 9100: mask 1 << 8, Ru'Aun, portal 8, lock 3 (droplet + just unlocked), 150 silt, 50",
              EvStr(ruPortal1.Id, 9100, 0, 256, EschaRuAun, 8, 3, 150, 50), Ev());
        mask |= 1 << 8;
        Check("  bit 8 unlocked: getTeleport, char_unlocks (the decimal text)", $"{mask}/{mask}",
              $"{Lua("report(player:getTeleport(xi.teleport.type.ESCHAN_PORTAL))")}/{DbRow(db, "eschan_portals", "FROM char_unlocks WHERE charid = ?")}");
        EventEnd(9100, 2);
        // zones[ESCHA_RUAUN].text has no YOU_HAVE_USED either: the droplet deleted, then messageSpecial(nil) errors
        Check("  option 2 (the droplet's warp): the droplet used up, no silt; YOU_HAVE_USED is nil in IDs.lua (a script bug): no message",
              "0/0/150/", $"{Qty(Container.TempItems, eschanDroplets)}/{DbQty(eschanDroplets, 3)}/{Currency("escha_silt")}/{Special()}");
        Check("  the finish's Lua error logged", "True", TakeErrors().Length > 0);

        // ---- Reisenjima: the Ethereal Ingresses ----
        Console.WriteLine("Reisenjima: the ethereal droplet ??? and Ethereal Ingress #10 (Scintillating Rhapsody)");
        var ingress10 = NpcIn(Reisenjima, "Ethereal_Ingress_#10");
        WarpTo(Reisenjima, ingress10.X + 1.5f, ingress10.Y, ingress10.Z);
        Check("in Reisenjima", Reisenjima, c.Zone);
        ushort etherealDroplet = ItemId("ETHEREAL_DROPLET");
        TalkTo("qm_ethereal_droplet");
        Check("qm_ethereal_droplet: the Ethereal Droplet (temporary), ITEM_OBTAINED", $"1/{Text(Reisenjima, "ITEM_OBTAINED")}:{etherealDroplet}",
              $"{Qty(Container.TempItems, etherealDroplet)}/{Special()}");
        TalkTo("qm_ethereal_droplet");
        Check("again: NOTHING_OUT_OF_ORDINARY", $"{Text(Reisenjima, "NOTHING_OUT_OF_ORDINARY")}:0/1", $"{Special()}/{Qty(Container.TempItems, etherealDroplet)}");
        Lua("player:addKeyItem(xi.keyItem.SCINTILLATING_RHAPSODY)");
        Near(ingress10.Id);
        Talk(ingress10.Id);
        // the Rhapsody (lock + 4, one more portal), the droplet (+ 2); #10 is bit 32: never unlocked (the exception);
        // one portal counted: the mask bit 16 alone
        Check("Ingress #10: mask 1 << 16, Reisenjima, portal 32, lock 6 (Rhapsody + droplet), 150 silt",
              EvStr(ingress10.Id, 9100, 0, 65536, Reisenjima, 32, 6, 150, 50), Ev());
        mask = Reloaded(mask);
        Check("  bit 32 not added (the #10 exception); the mask the zone change reloaded from \"307\"", $"{mask}",
              Lua("report(player:getTeleport(xi.teleport.type.ESCHAN_PORTAL))"));
        EventEnd(9100, 3);
        Check("  option 3: the droplet used, YOU_HAVE_USED, no silt", $"0/{Text(Reisenjima, "YOU_HAVE_USED")}:{etherealDroplet}/150",
              $"{Qty(Container.TempItems, etherealDroplet)}/{Special()}/{Currency("escha_silt")}");

        Console.WriteLine("Reisenjima: Ethereal Ingress #1 (unlocked: two portals with the Rhapsody), the Rhapsody's warp, a paid warp");
        var ingress1 = NpcIn(Reisenjima, "Ethereal_Ingress_#1");
        Near(ingress1.Id);
        Talk(ingress1.Id);
        mask |= 1 << 23;
        Check("Ingress #1: bit 23 added, the whole mask (two Reisenjima portals with the Rhapsody), portal 23, lock 5 (Rhapsody + just unlocked)",
              EvStr(ingress1.Id, 9100, 0, mask, Reisenjima, 23, 5, 150, 50), Ev());
        EventEnd(9100, 4);
        Check("  option 4 (the Rhapsody's portals): no silt", "150", Currency("escha_silt"));
        Talk(ingress1.Id);
        Check("again: lock 4 (the Rhapsody)", EvStr(ingress1.Id, 9100, 0, mask, Reisenjima, 23, 4, 150, 50), Ev());
        EventEnd(9100, 2);
        Check("  option 2: 50 silt (char_points)", "100/100", $"{Currency("escha_silt")}/{DbRow(db, "escha_silt", "FROM char_points WHERE charid = ?")}");
        // Ingress #9 is bit 31: addTeleport's 1 << 31 kept as the uint32 0x80000000 (getTeleport positive)
        var ingress9 = NpcIn(Reisenjima, "Ethereal_Ingress_#9");
        Near(ingress9.Id);
        Talk(ingress9.Id);
        mask |= 1u << 31;
        Check("Ingress #9: bit 31 added (the mask's top bit, unsigned), portal 31, lock 5", $"{EvStr(ingress9.Id, 9100, 0, mask, Reisenjima, 31, 5, 100, 50)}/{mask}/{mask}",
              $"{Ev()}/{Lua("report(string.format('%d', player:getTeleport(xi.teleport.type.ESCHAN_PORTAL)))")}/{DbRow(db, "eschan_portals", "FROM char_unlocks WHERE charid = ?")}");
        EventEnd(9100, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Escha - Ru'Aun: the Eschan Gargouilles and Naga Raja ----
        Console.WriteLine("Escha - Ru'Aun: an Eschan Gargouille (a statue until it engages)");
        WarpTo(EschaRuAun, 20.0f, -42.0f, -300.0f);
        Lua("player:setUnkillable(true)");
        var garg = zone.ZoneInfoOf(EschaRuAun).Mobs.First(m => m.Name == "Eschan_Gargouille");
        string GargState() => Lua($"local m = GetMobByID({garg.Id}) report(tostring(m:isNameHidden())) report(tostring(m:getUntargetable())) report(m:getAnimationSub())").Replace(",", "/");
        // the template's type [event] is the mob type, not the spawn: a normal spawn (respawn 180) up at the zone's start
        Check("up at the zone's start; onMobSpawn hid the name, untargetable, animation sub 6 (the statue)", "True/true/true/6", $"{garg.IsSpawned}/{GargState()}");
        s.PacketList.Clear();
        Attack(garg);
        Tick(400);
        Check("  the char's Attack on it refused (untargetable): not engaged, not claimed", "False/0", $"{c.AttackTarget == garg}/{garg.OwnerId}");
        Lua($"GetMobByID({garg.Id}):updateEnmity(player)");
        for (int i = 0; i < 3; i++) Tick(400);
        Check("its enmity on the char: engaged; onMobEngage shows the name, targetable, animation sub 0", "True/false/false/0",
              $"{garg.Engaged}/{GargState()}");
        Lua($"DespawnMob({garg.Id})");
        Tick(400);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Escha - Ru'Aun: Naga Raja (the [2hour]HPP roll forced to 40: Hundred Fists below it, draw-in, the title)");
        var naga = zone.ZoneInfoOf(EschaRuAun).Mobs.First(m => m.Name == "Naga_Raja");
        IntsFrom("Naga_Raja.lua", 40);
        Lua($"SpawnMob({naga.Id})");
        Check("SpawnMob: onMobSpawn: NO_MOVE, BASE_DAMAGE_MULTIPLIER 500, [2hour]HPP (randomInt(20, 65) forced) 40, not used, the roll drawn",
              "True/1/500/40/0/0/0", $"{naga.IsSpawned}/{Lua($"local m = GetMobByID({naga.Id}) report(m:getMobMod(xi.mobMod.NO_MOVE)) report(m:getMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER))").Replace(",", "/")}/" +
              $"{MobLocal(naga.Id, "[2hour]HPP")}/{MobLocal(naga.Id, "[2hour]Used")}/{QueuesLeft()}");
        ClaimBySwing(naga);
        // CMobController::Tick: an idle mob with spells buffs itself before its roam tick engages (DoBuffTick): the
        // engage waits out the cast
        for (int i = 0; i < 20 && !naga.Engaged; i++) Tick(1000);
        Check("claimed by the char's swing, engaged on the char", $"{c.Id}/True/True", $"{naga.OwnerId}/{naga.Engaged}/{naga.BattleTarget == c}");
        naga.Hp = naga.MaxHp / 2;
        Tick(1000);
        Tick(1000);
        Check("at 50%: no Hundred Fists", "0/False", $"{MobLocal(naga.Id, "[2hour]Used")}/{naga.StatusEffects.HasStatusEffect(46)}");
        naga.Hp = naga.MaxHp * 39 / 100;
        bool fists = false;
        for (int i = 0; i < 12 && !fists; i++)
        {
            Tick(500);
            fists = naga.StatusEffects.HasStatusEffect(46);
        }
        Check("at 39% (below 40): onMobFight's useMobAbility(HUNDRED_FISTS_1): [2hour]Used 1, the Hundred Fists effect on it", "1/True",
              $"{MobLocal(naga.Id, "[2hour]Used")}/{fists}");
        // draw_in: the target 2x its melee range off for over a second of combat ticks is drawn to the mob
        s.PacketList.Clear();
        MoveTo(naga.X - 20.0f, naga.Y, naga.Z, Facing(naga.X - 20.0f, naga.Z, naga.X, naga.Z));
        float far = MathF.Sqrt((c.X - naga.X) * (c.X - naga.X) + (c.Z - naga.Z) * (c.Z - naga.Z));
        for (int i = 0; i < 8 && Of(0x05B).Count == 0; i++) Tick(500);
        float near = MathF.Sqrt((c.X - naga.X) * (c.X - naga.X) + (c.Z - naga.Z) * (c.Z - naga.Z));
        Check("20 yalms off (the draw_in mixin's COMBAT_TICK): drawn in (0x05B WPOS, DRAWN_IN 232), at the mob", "True/1/True",
              $"{far >= 19.0f}/{Of(0x029).Count(p => BitConverter.ToUInt16(p.Buffer, 24) == 232)}/{near < 2.0f}");
        int nullifier = int.Parse(Lua("report(xi.title.NAGA_RAJA_NULLIFIER)"));
        // the char's bare-handed swings at a level 119 NM land too rarely to wait on (Paralyga, its evasion): its blow
        KillByDamage(naga);
        Check("killed by the char: onMobDeath's addTitle(NAGA_RAJA_NULLIFIER) (chars.titles), the current title", $"False/True/{nullifier}",
              $"{naga.IsAlive}/{DbTitle(nullifier)}/{Lua("report(player:getTitle())")}");
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Incursion (Mamook's doors) and the Walk of Echoes ----
        Console.WriteLine("Incursion: Mamook's Red Bell door (_1t1) and the landing's Mahogany Door (_1t0) (the event itself not coded)");
        Fixture(db, EchoesId, Mamook, -58.0f, 3.9f, 0.0f, Windurst, War, 99);
        CharId = EchoesId;
        Login();
        Check("in Mamook", Mamook, c.Zone);
        // 0x036 GP_SERV_COMMAND_TALKNUM as entity:mesnum:type
        string TalkNums() => string.Join(",", Of(0x036).Select(p => $"{BitConverter.ToUInt32(p.Buffer, 4)}:{BitConverter.ToUInt16(p.Buffer, 10)}:{p.Buffer[12]}"));
        uint redBell = NpcId("_1t1");
        TalkTo("_1t1");
        Check("the Red Bell door: messageText(npc, RED_BELL_LOCKED, false, 6): 0x036 with the name hidden (+0x8000), mode 6, no event",
              $"{redBell}:{Text(Mamook, "RED_BELL_LOCKED") + 0x8000}:6/none", $"{TalkNums()}/{Cur()}");
        // Mahogany_Door.lua's header names _1t0 (the landing's door), but the file drives the NPC named
        // Mahogany_Door (17043943, a ??? model by the Red Bell door's teleport): _1t0 has no script and is a plain door
        var landing = Npc(NpcId("_1t0"));
        TalkTo("_1t0");
        Check("_1t0 (no scripts/zones/Mamook/npcs/_1t0.lua): onTrigger -1, the door opens (animation 8, 0x00E), no event", "8/True/none",
              $"{landing.Animation}/{Of(0x00E).Any(p => BitConverter.ToUInt32(p.Buffer, 4) == landing.Id)}/{Cur()}");
        for (int i = 0; i < 20; i++) Tick(400);
        Check("  7 s on it swings shut (animation 9)", "9", landing.Animation);
        uint mahogany = NpcId("Mahogany_Door");
        TalkTo("Mahogany_Door");
        Check("Mahogany_Door (17043943): event 220", EvStr(mahogany, 220), Ev());
        EventEnd(220, 0);
        Check("  option 0: stays in Mamook", $"{Mamook}/none", $"{c.Zone}/{Cur()}");
        TalkTo("Mahogany_Door");
        EventEnd(220, 1);
        Rezone();
        Check("  option 1: setPos to the Jade Sepulcher (260, 8.5, -236)", $"{JadeSepulcher}/260/9/-236", $"{c.Zone}/{c.X:F0}/{c.Y:F0}/{c.Z:F0}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Walk of Echoes: the Veridical Confluxes out (Xarcabard [S], Grauberg [S])");
        WarpTo(WalkOfEchoes, -414.0f, 14.0f, -58.0f);
        Check("in the Walk of Echoes", WalkOfEchoes, c.Zone);
        uint conflux = NpcId("Veridical_Conflux");
        TalkTo("Veridical_Conflux");
        Check("Veridical Conflux: event 1004", EvStr(conflux, 1004), Ev());
        EventEnd(1004, cancelled);
        Check("  cancelled: stays", $"{WalkOfEchoes}/none", $"{c.Zone}/{Cur()}");
        TalkTo("Veridical_Conflux");
        EventEnd(1004, 0);
        Rezone();
        Check("  option 0: Xarcabard [S] (238, -8, -248)", $"{XarcabardS}/238/-8/-248", $"{c.Zone}/{c.X:F0}/{c.Y:F0}/{c.Z:F0}");
        WarpTo(WalkOfEchoes, -697.0f, 0.0f, -442.0f);
        uint confluxG = NpcId("Veridical_Conflux_G");
        TalkTo("Veridical_Conflux_G");
        Check("Veridical Conflux (Grauberg [S] exit): event 3", EvStr(confluxG, 3), Ev());
        EventEnd(3, 0);
        Check("  option 0: stays (only 99 leaves)", $"{WalkOfEchoes}/none", $"{c.Zone}/{Cur()}");
        TalkTo("Veridical_Conflux_G");
        EventEnd(3, 99);
        Rezone();
        Check("  option 99: Grauberg [S] (-143, -7, 580)", $"{GraubergS}/-143/-7/580", $"{c.Zone}/{c.X:F0}/{c.Y:F0}/{c.Z:F0}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- ENM: Zebada's Zephyr Fan, the timer NPCs, Brothers ----
        Console.WriteLine("ENM: Zebada in Uleguerand Range (the Cotton Pouch, the Chamnaet Ice traded, the Zephyr Fan)");
        Fixture(db, EnmId, UleguerandRange, -306.0f, -42.1f, -570.0f, Windurst, War, 75);
        CharId = EnmId;
        Login();
        Check("in Uleguerand Range", UleguerandRange, c.Zone);
        uint zebada = NpcId("Zebada");
        ushort cottonPouch = ItemId("COTTON_POUCH"), chamnaetIce = ItemId("HANDFUL_OF_CHAMNAET_ICE");
        int zephyrFan = Ki("ZEPHYR_FAN");
        TalkTo("Zebada");
        Check("no fan, no cooldown, no pouch nor ice: event 14", EvStr(zebada, 14), Ev());
        EventEnd(14);
        Check("  finished: npcUtil.giveItem: the Cotton Pouch, ITEM_OBTAINED", $"1/{Text(UleguerandRange, "ITEM_OBTAINED")}:{cottonPouch}/1",
              $"{Qty(Container.Inventory, cottonPouch)}/{Special()}/{DbQty(cottonPouch)}");
        TalkTo("Zebada");
        Check("the pouch held: event 16", EvStr(zebada, 16), Ev());
        EventEnd(16);
        // the pouch filled at the Chamnaet spring (a GM's !additem)
        Lua($"player:addItem({chamnaetIce})");
        Trade(zebada, (Slot(chamnaetIce), 1));
        Check("the Handful of Chamnaet Ice traded: tradeComplete (the ice gone), event 13", $"{EvStr(zebada, 13)}/0/0",
              $"{Ev()}/{Qty(Container.Inventory, chamnaetIce)}/{DbQty(chamnaetIce)}");
        long vanaNow = long.Parse(Lua("report(VanadielTime())"));
        int enmCooldown = int.Parse(Lua("report(xi.settings.main.ENM_COOLDOWN)"));
        long zephyrReady = vanaNow + enmCooldown * 3600L;
        EventEnd(13);
        Check("  finished: the Zephyr Fan (KEYITEM_OBTAINED, keyitems), [ENM]ZephyrFan = VanadielTime + ENM_COOLDOWN (120) hours (char_vars)",
              $"{Text(UleguerandRange, "KEYITEM_OBTAINED")}:{zephyrFan}/10/{zephyrReady}/{zephyrReady}",
              $"{Special()}/{DbKeyItem(zephyrFan)}/{Var("[ENM]ZephyrFan")}/{DbVar("[ENM]ZephyrFan")}");
        TalkTo("Zebada");
        Check("the fan held: event 12", EvStr(zebada, 12), Ev());
        EventEnd(12);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: the timer NPCs (Moritz's introduction, the Zephyr Fan's cooldown asked, Ophelia's recurring event)");
        WarpTo(UpperJeuno, 2.1f, 1.8f, 58.5f);
        Check("in Upper Jeuno", UpperJeuno, c.Zone);
        uint moritz = NpcId("Moritz");
        // getBitmaskForAvailableENMs: bit i for each CoP ENM the char's mission has not passed, bit 6 without the Pso'Xja pass
        int cop = int.Parse(Lua("report(player:getCurrentMission(xi.mission.log_id.COP))"));
        int[] copReqs = Lua("local m = xi.mission.id.cop report(m.THE_RITES_OF_LIFE) report(m.THE_RITES_OF_LIFE) report(m.THE_RITES_OF_LIFE) " +
                            "report(m.SLANDEROUS_UTTERINGS) report(m.AN_ETERNAL_MELODY)").Split(',').Select(int.Parse).ToArray();
        int enmMask = 1 << 6;
        for (int i = 1; i <= 5; i++) if (cop <= copReqs[i - 1]) enmMask |= 1 << i;
        TalkTo("Moritz");
        Check($"an ENM cooldown running, no introduction yet: the introduction 10028 with the ENM mask ({enmMask}: CoP mission {cop}, no Pso'Xja pass)",
              EvStr(moritz, 10028, enmMask), Ev());
        EventEnd(10028, 8, 1);
        Check("  update option 8 (Bearclaw Pinnacle): updateEvent(0, [ENM]ZephyrFan) (the time it is available)", $"0,{zephyrReady}", Upds());
        EventEnd(10028, 9, 1);
        Check("  update option 9 (Boneyard Gully, no cooldown): updateEvent(0)", "", Upds());
        EventEnd(10028, 0, 1);
        Check("  update option 0 (declined): updateEvent(1)", "1", Upds());
        EventEnd(10028, 0);
        Check("  finished: setUniqueEvent(ENM_TIMER_NPCS_INTRO)", "true/none",
              $"{Lua("report(tostring(player:hasCompletedUniqueEvent(xi.uniqueEvent.ENM_TIMER_NPCS_INTRO)))")}/{Cur()}");
        TalkTo("Moritz");
        Check("Moritz again: the recurring 10029", EvStr(moritz, 10029, enmMask), Ev());
        EventEnd(10029, 0);
        var ophelia = zone.ZoneInfoOf(SouthernSandoria).Npcs.First(n => n.Name == "Ophelia");
        WarpTo(SouthernSandoria, ophelia.X + 1.5f, ophelia.Y, ophelia.Z);
        Talk(ophelia.Id);
        Check("Ophelia (Southern San d'Oria): the introduction is shared: her recurring 753", EvStr(ophelia.Id, 753, enmMask), Ev());
        EventEnd(753, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Brothers (Bearclaw Pinnacle): Wind Pillar #4, the Zephyr Fan torn on entry");
        WarpTo(BearclawPinnacle, 121.5f, -171.4f, 760.0f);
        Check("in Bearclaw Pinnacle", BearclawPinnacle, c.Zone);
        Lua("player:setUnkillable(true)");
        var pillar = Npc(NpcId("Wind_Pillar_4"));
        Near(pillar.Id);
        Talk(pillar.Id);
        Check("Wind Pillar #4 talked to: the battlefield menu 32000", "32000", Cur());
        uint menu = uint.Parse(Lua("report(player:getLocalVar('[BCNM]MenuOptions'))"));
        Check("  Brothers (index 3) on offer with the Zephyr Fan", "True", (menu & 1 << 3) != 0);
        s.PacketList.Clear();
        // GP_CLI_COMMAND_EVENTENDXZY: Brothers asked for (menu index 3, area 1)
        Send(0x05C, 32, b =>
        {
            BitConverter.GetBytes(pillar.X).CopyTo(b, 4);
            BitConverter.GetBytes(pillar.Y).CopyTo(b, 8);
            BitConverter.GetBytes(pillar.Z).CopyTo(b, 12);
            BitConverter.GetBytes(pillar.Id).CopyTo(b, 16);
            BitConverter.GetBytes((uint)(3 << 4 | 1)).CopyTo(b, 20);
            BitConverter.GetBytes(pillar.TargId).CopyTo(b, 24);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
            b[30] = 1;
        });
        var bf = c.PBattlefield;
        int brothersId = int.Parse(Lua("report(xi.battlefield.id.BROTHERS)"));
        Check("  registered: Brothers in area 1", $"True/{brothersId}/1", $"{bf != null}/{bf?.Id}/{bf?.Area}");
        var entryPackets = new List<BasicPacket>(s.PacketList);
        EventEnd(32000);
        entryPackets.AddRange(s.PacketList);
        Check("  entered (the event over)", "True/none", $"{bf != null && bf.IsEntered(c)}/{Cur()}");
        Check("  the Zephyr Fan gone (requiredKeyItems, no keep): ZEPHYR_RIPS with the fan; keyitems", "True/False/00",
              $"{SpecialOf(entryPackets).Contains($"{Text(BearclawPinnacle, "ZEPHYR_RIPS")}:{zephyrFan}")}/{HasKi("ZEPHYR_FAN")}/{DbKeyItem(zephyrFan)}");
        Check("  ENTERING_THE_BATTLEFIELD_FOR Brothers (3), the 30 minutes, the 18 members (over 6: MEMBERS_OF_YOUR_ALLIANCE)", "True/True/True",
              $"{SpecialOf(entryPackets).Contains($"{Text(BearclawPinnacle, "ENTERING_THE_BATTLEFIELD_FOR")}:0:3")}/" +
              $"{SpecialOf(entryPackets).Contains($"{Text(BearclawPinnacle, "TIME_LIMIT_FOR_THIS_BATTLE_IS")}:0:0:0:30")}/" +
              $"{SpecialOf(entryPackets).Contains($"{Text(BearclawPinnacle, "MEMBERS_OF_YOUR_ALLIANCE")}:0:0:0:18")}");
        var taurs = bf.RequiredEnemyList.Concat(bf.AdditionalEnemyList).Select(m => m.PMob).ToList();
        var elder = taurs.First(m => m.Name == "Eldertaur");
        var minder = taurs.First(m => m.Name == "Mindertaur");
        Check("  Eldertaur and Mindertaur up (addEssentialMobs), onMobSpawn's REGAIN 50, Eldertaur's BASE_DAMAGE_MULTIPLIER 150", "2/True/50/50/150",
              $"{taurs.Count}/{elder.IsSpawned && minder.IsSpawned}/{Lua($"report(GetMobByID({elder.Id}):getMod(xi.mod.REGAIN))")}/" +
              $"{Lua($"report(GetMobByID({minder.Id}):getMod(xi.mod.REGAIN))")}/{Lua($"report(GetMobByID({elder.Id}):getMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Brothers fought: Mindertaur down, Eldertaur's Apocalyptic Ray, Eldertaur down, the crate");
        // the taurs cure each other (onMobSpellChoose's Cure V) faster than a 1 HP swing lands: the char's claim, then
        // its blow for the rest of the HP
        KillByDamage(minder);
        Check("Mindertaur killed by the char, the battlefield not won", "False/True", $"{minder.IsAlive}/{bf.Status != 2}");
        // the 6 skills now (Apocalyptic Ray added with Mindertaur dead): the 6th forced
        int apocalyptic = int.Parse(Lua("report(xi.mobSkill.APOCALYPTIC_RAY)"));
        ClaimBySwing(elder);
        IntsFrom("Eldertaur.lua", 6);
        Lua($"GetMobByID({elder.Id}):setTP(3000)");
        int elderSkill = 0;
        for (int i = 0; i < 30 && elderSkill == 0; i++)
        {
            Tick(400);
            elderSkill = elder.SkillState?.Skill.Id ?? 0;
        }
        Check("Eldertaur at 3000 TP: onMobMobskillChoose (Mindertaur dead: 6 skills, the 6th forced): Apocalyptic Ray", $"{apocalyptic}/0/0",
              $"{elderSkill}/{QueuesLeft()}");
        for (int i = 0; i < 20 && elder.SkillState != null; i++) Tick(400);
        Lua("player:setHP(player:getMaxHP())");
        KillByDamage(elder);
        uint crateId = uint.Parse(Lua("report(player:getBattlefield():getArmouryCrate())"));
        var crate = Npc(crateId);
        Check("Eldertaur killed: allDeath: the armoury crate shown (status normal) in the battlefield, not yet won", "False/0/True/True",
              $"{elder.IsAlive}/{crate.Status}/{crateId != 0}/{bf.Status != 2}");
        // utils.selectFromLootGroups: one roll per group (the third twice): 10000 the Cloud Evoker, 10000 the Dragon Bone,
        // 6000 the Scouter's Rope, 1 nothing
        ushort cloudEvoker = ItemId("CLOUD_EVOKER"), dragonBone = ItemId("DRAGON_BONE"), scoutersRope = ItemId("SCOUTERS_ROPE");
        IntsFrom("utils.lua", 10000, 10000, 6000, 1);
        Near(crateId);
        Talk(crateId);
        Check("the crate opened (ON_TRIGGER: npcUtil.openCrate): the rolls: the Cloud Evoker, the Dragon Bone, the Scouter's Rope through the pool",
              "1/1/1/0/0", $"{Qty(Container.Inventory, cloudEvoker)}/{Qty(Container.Inventory, dragonBone)}/{Qty(Container.Inventory, scoutersRope)}/{QueuesLeft()}");
        Check("  char_inventory", "1/1/1", $"{DbQty(cloudEvoker)}/{DbQty(dragonBone)}/{DbQty(scoutersRope)}");
        for (int i = 0; i < 60 && Cur() != "32001"; i++) Tick(400);
        Check("won (status 2), the win's cutscene 32001", "2/32001", $"{bf.Status}/{Cur()}");
        // onBattlefieldWin: startEvent(32001, area, clearTime, partySize, timeInside, zone, index, 0)
        var winEv = c.CurrentEvent.Params;
        Check("  32001's params: area 1, a clear time, 1 member, the time inside, Bearclaw Pinnacle, index 3", $"1/True/1/True/{BearclawPinnacle}/3",
              $"{winEv.GetValueOrDefault(0)}/{winEv.GetValueOrDefault(1) > 0}/{winEv.GetValueOrDefault(2)}/{winEv.GetValueOrDefault(3) > 0}/{winEv.GetValueOrDefault(4)}/{winEv.GetValueOrDefault(5)}");
        long expBefore = long.Parse(DbRow(db, "war", "FROM char_exp WHERE charid = ?"));
        EventEnd(32001);
        Check("  finished (onEventFinishWin): grantXP 3500 (char_exp)", "3500", long.Parse(DbRow(db, "war", "FROM char_exp WHERE charid = ?")) - expBefore);
        for (int i = 0; i < 40 && c.PBattlefield != null; i++) Tick(400);
        Check("  the battlefield over: the char out of it, the clearance gone", "True/False",
              $"{c.PBattlefield == null}/{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}");
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Zebada after the fight (the cooldown running)");
        WarpTo(UleguerandRange, -306.0f, -42.1f, -570.0f);
        TalkTo("Zebada");
        Check("no fan, [ENM]ZephyrFan in the future: event 15 with the cooldown", EvStr(zebada, 15, zephyrReady), Ev());
        EventEnd(15);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- ENM: Twinkbrix (Mine Shaft #2716's gate dial and lever, the warp) ----
        Console.WriteLine("ENM: Twinkbrix in Oldton Movalpolos (no key items: the generic event)");
        ushort sylvanStone = 0;
        Fixture(db, ShaftId, OldtonMovalpolos, -290.8f, 7.0f, -263.2f, Windurst, War, 75);
        CharId = ShaftId;
        Login();
        Check("in Oldton Movalpolos", OldtonMovalpolos, c.Zone);
        sylvanStone = ItemId("SYLVAN_STONE");
        int gateDial = Ki("SHAFT_GATE_OPERATING_DIAL"), lever = Ki("SHAFT_2716_OPERATING_LEVER");
        uint twinkbrix = NpcId("Twinkbrix");
        string Gil() => DbRow(db, "quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0");
        TalkTo("Twinkbrix");
        Check("event 52: the Sylvan Stone, both waits 432000 (none running), 2964, the flags 0", EvStr(twinkbrix, 52, sylvanStone, 432000, 2964, 432000, 0), Ev());
        EventEnd(52);

        Console.WriteLine("ENM: Twinkbrix's dice (2716 gil on a Lightsday: the roll's ceiling 20; 1000 gil another day, won)");
        // the Vana'diel day moved to a Lightsday
        long baseTicks = vanaTicks;
        int lightElement = int.Parse(Lua("report(xi.element.LIGHT)"));
        for (int d = 0; d < 8 && int.Parse(Lua("report(VanadielDayElement())")) != lightElement; d++) vanaTicks += VanaTime.DAY;
        Check("a Lightsday", lightElement.ToString(), Lua("report(VanadielDayElement())"));
        Lua("verifyIntArgs = {}");
        IntsFrom("Twinkbrix.lua", 15);
        Trade(twinkbrix, (0, 2716));
        Check("2716 gil traded on a Lightsday: randomInt(2, 20) (forced 15), tradeComplete (the gil gone), event 55 (2716, 13 to win, 15, the warp 2000)",
              $"2-20/{EvStr(twinkbrix, 55, 2716, 13, 15, 2000)}/{100000 - 2716}",
              $"{Lua("report(table.concat(verifyIntArgs, ','))")}/{Ev()}/{Gil()}");
        EventEnd(55, 0);
        Check("  option 0 (the client's lost roll): no dial, no cooldown", "False/0", $"{HasKi("SHAFT_GATE_OPERATING_DIAL")}/{Var("[ENM]GateDial")}");
        vanaTicks = baseTicks;
        if (int.Parse(Lua("report(VanadielDayElement())")) == lightElement) vanaTicks += VanaTime.DAY;
        Lua("verifyIntArgs = {}");
        IntsFrom("Twinkbrix.lua", 3);
        Trade(twinkbrix, (0, 1000));
        Check("1000 gil another day: randomInt(2, 100) (forced 3), event 55 (1000, 5 to win, 3, 2000)",
              $"2-100/{EvStr(twinkbrix, 55, 1000, 5, 3, 2000)}/{100000 - 2716 - 1000}",
              $"{Lua("report(table.concat(verifyIntArgs, ','))")}/{Ev()}/{Gil()}");
        long vanaShaft = long.Parse(Lua("report(VanadielTime())"));
        EventEnd(55, 1);
        Check("  option 1 (won): the gate dial (keyitems), [ENM]GateDial = VanadielTime + 120 hours", $"True/10/{vanaShaft + enmCooldown * 3600L}",
              $"{HasKi("SHAFT_GATE_OPERATING_DIAL")}/{DbKeyItem(gateDial)}/{DbVar("[ENM]GateDial")}");
        TalkTo("Twinkbrix");
        Check("the dial held, its wait running: event 52 with the dial's wait and the flags (dial waiting 2 + dial held 8)",
              EvStr(twinkbrix, 52, sylvanStone, 432000, 2964, vanaShaft + enmCooldown * 3600L, 10), Ev());
        EventEnd(52);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Twinkbrix: a Sylvan Stone for the operating lever, the warp to Mine Shaft #2716 for 2000 gil");
        Lua($"player:addItem({sylvanStone})");
        Trade(twinkbrix, (Slot(sylvanStone), 1));
        Check("the Sylvan Stone traded: event 51 (the stone)", EvStr(twinkbrix, 51, sylvanStone), Ev());
        long vanaLever = long.Parse(Lua("report(VanadielTime())"));
        EventEnd(51);
        Check("  finished: the lever (keyitems), [ENM]OperatingLever, tradeComplete (the stone gone)", $"10/{vanaLever + enmCooldown * 3600L}/0/0",
              $"{DbKeyItem(lever)}/{DbVar("[ENM]OperatingLever")}/{Qty(Container.Inventory, sylvanStone)}/{DbQty(sylvanStone)}");
        TalkTo("Twinkbrix");
        Check("both key items: event 50", EvStr(twinkbrix, 50), Ev());
        EventEnd(50);
        Trade(twinkbrix, (0, 2000));
        Check("2000 gil with the dial and the lever: event 56 (the stone, 23)", EvStr(twinkbrix, 56, sylvanStone, 23), Ev());
        string gilBefore = Gil();
        EventEnd(56, 1);
        Check("  option 1: the lever spent, the 2000 gil taken (tradeComplete)", $"False/00/True/{long.Parse(gilBefore) - 2000}",
              $"{HasKi("SHAFT_2716_OPERATING_LEVER")}/{DbKeyItem(lever)}/{HasKi("SHAFT_GATE_OPERATING_DIAL")}/{Gil()}");
        Rezone();
        Check("  xi.teleport.to(MINESHAFT): Mine Shaft #2716 at (-94, -120, -584)", $"{MineShaft2716}/-94/-120/-584", $"{c.Zone}/{c.X:F0}/{c.Y:F0}/{c.Z:F0}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Bionic Bug (Mine Shaft #2716): a new lever (a GM's !addkeyitem: the warp spent it), Bugboy's Mighty Strikes");
        Lua($"player:addKeyItem({lever})");
        var shaftDoor = Npc(NpcId("_0d0"));
        MoveTo(shaftDoor.X + 1.5f, shaftDoor.Y, shaftDoor.Z);
        Lua("player:setUnkillable(true)");
        Talk(shaftDoor.Id);
        Check("the Shaft Entrance (_0d0): 32000, Bionic Bug (index 2) on offer", "32000/True",
              $"{Cur()}/{(uint.Parse(Lua("report(player:getLocalVar('[BCNM]MenuOptions'))")) & 1 << 2) != 0}");
        s.PacketList.Clear();
        Send(0x05C, 32, b =>
        {
            BitConverter.GetBytes(shaftDoor.X).CopyTo(b, 4);
            BitConverter.GetBytes(shaftDoor.Y).CopyTo(b, 8);
            BitConverter.GetBytes(shaftDoor.Z).CopyTo(b, 12);
            BitConverter.GetBytes(shaftDoor.Id).CopyTo(b, 16);
            BitConverter.GetBytes((uint)(2 << 4 | 1)).CopyTo(b, 20);
            BitConverter.GetBytes(shaftDoor.TargId).CopyTo(b, 24);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
            b[30] = 1;
        });
        var bug = c.PBattlefield;
        var bugPackets = new List<BasicPacket>(s.PacketList);
        EventEnd(32000);
        bugPackets.AddRange(s.PacketList);
        Check("  entered Bionic Bug, SNAPS_IN_TWO with the lever, the dial kept", $"{Lua("report(xi.battlefield.id.BIONIC_BUG)")}/True/True/False/True",
              $"{bug?.Id}/{bug != null && bug.IsEntered(c)}/{SpecialOf(bugPackets).Contains($"{Text(MineShaft2716, "SNAPS_IN_TWO")}:{lever}")}/" +
              $"{HasKi("SHAFT_2716_OPERATING_LEVER")}/{HasKi("SHAFT_GATE_OPERATING_DIAL")}");
        var bugboy = bug.RequiredEnemyList.Concat(bug.AdditionalEnemyList).Select(m => m.PMob).First(m => m.Name == "Bugboy");
        Check("  Bugboy: BASE_DAMAGE_MULTIPLIER 150", "True/150", $"{bugboy.IsSpawned}/{Lua($"report(GetMobByID({bugboy.Id}):getMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER))")}");
        ClaimBySwing(bugboy);
        for (int i = 0; i < 20 && !bugboy.Engaged; i++) Tick(1000);
        // the follow-up roll (randomInt(1, 100) <= 50, each fight tick after a move) always missed
        IntsFrom("Bugboy.lua", Enumerable.Repeat(100, 400).ToArray());
        // the moves Bugboy readies, in order, over the ticks
        List<ushort> Moves(int ticks)
        {
            var seen = new List<ushort>();
            MobSkillState last = null;
            for (int i = 0; i < ticks; i++)
            {
                Tick(400);
                if (bugboy.SkillState != null && bugboy.SkillState != last) seen.Add(bugboy.SkillState.Skill.Id);
                last = bugboy.SkillState;
                c.Hp = c.MaxHp;
            }
            return seen;
        }
        bugboy.Hp = bugboy.MaxHp * 74 / 100;
        var moves1 = Moves(25);
        string Udmg() => Lua($"report(GetMobByID({bugboy.Id}):getMod(xi.mod.UDMGMAGIC))");
        Check("74%: Mighty Strikes (688), then onMobWeaponSkill's Heavy Blow (357); mightyStrikesCount 1, the effect on, UDMGMAGIC 0",
              "688,357/1/True/0", $"{string.Join(",", moves1)}/{MobLocal(bugboy.Id, "mightyStrikesCount")}/" +
                                  $"{Lua($"report(tostring(GetMobByID({bugboy.Id}):hasStatusEffect(xi.effect.MIGHTY_STRIKES)))") == "true"}/{Udmg()}");
        bugboy.Hp = bugboy.MaxHp * 49 / 100;
        var moves2 = Moves(25);
        Check("49%: Mighty Strikes and Heavy Blow again; count 2 with the effect on: UDMGMAGIC 5000", "688,357/2/5000",
              $"{string.Join(",", moves2)}/{MobLocal(bugboy.Id, "mightyStrikesCount")}/{Udmg()}");
        bugboy.Hp = bugboy.MaxHp * 24 / 100;
        var moves3 = Moves(25);
        Check("24%: the third: count 3: UDMGMAGIC 10000", "688,357/3/10000",
              $"{string.Join(",", moves3)}/{MobLocal(bugboy.Id, "mightyStrikesCount")}/{Udmg()}");
        Lua("verifyFilter = nil verifyInts = {}");
        KillByDamage(bugboy);
        uint bugCrate = uint.Parse(Lua("report(player:getBattlefield() and player:getBattlefield():getArmouryCrate() or 0)"));
        Check("Bugboy killed: the crate shown", "False/0", $"{bugboy.IsAlive}/{(bugCrate == 0 ? -1 : Npc(bugCrate).Status)}");
        Lua("player:leaveBattlefield(xi.battlefield.leaveCode.EXIT)");
        for (int i = 0; i < 40 && c.PBattlefield != null; i++) Tick(400);
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- ENM: Ghebi Damomohe's Astral Covenant, Test Your Mite lost on time ----
        Console.WriteLine("ENM: Ghebi Damomohe in Lower Jeuno (the Pso'Xja pass, a Florid Stone traded with something else)");
        Fixture(db, MiteId, LowerJeuno, 14.0f, 0.0f, -5.0f, Windurst, War, 75);
        CharId = MiteId;
        Login();
        Check("in Lower Jeuno", LowerJeuno, c.Zone);
        uint ghebi = NpcId("Ghebi_Damomohe");
        ushort floridStone = ItemId("FLORID_STONE"), cottonPouchMite = ItemId("COTTON_POUCH");
        int psoxjaPass = Ki("PSOXJA_PASS"), astralCovenant = Ki("ASTRAL_COVENANT");
        TalkTo("Ghebi_Damomohe");
        Check("no Pso'Xja pass: the standard event 106 (4)", EvStr(ghebi, 106, 4), Ev());
        EventEnd(106, 0);
        // option 0: xi.shop.general with Norg's fame: the 0x03C GP_SERV_COMMAND_SHOP_LIST items
        var shop = Of(0x03C);
        string ShopItems() => string.Join(",", shop.SelectMany(p => Enumerable.Range(0, (p.Buffer.Length - 8) / 12).Select(i => BitConverter.ToUInt16(p.Buffer, 8 + i * 12 + 4)))
                                                   .Where(x => x != 0));
        Check("  option 0: his shop (the Rice Ball, the Eel Kabob, the Garlic Cracker, the Chunk of Bronzite)",
              $"{ItemId("RICE_BALL")},{ItemId("EEL_KABOB")},{ItemId("GARLIC_CRACKER")},{ItemId("CHUNK_OF_BRONZITE")}", ShopItems());
        // the pass from Pso'Xja's mission (a GM's !addkeyitem)
        Lua($"player:addKeyItem({psoxjaPass})");
        TalkTo("Ghebi_Damomohe");
        Check("the pass, no covenant, no cooldown: event 106 (4, 1, the Florid Stone, the pass, the covenant)",
              EvStr(ghebi, 106, 4, 1, floridStone, psoxjaPass, astralCovenant), Ev());
        EventEnd(106, 1);
        Lua($"player:addItem({floridStone}) player:addItem({cottonPouchMite})");
        Trade(ghebi, (Slot(floridStone), 1), (Slot(cottonPouchMite), 1));
        // npcUtil.tradeHas (not exactly): the stone confirmed, confirmTrade takes it alone
        Check("the Florid Stone and a Cotton Pouch traded: event 10047 (1782); confirmTrade takes the stone, the pouch handed back",
              $"{EvStr(ghebi, 10047, 1782)}/0/1/0/1", $"{Ev()}/{Qty(Container.Inventory, floridStone)}/{Qty(Container.Inventory, cottonPouchMite)}/" +
                                                     $"{DbQty(floridStone)}/{DbQty(cottonPouchMite)}");
        long vanaMite = long.Parse(Lua("report(VanadielTime())"));
        EventEnd(10047);
        Check("  finished: the Astral Covenant (keyitems), [ENM]AstralCovenant (char_vars)", $"10/{vanaMite + enmCooldown * 3600L}",
              $"{DbKeyItem(astralCovenant)}/{DbVar("[ENM]AstralCovenant")}");
        TalkTo("Ghebi_Damomohe");
        Check("the covenant held: the standard 106 (4)", EvStr(ghebi, 106, 4), Ev());
        EventEnd(106, 1);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Test Your Mite (The Shrouded Maw): the Memento Circle, the level cap 40, Pasuk's TP drain");
        var circle = zone.ZoneInfoOf(TheShroudedMaw).Npcs.First(n => n.Name == "MC_Entrance");
        WarpTo(TheShroudedMaw, circle.X + 1.5f, circle.Y, circle.Z);
        Check("in The Shrouded Maw, level 75", $"{TheShroudedMaw}/75", $"{c.Zone}/{Lua("report(player:getMainLvl())")}");
        Lua("player:setUnkillable(true)");
        Talk(circle.Id);
        Check("the Memento Circle: the battlefield menu 32000, Test Your Mite (index 1) on offer", "32000/True",
              $"{Cur()}/{(uint.Parse(Lua("report(player:getLocalVar('[BCNM]MenuOptions'))")) & 1 << 1) != 0}");
        s.PacketList.Clear();
        Send(0x05C, 32, b =>
        {
            BitConverter.GetBytes(circle.X).CopyTo(b, 4);
            BitConverter.GetBytes(circle.Y).CopyTo(b, 8);
            BitConverter.GetBytes(circle.Z).CopyTo(b, 12);
            BitConverter.GetBytes(circle.Id).CopyTo(b, 16);
            BitConverter.GetBytes((uint)(1 << 4 | 1)).CopyTo(b, 20);
            BitConverter.GetBytes(circle.TargId).CopyTo(b, 24);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
            b[30] = 1;
        });
        var mite = c.PBattlefield;
        var mitePackets = new List<BasicPacket>(s.PacketList);
        EventEnd(32000);
        mitePackets.AddRange(s.PacketList);
        Check("  entered Test Your Mite, the covenant gone (ASTRAL_DISINTEGRATES)", $"{Lua("report(xi.battlefield.id.TEST_YOUR_MITE)")}/True/False/True",
              $"{mite?.Id}/{mite != null && mite.IsEntered(c)}/{HasKi("ASTRAL_COVENANT")}/" +
              $"{SpecialOf(mitePackets).Contains($"{Text(TheShroudedMaw, "ASTRAL_DISINTEGRATES")}:{astralCovenant}")}");
        Check("  the level cap: level 40 (LEVEL_RESTRICTION)", "40/True", $"{Lua("report(player:getMainLvl())")}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))") == "true"}");
        var pasuk = mite.RequiredEnemyList.Concat(mite.AdditionalEnemyList).Select(m => m.PMob).First(m => m.Name == "Pasuk");
        Check("  Pasuk up: ADD_EFFECT (onMobInitialize), base speed 45 (onMobSpawn)", "True/1/45",
              $"{pasuk.IsSpawned}/{Lua($"report(GetMobByID({pasuk.Id}):getMobMod(xi.mobMod.ADD_EFFECT))")}/{Lua($"report(GetMobByID({pasuk.Id}):getBaseSpeed())")}");
        // its hits drain TP: the char at 1000 TP, the power (randomInt(70, 250)) forced to 100, the proc roll to 1
        ClaimBySwing(pasuk);
        Lua("player:setTP(1000)");
        IntsFrom("Pasuk.lua,mobs.lua", 100, 1);
        (int Add, int AddParam, int AddMsg) drain = (0, 0, 0);
        for (int i = 0; i < 60 && drain.AddMsg == 0; i++)
        {
            s.PacketList.Clear();
            Tick(400);
            foreach (var p in Of(0x028).Where(p => U(p, 40, 32) == pasuk.Id && U(p, 82, 4) == 1))
            {
                var ae = FirstAddEffect(p);
                if (ae.AddMsg != 0) drain = ae;
            }
            if (Lua("report(#verifyInts)") != "0" && drain.AddMsg == 0 && i % 10 == 9) IntsFrom("Pasuk.lua,mobs.lua", 100, 1);
        }
        int tpDrainMsg = int.Parse(Lua("report(xi.msg.basic.ADD_EFFECT_TP_DRAIN)")), tpDrainSub = int.Parse(Lua("report(xi.subEffect.TP_DRAIN)"));
        Check("a Pasuk hit: onAdditionalEffect TP_DRAIN: the add effect (sub TP_DRAIN, ADD_EFFECT_TP_DRAIN 165) in its BATTLE2, the power at most 100",
              $"{tpDrainSub}/{tpDrainMsg}/True", $"{drain.Add}/{drain.AddMsg}/{drain.AddParam > 0 && drain.AddParam <= 100}");
        Lua("verifyFilter = nil verifyInts = {}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Test Your Mite runs out (the TIME_LEFT prompts, TIME_IN_THE_BATTLEFIELD_IS_UP, the char out)");
        var timeouts = new List<BasicPacket>();
        for (int i = 0; i < 2400 && c.PBattlefield != null; i++)
        {
            s.PacketList.Clear();
            Tick(mite.RemainingTimeMs > 120_000 ? 5000 : 1000);
            timeouts.AddRange(s.PacketList);
            c.Hp = c.MaxHp;
        }
        Check("the TIME_LEFT prompts (202) at 10 min, 5 min, 1 min, 30 s and 10 s", "600,300,60,30,10",
              string.Join(",", timeouts.Where(p => Opcode(p) == 0x029 && BitConverter.ToUInt16(p.Buffer, 24) == 202).Select(p => BitConverter.ToUInt32(p.Buffer, 12))));
        // onBattlefieldTick reads the status before its time-up setStatus(LOST): that tick's cleanup(true) skips the
        // PARTY_MEMBERS_HAVE_FALLEN branch, and a battlefield that can clean up gets no more ticks (HandleBattlefields)
        Check("  TIME_IN_THE_BATTLEFIELD_IS_UP; no PARTY_MEMBERS_HAVE_FALLEN (the stale status, a script quirk)", "True/False",
              $"{SpecialOf(timeouts).Contains($"{Text(TheShroudedMaw, "TIME_IN_THE_BATTLEFIELD_IS_UP")}:0")}/" +
              $"{SpecialOf(timeouts).Contains($"{Text(TheShroudedMaw, "PARTY_MEMBERS_HAVE_FALLEN")}:0")}");
        Check("  the char out at once (onBattlefieldLeave: no battlefield, no clearance, level 75 again), the covenant not given back",
              "True/False/75/False", $"{c.PBattlefield == null}/{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}/{Lua("report(player:getMainLvl())")}/" +
                                    $"{HasKi("ASTRAL_COVENANT")}");
        // onBattlefieldLeave(LOST): onBattlefieldLoss's 32002
        Check("  the loss cutscene 32002 (onBattlefieldLoss)", "32002", Cur());
        EventEnd(32002);
        bool pasukUp = pasuk.IsSpawned;
        for (int i = 0; i < 30; i++) Tick(400);
        Check("  Pasuk kept through the 10 s grace (Cleanup's m_cleanupTime), then despawned", "True/False", $"{pasukUp}/{pasuk.IsSpawned}");
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- ENM: Venessa's censers, Jakaka's Miasma Filter, Like the Wind (an experimental battlefield) ----
        Console.WriteLine("ENM: Venessa in Ru'Lude Gardens (the CoP gates, a censer, the visions traded)");
        Fixture(db, VenessaId, RuLudeGardens, 8.4f, 0.0f, -86.2f, Windurst, War, 75);
        CharId = VenessaId;
        Login();
        Check("in Ru'Lude Gardens", RuLudeGardens, c.Zone);
        uint venessa = NpcId("Venessa");
        int copLog = int.Parse(Lua("report(xi.mission.log_id.COP)"));
        TalkTo("Venessa");
        // startEvent with a { [0] = 99 } table: the params by index
        Check("CoP at Ancient Flames Beckon (not past The Rites of Life): event 10064 ({ [0] = 99 })", EvStr(venessa, 10064, 99), Ev());
        EventEnd(10064, 0);
        // a GM's !completemission / !addmission
        Lua($"player:completeMission({copLog}, player:getCurrentMission({copLog})) player:addMission({copLog}, xi.mission.id.cop.THE_MOTHERCRYSTALS)");
        TalkTo("Venessa");
        Check("CoP at The Mothercrystals (before Slanderous Utterings): event 10064 ({ [0] = 16, [5] = 6, [6] = 1 })",
              EvStr(venessa, 10064, 16, 0, 0, 0, 0, 6, 1), Ev());
        EventEnd(10064, 0);
        Lua($"player:completeMission({copLog}, player:getCurrentMission({copLog})) player:addMission({copLog}, xi.mission.id.cop.THE_ENDURING_TUMULT_OF_WAR)");
        TalkTo("Venessa");
        Check("CoP at The Enduring Tumult of War (all four Promyvions): event 10064, no params", EvStr(venessa, 10064), Ev());
        EventEnd(10064, 1, 1);
        Check("  update option 1 (Simulant, no timer): updateEvent(1, 0, 0, 1)", "1,0,0,1", Upds());
        long vanaVen = long.Parse(Lua("report(VanadielTime())"));
        int censer = Ki("CENSER_OF_ABANDONMENT");
        EventEnd(10064, 1);
        Check("  finished option 1: the Censer of Abandonment (keyitems), [ENM]abandonmentTimer, VANESSA_ENM_COMPLETE",
              $"10/{vanaVen + enmCooldown * 3600L}/true", $"{DbKeyItem(censer)}/{DbVar("[ENM]abandonmentTimer")}/" +
                                                         $"{Lua("report(tostring(player:hasCompletedUniqueEvent(xi.uniqueEvent.VANESSA_ENM_COMPLETE)))")}");
        TalkTo("Venessa");
        // hasCompletedUniqueEvent returns a boolean: `== 1` never holds, the 10065 of a returning char never shows
        Check("again: still 10064 (onTrigger compares hasCompletedUniqueEvent's boolean to 1: a script bug)", EvStr(venessa, 10064), Ev());
        EventEnd(10064, 1, 1);
        Check("  update option 1, the censer held, the timer running: updateEvent(1)", "1", Upds());
        EventEnd(10064, 1);
        Check("  finished again: no second censer, the timer kept", $"True/{vanaVen + enmCooldown * 3600L}",
              $"{HasKi("CENSER_OF_ABANDONMENT")}/{DbVar("[ENM]abandonmentTimer")}");
        // the censer burned in the Spire (a GM's !delkeyitem): the timer still running
        Lua($"player:delKeyItem({censer})");
        TalkTo("Venessa");
        EventEnd(10064, 1, 1);
        Check("  the censer gone, the timer running: updateEvent(1, 0, 0, 0, the timer, 1)", $"1,0,0,0,{vanaVen + enmCooldown * 3600L},1", Upds());
        EventEnd(10064, 0);
        ushort brilliant = ItemId("BRILLIANT_VISION"), summoningEarring = ItemId("SUMMONING_EARRING");
        Lua($"player:addItem({brilliant}, 2)");
        Trade(venessa, (Slot(brilliant), 2));
        Check("two Brilliant Visions traded: getItemCount ~= 1: nothing", "none/none", $"{Ev()}/{Cur()}");
        Trade(venessa, (Slot(brilliant), 1));
        Check("one: event 10066 (the Summoning Earring)", EvStr(venessa, 10066, summoningEarring), Ev());
        EventEnd(10066);
        Check("  finished: the earring given, the vision taken (tradeComplete), veneReward cleared", "1/1/1/0",
              $"{Qty(Container.Inventory, summoningEarring)}/{Qty(Container.Inventory, brilliant)}/{DbQty(summoningEarring)}/{Lua("report(player:getLocalVar('veneReward'))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Jakaka in Attohwa Chasm (the Flaxen Pouch, the Parradamo Stones, the Miasma Filter)");
        var jakaka = zone.ZoneInfoOf(AttohwaChasm).Npcs.First(n => n.Name == "Jakaka");
        WarpTo(AttohwaChasm, jakaka.X + 1.5f, jakaka.Y, jakaka.Z);
        ushort flaxenPouch = ItemId("FLAXEN_POUCH"), parradamo = ItemId("POUCH_OF_PARRADAMO_STONES");
        int miasmaFilter = Ki("MIASMA_FILTER");
        Talk(jakaka.Id);
        Check("event 13", EvStr(jakaka.Id, 13), Ev());
        EventEnd(13);
        Check("  the Flaxen Pouch", "1", Qty(Container.Inventory, flaxenPouch));
        Lua($"player:addItem({parradamo})");
        Trade(jakaka.Id, (Slot(parradamo), 1));
        Check("the Pouch of Parradamo Stones traded: event 12", EvStr(jakaka.Id, 12), Ev());
        long vanaJak = long.Parse(Lua("report(VanadielTime())"));
        EventEnd(12);
        Check("  the Miasma Filter, [ENM]MiasmaFilter", $"10/{vanaJak + enmCooldown * 3600L}", $"{DbKeyItem(miasmaFilter)}/{DbVar("[ENM]MiasmaFilter")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Like the Wind (Boneyard Gully, experimental): the doubled level, the shout, Race Runner's patrol");
        var miasma = zone.ZoneInfoOf(BoneyardGully).Npcs.First(n => n.Name == "_081");
        WarpTo(BoneyardGully, miasma.X + 1.5f, miasma.Y, miasma.Z);
        Lua("player:setUnkillable(true)");
        Talk(miasma.Id);
        Check("the Dark Miasma (_081): 32000, Like the Wind (index 1) on offer (BCNM_ENABLE_EXPERIMENTAL)", "32000/True",
              $"{Cur()}/{(uint.Parse(Lua("report(player:getLocalVar('[BCNM]MenuOptions'))")) & 1 << 1) != 0}");
        s.PacketList.Clear();
        Send(0x05C, 32, b =>
        {
            BitConverter.GetBytes(miasma.X).CopyTo(b, 4);
            BitConverter.GetBytes(miasma.Y).CopyTo(b, 8);
            BitConverter.GetBytes(miasma.Z).CopyTo(b, 12);
            BitConverter.GetBytes(miasma.Id).CopyTo(b, 16);
            BitConverter.GetBytes((uint)(1 << 4 | 1)).CopyTo(b, 20);
            BitConverter.GetBytes(miasma.TargId).CopyTo(b, 24);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
            b[30] = 1;
        });
        var wind = c.PBattlefield;
        var windPackets = new List<BasicPacket>(s.PacketList);
        EventEnd(32000);
        windPackets.AddRange(s.PacketList);
        var runner = wind.RequiredEnemyList.Concat(wind.AdditionalEnemyList).Select(m => m.PMob).First(m => m.Name == "Race_Runner");
        Check("  entered Like the Wind, the filter used up (no message: requiredKeyItems without one)", $"{Lua("report(xi.battlefield.id.LIKE_THE_WIND)")}/True/False",
              $"{wind?.Id}/{wind != null && wind.IsEntered(c)}/{HasKi("MIASMA_FILTER")}");
        // 0x017 GP_SERV_COMMAND_CHAT_STD: kind / message
        string ChatOf(IEnumerable<BasicPacket> from, int kind) => string.Join("|", from.Where(p => Opcode(p) == 0x017 && p.Buffer[4] == kind)
            .Select(p => System.Text.Encoding.ASCII.GetString(p.Buffer, 23, p.Buffer.Length - 23).Split('\0')[0]));
        Check("  the experimental shout (printToPlayer, NS_SHOUT 14)", "This battlefield has been marked as experimental.  Enemy levels have increased!",
              ChatOf(windPackets, 14));
        Check("  onBattlefieldInitialize: Race Runner's level doubled (75 -> 150, setMobLevel)", "150", Lua($"report(GetMobByID({runner.Id}):getMainLvl())"));
        float rx = runner.X, rz = runner.Z;
        for (int i = 0; i < 25; i++) Tick(400);
        Check("  its patrol (onMobSpawn's pathThrough PATROL | REVERSE): following the path, moved off its spawn",
              "true/True", $"{Lua($"report(tostring(GetMobByID({runner.Id}):isFollowingPath()))")}/{MathF.Abs(runner.X - rx) + MathF.Abs(runner.Z - rz) > 2.0f}");
        // the char leaves through the exit (leaveBattlefield EXIT): no win, the char out
        Lua("player:leaveBattlefield(xi.battlefield.leaveCode.EXIT)");
        for (int i = 0; i < 40 && runner.IsSpawned; i++) Tick(400);
        Check("left (leaveBattlefield EXIT): out, nobody inside: cleaned up, Race Runner gone", "True/False/False",
              $"{c.PBattlefield == null}/{c.StatusEffects.HasStatusEffect(Battlefield.EffectBattlefield)}/{runner.IsSpawned}");
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- ENM: Morangeart's Monarch Beard, Fire in the Sky (Razon's phases and self-destructs) ----
        Console.WriteLine("ENM: Morangeart in the Tavnazian Safehold (the CoP gate, the Monarch Beard)");
        Fixture(db, RazonId, TavnazianSafehold, -72.8f, -23.8f, -28.5f, Windurst, War, 75);
        CharId = RazonId;
        Login();
        Check("in the Tavnazian Safehold", TavnazianSafehold, c.Zone);
        uint morangeart = NpcId("Morangeart");
        int monarchBeard = Ki("MONARCH_BEARD");
        TalkTo("Morangeart");
        Check("CoP not past An Eternal Melody: event 523", EvStr(morangeart, 523), Ev());
        EventEnd(523);
        Lua($"player:completeMission({copLog}, player:getCurrentMission({copLog})) player:addMission({copLog}, xi.mission.id.cop.THE_ENDURING_TUMULT_OF_WAR)");
        TalkTo("Morangeart");
        Check("past it, no beard, no cooldown: event 521", EvStr(morangeart, 521), Ev());
        long vanaBeard = long.Parse(Lua("report(VanadielTime())"));
        EventEnd(521);
        Check("  finished: the Monarch Beard (keyitems), [ENM]MonarchBeard", $"10/{vanaBeard + enmCooldown * 3600L}",
              $"{DbKeyItem(monarchBeard)}/{DbVar("[ENM]MonarchBeard")}");
        TalkTo("Morangeart");
        Check("the beard held: event 520", EvStr(morangeart, 520), Ev());
        EventEnd(520);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Fire in the Sky (Monarch Linn): the beard torn away, Razon set up");
        var displacement = zone.ZoneInfoOf(MonarchLinn).Npcs.First(n => n.Name == "SD_Entrance");
        WarpTo(MonarchLinn, displacement.X + 1.5f, displacement.Y, displacement.Z);
        Lua("player:setUnkillable(true)");
        Talk(displacement.Id);
        Check("the Spatial Displacement: 32000, Fire in the Sky (index 2) on offer", "32000/True",
              $"{Cur()}/{(uint.Parse(Lua("report(player:getLocalVar('[BCNM]MenuOptions'))")) & 1 << 2) != 0}");
        s.PacketList.Clear();
        Send(0x05C, 32, b =>
        {
            BitConverter.GetBytes(displacement.X).CopyTo(b, 4);
            BitConverter.GetBytes(displacement.Y).CopyTo(b, 8);
            BitConverter.GetBytes(displacement.Z).CopyTo(b, 12);
            BitConverter.GetBytes(displacement.Id).CopyTo(b, 16);
            BitConverter.GetBytes((uint)(2 << 4 | 1)).CopyTo(b, 20);
            BitConverter.GetBytes(displacement.TargId).CopyTo(b, 24);
            BitConverter.GetBytes((ushort)32000).CopyTo(b, 26);
            b[30] = 1;
        });
        var sky = c.PBattlefield;
        var skyPackets = new List<BasicPacket>(s.PacketList);
        EventEnd(32000);
        skyPackets.AddRange(s.PacketList);
        Check("  entered Fire in the Sky (level 40), TORN_FROM_YOUR_HANDS with the beard", $"{Lua("report(xi.battlefield.id.FIRE_IN_THE_SKY)")}/True/40/False/True",
              $"{sky?.Id}/{sky != null && sky.IsEntered(c)}/{Lua("report(player:getMainLvl())")}/{HasKi("MONARCH_BEARD")}/" +
              $"{SpecialOf(skyPackets).Contains($"{Text(MonarchLinn, "TORN_FROM_YOUR_HANDS")}:{monarchBeard}")}");
        var razon = sky.RequiredEnemyList.Concat(sky.AdditionalEnemyList).Select(m => m.PMob).First(m => m.Name == "Razon");
        string RazonVar(string name) => MobLocal(razon.Id, name);
        string SkyPhase() => Lua("report(player:getBattlefield():getLocalVar('phase'))");
        int fire = int.Parse(Lua("report(xi.damageType.FIRE)")), dark = int.Parse(Lua("report(xi.damageType.DARK)"));
        int trigger1 = int.Parse(RazonVar("triggerElement1")), trigger2 = int.Parse(RazonVar("triggerElement2"));
        Check("  Razon: UDMGMAGIC 10000, UDMGPHYS -9500, two distinct trigger elements (utils.permgen), phase 1",
              "True/10000/-9500/True/1",
              $"{razon.IsSpawned}/{Lua($"report(GetMobByID({razon.Id}):getMod(xi.mod.UDMGMAGIC))")}/{Lua($"report(GetMobByID({razon.Id}):getMod(xi.mod.UDMGPHYS))")}/" +
              $"{trigger1 != trigger2 && trigger1 >= fire && trigger1 <= dark && trigger2 >= fire && trigger2 <= dark}/{SkyPhase()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ENM: Fire in the Sky: phase 1 by HP (66%), phase 2 by an element, phase 3's self-destruct knocks the char out");
        ClaimBySwing(razon);
        for (int i = 0; i < 20 && !razon.Engaged; i++) Tick(1000);
        Check("Razon engaged on the char", "True", razon.Engaged && razon.BattleTarget == c);
        ushort SkillOf(MobEntity m, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                if (m.SkillState != null) return m.SkillState.Skill.Id;
                Tick(400);
            }
            return 0;
        }
        void SkillDone(MobEntity m)
        {
            for (int i = 0; i < 30 && m.SkillState != null; i++) Tick(400);
            Lua("player:setHP(player:getMaxHP())");
        }
        Lua($"GetMobByID({razon.Id}):setHP(GetMobByID({razon.Id}):getMaxHP() * 60 / 100)");
        ushort first = SkillOf(razon, 10);
        Check("HP 60% (under 66): onMobFight's useMobAbility(SELF_DESTRUCT_CLUSTER_3), phase 2", "571/2", $"{first}/{SkyPhase()}");
        SkillDone(razon);
        Check("  the move done: setFinalAnimationSub(5)", "5", razon.AnimationSub);
        // a hit of the first trigger element (the TAKE_DAMAGE listener): the colored dust (injectActionPacket category 11)
        s.PacketList.Clear();
        Lua($"GetMobByID({razon.Id}):takeDamage(1, player, xi.attackType.MAGICAL, {trigger1})");
        Check("  a hit of trigger element 1 (TAKE_DAMAGE): elementTriggered", $"{trigger1}", RazonVar("elementTriggered"));
        var dust = new List<BasicPacket>();
        ushort second = 0;
        for (int i = 0; i < 10 && second == 0; i++)
        {
            Tick(400);
            dust.AddRange(Of(0x028));
            s.PacketList.Clear();
            if (razon.SkillState != null) second = razon.SkillState.Skill.Id;
        }
        int[] dustAnims = { 432, 433, 434, 435, 436, 437, 438, 439 };
        Check("  at 60% HP (over 33): the element's phase change: the dust (0x028 category 11, the element's animation), SELF_DESTRUCT_CLUSTER_2, phase 3, cleared",
              $"True/573/3/0", $"{dust.Any(p => U(p, 40, 32) == razon.Id && U(p, 82, 4) == 11 && U(p, 150, 32) == razon.Id && U(p, 186 + 5, 12) == (ulong)dustAnims[trigger1 - fire])}/{second}/{SkyPhase()}/{RazonVar("elementTriggered")}");
        // injectActionPacket(id, 11, anim, info 0, reaction 0x18, message 0, actionid 0, param 0): the reaction in the
        // 3-bit resolution field keeps 0x18 & 7 (packBitsBE's mask)
        var dustPacket = dust.First(p => U(p, 40, 32) == razon.Id && U(p, 82, 4) == 11);
        Check("  the dust's BATTLE2: one target (Razon), action id 0, resolution 0 (0x18 masked to 3 bits), kind 3 (normalize: MobSkillFinish), info 0, param 0, message 0",
              "1/0/0/3/0/0/0", $"{U(dustPacket, 72, 6)}/{U(dustPacket, 86, 32)}/{U(dustPacket, 186, 3)}/{U(dustPacket, 189, 2)}/{U(dustPacket, 203, 5)}/" +
                              $"{U(dustPacket, 213, 17)}/{U(dustPacket, 230, 10)}");
        SkillDone(razon);
        Lua($"GetMobByID({razon.Id}):takeDamage(1, player, xi.attackType.MAGICAL, {(trigger1 == fire ? fire + 1 : fire)})");
        Check("a hit of another element (not a trigger): nothing", "0", (trigger1 == fire ? fire + 1 : fire) == trigger2 ? "0" : RazonVar("elementTriggered"));
        var knocked = new List<BasicPacket>();
        s.PacketList.Clear();
        Lua($"GetMobByID({razon.Id}):takeDamage(1, player, xi.attackType.MAGICAL, {trigger2})");
        ushort third = 0;
        for (int i = 0; i < 20 && c.PBattlefield != null; i++)
        {
            Tick(400);
            if (razon.SkillState != null) third = razon.SkillState.Skill.Id;
            knocked.AddRange(s.PacketList);
            s.PacketList.Clear();
        }
        Check("trigger element 2: SELF_DESTRUCT_CLUSTER_RAZON (phase 4); onMobSkillFinalize's setHP(0): Razon dead", "1470/4/False",
              $"{third}/{Lua($"report(GetMobByID({razon.Id}):getBattlefield() and GetMobByID({razon.Id}):getBattlefield():getLocalVar('phase') or 'gone')")}/{razon.IsAlive}");
        // CMobSkillState::Cleanup fires WEAPONSKILL_STATE_EXIT only while the mob isAlive: onMobSkillFinalize's setHP(0)
        // came first, so Razon's FINAL_SELF_DESTRUCT listener never runs (a script bug, in C++ too): no knock-out, the
        // battlefield stays open with the char in it, and allDeath at phase 4 shows no crate
        Check("  no WEAPONSKILL_STATE_EXIT (dead by then): no KNOCKED_OUT_OF_BATTLEFIELD, the char still inside at 40, no crate (phase 4)",
              "False/True/40/False",
              $"{SpecialOf(knocked).Contains($"{Text(MonarchLinn, "KNOCKED_OUT_OF_BATTLEFIELD")}:0")}/{c.PBattlefield == sky}/{Lua("report(player:getMainLvl())")}/" +
              $"{zone.ZoneInfoOf(MonarchLinn).Npcs.Any(n => n.Name == "Armoury_Crate" && n.Status == NpcEntity.StatusNormal)}");
        Lua("player:leaveBattlefield(xi.battlefield.leaveCode.EXIT)");
        for (int i = 0; i < 40 && zone.ZoneInfoOf(MonarchLinn).BattlefieldHandler.Battlefields.Count > 0; i++) Tick(400);
        Check("  left through the exit: the char out at 75, the empty battlefield cleaned up", "True/75/0",
              $"{c.PBattlefield == null}/{Lua("report(player:getMainLvl())")}/{zone.ZoneInfoOf(MonarchLinn).BattlefieldHandler.Battlefields.Count}");
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        if (Environment.GetEnvironmentVariable("CONTENT6_DUMP_LOG") == "1") foreach (var l in log.Distinct()) Console.WriteLine("  log: " + l);
    }
}
