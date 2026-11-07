// Mean Machine (Log ID 1, Quest ID 25), a line-for-line port of scripts/quests/bastok/Mean_Machine.lua (quest rework B3b).
// Unlucky Rat : !pos -59.724 1.999 30.179 237. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class MeanMachine : QuestScript
    {
        public MeanMachine() : base(QuestLog.Bastok, "MEAN_MACHINE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Mean_Machine";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.ScrollOfWarp });

            // offered at Bastok fame level 2
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.Metalworks, Npc.Metalworks.UnluckyRat).OnTrigger(QuestReply.ProgressEvent(556))
                .OnEventFinish(Zone.Metalworks, 556, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.UnluckyRat)
                    .OnTrade(q => q.Trade.Matches((Item.VialOfSlimeOil, 1)) ? QuestReply.ProgressEvent(557) : null)
                    .OnTrigger(QuestReply.Event(559))
                // the Scroll of Warp refused with a full inventory: not completed, the oil kept
                .OnEventFinish(Zone.Metalworks, 557, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
