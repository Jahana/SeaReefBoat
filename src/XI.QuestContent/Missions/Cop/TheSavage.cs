// The Savage (Promathia 4-2, Log ID 6, Mission ID 418), a line-for-line port of
// scripts/missions/cop/4_2_The_Savage.lua (quest rework C3). Dilapidated Gate (_0p2) !pos -259 -30 276 25; Justinius
// !pos 76 -34 68 26. The mission current: Misareaux Coast's _0p2 plays cutscene 8 at Status 0 (its finish with option 1
// sets Status 1 and sets the char down in Riverne - Site #B01, any other option does nothing); the Monarch Linn fight
// is battlefield.savage (Battlefields/MonarchLinn/Savage.cs sets battlefieldWin), whose 32001 finish sets Status 2 only
// when battlefieldWin is the Savage's id and the status is 1; Tavnazian Safehold's Justinius plays 110 at Status 2
// (its finish completes the mission) and else 130 as an important event. The Nagmolada's Underling title is the
// reward; The Secrets of Worship is next.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheSavage : MissionScript
    {
        public TheSavage() : base(XI.Quests.MissionLog.CoP, "THE_SAVAGE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/4_2_The_Savage";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.NagmoladasUnderling,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheSecretsOfWorship,
            });

            b.State(current: true)
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p2).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressCutscene(8) : null)
                .OnEventFinish(Zone.MisareauxCoast, 8, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Mission.SetVar("Status", 1);
                        // Riverne - Site #B01
                        q.Player.SetPos(729.749, -20.325, 407.153, 90, 29);
                    }
                })
                .OnEventFinish(Zone.MonarchLinn, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.Savage && q.Mission.Var("Status") == 1)
                    {
                        q.Mission.SetVar("Status", 2);
                    }
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                    q.Mission.Var("Status") == 2
                        ? QuestReply.ProgressEvent(110)
                        : QuestReply.Event(130).ImportantEvent())
                .OnEventFinish(Zone.TavnazianSafehold, 110, q => q.Mission.Complete());
        }
    }
}
