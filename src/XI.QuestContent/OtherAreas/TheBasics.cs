// The Basics (Log ID 4, Quest ID 6), a line-for-line port of scripts/quests/otherAreas/RQ7_The_Basics.lua (quest rework
// B3b). Mhaura: Rycharde !pos 17.451 -16.000 88.815 249, Take; Selbina: Valgeir !pos 57.496 -15.273 20.229 248.
// Available at Windurst fame level >= 5 once The Clue is completed: Rycharde, when The Clue's DayCompleted + 7 < the
// Vana'diel day: 94, else nil (his default 69); 94's finish with option 85 clears Quest[4][5]DayCompleted, gives the
// Mhauran Couscous (npcUtil.giveKeyItem) and begins the quest. Take: 66. Valgeir: 144. Accepted: Valgeir at Prog 0: 106,
// whose finish gives a Baked Popoto (npcUtil.giveItem), takes the couscous with KEYITEM_OBTAINED + 1 and sets Prog 1;
// else 145. Rycharde 95; a Baked Popoto alone at Prog 1: 96, whose finish completes the quest (fame 60 Windurst, the Tea
// Set, Five Star Purveyor), takes the trade, adds 2000 exp and sets CommentaryValgeir and CommentaryRycharde to 1. Take
// while accepted: no handler (his default 59). Completed: Rycharde with CommentaryRycharde 1: 97 (its finish clears it),
// else 98 replaceDefault; Take 67 replaceDefault; Valgeir with CommentaryValgeir 1: 107 (its finish clears it), else 146
// replaceDefault.
// Kept as the Lua has it: 106's finish takes the couscous and sets Prog 1 even when the popoto could not be given (a full
// inventory: the quest cannot be finished then, short of another popoto); the Commentary vars are written after the
// cleanup, so they outlive the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheBasics : QuestScript
    {
        public TheBasics() : base(QuestLog.OtherAreas, "THE_BASICS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ7_The_Basics";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 60,
                FameArea = FameArea.Windurst,
                Item = Item.TeaSet,
                Title = Title.FiveStarPurveyor,
            });
            b.Steps(1);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                                                       q.Player.QuestStatus(QuestLog.OtherAreas, "THE_CLUE") == QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Player.CharVar("Quest[4][5]DayCompleted") + 7 < q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(94); // Quest starting event.
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    return QuestReply.Event(66);
                })
                .OnEventFinish(Zone.Mhaura, 94, q =>
                {
                    if (q.Option == 85) // Accept quest option.
                    {
                        q.Player.SetCharVar("Quest[4][5]DayCompleted", 0); // Delete previous quest (The clue) variables.
                        q.Player.GiveKeyItem(KeyItem.MhauranCouscous); // Give Key Item to player.
                        q.Quest.Begin();
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir).OnTrigger(q =>
                {
                    return QuestReply.Event(144);
                });

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BakedPopoto) &&
                            q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(96); // Quest completed.
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(95);
                    })
                .OnEventFinish(Zone.Mhaura, 96, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.AddExp(2000);
                        q.Quest.SetVar("CommentaryValgeir", 1);
                        q.Quest.SetVar("CommentaryRycharde", 1);
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(106); // Deliver Key Item.
                    }
                    else
                    {
                        return QuestReply.Event(145);
                    }
                })
                .OnEventFinish(Zone.Selbina, 106, q =>
                {
                    q.Player.GiveItem(Item.BakedPopoto);
                    q.Player.DelKeyItem(KeyItem.MhauranCouscous);
                    q.Text.Special(q.Text.Id(Text.Selbina.KEYITEM_OBTAINED) + 1, KeyItem.MhauranCouscous);
                    q.Quest.SetStep(1);
                });

            // Section: Quest completed. Handle optional post quest dialogs and default interactions.
            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Quest.Var("CommentaryRycharde") == 1)
                    {
                        return QuestReply.Event(97); // Optional dialog.
                    }
                    else
                    {
                        return QuestReply.Event(98).ReplaceDefault(); // Default message after clompleting this quest.
                    }
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    return QuestReply.Event(67).ReplaceDefault(); // Default message after clompleting this quest.
                })
                .OnEventFinish(Zone.Mhaura, 97, q =>
                {
                    q.Quest.SetVar("CommentaryRycharde", 0);
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir).OnTrigger(q =>
                {
                    if (q.Quest.Var("CommentaryValgeir") == 1)
                    {
                        return QuestReply.Event(107); // Optional dialog.
                    }
                    else
                    {
                        return QuestReply.Event(146).ReplaceDefault(); // Default message after clompleting this quest.
                    }
                })
                .OnEventFinish(Zone.Selbina, 107, q =>
                {
                    q.Quest.SetVar("CommentaryValgeir", 0);
                });
        }
    }
}
