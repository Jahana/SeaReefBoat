// Misplaced Nobility (ToAU 27, Log ID 4, Mission ID 26), a line-for-line port of
// scripts/missions/toau/27_Misplaced_Nobility.lua (quest rework C3 T3). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3116 (the mercenary rank and 1). Aydeewa Subterrane: the NPC
// blank_toau20 plays the progress event 12 (a 4 at the second operand), whose finish completes the mission (Bastion
// of Knowledge current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class MisplacedNobility : MissionScript
    {
        public MisplacedNobility() : base(XI.Quests.MissionLog.ToAU, "MISPLACED_NOBILITY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/27_Misplaced_Nobility";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.BastionOfKnowledge,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3116, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.blanktoau20).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(12, 0, 4, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 12, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
