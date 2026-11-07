// Vanishing Act's era module, a line-for-line port of modules/era/lua/quests/ahturhgan/vanishing_act.lua (quest
// rework B3b Aht Urhgan, P4). It restores the JST midnight wait before A Taste of Honey opens (the September 9, 2014
// version update shortened the wait to one minute;
// https://forum.square-enix.com/ffxi/threads/44090-Sep-9-2014-(JST)-Version-Update).
// Module:new('era_quest_vanishing_act', xi.pre(xi.expansion.SOA)); its onServerStart override replaces section 2's
// Whitegate onEventFinish 45 with a copy of the quest's own whose A Taste of Honey Stage is JstMidnight(), not now +
// 60.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class VanishingAct : QuestModule
    {
        public VanishingAct() : base("era_quest_vanishing_act",
            "modules/era/lua/quests/ahturhgan/vanishing_act.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.AHT_URHGAN_WHITEGATE].onEventFinish[45]: the quest's state 2 (accepted)
            m.Quest(QuestKey.AhtUrhgan.VanishingAct)
                .State(2).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 45, q =>
                {
                    // Copy of the base handler. A Taste of Honey opens at JST midnight.
                    if (q.Quest.Complete())
                    {
                        q.Player.SetNeedToZone(true);
                        q.Player.DelKeyItem(KeyItem.RainbowBerry);
                        // Module change
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "A_TASTE_OF_HONEY", "Stage", q.Player.JstMidnight);
                    }
                });
        }
    }
}
