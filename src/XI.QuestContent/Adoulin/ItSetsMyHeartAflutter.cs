// It Sets My Heart Aflutter (Log ID 9, Quest ID 79), a line-for-line port of
// scripts/quests/adoulin/It_Sets_My_Heart_Aflutter.lua (quest rework B3b Adoulin group 1).
// Adoulin fame quest cycle 1/3. Rala Waterways: Saldinor !pos -338.882 -1.000 -308.252 258 (no script; default 329).
// Available, with the Pioneer's Badge: Saldinor: 330, whose finish begins it on option 1. Once this quest,
// A Good Pair of Crocs and A Shot in the Dark are all completed and ADOULIN_FAME_QUEST_TRACKER is 0 (whatever this
// quest's status): 332, whose finish deletes the quest (delQuest) and begins it again (any option). Accepted:
// Saldinor: 333 (an Event); a trade of exactly two Twitherym Wings: 331, whose finish completes it (6 Adoulin fame,
// 200 bayld, 500 exp), confirms the trade and sets the tracker to 1. Completed: 329 (an Event).
// Kept as the Lua has it: the repeat section reads no status; the trade is confirmed after the completion; the reward's
// NOTE (about 100 quests to the fame cap).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class ItSetsMyHeartAflutter : QuestScript
    {
        public ItSetsMyHeartAflutter() : base(QuestLog.Adoulin, "IT_SETS_MY_HEART_AFLUTTER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/It_Sets_My_Heart_Aflutter";

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
            b.State(QuestStatus.Available, when: q => q.Player.HasKeyItem(KeyItem.PioneersBadge))
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.Saldinor).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(330);
                })
                .OnEventFinish(Zone.RalaWaterways, 330, q =>
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
                    q.Player.CharVar("ADOULIN_FAME_QUEST_TRACKER") == 0)
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.Saldinor).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(332);
                })
                .OnEventFinish(Zone.RalaWaterways, 332, q =>
                {
                    q.Player.DelQuest(QuestLog.Adoulin, "IT_SETS_MY_HEART_AFLUTTER");
                    q.Quest.Begin();
                });

            // Section: Questing
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.Saldinor).OnTrigger(q =>
                {
                    return QuestReply.Event(333);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.TwitherymWing, 2)))
                    {
                        return QuestReply.ProgressEvent(331);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RalaWaterways, 331, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetCharVar("ADOULIN_FAME_QUEST_TRACKER", 1);
                    }
                });

            // Section: Completed quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.Saldinor).OnTrigger(q =>
                {
                    return QuestReply.Event(329);
                });
        }
    }
}
