// Enveloped in Darkness (Log ID 0, Quest ID 85, RDM AF2), a line-for-line port of scripts/quests/sandoria/RDM_AF2_Enveloped_in_Darkness.lua
// (quest rework B3b, San d'Oria group 4). Curilla : !pos 27 0.1 0.1 233; Pagisalis : !pos 97 0.1 113 231. Offered to an
// RDM of AF2_QUEST_LEVEL with The Crimson Trial completed: Curilla's 94 (option 1: begun, the Old Pocket Watch; else Prog
// 1), then 95 (option 1: begun, the watch); Sharzalion's 68 while available. Accepted: Curilla's 92 at Prog <= 1, 101 at
// Prog 2 (Prog 3), 107 after; Sharzalion's 68, or 69 from Prog 2; Pagisalis's 48, or 58 from Prog 2; exactly a Square of
// Velvet Cloth to Pagisalis plays 37 (Prog 2, the velvet taken by tradeComplete, the watch deleted, the Old Boots). In
// the Crawlers' Nest, qm8 from Prog 2: with Time set, 5 once GetSystemTime() reaches it (completed: Warlock's Boots, fame
// +20), else EQUIPMENT_NOT_PURIFIED; without Time, 4 with the Crawler Blood and the Old Boots (option 1: Time = now + 30,
// both deleted, YOU_BURY_THE), else SOMEONE_HAS_BEEN_DIGGING_HERE. The Crawler Blood comes from the Crawlers' Nest
// Treasure Chest's own script, which reads this quest's Prog and Time vars (the container Quest[0][85] and the var names
// stay). Sharzalion's and Curilla's scripts and Borghertz's Vermillion Hands read the status (stay).
// Kept as the Lua has it: 5's finish ignores quest:complete's result (a full bag leaves it accepted); Pagisalis takes the
// velvet again at any Prog while accepted; Prog 1 means "declined once" and stays 1 after the quest begins.
// Its era module is Modules/Era/Sandoria/EnvelopedInDarkness.cs (the purification Time to JST midnight); the state
// numbers here are the Lua's section numbers, which the module patches.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class EnvelopedInDarkness : QuestScript
    {
        public EnvelopedInDarkness() : base(QuestLog.Sandoria, "ENVELOPED_IN_DARKNESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/RDM_AF2_Enveloped_in_Darkness";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.WarlocksBoots, Fame = 20, FameArea = FameArea.Sandoria });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "THE_CRIMSON_TRIAL") == QuestStatus.Completed &&
                    q.Player.MainJob == Job.Rdm &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        // first starting event
                        return QuestReply.ProgressEvent(94);
                    }
                    else
                    {
                        // next starting event if originally declined
                        return QuestReply.ProgressEvent(95);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 94, q =>
                {
                    if (q.Option == 1)
                    {
                        // accept quest
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.OldPocketWatch);
                    }
                    else
                    {
                        // you rejected the quest when first offered
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 95, q =>
                {
                    if (q.Option == 1)
                    {
                        // accept quest
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.OldPocketWatch);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sharzalion).OnTrigger(QuestReply.Event(68));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(q =>
                {
                    if (q.Quest.Step <= 1)
                    {
                        // church reminder (Curilla has a second possible reminder, 93, seemingly time triggered)
                        return QuestReply.Event(92);
                    }
                    else if (q.Quest.Step == 2)
                    {
                        // post church ghost event (optional)
                        return QuestReply.ProgressEvent(101);
                    }
                    else
                    {
                        // crawler blood reminder
                        return QuestReply.Event(107);
                    }
                })
                // set the optional event as seen
                .OnEventFinish(Zone.ChateauDoraguille, 101, q => q.Quest.SetStep(3))
                .Npc(Zone.CrawlersNest, Npc.CrawlersNest.qm8).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 2)
                    {
                        if (q.Quest.Var("Time") > 0)
                        {
                            if (q.Quest.Var("Time") <= q.Player.SystemTime)
                            {
                                // quest complete
                                return QuestReply.ProgressEvent(5);
                            }
                            else
                            {
                                // purification incomplete
                                return QuestReply.MessageSpecial(Text.CrawlersNest.EQUIPMENT_NOT_PURIFIED);
                            }
                        }
                        else
                        {
                            if (q.Player.HasKeyItem(KeyItem.CrawlerBlood) &&
                                q.Player.HasKeyItem(KeyItem.OldBoots))
                            {
                                // lose the key items, start the boot purification
                                return QuestReply.ProgressEvent(4);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.CrawlersNest.SOMEONE_HAS_BEEN_DIGGING_HERE);
                            }
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.CrawlersNest, 4, q =>
                {
                    if (q.Option == 1)
                    {
                        // set the purification time
                        q.Quest.SetVar("Time", q.Player.SystemTime + 30);

                        // delete the key items
                        q.Player.DelKeyItem(KeyItem.CrawlerBlood);
                        q.Player.DelKeyItem(KeyItem.OldBoots);

                        // the message when accepting to bury the boots and the blood
                        q.Text.Special(Text.CrawlersNest.YOU_BURY_THE, KeyItem.OldBoots, KeyItem.CrawlerBlood);
                    }
                })
                // quest:complete's result is not read
                .OnEventFinish(Zone.CrawlersNest, 5, q => { q.Quest.Complete(); })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Pagisalis)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.SQUARE_OF_VELVET_CLOTH)
                        if (q.Trade.HasExactly(Item.SquareOfVelvetCloth))
                        {
                            return QuestReply.ProgressEvent(37);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step >= 2)
                        {
                            // after seeing the ghost
                            return QuestReply.Event(58);
                        }
                        else
                        {
                            return QuestReply.Event(48);
                        }
                    })
                .OnEventFinish(Zone.NorthernSanDoria, 37, q =>
                {
                    // saw the ghost
                    q.Quest.SetStep(2);
                    q.Trade.Complete();
                    q.Player.DelKeyItem(KeyItem.OldPocketWatch);
                    q.Player.GiveKeyItem(KeyItem.OldBoots);
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sharzalion).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 2)
                    {
                        // after seeing the ghost
                        return QuestReply.Event(69);
                    }
                    else
                    {
                        return QuestReply.Event(68);
                    }
                });
        }
    }
}
