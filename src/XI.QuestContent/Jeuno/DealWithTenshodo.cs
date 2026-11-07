// Deal with Tenshodo (Log ID 3, Quest ID 26), a line-for-line port of scripts/quests/jeuno/Deal_with_Tenshodo.lua
// (quest rework B3b). Garnev : !pos 30 4 -36 245. Offered only while A Clock Most Delicate is accepted: Garnev's 167 at
// Norg fame level 2 (begins it on any finish), else his 168. Accepted: exactly one Gold Orcmask plays 166: completed
// (Norg fame +50, the Clock Tower Oil key item, Trader of Renown), the mask taken (confirmTrade). The Lua file has no
// known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class DealWithTenshodo : QuestScript
    {
        public DealWithTenshodo() : base(QuestLog.Jeuno, "DEAL_WITH_TENSHODO") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Deal_with_Tenshodo";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Norg,
                KeyItem = KeyItem.ClockTowerOil,
                Title = Title.TraderOfRenown,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "A_CLOCK_MOST_DELICATE") == QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Garnev).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Norg) >= 2)
                    {
                        return QuestReply.ProgressEvent(167);
                    }
                    else
                    {
                        return QuestReply.Event(168);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 167, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Garnev).OnTrade(q =>
                {
                    // npcUtil.tradeHasExactly
                    if (q.Trade.HasExactly(Item.GoldOrcmask))
                    {
                        return QuestReply.ProgressEvent(166);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 166, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
