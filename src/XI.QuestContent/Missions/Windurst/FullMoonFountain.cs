// Full Moon Fountain (Windurst 6-1, Log ID 2, Mission ID 16), a line-for-line port of
// scripts/missions/windurst/6_1_Full_Moon_Fountain.lua (quest rework C3 N5). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo
// !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Hakkuru-Rinkuru !pos
// -111 -4 101 240; Gate: Magical Gizmo (_5eb) !pos -291 0 -659 194. Reward: 650 rank points. Section 1 (no Windurst
// mission current, a Windurstian): the gate guards' finishes (Port Windurst 78, Windurst Walls 93, Windurst Waters
// 111, Windurst Woods 114) with option 16 (handleAcceptMission) begin the mission and say the zone's
// YOU_ACCEPT_THE_MISSION. Section 2 (current): Hakkuru-Rinkuru at status 0 plays 456 (progress, with the Southwestern
// Star Charm), at status 1 or 2 457 and at status 3 459 (events); Kuroido-Moido at status 1 or 2 plays 458 and at
// status 3 460 (events). 456's finish sets status 1 and gives the charm. Outer Horutoto Ruins' _5eb, with none of the
// four Jacks (FULL_MOON_FOUNTAIN_OFFSET + 0 to 3) spawned: at status 1 spawns and claims all four and says
// GUARDIAN_BLOCKING_WAY; at status 2 says STAR_CHARM_DISAPPEARS and plays 68 (progress); with one spawned it says
// DOOR_WONT_OPEN_STAR_CHARM. Each Jack's death (one shared handler, jackOnMobDeath) at status 1 sets status 2 once
// none of the four is still spawned and alive. 68's finish sets status 3 and takes the charm. Full Moon Fountain's
// zone-in at status 3 plays 50, whose finish completes the mission. Kept as the Lua has it: section 1 compares the
// nation with mission.areaId (the log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed
// check; _5eb says DOOR_WONT_OPEN_STAR_CHARM whenever a Jack is spawned, whatever the status.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class FullMoonFountain : MissionScript
    {
        public FullMoonFountain() : base(XI.Quests.MissionLog.Windurst, "FULL_MOON_FOUNTAIN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/6_1_Full_Moon_Fountain";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.FullMoonFountain;

        // outerHorutotoID.mob.FULL_MOON_FOUNTAIN_OFFSET
        const string Jacks = Mob.OuterHorutotoRuins.FULL_MOON_FOUNTAIN_OFFSET;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 16)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        static bool AreJacksSpawned(IQuestContext q)
        {
            for (int mobIdOffset = 0; mobIdOffset <= 3; mobIdOffset++)
            {
                if (q.Player.MobExists(Jacks, mobIdOffset) && q.Player.MobIsSpawned(Jacks, mobIdOffset))
                {
                    return true;
                }
            }

            return false;
        }

        static void JackOnMobDeath(IQuestContext q)
        {
            bool areMobsDefeated = true;

            if (q.Player.MissionStatus(AreaId) != 1)
            {
                return;
            }

            for (int mobIdOffset = 0; mobIdOffset <= 3; mobIdOffset++)
            {
                if (q.Player.MobExists(Jacks, mobIdOffset) &&
                    !q.Player.MobIsDead(Jacks, mobIdOffset) &&
                    q.Player.MobIsSpawned(Jacks, mobIdOffset))
                {
                    areMobsDefeated = false;
                }
            }

            if (areMobsDefeated)
            {
                q.Player.SetMissionStatus(AreaId, 2);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 650 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.PortWindurst, 78, q =>
                    HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93, q =>
                    HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111, q =>
                    HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114, q =>
                    HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(456, 0, KeyItem.SouthwesternStarCharm);
                    }
                    else if (missionStatus == 1 || missionStatus == 2)
                    {
                        return QuestReply.Event(457);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(459);
                    }

                    return null;
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1 || missionStatus == 2)
                    {
                        return QuestReply.Event(458);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(460);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 456, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.SouthwesternStarCharm);
                })
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5eb).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (!AreJacksSpawned(q))
                    {
                        if (missionStatus == 1)
                        {
                            for (int mobIdOffset = 0; mobIdOffset <= 3; mobIdOffset++)
                            {
                                q.Player.SpawnMob(Jacks, mobIdOffset, claim: true);
                            }

                            return QuestReply.MessageSpecial(Text.OuterHorutotoRuins.GUARDIAN_BLOCKING_WAY);
                        }
                        else if (missionStatus == 2)
                        {
                            q.Text.Special(Text.OuterHorutotoRuins.STAR_CHARM_DISAPPEARS,
                                Zone.OuterHorutotoRuins, KeyItem.SouthwesternStarCharm);
                            return QuestReply.ProgressEvent(68);
                        }
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.OuterHorutotoRuins.DOOR_WONT_OPEN_STAR_CHARM,
                            Zone.OuterHorutotoRuins, KeyItem.SouthwesternStarCharm);
                    }

                    return null;
                })
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins.JackofBatons).OnMobDeath(JackOnMobDeath)
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins.JackofCoins).OnMobDeath(JackOnMobDeath)
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins.JackofCups).OnMobDeath(JackOnMobDeath)
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins.JackofSwords).OnMobDeath(JackOnMobDeath)
                .OnEventFinish(Zone.OuterHorutotoRuins, 68, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.DelKeyItem(KeyItem.SouthwesternStarCharm);
                })
                .OnZoneIn(Zone.FullMoonFountain, q =>
                    q.Player.MissionStatus(AreaId) == 3 ? QuestReply.Event(50) : null)
                .OnEventFinish(Zone.FullMoonFountain, 50, q => q.Mission.Complete());
        }
    }
}
