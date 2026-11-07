using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// The client side of XI.Verify.QuestScripts.Live (adapted from XI.Verify.Quests.Live's): the zone host on the real
// database with its script engine and the clocks they read, the session of the char being driven, the packets a client
// sends (login, moves, talk, event ends, trades, GM warps) and the reads of what came back (event packets, messages,
// the quest log, char_vars, items, gil, fame).
internal static partial class Program
{
    private static IDatabase db;
    private static ZoneHost zone;
    private static ScriptEngine scripts;
    private static LuaState lua;
    private static readonly List<string> log = new();
    // the lines the script engine logged through its error action (ShowErrorFmt's level), also in log
    private static readonly List<string> errorLog = new();
    private static readonly List<string> got = new();
    private static MapSession s;
    private static CharData c;
    // the packets the char got that the session no longer holds (the harness clears them before each client packet, a
    // zone change replaces the session): a twin step compares them all
    private static readonly List<BasicPacket> pending_ = new();
    private static uint CharId;
    // the host's clocks, fixed unless a test moves them, so both runs of a twin see the same times
    private static long now = StartNow;
    private static long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
    // the wall clock (the 0x037's dead_counter2 and the char_vars expiries read it), fixed at the start
    private static readonly long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static void Setup(IDatabase database)
    {
        db = database;
        dataDir_ = FindDataDir();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        loader_ = new CharLoader(db, Grades.Load(dataDir_), AbilityTable.Load(db, _ => true, 1.0), stats);
        NewWorld();
    }

    private static string dataDir_;
    private static CharLoader loader_;
    private const long StartNow = 1_000_000;
    private const int Seed = 7;

    // a new zone host and script engine: no zone loaded yet, the host's clock back at its start, the mobs', the
    // scripts', the weather's, the status effects', synthesis', nominations' and the fishing contest's random numbers from
    // the same seed, and the given quest keys in CSHARP_QUESTS_OFF
    // before the containers load. Each run of a twin starts in such a world, so the mobs, the weather and the zone
    // scripts behave the same in both runs and only the quest's implementation differs.
    private static void NewWorld(params string[] csharpQuestsOff) => NewWorld(null, false, csharpQuestsOff);

    // the module lines of the committed modules/init.txt (its one entry), the worlds' default: the worktree's own
    // init.txt is the user's (git update-index --assume-unchanged), so a world never reads it
    private static readonly string[] CommittedModuleLines = { "custom/lua/test_npcs_in_gm_home.lua" };

    // the next worlds' ModuleInitLines (P11)
    private static IReadOnlyList<string> worldModuleLines_ = CommittedModuleLines;

    // hidden: require names the scripts engine skips (a Lua file gone); skipDiscovered: only the test quests are
    // registered
    private static void NewWorld(string[] hidden, bool skipDiscovered, params string[] csharpQuestsOff)
    {
        scripts?.Dispose();
        now = StartNow;
        zone = new ZoneHost(db, loader_, dataDir_, true, log.Add);
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => vanaTicks;
        zone.UnixNow = () => unixNow;
        zone.SendIpc = _ => { };
        zone.MobRng = new Random(Seed);
        var weather = new Random(Seed);
        zone.WeatherRandom = (min, max) => weather.Next(min, max);
        zone.SeedEffectRng(Seed);
        var synth = new Random(Seed);
        zone.Random01 = synth.NextDouble;
        zone.RandomBelow = new Random(Seed).Next;
        var contest = new Random(Seed);
        zone.ContestRandom = (min, max) => contest.Next(min, max);
        foreach (var key in csharpQuestsOff) zone.CSharpQuestsOff.Add(key);
        // platform round P11: the module lines the world starts with (the committed init.txt's unless a test sets them)
        zone.ModuleInitLines = worldModuleLines_;
        scripts = new ScriptEngine(Path.GetDirectoryName(dataDir_), log.Add, line => { log.Add(line); errorLog.Add(line); });
        if (hidden != null) scripts.HiddenContainerFiles = new HashSet<string>(hidden, StringComparer.Ordinal);
        // the fixture Lua quests, listed as Lua quest files of scripts/quests (Program.cs FixtureLuaFile)
        scripts.ExtraContainerFiles = FixtureContainerFiles;
        zone.SkipDiscoveredQuests = skipDiscovered;
        scripts.SeedRandom(Seed);
        scripts.VanaTicksNow = () => vanaTicks;
        scripts.UnixNowMs = () => unixNow * 1000;
        zone.InitScripts(scripts);
        lua = scripts.Lua;
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        InstallReturnRecorder();
    }

