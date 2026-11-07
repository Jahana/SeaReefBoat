// Candle Making (Log ID 3, Quest ID 22), a line-for-line port of scripts/quests/jeuno/Candle_Making.lua (quest rework
// B3b). Rouliette : !pos -24 -2 11 244. While A Candlelight Vigil is accepted Rouliette offers 36 (any option: begun).
// Accepted: she plays 36 again (no finish acts on it now), and a Lanolin Cube (tradeMatches) plays 37: completed (the
// Holy Candle, Believer of Altana), fame +13 x3, the cube taken. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class CandleMaking : QuestScript
    {
        public CandleMaking() : base(QuestLog.Jeuno, "CANDLE_MAKING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Candle_Making";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.HolyCandle, Title = Title.BelieverOfAltana });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "A_CANDLELIGHT_VIGIL") == QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Rouliette).OnTrigger(QuestReply.ProgressEvent(36))
                .OnEventFinish(Zone.UpperJeuno, 36, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Rouliette).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.LanolinCube, 1)))
                    {
                        return QuestReply.ProgressEvent(37);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(36);
                })
                .OnEventFinish(Zone.UpperJeuno, 37, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                        q.Trade.Complete();
                    }
                });
        }
    }
}
