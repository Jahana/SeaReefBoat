using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// The client side of XI.Verify.Quests.Live: the zone host, its script engine and the clocks they read, the session of
// the char being driven, and the packets a client sends (login, moves, talk, event ends and updates, trades, gear,
// zone lines, attacks) with the reads of what came back (event packets, messages, the quest log, char_vars, items,
// key items, gil, fame, titles).
internal static partial class Program
{
    private static IDatabase db;
    private static ZoneHost zone;
    private static ScriptEngine scripts;
    private static LuaState lua;
    private static readonly List<string> log = new();
    private static readonly List<string> got = new();
    private static MapSession s;
    private static CharData c;
    private static List<BasicPacket> loginPackets;
    // the host's clocks: the server tick clock, the Vana'diel clock (the scripts' Vanadiel* globals read the same), and
    // the unix clock the char_vars expiries are checked against (moved on past a JST midnight by the tests)
    private static long now = 1_000_000;
    private static long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
    private static long unixOffset;
    // warnings a section expects of the scripts (not counted by ScriptErrors)
    private static readonly HashSet<string> expectedWarnings = new();

    private static void Setup(IDatabase database, string dataDir, Grades grades, CharStats stats)
    {
        db = database;
        zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => vanaTicks;
        zone.UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + unixOffset;
        zone.SendIpc = _ => { };
        scripts = new ScriptEngine(System.IO.Path.GetDirectoryName(dataDir), log.Add, log.Add);
        scripts.VanaTicksNow = () => vanaTicks;
        scripts.UnixNowMs = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + unixOffset * 1000;
        zone.InitScripts(scripts);
        lua = scripts.Lua;
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
    }

