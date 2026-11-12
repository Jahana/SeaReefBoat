// Another World (WotG 28, Log ID 5, Mission ID 27), a line-for-line port of scripts/missions/wotg/28_Another_World.lua
// (quest rework C3 T7). Halver !pos 2 0.1 0.1 233. One section (the mission current): the zone-in to Southern
// San d'Oria from East Ronfaure plays the plain event 945, whose finish sets Prog 1; Halver in Chateau d'Oraguille at
// Prog 1 plays the progress event 566 (233, 23, 1756, 0, 0, 0, 1, 1), whose finish completes the mission (A Hawk in
// Repose current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class AnotherWorld : MissionScript
    {
        public AnotherWorld() : base(XI.Quests.MissionLog.WoTG, "ANOTHER_WORLD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/28_Another_World";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.AHawkInRepose,
            });

            b.State(current: true)
                .OnZoneIn(Zone.SouthernSanDoria, q =>
                    q.Player.PreviousZone == Zone.EastRonfaure ? QuestReply.Event(945) : null)
                .OnEventFinish(Zone.SouthernSanDoria, 945, q =>
                {
                    q.Mission.SetVar("Prog", 1);
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Mission.Var("Prog") == 1)
                    {
                        // the Lua's TODO: compare the last two parameters for a char of the Bastok or Windurst
                        // allegiance
                        return QuestReply.ProgressEvent(566, 233, 23, 1756, 0, 0, 0, 1, 1);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 566, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
