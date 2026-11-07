// The Bare Bones (Log ID 1, Quest ID 38), a line-for-line port of scripts/quests/bastok/The_Bare_Bones.lua (quest rework
// B3b). Degenhard : !pos -175 2 -135 235; Biggorf : !pos -211.379 1.999 -142.024 235. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheBareBones : QuestScript
    {
        public TheBareBones() : base(QuestLog.Bastok, "THE_BARE_BONES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Bare_Bones";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.MapOfTheDangrufWadi, FameArea = FameArea.Bastok, Fame = 20, Exp = 2000 });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Degenhard).OnTrigger(QuestReply.ProgressEvent(256))
                .OnEventFinish(Zone.BastokMarkets, 256, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Biggorf).OnTrigger(QuestReply.ProgressEvent(257))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Degenhard)
                    .OnTrade(q => q.Trade.Matches((Item.BoneChip, 1)) ? QuestReply.ProgressEvent(258) : null)
                .OnEventFinish(Zone.BastokMarkets, 258, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
