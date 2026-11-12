// The Price of Peace (Windurst 1-3, Log ID 2, Mission ID 2), a line-for-line port of
// scripts/missions/windurst/1_3_The_Price_of_Peace.lua (quest rework C3 N1c). Section 1 (no Windurst mission current,
// a Windurst citizen who has not completed this one): the finish of the gate guards' events (Port Windurst 78,
// Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) begins the mission with option 2 (not 1: kept as the
// Lua has it), sets the status to 1 and says YOU_ACCEPT_THE_MISSION; the guards' own events are not this mission's.
// Section 2 (current): the other NPCs of the four cities play their own events. Status 1: Leepe-Hoppe plays progress
// event 140, whose finish gives the Food and Drink Offerings and sets status 2; Kenapa-Keppa 145 and Ohbiru-Dohbiru
// 143. Status 2: Leepe-Hoppe plays 142; Laa Mozi (45) and Ghoo Pakya (49) of Giddeus play a progress cutscene whose
// finish takes the matching offering when the char has it, says OFFERED_UP_KEY_ITEM and counts it; the second
// offering sets status 3 and clears the count (a finish without the offering leaves the count as it was). Status 3:
// Ghoo Pakya (52) and Laa Mozi (48) play over their own events; Ohbiru-Dohbiru plays 144; trigger area 1 of Windurst
// Waters plays progress event 146, whose finish sets status 4. Status 4: Leepe-Hoppe plays 147 (important once); the
// gate guards of Port Windurst (114), Windurst Walls (116), Windurst Waters (Mokyokyo 148) and Windurst Woods (154)
// play a progress event whose finish completes the mission (1000 gil, rank 2). Last section (completed, Lost for
// Words not completed): the post-mission events of the four cities.
// KEPT LUA BUG (in 1-2's file, seen here): The Heart of the Matter's last section has no `currentMission == NONE` test,
// so with this mission current and 1-2 completed it also answers for these section-2 NPCs; both ports keep that.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class ThePriceOfPeace : MissionScript
    {
        public ThePriceOfPeace() : base(XI.Quests.MissionLog.Windurst, "THE_PRICE_OF_PEACE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/1_3_The_Price_of_Peace";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.ThePriceOfPeace;

        // handleAcceptMission
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 2)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 1);
                q.Text.Special(accepted);
            }
        }

        // offeringsTurnedIn
        static void OfferingsTurnedIn(IQuestContext q)
        {
            int offeringsVar = q.Mission.Var("OfferingsTurnedIn");

            if (q.Csid == 45 && q.Player.HasKeyItem(KeyItem.FoodOffering))
            {
                q.Player.DelKeyItem(KeyItem.FoodOffering);
                q.Text.Special(Text.Giddeus.OFFERED_UP_KEY_ITEM, KeyItem.FoodOffering);
                offeringsVar = offeringsVar + 1;
            }
            else if (q.Csid == 49 && q.Player.HasKeyItem(KeyItem.DrinkOffering))
            {
                q.Player.DelKeyItem(KeyItem.DrinkOffering);
                q.Text.Special(Text.Giddeus.OFFERED_UP_KEY_ITEM, KeyItem.DrinkOffering);
                offeringsVar = offeringsVar + 1;
            }

            if (offeringsVar == 2)
            {
                q.Player.SetMissionStatus(AreaId, 3);
                q.Mission.SetVar("OfferingsTurnedIn", 0);
            }
            else
            {
                q.Mission.SetVar("OfferingsTurnedIn", offeringsVar);
            }
        }

        // the finish of the gate guards' progress events at status 4
        static void CompleteMission(IQuestContext q)
        {
            q.Mission.Complete();
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 1000, Rank = 2 });

            // Player is offered mission from a gate guard
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .OnEventFinish(Zone.PortWindurst, 78,
                    q => HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93,
                    q => HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111,
                    q => HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114,
                    q => HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            // Mission has been accepted, these NPCs dialogue does not change during mission
            b.State(current: true)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(110))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(112))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(111))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(113))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(115))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(114))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(113))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(112))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(138))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(136))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(137))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(139))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(151))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(150))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(153))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(152));

            // Speak to Leepe-Hoppe on the roof of the Rhinostery near Waters HP #3
            b.State(current: true, status: 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(QuestReply.Event(145))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).OnTrigger(QuestReply.ProgressEvent(140))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(QuestReply.Event(143))
                .OnEventFinish(Zone.WindurstWaters, 140, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FoodOffering);
                    q.Player.GiveKeyItem(KeyItem.DrinkOffering);
                    q.Player.SetMissionStatus(AreaId, 2);
                });

            // Take the Offerings to Giddeus and give them to the Yagudo NPCs
            b.State(current: true, status: 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).OnTrigger(QuestReply.ProgressEvent(142))
                .Npc(Zone.Giddeus, Npc.Giddeus.GhooPakya).OnTrigger(QuestReply.ProgressCutscene(49))
                .Npc(Zone.Giddeus, Npc.Giddeus.LaaMozi).OnTrigger(QuestReply.ProgressCutscene(45))
                .OnEventFinish(Zone.Giddeus, 45, OfferingsTurnedIn)
                .OnEventFinish(Zone.Giddeus, 49, OfferingsTurnedIn);

            // Offerings were delivered
            b.State(current: true, status: 3)
                .Npc(Zone.Giddeus, Npc.Giddeus.GhooPakya).OnTrigger(QuestReply.Event(52).ReplaceDefault())
                .Npc(Zone.Giddeus, Npc.Giddeus.LaaMozi).OnTrigger(QuestReply.Event(48).ReplaceDefault())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(QuestReply.Event(144))
                .OnTriggerAreaEnter(Zone.WindurstWaters, 1, q => QuestReply.ProgressEvent(146))
                .OnEventFinish(Zone.WindurstWaters, 146, q => q.Player.SetMissionStatus(AreaId, 4));

            // Finish mission with a gate guard
            b.State(current: true, status: 4)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.ProgressEvent(114))
                .OnEventFinish(Zone.PortWindurst, 114, CompleteMission)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.ProgressEvent(116))
                .OnEventFinish(Zone.WindurstWalls, 116, CompleteMission)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe)
                .OnTrigger(QuestReply.Event(147).ImportantOnce())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.ProgressEvent(148))
                .OnEventFinish(Zone.WindurstWaters, 148, CompleteMission)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.ProgressEvent(154))
                .OnEventFinish(Zone.WindurstWoods, 154, CompleteMission);

            // All of the optional post-mission dialogue
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.LostForWords))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(117))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(116))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(118))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(120))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(118))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(119))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(150))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(152))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(154))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(157))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(158))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(131));
        }
    }
}
