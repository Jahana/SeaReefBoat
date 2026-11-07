// All the Way to the Bank (Log ID 9, Quest ID 103), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Defliaa.lua, Hujette.lua, Flapano.lua and Westerly_Breeze.lua (the no-log quests,
// call-only). Defliaa !pos 43 2 -113 256, Hujette !pos 35 0 -56 256, Flapano !pos 70 0 -13 256 (Exotic Delicacies and
// The Weatherspoon War hold him too), Westerly Breeze !pos 62 32 123 256 (default 553; Always More Quoth the Ravenous,
// Hunger Strikes and The Starving hold him too).
// The scripts never read or write the quest's status: the steps are the char vars ATWTTB_Payments (a bit per payment)
// and ATWTTB_Can_Trade_Gruel. No script gives the TARUTARU_SAUCE_INVOICE or sets ATWTTB_Can_Trade_Gruel 1, and none
// takes the TARUTARU_SAUCE_RECEIPT.
// Trades with the invoice: Defliaa 19440 gil with bit 0 clear: 5069; Hujette 3000 gil with bit 1 clear: 5070; Flapano
// 5600 gil: with bit 2 clear 5071, else nothing (his Exotic Delicacies trade not reached). Each finish: the trade
// taken, the payment's bit set (Defliaa 0, Hujette and Flapano 2), and with bits 0 to 4 set the receipt
// (npcUtil.giveKeyItem). Westerly Breeze, a trade of one item and no gil, a food (AH 52 to 57), at Can_Trade_Gruel 1:
// the Bowl of Vegetable Gruel 5068, the Bowl of Medicinal Gruel 5068 with 1; its finish: tradeComplete, 19716 gil
// (option 1: 39432) by npcUtil.giveCurrency, Can_Trade_Gruel 0.
// Stay Lua: the three shops (their talks). Kept as the scripts have it: Hujette tests bit 1 and sets bit 2, so she
// takes her 3000 gil again at each trade and her receipt arm cannot be reached; Flapano's arm with bit 2 set matches
// the trade and plays nothing (Exotic Delicacies not asked); 5068's gil follows the finish's option, not the gruel.
// Deliberate change (user ruling 2026-11-04; the scripts never wrote the log): a payment's finish adds the quest while
// it is available (the first write of ATWTTB_Payments), and the receipt completes it while it is not completed (the
// scripts' end state: nothing takes the receipt, and Westerly Breeze's gruel is "after completing" the quest). Raw
// player:addQuest / player:completeQuest: no reward, no quest:cleanup.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class AllTheWayToTheBank : QuestScript
    {
        const string Payments = "ATWTTB_Payments";
        const string CanTradeGruel = "ATWTTB_Can_Trade_Gruel";

        // xi.itemAHCategory
        private const int AhMeatEggs = 52;
        private const int AhSweets = 57;

        // xi.item.BOWL_OF_MEDICINAL_GRUEL: no Item constant (the script names the id)
        private const int BowlOfMedicinalGruel = 4534;

        public AllTheWayToTheBank() : base(QuestLog.Adoulin, "ALL_THE_WAY_TO_THE_BANK") { }

        // utils.mask.getBit(mask, pos)
        private static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        // the payment's finish: player:confirmTrade(), utils.mask.setBit(ATWTTB_Payments, bit, true), and the receipt
        // once utils.mask.isFull(ATWTTB_Payments, 5); the deliberate log writes (the begin, the completion)
        private static void Pay(IQuestContext q, int bit)
        {
            q.Trade.Confirm();
            q.Player.SetCharVar(Payments, q.Player.CharVar(Payments) | (1 << bit));
            if (q.Quest.Status == QuestStatus.Available)
            {
                q.Player.AddQuest(QuestLog.Adoulin, "ALL_THE_WAY_TO_THE_BANK"); // deliberate: the log entry
            }
            if ((q.Player.CharVar(Payments) & 0x1F) == 0x1F)
            {
                q.Player.GiveKeyItem(KeyItem.TarutaruSauceReceipt);
                if (q.Quest.Status != QuestStatus.Completed)
                {
                    q.Player.CompleteQuest(QuestLog.Adoulin, "ALL_THE_WAY_TO_THE_BANK"); // deliberate: the log's end
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Defliaa.lua onTrade (the whole of it)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Defliaa).Called().OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TarutaruSauceInvoice))
                    {
                        bool paidDefliaa = GetBit(q.Player.CharVar(Payments), 0);

                        if (!paidDefliaa && q.Trade.HasWithGil(19440))
                        {
                            return QuestReply.Event(5069);
                        }
                    }

                    return null;
                })
                // Hujette.lua onTrade (the whole of it)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Hujette).Called().OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TarutaruSauceInvoice))
                    {
                        bool paidHujette = GetBit(q.Player.CharVar(Payments), 1);
                        if (!paidHujette && q.Trade.HasWithGil(3000))
                        {
                            return QuestReply.Event(5070);
                        }
                    }

                    return null;
                })
                // Flapano.lua onTrade: the first arm (Exotic Delicacies' call is its else)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Flapano).Called().OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TarutaruSauceInvoice) &&
                        q.Trade.HasWithGil(5600))
                    {
                        bool paidFlapano = GetBit(q.Player.CharVar(Payments), 2);
                        if (!paidFlapano)
                        {
                            return QuestReply.Event(5071);
                        }

                        return QuestReply.NoAction(); // the arm taken: no Exotic Delicacies trade
                    }

                    return null;
                })
                // Westerly_Breeze.lua onTrade: the food arm
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.WesterlyBreeze).Called().OnTrade(q =>
                {
                    if (q.Trade.ItemCount == 1 && q.Trade.Gil == 0)
                    {
                        // trade:getItem(0):getID() and :getAHCat() (the script's `if not item` cannot fail: one item)
                        int itemId = q.Trade.ItemId(0);
                        int ahCategory = q.Trade.ItemAhCategory(0);

                        if (ahCategory >= AhMeatEggs && ahCategory <= AhSweets)
                        {
                            // We traded him a food item
                            if (q.Player.CharVar(CanTradeGruel) == 1 &&
                                (itemId == Item.BowlOfVegetableGruel || itemId == BowlOfMedicinalGruel))
                            {
                                if (itemId == Item.BowlOfVegetableGruel)
                                {
                                    // Trading him Vegetable Gruel after completing Quest: 'All The Way To The Bank'
                                    return QuestReply.Event(5068);
                                }
                                else
                                {
                                    // Trading him Medicinal Gruel after completing Quest: 'All The Way To The Bank'
                                    return QuestReply.Event(5068, 1);
                                }
                            }
                        }
                    }

                    return null;
                });

            b.AnyState()
                // Defliaa.lua, Hujette.lua, Flapano.lua onEventFinish 5069, 5070, 5071
                .OnEventFinish(Zone.WesternAdoulin, 5069, q => Pay(q, 0))
                .OnEventFinish(Zone.WesternAdoulin, 5070, q => Pay(q, 2)) // bit 2, as Hujette.lua has it
                .OnEventFinish(Zone.WesternAdoulin, 5071, q => Pay(q, 2))
                // Westerly_Breeze.lua onEventFinish 5068
                .OnEventFinish(Zone.WesternAdoulin, 5068, q =>
                {
                    // Trading him gruel after Quest: 'All The Way To The Bank'
                    q.Trade.Complete();
                    int gilObtained = 19716;
                    if (q.Option == 1)
                    {
                        gilObtained = 39432;
                    }

                    q.Player.GiveCurrency("gil", gilObtained);
                    q.Player.SetCharVar(CanTradeGruel, 0);
                });
        }
    }
}
