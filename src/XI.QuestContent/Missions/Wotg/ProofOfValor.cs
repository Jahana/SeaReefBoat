// Proof of Valor (WotG 21, Log ID 5, Mission ID 20), a line-for-line port of
// scripts/missions/wotg/21_Proof_of_Valor.lua (quest rework C3 T6). Raustigne !pos 3.979 -1.999 44.456 80. One section
// (the mission current), in Southern San d'Oria [S]. The charvar Option is the signature total (20 needed) and Status
// holds one bit per petition. Raustigne's first trigger while mustZone is clear and Option is below 20 sets mustZone,
// sends MUST_GATHER_SIGNATURES (20) and replies CURRENT_PETITIONS (0, Option, the North Bound Petition); at 20 or more
// it plays the progress event 148 (the campaign allegiance, Option). Each of the ten petition NPCs (Illeuse 115 bit 1,
// Aissaville 123 bit 0, Andagge 121 bit 2, Daigraffeaux 118 bit 3, Louxiard 119 bit 4, Machionage 120 bit 5, Elnonde
// 116 bit 6, Loillie 117 bit 7, Remiotte 124 bit 8, Mailleronce 122 bit 9) plays its event while its bit is clear; the
// finish sets the bit, adds 1 to Option and sends HAVE_GATHERED_SIGNATURE (the petition, the total). The four games
// (Farouel 125 bit 10, Vichauxdat 126 bit 13, Aurfet 127 bit 11, Corseihaut 128 bit 12) set the local vars gameRound 1
// and numCorrect (3, 3, 2, 0) on the trigger; each update with the option equal to three times the answers sends the
// update packet numCorrect, and one equal to the current round's answer adds 1 to both vars. Five NPCs first play a
// Remind event whose finish sets their Remind bit (Sabiliont 129 bit 3, Hauberliond 130 bit 2, Eumielle 131 bit 0,
// Coucheutand 132 bit 1, Rongelouts 137 bit 4) and afterwards the plain event 139, 140, 141 or 142 (Rongelouts 143 once
// per zone); the trades then play their events: Eumielle's Angler's Cassoulet 135 (12 signatures, bit 14),
// Hauberliond's 99 or more crossbow bolts and nothing else 134 (min(12, 8 + floor(n / 99)), bit 16), Sabiliont's gysahl
// greens and nothing else 133 (min(12, 5 + floor(n / 99)), bit 17), Coucheutand's single orc item 136 (Orcish Axe 5,
// Orc Helmet 10, Orc Pauldron 10, Gold Orcmask 15, bit 15), Rongelouts' Gnole Claw 144 (its finish with option 1 adds
// 35, bit 18, otherwise sets Prog 1; the trigger then plays 138 [80, 60] whose finish with option 1 adds 35). Each
// trade finish completes the trade. Event 148's finish with option 1 gives the first itemRewards entry whose threshold
// Option meets and completes the mission (A Sanguinary Prelude current), also with no entry; a full bag completes
// nothing; other options set mustZone. Kept as the Lua has it: the game finishes read mission:getVar('numCorrect')
// where the value lives in the local var, so the games add 0 signatures and only set the Status bit; pairs(itemRewards)
// is in hash order, the recorded one (Angel Skin Orb 81, Square of Foulard 41, Oxblood Orb 30 first, so Orichalcum 100
// and Molybdenum 120 give the Angel Skin Orb); the Hauberliond and Sabiliont formulas are the Lua's estimates; the
// trade handlers of Hauberliond, Sabiliont, Coucheutand, Eumielle and Rongelouts do not test their Status bit, so a
// repeat trade adds signatures again (#hauberliond: x99 sets bit 16 and adds 9; x198 and x297 add 10 and 11 again;
// #rongelouts: a second claw adds 35).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ProofOfValor : MissionScript
    {
        public ProofOfValor() : base(XI.Quests.MissionLog.WoTG, "PROOF_OF_VALOR") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/21_Proof_of_Valor";

        // the Lua's pairs(itemRewards) order as recorded: item, signatures required
        static readonly (int Item, int Required)[] ItemRewards =
        {
            (Item.AngelSkinOrb, 81),
            (Item.SquareOfFoulard, 41),
            (Item.OxbloodOrb, 30),
            (Item.OrichalcumIngot, 100),
            (Item.MolybdenumIngot, 120),
        };

        // the Lua's pairs(orcItems): each is a single item trade, so the order cannot show
        static readonly (int Item, int Signatures)[] OrcItems =
        {
            (Item.OrcishAxe, 5),
            (Item.OrcHelmet, 10),
            (Item.OrcPauldron, 10),
            (Item.GoldOrcmask, 15),
        };

        static void CompletePetition(IQuestContext q, int posBit, int numSignatures)
        {
            q.Mission.SetVarBit("Status", posBit);
            q.Mission.IncrementVar("Option", numSignatures);

            int totalSignatures = q.Mission.Var("Option");

            q.Text.Special(Text.SouthernSanDoriaS.HAVE_GATHERED_SIGNATURE, KeyItem.NorthBoundPetition, totalSignatures);
        }

        static void UpdateGameRound(IQuestContext q, int[] correctOptions)
        {
            int gameRound = q.Mission.LocalVar("gameRound");
            int numCorrect = q.Mission.LocalVar("numCorrect");

            if (q.Option == correctOptions.Length * 3)
            {
                q.Player.UpdateEvent(numCorrect);
            }
            else if (gameRound >= 1 && gameRound <= correctOptions.Length && q.Option == correctOptions[gameRound - 1])
            {
                q.Mission.SetLocalVar("numCorrect", numCorrect + 1);
                q.Mission.SetLocalVar("gameRound", gameRound + 1);
            }
        }

        // the game NPC's trigger: its Status bit clear sets the round and the starting count, plays its event
        static QuestReply GameTrigger(IQuestContext q, int bit, int numCorrect, int csid)
        {
            if (!q.Mission.IsVarBitsSet("Status", bit))
            {
                q.Mission.SetLocalVar("gameRound", 1);
                q.Mission.SetLocalVar("numCorrect", numCorrect);

                return QuestReply.ProgressEvent(csid);
            }

            return null;
        }

        // a petition NPC's trigger: its Status bit clear plays its progress event
        static QuestReply PetitionTrigger(IQuestContext q, int bit, int csid)
        {
            if (!q.Mission.IsVarBitsSet("Status", bit))
            {
                return QuestReply.ProgressEvent(csid);
            }

            return null;
        }

        // a Remind NPC's trigger: Status bit clear, then the Remind event once, afterwards the plain event
        static QuestReply RemindTrigger(IQuestContext q, int statusBit, int remindBit, int remindCsid, int plainCsid)
        {
            if (!q.Mission.IsVarBitsSet("Status", statusBit))
            {
                if (!q.Mission.IsVarBitsSet("Remind", remindBit))
                {
                    return QuestReply.ProgressEvent(remindCsid);
                }
                else
                {
                    return QuestReply.Event(plainCsid);
                }
            }

            return null;
        }

        static void GameFinish(IQuestContext q, int bit)
        {
            if (q.Option == 1)
            {
                int numSignatures = q.Mission.Var("numCorrect");

                CompletePetition(q, bit, numSignatures);
            }
        }

        static void TradeFinish(IQuestContext q, int bit)
        {
            q.Trade.Complete();

            int numSignatures = q.Mission.LocalVar("numSignatures");

            CompletePetition(q, bit, numSignatures);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ASanguinaryPrelude,
            });

            const int zone = Zone.SouthernSanDoriaS;

            b.State(current: true)
                .Npc(zone, Npc.SouthernSanDoriaS.Raustigne).OnTrigger(q =>
                {
                    int numPetitions = q.Mission.Var("Option");

                    if (!q.Mission.MustZone)
                    {
                        if (numPetitions < 20)
                        {
                            // NOTE: the messages are only displayed once per zone
                            q.Mission.SetMustZone();

                            q.Text.Special(Text.SouthernSanDoriaS.MUST_GATHER_SIGNATURES, 20);
                            return QuestReply.MessageSpecial(Text.SouthernSanDoriaS.CURRENT_PETITIONS, 0, numPetitions,
                                KeyItem.NorthBoundPetition);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(148, q.Player.CampaignAllegiance, q.Mission.Var("Option"));
                        }
                    }

                    return null;
                })
                .Npc(zone, Npc.SouthernSanDoriaS.Aissaville).OnTrigger(q => PetitionTrigger(q, 0, 123))
                .Npc(zone, Npc.SouthernSanDoriaS.Andagge).OnTrigger(q => PetitionTrigger(q, 2, 121))
                .Npc(zone, Npc.SouthernSanDoriaS.Aurfet).OnTrigger(q => GameTrigger(q, 11, 2, 127))
                .Npc(zone, Npc.SouthernSanDoriaS.Corseihaut).OnTrigger(q => GameTrigger(q, 12, 0, 128))
                .Npc(zone, Npc.SouthernSanDoriaS.Coucheutand).OnTrade(q =>
                {
                    foreach (var orc in OrcItems)
                    {
                        if (q.Trade.Matches((orc.Item, 1)))
                        {
                            q.Mission.SetLocalVar("numSignatures", orc.Signatures);

                            return QuestReply.ProgressEvent(136, orc.Item);
                        }
                    }

                    return null;
                })
                .OnTrigger(q => RemindTrigger(q, 15, 1, 132, 142))
                .Npc(zone, Npc.SouthernSanDoriaS.Daigraffeaux).OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Status", 3))
                    {
                        return QuestReply.ProgressEvent(118, q.Player.CampaignAllegiance, 19, 6, 0, 0, 0, 8, 0);
                    }

                    return null;
                })
                .Npc(zone, Npc.SouthernSanDoriaS.Elnonde).OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Status", 6))
                    {
                        return QuestReply.ProgressEvent(116, q.Player.CampaignAllegiance, 23, 2964, 32747, 1073741823,
                            3, 10, 0);
                    }

                    return null;
                })
                .Npc(zone, Npc.SouthernSanDoriaS.Eumielle).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.AnglersCassoulet, 1)) && q.Mission.IsVarBitsSet("Remind", 0))
                    {
                        return QuestReply.ProgressEvent(135);
                    }

                    return null;
                })
                .OnTrigger(q => RemindTrigger(q, 14, 0, 131, 141))
                .Npc(zone, Npc.SouthernSanDoriaS.Farouel).OnTrigger(q => GameTrigger(q, 10, 3, 125))
                .Npc(zone, Npc.SouthernSanDoriaS.Hauberliond).OnTrade(q =>
                {
                    // TODO in the Lua: this may not be accurate; a single crossbow bolt may be an acceptable trade
                    if (q.Trade.HasExactly((Item.CrossbowBolt, q.Trade.ItemCount)) &&
                        q.Trade.ItemCount >= 99 &&
                        q.Mission.IsVarBitsSet("Remind", 2))
                    {
                        int numStacks = q.Trade.ItemCount / 99;
                        int numSignatures = System.Math.Min(12, 8 + numStacks);

                        q.Mission.SetLocalVar("numSignatures", numSignatures);

                        return QuestReply.ProgressEvent(134);
                    }

                    return null;
                })
                .OnTrigger(q => RemindTrigger(q, 16, 2, 130, 140))
                .Npc(zone, Npc.SouthernSanDoriaS.Illeuse).OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Status", 1))
                    {
                        return QuestReply.ProgressEvent(115, q.Player.CampaignAllegiance, 22);
                    }

                    return null;
                })
                .Npc(zone, Npc.SouthernSanDoriaS.Loillie).OnTrigger(q => PetitionTrigger(q, 7, 117))
                .Npc(zone, Npc.SouthernSanDoriaS.Louxiard).OnTrigger(q => PetitionTrigger(q, 4, 119))
                .Npc(zone, Npc.SouthernSanDoriaS.Machionage).OnTrigger(q => PetitionTrigger(q, 5, 120))
                .Npc(zone, Npc.SouthernSanDoriaS.Mailleronce).OnTrigger(q => PetitionTrigger(q, 9, 122))
                .Npc(zone, Npc.SouthernSanDoriaS.Remiotte).OnTrigger(q => PetitionTrigger(q, 8, 124))
                .Npc(zone, Npc.SouthernSanDoriaS.RongeloutsNDistaud).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.GnoleClaw, 1)))
                    {
                        return QuestReply.ProgressEvent(144);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Status", 18))
                    {
                        if (q.Mission.Var("Prog") == 0)
                        {
                            if (!q.Mission.IsVarBitsSet("Remind", 4))
                            {
                                return QuestReply.ProgressEvent(137);
                            }
                            else
                            {
                                return QuestReply.Event(143).OncePerZone();
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(138, 80, 60);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(150).OncePerZone();
                    }
                })
                .Npc(zone, Npc.SouthernSanDoriaS.Sabiliont).OnTrade(q =>
                {
                    if (q.Mission.IsVarBitsSet("Remind", 3) &&
                        q.Trade.HasExactly((Item.BunchOfGysahlGreens, q.Trade.ItemCount)))
                    {
                        // TODO in the Lua: the formula is estimated; a single gysahl green awards 5 signatures
                        int numStacks = q.Trade.ItemCount / 99;
                        int numSignatures = System.Math.Min(12, 5 + numStacks);

                        q.Mission.SetLocalVar("numSignatures", numSignatures);

                        return QuestReply.ProgressEvent(133);
                    }

                    return null;
                })
                .OnTrigger(q => RemindTrigger(q, 17, 3, 129, 139))
                .Npc(zone, Npc.SouthernSanDoriaS.Vichauxdat).OnTrigger(q => GameTrigger(q, 13, 3, 126))
                .OnEventUpdate(zone, 125, q => UpdateGameRound(q, new[] { 1, 4, 8 }))
                .OnEventUpdate(zone, 126, q => UpdateGameRound(q, new[] { 1, 4, 6 }))
                .OnEventUpdate(zone, 127, q => UpdateGameRound(q, new[] { 1, 5, 7, 10 }))
                .OnEventUpdate(zone, 128, q => UpdateGameRound(q, new[] { 2, 5, 6, 11, 13, 16 }))
                .OnEventFinish(zone, 115, q => CompletePetition(q, 1, 1))
                .OnEventFinish(zone, 116, q => CompletePetition(q, 6, 1))
                .OnEventFinish(zone, 117, q => CompletePetition(q, 7, 1))
                .OnEventFinish(zone, 118, q => CompletePetition(q, 3, 1))
                .OnEventFinish(zone, 119, q => CompletePetition(q, 4, 1))
                .OnEventFinish(zone, 120, q => CompletePetition(q, 5, 1))
                .OnEventFinish(zone, 121, q => CompletePetition(q, 2, 1))
                .OnEventFinish(zone, 122, q => CompletePetition(q, 9, 1))
                .OnEventFinish(zone, 123, q => CompletePetition(q, 0, 1))
                .OnEventFinish(zone, 124, q => CompletePetition(q, 8, 1))
                .OnEventFinish(zone, 125, q => GameFinish(q, 10))
                .OnEventFinish(zone, 126, q => GameFinish(q, 13))
                .OnEventFinish(zone, 127, q => GameFinish(q, 11))
                .OnEventFinish(zone, 128, q => GameFinish(q, 12))
                .OnEventFinish(zone, 129, q => q.Mission.SetVarBit("Remind", 3))
                .OnEventFinish(zone, 130, q => q.Mission.SetVarBit("Remind", 2))
                .OnEventFinish(zone, 131, q => q.Mission.SetVarBit("Remind", 0))
                .OnEventFinish(zone, 132, q => q.Mission.SetVarBit("Remind", 1))
                .OnEventFinish(zone, 133, q => TradeFinish(q, 17))
                .OnEventFinish(zone, 134, q => TradeFinish(q, 16))
                .OnEventFinish(zone, 135, q =>
                {
                    q.Trade.Complete();
                    CompletePetition(q, 14, 12);
                })
                .OnEventFinish(zone, 136, q => TradeFinish(q, 15))
                .OnEventFinish(zone, 137, q => q.Mission.SetVarBit("Remind", 4))
                .OnEventFinish(zone, 138, q =>
                {
                    if (q.Option == 1)
                    {
                        CompletePetition(q, 18, 35);
                    }
                })
                .OnEventFinish(zone, 144, q =>
                {
                    q.Trade.Complete();

                    if (q.Option == 1)
                    {
                        CompletePetition(q, 18, 35);
                    }
                    else
                    {
                        q.Mission.SetVar("Prog", 1);
                    }
                })
                .OnEventFinish(zone, 148, q =>
                {
                    if (q.Option == 1)
                    {
                        int numSignatures = q.Mission.Var("Option");
                        int rewardItemId = 0;

                        foreach (var reward in ItemRewards)
                        {
                            if (numSignatures >= reward.Required)
                            {
                                rewardItemId = reward.Item;
                                break;
                            }
                        }

                        if (rewardItemId == 0 || q.Player.GiveItem(rewardItemId))
                        {
                            q.Mission.Complete();
                        }
                    }
                    else
                    {
                        q.Mission.SetMustZone();
                    }
                });
        }
    }
}
