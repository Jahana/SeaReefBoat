// The Forbidden Frontier (Log ID 8, Quest ID 166), a line-for-line port of
// scripts/quests/abyssea/The_Forbidden_Frontier.lua (quest rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0
// -9.978 246 (his script: the traverser stone menu). Available, with any of A Goldstruck Gigas, To Paste a Peiste or
// Megadrile Menace begun (accepted or completed): Joachim: 331, whose finish begins it. Accepted, with all three
// completed: Joachim: 332, whose finish completes it (no reward). Kept as the Lua has it: 331 begins on any option; no
// completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class TheForbiddenFrontier : QuestScript
    {
        public TheForbiddenFrontier() : base(QuestLog.Abyssea, "THE_FORBIDDEN_FRONTIER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/The_Forbidden_Frontier";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Abyssea, "A_GOLDSTRUCK_GIGAS") > QuestStatus.Available ||
                    q.Player.QuestStatus(QuestLog.Abyssea, "TO_PASTE_A_PEISTE") > QuestStatus.Available ||
                    q.Player.QuestStatus(QuestLog.Abyssea, "MEGADRILE_MENACE") > QuestStatus.Available)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(331))
                .OnEventFinish(Zone.PortJeuno, 331, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "A_GOLDSTRUCK_GIGAS") &&
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "TO_PASTE_A_PEISTE") &&
                    q.Player.HasCompletedQuest(QuestLog.Abyssea, "MEGADRILE_MENACE"))
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(332))
                .OnEventFinish(Zone.PortJeuno, 332, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
