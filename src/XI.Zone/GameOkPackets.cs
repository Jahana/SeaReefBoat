using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XI.Map;

namespace XI.Zone
{
    // unity_system: per unity leader (1..11), the contributing members and points of the current and previous week
    public sealed class UnityStandings
    {
        public readonly int[] MembersCurrent = new int[11], MembersPrevious = new int[11];
        public readonly double[] PointsCurrent = new double[11], PointsPrevious = new double[11];
    }

    // The packets the C++ map pushes on the client's 0x00C GP_CLI_COMMAND_GAMEOK (packets/c2s/0x00c_gameok.cpp), in
    // its order. Layouts follow src/map/packets/s2c/*; everything a character can be that the loader does not model
    // (monstrosity, linkshells, a filled treasure pool, merit counts, blacklist entries) is written as absent.
    public static class GameOkPackets
    {
        // 0x008 GP_SERV_COMMAND_ENTERZONE: EnterZoneTbl[48] is memcpy'd from m_ZonesVisitedList, reading past its 38
        // bytes into the spell list that follows it
        public static BasicPacket EnterZone(CharData c)
        {
            var w = new PacketWriter(0x008, 52);
            w.Bytes(4, c.ZonesVisited);
            w.Bytes(4 + 38, c.SpellList.AsSpan(0, 10));
            return w.Packet;
        }

        // 0x0B4 GP_SERV_COMMAND_CONFIG: SAVE_CONF (settings, both chat filters, PvpFlg, AreaCode), then the party
        // languages
        public static BasicPacket Config(CharData c)
        {
            var w = new PacketWriter(0x0B4, 24);
            w.U32(4, c.Settings);
            w.U32(8, c.ChatFilters1);
            w.U32(12, c.ChatFilters2);
            w.U8(20, c.Languages);
            return w.Packet;
        }

        // 0x01B GP_SERV_COMMAND_JOB_INFO
        public static BasicPacket JobInfo(CharData c, long unixNow)
        {
            var w = new PacketWriter(0x01B, 132);
            w.U16(6, c.Look.Race);                                     // face_no
            w.U8(8, c.MJob);
            w.U8(11, c.SJob);
            w.U32(12, c.UnlockedJobs);                                 // get_job_flag
            for (int i = 0; i < 16; i++) w.U8(16 + i, c.JobLevels[i]);
            for (int i = 0; i < 7; i++) w.U16(32 + 2 * i, c.BaseStats[i]);
            w.I32(60, c.MaxHp);
            w.I32(64, c.MaxMp);
            w.U8(68, (int)(c.UnlockedJobs & 1));                       // sjobflg
            for (int i = 0; i < CharData.MaxJobs; i++) w.U8(72 + i, c.JobLevels[i]);
            w.U32(96, (uint)(c.EquipBlock | c.StatsDebilitation << 16)); // encumbrance
            w.U8(100, c.GetCharVar("[ASSIST][ThumbsUp]Cooldown", unixNow) == 0 ? 1 : 0);
            w.U8(101, Math.Clamp(c.GetCharVar("[ASSIST][Evaluations]Count", unixNow) / 50 + 1, 1, 3)); // mentor_rank
            w.U8(102, 1);                                              // mastery_rank
            return w.Packet;
        }

        // 0x08E GP_SERV_COMMAND_ALTER_EGO_POINTS
        public static BasicPacket AlterEgoPoints(CharData c)
        {
            var w = new PacketWriter(0x08E, 104);
            w.U16(4, c.AlterEgoPoints);
            return w.Packet;
        }

        // 0x063 GP_SERV_COMMAND_MISCDATA: the type, then the body size in unknown06
        internal static PacketWriter MiscData(int type, uint size)
        {
            var w = new PacketWriter(0x063, size);
            w.U16(4, type);
            w.U16(6, (int)size - 4);
            return w;
        }

