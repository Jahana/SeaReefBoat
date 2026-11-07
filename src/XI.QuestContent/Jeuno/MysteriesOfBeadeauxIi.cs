// Mysteries of Beadeaux II (Log ID 3, Quest ID 32), a line-for-line port of
// scripts/quests/jeuno/Mysteries_of_Beadeaux_II.lua (quest rework B3b, Jeuno group 13). Sattal-Mansal : !pos 40 3 -53
// 245. Flagged by Mysteries of Beadeaux I's 89 finish (player:addQuest); it has an accepted section only. Accepted: a
// Quadav Augury Shell traded alone (npcUtil.tradeMatches): the Black Matinee Necklace added at once (no message), fame
// +7 in the three nations, the trade completed, then 92, whose finish completes the quest by player:completeQuest (no
// reward table) and prints KEYITEM_OBTAINED. No trigger: Sattal-Mansal's talk is Mysteries of Beadeaux I's (89 (1)
// while it is accepted) or his default 96.
// Kept as the Lua has it: the key item and the fame are given in the trade handler, before the event.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class MysteriesOfBeadeauxIi : QuestScript
    {
        public MysteriesOfBeadeauxIi() : base(QuestLog.Jeuno, "MYSTERIES_OF_BEADEAUX_II") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Mysteries_of_Beadeaux_II";

        protected override void Build(QuestScriptBuilder b)
        {
            // This quest is flagged from an event contained in Mysteries of Beadeaux I
            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.SattalMansal)
                    // Retail hands over the reward as the event starts. The message prints when it ends.
                    .OnTrade(q =>
                    {
                        if (!q.Trade.Matches((Item.QuadavAuguryShell, 1)))
                        {
                            return null;
                        }

                        q.Player.AddKeyItem(KeyItem.BlackMatineeNecklace);
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Complete();

                        return QuestReply.ProgressEvent(92);
                    })
                .OnEventFinish(Zone.LowerJeuno, 92, q =>
                {
                    q.Player.CompleteQuest(QuestLog.Jeuno, "MYSTERIES_OF_BEADEAUX_II");
                    q.Text.Special(Text.LowerJeuno.KEYITEM_OBTAINED, KeyItem.BlackMatineeNecklace);
                });
        }
    }
}
