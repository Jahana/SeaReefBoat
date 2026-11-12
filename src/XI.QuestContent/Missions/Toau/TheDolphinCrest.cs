// The Dolphin Crest (ToAU 14, Log ID 4, Mission ID 13), a line-for-line port of
// scripts/missions/toau/14_The_Dolphin_Crest.lua (quest rework C3 T2). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current): Naja plays the progress event 3072 (the mercenary rank, and 1), whose finish completes
// the mission (The Black Coffin current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class TheDolphinCrest : MissionScript
    {
        public TheDolphinCrest() : base(XI.Quests.MissionLog.ToAU, "THE_DOLPHIN_CREST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/14_The_Dolphin_Crest";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.TheBlackCoffin,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3072, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3072, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
