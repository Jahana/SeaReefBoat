using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Content4.Live -- live gate for crafting and gathering driven as a client drives them: synthesis
// (synthutils.cpp, items/transactions/synth.cpp: the 0x096 COMBINE_ASK, the recipe lookup, the claimed and greyed
// stacks, the break / HQ tier rolls per craft, the material loss on a break, the skill-ups, the rank caps and the
// specialization drop, the 0x030 / 0x06F / 0x070 packets, desynthesis, the crit fail of a zone change, the 15 s wait),
// the crafting guilds (scripts/globals/hobbies/crafting: the guild master's sign-up and rank test, the guild point
// contract and the daily item turned in, the points spent), the guild shops (guild_shops.lua: the hours, the 0x0AA-0x0AD
// lists, buys and sells), HELM (hobbies/helm: the tool traded to a point, the item tables, the tool breaking, the full
// inventory), chocobo digging (hobbies/chocobo_digging: the dig on a rented chocobo for a Gysahl Green, the item
// layers, the cooldowns, the spot and the fatigue) and clamming (hobbies/clamming). The real scripts on the real
// MariaDB (xidb), chars 996081-996089. The randomness is forced: the synth rolls through ZoneHost.Random01, the
// scripts' through math.randomInt / math.randomFloat queues.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;
    private static readonly List<string> _log = new List<string>();

    private static uint CharId = 996081;
    private const uint SynthId = 996081, WatcherId = 996082, GuildCharId = 996083, HelmCharId = 996084, DigCharId = 996085, ClamCharId = 996086,
                       SpecialistId = 996087;
    private static readonly uint[] CharIds = { 996081, 996082, 996083, 996084, 996085, 996086, 996087, 996088, 996089 };
    private const ushort NorthernSandoria = 231, SouthernSandoria = 230, EastRonfaure = 101, WestRonfaure = 100, BibikiBay = 4;
    private const ushort WindCrystal = 4098, EarthCrystal = 4099, LightningCrystal = 4100;
    private const ushort ArrowwoodLog = 688, ArrowwoodLumber = 705, WillowLog = 695, WillowLumber = 712, PetrifiedLog = 703, AncientLumber = 720,
                         LauanShield = 12289, LauanLumber = 706, BronzeIngot = 649, RattanLumber = 721, ButterflyCage = 371, MangledMess = 29695,
                         IronOre = 643, BundlingTwine = 1657, DogwoodLumber = 728, WhiteButterfly = 2770;
    private const byte Sandoria = 0, War = 1;
    private const int Woodworking = 49, Smithing = 50, Goldsmithing = 51;

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

    // every char table (char_insert's char_equip, char_exp, char_history, char_inventory, char_jobs, char_pet,
    // char_points, char_profile, char_storage, char_unlocks, and the ones the logins and the scripts write)
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
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

    // the server variables the host writes (guildutils::UpdateGuildPointsPattern's): kept as they were, restored after
    private static readonly string[] ServerVars = { "[GUILD]pattern_update", "[GUILD]pattern" };
    private static readonly Dictionary<string, (int Value, int Expiry)?> _serverVarsBefore = new Dictionary<string, (int, int)?>();

    private static void SaveServerVars(IDatabase db)
    {
        foreach (var name in ServerVars)
        {
            var r = Sql(db, "SELECT value, expiry FROM server_variables WHERE name = ?", BoundValue.Of(name));
            _serverVarsBefore[name] = r != null && r.Next() ? (r.Get<int>("value"), r.Get<int>("expiry")) : null;
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
            if (Environment.GetEnvironmentVariable("CONTENT4_DUMP_LOG") == "1") foreach (var l in _log.Distinct()) Console.WriteLine("  log: " + l);
            RestoreServerVars(db);
            Cleanup(db);
        }
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;
    private static ushort U16(BasicPacket p, int at) => BitConverter.ToUInt16(p.Buffer, at);
    private static uint U32(BasicPacket p, int at) => BitConverter.ToUInt32(p.Buffer, at);

    private static readonly string[] JobColumns = { "", "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng" };

    // a Hume of the nation and the job at level `lvl` at x / y / z in the zone, with 10000 gil and the items from
    // inventory slot 1 on
    private static void Fixture(IDatabase db, uint id, ushort zoneId, float x, float y, float z, byte nation, byte job, byte lvl,
                                params (ushort Item, uint Qty)[] items)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of($"content4live{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Contentfour{id % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
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

    private static void Skill(IDatabase db, uint id, int skillId, int value, int rank) =>
        Sql(db, "REPLACE INTO char_skills(charid, skillid, value, rank) VALUES (?, ?, ?, ?)",
            BoundValue.Of(id), BoundValue.Of((byte)skillId), BoundValue.Of((ushort)value), BoundValue.Of((byte)rank));

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        // the crafter, a WAR 30 of San d'Oria in Northern San d'Oria with the synth materials; a second char beside him
        // to see what the chars around see
        Fixture(db, SynthId, NorthernSandoria, 0.0f, 0.0f, 0.0f, Sandoria, War, 30,
                (WindCrystal, 12), (ArrowwoodLog, 12), (WindCrystal, 12), (ArrowwoodLog, 12), (WillowLog, 12), (PetrifiedLog, 1), (LightningCrystal, 4), (LauanShield, 1),
                (EarthCrystal, 6), (BronzeIngot, 6), (RattanLumber, 12), (IronOre, 2), (BundlingTwine, 2), (DogwoodLumber, 6), (WhiteButterfly, 6));
        Fixture(db, WatcherId, NorthernSandoria, 2.0f, 0.0f, 0.0f, Sandoria, War, 30);

        var log = _log;
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        // Vana'diel day 8000 (a Firesday), 10:00: the guild shops open
        long vanaTicks = 8000 * VanaTime.DAY + 10 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        long earth0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long unixOffset = 0;
        long EarthNow() => earth0 + (now - 1_000_000) / 1000 + unixOffset;
        zone.UnixNow = EarthNow;
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        scripts.VanaTicksNow = () => vanaTicks;
        // the wall clock the scripts' VanadielTime, NextGameTime and Jst* read on the harness's time too, as GetSystemTime is
        // (getVanaMidnight adds the two: on different clocks a real second passing between two calls shifts it)
        scripts.UnixNowMs = () => (earth0 + unixOffset) * 1000 + (now - 1_000_000);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)EarthNow()); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_content4");
            return string.Join(",", got);
        }
        var expectedWarnings = new HashSet<string>();
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                      !expectedWarnings.Contains(l)).Distinct().ToList();

        // the synth rolls (xirand through ZoneHost.Random01) in the order synthutils draws them; a roll past the queue
        // is counted, so each synth checks it drew exactly the rolls the C++ draws
        var rolls = new Queue<double>();
        int extraRolls = 0;
        zone.Random01 = () =>
        {
            if (rolls.Count > 0) return rolls.Dequeue();
            extraRolls++;
            return 0.999;
        };
        void Rolls(params double[] r)
        {
            rolls.Clear();
            extraRolls = 0;
            foreach (var x in r) rolls.Enqueue(x);
        }
        string RollsLeft() => $"{rolls.Count} left/{extraRolls} extra";

        // the scripts' math.randomInt / math.randomFloat, queued the same way (the engine's own when the queue is empty)
        lua.DoString(@"
            verifyInts, verifyFloats = {}, {}
            local engineInt, engineFloat = math.randomInt, math.randomFloat
            math.randomInt = function(lo, hi)
                if #verifyInts > 0 then return table.remove(verifyInts, 1) end
                return engineInt(lo, hi)
            end
            math.randomFloat = function(lo, hi)
                if #verifyFloats > 0 then return table.remove(verifyFloats, 1) end
                return engineFloat(lo, hi)
            end", "=verify_content4_random");
        void Ints(params int[] v) => lua.DoString($"verifyInts = {{ {string.Join(", ", v)} }}", "=verify_content4_ints");
        void Floats(params double[] v) => lua.DoString($"verifyFloats = {{ {string.Join(", ", v.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)))} }}",
                                                       "=verify_content4_floats");
        string QueuesLeft() => Lua("report(#verifyInts .. '/' .. #verifyFloats)");

        MapSession s = null, watcher = null;
        CharData c = null;
        void SendOn(MapSession ss, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ss, opcode, new BasicPacket(buf));
            zone.AfterParse(ss);
        }
        void Send(ushort opcode, int size, Action<byte[]> fill = null) => SendOn(s, opcode, size, fill);
        List<BasicPacket> OfOn(MapSession ss, int opcode) => new List<BasicPacket>(ss.PacketList).FindAll(p => Opcode(p) == opcode);
        List<BasicPacket> Of(int opcode) => OfOn(s, opcode);

        MapSession LoginAs(uint id, bool endEvents = true)
        {
            var ss = new MapSession { CharId = id, AccountId = id };
            zone.OnLogin(ss);
            var cc = (CharData)ss.Char;
            SendOn(ss, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            SendOn(ss, 0x00C, 12);
            for (int i = 0; i < 4 && endEvents && cc.IsInEvent; i++)
            {
                var ev = (ushort)cc.CurrentEvent.EventId;
                SendOn(ss, 0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
            }
            ss.PacketList.Clear();
            return ss;
        }
        void Login(bool endEvents = true)
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = LoginAs(CharId, endEvents);
            c = (CharData)s.Char;
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
        uint NpcId(string name) => zone.ZoneInfoOf(c.Zone).Npcs.First(n => n.Name == name).Id;
        void Near(uint id)
        {
            var n = Npc(id);
            MoveTo(n.X + 1.0f, n.Y, n.Z);
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
        // GP_CLI_COMMAND_ACTION on the char itself (Mount, Dismount, ChocoboDig)
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
        // the 0x02A TALKNUMWORK messages as message:param0[:param1]
        string Special() => string.Join(",", Of(0x02A).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}" +
                                                                  (BitConverter.ToUInt32(p.Buffer, 12) != 0 ? $":{BitConverter.ToUInt32(p.Buffer, 12)}" : "")));
        // the 0x029 battle messages as message:param:value
        string BasicOf(MapSession ss) => string.Join(",", OfOn(ss, 0x029).Select(p => $"{U16(p, 24)}:{U32(p, 12)}:{U32(p, 16)}"));
        string Basic() => BasicOf(s);
        int Text(ushort zoneId, string name) => int.Parse(Lua($"report(zones[{zoneId}].text.{name})"));
        string Var(string name) => Lua($"report(player:getCharVar('{name}'))");
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        ushort ItemId(string name) => ushort.Parse(Lua($"report(xi.item.{name})"));
        uint InvQty(ushort itemId) => (uint)c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == itemId).Sum(i => i.Quantity);
        byte Slot(ushort itemId) => c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == itemId && i.Quantity > 0).OrderBy(i => i.Slot).First().Slot;
        string DbQty(ushort itemId) => DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND location = 0 AND itemId = {itemId}");
        string DbSkill(int skillId) => DbRow(db, "value, rank", $"FROM char_skills WHERE charid = ? AND skillid = {skillId}");
        string FreeSlots() => Lua("report(player:getFreeSlotsCount())");

        // ---- Synthesis ----
        CharId = SynthId;
        Console.WriteLine("Synthesis: a Wind Crystal and an Arrowwood Log (recipe 2, Woodworking 2) at skill 0");
        Login();
        // the watcher steps up beside the crafter (GP_CLI_COMMAND_POS)
        void Watch()
        {
            watcher = LoginAs(WatcherId);
            SendOn(watcher, 0x015, 32, b =>
            {
                BitConverter.GetBytes(c.X + 2.0f).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
            });
            watcher.PacketList.Clear();
        }
        Watch();
        var w = (CharData)watcher.Char;
        Check("in Northern San d'Oria, the watcher 2 yalms off", $"{NorthernSandoria}/{NorthernSandoria}/2", $"{c.Zone}/{w.Zone}/{MathF.Round(w.X - c.X)}");

        List<BasicPacket> startPackets = null, startSeen = null, donePackets = null, doneSeen = null;
        // GP_CLI_COMMAND_COMBINE_ASK: the crystal and up to eight (item, slot) ingredients, as the client sends them
        void CombineAsk(ushort crystal, byte crystalSlot, params (ushort Item, byte Slot)[] ings)
        {
            s.PacketList.Clear();
            watcher.PacketList.Clear();
            Send(0x096, 36, b =>
            {
                BitConverter.GetBytes(crystal).CopyTo(b, 6);
                b[8] = crystalSlot;
                b[9] = (byte)ings.Length;
                for (int i = 0; i < ings.Length; i++)
                {
                    BitConverter.GetBytes(ings[i].Item).CopyTo(b, 10 + 2 * i);
                    b[26 + i] = ings[i].Slot;
                }
            });
            startPackets = new List<BasicPacket>(s.PacketList);
            startSeen = new List<BasicPacket>(watcher.PacketList);
        }
        void SynthStart(ushort crystal, params ushort[] ings) => CombineAsk(crystal, Slot(crystal), ings.Select(i => (i, Slot(i))).ToArray());
        // the synth's 16 s (CSynthState), the result packets
        void SynthWait(int ms = 16000)
        {
            s.PacketList.Clear();
            watcher.PacketList.Clear();
            Tick(ms);
            donePackets = new List<BasicPacket>(s.PacketList);
            doneSeen = new List<BasicPacket>(watcher.PacketList);
        }
        void Synth(ushort crystal, params ushort[] ings)
        {
            SynthStart(crystal, ings);
            SynthWait();
        }
        // 0x030 GP_SERV_COMMAND_EFFECT: effect:result:status
        string Effect(List<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x030 && U32(p, 4) == c.Id).Select(p => $"{U16(p, 10)}:{p.Buffer[12]}:{p.Buffer[13]}"));
        // 0x06F GP_SERV_COMMAND_COMBINE_ANS: result:count:item:breaks:upkinds:crystal:materials
        string Ans(List<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x06F).Select(p =>
            $"{p.Buffer[4]}:{p.Buffer[6]}:{U16(p, 8)}:{string.Join("-", Enumerable.Range(0, 8).Select(i => U16(p, 10 + 2 * i)).Where(x => x != 0))}:" +
            $"{string.Join("-", Enumerable.Range(0, 4).Select(i => p.Buffer[26 + i]).Where(x => x != 0))}:{U16(p, 34)}:" +
            $"{string.Join("-", Enumerable.Range(0, 8).Select(i => U16(p, 36 + 2 * i)).Where(x => x != 0))}"));
        // 0x070 GP_SERV_COMMAND_COMBINE_INF: result:count:item:breaks:uniqueNo:actIndex:name
        string Inf(List<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x070).Select(p =>
            $"{p.Buffer[4]}:{p.Buffer[6]}:{U16(p, 8)}:{string.Join("-", Enumerable.Range(0, 8).Select(i => U16(p, 10 + 2 * i)).Where(x => x != 0))}:" +
            $"{U16(p, 26)}:{U16(p, 28)}:{System.Text.Encoding.ASCII.GetString(p.Buffer, 30, 16).TrimEnd('\0')}"));
        // 0x01F GP_SERV_COMMAND_ITEM_LIST: item@slot=lock
        string Locks(List<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x01F).Select(p => $"{U16(p, 8)}@{p.Buffer[11]}={p.Buffer[12]}"));
        string BasicIn(List<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x029).Select(p => $"{U16(p, 24)}:{U32(p, 12)}:{U32(p, 16)}"));
        int Count(List<BasicPacket> from, int opcode) => from.Count(p => Opcode(p) == opcode);
        string Uniq() => $"{(ushort)c.Id}:{c.TargId}:{c.Name}";

        // the results land in a new stack each (applyAddItem inserts, as the C++ does), so the bag is grown to 80
        Lua("player:changeContainerSize(xi.inv.INVENTORY, 50)");
        string free0 = FreeSlots();
        uint crystals0 = InvQty(WindCrystal), logs0 = InvQty(ArrowwoodLog);
        byte crystalSlot = Slot(WindCrystal), logSlot = Slot(ArrowwoodLog);
        Rolls(0.5, 0.5, 0.5);
        SynthStart(WindCrystal, ArrowwoodLog);
        Check("0x096: the crystal and the log claimed and greyed out (0x01F NoSelect each)", $"{WindCrystal}@{crystalSlot}=15,{ArrowwoodLog}@{logSlot}=15", Locks(startPackets));
        Check("  the synth animation (0x030: the wind effect 0x11, RESULT_SUCCESS, Animation::Synth 44) to the char and the watcher",
              "17:0:44/17:0:44", $"{Effect(startPackets)}/{Effect(startSeen)}");
        Check("  the char's animation Synth, the crystal used at once, the log still held, one slot fewer free",
              $"44/{crystals0 - 1}/{logs0}/{int.Parse(free0) - 1}", $"{c.Animation}/{InvQty(WindCrystal)}/{InvQty(ArrowwoodLog)}/{FreeSlots()}");
        Check("  the crystal gone from char_inventory already", $"{crystals0 - 1}", DbQty(WindCrystal));
        var midSynth = new List<BasicPacket>();
        s.PacketList.Clear();
        Tick(15000);
        Check("15 s on: still synthesising, no result yet", "44/0", $"{c.Animation}/{Count(new List<BasicPacket>(s.PacketList), 0x06F)}");
        SynthWait(1000);
        Check("16 s: COMBINE_ANS Success: 1 Arrowwood Lumber (UpKind Woodworking, the crystal and the log)",
              $"0:1:{ArrowwoodLumber}::49:{WindCrystal}:{ArrowwoodLog}", Ans(donePackets));
        Check("  the watcher: COMBINE_INF Success with the item, the crafter's id, index and name", $"0:1:{ArrowwoodLumber}::{Uniq()}", Inf(doneSeen));
        Check("  the char sees no COMBINE_INF of his own", "", Inf(donePackets));
        Check("  skill-up (modern system, recipe 2 levels over: chance > 1): +0.1 (amount roll 50 < 85), SKILL_GAIN 38",
              "38:49:1", BasicIn(donePackets));
        Check("  the log ungreyed (0x01F Normal), the lumber in, the animation over", $"{ArrowwoodLog}@{logSlot}=0/{logs0 - 1}/1/0",
              $"{Locks(donePackets)}/{InvQty(ArrowwoodLog)}/{InvQty(ArrowwoodLumber)}/{c.Animation}");
        Check("  char_inventory: a crystal and a log less, 1 lumber; char_skills Woodworking 1 rank 0", $"{crystals0 - 1}/{logs0 - 1}/1/1 0",
              $"{DbQty(WindCrystal)}/{DbQty(ArrowwoodLog)}/{DbQty(ArrowwoodLumber)}/{DbSkill(Woodworking)}");
        Check("  the rolls: the break, the skill-up and its amount", "0 left/0 extra", RollsLeft());
        Check("  the free slots back (the lumber took one)", $"{int.Parse(free0) - 1}", FreeSlots());

        Console.WriteLine("Synthesis: a skill-up across a whole level (0.9 + 0.2), CLISTATUS2 and SKILL_LEVEL_UP");
        Lua($"player:setSkillLevel({Woodworking}, 9)");
        Rolls(0.0, 0.0, 0.9);
        Synth(WindCrystal, ArrowwoodLog);
        Check("the amount roll 90 (85..100: +0.2): SKILL_GAIN 2, SKILL_LEVEL_UP 1, CLISTATUS2", "38:49:2,53:49:1/1",
              $"{BasicIn(donePackets)}/{Count(donePackets, 0x062)}");
        Check("  Woodworking 11 (char_skills), WorkingSkills level 1 (0x20)", $"11 0/{0x20}", $"{DbSkill(Woodworking)}/{c.WorkingSkills[Woodworking] & 0x7FE0}");
        Check("  the rolls", "0 left/0 extra", RollsLeft());

        Console.WriteLine("Synthesis: a break (difficulty 1: 90%), the log lost on its 50% roll");
        uint logs = InvQty(ArrowwoodLog), crystals = InvQty(WindCrystal), lumber = InvQty(ArrowwoodLumber);
        Rolls(0.95, 0.1, 0.5);
        Synth(WindCrystal, ArrowwoodLog);
        Check("0x030: RESULT_FAIL", "17:1:44", Effect(startPackets));
        Check("COMBINE_ANS Failed: the Mangled Mess, the log in BreakNo", $"1:0:{MangledMess}:{ArrowwoodLog}:49:{WindCrystal}:{ArrowwoodLog}", Ans(donePackets));
        Check("  the watcher: COMBINE_INF Failed with the broken log", $"1:0:0:{ArrowwoodLog}:{Uniq()}", Inf(doneSeen));
        lumber = InvQty(ArrowwoodLumber);
        Check("  the crystal and the log gone, no lumber; no skill-up (half of 0.546 missed at 0.5)", $"{crystals - 1}/{logs - 1}/{lumber}/",
              $"{InvQty(WindCrystal)}/{InvQty(ArrowwoodLog)}/{InvQty(ArrowwoodLumber)}/{BasicIn(donePackets)}");
        Check("  the rolls: the break, the log's loss, the skill-up", "0 left/0 extra", RollsLeft());

        Console.WriteLine("Synthesis: a break with the log saved (91 > 50), a skill-up on the break");
        logs = InvQty(ArrowwoodLog);
        Rolls(0.95, 0.9, 0.1, 0.0);
        Synth(WindCrystal, ArrowwoodLog);
        Check("COMBINE_ANS Failed, nothing broken", $"1:0:{MangledMess}::49:{WindCrystal}:{ArrowwoodLog}", Ans(donePackets));
        Check("  the log kept and ungreyed, the crystal spent", $"{logs}/{crystals - 2}/{ArrowwoodLog}@{Slot(ArrowwoodLog)}=0",
              $"{InvQty(ArrowwoodLog)}/{InvQty(WindCrystal)}/{Locks(donePackets)}");
        Check("  the skill-up on a break (baseDiff 1 in (0, 5]: half chance 0.273 hit at 0.1): +0.1", "38:49:1/12 0", $"{BasicIn(donePackets)}/{DbSkill(Woodworking)}");
        Check("  the rolls", "0 left/0 extra", RollsLeft());

        Console.WriteLine("Synthesis: the HQ tiers (skill 2.0: tier 1; 30.0: tier 2; 60.0: tier 4) and the craft ring");
        Lua($"player:setSkillLevel({Woodworking}, 20)");
        lumber = InvQty(ArrowwoodLumber);
        Rolls(0.0, 0.01, 0.9);
        Synth(WindCrystal, ArrowwoodLog);
        Check("skill 2.0 (difficulty 0, tier 1: 1.5625%), the HQ roll 1: RESULT_HQ, HQ1 = 2 lumber",
              $"17:2:44/0:2:{ArrowwoodLumber}::49:{WindCrystal}:{ArrowwoodLog}/{lumber + 2}", $"{Effect(startPackets)}/{Ans(donePackets)}/{InvQty(ArrowwoodLumber)}");
        Check("  the watcher: COMBINE_INF Success with the 2", $"0:2:{ArrowwoodLumber}::{Uniq()}", Inf(doneSeen));
        Check("  the rolls: break, HQ (tier 1 has no upgrade), the skill-up missed (0.9 > 0.444)", "0 left/0 extra//20 0", $"{RollsLeft()}/{BasicIn(donePackets)}/{DbSkill(Woodworking)}");

        Lua($"player:setSkillLevel({Woodworking}, 300)");
        lumber = InvQty(ArrowwoodLumber);
        Rolls(0.0, 0.05, 0.2);
        Synth(WindCrystal, ArrowwoodLog);
        Check("skill 30.0 (difficulty -28, tier 2: 6.25%), the HQ roll 5, one upgrade roll 20 <= 25: HQ2 = 3 lumber",
              $"0:3:{ArrowwoodLumber}/{lumber + 3}/0 left/0 extra", $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(ArrowwoodLumber)}/{RollsLeft()}");
        Check("  no skill-up 28 levels over the recipe (modern: baseDiff <= -11)", "", BasicIn(donePackets));

        Lua($"player:setSkillRank({Woodworking}, 6) player:setSkillLevel({Woodworking}, 600)");
        lumber = InvQty(ArrowwoodLumber);
        Rolls(0.0, 0.1, 0.1, 0.1);
        Synth(WindCrystal, ArrowwoodLog);
        Check("skill 60.0 (difficulty -58, tier 4: 50%), the HQ roll 10, both upgrades 10 <= 25: HQ3 = 4 lumber",
              $"0:4:{ArrowwoodLumber}/{lumber + 4}/0 left/0 extra", $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(ArrowwoodLumber)}/{RollsLeft()}");
        lumber = InvQty(ArrowwoodLumber);
        Rolls(0.0, 0.6);
        Synth(WindCrystal, ArrowwoodLog);
        Check("  tier 4, the HQ roll 60 > 50: NQ, 1 lumber", $"0:1:{ArrowwoodLumber}/{lumber + 1}/0 left/0 extra",
              $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(ArrowwoodLumber)}/{RollsLeft()}");
        Lua($"player:addMod(xi.mod.SYNTH_ANTI_HQ_WOODWORKING, 1)");
        lumber = InvQty(ArrowwoodLumber);
        Rolls(0.955);
        Synth(WindCrystal, ArrowwoodLog);
        Check("  a Woodworking craft ring (SYNTH_ANTI_HQ_WOODWORKING): 95% + 1%, the break roll 95.5 passes, no HQ roll: NQ",
              $"17:0:44/0:1:{ArrowwoodLumber}/{lumber + 1}/0 left/0 extra",
              $"{Effect(startPackets)}/{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(ArrowwoodLumber)}/{RollsLeft()}");
        Lua($"player:delMod(xi.mod.SYNTH_ANTI_HQ_WOODWORKING, 1)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Synthesis: refusals (bad recipe, skill too low, the key item recipe, a full inventory, the 15 s wait)");
        crystals = InvQty(WindCrystal);
        Rolls();
        SynthStart(WindCrystal, ArrowwoodLog, WillowLog);
        Check("a crystal and two logs that make nothing: COMBINE_ANS CancelBadRecipe (3), nothing claimed, no animation",
              $"3:0:0:::0:/none/0/{crystals}", $"{Ans(startPackets)}/{(Locks(startPackets) == "" ? "none" : Locks(startPackets))}/{c.Animation}/{InvQty(WindCrystal)}");
        Lua($"player:setSkillRank({Woodworking}, 0) player:setSkillLevel({Woodworking}, 0)");
        SynthStart(WindCrystal, PetrifiedLog);
        Check("Ancient Lumber (Woodworking 72) at skill 0 (below 72 x 10 - 150): CancelSkillTooLow (6), the log kept", $"6:0:0:::0:/{crystals}/1",
              $"{Ans(startPackets)}/{InvQty(WindCrystal)}/{InvQty(PetrifiedLog)}");
        int lumberjack = Ki("LUMBERJACK");
        Check("recipe 3 (three logs and a Bundling Twine) needs the Lumberjack key item", "1986", lumberjack);
        void ThreeLogs() => CombineAsk(WindCrystal, Slot(WindCrystal), (ArrowwoodLog, Slot(ArrowwoodLog)), (ArrowwoodLog, Slot(ArrowwoodLog)),
                                       (ArrowwoodLog, Slot(ArrowwoodLog)), (BundlingTwine, Slot(BundlingTwine)));
        ThreeLogs();
        Check("  three logs from one stack and the twine without it: CancelBadRecipe", "3:0:0:::0:", Ans(startPackets));
        Lua($"player:addKeyItem({lumberjack})");
        logs = InvQty(ArrowwoodLog);
        lumber = InvQty(ArrowwoodLumber);
        Rolls(0.0, 0.9, 0.0);
        ThreeLogs();
        Check("  with it: one lock packet for the one stack the three logs come from", $"{WindCrystal}@{Slot(WindCrystal)}=15,{ArrowwoodLog}@{Slot(ArrowwoodLog)}=15," +
              $"{BundlingTwine}@{Slot(BundlingTwine)}=15", Locks(startPackets));
        SynthWait();
        Check("  done: 3 lumber for the 3 logs of the one stack and the twine", $"0:3:{ArrowwoodLumber}/{logs - 3}/{lumber + 3}/1/0 left/0 extra",
              $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(ArrowwoodLog)}/{InvQty(ArrowwoodLumber)}/{InvQty(BundlingTwine)}/{RollsLeft()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        int free = int.Parse(FreeSlots());
        Lua($"player:changeContainerSize(xi.inv.INVENTORY, -{free})");
        crystals = InvQty(WindCrystal);
        Rolls();
        SynthStart(WindCrystal, ArrowwoodLog);
        Check("a full inventory (no slot for the result, SynthTransaction::start): CancelBadRecipe, nothing claimed or used",
              $"0/3:0:0:::0:/{crystals}/0", $"{FreeSlots()}/{Ans(startPackets)}/{InvQty(WindCrystal)}/{Count(startPackets, 0x01F)}");
        Lua($"player:changeContainerSize(xi.inv.INVENTORY, {free})");

        Lua($"player:addMod(xi.mod.SYNTH_SPEED_WOODWORKING, 2000)");
        Rolls(0.0, 0.9);
        SynthStart(WindCrystal, ArrowwoodLog);
        SynthWait(14000);
        Check("SYNTH_SPEED_WOODWORKING 2000: done at 14 s", $"0:1:{ArrowwoodLumber}", Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b));
        Tick(500);
        crystals = InvQty(WindCrystal);
        SynthStart(WindCrystal, ArrowwoodLog);
        Check("  another at 14.5 s (inside the 15 s since the last): WAIT_LONGER (94), nothing used", $"94:0:0/{crystals}/0",
              $"{BasicIn(startPackets)}/{InvQty(WindCrystal)}/{c.Animation}");
        Tick(600);
        Rolls(0.0, 0.9);
        SynthStart(WindCrystal, ArrowwoodLog);
        Check("  at 15.1 s: under way", $"44/{crystals - 1}", $"{c.Animation}/{InvQty(WindCrystal)}");
        SynthWait();
        Lua($"player:delMod(xi.mod.SYNTH_SPEED_WOODWORKING, 2000)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Synthesis: a two-craft recipe (Woodworking 14, Smithing 3): the break per craft and the failing craft's loss mod");
        // three rows share the Earth / Bronze Ingot / Rattan Lumber x2 key (518-520 cages): the last loaded keeps it
        Lua($"player:setSkillRank({Woodworking}, 1) player:setSkillLevel({Woodworking}, 101)");
        Lua("player:addMod(xi.mod.SYNTH_MATERIAL_LOSS_SMITHING, 30)");
        uint ingots = InvQty(BronzeIngot), rattan = InvQty(RattanLumber), dogwood = InvQty(DogwoodLumber), butterflies = InvQty(WhiteButterfly);
        string cageMaterials = $"{BronzeIngot}-{RattanLumber}-{RattanLumber}-{DogwoodLumber}-{WhiteButterfly}";
        void Cage() => Synth(EarthCrystal, BronzeIngot, RattanLumber, RattanLumber, DogwoodLumber, WhiteButterfly);
        string CageStock() => $"{InvQty(BronzeIngot)}/{InvQty(RattanLumber)}/{InvQty(DogwoodLumber)}/{InvQty(WhiteButterfly)}";
        Rolls(0.0, 0.9, 0.25, 0.25, 0.25, 0.25, 0.25, 0.95, 0.95);
        Cage();
        Check("Woodworking passes (0 <= 70), Smithing breaks (90 > 80): COMBINE_ANS Failed, UpKind Woodworking then Smithing",
              $"1:0:{MangledMess}::49-50:{EarthCrystal}:{cageMaterials}", Ans(donePackets));
        Check("  the Smithing loss mod (50 - 30: 20%): each roll 26 keeps its ingredient", $"{ingots}/{rattan}/{dogwood}/{butterflies}", CageStock());
        Check("  the rolls: two breaks, five losses, two skill-ups missed", "0 left/0 extra/", $"{RollsLeft()}/{BasicIn(donePackets)}");
        Lua("player:delMod(xi.mod.SYNTH_MATERIAL_LOSS_SMITHING, 30)");
        Rolls(0.0, 0.9, 0.25, 0.25, 0.25, 0.25, 0.25, 0.95, 0.95);
        Cage();
        Check("  without it (50%): the five rolls 26 break all five", $"1:0:{MangledMess}:{cageMaterials}/{ingots - 1}/{rattan - 2}/{dogwood - 1}/{butterflies - 1}",
              $"{Ans(donePackets).Split(':').Take(4).Aggregate((a, b) => a + ":" + b)}/{CageStock()}");
        Check("  the watcher's COMBINE_INF packs the five broken", $"1:0:0:{cageMaterials}:{Uniq()}", Inf(doneSeen));
        Rolls(0.0, 0.0, 0.5, 0.0, 0.5, 0.0);
        Cage();
        Check("  both pass: the Butterfly Cage (rare), a skill-up in each craft", $"0:1:{ButterflyCage}/1/38:49:1,38:50:1/0 left/0 extra",
              $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(ButterflyCage)}/{BasicIn(donePackets)}/{RollsLeft()}");
        Check("  char_skills Smithing 1", "1 0", DbSkill(Smithing));
        Rolls();
        SynthStart(EarthCrystal, BronzeIngot, RattanLumber, RattanLumber, DogwoodLumber, WhiteButterfly);
        Check("  again with the cage held: CancelRareItem (7)", "7:0:0:::0:", Ans(startPackets));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Synthesis: desynthesis (a Lauan Shield, Woodworking 7)");
        Lua($"player:setSkillLevel({Woodworking}, 100)");
        uint lauan = InvQty(LauanLumber);
        Rolls(0.3, 0.5, 0.6, 0.01, 0.0);
        Synth(LightningCrystal, LauanShield);
        Check("success 30 <= 40, HQ 50 <= 60, no upgrade (60 >= 50): COMBINE_ANS SuccessDesynth (12), HQ1 = 2 Lauan Lumber",
              $"12:2:{LauanLumber}::49:{LightningCrystal}:{LauanShield}/{lauan + 2}/0", $"{Ans(donePackets)}/{InvQty(LauanLumber)}/{InvQty(LauanShield)}");
        Check("  the 0x030: the lightning effect, RESULT_HQ", "20:2:44", Effect(startPackets));
        Check("  the desynth skill-up (flat 5%, hit at 0.01): +0.1", "38:49:1/101 1", $"{BasicIn(donePackets)}/{DbSkill(Woodworking)}");
        Check("  the rolls", "0 left/0 extra", RollsLeft());

        Console.WriteLine("Synthesis: the rank cap (Amateur 10.0) and the crit fail of a zone change");
        Lua($"player:setSkillRank({Woodworking}, 0) player:setSkillLevel({Woodworking}, 99)");
        Rolls(0.0, 0.0, 0.9);
        Synth(WindCrystal, WillowLog);
        Check("Willow Lumber (Woodworking 13) at 9.9, the amount roll +0.2 capped to +0.1: 10.0, SKILL_LEVEL_UP 10, the blue cap bit",
              $"0:1:{WillowLumber}/38:49:1,53:49:10/100 0/{0x8000}", $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{BasicIn(donePackets)}/" +
              $"{DbSkill(Woodworking)}/{c.WorkingSkills[Woodworking] & 0x8000}");
        Rolls(0.0);
        Synth(WindCrystal, WillowLog);
        Check("  at the cap: no skill-up roll", "/100 0/0 left/0 extra", $"{BasicIn(donePackets)}/{DbSkill(Woodworking)}/{RollsLeft()}");

        crystals = InvQty(WindCrystal);
        logs = InvQty(WillowLog);
        Rolls(0.0);
        SynthStart(WindCrystal, WillowLog);
        Lua($"player:setPos(0, 0, 0, 0, {SouthernSandoria})");
        Tick(400);
        var critPackets = new List<BasicPacket>(s.PacketList);
        Check("setPos to another zone mid-synth (SendToZone's forceSynthCritFail): COMBINE_ANS InterruptedCritical (14), the log broken",
              $"14:0:{MangledMess}:{WillowLog}", Ans(critPackets).Split(':').Take(4).Aggregate((a, b) => a + ":" + b));
        Login();
        Check("  in Southern San d'Oria: the crystal and the log spent, no result", $"{SouthernSandoria}/{crystals - 1}/{logs - 1}/{logs - 1}",
              $"{c.Zone}/{InvQty(WindCrystal)}/{InvQty(WillowLog)}/{DbQty(WillowLog)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Synthesis: the specialization system (a skill-up past 70.0 with 400 points over the common cap)");
        Fixture(db, SpecialistId, NorthernSandoria, 0.0f, 0.0f, 0.0f, Sandoria, War, 30, (WindCrystal, 2), (PetrifiedLog, 2));
        Skill(db, SpecialistId, Woodworking, 700, 7);
        Skill(db, SpecialistId, Smithing, 1000, 10);
        Skill(db, SpecialistId, Goldsmithing, 800, 8);
        CharId = SpecialistId;
        zone.OnCharTimeout(watcher, false);
        Login();
        Watch();
        Rolls(0.0, 0.1);
        Synth(WindCrystal, PetrifiedLog);
        Check("Ancient Lumber (Woodworking 72) at 70.0: the lumber, +0.1 (no amount roll past 60), Smithing (the highest other) drops 0.1 " +
              "and its level message (C++ sends (999 - 1) / 10)", $"0:1:{AncientLumber}/38:49:1,310:50:1,53:50:99/0 left/0 extra",
              $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{BasicIn(donePackets)}/{RollsLeft()}");
        Check("  char_skills: Woodworking 701, Smithing 999, Goldsmithing 800", "701 7/999 10/800 8", $"{DbSkill(Woodworking)}/{DbSkill(Smithing)}/{DbSkill(Goldsmithing)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        zone.OnCharTimeout(watcher, false);
        watcher = null;

        // ---- Guilds ----
        Console.WriteLine("Guilds: the Carpenters' Guild master Cheupirudaux (event 621), the sign-up");
        ushort workbench = ItemId("WORKBENCH");
        ushort vejovisWand = ItemId("VEJOVIS_WAND");
        Fixture(db, GuildCharId, NorthernSandoria, 0.0f, 0.0f, 0.0f, Sandoria, War, 30, (workbench, 1), (ArrowwoodLumber, 12), (WindCrystal, 12),
                (708, 6), (840, 6), (vejovisWand, 1), (vejovisWand, 1));
        // the second Vejovis Wand signed by the char, as a synthesis with an HQ crystal leaves it
        Sql(db, "UPDATE char_inventory SET signature = ? WHERE charid = ? AND location = 0 AND slot = 7", BoundValue.Of($"Contentfour{GuildCharId % 100}"), BoundValue.Of(GuildCharId));
        CharId = GuildCharId;
        Login();
        // GP_SERV_COMMAND_TALKNUM (0x036) messages as message (the speaker flag off)
        string TalkNums() => string.Join(",", Of(0x036).Select(p => U16(p, 10) & 0x7FFF));
        uint master = NpcId("Cheupirudaux");
        Near(master);
        long t0 = EarthNow();
        Talk(master);
        Check("not a member: event 621 (GetSystemTime, skill 0, cap 10, no guilds, no quest, no renounce params)",
              $"{EvStr(master, 621, t0, 0, 10)}/621", $"{Ev()}/{Cur()}");
        EventEnd(621, 1);
        int nsText(string n) => Text(NorthernSandoria, n);
        Check("event 621 finished (1, the sign-up): a Wind Crystal, ITEM_OBTAINED, Guild_Member bit 1 (Woodworking)",
              $"13/{nsText("ITEM_OBTAINED")}:{WindCrystal}/2", $"{InvQty(WindCrystal)}/{Special()}/{Var("Guild_Member")}");
        Talk(master);
        // the renounce params every other talk: no craft at Artisan (7), every craft's bit kept (none can be renounced)
        Check("a member: Guild_Member 2, the renounce params (no Artisans, the full mask 0x1FE)", EvStr(master, 621, t0, 0, 10, 2, 0, 0, 0, 0x1FE), Ev());
        EventEnd(621, 0);
        Talk(master);
        Check("  the next talk skips them (skipRenounceDialog)", EvStr(master, 621, t0, 0, 10, 2), Ev());
        EventEnd(621, 0);
        Check("  skipRenounceDialog back to 0 on the talk after", "0", Lua("report(player:getLocalVar('skipRenounceDialog'))"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guilds: the rank test (Recruit: a Workbench at 8.0 of the Amateur's 10.0)");
        Lua($"player:setSkillLevel({Woodworking}, 70)");
        Trade(master, (Slot(workbench), 1));
        Check("the Workbench traded at 7.0 (below cap - 2): nothing, the Workbench kept", "none/1", $"{Cur()}/{InvQty(workbench)}");
        Lua($"player:setSkillLevel({Woodworking}, 80)");
        Talk(master);
        Check("at 8.0: event 621's first param the test item, the Workbench", $"{EvStr(master, 621, workbench, 8, 10, 2, 0, 0, 0, 0x1FE)}", Ev());
        EventEnd(621, 0);
        Trade(master, (Slot(workbench), 1), (Slot(ArrowwoodLumber), 1));
        Check("  the Workbench and a lumber (two items): nothing", "none/1", $"{Cur()}/{InvQty(workbench)}");
        Trade(master, (Slot(workbench), 1));
        Check("  the Workbench alone: event 622 (new rank 1), the rank raised at once", $"{EvStr(master, 622, 0, 0, 0, 0, 1)}/622/1",
              $"{Ev()}/{Cur()}/{Lua($"report(player:getSkillRank({Woodworking}))")}");
        EventEnd(622, 0);
        Check("event 622 finished: the Workbench taken (CompleteTrade), char_skills rank 1, the new cap 20", "0/80 1/0",
              $"{InvQty(workbench)}/{DbSkill(Woodworking)}/{Lua("report(player:getLocalVar('CompleteTrade'))")}");
        Talk(master);
        Check("  the master's cap param 20 now (the renounce params skipped this time)", EvStr(master, 621, EarthNow(), 8, 20, 2), Ev());
        EventEnd(621, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guilds: guild points (Andreas, events 731 / 732): the contract, the day's item, the points spent");
        Lua($"player:setSkillRank({Woodworking}, 3) player:setSkillLevel({Woodworking}, 300)");
        uint andreas = NpcId("Andreas");
        Near(andreas);
        string gp = Lua("local item, left = player:getCurrentGPItem(xi.guild.WOODWORKING) report(item) report(left)");
        ushort gpItem = ushort.Parse(gp.Split(',')[0]);
        int gpLeft = int.Parse(gp.Split(',')[1]);
        var gpRow = Sql(db, "SELECT points, max_points FROM guild_item_points WHERE guildid = 1 AND itemid = ? AND rank BETWEEN 0 AND 3 LIMIT 1", BoundValue.Of(gpItem));
        int gpPoints = 0, gpMax = 0;
        if (gpRow != null && gpRow.Next()) { gpPoints = gpRow.Get<int>("points"); gpMax = gpRow.Get<int>("max_points"); }
        Check("getCurrentGPItem (rank 3, today's pattern): a guild_item_points item for the Carpenters, its whole max left", $"True/{gpMax}",
              $"{gpItem != 0 && gpPoints > 0}/{gpLeft}");
        Talk(andreas);
        Check("no contract: event 731 (0 points, the guild -1, the item, the points left, the cap 40, the key item bits)",
              $"034:{andreas}:731:0,4294967295,{gpItem},{gpLeft},40", string.Join(",", Ev().Split(',').Take(5)));
        string kiBits = Lua($"local b = 0 for i, k in pairs(xi.crafting.guildKeyItemTable[xi.guild.WOODWORKING]) do if 3 >= k.rank and not player:hasKeyItem(k.id) then b = bit.bor(b, bit.lshift(1, i)) end end report(b)");
        Check("  the key item bits: the four Novice key items", "15", kiBits);
        EventEnd(731, 0xFFFFFFFF);
        Check("event 731 finished (-1): the contract ([GUILD]currentGuild 2), GUILD_NEW_CONTRACT", $"2/{nsText("GUILD_NEW_CONTRACT")}:1",
              $"{Var("[GUILD]currentGuild")}/{Special()}");
        Lua($"player:addItem({gpItem}, 1)");
        Trade(andreas, (Slot(gpItem), 1));
        Check($"the day's item traded: event 732 with its {gpPoints} points, the item taken", $"{EvStr(andreas, 732, gpPoints)}/0", $"{Ev()}/{InvQty(gpItem)}");
        EventEnd(732, (uint)gpPoints);
        Check("event 732 finished: GP_OBTAINED, the points in char_points, [GUILD]daily_points", $"{nsText("GP_OBTAINED")}:{gpPoints}/{gpPoints}/{gpPoints}",
              $"{Special()}/{DbRow(db, "guild_woodworking", "FROM char_points WHERE charid = ?")}/{Var("[GUILD]daily_points")}");
        Check("  getCurrentGPItem: the points left less them", $"{gpItem},{gpMax - gpPoints}", Lua("local item, left = player:getCurrentGPItem(xi.guild.WOODWORKING) report(item) report(left)"));
        Lua($"player:setCharVar('[GUILD]daily_points', {gpMax})");
        Lua($"player:addItem({gpItem}, 1)");
        Trade(andreas, (Slot(gpItem), 1));
        Check("  at the day's cap: NO_MORE_GP_ELIGIBLE, nothing taken", $"{nsText("NO_MORE_GP_ELIGIBLE")}/none/1", $"{TalkNums()}/{Cur()}/{InvQty(gpItem)}");
        // the kept item goes, so the shop checks below never see it (some days the item is the Workbench they sell)
        Lua($"player:delItem({gpItem}, 1)");
        Lua("player:setCharVar('[GUILD]daily_points', 0)");
        Trade(andreas, (Slot(ArrowwoodLumber), 1));
        Check("  another item: nothing", "none", Cur());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Lua("player:addCurrency('guild_woodworking', 60000)");
        int gpHave = gpPoints + 60000;
        Talk(andreas);
        EventEnd(731, 12u | 1u << 5, 1);
        int woodPurification = Ki("WOOD_PURIFICATION");
        Check("update 44 (key items, the first: Wood Purification for 40000): bought, KEYITEM_OBTAINED, its update",
              $"True/{gpHave - 40000}/{nsText("KEYITEM_OBTAINED")}:{woodPurification}/{woodPurification},0,40000,{gpMax},40,0,14,1",
              $"{Lua($"report(tostring(player:hasKeyItem({woodPurification})))") == "true"}/{Lua("report(player:getCurrency('guild_woodworking'))")}/{Special()}/{Upds()}");
        ushort belt = ItemId("CARPENTERS_BELT");
        EventEnd(731, 4u | 1u << 9, 1);
        Check("update 516 (items, the first: one Carpenter's Belt for 10000): bought, ITEM_OBTAINED",
              $"1/{gpHave - 50000}/{nsText("ITEM_OBTAINED")}:{belt}", $"{InvQty(belt)}/{Lua("report(player:getCurrency('guild_woodworking'))")}/{Special()}");
        ushort cyclone = ItemId("CYCLONE_CRYSTAL");
        EventEnd(731, 3u << 5 | 2u << 9, 1);
        Check("update 1120 (HQ crystals, the third: two Cyclone Crystals for 400)", $"2/{gpHave - 50400}/{cyclone},2,200,{gpMax},40,0,14,1",
              $"{InvQty(cyclone)}/{Lua("report(player:getCurrency('guild_woodworking'))")}/{Upds()}");
        EventEnd(731, 12u | 1u << 5, 1);
        Check("  Wood Purification again with too few points (9600 < 40000): NOT_HAVE_ENOUGH_GP", $"{nsText("NOT_HAVE_ENOUGH_GP")}/{gpHave - 50400}",
              $"{TalkNums()}/{Lua("report(player:getCurrency('guild_woodworking'))")}");
        EventEnd(731, 0x40000000);
        Check("  the menu left: the contract kept", "2", Var("[GUILD]currentGuild"));
        Check("char_points: the points left", $"{gpHave - 50400}", DbRow(db, "guild_woodworking", "FROM char_points WHERE charid = ?"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guild shops: Chaupire (the Carpenters' Guild shop, 6:00-21:00) at 10:00");
        uint chaupire = NpcId("Chaupire");
        Near(chaupire);
        Talk(chaupire);
        // GP_SERV_COMMAND_GUILD_OPEN (0x086): Stat, Time
        string GuildOpen() => string.Join(",", Of(0x086).Select(p => $"{p.Buffer[4]}:{U32(p, 8):X8}"));
        Check("talked to: GUILD_OPEN Open with the hours 6..20 as bits, CHAUPIRE_SHOP_DIALOG (showText)", $"0:{((1u << 15) - 1) << 6:X8}/{nsText("CHAUPIRE_SHOP_DIALOG")}:0",
              $"{GuildOpen()}/{Special()}");
        ushort ashLog = ItemId("ASH_LOG");
        string ShopItem(ushort id) => Lua($"local it = xi.guildShops.state['Chaupire'].items[{id}] report(it.stock .. '/' .. it.buyPrice .. '/' .. it.sellPrice)");
        Check("  the day rolled: the Ash Log's stock 144, buy price 86 (480 x (200 - 20) / 1000 past the knee 120)", "144/86", string.Join("/", ShopItem(ashLog).Split('/').Take(2)));
        // GP_CLI_COMMAND_GUILD_BUYLIST / SELLLIST and BUY / SELL: ItemNo@4, PropertyItemIndex@6, ItemNum@7
        void Guild(ushort opcode, ushort itemNo = 0, byte index = 0, byte num = 0)
        {
            s.PacketList.Clear();
            Send(opcode, opcode is 0x0AB or 0x0AD ? 4 : 8, b =>
            {
                if (b.Length < 8) return;
                BitConverter.GetBytes(itemNo).CopyTo(b, 4);
                b[6] = index;
                b[7] = num;
            });
        }
        // GP_SERV_COMMAND_GUILD_BUYLIST / SELLLIST: the entries as item:count:max:price, then Count and Stat of each packet
        string GuildListOf(int opcode, ushort item) => string.Join(",", Of(opcode).SelectMany(p => Enumerable.Range(0, p.Buffer[244])
            .Where(i => U16(p, 4 + i * 8) == item).Select(i => $"{item}:{p.Buffer[6 + i * 8]}:{p.Buffer[7 + i * 8]}:{U32(p, 8 + i * 8)}")));
        string GuildListStats(int opcode) => string.Join(",", Of(opcode).Select(p => $"{p.Buffer[244]}/{p.Buffer[245]:X2}"));
        Guild(0x0AB);
        string shopStock = Lua("local n = 0 for _, cfg in ipairs(xi.data.guildShops['Chaupire'].stock) do if xi.guildShops.state['Chaupire'].items[cfg.id].offered then n = n + 1 end end report(n)");
        int offered = int.Parse(shopStock);
        Check($"GUILD_BUYLIST: the {offered} items offered today, 30 a packet (the first 0x40, the last + 0x80)",
              string.Join(",", Enumerable.Range(0, (offered + 29) / 30).Select(i => $"{Math.Min(30, offered - i * 30)}/{(i == (offered - 1) / 30 ? 0x80 + i : i == 0 ? 0x40 : i):X2}")),
              GuildListStats(0x083));
        Check("  the Ash Log: 144 of 240 at 86", $"{ashLog}:144:240:86", GuildListOf(0x083, ashLog));
        long gil0 = InvQty(65535);
        Guild(0x0AA, ashLog, 0, 2);
        // GP_SERV_COMMAND_GUILD_BUY: ItemNo, Count (the stock left), Trade (the quantity bought)
        string GuildTradeOf(int opcode) => string.Join(",", Of(opcode).Select(p => $"{U16(p, 4)}:{p.Buffer[6]}:{(sbyte)p.Buffer[7]}"));
        Check("GUILD_BUY two Ash Logs: GUILD_BUY (the stock 142 left, 2 bought), 172 gil paid, the logs in", $"{ashLog}:142:2/{gil0 - 172}/2",
              $"{GuildTradeOf(0x082)}/{InvQty(65535)}/{InvQty(ashLog)}");
        Check("  char_inventory: the logs; the shop state's stock 142", $"2/142", $"{DbQty(ashLog)}/{ShopItem(ashLog).Split('/')[0]}");
        Guild(0x0AA, ashLog, 0, 13);
        Check("  13 (past a stack of 12): GUILD_BUY refused (-1), nothing bought", "0:0:-1/2", $"{GuildTradeOf(0x082)}/{InvQty(ashLog)}");
        Guild(0x0AA, ItemId("WILLOW_LOG"), 1, 1);
        Check("  a PropertyItemIndex not 0: the packet refused, no answer", "/0", $"{GuildTradeOf(0x082)}/{InvQty(ItemId("WILLOW_LOG"))}");
        Guild(0x0AA, ItemId("ORCHID_CHIP"), 0, 1);
        Check("  an item the shop does not carry: refused (-1)", "0:0:-1", GuildTradeOf(0x082));

        string lumberShop = ShopItem(ArrowwoodLumber);
        int lumberStock = int.Parse(lumberShop.Split('/')[0]), lumberPrice = int.Parse(lumberShop.Split('/')[2]);
        int lumberBase = int.Parse(Lua($"report(GetReadOnlyItem({ArrowwoodLumber}):getBasePrice())"));
        Check("the Arrowwood Lumber's sell price: base x (600 - 200 x stock / max) / 400", $"{lumberBase * (600 - 200 * lumberStock / 240) / 400}", lumberPrice);
        Guild(0x0AD);
        Check("GUILD_SELLLIST: the lumber at its stock and price", $"{ArrowwoodLumber}:{lumberStock}:240:{lumberPrice}", GuildListOf(0x085, ArrowwoodLumber));
        gil0 = InvQty(65535);
        uint lumberHeld = InvQty(ArrowwoodLumber);
        Guild(0x0AC, ArrowwoodLumber, Slot(ArrowwoodLumber), 3);
        Check("GUILD_SELL three lumber: GUILD_SELL (the stock + 3, 3 sold), paid 3 x the price, the lumber gone",
              $"{ArrowwoodLumber}:{lumberStock + 3}:3/{gil0 + 3 * lumberPrice}/{lumberHeld - 3}", $"{GuildTradeOf(0x084)}/{InvQty(65535)}/{InvQty(ArrowwoodLumber)}");
        Check("  char_inventory: the lumber and the gil", $"{lumberHeld - 3}/{gil0 + 3 * lumberPrice}", $"{DbQty(ArrowwoodLumber)}/{DbQty(65535)}");
        Guild(0x0AC, ArrowwoodLumber, Slot(ArrowwoodLumber), 20);
        Check("  20 (past a stack): GUILD_SELL refused (-4)", "0:0:-4", GuildTradeOf(0x084));
        Guild(0x0AC, ItemId("ASH_LOG"), Slot(ashLog), 1);
        Check("  an Ash Log back: sold (the shop buys what it sells)", $"{ashLog}:143:1/1", $"{GuildTradeOf(0x084)}/{InvQty(ashLog)}");
        Guild(0x0AC, workbench, 0, 1);
        Check("  a Workbench (not carried, not held): GUILD_SELL refused (-4)", "0:0:-4", GuildTradeOf(0x084));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guild shops: the shop closes at 21:00 with the char in it, and stays closed");
        s.PacketList.Clear();
        vanaTicks += 11 * VanaTime.HOUR;
        Tick(1000);
        Check("the hour 21 (xi.guildShops.onGameHour's onShopClose): GUILD_OPEN Close, passive (bit 31), the hours", $"1:{0x80000000 | ((1u << 15) - 1) << 6:X8}", GuildOpen());
        Guild(0x0AA, ashLog, 0, 1);
        Check("  GUILD_BUY with the shop cleared (clearGuildShop): refused by the validator, no answer", "/1", $"{GuildTradeOf(0x082)}/{InvQty(ashLog)}");
        Talk(chaupire);
        Check("talked to at 21:00: GUILD_OPEN Close, no shop dialog", $"1:{((1u << 15) - 1) << 6:X8}/", $"{GuildOpen()}/{Special()}");
        Guild(0x0AA, ashLog, 0, 1);
        Check("  GUILD_BUY while closed (onPlayerBuy: not open): refused (-1)", "0:0:-1/1", $"{GuildTradeOf(0x082)}/{InvQty(ashLog)}");
        Guild(0x0AC, ashLog, Slot(ashLog), 1);
        Check("  GUILD_SELL while closed: refused (-4), the log kept", "0:0:-4/1", $"{GuildTradeOf(0x084)}/{InvQty(ashLog)}");
        vanaTicks += 9 * VanaTime.HOUR;
        Tick(1000);
        Talk(chaupire);
        Check("the next day at 6:00: GUILD_OPEN Open again", "0", GuildOpen().Split(':')[0]);
        Check("  the day rolled: the Ash Log's 143 restocked by its 12 a day (under the target 180)", "155", ShopItem(ashLog).Split('/')[0]);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guilds: Ulycille's advanced image support (event 623), its mods in a synth, the HQ crystal's signature");
        Lua($"player:setSkillRank({Woodworking}, 0) player:setSkillLevel({Woodworking}, 0)");
        uint ulycille = NpcId("Ulycille");
        Near(ulycille);
        Talk(ulycille);
        long gilNow = InvQty(65535);
        Check("Ulycille: event 623 (the price 30 at rank 0, skill 0, the advanced 0, the guilds, the gil, no image yet)",
              EvStr(ulycille, 623, 30, 0, 0, 2, gilNow, 0), Ev());
        EventEnd(623, 1);
        Check("event 623 finished (1): 30 gil, IMAGE_SUPPORT, WOODWORKING_IMAGERY (power 3, subPower 10): WOOD +3, the woodworking loss -10",
              $"{gilNow - 30}/{nsText("IMAGE_SUPPORT")}:0:1/true/3/10",
              $"{InvQty(65535)}/{Special()}/{Lua("report(tostring(player:hasStatusEffect(xi.effect.WOODWORKING_IMAGERY)))")}/" +
              $"{Lua("report(player:getMod(xi.mod.WOOD))")}/{Lua("report(player:getMod(xi.mod.SYNTH_MATERIAL_LOSS_WOODWORKING))")}");
        Talk(ulycille);
        Check("  again: the image's seconds left (480)", EvStr(ulycille, 623, 30, 0, 0, 2, gilNow - 30, 480), Ev());
        EventEnd(623, 0);
        // a Maple Wand (Woodworking 6) at skill 0 + 3: difficulty 3, 80% (50% without the image)
        const ushort MapleLumber = 708, ChocoboFeather = 840, MapleWand = 17049;
        s.PacketList.Clear();
        watcher = LoginAs(WatcherId);
        SendOn(watcher, 0x015, 32, b =>
        {
            BitConverter.GetBytes(c.X + 2.0f).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
        });
        Rolls(0.7, 0.0, 0.0);
        Synth(cyclone, MapleLumber, ChocoboFeather);
        string WandSignatures() => DbRow(db, "GROUP_CONCAT(CONCAT('[', signature, ']') ORDER BY slot)", $"FROM char_inventory WHERE charid = ? AND location = 0 AND itemId = {MapleWand}");
        Check("with a Cyclone Crystal (the HQ wind crystal: the Wind recipe), the break roll 70 <= 80: the Maple Wand, signed (crystal > 0x1080)",
              $"0:1:{MapleWand}/1/[{c.Name}]/{c.Name}/0 left/0 extra",
              $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(MapleWand)}/{WandSignatures()}/" +
              $"{Lua($"report(player:findItem({MapleWand}):getSignature())")}/{RollsLeft()}");
        Check("  0x020 ITEM_ATTR for the result", "True", Count(donePackets, 0x020) >= 1);
        Rolls(0.7, 0.0, 0.0);
        Synth(WindCrystal, MapleLumber, ChocoboFeather);
        Check("with a Wind Crystal: the wand unsigned", $"2/[{c.Name}],[]", $"{InvQty(MapleWand)}/{WandSignatures()}");
        uint mapleLumber = InvQty(MapleLumber), feathers = InvQty(ChocoboFeather);
        Rolls(0.85, 0.45, 0.45);
        Synth(WindCrystal, MapleLumber, ChocoboFeather);
        Check("a break (85 > 80): the image's woodworking loss -10 (40%): the rolls 46 keep both; no skill-up 6 levels under on a break",
              $"1:0:{MangledMess}/{mapleLumber}/{feathers}//0 left/0 extra",
              $"{Ans(donePackets).Split(':').Take(3).Aggregate((a, b) => a + ":" + b)}/{InvQty(MapleLumber)}/{InvQty(ChocoboFeather)}/{BasicIn(donePackets)}/{RollsLeft()}");
        Lua("player:delStatusEffect(xi.effect.WOODWORKING_IMAGERY)");
        Check("  the image gone: the mods back to 0", "0/0", $"{Lua("report(player:getMod(xi.mod.WOOD))")}/{Lua("report(player:getMod(xi.mod.SYNTH_MATERIAL_LOSS_WOODWORKING))")}");
        zone.OnCharTimeout(watcher, false);
        watcher = null;
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guilds: the Expert rank test (the signed Vejovis Wand) and renouncing a craft");
        Lua($"player:setSkillRank({Woodworking}, 9) player:setSkillLevel({Woodworking}, 980)");
        Lua("player:addKeyItem(xi.keyItem.WAY_OF_THE_CARPENTER) player:setCharVar('[Expert]Woodworking', 2)");
        ushort vejovis = ItemId("VEJOVIS_WAND");
        Near(master);
        Talk(master);
        Check("the master at 98.0 of Veteran: the test item the Vejovis Wand, the quest bits (accepted 1 << 9, the key item 1 << 1, the reminder 1 << 10)",
              $"True/{(1 << 9) + (1 << 1) + (1 << 10)}", $"{Ev().StartsWith($"034:{master}:621:{vejovis},98,100,2,")}/{Ev().Split(':')[3].Split(',')[4]}");
        EventEnd(621, 0);
        byte unsignedSlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == vejovis && string.IsNullOrEmpty(ItemSignature.Get(i))).Slot;
        byte signedSlot = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == vejovis && !string.IsNullOrEmpty(ItemSignature.Get(i))).Slot;
        Trade(master, (unsignedSlot, 1));
        Check("an unsigned Vejovis Wand: event 622 (Expert 10, failed 0), the rank and the quest var kept", $"{EvStr(master, 622, 0, 0, 0, 0, 10, 0)}/9/2",
              $"{Ev()}/{Lua($"report(player:getSkillRank({Woodworking}))")}/{Var("[Expert]Woodworking")}");
        EventEnd(622, 0);
        Check("  event 622 finished: the unsigned wand kept", "2", InvQty(vejovis));
        Trade(master, (signedSlot, 1));
        Check("the wand signed by the char (char_inventory.signature, loaded with it): event 622 (10, 1), Expert, the var cleared",
              $"{EvStr(master, 622, 0, 0, 0, 0, 10, 1)}/10/0", $"{Ev()}/{Lua($"report(player:getSkillRank({Woodworking}))")}/{Var("[Expert]Woodworking")}");
        EventEnd(622, 0);
        Check("  event 622 finished: the signed wand taken, char_skills Woodworking rank 10", "1/980 10", $"{InvQty(vejovis)}/{DbSkill(Woodworking)}");
        Lua($"player:setSkillRank({Smithing}, 7) player:setSkillLevel({Smithing}, 700)");
        Talk(master);
        string renounceParams = string.Join(",", Ev().Split(':')[3].Split(',').Skip(6));
        if (renounceParams == "") { EventEnd(621, 0); Talk(master); renounceParams = string.Join(",", Ev().Split(':')[3].Split(',').Skip(6)); }
        Check("Smithing at Artisan 70.0 under Woodworking: two crafts at Artisan or above, Smithing renounceable (its bit out of the mask)",
              $"2,{0x1FE & ~(1 << (Smithing - 48))}", renounceParams);
        EventEnd(621, (uint)Smithing);
        Check("event 621 finished (50, Smithing): renounced to Craftsman 70.0, RENOUNCE_CRAFTSMAN", $"6/700 6/{nsText("RENOUNCE_CRAFTSMAN")}:0:1",
              $"{Lua($"report(player:getSkillRank({Smithing}))")}/{DbSkill(Smithing)}/{Special()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guilds: the Ephemeral Moogle's crystal storage (Northern San d'Oria, events 913 / 915 / 917)");
        uint ephemeral = NpcId("Ephemeral_Moogle_Wood");
        Near(ephemeral);
        Talk(ephemeral);
        Check("talked to, nothing stored: event 913", $"{EvStr(ephemeral, 913, 0, 0, 0, 0, 0, 0, 0, 0)}/913", $"{Ev()}/{Cur()}");
        EventEnd(913, 0);
        ushort windCluster = ItemId("WIND_CLUSTER");
        Lua($"player:addItem({windCluster}, 1)");
        uint windHeld = InvQty(WindCrystal);
        Trade(ephemeral, (Slot(ArrowwoodLumber), 1));
        Check("a lumber traded: event 917 (nothing it takes)", $"{EvStr(ephemeral, 917)}", Ev());
        EventEnd(917, 0);
        Trade(ephemeral, (Slot(WindCrystal), 5), (Slot(windCluster), 1));
        Check("five Wind Crystals and a Wind Cluster traded: event 915 (the wind param: 1 cluster + 5 crystals << 16), the 17 noted",
              $"{EvStr(ephemeral, 915, 0, 0, 1 + (5 << 16))}/17/{windHeld}/1", $"{Ev()}/{Lua("report(player:getLocalVar('[EM]wind_crystals'))")}/{InvQty(WindCrystal)}/{InvQty(windCluster)}");
        EventEnd(915, 0, 1);
        Check("  the update: confirmTrade (the crystals and the cluster taken), wind_crystals 17, the stored params (wind 17)",
              $"{windHeld - 5}/0/17/0,17", $"{InvQty(WindCrystal)}/{InvQty(windCluster)}/{Lua("report(player:getCurrency('wind_crystals'))")}/{Upds()}");
        EventEnd(915, 0);
        Check("  char_inventory and char_points after the event", $"{windHeld - 5}/0/17",
              $"{DbQty(WindCrystal)}/{DbQty(windCluster)}/{DbRow(db, "wind_crystals", "FROM char_points WHERE charid = ?")}");
        Talk(ephemeral);
        Check("talked to again: event 913 with the wind 17", EvStr(ephemeral, 913, 0, 17), Ev());
        EventEnd(913, 3u << 16 | 14);
        Check("event 913 finished (wind, 14): 1 cluster and 2 crystals back, 3 left stored", $"1/{windHeld - 3}/3",
              $"{InvQty(windCluster)}/{InvQty(WindCrystal)}/{Lua("report(player:getCurrency('wind_crystals'))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Guilds: the recipe suggestions (0x058 RECIPE, 0x031)");
        // GP_CLI_COMMAND_RECIPE: skill@4, level@6, Param0@8, Mode@10, Param1@12, Param2@14, Param3@16, Param4@18
        void RecipeAsk(ushort skill, ushort level, ushort mode, ushort p1 = 0, ushort p3 = 0, ushort p4 = 0)
        {
            s.PacketList.Clear();
            Send(0x058, 20, b =>
            {
                BitConverter.GetBytes(skill).CopyTo(b, 4);
                BitConverter.GetBytes(level).CopyTo(b, 6);
                BitConverter.GetBytes(mode).CopyTo(b, 10);
                BitConverter.GetBytes(p1).CopyTo(b, 12);
                BitConverter.GetBytes(p3).CopyTo(b, 16);
                BitConverter.GetBytes(p4).CopyTo(b, 18);
            });
        }
        const string recipeFrom = "FROM (SELECT *, CASE 1 WHEN 1 THEN Wood END AS craftLevel FROM synth_recipes WHERE Desynth = 0) AS recipes " +
                                  "INNER JOIN item_basic ON Result = item_basic.itemid WHERE craftLevel >= GREATEST(Wood, Smith, Gold, Cloth, Leather, Bone, Alchemy, Cook) " +
                                  "AND craftLevel BETWEEN 0 AND 10 ORDER BY craftLevel, item_basic.name";
        var listRows = new List<string>();
        var lr = Sql(db, $"SELECT Result {recipeFrom} LIMIT 0, 17");
        while (lr != null && lr.Next()) listRows.Add(lr.Get<string>("Result"));
        RecipeAsk(1, 10, 2, 0, 0, 0);
        var recipeList = Of(0x031).FirstOrDefault();
        Check("mode 2 (the Woodworking list at rank 0, level 10): RECIPE type 2, the first 16 results by level and name, the 17th as the next page's",
              $"2/{string.Join("-", listRows.Take(16))}/{(listRows.Count > 16 ? listRows[16] : "0")}",
              recipeList == null ? "none" : $"{U16(recipeList, 48)}/{string.Join("-", Enumerable.Range(0, 16).Select(i => U16(recipeList, 16 + i * 2)).Where(x => x != 0))}/{U16(recipeList, 50)}");
        var dr = Sql(db, $"SELECT Result, Crystal, KeyItem, Ingredient1, Ingredient2, Ingredient3, Ingredient4, Ingredient5, Ingredient6, Ingredient7, Ingredient8 {recipeFrom} LIMIT 2, 1");
        string expectDetail = "-";
        if (dr != null && dr.Next())
        {
            var ings = Enumerable.Range(1, 8).Select(i => dr.Get<ushort>($"Ingredient{i}")).Where(x => x != 0).GroupBy(x => x).OrderBy(g => g.Key).ToList();
            expectDetail = $"3/{dr.Get<ushort>("Result")}/{dr.Get<ushort>("Crystal")}/{dr.Get<ushort>("KeyItem")}/{string.Join("-", ings.Select(g => $"{g.Key}x{g.Count()}"))}";
        }
        RecipeAsk(1, 10, 3, 0, 2, 0);
        var detail = Of(0x031).FirstOrDefault();
        Check("mode 3 (the third of that list): RECIPE type 3, the result, the crystal, the key item, the ingredients counted in item order", expectDetail,
              detail == null ? "none" : $"{U16(detail, 48)}/{U16(detail, 4)}/{U16(detail, 12)}/{U16(detail, 14)}/" +
              string.Join("-", Enumerable.Range(0, 8).Where(i => U16(detail, 16 + i * 2) != 0).Select(i => $"{U16(detail, 16 + i * 2)}x{U16(detail, 32 + i * 2)}")));
        RecipeAsk(1, 10, 1);
        var random = Of(0x031).FirstOrDefault();
        Check("mode 1 (a random one of the level's ten): RECIPE type 1 with a Woodworking 10..20 recipe's result", "1/True",
              random == null ? "none" : $"{U16(random, 48)}/{Sql(db, $"SELECT 1 FROM synth_recipes WHERE Result = {U16(random, 4)} AND Desynth = 0 AND Wood BETWEEN 10 AND 20 LIMIT 1")?.Next() == true}");
        RecipeAsk(9, 10, 2);
        Check("skill 9 (past Cooking): refused by the validator", "0", Of(0x031).Count);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- HELM ----
        Console.WriteLine("HELM: logging in East Ronfaure (a Hatchet traded to a Logging Point, event 901)");
        ushort hatchet = ItemId("HATCHET");
        Fixture(db, HelmCharId, EastRonfaure, 200.0f, -50.0f, 140.0f, Sandoria, War, 30, (hatchet, 6), (ArrowwoodLumber, 1));
        CharId = HelmCharId;
        Login();
        Check("in East Ronfaure", $"{EastRonfaure}", c.Zone);
        var points = zone.ZoneInfoOf(EastRonfaure).Npcs.Where(n => n.Name == "Logging_Point").ToList();
        Check("xi.helm.initZone: the zone's Logging Points hidden for their respawn time (60 s) at the start", $"True/{points.Count}",
              $"{points.Count > 0}/{points.Count(n => n.Status != NpcEntity.StatusNormal)}");
        Tick(61000);
        Check("  61 s on: shown again, each moved to one of the zone's points (the queued setPos 3 s after the hide)", $"{points.Count}/{points.Count}",
              $"{points.Count(n => n.Status == NpcEntity.StatusNormal)}/" +
              $"{points.Count(n => Lua($"local pts = xi.helm.dataTable[xi.helmType.LOGGING].zone[xi.zone.EAST_RONFAURE].points local i = GetNPCByID({n.Id}):getLocalVar('[HELM]PositionIndex') " +
                                      $"report(tostring(i > 0 and math.abs(pts[i][1] - {n.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}) < 0.01))") == "true")}");
        int ercText(string n) => Text(EastRonfaure, n);
        var point = points[0];
        Near(point.Id);
        Talk(point.Id);
        Check("the point talked to: LOGGING_IS_POSSIBLE_HERE with the Hatchet", $"{ercText("LOGGING_IS_POSSIBLE_HERE")}:{hatchet}", Special());
        Trade(point.Id, (Slot(ArrowwoodLumber), 1));
        Check("  a lumber traded: the same message, no event", $"{ercText("LOGGING_IS_POSSIBLE_HERE")}:{hatchet}/none", $"{Special()}/{Cur()}");

        string HelmPos(NpcEntity n) => Lua($"report(GetNPCByID({n.Id}):getLocalVar('[HELM]PositionIndex'))");
        int firstIndex = int.Parse(HelmPos(point));
        Lua("player:addStatusEffect(xi.effect.INVISIBLE, { duration = 600, origin = player, tick = 10 })");
        Trade(point.Id, (Slot(hatchet), 1));
        // the script's own delStatusEffect(INVISIBLE) is never reached: the 0x036 handler turns an invisible char away first
        Check("a Hatchet traded while invisible: SYSTEMMES CannotWhileInvisible (172), no HELM", "172/none/6",
              $"{string.Join(",", Of(0x053).Select(p => U16(p, 12)))}/{Cur()}/{InvQty(hatchet)}");
        Lua("player:delStatusEffect(xi.effect.INVISIBLE)");
        // pickItem's obtain roll and weighted pick, doesToolBreak's roll, the WotG helper's roll, the relocation's
        // roll and the new point
        Floats(10, 50);
        Ints(1, 99, 1, 2);
        Trade(point.Id, (Slot(hatchet), 1));
        ushort arrowwood = ArrowwoodLog;
        Check("a Hatchet traded (found 10 < 73.22, pick 1 of 10000: an Arrowwood Log; break roll 50 >= 7.1): event 901 (the log, no break, not full)",
              $"{EvStr(point.Id, 901, arrowwood)}/1/6", $"{Ev()}/{InvQty(arrowwood)}/{InvQty(hatchet)}");
        Check("  the queues drawn (the AMK helper draws none before its mission)", "0/0", QueuesLeft());
        Check("  the point hidden for 60 s and moving (relocateRate 100): status, the camp penalty var the old position",
              $"True/{firstIndex}", $"{point.Status != NpcEntity.StatusNormal}/{Var("[HELM][3]Penalty")}");
        EventEnd(901, 0);
        Tick(3500);
        Check("  3 s on: at point 2 of the table", "2", HelmPos(point));
        Tick(60000);
        Check("  60 s on: shown again", "True", point.Status == NpcEntity.StatusNormal);
        Check("  char_inventory: the log", "1", DbQty(arrowwood));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("HELM: the tool breaking, nothing found, a full inventory");
        Near(point.Id);
        ushort maple = ItemId("MAPLE_LOG");
        Floats(10, 5);
        Ints(2741, 1, 3);
        Trade(point.Id, (Slot(hatchet), 1));
        Check("pick 2741 (the Maple Log), the break roll 5 < 7.1: event 901 (the log, broke), the Hatchet used up (tradeComplete)",
              $"{EvStr(point.Id, 901, maple, 1)}/1/5/5/0/0", $"{Ev()}/{InvQty(maple)}/{InvQty(hatchet)}/{DbQty(hatchet)}/{QueuesLeft()}");
        EventEnd(901, 0);
        Tick(64000);
        Near(point.Id);
        Floats(80, 50);
        Ints(99);
        Trade(point.Id, (Slot(hatchet), 1));
        Check("the obtain roll 80 >= 73.22: event 901 (nothing, no break), the Hatchet kept, the point stays", $"{EvStr(point.Id, 901, 0, 0, 0)}/5/True/0/0",
              $"{Ev()}/{InvQty(hatchet)}/{point.Status == NpcEntity.StatusNormal}/{QueuesLeft()}");
        EventEnd(901, 0);
        int helmFree = int.Parse(FreeSlots());
        Lua($"player:changeContainerSize(xi.inv.INVENTORY, -{helmFree})");
        Floats(10, 50);
        Ints(10000, 99);
        Trade(point.Id, (Slot(hatchet), 1));
        ushort seeds = ItemId("BAG_OF_FRUIT_SEEDS");
        Check("a full inventory: event 901 (the Bag of Fruit Seeds picked, full), nothing given, the point stays", $"{EvStr(point.Id, 901, seeds, 0, 1)}/0/True/0/0",
              $"{Ev()}/{InvQty(seeds)}/{point.Status == NpcEntity.StatusNormal}/{QueuesLeft()}");
        EventEnd(901, 0);
        Lua($"player:changeContainerSize(xi.inv.INVENTORY, {helmFree})");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("HELM: the camp penalty (the break chance x 2.1 back at the last point's position)");
        Floats(10, 50);
        Ints(1, 99, 1, int.Parse(HelmPos(point)));
        int campIndex = int.Parse(HelmPos(point));
        Trade(point.Id, (Slot(hatchet), 1));
        EventEnd(901, 0);
        Tick(3500);
        Check("a log, the point relocated to the same position: the penalty var it", $"{campIndex}/{campIndex}", $"{Var("[HELM][3]Penalty")}/{HelmPos(point)}");
        Tick(60000);
        Near(point.Id);
        uint hatchets = InvQty(hatchet);
        Floats(10, 14);
        Ints(1, 1, 1);
        Trade(point.Id, (Slot(hatchet), 1));
        Check("  at the camped position the break roll 14 < 7.1 x 2.1 (14.91): broke", $"{EvStr(point.Id, 901, arrowwood, 1)}/{hatchets - 1}", $"{Ev()}/{InvQty(hatchet)}");
        EventEnd(901, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Chocobo digging ----
        Console.WriteLine("Chocobo digging: a rented chocobo in West Ronfaure, a Gysahl Green a dig");
        const ushort Greens = 4545;
        Fixture(db, DigCharId, WestRonfaure, -200.0f, -40.0f, 260.0f, Sandoria, War, 30, (Greens, 5));
        CharId = DigCharId;
        Login();
        Check("in West Ronfaure", $"{WestRonfaure}", c.Zone);
        int wrText(string n) => Text(WestRonfaure, n);
        int digSkill = int.Parse(Lua("report(xi.skill.DIG)"));
        // GP_SERV_COMMAND_DIG (0x02F) to the char
        int Digs() => Of(0x02F).Count(p => U32(p, 4) == c.Id);
        string SysMes() => string.Join(",", Of(0x053).Select(p => $"{U16(p, 12)}:{U32(p, 4)}"));
        Act(ActionId.ChocoboDig);
        Check("ChocoboDig on foot: refused by the validator (not mounted), the greens kept", "0/5", $"{Digs()}/{InvQty(Greens)}");
        Lua("player:addKeyItem(xi.keyItem.CHOCOBO_COMPANION)");
        Act(ActionId.Mount, 0);
        Check("mounted on a rented chocobo", "True", c.StatusEffects.HasStatusEffect(EffectId.Mounted));
        Act(ActionId.ChocoboDig);
        int waitLongerRed = int.Parse(Lua("report(xi.msg.basic.WAIT_LONGER_RED)"));
        Check("a dig just after zoning in (ZoneInTime + 60 s at rank 0): WAIT_LONGER_RED with the seconds left, no dig, the green given back",
              $"True/0/5/0", $"{SysMes().StartsWith($"{waitLongerRed}:")}/{Digs()}/{InvQty(Greens)}/{QueuesLeft().Split('/')[0]}");
        unixOffset += 61;
        // keepGreens (a rental keeps none), the treasure layer's one roll, the eleven regular ones (the Little Worm
        // only), the weather crystal, the geode and the ore, the pick of the one, the skill-up, the extra skill-up and
        // the fatigue bypass
        var regular = new List<int> { 1 };
        regular.AddRange(Enumerable.Repeat(1000, 10));
        Ints(new[] { 50, 1000 }.Concat(regular).Concat(new[] { 1000, 1000, 1000, 1, 10, 50, 50 }).ToArray());
        Act(ActionId.ChocoboDig);
        ushort worm = ItemId("LITTLE_WORM");
        Check("a dig 61 s after: the Little Worm, ITEM_OBTAINED, a green spent, the dig shown (0x02F), the inventory synced",
              $"1/{wrText("ITEM_OBTAINED")}:{worm}/4/1/True", $"{InvQty(worm)}/{Special()}/{InvQty(Greens)}/{Digs()}/{Of(0x01D).Count >= 1}");
        Check("  the rolls drawn as the script draws them", "0/0", QueuesLeft());
        Check("  the skill-up (roll 10 <= 15): DIG 0.1 in char_skills; [DIG]DigCount 1 (the fatigue)", $"1 0/1", $"{DbSkill(digSkill)}/{Var("[DIG]DigCount")}");
        Check("  char_inventory: the worm and 4 greens", "1/4", $"{DbQty(worm)}/{DbQty(Greens)}");
        Act(ActionId.ChocoboDig);
        Check("again at once (LastDigTime + 15 s): WAIT_LONGER_RED, the green given back, no dig", "True/4/0",
              $"{SysMes().StartsWith($"{waitLongerRed}:")}/{InvQty(Greens)}/{Digs()}");
        unixOffset += 16;
        Act(ActionId.ChocoboDig);
        Check("16 s on, on the same spot (within 5'): FIND_NOTHING, the green spent all the same, the dig shown",
              $"{wrText("FIND_NOTHING")}/3/1/0/0", $"{TalkNums()}/{InvQty(Greens)}/{Digs()}/{QueuesLeft()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Chocobo digging: nothing found, the weather's crystal, the fatigue, no greens");
        MoveTo(c.X + 10.0f, c.Y, c.Z);
        unixOffset += 16;
        Ints(new[] { 50, 1000 }.Concat(Enumerable.Repeat(1000, 11)).Concat(new[] { 1000, 1000, 1000 }).ToArray());
        Act(ActionId.ChocoboDig);
        Check("10' off, every roll missed: FIND_NOTHING, a green spent, no fatigue", $"{wrText("FIND_NOTHING")}/2/1/1/0/0",
              $"{TalkNums()}/{InvQty(Greens)}/{Digs()}/{Var("[DIG]DigCount")}/{QueuesLeft()}");
        Lua("player:setWeather(xi.weather.HOT_SPELL)");
        MoveTo(c.X + 10.0f, c.Y, c.Z);
        unixOffset += 16;
        Ints(new[] { 50, 1000 }.Concat(Enumerable.Repeat(1000, 11)).Concat(new[] { 1, 1000, 1000, 1, 50, 50, 50 }).ToArray());
        Act(ActionId.ChocoboDig);
        Check("in a hot spell, the crystal roll 1 <= 100: a Fire Crystal, the fatigue 2", $"1/{wrText("ITEM_OBTAINED")}:4096/1/2/0/0",
              $"{InvQty(4096)}/{Special()}/{InvQty(Greens)}/{Var("[DIG]DigCount")}/{QueuesLeft()}");
        Lua("player:setWeather(xi.weather.NONE)");
        Lua("player:setCharVar('[DIG]DigCount', 100)");
        MoveTo(c.X + 10.0f, c.Y, c.Z);
        unixOffset += 16;
        Act(ActionId.ChocoboDig);
        Check("100 digs today (DIG_FATIGUE): FIND_NOTHING, the last green spent, no rolls", $"{wrText("FIND_NOTHING")}/0/1/0/0",
              $"{TalkNums()}/{InvQty(Greens)}/{Digs()}/{QueuesLeft()}");
        unixOffset += 16;
        Act(ActionId.ChocoboDig);
        Check("no greens left: SYSTEMMES YouDontHaveAny with the greens, no dig", $"39:{Greens}/0", $"{SysMes()}/{Digs()}");
        Act(ActionId.Dismount);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Chocobo digging: a registered chocobo (discernment B: keeps the greens 10% of the time; endurance: more digs a day)");
        Lua($"player:addItem({Greens}, 3)");
        Lua("player:registerChocobo({ color = 0, speed = 100, minutes = 20, strength = 0, endurance = 64, discernment = 96, receptivity = 64 })");
        Tick(61000);
        Act(ActionId.Mount, 0);
        Check("mounted on the registered chocobo (getFieldChocobo, the Mounted effect's personal flag)", "True/True",
              $"{c.StatusEffects.HasStatusEffect(EffectId.Mounted)}/{Lua("report(tostring(player:getFieldChocobo() ~= nil))") == "true"}");
        Lua("player:setCharVar('[DIG]DigCount', 100)");
        MoveTo(c.X + 10.0f, c.Y, c.Z);
        unixOffset += 16;
        // keepGreens 5 <= (rank 3 - 1) x 5, the rest as before: the Little Worm
        Ints(new[] { 5, 1000, 1 }.Concat(Enumerable.Repeat(1000, 10)).Concat(new[] { 1000, 1000, 1000, 1, 50, 50, 50 }).ToArray());
        uint worms = InvQty(worm);
        Act(ActionId.ChocoboDig);
        Check("100 digs today, endurance 64 (+ 64 / 2 = 32 digs a day): still digging; the worm, the chocobo refuses the greens " +
              "(BEASTMEN_CACHE_OFFSET + 4), the green given back (its stack of 3 keeps it), the dig shown",
              $"{worms + 1}/True/3/1/101/0/0",
              $"{InvQty(worm)}/{Special().Contains($"{wrText("BEASTMEN_CACHE_OFFSET") + 4}:{Greens}")}/{InvQty(Greens)}/{Digs()}/{Var("[DIG]DigCount")}/{QueuesLeft()}");
        Check("  char_inventory: the 3 greens", "3", DbQty(Greens));
        Lua($"player:delItem({Greens}, 2)");
        int digFree = int.Parse(FreeSlots());
        Lua($"player:changeContainerSize(xi.inv.INVENTORY, -{digFree})");
        MoveTo(c.X + 10.0f, c.Y, c.Z);
        unixOffset += 16;
        Ints(new[] { 5, 1000 }.Concat(Enumerable.Repeat(1000, 11)).Concat(new[] { 1, 1000, 1000, 1, 50, 50, 50 }).ToArray());
        Lua("player:setWeather(xi.weather.HOT_SPELL)");
        uint fireCrystals = InvQty(4096);
        Act(ActionId.ChocoboDig);
        Check("the last green and a full bag: the green's slot taken by the find (a Fire Crystal: no slot to give the green back to), so it is spent",
              $"{fireCrystals + 1}/0/1/0/0", $"{InvQty(4096)}/{InvQty(Greens)}/{Digs()}/{FreeSlots()}/{QueuesLeft().Split('/')[0]}");
        Lua("player:setWeather(xi.weather.NONE)");
        Lua($"player:changeContainerSize(xi.inv.INVENTORY, {digFree})");
        Act(ActionId.Dismount);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Clamming ----
        Console.WriteLine("Clamming: Toh Zonikki's kit (event 28) in Bibiki Bay");
        Fixture(db, ClamCharId, BibikiBay, 485.0f, -2.0f, 735.0f, Sandoria, War, 30);
        CharId = ClamCharId;
        Login();
        Check("in Bibiki Bay", $"{BibikiBay}", c.Zone);
        int bbText(string n) => Text(BibikiBay, n);
        int clammingKit = Ki("CLAMMING_KIT");
        uint zonikki = NpcId("Toh_Zonikki");
        Near(zonikki);
        Talk(zonikki);
        Check("no kit: event 28 (500 gil)", $"{EvStr(zonikki, 28, 500, 0, 0, 0, 0, 0, 0, 0)}/28", $"{Ev()}/{Cur()}");
        EventEnd(28, 0, 1);
        Check("  the update: the kit's key item, enough gil (1), the price", $"{clammingKit},1,0,0,0,500", Upds());
        long gilBefore = InvQty(65535);
        EventEnd(28, 1);
        Check("event 28 finished (1): the kit for 500 gil, KEYITEM_OBTAINED, [Clam]KitSize 50",
              $"True/{gilBefore - 500}/{bbText("KEYITEM_OBTAINED")}:{clammingKit}/50",
              $"{Lua($"report(tostring(player:hasKeyItem({clammingKit})))") == "true"}/{InvQty(65535)}/{Special()}/{Var("[Clam]KitSize")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Clamming: a Clamming Point (event 20), the bucket filled, the delay, the weight");
        uint clamPoint = NpcId("Clamming_Point_1");
        Near(clamPoint);
        Talk(clamPoint);
        Check("Clamming_Point_1 with the kit: event 20", $"{EvStr(clamPoint, 20, 0, 0, 0, 0, 0, 0, 0, 0)}/20", $"{Ev()}/{Cur()}");
        int tideColumn = int.Parse(Lua("local high = set{ xi.moonCycle.NEW_MOON, xi.moonCycle.LESSER_WAXING_CRESCENT, xi.moonCycle.GREATER_WAXING_CRESCENT, " +
                                       "xi.moonCycle.FIRST_QUARTER, xi.moonCycle.LESSER_WAXING_GIBBOUS, xi.moonCycle.GREATER_WAXING_GIBBOUS } " +
                                       "report(high[getVanadielMoonCycle()] and 3 or 2)"));
        // the weighted pick: the roll 1 is the table's first, the sum its last
        ushort jacknife = ItemId("JACKNIFE"), turtle = ItemId("TURTLE_SHELL");
        int rateSum = int.Parse(Lua($"local s = 0 for _, e in ipairs(xi.clamming.lootTable[50]) do s = s + e[{tideColumn}] end report(s)"));
        Ints(1);
        EventEnd(20, 0, 1);
        Check("  the update (the clamming), the roll 1: a Jacknife (11 ponzes) in the bucket, YOU_FIND_ITEM", $"1/11/{bbText("YOU_FIND_ITEM")}:{jacknife}/0/0",
              $"{Var("[Clam]Jacknife")}/{Var("[Clam]KitWeight")}/{Special()}/{QueuesLeft()}");
        EventEnd(20, 0);
        Talk(clamPoint);
        Check("  the point again within its 16 s: IT_LOOKS_LIKE_SOMEONE", $"{bbText("IT_LOOKS_LIKE_SOMEONE")}:0/none", $"{Special()}/{Cur()}");
        unixOffset += 17;
        Talk(clamPoint);
        Ints(rateSum);
        EventEnd(20, 0, 1);
        Check("  17 s on, the roll the sum: the table's last, a Turtle Shell (6 ponzes)", $"1/17/{bbText("YOU_FIND_ITEM")}:{turtle}",
              $"{Var("[Clam]TurtleShell")}/{Var("[Clam]KitWeight")}/{Special()}");
        EventEnd(20, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Clamming: the kit upgraded, the haul handed over (event 29)");
        Lua("player:setCharVar('[Clam]KitWeight', 45)");
        Near(zonikki);
        Talk(zonikki);
        Check("Toh Zonikki with the kit: event 29", $"{EvStr(zonikki, 29, 0, 0, 0, 0, 0, 0, 0, 0)}/29", $"{Ev()}/{Cur()}");
        EventEnd(29, 0, 1);
        Check("  the update: the weight, the size, the size and the next size", "45,50,50,100", Upds());
        EventEnd(29, 3);
        Check("event 29 finished (3, at 45 ponzes): the kit 100, YOUR_CLAMMING_CAPACITY", $"100/{bbText("YOUR_CLAMMING_CAPACITY")}:0",
              $"{Var("[Clam]KitSize")}/{Special()}");
        Talk(zonikki);
        EventEnd(29, 2);
        Check("event 29 finished (2): the kit returned, the Jacknife and the Turtle Shell given (YOU_OBTAIN each), the vars cleared",
              $"False/1/1/0/0/0", $"{Lua($"report(tostring(player:hasKeyItem({clammingKit})))") == "true"}/{InvQty(jacknife)}/{InvQty(turtle)}/" +
              $"{Var("[Clam]Jacknife")}/{Var("[Clam]KitSize")}/{Var("[Clam]KitWeight")}");
        Check("  the messages: YOU_RETURN_THE, then a YOU_OBTAIN for each", $"True/2",
              $"{Special().StartsWith($"{bbText("YOU_RETURN_THE")}:{clammingKit}")}/{Special().Split(',').Count(x => x.StartsWith($"{bbText("YOU_OBTAIN")}:"))}");
        Check("  char_inventory: the two", "1/1", $"{DbQty(jacknife)}/{DbQty(turtle)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Clamming: the bucket broken by the weight (event 30), the kit dropped on leaving the zone");
        Talk(zonikki);
        EventEnd(28, 1);
        Lua("player:setCharVar('[Clam]KitWeight', 45)");
        unixOffset += 17;
        Near(clamPoint);
        Talk(clamPoint);
        Ints(1);
        EventEnd(20, 0, 1);
        Check("45 ponzes and a Jacknife (11) in a 50 bucket: THE_WEIGHT_IS_TOO_MUCH, [Clam]KitBroken 1, the bucket emptied",
              $"{bbText("THE_WEIGHT_IS_TOO_MUCH")}:{jacknife}/1/0", $"{Special()}/{Var("[Clam]KitBroken")}/{Var("[Clam]Jacknife")}");
        EventEnd(20, 0);
        unixOffset += 17;
        Talk(clamPoint);
        EventEnd(20, 0, 1);
        Check("  the point with a broken bucket: YOU_CANNOT_COLLECT, the update (the weight, the size, 0)", $"{bbText("YOU_CANNOT_COLLECT")}:0/56,50",
              $"{Special()}/{Upds()}");
        EventEnd(20, 0);
        Near(zonikki);
        Talk(zonikki);
        Check("Toh Zonikki with a broken bucket: event 30", $"{EvStr(zonikki, 30, 0, 0, 0, 0, 0, 0, 0, 0)}", Ev());
        EventEnd(30, 0);
        Check("event 30 finished: the kit gone, YOU_RETURN_THE, the vars reset", $"False/{bbText("YOU_RETURN_THE")}:{clammingKit}/0/0",
              $"{Lua($"report(tostring(player:hasKeyItem({clammingKit})))") == "true"}/{Special()}/{Var("[Clam]KitBroken")}/{Var("[Clam]KitSize")}");
        Talk(zonikki);
        EventEnd(28, 1);
        Check("  a new kit", "50", Var("[Clam]KitSize"));
        Lua($"player:setPos(0, 0, 0, 0, {SouthernSandoria})");
        Tick(400);
        var zoneOut = new List<BasicPacket>(s.PacketList);
        Login();
        Check("leaving Bibiki Bay with the kit (the zone's onZoneOut): the kit dropped, YOU_DROPPED_THE, the vars reset",
              $"{SouthernSandoria}/False/0/True", $"{c.Zone}/{Lua($"report(tostring(player:hasKeyItem({clammingKit})))") == "true"}/{Var("[Clam]KitSize")}/" +
              $"{zoneOut.Any(p => Opcode(p) == 0x02A && (U16(p, 26) & 0x7FFF) == bbText("YOU_DROPPED_THE") && U32(p, 8) == clammingKit)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Sparks shop ----
        Console.WriteLine("Sparks: Rolandienne's Sparks exchange (Southern San d'Oria, event 995)");
        const uint SparksId = 996088;
        ushort copperVoucher = 0;
        Fixture(db, SparksId, SouthernSandoria, 0.0f, 0.0f, 0.0f, Sandoria, War, 30);
        CharId = SparksId;
        Login();
        copperVoucher = ItemId("COPPER_AMAN_VOUCHER");
        Lua($"player:addItem({copperVoucher}, 3)");
        int ssText(string n) => Text(SouthernSandoria, n);
        uint rolandienne = NpcId("Rolandienne");
        Near(rolandienne);
        Lua("player:addKeyItem(xi.keyItem.MEMORANDOLL) player:addCurrency('spark_of_eminence', 1000)");
        Check("the char not on Records of Eminence record 1 (getEminenceProgress)", "nil", Lua("report(tostring(player:getEminenceProgress(1)))"));
        Talk(rolandienne);
        Check("talked to with the Memorandoll: YOU_WISH_TO_EXCHANGE_SPARKS, event 995 (0, 1000 sparks, no vouchers, 0, no campaign, the week's 100000 left)",
              $"{ssText("YOU_WISH_TO_EXCHANGE_SPARKS")}/{EvStr(rolandienne, 995, 0, 1000, 0, 0, 0, 100000)}/995",
              $"{string.Join(",", Of(0x02A).Select(p => U16(p, 26) & 0x7FFF))}/{Ev()}/{Cur()}");
        EventEnd(995, 1, 1);
        ushort instantWarp = 4181;
        Check("update 1 (items, the first: a Scroll of Instant Warp for 10): ITEM_OBTAINED, 990 sparks, 10 of the week spent, the update",
              $"1/{ssText("ITEM_OBTAINED")}:{instantWarp}/990/10/990,0,0,0,0,99990",
              $"{InvQty(instantWarp)}/{Special()}/{Lua("report(player:getCurrency('spark_of_eminence'))")}/{Var("weekly_sparks_spent")}/{Upds()}");
        EventEnd(995, 2 | 3 << 10, 1);
        Check("update 2 | 3 << 10 (tomes, the first, three): three Mikehe's Memos for 600", "3/390/610",
              $"{InvQty(6147)}/{Lua("report(player:getCurrency('spark_of_eminence'))")}/{Var("weekly_sparks_spent")}");
        EventEnd(995, 1 | 2 << 16, 1);
        Check("update 1 | 2 << 16 (Rem's Tale 1 for 7500, 390 held): NOT_ENOUGH_SPARKS, nothing bought", $"{ssText("NOT_ENOUGH_SPARKS")}:0/390/0",
              $"{Special()}/{Lua("report(player:getCurrency('spark_of_eminence'))")}/{InvQty(4064)}");
        Lua("player:setCharVar('weekly_sparks_spent', 99995)");
        EventEnd(995, 1, 1);
        Check("update 1 with 5 of the week left: MAX_SPARKS_LIMIT_REACHED (100000), nothing bought", $"{ssText("MAX_SPARKS_LIMIT_REACHED")}:100000/1/390",
              $"{Special()}/{InvQty(instantWarp)}/{Lua("report(player:getCurrency('spark_of_eminence'))")}");
        Lua("player:setCharVar('weekly_sparks_spent', 0)");
        EventEnd(995, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Sparks: Copper Aman Vouchers (the trade, event 4601), the currency exchange and the provisions");
        Trade(rolandienne, (Slot(copperVoucher), 3));
        Check("three vouchers traded: confirmed and taken, aman_vouchers 3, event 4601 (the voucher, 3, 230)",
              $"0/3/{EvStr(rolandienne, 4601, copperVoucher, 3, 230)}", $"{InvQty(copperVoucher)}/{Lua("report(player:getCurrency('aman_vouchers'))")}/{Ev()}");
        EventEnd(4601, 0);
        Talk(rolandienne);
        EventEnd(995, 20 | 1 << 10 | 1 << 16, 1);
        Check("update 20 | 1 << 10 | 1 << 16 (a voucher for 1000 conquest points): the nation's sandoria_cp, YOU_NOW_HAVE_AMT_CURRENCY, the update",
              $"1000/2/{ssText("YOU_NOW_HAVE_AMT_CURRENCY")}:1:1000/390,2",
              $"{Lua("report(player:getCurrency('sandoria_cp'))")}/{Lua("report(player:getCurrency('aman_vouchers'))")}/{Special()}/{Upds()}");
        Lua("player:setCurrency('spark_of_eminence', 99500)");
        EventEnd(995, 20 | 1 << 10, 1);
        Check("update 20 | 1 << 10 (a voucher for 1000 sparks, 99500 held): capped at CAP_CURRENCY_SPARKS 99999", "99999/1",
              $"{Lua("report(player:getCurrency('spark_of_eminence'))")}/{Lua("report(player:getCurrency('aman_vouchers'))")}");
        ushort pluton = ItemId("PLUTON");
        EventEnd(995, 30u | 1u << 10 | (uint)pluton << 16, 1);
        Check("update 30 (a voucher for provisions, Pluton): two Pluton, YOU_OBTAIN_ITEM (singular), no vouchers left",
              $"2/{ssText("YOU_OBTAIN_ITEM")}:{pluton}:1/0", $"{InvQty(pluton)}/{Special()}/{Lua("report(player:getCurrency('aman_vouchers'))")}");
        EventEnd(995, 20 | 1 << 10 | 1 << 16, 1);
        Check("  another exchange with none: DO_NOT_POSSESS_ENOUGH (the voucher)", $"{ssText("DO_NOT_POSSESS_ENOUGH")}:{copperVoucher}", Special());
        EventEnd(995, 0);
        Check("char_points: sparks, vouchers, conquest points", "99999/0/1000",
              DbRow(db, "CONCAT(spark_of_eminence, '/', aman_vouchers, '/', sandoria_cp)", "FROM char_points WHERE charid = ?"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

    }
}
