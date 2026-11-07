using System;

namespace XI.Zone
{
    // CAutomatonEntity: a PUP's automaton, a CPetEntity with its frame, head and attachments (the master's equipment
    // when it was activated) and the burden of each element its maneuvers build up. Its spawn and stats are in
    // ZoneHost.Automatons.cs.
    public sealed class AutomatonEntity : PetEntity
    {
        // equip_: AutomatonEquip
        public byte Frame, Head;
        public readonly byte[] Attachments = new byte[12];
        // burden_: per element, fire .. dark
        public readonly byte[] Burden = new byte[8];

        // CAutomatonController's cooldowns and last times (ms; ZoneHost.AutomatonAi.cs)
        public long ActionCooldownMs = 3000, RangedCooldownMs, MagicCooldownMs, EnfeebleCooldownMs, ElementalCooldownMs, HealCooldownMs,
            EnhanceCooldownMs, StatusCooldownMs, ShieldBashCooldownMs;
        public long LastActionMs, LastMagicMs, LastEnfeebleMs, LastElementalMs, LastHealMs, LastEnhanceMs, LastStatusMs, LastRangedMs,
            LastShieldBashMs;

        public AutomatonEntity(int petId) : base(PetType.Automaton, petId)
        {
        }

        public bool HasAttachment(byte attachment) => Array.IndexOf(Attachments, attachment) >= 0;

        public void SetEquip(AutomatonInfo info)
        {
            Frame = info.Frame;
            Head = info.Head;
            Array.Copy(info.Attachments, Attachments, Attachments.Length);
        }

        // burdenTick: each element's burden down by 1 + the BURDEN_DECAY of the master and the automaton
        public void BurdenTick(BattleEntity master)
        {
            for (int i = 0; i < Burden.Length; i++)
                if (Burden[i] > 0)
                    Burden[i] -= (byte)Math.Clamp(1 + master.GetMod(Mod.BURDEN_DECAY) + GetMod(Mod.BURDEN_DECAY), 1, Burden[i]);
        }

        // addBurden: a third of it with Suppress Overload; past the threshold (30 + OVERLOAD_THRESH) an overload with
        // the chance of the excess + 5%, whose duration is the excess
        public byte AddBurden(BattleEntity master, byte element, sbyte burden, Random rng)
        {
            // Handle Kenkonken Suppress Overload
            if (master.GetMod(Mod.SUPPRESS_OVERLOAD) > 0) burden = (sbyte)(burden / 3);
            Burden[element] = (byte)Math.Clamp(Burden[element] + burden, 0, 255);
            if (burden > 0)
            {
                int thresh = 30 + master.GetMod(Mod.OVERLOAD_THRESH);
                if (Burden[element] > thresh && rng.Next(100) < Burden[element] - thresh + 5) return (byte)(Burden[element] - thresh);
            }
            return 0;
        }

        // overloadChance
        public byte OverloadChance(BattleEntity master, byte element)
        {
            int thresh = 30 + master.GetMod(Mod.OVERLOAD_THRESH);
            return (byte)Math.Clamp(Burden[element] - thresh + 5, 0, 255);
        }
    }
}
