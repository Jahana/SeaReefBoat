// Crafted Dancer Artifact (hidden quest DncArtifact), ported from
// scripts/quests/hiddenQuests/Crafted_Dancer_Artifact.lua
// (quest rework B3b hidden quests). Olgald : !pos -53.072 -1 103.380 244; Matthias : !pos -114.1 -4.29 -107.28 235.
// Vars: Prog (overall progress), Option (the selected piece), Status (the pieces still to make, a bit mask; 0 done),
// Timer (VanadielUniqueDay wait). Prog 1 is set outside: The Road to Divadom's completion writes the char var
// HQuest[DncArtifact]Prog 1 (Jeuno/TheRoadToDivadom.cs:79).
// Section 1 (The Road to Divadom completed, main job Dancer): Olgald at Prog 1: 10167 (finish: Prog 2, Status 7);
// Prog 2: 10168 once per zone. Section 2 (Prog > 0, Status > 0, Dancer): Matthias: Prog 2: 492 (finish: option 0
// Prog 3, else Prog 4 and Option); Prog 3: 493 with 1 for each piece made (finish on a piece still to make: Option,
// Prog 4); Prog 4: 494 (Option - 1); its trade, exactly the piece's three cloths: 495 (Option - 1; finish:
// confirmed, Timer tomorrow, Prog 5); Prog 5: the day past: 497 (the piece for the char's gender, 1 on the last
// piece; finish: the piece given, then on the last piece quest:complete (the vars cleared), else the Status bit
// unset and Prog 3), else 496. Section 3 (the three pieces held, Dancer): Matthias 498.
// Kept as the Lua has it: 497's finish reads the last piece again after the item is given; section 3 answers
// whenever the three pieces are held (made here or not); the sections' order decides (section 2 before 3 at
// Matthias).
// Differs from the Lua only in a state no play reaches (a GM's Prog 5 with Option 0): the Lua
// raises on dncArtifactOptions[0][1] where the (0, null) sentinel row below plays 497 with 0 - gender (and its
// finish gives item 0 / -1 instead of raising).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class CraftedDancerArtifact : HiddenQuestScript
    {
        public CraftedDancerArtifact() : base("DncArtifact") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Crafted_Dancer_Artifact";

        // dncArtifactOptions: [option] = { the female piece, { the three cloths } }
        private static readonly (int Piece, int[] Cloths)[] DncArtifactOptions =
        {
            (0, null),
            (Item.DancersTiaraF, new[]
                { Item.SquareOfImperialSilkCloth, Item.SquareOfWolfFelt, Item.SquareOfSilverBrocade }),
            (Item.DancersBanglesF, new[]
                { Item.SquareOfKarakulCloth, Item.SquareOfRainbowCloth, Item.SquareOfRainbowVelvet }),
            (Item.DancersToeShoesF, new[]
                { Item.SquareOfWamouraCloth, Item.SquareOfMoblinweave, Item.SquareOfGoldBrocade }),
        };

        // utils.mask.getBit(mask, bit)
        private static bool GetBit(int mask, int bit) => (mask & (1 << bit)) != 0;

        // utils.mask.countBits(mask, length)
        private static int CountBits(int mask, int length)
        {
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                if (GetBit(mask, i))
                {
                    count++;
                }
            }

            return count;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(2, 3, 4, 5);

            // Common Functions and Beginning Quest. Note: Progress variable is set on completion of Road to Divadom in
            // order to prevent needing a forever CharVar.
            b.AnyState(when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "THE_ROAD_TO_DIVADOM") &&
                    q.Player.MainJob == Job.Dnc)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Olgald).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(10167);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(10168).OncePerZone();
                    }

                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 10167, q =>
                {
                    q.Quest.SetStep(2);
                    q.Quest.SetVar("Status", 7);
                });

            // Equivalent of Accepted. If Status == 0, then there is no remaining Artifact to be quested, and
            // we can treat as Completed.
            b.AnyState(when: q =>
                    q.Quest.Step > 0 &&
                    q.Quest.Var("Status") > 0 &&
                    q.Player.MainJob == Job.Dnc)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Matthias).OnTrade(q =>
                {
                    int selectedArtifact = q.Quest.Var("Option");

                    if (q.Quest.Step == 4 &&
                        q.Trade.HasExactly(DncArtifactOptions[selectedArtifact].Cloths))
                    {
                        return QuestReply.ProgressEvent(495, selectedArtifact - 1);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(492);
                    }
                    else if (questProgress == 3)
                    {
                        int questStatus = q.Quest.Var("Status");
                        int[] completedItems = { 0, 0, 0 };

                        for (int artifactOption = 1; artifactOption <= 3; artifactOption++)
                        {
                            if (!GetBit(questStatus, artifactOption - 1))
                            {
                                completedItems[artifactOption - 1] = 1;
                            }
                        }

                        return QuestReply.ProgressEvent(493, completedItems[0], completedItems[1], completedItems[2]);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(494, q.Quest.Var("Option") - 1);
                    }
                    else if (questProgress == 5)
                    {
                        if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                        {
                            int isLastArtifact = CountBits(q.Quest.Var("Status"), 3) == 1 ? 1 : 0;

                            return QuestReply.ProgressEvent(497,
                                DncArtifactOptions[q.Quest.Var("Option")].Piece - q.Player.Gender, isLastArtifact);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(496);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.BastokMarkets, 492, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(3);
                    }
                    else
                    {
                        q.Quest.SetStep(4);
                        q.Quest.SetVar("Option", q.Option);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 493, q =>
                {
                    if (q.Option != 0)
                    {
                        if (GetBit(q.Quest.Var("Status"), q.Option - 1))
                        {
                            q.Quest.SetVar("Option", q.Option);
                            q.Quest.SetStep(4);
                        }
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 495, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.BastokMarkets, 497, q =>
                {
                    int questOption = q.Quest.Var("Option");
                    int artifactItemID = DncArtifactOptions[questOption].Piece - q.Player.Gender;

                    if (q.Player.GiveItem(artifactItemID))
                    {
                        if (CountBits(q.Quest.Var("Status"), 3) == 1)
                        {
                            q.Quest.Complete();
                        }
                        else
                        {
                            q.Quest.UnsetVarBit("Status", questOption - 1);
                            q.Quest.SetStep(3);
                        }
                    }
                });

            b.AnyState(when: q =>
                    q.Player.FindItem(Item.DancersTiaraF - q.Player.Gender) &&
                    q.Player.FindItem(Item.DancersBanglesF - q.Player.Gender) &&
                    q.Player.FindItem(Item.DancersToeShoesF - q.Player.Gender) &&
                    q.Player.MainJob == Job.Dnc)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Matthias).OnTrigger(QuestReply.ProgressEvent(498));
        }
    }
}
