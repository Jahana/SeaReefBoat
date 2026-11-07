// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 9 (Cook-a-roon, Rat Race, Led Astray, Two Horn the Savage, Not
// Meant to Be, What Friends Are For): each definition validates clean; the declared steps; the reply kinds that no
// flow can show: the NPCs with one container and no script (the pin check at the end).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup9()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 9: Nashmau and Cacaroon ==");
        var car = new QC.AhtUrhgan.CookARoon().Compile();
        var rr = new QC.AhtUrhgan.RatRace().Compile();
        var la = new QC.AhtUrhgan.LedAstray().Compile();
        var th = new QC.AhtUrhgan.TwoHornTheSavage().Compile();
        var nm = new QC.AhtUrhgan.NotMeantToBe().Compile();
        var wf = new QC.AhtUrhgan.WhatFriendsAreFor().Compile();
        foreach (var (name, def) in new[] { ("Cook-a-roon", car), ("Rat Race", rr), ("Led Astray", la),
                     ("Two Horn the Savage", th), ("Not Meant to Be", nm), ("What Friends Are For", wf) })
            Check($"ahturhgan g9:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // The steps each quest says it sets (the validator's metadata: no flow can see an extra one declared)
        Check("ahturhgan g9:the declared steps", "0,2,3|1,2,3,4,5,6|1,2,3,4|1,2,3,4|1,2,3,4|1,2,3,4",
              string.Join("|", new[] { car, rr, la, th, nm, wf }.Select(d => string.Join(",", d.DeclaredSteps))));
        // The reply kinds no flow can show. A Progress beats an Event and equal kinds rotate, so a kind shows only
        // against a rival container or an NPC script; the flows (#rivals, #zone, #mission) put one at every NPC that
        // has one that can play (Ququroon, Cacaroon, Mhasbaf, Rubahah, Viscous Liquid, the Cacaroon trades). These have
        // none: each is the only container at a script-less NPC (so a later rival there is caught here). Each pin is
        // the Lua's kind.
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, int step = 0) => new Fake { Status = st, Step = step };
        Fake Stew(QuestStatus st, int step) { var f = Fk(st, step); f.KeyItems[(int)QC.KeyItem.PotOfTsetseroonsStew] = true; return f; }
        Fake Bowl(int step) { var f = Fk(QuestStatus.Accepted, step); f.TradeItems[QC.Item.BowlOfNashmauStew] = 1; f.TradeResult = true; return f; }
        ushort Nm = QC.Zone.Nashmau, Aw = QC.Zone.AhtUrhganWhitegate;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger;
        L("rr kakkaroon avail", AU3Npc(rr, Tr, Nm, QC.Npc.Nashmau.Kakkaroon, Fk(QuestStatus.Available)));
        L("rr kakkaroon 6", AU3Npc(rr, Tr, Nm, QC.Npc.Nashmau.Kakkaroon, Fk(QuestStatus.Accepted, 6)));
        L("rr kakkaroon 7", AU3Npc(rr, Tr, Nm, QC.Npc.Nashmau.Kakkaroon, Fk(QuestStatus.Accepted, 7)));
        L("rr kakkaroon done", AU3Npc(rr, Tr, Nm, QC.Npc.Nashmau.Kakkaroon, Fk(QuestStatus.Completed)));
        foreach (var s in new[] { 1, 2, 3 })
            L("rr nadee " + s, AU3Npc(rr, Tr, Aw, QC.Npc.AhtUrhganWhitegate.NadeePeriyaha, Fk(QuestStatus.Accepted, s)));
        L("rr kyokyoroon 6", AU3Npc(rr, Tr, Nm, QC.Npc.Nashmau.Kyokyoroon, Fk(QuestStatus.Accepted, 6)));
        L("rr kyokyoroon done", AU3Npc(rr, Tr, Nm, QC.Npc.Nashmau.Kyokyoroon, Fk(QuestStatus.Completed)));
        L("rr kyokyoroon trade 5", AU3Npc(rr, QuestHandlerKind.OnTrade, Nm, QC.Npc.Nashmau.Kyokyoroon, Bowl(5)));
        L("la area7", AU3Area(la, Aw, 7, Fk(QuestStatus.Accepted, 0)));
        L("la tohka", AU3Npc(la, Tr, Aw, QC.Npc.AhtUrhganWhitegate.TohkaTelposkha, Fk(QuestStatus.Accepted, 1)));
        L("la area8", AU3Area(la, Aw, 8, Fk(QuestStatus.Accepted, 3)));
        L("la tataroon", AU3Npc(la, Tr, Nm, QC.Npc.Nashmau.Tataroon, Fk(QuestStatus.Accepted, 4)));
        L("th milazahn avail", AU3Npc(th, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Milazahn, Fk(QuestStatus.Available)));
        L("th milazahn 4", AU3Npc(th, Tr, Aw, QC.Npc.AhtUrhganWhitegate.Milazahn, Fk(QuestStatus.Accepted, 4)));
        L("nm fhe avail", AU3Npc(nm, Tr, Nm, QC.Npc.Nashmau.FheMaksojha, Fk(QuestStatus.Available)));
        foreach (var s in new[] { 1, 2, 3, 4 })
            L("nm fhe " + s, AU3Npc(nm, Tr, Nm, QC.Npc.Nashmau.FheMaksojha, Fk(QuestStatus.Accepted, s)));
        L("nm fhe done", AU3Npc(nm, Tr, Nm, QC.Npc.Nashmau.FheMaksojha, Fk(QuestStatus.Completed)));
        L("nm qm12 1", AU3Npc(nm, Tr, QC.Zone.CaedarvaMire, QC.Npc.CaedarvaMire.qm12, Fk(QuestStatus.Accepted, 1)));
        L("wf tsetseroon 2", AU3Npc(wf, Tr, Nm, QC.Npc.Nashmau.Tsetseroon, Fk(QuestStatus.Accepted, 2)));
        L("wf tsetseroon 3", AU3Npc(wf, Tr, Nm, QC.Npc.Nashmau.Tsetseroon, Fk(QuestStatus.Accepted, 3)));
        L("wf tsetseroon 4", AU3Npc(wf, Tr, Nm, QC.Npc.Nashmau.Tsetseroon, Fk(QuestStatus.Accepted, 4)));
        L("wf tsetseroon avail 0", AU3Npc(wf, Tr, Nm, QC.Npc.Nashmau.Tsetseroon, Fk(QuestStatus.Available, 0)));
        L("wf tsetseroon avail 1", AU3Npc(wf, Tr, Nm, QC.Npc.Nashmau.Tsetseroon, Fk(QuestStatus.Available, 1)));
        L("wf tsetseroon done", AU3Npc(wf, Tr, Nm, QC.Npc.Nashmau.Tsetseroon, Fk(QuestStatus.Completed)));
        L("wf qm9", AU3Npc(wf, Tr, QC.Zone.AydeewaSubterrane, QC.Npc.AydeewaSubterrane.qm9, Stew(QuestStatus.Accepted, 3)));
        Check("ahturhgan g9:the reply kinds no flow can show", 
              "rr kakkaroon avail=ProgressEvent(308) ; rr kakkaroon 6=ProgressEvent(312) ; " +
              "rr kakkaroon 7=Event(313) ; rr kakkaroon done=Event(314) ; " +
              "rr nadee 1=ProgressEvent(849) ; rr nadee 2=Event(851) ; rr nadee 3=Event(852) ; " +
              "rr kyokyoroon 6=Event(316) ; rr kyokyoroon done=Event(317) ; " +
              "rr kyokyoroon trade 5=ProgressEvent(311) ; la area7=ProgressEvent(809) ; " +
              "la tohka=ProgressEvent(810) ; la area8=ProgressEvent(813) ; " +
              "la tataroon=ProgressEvent(305) ; th milazahn avail=ProgressEvent(594) ; " +
              "th milazahn 4=ProgressEvent(596) ; nm fhe avail=ProgressEvent(293) ; " +
              "nm fhe 1=Event(295) ; nm fhe 2=ProgressEvent(294) ; nm fhe 3=Event(296) ; " +
              "nm fhe 4=Event(297) ; nm fhe done=Event(298) ; nm qm12 1=ProgressEvent(16) ; " +
              "wf tsetseroon 2=Event(17) ; wf tsetseroon 3=Event(19) ; " +
              "wf tsetseroon 4=ProgressEvent(20) ; wf tsetseroon avail 0=Event(4) ; " +
              "wf tsetseroon avail 1=ProgressEvent(16) ; wf tsetseroon done=Event(21) ; " +
              "wf qm9=ProgressEvent(8)", string.Join(" ; ", lines));
    }
}
