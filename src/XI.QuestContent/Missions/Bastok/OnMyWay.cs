// On My Way (Bastok 7-2, Log ID 1, Mission ID 19), a line-for-line port of
// scripts/missions/bastok/7_2_On_My_Way.lua (quest rework C3 N5). Argus !pos 132.157 7.496 -2.187 236; Cleades !pos
// -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Karst !pos 106 -21 0
// 237; Hilda !pos -163 -8 13 236; Gumbah !pos 52 0 -36 234. Reward: 60000 gil and rank 8. Section 1 (no Bastok
// mission current, a Bastokan): the four gate guards' 1001 finish with option 19 (handleAcceptMission) begins the
// mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades, Rashid, Malduc and Argus at status
// 3 with Option 1 play 1011 (progress); Gumbah at status 3 with Option 1 and GumbahDialog 1 plays 177 (progress);
// Franziska above status 0 plays 770 (progress); Karst plays 765 at status 0 and 766 with Option as its argument at
// status 3 (progress); Hilda at status 1 plays 255 (progress). Bastok Mines' 175 finish sets GumbahDialog 1 and 177's
// sets Option 0 and takes the Letter from Werei; 765's sets status 1; 766's reads Option, completes the mission and,
// when that succeeds, sets Stage 1 and sets Option 1 again when it was 1; 255's sets status 2. Waughroon Shrine's
// 32001 finish at status 2 with battlefieldWin ON_MY_WAY (67) gives the Letter from Werei (npcUtil.giveKeyItem), sets
// status 3 and Option 1. Section 3 (completed, Option 1): Cleades, Rashid, Malduc and Argus play 1011 (progress);
// Gumbah plays 177 with GumbahDialog 1, else 175 (progress), with the same two Bastok Mines finishes. Section 4
// (completed, Stage 1): Metalworks' _6lg plays 622 with Beauty and the Galka, Fallen Comrades and Rivals all
// completed, else 621 (progress); either finish sets Stage 0. Kept as the Lua has it: section 1 compares the nation
// with mission.areaId (the log id, 1, which is xi.nation.BASTOK) and has no rank check and no completed check;
// Cleades', Rashid's, Malduc's and Argus's else arms in section 2 build mission:messageSpecial(EXTENDED_MISSION_OFFSET
// + 7) without returning it, so they return nothing; section 2 lists a 175 finish though no section 2 trigger plays
// 175; a char with the mission both current and completed (a GM) with Option 1 and GumbahDialog 1 runs both
// sections' 175 and 177 finishes, as the Lua's interaction_lookup runs every handler whose check passes.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class OnMyWay : MissionScript
    {
        public OnMyWay() : base(XI.Quests.MissionLog.Bastok, "ON_MY_WAY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/7_2_On_My_Way";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.OnMyWay;

        // handleAcceptMission: option 19 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 19)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // a gate guard in section 2: 1011 at status 3 with Option 1. The Lua's else arm is
        // `mission:messageSpecial(<zone>.text.EXTENDED_MISSION_OFFSET + 7)` with no return: the action is built and
        // dropped, so the handler returns nothing
        static QuestReply GuardTrigger(IQuestContext q)
        {
            if (q.Player.MissionStatus(AreaId) == 3 &&
                q.Mission.Var("Option") == 1)
            {
                return QuestReply.ProgressEvent(1011);
            }
            else
            {
                // mission:messageSpecial(... EXTENDED_MISSION_OFFSET + 7), not returned
                return null;
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 60000, Rank = 8 });

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
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades).OnTrigger(GuardTrigger)
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Mission.Var("Option") == 1 &&
                        q.Mission.Var("GumbahDialog") == 1)
                    {
                        return QuestReply.ProgressEvent(177);
                    }

                    return null;
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid).OnTrigger(GuardTrigger)
                .OnEventFinish(Zone.BastokMines, 175, q => q.Mission.SetVar("GumbahDialog", 1))
                .OnEventFinish(Zone.BastokMines, 177, q =>
                {
                    q.Mission.SetVar("Option", 0);
                    q.Player.DelKeyItem(KeyItem.LetterFromWerei);
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Franziska).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) > 0)
                    {
                        return QuestReply.ProgressEvent(770);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Karst).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(765);
                    }
                    else if (missionStatus == 3)
                    {
                        int needsToDeliver = q.Mission.Var("Option");

                        return QuestReply.ProgressEvent(766, needsToDeliver);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc).OnTrigger(GuardTrigger)
                .OnEventFinish(Zone.Metalworks, 765, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.Metalworks, 766, q =>
                {
                    int blockingOption = q.Mission.Var("Option");

                    if (q.Mission.Complete())
                    {
                        // Cornelia has two options for which CS is displayed, depending on quest completion.
                        // This variable is cleared after viewing.
                        q.Mission.SetVar("Stage", 1);

                        // Gumbah dialogue is blocking before being able to progress.
                        // If this wasn't completed, make sure this var persists.
                        if (blockingOption == 1)
                        {
                            q.Mission.SetVar("Option", 1);
                        }
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus).OnTrigger(GuardTrigger)
                .Npc(Zone.PortBastok, Npc.PortBastok.Hilda).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(255);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 255, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.WaughroonShrine, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.OnMyWay)
                    {
                        q.Player.GiveKeyItem(KeyItem.LetterFromWerei);
                        q.Player.SetMissionStatus(AreaId, 3);
                        q.Mission.SetVar("Option", 1);
                    }
                });

            // Player has completed mission, but has not delivered letter to Gumbah
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Mission.Var("Option") == 1)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades).OnTrigger(QuestReply.ProgressEvent(1011))
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah).OnTrigger(q =>
                {
                    if (q.Mission.Var("GumbahDialog") == 1)
                    {
                        return QuestReply.ProgressEvent(177);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(175);
                    }
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid).OnTrigger(QuestReply.ProgressEvent(1011))
                .OnEventFinish(Zone.BastokMines, 175, q => q.Mission.SetVar("GumbahDialog", 1))
                .OnEventFinish(Zone.BastokMines, 177, q =>
                {
                    q.Mission.SetVar("Option", 0);
                    q.Player.DelKeyItem(KeyItem.LetterFromWerei);
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc).OnTrigger(QuestReply.ProgressEvent(1011))
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus).OnTrigger(QuestReply.ProgressEvent(1011));

            // Player has completed mission, optional Cornelia CS handling
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Mission.Var("Stage") == 1)
                .Npc(Zone.Metalworks, Npc.Metalworks._6lg).OnTrigger(q =>
                {
                    if (q.Player.HasCompletedQuest(QuestLog.Bastok, "BEAUTY_AND_THE_GALKA") &&
                        q.Player.HasCompletedQuest(QuestLog.Bastok, "FALLEN_COMRADES") &&
                        q.Player.HasCompletedQuest(QuestLog.Bastok, "RIVALS"))
                    {
                        return QuestReply.ProgressEvent(622);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(621);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 621, q => q.Mission.SetVar("Stage", 0))
                .OnEventFinish(Zone.Metalworks, 622, q => q.Mission.SetVar("Stage", 0));
        }
    }
}
