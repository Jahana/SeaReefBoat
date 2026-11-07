using System;
using System.Collections.Generic;
using System.IO;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Defense.Live -- live gate for the melee defensive reactions (ZoneHost.Defense: CBattleEntity::OnAttack's
// Perfect Dodge, CAttack::IsDeflected, CheckParried, battleutils::IsAbsorbByShadow, CheckAnticipated, CheckCounter and
// the counter's damage, CheckGuarded, the shield block and its absorption, HandleIssekiganEnmityBonus, and the
// BATTLE2 reaction block) both ways between a WAR and a Wild Rabbit in West Ronfaure, and the additional effects
// (battleutils::HandleEnspell: enspells, Blood Weapon, runes, Drain Samba, a weapon's and a mob script's) and the
// spikes (HandleSpikesDamage: Blaze / Dread Spikes, Reprisal, Retaliation, a mob script's auto spikes, Battuta's parry
// spikes), Zanshin's extra swing and the tail of TakePhysicalDamage (stoneskin by attack type, Formless Strikes, bind
// breaking, the TAKE_DAMAGE listener), with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 990957;
    private const ushort WestRonfaure = 100;
    private const uint Rabbit = 17186822;
    private const ushort RabbitTargId = 6;
    private const ushort BronzeSword = 16535, MapleShield = 12290, WindKnifePlus1 = 20568, Shuriken = 17301, Chaosbringer = 16607;
    private const ushort EffectPerfectDodge = 49, EffectThirdEye = 67, EffectDefenseBoost = 93, EffectIssekigan = 484,
        EffectCopyImage2 = 444, EffectEnlight = 274, EffectDrainSamba = 368, EffectDrainDaze = 378, EffectSulpor = 527,
        EffectBlazeSpikes = 34, EffectDreadSpikes = 173, EffectReprisal = 403, EffectRetaliation = 405, EffectBattuta = 570,
        EffectFormlessStrikes = 341;
    private const int TraitTreasureHunter = 19;
    private const long Never = long.MaxValue / 4;

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

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

    private record Result(int Res, int Param, int Msg, int Spikes, int SpikesParam, int SpikesMsg, int Add = 0, int AddParam = 0, int AddMsg = 0);

    // the first target's results, walking each result's optional additional effect and reaction blocks
    private static List<Result> Results(BasicPacket p)
    {
        var list = new List<Result>();
        int n = (int)U(p, 182, 4);
        int at = 186;
        for (int i = 0; i < n; i++)
        {
            int res = (int)U(p, at, 3), param = (int)U(p, at + 27, 17), msg = (int)U(p, at + 44, 10);
            at += 85;
            int add = 0, addParam = 0, addMsg = 0;
            if (U(p, at, 1) != 0)
            {
                add = (int)U(p, at + 1, 6);
                addParam = (int)U(p, at + 11, 17);
                addMsg = (int)U(p, at + 28, 10);
                at += 1 + 37;
            }
            else at += 1;
            int spikes = 0, sParam = 0, sMsg = 0;
            if (U(p, at, 1) != 0)
            {
                spikes = (int)U(p, at + 1, 6);
                sParam = (int)U(p, at + 11, 14);
                sMsg = (int)U(p, at + 25, 10);
                at += 1 + 34;
            }
            else at += 1;
            list.Add(new Result(res, param, msg, spikes, sParam, sMsg, add, addParam, addMsg));
        }
        return list;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("defverify"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume with the NEW_ADVENTURER title (not a first login: no charCreate), standing at the zone's entrance (a
        // char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Guardy"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-126.0f), BoundValue.Of(-62.0f), BoundValue.Of(273.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 30)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 30)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        // sword, shield and parrying skill
        foreach (var skill in new byte[] { 3, 30, 31 })
            Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, ?, 400, 0)", BoundValue.Of(CharId), BoundValue.Of(skill));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(BronzeSword), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 2, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(MapleShield), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 3, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(WindKnifePlus1), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 4, ?, 10, ?)",
            BoundValue.Of(CharId), BoundValue.Of(Shuriken), BoundValue.Of(new byte[24]));
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 5, ?, 1, ?)",
            BoundValue.Of(CharId), BoundValue.Of(Chaosbringer), BoundValue.Of(new byte[24]));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, new AbilityTable(), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(11);
        zone.SendIpc = _ => { };
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);

        var info = zone.ZoneInfoOf(WestRonfaure);
        var rabbit = info.MobById[Rabbit];
        Check("rabbit up", true, rabbit.IsSpawned);

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
        void Pos(float dx, byte dir)
        {
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(rabbit.X + dx).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(rabbit.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(rabbit.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            s.PacketList.Clear();
        }
        void Action(ActionId id, ushort targid)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(Rabbit).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)id).CopyTo(b, C2sAction.OffActionID);
            });
        }
        void Equip(byte invSlot, byte equipSlot) => Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
        List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);
        void Tick(long ms)
        {
            s.PacketList.Clear();
            now += ms;
            zone.Tick();
        }

        // the next round of the actor's (the other side frozen), each tick's HP changes alongside
        (List<Result> R, int CharLost, int RabbitLost) Round(uint actor)
        {
            bool charSwings = actor == CharId;
            if (charSwings) rabbit.SwingAtMs = Never; else c.SwingAtMs = Never;
            for (int t = 0; t < 40; t++)
            {
                if (charSwings && c.SwingAtMs > now + 400) c.SwingAtMs = now + 400;
                if (!charSwings && rabbit.SwingAtMs > now + 400) rabbit.SwingAtMs = now + 400;
                int ch = c.Hp, rh = rabbit.Hp;
                Tick(400);
                var round = Of(0x028).Find(p => U(p, 40, 32) == actor && U(p, 82, 4) == 1);
                if (round != null) return (Results(round), ch - c.Hp, rh - rabbit.Hp);
            }
            return (new List<Result>(), 0, 0);
        }
        void Fresh()
        {
            c.Hp = c.MaxHp;
            rabbit.BaseMaxHp = rabbit.MaxHp = rabbit.Hp = 5000;
            rabbit.Tp = 0;
        }

        Console.WriteLine("The shield's absorption (CItemEquipment::addModifier: DEF 2 / 2 + size 2's 40)");
        Check("maple shield: size 2, absorption 41", "True/2/41",
            $"{stats.Items.Equipment(MapleShield)?.IsShield}/{stats.Items.Equipment(MapleShield)?.ShieldSize}/{stats.Items.Equipment(MapleShield)?.ShieldAbsorption}");

        Console.WriteLine("The fight");
        Fresh();
        Pos(-1, 0);
        Action(ActionId.Attack, RabbitTargId);
        Check("engaged on the rabbit", true, c.BattleTarget == rabbit);
        Tick(3000);
        Tick(400);
        Tick(400);
        Check("rabbit engaged on the char", "True/True", $"{rabbit.Engaged}/{rabbit.BattleTarget == c}");
        rabbit.SetModifier(Mod.ACC, 1000);
        c.SetModifier(Mod.ACC, 1000);
        int ce0 = rabbit.Enmity.CE(c);
        Check("the char on the rabbit's list", true, ce0 > 100);

        // a mob's swings until n results matched, all the results seen
        List<Result> MobSwings(Func<Result, bool> stop, int rounds = 12)
        {
            var all = new List<Result>();
            for (int i = 0; i < rounds; i++)
            {
                var r = Round(Rabbit);
                all.AddRange(r.R);
                if (r.R.Exists(x => stop(x))) break;
            }
            return all;
        }

        Console.WriteLine("Utsusemi: three shadows (Copy Image)");
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.CopyImage, EffectId.CopyImage, 3, 0, 900_000, 0, 3));
        Check("three shadows up", 3, c.GetMod(Mod.UTSUSEMI));
        int absorbed = 0, hpLostBeforeGone = 0;
        bool shapeOk = true, iconOk = false;
        for (int i = 0; i < 12 && c.GetMod(Mod.UTSUSEMI) > 0; i++)
        {
            int ce = rabbit.Enmity.CE(c);
            var r = Round(Rabbit);
            int took = 0;
            foreach (var x in r.R)
            {
                if (x.Msg == 31) { absorbed++; shapeOk &= x.Res == 1 && x.Param == 1; took++; }
            }
            if (c.GetMod(Mod.UTSUSEMI) > 0) hpLostBeforeGone += r.CharLost;
            if (took > 0 && c.GetMod(Mod.UTSUSEMI) > 0)
                Check($"25 CE a shadow ({took})", ce - 25 * took, rabbit.Enmity.CE(c));
            if (c.GetMod(Mod.UTSUSEMI) == 2) iconOk = c.StatusEffects.GetStatusEffect(EffectId.CopyImage)?.Icon == EffectCopyImage2;
            if (c.GetMod(Mod.UTSUSEMI) == 1) iconOk &= c.StatusEffects.GetStatusEffect(EffectId.CopyImage)?.Icon == EffectId.CopyImage;
        }
        Check("three swings absorbed, miss / 31 / param 1", "3/True", $"{absorbed}/{shapeOk}");
        Check("the icon down to copy image (2), then copy image", true, iconOk);
        Check("no HP lost while the shadows held", 0, hpLostBeforeGone);
        Check("copy image gone with the last", "False/0", $"{c.StatusEffects.HasStatusEffect(EffectId.CopyImage)}/{c.GetMod(Mod.UTSUSEMI)}");

        Console.WriteLine("Blink: two shadows, at 80%");
        Fresh();
        c.SetModifier(Mod.BLINK, 2);
        var blink = MobSwings(x => false, 6);
        Check("at most two absorbed, blink down", true, blink.FindAll(x => x.Msg == 31).Count <= 2 && c.GetMod(Mod.BLINK) == 2 - blink.FindAll(x => x.Msg == 31).Count);
        c.SetModifier(Mod.BLINK, 0);

        Console.WriteLine("Perfect Dodge on the char");
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectPerfectDodge, EffectPerfectDodge, 1, 0, 30_000));
        var pd = Round(Rabbit);
        Check("every swing dodged: miss / 32 / 0", true, pd.R.Count > 0 && pd.R.TrueForAll(x => x.Res == 1 && x.Msg == 32 && x.Param == 0));
        Check("no HP lost", 0, pd.CharLost);
        c.StatusEffects.DelStatusEffectSilent(EffectPerfectDodge);

        Console.WriteLine("Defense Boost's deflection (subpower 128: the front half)");
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectDefenseBoost, EffectDefenseBoost, 1, 0, 30_000, 0, 128));
        var db1 = Round(Rabbit);
        Check("every swing deflected: parry / 1 / 0", true, db1.R.Count > 0 && db1.R.TrueForAll(x => x.Res == 3 && x.Msg == 1 && x.Param == 0));
        Check("no HP lost", 0, db1.CharLost);
        c.StatusEffects.DelStatusEffectSilent(EffectDefenseBoost);

        Console.WriteLine("Third Eye (no Seigan): one anticipation, then gone");
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectThirdEye, EffectThirdEye, 0, 0, 30_000));
        var te = MobSwings(x => x.Msg == 30);
        var ant = te.Find(x => x.Msg == 30);
        Check("anticipated: miss / 30 / 0", "1/0", ant == null ? "none" : $"{ant.Res}/{ant.Param}");
        Check("third eye used up", false, c.StatusEffects.HasStatusEffect(EffectThirdEye));

        Console.WriteLine("Counter (COUNTER 80): the reaction block, the rabbit's HP");
        Fresh();
        c.SetModifier(Mod.COUNTER, 80);
        int countered = 0, spikesDealt = 0, rabbitLost = 0;
        bool counterShape = true;
        for (int i = 0; i < 6; i++)
        {
            var r = Round(Rabbit);
            rabbitLost += r.RabbitLost;
            foreach (var x in r.R)
                if (x.Spikes != 0)
                {
                    countered++;
                    spikesDealt += x.SpikesParam;
                    counterShape &= x.Spikes == 63 && x.SpikesMsg == 33 && x.Res == 1 && x.Param == 0;
                }
        }
        Check("counters landed", true, countered > 0);
        Check("each: miss, reaction 63, message 33", true, counterShape);
        Check("the rabbit lost the counters' damage", spikesDealt, rabbitLost);
        Check("counter damage dealt", true, spikesDealt > 0);

        Console.WriteLine("A counter into the rabbit's shadows (UTSUSEMI 2)");
        Fresh();
        rabbit.SetModifier(Mod.UTSUSEMI, 2);
        int intoShadow = 0, shadowLost = 0;
        bool shadowShape = true;
        for (int i = 0; i < 8 && rabbit.GetMod(Mod.UTSUSEMI) > 0; i++)
        {
            var r = Round(Rabbit);
            foreach (var x in r.R)
                if (x.Spikes != 0 && x.SpikesMsg == 14)
                {
                    intoShadow++;
                    shadowShape &= x.Spikes == 63 && x.SpikesParam == 1 && x.Msg == 0 && x.Param == 0;
                    shadowLost += r.RabbitLost;
                }
        }
        Check("two counters taken by the shadows: message 0, reaction param 1 / message 14", "2/True", $"{intoShadow}/{shadowShape}");
        Check("the rabbit's shadows gone, no HP lost to them", "0/0", $"{rabbit.GetMod(Mod.UTSUSEMI)}/{shadowLost}");
        c.SetModifier(Mod.COUNTER, 0);

        Console.WriteLine("A shield block (PALISADE_BLOCK_BONUS 100: always)");
        Equip(2, CharStats.SlotSub);
        Check("the shield on", MapleShield, c.Equipped[CharStats.SlotSub]?.ItemId ?? 0);
        c.SetModifier(Mod.PALISADE_BLOCK_BONUS, 100);
        Fresh();
        var bl = MobSwings(x => false, 3);
        var hits = bl.FindAll(x => x.Res != 1);
        Check("every landed swing blocked", true, hits.Count > 0 && hits.TrueForAll(x => x.Res == 4 && (x.Msg == 1 || x.Msg == 67)));
        int blockedTaken = 0;
        foreach (var x in hits) blockedTaken += x.Param;
        Check("the HP lost: the blocked damage", c.MaxHp - blockedTaken, c.Hp);
        c.SetModifier(Mod.PALISADE_BLOCK_BONUS, 0);
        Equip(0, CharStats.SlotSub);

        Console.WriteLine("Parry with a sword (INQUARTATA 100: always), Issekigan's enmity");
        Equip(1, CharStats.SlotMain);
        Check("the sword on", BronzeSword, c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        c.SetModifier(Mod.INQUARTATA, 100);
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectIssekigan, EffectIssekigan, 25, 0, 300_000));
        int ceP = rabbit.Enmity.CE(c);
        var pr = Round(Rabbit);
        var parries = pr.R.FindAll(x => x.Msg == 70);
        Check("every landed swing parried: parry / 70 / 0", true,
            parries.Count > 0 && pr.R.TrueForAll(x => x.Msg == 15 || x.Res == 3 && x.Msg == 70 && x.Param == 0));
        Check("no HP lost", 0, pr.CharLost);
        Check("300 CE a parry under Issekigan", ceP + 300 * parries.Count, rabbit.Enmity.CE(c));
        c.StatusEffects.DelStatusEffectSilent(EffectIssekigan);
        c.SetModifier(Mod.INQUARTATA, 0);

        Console.WriteLine("The char swings: the rabbit guards (a MNK, ADDITIVE_GUARD 100)");
        Fresh();
        byte job = rabbit.MJob;
        rabbit.MJob = MobStats.JobMnk;
        rabbit.SetModifier(Mod.ADDITIVE_GUARD, 100);
        var gd = Round(CharId);
        var landed = gd.R.FindAll(x => x.Res != 1);
        int guarded = 0;
        foreach (var x in landed) guarded += x.Param;
        Check("every landed swing guarded", true, landed.Count > 0 && landed.TrueForAll(x => x.Res == 2 && (x.Msg == 1 || x.Msg == 67)));
        Check("the rabbit's HP: the guarded damage", guarded, gd.RabbitLost);
        rabbit.SetModifier(Mod.ADDITIVE_GUARD, 0);
        rabbit.MJob = job;

        Console.WriteLine("The rabbit counters (COUNTER 80)");
        Fresh();
        rabbit.SetModifier(Mod.COUNTER, 80);
        int mobCounters = 0, mobSpikes = 0, charLost = 0;
        for (int i = 0; i < 4; i++)
        {
            var r = Round(CharId);
            charLost += r.CharLost;
            foreach (var x in r.R)
                if (x.Spikes == 63 && x.SpikesMsg == 33) { mobCounters++; mobSpikes += x.SpikesParam; }
        }
        Check("the rabbit countered", true, mobCounters > 0);
        Check("the char lost the counters' damage", mobSpikes, charLost);
        rabbit.SetModifier(Mod.COUNTER, 0);

        Console.WriteLine("Perfect Dodge on the rabbit");
        Fresh();
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(EffectPerfectDodge, EffectPerfectDodge, 1, 0, 30_000));
        var rpd = Round(CharId);
        Check("every swing dodged: miss / 32", true, rpd.R.Count > 0 && rpd.R.TrueForAll(x => x.Res == 1 && x.Msg == 32 && x.Param == 0));
        Check("no HP lost", 0, rpd.RabbitLost);
        rabbit.StatusEffects.DelStatusEffectSilent(EffectPerfectDodge);

        Console.WriteLine("Additional effects (battleutils::HandleEnspell); no weather, Firesday, no thunder or light resist");
        info.Weather = 0;
        rabbit.SetModifier(Mod.THUNDER_MEVA, 0);
        rabbit.SetModifier(Mod.LIGHT_MEVA, 0);
        static bool Landed(Result x) => x.Res is 0 or 2 or 4;
        static int Total(List<Result> rs, Func<Result, int> f)
        {
            int n = 0;
            foreach (var x in rs) n += f(x);
            return n;
        }
        // the char's rounds until n landed swings
        List<Result> CharSwings(int landed, out int rabbitLost, out int charLost)
        {
            var all = new List<Result>();
            rabbitLost = charLost = 0;
            for (int i = 0; i < 12 && all.FindAll(Landed).Count < landed; i++)
            {
                rabbit.Tp = 0; // no TP move from the rabbit while only the char swings
                var r = Round(CharId);
                all.AddRange(r.R);
                rabbitLost += r.RabbitLost;
                charLost += r.CharLost;
            }
            return all;
        }

        Fresh();
        // a THF with the trait: TH 1 brings the rabbit to 1, and a sure proc (TREASURE_HUNTER_PROC 2400: x25) to 2
        byte war = c.MJob;
        c.MJob = MobStats.JobThf;
        c.Traits[TraitTreasureHunter >> 3] |= (byte)(1 << (TraitTreasureHunter & 7));
        c.SetModifier(Mod.TREASURE_HUNTER, 1);
        c.SetModifier(Mod.TREASURE_HUNTER_PROC, 2400);
        c.SetModifier(Mod.ENSPELL, 5); // (Treasure Hunter goes before an enspell)
        rabbit.ThLvl = 0;
        List<Result> th = null;
        for (int i = 0; i < 6 && (th == null || th.Count == 0 || !Landed(th[0])); i++) th = Round(CharId).R;
        Check("Treasure Hunter: the first swing's proc (7 / 603), the rabbit's TH level 2", "7/603/2/2",
              $"{th[0].Add}/{th[0].AddMsg}/{th[0].AddParam}/{rabbit.ThLvl}");
        // map.DISABLE_TREASURE_HUNTER_PROCS: no proc, the enspell goes off instead; TH 1 from the direct action only
        stats.Settings.DisableTreasureHunterProcs = true;
        Fresh();
        rabbit.ThLvl = 0;
        th = null;
        for (int i = 0; i < 6 && (th == null || th.Count == 0 || !Landed(th[0])); i++) th = Round(CharId).R;
        Check("DISABLE_TREASURE_HUNTER_PROCS: the first swing's enspell instead (5), the rabbit's TH level 1", "5/1",
              $"{th[0].Add}/{rabbit.ThLvl}");
        stats.Settings.DisableTreasureHunterProcs = false;
        c.MJob = war;
        c.Traits[TraitTreasureHunter >> 3] &= unchecked((byte)~(1 << (TraitTreasureHunter & 7)));
        c.SetModifier(Mod.TREASURE_HUNTER, 0);
        c.SetModifier(Mod.TREASURE_HUNTER_PROC, 0);
        rabbit.ThLvl = 0;

        Fresh();
        c.SetModifier(Mod.ENSPELL, 5); // Enthunder
        c.SetModifier(Mod.ENSPELL_DMG, 10);
        var en1 = CharSwings(3, out int en1Lost, out _);
        Check("Enthunder: every landed swing, thunder 10 (229)", true,
              en1.FindAll(Landed).Count >= 3 && en1.FindAll(Landed).TrueForAll(x => x.Add == 5 && x.AddMsg == 229 && x.AddParam == 10));
        Check("the rabbit lost the swings' and the enspell's damage", Total(en1, x => x.Param + x.AddParam), en1Lost);

        Fresh();
        c.SetModifier(Mod.ENSPELL, 13); // Enthunder II
        c.SetModifier(Mod.ENSPELL_DMG, 0);
        var en2 = new List<Result>();
        var en2First = new List<int>();
        bool en2OnlyFirst = true;
        for (int i = 0; i < 9; i++)
        {
            var r = Round(CharId).R;
            en2.AddRange(r);
            if (r.Count > 0 && Landed(r[0])) en2First.Add(r[0].AddParam);
            for (int j = 1; j < r.Count; j++) en2OnlyFirst &= r[j].Add == 0;
        }
        // (a WAR has no enhancing magic skill: the cap is 2 x 3)
        Check("Enthunder II: the first swing's potency one more a round, from 0 to the cap of 6", "0,1,2,3,4,5,6,6",
              string.Join(",", en2First.GetRange(0, Math.Min(8, en2First.Count))));
        Check("the later swings of a round: none", true, en2OnlyFirst);
        Check("ENSPELL_DMG held at the cap", 6, c.GetMod(Mod.ENSPELL_DMG));
        c.SetModifier(Mod.ENSPELL, 0);
        c.SetModifier(Mod.ENSPELL_DMG, 0);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectEnlight, EffectEnlight, 2, 0, 180_000));
        Check("Enlight: ENSPELL 7, its power the damage", "7/2", $"{c.GetMod(Mod.ENSPELL)}/{c.GetMod(Mod.ENSPELL_DMG)}");
        var enl = CharSwings(3, out _, out _).FindAll(Landed);
        Check("light 2, then 1 (7 / 229), then gone", "7:2,7:1,0:0",
              string.Join(",", enl.GetRange(0, 3).ConvertAll(x => $"{x.Add}:{x.AddParam}")));
        Check("Enlight worn off at 1", "False/0", $"{c.StatusEffects.HasStatusEffect(EffectEnlight)}/{c.GetMod(Mod.ENSPELL)}");
        c.SetModifier(Mod.ENSPELL_DMG, 0);

        Fresh();
        c.Hp = c.MaxHp / 2;
        c.SetModifier(Mod.ENSPELL, 17); // Blood Weapon
        var bw = CharSwings(2, out _, out int bwCharLost).FindAll(Landed);
        Check("Blood Weapon: HP drained by the swing's damage (21 / 161)", true,
              bw.Count >= 2 && bw.TrueForAll(x => x.Add == 21 && x.AddMsg == 161 && x.AddParam == x.Param));
        Check("the char healed by it", Total(bw, x => x.AddParam), -bwCharLost);
        c.SetModifier(Mod.ENSPELL, 0);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectSulpor, EffectSulpor, 1, 0, 180_000));
        var rune = CharSwings(3, out int runeLost, out _).FindAll(Landed);
        // the bronze sword's DPS 1.82: 0.97x - 1.30x, floored, is 1 - 2
        Check("Sulpor: rune enhancement, thunder 1-2 on every landed swing", true,
              rune.Count >= 3 && rune.TrueForAll(x => x.Add == 5 && x.AddMsg == 229 && x.AddParam >= 1 && x.AddParam <= 2));
        Check("the rabbit lost the swings' and the rune's damage", Total(rune, x => x.Param + x.AddParam), runeLost);
        c.StatusEffects.DelStatusEffectSilent(EffectSulpor);

        Fresh();
        c.Hp = c.MaxHp / 2;
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectDrainSamba, EffectDrainSamba, 1, 0, 120_000));
        var samba = CharSwings(3, out _, out int sambaCharLost).FindAll(Landed);
        var daze = rabbit.StatusEffects.GetStatusEffect(EffectDrainDaze);
        Check("Drain Samba: Drain Daze on the rabbit, the char's, power 1", $"{CharId}/1", $"{daze?.SubId}/{daze?.Power}");
        Check("each landed swing drains HP (21 / 161), at most half the damage", true,
              samba.Count >= 3 && samba.TrueForAll(x => x.Add == 21 && x.AddMsg == 161 && x.AddParam <= x.Param / 2));
        Check("the char healed by the drains (some)", $"{Total(samba, x => x.AddParam)}/True",
              $"{-sambaCharLost}/{Total(samba, x => x.AddParam) > 0}");
        c.StatusEffects.DelStatusEffectSilent(EffectDrainSamba);
        rabbit.StatusEffects.DelStatusEffectSilent(EffectDrainDaze);

        Fresh();
        Equip(3, 0);
        Check("the wind knife +1 in the main hand", WindKnifePlus1, (int)(c.MainWeapon?.Equip?.ItemId ?? 0));
        var knife = CharSwings(6, out int knifeLost, out _).FindAll(Landed);
        var procs = knife.FindAll(x => x.Add != 0);
        Check("its additional effect (xi.additionalEffect.attack): wind damage (3 / 163)", true,
              procs.Count > 0 && procs.TrueForAll(x => x.Add == 3 && x.AddMsg == 163) && Total(procs, x => x.AddParam) > 0);
        // calcDamage's takeDamage and the DAMAGE proc's delHP both land the proc's damage
        Check("the rabbit lost the swings' and twice the procs' damage", Total(knife, x => x.Param) + 2 * Total(procs, x => x.AddParam), knifeLost);
        Equip(1, 0);

        Fresh();
        scripts.Lua.DoString($"xi.zones['{info.Name}'].mobs['{rabbit.Name}'].onAdditionalEffect = function(mob, target, damage) return 1, 163, 7 end",
                             "=verify_defense");
        rabbit.SetMobMod(MobMod.AddEffect, 1);
        var mobAe = MobSwings(x => x.Res is 0 or 2 or 4, 6);
        var mobLanded = mobAe.FindAll(Landed);
        Check("a mob with ADD_EFFECT: its script's onAdditionalEffect returns into the result", true,
              mobLanded.Count > 0 && mobLanded.TrueForAll(x => x.Add == 1 && x.AddMsg == 163 && x.AddParam == 7));
        scripts.Lua.DoString($"xi.zones['{info.Name}'].mobs['{rabbit.Name}'].onAdditionalEffect = function(mob, target, damage) return 1, 163, -5 end",
                             "=verify_defense");
        var mobHeal = MobSwings(x => x.Res is 0 or 2 or 4, 6).FindAll(Landed);
        Check("a negative damage reads as recovers HP (384)", true, mobHeal.Count > 0 && mobHeal.TrueForAll(x => x.AddMsg == 384));
        rabbit.SetMobMod(MobMod.AddEffect, 0);
        scripts.Lua.DoString($"xi.zones['{info.Name}'].mobs['{rabbit.Name}'].onAdditionalEffect = nil", "=verify_defense");

        Console.WriteLine("Spikes (battleutils::HandleSpikesDamage)");
        // the rabbit's rounds until n landed swings, with the HP each side lost
        List<Result> RabbitSwings(int landed, out int rabbitLost, out int charLost)
        {
            var all = new List<Result>();
            rabbitLost = charLost = 0;
            for (int i = 0; i < 12 && all.FindAll(Landed).Count < landed; i++)
            {
                var r = Round(Rabbit);
                all.AddRange(r.R);
                rabbitLost += r.RabbitLost;
                charLost += r.CharLost;
            }
            return all;
        }

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectBlazeSpikes, EffectBlazeSpikes, 20, 0, 180_000));
        Check("Blaze Spikes: SPIKES 1, SPIKES_DMG 20", "1/20", $"{c.GetMod(Mod.SPIKES)}/{c.GetMod(Mod.SPIKES_DMG)}");
        var blaze = RabbitSwings(3, out int blazeLost, out _);
        Check("each landed swing: the spikes deal 20 (1 / 44)", true,
              blaze.FindAll(Landed).Count >= 3 && blaze.FindAll(Landed).TrueForAll(x => x.Spikes == 1 && x.SpikesMsg == 44 && x.SpikesParam == 20));
        Check("a missed swing: none", true, blaze.FindAll(x => x.Res == 1).TrueForAll(x => x.Spikes == 0));
        Check("the rabbit lost the spikes' damage", Total(blaze, x => x.SpikesParam), blazeLost);
        c.StatusEffects.DelStatusEffectSilent(EffectBlazeSpikes);
        Check("Blaze Spikes off: SPIKES 0", 0, c.GetMod(Mod.SPIKES));

        Fresh();
        c.Hp = c.MaxHp / 2;
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectDreadSpikes, EffectDreadSpikes, 1, 0, 180_000, 0, 1000));
        // the rabbit's blows are 0 - 2 against the char's DEF, so three of them could all be 0 and drain none: its attack
        // raised for the swings, each lands at the pDIF cap and some drain is certain
        short dreadAtt = rabbit.GetMod(Mod.ATT);
        rabbit.SetModifier(Mod.ATT, 1000);
        var dread = RabbitSwings(3, out int dreadLost, out int dreadCharLost).FindAll(Landed);
        rabbit.SetModifier(Mod.ATT, dreadAtt);
        // (a blow of no damage drains none and keeps the damage message)
        Check("Dread Spikes: each landed swing drains the damage taken (3 / 132), some drained", true,
              dread.Count >= 3 && dread.TrueForAll(x => x.Spikes == 3 && x.SpikesMsg == (x.SpikesParam > 0 ? 132 : 44)) &&
              Total(dread, x => x.SpikesParam) > 0);
        Check("the rabbit lost the drains; the char took the swings less them",
              $"{Total(dread, x => x.SpikesParam)}/{Total(dread, x => x.Param) - Total(dread, x => x.SpikesParam)}", $"{dreadLost}/{dreadCharLost}");
        Check("the subpower down by the damage drained", 1000 - Total(dread, x => x.SpikesParam),
              c.StatusEffects.GetStatusEffect(EffectDreadSpikes)?.SubPower ?? -1);
        c.StatusEffects.DelStatusEffectSilent(EffectDreadSpikes);

        Fresh();
        Equip(2, CharStats.SlotSub);
        c.SetModifier(Mod.PALISADE_BLOCK_BONUS, 100);
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectReprisal, EffectReprisal, 100, 0, 60_000));
        var rep = RabbitSwings(3, out int repLost, out _).FindAll(Landed);
        Check("Reprisal: each block reflects its blocked share (6 / 44)", true,
              rep.Count >= 3 && rep.TrueForAll(x => x.Res == 4 && x.Spikes == 6 && x.SpikesMsg == 44));
        Check("the rabbit lost the reflected damage", Total(rep, x => x.SpikesParam), repLost);
        c.StatusEffects.DelStatusEffectSilent(EffectReprisal);
        c.SetModifier(Mod.PALISADE_BLOCK_BONUS, 0);
        Equip(0, CharStats.SlotSub);
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectReprisal, EffectReprisal, 100, 0, 60_000));
        var repNoBlock = RabbitSwings(2, out _, out _).FindAll(Landed);
        Check("Reprisal without a shield (no block): none", true, repNoBlock.Count >= 2 && repNoBlock.TrueForAll(x => x.Res != 4 && x.Spikes == 0));
        c.StatusEffects.DelStatusEffectSilent(EffectReprisal);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectRetaliation, EffectRetaliation, 1, 0, 180_000));
        Pos(-1, 0);
        var ret = RabbitSwings(8, out int retLost, out _);
        // Retaliation fires on half the char's hit rate (at most 47 in 100), so eight swings can all go without one:
        // more rounds until one has (none in 48 more rounds is under 1 in 10^10)
        for (int i = 0; i < 48 && !ret.Exists(x => x.Spikes == 63); i++)
        {
            var more = Round(Rabbit);
            ret.AddRange(more.R);
            retLost += more.RabbitLost;
        }
        var retaliated = ret.FindAll(x => x.Spikes == 63);
        Check("Retaliation: some landed swings retaliated (63 / 536), each a hit", true,
              retaliated.Count > 0 && retaliated.TrueForAll(x => x.SpikesMsg == 536 && x.Res == 0));
        Check("the rabbit lost the retaliations' damage", Total(retaliated, x => x.SpikesParam), retLost);
        c.StatusEffects.DelStatusEffectSilent(EffectRetaliation);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectSulpor, EffectSulpor, 1, 0, 180_000));
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectBattuta, EffectBattuta, 100, 0, 90_000, 0, 15));
        Check("Battuta under Sulpor: INQUARTATA 100, shock parry spikes 15", "100/5/15",
              $"{c.GetMod(Mod.INQUARTATA)}/{c.GetMod(Mod.PARRY_SPIKES)}/{c.GetMod(Mod.PARRY_SPIKES_DMG)}");
        var bat = new List<Result>();
        for (int i = 0; i < 3; i++)
        {
            var r = Round(Rabbit);
            bat.AddRange(r.R);
        }
        var batParried = bat.FindAll(x => x.Res == 3);
        Check("each parry: the parry spikes deal 15 (5 / 44)", true,
              batParried.Count > 0 && batParried.TrueForAll(x => x.Msg == 70 && x.Spikes == 5 && x.SpikesMsg == 44 && x.SpikesParam == 15));
        c.StatusEffects.DelStatusEffectSilent(EffectBattuta);
        c.StatusEffects.DelStatusEffectSilent(EffectSulpor);

        Fresh();
        scripts.Lua.DoString($"xi.zones['{info.Name}'].mobs['{rabbit.Name}'].onSpikesDamage = function(mob, target, damage) return 1, 44, 9 end",
                             "=verify_defense");
        rabbit.SetMobMod(MobMod.AutoSpikes, 1);
        rabbit.SetModifier(Mod.SPIKES, 1);
        var auto = CharSwings(2, out _, out int autoCharLost).FindAll(Landed);
        Check("a mob's AUTO_SPIKES: its script's onSpikesDamage gives the spikes (1 / 44 / 9)", true,
              auto.Count >= 2 && auto.TrueForAll(x => x.Spikes == 1 && x.SpikesMsg == 44 && x.SpikesParam == 9));
        Check("the core deals none of it (the script does)", 0, autoCharLost);
        rabbit.SetMobMod(MobMod.AutoSpikes, 0);
        rabbit.SetModifier(Mod.SPIKES, 0);
        scripts.Lua.DoString($"xi.zones['{info.Name}'].mobs['{rabbit.Name}'].onSpikesDamage = nil", "=verify_defense");

        Console.WriteLine("Zanshin (CBattleEntity::OnAttack): a single-swing round's missed first swing swings again");
        Fresh();
        c.SetModifier(Mod.DOUBLE_ATTACK, 0);
        c.SetModifier(Mod.ZANSHIN, 100);
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(EffectPerfectDodge, EffectPerfectDodge, 1, 0, 30_000));
        bool zanOk = true;
        int zanRounds = 0;
        for (int i = 0; i < 4; i++)
        {
            var r = Round(CharId).R;
            if (r.Count == 0) continue;
            zanRounds++;
            zanOk &= r.Count == 2 && r.TrueForAll(x => x.Res == 1 && x.Msg == 32);
        }
        Check("dodged: each round two swings, the Zanshin one dodged too", "4/True", $"{zanRounds}/{zanOk}");
        rabbit.StatusEffects.DelStatusEffectSilent(EffectPerfectDodge);
        bool hitOnce = true;
        for (int i = 0; i < 4; i++)
        {
            var r = Round(CharId).R;
            if (r.Count > 0 && r[0].Res == 0) hitOnce &= r.Count == 1;
        }
        Check("a landed first swing: no Zanshin", true, hitOnce);
        c.SetModifier(Mod.ZANSHIN, 0);

        Console.WriteLine("TakePhysicalDamage's tail: stoneskin, Formless Strikes, bind, takeDamage");
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Stoneskin, EffectId.Stoneskin, 60, 0, 300_000));
        int ssLost = 0, ssLanded = 0;
        // the parry / guard / block rolls are Lua's (unseeded), and a guarded or blocked swing may deal nothing: rounds
        // until the stoneskin takes something
        for (int i = 0; i < 10 && (c.StatusEffects.GetStatusEffect(EffectId.Stoneskin)?.Power ?? 0) == 60; i++)
        {
            var r = Round(Rabbit);
            int before = c.StatusEffects.GetStatusEffect(EffectId.Stoneskin)?.Power ?? 0;
            ssLost += r.CharLost;
            ssLanded += r.R.FindAll(Landed).Count;
        }
        Check("Stoneskin 60: the rabbit's landed swings took no HP while it held", "True/0",
              $"{ssLanded > 0}/{(c.StatusEffects.HasStatusEffect(EffectId.Stoneskin) ? ssLost : 0)}");
        Check("its power down", true, (c.StatusEffects.GetStatusEffect(EffectId.Stoneskin)?.Power ?? 0) < 60);
        c.StatusEffects.DelStatusEffectSilent(EffectId.Stoneskin);

        Fresh();
        // a physical-only stoneskin (subtype 1) stops the char's blows, not Formless Strikes
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Stoneskin, EffectId.Stoneskin, 1000, 0, 300_000, 1));
        var phys = CharSwings(2, out int physLost, out _);
        Check("a physical-only stoneskin: the rabbit loses nothing", 0, physLost);
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectFormlessStrikes, EffectFormlessStrikes, 1, 0, 180_000));
        var formless = CharSwings(2, out int formlessLost, out _);
        Check("Formless Strikes goes through it (special damage)", "True/5", $"{formlessLost > 0}/{rabbit.LastHitAtkType}");
        c.StatusEffects.DelStatusEffectSilent(EffectFormlessStrikes);
        rabbit.StatusEffects.DelStatusEffectSilent(EffectId.Stoneskin);

        Fresh();
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Bind, EffectId.Bind, 1, 0, 60_000));
        CharSwings(2, out _, out _);
        Check("a landed blow breaks bind (95%)", false, rabbit.StatusEffects.HasStatusEffect(EffectId.Bind));

        Fresh();
        var taken = new List<string>();
        scripts.Lua.SetGlobal("reportTaken", l => { taken.Add(l.ToStringValue(1)); return 0; });
        scripts.Lua.DoString($"GetMobByID({Rabbit}):addListener('TAKE_DAMAGE', 'VERIFY_TAKE', function(mob, amount, attacker, atkType, dmgType) " +
                             "reportTaken(string.format('%d/%d/%d', amount, atkType, dmgType)) end)", "=verify_defense");
        var td = CharSwings(1, out _, out _).FindAll(Landed);
        Check("the TAKE_DAMAGE listener hears each blow: its damage, physical, slashing", true,
              td.Count > 0 && taken.Count == td.Count && taken.TrueForAll(t => t.EndsWith("/1/2")) &&
              string.Join(",", taken.ConvertAll(t => t.Split('/')[0])) == string.Join(",", td.ConvertAll(x => x.Param.ToString())));
        scripts.Lua.DoString($"GetMobByID({Rabbit}):removeListener('VERIFY_TAKE')", "=verify_defense");

        Console.WriteLine("Paralysis on a round, Consume Mana, Soul Eater, Feint, Fan Dance, Migawari");
        const ushort EffectSouleater = 63, EffectEvasionDown = 148, EffectFeint = 343, EffectFanDance = 411, EffectMigawari = 471,
            EffectConsumeMana = 599;
        // the actor's next BATTLE2, whatever its category
        BasicPacket NextAction(uint actor)
        {
            bool charSwings = actor == CharId;
            if (charSwings) rabbit.SwingAtMs = Never; else c.SwingAtMs = Never;
            for (int t = 0; t < 40; t++)
            {
                if (charSwings && c.SwingAtMs > now + 400) c.SwingAtMs = now + 400;
                if (!charSwings && rabbit.SwingAtMs > now + 400) rabbit.SwingAtMs = now + 400;
                rabbit.Tp = 0;
                Tick(400);
                var p = Of(0x028).Find(q => U(q, 40, 32) == actor);
                if (p != null) return p;
            }
            return null;
        }
        Fresh();
        c.SetModifier(Mod.PARALYZE, 100);
        var para = NextAction(CharId);
        Check("the char paralyzed: no round, the interrupt instead (magic finish 4, message 84)", "4/84",
              para == null ? "none" : $"{U(para, 82, 4)}/{Results(para)[0].Msg}");
        c.SetModifier(Mod.PARALYZE, 0);
        rabbit.SetModifier(Mod.PARALYZE, 100);
        var mobPara = NextAction(Rabbit);
        Check("the rabbit paralyzed: the same for a mob", "4/84",
              mobPara == null ? "none" : $"{U(mobPara, 82, 4)}/{Results(mobPara)[0].Msg}");
        rabbit.SetModifier(Mod.PARALYZE, 0);

        Fresh();
        c.Mp = 100;
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectConsumeMana, EffectConsumeMana, 1, 0, 60_000));
        CharSwings(1, out _, out _);
        Check("Consume Mana: a landed blow spends the MP and ends it", "0/False",
              $"{c.Mp}/{c.StatusEffects.HasStatusEffect(EffectConsumeMana)}");

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectSouleater, EffectSouleater, 1, 0, 60_000));
        CharSwings(1, out _, out int seLost);
        Check("Soul Eater: the char pays HP for its blows", true, seLost > 0);
        c.StatusEffects.DelStatusEffectSilent(EffectSouleater);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectFeint, EffectFeint, 30, 0, 60_000, 0, 50));
        int thProcBefore = rabbit.GetMod(Mod.TREASURE_HUNTER_PROC), feintEva = Melee.Eva(rabbit);
        CharSwings(1, out _, out _);
        Check("Feint: the rabbit's Evasion Down (30, held to its EVA) carrying TREASURE_HUNTER_PROC 50, Feint gone", $"{Math.Min(30, feintEva)}/50/False",
              $"{rabbit.StatusEffects.GetStatusEffect(EffectEvasionDown)?.Power}/{rabbit.GetMod(Mod.TREASURE_HUNTER_PROC) - thProcBefore}" +
              $"/{c.StatusEffects.HasStatusEffect(EffectFeint)}");
        rabbit.StatusEffects.DelStatusEffectSilent(EffectEvasionDown);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectFanDance, EffectFanDance, 5000, 0, 60_000));
        int fdLanded = 0;
        for (int i = 0; i < 8 && fdLanded == 0; i++) fdLanded += Round(Rabbit).R.FindAll(Landed).Count;
        Check("Fan Dance: each blow taken wears it down by 10 % (to 20 %)", $"True/{Math.Max(5000 - 1000 * fdLanded, 2000)}",
              $"{fdLanded > 0}/{c.StatusEffects.GetStatusEffect(EffectFanDance)?.Power}");
        c.StatusEffects.DelStatusEffectSilent(EffectFanDance);

        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectMigawari, EffectMigawari, 0, 0, 60_000, 0, 100));
        Result migawariHit = null;
        for (int i = 0; i < 8 && c.StatusEffects.HasStatusEffect(EffectMigawari); i++)
        {
            var r = Round(Rabbit);
            if (!c.StatusEffects.HasStatusEffect(EffectMigawari)) migawariHit = r.R.Find(Landed);
        }
        Check("Migawari: the first blow past its threshold lands for 0 and ends it", "False/0",
              $"{c.StatusEffects.HasStatusEffect(EffectMigawari)}/{migawariHit?.Param}");

        Console.WriteLine("Mikage, a Daken throw, Sange, a melee skill-up");
        const ushort EffectMikage = 502, EffectSange = 352;
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectMikage, EffectMikage, 1, 0, 60_000));
        c.SetModifier(Mod.UTSUSEMI, 3);
        var mikage = Round(CharId);
        Check("Mikage with 3 shadows: a swing each and the default one", true, mikage.R.Count >= 4);
        c.StatusEffects.DelStatusEffectSilent(EffectMikage);
        c.SetModifier(Mod.UTSUSEMI, 0);

        Fresh();
        byte jobBefore = c.MJob;
        c.MJob = MobStats.JobNin; // the shuriken is a level 18 ninja's
        byte ninLevel = c.JobLevels[MobStats.JobNin];
        c.JobLevels[MobStats.JobNin] = 30;
        Equip(4, CharStats.SlotAmmo);
        Check("the shuriken in the ammo slot", Shuriken, c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0);
        c.SetModifier(Mod.DAKEN, 100);
        var thrown = new List<Result>();
        for (int i = 0; i < 6 && thrown.Count == 0; i++) thrown = Round(CharId).R.FindAll(x => x.Msg is 352 or 353);
        Check("DAKEN 100: a throw each round, landing as a ranged hit (352 / 353)", true, thrown.Count == 1);
        Check("no Sange: the shuriken kept", 10u, c.FindItem(Container.Inventory, 4)?.Quantity ?? 0);
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectSange, EffectSange, 1, 0, 60_000));
        Round(CharId);
        Check("under Sange each throw spends a shuriken", 9u, c.FindItem(Container.Inventory, 4)?.Quantity ?? 0);
        c.StatusEffects.DelStatusEffectSilent(EffectSange);
        c.SetModifier(Mod.DAKEN, 0);
        Equip(0, CharStats.SlotAmmo);
        c.MJob = jobBefore;
        c.JobLevels[MobStats.JobNin] = ninLevel;

        Fresh();
        int swordSkill = c.RealSkills[Skill.Sword];
        c.RealSkills[Skill.Sword] = 10; // 1.0, far under the cap
        zone.Random01 = () => 0.0;
        CharSwings(1, out _, out _);
        Check("a landed blow's skill-up: the sword skill rises", true, c.RealSkills[Skill.Sword] > 10);
        zone.Random01 = Random.Shared.NextDouble;
        c.RealSkills[Skill.Sword] = (ushort)swordSkill;

        Fresh();
        const ushort EffectRestraint = 435;
        int wsdBefore = c.GetMod(Mod.ALL_WSDMG_FIRST_HIT);
        c.SetModifier(Mod.ENHANCES_RESTRAINT, 200);
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectRestraint, EffectRestraint, 0, 0, 60_000));
        var restraint = c.StatusEffects.GetStatusEffect(EffectRestraint);
        for (int i = 0; i < 12 && restraint.Power == 0; i++) Round(CharId);
        Check("Restraint: a round's first landed swing builds the WS damage bonus, ALL_WSDMG_FIRST_HIT with it", true,
              restraint.Power > 0 && c.GetMod(Mod.ALL_WSDMG_FIRST_HIT) - wsdBefore == restraint.Power);
        c.StatusEffects.DelStatusEffect(EffectRestraint);
        c.SetModifier(Mod.ENHANCES_RESTRAINT, 0);
        Check("Restraint gone: the bonus with it", wsdBefore, c.GetMod(Mod.ALL_WSDMG_FIRST_HIT));

        Check("rabbit still up", true, rabbit.Hp > 0);

        Console.WriteLine("Listeners: MELEE_SWING_HIT / MISS with a CAttack, CRITICAL_TAKE, ATTACK / ATTACKED, onItemEquip");
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal rabbit = GetMobByID({Rabbit})\n" + code, "=verify_defense");
            return string.Join(",", got);
        }
        Fresh();
        Lua("player:addListener('MELEE_SWING_HIT', 'T_HIT', function(a, t, attack) a:setLocalVar('tHit', a:getLocalVar('tHit') + 1) attack:setCritical(true) end) " +
            "player:addListener('MELEE_SWING_MISS', 'T_MISS', function(a, t, attack) a:setLocalVar('tMiss', a:getLocalVar('tMiss') + 1) end) " +
            "player:addListener('ATTACK', 'T_ATK', function(a, t, action) a:setLocalVar('tAtk', a:getLocalVar('tAtk') + 1) end) " +
            "rabbit:addListener('ATTACKED', 'T_ATKD', function(m, a, action) m:setLocalVar('tAtkd', m:getLocalVar('tAtkd') + 1) end) " +
            "rabbit:addListener('CRITICAL_TAKE', 'T_CRIT', function(m, a) m:setLocalVar('tCrit', m:getLocalVar('tCrit') + 1) end)");
        int rounds = 0, swingsSeen = 0, critMsgs = 0;
        for (int i = 0; i < 6; i++)
        {
            var (res, _, _) = Round(CharId);
            if (res.Count == 0) continue;
            rounds++;
            swingsSeen += res.Count;
            critMsgs += res.FindAll(x => x.Msg == 67).Count;
        }
        var counts = Lua("report(player:getLocalVar('tHit')) report(player:getLocalVar('tMiss')) report(player:getLocalVar('tAtk')) " +
                         "report(rabbit:getLocalVar('tAtkd')) report(rabbit:getLocalVar('tCrit'))").Split(',');
        int tHit = int.Parse(counts[0]), tMiss = int.Parse(counts[1]);
        Check("ATTACK / ATTACKED once a round each", $"{rounds}/{rounds}", $"{counts[2]}/{counts[3]}");
        Check("MELEE_SWING_HIT or _MISS on every swing", swingsSeen, tHit + tMiss);
        Check("setCritical(true) on each hit: each lands a critical (67), CRITICAL_TAKE on the rabbit for each", $"{tHit}/{tHit}", $"{critMsgs}/{counts[4]}");
        Check("some swings landed", true, tHit > 0);
        Lua("player:removeListener('T_HIT') player:removeListener('T_MISS') player:removeListener('T_ATK') " +
            "rabbit:removeListener('T_ATKD') rabbit:removeListener('T_CRIT')");

        Console.WriteLine("processActionEffectFlags after a melee round: the actor's ATTACK / ON_ATTACK, the target's ON_ATTACK, skipEvent");
        const ushort Mazurka = 219; // ATTACK | ON_ATTACK
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(Mazurka, Mazurka, 1, 0, 300_000));
        rabbit.StatusEffects.AddStatusEffect(new StatusEffect(Mazurka, Mazurka, 1, 0, 300_000));
        var mzRound = Round(CharId);
        Check("the char's round: its Mazurka and the rabbit's gone", "1/False/False",
              $"{(mzRound.R.Count > 0 ? 1 : 0)}/{c.StatusEffects.HasStatusEffect(Mazurka)}/{rabbit.StatusEffects.HasStatusEffect(Mazurka)}");
        Fresh();
        c.StatusEffects.AddStatusEffect(new StatusEffect(Mazurka, Mazurka, 1, 0, 300_000));
        var talker = info.Npcs[0];
        c.CurrentEvent = new EventInfo { EventId = 200, CanSkip = true, InterruptText = 7000, TargetEntity = talker };
        var skipRound = Round(Rabbit);
        var talk = Of(0x036);
        Check("the rabbit's round on a char in a skippable event: Mazurka gone, the event skipped (0x053 117, 0x052, 0x036 the NPC's interrupt text)",
              $"1/False/False/1/1/{talker.Id}/{7000 + 0x8000}",
              $"{(skipRound.R.Count > 0 ? 1 : 0)}/{c.StatusEffects.HasStatusEffect(Mazurka)}/{c.IsInEvent}/{Of(0x053).Count}/{Of(0x052).Count}/" +
              $"{(talk.Count == 1 ? BitConverter.ToUInt32(talk[0].Buffer, 4) : 0)}/{(talk.Count == 1 ? BitConverter.ToUInt16(talk[0].Buffer, 10) : 0)}");
        c.CurrentEvent = new EventInfo { EventId = 201, CanSkip = false };
        Round(Rabbit);
        Check("an event that cannot be skipped stays", true, c.IsInEvent);
        c.CurrentEvent = new EventInfo();

        Console.WriteLine("jail (jailutils::Add): the player controller gone");
        bool engagedBeforeJail = c.AttackTarget != null;
        Lua("player:jail()");
        Action(ActionId.AttackOff, rabbit.TargId);
        Check("jailed: AttackOff does nothing (CAIContainer::Disengage without a controller)", "True/True/True",
              $"{c.NoController}/{engagedBeforeJail}/{c.AttackTarget != null}");
        c.NoController = false;

        Equip(5, CharStats.SlotMain);
        Check("Chaosbringer in the main hand", Chaosbringer, c.Equipped[CharStats.SlotMain]?.ItemId ?? 0);
        Lua("player:addQuest(xi.questLog.BASTOK, xi.quest.id.bastok.BLADE_OF_DARKNESS)");
        rabbit.Hp = 1;
        for (int i = 0; i < 10 && rabbit.Hp > 0; i++) Round(CharId);
        Check("onItemEquip's ATTACK listener: the killing round counts for the quest", "1", Lua("report(player:getCharVar('ChaosbringerKills'))"));

        Check("no script errors", "", string.Join("\n", log.FindAll(l => !l.Contains("global scripts") && (l.Contains("lua:") || l.Contains("luautils::") || l.Contains("rror") || l.Contains("CLuaItem")))));
    }
}
