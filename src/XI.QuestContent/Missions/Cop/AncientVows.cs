// Ancient Vows (Promathia 2-5, Log ID 6, Mission ID 248), a line-for-line port of
// scripts/missions/cop/2_5_Ancient_Vows.lua (quest rework C3). Dilapidated Gate !pos -259 -30 276 25. One section (the
// mission current): Tavnazian Safehold's Justinius plays 128 replacing the default; Misareaux Coast's _0p2 plays 6 at
// Status 0 (its finish sets Status 1); the Riverne - Site #A01 zone-in at Status 1 plays 100 (its finish sets Status
// 2); Monarch Linn's 32001 finish completes the mission at Status 2 when the win was the Ancient Vows battlefield, and
// sets the char down in South Gustaberg (Battlefields/MonarchLinn/AncientVows.cs sets battlefieldWin). The Call of the
// Wyrmking is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class AncientVows : MissionScript
    {
        public AncientVows() : base(XI.Quests.MissionLog.CoP, "ANCIENT_VOWS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/2_5_Ancient_Vows";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheCallOfTheWyrmking,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius)
                    .OnTrigger(QuestReply.Event(128).ReplaceDefault())
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p2).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(6) : null)
                .OnEventFinish(Zone.MisareauxCoast, 6, q => q.Mission.SetVar("Status", 1))
                .OnZoneIn(Zone.RiverneSiteA01, q => q.Mission.Var("Status") == 1 ? QuestReply.Event(100) : null)
                .OnEventFinish(Zone.RiverneSiteA01, 100, q => q.Mission.SetVar("Status", 2))
                .OnEventFinish(Zone.MonarchLinn, 32001, q =>
                {
                    if (q.Mission.Var("Status") == 2 && q.Player.LocalVar("battlefieldWin") == Battlefield.AncientVows)
                    {
                        q.Mission.Complete();
                        // South Gustaberg
                        q.Player.SetPos(694, -5.5, -619, 74, 107);
                    }
                });
        }
    }
}
