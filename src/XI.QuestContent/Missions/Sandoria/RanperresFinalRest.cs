// Ranperre's Final Rest (San d'Oria 6-2, Log ID 0, Mission ID 17), a line-for-line port of
// scripts/missions/sandoria/6_2_Ranperres_Final_Rest.lua (quest rework C3 N5). Ambrotien !pos 93.419 -0.001 -57.347
// 230; Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; Door: Prince
// Royal's !pos -38 -3 73 233; _5a0: Heavy Stone Dr !pos -39 4.823 20 190; Tombstone !pos -73.594 7.585 20.130 190.
// Reward: rank 7 and 40000 gil. Section 1 (no San d'Oria mission current, a San d'Orian): the gate guards' 1009 and
// 2009 (Southern San d'Oria) and 1009 (Northern) finish with option 17 (handleAcceptMission) begins the mission.
// Section 2 (current): Chateau d'Oraguille's _6h0 plays 81 at status 0, 21 at 5 and 79 at 7 (progress); Perfaumand
// with the Ancient San d'Orian Book at status 3 to 5 plays 49, at status 6 50 and at 7 79 (progress); 21's finish sets
// status 6 and 81's status 1. King Ranperre's Tomb: _5a0 at status 1 pops Corrupted Yorgos, Soffeil and Ulbrig (hide
// 0, no claim) and says SENSE_SOMETHING_EVIL; at status 2 or 3 east of x -39.019 it plays 6, at status 3 at or west of
// it 7, at status 6 5 (progress). Each Corrupted's death at status 1, the other two dead or not spawned, sets status 2.
// Tombstone_Lower at status 3 without the book plays 8 (progress); with the book it says FINAL_RESTING_PLACE. 5's
// finish sets status 7; 6's, at status 2 at or west of x -39.019, sets status 3; 8's gives the book
// (npcUtil.giveKeyItem) and takes the Piece of Paper. Southern San d'Oria: Ambrotien with the book plays 1036, at
// status 4 1038 with the char's local var Mission[0][17]requiredToZone 1, else 1040, at status 7 1034; Endracion
// likewise 1035, 1037, 1039 and 1033 (progress). 1033's and 1034's finishes complete the mission; 1035's and 1036's
// take the book, set the local var 1 and set status 4; 1039's and 1040's set status 5. Northern San d'Oria: Grilau as
// Endracion, with the 1033, 1035 and 1039 finishes. Kept as the Lua has it: section 1 compares the nation with
// mission.areaId (the log id, 0, which is xi.nation.SANDORIA) and has no rank check and no completed check; the local
// table keeps its spelling ranparresRestTable; the requiredToZone local var is the char's own
// (player:setLocalVar('Mission[0][17]requiredToZone')), not mission:setLocalVar; 1033's and 1034's finishes ignore
// mission:complete's answer; 1037 and 1038 have no finish; the deaths read the other Corrupted with no nil guard.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class RanperresFinalRest : MissionScript
    {
        public RanperresFinalRest() : base(XI.Quests.MissionLog.Sandoria, "RANPERRES_FINAL_REST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/6_2_Ranperres_Final_Rest";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.RanperresFinalRest;

        // the Lua's local ranparresRestTable (its spelling): the three Corrupted, in the table's order
        static readonly string[] RanparresRestTable =
        {
            Mob.KingRanperresTomb.CORRUPTED_YORGOS,
            Mob.KingRanperresTomb.CORRUPTED_SOFFEIL,
            Mob.KingRanperresTomb.CORRUPTED_ULBRIG,
        };

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 17)
            {
                q.Mission.Begin();
            }
        }

        // (GetMobByID(id):isDead() or not GetMobByID(id):isSpawned())
        static bool DeadOrNotSpawned(IQuestContext q, string mobName) =>
            q.Player.MobIsDead(mobName) || !q.Player.MobIsSpawned(mobName);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 7, Gil = 40000 });

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
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(81);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(21);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(79);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Perfaumand).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (q.Player.HasKeyItem(KeyItem.AncientSanDorianBook) &&
                        missionStatus > 2 &&
                        missionStatus < 6)
                    {
                        return QuestReply.ProgressEvent(49);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(50);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(79);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 21, q => q.Player.SetMissionStatus(AreaId, 6))
                .OnEventFinish(Zone.ChateauDoraguille, 81, q => q.Player.SetMissionStatus(AreaId, 1))
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb._5a0).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1 &&
                        q.Player.PopFromQm(RanparresRestTable, hide: 0, claim: false))
                    {
                        return QuestReply.MessageSpecial(Text.KingRanperresTomb.SENSE_SOMETHING_EVIL);
                    }
                    else if ((missionStatus == 2 || missionStatus == 3) &&
                             q.Player.XPos > -39.019)
                    {
                        return QuestReply.ProgressEvent(6);
                    }
                    else if (missionStatus == 3 && q.Player.XPos <= -39.019)
                    {
                        return QuestReply.ProgressEvent(7);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(5);
                    }

                    return null;
                })
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb.CorruptedSoffeil).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        DeadOrNotSpawned(q, Mob.KingRanperresTomb.CORRUPTED_YORGOS) &&
                        DeadOrNotSpawned(q, Mob.KingRanperresTomb.CORRUPTED_ULBRIG))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb.CorruptedUlbrig).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        DeadOrNotSpawned(q, Mob.KingRanperresTomb.CORRUPTED_YORGOS) &&
                        DeadOrNotSpawned(q, Mob.KingRanperresTomb.CORRUPTED_SOFFEIL))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb.CorruptedYorgos).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        DeadOrNotSpawned(q, Mob.KingRanperresTomb.CORRUPTED_ULBRIG) &&
                        DeadOrNotSpawned(q, Mob.KingRanperresTomb.CORRUPTED_SOFFEIL))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.KingRanperresTomb, Npc.KingRanperresTomb.TombstoneLower).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        !q.Player.HasKeyItem(KeyItem.AncientSanDorianBook))
                    {
                        return QuestReply.ProgressEvent(8);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.AncientSanDorianBook))
                    {
                        return QuestReply.MessageSpecial(Text.KingRanperresTomb.FINAL_RESTING_PLACE);
                    }

                    return null;
                })
                .OnEventFinish(Zone.KingRanperresTomb, 5, q => q.Player.SetMissionStatus(AreaId, 7))
                .OnEventFinish(Zone.KingRanperresTomb, 6, q =>
                {
                    // Only set this status when they have not cancelled the CS.  Event Option is not
                    // changed when 'No' is selected.
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.XPos <= -39.019)
                    {
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                })
                .OnEventFinish(Zone.KingRanperresTomb, 8, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AncientSanDorianBook);
                    q.Player.DelKeyItem(KeyItem.PieceOfPaper);
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (q.Player.HasKeyItem(KeyItem.AncientSanDorianBook))
                    {
                        return QuestReply.ProgressEvent(1036);
                    }
                    else if (missionStatus == 4)
                    {
                        if (q.Player.LocalVar("Mission[0][17]requiredToZone") == 1)
                        {
                            return QuestReply.ProgressEvent(1038);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(1040);
                        }
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(1034);
                    }

                    return null;
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (q.Player.HasKeyItem(KeyItem.AncientSanDorianBook))
                    {
                        return QuestReply.ProgressEvent(1035);
                    }
                    else if (missionStatus == 4)
                    {
                        if (q.Player.LocalVar("Mission[0][17]requiredToZone") == 1)
                        {
                            return QuestReply.ProgressEvent(1037);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(1039);
                        }
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(1033);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 1033, q => q.Mission.Complete())
                .OnEventFinish(Zone.SouthernSanDoria, 1034, q => q.Mission.Complete())
                .OnEventFinish(Zone.SouthernSanDoria, 1035, q =>
                {
                    q.Player.DelKeyItem(KeyItem.AncientSanDorianBook);
                    q.Player.SetLocalVar("Mission[0][17]requiredToZone", 1);
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 1036, q =>
                {
                    q.Player.DelKeyItem(KeyItem.AncientSanDorianBook);
                    q.Player.SetLocalVar("Mission[0][17]requiredToZone", 1);
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 1039, q => q.Player.SetMissionStatus(AreaId, 5))
                .OnEventFinish(Zone.SouthernSanDoria, 1040, q => q.Player.SetMissionStatus(AreaId, 5))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (q.Player.HasKeyItem(KeyItem.AncientSanDorianBook))
                    {
                        return QuestReply.ProgressEvent(1035);
                    }
                    else if (missionStatus == 4)
                    {
                        if (q.Player.LocalVar("Mission[0][17]requiredToZone") == 1)
                        {
                            return QuestReply.ProgressEvent(1037);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(1039);
                        }
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(1033);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 1033, q => q.Mission.Complete())
                .OnEventFinish(Zone.NorthernSanDoria, 1035, q =>
                {
                    q.Player.DelKeyItem(KeyItem.AncientSanDorianBook);
                    q.Player.SetLocalVar("Mission[0][17]requiredToZone", 1);
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 1039, q => q.Player.SetMissionStatus(AreaId, 5));
        }
    }
}
