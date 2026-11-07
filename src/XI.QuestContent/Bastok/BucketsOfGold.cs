// Buckets of Gold (Log ID 1, Quest ID 41), a line-for-line port of scripts/quests/bastok/Buckets_of_Gold.lua (quest rework
// B3b). Foss : !pos -283 -12 -37 235. Kept as the Lua file has it: the trade section matches every status but available, so
// the rusty bucket trade completes the quest again after each completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BucketsOfGold : QuestScript
    {
        public BucketsOfGold() : base(QuestLog.Bastok, "BUCKETS_OF_GOLD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Buckets_of_Gold";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 300, Title = Title.BucketFisher });

            // the offer: option 0 accepts it
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Foss).OnTrigger(QuestReply.ProgressEvent(271))
                .OnEventFinish(Zone.BastokMarkets, 271, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Foss)
                    .OnTrade(q => q.Trade.Matches((Item.RustyBucket, 5)) ? QuestReply.ProgressEvent(272) : null)
                .OnEventFinish(Zone.BastokMarkets, 272, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
