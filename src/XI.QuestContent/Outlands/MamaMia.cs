// Mama Mia (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Mamaulabion.lua (quest rework B3b,
// Outlands group 6; call-only: the script now calls this quest). Mamaulabion : !pos -57 -9 68 252.
// Mamaulabion, in the script's order: available, Norg fame level 4 and Windurst's The Moonlit Path completed: 191 (its
// finish begins the quest); accepted: the seven items given (the char var tradesMamaMia, bits 0-6): before MamaMia_date:
// 196, else 197; else 192; completed with the Evoker's Ring: 198; completed without it: 243 (its finish on 1: the quest
// taken off and begun again). The script keeps its 93 (the standard talk) otherwise. Her trade, accepted, exactly one of
// the items 1202-1208 (Bubbly Water ... Ancient's Key): already given: 194 (the item); else its bit set: all seven: 195,
// else 193 (the item). 193's finish: the trade confirmed; 195's: confirmed, MamaMia_date = JstMidnight(); 197's: no
// free slot: ITEM_CANNOT_BE_OBTAINED (Evoker's Ring), else the ring (addItem, ITEM_OBTAINED), Norg fame +40, the quest
// completed, tradesMamaMia 0.
// Kept as the Lua has it: the trade sets the bit before the event's finish (the item is taken only by the confirm); 194
// has no finish (the item is not taken); 243's restart keeps tradesMamaMia (0 since 197) and MamaMia_date; the loop
// tests each item with tradeHasExactly in turn (each test confirms what it matched).
using XI.Quests;
using XI.Quests.Authoring;
using BastokWildcat = XI.QuestContent.Bastok.LureOfTheWildcat;

namespace XI.QuestContent.Outlands
{
    public sealed class MamaMia : QuestScript
    {
        public MamaMia() : base(QuestLog.Outlands, "MAMA_MIA") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Mamaulabion.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Norg) >= 4 &&
                    q.Player.QuestStatus(QuestLog.Windurst, "THE_MOONLIT_PATH") == QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Mamaulabion).Called().OnTrigger(QuestReply.Event(191)); // Start Quest 'Mama Mia'

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Mamaulabion).Called()
                    .OnTrigger(q =>
                    {
                        int tradesMamaMia = q.Player.CharVar("tradesMamaMia");

                        if (BastokWildcat.IsFull(tradesMamaMia, 7))
                        {
                            if (q.Player.SystemTime < q.Player.CharVar("MamaMia_date"))
                            {
                                return QuestReply.Event(196); // need to wait longer for reward
                            }
                            else
                            {
                                return QuestReply.Event(197); // Reward
                            }
                        }
                        else
                        {
                            return QuestReply.Event(192); // During Quest "Mama Mia"
                        }
                    })
                    // Mamaulabion.lua onTrade: check whether trade is an item with id 1202 to 1208
                    .OnTrade(q =>
                    {
                        int tradedItem = 0;
                        int bitToSet = 0;
                        for (int i = Item.BottleOfBubblyWater; i <= Item.AncientsKey; i++)
                        {
                            if (q.Trade.HasExactly(i))
                            {
                                tradedItem = i;
                                bitToSet = i - Item.BottleOfBubblyWater;
                                break;
                            }
                        }

                        if (tradedItem != 0)
                        {
                            int mask = q.Player.CharVar("tradesMamaMia");
                            bool wasSet = (mask & (1 << bitToSet)) != 0;

                            if (wasSet)
                            {
                                return QuestReply.Event(194, tradedItem); // Traded an item you already gave
                            }
                            else
                            {
                                mask = mask | (1 << bitToSet);
                                q.Player.SetCharVar("tradesMamaMia", mask);

                                if (BastokWildcat.IsFull(mask, 7))
                                {
                                    return QuestReply.Event(195, tradedItem); // Traded all seven items
                                }
                                else
                                {
                                    return QuestReply.Event(193, tradedItem); // Traded an item
                                }
                            }
                        }

                        return null;
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Mamaulabion).Called().OnTrigger(q =>
                {
                    if (q.Player.HasItem(Item.EvokersRing))
                    {
                        return QuestReply.Event(198); // New standard dialog after "Mama Mia" is complete
                    }
                    else
                    {
                        return QuestReply.Event(243); // Quest completed, but dropped ring
                    }
                });

            // Mamaulabion.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Norg, 191, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.Norg, 193, q =>
                {
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.Norg, 195, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetCharVar("MamaMia_date", q.Player.JstMidnight);
                })
                .OnEventFinish(Zone.Norg, 197, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.Norg.ITEM_CANNOT_BE_OBTAINED, 14625); // Evokers Ring
                    }
                    else
                    {
                        q.Player.AddItem(Item.EvokersRing); // Evokers Ring
                        q.Text.Special(Text.Norg.ITEM_OBTAINED, Item.EvokersRing); // Evokers Ring
                        q.Player.AddFame(FameArea.Norg, 40);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][131] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "MAMA_MIA");
                        q.Player.SetCharVar("tradesMamaMia", 0);
                    }
                })
                .OnEventFinish(Zone.Norg, 243, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.Outlands, "MAMA_MIA");
                        q.Quest.Begin();
                    }
                });
        }
    }
}
