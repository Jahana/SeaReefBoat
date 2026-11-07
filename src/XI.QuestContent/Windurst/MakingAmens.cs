// Making Amens! (Log ID 2, Quest ID 8), ported from the quest branches of
// scripts/zones/Port_Windurst/npcs/Kuroido-Moido.lua and scripts/zones/Garlaige_Citadel/npcs/Mashira.lua (quest rework
// B3b, Windurst group 21; call-only). Kuroido-Moido : !pos -112.5 -4.2 102.9 240; Mashira : !pos 141 -6 138 200.
// Neither has a default action. Making Amends (C#) answers Kuroido-Moido from its own container (276 accepted; 279
// completed with its mustZone, an Event that takes turns with his script), Orastery Woes (C#) too; Rubbish Day (Jeuno,
// C#) answers Mashira while it is accepted.
// Kuroido-Moido: Making Amends completed and this quest available: Windurst fame level 4 and Making Amends' mustZone
// not set: 280 (its finish, any option: begun); else nothing. Accepted without the Broken Wand: 283; with it: 284 (6000
// x GIL_RATE): needToZone, the wand taken, Hakkuru-Rinkuru's Benefactor, 6000 gil (npcUtil.giveCurrency), Windurst fame
// 40, completed. Completed: Wonder Wands accepted 261, completed 266, else 286 (0, Block of Animal Glue). Anything else
// (Making Amends not completed while this quest is available): his random standard 225 / 226, kept in his script.
// Mashira: accepted without the wand: 11 (0); accepted with it, and anything else: 11 (3), his standard, which his
// script keeps (the Lua's wand-held arm is the same startEvent(11, 3) as his else). 11's finish, option 0 while
// accepted: the Broken Wand (npcUtil.giveKeyItem).
// Kuroido-Moido's Wonder Wands lines (261, 266, 286) sit in this quest's completed arm, so they are here.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): Mashira's 11 finish calls no
// player:tradeComplete(); the Lua's call (on every option-0 finish while accepted) took what an earlier trade left in
// the trade container, though he has no onTrade.
// Kept as the Lua has it: 280 begins on any option; the available arm below fame level 4 or with the mustZone plays
// nothing (no else), so his standard dialog is not reached there; Mashira's finish on option 0 while accepted with the
// wand held gives nothing more (npcUtil.giveKeyItem skips a held key item); 284's argument is 6000 x GIL_RATE (the
// engine's integral getter, llround) while its gil is npcUtil.giveCurrency's own 6000 x GIL_RATE; the finishes read the
// status only where the Lua does (284 none).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class MakingAmens : QuestScript
    {
        public MakingAmens() : base(QuestLog.Windurst, "MAKING_AMENS") { }

        // xi.settings.main.GIL_RATE * 6000 (284's argument), read by the engine's integral getter (llround)
        static int Pay(IQuestContext q) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * 6000, MidpointRounding.AwayFromZero);

        static bool MakingAmendsCompleted(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Windurst, "MAKING_AMENDS") == QuestStatus.Completed;

        protected override void Build(QuestScriptBuilder b)
        {
            // Kuroido-Moido.lua onTrigger: Making Amends completed, this quest available, fame level 4, no mustZone
            b.State(QuestStatus.Available, when: q =>
                    MakingAmendsCompleted(q) &&
                    q.Player.FameLevel(FameArea.Windurst) >= 4 &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "MAKING_AMENDS"))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called().OnTrigger(QuestReply.Event(280));

            // ... else nothing (the inner if has no else: his standard dialog is not reached)
            b.State(QuestStatus.Available, when: MakingAmendsCompleted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called().OnTrigger(QuestReply.NoAction());

            // accepted, no Broken Wand: the reminder
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.BrokenWand))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called().OnTrigger(QuestReply.Event(283))
                // Mashira.lua onTrigger: accepted, no wand: the Making Amens dialogue (else his standard: his script)
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.Mashira).Called().OnTrigger(QuestReply.Event(11, 0));

            // accepted, the wand: the quest finish
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called()
                    .OnTrigger(q => QuestReply.Event(284, Pay(q)));

            // completed: during Wonder Wands
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "WONDER_WANDS") == QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called().OnTrigger(QuestReply.Event(261));

            // completed: after Wonder Wands
            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "WONDER_WANDS") == QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called().OnTrigger(QuestReply.Event(266));

            // completed: after Making Amens! (before Wonder Wands)
            b.State(QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).Called()
                    .OnTrigger(QuestReply.Event(286, 0, Item.BlockOfAnimalGlue));

            // Kuroido-Moido.lua and Mashira.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.PortWindurst, 280, q => q.Quest.Begin())
                .OnEventFinish(Zone.PortWindurst, 284, q =>
                {
                    q.Player.SetNeedToZone(true);
                    q.Player.DelKeyItem(KeyItem.BrokenWand);
                    q.Player.AddTitle(Title.HakkuruRinkurusBenefactor);
                    q.Player.GiveCurrency("gil", 6000);
                    q.Player.AddFame(FameArea.Windurst, 40);
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][8] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "MAKING_AMENS");
                })
                .OnEventFinish(Zone.GarlaigeCitadel, 11, q =>
                {
                    if (q.Option == 0 && q.Quest.Status == QuestStatus.Accepted)
                    {
                        q.Player.GiveKeyItem(KeyItem.BrokenWand);
                        // the Lua's player:tradeComplete() here is not ported: Mashira has no onTrade, so no trade
                        // belongs to this event (fixed, user ruling 2026-11-04)
                    }
                });
        }
    }
}
