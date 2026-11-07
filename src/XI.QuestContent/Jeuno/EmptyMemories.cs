// Empty Memories (Log ID 3, Quest ID 70), a line-for-line port of scripts/quests/jeuno/Empty_Memories.lua (quest rework
// B3b). Harith : !pos -4.349 1 134.014 243. Available once CoP is at The Mothercrystals or later, or Below the
// Arks' Option var is over 0: Harith's 113 (option 1 begins). Accepted or completed: Harith's 114; a trade holding one of
// the six recollections (the first found, in the reward table's pairs() order) plays 108 (2000) when that recollection
// wants 2000 gil and the trade's gil is not 2000, else confirms the recollection, keeps the reward in the quest's local
// var rewardItem and plays 109 (the gil ones) / 110 (the others). Their finish gives the reward; when given: the trade
// confirmed, the gil taken (the gil ones), fame +5 in the three nations while accepted, and the quest completed (again
// when already completed).
// Kept Lua quirks: the recollection is found with hasItemQty(item, 1), exactly one of it (other items beside it still
// match, two of it do not); the gil is checked only for the recollections that want it and only for not 2000; the reward
// table is walked with pairs(), whose order (LuaJIT's, for these six item-id keys) decides which of two traded
// recollections is found first, and a gil one found first with the wrong gil answers 108 before a later row could
// match: Suffering (Hamayumi), Pain (Hysteroanima), Animosity (Dia Wand), Anxiety (Stone Gorget), Guilt (Terroanima),
// Fear (Psychoanima); the trade's own gil is never taken (confirmItem marks the recollection only), delGil takes the
// 2000 from the char; the finish completes the quest again when already completed.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class EmptyMemories : QuestScript
    {
        public EmptyMemories() : base(QuestLog.Jeuno, "EMPTY_MEMORIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Empty_Memories";

        // rewardItems: awarded item -> the recollection traded and the gil it wants (none: 0), in the order the Lua's
        // pairs(rewardItems) walks them under the server's LuaJIT: 17208, 5262, 17466, 13177, 5263, 5261
        // (jeuno.empty_memories#two_recollections records each adjacent pair)
        static readonly (int Reward, int TradeItem, int Gil)[] RewardItems =
        {
            (Item.Hamayumi, Item.RecollectionOfSuffering, 0),
            (Item.BottleOfHysteroanima, Item.RecollectionOfPain, 2000),
            (Item.DiaWand, Item.RecollectionOfAnimosity, 0),
            (Item.StoneGorget, Item.RecollectionOfAnxiety, 0),
            (Item.BottleOfTerroanima, Item.RecollectionOfGuilt, 2000),
            (Item.BottleOfPsychoanima, Item.RecollectionOfFear, 2000),
        };

        static readonly Dictionary<int, int> GilByReward = new Dictionary<int, int>();

        static EmptyMemories()
        {
            foreach (var r in RewardItems)
            {
                GilByReward[r.Reward] = r.Gil;
            }
        }

        static void MemoriesOnEventFinish(IQuestContext q)
        {
            int rewardItem = q.Quest.LocalVar("rewardItem");

            if (q.Player.GiveItem(rewardItem))
            {
                q.Trade.Confirm();

                if (GilByReward[rewardItem] != 0)
                {
                    q.Player.DelGil(GilByReward[rewardItem]);
                }

                if (q.Quest.Status == QuestStatus.Accepted)
                {
                    q.Player.AddFame(FameArea.Sandoria, 5);
                    q.Player.AddFame(FameArea.Bastok, 5);
                    q.Player.AddFame(FameArea.Windurst, 5);
                }

                q.Quest.Complete();
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Cop) >= Mission.Cop.TheMothercrystals ||
                    q.Player.MissionVar(MissionLog.Cop, Mission.Cop.BelowTheArks, "Option") > 0)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Harith).OnTrigger(QuestReply.ProgressEvent(113))
                .OnEventFinish(Zone.RuludeGardens, 113, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // status >= QUEST_ACCEPTED: accepted or completed
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Harith)
                    .OnTrade(q =>
                    {
                        foreach (var (rewardItemId, tradeItem, gil) in RewardItems)
                        {
                            if (q.Trade.HasItemQty(tradeItem, 1))
                            {
                                if (gil != 0 && q.Trade.Gil != gil)
                                {
                                    return QuestReply.Event(108, gil);
                                }

                                int eventId = gil != 0 ? 109 : 110;

                                q.Trade.ConfirmItem(tradeItem, 1);
                                q.Quest.SetLocalVar("rewardItem", rewardItemId);

                                return QuestReply.ProgressEvent(eventId);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(114))
                .OnEventFinish(Zone.RuludeGardens, 109, MemoriesOnEventFinish)
                .OnEventFinish(Zone.RuludeGardens, 110, MemoriesOnEventFinish);
        }
    }
}
