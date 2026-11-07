// Like a Shining Subligar (Log ID 5, Quest ID 138), a line-for-line port of
// scripts/quests/outlands/Like_Shining_Subligar.lua (quest rework B3b Outlands, group 4). Norg: Heiji !pos 0.1 -4.9 23.1 (no
// script, no other quest; the default 122).
// Offered at Norg fame level 3: Heiji 123 begins it. Accepted: Heiji 124 (Prog: the Rusty Subligars turned in so far). A
// trade with no subligar, or with another item: 124 (Prog). Subligars alone: a total of 10 or more plays 125 (its finish:
// quest:complete, the Scroll of Kurayami: Ichi, Norg fame 30, Looks Sublime in a Subligar, then the trade taken by
// tradeComplete); below 10 the trade is taken at once (tradeComplete in the trade handler), Prog becomes the total and 124
// (the total) plays.
// Kept as the Lua has it: the trade handler takes the subligars itself before the 124 reply; 125's trade is taken only
// when quest:complete gives the scroll.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class LikeAShiningSubligar : QuestScript
    {
        public LikeAShiningSubligar() : base(QuestLog.Outlands, "LIKE_A_SHINING_SUBLIGAR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Like_Shining_Subligar";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Norg,
                Fame = 30,
                Title = Title.LooksSublimeInASubligar,
                Item = Item.ScrollOfKurayamiIchi,
            });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Norg) >= 3)
                .Npc(Zone.Norg, Npc.Norg.Heiji).OnTrigger(QuestReply.ProgressEvent(123))
                .OnEventFinish(Zone.Norg, 123, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Heiji)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(124, q.Quest.Step); // Update the player on the number of Rusty Subligars they have turned in
                    })
                    .OnTrade(q =>
                    {
                        int subligarsInTrade = q.Trade.ItemQty(Item.RustySubligar);
                        int subligarsStored = q.Quest.Step;
                        int subligarsTotal = subligarsInTrade + subligarsStored;

                        // No subligars in trade or items other than subligars.
                        if (subligarsInTrade == 0 ||
                            subligarsInTrade != q.Trade.ItemCount)
                        {
                            return QuestReply.Event(124, subligarsStored);
                        }

                        // Trade has only subligars. Check how many we have total.
                        if (subligarsTotal >= 10)
                        {
                            return QuestReply.ProgressEvent(125); // Complete quest
                        }
                        else
                        {
                            q.Trade.Complete();
                            q.Quest.SetStep(subligarsTotal);
                            return QuestReply.Event(124, subligarsTotal);
                        }
                    })
                .OnEventFinish(Zone.Norg, 125, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
