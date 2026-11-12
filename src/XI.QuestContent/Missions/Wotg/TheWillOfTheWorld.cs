// The Will of the World (WotG 25, Log ID 5, Mission ID 24), a line-for-line port of
// scripts/missions/wotg/25_The_Will_of_the_World.lua (quest rework C3 T6). One section (the mission current):
// Southern San d'Oria [S]'s Raustigne plays the progress event 149 (the campaign allegiance, 5, 2964, 0, 0, 0, 1,
// 4095), whose finish completes the mission (Fate in Haze current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TheWillOfTheWorld : MissionScript
    {
        public TheWillOfTheWorld() : base(XI.Quests.MissionLog.WoTG, "THE_WILL_OF_THE_WORLD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/25_The_Will_of_the_World";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.FateInHaze,
            });

            b.State(current: true)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Raustigne)
                .OnTrigger(q => QuestReply.ProgressEvent(149, q.Player.CampaignAllegiance, 5, 2964, 0, 0, 0, 1, 4095))
                .OnEventFinish(Zone.SouthernSanDoriaS, 149, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
