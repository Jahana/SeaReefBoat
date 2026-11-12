// The Heir to the Light (San d'Oria 9-2, Log ID 0, Mission ID 23), a line-for-line port of
// scripts/missions/sandoria/9_2_The_Heir_to_the_Light.lua (quest rework C3 N6). Ambrotien !pos 93.419 -0.001 -57.347
// 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; (_6h4) Great Hall !pos 0 -1 13 233; _5a0:
// Heavy Stone Dr !pos -39 4.823 20 190. Reward: rank 10, 100000 gil and the title San d'Orian Royal Heir. Section 1 (no
// San d'Oria mission current, a San d'Orian): the gate guards' 1009 and 2009 (Southern San d'Oria) and 1009 (Northern)
// finish with option 23 (handleAcceptMission) begins the mission. Section 2 (current), all progress events: in the
// Chateau, _6h0 plays 3 and _6h1 5 above status 5; _6h4 8 at 5; Aramaviont 14 above 4, else 13 above 1; Curilla 19
// above 3, else 18 above 1; Halver 9 at 7, else 30 above 5, 28 above 4, 29 above 1; Milchupain 35 above 4, else 34
// above 1; Nachou 6 at 5 and 4 at 2; Perfaumand 7 at 5 and 2 at 2; Rahal 40 above 4, else 39 above 1. The Chateau's
// zone-in at status 1 plays 10, whose finish sets status 2; 8's finish sets status 6; 9's completes the mission, sets
// Option 1, clears the char var Mission[0][21]Option and gives the San d'Orian Flag (npcUtil.giveItem), setting Flag 1
// when it cannot. In Northern San d'Oria Arnau at status 0 plays 4 (event) and _6fc above 5 with Papal 0 plays 50;
// the zone-in plays 1 at status 0 and 0 at 4; 0's finish sets status 5, 1's status 1, 50's Papal 1. Fei'Yin's zone-in
// at status 2 plays 23, whose finish sets status 3. Qu'Bia Arena's 32001 finish at status 3 with battlefieldWin
// HEIR_TO_THE_LIGHT (516) sets status 4; its 32004 finish sets Intro 1. King Ranperre's Tomb's _5a0 at status 6 plays
// 14, whose finish sets status 7. Section 3 (completed): Aramaviont 11, Curilla 20 and Rahal 41 at Option 1 (progress);
// Halver with Flag 1 gives the flag and, when it could, clears Flag; Northern San d'Oria's _6fc at Option 1 with Papal
// 0 plays 50, whose finish sets Papal 1; Southern San d'Oria's zone-in at Option 1 plays 0, whose finish clears Option
// and Papal. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA) and has no rank check and no completed check (it reads only the current mission and the
// nation); 9's finish sets Option and gives the flag whatever mission:complete answered (its cleanup runs first, so
// Option survives it).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class TheHeirToTheLight : MissionScript
    {
        public TheHeirToTheLight() : base(XI.Quests.MissionLog.Sandoria, "THE_HEIR_TO_THE_LIGHT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/9_2_The_Heir_to_the_Light";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.TheHeirToTheLight;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 23)
            {
                q.Mission.Begin();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 10, Gil = 100000, Title = Title.SanDorianRoyalHeir });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) > 5)
                    {
                        return QuestReply.ProgressEvent(3);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h1).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) > 5)
                    {
                        return QuestReply.ProgressEvent(5);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h4).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 5)
                    {
                        return QuestReply.ProgressEvent(8);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Aramaviont).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus > 4)
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    else if (missionStatus > 1)
                    {
                        return QuestReply.ProgressEvent(13);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus > 3)
                    {
                        return QuestReply.ProgressEvent(19);
                    }
                    else if (missionStatus > 1)
                    {
                        return QuestReply.ProgressEvent(18);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(9);
                    }
                    else if (missionStatus > 5)
                    {
                        return QuestReply.ProgressEvent(30);
                    }
                    else if (missionStatus > 4)
                    {
                        return QuestReply.ProgressEvent(28);
                    }
                    else if (missionStatus > 1)
                    {
                        return QuestReply.ProgressEvent(29);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Milchupain).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus > 4)
                    {
                        return QuestReply.ProgressEvent(35);
                    }
                    else if (missionStatus > 1)
                    {
                        return QuestReply.ProgressEvent(34);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Nachou).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(6);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(4);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Perfaumand).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(7);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(2);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus > 4)
                    {
                        return QuestReply.ProgressEvent(40);
                    }
                    else if (missionStatus > 1)
                    {
                        return QuestReply.ProgressEvent(39);
                    }

                    return null;
                })
                .OnZoneIn(Zone.ChateauDoraguille, q =>
                    q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(10) : null)
                .OnEventFinish(Zone.ChateauDoraguille, 8, q => q.Player.SetMissionStatus(AreaId, 6))
                // It is possible to obtain the San d'Orian flag after completing this mission
                // should inventory be full.  Set 'Flag' mission variable should this occur.
                // 'Option' set after complete for additional dialogue, and cleared on Zoning
                // into Southern San d'Oria.
                .OnEventFinish(Zone.ChateauDoraguille, 9, q =>
                {
                    q.Mission.Complete();
                    q.Mission.SetVar("Option", 1);

                    // The M8-2 Prince cutscenes are superseded; reset their var for cleanliness.
                    q.Player.SetCharVar("Mission[0][21]Option", 0);

                    if (!q.Player.GiveItem(Item.SanDorianFlag))
                    {
                        q.Mission.SetVar("Flag", 1);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 10, q => q.Player.SetMissionStatus(AreaId, 2))
                // The cathedral is preparing for the Rites of Succession.
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Arnau).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(4);
                    }

                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria._6fc).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) > 5 &&
                        q.Mission.Var("Papal") == 0)
                    {
                        return QuestReply.ProgressEvent(50);
                    }

                    return null;
                })
                .OnZoneIn(Zone.NorthernSanDoria, q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(1);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 0, q => q.Player.SetMissionStatus(AreaId, 5))
                .OnEventFinish(Zone.NorthernSanDoria, 1, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.NorthernSanDoria, 50, q => q.Mission.SetVar("Papal", 1))
                .OnZoneIn(Zone.Feiyin, q =>
                    q.Player.MissionStatus(AreaId) == 2 ? QuestReply.Event(23) : null)
                .OnEventFinish(Zone.Feiyin, 23, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.QubiaArena, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.HeirToTheLight)
                    {
                        q.Player.SetMissionStatus(AreaId, 4);
                    }
                })
                .OnEventFinish(Zone.QubiaArena, 32004, q => q.Mission.SetVar("Intro", 1))
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb._5a0).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 6)
                    {
                        return QuestReply.ProgressEvent(14);
                    }

                    return null;
                })
                .OnEventFinish(Zone.KingRanperresTomb, 14, q => q.Player.SetMissionStatus(AreaId, 7));

            // Player has completed this mission.
            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Aramaviont).OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(11);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(20);
                    }

                    return null;
                })
                // If the player's inventory was full on mission complete, give them the
                // nation flag, and clear the variable.  This could probably be short circuited
                // with one conditional, but playing it safe.
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Mission.Var("Flag") == 1)
                    {
                        if (q.Player.GiveItem(Item.SanDorianFlag))
                        {
                            q.Mission.SetVar("Flag", 0);
                        }
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal).OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(41);
                    }

                    return null;
                })
                // Viewable once; re-examining the door reports it locked, per its default action.
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria._6fc).OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 1 &&
                        q.Mission.Var("Papal") == 0)
                    {
                        return QuestReply.ProgressEvent(50);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 50, q => q.Mission.SetVar("Papal", 1))
                .OnZoneIn(Zone.SouthernSanDoria, q =>
                    q.Mission.Var("Option") == 1 ? QuestReply.Event(0) : null)
                .OnEventFinish(Zone.SouthernSanDoria, 0, q =>
                {
                    q.Mission.SetVar("Option", 0);
                    q.Mission.SetVar("Papal", 0);
                });
        }
    }
}
