// Save My Son (Log ID 3, Quest ID 5), a line-for-line port of scripts/quests/jeuno/Save_My_Son.lua (quest rework B3b).
// Door: Merchant's House (_6t2) : !pos -82.22 -7.65 -168.839 245; Nightflowers : !pos -264.775 -3.718 28.767 126.
// Chocobo's Wounds completed and level ADVANCED_JOB_LEVEL: the door's 164 begins it on option 0. Accepted: the door's
// 229 and Shalott's 101 until the Nightflowers of Qufim Island play 0 at night (after 21:30 or until 5:40, Prog 0):
// Prog 1, after which Shalott answers 44 and the door 163: completed (2100 gil, Beast Whistle, Life Saver, fame +7 in
// San d'Oria, Bastok and Windurst). Completed: the door's 132 and Shalott's 44 (replaceDefault); until Path of the
// Beastmaster is completed the chocobo and Osker answer 55. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class SaveMySon : QuestScript
    {
        public SaveMySon() : base(QuestLog.Jeuno, "SAVE_MY_SON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Save_My_Son";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 2100, Item = Item.BeastWhistle, Title = Title.LifeSaver });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "CHOCOBOS_WOUNDS") &&
                    q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).OnTrigger(QuestReply.ProgressEvent(164))
                .OnEventFinish(Zone.LowerJeuno, 164, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(163);
                    }
                    else
                    {
                        return QuestReply.Event(229);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 163, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Shalott).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(101);
                    }
                    else
                    {
                        return QuestReply.Event(44);
                    }
                })
                .Npc(Zone.QufimIsland, Npc.QufimIsland.Nightflowers).OnTrigger(q =>
                {
                    int vanadielClockTime = q.Player.VanadielClockTime;

                    if (q.Quest.Step == 0 && (vanadielClockTime > 2130 || vanadielClockTime <= 540))
                    {
                        return QuestReply.ProgressEvent(0);
                    }
                    return null;
                })
                .OnEventFinish(Zone.QufimIsland, 0, q => q.Quest.SetStep(1));

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).OnTrigger(QuestReply.Event(132).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Shalott).OnTrigger(QuestReply.Event(44).ReplaceDefault());

            b.State(QuestStatus.Completed, when: q => !q.Player.HasCompletedQuest(QuestLog.Jeuno, "PATH_OF_THE_BEASTMASTER"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Chocobo).OnTrigger(QuestReply.Event(55))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).OnTrigger(QuestReply.Event(55));
        }
    }
}
