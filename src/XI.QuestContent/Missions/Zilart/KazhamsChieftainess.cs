// Kazham's Chieftainess (Zilart 3, Log ID 3, Mission ID 6), a line-for-line port of
// scripts/missions/rotz/03_Kazhams_Chieftainess.lua (quest rework C3, C5). Gilgamesh !pos 122.452 -9.009 -12.052 252;
// Jakoh Wahcondalo !pos 101 -16 -115 250. Section 1 (current): Norg's Gilgamesh plays 7; Kazham's Jakoh Wahcondalo
// plays 114, whose finish completes the mission (the Sacrificial Chamber Key, The Temple of Uggalepih next).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class KazhamsChieftainess : MissionScript
    {
        public KazhamsChieftainess() : base(XI.Quests.MissionLog.Zilart, "KAZHAMS_CHIEFTAINESS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/03_Kazhams_Chieftainess";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.SacrificialChamberKey,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheTempleOfUggalepih,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(7))
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(QuestReply.ProgressEvent(114))
                .OnEventFinish(Zone.Kazham, 114, q => q.Mission.Complete());
        }
    }
}
