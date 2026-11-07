// Lure of the Wildcat Invitation Cards (hidden quest Invitation_Cards), ported from
// scripts/quests/hiddenQuests/Invitation_Cards.lua (quest rework B3b hidden quests). Naja Salaheem : !pos 22.700 -8.804
// -45.591 50. The four invitation cards (key items the Lure of the Wildcat quests give) handed back to Naja. One
// section (one card or more held). Her trigger, while the local Option is 0: Prog 0: 74 (text_table 0); else 75
// (text_table 0). 74's update on 1: updateEvent(cards, the coin, the amount: 1-3 cards: 1-3 Imperial Bronze Pieces, 4:
// 1 Imperial Mythril Piece); 75's update on 1: updateEvent(cards, Imperial Bronze Piece, 1). 74's finish: the local
// Option 1; on 3: the coins (npcUtil.giveItem { { coin, amount } }), and when given: the cards taken, Prog = cards
// when fewer than 4. 75's finish: the local Option 1; on 3: one Imperial Bronze Piece, and when given: the cards taken,
// Prog + cards >= 4: quest:complete (no reward: the vars cleared), else Prog += cards. Kept as the Lua has it: four
// cards on 74 pay the Mythril Piece and leave Prog 0 (no completion); 75 always pays one Bronze Piece, whatever its
// update showed; the local Option (a char local var, never reset by the quest) silences Naja's quest talk after either
// event, finished on any option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class InvitationCards : HiddenQuestScript
    {
        public InvitationCards() : base("Invitation_Cards") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Invitation_Cards";

        private static readonly int[] InvitationCardKeyItems =
        {
            KeyItem.RedInvitationCard,
            KeyItem.BlueInvitationCard,
            KeyItem.GreenInvitationCard,
            KeyItem.WhiteInvitationCard,
        };

        // rewards[numCards]
        private static readonly (int Coin, int Amount)[] Rewards =
        {
            (0, 0),
            (Item.ImperialBronzePiece, 1),
            (Item.ImperialBronzePiece, 2),
            (Item.ImperialBronzePiece, 3),
            (Item.ImperialMythrilPiece, 1),
        };

        // getCards(player): the cards held, and their reward
        private static (int Cards, int Coin, int Amount) GetCards(IQuestPlayerView player)
        {
            int numCards = 0;

            foreach (int tempKeyItem in InvitationCardKeyItems)
            {
                if (player.HasKeyItem(tempKeyItem))
                {
                    numCards = numCards + 1;
                }
            }

            return (numCards, Rewards[numCards].Coin, Rewards[numCards].Amount);
        }

        private static void DeleteCards(IQuestPlayer player)
        {
            foreach (int tempKeyItem in InvitationCardKeyItems)
            {
                if (player.HasKeyItem(tempKeyItem))
                {
                    player.DelKeyItem(tempKeyItem);
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => GetCards(q.Player).Cards > 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (q.Quest.LocalVar("Option") == 0)
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.ProgressEvent(74).TextTable(0);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(75).TextTable(0);
                        }
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 74, q =>
                {
                    if (q.Option == 1)
                    {
                        var (cards, coin, amount) = GetCards(q.Player);

                        // player:updateEvent({ [0] = cards, [1] = coin, [2] = amount, text_table = 0 }): the table form
                        // sends the same 0x05C (text_table is not read by updateEvent)
                        q.Player.UpdateEvent(cards, coin, amount);
                    }
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 75, q =>
                {
                    if (q.Option == 1)
                    {
                        int cards = GetCards(q.Player).Cards;

                        q.Player.UpdateEvent(cards, Item.ImperialBronzePiece, 1);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 74, q =>
                {
                    q.Quest.SetLocalVar("Option", 1);

                    if (q.Option == 3)
                    {
                        var (cards, coin, amount) = GetCards(q.Player);

                        if (q.Player.GiveItems((coin, amount)))
                        {
                            DeleteCards(q.Player);

                            if (cards < 4)
                            {
                                q.Quest.SetStep(cards);
                            }
                        }
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 75, q =>
                {
                    q.Quest.SetLocalVar("Option", 1);

                    if (q.Option == 3)
                    {
                        int cards = GetCards(q.Player).Cards;

                        if (q.Player.GiveItem(Item.ImperialBronzePiece))
                        {
                            DeleteCards(q.Player);

                            int count = q.Quest.Step;
                            if (cards + count >= 4)
                            {
                                q.Quest.Complete();
                            }
                            else
                            {
                                q.Quest.SetStep(count + cards);
                            }
                        }
                    }
                });
        }
    }
}
