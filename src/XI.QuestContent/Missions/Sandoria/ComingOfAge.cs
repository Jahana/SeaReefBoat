// Coming of Age (San d'Oria 8-1, Log ID 0, Mission ID 20), a line-for-line port of
// scripts/missions/sandoria/8_1_Coming_of_Age.lua (quest rework C3 N6). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; Halver !pos 2 0.1 0.1 233; Fountain of Kings
// !pos 567 18 -939 208. Reward: 800 rank points. Section 1 (no San d'Oria mission current, a San d'Orian): the gate
// guards' 1009 and 2009 (Southern San d'Oria) and 1009 (Northern) finish with option 20 (handleAcceptMission) begins
// the mission. Section 2 (current): Halver plays 58 at status 1 and 102 at 3 with the Drops of Amnio (progress); the
// doors _6h0 and _6h1 at status 2 or more play 64 and 75 (events). The Chateau's zone-in at status 0 plays 116. 58's
// finish sets status 2, 116's status 1; 102's completes the mission and then sets Progress to GetSystemTime() + 60
// and takes the Drops of Amnio (the era module MissionAdjustments replaces this finish under SOA). The Fountain of
// Kings, with both NMs found and each not spawned or dead, at status 2 spawns Valor and Honor and says
// SENSE_SOMETHING_EVIL, and at status 3 without the Drops of Amnio gives them (keyItem). Honor's and Valor's deaths at
// status 2 set status 3 (see below). Section 3 (completed, Progress above 0): Grilau, Ambrotien and Endracion say their
// zone's ORIGINAL_MISSION_OFFSET + 127 (messageSpecial, priority 1000); Northern San d'Oria's zone-in, once Progress
// is below GetSystemTime() and outside the Mog House, plays 16, whose finish sets Progress 0. Kept as the Lua has it:
// section 1 compares the nation with mission.areaId (the log id, 0, which is xi.nation.SANDORIA) and has no rank check
// and no completed check (it reads only the current mission and the nation); the Fountain of Kings names
// GetMobByID(VALOR) mobHonor and GetMobByID(HONOR) mobValor (harmless: both are tested alike); Honor's death reads
// GetMobByID(HONOR), itself, and Valor's death GetMobByID(VALOR), itself, not the other NM; the spawns take neither
// claim nor enmity.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class ComingOfAge : MissionScript
    {
        public ComingOfAge() : base(XI.Quests.MissionLog.Sandoria, "COMING_OF_AGE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/8_1_Coming_of_Age";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.ComingOfAge;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 20)
            {
                q.Mission.Begin();
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 127):setPriority(1000): a text id plus an
        // offset, so a closure
        static QuestReply OffsetPlus127(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 127).SetPriority(1000);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 800 });

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

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(58);
                    }
                    else if (missionStatus == 3 &&
                             q.Player.HasKeyItem(KeyItem.DropsOfAmnio))
                    {
                        return QuestReply.ProgressEvent(102);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 2)
                    {
                        return QuestReply.Event(64);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h1).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 2)
                    {
                        return QuestReply.Event(75);
                    }

                    return null;
                })
                .OnZoneIn(Zone.ChateauDoraguille, q =>
                    q.Player.MissionStatus(AreaId) == 0 ? QuestReply.Event(116) : null)
                .OnEventFinish(Zone.ChateauDoraguille, 58, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.ChateauDoraguille, 102, q =>
                {
                    if (q.Mission.Complete())
                    {
                        // This cutscene is blocking after the mission has been completed.  Check this
                        // before allowing further gate guard interaction (Mission[0][20]Progress).  Required
                        // final CS will set this to 0, and we should disallow on non-zero values
                        q.Mission.SetVar("Progress", q.Player.SystemTime + 60);
                        q.Player.DelKeyItem(KeyItem.DropsOfAmnio);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 116, q => q.Player.SetMissionStatus(AreaId, 1))
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.FountainofKings).OnTrigger(q =>
                {
                    // local mobHonor = GetMobByID(quicksandCavesID.mob.VALOR)
                    // local mobValor = GetMobByID(quicksandCavesID.mob.HONOR)
                    if (q.Player.MobExists(Mob.QuicksandCaves.VALOR) &&
                        q.Player.MobExists(Mob.QuicksandCaves.HONOR) &&
                        (!q.Player.MobIsSpawned(Mob.QuicksandCaves.VALOR) ||
                         q.Player.MobIsDead(Mob.QuicksandCaves.VALOR)) &&
                        (!q.Player.MobIsSpawned(Mob.QuicksandCaves.HONOR) ||
                         q.Player.MobIsDead(Mob.QuicksandCaves.HONOR)))
                    {
                        int missionStatus = q.Player.MissionStatus(AreaId);

                        if (missionStatus == 2)
                        {
                            q.Player.SpawnMob(Mob.QuicksandCaves.VALOR);
                            q.Player.SpawnMob(Mob.QuicksandCaves.HONOR);
                            return QuestReply.MessageSpecial(Text.QuicksandCaves.SENSE_SOMETHING_EVIL);
                        }
                        else if (missionStatus == 3 && !q.Player.HasKeyItem(KeyItem.DropsOfAmnio))
                        {
                            return QuestReply.KeyItem(KeyItem.DropsOfAmnio);
                        }
                    }

                    return null;
                })
                // Only one of these NMs are required to progress, but the mob must despawn while in combat with
                // the other target.
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.Honor).OnMobDeath(q =>
                {
                    // local mobValor = GetMobByID(quicksandCavesID.mob.HONOR)
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.MobExists(Mob.QuicksandCaves.HONOR) &&
                        (q.Player.MobIsDead(Mob.QuicksandCaves.HONOR) ||
                         !q.Player.MobIsSpawned(Mob.QuicksandCaves.HONOR)))
                    {
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.Valor).OnMobDeath(q =>
                {
                    // local mobHonor = GetMobByID(quicksandCavesID.mob.VALOR)
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.MobExists(Mob.QuicksandCaves.VALOR) &&
                        (q.Player.MobIsDead(Mob.QuicksandCaves.VALOR) ||
                         !q.Player.MobIsSpawned(Mob.QuicksandCaves.VALOR)))
                    {
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                });

            // Player has completed the mission and requires the final cutscene.  Guards are blocked until that time.
            // There is an additional dialogue that exists if wait time is less than required (OFFSET + 126); however,
            // modern requirement is only one minute.  It is highly unlikely that this scenario will ever be hit.
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Mission.Var("Progress") > 0)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => OffsetPlus127(q, Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .OnZoneIn(Zone.NorthernSanDoria, q =>
                {
                    if (q.Mission.Var("Progress") < q.Player.SystemTime &&
                        !q.Player.InMogHouse)
                    {
                        return QuestReply.Event(16);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 16, q => q.Mission.SetVar("Progress", 0))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => OffsetPlus127(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => OffsetPlus127(q, Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET));
        }
    }
}
