// Cait in the Woods (WotG 49, Log ID 5, Mission ID 48), a line-for-line port of
// scripts/missions/wotg/49_Cait_in_the_Woods.lua (quest rework C3 T8). Blank (Cait Sith) !pos 221.857 -49.213 176.24
// 81. One section (the mission current), in East Ronfaure [S]: blank_cait plays the progress event 16 (81, 300, 200,
// 100, 0, 7405585, 0, 22292), whose finish completes the mission (the Ronfaure Dawndrop key item, Fork in the Road
// current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class CaitInTheWoods : MissionScript
    {
        public CaitInTheWoods() : base(XI.Quests.MissionLog.WoTG, "CAIT_IN_THE_WOODS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/49_Cait_in_the_Woods";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.RonfaureDawndrop,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ForkInTheRoad,
            });

            b.State(current: true)
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.blankcait)
                .OnTrigger(QuestReply.ProgressEvent(16, 81, 300, 200, 100, 0, 7405585, 0, 22292))
                .OnEventFinish(Zone.EastRonfaureS, 16, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