    private static string Lua(string code)
    {
        got.Clear();
        lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_quests");
        return string.Join(",", got);
    }
    private static List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.Contains("luautils::")) &&
                                                                 !expectedWarnings.Contains(l)).Distinct().ToList();
    private static void NoErrors(string what = "no script errors") => Check(what, "", string.Join(" | ", ScriptErrors()));

    private static void Send(ushort opcode, int size, Action<byte[]> fill = null)
    {
        var buf = new byte[size];
        buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
        fill?.Invoke(buf);
        zone.Dispatch(s, opcode, new BasicPacket(buf));
        zone.AfterParse(s);
    }
    private static List<BasicPacket> Of(int opcode) => new List<BasicPacket>(s.PacketList).FindAll(p => Opcode(p) == opcode);

    // the 0x00A login and 0x00C; the zone-in cutscene left open (the packets kept) or ended with 0x05B as the client
    // does
    private static void Login(bool endZoneIn = true)
    {
        if (s != null) zone.OnCharTimeout(s, false);
        s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        c = (CharData)s.Char;
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        loginPackets = new List<BasicPacket>(s.PacketList);
        if (!endZoneIn) return;
        for (int i = 0; i < 4 && c.IsInEvent; i++)
        {
            var ev = (ushort)c.CurrentEvent.EventId;
            Send(0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
        }
        s.PacketList.Clear();
    }
    // the wall clock moved on (the char_vars expiries, GetSystemTime, the JST days)
    private static void Later(long seconds) => unixOffset += seconds;
    // the char logged out (its session timed out)
    private static void Logout()
    {
        if (s != null) zone.OnCharTimeout(s, false);
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
        s.PacketList.Clear();
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
    // GP_CLI_COMMAND_EVENTEND: mode 0 ends the event, mode 1 asks for an update
    private static void EventEnd(uint id, int eventId, uint option = 0, ushort mode = 0)
    {
        var n = Npc(id);
        SendEventEnd(n.Id, n.TargId, eventId, option, mode);
    }
    // the same for the current event, from its own entity (the NPC, or the char for a cutscene of its own)
    private static void EndEvent(int eventId, uint option = 0, ushort mode = 0)
    {
        var (id, targ) = c.CurrentEvent?.TargetEntity switch
        {
            NpcEntity n => (n.Id, n.TargId),
            BattleEntity b => (b.Id, b.TargId),
            _ => (c.Id, c.TargId),
        };
        SendEventEnd(id, targ, eventId, option, mode);
    }
    // the current event finished with the option, whatever it is
    private static void Finish(uint option = 0) => EndEvent(c.IsInEvent ? c.CurrentEvent.EventId : 0, option);
    private static void SendEventEnd(uint id, ushort targ, int eventId, uint option, ushort mode)
    {
        s.PacketList.Clear();
        Send(0x05B, 20, b =>
        {
            BitConverter.GetBytes(id).CopyTo(b, 4);
            BitConverter.GetBytes(option).CopyTo(b, 0x08);
            BitConverter.GetBytes(targ).CopyTo(b, 0x0C);
            BitConverter.GetBytes(mode).CopyTo(b, 0x0E);
            BitConverter.GetBytes((ushort)eventId).CopyTo(b, 0x12);
        });
    }
    // GP_CLI_COMMAND_ITEM_TRANSFER: (inventory slot, quantity) offers
    private static void Trade(uint id, params (byte Slot, uint Qty)[] items)
    {
        var n = Npc(id);
        s.PacketList.Clear();
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
    // a trade of items by id: each quantity from the inventory slots that hold the item, in slot order (gil from slot 0)
    private static void TradeItems(uint id, params (ushort Item, uint Qty)[] items)
    {
        var offers = new List<(byte, uint)>();
        foreach (var (item, qty) in items)
        {
            if (item == 65535)
            {
                offers.Add((0, qty));
                continue;
            }
            uint left = qty;
            foreach (var i in c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == item).OrderBy(i => i.Slot))
            {
                if (left == 0) break;
                uint take = Math.Min(left, i.Quantity);
                offers.Add((i.Slot, take));
                left -= take;
            }
            if (left > 0) offers.Add((SlotOf(item), left));
        }
        Trade(id, offers.ToArray());
    }
    // GP_CLI_COMMAND_MAPRECT: the zone line at its origin, then the tick that zones the char and the next login
    private static void TakeZoneLine(string rect, bool endZoneIn = true)
    {
        var line = zone.ZoneInfoOf(c.Zone).ZoneLines[ZoneLine.PackId(rect)];
        MoveTo(line.OriginX, line.OriginY, line.OriginZ);
        Send(0x05E, 24, b => BitConverter.GetBytes(line.Id).CopyTo(b, 4));
        zone.Tick();
        Login(endZoneIn);
    }
    // out through the first zone line that has a line back, and back in
    private static void ZoneOutAndBack()
    {
        ushort here = c.Zone;
        var line = zone.ZoneInfoOf(here).ZoneLines.Values.First(l => l.DestinationZone != here &&
                                                                   zone.ZoneInfoOf(l.DestinationZone).ZoneLines.Values.Any(b => b.DestinationZone == here));
        TakeZoneLine(ZoneLine.IdText(line.Id));
        TakeZoneLine(LineTo(here));
    }
    private static string LineTo(ushort to) => ZoneLine.IdText(zone.ZoneInfoOf(c.Zone).ZoneLines.Values.First(l => l.DestinationZone == to).Id);
    // a zone change the scripts asked for (setPos to another zone): the tick that zones the char, the next login
    private static void Rezone(bool endZoneIn = true)
    {
        Tick(400);
        Login(endZoneIn);
    }
    // the walk between places that are not the quest's: a GM !pos
    private static void Warp(float x, float y, float z, ushort zoneId, bool endZoneIn = true)
    {
        Lua($"player:setPos({x}, {y}, {z}, 0, {zoneId})");
        Rezone(endZoneIn);
    }
    // a mob the scripts spawned killed by the char: claimed first if it is not (the char's swing, the char kept facing
    // it as the client does; onMobDeath gets a player only through the claim), then the damage, the death on the next
    // tick, the defeat (onMobDeath) a second on
    private static void KillByChar(uint mobId)
    {
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
        for (int i = 0; m.OwnerId != c.Id && i < 30; i++)
        {
            float dx = m.X - c.X, dz = m.Z - c.Z;
            byte dir = (byte)(short)(MathF.Atan2(dz, dx) * -(128.0f / MathF.PI));
            Send(0x015, 32, b =>
            {
                BitConverter.GetBytes(c.X).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(c.Y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(c.Z).CopyTo(b, C2sPos.OffY);
                b[C2sPos.OffDir] = dir;
            });
            Tick(1000);
        }
    }

    // the event packet the server sent: 0x032 / 0x034 as op:npc:event[:params]
    private static string Ev(List<BasicPacket> from = null)
    {
        var ev = (from ?? new List<BasicPacket>(s.PacketList)).LastOrDefault(p => Opcode(p) is 0x032 or 0x033 or 0x034);
        if (ev == null) return "none";
        var b = ev.Buffer;
        if (Opcode(ev) == 0x032) return $"032:{BitConverter.ToUInt32(b, 4)}:{BitConverter.ToUInt16(b, 12)}";
        if (Opcode(ev) == 0x033) return $"033:{BitConverter.ToUInt32(b, 4 + S2cEventStr.OffUniqueNo)}:{BitConverter.ToUInt16(b, 4 + S2cEventStr.OffEventPara)}";
        var pars = new List<string>();
        for (int i = 0; i < S2cEventNum.NumCount; i++) pars.Add(BitConverter.ToUInt32(b, 4 + S2cEventNum.OffNum + i * 4).ToString());
        while (pars.Count > 0 && pars[^1] == "0") pars.RemoveAt(pars.Count - 1);
        return $"034:{BitConverter.ToUInt32(b, 4 + S2cEventNum.OffUniqueNo)}:{BitConverter.ToUInt16(b, 4 + S2cEventNum.OffEventPara)}" +
               (pars.Count > 0 ? ":" + string.Join(",", pars) : "");
    }
    // the event's entity and id, whichever packet carried it
    private static string EvId(List<BasicPacket> from = null)
    {
        var p = Ev(from).Split(':');
        return p.Length >= 3 ? $"{p[1]}:{p[2]}" : p[0];
    }
    // the 0x056 quest page sent on the given port: how many, and whether the quest's bit is set in the last
    private static string QuestPage(int port, int quest)
    {
        var pages = Of(0x056).FindAll(p => BitConverter.ToUInt16(p.Buffer, 36) == port);
        return pages.Count == 0 ? "0" : $"{pages.Count}/{(BitConverter.ToUInt32(pages[^1].Buffer, 4 + quest / 32 * 4) & 1u << quest % 32) != 0}";
    }
    private static string Cur() => c.IsInEvent ? c.CurrentEvent.EventId.ToString() : "none";
    // the quest log as the C++ questlog_t: current bits then complete bits, per log
    private static string QuestBits(int logId, int quest)
    {
        var blob = DbBlob(db, "quests");
        int off = logId * CharData.QuestLogSize + quest / 8, bit = 1 << quest % 8;
        bool cur = off < blob.Length && (blob[off] & bit) != 0, done = off + 32 < blob.Length && (blob[off + 32] & bit) != 0;
        return $"{(cur ? 1 : 0)}{(done ? 1 : 0)}";
    }
    private static string Status(int logId, int quest) => Lua($"report(player:getQuestStatus({logId}, {quest}))");
    // the quest's status from the scripts and from chars.quests: 1/10 accepted, 2/01 completed
    private static string QStat(int logId, int quest) => $"{Status(logId, quest)}/{QuestBits(logId, quest)}";
    // the first column of the query's first row (the char id its one parameter, if it has one)
    private static string DbScalar(string q)
    {
        var r = q.Contains('?') ? Sql(db, q, BoundValue.Of(CharId)) : Sql(db, q);
        return r != null && r.Next() ? r.Get<string>(0u) ?? "null" : "-";
    }
    private static string Var(string name) => DbRow(db, "value", $"FROM char_vars WHERE charid = ? AND varname = '{name}'");
    private static string Qty(ushort itemId) => DbRow(db, "COALESCE(SUM(quantity), 0)", $"FROM char_inventory WHERE charid = ? AND itemId = {itemId}");
    private static string Fame() => DbRow(db, "fame_bastok", "FROM char_profile WHERE charid = ?");
    private static string FameOf(string area) => Lua($"report(player:getFame(xi.fameArea.{area}))");
    // item_equipment: the job's bit and the level
    private static bool CanEquip(int itemId, int job, int level)
    {
        var r = Sql(db, "SELECT jobs, level FROM item_equipment WHERE itemId = ?", BoundValue.Of(itemId));
        return r != null && r.Next() && (r.Get<uint>(0u) & 1u << job - 1) != 0 && r.Get<int>(1u) <= level;
    }
    private static string Gil() => DbRow(db, "quantity", "FROM char_inventory WHERE charid = ? AND location = 0 AND slot = 0");
    // keyitems_t: per table of 512, the keyList bits then the seenList bits
    private static string DbKeyItem(int ki)
    {
        var k = DbBlob(db, "keyitems");
        int off = ki / 512 * 128 + ki % 512 / 8, bit = 1 << ki % 8;
        bool has = off < k.Length && (k[off] & bit) != 0, seen = off + 64 < k.Length && (k[off + 64] & bit) != 0;
        return $"{(has ? 1 : 0)}{(seen ? 1 : 0)}";
    }
    private static string HasKi(string name) => DbKeyItem(Ki(name)).Substring(0, 1);
    private static int Ki(string name) => int.Parse(Lua($"report(xi.keyItem.{name})"));
    private static int TitleId(string name) => int.Parse(Lua($"report(xi.title.{name})"));
    private static ushort ItemId(string name) => ushort.Parse(Lua($"report(xi.item.{name})"));
    private static int QuestId(string area, string name) => int.Parse(Lua($"report(xi.quest.id.{area}.{name})"));
    // the 0x02A TALKNUMWORK messages as message:param0
    private static string Special() => string.Join(",", Of(0x02A).ConvertAll(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
    private static string Text(ushort zoneId, string name) => Lua($"report(zones[{zoneId}].text.{name})");
    private static bool DbTitle(int title)
    {
        var t = DbBlob(db, "titles");
        return title / 8 < t.Length && (t[title / 8] & 1 << title % 8) != 0;
    }
    // the current title and whether chars.titles has it
    private static string TitleNow(string name)
    {
        int t = TitleId(name);
        return $"{Lua("report(player:getTitle())") == t.ToString()}/{DbTitle(t)}";
    }
    // GP_CLI_COMMAND_EQUIP_SET: inventory slot -> equipment slot
    private static void Equip(byte invSlot, byte equipSlot)
    {
        s.PacketList.Clear();
        Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
    }
    private static string Worn(byte equipSlot) => Lua($"report(player:getEquipID({equipSlot}))");
    private static byte SlotOf(int itemId) => c.Items.First(i => i.Location == Container.Inventory && i.ItemId == itemId).Slot;
}
