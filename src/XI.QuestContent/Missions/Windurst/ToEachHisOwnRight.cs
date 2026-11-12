// To Each His Own Right (Windurst 3-1, Log ID 2, Mission ID 10), a line-for-line port of
// scripts/missions/windurst/3_1_To_Each_His_Own_Right.lua (quest rework C3 N3). Rakoh Buuma !pos 106 -5 -23 241;
// Mokyokyo !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Kupipi !pos 2
// 0.1 30 242; Rhy Epocan !pos 2 -48 14 242; Hakkuru-Rinkuru !pos -111 -4 101 240; Trap Door !pos 22.310 -1.087 -14.320
// 151. Reward: 450 rank points. Section 1 (no Windurst mission current, a Windurstian who has not completed this
// mission): the finish of the gate guards' events (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst
// Woods 114) with option 10 begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Castle
// Oztroja's 43 finish (the handles _47b and _47c start 43 themselves at status 3) sets status 4. Heaven's Tower: Foo
// Beibo, Shaz Norem, Ufu Koromoa and Vahn Paineesha at status 2 play 112, 110, 111 and 109 (events); Kupipi at 0 plays
// 103 (progress, the Starway Stairway Bauble in slot 2), at 1 104, at 4 113 (events); Rhy Epocan at 1 plays 107
// (progress), at 2 108 (event), at 4 114 (progress). 103's finish sets status 1 and gives the bauble, 107's sets status
// 2, 114's completes the mission. Port Windurst: Hakkuru-Rinkuru at 2 plays 147 (progress), at 3 148; 147's finish sets
// status 3. Windurst Woods: Rakoh Buuma 182, Sola Jaab 184, Tih Pikeh 183 (events). Section 3 (no Windurst mission
// current, a Windurstian, this mission completed, Written in the Stars not completed): Foo Beibo 162, Rhy Epocan 115,
// Shaz Norem 117, Ufu Koromoa 118, Vahn Paineesha 116, each over its default. Kept as the Lua has it: sections 1 and 3
// compare the nation with mission.areaId (the log id, 2, which is xi.nation.WINDURST).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class ToEachHisOwnRight : MissionScript
    {
        public ToEachHisOwnRight() : base(XI.Quests.MissionLog.Windurst, "TO_EACH_HIS_OWN_RIGHT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/3_1_To_Each_His_Own_Right";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.ToEachHisOwnRight;

        // handleAcceptMission: option 10 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 10)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // a Sibyl Guard's line at status 2 (they only have it once event 107 has played)
        static QuestReply GuardLine(IQuestContext q, int csid) =>
            q.Player.MissionStatus(AreaId) == 2 ? QuestReply.Event(csid) : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 450 });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .OnEventFinish(Zone.PortWindurst, 78, q =>
                    HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93, q =>
                    HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111, q =>
                    HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114, q =>
                    HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            b.State(current: true)
                // NOTE: This event is fired from NPCs _47b and _47c.  This is to allow for trap door opening and
                // immediately triggering the CS.
                // TODO: Find an alternative method for handling the event.
                .OnEventFinish(Zone.CastleOztroja, 43, q => q.Player.SetMissionStatus(AreaId, 4))
                // The Sibyl Guards only have these lines once event 107 has played.
                .Npc(Zone.HeavensTower, Npc.HeavensTower.FooBeibo).OnTrigger(q => GuardLine(q, 112))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(103, 0, 0, KeyItem.StarwayStairwayBauble);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(104);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(113);
                    }

                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.RhyEpocan).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(108);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(114);
                    }

                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.ShazNorem).OnTrigger(q => GuardLine(q, 110))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.UfuKoromoa).OnTrigger(q => GuardLine(q, 111))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.VahnPaineesha).OnTrigger(q => GuardLine(q, 109))
                .OnEventFinish(Zone.HeavensTower, 103, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.StarwayStairwayBauble);
                })
                .OnEventFinish(Zone.HeavensTower, 107, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.HeavensTower, 114, q => q.Mission.Complete())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(147);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(148);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 147, q => q.Player.SetMissionStatus(AreaId, 3))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(q => QuestReply.Event(182))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(q => QuestReply.Event(184))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(q => QuestReply.Event(183));

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(AreaId, Mission.Windurst.WrittenInTheStars))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.FooBeibo)
                .OnTrigger(q => QuestReply.Event(162).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.RhyEpocan)
                .OnTrigger(q => QuestReply.Event(115).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.ShazNorem)
                .OnTrigger(q => QuestReply.Event(117).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.UfuKoromoa)
                .OnTrigger(q => QuestReply.Event(118).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.VahnPaineesha)
                .OnTrigger(q => QuestReply.Event(116).ReplaceDefault());
        }
    }
}
