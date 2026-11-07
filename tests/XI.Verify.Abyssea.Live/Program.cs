using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Abyssea.Live -- live gate for Abyssea's systems driven as a client drives them (scripts/globals/abyssea.lua,
// scripts/globals/abyssea/*, the effects visitant / atma, the mixins abyssea_weakness / drop_lights / spawn_pyxis and
// the Abyssea zones' scripts): the 0x00A login, 0x015 moves (the Searing Ward's trigger area), 0x01A Talk, Attack,
// weapon skills and spells, 0x036 trades, 0x05B event ends and updates, the zone's ticks (the effects' ticks, the
// players' and NPCs' Lua timers) and every step's effect: the status effects, char vars, key items, items, currencies,
// the event and message packets and the zone changes. The real scripts on the real MariaDB (xidb), chars 996021-996022.
//
// Visitant status: the maw into Abyssea - Konschtat, the Atma Fabricant without it, afterZoneIn's hidden five minutes,
// the Searing Ward's tether, the countdown messages and the expiry (event 2180 out), the Conflux Surveyor's stones
// (Sojourn abyssites, the Rhapsody in Mauve, the two-hour cap), the time stored on leaving and restored.
// Lights: kills by melee, weapon skills and a spell, the caps and tier messages, the char vars, the surveyor's display.
// Sturdy Pyxis: blue, red and gold chests out of kills, their mini-games, Forbidden Keys, the drops, the timers, another
// char's chest. Atma: the Infusionist's infusions, purges, reinfusion and history, removal out of Abyssea. Cruor: none
// from kills, the Prospectors' wares and enhancements. NMs: the ??? pops, the red / blue / yellow procs, the key items.
// Veridical Confluxes. Dominion: the sergeants' ops and the Grauberg Tactician. The Empyreal Paradox's Visitant status.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static uint CharId = 996021;
    private const uint OtherId = 996022;
    private static readonly uint[] CharIds = { 996021, 996022 };
    private const ushort Konschtat = 108, AbysseaKonschtat = 15;
    private const uint MawKonschtat = 17220154;
    private const ushort BronzeSword = 16535, FastBlade = 32, BurningBlade = 33, Stone = 159;

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

    // a Hume WAR 75 at x / y / z in the zone, with 10000 gil and the items from inventory slot 1 on
    private static void Fixture(IDatabase db, uint id, ushort zoneId, float x, float y, float z, params (ushort Item, uint Qty)[] items)
    {
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of($"abysslive{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of($"Abysslive{id % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)1), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(id));
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

        // a WAR 75 in Konschtat Highlands by the Cavernous Maw
        Fixture(db, CharId, Konschtat, 93.0f, -69.0f, -580.0f, (BronzeSword, 1));
        // the sword skill for Fast Blade and Burning Blade
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 3, 2300, 0)", BoundValue.Of(CharId));
        // a second char in Abyssea - Konschtat's Searing Ward, for another's chest
        Fixture(db, OtherId, AbysseaKonschtat, 140.0f, -72.0f, -820.0f, (2490, 2));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        // the earth clock (the host's and the scripts' GetSystemTime) on the test clock
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
            lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_abyssea");
            return string.Join(",", got);
        }
        var expectedWarnings = new HashSet<string>();
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

        // the 0x00A login and 0x00C; the zone-in cutscene ended with 0x05B as the client does
        void Login()
        {
            if (s != null) zone.OnCharTimeout(s, false);
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            Send(0x00C, 12);
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
        void Rezone()
        {
            Tick(400);
            Login();
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
        // the last 0x05C update's first n params
        string Upd(int n)
        {
            var up = Of(0x05C);
            if (up.Count == 0) return "none";
            return string.Join(",", Enumerable.Range(0, n).Select(i => BitConverter.ToInt32(up[^1].Buffer, 4 + S2cPendingnum.NumOffset(i))));
        }
        string Cur() => c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "none";
        int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
        // keyitems_t: per table of 512, the keyList bits then the seenList bits
        string DbKeyItem(int ki)
        {
            var k = DbBlob(db, "keyitems");
            int off = ki / 512 * 128 + ki % 512 / 8, bit = 1 << ki % 8;
            bool has = off < k.Length && (k[off] & bit) != 0, seen = off + 64 < k.Length && (k[off + 64] & bit) != 0;
            return $"{(has ? 1 : 0)}{(seen ? 1 : 0)}";
        }
        string Var(string name) => DbRow(db, "value", $"FROM char_vars WHERE charid = ? AND varname = '{name}'");
        // the 0x02A TALKNUMWORK messages as message:param0[:param1..3], the 0x027 TALKNUMWORK2 ones the same
        string SpecialOf(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x02A)
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        string Special() => SpecialOf(s.PacketList);
        string NamesOf(IEnumerable<BasicPacket> from) => string.Join(",", from.Where(p => Opcode(p) == 0x027)
            .Select(p => $"{BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF}:{BitConverter.ToInt32(p.Buffer, 16)}"));
        string Names() => NamesOf(s.PacketList);
        int Text(ushort zoneId, string name) => int.Parse(Lua($"report(zones[{zoneId}].text.{name})"));
        string Has(string effect) => Lua($"report(tostring(player:hasStatusEffect(xi.effect.{effect})))");
        // the Visitant effect: its icon and its seconds left (the effect's getTimeRemaining)
        string Visitant() => Lua("local e = player:getStatusEffect(xi.effect.VISITANT) if e then report(e:getIcon()) report(math.floor(e:getTimeRemaining() / 1000)) else report('none') end");
        string Lights() => Lua("report(table.concat(xi.abyssea.getLightsTable(player), ' '))");
        int LightOf(int type) => int.Parse(Lights().Split(' ')[type - 1]);
        string Cruor() => Lua("report(player:getCurrency('cruor'))");
        int visitantIcon = 0;

        // ---- Visitant status ----
        Console.WriteLine("Visitant: the Cavernous Maw into Abyssea - Konschtat");
        Login();
        Check("in Konschtat Highlands", $"{Konschtat}", c.Zone);
        visitantIcon = int.Parse(Lua("report(xi.effect.VISITANT)"));
        Near(MawKonschtat);
        Talk(MawKonschtat);
        Check("the Cavernous Maw (level 75): entranceMawOnTrigger's event 107 (0, 1)", $"034:{MawKonschtat}:107:0,1/107", $"{Ev()}/{Cur()}");
        EventEnd(107, 1);
        Rezone();
        Check("event 107 finished (option 1): Abyssea - Konschtat by the Searing Ward", $"{AbysseaKonschtat}/153/-840", $"{c.Zone}/{c.X}/{c.Z}");
        Check("  no Visitant status before afterZoneIn", "false", Has("VISITANT"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: the Atma Fabricant without it, afterZoneIn's hidden five minutes");
        uint fabricant = NpcId("Atma_Fabricant");
        Near(fabricant);
        Talk(fabricant);
        Check("the Atma Fabricant with no Visitant status: NO_VISITANT_STATUS, no event", $"{Text(AbysseaKonschtat, "NO_VISITANT_STATUS")}:0/none",
              $"{Special()}/{Cur()}");
        // back in the Searing Ward (the zone's trigger area 1) before afterZoneIn
        MoveTo(153.0f, -72.0f, -840.0f);
        s.PacketList.Clear();
        Tick(4500);
        Check("afterZoneIn (4 s on): Visitant with icon 0 (hidden), 304 s", "0/True",
              $"{Visitant().Split(',')[0]}/{int.Parse(Visitant().Split(',')[1]) is >= 299 and <= 304}");
        Check("  ABYSSEA_TIME_OFFSET + 5 (messageName, 5 minutes)", $"True", Names().Contains($"{Text(AbysseaKonschtat, "ABYSSEA_TIME_OFFSET") + 5}:5"));
        Check("  the effect's flags: OFFLINE_TICK, NO_CANCEL, ON_ZONE, HIDE_TIMER", "True",
              Lua("local e = player:getStatusEffect(xi.effect.VISITANT) report(tostring(e:hasEffectFlag(xi.effectFlag.OFFLINE_TICK) and e:hasEffectFlag(xi.effectFlag.NO_CANCEL) " +
                  "and e:hasEffectFlag(xi.effectFlag.ON_ZONE) and e:hasEffectFlag(xi.effectFlag.HIDE_TIMER)))") == "true" ? "True" : "False");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: the Searing Ward's tether without real Visitant status");
        s.PacketList.Clear();
        // the Searing Ward's cuboid ends at z -773
        MoveTo(153.0f, -72.0f, -700.0f);
        // the zone's trigger areas, checked on its tick
        Tick(400);
        Check("out of the Searing Ward with hidden time: NO_VISITANT_WARD (messageName, 10), tetherTimer 11",
              $"True/11", $"{Names().Contains($"{Text(AbysseaKonschtat, "NO_VISITANT_WARD")}:10")}/{c.LocalVars.GetValueOrDefault("tetherTimer")}");
        var tether = new List<BasicPacket>();
        string returning = $"{Text(AbysseaKonschtat, "RETURNING_TO_SEARING_IN")}";
        for (int i = 0; i < 100 && c.LocalVars.GetValueOrDefault("tetherTimer") != 0; i++)
        {
            s.PacketList.Clear();
            Tick(400);
            tether.AddRange(s.PacketList);
        }
        // the first effect tick also gives the five-minute warning
        tether.RemoveAll(p => Opcode(p) == 0x02A && (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) == Text(AbysseaKonschtat, "ABYSSEA_TIME_OFFSET") + 4);
        Check("the effect's tick: searingWardTimer's countdown every 1.5 s (RETURNING_TO_SEARING_IN 5..1), RETURNING_TO_WARD",
              $"{returning}:5,{returning}:4,{returning}:3,{returning}:2,{returning}:1,{Text(AbysseaKonschtat, "RETURNING_TO_WARD")}:0",
              SpecialOf(tether));
        Check("  setPos back to the Searing Ward's tether point, tetherTimer 0", "114/-808/0",
              $"{MathF.Round(c.X)}/{MathF.Round(c.Z)}/{c.LocalVars.GetValueOrDefault("tetherTimer")}");
        // the 0x05B sent (CCharEntity::pendingPositionUpdate holds the client's moves until it is)
        s.PacketList.Clear();
        Near(fabricant);
        Talk(fabricant);
        Check("the Atma Fabricant (in the Searing Ward) with hidden Visitant status: event 2182", $"032:{fabricant}:2182/2182", $"{Ev()}/{Cur()}");
        EventEnd(2182);
        MoveTo(153.0f, -72.0f, -840.0f);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: the hidden time runs out (the countdown, event 2180, out by the maw)");
        Lua("xi.abyssea.addPlayerLights(player, xi.abyssea.lightType.PEARL, 5)");
        Check("  some pearlescent light", "5 0 0 0 0 0 0", Lights());
        var countdown = new List<BasicPacket>();
        // the map's 400 ms ticks (the Lua timers run on the first tick past their deadline)
        for (int i = 0; i < 1000 && Cur() != "2180"; i++)
        {
            s.PacketList.Clear();
            Tick(400);
            countdown.AddRange(s.PacketList);
        }
        int offset = Text(AbysseaKonschtat, "ABYSSEA_TIME_OFFSET");
        Check("reportTimeRemaining: 4..1 minutes (OFFSET + 4), 30, 10, 5..2 s (+ 7), 1 s (+ 6), then the end (+ 8)",
              $"{offset + 4}:4,{offset + 4}:3,{offset + 4}:2,{offset + 4}:1,{offset + 7}:30,{offset + 7}:10,{offset + 7}:5,{offset + 7}:4," +
              $"{offset + 7}:3,{offset + 7}:2,{offset + 6}:1,{offset + 8}:0", SpecialOf(countdown));
        Check("  the effect gone (delStatusEffectSilent): onEffectLose's event 2180, the lights reset", "false/2180/0 0 0 0 0 0 0",
              $"{Has("VISITANT")}/{Cur()}/{Lights()}");
        Check("  no time stored for the hidden time", "-", Var("abysseaTimeStored"));
        EventEnd(2180);
        Rezone();
        Check("event 2180 finished: xi.abyssea.onEventFinish's setPos out to Konschtat Highlands", $"{Konschtat}/88/-580",
              $"{c.Zone}/{MathF.Round(c.X)}/{MathF.Round(c.Z)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: the Conflux Surveyor's traverser stones");
        // into Abyssea again by the maw, a GM's !addkeyitem for three traverser stones and 20000 cruor
        void IntoKonschtat()
        {
            Near(MawKonschtat);
            Talk(MawKonschtat);
            EventEnd(107, 1);
            Rezone();
            MoveTo(153.0f, -72.0f, -840.0f);
            Tick(4500);
            s.PacketList.Clear();
        }
        IntoKonschtat();
        Check("back in Abyssea - Konschtat with hidden time", $"{AbysseaKonschtat}/0", $"{c.Zone}/{Visitant().Split(',')[0]}");
        Lua("for k = xi.keyItem.TRAVERSER_STONE1, xi.keyItem.TRAVERSER_STONE1 + 2 do player:addKeyItem(k) end player:addCurrency('cruor', 20000)");
        string HeldStones() => Lua("report(xi.abyssea.getHeldTraverserStones(player))");
        uint surveyor = NpcId("Conflux_Surveyor");
        Near(surveyor);
        Talk(surveyor);
        Check("the Conflux Surveyor with hidden time: event 2001 (0, 0, the stored time 0, three stones, no Sojourn abyssite, no Rhapsody)",
              $"034:{surveyor}:2001:0,0,0,3/2001", $"{Ev()}/{Cur()}");
        // option 2, the stones in the upper bits
        EventEnd(2001, 2u | 1u << 16);
        string[] vis = Visitant().Split(',');
        int lightsMsg1 = Text(AbysseaKonschtat, "LIGHTS_MESSAGE_1"), lightsMsg2 = Text(AbysseaKonschtat, "LIGHTS_MESSAGE_2");
        Check("event 2001 finished (a stone): Visitant status shown (setIcon), 30 minutes and the 4 s buffer, two stones left",
              $"{visitantIcon}/1804/2", $"{vis[0]}/{vis[1]}/{HeldStones()}");
        Check("  displayAbysseaLights: LIGHTS_MESSAGE_1 and _2 (messageName)", "True/True",
              $"{Names().Contains($"{lightsMsg1}:0")}/{Names().Contains($"{lightsMsg2}:0")}");
        Tick(3000);
        Talk(surveyor);
        Check("the surveyor with Visitant status: event 2001 (3, the time left less the buffer, 0, two stones)",
              $"034:{surveyor}:2001:3,1797,0,2", Ev());
        EventEnd(2001, 0);
        Check("  nothing chosen (option 0): the time and the stones kept", "1801/2", $"{Visitant().Split(',')[1]}/{HeldStones()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: a Sojourn abyssite from the Cruor Prospector, the Rhapsody in Mauve, the two-hour cap");
        uint prospector = NpcId("Cruor_Prospector");
        Near(prospector);
        string cr = Cruor();
        Talk(prospector);
        Check("the Cruor Prospector: event 2002 (the cruor)", $"034:{prospector}:2002:{cr}/2002", $"{Ev()}/{Cur()}");
        int sojourn = Ki("IVORY_ABYSSITE_OF_SOJOURN");
        // the key item menu (3), selection 2
        EventEnd(2002, 3u | 2u << 16);
        Check("event 2002 finished (key items, selection 2): the Ivory Abyssite of Sojourn for 6000 cruor", $"10/{int.Parse(cr) - 6000}",
              $"{DbKeyItem(sojourn)}/{Cruor()}");
        Near(surveyor);
        Talk(surveyor);
        Check("the surveyor: event 2001 with one Sojourn abyssite", $"034:{surveyor}:2001:3,1797,0,2,1", Ev());
        EventEnd(2001, 2u | 1u << 16);
        Check("event 2001 finished (a stone): 30 minutes and 3 more (180 s a stone per Sojourn abyssite), one stone left",
              $"{1797 + 1800 + 180 + 4}/1", $"{Visitant().Split(',')[1]}/{HeldStones()}");
        Lua("player:addKeyItem(xi.keyItem.RHAPSODY_IN_MAUVE)");
        Talk(surveyor);
        // surveyorOnTrigger passes hasKeyItem's boolean, which StartEventHelper (numbers only) leaves at 0
        Check("with the Rhapsody in Mauve: event 2001 (.., 1 Sojourn, the Rhapsody's boolean as 0)", $"034:{surveyor}:2001:3,{1797 + 1800 + 180},0,1,1", Ev());
        EventEnd(2001, 2u | 1u << 16);
        Check("event 2001 finished (a stone, an hour each with the Rhapsody): capped at two hours, no stones left", "7200/0",
              $"{Visitant().Split(',')[1]}/{HeldStones()}");
        Lua("player:addKeyItem(xi.keyItem.TRAVERSER_STONE1)");
        Talk(surveyor);
        EventEnd(2001, 2u | 5u << 16);
        Check("five stones asked for with one held: one spent (the stones held), still the cap", "7200/0", $"{Visitant().Split(',')[1]}/{HeldStones()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: out by the maw (the time stored), back in (restored)");
        Lua("xi.abyssea.addPlayerLights(player, xi.abyssea.lightType.AZURE, 16)");
        Tick(10_000);
        var exitMaw = zone.ZoneInfoOf(AbysseaKonschtat).Npcs.First(n => n.Name == "Cavernous_Maw");
        Near(exitMaw.Id);
        Talk(exitMaw.Id);
        Check("the exit maw: event 200", $"032:{exitMaw.Id}:200/200", $"{Ev()}/{Cur()}");
        long leftMs = long.Parse(Lua("report(player:getStatusEffect(xi.effect.VISITANT):getTimeRemaining())"));
        EventEnd(200, 1);
        Rezone();
        Check("event 200 finished (option 1): Konschtat Highlands; Visitant (ON_ZONE) gone, no event 2180", $"{Konschtat}/false/none",
              $"{c.Zone}/{Has("VISITANT")}/{Cur()}");
        // the zone change 400 ms on, the seconds left less the buffer rounded to setCharVar's int32 (sol2's llround)
        Check("  onEffectLose (persist::flush, getZoneID the destination): abysseaTimeStored the time left less the buffer, the lights reset",
              $"{LuaState.LlRound((leftMs - 400) / 1000.0 - 4)}/0 0 0 0 0 0 0",
              $"{Var("abysseaTimeStored")}/{Lights()}");
        string stored = Var("abysseaTimeStored");
        IntoKonschtat();
        Near(surveyor);
        Talk(surveyor);
        Check("back in, the surveyor: event 2001 (0, 0, the stored time)", $"034:{surveyor}:2001:0,0,{stored},0,1", Ev());
        EventEnd(2001, 2u);
        Check("event 2001 finished (no stones): the stored time restored and shown, abysseaTimeStored cleared",
              $"{visitantIcon}/{int.Parse(stored) + 4}/-", $"{Visitant().Split(',')[0]}/{Visitant().Split(',')[1]}/{Var("abysseaTimeStored")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Visitant: a logout in Abyssea (OFFLINE_TICK), the time run out offline");
        // a logout, the lobby's login (char_stats.zoning 2: gameLogin) the earth clock later
        void Relog(long offlineMs)
        {
            zone.OnCharTimeout(s, false);
            s = null;
            Sql(db, "UPDATE char_stats SET zoning = 2 WHERE charid = ?", BoundValue.Of(CharId));
            now += offlineMs;
            Login();
        }
        long RemainingMs() => long.Parse(Lua("report(player:getStatusEffect(xi.effect.VISITANT):getTimeRemaining())"));
        long beforeLogout = RemainingMs();
        Relog(600_000);
        Check("logged back in 10 minutes on: in Abyssea - Konschtat, Visitant shown, the offline time gone from it, gameLogin cleared by onGameIn",
              $"{AbysseaKonschtat}/{visitantIcon}/{(beforeLogout - 600_000) / 1000}/0",
              $"{c.Zone}/{Visitant().Split(',')[0]}/{RemainingMs() / 1000}/{c.LocalVars.GetValueOrDefault("gameLogin")}");
        Relog(RemainingMs() + 60_000);
        Check("logged back in after the time ran out: LoadStatusEffects gives Visitant a second", $"{visitantIcon}/True",
              $"{Visitant().Split(',')[0]}/{RemainingMs() <= 1000}");
        s.PacketList.Clear();
        for (int i = 0; i < 10 && Cur() != "2180"; i++) Tick(400);
        Check("  it runs out in Abyssea: ABYSSEA_TIME_OFFSET + 8, event 2180", $"True/2180",
              $"{Special().Contains($"{offset + 8}:0")}/{Cur()}");
        EventEnd(2180);
        Rezone();
        Check("event 2180 finished: out to Konschtat Highlands", $"{Konschtat}", c.Zone);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Lights ----
        Console.WriteLine("Lights: Abyssea - Konschtat's mobs killed (drop_lights' listeners, DropLights)");
        IntoKonschtat();
        // an hour of Visitant status (two stones at the surveyor) for the kills
        Lua("player:addKeyItem(xi.keyItem.TRAVERSER_STONE1) player:addKeyItem(xi.keyItem.TRAVERSER_STONE2)");
        Near(surveyor);
        Talk(surveyor);
        EventEnd(2001, 2u | 2u << 16);
        Check("an hour of Visitant status", $"{visitantIcon}/True", $"{Visitant().Split(',')[0]}/{int.Parse(Visitant().Split(',')[1]) > 3000}");
        // the sword on (GP_CLI_COMMAND_EQUIP_SET)
        Send(0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotMain; b[6] = 0; });
        Check("the bronze sword on, Fast Blade and Burning Blade known", $"{BronzeSword}/True/True",
              $"{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{CharStats.HasWeaponSkill(c, FastBlade)}/{CharStats.HasWeaponSkill(c, BurningBlade)}");
        // lights.lua's Konschtat table for the mobs killed here: azure, pearl, ruby, amber
        var lightTable = new Dictionary<string, int[]>
        {
            ["Gunge_Slug"] = new[] { 8, 5, 16, 8 }, ["Razorback"] = new[] { 8, 5, 8, 8 }, ["Hoary_Ragwort"] = new[] { 8, 5, 8, 8 },
            ["Highland_Rafflesia"] = new[] { 8, 5, 16, 8 }, ["Licorice"] = new[] { 8, 5, 8, 16 }, ["Mesa_Wivre"] = new[] { 16, 5, 8, 8 },
            ["Morboling"] = new[] { 16, 5, 8, 8 }, ["Deep_Eye"] = new[] { 8, 5, 8, 8 }, ["Sods_Limule"] = new[] { 8, 5, 8, 8 },
            ["Viridis_Wyvern"] = new[] { 8, 5, 16, 16 }, ["Ypotryll"] = new[] { 8, 5, 16, 16 }, ["Qaitu"] = new[] { 16, 5, 16, 8 },
        };
        var konschtatInfo = zone.ZoneInfoOf(AbysseaKonschtat);
        int bodyEmits = Text(AbysseaKonschtat, "BODY_EMITS_OFFSET");
        // the char's swing claims the mob (the char kept facing it as the client does)
        void ClaimBySwing(MobEntity m)
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
        // the blows: the char's swings, a weapon skill, a spell; each on the mob brought to 1 HP
        void MeleeBlow(MobEntity m)
        {
            ClaimBySwing(m);
            m.Hp = 1;
            for (int i = 0; i < 30 && m.IsAlive; i++) Tick(400);
        }
        Action<MobEntity> WsBlow(ushort wsid) => m =>
        {
            ClaimBySwing(m);
            m.Hp = 1;
            c.Tp = 1000;
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Weaponskill).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, wsid);
            });
            for (int i = 0; i < 10 && m.IsAlive; i++) Tick(400);
        };
        void SpellBlow(MobEntity m)
        {
            MoveTo(m.X - 5.0f, m.Y, m.Z);
            // standing still there (m_lastMoveDistance 0: the cast is not begun moving)
            MoveTo(c.X, c.Y, c.Z);
            m.Hp = 1;
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.CastMagic).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSpellId(b, Stone);
            });
            for (int i = 0; i < 20 && m.IsAlive; i++) Tick(400);
        }
        // the table's mobs killed with the blow until the light rises: the rise the table's amount, the other lights
        // kept, BODY_EMITS_OFFSET + the light with the tier
        var lightPrey = konschtatInfo.Mobs.Where(m => lightTable.ContainsKey(m.Name) && !m.IsNotorious && m.IsSpawned && m.IsAlive).ToList();
        int nextPrey = 0;
        void KillForLight(string what, int light, int tableIndex, Action<MobEntity> blow, int tierMax)
        {
            Lua("player:setUnkillable(true)");
            int kills = 0, rise = 0;
            bool amountsRight = true, othersKept = true, msgRight = true;
            string name = "";
            for (; nextPrey < lightPrey.Count && rise == 0 && kills < 40; nextPrey++)
            {
                var m = lightPrey[nextPrey];
                if (!m.IsSpawned || !m.IsAlive) continue;
                string before = Lights();
                s.PacketList.Clear();
                var packets = new List<BasicPacket>();
                blow(m);
                Tick(1000);
                Tick(1000);
                packets.AddRange(s.PacketList);
                if (m.IsAlive) continue;
                kills++;
                var b = before.Split(' ').Select(int.Parse).ToArray();
                var a = Lights().Split(' ').Select(int.Parse).ToArray();
                rise = a[light - 1] - b[light - 1];
                // (a kill the swings landed before the blow drops pearl light instead: not the blow's)
                if (rise != 0)
                {
                    for (int t = 1; t <= 7; t++) if (t != light && a[t - 1] != b[t - 1]) othersKept = false;
                    int amount = lightTable[m.Name][tableIndex];
                    amountsRight = rise == amount;
                    name = m.Name;
                    int tier = Math.Min(amount <= 8 ? 0 : amount <= 16 ? 1 : amount <= 32 ? 2 : amount <= 64 ? 3 : 4, tierMax);
                    msgRight = SpecialOf(packets).Split(',').Contains($"{bodyEmits + light - 1}:{tier}");
                }
            }
            Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
            Check($"{what}: {kills} kills, {(rise > 0 ? $"the {name}'s light" : "no light")} (the table's amount, the other lights kept, BODY_EMITS_OFFSET + {light - 1} with the tier)",
                  "True/True/True/True", $"{rise > 0}/{amountsRight}/{othersKept}/{msgRight}");
        }
        Console.WriteLine("Lights: no cruor from the kills (charutils' cruor drop is a C++ TODO), the experience points");
        {
            Lua("player:setUnkillable(true)");
            string cruorBeforeKill = Cruor();
            int expBeforeKill = c.JobExp[c.MJob];
            MobEntity m = null;
            for (int i = 0; i < 5 && m?.IsDead != true; i++)
            {
                m = lightPrey.Skip(nextPrey).First(x => x.IsSpawned && x.IsAlive);
                nextPrey = lightPrey.IndexOf(m) + 1;
                s.PacketList.Clear();
                MeleeBlow(m);
                Tick(1000);
                Tick(1000);
            }
            Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
            Check($"a {m.Name} killed: experience points, the cruor unchanged, no CRUOR_TOTAL", $"True/True/{cruorBeforeKill}/False",
                  $"{m.IsDead}/{c.JobExp[c.MJob] > expBeforeKill}/{Cruor()}/{Special().Contains($"{Text(AbysseaKonschtat, "CRUOR_TOTAL")}:")}");
        }

        KillForLight("melee kills (deathType NONE: physical, pearlescent light at half the drop rate)", 1, 1, MeleeBlow, 2);
        KillForLight("Fast Blade kills (WEAPONSKILL_TAKE's WS_PHYSICAL: ruby light)", 6, 2, WsBlow(FastBlade), 4);
        KillForLight("Burning Blade kills (a magical weapon skill, WS_MAGICAL: amber light)", 7, 3, WsBlow(BurningBlade), 4);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Lights: a BLM's Stone kills (MAGIC_TAKE's MAGICAL: azure light)");
        Lua("player:changeJob(xi.job.BLM) player:setLevel(75) player:addSpell(xi.magic.spell.STONE) player:setMP(player:getMaxMP())");
        Check("the char a BLM 75 knowing Stone", "4/75/True", $"{c.MJob}/{c.MLvl}/{Lua("report(tostring(player:hasSpell(xi.magic.spell.STONE)))") == "true"}");
        KillForLight("Stone kills", 5, 0, SpellBlow, 4);
        Lua("player:changeJob(xi.job.WAR) player:setLevel(75)");
        Send(0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotMain; b[6] = 0; });
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Lights: the caps and the tier messages (addPlayerLights, as a red Pyxis gives them)");
        // xi.abyssea.lightType: PEARL 1, GOLDEN 2, SILVERY 3, EBON 4, AZURE 5, RUBY 6, AMBER 7; the caps and the highest tier
        var lightCaps = new[] { (230, 2), (200, 2), (200, 2), (200, 2), (255, 4), (255, 4), (255, 4) };
        for (int light = 1; light <= 7; light++)
        {
            s.PacketList.Clear();
            Lua($"xi.abyssea.addPlayerLights(player, {light}, 9)");
            string nine = $"{LightOf(light)}/{Special()}";
            s.PacketList.Clear();
            Lua($"xi.abyssea.addPlayerLights(player, {light}, 255)");
            Check($"light {light}: 9 more (tier 1), then 255 more: capped at {lightCaps[light - 1].Item1}, the tier message at most {lightCaps[light - 1].Item2}",
                  $"True/{lightCaps[light - 1].Item1}/{bodyEmits + light - 1}:{lightCaps[light - 1].Item2}",
                  $"{nine.EndsWith($"/{bodyEmits + light - 1}:1")}/{LightOf(light)}/{Special()}");
        }
        // abysseaLights1: pearl, golden, silvery, ebon a byte each (the ebon byte making it negative as an int32);
        // abysseaLights2: azure, ruby, amber (setLightsFromTable's shifts of 32, 40, 48 wrap to 0, 8, 16 in LuaJIT)
        Check("the char vars: abysseaLights1 and 2", $"{unchecked((int)(230u | 200u << 8 | 200u << 16 | 200u << 24))}/{255 | 255 << 8 | 255 << 16}",
              $"{Var("abysseaLights1")}/{Var("abysseaLights2")}");
        Near(surveyor);
        Talk(surveyor);
        EventEnd(2001, 0);
        Check("the surveyor's event, nothing chosen: no lights shown (displayAbysseaLights only after time added)", "False", Names().Contains($"{lightsMsg1}:"));
        Lua("player:addKeyItem(xi.keyItem.TRAVERSER_STONE1)");
        Talk(surveyor);
        EventEnd(2001, 2u | 1u << 16);
        var lm = Of(0x027).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF}:{BitConverter.ToInt32(p.Buffer, 16)}:{BitConverter.ToInt32(p.Buffer, 20)}:" +
                                       $"{BitConverter.ToInt32(p.Buffer, 24)}:{BitConverter.ToInt32(p.Buffer, 28)}").ToList();
        Check("a stone added: LIGHTS_MESSAGE_1 (pearl, ebon, golden, silvery) and _2 (azure, ruby, amber)",
              $"{lightsMsg1}:230:200:200:200,{lightsMsg2}:255:255:255:0", string.Join(",", lm.Where(x => x.StartsWith($"{lightsMsg1}:") || x.StartsWith($"{lightsMsg2}:"))));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Sturdy Pyxis ----
        Console.WriteLine("Sturdy Pyxis: the chests out of kills (spawn_pyxis' DEATH listener, SetPyxisData)");
        var pyxides = konschtatInfo.Npcs.Where(n => n.Name == "Sturdy_Pyxis").ToList();
        // the zone's npcs.yaml has a few out from the start (status normal, no chest data)
        var chestsSeen = pyxides.Where(n => n.Status == NpcEntity.StatusNormal).Select(n => n.Id).ToHashSet();
        int NV(uint id, string v) => int.Parse(Lua($"report(GetNPCByID({id}):getLocalVar('{v}'))"));
        void SetLights(int pearl, int azure, int ruby, int amber) =>
            Lua($"xi.abyssea.resetPlayerLights(player) xi.abyssea.addPlayerLights(player, 1, {pearl}) xi.abyssea.addPlayerLights(player, 5, {azure}) " +
                $"xi.abyssea.addPlayerLights(player, 6, {ruby}) xi.abyssea.addPlayerLights(player, 7, {amber})");
        // the 0x02A / 0x027 messages with their four numbers
        string SpecialFull() => string.Join(",", Of(0x02A).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}:" +
                                                                       $"{BitConverter.ToUInt32(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 16)}:{BitConverter.ToUInt32(p.Buffer, 20)}"));
        string NamesFull() => string.Join(",", Of(0x027).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF}:{BitConverter.ToInt32(p.Buffer, 16)}:" +
                                                                     $"{BitConverter.ToInt32(p.Buffer, 20)}:{BitConverter.ToInt32(p.Buffer, 24)}:{BitConverter.ToInt32(p.Buffer, 28)}"));
        // an 0x034 as Ev() prints it
        string EvStr(uint id, int ev, params long[] ps)
        {
            var l = ps.Select(x => ((uint)x).ToString()).ToList();
            while (l.Count > 0 && l[^1] == "0") l.RemoveAt(l.Count - 1);
            return $"034:{id}:{ev}" + (l.Count > 0 ? ":" + string.Join(",", l) : "");
        }
        int TempCount() => c.Items.Count(i => i.Location == Container.TempItems && i.ItemId != 0);
        // the light table's mobs killed (melee: only pearlescent light) until a chest comes out of one
        List<BasicPacket> chestKill = null;
        MobEntity chestMob = null;
        NpcEntity SpawnChest()
        {
            Lua("player:setUnkillable(true)");
            NpcEntity found = null;
            for (int kills = 0; kills < 40 && found == null; kills++)
            {
                if (nextPrey >= lightPrey.Count)
                {
                    lightPrey = konschtatInfo.Mobs.Where(m => lightTable.ContainsKey(m.Name) && !m.IsNotorious && m.IsSpawned && m.IsAlive).ToList();
                    nextPrey = 0;
                }
                var m = lightPrey[nextPrey++];
                if (!m.IsSpawned || !m.IsAlive) continue;
                s.PacketList.Clear();
                chestMob = m;
                MeleeBlow(m);
                chestKill = new List<BasicPacket>(s.PacketList);
                found = pyxides.FirstOrDefault(n => n.Status == NpcEntity.StatusNormal && !chestsSeen.Contains(n.Id));
            }
            // (kept unkillable through the chests: the zone's mobs wander by; the Atma section ends it)
            Lua("player:setHP(player:getMaxHP())");
            if (found != null) chestsSeen.Add(found.Id);
            s.PacketList.Clear();
            return found;
        }
        // a chest of the type: the lights set for it, chests spawned until one is (the others left to their timers)
        NpcEntity ChestOfType(int type, int pearl, int azure, int ruby, int amber, int tries = 12)
        {
            // the temporary items cleared first (GiveTempItem's ALLREADY_POSSESS_TEMP_ITEM is in no zone's IDs: a nil message id)
            Lua("player:delContainerItems(xi.inv.TEMPITEMS)");
            // the experience points kept off the level 75 cap for an EXP chest
            if (c.JobExp[c.MJob] > 30000) Lua("player:delExp(15000)");
            for (int i = 0; i < tries; i++)
            {
                SetLights(pearl, azure, ruby, amber);
                var ch = SpawnChest();
                if (ch != null && NV(ch.Id, "CHESTTYPE") == type) return ch;
                // a chest of another type taken away (left out, the zone's pyxides run out: GetPyxisID's 0)
                if (ch != null)
                {
                    Lua($"xi.pyxis.removeChest(player, GetNPCByID({ch.Id}), 0, 1)");
                    Tick(1000);
                    Tick(1000);
                }
            }
            return null;
        }
        string ChestState(NpcEntity ch) => $"{ch.Status}/{NV(ch.Id, "SPAWNSTATUS")}/{Lua($"report(GetNPCByID({ch.Id}):getAnimationSub())")}";
        // the chest's 'disappear in' timer (removeChest's npc:timer): DISAPPEAR within the seconds
        bool Disappears(NpcEntity ch, int seconds)
        {
            for (int i = 0; i < seconds * 5 / 2 + 2 && ch.Status == NpcEntity.StatusNormal; i++) Tick(400);
            return ch.Status != NpcEntity.StatusNormal && NV(ch.Id, "SPAWNSTATUS") == 0;
        }
        string ContentMessage(NpcEntity ch) => Lua($"local n = GetNPCByID({ch.Id}) report(xi.pyxis.npc.contentMessage[n:getLocalVar('MESSAGE')] + n:getLocalVar('CHESTTYPE'))");
        // GetPyxisID: timeElapsedCheck holds a chest used once its SPAWNTIME is past, so once the zone's pyxides have all
        // been used the next kill finds none, sets them all usable again and looks up chest 0 (luautils::GetNPCByID's warning)
        expectedWarnings.Add("lua: GetNPCByID NPC doesn't exist (0)");
        int forbiddenKey = int.Parse(Lua("report(xi.item.FORBIDDEN_KEY)"));
        // the experience points away from the level 75 cap (the light kills filled them) for the EXP chests
        Lua("player:delExp(20000)");
        Lua("player:addCurrency('cruor', 50000)");

        // the contents an opened chest gives at once (openChest), against what the char had before
        string Before() => $"{Cruor()}|{c.JobExp[c.MJob]}|{RemainingMs()}|{Lights()}|{TempCount()}|{c.Hp}|{c.Tp}";
        string ExpectOpened(NpcEntity ch, string before, out bool removes)
        {
            var b = before.Split('|');
            int tier = NV(ch.Id, "TIER");
            removes = true;
            switch (NV(ch.Id, "DROPTYPE"))
            {
                case 6:
                {
                    int light = NV(ch.Id, "LIGHT"), value = NV(ch.Id, "LIGHT_VALUE");
                    var l = b[3].Split(' ').Select(int.Parse).ToArray();
                    l[light - 1] = Math.Min(l[light - 1] + value, lightCaps[light - 1].Item1);
                    return $"light {light} +{value}: {string.Join(" ", l)}";
                }
                case 7: return $"restore {NV(ch.Id, "RESTORE")}";
                case 8: return $"cruor {int.Parse(b[0]) + 250 * tier}";
                case 9: return "time +600 s";
                case 10: return $"exp {int.Parse(b[1]) + 250 * tier}";
                case 11: return "several temporary items";
                default:
                    removes = false;
                    return $"open, drop type {NV(ch.Id, "DROPTYPE")}";
            }
        }
        string ActualOpened(NpcEntity ch, string before)
        {
            var b = before.Split('|');
            switch (NV(ch.Id, "DROPTYPE"))
            {
                case 6: return $"light {NV(ch.Id, "LIGHT")} +{NV(ch.Id, "LIGHT_VALUE")}: {Lights()}";
                case 7:
                {
                    int r = NV(ch.Id, "RESTORE");
                    bool ok = r switch
                    {
                        1 => c.Hp == c.MaxHp,
                        2 => true,
                        3 => c.Tp == int.Parse(b[6]) + 1000,
                        _ => c.Hp == c.MaxHp && c.Tp == Math.Min(3000, int.Parse(b[6]) + 3000),
                    };
                    return ok ? $"restore {r}" : $"restore {r} not given (HP {c.Hp}/{c.MaxHp}, TP {c.Tp})";
                }
                case 8: return $"cruor {Cruor()}";
                case 9:
                {
                    long gained = RemainingMs() - long.Parse(b[2]);
                    return gained is > 590_000 and <= 600_000 && Visitant().Split(',')[0] == $"{visitantIcon}" ? "time +600 s" : $"time +{gained} ms";
                }
                case 10: return $"exp {c.JobExp[c.MJob]}";
                case 11: return TempCount() > int.Parse(b[4]) && Special().Contains($"{Text(AbysseaKonschtat, "OBTAINS_SEVERAL_TEMPS")}:") ? "several temporary items" : "no temporary items";
                default: return $"open, drop type {NV(ch.Id, "DROPTYPE")}";
            }
        }

        Console.WriteLine("Sturdy Pyxis: a blue chest (no lights but pearl: always blue) and its dial");
        Lua("player:setHP(player:getMaxHP() - 200) player:setTP(0)");
        var blue = ChestOfType(1, 230, 0, 0, 0, 3);
        Check("a chest out of a kill: blue (CHESTTYPE 1), the char's, at the mob, model 965, locked (animation sub 12), MONSTER_CONCEALED_CHEST",
              $"True/1/{c.Id}/{MathF.Round(chestMob.X)}/{MathF.Round(chestMob.Z)}/965/12/True",
              blue == null ? "no chest" : $"True/{NV(blue.Id, "CHESTTYPE")}/{NV(blue.Id, "PLAYERID")}/{MathF.Round(blue.X)}/{MathF.Round(blue.Z)}/{blue.Look[1]}/" +
                                          $"{Lua($"report(GetNPCByID({blue.Id}):getAnimationSub())")}/{NamesOf(chestKill).Contains($"{Text(AbysseaKonschtat, "MONSTER_CONCEALED_CHEST")}:")}");
        if (blue != null)
        {
            int tier = NV(blue.Id, "TIER");
            int locked = 2003 + tier;
            Near(blue.Id);
            Talk(blue.Id);
            Check($"the blue chest (tier {tier}): its locked event {locked} (the content, the number, the attempts, the guesses wanted, the failures, 5, 3, the time)",
                  EvStr(blue.Id, locked, long.Parse(ContentMessage(blue)), NV(blue.Id, "RAND_NUM"), 0, NV(blue.Id, "REQUIRED"), 0, 5, 3,
                        (EarthNow() - NV(blue.Id, "[pyxis]SPAWNTIME")) * 60) + $"/{locked}",
                  $"{Ev()}/{Cur()}");
            EventEnd(locked);
            // higher (option 1) under 55, lower (2) from it, until the lock opens or five guesses fail
            int guessMsg = Text(AbysseaKonschtat, "RANDOM_SUCCESS_FAIL_GUESS"), openedMsg = Text(AbysseaKonschtat, "PLAYER_OPENED_LOCK"),
                failedMsg = Text(AbysseaKonschtat, "PLAYER_FAILED_LOCK");
            bool stepsRight = true, opened = false, failed = false;
            int guesses = 0;
            string before = Before(), expected = "", actual = "";
            while (!opened && !failed && guesses < 12)
            {
                int r0 = NV(blue.Id, "RAND_NUM"), right0 = NV(blue.Id, "CORRECT_GUESSES"), wrong0 = NV(blue.Id, "FAILED_ATTEMPTS"), need = NV(blue.Id, "REQUIRED");
                uint guess = r0 < 55 ? 1u : 2u;
                Lua("player:setHP(player:getMaxHP() - 200) player:setTP(0)");
                before = Before();
                Talk(blue.Id);
                EventEnd(locked, guess);
                guesses++;
                int r1 = NV(blue.Id, "RAND_NUM");
                bool right = guess == 1 ? r1 > r0 : r1 < r0;
                string names = NamesFull();
                stepsRight &= names.Contains($"{guessMsg}:{r1}:{(right ? 0 : 1)}:0:0") &&
                              NV(blue.Id, "CORRECT_GUESSES") == right0 + (right ? 1 : 0) && NV(blue.Id, "FAILED_ATTEMPTS") == wrong0 + (right ? 0 : 1);
                if (right && right0 + 1 >= need)
                {
                    opened = true;
                    stepsRight &= names.Contains($"{openedMsg}:");
                    expected = ExpectOpened(blue, before, out _);
                    actual = ActualOpened(blue, before);
                }
                else if (!right && wrong0 + 1 >= 5)
                {
                    failed = true;
                    stepsRight &= names.Contains($"{failedMsg}:") && Special().Contains($"{Text(AbysseaKonschtat, "CHEST_DISAPPEARED")}:");
                }
            }
            Check($"the dial, {guesses} guesses ({(opened ? "opened" : failed ? "five failures" : "unfinished")}): each RANDOM_SUCCESS_FAIL_GUESS with the new number, " +
                  "the right / wrong counts, PLAYER_OPENED_LOCK or PLAYER_FAILED_LOCK and CHEST_DISAPPEARED", "True/True", $"{stepsRight}/{opened || failed}");
            if (opened)
            {
                Check($"  opened (drop type {NV(blue.Id, "DROPTYPE")}): openChest's contents", expected, actual);
                if (NV(blue.Id, "DROPTYPE") == 1)
                {
                    // the temporary items wait in the chest
                    int unlocked = locked + 64;
                    int n = NV(blue.Id, "NB_ITEM");
                    var temps = Enumerable.Range(1, n).Select(i => NV(blue.Id, $"TEMP{i}")).ToList();
                    Talk(blue.Id);
                    Check("  the open chest: its unlocked event (1, 1, 1, the temporary items 1, 1, the time, 1, 1)",
                          EvStr(blue.Id, unlocked, 1, 1, 1, 1, 1, (EarthNow() - NV(blue.Id, "[pyxis]SPAWNTIME")) * 60, 1, 1), Ev());
                    EventEnd(unlocked, 0, 1);
                    Check("  the update: the temporary items", string.Join(",", temps), Upd(n));
                    int t0 = TempCount();
                    EventEnd(unlocked, 1u | 1u << 16);
                    Check("  the first taken: the item in the temporary items, OBTAINS_TEMP_ITEM, TEMP1 0", $"True/True/0",
                          $"{c.Items.Any(i => i.Location == Container.TempItems && i.ItemId == temps[0])}/" +
                          $"{NamesFull().Contains($"{Text(AbysseaKonschtat, "OBTAINS_TEMP_ITEM")}:{temps[0]}:")}/{NV(blue.Id, "TEMP1")}");
                    if (blue.Status == NpcEntity.StatusNormal && NV(blue.Id, "SPAWNSTATUS") == 1)
                    {
                        string cruorLeft = Cruor();
                        Talk(blue.Id);
                        EventEnd(unlocked, 2);
                        Check("  the rest left (option 2: openchoice 2): removeChest's TIER x 10 cruor, CRUOR_OBTAINED", $"{int.Parse(cruorLeft) + tier * 10}/True",
                              $"{Cruor()}/{Special().Contains($"{Text(AbysseaKonschtat, "CRUOR_OBTAINED")}:{tier * 10}")}");
                    }
                }
                Check("  the chest gone (removeChest's timer)", "True", Disappears(blue, 5));
            }
            else if (failed) Check("  the chest gone a second on (removeChest)", "True", Disappears(blue, 2));
        }
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Sturdy Pyxis: Forbidden Keys for the chests (the Cruor Prospector's, 40 at once)");
        Near(prospector);
        Talk(prospector);
        string prospectorEv = Ev();
        string crKeys = Cruor();
        EventEnd(2002, 1u | 16u << 16 | 40u << 24);
        Check("event 2002 finished (items, selection 16, 40 of them): 40 Forbidden Keys for 20000 cruor", $"40/{int.Parse(crKeys) - 20000}",
              $"{c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == forbiddenKey).Sum(i => (long)i.Quantity)}/{Cruor()}" +
              (Cruor() == crKeys ? $" (event {prospectorEv}, {Special()}, status {c.Status}, at {c.X},{c.Z})" : ""));
        int keyOpenMsg = Text(AbysseaKonschtat, "TRADE_KEY_OPEN");
        // a Forbidden Key traded to the chest: TRADE_KEY_OPEN (messageName, the key's id), the contents (openChest)
        // more keys from the Prospector when they run out (before the cruor is noted for a chest's contents)
        void EnsureKey()
        {
            if (c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == forbiddenKey)) return;
            Near(prospector);
            Talk(prospector);
            EventEnd(2002, 1u | 16u << 16 | 40u << 24);
        }
        bool OpenWithKey(NpcEntity ch)
        {
            EnsureKey();
            Near(ch.Id);
            var key = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == forbiddenKey);
            Trade(ch.Id, (key.Slot, 1));
            return NamesFull().Contains($"{keyOpenMsg}:{forbiddenKey}:");
        }
        // the item drops (temporary items, items, pop items, augmented items, a key item) taken from an open chest:
        // the unlocked event, the update's list, the first one taken, the rest left (option 2)
        string TakeFirst(NpcEntity ch)
        {
            int tier = NV(ch.Id, "TIER"), dropType = NV(ch.Id, "DROPTYPE"), unlocked = 2003 + tier + 64, n = NV(ch.Id, "NB_ITEM");
            int dropTypeEvent = dropType switch { 1 => 1, 2 => 2, 3 => 2, 4 => 3, _ => 4 };
            // the key item drawn from the zone's eight may be one an earlier chest gave: giveKeyItem then says
            // ALREADY_POSSESS_KEY_ITEM and keeps it, so the char is made not to hold it and the take is checked as a gain
            if (dropType >= 5 && NV(ch.Id, "KI") is var heldKi && heldKi != 0) Lua($"player:delKeyItem({heldKi})");
            Near(ch.Id);
            Talk(ch.Id);
            var problems = new List<string>();
            if (Ev() != EvStr(ch.Id, unlocked, 1, 1, 1, dropTypeEvent, 1, (EarthNow() - NV(ch.Id, "[pyxis]SPAWNTIME")) * 60, 1, 1)) problems.Add($"event {Ev()}");
            EventEnd(unlocked, 0, 1);
            string prefix = dropType switch { 1 => "TEMP", 2 => "ITEM", 3 => "POPITEM", _ => "" };
            uint first;
            if (dropType <= 3)
            {
                var list = Enumerable.Range(1, n).Select(i => NV(ch.Id, $"{prefix}{i}")).ToList();
                if (Upd(n) != string.Join(",", list)) problems.Add($"update {Upd(n)} not {string.Join(",", list)}");
                first = (uint)list[0];
            }
            else if (dropType == 4)
            {
                string aug = $"{NV(ch.Id, "ITEM1ID")},{0x0202},{NV(ch.Id, "ITEM1AU1")},{NV(ch.Id, "ITEM1AU2")},{NV(ch.Id, "ITEM2ID")},{0x0202},{NV(ch.Id, "ITEM2AU1")},{NV(ch.Id, "ITEM2AU2")}";
                if (Upd(8) != aug) problems.Add($"update {Upd(8)} not {aug}");
                first = (uint)NV(ch.Id, "ITEM1ID");
            }
            else
            {
                if (Upd(1) != $"{NV(ch.Id, "KI")}") problems.Add($"update {Upd(1)}");
                first = (uint)NV(ch.Id, "KI");
            }
            EventEnd(unlocked, 1u | 1u << 16);
            switch (dropType)
            {
                case 1:
                    if (!c.Items.Any(i => i.Location == Container.TempItems && i.ItemId == first)) problems.Add("no temporary item");
                    if (!NamesFull().Contains($"{Text(AbysseaKonschtat, "OBTAINS_TEMP_ITEM")}:{first}:")) problems.Add("no OBTAINS_TEMP_ITEM");
                    break;
                case 2:
                case 3:
                    if (!c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == first)) problems.Add($"no item {first}");
                    if (!NamesFull().Contains($"{Text(AbysseaKonschtat, "OBTAINS_ITEM")}:{first}:")) problems.Add("no OBTAINS_ITEM");
                    if (NV(ch.Id, $"{prefix}1") != 0) problems.Add($"{prefix}1 not cleared");
                    // the inventory kept from filling up over the chests
                    Lua($"player:delItem({first}, 1)");
                    break;
                case 4:
                {
                    var item = c.Items.FirstOrDefault(i => i.Location == Container.Inventory && i.ItemId == first);
                    if (item == null) problems.Add($"no augmented item {first}");
                    else if (Lua($"local it = player:findItem({first}) report(it and it:getAugment(0)[1] or -1)") != $"{NV(ch.Id, "ITEM1AUGMENT1")}")
                        problems.Add($"augment {Lua($"local it = player:findItem({first}) report(it and it:getAugment(0)[1] or -1)")} not {NV(ch.Id, "ITEM1AUGMENT1")}");
                    if (NV(ch.Id, "ITEM1ID") != 0) problems.Add("ITEM1ID not cleared");
                    Lua($"player:delItem({first}, 1)");
                    break;
                }
                default:
                    if (DbKeyItem((int)first) != "10" && Lua($"report(tostring(player:hasKeyItem({first})))") != "true") problems.Add($"no key item {first}");
                    if (!NamesFull().Contains($"{Text(AbysseaKonschtat, "OBTAINS_KEYITEM")}:{first}:")) problems.Add("no OBTAINS_KEYITEM");
                    break;
            }
            if (ch.Status == NpcEntity.StatusNormal && NV(ch.Id, "SPAWNSTATUS") == 1)
            {
                Talk(ch.Id);
                EventEnd(unlocked, 2);
            }
            if (!Disappears(ch, 5)) problems.Add("the chest stayed");
            return problems.Count == 0 ? $"drop type {dropType}: taken" : $"drop type {dropType}: {string.Join("; ", problems)}";
        }

        Console.WriteLine("Sturdy Pyxis: blue chests at full azure light (tier 5) opened with keys until one holds time");
        var blueTypes = new List<int>();
        var blueWrong = new List<string>();
        bool tiersFive = true;
        // a tier 5 blue chest holds time one time in six (spawn.lua: a pick of six drop types), so forty could all miss it
        // (1 in 1500): up to a hundred (1 in 10^8), and the spawns that bring no blue chest bounded too
        for (int chests = 0, spawns = 0; chests < 100 && spawns < 400 && !blueTypes.Contains(9); spawns++)
        {
            var ch = ChestOfType(1, 230, 255, 0, 0, 2);
            if (ch == null) continue;
            chests++;
            tiersFive &= NV(ch.Id, "TIER") == 5;
            Lua("player:setHP(player:getMaxHP() - 200) player:setTP(0)");
            EnsureKey();
            string before = Before();
            if (!OpenWithKey(ch)) blueWrong.Add("no TRADE_KEY_OPEN");
            int dropType = NV(ch.Id, "DROPTYPE");
            blueTypes.Add(dropType);
            string expect = ExpectOpened(ch, before, out bool removes), act = ActualOpened(ch, before);
            if (expect != act) blueWrong.Add($"{expect} / {act}");
            if (dropType == 9 && !Special().Contains($"{offset + 3}:10")) blueWrong.Add("no ABYSSEA_TIME_OFFSET + 3");
            if (removes) { if (!Disappears(ch, 5)) blueWrong.Add($"drop type {dropType}: the chest stayed"); }
            else
            {
                string taken = TakeFirst(ch);
                if (!taken.EndsWith("taken")) blueWrong.Add(taken);
            }
        }
        Check($"{blueTypes.Count} blue chests (drop types {string.Join(" ", blueTypes)}): all tier 5, each one's contents, the last one time (+10 minutes)",
              "True//True", $"{tiersFive}/{string.Join(" | ", blueWrong)}/{blueTypes.Contains(9)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Sturdy Pyxis: a red chest (ruby light) and its air pressure");
        var red = ChestOfType(2, 230, 0, 255, 0);
        Check("a red chest (CHESTTYPE 2) at full ruby light: tier 5, model 968, light drops (DROPTYPE 6)", "True/5/968/6",
              red == null ? "no chest" : $"True/{NV(red.Id, "TIER")}/{red.Look[1]}/{NV(red.Id, "DROPTYPE")}");
        if (red != null)
        {
            int locked = 2003 + NV(red.Id, "TIER");
            int pressureMsg = Text(AbysseaKonschtat, "AIR_PRESSURE_CHANGE");
            var pressureChoice = new Dictionary<int, int> { [2] = 10, [4] = 20, [8] = 30, [16] = 10, [32] = 20, [64] = 30 };
            bool stepsRight = true, opened = false, failed = false, firstEventRight = false;
            int attempts = 0;
            string before = "", expected = "", actual = "";
            Near(red.Id);
            while (!opened && !failed && attempts < 8)
            {
                Talk(red.Id);
                int target = NV(red.Id, "RAND_NUM"), pressure = NV(red.Id, "CURRENTPRESSURE"), wear = NV(red.Id, "LOCKWEARADD"), cur = NV(red.Id, "CURRENT_ATTEMPTS");
                // the wear message (1-4, less 1) is rolled for the event and kept only after a try
                if (attempts == 0)
                {
                    long wearMsg = Ev().Split(':').Length > 3 && Ev().Split(':')[3].Split(',').Length > 3 ? long.Parse(Ev().Split(':')[3].Split(',')[3]) : -1;
                    firstEventRight = pressure is >= 90 and <= 100 && wearMsg is >= 0 and <= 3 &&
                                      Ev() == EvStr(red.Id, locked, long.Parse(ContentMessage(red)), pressure, (target - 10) | (target + 10) << 16, wearMsg, 5, 0, 3,
                                                    (EarthNow() - NV(red.Id, "[pyxis]SPAWNTIME")) * 60);
                }
                // the choice landing nearest the target: options 1-3 reduce by 10 / 20 / 30, 4-6 raise, each plus the wear
                int best = 1, bestNew = 0, bestDist = int.MaxValue;
                for (int o = 1; o <= 6; o++)
                {
                    int change = pressureChoice[1 << o] + wear, np = o < 4 ? pressure - change : pressure + change;
                    if (Math.Abs(np - target) < bestDist) (best, bestNew, bestDist) = (o, np, Math.Abs(np - target));
                }
                before = Before();
                EventEnd(locked, (uint)best);
                attempts++;
                int changeMade = pressureChoice[1 << best] + wear;
                string names = NamesFull();
                bool inRange = bestNew >= target - 10 && bestNew <= target + 10;
                stepsRight &= NV(red.Id, "CURRENT_ATTEMPTS") == cur + 1 && (inRange || cur + 1 >= 5 || names.Contains($"{pressureMsg}:{changeMade}:0:0:{Math.Max(bestNew, 0)}"));
                if (inRange)
                {
                    opened = true;
                    stepsRight &= names.Contains($"{pressureMsg}:{changeMade}:0:0:{bestNew}") && names.Contains($"{Text(AbysseaKonschtat, "PLAYER_OPENED_LOCK")}:");
                    expected = ExpectOpened(red, before, out _);
                    actual = ActualOpened(red, before);
                }
                else if (cur + 1 >= 5)
                {
                    failed = true;
                    stepsRight &= names.Contains($"{Text(AbysseaKonschtat, "PLAYER_FAILED_LOCK")}:");
                }
                else stepsRight &= NV(red.Id, "CURRENTPRESSURE") == Math.Max(bestNew, 0);
            }
            Check("the red chest's locked event (the content, the pressure 90-100, the target range packed, the wear message, 5, the attempts, 3, the time)",
                  "True", firstEventRight);
            Check($"the pressure, {attempts} attempts ({(opened ? "opened" : failed ? "five failures" : "unfinished")}): AIR_PRESSURE_CHANGE with the change and the new pressure, " +
                  "the attempts, PLAYER_OPENED_LOCK or PLAYER_FAILED_LOCK", "True/True", $"{stepsRight}/{opened || failed}");
            if (opened) Check($"  opened: the light (giveLight)", expected, actual);
            Check("  the chest gone (removeChest)", "True", Disappears(red, 5));
        }
        // the golden, silvery and ebon lights only come out of red chests at tier 5
        var colours = new HashSet<int>();
        var redWrong = new List<string>();
        int redChests = 0;
        for (int tries = 0; tries < 60 && colours.Count < 3; tries++)
        {
            var ch = ChestOfType(2, 230, 0, 255, 0, 1);
            if (ch == null) continue;
            redChests++;
            EnsureKey();
            string before = Before();
            OpenWithKey(ch);
            colours.Add(NV(ch.Id, "LIGHT"));
            string expect = ExpectOpened(ch, before, out _), act = ActualOpened(ch, before);
            if (expect != act) redWrong.Add($"{expect} / {act}");
            if (!Disappears(ch, 5)) redWrong.Add("the chest stayed");
        }
        Check($"{redChests} red chests opened with keys: golden (2), silvery (3) and ebon (4) light, each the chest's LIGHT_VALUE", "2 3 4/",
              $"{string.Join(" ", colours.OrderBy(x => x))}/{string.Join(" | ", redWrong)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Sturdy Pyxis: a gold chest (amber light) and its combination");
        // amber at the top of tier 4 and the three abyssites of destiny (+1 tier each): tier 5. (Amber in tier 5 itself trips
        // GetGoldChestInfos' 4% tier roll: dataAmberTiers[tier + 1] is nil at tier 5, an error in the DEATH listener.)
        Lua("player:addKeyItem(xi.keyItem.VIRIDIAN_ABYSSITE_OF_DESTINY) player:addKeyItem(xi.keyItem.CRIMSON_ABYSSITE_OF_DESTINY) " +
            "player:addKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_DESTINY)");
        var gold = ChestOfType(3, 230, 0, 0, 223);
        Check("a gold chest (CHESTTYPE 3) at amber 223 with the abyssites of destiny: tier 5, the big coffer (size 1159), model 969, " +
              "68 the highest combination (90 on the 4% roll)", "True/5/1159/969/True",
              gold == null ? "no chest" : $"True/{NV(gold.Id, "TIER")}/{NV(gold.Id, "SIZE")}/{gold.Look[1]}/{NV(gold.Id, "MAX_UNLOCK_NUMBER") is 68 or 90}");
        if (gold != null)
        {
            int locked = 2003 + NV(gold.Id, "TIER");
            int target = NV(gold.Id, "RAND_NUM");
            Near(gold.Id);
            Talk(gold.Id);
            Check("its locked event (the content, 11, the highest, 5, the attempts, the combination itself, 3, the time)",
                  EvStr(gold.Id, locked, long.Parse(ContentMessage(gold)), 11, NV(gold.Id, "MAX_UNLOCK_NUMBER"), 5, 0, target, 3, (EarthNow() - NV(gold.Id, "[pyxis]SPAWNTIME")) * 60), Ev());
            int wrong = target < NV(gold.Id, "MAX_UNLOCK_NUMBER") ? target + 1 : target - 1;
            EventEnd(locked, (uint)wrong);
            string sp = SpecialFull();
            int hunchFrom = Text(AbysseaKonschtat, "HUNCH_SECOND_FIRST_EVEN_ODD"), hunchTo = Text(AbysseaKonschtat, "HUNCH_SUM_EQUALS");
            var hunch = sp.Split(',').Where(x => x.Length > 0).Select(x => x.Split(':').Select(long.Parse).ToArray()).FirstOrDefault(x => x[0] >= hunchFrom && x[0] <= hunchTo);
            int tens = target / 10, units = target % 10;
            bool hunchRight = hunch != null && (hunch[0] - hunchFrom) switch
            {
                // [second/first] digit even / odd, the digit, three numbers about it; one digit; the sum
                0 => (hunch[1] == 0 ? tens : units) % 2 == hunch[2],
                1 => (hunch[1] == 0 ? tens : units) == hunch[2],
                2 => (hunch[1] == 0 ? tens : units) is var d && d >= hunch[2] && d <= hunch[4] && hunch[3] == hunch[2] + 1 && hunch[4] == hunch[2] + 2,
                3 => hunch[1] == tens || hunch[1] == units,
                _ => hunch[1] == tens + units,
            };
            Check($"a wrong combination ({wrong}, the lock at {target}): INPUT_SUCCESS_FAIL_GUESS (nothing happens), GREATER_OR_LESS_THAN, a true hunch, one attempt",
                  $"True/True/True/1",
                  $"{NamesFull().Contains($"{Text(AbysseaKonschtat, "INPUT_SUCCESS_FAIL_GUESS")}:{wrong}:0:0:0")}/" +
                  $"{sp.Contains($"{Text(AbysseaKonschtat, "GREATER_OR_LESS_THAN")}:{wrong}:{(wrong > target ? 1 : 0)}:0:0")}/{hunchRight}/{NV(gold.Id, "CURRENT_ATTEMPTS")}");
            Talk(gold.Id);
            EventEnd(locked, (uint)target);
            Check("the combination: INPUT_SUCCESS_FAIL_GUESS (unlocking), PLAYER_OPENED_LOCK, the chest open (animation sub 13)", "True/True/13",
                  $"{NamesFull().Contains($"{Text(AbysseaKonschtat, "INPUT_SUCCESS_FAIL_GUESS")}:{target}:1:0:0")}/" +
                  $"{NamesFull().Contains($"{Text(AbysseaKonschtat, "PLAYER_OPENED_LOCK")}:")}/{Lua($"report(GetNPCByID({gold.Id}):getAnimationSub())")}");
            Check($"  the drop (type {NV(gold.Id, "DROPTYPE")}) taken from the open chest", $"drop type {NV(gold.Id, "DROPTYPE")}: taken", TakeFirst(gold));
        }
        // the other gold drops with keys: each kind taken
        var goldTypes = new HashSet<int>();
        var goldWrong = new List<string>();
        for (int tries = 0; tries < 100 && goldTypes.Count < 5; tries++)
        {
            var ch = ChestOfType(3, 230, 0, 0, 223, 1);
            if (ch == null) continue;
            if (goldTypes.Contains(NV(ch.Id, "DROPTYPE")))
            {
                Lua($"xi.pyxis.removeChest(player, GetNPCByID({ch.Id}), 0, 1)");
                Tick(1000);
                Tick(1000);
                continue;
            }
            OpenWithKey(ch);
            goldTypes.Add(NV(ch.Id, "DROPTYPE"));
            string taken = TakeFirst(ch);
            if (!taken.EndsWith("taken")) goldWrong.Add(taken);
        }
        Lua("player:delKeyItem(xi.keyItem.VIRIDIAN_ABYSSITE_OF_DESTINY) player:delKeyItem(xi.keyItem.CRIMSON_ABYSSITE_OF_DESTINY) " +
            "player:delKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_DESTINY)");
        Check($"gold chests opened with keys: the temporary items (1), items (2), pop items (3), augmented items (4) and a key item (5) each taken",
              "1 2 3 4 5/", $"{string.Join(" ", goldTypes.OrderBy(x => x))}/{string.Join(" | ", goldWrong)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Sturdy Pyxis: a chest left to its timer, one that went while its event was open");
        // the char kept up through the waits (the zone's mobs wander by)
        Lua("player:setUnkillable(true)");
        var left = ChestOfType(1, 230, 0, 0, 0, 3);
        if (left != null)
        {
            // the second char, in no party with the first, at the chest (its own session)
            var s2 = new MapSession { CharId = OtherId, AccountId = OtherId };
            zone.OnLogin(s2);
            var c2 = (CharData)s2.Char;
            void Send2(ushort opcode, int size, Action<byte[]> fill = null)
            {
                var buf = new byte[size];
                buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
                fill?.Invoke(buf);
                zone.Dispatch(s2, opcode, new BasicPacket(buf));
                zone.AfterParse(s2);
            }
            Send2(0x00A, 92, buf => BitConverter.GetBytes(OtherId).CopyTo(buf, 12));
            Send2(0x00C, 12);
            for (int i = 0; i < 4 && c2.IsInEvent; i++)
            {
                var ev = (ushort)c2.CurrentEvent.EventId;
                Send2(0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
            }
            Send2(0x015, 32, b =>
            {
                BitConverter.GetBytes(left.X + 1.0f).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(left.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(left.Z).CopyTo(b, C2sPos.OffY);
            });
            Tick(400);
            s2.PacketList.Clear();
            Send2(0x01A, 28, b =>
            {
                BitConverter.GetBytes(left.Id).CopyTo(b, 4);
                BitConverter.GetBytes(left.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
            });
            var p2 = new List<BasicPacket>(s2.PacketList);
            Check("another char's chest (canOpenChest: not in the owner's party, the owner in the zone): PARTY_NOT_OWN_CHEST, no event",
                  $"{c2.Zone}/True/False/False",
                  $"{AbysseaKonschtat}/{p2.Any(p => Opcode(p) == 0x02A && (BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF) == Text(AbysseaKonschtat, "PARTY_NOT_OWN_CHEST"))}/" +
                  $"{p2.Any(p => Opcode(p) is 0x032 or 0x034)}/{c2.IsInEvent}");
            zone.OnCharTimeout(s2, false);
            for (int i = 0; i < 175; i++) Tick(1000);
            Check("a chest 175 s on: still there", "0/1/12", ChestState(left));
            for (int i = 0; i < 10 && left.Status == NpcEntity.StatusNormal; i++) Tick(1000);
            Check("  past 180 s (SetPyxisData's npc:timer, removeChest a second on): DISAPPEAR, SPAWNSTATUS 0, animation sub 16", "2/0/16", ChestState(left));
        }
        var late = ChestOfType(1, 230, 0, 0, 0, 3);
        if (late != null)
        {
            int locked = 2003 + NV(late.Id, "TIER");
            Near(late.Id);
            Talk(late.Id);
            for (int i = 0; i < 185 && late.Status == NpcEntity.StatusNormal; i++) Tick(1000);
            Check("a chest gone while its event is open", "2", late.Status);
            EventEnd(locked, 1);
            Check("  the event finished with a guess: CHEST_DESPAWNED, nothing guessed", $"True/0",
                  $"{Special().Contains($"{Text(AbysseaKonschtat, "CHEST_DESPAWNED")}:")}/{NV(late.Id, "CORRECT_GUESSES") + NV(late.Id, "FAILED_ATTEMPTS")}");
        }
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Lua("player:setUnkillable(false) player:setHP(player:getMaxHP())");
        // ---- Atma ----
        Console.WriteLine("Atma: the Atma Infusionist (xi.atma's event 2003, one lunar abyssite)");
        uint infusionist = NpcId("Atma_Infusionist");
        // the atma key items (Lion, Stout Arm, Twin Claw, Allure: atmaMask[1]'s bits 0-3) and their index in the sorted table
        int[] atmaKi = { 1279, 1280, 1281, 1282 };
        int AtmaIndex(int ki) => int.Parse(Lua("local k = {} for key in pairs(xi.atma.atmaMods) do k[#k + 1] = key end table.sort(k) " +
                                               $"for i, v in ipairs(k) do if v == {ki} then report(i) return end end report(0)"));
        var atmaIdx = atmaKi.Select(AtmaIndex).ToArray();
        string AtmaSlot(int slot) => Lua($"local e = player:getStatusEffect(xi.effect.ATMA, {slot}) report(e and e:getPower() or 0)");
        string AtmaSlots() => $"{AtmaSlot(1)} {AtmaSlot(2)} {AtmaSlot(3)}";
        string ModOf(string mod) => Lua($"report(player:getMod(xi.mod.{mod}))");
        // the Lion's mods (TRIPLE_ATTACK 7, DMGPHYS -1000, THUNDER_MAB 30), the Stout Arm's (STR 40, ATT 50, RATT 40)
        string LionMods() => $"{ModOf("TRIPLE_ATTACK")}/{ModOf("DMGPHYS")}/{ModOf("THUNDER_MAB")}";
        string lionBase = LionMods();
        string strBase = ModOf("STR");
        Lua($"player:delCurrency('cruor', player:getCurrency('cruor')) player:addCurrency('cruor', 10000) player:addKeyItem(xi.keyItem.LUNAR_ABYSSITE1)");
        foreach (int ki in atmaKi) Lua($"player:addKeyItem({ki})");
        Near(infusionist);
        Talk(infusionist);
        Check("event 2003 (the cruor, the lunar abyssite mask 1 in the top byte with no atma, the atma masks: bits 0-3)",
              EvStr(infusionist, 2003, 10000, 1 << 24, 0xF) + "/2003", $"{Ev()}/{Cur()}");
        EventEnd(2003, 0, 1);
        Check("  its update: nothing to reinfuse, the history", "0,0,0,0", Upd(4));
        EventEnd(2003, 1u | (uint)atmaIdx[0] << 16);
        Check("option 1 (infuse the Lion): ATMA_INFUSED (100 cruor, the key item), the effect in slot 1, its mods, 100 cruor",
              $"{Text(AbysseaKonschtat, "ATMA_INFUSED")}:100:{atmaKi[0]}:0:0/{atmaKi[0]} 0 0/" +
              $"{int.Parse(lionBase.Split('/')[0]) + 7}/{int.Parse(lionBase.Split('/')[1]) - 1000}/{int.Parse(lionBase.Split('/')[2]) + 30}/9900",
              $"{SpecialFull()}/{AtmaSlots()}/{LionMods()}/{Cruor()}");
        Check("  the char vars: ABYSSEA_LAST_ATMA_INFUSED the Lion's index, ABYSSEA_HISTORY_ATMA1 too", $"{atmaIdx[0]}/{atmaIdx[0]}",
              $"{Var("ABYSSEA_LAST_ATMA_INFUSED")}/{Var("ABYSSEA_HISTORY_ATMA1")}");
        Check("  ON_ZONE and INFLUENCE flags", "True", Lua($"local e = player:getStatusEffect(xi.effect.ATMA, 1) " +
              "report(tostring(bit.band(e:getEffectFlags(), xi.effectFlag.ON_ZONE) ~= 0 and bit.band(e:getEffectFlags(), xi.effectFlag.INFLUENCE) ~= 0))") == "true" ? "True" : "False");
        Talk(infusionist);
        Check("event 2003 again: no free slot (lunar mask 0), the Lion's index in slot 1's byte", EvStr(infusionist, 2003, 9900, atmaIdx[0], 0xF), Ev());
        EventEnd(2003, 1u | (uint)atmaIdx[1] << 16);
        Check("the Stout Arm with one abyssite: ATMA_INFUSED sent all the same, nothing infused, no cruor taken (addAtma's no free slot)",
              $"{Text(AbysseaKonschtat, "ATMA_INFUSED")}:100:{atmaKi[1]}:0:0/{atmaKi[0]} 0 0/9900", $"{SpecialFull()}/{AtmaSlots()}/{Cruor()}");

        Console.WriteLine("Atma: three lunar abyssites, three atma");
        Lua("player:addKeyItem(xi.keyItem.LUNAR_ABYSSITE2) player:addKeyItem(xi.keyItem.LUNAR_ABYSSITE3)");
        Talk(infusionist);
        Check("event 2003: the lunar mask 7 (three abyssites, a free slot)", EvStr(infusionist, 2003, 9900, 7 << 24 | atmaIdx[0], 0xF), Ev());
        EventEnd(2003, 1u | (uint)atmaIdx[0] << 16);
        Check("the Lion again: refused (hasDuplicateAtmaEffect)", $"{atmaKi[0]} 0 0/9900", $"{AtmaSlots()}/{Cruor()}");
        Talk(infusionist);
        EventEnd(2003, 1u | (uint)atmaIdx[1] << 16);
        Talk(infusionist);
        EventEnd(2003, 1u | (uint)atmaIdx[2] << 16);
        Check("the Stout Arm and the Twin Claw: slots 2 and 3, STR +40, 9700 cruor", $"{atmaKi[0]} {atmaKi[1]} {atmaKi[2]}/{int.Parse(strBase) + 40}/9700",
              $"{AtmaSlots()}/{ModOf("STR")}/{Cruor()}");
        Check("  ABYSSEA_LAST_ATMA_INFUSED: the three indexes a byte each; the history newest first",
              $"{atmaIdx[0] | atmaIdx[1] << 8 | atmaIdx[2] << 16}/{atmaIdx[2] | atmaIdx[1] << 8 | atmaIdx[0] << 16}",
              $"{Var("ABYSSEA_LAST_ATMA_INFUSED")}/{Var("ABYSSEA_HISTORY_ATMA1")}");
        Talk(infusionist);
        Check("event 2003: no free slot, the three indexes", EvStr(infusionist, 2003, 9700, atmaIdx[0] | atmaIdx[1] << 8 | atmaIdx[2] << 16, 0xF), Ev());
        EventEnd(2003, 1u | (uint)atmaIdx[3] << 16);
        Check("a fourth (Allure): not infused", $"{atmaKi[0]} {atmaKi[1]} {atmaKi[2]}/9700", $"{AtmaSlots()}/{Cruor()}");

        Console.WriteLine("Atma: purged, reinfused, the history");
        Talk(infusionist);
        EventEnd(2003, 2u | 2u << 16);
        Check("option 2, slot 2: ATMA_PURGED (the key item), the Stout Arm gone, STR back", $"{Text(AbysseaKonschtat, "ATMA_PURGED")}:{atmaKi[1]}:0:0:0/{atmaKi[0]} 0 {atmaKi[2]}/{strBase}",
              $"{SpecialFull()}/{AtmaSlots()}/{ModOf("STR")}");
        Talk(infusionist);
        EventEnd(2003, 2u | 4u << 16);
        Check("option 2, slot 4: ALL_ATMA_PURGED, none left, the Lion's mods gone", $"{Text(AbysseaKonschtat, "ALL_ATMA_PURGED")}:0:0:0:0/0 0 0/{lionBase}",
              $"{SpecialFull()}/{AtmaSlots()}/{LionMods()}");
        Talk(infusionist);
        EventEnd(2003, 0, 1);
        int lastMask = atmaIdx[0] | atmaIdx[1] << 8 | atmaIdx[2] << 16;
        int histo1 = atmaIdx[2] | atmaIdx[1] << 8 | atmaIdx[0] << 16;
        Check("  the update with no atma: the last configuration to reinfuse, the history", $"{lastMask},{histo1},0,0", Upd(4));
        EventEnd(2003, 3);
        Check("option 3: the three again (addAtma each, 300 cruor), PREVIOUS_ATMA_INFUSED with 300",
              $"{Text(AbysseaKonschtat, "PREVIOUS_ATMA_INFUSED")}:300:0:0:0/{atmaKi[0]} {atmaKi[1]} {atmaKi[2]}/9400",
              $"{SpecialFull()}/{AtmaSlots()}/{Cruor()}");
        Talk(infusionist);
        // the history's second and third entries (bits 1 and 2 of the mask from bit 17): purged first, then infused
        EventEnd(2003, 5u | (0b110u << 17));
        Check("option 5 (the history's Stout Arm and Lion): all purged, the two infused (200 cruor); HISTORY_ATMA_INFUSED says " +
              "100 x the history's twelve entries (atmaPrice * #histo)", $"{Text(AbysseaKonschtat, "HISTORY_ATMA_INFUSED")}:1200:0:0:0/{atmaKi[1]} {atmaKi[0]} 0/9200",
              $"{SpecialFull()}/{AtmaSlots()}/{Cruor()}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Atma: a trade to the Infusionist, out of Abyssea");
        Lua("player:addItem(xi.item.FORBIDDEN_KEY)");
        var infusionKey = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == forbiddenKey);
        Trade(infusionist, (infusionKey.Slot, 1));
        var tradeErrors = ScriptErrors();
        Check("a trade: the npc's onTrade calls xi.atma.onTrade, which atma.lua does not define (a script error), nothing taken",
              "True/True", $"{tradeErrors.Any(e => e.Contains("onTrade"))}/{c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == forbiddenKey && i.Slot == infusionKey.Slot)}");
        foreach (var e in tradeErrors) expectedWarnings.Add(e);
        Near(exitMaw.Id);
        Talk(exitMaw.Id);
        EventEnd(200, 1);
        Rezone();
        Check("out by the maw: the atma (ON_ZONE) gone, the mods back", $"{Konschtat}/0 0 0/{lionBase}/{strBase}",
              $"{c.Zone}/{AtmaSlots()}/{LionMods()}/{ModOf("STR")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Cruor ----
        Console.WriteLine("Cruor: the Cruor Prospector's wares (visionsCruorProspectorOnEventFinish)");
        IntoKonschtat();
        Lua("player:addKeyItem(xi.keyItem.TRAVERSER_STONE1)");
        Near(surveyor);
        Talk(surveyor);
        EventEnd(2001, 2u | 1u << 16);
        Lua("player:delCurrency('cruor', player:getCurrency('cruor')) player:addCurrency('cruor', 20000)");
        Near(prospector);
        Talk(prospector);
        Check("event 2002 (the cruor, no demilune abyssite, no cipher campaign)", EvStr(prospector, 2002, 20000) + "/2002", $"{Ev()}/{Cur()}");
        EventEnd(2002, 3u | 5u << 16);
        int clearDemilune = Ki("CLEAR_DEMILUNE_ABYSSITE");
        Check("key items, selection 5: the Clear Demilune Abyssite (KEYITEM_OBTAINED), 300 cruor", $"True/True/19700",
              $"{Lua($"report(tostring(player:hasKeyItem({clearDemilune})))") == "true"}/{SpecialFull().Contains($"{Text(AbysseaKonschtat, "KEYITEM_OBTAINED")}:{clearDemilune}:")}/{Cruor()}");
        Talk(prospector);
        Check("event 2002: the demilune mask 1", EvStr(prospector, 2002, 19700, 1), Ev());
        EventEnd(2002, 3u | 5u << 16);
        Check("the same key item again: npcUtil.giveKeyItem gives nothing but answers true, the 300 cruor taken all the same", "19400/False",
              $"{Cruor()}/{SpecialFull().Contains($"{Text(AbysseaKonschtat, "KEYITEM_OBTAINED")}:")}");
        int perleSalade = int.Parse(Lua("report(xi.item.PERLE_SALADE)"));
        Talk(prospector);
        EventEnd(2002, 1u | 1u << 16);
        Check("items, selection 1: a Perle Salade (ITEM_OBTAINED), 4000 cruor", "True/True/15400",
              $"{c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == perleSalade)}/{SpecialFull().Contains($"{Text(AbysseaKonschtat, "ITEM_OBTAINED")}:{perleSalade}:")}/{Cruor()}");
        Lua($"player:delItem({perleSalade}, 1)");
        Talk(prospector);
        EventEnd(2002, 1u | 18u << 16);
        Check("items, selection 18: the Shadow Throne (2,000,000): not enough cruor, nothing", $"False/15400",
              $"{c.Items.Any(i => i.ItemId == int.Parse(Lua("report(xi.item.SHADOW_THRONE)")))}/{Cruor()}");
        int lucid = int.Parse(Lua("report(xi.item.LUCID_POTION_I)"));
        Lua("player:delContainerItems(xi.inv.TEMPITEMS)");
        Talk(prospector);
        EventEnd(2002, 2u | 1u << 16);
        Check("temporary items, selection 1: a Lucid Potion I in the temporary items (ITEM_OBTAINED), 80 cruor", "True/True/15320",
              $"{c.Items.Any(i => i.Location == Container.TempItems && i.ItemId == lucid)}/{SpecialFull().Contains($"{Text(AbysseaKonschtat, "ITEM_OBTAINED")}:{lucid}:")}/{Cruor()}");

        Console.WriteLine("Cruor: the Prospector's enhancements (the merit and furtherance abyssites' bonuses)");
        string Effect(string name) => Lua($"local e = player:getStatusEffect(xi.effect.{name}) report(e and (e:getPower() .. ':' .. e:getIcon()) or 'none')");
        int maxHpBoost = int.Parse(Lua("report(xi.effect.MAX_HP_BOOST)")), strBoost = int.Parse(Lua("report(xi.effect.STR_BOOST)")),
            dexBoost = int.Parse(Lua("report(xi.effect.DEX_BOOST)"));
        int hpBefore = c.MaxHp;
        string strBefore = ModOf("STR");
        Talk(prospector);
        EventEnd(2002, 4u | 6u << 16);
        Check("enhancements, selection 6: ABYSSEA_HP 20 with the Max HP Boost icon, max HP up, 50 cruor", $"20:{maxHpBoost}/True/15270",
              $"{Effect("ABYSSEA_HP")}/{c.MaxHp > hpBefore}/{Cruor()}");
        Lua("player:addKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_FURTHERANCE) player:addKeyItem(xi.keyItem.SCARLET_ABYSSITE_OF_FURTHERANCE)");
        Talk(prospector);
        EventEnd(2002, 4u | 8u << 16);
        Check("selection 8 with two furtherance abyssites: ABYSSEA_STR and ABYSSEA_DEX 10 + 2 x 10, STR up 30, 120 cruor",
              $"30:{strBoost}/30:{dexBoost}/{int.Parse(strBefore) + 30}/15150", $"{Effect("ABYSSEA_STR")}/{Effect("ABYSSEA_DEX")}/{ModOf("STR")}/{Cruor()}");
        Lua("player:delKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_FURTHERANCE) player:delKeyItem(xi.keyItem.SCARLET_ABYSSITE_OF_FURTHERANCE)");
        Lua("player:addKeyItem(xi.keyItem.AZURE_ABYSSITE_OF_MERIT)");
        Talk(prospector);
        EventEnd(2002, 4u | 11u << 16);
        Check("selection 11 (all nine) with a merit abyssite: ABYSSEA_HP 20 + 10, ABYSSEA_MP 10 + 5, ABYSSEA_VIT 10, ABYSSEA_STR kept at 30 " +
              "(the weaker one not taking over), 470 cruor", $"30/15/10/30/{15150 - 470}",
              $"{Effect("ABYSSEA_HP").Split(':')[0]}/{Effect("ABYSSEA_MP").Split(':')[0]}/{Effect("ABYSSEA_VIT").Split(':')[0]}/{Effect("ABYSSEA_STR").Split(':')[0]}/{Cruor()}");
        Lua("player:delKeyItem(xi.keyItem.AZURE_ABYSSITE_OF_MERIT)");
        Lua("player:delCurrency('cruor', player:getCurrency('cruor')) player:addCurrency('cruor', 40)");
        Lua("player:delStatusEffect(xi.effect.ABYSSEA_HP)");
        Talk(prospector);
        EventEnd(2002, 4u | 6u << 16);
        Check("selection 6 with 40 cruor: nothing", "none/40", $"{Effect("ABYSSEA_HP")}/{Cruor()}");
        Lua("player:addCurrency('cruor', 20000)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- NM pops ----
        // Visitant time for the long stretches ahead: two stones at the surveyor
        void TopUpVisitant()
        {
            Lua("player:addKeyItem(xi.keyItem.TRAVERSER_STONE1) player:addKeyItem(xi.keyItem.TRAVERSER_STONE2)");
            Near(surveyor);
            Talk(surveyor);
            EventEnd(2001, 2u | 2u << 16);
        }
        TopUpVisitant();
        Console.WriteLine("NMs: qm_alkonost (a Giant Bugard Tusk traded to pop Alkonost)");
        uint qmAlkonost = NpcId("qm_alkonost");
        var alkonost = konschtatInfo.Mobs.First(m => m.Name == "Alkonost");
        int tusk = int.Parse(Lua("report(xi.item.GIANT_BUGARD_TUSK)"));
        int MV(MobEntity m, string v) => int.Parse(Lua($"report(GetMobByID({m.Id}):getLocalVar('{v}'))"));
        bool HasKi(int ki) => Lua($"report(tostring(player:hasKeyItem({ki})))") == "true";
        // the char turned to the mob (0x015's direction, as the client does)
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
            Tick(400);
        }
        // a 0x028 of the mob's (actor, category, the first result's animation and param)
        static ulong UB(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);
        string TriggerOf(MobEntity m, List<BasicPacket> packets) => string.Join(",", packets.Where(p => Opcode(p) == 0x028 && UB(p, 40, 32) == m.Id && UB(p, 82, 4) == 11)
                                                                                         .Select(p => $"{UB(p, 150 + 36 + 5, 12)}:{UB(p, 150 + 36 + 27, 17)}"));
        // the NM brought to 1 HP and struck down: the char beside it, facing it, attacking (again each round, it
        // moves). The budget counts the ticks the char can act: a peiste NM's red eyes (Grim Glower: 30-45 s of a
        // petrifying gaze aura, the peiste mixin, then the last 15 s petrification) or a sleep or stun hold it as long
        // as they last
        void KillNm(MobEntity m)
        {
            int acted = 0, held = 0;
            while (acted < 150 && held < 600 && m.IsAlive && !m.IsDead)
            {
                if (c.StatusEffects.HasPreventActionEffect()) held++;
                else if (acted++ % 5 == 0)
                {
                    MoveTo(m.X - 2.0f, m.Y, m.Z);
                    Face(m);
                    Send(0x01A, 28, b =>
                    {
                        BitConverter.GetBytes(m.Id).CopyTo(b, 4);
                        BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
                        BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
                    });
                }
                m.Hp = 1;
                Tick(400);
            }
            Tick(1000);
            Tick(1000);
        }
        // the tusk traded (one added first, unless one the ??? did not take is still held: the tusk is rare, a second
        // is not added). tuskRefused: the state a trade that was not taken left, for the pop loops to print
        string tuskRefused = "";
        string CharEffects() => Lua("for _, e in ipairs(player:getStatusEffects()) do report(e:getEffectType()) end");
        bool TradeTusk()
        {
            if (!c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == tusk))
                Lua("player:addItem(xi.item.GIANT_BUGARD_TUSK)");
            var t = c.Items.First(i => i.Location == Container.Inventory && i.ItemId == tusk);
            Near(qmAlkonost);
            Trade(qmAlkonost, (t.Slot, 1));
            Tick(400);
            Tick(400);
            bool taken = !c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == tusk);
            if (!taken)
                tuskRefused = $"alkonost spawned={alkonost.IsSpawned} (Lua {Lua($"report(tostring(GetMobByID(" +
                              $"{alkonost.Id}):isSpawned()))")}) dead={alkonost.IsDead} hp={alkonost.Hp} " +
                              $"fade={alkonost.FadeAtMs} anim={alkonost.Animation}; char anim={c.Animation} " +
                              $"event={c.IsInEvent} effects=[" + CharEffects() + "]";
            return taken;
        }
        void PopAgain()
        {
            if (!TradeTusk()) Console.WriteLine($"  (a tusk not taken: {tuskRefused})");
        }
        // the NM taken away (a fresh spawn rolls its weaknesses again)
        void Despawn(MobEntity m)
        {
            Lua($"DespawnMob({m.Id})");
            // the fade-out (DespawnMob's 3 s), a dead one's own
            for (int i = 0; i < 20 && m.IsSpawned; i++) Tick(1000);
        }
        Near(qmAlkonost);
        Talk(qmAlkonost);
        Check("the ???: event 1010 (the trade wanted: a Giant Bugard Tusk)", EvStr(qmAlkonost, 1010, tusk) + "/1010", $"{Ev()}/{Cur()}");
        EventEnd(1010);
        Lua("player:setUnkillable(true)");
        bool tuskTaken = TradeTusk();
        Check("the tusk traded: taken, Alkonost spawned by the char (setSpawn within a yalm, then closing in), claimed, [ClaimedBy] the char", $"True/True/{c.Id}/{c.Id}/True",
              $"{tuskTaken}/{alkonost.IsSpawned && alkonost.IsAlive}/{alkonost.OwnerId}/{MV(alkonost, "[ClaimedBy]")}/" +
              $"{MathF.Abs(alkonost.X - c.X) <= 4f && MathF.Abs(alkonost.Z - c.Z) <= 4f}");
        Check("  the SPAWN listener (abyssea_weakness): [CanProc], a red weakness among the thirteen weapon skills, a blue one",
              "1/True/True", $"{MV(alkonost, "[CanProc]")}/{MV(alkonost, "[RedWeakness]") > 0}/{MV(alkonost, "[BlueWeakness]") > 0}");
        Check("another tusk while it is out: not taken", "False", TradeTusk());
        Lua("player:delItem(xi.item.GIANT_BUGARD_TUSK, 1)");

        Console.WriteLine("NMs: Alkonost's red weakness (a weapon skill), the key items it then drops");
        // the char's sword skills that can be red weaknesses: Red Lotus Blade, Seraph Blade (the NM popped again until one is:
        // 2 of the 13)
        var swordRed = new[] { 34, 37 }.Where(ws => CharStats.HasWeaponSkill(c, (ushort)ws)).ToList();
        int pops = 1;
        while (!swordRed.Contains(MV(alkonost, "[RedWeakness]")) && pops < 100)
        {
            Despawn(alkonost);
            PopAgain();
            pops++;
        }
        int redWs = MV(alkonost, "[RedWeakness]");
        Check($"a red weakness the char's sword has ({pops} pops): {redWs}", "True", swordRed.Contains(redWs));
        int gales = Ki("ATMA_OF_GALES"), wing = Ki("TATTERED_HIPPOGRYPH_WING");
        Lua($"player:delKeyItem({gales}) player:delKeyItem({wing})");
        var redPackets = new List<BasicPacket>();
        // on a failure: per try, the char's effects before it, then its messages and whether its weapon skill went out
        var redTries = new List<string>();
        // tried until it lands. The player waits out what keeps a weapon skill from going: Alkonost's Feather Maelstrom
        // (Amnesia, 60 s: the weapon skill refused, MsgWsCannotUseAny 191) and Jettatura (a terror gaze, 10-15 s: the
        // packet validator's PreventAction, no message). Choke Breath's paralysis (30 s) can interrupt a try
        // (CCharEntity::OnWeaponSkill's IsParalyzed, the TP spent): tried again
        bool CantWs() => c.StatusEffects.HasPreventActionEffect() ||
                         Lua("report(tostring(player:hasStatusEffect(xi.effect.AMNESIA)))") == "true";
        for (int i = 0; i < 8 && MV(alkonost, "[AbysseaRedProc]") == 0; i++)
        {
            for (int w = 0; w < 70 && CantWs(); w++) Tick(1000);
            string before = Lua("for _, e in ipairs(player:getStatusEffects()) do report(e:getEffectType()) end");
            int from = redPackets.Count;
            MoveTo(alkonost.X - 2.0f, alkonost.Y, alkonost.Z);
            Face(alkonost);
            c.Tp = 1000;
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(alkonost.Id).CopyTo(b, 4);
                BitConverter.GetBytes(alkonost.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Weaponskill).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, (ushort)redWs);
            });
            for (int t = 0; t < 6; t++) { Tick(400); redPackets.AddRange(s.PacketList); s.PacketList.Clear(); }
            var tried = redPackets.Skip(from).ToList();
            float dx = alkonost.X - c.X, dz = alkonost.Z - c.Z;
            var msgs = tried.Where(p => Opcode(p) == 0x029).Select(p => BitConverter.ToUInt16(p.Buffer, 24));
            int wsOut = tried.Count(p => Opcode(p) == 0x028 && UB(p, 40, 32) == c.Id);
            redTries.Add($"fx=[{before}] msgs=[{string.Join(",", msgs)}] char 0x028s {wsOut} " +
                         $"dist={MathF.Sqrt(dx * dx + dz * dz):0.0} anim={c.Animation}");
        }
        Check("the weapon skill (WEAPONSKILL_TAKE, procMonster RED): [AbysseaRedProc] 1, the red trigger (a MobSkillFinish on itself, " +
              "animation 1806, 2582), Terror", "1/True/true",
              $"{MV(alkonost, "[AbysseaRedProc]")}/{TriggerOf(alkonost, redPackets).Split(',').Contains("1806:2582")}/" +
              $"{Lua($"report(tostring(GetMobByID({alkonost.Id}):hasStatusEffect(xi.effect.TERROR)))")}",
              string.Join(" | ", redTries));
        s.PacketList.Clear();
        KillNm(alkonost);
        int keyMsg = Text(AbysseaKonschtat, "PLAYER_KEYITEM_OBTAINED");
        Check("Alkonost killed (giveNMDrops): the Tattered Hippogryph Wing (the red proc's sure drop) and the Atma of Gales (the claimer's), " +
              "PLAYER_KEYITEM_OBTAINED each", "True/True/True/True",
              $"{alkonost.IsDead}/{HasKi(wing) && HasKi(gales)}/{SpecialFull().Contains($"{keyMsg}:{wing}:")}/{SpecialFull().Contains($"{keyMsg}:{gales}:")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("NMs: qm_kukulkan (three key items to pop Kukulkan)");
        uint qmKukulkan = NpcId("qm_kukulkan");
        var kukulkan = konschtatInfo.Mobs.First(m => m.Name == "Kukulkan" && m.Id == konschtatInfo.Mobs.Where(x => x.Name == "Kukulkan").Min(x => x.Id));
        int[] kukulkanKis = { Ki("TATTERED_HIPPOGRYPH_WING"), Ki("CRACKED_WIVRE_HORN"), Ki("MUCID_AHRIMAN_EYEBALL") };
        Lua($"player:addKeyItem({kukulkanKis[0]}) player:delKeyItem({kukulkanKis[1]}) player:delKeyItem({kukulkanKis[2]})");
        Near(qmKukulkan);
        Talk(qmKukulkan);
        Check("with the wing alone: event 1021 (the key items wanted)", EvStr(qmKukulkan, 1021, kukulkanKis[0], kukulkanKis[1], kukulkanKis[2]) + "/1021", $"{Ev()}/{Cur()}");
        EventEnd(1021, 1);
        Check("  finished: nothing spawned, the wing kept", "False/True", $"{kukulkan.IsSpawned}/{HasKi(kukulkanKis[0])}");
        Lua($"player:addKeyItem({kukulkanKis[1]}) player:addKeyItem({kukulkanKis[2]})");
        Talk(qmKukulkan);
        Check("with all three: event 1020", EvStr(qmKukulkan, 1020, kukulkanKis[0], kukulkanKis[1], kukulkanKis[2]), Ev());
        int noxious = Ki("ATMA_OF_THE_NOXIOUS_FANG"), claw = Ki("VENOMOUS_PEISTE_CLAW");
        Lua($"player:delKeyItem({noxious}) player:delKeyItem({claw})");
        EventEnd(1020, 1);
        Tick(400);
        string lost = string.Join(",", kukulkanKis.Select(k => $"{SpecialFull().Contains($"{Text(AbysseaKonschtat, "LOST_KEYITEM")}:{k}:")}"));
        Check("event 1020 finished (option 1): the three key items gone (LOST_KEYITEM each), Kukulkan spawned by the char and claimed",
              $"False,False,False/True,True,True/True/{c.Id}/{c.Id}",
              $"{string.Join(",", kukulkanKis.Select(k => HasKi(k)))}/{lost}/{kukulkan.IsSpawned && kukulkan.IsAlive}/{kukulkan.OwnerId}/{MV(kukulkan, "[ClaimedBy]")}");
        s.PacketList.Clear();
        KillNm(kukulkan);
        // on a failure: Kukulkan's state (its eyes: the peiste gaze's animation sub), the char's, the actions sent
        string KukulkanDetail()
        {
            float dx = kukulkan.X - c.X, dy = kukulkan.Y - c.Y, dz = kukulkan.Z - c.Z;
            var acts = s.PacketList.Where(p => Opcode(p) == 0x028).Select(p => BitConverter.ToUInt32(p.Buffer, 5))
                .ToList();
            return $"kukulkan hp={kukulkan.Hp} anim={kukulkan.Animation} sub={kukulkan.AnimationSub} " +
                   $"owner={kukulkan.OwnerId} " +
                   $"dist={MathF.Sqrt(dx * dx + dy * dy + dz * dz):0.0} char hp={c.Hp} anim={c.Animation} " +
                   $"target={c.AttackTarget?.Id} effects=[" +
                   Lua("for _, e in ipairs(player:getStatusEffects()) do report(e:getEffectType()) end") +
                   $"] 0x028 by the char {acts.Count(a => a == c.Id)}, by Kukulkan {acts.Count(a => a == kukulkan.Id)}";
        }
        Check("Kukulkan killed without a red proc: the Atma of the Noxious Fang (sure), the Venomous Peiste Claw only on its 20% roll",
              $"True/True/{HasKi(claw)}", $"{kukulkan.IsDead}/{HasKi(noxious)}/{SpecialFull().Contains($"{keyMsg}:{claw}:")}",
              KukulkanDetail());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("NMs: a blue weakness (a slashing weapon skill between 14:00 and 22:00)");
        vanaTicks = vanaTicks / VanaTime.DAY * VanaTime.DAY + 16 * VanaTime.HOUR;
        Tick(1000);
        // (the slashing table's 15 weapon skills; the NM popped again until it is one of the char's)
        var swordBlue = new[] { 40, 41, 42 }.Where(ws => CharStats.HasWeaponSkill(c, (ushort)ws)).ToList();
        Check("the char's slashing sword skills (Vorpal Blade at 230 sword skill)", "True", swordBlue.Count > 0);
        pops = 0;
        do
        {
            if (alkonost.IsSpawned) Despawn(alkonost);
            PopAgain();
            pops++;
        } while (!swordBlue.Contains(MV(alkonost, "[BlueWeakness]")) && pops < 150);
        int blueWs = MV(alkonost, "[BlueWeakness]");
        Check($"a blue weakness the char's sword has ({pops} pops): {blueWs}", "True", swordBlue.Contains(blueWs));
        var bluePackets = new List<BasicPacket>();
        // tried until it lands, the char able to use it first (as the red weakness's)
        for (int i = 0; i < 8 && MV(alkonost, "[AbysseaBlueProc]") == 0; i++)
        {
            for (int w = 0; w < 70 && CantWs(); w++) Tick(1000);
            MoveTo(alkonost.X - 2.0f, alkonost.Y, alkonost.Z);
            Face(alkonost);
            c.Tp = 1000;
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(alkonost.Id).CopyTo(b, 4);
                BitConverter.GetBytes(alkonost.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Weaponskill).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, (ushort)blueWs);
            });
            for (int t = 0; t < 6; t++) { Tick(400); bluePackets.AddRange(s.PacketList); s.PacketList.Clear(); }
        }
        Check("the weapon skill: [AbysseaBlueProc] 1, the blue trigger (animation 1808), Terror", "1/True/true",
              $"{MV(alkonost, "[AbysseaBlueProc]")}/{TriggerOf(alkonost, bluePackets).Split(',').Contains("1808:2582")}/" +
              $"{Lua($"report(tostring(GetMobByID({alkonost.Id}):hasStatusEffect(xi.effect.TERROR)))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("NMs: a yellow weakness (a spell of the day's element or its neighbours', rolled on engaging)");
        Lua("player:changeJob(xi.job.BLM) player:setLevel(75)");
        var blmYellow = new[] { "FIRE_III", "FIRE_IV", "FIRAGA_III", "FLARE", "BLIZZARD_III", "BLIZZARD_IV", "BLIZZAGA_III", "FREEZE",
                                "AERO_III", "AERO_IV", "AEROGA_III", "TORNADO", "STONE_III", "STONE_IV", "STONEGA_III", "QUAKE",
                                "THUNDER_III", "THUNDER_IV", "THUNDAGA_III", "BURST", "WATER_III", "WATER_IV", "WATERGA_III", "FLOOD",
                                "ASPIR", "DRAIN", "BIO_II" }.ToDictionary(n => int.Parse(Lua($"report(xi.magic.spell.{n})")), n => n);
        pops = 0;
        int yellow = 0;
        do
        {
            Despawn(alkonost);
            PopAgain();
            for (int t = 0; t < 10 && MV(alkonost, "[YellowWeakness]") == 0; t++) Tick(400);
            yellow = MV(alkonost, "[YellowWeakness]");
            pops++;
        } while (!blmYellow.ContainsKey(yellow) && pops < 40);
        Check($"a yellow weakness a BLM 75 casts ({pops} pops): {(blmYellow.TryGetValue(yellow, out var yn) ? yn : yellow.ToString())}", "True", blmYellow.ContainsKey(yellow));
        var yellowPackets = new List<BasicPacket>();
        if (blmYellow.ContainsKey(yellow))
        {
            // Manafont: the casts not broken by the blows (TryInterruptSpell; the char has no elemental magic skill)
            Lua($"player:addSpell({yellow}) player:addStatusEffect(xi.effect.MANAFONT, {{ power = 1, duration = 120, origin = player }})");
            // the cast tried again if it still fails
            for (int i = 0; i < 6 && MV(alkonost, "[AbysseaYellowProc]") == 0; i++)
            {
                Lua("player:setMP(player:getMaxMP())");
                MoveTo(alkonost.X - 5.0f, alkonost.Y, alkonost.Z);
                MoveTo(c.X, c.Y, c.Z);
                Face(alkonost);
                s.PacketList.Clear();
                Send(0x01A, 28, b =>
                {
                    BitConverter.GetBytes(alkonost.Id).CopyTo(b, 4);
                    BitConverter.GetBytes(alkonost.TargId).CopyTo(b, C2sAction.OffActIndex);
                    BitConverter.GetBytes((ushort)ActionId.CastMagic).CopyTo(b, C2sAction.OffActionID);
                    C2sAction.SetSpellId(b, (ushort)yellow);
                });
                for (int t = 0; t < 30 && MV(alkonost, "[AbysseaYellowProc]") == 0; t++) { Tick(400); yellowPackets.AddRange(s.PacketList); s.PacketList.Clear(); }
                for (int t = 0; t < 3; t++) { Tick(400); yellowPackets.AddRange(s.PacketList); s.PacketList.Clear(); }
                for (int t = 0; t < 8; t++) Tick(400);
            }
        }
        Check("the spell (MAGIC_TAKE, procMonster YELLOW): [AbysseaYellowProc] 1, the yellow trigger (animation 1807), Terror", "1/True/true",
              $"{MV(alkonost, "[AbysseaYellowProc]")}/{TriggerOf(alkonost, yellowPackets).Split(',').Contains("1807:2582")}/" +
              $"{Lua($"report(tostring(GetMobByID({alkonost.Id}):hasStatusEffect(xi.effect.TERROR)))")}");
        Despawn(alkonost);
        Lua("player:changeJob(xi.job.WAR) player:setLevel(75) player:setUnkillable(false) player:setHP(player:getMaxHP())");
        Send(0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotMain; b[6] = 0; });
        vanaTicks = (vanaTicks / VanaTime.DAY + 1) * VanaTime.DAY + 12 * VanaTime.HOUR;
        Tick(1000);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Conflux ----
        TopUpVisitant();
        Console.WriteLine("Conflux: the Veridical Confluxes (activation, a teleport's cost, the confluence abyssite's discount)");
        uint conflux1 = NpcId("Veridical_Conflux_#01"), conflux3 = NpcId("Veridical_Conflux_#03");
        string ConfluxMask() => Lua("report(player:getTeleport(xi.teleport.type.ABYSSEA_CONFLUX, 0))");
        long Packed(int a, int b) => (uint)(a | b << 16);
        long[] KonschtatCosts(double discount)
        {
            var costs = new[] { 50, 100, 150, 200, 250, 300, 350, 400 }.Select(x => (int)(x * discount)).ToArray();
            return new[] { Packed(costs[0], costs[1]), Packed(costs[2], costs[3]), Packed(costs[4], costs[5]), Packed(costs[6], costs[7]) };
        }
        Lua("player:delCurrency('cruor', player:getCurrency('cruor')) player:addCurrency('cruor', 10000)");
        Lua("for k = xi.keyItem.IVORY_ABYSSITE_OF_CONFLUENCE, xi.keyItem.INDIGO_ABYSSITE_OF_CONFLUENCE do player:delKeyItem(k) end");
        Check("no conflux activated", "0", ConfluxMask());
        Near(conflux1);
        Talk(conflux1);
        Check("#01 not activated: event 2132 (the eight costs packed in pairs, 0, its bit 0, 2, the cruor)",
              EvStr(conflux1, 2132, KonschtatCosts(1).Concat(new long[] { 0, 0, 2, 10000 }).ToArray()) + "/2132", $"{Ev()}/{Cur()}");
        EventEnd(2132, 0, 1);
        Check("  its update: 1", "1", Upd(1));
        EventEnd(2132, 1);
        Check("  option 1: activated (bit 0 of the ABYSSEA_CONFLUX teleport mask), its own cost 50", "1/9950", $"{ConfluxMask()}/{Cruor()}");
        Talk(conflux1);
        Check("#01 again: event 2132 (the costs, the mask less its own bit, 9, 1, the cruor)",
              EvStr(conflux1, 2132, KonschtatCosts(1).Concat(new long[] { 0, 9, 1, 9950 }).ToArray()), Ev());
        EventEnd(2132, 0);
        Check("  option 0: nothing", "9950", Cruor());
        Near(conflux3);
        Talk(conflux3);
        Check("#03 not activated: event 2134 (its bit 2)", EvStr(conflux3, 2134, KonschtatCosts(1).Concat(new long[] { 0, 2, 2, 9950 }).ToArray()), Ev());
        EventEnd(2134, 3);
        Check("  an option other than 1 while not activated: nothing", "1/9950", $"{ConfluxMask()}/{Cruor()}");
        Talk(conflux3);
        EventEnd(2134, 1);
        Check("  option 1: activated, its own cost (the third, 150)", "5/9800", $"{ConfluxMask()}/{Cruor()}");
        float cx = c.X, cz = c.Z;
        Talk(conflux3);
        Check("#03 activated: the mask of the others (bit 0)", EvStr(conflux3, 2134, KonschtatCosts(1).Concat(new long[] { 1, 9, 1, 9800 }).ToArray()), Ev());
        EventEnd(2134, 1);
        Check("  option 1 (to #01): the first cost, 50; the char not moved (the finish sends no position: the client's own warp)",
              $"9750/{cx}/{cz}/none", $"{Cruor()}/{c.X}/{c.Z}/{Cur()}");
        Talk(conflux3);
        EventEnd(2134, 1073741824);
        Check("  the event cancelled: nothing", "9750", Cruor());
        Lua("player:addKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_CONFLUENCE)");
        Talk(conflux3);
        Check("with a confluence abyssite: the costs 80%", EvStr(conflux3, 2134, KonschtatCosts(0.8).Concat(new long[] { 1, 9, 1, 9750 }).ToArray()), Ev());
        EventEnd(2134, 1);
        Check("  option 1: 40 cruor", "9710", Cruor());
        Lua("player:delKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_CONFLUENCE)");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Dominion ----
        Console.WriteLine("Dominion: out of Abyssea - Konschtat, a GM's warp to Abyssea - Altepa");
        Near(exitMaw.Id);
        Talk(exitMaw.Id);
        EventEnd(200, 1);
        Rezone();
        const ushort AbysseaAltepa = 218, AbysseaUleguerand = 253, AbysseaGrauberg = 254, EmpyrealParadox = 255;
        // a GM's warp (setPos into the zone at its surveyor, in the Searing Ward) and two stones there
        void WarpTo(ushort zoneId, float x, float y, float z)
        {
            Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
            Rezone();
            // afterZoneIn's hidden time, as after the maw
            MoveTo(x, y, z);
            Tick(4500);
            s.PacketList.Clear();
        }
        void StonesHere()
        {
            uint sv = NpcId("Conflux_Surveyor");
            Lua("player:addKeyItem(xi.keyItem.TRAVERSER_STONE1) player:addKeyItem(xi.keyItem.TRAVERSER_STONE2)");
            Near(sv);
            Talk(sv);
            EventEnd(2001, 2u | 2u << 16);
        }
        WarpTo(AbysseaAltepa, 410.0f, -1.7f, 329.0f);
        StonesHere();
        Check("Abyssea - Altepa, an hour of Visitant status", $"{AbysseaAltepa}/{visitantIcon}", $"{c.Zone}/{Visitant().Split(',')[0]}");

        Console.WriteLine("Dominion: DSgt_Excenmille (event 500, the ops, signing on, the influence list)");
        uint excenmille = NpcId("DSgt_Excenmille");
        // dOpInfluence[218][1-4]: the fourteen ops' influence a byte each
        long[] influence = { 0x04030201, 0x08070605, 0x0C0B0A09, 0x0E0D };
        for (int i = 0; i < 4; i++) Lua($"SetServerVariable('dOpInfluence[218][{i + 1}]', {influence[i]})");
        Lua("player:delCurrency('dominion_note', player:getCurrency('dominion_note')) player:addCurrency('dominion_note', 500) player:delExp(20000)");
        Lua("player:setCharVar('activeDominionOp', 0)");
        int signedOn = Text(AbysseaAltepa, "DOMINION_SIGNED_ON"), canceled = Text(AbysseaAltepa, "CANCELED_OBJECTIVE");
        int QuestId(string quest) => int.Parse(Lua($"report(xi.quest.id.abyssea.{quest})"));
        string QuestStatus(string quest) => Lua($"report(player:getQuestStatus(xi.questLog.ABYSSEA, xi.quest.id.abyssea.{quest}))");
        Near(excenmille);
        Talk(excenmille);
        Check("no op: event 500 (the packed influence, the sergeant's op mask 30846, 0, the notes)",
              EvStr(excenmille, 500, influence[0], influence[1], influence[2], influence[3], 30846, 0, 500) + "/500", $"{Ev()}/{Cur()}");
        EventEnd(500, 2u | 3u << 4, 1);
        Check("  update 2, op 3: the targets 5 and the op id 562 (559 + Altepa's 0 x 14 + 3)", "5,0,0,0,0,0,0,562", Upd(8));
        EventEnd(500, 6, 1);
        Check("  update 6: the difficulty 1", "1", Upd(1));
        EventEnd(500, 9, 1);
        Check("  update 9: the sphere of influence bonuses (a TODO: all 0)", "0,0,0,0,0,0,0,0", Upd(8));
        EventEnd(500, 2u | 3u << 4);
        Check("finished 2, op 3: DOMINION_OP_03_ALTEPA accepted, activeDominionOp 562, DOMINION_SIGNED_ON", $"1/562/True",
              $"{QuestStatus("DOMINION_OP_03_ALTEPA")}/{Var("activeDominionOp")}/{Special().Contains($"{signedOn}:")}");
        // getOpInfluenceList's 8 * (opNumber - 1) shifts reach 104, LuaJIT's bit.rshift wrapping them to 0-24: the bytes
        // read back right, saved back unchanged
        Check("  the influence read and saved back unchanged", string.Join(",", influence),
              string.Join(",", Enumerable.Range(1, 4).Select(i => Lua($"report(GetServerVariable('dOpInfluence[218][{i}]'))"))));
        Talk(excenmille);
        Check("an op signed on: event 500 (the op, its 5 targets, 0 defeated, status 1)", EvStr(excenmille, 500, 562, 5, 0, 0, 0, 0, 0, 1), Ev());
        EventEnd(500, 0);
        // xi.abyssea.dominionOnMobDeath: no mob script calls it; called here as a mob's death would
        var fov = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            s.PacketList.Clear();
            Lua("xi.abyssea.dominionOnMobDeath(nil, player, 562)");
            fov.Add(string.Join("", Of(0x029).Where(p => BitConverter.ToUInt16(p.Buffer, 24) == 558).Select(p => $"{BitConverter.ToUInt32(p.Buffer, 12)}/{BitConverter.ToUInt32(p.Buffer, 16)}")));
        }
        Check("dominionOnMobDeath six times: FOV_DEFEATED_TARGET 1/5 .. 5/5, the sixth nothing; the progress var 5",
              "1/5,2/5,3/5,4/5,5/5,/5", $"{string.Join(",", fov)}/{Var($"Quest[8][{QuestId("DOMINION_OP_03_ALTEPA")}]Prog")}");
        Talk(excenmille);
        Check("event 500: 5 of 5 defeated, status 2", EvStr(excenmille, 500, 562, 5, 5, 0, 0, 0, 0, 2), Ev());
        int expBefore = c.JobExp[c.MJob];
        string cruorBefore = Cruor();
        EventEnd(500, 8);
        string opSp = SpecialFull();
        Check("finished 8: the op completed, 1000 exp, 200 cruor (CRUOR_TOTAL), 100 notes (OBTAINS_DOMINION_NOTES), the vars cleared",
              $"2/{expBefore + 1000}/{int.Parse(cruorBefore) + 200}/True/600/True/-/-",
              $"{QuestStatus("DOMINION_OP_03_ALTEPA")}/{c.JobExp[c.MJob]}/{Cruor()}/" +
              $"{opSp.Contains($"{Text(AbysseaAltepa, "CRUOR_TOTAL")}:200:{int.Parse(cruorBefore) + 200}:")}/{Lua("report(player:getCurrency('dominion_note'))")}/" +
              $"{opSp.Contains($"{Text(AbysseaAltepa, "OBTAINS_DOMINION_NOTES")}:100:600:")}/{Var("activeDominionOp")}/{Var($"Quest[8][{QuestId("DOMINION_OP_03_ALTEPA")}]Prog")}");
        Talk(excenmille);
        EventEnd(500, 2u | 1u << 4);
        Talk(excenmille);
        EventEnd(500, 3);
        Check("op 1 signed on, then finished 3: the quest removed, the vars cleared, CANCELED_OBJECTIVE", "0/-/True",
              $"{QuestStatus("DOMINION_OP_01_ALTEPA")}/{Var("activeDominionOp")}/{Special().Contains($"{canceled}:")}");
        uint nanaa = NpcId("DSgt_Nanaa");
        Near(nanaa);
        Talk(nanaa);
        Check("DSgt_Nanaa: event 501 with her op mask 8176", EvStr(nanaa, 501, influence[0], influence[1], influence[2], influence[3], 8176, 0, 600), Ev());
        EventEnd(501, 2u | 14u << 4);
        Check("  op 14 signed on (573)", "573/1", $"{Var("activeDominionOp")}/{QuestStatus("DOMINION_OP_14_ALTEPA")}");
        uint altepaProspector = NpcId("Cruor_Prospector");
        Lua("player:delCurrency('cruor', player:getCurrency('cruor')) player:addCurrency('cruor', 8000) player:delKeyItem(xi.keyItem.IVORY_ABYSSITE_OF_MERIT)");
        Near(altepaProspector);
        Talk(altepaProspector);
        Check("Altepa's Cruor Prospector (its own handlers): event 2002 (the cruor, the demilune mask; no cipher param)",
              EvStr(altepaProspector, 2002, 8000, int.Parse(Lua("report(xi.abyssea.getDemiluneAbyssite(player))"))), Ev());
        EventEnd(2002, 3u | 2u << 16);
        Check("  key items, selection 2: the Ivory Abyssite of Merit, 6000 cruor", "True/2000",
              $"{HasKi(Ki("IVORY_ABYSSITE_OF_MERIT"))}/{Cruor()}");
        uint altepaTactician = NpcId("Dominion_Tactician");
        Near(altepaTactician);
        Talk(altepaTactician);
        Check("Altepa's Dominion Tactician: no script (only Grauberg's has one), no event", "none", Ev());
        Check("no script errors", "", string.Join(" | ", ScriptErrors().Where(e => !e.Contains("Dominion_Tactician"))));
        foreach (var e in ScriptErrors()) expectedWarnings.Add(e);

        Console.WriteLine("Dominion: Abyssea - Grauberg with Altepa's op (a sergeant elsewhere, the Dominion Tactician)");
        WarpTo(AbysseaGrauberg, -530.0f, 26.2f, -776.0f);
        StonesHere();
        uint cornelia = NpcId("DSgt_Cornelia");
        Near(cornelia);
        Talk(cornelia);
        // sergeantOnTrigger's opStatus 3 compares the char's zone with the sergeant's own, never the op's: unreachable
        Check("DSgt_Cornelia with Altepa's op 573: event 501 (the op, status 1, not 3: the zone compared is the sergeant's)",
              EvStr(cornelia, 501, 573, 5, 0, 0, 0, 0, 0, 1), Ev());
        EventEnd(501, 3);
        Check("  cancelled here", "-/0", $"{Var("activeDominionOp")}/{QuestStatus("DOMINION_OP_14_ALTEPA")}");
        uint tactician = NpcId("Dominion_Tactician");
        Lua("player:addCurrency('dominion_note', 4400)");
        Near(tactician);
        Talk(tactician);
        Check("the Dominion Tactician: event 120 (the notes, the trophy mask 0)", EvStr(tactician, 120, 5000) + "/120", $"{Ev()}/{Cur()}");
        EventEnd(120, 6, 1);
        Check("  update 6: the NM percentages (a TODO: all 0)", "0,0,0,0,0,0,0,0", Upd(8));
        int unkai = int.Parse(Lua("report(xi.item.UNKAI_DOMARU)")), petrify = int.Parse(Lua("report(xi.item.PETRIFY_SCREEN)")),
            yataghan = int.Parse(Lua("report(xi.item.YATAGHAN)"));
        EventEnd(120, 1u | 1u << 8);
        Check("  items, selection 1: an Unkai Domaru, 1500 notes", "True/3500",
              $"{c.Items.Any(i => i.Location == Container.Inventory && i.ItemId == unkai)}/{Lua("report(player:getCurrency('dominion_note'))")}");
        Lua($"player:delItem({unkai}, 1) player:delContainerItems(xi.inv.TEMPITEMS)");
        Talk(tactician);
        EventEnd(120, 2u | 1u << 8);
        Check("  temporary items, selection 1: a Petrify Screen, 300 notes", "True/3200",
              $"{c.Items.Any(i => i.Location == Container.TempItems && i.ItemId == petrify)}/{Lua("report(player:getCurrency('dominion_note'))")}");
        Talk(tactician);
        EventEnd(120, 3u | 1u << 8);
        var aug = Lua($"local it = player:findItem({yataghan}) if it then local a = it:getAugment(0) report(a[1] .. ':' .. a[2]) else report('none') end");
        // giveAugmentedItem calls addItem(id, 1, augment, value, ...): CLuaBaseEntity::addItem's plain form takes only the
        // quantity (and silence), so the item comes without augments; it answers nil, so no notes are taken
        Check("  augmented, selection 1: a Yataghan, no augments (the plain addItem form), ITEM_OBTAINED; no notes taken", "0:0/True/3200",
              $"{aug}/{SpecialFull().Contains($"{Text(AbysseaGrauberg, "ITEM_OBTAINED")}:{yataghan}:")}/{Lua("report(player:getCurrency('dominion_note'))")}");
        Lua($"player:delItem({yataghan}, 1)");
        Lua("player:delCurrency('dominion_note', player:getCurrency('dominion_note')) player:addCurrency('dominion_note', 100)");
        Talk(tactician);
        EventEnd(120, 1u | 1u << 8);
        Check("  100 notes: nothing", "False/100", $"{c.Items.Any(i => i.ItemId == unkai)}/{Lua("report(player:getCurrency('dominion_note'))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Dominion: Abyssea - Uleguerand's DSgt_Maat");
        WarpTo(AbysseaUleguerand, -222.0f, -40.0f, -524.0f);
        StonesHere();
        for (int i = 0; i < 4; i++) Lua($"SetServerVariable('dOpInfluence[253][{i + 1}]', 0)");
        uint maat = NpcId("DSgt_Maat");
        Near(maat);
        Talk(maat);
        Check("DSgt_Maat: event 500 (no influence, his op mask 32270, the notes)", EvStr(maat, 500, 0, 0, 0, 0, 32270, 0, 100), Ev());
        EventEnd(500, 2u | 2u << 4, 1);
        Check("  update 2, op 2: 575 (559 + Uleguerand's 1 x 14 + 2)", "5,0,0,0,0,0,0,575", Upd(8));
        EventEnd(500, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- Empyreal Paradox ----
        Console.WriteLine("Empyreal Paradox: out of Uleguerand by its maw (the time stored), a GM's warp in");
        uint ulegMaw = NpcId("Cavernous_Maw");
        Near(ulegMaw);
        Talk(ulegMaw);
        EventEnd(200, 1);
        Rezone();
        string storedUleg = Var("abysseaTimeStored");
        Check("Xarcabard, Uleguerand's time stored", "112/True", $"{c.Zone}/{storedUleg != "-" && int.Parse(storedUleg) > 3000}");
        WarpTo(EmpyrealParadox, 540.0f, -500.0f, -571.0f);
        Check("Abyssea - Empyreal Paradox: afterZoneIn's own Visitant status (the icon shown, no duration: no countdown)", $"{EmpyrealParadox}/{visitantIcon},0/0",
              $"{c.Zone}/{Visitant()}/{Lua("report(player:getStatusEffect(xi.effect.VISITANT):getDuration())")}");
        for (int i = 0; i < 10; i++) Tick(1000);
        // (the Paradox's IDs have no ABYSSEA_TIME_OFFSET: a reportTimeRemaining here would be a script error)
        Check("  ten seconds on: unchanged, no effect ticks (no tick interval), no script errors", $"{visitantIcon},0/", $"{Visitant()}/{string.Join(" | ", ScriptErrors())}");
        uint epProspector = NpcId("Cruor_Prospector"), epInfusionist = NpcId("Atma_Infusionist"), epMaw = NpcId("Cavernous_Maw");
        Lua("player:delCurrency('cruor', player:getCurrency('cruor')) player:addCurrency('cruor', 1000) player:delContainerItems(xi.inv.TEMPITEMS)");
        Near(epProspector);
        Talk(epProspector);
        Check("the Paradox's Cruor Prospector: event 2002 (the cruor)", EvStr(epProspector, 2002, 1000, int.Parse(Lua("report(xi.abyssea.getDemiluneAbyssite(player))"))) + "/2002",
              $"{Ev()}/{Cur()}");
        EventEnd(2002, 2u | 1u << 16);
        Check("  temporary items, selection 1: a Lucid Potion I, 80 cruor (only temporary items and enhancements here)", "True/920",
              $"{c.Items.Any(i => i.Location == Container.TempItems && i.ItemId == lucid)}/{Cruor()}");
        Lua("player:delStatusEffect(xi.effect.ABYSSEA_HP)");
        Talk(epProspector);
        EventEnd(2002, 4u | 6u << 16);
        Check("  enhancements, selection 6: ABYSSEA_HP, 50 cruor", "True/870", $"{Effect("ABYSSEA_HP") != "none"}/{Cruor()}");
        Near(epInfusionist);
        Talk(epInfusionist);
        Check("the Paradox's Atma Infusionist: event 2003 (xi.atma.onTrigger)", "2003", Cur());
        EventEnd(2003, 0);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Empyreal Paradox: out by its maw");
        Near(epMaw);
        Talk(epMaw);
        Check("the Paradox's maw: event 200", "200", Cur());
        EventEnd(200, 1);
        Rezone();
        // onEffectLose: the Paradox's Visitant has the icon, so the time left is stored: no duration, 0 less 4, floored at 0
        Check("event 200 finished: Qufim Island; Visitant gone, no event 2180; the stored Uleguerand time overwritten by the " +
              "Paradox's 0 (setCharVar 0 clears it)", "126/false/none/-", $"{c.Zone}/{Has("VISITANT")}/{Cur()}/{Var("abysseaTimeStored")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        if (Environment.GetEnvironmentVariable("ABYSSEA_DUMP_LOG") == "1") foreach (var l in log.Distinct()) Console.WriteLine("  log: " + l);
    }
}
