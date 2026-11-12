// The Shadow Lord (San d'Oria 5-2, Log ID 0, Mission ID 15), a line-for-line port of
// scripts/missions/sandoria/5_2_The_Shadow_Lord.lua (quest rework C3 N4). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; Door: Prince
// Royal's (_6h0) !pos -38 -3 73 233; Door: Great Hall (_6h4) !pos 0 -1 13 233. Reward: 20000 gil and rank 6. Section
// 1 (no San d'Oria mission current, a San d'Orian): the gate guards' 1009 and 2009 (Southern San d'Oria) and 1009
// (Northern) finish with option 15 (handleAcceptMission) begins the mission. Section 2 (current): Halver at status 0
// plays 546 (progress), at 1 says PRINCE_TRION_CHAMBERS, at 2 WHAT_TRION_WILL_SAY (messageText), at 4 with the Shadow
// Fragment plays 548 (progress); _6h0 at status 1 plays 547 (progress). 546's finish sets status 1, 547's status 2;
// 548's completes the mission and then takes the fragment and sets hallEvent 1. The Throne Room's _4l1 at status 2
// plays 6, whose finish sets status 3; the zone-in at status 3 with PostBattle 1 plays 7 (text table -1, flags
// UNKNOWN_0008), whose finish sets status 4, clears PostBattle and gives the Shadow Fragment (npcUtil.giveKeyItem).
// 32001's finish at status 3 with battlefieldWin SHADOW_LORD_BATTLE (160) adds Zilart's The New Frontier unless it is
// current or completed, sets PostBattle 1 and moves the char to (90.425, -5.749, 0.089, 3) in the Throne Room.
// Section 3 (hallEvent 1): _6h4 plays 61 (progress), whose finish clears hallEvent; Arsha 85 and Chupaile 86 (events);
// Halver says HIS_MAJESTY_AWAITS (messageText). Section 4 (current at status 4 or more, or no San d'Oria mission
// current with it completed and not Leaute's Last Wishes): Halver with hallEvent 0 says KNEW_YOU_WERE_THE_ONE
// (messageText); Aramaviont 12, Curilla 56, Milchupain 33 and Rahal 77 (events). Kept as the Lua has it: section 1
// compares the nation with mission.areaId (the log id, 0, which is xi.nation.SANDORIA) and has no rank check and no
// completed check (it reads only the current mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheShadowLord : MissionScript
    {
        public TheShadowLord() : base(XI.Quests.MissionLog.Sandoria, "THE_SHADOW_LORD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/5_2_The_Shadow_Lord";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheShadowLord;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 15)
            {
                q.Mission.Begin();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 20000, Rank = 6 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(546);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.MessageText(Text.ChateauDoraguille.PRINCE_TRION_CHAMBERS);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.MessageText(Text.ChateauDoraguille.WHAT_TRION_WILL_SAY);
                    }
                    else if (missionStatus == 4 &&
                             q.Player.HasKeyItem(KeyItem.ShadowFragment))
                    {
                        return QuestReply.ProgressEvent(548);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(547);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 546, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.ChateauDoraguille, 547, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.ChateauDoraguille, 548, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ShadowFragment);
                        q.Mission.SetVar("hallEvent", 1);
                    }
                })
                .OnZoneIn(Zone.ThroneRoom, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Mission.Var("PostBattle") == 1)
                    {
                        // { 7, -1, xi.cutsceneFlag.UNKNOWN_0008 }
                        return QuestReply.Event(7).TextTable(-1).Flags(0x08);
                    }

                    return null;
                })
                .Npc(Zone.ThroneRoom, Npc.ThroneRoom._4l1).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.ProgressEvent(6);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ThroneRoom, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.ShadowLordBattle)
                    {
                        if (q.Player.CurrentMission(MissionLog.Zilart) != Mission.Zilart.TheNewFrontier &&
                            !q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier))
                        {
                            // Don't add missions we already completed. Players who change nation will hit this.
                            q.Player.AddMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier);
                        }

                        q.Mission.SetVar("PostBattle", 1);
                        q.Player.SetPos(90.425, -5.749, 0.089, 3, Zone.ThroneRoom);
                    }
                })
                .OnEventFinish(Zone.ThroneRoom, 6, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.ThroneRoom, 7, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Mission.SetVar("PostBattle", 0);
                    q.Player.GiveKeyItem(KeyItem.ShadowFragment);
                });

            // Optional Great Hall event after completion.
            b.State(current: null, when: q => q.Mission.Var("hallEvent") == 1)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h4).OnTrigger(QuestReply.ProgressEvent(61))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Arsha).OnTrigger(QuestReply.Event(85))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Chupaile).OnTrigger(QuestReply.Event(86))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver)
                .OnTrigger(QuestReply.MessageText(Text.ChateauDoraguille.HIS_MAJESTY_AWAITS))
                .OnEventFinish(Zone.ChateauDoraguille, 61, q => q.Mission.SetVar("hallEvent", 0));

            // Optional Dialogue after Completion
            b.State(current: null, when: q =>
                    (q.Player.CurrentMission(MissionLog.Sandoria) == MissionId &&
                     q.Player.MissionStatus(AreaId) >= 4) ||
                    (q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                     q.Player.HasCompletedMission(AreaId, MissionId) &&
                     !q.Player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.LeautesLastWishes)))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Mission.Var("hallEvent") == 0)
                    {
                        return QuestReply.MessageText(Text.ChateauDoraguille.KNEW_YOU_WERE_THE_ONE);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Aramaviont).OnTrigger(QuestReply.Event(12))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(QuestReply.Event(56))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Milchupain).OnTrigger(QuestReply.Event(33))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal).OnTrigger(QuestReply.Event(77));
        }
    }
}
