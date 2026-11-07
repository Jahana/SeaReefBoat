// Open Sesame (Log ID 5, Quest ID 165), a line-for-line port of scripts/quests/outlands/Open_Sesame.lua (quest rework B3b
// Outlands, group 5). Eastern Altepa Desert: Lokpix !pos -61.9 3.9 224.9 (no script; default 24).
// Available: Lokpix 20; option 1 begins it. Accepted: Lokpix 21; a trade holding exactly one Tremorstone and exactly the
// count of one of tradeOptions (1 Meteorite, 1 Soil Gem or 12 Soil Geodes) plays 22, any other trade 23. 22's finish runs
// quest:complete (the Loadstone) and on true adds San d'Oria and Bastok fame 15 each and takes the whole trade
// (tradeComplete). Completed: Lokpix 24 (replaceDefault).
// Kept as the Lua has it: the trade test reads getItemQty only, so other items may ride along (taken by tradeComplete);
// a 23 for every other trade.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class OpenSesame : QuestScript
    {
        // the Lua's tradeOptions, [1] .. [3]: { item, qty }
        static readonly (int Item, int Qty)[] TradeOptions =
        {
            (Item.Meteorite,   1),
            (Item.SoilGem,     1),
            (Item.SoilGeode,  12),
        };

        public OpenSesame() : base(QuestLog.Outlands, "OPEN_SESAME") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Open_Sesame";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.Loadstone });

            b.State(QuestStatus.Available)
                .Npc(Zone.EasternAltepaDesert, Npc.EasternAltepaDesert.Lokpix).OnTrigger(QuestReply.ProgressEvent(20))
                .OnEventFinish(Zone.EasternAltepaDesert, 20, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.EasternAltepaDesert, Npc.EasternAltepaDesert.Lokpix)
                    .OnTrade(q =>
                    {
                        for (int i = 1; i <= TradeOptions.Length; i++)
                        {
                            if (q.Trade.ItemQty(TradeOptions[i - 1].Item) == TradeOptions[i - 1].Qty &&
                                q.Trade.ItemQty(Item.Tremorstone) == 1)
                            {
                                return QuestReply.ProgressEvent(22);
                            }
                        }

                        return QuestReply.Event(23);
                    })
                    .OnTrigger(QuestReply.Event(21))
                .OnEventFinish(Zone.EasternAltepaDesert, 22, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 15);
                        q.Player.AddFame(FameArea.Bastok, 15);
                        q.Trade.Complete();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.EasternAltepaDesert, Npc.EasternAltepaDesert.Lokpix).OnTrigger(QuestReply.Event(24).ReplaceDefault());
        }
    }
}
