using System;
using System.Collections.Generic;
using System.Text;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // c2s/0x03C BLACK_LIST and 0x03D BLACK_EDIT, and blacklistutils over char_blacklist
    public sealed partial class ZoneHost
    {
        private const byte BlackEditAdd = 0, BlackEditRemove = 1;                            // GP_CLI_COMMAND_BLACK_EDIT_MODE
        private const byte BlackEditModeAdd = 0, BlackEditModeDelete = 1, BlackEditModeError = 2; // GP_SERV_COMMAND_BLACK_EDIT_MODE

        private void RegisterBlacklistHandlers()
        {
            Register(0x03C, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => Push(s, BlacklistPackets(c)));
            Register(0x03D, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf("Mode", (sbyte)p.Buffer[24], BlackEditAdd, BlackEditRemove),
                OnBlackEdit);
        }

        // blacklistutils::IsBlacklisted
        private bool IsBlacklisted(uint ownerId, uint targetId)
        {
            var r = Exec("SELECT * FROM char_blacklist WHERE charid_owner = ? AND charid_target = ? LIMIT 1",
                         BoundValue.Of(ownerId), BoundValue.Of(targetId));
            return r != null && r.RowsCount() > 0;
        }

        // blacklistutils::AddBlacklisted
        private bool AddBlacklisted(uint ownerId, uint targetId)
        {
            if (IsBlacklisted(ownerId, targetId)) return false;
            var r = Exec("INSERT INTO char_blacklist (charid_owner, charid_target) VALUES (?, ?)",
                         BoundValue.Of(ownerId), BoundValue.Of(targetId));
            return r != null && r.RowsAffected() > 0;
        }

        // blacklistutils::DeleteBlacklisted
        private bool DeleteBlacklisted(uint ownerId, uint targetId)
        {
            if (!IsBlacklisted(ownerId, targetId)) return false;
            var r = Exec("DELETE FROM char_blacklist WHERE charid_owner = ? AND charid_target = ? LIMIT 1",
                         BoundValue.Of(ownerId), BoundValue.Of(targetId));
            return r != null && r.RowsAffected() > 0;
        }

        // blacklistutils::SendBlacklist: pages of 12. As the C++: a name with anything but letters is skipped, a full
        // page resets the client's list only when it is the first, the remainder page never resets it, and a list of
        // nothing but skipped names sends nothing. The entries carry the target's char id.
        private List<BasicPacket> BlacklistPackets(CharData c)
        {
            var list = new List<BasicPacket>();
            var r = Exec("SELECT c.charid, c.charname FROM char_blacklist AS b INNER JOIN chars AS c ON b.charid_target = c.charid WHERE charid_owner = ?",
                         BoundValue.Of(c.Id));
            if (r == null || r.RowsCount() == 0)
            {
                list.Add(GameOkPackets.BlackList());
                return list;
            }

            var page = new List<(uint Id, string Name)>();
            int total = 0, rowCount = (int)r.RowsCount();
            while (r.Next())
            {
                var name = r.Get<string>("charname") ?? "";
                bool lettersOnly = true;
                foreach (char ch in name.ToLowerInvariant()) if (ch < 'a' || ch > 'z') { lettersOnly = false; break; }
                if (!lettersOnly) continue;

                page.Add((r.Get<uint>("charid"), name));
                total++;
                if (page.Count == 12)
                {
                    list.Add(GameOkPackets.BlackList(page, total <= 12, total == rowCount));
                    page.Clear();
                }
            }
            if (page.Count > 0) list.Add(GameOkPackets.BlackList(page, false, true));
            return list;
        }

        // GP_CLI_COMMAND_BLACK_EDIT: add or remove a char by name; the reply carries the target's account id
        private void OnBlackEdit(MapSession s, CharData c, BasicPacket p)
        {
            var name = SqlEscape.Escape(Encoding.Latin1.GetString(UntrustedBytes(p, 8, 15)));

            uint charId = 0, accId = 0;
            var r = Exec("SELECT charid, accid FROM chars WHERE charname = ? LIMIT 1", BoundValue.Of(name));
            if (r != null && r.Next())
            {
                charId = r.Get<uint>("charid");
                accId = r.Get<uint>("accid");
            }
            if (charId == 0)
            {
                Push(s, BlackEditPacket(0, "", BlackEditModeError));
                return;
            }

            bool add = p.Buffer[24] == BlackEditAdd;
            bool ok = add ? AddBlacklisted(c.Id, charId) : DeleteBlacklisted(c.Id, charId);
            Push(s, ok ? BlackEditPacket(accId, name, add ? BlackEditModeAdd : BlackEditModeDelete)
                       : BlackEditPacket(0, "", BlackEditModeError));
        }

        // 0x042 GP_SERV_COMMAND_BLACK_EDIT: SAVE_BLACK (ID, Name[16]) + Mode; an error has ID 0 and no name
        private static BasicPacket BlackEditPacket(uint accId, string name, byte mode)
        {
            var w = new PacketWriter(0x042, 28);
            w.U32(4, accId);
            w.Bytes(8, Encoding.Latin1.GetBytes(name.Length > 16 ? name.Substring(0, 16) : name));
            w.U8(24, mode);
            return w.Packet;
        }
    }
}
