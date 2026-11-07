// Tiger's Teeth (Log ID 0, Quest ID 23), a line-for-line port of scripts/quests/sandoria/Tigers_Teeth.lua (quest rework
// B3b, San d'Oria group 1). Taumila : !pos -140 -6 -8 230. Offered at San d'Oria fame level 3: 574 (option 0 begins it).
// Accepted: 575 (replaceDefault); exactly three Black Tiger Fangs play 572 (completed: 2100 gil, Fang Finder; the trade
// confirmed), any other trade 573. Completed: 571 (replaceDefault); the three fangs again set the quest's local var
// Option and play 572, which pays 2100 gil and fame +16 and confirms the trade; any other trade 573.
// Kept as the Lua has it: the reward's fame 16 has no fameArea, so npcUtil.completeQuest gives no fame on the first
// completion (each repeat adds 16 through addFame).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TigersTeeth : QuestScript
    {
        public TigersTeeth() : base(QuestLog.Sandoria, "TIGERS_TEETH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Tigers_Teeth";

        protected override void Build(QuestScriptBuilder b)
        {
            // no fameArea, as the Lua reward has none
            b.Reward(new QuestReward { Gil = 2100, Title = Title.FangFinder, Fame = 16 });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 3)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Taumila).OnTrigger(QuestReply.ProgressEvent(574))
                .OnEventFinish(Zone.SouthernSanDoria, 574, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Taumila)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, { { xi.item.BLACK_TIGER_FANG, 3 } })
                        if (q.Trade.HasExactly((Item.BlackTigerFang, 3)))
                        {
                            return QuestReply.ProgressEvent(572);
                        }
                        else
                        {
                            return QuestReply.Event(573);
                        }
                    })
                    .OnTrigger(QuestReply.Event(575).ReplaceDefault())
                .OnEventFinish(Zone.SouthernSanDoria, 572, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Taumila)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, { { xi.item.BLACK_TIGER_FANG, 3 } })
                        if (q.Trade.HasExactly((Item.BlackTigerFang, 3)))
                        {
                            q.Quest.SetLocalVar("Option", 1);
                            return QuestReply.ProgressEvent(572);
                        }
                        else
                        {
                            return QuestReply.Event(573);
                        }
                    })
                    .OnTrigger(QuestReply.Event(571).ReplaceDefault())
                .OnEventFinish(Zone.SouthernSanDoria, 572, q =>
                {
                    if (q.Quest.LocalVar("Option") == 1 &&
                        q.Player.GiveCurrency("gil", 2100))
                    {
                        q.Quest.SetLocalVar("Option", 0);
                        q.Trade.Confirm();
                        q.Player.AddFame(FameArea.Sandoria, 16);
                    }
                });
        }
    }
}
