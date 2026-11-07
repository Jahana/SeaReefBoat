using System;
using System.Collections.Generic;
using System.Text;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // The help desk (GM calls and their answers), wide scan, /translate and /itemsearch, the world pass, /volunteer,
    // and party map markers
    public sealed partial class ZoneHost
    {
        private enum FriendpassPara : ushort { BeginPurchase = 0, ConfirmPurchase = 1, BeginGoldPurchase = 2, ConfirmGoldPurchase = 3 }
        private enum TranslateIndex : byte { Japanese = 0, English = 1 }
        private enum ItemsearchLanguage : byte { Japanese = 0, English = 1 }

        // GP_TRACKING_STATE and GP_TRACKING_POS_STATE
        private const byte TrackingListStart = 1, TrackingListEnd = 2;
        private const byte TrackingPosStart = 1, TrackingPosLose = 2;
        private const byte JobBst = 9, JobRng = 11;
        private const uint EntityFlagUntargetable = 0x800;
        private const byte NameVisHidden = 0x08;

        private const int MaxGmCallPackets = 6, GmMsgChunk = 244;
        private static readonly HashSet<string> AllowedGmCallParams = new HashSet<string>
        {
            "GENERIC.AREACODE", "GENERIC.CLILANG", "GENERIC.SELLANG", "GENERIC.PLATFORM", "GENERIC.VERSION", "GAME.INFIELD",
            "GAME.GMTIME", "EXEC.LOGOUT", "EXEC.POLEXIT", "EXEC.RESCUE", "GMREPORT.NOTICE", "GMREPORT.GMCALL",
            "GMREPORT.HARASSMENT", "GMREPORT.STUCK", "GMREPORT.BLOCK", "GMCALL.INPUT",
        };

        private void RegisterMiscHandlers()
        {
            // the world pass: the C++ only answers, a pass number when a purchase is confirmed
            Register(0x01B, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).OneOf<FriendpassPara>(U16(p, 4)),
                (s, c, p) => Push(s, FriendPassPacket((U16(p, 4) & 1) != 0 ? (uint)(long)(Random01() * 9999999999L) : 0)));
            Register(0x01E, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), OnVolunteer);
            Register(0x01F, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent),
                (s, c, p) => log_($"zone: GP_CLI_COMMAND_GMCOMMAND: Not implemented. GMUniqueNo: {U32(p, 4)}, " +
                                  $"Command: '{CborRaw.FromBytes(UntrustedBytes(p, 8, 128))}'"));
            Register(0x02B, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf<TranslateIndex>(p.Buffer[4])
                    .OneOf<TranslateIndex>(p.Buffer[5]),
                OnTranslate);
            Register(0x02C, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).OneOf<ItemsearchLanguage>(p.Buffer[4]), OnItemSearch);
            // chocobo digging comes in as a 0x01A action; this one is ignored
            Register(0x063, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => { });
            Register(0x0D2, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(U32(p, 4), c.Zone, "ZoneNo does not match character zone"),
                OnMapGroup);
            Register(0x0D3, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf<FaqGmcallType>(p.Buffer[4] & 0x0F)
                    .MustEqual(p.Buffer[4] >> 4, 0, "vers not equal to 0")
                    .Range("eos", p.Buffer[6] >> 7, 0, 1),
                OnFaqGmCall);
            Register(0x0D4, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).MustEqual(U16(p, 6), 0, "Option not 0"),
                (s, c, p) => Push(s, FaqGmParamPacket(U16(p, 4))));
            Register(0x0D5, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => AcknowledgeOldestGmResponse(s, c));
            Register(0x0F4, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).MustEqual(U32(p, 4), 1, "SendFlg not equal to 1"),
                (s, c, p) => WideScan(s, c, WideScanRange(c)));
            Register(0x0F5, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).Range("ActIndex", U32(p, 4), 0x1, 0x1000),
                OnTrackingStart);
            Register(0x0F6, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => c.WideScanTarget = null);
        }

        // ---- /volunteer, the world pass ----

        // GP_CLI_COMMAND_GM: the reported size, less the header, bounds the text (it may not end in a NUL)
        private void OnVolunteer(MapSession s, CharData c, BasicPacket p)
        {
            int length = Math.Min((int)p.GetSize() - 4, 115);
            var text = UntrustedBytes(p, 4, length);
            if (lua_ == null) return;
            const string path = "xi.player.onPlayerVolunteer";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return;
                }
                lua_.PushObject(c);
                lua_.PushBytes(text);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // GP_SERV_COMMAND_FRIENDPASS (0x059); the C++ marks its values "all of this is wrong"
        private static BasicPacket FriendPassPacket(uint worldPass)
        {
            var w = new PacketWriter(0x059, 36);
            w.I32(4 + 8, 10000); // passPop, the price
            w.U8(4 + 28, 0x03);  // Type
            w.U8(4 + 29, 0x01);
            if (worldPass != 0)
            {
                w.I32(4 + 0, 1);   // leftNum: uses left
                w.I32(4 + 4, 167); // leftDays: hours until the pass lapses
                w.U8(4 + 28, 0x06);
                w.Name(4 + 12, worldPass.ToString().PadLeft(10, '0'), 16);
            }
            return w.Packet;
        }

        // ---- /translate and /itemsearch ----

        // itemutils' g_TranslateMap, keyed by the name's bytes (one char per byte): English by item_basic.sortname in
        // lower case with spaces, to (id, name_jp in Shift-JIS); Japanese by the Shift-JIS name, to (id, that sortname)
        private Dictionary<string, (ushort Id, byte[] Name)>[] translate_;

        private Dictionary<string, (ushort Id, byte[] Name)>[] TranslateMap()
        {
            if (translate_ != null) return translate_;
            var jp = new Dictionary<string, (ushort, byte[])>();
            var en = new Dictionary<string, (ushort, byte[])>();
            var r = Exec("SELECT itemid, sortname, name_jp FROM item_basic WHERE itemid < ? ORDER BY itemid", BoundValue.Of(32768u));
            while (r != null && r.Next())
            {
                var id = (ushort)r.Get<uint>("itemid");
                var sortname = AsciiLower(Encoding.UTF8.GetBytes(r.Get<string>("sortname") ?? ""));
                for (int i = 0; i < sortname.Length; i++)
                    if (sortname[i] == '_') sortname[i] = (byte)' ';
                var jpName = Sjis.FromUnicode(r.Get<string>("name_jp") ?? "");
                if (sortname.Length > 0) en[CborRaw.FromBytes(sortname)] = (id, jpName);
                if (jpName.Length > 0) jp[CborRaw.FromBytes(jpName)] = (id, sortname);
            }
            return translate_ = new[] { jp, en };
        }

        private static byte[] AsciiLower(byte[] b)
        {
            for (int i = 0; i < b.Length; i++)
                if (b[i] >= 'A' && b[i] <= 'Z') b[i] = (byte)(b[i] + 32);
            return b;
        }

        // itemutils::TranslateItemName: English names match in any case; the from-map holds the other language's name
        private (ushort Id, byte[] Name)? TranslateItemName(byte fromLang, byte[] name)
        {
            var key = fromLang == (byte)TranslateIndex.English ? AsciiLower((byte[])name.Clone()) : name;
            return TranslateMap()[fromLang].TryGetValue(CborRaw.FromBytes(key), out var hit) ? hit : null;
        }

        private void OnTranslate(MapSession s, CharData c, BasicPacket p)
        {
            var name = UntrustedBytes(p, 8, 64);
            var hit = TranslateItemName(p.Buffer[4], name);
            // GP_SERV_COMMAND_TRANSLATE (0x047)
            var w = new PacketWriter(0x047, 136);
            w.U16(4 + 0, hit?.Id ?? 0);
            w.U8(4 + 2, p.Buffer[4]);
            w.U8(4 + 3, p.Buffer[5]);
            w.Bytes(4 + 4, name.AsSpan(0, Math.Min(name.Length, 64)));
            var to = hit?.Name ?? Array.Empty<byte>();
            w.Bytes(4 + 68, to.AsSpan(0, Math.Min(to.Length, 64)));
            Push(s, w.Packet);
        }

        // GP_CLI_COMMAND_ITEMSEARCH: the same-language lookup gives the item number
        private void OnItemSearch(MapSession s, CharData c, BasicPacket p)
        {
            var name = UntrustedBytes(p, 8, 64);
            var hit = TranslateItemName(p.Buffer[4], name);
            var w = new PacketWriter(0x049, 4 + S2cItemSearch.SizeOf);
            w.U16(4 + S2cItemSearch.OffItemNo, hit?.Id ?? 0);
            w.Bytes(4 + S2cItemSearch.OffItemName, name.AsSpan(0, Math.Min(name.Length, 64)));
            Push(s, w.Packet);
        }

        // ---- party map markers ----

        // GP_CLI_COMMAND_MAP_GROUP: GP_SERV_COMMAND_MAP_GROUP (0x0A0) for every alliance member in the char's zone and
        // mog house, the char too
        private void OnMapGroup(MapSession s, CharData c, BasicPacket p)
        {
            foreach (var m in ForAlliance(c))
            {
                if (!SameArea(m, c) || m.MoghouseId != c.MoghouseId) continue;
                var w = new PacketWriter(0x0A0, 24);
                w.U32(4 + 0, m.Id);
                w.U16(4 + 4, (short)m.Zone);
                w.F32(4 + 8, m.X);
                w.F32(4 + 12, m.Y);
                w.F32(4 + 16, m.Z);
                Push(s, w.Packet);
            }
        }

        // ---- the help desk ----

        // GP_SERV_COMMAND_FAQ_GMPARAM (0x0B5): what the client mostly ignores on opening the help desk
        private static BasicPacket FaqGmParamPacket(ushort id)
        {
            var w = new PacketWriter(0x0B5, 32);
            w.U32(4 + 4, 1);       // params[0], once the number of calls queued
            w.U16(4 + 20, id);     // the client's own Id back
            w.U16(4 + 26, 1800);   // RescueTime
            return w.Packet;
        }

        // GP_CLI_COMMAND_FAQ_GMCALL: a GM call comes in several packets, taken once the last (eos) is in
        private void OnFaqGmCall(MapSession s, CharData c, BasicPacket p)
        {
            if ((p.Buffer[4] & 0x0F) != (int)FaqGmcallType.GMCall) return; // AddHistory and GMNotice are ignored
            if (AddGmCallPacket(c, p))
            {
                ProcessGmCall(s, c);
                c.History[HistoryGmCalls]++;
            }
        }

        // GMCallContainer::addPacket: a new pktId starts over; at most 6 packets; a call within 10s of the last is dropped
        private bool AddGmCallPacket(CharData c, BasicPacket p)
        {
            byte pktId = p.Buffer[5];
            if (pktId != c.GmCallPktId)
            {
                c.GmCallPackets.Clear();
                c.GmCallPktId = pktId;
            }
            if (c.GmCallPackets.Count >= MaxGmCallPackets)
            {
                log_("GMCallContainer: Packet limit reached. Possible abuse.");
                return false;
            }
            // the struct as the C++ copies it: the bit fields and Data[256], zeros past what came in
            var copy = new byte[8 + 256];
            Array.Copy(p.Buffer, 0, copy, 0, Math.Min(copy.Length, p.Buffer.Length));
            c.GmCallPackets.Add(copy);
            if (p.Buffer[6] >> 7 != 1) return false;
            long now = NowMs();
            if (c.GmCallLastMs is long last && now < last + 10000)
            {
                c.GmCallPackets.Clear();
                c.GmCallPktId = 0;
                return false;
            }
            c.GmCallLastMs = now;
            return true;
        }

        // GMCallContainer::processCall: the blocks of the packets in seq order become parameters and the message; the
        // call goes to help_desk and to xi_world. The C++ reads the row's id with LAST_INSERT_ID in a transaction; each
        // query here takes its own pooled connection, so this reads the char's newest row (one call per 10s at most).
        private void ProcessGmCall(MapSession s, CharData c)
        {
            var parameters = new Dictionary<string, string>();
            string message = "";
            var packets = new List<byte[]>(c.GmCallPackets);
            packets.Sort((a, b) => (a[6] & 0x7F).CompareTo(b[6] & 0x7F)); // std::ranges::sort, not stable either
            foreach (var pk in packets)
            {
                var data = pk.AsSpan(8, 256);
                int offset = 0;
                for (int block = 0; block < pk[7] && offset < data.Length; block++)
                {
                    if (offset + 4 > data.Length) break;
                    byte type = data[offset];
                    int size = data[offset + 1];
                    if (size < 4 || offset + size > data.Length) break;
                    var blk = data.Slice(offset);
                    switch ((GmReportBlockType)type)
                    {
                        case GmReportBlockType.Version:
                            if (size >= 24)
                            {
                                parameters["VERSION.UNKNOWN00"] = BitConverter.ToUInt32(blk.Slice(4)).ToString();
                                parameters["VERSION.UNKNOWN01"] = $"{BitConverter.ToUInt32(blk.Slice(8))},{BitConverter.ToUInt32(blk.Slice(12))}," +
                                                                  $"{BitConverter.ToUInt32(blk.Slice(16))},{BitConverter.ToUInt32(blk.Slice(20))}";
                            }
                            break;
                        case GmReportBlockType.Error:
                            if (size >= 12)
                            {
                                parameters["ERROR.CODE"] = BitConverter.ToInt16(blk.Slice(4)).ToString();
                                parameters["ERROR.COUNT"] = BitConverter.ToUInt16(blk.Slice(6)).ToString();
                                parameters["ERROR.TIMECODE"] = BitConverter.ToUInt32(blk.Slice(8)).ToString();
                            }
                            break;
                        case GmReportBlockType.StringParam:
                        {
                            var str = blk.Slice(4, Math.Min(size - 4, 256));
                            int nul = str.IndexOf((byte)0);
                            string param = CborRaw.FromBytes(nul >= 0 ? str[..nul] : str);
                            int colon = param.IndexOf(':');
                            if (colon < 0) break;
                            string key = param.Substring(0, colon), value = param.Substring(colon + 1);
                            if (!AllowedGmCallParams.Contains(key))
                            {
                                log_("GMCallContainer: Unknown parameter key. Ignoring.");
                                break;
                            }
                            parameters[key] = value;
                            if (key == "GMCALL.INPUT") message = value;
                            break;
                        }
                        case GmReportBlockType.LobbyHistory:
                        {
                            int entries = Math.Min((size - 4) / 28, 8);
                            for (int i = 0; i < entries; i++)
                            {
                                var e = blk.Slice(4 + i * 28, 28);
                                uint ident = BitConverter.ToUInt32(e.Slice(8));
                                if (ident == 0) continue;
                                var name = e.Slice(12, 16);
                                int nul = name.IndexOf((byte)0);
                                string prefix = $"LOBBY.{i}";
                                parameters[prefix + ".CMD"] = BitConverter.ToUInt16(e).ToString();
                                parameters[prefix + ".OPT"] = BitConverter.ToUInt16(e.Slice(2)).ToString();
                                parameters[prefix + ".TIMECODE"] = BitConverter.ToUInt32(e.Slice(4)).ToString();
                                parameters[prefix + ".IDENT"] = ident.ToString();
                                parameters[prefix + ".NAME"] = CborRaw.FromBytes(nul >= 0 ? name[..nul] : name);
                            }
                            break;
                        }
                    }
                    offset += size;
                }
            }

            Exec("INSERT INTO help_desk (charid, message) VALUES (?, ?)", BoundValue.Of(c.Id), BoundValue.Of(CborRaw.Bytes(message)));
            uint callId = 0;
            var r = Exec("SELECT id FROM help_desk WHERE charid = ? ORDER BY id DESC LIMIT 1", BoundValue.Of(c.Id));
            if (r != null && r.Next()) callId = r.Get<uint>("id");

            SendIpc(new GMCallRequest
            {
                CallId = callId, CharId = c.Id, CharName = c.Name, AccId = s.AccountId, ZoneId = (ZoneId)c.Zone,
                PosX = c.X, PosY = c.Y, PosZ = c.Z, Message = message, Parameters = parameters,
            });
        }

        // GMCallContainer::sendPendingResponse: the oldest answer not yet read, as GP_SERV_COMMAND_SET_GMMSG (0x0B6)
        // packets of 244 bytes; the last one's pktNum is 0
        private void SendPendingGmResponse(MapSession s, CharData c)
        {
            var r = Exec("SELECT id, response FROM help_desk WHERE charid = ? AND response IS NOT NULL AND deleted_at IS NULL " +
                         "ORDER BY id ASC LIMIT 1", BoundValue.Of(c.Id));
            if (r == null || !r.Next()) return;
            uint callId = r.Get<uint>("id");
            var response = Encoding.UTF8.GetBytes(r.Get<string>("response") ?? "");
            int total = (response.Length + GmMsgChunk - 1) / GmMsgChunk;
            for (int i = 0; i < total; i++)
            {
                int off = i * GmMsgChunk;
                var w = new PacketWriter(0x0B6, 256);
                w.U32(4 + 0, callId);
                w.U16(4 + 4, i + 1);
                w.U16(4 + 6, i == total - 1 ? 0 : i + 1);
                w.Bytes(4 + 8, response.AsSpan(off, Math.Min(GmMsgChunk, response.Length - off)));
                Push(s, w.Packet);
            }
        }

        // GMCallContainer::acknowledgeOldestResponse, then the next answer if there is one
        private void AcknowledgeOldestGmResponse(MapSession s, CharData c)
        {
            Exec("UPDATE help_desk SET deleted_at = NOW() WHERE charid = ? AND response IS NOT NULL AND deleted_at IS NULL " +
                 "ORDER BY id ASC LIMIT 1", BoundValue.Of(c.Id));
            SendPendingGmResponse(s, c);
        }

        // ---- wide scan ----

        // charutils::getWideScanRange(job, level): RNG and BST by level; any other job only with ALL_JOBS_WIDESCAN
        private ushort WideScanRange(byte job, byte level)
        {
            bool allJobs = ChatCfg.AllJobsWidescan;
            if (job == JobRng) return (ushort)(level >= 80 ? 350 : level >= 60 ? 300 : level >= 40 ? 250 : level >= 20 ? 200 : 150);
            if (job == JobBst) return (ushort)(level >= 80 ? 300 : level >= 60 ? 250 : level >= 40 ? 200 : level >= 20 || allJobs ? 150 : 50);
            return (ushort)(allJobs ? 150 : 0);
        }

        private ushort WideScanRange(CharData c) => Math.Max(WideScanRange(c.MJob, c.MLvl), WideScanRange(c.SJob, c.SLvl));

        // CNpcEntity::isWideScannable
        private static bool IsWideScannable(NpcEntity n) =>
            n.Widescan && n.Status == NpcEntity.StatusNormal && (n.NameVis & NameVisHidden) == 0 && (n.EntityFlags & EntityFlagUntargetable) == 0;

        private static float DistanceSquared(CharData c, NpcEntity n)
        {
            float dx = c.X - n.X, dy = c.Y - n.Y, dz = c.Z - n.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        // CMobEntity::isWideScannable: not despawned, its name shown, targetable, not NO_WIDESCAN
        private static bool IsWideScannable(MobEntity m) =>
            m.Status != NpcEntity.StatusDisappear && (m.NameVis & MobEntity.NameVisHideName) == 0 && !m.Untargetable &&
            m.GetMobMod(MobMod.NoWidescan) == 0;

        // CZoneEntities::WideScan: the NPCs, then the mobs, in range and on the char's floor (the collision mesh's map
        // id under each) between ListStart and ListEnd (GP_SERV_COMMAND_TRACKING_STATE)
        private void WideScan(MapSession s, CharData c, ushort radius)
        {
            var mesh = XiMeshOf(c.Zone);
            byte charFloor = mesh?.GetFloorId(c.X, c.Y, c.Z) ?? 0;
            bool SameFloor(float x, float y, float z) => (mesh?.GetFloorId(x, y, z) ?? 0) == charFloor;
            bool InRange(float x, float y, float z)
            {
                float dx = c.X - x, dy = c.Y - y, dz = c.Z - z;
                return dx * dx + dy * dy + dz * dz <= (float)radius * radius;
            }
            // GP_SERV_COMMAND_TRACKING_LIST (0x0F4): ActIndex:16, Level:8 (mobs only), Type:3 = objtype / 2 (an NPC 1, a mob 2)
            void Track(ushort targid, byte level, uint type, float x, float z)
            {
                var w = new PacketWriter(0x0F4, 28);
                w.U32(4 + 0, targid | (uint)level << 16 | type << 24);
                w.U16(4 + 4, (short)(int)(x - c.X));
                w.U16(4 + 6, (short)(int)(z - c.Z));
                Push(s, w.Packet);
            }

            Push(s, TrackingStatePacket(TrackingListStart));
            var area = AreaOf(c);
            var npcs = new List<NpcEntity>(area.Npcs);
            npcs.Sort((a, b) => a.TargId.CompareTo(b.TargId)); // m_npcList is keyed by targid
            foreach (var n in npcs)
                if (IsWideScannable(n) && InRange(n.X, n.Y, n.Z) && SameFloor(n.X, n.Y, n.Z)) Track(n.TargId, 0, 1, n.X, n.Z);
            var mobs = new List<MobEntity>(area.Mobs);
            mobs.Sort((a, b) => a.TargId.CompareTo(b.TargId)); // m_mobList too
            foreach (var m in mobs)
                if (IsWideScannable(m) && InRange(m.X, m.Y, m.Z) && SameFloor(m.X, m.Y, m.Z)) Track(m.TargId, m.MLvl, 2, m.X, m.Z);
            Push(s, TrackingStatePacket(TrackingListEnd));
        }

        private static BasicPacket TrackingStatePacket(byte state)
        {
            var w = new PacketWriter(0x0F6, 8);
            w.U8(4, state);
            return w.Packet;
        }

        private NpcEntity NpcByTargId(CharData c, ushort targid)
        {
            foreach (var n in AreaOf(c).Npcs)
                if (n.TargId == targid) return n;
            return null;
        }

        // GP_CLI_COMMAND_TRACKING_START: track an NPC that is scannable and in range; an act index with none clears it
        private void OnTrackingStart(MapSession s, CharData c, BasicPacket p)
        {
            var n = NpcByTargId(c, (ushort)U32(p, 4));
            if (n == null)
            {
                c.WideScanTarget = null;
                return;
            }
            if (IsWideScannable(n) && MathF.Sqrt(DistanceSquared(c, n)) <= WideScanRange(c)) c.WideScanTarget = n.TargId;
        }

        // 0x015's tail: where the tracked NPC is now (GP_SERV_COMMAND_TRACKING_POS, 0x0F5); a gone one ends the tracking
        private void SendWideScanTrack(MapSession s, CharData c)
        {
            if (c.WideScanTarget is not ushort targid) return;
            var n = NpcByTargId(c, targid);
            if (n == null)
            {
                c.WideScanTarget = null;
                return;
            }
            var w = new PacketWriter(0x0F5, 24);
            w.F32(4 + 0, n.X);
            w.F32(4 + 4, n.Y);
            w.F32(4 + 8, n.Z);
            w.U8(4 + 12, 1); // Level
            w.U16(4 + 14, n.TargId);
            w.U8(4 + 16, n.Status == NpcEntity.StatusDisappear ? TrackingPosLose : TrackingPosStart);
            Push(s, w.Packet);
            if (n.Status == NpcEntity.StatusDisappear) c.WideScanTarget = null;
        }
    }

    // encoding::utf8ToShiftJis: each codepoint through sjis.h's table; one past 0xFFFF or unmapped is '?'
    internal static class Sjis
    {
        private static Dictionary<int, ushort> map_;

        public static byte[] FromUnicode(string s)
        {
            if (map_ == null)
            {
                var m = new Dictionary<int, ushort>(SjisTable.Pairs.Length / 2);
                for (int i = 0; i < SjisTable.Pairs.Length; i += 2) m[SjisTable.Pairs[i]] = SjisTable.Pairs[i + 1];
                map_ = m;
            }
            var result = new List<byte>(s.Length * 2);
            foreach (var rune in s.EnumerateRunes())
            {
                if (rune.Value > 0xFFFF || !map_.TryGetValue(rune.Value, out ushort sjis))
                {
                    result.Add((byte)'?');
                    continue;
                }
                if (sjis <= 0xFF) result.Add((byte)sjis);
                else
                {
                    result.Add((byte)(sjis >> 8));
                    result.Add((byte)sjis);
                }
            }
            return result.ToArray();
        }
    }
}