        // the Monstrosity pages: empty outside monstrosity (Monstrosity.cs fills a Monipulator's)
        public static BasicPacket Monstrosity1(CharData c = null, int infamy = 0) => XI.Zone.Monstrosity.Monstrosity1(c, infamy);
        public static BasicPacket Monstrosity2(CharData c = null) => XI.Zone.Monstrosity.Monstrosity2(c);

        // 16 bytes each of the homepoint, survival guide and waypoint access bits
        public static BasicPacket Homepoints(CharData c)
        {
            var w = MiscData(6, 72);
            w.Bytes(8, c.HomepointTele.AsSpan(0, 16));
            w.Bytes(24, c.SurvivalTele.AsSpan(0, 16));
            w.Bytes(40, c.Waypoints.AsSpan(0, 16));
            return w.Packet;
        }

        private static PacketWriter Unity(int resultSet, int dataType, uint size)
        {
            var w = MiscData(7, size);
            w.U8(8, resultSet);
            w.U8(9, dataType);
            return w;
        }

        // the previous week's base page carries the vanadiel time the rankings were finalised (the current time, for now)
        public static BasicPacket UnityBase(int resultSet, int dataType, long unixNow)
        {
            var w = Unity(resultSet, dataType, 148);
            if (dataType == 0 && resultSet == 0) w.U32(16, LoginPackets.VanadielTimestamp(unixNow));
            return w.Packet;
        }

        public static BasicPacket UnityMembers(int resultSet, int[] members)
        {
            var w = Unity(resultSet, 1, 152);
            w.U16(16, 0x0108);
            for (int i = 0; i < 11; i++) w.U32(20 + 4 * i, (uint)members[i]);
            return w.Packet;
        }

        public static BasicPacket UnityPoints(int resultSet, double[] points)
        {
            var w = Unity(resultSet, 2, 152);
            w.U16(16, resultSet == 0 ? 0x0207 : 0x0208);
            for (int i = 0; i < 11; i++) w.U32(20 + 4 * i, (uint)points[i]);
            return w.Packet;
        }

        // DATA and PERSONAL share a layout: a uint16 after the header
        public static BasicPacket UnityData(int resultSet, int dataType, int value)
        {
            var w = Unity(resultSet, dataType, 148);
            w.U16(16, value);
            return w.Packet;
        }

        // the cur-week 0x10..0x1F values retail sends (0x14 is the personal ranking, not tracked)
        private static readonly ushort[] UnityCurrentData =
            { 0x2007, 0x2CC2, 0x6867, 0x6E6F, 0, 0x3605, 0x2007, 0x6C6C, 0x616E, 0x6767, 0, 0x2007, 0x2007, 0x0022, 0x0004, 0x2007 };

        // charutils::SendUnityPackets
        public static void AddUnity(List<BasicPacket> list, UnityStandings u, long unixNow)
        {
            for (int rs = 0; rs <= 1; rs++)
            {
                list.Add(UnityBase(rs, 0, unixNow));
                list.Add(UnityMembers(rs, rs == 0 ? u.MembersPrevious : u.MembersCurrent));
                list.Add(UnityPoints(rs, rs == 0 ? u.PointsPrevious : u.PointsCurrent));
                for (int t = 3; t < 0x10; t++) list.Add(UnityBase(rs, t, unixNow));
                for (int t = 0x10; t < 0x20; t++) list.Add(UnityData(rs, t, rs == 0 ? 0x0008 : UnityCurrentData[t - 0x10]));
            }
        }

        // STATUS_ICONS: every iconed effect (a deleted one not yet swept included) with when it wears off, in Vana'diel
        // time * 60 (wrapping at 32 bits); 0x7FFFFFFF for one without a duration or with a hidden timer, 0x00FF after
        public static BasicPacket StatusIcons(CharData c, long nowMs, long unixNow)
        {
            var w = MiscData(9, 200);
            for (int i = 0; i < 32; i++) w.U16(8 + 2 * i, 0x00FF);
            int n = 0;
            foreach (var e in c.StatusEffects.All)
            {
                if (n == 32) break;
                if (e.Icon == 0) continue;
                uint ts = 0x7FFFFFFF;
                if (e.DurationMs > 0 && !e.HasFlag(EffectFlag.HideTimer))
                {
                    uint seconds = (uint)(long)Math.Floor((e.StartMs - nowMs + e.DurationMs) / 1000.0);
                    seconds += LoginPackets.VanadielTimestamp(unixNow);
                    ts = seconds * 60;
                }
                w.U16(8 + 2 * n, e.Icon);
                w.U32(72 + 4 * n, ts);
                n++;
            }
            return w.Packet;
        }

