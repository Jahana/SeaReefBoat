// The Quadav's Curse (Log ID 1, Quest ID 4), a line-for-line port of scripts/quests/bastok/The_Quadavs_Curse.lua (quest
// rework B3b). Corann : !pos 90.935 -8.772 32.564 236. Out of One's Shell reads its status and keeps the read. The Lua
// file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheQuadavsCurse : QuestScript
    {
        public TheQuadavsCurse() : base(QuestLog.Bastok, "THE_QUADAVS_CURSE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Quadavs_Curse";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.BronzeSubligar });

            b.State(QuestStatus.Available)
                .Npc(Zone.PortBastok, Npc.PortBastok.Corann).OnTrigger(QuestReply.ProgressEvent(80))
                .OnEventFinish(Zone.PortBastok, 80, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Corann)
                    .OnTrade(q => q.Trade.Matches((Item.QuadavBackplate, 1)) ? QuestReply.ProgressEvent(81) : null)
                .OnEventFinish(Zone.PortBastok, 81, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });

            // a new default text until Out of One's Shell is completed
            b.State(QuestStatus.Completed, when: q => !q.Player.HasCompletedQuest(QuestLog.Bastok, "OUT_OF_ONES_SHELL"))
                .Npc(Zone.PortBastok, Npc.PortBastok.Corann).OnTrigger(QuestReply.Event(87).ReplaceDefault());
        }
    }
}
