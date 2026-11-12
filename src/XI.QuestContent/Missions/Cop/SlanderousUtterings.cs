// Slanderous Utterings (Promathia 4-4, Log ID 6, Mission ID 438), a line-for-line port of
// scripts/missions/cop/4_4_Slanderous_Utterings.lua (quest rework C3). Despachiaire !pos 108 -40 -83 26. The
// mission current: Despachiaire plays 132 as an important event; trigger area 2 of Tavnazian Safehold plays 112 at
// Status 0 (its finish sets Status 1); the Sealion's Den door plays 13 at Status 1 (its finish completes the mission).
// 4-4 completed: Arquil plays 293 and Despachiaire plays 317, both replacing the default. The Lost One title is the
// reward; The Enduring Tumult of War is next.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class SlanderousUtterings : MissionScript
    {
        public SlanderousUtterings() : base(XI.Quests.MissionLog.CoP, "SLANDEROUS_UTTERINGS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/4_4_Slanderous_Utterings";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.TheLostOne,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheEnduringTumultOfWar,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire)
                    .OnTrigger(QuestReply.Event(132).ImportantEvent())
                .OnTriggerAreaEnter(Zone.TavnazianSafehold, 2, q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(112) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 112, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.SealionsDen, Npc.SealionsDen._0w0).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(13) : null)
                .OnEventFinish(Zone.SealionsDen, 13, q => q.Mission.Complete());

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.SlanderousUtterings))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Arquil)
                    .OnTrigger(QuestReply.Event(293).ReplaceDefault())
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire)
                    .OnTrigger(QuestReply.Event(317).ReplaceDefault());
        }
    }
}
