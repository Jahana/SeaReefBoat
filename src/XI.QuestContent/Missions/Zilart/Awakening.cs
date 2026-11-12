// Awakening (Zilart 17, Log ID 3, Mission ID 30), a line-for-line port of scripts/missions/rotz/17_Awakening.lua
// (quest rework C3, C6). Gilgamesh !pos 122.452 -9.009 -12.052 252; Lower Jeuno !zone 245; Norg !zone 252.
// Section 1 (current): Norg's Gilgamesh plays 177. Section 2 (current; the status a bit field of the two once-only
// cutscenes): Lower Jeuno's _6tc (Door: Neptune's Spire) plays 20 while bit 1 is clear (else nothing), whose finish
// adds 2 to the status; Norg's zone-in plays 176 while bit 0 is clear, whose finish adds 1. The 232 and 234 finishes
// in Norg (Apocalypse Nigh's Gilgamesh events, Jeuno/ApocalypseNigh.cs) complete the mission (The Last Verse next; no
// key item, no title) when the option is 1 to 4 and the status is 3.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class Awakening : MissionScript
    {
        public Awakening() : base(XI.Quests.MissionLog.Zilart, "AWAKENING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/17_Awakening";

        // mission.areaId
        const int AreaId = MissionLog.Zilart;

        // utils.mask.getBit(mask, pos)
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        // the 232 and 234 finishes (the Lua's missionOnEventFinish)
        static void MissionOnEventFinish(IQuestContext q)
        {
            if (q.Option >= 1 &&
                q.Option <= 4 &&
                q.Player.MissionStatus(AreaId) == 3)
            {
                q.Mission.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheLastVerse,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(177));

            // Optional cutscenes that should be displayed only once onZone. In this case, missionStatus is handled as
            // a bitfield.
            b.State(current: true)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).OnTrigger(q =>
                {
                    if (!GetBit(q.Player.MissionStatus(AreaId), 1))
                    {
                        return QuestReply.Event(20);
                    }

                    return null;
                })
                // Set bit 1 of missionStatus
                .OnEventFinish(Zone.LowerJeuno, 20, q =>
                    q.Player.SetMissionStatus(AreaId, q.Player.MissionStatus(AreaId) + 2))
                .OnZoneIn(Zone.Norg, q =>
                {
                    if (!GetBit(q.Player.MissionStatus(AreaId), 0))
                    {
                        return QuestReply.Event(176);
                    }

                    return null;
                })
                // Set bit 0 of missionStatus
                .OnEventFinish(Zone.Norg, 176, q =>
                    q.Player.SetMissionStatus(AreaId, q.Player.MissionStatus(AreaId) + 1))
                .OnEventFinish(Zone.Norg, 232, MissionOnEventFinish)
                .OnEventFinish(Zone.Norg, 234, MissionOnEventFinish);
        }
    }
}
