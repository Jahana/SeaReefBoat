// Astral Waves (ToAU 10, Log ID 4, Mission ID 9), a line-for-line port of
// scripts/missions/toau/10_Astral_Waves.lua (quest rework C3 T1). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current), in Aht Urhgan Whitegate: Naja plays 3052 (mercenary rank first, 1 second); its update
// answers option 1 with the mercenary rank and 1; its finish completes the mission (Imperial Schemes current) and, only
// then, sets Imperial Schemes' Timer to the Vana'diel day after today and its mustZone.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class AstralWaves : MissionScript
    {
        public AstralWaves() : base(XI.Quests.MissionLog.ToAU, "ASTRAL_WAVES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/10_Astral_Waves";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.ImperialSchemes,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                    QuestReply.ProgressEvent(3052, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0))
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3052, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3052, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetMissionVar(AreaId, Mission.Toau.ImperialSchemes, "Timer",
                            q.Player.VanadielUniqueDay + 1);
                        q.Player.SetMissionMustZone(AreaId, Mission.Toau.ImperialSchemes);
                    }
                });
        }
    }
}
