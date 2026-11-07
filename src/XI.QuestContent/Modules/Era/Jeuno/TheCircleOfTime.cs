// The Circle of Time's era module, a line-for-line port of modules/era/lua/quests/jeuno/the_circle_of_time.lua (quest
// rework B3b Jeuno, P4): it restores the JST midnight wait for the star ring buried in the perennial snow (the June 17,
// 2014 version update shortened it to about one minute). Module:new('era_quest_the_circle_of_time',
// xi.pre(xi.expansion.SOA)); its onServerStart override replaces section 2's Xarcabard onEventFinish[3] with a copy of the
// quest's handler, Buried set to JstMidnight() where the quest has GetSystemTime() + 30.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class TheCircleOfTime : QuestModule
    {
        public TheCircleOfTime() : base("era_quest_the_circle_of_time", "modules/era/lua/quests/jeuno/the_circle_of_time.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.XARCABARD].onEventFinish[3]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Jeuno.TheCircleOfTime)
                .State(2).ReplaceOnEventFinish(Zone.Xarcabard, 3, q =>
                {
                    // Copy of the base handler with the 30 second wait extended to JST midnight.
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Buried", q.Player.JstMidnight); // Module change: the ring is purified at JST midnight.
                    }
                });
        }
    }
}