        // 0x055 GP_SERV_COMMAND_SCENARIOITEM: one key item table, owned bits then seen bits
        public static BasicPacket ScenarioItem(CharData c, int table)
        {
            var w = new PacketWriter(0x055, 136);
            w.Bytes(4, c.KeyItems.AsSpan(table * 128, 64));
            w.Bytes(68, c.KeyItems.AsSpan(table * 128 + 64, 64));
            w.U16(132, table);
            return w.Packet;
        }

        // ---- 0x056 GP_SERV_COMMAND_MISSION ----

        // QuestOffer / QuestComplete ports, indexed by QuestLog
        private static readonly ushort[] QuestOfferPort = { 0x50, 0x58, 0x60, 0x68, 0x70, 0x78, 0x80, 0x88, 0xE0, 0xF0, 0x100 };
        private static readonly ushort[] QuestCompletePort = { 0x90, 0x98, 0xA0, 0xA8, 0xB0, 0xB8, 0xC0, 0xC8, 0xE8, 0xF8, 0x108 };
        private const int QuestAhtUrghan = 6;
        private const int MissionToAU = 4, MissionWoTG = 5, MissionCoP = 6, MissionZilart = 3;
        private const int MissionACP = 9, MissionAMK = 10, MissionASA = 11, MissionSoA = 12, MissionRoV = 13;

        private static BasicPacket MissionOther(int port, uint[] data)
        {
            var w = new PacketWriter(0x056, 40);
            for (int i = 0; i < 8; i++) w.U32(4 + 4 * i, data[i]);
            w.U16(36, port);
            return w.Packet;
        }

        private static uint[] Words(byte[] src, int off)
        {
            var d = new uint[8];
            for (int i = 0; i < 8; i++) d[i] = BitConverter.ToUInt32(src, off + 4 * i);
            return d;
        }

        public static BasicPacket QuestOffer(CharData c, int log)
        {
            var d = Words(c.QuestLog, log * CharData.QuestLogSize);
            if (log == QuestAhtUrghan)
            {
                // the ToAU offer page also carries the current Assault, ToAU, WoTG and Campaign missions
                d[4] = c.AssaultCurrent;
                d[5] = c.MissionCurrent(MissionToAU);
                d[6] = c.MissionCurrent(MissionWoTG);
                d[7] = c.CampaignCurrent;
            }
            return MissionOther(QuestOfferPort[log], d);
        }

        public static BasicPacket QuestComplete(CharData c, int log)
        {
            var d = Words(c.QuestLog, log * CharData.QuestLogSize + 32);
            if (log == QuestAhtUrghan)
                for (int id = 0; id < 128; id++)
                    if (c.AssaultComplete(id)) d[4 + id / 32] |= 1u << (id % 32);
            return MissionOther(QuestCompletePort[log], d);
        }

        // MissionComplete::Nations: San, Bas, Win, Zilart, two words each
        public static BasicPacket NationsComplete(CharData c)
        {
            var d = new uint[8];
            for (int log = 0; log <= MissionZilart; log++)
                for (int id = 0; id < 64; id++)
                    if (c.MissionComplete(log, id)) d[log * 2 + id / 32] |= 1u << (id % 32);
            return MissionOther(0xD0, d);
        }

        public static BasicPacket ToAUWoTGComplete(CharData c)
        {
            var d = new uint[8];
            for (int id = 0; id < 64; id++)
            {
                if (c.MissionComplete(MissionToAU, id)) d[id / 32] |= 1u << (id % 32);
                if (c.MissionComplete(MissionWoTG, id)) d[2 + id / 32] |= 1u << (id % 32);
            }
            return MissionOther(0xD8, d);
        }

