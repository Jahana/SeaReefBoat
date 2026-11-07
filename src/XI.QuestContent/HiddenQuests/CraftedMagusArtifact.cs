// Crafted Magus Artifact (hidden quest MagusArtifact), ported from
// scripts/quests/hiddenQuests/Crafted_Magus_Artifact.lua
// (quest rework B3b hidden quests). Lathuya : !pos -95.081 -6.000 31.638 50.
// A commissioned piece is finished overnight: it is handed over once the Vana'diel day has changed since the fee was
// paid, and only after an area change. Ordering the next piece needs a second day change and another area change.
// The commission state is in the [BLUAF] char vars (not the container's): Transformations' completion seeds
// [BLUAF]Remaining 7 (AhtUrhgan/Transformations.cs:90); only the mustZone is the container's.
// One section: Transformations accepted or completed. Lathuya, with the offset 8 x the pieces made: no order and a
// piece left: the day past RestingDay and zoned: one piece left: 746 ([7] the last piece's number), else 730 +
// offset (7 - Remaining); not yet: 737 + offset - 8 (asleep). An order: stage 0: 731 + offset; stage 1: 733 +
// offset; stage 2: the day past PaymentDay and zoned: 736 + offset (the piece), else 735 + offset. All made: 753
// (replaceDefault). Trade, a piece still to make and an order: stage 0, the four materials (tradeMatches): 732 +
// offset; stage 1, the fee (imperial mythril pieces): 734 + offset. The updates of 730 / 738 / 746
// (onPieceSelected): option 2-9; option % 3 == 2 picks piece floor(option / 4) + 1 (when still to make: Current,
// updateEvent(0, the materials)); another option shows the fee of Current (updateEvent(0, the coin, the amount)).
// The finishes: 732 / 740 / 748 (materials: stage 1, taken), 734 / 742 / 750 (fee: taken, stage 2, PaymentDay today,
// mustZone, the Magus Order Slip), 736 / 744 / 752 (the piece: given when Current is set and it fits; the Remaining
// bit cleared, PaymentDay, stage, Current 0; all made: RestingDay 0, else RestingDay today and mustZone; the slip
// taken).
// Kept as the Lua has it: the asleep event at no pieces made is 729 (737 - 8; reachable only with RestingDay at
// today or later, a GM's); the finishes read no option; the slip is taken even when it is not held.
// Differs from the Lua only in a state no play reaches (a GM's [BLUAF]Current 4+ at stage 2 before PaymentDay has
// passed): the Lua plays 735 + offset without touching craftingItems[Current], where the C# throws on
// CraftingItems[Current].
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class CraftedMagusArtifact : HiddenQuestScript
    {
        public CraftedMagusArtifact() : base("MagusArtifact") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Crafted_Magus_Artifact";

        private sealed class CraftingItem
        {
            public (int Item, int Qty)[] Materials;
            public int Currency;
            public int CurrencyAmt;
            public int Result;
        }

        // craftingItems[1..3]; [0] is nil in the Lua (no order)
        private static readonly CraftingItem[] CraftingItems =
        {
            null,
            // magus bazubands
            new CraftingItem
            {
                Materials = new[]
                {
                    (Item.PlatinumSheet,          1),
                    (Item.SquareOfVelvetCloth,    1),
                    (Item.SquareOfKarakulLeather, 1),
                    (Item.FlaskOfVenomPotion,     1),
                },
                Currency = Item.ImperialMythrilPiece,
                CurrencyAmt = 2,
                Result = Item.MagusBazubands,
            },
            // magus shalwar
            new CraftingItem
            {
                Materials = new[]
                {
                    (Item.GoldChain,                 1),
                    (Item.SquareOfVelvetCloth,       1),
                    (Item.ChunkOfFlanMeat,           1),
                    (Item.SquareOfImperialSilkCloth, 1),
                },
                Currency = Item.ImperialMythrilPiece,
                CurrencyAmt = 2,
                Result = Item.MagusShalwar,
            },
            // magus jubbah
            new CraftingItem
            {
                Materials = new[]
                {
                    (Item.SquareOfVelvetCloth,       1),
                    (Item.VialOfChimeraBlood,        1),
                    (Item.SquareOfKarakulCloth,      1),
                    (Item.SquareOfImperialSilkCloth, 1),
                },
                Currency = Item.ImperialMythrilPiece,
                CurrencyAmt = 4,
                Result = Item.MagusJubbah,
            },
        };

        // craftingItems[n]: nil outside 1-3
        private static CraftingItem ItemOf(int n) => n >= 1 && n <= 3 ? CraftingItems[n] : null;

        private static bool GetBit(int mask, int bit) => (mask & (1 << bit)) != 0;

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

        private static void OnPieceSelected(IQuestContext q)
        {
            int option = q.Option;
            if (option < 2 || option > 9)
            {
                return;
            }

            int remainingBLUAF = q.Player.CharVar("[BLUAF]Remaining");

            // Choosing a piece.
            if (option % 3 == 2)
            {
                int piece = option / 4 + 1;

                // Make sure the player isn't trying to cheat somehow
                if (GetBit(remainingBLUAF, piece - 1))
                {
                    q.Player.SetCharVar("[BLUAF]Current", piece);

                    var materials = new int[CraftingItems[piece].Materials.Length + 1];
                    materials[0] = 0;
                    for (int i = 0; i < CraftingItems[piece].Materials.Length; i++)
                    {
                        materials[i + 1] = CraftingItems[piece].Materials[i].Item;
                    }

                    q.Player.UpdateEvent(materials);
                }

                return;
            }

            CraftingItem item = ItemOf(q.Player.CharVar("[BLUAF]Current"));

            if (item != null)
            {
                q.Player.UpdateEvent(0, item.Currency, item.CurrencyAmt);
            }
        }

        private static void OnMaterialsTraded(IQuestContext q)
        {
            q.Player.SetCharVar("[BLUAF]CraftingStage", 1);
            q.Trade.Complete();
        }

        private static void OnFeeTraded(IQuestContext q)
        {
            q.Trade.Complete();
            q.Player.SetCharVar("[BLUAF]CraftingStage", 2);
            q.Player.SetCharVar("[BLUAF]PaymentDay", q.Player.VanadielUniqueDay);
            q.Quest.SetMustZone();
            q.Player.GiveKeyItem(KeyItem.MagusOrderSlip);
        }

        private static void OnPieceReceived(IQuestContext q)
        {
            int remainingBLUAF = q.Player.CharVar("[BLUAF]Remaining");
            int currentTask = q.Player.CharVar("[BLUAF]Current");

            // craftingItems[currentTask].result: a Current outside 0-3 (a GM's) raises in the Lua
            if (currentTask == 0 ||
                !q.Player.GiveItem(CraftingItems[currentTask].Result))
            {
                return;
            }

            q.Player.SetCharVar("[BLUAF]Remaining", remainingBLUAF & ~(1 << (currentTask - 1)));
            q.Player.SetCharVar("[BLUAF]PaymentDay", 0);
            q.Player.SetCharVar("[BLUAF]CraftingStage", 0);
            q.Player.SetCharVar("[BLUAF]Current", 0);

            // Player is finished with Lathuya
            if (q.Player.CharVar("[BLUAF]Remaining") == 0)
            {
                q.Player.SetCharVar("[BLUAF]RestingDay", 0);
            }
            else
            {
                q.Player.SetCharVar("[BLUAF]RestingDay", q.Player.VanadielUniqueDay);
                q.Quest.SetMustZone();
            }

            q.Player.DelKeyItem(KeyItem.MagusOrderSlip);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.QuestStatus(QuestLog.AhtUrghan, "TRANSFORMATIONS") >= QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Lathuya).OnTrade(q =>
                {
                    int remainingBLUAF = q.Player.CharVar("[BLUAF]Remaining");
                    CraftingItem item = ItemOf(q.Player.CharVar("[BLUAF]Current"));

                    if (remainingBLUAF == 0 ||
                        item == null)
                    {
                        return null;
                    }

                    int craftingStage = q.Player.CharVar("[BLUAF]CraftingStage");
                    int artifactOffset = 8 * (3 - CountBits(remainingBLUAF, 3));

                    if (craftingStage == 0 &&
                        q.Trade.Matches(item.Materials))
                    {
                        return QuestReply.ProgressEvent(732 + artifactOffset, item.Result, item.Currency,
                            item.CurrencyAmt);
                    }
                    else if (craftingStage == 1 &&
                        q.Trade.Matches((item.Currency, item.CurrencyAmt)))
                    {
                        return QuestReply.ProgressEvent(734 + artifactOffset, 0, item.Currency, item.CurrencyAmt);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int remainingBLUAF = q.Player.CharVar("[BLUAF]Remaining");
                    int totalCraftedPieces = 3 - CountBits(remainingBLUAF, 3);
                    int currentTask = q.Player.CharVar("[BLUAF]Current");
                    int craftingStage = q.Player.CharVar("[BLUAF]CraftingStage");
                    int artifactOffset = 8 * totalCraftedPieces;

                    // No order placed, and there is still a piece left to make.
                    if (currentTask == 0 && totalCraftedPieces != 3)
                    {
                        // The next order is taken a day later, after leaving the area.
                        if (q.Player.VanadielUniqueDay > q.Player.CharVar("[BLUAF]RestingDay") &&
                            !q.Quest.MustZone)
                        {
                            // Only one piece is left, so there is nothing to choose.
                            if (totalCraftedPieces == 2)
                            {
                                return QuestReply.ProgressEvent(746).Option(7, remainingBLUAF / 2 + 1);
                            }

                            // Will prompt for choosing which armor to work on
                            return QuestReply.ProgressEvent(730 + artifactOffset, 7 - remainingBLUAF);
                        }

                        return QuestReply.Event(737 + (artifactOffset - 8)); // Asleep message, wait until 1 day passes
                    }
                    // An order is in progress.
                    else if (currentTask > 0)
                    {
                        // craftingItems[currentTask]: a Current past 3 (a GM's) is nil; the Lua raises only where it
                        // dereferences it (stages 0-1, or stage 2 once PaymentDay has passed), the C# already here
                        CraftingItem item = CraftingItems[currentTask];

                        if (craftingStage == 0)
                        {
                            return QuestReply.Event(731 + artifactOffset, 0, item.Currency, item.CurrencyAmt);
                        }
                        else if (craftingStage == 1)
                        {
                            return QuestReply.Event(733 + artifactOffset, item.Result, item.Currency, item.CurrencyAmt);
                        }

                        // The piece is handed over a day later, after leaving the area.
                        if (q.Player.VanadielUniqueDay > q.Player.CharVar("[BLUAF]PaymentDay") &&
                            !q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(736 + artifactOffset, item.Result);
                        }

                        return QuestReply.Event(735 + artifactOffset);
                    }

                    return QuestReply.Event(753).ReplaceDefault(); // Dialogue after crafting all BLU AF
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 730, OnPieceSelected)
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 738, OnPieceSelected)
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 746, OnPieceSelected)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 732, OnMaterialsTraded)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 740, OnMaterialsTraded)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 748, OnMaterialsTraded)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 734, OnFeeTraded)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 742, OnFeeTraded)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 750, OnFeeTraded)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 736, OnPieceReceived)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 744, OnPieceReceived)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 752, OnPieceReceived);
        }
    }
}
