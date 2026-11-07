// Reap What You Sow (Log ID 2, Quest ID 29), ported from scripts/zones/Windurst_Waters/npcs/Mashuu-Ajuu.lua (quest
// rework B3b, Windurst group 25; call-only: every branch of his script is this quest's, so his script only calls it).
// Mashuu-Ajuu : !pos 129 -6 167 238. He has a default action (429), which takes turns with his script. Trigger:
// available: math.randomInt(1, 2) 1: 463 (0, Sobbing Fungus, Bag of Herb Seeds), 2: nothing. Accepted: 1: 464 (the
// same), 2: 476. Completed with needToZone: 478. Completed, no needToZone, QuestReapSow_var 0: 1: 479 (the same), 2:
// nothing. Completed, var 1: 1: 464, 2: 476. Trade, accepted or completed, one item and no gil: a Sobbing Fungus: 475
// (500, Stationery Set); a Deathball: 477 (700). Finishes: 463 / 479 option 3 with no free slot:
// ITEM_CANNOT_BE_OBTAINED (the seeds). 463 option 3: begun, the seeds (ITEM_OBTAINED). 475 / 477 while accepted with no
// free slot: ITEM_CANNOT_BE_OBTAINED (the Stationery Set). 475 / 477: 500 / 700 x GIL_RATE gil (no message), the trade
// taken, needToZone; accepted: completed, Windurst fame 10, the Stationery Set (ITEM_OBTAINED); completed: fame 10, var
// 0. 479 option 3: var 1, the seeds. Kept as the Lua has it: the offers and the repeat play only on one roll in two
// (the other plays nothing); the event arguments 500 / 700 are literals (the pay is x GIL_RATE); a fungus or a
// Deathball is taken at any var once completed (no repeat accepted needed), and 477's comment's "Stationary Set" is
// given only on the first completion, as 475's.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ReapWhatYouSow : QuestScript
    {
        const string Var = "QuestReapSow_var";

        public ReapWhatYouSow() : base(QuestLog.Windurst, "REAP_WHAT_YOU_SOW") { }

        // xi.settings.main.GIL_RATE * amount, read by the engine's integral getter (llround)
        static int Pay(IQuestContext q, int amount) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * amount, MidpointRounding.AwayFromZero);

        // 475 / 477's finish after the free-slot check
        static void TurnIn(IQuestContext q, int amount)
        {
            q.Player.AddGil(Pay(q, amount));
            q.Trade.Complete();
            q.Player.SetNeedToZone(true);

            if (q.Quest.Status == QuestStatus.Accepted)
            {
                // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                // Quest[2][29] vars
                q.Player.CompleteQuest(QuestLog.Windurst, "REAP_WHAT_YOU_SOW");
                q.Player.AddFame(FameArea.Windurst, 10);
                q.Player.AddItem(Item.StationerySet);
                q.Text.Special(Text.WindurstWaters.ITEM_OBTAINED, Item.StationerySet);
            }
            else if (q.Quest.Status == QuestStatus.Completed)
            {
                q.Player.AddFame(FameArea.Windurst, 10);
                q.Player.SetCharVar(Var, 0);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MashuuAjuu).Called()
                    // Mashuu-Ajuu.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status != QuestStatus.Available && // reapstatus >= 1
                            q.Trade.ItemCount == 1 &&
                            q.Trade.Gil == 0)
                        {
                            if (q.Trade.HasItemQty(Item.SobbingFungus, 1))
                            {
                                return QuestReply.Event(475, 500, Item.StationerySet); // the fungus
                            }
                            else if (q.Trade.HasItemQty(Item.Deathball, 1))
                            {
                                return QuestReply.Event(477, 700); // the Deathball
                            }
                        }

                        return null;
                    })
                    // Mashuu-Ajuu.lua onTrigger
                    .OnTrigger(q =>
                    {
                        QuestStatus reapstatus = q.Quest.Status;

                        if (reapstatus == QuestStatus.Available)
                        {
                            if (q.Player.RandomInt(1, 2) == 1)
                            {
                                return QuestReply.Event(463, 0, Item.SobbingFungus, Item.BagOfHerbSeeds); // quest start
                            }
                        }
                        else if (reapstatus == QuestStatus.Accepted)
                        {
                            if (q.Player.RandomInt(1, 2) == 1)
                            {
                                return QuestReply.Event(464, 0, Item.SobbingFungus, Item.BagOfHerbSeeds); // reminder
                            }
                            else
                            {
                                return QuestReply.Event(476); // another conversation
                            }
                        }
                        else if (reapstatus == QuestStatus.Completed && q.Player.NeedToZone)
                        {
                            return QuestReply.Event(478); // after the quest
                        }
                        else if (reapstatus == QuestStatus.Completed &&
                            !q.Player.NeedToZone &&
                            q.Player.CharVar(Var) == 0)
                        {
                            if (q.Player.RandomInt(1, 2) == 1)
                            {
                                return QuestReply.Event(479, 0, Item.SobbingFungus, Item.BagOfHerbSeeds);
                            }
                        }
                        else if (reapstatus == QuestStatus.Completed && q.Player.CharVar(Var) == 1)
                        {
                            if (q.Player.RandomInt(1, 2) == 1)
                            {
                                return QuestReply.Event(464, 0, Item.SobbingFungus, Item.BagOfHerbSeeds);
                            }
                            else
                            {
                                return QuestReply.Event(476);
                            }
                        }

                        return null; // the other roll, or no arm: nothing plays
                    });

            // Mashuu-Ajuu.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 463, q =>
                {
                    if (q.Option == 3 && q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.WindurstWaters.ITEM_CANNOT_BE_OBTAINED, Item.BagOfHerbSeeds);
                    }
                    else if (q.Option == 3)
                    {
                        q.Quest.Begin();
                        q.Player.AddItem(Item.BagOfHerbSeeds);
                        q.Text.Special(Text.WindurstWaters.ITEM_OBTAINED, Item.BagOfHerbSeeds);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 479, q =>
                {
                    if (q.Option == 3 && q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.WindurstWaters.ITEM_CANNOT_BE_OBTAINED, Item.BagOfHerbSeeds);
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.SetCharVar(Var, 1);
                        q.Player.AddItem(Item.BagOfHerbSeeds);
                        q.Text.Special(Text.WindurstWaters.ITEM_OBTAINED, Item.BagOfHerbSeeds);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 475, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.WindurstWaters.ITEM_CANNOT_BE_OBTAINED, Item.StationerySet);
                    }
                    else
                    {
                        TurnIn(q, 500);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 477, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.WindurstWaters.ITEM_CANNOT_BE_OBTAINED, Item.StationerySet);
                    }
                    else
                    {
                        TurnIn(q, 700);
                    }
                });
        }
    }
}
