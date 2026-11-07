// A Foreman's Best Friend (Log ID 1, Quest ID 9), a line-for-line port of scripts/quests/bastok/A_Foremans_Best_Friend.lua
// (quest rework B3b). Gudav : !pos -3.286 1.407 50.591 236; Dehlner : !pos -4.023 4.898 50.129 236. The Lua file has no
// known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class AForemansBestFriend : QuestScript
    {
        public AForemansBestFriend() : base(QuestLog.Bastok, "A_FOREMANS_BEST_FRIEND") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/A_Foremans_Best_Friend";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok });

            b.State(QuestStatus.Available)
                .Npc(Zone.PortBastok, Npc.PortBastok.Gudav).OnTrigger(QuestReply.ProgressEvent(110))
                .OnEventFinish(Zone.PortBastok, 110, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Dehlner).OnTrigger(QuestReply.Event(111))
                .Npc(Zone.PortBastok, Npc.PortBastok.Gudav)
                    .OnTrade(q => q.Trade.Matches((Item.DogCollar, 1)) ? QuestReply.ProgressEvent(112) : null)
                // the Map of the Gusgen Mines, or 2000 exp when it is held already
                .OnEventFinish(Zone.PortBastok, 112, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        if (q.Player.HasKeyItem(KeyItem.MapOfTheGusgenMines))
                        {
                            q.Player.AddExp(2000);
                        }
                        else
                        {
                            q.Player.GiveKeyItem(KeyItem.MapOfTheGusgenMines);
                        }
                    }
                });
        }
    }
}
