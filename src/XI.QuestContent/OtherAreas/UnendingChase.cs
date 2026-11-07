// Unending Chase (Log ID 4, Quest ID 2), a line-for-line port of scripts/quests/otherAreas/RQ3_Unending_Chase.lua (quest
// rework B3b). Mhaura: Rycharde !pos 17.451 -16.000 88.815 249. Available once Way of the Cook is completed:
// Rycharde, when Way of the Cook's DayCompleted + 7 < the Vana'diel day and Windurst fame level >= 3: at Prog 0 82, else
// 84 (both with the Puffball); else 75. 82's finish sets Prog 1 and clears Quest[4][1]DayCompleted whatever the option,
// and option 77 begins the quest; 84's option 78 begins it. Take: 65. Accepted: Rycharde 85; a Puffball alone: 83, whose
// finish completes the quest (fame 30 Windurst, 2100 gil, Two Star Purveyor), takes the trade and stores DayCompleted.
// Take: 65.
// Kept as the Lua has it: 82's finish clears the previous quest's DayCompleted even when the offer is declined (so the
// wait is over for good: 0 + 7 < any day).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class UnendingChase : QuestScript
    {
        public UnendingChase() : base(QuestLog.OtherAreas, "UNENDING_CHASE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ3_Unending_Chase";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30,
                FameArea = FameArea.Windurst,
                Title = Title.TwoStarPurveyor,
                Gil = 2100,
            });
            b.Steps(1);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "WAY_OF_THE_COOK") == QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Player.CharVar("Quest[4][1]DayCompleted") + 7 < q.Player.VanadielUniqueDay &&
                        q.Player.FameLevel(FameArea.Windurst) >= 3)
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.ProgressEvent(82, Item.Puffball); // Unending Chase starting event.
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(84, Item.Puffball); // Unending Chase starting event after rejecting it.
                        }
                    }
                    else
                    {
                        return QuestReply.Event(75);
                    }
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(65))
                .OnEventFinish(Zone.Mhaura, 82, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.SetCharVar("Quest[4][1]DayCompleted", 0); // Delete previous quest (Rycharde the Chef) variables

                    if (q.Option == 77) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Mhaura, 84, q =>
                {
                    if (q.Option == 78) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(85); // No hurry dialog.
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.Puffball))
                        {
                            return QuestReply.ProgressEvent(83); // Quest completed.
                        }
                        return null;
                    })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(65))
                .OnEventFinish(Zone.Mhaura, 83, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Set completition day of quest.
                    }
                });
        }
    }
}
