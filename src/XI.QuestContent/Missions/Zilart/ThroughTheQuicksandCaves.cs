// Through the Quicksand Caves (Zilart 6, Log ID 3, Mission ID 12), a line-for-line port of
// scripts/missions/rotz/06_Through_the_Quicksand_Caves.lua (quest rework C3, C5). Shimmering Circle !pos -220 0 12
// 168. Section 1 (current): Norg's Gilgamesh plays 12; the Chamber of Oracles 32001 finish of a Through the Quicksand
// Caves win (Battlefields/ChamberOfOracles/ThroughTheQuicksandCaves.cs sets battlefieldWin) completes the mission (The
// Chamber of Oracles next; no key item, no title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class ThroughTheQuicksandCaves : MissionScript
    {
        public ThroughTheQuicksandCaves() : base(XI.Quests.MissionLog.Zilart, "THROUGH_THE_QUICKSAND_CAVES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/06_Through_the_Quicksand_Caves";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheChamberOfOracles,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(12))
                .OnEventFinish(Zone.ChamberOfOracles, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.ThroughTheQuicksandCaves)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
