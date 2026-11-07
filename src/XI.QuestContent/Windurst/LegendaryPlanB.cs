// Legendary Plan B (Log ID 2, Quest ID 44), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Kopuro-Popuro.lua and Kororo.lua (quest rework B3b, Windurst group 22; call-only).
// Kopuro-Popuro : !pos -0.037 -4.749 -22.589 241; Kororo : !pos -11.883 -2.750 5.508 241. Kopuro-Popuro's talk calls
// this quest after The All-New C-3000, his trade after The All-New C-2000; Kororo's talk after The All-New C-2000.
// Kopuro-Popuro: A Greeting Cardian accepted at its Prog 1 (the var Quest[2][43]Prog): 301 (A Greeting Cardian's line,
// which sits in this quest's run of his chain). A Greeting Cardian completed, this quest available, Windurst fame level
// 3: needToZone 306, else 308 (0, Luminicloth, Revival Tree Root, Wolf Hide) (its finish, any option: begun).
// Accepted: 309 (the same). Completed: 316. The trade, accepted: Luminicloth (529: no xi.item name), a Wolf Hide and a
// Revival Tree Root among the items (npcUtil.tradeHas): 314 (0, the three); else 309. 314's finish:
// npcUtil.completeQuest's steps (the Scentless Armlets, stopping when they cannot be given; Windurst fame 30;
// 700 x GIL_RATE gil, GIL_OBTAINED; the raw player:completeQuest, so the quest's Quest[2][44] vars stay); when given,
// the trade confirmed and needToZone. Kororo, accepted: 312 (the same three).
// Kept as the Lua has it: 308 begins on any option; 314's finish reads no status; the reminder 309 is also the wrong
// trade's answer.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class LegendaryPlanB : QuestScript
    {
        // item 529 (luminicloth): item_basic has it, xi.item has no name for it, so no Item constant
        const int Luminicloth = 529;
        // xi.quest.getVar(player, WINDURST, A_GREETING_CARDIAN, 'Prog')
        const string AGreetingCardianProg = "Quest[2][43]Prog";

        public LegendaryPlanB() : base(QuestLog.Windurst, "LEGENDARY_PLAN_B") { }

        static bool Offered(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Windurst, "A_GREETING_CARDIAN") == QuestStatus.Completed &&
            q.Player.FameLevel(FameArea.Windurst) >= 3;

        protected override void Build(QuestScriptBuilder b)
        {
            // Kopuro-Popuro.lua onTrigger: A Greeting Cardian accepted at its Prog 1, right before its completion
            b.AnyState(when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "A_GREETING_CARDIAN") == QuestStatus.Accepted &&
                    q.Player.CharVar(AGreetingCardianProg) == 1)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called().OnTrigger(QuestReply.Event(301));

            // A Greeting Cardian completed, available, fame level 3: needToZone: A Greeting Cardian's after text
            b.State(QuestStatus.Available, when: q => Offered(q) && q.Player.NeedToZone)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called().OnTrigger(QuestReply.Event(306));

            // ... else the offer
            b.State(QuestStatus.Available, when: Offered)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrigger(QuestReply.Event(308, 0, Luminicloth, Item.RevivalTreeRoot, Item.WolfHide));

            // accepted: the reminder; the trade; Kororo's line
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrigger(QuestReply.Event(309, 0, Luminicloth, Item.RevivalTreeRoot, Item.WolfHide))
                    // Kopuro-Popuro.lua onTrade (after The All-New C-2000's): the three among the items: the completion
                    .OnTrade(q => q.Trade.Has(Luminicloth, Item.WolfHide, Item.RevivalTreeRoot)
                        ? QuestReply.Event(314, 0, Luminicloth, Item.RevivalTreeRoot, Item.WolfHide)
                        : QuestReply.Event(309, 0, Luminicloth, Item.RevivalTreeRoot, Item.WolfHide))
                // Kororo.lua onTrigger (after The All-New C-2000's)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Kororo).Called()
                    .OnTrigger(QuestReply.Event(312, 0, Luminicloth, Item.RevivalTreeRoot, Item.WolfHide));

            // completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called().OnTrigger(QuestReply.Event(316));

            // Kopuro-Popuro.lua onEventFinish (308, 314)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 308, q => q.Quest.Begin())
                .OnEventFinish(Zone.WindurstWoods, 314, q =>
                {
                    // npcUtil.completeQuest with the Scentless Armlets, gil 700, fame 30 and the fameArea WINDURST,
                    // step for step: not quest:complete, whose cleanup would clear the quest's own Quest[2][44] vars.
                    // It returns false, with nothing more done, when the armlets cannot be given
                    if (!q.Player.GiveItem(Item.ScentlessArmlets))
                    {
                        return;
                    }
                    q.Player.AddFame(FameArea.Windurst, 30);
                    int gil = RateAmount(700, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.WindurstWoods.GIL_OBTAINED, gil);
                    q.Player.CompleteQuest(QuestLog.Windurst, "LEGENDARY_PLAN_B");
                    q.Trade.Confirm();
                    q.Player.SetNeedToZone(true);
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
