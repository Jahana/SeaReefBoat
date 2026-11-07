// Can Cardians Cry? (Log ID 2, Quest ID 47), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Apururu.lua (quest rework B3b, Windurst group 22; call-only). Apururu : !pos -11 -2
// 13 241. Her script answers The Kind Cardian (Jeuno, C#, call-only) first, then calls this quest, then plays the
// Ajido-Marujido trust event; she has a default action (274, Windurst_Woods/DefaultActions.lua), which takes turns with
// her script. Wild Card (C#) and Windurst missions 1-2, 8-2 and 9-1 answer her from their containers.
// The All-New C-3000 completed, this quest available, Windurst fame level 5: 319 (0, 20000) (its finish, any option:
// begun). Accepted: 320 (0, 20000). Completed: 330. Else her trust arm (her script). The trade (when The Kind
// Cardian's does not answer), accepted: a Bruised Starfruit among the items (npcUtil.tradeHas): 325 (0, 20000, 5000);
// else nothing. 325's finish: npcUtil.completeQuest's steps (5000 x GIL_RATE gil, GIL_OBTAINED,
// Deliverer of Tearful News, the raw player:completeQuest: no item, so it always completes; the quest's
// Quest[2][47] vars stay), then the trade confirmed.
// Kept as the Lua has it: 319 begins on any option; 325's finish reads no status; 319's and 320's 20000 and 325's 20000
// and 5000 are literals (no GIL_RATE).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CanCardiansCry : QuestScript
    {
        public CanCardiansCry() : base(QuestLog.Windurst, "CAN_CARDIANS_CRY") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Apururu.lua onTrigger (after The Kind Cardian's): The All-New C-3000 completed, available, fame level 5
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "THE_ALL_NEW_C_3000") == QuestStatus.Completed &&
                    q.Player.FameLevel(FameArea.Windurst) >= 5)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called().OnTrigger(QuestReply.Event(319, 0, 20000));

            // accepted: the reminder; the trade
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called()
                    .OnTrigger(QuestReply.Event(320, 0, 20000))
                    // Apururu.lua onTrade (after The Kind Cardian's): the starfruit among the items; else nothing
                    .OnTrade(q => q.Trade.Has(Item.BruisedStarfruit) ? QuestReply.Event(325, 0, 20000, 5000) : null);

            // completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called().OnTrigger(QuestReply.Event(330));

            // Apururu.lua onEventFinish (319, 325)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 319, q => q.Quest.Begin())
                .OnEventFinish(Zone.WindurstWoods, 325, q =>
                {
                    // npcUtil.completeQuest with gil 5000 and the title DELIVERER_OF_TEARFUL_NEWS, step for step: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[2][47] vars. No item, so it
                    // always returns true
                    int gil = RateAmount(5000, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.WindurstWoods.GIL_OBTAINED, gil);
                    q.Player.AddTitle(Title.DelivererOfTearfulNews);
                    q.Player.CompleteQuest(QuestLog.Windurst, "CAN_CARDIANS_CRY");
                    q.Trade.Confirm();
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
