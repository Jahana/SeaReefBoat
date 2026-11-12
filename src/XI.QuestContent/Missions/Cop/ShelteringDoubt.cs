// Sheltering Doubt (Promathia 4-1, Log ID 6, Mission ID 368), a line-for-line port of
// scripts/missions/cop/4_1_Sheltering_Doubt.lua (quest rework C3). Despachiaire !pos 108 -40 -83 26; Justinius !pos 76
// -34 68 26; Dilapidated Gate (_0p0) !pos 260 9 -435 25. Section 1 (the mission current): entering Tavnazian Safehold
// at Status 0 plays 107 (its finish sets Status 1); Despachiaire plays 108 at Status 1 (finish: Status 2); Justinius
// plays 109 as an important event at Status 2 (finish: Status 3) and 266 replacing the default at Status 3; Misareaux
// Coast's _0p0 plays cutscene 7 from Status 2 on (its finish completes the mission). Section 2 (4-1 completed):
// Justinius plays 266 replacing the default. In both sections the earlier CoP missions' Justinius handlers answer
// first in the lookup order, so the recorded traces show their events and the 266 arms are pinned by fake checks.
// The Savage is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class ShelteringDoubt : MissionScript
    {
        public ShelteringDoubt() : base(XI.Quests.MissionLog.CoP, "SHELTERING_DOUBT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/4_1_Sheltering_Doubt";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheSavage,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(108) : null)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.Event(109).ImportantEvent()
                    : q.Mission.Var("Status") == 3 ? QuestReply.Event(266).ReplaceDefault()
                    : null)
                .OnZoneIn(Zone.TavnazianSafehold, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(107) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 107, q => q.Mission.SetVar("Status", 1))
                .OnEventFinish(Zone.TavnazianSafehold, 108, q => q.Mission.SetVar("Status", 2))
                .OnEventFinish(Zone.TavnazianSafehold, 109, q => q.Mission.SetVar("Status", 3))
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p0).OnTrigger(q =>
                    q.Mission.Var("Status") >= 2 ? QuestReply.ProgressCutscene(7) : null)
                .OnEventFinish(Zone.MisareauxCoast, 7, q => q.Mission.Complete());

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.ShelteringDoubt))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius)
                    .OnTrigger(QuestReply.Event(266).ReplaceDefault());
        }
    }
}
