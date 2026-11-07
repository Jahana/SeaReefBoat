using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Pvp.Live -- live gate for a char engaging a char (CPlayerController::Engage on a TYPE_PC target,
// CCharEntity::ValidTarget's allegiance rules, CCharEntity::CanAttack, the char-on-char swing and the defender's death)
// in West Ronfaure; a weapon skill and a spell on a char (IsValidTarget(TARGET_ENEMY) through CCharEntity::ValidTarget).
// Two chars, a RDM and a WAR, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993780, RivalId = 993781;
    private const ushort WestRonfaure = 100;
    private const byte SanDoria = 2, Bastok = 3, Wyverns = 5, Griffons = 6;
    private const ushort BronzeSword = 16535, FastBlade = 32, Stone = 159;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

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
        foreach (uint id in new[] { CharId, RivalId })
        {
            foreach (var t in CharidTables(db))
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
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

    private static void AddChar(IDatabase db, uint id, string name, byte job, string jobColumn)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of("pvp" + id), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 500, ?, 75)", BoundValue.Of(id), BoundValue.Of(job));
        Sql(db, $"REPLACE INTO char_jobs(charid, {jobColumn}) VALUES (?, 75)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        AddChar(db, CharId, "Duelist", 5, "rdm");
        AddChar(db, RivalId, "Rival", 1, "war");
        Sql(db, "INSERT INTO char_spells(charid, spellid) VALUES (?, ?)", BoundValue.Of(CharId), BoundValue.Of(Stone));
        // the duelist's bronze sword and the sword skill for Fast Blade
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 3, 100, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(BronzeSword), BoundValue.Of(new byte[24]));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        // the duel is staged off the zone's navmesh
        zone.NavMeshDir = null;
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(3);
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        MapSession Login(uint id)
        {
            var s = new MapSession { CharId = id, AccountId = id };
            zone.OnLogin(s);
            Send(s, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            Send(s, 0x00C, 12);
            return s;
        }
        void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        var s1 = Login(CharId);
        var s2 = Login(RivalId);
        var c = (CharData)s1.Char;
        var rival = (CharData)s2.Char;
        Check("both in West Ronfaure", "100/100", $"{c.Zone}/{rival.Zone}");
        void Tick(long ms)
        {
            s1.PacketList.Clear();
            now += ms;
            zone.Tick();
        }
        // past onGameIn's zoning-in window
        Tick(3000);

        // the duel: the rival 1.5 yalms east of the char, the char facing east
        void Stage(float dx)
        {
            c.X = -300; c.Y = 0; c.Z = 300; c.Rotation = 0;
            rival.X = c.X + dx; rival.Y = c.Y; rival.Z = c.Z;
        }
        Stage(1.5f);
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s1.PacketList).FindAll(p => Opcode(p) == opcode);
        void Action(ActionId id)
        {
            s1.PacketList.Clear();
            Send(s1, 0x01A, 28, b =>
            {
                BitConverter.GetBytes(RivalId).CopyTo(b, 4);
                BitConverter.GetBytes(rival.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)id).CopyTo(b, C2sAction.OffActionID);
            });
        }
        void EndFight()
        {
            Action(ActionId.AttackOff);
            Tick(400);
            // the engage lockout after a fight
            Tick(5000);
        }

        Console.WriteLine("Same allegiance: no engage");
        Check("both of the player allegiance", "1/1", $"{c.Allegiance}/{rival.Allegiance}");
        Action(ActionId.Attack);
        Check("not engaged, no assist", "True/0", $"{c.AttackTarget == null}/{Of(0x058).Count}");

        Console.WriteLine("Nation PvP: San d'Oria engages Bastok");
        c.Allegiance = SanDoria;
        rival.Allegiance = Bastok;
        Action(ActionId.Attack);
        var assist = Of(0x058);
        Check("engaged on the rival, the assist packet on it", $"True/True/1/{RivalId}",
            $"{c.AttackTarget == rival}/{c.BattleTarget == rival}/{assist.Count}/{(assist.Count == 1 ? BitConverter.ToUInt32(assist[0].Buffer, 8) : 0)}");
        Check("weapon drawn, first swing 3 s off", $"1/{now + 3000}", $"{c.Animation}/{c.SwingAtMs}");
        int hpBefore = rival.Hp;
        Tick(3000);
        var rounds = Of(0x028);
        Check("a round at 3 s: the char's basic attack on the rival", $"1/{CharId}/1/{RivalId}",
            rounds.Count == 0 ? "none" : $"{rounds.Count}/{U(rounds[0], 40, 32)}/{U(rounds[0], 82, 4)}/{U(rounds[0], 150, 32)}");
        int dealt = 0;
        if (rounds.Count == 1)
        {
            int n = (int)U(rounds[0], 182, 4);
            for (int i = 0; i < n; i++)
            {
                int at = 186 + i * 87;
                if (U(rounds[0], at, 3) == 0) dealt += (int)U(rounds[0], at + 27, 17);
            }
        }
        Check("the rival's HP down by the hits, no claim on a char", $"{hpBefore - dealt}/True", $"{rival.Hp}/{c.ClaimedMob == null}");
        Check("TP to both: the swinger and the struck", "True", $"{dealt == 0 || (c.Tp > 0 && rival.Tp > 0)}");

        Console.WriteLine("CanAttack on a char: out of reach, facing away");
        Stage(6f);
        Tick(c.SwingAtMs - now);
        var err = Of(0x029).Find(p => BitConverter.ToUInt16(p.Buffer, 24) == 4);
        Check("out of reach: no round, target out of range (4) on the rival", $"0/True/{RivalId}",
            $"{Of(0x028).Count}/{err != null}/{(err == null ? 0 : BitConverter.ToUInt32(err.Buffer, 8))}");
        Check("still engaged", true, c.AttackTarget == rival);
        EndFight();
        Check("AttackOff: disengaged", "True/0", $"{c.AttackTarget == null}/{c.Animation}");

        Console.WriteLine("Team PvP");
        Stage(1.5f);
        c.Allegiance = Wyverns;
        rival.Allegiance = SanDoria;
        Action(ActionId.Attack);
        Check("Wyverns on San d'Oria: neither the same teams nor nations, no engage", true, c.AttackTarget == null);
        rival.Allegiance = Wyverns;
        Action(ActionId.Attack);
        Check("Wyverns on Wyverns: no engage", true, c.AttackTarget == null);
        rival.Allegiance = Griffons;
        Action(ActionId.Attack);
        Check("Wyverns on Griffons: engaged", true, c.AttackTarget == rival);

        Console.WriteLine("Confrontation: never across one");
        EndFight();
        scripts.Lua.DoString($"GetPlayerByID({CharId}):addStatusEffect(xi.effect.CONFRONTATION, {{ power = 7, duration = 600, origin = GetPlayerByID({CharId}) }})", "=verify_pvp");
        bool confronted = c.StatusEffects.GetConfrontationEffect() != rival.StatusEffects.GetConfrontationEffect();
        Action(ActionId.Attack);
        Check("a confrontation only the char is in: no engage", "True/True", $"{confronted}/{c.AttackTarget == null}");
        // the effect leaves the set on the next tick
        c.StatusEffects.DelStatusEffect(276);
        Tick(400);

        Console.WriteLine("A weapon skill on the rival");
        Send(s1, 0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotMain; b[6] = 0; });
        void Ws(ushort wsid)
        {
            s1.PacketList.Clear();
            Send(s1, 0x01A, 28, b =>
            {
                BitConverter.GetBytes(RivalId).CopyTo(b, 4);
                BitConverter.GetBytes(rival.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.Weaponskill).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, wsid);
            });
        }
        Check("the sword on, Fast Blade known", $"{BronzeSword}/True",
            $"{c.Equipped[CharStats.SlotMain]?.ItemId ?? 0}/{CharStats.HasWeaponSkill(c, FastBlade)}");
        c.Tp = 1000;
        rival.Allegiance = Wyverns;
        Ws(FastBlade);
        var refusal = Of(0x029).Find(p => BitConverter.ToUInt16(p.Buffer, 24) == 446);
        Check("on a char of its own team: cannot attack that target (446), no state, TP kept", "True/True/1000",
            $"{refusal != null}/{!c.InWeaponSkill}/{c.Tp}");
        rival.Allegiance = Griffons;
        int rivalHp = rival.Hp;
        Ws(FastBlade);
        Check("on a Griffon: readied", "True/1", $"{c.InWeaponSkill}/{Of(0x028).Count}");
        Tick(1);
        var finish = Of(0x028);
        int wsDealt = -1;
        if (finish.Count == 1)
        {
            int at = 186;
            Check("the finish: skill finish, Fast Blade, on the rival", $"{CharId}/3/{FastBlade}/{RivalId}",
                $"{U(finish[0], 40, 32)}/{U(finish[0], 82, 4)}/{U(finish[0], 86, 32)}/{U(finish[0], 150, 32)}");
            wsDealt = (int)U(finish[0], at + 27, 17);
        }
        else Check("the finish BATTLE2", 1, finish.Count);
        Check("the rival's HP down by the damage, TP spent", $"{rivalHp - wsDealt}/True", $"{rival.Hp}/{c.Tp < 1000}");
        Tick(10_000);
        Check("the state over", true, !c.InWeaponSkill);

        Console.WriteLine("A spell on the rival");
        void Cast(ushort spell)
        {
            s1.PacketList.Clear();
            Send(s1, 0x01A, 28, b =>
            {
                BitConverter.GetBytes(RivalId).CopyTo(b, 4);
                BitConverter.GetBytes(rival.TargId).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.CastMagic).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSpellId(b, spell);
            });
        }
        rival.Allegiance = Wyverns;
        Cast(Stone);
        Check("Stone on a char of its own team: refused, no cast", "True/True",
            $"{Of(0x029).Exists(p => BitConverter.ToUInt16(p.Buffer, 24) == 446)}/{c.MagicState == null}");
        rival.Allegiance = Griffons;
        rivalHp = rival.Hp;
        int mp = c.Mp;
        Cast(Stone);
        Check("on a Griffon: casting", true, c.MagicState != null);
        BasicPacket landed = null;
        for (int i = 0; i < 20 && landed == null; i++)
        {
            Tick(400);
            landed = Of(0x028).Find(p => U(p, 82, 4) == 4);
        }
        Check("the finish: magic finish, Stone, on the rival", $"{CharId}/4/{Stone}/{RivalId}",
            landed == null ? "none" : $"{U(landed, 40, 32)}/{U(landed, 82, 4)}/{U(landed, 86, 32)}/{U(landed, 150, 32)}");
        int spellDealt = landed == null ? -1 : (int)U(landed, 186 + 27, 17);
        Check("the rival's HP down by the damage, MP spent", $"{rivalHp - spellDealt}/True", $"{rival.Hp}/{c.Mp < mp}");
        Tick(5000);

        Console.WriteLine("The rival falls");
        Action(ActionId.Attack);
        Check("engaged again", true, c.AttackTarget == rival);
        rival.Hp = 1;
        bool down = false;
        string defeated = "none";
        for (int i = 0; i < 40 && !down; i++)
        {
            Tick(400);
            down = rival.DeathAtMs != 0;
            var m = Of(0x029).Find(p => BitConverter.ToUInt16(p.Buffer, 24) == 97);
            if (m != null) defeated = $"{BitConverter.ToUInt32(m.Buffer, 4)}>{BitConverter.ToUInt32(m.Buffer, 8)}";
        }
        Check("killed by the char: its death state", true, down);
        Check("the char in range sees 'the rival was defeated by the char' (97)", $"{CharId}>{RivalId}", defeated);
        Tick(400);
        Check("the dead rival is no target: the char's fight ends", "True/0", $"{c.AttackTarget == null}/{c.Animation}");
        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.Contains("lua") && l.Contains("rror") && !l.Contains(" 0 load errors"))));
    }
}
