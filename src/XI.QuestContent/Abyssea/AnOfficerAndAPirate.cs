// An Officer and a Pirate (Log ID 8, Quest ID 168), a line-for-line port of
// scripts/quests/abyssea/An_Officer_and_a_Pirate.lua (quest rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0
// -9.978 246 (his script: the traverser stone menu). Added by First Contact's 11 (no available section; that 11 plays
// from 18:00 to 4:59 since user ruling 2026-11-04). Accepted: Joachim: 334, whose finish completes it and adds Heart of
// Madness (accepted). Kept as the Lua has it: no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AnOfficerAndAPirate : QuestScript
    {
        public AnOfficerAndAPirate() : base(QuestLog.Abyssea, "AN_OFFICER_AND_A_PIRATE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/An_Officer_and_a_Pirate";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(334))
                .OnEventFinish(Zone.PortJeuno, 334, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "HEART_OF_MADNESS");
                    }
                });
        }
    }
}
