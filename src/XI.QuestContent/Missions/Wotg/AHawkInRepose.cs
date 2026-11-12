// A Hawk in Repose (WotG 29, Log ID 5, Mission ID 28), a line-for-line port of
// scripts/missions/wotg/29_A_Hawk_in_Repose.lua (quest rework C3 T7). Weathered Gravestone !pos 149.728 -5.109 -395.121
// 105. One section (the mission current), in Batallia Downs: the Weathered Gravestone at Status 0 plays the progress
// event 502 (105, 2, 300000, 0, 0, 0, 1, 22262), whose finish sets Status 1; a trade of exactly one Lilac at Status 1
// plays the progress event 503 (0, 23, 1753, 0, 0, 0, 1, 3871), whose finish takes the trade and completes the mission
// (The Battle of Xarcabard current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class AHawkInRepose : MissionScript
    {
        public AHawkInRepose() : base(XI.Quests.MissionLog.WoTG, "A_HAWK_IN_REPOSE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/29_A_Hawk_in_Repose";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TheBattleOfXarcabard,
            });

            b.State(current: true)
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.WeatheredGravestone).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.Lilac, 1)) && q.Mission.Var("Status") == 1)
                    {
                        return QuestReply.ProgressEvent(503, 0, 23, 1753, 0, 0, 0, 1, 3871);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 0)
                    {
                        return QuestReply.ProgressEvent(502, 105, 2, 300000, 0, 0, 0, 1, 22262);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BatalliaDowns, 502, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.BatalliaDowns, 503, q =>
                {
                    q.Trade.Complete();
                    q.Mission.Complete();
                });
        }
    }
}
