using System.Collections.Generic;
using XI.Common;
using XI.Map;

namespace XI.Zone
{
    // The S2C packets a zone-in sends, ported from src/map/packets/s2c/* and packets/char_update.cpp / char_status.cpp.
    // Offsets are packet-relative; bitfield masks come from the layout oracles (oracle/map_s2c_char_pc_golden.txt,
    // map_s2c_serverstatus_golden.txt). Time-dependent fields take `unixNow` (earth_time::timestamp); vanadiel time is
    // derived from it. Everything a character can be that the loader does not model yet (linkshells, pets, mounts,
    // monstrosity, costumes, events, mog house) is written as its absent/default state; the status icons come from
    // the char's StatusEffectContainer.
    public static class LoginPackets
    {
        public static uint VanadielTimestamp(long unixNow) => (uint)(unixNow - VanaTime.EpochUnixSec);

        // GrapIDTbl[9]: the model of each visible slot tagged with its slot in the top nibble
        private static void Grap(PacketWriter w, int off, CharData c, ushort first)
        {
            var l = c.ShownLook;
            w.U16(off + 0, first);
            w.U16(off + 2, c.DisplayHeadOffFlg ? 0 : l.Head + 0x1000);
            w.U16(off + 4, l.Body + 0x2000);
            w.U16(off + 6, l.Hands + 0x3000);
            w.U16(off + 8, l.Legs + 0x4000);
            w.U16(off + 10, l.Feet + 0x5000);
            w.U16(off + 12, l.Main + 0x6000);
            w.U16(off + 14, l.Sub + 0x7000);
            w.U16(off + 16, l.Ranged + 0x8000);
        }

        // GP_SERV_CHAR_PC SendFlg bits; UPDATE_ALL_CHAR is the first five
        public const byte SendPosition = 0x01, SendClaimStatus = 0x02, SendGeneral = 0x04, SendName = 0x08, SendModel = 0x10,
                          SendDespawn = 0x20, UpdateAllChar = 0x1F;

        // 0x00D GP_SERV_CHAR_PC as CCharUpdatePacket(PChar, ENTITY_SPAWN, 0) builds it: every section sent
        public static BasicPacket CharPcSpawn(CharData c, ZoneInfo z) => CharPc(c, z, EntityUpdate.Spawn, 0);

        // CCharUpdatePacket
        public static BasicPacket CharPc(CharData c, ZoneInfo z, EntityUpdate type, byte updatemask)
        {
            var w = new PacketWriter(0x00D, 0);
            w.U32(4, c.Id);
            CharPcUpdateWith(w.Packet, c, z, type, updatemask);
            return w.Packet;
        }

        // CCharUpdatePacket::updateWith: the sections SendFlg names are (re)written onto what the packet already holds,
        // and the size follows the last section present (position 64, general 72, model 92, then the name)
        public static void CharPcUpdateWith(BasicPacket packet, CharData c, ZoneInfo z, EntityUpdate type, byte updatemask)
        {
            if (System.BitConverter.ToUInt32(packet.Buffer, 4) != c.Id) return;
            var w = new PacketWriter(packet);
            var b = packet.Buffer;
            w.SetSize(64);
            w.U16(8, c.TargId);
            if (type == EntityUpdate.Spawn) b[10] |= UpdateAllChar;
            else if (type == EntityUpdate.Despawn) b[10] |= SendDespawn;
            else b[10] |= updatemask;
            byte flg = b[10];

            if ((flg & SendPosition) != 0)
            {
                w.U8(11, c.Rotation);
                w.F32(12, c.X);
                w.F32(16, c.Y);   // packet z is internal y
                w.F32(20, c.Z);
                w.U8(28, MountUtils.UpdateSpeed(c));
                w.U8(29, c.AnimationSpeed);
                // Flags0: MovTime = moving, GroundFlag = wallhack, facetarget = m_TargID
                w.U32(24, (uint)(c.Moving & 0x1FFF) | (c.WallhackEnabled ? 1u << 15 : 0) | (uint)c.FaceTarget << 17);
            }
            if ((flg & SendClaimStatus) != 0) w.U32(44, 0); // BtTargetID
            if ((flg & SendGeneral) != 0)
            {
                WriteCharPcGeneral(w, c, z);
                w.SetSize(72);
            }
            if ((flg & SendModel) != 0)
            {
                Grap(w, 72, c, c.Look.ModelId);
                if (c.Costume2 != 0)
                {
                    w.U16(72, c.Look.Race << 8 | c.Costume2);
                    w.U16(88, 0xFFFF);
                }
                if (c.Monstrosity is { } mon)
                {
                    // MonstrosityFlags (0xC000 would hide the species name, a client quirk), the name ids, the model
                    w.U16(62, 0x8000 | Monstrosity.DisplaySpecies(mon));
                    w.U8(64, mon.NamePrefix1);
                    w.U8(65, mon.NamePrefix2);
                    w.U16(72, mon.Look);
                    w.U16(88, 0xFFFF);
                }
                w.SetSize(92);
            }
            if ((flg & SendName) != 0)
            {
                w.Name(90, c.Name, 16);
                // name_size + strlen + 4, for the client's habit of clipping a name whose terminator sits on the edge
                int nameLen = System.Math.Min(c.Name.Length, 16);
                w.SetSize((uint)((90 + nameLen + 4 + 3) & ~3));
            }
            // Flags6.MountIndex = m_mountId, almost always set: a char that dismounted out of range would be invisible
            if ((flg & (SendDespawn | SendGeneral)) != 0) w.U32(68, System.BitConverter.ToUInt32(b, 68) & ~0xFF0u | (uint)c.MountId << 4);
            if (type != EntityUpdate.Despawn)
            {
                w.U16(48, c.Costume); // CostumeId
                w.U16(60, c.PPet?.TargId ?? 0); // PetActIndex
                w.U8(51, 0);  // Flags4: TrialFlag, JobMasterFlag (needs SUPERIOR_LEVEL 5)
            }
        }

