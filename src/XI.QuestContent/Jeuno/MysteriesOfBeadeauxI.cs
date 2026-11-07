// Mysteries of Beadeaux I (Log ID 3, Quest ID 31), a line-for-line port of
// scripts/quests/jeuno/Mysteries_of_Beadeaux_I.lua (quest rework B3b, Jeuno group 13). Sattal-Mansal : !pos 40 3 -53
// 245. Available with the Silver Bell: Sattal-Mansal's 89 (0); its finish begins this quest and adds Mysteries of
// Beadeaux II (player:addQuest). Accepted: 89 (1), the offer taken (no finish: 89's handler is the available
// section's); a Quadav Charm traded alone (npcUtil.tradeMatches): the Coruscant Rosary added at once (no message), fame
// +7 in the three nations, the trade completed, then 91, whose finish completes the quest by player:completeQuest (no
// reward table: no quest:complete) and prints KEYITEM_OBTAINED.
// Kept as the Lua has it: the key item and the fame are given in the trade handler, before the event (the Lua's
// comment: retail hands over the reward as the event starts); the completion message comes at the finish.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class MysteriesOfBeadeauxI : QuestScript
    {
        public MysteriesOfBeadeauxI() : base(QuestLog.Jeuno, "MYSTERIES_OF_BEADEAUX_I") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Mysteries_of_Beadeaux_I";

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available, when: q => q.Player.HasKeyItem(KeyItem.SilverBell))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.SattalMansal).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(89, 0);
                })
                // This event flags both Mysteries of Beadeaux I and II
                .OnEventFinish(Zone.LowerJeuno, 89, q =>
                {
                    q.Quest.Begin();
                    q.Player.AddQuest(QuestLog.Jeuno, "MYSTERIES_OF_BEADEAUX_II");
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.SattalMansal)
                    // Retail hands over the reward as the event starts. The message prints when it ends.
                    .OnTrade(q =>
                    {
                        if (!q.Trade.Matches((Item.QuadavCharm, 1)))
                        {
                            return null;
                        }

                        q.Player.AddKeyItem(KeyItem.CoruscantRosary);
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Complete();

                        return QuestReply.ProgressEvent(91);
                    })
                    // Param 0 flags the offer as already taken.
                    .OnTrigger(QuestReply.Event(89, 1))
                .OnEventFinish(Zone.LowerJeuno, 91, q =>
                {
                    q.Player.CompleteQuest(QuestLog.Jeuno, "MYSTERIES_OF_BEADEAUX_I");
                    q.Text.Special(Text.LowerJeuno.KEYITEM_OBTAINED, KeyItem.CoruscantRosary);
                });
        }
    }
}
