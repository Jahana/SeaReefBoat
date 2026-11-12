// The Temple of Desolation (Zilart 10, Log ID 3, Mission ID 20), a line-for-line port of
// scripts/missions/rotz/10_The_Temple_of_Desolation.lua (quest rework C3, C6). Kamui !pos 120.121 -8.009 -7.298 252;
// _6z0 !pos 0 -12 48 251. Section 1 (current): Lower Jeuno's Aldo plays 24; Norg's _700 plays 5 (replacing its
// default), Gilgamesh 10 and Kamui 11; the Hall of the Gods' _6z0 (Cermet Gate) plays 1, whose finish completes the
// mission (the Sealer of the Portal of the Gods title, The Hall of the Gods next). Kept as the Lua has it: the 1's
// finish answers whoever started 1 while the mission is current (_6z0's own script also plays 1 without the Cerulean
// Crystal, and its comment leaves the finish to this mission).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheTempleOfDesolation : MissionScript
    {
        public TheTempleOfDesolation() : base(XI.Quests.MissionLog.Zilart, "THE_TEMPLE_OF_DESOLATION") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/10_The_Temple_of_Desolation";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.SealerOfThePortalOfTheGods,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheHallOfTheGods,
            });

            b.State(current: true)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(QuestReply.Event(24))
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(QuestReply.Event(5).ReplaceDefault())
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(10))
                .Npc(Zone.Norg, Npc.Norg.Kamui).OnTrigger(QuestReply.Event(11))
                .Npc(Zone.HallOfTheGods, Npc.HallOfTheGods._6z0).OnTrigger(QuestReply.ProgressEvent(1))
                .OnEventFinish(Zone.HallOfTheGods, 1, q => q.Mission.Complete());
        }
    }
}