        // a hidden GM, or an invisible char, shows as invisible; a hidden GM, or a sneaking char, as silent
        private static bool InvisFlag(CharData c) => c.GmHidden || c.StatusEffects.HasStatusEffectByFlag(EffectFlag.Invisible);
        private static bool SilenceFlag(CharData c) => c.GmHidden || c.StatusEffects.HasStatusEffect(EffectId.Sneak);

        private static void WriteCharPcGeneral(PacketWriter w, CharData c, ZoneInfo z)
        {
            w.U8(30, c.Hpp);
            w.U8(31, c.Animation);

            var (chocoboIndex, custom0, custom1) = MountUtils.PacketDefinition(c);
            uint flags1 = 0;
            if (z.CanUseMisc("treasure")) flags1 |= 0x8;             // unknown_0_3: zone-wide treasure pool
            flags1 |= (uint)(chocoboIndex & 7) << 5;                  // ChocoboIndex: every mount needs a valid one
            flags1 |= (uint)(c.Look.Size & 3) << 9;                   // GraphSize
            if (c.InviteFlg) flags1 |= 0x800;                          // LfgFlag
            if (c.AnonymityFlg) flags1 |= 0x1000;                      // AnonymousFlag
            if (c.AwayFlg) flags1 |= 0x4000;                           // AwayFlag
            flags1 |= (uint)c.Gender << 15;                            // Gender
            var ls = LinkshellItem.Equipped(c, LinkshellItem.SlotLink1);
            if (ls != null) flags1 |= 0x20000;                         // LinkShellFlag
            if (c.IsLinkDead) flags1 |= 0x40000;                       // LinkDeadFlag
            flags1 |= (uint)(c.GmLevel & 7) << 24;                     // GmLevel
            if (c.WallhackEnabled) flags1 |= 1u << 27;                 // HackMove
            if (InvisFlag(c)) flags1 |= 1u << 29;                      // InvisFlag
            if (c.HasBazaar) flags1 |= 0x80000000;                     // BazaarFlag
            w.U32(32, flags1);
            if (ls != null)                                            // Flags2: linkshell colour
            {
                var (r, g, b) = LinkshellItem.DisplayColor(ls);
                w.U8(36, r);
                w.U8(37, g);
                w.U8(38, b);
            }

            uint flags3 = 0;
            if (c.RecruitFlg) flags3 |= 0x2;                           // LfgMasterFlag
            if (c.PriorityRender) flags3 |= 0x20;                      // CliPriorityFlag
            // BallistaTeam: retail puts a gladiator on team 0 in the Belligerency zones, against the Monipulators' team 1
            flags3 |= (Monstrosity.IsGladiatorInBelligerency(c) ? 0x08u : (uint)c.Allegiance) << 8;
            // Sword & Shield icon only shows outside of the Feretory
            if (c.Monstrosity is { Belligerency: true } && c.Zone != Monstrosity.ZoneFeretory) flags3 |= 0x08u << 8;
            if (SilenceFlag(c)) flags3 |= 1u << 21;                    // SilenceFlag
            if (!c.NewAdventurerOffFlg) flags3 |= 0x800000;            // NewCharacterFlag
            if (c.MentorFlg) flags3 |= 0x1000000;                      // MentorFlag
            w.U32(40, flags3);
            w.U32(52, custom0);                                        // CustomProperties: a personal chocobo's model,
            w.U32(56, custom1);                                        // 1 for the Noble Chocobo
            w.U8(66, 1 << 4);                                          // Flags5: GeoIndiSize = 1
            w.U8(67, c.ModelHitboxSize10);
            w.U32(68, 0);                                              // Flags6: GateId, MountIndex
        }

