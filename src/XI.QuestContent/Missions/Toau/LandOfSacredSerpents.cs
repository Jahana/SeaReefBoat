// Land of Sacred Serpents (ToAU 1, Log ID 4, Mission ID 0), a line-for-line port of
// scripts/missions/toau/01_Land_of_Sacred_Serpents.lua (quest rework C3 T1). The mission is set on character creation.
// One section (the mission current, ENABLE_TOAU 1 and the Boarding Permit held, a guard the Lua adds): Aht Urhgan
// Whitegate's trigger area 3 plays 3000 (text table 0: Whitegate uses two dats), whose finish completes the mission
// (the Supplies Package, Immortal Sentries current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class LandOfSacredSerpents : MissionScript
    {
        public LandOfSacredSerpents() : base(XI.Quests.MissionLog.ToAU, "LAND_OF_SACRED_SERPENTS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/01_Land_of_Sacred_Serpents";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.SuppliesPackage,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.ImmortalSentries,
            });

            // NOTE (Lua): I don't know HOW would someone get to Whitegate without the Boarding Permit Key Item, but
            // it's probably for the best to add the additional check.
            b.State(current: true, when: q =>
                    q.Settings.MainEquals("ENABLE_TOAU", 1) &&
                    q.Player.HasKeyItem(KeyItem.BoardingPermit))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    // Naja Salaheem interactions require the 9th argument set to 0.
                    // This is because Aht Uhrgan Whitegate uses 2 different dats.
                    return QuestReply.ProgressEvent(3000).TextTable(0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3000, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
