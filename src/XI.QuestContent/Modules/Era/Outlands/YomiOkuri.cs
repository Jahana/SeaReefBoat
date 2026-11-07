// Yomi Okuri's era module, a line-for-line port of modules/era/lua/quests/outlands/yomi_okuri.lua (quest rework B3b
// Outlands, group 1; P4). It restores the JST midnight wait before Jaucribaix hands over the Yomotsu Hirasaka, and the
// same wait before he offers A Thief in Norg!? (the July 8, 2014 version update shortened both to one minute;
// https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update).
// Module:new('era_quest_yomi_okuri', xi.pre(xi.expansion.SOA)); its onServerStart override wraps section 2's Norg
// onEventFinish 152 (the base, then Wait = JstMidnight()) and replaces its 156 with a copy that sets A Thief in Norg!?'s
// Timer = JstMidnight().
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Outlands
{
    public sealed class YomiOkuri : QuestModule
    {
        public YomiOkuri() : base("era_quest_yomi_okuri", "modules/era/lua/quests/outlands/yomi_okuri.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            var norg = m.Quest(QuestKey.Outlands.YomiOkuri).State(2);

            // The hirasaka is handed over at JST midnight.
            norg.WrapOnEventFinish(Zone.Norg, 152, (q, baseFeatherCS) =>
            {
                baseFeatherCS(q);
                q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
            });

            // Copy of the base handler. A Thief in Norg!? is offered at JST midnight.
            norg.ReplaceOnEventFinish(Zone.Norg, 156, q =>
            {
                if (q.Quest.Complete())
                {
                    q.Player.DelKeyItem(KeyItem.FadedYomotsuHirasaka);
                    q.Player.SetQuestMustZone(QuestLog.Outlands, "A_THIEF_IN_NORG");
                    q.Player.SetQuestVar(QuestLog.Outlands, "A_THIEF_IN_NORG", "Timer", q.Player.JstMidnight); // Module change
                }
            });
        }
    }
}
