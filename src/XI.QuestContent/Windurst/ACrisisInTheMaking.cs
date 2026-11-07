// A Crisis in the Making (Log ID 2, Quest ID 2), ported from the A Crisis in the Making branches of
// scripts/zones/Windurst_Waters/npcs/Ranpi-Monpi.lua and scripts/zones/Giddeus/npcs/Altar_of_Offerings.lua (quest rework
// B3b, Windurst group 14; call-only: each script calls this quest at the branches' spot). Ranpi-Monpi : !pos -116 -3 52
// 238; Altar of Offerings : !pos -137 17 177 145.
// Ranpi-Monpi: available, Windurst fame level 2 or more and no needToZone: 258 (0, 625); option 1: begun,
// QuestCrisisMaking_var 1, needToZone; option 2: needToZone. Accepted: var 1: 262 (0, 625); var 2: 267; another var:
// nothing (no event, not his standard dialog). Completed: no needToZone and var 0: 259 (0, 625) (the repeat; option 1:
// var 1, needToZone; option 2: needToZone); var 1: 262; var 2: 268. Else his random standard dialog (kept in his
// script). 267: 400 gil (npcUtil.giveCurrency), var 0, the Off Offering taken, Windurst fame 10, completed
// (player:completeQuest), needToZone; 268 the same without the completion. The Altar of Offerings: accepted or
// completed (status >= 1) and var 1: 53, option 1: the Off Offering (npcUtil.giveKeyItem), var 2; else its standard 60
// (kept in its script).
// In a Stew (C#, Windurst group 10) answers Ranpi-Monpi from its own container (its Prog >= 1, this quest not accepted),
// and The Dawn of Delectability (C#, Crystal War group 2, TheDawnOfDelectability.cs: accepted, Prog 1: 978; Prog 2: 979, 983 on its
// tradeHasExactly trade; Prog 3: 982, or 980 after the day; Prog 5: 981; all progressEvents) from its own sections,
// before his script runs; Ranpi-Monpi has no DefaultActions entry.
// Kept as the Lua has it: the finishes read no status (53 gives the offering whatever the status is when it ends); the
// altar's status >= 1 reads accepted and completed alike; the completed var-1 reminder does not read needToZone.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ACrisisInTheMaking : QuestScript
    {
        const string Var = "QuestCrisisMaking_var";

        public ACrisisInTheMaking() : base(QuestLog.Windurst, "A_CRISIS_IN_THE_MAKING") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Ranpi-Monpi.lua onTrigger: available, Windurst fame level 2 or more, no needToZone: Quest Offer
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 2 && !q.Player.NeedToZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called()
                    .OnTrigger(QuestReply.Event(258, 0, Item.BottleOfAppleVinegar));

            // accepted, var 1: Quest Objective Reminder
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called()
                    .OnTrigger(QuestReply.Event(262, 0, Item.BottleOfAppleVinegar));

            // accepted, var 2: Quest Finish
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called().OnTrigger(QuestReply.Event(267));

            // accepted at another var: the Lua's inner if has no else, so nothing plays (his standard dialog is not reached)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called().OnTrigger(QuestReply.NoAction());

            // completed, no needToZone, var 0: Repeatable Quest Offer
            b.State(QuestStatus.Completed, when: q => !q.Player.NeedToZone && q.Player.CharVar(Var) == 0)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called()
                    .OnTrigger(QuestReply.Event(259, 0, Item.BottleOfAppleVinegar));

            // completed, var 1: Quest Objective Reminder
            b.State(QuestStatus.Completed, when: q => q.Player.CharVar(Var) == 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called()
                    .OnTrigger(QuestReply.Event(262, 0, Item.BottleOfAppleVinegar));

            // completed, var 2: Repeatable Quest Finish
            b.State(QuestStatus.Completed, when: q => q.Player.CharVar(Var) == 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).Called().OnTrigger(QuestReply.Event(268));

            // Altar_of_Offerings.lua onTrigger: status >= 1 (accepted or completed) and var 1: Receive Offering
            b.AnyState(when: q => q.Quest.Status != QuestStatus.Available && q.Player.CharVar(Var) == 1)
                .Npc(Zone.Giddeus, Npc.Giddeus.AltarofOfferings).Called().OnTrigger(QuestReply.Event(53));

            b.AnyState()
                // Ranpi-Monpi.lua onEventFinish
                .OnEventFinish(Zone.WindurstWaters, 258, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Var, 1);
                        q.Player.SetNeedToZone(true);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.SetNeedToZone(true);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 259, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetCharVar(Var, 1);
                        q.Player.SetNeedToZone(true);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.SetNeedToZone(true);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 267, q =>
                {
                    q.Player.GiveCurrency("gil", 400);
                    q.Player.SetCharVar(Var, 0);
                    q.Player.DelKeyItem(KeyItem.OffOffering);
                    q.Player.AddFame(FameArea.Windurst, 10);
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][2] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "A_CRISIS_IN_THE_MAKING");
                    q.Player.SetNeedToZone(true);
                })
                .OnEventFinish(Zone.WindurstWaters, 268, q =>
                {
                    q.Player.GiveCurrency("gil", 400);
                    q.Player.SetCharVar(Var, 0);
                    q.Player.DelKeyItem(KeyItem.OffOffering);
                    q.Player.AddFame(FameArea.Windurst, 10);
                    q.Player.SetNeedToZone(true);
                })
                // Altar_of_Offerings.lua onEventFinish
                .OnEventFinish(Zone.Giddeus, 53, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.OffOffering);
                        q.Player.SetCharVar(Var, 2);
                    }
                });
        }
    }
}
