// XI.Verify.Quests -- quest rework B3b Adoulin group 7 (call-only: A Pioneer's Best (Imaginary) Friend,
// A Thirst for the Ages, F.A.I.L.ure Is Not an Option, Scaredy-Cats, Don't Ever Leaf Me): each definition validates
// clean and is call-only; the replies' kinds, which no flow can show: in an NPC call the bridge renders an Event, a
// ProgressEvent and a PriorityEvent alike, as player:startEvent (no action is built, so no priority). Each pin is the
// Lua's: player:startEvent(id) with no arguments, or nothing (null). The finishes' calls in the scripts' order: 2520's
// (the chained 2522 with Ionis on), 2522's and 76's (the raw completeQuest, then the rewards at the rates), 2523's
// (the influence effects, then Ionis for 9000 s with no power), 5053's, 5024's (option 1 only), 5013's and 5015's.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup7()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 7: Merleg, Ruth, Nikkhail, Clautaire, Eamonn, Bilp, " +
                          "Dewalt, Barenngo (call-only) ==");
        var pb = new QC.Adoulin.APioneersBestImaginaryFriend().Compile();
        var tf = new QC.Adoulin.AThirstForTheAges().Compile();
        var fa = new QC.Adoulin.FailureIsNotAnOption().Compile();
        var sc = new QC.Adoulin.Scaredycats().Compile();
        var dl = new QC.Adoulin.DontEverLeafMe().Compile();
        foreach (var (name, def) in new[]
                 {
                     ("A Pioneer's Best (Imaginary) Friend", pb), ("A Thirst for the Ages", tf),
                     ("F.A.I.L.ure Is Not an Option", fa), ("Scaredy-Cats", sc), ("Don't Ever Leaf Me", dl),
                 })
        {
            Check($"adoulin g7:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"adoulin g7:{name} is call-only", "True", def.CallOnly.ToString());
        }

        const int Ionis = 512;
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, Action<Fake> setup = null)
        {
            var f = new Fake { Status = st };
            setup?.Invoke(f);
            return f;
        }
        Action<Fake> Var(string name, int value) => f => f.CharVars[name] = value;
        Action<Fake> Ki(params int[] keyItems) => f => { foreach (int k in keyItems) f.KeyItems[k] = true; };
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger;
        ushort Wa = QC.Zone.WesternAdoulin;
        string R(QuestScriptDefinition def, string npc, Fake f) => AU3Npc(def, Tr, Wa, npc, f);
        string Merleg = QC.Npc.WesternAdoulin.Merleg, Ruth = QC.Npc.WesternAdoulin.Ruth;
        string Nikkhail = QC.Npc.WesternAdoulin.Nikkhail, Clautaire = QC.Npc.WesternAdoulin.Clautaire;
        string Eamonn = QC.Npc.WesternAdoulin.Eamonn, Bilp = QC.Npc.WesternAdoulin.Bilp;
        string Dewalt = QC.Npc.WesternAdoulin.Dewalt, Barenngo = QC.Npc.WesternAdoulin.Barenngo;
        Action<Fake> On = f => f.StatusEffects.Add(Ionis);
        int Copy = QC.KeyItem.CopyOfTheAllianceAgreement, Badge = QC.KeyItem.FailBadge, Hunk = QC.KeyItem.HunkOfBedrock;
        int ad = QC.FameArea.Adoulin;

        L("pb merleg available", R(pb, Merleg, Fk(QuestStatus.Available)));
        L("pb merleg accepted", R(pb, Merleg, Fk(QuestStatus.Accepted)));
        L("pb merleg ionis", R(pb, Merleg, Fk(QuestStatus.Accepted, On)));
        L("pb merleg completed", R(pb, Merleg, Fk(QuestStatus.Completed, On)));
        L("pb ruth accepted", R(pb, Ruth, Fk(QuestStatus.Accepted)));
        L("pb ruth ionis", R(pb, Ruth, Fk(QuestStatus.Accepted, On)));
        L("pb ruth available", R(pb, Ruth, Fk(QuestStatus.Available)));
        L("tf accepted", R(tf, Nikkhail, Fk(QuestStatus.Accepted)));
        L("tf status 1", R(tf, Nikkhail, Fk(QuestStatus.Accepted, Var("ATFTA_Status", 1))));
        L("tf status 2", R(tf, Nikkhail, Fk(QuestStatus.Accepted, Var("ATFTA_Status", 2))));
        L("tf copy", R(tf, Nikkhail, Fk(QuestStatus.Accepted, Ki(Copy))));
        L("tf available", R(tf, Nikkhail, Fk(QuestStatus.Available)));
        Action<Fake> Fame(int level, bool badge) => f => { f.Fame[ad] = level; if (badge) f.KeyItems[Badge] = true; };
        L("fa 78", R(fa, Clautaire, Fk(QuestStatus.Available, Fame(4, true))));
        L("fa level 3", R(fa, Clautaire, Fk(QuestStatus.Available, Fame(3, true))));
        L("fa no badge", R(fa, Clautaire, Fk(QuestStatus.Available, Fame(4, false))));
        L("fa 77", R(fa, Clautaire, Fk(QuestStatus.Accepted, Fame(4, true))));
        L("fa 76", R(fa, Clautaire, Fk(QuestStatus.Accepted, Ki(Hunk))));
        L("fa completed",
          R(fa, Clautaire, Fk(QuestStatus.Completed, f => { Fame(4, true)(f); f.KeyItems[Hunk] = true; })));
        foreach (string npc in new[] { Eamonn, Bilp })
        {
            L($"sc {npc} 0 available", R(sc, npc, Fk(QuestStatus.Available)));
            L($"sc {npc} 0 accepted", R(sc, npc, Fk(QuestStatus.Accepted)));
            L($"sc {npc} 1 available", R(sc, npc, Fk(QuestStatus.Available, Var("Scaredycats_Status", 1))));
            L($"sc {npc} 1 accepted", R(sc, npc, Fk(QuestStatus.Accepted, Var("Scaredycats_Status", 1))));
            L($"sc {npc} 2", R(sc, npc, Fk(QuestStatus.Accepted, Var("Scaredycats_Status", 2))));
            L($"sc {npc} 3", R(sc, npc, Fk(QuestStatus.Accepted, Var("Scaredycats_Status", 3))));
        }
        foreach (var (npc, v) in new[] { (Dewalt, "DELM_Dewalt_Branch"), (Barenngo, "DELM_Barenngo_Branch") })
        {
            L($"dl {npc} 0", R(dl, npc, Fk(QuestStatus.Accepted)));
            L($"dl {npc} 1", R(dl, npc, Fk(QuestStatus.Accepted, Var(v, 1))));
            L($"dl {npc} 2", R(dl, npc, Fk(QuestStatus.Accepted, Var(v, 2))));
            L($"dl {npc} available", R(dl, npc, Fk(QuestStatus.Available)));
        }
        Check("adoulin g7:the reply kinds no flow can show",
              "pb merleg available=Event(2520) ; pb merleg accepted=Event(2521) ; pb merleg ionis=Event(2522) ; " +
              "pb merleg completed=null ; pb ruth accepted=Event(2523) ; pb ruth ionis=null ; " +
              "pb ruth available=null ; tf accepted=Event(5053) ; tf status 1=Event(5053) ; tf status 2=null ; " +
              "tf copy=null ; tf available=null ; fa 78=Event(78) ; fa level 3=null ; fa no badge=null ; " +
              "fa 77=Event(77) ; fa 76=Event(76) ; fa completed=null ; " +
              $"sc {Eamonn} 0 available=Event(5031) ; sc {Eamonn} 0 accepted=Event(5030) ; " +
              $"sc {Eamonn} 1 available=Event(5024) ; sc {Eamonn} 1 accepted=Event(5025) ; " +
              $"sc {Eamonn} 2=Event(5027) ; sc {Eamonn} 3=Event(5030) ; " +
              $"sc {Bilp} 0 available=Event(5031) ; sc {Bilp} 0 accepted=Event(5029) ; " +
              $"sc {Bilp} 1 available=Event(5024) ; sc {Bilp} 1 accepted=Event(5025) ; " +
              $"sc {Bilp} 2=Event(5026) ; sc {Bilp} 3=Event(5029) ; " +
              $"dl {Dewalt} 0=Event(5013) ; dl {Dewalt} 1=Event(5014) ; dl {Dewalt} 2=null ; " +
              $"dl {Dewalt} available=null ; dl {Barenngo} 0=Event(5015) ; dl {Barenngo} 1=Event(5016) ; " +
              $"dl {Barenngo} 2=null ; dl {Barenngo} available=null",
              string.Join(" ; ", lines));

        // the finishes, each run on a fake at the csid (and option), in the scripts' order
        string Finish(QuestScriptDefinition def, int csid, int option = 0, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(st => st.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == Wa && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, Definition = def };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Action<Fake> Rates = f =>
        {
            f.MainNumbers["EXP_RATE"] = 1.125;
            f.MainNumbers["BAYLD_RATE"] = 1.375;
        };
        Check("adoulin g7:2520's finish: begun; with Ionis on, 2522 started from the finish",
              "Quest.Begin()|Quest.Begin();Player.StartEvent(2522)",
              Finish(pb, 2520, setup: f => f.Status = QuestStatus.Available) + "|" +
              Finish(pb, 2520, setup: f => { f.Status = QuestStatus.Available; On(f); }));
        Check("adoulin g7:2522's finish at EXP_RATE 1.125 (llround: half away from zero), in order",
              "Player.CompleteQuest(Adoulin,A_PIONEERS_BEST_IMAGINARY_FRIEND);Player.AddExp(563);" +
              $"Player.GiveCurrency(bayld,200);Player.GiveKeyItem({Badge});Player.AddFame({ad},30)",
              Finish(pb, 2522, setup: Rates));
        Check("adoulin g7:2523's finish: the influence effects silently, then Ionis for 9000 s (no power)",
              "Player.DelStatusEffectsByFlag(16777216,True);Player.AddStatusEffectForDuration(512,9000)",
              Finish(pb, 2523));
        Check("adoulin g7:76's finish at EXP_RATE 1.125, BAYLD_RATE 1.375, in order",
              $"Player.DelKeyItem({Hunk});Player.CompleteQuest(Adoulin,FAILURE_IS_NOT_AN_OPTION);" +
              "Player.AddExp(1125);Player.AddCurrency(bayld,688);Text.Special(256:BAYLD_OBTAINED,688);" +
              $"Player.AddFame({ad},30)",
              Finish(fa, 76, setup: Rates));
        Check("adoulin g7:78's and 5053's finishes",
              $"Quest.Begin()|Player.GiveKeyItem({Copy})",
              Finish(fa, 78, setup: f => f.Status = QuestStatus.Available) + "|" + Finish(tf, 5053));
        Check("adoulin g7:5024's finish on options 0 / 1 / 2: the status 2 and begun on 1 only",
              "|Player.SetCharVar(Scaredycats_Status,2,0);Quest.Begin()|",
              string.Join("|", new[] { 0, 1, 2 }.Select(o =>
                  Finish(sc, 5024, o, f =>
                  {
                      f.Status = QuestStatus.Available;
                      f.CharVars["Scaredycats_Status"] = 1;
                  }))));
        Check("adoulin g7:5013's and 5015's finishes: each NPC's branch var 1",
              "Player.SetCharVar(DELM_Dewalt_Branch,1,0)|Player.SetCharVar(DELM_Barenngo_Branch,1,0)",
              Finish(dl, 5013) + "|" + Finish(dl, 5015));
    }
}
