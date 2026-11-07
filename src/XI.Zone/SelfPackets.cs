using System;
using System.Collections.Generic;
using XI.Map;

namespace XI.Zone
{
    // S2C replies to the client's requests about its own state (the packets the C2S handlers in ZoneHost.Packets push)
    public static class SelfPackets
    {
        // enums/msg_std.h, the ones these handlers send
        public const ushort MsgStyleLockOn = 267, MsgStyleLockOff = 268, MsgStyleLockIsOn = 269, MsgStyleLockIsOff = 270;

        // 0x009 GP_SERV_COMMAND_MESSAGE(MsgStd): just the message number
        public static BasicPacket Message(ushort messageId)
        {
            var w = new PacketWriter(0x009, 0x10);
            w.U16(10, messageId);
            return w.Packet;
        }

        // 0x029 GP_SERV_COMMAND_BATTLE_MESSAGE from and to the char itself
        public static BasicPacket BattleMessage(CharData c, int param, int value, ushort messageId) =>
            BattleMessage(c.Id, c.TargId, c.Id, c.TargId, param, value, messageId);

        // 0x029 GP_SERV_COMMAND_BATTLE_MESSAGE(PSender, PTarget, param0, param1, messageID)
        public static BasicPacket BattleMessage(uint senderId, ushort senderTargId, uint targetId, ushort targetTargId, int param, int value, ushort messageId)
        {
            var w = new PacketWriter(0x029, 0x1C);
            w.U32(4, senderId);
            w.U32(8, targetId);
            w.U32(12, (uint)param);
            w.U32(16, (uint)value);
            w.U16(20, senderTargId);
            w.U16(22, targetTargId);
            w.U16(24, messageId);
            return w.Packet;
        }

        // 0x02D GP_SERV_COMMAND_BATTLE_MESSAGE2(PSender, PTarget, param0, param1, messageID): UniqueNoCas, UniqueNoTar,
        // ActIndexCas, ActIndexTar, Data, Data2, MessageNum, Type 0
        public static BasicPacket BattleMessage2(BattleEntity sender, BattleEntity target, int param, int value, ushort messageId)
        {
            var w = new PacketWriter(0x02D, 0x1C);
            w.U32(4, sender.Id);
            w.U32(8, target.Id);
            w.U16(12, sender.TargId);
            w.U16(14, target.TargId);
            w.U32(16, (uint)param);
            w.U32(20, (uint)value);
            w.U16(24, messageId);
            return w.Packet;
        }

        // 0x11C GP_SERV_COMMAND_LOCKSTYLE_ERROR: the items a lockstyle Set could not use, up to 16
        public static BasicPacket LockstyleError(System.Collections.Generic.IReadOnlyList<ushort> failedItemIds)
        {
            var w = new PacketWriter(0x11C, 40);
            int count = Math.Min(failedItemIds.Count, 16);
            w.U8(4, count);
            for (int i = 0; i < count; i++) w.U16(8 + i * 2, failedItemIds[i]);
            return w.Packet;
        }

        private const int ServmesChunk = 236;

        // 0x04D GP_SERV_COMMAND_FRAGMENTS::SERVMES: up to 236 bytes of the NUL-terminated message from `offset`, the
        // packet cut to the data rounded up to 4
        public static BasicPacket Servmes(byte[] message, byte language, int timestamp, int offset, long unixNow)
        {
            var w = new PacketWriter(0x04D, 24 + ServmesChunk);
            w.U8(4, offset == 0 ? 1 : 2); // Command: 1 start, 2 the rest
            w.U8(5, 1);                   // Result
            w.U8(6, 1);                   // value1: server message
            w.U8(7, language);
            w.I32(8, timestamp == 0 ? (int)unixNow : timestamp);
            if (message.Length > 0 && message.Length > offset)
            {
                int msgSize = message.Length + 1;
                int sndLength = Math.Min(ServmesChunk, msgSize - offset);
                w.I32(12, msgSize);
                w.I32(16, offset);
                w.I32(20, sndLength);
                // the terminator is the zeroed buffer
                w.Bytes(24, message.AsSpan(offset, Math.Min(sndLength, message.Length - offset)));
                w.SetSize((uint)(24 + sndLength + 3) & ~3u);
            }
            return w.Packet;
        }

