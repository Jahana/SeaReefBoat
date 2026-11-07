// Olduum (Log ID 6, Quest ID 2), a line-for-line port of scripts/quests/ahtUrhgan/Olduum.lua (quest rework B3b Aht
// Urhgan, group 10). Aht Urhgan Whitegate: Dkhaaya !pos -73.2 0 -5.8 (no script, no default). Aydeewa Subterrane: three
// Excavation Sites (no script; default NOTHING_HAPPENS). Wajaom Woodlands: the Leypoint !pos -200 -8.5 80.1 (no script;
// default LEYPOINT). The cells: keyItems = { ELECTROCELL, ELECTROPOT, ELECTROLOCOMOTIVE }, Prog their index;
// getQuestKeyItem is the first of them held, else NONE (0). Available: Dkhaaya 4, whose finish gives Dkhaaya's Research
// Journal and begins it. Accepted: Dkhaaya 6 ([0] = the cell) with a cell, else 5 (an Event); 6's update on option 1:
// updateEvent(keyItems[Prog]); 6's finish completes it (a Lightning Band) and takes keyItems[Prog]. An Excavation Site,
// with no Olduum Ring, no cell and a trade of exactly one Pickaxe: randomInt(1, 100) <= 50: Prog = randomInt(1, 3), 0
// ([0] = keyItems[Prog]); else the local var mineFail 1, 0 ([1] = 1); 0's finish: mineFail clears it, else
// keyItems[Prog] is added (no message); the trade completed either way. Completed: Dkhaaya: with the ring, the band or
// Wait in the future: 7; else with Reissue 1: 8 ([0] = the cell) with a cell, 7 ([7] = 2) without; else 7 ([7] = 1).
// 7's finish on option 99 with none of ring, band, cell or Wait: Reissue 1. 8's update on option 1:
// updateEvent(keyItems[Prog]); 8's finish with a cell, Reissue 1, none of ring, band, Wait: the band (when it fits),
// the cell taken, Prog 0, Reissue 0. An Excavation Site with Reissue 1, Wait past, no ring, band or cell and one
// Pickaxe: the same draw as above. The Leypoint: a trade of the band with no ring: ITEM_CANNOT_BE_OBTAINED (the ring)
// with no free slot, else 2; with the band held, a talk: LEYPOINT + 1 (the band); 2's finish: with no ring and the ring
// given, the trade completed and Wait = now + 60 (the era module olduum: now + 86400). Kept as the Lua has it: 4 begins
// it on any option; keyItems[Prog] outside 1..3 is a Lua nil, which only a GM reaches (updateEvent(nil) sends no
// parameter: UpdateEvent()); the mining draws come in the trade handler (before the lookup picks an action); 0's finish
// completes the trade whatever was drawn; the accepted section has no Leypoint; 7 is played with no parameter when the
// ring, band or Wait holds. Not as the Lua has it, but as it ends: the Lua's addKeyItem(nil) / delKeyItem(nil) raise an
// argument error, which the port throws (InvalidOperationException) at the same point: 0's finish stops before the
// trade is completed (the Pickaxe kept), 6's after the completion (the cell kept). No flow shows it: the recorder
// refuses a run with a script error (the pins in AhtUrhganGroup10.cs do).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class Olduum : QuestScript
    {
        public Olduum() : base(QuestLog.AhtUrghan, "OLDUUM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Olduum";

        static readonly int[] KeyItems =
        {
            KeyItem.Electrocell,
            KeyItem.Electropot,
            KeyItem.Electrolocomotive,
        };

        // keyItems[prog]: nil (0 here) outside 1..3
        static int KeyItemAt(int prog) => prog >= 1 && prog <= KeyItems.Length ? KeyItems[prog - 1] : 0;

        // player:addKeyItem(cell) / player:delKeyItem(cell), the cell keyItems[Prog]: for a Prog outside 1..3 the Lua
        // passes nil, which the binding refuses (KeyItemWrite's ToNumberArg: a LuaArgumentException), so the handler
        // stops there
        static int CellOrRaise(int prog, string call)
        {
            int cell = KeyItemAt(prog);
            return cell != 0
                ? cell
                : throw new System.InvalidOperationException(
                    $"keyItems[{prog}] is nil: player:{call}(nil) raises (bad argument #1, a number expected)");
        }

        static int GetQuestKeyItem(IQuestContext q)
        {
            foreach (int keyItem in KeyItems)
            {
                if (q.Player.HasKeyItem(keyItem))
                {
                    return keyItem;
                }
            }

            return KeyItem.None;
        }

        // player:updateEvent(keyItems[quest:getVar(player, 'Prog')]): a nil sends no parameter
        static void UpdateWithCell(IQuestContext q)
        {
            int cell = KeyItemAt(q.Quest.Step);
            if (cell != 0)
            {
                q.Player.UpdateEvent(cell);
            }
            else
            {
                q.Player.UpdateEvent();
            }
        }

        // the mining draw, both sections: math.randomInt(1, 100) <= 50, then Prog = math.randomInt(1, 3)
        static QuestReply Mine(IQuestContext q)
        {
            if (q.Player.RandomInt(1, 100) <= 50)
            {
                // quest:setVar(player, 'Prog', math.randomInt(1, 3)), then keyItems[quest:getVar(player, 'Prog')]: the
                // value set
                int drawn = q.Player.RandomInt(1, 3);
                q.Quest.SetStep(drawn);

                return QuestReply.ProgressEvent(0).Option(0, KeyItemAt(drawn));
            }
            else
            {
                q.Player.SetLocalVar("mineFail", 1);
                return QuestReply.ProgressEvent(0).Option(1, 1);
            }
        }

        static void MineFinish(IQuestContext q)
        {
            if (q.Player.LocalVar("mineFail") == 1)
            {
                q.Player.SetLocalVar("mineFail", 0);
            }
            else
            {
                // player:addKeyItem(keyItems[Prog]): no message; a nil cell raises before tradeComplete
                q.Player.AddKeyItem(CellOrRaise(q.Quest.Step, "addKeyItem"));
            }

            q.Trade.Complete();
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.LightningBand,
            });
            b.Steps(0, 1, 2, 3);

            // Section: Begin quest
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Dkhaaya)
                    .OnTrigger(q => QuestReply.ProgressEvent(4))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 4, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.DkhaayasResearchJournal);
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Dkhaaya).OnTrigger(q =>
                {
                    int questKeyItem = GetQuestKeyItem(q);

                    if (questKeyItem > 0)
                    {
                        return QuestReply.ProgressEvent(6).Option(0, questKeyItem);
                    }
                    else
                    {
                        return QuestReply.Event(5);
                    }
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 6, q =>
                {
                    if (q.Option == 1)
                    {
                        UpdateWithCell(q);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 6, q =>
                {
                    // local cellKeyItem = keyItems[quest:getVar(player, 'Prog')], read before complete clears Prog
                    int prog = q.Quest.Step;

                    if (q.Quest.Complete())
                    {
                        // player:delKeyItem(cellKeyItem): a nil cell (Prog outside 1..3, only by a GM) raises after the
                        // completion
                        q.Player.DelKeyItem(CellOrRaise(prog, "delKeyItem"));
                    }
                })
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.ExcavationSite).OnTrade(q =>
                {
                    if (!q.Player.HasItem(Item.OlduumRing) &&
                        GetQuestKeyItem(q) == KeyItem.None &&
                        q.Trade.Matches((Item.Pickaxe, 1)))
                    {
                        return Mine(q);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 0, MineFinish);

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Dkhaaya).OnTrigger(q =>
                {
                    if (q.Player.HasItem(Item.OlduumRing) ||
                        q.Player.HasItem(Item.LightningBand) ||
                        q.Quest.Var("Wait") > q.Player.SystemTime)
                    {
                        return QuestReply.ProgressEvent(7);
                    }

                    if (q.Quest.Var("Reissue") == 1)
                    {
                        int questKeyItem = GetQuestKeyItem(q);

                        if (questKeyItem > 0)
                        {
                            return QuestReply.ProgressEvent(8).Option(0, questKeyItem);
                        }

                        return QuestReply.ProgressEvent(7).Option(7, 2);
                    }

                    return QuestReply.ProgressEvent(7).Option(7, 1);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 8, q =>
                {
                    if (q.Option == 1)
                    {
                        UpdateWithCell(q);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 7, q =>
                {
                    if (q.Option != 99 ||
                        q.Player.HasItem(Item.OlduumRing) ||
                        q.Player.HasItem(Item.LightningBand) ||
                        GetQuestKeyItem(q) > 0 ||
                        q.Quest.Var("Wait") > q.Player.SystemTime)
                    {
                        return;
                    }

                    q.Quest.SetVar("Reissue", 1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 8, q =>
                {
                    int questKeyItem = GetQuestKeyItem(q);

                    if (questKeyItem == KeyItem.None ||
                        q.Player.HasItem(Item.OlduumRing) ||
                        q.Player.HasItem(Item.LightningBand) ||
                        q.Quest.Var("Reissue") != 1 ||
                        q.Quest.Var("Wait") > q.Player.SystemTime)
                    {
                        return;
                    }

                    if (!q.Player.GiveItem(Item.LightningBand))
                    {
                        return;
                    }

                    q.Player.DelKeyItem(questKeyItem);
                    q.Quest.SetStep(0);
                    q.Quest.SetVar("Reissue", 0);
                })
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.ExcavationSite).OnTrade(q =>
                {
                    if (q.Quest.Var("Reissue") == 1 &&
                        q.Quest.Var("Wait") <= q.Player.SystemTime &&
                        !q.Player.HasItem(Item.OlduumRing) &&
                        !q.Player.HasItem(Item.LightningBand) &&
                        GetQuestKeyItem(q) == KeyItem.None &&
                        q.Trade.Matches((Item.Pickaxe, 1)))
                    {
                        return Mine(q);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 0, MineFinish)
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.Leypoint).OnTrade(q =>
                {
                    if (!q.Player.HasItem(Item.OlduumRing) && q.Trade.Matches((Item.LightningBand, 1)))
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            // zones[player:getZoneID()].text.ITEM_CANNOT_BE_OBTAINED: the handler's zone, Wajaom
                            return QuestReply.MessageSpecial(Text.WajaomWoodlands.ITEM_CANNOT_BE_OBTAINED,
                                Item.OlduumRing);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(2);
                        }
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.HasItem(Item.LightningBand))
                    {
                        // zones[player:getZoneID()].text.LEYPOINT + 1
                        return QuestReply.MessageSpecial(q.Text.Id(Text.WajaomWoodlands.LEYPOINT) + 1,
                            Item.LightningBand);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WajaomWoodlands, 2, q =>
                {
                    if (!q.Player.HasItem(Item.OlduumRing) && q.Player.GiveItem(Item.OlduumRing))
                    {
                        q.Trade.Complete();
                        q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute
                    }
                });
        }
    }
}
