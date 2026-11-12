// In the Blood (ToAU 32, Log ID 4, Mission ID 31), a line-for-line port of
// scripts/missions/toau/32_In_the_Blood.lua (quest rework C3 T3). One section (the mission current), all in Aht Urhgan
// Whitegate. Naja Salaheem plays the progress event 3113 (the mercenary rank and 1), whose finish completes the
// mission (Sentinels' Honor current, an Imperial Gold Piece: with a full bag the completion fails and nothing else
// happens) and, when it did, sets the Timer var (Sentinels' Honor reads it) to the next Vana'diel day and a local
// var. Kept as the Lua has it: the finish sets Sentinels' Honor's mustZone as a local var, which getMustZone (a char
// var) never reads, so only the Timer delays the next mission. The ROV era module
// (Modules/Era/Missions/MissionAdjustments.cs) replaces the finish.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class InTheBlood : MissionScript
    {
        public InTheBlood() : base(XI.Quests.MissionLog.ToAU, "IN_THE_BLOOD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/32_In_the_Blood";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Item = Item.ImperialGoldPiece,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.SentinelsHonor,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3113, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3113, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetCharVar("Mission[4][32]Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetLocalVar("Mission[4][32]mustZone", 1);
                    }
                });
        }
    }
}
