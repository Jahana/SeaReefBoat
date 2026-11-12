// Darkness Rising (Bastok 5-1, Log ID 1, Mission ID 14), a line-for-line port of
// scripts/missions/bastok/5_1_Darkness_Rising.lua (quest rework C3 N4). Argus !pos 132.157 7.496 -2.187 236; Cleades
// !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Naji !pos 64 -14
// -4 237. Reward: 600 rank points. Section 1 (no Bastok mission current, a Bastokan of rank 5 who has not completed
// it): at status 0 the gate guards Cleades, Rashid, Malduc and Argus play 1007 and Naji 720 (progress). 720's finish
// with option 0 begins the mission, takes the Message to Jeuno, gives the New Fei'Yin Seal and sets status 10; any
// other option sets status 8 (declined). The gate guards' 1001 finish with option 14 (handleAcceptMission) begins the
// mission, sets status 9, takes the message and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Naji at
// status 9 plays 721, at status 12 with the Burnt Seal 722 (progress). 721's finish gives the seal and sets status 10;
// 722's completes the mission and then takes the Burnt Seal. Fei'Yin's zone-in at status 10 plays 1, whose finish
// sets status 11. Qu'Bia Arena's 32001 finish at status 11 with battlefieldWin RANK_5_MISSION (512, the Rank 5
// Mission battlefield) gives the Burnt Seal, sets status 12 and takes the New Fei'Yin Seal. Kept as the Lua has it:
// section 1 compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class DarknessRising : MissionScript
    {
        public DarknessRising() : base(XI.Quests.MissionLog.Bastok, "DARKNESS_RISING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/5_1_Darkness_Rising";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.DarknessRising;

        // The message KI is removed here to still preserve this mission should a player decline and then choose to
        // change nations. By retaining it until this point, we'll still have a point of reference to return to should
        // missionStatus be reset.
        //
        // NOTE: This function is only called if a player has declined the mission previously. Should this option be
        // selected, they will still have to speak to Naji afterwards.
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 14)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 9);
                q.Player.DelKeyItem(KeyItem.MessageToJeunoBastok);
                q.Text.Special(accepted);
            }
        }

        // a gate guard at status 0: 1007
        static QuestReply GuardTrigger(IQuestContext q) =>
            q.Player.MissionStatus(AreaId) == 0 ? QuestReply.ProgressEvent(1007) : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 600 });

            // Player meets requirements, but has not started the mission
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.Rank(q.Player.Nation) == 5 &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades).OnTrigger(GuardTrigger)
                .OnEventFinish(Zone.BastokMarkets, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMarkets.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid).OnTrigger(GuardTrigger)
                .OnEventFinish(Zone.BastokMines, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMines.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc).OnTrigger(GuardTrigger)
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(720);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 720, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Mission.Begin();
                        q.Player.DelKeyItem(KeyItem.MessageToJeunoBastok);
                        q.Player.GiveKeyItem(KeyItem.NewFeiyinSeal);
                        q.Player.SetMissionStatus(AreaId, 10);
                    }
                    else
                    {
                        q.Player.SetMissionStatus(AreaId, 8);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 1001, q =>
                    HandleAcceptMission(q, Text.Metalworks.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus).OnTrigger(GuardTrigger)
                .OnEventFinish(Zone.PortBastok, 1001, q =>
                    HandleAcceptMission(q, Text.PortBastok.YOU_ACCEPT_THE_MISSION));

            // Mission has been accepted
            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 9)
                    {
                        return QuestReply.ProgressEvent(721);
                    }
                    else if (q.Player.MissionStatus(AreaId) == 12 &&
                             q.Player.HasKeyItem(KeyItem.BurntSeal))
                    {
                        return QuestReply.ProgressEvent(722);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 721, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.NewFeiyinSeal);
                    q.Player.SetMissionStatus(AreaId, 10);
                })
                .OnEventFinish(Zone.Metalworks, 722, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.BurntSeal);
                    }
                })
                .OnZoneIn(Zone.Feiyin, q =>
                    q.Player.MissionStatus(AreaId) == 10 ? QuestReply.Event(1) : null)
                .OnEventFinish(Zone.Feiyin, 1, q => q.Player.SetMissionStatus(AreaId, 11))
                .OnEventFinish(Zone.QubiaArena, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 11 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank5Mission)
                    {
                        q.Player.GiveKeyItem(KeyItem.BurntSeal);
                        q.Player.SetMissionStatus(AreaId, 12);
                        q.Player.DelKeyItem(KeyItem.NewFeiyinSeal);
                    }
                });
        }
    }
}
