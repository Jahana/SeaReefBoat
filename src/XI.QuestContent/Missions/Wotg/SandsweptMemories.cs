// Sandswept Memories (WotG 16, Log ID 5, Mission ID 15), a line-for-line port of
// scripts/missions/wotg/16_Sandswept_Memories.lua (quest rework C3 T6). One section (the mission current): Southern
// San d'Oria [S]'s Lion Springs plays the progress event 146 (the campaign allegiance, 0, 0, 0, 0, 0, 0, 4095),
// whose finish completes the mission (Northland Exposure current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class SandsweptMemories : MissionScript
    {
        public SandsweptMemories() : base(XI.Quests.MissionLog.WoTG, "SANDSWEPT_MEMORIES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/16_Sandswept_Memories";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.NorthlandExposure,
            });

            b.State(current: true)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings)
                .OnTrigger(q => QuestReply.ProgressEvent(146, q.Player.CampaignAllegiance, 0, 0, 0, 0, 0, 0, 4095))
                .OnEventFinish(Zone.SouthernSanDoriaS, 146, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