        // 0x04F GP_SERV_COMMAND_EQUIP_CLEAR
        public static BasicPacket EquipClear() => new PacketWriter(0x04F, 8).Packet;

        // 0x051 GP_SERV_COMMAND_GRAP_LIST
        public static BasicPacket GrapList(CharData c)
        {
            var w = new PacketWriter(0x051, 24);
            Grap(w, 4, c, c.Look.ModelId);
            if (c.Costume2 != 0)
            {
                w.U16(4, c.Costume2);
                w.U16(20, 0xFFFF);
            }
            if (c.Monstrosity != null)
            {
                w.U16(4, c.Monstrosity.Look);
                w.U16(20, 0xFFFF);
            }
            return w.Packet;
        }

        // 0x01C GP_SERV_COMMAND_ITEM_MAX: per container 1 + size, then 1 + usable size (0 disables -- the mog locker
        // without access)
        public static BasicPacket ItemMax(CharData c)
        {
            var w = new PacketWriter(0x01C, 100);
            for (int i = 0; i < CharData.MaxContainers; i++)
            {
                w.U8(4 + i, 1 + c.StorageSize[i]);
                bool disabled = i == (int)Container.MogLocker && !c.MogLockerAccess;
                w.U16(36 + 2 * i, disabled ? 0 : 1 + c.StorageSize[i]);
            }
            return w.Packet;
        }

        // GetMogHouseModelID: the 2F's style, else the region's (a nation's own city its own model). Null for a region
        // the C++ warns of and gives 0x0100.
        public static ushort? MogHouseModelId(CharData owner)
        {
            if ((owner.MhFlag & 0x40) != 0) return (ushort)(0x0267 + ((owner.MhFlag >> 7) & 0x03));
            switch (ZoneRegions.Of(owner.Zone))
            {
                case ZoneRegions.WestAhtUrhgan: return 214;
                case ZoneRegions.RonfaureFront: return 745;
                case ZoneRegions.GustabergFront: return 199;
                case ZoneRegions.SarutaFront: return 219;
                case ZoneRegions.Sandoria: return (ushort)(owner.Nation == 0 ? 0x0121 : 0x0101);
                case ZoneRegions.Bastok: return (ushort)(owner.Nation == 1 ? 0x0122 : 0x0102);
                case ZoneRegions.Windurst: return (ushort)(owner.Nation == 2 ? 0x0123 : 0x0120);
                case ZoneRegions.Jeuno: return 0x0100;
                case ZoneRegions.AdoulinIslands: return 0x0124;
                default: return null;
            }
        }

        // GetMogHouseLeavingFlag: which exit the mog house menu offers (Adoulin's always, else once its mhflag bit is set)
        public static byte MogHouseLeavingFlag(CharData c)
        {
            switch (ZoneRegions.Of(c.Zone))
            {
                case ZoneRegions.AdoulinIslands: return 9;
                case ZoneRegions.WestAhtUrhgan: return (byte)((c.MhFlag & 0x10) != 0 ? 5 : 0);
                case ZoneRegions.Sandoria: return (byte)((c.MhFlag & 0x01) != 0 ? 1 : 0);
                case ZoneRegions.Bastok: return (byte)((c.MhFlag & 0x02) != 0 ? 2 : 0);
                case ZoneRegions.Windurst: return (byte)((c.MhFlag & 0x04) != 0 ? 3 : 0);
                case ZoneRegions.Jeuno: return (byte)((c.MhFlag & 0x08) != 0 ? 4 : 0);
                default: return 0;
            }
        }

        public const ushort ZoneFeretory = 285;

