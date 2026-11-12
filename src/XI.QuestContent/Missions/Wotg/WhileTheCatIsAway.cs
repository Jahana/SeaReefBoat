// While the Cat is Away (WotG 5, Log ID 5, Mission ID 4), a line-for-line port of
// scripts/missions/wotg/05_While_the_Cat_is_Away.lua (quest rework C3 T5). One section (the mission current, any
// status): the zone-in to East Ronfaure [S] from Southern San d'Oria [S] plays 7; its update (option 0) updates the
// event with four flags (Burden of Suspicion done, Wrath of the Griffon done, A Manifest Problem done, the Rhapsodies
// of Vanadiel mission Ganged Up On or later), 0, 7733257, 0, 0; its finish completes the mission (A Timeswept Butterfly
// current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class WhileTheCatIsAway : MissionScript
    {
        public WhileTheCatIsAway() : base(XI.Quests.MissionLog.WoTG, "WHILE_THE_CAT_IS_AWAY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/05_While_the_Cat_is_Away";

        // player:hasCompletedQuest(xi.questLog.CRYSTAL_WAR, quest) and 1 or 0
        static int Done(IQuestContext q, string quest) =>
            q.Player.HasCompletedQuest(XI.Quests.QuestLog.CrystalWar, quest) ? 1 : 0;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ATimesweptButterfly,
            });

            b.State(current: true)
                .OnZoneIn(Zone.EastRonfaureS, q =>
                    q.Player.PreviousZone == Zone.SouthernSanDoriaS ? QuestReply.Event(7) : null)
                .OnEventUpdate(Zone.EastRonfaureS, 7, q =>
                {
                    if (q.Option == 0)
                    {
                        int sandyFlag = Done(q, "BURDEN_OF_SUSPICION");
                        int bastokFlag = Done(q, "WRATH_OF_THE_GRIFFON");
                        int windyFlag = Done(q, "A_MANIFEST_PROBLEM");
                        int rovFlag = q.Player.CurrentMission(MissionLog.Rov) >= Mission.Rov.GangedUpOn ? 1 : 0;

                        q.Player.UpdateEvent(sandyFlag, bastokFlag, windyFlag, rovFlag, 0, 7733257, 0, 0);
                    }
                })
                .OnEventFinish(Zone.EastRonfaureS, 7, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
