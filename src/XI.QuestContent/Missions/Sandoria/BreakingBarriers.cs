// Breaking Barriers (San d'Oria 9-1, Log ID 0, Mission ID 22), a line-for-line port of
// scripts/missions/sandoria/9_1_Breaking_Barriers.lua (quest rework C3 N6). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; (_6h4) Great Hall !pos 0 -1 13 233;
// qm_breaking_barriers (VoS) !pos 91 -3 -16 128; qm5 (Xarcabard) !pos 179 -33 82 112; qm3 (Batallia Downs) !pos 210 17
// -615 105. Reward: 900 rank points. Section 1 (no San d'Oria mission current, a San d'Orian): the gate guards' 1009
// and 2009 (Southern San d'Oria) and 1009 (Northern) finish with option 22 (handleAcceptMission) begins the mission.
// Section 2 (current): _6h4 plays 32 at status 0 and 76 at 4 with the figures of Leviathan, Garuda and Titan; Halver
// 26 at 0 and 1 at 1 (progress). 32's finish sets status 1; 76's completes the mission and then takes the three
// figures. The Valley of Sorrows' qm_breaking_barriers plays 0 at status 1, else says SOMETHING_BURRIED; 0's finish
// gives the Figure of Titan (npcUtil.giveKeyItem) and sets status 2. Xarcabard's qm5 plays 12 at status 2, else says
// SOMETHING_BURIED; 12's finish gives the Figure of Garuda and sets status 3. Batallia Downs' qm3 at status 3 with
// Suparna and its Fledgling found, each not spawned or dead, plays 904 with Prog above 0, else spawns the two and says
// SENSE_SOMETHING_LURKING; otherwise it says SOMEONE_HAS_BURIED. 904's finish gives the Figure of Leviathan and sets
// status 4. Suparna's and the Fledgling's deaths at status 3, the other found and dead or not spawned, set Prog 1.
// Section 3 (completed, The Heir to the Light not current): no handlers (see below). Kept as the Lua has it: section 1
// compares the nation with mission.areaId (the log id, 0, which is xi.nation.SANDORIA) and has no rank check and no
// completed check (it reads only the current mission and the nation); the Suparna spawns take neither claim nor
// enmity; section 3 puts ['Curilla'] = mission:event(16):replaceDefault() and ['Rahal'] =
// mission:event(37):replaceDefault() directly in the section, with no zone key, so the interaction lookup (which keys
// a section's handlers by zone id: interaction_lookup.lua addContainer) never finds them and they never play. The
// state is kept, with its check and no handlers, so the states keep the Lua's section numbers.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class BreakingBarriers : MissionScript
    {
        public BreakingBarriers() : base(XI.Quests.MissionLog.Sandoria, "BREAKING_BARRIERS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/9_1_Breaking_Barriers";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.BreakingBarriers;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 22)
            {
                q.Mission.Begin();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 900 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h4).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(32);
                    }
                    else if (missionStatus == 4 &&
                             q.Player.HasKeyItem(KeyItem.FigureOfLeviathan) &&
                             q.Player.HasKeyItem(KeyItem.FigureOfGaruda) &&
                             q.Player.HasKeyItem(KeyItem.FigureOfTitan))
                    {
                        return QuestReply.ProgressEvent(76);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(26);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(1);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 32, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.ChateauDoraguille, 76, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.FigureOfLeviathan);
                        q.Player.DelKeyItem(KeyItem.FigureOfGaruda);
                        q.Player.DelKeyItem(KeyItem.FigureOfTitan);
                    }
                })
                .Npc(Zone.ValleyOfSorrows, Npc.ValleyOfSorrows.qmbreakingbarriers).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(0);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.ValleyOfSorrows.SOMETHING_BURRIED);
                    }
                })
                .OnEventFinish(Zone.ValleyOfSorrows, 0, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FigureOfTitan);
                    q.Player.SetMissionStatus(AreaId, 2);
                })
                .Npc(Zone.Xarcabard, Npc.Xarcabard.qm5).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.ProgressEvent(12);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.Xarcabard.SOMETHING_BURIED);
                    }
                })
                .OnEventFinish(Zone.Xarcabard, 12, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FigureOfGaruda);
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.qm3).OnTrigger(q =>
                {
                    // local suparnaMob = GetMobByID(batalliaID.mob.SUPARNA)
                    // local fledglingMob = GetMobByID(batalliaID.mob.SUPARNA_FLEDGLING)
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.MobExists(Mob.BatalliaDowns.SUPARNA) &&
                        q.Player.MobExists(Mob.BatalliaDowns.SUPARNA_FLEDGLING) &&
                        (!q.Player.MobIsSpawned(Mob.BatalliaDowns.SUPARNA) ||
                         q.Player.MobIsDead(Mob.BatalliaDowns.SUPARNA)) &&
                        (!q.Player.MobIsSpawned(Mob.BatalliaDowns.SUPARNA_FLEDGLING) ||
                         q.Player.MobIsDead(Mob.BatalliaDowns.SUPARNA_FLEDGLING)))
                    {
                        if (q.Mission.Var("Prog") > 0)
                        {
                            return QuestReply.ProgressEvent(904);
                        }
                        else
                        {
                            q.Player.SpawnMob(Mob.BatalliaDowns.SUPARNA);
                            q.Player.SpawnMob(Mob.BatalliaDowns.SUPARNA_FLEDGLING);
                            return QuestReply.MessageSpecial(Text.BatalliaDowns.SENSE_SOMETHING_LURKING);
                        }
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.BatalliaDowns.SOMEONE_HAS_BURIED);
                    }
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.Suparna).OnMobDeath(q =>
                {
                    // local mobSuparnaFledgling = GetMobByID(batalliaID.mob.SUPARNA_FLEDGLING)
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.MobExists(Mob.BatalliaDowns.SUPARNA_FLEDGLING) &&
                        (q.Player.MobIsDead(Mob.BatalliaDowns.SUPARNA_FLEDGLING) ||
                         !q.Player.MobIsSpawned(Mob.BatalliaDowns.SUPARNA_FLEDGLING)))
                    {
                        q.Mission.SetVar("Prog", 1);
                    }
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.SuparnaFledgling).OnMobDeath(q =>
                {
                    // local mobSuparna = GetMobByID(batalliaID.mob.SUPARNA)
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.MobExists(Mob.BatalliaDowns.SUPARNA) &&
                        (q.Player.MobIsDead(Mob.BatalliaDowns.SUPARNA) ||
                         !q.Player.MobIsSpawned(Mob.BatalliaDowns.SUPARNA)))
                    {
                        q.Mission.SetVar("Prog", 1);
                    }
                })
                .OnEventFinish(Zone.BatalliaDowns, 904, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FigureOfLeviathan);
                    q.Player.SetMissionStatus(AreaId, 4);
                });

            // Optional dialogue after completing the Mission. The Lua's section holds
            //     ['Curilla'] = mission:event(16):replaceDefault(),
            //     ['Rahal']   = mission:event(37):replaceDefault(),
            // with no zone key: the lookup never registers them (see the header), so the state has no handlers.
            b.State(current: null, when: q =>
                q.Player.HasCompletedMission(AreaId, MissionId) &&
                q.Player.CurrentMission(AreaId) != Mission.Sandoria.TheHeirToTheLight);
        }
    }
}
