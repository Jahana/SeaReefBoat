// The Merchant's Bidding (Log ID 0, Quest ID 69), a line-for-line port of scripts/quests/sandoria/The_Merchants_Bidding.lua
// (quest rework B3b, San d'Oria group 1). Parvipon : !pos -169 -1 13 230. Available: 90 (option 1 begins it). Accepted: 88;
// completed: 90 with option [7] = 1 (no finish handler then). Accepted or completed, three Rabbit Hides (tradeHas) play 89:
// quest:complete (no reward table: always true, so fame +10 each time), 120 gil, the trade confirmed.
// Kept as the Lua has it: the quest repeats without being taken again, and every 89 runs quest:complete again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheMerchantsBidding : QuestScript
    {
        public TheMerchantsBidding() : base(QuestLog.Sandoria, "THE_MERCHANTS_BIDDING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Merchants_Bidding";

        protected override void Build(QuestScriptBuilder b)
        {
            // no quest.reward in the Lua

            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Parvipon).OnTrigger(QuestReply.ProgressEvent(90))
                .OnEventFinish(Zone.SouthernSanDoria, 90, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // status ~= QUEST_AVAILABLE
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Parvipon)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { xi.item.RABBIT_HIDE, 3 } })
                        if (q.Trade.HasQty((Item.RabbitHide, 3)))
                        {
                            return QuestReply.ProgressEvent(89);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(88);
                        }
                        else
                        {
                            return QuestReply.Event(90).Option(7, 1);
                        }
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 89, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                    }

                    q.Player.GiveCurrency("gil", 120);
                    q.Trade.Confirm();
                });
        }
    }
}
