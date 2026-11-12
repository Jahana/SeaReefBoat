// Stirrings of War (ToAU 38, Log ID 4, Mission ID 37), a line-for-line port of
// scripts/missions/toau/38_Stirrings_of_War.lua (quest rework C3 T3). One section, all in Aht Urhgan Whitegate. Its
// check is the mission current with the mustZone clear and VanadielUniqueDay at or past the Timer var (Path of
// Blood's finish sets the Timer): until both hold the mission plays nothing. Naja Salaheem plays the plain event 3134
// (the mercenary rank and 1). Trigger area 5 plays the progress event 3136 (text table 0), whose finish completes the
// mission (Allied Rumblings current, the Allied Council Summons key item). The ROV era module
// (Modules/Era/Missions/MissionAdjustments.cs) replaces the check.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class StirringsOfWar : MissionScript
    {
        public StirringsOfWar() : base(XI.Quests.MissionLog.ToAU, "STIRRINGS_OF_WAR") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/38_Stirrings_of_War";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.AlliedCouncilSummons,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.AlliedRumblings,
            });

            b.State(current: true, when: q =>
                !q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3134, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 5, q =>
                {
                    return QuestReply.ProgressEvent(3136).TextTable(0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3136, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
