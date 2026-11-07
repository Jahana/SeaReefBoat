// Path of the Bard (Log ID 3, Quest ID 20), a line-for-line port of scripts/quests/jeuno/Path_of_the_Bard.lua (quest
// rework B3b). Song Runes !pos -721 -7 102 103. Never begun: while it is available (A Minstrel in Despair completed)
// Bki Tbujhja 182, Mataligeat 144, Mertaire speaks COULD_HE_BE, and the Song Runes of Valkurm Dunes play 2: the bard
// job unlocked (UNLOCK_BARD), completed (3000 gil, the bard's job gesture, Wandering Minstrel), fame +7 x3 whatever
// complete returns. Completed: Bki Tbujhja 180, Mataligeat 143, Mertaire 103 while Painful Memory is available.
// Mertaire's messageName(..., true, false): showName, no turn (QuestArg bools), as in TheOldMonument.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class PathOfTheBard : QuestScript
    {
        public PathOfTheBard() : base(QuestLog.Jeuno, "PATH_OF_THE_BARD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Path_of_the_Bard";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 3000, KeyItem = KeyItem.JobGestureBard, Title = Title.WanderingMinstrel });

            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.Jeuno, "A_MINSTREL_IN_DESPAIR") == QuestStatus.Completed)
                // It was found that all of the dialogue is optional and the player can go straight to the song runes in Valkrum Dunes upon finishing A Minstrel in Despair
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.BkiTbujhja).OnTrigger(QuestReply.Event(182))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mataligeat).OnTrigger(QuestReply.Event(144))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(QuestReply.MessageName(Text.LowerJeuno.COULD_HE_BE, 0, 0, 0, 0, true, false))
                .Npc(Zone.ValkurmDunes, Npc.ValkurmDunes.SongRunes).OnTrigger(QuestReply.ProgressCutscene(2))
                .OnEventFinish(Zone.ValkurmDunes, 2, q =>
                {
                    q.Trade.Confirm();
                    q.Player.UnlockJob(Job.Brd);
                    q.Text.Special(Text.ValkurmDunes.UNLOCK_BARD);
                    q.Quest.Complete();
                    q.Player.AddFame(FameArea.Sandoria, 7);
                    q.Player.AddFame(FameArea.Bastok, 7);
                    q.Player.AddFame(FameArea.Windurst, 7);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.BkiTbujhja).OnTrigger(QuestReply.Event(180))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mataligeat).OnTrigger(QuestReply.Event(143))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(q =>
                {
                    if (q.Player.QuestStatus(QuestLog.Jeuno, "PAINFUL_MEMORY") == QuestStatus.Available)
                    {
                        return QuestReply.Event(103); // Plays after this quest is complete and before the player meets the requirements to start "Painful Memory"
                    }
                    return null;
                });
        }
    }
}