        // Campaign1 holds missions 0..255, Campaign2 256..511
        public static BasicPacket CampaignComplete(CharData c, int half)
        {
            var d = new uint[8];
            for (int id = half * 256; id < half * 256 + 256; id++)
                if (c.CampaignComplete(id)) d[id % 256 / 32] |= 1u << (id % 32);
            return MissionOther(half == 0 ? 0x30 : 0x38, d);
        }

        // MISSION: the current nation and expansion missions; a declined "Tales Beginning" zeroes its expansion
        public static BasicPacket Mission(CharData c, long unixNow)
        {
            bool Declined(string exp) => c.GetCharVar($"[{exp}]TalesBeginning", unixNow) > 0;
            bool roz = Declined("ROZ"), cop = Declined("COP"), acp = Declined("ACP"), amk = Declined("AMK"),
                asa = Declined("ASA"), soa = Declined("SOA"), rov = Declined("ROV");

            var w = new PacketWriter(0x056, 40);
            w.U32(4, c.Nation);
            w.U32(8, c.MissionCurrent(c.Nation));
            w.U32(12, roz ? 0u : c.MissionCurrent(MissionZilart));
            w.U32(16, cop ? 0u : c.MissionCurrent(MissionCoP));
            w.U32(20, (uint)c.MissionStatusUpper(MissionCoP) << 16 | c.MissionStatusLower(MissionCoP));
            int addons = (acp ? 0 : c.MissionCurrent(MissionACP) & 0xF)
                       | (amk ? 0 : c.MissionCurrent(MissionAMK) & 0xF) << 4
                       | (asa ? 0 : c.MissionCurrent(MissionASA) & 0xF) << 8;
            w.U16(24, addons);
            int tales = (roz ? 1 : 0) | (acp ? 2 : 0) | (asa ? 4 : 0) | (cop ? 8 : 0) | (soa ? 0x20 : 0) | (rov ? 0x40 : 0);
            w.U16(26, tales);
            w.U32(28, soa ? 0u : (uint)(c.MissionCurrent(MissionSoA) * 2 + 0x6E));
            w.U32(32, rov ? 0u : (uint)(c.MissionCurrent(MissionRoV) + 0x6C));
            w.U16(36, 0xFFFF);
            return w.Packet;
        }

        // TVR: not tracked, so the empty log
        public static BasicPacket MissionTvr()
        {
            var w = new PacketWriter(0x056, 40);
            w.U16(36, 0xFFFE);
            return w.Packet;
        }

        // charutils::SendQuestMissionLog, in its retail order
        public static void AddQuestMissionLog(List<BasicPacket> list, CharData c, long unixNow)
        {
            for (int log = 0; log <= 7; log++) list.Add(QuestOffer(c, log));
            for (int log = 0; log <= 7; log++) list.Add(QuestComplete(c, log));
            list.Add(NationsComplete(c));
            list.Add(ToAUWoTGComplete(c));
            list.Add(CampaignComplete(c, 0));
            list.Add(CampaignComplete(c, 1));
            for (int log = 8; log <= 10; log++)
            {
                list.Add(QuestOffer(c, log));
                list.Add(QuestComplete(c, log));
            }
            list.Add(Mission(c, unixNow));
            list.Add(MissionTvr());
        }

        // 0x110 GP_SERV_COMMAND_UNITY: sparks/deeds/plaudits and the RoE unity rotation, all left 0 without a
        // char_points row
        public static BasicPacket Unity(CharData c, long unixNow)
        {
            var w = new PacketWriter(0x110, 20);
            if (c.HasPointsRow)
            {
                w.U8(4, c.Sparks);
                w.U8(5, c.Sparks >> 8);
                w.U8(6, c.Sparks >> 16);
                w.U16(8, c.Deeds);
                w.U16(10, c.Plaudits);
                uint vana = LoginPackets.VanadielTimestamp(unixNow);
                w.U8(12, (int)(vana / 86400 % 6));      // RoEUnityShared
                w.U8(13, (int)(vana / 604800 % 4));     // RoEUnityLeader
                w.Fill(14, 6, 0xFF);
            }
            return w.Packet;
        }

