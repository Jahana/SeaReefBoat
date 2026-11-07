// Fistful of Fury (Log ID 3, Quest ID 41), ported from the Fistful of Fury branches of
// scripts/zones/Lower_Jeuno/npcs/Vola.lua (quest rework B3b, Jeuno group 18; call-only: her script now calls this quest
// at the branches' spot). Vola : !pos 43 3 -45 245.
// Vola, her first arms: Norg fame level 3 or more, available and Silence of the Rams (Bastok) completed: 216 (its finish,
// option 1: begun); accepted: 215. Then Beat Around the Bushin's arms (its own C#), then her 212. Her trade, accepted: a
// Nue Fang, a Morbolger Vine and a Dodo Skin, nothing else (gil counts as one item in trade:getItemCount()): 213 (its
// finish: no free slot: ITEM_CANNOT_BE_OBTAINED; else Brown Belt (the title), a Brown Belt (addItem, ITEM_OBTAINED),
// Norg fame +30, the trade taken, completed).
// Kept as the Lua has it: 216 begins on option 1 only; no finish reads the status (216 over a completed quest calls
// addQuest; 213 completes it from any status).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class FistfulOfFury : QuestScript
    {
        public FistfulOfFury() : base(QuestLog.Jeuno, "FISTFUL_OF_FURY") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Vola.lua onTrigger: Norg fame level 3 or more, available, Silence of the Rams completed: Start Quest "Fistful
            // of Fury"
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Norg) >= 3 &&
                                                      q.Player.QuestStatus(QuestLog.Bastok, "SILENCE_OF_THE_RAMS") == QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Vola).Called().OnTrigger(QuestReply.Event(216));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Vola).Called()
                    // Vola.lua onTrigger, accepted: During Quest "Fistful of Fury"
                    .OnTrigger(QuestReply.Event(215))
                    // Vola.lua onTrade, accepted: the three items and nothing else: Finish Quest "Fistful of Fury"
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.NueFang, 1) &&
                            q.Trade.HasItemQty(Item.MorbolgerVine, 1) &&
                            q.Trade.HasItemQty(Item.DodoSkin, 1) &&
                            q.Trade.ItemCount == 3)
                        {
                            return QuestReply.Event(213);
                        }

                        return null;
                    });

            b.AnyState()
                // Vola.lua onEventFinish 216 / 213
                .OnEventFinish(Zone.LowerJeuno, 216, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 213, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.LowerJeuno.ITEM_CANNOT_BE_OBTAINED, Item.BrownBelt);
                    }
                    else
                    {
                        q.Player.AddTitle(Title.BrownBelt);
                        q.Player.AddItem(Item.BrownBelt);
                        q.Text.Special(Text.LowerJeuno.ITEM_OBTAINED, Item.BrownBelt);
                        q.Player.AddFame(FameArea.Norg, 30);
                        q.Trade.Complete();
                        // the raw player:completeQuest, which keeps the quest's own Quest[3][41] vars
                        q.Player.CompleteQuest(QuestLog.Jeuno, "FISTFUL_OF_FURY");
                    }
                });
        }
    }
}
