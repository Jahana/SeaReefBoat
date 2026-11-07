using System;
using System.Collections.Generic;
using XI.Map;

namespace XI.Zone
{
    public enum EventType
    {
        Normal, Cutscene, OptionalCutscene,
    }

    // EventInfo (and EventPrep, which is its first two fields): eventId -1 is no event
    public sealed class EventInfo
    {
        // the NPC (or mob) the event comes from
        public object TargetEntity;
        public string ScriptFile = "";
        public int EventId = -1;
        public int Option;
        public readonly SortedDictionary<int, uint> Params = new SortedDictionary<int, uint>();
        public readonly SortedDictionary<int, string> Strings = new SortedDictionary<int, string>();
        public short TextTable = -1;
        public EventType Type;
        public List<int> CutsceneOptions = new List<int>();
        public short InterruptText;
        public uint EventFlags;
        public bool CanSkip, IsHidden;

        public bool HasCutsceneOption(int option) => CutsceneOptions.Contains(option);

        public void Reset()
        {
            TargetEntity = null;
            ScriptFile = "";
            EventId = -1;
            Option = 0;
            Params.Clear();
            Strings.Clear();
            TextTable = -1;
            Type = EventType.Normal;
            CutsceneOptions = new List<int>();
            InterruptText = 0;
            EventFlags = 0;
            CanSkip = false;
            IsHidden = false;
        }
    }

    // The event and NPC-message S2C packets. A packet's id/size header is the first 4 bytes, so body offsets are +4.
    public static class EventPackets
    {
        private static (uint Id, ushort TargId) Target(CharData c, EventInfo e) =>
            e.TargetEntity switch
            {
                NpcEntity n => (n.Id, n.TargId),
                BattleEntity b => (b.Id, b.TargId),
                _ => (c.Id, c.TargId),
            };

        // GP_SERV_COMMAND_EVENTNUM (0x034)
        public static BasicPacket EventNum(CharData c, EventInfo e)
        {
            var w = new PacketWriter(0x034, 4 + S2cEventNum.SizeOf);
            var (id, targ) = Target(c, e);
            w.U32(4 + S2cEventNum.OffUniqueNo, id);
            foreach (var (i, v) in e.Params)
                if (i < S2cEventNum.NumCount) w.U32(4 + S2cEventNum.OffNum + i * 4, v);
            w.U16(4 + S2cEventNum.OffActIndex, targ);
            w.U16(4 + S2cEventNum.OffEventNum, c.Zone);
            w.U16(4 + S2cEventNum.OffEventPara, e.EventId);
            w.U16(4 + S2cEventNum.OffMode, e.EventFlags != 0 ? (int)(e.EventFlags & 0xFFFF) : 8);
            w.U16(4 + S2cEventNum.OffEventNum2, e.TextTable != -1 ? e.TextTable : c.Zone);
            if (e.EventFlags != 0) w.U16(4 + S2cEventNum.OffEventPara2, (int)(e.EventFlags >> 16));
            return w.Packet;
        }

        // GP_SERV_COMMAND_EVENT (0x032): no parameters
        public static BasicPacket Event(CharData c, EventInfo e)
        {
            var w = new PacketWriter(0x032, 4 + 16);
            var (id, targ) = Target(c, e);
            w.U32(4, id);
            w.U16(8, targ);
            w.U16(10, c.Zone);
            w.U16(12, e.EventId);
            w.U16(14, (int)(e.EventFlags & 0xFFFF));
            w.U16(16, c.Zone);
            w.U16(18, (int)(e.EventFlags >> 16));
            return w.Packet;
        }

        // GP_SERV_COMMAND_EVENTSTR (0x033): four strings of up to 15 characters, then the parameters
        public static BasicPacket EventStr(CharData c, EventInfo e)
        {
            var w = new PacketWriter(0x033, 4 + S2cEventStr.SizeOf);
            var (id, targ) = Target(c, e);
            w.U32(4 + S2cEventStr.OffUniqueNo, id);
            w.U16(4 + S2cEventStr.OffActIndex, targ);
            w.U16(4 + S2cEventStr.OffEventNum, c.Zone);
            w.U16(4 + S2cEventStr.OffEventPara, e.EventId);
            w.U16(4 + S2cEventStr.OffMode, e.EventFlags != 0 ? (int)(e.EventFlags & 0xFFFF) : 8);
            foreach (var (i, s) in e.Strings)
                if (i >= 0 && i < S2cEventStr.StringRows) w.Name(4 + S2cEventStr.OffString + i * S2cEventStr.StringRowStride, s, S2cEventStr.StringCols - 1);
            foreach (var (i, v) in e.Params)
                if (i < 8) w.U32(4 + 76 + i * 4, v);
            return w.Packet;
        }

        // GP_SERV_COMMAND_EVENTUCOFF (0x052): CancelEvent carries the event being cancelled
        public static BasicPacket Eventucoff(CharData c, EventucoffMode mode)
        {
            var w = new PacketWriter(0x052, 4 + S2cEventucoff.SizeOf);
            uint m = (uint)mode;
            if (mode == EventucoffMode.CancelEvent) m |= (uint)(c.CurrentEvent.EventId << 8);
            w.U32(4, m);
            return w.Packet;
        }

        // GP_SERV_COMMAND_PENDINGNUM (0x05C)
        public static BasicPacket PendingNum(IEnumerable<(int Index, uint Value)> values)
        {
            var w = new PacketWriter(0x05C, 4 + S2cPendingnum.SizeOf);
            foreach (var (i, v) in values)
                if (i < S2cPendingnum.NumCount) w.U32(4 + S2cPendingnum.NumOffset(i), v);
            return w.Packet;
        }

        // GP_SERV_COMMAND_TALKNUMWORK (0x02A): a char speaker without its name shown is flagged 0x8000; a type
        // shortens the packet when no name is shown
        public static BasicPacket TalkNumWork(uint id, ushort targId, string name, bool isChar, ushort message,
                                              uint p0, uint p1, uint p2, uint p3, bool showName, byte? type = null)
        {
            var w = new PacketWriter(0x02A, 0x40);
            w.U32(4, id);
            w.U32(8, p0);
            w.U32(12, p1);
            w.U32(16, p2);
            w.U32(20, p3);
            w.U16(24, targId);
            w.U8(28, type ?? 0);
            if (showName) w.Name(30, name, 31);
            else if (isChar) message += 0x8000;
            w.U16(26, message);
            if (type.HasValue && !showName) w.SetSize(0x20);
            return w.Packet;
        }

        // GP_SERV_COMMAND_TALKNUM (0x036)
        public static BasicPacket TalkNum(uint id, ushort targId, bool isChar, ushort message, bool showName, byte mode)
        {
            var w = new PacketWriter(0x036, 16);
            w.U32(4, id);
            w.U16(8, targId);
            w.U16(10, isChar || !showName ? message + 0x8000 : message);
            w.U8(12, mode);
            return w.Packet;
        }

        // GP_SERV_COMMAND_ITEM_NUM (0x01E)
        public static BasicPacket ItemNum(Container location, byte slot, uint quantity)
        {
            var w = new PacketWriter(0x01E, 12);
            w.U32(4, quantity);
            w.U8(8, (byte)location);
            w.U8(9, slot);
            return w.Packet;
        }
    }
}