        // 0x111 GP_SERV_COMMAND_ROE_ACTIVELOG: the 31 active records (the 31st in slot 63), id | progress << 12
        public static BasicPacket RoeActiveLog(CharData c)
        {
            var w = new PacketWriter(0x111, 260);
            for (int i = 0; i < 31; i++)
            {
                uint active = BitConverter.ToUInt16(c.EminenceLog, 2 * i);
                uint progress = BitConverter.ToUInt32(c.EminenceLog, 64 + 4 * i);
                w.U32(4 + 4 * (i < 30 ? i : 63), active | progress << 12);
            }
            return w.Packet;
        }

        // 0x112 GP_SERV_COMMAND_ROE_LOG: a quarter of the completed-record bits
        public static BasicPacket RoeLog(CharData c, int order)
        {
            var w = new PacketWriter(0x112, 136);
            w.Bytes(4, c.EminenceLog.AsSpan(188 + order * 128, 128));
            w.U16(132, order);
            return w.Packet;
        }

        // 0x0AE GP_SERV_COMMAND_MOUNT_DATA: the mount key items (table 6)
        public static BasicPacket MountData(CharData c)
        {
            var w = new PacketWriter(0x0AE, 12);
            w.Bytes(4, c.KeyItems.AsSpan(6 * 128, 8));
            return w.Packet;
        }

        // 0x0AD GP_SERV_COMMAND_DUNGEON: Maze Monger vouchers and runes
        public static BasicPacket Dungeon(CharData c)
        {
            var w = new PacketWriter(0x0AD, 132);
            w.Bytes(4, c.MazeVouchers);
            w.Bytes(12, c.MazeRunes);
            return w.Packet;
        }

        // 0x067 CCharSyncPacket. owner: the host of the mog house the char is visiting
        // (MogHouseContainer::host), whose 2F flag it shows; the char's own when null
        public static BasicPacket CharSync(CharData c, CharData owner = null)
        {
            var w = new PacketWriter(0x067, 40);
            w.U8(4, 0x02);
            w.U8(5, 0x09);
            w.U16(6, c.TargId);
            w.U32(8, c.Id);
            // 0x02 campaign battle (ALLIED_TAGS), 0x04 level sync (only shown outside a battlefield)
            byte flags = (byte)(c.StatusEffects.HasStatusEffect(EffectId.AlliedTags) ? 0x02 : 0x00);
            if (c.LevelRestriction != 0 && c.StatusEffects.HasStatusEffect(EffectId.LevelSync))
            {
                if (c.PBattlefield == null) flags |= 0x04;
                w.U8(0x26, (byte)c.LevelRestriction);
            }
            w.U8(0x10, flags);
            if (c.StatusEffects.GetStatusEffect(EffectId.Mounted) is { } mounted)
            {
                var (_, custom0, custom1) = MountUtils.PacketDefinition(c);
                w.U16(0x13, mounted.SubPower);
                w.U32(0x18, custom0);                                                   // a personal chocobo's model
                w.U32(0x1C, custom1);                                                   // the Noble Chocobo
            }
            if (c.MoghouseId != 0 && c.MoghouseId != c.Id) w.U32(0x20, c.MoghouseId); // the owner of the one visited
            w.U8(0x24, c.MogHouseOpen ? 1 : 0);                                         // MogHouseFlag: open to visitors
            w.U8(0x25, c.JobLevels[c.MJob]);
            w.U8(0x27, ((owner ?? c).MhFlag & 0x20) != 0 ? 1 : 0);                     // MogExpansionFlag
            return w.Packet;
        }

        // 0x0CA GP_SERV_COMMAND_INSPECT_MESSAGE for the character itself
        public static BasicPacket InspectMessage(CharData c) => InspectMessage(c, c);

