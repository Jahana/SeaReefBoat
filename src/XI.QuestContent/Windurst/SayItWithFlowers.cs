// Say it with Flowers (Log ID 2, Quest ID 50), a line-for-line port of scripts/quests/windurst/Say_It_With_Flowers.lua
// (quest rework B3b). Moari-Kaaori : !pos -252 -5 -230 238. Offered at fame level 2. While Prog is 1 (any status): the
// Tahrongi Cacti give cacti, Ohbiru sells one flower (516: options 0-5 buy, 7 asks about the cactus), Kenapa and Ohbiru
// talk about the cactus once asked, and Moari takes a cactus (520: an Iron Sword the first time, SIWFSword; 525: 400 gil
// after) or exactly one of the six flowers (522: 100 gil). Each hand-in completes the quest (Cupid's Florist, fame +10)
// and sets its mustZone; after zoning Moari restarts it (523 / 685: Prog 1). Food for Thought reads its Option and Prog
// char vars. Kept as the Lua file has it: 520 completes without checking quest:complete; 516 option 6 (no such flower)
// and a buyer short of gil both get WHAT_DO_YOU_MEAN; Option 1 is never reset until a completion clears the vars; the
// cactus trade is not exact (npcUtil.tradeHas: the cactus is taken, anything else traded is kept).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class SayItWithFlowers : QuestScript
    {
        // flowerList: [option] = { itemId, gil }, options 0-5
        static readonly (int ItemId, int Gil)[] FlowerList =
        {
            (Item.Carnation, 300),
            (Item.RedRose, 200),
            (Item.RainLily, 250),
            (Item.Lilac, 150),
            (Item.Amaryllis, 200),
            (Item.Marguerite, 100),
        };

        static readonly int[] Flowers =
        {
            Item.Carnation,
            Item.RedRose,
            Item.RainLily,
            Item.Lilac,
            Item.Amaryllis,
            Item.Marguerite,
        };

        public SayItWithFlowers() : base(QuestLog.Windurst, "SAY_IT_WITH_FLOWERS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Say_It_With_Flowers";

        // npcUtil.tradeSetInList(trade, itemList): its body, line for line: the first item of the list traded exactly
        static bool TradeSetInList(IQuestTrade trade, int[] itemList)
        {
            foreach (int item in itemList)
            {
                if (trade.HasExactly(item))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.CupidsFlorist });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MoariKaaori).OnTrigger(QuestReply.ProgressEvent(514))
                .OnEventFinish(Zone.WindurstWaters, 514, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetCharVar("SIWFSword", 1); // Used to mark that the player has NOT recieved the sword reward yet.
                        q.Quest.Begin();
                        q.Quest.SetStep(1);
                    }
                });

            // vars.Prog == 1, whatever the status
            b.AnyState(when: q => q.Quest.Step == 1)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.TahrongiCacti).OnTrigger(q =>
                {
                    // The player can obtain as many as they want. They are unsellable.
                    if (q.Player.GiveItem(Item.TahrongiCactus, true, false))
                    {
                        // This dialogue does not currently play on retail
                        return QuestReply.MessageSpecial(Text.TahrongiCanyon.BUD_BREAKS_OFF, 0, Item.TahrongiCactus);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.Event(519);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.Event(517);
                    }
                    else if (q.Quest.MustZone)
                    {
                        return QuestReply.ProgressEvent(518);
                    }
                    else
                    {
                        return QuestReply.Event(516).Option(4, Item.TahrongiCactus);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MoariKaaori)
                    .OnTrigger(q => QuestReply.Event(515))
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.TahrongiCactus))
                        {
                            if (q.Player.CharVar("SIWFSword") == 1)
                            {
                                return QuestReply.ProgressEvent(520); // Player has not received the sword already
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(525, 400); // Player has received the sword already
                            }
                        }
                        else if (TradeSetInList(q.Trade, Flowers))
                        {
                            return QuestReply.ProgressEvent(522); // Player traded a flower that isn't the cactus.
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 516, q =>
                {
                    if (q.Option < 7)
                    {
                        // flowerList[option]: nil for an option outside 0-5
                        bool hasChoice = q.Option >= 0 && q.Option < FlowerList.Length;

                        if (hasChoice && q.Player.Gil >= FlowerList[q.Option].Gil)
                        {
                            var choice = FlowerList[q.Option];

                            if (q.Player.GiveItem(choice.ItemId))
                            {
                                q.Player.DelGil(choice.Gil);
                                q.Quest.SetMustZone();
                            }
                        }
                        else
                        {
                            // player:messageText(npc, watersID.text.WHAT_DO_YOU_MEAN): its showName and mode are nil, which
                            // the binding reads as true and 0
                            q.Text.MessageText(Text.WindurstWaters.WHAT_DO_YOU_MEAN, true, 0);
                        }
                    }
                    else if (q.Option == 7)
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 520, q => // Cactus trade with Iron Sword reward
                {
                    if (q.Player.GiveItem(Item.IronSword))
                    {
                        q.Quest.Complete();
                        q.Trade.Confirm();
                        q.Player.AddFame(FameArea.Windurst, 10);
                        q.Quest.SetMustZone();
                        q.Quest.SetVar("Stage", 1);
                        q.Player.SetCharVar("SIWFSword", 0); // Removes the var so the player cannot obtain the sword again.
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 522, q => // Flower trade
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddFame(FameArea.Windurst, 10);
                        q.Player.AddGil(100);
                        q.Quest.SetMustZone();
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 525, q => // Cactus trade repeat
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddFame(FameArea.Windurst, 10);
                        q.Player.AddGil(400); // Obtained gil text baked into the cs
                        q.Quest.SetMustZone();
                        q.Quest.SetVar("Stage", 1);
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.Step == 0)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MoariKaaori).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(521);
                    }
                    else if (q.Quest.Var("Stage") == 1)
                    {
                        return QuestReply.ProgressEvent(685, 0, Item.TahrongiCactus); // Player had turned in a cactus
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(523); // Player had turned in a flower
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 523, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.WindurstWaters, 685, q => q.Quest.SetStep(1));
        }
    }
}
