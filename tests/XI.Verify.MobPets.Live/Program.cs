using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.MobPets.Live -- live gate for mob pets (ZoneHost.MobPets.cs): a mob linked to another by setPet (as
// xi.pet.setMobPet does), spawnPet beside the master at 30% HP, hasPet / getPetName / getPetID, the pet engaging with
// its master and coming back after petRetreat, the master answering its pet, the pet leaving with its despawned
// master, SpawnMobPet's pet_list row (an Ifrit's name, species, skill list and fire resistance) and setPetStats keeping
// the name, an SMN master's pet dying with it, and a BST master's Call Beast (the special skill) spawning the pet from
// its idle roam. A Forest Funguar of West Ronfaure as the master, a Wild Rabbit as the pet, one WAR 75, with the real
// MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993700;
    private const ushort WestRonfaure = 100;
    private const uint Funguar = 17186979, OtherFunguar = 17186980, Rabbit = 17186822;
    private const ushort CallBeast = 1017;

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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("mpet" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Petty"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of(0.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(5);
        var queue = new Queue<IIpcMessage>();
        zone.SendIpc = m => queue.Enqueue(m);
        void Pump()
        {
            for (int round = 0; round < 8; round++)
            {
                while (queue.Count > 0) zone.OnIpc(IpcFraming.Decode(IpcFraming.ToBytesWithHeader(queue.Dequeue())));
                zone.Tick();
                if (queue.Count == 0) return;
            }
        }
        void Tick(int ticks = 1)
        {
            for (int t = 0; t < ticks; t++)
            {
                now += 400;
                zone.Tick();
            }
        }
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local mob = GetMobByID({Funguar})\nlocal pet = GetMobByID({Rabbit})\n" + code, "=verify_mobpets");
            return string.Join(",", got);
        }

        var info = zone.ZoneInfoOf(WestRonfaure);
        var master = info.MobById[Funguar];
        var other = info.MobById[OtherFunguar];
        var pet = info.MobById[Rabbit];

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
        Pump();
        Check("the funguar and the rabbit up", "True/True", $"{master.IsSpawned}/{pet.IsSpawned}");

        // the master 3 east of the char facing west, held still; its party mate and everything else out of the way
        void Place()
        {
            foreach (var m in new[] { master, pet })
            {
                m.Engaged = false;
                m.BattleTarget = null;
                m.Animation = Anim.None;
                m.Neutral = false;
                m.NeutralAtMs = now - 60000;
                m.Enmity.Clear();
                m.OwnerId = 0;
                m.Hp = m.MaxHp;
                m.PathFind?.Clear();
                m.LastActionMs = now;
            }
            master.X = c.X + 3; master.Y = c.Y; master.Z = c.Z; master.Rotation = 128;
            other.X = 500; other.Z = 500;
        }

        Console.WriteLine("setPet and spawnPet: the pet beside its master at 30% HP");
        pet.MinLevel = pet.MaxLevel = 5;
        // a real pet spawns at a fixed point (Spawn would sample a region's point over the one beside its master)
        pet.Regions.Clear();
        Lua($"DespawnMob({Rabbit})");
        Tick(10);
        Lua($"SpawnMob({Rabbit})");
        int soloHp = pet.MaxHp;
        Lua($"DisallowRespawn({Rabbit}, true) mob:setPet(pet)");
        Check("hasPet (the rabbit up), getPetName, getPetID (no player pet)", "true/Wild_Rabbit/0",
            Lua("report(tostring(mob:hasPet()) .. '/' .. mob:getPetName() .. '/' .. mob:getPetID())"));
        Place();
        Lua("mob:spawnPet()");
        float dx = pet.X - master.X, dz = pet.Z - master.Z;
        Check("spawned 2.2 behind its master (who faces west: so east of it), facing the same way", "2.2/True/128",
            $"{MathF.Sqrt(dx * dx + dz * dz):0.0}/{dx > 2.1f}/{pet.Rotation}");
        Check("level 5 alone, then 30% of that HP as a pet", true, pet.MLvl == 5 && soloHp > 50 && Math.Abs(pet.MaxHp - (int)(soloHp * 0.30f)) <= 1);

        Console.WriteLine("The pet fights with its master");
        Place();
        master.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick();
        Check("the master engages the char, the pet the same target", "True/True/True",
            $"{master.Engaged && master.BattleTarget == c}/{pet.Engaged}/{pet.BattleTarget == c}");
        Lua("mob:petRetreat()");
        Check("petRetreat: the pet disengages", false, pet.Engaged);
        Tick(2);
        Check("the master's next combat tick calls it back", true, pet.Engaged && pet.BattleTarget == c);

        Console.WriteLine("The master's pet mods reach the pet out");
        short att = pet.GetMod(Mod.ATT);
        Lua("mob:addPetMod(xi.mod.ATT, 50)");
        short added = pet.GetMod(Mod.ATT);
        Lua("mob:delPetMod(xi.mod.ATT, 20)");
        short less = pet.GetMod(Mod.ATT);
        Lua("mob:setPetMod(xi.mod.ATT, 7)");
        Check("addPetMod +50, delPetMod -20, setPetMod 7; the master keeps 7", "50/30/7/7",
            $"{added - att}/{less - att}/{pet.GetMod(Mod.ATT)}/{master.PetMods[(0, Mod.ATT)]}");
        Lua("mob:setPetMod(xi.mod.ATT, 0)");

        Console.WriteLine("The master answers its pet");
        Place();
        pet.X = master.X + 2; pet.Z = master.Z; pet.Rotation = 128;
        pet.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick(3);
        Check("the pet engages, its master joins in", "True/True", $"{pet.Engaged}/{master.Engaged && master.BattleTarget == c}");

        Console.WriteLine("An idle pet leaves with its despawned master");
        Place();
        Lua($"DespawnMob({Funguar})");
        Tick(20);
        Check("master gone, then the pet", "False/False", $"{master.IsSpawned}/{pet.IsSpawned}");
        Check("hasPet: a despawned pet is no pet", "false", Lua("report(tostring(mob:hasPet()))"));

        Console.WriteLine("SpawnMobPet: an Ifrit from pet_list; setPetStats keeps the name");
        Lua($"SpawnMob({Funguar})");
        Place();
        Lua("mob:spawnPet(xi.petId.IFRIT)");
        Check("the pet is Ifrit: species 248, skill list 715, model, fire resistance kept over the spawn", true,
            pet.Name == "Ifrit" && pet.Species == 248 && pet.GetMobMod(MobMod.SkillList) == 715 && pet.Look[1] != 0 && pet.IsSpawned);
        Check("Ifrit's resistance ranks from mob_resistances (fire 11, water -3)", "11/-3",
            $"{pet.GetMod(Mod.FIRE_RES_RANK)}/{pet.GetMod(Mod.WATER_RES_RANK)}");
        Lua("pet:setPetStats(xi.petId.WATER_SPIRIT)");
        // the elemental's -50% physical damage is set before CalculateMobStats, whose restore drops it (as in C++)
        Check("setPetStats: still named Ifrit, now an elemental with the water spirit's ranks", "Ifrit/10/11/0",
            $"{pet.Name}/{pet.Ecosystem}/{pet.GetMod(Mod.WATER_RES_RANK)}/{pet.GetMod(Mod.DMGPHYS)}");
        Lua("mob:despawnPet()");
        Check("despawnPet on a mob master: refused (DetachPet takes a PC)", true, log.Exists(l => l.Contains("Non-PC passed into function")));

        Console.WriteLine("An SMN master's pet dies with it");
        Place();
        master.MJob = 15;
        master.Hp = 0;
        Tick();
        Check("both dead", "True/True", $"{master.IsDead}/{pet.IsDead}");
        Tick(60);

        Console.WriteLine("A BST master's Call Beast: the special skill spawns the pet from the idle roam");
        Lua($"SpawnMob({Funguar})");
        Check("master up, pet down", "True/False", $"{master.IsSpawned}/{pet.IsSpawned}");
        Place();
        // at home, so the idle action is not the walk back
        master.X = master.SpawnX; master.Y = master.SpawnY; master.Z = master.SpawnZ;
        master.MJob = 9;
        master.SetMobMod(MobMod.SpecialSkill, (short)CallBeast);
        master.SetMobMod(MobMod.SpecialCool, 0);
        master.LastActionMs = now - 600_000;
        bool called = false;
        for (int t = 0; t < 20 && !pet.IsSpawned; t++)
        {
            Tick();
            called |= master.SkillState != null;
        }
        Check("call beast readied, then the pet spawned beside its master", "True/True", $"{called}/{pet.IsSpawned}");
        Check("with the pet up, call beast's check refuses another", "1",
            Lua($"report(xi.pet.onMobSkillCheck(mob, mob, nil))"));

        Console.WriteLine("A char's gear gives its pet mods (item_mods_pet)");
        const ushort FidelityMantle = 11531;
        Lua($"GetPlayerByID({CharId}):addItem({FidelityMantle})");
        Lua($"GetPlayerByID({CharId}):equipItem({FidelityMantle})");
        string mantle = $"{c.PetMods.GetValueOrDefault(((byte)0, (Mod)73))}";
        Lua($"GetPlayerByID({CharId}):unequipItem(xi.slot.BACK)");
        Check("fidelity mantle on: +3 (mod 73, every pet), off: 0", "3/0", $"{mantle}/{c.PetMods.GetValueOrDefault(((byte)0, (Mod)73))}");

        Check("no script errors", 0, log.FindAll(x => x.Contains("rror") && !x.Contains("0 load errors")).Count);
        foreach (var x in log.FindAll(x => x.Contains("rror") && !x.Contains("0 load errors"))) Console.WriteLine("  " + x);
    }
}
