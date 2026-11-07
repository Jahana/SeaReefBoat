// Promotion: Second Lieutenant (Log ID 6, Quest ID 97), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_Second_Lieutenant.lua (quest rework B3b Aht Urhgan, group 4). Aht Urhgan
// Whitegate: Abquhbah !pos 35.5 -6.6 -58 (his script: 255), trigger area 3 (31 -7 -60 .. 39 -6 -52, where he stands).
// Available with AssaultPromotion >= 25 and Promotion: Chief Sergeant completed: Abquhbah 5071, or area 3: 5071; its
// finish begins it. Prog 0: Abquhbah 5072 once per zone; a trade of exactly three Imperial Gold Pieces: 5073, whose
// finish takes them (confirmTrade), sets Prog 1 and gives the Officer Academy Manual. Prog 1: Abquhbah, local var Prog
// 0: 5074 (its finish: local Prog 1), else nil; exactly two Imperial Mythril Pieces: 5075, whose finish takes them,
// clears the local var and at option 1 sets Prog 2. Prog 2: 5074 as before; two Mythril Pieces: 5076 (taken, Prog 3).
// Prog 3: Abquhbah 5079 once per zone; one of eleven beastmen items alone: 5077 (taken, local var 0, Prog 4). Prog 4:
// 5074 as before; two Mythril Pieces: 5078, whose finish clears the local var, takes them and at option 1 completes it
// (the SL Wildcat Badge, Second Lieutenant), then sets AssaultPromotion 0, takes the CS Wildcat Badge and says
// SECOND_LIEUTENANT.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 5078's SECOND_LIEUTENANT is sent; the Lua's
// was a quest:messageSpecial action, built in a finish and dropped (no message).
// Kept as the Lua has it: 5071 begins it on any option; 5075 / 5078 take the coins whatever the option; the local var
// Prog lives on the char until it zones (a zone-in loads the char afresh), so 5074 plays once per zone visit until a
// game's trade clears it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionSecondLieutenant : QuestScript
    {
        public PromotionSecondLieutenant() : base(QuestLog.AhtUrghan, "PROMOTION_SECOND_LIEUTENANT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Second_Lieutenant";

        // the beastmen items of the Prog 3 trade, in the Lua's order
        static readonly int[] BeastmenItems =
        {
            Item.MacuahuitlM1, Item.MamoolJaCollar, Item.MamoolJaHelm,
            Item.JadagnaM1, Item.JanuwiyahM1, Item.TariqahM1,
            Item.TrollPauldron, Item.TrollVambrace, Item.LamianArmlet,
            Item.LamianKamanM1, Item.QutrubGorget,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.SlWildcatBadge,
                Title = Title.SecondLieutenant,
            });
            b.Steps(1, 2, 3, 4);

            // Trigger to start the quest
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_CHIEF_SERGEANT"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.ProgressEvent(5071))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q => QuestReply.ProgressEvent(5071))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5071, q => q.Quest.Begin());

            // Trigger for a reminder; trade the coins for the academy tuition
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.ImperialGoldPiece, 3)))
                        {
                            return QuestReply.ProgressEvent(5073);
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.ProgressEvent(5072).OncePerZone())
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5073, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                    q.Player.GiveKeyItem(KeyItem.OfficerAcademyManual);
                });

            // Begin the 1st game: trigger for a reminder, trade the coins to start
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrade(q => TwoMythrilPieces(q) ? QuestReply.ProgressEvent(5075) : null)
                    .OnTrigger(Reminder)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5074, q => q.Quest.SetLocalVar("Prog", 1))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5075, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetLocalVar("Prog", 0);
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                });

            // Start the 2nd game: trigger for a reminder, trade the coins to progress
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrade(q => TwoMythrilPieces(q) ? QuestReply.ProgressEvent(5076) : null)
                    .OnTrigger(Reminder)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5074, q => q.Quest.SetLocalVar("Prog", 1))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5076, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(3);
                });

            // Complete the 2nd game: trigger for a reminder, trade an item to progress
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrade(q =>
                    {
                        foreach (int requiredTrade in BeastmenItems)
                        {
                            if (q.Trade.HasExactly(requiredTrade))
                            {
                                return QuestReply.ProgressEvent(5077);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.ProgressEvent(5079).OncePerZone())
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5077, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetLocalVar("Prog", 0);
                    q.Quest.SetStep(4);
                });

            // The 3rd game: trigger for a reminder, trade the coins to play
            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrade(q => TwoMythrilPieces(q) ? QuestReply.ProgressEvent(5078) : null)
                    .OnTrigger(Reminder)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5074, q => q.Quest.SetLocalVar("Prog", 1))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5078, q =>
                {
                    q.Quest.SetLocalVar("Prog", 0);
                    q.Trade.Confirm();
                    if (q.Option == 1)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Player.SetCharVar("AssaultPromotion", 0);
                            q.Player.DelKeyItem(KeyItem.CsWildcatBadge);
                            // the Lua's quest:messageSpecial: built and dropped (fixed, user ruling 2026-11-04)
                            q.Text.Special(Text.AhtUrhganWhitegate.SECOND_LIEUTENANT);
                        }
                    }
                });
        }

        // npcUtil.tradeHasExactly(trade, { { xi.item.IMPERIAL_MYTHRIL_PIECE, 2 } })
        static bool TwoMythrilPieces(IQuestContext q) => q.Trade.HasExactly((Item.ImperialMythrilPiece, 2));

        // Progs 1, 2, 4: 5074 while the local var Prog is 0
        static QuestReply Reminder(IQuestContext q) =>
            q.Quest.LocalVar("Prog") == 0 ? QuestReply.ProgressEvent(5074) : null;
    }
}
