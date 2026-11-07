// Paying Lip Service (Log ID 2, Quest ID 60), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Tapoh_Lihzeh.lua (quest rework B3b, Windurst group 24; call-only). Tapoh Lihzeh :
// !pos 51.011 -3.749 54.402 241. His script calls Chocobilious first, then this quest, else plays his standard 437.
// Accepted: 478 (0, Beehive Chip, Remi Shell, 150 x GIL_RATE, 200 x GIL_RATE); available: 477 (the same) (its finish,
// option 1: begun); completed: his 437. The trade (when Chocobilious' does not answer), accepted or completed: 3
// Beehive Chips (npcUtil.tradeHas): 479 (0, Beehive Chip, Remi Shell, 0, 0); else 2 Remi Shells: 479 (..., 0, 1). 479's
// finish: accepted: npcUtil.completeQuest's steps (Kisser Make-Upper, the raw player:completeQuest, so the quest's
// Quest[2][60] vars stay); else Windurst fame 10; then option 1: 200 gil, else 150 (npcUtil.giveCurrency); the trade
// confirmed. Kept as the Lua has it: the first completion's reward table names fame 10 but no fameArea, so it gives no
// fame (the repeats give Windurst fame 10); the gil follows the finish's option, not the items traded; 479's finish
// reads the status when it ends.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class PayingLipService : QuestScript
    {
        public PayingLipService() : base(QuestLog.Windurst, "PAYING_LIP_SERVICE") { }

        // xi.settings.main.GIL_RATE * amount (the offer's arguments), read by the engine's integral getter (llround)
        static int Pay(IQuestContext q, int amount) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * amount, MidpointRounding.AwayFromZero);

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TapohLihzeh).Called()
                    // Tapoh_Lihzeh.lua onTrade (after Chocobilious')
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status != QuestStatus.Available) // >= QUEST_ACCEPTED
                        {
                            if (q.Trade.HasQty((Item.BeehiveChip, 3)))
                            {
                                return QuestReply.Event(479, 0, Item.BeehiveChip, Item.RemiShell, 0, 0);
                            }
                            else if (q.Trade.HasQty((Item.RemiShell, 2)))
                            {
                                return QuestReply.Event(479, 0, Item.BeehiveChip, Item.RemiShell, 0, 1);
                            }
                        }

                        return null;
                    })
                    // Tapoh_Lihzeh.lua onTrigger (after Chocobilious' arms; his 437 stays in his script)
                    .OnTrigger(q =>
                    {
                        QuestStatus payingLipService = q.Quest.Status;

                        if (payingLipService == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(478, 0, Item.BeehiveChip, Item.RemiShell, Pay(q, 150), Pay(q, 200));
                        }
                        else if (payingLipService == QuestStatus.Available)
                        {
                            return QuestReply.Event(477, 0, Item.BeehiveChip, Item.RemiShell, Pay(q, 150), Pay(q, 200));
                        }

                        return null; // his standard 437
                    });

            // Tapoh_Lihzeh.lua onEventFinish (477, 479)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 477, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 479, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // npcUtil.completeQuest with fame 10 and the title KISSER_MAKE_UPPER, step for step: not
                        // quest:complete, whose cleanup would clear the quest's own Quest[2][60] vars. No fameArea, so
                        // no fame
                        q.Player.AddTitle(Title.KisserMakeUpper);
                        q.Player.CompleteQuest(QuestLog.Windurst, "PAYING_LIP_SERVICE");
                    }
                    else
                    {
                        q.Player.AddFame(FameArea.Windurst, 10);
                    }

                    if (q.Option == 1) // remi_shell
                    {
                        q.Player.GiveCurrency("gil", 200);
                    }
                    else // beehive_chip
                    {
                        q.Player.GiveCurrency("gil", 150);
                    }

                    q.Trade.Confirm();
                });
        }
    }
}
