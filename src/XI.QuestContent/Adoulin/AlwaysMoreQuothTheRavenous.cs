// Always More Quoth the Ravenous (Adoulin 88), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Westerly_Breeze.lua (quest rework B3b Adoulin group 5, call-only).
// Westerly Breeze !pos 62 32 123 256 (default 553; the group 1 quests Hunger Strikes and The Starving hold him too).
// His script calls it (xi.cs.quest / questEvent), and All the Way to the Bank (AllTheWayToTheBank.cs) for the gruel
// trade (ATWTTB_Can_Trade_Gruel, 5068).
// Talk, at Adoulin fame level 2 or more, not needing to zone and past the day in Westerly_Breeze_Wait: at
// fame level 3 or more and not completed: available 3010, else (accepted) 3011. 3010's finish: player:addQuest. Trade
// of one item and no gil: a drink (AH category 58) while accepted: the Goblin Drink 3013 (he refuses it),
// another 3014 (he drinks it); an item neither food (52-57) nor drink: the Cursed Beverage while accepted 3012. 3012's
// finish: tradeComplete, the raw player:completeQuest, 1500 exp and 1000 bayld (at the rates),
// BAYLD_OBTAINED, 30 Adoulin fame, Westerly_Breeze_Wait 0. 3014's finish: tradeComplete.
// Kept as the script has it: the raw completeQuest with its own rewards (not npcUtil.completeQuest); the wait var is
// set by nothing but cleared here; the reminder 3011 reads any status but available and completed; the
// fame level 2 test is the outer one of a level 3 test.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class AlwaysMoreQuothTheRavenous : QuestScript
    {
        public AlwaysMoreQuothTheRavenous() : base(QuestLog.Adoulin, "ALWAYS_MORE_QUOTH_THE_RAVENOUS") { }

        // xi.itemAHCategory
        private const int AhMeatEggs = 52;
        private const int AhSweets = 57;
        private const int AhDrinks = 58;

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.WesterlyBreeze).Called().OnTrade(q =>
                {
                    QuestStatus amqtr = q.Quest.Status;

                    if (q.Trade.ItemCount == 1 && q.Trade.Gil == 0)
                    {
                        // trade:getItem(0):getID() and :getAHCat() (the script's `if not item` cannot fail: one item)
                        int itemId = q.Trade.ItemId(0);
                        int ahCategory = q.Trade.ItemAhCategory(0);

                        if (ahCategory >= AhMeatEggs && ahCategory <= AhSweets)
                        {
                            // a food: the script's gruel branch (All the Way to the Bank's call), not this quest's
                            return null;
                        }
                        else if (ahCategory == AhDrinks)
                        {
                            if (amqtr == QuestStatus.Accepted)
                            {
                                if (itemId == Item.BottleOfGoblinDrink)
                                {
                                    // We gave him another Goblin Drink.
                                    // Special event where he refuses it.
                                    return QuestReply.Event(3013);
                                }
                                else
                                {
                                    // Special event where he drinks a non-goblin drink.
                                    return QuestReply.Event(3014);
                                }
                            }
                        }
                        else
                        {
                            if (itemId == Item.BottleOfCursedBeverage && amqtr == QuestStatus.Accepted)
                            {
                                // We gave him Cursed Beverage.
                                // Finishes Quest: 'Always More Quoth the Ravenous'
                                return QuestReply.Event(3012);
                            }
                        }
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    QuestStatus amqtr = q.Quest.Status;

                    if (q.Player.FameLevel(FameArea.Adoulin) >= 2 &&
                        !q.Player.NeedToZone && q.Player.VanadielUniqueDay > q.Player.CharVar("Westerly_Breeze_Wait"))
                    {
                        if (amqtr != QuestStatus.Completed &&
                            q.Player.FameLevel(FameArea.Adoulin) >= 3)
                        {
                            if (amqtr == QuestStatus.Available)
                            {
                                // Starts Quest: 'Always More Quoth the Ravenous'
                                return QuestReply.Event(3010);
                            }
                            else
                            {
                                // Reminder for Quest: 'Always More Quoth the Ravenous'
                                return QuestReply.Event(3011);
                            }
                        }
                    }

                    return null;
                });

            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 3010, q =>
                {
                    // Starting Quest: 'Always More Quoth the Ravenous'
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.WesternAdoulin, 3012, q =>
                {
                    // Finishing Quest: 'Always More Quoth The Ravenous'
                    q.Trade.Complete();
                    q.Player.CompleteQuest(QuestLog.Adoulin, "ALWAYS_MORE_QUOTH_THE_RAVENOUS");
                    q.Player.AddExp(RateAmount(1500, q.Settings.MainNumber("EXP_RATE")));
                    int bayld = RateAmount(1000, q.Settings.MainNumber("BAYLD_RATE"));
                    q.Player.AddCurrency("bayld", bayld);
                    q.Text.Special(Text.WesternAdoulin.BAYLD_OBTAINED, bayld);

                    // TODO: Verify fame value added
                    q.Player.AddFame(FameArea.Adoulin, 30);
                    q.Player.SetCharVar("Westerly_Breeze_Wait", 0);
                })
                .OnEventFinish(Zone.WesternAdoulin, 3014, q =>
                {
                    // Consuming wrong food item given to him during his quests
                    q.Trade.Complete();
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
