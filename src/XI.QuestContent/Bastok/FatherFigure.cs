// Father Figure (Log ID 1, Quest ID 29), a line-for-line port of scripts/quests/bastok/Father_Figure.lua (quest rework
// B3b). Michea : !pos -298 -16 -157 235. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class FatherFigure : QuestScript
    {
        public FatherFigure() : base(QuestLog.Bastok, "FATHER_FIGURE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Father_Figure";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Gil = 2200 });

            // offered at Bastok fame level 2 once The Elvaan Goldsmith is completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_ELVAAN_GOLDSMITH") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Michea).OnTrigger(QuestReply.ProgressEvent(240))
                .OnEventFinish(Zone.BastokMarkets, 240, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Michea)
                    .OnTrade(q => q.Trade.Matches((Item.SilverIngot, 1)) ? QuestReply.ProgressEvent(241) : null)
                .OnEventFinish(Zone.BastokMarkets, 241, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
