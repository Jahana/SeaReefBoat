// Vengeful Wrath (Log ID 1, Quest ID 32), a line-for-line port of scripts/quests/bastok/Vengeful_Wrath.lua (quest rework
// B3b). Goraow : !pos 38 .1 14 234. Kept as the Lua file has it: the trade section matches every status but available, so
// the Quadav helm trade completes the quest again after each completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class VengefulWrath : QuestScript
    {
        public VengefulWrath() : base(QuestLog.Bastok, "VENGEFUL_WRATH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Vengeful_Wrath";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 16, FameArea = FameArea.Bastok, Gil = 900, Title = Title.Avenger });

            // offered at Bastok fame level 3
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.BastokMines, Npc.BastokMines.Goraow).OnTrigger(QuestReply.ProgressEvent(106))
                .OnEventFinish(Zone.BastokMines, 106, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Goraow)
                    .OnTrade(q => q.Trade.Matches((Item.QuadavHelm, 1)) ? QuestReply.ProgressEvent(107) : null)
                .OnEventFinish(Zone.BastokMines, 107, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
