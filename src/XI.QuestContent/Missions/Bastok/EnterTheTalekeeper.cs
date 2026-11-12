// Enter the Talekeeper (Bastok 8-2, Log ID 1, Mission ID 21), a line-for-line port of
// scripts/missions/bastok/8_2_Enter_the_Talekeeper.lua (quest rework C3 N6). Argus !pos 132.157 7.496 -2.187 236;
// Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Drake
// Fang !pos -74 0.1 58 172; qm5 !pos -29.195 -22.159 -183.716 174; qm6 !pos -27.964 -10.358 -185.768 174. Reward:
// 80000 gil and rank 9. Section 1 (no Bastok mission current, a Bastokan): the gate guards' 1001 finish with option
// 21 (handleAcceptMission) begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current):
// Cleades, Rashid, Malduc and Argus say their zone's EXTENDED_MISSION_OFFSET + 12 (messageSpecial). Bastok Mines'
// zone-in from the Zeruhn Mines at status 5 plays 176, whose finish completes the mission and then takes the Old
// Piece of Wood. Drake Fang plays 202 at status 0 (progress), 203 from 1 to 3 (event) and 204 at 4 (progress); 202's
// finish sets status 1, 204's sets status 5 and moves the char to (23, 0, 4). In the Kuftal Tunnel, qm5 at status 1
// plays 12, whose finish with option 0 sets status 2 and says FELL; qm6, with none of the three ghosts
// (TALEKEEPER_OFFSET + 0 to 2) alive, at status 2 spawns the three and says EVIL, and at status 3 plays 13, whose
// finish sets status 4 and gives the Old Piece of Wood (npcUtil.giveKeyItem). Each ghost's death at status 2 with no
// ghost left alive sets status 3. Section 3 (a Bastokan with no Bastok mission current who has completed it and not
// The Salt of the Earth): Detzo 184, Gumbah 183, Pavvke 76 (1) and Iron Eater 769 (events, importantOnce); in the
// play-through Blade of Darkness's 99 (a progress event) answers Gumbah over the mission's 183. Kept as the
// Lua has it: section 1 compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK) and has no
// rank check and no completed check (it reads only the current mission and the nation); qm6 reads the status of
// getMissionStatus(player:getNation()) where the rest read mission.areaId (the same log for a Bastokan); qm6's spawns
// take neither claim nor enmity.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class EnterTheTalekeeper : MissionScript
    {
        public EnterTheTalekeeper() : base(XI.Quests.MissionLog.Bastok, "ENTER_THE_TALEKEEPER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/8_2_Enter_the_Talekeeper";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.EnterTheTalekeeper;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 21)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // the Lua's local isGhostsAlive: any of the three ghosts found, spawned and not dead
        static bool IsGhostsAlive(IQuestContext q)
        {
            for (int i = 0; i <= 2; i++)
            {
                // local ghostMob = GetMobByID(kuftalID.mob.TALEKEEPER_OFFSET + i)
                if (q.Player.MobExists(Mob.KuftalTunnel.TALEKEEPER_OFFSET, i) &&
                    q.Player.MobIsSpawned(Mob.KuftalTunnel.TALEKEEPER_OFFSET, i) &&
                    !q.Player.MobIsDead(Mob.KuftalTunnel.TALEKEEPER_OFFSET, i))
                {
                    return true;
                }
            }

            return false;
        }

        // mission:messageSpecial(zone.text.EXTENDED_MISSION_OFFSET + 12): a text id plus an offset, so a closure
        static QuestReply OffsetPlus12(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 12);

        // a ghost's death: at status 2 with no ghost left alive, status 3
        static void GhostDeath(IQuestContext q)
        {
            if (q.Player.MissionStatus(AreaId) == 2 &&
                !IsGhostsAlive(q))
            {
                q.Player.SetMissionStatus(AreaId, 3);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 80000, Rank = 9 });

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
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades)
                .OnTrigger(q => OffsetPlus12(q, Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus12(q, Text.BastokMines.EXTENDED_MISSION_OFFSET))
                // This is a continuation of event 204 from Zeruhn Mines.
                // Player is teleported here automatically, and this event will finish the sequence and complete the
                // mission. prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone).
                .OnZoneIn(Zone.BastokMines, q =>
                {
                    if (q.Player.PreviousZone == Zone.ZeruhnMines &&
                        q.Player.MissionStatus(AreaId) == 5)
                    {
                        return QuestReply.Event(176);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 176, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OldPieceOfWood);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus12(q, Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus12(q, Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.ZeruhnMines, Npc.ZeruhnMines.DrakeFang).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(202);
                    }
                    else if (missionStatus >= 1 && missionStatus < 4)
                    {
                        return QuestReply.Event(203);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(204);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ZeruhnMines, 202, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.ZeruhnMines, 204, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 5);
                    q.Player.SetPos(23, 0, 4);
                })
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.qm5).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(12);
                    }

                    return null;
                })
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.qm6).OnTrigger(q =>
                {
                    bool anyGhostsAlive = IsGhostsAlive(q);

                    if (q.Player.MissionStatus(q.Player.Nation) == 2 &&
                        !anyGhostsAlive)
                    {
                        for (int i = 0; i <= 2; i++)
                        {
                            q.Player.SpawnMob(Mob.KuftalTunnel.TALEKEEPER_OFFSET, i);
                        }

                        return QuestReply.MessageSpecial(Text.KuftalTunnel.EVIL);
                    }
                    else if (q.Player.MissionStatus(q.Player.Nation) == 3 &&
                             !anyGhostsAlive)
                    {
                        return QuestReply.ProgressEvent(13);
                    }

                    return null;
                })
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.DervosGhost).OnMobDeath(GhostDeath)
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.GizerlsGhost).OnMobDeath(GhostDeath)
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.GordovsGhost).OnMobDeath(GhostDeath)
                .OnEventFinish(Zone.KuftalTunnel, 12, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                        q.Text.Special(Text.KuftalTunnel.FELL);
                    }
                })
                .OnEventFinish(Zone.KuftalTunnel, 13, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Player.GiveKeyItem(KeyItem.OldPieceOfWood);
                });

            b.State(current: null, when: q =>
                    q.Player.Nation == Nation.Bastok &&
                    q.Player.CurrentMission(AreaId) == Mission.Bastok.None &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheSaltOfTheEarth))
                .Npc(Zone.BastokMines, Npc.BastokMines.Detzo).OnTrigger(QuestReply.Event(184).ImportantOnce())
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah).OnTrigger(QuestReply.Event(183).ImportantOnce())
                .Npc(Zone.BastokMines, Npc.BastokMines.Pavvke).OnTrigger(QuestReply.Event(76, 1).ImportantOnce())
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(QuestReply.Event(769).ImportantOnce());
        }
    }
}
