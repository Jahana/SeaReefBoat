using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // Blue magic learning: blueutils::TryLearningSpells at the kill (CMobEntity::DistributeRewards, unless help was
    // called), from the mob skills the mob used (m_UsedSkillIds, filled by OnMobSkillFinished in ZoneHost.MobSkills.cs
    // and cleared at the kill and when the mob rests to full HP), through spell::GetSpellByMonsterSkillId
    // (blue_spell_list's mob_skill_id).
    public sealed partial class ZoneHost
    {
        private const ushort MsgLearnsSpell = 419; // MsgBasic::LearnsSpell: <target> learns <spell>!
        private const byte JobBlu = 16;

        // spell::LoadSpellList's PMobSkillToBlueSpell: a blue_spell_list row whose content is on and whose spell is
        // in the spell list; the first row for a mob skill wins (std::map::insert)
        private Dictionary<ushort, ushort> mobSkillToBlueSpell_;

        // spell::GetSpellByMonsterSkillId
        private Spell SpellByMonsterSkillId(ushort skillId)
        {
            if (mobSkillToBlueSpell_ == null)
            {
                mobSkillToBlueSpell_ = new Dictionary<ushort, ushort>();
                var r = Exec("SELECT blue_spell_list.spellid, blue_spell_list.mob_skill_id, spell_list.content_tag " +
                             "FROM blue_spell_list JOIN spell_list on blue_spell_list.spellid = spell_list.spellid");
                while (r != null && r.Next())
                {
                    string tag = r.IsNull("content_tag") ? "" : r.Get<string>("content_tag");
                    if (tag != "" && !contentEnabled_(tag)) continue;
                    ushort spellId = r.Get<ushort>("spellid");
                    if (GetSpell(spellId) == null)
                    {
                        log_($"spell::LoadSpellList Tried to load nullptr blue spell ({spellId})");
                        continue;
                    }
                    mobSkillToBlueSpell_.TryAdd(r.Get<ushort>("mob_skill_id"), spellId);
                }
            }
            return mobSkillToBlueSpell_.TryGetValue(skillId, out var id) ? GetSpell(id) : null;
        }

        // blueutils::TryLearningSpells: each BLU of the killer's alliance (or the killer alone) in the mob's zone,
        // alive and within 100 yalms, gets one try at the first skill it does not know yet whose spell is within 31
        // points of its blue magic skill: 33% plus BLUE_LEARN_CHANCE
        private void TryLearningSpells(CharData pc, MobEntity m)
        {
            // minor optimisation
            if (m.UsedSkillIds.Count == 0) return;

            // prune the learnable blue spells
            var learnable = new List<Spell>();
            foreach (var used in m.UsedSkillIds)
                if (SpellByMonsterSkillId(used.Key) is { } spell) learnable.Add(spell);
            if (learnable.Count == 0) return;

            // populate the BLUs: the alliance's or the party's members, or the char alone when it is one
            var blueMages = new List<CharData>();
            foreach (var member in ForAlliance(pc))
                if (member.MJob == JobBlu && (pc.Party == null || member.Zone == m.Zone)) blueMages.Add(member);

            var growth = loader_.Stats?.Growth;
            foreach (var blu in blueMages)
            {
                // too dead to learn
                if (blu.Hp <= 0) continue;
                // too far away to learn
                if (Distance(blu, m) > 100) continue;

                foreach (var spell in learnable)
                {
                    if (HasBit(blu.SpellList, spell.Id)) continue;

                    // get the skill cap for the spell level
                    int skillLvlForSpell = growth?.GetMaxSkill(Skill.BlueMagic, JobBlu, spell.Job(JobBlu)) ?? 0;
                    // get player skill level with bonus from gear
                    int playerSkillLvl = CharStats.GetSkill(blu, Skill.BlueMagic);

                    // make sure the difference between spell skill and player is at most 31 points
                    if (playerSkillLvl >= skillLvlForSpell - 31)
                    {
                        int chanceToLearn = 33 + blu.GetMod(Mod.BLUE_LEARN_CHANCE);
                        if (MobRng.Next(100) < chanceToLearn && spell.Id / 8 < blu.SpellList.Length)
                        {
                            // charutils::addSpell, the message, SaveSpell and the MAGIC_DATA
                            blu.SpellList[spell.Id / 8] |= (byte)(1 << spell.Id % 8);
                            PushTo(blu, SelfPackets.BattleMessage(blu, spell.Id, 0, MsgLearnsSpell));
                            Exec("INSERT IGNORE INTO char_spells VALUES (?, ?)", BoundValue.Of(blu.Id), BoundValue.Of(spell.Id));
                            PushTo(blu, LoginPackets.MagicData(blu));
                        }
                        break; // only one attempt at learning a spell, regardless of learn or not.
                    }
                }
            }
        }
    }
}
