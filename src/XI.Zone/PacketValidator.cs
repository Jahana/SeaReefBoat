// Port of src/map/packets/c2s/validation.{h,cpp}: the checks a C2S packet's validate() chains. Every check is a
// no-op once one has failed, so the first error is the one reported (blockedBy stops at its first hit too).

using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // xi::Animation (scripts/enum/animation.codegen.lua), the values the server branches on
    public static class Anim
    {
        public const byte None = 0, Attack = 1, Death = 3, Event = 4, Chocobo = 5, Healing = 33, Synth = 44, Sit = 47,
            FishingStart = 50, NewFishingStart = 56, NewFishingFish = 57, NewFishingCaught = 58, NewFishingRodBreak = 59,
            NewFishingLineBreak = 60, NewFishingMonster = 61, NewFishingStop = 62, Sitchair0 = 63, Sitchair10 = 73,
            Mount = 85;
    }

    // enums/blocked_state.h
    [Flags]
    public enum BlockedState : ushort
    {
        Jailed = 0x0001,
        Dead = 0x0002,
        Crafting = 0x0004,
        Healing = 0x0008,
        Fishing = 0x0010,
        Sitting = 0x0020,
        Mounted = 0x0040,
        Charmed = 0x0080,
        PreventAction = 0x0100,
        InEvent = 0x0200,
        Engaged = 0x0400,
        AbnormalStatus = 0x0800,
        Monstrosity = 0x1000,
        Gestation = 0x2000,
    }

    // xi::MogHouse
    public enum MogHouseKind { Any, Own, Visiting }

    public sealed class PacketValidator
    {
        private const ushort MordionGaol = 131;

        private readonly CharData c_;
        private readonly List<string> errors_ = new List<string>();

        public PacketValidator(CharData c) => c_ = c;

        public bool Valid => errors_.Count == 0;
        public string ErrorString => string.Join(", ", errors_);

        public PacketValidator AddError(string error)
        {
            errors_.Add(error);
            return this;
        }

        public PacketValidator MustEqual(long left, long right, string errMsg)
        {
            if (Valid && left != right) errors_.Add(errMsg);
            return this;
        }

        public PacketValidator MustNotEqual(long left, long right, string errMsg)
        {
            if (Valid && left == right) errors_.Add(errMsg);
            return this;
        }

        // inclusive
        public PacketValidator Range(string fieldName, long value, long min, long max)
        {
            if (Valid && (value < min || value > max)) errors_.Add($"{fieldName} out of range: {value} not in [{min}, {max}]");
            return this;
        }

        public PacketValidator MultipleOf(string fieldName, long value, long divisor)
        {
            if (Valid && value % divisor != 0) errors_.Add($"{fieldName} is not a multiple of {divisor}.");
            return this;
        }

        public PacketValidator OneOf(string fieldName, long value, params long[] allowed)
        {
            if (Valid && Array.IndexOf(allowed, value) < 0) errors_.Add($"{fieldName} value {value} is not allowed.");
            return this;
        }

        // oneOf<E>(underlying): the value must name a member of the enum
        public PacketValidator OneOf<E>(long value) where E : struct, Enum
        {
            if (!Valid) return this;
            foreach (var v in Enum.GetValues<E>())
                if (Convert.ToInt64(v) == value) return this;
            errors_.Add($"{value} not a valid {typeof(E).Name} value.");
            return this;
        }

        public PacketValidator BlockedBy(BlockedState states)
        {
            if (!Valid) return this;
            byte a = c_.Animation;
            // AbnormalStatus has no state here yet, so it never blocks
            Check(BlockedState.Jailed, c_.AccountGmLevel == 0 && c_.Zone == MordionGaol);
            Check(BlockedState.Engaged, a == Anim.Attack);
            Check(BlockedState.Dead, c_.Hp <= 0);
            Check(BlockedState.Crafting, a == Anim.Synth || c_.Synth != null);
            Check(BlockedState.Fishing, (a >= Anim.NewFishingFish && a <= Anim.NewFishingStop) || a == Anim.FishingStart || a == Anim.NewFishingStart);
            Check(BlockedState.Sitting, a == Anim.Sit || (a >= Anim.Sitchair0 && a <= Anim.Sitchair10));
            Check(BlockedState.Mounted, a == Anim.Chocobo || a == Anim.Mount);
            Check(BlockedState.InEvent, c_.IsInEvent);
            var effects = c_.StatusEffects;
            Check(BlockedState.Monstrosity, c_.Monstrosity != null);
            Check(BlockedState.Gestation, effects.HasStatusEffect(EffectId.Gestation));
            Check(BlockedState.Healing, effects.HasStatusEffect(EffectId.Healing) || a == Anim.Healing);
            Check(BlockedState.Charmed, effects.HasStatusEffect(EffectId.CharmI, EffectId.CharmII));
            Check(BlockedState.PreventAction, effects.HasPreventActionEffect());
            return this;

            void Check(BlockedState flag, bool condition)
            {
                if (Valid && (states & flag) != 0 && condition) errors_.Add($"Invalid state: {flag}");
            }
        }

        public PacketValidator IsInEvent(int? eventId = null)
        {
            if (!Valid) return this;
            if (!c_.IsInEvent) errors_.Add("Not in an event.");
            else if (eventId.HasValue && c_.CurrentEvent.EventId != eventId.Value)
                errors_.Add($"Event ID mismatch {c_.CurrentEvent.EventId} != {eventId.Value}.");
            return this;
        }

        // isEngaged: PAI->IsEngaged(), the attack animation
        public PacketValidator IsEngaged()
        {
            if (!Valid) return this;
            if (c_.Animation != Anim.Attack) errors_.Add("Character is not engaged.");
            return this;
        }

        // getStorage resolves every id below MAX_CONTAINER_ID
        public PacketValidator IsValidContainer(string fieldName, uint containerId)
        {
            if (Valid && containerId >= CharData.MaxContainers) errors_.Add($"{fieldName} value {containerId} is not a valid container.");
            return this;
        }

        // isInMogHouse: CCharEntity::inMogHouse(kind)
        public PacketValidator IsInMogHouse(MogHouseKind kind)
        {
            bool inside = kind switch
            {
                MogHouseKind.Own => c_.MoghouseId == c_.Id,
                MogHouseKind.Visiting => c_.MoghouseId != 0 && c_.MoghouseId != c_.Id,
                _ => c_.MoghouseId != 0,
            };
            if (Valid && !inside) errors_.Add("Character is not in Mog House.");
            return this;
        }

        public PacketValidator HasKeyItem(ushort keyItemId)
        {
            if (Valid && !c_.HasKeyItem(keyItemId)) errors_.Add($"Missing Key Item {keyItemId}.");
            return this;
        }

        public PacketValidator RequiresPriorPacket(ushort expectedPacketId)
        {
            if (Valid && c_.LastPacketType != expectedPacketId)
                errors_.Add($"Expected prior packet 0x{expectedPacketId:x3}, got 0x{c_.LastPacketType:x3}.");
            return this;
        }

        public PacketValidator IsPartyLeader()
        {
            if (!Valid) return this;
            if (c_.Party == null) errors_.Add("Not in a party.");
            else if (c_.Party.Leader != c_) errors_.Add("Not the party leader.");
            return this;
        }

        public PacketValidator IsAllianceLeader()
        {
            if (!Valid) return this;
            if (c_.Party == null) errors_.Add("Not in a party.");
            else if (c_.Party.Alliance == null) errors_.Add("Not in an alliance.");
            else if (c_.Party.Alliance.MainParty == null) errors_.Add("No alliance main party.");
            else if (c_.Party.Alliance.MainParty.Leader != c_) errors_.Add("Not the alliance leader.");
            return this;
        }

        // the linkshell item in SLOT_LINK1/2 is at least `rank` (LSTYPE: the shell 1 outranks a sack 2, a sack a pearl 3)
        public PacketValidator HasLinkshellRank(byte slot, byte rank)
        {
            if (!Valid) return this;
            if (slot != 1 && slot != 2)
            {
                errors_.Add("Invalid linkshell slot.");
                return this;
            }
            var item = LinkshellItem.Equipped(c_, slot == 1 ? LinkshellItem.SlotLink1 : LinkshellItem.SlotLink2);
            if (item == null)
            {
                errors_.Add("Invalid linkshell item.");
                return this;
            }
            byte actual = LinkshellItem.Type(item);
            bool matching = rank switch
            {
                LinkshellItem.TypeLinkshell => actual == LinkshellItem.TypeLinkshell,
                LinkshellItem.TypePearlsack => actual == LinkshellItem.TypeLinkshell || actual == LinkshellItem.TypePearlsack,
                LinkshellItem.TypeLinkpearl => actual == LinkshellItem.TypeLinkshell || actual == LinkshellItem.TypePearlsack ||
                                               actual == LinkshellItem.TypeLinkpearl,
                _ => false,
            };
            if (!matching) errors_.Add("Invalid linkshell rank.");
            return this;
        }

        public PacketValidator Custom(Action<PacketValidator> check)
        {
            if (Valid) check(this);
            return this;
        }
    }
}
