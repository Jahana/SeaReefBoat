// XI.Verify.Quests -- quest rework B3b Other Areas group 5 (Despachiaire's quests: Elderly Pursuits, Secrets of Ovens Lost,
// Tango with a Tracker, Requiem of Sin, X Marks the Spot): the facet members the group added (Player.BattlefieldId,
// IQuestProgress.Cleanup), on the fake, and the arms no trace can record.
// - Elderly Pursuits' Para death, `mob:getID() == ID.mob.PARA` false (a clone of Para): the clones (PARA+1 to +4) spawn only
//   from Para's own weapon skills under 50% HP, and no flow statement spawns a mob (a kill of a mob that is not up runs no
//   death): `oa g5 para` checks the C# leaves Prog alone for a clone, beside the arms the traces hold.
// - Kinds: where no other quest and no NPC script answers the NPC (Jonette, the cookbook ???s, qm_para, Rouva at Prog 3 and
//   4), or where the only rival is X Marks the Spot's accepted 144 (importantOnce: Event priority; a Progress 518 / 577 /
//   579 beats it, an Event one would tie with it and play at the traces' steps all the same, the cycle's last action being
//   144), a Progress and an Event reply play alike, so no trace tells them apart: `oa g5 kinds` checks each reply as the
//   Lua writes it. The traces see the kinds of 517, 505, 576, 578, the completed 579 (ties with X Marks the Spot's
//   available 144 and with each other) and Rouva's 747 (a tie with Lure of the Wildcat's 808).
// - X Marks the Spot (added with IQuestProgress.Cleanup): the fake's Cleanup (recorded, the vars cleared) and the
//   completed section's Tavnazian Safehold onZoneOut on it (Post 2: Cleanup; Post 1, 0: nothing), beside the trace that
//   reaches it on Lua (x_marks_the_spot's warp at Post 2). Kinds: Odeya (141 / 143 Progress, 146 / 147 / 148 Event, the
//   trade's 142 Progress) and Phomiuna's _0r9 (37 Progress) have no rival and no script, and the accepted 144's Event
//   kind only ever meets a Default-priority rival (CoP's 106 / 315, below either kind): `oa g5 xmarks` checks each reply
//   as the Lua writes it. 143's finish with quest:complete false (a gil-only reward cannot fail) sets no Post: checked
//   on the fake. The traces see 144's available kind (a tie with Requiem's completed 579), its importantOnce
//   (315 once it is spent, 144 again after a zone change) and Parelbriaux's 145 (a tie with Petals for Parelbriaux's 513).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup5()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 5: facets, the arms no trace records ==");
        RunOtherAreasGroup5Facets();
        RunOtherAreasGroup5Para();
        RunOtherAreasGroup5Kinds();
        RunOtherAreasGroup5XMarks();
    }

    // player:getBattlefield():getID(), null when the char is in no battlefield; Tango's and Requiem's 32000 finishes on it
    static void RunOtherAreasGroup5Facets()
    {
        var none = new Fake();
        var tango = new QC.OtherAreas.TangoWithATracker().Compile();
        var requiem = new QC.OtherAreas.RequiemOfSin().Compile();
        var fin = QuestHandlerKind.OnEventFinish;
        string Begun(QuestScriptDefinition def, int? battlefield)
        {
            var f = new Fake { Status = QuestStatus.Available, BattlefieldId = battlefield };
            G2Handler(def, 0, fin, null, 32000).Run(f);
            return string.Join(";", f.Calls);
        }
        Check("oa g5 facets:the fake answers BattlefieldId (null when not set); Tango's 32000 begins in 677 only, Requiem's in 678 only, neither with no battlefield",
              "null/677|Quest.Begin()|||Quest.Begin()|",
              $"{(none.Player.BattlefieldId?.ToString() ?? "null")}/{new Fake { BattlefieldId = 677 }.Player.BattlefieldId}|" +
              $"{Begun(tango, 677)}|{Begun(tango, 678)}|{Begun(tango, null)}|{Begun(requiem, 678)}|{Begun(requiem, 677)}");
    }

    // an onTrigger by its zone too: the two cookbook ???s share a name
    static QuestHandlerDecl OA5Handler(QuestScriptDefinition def, int state, ushort zone, string npc) =>
        System.Linq.Enumerable.Single(def.States[state].Handlers, h => h.Kind == QuestHandlerKind.OnTrigger && h.Zone == zone && h.Entity == npc);

    static string OA5Run(QuestHandlerDecl h, Fake f)
    {
        h.Run(f);
        return string.Join(";", f.Calls);
    }

    // Elderly Pursuits' Para death: the first Para at Prog 1 sets Prog 2; a clone (another id of the Para template) at Prog
    // 1, and the first Para at Prog 0, set nothing
    static void RunOtherAreasGroup5Para()
    {
        var ep = new QC.OtherAreas.ElderlyPursuits().Compile();
        var death = G2Handler(ep, 1, QuestHandlerKind.OnMobDeath, QC.Npc.CarpentersLanding.Para);
        const int para = 16785716;
        Fake Dying(int mobId, int step)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = step, Mob = new QuestNpcFake(mobId) };
            f.MobIds["PARA"] = para;
            return f;
        }
        Check("oa g5 para:Para's death sets Prog 2 for the first Para (ID.mob.PARA) at Prog 1 only: a clone (PARA+1, +4) at Prog 1 and the first Para at Prog 0 set nothing",
              "Quest.SetStep(2)||||",
              $"{OA5Run(death, Dying(para, 1))}|{OA5Run(death, Dying(para + 1, 1))}|{OA5Run(death, Dying(para + 4, 1))}|" +
              $"{OA5Run(death, Dying(para, 0))}|{OA5Run(death, Dying(para - 1, 1))}");
    }

    // the reply kinds no trace can tell apart (no competitor, no NPC script, or a competitor that ranks below either kind)
    static void RunOtherAreasGroup5Kinds()
    {
        var ep = new QC.OtherAreas.ElderlyPursuits().Compile();
        var sol = new QC.OtherAreas.SecretsOfOvensLost().Compile();
        var tango = new QC.OtherAreas.TangoWithATracker().Compile();
        var requiem = new QC.OtherAreas.RequiemOfSin().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        const int para = 16785716;

        Fake At(QuestStatus status, int step = 0, params int[] keyItems)
        {
            var f = new Fake { Status = status, Step = step, Tally = 1800198000 };
            foreach (int k in keyItems) f.KeyItems[k] = true;
            f.MobIds["PARA"] = para;
            return f;
        }
        Fake Popping(int step)
        {
            var f = At(QuestStatus.Accepted, step, QC.KeyItem.AntiqueAmulet);
            f.PopResult = true;
            return f;
        }
        Fake Option(int option, QuestStatus status)
        {
            var f = At(status);
            f.Vars["Option"] = option;
            f.QuestStatuses[$"{QuestLog.OtherAreas}/SECRETS_OF_OVENS_LOST"] = status;
            return f;
        }
        int cookbook = QC.KeyItem.TavnazianCookbook, medallion = QC.KeyItem.CathedralMedallion;

        string epKinds = string.Join("|",
            G2Reply(G2Handler(ep, 1, trig, QC.Npc.CarpentersLanding.qmpara), Popping(1)),
            G2Reply(G2Handler(ep, 1, trig, QC.Npc.CarpentersLanding.qmpara), At(QuestStatus.Accepted, 2, QC.KeyItem.AntiqueAmulet)),
            G2Reply(G2Handler(ep, 2, trig, QC.Npc.SouthernSanDoria.Rouva), At(QuestStatus.Accepted, 3, medallion)),
            G2Reply(G2Handler(ep, 2, trig, QC.Npc.TavnazianSafehold.Despachiaire), At(QuestStatus.Accepted, 4, medallion)));
        string solKinds = string.Join("|",
            G2Reply(G2Handler(sol, 0, trig, QC.Npc.TavnazianSafehold.Jonette), Option(1, QuestStatus.Available)),
            G2Reply(G2Handler(sol, 0, trig, QC.Npc.TavnazianSafehold.Jonette), Option(0, QuestStatus.Completed)),
            G2Reply(G2Handler(sol, 1, trig, QC.Npc.TavnazianSafehold.Jonette), At(QuestStatus.Accepted, 1, cookbook)),
            G2Reply(OA5Handler(sol, 1, QC.Zone.Sacrarium, QC.Npc.Sacrarium.qmtavnaziancookbook), At(QuestStatus.Accepted, 1)),
            G2Reply(OA5Handler(sol, 1, QC.Zone.PhomiunaAqueducts, QC.Npc.PhomiunaAqueducts.qmtavnaziancookbook), At(QuestStatus.Accepted, 1)));
        string tangoKinds = G2Reply(G2Handler(tango, 1, trig, QC.Npc.TavnazianSafehold.Despachiaire), At(QuestStatus.Accepted));
        string requiemKinds = G2Reply(G2Handler(requiem, 1, trig, QC.Npc.TavnazianSafehold.Despachiaire), At(QuestStatus.Accepted));
        Check("oa g5 kinds:Elderly Pursuits' qm_para a messageSpecial (STENCH_OF_DECAY; KEYITEM_OBTAINED), Rouva 748 and Despachiaire 518 Progress; Secrets of Ovens Lost's Jonette 506 / 507 / 508 Progress, the ???s a keyItem reply; Tango's accepted 577 and Requiem's accepted 579 Progress",
              "MessageSpecial(2:STENCH_OF_DECAY)|" +
              $"MessageSpecial(2:KEYITEM_OBTAINED,{medallion})|ProgressEvent(748)|ProgressEvent(518)||" +
              $"ProgressEvent(506)|ProgressEvent(507)|ProgressEvent(508)|KeyItem({cookbook})|KeyItem({cookbook})||" +
              "ProgressEvent(577)|ProgressEvent(579)",
              $"{epKinds}||{solKinds}||{tangoKinds}|{requiemKinds}");
    }

    // X Marks the Spot: quest:cleanup on the fake and the completed section's zone-out; the reply kinds no trace sees
    static void RunOtherAreasGroup5XMarks()
    {
        var xm = new QC.OtherAreas.XMarksTheSpot().Compile();
        var trig = QuestHandlerKind.OnTrigger;

        var cleaned = new Fake { Status = QuestStatus.Completed, Step = 3 };
        cleaned.Vars["Post"] = 2;
        cleaned.Quest.Cleanup();
        string cleanup = $"{string.Join(";", cleaned.Calls)}/{cleaned.Vars.Count}/{cleaned.Quest.Step}/{cleaned.Quest.Var("Post")}";

        var zoneOut = System.Linq.Enumerable.Single(xm.States[2].Handlers, h => h.Kind == QuestHandlerKind.OnZoneOut);
        string Leaving(int post)
        {
            var f = new Fake { Status = QuestStatus.Completed };
            if (post != 0) f.Vars["Post"] = post;
            zoneOut.Run(f);
            return $"{string.Join(";", f.Calls)}/{f.Quest.Var("Post")}";
        }
        Check("oa g5 xmarks:the fake's Quest.Cleanup() is recorded and clears every var (Prog too); X Marks the Spot's completed Tavnazian Safehold zone-out cleans up at Post 2 only",
              $"Quest.Cleanup()/0/0/0|{QC.Zone.TavnazianSafehold}|Quest.Cleanup()/0|/1|/0|/3",
              $"{cleanup}|{zoneOut.Zone}|{Leaving(2)}|{Leaving(1)}|{Leaving(0)}|{Leaving(3)}");

        // 143's finish: Post 1 only when quest:complete returned true. The reward is gil only (npcUtil.completeQuest cannot
        // fail on it), so no flow reaches the false arm
        string Completing(bool result)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 4, CompleteResult = result };
            G2Handler(xm, 1, QuestHandlerKind.OnEventFinish, null, 143).Run(f);
            return $"{string.Join(";", f.Calls)}/{f.Quest.Var("Post")}";
        }
        Check("oa g5 xmarks:143's finish sets Post 1 when the quest completes, nothing when quest:complete fails (a gil-only reward: no flow can fail it)",
              "Quest.Complete();Quest.SetVar(Post,1,0)/1|Quest.Complete()/0",
              $"{Completing(true)}|{Completing(false)}");

        Fake At(int step, int post = 0)
        {
            var f = new Fake { Status = post == 0 ? QuestStatus.Accepted : QuestStatus.Completed, Step = step };
            if (post != 0) f.Vars["Post"] = post;
            return f;
        }
        var odeya = G2Handler(xm, 1, trig, QC.Npc.TavnazianSafehold.Odeya);
        var trading = At(2);
        trading.TradeItems[QC.Item.TavnazianLiver] = 1;
        string kinds = string.Join("|",
            G2Reply(odeya, At(1)), G2Reply(odeya, At(2)), G2Reply(odeya, At(3)), G2Reply(odeya, At(4)), G2Reply(odeya, At(0)),
            G2Reply(G2Handler(xm, 1, QuestHandlerKind.OnTrade, QC.Npc.TavnazianSafehold.Odeya), trading),
            G2Reply(G2Handler(xm, 2, trig, QC.Npc.TavnazianSafehold.Odeya), At(0, 1)),
            G2Reply(G2Handler(xm, 1, trig, QC.Npc.PhomiunaAqueducts._0r9), At(3)),
            G2Reply(G2Handler(xm, 1, trig, QC.Npc.TavnazianSafehold.Despachiaire), At(0)));
        Check("oa g5 xmarks:Odeya 141 (0, the liver) / 143 Progress, 146 (0, the liver) / 147 Event, nothing at Prog 0; the liver's 142 Progress; completed 148 Event; _0r9 37 Progress; the accepted Despachiaire 144 Event, importantOnce",
              $"ProgressEvent(141,0,{QC.Item.TavnazianLiver})|Event(146,0,{QC.Item.TavnazianLiver})|Event(147)|ProgressEvent(143)|null|" +
              "ProgressEvent(142)|Event(148)|ProgressEvent(37)|Event(144):importantOnce()", kinds);
    }
}
