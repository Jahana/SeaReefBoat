// Teahouse Tumult (ToAU 20, Log ID 4, Mission ID 19), a line-for-line port of
// scripts/missions/toau/20_Teahouse_Tumult.lua (quest rework C3 T2). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3091 (the mercenary rank, and 1). Aydeewa Subterrane: the zone-in at
// status 0 plays 10, whose finish sets status 1; the blank_toau20 NPC at status 1 plays the progress event 11 (eight
// zeros), whose finish completes the mission (Finders Keepers current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class TeahouseTumult : MissionScript
    {
        public TeahouseTumult() : base(XI.Quests.MissionLog.ToAU, "TEAHOUSE_TUMULT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/20_Teahouse_Tumult";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.FindersKeepers,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3091, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.blanktoau20).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(11, 0, 0, 0, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnZoneIn(Zone.AydeewaSubterrane, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(10);
                    }

                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 10, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 11, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
