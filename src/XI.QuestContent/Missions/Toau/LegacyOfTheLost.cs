// Legacy of the Lost (ToAU 35, Log ID 4, Mission ID 34), a line-for-line port of
// scripts/missions/toau/35_Legacy_of_the_Lost.lua (quest rework C3 T3). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3133 (the mercenary rank and 1). Talacca Cove: the battlefield's win
// finish 32001 (Battlefields/TalaccaCove/LegacyOfTheLost.cs) completes the mission (Gaze of the Saboteur current, the
// Gessho's Mercy title) only when the char's battlefieldWin local var is this battlefield's id.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class LegacyOfTheLost : MissionScript
    {
        public LegacyOfTheLost() : base(XI.Quests.MissionLog.ToAU, "LEGACY_OF_THE_LOST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/35_Legacy_of_the_Lost";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.GesshosMercy,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.GazeOfTheSaboteur,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3133, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.TalaccaCove, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.LegacyOfTheLost)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
