// What Price Loyalty (Log ID 7, Quest ID 50), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_10_What_Price_Loyalty.lua (quest rework B3b Crystal War group 5). Bastok Markets
// [S]: Gentle Tiger !pos -203.9 -10 2.2 (default 109). North Gustaberg [S]: Roderich !pos -400 40 -90.4 (default 111).
// Xarcabard [S]: Forbidding Portal !pos 320 -10.8 158.7 (default NO_RESPONSE). Available, Beneath the Mask completed
// and the current WotG mission Fate in Haze or later: Gentle Tiger 348, whose finish begins it and gives the Sack of
// Victuals. Accepted, by Prog: 0 with the sack: Gentle Tiger 354 (an Event); Roderich 10, whose finish sets Prog 1 and
// takes the sack. 1: Roderich 11 (an Event); Gentle Tiger 349 (-> 2). 2: Gentle Tiger 355 (an Event); any zone-in to
// Xarcabard [S]: 8, whose finish sets Prog 3 and gives the Commander's Endorsement. 3 with the endorsement: Gentle
// Tiger 355; the Forbidding Portal 9 (-> 4). 4: Gentle Tiger: the endorsement held: 355; else 357, whose finish gives
// it again; Everbloom Hollow's 10000 finished: Prog 5 and the char set down by the portal in Xarcabard [S] ("What Price
// Loyalty instance is not implemented currently"). 5: the zone-in to Xarcabard [S] from Everbloom Hollow: 10 (-> 6). 6:
// Gentle Tiger 350, whose finish completes it (a Fourth Staff). Completed: Gentle Tiger 356 (an Event: it ties with The
// Truth Lies Hid's Prog 0 212 and ranks above Bonds of Mythril's completed 214, replaceDefault).
// Kept as the Lua has it: 348 begins on any option; the Prog 2 zone-in plays 8 from any zone; Prog 0 without the sack
// and Prog 3 without the endorsement have no section; nothing but the instance's 10000 sets Prog 5; 350 completes with
// no check of its result.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class WhatPriceLoyalty : QuestScript
    {
        public WhatPriceLoyalty() : base(QuestLog.CrystalWar, "WHAT_PRICE_LOYALTY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_10_What_Price_Loyalty";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6);

            b.Reward(new QuestReward
            {
                Item = Item.FourthStaff,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "BENEATH_THE_MASK") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.FateInHaze)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(348))
                .OnEventFinish(Zone.BastokMarketsS, 348, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.SackOfVictuals);
                });

            b.State(QuestStatus.Accepted, step: 0, when: q => q.Player.HasKeyItem(KeyItem.SackOfVictuals))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(354))
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Roderich).OnTrigger(QuestReply.ProgressEvent(10))
                .OnEventFinish(Zone.NorthGustabergS, 10, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.DelKeyItem(KeyItem.SackOfVictuals);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Roderich).OnTrigger(QuestReply.Event(11))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(349))
                .OnEventFinish(Zone.BastokMarketsS, 349, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(355))
                .OnZoneIn(Zone.XarcabardS, q => QuestReply.Event(8))
                .OnEventFinish(Zone.XarcabardS, 8, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.GiveKeyItem(KeyItem.CommandersEndorsement);
                });

            b.State(QuestStatus.Accepted, step: 3, when: q => q.Player.HasKeyItem(KeyItem.CommandersEndorsement))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(355))
                .Npc(Zone.XarcabardS, Npc.XarcabardS.ForbiddingPortal).OnTrigger(QuestReply.ProgressEvent(9))
                .OnEventFinish(Zone.XarcabardS, 9, q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CommandersEndorsement))
                    {
                        return QuestReply.Event(355);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(357);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 357, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CommandersEndorsement);
                })
                // What Price Loyalty instance is not implemented currently.
                .OnEventFinish(Zone.EverbloomHollow, 10000, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.SetPos(320.000, -10.835, 158.699, 0, Zone.XarcabardS);
                });

            b.State(QuestStatus.Accepted, step: 5)
                .OnZoneIn(Zone.XarcabardS, q =>
                {
                    if (q.Player.PreviousZone == Zone.EverbloomHollow)
                    {
                        return QuestReply.Event(10);
                    }
                    return null;
                })
                .OnEventFinish(Zone.XarcabardS, 10, q =>
                {
                    q.Quest.SetStep(6);
                });

            b.State(QuestStatus.Accepted, step: 6)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(350))
                .OnEventFinish(Zone.BastokMarketsS, 350, q =>
                {
                    q.Quest.Complete();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(356));
        }
    }
}
