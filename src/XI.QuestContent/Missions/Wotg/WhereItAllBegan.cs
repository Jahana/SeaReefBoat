// Where It All Began (WotG 52, Log ID 5, Mission ID 51), a line-for-line port of
// scripts/missions/wotg/52_Where_It_All_Began.lua (quest rework C3 T8). Lion Springs Door !pos 96 0 106 80. One
// section (the mission current), in Southern San d'Oria [S]: Lion Springs plays the progress event 175 (80, 72), whose
// finish completes the mission (the Wedding Invitation key item, A Token of Troth current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class WhereItAllBegan : MissionScript
    {
        public WhereItAllBegan() : base(XI.Quests.MissionLog.WoTG, "WHERE_IT_ALL_BEGAN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/52_Where_It_All_Began";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.WeddingInvitation,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ATokenOfTroth,
            });

            b.State(current: true)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings)
                .OnTrigger(QuestReply.ProgressEvent(175, 80, 72))
                .OnEventFinish(Zone.SouthernSanDoriaS, 175, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
