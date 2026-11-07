// A Stone's Throw Away (Log ID 9, Quest ID 56), a line-for-line port of scripts/quests/adoulin/A_Stones_Throw_Away.lua
// (quest rework B3b Adoulin group 3). Morimar Basalt Fields: Apolliane !pos 447.088 -15.846 -320 265 (no script, no
// default).
// Available: Apolliane: 2571, whose finish (any option) begins it. Accepted: Apolliane: 2572 (an Event); a trade of a
// Marble Nugget (exactly) at Prog 1: 2573, whose finish shows YOU_HAVE_LEARNED Demolishing, then completes it
// (500 bayld, the Adoulin fame area), adds Demolishing (no message) and confirms the trade.
// Kept as the Lua has it: nothing sets Prog 1 (the Lua's TODO: Morimar's HELM is not implemented; the mining of a
// Marble Nugget would set it), so the turn-in is reachable only by a GM; the "learned" line comes before the
// completion; the completion cannot fail (no item in the reward).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class AStonesThrowAway : QuestScript
    {
        public AStonesThrowAway() : base(QuestLog.Adoulin, "A_STONES_THROW_AWAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/A_Stones_Throw_Away";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.MorimarBasaltFields, Npc.MorimarBasaltFields.Apolliane)
                .OnTrigger(QuestReply.ProgressEvent(2571))
                .OnEventFinish(Zone.MorimarBasaltFields, 2571, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.MorimarBasaltFields, Npc.MorimarBasaltFields.Apolliane).OnTrade(q =>
                {
                    // TODO: Morimar Basalt Fields HELM is not implemented at this time.  When it is,
                    // on mining a Marble Nugget, set the Prog quest variable to 1, as this quest requires
                    // the player to mine this object.
                    if (q.Trade.HasExactly(Item.MarbleNugget) &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(2573);
                    }

                    return null;
                })
                .OnTrigger(QuestReply.Event(2572))
                .OnEventFinish(Zone.MorimarBasaltFields, 2573, q =>
                {
                    q.Text.Special(Text.MorimarBasaltFields.YOU_HAVE_LEARNED, KeyItem.Demolishing);

                    if (q.Quest.Complete())
                    {
                        q.Player.AddKeyItem(KeyItem.Demolishing);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
