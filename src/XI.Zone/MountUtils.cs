using System;
using System.Buffers.Binary;

namespace XI.Zone
{
    // mountutils and CCharEntity::m_chocoboUserData. The user data is ChocoboUserData_t laid flat (32 bytes, saved raw
    // in char_pet.chocobo_user_data): fieldChocobo (the registered chocobo's ChocoboCustomProperties, 0 for none) at 0,
    // flags at 4, chocobosRaised at 8, the registered abilities / strength / endurance / discernment / receptivity /
    // weather at 10..16, silksSpeedBonus at 17. ChocoboCustomProperties packs largeBeak (bit 0), largeTalons (3),
    // fullTail (6), color (9..11), speed (12..18) and minutes (19..24).
    public static class MountUtils
    {
        public const byte MountChocobo = 0, MountNobleChocobo = 34;
        public const ushort PersonalChocoboFlag = 0x40; // the Mounted effect's sub power for a registered chocobo

        public const int OffFieldChocobo = 0, OffFlags = 4, OffChocobosRaised = 8, OffAbility1 = 10, OffAbility2 = 11,
            OffStrength = 12, OffEndurance = 13, OffDiscernment = 14, OffReceptivity = 15, OffWeather = 16, OffSilksSpeedBonus = 17,
            UserDataSize = 32;

        // map.BASE_SPEED, map.SPEED_LIMIT, map.MOUNT_SPEED (ZoneHost sets them from its settings)
        public static byte BaseSpeed = 50, SpeedLimit = 80, MountSpeed = 80;

