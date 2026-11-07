// A Greeting Cardian's era module, a line-for-line port of modules/era/lua/quests/windurst/a_greeting_cardian.lua
// (quest rework B3b). It restores the JST midnight wait before Kororo offers the quest (the July 8, 2014 version update
// shortened it to one minute; the zone change after the teaching stays in the base script).
// Module:new('era_quest_a_greeting_cardian', xi.pre(xi.expansion.SOA)); its onServerStart override wraps section 1's
// Windurst Woods 295 finish: the base finish, then Wait = JstMidnight(), on every option (an escape included: the base
// teaches nothing, the module still writes Wait).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Windurst
{
    public sealed class AGreetingCardian : QuestModule
    {
        public AGreetingCardian() : base("era_quest_a_greeting_cardian", "modules/era/lua/quests/windurst/a_greeting_cardian.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[1][xi.zone.WINDURST_WOODS].onEventFinish[295]: the quest's state 1 (available)
            m.Quest(QuestKey.Windurst.AGreetingCardian)
                .State(1).WrapOnEventFinish(Zone.WindurstWoods, 295, (q, baseTeachCS) =>
                {
                    baseTeachCS(q);
                    // Kororo makes the offer at JST midnight, after the zone change.
                    q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                });
        }
    }
}
