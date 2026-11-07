using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // CItemLinkshell's exdata (GroupId u32@0, Color u16@6, Flag/LSTYPE@8, Name[15]@9) and the 6-bit linkshell name
    // packing (EncodeStringLinkshell / DecodeStringLinkshell)
    internal static class LinkshellItem
    {
        public const ushort NewLinkshellId = 512, LinkshellId = 513, PearlsackId = 514, LinkpearlId = 515;
        public const byte TypeNew = 0, TypeLinkshell = 1, TypePearlsack = 2, TypeLinkpearl = 3, TypeBroken = 4; // LSTYPE
        public const int SlotLink1 = 16, SlotLink2 = 17;
        public const byte LockFlgLinkshell = 0x13;
        private const int NameAt = 9, NameSize = 15, EncodedLength = 20; // LinkshellStringLength

        // ITEM_LINKSHELL: item_basic's type 2 rows are exactly 512-517
        public static bool Is(ushort itemId) => itemId >= 512 && itemId <= 517;
        public static bool Is(Item item) => item != null && Is(item.ItemId);

        public static uint Lsid(Item item) => BitConverter.ToUInt32(item.Extra, 0);
        public static void SetLsid(Item item, uint id) => BitConverter.TryWriteBytes(item.Extra.AsSpan(0, 4), id);
        public static ushort Color(Item item) => BitConverter.ToUInt16(item.Extra, 6);
        public static void SetColor(Item item, ushort color) => BitConverter.TryWriteBytes(item.Extra.AsSpan(6, 2), color);
        public static byte Type(Item item) => item.Extra[8];

        // CCharEntity::getLinkshell: the item in SLOT_LINK1/2, if it is a linkshell
        public static Item Equipped(CharData c, int slot)
        {
            var item = CharStats.EquippedItem(c, slot);
            return Is(item) ? item : null;
        }

        // the 0x00D / 0x037 colour bytes of the char's LS1: (nibble << 4) + 15, as the C++ ("seems wrong, but displays
        // correctly")
        public static (byte R, byte G, byte B) DisplayColor(Item ls)
        {
            ushort color = Color(ls);
            return ((byte)(((color & 0xF) << 4) + 15), (byte)(((color >> 4 & 0xF) << 4) + 15), (byte)(((color >> 8 & 0xF) << 4) + 15));
        }

        // getSignature: the name decoded from the exdata
        public static string Signature(Item item) => Decode(item.Extra.AsSpan(NameAt, NameSize));

        // setSignature: the encoded name over Name[15]; Item.Signature mirrors getSignature
        public static void SetSignature(Item item, string signature)
        {
            var encoded = Encode(signature);
            Array.Clear(item.Extra, NameAt, NameSize);
            Array.Copy(encoded, 0, item.Extra, NameAt, NameSize);
            item.Signature = Signature(item);
        }

        // EncodeStringLinkshell: 6 bits a char, most significant first (0-9 53.., A-Z 27.., a-z 1.., anything else 0),
        // then a run of set bits; strncpy then keeps up to the first zero byte of the 20
        public static byte[] Encode(string signature)
        {
            var bits = new byte[EncodedLength];
            int chars = Math.Min(20, signature.Length);
            for (int i = 0; i < chars; i++)
            {
                char ch = signature[i];
                int code = ch >= '0' && ch <= '9' ? ch - '0' + 53 : ch >= 'A' && ch <= 'Z' ? ch - 'A' + 27 : ch >= 'a' && ch <= 'z' ? ch - 'a' + 1 : 0;
                PackBits(bits, code, 6 * i, 6);
            }
            int leftover = 8 - chars * 6 % 8;
            if (leftover == 8 || leftover == 2) leftover = 6;
            PackBits(bits, 0xFF, 6 * chars, leftover);

            int nul = Array.IndexOf(bits, (byte)0);
            if (nul >= 0) Array.Clear(bits, nul, bits.Length - nul);
            return bits;
        }

        // the encoded name as a std::string holds it: up to the first zero byte
        public static byte[] EncodeName(string name)
        {
            var bits = Encode(name);
            int nul = Array.IndexOf(bits, (byte)0);
            return bits.AsSpan(0, nul < 0 ? bits.Length : nul).ToArray();
        }

        // DecodeStringLinkshell: a zero code ends the name a char early (the terminator's partial last code), 63 ends it
        // there; bytes past the input read as zero
        public static string Decode(ReadOnlySpan<byte> encoded)
        {
            var decoded = new char[21];
            int length = Math.Min(20, encoded.Length * 8 / 6);
            int end = length;
            for (int i = 0; i < length; i++)
            {
                int code = UnpackBits(encoded, 6 * i, 6);
                char ch = code >= 1 && code <= 26 ? (char)('a' - 1 + code)
                        : code >= 27 && code <= 52 ? (char)('A' - 27 + code)
                        : code >= 53 && code <= 62 ? (char)('0' - 53 + code)
                        : (char)code;
                if (ch == '\0')
                {
                    end = i == 0 ? 0 : i - 1;
                    break;
                }
                if (ch == (char)63)
                {
                    end = i;
                    break;
                }
                decoded[i] = ch;
            }
            int nul = Array.IndexOf(decoded, '\0');
            return new string(decoded, 0, Math.Min(end, nul < 0 ? end : nul));
        }

        // packBitsLE over the name (and ItemSignature's): `length` bits of value, most significant first, from bit
        // `offset` of byte 0's top
        internal static void PackBits(byte[] target, int value, int offset, int length)
        {
            for (int b = 0; b < length; b++)
            {
                int at = offset + b;
                if (at / 8 >= target.Length) return;
                int mask = 0x80 >> (at % 8);
                if ((value >> (length - 1 - b) & 1) != 0) target[at / 8] |= (byte)mask;
                else target[at / 8] &= (byte)~mask;
            }
        }

        internal static int UnpackBits(ReadOnlySpan<byte> source, int offset, int length)
        {
            int value = 0;
            for (int b = 0; b < length; b++)
            {
                int at = offset + b;
                int bit = at / 8 < source.Length ? source[at / 8] >> (7 - at % 8) & 1 : 0;
                value = value << 1 | bit;
            }
            return value;
        }
    }

    // CLinkshell and linkshell:: (linkshell.cpp), c2s/0x0C3 GROUP_COMLINK_MAKE, 0x0C4 GROUP_COMLINK_ACTIVE, 0x0E1
    // GET_LSMSG, 0x0E2 SET_LSMSG and 0x0E4 GET_LSPRIV, and the linkshell IPC the world routes back (ipc_client.cpp)
    public sealed partial class ZoneHost
    {
        // CLuaBaseEntity::sendLinkshellConcierge: the GP_SERV_COMMAND_LINK_CONCIERGE (0x048) HEADER (the FEFEFEFE
        // sentinel, the char's own slot with ListingFlag 0xFFFF and Registered, the days since its post as "22d"), then
        // four RECORDs of the 16 slots: each the slot ids (0xFF empty), 0x0203 x4, the linkshell exdata bodies (group id,
        // key, color, flag, the packed name) and the attribute bits (Active, JP / EN / Other, MembersGoal 6-9,
        // ActiveTier 14-15, Characteristics 16-31)
        private void SendLinkshellConcierge(CharData c, LuaState l, int data)
        {
            var h = new PacketWriter(0x048, 128);
            h.Fill(4, 4, 0xFE);
            l.GetField(data, "yourSlot");
            if (l.IsNumber(-1))
            {
                h.U16(8, (byte)l.ToIntegral(-1));
                h.U16(10, 0xFFFF);
                h.U8(48, 1);
            }
            l.Pop(1);
            ushort days = (ushort)FieldInt(l, data, "postedDays", 0);
            if (days > 0)
            {
                var str = System.Text.Encoding.ASCII.GetBytes($"{days}d");
                h.Bytes(28, str.AsSpan(0, Math.Min(str.Length, 3)));
            }
            PushTo(c, h.Packet);

            l.GetField(data, "slots");
            int slots = l.Top;
            for (int pkt = 0; pkt < 4; pkt++)
            {
                var w = new PacketWriter(0x048, 128);
                for (int entry = 0; entry < 4; entry++)
                {
                    w.U16(8 + entry * 2, 0x0203);
                    int slotId = pkt * 4 + entry;
                    bool filled = false;
                    if (l.IsTable(slots))
                    {
                        l.RawGetI(slots, slotId);
                        int t = l.Top;
                        if (l.IsTable(t))
                        {
                            filled = true;
                            w.U8(4 + entry, slotId);
                            int body = 16 + entry * 24;
                            w.U32(body, (uint)FieldInt(l, t, "groupId", 0));
                            w.U16(body + 4, (ushort)FieldInt(l, t, "groupKey", 0));
                            w.U16(body + 6, (ushort)FieldInt(l, t, "color", 0));
                            w.U8(body + 8, (byte)FieldInt(l, t, "flag", 0));
                            w.Bytes(body + 9, LinkshellItem.Encode(FieldString(l, t, "name", "")).AsSpan(0, 15));
                            byte lang = (byte)FieldInt(l, t, "lang", 0);
                            uint attrs = 1u | (lang == 1 ? 1u << 1 : 0) | (lang == 2 ? 1u << 2 : 0) | (lang == 3 ? 1u << 5 : 0)
                                       | ((uint)FieldInt(l, t, "membersGoal", 0) & 0xF) << 6
                                       | ((uint)FieldInt(l, t, "activeTier", 0) & 0x3) << 14
                                       | ((uint)FieldInt(l, t, "characteristics", 0) & 0xFFFF) << 16;
                            w.U32(112 + entry * 4, attrs);
                        }
                        l.Pop(1);
                    }
                    if (!filled) w.U8(4 + entry, 0xFF);
                }
                PushTo(c, w.Packet);
            }
            l.Pop(1);
        }

        // GP_CLI_COMMAND_SET_LSMSG_WRITELEVEL
        private const byte WriteLevelLinkshell = 0, WriteLevelPearlsack = 1, WriteLevelLinkpearl = 2;
        private const ushort MsgStdLinkshellKicked = 109, MsgStdLinkshellNoLongerExists = 110, MsgStdLinkshellUnavailable = 112;
        private const byte LinkshellSlotLs1 = 0, LinkshellSlotLs2 = 1; // LinkshellSlot
        private const byte ChatLinkshell = 5, ChatLinkshell2 = 0x1B;   // MESSAGE_LINKSHELL, MESSAGE_LINKSHELL2

        // linkshell::LinkshellList: the shells with members online here
        private readonly Dictionary<uint, Linkshell> linkshells_ = new Dictionary<uint, Linkshell>();

        internal sealed class Linkshell
        {
            private readonly ZoneHost h_;
            public readonly uint Id;
            public ushort Color;
            public byte[] Name = Array.Empty<byte>(); // m_name: the encoded name, to its first zero byte
            public byte PostRights = WriteLevelLinkshell;
            public readonly List<CharData> Members = new List<CharData>();

            public Linkshell(ZoneHost h, uint id)
            {
                h_ = h;
                Id = id;
            }

            public void SetPostRights(byte writeLevel)
            {
                PostRights = writeLevel;
                h_.Exec("UPDATE linkshells SET postrights = ? WHERE linkshellid = ?", BoundValue.Of(PostRights), BoundValue.Of(Id));
            }

            // setMessage: stored as sent; a non-empty one goes to every map's members
            public void SetMessage(byte[] message, string poster)
            {
                uint postTime = (uint)h_.UnixNow();
                if (h_.Exec("UPDATE linkshells SET poster = ?, message = ?, messagetime = ? WHERE linkshellid = ?",
                            BoundValue.Of(poster), BoundValue.Of(message), BoundValue.Of(postTime), BoundValue.Of(Id)) == null)
                {
                    h_.log_($"Failed to update linkshell message for linkshell {Id}");
                    return;
                }
                if (message.Length != 0)
                    h_.SendIpc(new LinkshellSetMessage
                    {
                        LinkshellId = Id, LinkshellName = CborRaw.FromBytes(Name), Poster = poster,
                        Message = CborRaw.FromBytes(message), PostTime = postTime,
                    });
            }

            // AddMember: online in this map, and the session's linkshell columns
            public void AddMember(CharData c, byte type, int lsNum)
            {
                if (Members.Contains(c))
                {
                    h_.log_($"CLinkshell::AddMember attempted to add member '{c.Name}' who is already in the online member list.");
                    return;
                }
                Members.Add(c);
                if (lsNum == 1)
                {
                    h_.Exec("UPDATE accounts_sessions SET linkshellid1 = ?, linkshellrank1 = ? WHERE charid = ?",
                            BoundValue.Of(Id), BoundValue.Of(type), BoundValue.Of(c.Id));
                    c.Linkshell1 = this;
                }
                else
                {
                    h_.Exec("UPDATE accounts_sessions SET linkshellid2 = ?, linkshellrank2 = ? WHERE charid = ?",
                            BoundValue.Of(Id), BoundValue.Of(type), BoundValue.Of(c.Id));
                    c.Linkshell2 = this;
                }
            }

            // DelMember: false once no member is left
            public bool DelMember(CharData c)
            {
                if (Members.Remove(c))
                {
                    if (c.Linkshell1 == this)
                    {
                        h_.Exec("UPDATE accounts_sessions SET linkshellid1 = 0, linkshellrank1 = 0 WHERE charid = ?", BoundValue.Of(c.Id));
                        c.Linkshell1 = null;
                    }
                    else if (c.Linkshell2 == this)
                    {
                        h_.Exec("UPDATE accounts_sessions SET linkshellid2 = 0, linkshellrank2 = 0 WHERE charid = ?", BoundValue.Of(c.Id));
                        c.Linkshell2 = null;
                    }
                }
                return Members.Count > 0;
            }

            private CharData MemberByName(string name) =>
                Members.Find(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

            // ChangeMemberRank: the shell holder turns a member's pearl into a sack (2) or back (3), in place
            public void ChangeMemberRank(string memberName, byte requesterRank, byte newRank)
            {
                if (newRank < 2 || newRank > 3 || requesterRank != LinkshellItem.TypeLinkshell)
                {
                    h_.log_($"CLinkshell::ChangeMemberRank: Invalid rank change request for member '{memberName}' in linkshell {Id}.");
                    return;
                }
                var member = MemberByName(memberName);
                if (member == null) return;

                int lsNum = member.Linkshell2 == this ? 2 : 1;
                var item = LinkshellItem.Equipped(member, lsNum == 2 ? LinkshellItem.SlotLink2 : LinkshellItem.SlotLink1);
                if (item != null && LinkshellItem.Lsid(item) == Id)
                {
                    // the new item takes the old one's place, exdata, slot and equip binding
                    item.ItemId = (ushort)(LinkshellItem.NewLinkshellId + newRank);
                    item.Quantity = 1;
                    item.Extra[8] = newRank == 2 ? LinkshellItem.TypePearlsack : LinkshellItem.TypeLinkpearl;
                    h_.Exec("UPDATE char_inventory SET itemid = ?, extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                            BoundValue.Of(item.ItemId), BoundValue.Of(item.Extra), BoundValue.Of(member.Id),
                            BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
                    if (lsNum == 1)
                        h_.Exec("UPDATE accounts_sessions SET linkshellid1 = ?, linkshellrank1 = ? WHERE charid = ? LIMIT 1",
                                BoundValue.Of(Id), BoundValue.Of(LinkshellItem.Type(item)), BoundValue.Of(member.Id));
                    else
                        h_.Exec("UPDATE accounts_sessions SET linkshellid2 = ?, linkshellrank2 = ? WHERE charid = ?",
                                BoundValue.Of(Id), BoundValue.Of(LinkshellItem.Type(item)), BoundValue.Of(member.Id));
                    h_.PushTo(member, LoginPackets.ItemList(item, 0));
                    h_.PushTo(member, GroupComlink(member, lsNum));
                    h_.PushTo(member, GameOkPackets.ItemAttr(item, ItemLockFlg(member, item)));
                }
                h_.SaveCharStats(member);
                h_.SaveCharEquip(member);
                h_.PushTo(member, GameOkPackets.ItemSame(1, CharData.MaxContainers, member.SyncedContainers));
                h_.PushTo(member, h_.CharStatus(member));
            }

            // RemoveMemberByName: unequip the member's pearl of this shell; the holder (rank 1) breaks every pearl and
            // sack of it the member carries, a sack only the equipped one
            public void RemoveMemberByName(string memberName, byte requesterRank, bool breakLinkshell = false)
            {
                var member = MemberByName(memberName);
                if (member == null) return;

                int slot = LinkshellItem.SlotLink1, lsNum = 1;
                var equipped = LinkshellItem.Equipped(member, slot);
                if (equipped == null || LinkshellItem.Lsid(equipped) != Id)
                {
                    slot = LinkshellItem.SlotLink2;
                    lsNum = 2;
                    equipped = LinkshellItem.Equipped(member, slot);
                }
                if (equipped != null)
                {
                    h_.DelOnlineMember(member, equipped);
                    member.Equip.RemoveAll(e => e.EquipSlot == slot);
                    if (slot == LinkshellItem.SlotLink1) member.UpdateMask |= CharStats.UpdateHp;
                    h_.PushTo(member, LoginPackets.ItemList(equipped, 0));
                    h_.PushTo(member, GroupComlink(member, lsNum));
                }

                foreach (var item in member.Items)
                {
                    if (!LinkshellItem.Is(item) || LinkshellItem.Lsid(item) != Id) continue;
                    if (requesterRank != LinkshellItem.TypeLinkshell && item != equipped) continue;
                    if (LinkshellItem.Type(item) == LinkshellItem.TypeLinkshell) continue;
                    item.Extra[8] = LinkshellItem.TypeBroken;
                    h_.Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                            BoundValue.Of(item.Extra), BoundValue.Of(member.Id), BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
                    h_.PushTo(member, GameOkPackets.ItemAttr(item, ItemLockFlg(member, item)));
                }

                h_.SaveCharStats(member);
                h_.SaveCharEquip(member);
                h_.PushTo(member, GameOkPackets.ItemSame(1, CharData.MaxContainers, member.SyncedContainers));
                h_.PushTo(member, h_.CharStatus(member));
                h_.PushTo(member, SelfPackets.Message(breakLinkshell ? MsgStdLinkshellNoLongerExists : MsgStdLinkshellKicked));
                // an IPC or another char's packet got here, so no AfterParse of the member's flushes the mask
                if (h_.SessionOf(member) is MapSession ms) h_.SendUpdateMask(ms, member);
            }

            // BreakLinkshell: every online member loses it, then it is broken for good
            public void BreakLinkshell()
            {
                foreach (var member in Members.ToArray()) RemoveMemberByName(member.Name, LinkshellItem.TypeLinkshell, true);
                h_.Exec("UPDATE linkshells SET broken = 1 WHERE linkshellid = ? LIMIT 1", BoundValue.Of(Id));
            }

            // PushPacket: the members but the sender, in the zone and not jailed; LS2 holders get it as their second shell
            public void PushPacket(uint senderId, BasicPacket packet)
            {
                foreach (var member in Members)
                {
                    if (member.Id == senderId || !h_.charSessions_.TryGetValue(member, out var s) || !s.InZone ||
                        h_.zoneChanges_.ContainsKey(s) || InPrison(member))
                        continue;
                    var copy = new BasicPacket(packet.Buffer);
                    if (member.Linkshell2 == this)
                    {
                        if (copy.GetOpcode() == 0x017) copy.Buffer[4] = ChatLinkshell2;
                        else if (copy.GetOpcode() == 0x0CC) copy.Buffer[5] |= 0x40;
                    }
                    Push(s, copy);
                }
            }

            // PushLinkshellMessage: the stored message, if any (none sends nothing, as the C++)
            public void PushLinkshellMessage(CharData c, byte slot)
            {
                var r = h_.Exec("SELECT poster, message, messagetime FROM linkshells WHERE linkshellid = ?", BoundValue.Of(Id));
                if (r == null || !r.Next()) return;
                string poster = r.IsNull("poster") ? "" : r.Get<string>("poster");
                byte[] message = r.IsNull("message") ? Array.Empty<byte>() : r.GetBlobBytes("message");
                uint time = r.IsNull("messagetime") ? 0 : r.Get<uint>("messagetime");
                if (message.Length != 0)
                    h_.PushTo(c, LinkshellMessage(CborRaw.Bytes(poster), message, Name, time, slot, PostRights, post: false));
            }
        }

        // linkshell::LoadLinkshell: replaces any loaded entry
        private Linkshell LoadLinkshell(uint id)
        {
            var r = Exec("SELECT linkshellid, color, name, postrights FROM linkshells WHERE linkshellid = ? LIMIT 1", BoundValue.Of(id));
            if (r == null || !r.Next()) return null;
            var ls = new Linkshell(this, r.Get<uint>("linkshellid"))
            {
                Color = r.Get<ushort>("color"),
                Name = LinkshellItem.EncodeName(r.Get<string>("name") ?? ""),
            };
            ls.SetPostRights(r.Get<byte>("postrights"));
            linkshells_[id] = ls;
            return ls;
        }

        private void UnloadLinkshell(uint id) => linkshells_.Remove(id);

        private Linkshell GetLinkshell(uint id) => linkshells_.TryGetValue(id, out var ls) ? ls : null;

        // linkshell::AddOnlineMember
        private void AddOnlineMember(CharData c, Item item, int lsNum)
        {
            if (!LinkshellItem.Is(item)) return;
            var ls = GetLinkshell(LinkshellItem.Lsid(item)) ?? LoadLinkshell(LinkshellItem.Lsid(item));
            ls?.AddMember(c, LinkshellItem.Type(item), lsNum);
        }

        // linkshell::DelOnlineMember: the shell is dropped with its last member
        private void DelOnlineMember(CharData c, Item item)
        {
            if (!LinkshellItem.Is(item)) return;
            uint id = LinkshellItem.Lsid(item);
            if (!linkshells_.TryGetValue(id, out var ls))
            {
                log_($"linkshell::DelOnlineMember caught exception: no linkshell {id}");
                return;
            }
            if (!ls.DelMember(c)) linkshells_.Remove(id);
        }

        // ~CCharEntity: out of the online lists, the shells staying loaded
        private void LeaveLinkshells(CharData c)
        {
            c.Linkshell1?.DelMember(c);
            c.Linkshell2?.DelMember(c);
        }

        // linkshell::IsValidLinkshellName: a failed query lets the name through
        private bool IsValidLinkshellName(string name)
        {
            var r = Exec("SELECT linkshellid FROM linkshells WHERE name = ? AND broken != 1", BoundValue.Of(name));
            return r == null || !r.Next();
        }

        // linkshell::RegisterNewLinkshell: the new shell's id (loaded), or 0
        private uint RegisterNewLinkshell(string name, ushort color)
        {
            if (!IsValidLinkshellName(name)) return 0;
            if (Exec("INSERT INTO linkshells (name, color, postrights) VALUES (?, ?, ?)",
                     BoundValue.Of(name), BoundValue.Of(color), BoundValue.Of(WriteLevelPearlsack)) == null)
                return 0;
            var r = Exec("SELECT linkshellid FROM linkshells WHERE name = ? AND broken != 1", BoundValue.Of(name));
            if (r == null || !r.Next()) return 0;
            return LoadLinkshell(r.Get<uint>("linkshellid"))?.Id ?? 0;
        }

        // LoadEquip's tail: an equipped shell that broke meanwhile is unequipped (its row deleted), else the char joins it
        private void JoinEquippedLinkshells(CharData c)
        {
            for (int lsNum = 1; lsNum <= 2; lsNum++)
            {
                int slot = lsNum == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2;
                var item = LinkshellItem.Equipped(c, slot);
                if (item == null) continue;
                var r = Exec("SELECT broken FROM linkshells WHERE linkshellid = ? LIMIT 1", BoundValue.Of(LinkshellItem.Lsid(item)));
                if (LinkshellItem.Type(item) == LinkshellItem.TypeBroken || (r != null && r.Next() && r.Get<uint>("broken") == 1))
                {
                    c.Equip.RemoveAll(e => e.EquipSlot == slot);
                    Exec("DELETE FROM char_equip WHERE charid = ? AND slotid = ? AND containerid = ? LIMIT 1",
                         BoundValue.Of(c.Id), BoundValue.Of(item.Slot), BoundValue.Of((byte)item.Location));
                }
                else AddOnlineMember(c, item, lsNum);
            }
        }

        // 0x0E0 GP_SERV_COMMAND_GROUP_COMLINK: which item is in the linkshell slot, if any
        internal static BasicPacket GroupComlink(CharData c, int lsNum)
        {
            var w = new PacketWriter(0x0E0, 8);
            w.U8(4, (byte)lsNum);
            var item = CharStats.EquippedItem(c, lsNum == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2);
            if (item != null)
            {
                w.U8(5, item.Slot);
                w.U8(6, (byte)item.Location);
            }
            return w.Packet;
        }

        // 0x0CC GP_SERV_COMMAND_LINKSHELL_MESSAGE(poster, message, lsname, posttime, slot, writeLevel, op): Post 1, Load 2
        private static BasicPacket LinkshellMessage(byte[] poster, byte[] message, byte[] lsName, uint postTime, byte slot, byte writeLevel, bool post)
        {
            var w = new PacketWriter(0x0CC, 176);
            w.U8(4, 0x0 | 0x7 << 4);                                                   // stat 0, attr 7
            w.U8(5, (byte)(0x2 | (writeLevel & 3) << 2 | (slot == LinkshellSlotLs2 ? 1 : 0) << 6)); // readLevel 2
            w.Bytes(8, message.AsSpan(0, Math.Min(message.Length, 128)));
            w.U32(136, postTime);
            w.Bytes(140, poster.AsSpan(0, Math.Min(poster.Length, 16)));
            w.U16(156, (ushort)(post ? 0x01 : 0x02));
            w.Bytes(160, lsName.AsSpan(0, Math.Min(lsName.Length, 16)));
            return w.Packet;
        }

        // 0x0CC GP_SERV_COMMAND_LINKSHELL_MESSAGE(result, lsname, slot, writeLevel): LevelChanged or Denied
        private static BasicPacket LinkshellMessageResult(bool denied, byte[] lsName, byte slot, byte writeLevel)
        {
            var w = new PacketWriter(0x0CC, 176);
            w.U8(4, (byte)(denied ? 0x3 : 0x3 << 4));
            w.U8(5, (byte)(0x2 | (writeLevel & 3) << 2 | (slot == LinkshellSlotLs2 ? 1 : 0) << 6));
            w.U16(156, 0x03);
            w.Bytes(160, lsName.AsSpan(0, Math.Min(lsName.Length, 16)));
            return w.Packet;
        }

        private void RegisterLinkshellHandlers()
        {
            Register(0x0C3, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(p.Buffer[4], 0, "State not 0")
                    .OneOf("LinkshellId", p.Buffer[5], 1, 2)
                    .HasLinkshellRank(p.Buffer[5], LinkshellItem.TypePearlsack),
                OnGroupComlinkMake);
            Register(0x0C4, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .Range("r", p.Buffer[4] & 0xF, 0, 15)
                    .Range("g", p.Buffer[4] >> 4, 0, 15)
                    .Range("b", p.Buffer[5] & 0xF, 0, 15)
                    .MustEqual(p.Buffer[5] >> 4, 15, "a not 15")
                    .OneOf("ActiveFlg", p.Buffer[8], 0, 1)
                    .OneOf("LinkshellId", p.Buffer[27], 1, 2)
                    .IsValidContainer("Category", p.Buffer[7]),
                OnGroupComlinkActive);
            Register(0x0E1, (s, c, p) =>
            {
                byte slot = (byte)(p.Buffer[5] >> 6);
                var pv = Pv(c).BlockedBy(BlockedState.InEvent).OneOf("LinkshellSlot", slot, LinkshellSlotLs1, LinkshellSlotLs2);
                if (slot == LinkshellSlotLs1) pv.MustNotEqual(c.Linkshell1 == null ? 0 : 1, 0, "Character does not have Linkshell 1");
                else if (slot == LinkshellSlotLs2) pv.MustNotEqual(c.Linkshell2 == null ? 0 : 1, 0, "Character does not have Linkshell 2");
                return pv;
            }, OnGetLsMsg);
            Register(0x0E2, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf("writeLevel", p.Buffer[5] >> 2 & 3, WriteLevelLinkshell, WriteLevelPearlsack, WriteLevelLinkpearl)
                    .MustNotEqual(c.Linkshell1 == null ? 0 : 1, 0, "Character does not have Linkshell 1"),
                OnSetLsMsg);
            Register(0x0E4, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), OnGetLsPriv);
        }

        // GP_CLI_COMMAND_GROUP_COMLINK_MAKE: State@4, LinkshellId@5. A sack holder makes a new pearl of the equipped shell.
        private void OnGroupComlinkMake(MapSession s, CharData c, BasicPacket p)
        {
            var shell = LinkshellItem.Equipped(c, p.Buffer[5] == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2);
            if (shell == null) return;

            var pearl = new Item { ItemId = LinkshellItem.LinkpearlId, Quantity = 1, Extra = (byte[])shell.Extra.Clone() };
            pearl.Extra[8] = LinkshellItem.TypeLinkpearl;
            pearl.Signature = LinkshellItem.Signature(pearl);
            using var tx = new ItemTransaction(this, c);
            if (tx.GiveItem(Container.Inventory, pearl) == null || !tx.Commit())
                log_($"GP_CLI_COMMAND_GROUP_COMLINK_MAKE: {c.Name} did not receive the linkshell");
        }

        // GP_CLI_COMMAND_GROUP_COMLINK_ACTIVE: r/g/b/a nibbles@4, ItemIndex@6, Category@7, ActiveFlg@8,
        // sComLinkName[15]@12, LinkshellId@27. Create a new shell, equip a pearl / sack / shell, or unequip it.
        private void OnGroupComlinkActive(MapSession s, CharData c, BasicPacket p)
        {
            var category = (Container)p.Buffer[7];
            byte index = p.Buffer[6];
            byte lsNum = p.Buffer[27];
            int slot = LinkshellItem.SlotLink1 - 1 + lsNum;
            var item = StorageItem(c, category, index);
            if (!LinkshellItem.Is(item)) return;

            if (p.Buffer[8] == 1)
            {
                if (IsBusy(c, item)) return;
                if (item.ItemId == LinkshellItem.NewLinkshellId) CreateLinkshell(s, c, item, p);
                else EquipLinkshell(s, c, item, lsNum, slot);
            }
            else
            {
                if (LinkshellItem.Equipped(c, slot) != item) return;
                DelOnlineMember(c, item);
                c.Equip.RemoveAll(e => e.EquipSlot == slot);
                if (lsNum == 1) c.UpdateMask |= CharStats.UpdateHp;
                Push(s, LoginPackets.ItemList(item, LinkshellItem.LockFlgLinkshell));
                SaveCharStats(c);
                SaveCharEquip(c);
                Push(s, GroupComlink(c, lsNum));
                Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
            }

            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            Push(s, CharStatus(c));
        }

        // createLinkshell: the new linkshell item becomes the shell (513) of a newly registered linkshell. The decoded
        // name only holds [A-Za-z0-9], so the C++'s escapeString leaves it as it is.
        private void CreateLinkshell(MapSession s, CharData c, Item item, BasicPacket p)
        {
            ushort color = (ushort)((p.Buffer[5] >> 4) << 12 | (p.Buffer[5] & 0xF) << 8 | (p.Buffer[4] >> 4) << 4 | p.Buffer[4] & 0xF);
            string name = LinkshellItem.Decode(UntrustedBytes(p, 12, 15));
            uint id = RegisterNewLinkshell(name, color);
            if (id == 0)
            {
                Push(s, SelfPackets.Message(MsgStdLinkshellUnavailable));
                return;
            }

            // a fresh item in the same slot
            item.ItemId = LinkshellItem.LinkshellId;
            item.Quantity = 1;
            item.Extra = new byte[24];
            item.CharPrice = 0;
            LinkshellItem.SetLsid(item, id);
            item.Extra[8] = LinkshellItem.TypeLinkshell;
            LinkshellItem.SetSignature(item, name);
            LinkshellItem.SetColor(item, color);

            var r = Exec("UPDATE char_inventory SET signature = ?, extra = ?, itemId = 513 WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                         BoundValue.Of(name), BoundValue.Of(item.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
            if (r != null && r.RowsAffected() > 0) Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
        }

        // equipLinkshell: a broken shell (by its item or the linkshells row) is unusable; else the slot's old one is left
        // and this one joined and bound
        private void EquipLinkshell(MapSession s, CharData c, Item item, byte lsNum, int slot)
        {
            var old = lsNum == 2 ? c.Linkshell2 : c.Linkshell1;
            if (LinkshellItem.Type(item) == LinkshellItem.TypeBroken)
            {
                Push(s, SelfPackets.Message(MsgStdLinkshellNoLongerExists));
                return;
            }

            var r = Exec("SELECT broken FROM linkshells WHERE linkshellid = ? LIMIT 1", BoundValue.Of(LinkshellItem.Lsid(item)));
            if (r != null && r.Next() && r.Get<byte>("broken") == 1)
            {
                item.Extra[8] = LinkshellItem.TypeBroken;
                Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                     BoundValue.Of(item.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
                Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
                Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
                Push(s, SelfPackets.Message(MsgStdLinkshellNoLongerExists));
                return;
            }

            if (LinkshellItem.Lsid(item) == 0)
            {
                Push(s, SelfPackets.Message(MsgStdLinkshellNoLongerExists));
                return;
            }

            if (old != null && LinkshellItem.Equipped(c, slot) is Item oldItem)
            {
                DelOnlineMember(c, oldItem);
                Push(s, LoginPackets.ItemList(oldItem, 0));
            }

            // bindEquip: the item was checked free above, so this cannot fail
            AddOnlineMember(c, item, lsNum);
            c.Equip.RemoveAll(e => e.EquipSlot == slot);
            c.Equip.Add(new EquipEntry { EquipSlot = (byte)slot, InvSlot = item.Slot, Container = item.Location });
            c.Equip.Sort((a, b) => a.EquipSlot.CompareTo(b.EquipSlot));

            if (lsNum == 1) c.UpdateMask |= CharStats.UpdateHp;
            Push(s, LoginPackets.ItemList(item, LinkshellItem.LockFlgLinkshell));
            SaveCharStats(c);
            SaveCharEquip(c);
            Push(s, GroupComlink(c, lsNum));
            Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
        }

        // GP_CLI_COMMAND_GET_LSMSG: byte 5's top two bits pick the shell
        private void OnGetLsMsg(MapSession s, CharData c, BasicPacket p)
        {
            if ((p.Buffer[5] >> 6) == LinkshellSlotLs1) c.Linkshell1.PushLinkshellMessage(c, LinkshellSlotLs1);
            else c.Linkshell2.PushLinkshellMessage(c, LinkshellSlotLs2);
        }

        // GP_CLI_COMMAND_SET_LSMSG: byte 4 bit 5 (unknown02) sets the write level (the holder only), bit 6 (unknown03)
        // posts sMessage[128]@16 if the write level lets the char's pearl/sack/shell
        private void OnSetLsMsg(MapSession s, CharData c, BasicPacket p)
        {
            var item = LinkshellItem.Equipped(c, LinkshellItem.SlotLink1);
            if (item == null) return;
            var ls = c.Linkshell1;
            byte type = LinkshellItem.Type(item);

            if ((p.Buffer[4] & 0x20) != 0)
            {
                if (type == LinkshellItem.TypeLinkshell)
                {
                    ls.SetPostRights((byte)(p.Buffer[5] >> 2 & 3));
                    Push(s, LinkshellMessageResult(false, ls.Name, LinkshellSlotLs1, ls.PostRights));
                    return;
                }
                Push(s, LinkshellMessageResult(true, ls.Name, LinkshellSlotLs1, ls.PostRights));
            }
            else if ((p.Buffer[4] & 0x40) != 0)
            {
                bool canEdit = ls.PostRights switch
                {
                    WriteLevelLinkshell => type == LinkshellItem.TypeLinkshell,
                    WriteLevelPearlsack => type == LinkshellItem.TypePearlsack || type == LinkshellItem.TypeLinkshell,
                    WriteLevelLinkpearl => true,
                    _ => false,
                };
                if (canEdit)
                {
                    ls.SetMessage(UntrustedBytes(p, 16, 128), c.Name);
                    return;
                }
                Push(s, LinkshellMessageResult(true, ls.Name, LinkshellSlotLs1, ls.PostRights));
            }
        }

        // GP_CLI_COMMAND_GET_LSPRIV (/lsmes level): not implemented in the C++ either
        private void OnGetLsPriv(MapSession s, CharData c, BasicPacket p) =>
            log_($"GP_CLI_COMMAND_GET_LSPRIV: Not implemented. unknown01 {p.Buffer[4] >> 4 & 1}, unknown02 {p.Buffer[4] >> 5 & 1}, " +
                 $"unknown04 {p.Buffer[4] >> 7 & 1}, seqId {U16(p, 10)}");

        // the linkshell IPC: false when the message is not one
        private bool OnLinkshellIpc(IIpcMessage message)
        {
            switch (message)
            {
                case ChatMessageLinkshell m:
                    // TODO in the C++ too: LS1 vs LS2 is left to PushPacket
                    GetLinkshell(m.LinkshellId)?.PushPacket(m.SenderId,
                        ChatStd(m.SenderName, (ushort)m.ZoneId, ChatLinkshell, CborRaw.Bytes(m.Message), m.GmLevel));
                    return true;
                case LinkshellSetMessage m:
                    if (GetLinkshell(m.LinkshellId) is Linkshell posted)
                        posted.PushPacket(0, LinkshellMessage(CborRaw.Bytes(m.Poster), CborRaw.Bytes(m.Message), CborRaw.Bytes(m.LinkshellName),
                                                              m.PostTime, LinkshellSlotLs1, posted.PostRights, post: true));
                    return true;
                case LinkshellRankChange m:
                    GetLinkshell(m.LinkshellId)?.ChangeMemberRank(m.MemberName, m.RequesterRank, m.NewRank);
                    return true;
                case LinkshellRemove m:
                {
                    // the holder removes anyone, a sack holder only pearls
                    var (victim, _) = CharByName(m.VictimName);
                    if (victim?.Linkshell1 != null && victim.Linkshell1.Id == m.LinkshellId)
                    {
                        var target = LinkshellItem.Equipped(victim, LinkshellItem.SlotLink1);
                        if (target != null && CanRemove(m.RequesterRank, target)) victim.Linkshell1.RemoveMemberByName(m.VictimName, m.RequesterRank);
                    }
                    else if (victim?.Linkshell2 != null && victim.Linkshell2.Id == m.LinkshellId)
                    {
                        var target = LinkshellItem.Equipped(victim, LinkshellItem.SlotLink2);
                        if (target != null && CanRemove(m.RequesterRank, target)) victim.Linkshell2.RemoveMemberByName(m.VictimName, m.RequesterRank);
                    }
                    return true;
                }
                default:
                    return false;
            }

            static bool CanRemove(byte requesterRank, Item target) =>
                requesterRank == LinkshellItem.TypeLinkshell ||
                (requesterRank == LinkshellItem.TypePearlsack && LinkshellItem.Type(target) == LinkshellItem.TypeLinkpearl);
        }

        // 0x0B5 Linkshell1/2: to every map's members of the shell, and audit_chat when map.AUDIT_LINKSHELL is on
        private void SendLinkshellChat(CharData c, Linkshell ls, string msg, byte[] raw)
        {
            if (ls == null) return;
            SendIpc(new ChatMessageLinkshell
            {
                LinkshellId = ls.Id, SenderId = c.Id, SenderName = c.Name, Message = msg,
                ZoneId = (XI.Ipc.ZoneId)c.Zone, GmLevel = c.AccountGmLevel,
            });
            if (ChatCfg.AuditChat && ChatCfg.AuditLinkshell)
                Exec("INSERT INTO audit_chat (speaker, type, lsName, zoneid, message, datetime) VALUES(?, 'LINKSHELL', ?, ?, ?, current_timestamp())",
                     BoundValue.Of(c.Name), BoundValue.Of(LinkshellItem.Decode(ls.Name)), BoundValue.Of(c.Zone), BoundValue.Of(raw));
        }

        // 0x028's linkshell break: throwing away the shell itself breaks the linkshell
        private void BreakDumpedLinkshell(Item item)
        {
            if (!LinkshellItem.Is(item) || LinkshellItem.Type(item) != LinkshellItem.TypeLinkshell) return;
            uint id = LinkshellItem.Lsid(item);
            var ls = GetLinkshell(id) ?? LoadLinkshell(id);
            if (ls == null) return; // a null deref in the C++
            ls.BreakLinkshell();
            UnloadLinkshell(id);
        }
    }
}
