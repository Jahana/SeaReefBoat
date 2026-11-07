// Crafted Puppetry Artifact (hidden quest PuppetryArtifact), ported from
// scripts/quests/hiddenQuests/Crafted_Puppetry_Artifact.lua (quest rework B3b hidden quests). Dhima Polevhia : !pos
// 67.802 -6.000 26.315 50.
// Dhima finishes a piece overnight: it is handed over once the Vana'diel day has changed since the materials were
// traded; no area change, and the next order can be placed at once. The state is in the [PUP] char vars and
// [AF]pupCrafted (shared with the artifact reset system): none is the container's.
// One section: Puppetmaster Blues accepted or completed. Dhima: orderStage 2: the day past orderTime: 793 when two
// pieces are made (the last), else 792; not yet: 796. Stage 1: 790 (0, 0, 0, the piece's bit). Three made: 788
// (replaceDefault). Some made: 791 (0, the mask). None: 789. 789 / 791's finish on option 1-3: orderId the piece,
// orderStage 1. Trade at stage 1, the piece's five items (tradeMatches): 795 (finish: taken, stage 2, orderTime today).
// 792 / 793's finish: the piece (npcUtil.giveItem); when it fits, [AF]pupCrafted += 1 << its bit (1-3), orderId,
// orderStage and orderTime 0.
// Kept as the Lua has it: the piece bits are 1-3 (bit 0 unused; countBits over 4 bits); pieceBits[orderId] for an
// orderId that is no piece (a GM's) is nil (790's fourth parameter nil; 792's increment raises); an order may be
// placed again for a piece already made (791's menu decides).
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class CraftedPuppetryArtifact : HiddenQuestScript
    {
        public CraftedPuppetryArtifact() : base("PuppetryArtifact") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Crafted_Puppetry_Artifact";

        private static readonly Dictionary<int, int> PieceBits = new Dictionary<int, int>
        {
            [Item.PuppetryTobe] = 1,
            [Item.PuppetryDastanas] = 2,
            [Item.PuppetryBabouches] = 3,
        };

        private static readonly Dictionary<int, (int Item, int Qty)[]> TradeTable =
            new Dictionary<int, (int Item, int Qty)[]>
        {
            [Item.PuppetryTobe] = new[]
            {
                (Item.Ruby,                      1),
                (Item.SquareOfMoblinweave,       1),
                (Item.SquareOfScarletLinenCloth, 1),
                (Item.SquareOfWamouraCloth,      1),
                (Item.ImperialGoldPiece,         1),
            },
            [Item.PuppetryDastanas] = new[]
            {
                (Item.PlatinumSheet,          1),
                (Item.SpoolOfRainbowThread,   1),
                (Item.SquareOfMaridLeather,   1),
                (Item.SquareOfWamouraCloth,   1),
                (Item.ImperialMythrilPiece,   1),
            },
            [Item.PuppetryBabouches] = new[]
            {
                (Item.PlatinumSheet,          1),
                (Item.Ruby,                   1),
                (Item.SquareOfMaridLeather,   1),
                (Item.SquareOfWamouraCloth,   1),
                (Item.ImperialMythrilPiece,   2),
            },
        };

        // orderedPieces[1..3]
        private static readonly int[] OrderedPieces =
            { 0, Item.PuppetryTobe, Item.PuppetryDastanas, Item.PuppetryBabouches };

        private static int CountBits(int mask, int length)
        {
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static void OnOrderPlaced(IQuestContext q)
        {
            if (q.Option >= 1 && q.Option <= 3)
            {
                q.Player.SetCharVar("[PUP]orderId", OrderedPieces[q.Option]);
                q.Player.SetCharVar("[PUP]orderStage", 1);
            }
        }

        private static void OnOrderReceived(IQuestContext q)
        {
            int orderId = q.Player.CharVar("[PUP]orderId");

            if (q.Player.GiveItem(orderId))
            {
                q.Player.IncrementCharVar("[AF]pupCrafted", 1 << PieceBits[orderId]);
                q.Player.SetCharVar("[PUP]orderId", 0);
                q.Player.SetCharVar("[PUP]orderStage", 0);
                q.Player.SetCharVar("[PUP]orderTime", 0);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "PUPPETMASTER_BLUES") >= QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.DhimaPolevhia).OnTrade(q =>
                {
                    // Materials are only accepted while an order is waiting on them.
                    if (q.Player.CharVar("[PUP]orderStage") != 1)
                    {
                        return null;
                    }

                    if (TradeTable.TryGetValue(q.Player.CharVar("[PUP]orderId"), out var recipe) &&
                        q.Trade.Matches(recipe))
                    {
                        return QuestReply.ProgressEvent(795);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int piecesBitmask = q.Player.CharVar("[AF]pupCrafted");
                    int piecesNumber = CountBits(piecesBitmask, 4);
                    int orderId = q.Player.CharVar("[PUP]orderId");
                    int orderStage = q.Player.CharVar("[PUP]orderStage");
                    int orderTime = q.Player.CharVar("[PUP]orderTime");

                    // Trade completed. Check time.
                    if (orderStage == 2)
                    {
                        if (q.Player.VanadielUniqueDay > orderTime)
                        {
                            if (piecesNumber == 2)
                            {
                                return QuestReply.ProgressEvent(793); // Order is ready. Last time.
                            }

                            return QuestReply.ProgressEvent(792); // Order is ready. First and second time.
                        }

                        return QuestReply.Event(796); // Order is not ready.
                    }
                    // Placed order. Waiting for trade.
                    else if (orderStage == 1)
                    {
                        // pieceBits[orderId]: nil (no parameter) for an orderId that is no piece, a GM's
                        return QuestReply.Event(790, 0, 0, 0, PieceBits.TryGetValue(orderId, out int bit) ? bit : 0);
                    }
                    // Has made all pieces already.
                    else if (piecesNumber == 3)
                    {
                        return QuestReply.Event(788).ReplaceDefault();
                    }
                    // Place order. (Not first time).
                    else if (piecesNumber > 0)
                    {
                        return QuestReply.ProgressEvent(791, 0, piecesBitmask);
                    }

                    // Place order. (First time)
                    return QuestReply.ProgressEvent(789);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 789, OnOrderPlaced)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 791, OnOrderPlaced)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 792, OnOrderReceived)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 793, OnOrderReceived)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 795, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar("[PUP]orderStage", 2);
                    q.Player.SetCharVar("[PUP]orderTime", q.Player.VanadielUniqueDay);
                });
        }
    }
}
