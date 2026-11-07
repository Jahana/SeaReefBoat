// Save the Children (San d'Oria 1-3, Log ID 0, Mission ID 2), a line-for-line port of
// scripts/missions/sandoria/1_3_Save_the_Children.lua (quest rework C2). It handles both the first completion and
// repeats (!addmission 0 2). Ambrotien !pos 93.419 -0.001 -57.347 230; Grilau !pos -241.987 6.999 57.887 231;
// Endracion !pos -110 1 -34 230; Arnau !pos 148 0 139 231; Hut_Door !pos -165.357 -11.672 77.771 140.
// Section 1 (no San d'Oria mission current, a San d'Orian): the 1009 and 2009 finishes of Southern San d'Oria and the
// 1009 finish of Northern San d'Oria begin the mission on option 102, at status 1. Section 2 (1-3 current, status
// below 3): Arnau plays 693 below status 2, whose finish sets status 2; Grilau, Ambrotien and Endracion play 1025 /
// 2025 / 1025 at status 0, else their zone's ORIGINAL_MISSION_OFFSET + 24; Ghelsba Outpost's 32001 finish, when
// battlefieldWin is Save the Children (the battlefield sets it, Battlefields/GhelsbaOutpost/SaveTheChildren.cs), gives
// the Orcish Hut Key and the Fodderchief Flayer title and sets status 3. Section 3 (1-3 current, status 3, the key):
// Hut_Door plays 3 when 1-3 was completed before, else 55; either finish takes the key and sets status 4. Section 4
// (1-3 current, status 4): Ambrotien, Endracion and Grilau play their first or repeat event, whose finishes give rank 2
// and 1000 gil (first) or 250 rank points (repeat), then complete the mission and set its Option var 1. Section 5 (1-3
// completed, Option 1): Arnau plays 694, whose finish sets Option 0. Kept as the Lua has it: the reward table is empty
// (mission:complete gives nothing); the nation is compared with mission.areaId (the log id, 0, which is
// xi.nation.SANDORIA); handlers read the status with player:getMissionStatus(areaId); a status of 2 or more in
// section 2 makes Arnau return nothing (his default plays); Option is set only when mission:complete answers true.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class SaveTheChildren : MissionScript
    {
        public SaveTheChildren() : base(XI.Quests.MissionLog.Sandoria, "SAVE_THE_CHILDREN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/1_3_Save_the_Children";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.SaveTheChildren;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 102)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 1);
            }
        }

        static void HandleCompleteEventFinish(IQuestContext q)
        {
            if (!q.Player.HasCompletedMission(AreaId, MissionId))
            {
                q.Player.SetRank(2);
                q.Player.GiveCurrency("gil", 1000);
            }
            else
            {
                q.Player.AddRankPoints(250);
            }

            if (q.Mission.Complete())
            {
                q.Mission.SetVar("Option", 1);
            }
        }

        static QuestReply HandleCompleteTrigger(IQuestContext q, int firstId, int repeatId)
        {
            if (!q.Player.HasCompletedMission(AreaId, MissionId))
            {
                return QuestReply.ProgressEvent(firstId);
            }
            else
            {
                return QuestReply.ProgressEvent(repeatId);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            // mission.reward: First Time Completion (gil = 1000, rank = 2) and Repeat Only (rankPoints = 250) are
            // commented out in the Lua, so the table is empty, as Mission.reward's default: no b.Reward

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // The Arnau CS is only required if the mission has not been completed before; however,
            // it is still optional on repeat.  This is accounted for in the bcnm.lua
            // entry requirements for the mission.
            b.State(current: true, when: q => q.Mission.Status < 3)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Arnau).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) < 2)
                    {
                        return QuestReply.ProgressEvent(693);
                    }

                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(1025);
                    }
                    else
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.NorthernSanDoria.ORIGINAL_MISSION_OFFSET) + 24);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 693, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(2025);
                    }
                    else
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET) + 24);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(1025);
                    }
                    else
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.SouthernSanDoria.ORIGINAL_MISSION_OFFSET) + 24);
                    }
                })
                .OnEventFinish(Zone.GhelsbaOutpost, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.SaveTheChildren)
                    {
                        q.Player.GiveKeyItem(KeyItem.OrcishHutKey);
                        q.Player.SetTitle(Title.FodderchiefFlayer);
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                });

            b.State(current: true, when: q => q.Mission.Status == 3 && q.Player.HasKeyItem(KeyItem.OrcishHutKey))
                .Npc(Zone.GhelsbaOutpost, Npc.GhelsbaOutpost.HutDoor).OnTrigger(q =>
                {
                    if (q.Player.HasCompletedMission(AreaId, MissionId))
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(55);
                    }
                })
                .OnEventFinish(Zone.GhelsbaOutpost, 3, q =>
                {
                    q.Player.DelKeyItem(KeyItem.OrcishHutKey);
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                .OnEventFinish(Zone.GhelsbaOutpost, 55, q =>
                {
                    q.Player.DelKeyItem(KeyItem.OrcishHutKey);
                    q.Player.SetMissionStatus(AreaId, 4);
                });

            // BCNM has been completed successfully, and Hut Door checked afterwards
            b.State(current: true, when: q => q.Mission.Status == 4)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien)
                .OnTrigger(q => HandleCompleteTrigger(q, 2004, 2024))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion)
                .OnTrigger(q => HandleCompleteTrigger(q, 1004, 1024))
                .OnEventFinish(Zone.SouthernSanDoria, 1004, HandleCompleteEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 1024, HandleCompleteEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2004, HandleCompleteEventFinish)
                .OnEventFinish(Zone.SouthernSanDoria, 2024, HandleCompleteEventFinish)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau)
                .OnTrigger(q => HandleCompleteTrigger(q, 1004, 1024))
                .OnEventFinish(Zone.NorthernSanDoria, 1004, HandleCompleteEventFinish)
                .OnEventFinish(Zone.NorthernSanDoria, 1024, HandleCompleteEventFinish);

            // Optional CS after completing this mission.  This var should persist until
            // it is viewed, and is maintained after mission:complete()
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) && q.Mission.Var("Option") == 1)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Arnau).OnTrigger(QuestReply.ProgressEvent(694))
                .OnEventFinish(Zone.NorthernSanDoria, 694, q =>
                {
                    q.Mission.SetVar("Option", 0);
                });
        }
    }
}
