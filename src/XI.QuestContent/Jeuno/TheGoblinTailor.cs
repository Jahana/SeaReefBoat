// The Goblin Tailor (Log ID 3, Quest ID 42), a line-for-line port of scripts/quests/jeuno/The_Goblin_Tailor.lua (quest
// rework B3b). Guttrix : !pos -36.010 4.499 -139.714 245. The race-specific equipment (RSE): the week's race
// (VanadielRSERace(), 1-8 as xi.race) and location (VanadielRSELocation(): 0 Ordelle's Caves, 1 Gusgen Mines, 2 the Maze
// of Shakhrami) turn each Vana'diel week. Available: Guttrix's 10016 (location, race) from level 10 at Jeuno fame level 3
// (its finish begins the quest), else 10020. Accepted or completed (status >= QUEST_ACCEPTED): the week's location's
// Treasure Chest, for a char of the week's race without the Magical Pattern, opens to the pattern (xi.treasure.onTrade
// with the key item as its bypass reward) and the lookup does nothing more (quest:noAction()); Guttrix: 10019 once all
// four pieces are made ([RSE]Gear 15), 10020 below level 10, 10018 ([RSE]Gear) with the pattern, else 10017 (location,
// race). 10018's finish with the pattern and option 1-4 whose piece is not yet made: the char's race's piece (body,
// hands, legs, feet), the pattern taken, its bit of [RSE]Gear set, completed (again: Goblin's Exclusive Fashion
// Mannequin) and fame +5 in the three nations.
// Kept as the Lua has it: quest:complete runs on every piece, the first completing the quest and the later ones on a
// completed quest; the Taru male and female rows give the same pieces; the chests' handlers run while the quest is
// completed too. Guttrix has no script and no default; a quest nil at a chest falls to the chest's own script (its
// key opens it to its loot).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheGoblinTailor : QuestScript
    {
        public TheGoblinTailor() : base(QuestLog.Jeuno, "THE_GOBLIN_TAILOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Goblin_Tailor";

        // xi.treasure.onTrade's bypass type: a key item
        const int BypassKeyItem = 2;

        // [race] = { body, hands, legs, feet } (xi.race: 1 Hume male ... 8 Galka)
        static readonly int[][] RseTable =
        {
            null,
            new[] { Item.CustomTunic, Item.CustomMGloves, Item.CustomSlacks, Item.CustomMBoots },         // HUME_M
            new[] { Item.CustomVest, Item.CustomFGloves, Item.CustomPants, Item.CustomFBoots },           // HUME_F
            new[] { Item.MagnaJerkin, Item.MagnaGauntlets, Item.MagnaMChausses, Item.MagnaMLedelsens },   // ELVAAN_M
            new[] { Item.MagnaBodice, Item.MagnaGloves, Item.MagnaFChausses, Item.MagnaFLedelsens },      // ELVAAN_F
            new[] { Item.WonderKaftan, Item.WonderMitts, Item.WonderBraccae, Item.WonderClomps },         // TARU_M
            new[] { Item.WonderKaftan, Item.WonderMitts, Item.WonderBraccae, Item.WonderClomps },         // TARU_F
            new[] { Item.SavageSeparates, Item.SavageGauntlets, Item.SavageLoincloth, Item.SavageGaiters }, // MITHRA
            new[] { Item.EldersSurcoat, Item.EldersBracers, Item.EldersBraguette, Item.EldersSandals },    // GALKA
        };

        // utils.mask.getBit(mask, bit)
        static bool GetBit(int mask, int bit) => (mask & (1 << bit)) != 0;

        // the three chests' trade: the same handler, each at its week's location
        static QuestReply ChestTrade(IQuestContext q, int location)
        {
            if (q.Player.VanadielRseLocation == location &&
                q.Player.VanadielRseRace == q.Player.Race &&
                !q.Player.HasKeyItem(KeyItem.MagicalPattern))
            {
                q.Trade.TreasureTrade(BypassKeyItem, KeyItem.MagicalPattern);

                return QuestReply.NoAction();
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.GoblinsExclusiveFashionMannequin,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Guttrix).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 10 &&
                        q.Player.FameLevel(FameArea.Jeuno) >= 3)
                    {
                        return QuestReply.ProgressEvent(10016, q.Player.VanadielRseLocation, q.Player.VanadielRseRace);
                    }
                    else
                    {
                        return QuestReply.Event(10020);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10016, q =>
                {
                    q.Quest.Begin();
                });

            // status >= xi.questStatus.QUEST_ACCEPTED
            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.GusgenMines, Npc.GusgenMines.TreasureChest).OnTrade(q => ChestTrade(q, 1))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Guttrix).OnTrigger(q =>
                {
                    int rseTracker = q.Player.CharVar("[RSE]Gear");
                    if (rseTracker == 15)
                    {
                        return QuestReply.Event(10019);
                    }

                    if (q.Player.MainLevel < 10)
                    {
                        return QuestReply.Event(10020);
                    }

                    if (q.Player.HasKeyItem(KeyItem.MagicalPattern))
                    {
                        return QuestReply.ProgressEvent(10018, rseTracker);
                    }
                    else
                    {
                        return QuestReply.Event(10017, q.Player.VanadielRseLocation, q.Player.VanadielRseRace);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10018, q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MagicalPattern))
                    {
                        return;
                    }

                    if (q.Option < 1)
                    {
                        return;
                    }

                    if (q.Option > 4)
                    {
                        return;
                    }

                    int rseTracker = q.Player.CharVar("[RSE]Gear");
                    if (GetBit(rseTracker, q.Option - 1))
                    {
                        return;
                    }

                    if (q.Player.GiveItem(RseTable[q.Player.Race][q.Option - 1]))
                    {
                        q.Player.DelKeyItem(KeyItem.MagicalPattern);
                        q.Player.IncrementCharVar("[RSE]Gear", 1 << (q.Option - 1));

                        q.Quest.Complete();
                        q.Player.AddFame(FameArea.Sandoria, 5);
                        q.Player.AddFame(FameArea.Bastok, 5);
                        q.Player.AddFame(FameArea.Windurst, 5);
                    }
                })
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.TreasureChest).OnTrade(q => ChestTrade(q, 2))
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.TreasureChest).OnTrade(q => ChestTrade(q, 0));
        }
    }
}
