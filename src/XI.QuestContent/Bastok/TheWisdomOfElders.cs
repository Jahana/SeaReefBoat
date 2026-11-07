// The Wisdom of Elders (Log ID 1, Quest ID 36), a line-for-line port of scripts/quests/bastok/The_Wisdom_of_Elders.lua
// (quest rework B3b). Benita : !pos 49.692 -4.771 36.189 236; Tete : !pos 15.249 -2.097 43.012 236. Kept as the Lua file
// has it: nothing reads the Prog var Tete's 175 sets.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheWisdomOfElders : QuestScript
    {
        public TheWisdomOfElders() : base(QuestLog.Bastok, "THE_WISDOM_OF_ELDERS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Wisdom_of_Elders";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.TravelersHat });

            b.State(QuestStatus.Available)
                .Npc(Zone.PortBastok, Npc.PortBastok.Benita).OnTrigger(QuestReply.ProgressEvent(174))
                .OnEventFinish(Zone.PortBastok, 174, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Benita)
                    .OnTrade(q => q.Trade.Matches((Item.PinchOfBombAsh, 1)) ? QuestReply.ProgressEvent(176) : null)
                .Npc(Zone.PortBastok, Npc.PortBastok.Tete).OnTrigger(QuestReply.Event(175).ImportantOnce())
                .OnEventFinish(Zone.PortBastok, 175, q =>
                {
                    if (q.Quest.Var("Prog") == 0)
                    {
                        q.Quest.SetVar("Prog", 1);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 176, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
