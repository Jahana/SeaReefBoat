// Fear of the Dark II (Log ID 6, Quest ID 14), a line-for-line port of
// scripts/quests/ahtUrhgan/Fear_of_the_Dark_2.lua (quest rework B3b Aht Urhgan, draft). Al Zahbi: Suldiran !pos
// 41.7 -6 -42.5 (no script, no default). Available (no other condition): Suldiran 14, option 1: begun. Accepted:
// Suldiran 15 (an Event); a trade of exactly two Imp Wings: 16, whose finish completes it (200 gil, Dark Resistant)
// and, when it does, takes the wings. Completed: Suldiran 17 (an Event); a trade of exactly two Imp Wings: 18,
// whose finish takes them and gives 200 gil. Kept as the Lua has it: 18 confirms the trade before the gil; any
// other trade returns nil (no default: nothing).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class FearOfTheDarkIi : QuestScript
    {
        public FearOfTheDarkIi() : base(QuestLog.AhtUrghan, "FEAR_OF_THE_DARK_II") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Fear_of_the_Dark_2";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 200,
                Title = Title.DarkResistant,
            });

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Suldiran).OnTrigger(q => QuestReply.ProgressEvent(14))
                .OnEventFinish(Zone.AlZahbi, 14, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Suldiran).OnTrigger(q => QuestReply.Event(15))
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.ImpWing, 2)))
                    {
                        return QuestReply.ProgressEvent(16);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AlZahbi, 16, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Suldiran).OnTrigger(q => QuestReply.Event(17))
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.ImpWing, 2)))
                    {
                        return QuestReply.ProgressEvent(18);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AlZahbi, 18, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveCurrency("gil", 200);
                });
        }
    }
}
