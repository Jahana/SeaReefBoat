// A Shot in the Dark (Log ID 9, Quest ID 96), a line-for-line port of scripts/quests/adoulin/A_Shot_in_the_Dark.lua
// (quest rework B3b Adoulin group 1). Adoulin fame quest cycle 3/3.
// Eastern Adoulin: Pudith !pos -109.533 -0.150 56.939 257 (no script, no default).
// Available: Pudith: 3011, whose finish begins it on option 1. Once It Sets My Heart Aflutter, A Good Pair of Crocs and
// this quest are all completed and ADOULIN_FAME_QUEST_TRACKER is 2 (whatever this quest's status): 3013, whose finish
// deletes the quest (delQuest) and begins it again (any option). Accepted: Pudith: 3014 (an Event); a trade of exactly
// an Umbril Ooze: 3012, whose finish completes it (6 Adoulin fame, 200 bayld, 500 exp), confirms the trade and sets the
// tracker to 0. Completed: 3010 (an Event).
// Kept as the Lua has it: the repeat section reads no status; the trade is confirmed after the completion; the reward's
// NOTE (about 100 quests to the fame cap).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class AShotInTheDark : QuestScript
    {
        public AShotInTheDark() : base(QuestLog.Adoulin, "A_SHOT_IN_THE_DARK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/A_Shot_in_the_Dark";

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
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Pudith).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3011);
                })
                .OnEventFinish(Zone.EasternAdoulin, 3011, q =>
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
                    q.Player.CharVar("ADOULIN_FAME_QUEST_TRACKER") == 2)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Pudith).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3013);
                })
                .OnEventFinish(Zone.EasternAdoulin, 3013, q =>
                {
                    q.Player.DelQuest(QuestLog.Adoulin, "A_SHOT_IN_THE_DARK");
                    q.Quest.Begin();
                });

            // Section: Questing
            b.State(QuestStatus.Accepted)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Pudith).OnTrigger(q =>
                {
                    return QuestReply.Event(3014);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.UmbrilOoze))
                    {
                        return QuestReply.ProgressEvent(3012);
                    }
                    return null;
                })
                .OnEventFinish(Zone.EasternAdoulin, 3012, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetCharVar("ADOULIN_FAME_QUEST_TRACKER", 0);
                    }
                });

            // Section: Completed quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Pudith).OnTrigger(q =>
                {
                    return QuestReply.Event(3010);
                });
        }
    }
}
