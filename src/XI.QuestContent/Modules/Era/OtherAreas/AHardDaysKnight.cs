// A Hard Day's Knight's era module, a line-for-line port of modules/era/lua/quests/otherareas/a_hard_days_knight.lua (quest
// rework B3b, Other Areas group 7): it removes the ability to obtain the Temple Knight Key
// (https://forum.square-enix.com/ffxi/threads/40059). Module:new('era_quest_a_hard_days_knight', xi.pre(xi.expansion.SOA));
// its onServerStart override runs table.remove(quest.sections, 3): the completed section (Quelveuiat's crest-key trade
// and trigger, the 631 and 632 finishes) is gone.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.OtherAreas
{
    public sealed class AHardDaysKnight : QuestModule
    {
        public AHardDaysKnight() : base("era_quest_a_hard_days_knight", "modules/era/lua/quests/otherareas/a_hard_days_knight.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // table.remove(quest.sections, 3): the quest's state 3 (completed)
            m.Quest(QuestKey.OtherAreas.AHardDaysKnight)
                .RemoveState(3);
        }
    }
}
