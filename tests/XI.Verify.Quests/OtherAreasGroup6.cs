// XI.Verify.Quests -- quest rework B3b Other Areas group 6 (Tavnazian Safehold: A Bitter Past, In Search of the Truth,
// Petals for Parelbriaux, The Call of the Sea): the reply kinds no trace can tell apart. The group adds no facet member
// (In Search of the Truth's player:completeQuest is Player.CompleteQuest, from the Jeuno lane).
// - Kinds: where no other quest and no NPC script answers the NPC (Frescheque, Tressia, Zadant, Fouagine, Noam, the ???s,
//   Leporaitceau, Anteurephiaux), or where the only rival ranks below either kind (Mengrenaux's 560 against CoP 2-3's 271,
//   replaceDefault; Tressia's 314 replaceDefault against the default 310 alone), a Progress and an Event reply play alike,
//   so no trace tells them apart: `oa g6 kinds` checks each reply as the Lua writes it. The traces see the kinds of
//   Raminey's 152 / 549 (a tie), Equette's 153 / 170 / 174 (ties) and 155 (importantOnce: 162 once spent, 155 after a zone
//   change), Chemioue's 561 (Event: Petals' 512 beats it) and Ondieulix's 559 / 514 / 516 (ties).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup6()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 6: the reply kinds no trace records ==");
        RunOtherAreasGroup6Kinds();
    }

    static Fake OA6At(QuestStatus status, int step = 0, int option = 0, params int[] keyItems)
    {
        var f = new Fake { Status = status, Step = step };
        if (option != 0) f.Vars["Option"] = option;
        foreach (int k in keyItems) f.KeyItems[k] = true;
        return f;
    }

    static void RunOtherAreasGroup6Kinds()
    {
        var abp = new QC.OtherAreas.ABitterPast().Compile();
        var ist = new QC.OtherAreas.InSearchOfTheTruth().Compile();
        var pfp = new QC.OtherAreas.PetalsForParelbriaux().Compile();
        var cos = new QC.OtherAreas.TheCallOfTheSea().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var acc = QuestStatus.Accepted;
        int wristlet = QC.KeyItem.TinyWristlet, cruse = QC.KeyItem.ShadedCruse;

        Fake Popping(int step)
        {
            var f = OA6At(acc, step);
            f.PopResult = true;
            return f;
        }
        string abpKinds = string.Join("|",
            G2Reply(G2Handler(abp, 0, trig, QC.Npc.TavnazianSafehold.Frescheque), OA6At(QuestStatus.Available)),
            G2Reply(G2Handler(abp, 1, trig, QC.Npc.TavnazianSafehold.Frescheque), OA6At(acc, 0, 0, wristlet)),
            G2Reply(G2Handler(abp, 1, trig, QC.Npc.TavnazianSafehold.Raminey), OA6At(acc, 0)),
            G2Reply(G2Handler(abp, 1, trig, QC.Npc.TavnazianSafehold.Equette), OA6At(acc, 1)),
            G2Reply(G2Handler(abp, 1, trig, QC.Npc.LufaiseMeadows.qmbitterpast), Popping(2)),
            G2Reply(G2Handler(abp, 2, trig, QC.Npc.TavnazianSafehold.Equette), OA6At(QuestStatus.Completed)));
        Check("oa g6 kinds:A Bitter Past's Frescheque 151 / 154, Raminey 152, Equette 153 Progress; the ??? a messageText (SENSE_OF_FOREBODING); the completed Equette 155 Event, importantOnce",
              "ProgressEvent(151)|ProgressEvent(154)|ProgressEvent(152)|ProgressEvent(153)|MessageText(24:SENSE_OF_FOREBODING)|Event(155):importantOnce()",
              abpKinds);

        string istKinds = string.Join("|",
            G2Reply(G2Handler(ist, 0, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(QuestStatus.Available)),
            G2Reply(G2Handler(ist, 1, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(acc, 0, 30)),
            G2Reply(G2Handler(ist, 1, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(acc, 0, 0)),
            G2Reply(G2Handler(ist, 1, trig, QC.Npc.TavnazianSafehold.Raminey), OA6At(acc, 0)),
            G2Reply(G2Handler(ist, 1, trig, QC.Npc.TavnazianSafehold.Zadant), OA6At(acc, 0)),
            G2Reply(G2Handler(ist, 1, trig, QC.Npc.TavnazianSafehold.Fouagine), OA6At(acc, 0)),
            G2Reply(G2Handler(ist, 1, trig, QC.Npc.TavnazianSafehold.Noam), OA6At(acc, 0)),
            G2Reply(G2Handler(ist, 2, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(acc, 1)),
            G2Reply(G2Handler(ist, 2, trig, QC.Npc.TavnazianSafehold.Mengrenaux), OA6At(acc, 1)),
            G2Reply(G2Handler(ist, 2, trig, QC.Npc.TavnazianSafehold.Chemioue), OA6At(acc, 1)),
            G2Reply(G2Handler(ist, 3, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(acc, 2, 1)),
            G2Reply(G2Handler(ist, 3, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(acc, 2, 0)),
            G2Reply(G2Handler(ist, 3, trig, QC.Npc.TavnazianSafehold.qm5insearchoftruth), OA6WithWater(OA6At(acc, 2, 0, cruse), 4)),
            G2Reply(G2Handler(ist, 4, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(QuestStatus.Completed, 0, 1)),
            G2Reply(G2Handler(ist, 4, trig, QC.Npc.TavnazianSafehold.Tressia), OA6At(QuestStatus.Completed, 0, 0)),
            G2Reply(G2Handler(ist, 4, trig, QC.Npc.TavnazianSafehold.Ondieulix), OA6At(QuestStatus.Completed, 0, 2)));
        Check("oa g6 kinds:In Search of the Truth's Tressia 544 (option 1 the cruse) / 557 Progress, 556 Event; Raminey 549, Zadant 551, Fouagine 553, Noam 555 Progress; Tressia 565 Progress, Mengrenaux 560 and Chemioue 561 Event; Tressia 562 Progress, 566 Event; qm5 558 Progress (option 1 the cruse); completed Tressia 562 Progress, 314 Event replaceDefault; Ondieulix 559 Progress",
              $"ProgressEvent(544,{{[1]={cruse}}})|ProgressEvent(557)|Event(556)|ProgressEvent(549)|ProgressEvent(551)|ProgressEvent(553)|ProgressEvent(555)|" +
              "ProgressEvent(565)|Event(560)|Event(561)|ProgressEvent(562)|Event(566)|" +
              $"ProgressEvent(558,{{[1]={cruse}}})|ProgressEvent(562)|Event(314):replaceDefault()|ProgressEvent(559)",
              istKinds);

        Fake Petal(int step)
        {
            var f = OA6At(acc, step);
            f.PlayerLocalVars["NMKilled"] = 1;
            return f;
        }
        string pfpKinds = string.Join("|",
            G2Reply(G2Handler(pfp, 0, trig, QC.Npc.TavnazianSafehold.Chemioue), OA6At(QuestStatus.Available, 0)),
            G2Reply(G2Handler(pfp, 0, trig, QC.Npc.TavnazianSafehold.Parelbriaux), OA6At(QuestStatus.Available, 1)),
            G2Reply(G2Handler(pfp, 0, trig, QC.Npc.TavnazianSafehold.Ondieulix), OA6At(QuestStatus.Available, 2)),
            G2Reply(G2Handler(pfp, 1, trig, QC.Npc.LufaiseMeadows.qmbaumesel), Petal(3)),
            G2Reply(G2Handler(pfp, 1, trig, QC.Npc.TavnazianSafehold.Ondieulix), OA6At(acc, 4)));
        Check("oa g6 kinds:Petals for Parelbriaux's Chemioue 512, Parelbriaux 513, Ondieulix 514 / 516 Progress; the ??? 115 a progressCutscene",
              "ProgressEvent(512)|ProgressEvent(513)|ProgressEvent(514)|ProgressCutscene(115)|ProgressEvent(516)",
              pfpKinds);

        string cosKinds = string.Join("|",
            G2Reply(G2Handler(cos, 0, trig, QC.Npc.TavnazianSafehold.Equette), OA6At(QuestStatus.Available, 0)),
            G2Reply(G2Handler(cos, 0, trig, QC.Npc.TavnazianSafehold.Leporaitceau), OA6At(QuestStatus.Available, 0)),
            G2Reply(G2Handler(cos, 0, trig, QC.Npc.TavnazianSafehold.Anteurephiaux), OA6At(QuestStatus.Available, 1)),
            G2Reply(G2Handler(cos, 1, trig, QC.Npc.TavnazianSafehold.Leporaitceau), OA6At(acc, 2)),
            G2Reply(G2Handler(cos, 1, trig, QC.Npc.TavnazianSafehold.Anteurephiaux), OA6At(acc, 3)),
            G2Reply(G2Handler(cos, 1, trig, QC.Npc.TavnazianSafehold.Equette), OA6At(acc, 4)));
        Check("oa g6 kinds:The Call of the Sea's Equette 170 / 174, Leporaitceau 171 (both sections), Anteurephiaux 172 / 173 Progress",
              "ProgressEvent(170)|ProgressEvent(171)|ProgressEvent(172)|ProgressEvent(171)|ProgressEvent(173)|ProgressEvent(174)",
              cosKinds);
    }

    static Fake OA6WithWater(Fake f, int water)
    {
        f.Vars["Water"] = water;
        return f;
    }
}
