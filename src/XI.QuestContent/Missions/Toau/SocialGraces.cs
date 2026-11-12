// Social Graces (ToAU 23, Log ID 4, Mission ID 22), a line-for-line port of scripts/missions/toau/23_Social_Graces.lua
// (quest rework C3 T2). One section (the mission current): Aht Urhgan Whitegate's trigger area 3 plays the progress
// event 3095 (0, 1, and a 4 at the eighth operand). 3095's update with option 1 answers 1, 1; its finish sets Foiled
// Ambition's mustZone local var and the Timer char var to the next Vana'diel day, then completes the mission (Foiled
// Ambition current). Kept as the Lua has it: the finish sets the next mission's mustZone as a local var, which
// getMustZone (a char var) never reads, so only the Timer delays the next mission.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class SocialGraces : MissionScript
    {
        public SocialGraces() : base(XI.Quests.MissionLog.ToAU, "SOCIAL_GRACES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/23_Social_Graces";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.FoiledAmbition,
            });

            b.State(current: true)
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    return QuestReply.ProgressEvent(3095, 0, 1, 0, 0, 0, 0, 0, 4);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3095, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3095, q =>
                {
                    q.Player.SetLocalVar("Mission[4][23]mustZone", 1);
                    q.Player.SetCharVar("Mission[4][23]Timer", q.Player.VanadielUniqueDay + 1);
                    q.Mission.Complete();
                });
        }
    }
}
