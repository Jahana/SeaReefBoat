// Too Many Chefs (Log ID 1, Quest ID 86), ported from the quest branches of scripts/zones/Metalworks/npcs/Ferghus.lua and
// Leonhardt.lua, scripts/zones/Bastok_Markets_[S]/npcs/Raginmund.lua and scripts/zones/Bastok_Markets/npcs/Umberto.lua
// (quest rework B3a). The quest lives in four NPC scripts across three zones, so each NPC calls it (xi.cs.quest /
// xi.cs.questEvent) and keeps its own default event when the quest does not act. Its state is the char var TOO_MANY_CHEFS
// (1-5), not a quest var; like the NPC scripts, the steps do not look at the quest's status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TooManyChefs : QuestScript
    {
        public TooManyChefs() : base(QuestLog.Bastok, "TOO_MANY_CHEFS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Ferghus: the offer (Wings of the Goddess on, Bastok fame level 5)
            b.State(QuestStatus.Available, when: q =>
                    q.Settings.MainEquals("ENABLE_WOTG", 1) && q.Player.FameLevel(FameArea.Bastok) >= 5)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ferghus).Called().OnTrigger(QuestReply.Event(946))
                .OnEventFinish(Zone.Metalworks, 946, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar("TOO_MANY_CHEFS", 1);
                    }
                });

            // 1: Leonhardt, part 2
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 1)
                .Or(QuestStatus.Accepted, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 1)
                .Or(QuestStatus.Completed, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 1)
                .Npc(Zone.Metalworks, Npc.Metalworks.Leonhardt).Called().OnTrigger(QuestReply.Event(948))
                .OnEventFinish(Zone.Metalworks, 948, q => q.Player.SetCharVar("TOO_MANY_CHEFS", 2));

            // 2: Raginmund in Bastok Markets [S], part 3
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 2)
                .Or(QuestStatus.Accepted, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 2)
                .Or(QuestStatus.Completed, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 2)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Raginmund).Called().OnTrigger(QuestReply.Event(112))
                .OnEventFinish(Zone.BastokMarketsS, 112, q => q.Player.SetCharVar("TOO_MANY_CHEFS", 3));

            // 3: the Red Oven Mitt to Leonhardt
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 3)
                .Or(QuestStatus.Accepted, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 3)
                .Or(QuestStatus.Completed, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 3)
                .Npc(Zone.Metalworks, Npc.Metalworks.Leonhardt).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.RedOvenMitt, 1))
                    {
                        q.Trade.Complete();
                        return QuestReply.Event(950);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 950, q => q.Player.SetCharVar("TOO_MANY_CHEFS", 4));

            // 4: back to Ferghus
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 4)
                .Or(QuestStatus.Accepted, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 4)
                .Or(QuestStatus.Completed, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 4)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ferghus).Called().OnTrigger(QuestReply.Event(947))
                .OnEventFinish(Zone.Metalworks, 947, q => q.Player.SetCharVar("TOO_MANY_CHEFS", 5));

            // 5: Umberto, Aileen's Delight
            b.State(QuestStatus.Available, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 5)
                .Or(QuestStatus.Accepted, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 5)
                .Or(QuestStatus.Completed, when: q => q.Player.CharVar("TOO_MANY_CHEFS") == 5)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Umberto).Called().OnTrigger(QuestReply.Event(473))
                .OnEventFinish(Zone.BastokMarkets, 473, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.BastokMarkets.ITEM_CANNOT_BE_OBTAINED, Item.AileensDelight);
                    }
                    else
                    {
                        q.Player.AddItem(Item.AileensDelight);
                        q.Text.Special(Text.BastokMarkets.ITEM_OBTAINED, Item.AileensDelight);
                        q.Player.AddFame(FameArea.Bastok, 50);
                        q.Player.SetCharVar("TOO_MANY_CHEFS", 0);
                        // the raw player:completeQuest, which keeps the quest's own Quest[1][86] vars
                        q.Player.CompleteQuest(QuestLog.Bastok, "TOO_MANY_CHEFS");
                    }
                });
        }
    }
}
