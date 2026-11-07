// A Squire's Test (Log ID 0, Quest ID 10), a line-for-line port of scripts/quests/sandoria/A_Squires_Test.lua (quest
// rework B3b, San d'Oria group 2). Balasiel : !pos -136 -11 64 230. Available: 616 (option 0 begins it, any other option
// sets the quest var Option 1), then 631 (option 0 begins it). Accepted: the message GO_TO_KING_RANPERRES (the Revival
// Tree Root); exactly one Revival Tree Root plays 617: completed (Spatha, fame +20, Knight in Training), the trade
// confirmed. No section once completed: A Squire's Test II answers Balasiel. Celyddon's script reads the status (stays).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ASquiresTest : QuestScript
    {
        public ASquiresTest() : base(QuestLog.Sandoria, "A_SQUIRES_TEST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Squires_Test";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.Spatha, Title = Title.KnightInTraining });

            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(616);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(631);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 616, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 631, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.REVIVAL_TREE_ROOT)
                        if (q.Trade.HasExactly(Item.RevivalTreeRoot))
                        {
                            return QuestReply.ProgressEvent(617);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.MessageName(Text.SouthernSanDoria.GO_TO_KING_RANPERRES, Item.RevivalTreeRoot))
                .OnEventFinish(Zone.SouthernSanDoria, 617, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
