using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Content1.Live -- live gate for the endgame instanced content driven as a client drives it, through the real
// Lua scripts: Dynamis (scripts/globals/dynamis.lua, the DYNAMIS effect, the Dynamis zones' mobs, NPCs and mixins),
// Einherjar (scripts/globals/einherjar), Salvage (scripts/globals/salvage.lua and the Remnants), Nyzul Isle
// (scripts/globals/nyzul.lua, scripts/globals/nyzul) and what Limbus keeps (Apollyon / Temenos). The 0x00A login, 0x015
// moves, 0x01A Talk and Attack, 0x036 trades, 0x05B event ends and updates, the zone's ticks, and every step's effect:
// the status effects, char vars, key items, items, the treasure pool, the event and message packets and the zone
// changes. The real scripts on the real MariaDB (xidb), chars 996051-996059. The walks between places the content does
// not script are GM !pos warps; GM setup (!addkeyitem, !completemission) stands in for the content's prerequisites.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;
    private static readonly List<string> _log = new List<string>();

    private static uint CharId = 996051;
    private static readonly uint[] CharIds = { 996051, 996052, 996053, 996054, 996055 };
    private const ushort SouthernSandoria = 230, Davoi = 149, DynamisSandoria = 185, ValkurmDunes = 103, DynamisValkurm = 39;

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
            if (Environment.GetEnvironmentVariable("CONTENT_DUMP_LOG") == "1") foreach (var l in _log.Distinct()) Console.WriteLine("  log: " + l);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // a Hume WAR 75 / MNK 37 past its first login (NEW_ADVENTURER) at x / y / z in the zone, with the gil and the items
    // from inventory slot 1 on
    private static void Fixture(IDatabase db, ushort zoneId, float x, float y, float z, uint gil, params (ushort Item, uint Qty)[] items)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of($"contentone{CharId % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of($"Contentone{CharId % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 1500, 50, 1, 75, 2, 37)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, unlocked, war, mnk) VALUES (?, 6, 75, 37)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_storage(charid, inventory) VALUES (?, 80) ON DUPLICATE KEY UPDATE inventory = 80", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,?)", BoundValue.Of(CharId), BoundValue.Of(gil));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(CharId), BoundValue.Of((byte)(i + 1)), BoundValue.Of(items[i].Item), BoundValue.Of(items[i].Qty));
    }

    // the GetMobByID(0) calls of an Einherjar chamber planned without a special mob, from its own log lines (from: the
    // line it was planned at, up to its "Releasing chamber"). Each despawnSpecialMob is one: cleanChamber's
    // ("Chamber cleaned.") and cycleWave's at the last wave ("All waves cleared!"). The engage's queued spawn is one
    // more when it ran before the last wave was cleared (on a completed chamber it returns before its GetMobByID):
    // "Special mob will spawn at <t>", then "Executing event at <t>"
    internal static int NoSpecialCalls(IReadOnlyList<string> log, string name, int from)
    {
        string tag = $"[einherjar][{name}] ";
        const string SpawnAt = "Special mob will spawn at ";
        int calls = 0;
        bool completed = false;
        string spawnAt = null;
        for (int i = from; i < log.Count; i++)
        {
            int at = log[i].IndexOf(tag, StringComparison.Ordinal);
            if (at < 0) continue;
            string msg = log[i].Substring(at + tag.Length).TrimEnd();
            if (msg == "Chamber cleaned.") calls++;
            else if (msg.StartsWith("All waves cleared!", StringComparison.Ordinal))
            {
                calls++;
                completed = true;
            }
            else if (msg.StartsWith(SpawnAt, StringComparison.Ordinal)) spawnAt = msg.Substring(SpawnAt.Length);
            else if (spawnAt != null && msg == $"Executing event at {spawnAt}")
            {
                if (!completed) calls++;
                spawnAt = null;
            }
            else if (msg == "Releasing chamber") break;
        }
        return calls;
    }

    // NoSpecialCalls on made-up log lines (no database)
    private static void CheckNoSpecialCalls()
    {
        static string L(string chamber, string msg) => $"[einherjar][{chamber}] {msg}";
        var released = new List<string>
        {
            "x", L("SIEGRUNE", "Reservation timeout, clearing chamber."), L("SIEGRUNE", "Chamber cleaned."),
            L("SIEGRUNE", "Releasing chamber"), L("SIEGRUNE", "Chamber cleaned."),
        };
        var fought = new List<string>
        {
            L("ROSSWEISSE", "Special mob will spawn at 1000"), L("GRIMGERDE", "Chamber cleaned."),
            L("ROSSWEISSE", "Executing event at 900"), L("ROSSWEISSE", "Executing event at 1000"),
            L("ROSSWEISSE", "All waves cleared! Showing armoury crate."), L("ROSSWEISSE", "Chamber cleaned."),
            L("ROSSWEISSE", "Releasing chamber"),
        };
        var late = new List<string>
        {
            L("ROSSWEISSE", "Special mob will spawn at 1000"),
            L("ROSSWEISSE", "All waves cleared! Showing armoury crate."),
            L("ROSSWEISSE", "Executing event at 1000"), L("ROSSWEISSE", "Chamber cleaned."),
        };
        Check("Einherjar's GetMobByID(0) count: a release is one call, up to the chamber's release (from the line " +
              "given); the spawn run before the last wave one more, after it none; another chamber's lines not counted",
              "1/0/3/2", $"{NoSpecialCalls(released, "SIEGRUNE", 0)}/{NoSpecialCalls(released, "SIEGRUNE", 3)}/" +
                         $"{NoSpecialCalls(fought, "ROSSWEISSE", 0)}/{NoSpecialCalls(late, "ROSSWEISSE", 0)}");
    }

    private static void Run(IDatabase db)
    {
        CheckNoSpecialCalls();
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        var log = _log;
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        zone.MobRng = new Random(7);
        // the earth clock (the host's and the scripts' GetSystemTime, JstMidnight) on the test clock, a JST noon
        long earth0 = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400) * 86400 + 3 * 3600;
        long EarthNow() => earth0 + (now - 1_000_000) / 1000;
        zone.UnixNow = EarthNow;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        scripts.VanaTicksNow = () => vanaTicks;
        scripts.UnixNowMs = () => EarthNow() * 1000 + (now % 1000);
        zone.InitScripts(scripts);
        // the scripts' math.random and the status effects' rolls from one seed (LIVE_SEED, else a fresh one),
        // printed so that a failing run can be replayed (the mobs' stay on 7)
        int seed = int.TryParse(Environment.GetEnvironmentVariable("LIVE_SEED"), out int given)
            ? given : Environment.TickCount & int.MaxValue;
        Console.WriteLine($"seed {seed}");
        scripts.SeedRandom(seed);
        zone.SeedEffectRng(seed);
        var lua = scripts.Lua;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)EarthNow()); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_content1");
            return string.Join(",", got);
        }
        // the GM !addmission past Darkness Named overwrites the CoP log's current mission (CLuaBaseEntity::addMission warns)
        var expectedWarnings = new HashSet<string> { "lua: addMission: overwriting current mission 101 of log 6 with 530" };
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                      !expectedWarnings.Contains(l)).Distinct().ToList();
        void NoErrors() => Check("no script errors", "", string.Join(" | ", ScriptErrors()));

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
        // the zone's timers: the clock moved on, then a tick
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
        ZoneInfo Info() => c.Instance?.Info ?? zone.ZoneInfoOf(c.Zone);
        NpcEntity Npc(uint id) => Info().NpcById[id];
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
        // a zone change the scripts asked for (setPos to another zone): the tick that zones the char, the next login
        void Rezone(bool endZoneIn = true)
        {
            Tick(400);
            Login(endZoneIn);
        }
        // the walk between places that are not the content's: a GM !pos
        void Warp(float x, float y, float z, ushort zoneId)
        {
            Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
            Rezone();
        }
        // the NPC of that name, a talkable one first (a cutscene-only twin shares the name)
        uint NpcId(string name) => Info().Npcs.Where(n => n.Name == name).OrderBy(n => n.Status == NpcEntity.StatusNormal ? 0 : 1).First().Id;
        MobEntity Mob(uint id) => Info().MobById[id];
        // a mob claimed by the char's swing (CBattleEntity::OnAttack's ClaimMob, the char kept facing it as the client
        // does; onMobDeath gets a player only through the claim)
        void ClaimBySwing(uint mobId)
        {
            var m = Mob(mobId);
            if (m.OwnerId == c.Id) return;
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
                if (c.Animation != Anim.Attack || c.BattleTarget != m)
                    Send(0x01A, 28, b =>
                    {
                        BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                        BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                        BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
                    });
                c.Hp = c.MaxHp;
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
        // a mob killed by the char: claimed, the damage, the death on the next tick, the defeat a second on; the
        // packets of the kill kept in killPackets
        var killPackets = new List<BasicPacket>();
        void KillByChar(uint mobId)
        {
            ClaimBySwing(mobId);
            killPackets.Clear();
            s.PacketList.Clear();
            Lua($"local mob = GetMobByID({mobId}, player:getInstance()) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            Tick(1000);
            killPackets.AddRange(s.PacketList);
            Tick(1000);
            killPackets.AddRange(s.PacketList.Where(p => !killPackets.Contains(p)));
            c.Hp = c.MaxHp;
        }

        // the event packet the server sent: 0x032 / 0x034 as op:entity:event[:params]
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
        string Cur() => c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "none";
        // the 0x05C an update sent: its eight params, the trailing zeros trimmed
        string Upd()
        {
            var up = Of(0x05C);
            if (up.Count == 0) return "none";
            var pars = Enumerable.Range(0, 8).Select(i => BitConverter.ToUInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(i)).ToString()).ToList();
            while (pars.Count > 0 && pars[^1] == "0") pars.RemoveAt(pars.Count - 1);
            return string.Join(",", pars);
        }
        string Var(string name) => DbRow(db, "value", $"FROM char_vars WHERE charid = ? AND varname = '{name}'");
        string Qty(int itemId) => DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND location = 0 AND itemId = {itemId}");
        string Gil() => DbRow(db, "quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0");
        byte Slot(int itemId, int atLeast = 1) => byte.Parse(DbRow(db, "slot", $"FROM char_inventory WHERE charid = ? AND location = 0 AND itemId = {itemId} AND quantity >= {atLeast} LIMIT 1"));
        // keyitems_t: per table of 512, the keyList bits then the seenList bits
        string DbKeyItem(int ki)
        {
            var k = DbBlob(db, "keyitems");
            int off = ki / 512 * 128 + ki % 512 / 8, bit = 1 << ki % 8;
            bool has = off < k.Length && (k[off] & bit) != 0, seen = off + 64 < k.Length && (k[off + 64] & bit) != 0;
            return $"{(has ? 1 : 0)}{(seen ? 1 : 0)}";
        }
        bool DbTitle(int title)
        {
            var t = DbBlob(db, "titles");
            return title / 8 < t.Length && (t[title / 8] & 1 << title % 8) != 0;
        }
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        int Item(string name) => int.Parse(Lua($"report(xi.item.{name})"));
        int TitleId(string name) => int.Parse(Lua($"report(xi.title.{name})"));
        bool HasKi(string name) => Lua($"report(tostring(player:hasKeyItem(xi.keyItem.{name})))") == "true";
        // the 0x02A TALKNUMWORK messages as message:param0
        string SpecialOf(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x02A)
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        string Special() => SpecialOf(s.PacketList);
        // the 0x02A messages with their first two params
        string Special2Of(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x02A)
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}:{BitConverter.ToUInt32(p.Buffer, 12)}"));
        int Text(ushort zoneId, string name) => int.Parse(Lua($"report(zones[{zoneId}].text.{name})"));
        // the char's position as x/z when within a yalm and a half of the one given (a beastman's blow may move it as it
        // zones out of a fight), else its own
        string Pos(float ex, float ez) => Math.Abs(c.X - ex) <= 1.5f && Math.Abs(c.Z - ez) <= 1.5f ? $"{ex}/{ez}" : $"{c.X}/{c.Z}";
        string Has(string effect) => Lua($"report(tostring(player:hasStatusEffect(xi.effect.{effect})))");
        // the treasure pool's items
        string Pool() => Lua("local p = player:getTreasurePool() if p then for _, t in ipairs(p:getItems()) do if t.id ~= 0 then report(t.id) end end end");

        // ---- Dynamis ----
        Fixture(db, SouthernSandoria, 140.0f, -2.0f, 121.0f, 200000);
        Console.WriteLine("Dynamis: the Trail Markings without a hourglass");
        Login();
        Check("in Southern San d'Oria", $"{SouthernSandoria}", c.Zone);
        uint markings = NpcId("Trail_Markings");
        Near(markings);
        Talk(markings);
        Check("no Prismatic Hourglass: DYNA_NPC_DEFAULT_MESSAGE, no event", $"{Text(SouthernSandoria, "DYNA_NPC_DEFAULT_MESSAGE")}:0/none", $"{Special()}/{Cur()}");
        NoErrors();

        Console.WriteLine("Dynamis: Lootblox in Davoi (the Prismatic Hourglass, a map, the ancient currency)");
        Warp(218.0f, -1.0f, -22.0f, Davoi);
        Check("in Davoi", $"{Davoi}", c.Zone);
        uint lootblox = NpcId("Lootblox");
        Near(lootblox);
        Talk(lootblox);
        Check("Lootblox without the shrouded sand: event 130", $"032:{lootblox}:130/130", $"{Ev()}/{Cur()}");
        EventEnd(130);
        // the vial of shrouded sand of the Xarcabard cutscene (GM !addkeyitem)
        Lua("player:addKeyItem(xi.keyItem.VIAL_OF_SHROUDED_SAND)");
        int bronze = Item("ORDELLE_BRONZEPIECE"), silver = Item("MONTIONT_SILVERPIECE"), gold = Item("RANPERRE_GOLDPIECE");
        Talk(lootblox);
        Check("with the sand: event 133 (the currencies, the rate, the hourglass costs)",
              $"034:{lootblox}:133:{bronze},100,{silver},100,{gold},50000,{Item("TIMELESS_HOURGLASS")},500000/133", $"{Ev()}/{Cur()}");
        EventEnd(133, 11, 1);
        Check("the main menu (update 11): the map mask 0, the gil", "0,200000", Upd());
        int mapSandy = Ki("MAP_OF_DYNAMIS_SAN_DORIA");
        EventEnd(133, (uint)mapSandy, 1);
        Check("a map bought (update with its key item): 10000 gil, the map, the mask bit 1", $"2,190000/190000/10",
              $"{Upd()}/{Gil()}/{DbKeyItem(mapSandy)}");
        EventEnd(133, 2, 1);
        Check("the shop page (update 2)", $"5,{Item("TWINCOON")},6,{Item("PILE_OF_RELIC_IRON")},7,{Item("JAR_OF_GOBLIN_GREASE")},8,{Item("GRIFFON_HIDE")}", Upd());
        EventEnd(133);
        Check("  finished", "none", Cur());
        // the hourglass: 50000 gil traded
        Trade(lootblox, (0, 50000));
        Check("50000 gil traded: event 134", $"032:{lootblox}:134/134", $"{Ev()}/{Cur()}");
        EventEnd(134);
        int hourglass = Ki("PRISMATIC_HOURGLASS");
        Check("event 134 finished: the Prismatic Hourglass, the gil taken, KEYITEM_OBTAINED", $"10/140000/{Text(Davoi, "KEYITEM_OBTAINED")}:{hourglass}",
              $"{DbKeyItem(hourglass)}/{Gil()}/{Special()}");
        Trade(lootblox, (0, 50000));
        Check("a second 50000 gil with the hourglass held: nothing", "none/140000", $"{Cur()}/{Gil()}");
        NoErrors();
        // the currency exchanges: 100 bronzepieces to a silverpiece, a goldpiece to 100 silverpieces, 5 silverpieces
        // to a Twincoon
        Lua($"player:addItem({bronze}, 99) player:addItem({bronze}, 1) player:addItem({gold}, 1)");
        Trade(lootblox, (Slot(bronze), 99), ((byte)(Slot(bronze) + 1), 1));
        Check("100 bronzepieces traded: event 135 (the rate)", $"034:{lootblox}:135:100/135", $"{Ev()}/{Cur()}");
        EventEnd(135);
        Check("event 135 finished: a silverpiece for the bronzepieces, ITEM_OBTAINED", $"0/1/{Text(Davoi, "ITEM_OBTAINED")}:{silver}",
              $"{Qty(bronze)}/{Qty(silver)}/{Special()}");
        Trade(lootblox, (Slot(gold), 1));
        Check("a goldpiece traded: event 138 (gold, silver, the rate)", $"034:{lootblox}:138:{gold},{silver},100/138", $"{Ev()}/{Cur()}");
        EventEnd(138);
        Check("event 138 finished: 100 silverpieces (99 + 1), ITEMS_OBTAINED", $"0/101/{Text(Davoi, "ITEMS_OBTAINED")}:{silver}",
              $"{Qty(gold)}/{Qty(silver)}/{Special()}");
        Trade(lootblox, (Slot(silver, 5), 5));
        Check("5 silverpieces traded: event 137 (silver, 5, the Twincoon)", $"034:{lootblox}:137:{silver},5,{Item("TWINCOON")}/137", $"{Ev()}/{Cur()}");
        EventEnd(137);
        Check("event 137 finished: the Twincoon for 5 silverpieces", "96/1", $"{Qty(silver)}/{Qty(Item("TWINCOON"))}");
        // the Timeless Hourglass returned: the trade starts baseCs + 23, the finish refunds on baseCs + 13 (a script bug:
        // no refund ever)
        int timeless = Item("TIMELESS_HOURGLASS");
        Lua($"player:addItem({timeless})");
        Trade(lootblox, (Slot(timeless), 1));
        Check("a Timeless Hourglass traded: event 153 (baseCs + 23)", $"032:{lootblox}:153/153", $"{Ev()}/{Cur()}");
        EventEnd(153);
        Check("  finished: no refund, the hourglass kept (onEventFinish refunds on baseCs + 13, a script bug)", "140000/1", $"{Gil()}/{Qty(timeless)}");
        NoErrors();

        Console.WriteLine("Dynamis: the Trail Markings' entry menu, into Dynamis - San d'Oria");
        Warp(140.0f, -2.0f, 121.0f, SouthernSandoria);
        Near(markings);
        Talk(markings);
        int vial = Ki("VIAL_OF_SHROUDED_SAND");
        Check("with the hourglass: DYNA_NPC_DEFAULT_MESSAGE, then event 961 (bit 1, a first visit to any, the hourglass, no subjob option, the sand, the hourglass items)",
              $"{Text(SouthernSandoria, "DYNA_NPC_DEFAULT_MESSAGE")}:0/034:{markings}:961:1,1,{hourglass},0,0,{vial},4236,4237/961", $"{Special()}/{Ev()}/{Cur()}");
        long midnight = long.Parse(Lua("report(JstMidnight())"));
        EventEnd(961, 0);
        Check("event 961 finished (enter): Dynamis_Status bit 1, Dynamis_Entry, dynaWaitxDay the last JST midnight, the first-visit cutscene 692",
              $"2/1/{midnight - 86400}/692", $"{Var("Dynamis_Status")}/{Var("Dynamis_Entry")}/{Var("dynaWaitxDay")}/{Cur()}");
        Rezone(false);
        Check("setPos: Dynamis - San d'Oria at the entry, no zone-in event", $"{DynamisSandoria}/162/162/none",
              $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Cur()}");
        Check("zoneOnZoneIn: the DYNAMIS effect (an hour, 3 s ticks, no icon), Dynamis_Entry and Dynamis_subjob cleared, no subjob restriction",
              "true/3600/3000/0/-/-/false",
              $"{Has("DYNAMIS")}/" + Lua("local e = player:getStatusEffect(xi.effect.DYNAMIS) report(math.floor(e:getTimeRemaining() / 1000)) report(e:getTickCount() == nil and -1 or e:getTick()) report(e:getIcon())").Replace(",", "/") +
              $"/{Var("Dynamis_Entry")}/{Var("Dynamis_subjob")}/{Has("SJ_RESTRICTION")}");
        Check("  the region: Dynamis (23), the dynamis_lasttimeupdate local var", "23/3600",
              Lua("report(player:getCurrentRegion()) report(player:getLocalVar('dynamis_lasttimeupdate'))").Replace(",", "/"));
        s.PacketList.Clear();
        var begin = new List<BasicPacket>();
        for (int i = 0; i < 15; i++) { Tick(400); begin.AddRange(s.PacketList); s.PacketList.Clear(); }
        Check("the zone-in timer 5.5 s on: DYNAMIS_TIME_BEGIN (60 minutes, the hourglass)", $"{Text(DynamisSandoria, "DYNAMIS_TIME_BEGIN")}:60:{hourglass}",
              Special2Of(begin));
        NoErrors();

        Console.WriteLine("Dynamis: zoneOnInitialize's time extensions and refill statues");
        var teIds = new uint[] { 17535026, 17535057, 17535139, 17535131, 17535128 };
        Check("the five time extension mobs spawned", "True", teIds.All(id => Mob(id).IsSpawned && Mob(id).IsAlive));
        var statueGroups = new[] { new uint[] { 17535003, 17535004, 17535005 }, new uint[] { 17535113, 17535114, 17535115 }, new uint[] { 17535154, 17535155 },
                                   new uint[] { 17535166, 17535167 }, new uint[] { 17535193, 17535194 }, new uint[] { 17535205, 17535206 } };
        Check("one refill statue of each group spawned", "1,1,1,1,1,1", string.Join(",", statueGroups.Select(g => g.Count(id => Mob(id).IsSpawned))));
        // refillStatueOnSpawn: the eye of its entry (RED 1, BLUE 2, GREEN 3 in the group order)
        var eyes = new Dictionary<uint, int> { [17535003] = 1, [17535004] = 2, [17535005] = 3, [17535113] = 1, [17535114] = 2, [17535115] = 3, [17535154] = 1,
                                               [17535155] = 3, [17535166] = 1, [17535167] = 2, [17535193] = 1, [17535194] = 3, [17535205] = 1, [17535206] = 2 };
        Check("  refillStatueOnSpawn: each statue's eye (its animation sub)", "True",
              statueGroups.SelectMany(g => g).Where(id => Mob(id).IsSpawned).All(id => Mob(id).AnimationSub == eyes[id]));
        NoErrors();

        // the beastmen around (1.5x base damage, linked, their TP moves) can KO the char within one of the fights'
        // ticks: the KO loses its claim, so onMobDeath and the DEATH listener get no killer (no granules, no currency),
        // and the char's swings and Provoke are refused. The char kept alive through the fights (a GM's setUnkillable),
        // as through the time-out below
        Lua("player:setUnkillable(true)");
        Console.WriteLine("Dynamis: a time extension killed (the granules, the time, the respawn)");
        int crimson = Ki("CRIMSON_GRANULES_OF_TIME");
        ClaimBySwing(teIds[0]);
        long leftBefore = long.Parse(Lua("report(player:getStatusEffect(xi.effect.DYNAMIS):getTimeRemaining())"));
        KillByChar(teIds[0]);
        long leftAfter = long.Parse(Lua("report(player:getStatusEffect(xi.effect.DYNAMIS):getTimeRemaining())"));
        Check("timeExtensionOnDeath: the Crimson Granules, KEYITEM_OBTAINED, DYNAMIS_TIME_EXTEND 10",
              $"10/True/True", $"{DbKeyItem(crimson)}/{SpecialOf(killPackets).Contains($"{Text(DynamisSandoria, "KEYITEM_OBTAINED")}:{crimson}")}/" +
              $"{SpecialOf(killPackets).Contains($"{Text(DynamisSandoria, "DYNAMIS_TIME_EXTEND")}:10")}");
        Check("  the effect 10 minutes longer (setDuration), dynamis_lasttimeupdate its seconds left", "True/True",
              $"{leftAfter - leftBefore >= 598_000 && leftAfter - leftBefore <= 600_000}/{Lua("report(player:getLocalVar('dynamis_lasttimeupdate'))") == (leftAfter / 1000).ToString() || Math.Abs(long.Parse(Lua("report(player:getLocalVar('dynamis_lasttimeupdate'))")) - leftAfter / 1000) <= 3}");
        Check("  the TE respawns in 85 s (setRespawnTime)", "True/False", $"{Mob(teIds[0]).IsDead || !Mob(teIds[0]).IsSpawned}/{Mob(teIds[0]).IsSpawned && Mob(teIds[0]).IsAlive}");
        long teDeath = now;
        for (int i = 0; i < 200 && !(Mob(teIds[0]).IsSpawned && Mob(teIds[0]).IsAlive); i++) Tick(1000);
        // registered on the despawn (CDespawnState::init, the 15 s death state over), up on the spawn handler's 30 s tick
        Check("  respawned 85 s after its despawn (the spawn handler's 30 s tick)", "True/True",
              $"{Mob(teIds[0]).IsSpawned && Mob(teIds[0]).IsAlive}/{(now - teDeath) / 1000 is >= 98 and <= 135}");
        KillByChar(teIds[0]);
        Check("killed again with the granules held: no extension", "False", SpecialOf(killPackets).Contains($"{Text(DynamisSandoria, "DYNAMIS_TIME_EXTEND")}:"));
        foreach (var te in teIds.Skip(1)) KillByChar(te);
        Check("the other four killed: all five granules", "10,10,10,10,10",
              string.Join(",", new[] { "CRIMSON", "AZURE", "AMBER", "ALABASTER", "OBSIDIAN" }.Select(g => DbKeyItem(Ki(g + "_GRANULES_OF_TIME")))));
        long fullLeft = long.Parse(Lua("report(player:getStatusEffect(xi.effect.DYNAMIS):getTimeRemaining())"));
        Check("  the time 60 + 10 + 10 + 10 + 15 + 15 minutes less what has passed", "True", fullLeft > 6600_000 && fullLeft <= 7200_000);
        NoErrors();

        Console.WriteLine("Dynamis: a refill statue killed (the eye's restore, the eye put out, the group's next statue)");
        {
            var statue = statueGroups.SelectMany(g => g).Where(id => Mob(id).IsSpawned).OrderBy(id => eyes[id] == 3 ? 0 : eyes[id] == 2 ? 1 : 2).First();
            var group = statueGroups.First(g => g.Contains(statue));
            int eye = eyes[statue];
            ClaimBySwing(statue);
            c.Hp = c.MaxHp / 2;
            Lua("player:setMP(0)");
            int hpBefore = c.Hp, mpBefore = c.Mp;
            s.PacketList.Clear();
            Lua($"local mob = GetMobByID({statue}) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
            Tick(1000);
            Tick(1000);
            var recover = Of(0x029).Where(p => BitConverter.ToUInt16(p.Buffer, 24) is 24 or 25).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 16)}").ToList();
            string expected = eye == 3 ? $"24:{c.MaxHp - hpBefore}" : eye == 2 ? $"25:{c.MaxMp - mpBefore}/{c.MaxMp}" : "/";
            string actual = eye == 2 ? $"{string.Join(",", recover)}/{c.Mp}" : $"{string.Join(",", recover)}/";
            // the HP restored is the HP lost at the death: the beastmen around keep hitting (at least the half taken)
            if (eye == 3) actual = recover.Count == 1 && recover[0].StartsWith("24:") && int.Parse(recover[0][3..]) >= c.MaxHp - hpBefore ? $"24:{c.MaxHp - hpBefore}" : string.Join(",", recover);
            Check($"refillStatueOnDeath ({(eye == 3 ? "green: the HP" : eye == 2 ? "blue: the MP" : "red: nothing")} restored to those within 30', RECOVERS_*)", expected, actual);
            Check("  the eye put out (setAnimationSub NONE), dead", "0/True", $"{Mob(statue).AnimationSub}/{Mob(statue).IsDead}");
            // registered now for another statue of the group, else on its despawn (CDespawnState::init) 18 s on
            for (int i = 0; i < 20; i++) Tick(1000);
            string pending = Lua(string.Join(" ", group.Select(id => $"report(GetMobByID({id}):getRespawnTime())")));
            Check("  a statue of its group pending within 5 minutes (setRespawnTime 300)", "True", pending.Split(',').Any(t => int.TryParse(t, out int ms) && ms is > 250 and <= 300));
            c.Hp = c.MaxHp;
        }
        NoErrors();

        // the beastman to fight: the nearest one at home, inside its roam region (else within its roam distance of its
        // spawn point) and not despawning. One a fight pulled out of its region despawns on its next idle tick
        // (CMobController::DoRoamTick: out of its region it does not walk back, it despawns and respawns inside), and
        // the claim and its local vars (dynamis_proc) go with it
        bool AtHome(MobEntity m)
        {
            if (m.RoamRegion != null) return m.RoamRegion.DistanceOutside(m.X, m.Y, m.Z) <= 0;
            float dx = m.X - m.SpawnX, dz = m.Z - m.SpawnZ;
            return MathF.Sqrt(dx * dx + dz * dz) <= m.MaxRoamDistance;
        }
        MobEntity NearestBeastman(Func<MobEntity, bool> also) => Info().Mobs
            .Where(m => m.Name.StartsWith("Vanguard_") && m.IsSpawned && m.IsAlive && !m.IsNotorious &&
                        m.FadeAtMs == 0 && AtHome(m) && also(m))
            .OrderBy(m => (m.X - c.X) * (m.X - c.X) + (m.Z - c.Z) * (m.Z - c.Z)).FirstOrDefault();

        Console.WriteLine("Dynamis: a beastman's proc (procMonster with five granules) and its currency");
        {
            var orc = NearestBeastman(_ => true);
            ClaimBySwing(orc.Id);
            Check($"a {orc.Name}: an orc, no gil (GIL_MAX -1), 1.5x base damage", $"{Lua("report(xi.mobFamily.ORC)")}/-1/150",
                  Lua($"local m = GetMobByID({orc.Id}) report(m:getFamily()) report(m:getMobMod(xi.mobMod.GIL_MAX)) report(m:getMobMod(xi.mobMod.BASE_DAMAGE_MULTIPLIER))").Replace(",", "/"));
            Lua($"xi.dynamis.procMonster(GetMobByID({orc.Id}), player)");
            Check("  procMonster (five granules, no subjob restriction): a red proc, terror for 30 s", "3/true",
                  Lua($"local m = GetMobByID({orc.Id}) report(m:getLocalVar('dynamis_proc')) report(tostring(m:hasStatusEffect(xi.effect.TERROR)))").Replace(",", "/"));
            KillByChar(orc.Id);
            int pooled = Pool().Split(',', StringSplitOptions.RemoveEmptyEntries).Count(i => i == bronze.ToString());
            Check("  killed: the DEATH listener's treasure (a red proc's sure bronzepiece) in the pool or the inventory", "True",
                  pooled + int.Parse(Qty(bronze)) >= 1, $"pool=[{Pool()}] qty={Qty(bronze)} free={Lua("report(player:getFreeSlotsCount())")} proc={Lua($"report(GetMobByID({orc.Id}):getLocalVar('dynamis_proc'))")} " +
                  $"owner={orc.OwnerId} spawned={orc.IsSpawned} char hp={c.Hp} anim={c.Animation} " +
                  string.Join(" | ", log.TakeLast(8)));
        }
        NoErrors();

        Console.WriteLine("Dynamis: a job ability on a THF beastman (the ABILITY_TAKE listener's proc)");
        {
            var thf = NearestBeastman(m => m.MJob is 6 or 2 or 9 or 11 or 13);
            Check("a spawned 'ja' beastman (MNK / THF / BST / RNG / NIN)", "True", thf != null);
            if (thf != null)
            {
                ClaimBySwing(thf.Id);
                // the client's /ja Provoke, until it reaches the beastman (its ABILITY_TAKE, which a test listener
                // notes). A beastman's Sleep, Stun or Lullaby on the char has the packet validator refuse it
                // (PreventAction, no message), and its Paralysis interrupts it with the recast spent
                // (CCharEntity::OnAbility's IsParalyzed): the player waits until the char can act and the recast is
                // over, and uses it again. The 20% roll made sure each time (math.randomInt's low end, a GM's loaded
                // dice), then put back
                Lua($"GetMobByID({thf.Id}):addListener('ABILITY_TAKE', 'VERIFY_PROVOKE_TAKEN', " +
                    "function() _G.__provokeTaken = true end)");
                bool Taken() => Lua("report(tostring(_G.__provokeTaken == true))") == "true";
                bool Recast() => Lua("report(tostring(player:hasRecast(xi.recast.ABILITY, 5)))") == "true";
                var provoked = new List<BasicPacket>();
                int sends = 0;
                for (; sends < 5 && !Taken(); sends++)
                {
                    for (int i = 0; i < 40 && (c.StatusEffects.HasPreventActionEffect() || Recast()); i++) Tick(1000);
                    Lua("_G.__randomInt = math.randomInt math.randomInt = function(a, b) return a end");
                    s.PacketList.Clear();
                    Send(0x01A, 28, b =>
                    {
                        BitConverter.GetBytes(thf.Id).CopyTo(b, 4);
                        BitConverter.GetBytes(thf.TargId).CopyTo(b, C2sAction.OffActIndex);
                        BitConverter.GetBytes((ushort)ActionId.JobAbility).CopyTo(b, C2sAction.OffActionID);
                        C2sAction.SetSkillId(b, 35);
                    });
                    for (int i = 0; i < 3; i++) { Tick(400); provoked.AddRange(s.PacketList); s.PacketList.Clear(); }
                    Lua("math.randomInt = _G.__randomInt");
                }
                Lua($"GetMobByID({thf.Id}):removeListener('VERIFY_PROVOKE_TAKEN') _G.__provokeTaken = nil");
                // on a failure: the sends, the 0x029 messages of their ticks, the 0x028s sent (and whether one is the
                // char's), the char's effects and state, the mob's distance, owner, job and state
                string ProvokeDetail()
                {
                    var msgs = provoked.Where(p => Opcode(p) == 0x029).Select(p => BitConverter.ToUInt16(p.Buffer, 24));
                    var acts = provoked.Where(p => Opcode(p) == 0x028).ToList();
                    float dx = thf.X - c.X, dy = thf.Y - c.Y, dz = thf.Z - c.Z;
                    string effects =
                        Lua("for _, e in ipairs(player:getStatusEffects()) do report(e:getEffectType()) end");
                    return $"sends={sends} msg029=[{string.Join(",", msgs)}] act028={acts.Count} (char's: " +
                           $"{acts.Any(p => BitConverter.ToUInt32(p.Buffer, 5) == c.Id)}) " +
                           $"effects=[{effects}] char anim={c.Animation} hp={c.Hp} target={c.BattleTarget?.Id} " +
                           $"mob dist={MathF.Sqrt(dx * dx + dy * dy + dz * dz):0.0} owner={thf.OwnerId} " +
                           $"job={thf.MJob} " +
                           $"spawned={thf.IsSpawned} alive={thf.IsAlive} anim={thf.Animation}";
                }
                Check($"  Provoke on the {thf.Name}: ABILITY_TAKE's procMonster, a red proc (five granules) and terror", "3/true",
                      Lua($"local m = GetMobByID({thf.Id}) report(m:getLocalVar('dynamis_proc')) report(tostring(m:hasStatusEffect(xi.effect.TERROR)))").Replace(",", "/"),
                      ProvokeDetail());
                KillByChar(thf.Id);
            }
        }
        NoErrors();

        Console.WriteLine("Dynamis: the Overlord's Tombstone (the ??? traded to, the mega boss, its win)");
        {
            uint qm0 = 17535224, overlord = 17534977;
            int coin = 3353;
            Near(qm0);
            Talk(qm0);
            Check("the ??? (qm0) talked to: event 102 with the items it takes", $"034:{qm0}:102:3353,3404,3405,3406,3407,3408/102", $"{Ev()}/{Cur()}");
            EventEnd(102);
            Lua($"player:addItem({coin})");
            Trade(qm0, (Slot(coin), 1));
            Check("the item traded: the Overlord's Tombstone up and claimed, the item taken", $"True/True/0",
                  $"{Mob(overlord).IsSpawned && Mob(overlord).IsAlive}/{Mob(overlord).OwnerId == c.Id}/{Qty(coin)}");
            for (int i = 0; i < 5; i++) Tick(1000);
            Check("  onMobEngage: its two helpers up", "True", Mob(overlord + 1).IsSpawned && Mob(overlord + 2).IsSpawned);
            int title = TitleId("DYNAMIS_SAN_DORIA_INTERLOPER"), scepter = Ki("HYDRA_CORPS_COMMAND_SCEPTER");
            KillByChar(overlord);
            Check("megaBossOnDeath: the title, the Hydra Corps Command Scepter, DynaSandoria_Win 1", "True/10/1",
                  $"{DbTitle(title)}/{DbKeyItem(scepter)}/{Var("DynaSandoria_Win")}");
            for (int i = 0; i < 5; i++) Tick(1000);
            Check("  the helpers despawned with it", "False/False", $"{Mob(overlord + 1).IsSpawned && Mob(overlord + 1).IsAlive}/{Mob(overlord + 2).IsSpawned && Mob(overlord + 2).IsAlive}");
        }
        NoErrors();

        Console.WriteLine("Dynamis: the time runs out (the warnings, the expiry, ejected)");
        {
            // the wait out of the beastmen's reach, by the entry (a GM's !hide)
            MoveTo(161.8f, -2.0f, 161.7f);
            Lua("player:setUnkillable(true)");
            var warnings = new List<BasicPacket>();
            long Left() => long.Parse(Lua("local e = player:getStatusEffect(xi.effect.DYNAMIS) report(e and e:getTimeRemaining() or -1)"));
            for (int i = 0; i < 400 && Left() > 700_000; i++) { s.PacketList.Clear(); Tick(60_000); warnings.AddRange(s.PacketList); }
            for (int i = 0; i < 400 && Left() > 0 && Cur() != "100"; i++) { s.PacketList.Clear(); Tick(3000); warnings.AddRange(s.PacketList); }
            int u1 = Text(DynamisSandoria, "DYNAMIS_TIME_UPDATE_1"), u2 = Text(DynamisSandoria, "DYNAMIS_TIME_UPDATE_2");
            Check("the DYNAMIS effect's ticks: 10 and 5 minutes, 1 minute (UPDATE_1), 30 and 10 seconds, then DYNAMIS_TIME_EXPIRED",
                  $"{u2}:10:1,{u2}:5:1,{u1}:1:1,{u2}:30:0,{u2}:10:0,{Text(DynamisSandoria, "DYNAMIS_TIME_EXPIRED")}:0:0",
                  Special2Of(warnings.Where(p => Opcode(p) != 0x02A || (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) is not (6395 or 6398))));
            Check("  onEffectLose: the effect gone, the granules taken, event 100 (startCutscene)", "false/00,00,00,00,00/100",
                  $"{Has("DYNAMIS")}/{string.Join(",", new[] { "CRIMSON", "AZURE", "AMBER", "ALABASTER", "OBSIDIAN" }.Select(g => DbKeyItem(Ki(g + "_GRANULES_OF_TIME"))).Select(k => k == "01" ? "00" : k))}/{Cur()}");
            EventEnd(100);
            Rezone();
            Lua("player:setUnkillable(false)");
            c.Hp = c.MaxHp;
            Check("event 100 finished: zoneOnEventFinish's setPos out to the eject position in Southern San d'Oria", $"{SouthernSandoria}/161/161",
                  $"{c.Zone}/{Pos(161, 161)}");
        }
        NoErrors();

        Console.WriteLine("Dynamis: the Trail Markings after the win (the victory cutscene, the wait to re-enter)");
        Near(markings);
        Talk(markings);
        Check("DynaSandoria_Win: the victory cutscene 698 (the scepter, 0, the hourglass, the sand)",
              $"034:{markings}:698:{Ki("HYDRA_CORPS_COMMAND_SCEPTER")},0,{hourglass},{vial}/698/", $"{Ev()}/{Cur()}/{Special()}");
        EventEnd(698);
        Check("  finished: DynaSandoria_Win cleared", "-", Var("DynaSandoria_Win"));
        Talk(markings);
        long dayRemaining = (midnight - EarthNow()) / 3456;
        Check("again before the next JST midnight: DYNA_NPC_DEFAULT_MESSAGE, YOU_CANNOT_ENTER_DYNAMIS (the Vana'diel days left, bit 1), no event",
              $"{Text(SouthernSandoria, "DYNA_NPC_DEFAULT_MESSAGE")}:0:0,{Text(SouthernSandoria, "YOU_CANNOT_ENTER_DYNAMIS")}:{dayRemaining}:1/none",
              $"{Special2Of(s.PacketList)}/{Cur()}");
        NoErrors();
        Console.WriteLine("Dynamis: into Dynamis - Valkurm without the effect (a GM's !zone): event 100 and out");
        Lua($"player:setPos(100, -8, 131, 0, {DynamisValkurm})");
        Rezone(false);
        Check("zoneOnZoneIn with no DYNAMIS effect: the eject event 100 at the zone-in", $"{DynamisValkurm}/100", $"{c.Zone}/{Cur()}");
        EventEnd(100);
        Rezone();
        Check("  finished: zoneOnEventFinish's setPos to the eject position in Valkurm Dunes", $"{ValkurmDunes}/119/131",
              $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        NoErrors();

        Console.WriteLine("Dynamis: Valkurm's Hieroglyphics (the CoP requirement, the wait, the subjob option)");
        uint hieroglyphics = NpcId("Hieroglyphics");
        Near(hieroglyphics);
        Talk(hieroglyphics);
        Check("Darkness Named not done: DYNA_NPC_DEFAULT_MESSAGE, no event (the CoP requirement)", $"{Text(ValkurmDunes, "DYNA_NPC_DEFAULT_MESSAGE")}:0/none",
              $"{Special()}/{Cur()}");
        // CoP 3-5 done: the CoP log past it (GM !addmission; CoP's completion is the current mission past it)
        Lua("player:addMission(xi.mission.log_id.COP, xi.mission.id.cop.THREE_PATHS)");
        Talk(hieroglyphics);
        Check("Darkness Named done, before the next JST midnight: YOU_CANNOT_ENTER_DYNAMIS (bit 7)",
              $"{Text(ValkurmDunes, "DYNA_NPC_DEFAULT_MESSAGE")}:0:0,{Text(ValkurmDunes, "YOU_CANNOT_ENTER_DYNAMIS")}:{(midnight - EarthNow()) / 3456}:7/none",
              $"{Special2Of(s.PacketList)}/{Cur()}");
        // the earth clock past the next JST midnight
        now += (midnight - EarthNow() + 120) * 1000;
        Tick(400);
        Talk(hieroglyphics);
        Check("past the midnight: event 58 (bit 7, a first visit here, the hourglass, the subjob option)",
              $"034:{hieroglyphics}:58:7,2,{hourglass},1,0,{vial},4236,4237/58", $"{Ev()}/{Cur()}");
        long midnight2 = long.Parse(Lua("report(JstMidnight())"));
        EventEnd(58, 1);
        // handleEntryTime records the JST day only when dynaWaitxDay + 24 h is strictly before it: a day on, the record
        // (the last midnight less a day) plus 24 h is the last midnight itself, so it is kept (a script bug: the wait
        // is not renewed by an entry the day after the last)
        Check("event 58 finished (enter, subjobs restricted): Dynamis_subjob 1, Dynamis_Status bits 1 and 7, dynaWaitxDay kept (handleEntryTime's <), cutscene 33",
              $"1/{2 | 1 << 7}/{midnight - 86400}/33", $"{Var("Dynamis_subjob")}/{Var("Dynamis_Status")}/{Var("dynaWaitxDay")}/{Cur()}");
        int hpUnrestricted = c.MaxHp;
        Rezone(false);
        Check("Dynamis - Valkurm at the entry", $"{DynamisValkurm}/100/131/none", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Cur()}");
        Check("zoneOnZoneIn: DYNAMIS and SJ_RESTRICTION, the subjob and its level gone (GetSJob / GetSLevel), the MNK's HP with it",
              "true/true/0/0/True", $"{Has("DYNAMIS")}/{Has("SJ_RESTRICTION")}/{Lua("report(player:getSubJob()) report(player:getSubLvl())").Replace(",", "/")}/{c.MaxHp < hpUnrestricted}");
        var cliStatus = loginPackets.LastOrDefault(p => Opcode(p) == 0x061);
        Check("  the char's status (0x061) and the zone-in's 0x00A: no sub job", "0/0",
              $"{(cliStatus == null ? -1 : cliStatus.Buffer[4 + 10])}/{(cliStatus == null ? -1 : cliStatus.Buffer[4 + 11])}");
        Lua("player:setUnkillable(true)");
        NoErrors();

        Console.WriteLine("Dynamis: Valkurm's grouped time extension (one of four up, a new one picked on the kill)");
        {
            var obsidian = new uint[] { 16937500, 16937525, 16937550, 16937575 };
            Check("one of the four Obsidian time extensions up", "1", obsidian.Count(id => Mob(id).IsSpawned && Mob(id).IsAlive));
            uint up = obsidian.First(id => Mob(id).IsSpawned && Mob(id).IsAlive);
            KillByChar(up);
            Check("killed: the Obsidian Granules, 20 minutes more", $"10/True", $"{DbKeyItem(Ki("OBSIDIAN_GRANULES_OF_TIME"))}/" +
                  SpecialOf(killPackets).Contains($"{Text(DynamisValkurm, "DYNAMIS_TIME_EXTEND")}:20"));
            long killed = now;
            for (int i = 0; i < 200 && !obsidian.Any(id => Mob(id).IsSpawned && Mob(id).IsAlive); i++) Tick(1000);
            uint next = obsidian.FirstOrDefault(id => Mob(id).IsSpawned && Mob(id).IsAlive);
            Check("  timeExtensionOnDeath's pick of the group up 85 s on (the others' respawn disallowed)", "1/True/True",
                  $"{obsidian.Count(id => Mob(id).IsSpawned && Mob(id).IsAlive)}/{(now - killed) / 1000 is >= 85 and <= 135}/" +
                  $"{obsidian.Where(id => id != next).All(id => !Mob(id).AllowRespawn)}");
        }
        NoErrors();

        Console.WriteLine("Dynamis: a logout inside (OFFLINE_TICK) and back");
        {
            long before = long.Parse(Lua("report(player:getStatusEffect(xi.effect.DYNAMIS):getTimeRemaining())"));
            zone.OnCharTimeout(s, false);
            s = null;
            Sql(db, "UPDATE char_stats SET zoning = 2 WHERE charid = ?", BoundValue.Of(CharId));
            now += 300_000;
            Login();
            long after = long.Parse(Lua("report(player:getStatusEffect(xi.effect.DYNAMIS):getTimeRemaining())"));
            Check("logged back in 5 minutes on: in Dynamis - Valkurm, no event 100, DYNAMIS with the offline time gone, SJ_RESTRICTION kept",
                  $"{DynamisValkurm}/none/True/true", $"{c.Zone}/{Cur()}/{Math.Abs(before - 300_000 - after) <= 2000}/{Has("SJ_RESTRICTION")}");
            Lua("player:setUnkillable(true)");
        }
        NoErrors();

        Console.WriteLine("Dynamis: the Somnial Threshold (the subjob unlocked, then out)");
        {
            uint threshold = NpcId("Somnial_Threshold");
            Near(threshold);
            Talk(threshold);
            Check("the Somnial Threshold with the subjob restricted: event 101 (0x27, 1, menu 7)", $"034:{threshold}:101:39,1,7/101", $"{Ev()}/{Cur()}");
            EventEnd(101, 2);
            Check("event 101 finished (option 2): DYNAMIS_SUB_UNLOCKED, the restriction gone, the subjob, its level and HP back (recalculateStats)",
                  $"{Text(DynamisValkurm, "DYNAMIS_SUB_UNLOCKED")}:0/false/2/37/{hpUnrestricted}",
                  $"{Special()}/{Has("SJ_RESTRICTION")}/{Lua("report(player:getSubJob()) report(player:getSubLvl())").Replace(",", "/")}/{c.MaxHp}");
            Talk(threshold);
            Check("again: event 101 (0x27, 0, menu 5)", $"034:{threshold}:101:39,0,5/101", $"{Ev()}/{Cur()}");
            EventEnd(101, 1);
            Check("event 101 finished (option 1, leave): the eject event 100", "100", Cur());
            EventEnd(100);
            Rezone();
            Lua("player:setUnkillable(false)");
            c.Hp = c.MaxHp;
            Check("event 100 finished: Valkurm Dunes at the eject position, DYNAMIS kept until its tick", $"{ValkurmDunes}/119/131/true",
                  $"{c.Zone}/{Pos(119, 131)}/{Has("DYNAMIS")}");
            s.PacketList.Clear();
            for (int i = 0; i < 10; i++) Tick(1000);
            Check("  its tick out of the Dynamis region: removed silently, the granules taken, no expiry message, no event",
                  "false/00/none/False", $"{Has("DYNAMIS")}/{DbKeyItem(Ki("OBSIDIAN_GRANULES_OF_TIME")).Replace("01", "00")}/{Cur()}/" +
                  $"{Special().Contains($"{Text(DynamisValkurm, "DYNAMIS_TIME_EXPIRED")}:")}");
            Near(hieroglyphics);
            Talk(hieroglyphics);
            Check("the Hieroglyphics again the same JST day: the menu again, the wait not renewed (handleEntryTime's <)",
                  $"034:{hieroglyphics}:58:7,3,{hourglass},1,0,{vial},4236,4237/58", $"{Ev()}/{Cur()}");
            EventEnd(58, 2);
        }
        NoErrors();
        // ---- Einherjar ----
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = 996052;
        const ushort Hazhalm = 78;
        Fixture(db, Hazhalm, 652.0f, -272.6f, -104.9f, 10000);
        // a chamber planned without a special mob (getRandomSpecial's 2% 'may not spawn' row, ids 0) calls
        // GetMobByID(0) at each despawnSpecialMob (cleanChamber's, "Chamber cleaned.", and cycleWave's at the last
        // wave, "All waves cleared!") and when its engage's queued spawn runs before the chamber is completed: the
        // script's `if specialId` takes the 0 for a mob, and luautils warns as the C++'s does. Kept apart from the
        // script errors while the Einherjar scenarios run; at their end the warnings are counted against the calls
        // each such chamber's own log lines show (NoSpecialCalls), so another script's GetMobByID(0) is not hidden
        const string NoSpecialWarning = "luautils::GetMobByID Mob doesn't exist (0)";
        int einherjarFrom = log.Count;
        // each chamber planned without a special mob: its id, its xi.einherjar.chamber name and the log line it was
        // planned at
        var noSpecial = new List<(int Chamber, string Name, int From)>();
        void NoteSpecial(int chamber)
        {
            if (Lua($"report(xi.einherjar.getChamber({chamber}).encounters.special)") != "0") return;
            string name = Lua($"for name, id in pairs(xi.einherjar.chamber) do if id == {chamber} then report(name) " +
                              "end end");
            noSpecial.Add((chamber, name, log.Count));
            expectedWarnings.Add(NoSpecialWarning);
        }
        Login();
        Console.WriteLine("Einherjar: the entry gate (_260) disabled, then the requirements");
        Check("in the Hazhalm Testing Grounds by the gate", $"{Hazhalm}/none", $"{c.Zone}/{Cur()}");
        uint gate = NpcId("_260");
        int smoldering = Item("SMOLDERING_LAMP"), glowing = Item("GLOWING_LAMP");
        Lua($"player:addItem({smoldering})");
        Near(gate);
        Talk(gate);
        Check("the gate talked to: GATE_FIRMLY_CLOSED", $"{Text(Hazhalm, "GATE_FIRMLY_CLOSED")}:0/none", $"{Special()}/{Cur()}");
        Trade(gate, (Slot(smoldering), 1));
        Check("a Smoldering Lamp traded with Einherjar disabled (EINHERJAR_ENABLED false by default): nothing", "/none/1", $"{Special()}/{Cur()}/{Qty(smoldering)}");
        // a GM's server setting: Einherjar on
        Lua("xi.einherjar.settings.EINHERJAR_ENABLED = true");
        Trade(gate, (Slot(smoldering), 1));
        Check("enabled, Immortal Sentries not done: REQUIREMENTS_UNMET, the lamp released", $"{Text(Hazhalm, "REQUIREMENTS_UNMET")}:0/none/1",
              $"{Special()}/{Cur()}/{Qty(smoldering)}");
        // ToAU 2 done (GM !addmission / !completemission)
        Lua("player:addMission(xi.mission.log_id.TOAU, xi.mission.id.toau.IMMORTAL_SENTRIES) player:completeMission(xi.mission.log_id.TOAU, xi.mission.id.toau.IMMORTAL_SENTRIES)");
        NoErrors();

        Console.WriteLine("Einherjar: Rossweisse's Chamber reserved (event 2, the update, the Glowing Lamp)");
        Trade(gate, (Slot(smoldering), 1));
        string rank = Lua("report(xi.besieged.getMercenaryRank(player))");
        Check("the lamp traded: event 2 (rank, 3 minutes KO expel, 20 hours, the wing 1 menu 0xFF0, the lamps)",
              $"034:{gate}:2:0,{rank},3,20,0,{0xFF0},{smoldering},{glowing}/2", $"{Ev()}/{Cur()}");
        EventEnd(2, 1, 1);
        NoteSpecial(1);
        Check("update (option 1, Rossweisse): updateEvent, the chamber made (wave 1 up), the Glowing Lamp, the lockout",
              $"0,10,3,20,0,{0xFF0},{smoldering},{glowing}/true/1/True/1",
              $"{Upd()}/{Lua("report(tostring(xi.einherjar.getChamber(1) ~= nil))")}/{Lua("report(xi.einherjar.getChamber(1).waveIndex)")}/" +
              $"{long.Parse(Var("[ein]lockout")) > EarthNow()}/{Qty(glowing)}");
        var reg = Of(0x0BF).LastOrDefault();
        Check("  instanceEntry(npc, 4): GP_SERV_COMMAND_REGISTRATION with response 4", "4", reg == null ? "none" : BitConverter.ToUInt16(reg.Buffer, 6).ToString());
        string lamp = Lua($"for _, i in ipairs(player:findItems({glowing})) do local d = xi.einherjar.decypherLamp(i) report(d.chamberId) report(d.startTime) report(d.endTime) end");
        Check("  the lamp's exdata: chamber 1, the start, 30 minutes on", $"1,{EarthNow()},{EarthNow() + 1800}", lamp);
        EventEnd(2, 65);
        Check("event 2 finished (65): GLOWING_LAMP_OBTAINED, CLAIM_RELINQUISH (3 minutes), ITEM_OBTAINED, the smoldering lamp taken",
              $"{Text(Hazhalm, "GLOWING_LAMP_OBTAINED")}:{glowing},{Text(Hazhalm, "CLAIM_RELINQUISH")}:{glowing},{Text(Hazhalm, "ITEM_OBTAINED")}:{glowing}/0",
              $"{Special()}/{Qty(smoldering)}");
        NoErrors();

        Console.WriteLine("Einherjar: into the chamber (the Glowing Lamp traded, event 3)");
        Trade(gate, (Slot(glowing), 1));
        Check("the Glowing Lamp traded: event 3 (0x1D + 1, ...), the lamp kept", $"034:{gate}:3:30,{rank},3,20,0,{0xFF0},{smoldering},{glowing}/3/1",
              $"{Ev()}/{Cur()}/{Qty(glowing)}");
        EventEnd(3, 1);
        Check("event 3 finished (1): onChamberEnter ([ein]chamber 1, the chamber music 0x8F, in the chamber's players)",
              "1/true", $"{Var("[ein]chamber")}/{Lua("report(tostring(xi.einherjar.getChamber(1).players[player:getID()] ~= nil))")}");
        var music = Of(0x05F);
        Check("  the four music slots changed to 0x8F (0x05F)", "4", music.Count(p => BitConverter.ToUInt16(p.Buffer, 6) == 0x8F));
        // the event's warp into the chamber, a client-side cutscene: the char by the chamber's center
        MoveTo(401.1f, -216.0f, 40.6f);
        Lua("player:setUnkillable(true)");
        NoErrors();

        Console.WriteLine("Einherjar: the waves (the chamber locked on the first engage, the waves cycled, the boss)");
        {
            int waves = int.Parse(Lua("report(xi.einherjar.getChamber(1).encounters.waveCount)"));
            int planned = int.Parse(Lua("report(xi.einherjar.getChamber(1).plannedMobs)"));
            uint boss = uint.Parse(Lua("report(xi.einherjar.getChamber(1).encounters.boss)"));
            Check("the plan: one wave for a wing 1 chamber, regular mobs planned, a boss", "1/True/True", $"{waves}/{planned > 0}/{boss != 0}");
            List<uint> WaveMobs() => Lua("for _, m in ipairs(xi.einherjar.getChamber(1).mobs) do report(m:getID()) end")
                .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(uint.Parse).ToList();
            var first = WaveMobs();
            Check("  wave 1 and the boss up (the last wave), each spawned with the chamber's mob mods", $"{planned + 1}/True/True/-100",
                  $"{first.Count}/{first.Contains(boss)}/{first.All(id => Mob(id).IsSpawned && Mob(id).IsAlive)}/{Lua($"report(GetMobByID({first[0]}):getMobMod(xi.mobMod.EXP_BONUS))")}");
            // the first claimed, engaging the char (the ENGAGE listener)
            Check("  open before a fight", "false", Lua("report(tostring(xi.einherjar.getChamber(1).locked))"));
            ClaimBySwing(first[0]);
            for (int i = 0; i < 10 && Lua("report(tostring(xi.einherjar.getChamber(1).locked))") != "true"; i++) Tick(1000);
            string special = Lua("report(tostring(xi.einherjar.getChamber(1).encounters.special ~= nil))");
            Check("  a mob engaged: onMobEngage locks the chamber, the special mob's spawn queued (90 to 300 s on) when the plan has one",
                  $"true/{special == "true"}", $"{Lua("report(tostring(xi.einherjar.getChamber(1).locked))")}/{log.Any(l => l.Contains("Special mob will spawn at"))}",
                  $"{Mob(first[0]).Name} owner={Mob(first[0]).OwnerId} engaged={Mob(first[0]).Engaged}");
            for (int round = 0; round < 6 && WaveMobs().Count > 0; round++)
            {
                foreach (var id in WaveMobs())
                {
                    if (!Mob(id).IsAlive) continue;
                    KillByChar(id);
                }
                for (int i = 0; i < 25; i++) Tick(1000);
            }
            Check("all killed and despawned (the DESPAWN listeners): no mobs left, the regulars counted dead, the crate shown, completed",
                  $"0/{planned}/0/true", Lua("local ch = xi.einherjar.getChamber(1) report(#ch.mobs) report(#ch.deadMobs) report(ch.lootCrate:getStatus()) report(tostring(ch.completed))").Replace(",", "/"));
        }
        NoErrors();

        Console.WriteLine("Einherjar: the armoury crate (the ON_TRIGGER listener: the win, the feather, the rewards)");
        {
            uint crate = uint.Parse(Lua("report(xi.einherjar.getChamber(1).lootCrate:getID())"));
            Near(crate);
            int itemsBefore = int.Parse(DbRow(db, "COUNT(*)", "FROM char_inventory WHERE charid = ? AND location = 0"));
            Talk(crate);
            int feather = Ki("ROSSWEISSES_FEATHER");
            Check("the crate talked to: CHAMBER_CLEARED (5 minutes, chamber 0), the Rossweisse's Feather, KEYITEM_OBTAINED",
                  $"True/True/10", $"{Special2Of(s.PacketList).Contains($"{Text(Hazhalm, "CHAMBER_CLEARED")}:5:0")}/" +
                  $"{SpecialOf(s.PacketList).Contains($"{Text(Hazhalm, "KEYITEM_OBTAINED")}:{feather}")}/{DbKeyItem(feather)}");
            // a solo char's pool hands each item straight over (CTreasurePool::addItem's checkTreasureItem, a free slot)
            Check("  the crate's rewards (a crafting material at least) through the char's pool to the inventory, the crate opened", "True/0/1",
                  $"{int.Parse(DbRow(db, "COUNT(*)", "FROM char_inventory WHERE charid = ? AND location = 0")) > itemsBefore}/" +
                  $"{Pool().Split(',', StringSplitOptions.RemoveEmptyEntries).Length}/{Lua($"report(GetNPCByID({crate}):getLocalVar('opened'))")}");
            for (int i = 0; i < 12; i++) Tick(1000);
            Check("  7 s on the disappear, 3 s later gone (npcUtil.disappearCrate)", "2", Lua($"report(GetNPCByID({crate}):getStatus())"));
        }
        NoErrors();

        Console.WriteLine("Einherjar: expelled 5 minutes after the win (the lamp voided, the ichor, the music)");
        {
            s.PacketList.Clear();
            var expel = new List<BasicPacket>();
            for (int i = 0; i < 320 && Var("[ein]chamber") != "-"; i++) { Tick(1000); expel.AddRange(s.PacketList); s.PacketList.Clear(); }
            Check("onChamberExit: [ein]chamber cleared, LAMP_POWER_FADED, event 4, AMPOULES_OBTAINED (960, all killed), the therion ichor",
                  $"-/True/4/True/960", $"{Var("[ein]chamber")}/{SpecialOf(expel).Contains($"{Text(Hazhalm, "LAMP_POWER_FADED")}:{glowing}")}/{Cur()}/" +
                  $"{SpecialOf(expel).Contains($"{Text(Hazhalm, "AMPOULES_OBTAINED")}:960")}/{Lua("report(player:getCurrency('therion_ichor'))")}");
            Check("  the lamp voided (exdata zeroed), the chamber released, the music back to 0", "0/true/4",
                  $"{Lua($"for _, i in ipairs(player:findItems({glowing})) do report(xi.einherjar.decypherLamp(i).chamberId) end")}/" +
                  $"{Lua("report(tostring(xi.einherjar.getChamber(1) == nil))")}/{expel.Count(p => Opcode(p) == 0x05F && BitConverter.ToUInt16(p.Buffer, 6) == 0)}");
            EventEnd(4);
            Lua("player:setUnkillable(false)");
            c.Hp = c.MaxHp;
        }
        NoErrors();

        // a reservation at the gate for the chamber (the lockout lifted first, a GM's !setcharvar), the lamp's event 2
        // finished; the char's new Glowing Lamp slot
        void Reserve(int chamber)
        {
            Lua("player:setCharVar('[ein]lockout', 0)");
            Lua($"player:addItem({smoldering})");
            Near(gate);
            Trade(gate, (Slot(smoldering), 1));
            EventEnd(2, (uint)chamber, 1);
            NoteSpecial(chamber);
            EventEnd(2, (uint)(64 + chamber));
        }
        byte LampSlot(int chamber) => byte.Parse(Lua($"for _, i in ipairs(player:findItems({glowing})) do if xi.einherjar.decypherLamp(i).chamberId == {chamber} then report(i:getSlotID()) break end end"));

        Console.WriteLine("Einherjar: a reservation nobody enters (released 3 minutes on)");
        Reserve(3);
        Check("Siegrune's Chamber reserved: the chamber up, a lamp for it", "true/True",
              $"{Lua("report(tostring(xi.einherjar.getChamber(3) ~= nil))")}/{LampSlot(3) > 0}");
        for (int i = 0; i < 200 && Lua("report(tostring(xi.einherjar.getChamber(3) ~= nil))") == "true"; i++) Tick(1000);
        Check("  3 minutes on, the leader not in: emptyChamberCheck releases it, its mobs despawned", "true/True",
              $"{Lua("report(tostring(xi.einherjar.getChamber(3) == nil))")}/{log.Any(l => l.Contains("[SIEGRUNE] Reservation timeout, clearing chamber."))}");
        Trade(gate, (LampSlot(3), 1));
        Check("  its lamp traded: REQUIREMENTS_UNMET, the lamp voided (voidLamp)", $"{Text(Hazhalm, "REQUIREMENTS_UNMET")}:0/none/0",
              $"{Special()}/{Cur()}/{Lua($"for _, i in ipairs(player:findItems({glowing})) do report(xi.einherjar.decypherLamp(i).chamberId) end").Split(',').Count(x => x == "3")}");
        NoErrors();

        Console.WriteLine("Einherjar: Grimgerde's Chamber, a logout inside and the reconnection");
        Reserve(2);
        Trade(gate, (LampSlot(2), 1));
        EventEnd(3, 1);
        MoveTo(159.6f, -196.0f, -41.4f);
        // the chamber's wave not fought: the char kept alive (a KO there expels it 3 minutes on)
        Lua("player:setUnkillable(true)");
        Check("in Grimgerde's Chamber", "2/true", $"{Var("[ein]chamber")}/{Lua("report(tostring(xi.einherjar.getChamber(2).players[player:getID()] ~= nil))")}");
        Lua($"player:addTempItem({Item("DUSTY_POTION")})");
        string lockout2 = Var("[ein]lockout");
        zone.OnCharTimeout(s, false);
        s = null;
        Check("logged out: zoneObject.onZoneOut's onChamberExit (zoning: the char out of the chamber's players, the temporary items gone, [ein]chamber kept)",
              "false/2/0", $"{Lua("report(tostring(xi.einherjar.getChamber(2).players[" + CharId + "] ~= nil))")}/{Var("[ein]chamber")}/" +
              DbRow(db, "COUNT(*)", "FROM char_inventory WHERE charid = ? AND location = 3"));
        now += 30_000;
        Login();
        Check("logged back in past the gate: onReconnection (a matching lamp), no warp-out event", $"{Hazhalm}/none/true",
              $"{c.Zone}/{Cur()}/{Lua("report(tostring(xi.einherjar.getChamber(2).players[player:getID()] ~= nil))")}");
        Lua("player:setUnkillable(true)");
        s.PacketList.Clear();
        for (int i = 0; i < 7; i++) { Tick(1000); c.Hp = c.MaxHp; }
        Check("  5 s on: onChamberEnter again (reconnecting: the chamber music, the lockout not recorded again)", $"4/{lockout2}",
              $"{Of(0x05F).Count(p => BitConverter.ToUInt16(p.Buffer, 6) == 0x8F)}/{Var("[ein]lockout")}");
        Lua("player:setUnkillable(true)");
        NoErrors();

        Console.WriteLine("Einherjar: the reservation's time runs out (the warnings, everyone expelled)");
        {
            long end = long.Parse(Lua("report(xi.einherjar.getChamber(2).endTime)"));
            var warn = new List<BasicPacket>();
            while (EarthNow() < end - 700) { Tick(60_000); s.PacketList.Clear(); }
            // the ichor for the regulars dead by then (a bomb's Self-Destruct counts)
            int ichor = 0;
            for (int i = 0; i < 800 && Var("[ein]chamber") != "-"; i++)
            {
                string counts = Lua("local ch = xi.einherjar.getChamber(2) if ch then report(#ch.deadMobs) report(ch.plannedMobs) end");
                if (counts != "") ichor = 960 * int.Parse(counts.Split(',')[0]) / int.Parse(counts.Split(',')[1]);
                s.PacketList.Clear();
                Tick(1000);
                warn.AddRange(s.PacketList);
                c.Hp = c.MaxHp;
            }
            int tw = Text(Hazhalm, "TIMEOUT_WARNING"), tws = Text(Hazhalm, "TIMEOUT_WARNING_SECONDS");
            Check("TIMEOUT_WARNING 10 and 5 minutes, TIMEOUT_WARNING_SECONDS 30, then expelled at the end (LAMP_POWER_FADED, event 4, the ichor for the dead)",
                  $"{tw}:10,{tw}:5,{tws}:30,{Text(Hazhalm, "LAMP_POWER_FADED")}:{glowing},{Text(Hazhalm, "AMPOULES_OBTAINED")}:{ichor}/4/-/true",
                  $"{SpecialOf(warn.Where(p => Opcode(p) == 0x02A && (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) is not (6395 or 6398 or 6387)))}/{Cur()}/{Var("[ein]chamber")}/" +
                  $"{Lua("report(tostring(xi.einherjar.getChamber(2) == nil))")}");
            EventEnd(4);
            Lua("player:setUnkillable(false)");
            c.Hp = c.MaxHp;
        }
        NoErrors();

        Console.WriteLine("Einherjar: the lockout (the gate, a new lamp)");
        Near(gate);
        Talk(gate);
        long lockDays = (long)Math.Ceiling((long.Parse(Var("[ein]lockout")) - EarthNow()) / 2088.0);
        Check("the gate talked to while locked out: ENTRY_PROHIBITED (the Vana'diel days left)", $"{Text(Hazhalm, "ENTRY_PROHIBITED")}:{lockDays}/none",
              $"{Special()}/{Cur()}");
        Lua($"player:addItem({smoldering})");
        Trade(gate, (Slot(smoldering), 1));
        Check("a new Smoldering Lamp: RESERVATION_LOCKOUT, no event, the lamp kept", $"{Text(Hazhalm, "RESERVATION_LOCKOUT")}:{lockDays}/none/1",
              $"{Special()}/{Cur()}/{Qty(smoldering)}");
        NoErrors();
        if (noSpecial.Count > 0)
        {
            var calls = noSpecial.Select(ch => (ch.Chamber, Calls: NoSpecialCalls(log, ch.Name, ch.From))).ToList();
            int warned = log.Skip(einherjarFrom).Count(l => l == NoSpecialWarning);
            string each = string.Join(", ", calls.Select(x => $"{x.Chamber}: {x.Calls}"));
            Check($"chambers planned without a special mob ({each} GetMobByID(0) calls): one warning each, none from " +
                  "another script",
                  calls.Sum(x => x.Calls).ToString(), warned.ToString());
            log.RemoveAll(l => l == NoSpecialWarning);
            expectedWarnings.Remove(NoSpecialWarning);
        }
        // ---- Nyzul Isle ----
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = 996053;
        const ushort Alzadaal = 72, NyzulIsle = 77;
        Fixture(db, Alzadaal, 127.0f, -2.0f, 20.0f, 10000);
        Login();
        // math.randomInt's low end while the scripts roll (a GM's loaded dice): the floors the test drives
        void LoadedDice() => Lua("_G.__randomInt = _G.__randomInt or math.randomInt math.randomInt = function(a, b) return a end");
        void FairDice() => Lua("if _G.__randomInt then math.randomInt = _G.__randomInt end");
        int Instance(string what) => int.Parse(Lua($"local i = player:getInstance() report({what})"));
        string IVar(string name) => Lua($"report(player:getInstance():getLocalVar('{name}'))");
        // the 0x027 messageName messages as message:param0
        string NamesOf(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x027)
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF}:{BitConverter.ToInt32(p.Buffer, 16)}"));

        Console.WriteLine("Nyzul Isle: the Runic Seal (_20m) without and with the assault orders");
        Check("in the Alzadaal Undersea Ruins", $"{Alzadaal}", c.Zone);
        uint seal = NpcId("_20m");
        Near(seal);
        Talk(seal);
        Check("no Nyzul Isle Assault Orders: NOTHING_HAPPENS, no event", $"{Text(Alzadaal, "NOTHING_HAPPENS")}:0/none", $"{Special()}/{Cur()}");
        Lua("player:addKeyItem(xi.keyItem.NYZUL_ISLE_ASSAULT_ORDERS)");
        Talk(seal);
        Check("the orders held: event 405 (Nyzul Isle Investigation's 51, -4, 0, 75, 5, 1)", $"034:{seal}:405:51,{unchecked((uint)-4)},0,75,5,1/405/7704",
              $"{Ev()}/{Cur()}/{Lua("report(player:getLocalVar('INSTANCE_ID'))")}");
        EventEnd(405, 0, 1);
        Check("the client's update: createInstance asked for (INSTANCE_REQUESTED, NYZUL_INSTANCE), no instance yet", "2/1/nil",
              $"{Lua("report(player:getLocalVar('INSTANCE_REQUESTED'))")}/{Lua("report(player:getLocalVar('NYZUL_INSTANCE'))")}/{Lua("report(tostring(player:getInstance()))")}");
        s.PacketList.Clear();
        Tick(400);
        Check("the instance loaded: onInstanceCreatedCallback's setInstance, the update 405 (3s), the registration (4)", "7704/405,3,3,3,3,3,3,3/4",
              $"{Instance("i:getID()")}/{Upd()}/{(Of(0x0BF).LastOrDefault() is { } r0 ? BitConverter.ToUInt16(r0.Buffer, 6).ToString() : "none")}");
        // the client's cancel of 405 (the event force-terminated), then 116 finished with 1
        EventEnd(405, 0x40000000);
        Check("405 ended cancelled: event 116 (2)", "116", Cur());
        EventEnd(116, 1);
        Rezone(false);
        Check("116 finished (1): into the Nyzul Isle run at its entry, the fade-in event 51 (onZoneIn)", $"{NyzulIsle}/7704/51",
              $"{c.Zone}/{Instance("i:getID()")}/{Cur()}");
        var entry = Lua("local p = player:getInstance():getEntryPos() report(p.x) report(p.z)").Split(',');
        Check("  at the instance's entry position", $"{entry[0]}/{entry[1]}", $"{c.X}/{c.Z}");
        EventEnd(51);
        var afterReg = new List<BasicPacket>();
        for (int i = 0; i < 3; i++) { s.PacketList.Clear(); Tick(400); afterReg.AddRange(s.PacketList); }
        int timeLimit = Instance("i:getTimeLimit()");
        Check("afterInstanceRegister: COMMENCE (51), TIME_TO_COMPLETE, the fireflies, assaultEntered, the orders gone (KEYITEM_LOST)",
              $"True/True/1/1/00/True", $"{NamesOf(afterReg).Contains($"{Text(NyzulIsle, "COMMENCE")}:")}/" +
              $"{NamesOf(afterReg).Contains($"{Text(NyzulIsle, "TIME_TO_COMPLETE")}:")}/" +
              $"{Lua($"report(player:hasItem({Item("UNDERSEA_RUINS_FIREFLIES")}, xi.inv.TEMPITEMS) and 1 or 0)")}/{Var("assaultEntered")}/" +
              $"{DbKeyItem(Ki("NYZUL_ISLE_ASSAULT_ORDERS")).Replace("01", "00")}/{SpecialOf(afterReg).Contains($"{Text(NyzulIsle, "KEYITEM_LOST")}:{Ki("NYZUL_ISLE_ASSAULT_ORDERS")}")}");
        NoErrors();

        Console.WriteLine("Nyzul Isle: the Rune of Transfer at the start (the Runic Disc, floor 1)");
        Lua("player:setUnkillable(true)");
        uint runeStart = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].npc.RUNE_OF_TRANSFER_ENTRANCE)"));
        uint rune0 = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].npc.RUNE_OF_TRANSFER_OFFSET)"));
        int disc = Ki("RUNIC_DISC");
        Near(runeStart);
        Talk(runeStart);
        Check("a first user: NEW_USER, the Runic Disc (KEYITEM_OBTAINED), no event",
              $"{Text(NyzulIsle, "NEW_USER")}:{disc},{Text(NyzulIsle, "KEYITEM_OBTAINED")}:{disc}/none/10", $"{Special()}/{Cur()}/{DbKeyItem(disc)}");
        Talk(runeStart);
        Check("again: event 94 (the disc, no tokens, 1, no preference, 100, 200, 300, the floors' mask)",
              $"034:{runeStart}:94:{disc},0,1,0,100,200,300,{0xFFFFFFFC}/94/{CharId}", $"{Ev()}/{Cur()}/{IVar("runeHandler")}");
        EventEnd(94, 1);
        Check("94 finished (floor 1, 0 tokens): the starting and current floor 1, the disk holder", $"1/1/{CharId}/1",
              $"{IVar("Nyzul_Isle_StartingFloor")}/{IVar("Nyzul_Current_Floor")}/{IVar("diskHolder")}/{IVar("partySize")}");
        for (int i = 0; i < 5 && Cur() != "95"; i++) Tick(400);
        Check("  1.5 s on: event 95 (the transfer)", "95", Cur());
        LoadedDice();
        s.PacketList.Clear();
        EventEnd(95, 0, 1);
        FairDice();
        var transfer = new List<BasicPacket>(s.PacketList);
        Check("95's update (pickSetPoint, the dice low): a free floor on layout 1, the char there, WELCOME_TO_FLOOR 1, the rune placed",
              $"6/1/380/-500/True/0/380",
              $"{Instance("i:getStage()")}/{IVar("Nyzul_Isle_FloorLayout")}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/" +
              $"{NamesOf(transfer).Contains($"{Text(NyzulIsle, "WELCOME_TO_FLOOR")}:")}/" +
              Lua($"local r = GetNPCByID({rune0}, player:getInstance()) report(r:getStatus()) report(math.floor(r:getXPos()))").Replace(",", "/"));
        EventEnd(95);
        Check("  95 finished: no mobs on a free floor", "0", Lua("local n = 0 for _, m in ipairs(player:getInstance():getMobs()) do if m:isSpawned() then n = n + 1 end end report(n)"));
        var freeFloor = new List<BasicPacket>();
        for (int i = 0; i < 30 && Lua($"report(GetNPCByID({rune0}, player:getInstance()):getAnimationSub())") != "1"; i++) { s.PacketList.Clear(); Tick(1000); freeFloor.AddRange(s.PacketList); }
        Check("the free floor's 9 s timer: setProgress(15), OBJECTIVE_COMPLETE (1), the rune activated, 200 potential tokens",
              $"True/1/200", $"{SpecialOf(freeFloor).Contains($"{Text(NyzulIsle, "OBJECTIVE_COMPLETE")}:1")}/" +
              $"{Lua($"report(GetNPCByID({rune0}, player:getInstance()):getAnimationSub())")}/{IVar("potential_tokens")}");
        NoErrors();

        Console.WriteLine("Nyzul Isle: on to floor 2 (the Rune of Transfer, an enemy leader)");
        Near(rune0);
        Talk(rune0);
        Check("the active rune: optional cutscene 201 (the left / right menu: menuChoice 1)", "201", Cur());
        EventEnd(201, 0, 1);
        Check("  its update: the rune handler", $"{CharId}", IVar("runeHandler"));
        EventEnd(201, 2);
        Check("201 finished (2, next floor): floor 2, the rune cued", "2/1", $"{IVar("Nyzul_Current_Floor")}/{Lua($"report(GetNPCByID({rune0}, player:getInstance()):getLocalVar('cued'))")}");
        for (int i = 0; i < 5 && Cur() != "95"; i++) Tick(400);
        Check("  1.5 s on: the transfer (startCutscene 95)", "95", Cur());
        LoadedDice();
        EventEnd(95, 0, 1);
        EventEnd(95);
        FairDice();
        Check("95 (the dice low): eliminate the enemy leader on floor 2", "1/2", $"{Instance("i:getStage()")}/{IVar("Nyzul_Current_Floor")}");
        uint leader = uint.Parse(Lua("report(zones[xi.zone.NYZUL_ISLE].mob.LEADER_OFFSET)"));
        Check("  prepareMobs: the leader (Mokke) up with the floor's others", "True/True",
              $"{Mob(leader).IsSpawned && Mob(leader).IsAlive}/{Info().Mobs.Count(m => m.IsSpawned && m.IsAlive) > 1}");
        for (int i = 0; i < 12; i++) Tick(1000);
        Check("  the old rune hidden 8 s on", "2", Lua($"report(GetNPCByID({rune0}, player:getInstance()):getStatus())"));
        KillByChar(leader);
        Check("the leader killed: enemyLeaderKill (progress 15), OBJECTIVE_COMPLETE (2), a rune activated, 400 potential tokens", "True/1/400",
              $"{SpecialOf(killPackets).Contains($"{Text(NyzulIsle, "OBJECTIVE_COMPLETE")}:2")}/" +
              $"{Lua($"local i = player:getInstance() local a = 0 for id = {rune0}, {rune0 + 1} do local r = GetNPCByID(id, i) if r:getStatus() == 0 and r:getAnimationSub() == 1 then a = 1 end end report(a)")}/" +
              $"{IVar("potential_tokens")}");
        NoErrors();

        Console.WriteLine("Nyzul Isle: leaving the assault (the floor record, the tokens, out)");
        {
            uint rune = uint.Parse(Lua($"local i = player:getInstance() for id = {rune0}, {rune0 + 1} do local r = GetNPCByID(id, i) if r:getStatus() == 0 then report(id) end end"));
            int penalty = int.Parse(Lua("report(xi.nyzul.getTokenPenalty(player:getInstance()))"));
            Near(rune);
            Talk(rune);
            EventEnd(201, 0, 1);
            s.PacketList.Clear();
            EventEnd(201, 1);
            int tokens = Math.Max(0, 400 - penalty);
            Check("201 finished (1, leave): FLOOR_RECORD (2), NyzulFloorProgress 2, OBTAIN_TOKENS, the tokens, AssaultPromotion 5, AssaultComplete, cutscene 1",
                  $"True/2/True/{tokens}/5/1/1", $"{SpecialOf(s.PacketList).Contains($"{Text(NyzulIsle, "FLOOR_RECORD")}:{disc}")}/{Var("NyzulFloorProgress")}/" +
                  $"{SpecialOf(s.PacketList).Contains($"{Text(NyzulIsle, "OBTAIN_TOKENS")}:{tokens}")}/{Lua("report(player:getCurrency('nyzul_isle_assault_point'))")}/" +
                  $"{Var("AssaultPromotion")}/{Var("AssaultComplete")}/{Cur()}");
            EventEnd(1);
            Rezone();
            Lua("player:setUnkillable(false)");
            c.Hp = c.MaxHp;
            Check("cutscene 1 finished: out to the Alzadaal Undersea Ruins (onZoneIn's position), no instance, the temporary items gone",
                  $"{Alzadaal}/223/20/nil/0", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Lua("report(tostring(player:getInstance()))")}/" +
                  $"{Lua($"report(player:hasItem({Item("UNDERSEA_RUINS_FIREFLIES")}, xi.inv.TEMPITEMS) and 1 or 0)")}");
        }
        NoErrors();

        // ---- Salvage ----
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = 996054;
        const ushort Zhayolm = 73;
        Fixture(db, Alzadaal, -580.0f, 0.0f, -410.0f, 10000);
        Login();
        Console.WriteLine("Salvage: the Gilded Gateway (Zhayolm) (_20v), its entry commented out");
        uint gateway = NpcId("_20v");
        int permit = Ki("REMNANTS_PERMIT"), sword = Item("BRONZE_SWORD");
        // equipItem with no slot takes the item's lowest slot bit (getSlotType: a sword's is the sub slot)
        Lua($"player:addItem({sword}) player:equipItem({sword}, xi.inv.INVENTORY, xi.slot.MAIN)");
        Check("in the Alzadaal Undersea Ruins, a Bronze Sword in the main hand", $"{Alzadaal}/{sword}", $"{c.Zone}/{Lua("report(player:getEquipID(xi.slot.MAIN))")}");
        Near(gateway);
        Talk(gateway);
        Check("the gateway talked to: NOTHING_HAPPENS, no event (onTrigger's xi.instance.onTrigger is commented out)",
              $"{Text(Alzadaal, "NOTHING_HAPPENS")}:0/none", $"{Special()}/{Cur()}");
        // the commented-out line run as a GM would: the trigger's eventPreparation (luautils::OnTrigger: the gateway and
        // its script), then xi.instance.onTrigger(player, npc, ZHAYOLM_REMNANTS)
        string GatewayTrigger()
        {
            s.PacketList.Clear();
            c.EventPreparation.TargetEntity = Npc(gateway);
            c.EventPreparation.ScriptFile = "./scripts/zones/Alzadaal_Undersea_Ruins/npcs/_20v.lua";
            string r = Lua($"report(tostring(xi.instance.onTrigger(player, GetNPCByID({gateway}), xi.zone.ZHAYOLM_REMNANTS)))");
            if (!c.IsInEvent) c.EventPreparation.Reset();
            return r;
        }
        Check("xi.instance.onTrigger without a Remnants Permit: false (registryRequirements), no event", "false/none", $"{GatewayTrigger()}/{Cur()}");
        Lua("player:addKeyItem(xi.keyItem.REMNANTS_PERMIT)");
        Check("with the permit: event 407 (Salvage I's 0, -6, 0, 0, 7), INSTANCE_ID 7300", $"true/034:{gateway}:407:0,{unchecked((uint)-6)},0,0,7/407/7300",
              $"{GatewayTrigger()}/{Ev()}/{Cur()}/{Lua("report(player:getLocalVar('INSTANCE_ID'))")}");
        EventEnd(407, 0, 1);
        Check("the client's update (_20v's onEventUpdate, no imbued items): createInstance asked for, no instance yet", "2/nil",
              $"{Lua("report(player:getLocalVar('INSTANCE_REQUESTED'))")}/{Lua("report(tostring(player:getInstance()))")}");
        s.PacketList.Clear();
        Tick(400);
        Check("the instance loaded: onInstanceCreatedCallback's setInstance (stage 1, progress 1, the first door unsealed), the registration (4)",
              $"7300/1/1/1/4", $"{Instance("i:getID()")}/{Instance("i:getStage()")}/{Instance("i:getProgress()")}/" +
              $"{Lua("report(GetNPCByID(zones[xi.zone.ZHAYOLM_REMNANTS].npc.DOOR_1_0, player:getInstance()):getLocalVar('unSealed'))")}/" +
              $"{(Of(0x0BF).LastOrDefault() is { } r1 ? BitConverter.ToUInt16(r1.Buffer, 6).ToString() : "none")}");
        EventEnd(407, 4);
        Rezone(false);
        // onInstanceZoneIn's startEvent(101) goes out (the 0x032), then luautils::OnZoneIn sets the current event to
        // Zone.lua's onZoneIn result, -1: the server is in no event, so the 101 the client ends is refused (isInEvent)
        Check("407 finished (4): xi.instance.onEventFinish's setPos into the run, onInstanceZoneIn at the entry, event 101 sent, then cleared by onZoneIn's -1",
              $"{Zhayolm}/7300/340/-592/True/none", $"{c.Zone}/{Instance("i:getID()")}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Ev(loginPackets).Split(':').ElementAtOrDefault(2) == "101"}/{Cur()}");
        {
            var regPackets = new List<BasicPacket>();
            for (int i = 0; i < 3; i++) { s.PacketList.Clear(); Tick(400); regPackets.AddRange(s.PacketList); }
            Check("afterInstanceRegister (instanceRegister): the gear off, the five pathos, the fireflies, the permit gone",
                  "0/true/true/true/true/true/1/00", $"{Lua("report(player:getEquipID(xi.slot.MAIN))")}/{Has("ENCUMBRANCE_I")}/{Has("OBLIVISCENCE")}/" +
                  $"{Has("OMERTA")}/{Has("IMPAIRMENT")}/{Has("DEBILITATION")}/" +
                  $"{Lua($"report(player:hasItem({Item("CAGE_OF_Z_REMNANTS_FIREFLIES")}, xi.inv.TEMPITEMS) and 1 or 0)")}/{DbKeyItem(permit).Replace("01", "00")}");
            Check("  Obliviscence: no sub job (GetSJob / GetSLevel), Encumbrance's 65535 every slot locked (setEquipBlock)", "0/0/65535",
                  $"{Lua("report(player:getSubJob())")}/{Lua("report(player:getSubLvl())")}/{c.EquipBlock}");
            Send(0x050, 12, b => { b[4] = Slot(sword); b[5] = 0; b[6] = 0; });
            Check("  the sword equipped by the client (0x050): refused, the slot locked", "0", Lua("report(player:getEquipID(xi.slot.MAIN))"));
        }
        EventEnd(101);
        Check("the client's end of 101: refused, so no TIME_TO_COMPLETE / SALVAGE_START (the instance's onEventFinish 101 never runs)", "", Special());
        Lua("player:setUnkillable(true)");
        NoErrors();

        Console.WriteLine("Salvage: the cells (a Duplicatus Cell for the sub job, an Incus Cell for the weapons)");
        {
            int duplicatus = Item("DUPLICATUS_CELL"), incus = Item("INCUS_CELL");
            Lua($"player:addItem({duplicatus}) player:addItem({incus}, 2)");
            int offset = Text(Zhayolm, "CELL_OFFSET");
            string TalkNums() => string.Join(",", s.PacketList.Where(p => Opcode(p) == 0x036).Select(p => BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF));
            void UseItem(int itemId)
            {
                byte slot = Slot(itemId);
                s.PacketList.Clear();
                Send(0x037, 20, b => { BitConverter.GetBytes(c.Id).CopyTo(b, 4); BitConverter.GetBytes(c.TargId).CopyTo(b, 12); b[14] = slot; });
                var got2 = new List<BasicPacket>(s.PacketList);
                for (int i = 0; i < 8; i++) { s.PacketList.Clear(); Tick(500); got2.AddRange(s.PacketList); }
                s.PacketList.Clear();
                foreach (var p in got2) s.PacketList.AddLast(p);
            }
            UseItem(duplicatus);
            Check("the Duplicatus Cell used: Obliviscence off silently (onItemUse), CELL_OFFSET + 8, the sub job back (MNK 37), the cell gone",
                  $"false/{offset + 8}/2/37/0", $"{Has("OBLIVISCENCE")}/{TalkNums()}/{Lua("report(player:getSubJob())")}/{Lua("report(player:getSubLvl())")}/{Qty(duplicatus)}");
            UseItem(incus);
            Check("an Incus Cell used: Encumbrance again without the main and sub bits (65532), CELL_OFFSET + 0, cellsUsed 1 (the Duplicatus Cell's own onItemUse counts none)",
                  $"65532/65532/{offset}/1/1", $"{Lua("report(player:getStatusEffect(xi.effect.ENCUMBRANCE_I):getPower())")}/{c.EquipBlock}/{TalkNums()}/{IVar("cellsUsed")}/{Qty(incus)}");
            Lua($"player:equipItem({sword}, xi.inv.INVENTORY, xi.slot.MAIN)");
            Check("  the main hand unlocked: the sword equips", $"{sword}", Lua("report(player:getEquipID(xi.slot.MAIN))"));
            UseItem(incus);
            Check("a second Incus Cell: onCellItemCheck's ITEM_UNABLE_TO_USE (the bits already gone), the cell kept", "1/1",
                  $"{Qty(incus)}/{IVar("cellsUsed")}");
        }
        NoErrors();

        Console.WriteLine("Salvage: the first floor (the door, the Puks, the Poroggo Gent, the temporary chest)");
        {
            uint door = uint.Parse(Lua("report(zones[xi.zone.ZHAYOLM_REMNANTS].npc.DOOR_1_0)"));
            Near(door);
            Talk(door);
            Check("the first door (_210) talked to: event 300", $"300", Cur());
            EventEnd(300, 1);
            Check("300 finished (1): onDoorOpen (open, untargetable, sealed again), the four doors on unsealed, allySize 1",
                  $"{Lua("report(xi.animation.OPEN_DOOR)")}/true/0/1,1,1,1/1",
                  $"{Npc(door).Animation}/{Lua($"report(tostring(GetNPCByID({door}, player:getInstance()):getUntargetable()))")}/" +
                  $"{Lua($"report(GetNPCByID({door}, player:getInstance()):getLocalVar('unSealed'))")}/" +
                  $"{Lua("local ID = zones[xi.zone.ZHAYOLM_REMNANTS] for _, d in ipairs({ ID.npc.DOOR_1_1, ID.npc.DOOR_1_2, ID.npc.DOOR_1_3, ID.npc.DOOR_1_4 }) do report(GetNPCByID(d, player:getInstance()):getLocalVar('unSealed')) end")}/" +
                  $"{IVar("allySize")}");
            var puks = Lua("for _, id in ipairs(zones[xi.zone.ZHAYOLM_REMNANTS].mob.PUK) do report(id) end").Split(',').Select(uint.Parse).ToList();
            uint gent = uint.Parse(Lua("report(zones[xi.zone.ZHAYOLM_REMNANTS].mob.POROGGO_GENT[1])"));
            uint madame = uint.Parse(Lua("report(zones[xi.zone.ZHAYOLM_REMNANTS].mob.POROGGO_MADAME[1])"));
            Check("the stage's Puks up (onInstanceCreated's spawnGroup), the Poroggo Gent not", $"{puks.Count}/False",
                  $"{puks.Count(id => Mob(id).IsSpawned && Mob(id).IsAlive)}/{Mob(gent).IsSpawned && Mob(gent).IsAlive}");
            for (int i = 0; i < puks.Count; i++)
            {
                KillByChar(puks[i]);
                if (i == puks.Count - 2)
                    Check("  all but one Puk killed: no Gent yet (groupKilled)", "False", Mob(gent).IsSpawned && Mob(gent).IsAlive);
            }
            Check("the last Puk killed: the Poroggo Gent up, its drop list 3376", "True/3376",
                  $"{Mob(gent).IsSpawned && Mob(gent).IsAlive}/{Lua($"report(GetMobByID({gent}, player:getInstance()):getDropID())")}");
            KillByChar(gent);
            // where it died (it chased the char)
            float gx = Mob(gent).X, gz = Mob(gent).Z;
            uint chest = uint.Parse(Lua("local i = player:getInstance() local ID = zones[xi.zone.ZHAYOLM_REMNANTS] for k = 2, #ID.npc.ARMOURY_CRATE do local n = GetNPCByID(ID.npc.ARMOURY_CRATE[k], i) if n:getStatus() == xi.status.NORMAL then report(n:getID()) break end end"));
            Check("the Gent killed: progress 2, a temporary chest at its position (spawnTempChest, rate 1000), no Madame (cells used)",
                  $"2/True/False", $"{Instance("i:getProgress()")}/{Math.Abs(Npc(chest).X - gx) < 0.5f && Math.Abs(Npc(chest).Z - gz) < 0.5f}/" +
                  $"{Mob(madame).IsSpawned && Mob(madame).IsAlive}");
            int drink = Item("BOTTLE_OF_FIGHTERS_DRINK");
            Near(chest);
            Talk(chest);
            Check("the chest talked to: tempBoxTrigger's event 2 (the Fighter's Drink x10 pre-picked), opened (animation sub 13)",
                  $"034:{chest}:2:{drink + 10 * 65536}/2/13", $"{Ev()}/{Cur()}/{Npc(chest).AnimationSub}");
            EventEnd(2, 1);
            Check("event 2 finished (1): the drink as a temporary item, TEMP_ITEM, 9 left", $"1/{Text(Zhayolm, "TEMP_ITEM")}:{drink}/9",
                  $"{Lua($"report(player:hasItem({drink}, xi.inv.TEMPITEMS) and 1 or 0)")}/{Special()}/{Lua($"report(GetNPCByID({chest}, player:getInstance()):getLocalVar('itemAmount_1'))")}");
            Talk(chest);
            EventEnd(2, 1);
            Check("again: HAVE_TEMP_ITEM, still 9", $"{Text(Zhayolm, "HAVE_TEMP_ITEM")}:0/9",
                  $"{Special()}/{Lua($"report(GetNPCByID({chest}, player:getInstance()):getLocalVar('itemAmount_1'))")}");
        }
        NoErrors();

        Console.WriteLine("Salvage: the time runs out (the warnings, onInstanceFailure, out to Alzadaal)");
        {
            var warn = new List<BasicPacket>();
            // 88 minutes on by the minute, then by the second
            for (int i = 0; i < 88; i++) { s.PacketList.Clear(); Tick(60_000); warn.AddRange(s.PacketList); c.Hp = c.MaxHp; }
            for (int i = 0; i < 900 && Cur() != "1"; i++) { s.PacketList.Clear(); Tick(1000); warn.AddRange(s.PacketList); c.Hp = c.MaxHp; }
            // the despawns (DespawnMob: the despawn state's fade-out) done 3 s on
            for (int i = 0; i < 4; i++) Tick(1000);
            int min = Text(Zhayolm, "TIME_REMAINING_MINUTES"), sec = Text(Zhayolm, "TIME_REMAINING_SECONDS");
            Check("updateInstanceTime: 10 and 5 minutes, then 60, 30 and 10 seconds; the failure (MISSION_FAILED, the mobs despawned, cutscene 1)",
                  $"{min}:10:0,{min}:5:0,{sec}:60:0,{sec}:30:0,{sec}:10:0,{Text(Zhayolm, "MISSION_FAILED")}:10:10/0/1",
                  $"{Special2Of(warn.Where(p => Opcode(p) == 0x02A && (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) is var m && (m == min || m == sec || m == Text(Zhayolm, "MISSION_FAILED"))))}/" +
                  $"{Lua("local n = 0 for _, m in pairs(player:getInstance():getMobs()) do if m:isSpawned() then n = n + 1 end end report(n)")}/{Cur()}");
            EventEnd(1);
            Rezone();
            Lua("player:setUnkillable(false)");
            c.Hp = c.MaxHp;
            Check("cutscene 1 finished (the instance's onEventFinish, the cutscene having no script): Alzadaal by the gateway, no instance",
                  $"{Alzadaal}/-580/-433/nil", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Lua("report(tostring(player:getInstance()))")}");
            Check("  CZone::IncreaseZoneCounter: the pathos gone (on_zone_pathos), the temporary items cleared (ClearTempItems)",
                  "false/false/false/false/0", $"{Has("ENCUMBRANCE_I")}/{Has("OMERTA")}/{Has("IMPAIRMENT")}/{Has("DEBILITATION")}/" +
                  DbRow(db, "COUNT(*)", "FROM char_inventory WHERE charid = ? AND location = 3"));
            Check("  the sub job's level back, every slot free (the effects' onEffectLose: setEquipBlock 0)", "37/0", $"{Lua("report(player:getSubLvl())")}/{c.EquipBlock}");
        }
        NoErrors();

        // ---- Limbus ----
        // Limbus is gone from the scripts: no xi.limbus, no Apollyon / Temenos battlefields, no NPC scripts there and one
        // mob script (Temenos_Meeble); Al'Taieu's Swirling_Vortex has its xi.limbus.enter commented out. The zones and
        // their mobs are still loaded; what runs is each zone's onZoneIn, placing a char that arrives at 0,0,0 (a GM's
        // !zone) at the entrance.
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = 996055;
        const ushort AlTaieu = 33, Temenos = 37, Apollyon = 38;
        Fixture(db, AlTaieu, 0.0f, 0.0f, -520.0f, 10000);
        Login();
        Console.WriteLine("Limbus: Al'Taieu's Swirling Vortex, Apollyon and Temenos (what the scripts still hold)");
        {
            uint vortex = NpcId("Swirling_Vortex");
            Near(vortex);
            Talk(vortex);
            Check("in Al'Taieu, the Swirling Vortex talked to: nothing (its onTrigger commented out), no event", $"{AlTaieu}//none", $"{c.Zone}/{Special()}/{Cur()}");
        }
        Warp(0.0f, 0.0f, 0.0f, Apollyon);
        Check("a GM's !zone to Apollyon (0,0,0): onZoneIn's setPos to the entrance, no event, its mobs loaded but none up (no battlefield spawns them)",
              $"{Apollyon}/643/-600/none/True/0", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Cur()}/{Info().Mobs.Count > 0}/{Info().Mobs.Count(m => m.IsSpawned)}");
        Warp(0.0f, 0.0f, 0.0f, Temenos);
        Check("to Temenos (0,0,0): onZoneIn's setPos (580, -1.5, 4.452, rotation 192), no event, none of its mobs up",
              $"{Temenos}/580/4/192/none/0", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{c.Rotation}/{Cur()}/{Info().Mobs.Count(m => m.IsSpawned)}");
        NoErrors();
    }
}
