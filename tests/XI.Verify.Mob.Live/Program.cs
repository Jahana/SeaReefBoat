using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// XI.Verify.Mob.Live -- live gate for mobs (MobData: ecosystems.yaml / mobs.yaml / regions.yaml, MobStats: mobutils'
// ApplySpecies / InitializeMob / CalculateMobStats, ZoneHost.Mobs: InsertMobs, LoadMOBList, SpawnHandler, SpawnMOBs and
// the mob's 0x00E) in West Ronfaure against the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990951;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822, Bats = 17186837; // one spawn slot: a Wild Rabbit, Ding Bats at evening

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
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("mobverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Mobby"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR; // noon: the evening Ding Bats wait
        zone.MobRng = new Random(7);

        Console.WriteLine("load: InsertMobs and the first spawns");
        var info = zone.ZoneInfoOf(WestRonfaure);
        Check("mobs loaded", true, info.Mobs.Count > 100);
        Check("the load is logged", true, log.Exists(l => l.Contains("West_Ronfaure loaded") && l.Contains("mobs")));
        var rabbit = info.MobById[Rabbit];
        var bats = info.MobById[Bats];
        Check("rabbit name / packet name (display_name defaults to the key)", "Wild_Rabbit/Wild_Rabbit", $"{rabbit.Name}/{rabbit.PacketName}");
        Check("rabbit targid", 6, rabbit.TargId);
        Check("rabbit spawned at noon", true, rabbit.IsSpawned);
        Check("bats (at_evening) wait at noon", false, bats.IsSpawned);
        Check("one slot", true, rabbit.Slot != null && rabbit.Slot == bats.Slot);
        Check("rabbit status Update (allegiance mob)", NpcEntity.StatusUpdate, rabbit.Status);

        Console.WriteLine("ApplySpecies: rabbit (beast / rabbit / rabbit), template Wild_Rabbit");
        Check("ecosystem / family / species", "5/50/106", $"{rabbit.Ecosystem}/{rabbit.Family}/{rabbit.Species}");
        Check("element earth", 4, rabbit.Element);
        Check("ranks str/dex/vit/agi/int/mnd/chr", "4/3/4/4/4/4/4",
            $"{rabbit.StrRank}/{rabbit.DexRank}/{rabbit.VitRank}/{rabbit.AgiRank}/{rabbit.IntRank}/{rabbit.MndRank}/{rabbit.ChrRank}");
        Check("detects sight | scent", 0x101, rabbit.GetMobMod(MobMod.Detection));
        Check("charmable", 1, rabbit.GetMobMod(MobMod.Charmable));
        Check("resist fire / thunder rank", "-2/-3", $"{rabbit.GetMod(Mod.FIRE_RES_RANK)}/{rabbit.GetMod(Mod.THUNDER_RES_RANK)}");
        Check("speed / animation speed", "40/40", $"{rabbit.Speed}/{rabbit.AnimationSpeed}");
        Check("look standard 268", "0/268", $"{rabbit.Look[0]}/{rabbit.Look[1]}");
        Check("entity flags", 129u, rabbit.EntityFlags);
        Check("hitbox", 1.1f, rabbit.ModelHitboxSize);
        Check("respawn", 60u, rabbit.RespawnTimeSec);
        Check("pool / skill list", "4343/206", $"{rabbit.Pool}/{rabbit.MobSkillList}");
        Check("jobs WAR/WAR", "1/1", $"{rabbit.MJob}/{rabbit.SJob}");

        Console.WriteLine("InitializeMob: default mob mods, the beast's lizard killer, the family's roam mods");
        Check("link radius / sight / sound / magic range", "10/15/8/20",
            $"{rabbit.GetMobMod(MobMod.LinkRadius)}/{rabbit.GetMobMod(MobMod.SightRange)}/{rabbit.GetMobMod(MobMod.SoundRange)}/{rabbit.GetMobMod(MobMod.MagicRange)}");
        Check("skill list mob mod", 206, rabbit.GetMobMod(MobMod.SkillList));
        Check("lizard killer +5", 5, rabbit.GetMod(Mod.LIZARD_KILLER));
        Check("roam cool / rate / turns (family)", "35/35/3",
            $"{rabbit.GetMobMod(MobMod.RoamCool)}/{rabbit.GetMobMod(MobMod.RoamRate)}/{rabbit.GetMobMod(MobMod.RoamTurns)}");
        Check("roam distance default", 6, rabbit.GetMobMod(MobMod.RoamDistance));

        Console.WriteLine("CalculateMobStats at level 1 (WAR/WAR, HP grade B: 33)");
        Check("level 1/1", "1/1", $"{rabbit.MLvl}/{rabbit.SLvl}");
        Check("hp / max / mp", "33/33/0", $"{rabbit.Hp}/{rabbit.MaxHp}/{rabbit.MaxMp}");
        // family + WAR + sub: STR D 3 + A 5 + A 2 (floor raised to 2), VIT D 3 + D 3 + D 1, INT D 3 + F 2 + F 1
        Check("STR..CHR", string.Join("/", new[] { 10, 10, 7, 9, 6, 6, 7 }), string.Join("/", rabbit.Stats));
        Check("weapon damage = level", "1/1", $"{rabbit.MainDamage}/{rabbit.RangedDamage}");
        // Ronfaure (region 0) offsets 2/4, one less outside an NM in West Ronfaure
        Check("damage offsets", "1/3", $"{rabbit.GetMobMod(MobMod.DamageOffset)}/{rabbit.GetMobMod(MobMod.RangedDamageOffset)}");
        Check("delay 240", "240/240", $"{rabbit.MainDelay}/{rabbit.BaseDelay}");
        Check("DEF / EVA (rank C at 1: 5)", "5/5", $"{rabbit.GetMod(Mod.DEF)}/{rabbit.GetMod(Mod.EVA)}");
        Check("ATT / ACC / RATT / RACC (A+ great axe at 1)", "6/6/6/6",
            $"{rabbit.GetMod(Mod.ATT)}/{rabbit.GetMod(Mod.ACC)}/{rabbit.GetMod(Mod.RATT)}/{rabbit.GetMod(Mod.RACC)}");
        Check("tp 0", 0, rabbit.Tp);

        Console.WriteLine("the gil purse: CanStealGil and ResetGilPurse at the spawn");
        Check("a rabbit drops no gil: no purse", 0, rabbit.GetMobMod(MobMod.MugGil));
        var beastman = info.Mobs.Find(m => m.Ecosystem == Ecosystem.Beastmen && m.IsSpawned && m.GetMobMod(MobMod.GilMax) == 0);
        Check("a beastman (GilBonus 100): a purse of a random drop cut by 4 to 6", true,
            beastman != null && beastman.GetMobMod(MobMod.MugGil) > 0 && beastman.GetMobMod(MobMod.MugGil) <= 2 * beastman.MLvl + 40);

        Console.WriteLine("Spawn: a point in the spawn's region");
        var n6 = info.Regions["n_6"];
        Check("spawn region n_6", "n_6", rabbit.RoamRegion?.Name);
        Check("position inside n_6", true, n6.Contains(rabbit.X, rabbit.Z));
        // the end of Spawn starts a roam and takes its first step (speed 40: 40 / 17 * 0.4 = 0.94 yalms a tick)
        Check("one roam step from the spawn point", true,
            MathF.Sqrt((rabbit.X - rabbit.SpawnX) * (rabbit.X - rabbit.SpawnX) + (rabbit.Z - rabbit.SpawnZ) * (rabbit.Z - rabbit.SpawnZ)) <= 0.95f);
        Check("spawn point inside n_6", true, n6.Contains(rabbit.SpawnX, rabbit.SpawnZ));
        var points = new HashSet<(float, float)>();
        bool allInside = true;
        for (int k = 0; k < 50; k++)
        {
            var p = n6.SamplePoint(zone.MobRng);
            allInside &= n6.Contains(p.X, p.Z);
            points.Add((p.X, p.Z));
        }
        Check("50 samples all inside", true, allInside);
        Check("samples spread", true, points.Count > 40);

        Console.WriteLine("SpawnMOBs: a char's spawn list follows its 0x015");
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        void Pos(float x, float y, float z)
        {
            s.PacketList.Clear();
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
            });
        }
        BasicPacket MobPacket() => new List<BasicPacket>(s.PacketList).Find(p => Opcode(p) == 0x00E && BitConverter.ToUInt32(p.Buffer, 4) == Rabbit);

        Pos(rabbit.X + 5, rabbit.Y, rabbit.Z);
        var e = MobPacket();
        Check("rabbit in the spawn list", true, c.SpawnMobList.Contains(Rabbit));
        Check("0x00E for the rabbit", true, e != null);
        if (e != null)
        {
            var b = e.Buffer;
            Check("targid", 6, BitConverter.ToUInt16(b, 8));
            Check("mask UPDATE_ALL_MOB", 0x0F, b[0x0A]);
            Check("x / y / z", $"{rabbit.X}/{rabbit.Y}/{rabbit.Z}",
                $"{BitConverter.ToSingle(b, 0x0C)}/{BitConverter.ToSingle(b, 0x10)}/{BitConverter.ToSingle(b, 0x14)}");
            Check("speed / animation speed", "40/40", $"{b[0x1C]}/{b[0x1D]}");
            Check("hpp 100", 100, b[0x1E]);
            Check("status 1", 1, b[0x20]);
            Check("flags", 129u, BitConverter.ToUInt32(b, 0x21));
            Check("alive bit", 0x08, b[0x25]);
            Check("not triggerable while Update", 0, b[0x28] & 0x40);
            Check("model", 268, BitConverter.ToUInt16(b, 0x32));
            Check("name", "Wild_Rabbit", System.Text.Encoding.ASCII.GetString(b, 0x34, 16).TrimEnd('\0'));
            Check("size 0x48", 0x48, (b[1] >> 1) * 4);
        }
        Pos(rabbit.X + 5, rabbit.Y, rabbit.Z);
        Check("no second spawn while in view", null, MobPacket());

        // 20 yalms above the char's eyes is the limit
        Pos(rabbit.X, rabbit.Y - 21.0f, rabbit.Z);
        e = MobPacket();
        Check("out of view vertically: despawn", "48", e?.Buffer[0x0A].ToString());
        Check("dropped from the list", false, c.SpawnMobList.Contains(Rabbit));
        Pos(rabbit.X + 40, rabbit.Y, rabbit.Z);
        Check("back within 50: spawn", true, MobPacket() != null);
        Pos(rabbit.X + 60, rabbit.Y, rabbit.Z);
        Check("past 50: despawn", "48", MobPacket()?.Buffer[0x0A].ToString());
        Pos(rabbit.X + 1, rabbit.Y, rabbit.Z);

        Console.WriteLine("CHARREQ2: a mob asked for by act index or unique number gets an UPDATE_ALL_MOB 0x00E");
        int MobUpdates() => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == 0x00E).Count;
        s.PacketList.Clear();
        Send(0x017, 20, buf => BitConverter.GetBytes((ushort)6).CopyTo(buf, 4));
        Check("by act index", "15", MobPacket()?.Buffer[0x0A].ToString());
        s.PacketList.Clear();
        Send(0x017, 20, buf => BitConverter.GetBytes(Rabbit).CopyTo(buf, 8));
        Check("by unique number", "15", MobPacket()?.Buffer[0x0A].ToString());
        s.PacketList.Clear();
        Send(0x017, 20, buf => { BitConverter.GetBytes((ushort)6).CopyTo(buf, 4); BitConverter.GetBytes(Rabbit).CopyTo(buf, 8); });
        Check("the same mob once", 1, MobUpdates());
        // the act index matches but the id names another zone's mob: GetEntity's id check refuses it
        s.PacketList.Clear();
        Send(0x017, 20, buf => BitConverter.GetBytes(Rabbit + 0x1000).CopyTo(buf, 8));
        Check("an id that is not the mob's: nothing", 0, MobUpdates());

        Console.WriteLine("PostTick: a pending update flushes to the chars that see the mob, at most every 250 ms");
        // roaming held still (its steps would add UPDATE_POS): no path, the next idle action RoamCool away, and the
        // spawn's first step (UPDATE_POS, not yet flushed: no tick has run) dropped
        rabbit.PathFind?.Clear();
        rabbit.LastActionMs = now;
        rabbit.UpdateMask = 0;
        s.PacketList.Clear();
        rabbit.Hp = 10;
        rabbit.MobHealAtMs = now; // just rested: the idle rest (10% every 10 s) stays out of the way
        rabbit.UpdateMask |= EntityUpdatePacket.UpdateHp;
        zone.Tick();
        e = MobPacket();
        Check("an UPDATE_HP 0x00E", "4/30", e == null ? "" : $"{e.Buffer[0x0A]}/{e.Buffer[0x1E]}");
        Check("mask cleared", 0, rabbit.UpdateMask);
        s.PacketList.Clear();
        rabbit.UpdateMask |= EntityUpdatePacket.UpdateHp;
        now += 100;
        zone.Tick();
        Check("held within 250 ms", null, MobPacket());
        now += 200;
        zone.Tick();
        Check("sent after 250 ms", true, MobPacket() != null);
        rabbit.Hp = rabbit.MaxHp;

        Console.WriteLine("charreq (0x016): a mob is answered with a spawn");
        s.PacketList.Clear();
        Send(0x016, 8, b => BitConverter.GetBytes((ushort)6).CopyTo(b, 4));
        Check("0x00E spawn", "15", MobPacket()?.Buffer[0x0A].ToString());

        Console.WriteLine("/check (0x0DD) on a mob");
        string CheckMob(uint uniqueNo)
        {
            s.PacketList.Clear();
            Send(0x0DD, 16, b => { BitConverter.GetBytes(uniqueNo).CopyTo(b, 4); BitConverter.GetBytes((uint)rabbit.TargId).CopyTo(b, 8); b[12] = 0; });
            var m = new List<BasicPacket>(s.PacketList).Find(p => Opcode(p) == 0x029);
            return m == null ? "none" : $"{BitConverter.ToUInt32(m.Buffer, 8) == Rabbit}/{BitConverter.ToUInt32(m.Buffer, 12)}/{BitConverter.ToUInt32(m.Buffer, 16)}/{BitConverter.ToUInt16(m.Buffer, 24)}";
        }
        // a level 30 WAR on a level 1 rabbit: Too Weak (64), low defence (+1) and low evasion (+3) on the default 174
        Check("the level, Too Weak and the low defence / low evasion message", "True/1/64/178", CheckMob(Rabbit));
        Check("a UniqueNo that isn't the mob's: no answer", "none", CheckMob(Rabbit + 1));
        rabbit.SetMobMod(MobMod.CheckAsNm, 1);
        Check("CHECK_AS_NM: impossible to gauge", "True/0/0/249", CheckMob(Rabbit));
        rabbit.SetMobMod(MobMod.CheckAsNm, 0);
        rabbit.Type |= MobType.Notorious;
        Check("an NM: impossible to gauge", "True/0/0/249", CheckMob(Rabbit));
        rabbit.Type &= unchecked((ushort)~MobType.Notorious);
        float cx = c.X;
        c.X = rabbit.X + 60;
        log.Clear();
        Check("from 60 yalms: still checked", "True/1/64/178", CheckMob(Rabbit));
        Check("and logged", true, log.Exists(l => l.Contains("checking mob Wild_Rabbit from too far away")));
        c.X = cx;

        Console.WriteLine("despawn and respawn: CDespawnState registers the slot, the handler respawns it");
        rabbit.DespawnAtMs = now + 1;
        now += 10;
        zone.Tick();
        Check("fading", true, rabbit.FadeAtMs > 0 && rabbit.IsSpawned);
        Check("slot registered", 60000L, info.Spawner.RemainingRespawnMs(rabbit, now));
        now += 3000;
        zone.Tick();
        Check("faded out", false, rabbit.IsSpawned);
        Pos(rabbit.X + 1, rabbit.Y, rabbit.Z);
        Check("despawn 0x00E on the next 0x015", "48", MobPacket()?.Buffer[0x0A].ToString());
        // the handler runs every 30 s and spawns what is due within 15 s
        int ticks = 0;
        while (!rabbit.IsSpawned && ticks < 10)
        {
            now += 10_000;
            zone.Tick();
            ticks++;
        }
        Check("respawned", true, rabbit.IsSpawned);
        Check("at the 60 s mark (due within the window)", true, ticks >= 5 && ticks <= 7);
        Check("the bats still wait", false, bats.IsSpawned);
        Check("level / hp fresh", "1/33", $"{rabbit.MLvl}/{rabbit.Hp}");
        Check("a new point in n_6", true, n6.Contains(rabbit.X, rabbit.Z));
        Pos(rabbit.X + 1, rabbit.Y, rabbit.Z);
        Check("spawn 0x00E", "15", MobPacket()?.Buffer[0x0A].ToString());

        Console.WriteLine("the slot at 21:00: either member may come back, never both");
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 21 * VanaTime.HOUR;
        rabbit.DespawnAtMs = now + 1;
        now += 3100;
        zone.Tick();
        now += 3100;
        zone.Tick();
        Check("rabbit gone", false, rabbit.IsSpawned);
        for (ticks = 0; !rabbit.IsSpawned && !bats.IsSpawned && ticks < 10; ticks++)
        {
            now += 10_000;
            zone.Tick();
        }
        Check("one member up", true, rabbit.IsSpawned ^ bats.IsSpawned);
        var up = rabbit.IsSpawned ? rabbit : bats;
        Check("in n_6", true, n6.Contains(up.X, up.Z));
        // onGameHour: the bats leave once their window closes
        if (bats.IsSpawned)
        {
            zone.VanaTicksNow = () => 8001 * VanaTime.DAY + 7 * VanaTime.HOUR;
            now += 1000;
            zone.Tick();
            now += 100;
            zone.Tick();
            Check("bats fading", true, bats.FadeAtMs > 0);
            now += 3100;
            zone.Tick();
            Check("bats despawn at 7:00", false, bats.IsSpawned);
        }

        Console.WriteLine("every zone's loot parses, and names items item_basic knows");
        {
            var names = new HashSet<string> { "nothing" };
            var r = Sql(db, "SELECT name FROM item_basic");
            while (r != null && r.Next()) names.Add(r.Get<string>(0u));
            var enums = new DataEnums(dataDir);
            int files = 0, templates = 0, rolls = 0, groups = 0;
            var bad = new List<string>();
            var unknown = new SortedSet<string>();
            foreach (var dir in Directory.GetDirectories(Path.Combine(dataDir, "zones")))
            {
                var path = Path.Combine(dir, "mobs.yaml");
                if (!File.Exists(path)) continue;
                files++;
                try
                {
                    foreach (var t in MobsFile.Load(path, enums).Templates.Values)
                    {
                        templates++;
                        foreach (var d in t.Loot.Drops)
                        {
                            rolls++;
                            if (d.OneOf.Count > 0) groups++;
                            if (d.OneOf.Count == 0 && !names.Contains(d.Item)) unknown.Add(d.Item);
                            foreach (var (n, _) in d.OneOf) if (!names.Contains(n)) unknown.Add(n);
                        }
                        foreach (var n in t.Loot.Steal) if (!names.Contains(n)) unknown.Add(n);
                        foreach (var (n, _) in t.Loot.Despoil) if (!names.Contains(n)) unknown.Add(n);
                    }
                }
                catch (Exception ex) { bad.Add($"{Path.GetFileName(dir)}: {ex.Message}"); }
            }
            Console.WriteLine($"  ({files} files, {templates} templates, {rolls} drop rolls, {groups} one_of)");
            Check("every mobs.yaml loads", "", string.Join("; ", bad));
            Check("every loot name resolves", "", string.Join(", ", unknown));
            Check("one_of rolls exist", true, groups > 0);
        }

        Console.WriteLine("main.NORMAL_MOB_MAX_LEVEL_RANGE_MIN / MAX: normal mobs' levels capped at load, NMs kept");
        {
            var cappedStats = new CharStats(GrowthTables.Load(db), new ItemData(db),
                new StatSettings { NormalMobMaxLevelRangeMin = 2, NormalMobMaxLevelRangeMax = 3 });
            var capped = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), cappedStats), dataDir, true, _ => { });
            capped.NowMs = () => now;
            capped.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
            var cappedInfo = capped.ZoneInfoOf(WestRonfaure);
            var normal = info.Mobs.Find(m => !m.IsNotorious && m.MinLevel > 3);
            var nm = info.Mobs.Find(m => m.IsNotorious && m.MaxLevel > 3);
            Check("a normal mob: min 2, max 3", "2/3",
                normal == null ? "none" : $"{cappedInfo.MobById[normal.Id].MinLevel}/{cappedInfo.MobById[normal.Id].MaxLevel}");
            Check("an NM keeps its levels", nm == null ? "none" : $"{nm.MinLevel}/{nm.MaxLevel}",
                nm == null ? "none" : $"{cappedInfo.MobById[nm.Id].MinLevel}/{cappedInfo.MobById[nm.Id].MaxLevel}");
        }

        Console.WriteLine("SpawnHandler helpers");
        Check("window 18-6 at 20", true, MobSpawnHandler.HourInWindow(20, 18, 6));
        Check("window 18-6 at 12", false, MobSpawnHandler.HourInWindow(12, 18, 6));
        Check("window 6-18 at 18", false, MobSpawnHandler.HourInWindow(18, 6, 18));
        Check("bats window (its own 20-6)", "(20, 6)", MobSpawnHandler.SpawnWindowOf(bats).ToString());
        Check("weather element: rain is water", 6, MobSpawnHandler.WeatherElement(6));

        Console.WriteLine("MobStats formulas");
        Check("base HP 75 B", true, MobStats.CalculateBaseMobHP(75, 33, 8, 1) > 1000);
        Check("sub HP below 25", 0u, MobStats.CalculateSubjobHP(24, 8, 1));
        Check("GetBaseToRank A at 75", 42, MobStats.GetBaseToRank(1, 75));
        Check("sub job zone 100", true, MobStats.CheckSubJobZone(100));
        Check("sub job zone 230", false, MobStats.CheckSubJobZone(230));
    }
}
