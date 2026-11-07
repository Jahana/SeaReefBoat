// Collect Tarut Cards' era module, a line-for-line port of modules/era/lua/quests/jeuno/collect_tarut_cards.lua (quest
// rework B3b Jeuno, P4): it removes the extra batch of cards (Chululu gives five once, when the quest is accepted; the July
// 12, 2011 version update let her hand out a new batch after each tally). Module:new('era_quest_collect_tarut_cards',
// xi.pre(xi.expansion.ABYSSEA)); its onServerStart override replaces section 2's Lower Jeuno Chululu onTrigger with
// quest:event(27), the reminder, whatever the Date (an Event, where the quest's 27 / 10112 are Progress events).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class CollectTarutCards : QuestModule
    {
        public CollectTarutCards() : base("era_quest_collect_tarut_cards", "modules/era/lua/quests/jeuno/collect_tarut_cards.lua") { }

        // xi.pre(xi.expansion.ABYSSEA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Abyssea);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.LOWER_JEUNO]['Chululu'].onTrigger: the quest's state 2 (accepted)
            m.Quest(QuestKey.Jeuno.CollectTarutCards)
                .State(2).Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).ReplaceOnTrigger(q =>
                {
                    return QuestReply.Event(27); // Reminder
                });
        }
    }
}
