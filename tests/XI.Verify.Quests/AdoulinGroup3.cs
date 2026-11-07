// XI.Verify.Quests -- quest rework B3b Adoulin group 3 (the frontier: Breaking the Ice, I'm on a Boat,
// A Stone's Throw Away, Hide and Go Peak): each definition validates clean and declares the steps it sets. Their
// branches are the flows' (tests/golden/flows/adoulin.*.flow), except what no trace can see:
// - The reply kinds. Traiffeaux, Apolliane and Toppled Tree have no script and no default; Choubollet's default 2564,
//   the Castoff Points' and Scalable Areas' messages and the Velkk Cache's rank Default, below any Event; each NPC has
//   one container up at a time (the completed pads' state is never up with the accepted one). A Progress beats an Event
//   and equal kinds rotate, so no trace tells a Progress from an Event here. All are pinned, each the Lua's kind.
// - The finishes with quest:complete false (no item reward: never false in play): Breaking the Ice's 26,
//   I'm on a Boat's 2563 and A Stone's Throw Away's 2573 keep the trade and give no key item; Hide and Go Peak's 7
//   reads no result.
// - Finish 19 at an NPC whose name ends in no 4 or 5 (19 comes only from Castoff_Point_4 and _5): nothing on option 1;
//   at _5 on Prog 0 (point 5 first, as the boat flow also plays it): Prog 2.
// - Each quest's LegacyPath, its own Lua file (its slot in the containers list); the rewards.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAdoulinGroup3()
    {
        Console.WriteLine("== quest rework B3b Adoulin group 3 ==");
        var quests = new QuestScript[] { new QC.Adoulin.BreakingTheIce(), new QC.Adoulin.ImOnABoat(),
            new QC.Adoulin.AStonesThrowAway(), new QC.Adoulin.HideAndGoPeak() };
        foreach (var quest in quests)
        {
            string name = quest.GetType().Name;
            Check($"adoulin g3:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(quest.Compile())));
        }
        Check("adoulin g3:the steps each quest sets",
              " | 1,2 |  | 1,2",
              string.Join(" | ", quests.Select(q => string.Join(",", q.Compile().DeclaredSteps))));

        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => m.AnyStatus ? "any" : m.Status.ToString()))));
        Check("adoulin g3:the states' statuses",
              "Available Accepted | Available Accepted Completed | Available Accepted | Available Accepted Completed",
              string.Join(" | ", quests.Select(States)));

        Check("adoulin g3:each quest's LegacyPath is its own Lua file",
              "Breaking_the_Ice ; Im_on_a_Boat ; A_Stones_Throw_Away ; Hide_and_Go_Peak",
              string.Join(" ; ", quests.Select(q => q.LegacyPath.Substring("scripts/quests/adoulin/".Length))));

        // fame / fameArea / bayld / exp / gil / item (no fame field: npcUtil.completeQuest's 30, as the traces show)
        string Rw(QuestScript q)
        {
            var r = q.Compile().Reward;
            return $"{r.Fame?.ToString() ?? "-"}/{r.FameArea}/{r.Bayld}/{r.Exp?.ToString() ?? "-"}/" +
                   $"{r.Gil?.ToString() ?? "-"}/" +
                   $"{r.Item?.ToString() ?? "-"}";
        }
        int ad = QC.FameArea.Adoulin;
        Check("adoulin g3:the rewards",
              $"-/{ad}/500/-/-/- | -/{ad}/500/-/-/- | -/{ad}/500/-/-/- | -/{ad}/500/-/-/-",
              string.Join(" | ", quests.Select(Rw)));

        RunAdoulinGroup3Kinds();
        RunAdoulinGroup3Finishes();
    }

    static Fake AD3Fake(QuestStatus st, int prog = 0)
    {
        var f = new Fake { Status = st };
        if (prog != 0) f.Step = prog;
        return f;
    }

    // every reply of the four (the pads: one of each zone's), each as the fake state that offers it gives it
    static void RunAdoulinGroup3Kinds()
    {
        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        ushort kam = QC.Zone.KamihrDrifts, foret = QC.Zone.ForetDeHennetiel, mori = QC.Zone.MorimarBasaltFields,
               marj = QC.Zone.MarjamiRavine, cird = QC.Zone.CirdasCaverns;
        var bi = new QC.Adoulin.BreakingTheIce().Compile();
        var bo = new QC.Adoulin.ImOnABoat().Compile();
        var st = new QC.Adoulin.AStonesThrowAway().Compile();
        var hg = new QC.Adoulin.HideAndGoPeak().Compile();
        Fake Items(int prog, params (int Item, int Qty)[] items)
        {
            var f = AD3Fake(QuestStatus.Accepted, prog);
            foreach (var (item, qty) in items) f.TradeItems[item] = qty;
            return f;
        }
        Fake Craft(int prog, bool title = false)
        {
            var f = AD3Fake(QuestStatus.Accepted, prog);
            f.KeyItems[QC.KeyItem.Watercraft] = true;
            if (title) f.Titles.Add(QC.Title.ToxinTussler);
            return f;
        }
        Fake Pad(ushort zone, string offsetKey, int offset, int id)
        {
            var f = AD3Fake(QuestStatus.Completed);
            f.Zone = zone;
            f.NpcIds[offsetKey] = offset;
            f.Npc = new QuestNpcFake(id);
            return f;
        }
        string Choub = QC.Npc.ForetDeHennetiel.Choubollet;

        L("bi offer", AU3Npc(bi, Tr, kam, QC.Npc.KamihrDrifts.Traiffeaux, AD3Fake(QuestStatus.Available)));
        L("bi accepted", AU3Npc(bi, Tr, kam, QC.Npc.KamihrDrifts.Traiffeaux, AD3Fake(QuestStatus.Accepted)));
        L("bi trade", AU3Npc(bi, Td, kam, QC.Npc.KamihrDrifts.Traiffeaux,
                             Items(0, (QC.Item.RabbitHide, 3), (QC.Item.RaazTusk, 1))));
        L("bo offer", AU3Npc(bo, Tr, foret, Choub, AD3Fake(QuestStatus.Available)));
        L("bo no craft", AU3Npc(bo, Tr, foret, Choub, AD3Fake(QuestStatus.Accepted)));
        L("bo prog 2", AU3Npc(bo, Tr, foret, Choub, Craft(2)));
        L("bo prog 1", AU3Npc(bo, Tr, foret, Choub, Craft(1)));
        L("bo trade", AU3Npc(bo, Td, foret, Choub, Items(0, (QC.Item.SquareOfDhalmelLeather, 3),
                                                             (QC.Item.UmbrilOoze, 1), (QC.Item.TwitherymScale, 1))));
        L("bo point 4", AU3Npc(bo, Tr, foret, QC.Npc.ForetDeHennetiel.CastoffPoint4, Craft(0)));
        L("bo point 5", AU3Npc(bo, Tr, foret, QC.Npc.ForetDeHennetiel.CastoffPoint5, Craft(1, true)));
        L("bo foret pad 3", AU3Npc(bo, Tr, foret, QC.Npc.ForetDeHennetiel.CastoffPoint3,
                                   Pad(foret, "CASTOFF_POINT_OFFSET", 17850930, 17850933)));
        L("bo cirdas pad 0", AU3Npc(bo, Tr, cird, QC.Npc.CirdasCaverns.CastoffPoint,
                                    Pad(cird, "CASTOFF_POINT_OFFSET", 17883919, 17883919)));
        L("st offer", AU3Npc(st, Tr, mori, QC.Npc.MorimarBasaltFields.Apolliane, AD3Fake(QuestStatus.Available)));
        L("st accepted", AU3Npc(st, Tr, mori, QC.Npc.MorimarBasaltFields.Apolliane, AD3Fake(QuestStatus.Accepted)));
        L("st trade", AU3Npc(st, Td, mori, QC.Npc.MorimarBasaltFields.Apolliane, Items(1, (QC.Item.MarbleNugget, 1))));
        L("hg offer", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.ToppledTree, AD3Fake(QuestStatus.Available)));
        L("hg prog 0", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.ToppledTree, AD3Fake(QuestStatus.Accepted)));
        L("hg prog 1", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.ToppledTree, AD3Fake(QuestStatus.Accepted, 1)));
        L("hg prog 2", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.ToppledTree, AD3Fake(QuestStatus.Accepted, 2)));
        L("hg climb", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.ScalableArea2, AD3Fake(QuestStatus.Accepted, 2)));
        L("hg cache", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.VelkkCache, AD3Fake(QuestStatus.Accepted)));
        L("hg marjami pad 2", AU3Npc(hg, Tr, marj, QC.Npc.MarjamiRavine.ScalableArea2,
                                     Pad(marj, "SCALABLE_AREA_OFFSET", 17867181, 17867183)));
        Check("adoulin g3:the reply kinds",
              "bi offer=ProgressEvent(24) ; bi accepted=Event(25) ; bi trade=ProgressEvent(26) ; " +
              "bo offer=ProgressEvent(2561) ; bo no craft=Event(2562) ; bo prog 2=ProgressEvent(2563) ; " +
              "bo prog 1=Event(2577) ; bo trade=ProgressEvent(2576) ; bo point 4=Event(19,4,0) ; " +
              "bo point 5=Event(19,5,1) ; bo foret pad 3=Event(18,3) ; bo cirdas pad 0=Event(11,1) ; " +
              "st offer=ProgressEvent(2571) ; st accepted=Event(2572) ; st trade=ProgressEvent(2573) ; " +
              "hg offer=ProgressEvent(3) ; hg prog 0=Event(4) ; hg prog 1=ProgressEvent(5) ; hg prog 2=Event(6) ; " +
              "hg climb=ProgressEvent(7) ; hg cache=KeyItem(" + QC.KeyItem.LargeStripOfVelkkHide + ") ; " +
              "hg marjami pad 2=Event(16,{[5]=3})",
              string.Join(" ; ", lines));
    }

    // the finishes with quest:complete true and false (false never happens in play: no item reward), and finish 19 at
    // NPCs of each name ending (19 comes only from points 4 and 5)
    static void RunAdoulinGroup3Finishes()
    {
        string Fin(QuestScript quest, int csid, bool complete)
        {
            var d = quest.Compile();
            var fin = d.States.SelectMany(s => s.Handlers)
                .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == csid);
            var f = AD3Fake(QuestStatus.Accepted, 2);
            f.CompleteResult = complete;
            fin.Run(f);
            return $"{csid}:{string.Join(";", f.Calls)}";
        }
        Check("adoulin g3:the turn-in finishes, completed / not",
              $"26:Player.GiveKeyItem({QC.KeyItem.PairOfFuzzyEarmuffs});" +
              $"Text.Special({QC.Zone.KamihrDrifts}:YOU_HAVE_LEARNED,{QC.KeyItem.Fragmenting});Quest.Complete();" +
              $"Trade.Confirm();Player.AddKeyItem({QC.KeyItem.Fragmenting}) | " +
              $"26:Player.GiveKeyItem({QC.KeyItem.PairOfFuzzyEarmuffs});" +
              $"Text.Special({QC.Zone.KamihrDrifts}:YOU_HAVE_LEARNED,{QC.KeyItem.Fragmenting});Quest.Complete() | " +
              $"2563:Text.Special({QC.Zone.ForetDeHennetiel}:YOU_HAVE_LEARNED,{QC.KeyItem.Watercrafting});" +
              $"Quest.Complete();Player.DelKeyItem({QC.KeyItem.Watercraft});" +
              $"Player.AddKeyItem({QC.KeyItem.Watercrafting}) | " +
              $"2563:Text.Special({QC.Zone.ForetDeHennetiel}:YOU_HAVE_LEARNED,{QC.KeyItem.Watercrafting});" +
              $"Quest.Complete() | " +
              $"2573:Text.Special({QC.Zone.MorimarBasaltFields}:YOU_HAVE_LEARNED,{QC.KeyItem.Demolishing});" +
              $"Quest.Complete();Player.AddKeyItem({QC.KeyItem.Demolishing});Trade.Confirm() | " +
              $"2573:Text.Special({QC.Zone.MorimarBasaltFields}:YOU_HAVE_LEARNED,{QC.KeyItem.Demolishing});" +
              $"Quest.Complete() | " +
              $"7:Player.GiveKeyItem({QC.KeyItem.PairOfVelkkGloves});Player.AddKeyItem({QC.KeyItem.Climbing});" +
              $"Text.Special({QC.Zone.MarjamiRavine}:YOU_HAVE_LEARNED,{QC.KeyItem.Climbing});Quest.Complete() | " +
              $"7:Player.GiveKeyItem({QC.KeyItem.PairOfVelkkGloves});Player.AddKeyItem({QC.KeyItem.Climbing});" +
              $"Text.Special({QC.Zone.MarjamiRavine}:YOU_HAVE_LEARNED,{QC.KeyItem.Climbing});Quest.Complete()",
              string.Join(" | ", new (QuestScript Q, int Csid)[] { (new QC.Adoulin.BreakingTheIce(), 26),
                  (new QC.Adoulin.ImOnABoat(), 2563), (new QC.Adoulin.AStonesThrowAway(), 2573),
                  (new QC.Adoulin.HideAndGoPeak(), 7) }
                  .SelectMany(x => new[] { Fin(x.Q, x.Csid, true), Fin(x.Q, x.Csid, false) })));

        string Nineteen(string npcName, int option, int prog)
        {
            var fin = new QC.Adoulin.ImOnABoat().Compile().States.SelectMany(s => s.Handlers)
                .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 19);
            var f = AD3Fake(QuestStatus.Accepted, prog);
            f.Option = option;
            f.Npc = new QuestNpcFake(17850930) { Name = npcName };
            fin.Run(f);
            return $"{npcName}/{option}/{prog}:{string.Join(";", f.Calls)}";
        }
        Check("adoulin g3:finish 19 at each name ending",
              "Castoff_Point_0/1/0: | Castoff_Point/1/0: | Castoff_Point_4/1/1:Quest.SetStep(1);" +
              $"Text.Special({QC.Zone.ForetDeHennetiel}:STARTED_TO_LEARN_BOAT) | " +
              $"Castoff_Point_5/1/0:Quest.SetStep(2);Text.MessageName({QC.Zone.ForetDeHennetiel}:FIGURED_OUT_BOAT) | " +
              $"Castoff_Point_0/3/0:Player.AddTitle({QC.Title.ToxinTussler})",
              string.Join(" | ", new[] { Nineteen("Castoff_Point_0", 1, 0), Nineteen("Castoff_Point", 1, 0),
                  Nineteen("Castoff_Point_4", 1, 1), Nineteen("Castoff_Point_5", 1, 0),
                  Nineteen("Castoff_Point_0", 3, 0) }));
    }
}
