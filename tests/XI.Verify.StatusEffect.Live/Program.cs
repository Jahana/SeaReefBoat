using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.StatusEffect.Live -- live gate for the status effect container (ZoneHost.StatusEffects, StatusEffects.cs):
// addStatusEffect from Lua and the effect scripts' gain / lose mods, the overwrite, negative and block rules, the icons
// in 0x037 / 0x063 and the re-send a change makes, the wear-off on expiry (0x029), the regen tick, the client's cancel
// (0x0F1) and its no_cancel refusal, a food item's own effect script through ITEM_USE, the CLuaStatusEffect methods,
// and char_effects across a logout (an offline-ticking effect counts down while the char is away).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a char gaining effects, in Southern San d'Oria
    private static readonly (uint Id, string Name, ushort Zone)[] Chars = { (990995, "Buffed", 230) };

    private const long Epoch = 1_800_000_000;
    private const ushort Haste = 33, Slow = 13, Blink = 36, CopyImage = 66, Protect = 40, Regen = 42, Food = 251, Visitant = 285, Poison = 3;

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
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        var loader = new CharLoader(db, Grades.Load(dataDir), AbilityTable.Load(db, _ => true), stats);
        var log = new List<string>();
        var zone = new ZoneHost(db, loader, dataDir, true, log.Add);
        zone.SendIpc = _ => { };
        long nowMs = 1_000_000, unix = Epoch;
        zone.NowMs = () => nowMs;
        zone.UnixNow = () => unix;
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;

        var (me, name, z) = Chars[0];
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(me), BoundValue.Of("se" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
        // a Hume past its first login (the NEW_ADVENTURER title: no charCreate) that has seen the RoV opening
        // (Rhapsodies of Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs as a
        // new char has them), standing at the mog house exit (a char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles,missions) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(me), BoundValue.Of(me), BoundValue.Of(name), BoundValue.Of(z), BoundValue.Of((ushort)0),
            BoundValue.Of(159.5f), BoundValue.Of(-2.0f), BoundValue.Of(160.0f),
            BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles), BoundValue.Of(missions));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(me));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(me), BoundValue.Of(me), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 30, 0, 0)", BoundValue.Of(me));
        Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(me));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(me));
        Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,extra) VALUES (?,0,1,4510,2,?)",
            BoundValue.Of(me), BoundValue.Of(new byte[24])); // two acorn cookies

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
        // 0x063 MISCDATA type 9: STATUS_ICONS
        List<BasicPacket> IconPackets() => Of(0x063).FindAll(p => BitConverter.ToUInt16(p.Buffer, 4) == 9);
        void Clear() { s.PacketList.Clear(); log.Clear(); }
        void Login()
        {
            s = new MapSession { CharId = me, AccountId = me };
            zone.OnLogin(s);
            c = (CharData)s.Char;
            Send(0x00A, 92, buf => BitConverter.GetBytes(me).CopyTo(buf, 12));
            Send(0x00C, 12);
        }
        void Advance(long ms)
        {
            nowMs += ms;
            zone.Tick();
        }
        // 0x029 BATTLE_MESSAGE: target/param/value/message
        string Messages() => string.Join(",", Of(0x029).ConvertAll(p =>
            $"{BitConverter.ToUInt32(p.Buffer, 8)}/{BitConverter.ToInt32(p.Buffer, 12)}/{BitConverter.ToInt32(p.Buffer, 16)}/{BitConverter.ToUInt16(p.Buffer, 24)}"));
        string Ids() => string.Join(",", c.StatusEffects.All.ConvertAllIds());
        string Icons(int n) => string.Join(",", new ArraySegment<byte>(c.StatusEffects.Icons, 0, n));

        lua.SetGlobal("me", l => { l.PushObject(c); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString("local p = me()\n" + code, "=verify_effects");
            return string.Join(",", got);
        }
        string Add(ushort id, string extra) =>
            Lua($"report(tostring(p:addStatusEffect({id}, {{ origin = p, {extra} }})))");

        Login();
        Check("login: no effects, every icon slot free", "0/255,255,255", $"{c.StatusEffects.All.Count}/{Icons(3)}");
        var icons0 = IconPackets();
        Check("login: STATUS_ICONS sent with nothing in it", "1/255/0", icons0.Count == 1 ? $"1/{icons0[0].Buffer[8]}/{BitConverter.ToUInt32(icons0[0].Buffer, 72)}" : $"{icons0.Count}");

        // ---- gain: the effect script's mods, the icons and the re-send on the next tick ----
        Clear();
        Check("haste 150 for 180s: gained", "true", Add(Haste, "power = 150, duration = 180"));
        Check("haste: the script's HASTE_MAGIC, the icon in slot 0", "150/33,255", $"{c.GetMod(Mod.HASTE_MAGIC)}/{Icons(2)}");
        Check("haste: hasStatusEffect, countEffect, the effect's methods", "true,1,33,150,180000,33,180000",
              Lua($@"report(tostring(p:hasStatusEffect({Haste}))) report(p:countEffect({Haste}))
                     local e = p:getStatusEffect({Haste})
                     report(e:getEffectType()) report(e:getPower()) report(e:getDuration()) report(e:getIcon()) report(e:getTimeRemaining())"));
        Check("haste: nothing sent until the tick", "0/0", $"{IconPackets().Count}/{Of(0x037).Count}");
        Advance(1);
        var icons = IconPackets();
        var status = Of(0x037);
        // PostTick: the effects' CHAR_STATUS, then the updatemask's (UPDATE_HP from the gain) a second
        Check("tick: CHAR_STATUS (0x037) twice, CHAR_SYNC (0x067) and STATUS_ICONS (0x063) once", "2/1/1",
              $"{status.Count}/{Of(0x067).Count}/{icons.Count}");
        Check("0x037: the icons at @4", "33/255", status.Count > 0 ? $"{status[0].Buffer[4]}/{status[0].Buffer[5]}" : "none");
        // 179.999s left when the tick ran: floor, plus the Vana'diel timestamp, in 1/60s
        uint expectTs = (uint)((179 + LoginPackets.VanadielTimestamp(unix)) * 60);
        Check("0x063: the icon and its end time", $"33/{expectTs}/255",
              icons.Count == 1 ? $"{BitConverter.ToUInt16(icons[0].Buffer, 8)}/{BitConverter.ToUInt32(icons[0].Buffer, 72)}/{BitConverter.ToUInt16(icons[0].Buffer, 10)}" : "none");
        Clear();
        Advance(1);
        Check("no change: nothing re-sent", 0, IconPackets().Count);

        // ---- overwrite (equal_higher), the negative, the block ----
        Check("haste 100: lower, refused", "false/150", $"{Add(Haste, "power = 100, duration = 180")}/{c.GetMod(Mod.HASTE_MAGIC)}");
        Clear();
        Check("haste 200: replaces it silently", "true/200/33/", $"{Add(Haste, "power = 200, duration = 180")}/{c.GetMod(Mod.HASTE_MAGIC)}/{Ids()}/{Messages()}");
        Check("slow 100 against haste 200: refused", "false/33", $"{Add(Slow, "power = 100, duration = 60")}/{Ids()}");
        Check("slow 300: haste goes, slow's -300 alone", "true/13/-300", $"{Add(Slow, "power = 300, duration = 60")}/{Ids()}/{c.GetMod(Mod.HASTE_MAGIC)}");
        Check("canGainStatusEffect: haste 100 against slow 300 no, 300 yes", "false,true",
              Lua($"report(tostring(p:canGainStatusEffect({Haste}, 100))) report(tostring(p:canGainStatusEffect({Haste}, 300)))"));
        Check("copy image (3 shadows): UTSUSEMI 3", "true/3", $"{Add(CopyImage, "power = 1, subPower = 3, duration = 300")}/{c.GetMod(Mod.UTSUSEMI)}");
        Check("blink while copy image is up: blocked", "false/False", $"{Add(Blink, "power = 3, duration = 300")}/{c.StatusEffects.HasStatusEffect(Blink)}");
        Check("delStatusEffect(slow): gone, its mod back to 0, the wear-off sent", $"true/0/{me}/{Slow}/0/206",
              $"{Lua($"report(tostring(p:delStatusEffect({Slow})))")}/{c.GetMod(Mod.HASTE_MAGIC)}/{Messages()}");
        Check("delStatusEffect again: false", "false", Lua($"report(tostring(p:delStatusEffect({Slow})))"));

        // ---- expiry ----
        int baseDef = c.GetMod(Mod.DEF); // the traits' DEF (a WAR has Defense Bonus)
        Check("protect 40 for 10s", "true/40", $"{Add(Protect, "power = 40, duration = 10")}/{c.GetMod(Mod.DEF) - baseDef}");
        Advance(9_000);
        Check("protect: still up at 9s", true, c.StatusEffects.HasStatusEffect(Protect));
        Clear();
        Advance(1_000);
        Check("protect at 10s: worn off (0x029 icon 40, msg 206), DEF back", $"False/{me}/{Protect}/0/206/0",
              $"{c.StatusEffects.HasStatusEffect(Protect)}/{Messages()}/{c.GetMod(Mod.DEF) - baseDef}");
        Check("protect: the icons re-sent without it", "1/66", $"{IconPackets().Count}/{(IconPackets().Count > 0 ? IconPackets()[0].Buffer[8] : -1)}");

        // ---- the regen tick: power per 3s ----
        c.Hp = 100;
        Check("regen 5 for 30s", "true/5", $"{Add(Regen, "power = 5, tick = 3, duration = 30")}/{c.GetMod(Mod.REGEN)}");
        long hpBefore = c.Hp;
        Advance(3_000);
        Advance(3_000);
        Check("regen: two ticks, +10 HP", hpBefore + 10, c.Hp);
        Check("regen: the ticks counted", "true", Lua($"report(tostring(p:getStatusEffect({Regen}):getTickCount() >= 1))"));

        // ---- the client's cancel ----
        Clear();
        Send(0x0F1, 8, buf => BitConverter.GetBytes(Regen).CopyTo(buf, 4));
        Check("0x0F1 regen: cancelled with its wear-off, REGEN back to 0", $"False/{me}/{Regen}/0/206/0",
              $"{c.StatusEffects.HasStatusEffect(Regen)}/{Messages()}/{c.GetMod(Mod.REGEN)}");

        // ---- poison: REGEN_DOWN through takeDamage (TAKE_DAMAGE, the last attacker cleared), then the death ----
        c.Hp = 50;
        c.LastAttackerId = 12345;
        Lua("p:addListener('TAKE_DAMAGE', 'verify_poison', function(t, amount, attacker) report(amount .. '/' .. tostring(attacker)) end)");
        Check("poison 30 for 60s: REGEN_DOWN 30", "true/30", $"{Add(Poison, "power = 30, duration = 60")}/{c.GetMod(Mod.REGEN_DOWN)}");
        got.Clear();
        Clear();
        Advance(3_000);
        Check("poison tick: -30 HP, TAKE_DAMAGE with no attacker, the last attacker cleared", "20/30/nil/0",
              $"{c.Hp}/{string.Join(",", got)}/{c.LastAttackerId}");
        Advance(3_000);
        Advance(1);
        Check("poison to 0 HP: dead, 'falls to the ground' (20)", $"0/{(int)Anim.Death}/True",
              $"{c.Hp}/{c.Animation}/{Messages().Contains($"{me}/0/0/20")}");
        Lua("p:removeListener('verify_poison')");
        c.StatusEffects.DelStatusEffectSilent(Poison);
        c.Hp = 300;
        c.Animation = 0;
        Advance(1);
        // the death took copy image (erased on death); the later checks keep it
        Add(CopyImage, "power = 1, subPower = 3, duration = 300");

        // ---- a food item: ITEM_USE, its own script's mods, no_cancel, IS_FULL ----
        Clear();
        Send(0x037, 20, buf =>
        {
            BitConverter.GetBytes(c.TargId).CopyTo(buf, 12);
            buf[14] = 1;
        });
        for (int i = 0; i < 10 && !c.StatusEffects.HasStatusEffect(Food); i++) Advance(500);
        Advance(1);
        if (!c.StatusEffects.HasStatusEffect(Food)) Console.WriteLine("  log: " + string.Join(" | ", log) + "  msgs: " + Messages());
        Check("acorn cookie: FOOD from the item's script (AQUAN_KILLER 10, SILENCERES 10, MPHEAL 3)", "True/10/10/3",
              $"{c.StatusEffects.HasStatusEffect(Food)}/{c.GetMod(Mod.AQUAN_KILLER)}/{c.GetMod(Mod.SILENCERES)}/{c.GetMod(Mod.MPHEAL)}");
        Check("food: the source is the item", "4510/180000",
              Lua($"local e = p:getStatusEffect({Food}) report(e:getSourceTypeParam()) report(e:getDuration())").Replace(",", "/"));
        Send(0x0F1, 8, buf => BitConverter.GetBytes(Food).CopyTo(buf, 4));
        Check("0x0F1 food: no_cancel, still there", true, c.StatusEffects.HasStatusEffect(Food));
        Clear();
        Send(0x037, 20, buf =>
        {
            BitConverter.GetBytes(c.TargId).CopyTo(buf, 12);
            buf[14] = 1;
        });
        Check("a second cookie: IS_FULL (246) from onItemCheck", $"{me}/4510/0/246", Messages());

        // ---- effect methods writing back ----
        Check("shell for the methods", "true", Add(41, "power = 20, duration = 60"));
        Check("setPower / setDuration / addEffectFlag", "99,5000,true",
              Lua($@"local e = p:getStatusEffect(41) e:setPower(99) e:setDuration(5000) e:addEffectFlag(0x00800)
                     report(e:getPower()) report(e:getDuration()) report(tostring(e:hasEffectFlag(0x00800)))"));
        Check("getStatusEffects: copy image, shell and food", $"{CopyImage},41,{Food}",
              Lua("for _, e in ipairs(p:getStatusEffects()) do report(e:getEffectType()) end"));
        Check("getMod / setMod / delMod", "7,4",
              Lua("p:setMod(xi.mod.STR, 7) report(p:getMod(xi.mod.STR)) p:delMod(xi.mod.STR, 3) report(p:getMod(xi.mod.STR))"));

        // ---- /heal: HEALING, its script's animation and ticks ----
        const ushort Healing = 794, LeaveGame = 795;
        Clear();
        c.Hp = 100;
        Send(0x0E8, 8, buf => buf[4] = 1);
        Check("/heal on: HEALING, animation 33, CHAR_STATUS from the handler and the UPDATE_HP, no script error", "True/33/2/0",
              $"{c.StatusEffects.HasStatusEffect(Healing)}/{c.Animation}/{Of(0x037).Count}/{log.FindAll(x => x.StartsWith("lua:")).Count}");
        if (log.Count > 0) Console.WriteLine("  log: " + string.Join(" | ", log));
        int TickCount(ushort id) => (int)(c.StatusEffects.GetStatusEffect(id)?.TickCount ?? 0);
        long healStart = nowMs;
        while (TickCount(Healing) < 1 && nowMs - healStart < 30_000) Advance(1_000);
        Check("/heal: the first tick (10s) heals nothing", "100", $"{c.Hp}");
        while (TickCount(Healing) < 2 && nowMs - healStart < 40_000) Advance(1_000);
        long second = nowMs - healStart;
        Check("/heal: the second tick at 20s (3s grid) heals 10 + HPHEAL", $"True/{100 + 10 + c.GetMod(Mod.HPHEAL)}",
              $"{second >= 20_000 && second <= 23_000}/{c.Hp}");
        Send(0x0E8, 8, buf => buf[4] = 1);
        Check("/heal on again: refused by the validator, the effect kept", "2", $"{TickCount(Healing)}");
        Clear();
        Send(0x0E8, 8, buf => buf[4] = 2);
        Check("/heal off: HEALING gone silently, animation 0", "False/0/", $"{c.StatusEffects.HasStatusEffect(Healing)}/{c.Animation}/{Messages()}");

        // ---- /logout: LEAVEGAME (power = kind) brings HEALING and counts down ----
        string SysMsgs() => string.Join(",", Of(0x053).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 12)}:{BitConverter.ToUInt32(p.Buffer, 4)}"));
        void ReqLogout(byte mode, byte kind) => Send(0x0E7, 8, buf => { buf[4] = mode; buf[6] = kind; });
        Clear();
        ReqLogout(0, 1);
        Check("/logout: LEAVEGAME power 1, HEALING with it, animation 33, EXECUTING_LOGOUT 30", "1/True/33/7:30",
              $"{c.StatusEffects.GetStatusEffect(LeaveGame)?.Power}/{c.StatusEffects.HasStatusEffect(Healing)}/{c.Animation}/{SysMsgs()}");
        long leaveStart = nowMs;
        Clear();
        while (TickCount(LeaveGame) < 1 && nowMs - leaveStart < 10_000) Advance(1_000);
        Check("/logout: the first 5s tick says 25", "7:25", SysMsgs());
        Clear();
        ReqLogout(0, 1);
        Check("/logout toggled off: LEAVEGAME and HEALING gone, animation 0", "False/False/0",
              $"{c.StatusEffects.HasStatusEffect(LeaveGame)}/{c.StatusEffects.HasStatusEffect(Healing)}/{c.Animation}");
        Clear();
        ReqLogout(3, 3);
        Check("/shutdown: power 3, EXECUTING_SHUTDOWN 30", "3/35:30", $"{c.StatusEffects.GetStatusEffect(LeaveGame)?.Power}/{SysMsgs()}");
        ReqLogout(1, 3);
        Check("crafted LogoutOn for the shutdown kind: nothing", "3", $"{c.StatusEffects.GetStatusEffect(LeaveGame)?.Power}");
        Clear();
        ReqLogout(1, 1);
        Check("/logout over /shutdown: only the kind changes", "1/", $"{c.StatusEffects.GetStatusEffect(LeaveGame)?.Power}/{SysMsgs()}");
        leaveStart = nowMs;
        while (TickCount(LeaveGame) < 1 && nowMs - leaveStart < 10_000) Advance(1_000);
        Check("the next tick speaks of logout", "7:25", SysMsgs());
        Send(0x0EA, 8, buf => buf[4] = 1);
        Check("/sit: HEALING dropped silently, LEAVEGAME with it, sitting", "False/False/47",
              $"{c.StatusEffects.HasStatusEffect(Healing)}/{c.StatusEffects.HasStatusEffect(LeaveGame)}/{c.Animation}");
        Send(0x0EA, 8, buf => buf[4] = 2);
        Check("stood up", "0", $"{c.Animation}");
        Check("asleep (a prevent-action effect)", "true", Add(2, "power = 1, duration = 30"));
        log.Clear();
        Send(0x0EA, 8, buf => buf[4] = 1);
        Check("/sit while asleep: Invalid state: PreventAction, still standing", "True/0",
              $"{log.Exists(l => l.Contains("Invalid state: PreventAction"))}/{c.Animation}");
        c.StatusEffects.DelStatusEffectSilent(2);

        // ---- the persist sweep: what changed goes to char_effects every 5 s while the char is online ----
        string HasteRow() => Scalar(db, "SELECT power FROM char_effects WHERE charid = ? AND effectid = ?", BoundValue.Of(me), BoundValue.Of(Haste)) ?? "none";
        Advance(5_000);
        Check("sweep: nothing hasted yet", "none", HasteRow());
        Check("haste 150 for 300s", "true", Add(Haste, "power = 150, duration = 300"));
        Advance(5_000);
        Check("sweep: the gained haste saved", "150", HasteRow());
        Sql(db, "UPDATE char_effects SET power = 99 WHERE charid = ? AND effectid = ?", BoundValue.Of(me), BoundValue.Of(Haste));
        Advance(5_000);
        Check("sweep: nothing changed, nothing written", "99", HasteRow());
        c.StatusEffects.GetStatusEffect(Haste).MarkPersistDirty();
        Advance(5_000);
        Check("sweep: a marked effect rewritten", "150", HasteRow());
        c.StatusEffects.DelStatusEffectSilent(Haste);
        Advance(5_000);
        Check("sweep: the lost haste gone", "none", HasteRow());

        // ---- char_effects across a logout ----
        Check("visitant 600s: gained", "true", Add(Visitant, "power = 1, duration = 600"));
        Check("haste 200 for 120s", "true", Add(Haste, "power = 200, duration = 120"));
        nowMs += 20_000; unix += 20;
        zone.OnCharTimeout(s, false);
        string Row(ushort id) => Scalar(db, "SELECT CONCAT(power, '/', duration, '/', flags & 0x2000000 > 0) FROM char_effects WHERE charid = ? AND effectid = ?",
                                        BoundValue.Of(me), BoundValue.Of(id)) ?? "none";
        Check("saved: haste with the 100s left", "200/100/0", Row(Haste));
        Check("saved: visitant whole (offline tick)", "1/600/1", Row(Visitant));
        Check("saved: food with its source", "4510", Scalar(db, "SELECT sourcetypeparam FROM char_effects WHERE charid = ? AND effectid = ?",
                                                               BoundValue.Of(me), BoundValue.Of(Food)));
        Check("saved: copy image with its 3 shadows", "3", Scalar(db, "SELECT subpower FROM char_effects WHERE charid = ? AND effectid = ?",
                                                                     BoundValue.Of(me), BoundValue.Of(CopyImage)));

        // 300s offline: the visitant's clock ran, haste's did not
        unix += 300;
        nowMs += 300_000;
        Login();
        Check("reload: the effects back", $"True/True/True/True", $"{c.StatusEffects.HasStatusEffect(Haste)}/{c.StatusEffects.HasStatusEffect(Visitant)}/" +
              $"{c.StatusEffects.HasStatusEffect(Food)}/{c.StatusEffects.HasStatusEffect(CopyImage)}");
        Check("reload: haste with its 100s, visitant with 280s", "100000/280000",
              $"{c.StatusEffects.GetStatusEffect(Haste).DurationMs}/{c.StatusEffects.GetStatusEffect(Visitant).DurationMs}");
        Check("reload: the mods from the scripts and the shadows", "200/10/3",
              $"{c.GetMod(Mod.HASTE_MAGIC)}/{c.GetMod(Mod.AQUAN_KILLER)}/{c.GetMod(Mod.UTSUSEMI)}");
        var loginIcons = IconPackets();
        Check("reload: STATUS_ICONS at login carries them", true, loginIcons.Count >= 1 && loginIcons[0].Buffer[8] != 255);

        // offline past the end: the visitant comes back for one second
        zone.OnCharTimeout(s, false);
        Sql(db, "UPDATE char_effects SET timestamp = timestamp - 10000 WHERE charid = ? AND effectid = ?", BoundValue.Of(me), BoundValue.Of(Visitant));
        Login();
        Check("reload: a run-out visitant gets one second", 1000L, c.StatusEffects.GetStatusEffect(Visitant)?.DurationMs ?? -1);

        // ---- the whole countdown: the sixth tick leaves the game ----
        Clear();
        ReqLogout(1, 1);
        long countdown = nowMs;
        var said = new List<string>();
        bool gone = false;
        while (!gone && nowMs - countdown < 45_000)
        {
            s.PacketList.Clear();
            Advance(1_000);
            said.AddRange(Of(0x053).ConvertAll(p => $"{BitConverter.ToUInt32(p.Buffer, 4)}"));
            gone = s.PacketList.Count > 0 && Opcode(s.PacketList.Last.Value) == 0x00B;
        }
        long took = nowMs - countdown;
        Check("countdown: 25..5, then logged out at 30s (3s grid)", "25,20,15,10,5/True/True", $"{string.Join(",", said)}/{gone}/{took >= 30_000 && took <= 33_000}");
        Check("logged out: HEALING and LEAVEGAME not saved", "0",
              Scalar(db, "SELECT COUNT(*) FROM char_effects WHERE charid = ? AND effectid IN (794, 795)", BoundValue.Of(me)));
    }

    private static List<ushort> ConvertAllIds(this IReadOnlyList<StatusEffect> all)
    {
        var ids = new List<ushort>();
        foreach (var e in all) if (!e.Deleted) ids.Add(e.Id);
        return ids;
    }
}
