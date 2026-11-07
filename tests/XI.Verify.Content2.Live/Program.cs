using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Content2.Live -- live gate for the content systems driven as a client drives them through the real Lua
// scripts: Magian trials (scripts/globals/magian.lua, magian_data.lua: the Magian Moogles, taking / dropping trials,
// the trial items' exdata, progress from kills and the listeners, the delivery crate, completion and the reward),
// Hunts (scripts/globals/hunts.lua: the Hunt Registry, registering, the kill, the reward and the lockout), Garrison
// (scripts/globals/garrison.lua, garrison_data.lua: the outpost guard's trade, the allies, the waves, the win and the
// loss), Ambuscade (scripts/globals/ambuscade.lua: Gorpa-Masorpa, the Ambuscade Tome and its instance) and Mog Garden
// (scripts/globals/mog_garden.lua and its zone). The 0x00A login, 0x015 moves, 0x01A Talk and Attack, 0x036 trades,
// 0x050 equips, 0x05B event ends and updates, 0x05E the Mog House exit, the zone's ticks, and every step's effect:
// the event and message packets, char vars, key items, items and their exdata, currencies, status effects, the
// spawned entities and the zone changes. The real scripts on the real MariaDB (xidb), chars 996061-996065. The walks
// between the places the scripts do not script are GM !pos warps.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static uint CharId = 996061;
    private const uint MagianId = 996061, SkeletonId = 996062, GarrisonId = 996063, AmbuscadeId = 996064, GardenId = 996065;
    private static readonly uint[] CharIds = { 996061, 996062, 996063, 996064, 996065 };
    private const ushort RuLude = 243, PortWindurst = 240, InnerHorutoto = 192, WestRonfaure = 100, Mhaura = 249, MogGarden = 280,
                         Maquette = 287;
    private const ushort Peeler = 19332, Renegade = 19333, Pugilists = 19327, CatsClaws = 19328;
    private const byte Sandoria = 0, Windurst = 2;
    private const byte War = 1, Mnk = 2, Thf = 6;

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

    // the server variables the scripts write (the garrison's zone lockout): kept as they were before the run, cleared
    // for it, restored after
    private static readonly string[] ServerVars = { "[Garrison]NextEntryTime_100" };
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
            BoundValue.Of(id), BoundValue.Of($"content2live{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Contentlive{id % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
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

        // a THF 75 of Windurst by Ru'Lude Gardens' Magian Moogles with a Peeler
        Fixture(db, MagianId, RuLude, -11.0f, 2.5f, 118.0f, Windurst, Thf, 75, (Peeler, 1));
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 2, 2300, 0)", BoundValue.Of(MagianId));

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
        // the wall clock VanadielTime reads on the harness's time: getVanaMidnight is GetSystemTime() plus the seconds
        // VanadielTime() has to the midnight, so a real second passing between the hunt's finish and the check's own
        // getVanaMidnight would put the two a second apart
        scripts.UnixNowMs = () => earth0 * 1000 + (now - 1_000_000);
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
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)EarthNow()); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_content2");
            return string.Join(",", got);
        }
        var expectedWarnings = new HashSet<string>();
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                      !expectedWarnings.Contains(l)).Distinct().ToList();
        void ClearErrors() { foreach (var e in ScriptErrors()) expectedWarnings.Add(e); }

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
        // the 0x02A TALKNUMWORK messages as message:param0[:param1]
        string Special() => string.Join(",", Of(0x02A).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}" +
                                                                  (BitConverter.ToUInt32(p.Buffer, 12) != 0 ? $":{BitConverter.ToUInt32(p.Buffer, 12)}" : "")));
        // the 0x029 battle messages as message:param:value
        string Basic() => string.Join(",", Of(0x029).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}"));
        int Text(ushort zoneId, string name) => int.Parse(Lua($"report(zones[{zoneId}].text.{name})"));
        string Var(string name) => Lua($"report(player:getCharVar('{name}'))");
        bool HasKi(string name) => Lua($"report(tostring(player:hasKeyItem(xi.keyItem.{name})))") == "true";
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        string Currency(string name) => Lua($"report(player:getCurrency('{name}'))");
        Item InvItem(ushort itemId) => c.Items.FirstOrDefault(i => i.Location == Container.Inventory && i.ItemId == itemId && i.Quantity > 0);
        int InvCount(ushort itemId) => c.Items.Count(i => i.Location == Container.Inventory && i.ItemId == itemId && i.Quantity > 0);
        // the item's exdata through CLuaItem::getExData's table: augmentKind, augmentSubKind, the trial, the first augment
        string Exdata(ushort itemId) => Lua($"local it = player:findItem({itemId}) if not it then report('none') return end " +
                                           "local e = it:getExData() report(string.format('%d/%d/%d/%s/%d:%d', e.augmentKind or 0, e.augmentSubKind or 0, " +
                                           "e.trial and e.trial.id or 0, tostring(e.trial and e.trial.completed), e.augments and e.augments[1].id or 0, " +
                                           "e.augments and e.augments[1].value or 0))");
        // the char turned to the mob where it stands (GP_CLI_COMMAND_POS with the rotation to it)
        void Face(MobEntity m)
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
        }
        // the char's swing claims the mob, then the auto-attacks. The char walks beside it and faces it each second as
        // the client does: an unclaimed mob roams on, and a char left where the mob was is out of reach (no engage, no
        // swing, no claim)
        void ClaimBySwing(MobEntity m)
        {
            // two yalms west of it, facing east (rotation 0)
            void Beside() => Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(m.X - 2.0f).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(m.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = 0;
            });
            Beside();
            void Attack() => Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
            });
            Attack();
            for (int i = 0; m.OwnerId != c.Id && i < 30; i++)
            {
                Beside();
                // the engage refused while the last disengage's wait runs (MsgWaitLonger): the client asks again
                if (c.AttackTarget != m) Attack();
                Tick(1000);
            }
        }
        // the mob brought to 1 HP and killed by the char's swings; the packets of the kill. killDetail: how the claim
        // and the swings went, for a failure's detail
        string killDetail = "";
        string KillState(MobEntity m)
        {
            float dx = m.X - c.X, dy = m.Y - c.Y, dz = m.Z - c.Z;
            return $"owner={m.OwnerId} hp={m.Hp} anim={m.Animation} spawned={m.IsSpawned} fade={m.FadeAtMs} " +
                   $"dist={MathF.Sqrt(dx * dx + dy * dy + dz * dz):0.0} " +
                   $"char anim={c.Animation} target={c.AttackTarget?.Id} effects=[" +
                   Lua("for _, e in ipairs(player:getStatusEffects()) do report(e:getEffectType()) end") + "]";
        }
        List<BasicPacket> Kill(MobEntity m)
        {
            Lua("player:setUnkillable(true)");
            s.PacketList.Clear();
            var packets = new List<BasicPacket>();
            ClaimBySwing(m);
            killDetail = "claimed: " + KillState(m);
            m.Hp = 1;
            for (int i = 0; i < 80 && m.IsAlive; i++)
            {
                // the client turns the char to its target as the mob moves about it (a swing needs the char facing it:
                // CanAttack's MsgCannotSee)
                Face(m);
                Tick(400);
                packets.AddRange(s.PacketList);
                s.PacketList.Clear();
            }
            int swings = packets.Count(p => Opcode(p) == 0x028 && BitConverter.ToUInt32(p.Buffer, 5) == c.Id);
            var msgs = packets.Where(p => Opcode(p) == 0x029).Select(p => BitConverter.ToUInt16(p.Buffer, 24));
            killDetail += $"; the char's swings {swings}, 0x029s [{string.Join(",", msgs)}], after: " + KillState(m);
            // the death's handlers (onMobDeath, the DEFEATED_MOB listeners) on the ticks after
            for (int i = 0; i < 8; i++)
            {
                Tick(400);
                packets.AddRange(s.PacketList);
                s.PacketList.Clear();
            }
            Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
            return packets;
        }
        string BasicOf(IEnumerable<BasicPacket> from, params int[] msgs) => string.Join(",", from.Where(p => Opcode(p) == 0x029 && msgs.Contains(BitConverter.ToUInt16(p.Buffer, 24)))
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}"));
        // GP_CLI_COMMAND_EQUIP_SET: the inventory slot into the equipment slot
        void Equip(byte invSlot, byte equipSlot) => Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });

        // ---- Magian trials ----
        CharId = MagianId;
        Console.WriteLine("Magian: the Orange Magian Moogle without the Magian Trial Log");
        Login();
        Check("in Ru'Lude Gardens, the Peeler in inventory slot 1", $"{RuLude}/{Peeler}", $"{c.Zone}/{c.Items.FirstOrDefault(i => i.Location == Container.Inventory && i.Slot == 1)?.ItemId}");
        Check("trial listeners registered at startup (itemutils::Initialize's xi.magian.registerTrialListeners): xi.items.pugilists.onItemEquip",
              "true", Lua("report(tostring(xi.items and xi.items.pugilists ~= nil and xi.items.pugilists.onItemEquip == xi.magian.onItemEquip))"));
        uint orange = NpcId("Magian_Moogle_Orange");
        Near(orange);
        Talk(orange);
        Check("level 75 without the log: event 10122", $"{EvStr(orange, 10122)}/10122", $"{Ev()}/{Cur()}");
        EventEnd(10122, 1);
        int magianLog = Ki("MAGIAN_TRIAL_LOG");
        Check("event 10122 finished (option 1): npcUtil.giveKeyItem's MAGIAN_TRIAL_LOG and KEYITEM_OBTAINED", $"True/{Text(RuLude, "KEYITEM_OBTAINED")}:{magianLog}",
              $"{HasKi("MAGIAN_TRIAL_LOG")}/{Special()}");
        Talk(orange);
        Check("with the log, no trials: event 10123 (no packed trials, count 0: 0x034 for the params given)", $"{EvStr(orange, 10123, 0, 0, 0, 0, 0, 0, 0, 0)}/10123", $"{Ev()}/{Cur()}");
        EventEnd(10123, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian: the Peeler traded (no trial on it), the menu's updates, trial 2 taken");
        string trialsForPeeler = Lua($"local t = {{}} for id, d in pairs(xi.magian.trials) do if d.requiredItem.itemId == {Peeler} and not d.requiredItem.itemAugments then table.insert(t, id) end end table.sort(t) report(table.concat(t, ' '))");
        Check("magian_data: the plain Peeler starts trial 2 alone", "2", trialsForPeeler);
        Trade(orange, (1, 1));
        Check("traded: event 10124 (trial 2, the item id), the Peeler taken (tradeComplete)", $"{EvStr(orange, 10124, 2, 0, 0, 0, 0, Peeler)}/10124/0",
              $"{Ev()}/{Cur()}/{InvCount(Peeler)}");
        Check("  storeTrialId 2", "2", Lua("report(player:getLocalVar('storeTrialId'))"));
        EventEnd(10124, 1u | 2u << 16, 1);
        Check("  update 1 (trial 2): 2, no augments, the Peeler, no trade item, text offset 1", $"2,0,0,{Peeler},0,0,1", Upds());
        EventEnd(10124, 2u | 2u << 16, 1);
        // getTrialProgress is nil for a trial not taken: updateEvent's nil progress sends 0
        Check("  update 2 (trial 2): the objectives 3, no progress (not taken)", "3", Upds());
        EventEnd(10124, 3u | 2u << 16, 1);
        Check("  update 3 (trial 2): the reward Renegade", $"2,0,0,{Renegade}", Upds());
        EventEnd(10124, 4u | 2u << 16, 1);
        Check("  update 4 (trial 2): its child trial 3, its parent 0", "3", Upds());
        EventEnd(10124, 7u | 2u << 8, 1);
        Check("  update 7 (trial 2): not in progress: 2, no augments, the Peeler", $"2,0,0,{Peeler}", Upds());
        EventEnd(10124, 13u | 2u << 8, 1);
        Check("  update 13 (trial 2): the Peeler and the Renegade both level 75: 0", "", Upds());
        EventEnd(10124, 14u | 2u << 8, 1);
        Check("  update 14 (trial 2): no Renegade held: 0", "", Upds());
        EventEnd(10124, 7u | 2u << 8);
        Check("event 10124 finished (7, trial 2): the Peeler back with trial 2 inscribed, RETURN_MAGIAN_ITEM",
              $"1/{Text(RuLude, "RETURN_MAGIAN_ITEM")}:{Peeler}", $"{InvCount(Peeler)}/{Special()}");
        int hasAugments = int.Parse(Lua("report(xi.augment.kind.HAS_AUGMENTS)")),
            standardTrial = int.Parse(Lua("report(xi.augment.subKind.STANDARD + xi.augment.subKind.TRIAL)"));
        Check("  its exdata: HAS_AUGMENTS, STANDARD + TRIAL, trial 2 not completed (getTrialNumber 2)", $"{hasAugments}/{standardTrial}/2/false/0:0/2",
              $"{Exdata(Peeler)}/{Lua($"report(player:findItem({Peeler}):getTrialNumber())")}");
        Check("  [trial]1 = trial 2 << 16 (no progress), storeTrialId cleared", $"{2 << 16}/0", $"{Var("[trial]1")}/{Lua("report(player:getLocalVar('storeTrialId'))")}");
        Check("  char_inventory.extra saved with the trial", "True", (DbRow(db, "HEX(extra)", $"FROM char_inventory WHERE charid = ? AND itemId = {Peeler}") is var hex &&
                                                                     hex.Length >= 24 && hex.Substring(20, 4) == "0200").ToString());
        Talk(orange);
        Check("the moogle with trial 2 taken: event 10123 (trial 2 packed, count 1)", EvStr(orange, 10123, 2, 0, 0, 0, 0, 0, 0, 1), Ev());
        EventEnd(10123, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian: the inscribed Peeler traded back (the trial's status), returned");
        byte peelerSlot = InvItem(Peeler).Slot;
        Trade(orange, (peelerSlot, 1));
        Check("traded with trial 2 under way: event 10125 (trial 2, the Peeler, .., MAX_UINT32 - 1)",
              $"{EvStr(orange, 10125, 2, Peeler, 0, 0, 0, 0, 0, 4294967294)}/0", $"{Ev()}/{InvCount(Peeler)}");
        EventEnd(10125, 2u | 2u << 16, 1);
        Check("  update 2 (trial 2): 3 objectives, no progress", "3", Upds());
        EventEnd(10125, 0);
        Check("event 10125 finished (0): the Peeler back, still trial 2", $"1/{Text(RuLude, "RETURN_MAGIAN_ITEM")}:{Peeler}/2",
              $"{InvCount(Peeler)}/{Special()}/{Lua($"report(player:findItem({Peeler}):getTrialNumber())")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Hunts ----
        Console.WriteLine("Hunts: Port Windurst's Hunt Registry, hunt 287 (Nocuous Weapon) registered");
        WarpTo(PortWindurst, -222.5f, -8.0f, 178.0f);
        Check("in Port Windurst", $"{PortWindurst}", c.Zone);
        uint registry = NpcId("Hunt_Registry");
        int huntMenu1 = int.Parse(Lua("report(2096896)"));
        Talk(registry);
        string pwMenu = Ev();
        Check("the registry: event 1500 (no scylds, no hunt: 0, the registry's first menu)", "True/1500",
              $"{pwMenu.StartsWith($"034:{registry}:1500:0,")}/{Cur()}");
        EventEnd(1500, 346, 1);
        Check("  update 346 (Inner Horutoto Ruins, hunt 287): the region's info last (1, .., the params, bounty 5 << 10, 1)",
              $"1,0,0,0,0,35103,{5 << 10},1", Upds().Split(" | ").Last());
        EventEnd(1500, 287u << 3);
        int pwText(string n) => Text(PortWindurst, n);
        Check("event 1500 finished (hunt 287 << 3): [hunt]status 1, [hunt]id 287, HUNT_ACCEPTED, USE_SCYLDS (no fee)",
              $"1/287/{pwText("HUNT_ACCEPTED")}:0,{pwText("USE_SCYLDS")}:0", $"{Var("[hunt]status")}/{Var("[hunt]id")}/{Special()}");
        Talk(registry);
        Check("the registry with the hunt under way: scyld bits 287 << 4 + 2", $"034:{registry}:1500:{(287 << 4) + 2}", string.Join(":", Ev().Split(':').Take(3)) + ":" + Ev().Split(':')[3].Split(',')[0]);
        EventEnd(1500, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian / Hunts: the Peeler on, Nocuous Weapon killed in Inner Horutoto Ruins");
        Equip(InvItem(Peeler).Slot, CharStats.SlotMain);
        Check("the Peeler on (main)", $"{Peeler}", c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        WarpTo(InnerHorutoto, -259.0f, -1.0f, -21.0f);
        Check("in Inner Horutoto Ruins", $"{InnerHorutoto}", c.Zone);
        const uint NocuousWeapon = 17563801;
        MobEntity Nocuous()
        {
            var m = zone.ZoneInfoOf(InnerHorutoto).Mobs.First(x => x.Id == NocuousWeapon);
            if (!m.IsSpawned || !m.IsAlive) Lua($"SpawnMob({NocuousWeapon})");
            return m;
        }
        int trialComplete = 584;
        var nw = Nocuous();
        Check("Nocuous Weapon up (a GM's SpawnMob when it is not)", "True", nw.IsSpawned && nw.IsAlive);
        var kill1 = Kill(nw);
        Check("killed: onMobDeath's xi.magian.onMobDeath (MAGIAN_TRIAL_COMPLETE - 1: trial 2, 2 to go), checkHunt (FOV_DEFEATED_TARGET + 20)",
              $"True/{trialComplete - 1}:2:2,578:0:0", $"{nw.IsDead}/{BasicOf(kill1, trialComplete - 1, trialComplete, 578)}");
        Check("  [trial]1 progress 1, [hunt]status 2", $"{(2 << 16) + 1}/2", $"{Var("[trial]1")}/{Var("[hunt]status")}");
        for (int k = 0; k < 30 && nw.IsSpawned; k++) Tick(1000);
        nw = Nocuous();
        var kill2 = Kill(nw);
        Check("killed again: trial 2, 1 to go; no hunt message (status 2)", $"True/{trialComplete - 1}:2:1", $"{nw.IsDead}/{BasicOf(kill2, trialComplete - 1, trialComplete, 578)}");
        for (int k = 0; k < 30 && nw.IsSpawned; k++) Tick(1000);
        Equip(0, CharStats.SlotMain);
        nw = Nocuous();
        var kill3 = Kill(nw);
        Check("killed with the Peeler off: no progress", $"/{(2 << 16) + 2}", $"{BasicOf(kill3, trialComplete - 1, trialComplete)}/{Var("[trial]1")}");
        for (int k = 0; k < 30 && nw.IsSpawned; k++) Tick(1000);
        Equip(InvItem(Peeler).Slot, CharStats.SlotMain);
        nw = Nocuous();
        var kill4 = Kill(nw);
        Check("killed with it on: MAGIAN_TRIAL_COMPLETE (trial 2)", $"{trialComplete}:2:0", BasicOf(kill4, trialComplete - 1, trialComplete));
        Check("  [trial]1 progress 3", $"{(2 << 16) + 3}", Var("[trial]1"));
        for (int k = 0; k < 30 && nw.IsSpawned; k++) Tick(1000);
        nw = Nocuous();
        var kill5 = Kill(nw);
        Check("killed once more: no progress past the objectives", $"/{(2 << 16) + 3}", $"{BasicOf(kill5, trialComplete - 1, trialComplete)}/{Var("[trial]1")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Hunts: the hunt recorded at the registry, the lockout");
        WarpTo(PortWindurst, -222.5f, -8.0f, 178.0f);
        Talk(registry);
        Check("the registry with the hunt done: scyld bits 287 << 4 + 0xA", $"{(287 << 4) + 0xA}", Ev().Split(':')[3].Split(',')[0]);
        EventEnd(1500, 5);
        long midnight = long.Parse(Lua("report(getVanaMidnight())"));
        Check("event 1500 finished (5): 5 scylds, [hunt]nextHunt the next Vana'diel midnight, the vars cleared, HUNT_RECORDED, OBTAIN_SCYLDS",
              $"5/{midnight}/0/0/{pwText("HUNT_RECORDED")}:0,{pwText("OBTAIN_SCYLDS")}:5:5",
              $"{Currency("scyld")}/{Var("[hunt]nextHunt")}/{Var("[hunt]id")}/{Var("[hunt]status")}/{Special()}");
        Talk(registry);
        Check("the registry before the midnight: the lock bit and the scylds (5 << 14 + 1 << 24)", $"{(5 << 14) + (1 << 24)}", Ev().Split(':')[3].Split(',')[0]);
        EventEnd(1500, 287u << 3);
        Check("  hunt 287 asked for while locked: nothing", "0/0", $"{Var("[hunt]status")}/{Var("[hunt]id")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Hunts: past the midnight, a hunt with a fee, cancelled");
        Lua("player:setCharVar('[hunt]nextHunt', GetSystemTime() - 1)");
        Lua("player:addCurrency('scyld', 20)");
        Talk(registry);
        Check("the registry after the lockout: the scylds alone (25 << 14)", $"{25 << 14}", Ev().Split(':')[3].Split(',')[0]);
        // hunt 290: look up a Port Windurst hunt with a fee
        string feeHunt = Lua("report(0)");
        EventEnd(1500, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian: the completed trial at the moogle, the reward");
        WarpTo(RuLude, -11.0f, 2.5f, 118.0f);
        Equip(0, CharStats.SlotMain);
        Near(orange);
        Trade(orange, (InvItem(Peeler).Slot, 1));
        // magianOnTrade passes trialData.rewardItem (a table) as the fourth param: ParseEvent's non-number params are 0
        Check("traded with trial 2 complete: event 10129 (the reward table as 0, the Peeler last), the Peeler taken",
              $"{EvStr(orange, 10129, 0, 0, 0, 0, 0, 0, 0, Peeler)}/0", $"{Ev()}/{InvCount(Peeler)}");
        EventEnd(10129, 0);
        Check("event 10129 finished (0): the Renegade (no augments), ITEM_OBTAINED, [trial]1 cleared",
              $"1/{Text(RuLude, "ITEM_OBTAINED")}:{Renegade}/0", $"{InvCount(Renegade)}/{Special()}/{Var("[trial]1")}");
        Check("  the Renegade's exdata: HAS_AUGMENTS, STANDARD (AugmentStandard's table: no trial)", $"{hasAugments}/{int.Parse(Lua("report(xi.augment.subKind.STANDARD)"))}/0/nil/0:0", Exdata(Renegade));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian: the Renegade's trial 3 taken, abandoned at the moogle's menu (updates 5 and 6)");
        Trade(orange, (InvItem(Renegade).Slot, 1));
        Check("the plain Renegade traded: event 10124 (trial 3 alone)", EvStr(orange, 10124, 3, 0, 0, 0, 0, Renegade), Ev());
        EventEnd(10124, 7u | 3u << 8);
        Check("event 10124 finished (7, trial 3): [trial]1 trial 3, the Renegade inscribed", $"{3 << 16}/3",
              $"{Var("[trial]1")}/{Lua($"report(player:findItem({Renegade}):getTrialNumber())")}");
        Talk(orange);
        Check("the moogle: event 10123 (trial 3, count 1)", EvStr(orange, 10123, 3, 0, 0, 0, 0, 0, 0, 1), Ev());
        EventEnd(10123, 5, 1);
        Check("  update 5: the trials to abandon (trial 3, count 1)", "3,0,0,0,0,0,0,1", Upds());
        EventEnd(10123, 6u | 3u << 8, 1);
        Check("  update 6 (trial 3): its slot 1 sent, [trial]1 cleared", "0,0,0,0,0,1/0", $"{Upds()}/{Var("[trial]1")}");
        EventEnd(10123, 0);
        Check("  the Renegade keeps trial 3 inscribed", "3", Lua($"report(player:findItem({Renegade}):getTrialNumber())"));
        Trade(orange, (InvItem(Renegade).Slot, 1));
        Check("the inscribed Renegade traded with no trial 3 kept: event 10125 (trial 3, reqItem nil as 0, .., MAX_UINT32 - 2)",
              EvStr(orange, 10125, 3, 0, 0, 0, 0, 0, 0, 4294967293), Ev());
        EventEnd(10125, 8u | 3u << 8, 1);
        Check("  update 8 (trial 3): the required Renegade", $"0,0,0,{Renegade}", Upds());
        EventEnd(10125, 8u | 3u << 8);
        Check("event 10125 finished (8, trial 3): the Renegade back with no trial, RETURN_MAGIAN_ITEM", $"1/0/{Text(RuLude, "RETURN_MAGIAN_ITEM")}:{Renegade}",
              $"{InvCount(Renegade)}/{Lua($"report(player:findItem({Renegade}):getTrialNumber())")}/{Special()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian: ten trials under way, an eleventh refused");
        for (int t = 1; t <= 10; t++) Lua($"player:setCharVar('[trial]{t}', {(1000 + t) << 16})");
        // getPlayerTrialData's cache is refreshed when magianZoned is not 1 (as after a zone change)
        Lua("player:setLocalVar('magianZoned', 0)");
        Trade(orange, (InvItem(Renegade).Slot, 1));
        Check("the plain Renegade traded with ten trials: event 10124 (.., MAX_UINT32 - 254), nothing taken",
              $"{EvStr(orange, 10124, 0, 0, 0, 0, 0, 0, 0, 4294967041)}/1", $"{Ev()}/{InvCount(Renegade)}");
        EventEnd(10124, 0);
        for (int t = 1; t <= 10; t++) Lua($"player:setCharVar('[trial]{t}', 0)");
        Lua("player:setLocalVar('magianZoned', 0)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Magian: the trial listeners ----
        Console.WriteLine("Magian: trial 82 (Pugilists: Skeletons x50 on any day) taken by a MNK");
        Fixture(db, SkeletonId, RuLude, -11.0f, 2.5f, 118.0f, Windurst, Mnk, 75, (Pugilists, 1));
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 1, 2300, 0)", BoundValue.Of(SkeletonId));
        CharId = SkeletonId;
        Login();
        Lua("player:addKeyItem(xi.keyItem.MAGIAN_TRIAL_LOG)");
        Near(orange);
        Trade(orange, (1, 1));
        Check("the Pugilists traded: event 10124 (trials 82 and 68, magian_data's order)", EvStr(orange, 10124, 82, 68, 0, 0, 0, Pugilists), Ev());
        EventEnd(10124, 2u | 82u << 16, 1);
        Check("  update 2 (trial 82): 50 objectives, no progress, the day / weather (any: 10) as the optional param",
              "50,0,0,0,0,10", Upds());
        EventEnd(10124, 7u | 82u << 8);
        Check("event 10124 finished (7, trial 82): [trial]1 trial 82, the Pugilists inscribed", $"{82 << 16}/82",
              $"{Var("[trial]1")}/{Lua($"report(player:findItem({Pugilists}):getTrialNumber())")}");
        Equip(InvItem(Pugilists).Slot, CharStats.SlotMain);
        Check("the Pugilists on: xi.items.pugilists.onItemEquip (xi.magian.onItemEquip)", $"{Pugilists}", c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Magian: the DEFEATED_MOB listener: Skeletons killed in Inner Horutoto Ruins");
        WarpTo(InnerHorutoto, -259.0f, -1.0f, -21.0f);
        int skeleton = int.Parse(Lua("report(xi.mobFamily.SKELETON)"));
        // the skeletons to fight: spawned, at home (inside their roam region, else within their roam distance of the
        // spawn point) and not despawning. One out of its region despawns on its next idle tick
        // (CMobController::DoRoamTick), and a kill on it never lands
        bool AtHome(MobEntity m)
        {
            if (m.RoamRegion != null) return m.RoamRegion.DistanceOutside(m.X, m.Y, m.Z) <= 0;
            float dx = m.X - m.SpawnX, dz = m.Z - m.SpawnZ;
            return MathF.Sqrt(dx * dx + dz * dz) <= m.MaxRoamDistance;
        }
        List<MobEntity> Bones() => zone.ZoneInfoOf(InnerHorutoto).Mobs.Where(m => m.IsSpawned && m.IsAlive &&
            m.FadeAtMs == 0 && AtHome(m) &&
            Lua($"report(GetMobByID({m.Id}):getFamily())") == skeleton.ToString()).ToList();
        var bones = Bones();
        Check("Skeletons up in Inner Horutoto Ruins (Wendigos, levels 25 to 28)", "True", bones.Count(b => b.Name == "Wendigo" && b.MLvl <= 28) >= 4);
        // each kill on a Wendigo picked then (the ones killed before are not alive), not on one picked before the
        // kills: one at home then may have left it since
        MobEntity NextWendigo() => Bones().First(b => b.Name == "Wendigo");
        var weak = NextWendigo();
        var weakKill = Kill(weak);
        Check($"a level {weak.MLvl} {weak.Name} killed: too weak for kill credit (checkKillCredit), no progress",
              $"True//{82 << 16}", $"{!weak.IsAlive}/{BasicOf(weakKill, trialComplete - 1, trialComplete)}/{Var("[trial]1")}",
              killDetail);
        var tough = NextWendigo();
        Lua($"GetMobByID({tough.Id}):setMobLevel(70)");
        var toughKill = Kill(tough);
        Check($"a {tough.Name} at level 70 (a GM's setMobLevel) killed: the day's element in any: 1 (MAGIAN_TRIAL_COMPLETE - 1: 49 to go)",
              $"True/{trialComplete - 1}:82:49/{(82 << 16) + 1}", $"{!tough.IsAlive}/{BasicOf(toughKill, trialComplete - 1, trialComplete)}/{Var("[trial]1")}",
              killDetail);
        Lua("player:setWeather(xi.weather.RAIN)");
        var wet = NextWendigo();
        Lua($"GetMobByID({wet.Id}):setMobLevel(70)");
        var wetKill = Kill(wet);
        Check($"another in the rain: the day's element 1 and the weather's 5: 6 (43 to go)",
              $"True/{trialComplete - 1}:82:43/{(82 << 16) + 7}", $"{!wet.IsAlive}/{BasicOf(wetKill, trialComplete - 1, trialComplete)}/{Var("[trial]1")}",
              killDetail);
        Lua("player:setWeather(xi.weather.NONE)");
        Equip(0, CharStats.SlotMain);
        var bare = NextWendigo();
        Lua($"GetMobByID({bare.Id}):setMobLevel(70)");
        var bareKill = Kill(bare);
        Check("the Pugilists off (onItemUnequip removes the listener): a kill gives nothing", $"True//{(82 << 16) + 7}",
              $"{!bare.IsAlive}/{BasicOf(bareKill, trialComplete - 1, trialComplete)}/{Var("[trial]1")}",
              killDetail);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Garrison ----
        Console.WriteLine("Garrison: West Ronfaure's outpost guard of the region's owner, the entry checks");
        // the char of the nation that holds Ronfaure (conquest_system as the world has it), by that nation's guard
        int owner = int.Parse(Lua("report(GetRegionOwner(xi.region.RONFAURE))"));
        Check("Ronfaure held by a nation (not the beastmen)", "True", owner <= 2);
        var guardName = new[] { "Doladepaiton_RK", "Yoshihiro_IM", "Kyanta-Pakyanta_WW" }[Math.Min(owner, 2)];
        const ushort RedCryptex = 1528;
        Fixture(db, GarrisonId, WestRonfaure, -449.0f, -20.0f, -220.0f, (byte)Math.Min(owner, 2), War, 75, (RedCryptex, 1), (RedCryptex, 1));
        CharId = GarrisonId;
        Login();
        Lua($"player:setRank({1})");
        uint guard = NpcId(guardName);
        Check($"in West Ronfaure, {guardName} shown (the owner's guard)", $"{WestRonfaure}/True", $"{c.Zone}/{Npc(guard).Status == NpcEntity.StatusNormal}");
        int garrisonBase = Text(WestRonfaure, "GARRISON_BASE");
        string Talks() => string.Join(",", Of(0x036).Select(p => BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF));
        Near(guard);
        Trade(guard, (1, 1));
        Check("a Red Cryptex traded at rank 1: GARRISON_BASE + 4 (lacks battle experience), no event, the cryptex kept",
              $"{garrisonBase + 4}/none/2", $"{Talks()}/{Cur()}/{InvCount(RedCryptex)}");
        Lua("player:setRank(2)");
        Trade(guard, (1, 1), (2, 1));
        Check("two cryptexes at rank 2: not tradeHasExactly, the guard's conquest trade (nothing)", "/none", $"{Talks()}/{Cur()}");
        Lua("player:addStatusEffect(xi.effect.LEVEL_SYNC, { power = 30, origin = player })");
        Trade(guard, (1, 1));
        Check("one, level synced: MEMBERS_LEVELS_ARE_RESTRICTED", $"{Text(WestRonfaure, "MEMBERS_LEVELS_ARE_RESTRICTED")}/none", $"{Talks()}/{Cur()}");
        Lua("player:delStatusEffect(xi.effect.LEVEL_SYNC)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Garrison: the cryptex accepted, the start (level cap, lockouts, the ally)");
        Trade(guard, (1, 1));
        int startEvent = 32753 + Math.Min(owner, 2);
        Check($"one at rank 2: event {startEvent} (32753 + the nation)", $"{EvStr(guard, startEvent)}/{startEvent}", $"{Ev()}/{Cur()}");
        EventEnd(startEvent, 0);
        Check("event finished (0): the cryptex taken (confirmTrade), LEVEL_RESTRICTION 20 (ON_ZONE, CONFRONTATION), the char at level 20",
              $"1/20/True/20", $"{InvCount(RedCryptex)}/{Lua("report(player:getStatusEffect(xi.effect.LEVEL_RESTRICTION):getPower())")}/" +
              $"{Lua("local e = player:getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(tostring(e:hasEffectFlag(xi.effectFlag.ON_ZONE) and e:hasEffectFlag(xi.effectFlag.CONFRONTATION)))") == "true"}/{c.MLvl}");
        Check("  LEVEL_IS_RESTRICTED (messageBasic 20)", $"True", Basic().Contains($"{int.Parse(Lua("report(xi.msg.basic.LEVEL_IS_RESTRICTED)"))}:20:"));
        long tally = long.Parse(Lua("report(NextConquestTally())"));
        Check("  the lockouts: [Garrison]NextEntryTime 1 (expiring at the tally), the zone's server var 1800 s on",
              $"1/{EarthNow() + 1800}", $"{Var("[Garrison]NextEntryTime")}/{Lua("report(GetServerVariable('[Garrison]NextEntryTime_100'))")}");
        Check("  char_vars expiry the next conquest tally", $"{tally}", DbRow(db, "expiry", "FROM char_vars WHERE charid = ? AND varname = '[Garrison]NextEntryTime'"));
        Check("  isInGarrison", "true", Lua("report(tostring(xi.garrison.isInGarrison(player)))"));
        string garrisonState() => Lua("local d = xi.garrison.zoneData[xi.zone.WEST_RONFAURE] report(string.format('%s/%d/%d/%d/%d/%d', tostring(d.isRunning), d.state, d.waveIndex, d.groupIndex, #d.npcs, #d.mobs))");
        Check("  zoneData: running, state SPAWN_NPCS ran at once (BATTLE), wave 1, group 1, one ally, no mobs", "true/1/1/1/1/0", garrisonState());
        var allyName = Lua($"report(xi.garrison.allyNames[20][{owner}])");
        var allies = zone.ZoneInfoOf(WestRonfaure).Mobs.Where(m => m.Name == "DE_" + allyName && m.IsSpawned).ToList();
        var ally = allies.FirstOrDefault();
        // spawnNPC sets NO_DROPS before mob:spawn(), whose CalculateMobStats restores the saved mob mods (restoreMobModifiers):
        // the mod is gone in the C++ as well (a script order bug)
        Check($"  the ally: one dynamic {allyName} (GM_HOME group 1) at the outpost, allegiance player, level 15-20, LEVEL_RESTRICTION, " +
              "NO_DROPS wiped by the spawn", $"1/True/1/True/True/0",$"{allies.Count}/{ally != null && MathF.Abs(ally.X + 438) < 1 && MathF.Abs(ally.Z + 223) < 1}/{ally?.Allegiance}/" +
              $"{ally?.MLvl is >= 15 and <= 20}/{Lua($"report(tostring(GetMobByID({ally?.Id}):hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))") == "true"}/" +
              $"{Lua($"report(GetMobByID({ally?.Id}):getMobMod(xi.mobMod.NO_DROPS))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Garrison: a second trade while it runs");
        Trade(guard, (InvItem(RedCryptex).Slot, 1));
        Check("the other cryptex traded while running: GARRISON_BASE + 1, no event", $"{garrisonBase + 1}/none", $"{Talks()}/{Cur()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Garrison: the waves (one party: 2, 2+2, 2+2+2, 2+2+2+2 every 15 s), the ally left to fight alone: the loss");
        uint boss = zone.ZoneInfoOf(WestRonfaure).Mobs.First(m => m.Name == "Orcish_Fighterchief").Id;
        var pool = Enumerable.Range(0, 8).Select(i => zone.ZoneInfoOf(WestRonfaure).MobById[(uint)(boss - 8 + i)]).ToList();
        pool.Add(zone.ZoneInfoOf(WestRonfaure).MobById[boss]);
        string Running() => Lua("report(tostring(xi.garrison.zoneData[xi.zone.WEST_RONFAURE].isRunning))");
        // the garrison's ticks with the char by the outpost; `fight`: the char swings at what is up (each brought to 1 HP)
        (List<string> States, bool Capped, bool NoExp, bool HateAlly, List<BasicPacket> Packets) RunGarrison(MobEntity theAlly, bool fight)
        {
            var states = new List<string>();
            var packets = new List<BasicPacket>();
            bool capped = true, noExp = true, hateAlly = true;
            var seen = new HashSet<MobEntity>();
            for (int sec = 0; sec < 900 && Running() == "true"; sec++)
            {
                s.PacketList.Clear();
                Tick(1000);
                packets.AddRange(s.PacketList);
                var up = pool.Where(m => m.IsSpawned && m.IsAlive).ToList();
                foreach (var m in up.Where(m => seen.Add(m)))
                {
                    // a wave's mob as it comes: capped, no exp, hating the ally (aggroGroups)
                    capped &= Lua($"local e = GetMobByID({m.Id}):getStatusEffect(xi.effect.LEVEL_RESTRICTION) report(e and e:getPower() or 0)") == "20";
                    noExp &= Lua($"report(GetMobByID({m.Id}):getMobMod(xi.mobMod.EXP_BONUS))") == "-100";
                    hateAlly &= m.Enmity.HasId(theAlly.Id);
                    string st = garrisonState();
                    if (!states.Contains(st)) states.Add(st);
                }
                seen.RemoveWhere(m => !m.IsSpawned);
                if (!fight) continue;
                var target = up.FirstOrDefault();
                if (target == null) continue;
                target.Hp = Math.Min(target.Hp, 1);
                // the char keeps beside it (west of it, facing east) as it moves on the ally
                if (MathF.Abs(c.X - (target.X - 2.0f)) > 0.5f || MathF.Abs(c.Z - target.Z) > 0.5f)
                    Send(0x015, 32, b =>
                    {
                        BitConverter.GetBytes(target.X - 2.0f).CopyTo(b, C2sPos.OffX);
                        BitConverter.GetBytes(target.Y).CopyTo(b, C2sPos.OffZ);
                        BitConverter.GetBytes(target.Z).CopyTo(b, C2sPos.OffY);
                        b[C2sPos.OffDir] = 0;
                    });
                if (c.AttackTarget != target)
                {
                    Send(0x01A, 28, b =>
                    {
                        BitConverter.GetBytes(target.Id).CopyTo(b, 4);
                        BitConverter.GetBytes(target.TargId).CopyTo(b, C2sAction.OffActIndex);
                        BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
                    });
                }
            }
            return (states, capped, noExp, hateAlly, packets);
        }
        string TalksOf(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x036).Select(p => BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF));
        Lua("player:setUnkillable(true)");
        // by the guard, away from the outpost's fight
        Near(guard);
        var loss = RunGarrison(ally, false);
        Console.WriteLine("  states seen: " + string.Join(" ", loss.States));
        Check("wave 1 (2 mobs) 15 s in, its groups as they come, the mobs capped at 20 with no exp and hating the ally", "True/True/True/True",
              $"{loss.States.FirstOrDefault()?.StartsWith("true/1/1/2/1/2")}/{loss.Capped}/{loss.NoExp}/{loss.HateAlly}");
        Check("the ally fights them (its enmity on the orcs: GetHighestEnmity keeps another allegiance's mob) and falls: GARRISON_BASE + 39, the garrison stopped",
              $"True/{garrisonBase + 39}/false", $"{!ally.IsAlive || !ally.IsSpawned}/{TalksOf(loss.Packets)}/{Running()}");
        long lockout = EarthNow() + 1800;
        bool fading = pool.Any(m => m.IsSpawned && m.IsAlive && m.FadeAtMs != 0);
        // DespawnMob's fade out (CDespawnState) over the next ticks
        for (int i = 0; i < 10; i++) Tick(1000);
        Check("  stop: LEVEL_RESTRICTION gone (the char at 75), the mobs (DespawnMob: fading, then gone) and the ally despawned, not in the garrison, the zone lockout renewed",
              $"false/75/True/True/False/false/{lockout}",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.LEVEL_RESTRICTION)))")}/{c.MLvl}/{fading}/{pool.All(m => !m.IsSpawned)}/" +
              $"{zone.ZoneInfoOf(WestRonfaure).Mobs.Any(m => m.Name == "DE_" + allyName && m.IsSpawned)}/" +
              $"{Lua("report(tostring(xi.garrison.isInGarrison(player)))")}/{Lua("report(GetServerVariable('[Garrison]NextEntryTime_100'))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Garrison: the lockouts, a second garrison won (the ally kept up, the char fighting)");
        for (int i = 0; i < 10; i++) Tick(1000);
        Trade(guard, (InvItem(RedCryptex).Slot, 1));
        Check("the other cryptex the same week: the tally lockout ([Garrison]NextEntryTime): GARRISON_BASE + 40", $"{garrisonBase + 40}/none", $"{Talks()}/{Cur()}");
        Lua("player:setCharVar('[Garrison]NextEntryTime', 0)");
        Trade(guard, (InvItem(RedCryptex).Slot, 1));
        Check("  the tally lockout cleared (a GM's setCharVar), the zone's 30 minutes not over: GARRISON_BASE + 40", $"{garrisonBase + 40}/none", $"{Talks()}/{Cur()}");
        // half an hour on (the clock moved, then a tick)
        Tick(1_800_000);
        Trade(guard, (InvItem(RedCryptex).Slot, 1));
        EventEnd(startEvent, 0);
        Check("half an hour on: the event, finished: the garrison running again, the cryptex taken", $"true/0", $"{Running()}/{InvCount(RedCryptex)}");
        var ally2 = zone.ZoneInfoOf(WestRonfaure).Mobs.First(m => m.Name == "DE_" + allyName && m.IsSpawned && m.IsAlive);
        // a GM keeps the ally up
        Lua($"GetMobByID({ally2.Id}):setUnkillable(true)");
        long gilBefore = long.Parse(Lua("report(player:getGil())"));
        var win = RunGarrison(ally2, true);
        Console.WriteLine("  states seen: " + string.Join(" ", win.States));
        Check("the waves: 1 (2), 2 (2 + 2), 3 (2 + 2 + 2), 4 (2 + 2 + 2 + 2), then the boss (9 ids this wave)", "True",
              win.States.Any(w => w.StartsWith("true/1/1/2/1/2")) && win.States.Any(w => w.StartsWith("true/1/2/3/1/4")) &&
              win.States.Any(w => w.StartsWith("true/1/3/4/1/6")) && win.States.Any(w => w.StartsWith("true/1/4/5/1/8")) &&
              win.States.Any(w => w.StartsWith("true/1/4/5/1/9")));
        Check("  the mobs capped at 20 with no exp and hating the ally", "True/True/True", $"{win.Capped}/{win.NoExp}/{win.HateAlly}");
        string gilMsgs = string.Join(",", win.Packets.Where(p => Opcode(p) == 0x02A).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        Check("all dead: GRANT_LOOT: GARRISON_BASE + 36, GIL_OBTAINED 2000 (GIL_RATE x 20 x 100 x 1), the gil given (with the orcs' own gil drops)",
              $"{garrisonBase + 36}/True/True", $"{TalksOf(win.Packets)}/{gilMsgs.Contains($"{Text(WestRonfaure, "GIL_OBTAINED")}:2000")}/{long.Parse(Lua("report(player:getGil())")) >= gilBefore + 2000}");
        Check("  ENDED: stopped, LEVEL_RESTRICTION gone", "false/75", $"{Running()}/{c.MLvl}");
        // handleLootRolls: one item of the level 20 table into the treasure pool (addTreasure)
        var lootIds = Lua("local t = {} for _, e in ipairs(xi.garrison.loot[20]) do table.insert(t, e.itemId) end report(table.concat(t, ' '))").Split(' ').Select(ushort.Parse).ToList();
        for (int i = 0; i < 400 && !c.Items.Any(it => it.Location == Container.Inventory && lootIds.Contains(it.ItemId)); i++) Tick(1000);
        Check("  the loot (one of the level 20 table, addTreasure: the treasure pool) the char's in time", "True",
              c.Items.Any(it => it.Location == Container.Inventory && lootIds.Contains(it.ItemId)));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        Lua("player:setUnkillable(false)");
        // ---- Ambuscade ----
        Console.WriteLine("Ambuscade: Gorpa-Masorpa and Records of Eminence 499 (Stepping into an Ambuscade)");
        Fixture(db, AmbuscadeId, Mhaura, -26.0f, -16.0f, 52.5f, Windurst, War, 75);
        CharId = AmbuscadeId;
        Login();
        uint gorpa = NpcId("Gorpa-Masorpa"), tome = NpcId("Ambuscade_Tome");
        Near(gorpa);
        Talk(gorpa);
        Check("Gorpa-Masorpa without record 499: event 384 (set the record)", $"{EvStr(gorpa, 384)}/384", $"{Ev()}/{Cur()}");
        EventEnd(384, 0);
        // GP_CLI_COMMAND_ROE_START: the record taken from the RoE menu
        s.PacketList.Clear();
        Send(0x10C, 8, b => BitConverter.GetBytes((ushort)499).CopyTo(b, 4));
        Check("record 499 taken (0x10C): ROE_START, progress 0", $"True/0", $"{Basic().Contains("704:499:")}/{Lua("report(player:getEminenceProgress(499))")}");
        Talk(gorpa);
        Check("with record 499 under way: event 385 (the intro)", $"{EvStr(gorpa, 385)}/385", $"{Ev()}/{Cur()}");
        long expBefore = c.JobExp[c.MJob];
        EventEnd(385, 0);
        Check("event 385 finished: xi.roe.onRecordTrigger(499) completes it: ROE_COMPLETE, the first-time 300 sparks, 300 exp, the Ambuscade Primer Volume Two",
              $"True/True/300/True/True", $"{Basic().Contains("690:499:")}/{Lua("report(tostring(player:getEminenceCompleted(499)))") == "true"}/" +
              $"{Currency("spark_of_eminence")}/{c.JobExp[c.MJob] > expBefore || c.MLvl > 75}/{HasKi("AMBUSCADE_PRIMER_VOLUME_TWO")}");
        Lua("player:addCurrency('current_hallmarks', 120) player:addCurrency('total_hallmarks', 340) player:addCurrency('gallantry', 55)");
        Talk(gorpa);
        Check("the record done: event 386 (menu 0, the hallmarks 120 / 340, 0, 8, the gallantry 55)", EvStr(gorpa, 386, 0, 120, 340, 0, 8, 55, 0, 0), Ev());
        foreach (uint opt in new uint[] { 1, 2, 5, 6, 9, 10, 3 })
        {
            EventEnd(386, opt, 1);
            Check($"  update {opt}: all zeros (the menus are TODOs)", "", Upds());
        }
        EventEnd(386, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Ambuscade: the Ambuscade Tome, the instance (Maquette Abdhaljs-Legion B)");
        Lua("player:addKeyItem(xi.keyItem.AMBUSCADE_PRIMER_VOLUME_ONE)");
        Near(tome);
        Talk(tome);
        Check("the tome: event 374 (menu 12, the starting difficulties 119 / 109, page 735, 5)", $"{EvStr(tome, 374, 12, 119, 109, 735, 5)}/374", $"{Ev()}/{Cur()}");
        EventEnd(374, 3, 1);
        Check("  an update: none sent (a TODO)", "", Upds());
        EventEnd(374, 5);
        // the instance loaded (CInstanceLoader), then the requester's onInstanceCreatedCallback: setInstance, setPos into 287
        for (int i = 0; i < 10 && c.Zone == Mhaura; i++)
        {
            Tick(400);
            if (c.Zone != Mhaura || Lua("report(tostring(player:getInstance() ~= nil))") == "true") break;
        }
        Check("event 374 finished (5): createInstance(30000): the instance, the char set into it", "true", Lua("report(tostring(player:getInstance() ~= nil))"));
        Rezone(false);
        Check("  into Maquette Abdhaljs-Legion B: onInstanceZoneIn puts the char at the entry (137, 12.5, -137), the time limit 30 minutes",
              $"{Maquette}/137/-137/1800", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{Lua("report(player:getInstance():getTimeLimit())")}");
        // Bozzetto Breadwinner's spawn (17952867, ID.mob's one mob) has no template in the zone's mobs.yaml: InsertMobs leaves it
        // out (zoneutils::InsertMobs the same), so SpawnMob finds nothing and the run has no mobs
        Check("  onInstanceCreated's SpawnMob(17952867): no mob in the run (the spawn has no template in the data)", "0",
              Lua("local n = 0 for _ in pairs(player:getInstance():getMobs()) do n = n + 1 end report(n)"));
        // CInstance::CheckTime: once the char is in, onInstanceTimeUpdate every second; no mob alive: instance:complete()
        for (int i = 0; i < 10 && Cur() != "10001"; i++) Tick(400);
        Check("onInstanceTimeUpdate with no mob alive: complete(): onInstanceComplete: 200 hallmarks (difficulty 1 x 1 char) and total, no gallantry " +
              "(alone), the Primer Volume One taken, event 10001", $"320/540/55/False/10001",
              $"{Currency("current_hallmarks")}/{Currency("total_hallmarks")}/{Currency("gallantry")}/{HasKi("AMBUSCADE_PRIMER_VOLUME_ONE")}/{Cur()}");
        Check("  the run completed", "true", Lua("report(tostring(player:getInstance():completed()))"));
        EventEnd(10001, 0);
        Rezone();
        Check("event 10001 finished: the instance's onEventFinish: setPos back to Mhaura", $"{Mhaura}/-34/58", $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Mog Garden ----
        Console.WriteLine("Mog Garden: out of the Mog House (0x05E zmrq, exit mode Mog Garden)");
        const ushort WindurstWaters = 238, WesternAdoulin = 256;
        Fixture(db, GardenId, WindurstWaters, 0.0f, 0.0f, 0.0f, Windurst, War, 75);
        Sql(db, "UPDATE chars SET moghouse = ?, home_zone = ?, home_x = -40, home_y = -5, home_z = 100 WHERE charid = ?",
            BoundValue.Of(GardenId), BoundValue.Of(WindurstWaters), BoundValue.Of(GardenId));
        CharId = GardenId;
        Login();
        Check("in Windurst Waters' Mog House", $"{WindurstWaters}/{GardenId}", $"{c.Zone}/{c.MoghouseId}");
        // GP_CLI_COMMAND_MAPRECT: the universal exit zoneline, MyRoomExitMode 127 (Mog Garden)
        Send(0x05E, 24, b =>
        {
            BitConverter.GetBytes(ZoneLine.PackId("zmrq")).CopyTo(b, 4);
            b[22] = 0;
            b[23] = 127;
        });
        Check("the exit to the Mog Garden: out of the Mog House, the destination 280 at 0, 0, 0", "0/0", $"{c.MoghouseId}/{c.X}");
        Rezone();
        Check("into the Mog Garden: onZoneIn's setPos from 0, 0, 0 to (-2.517, 0.452, -5.068)", $"{MogGarden}/-2.5/-5.1",
              $"{c.Zone}/{MathF.Round(c.X, 1)}/{MathF.Round(c.Z, 1)}");
        var gardenInfo = zone.ZoneInfoOf(MogGarden);
        uint greenThumb = NpcId("Green_Thumb_Moogle"), dinghy = NpcId("Mog_Dinghy"), gardenPorter = NpcId("Porter_Moogle");
        var shown = gardenInfo.Npcs.Where(n => n.Status == NpcEntity.StatusNormal).Select(n => n.Id).OrderBy(x => x).ToList();
        Check("xi.mog_garden.onInitialize: every NPC hidden but the Green Thumb Moogle, the first Mog Dinghy and the Porter Moogle",
              string.Join(" ", new[] { greenThumb, dinghy, gardenPorter }.OrderBy(x => x)), string.Join(" ", shown));
        Check("  the second Mog Dinghy and the Ephemeral Moogle hidden", "True",
              gardenInfo.Npcs.Where(n => (n.Name == "Mog_Dinghy" && n.Id != dinghy) || n.Name.StartsWith("Ephemeral_Moogle")).All(n => n.Status != NpcEntity.StatusNormal));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Mog Garden: the Green Thumb Moogle (the Mog House menu, the seed shop)");
        Near(greenThumb);
        Talk(greenThumb);
        Check("the Green Thumb Moogle: event 1016", $"{EvStr(greenThumb, 1016)}/1016", $"{Ev()}/{Cur()}");
        EventEnd(1016, 0xFFF00FF);
        Check("event 1016 finished (0xFFF00FF): no Mog Locker lease (no message), the Mog House menu (sendMenu MOOGLE: 0x02E)", "1/",
              $"{Of(0x02E).Count}/{Special()}");
        Talk(greenThumb);
        EventEnd(1016, 0xFFE00FF);
        var shopList = Of(0x03C);
        Check("event 1016 finished (0xFFE00FF): xi.shop.general's nine seeds (0x03E open, 0x03C list)", "9/True",
              $"{(Of(0x03E).Count > 0 ? BitConverter.ToUInt16(Of(0x03E)[0].Buffer, 4) : -1)}/{shopList.Count > 0}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Mog Garden: the Porter Moogle, the Mog Dinghy to Western Adoulin");
        Near(gardenPorter);
        Talk(gardenPorter);
        Check("the Porter Moogle: event 800 (.., the gil 10000, 1000)", EvStr(gardenPorter, 800, 0x6FFFFF, 1, 0x06DD, 0x27, 0x7C7E, 0x15, 10000, 0x03E8), Ev());
        // the talk event's option: the slip (option / 16 + option % 16); slipIds[option], so option 0 (no slip) is no choice
        // (hasItem(nil) raises, in C++ too); 1: Moogle Storage Slip 01 for 1000 gil
        EventEnd(800, 1);
        Check("event 800 finished (1): Moogle Storage Slip 01 for 1000 gil, ITEM_OBTAINED", $"1/9000/{Text(MogGarden, "ITEM_OBTAINED")}:29312",
              $"{InvCount(29312)}/{Lua("report(player:getGil())")}/{Special()}");
        Talk(gardenPorter);
        EventEnd(800, 1);
        Check("  again: the slip held: ITEM_CANNOT_BE_OBTAINED, no gil taken", $"1/9000/{Text(MogGarden, "ITEM_CANNOT_BE_OBTAINED")}:29312",
              $"{InvCount(29312)}/{Lua("report(player:getGil())")}/{Special()}");
        Talk(gardenPorter);
        Lua("player:setLocalVar('slipId', 5)");
        EventEnd(800, 1000);
        Check("  finished 1000 (no slip): the slipId local var cleared, nothing bought", "0/1/9000",
              $"{Lua("report(player:getLocalVar('slipId'))")}/{InvCount(29312)}/{Lua("report(player:getGil())")}");
        Near(dinghy);
        Talk(dinghy);
        Check("the Mog Dinghy: event 1015 (1 x 8)", $"{EvStr(dinghy, 1015, 1, 1, 1, 1, 1, 1, 1, 1)}/1015", $"{Ev()}/{Cur()}");
        EventEnd(1015, 2);
        Rezone();
        Check("event 1015 finished (2): setPos to Western Adoulin", $"{WesternAdoulin}", c.Zone);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Mog Garden: back by the Mog House, the Green Thumb Moogle's way out (warp: the home point)");
        Lua($"player:setPos(0, 0, 0, 0, {WindurstWaters})");
        Rezone();
        Sql(db, "UPDATE chars SET moghouse = ? WHERE charid = ?", BoundValue.Of(GardenId), BoundValue.Of(GardenId));
        Login();
        Send(0x05E, 24, b =>
        {
            BitConverter.GetBytes(ZoneLine.PackId("zmrq")).CopyTo(b, 4);
            b[23] = 127;
        });
        Rezone();
        Check("in the Mog Garden again", $"{MogGarden}", c.Zone);
        Near(greenThumb);
        Talk(greenThumb);
        EventEnd(1016, 0xFFB00FF);
        Rezone();
        Check("event 1016 finished (0xFFB00FF, Leave this Mog Garden): player:warp(): the home point in Windurst Waters", $"{WindurstWaters}/-40/100",
              $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        if (Environment.GetEnvironmentVariable("CONTENT2_DUMP_LOG") == "1") foreach (var l in log.Distinct()) Console.WriteLine("  log: " + l);
    }
}
