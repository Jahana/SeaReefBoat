// The Return of the Adventurer (Log ID 1, Quest ID 30), a line-for-line port of
// scripts/quests/bastok/The_Return_of_the_Adventurer.lua (quest rework B3b). Gwill : !pos -317.829 -15.948 -177.375 235.
// Lamepaue's hint list reads its status and keeps the read. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheReturnOfTheAdventurer : QuestScript
    {
        public TheReturnOfTheAdventurer() : base(QuestLog.Bastok, "THE_RETURN_OF_THE_ADVENTURER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Return_of_the_Adventurer";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30, FameArea = FameArea.Bastok, Item = Item.CottonHeadband, Title = Title.KulatzBridgeCompanion,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "FATHER_FIGURE") && q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Gwill).OnTrigger(QuestReply.ProgressEvent(242))
                .OnEventFinish(Zone.BastokMarkets, 242, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Gwill)
                    .OnTrade(q => q.Trade.Matches((Item.StickOfCinnamon, 1)) ? QuestReply.ProgressEvent(243) : null)
                .OnEventFinish(Zone.BastokMarkets, 243, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
