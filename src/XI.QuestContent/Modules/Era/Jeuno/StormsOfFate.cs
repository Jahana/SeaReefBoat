// Storms of Fate's era module, a line-for-line port of modules/era/lua/quests/jeuno/storms_of_fate.lua (quest rework B3b
// Jeuno, P4). It restores the JST midnight wait
// before Shadows of the Departed: Module:new('era_quest_storms_of_fate', xi.pre(xi.expansion.SOA)); its onServerStart
// override replaces section 2's Ru'Lude Gardens 143 finish (Shadows of the Departed's Timer = JstMidnight()).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class StormsOfFate : QuestModule
    {
        public StormsOfFate() : base("era_quest_storms_of_fate", "modules/era/lua/quests/jeuno/storms_of_fate.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // Copy of the base handler with the one Vana'diel day wait extended to JST midnight.
            m.Quest(QuestKey.Jeuno.StormsOfFate)
                .State(2).ReplaceOnEventFinish(Zone.RuludeGardens, 143, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(QuestLog.Jeuno, "SHADOWS_OF_THE_DEPARTED", "Timer", q.Player.JstMidnight); // Module change: Shadows of the Departed opens at JST midnight.
                    }
                });
        }
    }
}
