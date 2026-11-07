// Lakeside Minuet (Log ID 3, Quest ID 95), a line-for-line port of scripts/quests/jeuno/Lakeside_Minuet.lua (quest
// rework B3b). Laila : !pos -54.045 -1 100.996 244; Rhea Myuliah : !pos -56.220 -1 101.805 244; Valderotaux : !pos 97
// 0.1 113 230; Glowing Pebbles : !pos 104.2 4.1 443.6 82. From ADVANCED_JOB_LEVEL with ENABLE_WOTG 1 Laila offers 10111
// (option 1: begun). Accepted: Laila 10112 (10118 with the Stardust Pebble: completed, Troupe Brilioth Dancer, fame +7
// x3, Dancer unlocked, UNLOCK_DANCER, the pebble taken, Job Gesture: Dancer, needToZone); Rhea Myuliah 10113 at Prog 0
// (its finish: Prog 1 while Prog is still 0), 10114 at 1, 10115 at 2 (Prog 3), at 3 10117 / 10116 by turns (the
// rheaOption local var), 10121 at 4; Valderotaux in Southern San d'Oria 888 at Prog 1 (Prog 2), 889 from 2; the Glowing
// Pebbles in Jugner Forest [S] 100 at Prog 3 without the pebble (the pebble, Prog 4). Completed, until The Unfinished
// Waltz is completed: Laila 10119, Rhea Myuliah 10126, both over the default.
// Kept Lua quirk: Rhea Myuliah's Prog 4 arm plays 10121, the csid of her default (a GM's Prog 5 gets the default).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class LakesideMinuet : QuestScript
    {
        public LakesideMinuet() : base(QuestLog.Jeuno, "LAKESIDE_MINUET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Lakeside_Minuet";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.TroupeBriliothDancer });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL") &&
                    q.Settings.MainEquals("ENABLE_WOTG", 1))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.ProgressEvent(10111))
                .OnEventFinish(Zone.UpperJeuno, 10111, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.StardustPebble))
                    {
                        return QuestReply.ProgressEvent(10118);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(10112);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(10113);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(10114);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(10115);
                    }
                    else if (questProgress == 3)
                    {
                        if (q.Quest.LocalVar("rheaOption") == 0)
                        {
                            return QuestReply.ProgressEvent(10117);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10116);
                        }
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(10121);
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 10113, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10115, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.UpperJeuno, 10116, q =>
                {
                    q.Quest.SetLocalVar("rheaOption", 0);
                })
                .OnEventFinish(Zone.UpperJeuno, 10117, q =>
                {
                    q.Quest.SetLocalVar("rheaOption", 1);
                })
                .OnEventFinish(Zone.UpperJeuno, 10118, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Player.UnlockJob(Job.Dnc);
                        q.Text.Special(Text.UpperJeuno.UNLOCK_DANCER);
                        q.Player.DelKeyItem(KeyItem.StardustPebble);
                        q.Player.GiveKeyItem(KeyItem.JobGestureDancer);
                        q.Player.SetNeedToZone(true);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Valderotaux).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(888);
                    }
                    else if (questProgress >= 2)
                    {
                        return QuestReply.Event(889);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 888, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.GlowingPebbles).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3 &&
                        !q.Player.HasKeyItem(KeyItem.StardustPebble))
                    {
                        return QuestReply.ProgressEvent(100);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForestS, 100, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.StardustPebble);
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Completed, when: q =>
                    !q.Player.HasCompletedQuest(QuestLog.Jeuno, "THE_UNFINISHED_WALTZ"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Laila).OnTrigger(QuestReply.Event(10119).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.RheaMyuliah).OnTrigger(QuestReply.Event(10126).ReplaceDefault());
        }
    }
}
