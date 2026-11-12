// A Vessel Without a Captain (Promathia 3-2, Log ID 6, Mission ID 318), a line-for-line port of
// scripts/missions/cop/3_2_A_Vessel_Without_a_Captain.lua (quest rework C3). Door: Neptune's Spire !pos 35 0 -15 245;
// Ru'Lude Homepoint 1 !pos -6 3 0.001 243. One section (the mission current): Metalworks Cid plays 861 once a zone
// whatever the status; Lower Jeuno's _6tc plays 86 at Status 0 (its finish sets Status 1) and Harnek plays event 12 at
// Status 1; Ru'Lude Gardens' trigger area 1 at Status 1 plays 65, whose finish completes the mission. The Road Forks is
// next; no title. Kept as the Lua has it: the Lua passes (nation, hasDefeatedShadowlord) to event 65, a boolean that
// the event packet's number parameters drop, so only the nation is sent (the recorded traces are the same with The
// Shadow Lord completed or not); the Lua's lookup of that boolean has no effect and is not ported.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class AVesselWithoutACaptain : MissionScript
    {
        public AVesselWithoutACaptain() : base(XI.Quests.MissionLog.CoP, "A_VESSEL_WITHOUT_A_CAPTAIN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/3_2_A_Vessel_Without_a_Captain";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheRoadForks,
            });

            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.ProgressEvent(861).OncePerZone())
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(86) : null)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Harnek).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.Event(12).ImportantEvent() : null)
                .OnEventFinish(Zone.LowerJeuno, 86, q => q.Mission.SetVar("Status", 1))
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    if (q.Mission.Var("Status") != 1) return null;
                    int nation = q.Player.Nation;
                    return QuestReply.ProgressEvent(65, nation);
                })
                .OnEventFinish(Zone.RuludeGardens, 65, q => q.Mission.Complete());
        }
    }
}
