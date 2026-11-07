// XI.Verify.Quests -- quest rework B3b Outlands group 5 (Soul Searching, Wandering Souls, Wrath of the Opo-Opos, The Kuftal
// Tour, The Missing Piece, Open Sesame): what the golden traces cannot show.
// - The reply kinds of NPCs that only these quests answer and that have no script of their own (the three Cermet
//   Headstones, whose Zilart mission answers nil beside them; Datta, Alfesar, Quicksand Caves' qm5, Charlaimagnat,
//   Lokpix): with no rival and no script turn, a Progress and an Event reply send the same packets, and so do a reply with
//   replaceDefault and one without (Alfesar's 11, Lokpix's 24). Kuftal Tunnel's qm5 14 is shown Progress by its tie with
//   Bastok 8-2's 12 (#mission); it is pinned here too.
// - The Kuftal Tour's alliance test at its edges (a member at exactly 40 and at 41, at exactly 15 yalms and just past
//   them), base and module: a flow cannot place a char at exactly 15 yalms.
// - quest:complete's false arm where the reward cannot fail: Open Sesame's 22 (a key item) and The Kuftal Tour's 75 (gil
//   and a title, the result not read: the fame is added anyway); The Missing Piece's 705 is taken by a flow (a full bag).
// - npcUtil.pickNewPosition when Quicksand Caves' qm5 stands on a spot of the table (the flows find it at its spawn spot,
//   none of the table's, so index 1 is "current"): that spot is never drawn.
// - The two era modules' conditions, xi.pre(ROV) and xi.pre(SOA): a module flow needs it true (a module not enabled fails
//   the run), so only the true side is traced.
// - Each section's status (a section in another status is masked by the rival sections or by the default live).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup5()
    {
        Console.WriteLine("== quest rework B3b Outlands group 5: lone NPCs' reply kinds, the Kuftal alliance edges, complete false, qm5's move ==");
        OL5Headstones();
        OL5Kinds();
        OL5Alliance();
        OL5CompleteFalse();
        OL5PickNewPosition();
        OL5Modules();
        OL5States();
    }

    static Fake OL5At(int step, QuestStatus status = QuestStatus.Accepted)
    {
        var f = new Fake { Status = status };
        f.Vars["Prog"] = step;
        return f;
    }

    // a handler's effects on a fake, as its Calls list records them
    static string OL5Run(QuestHandlerDecl h, Fake f)
    {
        h.Run(f);
        return string.Join(";", f.Calls);
    }

    static void OL5Headstones()
    {
        var ss = new QC.Outlands.SoulSearching().Compile();
        var ws = new QC.Outlands.WanderingSouls().Compile();
        var wo = new QC.Outlands.WrathOfTheOpoOpos().Compile();
        var frag = OL5At(0);
        frag.KeyItems[QC.KeyItem.PrismaticFragment] = true;
        var lily = OL5At(0);
        lily.TradeItems[QC.Item.RainLily] = 1;
        var garnet = OL5At(0);
        garnet.TradeItems[QC.Item.Garnet] = 1;
        Check("outlands g5:the headstones' reply kinds: Soul Searching's 202 / message; Wandering Souls' 202 / message; Wrath of the Opo-Opos' 202 / message",
              $"ProgressEvent(202,{QC.KeyItem.PrismaticFragment})|MessageSpecial({QC.Zone.TheSanctuaryOfZitah}:AIR_REMAINS_STAGNANT)|" +
              $"ProgressEvent(202,{QC.Item.RainLily})|MessageSpecial({QC.Zone.CapeTeriggan}:MUST_BE_A_WAY_TO_SOOTHE)|" +
              $"ProgressEvent(202,{QC.Item.Garnet})|MessageSpecial({QC.Zone.YuhtungaJungle}:TOUCHING_RED_JEWEL)",
              $"{OLReply(OLHandler(ss, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.TheSanctuaryOfZitah.CermetHeadstone), frag)}|" +
              $"{OLReply(OLHandler(ss, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.TheSanctuaryOfZitah.CermetHeadstone), OL5At(0))}|" +
              $"{OLReply(OLHandler(ws, 0, QuestHandlerKind.OnTrade, entity: QC.Npc.CapeTeriggan.CermetHeadstone), lily)}|" +
              $"{OLReply(OLHandler(ws, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.CapeTeriggan.CermetHeadstone), OL5At(0))}|" +
              $"{OLReply(OLHandler(wo, 0, QuestHandlerKind.OnTrade, entity: QC.Npc.YuhtungaJungle.CermetHeadstone), garnet)}|" +
              OLReply(OLHandler(wo, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.YuhtungaJungle.CermetHeadstone), OL5At(0)));
    }

    static void OL5Kinds()
    {
        var kt = new QC.Outlands.TheKuftalTour().Compile();
        var pair = OL5At(0);
        pair.Party = 2;
        pair.MainLevel = 30;
        pair.AllianceMembers.Add((30, 1.0));
        Check("outlands g5:The Kuftal Tour's reply kinds: Datta 74 / 75; Kuftal Tunnel's qm5 14",
              "ProgressEvent(74)|ProgressEvent(75)|ProgressEvent(14)",
              $"{OLReply(OLHandler(kt, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Datta), new Fake())}|" +
              $"{OLReply(OLHandler(kt, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Datta), OL5At(1))}|" +
              OLReply(OLHandler(kt, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.KuftalTunnel.qm5), pair));

        var mp = new QC.Outlands.TheMissingPiece().Compile();
        var alf = OLHandler(mp, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Alfesar);
        var ch = OLHandler(mp, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.NorthernSanDoria.Charlaimagnat);
        Fake Waiting(int wait, int now)
        {
            var f = OL5At(3);
            f.Vars["Wait"] = wait;
            f.Now = now;
            return f;
        }
        var qm = OL5At(0);
        qm.Npc = new QuestNpcFake(17629736, qm.Calls) { XPos = 781.727, YPos = -16.0, ZPos = -825.022 };
        foreach (int r in new[] { 1, 2 }) qm.Rolls.Enqueue(r);
        Check("outlands g5:The Missing Piece's reply kinds: Alfesar 6 / 7 / 8 / 9 / 11 (replaceDefault); Charlaimagnat 703 / 704 / 705; qm5's key item",
              $"ProgressEvent(6,{QC.KeyItem.AncientTabletFragment})|Event(7,{QC.KeyItem.AncientTabletFragment})|" +
              $"ProgressEvent(8,{QC.KeyItem.AncientTabletFragment},{QC.KeyItem.TabletOfAncientMagic},{QC.KeyItem.LetterFromAlfesar})|" +
              $"Event(9,0,{QC.KeyItem.TabletOfAncientMagic})|Event(11):replaceDefault()|ProgressEvent(703)|Event(704)|ProgressEvent(705)|" +
              $"KeyItem({QC.KeyItem.AncientTabletFragment})",
              $"{OLReply(OLHandler(mp, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Alfesar), new Fake())}|" +
              $"{OLReply(alf, OL5At(0))}|{OLReply(alf, OL5At(1))}|{OLReply(alf, OL5At(2))}|" +
              $"{OLReply(OLHandler(mp, 2, QuestHandlerKind.OnTrigger, entity: QC.Npc.Rabao.Alfesar), new Fake { Status = QuestStatus.Completed })}|" +
              $"{OLReply(ch, OL5At(2))}|{OLReply(ch, Waiting(1800000001, 1800000000))}|{OLReply(ch, Waiting(1800000000, 1800000000))}|" +
              OLReply(OLHandler(mp, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.QuicksandCaves.qm5), qm));

        var os = new QC.Outlands.OpenSesame().Compile();
        var lok = OLHandler(os, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.EasternAltepaDesert.Lokpix);
        var right = OL5At(0);
        right.TradeItems[QC.Item.Meteorite] = 1;
        right.TradeItems[QC.Item.Tremorstone] = 1;
        var wrong = OL5At(0);
        wrong.TradeItems[QC.Item.Meteorite] = 2;
        wrong.TradeItems[QC.Item.Tremorstone] = 1;
        Check("outlands g5:Lokpix's reply kinds: 20 / 21 / the trade's 22 / 23 / 24 (replaceDefault)",
              "ProgressEvent(20)|Event(21)|ProgressEvent(22)|Event(23)|Event(24):replaceDefault()",
              $"{OLReply(OLHandler(os, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.EasternAltepaDesert.Lokpix), new Fake())}|" +
              $"{OLReply(OLHandler(os, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.EasternAltepaDesert.Lokpix), OL5At(0))}|" +
              $"{OLReply(lok, right)}|{OLReply(lok, wrong)}|" +
              OLReply(OLHandler(os, 2, QuestHandlerKind.OnTrigger, entity: QC.Npc.EasternAltepaDesert.Lokpix), new Fake { Status = QuestStatus.Completed }));
    }

    // qm5's alliance test: every member (the char among them) at level 40 or below and within 15 yalms of the char; the base
    // asks for a party of 2 or more, the module for exactly 6
    static void OL5Alliance()
    {
        Fake Party(int size, int level, double distance)
        {
            var f = OL5At(0);
            f.MainLevel = 30;
            f.Party = size;
            f.AllianceMembers.Add((level, distance));
            return f;
        }
        var kt = new QC.Outlands.TheKuftalTour().Compile();
        var qm = OLHandler(kt, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.KuftalTunnel.qm5);
        var alone = OL5At(0);
        alone.MainLevel = 30;
        var high = OL5At(0);
        high.MainLevel = 41;
        high.Party = 2;
        Check("outlands g5:Kuftal Tunnel's qm5: a member at 40 and 15 yalms: 14; at 41: nil; at 15.001 yalms: nil; alone: nil; the char at 41: nil",
              "ProgressEvent(14)|null|null|null|null",
              $"{OLReply(qm, Party(2, 40, 15.0))}|{OLReply(qm, Party(2, 41, 1.0))}|{OLReply(qm, Party(2, 40, 15.001))}|" +
              $"{OLReply(qm, alone)}|{OLReply(qm, high)}");

        var applied = QuestOverrides.Apply(new QC.Outlands.TheKuftalTour().Compile(),
            new QC.Modules.Era.Outlands.TheKuftalTour().Compile(new Fake()), QC.QuestKey.Outlands.TheKuftalTour);
        var mqm = OLHandler(applied.Definition, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.KuftalTunnel.qm5);
        Check("outlands g5:the Kuftal Tour module's qm5: a party of 6 at 40 and 15 yalms: 14; 5: nil; 6 with one at 41: nil; at 15.001 yalms: nil",
              "the_kuftal_tour|ProgressEvent(14)|null|null|null",
              $"{mqm.Module?.Name}|{OLReply(mqm, Party(6, 40, 15.0))}|{OLReply(mqm, Party(5, 40, 1.0))}|{OLReply(mqm, Party(6, 41, 1.0))}|" +
              OLReply(mqm, Party(6, 40, 15.001)));
    }

    static void OL5CompleteFalse()
    {
        var os = new QC.Outlands.OpenSesame().Compile();
        var kt = new QC.Outlands.TheKuftalTour().Compile();
        Fake Done(bool completes) => new Fake { Status = QuestStatus.Accepted, CompleteResult = completes };
        Check("outlands g5:Open Sesame's 22 complete false (not reachable: a key item reward): nothing more; true: the fame, tradeComplete",
              $"Quest.Complete()|Quest.Complete();Player.AddFame({QC.FameArea.Sandoria},15);Player.AddFame({QC.FameArea.Bastok},15);Trade.Complete()",
              $"{OL5Run(OLHandler(os, 1, QuestHandlerKind.OnEventFinish, id: 22), Done(false))}|" +
              OL5Run(OLHandler(os, 1, QuestHandlerKind.OnEventFinish, id: 22), Done(true)));
        Check("outlands g5:The Kuftal Tour's 75 complete false (not reachable: gil and a title): the fame added anyway (the result not read)",
              $"Quest.Complete();Player.AddFame({QC.FameArea.Sandoria},10);Player.AddFame({QC.FameArea.Bastok},10)",
              OL5Run(OLHandler(kt, 1, QuestHandlerKind.OnEventFinish, id: 75), Done(false)));
    }

    // npcUtil.pickNewPosition(npc:getID(), positionTable): randomInt(1, 0) first (the fake dequeues the 1 given), the qm's spot
    // found by its floored coordinates (here [4], 451 -16 -739), randomInt(1, 5) until it differs (4, 4, then 2), the move
    static void OL5PickNewPosition()
    {
        var mp = new QC.Outlands.TheMissingPiece().Compile();
        var f = OL5At(0);
        f.Npc = new QuestNpcFake(17629736, f.Calls) { XPos = 451.4, YPos = -15.6, ZPos = -738.2 };
        foreach (int r in new[] { 1, 4, 4, 2 }) f.Rolls.Enqueue(r);
        var reply = OLReply(OLHandler(mp, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.QuicksandCaves.qm5), f);
        Check("outlands g5:Quicksand Caves' qm5 on spot 4: Prog 1, randomInt(1, 0), randomInt(1, 5) until not 4, the move to spot 2, the key item",
              $"Quest.SetStep(1);Player.RandomInt(1,0);Player.RandomInt(1,5);Player.RandomInt(1,5);Player.RandomInt(1,5);Npc.SetPos(657,0,-537)|KeyItem({QC.KeyItem.AncientTabletFragment})",
              $"{string.Join(";", f.Calls)}|{reply}");
    }

    // each section's status test: the Lua's checks read the status alone (`status == xi.questStatus.QUEST_<X>`), so a section
    // in another status (Lokpix's completed 24 under 20 / 21, Kuftal's accepted qm5 for a char in the party without the quest)
    // is masked live or shows only on a fake
    static void OL5States()
    {
        string States(QuestScript q) => string.Join(",", q.Compile().States.Select(st =>
            string.Join("+", st.Matches.Select(mt => mt.AnyStatus ? "any" : mt.Status.ToString()))));
        Check("outlands g5:the sections' statuses: Soul Searching, Wandering Souls, Wrath, The Kuftal Tour, The Missing Piece, Open Sesame",
              "Accepted|Accepted|Accepted|Available,Accepted|Available,Accepted,Completed|Available,Accepted,Completed",
              $"{States(new QC.Outlands.SoulSearching())}|{States(new QC.Outlands.WanderingSouls())}|{States(new QC.Outlands.WrathOfTheOpoOpos())}|" +
              $"{States(new QC.Outlands.TheKuftalTour())}|{States(new QC.Outlands.TheMissingPiece())}|{States(new QC.Outlands.OpenSesame())}");
    }

    // xi.pre(xi.expansion.ROV) / xi.pre(xi.expansion.SOA): content restricted and the expansion off; either setting otherwise: off
    static void OL5Modules()
    {
        Fake With(int restrict, string expansion, int enabled)
        {
            var f = new Fake();
            f.MainSettings["RESTRICT_CONTENT"] = restrict;
            f.MainSettings["ENABLE_" + expansion] = enabled;
            return f;
        }
        var kt = new QC.Modules.Era.Outlands.TheKuftalTour();
        var mp = new QC.Modules.Era.Outlands.TheMissingPiece();
        Check("outlands g5:the era modules' conditions: The Kuftal Tour xi.pre(ROV), The Missing Piece xi.pre(SOA): restricted, off / on / unrestricted",
              "True|False|False|True|False|False",
              $"{kt.Condition(With(1, "ROV", 0))}|{kt.Condition(With(1, "ROV", 1))}|{kt.Condition(With(0, "ROV", 0))}|" +
              $"{mp.Condition(With(1, "SOA", 0))}|{mp.Condition(With(1, "SOA", 1))}|{mp.Condition(With(0, "SOA", 0))}");

        var applied = QuestOverrides.Apply(new QC.Outlands.TheMissingPiece().Compile(),
            new QC.Modules.Era.Outlands.TheMissingPiece().Compile(new Fake()), QC.QuestKey.Outlands.TheMissingPiece);
        var f703 = OL5At(2);
        f703.Now = 1800000000;
        f703.NextJstMidnight = 1800025200;
        Check("outlands g5:the Missing Piece module's 703: the base (Wait = now + 60, the title, the key items, Prog 3), then Wait = JstMidnight()",
              $"Quest.SetVar(Wait,1800000060,0);Player.AddTitle({QC.Title.AcquirerOfAncientArcanum});Player.DelKeyItem({QC.KeyItem.TabletOfAncientMagic});" +
              $"Player.DelKeyItem({QC.KeyItem.LetterFromAlfesar});Quest.SetStep(3);Quest.SetVar(Wait,1800025200,0)",
              OL5Run(OLHandler(applied.Definition, 1, QuestHandlerKind.OnEventFinish, id: 703), f703));
    }
}
