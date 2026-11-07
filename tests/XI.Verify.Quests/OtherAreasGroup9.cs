// XI.Verify.Quests -- quest rework B3b Other Areas group 9 (the Mog House moogles and the odd ones: Give a Moogle a
// Break, The Moogle's Picnic!, Moogles in the Wild, Monstrosity, An Explorer's Footsteps, Waking the Beast): the new
// facet members, the arms no trace reaches, the reply kinds no trace can tell apart, and the zones no flow visits.
// - Facets: Player.InMogHouseInHomeNation, Player.SetMonstrosityData, Player.MonstrosityUpdateIntroEvent and
//   Trade.CompleteChecked on the fake: `oa g9 facets`.
// - Trade complete: Monstrosity's trade finish reads `not player:tradeComplete()`, which is true only when the char
//   holds no trade (or keeps part of it); the finish always follows the trade's event, so no trace takes it: `oa g9
//   trade complete` takes it on the fake (the local cleared, no ring, no data), with species 0 (no tradeComplete read)
//   and the true arm beside it.
// - Kinds: a Progress and an Event reply play alike where no rival answers and the NPC's script does nothing (Pashhow's
//   Suspicious Hume before completion; the Moogle's trade, whose script's moogleTrade does nothing with these items;
//   Abelard's tablet trade, which no other line answers; the Suspicious NPCs' city trades, whose scripts have no
//   onTrade), so no trace tells them apart: `oa g9 kinds` checks each reply as the Lua writes it.
// - Rewards: the 30008 / 30012 / 30016 finish on quest:complete false and Waking's Option -1: `oa g9 rewards`.
// - Clay: An Explorer's Footsteps' ClayRecieved matters only with neither the clay nor a tablet held in the same zone
//   visit, which no flow step reaches (none drops an item): `oa g9 clay`.
// - Zones: the moogles' sections hold in every one of xi.moghouse.moghouseZones (21; the #zones and #era_zones flows
//   visit each), the era modules patch 30007 / 30011 / 30015 in each, and An Explorer's Footsteps' monuments are
//   monumentTable's 25 zones: `oa g9 zones` lists them.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup9()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 9: the new facets, the unreachable arms, " +
                          "the reply kinds no trace records, the zones ==");
        RunOtherAreasGroup9Facets();
        RunOtherAreasGroup9TradeComplete();
        RunOtherAreasGroup9Kinds();
        RunOtherAreasGroup9Rewards();
        RunOtherAreasGroup9Clay();
        RunOtherAreasGroup9Zones();
    }

    static QuestHandlerDecl OA9Handler(QuestScriptDefinition def, int state, QuestHandlerKind kind, ushort zone,
                                       string npc, int id = 0) =>
        def.States[state].Handlers.Single(h =>
            h.Kind == kind && h.Zone == zone && (npc == null || h.Entity == npc) && (id == 0 || h.Id == id));

    static Fake OA9Traded(QuestStatus status, ushort zone, params int[] items)
    {
        var f = new Fake { Status = status, Zone = zone };
        foreach (int i in items)
        {
            f.TradeItems[i] = f.TradeItems.TryGetValue(i, out var n) ? n + 1 : 1;
            f.TradeSlots.Add(i);
        }
        return f;
    }

    // the fake answers InMogHouseInHomeNation (InMogHouseHome) and CompleteChecked (TradeCompleteResult);
    // SetMonstrosityData and MonstrosityUpdateIntroEvent are recorded
    static void RunOtherAreasGroup9Facets()
    {
        var f = new Fake();
        bool before = f.Player.InMogHouseInHomeNation;
        f.InMogHouseHome = true;
        bool after = f.Player.InMogHouseInHomeNation;
        bool done = f.Trade.CompleteChecked();
        f.TradeCompleteResult = false;
        bool kept = f.Trade.CompleteChecked();
        f.Player.SetMonstrosityData(43, 43);
        f.Player.MonstrosityUpdateIntroEvent();
        Check("oa g9 facets:the fake answers InMogHouseInHomeNation and Trade.CompleteChecked; " +
              "SetMonstrosityData and MonstrosityUpdateIntroEvent are recorded",
              "False/True/True/False|Trade.CompleteChecked();Trade.CompleteChecked();" +
              "Player.SetMonstrosityData(43,43);Player.MonstrosityUpdateIntroEvent()",
              $"{before}/{after}/{done}/{kept}|{string.Join(";", f.Calls)}");
    }

    // Monstrosity's trade finish (883 / 419 / 881): species 0: the local cleared, nothing more; tradeComplete false: no
    // ring, no data; true: the ring and setMonstrosityData(species, species)
    static void RunOtherAreasGroup9TradeComplete()
    {
        var mon = new QC.OtherAreas.Monstrosity().Compile();
        string Run(int species, bool completes)
        {
            var f = new Fake
            {
                Status = QuestStatus.Accepted, Zone = QC.Zone.PortBastok, Csid = 419, TradeCompleteResult = completes,
            };
            f.PlayerLocalVars["MONSTROSITY_UNLOCK"] = species;
            OA9Handler(mon, 1, QuestHandlerKind.OnEventFinish, QC.Zone.PortBastok, null, 419).Run(f);
            return string.Join(";", f.Calls);
        }
        Check("oa g9 trade complete:Monstrosity's 419 finish: species 0: the local cleared only; " +
              "tradeComplete false: no ring, no data; true: both",
              "Player.SetLocalVar(MONSTROSITY_UNLOCK,0)|" +
              "Player.SetLocalVar(MONSTROSITY_UNLOCK,0);Trade.CompleteChecked()|" +
              "Player.SetLocalVar(MONSTROSITY_UNLOCK,0);Trade.CompleteChecked();" +
              $"Player.GiveKeyItem({QC.KeyItem.RingOfSupernalDisjunction});Player.SetMonstrosityData(1,1)",
              $"{Run(0, true)}|{Run(1, false)}|{Run(1, true)}");
    }

    static void RunOtherAreasGroup9Kinds()
    {
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var avail = QuestStatus.Available;
        var acc = QuestStatus.Accepted;

        // Monstrosity's Suspicious Hume: 40 Progress, 41 / 42 PriorityEvent (its script does nothing before completion)
        var mon = new QC.OtherAreas.Monstrosity().Compile();
        string hume = QC.Npc.PashhowMarshlands.SuspiciousHume;
        var ring = new Fake { Status = acc };
        ring.KeyItems[QC.KeyItem.RingOfSupernalDisjunction] = true;
        string humeKinds = string.Join("|",
            G2Reply(OA9Handler(mon, 0, trig, QC.Zone.PashhowMarshlands, hume), new Fake { Status = avail }),
            G2Reply(OA9Handler(mon, 1, trig, QC.Zone.PashhowMarshlands, hume), new Fake { Status = acc }),
            G2Reply(OA9Handler(mon, 1, trig, QC.Zone.PashhowMarshlands, hume), ring));
        Check("oa g9 kinds:Monstrosity's Suspicious Hume 40 Progress, 41 / 42 PriorityEvent",
              "ProgressEvent(40)|PriorityEvent(41)|PriorityEvent(42)",
              humeKinds);

        // the three cities' trade replies (the Suspicious NPCs' scripts have no onTrade, and no other line trades with
        // them): 883 / 419 / 881 Progress, the item the parameter (the fake's Matches answers TradeResult for any
        // entry: the table's first, the lizard tail)
        Fake CityTrade(ushort zone)
        {
            var f = OA9Traded(acc, zone, QC.Item.LizardTail);
            f.TradeResult = true;
            return f;
        }
        string cityTrades = string.Join("|",
            G2Reply(OA9Handler(mon, 1, trade, QC.Zone.NorthernSanDoria, QC.Npc.NorthernSanDoria.SuspiciousElvaan),
                    CityTrade(QC.Zone.NorthernSanDoria)),
            G2Reply(OA9Handler(mon, 1, trade, QC.Zone.PortBastok, QC.Npc.PortBastok.SuspiciousGalka),
                    CityTrade(QC.Zone.PortBastok)),
            G2Reply(OA9Handler(mon, 1, trade, QC.Zone.PortWindurst, QC.Npc.PortWindurst.SuspiciousTarutaru),
                    CityTrade(QC.Zone.PortWindurst)));
        Check("oa g9 kinds:Monstrosity's city trades 883 / 419 / 881 Progress",
              $"ProgressEvent(883,{QC.Item.LizardTail})|ProgressEvent(419,{QC.Item.LizardTail})|" +
              $"ProgressEvent(881,{QC.Item.LizardTail})",
              cityTrades);

        // the three moogle quests' trades at Bastok Mines' Moogle (npcUtil.tradeMatches: the fake's TradeResult): 30007
        // / 30011 / 30015 Progress
        Fake Matched(params int[] items)
        {
            var f = OA9Traded(acc, QC.Zone.BastokMines, items);
            f.TradeResult = true;
            return f;
        }
        string moogle = QC.Npc.BastokMines.Moogle;
        ushort mines = QC.Zone.BastokMines;
        string moogleTrades = string.Join("|",
            G2Reply(OA9Handler(new QC.OtherAreas.GiveAMoogleABreak().Compile(), 1, trade, mines, moogle),
                    Matched(QC.Item.PowerBow, QC.Item.BeetleRing)),
            G2Reply(OA9Handler(new QC.OtherAreas.TheMooglePicnic().Compile(), 1, trade, mines, moogle),
                    Matched(QC.Item.ShrimpLure, QC.Item.StickOfSelbinaButter)),
            G2Reply(OA9Handler(new QC.OtherAreas.MooglesInTheWild().Compile(), 1, trade, mines, moogle),
                    Matched(QC.Item.RaptorMantle, QC.Item.WoolHat)));
        Check("oa g9 kinds:the Moogle's trades 30007 / 30011 / 30015 Progress",
              "ProgressEvent(30007)|ProgressEvent(30011)|ProgressEvent(30015)",
              moogleTrades);

        // An Explorer's Footsteps' Abelard trade: 45 Event (the bit set), 47 / 41 / 46 Progress; the monument's trade
        // nil
        var aef = new QC.OtherAreas.AnExplorersFootsteps().Compile();
        string abelard = QC.Npc.Selbina.Abelard;
        ushort selbina = QC.Zone.Selbina;
        Fake Tablet(int bitmask, int count, int target)
        {
            var f = OA9Traded(acc, selbina, QC.Item.ClayTablet);
            f.Vars["[EF]TabletZoneId"] = QC.Zone.WestRonfaure;
            f.Vars["[EF]TargetMonument"] = target;
            f.CharVars["[EF]MonumentBitmask"] = bitmask;
            f.CharVars["[EF]MonumentCount"] = count;
            return f;
        }
        var clay = OA9Traded(acc, QC.Zone.WestRonfaure, QC.Item.LumpOfSelbinaClay);
        string aefTrades = string.Join("|",
            G2Reply(OA9Handler(aef, 1, trade, selbina, abelard), Tablet(1, 1, 0)),
            G2Reply(OA9Handler(aef, 1, trade, selbina, abelard), Tablet(0x1FFFE, 16, 1)),
            G2Reply(OA9Handler(aef, 1, trade, selbina, abelard), Tablet(0, 0, 0)),
            G2Reply(OA9Handler(aef, 1, trade, selbina, abelard), Tablet(0, 0, 1)),
            G2Reply(OA9Handler(aef, 1, trade, QC.Zone.WestRonfaure, QC.Npc.WestRonfaure.StoneMonument), clay));
        Check("oa g9 kinds:An Explorer's Footsteps' Abelard trade 45 Event, 47 / 41 / 46 Progress; " +
              "the Stone Monument's trade nil",
              "Event(45)|ProgressEvent(47)|ProgressEvent(41)|ProgressEvent(46)|null",
              aefTrades);
    }

    // the rewards' arms no trace takes: the moogle quests' finish on quest:complete false (the reward is a title only,
    // which cannot fail: nothing grows) beside the true arm (the title, both safes + 10; the traces show those through
    // char_stats.title and char_storage); Waking the Beast's pole and its 208 finish's title by Option. User ruling
    // 2026-11-04 (kept Lua bugs): the C# reads Option before quest:complete, whose cleanup clears it (the Lua read it
    // after, always 0); a fake whose Complete clears the vars, as the live cleanup does, shows the read's order
    static void RunOtherAreasGroup9Rewards()
    {
        string Finish(QuestScriptDefinition def, int csid, bool completes)
        {
            var f = new Fake
            {
                Status = QuestStatus.Accepted, Zone = QC.Zone.BastokMines, Csid = csid, CompleteResult = completes,
            };
            OA9Handler(def, 1, QuestHandlerKind.OnEventFinish, QC.Zone.BastokMines, null, csid).Run(f);
            return $"{def.Reward.Title}:{string.Join(";", f.Calls)}";
        }
        var give = new QC.OtherAreas.GiveAMoogleABreak().Compile();
        var picnic = new QC.OtherAreas.TheMooglePicnic().Compile();
        var wild = new QC.OtherAreas.MooglesInTheWild().Compile();
        string grow = "Quest.Complete();Player.ChangeContainerSize(1,10);Player.ChangeContainerSize(9,10)";
        Check("oa g9 rewards:the moogle quests' titles; 30008 / 30012 / 30016 grow the Mog Safe and Mog Safe 2 by 10 " +
              "on complete only",
              $"{QC.Title.MogsKindMaster}:{grow}|{QC.Title.MogsExceptionallyKindMaster}:{grow}|" +
              $"{QC.Title.MogsLovingMaster}:{grow}|{QC.Title.MogsKindMaster}:Quest.Complete()",
              string.Join("|", Finish(give, 30008, true), Finish(picnic, 30012, true), Finish(wild, 30016, true),
                  Finish(give, 30008, false)));

        var wtb = new QC.OtherAreas.WakingTheBeast().Compile();
        string Ruby(int option)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Zone = QC.Zone.LaTheinePlateau, Csid = 208 };
            f.Vars["Option"] = option;
            OA9Handler(wtb, 1, QuestHandlerKind.OnEventFinish, QC.Zone.LaTheinePlateau, null, 208).Run(f);
            return string.Join(";", f.Calls.Where(c => c.Contains("AddTitle")));
        }
        Check("oa g9 rewards:Waking the Beast's pole; 208's title by the Option read: 0 Disturber of Slumber, " +
              "1 and -1 Interrupter of Dreams",
              $"{QC.Item.CarbunclesPole}|Player.AddTitle({QC.Title.DisturberOfSlumber})|" +
              $"Player.AddTitle({QC.Title.InterrupterOfDreams})|Player.AddTitle({QC.Title.InterrupterOfDreams})",
              $"{wtb.Reward.Item}|{Ruby(0)}|{Ruby(1)}|{Ruby(-1)}");
        var cleared = new CompleteClearsVarsFake
        {
            Status = QuestStatus.Accepted, Zone = QC.Zone.LaTheinePlateau, Csid = 208,
        };
        cleared.Vars["Option"] = 1;
        OA9Handler(wtb, 1, QuestHandlerKind.OnEventFinish, QC.Zone.LaTheinePlateau, null, 208).Run(cleared);
        Check("oa g9 rewards:Waking the Beast's 208 reads Option before quest:complete clears it (user ruling " +
              "2026-11-04): Option 1, Interrupter of Dreams",
              $"Player.AddTitle({QC.Title.InterrupterOfDreams})",
              string.Join(";", cleared.Calls.Where(c => c.Contains("AddTitle"))));
    }

    // An Explorer's Footsteps' ClayRecieved: set wherever a clay is given (abelardAccept on 40 / 42 option 100, the 44
    // and 45 finishes) and only then. It matters only when the char then holds neither the clay nor a tablet in the
    // same zone visit (44 is then refused), which a flow cannot reach: no step drops an item
    static void RunOtherAreasGroup9Clay()
    {
        var aef = new QC.OtherAreas.AnExplorersFootsteps().Compile();
        string Finish(int state, int csid, int option, bool gives)
        {
            var f = new Fake
            {
                Status = state == 0 ? QuestStatus.Available : QuestStatus.Accepted, Zone = QC.Zone.Selbina,
                Csid = csid, Option = option, GiveResult = gives,
            };
            OA9Handler(aef, state, QuestHandlerKind.OnEventFinish, QC.Zone.Selbina, null, csid).Run(f);
            return string.Join(";",
                f.Calls.Where(c => c.Contains("ClayRecieved") || c.Contains("GiveItem") || c.Contains("Confirm")));
        }
        string clay = $"Player.GiveItem({QC.Item.LumpOfSelbinaClay})";
        string set = "Player.SetLocalVar([EF]ClayRecieved,1)";
        Check("oa g9 clay:ClayRecieved set where a clay is given (40 / 42 option 100, 44, 45) and not when the give " +
              "fails",
              $"{clay};{set}|{clay}|{clay};{set}|{clay};{set}|{clay}|{clay};Trade.Confirm();{set}|{clay}",
              string.Join("|", Finish(0, 40, 100, true), Finish(0, 40, 100, false), Finish(0, 42, 100, true),
                  Finish(1, 44, 0, true), Finish(1, 44, 0, false), Finish(1, 45, 0, true), Finish(1, 45, 0, false)));
    }

    // the moogles' handlers in each Mog House zone (xi.moghouse.moghouseZones, in its order), each era module's 30007 /
    // 30011 / 30015 finish in each; the Stone Monuments' trade in monumentTable's 25 zones
    static void RunOtherAreasGroup9Zones()
    {
        string mogZones = "48,50,80,87,94,230,231,232,234,235,236,238,239,240,241,243,244,245,246,256,257";
        string Zones(QuestScriptDefinition def, int state, QuestHandlerKind kind, int id = 0) =>
            string.Join(",", def.States[state].Handlers
                .Where(h => h.Kind == kind && (id == 0 || h.Id == id)).Select(h => h.Zone));
        var give = new QC.OtherAreas.GiveAMoogleABreak().Compile();
        var picnic = new QC.OtherAreas.TheMooglePicnic().Compile();
        var wild = new QC.OtherAreas.MooglesInTheWild().Compile();
        string moogles = string.Join("|",
            Zones(give, 0, QuestHandlerKind.OnTrigger), Zones(give, 1, QuestHandlerKind.OnTrade),
            Zones(give, 1, QuestHandlerKind.OnEventFinish, 30008),
            Zones(picnic, 0, QuestHandlerKind.OnTrigger), Zones(picnic, 1, QuestHandlerKind.OnEventFinish, 30012),
            Zones(wild, 0, QuestHandlerKind.OnTrigger), Zones(wild, 1, QuestHandlerKind.OnEventFinish, 30016));
        Check("oa g9 zones:the moogle quests' Moogle and finishes in every Mog House zone",
              string.Join("|", Enumerable.Repeat(mogZones, 7)),
              moogles);

        // each era module's finish copy in each zone: the Timer at JstMidnight() (the quest's own: now + 60)
        string Timers(QuestScriptDefinition def, QuestModule module, string key, int csid) =>
            string.Join(",", QuestOverrides.Apply(def, module.Compile(new Fake()), key).Definition.States[1].Handlers
                .Where(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == csid)
                .Select(h =>
                {
                    var f = new Fake
                    {
                        Status = QuestStatus.Accepted, Zone = h.Zone, Csid = csid, Now = 1800000000,
                        NextJstMidnight = 1800025200,
                    };
                    h.Run(f);
                    return $"{h.Zone}:{f.Vars["Timer"]}";
                }));
        string Expected(int timer) => string.Join(",", mogZones.Split(',').Select(z => $"{z}:{timer}"));
        Check("oa g9 zones:each era module's 30007 / 30011 / 30015 finish sets the Timer at JstMidnight() in every " +
              "Mog House zone",
              string.Join("|", Expected(1800025200), Expected(1800025200), Expected(1800025200), Expected(1800000060)),
              string.Join("|",
                  Timers(give, new QC.Modules.Era.OtherAreas.GiveAMoogleABreak(),
                      QC.QuestKey.OtherAreas.GiveAMoogleABreak, 30007),
                  Timers(picnic, new QC.Modules.Era.OtherAreas.TheMooglePicnic(),
                      QC.QuestKey.OtherAreas.TheMooglePicnic, 30011),
                  Timers(wild, new QC.Modules.Era.OtherAreas.MooglesInTheWild(),
                      QC.QuestKey.OtherAreas.MooglesInTheWild, 30015),
                  string.Join(",", give.States[1].Handlers
                      .Where(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 30007)
                      .Select(h =>
                      {
                          var f = new Fake
                          {
                              Status = QuestStatus.Accepted, Zone = h.Zone, Csid = 30007, Now = 1800000000,
                              NextJstMidnight = 1800025200,
                          };
                          h.Run(f);
                          return $"{h.Zone}:{f.Vars["Timer"]}";
                      }))));

        var aef = new QC.OtherAreas.AnExplorersFootsteps().Compile();
        Check("oa g9 zones:An Explorer's Footsteps' Stone Monument trades in monumentTable's 25 zones",
              "100,101,81,102,103,104,82,105,84,106,107,108,109,90,110,91,115,95,116,117,118,119,97,120,98",
              Zones(aef, 1, QuestHandlerKind.OnTrade).Replace(",248", ""));
    }
}
