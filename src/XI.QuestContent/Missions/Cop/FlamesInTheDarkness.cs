// Flames in the Darkness (Promathia 7-2, Log ID 6, Mission ID 718), a line-for-line port of
// scripts/missions/cop/7_2_Flames_in_the_Darkness.lua (quest rework C3, C4). The mission current. Misareaux Coast's
// _0p2 plays the cutscene 12 at Status 0 (Status 1); Sealion's Den's Sueleen 16 at Status 1 (Status 2); Ru'Lude
// Gardens' trigger area 1 10051 at Status 2 (Status 3); Upper Jeuno's _6s1 10012 at Status 3, whose finish completes
// the mission. Fire in the Eyes of Men is next, with the Eshantarl's Comrade in Arms title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class FlamesInTheDarkness : MissionScript
    {
        public FlamesInTheDarkness() : base(XI.Quests.MissionLog.CoP, "FLAMES_IN_THE_DARKNESS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/7_2_Flames_in_the_Darkness";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.EshantarlsComradeInArms,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.FireInTheEyesOfMen,
            });

            b.State(current: true)
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p2).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressCutscene(12) : null)
                .OnEventFinish(Zone.MisareauxCoast, 12, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.SealionsDen, Npc.SealionsDen.Sueleen).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(16) : null)
                .OnEventFinish(Zone.SealionsDen, 16, q => q.Mission.SetVar("Status", 2))
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.ProgressEvent(10051) : null)
                .OnEventFinish(Zone.RuludeGardens, 10051, q => q.Mission.SetVar("Status", 3))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s1).OnTrigger(q =>
                    q.Mission.Var("Status") == 3 ? QuestReply.ProgressEvent(10012) : null)
                .OnEventFinish(Zone.UpperJeuno, 10012, q => q.Mission.Complete());
        }
    }
}
