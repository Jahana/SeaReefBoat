using System;
using System.Collections.Generic;
using System.IO;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.ItemUse.Live -- live gate for c2s 0x037 ITEM_USE (ZoneHost.ItemUse): CItemState's start (onItemCheck, the
// BATTLE2 item start, the claimed stack), its finish after the activation time (onItemUse per target, the item finish
// BATTLE2, one off the stack in char_inventory), the interrupt on a move, the end after the animation time and the
// zone-out cancel; charged equipment (charges, use and reuse delays, exdata saved); and the refusals (validation,
// too far, a target the item may not take, a claimed stack, an item that is not usable).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    // a char using items and one standing by, both in Southern San d'Oria
    private static readonly (uint Id, string Name, ushort Zone)[] Chars = { (990993, "Drinker", 230), (990994, "Bystander", 230) };

    private const long Epoch = 1_800_000_000;
    private const uint FourCcItemUse = 0x74696163, FourCcItemInterrupt = 0x74697073;

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

    // the first target's first result of a BATTLE2: actor/type/actionid/target/animation/param/message
    private static string Battle2(BasicPacket p)
    {
        ulong U(int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);
        return $"{U(40, 32)}/{U(82, 4)}/{U(86, 32):X}/{U(72, 6)}/{U(150, 32)}/{U(191, 12)}/{U(213, 17)}/{U(230, 10)}";
    }

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
        // the ring's own scripts lean on status effects, which are not ported: a stand-in that adds TP
        lua.DoString(@"xi.items = xi.items or {}
            xi.items.anniversary_ring = {
                onItemCheck = function(target, item, caster) return 0 end,
                onItemUse = function(target, user, item, action) target:addTP(7) action:messageID(target:getID(), 100) return 42 end,
            }
            -- potion drops call for the medicated effect, not ported either: a stand-in that only heals
            xi.items.bottle_of_potion_drops = {
                onItemCheck = function(target, item, caster) return 0 end,
                onItemUse = function(target) target:messageBasic(xi.msg.basic.RECOVERS_HP, 0, target:addHP(60)) end,
            }", "=stub");

        // Humes past their first login (the NEW_ADVENTURER title: no charCreate) that have seen the RoV opening
        // (Rhapsodies of Vana'diel, a zone-in cutscene in the starting cities: Resonance current, the other logs as a
        // new char has them), standing at the mog house exit (a char at 0,0,0 is moved there by the zone's onZoneIn)
        var titles = new byte[143];
        titles[206 >> 3] |= 1 << (206 & 7);
        var missions = new byte[15 * 70];
        for (int i = 0; i <= 3; i++) BitConverter.GetBytes((ushort)0xFFFF).CopyTo(missions, i * 70);
        BitConverter.GetBytes((ushort)101).CopyTo(missions, 6 * 70);
        BitConverter.GetBytes((ushort)1).CopyTo(missions, 13 * 70);
        missions[13 * 70 + 6] = 1;
        foreach (var (id, name, z) in Chars)
        {
            Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of("iu" + name), BoundValue.Of("x"), BoundValue.Of((byte)1));
            Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,moghouse,nation,gmlevel,titles,missions) " +
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(z), BoundValue.Of((ushort)0),
                BoundValue.Of(159.5f), BoundValue.Of(-2.0f), BoundValue.Of(160.0f),
                BoundValue.Of(0u), BoundValue.Of((byte)0), BoundValue.Of((byte)0), BoundValue.Of(titles), BoundValue.Of(missions));
            Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
            Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
                BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
            Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl, sjob, slvl) VALUES (?, 300, 50, 1, 30, 0, 0)", BoundValue.Of(id));
            Sql(db, "REPLACE INTO char_jobs SET charid = ?, unlocked = 2, war = 30", BoundValue.Of(id));
            Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        }
        uint me = Chars[0].Id;
        void Give(byte slot, ushort itemId, uint qty, byte[] extra = null) =>
            Sql(db, "INSERT INTO char_inventory(charid,location,slot,itemId,quantity,extra) VALUES (?,0,?,?,?,?)",
                BoundValue.Of(me), BoundValue.Of(slot), BoundValue.Of(itemId), BoundValue.Of(qty), BoundValue.Of(extra ?? new byte[24]));
        Give(1, 4112, 1);  // a potion (they do not stack)
        Give(2, 4202, 1);  // a daedalus wing
        Give(3, 640, 1);   // copper ore, not usable
        Give(4, 4148, 1);  // an antidote
        Give(6, 5327, 3);  // potion drops
        var ringExtra = new byte[24];
        ringExtra[1] = 10; // all 10 charges
        Give(5, 15793, 1, ringExtra); // an anniversary ring, equipped in the first ring slot
        Sql(db, "INSERT INTO char_equip(charid,equipslotid,slotid,containerid) VALUES (?,13,5,0)", BoundValue.Of(me));

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
        void Clear() { foreach (var s in ss) s?.PacketList.Clear(); log.Clear(); }
        bool Logged(string text) => log.Exists(l => l.Contains(text));
        void Login(int i)
        {
            ss[i] = new MapSession { CharId = Chars[i].Id, AccountId = Chars[i].Id };
            zone.OnLogin(ss[i]);
            cs[i] = (CharData)ss[i].Char;
            Send(i, 0x00A, 92, buf => BitConverter.GetBytes(Chars[i].Id).CopyTo(buf, 12));
            Send(i, 0x00C, 12);
        }
        // GP_CLI_COMMAND_ITEM_USE: UniqueNo u32 @4, ItemNum u32 @8, ActIndex u16 @12, PropertyItemIndex u8 @14, Category u32 @16
        void Use(byte slot, ushort actIndex = 0, uint category = 0, uint itemNum = 0) => Send(0, 0x037, 20, buf =>
        {
            BitConverter.GetBytes(itemNum).CopyTo(buf, 8);
            BitConverter.GetBytes(actIndex == 0 ? cs[0].TargId : actIndex).CopyTo(buf, 12);
            buf[14] = slot;
            BitConverter.GetBytes(category).CopyTo(buf, 16);
        });
        void Advance(long ms)
        {
            nowMs += ms;
            zone.Tick();
        }
        // 0x029 BATTLE_MESSAGE: target/param/value/message
        string Messages(int i) => string.Join(",", Of(i, 0x029).ConvertAll(p =>
            $"{BitConverter.ToUInt32(p.Buffer, 8)}/{BitConverter.ToInt32(p.Buffer, 12)}/{BitConverter.ToInt32(p.Buffer, 16)}/{BitConverter.ToUInt16(p.Buffer, 24)}"));
        string Qty(byte slot) => Scalar(db, "SELECT quantity FROM char_inventory WHERE charid = ? AND location = 0 AND slot = ?", BoundValue.Of(me), BoundValue.Of(slot)) ?? "none";

        Login(0);
        Login(1);
        var c = cs[0];
        var b = cs[1];
        c.X = 10; c.Y = 0; c.Z = 10;
        b.X = 12; b.Y = 0; b.Z = 10;
        Check("login: the potion drops and the ring", "3/10", $"{c.FindItem(Container.Inventory, 6)?.Quantity}/{c.FindItem(Container.Inventory, 5)?.Extra[1]}");
        var potion = c.FindItem(Container.Inventory, 1);
        var drops = c.FindItem(Container.Inventory, 6);

        // ---- onItemCheck refuses: a potion at full HP ----
        c.Hp = c.MaxHp;
        Clear();
        Use(1);
        Check("full HP: ITEM_UNABLE_TO_USE with the item id", $"{me}/4112/0/55", Messages(0));
        Check("full HP: no use started", "False/0/False", $"{c.UsingItem}/{Of(0, 0x028).Count}/{potion.InTransaction}");

        // ---- potion drops: start ----
        var heard = new List<string>();
        lua.SetGlobal("heard", l => { heard.Add(l.ToStringValue(1)); return 0; });
        lua.DoString($"local p = GetPlayerByID({me}) " +
            "p:addListener('ITEM_START', 'T_IS', function(target, item, action) heard('start:' .. item:getID() .. ':' .. target:getID()) end) " +
            "p:addListener('ITEM_USE', 'T_IU', function(user, item, action) heard('use:' .. item:getID()) end) " +
            "p:addListener('ITEM_STATE_EXIT', 'T_IX', function(user, item) heard('exit') end)", "=verify_itemuse");
        c.Hp = c.MaxHp - 100;
        Clear();
        Use(6);
        Check("start: using, the stack claimed", "True/True/UseItem", $"{c.UsingItem}/{drops.InTransaction}/{c.UContainer.Type}");
        var start = Of(0, 0x028);
        Check("start: BATTLE2 item start (actor, type 9, cait, 1 target, param item, msg 28)",
            $"{me}/9/{FourCcItemUse:X}/1/{me}/0/5327/28", start.Count == 1 ? Battle2(start[0]) : "none");
        Check("start: the bystander in range sees it", 1, Of(1, 0x028).Count);
        Check("start: ITEM_LIST greyed (NoSelect) and ITEM_SAME", "1/15/1",
            $"{Of(0, 0x01F).Count}/{(Of(0, 0x01F).Count > 0 ? Of(0, 0x01F)[0].Buffer[12] : -1)}/{Of(0, 0x01D).Count}");

        Clear();
        Use(1);
        Check("another item while one runs: UNABLE_TO_USE_ITEM", $"{me}/0/0/56", Messages(0));

        Clear();
        Use(6);
        Check("the claimed stack again: refused", true, Logged("trying to use a claimed item"));

        // ---- the drops finish after their 1.5s activation, then end (no animation time) ----
        Clear();
        Advance(1000);
        Check("before the activation: nothing", "0/True", $"{Of(0, 0x028).Count}/{c.UsingItem}");
        // a skippable event reached through the use (processActionEffectFlags' skipEvent on each char target)
        c.CurrentEvent = new EventInfo { EventId = 300, CanSkip = true };
        Advance(501);
        var finish = Of(0, 0x028);
        Check("finish: BATTLE2 item finish (type 5, actionid item, animation 30, param the HP)",
            $"{me}/5/{5327:X}/1/{me}/30/0/0", finish.Count == 1 ? Battle2(finish[0]) : "none");
        Check("finish: RECOVERS_HP from the script", $"{me}/0/60/24", Messages(0));
        Check("finish: HP up 60, one off the stack", $"{c.MaxHp - 40}/2/2", $"{c.Hp}/{drops.Quantity}/{Qty(6)}");
        Check("finish: the HP sent", true, Of(0, 0x0DF).Count + Of(0, 0x037).Count > 0);
        Check("finish: still in the state until the next tick", true, c.UsingItem);
        Check("finish: the user's skippable event skipped (0x053, 0x052)", "False/1/1", $"{c.IsInEvent}/{Of(0, 0x053).Count}/{Of(0, 0x052).Count}");
        Clear();
        Advance(1);
        Check("end: the state left, the stack released, the container empty", "False/False/Empty",
            $"{c.UsingItem}/{drops.InTransaction}/{c.UContainer.Type}");
        Check("ITEM_START / ITEM_USE / ITEM_STATE_EXIT heard in order", $"start:5327:{me},use:5327,exit", string.Join(",", heard));
        lua.DoString($"local p = GetPlayerByID({me}) p:removeListener('T_IS') p:removeListener('T_IU') p:removeListener('T_IX')", "=verify_itemuse");
        Check("end: ITEM_LIST normal, ITEM_ATTR, ITEM_SAME", "0/1/1",
            $"{(Of(0, 0x01F).Count > 0 ? Of(0, 0x01F)[0].Buffer[12] : -1)}/{Of(0, 0x020).Count}/{Of(0, 0x01D).Count}");

        // ---- interrupted by a move ----
        Clear();
        Use(1);
        c.X += 1;
        Clear();
        Advance(1001);
        var interrupt = Of(0, 0x028);
        Check("moved: BATTLE2 item interrupt (type 9, spit)", $"{me}/9/{FourCcItemInterrupt:X}/1/{me}/0/0/0",
            interrupt.Count == 1 ? Battle2(interrupt[0]) : "none");
        Check("moved: ITEM_FAILS_TO_ACTIVATE", $"{me}/4112/0/62", Messages(0));
        Check("moved: nothing used", $"{c.MaxHp - 40}/1", $"{c.Hp}/{Qty(1)}");
        Advance(1);
        Check("moved: the state ends and lets the stack go", "False/False", $"{c.UsingItem}/{potion.InTransaction}");

        // ---- a daedalus wing: TP, the last of its stack ----
        c.Tp = 0;
        Clear();
        Use(2);
        Advance(1001);
        Check("wing: not before its 2s activation", "0/True", $"{c.Tp}/{c.UsingItem}");
        Advance(1000);
        Check("wing: +1000 TP, the stack used up", "1000/none/True", $"{c.Tp}/{Qty(2)}/{c.FindItem(Container.Inventory, 2) == null}");
        Clear();
        Advance(1);
        var attr = Of(0, 0x020);
        Check("wing: an empty slot ITEM_ATTR, no ITEM_LIST", "1/0/0/0", $"{attr.Count}/{(attr.Count > 0 ? BitConverter.ToUInt16(attr[0].Buffer, 12) : -1)}/{Of(0, 0x01F).Count}/{(c.UsingItem ? 1 : 0)}");

        // ---- refusals ----
        Clear();
        Use(3);
        Check("copper ore: UNABLE_TO_USE_ITEM", $"{me}/0/0/56", Messages(0));
        Clear();
        Use(1, b.TargId);
        Check("a potion on another char (self only): CannotAttackTarget", $"{me}/0/0/446", Messages(0));
        b.X = 40;
        Clear();
        Use(1, b.TargId);
        Check("a target past 12 yalms: TooFarAway", $"{b.Id}/0/0/78", Messages(0));
        Clear();
        Use(1, 0x7FF);
        Check("no such target: nothing", "0/0", $"{Of(0, 0x029).Count}/{Of(0, 0x028).Count}");
        Clear();
        Use(1, category: 5);
        Check("a bad container refused", "True/False", $"{Logged("Category")}/{c.UsingItem}");
        Clear();
        Use(1, itemNum: 1);
        Check("ItemNum not 0 refused", "True/False", $"{Logged("ItemNum not 0")}/{c.UsingItem}");
        c.MoghouseId = c.Id;
        Clear();
        Use(1);
        Check("in a mog house refused", "True/False", $"{Logged("Player is in moghouse")}/{c.UsingItem}");
        c.MoghouseId = 0;
        potion.InTransaction = true;
        Clear();
        Use(1);
        Check("a claimed stack refused", "True/False", $"{Logged("trying to use a claimed item")}/{c.UsingItem}");
        potion.InTransaction = false;
        Clear();
        Use(9);
        Check("an empty slot: nothing", "0/False", $"{Of(0, 0x029).Count}/{c.UsingItem}");

        // ---- charged equipment: the use delay after equipping, a charge, the reuse delay ----
        var ring = c.FindItem(Container.Inventory, 5);
        Clear();
        Use(5);
        Check("ring: within its 15s use delay, UNABLE_TO_USE_ITEM", $"{me}/0/0/56/False", $"{Messages(0)}/{c.UsingItem}");
        // equipped at login with charges left: on the item recast list (slot 5 << 8 | inventory) for the use delay
        Check("ring: on the item recast list from being equipped", "15000",
            $"{c.Recasts?.GetRecast(RecastType.Item, 5 << 8)?.RecastTimeMs}");
        unix += 16;
        c.Tp = 0;
        Clear();
        Use(5);
        Check("ring: used while equipped, the stack not claimed", "True/False", $"{c.UsingItem}/{ring.InTransaction}");
        Clear();
        Advance(3001);
        var ringFinish = Of(0, 0x028);
        Check("ring: finish with the script's param and message", $"{me}/5/{15793:X}/1/{me}/76/42/100",
            ringFinish.Count == 1 ? Battle2(ringFinish[0]) : "none");
        uint lastUse = BitConverter.ToUInt32(ring.Extra, 4);
        Check("ring: a charge off, the last use stamped, TP from the script", $"9/{unix - XI.Common.VanaTime.EpochUnixSec}/7",
            $"{ring.Extra[1]}/{lastUse}/{c.Tp}");
        Check("ring: exdata saved", $"9/{lastUse}", ExtraOf(db, me, 5));
        Check("ring: used, back on the item recast list for its 3600s reuse delay", "3600000",
            $"{c.Recasts?.GetRecast(RecastType.Item, 5 << 8)?.RecastTimeMs}");
        Check("ring: still there", "1", Qty(5));
        Advance(1);
        Clear();
        unix += 60;
        Use(5);
        Check("ring: within its 3600s reuse delay, UNABLE_TO_USE_ITEM", $"{me}/0/0/56/False", $"{Messages(0)}/{c.UsingItem}");

        // ---- paralysis at the finish: the paralyzed message and the cancel, nothing used; the state holds the char ----
        string canChange = "";
        lua.SetGlobal("report", l => { canChange = l.ToStringValue(1); return 0; });
        Advance(4000);
        c.Hp = c.MaxHp - 100;
        Clear();
        Use(6);
        Check("drops again: started", true, c.UsingItem);
        lua.DoString($"report(tostring(GetPlayerByID({me}):canChangeState()))", "=verify_itemuse");
        Check("in the item state: canChangeState false", "false", canChange);
        c.SetModifier(Mod.PARALYZE, 100);
        Clear();
        Advance(1501);
        var para = Of(0, 0x028);
        Check("paralyzed: a magic finish with IS_PARALYZED_2 (84), then the item's cancel animation",
            $"2/{me}/4/0/1/{me}/508/0/84|{me}/9/{(uint)FourCC.ItemInterrupt:X}/1/{me}/508/0/0",
            para.Count == 2 ? $"2/{Battle2(para[0])}|{Battle2(para[1])}" : $"{para.Count}");
        Check("paralyzed: no message of its own, nothing used", "/2/2", $"{Messages(0)}/{drops.Quantity}/{Qty(6)}");
        c.SetModifier(Mod.PARALYZE, 0);
        Advance(1);
        Check("paralyzed: the state left, the stack released", "False/False", $"{c.UsingItem}/{drops.InTransaction}");
        lua.DoString($"report(tostring(GetPlayerByID({me}):canChangeState()))", "=verify_itemuse");
        Check("out of it: canChangeState true", "true", canChange);

        // ---- onItemDrop: the drops thrown into the recycle bin, then a load empties it ----
        var dropped = new List<string>();
        lua.SetGlobal("dropped", l => { dropped.Add(l.ToStringValue(1)); return 0; });
        lua.DoString("xi.items.bottle_of_potion_drops.onItemDrop = function(target, item, bin) dropped(item:getID() .. ':' .. tostring(bin)) end", "=verify_itemuse");
        Clear();
        Send(0, 0x028, 12, buf => { BitConverter.GetBytes(2u).CopyTo(buf, 4); buf[8] = 0; buf[9] = 6; });
        Check("thrown into the recycle bin: onItemDrop with the bin flag", "5327:true/True",
              $"{string.Join(",", dropped)}/{c.Items.Exists(i => i.Location == Container.RecycleBin && i.ItemId == 5327)}");
        dropped.Clear();
        var relog = new MapSession { CharId = me, AccountId = me };
        zone.OnLogin(relog);
        Check("a load empties the bin (charutils::EmptyRecycleBin): onItemDrop without the flag, the bin and its rows gone", "5327:false/False/0",
              $"{string.Join(",", dropped)}/{((CharData)relog.Char).Items.Exists(i => i.Location == Container.RecycleBin)}/" +
              $"{(Sql(db, "SELECT COUNT(*) AS n FROM char_inventory WHERE charid = ? AND location = 17", BoundValue.Of(me)) is var bin && bin.Next() ? bin.Get<long>("n") : -1)}");

        // ---- a zone-out while an item is in use: interrupted, nothing used ----
        Sql(db, "UPDATE char_stats SET hp = 1 WHERE charid = ?", BoundValue.Of(me));
        Clear();
        Use(4);
        var antidote = c.FindItem(Container.Inventory, 4);
        Check("antidote: started", "True/True", $"{c.UsingItem}/{antidote?.InTransaction}");
        Clear();
        zone.OnCharTimeout(ss[0], false);
        Check("timeout: ITEM_FAILS_TO_ACTIVATE and the stack released", $"{me}/4148/0/62/False/False",
            $"{Messages(0)}/{c.UsingItem}/{antidote?.InTransaction}");
        Check("timeout: the antidote kept", "1", Qty(4));

        Check("no script errors", "", string.Join(" | ", log.FindAll(l => l.StartsWith("lua:"))));
    }

    // char_inventory.extra: the remaining charges and TimeValue1
    private static string ExtraOf(IDatabase db, uint charId, byte slot)
    {
        var r = db.Execute("SELECT extra FROM char_inventory WHERE charid = ? AND location = 0 AND slot = ?", new[] { BoundValue.Of(charId), BoundValue.Of(slot) });
        if (r == null || !r.Next()) return "none";
        var e = r.GetBlobBytes("extra");
        return $"{e[1]}/{BitConverter.ToUInt32(e, 4)}";
    }
}
