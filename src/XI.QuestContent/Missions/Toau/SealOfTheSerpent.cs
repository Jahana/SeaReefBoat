// Seal of the Serpent (ToAU 26, Log ID 4, Mission ID 25), a line-for-line port of
// scripts/missions/toau/26_Seal_of_the_Serpent.lua (quest rework C3 T3). One section (the mission current), all in Aht
// Urhgan Whitegate. Naja Salaheem plays the plain event 3115 (the mercenary rank and 1). The Imperial Whitegate, with
// nothing in the main and sub hands (equip ids 0 and 0), plays the progress event 3111 (a 1 at the second and third
// operands, 128 at the eighth); with either hand holding something, the plain event 3114 (a 1 at the seventh). 3111's
// finish completes the mission (Misplaced Nobility current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class SealOfTheSerpent : MissionScript
    {
        public SealOfTheSerpent() : base(XI.Quests.MissionLog.ToAU, "SEAL_OF_THE_SERPENT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/26_Seal_of_the_Serpent";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.MisplacedNobility,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3115, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    if (q.Player.EquipId(Slot.Main) == 0 && q.Player.EquipId(Slot.Sub) == 0)
                    {
                        return QuestReply.ProgressEvent(3111, 0, 1, 1, 0, 0, 0, 0, 128);
                    }

                    return QuestReply.Event(3114, 0, 1, 0, 0, 0, 0, 1, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3111, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