        // 0x04D GP_SERV_COMMAND_FRAGMENTS::FISHRANKING: the self block then up to five leaderboard blocks, 36 bytes each.
        // The size counts the leaderboard blocks only (the self block is not counted), so the last block is cut off.
        public static BasicPacket FishRanking(IReadOnlyList<FishingContest.Entry> entries, byte language, int timestamp, int offset,
            uint totalEntries, byte chunk, long unixNow)
        {
            const int dataOffset = 32;
            var w = new PacketWriter(0x04D, dataOffset + 6 * FishingContest.Entry.Size);
            w.U8(4, chunk);
            w.U8(5, 1);    // Result
            w.U8(6, 2);    // value1: fishing ranking
            w.U8(7, language);
            w.I32(8, timestamp == 0 ? (int)unixNow : timestamp);
            w.U32(24, totalEntries);
            if (entries.Count > 0 && entries.Count <= 6)
            {
                int sndLength = Math.Min((entries.Count - 1) * FishingContest.Entry.Size, 236);
                w.I32(12, (int)(totalEntries * FishingContest.Entry.Size));
                w.I32(16, offset);
                w.I32(20, sndLength);
                for (int i = 0; i < entries.Count; i++) entries[i].Write(w, dataOffset + i * FishingContest.Entry.Size);
                w.SetSize((uint)(dataOffset + sndLength));
            }
            return w.Packet;
        }

        // the unity fields of CLISTATUS: the leader's rank, the char_points accolades and whether the char is in its
        // unity's chat (PUnityChat)
        public readonly record struct UnityStatus(byte LeaderRank, int Accolades, int CurrentAccolades, int PrevAccolades, bool InUnityChat = false);

        // 0x061 GP_SERV_COMMAND_CLISTATUS: the status menu. No mastery (master levels) data.
        public static BasicPacket CliStatus(CharData c, CharStats stats, UnityStatus unity)
        {
            var w = new PacketWriter(0x061, 112);
            const int o = 4;
            w.I32(o + 0, c.MaxHp);
            w.I32(o + 4, c.MaxMp);
            w.U8(o + 8, c.MJob);
            w.U8(o + 9, c.MLvl);
            w.U8(o + 10, c.SJob);
            w.U8(o + 11, c.SLvl);
            w.U16(o + 12, c.JobExp[c.MJob]);
            w.U16(o + 14, (int)stats.ExpNextLevelFor(c, c.JobLevels[c.MJob]));
            for (int i = 0; i < 7; i++)
            {
                int stat = c.BaseStats[i];
                int mod = c.GetMod((Mod)((int)Mod.STR + i));
                // Hasso gives STR only with a two-handed main weapon
                if (i == 0 && c.MainWeapon != null && c.MainWeapon.TwoHanded) mod += c.GetMod(Mod.TWOHAND_STR);
                w.U16(o + 16 + 2 * i, stat);
                w.U16(o + 30 + 2 * i, Math.Clamp((short)mod, (short)(-999 + stat), (short)(999 - stat)));
            }
            w.U16(o + 44, stats.AttMain(c));
            w.U16(o + 46, stats.Def(c));
            for (int i = 0; i < 8; i++) w.U16(o + 48 + 2 * i, c.GetMod((Mod)((int)Mod.FIRE_MEVA + i)));
            w.U16(o + 64, c.Title);
            w.U16(o + 66, c.Nation < c.Rank.Length ? c.Rank[c.Nation] : 0);
            w.U16(o + 68, c.RankPoints);
            w.U16(o + 70, c.HomeZone);
            w.U8(o + 76, c.Nation);
            w.U8(o + 78, c.GetMod(Mod.SUPERIOR_LEVEL));
            w.U8(o + 80, CharStats.MaxItemLevel(c));
            w.U8(o + 81, CharStats.ItemLevelDifference(c));
            w.U8(o + 82, CharStats.MainhandItemLevel(c));
            w.U8(o + 83, CharStats.RangedItemLevel(c));
            // unityinfo_t: Faction:5, Unknown:5 (the leader's rank), Points:17
            w.U32(o + 84, (uint)(c.UnityLeader & 0x1F) | (uint)(unity.LeaderRank & 0x1F) << 5 | (uint)(unity.Accolades & 0x1FFFF) << 10);
            w.U16(o + 88, unity.CurrentAccolades / 1000);
            w.U16(o + 90, unity.PrevAccolades / 1000);
            w.U32(o + 92, unity.InUnityChat ? 1u : 0u); // unity_chat_color_flag
            return w.Packet;
        }

