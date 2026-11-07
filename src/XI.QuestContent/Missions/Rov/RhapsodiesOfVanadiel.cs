// Rhapsodies of Vana'diel (Rhapsodies of Vana'diel Mission 1-1, Log ID 13, Mission ID 0), a line-for-line port of
// scripts/missions/rov/1_01_Rhapsodies_of_Vanadiel.lua (quest rework C2). The RoV log's current mission is this one by
// default (!addmission 13 0). Its one section (current, ENABLE_ROV 1, main level 3 or more, not in a Mog House): the
// zone-in of each of the ten nation city zones plays 30035, whose finish completes the mission (Resonance next).
// Kept as the Lua has it: the one rovZoneInEvent table of the ten zones is ten zone-ins and ten finishes here, each its
// zone's; ENABLE_ROV is compared with the number 1 (a boolean true fails); 30035's finish completes on any option and
// ignores mission:complete's answer.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Rov
{
    public sealed class RhapsodiesOfVanadiel : MissionScript
    {
        public RhapsodiesOfVanadiel() : base(XI.Quests.MissionLog.RoV, "RHAPSODIES_OF_VANADIEL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rov/1_01_Rhapsodies_of_Vanadiel";

        // the zones whose zone-in plays 30035, in the Lua's order
        static readonly ushort[] RovEntryZones =
        {
            Zone.BastokMarkets,
            Zone.BastokMines,
            Zone.NorthernSanDoria,
            Zone.PortBastok,
            Zone.PortSanDoria,
            Zone.PortWindurst,
            Zone.SouthernSanDoria,
            Zone.WindurstWalls,
            Zone.WindurstWaters,
            Zone.WindurstWoods,
        };

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.RoV,
                NextMission = Mission.Rov.Resonance,
            });

            var section = b.State(current: true, when: q =>
                q.Settings.MainEquals("ENABLE_ROV", 1) &&
                q.Player.MainLevel >= 3 &&
                !q.Player.InMogHouse);

            // rovZoneInEvent in each entry zone
            foreach (ushort zoneId in RovEntryZones)
            {
                section
                    .OnZoneIn(zoneId, q =>
                    {
                        return QuestReply.Event(30035);
                    })
                    .OnEventFinish(zoneId, 30035, q =>
                    {
                        q.Mission.Complete();
                    });
            }
        }
    }
}
