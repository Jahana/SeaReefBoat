// Fallen Comrades (Log ID 1, Quest ID 19), a line-for-line port of scripts/quests/bastok/Fallen_Comrades.lua (quest rework
// B3b). Pavvke : !pos 16.586 6.985 -14.843 234. Mission 7-2 reads its status and keeps the read. Kept as the Lua file has
// it: the second section matches accepted and completed, so each name tag traded after completion completes the quest
// again (92), with 10 more Bastok fame.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class FallenComrades : QuestScript
    {
        public FallenComrades() : base(QuestLog.Bastok, "FALLEN_COMRADES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Fallen_Comrades";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 550 });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.BastokMines, Npc.BastokMines.Pavvke).OnTrigger(QuestReply.ProgressEvent(90))
                .OnEventFinish(Zone.BastokMines, 90, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Pavvke).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.SilverNameTag, 1)))
                    {
                        return q.Player.HasCompletedQuest(QuestLog.Bastok, "FALLEN_COMRADES")
                            ? QuestReply.ProgressEvent(92)
                            : QuestReply.ProgressEvent(91);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 91, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .OnEventFinish(Zone.BastokMines, 92, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.AddFame(FameArea.Bastok, 10);
                    }
                });
        }
    }
}
