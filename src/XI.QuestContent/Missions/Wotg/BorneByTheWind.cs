// Borne by the Wind (WotG 13, Log ID 5, Mission ID 12), a line-for-line port of
// scripts/missions/wotg/13_Borne_by_the_Wind.lua (quest rework C3 T5). One section (status 0): Bastok Markets [S]'s
// Radford plays the plain event 177 (87, 23); Windurst Waters [S]'s Velda-Galda plays the plain event 180 (94, 3);
// Sauromugue Champaign [S]'s Bulwark Gate plays the progress event 5 (98, 0, 2963, 0, 0, 0, 1, 0), whose finish
// completes the mission (A Nation on the Brink current, the Underpass Hatch Key given).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class BorneByTheWind : MissionScript
    {
        public BorneByTheWind() : base(XI.Quests.MissionLog.WoTG, "BORNE_BY_THE_WIND") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/13_Borne_by_the_Wind";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.UnderpassHatchKey,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ANationOnTheBrink,
            });

            b.State(current: true, status: 0)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Radford).OnTrigger(QuestReply.Event(177, 87, 23))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.VeldaGalda).OnTrigger(QuestReply.Event(180, 94, 3))
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate)
                .OnTrigger(QuestReply.ProgressEvent(5, 98, 0, 2963, 0, 0, 0, 1, 0))
                .OnEventFinish(Zone.SauromugueChampaignS, 5, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
