// Brygid the Stylist (Log ID 1, Quest ID 44), a line-for-line port of scripts/quests/bastok/Brygid_the_Stylist.lua
// (quest rework B3b). Brygid : !pos -90 -4 -108 235. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BrygidTheStylist : QuestScript
    {
        public BrygidTheStylist() : base(QuestLog.Bastok, "BRYGID_THE_STYLIST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Brygid_the_Stylist";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.Gloves, Title = Title.BrygidApproved });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Brygid).OnTrigger(QuestReply.ProgressEvent(310))
                .OnEventFinish(Zone.BastokMarkets, 310, q => q.Quest.Begin());

            // a Robe and a Bronze Subligar worn together: 311, else the offer's 310 again
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Brygid).OnTrigger(q =>
                    q.Player.EquipId(Slot.Body) == Item.Robe && q.Player.EquipId(Slot.Legs) == Item.BronzeSubligar
                        ? QuestReply.ProgressEvent(311)
                        : QuestReply.Event(310))
                .OnEventFinish(Zone.BastokMarkets, 311, q => q.Quest.Complete());
        }
    }
}
