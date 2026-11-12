// Time Slips Away (WotG 45, Log ID 5, Mission ID 44), a line-for-line port of
// scripts/missions/wotg/45_Time_Slips_Away.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749 585.239
// 89. One section (the mission current), in Grauberg [S]: a trade of exactly one Punch Bug plays the progress event 35
// (64, 23, 1756, 0, 0, 0, 0, 0), whose finish completes the mission (the Bottled Punch Bug key item, When Wills
// Collide current) and then, when it completed, takes the trade; a trigger plays the plain event 40 (89, 10, 1, 0, 0,
// 0, 0, 0).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TimeSlipsAway : MissionScript
    {
        public TimeSlipsAway() : base(XI.Quests.MissionLog.WoTG, "TIME_SLIPS_AWAY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/45_Time_Slips_Away";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.BottledPunchBug,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.WhenWillsCollide,
            });

            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.PunchBug, 1)))
                    {
                        return QuestReply.ProgressEvent(35, 64, 23, 1756, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnTrigger(QuestReply.Event(40, 89, 10, 1, 0, 0, 0, 0, 0))
                .OnEventFinish(Zone.GraubergS, 35, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
