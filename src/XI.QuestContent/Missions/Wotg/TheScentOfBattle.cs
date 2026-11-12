// The Scent of Battle (WotG 27, Log ID 5, Mission ID 26), a line-for-line port of
// scripts/missions/wotg/27_The_Scent_of_Battle.lua (quest rework C3 T6). One section (the mission current):
// Sauromugue Champaign [S]'s Bulwark Gate plays the progress event 9 (98, 5, 0, 0, utils.MAX_UINT32 - 1611137536,
// utils.MAX_UINT32 - 41990201, 3, 1), whose finish completes the mission (Another World current). The Lua gives the
// NPC a fixed reply table, so the campaign allegiance is not read.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TheScentOfBattle : MissionScript
    {
        public TheScentOfBattle() : base(XI.Quests.MissionLog.WoTG, "THE_SCENT_OF_BATTLE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/27_The_Scent_of_Battle";

        // utils.MAX_UINT32 - n
        static int MaxUint32Minus(uint n) => unchecked((int)(uint.MaxValue - n));

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.AnotherWorld,
            });

            b.State(current: true)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate)
                .OnTrigger(QuestReply.ProgressEvent(9, 98, 5, 0, 0, MaxUint32Minus(1611137536),
                    MaxUint32Minus(41990201), 3, 1))
                .OnEventFinish(Zone.SauromugueChampaignS, 9, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
