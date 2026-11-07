// Tenuous Existence (Log ID 8, Quest ID 170), a line-for-line port of scripts/quests/abyssea/Tenuous_Existence.lua
// (quest rework B3b Abyssea). La Theine Plateau: qm3 !pos -179 8 254 102 (no script; default STRANGE_ENERGY). Port
// Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu). Added by Heart of Madness' 335 (no
// available section). Section 1 wants it accepted from 18:00 to 4:59: qm3 12 (-> Prog 1), at any Prog. Section 2,
// accepted at Prog 1: Joachim: 336, whose finish completes it and adds Champions of Abyssea (accepted). A deliberate
// change from the Lua (user ruling 2026-11-04, kept Lua bugs): the Lua's section 1 check is VanadielHour() >= 18 and
// VanadielHour() < 5, never true, so nothing in play set Prog 1; the C# reads it as an `or` (18:00 to 4:59). Kept as
// the Lua has it: no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class TenuousExistence : QuestScript
    {
        public TenuousExistence() : base(QuestLog.Abyssea, "TENUOUS_EXISTENCE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Tenuous_Existence";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            b.Reward(new QuestReward());

            // 18:00 to 4:59 (an `or`; the Lua's `and` was never true: fixed, user ruling 2026-11-04)
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.VanadielHour >= 18 ||
                    q.Player.VanadielHour < 5)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm3).OnTrigger(QuestReply.ProgressEvent(12))
                .OnEventFinish(Zone.LaTheinePlateau, 12, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(336))
                .OnEventFinish(Zone.PortJeuno, 336, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "CHAMPIONS_OF_ABYSSEA");
                    }
                });
        }
    }
}
