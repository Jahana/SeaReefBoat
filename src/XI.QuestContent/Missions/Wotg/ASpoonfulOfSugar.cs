// A Spoonful of Sugar (WotG 11, Log ID 5, Mission ID 10), a line-for-line port of
// scripts/missions/wotg/11_A_Spoonful_of_Sugar.lua (quest rework C3 T5). One section (the mission current, any status):
// Southern San d'Oria [S]'s Raustigne plays the progress event 87 (the char's Campaign allegiance, 7, 0, 0, 0, 0, 3,
// 4095), whose finish completes the mission (Affairs of State current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ASpoonfulOfSugar : MissionScript
    {
        public ASpoonfulOfSugar() : base(XI.Quests.MissionLog.WoTG, "A_SPOONFUL_OF_SUGAR") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/11_A_Spoonful_of_Sugar";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.AffairsOfState,
            });

            b.State(current: true)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Raustigne).OnTrigger(q =>
                    QuestReply.ProgressEvent(87, q.Player.CampaignAllegiance, 7, 0, 0, 0, 0, 3, 4095))
                .OnEventFinish(Zone.SouthernSanDoriaS, 87, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
