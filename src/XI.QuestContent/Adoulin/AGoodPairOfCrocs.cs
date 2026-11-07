// A Good Pair of Crocs (Log ID 9, Quest ID 93), a line-for-line port of scripts/quests/adoulin/A_Good_Pair_of_Crocs.lua
// (quest rework B3b Adoulin group 1). Adoulin fame quest cycle 2/3.
// Eastern Adoulin: Felmsy !pos -53.111 -0.150 88.456 257 (no script, no default).
// Available: Felmsy: 3001, whose finish begins it on option 1. Once It Sets My Heart Aflutter, this quest and
// A Shot in the Dark are all completed and ADOULIN_FAME_QUEST_TRACKER is 1 (whatever this quest's status): 3003, whose
// finish deletes the quest (delQuest) and begins it again (any option). Accepted: Felmsy: 3004 (an Event); a trade of
// exactly a Velkk Necklace or exactly a Velkk Mask: 3002, whose finish completes it (6 Adoulin fame, 200 bayld,
// 500 exp), confirms the trade and sets the tracker to 2. Completed: 3000 (an Event).
// Kept as the Lua has it: the repeat section reads no status; the trade is confirmed after the completion; the reward's
// NOTE (about 100 quests to the fame cap).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class AGoodPairOfCrocs : QuestScript
    {
        public AGoodPairOfCrocs() : base(QuestLog.Adoulin, "A_GOOD_PAIR_OF_CROCS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/A_Good_Pair_of_Crocs";

        protected override void Build(QuestScriptBuilder b)
        {
            // NOTE: It is reported that to reach max fame (~610) you must complete this cycle of quests around 30
            // times, for a total of ~100 quests. 610 / 100 = ~6 fame each
            b.Reward(new QuestReward
            {
                Fame = 6,
                FameArea = FameArea.Adoulin,
                Bayld = 200,
                Exp = 500,
            });

            // Section: Begin quest (First time)
            b.State(QuestStatus.Available)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Felmsy).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3001);
                })
                .OnEventFinish(Zone.EasternAdoulin, 3001, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Begin quest (Repeated)
            b.AnyState(when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Adoulin, "IT_SETS_MY_HEART_AFLUTTER") &&
                    q.Player.HasCompletedQuest(QuestLog.Adoulin, "A_GOOD_PAIR_OF_CROCS") &&
                    q.Player.HasCompletedQuest(QuestLog.Adoulin, "A_SHOT_IN_THE_DARK") &&
                    q.Player.CharVar("ADOULIN_FAME_QUEST_TRACKER") == 1)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Felmsy).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3003);
                })
                .OnEventFinish(Zone.EasternAdoulin, 3003, q =>
                {
                    q.Player.DelQuest(QuestLog.Adoulin, "A_GOOD_PAIR_OF_CROCS");
                    q.Quest.Begin();
                });

            // Section: Questing
            b.State(QuestStatus.Accepted)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Felmsy).OnTrigger(q =>
                {
                    return QuestReply.Event(3004);
                })
                .OnTrade(q =>
                {
                    if (
                        q.Trade.HasExactly(Item.VelkkNecklace) ||
                        q.Trade.HasExactly(Item.VelkkMask))
                    {
                        return QuestReply.ProgressEvent(3002);
                    }
                    return null;
                })
                .OnEventFinish(Zone.EasternAdoulin, 3002, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetCharVar("ADOULIN_FAME_QUEST_TRACKER", 2);
                    }
                });

            // Section: Completed quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Felmsy).OnTrigger(q =>
                {
                    return QuestReply.Event(3000);
                });
        }
    }
}
