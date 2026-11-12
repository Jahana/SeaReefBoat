// Path of Blood (ToAU 37, Log ID 4, Mission ID 36), a line-for-line port of
// scripts/missions/toau/37_Path_of_Blood.lua (quest rework C3 T3). One section (the mission current), all in Aht
// Urhgan Whitegate. Trigger area 3 plays the progress event 3131 (a 1 at the second operand); its update answers 2, 1
// whatever the option; its finish sets status 1 and puts the char at 97.936 0 0.109 (rotation 0) in the Whitegate
// (the forced zone, "retail accurate"). The zone-in at status 1 plays the plain event 3220, whose finish completes
// the mission (Stirrings of War current) and, when it did, sets the Timer var (Stirrings of War reads it) to the next
// Vana'diel day and a local var. Kept as the Lua has it: the finish sets Stirrings of War's mustZone as a local var,
// which getMustZone (a char var) never reads, so only the Timer delays the next mission. The ROV era module
// (Modules/Era/Missions/MissionAdjustments.cs) replaces the 3220 finish.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PathOfBlood : MissionScript
    {
        public PathOfBlood() : base(XI.Quests.MissionLog.ToAU, "PATH_OF_BLOOD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/37_Path_of_Blood";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.StirringsOfWar,
            });

            b.State(current: true)
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    return QuestReply.ProgressEvent(3131, 0, 1, 0, 0, 0, 0, 0);
                })
                .OnZoneIn(Zone.AhtUrhganWhitegate, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.Event(3220);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3131, q =>
                {
                    q.Player.UpdateEvent(2, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3131, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    // Force zone. Retail accurate.
                    q.Player.SetPos(97.936, 0, 0.109, 0, Zone.AhtUrhganWhitegate);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3220, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetLocalVar("Mission[4][37]mustZone", 1);
                        q.Player.SetCharVar("Mission[4][37]Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });
        }
    }
}
