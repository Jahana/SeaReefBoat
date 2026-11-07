// Hunger Strikes (Log ID 9, Quest ID 76), a line-for-line port of scripts/quests/adoulin/Hunger_Strikes.lua (quest
// rework B3b Adoulin group 1). Western Adoulin: Westerly Breeze !pos 62 32 123 256 (his script:
// Always More Quoth the Ravenous and the gruel trade; default 553).
// Available: Westerly Breeze: 2530 (a progressEvent), whose finish begins it (any option). Accepted: Westerly Breeze:
// a trade of exactly the Bowl of Wisdom Soup: 2532 (a progressEvent), whose finish completes it, takes the soup and
// sets The Starving's Timer to tomorrow; else one item and no gil, a food (auction categories MEAT_EGGS .. SWEETS):
// 2533 (an event), whose finish's confirmTrade takes nothing (the trade confirms no item); else nil.
// A talk: 2531 (an event).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the accepted section is registered under
// Western Adoulin. The Lua's puts ['Westerly_Breeze'] and onEventFinish straight in the section, with no
// [xi.zone.WESTERN_ADOULIN] around them: InteractionLookup:addContainer (interaction_lookup.lua:231-243) files them
// under the "zones" 'Westerly_Breeze' and 'onEventFinish', which no lookup reads, so once begun nothing of the quest
// answered and it could never be completed.
// Kept as the Lua has it: the offer begins on any option; while The Starving is available too, this 2530 and its 3005
// (both progressEvents on Westerly Breeze) tie and rotate; 2533's confirmTrade takes nothing (no tradeHas confirmed
// the food: the Lua's trade reads only the count and the auction category).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class HungerStrikes : QuestScript
    {
        public HungerStrikes() : base(QuestLog.Adoulin, "HUNGER_STRIKES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Hunger_Strikes";

        // xi.itemAHCategory
        private const int AhMeatEggs = 52;
        private const int AhSweets = 57;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
                Exp = 1000,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.WesterlyBreeze).OnTrigger(QuestReply.ProgressEvent(2530))
                .OnEventFinish(Zone.WesternAdoulin, 2530, q =>
                {
                    q.Quest.Begin();
                });

            // the Lua's accepted section, under Western Adoulin (fixed, user ruling 2026-11-04)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.WesterlyBreeze).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.BowlOfWisdomSoup))
                    {
                        return QuestReply.ProgressEvent(2532);
                    }
                    else if (q.Trade.ItemCount == 1 && q.Trade.Gil == 0)
                    {
                        int auctionCategory = q.Trade.ItemAhCategory(0);

                        if (auctionCategory >= AhMeatEggs && auctionCategory <= AhSweets)
                        {
                            return QuestReply.Event(2533);
                        }
                    }

                    return null;
                })
                .OnTrigger(QuestReply.Event(2531))
                .OnEventFinish(Zone.WesternAdoulin, 2532, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        q.Player.SetQuestVar(QuestLog.Adoulin, "THE_STARVING", "Timer", q.Player.VanadielUniqueDay + 1);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 2533, q =>
                {
                    q.Trade.Confirm();
                });
        }
    }
}
