// Scars of Abyssea (Log ID 8, Quest ID 175), a line-for-line port of scripts/quests/abyssea/Scars_of_Abyssea.lua (quest
// rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu). Available,
// with any of The Beast of Bastore, A Delectable Demon or A Fluttery Fiend begun (accepted or completed): Joachim: 337,
// whose finish begins it. Accepted, with all three completed: Joachim: 338, whose finish completes it (no reward). Kept
// as the Lua has it: 337 begins on any option; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class ScarsOfAbyssea : QuestScript
    {
        public ScarsOfAbyssea() : base(QuestLog.Abyssea, "SCARS_OF_ABYSSEA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Scars_of_Abyssea";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Abyssea, "THE_BEAST_OF_BASTORE") > QuestStatus.Available ||
                    q.Player.QuestStatus(QuestLog.Abyssea, "A_DELECTABLE_DEMON") > QuestStatus.Available ||
                    q.Player.QuestStatus(QuestLog.Abyssea, "A_FLUTTERY_FIEND") > QuestStatus.Available)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(337))
                .OnEventFinish(Zone.PortJeuno, 337, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "THE_BEAST_OF_BASTORE") &&
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "A_DELECTABLE_DEMON") &&
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "A_FLUTTERY_FIEND"))
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(338))
                .OnEventFinish(Zone.PortJeuno, 338, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
