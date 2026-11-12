// Return to Delkfutt's Tower (Zilart 8, Log ID 3, Mission ID 16), a line-for-line port of
// scripts/missions/rotz/08_Return_to_Delkfutts_Tower.lua (quest rework C3, C5). Gilgamesh !pos 122.452 -9.009 -12.052
// 252; Aldo !pos 20 3 -58 245; Lower Delkfutt's Tower !zone 184; Stellar Fulcrum !zone 179; Qe'Lov Gate (BCNM)
// !pos -520 -4 17 179. Section 1 (current): the Lower Delkfutt's Tower zone-in plays 14 from Middle Delkfutt's Tower,
// else 15 while bit 1 of the Option var is clear; 15's finish sets bit 1. Lower Jeuno's Aldo plays 104 while bit 0
// is clear, whose finish sets it, else 68. Norg's Gilgamesh plays 13. The Stellar Fulcrum zone-in plays 0 at status 0,
// 17 at status 2, else 7 from Upper Delkfutt's Tower; 0's update on option 0 answers (1, 23, 1756, 488, 179, 7, 0,
// 0); 0's finish sets status 1; 17's finish completes the mission (Ro'Maeve next; no key item, no title). The 32001
// finish of a Return to Delkfutt's Tower win (Battlefields/StellarFulcrum/ReturnToDelkfuttsTower.cs sets
// battlefieldWin) sets status 2 and moves the char to (-519.99, 1.076, -19.943, 64) in the Stellar Fulcrum, whose
// zone-in plays 17. Kept as the Lua has it: the two Option bits are optional cutscenes, gating nothing; the 14 and the
// 7 arms play the same events the zones' own Zone.lua zone-ins play; the 7 arm is not gated on the status (status 1,
// 3 and above alike).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class ReturnToDelkfuttsTower : MissionScript
    {
        public ReturnToDelkfuttsTower() : base(XI.Quests.MissionLog.Zilart, "RETURN_TO_DELKFUTTS_TOWER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/08_Return_to_Delkfutts_Tower";

        // mission.areaId
        const int AreaId = MissionLog.Zilart;

        // Bits
        // 0 -> Lower Jeuno Aldo interaction. Optional.
        // 1 -> Lower Delkfutt tower Zone-in cutscene. Optional. Yes. Really.
        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.Romaeve,
            });

            // Section: Mission Active
            b.State(current: true)
                .OnZoneIn(Zone.LowerDelkfuttsTower, q =>
                {
                    if (q.Player.PreviousZone == Zone.MiddleDelkfuttsTower)
                    {
                        return QuestReply.Event(14); // Ensure regular entering CS plays.
                    }
                    else if (!q.Mission.IsVarBitsSet("Option", 1))
                    {
                        return QuestReply.Event(15); // Optional cutscene. Plays on any arrival, including teleports.
                    }

                    return null;
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 15, q => q.Mission.SetVarBit("Option", 1))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Option", 0))
                    {
                        return QuestReply.ProgressEvent(104, 0, 1, 255, 0, 12590582, 95569, 4095, 1);
                    }
                    else
                    {
                        return QuestReply.Event(68);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 104, q => q.Mission.SetVarBit("Option", 0))
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(13))
                .OnZoneIn(Zone.StellarFulcrum, q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);
                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(0); // Pre-Battle.
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(17); // Post-Battle. Mission complete event.
                    }
                    else if (q.Player.PreviousZone == Zone.UpperDelkfuttsTower)
                    {
                        return QuestReply.Event(7); // Ensure regular entering CS plays.
                    }

                    return null;
                })
                .OnEventUpdate(Zone.StellarFulcrum, 0, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(1, 23, 1756, 488, 179, 7, 0, 0);
                    }
                })
                .OnEventFinish(Zone.StellarFulcrum, 0, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.StellarFulcrum, 17, q => q.Mission.Complete())
                .OnEventFinish(Zone.StellarFulcrum, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.ReturnToDelkfuttsTower)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                        q.Player.SetPos(-519.99, 1.076, -19.943, 64, Zone.StellarFulcrum);
                    }
                });
        }
    }
}
