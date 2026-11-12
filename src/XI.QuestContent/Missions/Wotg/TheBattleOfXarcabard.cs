// The Battle of Xarcabard (WotG 30, Log ID 5, Mission ID 29), a line-for-line port of
// scripts/missions/wotg/30_The_Battle_of_Xarcabard.lua (quest rework C3 T7). Rally Point: Red !pos -106.071 -25.5
// -52.841 137. One section (the mission current), in Xarcabard [S]: the zone-in at Status 0 plays the plain event 17,
// whose finish sets Status 1; Rally Point: Red at Status 1 plays the progress event 18 (0, 5, 0, 0, 0, 0, 0, 1), whose
// finish completes the mission (Prelude to a Storm current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TheBattleOfXarcabard : MissionScript
    {
        public TheBattleOfXarcabard() : base(XI.Quests.MissionLog.WoTG, "THE_BATTLE_OF_XARCABARD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/30_The_Battle_of_Xarcabard";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.PreludeToAStorm,
            });

            b.State(current: true)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointRed).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 1)
                    {
                        // the Lua's TODO: compare the last parameter for a char of the Bastok or Windurst allegiance
                        return QuestReply.ProgressEvent(18, 0, 5, 0, 0, 0, 0, 0, 1);
                    }

                    return null;
                })
                .OnZoneIn(Zone.XarcabardS, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(17) : null)
                .OnEventFinish(Zone.XarcabardS, 17, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.XarcabardS, 18, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
