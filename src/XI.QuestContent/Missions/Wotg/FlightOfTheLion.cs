// Flight of the Lion (WotG 35, Log ID 5, Mission ID 34), a line-for-line port of
// scripts/missions/wotg/35_Flight_of_the_Lion.lua (quest rework C3 T7). Bulwark Gate !pos -447.174 -1.831
// 342.417 98. One section (the mission current):
// Sauromugue Champaign [S]'s Bulwark Gate plays the progress event 10 (98, 300, 200, 100, 0, 9240578, 0, 0), whose
// finish completes the mission (Fall of the Hawk current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class FlightOfTheLion : MissionScript
    {
        public FlightOfTheLion() : base(XI.Quests.MissionLog.WoTG, "FLIGHT_OF_THE_LION") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/35_Flight_of_the_Lion";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.FallOfTheHawk,
            });

            b.State(current: true)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate)
                .OnTrigger(QuestReply.ProgressEvent(10, 98, 300, 200, 100, 0, 9240578, 0, 0))
                .OnEventFinish(Zone.SauromugueChampaignS, 10, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
