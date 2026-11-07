// Hoist the Jelly, Roger (Log ID 2, Quest ID 51), ported from scripts/zones/Windurst_Waters/npcs/Maysoon.lua (quest
// rework B3b, Windurst group 20; call-only: every branch of her script is this quest's, so her script only calls it).
// Maysoon : !pos -105 -2 69 238. No default action, and no other quest answers her.
// Trigger: Cook's Pride (Jeuno, C#) accepted and this quest available: 10000 (its finish, any option: begun); else 266
// (her standard dialog). Trade, accepted: exactly one Serving of Royal Jelly (hasItemQty is exact), no gil and nothing
// else (getItemCount 1): 10001; its finish: completed, the Super Soup Pot (npcUtil.giveKeyItem, KEYITEM_OBTAINED),
// Windurst fame 40, the jelly taken. Cook's Pride's Naruru reads the pot.
// Kept as the Lua has it: 10000 begins on any option; 10001's finish reads no status and completes the quest before the
// key item and the fame. The trade's getGil() == 0 operand is shadowed in game (a gil slot counts in getItemCount, so
// the count already refuses gil): only the XI.Verify.Quests check `windurst g20:Maysoon's trade, a jelly and gil`
// pins it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class HoistTheJellyRoger : QuestScript
    {
        public HoistTheJellyRoger() : base(QuestLog.Windurst, "HOIST_THE_JELLY_ROGER") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Maysoon.lua onTrigger: Cook's Pride accepted and this quest available: start the quest
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "COOKS_PRIDE") == QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Maysoon).Called().OnTrigger(QuestReply.Event(10000));

            // else: standard dialog
            b.AnyState()
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Maysoon).Called().OnTrigger(QuestReply.Event(266));

            // Maysoon.lua onTrade: accepted, one jelly, no gil, nothing else: finish the quest
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Maysoon).Called()
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.ServingOfRoyalJelly, 1) &&
                            q.Trade.Gil == 0 &&
                            q.Trade.ItemCount == 1)
                        {
                            return QuestReply.Event(10001);
                        }

                        return null;
                    });

            // Maysoon.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 10000, q => q.Quest.Begin())
                .OnEventFinish(Zone.WindurstWaters, 10001, q =>
                {
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][51] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "HOIST_THE_JELLY_ROGER");
                    q.Player.GiveKeyItem(KeyItem.SuperSoupPot);
                    q.Player.AddFame(FameArea.Windurst, 40);
                    q.Trade.Complete();
                });
        }
    }
}
