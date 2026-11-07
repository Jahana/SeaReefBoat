// Waters of the Cheval (Log ID 0, Quest ID 1), a line-for-line port of scripts/quests/sandoria/Waters_of_the_Cheval.lua
// (quest rework B3b, San d'Oria group 10). Miageau : !pos 115 0 108 231; Nouveil : !pos 123 0 106 231; Cheval_River : !pos
// 223 -58 426 101. Available: Miageau's 504 (begun on any option). Accepted: 10 gil or more to Nouveil shows
// YOU_DONATE_GIL and plays 571 (finish: the Blessed Waterskin, from the trade; the gil taken); Nouveil's 573 / 572 / 575
// with the river water / the waterskin / neither; the waterskin to the Cheval River (East Ronfaure) gives the Skin of
// Cheval River Water silently (the waterskin taken, CHEVAL_RIVER_WATER); the river's BLESSED_WATERSKIN on a talk;
// Miageau's 512 / 519 with / without the waterskin; the river water to Miageau plays 515 (completed: Wing Pendant from
// the trade, fame +20, The Pure One; the water taken). Outside reference: The Vicasque's Sermon's guard reads its
// completion (a status read, in the quest file of group 10).
// Kept as the Lua file has it: the trades are tradeHas (more gil, other items are accepted, only the confirmed part
// taken); the river's give is silent and the river's trade returns nil when the give fails (nothing shown).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class WatersOfTheCheval : QuestScript
    {
        public WatersOfTheCheval() : base(QuestLog.Sandoria, "WATERS_OF_THE_CHEVAL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Waters_of_the_Cheval";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Item = Item.WingPendant,
                ItemParams = new QuestItemParams { FromTrade = true },
                Title = Title.ThePureOne,
            });

            // Speak with Miageau (L-7) just past the entrance to the Cathedral.
            b.State(QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Miageau).OnTrigger(QuestReply.ProgressEvent(504))
                .OnEventFinish(Zone.NorthernSanDoria, 504, q =>
                {
                    q.Quest.Begin();
                });

            // Buy a Blessed Waterskin by trading 10g to Nouveil (to your right). Trade the waterskin to the Cheval River
            // target East Ronfaure (H-5) (top of the river) to receive Cheval Water. Trade Cheval Water to Miageau.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Miageau)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.SkinOfChevalRiverWater))
                        {
                            return QuestReply.ProgressEvent(515);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.BlessedWaterskin))
                        {
                            return QuestReply.Event(512);
                        }
                        else
                        {
                            return QuestReply.Event(519);
                        }
                    })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Nouveil)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { 'gil', 10 } })
                        if (q.Trade.HasWithGil(10))
                        {
                            q.Text.Special(Text.NorthernSanDoria.YOU_DONATE_GIL);
                            return QuestReply.ProgressEvent(571);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.SkinOfChevalRiverWater))
                        {
                            return QuestReply.Event(573);
                        }
                        else if (q.Player.HasItem(Item.BlessedWaterskin))
                        {
                            return QuestReply.Event(572);
                        }
                        else
                        {
                            return QuestReply.Event(575);
                        }
                    })
                .OnEventFinish(Zone.NorthernSanDoria, 515, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 571, q =>
                {
                    // npcUtil.giveItem(player, xi.item.BLESSED_WATERSKIN, { fromTrade = true })
                    if (q.Player.GiveItem(Item.BlessedWaterskin, silent: false, fromTrade: true))
                    {
                        q.Trade.Confirm();
                    }
                })
                .Npc(Zone.EastRonfaure, Npc.EastRonfaure.ChevalRiver)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.BlessedWaterskin) &&
                            q.Player.GiveItem(Item.SkinOfChevalRiverWater, silent: true, fromTrade: true))
                        {
                            q.Trade.Confirm();
                            return QuestReply.MessageSpecial(Text.EastRonfaure.CHEVAL_RIVER_WATER, Item.SkinOfChevalRiverWater);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.MessageSpecial(Text.EastRonfaure.BLESSED_WATERSKIN));
        }
    }
}