    private static string Lua(string code)
    {
        got.Clear();
        lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_questscripts");
        return string.Join(",", got);
    }
    private static List<string> ScriptErrors() => log.Where(l => (l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") ||
                                                                 l.Contains("luautils::") || l.Contains("Error running")).Distinct().ToList();

    private static ResultSet Sql(IDatabase database, string q, params BoundValue[] a) => database.Execute(q, a);

    private static string DbRow(string cols, string from)
    {
        var r = Sql(db, $"SELECT {cols} {from}", BoundValue.Of(CharId));
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
    }

    private static byte[] DbBlob(string col)
    {
        var r = Sql(db, $"SELECT {col} FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.GetBlobBytes(col) ?? Array.Empty<byte>() : Array.Empty<byte>();
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    private static void Send(ushort opcode, int size, Action<byte[]> fill = null)
    {
        var buf = new byte[size];
        buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
        fill?.Invoke(buf);
        zone.Dispatch(s, opcode, new BasicPacket(buf));
        zone.AfterParse(s);
    }
    // the session's packets so far, kept for the step's record, the session's list emptied
    private static void Collect()
    {
        if (s == null) return;
        pending_.AddRange(s.PacketList);
        s.PacketList.Clear();
    }
    private static List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);

    // the 0x00A login and 0x00C, the zone-in cutscene ended with 0x05B as the client does
    private static void Login()
    {
        Collect();
        if (s != null) zone.OnCharTimeout(s, false);
        Collect();
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
        Collect();
    }
    private static void Logout()
    {
        Collect();
        if (s != null) zone.OnCharTimeout(s, false);
        Collect();
        pending_.Clear();
        s = null;
        c = null;
    }
    // the zone's timers: the clock moved on, then a tick
    private static void Tick(int ms = 1000)
    {
        now += ms;
        zone.Tick();
    }
    // GP_CLI_COMMAND_POS: walk to the place
    private static void MoveTo(float x, float y, float z)
    {
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
        });
    }
    private static ZoneInfo Here => c.Instance?.Info ?? zone.ZoneInfoOf(c.Zone);
    private static NpcEntity Npc(uint id) => Here.NpcById[id];
    private static uint NpcId(string name) => Here.Npcs.First(n => n.Name == name).Id;
    private static void Near(uint id)
    {
        var n = Npc(id);
        MoveTo(n.X + 1.5f, n.Y, n.Z);
    }
    // GP_CLI_COMMAND_ACTION Talk
    private static void Talk(uint id)
    {
        var n = Npc(id);
        Collect();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(n.Id).CopyTo(b, 4);
            BitConverter.GetBytes(n.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
        });
    }
    // walk up to the NPC (by name) and talk to it
    private static uint TalkTo(string name)
    {
        uint id = NpcId(name);
        Near(id);
        Talk(id);
        return id;
    }
    // GP_CLI_COMMAND_EVENTEND for the current event, from its own entity (the NPC, or the char for its own cutscene)
    private static void EndEvent(int eventId, uint option = 0, ushort mode = 0)
    {
        var (id, targ) = c.CurrentEvent?.TargetEntity switch
        {
            NpcEntity n => (n.Id, n.TargId),
            BattleEntity b => (b.Id, b.TargId),
            _ => (c.Id, c.TargId),
        };
        Collect();
        Send(0x05B, 20, b =>
        {
            BitConverter.GetBytes(id).CopyTo(b, 4);
            BitConverter.GetBytes(option).CopyTo(b, 0x08);
            BitConverter.GetBytes(targ).CopyTo(b, 0x0C);
            BitConverter.GetBytes(mode).CopyTo(b, 0x0E);
            BitConverter.GetBytes((ushort)eventId).CopyTo(b, 0x12);
        });
    }
    // the current event finished with the option, whatever it is
    private static void Finish(uint option = 0) => EndEvent(c.IsInEvent ? c.CurrentEvent.EventId : 0, option);
    // GP_CLI_COMMAND_ITEM_TRANSFER: (inventory slot, quantity) offers
    private static void Trade(uint id, params (byte Slot, uint Qty)[] items)
    {
        var n = Npc(id);
        Collect();
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
    // a trade of items by id: each quantity from the inventory slots that hold the item, in slot order
    private static void TradeItems(uint id, params (int Item, uint Qty)[] items)
    {
        var offers = new List<(byte, uint)>();
        foreach (var (item, qty) in items)
        {
            uint left = qty;
            foreach (var i in c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == item).OrderBy(i => i.Slot))
            {
                if (left == 0) break;
                uint take = Math.Min(left, i.Quantity);
                offers.Add((i.Slot, take));
                left -= take;
            }
        }
        Trade(id, offers.ToArray());
    }
    // a GM !pos: the char set down at the place, in another zone through the tick that zones it and the next login (the
    // numbers written invariantly: Lua reads 1.5, never 1,5)
    private static void Warp(float x, float y, float z, ushort zoneId)
    {
        Lua(string.Format(CultureInfo.InvariantCulture, "player:setPos({0}, {1}, {2}, 0, {3})", x, y, z, zoneId));
        Tick(400);
        Login();
    }
    // GP_CLI_COMMAND_MAPRECT: the zone line at its origin, then the tick that zones the char and the next login. The
    // line's arrival slot is put back to the first before (it rotates per use), so a take lands on the same spot
    // however often the line was taken before, in this run or in a world a test kept
    private static void TakeZoneLine(string rect)
    {
        var line = zone.ZoneInfoOf(c.Zone).ZoneLines[ZoneLine.PackId(rect)];
        line.ResetSpawnSlot();
        MoveTo(line.OriginX, line.OriginY, line.OriginZ);
        Send(0x05E, 24, b => BitConverter.GetBytes(line.Id).CopyTo(b, 4));
        zone.Tick();
        Login();
    }
    // the first zone line from here to the zone
    private static string LineTo(ushort to) => ZoneLine.IdText(zone.ZoneInfoOf(c.Zone).ZoneLines.Values.First(l => l.DestinationZone == to).Id);

    // the current event finished (option 0 unless given), when there is one: an NPC's default may be a message
    private static void FinishIfEvent(uint option = 0)
    {
        if (c.IsInEvent) Finish(option);
    }
    // through the zone lines, zone by zone, to the last one given
    private static void Route(params ushort[] zones)
    {
        foreach (var to in zones) TakeZoneLine(LineTo(to));
    }
    // GP_CLI_COMMAND_EQUIP_SET: the inventory slot's item worn in the equipment slot
    private static void Equip(byte invSlot, byte equipSlot)
    {
        Collect();
        Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
    }
    // GP_CLI_COMMAND_SCENARIOITEM: the key item looked at in the key item menu (seen)
    private static void SeeKeyItem(int keyItem)
    {
        Collect();
        Send(0x064, 76, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(1u << keyItem % 32).CopyTo(b, 8 + keyItem % 512 / 32 * 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, 72);
            BitConverter.GetBytes((ushort)(keyItem / 512)).CopyTo(b, 74);
        });
    }
    // the inventory filled with Bronze Swords (one a slot), and those taken out again: a GM's items, the same in both runs
    private static void FillInventory()
    {
        Collect();
        Lua("for i = 1, 80 do if player:getFreeSlotsCount() == 0 then break end player:addItem(xi.item.BRONZE_SWORD) end");
    }
    private static void FreeInventory()
    {
        Collect();
        Lua("for i = 1, 80 do if not player:hasItem(xi.item.BRONZE_SWORD) then break end player:delItem(xi.item.BRONZE_SWORD, 1) end");
    }
    // walked to the place, then the zone's ticks (a trigger area is entered on the tick after the move)
    private static void WalkTo(float x, float y, float z, int ticks = 2)
    {
        Collect();
        MoveTo(x, y, z);
        for (int i = 0; i < ticks; i++) Tick(400);
    }
    private static uint MobId(string name) => Here.Mobs.First(m => m.Name == name).Id;
    // a mob the scripts spawned killed by the char: claimed first if it is not (the char's swing; onMobDeath gets a
    // player only through the claim), then the damage, the death on the next tick, the defeat (onMobDeath) a second on
    private static void KillByChar(uint mobId)
    {
        Collect();
        ClaimBySwing(mobId);
        Lua($"local mob = GetMobByID({mobId}, player:getInstance()) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
        Tick(1000);
        Tick(1000);
    }
    private static void ClaimBySwing(uint mobId)
    {
        var m = Here.MobById[mobId];
        if (m.OwnerId == c.Id) return;
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
        for (int i = 0; m.OwnerId != c.Id && i < 30; i++) Tick(1000);
    }

    // the event the server started (0x032 / 0x034) as entity:event, "none" without one
    private static string EvId()
    {
        var ev = new List<BasicPacket>(s.PacketList).LastOrDefault(p => Opcode(p) is 0x032 or 0x034);
        if (ev == null) return "none";
        var b = ev.Buffer;
        return Opcode(ev) == 0x032
            ? $"{BitConverter.ToUInt32(b, 4)}:{BitConverter.ToUInt16(b, 12)}"
            : $"{BitConverter.ToUInt32(b, 4 + S2cEventNum.OffUniqueNo)}:{BitConverter.ToUInt16(b, 4 + S2cEventNum.OffEventPara)}";
    }
    // the quest log as the C++ questlog_t: current bits then complete bits, per log
    private static string QuestBits(int logId, int quest)
    {
        var blob = DbBlob("quests");
        int off = logId * CharData.QuestLogSize + quest / 8, bit = 1 << quest % 8;
        bool cur = off < blob.Length && (blob[off] & bit) != 0, done = off + 32 < blob.Length && (blob[off + 32] & bit) != 0;
        return $"{(cur ? 1 : 0)}{(done ? 1 : 0)}";
    }
    private static string Var(string name) => DbRow("value", $"FROM char_vars WHERE charid = ? AND varname = '{name}'");
    private static string Qty(int itemId) => DbRow("COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND itemId = {itemId}");
    private static string Fame() => DbRow("fame_bastok", "FROM char_profile WHERE charid = ?");
    private static string Gil() => DbRow("quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0");
    private static string HasKi(int keyItem) => Lua($"report(tostring(player:hasKeyItem({keyItem})))");
    // the 0x02A TALKNUMWORK messages as message:param0
    private static string Special() => string.Join(",", Of(0x02A).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
}
