// Silence of the Rams (Log ID 1, Quest ID 48), a line-for-line port of scripts/quests/bastok/Silence_of_the_Rams.lua
// (quest rework B3b). Paujean : !pos -93.738 4.649 34.373 236. Lower Jeuno's Vola reads its status and keeps the read.
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class SilenceOfTheRams : QuestScript
    {
        public SilenceOfTheRams() : base(QuestLog.Bastok, "SILENCE_OF_THE_RAMS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Silence_of_the_Rams";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Norg, Item = Item.PurpleBelt, Title = Title.PurpleBelt });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Norg) >= 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Paujean).OnTrigger(QuestReply.ProgressEvent(195))
                .OnEventFinish(Zone.PortBastok, 195, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Paujean)
                    .OnTrade(q => q.Trade.Matches((Item.LumberingHorn, 1), (Item.RampagingHorn, 1)) ? QuestReply.ProgressEvent(196) : null)
                    .OnTrigger(QuestReply.MessageName(Text.PortBastok.PAUJEAN_DIALOG_1))
                .OnEventFinish(Zone.PortBastok, 196, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