        public static uint FieldChocobo(CharData c) => BinaryPrimitives.ReadUInt32LittleEndian(c.ChocoboUserData.AsSpan(OffFieldChocobo));
        public static void SetFieldChocobo(CharData c, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(c.ChocoboUserData.AsSpan(OffFieldChocobo), v);
        public static uint Flags(CharData c) => BinaryPrimitives.ReadUInt32LittleEndian(c.ChocoboUserData.AsSpan(OffFlags));
        public static void SetFlags(CharData c, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(c.ChocoboUserData.AsSpan(OffFlags), v);
        public static ushort ChocobosRaised(CharData c) => BinaryPrimitives.ReadUInt16LittleEndian(c.ChocoboUserData.AsSpan(OffChocobosRaised));
        public static void SetChocobosRaised(CharData c, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(c.ChocoboUserData.AsSpan(OffChocobosRaised), v);

        // ChocoboCustomProperties
        public static bool LargeBeak(uint p) => (p & 1) != 0;
        public static bool LargeTalons(uint p) => (p >> 3 & 1) != 0;
        public static bool FullTail(uint p) => (p >> 6 & 1) != 0;
        public static byte Color(uint p) => (byte)(p >> 9 & 0x7);
        public static byte Speed(uint p) => (byte)(p >> 12 & 0x7F);
        public static byte Minutes(uint p) => (byte)(p >> 19 & 0x3F);

        public static uint Properties(bool largeBeak, bool fullTail, bool largeTalons, uint color, uint speed, uint minutes) =>
            (largeBeak ? 1u : 0) | (largeTalons ? 1u << 3 : 0) | (fullTail ? 1u << 6 : 0) | (color & 0x7) << 9 | (speed & 0x7F) << 12 |
            (minutes & 0x3F) << 19;

        // mountutils::isPersonalChocobo: a registered chocobo, ridden as the char's own
        public static bool IsPersonalChocobo(CharData c)
        {
            if (FieldChocobo(c) == 0) return false;
            var effect = c.StatusEffects.GetStatusEffect(EffectId.Mounted);
            return effect != null && effect.Power == MountChocobo && (effect.SubPower & PersonalChocoboFlag) != 0;
        }

        // mountutils::packetDefinition: the ChocoboIndex and CustomProperties the char packets carry
        public static (byte ChocoboIndex, uint Custom0, uint Custom1) PacketDefinition(CharData c)
        {
            var effect = c.StatusEffects.GetStatusEffect(EffectId.Mounted);
            if (effect == null) return (0, 0, 0);
            ushort mount = effect.Power;
            return mount switch
            {
                // a customized chocobo needs ChocoboIndex 2, a regular one 1
                MountChocobo => IsPersonalChocobo(c) ? ((byte)2, FieldChocobo(c), 0u) : ((byte)1, 0u, 0u),
                // the Noble Chocobo: 4 in captures, with 1 in CustomProperties[1]
                MountNobleChocobo => ((byte)(mount % 8 + 2), 0u, 1u),
                _ => ((byte)(mount % 8 + 1), 0u, 0u),
            };
        }

        // mountutils::personalChocoboSpeed: the registered speed (map.MOUNT_SPEED when it predates speeds), plus the
        // silks' bonus while they are worn
        public static byte PersonalChocoboSpeed(CharData c)
        {
            uint speed = Speed(FieldChocobo(c));
            if (speed == 0) return MountSpeed;
            if (c.GetMod(Mod.PERSONAL_CHOCOBO_SPEED) > 0) speed += c.ChocoboUserData[OffSilksSpeedBonus];
            return (byte)Math.Min(speed, 0xFFu);
        }

        // CBattleEntity::UpdateSpeed for a char: a mount's speed, or the base speed through the gear, weight, flee,
        // cheer, Bolter's Roll, gear bonus and Quickening / Mazurka factors (each step truncating), capped at
        // map.SPEED_LIMIT. Sets and returns the char's speed
        public static byte UpdateSpeed(CharData c)
        {
            short output;
            if (c.StatusEffects.HasStatusEffect(EffectId.Mounted))
            {
                output = (short)(MountSpeed / 2);
                if (IsPersonalChocobo(c)) output = (short)(PersonalChocoboSpeed(c) / 2);
                output = (short)(output * (1.0f + c.GetMod(Mod.MOUNT_MOVE) / 100.0f));
            }
            else if ((c.BaseSpeedOverride ?? BaseSpeed) == 0 || c.GetMod(Mod.MOVE_SPEED_OVERRIDE) < 0) output = 0;
            else if (c.GetMod(Mod.MOVE_SPEED_OVERRIDE) > 0) output = c.GetMod(Mod.MOVE_SPEED_OVERRIDE);
            else
            {
                float gearFactor = Math.Clamp(1.0f + ZoneHost.MaxGearMod(c, Mod.MOVE_SPEED_GEAR_BONUS) / 100.0f, 1.0f, 1.25f);
                byte mazurkaQuickening = (byte)Math.Clamp(c.GetMod(Mod.MOVE_SPEED_QUICKENING) + c.GetMod(Mod.MOVE_SPEED_MAZURKA), 0, 10);
                output = (short)((c.BaseSpeedOverride ?? BaseSpeed) + (sbyte)c.GetMod(Mod.MOVE_SPEED_STACKABLE));
                output = (short)(output * Math.Clamp(1.0f - c.GetMod(Mod.MOVE_SPEED_WEIGHT_PENALTY) / 100.0f, 0.1f, 1.0f));
                output = (short)(output * Math.Clamp(1.0f + c.GetMod(Mod.MOVE_SPEED_FLEE) / 10000.0f, 1.0f, 2.0f));
                output = (short)(output * ((99.0f + c.GetMod(Mod.MOVE_SPEED_CHEER)) / 99.0f));
                output = (short)(output + (byte)c.GetMod(Mod.MOVE_SPEED_BOLTERS_ROLL));
                output = (short)(output * gearFactor);
                if (output > 0) output = (short)(output + mazurkaQuickening);
                output = Math.Clamp(output, (short)0, (short)SpeedLimit);
            }
            c.Speed = (byte)Math.Clamp(output, (short)0, (short)255);
            return c.Speed;
        }
    }
}
