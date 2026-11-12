// The Sealed Shrine (Zilart 15, Log ID 3, Mission ID 27), a line-for-line port of
// scripts/missions/rotz/15_The_Sealed_Shrine.lua (quest rework C3, C6). _700 (Oaken Door) !pos 97 -7 -12 252; Aldo
// !pos 20 3 -58 245; Ru'Avitau Main Entrance !pos -0.2171 -45.013 -119.7575. Section 1 (current): Norg's _700 plays
// 172 at status 0 (else nothing), whose finish on option 0 sets status 1; Gilgamesh plays 173. Lower Jeuno's Aldo
// plays 111 (1, 1, 11, 0, 65339390, 899376, 4095, 0) while the Option var is 0, whose finish sets it to 1, else 69 (0,
// 1, 5, 0, 0, 280, 4095, 4). The Shrine of Ru'Avitau's zone-in plays 51 at status 1, whose finish completes the
// mission (The Celestial Nexus next; no key item, no title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheSealedShrine : MissionScript
    {
        public TheSealedShrine() : base(XI.Quests.MissionLog.Zilart, "THE_SEALED_SHRINE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/15_The_Sealed_Shrine";

        // mission.areaId
        const int AreaId = MissionLog.Zilart;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheCelestialNexus,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(172);
                    }

                    return null;
                })
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(173))
                .OnEventFinish(Zone.Norg, 172, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(111, 1, 1, 11, 0, 65339390, 899376, 4095, 0);
                    }
                    else
                    {
                        return QuestReply.Event(69, 0, 1, 5, 0, 0, 280, 4095, 4);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 111, q => q.Mission.SetVar("Option", 1))
                .OnZoneIn(Zone.TheShrineOfRuavitau, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.Event(51);
                    }

                    return null;
                })
                .OnEventFinish(Zone.TheShrineOfRuavitau, 51, q => q.Mission.Complete());
        }
    }
}