        // 0x00A GP_SERV_COMMAND_LOGIN. mhOwner: the host of the mog house the char is in (the char itself when null);
        // ev: the char's currentEvent, a zone-in cutscene luautils::OnZoneIn left (none when null)
        public static BasicPacket Login(CharData c, ZoneInfo z, long unixNow, CharData mhOwner = null, EventInfo ev = null)
        {
            var w = new PacketWriter(0x00A, 260);

            // PosHead
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U8(11, c.Rotation);
            w.F32(12, c.X);
            w.F32(16, c.Y);       // z is internal y, not a typo
            w.F32(20, c.Z);
            w.U8(28, MountUtils.UpdateSpeed(c));
            w.U8(29, c.AnimationSpeed);
            w.U8(30, c.Hpp);      // HpMax
            w.U8(31, c.Animation);
            // flags2: the mount's sub power in byte 0, gender / size in byte 1
            w.U32(32, (uint)(c.Gender * 128 + (1 << c.Look.Size)) << 8 | (byte)(c.StatusEffects.GetStatusEffect(EffectId.Mounted)?.SubPower ?? 0));
            w.U32(40, 0x0100);    // flags4

            w.U32(48, z.Id);                                   // ZoneNo
            w.U32(56, (uint)unixNow);                          // ntTimeSec
            w.U32(60, VanadielTimestamp(unixNow));             // GameTime
            w.U16(66, z.Id);                                   // MapNumber
            Grap(w, 68, c, c.Look.ModelId);
            w.U16(86, z.MusicDay);
            w.U16(88, z.MusicNight);
            w.U16(90, z.BattleSolo);
            w.U16(92, z.BattleParty);
            w.U16(94, c.Animation == 5 ? 0x54 : 0xD4);         // Animation::Mount
            int csid = ev?.EventId ?? -1;
            if (csid != -1)
            {
                w.U16(64, ev.TextTable == -1 ? z.Id : (ushort)ev.TextTable); // EventNo
                w.U16(98, z.Id);                               // EventNum
                w.U16(100, (ushort)csid);                      // EventPara
                w.U16(102, (ushort)(ev.EventFlags & 0xFFFF));  // EventMode
                c.Animation = 4;                               // Animation::Event
                w.U8(31, c.Animation);                         // PosHead.server_status
            }
            w.U16(96, c.Boundary);                             // SubMapNumber
            w.U16(104, z.Weather);                             // WeatherNumber
            w.U32(108, z.WeatherChangeTime);                   // WeatherTime
            w.Name(132, c.Name, 16);
            w.U32(160, c.PlayTimeSec);
            w.U32(164, 60 * c.DeadRemainingSec);               // DeadCounter
            if (c.MoghouseId != 0)
            {
                var owner = mhOwner ?? c;
                w.U32(128, 1);                                 // LoginState = SAVE_LOGIN_STATE_MYROOM
                if ((owner.MhFlag & 0x0040) != 0) w.U8(168, 2); // MyroomSubMapNumber: on the 2F, the full exit menu
                w.U16(170, MogHouseModelId(owner) ?? 0x0100);  // MyroomMapNumber
            }
            else
            {
                w.U32(128, 2);                                 // LoginState = SAVE_LOGIN_STATE_GAME
                w.U16(170, 0x01FF);                            // MyroomMapNumber
                w.U16(172, csid > 0 ? 1 : 0);                  // SendCount
                w.U8(175, z.CanUseMisc("mogmenu") ? 1 : 0);    // MogZoneFlag
            }
            w.U8(174, MogHouseLeavingFlag(c));                 // MyRoomExitBit
            if (z.Id == ZoneFeretory)
            {
                // disables the zone model (and abilities)
                w.U32(128, 1);
                w.U16(170, 0x02D9);
            }
            if (c.Monstrosity != null)
            {
                w.U16(68, c.Monstrosity.Look);  // GrapIDTbl[0]
                w.U16(84, 0xFFFF);              // GrapIDTbl[8]
                w.U16(126, 0x1F);               // IsMonstrosity
            }

            // Dancer
            w.U8(180, c.MJob);
            w.U8(183, c.SJob);
            for (int i = 0; i < 7; i++) w.U16(204 + 2 * i, c.BaseStats[i]); // bp_base
            w.I32(232, c.MaxHp);
            w.I32(236, c.MaxMp);
            w.U8(240, (int)(c.UnlockedJobs & 1));              // sjobflg

            w.U32(244, c.Settings);                            // ConfData
            w.U32(248, c.ChatFilters1);
            w.U32(252, c.ChatFilters2);
            w.U8(256, 1);                                      // Ex
            return w.Packet;
        }

