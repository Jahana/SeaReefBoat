// Chasing Quotas (Log ID 0, Quest ID 95, DRG AF2), a line-for-line port of scripts/quests/sandoria/Chasing_Quotas.lua
// (quest rework B3b, San d'Oria group 8). Ceraulian : !pos 0 -8 -122 232; Miaux : !pos -169.127 2.999 158.677 231; Ardea :
// !pos -198 -6 -69 235; Esca : !pos -624.231 -51.499 278.369 100; qm2 (???) : !pos 145.373 16.462 -548.560 105.
// Offered after A Craftsman's Work to a DRG at AF2_QUEST_LEVEL: Ceraulian's 18 (option 0: Declined 1; cancelled: nothing;
// else begun), then 14 while declined (option 1: begun). Accepted: the Gold Hairpin to Ceraulian (17: Prog 1, a 60 s
// Wait); Ceraulian's 7 once the wait passes (Prog 2); Miaux's 67 (Shiny Earring, Prog 3); Ardea's 264 (Prog 4); Esca's 137
// (the earring taken, Prog 5); the qm2 in Batallia Downs at Prog 5 pops Sturmtiger (SENSE_AN_EVIL_PRESENCE as speakerless
// text, else SOMEONE_DUG); Sturmtiger's death at Prog 5 sets Killed; the qm2 then gives Ranchuriome's Legacy (Prog 6,
// Killed 0); Ceraulian's 15 (completed: Drachen Brais, fame +20; the legacy taken). Reminders: Ceraulian 13 / 3 / 8 / 6 /
// 9, Miaux 68 / 66, Ardea 265, Esca 138. Outside references are status reads only (Ceraulian's and Rahal's scripts,
// Borghertz's Dragon Hands, Vingijard, the RoE record). Its era module is Modules/Era/Sandoria/ChasingQuotas.cs (the
// Wait to JST midnight); the state numbers here are the Lua's section numbers, which the module patches.
// Kept as the Lua has it: 18's option 0 declines and any other option but a cancel accepts; the qm2 answers on every
// talk while accepted (SOMEONE_DUG off Prog 5); the reminders return nothing at the other Progs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ChasingQuotas : QuestScript
    {
        public ChasingQuotas() : base(QuestLog.Sandoria, "CHASING_QUOTAS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Chasing_Quotas";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.DrachenBrais });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Sandoria, "A_CRAFTSMANS_WORK"))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ceraulian).OnTrigger(q =>
                {
                    if (q.Player.MainJob != Job.Drg ||
                        q.Player.MainLevel < q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                    {
                        return null;
                    }

                    if (q.Quest.Var("Declined") == 0)
                    {
                        return QuestReply.ProgressEvent(18);
                    }

                    return QuestReply.ProgressEvent(14);
                })
                .OnEventFinish(Zone.PortSanDoria, 14, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetVar("Declined", 0);
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 18, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Declined", 1);
                    }
                    else if (q.Option != Utils.EventCancelledOption)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Ardea).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 3)
                    {
                        return QuestReply.ProgressEvent(264);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.Event(265); // Reminder: return the earring to Esca.
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarkets, 264, q => q.Quest.SetStep(4))
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.qm2).OnTrigger(q =>
                {
                    if (q.Quest.Var("Killed") == 1)
                    {
                        q.Quest.SetStep(6);
                        q.Quest.SetVar("Killed", 0);
                        return QuestReply.KeyItem(KeyItem.RanchuriomesLegacy);
                    }

                    // Retail sends both lines as speakerless npc text (flag unset, type 6).
                    if (q.Quest.Step == 5 &&
                        q.Player.PopFromQm(Mob.BatalliaDowns.STURMTIGER, claim: true, hide: 0))
                    {
                        q.Text.MessageText(Text.BatalliaDowns.SENSE_AN_EVIL_PRESENCE, false, 6);
                    }
                    else
                    {
                        q.Text.MessageText(Text.BatalliaDowns.SOMEONE_DUG, false, 6);
                    }

                    return QuestReply.NoAction();
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.Sturmtiger).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        q.Quest.SetVar("Killed", 1);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Miaux).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 2)
                    {
                        return QuestReply.ProgressEvent(67);
                    }
                    else if (progress == 3 || progress == 4)
                    {
                        return QuestReply.Event(68);
                    }
                    else if (progress == 5 || progress == 6)
                    {
                        return QuestReply.Event(66);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 67, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.ShinyEarring);
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ceraulian)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 0 &&
                            q.Trade.Matches((Item.GoldHairpin, 1)))
                        {
                            return QuestReply.ProgressEvent(17);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 0)
                        {
                            return QuestReply.Event(13); // Reminder: bring the gold hairpin.
                        }
                        else if (progress == 1)
                        {
                            if (q.Player.SystemTime < q.Quest.Var("Wait"))
                            {
                                return QuestReply.Event(3); // Nothing new until the wait passes.
                            }

                            return QuestReply.ProgressEvent(7);
                        }
                        else if (progress == 2)
                        {
                            return QuestReply.Event(8); // Reminder: investigate the mugging.
                        }
                        else if (progress == 3)
                        {
                            return QuestReply.Event(6); // Optional. Ceraulian sizes up the earring.
                        }
                        else if (progress == 4 || progress == 5)
                        {
                            return QuestReply.Event(9); // Nothing new until the armor is recovered.
                        }
                        else if (progress == 6)
                        {
                            return QuestReply.ProgressEvent(15);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.PortSanDoria, 7, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.PortSanDoria, 15, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.RanchuriomesLegacy);
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 17, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetStep(1);
                    q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute wait time.
                })
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Esca).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(137);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.Event(138); // Reminder: the armor is buried past the Eldieme Necropolis.
                    }
                    return null;
                })
                .OnEventFinish(Zone.WestRonfaure, 137, q =>
                {
                    q.Player.DelKeyItem(KeyItem.ShinyEarring);
                    q.Quest.SetStep(5);
                });
        }
    }
}
