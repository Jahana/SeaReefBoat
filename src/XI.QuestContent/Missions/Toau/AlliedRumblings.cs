// Allied Rumblings (ToAU 39, Log ID 4, Mission ID 38), a line-for-line port of
// scripts/missions/toau/39_Allied_Rumblings.lua (quest rework C3 T4). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3148 (the mercenary rank, and 1). Ru'Lude Gardens: trigger area 1
// plays the progress event 10097, whose finish completes the mission (Unraveling Reason current) and, when it did, sets
// Unraveling Reason's mustZone as a LOCAL var and its Timer char var to the next Vana'diel day. Kept as the Lua has it:
// the mustZone is written with player:setLocalVar, which getMustZone (a char var) never reads, so only the Timer delays
// the next mission.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class AlliedRumblings : MissionScript
    {
        public AlliedRumblings() : base(XI.Quests.MissionLog.ToAU, "ALLIED_RUMBLINGS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/39_Allied_Rumblings";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.UnravelingReason,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3148, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    return QuestReply.ProgressEvent(10097, 0, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.RuludeGardens, 10097, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetLocalVar("Mission[4][39]mustZone", 1);
                        q.Player.SetCharVar("Mission[4][39]Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });
        }
    }
}
