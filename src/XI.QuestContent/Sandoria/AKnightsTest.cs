// A Knight's Test (Log ID 0, Quest ID 29), a line-for-line port of scripts/quests/sandoria/A_Knights_Test.lua (quest
// rework B3b, San d'Oria group 2). Balasiel : !pos -136 -11 64 230; Cahaurme : !pos 55.749 -8.601 -29.354 230; Baunise :
// !pos -55 -8 -32 230; Disused Well : !pos -221 2 -293 149. Offered once A Squire's Test II is completed: at
// ADVANCED_JOB_LEVEL 627 (option 0: the Book of Tasks and begun; any other option: the quest var Option 1), then 635
// (option 0: the same), below that level 670. Accepted: Baunise's 634 gives the Book of the West and Cahaurme's 633
// the Book of the East (afterwards their messages DO_NOT_FRET / TRIAL_IS_DIFFICULT); with both books the Disused Well in
// Davoi gives the Knight's Soul (YOU_FIND_NOTHING once held, nothing without both books); Balasiel's 669, or 628 with the
// soul: completed (Kite Shield, the Paladin gesture, fame +20, Tried and Tested Knight), PLD unlocked (UNLOCK_PALADIN),
// the four key items deleted. Completed: Baunise's YOUVE_DONE_WELL and Cahaurme's MAKE_EXCELLENT_KNIGHT
// (replaceDefault). RoE records read this quest's status (stays). The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class AKnightsTest : QuestScript
    {
        public AKnightsTest() : base(QuestLog.Sandoria, "A_KNIGHTS_TEST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Knights_Test";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Item = Item.KiteShield,
                KeyItem = KeyItem.JobGesturePaladin,
                Title = Title.TriedAndTestedKnight,
            });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Sandoria, "A_SQUIRES_TEST_II"))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                    {
                        if (q.Quest.Var("Option") == 0)
                        {
                            return QuestReply.ProgressEvent(627);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(635);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(670);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 627, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.BookOfTasks);
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 635, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.BookOfTasks);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.KnightsSoul))
                    {
                        return QuestReply.ProgressEvent(628);
                    }
                    else
                    {
                        return QuestReply.Event(669);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Baunise).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BookOfTheWest))
                    {
                        return QuestReply.ProgressEvent(634);
                    }
                    else
                    {
                        return QuestReply.MessageName(Text.SouthernSanDoria.DO_NOT_FRET);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Cahaurme).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BookOfTheEast))
                    {
                        return QuestReply.ProgressEvent(633);
                    }
                    else
                    {
                        return QuestReply.MessageName(Text.SouthernSanDoria.TRIAL_IS_DIFFICULT);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 628, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.UnlockJob(Job.Pld);
                        q.Text.Special(Text.SouthernSanDoria.UNLOCK_PALADIN);

                        q.Player.DelKeyItem(KeyItem.KnightsSoul);
                        q.Player.DelKeyItem(KeyItem.BookOfTasks);
                        q.Player.DelKeyItem(KeyItem.BookOfTheWest);
                        q.Player.DelKeyItem(KeyItem.BookOfTheEast);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 633, q => q.Player.GiveKeyItem(KeyItem.BookOfTheEast))
                .OnEventFinish(Zone.SouthernSanDoria, 634, q => q.Player.GiveKeyItem(KeyItem.BookOfTheWest))
                .Npc(Zone.Davoi, Npc.Davoi.DisusedWell).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.BookOfTheEast) &&
                        q.Player.HasKeyItem(KeyItem.BookOfTheWest))
                    {
                        if (!q.Player.HasKeyItem(KeyItem.KnightsSoul))
                        {
                            return QuestReply.KeyItem(KeyItem.KnightsSoul);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.Davoi.YOU_FIND_NOTHING);
                        }
                    }
                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Baunise)
                    .OnTrigger(QuestReply.MessageName(Text.SouthernSanDoria.YOUVE_DONE_WELL).ReplaceDefault())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Cahaurme)
                    .OnTrigger(QuestReply.MessageName(Text.SouthernSanDoria.MAKE_EXCELLENT_KNIGHT).ReplaceDefault());
        }
    }
}