        // the target's bazaar message, name and title, as `checker` sees it
        public static BasicPacket InspectMessage(CharData checker, CharData target)
        {
            var w = new PacketWriter(0x0CA, 148);
            w.Bytes(4, target.BazaarMessage.AsSpan(0, Math.Min(target.BazaarMessage.Length, 123)));
            w.U8(127, (byte)(1 | (checker == target ? 1 << 1 : 0) | 1 << 2)); // BazaarFlag, MyFlag, Race = 1
            w.Name(128, target.Name, 16);
            w.U32(144, target.Title);
            return w.Packet;
        }

        // 0x08C GP_SERV_COMMAND_MERIT x5: every slot's id, next cost and upgrades, in the table's id order (next is
        // cleared outside a mog house; the slots past the merits stay zero). A char with no merits loaded holds none.
        public static void AddMerits(List<BasicPacket> list, MeritTable table, CharData c)
        {
            for (int p = 0; p < 5; p++)
            {
                var w = new PacketWriter(0x08C, 256);
                w.U16(4, MeritTable.PerPacket);
                for (int i = 0; i < MeritTable.PerPacket; i++)
                {
                    int index = p * MeritTable.PerPacket + i;
                    if (index >= table.Entries.Count) break;
                    var e = table.Entries[index];
                    byte count = c.Merits?.Counts[index] ?? 0;
                    w.U16(8 + 4 * i, e.Id);
                    if (c.MoghouseId != 0) w.U8(10 + 4 * i, MeritTable.NextCost(e, count));
                    w.U8(11 + 4 * i, count);
                }
                list.Add(w.Packet);
            }
        }

        // 0x08C GP_SERV_COMMAND_MERIT for one merit: a count of 1 and the merit in the first slot (sized for two)
        public static BasicPacket Merit(CharData c, ushort id)
        {
            var w = new PacketWriter(0x08C, 0x08 + 2 * 4);
            w.U16(4, 1);
            int index = c.Merits?.Table.IndexOf(id) ?? -1;
            if (index >= 0)
            {
                w.U16(8, id);
                w.U8(10, c.Merits.Next(index));
                w.U8(11, c.Merits.Counts[index]);
            }
            return w.Packet;
        }

        // ItemLockFlg values lockFlagFor picks: a bazaar price, then equipped (a linkshell its own flag), then an
        // installed furnishing
        private const byte LockFlgNormal = 0, LockFlgNoDrop = 5, LockFlgBazaar = 0x19;

        // lockFlagFor
        public static byte LockFlagFor(CharData c, Item item) =>
            item.CharPrice != 0 ? LockFlgBazaar
            : IsEquipped(c, item) ? (LinkshellItem.Is(item) ? LinkshellItem.LockFlgLinkshell : LockFlgNoDrop)
            : item.Installed ? LockFlgNoDrop
            : LockFlgNormal;

        // ITEM_ATTR's overrides of the Attr, set by the zone host that knows the item tables: an ITEM_CHARGED item's
        // flag, charges and ready/recharging bits (that enable the client's Use), then an unlockable weapon's zeroed
        // points
        public static Action<Item, byte[]> AttrOverride;

        // 0x020 GP_SERV_COMMAND_ITEM_ATTR; lockFlg from LockFlagFor. Attr is the item's exdata after AttrOverride.
        public static BasicPacket ItemAttr(Item item, byte lockFlg) => ItemAttr(item, lockFlg, item.Location, item.Slot);

        // ITEM_ATTR(item, location, slot): the item as if at that place
        public static BasicPacket ItemAttr(Item item, byte lockFlg, Container location, byte slot)
        {
            var w = new PacketWriter(0x020, 44);
            w.U32(4, item.Quantity);
            w.U32(8, item.CharPrice);
            w.U16(12, item.ItemId);
            w.U8(14, (byte)location);
            w.U8(15, slot);
            w.U8(16, lockFlg);
            var attr = item.Extra;
            if (AttrOverride != null)
            {
                attr = (byte[])attr.Clone();
                AttrOverride(item, attr);
            }
            w.Bytes(17, attr);
            return w.Packet;
        }

