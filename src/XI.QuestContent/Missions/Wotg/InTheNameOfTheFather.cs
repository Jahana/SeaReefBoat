// In the Name of the Father (WotG 8, Log ID 5, Mission ID 7), a line-for-line port of
// scripts/missions/wotg/08_In_the_Name_of_the_Father.lua (quest rework C3 T5). Lion Springs Door !pos 96 0 106 80. One
// section (the mission current and xi.wotg.helpers.meetsMission8Reqs, which stays Lua): Southern San d'Oria [S]'s Lion
// Springs plays the progress event 85 (the char's Campaign allegiance, 13, 0, 1, 3, 4, 8, 3), whose finish completes
// the mission (Dancers in Distress current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class InTheNameOfTheFather : MissionScript
    {
        public InTheNameOfTheFather() : base(XI.Quests.MissionLog.WoTG, "IN_THE_NAME_OF_THE_FATHER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/08_In_the_Name_of_the_Father";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.DancersInDistress,
            });

            b.State(current: true, when: q => q.Player.WotgMeetsMissionReqs(8))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings).OnTrigger(q =>
                    QuestReply.ProgressEvent(85, q.Player.CampaignAllegiance, 13, 0, 1, 3, 4, 8, 3))
                .OnEventFinish(Zone.SouthernSanDoriaS, 85, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
