using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.ExtendedJob.Live -- live gate for c2s 0x102 EXTENDED_JOB (ZoneHost.ExtendedJob: blueutils' set blue magic,
// puppetutils' automaton head / frame / attachments, s2c 0x044 EXTENDED_JOB) against the real MariaDB (xidb) and the
// automaton Lua tables, plus the job change's blue magic refresh (0x100).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a BLU30/WAR15 and a PUP30/WAR15, each in its own mog house in Windurst Waters
    private static readonly (uint Id, string Name, byte Job, string Jobs)[] Chars =
    {
        (990971, "Exblu", 16, "war = 30, whm = 10, blu = 30"), (990972, "Expup", 18, "war = 30, pup = 30"),
    };

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string Scalar(IDatabase db, string q, params BoundValue[] a)
    {
        var r = db.Execute(q, a);
        return r != null && r.Next() ? r.Get<string>(0u) : null;
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
        foreach (var (id, _, _, _) in Chars)
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

    private static string Hex(byte[] b, int off, int len) => BitConverter.ToString(b, off, len).Replace("-", "");

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        uint unlocked = 1u << 1 | 1u << 3 | 1u << 16 | 1u << 18;
        foreach (var (id, name, job, jobs) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("ej" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of((ushort)238), BoundValue.Of((ushort)0),
                BoundValue.Of(id), BoundValue.Of((byte)2), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, ?, 30, 1, 15)", BoundValue.Of(id), BoundValue.Of(job));
            Sql(db, $"REPLACE INTO char_jobs SET charid = ?, unlocked = ?, {jobs}", BoundValue.Of(id), BoundValue.Of(unlocked));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
            Sql(db, "DELETE FROM char_equip WHERE charid = ?", BoundValue.Of(id));
        }

        // the blue mage knows venom shell (42), metallic body (8), screwdriver (26), death ray (34), sandspin (1),
        // healing breeze (16), soporific (24), wild oats (4), feather storm (12), head butt (12) and pollen (1); its
        // saved set is screwdriver, venom shell (too high at 30) and sandspin, with a gap
        uint blu = Chars[0].Id, pup = Chars[1].Id;
        foreach (ushort spell in new ushort[] { 513, 517, 519, 522, 524, 581, 598, 603, 638, 623, 549 })
            Sql(db, "INSERT INTO char_spells(charid, spellid) VALUES (?, ?)", BoundValue.Of(blu), BoundValue.Of(spell));
        var saved = new byte[20];
        saved[0] = 519 - 0x200; saved[2] = 513 - 0x200; saved[3] = 524 - 0x200;
        Sql(db, "UPDATE chars SET set_blue_spells = ? WHERE charid = ?", BoundValue.Of(saved), BoundValue.Of(blu));

        // the puppetmaster's automaton "Azure" (pet_name 1): Valoredge head and frame unlocked; strobe, tension spring,
        // the auto-repair kit, the mana tank and mana tank IV unlocked (inhibitor not); Harlequin with a strobe on
        var unlockBlob = new byte[36];
        unlockBlob[0] = 1 << 2;      // heads: Valoredge (0x2002 & 0x0F)
        unlockBlob[1] = 1 << 1;      // frames: Valoredge (0x2021 & 0x0F)
        foreach (int att in new[] { 1, 2, 193, 225, 235 }) unlockBlob[4 + att / 8] |= (byte)(1 << att % 8);
        var equipBlob = new byte[14];
        equipBlob[0] = 0x20; equipBlob[1] = 1; equipBlob[2] = 1;
        Sql(db, "REPLACE INTO char_pet(charid, automatonid, unlocked_attachments, equipped_attachments) VALUES (?, 1, ?, ?)",
            BoundValue.Of(pup), BoundValue.Of(unlockBlob), BoundValue.Of(equipBlob));

        var ss = new MapSession[Chars.Length];
        var cs = new CharData[Chars.Length];
        void Send(int i, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(ss[i], opcode, new BasicPacket(buf));
            zone.AfterParse(ss[i]);
        }
        List<BasicPacket> Of(int i, int opcode) => new List<BasicPacket>(ss[i].PacketList).FindAll(p => Opcode(p) == opcode);
        void Clear() { foreach (var s in ss) s?.PacketList.Clear(); log.Clear(); sent.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        void Login(int i)
        {
            ss[i] = new MapSession { CharId = Chars[i].Id, AccountId = Chars[i].Id };
            zone.OnLogin(ss[i]);
            cs[i] = (CharData)ss[i].Char;
            Send(i, 0x00A, 92, buf => BitConverter.GetBytes(Chars[i].Id).CopyTo(buf, 12));
            Send(i, 0x00C, 12);
        }
        // 0x102 blu_data_t / pup_data_t: SpellId or ItemId @4, JobIndex @8, Spells[20] or Slots[14] @12
        void Ext(int i, byte first, byte jobIndex, params (int Index, byte Value)[] slots) =>
            Send(i, 0x102, 164, buf =>
            {
                buf[4] = first;
                buf[8] = jobIndex;
                foreach (var (index, value) in slots) buf[12 + index] = value;
            });
        string SetSpells(CharData c) => Hex(c.SetBlueSpells, 0, 20).TrimEnd('0');
        string DbSpells() => Scalar(db, "SELECT HEX(set_blue_spells) FROM chars WHERE charid = ?", BoundValue.Of(blu)).TrimEnd('0');
        string Mods(CharData c) => $"{c.ModStat[2]}/{c.ModStat[14]}";

        // ---- blue magic ----
        Login(0);
        var b = cs[0];
        Check("login: venom shell (42) dropped, the gap compacted", "070C", SetSpells(b));
        Check("login: the validated set saved", "070C", DbSpells());
        Check("login: screwdriver's HP+10 / CHR+1 / VIT+1 in, sandspin's VIT+1", "10/1/2", $"{Mods(b)}/{b.ModStat[10]}");
        var ext = Of(0, 0x044);
        Check("game ok: one 0x044 (the WAR sub has none)", 1, ext.Count);
        Check("0x044 BLU: job 16, main, the set spells", "16:0:070C", ext.Count == 1 ? $"{ext[0].Buffer[4]}:{ext[0].Buffer[5]}:{Hex(ext[0].Buffer, 8, 20).TrimEnd('0')}" : "");
        Check("0x044 is 160 bytes", 160, ext.Count == 1 ? (ext[0].Buffer[1] >> 1) * 4 : 0);

        Clear();
        Ext(0, 5, 16, (2, 517 - 0x200));
        Check("metallic body into slot 2", "070C05", SetSpells(b));
        Check("the set: COMMAND_DATA, 0x044, CLISTATUS", "1/1/1", $"{Of(0, 0x0AC).Count}/{Of(0, 0x044).Count}/{Of(0, 0x061).Count}");
        Check("saved", "070C05", DbSpells());
        string BlueRecasts() => string.Join(",", new[] { 0x207, 0x20C, 0x205 }.Select(id => b.Recasts?.HasRecast(RecastType.Magic, (ushort)id, 0) ?? false));
        Check("every set spell on its 60 s recast (screwdriver, sandspin, metallic body)", "True,True,True", BlueRecasts());
        foreach (var id in new[] { 0x207, 0x20C, 0x205 }) b.Recasts.Del(RecastType.Magic, (ushort)id);

        Clear();
        Ext(0, 10, 16, (3, 522 - 0x200));
        Check("death ray (34) refused at 30", "True/070C05/0", $"{Logged("invalid level")}/{SetSpells(b)}/{ss[0].PacketList.Count}");
        Check("refused: no recasts", "False,False,False", BlueRecasts());
        Clear();
        Ext(0, 12, 16, (3, 524 - 0x200));
        Check("sandspin is set already: slot 3 stays empty", "070C05", SetSpells(b));
        Clear();
        Ext(0, 108, 16, (3, 620 - 0x200));
        Check("battle dance (not known) refused", "True/070C05", $"{Logged("they don't have")}/{SetSpells(b)}");

        Clear();
        Ext(0, 69, 16, (3, 581 - 0x200));
        Ext(0, 86, 16, (4, 598 - 0x200));
        Ext(0, 91, 16, (5, 603 - 0x200));
        Ext(0, 126, 16, (6, 638 - 0x200));
        Check("healing breeze, soporific, wild oats, feather storm: 20 of 20 points", "070C0545565B7E", SetSpells(b));
        Check("their mods", "30/6", Mods(b));
        Ext(0, 111, 16, (7, 623 - 0x200));
        Check("head butt (3 more points) refused", "070C0545565B7E", SetSpells(b));
        Clear();
        Ext(0, 37, 16, (10, 549 - 0x200));
        Check("slot 10 is past the 10 slots of BLU30", "True/070C0545565B7E", $"{Logged("beyond their slot count")}/{SetSpells(b)}");
        Ext(0, 111, 16, (6, 623 - 0x200));
        Check("head butt replaces feather storm (the slot's points do not count)", "070C0545565B6F", SetSpells(b));
        Check("feather storm's HP+5 / CHR+2 out", "25/4", Mods(b));

        Clear();
        Ext(0, 0, 16, (0, 519 - 0x200));
        Check("screwdriver removed (no compaction)", "000C0545565B6F", SetSpells(b));
        Check("its mods out", "15/3", Mods(b));
        Check("saved", "000C0545565B6F", DbSpells());
        ext = Of(0, 0x044);
        Check("the removal's 0x044", "000C0545565B6F", ext.Count == 1 ? Hex(ext[0].Buffer, 8, 20).TrimEnd('0') : "");
        Clear();
        Ext(0, 0, 16, (7, 549 - 0x200));
        Check("removing what slot 7 does not hold", "True/0", $"{Logged("don't have set")}/{ss[0].PacketList.Count}");
        Clear();
        Ext(0, 0, 16, (2, 0xFF));
        Check("an unknown spell id stops the removal", "True/000C0545565B6F", $"{Logged("Cannot resolve spell id 255")}/{SetSpells(b)}");

        Clear();
        Ext(0, 37, 18, (7, 549 - 0x200));
        Check("PUP data from a BLU/WAR: refused", "True/0", $"{Logged("Did not match any of the extended job system.")}/{ss[0].PacketList.Count}");
        Clear();
        b.CurrentEvent.EventId = 100;
        Ext(0, 0, 16, (1, 524 - 0x200));
        b.CurrentEvent.EventId = -1;
        Check("in an event: blocked", "000C0545565B6F/0", $"{SetSpells(b)}/{ss[0].PacketList.Count}");

        // the job change: WAR30 main (BLU15 swapped to the sub) drops the spells above 15 and compacts
        Clear();
        Send(0, 0x100, 8, buf => buf[4] = 1);
        Check("WAR/BLU15: healing breeze (16) and soporific (24) out, compacted", "0C055B6F", SetSpells(b));
        Check("saved", "0C055B6F", DbSpells());
        Check("their mods out", "10/1", Mods(b));
        ext = Of(0, 0x044);
        Check("the sub's 0x044 after COMMAND_DATA", "1:16:1:0C055B6F",
              ext.Count == 1 ? $"{ext.Count}:{ext[0].Buffer[4]}:{ext[0].Buffer[5]}:{Hex(ext[0].Buffer, 8, 20).TrimEnd('0')}" : ext.Count.ToString());
        Clear();
        Send(0, 0x100, 8, buf => buf[5] = 3);
        Check("WAR/WHM: every spell unequipped, the save kept", "/0C055B6F/0/0", $"{SetSpells(b)}/{DbSpells()}/{Mods(b)}");
        Check("no 0x044 for WAR/WHM", 0, Of(0, 0x044).Count);
        Clear();
        Send(0, 0x100, 8, buf => buf[4] = 16);
        Check("BLU/WHM: the saved set loaded", "0C055B6F/10/1", $"{SetSpells(b)}/{Mods(b)}");

        // ---- the automaton ----
        Login(1);
        var p = cs[1];
        var a = p.Automaton;
        string Equip() => $"{a.Head}:{a.Frame:X2}:{Hex(a.Attachments, 0, 12).TrimEnd('0')}";
        string Elements() => $"{Hex(a.ElementEquip, 0, 8)}/{Hex(a.ElementMax, 0, 8)}";
        string DbEquip() => Scalar(db, "SELECT HEX(equipped_attachments) FROM char_pet WHERE charid = ?", BoundValue.Of(pup)).TrimEnd('0');
        Check("login: Harlequin, the strobe on, Azure", "1:20:01/Azure/07B9", $"{Equip()}/{a.Name}/{a.ModelId:X4}");
        Check("login: elements (equip / max)", "0100000000000000/0505050505050303", Elements());
        Check("login: Harlequin 30's frame stats", "789/96/36,32,34,31,29,32,35", $"{a.MaxHp}/{a.MaxMp}/{string.Join(",", a.Stats)}");
        ext = Of(1, 0x044);
        Check("game ok: one 0x044 PUP", "1:18:0", ext.Count == 1 ? $"1:{ext[0].Buffer[4]}:{ext[0].Buffer[5]}" : ext.Count.ToString());

        Clear();
        Ext(1, 2, 18, (3, 2));
        Check("tension spring into slot 1", "1:20:0102/0200000000000000", $"{Equip()}/{Hex(a.ElementEquip, 0, 8)}");
        Check("saved", "20010102", DbEquip());
        Check("the change's 0x044 only", "1/1", $"{Of(1, 0x044).Count}/{ss[1].PacketList.Count}");
        Ext(1, 3, 18, (4, 3));
        Check("inhibitor (locked) refused", "1:20:0102", Equip());
        Ext(1, 1, 18, (4, 1));
        Check("the strobe is on already", "1:20:0102", Equip());
        Ext(1, 225, 18, (4, 225));
        Check("mana tank (dark 2 of 3) in, MP +1/20", "1:20:0102E1/100", $"{Equip()}/{a.MaxMp}");
        Ext(1, 235, 18, (5, 235));
        Check("mana tank IV (dark 5) over capacity", "1:20:0102E1", Equip());
        Ext(1, 193, 18, (5, 193));
        Check("auto-repair kit in, HP +1/20", "1:20:0102E1C1/828", $"{Equip()}/{a.MaxHp}");
        Ext(1, 235, 18, (2, 235));
        Check("mana tank IV over the strobe: refused, the strobe back", "1:20:0102E1C1/0200000000000202", $"{Equip()}/{Hex(a.ElementEquip, 0, 8)}");
        Ext(1, 0, 18, (3, 2));
        Check("tension spring removed", "1:20:0100E1C1/0100000000000202", $"{Equip()}/{Hex(a.ElementEquip, 0, 8)}");

        Ext(1, 2, 18, (0, 2));
        Check("Valoredge head", "2:20/07BA/0603050605030503", $"{a.Head}:{a.Frame:X2}/{a.ModelId:X4}/{Hex(a.ElementMax, 0, 8)}");
        Ext(1, 3, 18, (0, 3));
        Check("Sharpshot head (locked) refused", 2, a.Head);
        Ext(1, 0x21, 18, (1, 0x21));
        Check("Valoredge frame", "2:21/07BF/0702050705030502", $"{a.Head}:{a.Frame:X2}/{a.ModelId:X4}/{Hex(a.ElementMax, 0, 8)}");
        Check("Valoredge 30: HP 946 + 946/24, no MP (no mana tank boost)", "985/0/39,32,39,28,25,33,34", $"{a.MaxHp}/{a.MaxMp}/{string.Join(",", a.Stats)}");
        Check("saved", "21020100E1C1", DbEquip());

        ext = Of(1, 0x044);
        var e = ext.Count > 0 ? ext[^1].Buffer : new byte[160];
        int U16(int off) => BitConverter.ToUInt16(e, off);
        Check("0x044 PUP: head, frame, attachments", "2:21:0100E1C1", $"{e[8]}:{e[9]:X2}:{Hex(e, 10, 12).TrimEnd('0')}");
        Check("0x044 PUP: unlocks", $"4:2:{Hex(unlockBlob, 4, 32)}", $"{BitConverter.ToUInt32(e, 24)}:{BitConverter.ToUInt32(e, 28)}:{Hex(e, 56, 32)}");
        Check("0x044 PUP: name", "Azure", System.Text.Encoding.ASCII.GetString(e, 88, 16).TrimEnd('\0'));
        Check("0x044 PUP: HP / MP", "985:985:0:0", $"{U16(104)}:{U16(106)}:{U16(108)}:{U16(110)}");
        // Valoredge's melee B+ less the head's 2 (A+); no ranged or magic on the Valoredge frame
        int meleeCap = stats.Growth.GetMaxSkillByRank(1, 30), melee = Math.Min(meleeCap, p.WorkingSkills[22] & 0x7FFF);
        Check("0x044 PUP: skills", $"{melee}:{meleeCap}:0:0:0:0", $"{U16(112)}:{U16(114)}:{U16(116)}:{U16(118)}:{U16(120)}:{U16(122)}");
        Check("0x044 PUP: stats (no bonuses)", "39,0,32,0,39,0,28,0,25,0,33,0,34,0",
              string.Join(",", new[] { 128, 130, 132, 134, 136, 138, 140, 142, 144, 146, 148, 150, 152, 154 }.Select(U16)));
        Clear();
        Ext(1, 5, 16, (0, 5));
        Check("BLU data from a PUP/WAR: refused", "True/0", $"{Logged("Did not match any of the extended job system.")}/{ss[1].PacketList.Count}");

        // the job change reloads the automaton: PUP as the sub (15) keeps the parts, Harlequin's stats at 15 no more
        Clear();
        Send(1, 0x100, 8, buf => buf[4] = 1);
        ext = Of(1, 0x044);
        Check("WAR/PUP15: the sub's 0x044", "1:18:1:2:21", ext.Count == 1 ? $"1:{ext[0].Buffer[4]}:{ext[0].Buffer[5]}:{ext[0].Buffer[8]}:{ext[0].Buffer[9]:X2}" : ext.Count.ToString());
    }
}
