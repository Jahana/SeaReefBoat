// Rycharde the Chef (Log ID 4, Quest ID 0), a line-for-line port of scripts/quests/otherAreas/RQ1_Rycharde_the_Chef.lua
// (quest rework B3b). Mhaura: Rycharde !pos 17.451 -16.000 88.815 249, Take !pos 20.616 -8.000 69.757 249, Numi
// Adaligo !pos -80.332 -24.050 34.794 249. Available (no other condition): Numi Adaligo's default 50 (no quest trigger of
// hers) finished with option 2 sets Prog 1; Take at Prog 1: 60, whose finish sets Prog 2 (at Prog 2 or more: 68; at Prog
// 0: nil, his default 59); Rycharde at Prog 2: 70 (the meat), option 71 begins, option 70 sets Prog 3; at Prog 3 (or
// more): 71, option 72 begins; at Prog 0 or 1: nil (his default 69). Accepted: Rycharde 72; two Slices of Dhalmel Meat
// alone (tradeHasExactly): 74, whose finish completes the quest (fame 20 Windurst, 1500 gil, Purveyor in Training) and
// then confirms the trade and stores DayCompleted (the Vana'diel day, Way of the Cook's wait); exactly one slice among the
// items (hasItemQty is an equality): 73; Take: 68.
// Kept as the Lua has it: Numi Adaligo's 50 finished with option 2 while available sets Prog 1 at any Prog (Prog 2 or 3
// falls back to 1); the trade is confirmed after quest:complete (the cleanup runs first); DayCompleted is written after the
// cleanup, so it outlives the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class RychardeTheChef : QuestScript
    {
        public RychardeTheChef() : base(QuestLog.OtherAreas, "RYCHARDE_THE_CHEF") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ1_Rycharde_the_Chef";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Title = Title.PurveyorInTraining,
                Gil = 1500,
            });
            b.Steps(1, 2, 3);

            // Section: Quest available. Talk to Numi Adaligo, Take and Rycharde.
            b.State(QuestStatus.Available)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(70, Item.SliceOfDhalmelMeat);
                    }
                    else if (q.Quest.Step > 2)
                    {
                        return QuestReply.ProgressEvent(71, Item.SliceOfDhalmelMeat);
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(60);
                    }
                    else if (q.Quest.Step > 1)
                    {
                        return QuestReply.Event(68);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 50, q =>
                {
                    if (q.Option == 2) // Ask for work.
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 60, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Mhaura, 70, q =>
                {
                    if (q.Option == 71) // Accept quest.
                    {
                        q.Quest.Begin();
                    }
                    else if (q.Option == 70) // Decline quest.
                    {
                        q.Quest.SetStep(3);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 71, q =>
                {
                    if (q.Option == 72) // Accept quest.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted. Handle trade outcomes.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(72); // Interaction dialog.
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.SliceOfDhalmelMeat, 2)))
                        {
                            return QuestReply.ProgressEvent(74); // Quest completed dialog.
                        }
                        else if (q.Trade.HasItemQty(Item.SliceOfDhalmelMeat, 1))
                        {
                            return QuestReply.Event(73); // "That's not enough!" dialog.
                        }
                        return null;
                    })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(68)) // Optional dialog.
                .OnEventFinish(Zone.Mhaura, 74, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Used for next quest wait time.
                    }
                });
        }
    }
}
