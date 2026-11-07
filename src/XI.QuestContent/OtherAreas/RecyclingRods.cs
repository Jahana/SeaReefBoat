// Recycling Rods (Log ID 4, Quest ID 30), a line-for-line port of scripts/quests/otherAreas/Recycling_Rods.lua (quest
// rework B3b, Other Areas group 4). Mhaura: Keshab-Menjab !pos -15.6 -8 52 249 (no script, no default).
// Available: Keshab-Menjab 313, whose finish with option 1 begins the quest. Accepted: 315 (Event); a trade of exactly a
// Clean Snap Rod: 317, any other trade: 316 (Event); 317's finish: quest:complete (1500 gil), then the trade confirmed.
// Completed: 314 (Event).
// Kept as the Lua has it: any trade while accepted answers (316 for a wrong one); the quest is not repeatable.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class RecyclingRods : QuestScript
    {
        public RecyclingRods() : base(QuestLog.OtherAreas, "RECYCLING_RODS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Recycling_Rods";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 1500,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.Mhaura, Npc.Mhaura.KeshabMenjab).OnTrigger(QuestReply.ProgressEvent(313))
                .OnEventFinish(Zone.Mhaura, 313, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.KeshabMenjab).OnTrigger(q =>
                {
                    return QuestReply.Event(315);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.CleanSnapRod))
                    {
                        return QuestReply.ProgressEvent(317);
                    }
                    else
                    {
                        return QuestReply.Event(316);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 317, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.KeshabMenjab).OnTrigger(QuestReply.Event(314));
        }
    }
}
