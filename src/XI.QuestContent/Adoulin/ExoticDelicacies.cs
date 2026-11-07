// Exotic Delicacies (Log ID 9, Quest ID 74), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Flapano.lua (quest rework B3b Adoulin group 8; call-only: the script calls this
// quest). Flapano : !pos 70 0 -13 256 (no default). His script keeps All the Way to the Bank's gil trade (5071, first;
// no quest id) and the shop's text and stock.
// Talk (after The Weatherspoon War's call): not completed and Flapano_Odd_Even 0: available 2860, accepted 2863, and
// the var set 1. Else (his shop's turn): not completed: the var set 0; the call answers nothing and the script shows
// the shop. Trade (after All the Way to the Bank's arm), accepted: a Pinch of Saffron, a Mussel and two Barnacles:
// 2861; else a Plate of Barnacle Paella or a Plate of Flapano's Paella: 2862. 2860's finish on option 1: added.
// 2861's finish: npcUtil.completeQuest with 500 bayld, a Plate of Flapano's Paella and 1000 exp (at their rates),
// written out step for step, ending in the raw player:completeQuest, which keeps the quest's own Quest[9][74] vars;
// when it completes, the trade confirmed and the var 0.
// Kept as the script has it: the offer and the shop alternate on the var; 2862 has no finish (the paella stays); the
// finishes read no status. The shop turn's var reset now runs before the shop's text and stock (the Lua set it
// after them), within the same talk: a trace compares the step's rows after it, not their order.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class ExoticDelicacies : QuestScript
    {
        public ExoticDelicacies() : base(QuestLog.Adoulin, "EXOTIC_DELICACIES") { }

        // items with no generated constant (item_basic.sql; no xi.item name)
        private const int PinchOfSaffron = 3916;
        private const int Mussel = 5949;
        private const int Barnacle = 5954;

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Flapano).Called().OnTrade(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // npcUtil.tradeHas(trade, { 3916, 5949, { 5954, 2 } })
                        if (q.Trade.HasQty((PinchOfSaffron, 1), (Mussel, 1), (Barnacle, 2)))
                        {
                            return QuestReply.Event(2861);
                        }
                        else if (q.Trade.Has(Item.PlateOfBarnaclePaella) || q.Trade.Has(Item.PlateOfFlapanosPaella))
                        {
                            return QuestReply.Event(2862);
                        }
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    QuestStatus exoticDelicacies = q.Quest.Status;

                    // Flapano offers his quest every other time the player talks to him
                    if (exoticDelicacies != QuestStatus.Completed && q.Player.CharVar("Flapano_Odd_Even") == 0)
                    {
                        QuestReply reply = null;
                        if (exoticDelicacies == QuestStatus.Available)
                        {
                            reply = QuestReply.Event(2860);
                        }
                        else if (exoticDelicacies == QuestStatus.Accepted)
                        {
                            reply = QuestReply.Event(2863);
                        }

                        q.Player.SetCharVar("Flapano_Odd_Even", 1);
                        return reply;
                    }

                    // SHOP: his script shows it when this answers nothing; the var's reset is the quest's
                    if (exoticDelicacies != QuestStatus.Completed)
                    {
                        q.Player.SetCharVar("Flapano_Odd_Even", 0);
                    }

                    return null;
                });

            // the finishes, keyed on the csid (and option) alone as the script's are
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 2860, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 2861, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[9][74] vars
                    if (q.Player.GiveItem(Item.PlateOfFlapanosPaella))
                    {
                        int bayld = RateAmount(500, q.Settings.MainNumber("BAYLD_RATE"));
                        q.Player.AddCurrency("bayld", bayld);
                        q.Text.Special(Text.WesternAdoulin.BAYLD_OBTAINED, bayld);
                        q.Player.AddExp(RateAmount(1000, q.Settings.MainNumber("EXP_RATE")));
                        q.Player.CompleteQuest(QuestLog.Adoulin, "EXOTIC_DELICACIES");
                        q.Trade.Confirm();
                        q.Player.SetCharVar("Flapano_Odd_Even", 0);
                    }
                });
        }

        // n * rate as the engine's bindings read a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
