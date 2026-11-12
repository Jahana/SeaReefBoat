// Tending Aged Wounds (Promathia 3-4, Log ID 6, Mission ID 350), a line-for-line port of
// scripts/missions/cop/3_4_Tending_Aged_Wounds.lua (quest rework C3). Cid !pos -12 -12 1 237; Door: Neptune's
// Spire !pos 35 0 -15 245. One section (the mission current): Metalworks Cid plays 862 once a zone whatever the
// status; Lower Jeuno's zone-in at Status 0 plays 70 (its finish sets Status 1); _6tc at Status 1 plays 22, whose
// finish completes the mission. Darkness Named is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TendingAgedWounds : MissionScript
    {
        public TendingAgedWounds() : base(XI.Quests.MissionLog.CoP, "TENDING_AGED_WOUNDS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/3_4_Tending_Aged_Wounds";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.DarknessNamed,
            });

            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.ProgressEvent(862).OncePerZone())
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(22) : null)
                .OnZoneIn(Zone.LowerJeuno, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(70) : null)
                .OnEventFinish(Zone.LowerJeuno, 22, q => q.Mission.Complete())
                .OnEventFinish(Zone.LowerJeuno, 70, q => q.Mission.SetVar("Status", 1));
        }
    }
}
