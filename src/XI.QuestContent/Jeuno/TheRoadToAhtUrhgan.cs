// The Road to Aht Urhgan (Log ID 3, Quest ID 91), a line-for-line port of scripts/quests/jeuno/The_Road_to_Aht_Urhgan.lua
// (quest rework B3b). Faursel : !pos 37.985 3.118 -45.208 245. Available with ENABLE_TOAU 1: Faursel's 10062 at
// Prog 0 (its finish with option 1: Prog 1, the quest not yet added), else 10063 (0, 0, 0, 0, 0, RoV Inescapable Binds
// current); 10063's updates read the item lists (options 10, 12, 13) or the price (14: the char's gil); its finish with
// option 2 or 3 begins the quest, then the selection: option 1 at RoV Inescapable Binds: completed, the Boarding Permit,
// the mission completed and Desert Winds added; option 2: Prog 1; option 3 with 500000 gil: the gil taken, PAY_FAURSEL,
// Prog 2, the mustZone, Timer = tomorrow. Accepted: a trade at Prog 1 of one of the item sets (the six beginner items,
// the three intermediate ones, the three chips, or one of the 27 coffer keys / testimonies alone): 10069 (the trade
// taken, the mustZone, Prog 3, Timer = tomorrow). Faursel at Prog 0 / 1: 10064 (as 10063, with its updates and its
// selection finish); Prog 2: 10067 once the Timer has come and the mustZone is clear (its option 30 update: (79, 1, 0,
// 34, 910, 540, 99, 99); its finish: Prog 4 and the teleport to the Wajaom leypoint), else 10066; Prog 3: 10070 likewise
// (completed, the Boarding Permit), else 10072; Prog 5: 10068 (completed). Wajaom Woodlands' afterZoneIn at Prog 4: the
// Boarding Permit and the Map of Wajaom Woodlands, Prog 5. Completed: 10071 (replaceDefault).
// Kept as the Lua has it: the reward table is empty; the 10068 finish ignores quest:complete's result; the available
// 10063's option 1 (RoV) completes the quest straight from available; an option-3 selection without the gil begins the
// quest (available section) and changes nothing else; Prog 4 has no Faursel answer.
// ENABLE_TOAU is read as Lua has it (Settings.MainNumber: 1.25 is not 1). Faursel has no script; his DefaultActions
// event is 10065 (no quest answer: ENABLE_TOAU off, Prog 4).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheRoadToAhtUrhgan : QuestScript
    {
        public TheRoadToAhtUrhgan() : base(QuestLog.Jeuno, "THE_ROAD_TO_AHT_URHGAN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Road_to_Aht_Urhgan";

        // xi.teleport.id.WAJAOM_LEYPOINT
        const int TeleportWajaomLeypoint = 49;

        static readonly (int Item, int Qty)[] BeginnerList =
        {
            (Item.DamselflyWorm, 1),
            (Item.MagickedSkull, 1),
            (Item.CrabApron, 1),
            (Item.BloodyRobe, 1),
            (Item.CupOfDhalmelSaliva, 1),
            (Item.WildRabbitTail, 1),
        };

        static readonly (int Item, int Qty)[] IntermediateList =
        {
            (Item.JadeCryptex, 1),
            (Item.SilverEngraving, 1),
            (Item.ThirteenKnotQuipu, 1),
        };

        static readonly (int Item, int Qty)[] ChipList =
        {
            (Item.CarmineChip, 1),
            (Item.CyanChip, 1),
            (Item.GrayChip, 1),
        };

        static readonly int[] AdvancedSingleList =
        {
            Item.DavoiCofferKey,
            Item.BeadeauxCofferKey,
            Item.OztrojaCofferKey,
            Item.UggalepihCofferKey,
            Item.RancorDenCofferKey,
            Item.QuicksandCofferKey,
            Item.GrottoCofferKey,
            Item.WarriorsTestimony,
            Item.MonksTestimony,
            Item.WhiteMagesTestimony,
            Item.BlackMagesTestimony,
            Item.RedMagesTestimony,
            Item.ThiefsTestimony,
            Item.PaladinsTestimony,
            Item.DarkKnightsTestimony,
            Item.BeastmastersTestimony,
            Item.BardsTestimony,
            Item.RangersTestimony,
            Item.SamuraisTestimony,
            Item.NinjasTestimony,
            Item.DragoonsTestimony,
            Item.SummonersTestimony,
            Item.BlueMagesTestimony,
            Item.CorsairsTestimony,
            Item.PuppetmastersTestimony,
            Item.DancersTestimony,
            Item.ScholarsTestimony,
        };

        // npcUtil.tradeSetInList(trade, itemList): npcUtil.tradeHasExactly of each item in turn, true at the first match
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

        static int AtInescapableBinds(IQuestPlayerView player) =>
            player.CurrentMission(MissionLog.Rov) == Mission.Rov.InescapableBinds ? 1 : 0;

        static void HandleEventUpdate(IQuestContext q)
        {
            if (q.Option == 10)
            {
                q.Player.UpdateEvent(Item.DamselflyWorm, Item.MagickedSkull, Item.CrabApron, Item.BloodyRobe, Item.CupOfDhalmelSaliva, Item.WildRabbitTail, 0, 0);
            }
            else if (q.Option == 12)
            {
                q.Player.UpdateEvent(Item.JadeCryptex, Item.SilverEngraving, Item.ThirteenKnotQuipu, 0, 0, 0, 0, 0);
            }
            else if (q.Option == 13)
            {
                q.Player.UpdateEvent(Item.CarmineChip, Item.CyanChip, Item.GrayChip, 0, 0, 0, 0, 0);
            }
            else if (q.Option == 14)
            {
                q.Player.UpdateEvent(1, 1, 1, 1, 1, 1, q.Player.Gil, 1);
            }
        }

        void HandleSelectionEventFinish(IQuestContext q)
        {
            // Where's Tenzen?
            if (q.Option == 1 &&
                q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.InescapableBinds)
            {
                if (q.Quest.Complete())
                {
                    q.Player.GiveKeyItem(KeyItem.BoardingPermit);
                    q.Player.CompleteMission(MissionLog.Rov, Mission.Rov.InescapableBinds, MissionLog.Rov, Mission.Rov.DesertWinds);
                }
            }
            // Let me think about it.
            else if (q.Option == 2)
            {
                q.Quest.SetStep(1);
            }
            // Purchase Boarding Permit with Gil
            else if (q.Option == 3)
            {
                if (q.Player.Gil >= 500000)
                {
                    q.Player.DelGil(500000);
                    q.Text.Special(Text.LowerJeuno.PAY_FAURSEL, 500000);
                    q.Quest.SetStep(2);
                    q.Quest.SetMustZone();
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // quest.reward = {}: an empty table, no b.Reward

            b.State(QuestStatus.Available, when: q => q.Settings.MainEquals("ENABLE_TOAU", 1))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Faursel).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(10062);
                    }

                    return QuestReply.ProgressEvent(10063, 0, 0, 0, 0, 0, AtInescapableBinds(q.Player));
                })
                .OnEventUpdate(Zone.LowerJeuno, 10063, HandleEventUpdate)
                // Hearing Faursel out grants nothing. The quest is only added to the log when 10063 ends on a selection.
                .OnEventFinish(Zone.LowerJeuno, 10062, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10063, q =>
                {
                    if (q.Option == 2 || q.Option == 3)
                    {
                        q.Quest.Begin();
                    }

                    HandleSelectionEventFinish(q);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Faursel)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 &&
                            (
                                q.Trade.Matches(BeginnerList) ||
                                q.Trade.Matches(IntermediateList) ||
                                q.Trade.Matches(ChipList) ||
                                TradeSetInList(q.Trade, AdvancedSingleList)
                            ))
                        {
                            return QuestReply.ProgressEvent(10069);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;
                        bool timePassed = q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay && !q.Quest.MustZone;

                        // No decision yet. Faursel reads the lists again.
                        // Prog 0: accepted back when 10062 granted the quest.
                        if (questProgress == 0 || questProgress == 1)
                        {
                            return QuestReply.ProgressEvent(10064, 0, 0, 0, 0, 0, AtInescapableBinds(q.Player));
                        }
                        // Purchased the Boarding Permit
                        else if (questProgress == 2)
                        {
                            if (timePassed)
                            {
                                // Capture: the parameters 10067 starts with. Zeros end the cutscene before the fade to black.
                                return QuestReply.ProgressEvent(10067, 15, 1, 3, -1, 541, 540, 0, 99);
                            }
                            else
                            {
                                return QuestReply.Event(10066); // No refunds. Come back tomorrow.
                            }
                        }
                        // Traded Items for Boarding Permit
                        else if (questProgress == 3)
                        {
                            if (timePassed)
                            {
                                return QuestReply.ProgressEvent(10070);
                            }
                            else
                            {
                                return QuestReply.Event(10072); // Come back tomorrow.
                            }
                        }
                        // Purchased Permit, and has returned from Wajaom Woodlands
                        else if (questProgress == 5)
                        {
                            return QuestReply.ProgressEvent(10068);
                        }
                        return null;
                    })
                .OnEventUpdate(Zone.LowerJeuno, 10064, HandleEventUpdate)
                // Capture: the reply to the option 30 update. The cutscene shows the key items and holds the black screen for the warp.
                .OnEventUpdate(Zone.LowerJeuno, 10067, q =>
                {
                    if (q.Option == 30)
                    {
                        q.Player.UpdateEvent(79, 1, 0, 34, 910, 540, 99, 99);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10064, HandleSelectionEventFinish)
                .OnEventFinish(Zone.LowerJeuno, 10067, q =>
                {
                    q.Quest.SetStep(4);
                    q.Player.TeleportTo(TeleportWajaomLeypoint);
                })
                .OnEventFinish(Zone.LowerJeuno, 10068, q =>
                {
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.LowerJeuno, 10069, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetMustZone();
                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                })
                .OnEventFinish(Zone.LowerJeuno, 10070, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.GiveKeyItem(KeyItem.BoardingPermit);
                    }
                })
                // Player won't see these messages due to teleporting at the end of the cutscene if awarded then. Display after they zone in.
                // NOTE: Prog value of 4 is set immediately before teleporting the player.
                .OnAfterZoneIn(Zone.WajaomWoodlands, q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        q.Player.GiveKeyItem(KeyItem.BoardingPermit);
                        q.Player.GiveKeyItem(KeyItem.MapOfWajaomWoodlands);
                        q.Quest.SetStep(5);
                    }
                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Faursel).OnTrigger(QuestReply.Event(10071).ReplaceDefault());
        }
    }
}
