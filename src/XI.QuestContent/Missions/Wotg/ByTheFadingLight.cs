// By the Fading Light (WotG 39, Log ID 5, Mission ID 38), a line-for-line port of
// scripts/missions/wotg/39_By_the_Fading_Light.lua (quest rework C3 T7). Rally Point: Red !pos -106.071
// -25.5 -52.841 137. One section (the mission current):
// Xarcabard [S]'s Rally Point: Red plays the progress event 41 (137, 0, 0, 7), whose finish completes the mission
// (Edge of Existence current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ByTheFadingLight : MissionScript
    {
        public ByTheFadingLight() : base(XI.Quests.MissionLog.WoTG, "BY_THE_FADING_LIGHT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/39_By_the_Fading_Light";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.EdgeOfExistence,
            });

            b.State(current: true)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointRed)
                .OnTrigger(QuestReply.ProgressEvent(41, 137, 0, 0, 7))
                .OnEventFinish(Zone.XarcabardS, 41, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
