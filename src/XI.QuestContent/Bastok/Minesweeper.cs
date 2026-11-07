// Minesweeper (Log ID 1, Quest ID 39), a line-for-line port of scripts/quests/bastok/Minesweeper.lua (quest rework B3b).
// Gerbaum : !pos -119.899 -3.492 -74.651 234. Kept as the Lua file has it: the trade section matches every status but
// available, so the soot trade completes the quest again after each completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Minesweeper : QuestScript
    {
        public Minesweeper() : base(QuestLog.Bastok, "MINESWEEPER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Minesweeper";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 150, Title = Title.ZeruhnSweeper });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMines, Npc.BastokMines.Gerbaum).OnTrigger(QuestReply.ProgressEvent(108))
                .OnEventFinish(Zone.BastokMines, 108, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Gerbaum)
                    .OnTrade(q => q.Trade.Matches((Item.PinchOfZeruhnSoot, 3)) ? QuestReply.ProgressEvent(109) : null)
                .OnEventFinish(Zone.BastokMines, 109, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
