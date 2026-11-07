using System;

namespace XI.Zone
{
    // CAbility: one row of the abilities table. The times are in milliseconds; the recast time is stored already
    // scaled by map.ABILITY_RECAST_MULTIPLIER (CAbility::setRecastTime). The pet abilities build a char's pet commands
    // (ZoneHost.PetSkills.cs); a pet skill's mob skill id is CPetSkill's (MobSkill), as CAbility::getMobSkillID is
    // declared and never defined.
    public sealed class AbilityInfo
    {
        // ABILITY ids the ability code checks
        public const ushort Steal = 41, Gauge = 53, Tame = 54, ThirdEye = 62, Fight = 69, Sic = 72, TrickAttack = 76, Reward = 78,
            Assault = 88, Deploy = 138, LightArts = 211, DarkArts = 212, Ready = 251, ConcentricPulse = 353, RadialArcana = 355,
            HealingRuby = 512;

        public ushort Id;
        public string Name = "";
        public byte Job;
        public byte Level;
        public ushort ValidTarget;
        public long RecastTimeMs;
        public ushort Message;
        public ushort Animation;
        public long AnimationTimeMs;
        public long CastTimeMs;
        public byte ActionType;
        public float Range;
        public byte Aoe;
        public byte Radius;
        public ushort RecastId;
        public int CE, VE;
        public ushort MeritModId;
        public ushort AddType;
        public ushort CleanupEffect; // m_postActionEffectCleanup
        public double RecastMultiplier = 1.0;

        // CAbility::isPetAbility
        public bool IsPetAbility => Id >= ConcentricPulse && Id <= RadialArcana || Id >= HealingRuby;

        // CAbility::isAoE
        public bool IsAoE => Aoe == 1;

        // CAbility::setRecastTime: floored to the millisecond after map.ABILITY_RECAST_MULTIPLIER
        public void SetRecastTime(long ms) => RecastTimeMs = (long)Math.Floor(ms * RecastMultiplier);

        // the copy CAbilityState works on (std::make_unique<CAbility>(*PAbility))
        public AbilityInfo Clone() => (AbilityInfo)MemberwiseClone();
    }

    // Charge_t: one row of abilities_charges
    public sealed class AbilityCharge
    {
        public ushort RecastId;
        public byte Job;
        public byte Level;
        public byte MaxCharges;
        public long ChargeTimeMs;
        public ushort Merit;
    }
}
