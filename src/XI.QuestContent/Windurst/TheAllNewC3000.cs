// The All-New C-3000 (Log ID 2, Quest ID 18), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Kopuro-Popuro.lua (quest rework B3b, Windurst group 22; call-only). Kopuro-Popuro :
// !pos -0.037 -4.749 -22.589 241. His talk calls this quest first, his trade last (after The All-New C-2000 and
// Legendary Plan B).
// Legendary Plan B completed, this quest available, Windurst fame level 4: needToZone 316 (Legendary Plan B's after
// text), else 655 (0, Beetle Shell, Hecteyes Eye) (its finish, any option: begun). Completed: 659 (the same). Accepted:
// no arm here (Legendary Plan B's completed 316 answers). The trade, accepted or completed: a Beetle Shell and a
// Hecteyes Eye among the items (npcUtil.tradeHas): 657 (the same); else 656 (the same). 657's finish:
// npcUtil.completeQuest's steps (600 x GIL_RATE gil, GIL_OBTAINED; the raw player:completeQuest, so the quest's
// Quest[2][18] vars stay); the trade confirmed.
// Kept as the Lua has it: the reward table names fame 40 but no fameArea, so npcUtil.completeQuest gives no fame; the
// trade answers completed chars too, and 657 then completes again and pays 600 gil again; 655 begins on any option.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheAllNewC3000 : QuestScript
    {
        public TheAllNewC3000() : base(QuestLog.Windurst, "THE_ALL_NEW_C_3000") { }

        static bool Offered(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Windurst, "LEGENDARY_PLAN_B") == QuestStatus.Completed &&
            q.Player.FameLevel(FameArea.Windurst) >= 4;

        protected override void Build(QuestScriptBuilder b)
        {
            // Kopuro-Popuro.lua onTrigger: Legendary Plan B completed, available, fame level 4: needToZone: its after
            // text
            b.State(QuestStatus.Available, when: q => Offered(q) && q.Player.NeedToZone)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called().OnTrigger(QuestReply.Event(316));

            // ... else the offer
            b.State(QuestStatus.Available, when: Offered)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrigger(QuestReply.Event(655, 0, Item.BeetleShell, Item.HecteyesEye));

            // Kopuro-Popuro.lua onTrade (last): accepted or completed: the two among the items: the completion
            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrade(q => q.Trade.Has(Item.BeetleShell, Item.HecteyesEye)
                        ? QuestReply.Event(657, 0, Item.BeetleShell, Item.HecteyesEye)
                        : QuestReply.Event(656, 0, Item.BeetleShell, Item.HecteyesEye));

            // completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrigger(QuestReply.Event(659, 0, Item.BeetleShell, Item.HecteyesEye));

            // Kopuro-Popuro.lua onEventFinish (655, 657)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 655, q => q.Quest.Begin())
                .OnEventFinish(Zone.WindurstWoods, 657, q =>
                {
                    // npcUtil.completeQuest with fame 40 and gil 600, step for step: not quest:complete, whose cleanup
                    // would clear the quest's own Quest[2][18] vars. No fameArea, so no fame; no item, so it always
                    // returns true
                    int gil = RateAmount(600, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.WindurstWoods.GIL_OBTAINED, gil);
                    q.Player.CompleteQuest(QuestLog.Windurst, "THE_ALL_NEW_C_3000");
                    q.Trade.Confirm();
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
