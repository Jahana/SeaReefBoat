using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Content5.Live -- live gate for the content systems driven as a client drives them through the real Lua
// scripts: the Unity Concord (scripts/globals/unity.lua and Urbiolaine: the ten records and All for One, joining a
// unity, the accolades it pays, the exchange, the warp, the change of unity, the unity menus and the unity chat toggle).
// The 0x00A login, 0x015 moves, 0x01A Talk, 0x05B event ends and updates, 0x10C RoE starts, 0x116 / 0x117 / 0x118
// the unity menu packets, the zone's ticks, and every step's effect: the event and message packets, char vars, key
// items, items, currencies, the unity rows. The real scripts on the real MariaDB (xidb), chars 996091-996099. The
// walks between the places the scripts do not script are GM !pos warps.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static uint CharId = 996091;
    private const uint UnityId = 996091, TreasureId = 996092, IxionId = 996093, RiderId = 996094, ApparatusId = 996095, BrigandId = 996096, PirateId = 996097, ContestId = 996098, EfId = 996099;
    private static readonly uint[] CharIds = { 996091, 996092, 996093, 996094, 996095, 996096, 996097, 996098, 996099 };
    private const ushort SouthernSandoria = 230, LaTheine = 102, YuhtungaJungle = 123, JugnerS = 82, Wajaom = 51, DangrufWadi = 191, Buburimu = 118, Valkurm = 103, Selbina = 248, BastokMines = 234, PortBastok = 236;
    private const byte Sandoria = 0, Bastok = 1, Windurst = 2;
    private const byte War = 1, Mnk = 2, Thf = 6;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbScalar(IDatabase db, string q)
    {
        var r = Sql(db, q, BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>(0u) ?? "null" : "-";
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

    // the server variables the scripts write: kept as they were before the run, cleared for it, restored after
    private static readonly string[] ServerVars =
    {
        "DarkIxion_ZoneID", "DarkIxion_PopTime", "DarkIxion_HP", "DarkIxion_HornState", "DarkRider_ZoneID", "DarkRider_PopTime",
    };
    private static readonly Dictionary<string, (int Value, int Expiry)?> _serverVarsBefore = new Dictionary<string, (int, int)?>();
    // unity_system (the accolades a member earns add to its leader's points_current): kept as it was, restored after
    private static readonly List<(int Leader, int MembersCurrent, double PointsCurrent, int MembersPrev, double PointsPrev)> _unityBefore =
        new List<(int, int, double, int, double)>();

    // the fishing contest's row and its entries (a GM's new contest wipes them): kept as they were, restored after
    private static string _contestBefore;
    private static readonly List<string> _contestEntriesBefore = new List<string>();

    private static void SaveServerVars(IDatabase db)
    {
        var fc = Sql(db, "SELECT CONCAT_WS(',', status, criteria, measure, fishid, starttime, nextstagetime) FROM fishing_contest LIMIT 1");
        _contestBefore = fc != null && fc.Next() ? fc.Get<string>(0u) : null;
        var fe = Sql(db, "SELECT CONCAT_WS(',', charid, mjob, sjob, mlevel, slevel, race, allegiance, fishrank, score, submittime, contestrank, share, claimed) FROM fishing_contest_entries");
        while (fe != null && fe.Next()) _contestEntriesBefore.Add(fe.Get<string>(0u));
        foreach (var name in ServerVars)
        {
            var r = Sql(db, "SELECT value, expiry FROM server_variables WHERE name = ?", BoundValue.Of(name));
            _serverVarsBefore[name] = r != null && r.Next() ? (r.Get<int>("value"), r.Get<int>("expiry")) : null;
            Sql(db, "DELETE FROM server_variables WHERE name = ?", BoundValue.Of(name));
        }
        var u = Sql(db, "SELECT leader, members_current, points_current, members_prev, points_prev FROM unity_system");
        while (u != null && u.Next())
            _unityBefore.Add((u.Get<int>("leader"), u.Get<int>("members_current"), u.Get<double>("points_current"), u.Get<int>("members_prev"), u.Get<double>("points_prev")));
    }

    private static void RestoreServerVars(IDatabase db)
    {
        if (_contestBefore != null)
        {
            Sql(db, "DELETE FROM fishing_contest");
            Sql(db, $"INSERT INTO fishing_contest VALUES ({_contestBefore})");
            foreach (var e in _contestEntriesBefore) Sql(db, $"INSERT IGNORE INTO fishing_contest_entries VALUES ({e})");
        }
        foreach (var (name, before) in _serverVarsBefore)
        {
            Sql(db, "DELETE FROM server_variables WHERE name = ?", BoundValue.Of(name));
            if (before is { } v)
                Sql(db, "INSERT INTO server_variables(name, value, expiry) VALUES (?, ?, ?)", BoundValue.Of(name), BoundValue.Of(v.Value), BoundValue.Of(v.Expiry));
        }
        foreach (var u in _unityBefore)
            Sql(db, "UPDATE unity_system SET members_current = ?, points_current = ?, members_prev = ?, points_prev = ? WHERE leader = ?",
                BoundValue.Of(u.MembersCurrent), BoundValue.Of(u.PointsCurrent), BoundValue.Of(u.MembersPrev), BoundValue.Of(u.PointsPrev), BoundValue.Of(u.Leader));
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
            SaveServerVars(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            RestoreServerVars(db);
            Cleanup(db);
        }
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    private static readonly string[] JobColumns = { "", "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng" };

    // a Hume of the nation and the job at level `lvl` at x / y / z in the zone, with 10000 gil and the items from
    // inventory slot 1 on
    private static void Fixture(IDatabase db, uint id, ushort zoneId, float x, float y, float z, byte nation, byte job, byte lvl,
                                params (ushort Item, uint Qty)[] items)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of($"content5live{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Cfivelive{id % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of(nation), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, ?, ?)", BoundValue.Of(id), BoundValue.Of(job), BoundValue.Of(lvl));
        Sql(db, $"REPLACE INTO char_jobs(charid, {JobColumns[job]}) VALUES (?, ?)", BoundValue.Of(id), BoundValue.Of(lvl));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,10000)", BoundValue.Of(id));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(id), BoundValue.Of((byte)(i + 1)), BoundValue.Of(items[i].Item), BoundValue.Of(items[i].Qty));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Fixture(db, UnityId, SouthernSandoria, -86.5f, 1.0f, -50.75f, Sandoria, War, 75);

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
        // the wall clock the scripts' VanadielTime, NextGameTime and Jst* read on the harness's time too, as GetSystemTime is
        // (getVanaMidnight adds the two: on different clocks a real second passing between two calls shifts it)
        scripts.UnixNowMs = () => earth0 * 1000 + (now - 1_000_000);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)EarthNow()); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_content5");
            return string.Join(",", got);
        }
        var expectedWarnings = new HashSet<string>();
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                      !expectedWarnings.Contains(l)).Distinct().ToList();
        void ClearErrors() { foreach (var e in ScriptErrors()) expectedWarnings.Add(e); }

        bool keepUnkillable = false; // a GM keeps the char up through a section's fights
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
        MobEntity Mob(uint id) => zone.ZoneInfoOf(c.Zone).MobById[id];
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
        // a zone change the scripts asked for (setPos to another zone): the tick that zones the char, the next login
        void Rezone(bool endEvents = true)
        {
            Tick(400);
            Login(endEvents);
        }
        void WarpTo(ushort zoneId, float x, float y, float z)
        {
            Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
            Rezone();
            MoveTo(x, y, z);
            s.PacketList.Clear();
        }
        uint NpcId(string name) => zone.ZoneInfoOf(c.Zone).Npcs.First(n => n.Name == name).Id;

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
        // the 0x029 battle messages as message:param:value
        string Basic() => string.Join(",", Of(0x029).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}"));
        int Text(ushort zoneId, string name) => int.Parse(Lua($"report(zones[{zoneId}].text.{name})"));
        string Var(string name) => Lua($"report(player:getCharVar('{name}'))");
        bool HasKi(string name) => Lua($"report(tostring(player:hasKeyItem(xi.keyItem.{name})))") == "true";
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        string Currency(string name) => Lua($"report(player:getCurrency('{name}'))");
        Item InvItem(ushort itemId) => c.Items.FirstOrDefault(i => i.Location == Container.Inventory && i.ItemId == itemId && i.Quantity > 0);
        int InvCount(ushort itemId) => c.Items.Count(i => i.Location == Container.Inventory && i.ItemId == itemId && i.Quantity > 0);
        string MobLocal(uint id, string name) => Lua($"report(GetMobByID({id}):getLocalVar('{name}'))");
        string NpcLocal(uint id, string name) => Lua($"report(GetNPCByID({id}):getLocalVar('{name}'))");
        // the char's swing claims the mob (the char kept facing it as the client does), then the auto-attacks
        void ClaimBySwing(MobEntity m)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(m.X - 2.0f).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(m.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = 0;
            });
            void Attack() => Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
            });
            Attack();
            for (int i = 0; m.OwnerId != c.Id && i < 30; i++)
            {
                if (c.AttackTarget != m) Attack();
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
        // the mob brought to 1 HP and killed by the char's swings; the packets of the kill
        List<BasicPacket> Kill(MobEntity m)
        {
            Lua("player:setUnkillable(true)");
            s.PacketList.Clear();
            var packets = new List<BasicPacket>();
            ClaimBySwing(m);
            m.Hp = 1;
            // a bare-handed swing every 8 s: up to 80 s for one to land
            for (int i = 0; i < 200 && m.IsAlive; i++)
            {
                // healed meanwhile (a cure, a regen, a two-hour): the GM's 1 HP again
                if (m.Hp > 1 && i % 25 == 24) m.Hp = 1;
                // the client keeps beside the mob and facing it as it moves
                float kx = m.X - c.X, kz = m.Z - c.Z;
                if (MathF.Sqrt(kx * kx + kz * kz) > 3.0f || i % 4 == 0)
                {
                    float px = MathF.Sqrt(kx * kx + kz * kz) > 3.0f ? m.X - 2.0f : c.X, pz = MathF.Sqrt(kx * kx + kz * kz) > 3.0f ? m.Z : c.Z;
                    byte kdir = (byte)(short)(MathF.Atan2(m.Z - pz, m.X - px) * -(128.0f / MathF.PI));
                    Send(0x015, 32, b =>
                    {
                        BitConverter.GetBytes(px).CopyTo(b, C2sPos.OffX);
                        BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
                        BitConverter.GetBytes(pz).CopyTo(b, C2sPos.OffY);
                        b[C2sPos.OffDir] = kdir;
                    });
                }
                // the engage refused while the last disengage's wait runs: the client asks again
                if (c.AttackTarget != m)
                    Send(0x01A, 28, b =>
                    {
                        BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                        BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                        BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
                    });
                Tick(400);
                packets.AddRange(s.PacketList);
                s.PacketList.Clear();
            }
            // the death's handlers (onMobDeath, the DEFEATED_MOB listeners) on the ticks after
            for (int i = 0; i < 8; i++)
            {
                Tick(400);
                packets.AddRange(s.PacketList);
                s.PacketList.Clear();
            }
            Lua($"player:setUnkillable({(keepUnkillable ? "true" : "false")}) player:setHP(player:getMaxHP())");
            return packets;
        }
        string BasicOf(IEnumerable<BasicPacket> from, params int[] msgs) => string.Join(",", from.Where(p => Opcode(p) == 0x029 && msgs.Contains(BitConverter.ToUInt16(p.Buffer, 24)))
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}"));
        // GP_CLI_COMMAND_EQUIP_SET: the inventory slot into the equipment slot
        void Equip(byte invSlot, byte equipSlot) => Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
        // a walk into a trigger area: the zone-in's ZoningIn local var gone (xi.player.onGameIn's 2.5 s timer), the char
        // moved to its middle, the zone's trigger-area tick
        void Enter(float x, float y, float z)
        {
            s.PacketList.Clear();
            MoveTo(x, y, z);
            Tick(400);
        }

        // ---- Unity Concord ----
        CharId = UnityId;
        Console.WriteLine("Unity: Urbiolaine before the ten records and All for One");
        Login();
        uint urbio = NpcId("Urbiolaine");
        Near(urbio);
        Talk(urbio);
        Check("Urbiolaine, no record completed: event 3528 (ten records needed)", $"{EvStr(urbio, 3528)}/3528", $"{Ev()}/{Cur()}");
        EventEnd(3528, 0);
        // a GM's ten records completed (the char's eminence bits)
        foreach (var rec in new[] { 1, 2, 3, 4, 6, 7, 8, 9, 10, 11 }) Lua($"player:setEminenceCompleted({rec})");
        Check("  ten records completed (getNumEminenceCompleted)", "10", Lua("report(player:getNumEminenceCompleted())"));
        Talk(urbio);
        Check("ten records, All for One neither taken nor done: event 3525", $"{EvStr(urbio, 3525)}/3525", $"{Ev()}/{Cur()}");
        EventEnd(3525, 0);
        // GP_CLI_COMMAND_ROE_START: All for One (record 5) taken
        s.PacketList.Clear();
        Send(0x10C, 8, b => BitConverter.GetBytes((ushort)5).CopyTo(b, 4));
        Check("0x10C ROE_START 5: All for One taken (hasEminenceRecord)", "true", Lua("report(tostring(player:hasEminenceRecord(5)))"));
        Talk(urbio);
        Check("All for One taken: event 3526 (the first choice of a unity)", $"{EvStr(urbio, 3526)}/3526", $"{Ev()}/{Cur()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Unity: Invincible Shield's unity joined (event 3526, option 3): All for One completes");
        int concordoll = Ki("CONCORDOLL");
        int roeComplete = int.Parse(Lua("report(xi.msg.basic.ROE_COMPLETE)")), roeFirstSparks = int.Parse(Lua("report(xi.msg.basic.ROE_FIRST_TIME_SPARKS)")),
            roeAccolades = int.Parse(Lua("report(xi.msg.basic.ROE_RECEIVED_ACCOLADES)"));
        EventEnd(3526, 3);
        var joinPackets = new List<BasicPacket>(s.PacketList);
        Check("changeUnityLeader: unity leader 3, unity_changed 1, saved to char_profile", "3/1/3",
              $"{Lua("report(player:getUnityLeader())")}/{Var("unity_changed")}/{DbScalar(db, "SELECT unity_leader FROM char_profile WHERE charid = ?")}");
        Check("  KEYITEM_OBTAINED CONCORDOLL (the record's reward), then YOU_HAVE_JOINED_UNITY (Invincible Shield's: 2)",
              $"{Text(SouthernSandoria, "KEYITEM_OBTAINED")}:{concordoll},{Text(SouthernSandoria, "YOU_HAVE_JOINED_UNITY")}:2", Special());
        Check("xi.roe.onRecordTrigger(5): ROE_COMPLETE 5, ROE_FIRST_TIME_SPARKS 300, ROE_RECEIVED_ACCOLADES 1000 (no rank bonus for All for One)",
              $"{roeComplete}:5:0,{roeFirstSparks}:300:300,{roeAccolades}:1000:1000", BasicOf(joinPackets, roeComplete, roeFirstSparks, roeAccolades));
        Check("  All for One done, no longer in the log; sparks 300", "true/false/300",
              $"{Lua("report(tostring(player:getEminenceCompleted(5)))")}/{Lua("report(tostring(player:hasEminenceRecord(5)))")}/{Currency("spark_of_eminence")}");
        Check("  accolades 1000, the week's (current_accolades) 1000, CONCORDOLL", "1000/1000/True",
              $"{Currency("unity_accolades")}/{Currency("current_accolades")}/{HasKi("CONCORDOLL")}");
        Check("  the 0x061 CLISTATUS after the accolades: leader 3, 1000 accolades", "3/1000",
              joinPackets.Where(p => Opcode(p) == 0x061).Select(p => { uint w = BitConverter.ToUInt32(p.Buffer, 4 + 84); return $"{w & 0x1F}/{w >> 10 & 0x1FFFF}"; }).LastOrDefault() ?? "none");
        Check("  the hidden record 4085 (ten records) set", "true", Lua("report(tostring(player:getEminenceCompleted(4085)))"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Unity: Urbiolaine as a member (event 3529): the exchange");
        int remaining = int.Parse(Lua("report(xi.settings.main.WEEKLY_EXCHANGE_LIMIT)"));
        Talk(urbio);
        Check("event 3529: 0, the leader 3, the accolades 1000, the week's remaining limit", $"{EvStr(urbio, 3529, 0, 3, 1000, remaining)}/3529", $"{Ev()}/{Cur()}");
        EventEnd(3529, 10, 1);
        Check("  update 10: the remaining limit", $"0,0,0,{remaining}", Upds());
        ushort instantWarp = ushort.Parse(Lua("report(xi.item.SCROLL_OF_INSTANT_WARP)"));
        // category 3 (an item picked: its quantity rules), selection 2 (Instant Warp)
        EventEnd(3529, 3 | 2 << 5, 1);
        Check("  update category 3, Instant Warp: askQuantity 1, limit 1, the leader in the 8th", "1,1,0,0,0,0,0,3", Upds());
        EventEnd(3529, 3 | 41 << 5, 1);
        Check("  update category 3, no such item (41): MAX_UINT32", "4294967295", Upds());
        // category 4 (the item bought), Instant Warp x1 for 10 accolades
        EventEnd(3529, 4 | 2 << 5 | 1 << 13, 1);
        Check("  update category 4, Instant Warp x1: the item, 1, accolades 990, the limit less 10, the leader", $"{instantWarp},1,990,{remaining - 10},0,0,0,3", Upds());
        Check("  the scroll given, 10 accolades taken, weekly_accolades_spent 10", "1/990/10",
              $"{InvCount(instantWarp)}/{Currency("unity_accolades")}/{Var("weekly_accolades_spent")}");
        ushort refractive = ushort.Parse(Lua("report(xi.item.REFRACTIVE_CRYSTAL)"));
        EventEnd(3529, 4 | 0 << 5 | 1 << 13, 1);
        Check("  update category 4, a Refractive Crystal (15000) with 990: MAX_UINT32, nothing taken", "4294967295/0/990",
              $"{Upds()}/{InvCount(refractive)}/{Currency("unity_accolades")}");
        EventEnd(3529, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Unity: changing unity");
        Talk(urbio);
        // category 5 (the change asked), Sylvie's (11) while the tabulation's change is spent
        s.PacketList.Clear();
        EventEnd(3529, 5 | 11 << 5, 1);
        Check("  update category 5 with unity_changed 1: MAX_UINT32, HAVE_ALREADY_CHANGED_UNITY", $"4294967295/{Text(SouthernSandoria, "HAVE_ALREADY_CHANGED_UNITY")}:{5 | 11 << 5}",
              $"{Upds()}/{Special()}");
        Lua("player:setCharVar('unity_changed', 0)");
        int rank3 = int.Parse(Lua("report(player:getUnityRank())")), rank11 = int.Parse(Lua("report(player:getUnityRank(11))"));
        int cost = Math.Max(100, 500 * (11 - rank11) - 400 * (11 - rank3));
        Check("  the ranks of unity_system (getUnityRank: the C++'s UpdateUnityRankings)", "True", rank3 is >= 1 and <= 11 && rank11 is >= 1 and <= 11);
        EventEnd(3529, 5 | 11 << 5, 1);
        Check("  update category 5 (Sylvie's) once the var is cleared: the cost twice", $"{cost},{cost}", Upds());
        Lua($"player:setCurrency('unity_accolades', {cost + 200})");
        EventEnd(3529, 6 | 11 << 5);
        Check("event 3529 finished, category 6 Sylvie's: the cost taken, leader 11, unity_changed 1, the week's accolades reset", $"200/11/1/0/0/11",
              $"{Currency("unity_accolades")}/{Lua("report(player:getUnityLeader())")}/{Var("unity_changed")}/{Currency("current_accolades")}/{Currency("prev_accolades")}/" +
              DbScalar(db, "SELECT unity_leader FROM char_profile WHERE charid = ?"));
        Check("  YOU_HAVE_JOINED_UNITY (Sylvie's: 10)", $"{Text(SouthernSandoria, "YOU_HAVE_JOINED_UNITY")}:10", Special());
        Talk(urbio);
        EventEnd(3529, 6 | 3 << 5);
        Check("  again in the same tabulation (unity_changed 1): nothing", "200/11", $"{Currency("unity_accolades")}/{Lua("report(player:getUnityLeader())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Unity: the unity warp (category 1)");
        Talk(urbio);
        EventEnd(3529, 1 | 3 << 5);
        Check("event 3529 finished, category 1 La Theine Plateau (3): 100 accolades taken", "100", Currency("unity_accolades"));
        Rezone();
        Check("  setPos to La Theine Plateau (2.18, 0, 0.42)", $"{LaTheine}/2.2/0.4", $"{c.Zone}/{c.X:F1}/{c.Z:F1}");
        Lua($"player:setPos(-86.5, 1, -50.75, 0, {SouthernSandoria})");
        Rezone();
        Talk(urbio);
        Lua("player:setCurrency('unity_accolades', 99)");
        EventEnd(3529, 1 | 3 << 5);
        Check("  with 99 accolades: no warp, nothing taken", $"{SouthernSandoria}/99", $"{c.Zone}/{Currency("unity_accolades")}");
        Tick(400);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Unity: the unity menu packets (0x116, 0x117) and the chat toggle (0x118)");
        s.PacketList.Clear();
        Send(0x116, 8, b => BitConverter.GetBytes(0u).CopyTo(b, 4));
        Check("0x116 UNITY_MENU kind 0: GP_SERV_COMMAND_UNITY (0x110) then the 0x063 unity pages", "1/True", $"{Of(0x110).Count}/{Of(0x063).Count > 0}");
        s.PacketList.Clear();
        Send(0x116, 8, b => BitConverter.GetBytes(2u).CopyTo(b, 4));
        Check("  kind 2: refused (Kind 0..1)", "0", Of(0x110).Count);
        s.PacketList.Clear();
        Send(0x117, 8, b => BitConverter.GetBytes(0u).CopyTo(b, 4));
        Check("0x117 UNITY_QUEST kind 0: 0x110 and the pages", "1/True", $"{Of(0x110).Count}/{Of(0x063).Count > 0}");
        s.PacketList.Clear();
        Send(0x118, 8, b => b[4] = 1);
        Check("0x118 toggle on: the 0x061 unity_chat_color_flag set", "1", Of(0x061).Select(p => BitConverter.ToUInt32(p.Buffer, 4 + 92)).LastOrDefault());
        s.PacketList.Clear();
        Send(0x118, 8, b => b[4] = 0);
        Check("  off: cleared", "0", Of(0x061).Select(p => BitConverter.ToUInt32(p.Buffer, 4 + 92)).LastOrDefault());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Beastmen treasure ----
        Console.WriteLine("Beastmen treasure: Peddlestox in Yuhtunga Jungle, hidden until Windsday (updatePeddlestox)");
        ushort mermaidHead = ushort.Parse(Lua("report(xi.item.MERMAID_HEAD)"));
        ushort[] mermaid = { mermaidHead, ushort.Parse(Lua("report(xi.item.MERMAID_BODY)")), ushort.Parse(Lua("report(xi.item.MERMAID_HANDS)")),
                             ushort.Parse(Lua("report(xi.item.MERMAID_TAIL)")) };
        ushort pickaxe = ushort.Parse(Lua("report(xi.item.PICKAXE)"));
        int windsday = int.Parse(Lua("report(xi.day.WINDSDAY)"));
        Fixture(db, TreasureId, YuhtungaJungle, -101.8f, 0.6f, 434.9f, Windurst, War, 75,
                (mermaid[0], 1), (mermaid[1], 1), (mermaid[2], 1), (mermaid[3], 1), (pickaxe, 2));
        // the zone loads on a Firesday (day 8000)
        vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        CharId = TreasureId;
        Login();
        uint ped = NpcId("Peddlestox");
        Check("zone init on a Firesday: Peddlestox left hidden (DISAPPEAR)", "0/2", $"{Lua("report(VanadielDayOfTheWeek())")}/{Npc(ped).Status}");
        // the clock on to Windsday's midnight: the zone's onGameDay
        vanaTicks = (8000 + windsday) * VanaTime.DAY;
        Tick(400);
        Check("Windsday's onGameDay: Peddlestox shown (NORMAL)", $"{windsday}/0", $"{Lua("report(VanadielDayOfTheWeek())")}/{Npc(ped).Status}");
        Near(ped);
        Talk(ped);
        Check("Peddlestox without the map: event 102", $"{EvStr(ped, 102)}/102", $"{Ev()}/{Cur()}");
        EventEnd(102, 0);
        Lua("player:addKeyItem(xi.keyItem.MAP_OF_THE_ELSHIMO_REGIONS)");
        Talk(ped);
        Check("with the map: event 100 (go fetch)", $"{EvStr(ped, 100)}/100", $"{Ev()}/{Cur()}");
        EventEnd(100, 0);
        Check("  event 100 finished: BMT_Lowlands_Status QUEST_ACCEPTED (1)", "1", Var("BMT_Lowlands_Status"));
        Talk(ped);
        Check("  again: event 104 (no charity)", $"{EvStr(ped, 104)}/104", $"{Ev()}/{Cur()}");
        EventEnd(104, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Beastmen treasure: the four Mermaid parts traded, the dig site shown and paid for");
        // the dig site's math.randomInt(1, 8), the gil's and the chip's (a GM's dice)
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "math.randomInt = function(a, b) if a == 1 and b == 8 then return 8 elseif a == 4000 and b == 5000 then return 4321 " +
            "elseif a == 1 and b == 100 then return 25 end return _verifyRandomInt(a, b) end");
        uint digOffset = uint.Parse(Lua($"report(zones[{YuhtungaJungle}].npc.BEASTMEN_TREASURE_OFFSET)"));
        uint site8 = digOffset + 7, site1 = digOffset;
        var q8 = Npc(site8);
        string Pos1000(NpcEntity n) => $"{(uint)(int)(n.X * 1000)},{(uint)(int)(n.Z * 1000)}";
        Trade(ped, (1, 1), (2, 1), (3, 1), (4, 1));
        Check("the trade: event 101 (zone, 0, the dig site's x and z * 1000: qm10's 369795, 201805)", $"034:{ped}:101:{YuhtungaJungle},0,369795,201805/101",
              $"{Ev()}/{Cur()}");
        Check("  the site kept pending: the local var 8, the char var 0, still QUEST_ACCEPTED, the parts still held", "8/0/1/4",
              $"{Lua("report(player:getLocalVar('BMT_Lowlands_Digsite'))")}/{Var("BMT_Lowlands_Digsite")}/{Var("BMT_Lowlands_Status")}/{mermaid.Sum(InvCount)}");
        EventEnd(101, 0);
        Check("event 101 finished: the parts taken (tradeComplete at the event's end), the site 8 saved, QUEST_COMPLETED", "0/8/2/0",
              $"{mermaid.Sum(InvCount)}/{Var("BMT_Lowlands_Digsite")}/{Var("BMT_Lowlands_Status")}/{Lua("report(player:getLocalVar('BMT_Lowlands_Digsite'))")}");
        Talk(ped);
        Check("Peddlestox again: event 103 (the site reminded)", $"034:{ped}:103:{YuhtungaJungle},0,{Pos1000(q8)}/103", $"{Ev()}/{Cur()}");
        EventEnd(103, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Beastmen treasure: the dig sites, the pickaxe and the chest");
        // a GM's !pos to the first dig site (qm3): not the char's
        Lua($"player:setPos({Npc(site1).X + 1}, {Npc(site1).Y}, {Npc(site1).Z})");
        Tick(400);
        Talk(site1);
        Check("qm3 (site 1, not the char's): NOTHING_OUT_OF_ORDINARY", $"{Text(YuhtungaJungle, "NOTHING_OUT_OF_ORDINARY")}:0", Special());
        byte pickSlot = InvItem(pickaxe).Slot;
        Trade(site1, (pickSlot, 1));
        Check("  a pickaxe traded to it: no event, the pickaxe kept and free again (releaseUnconfirmed: xi.itemState.FREE)", "none/2/0",
              $"{Cur()}/{InvItem(pickaxe).Quantity}/{Lua($"report(player:findItem({pickaxe}):state())")}");
        Lua($"player:setPos({q8.X + 1}, {q8.Y}, {q8.Z})");
        Tick(400);
        Talk(site8);
        Check("qm10 (site 8, the char's): SOMETHING_IS_BURIED_HERE with the pickaxe", $"{Text(YuhtungaJungle, "SOMETHING_IS_BURIED_HERE")}:{pickaxe}", Special());
        uint gilBefore = uint.Parse(Lua("report(player:getGil())"));
        Trade(site8, (pickSlot, 1));
        Check("a pickaxe traded to qm10: event 105 (zone, 0, 0, 0, the site - 1)", $"034:{site8}:105:{YuhtungaJungle},0,0,0,7/105", $"{Ev()}/{Cur()}");
        EventEnd(105, 0);
        var digPackets = new List<BasicPacket>(s.PacketList);
        Check("event 105 finished: one pickaxe taken, 4321 gil, GIL_OBTAINED", $"1/{gilBefore + 4321}/{Text(YuhtungaJungle, "GIL_OBTAINED")}:4321",
              $"{InvItem(pickaxe)?.Quantity ?? 0}/{Lua("report(player:getGil())")}/{Special()}");
        // the pool's 0x0D2 per item, in the script's order; a solo pool hands each out at once (checkTreasureItem)
        var pool = digPackets.Where(p => Opcode(p) == 0x0D2).Select(p => (int)BitConverter.ToUInt16(p.Buffer, 16)).ToList();
        int[] chips = Lua("report(table.concat({ xi.item.RED_CHIP, xi.item.BLUE_CHIP, xi.item.YELLOW_CHIP, xi.item.GREEN_CHIP, xi.item.CLEAR_CHIP }, ' '))").Split(' ').Select(int.Parse).ToArray();
        int[] coins = { 748, 749, 750 };
        Check("  addTreasure: a rock or gem, a seed or Coral Fragment, a beastcoin, a seal or Mermaid's Ring, and the 25 % chip (0x0D2 each)", "5/True/True/True/True/True",
              pool.Count < 5 ? string.Join(",", pool) :
              $"{pool.Count}/{pool[0] is >= 769 and <= 814}/{pool[1] is (>= 572 and <= 575) or 887}/{coins.Contains(pool[2])}/{pool[3] is 1126 or 14626}/{chips.Contains(pool[4])}");
        Check("  the solo pool hands them to the char at once (memberCount 1: checkTreasureItem), the pool empty", "5/0",
              $"{pool.Count(id => InvCount((ushort)id) > 0)}/" +
              Lua("local n = 0 for _, it in ipairs(player:getTreasurePool():getItems()) do if it.id ~= 0 then n = n + 1 end end report(n)"));
        Check("  the hunt reset: QUEST_AVAILABLE, no site", "0/0", $"{Var("BMT_Lowlands_Status")}/{Var("BMT_Lowlands_Digsite")}");
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        Talk(site8);
        Check("  qm10 again: NOTHING_OUT_OF_ORDINARY", $"{Text(YuhtungaJungle, "NOTHING_OUT_OF_ORDINARY")}:0", Special());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Beastmen treasure: Iceday's onGameDay: Peddlestox teleports away");
        Lua($"player:setPos({Npc(ped).X + 1.5}, {Npc(ped).Y}, {Npc(ped).Z})");
        Tick(400);
        s.PacketList.Clear();
        vanaTicks = (8000 + windsday + 1) * VanaTime.DAY;
        Tick(400);
        Check("independentAnimation 122 (0x03A) to the chars near", "True",
              Of(0x03A).Any(p => BitConverter.ToUInt32(p.Buffer, 4) == ped && BitConverter.ToUInt16(p.Buffer, 16) == 122));
        Check("  still shown", "0", Npc(ped).Status);
        for (int i = 0; i < 14; i++) Tick(400);
        Check("  5 s on (its timer): DISAPPEAR", "2", Npc(ped).Status);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Dark Ixion ----
        Console.WriteLine("Dark Ixion: Jugner Forest [S] assigned and due (the server vars): zoneOnInit, the game hour's pop");
        ushort StygianAsh = ushort.Parse(Lua("report(xi.item.PINCH_OF_STYGIAN_ASH)"));
        Fixture(db, IxionId, JugnerS, -540.0f, -0.3f, -440.0f, Windurst, War, 75, (StygianAsh, 12));
        // a GM's server vars: Jugner Forest [S] (82) holds him and his pop time has passed
        Lua($"SetServerVariable('DarkIxion_ZoneID', {JugnerS}) SetServerVariable('DarkIxion_PopTime', GetSystemTime() - 100)");
        CharId = IxionId;
        Login();
        string IxVar(string name) => Lua($"report(GetServerVariable('DarkIxion_{name}'))");
        uint ixId = uint.Parse(Lua("report(GetZone(player:getZoneID()):queryEntitiesByName('Dark_Ixion')[1]:getID())"));
        var ix = Mob(ixId);
        string IxLua(string expr) => Lua($"local m = GetMobByID({ixId}) report({expr})");
        Check("zoneOnInit, due in its zone: DarkIxion_PopTime pushed to 10 s on, not up", $"{EarthNow() + 10}/False", $"{IxVar("PopTime")}/{ix.IsSpawned}");
        // the pop time 45 s gone, then the Vana'diel hour: zoneOnGameHour's setRespawnTime(math.randomInt(0, 90)) (a GM's 30)
        for (int i = 0; i < 60; i++) Tick(1000);
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "math.randomInt = function(a, b) if a == 0 and b == 90 then return 30 end return _verifyRandomInt(a, b) end");
        vanaTicks += VanaTime.HOUR;
        Tick(400);
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        Check("  the game hour: not up yet (its respawn 30 s on)", "False", ix.IsSpawned);
        long ixPop = 0;
        for (int i = 0; i < 40 && !ix.IsSpawned; i++) { Tick(1000); if (ix.IsSpawned) ixPop = EarthNow(); }
        Check("  up 30 s on: onMobSpawn's DarkIxion_PopTime now", $"True/{ixPop}", $"{ix.IsSpawned}/{IxVar("PopTime")}");
        Check("  roamingMods: speed 40, UDMGPHYS / BREATH / MAGIC -10000, REGAIN 0, the HP stored (DarkIxion_HP)",
              $"40/-10000/-10000/-10000/0/{ix.Hp}",
              $"{IxLua("m:getBaseSpeed()")}/{IxLua("m:getMod(xi.mod.UDMGPHYS)")}/{IxLua("m:getMod(xi.mod.UDMGBREATH)")}/{IxLua("m:getMod(xi.mod.UDMGMAGIC)")}/" +
              $"{IxLua("m:getMod(xi.mod.REGAIN)")}/{IxVar("HP")}");
        Check("  changeHornState(1): DarkIxion_HornState 1, animationSub 0 (NORMAL), the HP hidden", "1/0/true",
              $"{IxVar("HornState")}/{ix.AnimationSub}/{((ix.EntityFlags & 0x100) != 0).ToString().ToLower()}");
        Check("  aggressive, roam flags SCRIPTED, mob skill attack 39, immune to stun (onMobInitialize)", "true/True/39/true",
              $"{ix.Aggro.ToString().ToLower()}/{(ix.RoamFlags & int.Parse(Lua("report(xi.roamFlag.SCRIPTED)"))) != 0}/" +
              $"{IxLua("m:getMobMod(xi.mobMod.ATTACK_SKILL_LIST)")}/{IxLua("tostring(m:hasImmunity(xi.immunity.STUN))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dark Ixion: the patrol (onMobRoam's pathThrough), spooked (onMobEngage's disengage), the run away and the repop");
        float ixX0 = ix.X, ixZ0 = ix.Z;
        for (int i = 0; i < 20; i++) Tick(400);
        Check("onMobRoam: following the zone's patrol path (pathThrough RUN | PATROL), on the way to its second point (-139.6, 97)", "true/True",
              $"{IxLua("tostring(m:isFollowingPath())")}/{MathF.Sqrt((ix.X + 139.6f) * (ix.X + 139.6f) + (ix.Z - 97f) * (ix.Z - 97f)) < MathF.Sqrt((ixX0 + 139.6f) * (ixX0 + 139.6f) + (ixZ0 - 97f) * (ixZ0 - 97f)) - 2}");
        // a GM's !pos in front of him: his aggro (sight) engages the char
        Lua("player:setUnkillable(true)");
        // the spawn's 15 s of neutral (kNeutralDuration) over first
        for (int i = 0; i < 60 && ix.Neutral; i++) Tick(400);
        float fx = MathF.Cos(ix.Rotation * MathF.PI / 128f), fz = -MathF.Sin(ix.Rotation * MathF.PI / 128f);
        Lua($"player:setPos({ix.X + fx * 8}, {ix.Y}, {ix.Z + fz * 8})");
        for (int i = 0; i < 40 && Lua($"local m = GetMobByID({ixId}) report(m:getLocalVar('RunAway'))") == "0"; i++)
        {
            // the client's position packets as it stands there (the 0x015 that spawns the mobs and taps their aggro)
            s.PacketList.Clear();
            MoveTo(c.X, c.Y, c.Z);
            Tick(400);
        }
        long runAway = long.Parse(IxLua("m:getLocalVar('RunAway')"));
        Check("spooked: engaged by aggro, onMobEngage (no Stygian Ash) disengages at once", "True/False/True", $"{runAway > 0}/{ix.Engaged}/{ix.OwnerId == 0}");
        Check("  onMobDisengage: RunAway 15 s on, speed 70, no longer aggressive, the HP stored", $"{EarthNow() + 15}/70/false/{ix.Hp}",
              $"{runAway}/{IxLua("m:getBaseSpeed()")}/{ix.Aggro.ToString().ToLower()}/{IxVar("HP")}");
        float stunX = ix.X, stunZ = ix.Z;
        for (int i = 0; i < 25; i++) Tick(400);
        Check("  stun(15000) (PAI->Inactive): standing still 10 s on", "True", MathF.Abs(ix.X - stunX) < 0.01f && MathF.Abs(ix.Z - stunZ) < 0.01f);
        for (int i = 0; i < 20; i++) Tick(400);
        Check("  running again once it is over", "True", MathF.Abs(ix.X - stunX) > 1f || MathF.Abs(ix.Z - stunZ) > 1f);
        Lua($"player:setPos(-540, -0.3, -440)");
        Tick(400);
        // the repop's zone (keys[math.randomInt(1, #keys)]: a GM's West Sarutabaruta [S]) and pop time (1 to 15 minutes: 600 s)
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "local keys = {} for k, _ in pairs(xi.darkixion.zoneinfo) do table.insert(keys, k) end " +
            "local idx = 1 for i, k in ipairs(keys) do if k == xi.zone.WEST_SARUTABARUTA_S then idx = i end end " +
            "math.randomInt = function(a, b) if a == 1 and b == #keys then return idx elseif a == 1 and b == 900 then return 600 end return _verifyRandomInt(a, b) end");
        long repopAt = 0;
        for (int i = 0; i < 100 && repopAt == 0; i++)
        {
            Tick(1000);
            if (IxVar("ZoneID") != JugnerS.ToString()) repopAt = EarthNow();
        }
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        int westSaruta = int.Parse(Lua("report(xi.zone.WEST_SARUTABARUTA_S)"));
        Check("onMobRoam 60 s after RunAway (the stun's 15 s on): repop to West Sarutabaruta [S], pop time 600 s on", $"True/{westSaruta}/{repopAt + 600}",
              $"{repopAt - runAway is >= 60 and <= 63}/{IxVar("ZoneID")}/{IxVar("PopTime")}");
        for (int i = 0; i < 10; i++) Tick(400);
        Check("  despawned (DespawnMob's 3 s fade), onMobDespawn: not its zone now, the pop time left", $"False/{repopAt + 600}", $"{ix.IsSpawned}/{IxVar("PopTime")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dark Ixion: claimed with a Stygian Ash (the ranged hit's NM_SPECIFIC additional effect)");
        // a GM's !setserver DarkIxion_ZoneID back to Jugner Forest [S] and !spawnmob; the char 10' behind him, facing his back
        Lua($"SetServerVariable('DarkIxion_ZoneID', {JugnerS})");
        Lua($"SpawnMob({ixId})");
        Tick(400);
        Check("SpawnMob: up again at its spawn point, the HP kept from the server var", $"True/{IxVar("HP")}", $"{ix.IsSpawned}/{ix.Hp}");
        float bx = -MathF.Cos(ix.Rotation * MathF.PI / 128f) * 10, bz = MathF.Sin(ix.Rotation * MathF.PI / 128f) * 10;
        Lua($"player:setPos({ix.X + bx}, {ix.Y}, {ix.Z + bz})");
        Tick(400);
        s.PacketList.Clear();
        byte toIx = (byte)(short)(MathF.Atan2(ix.Z - (ix.Z + bz), ix.X - (ix.X + bx)) * -(128.0f / MathF.PI));
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
            b[C2sPos.OffDir] = toIx;
        });
        Equip(InvItem(StygianAsh).Slot, CharStats.SlotAmmo);
        Check("  the Stygian Ash on (ammo)", StygianAsh, c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0);
        // the shot's rolls made sure (a GM's loaded dice: the engine's rolls their low end)
        var fairRng = zone.MobRng;
        zone.MobRng = new LoadedRandom();
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(ix.Id).CopyTo(b, 4);
            BitConverter.GetBytes(ix.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Shoot).CopyTo(b, C2sAction.OffActionID);
        });
        var shotPackets = new List<BasicPacket>();
        for (int i = 0; i < 30 && (c.InRangedAttack || shotPackets.Count == 0); i++)
        {
            Tick(200);
            shotPackets.AddRange(s.PacketList);
            s.PacketList.Clear();
        }
        for (int i = 0; i < 5; i++) Tick(400);
        zone.MobRng = fairRng;
        Check("the shot landed: aeFromItemId the ash (xi.additionalEffect NM_SPECIFIC, Dark Ixion not in nmSpecificConfigs)", $"{StygianAsh}", IxLua("m:getLocalVar('aeFromItemId')"));
        Check("  claimed and fighting: onMobEngage kept the fight (RunAway 0)", "True/True/0", $"{ix.Engaged}/{ix.OwnerId == c.Id}/{IxLua("m:getLocalVar('RunAway')")}");
        Check("  the fight's mods: UDMGPHYS / RANGE / BREATH / MAGIC 0, REGAIN 20, phaseChange 60 to 240 s on", "0/0/0/0/20/True",
              $"{IxLua("m:getMod(xi.mod.UDMGPHYS)")}/{IxLua("m:getMod(xi.mod.UDMGRANGE)")}/{IxLua("m:getMod(xi.mod.UDMGBREATH)")}/{IxLua("m:getMod(xi.mod.UDMGMAGIC)")}/" +
              $"{IxLua("m:getMod(xi.mod.REGAIN)")}/{long.Parse(IxLua("m:getLocalVar('phaseChange')")) - EarthNow() is >= 55 and <= 240}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dark Ixion: the fight: the glow phase (onMobFight), the horn attacks (skill list 39) and the trample");
        // a GM's !exec of the phase timer: due now
        Lua($"GetMobByID({ixId}):setLocalVar('phaseChange', GetSystemTime())");
        for (int i = 0; i < 10 && ix.AnimationSub == 0; i++) Tick(400);
        Check("phaseChange due: GLOWING (animationSub 3), the next change 60 to 240 s on", "3/True",
              $"{ix.AnimationSub}/{long.Parse(IxLua("m:getLocalVar('phaseChange')")) - EarthNow() is >= 59 and <= 240}");
        Lua($"GetMobByID({ixId}):setLocalVar('phaseChange', GetSystemTime())");
        for (int i = 0; i < 10 && ix.AnimationSub == 3; i++) Tick(400);
        Check("  due again: back to NORMAL (0)", "0", ix.AnimationSub);
        // the next phase change far off (a GM's), and the horn attacks' trample roll (math.randomInt(1, 100) <= 10: a GM's 5)
        Lua($"GetMobByID({ixId}):setLocalVar('phaseChange', GetSystemTime() + 1000)");
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "math.randomInt = function(a, b) if a == 1 and b == 100 then return 5 end return _verifyRandomInt(a, b) end");
        // the trample's pick among the entities within 30 yalms (utils.randomEntry): a GM's the char, not a mob wandering by
        Lua("_verifyRandomEntry = utils.randomEntry utils.randomEntry = function(t) for _, e in pairs(t) do " +
            "local ok, pc = pcall(function() return e:isPC() end) if ok and pc then return e end end return _verifyRandomEntry(t) end");
        int[] meleeSkills = { 2329, 2330, 2331 };
        bool trampleSeen = false, busySeen = false, noAutoSeen = false;
        int trampleTarg = 0, speedSeen = 0;
        var fightPackets = new List<BasicPacket>();
        for (int i = 0; i < 150 && !(trampleSeen && ix.AnimationSub == 0); i++)
        {
            // the client keeps facing him
            float kx = ix.X - c.X, kz = ix.Z - c.Z;
            byte kdir = (byte)(short)(MathF.Atan2(kz, kx) * -(128.0f / MathF.PI));
            s.PacketList.Clear();
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = kdir;
            });
            Tick(400);
            fightPackets.AddRange(s.PacketList);
            if (ix.AnimationSub == 1)
            {
                trampleSeen = true;
                busySeen |= IxLua("m:getLocalVar('isBusy')") == "1";
                noAutoSeen |= !ix.AutoAttackEnabled;
                trampleTarg = int.Parse(IxLua("m:getLocalVar('trampleTargID')"));
                speedSeen = int.Parse(IxLua("m:getBaseSpeed()"));
            }
        }
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        Lua("if _verifyRandomEntry then utils.randomEntry = _verifyRandomEntry _verifyRandomEntry = nil end");
        // his 0x028 mob skill actions (category 11) and their skill ids
        static ulong UB(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);
        var skills = fightPackets.Where(p => Opcode(p) == 0x028 && UB(p, 40, 32) == ix.Id && UB(p, 82, 4) == 11).Select(p => (int)UB(p, 86, 32)).ToList();
        Check("his auto-attacks are the Ixion_Melee skills (setMobSkillAttack(39))", "True", skills.Any(meleeSkills.Contains));
        Check("  a horn attack's roll (5 <= 10): trampleCount 1; onMobFight: the trample (animationSub 1, isBusy, no auto-attack, speed 70) at the char",
              $"True/True/True/{c.TargId}/70", $"{trampleSeen}/{busySeen}/{noAutoSeen}/{trampleTarg}/{speedSeen}");
        Check("  DI_TRAMPLE (2332) on the char in front", "True", skills.Contains(2332));
        Check("  endTramplePath: NORMAL again, auto-attacks back, speed 40, trampleCount 0, nextTrampleTime 15 to 60 s on", "0/True/40/0/True",
              $"{ix.AnimationSub}/{ix.AutoAttackEnabled}/{IxLua("m:getBaseSpeed()")}/{IxLua("m:getLocalVar('trampleCount')")}/" +
              $"{long.Parse(IxLua("m:getLocalVar('nextTrampleTime')")) - EarthNow() is >= 10 and <= 60}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dark Ixion: killed (the char's swings, a GM's loaded dice): the title, the HP and horn reset, the next pop 20 to 24 hours on");
        // the despawn's repop (West Sarutabaruta [S] again) and the 20 to 24 hours (a GM's 22)
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "local keys = {} for k, _ in pairs(xi.darkixion.zoneinfo) do table.insert(keys, k) end " +
            "local idx = 1 for i, k in ipairs(keys) do if k == xi.zone.WEST_SARUTABARUTA_S then idx = i end end " +
            "math.randomInt = function(a, b) if a == 1 and b == #keys then return idx elseif a == 20 and b == 24 then return 22 end return _verifyRandomInt(a, b) end");
        Lua($"GetMobByID({ixId}):setLocalVar('nextTrampleTime', GetSystemTime() + 1000)");
        zone.MobRng = new LoadedRandom();
        Kill(ix);
        zone.MobRng = fairRng;
        Check("killed: onMobDeath's IXION_HORNBREAKER title, DarkIxion_HP 0, DarkIxion_HornState 1", "True/0/1",
              $"{Lua("report(tostring(player:hasTitle(xi.title.IXION_HORNBREAKER)))") == "true"}/{IxVar("HP")}/{IxVar("HornState")}");
        for (int i = 0; i < 100 && ix.IsSpawned; i++) Tick(400);
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        long despawnAt = EarthNow();
        Check("  despawned; onMobDespawn in its zone: repop (West Sarutabaruta [S]), DarkIxion_PopTime 22 hours on", $"False/{westSaruta}/True",
              $"{ix.IsSpawned}/{IxVar("ZoneID")}/{Math.Abs(long.Parse(IxVar("PopTime")) - (despawnAt + 22 * 3600)) <= 2}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Dark Rider ----
        Console.WriteLine("Dark Rider: Wajaom Woodlands due (the server vars): the game hour's pop, the route and its escorts");
        Fixture(db, RiderId, Wajaom, -469.0f, -18.4f, 177.7f, Windurst, War, 75);
        Lua($"SetServerVariable('DarkRider_ZoneID', {Wajaom}) SetServerVariable('DarkRider_PopTime', GetSystemTime() - 10)");
        CharId = RiderId;
        Login();
        string DrVar(string name) => Lua($"report(GetServerVariable('DarkRider_{name}'))");
        uint riderId = uint.Parse(Lua($"report(zones[{Wajaom}].mob.DARK_RIDER)"));
        uint hoof0 = uint.Parse(Lua($"report(zones[{Wajaom}].npc.HOOFPRINT)"));
        var rider = Mob(riderId);
        string DrLua(string expr) => Lua($"local m = GetMobByID({riderId}) report({expr})");
        Check("onZoneInitialize: a pop time already set (not 0, not over 3 hours on) is kept; the rider down", $"{Wajaom}/False", $"{DrVar("ZoneID")}/{rider.IsSpawned}");
        // the route (math.randomInt(1, #routes): a GM's first) and the legs' lengths (150 to 200: a GM's 150) and rests (6 to 11 s: 6)
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "math.randomInt = function(a, b) if a == 1 and b == 4 then return 1 elseif a == 150 and b == 200 then return 150 elseif a == 6 and b == 11 then return 6 " +
            "elseif a == 60 and b == 72 then return 66 end return _verifyRandomInt(a, b) end");
        vanaTicks += VanaTime.HOUR;
        Tick(400);
        Check("the game hour, its zone and its time: SpawnMob (the rider up at the route's start (-640.7, -133.8))", "True/-640.7/-133.8",
              $"{rider.IsSpawned}/{rider.X:F1}/{rider.Z:F1}");
        Check("  onMobSpawn: routeIndex 1, pointIndex 1, UDMGPHYS -10000, UFASTCAST 150, MAGIC_COOL 30, immune to sleep", "1/1/-10000/150/30/true",
              $"{DrLua("m:getLocalVar('routeIndex')")}/{DrLua("m:getLocalVar('pointIndex')")}/{DrLua("m:getMod(xi.mod.UDMGPHYS)")}/{DrLua("m:getMod(xi.mod.UFASTCAST)")}/" +
              $"{DrLua("m:getMobMod(xi.mobMod.MAGIC_COOL)")}/{DrLua("tostring(m:hasImmunity(xi.immunity.DARK_SLEEP))")}");
        var escorts = Enumerable.Range(1, 6).Select(o => Mob(riderId + (uint)o)).ToList();
        Check("  the six escorts up, spawned at the route's posts (setSpawn then SpawnMob): the first's (-636.75, -123.9)", "True/-636.8/-123.9",
              $"{escorts.All(e => e.IsSpawned)}/{escorts[0].SpawnX:F1}/{escorts[0].SpawnZ:F1}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dark Rider: the run (onMobRoamAction's legs and rests), the hoofprints, the end of the route");
        float rx0 = rider.X, rz0 = rider.Z;
        int legs = 0, offMesh = 0;
        string restingWas = "0";
        var hoofSeen = new List<string>();
        long runStart = EarthNow();
        for (int i = 0; i < 3000 && rider.IsSpawned; i++)
        {
            Tick(400);
            string resting = DrLua("m:getLocalVar('resting')");
            if (resting == "1" && restingWas == "0") legs++;
            restingWas = resting;
            string placed = DrLua("m:getLocalVar('hoofprints')");
            if (hoofSeen.Count == 0 || hoofSeen[^1] != placed) hoofSeen.Add(placed);
            // the reconstructed route's heights leave him off the navmesh at some rests, where DoRoamTick's guard (an
            // idle mob off walkable ground does nothing) would hold him for good, in C++ too: a GM's !pos onto the
            // ground below, the run going on from there
            if (!rider.PathFind.IsFollowingPath && !rider.PathFind.ValidPosition(rider.X, rider.Y, rider.Z, now) &&
                zone.NavMeshOf(Wajaom)?.FindClosestValidPoint(rider.X, rider.Y, rider.Z) is { } ground)
            {
                Lua($"GetMobByID({riderId}):setPos({ground.X}, {ground.Y}, {ground.Z})");
                offMesh++;
            }
        }
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        long runEnd = EarthNow();
        Check("the rider ran the route in legs (pathThrough COORDS | RUN | SCRIPT), a hoofprint left at each rest, and despawned at its end",
              "False/True/True", $"{rider.IsSpawned}/{hoofSeen.Count >= 3}/{runEnd - runStart > 60}");
        Check("  onMobDespawn: the three hoofprints shown (NORMAL) where the rider rested", "0/0/0",
              $"{Npc(hoof0).Status}/{Npc(hoof0 + 1).Status}/{Npc(hoof0 + 2).Status}");
        Check("  scheduleRun(60, 72): the next run 66 hours on, in one of the four zones", "True/True",
              $"{Math.Abs(long.Parse(DrVar("PopTime")) - (runEnd + 66 * 3600)) <= 2}/{new[] { 51, 52, 61, 79 }.Contains(int.Parse(DrVar("ZoneID")))}");
        for (int i = 0; i < 25; i++) Tick(400);
        Check("  the escorts despawned (their 5 s timers, then the 3 s fade)", "True", escorts.All(e => !e.IsSpawned));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dark Rider: the hoofprints fade over the 12 hours before the next run (onGameHour's rolls)");
        // an hour with the run 66 hours off (no roll over 12 hours): hoofprintRoll 66
        vanaTicks += VanaTime.HOUR;
        Tick(400);
        Check("an hour on, 66 hours left: no roll, hoofprintRoll 66, the three still shown", "66/3",
              $"{Lua($"report(GetZone({Wajaom}):getLocalVar('hoofprintRoll'))")}/{Enumerable.Range(0, 3).Count(o => Npc(hoof0 + (uint)o).Status == 0)}");
        // the next run (a GM's) 11 hours on in this zone; the hour's roll (math.randomInt(1, 5) == 1: a GM's 1)
        Lua($"SetServerVariable('DarkRider_ZoneID', {Wajaom}) SetServerVariable('DarkRider_PopTime', GetSystemTime() + 11 * 3600 - 60)");
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "math.randomInt = function(a, b) if a == 1 and b == 5 then return 1 end return _verifyRandomInt(a, b) end");
        vanaTicks += VanaTime.HOUR;
        Tick(400);
        int shown = Enumerable.Range(0, 3).Count(o => Npc(hoof0 + (uint)o).Status == 0);
        Check("11 hours left (under the last hour's roll of 66): one hoofprint gone, hoofprintRoll 11", "2/11",
              $"{shown}/{Lua($"report(GetZone({Wajaom}):getLocalVar('hoofprintRoll'))")}");
        vanaTicks += VanaTime.HOUR;
        Tick(400);
        Check("  the next hour, 11 hours left still (not under the roll): none more", "2", Enumerable.Range(0, 3).Count(o => Npc(hoof0 + (uint)o).Status == 0));
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        // the run due: the rest go, and the rider pops
        Lua("SetServerVariable('DarkRider_PopTime', GetSystemTime() - 1)");
        vanaTicks += VanaTime.HOUR;
        Tick(400);
        Check("the run due: the hoofprints all gone, the rider up again", "0/True",
              $"{Enumerable.Range(0, 3).Count(o => Npc(hoof0 + (uint)o).Status == 0)}/{rider.IsSpawned}");
        Lua($"DespawnMob({riderId})");
        for (int i = 0; i < 25; i++) Tick(400);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- The Strange Apparatus ----
        Console.WriteLine("Strange Apparatus: Dangruf Wadi's (_5b1), the password (0x060) and the doctor status");
        const ushort InfinityCore = 1474, RedChip = 474, BlueChip = 475, FireCluster = 4104, RuneBaghnakhs = 16408;
        Fixture(db, ApparatusId, DangrufWadi, -495.0f, -5.0f, -100.0f, Windurst, War, 75,
                (InfinityCore, 4), (RedChip, 2), (BlueChip, 1));
        CharId = ApparatusId;
        Login();
        uint app = NpcId("_5b1");
        Near(app);
        Talk(app);
        Check("the apparatus: event 1 (no doctor status, 0, the Infinity Core, the zone in the 8th), strAppPass 1",
              $"{EvStr(app, 1, 0, 0, InfinityCore, 0, 0, 0, 0, DangrufWadi)}/1/1", $"{Ev()}/{Cur()}/{Lua("report(player:getLocalVar('strAppPass'))")}");
        // GP_CLI_COMMAND_PASSWARDS: the input string
        void Password(string text)
        {
            s.PacketList.Clear();
            Send(0x060, 28, b =>
            {
                BitConverter.GetBytes(app).CopyTo(b, 4);
                BitConverter.GetBytes(Npc(app).TargId).CopyTo(b, 8);
                System.Text.Encoding.ASCII.GetBytes(text).CopyTo(b, 12);
            });
        }
        string Ucoff() => string.Join(",", Of(0x052).Select(p => p.Buffer[4]));
        Password("12345678");
        Check("0x060 a wrong password: updateEvent(1, 0, the core), EVENTUCOFF EventRecvPending then CancelInput, no doctor status",
              $"1,0,{InfinityCore}/1,3/0", $"{Upds()}/{Ucoff()}/{Var("StrangeApparatusDoctorStatusDW")}");
        // generatePassword: the name's first three letters (c, f, i: 2, 5, 8) plus the uid (0) each, and their sum's last two digits
        string pass = Lua("local n = string.lower(player:getName()) local function v(i) return string.byte(n, i) - 97 end " +
                          "report(string.format('%02d%02d%02d%02d', v(1), v(2), v(3), (v(1) + v(2) + v(3)) % 100))");
        Password(pass);
        Check($"0x060 the char's password ({pass}): updateEvent(0, 0, the core), the doctor status 2 days on", $"0,0,{InfinityCore}/{EarthNow() + 172800}",
              $"{Upds()}/{Var("StrangeApparatusDoctorStatusDW")}");
        Password("12345678");
        Check("  again with the status: no update", "", Upds());
        EventEnd(1, 0);
        Talk(app);
        Check("the apparatus with the doctor status: event 1 (1, ...)", EvStr(app, 1, 1, 0, InfinityCore, 0, 0, 0, 0, DangrufWadi), Ev());
        EventEnd(1, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Strange Apparatus: an Infinity Core and the zone's chip (red), the reward; another chip, the elemental");
        // the reward's math.randomFloat(0, 1) (a GM's 0.1: a Rune Baghnakhs) and the clusters' roll (math.randomInt(1, 100) <= 50: 30)
        Lua("if not _verifyRandomFloat then _verifyRandomFloat = math.randomFloat end math.randomFloat = function(a, b) if a == 0 and b == 1 then return 0.1 end return _verifyRandomFloat(a, b) end " +
            "if not _verifyRandomInt then _verifyRandomInt = math.randomInt end math.randomInt = function(a, b) if a == 1 and b == 100 then return 30 end return _verifyRandomInt(a, b) end");
        Trade(app, (InvItem(InfinityCore).Slot, 1), (InvItem(RedChip).Slot, 1));
        Check("core + red chip with the doctor status: event 3 (the Rune Baghnakhs, 1, the core, ..., the doctor status 1)",
              $"{EvStr(app, 3, RuneBaghnakhs, 1, InfinityCore, 0, 0, 0, 1)}/3", $"{Ev()}/{Cur()}");
        Check("  the core and the chip taken (confirmTrade)", "3/1", $"{InvItem(InfinityCore)?.Quantity ?? 0}/{InvItem(RedChip)?.Quantity ?? 0}");
        EventEnd(3, 0);
        Check("event 3 finished: the Rune Baghnakhs given, ITEM_OBTAINED, strAppDrop cleared", $"1/{Text(DangrufWadi, "ITEM_OBTAINED")}:{RuneBaghnakhs}/0",
              $"{InvCount(RuneBaghnakhs)}/{Special()}/{Lua("report(player:getLocalVar('strAppDrop'))")}");
        // the doctor status gone (a GM's), the cluster roll wins
        Lua("player:setCharVar('StrangeApparatusDoctorStatusDW', 0)");
        Trade(app, (InvItem(InfinityCore).Slot, 1), (InvItem(RedChip).Slot, 1));
        Check("core + red chip without it: the clusters roll (30 <= 50): event 3 (two Fire Clusters, ..., 0)",
              $"{EvStr(app, 3, FireCluster, 2, InfinityCore)}/3", $"{Ev()}/{Cur()}");
        EventEnd(3, 0);
        Check("  two Fire Clusters given", "2", InvItem(FireCluster)?.Quantity ?? 0);
        Lua("if _verifyRandomFloat then math.randomFloat = _verifyRandomFloat _verifyRandomFloat = nil end if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        // the doctor status back (a GM's), then the wrong chip
        Lua("player:setCharVar('StrangeApparatusDoctorStatusDW', GetSystemTime() + 1000)");
        uint elemental = uint.Parse(Lua($"report(zones[{DangrufWadi}].mob.APPARATUS_ELEMENTAL)"));
        Lua("player:setUnkillable(true)");
        Trade(app, (InvItem(InfinityCore).Slot, 1), (InvItem(BlueChip).Slot, 1));
        // addItem never stacks onto the traded core: a second stack of 1 beside the 1 left
        Check("core + blue chip (not the zone's): the core given back (a new stack), the chip lost, SYS_OVERLOAD, YOU_LOST_THE, the doctor status erased",
              $"1+1/0/{Text(DangrufWadi, "SYS_OVERLOAD")}:0,{Text(DangrufWadi, "YOU_LOST_THE")}:{BlueChip}/0/none",
              $"{string.Join("+", c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == InfinityCore && i.Quantity > 0).Select(i => i.Quantity))}/{InvCount(BlueChip)}/{SpecialOf(s.PacketList.Where(p => BitConverter.ToUInt16(p.Buffer, 26) != Text(DangrufWadi, "ITEM_OBTAINED")))}/" +
              $"{Var("StrangeApparatusDoctorStatusDW")}/{Cur()}");
        Tick(400);
        var fireEl = Mob(elemental);
        Check("  the Fire Elemental spawned and set on the char (updateEnmity)", "True/True",
              $"{fireEl.IsSpawned && fireEl.IsAlive}/{fireEl.Engaged && fireEl.BattleTarget == c}");
        Lua($"DespawnMob({elemental})");
        Trade(app, (InvItem(InfinityCore).Slot, 1));
        Check("the core alone: DEVICE_NOT_WORKING", $"{Text(DangrufWadi, "DEVICE_NOT_WORKING")}:0", Special());
        for (int i = 0; i < 10; i++) Tick(400);
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Brigand's Chart ----
        Console.WriteLine("Brigand's Chart: Buburimu Peninsula's ??? (qm1), the chart returned to the sea, the event's three minutes");
        const ushort BrigandsChart = 1873, PenguinRing = 15556, YellowGlobe = 4403;
        Fixture(db, BrigandId, Buburimu, -86.0f, 20.4f, -335.0f, Windurst, War, 75, (BrigandsChart, 1));
        CharId = BrigandId;
        Login();
        // the server's fishing on (xi.settings.map.FISHING_ENABLE, false by default: qm1's onTrade does nothing then)
        string fishingWas = Lua("report(tostring(xi.settings.map.FISHING_ENABLE))");
        Lua("xi.settings.map.FISHING_ENABLE = true");
        uint bqm = NpcId("qm1");
        uint hume = uint.Parse(Lua($"report(zones[{Buburimu}].npc.BRIGAND_CHART_HUME)")), shimmer = uint.Parse(Lua($"report(zones[{Buburimu}].npc.SHIMMERING_POINT)"));
        var etuis = Lua($"report(table.concat(zones[{Buburimu}].npc.JADE_ETUI_TABLE, ' '))").Split(' ').Select(uint.Parse).ToList();
        Check("before: the ??? shown, the hume and the shimmering point hidden, the Jade Etuis hidden", "0/2/2/True",
              $"{Npc(bqm).Status}/{Npc(hume).Status}/{Npc(shimmer).Status}/{etuis.All(e => Npc(e).Status != 0)}");
        Near(bqm);
        Trade(bqm, (InvItem(BrigandsChart).Slot, 1));
        Check("the chart traded: RETURN_TO_SEA, event 902", $"{Text(Buburimu, "RETURN_TO_SEA")}:{BrigandsChart}/{EvStr(bqm, 902)}/902", $"{Special()}/{Ev()}/{Cur()}");
        EventEnd(902, 0, 1);
        var musics = Of(0x05F).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 4)}:{BitConverter.ToUInt16(p.Buffer, 6)}").ToList();
        Check("update 0: the chart taken (confirmTrade), bChartActive, the four music slots 136, LEVEL_RESTRICTION 20",
              "0/1/0:136,1:136,2:136,3:136/20", $"{InvCount(BrigandsChart)}/{Lua("report(player:getLocalVar('bChartActive'))")}/{string.Join(",", musics)}/" +
              Lua("local e = player:getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(e and e:getPower() or 0)"));
        Check("  the ??? holds the spawner (bChartSpawnerID)", c.Id.ToString(), NpcLocal(bqm, "bChartSpawnerID"));
        EventEnd(902, 0);
        var startPackets = new List<BasicPacket>(s.PacketList);
        Check("event 902 finished: the hume and the shimmering point shown, the ??? hidden, MY_ITEM (a Penguin Ring)", $"0/0/2/{Text(Buburimu, "MY_ITEM")}:{PenguinRing}",
              $"{Npc(hume).Status}/{Npc(shimmer).Status}/{Npc(bqm).Status}/{SpecialOf(startPackets)}");
        var shimmerPackets = new List<BasicPacket>();
        for (int i = 0; i < 6; i++) { s.PacketList.Clear(); Tick(400); shimmerPackets.AddRange(s.PacketList); }
        Check("  2 s on: the shimmering point's SHIMMER (0x038 efon)", "True",
              shimmerPackets.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == shimmer && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "efon"));
        var emotes = new List<BasicPacket>();
        for (int i = 0; i < 160; i++) { s.PacketList.Clear(); Tick(400); emotes.AddRange(s.PacketList); }
        int whatCan = Text(Buburimu, "WHAT_CAN_I_DO");
        Check("  past a minute (emoteChecking's 1 s timer): WHAT_CAN_I_DO, the ??? the speaker", $"{whatCan}:0", SpecialOf(emotes));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Brigand's Chart: the Jade Etuis fished up (a GM shows one: Fishing.Live drives the catch), opened");
        // the common reward's roll (1: the first, a Beastcoin) and the special's (the top: the Penguin Ring)
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "local function total(g) local n = 0 for _, e in pairs(g) do if type(e) == 'table' then n = n + e.weight end end return n end " +
            "local cm, sm = total(xi.brigandsChart.rewards.common[1]), total(xi.brigandsChart.rewards.special[1]) " +
            "math.randomInt = function(a, b) if a == 1 and b == sm then return b elseif a == 1 and b == cm then return 1 end return _verifyRandomInt(a, b) end");
        ushort beastcoin = ushort.Parse(Lua("report(xi.item.BEASTCOIN)"));
        uint etui = etuis[0];
        Lua($"local n = GetNPCByID({etui}) n:setPos({c.X - 1.5}, {c.Y}, {c.Z}) n:setStatus(xi.status.NORMAL)");
        Tick(400);
        s.PacketList.Clear();
        Talk(etui);
        var openPackets = new List<BasicPacket>(s.PacketList);
        Check("the first Jade Etui: OPEN_CRATE_GLOW (0x038 open), the common reward (a Beastcoin, addTreasure), bChartChestNum 1", "True/1/1",
              $"{openPackets.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == etui && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "open")}/" +
              $"{InvCount(beastcoin)}/{Lua("report(player:getLocalVar('bChartChestNum'))")}");
        Talk(etui);
        Check("  opened again: nothing (its OPEN_CRATE_GLOW var)", "1/1", $"{InvCount(beastcoin)}/{Lua("report(player:getLocalVar('bChartChestNum'))")}");
        var etuiFade = new List<BasicPacket>();
        for (int i = 0; i < 42; i++) { s.PacketList.Clear(); Tick(400); etuiFade.AddRange(s.PacketList); }
        Check("  15 s on STATUS_DISAPPEAR (kesu), 16 s on removed (DISAPPEAR, its vars reset)", "True/2/0",
              $"{etuiFade.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == etui && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "kesu")}/" +
              $"{Npc(etui).Status}/{NpcLocal(etui, "open")}");
        // the fourth chest (bChartChestNum 3, a GM's): the special roll
        Lua("player:setLocalVar('bChartChestNum', 3)");
        uint etui2 = etuis[1];
        Lua($"local n = GetNPCByID({etui2}) n:setPos({c.X - 1.5}, {c.Y}, {c.Z}) n:setStatus(xi.status.NORMAL)");
        Tick(400);
        Talk(etui2);
        // each treasure is its own item (the pool's give: InsertItem, no stacking)
        Check("the fourth Jade Etui: the Penguin Ring and three Yellow Globes (three stacks of 1)", "1/3", $"{InvCount(PenguinRing)}/{InvCount(YellowGlobe)}");
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        Check("  the ring ends the event (resetEvent): bChartActive 0, LEVEL_RESTRICTION off, the ??? back, the hume and the point hidden", "0/false/0/2/2",
              $"{Lua("report(player:getLocalVar('bChartActive'))")}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))")}/" +
              $"{Npc(bqm).Status}/{Npc(hume).Status}/{Npc(shimmer).Status}");
        Check("  the Jade Etuis all hidden (clearChests)", "True", etuis.All(e => Npc(e).Status == 2));
        for (int i = 0; i < 300; i++) Tick(400);
        Check("  the three minutes' end later: nothing more (the spawner no longer active)", "0/0", $"{Npc(bqm).Status}/{NpcLocal(bqm, "bChartSpawnerID")}");
        Lua($"xi.settings.map.FISHING_ENABLE = {fishingWas}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Pirates' Chart ----
        Console.WriteLine("Pirates' Chart: Valkurm Dunes' ??? (qm4), the chart returned to the sea, the panicked Tarutaru's fifty seconds");
        const ushort PiratesChart = 1874;
        Fixture(db, PirateId, Valkurm, -167.1f, 4.0f, -131.4f, Windurst, War, 75, (PiratesChart, 1));
        CharId = PirateId;
        Login();
        // past zoneutils::AfterZoneIn (4 s after the login: updateCharLevelRestriction lifts a level restriction outside a level-capped zone)
        for (int i = 0; i < 12; i++) Tick(400);
        // qm4's onTrade too does nothing without the server's fishing on
        string fishingWas2 = Lua("report(tostring(xi.settings.map.FISHING_ENABLE))");
        Lua("xi.settings.map.FISHING_ENABLE = true");
        uint VId(string kind, string name) => uint.Parse(Lua($"report(zones[{Valkurm}].{kind}.{name})"));
        string MobMod(uint id, string name) => Lua($"report(GetMobByID({id}):getMobMod(xi.mobMod.{name}))");
        uint pqm = VId("npc", "PIRATE_CHART_QM"), taru = VId("npc", "PIRATE_CHART_TARU"), pshimmer = VId("npc", "SHIMMERING_POINT"), box = VId("npc", "BARNACLED_BOX");
        uint[] buddies = { VId("mob", "BEACH_MONK"), VId("mob", "HEIKE_CRAB"), VId("mob", "HOUU_THE_SHOALWADER") };
        Check("before: the ??? shown, the Tarutaru and the shimmering point hidden, the Barnacled Box CUTSCENE_ONLY (6)", "0/2/2/6",
              $"{Npc(pqm).Status}/{Npc(taru).Status}/{Npc(pshimmer).Status}/{Npc(box).Status}");
        Near(pqm);
        Trade(pqm, (InvItem(PiratesChart).Slot, 1));
        Check("the chart traded: RETURN_TO_SEA, event 14 (0, 0, 0, 3)", $"{Text(Valkurm, "RETURN_TO_SEA")}:{PiratesChart}/{EvStr(pqm, 14, 0, 0, 0, 3)}/14", $"{Special()}/{Ev()}/{Cur()}");
        EventEnd(14, 0, 1);
        var pmusics = Of(0x05F).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 4)}:{BitConverter.ToUInt16(p.Buffer, 6)}").ToList();
        Check("update 0: the chart taken (confirmTrade), pChartActive, the four music slots 136, LEVEL_RESTRICTION 20",
              "0/1/0:136,1:136,2:136,3:136/20", $"{InvCount(PiratesChart)}/{Lua("report(player:getLocalVar('pChartActive'))")}/{string.Join(",", pmusics)}/" +
              Lua("local e = player:getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(e and e:getPower() or 0)"));
        Check("  the ??? and the box hold the spawner, the ??? the party's first member", $"{c.Id}/{c.Id}/{c.Id}",
              $"{NpcLocal(pqm, "pChartSpawnerID")}/{NpcLocal(box, "pChartSpawnerID")}/{NpcLocal(pqm, "pChartMemberID_1")}");
        EventEnd(14, 0);
        Check("event 14 finished: the Tarutaru and the shimmering point shown, the ??? hidden", "0/0/2", $"{Npc(taru).Status}/{Npc(pshimmer).Status}/{Npc(pqm).Status}");
        // rangeChecking's 1 s timer: eventTable's texts (the ??? the speaker, the Tarutaru the last) and the Tarutaru's emotes at the ???
        var tPackets = new List<BasicPacket>();
        bool chillSeen = false;
        int chill = Text(Valkurm, "NO_LONGER_FEEL_CHILL");
        for (int i = 0; i < 160 && !Mob(buddies[0]).IsSpawned; i++)
        {
            if (i == 30)
            {
                // a dozen yalms off the ???: the chill message once, then back
                var q = Npc(pqm);
                s.PacketList.Clear();
                MoveTo(q.X + 12.0f, q.Y, q.Z);
                for (int j = 0; j < 4; j++) Tick(400);
                chillSeen = SpecialOf(s.PacketList) == $"{chill}:0";
                tPackets.AddRange(s.PacketList);
                s.PacketList.Clear();
                MoveTo(q.X + 1.5f, q.Y, q.Z);
                i += 4;
            }
            s.PacketList.Clear();
            Tick(400);
            tPackets.AddRange(s.PacketList);
        }
        Check("  2 s on: the shimmering point's SHIMMER (0x038 efon)", "True",
              tPackets.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == pshimmer && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "efon"));
        int rot = Text(Valkurm, "RIGHT_OVER_THERE_POINT");
        // the ???'s showText (0x02A) and the Tarutaru's messageText (0x036)
        string talks = string.Join(",", tPackets.Where(p => (Opcode(p) == 0x02A && BitConverter.ToUInt32(p.Buffer, 4) == pqm) || (Opcode(p) == 0x036 && BitConverter.ToUInt32(p.Buffer, 4) == taru))
                                                  .Select(p => Opcode(p) == 0x02A ? $"qm:{(BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) - rot}" : $"taru:{(BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF) - rot}"));
        Check("  the texts: RIGHT_OVER_THERE_POINT + 0 to 4 from the ??? (showText), + 5 from the Tarutaru (messageText)", "qm:0,qm:1,qm:2,qm:3,qm:4,taru:5", talks);
        string pemotes = string.Join(",", tPackets.Where(p => Opcode(p) == 0x05A && BitConverter.ToUInt32(p.Buffer, 4) == taru)
                                                     .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 16)}:{(BitConverter.ToUInt32(p.Buffer, 8) == pqm ? "qm" : "?")}"));
        Check("  the Tarutaru's emotes at the ???: POINT, then PANIC three times", $"{Lua("report(xi.emote.POINT)")}:qm,{string.Join(",", Enumerable.Repeat(Lua("report(xi.emote.PANIC)") + ":qm", 3))}", pemotes);
        Check("  the Tarutaru's EFFECT_DEATH (0x038 dead)", "True",
              tPackets.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == taru && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "dead"));
        Check("  a dozen yalms off: NO_LONGER_FEEL_CHILL", "True", chillSeen);
        uint lookupKey = pqm >> 16;
        Check("fifty seconds on, in range: the three pirates up and claimed, the Tarutaru and the point hidden", "True/True/2/2",
              $"{buddies.All(b => Mob(b).IsSpawned && Mob(b).IsAlive)}/{buddies.All(b => Mob(b).OwnerId == c.Id)}/{Npc(taru).Status}/{Npc(pshimmer).Status}");
        Check("  xi.confrontation.start: CONFRONTATION (power the ???'s id >> 16) on the char and on the three", $"{lookupKey}/True",
              $"{Lua("local e = player:getStatusEffect(xi.effect.CONFRONTATION) report(e and e:getPower() or 0)")}/" +
              buddies.All(b => Lua($"local e = GetMobByID({b}):getStatusEffect(xi.effect.CONFRONTATION) report(e and e:getPower() or 0)") == lookupKey.ToString()));
        Check("  onMobSpawn: EXP_BONUS -100, NO_DROPS, GIL_MAX -1, IDLE_DESPAWN 60", "-100/1/-1/60",
              $"{MobMod(buddies[0], "EXP_BONUS")}/{MobMod(buddies[0], "NO_DROPS")}/{MobMod(buddies[0], "GIL_MAX")}/{MobMod(buddies[0], "IDLE_DESPAWN")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Pirates' Chart: the three pirates killed, the Barnacled Box where the last fell, its rewards");
        // a GM's strength for the char's swings at level 20 (bare-handed, they mostly do 0 against the pirates' defense)
        Lua("player:addMod(xi.mod.STR, 200)");
        Kill(Mob(buddies[0]));
        Check("the Beach Monk's 2-hour (HUNDRED_FISTS under half HP: usedTwoHour), killed: the box still not shown (myBuddiesAreDead false)", "1/6",
              $"{MobLocal(buddies[0], "usedTwoHour")}/{Npc(box).Status}");
        Kill(Mob(buddies[1]));
        var last = Mob(buddies[2]);
        Kill(last);
        Lua("player:delMod(xi.mod.STR, 200)");
        float lx = last.X, lz = last.Z;
        Check("the last one killed: the box teleported to it and shown (open 0), spawnedChest", $"0/{lx:F1}/{lz:F1}/1",
              $"{Npc(box).Status}/{Npc(box).X:F1}/{Npc(box).Z:F1}/{MobLocal(buddies[2], "spawnedChest")}");
        Check("  the confrontation won (its DEATH listeners' check): CONFRONTATION off, the lookup gone", "false/nil",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.CONFRONTATION)))")}/{Lua($"report(tostring(xi.confrontation.lookup[{lookupKey}]))")}");
        Check("  LEVEL_RESTRICTION still on (until the box)", "true", Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))"));
        Lua("player:setUnkillable(false)");
        // every loot group's roll its low end (math.randomInt(1, max): 1): each group's first item
        ushort[] loot = new[] { "CORAL_FRAGMENT", "ARROWWOOD_LOG", "FUSCINA", "ALBATROSS_RING" }.Select(n => ushort.Parse(Lua($"report(xi.item.{n})"))).ToArray();
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end math.randomInt = function(a, b) return a end");
        Near(box);
        Talk(box);
        var boxPackets = new List<BasicPacket>(s.PacketList);
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        Check("the box: OPEN_CRATE_GLOW (0x038 open), the four groups' first items (addTreasure), the Albatross Ring the last", "True/1,1,1,1",
              $"{boxPackets.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == box && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "open")}/" +
              string.Join(",", loot.Select(InvCount)));
        var rmusics = boxPackets.Where(p => Opcode(p) == 0x05F).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 4)}:{BitConverter.ToUInt16(p.Buffer, 6)}").ToList();
        Check("  resetEvent: LEVEL_RESTRICTION off, the music back (0, 0, 101, 102), pChartActive 0, the ??? shown", "false/0:0,1:0,2:101,3:102/0/0",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))")}/{string.Join(",", rmusics)}/{Lua("report(player:getLocalVar('pChartActive'))")}/{Npc(pqm).Status}");
        Talk(box);
        Check("  opened again: nothing more (and not the spawner's any more: resetLocalVars)", "1", InvCount(loot[3]));
        var boxFade = new List<BasicPacket>();
        for (int i = 0; i < 42; i++) { s.PacketList.Clear(); Tick(400); boxFade.AddRange(s.PacketList); }
        Check("  15 s on STATUS_DISAPPEAR (kesu), 16 s on hidden", "True/2",
              $"{boxFade.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == box && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "kesu")}/{Npc(box).Status}");
        Lua($"xi.settings.map.FISHING_ENABLE = {fishingWas2}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- the Fish Ranking contest ----
        Console.WriteLine("Fishing contest: a GM's new contest (InitNewFishingContest, SetContestParameters) to ACCEPTING; Chenon in Selbina");
        const ushort GigantSquid = 4474, PelicanRing = 15554;
        Fixture(db, ContestId, Selbina, -12.0f, -7.3f, 9.5f, Windurst, War, 75);
        CharId = ContestId;
        Login();
        // the criteria and measure rolls (xirand) their low end, the contest's fish, measure and criteria set by the GM: the Gigant
        // Squid, greatest, size + weight
        zone.ContestRandom = (min, max) => min;
        Lua($"InitNewFishingContest() SetContestParameters({GigantSquid}, 0, 2)");
        string Contest(string field) => Lua($"report(GetFishingContest().{field})");
        void Progress()
        {
            now = (long.Parse(Contest("changetime")) - earth0) * 1000 + 1_000_000 + 1000;
            Lua("ProgressFishingContest()");
        }
        Progress();
        Progress();
        Check("the GM's contest: ACCEPTING, the Gigant Squid, size + weight, greatest, 14 days to the results", $"2/{GigantSquid}/2/0/{EarthNow() + 1209600}",
              $"{Contest("status")}/{Contest("fishid")}/{Contest("criteria")}/{Contest("measure")}/{Contest("changetime")}");
        uint chenon = NpcId("Chenon");
        Near(chenon);
        Talk(chenon);
        Check("Chenon: event 10006 (ACCEPTING, the fish, the criteria, the measure)", $"{EvStr(chenon, 10006, 2, GigantSquid, 2, 0)}/10006", $"{Ev()}/{Cur()}");
        EventEnd(10006, 150, 1);
        long left = long.Parse(Contest("changetime")) - EarthNow();
        Check("  option 150: updateEvent (ACCEPTING, the fish, 2, 0, no reward, the days / hours / minutes left)",
              System.Text.RegularExpressions.Regex.Replace($"2,{GigantSquid},2,0,0,{left / 86400},{left / 3600 % 24},{left / 60 % 60}", "(,0)+$", ""), Upds());
        EventEnd(10006, 147, 1);
        Check("  option 147: the award history (none: 0)", "1/", $"{Of(0x05C).Count}/{Upds()}");
        EventEnd(10006, 149, 1);
        Check("  option 149: the rules (ACCEPTING, the fish, 2, 0)", $"2,{GigantSquid},2", Upds());
        EventEnd(10006, 151, 1);
        Check("  option 151: the entry (no score yet)", $"2,{GigantSquid},2", Upds());
        EventEnd(10006, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Fishing contest: a Gigant Squid (size 120, weight 300) entered, its fee, the ranked fish back, withdrawn, another entered");
        // a GM's caught squid: the fishing exdata (Fishing.Live drives the catch itself)
        Lua($"local i = player:addItem({{ id = {GigantSquid}, quantity = 1 }}) i:setExData({{ size = 120, weight = 300 }})");
        Trade(chenon, (InvItem(GigantSquid).Slot, 1));
        Check("the squid traded: event 10007 (its score 420 in the 6th, the current 0), the size and weight held", $"{EvStr(chenon, 10007, 0, 0, 0, 0, 0, 420)}/120/300",
              $"{Ev()}/{Lua("report(player:getLocalVar('[FishContest]Length'))")}/{Lua("report(player:getLocalVar('[FishContest]Weight'))")}");
        EventEnd(10007, 144, 1);
        Check("  option 144 (the 500 gil there): an empty update", "1/", $"{Of(0x05C).Count}/{Upds()}");
        EventEnd(10007, 145);
        var ranked = InvItem(GigantSquid);
        Check("  finished with 145: ITEM_OBTAINED, the fish back ranked (its size and weight kept), 500 gil taken, the entry's score 420",
              $"{Text(Selbina, "ITEM_OBTAINED")}:{GigantSquid}/1/true,120,300/9500/420",
              $"{Special()}/{InvCount(GigantSquid)}/{Lua($"local i = player:findItem({GigantSquid}) local e = i:getExData() report(tostring(e.isRanked) .. ',' .. e.size .. ',' .. e.weight)")}/" +
              $"{Lua("report(player:getGil())")}/{Lua("report(player:getContestScore())")}");
        Check("  the entry's row (fishing_contest_entries: the score, War 75)", "420/1/75",
              DbScalar(db, "SELECT CONCAT_WS('/', score, mjob, mlevel) FROM fishing_contest_entries WHERE charid = ?"));
        Trade(chenon, (InvItem(GigantSquid).Slot, 1));
        Check("the ranked squid again: event 10007 (already ranked in the 5th)", EvStr(chenon, 10007, 0, 0, 0, 0, 1), Ev());
        EventEnd(10007, 145);
        Check("  finished: nothing (no length held), the fish kept, no fee", $"1/9500/420", $"{InvCount(GigantSquid)}/{Lua("report(player:getGil())")}/{Lua("report(player:getContestScore())")}");
        Talk(chenon);
        EventEnd(10006, 151, 1);
        Check("Chenon's option 151: the entry's score (420 in the 6th)", $"2,{GigantSquid},2,0,0,420", Upds());
        EventEnd(10006, 152, 1);
        Check("  option 152: withdrawn (withdrawContestFish: the score and the row gone)", "0/-", $"{Lua("report(player:getContestScore())")}/{DbScalar(db, "SELECT score FROM fishing_contest_entries WHERE charid = ?")}");
        EventEnd(10006, 0);
        var rankedSlot = InvItem(GigantSquid).Slot;
        Lua($"local i = player:addItem({{ id = {GigantSquid}, quantity = 1 }}) i:setExData({{ size = 150, weight = 310 }})");
        // the new one: not the ranked one's slot
        var fresh = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == GigantSquid && i.Quantity > 0 && i.Slot != rankedSlot);
        Trade(chenon, (fresh.Slot, 1));
        EventEnd(10007, 145);
        Check("another squid (150 + 310) entered: the score 460, 500 gil more", "460/9000", $"{Lua("report(player:getContestScore())")}/{Lua("report(player:getGil())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Fishing contest: the results (ProgressFishingContest: RELEASING, PRESENTING), the award claimed at Chenon");
        Progress();
        Check("RELEASING (ScoreContest): rank 1, share 1; the ranking history's first place (char_fishing_contest_history)", "3/1,1/1",
              $"{Contest("status")}/{Lua("local r = player:getContestRewardStatus() report(r.rank .. ',' .. r.share)")}/" +
              DbScalar(db, "SELECT contest_rank_1 FROM char_fishing_contest_history WHERE charid = ?"));
        Talk(chenon);
        EventEnd(10006, 149, 1);
        Check("  Chenon while RELEASING: no rules update (149)", "", Upds());
        EventEnd(10006, 149);
        Check("  finished 149: no award before PRESENTING", "9000/0", $"{Lua("report(player:getGil())")}/{InvCount(PelicanRing)}");
        Progress();
        Talk(chenon);
        Check("PRESENTING: event 10006 (4, the fish, 2, 0)", EvStr(chenon, 10006, 4, GigantSquid, 2, 0), Ev());
        EventEnd(10006, 150, 1);
        left = long.Parse(Contest("changetime")) - EarthNow();
        Check("  option 150: the award ready (1 in the 5th), the time to the hiatus", System.Text.RegularExpressions.Regex.Replace($"4,{GigantSquid},2,0,1,{left / 86400},{left / 3600 % 24},{left / 60 % 60}", "(,0)+$", ""), Upds());
        EventEnd(10006, 147, 1);
        Check("  option 147: the award history (one first place: 1)", "0,0,0,0,0,1", Upds());
        s.PacketList.Clear();
        EventEnd(10006, 149);
        Check("  finished 149: the first place's award (giveReward: the Pelican Ring, 50000 gil, Gold Hook), claimed",
              $"1/59000/true/0",
              $"{InvCount(PelicanRing)}/{Lua("report(player:getGil())")}/{Lua("report(tostring(player:hasTitle(xi.title.GOLD_HOOK)))")}/{Lua("report(player:getContestRewardStatus().rank)")}");
        Check("  the messages: ITEM_OBTAINED (the ring), GIL_OBTAINED", $"{Text(Selbina, "ITEM_OBTAINED")}:{PelicanRing},{Text(Selbina, "GIL_OBTAINED")}:50000", Special());
        Talk(chenon);
        EventEnd(10006, 149);
        Check("  again: nothing more (claimed)", "1/59000", $"{InvCount(PelicanRing)}/{Lua("report(player:getGil())")}");
        zone.ContestRandom = (min, max) => Random.Shared.Next(min, max);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Expeditionary Force ----
        Console.WriteLine("Expeditionary Force: Valkurm Dunes' Beastmen's Banner (Zulkheim) for a nation that neither holds nor is allied with its owner");
        // the nation: not Zulkheim's owner, not allied with it (xi.conquest.areAllies)
        byte efNation = byte.Parse(Lua("local owner = GetRegionOwner(xi.region.ZULKHEIM) for n = 0, 2 do " +
                                       "if n ~= owner and not xi.conquest.areAllies(n, owner) then report(n) return end end report(255)"));
        Check("a nation that may fight there (not the owner, not allied)", "True", efNation <= 2);
        if (efNation > 2) efNation = Windurst;
        Fixture(db, EfId, Valkurm, -114.0f, 4.0f, -113.6f, efNation, War, 75);
        CharId = EfId;
        Login();
        for (int i = 0; i < 12; i++) Tick(400);
        uint banner = VId("npc", "BEASTMENS_BANNER");
        // a GM's re-init with its roll the top end: the beach's banner (the 5th, the Metaquadavs) unless it stood there already
        Lua("if not _verifyRandomInt then _verifyRandomInt = math.randomInt end math.randomInt = function(a, b) return b end " +
            $"xi.expeditionaryForce.initZone(GetNPCByID({banner}):getZone()) math.randomInt = _verifyRandomInt _verifyRandomInt = nil");
        string bannerIdx = NpcLocal(banner, "BannerIndex");
        Check("initZone: the banner IDLE, shown, at its option's position", "0/0/True",
              $"{NpcLocal(banner, "State")}/{Npc(banner).Status}/{Math.Abs(Npc(banner).X - (bannerIdx == "5" ? -116.204f : -352.679f)) < 0.01f}");
        Near(banner);
        Talk(banner);
        int bannerText = Text(Valkurm, "BEASTMEN_BANNER");
        Check("the banner without Zulkheim's insignia: BEASTMEN_BANNER, nothing more", $"{bannerText}:0/0/false", $"{Special()}/{NpcLocal(banner, "State")}/" +
              Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))"));
        Lua("player:addKeyItem(xi.keyItem.ZULKHEIM_EF_INSIGNIA)");
        var ipc = new List<XI.Ipc.IIpcMessage>();
        zone.SendIpc = m => ipc.Add(m);
        Talk(banner);
        uint[] efMobs = Lua($"local t = {{}} for _, id in ipairs({{ {string.Join(", ", new[] { "BLACK_MAGE", "DARK_KNIGHT", "PALADIN", "RED_MAGE", "THIEF", "WARRIOR", "WHITE_MAGE" }.Select(j => $"zones[{Valkurm}].mob.METAQUADAV_{j}"))} }}) do " +
                            "if GetMobByID(id):isSpawned() then table.insert(t, id) end end report(table.concat(t, ' '))")
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(uint.Parse).ToArray();
        Check("with the insignia: BEASTMEN_BANNER_CURSE, LEVEL_RESTRICTION 30 (subPower 1, 15 minutes), the banner ACTIVE",
              $"{Text(Valkurm, "BEASTMEN_BANNER_CURSE")}:0/30,1,900/1",
              $"{Special()}/{Lua("local e = player:getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(e:getPower() .. ',' .. e:getSubPower() .. ',' .. e:getDuration() / 1000)")}/{NpcLocal(banner, "State")}");
        Check("  the effect's flags: ON_ZONE and CONFRONTATION", "true",
              Lua("local e = player:getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(tostring(bit.band(e:getEffectFlags(), xi.effectFlag.ON_ZONE) ~= 0 and bit.band(e:getEffectFlags(), xi.effectFlag.CONFRONTATION) ~= 0))"));
        Check("  spawnBattleNMs: four of the option's Metaquadavs up within 7.5 yalms of the banner, claimed, each LEVEL_RESTRICTION 30 (CONFRONTATION)",
              "4/True/True/True",
              $"{efMobs.Length}/{efMobs.All(m => { var b = Npc(banner); float dx = Mob(m).X - b.X, dz = Mob(m).Z - b.Z; return MathF.Sqrt(dx * dx + dz * dz) <= 7.6f; })}/" +
              $"{efMobs.All(m => Mob(m).OwnerId == c.Id)}/" +
              efMobs.All(m => Lua($"local e = GetMobByID({m}):getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(tostring(e ~= nil and e:getPower() == 30 and bit.band(e:getEffectFlags(), xi.effectFlag.CONFRONTATION) ~= 0))") == "true"));
        Talk(banner);
        Check("the banner ACTIVE again: the restriction put on afresh, BEASTMEN_BANNER_CURSE", $"{Text(Valkurm, "BEASTMEN_BANNER_CURSE")}:0/30", $"{Special()}/" +
              Lua("local e = player:getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(e and e:getPower() or 0)"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Expeditionary Force: the four killed: the influence and kills (IPC), the messages, the title, the participation; the banner cleared");
        // the claims: ClaimMob gave each the char in turn, the earlier three's declaim time tapped (CMobController's 3 s stale
        // claim drops them, as in the C++); the Metaquadavs' cures (their White and Red Mages) and their two-hours (the
        // job_special mixin's Benediction, Invincible, Perfect Dodge at low HP) kept off by a GM
        foreach (var m in efMobs) Lua($"local m = GetMobByID({m}) m:setMagicCastingEnabled(false) m:setLocalVar('[jobSpecial]cooldown', GetSystemTime() + 100000)");
        string efRank = Lua($"report(GetNationRank({efNation}))");
        int points = efRank switch { "2" => 15000, "3" => 20000, _ => 10000 };
        var efPackets = new List<BasicPacket>();
        Lua("player:addMod(xi.mod.STR, 200)");
        for (int k = 0; k < efMobs.Length; k++)
        {
            efPackets.AddRange(Kill(Mob(efMobs[k])));
            if (k == 0)
                Check("the first killed: the banner still ACTIVE", "1", NpcLocal(banner, "State"));
        }
        Lua("player:delMod(xi.mod.STR, 200)");
        var conquest = ipc.OfType<XI.Ipc.ConquestEvent>().ToList();
        string influence = string.Join(",", conquest.Where(e => e.TypeField == (byte)XI.Ipc.ConquestMessage.M2W_AddInfluencePoints)
                                                    .Select(e => { var p = XI.World.ConquestAddInfluencePoints.FromBytes(e.Payload); return $"{p.Points}:{p.Nation}:{p.Region}"; }));
        string kills = string.Join(",", conquest.Where(e => e.TypeField == (byte)XI.Ipc.ConquestMessage.M2W_AddMobKills)
                                                .Select(e => { var p = XI.World.ConquestAddCounter.FromBytes(e.Payload); return $"{p.Count}:{p.Region}"; }));
        Check($"each kill: gainConquestInfluence (rank {efRank}: {points}) and addConquestMobKills (the kill weight 10 x 2) for Zulkheim",
              $"{string.Join(",", Enumerable.Repeat($"{points}:{efNation}:1", 4))}/{string.Join(",", Enumerable.Repeat("20:1", 4))}", $"{influence}/{kills}");
        int efKill = Text(Valkurm, "EXP_FORCE_KILL_SANDORIA") + efNation, regionPts = Text(Valkurm, "REGION_POINTS_SANDORIA") + efNation;
        Check("  the zone's players: EXP_FORCE_KILL (the nation's, messageText) each kill; REGION_POINTS (the nation's) each kill", "4/4",
              $"{efPackets.Count(p => Opcode(p) == 0x036 && (BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF) == efKill)}/" +
              $"{efPackets.Count(p => Opcode(p) == 0x02A && (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) == regionPts)}");
        Check("  the title EXPEDITIONARY_TROOPER, the participation bit for Zulkheim (1 << 1)", "true/2",
              $"{Lua("report(tostring(player:hasTitle(xi.title.EXPEDITIONARY_TROOPER)))")}/{Var("[ExpForce]Participation")}");
        Check("the last killed: the banner CLEARED", "2", NpcLocal(banner, "State"));
        zone.SendIpc = _ => { };
        Lua("player:setUnkillable(false)");
        Near(banner);
        Talk(banner);
        Check("the banner CLEARED: the restriction lifted, BEASTMEN_BANNER_LIFTED", $"{Text(Valkurm, "BEASTMEN_BANNER_LIFTED")}:0/false",
              $"{Special()}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))")}");
        for (int i = 0; i < 160 && NpcLocal(banner, "State") != "3"; i++) Tick(400);
        Check("  60 s on (hideBanner): HIDDEN, the banner gone", "3/2", $"{NpcLocal(banner, "State")}/{Npc(banner).Status}");
        Talk(banner);
        Check("  the hidden banner: nothing", "", Special());
        for (int i = 0; i < 760 && NpcLocal(banner, "State") != "0"; i++) Tick(400);
        Check("  5 minutes on: initZone again (IDLE, shown, at another option)", $"0/0/True",
              $"{NpcLocal(banner, "State")}/{Npc(banner).Status}/{NpcLocal(banner, "BannerIndex") != bannerIdx}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- the Harvest Festival (2005) ----
        Console.WriteLine("Harvest Festival: the GM's settings (HALLOWEEN_2005, all year), Bastok Mines' costumes, treats traded to Faustin");
        const ushort AcornCookie = 4510, BubbleChocolate = 4496, GingerCookie = 4394, PumpkinHead = 13916, HorrorHead = 13917, FireCrystal = 4096;
        string hw2005Was = Lua("report(xi.settings.main.HALLOWEEN_2005)"), hwYearWas = Lua("report(xi.settings.main.HALLOWEEN_YEAR_ROUND)");
        Lua("xi.settings.main.HALLOWEEN_2005 = 0 xi.settings.main.HALLOWEEN_YEAR_ROUND = 0");
        uint faustin = uint.Parse(Lua($"for id, _ in pairs(zones[{BastokMines}].npc.HALLOWEEN_SKINS) do if GetNPCByID(id):getName() == 'Faustin' then report(id) end end"));
        Check("the settings off: isHalloweenEnabled 0 (in season or not: HALLOWEEN_2005 picks the edition)", "0",
              Lua("report(xi.events.harvestFestival.isHalloweenEnabled())"));
        Lua("xi.settings.main.HALLOWEEN_2005 = 1 xi.settings.main.HALLOWEEN_YEAR_ROUND = 1");
        Lua($"xi.events.harvestFestival.applyHalloweenNpcCostumes({BastokMines})");
        Check("the GM's settings on: isHalloweenEnabled 1; applyHalloweenNpcCostumes: Faustin's skin 41, Mille's 43", "1/41/43",
              $"{Lua("report(xi.events.harvestFestival.isHalloweenEnabled())")}/{Lua($"report(GetNPCByID({faustin}):getModelId())")}/" +
              Lua($"report(GetNPCByID({faustin + 2}):getModelId())"));
        WarpTo(BastokMines, 63.5f, 0.0f, -96.0f);
        Lua($"player:addItem({AcornCookie}, 3) player:addItem({BubbleChocolate}, 1) player:addItem({GingerCookie}, 1) player:addItem({FireCrystal}, 1)");
        // the dice: the reward roll (math.randomInt(1, 3) < 2) a GM's, every other roll its low end (the first of the rewards,
        // the costumes' first ids, the first costume)
        Lua("_verifyHwRoll = 1 if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
            "math.randomInt = function(a, b) if a == 1 and b == 3 then return _verifyHwRoll end return a end");
        Near(faustin);
        Trade(faustin, (InvItem(AcornCookie).Slot, 1));
        Check("an Acorn Cookie, the reward roll 1: HERE_TAKE_THIS, the first reward (a Pumpkin Head), the cookie taken, its bit in harvestFestTreats",
              $"{Text(BastokMines, "HERE_TAKE_THIS")}:0,{Text(BastokMines, "ITEM_OBTAINED")}:{PumpkinHead}/1/2/2",
              $"{Special()}/{InvCount(PumpkinHead)}/{InvItem(AcornCookie)?.Quantity ?? 0}/{Var("harvestFestTreats")}");
        Lua("_verifyHwRoll = 3");
        Trade(faustin, (InvItem(AcornCookie).Slot, 1));
        Check("another Acorn Cookie, the roll 3: already traded: THANK_YOU, no costume", $"{Text(BastokMines, "THANK_YOU")}:0/1/false",
              $"{Special()}/{InvItem(AcornCookie)?.Quantity ?? 0}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.COSTUME)))")}");
        Trade(faustin, (InvItem(BubbleChocolate).Slot, 1));
        Check("a Bubble Chocolate, the roll 3: a costume (the first: a Quadav, 644) for an hour, THANK_YOU_TREAT, its bit (8) added",
              $"644,3600/644/{Text(BastokMines, "THANK_YOU_TREAT")}:0/10/0",
              $"{Lua("local e = player:getStatusEffect(xi.effect.COSTUME) report(e:getPower() .. ',' .. e:getDuration() / 1000)")}/{Lua("report(player:getCostume())")}/" +
              $"{Special()}/{Var("harvestFestTreats")}/{InvCount(BubbleChocolate)}");
        // the Pumpkin Head worn (GP_CLI_COMMAND_EQUIP_SET), then a Ginger Cookie with the reward roll: the HQ
        Send(0x050, 8, b => { b[4] = InvItem(PumpkinHead).Slot; b[5] = 4; b[6] = 0; });
        Lua("_verifyHwRoll = 1");
        Trade(faustin, (InvItem(GingerCookie).Slot, 1));
        Check("the Pumpkin Head worn, a Ginger Cookie with the reward roll: the Horror Head (halloweenItemsCheck's upgrade), its bit (32)",
              $"{PumpkinHead}/{Text(BastokMines, "HERE_TAKE_THIS")}:0,{Text(BastokMines, "ITEM_OBTAINED")}:{HorrorHead}/1/42",
              $"{Lua("report(player:getEquipID(xi.slot.HEAD))")}/{Special()}/{InvCount(HorrorHead)}/{Var("harvestFestTreats")}");
        Trade(faustin, (InvItem(FireCrystal).Slot, 1));
        Check("a Fire Crystal (no treat): nothing, the crystal kept", "/1", $"{Special()}/{InvCount(FireCrystal)}");
        Lua("xi.settings.main.HALLOWEEN_2005 = 0 xi.settings.main.HALLOWEEN_YEAR_ROUND = 0");
        Trade(faustin, (InvItem(AcornCookie).Slot, 1));
        Check("the settings off again (out of season): the treat refused, kept", "/1", $"{Special()}/{InvItem(AcornCookie)?.Quantity ?? 0}");
        Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end _verifyHwRoll = nil");
        Lua($"xi.settings.main.HALLOWEEN_2005 = {hw2005Was} xi.settings.main.HALLOWEEN_YEAR_ROUND = {hwYearWas}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- the Festive Moogle ----
        Console.WriteLine("Festive Moogle: Port Bastok's, the granted items, the Mog Pells and their rewards");
        const ushort GoldMogPell = 8716, Joyeuse = 17652, Ridill = 16555, RemsTale1 = 4064, MoogleCap = 16118, NomadCap = 16119;
        WarpTo(PortBastok, 48.4f, 8.5f, -242.3f);
        uint moogle = NpcId("Festive_Moogle");
        Near(moogle);
        Talk(moogle);
        Check("nothing granted: event 381", $"{EvStr(moogle, 381)}/381", $"{Ev()}/{Cur()}");
        EventEnd(381, 0);
        // a GM's grants: the Moogle Cap and the Nomad Cap
        Lua("player:setCharVar('festiveMoogleMoogleCap', 1) player:setCharVar('festiveMoogleNomadCap', 1)");
        Talk(moogle);
        var grantedParams = Ev().Split(':').Last().Split(',').Select(ushort.Parse).ToList();
        Check("two granted: event 380 with the two caps", $"380/True", $"{Cur()}/{grantedParams.OrderBy(x => x).SequenceEqual(new[] { NomadCap, MoogleCap }.OrderBy(x => x))}");
        EventEnd(380, 1);
        Check("  option 1: the first one given, its var cleared", $"1/0", $"{InvCount(grantedParams[0])}/" +
              Var(grantedParams[0] == MoogleCap ? "festiveMoogleMoogleCap" : "festiveMoogleNomadCap"));
        Talk(moogle);
        Check("  again: event 380 with the other alone", EvStr(moogle, 380, grantedParams[1]), Ev());
        int cancelled = 1 << 30;
        EventEnd(380, (uint)cancelled);
        Check("  cancelled: nothing, its var kept", "0/1", $"{InvCount(grantedParams[1])}/" + Var(grantedParams[1] == MoogleCap ? "festiveMoogleMoogleCap" : "festiveMoogleNomadCap"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Lua($"player:addItem({GoldMogPell}, 1) player:addItem({GoldMogPell}, 1) player:addItem({GoldMogPell}, 1) player:addItem({GoldMogPell}, 1) player:addItem({GoldMogPell}, 1) player:addItem({Joyeuse}, 1)");
        Trade(moogle, (InvItem(GoldMogPell).Slot, 1));
        Check("a Gold Mog Pell: event 439 (0, the pell type 0, the Rare equipment held: the Joyeuse's bit 1, 0, no guild: -1)",
              $"{EvStr(moogle, 439, 0, 0, 2, 0, -1)}/{GoldMogPell}", $"{Ev()}/{Lua("report(player:getLocalVar('tradedPell'))")}");
        EventEnd(439, 2 | 5 << 16);
        Check("  items, the 6th (category 2, item 5): three Rem's Tale chapter 1s, the pell taken", $"3/4",
              $"{c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == RemsTale1).Sum(i => i.Quantity)}/{InvCount(GoldMogPell)}");
        Trade(moogle, (InvItem(GoldMogPell).Slot, 1));
        EventEnd(439, 1 | 0 << 16);
        Check("  equipment, the first (category 1): the Ridill, the pell taken", "1/3", $"{InvCount(Ridill)}/{InvCount(GoldMogPell)}");
        Trade(moogle, (InvItem(GoldMogPell).Slot, 1));
        EventEnd(439, 3 | 1 << 16);
        Check("  currency, imperial standing 50000: IMPERIAL_STANDING_INCREASED (the amount the 1st param), the pell taken",
              $"50000/{Text(PortBastok, "IMPERIAL_STANDING_INCREASED")}:50000/2", $"{Currency("imperial_standing")}/{Special()}/{InvCount(GoldMogPell)}");
        string cpWas = Lua("report(player:getCP())");
        Trade(moogle, (InvItem(GoldMogPell).Slot, 1));
        EventEnd(439, 3 | 0 << 16);
        Check("  currency, conquest points 50000 (addCP): RECEIVED_CONQUEST_POINTS (the amount the 2nd param), the pell taken",
              $"{long.Parse(cpWas) + 50000}/{Text(PortBastok, "RECEIVED_CONQUEST_POINTS")}:0:50000/1", $"{Lua("report(player:getCP())")}/{Special()}/{InvCount(GoldMogPell)}");
        Trade(moogle, (InvItem(GoldMogPell).Slot, 1));
        EventEnd(439, (uint)cancelled);
        Check("  cancelled: ITEM_RETURNED, the pell kept", $"{Text(PortBastok, "ITEM_RETURNED")}:{GoldMogPell}/1", $"{Special()}/{InvCount(GoldMogPell)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
    }
}

// a GM's loaded dice for the engine's rolls: every roll its low end (a hit, a proc, no evasion)
internal sealed class LoadedRandom : Random
{
    public override int Next() => 0;
    public override int Next(int maxValue) => 0;
    public override int Next(int minValue, int maxValue) => minValue;
    public override long NextInt64(long minValue, long maxValue) => minValue;
    public override double NextDouble() => 0.0;
    protected override double Sample() => 0.0;
}
