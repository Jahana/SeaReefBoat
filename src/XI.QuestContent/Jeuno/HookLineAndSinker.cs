// Hook, Line, and Sinker (Log ID 3, Quest ID 71), a line-for-line port of scripts/quests/jeuno/Hook_Line_and_Sinker.lua
// (quest rework B3b). Omer, Lower Jeuno: !pos -89.43 0 -124.1 245. Available once the CoP mission is past A Vessel
// Without a Captain: Omer's 10040 (0, Three-eyed Fish, Crescent Fish, 0, Egret Fishing Rod) begins it. Accepted: Omer's
// 10041 (0, 0, 0, 0, Egret Fishing Rod); the rod traded alone: 10042 (the same), whose finish completes it (3000 gil, Rod
// Retriever), fame +7 in the three nations and takes the rod. Completed: no section (Omer's default 206).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class HookLineAndSinker : QuestScript
    {
        public HookLineAndSinker() : base(QuestLog.Jeuno, "HOOK_LINE_AND_SINKER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Hook_Line_and_Sinker";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 3000, Title = Title.RodRetriever });

            // After completing A Vessel Without a Captain, talk to Omer to begin the quest.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.AVesselWithoutACaptain)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Omer).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(10040, 0, Item.ThreeEyedFish1, Item.CrescentFish, 0, Item.EgretFishingRod);
                })
                .OnEventFinish(Zone.LowerJeuno, 10040, q =>
                {
                    q.Quest.Begin();
                });

            // Obtain an Egret Fishing Rod, which drops from Sea Bishops and Krakens fished up on Qufim Island. No skill is required to hook these.
            // Return it to Omer to complete the quest.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Omer)
                    .OnTrigger(q =>
                    {
                        return QuestReply.ProgressEvent(10041, 0, 0, 0, 0, Item.EgretFishingRod);
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.EgretFishingRod))
                        {
                            return QuestReply.ProgressEvent(10042, 0, 0, 0, 0, Item.EgretFishingRod);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.LowerJeuno, 10042, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
