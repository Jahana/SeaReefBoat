// Breaking the Ice (Log ID 9, Quest ID 54), a line-for-line port of scripts/quests/adoulin/Breaking_the_Ice.lua (quest
// rework B3b Adoulin group 3). Kamihr Drifts: Traiffeaux !pos 437.451 63 -290.512 267 (no script, no default).
// Available: Traiffeaux: 24, whose finish begins it on option 2. Accepted: Traiffeaux: 25 (an Event); a trade of three
// Rabbit Hides and a Raaz Tusk (exactly): 26, whose finish gives the Pair of Fuzzy Earmuffs (npcUtil.giveKeyItem),
// shows YOU_HAVE_LEARNED Fragmenting, then completes it (500 bayld, the Adoulin fame area), confirms the trade and
// adds Fragmenting (no message).
// Kept as the Lua has it: the earmuffs and the "learned" line come before the completion; the trade is confirmed after
// it; the completion cannot fail (no item in the reward).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class BreakingTheIce : QuestScript
    {
        public BreakingTheIce() : base(QuestLog.Adoulin, "BREAKING_THE_ICE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Breaking_the_Ice";

        // requiredTradeItems
        private static readonly (int Item, int Qty)[] RequiredTradeItems =
        {
            (Item.RabbitHide, 3),
            (Item.RaazTusk, 1),
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.KamihrDrifts, Npc.KamihrDrifts.Traiffeaux).OnTrigger(QuestReply.ProgressEvent(24))
                .OnEventFinish(Zone.KamihrDrifts, 24, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.KamihrDrifts, Npc.KamihrDrifts.Traiffeaux).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(RequiredTradeItems))
                    {
                        // NOTE: This event may cause Windower to hang part-way through.

                        return QuestReply.ProgressEvent(26);
                    }

                    return null;
                })
                .OnTrigger(QuestReply.Event(25))
                .OnEventFinish(Zone.KamihrDrifts, 26, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.PairOfFuzzyEarmuffs);
                    q.Text.Special(Text.KamihrDrifts.YOU_HAVE_LEARNED, KeyItem.Fragmenting);

                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddKeyItem(KeyItem.Fragmenting);
                    }
                });
        }
    }
}
