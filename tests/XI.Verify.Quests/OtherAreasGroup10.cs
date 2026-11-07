// XI.Verify.Quests -- quest rework B3b Other Areas group 10 (call-only: Orlando's Antiques, A Potter's Preference,
// Donate to Recycling, Only the Best, Cargo): what the golden traces cannot show.
// - The reply kinds. The five quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an
//   Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply):
//   there is no lookup action, so no priority to tell them apart, and every trace replays the same with any of the
//   three. The Lua scripts call player:startEvent, so each reply is pinned here as Event.
// - The trades that answer nothing. Each script is the call alone and none of the five NPCs has a DefaultActions entry,
//   so a NoAction in place of null would look the same in every trace: each "nothing" arm is pinned here as null.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup10()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 10: the call-mode reply kinds, the trades that " +
                          "answer nothing ==");
        OA10Orlando();
        OA10Nereus();
        OA10Romeo();
        OA10Melyon();
        OA10Vuntar();
    }

    // the quest's one NPC handler of that kind (each quest has one state of NPC handlers, state 0)
    static string OA10Reply(QuestScriptDefinition def, QuestHandlerKind kind, Fake f) =>
        G2Reply(def.States[0].Handlers.Single(h => h.Kind == kind), f);

    // a fake at that status holding a trade: (item, qty) per slot, in slot order
    static Fake OA10Traded(QuestStatus status, params (int Item, int Qty)[] slots)
    {
        var f = new Fake { Status = status };
        foreach (var (item, qty) in slots)
        {
            f.TradeItems[item] = f.TradeItems.TryGetValue(item, out var n) ? n + qty : qty;
            f.TradeSlots.Add(item);
        }
        return f;
    }

    static void OA10Orlando()
    {
        var def = new QC.OtherAreas.OrlandosAntiques().Compile();
        Fake Talk(QuestStatus status, int fame, bool license)
        {
            var f = new Fake { Status = status };
            f.Fame[QC.FameArea.Windurst] = fame;
            if (license) f.KeyItems[QC.KeyItem.ChocoboLicense] = true;
            return f;
        }
        Fake Coins(QuestStatus status, params (int Item, int Qty)[] slots)
        {
            var f = OA10Traded(status, slots);
            f.MainNumbers["GIL_RATE"] = 1;
            return f;
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var acc = QuestStatus.Accepted;
        Check("oa g10:Orlando's Antiques' replies are events (call mode): 100 / 101 / 103; the trade's 102 / 104; " +
              "null for the trades that answer nothing (available; 8 not all the slot's item; 9 of it; 8 of it and a " +
              "die)",
              "Event(100)|Event(101)|Event(103)|Event(102,600,656)|Event(104)|null|null|null|null",
              string.Join("|",
                  OA10Reply(def, trig, Talk(QuestStatus.Available, 1, true)),
                  OA10Reply(def, trig, Talk(QuestStatus.Available, 2, true)),
                  OA10Reply(def, trig, Talk(acc, 2, true)),
                  OA10Reply(def, trade, Coins(acc, (QC.Item.Beastcoin, 8))),
                  OA10Reply(def, trade, Coins(acc, (QC.Item.Beastcoin, 7))),
                  OA10Reply(def, trade, Coins(QuestStatus.Available, (QC.Item.Beastcoin, 8))),
                  OA10Reply(def, trade, Coins(acc, (QC.Item.Beastcoin, 7), (QC.Item.GoblinDie, 1))),
                  // 9 of it: null through hasItemQty (exact, in the fake since P9 D8 as in the engine); 8 of it and
                  // a die: null through ItemCount, the slot that pins o21 / o22
                  OA10Reply(def, trade, Coins(acc, (QC.Item.Beastcoin, 9))),
                  OA10Reply(def, trade, Coins(acc, (QC.Item.Beastcoin, 8), (QC.Item.GoblinDie, 1)))));
    }

    static void OA10Nereus()
    {
        var def = new QC.OtherAreas.APottersPreference().Compile();
        Fake Talk(QuestStatus status, int fame = 6, int day = 0)
        {
            var f = new Fake { Status = status, UniqueDay = day };
            f.Fame[QC.FameArea.Windurst] = fame;
            return f;
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        Check("oa g10:A Potter's Preference's replies are events (call mode): 110 / 111 / 114 / 112 / 115; the " +
              "trade's 113; null for the trades that answer nothing (available; accepted without the dish)",
              "Event(110)|Event(111,569)|Event(114,569)|Event(112)|Event(115)|Event(113)|null|null",
              string.Join("|",
                  OA10Reply(def, trig, Talk(QuestStatus.Available, fame: 5)),
                  OA10Reply(def, trig, Talk(QuestStatus.Available)),
                  OA10Reply(def, trig, Talk(QuestStatus.Accepted)),
                  OA10Reply(def, trig, Talk(QuestStatus.Completed, day: 8)),
                  OA10Reply(def, trig, Talk(QuestStatus.Completed, day: 7)),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Accepted, (QC.Item.DishOfGusgenClay, 1))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Available, (QC.Item.DishOfGusgenClay, 1))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Accepted, (QC.Item.LaTheineCabbage, 1)))));
    }

    static void OA10Romeo()
    {
        var def = new QC.OtherAreas.DonateToRecycling().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        Check("oa g10:Donate to Recycling's replies are events (call mode): 20 / 22 / 23; the trade's 21; null for " +
              "the trades that answer nothing (available; 4 swords)",
              "Event(20)|Event(22)|Event(23)|Event(21)|null|null",
              string.Join("|",
                  OA10Reply(def, trig, new Fake { Status = QuestStatus.Available }),
                  OA10Reply(def, trig, new Fake { Status = QuestStatus.Accepted }),
                  OA10Reply(def, trig, new Fake { Status = QuestStatus.Completed }),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Accepted, (QC.Item.OnionSword, 5))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Available, (QC.Item.OnionSword, 5))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Accepted, (QC.Item.OnionSword, 4)))));
    }

    static void OA10Melyon()
    {
        var def = new QC.OtherAreas.OnlyTheBest().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var acc = QuestStatus.Accepted;
        Check("oa g10:Only the Best's replies are events (call mode): 60 / 61; the trade's 62 / 63 / 64; null for " +
              "the trades that answer nothing (available; 4 cabbages)",
              "Event(60,4366,629,919)|Event(61,4366,629,919)|Event(62,0,4366)|Event(63,0,629)|Event(64,0,919)|" +
              "null|null",
              string.Join("|",
                  OA10Reply(def, trig, new Fake { Status = QuestStatus.Available }),
                  OA10Reply(def, trig, new Fake { Status = acc }),
                  OA10Reply(def, trade, OA10Traded(acc, (QC.Item.LaTheineCabbage, 5))),
                  OA10Reply(def, trade, OA10Traded(acc, (QC.Item.EarOfMillioncorn, 3))),
                  OA10Reply(def, trade, OA10Traded(acc, (QC.Item.ClumpOfBoyahdaMoss, 1))),
                  OA10Reply(def, trade, OA10Traded(QuestStatus.Available, (QC.Item.LaTheineCabbage, 5))),
                  OA10Reply(def, trade, OA10Traded(acc, (QC.Item.LaTheineCabbage, 4)))));
    }

    static void OA10Vuntar()
    {
        var def = new QC.OtherAreas.Cargo().Compile();
        Fake Berry(QuestStatus status, int item, int date = 0)
        {
            var f = OA10Traded(status, (item, 1));
            f.Now = 1800000000;
            f.CharVars["VuntarCanBuyItem_date"] = date;
            return f;
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var acc = QuestStatus.Accepted;
        Check("oa g10:Cargo's replies are events (call mode): 50 / 53 / 51; the trade's 52 (1 / 2 / 3) / 1134; null " +
              "for the trades that answer nothing (available; past the date, no berry)",
              "Event(50,4365)|Event(53)|Event(51,4365)|Event(52,1)|Event(52,2)|Event(52,3)|Event(1134,4365)|null|null",
              string.Join("|",
                  OA10Reply(def, trig, new Fake { Status = QuestStatus.Available, MainLevel = 20 }),
                  OA10Reply(def, trig, new Fake { Status = QuestStatus.Available, MainLevel = 19 }),
                  OA10Reply(def, trig, new Fake { Status = acc, MainLevel = 20 }),
                  OA10Reply(def, trade, Berry(acc, QC.Item.Rolanberry881Ce)),
                  OA10Reply(def, trade, Berry(acc, QC.Item.Rolanberry874Ce)),
                  OA10Reply(def, trade, Berry(acc, QC.Item.Rolanberry864Ce)),
                  OA10Reply(def, trade, Berry(acc, QC.Item.Rolanberry881Ce, date: 1800000000)),
                  OA10Reply(def, trade, Berry(QuestStatus.Available, QC.Item.Rolanberry881Ce)),
                  OA10Reply(def, trade, Berry(acc, QC.Item.LaTheineCabbage))));
    }
}
