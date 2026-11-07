// XI.Verify.Quests -- quest rework B3b Crystal War group 10 (Her Memories: Homecoming Queen, Of Malign Maladies,
// Operation Cupid, Carnelian Footfalls, Azure Footfalls): what no trace can see (validator, declared steps, the
// sub-quest key item order, the fourth-fragment arm, the kinds no flow can tell apart, the completions' false arm).
// The replies, ties and settings are flows' (crystalWar.her_memories_*).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup10()
    {
        Console.WriteLine(
            "== quest rework B3b Crystal War group 10: the validator, the declared steps and the fragment arms ==");
        var hq = new QC.CrystalWar.HerMemoriesHomecomingQueen().Compile();
        var mm = new QC.CrystalWar.HerMemoriesOfMalignMaladies().Compile();
        var oc = new QC.CrystalWar.HerMemoriesOperationCupid().Compile();
        var cf = new QC.CrystalWar.HerMemoriesCarnelianFootfalls().Compile();
        var af = new QC.CrystalWar.HerMemoriesAzureFootfalls().Compile();
        Check("crystalwar g10:Homecoming Queen clean", "", string.Join(" | ", QuestScriptValidator.Validate(hq)));
        Check("crystalwar g10:Of Malign Maladies clean", "", string.Join(" | ", QuestScriptValidator.Validate(mm)));
        Check("crystalwar g10:Operation Cupid clean", "", string.Join(" | ", QuestScriptValidator.Validate(oc)));
        Check("crystalwar g10:Carnelian Footfalls clean", "", string.Join(" | ", QuestScriptValidator.Validate(cf)));
        Check("crystalwar g10:Azure Footfalls clean", "", string.Join(" | ", QuestScriptValidator.Validate(af)));
        Check("crystalwar g10:the declared steps", " | 1,2,3 | 1,2 | 1,2 | 1,2,3,4,5",
              string.Join(" | ", new[] { hq, mm, oc, cf, af }.Select(d => string.Join(",", d.DeclaredSteps))));

        string Fin(QuestScriptDefinition d, ushort zone, int csid, Action<Fake> set = null)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var fk = new Fake { Status = QuestStatus.Accepted, Csid = csid, Zone = zone };
            set?.Invoke(fk);
            h.Run(fk);
            return string.Join(";", fk.Calls);
        }

        // Operation Cupid's 24 finish (completes the quest, then runs the Lua helper) is a flow's since
        // carry-harness-2: crystalWar.her_memories_operation_cupid#fourth_fragment takes the fourth fragment's arm (its
        // Lua baseline with an expect-error step, the helper raising at REPORT_TO_CAIT_SITH, nil in upstream's Batallia
        // Downs [S]; since user ruling 2026-11-05 the zone has the text and Her Memories completes), so the call and
        // its order are traced

        // the Lua's pairs(subQuestData) visits 870, 793, 6: the three tiny fragments are deleted in that order when the
        // sub-quest finish finds two or more held
        var tiny = new[]
        {
            QC.KeyItem.TinyMemoryFragment1, QC.KeyItem.TinyMemoryFragment2, QC.KeyItem.TinyMemoryFragment3,
        };
        string meld = Fin(hq, QC.Zone.PortSanDoria, 793, f => { foreach (int k in tiny) f.KeyItems[k] = true; });
        Check("crystalwar g10:Homecoming Queen's 793 meld deletes the tiny fragments in pairs order",
              "Player.CompleteQuest(CrystalWar,HER_MEMORIES_OLD_BEAN);Text.Special(232:FRAGMENTS_MELD,1606);" +
              "Player.DelKeyItem(1700);Player.DelKeyItem(1605);Player.DelKeyItem(1701);Quest.Complete();" +
              "Player.CheckMemoryFragments()", meld);
        RunCrystalWarGroup10Pins(hq, mm, oc, cf, af, Fin);
    }

    // What no flow can tell apart. The kinds: a zone-in plays its csid as a number whatever the reply's kind
    // (interaction_lookup.lua getHighestPriorityActions); Library_book3 and the Fey Blossoms have no script and no
    // other quest line; Leadavox has no script, and its other trade lines (Better Part of Valor, Beneath the Mask) take
    // other items exactly. Every other reply's kind is a flow's: the #rivals flows tie it with Waking the Colossus
    // (Halver), A Taste for Meat (Thierride), A Hawk in Repose (the Weathered Gravestone), Steamed Rams (Mainchelite,
    // the qms), What Price Loyalty (Roderich), Knot Quite There (Bulwark Gate), Better Part of Valor (Leadavox) and
    // Homecoming Queen / Boy and the Beast (Amaura, Raustigne), and the main flows talk again where the NPC's own
    // script takes the turn after an Event. The completions: the reward is a key item, so Complete() never fails; the
    // false arm of each fragment finish is pinned here (Azure Footfalls' 360 runs the fragment check either way).
    static void RunCrystalWarGroup10Pins(QuestScriptDefinition hq, QuestScriptDefinition mm, QuestScriptDefinition oc,
                                         QuestScriptDefinition cf, QuestScriptDefinition af,
                                         Func<QuestScriptDefinition, ushort, int, Action<Fake>, string> fin)
    {
        string R(QuestScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string npc,
                 int step = 0, Action<Fake> set = null)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone &&
                                                         (kind == QuestHandlerKind.OnZoneIn || x.Entity == npc));
            var fk = new Fake { Status = state == 0 ? QuestStatus.Available : QuestStatus.Accepted, Step = step,
                                UniqueDay = 8000 };
            set?.Invoke(fk);
            var r = h.Fixed ?? h.Reply(fk);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var zin = QuestHandlerKind.OnZoneIn;
        Action<Fake> From(ushort zone) => f => f.PrevZone = zone;

        Check("crystalwar g10 kinds:the zone-ins (957, 23, 24, 170, 12, 358)",
              "Event(957) | Event(23) | Event(24) | Event(170) | Event(12) | Event(358)",
              string.Join(" | ",
                  R(hq, 0, zin, QC.Zone.SouthernSanDoria, null, 0, From(QC.Zone.EastRonfaure)),
                  R(oc, 0, zin, QC.Zone.BatalliaDownsS, null, 0, From(QC.Zone.JugnerForestS)),
                  R(oc, 1, zin, QC.Zone.BatalliaDownsS, null, 2, From(QC.Zone.RolanberryFieldsS)),
                  R(cf, 0, zin, QC.Zone.SouthernSanDoriaS, null, 0, From(QC.Zone.EastRonfaureS)),
                  R(cf, 1, zin, QC.Zone.EastRonfaureS, null, 1, From(QC.Zone.SouthernSanDoriaS)),
                  R(af, 0, zin, QC.Zone.BastokMarketsS, null, 0, From(QC.Zone.NorthGustabergS))));

        ushort ww = QC.Zone.WindurstWaters, gs = QC.Zone.GraubergS;
        string book = QC.Npc.WindurstWaters.Librarybook3, fey = QC.Npc.GraubergS.FeyBlossoms;
        Check("crystalwar g10 kinds:Of Malign Maladies, Library_book3 (Prog 1, 2) and the Fey Blossoms (the stone at "
              + "Prog 2; a talk at Prog 2, at Prog 3 with the Timer to come, then due)",
              "ProgressEvent(1017,650060514,0,0,0,110,6312479,4095,128) | " +
              "Event(1018,650060691,0,0,0,65863679,4104166,4095,4) | " +
              "ProgressEvent(30,89,23,2964,56,0,6029313,0,0) | MessageSpecial(89:SUITABLE_PLACE_TO_SOAK,942) | " +
              "Event(31,89,23,2964,56,0,6029313,0,0) | ProgressEvent(32,89,6,0,56,0,6029328,0,0)",
              string.Join(" | ",
                  R(mm, 1, trig, ww, book, 1), R(mm, 1, trig, ww, book, 2),
                  R(mm, 1, trade, gs, fey, 2, f => f.TradeItems[QC.Item.PhilosophersStone] = 1),
                  R(mm, 1, trig, gs, fey, 2),
                  R(mm, 1, trig, gs, fey, 3, f => f.Vars["Timer"] = 8001),
                  R(mm, 1, trig, gs, fey, 3, f => f.Vars["Timer"] = 8000)));

        Check("crystalwar g10 kinds:Operation Cupid, Leadavox's trade of the three at Prog 1", "ProgressEvent(4)",
              R(oc, 1, trade, QC.Zone.VunkerlInletS, QC.Npc.VunkerlInletS.Leadavox, 1, f =>
              {
                  f.TradeItems[QC.Item.BottleOfRiceVinegar] = 1;
                  f.TradeItems[QC.Item.JarOfGroundWasabi] = 1;
                  f.TradeItems[QC.Item.SprigOfHolyBasil] = 1;
              }));

        // Complete() false: no fragment check and no Fey Stone taken (Azure Footfalls' 360 checks anyway)
        Action<Fake> fails = f => f.CompleteResult = false;
        var tiny = new[]
        {
            QC.KeyItem.TinyMemoryFragment1, QC.KeyItem.TinyMemoryFragment2, QC.KeyItem.TinyMemoryFragment3,
        };
        Check("crystalwar g10 pins:the fragment finishes when Complete() fails (793 meld, 169, 24, 172, 360)",
              "Player.CompleteQuest(CrystalWar,HER_MEMORIES_OLD_BEAN);Text.Special(232:FRAGMENTS_MELD,1606);" +
              "Player.DelKeyItem(1700);Player.DelKeyItem(1605);Player.DelKeyItem(1701);Quest.Complete() | " +
              "Quest.Complete() | Quest.Complete() | Quest.Complete() | Quest.Complete();Player.CheckMemoryFragments()",
              string.Join(" | ",
                  fin(hq, QC.Zone.PortSanDoria, 793, f => { foreach (int k in tiny) f.KeyItems[k] = true; fails(f); }),
                  fin(mm, QC.Zone.SouthernSanDoriaS, 169, fails), fin(oc, QC.Zone.BatalliaDownsS, 24, fails),
                  fin(cf, QC.Zone.SouthernSanDoriaS, 172, fails), fin(af, QC.Zone.BastokMarketsS, 360, fails)));
    }
}
