// The Clue (Log ID 4, Quest ID 5), a line-for-line port of scripts/quests/otherAreas/RQ6_The_Clue.lua (quest rework B3b).
// Mhaura: Rycharde !pos 17.451 -16.000 88.815 249, Take; Selbina: Valgeir. Available at Windurst fame level >= 5
// once Expertise is completed: Rycharde, when Expertise's DayCompleted + 7 < the Vana'diel day: at Prog 0 90, else 91
// (both with the Crawler Egg); otherwise nil (his default 69). 90's finish sets Prog 1 and clears Quest[4][4]DayCompleted
// whatever the option, and option 83 begins the quest; 91's option 83 begins it. Take: 64. Valgeir: 143. Accepted: Take
// 65; Rycharde 85; four Crawler Eggs alone: 92, whose finish completes the quest (fame 50 Windurst, 3000 gil, Four Star
// Purveyor), takes the trade and stores DayCompleted; else, when the trade holds only eggs (hasItemQty(egg,
// getItemCount()): the egg count equals the whole count) and fewer than four: 93. Valgeir: 143.
// Kept as the Lua has it: the fame level is part of the available check (below level 5 the section is off: Take and
// Valgeir answer with their defaults); 90's finish clears the previous DayCompleted even when declined.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheClue : QuestScript
    {
        public TheClue() : base(QuestLog.OtherAreas, "THE_CLUE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ6_The_Clue";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Windurst,
                Title = Title.FourStarPurveyor,
                Gil = 3000,
            });
            b.Steps(1);

            // Section: Quest is available and never interacted.
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                                                       q.Player.QuestStatus(QuestLog.OtherAreas, "EXPERTISE") == QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Player.CharVar("Quest[4][4]DayCompleted") + 7 < q.Player.VanadielUniqueDay)
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.ProgressEvent(90, Item.CrawlerEgg); // Starting event.
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(91, Item.CrawlerEgg); // Starting event after rejecting.
                        }
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    return QuestReply.Event(64); // Default message after clompleting Expertise quest and before accepting The Clue quest.
                })
                .OnEventFinish(Zone.Mhaura, 90, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.SetCharVar("Quest[4][4]DayCompleted", 0); // Delete previous quest (Rycharde the Chef) variables
                    if (q.Option == 83) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Mhaura, 91, q =>
                {
                    if (q.Option == 83) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir).OnTrigger(q =>
                {
                    return QuestReply.Event(143);
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    return QuestReply.Event(65);
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(85); // No hurry dialog.
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.CrawlerEgg, 4)))
                        {
                            return QuestReply.ProgressEvent(92); // Quest completed.
                        }
                        else
                        {
                            int count = q.Trade.ItemCount;
                            bool crawlerEgg = q.Trade.HasItemQty(Item.CrawlerEgg, q.Trade.ItemCount);

                            if (crawlerEgg && count < 4)
                            {
                                return QuestReply.Event(93);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Mhaura, 92, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Set completition day of quest.
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir).OnTrigger(q =>
                {
                    return QuestReply.Event(143);
                });
        }
    }
}
