// Gourmet (Log ID 1, Quest ID 12), a line-for-line port of scripts/quests/bastok/Gourmet.lua (quest rework B3b). Salimah
// : !pos -31.687 -6.824 -73.282 235. Salimah's 200 begins it; one Sleepshroom, Treant Bulb or Wild Onion traded while the
// quest's mustZone is clear plays its event for the Vana'diel hour (201 / 202 in the item's window, 203 outside it), whose
// finish completes the quest (Mommy's Helper) and pays 200 / 350 / 100 gil and Bastok fame +10, then sets the mustZone.
// The windows are on VanadielHour() - 6 (wrapped into 0..23): Sleepshroom 12..23 (18:00-06:00), Treant Bulb 0..5
// (06:00-12:00), Wild Onion 6..11 (12:00-18:00).
// Kept as the Lua file has it: the second section matches accepted and completed, so after each zone the trade completes
// it again, with its gil and fame; the reward table holds only the title, and the gil and fame are given after
// quest:complete. The Lua walks tradeItemData with pairs(); a trade matches at most one of its items, so the order does
// not show.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Gourmet : QuestScript
    {
        public Gourmet() : base(QuestLog.Bastok, "GOURMET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Gourmet";

        // tradeItemData: { item, eventId, timeMin, timeMax }
        static readonly (int Item, int EventId, int TimeMin, int TimeMax)[] TradeItemData =
        {
            (Item.Sleepshroom, 201, 12, 24), // 18:00 ~ 06:00
            (Item.TreantBulb,  201,  0,  6), // 06:00 ~ 12:00
            (Item.WildOnion,   202,  6, 12), // 12:00 ~ 18:00
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.MommysHelper });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Salimah).OnTrigger(QuestReply.ProgressEvent(200))
                .OnEventFinish(Zone.BastokMarkets, 200, q => q.Quest.Begin());

            // status >= QUEST_ACCEPTED
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Salimah)
                    .OnTrade(q =>
                    {
                        if (!q.Quest.MustZone)
                        {
                            foreach (var itemData in TradeItemData)
                            {
                                if (q.Trade.Matches((itemData.Item, 1)))
                                {
                                    // VanadielHour() - 6
                                    int timeOffset = q.Player.VanadielHour - 6;

                                    if (timeOffset < 0)
                                    {
                                        timeOffset = 24 + timeOffset;
                                    }

                                    if (timeOffset >= itemData.TimeMin && timeOffset < itemData.TimeMax)
                                    {
                                        return QuestReply.ProgressEvent(itemData.EventId, itemData.Item);
                                    }
                                    else
                                    {
                                        return QuestReply.ProgressEvent(203, itemData.Item);
                                    }
                                }
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q => !q.Quest.MustZone ? QuestReply.Event(200) : QuestReply.Event(121))
                .OnEventFinish(Zone.BastokMarkets, 201, q => TradeEventFinish(q, 200))
                .OnEventFinish(Zone.BastokMarkets, 202, q => TradeEventFinish(q, 350))
                .OnEventFinish(Zone.BastokMarkets, 203, q => TradeEventFinish(q, 100));
        }

        // tradeEventFinish(player, gilReward)
        static void TradeEventFinish(IQuestContext q, int gilReward)
        {
            if (q.Quest.Complete())
            {
                q.Trade.Complete();
                q.Player.GiveCurrency("gil", gilReward);
                q.Player.AddFame(FameArea.Bastok, 10);
                q.Quest.SetMustZone();
            }
        }
    }
}
