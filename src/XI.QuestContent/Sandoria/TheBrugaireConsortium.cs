// The Brugaire Consortium (Log ID 0, Quest ID 12), a line-for-line port of
// scripts/quests/sandoria/The_Brugaire_Consortium.lua (quest rework B3b, San d'Oria group 6). Fontoumant : !pos -10 -10
// -122 232; Regine : !pos 68 -9 -74 232; Apstaule : !pos -6.983 -13.3 -157.517; Thierride : !pos -67 -5 -28 232.
// Available: Fontoumant's 509 (option 0, not 1, with the Parcel for the Magic Shop given, begins it). Accepted: Prog
// counts the parcels delivered, Option the parcels handed out after the first. Fontoumant: while Prog > Option, the next
// parcel's event (511 at Option 0, 512 at 1: the parcel of index Prog given, Option + 1; 515 at 2: completed, Lauan
// Shield, fame +20, Courier Extraordinaire); else 560. Exactly 100 gil to him while Prog < 3 and Prog == Option plays
// 608 + Prog (the gil taken, Prog + 1: the delivery paid off, the parcel kept). Regine at Prog 0 (the magic shop parcel:
// 535), Apstaule at Prog 1 (the auction house parcel: 540), Thierride at Prog 2 (the pub parcel: 539) take the parcel
// (Prog + 1). Chanpau's script (Southern San d'Oria) reads the status (stays).
// Kept as the Lua has it: 509's option 0 accepts; 515's finish ignores quest:complete's result; the gil trade leaves
// the undelivered parcel in the inventory.
// Fontoumant at Prog > Option with Option 3 or more (a GM-set state) indexes past ParcelItemIds and throws, as the Lua's
// parcelItemIds[3] is nil and errors (a Lua-bug arm, not recorded). Likewise the 511 / 512 finish with a GM-set Prog 3
// and Option 0 or 1: the Lua's triggerOnEventFinish reads parcelItemIds[3][2] and errors before the give, and the C#
// TriggerOnEventFinish indexes ParcelItemIds[3] and throws the same way (a Lua-bug arm, not recorded).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheBrugaireConsortium : QuestScript
    {
        // NOTE: Event Ids in Index 1 are for the next event (Option), while item is used by quest progress (Prog)
        static readonly (int Event, int Item)[] ParcelItemIds =
        {
            (511, Item.ParcelForTheMagicShop),
            (512, Item.ParcelForTheAuctionHouse),
            (515, Item.ParcelForThePub),
        };

        public TheBrugaireConsortium() : base(QuestLog.Sandoria, "THE_BRUGAIRE_CONSORTIUM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Brugaire_Consortium";

        // the Lua's tradeOnEventFinish
        static void TradeOnEventFinish(IQuestContext q)
        {
            q.Trade.Confirm();
            // quest:incrementVar(player, 'Prog', 1) (the Prog var: Step)
            q.Quest.IncrementVar("Prog", 1);
        }

        // the Lua's triggerOnEventFinish
        static void TriggerOnEventFinish(IQuestContext q)
        {
            if (q.Player.GiveItem(ParcelItemIds[q.Quest.Step].Item))
            {
                // quest:incrementVar(player, 'Option', 1)
                q.Quest.IncrementVar("Option", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Item = Item.LauanShield,
                Title = Title.CourierExtraordinaire,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Fontoumant).OnTrigger(QuestReply.ProgressEvent(509))
                .OnEventFinish(Zone.PortSanDoria, 509, q =>
                {
                    if (q.Option == 0 &&
                        q.Player.GiveItem(Item.ParcelForTheMagicShop))
                    {
                        q.Quest.Begin();
                    }
                });

            Action<IQuestContext> tradeOnEventFinish = TradeOnEventFinish;
            Action<IQuestContext> triggerOnEventFinish = TriggerOnEventFinish;

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Fontoumant)
                    .OnTrade(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress < 3 &&
                            questProgress == q.Quest.Var("Option") &&
                            // npcUtil.tradeHasExactly(trade, { { 'gil', 100 } })
                            q.Trade.HasExactlyWithGil(100))
                        {
                            return QuestReply.ProgressEvent(608 + questProgress);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;
                        int questOption = q.Quest.Var("Option");

                        if (questProgress > questOption)
                        {
                            return QuestReply.ProgressEvent(ParcelItemIds[questOption].Event);
                        }
                        else
                        {
                            return QuestReply.Event(560);
                        }
                    })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Regine).OnTrade(q =>
                {
                    if (q.Quest.Step == 0 &&
                        // npcUtil.tradeHasExactly(trade, xi.item.PARCEL_FOR_THE_MAGIC_SHOP)
                        q.Trade.HasExactly(Item.ParcelForTheMagicShop))
                    {
                        return QuestReply.ProgressEvent(535);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Apstaule).OnTrade(q =>
                {
                    if (q.Quest.Step == 1 &&
                        // npcUtil.tradeHasExactly(trade, xi.item.PARCEL_FOR_THE_AUCTION_HOUSE)
                        q.Trade.HasExactly(Item.ParcelForTheAuctionHouse))
                    {
                        return QuestReply.ProgressEvent(540);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Thierride).OnTrade(q =>
                {
                    if (q.Quest.Step == 2 &&
                        // npcUtil.tradeHasExactly(trade, xi.item.PARCEL_FOR_THE_PUB)
                        q.Trade.HasExactly(Item.ParcelForThePub))
                    {
                        return QuestReply.ProgressEvent(539);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 511, triggerOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 512, triggerOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 515, q => q.Quest.Complete())
                .OnEventFinish(Zone.PortSanDoria, 535, tradeOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 539, tradeOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 540, tradeOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 608, tradeOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 609, tradeOnEventFinish)
                .OnEventFinish(Zone.PortSanDoria, 610, tradeOnEventFinish);
        }
    }
}
