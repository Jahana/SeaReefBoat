using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // c2s/0x0DD EQUIP_INSPECT (/check, /checkname, /checkparam) and 0x0DE INSPECT_MESSAGE (the bazaar message)
    public sealed partial class ZoneHost
    {
        private enum EquipInspectKind : byte { Check = 0, CheckName = 1, CheckParam = 2 }

        private const ushort MsgBasicCheckparamPrimary = 712, MsgBasicCheckparamAuxiliary = 713, MsgBasicCheckparamRange = 714,
            MsgBasicCheckparamDefense = 715, MsgBasicCheckparamIlvl = 731, MsgBasicCheckparamName = 733;
        private const float CheckPcDistance = 50.0f; // /check on a PC has a 50 yalm range
        private const ushort MsgBasicCheckDefault = 174, MsgBasicCheckImpossibleToGauge = 249;

        // the monster /check: the level and the difficulty (64 is Too Weak), its message moved by 1 for a defence above
        // the attack (or at 1.25x under it) and by 3 for an evasion over the accuracy by 30 (or 10 under it)
        private BasicPacket CheckAsMonster(CharData c, BattleEntity target)
        {
            var stats = loader_.Stats;
            int mobLvl = target.MLvl + target.GetMod(Mod.EXP_LVL_MOD);
            int messageValue = 64 + CheckMob(c.MLvl, target);
            int charAcc = stats?.Acc(c, 0, 0) ?? 0, charAtt = stats?.AttMain(c) ?? 0;
            int mobEva = Melee.Eva(target), mobDef = MeleeFor().Def(target);
            int messageId = MsgBasicCheckDefault;
            if (mobDef > charAtt) messageId -= 1;
            else if (mobDef * 1.25 <= charAtt) messageId += 1;
            if (mobEva - 30 > charAcc) messageId -= 3;
            else if (mobEva + 10 <= charAcc) messageId += 3;
            return SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, mobLvl, messageValue, (ushort)messageId);
        }
        private const int InspectMessageLength = 120; // sInspectMessage holds 123 bytes, the message is 120

        private void RegisterInspectHandlers()
        {
            Register(0x0DD, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf<EquipInspectKind>(p.Buffer[12]),
                OnEquipInspect);
            Register(0x0DE, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), OnInspectMessage);
        }

        // GP_CLI_COMMAND_EQUIP_INSPECT: UniqueNo@4, ActIndex u32@8, Kind@12, on a mob or a PC (GetEntity(TYPE_MOB |
        // TYPE_PC)). A mob, or a Monipulator, is checked like a monster, an NM's or a battlefield mob's strength (or one
        // with CHECK_AS_NM) impossible to gauge; a mob past 50 yalms is still checked, only logged. A Monipulator's
        // /checkname is answered with the possession messages; /checkparam covers the char or its pet.
        private void OnEquipInspect(MapSession s, CharData c, BasicPacket p)
        {
            if (InPrison(c))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }

            uint uniqueNo = U32(p, 4);
            ushort actIndex = (ushort)U32(p, 8);
            var target = CharByTargId(c, actIndex);
            var mob = target == null ? MobByTargId(c, actIndex) : null;
            if (target != null && Distance(c, target) > CheckPcDistance)
            {
                log_($"GP_CLI_COMMAND_EQUIP_INSPECT: {c.Name} attempting to check PC {target.Name} from too far away");
                return;
            }
            // the limit doesn't apply to monsters, but it is still logged
            if (mob != null && Distance(c, mob) > CheckPcDistance)
                log_($"GP_CLI_COMMAND_EQUIP_INSPECT: {c.Name} checking mob {mob.Name} from too far away");

            switch ((EquipInspectKind)p.Buffer[12])
            {
                case EquipInspectKind.Check:
                    if (mob != null)
                    {
                        if (mob.Id != uniqueNo) return;
                        if ((mob.Type & (MobType.Notorious | MobType.Battlefield)) != 0 || mob.GetMobMod(MobMod.CheckAsNm) > 0)
                            Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, mob.Id, mob.TargId, 0, 0, MsgBasicCheckImpossibleToGauge));
                        else
                            Push(s, CheckAsMonster(c, mob));
                        break;
                    }
                    if (target == null || target.Id != uniqueNo) return;
                    // retail answers a /check on a Monipulator like one on a monster
                    if (target.Monstrosity != null)
                    {
                        PushTo(target, MessageFor(c, 0, 0, MsgStdExamine));
                        Push(s, CheckAsMonster(c, target));
                        break;
                    }
                    // a hidden GM checks unseen, unless the target's GM level is as high
                    if (!c.GmHidden || target.AccountGmLevel >= c.AccountGmLevel) PushTo(target, MessageFor(c, 0, 0, MsgStdExamine));
                    Push(s, GameOkPackets.InspectMessage(c, target));
                    Push(s, EquipInspectEquipment(target));
                    Push(s, EquipInspectGeneral(c, target));
                    break;

                case EquipInspectKind.CheckName:
                    if (target?.Monstrosity != null)
                    {
                        Push(s, MessageFor(target, 0, 0, MsgStdMonstrosityCheckOut));
                        PushTo(target, MessageFor(c, 0, 0, MsgStdMonstrosityCheckIn));
                    }
                    break;

                case EquipInspectKind.CheckParam:
                    if (c.Id != uniqueNo)
                    {
                        if (c.PPet is BattleEntity pet && pet.Id == uniqueNo) CheckParamPet(s, c, pet);
                        return;
                    }
                    var stats = loader_.Stats;
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicCheckparamName));
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicCheckparamIlvl));
                    Push(s, SelfPackets.BattleMessage(c, stats.Acc(c, 0, 0), stats.Att(c, CharStats.SlotMain), MsgBasicCheckparamPrimary));
                    if (c.Equipped[CharStats.SlotSub]?.Weapon != null)
                        Push(s, SelfPackets.BattleMessage(c, stats.Acc(c, 1, 0), stats.Att(c, CharStats.SlotSub), MsgBasicCheckparamAuxiliary));
                    else
                        Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicCheckparamAuxiliary));
                    if (c.Equipped[CharStats.SlotRanged]?.Weapon != null || c.Equipped[CharStats.SlotAmmo]?.Weapon != null)
                        Push(s, SelfPackets.BattleMessage(c, stats.Racc(c), stats.Ratt(c), MsgBasicCheckparamRange));
                    else
                        Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicCheckparamRange));
                    Push(s, SelfPackets.BattleMessage(c, CharStats.Eva(c), stats.Def(c), MsgBasicCheckparamDefense));
                    break;
            }
        }

        // /checkparam on the pet: its name, its melee pair (the auxiliary line with its second attack's accuracy when the
        // master holds a sub weapon), its ranged pair when the master holds a ranged weapon or ammo (RATT is handed the
        // weapon's skill id as its bonus, as the C++ does) and its evasion and defence
        private void CheckParamPet(MapSession s, CharData c, BattleEntity pet)
        {
            BasicPacket Msg(int param, int value, ushort id) => SelfPackets.BattleMessage(c.Id, c.TargId, pet.Id, pet.TargId, param, value, id);
            var melee = MeleeFor();
            Push(s, Msg(0, 0, MsgBasicCheckparamName));
            Push(s, Msg(melee.Acc(pet, 0), melee.Att(pet, CharStats.SlotMain), MsgBasicCheckparamPrimary));
            if (c.Equipped[CharStats.SlotSub]?.Weapon != null)
                Push(s, Msg(melee.Acc(pet, 1), melee.Att(pet, CharStats.SlotMain), MsgBasicCheckparamAuxiliary));
            else
                Push(s, Msg(0, 0, MsgBasicCheckparamAuxiliary));
            var rangedWeapon = c.Equipped[CharStats.SlotRanged]?.Weapon ?? c.Equipped[CharStats.SlotAmmo]?.Weapon;
            if (rangedWeapon != null)
                Push(s, Msg(Racc(pet), Ratt(pet, rangedWeapon.Skill), MsgBasicCheckparamRange));
            else
                Push(s, Msg(0, 0, MsgBasicCheckparamRange));
            Push(s, Msg(Melee.Eva(pet), melee.Def(pet), MsgBasicCheckparamDefense));
        }

        // GP_CLI_COMMAND_INSPECT_MESSAGE: the message is stored unescaped, exactly as sent, for the bazaar to show
        private void OnInspectMessage(MapSession s, CharData c, BasicPacket p)
        {
            var message = UntrustedBytes(p, 4, InspectMessageLength);
            if (Exec("UPDATE char_stats SET bazaar_message = ? WHERE charid = ? LIMIT 1", BoundValue.Of(message), BoundValue.Of(c.Id)) != null)
            {
                if (DebugBazaarsOn)
                    log_($"Bazaar Interaction [Set Message] - Character: {c.Name}, " +
                         $"Message: '{XI.Ipc.CborRaw.FromBytes(message)}'");
                c.BazaarMessage = message;
            }
        }

        // a charged item's Attr (inspect, bazaar list): the flag, the remaining charges, whether it is still recharging, its
        // next use and when the use delay would end. The next use is the last use (exdata TimeValue1) plus the reuse delay;
        // with no last use that is the steady clock's epoch plus the delay, as C++'s default time_point
        private void ChargedAttr(Span<byte> data, Item item, EquipmentInfo info, uint vanaNow)
        {
            uint useTime = BitConverter.ToUInt32(item.Extra, 4);
            long nextUse = useTime != 0 ? (long)useTime + info.ReuseDelay : vanaNow - NowMs() / 1000 + info.ReuseDelay;
            data[0] = 0x01;
            data[1] = item.Extra[1];
            data[3] = (byte)(nextUse > vanaNow ? 0x90 : 0xD0);
            BitConverter.TryWriteBytes(data.Slice(4, 4), (uint)nextUse);
            BitConverter.TryWriteBytes(data.Slice(8, 4), info.UseDelay + vanaNow);
        }

        // 0x0C9 GP_SERV_COMMAND_EQUIP_INSPECT EQUIPMENT (OptionFlag 3): the target's equipment in slot order, 8 to a
        // packet; each full page goes out sized for 8, the last holds the rest (sized for at least one entry)
        private List<BasicPacket> EquipInspectEquipment(CharData target)
        {
            const int EquipAt = 12, EntrySize = 28, PerPage = 8;
            var pages = new List<BasicPacket>();
            uint vanaNow = LoginPackets.VanadielTimestamp(UnixNow());
            byte[] page = new byte[EquipAt + EntrySize * PerPage];
            int count = 0;

            BasicPacket Page(int entries)
            {
                var w = new PacketWriter(0x0C9, (uint)(EquipAt + EntrySize * Math.Max(entries, 1)));
                w.U32(4, target.Id);
                w.U16(8, target.TargId);
                w.U8(10, 0x03);
                w.U8(11, (byte)entries);
                w.Bytes(EquipAt, page.AsSpan(EquipAt, EntrySize * Math.Max(entries, 1)));
                return w.Packet;
            }

            for (int slot = 0; slot < 16; slot++)
            {
                var item = CharStats.EquippedItem(target, slot);
                if (item == null) continue;
                var info = target.Equipped[slot];
                int at = EquipAt + count * EntrySize;
                BitConverter.TryWriteBytes(page.AsSpan(at, 2), item.ItemId);
                page[at + 2] = (byte)slot;
                var data = page.AsSpan(at + 4, 24);

                if (info?.Charged == true) ChargedAttr(data, item, info, vanaNow);
                if (item.Augmented)
                {
                    data[0] = 0x02;
                    item.Extra.AsSpan(2, 8).CopyTo(data.Slice(2, 8)); // the four augments
                }
                // every equipment exdata kind keeps the signature at 0x0C
                item.Extra.AsSpan(12, 12).CopyTo(data.Slice(12, 12));

                if (++count == PerPage)
                {
                    pages.Add(Page(count));
                    Array.Clear(page);
                    count = 0;
                }
            }
            pages.Add(Page(count));
            return pages;
        }

        // 0x0C9 GP_SERV_COMMAND_EQUIP_INSPECT GENERAL (OptionFlag 1): jobs and levels unless the target is anonymous (a
        // visible GM level 3+ sees them anyway), and the LS1 item, name and colour. The master level and its flags, and
        // the Ballista info, are 0 as in C++ (its TODOs: there are no master levels to send).
        private static BasicPacket EquipInspectGeneral(CharData checker, CharData target)
        {
            var w = new PacketWriter(0x0C9, 0x54);
            w.U32(4, target.Id);
            w.U16(8, target.TargId);
            w.U8(10, 0x01);
            if (LinkshellItem.Equipped(target, LinkshellItem.SlotLink1) is Item ls)
            {
                w.U16(14, ls.ItemId);
                w.Bytes(16, ls.Extra.AsSpan(9, 15));
                w.U16(32, LinkshellItem.Color(ls));
            }
            if (checker.GmLevel >= 3 || !target.AnonymityFlg)
            {
                w.U8(34, target.MJob);
                w.U8(35, target.SJob);
                w.U8(36, target.MLvl);
                w.U8(37, target.SLvl);
                w.U8(38, target.MJob); // mjob is sent even if the job is not mastered
                w.U8(39, 0);           // mlvl: the master job level
                w.U8(40, 0);           // mflags: 0x01 leveling, 0x02 capped
            }
            return w.Packet;
        }
    }
}
