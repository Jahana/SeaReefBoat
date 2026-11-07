using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Fishing.Live -- live gate for fishing (ZoneHost.Fishing: ACTION Fish casting, c2s 0x110 FISHING_2's
// hook check / mini-game end / release, s2c 0x115 FISH, 0x027 TALKNUMWORK2 and 0x038 SCHEDULOR) against the real
// MariaDB (xidb) fishing tables, in Phanauet Channel (zone 1, one fishing area over the whole zone); a fishing mob
// (fishing_mob) hooked and reeled in, the fishing moghancement, and the Brigand's Chart pugil and Jade Etui chests in
// Buburimu Peninsula (zone 118).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // an angler with a willow fishing rod and twelve little worms, one with the rod only, one with neither; a second
    // angler for the fishing mobs, and one in Buburimu Peninsula for the Brigand's Chart
    private static readonly (uint Id, string Name, ushort Zone)[] Chars =
        { (990981, "Angler", 1), (990982, "Rodonly", 1), (990983, "Barehand", 1), (995801, "Mobangler", 1), (995802, "Brigand", 118) };

    private const ushort WillowRod = 17391, LittleWorm = 17396, Lungfish = 4315;
    private const int MsgOffset = 7242; // Phanauet Channel's FISHING_MESSAGE_OFFSET

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
        foreach (var (id, _, _) in Chars)
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

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var cfg = new StatSettings { FishingEnable = true };
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), cfg);
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        var sent = new List<IIpcMessage>();
        zone.SendIpc = sent.Add;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        long now = 1_800_000_000;
        double roll = 0;
        zone.UnixNow = () => now;
        zone.Random01 = () => roll;
        long ticks = zone.VanaTicksNow();
        zone.VanaTicksNow = () => ticks;

        foreach (var (id, name, zoneId) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("fi" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,moghouse,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(zoneId), BoundValue.Of((ushort)0),
                BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 30, 0, 0)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        }
        void Give(uint id, byte slot, ushort item, uint qty, byte equipSlot)
        {
            Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity) VALUES (?, 0, ?, ?, ?)",
                BoundValue.Of(id), BoundValue.Of(slot), BoundValue.Of(item), BoundValue.Of(qty));
            Sql(db, "INSERT INTO char_equip(charid, slotid, equipslotid, containerid) VALUES (?, ?, ?, 0)",
                BoundValue.Of(id), BoundValue.Of(slot), BoundValue.Of(equipSlot));
        }
        Give(Chars[0].Id, 1, WillowRod, 1, 2);
        Give(Chars[0].Id, 2, LittleWorm, 12, 3);
        Give(Chars[1].Id, 1, WillowRod, 1, 2);
        Give(Chars[3].Id, 1, WillowRod, 1, 2);
        Give(Chars[3].Id, 2, LittleWorm, 12, 3);
        Give(Chars[4].Id, 1, WillowRod, 1, 2);
        Give(Chars[4].Id, 2, LittleWorm, 12, 3);

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
        void Cast(int i) => Send(i, 0x01A, 28, buf =>
        {
            BitConverter.GetBytes(cs[i].Id).CopyTo(buf, C2sAction.OffUniqueNo);
            BitConverter.GetBytes(cs[i].TargId).CopyTo(buf, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Fish).CopyTo(buf, C2sAction.OffActionID);
        });
        // GP_CLI_COMMAND_FISHING_2: UniqueNo @4, para @8, ActIndex @12, mode @14, para2 @16
        void Fish(int i, byte mode, int para = 0, uint para2 = 0, uint? uniqueNo = null) => Send(i, 0x110, 20, buf =>
        {
            BitConverter.GetBytes(uniqueNo ?? cs[i].Id).CopyTo(buf, 4);
            BitConverter.GetBytes(para).CopyTo(buf, 8);
            BitConverter.GetBytes(cs[i].TargId).CopyTo(buf, 12);
            buf[14] = mode;
            BitConverter.GetBytes(para2).CopyTo(buf, 16);
        });
        // TALKNUM (0x036) message numbers, less the fishing offset and the char flag
        string Talks(int i, int offset = MsgOffset) => string.Join(",", Of(i, 0x036).ConvertAll(p => (BitConverter.ToUInt16(p.Buffer, 10) - 0x8000 - offset).ToString("X")));
        string Worms() => Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND itemId = ?", BoundValue.Of(Chars[0].Id), BoundValue.Of(LittleWorm)) ?? "none";
        string FishSkill() => Scalar(db, "SELECT value FROM char_skills WHERE charid = ? AND skillid = 48", BoundValue.Of(Chars[0].Id)) ?? "none";

        Login(0);
        var a = cs[0];
        Check("login: rod and bait equipped", $"{WillowRod}/{LittleWorm}", $"{a.Equipped[2]?.ItemId}/{a.Equipped[3]?.ItemId}");

        // ---- the cast, a hook check too early, the release ----
        const ushort Invisible = 69;
        a.StatusEffects.AddStatusEffect(new StatusEffect(Invisible, Invisible, 0, 0, 300_000));
        Clear();
        Cast(0);
        Check("cast: NewFishingStart, a 7-13 hook delay", "56/True", $"{(int)a.Animation}/{a.HookDelay >= 7 && a.HookDelay <= 13}");
        Check("cast: invisible dropped (charutils::RemoveInvisible)", false, a.StatusEffects.HasStatusEffect(Invisible));
        Check("cast: [Fish]LastCastTime saved", now.ToString(), Scalar(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = '[Fish]LastCastTime'", BoundValue.Of(a.Id)));
        Check("cast: a token", true, a.FishingToken >= 1 && a.FishingToken <= 10000);
        Clear();
        Cast(0);
        Check("casting while fishing is blocked", "0/56", $"{Of(0, 0x036).Count}/{(int)a.Animation}");
        Clear();
        Fish(0, 2);
        Check("hook check before the delay: no catch", "62/4", $"{(int)a.Animation}/{Talks(0)}");
        Fish(0, 4);
        Check("release: back to no animation", 0, (int)a.Animation);
        Clear();
        Cast(0);
        Check("recast within 5 seconds: cannot fish at the moment", "3/1/0", $"{Talks(0)}/{Of(0, 0x052).Count}/{(int)a.Animation}");

        // ---- a lungfish: skill 0 against its 32, so a bad feeling (the line can snap at rank 10 of the rod's 5) ----
        now += 6;
        Clear();
        Cast(0);
        now += 20;
        Clear();
        Fish(0, 2);
        var f = Of(0, 0x115);
        Check("hook: one 0x115", 1, f.Count);
        var fb = f.Count > 0 ? f[0].Buffer : new byte[24];
        ushort F(int off) => BitConverter.ToUInt16(fb, off);
        Check("0x115: stamina 95*34, delay 4+2, regen 128+5, move 8+1, attack 16*1.5*20, heal 120, time 30, small",
              "3230/6/133/9/480/120/30/0", $"{F(4)}/{F(6)}/{F(8)}/{F(10)}/{F(12)}/{F(14)}/{F(16)}/{fb[18]}");
        uint special = BitConverter.ToUInt32(fb, 20);
        Check("0x115: the lucky timing is the hooked fish's", a.HookedFish?.Special, special);
        Check("hook: hooked small, bad feeling", "8,2A", Talks(0));
        var sch = Of(0, 0x038);
        Check("hook: SCHEDULOR sweating on self", $"1:{a.Id}:{(uint)FourCC.Sweating:X8}:{a.TargId}",
              sch.Count == 1 ? $"1:{BitConverter.ToUInt32(sch[0].Buffer, 4)}:{BitConverter.ToUInt32(sch[0].Buffer, 12):X8}:{BitConverter.ToUInt16(sch[0].Buffer, 16)}" : sch.Count.ToString());
        Check("hook: NewFishingFish, the lungfish hooked", $"57/{Lungfish}/42/6", $"{(int)a.Animation}/{a.HookedFish?.CatchId}/{a.HookedFish?.Snap.Chance}/{a.HookedFish?.Break.Chance}");

        Clear();
        Fish(0, 3, 0, special + 1);
        Check("a special that is not the hooked fish's is refused", "True/57", $"{Logged("para2 not equal")}/{(int)a.Animation}");

        roll = 0.99;
        Clear();
        Fish(0, 3, 0, special);
        Check("reeled in: NewFishingCaught", 58, (int)a.Animation);
        Check("the lungfish in the inventory", "1", Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND itemId = ?", BoundValue.Of(a.Id), BoundValue.Of(Lungfish)));
        var caught = Of(0, 0x027);
        Check("0x027: caught a lungfish", $"1:{Lungfish}:1:27:Angler",
              caught.Count == 1 ? $"1:{BitConverter.ToUInt32(caught[0].Buffer, 16)}:{BitConverter.ToUInt32(caught[0].Buffer, 20)}:" +
                                  $"{BitConverter.ToUInt16(caught[0].Buffer, 10) - 0x8000 - MsgOffset:X}:{System.Text.Encoding.ASCII.GetString(caught[0].Buffer, 32, 6)}"
                                : caught.Count.ToString());
        Check("a worm used", "11", Worms());
        Check("the token spent", 0u, a.FishingToken);

        roll = 0;
        Clear();
        Fish(0, 4);
        Check("release: a fishing skill-up (0.1)", "1/0/1", $"{a.RealSkills[48]}/{(int)a.Animation}/{FishSkill()}");
        var bm = Of(0, 0x029);
        Check("release: skill gain message 38", "1:48:1:38",
              bm.Count == 1 ? $"1:{BitConverter.ToUInt32(bm[0].Buffer, 12)}:{BitConverter.ToUInt32(bm[0].Buffer, 16)}:{BitConverter.ToUInt16(bm[0].Buffer, 24)}" : bm.Count.ToString());

        // ---- the line snaps (lose 20, snap 42: a 30 roll) ----
        now += 6;
        Cast(0);
        now += 20;
        Fish(0, 2);
        special = a.HookedFish?.Special ?? 0;
        Clear();
        Fish(0, 5, 3);
        Check("mid-fight time-out warning", "28/57", $"{Talks(0)}/{(int)a.Animation}");
        roll = 0.3;
        Clear();
        Fish(0, 3, 0, special);
        Check("line break: NewFishingLineBreak, the message", "60/6", $"{(int)a.Animation}/{Talks(0)}");
        Check("line break: the bait goes with it", "10", Worms());
        roll = 0.99;
        Fish(0, 4);
        Check("release after the snap: no skill-up on a high roll", "1/0", $"{a.RealSkills[48]}/{(int)a.Animation}");

        // ---- giving up (stamina 200): the bait lost ----
        now += 6;
        Cast(0);
        now += 20;
        roll = 0;
        Fish(0, 2);
        Clear();
        Fish(0, 3, 200);
        Check("give up: NewFishingStop, bait-loss message", "62/24/9", $"{(int)a.Animation}/{Talks(0)}/{Worms()}");
        Fish(0, 4);
        Clear();
        Fish(0, 5, 3);
        Check("a warning with the token spent stops fishing", "/62", $"{Talks(0)}/{(int)a.Animation}");
        Fish(0, 4);

        // ---- validation ----
        Clear();
        Fish(0, 1);
        Check("mode 1 refused", true, log.Exists(l => l.Contains("mode")));
        Clear();
        Fish(0, 2, 5);
        Check("check hook with para refused", true, Logged("para must be 0"));
        Clear();
        Fish(0, 5, 11);
        Check("timeout warning with para 11 refused", "True/", $"{log.Exists(l => l.Contains("para"))}/{Talks(0)}");
        Clear();
        Fish(0, 4, 0, 0, a.Id + 1);
        Check("another char's id refused", true, Logged("Character id mismatch"));
        cfg.FishingEnable = false;
        Clear();
        Fish(0, 4);
        Check("0x110 with fishing disabled refused", true, Logged("Fishing is disabled"));
        Clear();
        Cast(0);
        Check("cast with fishing disabled: logged, released", "True/1/0", $"{Logged("Fishing is currently disabled")}/{Of(0, 0x052).Count}/{(int)a.Animation}");
        cfg.FishingEnable = true;

        // ---- no bait, no rod, the mog house ----
        Login(1);
        now += 6;
        Clear();
        Cast(1);
        Check("rod only: no bait", "2/1/0", $"{Talks(1)}/{Of(1, 0x052).Count}/{(int)cs[1].Animation}");
        Login(2);
        Clear();
        Cast(2);
        Check("nothing equipped: no rod", "1/1/0", $"{Talks(2)}/{Of(2, 0x052).Count}/{(int)cs[2].Animation}");
        cs[2].MoghouseId = 1;
        now += 6;
        Clear();
        Cast(2);
        Check("in a mog house: refused", "True/1/", $"{Logged("trying to fish in Mog House")}/{Of(2, 0x052).Count}/{Talks(2)}");

        // ---- leaving with a fish on the line loses the bait ----
        now += 6;
        Cast(0);
        now += 20;
        roll = 0;
        Fish(0, 2);
        Check("hooked before the time-out", 57, (int)a.Animation);
        zone.OnCharTimeout(ss[0], false);
        Check("time-out: the catch dropped, the bait lost", "/0/8", $"{a.HookedFish?.CatchId}/{(int)a.Animation}/{Worms()}");

        // ---- the fishing moghancement: +1 fishing (Mod.FISH), so a lungfish at skill 1 regenerates one less ----
        Login(3);
        var ma = cs[3];
        string HookRegen()
        {
            now += 6;
            Cast(3);
            now += 20;
            Clear();
            Fish(3, 2);
            var hp = Of(3, 0x115);
            string got = $"{ma.HookedFish?.CatchId}:{(hp.Count == 1 ? BitConverter.ToUInt16(hp[0].Buffer, 8) : 0)}";
            Fish(3, 4);
            return got;
        }
        roll = 0;
        string plain = HookRegen();
        Moghancement.Set(ma, Moghancement.Fishing);
        Check("fishing moghancement: the lungfish's regen 133, then 132 at fishing +1", $"{Lungfish}:133/{Lungfish}:132/1",
              $"{plain}/{HookRegen()}/{ma.GetMod(Mod.FISH)}");
        Moghancement.Set(ma, 0);

        // ---- a fishing mob (LoadFishMobs, GetMobPool): some roll hooks one of Phanauet Channel's ----
        var info1 = zone.ZoneInfoOf(1);
        ma.Rotation = 64;
        MobEntity hookedMob = null;
        double mobRoll = -1;
        for (int k = 0; k < 100 && hookedMob == null; k++)
        {
            roll = k / 100.0;
            now += 6;
            Cast(3);
            now += 20;
            Clear();
            Fish(3, 2);
            if (ma.HookedFish?.CatchType == 4)
            {
                hookedMob = info1.MobById[ma.HookedFish.CatchId];
                mobRoll = roll;
            }
            else Fish(3, 4);
        }
        Check("a roll hooks a fishing mob", true, hookedMob != null);
        if (hookedMob != null)
        {
            string MobCol(string col) => Scalar(db, $"SELECT {col} FROM fishing_mob WHERE mobid = ?", BoundValue.Of(hookedMob.Id));
            Check("hooked mob: one of the zone's non-NM fishing mobs, still down", "1/0/False",
                  $"{MobCol("zoneid")}/{MobCol("nm")}/{hookedMob.IsSpawned}");
            Check("hooked mob: hooked local var, hookedTime, NewFishingFish", $"1/{now}/57",
                  $"{hookedMob.LocalVars.GetValueOrDefault("hooked")}/{hookedMob.LocalVars.GetValueOrDefault("hookedTime")}/{(int)ma.Animation}");
            Check("hooked mob: something clamps onto your line ferociously (0x34)", "34", Talks(3).Split(',')[0]);
            var mf = Of(3, 0x115);
            var mb = mf.Count > 0 ? mf[0].Buffer : new byte[24];
            int level = int.Parse(MobCol("level")), difficulty = int.Parse(MobCol("difficulty"));
            int attack = (int)Math.Floor(difficulty * 1.5f * 20f);
            string expected = $"{(95 + (int)(mobRoll * 10)) * ((level + 36) / 2)}/{Math.Min(15, int.Parse(MobCol("base_delay")) + 1)}/129/" +
                              $"{Math.Min(15, int.Parse(MobCol("base_move")))}/{attack}/{(int)Math.Floor(attack / 20f * 0.5f) * 10}/30/1";
            Check("hooked mob 0x115: stamina by level, delay/move + the willow's large bonuses, regen 129, attack and heal by difficulty, large",
                  expected, $"{BitConverter.ToUInt16(mb, 4)}/{BitConverter.ToUInt16(mb, 6)}/{BitConverter.ToUInt16(mb, 8)}/{BitConverter.ToUInt16(mb, 10)}/" +
                            $"{BitConverter.ToUInt16(mb, 12)}/{BitConverter.ToUInt16(mb, 14)}/{BitConverter.ToUInt16(mb, 16)}/{mb[18]}");
            Fish(3, 4);
            Check("release: the mob unhooked (UnhookMob)", "0/0", $"{hookedMob.LocalVars.GetValueOrDefault("hooked")}/{(int)ma.Animation}");

            now += 6;
            Cast(3);
            now += 20;
            Fish(3, 2);
            Check("the same roll hooks it again", hookedMob.Id, ma.HookedFish?.CatchId);
            uint mobSpecial = ma.HookedFish?.Special ?? 0;
            string mobWorms = Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND itemId = ?", BoundValue.Of(ma.Id), BoundValue.Of(LittleWorm));
            roll = 0.99;
            Clear();
            Fish(3, 3, 0, mobSpecial);
            Check("reeled in (CatchMonster): NewFishingMonster", 61, (int)ma.Animation);
            var tn = Of(3, 0x043);
            Check("0x043 TALKNUMNAME: <name> caught a monster! (5)", "1:5:Mobangler",
                  tn.Count == 1 ? $"1:{BitConverter.ToUInt16(tn[0].Buffer, 10) - 0x8000 - MsgOffset:X}:{System.Text.Encoding.ASCII.GetString(tn[0].Buffer, 16, 9)}" : tn.Count.ToString());
            Check("the mob spawned two yalms behind the angler, half a yalm down",
                  $"True/{ma.X - 2 * Math.Cos(Math.PI / 2):F2}/{ma.Y - 0.5f:F2}/{ma.Z + 2 * Math.Sin(Math.PI / 2):F2}",
                  $"{hookedMob.IsSpawned}/{hookedMob.SpawnX:F2}/{hookedMob.SpawnY:F2}/{hookedMob.SpawnZ:F2}");
            Check("the mob: idle despawn 180, not charmable, unhooked, respawnTime the max", "180/0/0/0",
                  $"{hookedMob.GetMobMod(MobMod.IdleDespawn)}/{hookedMob.GetMobMod(MobMod.Charmable)}/" +
                  $"{hookedMob.LocalVars.GetValueOrDefault("hooked")}/{hookedMob.LocalVars.GetValueOrDefault("respawnTime")}");
            Check("the mob engages the angler, who claims it", $"True/{ma.Id}", $"{hookedMob.BattleTarget == ma}/{hookedMob.OwnerId}");
            Check("the bait goes with the catch", $"{int.Parse(mobWorms) - 1}",
                  Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND itemId = ?", BoundValue.Of(ma.Id), BoundValue.Of(LittleWorm)));
            ushort skillBefore = ma.RealSkills[48];
            roll = 0;
            Fish(3, 4);
            Check("release after a mob: no skill-up", $"{skillBefore}/0", $"{ma.RealSkills[48]}/{(int)ma.Animation}");
        }

        // ---- the Brigand's Chart (Buburimu Peninsula): only its pugil (25) and the Jade Etui chests (75) ----
        const int BubMsgOffset = 7273;
        const uint BrigandPugil = 17261040, FirstEtui = 17261035;
        Login(4);
        var br = cs[4];
        var info118 = zone.ZoneInfoOf(118);
        var pugil = info118.MobById[BrigandPugil];
        br.LocalVars["bChartActive"] = 1;
        br.Rotation = 0;
        Check("Brigand's Chart: the pugil is down", false, pugil.IsSpawned);
        roll = 0.1;
        now += 6;
        Cast(4);
        now += 20;
        Clear();
        Fish(4, 2);
        Check("chart, roll 10 of 100: the pugil hooked", $"4/{BrigandPugil}/57", $"{br.HookedFish?.CatchType}/{br.HookedFish?.CatchId}/{(int)br.Animation}");
        var pf = Of(4, 0x115);
        var pb = pf.Count > 0 ? pf[0].Buffer : new byte[24];
        Check("pugil 0x115: stamina 96*23, delay 11, regen 129, move 15, attack 480, heal 120, time 30, large",
              "2208/11/129/15/480/120/30/1", $"{BitConverter.ToUInt16(pb, 4)}/{BitConverter.ToUInt16(pb, 6)}/{BitConverter.ToUInt16(pb, 8)}/" +
                                            $"{BitConverter.ToUInt16(pb, 10)}/{BitConverter.ToUInt16(pb, 12)}/{BitConverter.ToUInt16(pb, 14)}/{BitConverter.ToUInt16(pb, 16)}/{pb[18]}");
        uint pugilSpecial = br.HookedFish?.Special ?? 0;
        roll = 0.99;
        Clear();
        const ushort Sneak = 71;
        br.StatusEffects.AddStatusEffect(new StatusEffect(Sneak, Sneak, 0, 0, 300_000));
        Fish(4, 3, 0, pugilSpecial);
        long pugilDespawnIn = pugil.DespawnAtMs - zone.NowMs();
        Check("the pugil reeled in under sneak: spawned behind the angler, not engaged or claimed, despawning in 180 s",
              $"61/True/{br.X - 2:F2}/{br.Y - 0.5f:F2}/{br.Z:F2}/False/0/True",
              $"{(int)br.Animation}/{pugil.IsSpawned}/{pugil.SpawnX:F2}/{pugil.SpawnY:F2}/{pugil.SpawnZ:F2}/{pugil.BattleTarget == br}/{pugil.OwnerId}/" +
              $"{pugilDespawnIn > 170_000 && pugilDespawnIn <= 180_000}");
        Fish(4, 4);

        roll = 0.5;
        now += 6;
        Cast(4);
        now += 20;
        Clear();
        Fish(4, 2);
        Check("chart with the pugil up: a Jade Etui hooked, an item hooked and a good feeling", $"5/{FirstEtui}/57/33,29",
              $"{br.HookedFish?.CatchType}/{br.HookedFish?.CatchId}/{(int)br.Animation}/{Talks(4, BubMsgOffset)}");
        var cf = Of(4, 0x115);
        var cb = cf.Count > 0 ? cf[0].Buffer : new byte[24];
        Check("chest 0x115: stamina 100*11, delay 10+1, regen 129, move 15, attack 480, heal 120, time 30, large",
              "1100/11/129/15/480/120/30/1", $"{BitConverter.ToUInt16(cb, 4)}/{BitConverter.ToUInt16(cb, 6)}/{BitConverter.ToUInt16(cb, 8)}/" +
                                            $"{BitConverter.ToUInt16(cb, 10)}/{BitConverter.ToUInt16(cb, 12)}/{BitConverter.ToUInt16(cb, 14)}/{BitConverter.ToUInt16(cb, 16)}/{cb[18]}");
        uint chestSpecial = br.HookedFish?.Special ?? 0;
        roll = 0.99;
        Clear();
        Fish(4, 3, 0, chestSpecial);
        var etui = info118.NpcById[FirstEtui];
        var ctn = Of(4, 0x043);
        Check("reeled in (CatchChest): NewFishingCaught, <name> fishes up a large box! (0x40)", "58/1:40:Brigand",
              $"{(int)br.Animation}/" + (ctn.Count == 1 ? $"1:{BitConverter.ToUInt16(ctn[0].Buffer, 10) - 0x8000 - BubMsgOffset:X}:{System.Text.Encoding.ASCII.GetString(ctn[0].Buffer, 16, 7)}" : ctn.Count.ToString()));
        Check("the chest set down two yalms behind, shown, owned by the angler", $"{br.X - 2:F2}/{br.Y:F2}/{br.Z:F2}/0/{br.Id}",
              $"{etui.X:F2}/{etui.Y:F2}/{etui.Z:F2}/{etui.Status}/{etui.LocalVars.GetValueOrDefault("owner")}");
        Fish(4, 4);
        now += 6;
        Cast(4);
        now += 20;
        Fish(4, 2);
        Check("the next hook takes the next unowned chest", $"5/{FirstEtui + 1}", $"{br.HookedFish?.CatchType}/{br.HookedFish?.CatchId}");
        Fish(4, 4);

        // ---- the fishing contest (fishingcontest.cpp); the shared contest row is put back afterwards ----
        string savedContest = Scalar(db, "SELECT CONCAT_WS(',', status, criteria, measure, fishid, starttime, nextstagetime) FROM fishing_contest LIMIT 1");
        try
        {
            var lua = scripts.Lua;
            var got = new List<string>();
            lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
            string L(string code)
            {
                got.Clear();
                lua.DoString($"local p = GetPlayerByID({Chars[2].Id}) local q = GetPlayerByID({Chars[1].Id})\n" + code, "=verify_contest");
                return string.Join(",", got);
            }
            string Db(uint id, string col) => Scalar(db, $"SELECT {col} FROM fishing_contest_entries WHERE charid = ?", BoundValue.Of(id)) ?? "none";
            // the criteria and measure rolls take their top value: both, smallest
            zone.ContestRandom = (min, max) => max - 1;
            Check("InitNewFishingContest: CONTESTING, both, smallest, a fish, started now, 300 s to the next stage", $"0,2,1,true,{now},{now + 300}",
                L("InitNewFishingContest() local c = GetFishingContest() report(c.status) report(c.criteria) report(c.measure) " +
                  "report(tostring(c.fishid > 0)) report(c.starttime) report(c.changetime)"));
            Check("InitNewFishingContest: one contest row, CONTESTING", "1/0",
                $"{Scalar(db, "SELECT COUNT(*) FROM fishing_contest")}/{Scalar(db, "SELECT status FROM fishing_contest")}");
            Check("submitContestFish: the scores, the rows (job, level, fishing rank)", "700,300/700/1/30",
                L("p:submitContestFish(700) q:submitContestFish(300) report(p:getContestScore()) report(q:getContestScore())") +
                $"/{Db(Chars[2].Id, "score")}/{Db(Chars[2].Id, "mjob")}/{Db(Chars[2].Id, "mlevel")}");
            Check("withdrawContestFish: the entry and its row gone", "0/none", L("p:withdrawContestFish() report(p:getContestScore())") + $"/{Db(Chars[2].Id, "score")}");
            L("p:submitContestFish(700)");
            string Progress()
            {
                now = long.Parse(L("report(GetFishingContest().changetime)")) + 1;
                return L("ProgressFishingContest() report(GetFishingContest().status)");
            }
            Check("ProgressFishingContest: nothing before the change time", "0", L("ProgressFishingContest() report(GetFishingContest().status)"));
            Check("ProgressFishingContest: OPENING, ACCEPTING, then RELEASING", "1,2,3", $"{Progress()},{Progress()},{Progress()}");
            Check("RELEASING scores (the smallest first): ranks, shares, written", "1,2/1/1",
                L("report(q:getContestRank()) report(p:getContestRank())") + $"/{Db(Chars[1].Id, "contestRank")}/{Db(Chars[1].Id, "share")}");
            Check("the ranking history: 1st and 2nd counted", "1/0/1",
                $"{Scalar(db, "SELECT contest_rank_1 FROM char_fishing_contest_history WHERE charid = ?", BoundValue.Of(Chars[1].Id))}/" +
                $"{Scalar(db, "SELECT contest_rank_1 FROM char_fishing_contest_history WHERE charid = ?", BoundValue.Of(Chars[2].Id))}/" +
                $"{Scalar(db, "SELECT contest_rank_2 FROM char_fishing_contest_history WHERE charid = ?", BoundValue.Of(Chars[2].Id))}");
            Check("claimContestReward outside PRESENTING: refused", "1", L("q:claimContestReward() report(q:getContestRewardStatus().rank)"));
            string presenting = Progress();
            Check("PRESENTING: the next stage, 1206000 s long", $"4/{now + 1206000}", $"{presenting}/{L("report(GetFishingContest().changetime)")}");
            Check("getContestRewardStatus, then claimContestReward marks it claimed", "1,1,0",
                L("report(q:getContestRewardStatus().rank) report(q:getContestRewardStatus().share) q:claimContestReward() report(q:getContestRewardStatus().rank)"));
            Check("getContestRankHistory: rank1, rank2, rank3, rank4to10", "1,0,0,0/0,1,0,0",
                L("local h = q:getContestRankHistory() report(h.rank1) report(h.rank2) report(h.rank3) report(h.rank4to10)") + "/" +
                L("local h = p:getContestRankHistory() report(h.rank1) report(h.rank2) report(h.rank3) report(h.rank4to10)"));
            Check("SetContestParameters: the fish, measure and criteria", "4472,0,1",
                L("SetContestParameters(4472, 0, 1) local c = GetFishingContest() report(c.fishid) report(c.measure) report(c.criteria)"));
            Check("HIATUS, then a new contest (the entries cleared)", "5,0/0/0",
                $"{Progress()},{Progress()}/{L("report(p:getContestScore())")}/{Scalar(db, "SELECT COUNT(*) FROM fishing_contest_entries")}");
        }
        finally
        {
            Sql(db, "DELETE FROM fishing_contest");
            if (savedContest != null) Sql(db, $"INSERT INTO fishing_contest VALUES ({savedContest})");
            Sql(db, "DELETE FROM fishing_contest_entries");
            foreach (var (id, _, _) in Chars) Sql(db, "DELETE FROM char_fishing_contest_history WHERE charid = ?", BoundValue.Of(id));
        }
    }
}