        // 0x020 for an empty slot (a null PItem); `staleExtra` is the exdata retail leaks from the item that just left it
        public static BasicPacket EmptySlotAttr(Container location, byte slot, byte[] staleExtra = null)
        {
            var w = new PacketWriter(0x020, 44);
            w.U8(14, (byte)location);
            w.U8(15, slot);
            if (staleExtra != null) w.Bytes(17, staleExtra);
            return w.Packet;
        }

        // 0x01D GP_SERV_COMMAND_ITEM_SAME: a container finished syncing, with the running bitmask of synced ones
        public static BasicPacket ItemSame(int state, int container, uint flags)
        {
            var w = new PacketWriter(0x01D, 12);
            w.U8(4, state);
            w.U8(5, container);
            w.U32(8, flags);
            return w.Packet;
        }

        private static readonly Container[] SyncOrder =
        {
            Container.Inventory, Container.MogSafe, Container.MogSafe2, Container.Storage, Container.RecycleBin,
            Container.Wardrobe, Container.Wardrobe2, Container.Wardrobe3, Container.Wardrobe4, Container.Wardrobe5,
            Container.Wardrobe6, Container.Wardrobe7, Container.Wardrobe8, Container.TempItems, Container.MogLocker,
            Container.MogSatchel, Container.MogSack, Container.MogCase,
        };

        // every container synced: what 0x01D carries once the inventory is loaded
        public static readonly uint AllSyncedMask = (1u << CharData.MaxContainers) - 1;

        private static bool IsEquipped(CharData c, Item item) =>
            c.Equip.Exists(e => e.Container == item.Location && e.InvSlot == item.Slot);

        // charutils::SendInventory: each container's items then its 0x01D, the equipped items, then the all-loaded 0x01D. A
        // visitor gets the host's installed furniture as its safes, compacted to slots 1..N
        public static void AddInventory(List<BasicPacket> list, CharData c, CharData visitedOwner = null)
        {
            uint synced = 0;
            foreach (var container in SyncOrder)
            {
                if (visitedOwner != null && (container == Container.MogSafe || container == Container.MogSafe2))
                {
                    var furniture = visitedOwner.Items.FindAll(i => i.Location == container && i.Slot <= visitedOwner.StorageSize[(int)container] && i.Installed);
                    furniture.Sort((a, b) => a.Slot.CompareTo(b.Slot));
                    byte slot = 1;
                    foreach (var item in furniture) list.Add(ItemAttr(item, LockFlagFor(visitedOwner, item), container, slot++));
                    synced |= 1u << (int)container;
                    list.Add(ItemSame(0, (int)container, synced));
                    continue;
                }
                var items = c.Items.FindAll(i => i.Location == container && i.Slot <= c.StorageSize[(int)container]);
                items.Sort((a, b) => a.Slot.CompareTo(b.Slot));
                foreach (var item in items) list.Add(ItemAttr(item, LockFlagFor(c, item)));
                synced |= 1u << (int)container;
                list.Add(ItemSame(0, (int)container, synced));
            }
            foreach (var e in c.Equip)
                if (e.EquipSlot < 16) list.Add(LoginPackets.ItemList(c.FindItem(e.Container, e.InvSlot), 5));
            // the linkshells: their attr and lock, then which item is in the slot
            for (int lsNum = 1; lsNum <= 2; lsNum++)
            {
                var ls = LinkshellItem.Equipped(c, lsNum == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2);
                if (ls == null) continue;
                list.Add(ItemAttr(ls, LinkshellItem.LockFlgLinkshell));
                list.Add(LoginPackets.ItemList(ls, LinkshellItem.LockFlgLinkshell));
                list.Add(ZoneHost.GroupComlink(c, lsNum));
            }
            list.Add(ItemSame(1, 0x12, synced));
            c.SyncedContainers = synced;
        }

