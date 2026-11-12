// Undersea Scouting (ToAU 9, Log ID 4, Mission ID 8), a line-for-line port of
// scripts/missions/toau/09_Undersea_Scouting.lua (quest rework C3 T1). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current). Aht Urhgan Whitegate: Naja plays 3051 (a plain event; the mercenary rank, and the
// dialog
// as its eighth operand), the mission Option var cycling 1, 0, 1, 0. Alzadaal Undersea Ruins: trigger area 23 plays 1
// (the mercenary rank and a zero third operand), whose finish completes the mission (Astral Waves current, the Astral
// Compass).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class UnderseaScouting : MissionScript
    {
        public UnderseaScouting() : base(XI.Quests.MissionLog.ToAU, "UNDERSEA_SCOUTING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/09_Undersea_Scouting";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.AstralCompass,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.AstralWaves,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    // Order goes 1 -> 0 -> 1 -> 0 ...
                    int dialog = q.Mission.Var("Option") == 0 ? 1 : 0;
                    q.Mission.SetVar("Option", dialog);

                    return QuestReply.Event(3051, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                })
                .OnTriggerAreaEnter(Zone.AlzadaalUnderseaRuins, 23, q =>
                {
                    int param3 = 0; // Mentions, "Hey, it's you!" Maybe tied to PUP?

                    return QuestReply.ProgressEvent(1, q.Player.MercenaryRank, param3);
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 1, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
