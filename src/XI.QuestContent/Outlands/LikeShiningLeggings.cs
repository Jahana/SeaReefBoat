// Like Shining Leggings (Log ID 5, Quest ID 139), a line-for-line port of scripts/quests/outlands/Like_Shining_Leggings.lua
// (quest rework B3b Outlands, group 4). Norg: Heizo !pos -2.6 -4.8 22.8 (no script, no other quest; the default 126).
// Offered at Norg fame level 3: Heizo 127 begins it. Accepted: Heizo 128 (Prog: the Rusty Leggings turned in so far). A
// trade of Rusty Leggings alone: when Prog plus the count reaches 10, 129 (its finish: quest:complete, the Scroll of
// Dokumori: Ichi, Norg fame 30, Looks Good in Leggings, then the trade taken by tradeComplete); below 10 the trade is taken
// at once (tradeComplete in the trade handler), Prog becomes the total and 128 (the total) plays. Any other trade (none of
// the leggings, or another item with them): 128 (Prog).
// Kept as the Lua has it: the trade handler takes the leggings itself before the 128 reply; the inner `turnedInVar <= 9`
// is always true there (the total is below 10 and the trade holds a legging), so its nil arm is dead; 129's trade is taken
// only when quest:complete gives the scroll.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class LikeShiningLeggings : QuestScript
    {
        public LikeShiningLeggings() : base(QuestLog.Outlands, "LIKE_SHINING_LEGGINGS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Like_Shining_Leggings";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Norg,
                Fame = 30,
                Title = Title.LooksGoodInLeggings,
                Item = Item.ScrollOfDokumoriIchi,
            });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Norg) >= 3)
                .Npc(Zone.Norg, Npc.Norg.Heizo).OnTrigger(QuestReply.ProgressEvent(127))
                .OnEventFinish(Zone.Norg, 127, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Heizo)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(128, q.Quest.Step); // Update the player on the number of Rusty Leggings they have turned in
                    })
                    .OnTrade(q =>
                    {
                        int legging = q.Trade.ItemQty(Item.RustyLeggings);
                        int turnedInVar = q.Quest.Step;
                        int totalLeggings = legging + turnedInVar;

                        if (legging > 0 &&
                            legging == q.Trade.ItemCount) // Verifies that only Rusty Leggings are being traded
                        {
                            if (turnedInVar + legging >= 10) // complete quest
                            {
                                return QuestReply.ProgressEvent(129);
                            }
                            else if (turnedInVar <= 9) // turning in less than the amount needed to finish the quest
                            {
                                q.Trade.Complete();
                                q.Quest.SetStep(totalLeggings);
                                return QuestReply.Event(128, totalLeggings); // Update player on number of leggings turned in
                            }
                        }
                        else
                        {
                            return QuestReply.Event(128, turnedInVar); // Update player on number of leggings turned in, but doesn't accept anything other than leggings
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 129, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
