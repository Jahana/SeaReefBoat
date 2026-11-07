// Crafted Corsair Artifact (hidden quest CorArtifact), ported from
// scripts/quests/hiddenQuests/Crafted_Corsair_Artifact.lua
// (quest rework B3b hidden quests). Leleroon : !pos -14.687 0.000 25.114 53; Raqtibahl (San d'Oria, Corsair's Frac)
// : !pos -59.000 -4.000 -39.000 232; Door_House (Bastok, Corsair's Bottes) : !pos 10.000 0.000 -16.000 234;
// Door_House (Windurst, Corsair's Gants) : !pos -200.000 -4.000 -111.000 238.
// One section: Against All Odds accepted or completed. Leleroon (Nashmau): Prog 1-4: 285 / 286 / 287 by Option (the
// commission's reminder); Prog 0 and not all three pieces made (Completed ~= 7): 282 (a progress event: [1] 1 once a
// piece is made, [7] Completed * 2: the menu hides a made piece one bit above its Completed bit); 282's finish on
// option 1-3 for a piece not made: the letter (green / blue / red), Option, Prog 1, Timer 0. The crafter of the
// Option (Windurst Waters' Leleroon green door, Option 1, Gants; Bastok Mines' blue door, Option 2, Bottes;
// Raqtibahl, Option 3, Frac): the letter held: the progress event (941 / 519 / 753; finish: Prog 2, the letter
// taken); Prog 2: a reminder (942 / 520 / 754), the four materials (tradeMatches): 943 / 521 / 755 (finish: taken,
// Prog 3); Prog 3: a reminder (954 / 535 / 761), the imperial pieces (four mythril / four mythril / one gold): 946 /
// 524 / 760 (finish: taken, Prog 4, Timer tomorrow, mustZone); Prog 4: the day past and zoned: 944 / 522 / 756
// (finish: the piece by npcUtil.giveItem, and when it fits finishCommission: the Completed bit of Option - 1, Prog,
// Option and Timer 0), else 945 / 523 / 757. Raqtibahl, not Option 3, with the Frac made (Completed bit 2): 758.
// Kept as the Lua has it: the doors answer only the named door (npc:getID() == LELEROON_*_DOOR, the zone's first
// Door_House); the other Door_Houses fall through to their defaults; a letter held wins over the Prog tests;
// Raqtibahl's 758 plays for any Option but 3 once the Frac is made (a second commission's char included).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class CraftedCorsairArtifact : HiddenQuestScript
    {
        public CraftedCorsairArtifact() : base("CorArtifact") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Crafted_Corsair_Artifact";

        // utils.mask.getBit(mask, bit)
        private static bool GetBit(int mask, int bit) => (mask & (1 << bit)) != 0;

        // finishCommission(player)
        private static void FinishCommission(IQuestContext q)
        {
            q.Quest.SetVarBit("Completed", q.Quest.Var("Option") - 1);
            q.Quest.SetStep(0);
            q.Quest.SetVar("Option", 0);
            q.Quest.SetVar("Timer", 0);
        }

        // the crafter's talk (the three have one shape): the letter, then the Prog reminders and the piece's day
        private static QuestReply CrafterTrigger(IQuestContext q, int letter, int letterCsid, int prog2Csid,
            int prog3Csid, int readyCsid, int waitCsid)
        {
            int questProgress = q.Quest.Step;

            if (q.Player.HasKeyItem(letter))
            {
                return QuestReply.ProgressEvent(letterCsid);
            }
            else if (questProgress == 2)
            {
                return QuestReply.Event(prog2Csid);
            }
            else if (questProgress == 3)
            {
                return QuestReply.Event(prog3Csid);
            }
            else if (questProgress == 4)
            {
                // The piece is handed over a day later, after leaving the area.
                if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay &&
                    !q.Quest.MustZone)
                {
                    return QuestReply.ProgressEvent(readyCsid);
                }
                else
                {
                    return QuestReply.Event(waitCsid);
                }
            }

            return null;
        }

        // the crafter's trade: the four materials at Prog 2, the imperial pieces at Prog 3
        private static QuestReply CrafterTrade(IQuestContext q, (int Item, int Qty)[] materials, int materialsCsid,
            (int Item, int Qty) pieces, int piecesCsid)
        {
            int questProgress = q.Quest.Step;

            if (questProgress == 2 && q.Trade.Matches(materials))
            {
                return QuestReply.ProgressEvent(materialsCsid);
            }
            else if (questProgress == 3 && q.Trade.Matches(pieces))
            {
                return QuestReply.ProgressEvent(piecesCsid);
            }

            return null;
        }

        // the letter's finish: Prog 2, the letter taken
        private static void LetterFinish(IQuestContext q, int letter)
        {
            q.Quest.SetStep(2);
            q.Player.DelKeyItem(letter);
        }

        // the materials' finish: taken, Prog 3
        private static void MaterialsFinish(IQuestContext q)
        {
            q.Trade.Complete();
            q.Quest.SetStep(3);
        }

        // the pieces' finish: taken, Prog 4, Timer tomorrow, mustZone
        private static void PiecesFinish(IQuestContext q)
        {
            q.Trade.Complete();
            q.Quest.SetStep(4);
            q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
            q.Quest.SetMustZone();
        }

        // the piece's finish: given, then finishCommission when it fit
        private static void PieceFinish(IQuestContext q, int piece)
        {
            if (q.Player.GiveItem(piece))
            {
                FinishCommission(q);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(0, 1, 2, 3, 4);

            b.AnyState(when: q => q.Player.QuestStatus(QuestLog.AhtUrghan, "AGAINST_ALL_ODDS") >= QuestStatus.Accepted)
                // Bastok Mines: Corsair's Bottes (Option 2)
                .Npc(Zone.BastokMines, Npc.BastokMines.DoorHouse).OnTrade(q =>
                {
                    if (q.Npc.Id == q.Player.NpcIdOf("LELEROON_BLUE_DOOR") &&
                        q.Quest.Var("Option") == 2)
                    {
                        return CrafterTrade(q,
                            new[]
                            {
                                (Item.MythrilSheet,                1),
                                (Item.SquareOfKarakulLeather,      1),
                                (Item.SquareOfLmBuffaloLeather,    1),
                                (Item.SquareOfWolfFelt,            1),
                            }, 521,
                            (Item.ImperialMythrilPiece, 4), 524);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Npc.Id == q.Player.NpcIdOf("LELEROON_BLUE_DOOR") &&
                        q.Quest.Var("Option") == 2)
                    {
                        return CrafterTrigger(q, KeyItem.LeleroonsLetterBlue, 519, 520, 535, 522, 523);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 519, q => LetterFinish(q, KeyItem.LeleroonsLetterBlue))
                .OnEventFinish(Zone.BastokMines, 521, MaterialsFinish)
                .OnEventFinish(Zone.BastokMines, 522, q => PieceFinish(q, Item.CorsairsBottes))
                .OnEventFinish(Zone.BastokMines, 524, PiecesFinish)

                // Nashmau: Leleroon
                .Npc(Zone.Nashmau, Npc.Nashmau.Leleroon).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    int questOption = q.Quest.Var("Option");

                    if (questProgress >= 1 && questProgress <= 4)
                    {
                        if (questOption == 1)
                        {
                            return QuestReply.Event(285);
                        }
                        else if (questOption == 2)
                        {
                            return QuestReply.Event(286);
                        }
                        else if (questOption == 3)
                        {
                            return QuestReply.Event(287);
                        }
                    }
                    else if (questProgress == 0 &&
                        q.Quest.Var("Completed") != 7)
                    {
                        // The introduction is only played for a first time customer.
                        // The menu hides a finished piece one bit above its Completed bit.
                        return QuestReply.ProgressEvent(282)
                            .Option(1, q.Quest.Var("Completed") != 0 ? 1 : 0)
                            .Option(7, q.Quest.Var("Completed") * 2);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 282, q =>
                {
                    int option = q.Option;
                    if (option >= 1 &&
                        option <= 3 &&
                        !GetBit(q.Quest.Var("Completed"), option - 1))
                    {
                        if (option == 1)
                        {
                            q.Player.GiveKeyItem(KeyItem.LeleroonsLetterGreen);
                        }
                        else if (option == 2)
                        {
                            q.Player.GiveKeyItem(KeyItem.LeleroonsLetterBlue);
                        }
                        else if (option == 3)
                        {
                            q.Player.GiveKeyItem(KeyItem.LeleroonsLetterRed);
                        }

                        q.Quest.SetVar("Option", option);
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Timer", 0);
                    }
                })

                // Port San d'Oria: Raqtibahl, Corsair's Frac (Option 3)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Raqtibahl).OnTrade(q =>
                {
                    if (q.Quest.Var("Option") == 3)
                    {
                        return CrafterTrade(q,
                            new[]
                            {
                                (Item.GoldChain,               1),
                                (Item.SquareOfVelvetCloth,     1),
                                (Item.SquareOfRedGrassCloth,   1),
                                (Item.SquareOfSailcloth,       1),
                            }, 755,
                            (Item.ImperialGoldPiece, 1), 760);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 3)
                    {
                        return CrafterTrigger(q, KeyItem.LeleroonsLetterRed, 753, 754, 761, 756, 757);
                    }
                    else if (GetBit(q.Quest.Var("Completed"), 2))
                    {
                        return QuestReply.Event(758);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 753, q => LetterFinish(q, KeyItem.LeleroonsLetterRed))
                .OnEventFinish(Zone.PortSanDoria, 755, MaterialsFinish)
                .OnEventFinish(Zone.PortSanDoria, 756, q => PieceFinish(q, Item.CorsairsFrac))
                .OnEventFinish(Zone.PortSanDoria, 760, PiecesFinish)

                // Windurst Waters: Corsair's Gants (Option 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DoorHouse).OnTrade(q =>
                {
                    if (q.Npc.Id == q.Player.NpcIdOf("LELEROON_GREEN_DOOR") &&
                        q.Quest.Var("Option") == 1)
                    {
                        return CrafterTrade(q,
                            new[]
                            {
                                (Item.SpoolOfGoldThread,       1),
                                (Item.SquareOfKarakulLeather,  1),
                                (Item.SquareOfRedGrassCloth,   1),
                                (Item.SpoolOfWamouraSilk,      1),
                            }, 943,
                            (Item.ImperialMythrilPiece, 4), 946);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Npc.Id == q.Player.NpcIdOf("LELEROON_GREEN_DOOR") &&
                        q.Quest.Var("Option") == 1)
                    {
                        return CrafterTrigger(q, KeyItem.LeleroonsLetterGreen, 941, 942, 954, 944, 945);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 941, q => LetterFinish(q, KeyItem.LeleroonsLetterGreen))
                .OnEventFinish(Zone.WindurstWaters, 943, MaterialsFinish)
                .OnEventFinish(Zone.WindurstWaters, 944, q => PieceFinish(q, Item.CorsairsGants))
                .OnEventFinish(Zone.WindurstWaters, 946, PiecesFinish);
        }
    }
}
