// Tutorial Mini-Quest (hidden quest Tutorial), ported from scripts/quests/hiddenQuests/Tutorial_Quest.lua (quest rework
// B3b hidden quests). The three tutors: Gulldago (Bastok Markets), Alaune (Southern San d'Oria), Selele (Windurst
// Woods). One section per Prog, 0 to 12, each answering at the three tutors (their events differ by nation; the table
// below): 0: PE intro (finish: Prog 1). 1: PE get the Signet (Prog 2). 2: the Signet effect: PE (finish: 6 Meat Jerky,
// then Prog 3), else E. 3: a food effect: PE (Prog 4), else E. 4: a weapon skill learnt (a combat skill, Hand-to-Hand
// to Staff, at 5 or more): PE (100 x GIL_RATE gil, Prog 5), else E. 5: the Memorandoll: PE (the nation's three items,
// then Prog 6), else E. 6: the auction house checked (Option 1): PE (the Conquest Promotion Voucher, Prog 7, Option 0),
// else E; any Auction Counter of the eleven zones: Option 1, the auction menu, noAction. 7: level 5: PE (the Raising
// Earring, then Prog 8), else E. 8: the nation's trust permit: PE (the Warp Ring, then Prog 9), else E. 9:
// TutorialBypass 2 or more: PE (nation) (12 Saltena, then Prog 10, TutorialBypass 0), else E (nation); the survival
// guides of the city (TutorialBypass 0 -> 1) and of the outpost (1 -> 2): the guide's own onTrigger, nil.
// 10: level 18: PE (nation) (the Copper A.M.A.N. Voucher, then Prog 11), else E. 11: a support job: PE
// (12 Free Chocopasses and the Echad Ring, then Prog 12), else E (nation). 12: E done. Kept as the Lua has it: the
// reward finishes give the item first and move Prog only when it fit; step 4 checks a combat skill's level, not a
// weapon skill (the Lua's note: retail does not either); the survival guide arms return nil after
// xi.survivalGuide.onTrigger has answered, so the lookup goes on to the guide's own script (which calls it
// again); step 6's Option is the quest's var (kept across zones); step 9's TutorialBypass is a plain char var (the
// survival guide reads it too: survival_guide.lua:82).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TutorialQuest : HiddenQuestScript
    {
        public TutorialQuest() : base("Tutorial") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Tutorial_Quest";

        // xi.effect
        private const int EffectFood = 251;
        private const int EffectSignet = 253;

        // xi.menuType.AUCTION
        private const int MenuTypeAuction = 3;

        // one tutor: the zone, the npc, the nation, and its events (Pass: the progressEvent; NotYet: the event)
        private sealed class Tutor
        {
            public ushort Zone;
            public string Npc;
            public int Nation;
            public int Intro;                                // step 0
            public int Signet;                               // step 1
            public int Step2NotYet, Step2Pass;
            public int Step3NotYet, Step3Pass;
            public int Step4NotYet, Step4Pass;
            public int Step5NotYet, Step5Pass;
            public (int Item, int Qty)[] Step5Items;
            public int Step6NotYet, Step6Pass;
            public int Step7NotYet, Step7Pass;
            public int Step8NotYet, Step8Pass;
            public int TrustPermit;
            public int Step9NotYet, Step9Pass;
            public int Step10NotYet, Step10Pass;
            public int Step11NotYet, Step11Pass;
            public int Done;                                 // step 12
        }

        private static readonly Tutor[] Tutors =
        {
            new Tutor
            {
                Zone = XI.QuestContent.Zone.BastokMarkets, Npc = XI.QuestContent.Npc.BastokMarkets.Gulldago,
                Nation = Nation.Bastok,
                Intro = 719, Signet = 697,
                Step2NotYet = 698, Step2Pass = 699, Step3NotYet = 700, Step3Pass = 701, Step4NotYet = 702,
                Step4Pass = 703,
                Step5NotYet = 704, Step5Pass = 705,
                Step5Items = new[] { (Item.LizardTail, 1), (Item.PotOfHoney, 1), (Item.FireCrystal, 1) },
                Step6NotYet = 706, Step6Pass = 707, Step7NotYet = 708, Step7Pass = 709, Step8NotYet = 710,
                Step8Pass = 711,
                TrustPermit = KeyItem.BastokTrustPermit,
                Step9NotYet = 712, Step9Pass = 713, Step10NotYet = 714, Step10Pass = 715, Step11NotYet = 716,
                Step11Pass = 717,
                Done = 718,
            },
            new Tutor
            {
                Zone = XI.QuestContent.Zone.SouthernSanDoria, Npc = XI.QuestContent.Npc.SouthernSanDoria.Alaune,
                Nation = Nation.Sandoria,
                Intro = 3662, Signet = 3640,
                Step2NotYet = 3641, Step2Pass = 3642, Step3NotYet = 3643, Step3Pass = 3644, Step4NotYet = 3645,
                Step4Pass = 3646,
                Step5NotYet = 3647, Step5Pass = 3648,
                Step5Items = new[] { (Item.ChunkOfRockSalt, 1), (Item.SliceOfHareMeat, 1), (Item.FireCrystal, 1) },
                Step6NotYet = 3649, Step6Pass = 3650, Step7NotYet = 3651, Step7Pass = 3652, Step8NotYet = 3653,
                Step8Pass = 3654,
                TrustPermit = KeyItem.SanDoriaTrustPermit,
                Step9NotYet = 3655, Step9Pass = 3656, Step10NotYet = 3657, Step10Pass = 3658, Step11NotYet = 3659,
                Step11Pass = 3660,
                Done = 3661,
            },
            new Tutor
            {
                Zone = XI.QuestContent.Zone.WindurstWoods, Npc = XI.QuestContent.Npc.WindurstWoods.Selele,
                Nation = Nation.Windurst,
                Intro = 1010, Signet = 988,
                Step2NotYet = 989, Step2Pass = 990, Step3NotYet = 991, Step3Pass = 992, Step4NotYet = 993,
                Step4Pass = 994,
                Step5NotYet = 995, Step5Pass = 996,
                Step5Items = new[] { (Item.BirdEgg, 1), (Item.PotOfHoney, 1), (Item.WaterCrystal, 1) },
                Step6NotYet = 997, Step6Pass = 998, Step7NotYet = 999, Step7Pass = 1000, Step8NotYet = 1001,
                Step8Pass = 1002,
                TrustPermit = KeyItem.WindurstTrustPermit,
                Step9NotYet = 1003, Step9Pass = 1004, Step10NotYet = 1005, Step10Pass = 1006, Step11NotYet = 1007,
                Step11Pass = 1008,
                Done = 1009,
            },
        };

        // step 6's Auction Counters, in the Lua's zone order
        private static readonly (ushort Zone, string Npc)[] AuctionCounters =
        {
            (Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.AuctionCounter),
            (Zone.BastokMarkets,      Npc.BastokMarkets.AuctionCounter),
            (Zone.BastokMines,        Npc.BastokMines.AuctionCounter),
            (Zone.EasternAdoulin,     Npc.EasternAdoulin.AuctionCounter),
            (Zone.LowerJeuno,         Npc.LowerJeuno.AuctionCounter),
            (Zone.PortSanDoria,       Npc.PortSanDoria.AuctionCounter),
            (Zone.RuludeGardens,      Npc.RuludeGardens.AuctionCounter),
            (Zone.SouthernSanDoria,   Npc.SouthernSanDoria.AuctionCounter),
            (Zone.WesternAdoulin,     Npc.WesternAdoulin.AuctionCounter),
            (Zone.WindurstWalls,      Npc.WindurstWalls.AuctionCounter),
            (Zone.WindurstWoods,      Npc.WindurstWoods.AuctionCounter),
        };

        // step 9's Survival Guides, in the Lua's zone order: the TutorialBypass value read, then written
        private static readonly (ushort Zone, string Npc, int From)[] SurvivalGuides =
        {
            (Zone.BastokMines,       Npc.BastokMines.SurvivalGuide,       0),
            (Zone.NorthGustaberg,    Npc.NorthGustaberg.SurvivalGuide,    1),
            (Zone.NorthernSanDoria,  Npc.NorthernSanDoria.SurvivalGuide,  0),
            (Zone.PortWindurst,      Npc.PortWindurst.SurvivalGuide,      0),
            (Zone.WestRonfaure,      Npc.WestRonfaure.SurvivalGuide,      1),
            (Zone.WestSarutabaruta,  Npc.WestSarutabaruta.SurvivalGuide,  1),
        };

        // hasLearntWeaponskill(player): a combat skill, HAND_TO_HAND (1) to STAFF (12), at 5 or more
        private static bool HasLearntWeaponskill(IQuestPlayerView player)
        {
            for (int skillId = Skill.HandToHand; skillId <= Skill.Staff; skillId++)
            {
                if (player.SkillLevel(skillId) >= 5)
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);

            // Step 0 -> Introduction.
            var step0 = b.AnyState(when: q => q.Quest.Step == 0);
            foreach (Tutor t in Tutors)
            {
                step0
                    .Npc(t.Zone, t.Npc).OnTrigger(QuestReply.ProgressEvent(t.Intro))
                    .OnEventFinish(t.Zone, t.Intro, q =>
                    {
                        q.Quest.SetStep(1);
                    });
            }

            // Step 1 -> Instruct to get Signet.
            var step1 = b.AnyState(when: q => q.Quest.Step == 1);
            foreach (Tutor t in Tutors)
            {
                step1
                    .Npc(t.Zone, t.Npc).OnTrigger(QuestReply.ProgressEvent(t.Signet))
                    .OnEventFinish(t.Zone, t.Signet, q =>
                    {
                        q.Quest.SetStep(2);
                    });
            }

            // Step 2 -> Talk to NPC after getting Signet (Reward: Meat Jerky x6.). Instruct to eat.
            var step2 = b.AnyState(when: q => q.Quest.Step == 2);
            foreach (Tutor t in Tutors)
            {
                step2
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.HasStatusEffect(EffectSignet))
                        {
                            return QuestReply.ProgressEvent(t.Step2Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step2NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step2Pass, q =>
                    {
                        if (q.Player.GiveItems((Item.StripOfMeatJerky, 6)))
                        {
                            q.Quest.SetStep(3);
                        }
                    });
            }

            // Step 3 -> Talk to NPC after having eaten. Instruct to learn a weaponskill.
            var step3 = b.AnyState(when: q => q.Quest.Step == 3);
            foreach (Tutor t in Tutors)
            {
                step3
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.HasStatusEffect(EffectFood))
                        {
                            return QuestReply.ProgressEvent(t.Step3Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step3NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step3Pass, q =>
                    {
                        q.Quest.SetStep(4);
                    });
            }

            // Step 4 -> Talk to NPC after learning a Weaponskill (Reward: 100 gil). Instruct to activate Records of
            // Eminence. NOTE: Retail does NOT check for weaponskill usage.
            var step4 = b.AnyState(when: q => q.Quest.Step == 4);
            foreach (Tutor t in Tutors)
            {
                step4
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (HasLearntWeaponskill(q.Player))
                        {
                            return QuestReply.ProgressEvent(t.Step4Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step4NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step4Pass, q =>
                    {
                        // npcUtil.giveCurrency(player, 'gil', 100 * xi.settings.main.GIL_RATE): the Lua number goes in
                        // unrounded (giveCurrency multiplies by GIL_RATE again; the engine rounds once, at addGil)
                        q.Player.GiveCurrency("gil", 100 * q.Settings.MainNumber("GIL_RATE"));
                        q.Quest.SetStep(5);
                    });
            }

            // Step 5 -> Talk to NPC after getting Memorandoll (Reward: Crystal and 2 items). Instruct to check the AH.
            var step5 = b.AnyState(when: q => q.Quest.Step == 5);
            foreach (Tutor t in Tutors)
            {
                step5
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.Memorandoll))
                        {
                            return QuestReply.ProgressEvent(t.Step5Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step5NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step5Pass, q =>
                    {
                        if (q.Player.GiveItems(t.Step5Items))
                        {
                            q.Quest.SetStep(6);
                        }
                    });
            }

            // Step 6 -> Talk to NPC after checking the AH (Reward: Conquest promotion voucher). Instruct to reach
            // level 5.
            var step6 = b.AnyState(when: q => q.Quest.Step == 6);
            foreach (var (zone, npc) in AuctionCounters)
            {
                step6.Npc(zone, npc).OnTrigger(q =>
                {
                    q.Quest.SetVar("Option", 1);
                    q.Player.SendMenu(MenuTypeAuction);
                    return QuestReply.NoAction();
                });
            }

            foreach (Tutor t in Tutors)
            {
                step6
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Quest.Var("Option") == 1)
                        {
                            return QuestReply.ProgressEvent(t.Step6Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step6NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step6Pass, q =>
                    {
                        q.Player.GiveKeyItem(KeyItem.ConquestPromotionVoucher);
                        q.Quest.SetStep(7);
                        q.Quest.SetVar("Option", 0);
                    });
            }

            // Step 7 -> Talk to NPC after reaching level 5 (Reward: Raising Earring). Instruct to get nation's
            // trust permit.
            var step7 = b.AnyState(when: q => q.Quest.Step == 7);
            foreach (Tutor t in Tutors)
            {
                step7
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.MainLevel >= 5)
                        {
                            return QuestReply.ProgressEvent(t.Step7Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step7NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step7Pass, q =>
                    {
                        if (q.Player.GiveItem(Item.RaisingEarring))
                        {
                            q.Quest.SetStep(8);
                        }
                    });
            }

            // Step 8 -> Talk to NPC after getting nation trust permit (Reward: Warp Ring). Instruct to use
            // Survival guide.
            var step8 = b.AnyState(when: q => q.Quest.Step == 8);
            foreach (Tutor t in Tutors)
            {
                step8
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(t.TrustPermit))
                        {
                            return QuestReply.ProgressEvent(t.Step8Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step8NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step8Pass, q =>
                    {
                        if (q.Player.GiveItem(Item.WarpRing))
                        {
                            q.Quest.SetStep(9);
                        }
                    });
            }

            // Step 9 -> Talk to NPC after checking Survival guides in order (Reward: Saltena x12). Instruct to
            // reach level 18.
            // NOTE: Retail only cares for checking the survival guide in city and in outpost.
            var step9 = b.AnyState(when: q => q.Quest.Step == 9);
            foreach (Tutor t in Tutors)
            {
                step9
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.CharVar("TutorialBypass") >= 2)
                        {
                            return QuestReply.ProgressEvent(t.Step9Pass, t.Nation);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step9NotYet, t.Nation);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step9Pass, q =>
                    {
                        if (q.Player.GiveItems((Item.Saltena, 12)))
                        {
                            q.Quest.SetStep(10);
                            q.Player.SetCharVar("TutorialBypass", 0);
                        }
                    });
            }

            foreach (var (zone, npc, from) in SurvivalGuides)
            {
                step9.Npc(zone, npc).OnTrigger(q =>
                {
                    if (q.Player.CharVar("TutorialBypass") == from)
                    {
                        q.Player.SetCharVar("TutorialBypass", from + 1);
                        q.Player.SurvivalGuideOnTrigger();
                    }

                    return null;
                });
            }

            // Step 10 -> Talk to NPC after reaching level 18 (Reward: Copper A.M.A.N. Voucher). Instruct to get and
            // set a subjob.
            var step10 = b.AnyState(when: q => q.Quest.Step == 10);
            foreach (Tutor t in Tutors)
            {
                step10
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.MainLevel >= 18)
                        {
                            return QuestReply.ProgressEvent(t.Step10Pass, t.Nation);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step10NotYet);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step10Pass, q =>
                    {
                        if (q.Player.GiveItem(Item.CopperAmanVoucher))
                        {
                            q.Quest.SetStep(11);
                        }
                    });
            }

            // Step 11 -> Talk to NPC after setting a subjob (Reward: Chocopass x12, Echad Ring).
            var step11 = b.AnyState(when: q => q.Quest.Step == 11);
            foreach (Tutor t in Tutors)
            {
                step11
                    .Npc(t.Zone, t.Npc).OnTrigger(q =>
                    {
                        if (q.Player.SubLevel >= 1)
                        {
                            return QuestReply.ProgressEvent(t.Step11Pass);
                        }
                        else
                        {
                            return QuestReply.Event(t.Step11NotYet, t.Nation);
                        }
                    })
                    .OnEventFinish(t.Zone, t.Step11Pass, q =>
                    {
                        if (q.Player.GiveItems((Item.FreeChocopass, 12), (Item.EchadRing, 1)))
                        {
                            q.Quest.SetStep(12);
                        }
                    });
            }

            // Default dialog after completing tutorial.
            var step12 = b.AnyState(when: q => q.Quest.Step == 12);
            foreach (Tutor t in Tutors)
            {
                step12.Npc(t.Zone, t.Npc).OnTrigger(QuestReply.Event(t.Done));
            }
        }
    }
}
