// Sin Hunting's era module, a line-for-line port of modules/era/lua/quests/windurst/sin_hunting.lua (quest rework B3b).
// It restores the full moon requirement to examine the ??? in Jugner Forest (the June 17, 2014 version update made the
// event occur regardless of Vana'diel time and the moon). Module:new('era_quest_sin_hunting', xi.pre(xi.expansion.SOA));
// its onServerStart override wraps section 2's Jugner Forest qm2 onTrigger: the quest's own action at the full moon
// (getVanadielMoonCycle() == xi.moonCycle.FULL_MOON), else nil.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Windurst
{
    public sealed class SinHunting : QuestModule
    {
        // xi.moonCycle.FULL_MOON (scripts/enum/moon_cycle.lua)
        const int MoonCycleFullMoon = 6;

        public SinHunting() : base("era_quest_sin_hunting", "modules/era/lua/quests/windurst/sin_hunting.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.JUGNER_FOREST]['qm2'].onTrigger: the quest's state 2 (accepted)
            m.Quest(QuestKey.Windurst.SinHunting)
                .State(2).Npc(Zone.JugnerForest, Npc.JugnerForest.qm2).WrapOnTrigger((q, baseOnTrigger) =>
                {
                    // getVanadielMoonCycle()
                    if (q.Player.VanadielMoonCycle == MoonCycleFullMoon)
                    {
                        return baseOnTrigger(q);
                    }

                    return null;
                });
        }
    }
}
