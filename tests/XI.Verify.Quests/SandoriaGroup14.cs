// XI.Verify.Quests -- quest rework B3b San d'Oria group 14 (call-only: A Boy's Dream, Under Oath). Each definition
// validates clean and is call-only. The checks pin the replies' kinds, which no flow can show: in an NPC call the
// bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as player:startEvent (RenderCallReply); the Lua
// is player:startEvent(id) with no arguments, or a messageSpecial (the qm9). Each reply is picked as
// ZoneHost.QuestCalls.cs does (the first state, in order, with a called handler for that NPC and kind whose check
// holds). The offer (41 / 40 and their finishes) is sandoria.a_boys_dream#offer's trace.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunSandoriaGroup14()
    {
        Console.WriteLine("== quest rework B3b San d'Oria group 14: A Boy's Dream, Under Oath (call-only) ==");
        var abd = new QC.Sandoria.ABoysDream().Compile();
        var uo = new QC.Sandoria.UnderOath().Compile();
        foreach (var (name, def) in new[] { ("A Boy's Dream", abd), ("Under Oath", uo) })
        {
            Check($"sandoria g14:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"sandoria g14:{name} is call-only", "True", def.CallOnly.ToString());
        }

        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        ushort Ns = QC.Zone.NorthernSanDoria, Ss = QC.Zone.SouthernSanDoria, Ch = QC.Zone.ChateauDoraguille,
               Cn = QC.Zone.CrawlersNest;
        string Ailbeche = QC.Npc.NorthernSanDoria.Ailbeche, Door = QC.Npc.ChateauDoraguille._6h0,
               Vemalpeau = QC.Npc.SouthernSanDoria.Vemalpeau, Qm9 = QC.Npc.CrawlersNest.qm9;

        // the first called handler whose state's check holds, as the bridge picks it; "none" when no state has one
        string Call(QuestScriptDefinition def, QuestHandlerKind kind, ushort zone, string npc, Fake f)
        {
            foreach (var st in def.States)
            {
                var h = st.Handlers.FirstOrDefault(x =>
                    def.IsCalled(x) && x.Kind == kind && x.Zone == zone && x.Entity == npc);
                if (h == null || !st.IsMatch(f.Status, f.Step, f)) continue;
                string r = AU1Describe(h, f);
                return f.Calls.Count == 0 ? r : r + "+" + string.Join("+", f.Calls);
            }
            return "none";
        }
        Fake Fk(QuestStatus st, int job = QC.Job.Pld, int lvl = 50, QuestStatus sharpening = QuestStatus.Completed,
                (string, int)[] vars = null, int[] keyItems = null, params (int, int)[] trade)
        {
            var f = new Fake { Status = st, MainJob = job, MainLevel = lvl, UniqueDay = 8000, Now = 1800000000 };
            f.QuestStatuses["Sandoria/SHARPENING_THE_SWORD"] = sharpening;
            f.MainNumbers["AF2_QUEST_LEVEL"] = 50;
            foreach (var (n, v) in vars ?? Array.Empty<(string, int)>()) f.CharVars[n] = v;
            foreach (int k in keyItems ?? Array.Empty<int>()) f.KeyItems[k] = true;
            foreach (var (i, q) in trade) f.TradeItems[i] = q;
            return f;
        }
        (string, int)[] Cs(int v) => new[] { ("aBoysDreamCS", v) };
        (string, int)[] Uo(int v) => new[] { ("UnderOathCS", v) };
        const QuestStatus Av = QuestStatus.Available, Ac = QuestStatus.Accepted, Co = QuestStatus.Completed;

        // the replies' kinds no flow can show (each also reached by a trace)
        var kinds = new[]
        {
            ("41", Call(abd, Tr, Ns, Ailbeche, Fk(Av, vars: Cs(0)))),
            ("40", Call(abd, Tr, Ns, Ailbeche, Fk(Av, vars: Cs(1)))),
            ("46", Call(abd, Tr, Ns, Ailbeche, Fk(Ac, vars: Cs(2)))),
            ("39", Call(abd, Tr, Ns, Ailbeche, Fk(Ac, vars: Cs(3)))),
            ("60", Call(abd, Tr, Ns, Ailbeche, Fk(Ac, vars: Cs(4)))),
            ("47", Call(abd, Tr, Ns, Ailbeche, Fk(Ac, vars: Cs(5)))),
            ("25", Call(abd, Tr, Ns, Ailbeche, Fk(Ac, vars: Cs(7)))),
            ("CS 1", Call(abd, Tr, Ns, Ailbeche, Fk(Ac, vars: Cs(1)))),
            ("bug", Call(abd, Td, Ns, Ailbeche, Fk(Ac, vars: Cs(3), trade: (QC.Item.GiantShellBug, 1)))),
            ("fish", Call(abd, Td, Ns, Ailbeche, Fk(Ac, vars: Cs(4), trade: (QC.Item.Odontotyrannus, 1)))),
            ("bug at 2", Call(abd, Td, Ns, Ailbeche, Fk(Ac, vars: Cs(2), trade: (QC.Item.GiantShellBug, 1)))),
            ("88", Call(abd, Tr, Ch, Door, Fk(Co, vars: Cs(8)))),
            ("qm9 pop", Call(abd, Tr, Cn, Qm9, Fk(Ac))),
            ("qm9 today", Call(abd, Tr, Cn, Qm9, Fk(Ac, vars: new[] { ("DreadbugNM_Day", 8000) }))),
            ("qm9 available", Call(abd, Tr, Cn, Qm9, Fk(Av))),
            ("89", Call(uo, Tr, Ch, Door, Fk(Av, vars: Uo(8)))),
            ("90", Call(uo, Tr, Ch, Door, MakeUo90())),
            ("7", Call(uo, Tr, Ss, Vemalpeau, Fk(Ac, vars: Uo(0)))),
            ("5", Call(uo, Tr, Ss, Vemalpeau, Fk(Ac, vars: Uo(3), keyItems: new[] { QC.KeyItem.MiquesPaintbrush }))),
            ("3", Call(uo, Tr, Ss, Vemalpeau, Fk(Ac, vars: Uo(4), keyItems: new[] { QC.KeyItem.StrangeSheetOfPaper }))),
            ("2", Call(uo, Tr, Ss, Vemalpeau, Fk(Co, vars: Uo(9), keyItems: new[] { QC.KeyItem.KnightsConfession }))),
            ("Vemalpeau 1", Call(uo, Tr, Ss, Vemalpeau, Fk(Ac, vars: Uo(1)))),
        };
        Fake MakeUo90()
        {
            var f = Fk(Av, lvl: 60);
            f.QuestStatuses["Sandoria/A_BOYS_DREAM"] = Co;
            return f;
        }
        Check("sandoria g14:the reply kinds no flow can show (player:startEvent(id), the qm9's messageSpecial)",
              "41=Event(41) ; 40=Event(40) ; " +
              "46=Event(46) ; 39=Event(39) ; 60=Event(60) ; 47=Event(47) ; 25=Event(25) ; CS 1=none ; " +
              "bug=Event(15) ; fish=Event(47) ; bug at 2=none ; 88=Event(88) ; " +
              "qm9 pop=NoAction()+Player.PopFromQm(DREADBUG,True,0)+Text.Special(197:SENSE_OF_FOREBODING)+" +
              "Player.SetCharVar(DreadbugNM_Timer,1800000180,0)+Player.SetCharVar(DreadbugNM_Day,8000,0) ; " +
              "qm9 today=MessageSpecial(197:NOTHING_WILL_HAPPEN_YET) ; " +
              "qm9 available=MessageSpecial(197:NOTHING_WILL_HAPPEN_YET) ; 89=Event(89) ; 90=Event(90) ; " +
              "7=Event(7) ; 5=Event(5) ; 3=Event(3) ; 2=Event(2) ; Vemalpeau 1=none",
              string.Join(" ; ", kinds.Select(x => x.Item1 + "=" + x.Item2)));
    }
}
