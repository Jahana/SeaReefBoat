// Her Memories: Azure Footfalls (Log ID 7, Quest ID 70), a line-for-line port of
// scripts/quests/crystalWar/Her_Memories_Azure_Footfalls.lua (quest rework B3b Crystal War, draft). Bastok Markets
// [S]: Adelbrecht !pos -325.8 -12.6 -77.0. North Gustaberg [S]: Gebhardt !pos 206.3 -20.8 669.1 (default 110), Roderich
// !pos -400.0 40.0 -90.4 (default 111), Barricade !pos -515.0 38.0 583.3 (default NO_WAY_PAST_BARRIER).
// Available, the WotG mission Her Memories current and the Bastok campaign allegiance (2): the Bastok Markets [S]
// zone-in from North Gustaberg [S] at Prog 0: 358 (-> Prog 1). Adelbrecht: Prog 1: 359, whose finish begins it and sets
// Prog 2. Accepted: Gebhardt: Prog 2: 14 (-> 3). Roderich: Prog 3: 15 (-> 4). Barricade: Prog 4: 16 (-> 5).
// Adelbrecht: Prog 5: 360, whose finish completes it (the Large Memory Fragment) and runs
// xi.wotg.helpers.checkMemoryFragments.
// Kept as the Lua has it: Prog 1 written while available; 359 begins on any option; 360's fragment check runs whether
// or not the completion did (with only a key item in the reward it always does); no completed section.
// Rival containers: What Price Loyalty at Roderich (10, Progress, ties 15; 11, an Event, under it): golden flow
// #rivals. The Fighting Fourth's lines at Adelbrecht, Gebhardt, Roderich and the Barricade are its completed
// replaceDefault replies (#edges, the main flow).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HerMemoriesAzureFootfalls : QuestScript
    {
        public HerMemoriesAzureFootfalls() : base(QuestLog.CrystalWar, "HER_MEMORIES_AZURE_FOOTFALLS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Her_Memories_Azure_Footfalls";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.LargeMemoryFragment4,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Wotg) == Mission.Wotg.HerMemories &&
                    q.Player.CampaignAllegiance == 2)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Adelbrecht).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(359);
                    }
                    return null;
                })
                .OnZoneIn(Zone.BastokMarketsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.NorthGustabergS &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.Event(358);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 358, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BastokMarketsS, 359, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Gebhardt).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    return null;
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Roderich).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(15);
                    }
                    return null;
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Barricade).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(16);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthGustabergS, 14, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.NorthGustabergS, 15, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.NorthGustabergS, 16, q =>
                {
                    q.Quest.SetStep(5);
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Adelbrecht).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(360);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 360, q =>
                {
                    q.Quest.Complete();
                    q.Player.CheckMemoryFragments();
                });
        }
    }
}
