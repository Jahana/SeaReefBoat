// Rivals (Log ID 1, Quest ID 20), a line-for-line port of scripts/quests/bastok/Rivals.lua (quest rework B3b).
// Detzo : !pos 5.365 6.999 9.891 234. Mission 7-2 reads its status and keeps the read. Kept as the Lua file has it: the
// Mythril Sallet is only borrowed, so the finish completes the quest without taking the trade.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Rivals : QuestScript
    {
        public Rivals() : base(QuestLog.Bastok, "RIVALS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Rivals";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Bastok, Item = Item.WolfGorget, Title = Title.ContestRigger });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.BastokMines, Npc.BastokMines.Detzo).OnTrigger(QuestReply.ProgressEvent(93))
                .OnEventFinish(Zone.BastokMines, 93, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Detzo)
                    .OnTrade(q => q.Trade.Matches((Item.MythrilSallet, 1)) ? QuestReply.ProgressEvent(94) : null)
                    .OnTrigger(QuestReply.MessageName(Text.BastokMines.DETZO_RIVALS_DIALOG))
                .OnEventFinish(Zone.BastokMines, 94, q => q.Quest.Complete());
        }
    }
}
