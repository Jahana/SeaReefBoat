// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 8 (No Strings Attached, Puppetmaster Blues, Got It All, Three
// Men and a Closet, Fear of the Dark II): each definition validates clean; the declared steps; the reply kinds that no
// flow can show (the pin check at the end).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAhtUrhganGroup8()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 8: the puppetmaster, Got It All and Imp Wing quests ==");
        var nsa = new QC.AhtUrhgan.NoStringsAttached().Compile();
        var pb = new QC.AhtUrhgan.PuppetmasterBlues().Compile();
        var gia = new QC.AhtUrhgan.GotItAll().Compile();
        var tm = new QC.AhtUrhgan.ThreeMenAndACloset().Compile();
        var fotd = new QC.AhtUrhgan.FearOfTheDarkIi().Compile();
        foreach (var (name, def) in new[] { ("No Strings Attached", nsa), ("Puppetmaster Blues", pb), ("Got It All", gia),
                     ("Three Men and a Closet", tm), ("Fear of the Dark II", fotd) })
            Check($"ahturhgan g8:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // The steps each quest says it sets (the validator's metadata: no flow can see an extra one declared)
        Check("ahturhgan g8:the declared steps", "1,2,3,4|1,2,3,4,5|1,2,3,4,5,6,7|1,2,3,4|",
              string.Join("|", new[] { nsa, pb, gia, tm, fotd }.Select(d => string.Join(",", d.DeclaredSteps))));

        // The reply kinds no flow can show. A Progress beats an Event and equal kinds rotate, so a kind shows only
        // against a rival container or an NPC script; the flows (#kinds2, #rivals, #kinds) put one at every NPC that
        // has one that can play. These have none: Suldiran (no script, one container), Kubhe Ijyuhla (Three Men and
        // a Closet only; his only rival is the Default-priority default, a state that cannot occur in game), the Got
        // It All area 1 trigger (one container) and the Arrapago Reef qm10 (no script, a Default-priority rival
        // only). Each pin is the Lua's kind.
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        Fake Fk(QuestStatus st, int step = 0) => new Fake { Status = st, Step = step };
        Fake Wings(QuestStatus st, int n) { var f = Fk(st); f.TradeItems[QC.Item.ImpWing] = n; f.TradeResult = n == 2; return f; }
        const string Su = QC.Npc.AlZahbi.Suldiran;
        L("fotd avail", AU3Npc(fotd, QuestHandlerKind.OnTrigger, QC.Zone.AlZahbi, Su, Fk(QuestStatus.Available)));
        L("fotd acc", AU3Npc(fotd, QuestHandlerKind.OnTrigger, QC.Zone.AlZahbi, Su, Fk(QuestStatus.Accepted)));
        L("fotd acc trade", AU3Npc(fotd, QuestHandlerKind.OnTrade, QC.Zone.AlZahbi, Su, Wings(QuestStatus.Accepted, 2)));
        L("fotd done", AU3Npc(fotd, QuestHandlerKind.OnTrigger, QC.Zone.AlZahbi, Su, Fk(QuestStatus.Completed)));
        L("fotd done trade", AU3Npc(fotd, QuestHandlerKind.OnTrade, QC.Zone.AlZahbi, Su, Wings(QuestStatus.Completed, 2)));
        L("gia area", AU3Area(gia, QC.Zone.AhtUrhganWhitegate, 1, Fk(QuestStatus.Accepted, 5)));
        Fake GiaDone(Fake f) { f.QuestStatuses["AhtUrghan/GOT_IT_ALL"] = QuestStatus.Completed; return f; }
        const string Ku = QC.Npc.AhtUrhganWhitegate.KubheIjyuhla;
        foreach (var (st, n) in new[] { (0, 837), (1, 838), (2, 839), (3, 842), (4, 845) })
            L("tm kubhe acc " + st, AU3Npc(tm, QuestHandlerKind.OnTrigger, QC.Zone.AhtUrhganWhitegate, Ku, Fk(QuestStatus.Accepted, st)));
        L("tm kubhe avail", AU3Npc(tm, QuestHandlerKind.OnTrigger, QC.Zone.AhtUrhganWhitegate, Ku, GiaDone(Fk(QuestStatus.Available))));
        L("tm kubhe done", AU3Npc(tm, QuestHandlerKind.OnTrigger, QC.Zone.AhtUrhganWhitegate, Ku, Fk(QuestStatus.Completed)));
        L("nsa qm10 1", AU3Npc(nsa, QuestHandlerKind.OnTrigger, QC.Zone.ArrapagoReef, QC.Npc.ArrapagoReef.qm10, Fk(QuestStatus.Accepted, 1)));
        L("nsa qm10 2", AU3Npc(nsa, QuestHandlerKind.OnTrigger, QC.Zone.ArrapagoReef, QC.Npc.ArrapagoReef.qm10, Fk(QuestStatus.Accepted, 2)));
        Check("ahturhgan g8:the reply kinds no flow can show", "fotd avail=ProgressEvent(14) ; fotd acc=Event(15) ; fotd acc trade=ProgressEvent(16) ; fotd done=Event(17) ; " +
              "fotd done trade=ProgressEvent(18) ; gia area=ProgressEvent(526) ; tm kubhe acc 0=Event(837) ; " +
              "tm kubhe acc 1=ProgressEvent(838) ; tm kubhe acc 2=Event(839) ; tm kubhe acc 3=Event(842) ; tm kubhe acc 4=ProgressEvent(845) ; " +
              "tm kubhe avail=ProgressEvent(836) ; tm kubhe done=Event(846) ; nsa qm10 1=ProgressCutscene(214) ; " +
              "nsa qm10 2=MessageSpecial(54:PILE_OF_DISCARDED_MATERIALS)",
              string.Join(" ; ", lines));
    }
}
