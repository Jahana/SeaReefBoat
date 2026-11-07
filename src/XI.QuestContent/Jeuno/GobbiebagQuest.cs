// The Gobbiebag Parts I-X: the template xi.jeuno.helpers.GobbiebagQuest (scripts/quests/jeuno/helpers.lua:8-113), a
// line-for-line port (quest rework B3b). Each of the ten Lua files (scripts/quests/jeuno/The_Gobbiebag_Part_<n>.lua)
// is a params table handed to GobbiebagQuest:new; here each is a Part of Parts, and each quest a sealed subclass naming
// its part (the quest classes are found by reflection: the abstract base is skipped). Bluffnix : !pos -43 6 -115 245.
// Available while reqsMet (Jeuno fame level >= the part's, the inventory exactly the part's start size, the previous part
// completed): Bluffnix's 43 (size + 1, QUEST_AVAILABLE, 1); its finish with option 0 begins it. Accepted while reqsMet:
// a trade of exactly the part's four items, or exactly a Bowl of Goblin Stew (880): 73 (size + 5 + 1); any other trade
// and the trigger: 43 (size + 1, QUEST_ACCEPTED, 1). 73's finish: completed (the part's title, if any), the inventory +5,
// the Mog Satchel +5 when the char has one, INVENTORY_INCREASED, the trade confirmed.
// Kept as the Lua has it: the trigger's third parameter recomputes reqsMet, which is always true where the section
// matches (1); the reward of a part without a title is an empty table (no reward table: no b.Reward); the accepted
// section's trade answers 43 to any other trade, even one item.
// Bluffnix has no script; his DefaultActions event is 43 with 81 (no section). Lure of the Wildcat (Jeuno)'s 10056 (bit
// 12 unset) is a Progress event too: it ties with this 43 and the two play in turn (the container order first, then the
// action after the last event id played).
// The era module modules/era/lua/quests/jeuno/the_gobbiebag_part_1_to_10.lua (Modules/Era/Jeuno/TheGobbiebagPart1To10.cs)
// replaces states 1 and 2's checks with its own fame levels and adds a third state; it reads Parts and ReqsMet.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public abstract class GobbiebagQuest : QuestScript
    {
        // one Lua file's params
        public sealed class Part
        {
            public Part(string constName, string luaFile, string prerequisite, int startInventorySize, int fame, int[] tradeItems, int? title)
            {
                ConstName = constName;
                LuaFile = luaFile;
                Prerequisite = prerequisite;
                StartInventorySize = startInventorySize;
                Fame = fame;
                TradeItems = tradeItems;
                Title = title;
            }

            // params.questId (its xi.quest.id.jeuno name)
            public string ConstName { get; }
            public string LuaFile { get; }
            // params.prerequisite: the previous part (null for Part I)
            public string Prerequisite { get; }
            public int StartInventorySize { get; }
            public int Fame { get; }
            public int[] TradeItems { get; }
            // params.reward.title (null: the reward table is empty)
            public int? Title { get; }
            // params.tradeStew: the same for every part
            public int TradeStew => Item.BowlOfGoblinStew880;
        }

        public static readonly System.Collections.Generic.IReadOnlyList<Part> Parts = new Part[]
        {
            new Part("THE_GOBBIEBAG_PART_I", "The_Gobbiebag_Part_I", null, 30, 1,
                new[] { Item.SquareOfDhalmelLeather, Item.SteelIngot, Item.SquareOfLinenCloth, Item.Peridot }, null),
            new Part("THE_GOBBIEBAG_PART_II", "The_Gobbiebag_Part_II", "THE_GOBBIEBAG_PART_I", 35, 1,
                new[] { Item.SquareOfRamLeather, Item.MythrilIngot, Item.SquareOfWoolCloth, Item.Turquoise }, null),
            new Part("THE_GOBBIEBAG_PART_III", "The_Gobbiebag_Part_III", "THE_GOBBIEBAG_PART_II", 40, 1,
                new[] { Item.SquareOfBlackTigerLeather, Item.GoldIngot, Item.SquareOfVelvetCloth, Item.Painite }, null),
            new Part("THE_GOBBIEBAG_PART_IV", "The_Gobbiebag_Part_IV", "THE_GOBBIEBAG_PART_III", 45, 1,
                new[] { Item.CermetChunk, Item.DarksteelIngot, Item.SquareOfSilkCloth, Item.Goshenite }, null),
            new Part("THE_GOBBIEBAG_PART_V", "The_Gobbiebag_Part_V", "THE_GOBBIEBAG_PART_IV", 50, 3,
                new[] { Item.Rhodonite, Item.PaktongIngot, Item.SquareOfMoblinweave, Item.SquareOfBugardLeather }, Title.Greedalox),
            new Part("THE_GOBBIEBAG_PART_VI", "The_Gobbiebag_Part_VI", "THE_GOBBIEBAG_PART_V", 55, 3,
                new[] { Item.ShakudoIngot, Item.SquareOfBallonCloth, Item.Iolite, Item.HighQualityEftSkin }, null),
            new Part("THE_GOBBIEBAG_PART_VII", "The_Gobbiebag_Part_VII", "THE_GOBBIEBAG_PART_VI", 60, 4,
                new[] { Item.SquareOfLynxLeather, Item.AdamanIngot, Item.SquareOfRainbowCloth, Item.Deathstone }, null),
            new Part("THE_GOBBIEBAG_PART_VIII", "The_Gobbiebag_Part_VIII", "THE_GOBBIEBAG_PART_VII", 65, 4,
                new[] { Item.SquareOfSmilodonLeather, Item.ElectrumIngot, Item.SquareOfCilice, Item.Angelstone }, null),
            new Part("THE_GOBBIEBAG_PART_IX", "The_Gobbiebag_Part_IX", "THE_GOBBIEBAG_PART_VIII", 70, 5,
                new[] { Item.OrichalcumIngot, Item.SquareOfPeisteLeather, Item.SquareOfOilSoakedCloth, Item.OxbloodOrb }, null),
            new Part("THE_GOBBIEBAG_PART_X", "The_Gobbiebag_Part_X", "THE_GOBBIEBAG_PART_IX", 75, 5,
                new[] { Item.MolybdenumIngot, Item.SquareOfGriffonLeather, Item.SquareOfFoulard, Item.AngelSkinOrb }, Title.GrandGreedalox),
        };

        // xi.inv.INVENTORY / xi.inv.MOGSATCHEL
        public const int Inventory = 0;
        public const int MogSatchel = 5;
        // xi.questStatus.QUEST_AVAILABLE / QUEST_ACCEPTED, as event parameters
        const int QuestAvailable = 0;
        const int QuestAccepted = 1;
        const int BagIncrease = 5;

        readonly Part _params;

        protected GobbiebagQuest(Part part) : base(QuestLog.Jeuno, part.ConstName)
        {
            _params = part;
        }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/" + _params.LuaFile;

        // If quest is available or accepted, the correct dialogue ID is the expected pre quest inventory size offset by 1
        private static int PendingDialogueId(IQuestPlayerView player) => player.ContainerSize(Inventory) + 1;

        // If quest is completed, the correct dialogue ID is the expected post quest inventory size offset by 1
        static int CompleteDialogueId(IQuestPlayerView player) => player.ContainerSize(Inventory) + BagIncrease + 1;

        // getReqsMet, with the fame level as a parameter: the quest's own (params.fame), or the era module's
        public static bool ReqsMet(IQuestPlayerView player, Part part, int fame)
        {
            return player.FameLevel(FameArea.Jeuno) >= fame &&
                player.ContainerSize(Inventory) == part.StartInventorySize &&
                (part.Prerequisite == null || player.HasCompletedQuest(QuestLog.Jeuno, part.Prerequisite));
        }

        protected override void Build(QuestScriptBuilder b)
        {
            var p = _params;

            if (p.Title != null)
            {
                b.Reward(new QuestReward { Title = p.Title });
            }

            b.State(QuestStatus.Available, when: q => ReqsMet(q.Player, p, p.Fame))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Bluffnix).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(43, PendingDialogueId(q.Player), QuestAvailable, ReqsMet(q.Player, p, p.Fame) ? 1 : 0);
                })
                .OnEventFinish(Zone.LowerJeuno, 43, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => ReqsMet(q.Player, p, p.Fame))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Bluffnix)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(p.TradeItems) ||
                            q.Trade.HasExactly(p.TradeStew))
                        {
                            return QuestReply.ProgressEvent(73, CompleteDialogueId(q.Player));
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(43, PendingDialogueId(q.Player), QuestAccepted, 1);
                        }
                    })
                    .OnTrigger(q =>
                    {
                        return QuestReply.ProgressEvent(43, PendingDialogueId(q.Player), QuestAccepted, 1);
                    })
                .OnEventFinish(Zone.LowerJeuno, 73, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.ChangeContainerSize(Inventory, BagIncrease);

                        if (q.Player.ContainerSize(MogSatchel) > 0)
                        {
                            q.Player.ChangeContainerSize(MogSatchel, BagIncrease);
                        }

                        q.Text.Special(Text.LowerJeuno.INVENTORY_INCREASED);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
