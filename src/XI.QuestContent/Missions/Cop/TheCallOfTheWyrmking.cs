// The Call of the Wyrmking (Promathia 3-1, Log ID 6, Mission ID 258), a line-for-line port of
// scripts/missions/cop/3_1_The_Call_of_the_Wyrmking.lua (quest rework C3). Port Bastok HP !pos -126 -6 10 236; Cid !pos
// -12 -12 1 237. One section (the mission current): the South Gustaberg zone-in at Status 0 plays 906 (its finish sets
// Status 1); Port Bastok's trigger area 1 at Status 1 plays 305 (Status 2); Metalworks Cid at Status 2 plays 845, whose
// finish completes the mission. A Vessel Without a Captain is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheCallOfTheWyrmking : MissionScript
    {
        public TheCallOfTheWyrmking() : base(XI.Quests.MissionLog.CoP, "THE_CALL_OF_THE_WYRMKING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/3_1_The_Call_of_the_Wyrmking";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.AVesselWithoutACaptain,
            });

            b.State(current: true)
                .OnZoneIn(Zone.SouthGustaberg, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(906) : null)
                .OnEventFinish(Zone.SouthGustaberg, 906, q => q.Mission.SetVar("Status", 1))
                .OnTriggerAreaEnter(Zone.PortBastok, 1, q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(305) : null)
                .OnEventFinish(Zone.PortBastok, 305, q => q.Mission.SetVar("Status", 2))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.ProgressEvent(845) : null)
                .OnEventFinish(Zone.Metalworks, 845, q => q.Mission.Complete());
        }
    }
}
