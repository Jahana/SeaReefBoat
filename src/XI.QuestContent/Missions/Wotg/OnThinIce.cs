// On Thin Ice (WotG 20, Log ID 5, Mission ID 19), a line-for-line port of scripts/missions/wotg/20_On_Thin_Ice.lua
// (quest rework C3 T6). One section (the mission current): Southern San d'Oria [S]'s Raustigne plays the progress
// event 147 (the campaign allegiance, 16, 1), whose finish completes the mission (Proof of Valor current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class OnThinIce : MissionScript
    {
        public OnThinIce() : base(XI.Quests.MissionLog.WoTG, "ON_THIN_ICE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/20_On_Thin_Ice";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ProofOfValor,
            });

            b.State(current: true)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Raustigne)
                .OnTrigger(q => QuestReply.ProgressEvent(147, q.Player.CampaignAllegiance, 16, 1))
                .OnEventFinish(Zone.SouthernSanDoriaS, 147, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
