using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.World;
using XI.Zone;

// XI.Verify.Warp.Live -- live gate for the home point warp (ZoneHost.Warp: requestedWarp + charutils::HomePoint from
// the zone tick; c2s 0x0F0 RESCUE (self-unstuck), 0x01A's death menu Home Point, 0x0F1 BUFFCANCEL) against the real
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990901;
    private const string CharName = "Warpa";
    private const ushort StartZone = 240, HomeZone = 238;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Q1(IDatabase db, string q)
    {
        var r = Sql(db, q, BoundValue.Of(CharId));
        return r != null && r.Next() ? r.Get<string>(0u) : "-";
    }

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var t in CharidTables(db))
            Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(CharId));
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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var cfg = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), cfg);
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;

        // WAR75 standing in Port Windurst, its home point in Windurst Waters
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("wrp" + CharName), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,pos_rot,home_zone,home_x,home_y,home_z,home_rot,nation,gmlevel) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(CharName), BoundValue.Of(StartZone), BoundValue.Of((ushort)0),
            BoundValue.Of(-100f), BoundValue.Of(-5f), BoundValue.Of(20f), BoundValue.Of((byte)64),
            BoundValue.Of(HomeZone), BoundValue.Of(12.5f), BoundValue.Of(-3f), BoundValue.Of(40.25f), BoundValue.Of((byte)192),
            BoundValue.Of((byte)2), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 75, 0, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));

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
        void Login()
        {
            s = new MapSession { CharId = CharId, AccountId = CharId };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
            s.PacketList.Clear();
        }
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear() { s.PacketList.Clear(); log.Clear(); sent.Clear(); }
        void Rescue(uint state) => Send(0x0F0, 8, buf => BitConverter.GetBytes(state).CopyTo(buf, 4));
        void HomepointMenu() => Send(0x01A, 28, buf => BitConverter.GetBytes((ushort)ActionId.HomepointMenu).CopyTo(buf, C2sAction.OffActionID));
        string Pos() => Q1(db, "SELECT CONCAT_WS('/', pos_zone, pos_prevzone, pos_x, pos_y, pos_z, pos_rot, boundary) FROM chars WHERE charid = ?");
        string Var(string name) => Q1(db, $"SELECT CONCAT(value, '/', expiry) FROM char_vars WHERE charid = ? AND varname = '{name}'");

        Login();
        Check("loaded the home point", "238/12.5/-3/40.25/192", $"{c.HomeZone}/{c.HomeX}/{c.HomeY}/{c.HomeZ}/{c.HomeRot}");

        Console.WriteLine("rescue (0x0F0) while SELF_UNSTUCK_ENABLED is off:");
        Clear();
        Rescue(1);
        Check("State 1 is refused", true, log.Exists(l => l.Contains("State not 0")));
        Clear();
        Rescue(0);
        zone.Tick();
        Check("nothing: no warp, no message", "None/0/0/True", $"{c.RequestedWarp}/{Of(0x017).Count}/{Of(0x00B).Count}/{s.InZone}");
        Check("no cooldown var", "-", Var("[GM]SelfUnstuck"));

        Console.WriteLine("rescue, enabled:");
        cfg.SelfUnstuckEnabled = true;
        cfg.SelfUnstuckCooldown = 3600;
        c.Hp = 20;
        c.Boundary = 3;
        Clear();
        long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Rescue(0);
        Check("the warp is requested", WarpRequest.HomePoint, c.RequestedWarp);
        Check("logged", true, log.Exists(l => l == "Warpa requested self-unstuck, warping them to their Home Point."));
        var cd = Var("[GM]SelfUnstuck").Split('/');
        Check("[GM]SelfUnstuck 1 for SELF_UNSTUCK_COOLDOWN", "1/True", $"{cd[0]}/{Math.Abs(long.Parse(cd[1]) - before - 3600) <= 2}");
        Check("nothing sent before the tick", 0, s.PacketList.Count);
        zone.Tick();
        Check("the tick: LOGOUT (zone change) only, out of the zone", "1/1/False", $"{s.PacketList.Count}/{Of(0x00B).Count}/{s.InZone}");
        Check("the char is saved at its home point, from the start zone, boundary 0", "238/240/12.500/-3.000/40.250/192/0", Pos());
        Check("alive: HP untouched, the request cleared", "20/None", $"{c.Hp}/{c.RequestedWarp}");
        Check("no conquest homepoint for the living", 0, sent.Count);

        Console.WriteLine("rescue on cooldown (after zoning in at the home point):");
        Login();
        Check("in Windurst Waters", "238/12.5", $"{c.Zone}/{c.X}");
        Clear();
        Rescue(0);
        var std = Of(0x017);
        Check("CHAT_STD MESSAGE_SYSTEM_1 from the char", "1/6/Warpa/Self-unstuck is on cooldown.",
              std.Count == 1 ? $"1/{std[0].Buffer[4]}/{CborRaw.FromBytes(std[0].Buffer.AsSpan(8, 15)).TrimEnd('\0')}/" +
                               $"{CborRaw.FromBytes(std[0].Buffer.AsSpan(23, 28)).TrimEnd('\0')}" : $"{std.Count}");
        Check("size: roundUp4(23 + 28)", 52, std.Count == 1 ? std[0].GetSize() : 0);
        Check("logged, no warp", "True/None", $"{log.Exists(l => l.Contains("still on cooldown"))}/{c.RequestedWarp}");
        Sql(db, "UPDATE char_vars SET expiry = UNIX_TIMESTAMP() - 1 WHERE charid = ? AND varname = '[GM]SelfUnstuck'", BoundValue.Of(CharId));
        c.CharVars.Remove("[GM]SelfUnstuck");
        Clear();
        Rescue(0);
        Check("the cooldown expired: warps again", WarpRequest.HomePoint, c.RequestedWarp);
        c.RequestedWarp = WarpRequest.None;

        Console.WriteLine("home point at the zone cap:");
        cfg.ZonePlayerCap = 1;
        c.RequestedWarp = WarpRequest.HomePoint;
        Clear();
        zone.Tick();
        var sys = Of(0x053);
        Check("CouldNotEnter (2), the request dropped, still in the zone", "1/2/None/0/True",
              $"{sys.Count}/{(sys.Count == 1 ? BitConverter.ToUInt16(sys[0].Buffer, 12) : -1)}/{c.RequestedWarp}/{Of(0x00B).Count}/{s.InZone}");
        cfg.ZonePlayerCap = 700;

        Console.WriteLine("the death menu's Home Point (0x01A HomepointMenu), dead in West Sarutabaruta:");
        Sql(db, "UPDATE chars SET pos_zone = 115, pos_prevzone = 238 WHERE charid = ?", BoundValue.Of(CharId));
        Login();
        Clear();
        HomepointMenu();
        Check("alive: Character is not dead", "True/None", $"{log.Exists(l => l.Contains("Character is not dead"))}/{c.RequestedWarp}");
        Sql(db, "INSERT INTO char_vars(charid, varname, value, expiry) VALUES (?, 'expLost', 1234, 0)", BoundValue.Of(CharId));
        c.CharVars.Remove("expLost");
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Weakness, EffectId.Weakness, 0, 0, 300_000));
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.LevelSync, EffectId.LevelSync, 30, 0, 0));
        Check("weakness and level sync on", "True/True", $"{c.StatusEffects.HasStatusEffect(EffectId.Weakness)}/{c.StatusEffects.HasStatusEffect(EffectId.LevelSync)}");
        c.Hp = 0;
        c.Mp = 3;
        c.Animation = 3;
        Clear();
        HomepointMenu();
        Check("dead: expLost cleared, the warp requested", "-/HomePoint", $"{Var("expLost")}/{c.RequestedWarp}");
        zone.Tick();
        Check("revived to full HP/MP, animation none", $"{c.MaxHp}/{c.MaxMp}/0/True", $"{c.Hp}/{c.Mp}/{c.Animation}/{c.MaxHp > 0}");
        Check("weakness and level sync gone", "False/False", $"{c.StatusEffects.HasStatusEffect(EffectId.Weakness)}/{c.StatusEffects.HasStatusEffect(EffectId.LevelSync)}");
        Check("zoned to the home point", "1/False/238/115", $"{Of(0x00B).Count}/{s.InZone}/{Pos().Split('/')[0]}/{Pos().Split('/')[1]}");
        byte region = ZoneRegions.Of(115);
        var ev = sent.Find(m => m is ConquestEvent) as ConquestEvent;
        var counter = ev == null ? null : ConquestAddCounter.FromBytes(ev.Payload);
        Check("M2W_AddPlayerHomepoints, count 1 for the death zone's region", $"{(byte)ConquestMessage.M2W_AddPlayerHomepoints}/1/{region}",
              ev == null ? "none" : $"{ev.TypeField}/{counter?.Count}/{counter?.Region}");

        Console.WriteLine("dead in a city (Windurst Waters, not a conquest region):");
        Login();
        c.Hp = 0;
        Clear();
        HomepointMenu();
        zone.Tick();
        Check("a warp in the same zone keeps pos_prevzone; no conquest homepoint", $"{c.MaxHp}/1/238/115/0",
              $"{c.Hp}/{Of(0x00B).Count}/{Pos().Split('/')[0]}/{Pos().Split('/')[1]}/{sent.Count}");

        Console.WriteLine("below MINIMUM_LEVEL_CONQUEST_INFUENCE_LOSS:");
        Sql(db, "UPDATE chars SET pos_zone = 115 WHERE charid = ?", BoundValue.Of(CharId));
        Login();
        c.MLvl = 5;
        c.Hp = 0;
        Clear();
        HomepointMenu();
        zone.Tick();
        Check("revived and zoned, no conquest homepoint", $"{c.MaxHp}/1/0", $"{c.Hp}/{Of(0x00B).Count}/{sent.Count}");

        Console.WriteLine("buffcancel (0x0F1):");
        Login();
        void Cancel(ushort buff) => Send(0x0F1, 8, buf => BitConverter.GetBytes(buff).CopyTo(buf, 4));
        Clear();
        Cancel(0);
        Check("BuffNo 0 is refused", true, log.Exists(l => l.Contains("BuffNo")));
        Clear();
        Cancel(815);
        Check("BuffNo 815 (over MAX_EFFECTID) is refused", true, log.Exists(l => l.Contains("BuffNo")));
        Clear();
        Cancel(814);
        Check("BuffNo 814: accepted, no effect to cancel", "False/0", $"{log.Exists(l => l.Contains("BuffNo"))}/{s.PacketList.Count}");
    }
}
