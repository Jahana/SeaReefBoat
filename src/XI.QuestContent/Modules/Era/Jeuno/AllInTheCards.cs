// All in the Cards' era module, a line-for-line port of modules/era/lua/quests/jeuno/all_in_the_cards.lua (quest rework
// B3b Jeuno, P4): it removes the quest (Chululu did not offer it before the July 12, 2011 version update).
// Module:new('era_quest_all_in_the_cards', xi.pre(xi.expansion.ABYSSEA)); its onServerStart override replaces the checks
// of section 1 (the first offer) and section 2 (the repeat offer) with false. A char already holding the quest can still
// turn it in (section 3 is the quest's).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class AllInTheCards : QuestModule
    {
        public AllInTheCards() : base("era_quest_all_in_the_cards", "modules/era/lua/quests/jeuno/all_in_the_cards.lua") { }

        // xi.pre(xi.expansion.ABYSSEA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Abyssea);

        protected override void Build(QuestModuleBuilder m)
        {
            // The first offer and the repeat offer. A player already holding the quest can still turn it in.
            m.Quest(QuestKey.Jeuno.AllInTheCards)
                .State(1).ReplaceCheck(q => false) // quest.sections[1].check: available
                .State(2).ReplaceCheck(q => false); // quest.sections[2].check: completed
        }
    }
}
