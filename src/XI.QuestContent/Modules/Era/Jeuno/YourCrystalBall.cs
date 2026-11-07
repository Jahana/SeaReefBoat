// Your Crystal Ball's era module, a line-for-line port of modules/era/lua/quests/jeuno/your_crystal_ball.lua (quest rework
// B3b Jeuno, P4): it restores the JST midnight wait while the ahriman lens soaks in the Maze of Shakhrami (the June 17,
// 2014 version update shortened it to one minute). Module:new('era_quest_your_crystal_ball', xi.pre(xi.expansion.SOA));
// its onServerStart override wraps section 2's Maze of Shakhrami Rockwell onTrade: the quest's handler runs, and when it
// changed Wait (the lens taken: now + 60), Wait becomes JstMidnight().
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class YourCrystalBall : QuestModule
    {
        public YourCrystalBall() : base("era_quest_your_crystal_ball", "modules/era/lua/quests/jeuno/your_crystal_ball.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.MAZE_OF_SHAKHRAMI]['Rockwell'].onTrade: the quest's state 2 (accepted)
            m.Quest(QuestKey.Jeuno.YourCrystalBall)
                .State(2).Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.Rockwell).WrapOnTrade((q, baseTrade) =>
                {
                    // The lens soaks overnight. Rockwell hands the sphere over at JST midnight.
                    int previousWait = q.Quest.Var("Wait");
                    var result = baseTrade(q);

                    if (q.Quest.Var("Wait") != previousWait)
                    {
                        q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                    }

                    return result;
                });
        }
    }
}
