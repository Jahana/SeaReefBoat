// Undying Flames (Log ID 0, Quest ID 26), ported from the quest branches of
// scripts/zones/Northern_San_dOria/npcs/Pagisalis.lua (quest rework B3b, San d'Oria group 11; call-only: the script now
// calls this quest). Pagisalis : !pos 138.6 6.5 131.7 231. His 562 (San d'Oria fame level 2, available; option 0 adds
// the quest), 565 while accepted, 566 once completed; while accepted exactly two Lumps of Beeswax play 563, whose
// finish (with a free slot) takes them and completes the quest (Faith Like a Candle, Friar's Rope, ITEM_OBTAINED, fame
// +20) or, with none, shows ITEM_CANNOT_BE_OBTAINED. The script keeps its own 564 when the quest does not act
// (available below fame level 2).
// Kept as the Lua has it: the beeswax test is hasItemQty(2) and two items in all (three lumps fail); the finishes read
// no status (562's option 0 on a completed quest sets the log's current bit; 563 completes from any status); 563's
// finish checks the free slots itself and gives the rope with addItem, before completeQuest (the raw
// player:completeQuest, which keeps the quest's own Quest[0][26] vars).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class UndyingFlames : QuestScript
    {
        public UndyingFlames() : base(QuestLog.Sandoria, "UNDYING_FLAMES") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Pagisalis.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Pagisalis).Called().OnTrigger(QuestReply.Event(562));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Pagisalis).Called()
                    .OnTrigger(QuestReply.Event(565))
                    // Pagisalis.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.LumpOfBeeswax, 2) &&
                            q.Trade.ItemCount == 2)
                        {
                            return QuestReply.Event(563);
                        }
                        return null;
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Pagisalis).Called().OnTrigger(QuestReply.Event(566));

            // Pagisalis.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.NorthernSanDoria, 562, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 563, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.NorthernSanDoria.ITEM_CANNOT_BE_OBTAINED, Item.FriarsRope);
                    }
                    else
                    {
                        q.Trade.Complete();
                        q.Player.AddTitle(Title.FaithLikeACandle);
                        q.Player.AddItem(Item.FriarsRope);
                        q.Text.Special(Text.NorthernSanDoria.ITEM_OBTAINED, Item.FriarsRope);
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][26] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "UNDYING_FLAMES");
                    }
                });
        }
    }
}
