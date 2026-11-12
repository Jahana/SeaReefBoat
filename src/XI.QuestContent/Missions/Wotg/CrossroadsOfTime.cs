// Crossroads of Time (WotG 15, Log ID 5, Mission ID 14), a line-for-line port of
// scripts/missions/wotg/15_Crossroads_of_Time.lua (quest rework C3 T6). One section (the mission current and
// xi.wotg.helpers.meetsMission15Reqs, which stays Lua): the Southern San d'Oria [S] zone-in plays the plain event
// 145 when the char came from East Ronfaure [S], and its finish completes the mission (Sandswept Memories current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class CrossroadsOfTime : MissionScript
    {
        public CrossroadsOfTime() : base(XI.Quests.MissionLog.WoTG, "CROSSROADS_OF_TIME") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/15_Crossroads_of_Time";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.SandsweptMemories,
            });

            b.State(current: true, when: q => q.Player.WotgMeetsMissionReqs(15))
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                {
                    if (q.Player.PreviousZone == Zone.EastRonfaureS)
                    {
                        return QuestReply.Event(145);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 145, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
