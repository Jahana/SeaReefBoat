// Eco-Warrior (San d'Oria)'s era module, a line-for-line port of modules/era/lua/quests/sandoria/eco_warrior.lua (quest
// rework B3b, San d'Oria group 10): it restores the level 20 restriction on the ointment (the June 25, 2015 version
// update raised the level cap from 20 to 25).
// Module:new('era_quest_eco_warrior_sandoria', xi.pre(xi.expansion.ROV)); its onServerStart override replaces section
// 2's Ordelles Caves onEventFinish[51] with a copy of the quest's handler, the restriction's power 20 instead of 25.
// The twin of src/XI.QuestContent/Modules/Era/Bastok/EcoWarrior.cs.
using XI.Quests.Authoring;
using BastokEcoWarrior = XI.QuestContent.Bastok.EcoWarrior;

namespace XI.QuestContent.Modules.Era.Sandoria
{
    public sealed class EcoWarrior : QuestModule
    {
        public EcoWarrior() : base("era_quest_eco_warrior_sandoria", "modules/era/lua/quests/sandoria/eco_warrior.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.ORDELLES_CAVES].onEventFinish[51]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Sandoria.EcoWarrior)
                .State(2).ReplaceOnEventFinish(Zone.OrdellesCaves, 51, q =>
                {
                    // Copy of the base handler with the level restriction lowered to 20.
                    if (q.Option == 1)
                    {
                        // Module change: the level restriction is 20. subPower 1: exp uses actual level and not the
                        // restricted level; the origin is the char
                        q.Player.AddStatusEffect(BastokEcoWarrior.LevelRestriction, power: 20, subPower: 1, flag: BastokEcoWarrior.EffectFlagOnZone);
                    }
                });
        }
    }
}