        // 0x050 GP_SERV_COMMAND_EQUIP_LIST
        public static BasicPacket EquipList(byte invSlot, byte equipSlot, Container container)
        {
            var w = new PacketWriter(0x050, 8);
            w.U8(4, invSlot);
            w.U8(5, equipSlot);
            w.U8(6, (byte)container);
            return w.Packet;
        }

        // 0x01F GP_SERV_COMMAND_ITEM_LIST; lockFlg 5 = equipped
        public static BasicPacket ItemList(Item item, byte lockFlg)
        {
            var w = new PacketWriter(0x01F, 16);
            w.U32(4, item.Quantity);
            w.U16(8, item.ItemId);
            w.U8(10, (byte)item.Location);
            w.U8(11, item.Slot);
            w.U8(12, lockFlg);
            return w.Packet;
        }

        // 0x0AA GP_SERV_COMMAND_MAGIC_DATA: the spell bitset
        public static BasicPacket MagicData(CharData c)
        {
            var w = new PacketWriter(0x0AA, 132);
            w.Bytes(4, c.SpellList);
            // a Monipulator lists its Trusts, and only the spells its species can cast (monstrosity::ListsSpell)
            if (c.Monstrosity?.ListedSpells is { } listed)
                for (int i = 0; i < listed.Length && i < c.SpellList.Length; i++) w.U8(4 + i, c.SpellList[i] & listed[i]);
            return w.Packet;
        }

        // 0x0AC GP_SERV_COMMAND_COMMAND_DATA: weaponskills, job abilities, pet commands, traits
        public static BasicPacket CommandData(CharData c)
        {
            var w = new PacketWriter(0x0AC, 228);
            w.Bytes(4, c.WeaponSkills);
            w.Bytes(68, c.Abilities);
            w.Bytes(132, c.PetCommands);
            w.Bytes(196, c.Traits);
            return w.Packet;
        }

        // 0x0DF GP_SERV_COMMAND_GROUP_ATTR for a character (jobs hidden while anonymous; a Monipulator's species and
        // the 16-bit half of its packed name)
        public static BasicPacket GroupAttr(CharData c)
        {
            var w = new PacketWriter(0x0DF, 40);
            w.U32(4, c.Id);
            w.I32(8, c.Hp);
            w.I32(12, c.Mp);
            w.I32(16, c.Tp);
            w.U16(20, c.TargId);
            w.U8(22, c.Hpp);
            w.U8(23, c.Mpp);
            if (c.Monstrosity is { } mon)
            {
                w.U16(28, 0x8000 | Monstrosity.DisplaySpecies(mon));
                w.U16(30, (ushort)Monstrosity.PackedName(mon));
            }
            if (!c.AnonymityFlg)
            {
                w.U8(32, c.MJob);
                w.U8(33, c.MLvl);
                w.U8(34, c.SJob);
                w.U8(35, c.SLvl);
            }
            return w.Packet;
        }

