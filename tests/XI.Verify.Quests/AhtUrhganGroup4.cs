// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 4 (the Salaheem's Sentinels promotions II: Sergeant Major,
// Chief Sergeant, Second Lieutenant, First Lieutenant): each definition validates clean; the reply kinds no trace sees
// (the mutants' structural survivors; every other kind is pinned by a trace, the #kinds, #abquhbah and #hagakoff flows
// among them).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup4()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 4: the promotions II; the reply kinds no trace sees ==");
        var sm = new QC.AhtUrhgan.PromotionSergeantMajor().Compile();
        var cs = new QC.AhtUrhgan.PromotionChiefSergeant().Compile();
        var sl = new QC.AhtUrhgan.PromotionSecondLieutenant().Compile();
        var fl = new QC.AhtUrhgan.PromotionFirstLieutenant().Compile();
        foreach (var (name, def) in new[] { ("Sergeant Major", sm), ("Chief Sergeant", cs),
                     ("Second Lieutenant", sl), ("First Lieutenant", fl) })
            Check($"ahturhgan g4:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // what no trace can see. The Mushroom Patches have no script of their own (no fallback: the lookup runs the
        // action whatever its rank) and their only rival is the zone's DefaultActions NOTHING_OUT_OF_ORDINARY (below
        // Event); one quest's lines, one state up at a time. Abquhbah's script has no onTrade, and one quest's trade line
        // is up at him at a time (Second Lieutenant's, then First Lieutenant's 5081: it needs Second Lieutenant
        // completed). First Lieutenant's 5089 at Prog 3 is importantOnce: his script's 255 alternates with an action at
        // Event or below either way, so Event then Default plays as a plain Event would, and importantOnce sets the rank
        // to Event itself (a Progress under it plays the same).
        const int firstPatch = 17056000;   // any id: the Lua reads the offset from the first patch (patch 0 here)
        Fake Patch(int mpatch, bool scourshroom = false, bool needToZone = false)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 3, ZoningFlag = needToZone };
            f.Npc = new QuestNpcFake(firstPatch);
            f.NpcIds["MUSHROOM_PATCH"] = firstPatch;
            f.Vars["MPatch0"] = mpatch;
            if (scourshroom) f.KeyItems[QC.KeyItem.Scourshroom] = true;
            return f;
        }
        // accepted at that Prog, the trade each state's line takes (Second Lieutenant: three Gold Pieces, two Mythril
        // Pieces, the Troll Pauldron; First Lieutenant: five Gold Pieces)
        Fake Traded(int prog, int item, int qty)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = prog };
            f.TradeItems[item] = qty;
            return f;
        }
        int gold = QC.Item.ImperialGoldPiece, mythril = QC.Item.ImperialMythrilPiece;
        const QuestHandlerKind talk = QuestHandlerKind.OnTrigger, trade = QuestHandlerKind.OnTrade;
        ushort aydeewa = QC.Zone.AydeewaSubterrane, whitegate = QC.Zone.AhtUrhganWhitegate;
        string patch = QC.Npc.AydeewaSubterrane.MushroomPatch, abquhbah = QC.Npc.AhtUrhganWhitegate.Abquhbah;
        Check("ahturhgan g4:the reply kinds no flow can show, as the Lua has them: the Mushroom Patches' 14, 15, 19, " +
              "20, 21 and 18 Progress and triggerPatch's own NOTHING_OUT_OF_ORDINARY (the default gives the same text); " +
              "Second Lieutenant's trades 5073, 5075, 5076, 5077 and 5078 and First Lieutenant's 5081 Progress; First " +
              "Lieutenant's 5089 at Prog 3 an importantOnce Event",
              "ProgressEvent(14)|ProgressEvent(15)|ProgressEvent(19)|ProgressEvent(20)|ProgressEvent(21,{[0]=1,[3]=0})|" +
              "ProgressEvent(18)|MessageSpecial(68:NOTHING_OUT_OF_ORDINARY)|ProgressEvent(5073)|ProgressEvent(5075)|" +
              "ProgressEvent(5076)|ProgressEvent(5077)|ProgressEvent(5078)|ProgressEvent(5081,{text_table=0})|" +
              "Event(5089,{[0]=0,text_table=0}):importantOnce()",
              string.Join("|",
                  AU3Npc(cs, talk, aydeewa, patch, Patch(0)),
                  AU3Npc(cs, talk, aydeewa, patch, Patch(1)),
                  AU3Npc(cs, talk, aydeewa, patch, Patch(0, scourshroom: true)),
                  AU3Npc(cs, talk, aydeewa, patch, Patch(2, needToZone: true)),
                  AU3Npc(cs, talk, aydeewa, patch, Patch(2)),
                  AU3Npc(cs, talk, aydeewa, patch, Patch(3)),
                  AU3Npc(cs, talk, aydeewa, patch, Patch(4)),
                  AU3Npc(sl, trade, whitegate, abquhbah, Traded(0, gold, 3)),
                  AU3Npc(sl, trade, whitegate, abquhbah, Traded(1, mythril, 2)),
                  AU3Npc(sl, trade, whitegate, abquhbah, Traded(2, mythril, 2)),
                  AU3Npc(sl, trade, whitegate, abquhbah, Traded(3, QC.Item.TrollPauldron, 1)),
                  AU3Npc(sl, trade, whitegate, abquhbah, Traded(4, mythril, 2)),
                  AU3Npc(fl, trade, whitegate, abquhbah, Traded(0, gold, 5)),
                  AU3Npc(fl, talk, whitegate, abquhbah, new Fake { Status = QuestStatus.Accepted, Step = 3 })));
    }
}
