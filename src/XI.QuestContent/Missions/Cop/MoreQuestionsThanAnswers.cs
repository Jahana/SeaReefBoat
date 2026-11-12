// More Questions than Answers (Promathia 6-3, Log ID 6), a line-for-line port of
// scripts/missions/cop/6_3_More_Questions_than_Answers.lua (quest rework C3). The mission current. Ru'Lud Gardens: the
// _6r9 plays 10050 at Status 1 (Status 2); Pherimociel plays 10049 at Status 0 (Status 1), 10053 at Status 1 and 10063
// at Status 2; Auchefort plays 10054 at Status 2 and 9 otherwise; Adolie, Chapi Galepilai, Colti and Neraf-Najiruf
// (once per zone) play 158, 12, 22 and 31 whatever the status. Selbina's Mathilde plays 10005 at Status 2 and its
// finish completes the mission. One to be Feared is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class MoreQuestionsThanAnswers : MissionScript
    {
        public MoreQuestionsThanAnswers() : base(XI.Quests.MissionLog.CoP, "MORE_QUESTIONS_THAN_ANSWERS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/6_3_More_Questions_than_Answers";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.OneToBeFeared,
            });

            b.State(current: true)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r9).OnTrigger(q =>
                    q.Mission.Var("Status") == 1 ? QuestReply.ProgressEvent(10050) : null)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Pherimociel).OnTrigger(q =>
                {
                    switch (q.Mission.Var("Status"))
                    {
                        case 0:
                            return QuestReply.ProgressEvent(10049);
                        case 1:
                            return QuestReply.Event(10053);
                        case 2:
                            return QuestReply.Event(10063);
                        default:
                            return null;
                    }
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Auchefort).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.Event(10054) : QuestReply.Event(9))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Adolie).OnTrigger(QuestReply.Event(158))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.ChapiGalepilai).OnTrigger(QuestReply.Event(12))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Colti).OnTrigger(QuestReply.Event(22))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NerafNajiruf).OnTrigger(QuestReply.Event(31).OncePerZone())
                .OnEventFinish(Zone.RuludeGardens, 10049, q => q.Mission.SetVar("Status", 1))
                .OnEventFinish(Zone.RuludeGardens, 10050, q => q.Mission.SetVar("Status", 2))
                .Npc(Zone.Selbina, Npc.Selbina.Mathilde).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.ProgressEvent(10005) : null)
                .OnEventFinish(Zone.Selbina, 10005, q => q.Mission.Complete());
        }
    }
}
