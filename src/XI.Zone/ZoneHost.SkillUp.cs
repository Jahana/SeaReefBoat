using System;

namespace XI.Zone
{
    // charutils::TrySkillUP: the combat and magic skill-up roll. A level gained raises the shown skill, unless the
    // scholar's arts lift it and the lift shrank by as much (the real skill still under the arts baseline).
    public sealed partial class ZoneHost
    {
        // the Rhapsodies key items: each one adds a multiple of the skill gained
        private static readonly int[] SkillupIncreaseKeyItems = { 2884, 2887, 2890 };

        internal void TrySkillUp(CharData c, int skillId, int level, bool forceSkillUp, bool useSubSkill)
        {
            if (skillId < 0 || skillId >= Skill.Max)
            {
                log_($"SkillID ({skillId}) exceeds MAX_SKILLTYPE.");
                return;
            }
            if (!((c.WorkingSkillRank[skillId] != 0 && (c.WorkingSkills[skillId] & 0x8000) == 0) || useSubSkill)) return;
            var growth = loader_.Stats?.Growth;
            if (growth == null) return;

            int curSkill = c.RealSkills[skillId];
            int mainCap = growth.GetMaxSkill(skillId, c.MJob, c.MLvl), subCap = growth.GetMaxSkill(skillId, c.SJob, c.SLvl);
            int mainMax = growth.GetMaxSkill(skillId, c.MJob, Math.Min((int)c.MLvl, level));
            int subMax = growth.GetMaxSkill(skillId, c.SJob, Math.Min((int)c.SLvl, level));
            int capSkill = mainCap, maxSkill = mainMax;
            if (useSubSkill && mainCap <= subCap)
            {
                capSkill = subCap;
                maxSkill = subMax;
            }

            var cfg = ChatCfg;
            short diff = (short)(maxSkill - curSkill / 10);
            double chance = diff / 5.0 + cfg.SkillupChanceMultiplier * (2.0 - Math.Log10(1.0 + curSkill / 100));
            double random = Random01();
            chance = Math.Min(chance, 0.5);

            if (skillId >= 1 && skillId <= 12 || skillId >= 25 && skillId <= 31)
                chance *= (100.0f + c.GetMod(Mod.COMBAT_SKILLUP_RATE)) / 100.0f;
            else if (skillId >= 32 && skillId <= 44)
                chance *= (100.0f + c.GetMod(Mod.MAGIC_SKILLUP_RATE)) / 100.0f;

            if (diff <= 0 || !(random < chance || forceSkillUp)) return;

            int amount = 1, tier = Math.Min(1 + diff / 5, 5);
            for (int i = 0; i < 4; i++) // up to 4 more, 5 in all
            {
                random = Random01();
                double extra = tier switch { 5 => 0.900, 4 => 0.700, 3 => 0.500, 2 => 0.300, 1 => 0.200, _ => 0.000 };
                if (extra < random || amount == 5) break;
                tier--;
                amount++;
            }
            capSkill *= 10; // tenths

            int rovBonus = 1;
            foreach (int keyItem in SkillupIncreaseKeyItems)
                if (c.HasKeyItem(keyItem)) rovBonus++;
            amount = Math.Min((int)(byte)(amount * rovBonus), 9);

            if (cfg.SkillupAmountMultiplier > 1)
                amount = Math.Min((int)(byte)(amount + (byte)(amount * cfg.SkillupAmountMultiplier)), 9);

            if (amount + curSkill >= capSkill)
            {
                // capped: the blue flag
                amount = capSkill - curSkill;
                c.WorkingSkills[skillId] |= 0x8000;
            }

            // whether the skill-up changes the arts' lift
            var stats = loader_.Stats;
            bool arts = CharStats.IsArtsBonusActive(c, skillId);
            short artsBonus = arts ? stats.ArtsBonusSkill(c, skillId) : (short)0;

            c.RealSkills[skillId] += (ushort)amount;
            PushTo(c, SelfPackets.BattleMessage(c, skillId, (byte)amount, MsgSkillGain));
            if (curSkill / 10 < (curSkill + amount) / 10)
            {
                if (!arts || artsBonus == stats.ArtsBonusSkill(c, skillId)) c.WorkingSkills[skillId] += 1;
                PushTo(c, SelfPackets.CliStatus2(c));
                PushTo(c, SelfPackets.BattleMessage(c, skillId, (curSkill + amount) / 10, MsgSkillLevelUp));
                CheckWeaponSkill(c, skillId);
            }
            SaveCharSkill(c, skillId);
        }
    }
}
