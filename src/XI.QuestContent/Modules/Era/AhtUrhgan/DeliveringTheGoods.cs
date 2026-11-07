// Delivering the Goods' era module, a line-for-line port of modules/era/lua/quests/ahturhgan/delivering_the_goods.lua
// (quest rework B3b Aht Urhgan, P4). It restores the JST midnight wait before Vanishing Act opens (the September 9,
// 2014 version update shortened the wait to one minute;
// https://forum.square-enix.com/ffxi/threads/44090-Sep-9-2014-(JST)-Version-Update).
// Module:new('era_quest_delivering_the_goods', xi.pre(xi.expansion.SOA)); its onServerStart override replaces section
// 3's Whitegate onEventFinish 41 with a copy of the quest's own whose Vanishing Act Stage is JstMidnight(), not now +
// 60.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class DeliveringTheGoods : QuestModule
    {
        public DeliveringTheGoods() : base("era_quest_delivering_the_goods",
            "modules/era/lua/quests/ahturhgan/delivering_the_goods.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[3][xi.zone.AHT_URHGAN_WHITEGATE].onEventFinish[41]: the quest's state 3 (accepted, Prog 1)
            m.Quest(QuestKey.AhtUrhgan.DeliveringTheGoods)
                .State(3).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 41, q =>
                {
                    // Copy of the base handler. Vanishing Act opens at JST midnight.
                    // npcUtil.completeQuest gives reward.item ({ { IMPERIAL_BRONZE_PIECE, 3 } })
                    // first and stops when it does not fit
                    if (q.Player.GiveItems((Item.ImperialBronzePiece, 3)) && q.Quest.Complete())
                    {
                        // Module change
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "VANISHING_ACT", "Stage", q.Player.JstMidnight);

                        // Player must zone before being able to flag the next quest
                        q.Player.SetNeedToZone(true);
                    }
                });
        }
    }
}
