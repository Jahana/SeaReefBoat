using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Content3.Live -- live gate for the content systems driven as a client drives them through the real Lua
// scripts: Promyvion (scripts/globals/promyvion.lua and Promyvion-Holla: the memory receptacles, their strays, the
// floor portals and their trigger areas), Voidwalker (scripts/globals/voidwalker.lua, voidwalkerpos.lua: Assai
// Nybaem's abyssites, the /heal search, the NM's pop, its kill and the key item upgrade), Zeni Notorious Monsters
// (scripts/globals/znm.lua, znm_data.lua: the Soultrapper's soul plate and its exdata, Ryo, Sanraku's zeni and pop
// items, the ??? pop), the Gobbie Mystery Box (scripts/globals/gobbie_mystery_box.lua), Colonization Reives
// (scripts/globals/colonization_reives.lua, colonization_reive_data.lua) and Domain Invasion's fence
// (scripts/globals/domain_invasion.lua). The 0x00A login, 0x015 moves, 0x01A Talk and Attack, 0x036 trades, 0x037
// item use, 0x050 equips, 0x05B event ends and updates, 0x0E8 /heal, the zone's ticks, and every step's effect: the
// event and message packets, char vars, key items, items and their exdata, currencies, the spawned entities and
// their animations. The real scripts on the real MariaDB (xidb), chars 996071-996076. The walks between the places
// the scripts do not script are GM !pos warps.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static uint CharId = 996071;
    private const uint PromyvionId = 996071, VoidwalkerId = 996072, ZnmId = 996073, GobbieId = 996074, ReiveId = 996075, DomainId = 996076;
    private static readonly uint[] CharIds = { 996071, 996072, 996073, 996074, 996075, 996076 };
    private const ushort PromyvionHolla = 16, HallOfTransference = 14, RuLude = 243, LaTheine = 102, Whitegate = 50, Wajaom = 51, WindurstWoods = 241,
                         Ceizak = 261, EschaRuAun = 289;
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
        var r = Sql(db, q);
        return r != null && r.Next() ? r.Get<string>(0u) ?? "null" : "-";
    }

    private static string DbRow(IDatabase db, string cols, string from)
    {
        var r = Sql(db, $"SELECT {cols} {from}", BoundValue.Of(CharId));
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
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

    // the server variables the scripts write (Sanraku's interest, fauna and trades, the pop item prices): kept as they
    // were before the run, cleared for it, restored after
    private static readonly string[] ServerVars =
    {
        "[ZNM][Sanraku]Interest", "[ZNM][Sanraku]Fauna", "[ZNM][Sanraku]Trades", "[ZNM][Vulpangue]PopCost",
    };
    private static readonly Dictionary<string, (int Value, int Expiry)?> _serverVarsBefore = new Dictionary<string, (int, int)?>();

    private static void SaveServerVars(IDatabase db)
    {
        foreach (var name in ServerVars)
        {
            var r = Sql(db, "SELECT value, expiry FROM server_variables WHERE name = ?", BoundValue.Of(name));
            _serverVarsBefore[name] = r != null && r.Next() ? (r.Get<int>("value"), r.Get<int>("expiry")) : null;
            Sql(db, "DELETE FROM server_variables WHERE name = ?", BoundValue.Of(name));
        }
    }

    private static void RestoreServerVars(IDatabase db)
    {
        foreach (var (name, before) in _serverVarsBefore)
        {
            Sql(db, "DELETE FROM server_variables WHERE name = ?", BoundValue.Of(name));
            if (before is { } v)
                Sql(db, "INSERT INTO server_variables(name, value, expiry) VALUES (?, ?, ?)", BoundValue.Of(name), BoundValue.Of(v.Value), BoundValue.Of(v.Expiry));
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
            BoundValue.Of(id), BoundValue.Of($"content3live{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Cthreelive{id % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
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

        Fixture(db, PromyvionId, PromyvionHolla, 0.0f, 0.0f, 0.0f, Windurst, War, 75, (TealMemosphere, 1));

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
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_content3");
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

        // ---- Promyvion ----
        CharId = PromyvionId;
        Console.WriteLine("Promyvion: into Promyvion-Holla, the zone's portals and its receptacles");
        Login();
        Check("onZoneIn from 0, 0, 0: setPos to floor 1 (92.033, 0, 80.380)", $"{PromyvionHolla}/92.0/80.4", $"{c.Zone}/{c.X:F1}/{c.Z:F1}");
        Check("afterZoneIn: ENABLE_COP_ZONE_CAP 0: no LEVEL_RESTRICTION", "false", Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))"));
        uint stream = uint.Parse(Lua($"report(zones[{PromyvionHolla}].npc.MEMORY_STREAM_OFFSET)"));
        var groups = new[] { new[] { 7 }, new[] { 4, 6 }, new[] { 3, 5 }, new[] { 0, 1, 2 }, new[] { 8, 9, 10 } };
        Check("onInitialize's setupInitialPortals: one portal of each of the five groups chosen ([Portal]Chosen 1)", "1,1,1,1,1",
              string.Join(",", groups.Select(g => g.Count(o => NpcLocal(stream + (uint)o, "[Portal]Chosen") == "1"))));
        Check("  every memory stream closed", "True", Enumerable.Range(0, 11).All(o => Npc(stream + (uint)o).Animation == 9));
        var receptacles = Lua($"report(table.concat(zones[{PromyvionHolla}].mob.MEMORY_RECEPTACLE_TABLE, ' '))").Split(' ').Select(uint.Parse).ToList();
        uint rec1 = receptacles[0];
        var r1 = Mob(rec1);
        Check("11 receptacles, the first 16842781 up at (-40, -1, 200), animationSub 4", "11/16842781/True/-40/200/4",
              $"{receptacles.Count}/{rec1}/{r1.IsSpawned && r1.IsAlive}/{MathF.Round(r1.X)}/{MathF.Round(r1.Z)}/{r1.AnimationSub}");
        Check("  receptacleOnMobInitialize: no auto attack, DESPAWN_TIME_REDUCTION 15", "False/15",
              $"{r1.AutoAttackEnabled}/{Lua($"report(GetMobByID({rec1}):getMod(xi.mod.DESPAWN_TIME_REDUCTION))")}");
        long idle = long.Parse(MobLocal(rec1, "[Stray]CooldownIdle"));
        Check("  receptacleOnMobSpawn: [Stray]CooldownIdle 2 to 6 minutes on, [Stray]CooldownFight 0", "True/0",
              $"{idle >= EarthNow() + 120 - 5 && idle <= EarthNow() + 360}/{MobLocal(rec1, "[Stray]CooldownFight")}");
        var deco = Npc(rec1 - 1);
        Check("  its decoration (the npc before it): updateToEntireZone NORMAL, NONE", "0/0", $"{deco.Status}/{deco.Animation}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Promyvion: the idle receptacle pops a stray (onMobRoam once [Stray]CooldownIdle passes)");
        MoveTo(-40.0f, -1.0f, 230.0f);
        uint stray1 = rec1 + 1;
        var subs = new List<byte> { r1.AnimationSub };
        long strayAt = 0, raisedAt = 0;
        for (int i = 0; i < 2000 && (strayAt == 0 || now < strayAt + 4000); i++)
        {
            Tick(250);
            if (subs[^1] != r1.AnimationSub) subs.Add(r1.AnimationSub);
            if (raisedAt == 0 && r1.AnimationSub == 5) raisedAt = now;
            if (strayAt == 0 && Mob(stray1).IsSpawned) strayAt = now;
        }
        Check("the receptacle raised (5), lowered (6), reset (4) as the stray pops 875 ms after the raise", "4,5,6,4/True",
              $"{string.Join(",", subs)}/{strayAt - raisedAt is >= 875 and <= 1250}");
        Check("  the raise once the idle cooldown passed, a new one 2 to 6 minutes on", "True/True",
              $"{raisedAt / 1000 - 1000 + earth0 >= idle}/{long.Parse(MobLocal(rec1, "[Stray]CooldownIdle")) >= EarthNow() + 110}");
        var s1 = Mob(stray1);
        // receptacleOnMobRoam sets [Stray]RangedEnmity on the stray right after spawnStray, 875 ms before its spawn:
        // CBaseEntity::Spawn's ResetLocalVars wipes it (in C++ too: a script order bug)
        Check("  the first of its strays (16842782): up, [Stray]RangedEnmity wiped by its spawn (0)", "True/0", $"{s1.IsSpawned && s1.IsAlive}/{MobLocal(stray1, "[Stray]RangedEnmity")}");
        int[][] strayTable = { new[] { 1108, 14, 40 }, new[] { 1110, 13, 50 }, new[] { 1110, 14, 50 }, new[] { 1107, 14, 40 }, new[] { 1107, 13, 40 },
                               new[] { 1106, 14, 40 }, new[] { 1108, 13, 40 }, new[] { 1106, 13, 40 } };
        int strayElement = Array.FindIndex(strayTable, e => e[0] == s1.Look[1] && e[1] == s1.AnimationSub && e[2] == s1.Speed) + 1;
        Check("  emptyOnMobSpawn: a model, animationSub and speed of the STRAY table (one element's)", "True", strayElement > 0);
        Check("  its element and the one it is strong against resist rank 11, the weak one -3, the others 0", "True",
              Lua($"local m = GetMobByID({stray1}) local e = {strayElement} local ok = true " +
                  "for i = xi.element.FIRE, xi.element.DARK do local v = m:getMod(xi.data.element.getElementalResistanceRankModifier(i)) " +
                  "local want = 0 if i == e or i == xi.data.element.getElementStrength(e) then want = 11 elseif i == xi.data.element.getElementWeakness(e) then want = -3 end " +
                  "if v ~= want then ok = false end end report(tostring(ok))") == "true" ? "True" : "False");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Promyvion: the receptacle fought (its TP move, the strays it pops and sets on the char), killed: the portal opens");
        Lua("player:setUnkillable(true)");
        ClaimBySwing(r1);
        Check("engaged and claimed: onMobEngage's setTP(3000)", "True", r1.OwnerId == c.Id);
        uint stray2 = rec1 + 2;
        long fightStart = now;
        bool usedSkill = false;
        for (int i = 0; i < 160 && !Mob(stray2).IsSpawned; i++)
        {
            Tick(250);
            usedSkill |= r1.SkillState != null;
        }
        long fightCd = long.Parse(MobLocal(rec1, "[Stray]CooldownFight"));
        Check("its TP move used (the receptacle only uses TP moves), onMobWeaponSkill's REGAIN 50 to 150", "True/True",
              $"{usedSkill}/{int.Parse(Lua($"report(GetMobByID({rec1}):getMod(xi.mod.REGAIN))")) is 50 or 100 or 150}");
        Check("onMobFight: the second stray (16842783) 10 to 20 s in, the next [Stray]CooldownFight 10 to 20 s on", "True/True/True",
              $"{Mob(stray2).IsSpawned}/{(now - fightStart) / 1000 is >= 9 and <= 22}/{fightCd >= EarthNow() + 9 && fightCd <= EarthNow() + 21}");
        for (int i = 0; i < 8; i++) Tick(250);
        // updateEnmity is PAI->Engage (no enmity added): the strays fight the char, their lists still empty
        Check("  both strays set on the char (updateEnmity: the fight's stray, and the idle one with its RangedEnmity wiped)", "True/True",
              $"{Mob(stray2).Engaged && Mob(stray2).BattleTarget == c}/{s1.Engaged && s1.BattleTarget == c}");
        uint portal1 = stream + 7;
        var p1 = Npc(portal1);
        Check("the floor 1 portal (memory stream + 7) closed before the kill", "9", p1.Animation);
        Enter(-40.0f, -1.0f, 200.0f);
        Check("  its trigger area (6) entered while closed (and fighting): no cutscene", "none", Cur());
        var killPackets = Kill(r1);
        long deathAt = r1.DeathAtMs;
        var despawnPackets = new List<BasicPacket>();
        for (int i = 0; i < 40 && r1.IsSpawned; i++)
        {
            s.PacketList.Clear();
            Tick(250);
            despawnPackets.AddRange(s.PacketList);
        }
        Check("the receptacle killed, gone at once (DESPAWN_TIME_REDUCTION 15 off the death state's 15 s: the 3 s fade alone)", "True/True",
              $"{!r1.IsSpawned}/{now - deathAt <= 4500}");
        Check("onMobDespawn: the chosen portal opened (OPEN_DOOR) and the group's next chosen (the same one, alone in its group)", "8/1",
              $"{p1.Animation}/{NpcLocal(portal1, "[Portal]Chosen")}");
        Check("  the receptacle back at animationSub 4", "4", r1.AnimationSub);
        Check("  its decoration: STATUS_DISAPPEAR (0x038 kesu) at once, still shown", "True/0",
              $"{despawnPackets.Any(p => Opcode(p) == 0x038 && BitConverter.ToUInt32(p.Buffer, 4) == deco.Id && System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) == "kesu")}/{deco.Status}");
        for (int i = 0; i < 12; i++) Tick(250);
        Check("  2.5 s on: updateToEntireZone CUTSCENE_ONLY / DESPAWN", "6/2", $"{deco.Status}/{deco.Animation}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Promyvion: the open portal's trigger area, the floors' portals and the way out");
        foreach (var st in new[] { s1, Mob(stray2) }) Lua($"DespawnMob({st.Id})");
        Send(0x01A, 28, b => BitConverter.GetBytes((ushort)ActionId.AttackOff).CopyTo(b, C2sAction.OffActionID));
        for (int i = 0; i < 10; i++) Tick(400);
        Enter(-40.0f, -1.0f, 230.0f);
        Enter(-40.0f, -1.0f, 200.0f);
        Check("the open portal's trigger area (6), the char idle: handlePortal: optional cutscene 37", "37", Cur());
        EventEnd(37, 1);
        Check("  event 37 finished: nothing more (the zone's onEventFinish has no case)", "none/16", $"{Cur()}/{c.Zone}");
        // a GM's !pos to floor 3 (West), the NE portal opened (!exec GetNPCByID(..):openDoor(180))
        Lua($"GetNPCByID({stream}):openDoor(180)");
        Lua("player:setPos(-280, -1, 270)");
        Tick(400);
        Enter(-280.0f, 0.5f, 280.0f);
        Check("floor 3 (West)'s NE portal (trigger area 11, open): optional cutscene 30", "30", Cur());
        EventEnd(30, 1);
        Check("  event 30 finished (1): [Holla]ReturnWest 1", "1", Var("[Holla]ReturnWest"));
        Lua("player:setPos(120, -1, -310)");
        Tick(400);
        Enter(120.0f, -1.0f, -320.0f);
        Check("floor 4's return (trigger area 5): optional cutscene 43 + [Holla]ReturnWest (44: to floor 3 West)", "44", Cur());
        EventEnd(44, 1);
        Check("  event 44 finished (1): [Holla]ReturnWest 0", "0", Var("[Holla]ReturnWest"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Promyvion: the portal closes after its 3 minutes; the map's ??? (a Teal Memosphere)");
        for (int i = 0; i < 180 && p1.Animation == 8; i++) Tick(1000);
        Check("openDoor(180): the floor 1 portal closed again 3 minutes after the kill", "9", p1.Animation);
        for (int i = 0; i < 200 && !r1.IsSpawned; i++) Tick(1000);
        Check("the receptacle back after its 5 minute respawn: animationSub 4, receptacleOnMobSpawn shows its decoration again (NORMAL, NONE)",
              "True/4/0/0", $"{r1.IsSpawned && r1.IsAlive}/{r1.AnimationSub}/{deco.Status}/{deco.Animation}");
        uint qm = NpcId("qm_map");
        Lua($"player:setPos({Npc(qm).X + 1}, {Npc(qm).Y}, {Npc(qm).Z})");
        Tick(400);
        Talk(qm);
        Check("the ??? without the map: EERIE_GREEN_GLOW", $"{Text(PromyvionHolla, "EERIE_GREEN_GLOW")}:0", Special());
        Trade(qm, (InvItem(TealMemosphere).Slot, 1));
        Check("a Teal Memosphere traded: event 49", $"{EvStr(qm, 49)}/49", $"{Ev()}/{Cur()}");
        EventEnd(49, 0);
        Check("event 49 finished: the memosphere taken (confirmTrade), MAP_OF_PROMYVION_HOLLA, KEYITEM_OBTAINED", $"0/True/{Text(PromyvionHolla, "KEYITEM_OBTAINED")}:{Ki("MAP_OF_PROMYVION_HOLLA")}",
              $"{InvCount(TealMemosphere)}/{HasKi("MAP_OF_PROMYVION_HOLLA")}/{Special()}");
        Talk(qm);
        Check("  the ??? with the map: NOTHING_OUT_OF_ORDINARY", $"{Text(PromyvionHolla, "NOTHING_OUT_OF_ORDINARY")}:0", Special());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Promyvion: floor 1's exit");
        Lua("player:setPos(80, 0, 90)");
        Tick(400);
        Enter(80.0f, 0.0f, 80.0f);
        Check("floor 1's exit (trigger area 1): optional cutscene 46", "46", Cur());
        Lua("player:setCharVar('[Holla]ReturnWest', 1)");
        EventEnd(46, 1);
        Check("  event 46 finished (1): [Holla]ReturnWest 0, setPos to the Hall of Transference", "0", Var("[Holla]ReturnWest"));
        Rezone();
        Check("  in the Hall of Transference (-225.682, 280.002)", $"{HallOfTransference}/-225.7/280.0", $"{c.Zone}/{c.X:F1}/{c.Z:F1}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Voidwalker ----
        Console.WriteLine("Voidwalker: Assai Nybaem in Ru'Lude Gardens, a Clear Abyssite bought");
        Fixture(db, VoidwalkerId, RuLude, -32.0f, 0.0f, -72.0f, Windurst, War, 75);
        CharId = VoidwalkerId;
        Login();
        uint assai = NpcId("Assai_Nybaem");
        Near(assai);
        Talk(assai);
        Check("Assai Nybaem with no abyssite: event 10120 (the held abyssites' bits 0)", $"{EvStr(assai, 10120, 0)}/10120", $"{Ev()}/{Cur()}");
        EventEnd(10120, 3, 1);
        Check("  update 3: 1000 gil held, no Clear Abyssite: 1", "1", Upds());
        EventEnd(10120, 1);
        int clearKi = Ki("CLEAR_ABYSSITE"), colorfulKi = Ki("COLORFUL_ABYSSITE"), greyKi = Ki("GREY_ABYSSITE"), blackKi = Ki("BLACK_ABYSSITE");
        Check("event 10120 finished (1): 1000 gil taken, CLEAR_ABYSSITE, KEYITEM_OBTAINED", $"9000/True/{Text(RuLude, "KEYITEM_OBTAINED")}:{clearKi}",
              $"{Lua("report(player:getGil())")}/{HasKi("CLEAR_ABYSSITE")}/{Special()}");
        Talk(assai);
        Check("  again: the bits 1 (the Clear Abyssite)", EvStr(assai, 10120, 1), Ev());
        EventEnd(10120, 3, 1);
        Check("  update 3 with it held: 2", "2", Upds());
        EventEnd(10120, 0);
        Lua("player:setGil(500)");
        Talk(assai);
        EventEnd(10120, 3, 1);
        Check("  update 3 with 500 gil: 3", "3", Upds());
        EventEnd(10120, 0);
        Lua("player:setGil(9000)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Voidwalker: La Theine Plateau's voidwalkers (zoneOnInit's random positions, hidden)");
        WarpTo(LaTheine, -334.0f, 37.0f, 150.0f);
        var vw = new Dictionary<int, List<uint>>();
        foreach (var ki in new[] { clearKi, colorfulKi, greyKi, blackKi })
            vw[ki] = Lua($"report(table.concat(zones[{LaTheine}].mob.VOIDWALKER[{ki}], ' '))").Split(' ').Select(uint.Parse).ToList();
        var allVw = vw.Values.SelectMany(x => x).ToList();
        List<string[]> VwPos() => Lua($"local t = {{}} for i, p in ipairs(xi.voidwalker.pos[{LaTheine}]) do table.insert(t, p.pos[1] .. ':' .. p.pos[3] .. ':' .. tostring(p.mobId)) end report(table.concat(t, ' '))")
            .Split(' ').Select(x => x.Split(':')).ToList();
        var vwPos = VwPos();
        string PosOf(MobEntity m) => $"{MathF.Round(m.X)}:{MathF.Round(m.Z)}";
        Check("12 voidwalkers (8 Clear, 2 Colorful, 1 Grey, 1 Black), each up", "12/8/2/1/1/True",
              $"{allVw.Count}/{vw[clearKi].Count}/{vw[colorfulKi].Count}/{vw[greyKi].Count}/{vw[blackKi].Count}/{allVw.All(id => Mob(id).IsSpawned && Mob(id).IsAlive)}");
        string SpawnOf(MobEntity m) => $"{MathF.Round(m.SpawnX)}:{MathF.Round(m.SpawnZ)}";
        Check("  each spawning at its own position of xi.voidwalker.pos (setSpawn / setPos; roaming from there), the position taken by it", "True/12",
              $"{allVw.All(id => vwPos.Any(p => $"{p[0]}:{p[1]}" == SpawnOf(Mob(id)) && p[2] == id.ToString()))}/{allVw.Select(id => SpawnOf(Mob(id))).Distinct().Count()}");
        Check("  onMobSpawn: INVISIBLE, the name and the HP hidden, untargetable", "True",
              allVw.All(id => Mob(id).Status == 3 && Mob(id).Untargetable && Lua($"report(tostring(GetMobByID({id}):isNameHidden()))") == "true"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // the 0x0E8 /heal on, then off
        List<BasicPacket> Heal()
        {
            s.PacketList.Clear();
            Send(0x0E8, 8, b => b[4] = 1);
            var p = new List<BasicPacket>(s.PacketList);
            Send(0x0E8, 8, b => b[4] = 2);
            Tick(400);
            return p;
        }
        // getDirection's eight ways (0 east, 2 south, 4 west, 6 north)
        int Direction(MobEntity m)
        {
            float dx = m.X - c.X, dz = m.Z - c.Z;
            double degree = Math.Abs(Math.Atan(dz / dx) * 180.0 / Math.PI);
            if (degree >= 70) return dz >= 0 ? 6 : 2;
            if (degree <= 20) return dx >= 0 ? 0 : 4;
            return dz >= 0 ? (dx >= 0 ? 7 : 5) : (dx >= 0 ? 1 : 3);
        }
        float Dist(MobEntity m) => MathF.Sqrt((m.X - c.X) * (m.X - c.X) + (m.Y - c.Y) * (m.Y - c.Y) + (m.Z - c.Z) * (m.Z - c.Z));
        MobEntity Nearest(int ki) => vw[ki].Select(Mob).Where(m => m.IsSpawned && m.IsAlive).OrderBy(Dist).First();
        // a GM !pos: the char put at x / z by the mob (its height)
        void PutBy(MobEntity m, float dx, float dz)
        {
            float x = m.X + dx, y = m.Y, z = m.Z + dz;
            Lua($"player:setPos({x}, {y}, {z})");
            Tick(400);
            MoveTo(x, y, z);
        }

        Console.WriteLine("Voidwalker: /heal with the Clear Abyssite: the hint, then the pop beside the nearest");
        // the level 80 voidwalkers and the zone's aggressive mobs: a GM keeps the char up, and its bare-handed swings
        // landing (ACC)
        keepUnkillable = true;
        Lua("player:addMod(xi.mod.ACC, 1000)");
        Lua("player:setUnkillable(true)");
        var target = Mob(vw[clearKi][0]);
        PutBy(target, -40.0f, 0.0f);
        var near = Nearest(clearKi);
        // the distance a float: messageSpecial's va.get<uint32> rounds it (sol's llround)
        string hint = $"{Text(LaTheine, "VOIDWALKER_MOB_HINT")}:0:{Direction(near)}:{(uint)MathF.Round(Dist(near), MidpointRounding.AwayFromZero)}:{clearKi}";
        var heal1 = Heal();
        Check("/heal 40 yalms west of a Clear voidwalker: HEALING's onEffectGain: VOIDWALKER_MOB_HINT (Clear: 0, the way, the distance, the key item)",
              hint, SpecialOf(heal1));
        Check("  HEALING given and taken off again", "false", Lua("report(tostring(player:hasStatusEffect(xi.effect.HEALING)))"));
        near = Nearest(clearKi);
        PutBy(near, -2.0f, 1.0f);
        var heal2 = Heal();
        Check("/heal within 4 yalms: VOIDWALKER_SPAWN_MOB, the voidwalker shown (UPDATE, its name, targetable) and claimed",
              $"{Text(LaTheine, "VOIDWALKER_SPAWN_MOB")}:0/1/False/false/{c.Id}",
              $"{SpecialOf(heal2)}/{near.Status}/{near.Untargetable}/{Lua($"report(tostring(GetMobByID({near.Id}):isNameHidden()))")}/{near.OwnerId}");
        Check("  [VoidWalker]PopedBy the char, PopedWith the Clear Abyssite, PopedAt now; the abyssite kept",
              $"{c.Id}/{clearKi}/True/True", $"{MobLocal(near.Id, "[VoidWalker]PopedBy")}/{MobLocal(near.Id, "[VoidWalker]PopedWith")}/" +
                                              $"{Math.Abs(long.Parse(MobLocal(near.Id, "[VoidWalker]PopedAt")) - EarthNow()) <= 2}/{HasKi("CLEAR_ABYSSITE")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Voidwalker: the popped voidwalker killed (no upgrade: checkUpgrade's 1 in 10 missed), its new position");
        // checkUpgrade's math.randomInt(1, 10) == 5 forced off, then on (a GM's dice): the rest of the script as it is
        void ForceUpgradeRoll(int roll) => Lua($"if not _verifyRandomInt then _verifyRandomInt = math.randomInt end " +
                                                $"math.randomInt = function(a, b) if a == 1 and b == 10 then return {roll} end return _verifyRandomInt(a, b) end");
        void RestoreRoll() => Lua("if _verifyRandomInt then math.randomInt = _verifyRandomInt _verifyRandomInt = nil end");
        ForceUpgradeRoll(4);
        var vwKill1 = Kill(near);
        RestoreRoll();
        Check("killed: no key item change (the roll not 5)", "True/True/False", $"{near.IsDead || !near.IsSpawned}/{HasKi("CLEAR_ABYSSITE")}/{HasKi("COLORFUL_ABYSSITE")}");
        for (int i = 0; i < 60 && near.IsSpawned; i++) Tick(500);
        vwPos = VwPos();
        Check("  despawned: onMobDespawn's removeMobIdFromPos and setRandomPos: a position of the table its alone, the vars cleared",
              $"True/1/0/0", $"{vwPos.Any(p => $"{p[0]}:{p[1]}" == $"{MathF.Round(near.SpawnX)}:{MathF.Round(near.SpawnZ)}" && p[2] == near.Id.ToString())}/" +
                             $"{vwPos.Count(p => p[2] == near.Id.ToString())}/{MobLocal(near.Id, "[VoidWalker]PopedBy")}/{MobLocal(near.Id, "[VoidWalker]PopedWith")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Voidwalker: another popped and killed, the roll 5: the Clear Abyssite becomes Colorful");
        near = Nearest(clearKi);
        PutBy(near, 2.0f, 0.0f);
        Heal();
        Check("the next Clear voidwalker popped and claimed", $"1/{c.Id}", $"{near.Status}/{near.OwnerId}");
        ForceUpgradeRoll(5);
        var vwKill2 = Kill(near);
        RestoreRoll();
        // the voidwalker texts alone (a treasure casket's PLAYER_OBTAINS_TEMP_ITEM + 21 may come with the kill)
        int vwFirstText = Text(LaTheine, "VOIDWALKER_NO_MOB");
        Check("killed with the roll 5: the Clear Abyssite gone, the Colorful given, VOIDWALKER_UPGRADE_KI_1 (Clear, Colorful)",
              $"False/True/{Text(LaTheine, "VOIDWALKER_UPGRADE_KI_1")}:{clearKi}:{colorfulKi}",
              $"{HasKi("CLEAR_ABYSSITE")}/{HasKi("COLORFUL_ABYSSITE")}/{SpecialOf(vwKill2.Where(p => Opcode(p) == 0x02A && (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) >= vwFirstText))}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Voidwalker: the Colorful Abyssite's voidwalker (Chesma / Tammuz) popped, left behind: it fades");
        var colorful = Nearest(colorfulKi);
        // a spot 300 yalms or more from both Colorful voidwalkers
        var far = (from gx in Enumerable.Range(-4, 9) from gz in Enumerable.Range(-4, 9) select (X: gx * 200.0f, Z: gz * 200.0f))
            .OrderByDescending(p => vw[colorfulKi].Select(Mob).Min(m => MathF.Sqrt((m.X - p.X) * (m.X - p.X) + (m.Z - p.Z) * (m.Z - p.Z)))).First();
        PutBy(colorful, far.X - colorful.X, far.Z - colorful.Z);
        var heal3 = Heal();
        Check("/heal 300 yalms or more from both: VOIDWALKER_MOB_TOO_FAR (the Colorful Abyssite)", $"True/{Text(LaTheine, "VOIDWALKER_MOB_TOO_FAR")}:{colorfulKi}",
              $"{Dist(Nearest(colorfulKi)) >= 300}/{SpecialOf(heal3)}");
        PutBy(colorful, 0.0f, -2.0f);
        var heal4 = Heal();
        Check("/heal beside it: VOIDWALKER_SPAWN_MOB, shown, claimed", $"{Text(LaTheine, "VOIDWALKER_SPAWN_MOB")}:0/1/{c.Id}", $"{SpecialOf(heal4)}/{colorful.Status}/{colorful.OwnerId}");
        Lua("player:setUnkillable(true)");
        for (int i = 0; i < 20 && !colorful.Engaged; i++) Tick(400);
        Check("  it fights the char (the claim's enmity)", "True", colorful.Engaged && colorful.BattleTarget == c);
        var fadePackets = new List<BasicPacket>();
        s.PacketList.Clear();
        PutBy(colorful, 0.0f, -40.0f);
        fadePackets.AddRange(s.PacketList);
        for (int i = 0; i < 20 && colorful.FadeAtMs == 0 && colorful.IsSpawned; i++)
        {
            s.PacketList.Clear();
            Tick(400);
            fadePackets.AddRange(s.PacketList);
            if (colorful.FadeAtMs == 0 && colorful.IsSpawned && Dist(colorful) < 26) PutBy(colorful, 0.0f, -40.0f);
        }
        Check("the char more than 25 yalms off: onMobFight's VOIDWALKER_DESPAWN, DespawnMob", $"{Text(LaTheine, "VOIDWALKER_DESPAWN")}:0/True",
              $"{SpecialOf(fadePackets)}/{colorful.FadeAtMs != 0 || !colorful.IsSpawned}");
        Lua("player:setHP(player:getMaxHP())");
        Check("  the Colorful Abyssite kept (not broken by its pop)", "True", HasKi("COLORFUL_ABYSSITE"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Voidwalker: a Grey Abyssite (a GM's !addkeyitem) breaks on Dawon's pop; a lone Black Abyssite with Yilbegan gone");
        for (int i = 0; i < 20 && colorful.IsSpawned; i++) Tick(500);
        Lua("player:delKeyItem(xi.keyItem.COLORFUL_ABYSSITE) player:addKeyItem(xi.keyItem.GREY_ABYSSITE)");
        var dawon = Mob(vw[greyKi][0]);
        PutBy(dawon, 1.0f, 1.0f);
        var heal5 = Heal();
        Check("/heal beside Dawon: VOIDWALKER_BREAK_KI (the Grey Abyssite gone), shown, claimed",
              $"{Text(LaTheine, "VOIDWALKER_BREAK_KI")}:{greyKi}/False/1/{c.Id}", $"{SpecialOf(heal5)}/{HasKi("GREY_ABYSSITE")}/{dawon.Status}/{dawon.OwnerId}");
        Lua($"DespawnMob({dawon.Id})");
        var yilbegan = Mob(vw[blackKi][0]);
        Lua($"player:addKeyItem(xi.keyItem.BLACK_ABYSSITE) DespawnMob({yilbegan.Id})");
        for (int i = 0; i < 20 && yilbegan.IsSpawned; i++) Tick(500);
        var heal6 = Heal();
        Check("/heal with the Black Abyssite alone, Yilbegan gone: VOIDWALKER_NO_MOB (the Black Abyssite)", $"{Text(LaTheine, "VOIDWALKER_NO_MOB")}:{blackKi}", SpecialOf(heal6));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        keepUnkillable = false;
        Lua("player:setUnkillable(false)");
        Console.WriteLine("Voidwalker: Assai Nybaem takes an abyssite back");
        WarpTo(RuLude, -32.0f, 0.0f, -72.0f);
        Talk(assai);
        Check("Assai Nybaem with the Black Abyssite: the bits 1 << 8", EvStr(assai, 10120, 1 << 8), Ev());
        EventEnd(10120, 2u | 9u << 4);
        Check("event 10120 finished (2, abyssite 9): the Black Abyssite discarded", "False", HasKi("BLACK_ABYSSITE"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Zeni Notorious Monsters ----
        Console.WriteLine("ZNM: Sanraku and Ryo in Aht Urhgan Whitegate, the first talk");
        Fixture(db, ZnmId, Whitegate, -126.0f, 1.0f, 24.0f, Windurst, War, 75);
        CharId = ZnmId;
        Login();
        uint sanraku = NpcId("Sanraku"), ryo = NpcId("Ryo");
        int wgText(string n) => Text(Whitegate, n);
        Near(ryo);
        Talk(ryo);
        Check("Ryo before Sanraku: MASTER_FORBID (showText: 0x02A from Ryo), no event", $"{wgText("MASTER_FORBID")}:0/{ryo}/none", $"{Special()}/{Of(0x02A).Select(p => BitConverter.ToUInt32(p.Buffer, 4)).FirstOrDefault()}/{Cur()}");
        Near(sanraku);
        Talk(sanraku);
        Check("Sanraku the first time: event 908", $"{EvStr(sanraku, 908)}/908", $"{Ev()}/{Cur()}");
        EventEnd(908, 0);
        Check("event 908 finished: ZeniStatus 1", "1", Var("ZeniStatus"));
        Talk(sanraku);
        Check("Sanraku with no zeni: HOPES_REST, no event", $"{wgText("HOPES_REST")}:0/none", $"{Special()}/{Cur()}");
        Near(ryo);
        Talk(ryo);
        Check("Ryo once Sanraku has spoken: event 913", $"{EvStr(ryo, 913)}/913", $"{Ev()}/{Cur()}");
        EventEnd(913, 200, 1);
        string interest = Lua("report(GetServerVariable('[ZNM][Sanraku]Interest'))");
        Check("  update 200 (the subject of interest): a random one of 61, kept as [ZNM][Sanraku]Interest", $"True/{interest}",
              $"{int.Parse(interest) is >= 1 and <= 61}/{Upds()}");
        EventEnd(913, 201, 1);
        string fauna = Lua("report(GetServerVariable('[ZNM][Sanraku]Fauna'))");
        Check("  update 201 (the recommended fauna): a random one of 54, kept as [ZNM][Sanraku]Fauna", $"True/{fauna}",
              $"{int.Parse(fauna) is >= 1 and <= 54}/{Upds()}");
        EventEnd(913, 300, 1);
        Check("  update 300 (the zeni balance): 0", "", Upds());
        EventEnd(913, 401, 1);
        EventEnd(913, 402, 1);
        Check("  updates 401 and 402: ZeniStatus 2, then 3 (the islets asked about)", "3", Var("ZeniStatus"));
        EventEnd(913, 404, 1);
        Check("  update 404 (the menu): 175 - 12 (ZeniStatus 2 or more), no zeni", "163", Upds());
        EventEnd(913, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ZNM: a Soultrapper and a Blank Soul Plate in Wajaom Woodlands, a mob's image taken");
        ushort soultrapper = ushort.Parse(Lua("report(xi.item.SOULTRAPPER)")), blankPlate = ushort.Parse(Lua("report(xi.item.BLANK_SOUL_PLATE)")),
               soulPlate = ushort.Parse(Lua("report(xi.item.SOUL_PLATE)")), butterfly = ushort.Parse(Lua("report(xi.item.HELLCAGE_BUTTERFLY)")),
               wing = ushort.Parse(Lua("report(xi.item.VULPANGUES_WING)"));
        Lua($"player:addItem({soultrapper}) player:addItem({blankPlate}, 12)");
        WarpTo(Wajaom, -690.0f, -7.0f, -123.0f);
        Lua("player:setUnkillable(true) player:addMod(xi.mod.ACC, 1000)");
        Equip(InvItem(soultrapper).Slot, 2);
        Equip(InvItem(blankPlate).Slot, 3);
        Check("the Soultrapper (ranged) and the Blank Soul Plates (ammo) on", $"{soultrapper}/{blankPlate}",
              $"{c.Equipped[2]?.ItemId ?? 0}/{c.Equipped[3]?.ItemId ?? 0}");
        var wjInfo = zone.ZoneInfoOf(Wajaom);
        // the nearest mob the char's swing claims
        MobEntity subject = null;
        foreach (var cand in wjInfo.Mobs.Where(m => m.IsSpawned && m.IsAlive && m.Type == MobType.Normal && m.Allegiance == 0 && m.Status == 1)
                     .OrderBy(m => (m.X - c.X) * (m.X - c.X) + (m.Z - c.Z) * (m.Z - c.Z)).Take(4).ToList())
        {
            subject = cand;
            ClaimBySwing(cand);
            if (cand.OwnerId == c.Id) break;
        }
        // Sanraku's interest and fauna set off the mob (a GM's !setservervar): the plate's zeni is its quality alone
        int subjectFamily = int.Parse(Lua($"report(GetMobByID({subject.Id}):getFamily())"));
        Lua($"for i = 1, #xi.znm.SANRAKUS_INTEREST do local r = xi.znm.SANRAKUS_INTEREST[i] if r.family ~= {subjectFamily} and not utils.contains({subjectFamily}, r.ecoSystem) then " +
            "SetServerVariable('[ZNM][Sanraku]Interest', i) break end end SetServerVariable('[ZNM][Sanraku]Fauna', 1)");
        Check($"a {subject.Name} (family {subjectFamily}) claimed", $"{c.Id}", subject.OwnerId);
        // the soultrapper's use delay (10 s from the equip)
        for (int i = 0; i < 12; i++) Tick(1000);
        // the capture roll forced to succeed (a GM's dice) and getZeniValue's result kept: the rest of the script as it is
        Lua("_verifyRandomInt = math.randomInt math.randomInt = function(a, b) if a == 1 and b == 100 then return 1 end return _verifyRandomInt(a, b) end " +
            "_verifyGetZeni = xi.znm.soultrapper.getZeniValue xi.znm.soultrapper.getZeniValue = function(t, p) _verifyZeni = _verifyGetZeni(t, p) return _verifyZeni end");
        s.PacketList.Clear();
        var usePackets = new List<BasicPacket>();
        Send(0x037, 20, b =>
        {
            BitConverter.GetBytes(subject.Id).CopyTo(b, 4);
            BitConverter.GetBytes(subject.TargId).CopyTo(b, 12);
            b[14] = InvItem(soultrapper).Slot;
        });
        usePackets.AddRange(s.PacketList);
        for (int i = 0; i < 20 && InvCount(soulPlate) == 0; i++)
        {
            s.PacketList.Clear();
            Tick(400);
            usePackets.AddRange(s.PacketList);
        }
        Lua("math.randomInt = _verifyRandomInt xi.znm.soultrapper.getZeniValue = _verifyGetZeni");
        int zeniValue = int.Parse(Lua("report(_verifyZeni or -1)"));
        Check("the Soultrapper used on it (0x037): onItemCheck passes, onItemUse: a Soul Plate, one Blank Soul Plate used (removeAmmo)", "1/11",
              $"{InvCount(soulPlate)}/{InvItem(blankPlate)?.Quantity}");
        Check("  getZeniValue with the claim: 5 to 150", "True", zeniValue is >= 5 and <= 150);
        string plateData = Lua($"local e = player:findItem({soulPlate}):getExData() report(string.format('%s/%d/%d/%d/%d/%d/%d', e.signature, e.zoneId, e.familyId, e.poolId, e.level, e.quality, " +
                               "xi.data.pankration.feralSkills[e.feralSkill] and xi.data.pankration.feralSkills[e.feralSkill].fp == e.feralPoints and 1 or 0))");
        // Exdata::SoulPlate's Quality is a 6-bit field: a zeni value of 64 or more is kept modulo 64 (in C++ too)
        Check("  its exdata (Exdata::SoulPlate): the signature (PackSoultrapperName: 15 chars, no underscores), the zone, the family, the pool, the level, the quality (6 bits), a feral skill and its points",
              $"{(subject.Name.Length > 15 ? subject.Name.Substring(0, 15) : subject.Name).Replace("_", "")}/{Wajaom}/{subjectFamily}/{subject.Pool}/{subject.MLvl}/{zeniValue & 63}/1", plateData);
        Check("  char_inventory.extra saved", "True", DbRow(db, "HEX(extra)", $"FROM char_inventory WHERE charid = ? AND itemId = {soulPlate}") is var plateHex && plateHex.Length >= 48 && plateHex.Trim('0').Length > 0);
        for (int i = 0; i < 12; i++)
        {
            s.PacketList.Clear();
            Tick(400);
            usePackets.AddRange(s.PacketList);
        }
        Check("  4 s on: SOULTRAPPER_SUCCESS (0, the Soul Plate)", $"515:0:{soulPlate}", BasicOf(usePackets, 515, 516));
        Lua($"DespawnMob({subject.Id})");
        Send(0x01A, 28, b => BitConverter.GetBytes((ushort)ActionId.AttackOff).CopyTo(b, C2sAction.OffActionID));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ZNM: the plate to Ryo (its worth) and to Sanraku (the zeni)");
        WarpTo(Whitegate, -126.0f, 1.0f, 24.0f);
        int plateZeni = int.Parse(Lua($"report(xi.znm.calculatePlateZeni(player, player:findItem({soulPlate}):getExData()))"));
        Check("calculatePlateZeni: off Sanraku's interest and fauna, the quality clamped to 5 .. 150", Math.Clamp(zeniValue & 63, 5, 150), plateZeni);
        Near(ryo);
        Trade(ryo, (InvItem(soulPlate).Slot, 1));
        Check("the plate traded to Ryo: event 914, its worth cached", $"{EvStr(ryo, 914)}/{plateZeni}", $"{Ev()}/{Lua("report(player:getLocalVar('[ZNM][Ryo]SoulPlateValue'))")}");
        EventEnd(914, 0, 1);
        Check("  update: the worth, the cache cleared", $"{plateZeni}/0", $"{Upds()}/{Lua("report(player:getLocalVar('[ZNM][Ryo]SoulPlateValue'))")}");
        EventEnd(914, 0);
        Check("event 914 finished: the trade cleaned, the plate kept", "1", InvCount(soulPlate));
        int tradesBefore = int.Parse(Lua("report(GetServerVariable('[ZNM][Sanraku]Trades'))"));
        Near(sanraku);
        Trade(sanraku, (InvItem(soulPlate).Slot, 1));
        Check("the plate traded to Sanraku: event 910 (the zeni), [ZNM][Sanraku]Trades + 1", $"{EvStr(sanraku, 910, plateZeni)}/{tradesBefore + 1}",
              $"{Ev()}/{Lua("report(GetServerVariable('[ZNM][Sanraku]Trades'))")}");
        EventEnd(910, 0);
        Check("event 910 finished: the plate taken, the zeni given, the day's trading day and count",
              $"0/{plateZeni}/{Lua("report(VanadielUniqueDay())")}/1",
              $"{InvCount(soulPlate)}/{Currency("zeni_point")}/{Var("[ZNM][Sanraku]TradingDay")}/{Var("[ZNM][Sanraku]TradedPlates")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ZNM: Sanraku's menu, a pop item bought, the islet's salt, a trophy for a seal");
        Talk(sanraku);
        Check("Sanraku with zeni: event 909 (the menu, no seals: the default 0x7F8FE3F8)", EvStr(sanraku, 909, 0x7F8FE3F8), Ev());
        EventEnd(909, 1, 1);
        Check("  update 1: ZeniStatus 3: the islets' menu (1)", "1", Upds());
        Lua("player:addCurrency('zeni_point', 4000)");
        int zeni0 = int.Parse(Currency("zeni_point"));
        EventEnd(909, 100, 1);
        Check("  update 100 (Vulpangue?): its pop price, the tier 1 minimum 1000, kept as [ZNM][Vulpangue]PopCost", "0,0,0,0,0,0,1000/1000",
              $"{Upds()}/{Lua("report(GetServerVariable('[ZNM][Vulpangue]PopCost'))")}");
        EventEnd(909, 400, 1);
        Check("  update 400 (Vulpangue): 1, 1000, the Hellcage Butterfly; it given, 1000 zeni taken, the price up to 1100",
              $"1,1000,{butterfly}/1/{zeni0 - 1000}/1100", $"{Upds()}/{InvCount(butterfly)}/{Currency("zeni_point")}/{Lua("report(GetServerVariable('[ZNM][Vulpangue]PopCost'))")}");
        EventEnd(909, 400, 1);
        Check("  update 400 again, the butterfly held: 4, nothing taken", $"4/1/{zeni0 - 1000}", $"{Upds()}/{InvCount(butterfly)}/{Currency("zeni_point")}");
        EventEnd(909, 300, 1);
        int salt = Ki("SICKLEMOON_SALT");
        Check("  update 300 (the first islet): 1, 500, 0, the Sicklemoon Salt; 500 zeni taken", $"1,500,0,{salt}/True/{zeni0 - 1500}",
              $"{Upds()}/{HasKi("SICKLEMOON_SALT")}/{Currency("zeni_point")}");
        EventEnd(909, 0);
        Lua($"player:addItem({wing})");
        int maroon = Ki("MAROON_SEAL");
        Trade(sanraku, (InvItem(wing).Slot, 1));
        Check("Vulpangue's Wing traded (a GM's !additem): event 912 (0, 0, 1, the Maroon Seal)", $"{EvStr(sanraku, 912, 0, 0, 1, maroon)}/{maroon}",
              $"{Ev()}/{Lua("report(player:getLocalVar('[ZNM]TrophyTrade'))")}");
        EventEnd(912, 0);
        Check("event 912 finished: the wing taken, the Maroon Seal", "0/True/0", $"{InvCount(wing)}/{HasKi("MAROON_SEAL")}/{Lua("report(player:getLocalVar('[ZNM]TrophyTrade'))")}");
        Talk(sanraku);
        Check("  the menu with the Maroon Seal: the tier 2 Tinnin ZNMs (0x38) added", EvStr(sanraku, 909, 0x7F8FE3F8 & ~0x38), Ev());
        EventEnd(909, 0);
        Lua($"player:addItem({wing})");
        Trade(sanraku, (InvItem(wing).Slot, 1));
        Check("another wing with the seal held: SINGLE_TALLY, no event, the wing kept", $"{wgText("SINGLE_TALLY")}:0/none/1", $"{Special()}/{Cur()}/{InvCount(wing)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("ZNM: the Hellcage Butterfly at Wajaom Woodlands' ???: Vulpangue");
        WarpTo(Wajaom, -690.0f, -7.0f, -123.0f);
        uint wjQm = NpcId("qm1");
        uint vulpangueId = uint.Parse(Lua($"report(zones[{Wajaom}].mob.VULPANGUE)"));
        var vulpangue = Mob(vulpangueId);
        Near(wjQm);
        Talk(wjQm);
        Check("the ???: BROKEN_SHARDS", $"{Text(Wajaom, "BROKEN_SHARDS")}:0", Special());
        Check("  Vulpangue not up", "False", vulpangue.IsSpawned);
        Trade(wjQm, (InvItem(butterfly).Slot, 1));
        Check("the butterfly traded: popFromQM: Vulpangue up and claimed, the ??? hidden, the butterfly taken, DRAWS_NEAR",
              $"True/{c.Id}/2/0/{Text(Wajaom, "DRAWS_NEAR")}:0", $"{vulpangue.IsSpawned && vulpangue.IsAlive}/{vulpangue.OwnerId}/{Npc(wjQm).Status}/{InvCount(butterfly)}/{Special()}");
        Check("  its onMobInitialize / onMobSpawn: IDLE_DESPAWN 300, [rage]timer 3600, HPP 90", "300/3600/90",
              $"{Lua($"report(GetMobByID({vulpangueId}):getMobMod(xi.mobMod.IDLE_DESPAWN))")}/{MobLocal(vulpangueId, "[rage]timer")}/{MobLocal(vulpangueId, "HPP")}");
        Lua($"DespawnMob({vulpangueId})");
        for (int i = 0; i < 10 && vulpangue.IsSpawned; i++) Tick(1000);
        Check("Vulpangue despawned (a GM's !despawnmob): the ??? still hidden (updateNPCHideTime 300)", "False/2", $"{vulpangue.IsSpawned}/{Npc(wjQm).Status}");
        for (int i = 0; i < 310 && Npc(wjQm).Status != 0; i++) Tick(1000);
        Check("  the ??? back 300 s on", "0", Npc(wjQm).Status);
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Gobbie Mystery Box ----
        Console.WriteLine("Gobbie Mystery Box: Funtrox in Windurst Woods, a new char turned away");
        Fixture(db, GobbieId, WindurstWoods, 0.0f, 0.0f, 0.0f, Windurst, War, 75);
        // the char_points row character creation makes (daily_tally -1: never signed up)
        Sql(db, "INSERT IGNORE INTO char_points(charid) VALUES (?)", BoundValue.Of(GobbieId));
        CharId = GobbieId;
        Login();
        uint funtrox = NpcId("Funtrox");
        Near(funtrox);
        Talk(funtrox);
        // messageSpecial's va.get<uint32>: sol's integral getter is llround(lua_tonumber) under LuaJIT, not a truncation
        Check("a char created today: YOU_MUST_WAIT_ANOTHER_N_DAYS (GOBBIE_BOX_MIN_AGE - its age + 1: 45.99.. sent rounded, 46), no event",
              $"{Text(WindurstWoods, "YOU_MUST_WAIT_ANOTHER_N_DAYS")}:46/none", $"{Special()}/{Cur()}");
        Check("  daily_tally -1 (never signed up)", "-1", Currency("daily_tally"));
        // a GM's backdating of chars.timecreated past the 45 days
        Sql(db, "UPDATE chars SET timecreated = NOW() - INTERVAL 60 DAY WHERE charid = ?", BoundValue.Of(GobbieId));
        Talk(funtrox);
        Check("60 days old, never signed up: event 6000 (the intro)", $"{EvStr(funtrox, 6000)}/6000", $"{Ev()}/{Cur()}");
        EventEnd(6000, 0);
        Check("event 6000 finished: daily_tally 50 (char_points)", "50/50", $"{Currency("daily_tally")}/{DbRow(db, "daily_tally", "FROM char_points WHERE charid = ?")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Gobbie Mystery Box: the dials (the peek, the Materials dial for 10 points, the item taken)");
        Talk(funtrox);
        const int hideFlags = 0x80 + 0x100 + 0x1000;
        Check("event 6001: no dial used, the hidden options 0x1180, 50 points", EvStr(funtrox, 6001, 0, 0, 0, 0, 0, 0, hideFlags, 50), Ev());
        EventEnd(6001, 4, 1);
        var peek = Upds().Split(',');
        Check("  update 4 (the peek): three of the special dial's items (SelectDailyItem 6: item_basic flags & 4), the points last",
              "True/50", $"{peek.Length == 8 && peek.Take(3).All(x => DbScalar(db, $"SELECT flags & 4 FROM item_basic WHERE itemid = {x}") == "4")}/{peek.LastOrDefault()}");
        EventEnd(6001, 1 + 8 * 1, 1);
        var dial1 = Upds().Split(',');
        string held = Var("gobbieBoxHoldingItem");
        Check("  update 9 (the Materials dial): its item (aH 38-44 or 50, flags & 4), dial 1, 0; held as gobbieBoxHoldingItem, 10 points spent",
              $"True/{held}/1/40", $"{DbScalar(db, $"SELECT (aH IN (38,39,40,41,42,43,44,50) AND flags & 4 > 0) FROM item_basic WHERE itemid = {dial1[0]}") == "1"}/{dial1[0]}/" +
                                   $"{(dial1.Length > 1 ? dial1[1] : "")}/{Currency("daily_tally")}");
        EventEnd(6001, 2 + 8 * 1, 1);
        Check("  update 10 (take it?) with room: no update", "", Upds());
        ushort heldItem = ushort.Parse(held);
        int heldBefore = InvCount(heldItem) > 0 ? (int)InvItem(heldItem).Quantity : 0;
        EventEnd(6001, 5 + 8 * 1, 1);
        Check("  update 13 (take it): npcUtil.giveItem: the item, ITEM_OBTAINED; the holding var cleared; the menu again (40 points)",
              $"True/True/0/0,0,0,0,0,0,{hideFlags},40", $"{c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == heldItem).Sum(i => (int)i.Quantity) > heldBefore}/" +
                                                        $"{Special().Contains($"{Text(WindurstWoods, "ITEM_OBTAINED")}:{heldItem}")}/{Var("gobbieBoxHoldingItem")}/{Upds()}");
        EventEnd(6001, 1 + 8 * 6, 1);
        Check("  update 49 (the special dial, 50 points) with 40: 1, 6, 1 (not enough)", "1,6,1/40", $"{Upds()}/{Currency("daily_tally")}");
        Lua("player:setCurrency('daily_tally', 60)");
        EventEnd(6001, 1 + 8 * 6, 1);
        var dial6 = Upds().Split(',');
        Check("  with 60 (a GM's): the special dial's item, 6, 0; 10 points left, gobbieBoxUsed bit 0",
              $"True/6/10/1/{dial6[0]}", $"{DbScalar(db, $"SELECT flags & 4 FROM item_basic WHERE itemid = {dial6[0]}") == "4"}/{(dial6.Length > 1 ? dial6[1] : "")}/" +
                                         $"{Currency("daily_tally")}/{Var("gobbieBoxUsed")}/{Var("gobbieBoxHoldingItem")}");
        EventEnd(6001, 0);
        Check("event 6001 finished with the item not taken: still held", dial6[0], Var("gobbieBoxHoldingItem"));
        Talk(funtrox);
        Check("Funtrox with an item held: event 6002", $"{EvStr(funtrox, 6002)}/6002", $"{Ev()}/{Cur()}");
        ushort special = ushort.Parse(dial6[0]);
        int specialBefore = c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == special).Sum(i => (int)i.Quantity);
        EventEnd(6002, 0);
        Check("event 6002 finished: the item given, the holding var cleared", $"True/0",
              $"{c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == special).Sum(i => (int)i.Quantity) > specialBefore}/{Var("gobbieBoxHoldingItem")}");
        Lua("player:setCurrency('daily_tally', 60)");
        Talk(funtrox);
        Check("event 6001 with the special dial used: its flag 1, 60 points", EvStr(funtrox, 6001, 1, 0, 0, 0, 0, 0, hideFlags, 60), Ev());
        EventEnd(6001, 1 + 8 * 6, 1);
        Check("  update 49 again: 1, 6, 2 (already used), nothing spent", "1,6,2/60", $"{Upds()}/{Currency("daily_tally")}");
        EventEnd(6001, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Gobbie Mystery Box: a Dial Key #Ab traded (a GM's !additem)");
        const ushort dialKeyAb = 9217;
        Lua($"player:addItem({dialKeyAb})");
        Trade(funtrox, (InvItem(dialKeyAb).Slot, 1));
        Check("the key traded: event 6010 (the key, dial 9), the key noted", $"{EvStr(funtrox, 6010, dialKeyAb, 9)}/{dialKeyAb}",
              $"{Ev()}/{Lua("report(player:getLocalVar('gobbieBoxKey'))")}");
        EventEnd(6010, 1, 1);
        var keyUpd = Upds().Split(',');
        string abjurations = Lua("report(table.concat({ xi.item.DRYADIC_ABJURATION_HEAD, xi.item.CYLLENIAN_ABJURATION_FEET }, ' '))");
        Check("  update 1: an abjuration (or Gobbie junk for one held), dial 9, 3; the key taken (tradeComplete), the item held",
              $"9/3/0/{keyUpd[0]}/0", $"{(keyUpd.Length > 2 ? keyUpd[1] + "/" + keyUpd[2] : "")}/{InvCount(dialKeyAb)}/{Var("gobbieBoxHoldingItem")}/" +
                                    $"{Lua("report(player:getLocalVar('gobbieBoxKey'))")}");
        ushort abItem = ushort.Parse(keyUpd[0]);
        Check("  the item an abjuration or Gobbie junk", "True",
              Lua($"local ok = false for _, i in ipairs({{ xi.item.GOBLIN_MESS_TIN, xi.item.GOBLIN_WEEL, xi.item.CHUNK_OF_HOBGOBLIN_CHOCOLATE, xi.item.HOBGOBLIN_PIE, " +
                  "xi.item.LOAF_OF_HOBGOBLIN_BREAD, xi.item.LOAF_OF_GOBLIN_BREAD, xi.item.CHUNK_OF_GOBLIN_CHOCOLATE, xi.item.GOBLIN_PIE }) do if i == " + abItem + " then ok = true end end " +
                  $"local n = GetItemByID({abItem}) and GetItemByID({abItem}):getName() or '' if string.find(n, 'abjuration') then ok = true end report(tostring(ok))") == "true" ? "True" : "False");
        EventEnd(6010, 2, 1);
        Check("  update 2: the held item given, the holding var cleared", $"1/0", $"{InvCount(abItem)}/{Var("gobbieBoxHoldingItem")}");
        EventEnd(6010, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Colonization Reives ----
        Console.WriteLine("Reives: Ceizak Battlegrounds' reives up at the zone's start (setupZone: ENABLE_SOA 1)");
        Fixture(db, ReiveId, Ceizak, 128.0f, 0.0f, -58.0f, Windurst, War, 75);
        CharId = ReiveId;
        Login();
        keepUnkillable = true;
        Lua("player:setUnkillable(true) player:addMod(xi.mod.ACC, 1000)");
        List<uint> ReiveIds(int reive, string part) => Lua($"report(table.concat(xi.reives.zoneData[{Ceizak}].reive[{reive}].{part}, ' '))").Split(' ').Select(uint.Parse).ToList();
        var obstacles = ReiveIds(1, "obstacles").Select(Mob).ToList();
        var defenders = ReiveIds(1, "mob").Select(Mob).ToList();
        var collision = ReiveIds(1, "collision").Select(Npc).ToList();
        int reiveCount = int.Parse(Lua($"report(#xi.reives.zoneData[{Ceizak}].reive)"));
        Check($"reive 1 of {reiveCount}: three Knotted Roots (17846624-26), four defenders, two collision blockers", "17846624,17846625,17846626/4/2",
              $"{string.Join(",", obstacles.Select(m => m.Id))}/{defenders.Count}/{collision.Count}");
        Check("  enableReive: the obstacles up, CLOSE_DOOR, no auto attack, no mob skills, NO_MOVE and NO_REST, no respawn", "True",
              obstacles.All(m => m.IsSpawned && m.IsAlive && m.Animation == 9 && !m.AutoAttackEnabled && !m.MobAbilityEnabled && !m.AllowRespawn &&
                                 Lua($"local m = GetMobByID({m.Id}) report(m:getMobMod(xi.mobMod.NO_MOVE) .. '/' .. m:getMobMod(xi.mobMod.NO_REST))") == "1/1"));
        Check("  the defenders up, respawning in 5 minutes", "True", defenders.All(m => m.IsSpawned && m.IsAlive && m.AllowRespawn && m.RespawnTimeSec == 300));
        Check("  the collision blockers closed (CLOSE_DOOR)", "9,9", string.Join(",", collision.Select(n => n.Animation)));
        Check("  every reive of the zone up", "True",
              Enumerable.Range(1, reiveCount).All(r => ReiveIds(r, "obstacles").All(id => Mob(id).IsSpawned && Mob(id).IsAlive && Mob(id).Animation == 9)));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Reives: the Knotted Roots broken one by one: the reive ends with the last");
        Kill(obstacles[0]);
        for (int i = 0; i < 20 && obstacles[0].IsSpawned; i++) Tick(1000);
        Check("the first root killed: gone (no respawn), the reive still on (the defenders up, the blockers closed)", "False/True/9,9",
              $"{obstacles[0].IsSpawned}/{defenders.All(m => m.IsSpawned)}/{string.Join(",", collision.Select(n => n.Animation))}");
        Check("  checkObjectiveStatus: not complete", "false", Lua($"report(tostring(xi.reives.checkObjectiveStatus({Ceizak}, 1)))"));
        Kill(obstacles[1]);
        var lastKill = Kill(obstacles[2]);
        Check("the last root killed: disableReive: the blockers opened (OPEN_DOOR), the defenders despawning with no respawn", "8,8/True",
              $"{string.Join(",", collision.Select(n => n.Animation))}/{defenders.All(m => (!m.IsSpawned || m.FadeAtMs != 0) && !m.AllowRespawn)}");
        Check("  the roots: OPEN_DOOR, respawning in 60 minutes", "True", obstacles.All(m => m.Animation == 8 && m.AllowRespawn && m.RespawnTimeSec == 3600));
        for (int i = 0; i < 20 && (obstacles.Any(m => m.IsSpawned) || defenders.Any(m => m.IsSpawned)); i++) Tick(1000);
        Check("  all gone", "False/False", $"{obstacles.Any(m => m.IsSpawned)}/{defenders.Any(m => m.IsSpawned)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Reives: an hour on, the roots back: the reive again");
        long reiveOff = now;
        for (int i = 0; i < 800 && !obstacles.All(m => m.IsSpawned); i++) Tick(5000);
        Check("the roots respawned about 60 minutes on", "True/True", $"{obstacles.All(m => m.IsSpawned && m.IsAlive)}/{(now - reiveOff) / 60000 is >= 59 and <= 62}");
        Check("  onMobSpawn's enableReive: the defenders spawned again, the blockers closed, the roots no respawn", "True/9,9/True",
              $"{defenders.All(m => m.IsSpawned && m.IsAlive && m.RespawnTimeSec == 300)}/{string.Join(",", collision.Select(n => n.Animation))}/" +
              $"{obstacles.All(m => !m.AllowRespawn)}");
        // enableReive sets CLOSE_DOOR only on a root it spawns itself (not isAlive); the spawn handler's wave spawns the
        // three roots it took in, the first one's onMobSpawn spawning the others, which the wave then spawns again
        // (CMobEntity::Spawn: animation NONE): some root is left without CLOSE_DOOR (in C++ too, a script order bug)
        Check("  the roots' animation: NONE for a root the spawn handler spawned last, CLOSE_DOOR for one enableReive spawned last", "True/True",
              $"{obstacles.Any(m => m.Animation == 0)}/{obstacles.All(m => m.Animation is 0 or 9)}");
        keepUnkillable = false;
        Lua("player:setUnkillable(false)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Domain Invasion ----
        Console.WriteLine("Domain Invasion: Escha - Ru'Aun's fence, without Elvorseal");
        Fixture(db, DomainId, EschaRuAun, 0.0f, 0.0f, 0.0f, Windurst, War, 75);
        // a GM: the !naga command (permission 5) sets the encounter up
        Sql(db, "UPDATE chars SET gmlevel = 5 WHERE charid = ?", BoundValue.Of(DomainId));
        CharId = DomainId;
        Login();
        Check("onZoneIn from 0, 0, 0: setPos to (-0.371, -34.277, -466.98)", $"{EschaRuAun}/-0.4/-467.0", $"{c.Zone}/{c.X:F1}/{c.Z:F1}");
        // the 0x075 fences as flags:x:z:radius:render:color:gateId
        string FencesOf(IEnumerable<BasicPacket> from) => string.Join(" | ", from.Where(p => Opcode(p) == 0x075).Select(p =>
            $"{p.Buffer[4 + S2cBattlefield.OffFlags]}:{BitConverter.ToInt32(p.Buffer, 4 + S2cBattlefield.OffFenceX)}:{BitConverter.ToInt32(p.Buffer, 4 + S2cBattlefield.OffFenceY)}:" +
            $"{BitConverter.ToUInt32(p.Buffer, 4 + S2cBattlefield.OffFenceRadius)}:{BitConverter.ToUInt32(p.Buffer, 4 + S2cBattlefield.OffFenceRotation)}:" +
            $"{p.Buffer[4 + S2cBattlefield.OffFenceColor]}:{p.Buffer[4 + S2cBattlefield.OffFenceGateId]}"));
        // a walk: the client's 0x015 steps, the zone's trigger-area tick after each
        List<BasicPacket> Walk(params (float X, float Y, float Z)[] steps)
        {
            var got = new List<BasicPacket>();
            foreach (var (x, y, z) in steps)
            {
                s.PacketList.Clear();
                MoveTo(x, y, z);
                Tick(400);
                got.AddRange(s.PacketList);
            }
            return got;
        }
        // GP_CLI_COMMAND_CHAT_STD: a say
        List<BasicPacket> Say(string text)
        {
            s.PacketList.Clear();
            var bytes = System.Text.Encoding.ASCII.GetBytes(text);
            Send(0x0B5, 8 + (bytes.Length + 4) / 4 * 4, b => { b[4] = 0; bytes.CopyTo(b, 6); });
            return new List<BasicPacket>(s.PacketList);
        }
        Tick(3000);
        var inNoSeal = Walk((0.0f, -43.775f, -250.0f), (0.0f, -43.775f, -215.0f));
        Check("into the fence's trigger area (1, 25 yalms round (0, -210)) without Elvorseal: updateFence sends nothing", "", FencesOf(inNoSeal));
        Walk((0.0f, -43.775f, -250.0f));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Domain Invasion: !naga (Naga Raja, Elvorseal), the fence crossed both ways");
        var naga = zone.ZoneInfoOf(EschaRuAun).Mobs.First(m => m.Name == "Naga_Raja");
        var nagaCmd = Say("!naga");
        // the effect's power is a uint16 (CStatusEffect::m_Power): the char's id kept modulo 65536, in C++ too
        Check("!naga: Naga Raja up, Elvorseal on it and the char (the char's id as a uint16, the gate 13)", $"True/{c.Id & 0xFFFF}:13/{c.Id & 0xFFFF}:13",
              $"{naga.IsSpawned && naga.IsAlive}/{Lua($"local e = GetMobByID({naga.Id}):getStatusEffect(xi.effect.ELVORSEAL) report(e:getPower() .. ':' .. e:getSubPower())")}/" +
              $"{Lua("local e = player:getStatusEffect(xi.effect.ELVORSEAL) report(e:getPower() .. ':' .. e:getSubPower())")}");
        Check("  the fence round Naga Raja (blue; the gate the confrontation subPower & 0x0F: 13), the char put at (0, -238)",
              $"8:{(int)(naga.X * 1000)}:{(int)(naga.Z * 1000)}:25000:25000:1:13/0/-238", $"{FencesOf(nagaCmd)}/{c.X:F0}/{c.Z:F0}");
        var crossIn = Walk((0.0f, -43.775f, -230.0f), (0.0f, -43.775f, -215.0f));
        Check("into the fence: onTriggerAreaEnter's updateFence: the fence at (0, -210), 25, blue, the Elvorseal's gate 13", "8:0:-210000:25000:25000:1:13", FencesOf(crossIn));
        var inside = Walk((2.0f, -43.775f, -212.0f));
        Check("  a step inside: nothing more", "", FencesOf(inside));
        var crossOut = Walk((0.0f, -43.775f, -240.0f));
        Check("out of it: onTriggerAreaLeave's updateFence: gate 0", "8:0:-210000:25000:25000:1:0", FencesOf(crossOut));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Domain Invasion: !naga again (the teardown)");
        var teardown = Say("!naga");
        for (int i = 0; i < 10 && naga.IsSpawned; i++) Tick(1000);
        // delStatusEffect only marks the effect deleted (the set drops it on the next tick), and GetConfrontationSubPower
        // does not skip deleted ones: the removal's default gate is still 13 (in C++ too)
        Check("!naga again: Elvorseal gone, the fence removed (radius 0, the default gate still the deleted Elvorseal's 13), Naga Raja despawned",
              "false/8:0:0:0:25000:0:13/False",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.ELVORSEAL)))")}/{FencesOf(teardown)}/{naga.IsSpawned}");
        var after = Walk((0.0f, -43.775f, -215.0f));
        Check("  into the fence's area again: nothing", "", FencesOf(after));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        if (Environment.GetEnvironmentVariable("CONTENT3_DUMP_LOG") == "1") foreach (var l in log.Distinct()) Console.WriteLine("  log: " + l);
    }

    private const ushort TealMemosphere = 1720;
}