        // 0x062 GP_SERV_COMMAND_CLISTATUS2: the skills menu. The ability recasts in front are left 0, and the automaton
        // skills are hidden here (they are in another packet).
        public static BasicPacket CliStatus2(CharData c)
        {
            var w = new PacketWriter(0x062, 256);
            for (int i = 0; i < Skill.Max; i++)
                w.U16(128 + 2 * i, i >= Skill.AutomatonMelee && i <= Skill.AutomatonMagic ? 0x8000 : c.WorkingSkills[i]);
            return w.Packet;
        }

        // KeyItem::LimitBreaker / JobBreaker
        public const int KeyItemLimitBreaker = 606, KeyItemJobBreaker = 2544;

        // 0x063 GP_SERV_COMMAND_MISCDATA::MERITS (no BLU merits or job points yet, so no BLU spell point bonus)
        public static BasicPacket Merits(CharData c, CharStats stats)
        {
            var w = GameOkPackets.MiscData(2, 16);
            int jobLevel = c.JobLevels[c.MJob];
            uint expNext = stats.ExpNextLevelFor(c, jobLevel);
            bool atMaxLevelLimit = jobLevel >= c.Genkai;
            bool hasCappedXp = c.JobExp[c.MJob] == expNext - 1;
            bool canUseMeritMode = jobLevel >= 75 && c.HasKeyItem(KeyItemLimitBreaker);
            bool xpCappedOrMeritMode = atMaxLevelLimit && hasCappedXp || c.MeritMode;
            bool meritModeEnabled = canUseMeritMode && c.MeritMode;
            // BLU's spell point bonus: Assimilation at 75, the blue magic point bonus job point at 99
            int bluBonus = 0;
            if (c.MJob == JobBlu)
            {
                if (c.MLvl >= 75) bluBonus += c.Merits?.Value(c, MeritTable.Assimilation) ?? 0;
                if (c.MLvl >= 99) bluBonus += JobPointUtil.Value(c, JobPointUtil.BlueMagicPointBonus);
            }
            w.U16(8, c.LimitPoints);
            w.U16(10, c.MeritPoints & 0x7F | (bluBonus & 0x3F) << 7 | (canUseMeritMode ? 1 << 13 : 0) | (xpCappedOrMeritMode ? 1 << 14 : 0) |
                      (meritModeEnabled ? 1 << 15 : 0));
            w.U8(12, stats.Settings.MaxMeritPoints + (c.Merits?.Value(c, MeritTable.MaxMerit) ?? 0));
            return w.Packet;
        }

        // 0x063 GP_SERV_COMMAND_MISCDATA::JOB_POINTS: per job from WAR, capacity points, job points, job points spent
        private const int JobBlu = 16;

        public static BasicPacket JobPoints(CharData c)
        {
            var w = GameOkPackets.MiscData(5, 156);
            w.U8(8, c.HasKeyItem(KeyItemJobBreaker) ? 1 : 0);
            for (int i = 1; i < CharData.MaxJobs; i++)
            {
                var jp = c.JobPoints[i];
                w.U16(12 + 6 * i, jp.Capacity);
                w.U16(14 + 6 * i, jp.Current);
                w.U16(16 + 6 * i, jp.Spent);
            }
            return w.Packet;
        }
    }
}
