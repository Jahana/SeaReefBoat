// Heroes of Abyssea (Log ID 8, Quest ID 179), a line-for-line port of scripts/quests/abyssea/Heroes_of_Abyssea.lua
// (quest rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu).
// Available, with any of An Ulcerous Uragnite, A Beaked Blusterer or A Man-eating Mite begun (accepted or completed):
// Joachim: 340, whose finish begins it. Accepted, with all three completed: Joachim: 341, whose finish completes it (no
// reward). Kept as the Lua has it: 340 begins on any option; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class HeroesOfAbyssea : QuestScript
    {
        public HeroesOfAbyssea() : base(QuestLog.Abyssea, "HEROES_OF_ABYSSEA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Heroes_of_Abyssea";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Abyssea, "AN_ULCEROUS_URAGNITE") > QuestStatus.Available ||
                    q.Player.QuestStatus(QuestLog.Abyssea, "A_BEAKED_BLUSTERER") > QuestStatus.Available ||
                    q.Player.QuestStatus(QuestLog.Abyssea, "A_MAN_EATING_MITE") > QuestStatus.Available)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(340))
                .OnEventFinish(Zone.PortJeuno, 340, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "AN_ULCEROUS_URAGNITE") &&
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "A_BEAKED_BLUSTERER") &&
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "A_MAN_EATING_MITE"))
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(341))
                .OnEventFinish(Zone.PortJeuno, 341, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
