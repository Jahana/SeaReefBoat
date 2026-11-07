// Atelloune's Lament (Log ID 0, Quest ID 114), a line-for-line port of scripts/quests/sandoria/Atellounes_Lament.lua
// (quest rework B3b, San d'Oria group 9). Atelloune : !pos 122 0 82 230. Offered at San d'Oria fame level 2 once the
// Crystal War quest Seeing Spots is completed: Atelloune's 890 (begun on any option). Accepted: her 892; exactly one
// Ladybug Wing plays 891 (completed: Trainee Gloves, fame +20; the wing taken). No outside reference.
// Kept as the Lua file has it: 890 begins it on any option, a cancel too; 891's finish takes the wing only when complete
// succeeds (with no room for the gloves, nothing happens and 891 can be played again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class AtellounesLament : QuestScript
    {
        public AtellounesLament() : base(QuestLog.Sandoria, "ATELLOUNES_LAMENT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Atellounes_Lament";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.TraineeGloves });

            // Speak to Atelloune in Southern San d'Oria at (L-6) for a cutscene to start the quest.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Sandoria) >= 2 &&
                    q.Player.QuestStatus(QuestLog.CrystalWar, "SEEING_SPOTS") == QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Atelloune).OnTrigger(QuestReply.ProgressEvent(890))
                .OnEventFinish(Zone.SouthernSanDoria, 890, q =>
                {
                    q.Quest.Begin();
                });

            // Trade her a Ladybug Wing for a cutscene and to receive your reward.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Atelloune)
                    .OnTrigger(QuestReply.ProgressEvent(892))
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.LadybugWing))
                        {
                            return QuestReply.ProgressEvent(891);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 891, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