        // 0x037 GP_SERV_SERVERSTATUS (CCharStatusPacket)
        public static BasicPacket ServerStatus(CharData c, ZoneInfo z, long unixNow)
        {
            var w = new PacketWriter(0x037, 96);
            w.Bytes(4, c.StatusEffects.Icons);                         // BufStatus
            w.U32(36, c.Id);
            w.U8(48, c.Animation);

            uint flags0 = 0;
            if (c.InviteFlg) flags0 |= 0x10;                           // LfgFlag
            if (c.AnonymityFlg) flags0 |= 0x20;                        // AnonymousFlag
            if (c.AwayFlg) flags0 |= 0x80;                             // AwayFlag
            flags0 |= (uint)c.Gender << 8;
            if (z.CanUseMisc("treasure")) flags0 |= 0x200;
            flags0 |= (uint)(c.Look.Size & 3) << 11;                   // GraphSize
            var mounted = c.StatusEffects.GetStatusEffect(EffectId.Mounted);
            if (mounted != null)
            {
                flags0 |= (uint)(MountUtils.PacketDefinition(c).ChocoboIndex & 7) << 13; // Chocobo_Index
                w.U8(91, (byte)mounted.Power);                         // mount_id
            }
            flags0 |= (uint)c.Hpp << 16;
            var ls = LinkshellItem.Equipped(c, LinkshellItem.SlotLink1);
            if (ls != null) flags0 |= 1u << 25;                        // LinkShellFlag
            if (c.IsLinkDead) flags0 |= 1u << 26;                      // LinkDeadFlag
            flags0 |= (uint)(c.GmLevel & 7) << 29;
            w.U32(40, flags0);
            if (ls != null)
            {
                var (r, g, b) = LinkshellItem.DisplayColor(ls);
                w.U8(49, r);
                w.U8(50, g);
                w.U8(51, b);
            }

            // Flags1: Speed, Hackmove, FreezeFlag, InvisFlag, SpeedBase, BazaarFlag
            w.U32(44, (uint)(MountUtils.UpdateSpeed(c) & 0xFFF) | (c.WallhackEnabled ? 1u << 12 : 0) | (c.FrozenFlagged ? 1u << 13 : 0) |
                      (InvisFlag(c) ? 1u << 15 : 0) | (uint)c.AnimationSpeed << 17 | (c.HasBazaar ? 1u << 29 : 0));
            // Flags2: PetIndex, CliPriorityFlag, BallistaFlg (a flagged Monipulator's Sword & Shield icon outside the
            // Feretory)
            bool belligerent = c.Monstrosity is { Belligerency: true } && z.Id != Monstrosity.ZoneFeretory;
            // a gladiator in a Belligerency zone: team 0 (BallistaFlg 0x08)
            uint ballista = Monstrosity.IsGladiatorInBelligerency(c) ? 0x08u : (uint)c.Allegiance;
            w.U32(52, (uint)(c.PPet?.TargId ?? 0) << 3 | (c.PriorityRender ? 1u << 20 : 0) | ballista << 21 | (belligerent ? 0x08u << 21 : 0));

            uint flags3 = 0;
            if (c.RecruitFlg) flags3 |= 0x1;                           // LfgMasterFlag
            if (SilenceFlag(c)) flags3 |= 0x4;                         // SilenceFlag
            if (!c.NewAdventurerOffFlg) flags3 |= 0x8;                 // NewCharacterFlag
            if (c.MentorFlg) flags3 |= 0x10;                           // MentorFlag
            flags3 |= (uint)c.Allegiance << 8;                  // BallistaTeam
            w.U32(56, flags3);

            w.U32(60, 60 * c.DeadRemainingSec);                        // dead_counter1
            w.U32(64, VanadielTimestamp(unixNow) + c.DeadRemainingSec); // dead_counter2
            w.U16(68, c.Costume);                                      // costume_id
            if (c.Animation == Anim.NewFishingStart) w.U8(74, c.HookDelay); // fishing_timer
            w.U64(76, c.StatusEffects.Bits);                           // BufStatusBits
            if (c.Monstrosity is { } mon)
            {
                w.U16(84, 0x8000 | Monstrosity.DisplaySpecies(mon));   // monstrosity_info
                w.U8(86, mon.NamePrefix1);
                w.U8(87, mon.NamePrefix2);
                w.U8(90, mon.Rank & 3);                                // Flags5: MonstrosityRank
            }
            w.U8(88, 1 << 4);                                          // Flags4: GeoIndiSize = 1
            w.U8(89, c.ModelHitboxSize10);
            w.U8(92, 0x7B);                                            // mog wardrobe enable bits
            return w.Packet;
        }

        // What the C++ map pushes when a client's 0x00A login lands (in the order the reference capture shows):
        // the char spawn, equipment reset, look, container sizes, the login itself, then each equipped slot; the
        // inventory sync re-sends each slot with the look, then each equipped item, the spell list and command lists.
        public static List<BasicPacket> OnLogin(CharData c, ZoneInfo z, long unixNow, CharData mhOwner = null, EventInfo ev = null)
        {
            var list = new List<BasicPacket> { CharPcSpawn(c, z), EquipClear(), GrapList(c), ItemMax(c), Login(c, z, unixNow, mhOwner, ev) };
            var equip = c.Equip.FindAll(e => e.EquipSlot < 16); // the linkshells go out with the inventory
            foreach (var e in equip) list.Add(EquipList(e.InvSlot, e.EquipSlot, e.Container));
            foreach (var e in equip)
            {
                list.Add(EquipList(e.InvSlot, e.EquipSlot, e.Container));
                list.Add(GrapList(c));
            }
            foreach (var e in equip) list.Add(ItemList(c.FindItem(e.Container, e.InvSlot), 5));
            list.Add(MagicData(c));
            list.Add(CommandData(c));
            return list;
        }

        // The reply to the client's 0x011 (zone loaded)
        public static List<BasicPacket> OnZoneLoaded(CharData c, ZoneInfo z, long unixNow) =>
            new List<BasicPacket> { GroupAttr(c), ServerStatus(c, z, unixNow) };
    }
}
