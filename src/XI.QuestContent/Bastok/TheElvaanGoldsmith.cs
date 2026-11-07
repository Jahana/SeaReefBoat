// The Elvaan Goldsmith (Log ID 1, Quest ID 13), a line-for-line port of scripts/quests/bastok/The_Elvaan_Goldsmith.lua
// (quest rework B2). Michea : !pos -298 -16 -157 235. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheElvaanGoldsmith : QuestScript
    {
        public TheElvaanGoldsmith() : base(QuestLog.Bastok, "THE_ELVAAN_GOLDSMITH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Elvaan_Goldsmith";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 180 });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Michea).OnTrigger(QuestReply.ProgressEvent(215))
                .OnEventFinish(Zone.BastokMarkets, 215, q => q.Quest.Begin());

            // accepted, or completed at fame level 1 once the char has zoned since: the repeat
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed, when: q => q.Player.FameLevel(FameArea.Bastok) == 1 && !q.Quest.MustZone)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Michea)
                    .OnTrade(q => q.Trade.Matches((Item.CopperIngot, 1)) ? QuestReply.ProgressEvent(216) : null)
                    .OnTrigger(QuestReply.Event(215))
                .OnEventFinish(Zone.BastokMarkets, 216, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Quest.SetMustZone();
                    }
                });
        }
    }
}
