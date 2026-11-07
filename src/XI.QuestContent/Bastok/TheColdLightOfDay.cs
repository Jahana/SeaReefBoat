// The Cold Light of Day (Log ID 1, Quest ID 11), a line-for-line port of scripts/quests/bastok/The_Cold_Light_of_Day.lua
// (quest rework B3b). Malene : !pos -173 -5 64 235; Gwill : !pos -317.829 -15.948 -177.375 235. Kept as the Lua file has
// it: the second section matches accepted and completed, so each steam clock traded after completion completes the
// quest again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheColdLightOfDay : QuestScript
    {
        public TheColdLightOfDay() : base(QuestLog.Bastok, "THE_COLD_LIGHT_OF_DAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Cold_Light_of_Day";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 500, Title = Title.CrabCrusher });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Malene).OnTrigger(QuestReply.ProgressEvent(102))
                .OnEventFinish(Zone.BastokMarkets, 102, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Gwill).OnTrigger(QuestReply.Event(103))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Malene)
                    .OnTrade(q => q.Trade.Matches((Item.SteamClock, 1)) ? QuestReply.ProgressEvent(104) : null)
                    .OnTrigger(QuestReply.Event(102))
                .OnEventFinish(Zone.BastokMarkets, 104, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
