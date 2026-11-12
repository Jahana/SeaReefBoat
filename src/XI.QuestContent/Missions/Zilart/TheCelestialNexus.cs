// The Celestial Nexus (Zilart 16, Log ID 3, Mission ID 28), a line-for-line port of
// scripts/missions/rotz/16_The_Celestial_Nexus.lua (quest rework C3, C6). Gilgamesh !pos 122.452 -9.009 -12.052 252;
// _515 (BCNM Entry) !pos -665.2291 -5.8232 -32.4834 181. Section 1 (current): Norg's Gilgamesh plays 173. The
// Celestial Nexus' 32001 finish completes the mission (the Burier of the Illusion title, Awakening next) when
// battlefieldWin is the Celestial Nexus fight's (Battlefields/TheCelestialNexus/CelestialNexus.cs sets it), and in any
// case sets the char down at (0.003, -18.897, 137.112, 64) in the Hall of the Gods.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheCelestialNexus : MissionScript
    {
        public TheCelestialNexus() : base(XI.Quests.MissionLog.Zilart, "THE_CELESTIAL_NEXUS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/16_The_Celestial_Nexus";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.BurierOfTheIllusion,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.Awakening,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(173))
                .OnEventFinish(Zone.TheCelestialNexus, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.CelestialNexus)
                    {
                        q.Mission.Complete();
                    }

                    q.Player.SetPos(0.003, -18.897, 137.112, 64, Zone.HallOfTheGods);
                });
        }
    }
}
