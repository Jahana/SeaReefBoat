using System;

namespace XI.Zone
{
    // CCharEntity's automatonInfo_ (AutomatonInfo) and m_unlockedAttachments (UnlockedAttachments_t)
    public sealed class AutomatonInfo
    {
        public const byte FrameHarlequin = 0x20, FrameStormwaker = 0x23, HeadHarlequin = 0x01, HeadSpiritreaver = 0x06;

        // AutomatonEquip: frame, head, attachments[12] -- the char_pet.equipped_attachments blob
        public byte Frame, Head;
        public readonly byte[] Attachments = new byte[12];
        public readonly byte[] ElementMax = new byte[8], ElementEquip = new byte[8];
        public byte ElementalCapacityBonus;
        public string Name = "Automaton";
        public ushort ModelId;

        // automatonHealth / automatonStats (STR, DEX, VIT, AGI, INT, MND, CHR) from LoadAutomatonStats
        public ushort MaxHp, Hp, MaxMp, Mp;
        public readonly ushort[] Stats = new ushort[7];

        // automatonSkills: what CalculateAutomatonSkills last made of the master's automaton skills
        public readonly ushort[] Skills = new ushort[Skill.Max];

        // UnlockedAttachments_t: heads u8, frames u8, padding, attachments u32[8] -- the unlocked_attachments blob
        public byte UnlockedHeads, UnlockedFrames;
        public readonly byte[] UnlockedAttachments = new byte[32];

        public byte[] EquipBlob()
        {
            var b = new byte[14];
            b[0] = Frame;
            b[1] = Head;
            Attachments.CopyTo(b, 2);
            return b;
        }

        public byte[] UnlockedBlob()
        {
            var b = new byte[36];
            b[0] = UnlockedHeads;
            b[1] = UnlockedFrames;
            UnlockedAttachments.CopyTo(b, 4);
            return b;
        }

        public void LoadUnlocked(byte[] blob)
        {
            var b = new byte[36];
            Array.Copy(blob, b, Math.Min(blob.Length, b.Length));
            UnlockedHeads = b[0];
            UnlockedFrames = b[1];
            Array.Copy(b, 4, UnlockedAttachments, 0, 32);
        }

        public bool HasAttachment(byte attachment) => Array.IndexOf(Attachments, attachment) >= 0;

        // setAutomatonElementalCapacityBonus: the change moves every element's max
        public void SetElementalCapacityBonus(byte bonus)
        {
            if (bonus == ElementalCapacityBonus) return;
            int difference = (sbyte)(bonus - ElementalCapacityBonus);
            for (int i = 0; i < ElementMax.Length; i++) ElementMax[i] = (byte)(ElementMax[i] + difference);
            ElementalCapacityBonus = bonus;
        }
    }
}
