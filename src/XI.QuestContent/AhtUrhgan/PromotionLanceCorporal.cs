// Promotion: Lance Corporal (Log ID 6, Quest ID 92), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_Lance_Corporal.lua (quest rework B3b Aht Urhgan, group 3). Aht Urhgan Whitegate:
// Abquhbah !pos 35.5 -6.6 -58 (his script: startEvent(255), the Sentinels' rank menu), Nafiwaa !pos -74 -6 126 (no
// script; default 661), trigger area 3 (31 -7 -60 .. 39 -6 -52: Salaheem's Sentinels' second platform, where Abquhbah
// stands; the ToAU missions' area-3 handlers (1, 8, 18, 21, 24) need their mission current; 1's also the Boarding
// Permit). Wajaom Woodlands: the Mythralline
// Wellsprings 1-4 (four NPCs of one name; no script; default WELLSPRING); Bhaflau Thickets: Wellspring 5 (the same).
// Vars: Stage (0 START, 1 FIRST_MIX, 2 REMIX, 3 WAIT), Prog (two bits a tube: the thirds of each tube used, 0 full ..
// 3 empty), Option (the thirds of each tube in the mixture turned in), Wait (the day 5031 is due).
// Available with AssaultPromotion >= 25 and Promotion: Superior Private completed: Abquhbah 5030 (text_table 0), whose
// finish begins it. Stage 0: Abquhbah 5032 (importantOnce); Nafiwaa 5035, whose finish sets Stage 1 and gives the five
// Empty Test Tubes. Stages 1 and 2: Abquhbah 5033 (importantOnce); a Wellspring whose tube can be filled (its empty
// tube held, or at Stage 2 a tube not full): its event (Wajaom 4-7, Bhaflau 10), whose finish fills the tube (its two
// Prog bits cleared; the empty tube swapped for the filled one; at Stage 1 KEYITEM_OBTAINED with the tube, at Stage 2
// WELLSPRING + 1); else WELLSPRING + 3. Nafiwaa at Stage 1: no tube filled 5036, one to four 5037 ([1] = the count); at
// Stage 2 with fewer than two thirds left 5036; else the mixing game 5038 ([2] = Prog, [5] = the empty tubes' bits, [7]
// = 1 at Stage 2, text_table 0). 5038's option: the low 16 bits the tubes' contents after the mix, bits 30-31 the
// result: 2 accepts the mixture (Stage 3, Wait = tomorrow, Option = the thirds used in this mix, every tube key item
// taken), 1 throws it out (Stage 2, Prog = the contents, each tube now empty swapped back for its empty one), else
// nothing. Stage 3 with Option > 0: Nafiwaa 5039, Abquhbah 5034 (importantOnce), the Wellsprings WELLSPRING + 2; area 3
// once Wait <= today: 5031 (text_table 0), whose finish gives the reward of the mixture's quality (sum of thirds x
// density: 10 two Imperial Gold Pieces, 6 to 9.5 one of three or four Imperial Mythril Pieces or one Imperial Gold
// Piece; nothing otherwise; a full bag ends it there), completes it (the LC Wildcat Badge, Lance Corporal), sets
// AssaultPromotion 0 and takes the SP Wildcat Badge, saying LANCE_CORPORAL after the completion.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 5031's LANCE_CORPORAL is sent; the Lua's
// was a quest:messageSpecial action, built in a finish and dropped (no message).
// Kept as the Lua has it: getQuestReward draws math.randomInt(3, 4) whatever the quality (the table built before the
// test), then randomInt(1, 2) for the platinum tier only; the completion's result is not read; a mixture turned in with
// nothing used (Option 0) leaves no section (Stage 3, Option 0); 5038's other results (0, 3) change nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionLanceCorporal : QuestScript
    {
        public PromotionLanceCorporal() : base(QuestLog.AhtUrghan, "PROMOTION_LANCE_CORPORAL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Lance_Corporal";

        // local stage
        const int StageStart = 0, StageFirstMix = 1, StageRemix = 2, StageWait = 3;
        // local fillLevel
        const int FillFull = 0, FillEmpty = 3;

        // tubeKeyItems[tube]: { filledTube, emptyTube }, tubes 1 to 5 at [1] .. [5]
        static readonly int[] FilledTube =
            { 0, KeyItem.TestTube1, KeyItem.TestTube2, KeyItem.TestTube3, KeyItem.TestTube4, KeyItem.TestTube5 };
        static readonly int[] EmptyTube =
        {
            0, KeyItem.EmptyTestTube1, KeyItem.EmptyTestTube2, KeyItem.EmptyTestTube3, KeyItem.EmptyTestTube4,
            KeyItem.EmptyTestTube5,
        };

        // getQuestReward's densities, tubes 1 to 5
        static readonly double[] Density = { 0, 0.5, 1.5, 2.0, 1.5, 1.0 };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.LcWildcatBadge,
                Title = Title.LanceCorporal,
            });

            // Start: trigger Abquhbah
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_SUPERIOR_PRIVATE"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.ProgressEvent(5030).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5030, q => q.Quest.Begin());

            // 1st stage: trigger Nafiwaa and receive the 5 empty test tubes
            b.State(QuestStatus.Accepted, when: q => q.Quest.Var("Stage") == StageStart)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.Event(5032).ImportantOnce())
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nafiwaa)
                    .OnTrigger(q => QuestReply.ProgressEvent(5035).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5035, q =>
                {
                    q.Quest.SetVar("Stage", StageFirstMix);
                    // npcUtil.giveKeyItem(player, { EMPTY_TEST_TUBE_1, ..., EMPTY_TEST_TUBE_5 }): each in turn
                    q.Player.GiveKeyItem(KeyItem.EmptyTestTube1);
                    q.Player.GiveKeyItem(KeyItem.EmptyTestTube2);
                    q.Player.GiveKeyItem(KeyItem.EmptyTestTube3);
                    q.Player.GiveKeyItem(KeyItem.EmptyTestTube4);
                    q.Player.GiveKeyItem(KeyItem.EmptyTestTube5);
                });

            // 2nd (and optional 3rd) stage: fill the tubes at the wellsprings; Nafiwaa's mixing game
            b.State(QuestStatus.Accepted, when: q =>
                    q.Quest.Var("Stage") == StageFirstMix || q.Quest.Var("Stage") == StageRemix)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.Event(5033).ImportantOnce())
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nafiwaa).OnTrigger(NafiwaaMix)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5038, Finish5038)
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.MythrallineWellspring).OnTrigger(q =>
                {
                    // local offset = npc:getID() - wajaomID.npc.WELLSPRING (GetFirstID('Mythralline_Wellspring'))
                    int offset = q.Npc.Id - q.Player.NpcIdOf("WELLSPRING");
                    if (CanFillTube(q, 1 + offset))
                    {
                        return QuestReply.ProgressEvent(4 + offset);
                    }

                    return QuestReply.MessageSpecial(q.Text.Id(Text.WajaomWoodlands.WELLSPRING) + 3);
                })
                .OnEventFinish(Zone.WajaomWoodlands, 4, q =>
                    FillTestTube(q, 1, Text.WajaomWoodlands.KEYITEM_OBTAINED, Text.WajaomWoodlands.WELLSPRING))
                .OnEventFinish(Zone.WajaomWoodlands, 5, q =>
                    FillTestTube(q, 2, Text.WajaomWoodlands.KEYITEM_OBTAINED, Text.WajaomWoodlands.WELLSPRING))
                .OnEventFinish(Zone.WajaomWoodlands, 6, q =>
                    FillTestTube(q, 3, Text.WajaomWoodlands.KEYITEM_OBTAINED, Text.WajaomWoodlands.WELLSPRING))
                .OnEventFinish(Zone.WajaomWoodlands, 7, q =>
                    FillTestTube(q, 4, Text.WajaomWoodlands.KEYITEM_OBTAINED, Text.WajaomWoodlands.WELLSPRING))
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.MythrallineWellspring).OnTrigger(q =>
                {
                    if (CanFillTube(q, 5))
                    {
                        return QuestReply.ProgressEvent(10);
                    }

                    return QuestReply.MessageSpecial(q.Text.Id(Text.BhaflauThickets.WELLSPRING) + 3);
                })
                .OnEventFinish(Zone.BhaflauThickets, 10, q =>
                    FillTestTube(q, 5, Text.BhaflauThickets.KEYITEM_OBTAINED, Text.BhaflauThickets.WELLSPRING));

            // Complete: after the game day's wait, enter the area
            b.State(QuestStatus.Accepted, when: q => q.Quest.Var("Stage") == StageWait && q.Quest.Var("Option") > 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nafiwaa).OnTrigger(q => QuestReply.Event(5039))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrigger(q => QuestReply.Event(5034).ImportantOnce())
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                    q.Quest.Var("Wait") <= q.Player.VanadielUniqueDay
                        ? QuestReply.ProgressEvent(5031).TextTable(0)
                        : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5031, q =>
                {
                    var reward = GetQuestReward(q);

                    if (reward != null)
                    {
                        if (!q.Player.GiveItems((reward.Value.Item, reward.Value.Amount)))
                        {
                            return;
                        }
                    }

                    q.Quest.Complete();
                    // the Lua's quest:messageSpecial: built and dropped (fixed, user ruling 2026-11-04)
                    q.Text.Special(Text.AhtUrhganWhitegate.LANCE_CORPORAL);
                    q.Player.SetCharVar("AssaultPromotion", 0);
                    q.Player.DelKeyItem(KeyItem.SpWildcatBadge);
                })
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.MythrallineWellspring)
                    .OnTrigger(q => QuestReply.MessageSpecial(q.Text.Id(Text.WajaomWoodlands.WELLSPRING) + 2))
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.MythrallineWellspring)
                    .OnTrigger(q => QuestReply.MessageSpecial(q.Text.Id(Text.BhaflauThickets.WELLSPRING) + 2));
        }

        // getFluidLevel: utils.mask.splitBits(quest:getVar(player, var), 2)[tubeNum] or 0 (splitBits stops at the last
        // non-zero pair and returns nothing for a value of 0 or less)
        static int FluidLevel(IQuestContext q, int tubeNum, string var) => FluidLevel(q.Quest.Var(var), tubeNum);

        static int FluidLevel(int bits, int tubeNum) => bits > 0 ? (int)(((uint)bits >> (2 * (tubeNum - 1))) & 3) : 0;

        // getFilledTubeCount
        static int FilledTubeCount(IQuestContext q)
        {
            int tubeCount = 0;

            for (int tube = 1; tube <= 5; tube++)
            {
                if (q.Player.HasKeyItem(FilledTube[tube]))
                {
                    tubeCount = tubeCount + 1;
                }
            }

            return tubeCount;
        }

        // canFillTube
        static bool CanFillTube(IQuestContext q, int tubeNum) =>
            q.Player.HasKeyItem(EmptyTube[tubeNum]) ||
            (q.Quest.Var("Stage") == StageRemix && FluidLevel(q, tubeNum, "Prog") != FillFull);

        // quest:unsetVarBit(player, name, bitNum): the var less the bit, written only when the bit is set
        // (Container:unsetVarBit: player:setVar(prefix .. name, currentValue - bitValue))
        static void UnsetVarBit(IQuestContext q, string name, int bitNum)
        {
            int currentValue = q.Quest.Var(name);
            int bitValue = 1 << bitNum;
            if ((currentValue & bitValue) != 0)
            {
                q.Quest.SetVar(name, currentValue - bitValue);
            }
        }

        // fillTestTube; zones[player:getZoneID()]'s texts: the handler's zone's
        static void FillTestTube(IQuestContext q, int tubeNum, ZoneText keyItemObtained, ZoneText wellspring)
        {
            int bitPosOne = (tubeNum * 2) - 1;
            int bitPosTwo = bitPosOne - 1;

            UnsetVarBit(q, "Prog", bitPosOne);
            UnsetVarBit(q, "Prog", bitPosTwo);

            if (q.Player.HasKeyItem(EmptyTube[tubeNum]))
            {
                q.Player.DelKeyItem(EmptyTube[tubeNum]);
                q.Player.AddKeyItem(FilledTube[tubeNum]);
            }

            if (q.Quest.Var("Stage") == StageFirstMix)
            {
                q.Text.Special(keyItemObtained, FilledTube[tubeNum]);
            }
            else
            {
                q.Text.Special(q.Text.Id(wellspring) + 1);
            }
        }

        // Nafiwaa's onTrigger in stages 1 and 2
        static QuestReply NafiwaaMix(IQuestContext q)
        {
            int currentStage = q.Quest.Var("Stage");
            int tubeContents = q.Quest.Var("Prog");
            int isRemix = currentStage == StageRemix ? 1 : 0;
            int emptyTubes = 0;

            if (currentStage == StageFirstMix)
            {
                int filledTubeCount = FilledTubeCount(q);

                if (filledTubeCount == 0)
                {
                    return QuestReply.Event(5036);
                }
                else if (filledTubeCount < 5) // must gather all 5 tubes on first mix
                {
                    return QuestReply.Event(5037).Option(1, filledTubeCount);
                }
            }
            else if (currentStage == StageRemix)
            {
                int totalThirds = 15;

                for (int tube = 1; tube <= 5; tube++)
                {
                    totalThirds = totalThirds - FluidLevel(q, tube, "Prog");
                }

                if (totalThirds < 2) // not enough liquid to mix anything
                {
                    return QuestReply.Event(5036);
                }
            }

            for (int tube = 1; tube <= 5; tube++)
            {
                if (q.Player.HasKeyItem(EmptyTube[tube]))
                {
                    emptyTubes = emptyTubes | (1 << (tube - 1)); // utils.mask.setBit(emptyTubes, tubeNum - 1, 1)
                }
            }

            return QuestReply.ProgressEvent(5038)
                .Option(2, tubeContents).Option(5, emptyTubes).Option(7, isRemix).TextTable(0);
        }

        // 5038's finish: local remainingTubeContents, mask = utils.mask.varSplit(option, 16); result = bit.rshift(mask,
        // 14) (the option a uint32: bit's 32-bit view of it)
        static void Finish5038(IQuestContext q)
        {
            uint option = unchecked((uint)q.Option);
            int remainingTubeContents = (int)(option & 0xFFFF);
            int mask = (int)(option >> 16);

            int result = mask >> 14;

            if (result == 2) // accept the results, turn in to guild
            {
                int startingContents = q.Quest.Var("Prog");

                q.Quest.SetVar("Stage", StageWait);
                q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay + 1);
                q.Quest.SetVar("Option", remainingTubeContents - startingContents);

                for (int keyItem = KeyItem.EmptyTestTube1; keyItem <= KeyItem.TestTube5; keyItem++)
                {
                    if (q.Player.HasKeyItem(keyItem))
                    {
                        q.Player.DelKeyItem(keyItem);
                    }
                }
            }
            else if (result == 1) // threw it out
            {
                q.Quest.SetVar("Stage", StageRemix);
                q.Quest.SetVar("Prog", remainingTubeContents);

                for (int tube = 1; tube <= 5; tube++)
                {
                    if (FluidLevel(q, tube, "Prog") == FillEmpty &&
                        q.Player.HasKeyItem(FilledTube[tube]))
                    {
                        q.Player.DelKeyItem(FilledTube[tube]);
                        q.Player.AddKeyItem(EmptyTube[tube]);
                    }
                }
            }
        }

        // getQuestReward: the thirds of each tube in the mixture (Option) times the tube's density, summed
        static (int Item, int Amount)? GetQuestReward(IQuestContext q)
        {
            double mythrallineQuality = 0;

            for (int tube = 1; tube <= 5; tube++)
            {
                mythrallineQuality = mythrallineQuality + (FluidLevel(q, tube, "Option") * Density[tube]);
            }

            // Final Mythralline Quality: 1.0 - 1.5 Silver, 2.0 - 3.5 Mythril, 4.0 - 5.5 Gold, 6.0 - 9.5 Platinum, 10.0
            // Luminium, 10.5 Smoke. The rewardTiers table is built first, so its math.randomInt(3, 4) is drawn every
            // time
            int platinumMythrilAmount = q.Player.RandomInt(3, 4);

            if (mythrallineQuality == 10)
            {
                return (Item.ImperialGoldPiece, 2);
            }
            else if (mythrallineQuality >= 6 && mythrallineQuality <= 9.5)
            {
                return q.Player.RandomInt(1, 2) == 1
                    ? (Item.ImperialMythrilPiece, platinumMythrilAmount)
                    : (Item.ImperialGoldPiece, 1);
            }

            return null;
        }
    }
}
