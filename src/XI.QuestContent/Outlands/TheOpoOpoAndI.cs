// The Opo-Opo and I (Log ID 5, Quest ID 11), ported from the quest branches of the ten Opo-opo handlers' scripts in
// scripts/zones/Kazham/npcs/ (Lulupp, Kukupp, Mumupp, Roropp, Popopp, Bubupp, Tatapp, Kakapp, Lalapp, Nenepp) (quest
// rework B3b, Outlands group 10; call-only: each script now calls this quest; their onSpawn paths stay). Lulupp : !pos
// -26.567 -3.5 -3.544 250.
// Bathed in scent (the char var BathedInScent 1, Personal Hygiene's) and available: Lulupp's 217 (0, 483); option 1
// begins the quest. Accepted, the ten handlers want one item each, in order (k = 0 .. 9): Lulupp the Broken Mithran
// Fishing Rod, Kukupp the Workbench, Mumupp the Ten of Coins, Roropp the Sands of Silence, Popopp the Wandering Bulb,
// Bubupp the Giant Fish Bones, Tatapp the Blackened Toad, Kakapp the Wyvern Skull, Lalapp the Ancient Salt, Nenepp the
// Lucky Egg. Handler k takes a trade while OPO_OPO_PROGRESS is k or OPO_OPO_FAILED is k + 1: its item plays the good
// event (finish: progress k: the trade taken, progress k + 1, failed 0; else failed k + 2, nothing taken), one of the nine
// other items the bad event (finish: failed 1, OPO_OPO_RETRY k + 1; Lulupp's sets the retry 1 alone). A talk: retry 1 or
// more: the handler's "start over" event (Lulupp: 239 with the clue of the handler that refused, its option 1 clears the
// retry and sets failed 1); progress k or failed k + 1: the ask; progress above k or failed above k + 1: the thanks.
// After a failure the char trades the items again from Lulupp on: each handler before the one that refused plays its
// good event with failed set (nothing taken) and passes the failure on, until the handler at the char's progress takes
// its item. Nenepp's good event 241 (no progress test): with 4 free slots, the trade taken, Windurst fame +70, completed,
// the Opo-opo Crown and 3 Bunches of Pamamas (addItem, ITEM_OBTAINED), the three vars 0, the title King of the Opo-Opos
// (setTitle); else ITEM_CANNOT_BE_OBTAINED (no item). Lalapp alone answers 205 while not accepted (his script's else).
// The others' defaults (the OPO_OPO text, Kazham DefaultActions) stay in the lookup.
// Kept as the Lua has it: a trade holding the wanted item and another plays the good event (tradeComplete takes both);
// Nenepp's 241 wants 4 free slots for two items and skips the progress test; its no-room message names no item; Lulupp's
// bad event sets no failed var (his 239 sets it); event ids shared across handlers (Roropp's / Popopp's ask 210,
// Tatapp's / Nenepp's ask 212, Lalapp's / Nenepp's thanks 250) are passed as the Lua has them; the finishes read no
// status (a GM change while the event is open).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheOpoOpoAndI : QuestScript
    {
        // the ten items, by handler index (the Lua comment's list)
        private static readonly int[] Items =
        {
            Item.BrokenMithranFishingRod,     // 483  Lulupp
            Item.Workbench,                   // 22   Kukupp
            Item.TenOfCoinsCard,              // 1008 Mumupp
            Item.HandfulOfTheSandsOfSilence,  // 1157 Roropp
            Item.WanderingBulb,               // 1158 Popopp
            Item.SetOfGiantFishBones,         // 904  Bubupp
            Item.BlackenedToad,               // 4599 Tatapp
            Item.WyvernSkull,                 // 905  Kakapp
            Item.RockOfAncientSalt,           // 1147 Lalapp
            Item.LuckyEgg,                    // 4600 Nenepp
        };

        // handlers 1 .. 8: name, good / bad trade event, the "start over" / ask / thanks talk events
        private static readonly (string Npc, int Good, int Bad, int Retry, int Ask, int Thanks)[] Middle =
        {
            (Npc.Kazham.Kukupp, 220, 230, 198, 208, 243),
            (Npc.Kazham.Mumupp, 221, 231, 199, 209, 244),
            (Npc.Kazham.Roropp, 222, 232, 200, 210, 245),
            (Npc.Kazham.Popopp, 223, 233, 201, 210, 246),
            (Npc.Kazham.Bubupp, 224, 234, 202, 211, 247),
            (Npc.Kazham.Tatapp, 225, 235, 203, 212, 248),
            (Npc.Kazham.Kakapp, 226, 236, 204, 213, 249),
            (Npc.Kazham.Lalapp, 227, 237, 205, 214, 250),
        };

        public TheOpoOpoAndI() : base(QuestLog.Outlands, "THE_OPO_OPO_AND_I") { }

        // badtrade: one of the nine items the handler does not want (the Lua's or-chain)
        static bool BadTrade(IQuestTrade trade, int k)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (i != k && trade.HasItemQty(Items[i], 1))
                {
                    return true;
                }
            }

            return false;
        }

        // the trade window of handler k: progress k or failed k + 1
        static bool Turn(IQuestPlayerView player, int k) =>
            player.CharVar("OPO_OPO_PROGRESS") == k || player.CharVar("OPO_OPO_FAILED") == k + 1;

        // handlers 1 .. 9's onTrigger while accepted
        static QuestReply MiddleTrigger(IQuestPlayerView player, int k, int retryEv, int askEv, int happyEv)
        {
            int progress = player.CharVar("OPO_OPO_PROGRESS");
            int failed = player.CharVar("OPO_OPO_FAILED");
            int retry = player.CharVar("OPO_OPO_RETRY");

            if (retry >= 1)
            {
                // has failed on a later npc: the earlier successful trades are disregarded
                return QuestReply.Event(retryEv);
            }
            else if (progress == k || failed == k + 1)
            {
                return QuestReply.Event(askEv);
            }
            else if (progress >= k + 1 || failed >= k + 2)
            {
                return QuestReply.Event(happyEv);
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Lulupp.lua onTrigger: bathed in scent and available: the quest start
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("BathedInScent") == 1)
                .Npc(Zone.Kazham, Npc.Kazham.Lulupp).Called()
                    .OnTrigger(QuestReply.Event(217, 0, 483)); // 483: the Broken Mithran Fishing Rod

            // Lalapp.lua onTrigger's else: not accepted
            b.State(QuestStatus.Available)
                .Npc(Zone.Kazham, Npc.Kazham.Lalapp).Called()
                    .OnTrigger(QuestReply.Event(205));
            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.Lalapp).Called()
                    .OnTrigger(QuestReply.Event(205));

            var accepted = b.State(QuestStatus.Accepted);

            // Lulupp.lua onTrade / onTrigger (handler 0)
            accepted
                .Npc(Zone.Kazham, Npc.Kazham.Lulupp).Called()
                    .OnTrade(q =>
                    {
                        if (Turn(q.Player, 0))
                        {
                            if (q.Trade.HasItemQty(Item.BrokenMithranFishingRod, 1))
                            {
                                // first or second time trading correctly
                                return QuestReply.Event(219);
                            }
                            else if (BadTrade(q.Trade, 0))
                            {
                                return QuestReply.Event(229);
                            }
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int progress = q.Player.CharVar("OPO_OPO_PROGRESS");
                        int failed = q.Player.CharVar("OPO_OPO_FAILED");
                        int retry = q.Player.CharVar("OPO_OPO_RETRY");

                        if (retry == 1)
                        {
                            // gave the 1st npc a wrong item instead of the Broken Mithran Fishing Rod
                            return QuestReply.Event(239);
                        }
                        else if (retry >= 2 && retry <= 10)
                        {
                            // gave the retry-th npc a wrong item: the Lua's ten elseif arms, 239 (0, 0, retry - 1)
                            return QuestReply.Event(239, 0, 0, retry - 1);
                        }
                        else if (progress == 0 || failed == 1)
                        {
                            return QuestReply.Event(207); // asking for the rod
                        }
                        else if (progress >= 1 || failed >= 2)
                        {
                            return QuestReply.Event(242); // happy with the rod
                        }

                        return null;
                    });

            // Kukupp .. Lalapp (handlers 1 .. 8): onTrade / onTrigger
            for (int i = 0; i < Middle.Length; i++)
            {
                int k = i + 1;
                var h = Middle[i];

                accepted
                    .Npc(Zone.Kazham, h.Npc).Called()
                        .OnTrade(q =>
                        {
                            if (Turn(q.Player, k))
                            {
                                if (q.Trade.HasItemQty(Items[k], 1))
                                {
                                    return QuestReply.Event(h.Good);
                                }
                                else if (BadTrade(q.Trade, k))
                                {
                                    return QuestReply.Event(h.Bad);
                                }
                            }

                            return null;
                        })
                        .OnTrigger(q => MiddleTrigger(q.Player, k, h.Retry, h.Ask, h.Thanks));
            }

            // Nenepp.lua onTrade / onTrigger (handler 9)
            accepted
                .Npc(Zone.Kazham, Npc.Kazham.Nenepp).Called()
                    .OnTrade(q =>
                    {
                        if (Turn(q.Player, 9))
                        {
                            if (q.Trade.HasItemQty(Item.LuckyEgg, 1))
                            {
                                return QuestReply.Event(241);
                            }
                            else if (BadTrade(q.Trade, 9))
                            {
                                return QuestReply.Event(238);
                            }
                        }

                        return null;
                    })
                    .OnTrigger(q => MiddleTrigger(q.Player, 9, 206, 212, 250));

            // the scripts' onEventFinish
            var finishes = b.AnyState()
                .OnEventFinish(Zone.Kazham, 217, q =>
                {
                    if (q.Option == 1)
                    {
                        // Opo Opo and I quest start cs
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Kazham, 219, q =>
                {
                    if (q.Player.CharVar("OPO_OPO_PROGRESS") == 0)
                    {
                        // failed is left as it is (the middle handlers' finishes set it 0): after a replay from failed 1 the
                        // rod's talk still asks, 207
                        q.Trade.Complete();
                        q.Player.SetCharVar("OPO_OPO_PROGRESS", 1);
                    }
                    else
                    {
                        q.Player.SetCharVar("OPO_OPO_FAILED", 2);
                    }
                })
                .OnEventFinish(Zone.Kazham, 229, q =>
                {
                    // traded a wrong item: saving the current progress so as not to take the items up to this point
                    q.Player.SetCharVar("OPO_OPO_RETRY", 1);
                })
                .OnEventFinish(Zone.Kazham, 239, q =>
                {
                    if (q.Option == 1)
                    {
                        // traded wrong to another npc: a clue
                        q.Player.SetCharVar("OPO_OPO_RETRY", 0);
                        q.Player.SetCharVar("OPO_OPO_FAILED", 1);
                    }
                });

            for (int i = 0; i < Middle.Length; i++)
            {
                int k = i + 1;
                var h = Middle[i];

                finishes
                    .OnEventFinish(Zone.Kazham, h.Good, q =>
                    {
                        // correct trade, onto the next opo
                        if (q.Player.CharVar("OPO_OPO_PROGRESS") == k)
                        {
                            q.Trade.Complete();
                            q.Player.SetCharVar("OPO_OPO_PROGRESS", k + 1);
                            q.Player.SetCharVar("OPO_OPO_FAILED", 0);
                        }
                        else
                        {
                            q.Player.SetCharVar("OPO_OPO_FAILED", k + 2);
                        }
                    })
                    .OnEventFinish(Zone.Kazham, h.Bad, q =>
                    {
                        // wrong trade: restart at the first opo
                        q.Player.SetCharVar("OPO_OPO_FAILED", 1);
                        q.Player.SetCharVar("OPO_OPO_RETRY", k + 1);
                    });
            }

            finishes
                .OnEventFinish(Zone.Kazham, 241, q =>
                {
                    // correct trade: the quest finished, the Opo-opo Crown and 3 pamamas
                    if (q.Player.FreeSlotCount >= 4)
                    {
                        q.Trade.Complete();
                        q.Player.AddFame(FameArea.Windurst, 70);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][11] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "THE_OPO_OPO_AND_I");
                        q.Player.AddItem(Item.OpoOpoCrown);
                        q.Text.Special(Text.Kazham.ITEM_OBTAINED, Item.OpoOpoCrown);
                        q.Player.AddItem(Item.BunchOfPamamas, 3);
                        q.Text.Special(Text.Kazham.ITEM_OBTAINED, Item.BunchOfPamamas, 3);
                        q.Player.SetCharVar("OPO_OPO_PROGRESS", 0);
                        q.Player.SetCharVar("OPO_OPO_FAILED", 0);
                        q.Player.SetCharVar("OPO_OPO_RETRY", 0);
                        q.Player.SetTitle(Title.KingOfTheOpoOpos);
                    }
                    else
                    {
                        q.Text.Special(Text.Kazham.ITEM_CANNOT_BE_OBTAINED);
                    }
                })
                .OnEventFinish(Zone.Kazham, 238, q =>
                {
                    // wrong trade: restart at the first opo
                    q.Player.SetCharVar("OPO_OPO_FAILED", 1);
                    q.Player.SetCharVar("OPO_OPO_RETRY", 10);
                });
        }
    }
}
