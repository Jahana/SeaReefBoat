// As Thick as Thieves (Log ID 2, Quest ID 70), a line-for-line port of
// scripts/quests/windurst/THF_AF2_As_Thick_as_Thieves.lua (quest rework B3b). Nanaa Mihgo : !pos 62 -4 240 241; the
// Sauromugue Champaign tower qm2-qm7; Sniggnix (Lower Jeuno); Dangruf Wadi qm3 (Saltvix), qm4 (Grasswix), qm5
// (Eggblix); North Gustaberg qm2. Once The Tenshodo Showdown is completed, a THF at AF2_QUEST_LEVEL: Nanaa Mihgo's 504,
// option 1, begins it (the Gang Whereabouts Note and two forged envelopes). The first envelope: the Grapnel traded alone
// to the tower qm2 with nothing equipped: 2 (taken, the first envelope signed); the grapnel from Climbpix Highrise,
// popped at the day's tower qm (its local var Option, set by the zone at random) when no grapnel is held. The second:
// Sniggnix's 10024 (option 1: Prog 1), then the three goblins' dice games in Dangruf Wadi (Prog 1-3: the item traded
// alone, a random win (Prog + 1 on option 0) or loss), the North Gustaberg qm2 at Prog 4 (Gambilox Wanderling popped;
// his death: Prog 5) and at Prog 5 (200: the Regal Die, Prog 6), the die traded to Sniggnix at Prog 6 (10026: taken,
// the second envelope signed, Prog 7). Both signed: Nanaa Mihgo's 508 completes it (fame 20, the Rogue's Bonnet; the
// three key items taken). Kept as the Lua file has it: 508 sets the player local var 'Quest[2][71]mustZone', which
// nothing reads (Hitting the Marquisate's offer reads the char var of that name, which nothing sets); Sniggnix draws
// two random numbers on every talk, used or not; qm3's trade at Prog > 1 sends BEAT_SALTVIX and returns nil.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class AsThickAsThieves : QuestScript
    {
        public AsThickAsThieves() : base(QuestLog.Windurst, "AS_THICK_AS_THIEVES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/THF_AF2_As_Thick_as_Thieves";

        static bool IsNaked(IQuestContext q)
        {
            for (int i = Slot.Main; i <= Slot.Back; i++)
            {
                if (q.Player.EquipId(i) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        static QuestReply TowerIncorrectTrade(IQuestContext q)
        {
            if (q.Trade.HasExactly(Item.Grapnel))
            {
                // quest:messageSpecial(sauromugueID.text.THF_AF_WALL_OFFSET + 3, 0, xi.item.GRAPNEL)
                return QuestReply.MessageSpecial(q.Text.Id(Text.SauromugueChampaign.THF_AF_WALL_OFFSET) + 3, 0, Item.Grapnel);
            }

            return null;
        }

        static QuestReply TowerOnTrigger(IQuestContext q)
        {
            if (!q.Player.HasKeyItem(KeyItem.FirstSignedForgedEnvelope))
            {
                // quest:getLocalVar(npc, 'Option'): npc:getLocalVar('Quest[2][70]Option')
                if (q.Npc.LocalVar("Quest[2][70]Option") == 1 &&
                    !q.Player.FindItem(Item.Grapnel) &&
                    // npcUtil.popFromQM(player, npc, sauromugueID.mob.CLIMBPIX_HIGHRISE, { radius = 1, hide = 0 }): no claim
                    // given, so popFromQM's default (true)
                    q.Player.PopFromQm(Mob.SauromugueChampaign.CLIMBPIX_HIGHRISE, true, 0, 1))
                {
                    return QuestReply.MessageSpecial(Text.SauromugueChampaign.THF_AF_MOB);
                }
                else
                {
                    // It is impossible to climb this wall with your bare hands.
                    return QuestReply.MessageSpecial(Text.SauromugueChampaign.THF_AF_WALL_OFFSET);
                }
            }
            else
            {
                // There is no longer any need to climb the tower.
                // quest:messageSpecial(sauromugueID.text.THF_AF_WALL_OFFSET + 1)
                return QuestReply.MessageSpecial(q.Text.Id(Text.SauromugueChampaign.THF_AF_WALL_OFFSET) + 1);
            }
        }

        static QuestReply HandleDangrufMinigame(IQuestContext q, int winEventId, int loseEventId, int itemPlaced)
        {
            int rand1 = q.Player.RandomInt(1, 999);
            int rand2 = q.Player.RandomInt(1, 999);

            if (rand1 > rand2)
            {
                q.Text.Special(Text.DangrufWadi.YOU_PLACE_ITEM, 0, itemPlaced);
                return QuestReply.ProgressEvent(winEventId, Item.RegalDie, 0, rand1, rand2);
            }
            else
            {
                q.Text.Special(Text.DangrufWadi.YOU_PLACE_ITEM, 0, itemPlaced);
                return QuestReply.ProgressEvent(loseEventId, 0, 0, rand1, rand2);
            }
        }

        static void LostMinigameEvent(IQuestContext q)
        {
            q.Trade.Confirm();

            // Only Saltvix has an alternate CS progression on repeating attempts
            if (q.Csid == 139 || q.Csid == 145)
            {
                q.Quest.SetVar("failedGame", 1);
            }

            if (q.Option == 1)
            {
                q.Text.Special(Text.DangrufWadi.GOBLIN_BEAT_YOU);
            }
            else if (q.Option == 2)
            {
                q.Text.Special(Text.DangrufWadi.YOU_GAVE_UP);
            }
        }

        static void WonMinigameEvent(IQuestContext q)
        {
            q.Trade.Confirm();

            if (q.Option == 0)
            {
                if (q.Csid == 136 || q.Csid == 142)
                {
                    q.Quest.SetVar("failedGame", 0);
                }

                q.Text.Special(Text.DangrufWadi.YOU_BEAT_GOBLIN);
                q.Quest.SetStep(q.Quest.Step + 1);
            }
            else if (q.Option == 2)
            {
                if (q.Csid == 136 || q.Csid == 142)
                {
                    q.Quest.SetVar("failedGame", 1);
                }

                q.Text.Special(Text.DangrufWadi.YOU_GAVE_UP);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.RoguesBonnet });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "THE_TENSHODO_SHOWDOWN") &&
                    q.Player.MainJob == Job.Thf &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(
                    QuestReply.ProgressEvent(504, 0, KeyItem.SignedEnvelope, KeyItem.TenshodoEnvelope))
                .OnEventFinish(Zone.WindurstWoods, 504, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.GangWhereaboutsNote);
                        q.Player.GiveKeyItem(KeyItem.FirstForgedEnvelope);
                        q.Player.GiveKeyItem(KeyItem.SecondForgedEnvelope);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.DangrufWadi, Npc.DangrufWadi.qm3).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.ChunkOfRockSalt))
                    {
                        int questProgress = q.Quest.Step;
                        int eventOffset = q.Quest.Var("failedGame") == 0 ? 136 : 142;

                        if (questProgress == 1)
                        {
                            return HandleDangrufMinigame(q, eventOffset, eventOffset + 3, Item.ChunkOfRockSalt);
                        }
                        else if (questProgress > 1)
                        {
                            q.Text.Special(Text.DangrufWadi.BEAT_SALTVIX);
                        }
                    }

                    return null;
                })
                .Npc(Zone.DangrufWadi, Npc.DangrufWadi.qm4).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.ClumpOfGausebitWildgrass))
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 2)
                        {
                            return HandleDangrufMinigame(q, 137, 140, Item.ClumpOfGausebitWildgrass);
                        }
                        else if (questProgress < 2)
                        {
                            return QuestReply.MessageSpecial(Text.DangrufWadi.DONT_WASTE_TIME);
                        }
                        else if (questProgress > 2)
                        {
                            return QuestReply.MessageSpecial(Text.DangrufWadi.BEAT_GRASSWIX);
                        }
                    }

                    return null;
                })
                .Npc(Zone.DangrufWadi, Npc.DangrufWadi.qm5).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.LizardEgg))
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 3)
                        {
                            return HandleDangrufMinigame(q, 138, 141, Item.LizardEgg);
                        }
                        else if (questProgress < 3)
                        {
                            return QuestReply.MessageSpecial(Text.DangrufWadi.JUST_WONT_DO);
                        }
                        else if (questProgress > 3)
                        {
                            return QuestReply.MessageSpecial(Text.DangrufWadi.BEAT_EGGBLIX);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.DangrufWadi, 136, WonMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 137, WonMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 138, WonMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 139, LostMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 140, LostMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 141, LostMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 142, WonMinigameEvent)
                .OnEventFinish(Zone.DangrufWadi, 145, LostMinigameEvent)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Sniggnix)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.RegalDie) &&
                            q.Quest.Step == 6)
                        {
                            return QuestReply.ProgressEvent(10026, 0, Item.RegalDie, q.Player.RandomInt(1, 700));
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        // NOTE: 'Prog' questVar is only used for the gambling phase of this quest.  All other tracking utilizes
                        // key items obtained.
                        int questProgress = q.Quest.Step;
                        int rand1 = q.Player.RandomInt(1, 999);
                        int rand2 = q.Player.RandomInt(1, 999);

                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(10024, 0, Item.RegalDie, rand1, rand2);
                        }
                        else if (questProgress >= 1 &&
                            questProgress <= 5)
                        {
                            return QuestReply.ProgressEvent(10025, 0, Item.RegalDie, rand1, rand2);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10023);
                        }
                    })
                .OnEventFinish(Zone.LowerJeuno, 10024, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10026, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.SecondForgedEnvelope);
                    q.Player.GiveKeyItem(KeyItem.SecondSignedForgedEnvelope);
                    q.Quest.SetStep(7);
                })
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.GambiloxWanderling).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        q.Quest.SetStep(5);
                    }
                })
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.qm2).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 4)
                    {
                        q.Player.PopFromQm(Mob.NorthGustaberg.GAMBILOX_WANDERLING, 0);
                        return QuestReply.MessageSpecial(Text.NorthGustaberg.SENSE_EVIL_PRESENCE);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.ProgressEvent(200, Item.RegalDie);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthGustaberg, 200, q =>
                {
                    if (q.Player.GiveItem(Item.RegalDie))
                    {
                        q.Quest.SetStep(6);
                    }
                })
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.qm2)
                    .OnTrade(q =>
                    {
                        if (!q.Player.HasKeyItem(KeyItem.FirstSignedForgedEnvelope) &&
                            q.Trade.HasExactly(Item.Grapnel))
                        {
                            if (IsNaked(q))
                            {
                                return QuestReply.ProgressEvent(2, 0, Item.Grapnel);
                            }
                            else
                            {
                                // player:messageSpecial(sauromugueID.text.THF_AF_WALL_OFFSET + 2, 0, xi.item.GRAPNEL)
                                q.Text.Special(q.Text.Id(Text.SauromugueChampaign.THF_AF_WALL_OFFSET) + 2, 0, Item.Grapnel);
                            }
                        }

                        return null;
                    })
                    .OnTrigger(TowerOnTrigger)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.qm3).OnTrade(TowerIncorrectTrade).OnTrigger(TowerOnTrigger)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.qm4).OnTrade(TowerIncorrectTrade).OnTrigger(TowerOnTrigger)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.qm5).OnTrade(TowerIncorrectTrade).OnTrigger(TowerOnTrigger)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.qm6).OnTrade(TowerIncorrectTrade).OnTrigger(TowerOnTrigger)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.qm7).OnTrade(TowerIncorrectTrade).OnTrigger(TowerOnTrigger)
                .OnEventFinish(Zone.SauromugueChampaign, 2, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.FirstForgedEnvelope);
                    q.Player.GiveKeyItem(KeyItem.FirstSignedForgedEnvelope);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(QuestReply.ProgressEvent(506))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(QuestReply.ProgressEvent(507, 0, Item.Grapnel))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FirstSignedForgedEnvelope) &&
                        q.Player.HasKeyItem(KeyItem.SecondSignedForgedEnvelope))
                    {
                        return QuestReply.ProgressEvent(508);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(505, 0, KeyItem.GangWhereaboutsNote);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 508, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.GangWhereaboutsNote);
                        q.Player.DelKeyItem(KeyItem.FirstSignedForgedEnvelope);
                        q.Player.DelKeyItem(KeyItem.SecondSignedForgedEnvelope);
                        q.Player.SetLocalVar("Quest[2][71]mustZone", 1);
                    }
                });
        }
    }
}
