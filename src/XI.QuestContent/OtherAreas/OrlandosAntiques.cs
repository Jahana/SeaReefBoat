// Orlando's Antiques (Log ID 4, Quest ID 7), ported from scripts/zones/Mhaura/npcs/Orlando.lua (quest rework B3b,
// Other Areas group 10; call-only: every branch of his script is this quest's, so his script only calls it; he has no
// DefaultActions entry). Orlando : !pos -37.268 -9 58.047 249.
// Trigger: Windurst fame level >= 2 and the Chocobo License: available: 101, else 103; else 100. 101's finish: begun.
// 103's finish, completed: the local var OrlandoRepeat 1.
// Trade, accepted or OrlandoRepeat 1: the item in the trade's first slot one of the eleven antiques: 8 of it and 8
// items in all: ANTIQUE_PAYOUT = GIL_RATE x its price, 102 (the payout, the item); fewer than 8 items: 104; else
// nothing.
// 102's finish: the trade taken, Windurst fame +10, the payout (npcUtil.giveCurrency, which multiplies it by GIL_RATE
// again), completed, ANTIQUE_PAYOUT 0, OrlandoRepeat 0.
// Kept as the Lua has it: 101 begins with no option read; 102's finish reads no status (it completes again on a
// repeat); a trade of more than 8 items, or 8 not all the slot's item, plays nothing (trade:hasItemQty is exact, so 9
// of the slot's item is not 8); the payout is GIL_RATE x the price as a Lua number, which the engine's integral
// setters round half away from zero (GIL_RATE 1.25: 187.5 is 188).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class OrlandosAntiques : QuestScript
    {
        const string PayoutVar = "ANTIQUE_PAYOUT";
        const string RepeatLocalVar = "OrlandoRepeat";

        // the item and its price (the Lua's itemList)
        static readonly (int Item, int Price)[] ItemList =
        {
            (564, 200),   // Fingernail Sack
            (565, 250),   // Teeth Sack
            (566, 200),   // Goblin Cup
            (568, 120),   // Goblin Die
            (656, 600),   // Beastcoin
            (748, 900),   // Gold Beastcoin
            (749, 800),   // Mythril Beastcoin
            (750, 750),   // Silver Beastcoin
            (898, 120),   // Chicken Bone
            (900, 100),   // Fish Bone
            (16995, 150), // Rotten Meat
        };

        public OrlandosAntiques() : base(QuestLog.OtherAreas, "ORLANDOS_ANTIQUES") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.Mhaura, Npc.Mhaura.Orlando).Called()
                    // Orlando.lua onTrade
                    .OnTrade(q =>
                    {
                        QuestStatus questStatus = q.Quest.Status;
                        int itemID = q.Trade.ItemId(0); // trade:getItemId(): the first slot's item

                        if (questStatus == QuestStatus.Accepted || q.Player.LocalVar(RepeatLocalVar) == 1)
                        {
                            foreach (var (item, price) in ItemList)
                            {
                                if (item == itemID)
                                {
                                    if (q.Trade.HasItemQty(itemID, 8) && q.Trade.ItemCount == 8)
                                    {
                                        // Correct amount, valid item.
                                        // xi.settings.main.GIL_RATE * price, as the engine's integral setters read
                                        // it (llround)
                                        int payout = (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * price,
                                                                     MidpointRounding.AwayFromZero);
                                        q.Player.SetCharVar(PayoutVar, payout);
                                        return QuestReply.Event(102, payout, itemID);
                                    }
                                    else if (q.Trade.ItemCount < 8)
                                    {
                                        // Wrong amount, but valid item.
                                        return QuestReply.Event(104);
                                    }
                                }
                            }
                        }

                        return null;
                    })
                    // Orlando.lua onTrigger
                    // TODO: Event 106 needs confirmation if/when it happens
                    .OnTrigger(q =>
                    {
                        QuestStatus questStatus = q.Quest.Status;

                        if (q.Player.FameLevel(FameArea.Windurst) >= 2 &&
                            q.Player.HasKeyItem(KeyItem.ChocoboLicense))
                        {
                            if (questStatus != QuestStatus.Available)
                            {
                                return QuestReply.Event(103);
                            }
                            else
                            {
                                return QuestReply.Event(101);
                            }
                        }
                        else
                        {
                            return QuestReply.Event(100);
                        }
                    });

            b.AnyState()
                // Orlando.lua onEventFinish 101 / 102 / 103
                .OnEventFinish(Zone.Mhaura, 101, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.Mhaura, 102, q =>
                {
                    int payout = q.Player.CharVar(PayoutVar);

                    q.Trade.Complete();
                    q.Player.AddFame(FameArea.Windurst, 10);
                    q.Player.GiveCurrency("gil", payout);
                    // the raw player:completeQuest, which keeps the quest's own Quest[4][7] vars
                    q.Player.CompleteQuest(QuestLog.OtherAreas, "ORLANDOS_ANTIQUES");
                    q.Player.SetCharVar(PayoutVar, 0);
                    q.Player.SetLocalVar(RepeatLocalVar, 0);
                })
                .OnEventFinish(Zone.Mhaura, 103, q =>
                {
                    if (q.Quest.Status == QuestStatus.Completed)
                    {
                        q.Player.SetLocalVar(RepeatLocalVar, 1);
                    }
                });
        }
    }
}
