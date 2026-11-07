// Only the Best (Log ID 4, Quest ID 18), ported from scripts/zones/Selbina/npcs/Melyon.lua (quest rework B3b, Other
// Areas group 10; call-only: every branch of his script is this quest's, so his script only calls it; he has no
// DefaultActions entry; Riding on the Clouds' C# handler answers his trade first, at its own var).
// Melyon : !pos 25 -6 6 248.
// Trigger: available: 60 (4366, 629, Boyahda Moss); else 61 (the same). 60 option 10: begun.
// Trade, not available: 5 La Theine Cabbages: 62 (0, the cabbage); else 3 Ears of Millioncorn: 63 (0, the
// millioncorn); else a Clump of Boyahda Moss: 64 (0, the moss) (npcUtil.tradeHas; the match confirmed).
// 62 option 11: 100 gil (npcUtil.giveCurrency), Bastok and San d'Oria fame +4, completed, the trade taken. 63 option
// 12: 120 gil, fame +6. 64 option 13: 600 gil, fame +8.
// Kept as the Lua has it: the trade's turn-ins pay and complete again once completed (repeatable); another option ends
// the event with nothing (the items kept).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class OnlyTheBest : QuestScript
    {
        public OnlyTheBest() : base(QuestLog.OtherAreas, "ONLY_THE_BEST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.Selbina, Npc.Selbina.Melyon).Called()
                    // Melyon.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status != QuestStatus.Available)
                        {
                            if (q.Trade.HasQty((Item.LaTheineCabbage, 5))) // La Theine Cabbage x5
                            {
                                return QuestReply.Event(62, 0, Item.LaTheineCabbage);
                            }
                            else if (q.Trade.HasQty((Item.EarOfMillioncorn, 3))) // Millioncorn x3
                            {
                                return QuestReply.Event(63, 0, Item.EarOfMillioncorn);
                            }
                            else if (q.Trade.Has(Item.ClumpOfBoyahdaMoss)) // Boyahda Moss x1
                            {
                                return QuestReply.Event(64, 0, Item.ClumpOfBoyahdaMoss);
                            }
                        }

                        return null;
                    })
                    // Melyon.lua onTrigger
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Available)
                        {
                            // Start quest "Only the Best"
                            return QuestReply.Event(60, 4366, 629, Item.ClumpOfBoyahdaMoss);
                        }
                        else
                        {
                            // During & after completed quest "Only the Best"
                            return QuestReply.Event(61, 4366, 629, Item.ClumpOfBoyahdaMoss);
                        }
                    });

            b.AnyState()
                // Melyon.lua onEventFinish 60 / 62 / 63 / 64
                .OnEventFinish(Zone.Selbina, 60, q =>
                {
                    if (q.Option == 10)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Selbina, 62, q =>
                {
                    if (q.Option == 11)
                    {
                        Pay(q, 100, 4);
                    }
                })
                .OnEventFinish(Zone.Selbina, 63, q =>
                {
                    if (q.Option == 12)
                    {
                        Pay(q, 120, 6);
                    }
                })
                .OnEventFinish(Zone.Selbina, 64, q =>
                {
                    if (q.Option == 13)
                    {
                        Pay(q, 600, 8);
                    }
                });
        }

        static void Pay(IQuestContext q, int gil, int fame)
        {
            q.Player.GiveCurrency("gil", gil);
            q.Player.AddFame(FameArea.Bastok, fame);
            q.Player.AddFame(FameArea.Sandoria, fame);
            // the raw player:completeQuest, which keeps the quest's own Quest[4][18] vars
            q.Player.CompleteQuest(QuestLog.OtherAreas, "ONLY_THE_BEST");
            q.Trade.Confirm();
        }
    }
}
