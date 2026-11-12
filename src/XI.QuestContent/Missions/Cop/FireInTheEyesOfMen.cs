// Fire in the Eyes of Men (Promathia 7-3, Log ID 6, Mission ID 728), a line-for-line port of
// scripts/missions/cop/7_3_Fire_in_the_Eyes_of_Men.lua (quest rework C3, C4). The mission current. Mine Shaft #2716's
// _0d0 plays 4 at Status 0 (Status 1). Metalworks' Cid at Status 1: 857 while Timer is 0 (its finish sets Timer to
// the next Vana'diel day), 890 once Timer is at or below the day (its finish completes the mission), else the
// important event 858. Calm Before the Storm is next, with the Prishe's Buddy title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class FireInTheEyesOfMen : MissionScript
    {
        public FireInTheEyesOfMen() : base(XI.Quests.MissionLog.CoP, "FIRE_IN_THE_EYES_OF_MEN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/7_3_Fire_in_the_Eyes_of_Men";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.PrishesBuddy,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.CalmBeforeTheStorm,
            });

            b.State(current: true)
                .Npc(Zone.MineShaft2716, Npc.MineShaft2716._0d0).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(4) : null)
                .OnEventFinish(Zone.MineShaft2716, 4, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    int waitTimer = q.Mission.Var("Timer");

                    if (q.Mission.Var("Status") == 1)
                    {
                        if (waitTimer == 0)
                        {
                            return QuestReply.ProgressEvent(857);
                        }
                        else if (waitTimer <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(890);
                        }
                        else
                        {
                            return QuestReply.Event(858).ImportantEvent();
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 857, q => q.Mission.SetVar("Timer", q.Player.VanadielUniqueDay + 1))
                .OnEventFinish(Zone.Metalworks, 890, q => q.Mission.Complete());
        }
    }
}
