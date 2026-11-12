// Written in the Stars (Windurst 3-2, Log ID 2, Mission ID 11), a line-for-line port of
// scripts/missions/windurst/3_2_Written_in_the_Stars.lua (quest rework C3 N3). Rakoh Buuma !pos 106 -5 -23 241;
// Mokyokyo !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Zubaba !pos 15
// -27 18 242; Gate of Light !pos -331 0 139 192. Reward: 400 rank points (the base; a first completion gets 100 more
// from 135's finish). Section 1 (no Windurst mission current, a Windurstian): Kiwawa, Nebibi and Zubaba play 138, 147
// and 136 over their defaults when this mission is completed and A New Journey is not; the finish of the gate guards'
// events (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 11 begins the
// mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current, neither this mission nor A New Journey
// completed): Kiwawa and Nebibi at status 1 play 124 and 134; Kupipi plays 120 (events); Zubaba at 0 plays 121
// (progress), at 1 122 (event), at 2 135 (progress). 121's finish gives the Charm of Light and sets status 1; 135's
// completes the mission and, when that succeeds, adds 100 rank points. Inner Horutoto Ruins: the Gate of Light _5ci at
// status 1 plays 41 (progress, the charm in slot 1), whose finish sets status 2 and takes the charm. Windurst Woods:
// Rakoh Buuma 188, Sola Jaab 190, Tih Pikeh 189 (events). Section 3 (current, and this mission or A New Journey
// completed: a repeat): Zubaba takes a trade of exactly three Rusty Daggers at status 3 with 151 (progress); his
// trigger at 0 plays 257 (progress, the dagger in slot 1), at 3 150 (event, the same parameters). 151's finish
// completes the mission and, when that succeeds, takes the trade; 257's sets status 3. Kept as the Lua has it: section
// 1 has no completed check (Kiwawa, Nebibi and Zubaba test it themselves) and compares the nation with mission.areaId
// (the log id, 2, which is xi.nation.WINDURST).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class WrittenInTheStars : MissionScript
    {
        public WrittenInTheStars() : base(XI.Quests.MissionLog.Windurst, "WRITTEN_IN_THE_STARS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/3_2_Written_in_the_Stars";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.WrittenInTheStars;

        // handleAcceptMission: option 11 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 11)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // this mission completed and A New Journey not completed
        static bool BetweenMissions(IQuestContext q) =>
            q.Player.HasCompletedMission(AreaId, MissionId) &&
            !q.Player.HasCompletedMission(AreaId, Mission.Windurst.ANewJourney);

        protected override void Build(MissionScriptBuilder b)
        {
            // Base Rank Point Value, First time completion awards 500
            b.Reward(new MissionReward { RankPoints = 400 });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kiwawa).OnTrigger(q =>
                    BetweenMissions(q) ? QuestReply.Event(138).ReplaceDefault() : null)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Nebibi).OnTrigger(q =>
                    BetweenMissions(q) ? QuestReply.Event(147).ReplaceDefault() : null)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Zubaba).OnTrigger(q =>
                    BetweenMissions(q) ? QuestReply.Event(136).ReplaceDefault() : null)
                .OnEventFinish(Zone.PortWindurst, 78, q =>
                    HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93, q =>
                    HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111, q =>
                    HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114, q =>
                    HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            // Has not completed A New Journey, and first time running this mission
            b.State(current: true, when: q =>
                    !q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(AreaId, Mission.Windurst.ANewJourney))
                // Event 121 gives these two their lines. Event 41 takes them away again.
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kiwawa).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(124) : null)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q => QuestReply.Event(120))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Nebibi).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(134) : null)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Zubaba).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(121);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(122);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(135);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 121, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CharmOfLight);
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.HeavensTower, 135, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddRankPoints(100);
                    }
                })
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5ci).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 1
                        ? QuestReply.ProgressEvent(41, 0, KeyItem.CharmOfLight)
                        : null)
                .OnEventFinish(Zone.InnerHorutotoRuins, 41, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.DelKeyItem(KeyItem.CharmOfLight);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(q => QuestReply.Event(188))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(q => QuestReply.Event(190))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(q => QuestReply.Event(189));

            // Repeating or has completed the next mission
            b.State(current: true, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) ||
                    q.Player.HasCompletedMission(AreaId, Mission.Windurst.ANewJourney))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Zubaba)
                .OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Trade.Matches((Item.RustyDagger, 3)))
                    {
                        return QuestReply.ProgressEvent(151);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(257, 0, Item.RustyDagger);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(150, 0, Item.RustyDagger);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 151, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .OnEventFinish(Zone.HeavensTower, 257, q => q.Player.SetMissionStatus(AreaId, 3));
        }
    }
}
