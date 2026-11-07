// Lerene's Lament (Log ID 9, Quest ID 126), a line-for-line port of scripts/quests/adoulin/Lerenes_Lament.lua (quest
// rework B3b Adoulin group 4). Outer Ra'Kaznar: Lerene !pos -46.876 -179.334 -28.602 274 (no script; default 25).
// Available: Lerene: 22, whose finish begins it on option 2. Accepted: Lerene: 23 (an Event); a trade of two
// Squares of Ancestral Cloth (exactly): 24, whose finish gives Lerene's Paten (npcUtil.giveKeyItem), shows
// YOU_HAVE_LEARNED Pulverizing, then completes it (500 bayld, the Adoulin fame area), confirms the trade and adds
// Pulverizing (no message).
// Kept as the Lua has it: the paten and the "learned" line come before the completion; the trade is confirmed after
// it; the completion cannot fail (no item in the reward). The twin of Breaking the Ice.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class LerenesLament : QuestScript
    {
        public LerenesLament() : base(QuestLog.Adoulin, "LERENES_LAMENT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Lerenes_Lament";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.OuterRakaznar, Npc.OuterRakaznar.Lerene).OnTrigger(QuestReply.ProgressEvent(22))
                .OnEventFinish(Zone.OuterRakaznar, 22, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.OuterRakaznar, Npc.OuterRakaznar.Lerene).OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.SquareOfAncestralCloth, 2)))
                    {
                        return QuestReply.ProgressEvent(24);
                    }

                    return null;
                })
                .OnTrigger(QuestReply.Event(23))
                .OnEventFinish(Zone.OuterRakaznar, 24, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LerenesPaten);
                    q.Text.Special(Text.OuterRakaznar.YOU_HAVE_LEARNED, KeyItem.Pulverizing);

                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddKeyItem(KeyItem.Pulverizing);
                    }
                });
        }
    }
}
