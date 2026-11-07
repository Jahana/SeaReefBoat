// Fear of the Dark III (Log ID 8, Quest ID 2), a line-for-line port of scripts/quests/abyssea/Fear_of_the_Dark_III.lua
// (quest rework B3b Abyssea). Abyssea - La Theine: Secodiand !pos -489.5 -2.8 759.2 132 (no script, no default).
// Available: Secodiand: 161 (an Event), whose finish begins it on any option but 0. Not available (accepted or
// completed): Secodiand: 159 (an Event); a trade of three Clionid Wings (at least, other items allowed): 160 (an
// Event), whose finish confirms the trade, gives 200 cruor (CRUOR_TOTAL with the new total) and completes it (10
// Abyssea - La Theine fame). Kept as the Lua has it: the second section matches a completed quest too, so the wings can
// be turned in again after the completion (160 again: the cruor, and quest:complete on a completed quest, which gives
// the fame again); no trade handler while available.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class FearOfTheDarkIii : QuestScript
    {
        public FearOfTheDarkIii() : base(QuestLog.Abyssea, "FEAR_OF_THE_DARK_III") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Fear_of_the_Dark_III";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 10,
                FameArea = FameArea.AbysseaLatheine,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Secodiand).OnTrigger(q =>
                {
                    return QuestReply.Event(161);
                })
                .OnEventFinish(Zone.AbysseaLaTheine, 161, q =>
                {
                    if (q.Option != 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // status ~= QUEST_AVAILABLE: accepted or completed
            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Secodiand).OnTrigger(q =>
                {
                    return QuestReply.Event(159);
                })
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Secodiand).OnTrade(q =>
                {
                    if (q.Trade.HasQty((Item.ClionidWing, 3)))
                    {
                        return QuestReply.Event(160);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AbysseaLaTheine, 160, q =>
                {
                    q.Trade.Confirm();
                    q.Player.AddCurrency("cruor", 200);
                    q.Text.Special(Text.AbysseaLaTheine.CRUOR_TOTAL, 200, q.Player.Currency("cruor"));
                    q.Quest.Complete();
                });
        }
    }
}
