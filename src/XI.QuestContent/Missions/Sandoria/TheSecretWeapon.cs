// The Secret Weapon (San d'Oria 7-2, Log ID 0, Mission ID 19), a line-for-line port of
// scripts/missions/sandoria/7_2_The_Secret_Weapon.lua (quest rework C3 N5). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230. Reward: rank 8 and 60000 gil. Section 1 (no
// San d'Oria mission current, a San d'Orian of rank 7 who has completed Prestige of the Papsque and has the rank points
// the mission needs): Grilau and Endracion below status 2 play 1041 and Ambrotien 1042 (progress); 1041's and 1042's
// finishes at status 0 set status 1; the gate guards' 1009 (Northern) and 1009 and 2009 (Southern San d'Oria) finish
// with option 19 (handleAcceptMission) begins the mission; Chateau d'Oraguille's trigger area 1 at status 1 plays 0
// (progress), whose finish sets status 2. Section 2 (current): Horlais Peak's 32001 finish at status 2 with
// battlefieldWin THE_SECRET_WEAPON (3) gives the Crystal Dowser (npcUtil.giveKeyItem) and sets status 3; at status 3
// Grilau and Endracion play 1043 and Ambrotien 1044 (progress), each finish completing the mission and then taking
// the dowser. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA); section 1's 1009 / 2009 accept reads no status.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheSecretWeapon : MissionScript
    {
        public TheSecretWeapon() : base(XI.Quests.MissionLog.Sandoria, "THE_SECRET_WEAPON") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/7_2_The_Secret_Weapon";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheSecretWeapon;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 19)
            {
                q.Mission.Begin();
            }
        }

        // 1041 / 1042's finish: status 0 moves to 1
        static void HandleIntroFinish(IQuestContext q)
        {
            if (q.Player.MissionStatus(AreaId) == 0)
            {
                q.Player.SetMissionStatus(AreaId, 1);
            }
        }

        // 1043 / 1044's finish
        static void HandleCompleteFinish(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.DelKeyItem(KeyItem.CrystalDowser);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 8, Gil = 60000 });

            // This mission contains up to 3 statuses that are used before the mission is actually accepted, and is
            // blocking for other missions when the requirements are met.  Until those requirements are met, gate
            // guards will default to their typical mission assignment behavior.

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.Rank(q.Player.Nation) == 7 &&
                    q.Player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.PrestigeOfThePapsque) &&
                    q.Player.MissionRankPoints(MissionId))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) < 2)
                    {
                        return QuestReply.ProgressEvent(1041);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1041, HandleIntroFinish)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) < 2)
                    {
                        return QuestReply.ProgressEvent(1042);
                    }

                    return null;
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) < 2)
                    {
                        return QuestReply.ProgressEvent(1041);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 1041, HandleIntroFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 1042, HandleIntroFinish)
                .OnTriggerAreaEnter(Zone.ChateauDoraguille, 1, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 0, q => q.Player.SetMissionStatus(AreaId, 2));

            // Player has accepted the mission
            b.State(current: true)
                .OnEventFinish(Zone.HorlaisPeak, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.TheSecretWeapon)
                    {
                        q.Player.GiveKeyItem(KeyItem.CrystalDowser);
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(1043);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 1043, HandleCompleteFinish)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(1044);
                    }

                    return null;
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(1043);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 1043, HandleCompleteFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 1044, HandleCompleteFinish);
        }
    }
}
