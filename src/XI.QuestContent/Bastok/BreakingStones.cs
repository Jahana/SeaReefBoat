// Breaking Stones (Log ID 1, Quest ID 10), a line-for-line port of scripts/quests/bastok/Breaking_Stones.lua (quest rework
// B3b). Horatius : !pos -158 -6 -117 235. Kept as the Lua file has it: the trade section matches accepted and completed, so
// the Dangruf stone trade completes the quest again after each completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BreakingStones : QuestScript
    {
        public BreakingStones() : base(QuestLog.Bastok, "BREAKING_STONES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Breaking_Stones";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 400, Fame = 30, FameArea = FameArea.Bastok });

            // offered at Bastok fame level 2: option 0 accepts it
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Horatius).OnTrigger(QuestReply.ProgressEvent(100))
                .OnEventFinish(Zone.BastokMarkets, 100, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Horatius)
                    .OnTrade(q => q.Trade.Matches((Item.DangrufStone, 1)) ? QuestReply.ProgressEvent(101) : null)
                .OnEventFinish(Zone.BastokMarkets, 101, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
