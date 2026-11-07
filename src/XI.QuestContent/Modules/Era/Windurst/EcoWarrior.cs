// Eco-Warrior (Windurst)'s era module, a line-for-line port of modules/era/lua/quests/windurst/eco_warrior.lua (quest
// rework B3b, Windurst group 12): it restores the level 20 restriction on the ointment (the June 25, 2015 version update
// raised the level cap from 20 to 25).
// Module:new('era_quest_eco_warrior_windurst', xi.pre(xi.expansion.ROV)); its onServerStart override replaces section
// 2's Maze of Shakhrami onEventFinish[62] with a copy of the quest's handler, the restriction's power 20 instead of 25.
// The twin of Modules/Era/Bastok/EcoWarrior.cs.
using XI.Quests.Authoring;
using BastokEcoWarrior = XI.QuestContent.Bastok.EcoWarrior;

namespace XI.QuestContent.Modules.Era.Windurst
{
    public sealed class EcoWarrior : QuestModule
    {
        public EcoWarrior() : base("era_quest_eco_warrior_windurst", "modules/era/lua/quests/windurst/eco_warrior.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.MAZE_OF_SHAKHRAMI].onEventFinish[62]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Windurst.EcoWarrior)
                .State(2).ReplaceOnEventFinish(Zone.MazeOfShakhrami, 62, q =>
                {
                    // Copy of the base handler with the level restriction lowered to 20.
                    if (q.Option == 1)
                    {
                        // Module change: the level restriction is 20. subPower 1: exp uses actual level and not the
                        // restricted level; the origin is the char
                        q.Player.AddStatusEffect(BastokEcoWarrior.LevelRestriction, power: 20, subPower: 1,
                                                 flag: BastokEcoWarrior.EffectFlagOnZone);
                    }
                });
        }
    }
}