        // 0x041 GP_SERV_COMMAND_BLACK_LIST with an empty list: reset the client's list, and this is the last page
        public static BasicPacket BlackList() => BlackList(new List<(uint, string)>(), true, true);

        // 0x041 GP_SERV_COMMAND_BLACK_LIST: up to 12 SAVE_BLACK (ID, Name[16]); Stat 0x01 resets the client's list,
        // 0x02 marks the last page
        public static BasicPacket BlackList(List<(uint Id, string Name)> entries, bool reset, bool last)
        {
            var w = new PacketWriter(0x041, 248);
            for (int i = 0; i < entries.Count && i < 12; i++)
            {
                w.U32(4 + i * 20, entries[i].Id);
                var name = System.Text.Encoding.Latin1.GetBytes(entries[i].Name);
                w.Bytes(8 + i * 20, name.AsSpan(0, Math.Min(name.Length, 16)));
            }
            w.U8(244, (byte)((reset ? 0x01 : 0) | (last ? 0x02 : 0)));
            w.U8(245, (byte)entries.Count);
            return w.Packet;
        }

        // 0x0D2 GP_SERV_COMMAND_TROPHY_LIST: CTreasurePool::updatePool sends all 10 (empty) slots as old items
        public static BasicPacket TrophyList(int slot)
        {
            var w = new PacketWriter(0x0D2, 60);
            w.U32(4, 1);           // TrophyItemNum
            w.U8(20, slot);        // TrophyItemIndex
            w.U8(21, 1);           // Entry: old item
            return w.Packet;
        }

        // The reply to 0x00C for a character outside monstrosity and the mog house, in 0x00c_gameok.cpp's order (the
        // transport spawn and everything after the treasure pool are not included). treasurePool adds an empty pool's 10
        // slots; the zone host leaves them out and sends the char's own pool after the pending GM response.
        public static List<BasicPacket> OnGameOk(CharData c, ZoneInfo z, long unixNow, UnityStandings unity, MeritTable merits, bool enableRoe,
                                                 List<BasicPacket> blacklist = null, int roeTimedNotify = -1, CharData mhOwner = null,
                                                 bool treasurePool = true, List<BasicPacket> extendedJob = null, long nowMs = 0,
                                                 int infamy = 0)
        {
            var list = new List<BasicPacket>
            {
                EnterZone(c), LoginPackets.ItemMax(c), Config(c), LoginPackets.GrapList(c), JobInfo(c, unixNow),
                AlterEgoPoints(c), LoginPackets.ServerStatus(c, z, unixNow),
                Monstrosity1(c, infamy), Monstrosity2(c), Homepoints(c),
            };
            // charutils::SendExtendedJobPackets
            if (extendedJob != null) list.AddRange(extendedJob);
            AddUnity(list, unity, unixNow);
            list.Add(StatusIcons(c, nowMs, unixNow));
            for (int t = 0; t < 8; t++) list.Add(ScenarioItem(c, t));
            AddQuestMissionLog(list, c, unixNow);
            list.Add(Unity(c, unixNow));
            if (enableRoe)
            {
                list.Add(RoeActiveLog(c));
                // a char logging in to a new timed record hears of it once (roeTimedNotify is that record)
                if (roeTimedNotify >= 0) list.Add(SelfPackets.BattleMessage(c, roeTimedNotify, 0, 705));
                for (int o = 0; o < 4; o++) list.Add(RoeLog(c, o));
            }
            list.Add(LoginPackets.MagicData(c));
            list.Add(MountData(c));
            list.Add(Dungeon(c));
            list.Add(LoginPackets.CommandData(c));
            list.Add(CharSync(c, mhOwner));
            list.Add(InspectMessage(c));
            AddMerits(list, merits, c);
            AddInventory(list, c, mhOwner != null && mhOwner != c ? mhOwner : null);
            if (blacklist != null) list.AddRange(blacklist);
            else list.Add(BlackList());
            if (treasurePool)
                for (int s = 0; s < 10; s++) list.Add(TrophyList(s));
            return list;
        }
    }
}
