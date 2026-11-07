// Donate to Recycling (Log ID 4, Quest ID 16), ported from scripts/zones/Selbina/npcs/Romeo.lua (quest rework B3b,
// Other Areas group 10; call-only: every branch of his script is this quest's, so his script only calls it; he has no
// DefaultActions entry). Romeo : !pos -11 -11 -6 248.
// Trigger: available: 20; accepted: 22; else 23. 20's finish: begun.
// Trade, accepted, at least 5 of one Onion weapon (Dagger, Knife, Sword, Rod, Staff; npcUtil.tradeHas, the first that
// matches confirmed): 21. 21's finish: npcUtil.completeQuest with the Wastebasket and Ecologist, step for step (the
// Wastebasket, stopping when it cannot be given; the title; the raw player:completeQuest, which keeps the quest's own
// Quest[4][16] vars); done: San d'Oria and Bastok fame +10, the confirmed items taken.
// Kept as the Lua has it: 20 begins with no option read; 21's completeQuest reads no status (a full inventory: false,
// the trade kept).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class DonateToRecycling : QuestScript
    {
        public DonateToRecycling() : base(QuestLog.OtherAreas, "DONATE_TO_RECYCLING") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.Selbina, Npc.Selbina.Romeo).Called()
                    // Romeo.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            (q.Trade.HasQty((Item.OnionDagger, 5)) ||
                             q.Trade.HasQty((Item.OnionKnife, 5)) ||
                             q.Trade.HasQty((Item.OnionSword, 5)) ||
                             q.Trade.HasQty((Item.OnionRod, 5)) ||
                             q.Trade.HasQty((Item.OnionStaff, 5))))
                        {
                            return QuestReply.Event(21); // Finish quest "Donate to Recycling"
                        }

                        return null;
                    })
                    // Romeo.lua onTrigger
                    .OnTrigger(q =>
                    {
                        QuestStatus donateToRecycling = q.Quest.Status;

                        if (donateToRecycling == QuestStatus.Available)
                        {
                            return QuestReply.Event(20); // Start quest "Donate to Recycling"
                        }
                        else if (donateToRecycling == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(22); // During quest "Donate to Recycling"
                        }
                        else
                        {
                            return QuestReply.Event(23); // Standard dialog
                        }
                    });

            b.AnyState()
                // Romeo.lua onEventFinish 20 / 21
                .OnEventFinish(Zone.Selbina, 20, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.Selbina, 21, q =>
                {
                    if (CompleteWithReward(q))
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Trade.Confirm();
                    }
                });
        }

        // npcUtil.completeQuest(player, OTHER_AREAS, DONATE_TO_RECYCLING, { item = WASTEBASKET, title = ECOLOGIST }),
        // step for step: not quest:complete, whose cleanup would clear the quest's own Quest[4][16] vars
        static bool CompleteWithReward(IQuestContext q)
        {
            if (!q.Player.GiveItem(Item.Wastebasket))
            {
                return false; // no free slot (or the wastebasket held): npcUtil.completeQuest returns false
            }

            q.Player.AddTitle(Title.Ecologist);
            q.Player.CompleteQuest(QuestLog.OtherAreas, "DONATE_TO_RECYCLING");
            return true;
        }
    }
}
