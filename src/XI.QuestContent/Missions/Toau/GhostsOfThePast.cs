// Ghosts of the Past (ToAU 16, Log ID 4, Mission ID 15), a line-for-line port of
// scripts/missions/toau/16_Ghosts_of_the_Past.lua (quest rework C3 T2). Naja Salaheem !pos 22.700 -8.804 -45.591 50.
// One section (the mission current): Naja plays the progress event 3074 with the mercenary rank, and the eighth
// operand 1 when the Royal Palace armor check passes (Aht_Urhgan_Whitegate/Shared's doRoyalPalaceArmorCheck, the facet
// RoyalPalaceArmorCheck), else 0. The dress-up updates of 3074 are NajaDressUp's. The finish completes the mission
// (Guests of the Empire current), and with option 2 sets the next mission's status 1.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class GhostsOfThePast : MissionScript
    {
        public GhostsOfThePast() : base(XI.Quests.MissionLog.ToAU, "GHOSTS_OF_THE_PAST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/16_Ghosts_of_the_Past";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.GuestsOfTheEmpire,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    // Controls how bad Naja judges you.
                    int properlyDressed = q.Player.RoyalPalaceArmorCheck() ? 1 : 0;

                    return QuestReply.ProgressEvent(3074, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, properlyDressed, 0);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3074, NajaDressUp.Update)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3074, q =>
                {
                    q.Mission.Complete();
                    if (q.Option == 2)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                });
        }
    }
}
