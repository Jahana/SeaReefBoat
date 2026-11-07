// Black Market (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Muzaffar.lua (quest rework B3b,
// Outlands group 6; call-only: the script now calls this quest). Muzaffar : !pos 16.678 -2.044 -14.600 252.
// Muzaffar: accepted or completed: 16; otherwise (available): 15, whose finish on option 1 begins the quest. His trade,
// accepted or completed, four of one kind and four items in all: the trade taken (tradeComplete) and 17 (Northern Furs)
// / 18 (Eastern Pottery) / 19 (Southern Mummies), each with the item twice. Their finishes: 1500 / 2000 / 3000 gil
// (npcUtil.giveCurrency), the quest completed while accepted, Norg fame +30 / +40 / +50, the Black Marketeer title, then
// 20 started by the finish.
// Kept as the Lua has it: the trade is taken before the event (tradeComplete in onTrade); the reward repeats each time
// once completed (fame, gil and the title again); 20 is started inside the finish (player:startEvent).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class BlackMarket : QuestScript
    {
        public BlackMarket() : base(QuestLog.Outlands, "BLACK_MARKET") { }

        // a sale's finish: the gil, the completion while accepted, the fame and the title, then 20
        private static void Sale(IQuestContext q, int gil, int fame)
        {
            q.Player.GiveCurrency("gil", gil);
            if (q.Quest.Status == QuestStatus.Accepted)
            {
                // the raw player:completeQuest, which keeps the quest's own Quest[5][130] vars
                q.Player.CompleteQuest(QuestLog.Outlands, "BLACK_MARKET");
            }

            q.Player.AddFame(FameArea.Norg, fame);
            q.Player.AddTitle(Title.BlackMarketeer);
            q.Player.StartEvent(20);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Muzaffar.lua onTrigger: the else (15) is the available arm
            b.State(QuestStatus.Available)
                .Npc(Zone.Norg, Npc.Norg.Muzaffar).Called().OnTrigger(QuestReply.Event(15));

            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Muzaffar).Called()
                    .OnTrigger(QuestReply.Event(16))
                    // Muzaffar.lua onTrade
                    .OnTrade(q =>
                    {
                        int count = q.Trade.ItemCount;
                        bool northernFurs = q.Trade.HasItemQty(Item.NorthernFur, 4);
                        bool easternPottery = q.Trade.HasItemQty(Item.PieceOfEasternPottery, 4);
                        bool southernMummies = q.Trade.HasItemQty(Item.SouthernMummy, 4);

                        if (northernFurs && count == 4)
                        {
                            q.Trade.Complete();
                            return QuestReply.Event(17, Item.NorthernFur, Item.NorthernFur);
                        }
                        else if (easternPottery && count == 4)
                        {
                            q.Trade.Complete();
                            return QuestReply.Event(18, Item.PieceOfEasternPottery, Item.PieceOfEasternPottery);
                        }
                        else if (southernMummies && count == 4)
                        {
                            q.Trade.Complete();
                            return QuestReply.Event(19, Item.SouthernMummy, Item.SouthernMummy);
                        }

                        return null;
                    });

            // Muzaffar.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Norg, 15, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Norg, 17, q => Sale(q, 1500, 30))
                .OnEventFinish(Zone.Norg, 18, q => Sale(q, 2000, 40))
                .OnEventFinish(Zone.Norg, 19, q => Sale(q, 3000, 50));
        }
    }
}
