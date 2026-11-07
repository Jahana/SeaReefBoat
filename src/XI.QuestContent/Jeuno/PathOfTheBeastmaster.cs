// Path of the Beastmaster (Log ID 3, Quest ID 19), a line-for-line port of
// scripts/quests/jeuno/Path_of_the_Beastmaster.lua (quest rework B3b). Brutus : !pos -55 8 95 244. Save My Son
// completed and level ADVANCED_JOB_LEVEL: Brutus's 70 unlocks the beastmaster job (YOU_CAN_NOW_BECOME_A_BEASTMASTER)
// and completes it (the Job Gesture: Beastmaster key item, Animal Trainer), fame +7 in San d'Oria, Bastok and Windurst
// whatever quest:complete returns. Completed: Brutus 20, the chocobo 72 and Osker 45 (both replaceDefault).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class PathOfTheBeastmaster : QuestScript
    {
        public PathOfTheBeastmaster() : base(QuestLog.Jeuno, "PATH_OF_THE_BEASTMASTER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Path_of_the_Beastmaster";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.JobGestureBeastmaster, Title = Title.AnimalTrainer });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "SAVE_MY_SON") &&
                    q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(QuestReply.ProgressEvent(70))
                .OnEventFinish(Zone.UpperJeuno, 70, q =>
                {
                    q.Player.UnlockJob(Job.Bst);
                    q.Text.Special(Text.UpperJeuno.YOU_CAN_NOW_BECOME_A_BEASTMASTER);
                    q.Quest.Complete();
                    q.Player.AddFame(FameArea.Sandoria, 7);
                    q.Player.AddFame(FameArea.Bastok, 7);
                    q.Player.AddFame(FameArea.Windurst, 7);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Chocobo).OnTrigger(QuestReply.Event(72).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).OnTrigger(QuestReply.Event(45).ReplaceDefault());
        }
    }
}
