// Eternal Mercenary (ToAU 48, Log ID 4, Mission ID 47), a line-for-line port of
// scripts/missions/toau/48_Eternal_Mercenary.lua (quest rework C3 T4). The last mission: no reward. One section (the
// mission current): in Aht Urhgan Whitegate Naja Salaheem plays the plain event 3151 (the mercenary rank, and 1) in
// place of the default dialog.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class EternalMercenary : MissionScript
    {
        public EternalMercenary() : base(XI.Quests.MissionLog.ToAU, "ETERNAL_MERCENARY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/48_Eternal_Mercenary";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward());

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3151, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0).ReplaceDefault();
                });
        }
    }
}
