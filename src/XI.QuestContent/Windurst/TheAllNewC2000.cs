// The All-New C-2000 (Log ID 2, Quest ID 24), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Kopuro-Popuro.lua, Kororo.lua and Boizo-Naizo.lua (quest rework B3b, Windurst group
// 22; call-only). Kopuro-Popuro : !pos -0.037 -4.749 -22.589 241; Kororo : !pos -11.883 -2.750 5.508 241; Boizo-Naizo :
// !pos -9.581 -2.750 -26.062 241. Kopuro-Popuro has no default action; his talk calls The All-New C-3000, Legendary
// Plan B, then this quest (his standard 276 last), his trade this quest first. Kororo (default 277) and Boizo-Naizo
// (default 275) take turns with their scripts; A Greeting Cardian (C#) answers both from its container, Riding on the
// Clouds (Jeuno, C#) Boizo-Naizo's trade.
// Kopuro-Popuro: available: 285 (0, Rabbit Hide, Insect Wing, Two-Leaf Mandragora Bud) (its finish: any option but 2
// begins); accepted: 288 (the same); completed: 293. The trade, accepted: an Insect Wing, a Rabbit Hide and a Two-Leaf
// Mandragora Bud among the items (npcUtil.tradeHas): 292 (200 x GIL_RATE); else 288. 292's finish: completed
// (player:completeQuest), Windurst fame 20, Cardian Tutor, 200 x GIL_RATE gil (player:addGil, no message), the trade
// confirmed. Kororo and Boizo-Naizo, accepted: 291 / 290 (Kororo's script calls Legendary Plan B after this quest).
// Kept as the Lua has it: 285 begins on options other than 2 (0 included); the finishes read no status; this quest's
// three statuses each answer at Kopuro-Popuro, so his standard 276 (kept in his script) is never reached; 292's
// argument and gil are 200 x GIL_RATE through the engine's integral getter (llround).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheAllNewC2000 : QuestScript
    {
        public TheAllNewC2000() : base(QuestLog.Windurst, "THE_ALL_NEW_C_2000") { }

        // xi.settings.main.GIL_RATE * 200 (292's argument and the pay), read by the engine's integral getter (llround)
        static int Pay(IQuestContext q) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * 200, MidpointRounding.AwayFromZero);

        protected override void Build(QuestScriptBuilder b)
        {
            // Kopuro-Popuro.lua onTrigger: available: the offer
            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrigger(QuestReply.Event(285, 0, Item.RabbitHide, Item.InsectWing, Item.TwoLeafMandragoraBud));

            // accepted: the reminder; the trade; Kororo's and Boizo-Naizo's lines
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called()
                    .OnTrigger(QuestReply.Event(288, 0, Item.RabbitHide, Item.InsectWing, Item.TwoLeafMandragoraBud))
                    // Kopuro-Popuro.lua onTrade (first): the three among the items: the completion; else the reminder
                    .OnTrade(q => q.Trade.Has(Item.InsectWing, Item.RabbitHide, Item.TwoLeafMandragoraBud)
                        ? QuestReply.Event(292, Pay(q))
                        : QuestReply.Event(288, 0, Item.RabbitHide, Item.InsectWing, Item.TwoLeafMandragoraBud))
                // Kororo.lua onTrigger (first)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Kororo).Called().OnTrigger(QuestReply.Event(291))
                // Boizo-Naizo.lua onTrigger
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BoizoNaizo).Called().OnTrigger(QuestReply.Event(290));

            // completed: after the quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KopuroPopuro).Called().OnTrigger(QuestReply.Event(293));

            // Kopuro-Popuro.lua onEventFinish (285, 292)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 285, q =>
                {
                    if (q.Option != 2) // option 2 declines
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 292, q =>
                {
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][24] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "THE_ALL_NEW_C_2000");
                    q.Player.AddFame(FameArea.Windurst, 20);
                    q.Player.AddTitle(Title.CardianTutor);
                    q.Player.AddGil(Pay(q));
                    q.Trade.Confirm();
                });
        }
    }
}
