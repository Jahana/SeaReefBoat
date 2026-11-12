// The Four Musketeers (Bastok 3-1, Log ID 1, Mission ID 10), a line-for-line port of
// scripts/missions/bastok/3_1_The_Four_Musketeers.lua (quest rework C3 N3). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Iron Eater
// !pos 92.936 -19.532 1.814 237. Reward: 350 rank points. Section 1 (no Bastok mission current, a Bastokan): the four
// gate guards' 1001 finish with option 10 begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2
// (current): Cleades, Rashid, Malduc and Argus play 1002 (progress). Metalworks: Ayame and Naji above status 0 play 718
// and 717 (events); Iron Eater at 0 plays 715 (progress), else 716 (event); 715's finish with option 0 sets status 1.
// Beadeaux: a Copper Quadav's death above status 1 and below 22 adds one to the status; the zone-in at status 1 plays
// 120, whose finish sets status 2 and puts the char at -297 1 96 (rotation 1). Pashhow Marshlands: a zone-in from
// Beadeaux above status 1 and below 22 plays 10, at 22 plays 11; 10's finish puts the char at 578 25 -376 (rotation
// 126); 11's completes the mission and, when that succeeds, does the same. Kept as the Lua has it: section 1 has no
// completed check and compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheFourMusketeers : MissionScript
    {
        public TheFourMusketeers() : base(XI.Quests.MissionLog.Bastok, "THE_FOUR_MUSKETEERS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/3_1_The_Four_Musketeers";

        // mission.areaId
        const int AreaId = MissionLog.Bastok;

        // handleAcceptMission: option 10 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 10)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 350 });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.BastokMarkets, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMarkets.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.BastokMines, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMines.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.Metalworks, 1001, q =>
                    HandleAcceptMission(q, Text.Metalworks.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.PortBastok, 1001, q =>
                    HandleAcceptMission(q, Text.PortBastok.YOU_ACCEPT_THE_MISSION));

            b.State(current: true)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades).OnTrigger(q => QuestReply.ProgressEvent(1002))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid).OnTrigger(q => QuestReply.ProgressEvent(1002))
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) > 0 ? QuestReply.Event(718) : null)
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(715);
                    }
                    else
                    {
                        return QuestReply.Event(716);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc).OnTrigger(q => QuestReply.ProgressEvent(1002))
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) > 0 ? QuestReply.Event(717) : null)
                .OnEventFinish(Zone.Metalworks, 715, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                })
                .Npc(Zone.Beadeaux, Npc.Beadeaux.CopperQuadav).OnMobDeath(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus > 1 &&
                        missionStatus < 22)
                    {
                        q.Player.SetMissionStatus(AreaId, missionStatus + 1);
                    }
                })
                .OnZoneIn(Zone.Beadeaux, q =>
                    q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(120) : null)
                .OnEventFinish(Zone.Beadeaux, 120, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.SetPos(-297, 1, 96, 1);
                })
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.PashhowMarshlands, q =>
                {
                    if (q.Player.PreviousZone == Zone.Beadeaux)
                    {
                        int missionStatus = q.Player.MissionStatus(AreaId);

                        if (missionStatus > 1 &&
                            missionStatus < 22)
                        {
                            return QuestReply.Event(10);
                        }
                        else if (missionStatus == 22)
                        {
                            return QuestReply.Event(11);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.PashhowMarshlands, 10, q => q.Player.SetPos(578, 25, -376, 126))
                .OnEventFinish(Zone.PashhowMarshlands, 11, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetPos(578, 25, -376, 126);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus).OnTrigger(q => QuestReply.ProgressEvent(1002));
        }
    }
}
