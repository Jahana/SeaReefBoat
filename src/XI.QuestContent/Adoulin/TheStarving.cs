// The Starving (Log ID 9, Quest ID 84), a line-for-line port of scripts/quests/adoulin/The_Starving.lua (quest rework
// B3b Adoulin group 1). Western Adoulin: Westerly Breeze !pos 62 32 123 256 (his script:
// Always More Quoth the Ravenous and the gruel trade; default 553).
// Available, its Timer at or before today (Hunger Strikes' 2532 sets it to the day after): Westerly Breeze: 3005
// (a progressEvent), whose finish begins it (any option). Accepted: Westerly Breeze: a trade of exactly the
// Bottle of Goblin Drink: 3007 (a progressEvent), whose finish completes it, takes the drink, and sets the mustZone
// and the Timer (tomorrow) of Always More Quoth the Ravenous; else one item and no gil:
// a drink (auction category DRINKS): 3008 (an event), whose finish's confirmTrade takes nothing (the trade confirms
// no item); any other item: 3006 (an event); any other trade: 3006. A talk: 3006 (an event).
// Deliberate changes from the Lua (user ruling 2026-11-04, kept Lua bugs): the accepted section is registered under
// Western Adoulin (the Lua's has Hunger Strikes' fault, ['Westerly_Breeze'] and onEventFinish with no zone key, so it
// never registered, nothing of the quest answered once begun and it could never be completed), and 3007's finish
// takes the drink (the Lua's has no confirmTrade).
// Kept as the Lua has it: the offer begins on any option; while Hunger Strikes is available too, its 2530 and this
// 3005 (both progressEvents on Westerly Breeze) tie and rotate; 3008's confirmTrade takes nothing (no tradeHas
// confirmed the drink: the Lua's trade reads only the count and the auction category).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class TheStarving : QuestScript
    {
        public TheStarving() : base(QuestLog.Adoulin, "THE_STARVING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/The_Starving";

        // xi.itemAHCategory
        private const int AhDrinks = 58;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Exp = 1000,
                Bayld = 500,
            });

            b.State(QuestStatus.Available, when: q => q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.WesterlyBreeze).OnTrigger(QuestReply.ProgressEvent(3005))
                .OnEventFinish(Zone.WesternAdoulin, 3005, q =>
                {
                    q.Quest.Begin();
                });

            // the Lua's accepted section, under Western Adoulin (fixed, user ruling 2026-11-04)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.WesterlyBreeze).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.BottleOfGoblinDrink))
                    {
                        return QuestReply.ProgressEvent(3007);
                    }
                    else if (q.Trade.ItemCount == 1 && q.Trade.Gil == 0)
                    {
                        int auctionCategory = q.Trade.ItemAhCategory(0);

                        if (auctionCategory == AhDrinks)
                        {
                            return QuestReply.Event(3008);
                        }
                        else
                        {
                            return QuestReply.Event(3006);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(3006);
                    }
                })
                .OnTrigger(QuestReply.Event(3006))
                .OnEventFinish(Zone.WesternAdoulin, 3007, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // the drink taken: the Lua's 3007 had no confirmTrade (fixed, user ruling 2026-11-04)
                        q.Trade.Confirm();

                        q.Player.SetQuestMustZone(QuestLog.Adoulin, "ALWAYS_MORE_QUOTH_THE_RAVENOUS");
                        q.Player.SetQuestVar(QuestLog.Adoulin, "ALWAYS_MORE_QUOTH_THE_RAVENOUS", "Timer",
                            q.Player.VanadielUniqueDay + 1);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 3008, q =>
                {
                    q.Trade.Confirm();
                });
        }
    }
}
