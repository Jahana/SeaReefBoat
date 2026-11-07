// Stardust (Log ID 1, Quest ID 24), a line-for-line port of scripts/quests/bastok/Stardust.lua (quest rework B3b). Baldric
// : !pos -50.858 1.777 -31.141 237; qm1 : !pos 238.524 2.661 -148.784 103. Baldric's 554 (Bastok fame level 2) begins it;
// a Pinch of Valkurm Sunsand traded plays 555, which completes it (300 gil, fame +10), takes the sunsand and sets the
// mustZone. Drangord plays 97 while it is accepted. Repeatable: once completed, Baldric's 554 (Prog 0, the mustZone
// clear) sets Prog 1, and the sunsand at Prog 1 plays 555 again, which completes it again unless the mustZone is set.
// Kept as the Lua file has it: the completion clears the quest's vars (quest:complete's cleanup) and then sets the
// mustZone again; 555's finish with the mustZone set (a GM Prog) does nothing and keeps the sunsand.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Stardust : QuestScript
    {
        public Stardust() : base(QuestLog.Bastok, "STARDUST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Stardust";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 300 });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.Metalworks, Npc.Metalworks.Baldric).OnTrigger(QuestReply.ProgressEvent(554))
                .OnEventFinish(Zone.Metalworks, 554, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Baldric).OnTrade(q =>
                    q.Trade.Matches((Item.PinchOfValkurmSunsand, 1)) ? QuestReply.ProgressEvent(555) : null)
                .OnEventFinish(Zone.Metalworks, 555, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Drangord).OnTrigger(QuestReply.Event(97));

            b.State(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.Baldric)
                    .OnTrade(q =>
                        q.Quest.Step == 1 && q.Trade.Matches((Item.PinchOfValkurmSunsand, 1))
                            ? QuestReply.ProgressEvent(555)
                            : null)
                    .OnTrigger(q =>
                        q.Quest.Step == 0 && !q.Quest.MustZone ? QuestReply.ProgressEvent(554) : null)
                .OnEventFinish(Zone.Metalworks, 554, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.Metalworks, 555, q =>
                {
                    if (!q.Quest.MustZone && q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Quest.SetMustZone();
                    }
                });
        }
    }
}
