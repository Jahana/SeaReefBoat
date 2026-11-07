// A Thief in Norg!?'s era module, a line-for-line port of modules/era/lua/quests/outlands/a_thief_in_norg.lua (quest rework
// B3b Outlands, group 1; P4). It restores the JST midnight wait before Jaucribaix repairs the Charred Helm (the July 8,
// 2014 version update shortened the wait to one minute; https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update).
// Module:new('era_quest_a_thief_in_norg', xi.pre(xi.expansion.SOA)); its onServerStart override wraps section 2's Norg
// onEventFinish 162 (the base, then Wait = JstMidnight()).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Outlands
{
    public sealed class AThiefInNorg : QuestModule
    {
        public AThiefInNorg() : base("era_quest_a_thief_in_norg", "modules/era/lua/quests/outlands/a_thief_in_norg.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.Outlands.AThiefInNorg)
                // The helm is repaired at JST midnight.
                .State(2).WrapOnEventFinish(Zone.Norg, 162, (q, baseThreadCS) =>
                {
                    baseThreadCS(q);
                    q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                });
        }
    }
}
