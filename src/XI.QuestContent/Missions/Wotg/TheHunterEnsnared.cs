// The Hunter Ensnared (WotG 34, Log ID 5, Mission ID 33), a line-for-line port of
// scripts/missions/wotg/34_The_Hunter_Ensnared.lua (quest rework C3 T7). Rally Point: Red !pos -106.071
// -25.5 -52.841 137. One section (the mission current):
// Xarcabard [S]'s Rally Point: Red plays the progress event 24 (137, 23, 1756, 0, 65863679, 8553890, 4095, 0), whose
// finish completes the mission (Flight of the Lion current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TheHunterEnsnared : MissionScript
    {
        public TheHunterEnsnared() : base(XI.Quests.MissionLog.WoTG, "THE_HUNTER_ENSNARED") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/34_The_Hunter_Ensnared";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.FlightOfTheLion,
            });

            b.State(current: true)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointRed)
                .OnTrigger(QuestReply.ProgressEvent(24, 137, 23, 1756, 0, 65863679, 8553890, 4095, 0))
                .OnEventFinish(Zone.XarcabardS, 24, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
