// Bombs Away! (Log ID 4, Quest ID 96), a line-for-line port of scripts/quests/otherAreas/Bombs_Away.lua (quest rework
// B3b, Other Areas group 8). Uleguerand Range: Buffalostalker Dodzbraz !pos -380.171 -24.89 -180.797 (no script, no
// DefaultActions entry: nil is nothing).
// Available (no other condition): Buffalostalker 6, whose finish begins the quest on option 1. Not available (accepted
// or completed): his trigger: 7 (event); a trade of exactly two Cluster Cores: 8, whose finish runs quest:complete (a
// Chunk of Shumeyo Salt) and on true tradeComplete.
// Kept as the Lua has it: the second section holds while the quest is completed too, so the trade repeats: each two
// cores give another salt (quest:complete on a completed quest gives the reward again and answers true); with no room
// for the salt complete answers false and the cores stay.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class BombsAway : QuestScript
    {
        public BombsAway() : base(QuestLog.OtherAreas, "BOMBS_AWAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Bombs_Away";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ChunkOfShumeyoSalt,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.UleguerandRange, Npc.UleguerandRange.BuffalostalkerDodzbraz)
                    .OnTrigger(QuestReply.ProgressEvent(6))
                .OnEventFinish(Zone.UleguerandRange, 6, q =>
                {
                    if (q.Option == 1) // You climb mountain.
                    {
                        q.Quest.Begin();
                    }
                });

            // status ~= QUEST_AVAILABLE
            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.UleguerandRange, Npc.UleguerandRange.BuffalostalkerDodzbraz)
                .OnTrigger(q => QuestReply.Event(7))
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.ClusterCore, 2)))
                    {
                        return QuestReply.ProgressEvent(8); // Quest completed dialog.
                    }
                    return null;
                })
                .OnEventFinish(Zone.UleguerandRange, 8, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
