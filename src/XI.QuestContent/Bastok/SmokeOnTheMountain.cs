// Smoke on the Mountain (Log ID 1, Quest ID 15), a line-for-line port of scripts/quests/bastok/Smoke_on_the_Mountain.lua
// (quest rework B3b). Hungry Wolf : !pos -25.861 -11 -30.172 237; Offa : !pos -281.628 -15.971 -140.607 235. Kept as the
// Lua file has it: the second section matches accepted and completed, so the sausage trade completes the quest again
// after each completion, and Offa's 222 plays once per zone-in (oncePerZone) while it is accepted or completed.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class SmokeOnTheMountain : QuestScript
    {
        public SmokeOnTheMountain() : base(QuestLog.Bastok, "SMOKE_ON_THE_MOUNTAIN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Smoke_on_the_Mountain";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 300, Title = Title.HotDog });

            b.State(QuestStatus.Available)
                .Npc(Zone.Metalworks, Npc.Metalworks.HungryWolf).OnTrigger(QuestReply.ProgressEvent(428))
                .OnEventFinish(Zone.Metalworks, 428, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.HungryWolf)
                    .OnTrade(q => q.Trade.Matches((Item.GalkanSausage, 1)) ? QuestReply.ProgressEvent(429) : null)
                .OnEventFinish(Zone.Metalworks, 429, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Offa).OnTrigger(QuestReply.Event(222).OncePerZone());
        }
    }
}
