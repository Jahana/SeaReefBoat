// XI.Verify.Quests -- quest rework B3b Aht Urhgan group 5 (Waking the Colossus, Divine Interference, Give Peace a
// Chance, Moment of Truth): each definition validates clean; the declared steps; the reply kinds (Progress or Event)
// no flow can flip. Every other kind is pinned by a trace (the #rivals flows put a Progress or an Event of these
// quests against the rival containers at the same NPC, which show the kind through the tie and rank rules). The kinds
// here stay structural, one reason per NPC:
//  - Mishhar (562..576, 589, 800..806): no rival container in these quests' states beyond Give Peace a Chance and
//    Moment of Truth themselves, never up together: no competitor.
//  - qm8 / qm4 / qm7 (504, 211, 221): the one container of their NPC (no script): no competitor.
//  - Mount Zhayolm's Acid-eaten Door (154, 155, 157, 158) and trigger area 13 (925, 930): Waking the Colossus and
//    Divine Interference are never up together (one is completed before the other begins), and nothing else is
//    registered there.
//  - The zone-ins 307 and 309: the kind of a zone-in is never read.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;
using KI = XI.QuestContent.KeyItem;
using Z = XI.QuestContent.Zone;

internal static partial class Program
{
    static void RunAhtUrhganGroup5()
    {
        Console.WriteLine("== quest rework B3b Aht Urhgan group 5: the Alzadaal quests; reply kinds no trace sees ==");
        var wtc = new QC.AhtUrhgan.WakingTheColossus().Compile();
        var di = new QC.AhtUrhgan.DivineInterference().Compile();
        var gpc = new QC.AhtUrhgan.GivePeaceAChance().Compile();
        var mot = new QC.AhtUrhgan.MomentOfTruth().Compile();
        foreach (var (name, def) in new[] { ("Waking the Colossus", wtc), ("Divine Interference", di),
                     ("Give Peace a Chance", gpc), ("Moment of Truth", mot) })
            Check($"ahturhgan g5:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // The steps each quest says it sets (the validator's metadata: no flow can see an extra one declared)
        Check("ahturhgan g5:the declared steps", "1,2,3,5|1,2,3|1,2,3,4|1,2,3,4,5",
              string.Join("|", new[] { wtc, di, gpc, mot }.Select(d => string.Join(",", d.DeclaredSteps))));

        const QuestStatus Acc = QuestStatus.Accepted;
        ushort whitegate = Z.AhtUrhganWhitegate;
        string mishhar = QC.Npc.AhtUrhganWhitegate.Mishhar;
        const QuestHandlerKind talk = QuestHandlerKind.OnTrigger, trade = QuestHandlerKind.OnTrade;
        Fake Q(QuestStatus status, int step, params int[] keyItems)
        {
            var f = new Fake { Status = status, Step = step };
            f.CurrentMissions[(int)QC.MissionLog.Toau] = (int)QC.Mission.Toau.EternalMercenary;
            foreach (var k in keyItems) f.KeyItems[k] = true;
            return f;
        }
        string At(QuestScriptDefinition d, QuestHandlerKind k, ushort zone, string npc, Fake f) =>
            AU3Npc(d, k, zone, npc, f);
        string Zi(QuestScriptDefinition d, ushort zone, Fake f) =>
            AU1Describe(AU3Up(d, f).Single(x => x.Kind == QuestHandlerKind.OnZoneIn && x.Zone == zone), f);
        var lamp = Z.AlzadaalUnderseaRuins;
        var parts = new System.Collections.Generic.List<string>();
        // Give Peace a Chance: Mishhar by Prog, qm8 at night, qm4
        parts.Add(At(gpc, talk, whitegate, mishhar, Q(QuestStatus.Available, 0)));
        for (int p = 0; p <= 4; p++) parts.Add(At(gpc, talk, whitegate, mishhar, Q(Acc, p)));
        var night = Q(Acc, 0); night.Totd = 7;
        parts.Add(At(gpc, talk, Z.WajaomWoodlands, QC.Npc.WajaomWoodlands.qm8, night));
        parts.Add(At(gpc, talk, Z.Mamook, QC.Npc.Mamook.qm4, Q(Acc, 2)));
        // Moment of Truth
        var mAvail = Q(QuestStatus.Available, 0);
        mAvail.QuestStatuses["AhtUrghan/GIVE_PEACE_A_CHANCE"] = QuestStatus.Completed;
        parts.Add(At(mot, talk, whitegate, mishhar, mAvail));
        for (int p = 0; p <= 5; p++) parts.Add(At(mot, talk, whitegate, mishhar, Q(Acc, p)));
        parts.Add(At(mot, talk, Z.Mamook, QC.Npc.Mamook.qm7, Q(Acc, 2)));
        parts.Add(At(mot, talk, whitegate, mishhar, Q(QuestStatus.Completed, 0)));
        // Divine Interference
        var dArea = Q(QuestStatus.Available, 0);
        dArea.CompletedQuests.Add("AhtUrghan/WAKING_THE_COLOSSUS");
        parts.Add(AU3Area(di, whitegate, 13, dArea));
        parts.Add(Zi(di, lamp, Q(Acc, 4)));
        parts.Add(At(di, talk, Z.MountZhayolm, QC.Npc.MountZhayolm.AcideatenDoor, Q(Acc, 1)));
        var dTrade = Q(Acc, 2); dTrade.TradeItems[QC.Item.SlabOfPlumbago] = 3;
        parts.Add(At(di, trade, Z.MountZhayolm, QC.Npc.MountZhayolm.AcideatenDoor, dTrade));
        // Waking the Colossus
        var wArea = Q(QuestStatus.Available, 0);
        parts.Add(AU3Area(wtc, whitegate, 13, wArea));
        parts.Add(At(wtc, talk, Z.MountZhayolm, QC.Npc.MountZhayolm.AcideatenDoor, Q(Acc, 2, KI.PresentForMegomak)));
        var wTrade = Q(Acc, 2, KI.MegomaksShoppingList); wTrade.TradeItems[QC.Item.SlabOfPlumbago] = 3;
        parts.Add(At(wtc, trade, Z.MountZhayolm, QC.Npc.MountZhayolm.AcideatenDoor, wTrade));
        parts.Add(Zi(wtc, lamp, Q(Acc, 4)));
        Check("ahturhgan g5:the reply kinds the flows cannot flip, as the Lua has them: Progress and Event per line",
              "ProgressEvent(562)|Event(587)|ProgressEvent(575)|Event(588)|ProgressEvent(576)|ProgressEvent(589)|" +
              "ProgressCutscene(504)|ProgressCutscene(211)|ProgressEvent(800)|Event(801)|ProgressEvent(803)|" +
              "Event(588)|Event(804)|Event(804)|ProgressEvent(805)|ProgressCutscene(221)|" +
              "Event(806):replaceDefault()|ProgressEvent(930,{[0]=0,[1]=1,text_table=0})|Event(309)|" +
              "ProgressEvent(157)|ProgressEvent(158)|ProgressEvent(925,{[0]=50,[1]=1,[2]=4,[5]=1,text_table=0})|" +
              "ProgressEvent(154)|ProgressEvent(155)|Event(307)",
              string.Join("|", parts));
    }
}
