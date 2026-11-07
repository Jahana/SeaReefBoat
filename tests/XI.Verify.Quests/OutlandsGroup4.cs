// XI.Verify.Quests -- quest rework B3b Outlands group 4 (Like Shining Leggings, Like a Shining Subligar, Secret of the Damp
// Scroll, The Sahagin's Stash, Divine Might, Divine Might (Repeat)): what the golden traces cannot show.
// - The reply kinds of NPCs that only these quests answer and that have no onTrigger / onTrade of their own (Heizo,
//   Heiji, Shivivi, Laisrean, Sea Serpent Grotto's qm2, the Hot Springs' trade, the Qu'Hau Spring's trades, and
//   blank_divine_might wherever Ark Angels' 53 does not answer beside them): with no rival and no script turn, a Progress
//   and an Event reply send the same packets.
// - Secret of the Damp Scroll's offer reads player:hasItem (every container): the harness puts items in the inventory
//   only, so the scroll in the Mog Safe is pinned here.
// - Divine Might's own answer at ARK_ANGELS status 0 (nil without a shard, 56 with one): live, Ark Angels' 53 (a progress
//   cutscene) outranks it on every talk, so no trace shows it.
// - The era module's condition, xi.pre(ROV): a module flow needs it true (a module not enabled fails the run), so only
//   its true side is traced.
// The handler lookup and the reply's description are the shared OLHandler / OLReply (OutlandsShared.cs).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup4()
    {
        Console.WriteLine("== quest rework B3b Outlands group 4: lone NPCs' reply kinds, the Damp Scroll anywhere ==");
        OL4Norg();
        OL4DampScroll();
        OL4Sahagin();
        OL4Blank();
        OL4Spring();
        OL4Module();
    }

    static Fake OL4At(int step, QuestStatus status = QuestStatus.Accepted)
    {
        var f = new Fake { Status = status };
        f.Vars["Prog"] = step;
        return f;
    }

    static Fake OL4Trade(int step, int item, int count)
    {
        var f = OL4At(step);
        f.TradeItems[item] = count;
        return f;
    }

    // Heizo and Heiji: the offer, the count, and the trade's three answers (done, taken, refused)
    static void OL4Norg()
    {
        var lk = new QC.Outlands.LikeShiningLeggings().Compile();
        var heizo = OLHandler(lk, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Heizo);
        var heizoTrade = OLHandler(lk, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Norg.Heizo);
        Check("outlands g4:Heizo's reply kinds: 127 / 128 (Prog) / the trade's 129 / 128 (the total) / 128 (Prog)",
              "ProgressEvent(127)|Event(128,3)|ProgressEvent(129)|Event(128,4)|Event(128,3)",
              $"{OLReply(OLHandler(lk, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Heizo), new Fake())}|" +
              $"{OLReply(heizo, OL4At(3))}|{OLReply(heizoTrade, OL4Trade(9, QC.Item.RustyLeggings, 1))}|" +
              $"{OLReply(heizoTrade, OL4Trade(3, QC.Item.RustyLeggings, 1))}|{OLReply(heizoTrade, OL4Trade(3, QC.Item.BoneChip, 1))}");

        var lg = new QC.Outlands.LikeAShiningSubligar().Compile();
        var heiji = OLHandler(lg, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Heiji);
        var heijiTrade = OLHandler(lg, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Norg.Heiji);
        Check("outlands g4:Heiji's reply kinds: 123 / 124 (Prog) / the trade's 125 / 124 (the total) / 124 (Prog)",
              "ProgressEvent(123)|Event(124,3)|ProgressEvent(125)|Event(124,4)|Event(124,3)",
              $"{OLReply(OLHandler(lg, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Heiji), new Fake())}|" +
              $"{OLReply(heiji, OL4At(3))}|{OLReply(heijiTrade, OL4Trade(9, QC.Item.RustySubligar, 1))}|" +
              $"{OLReply(heijiTrade, OL4Trade(3, QC.Item.RustySubligar, 1))}|{OLReply(heijiTrade, OL4Trade(3, QC.Item.BoneChip, 1))}");
    }

    static void OL4DampScroll()
    {
        var ds = new QC.Outlands.SecretOfTheDampScroll().Compile();
        Check("outlands g4:Shivivi's reply kinds: 31 / 32; the Hot Springs' trade: 2",
              "ProgressEvent(31,1210)|Event(32)|ProgressEvent(2,1210)",
              $"{OLReply(OLHandler(ds, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Shivivi), new Fake())}|" +
              $"{OLReply(OLHandler(ds, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Shivivi), OL4At(0))}|" +
              OLReply(OLHandler(ds, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.HorlaisPeak.HotSprings), OL4Trade(0, QC.Item.DampScroll, 1)));

        Fake Offer(Action<Fake> scroll)
        {
            var f = new Fake { MainLevel = 10 };
            f.Fame[QC.FameArea.Norg] = 3;
            scroll(f);
            return f;
        }
        var inInventory = Offer(f => f.Items.Add(QC.Item.DampScroll));
        var inSafe = Offer(f => f.ItemsElsewhere[1] = new System.Collections.Generic.HashSet<int> { QC.Item.DampScroll });
        var none = Offer(f => { });
        Check("outlands g4:the Damp Scroll offer reads the scroll in any container (player:hasItem): inventory / Mog Safe / none",
              "True|True|False",
              $"{ds.States[0].Matches[0].When(inInventory)}|{ds.States[0].Matches[0].When(inSafe)}|{ds.States[0].Matches[0].When(none)}");
    }

    static void OL4Sahagin()
    {
        var ss = new QC.Outlands.TheSahaginsStash().Compile();
        var lais = OLHandler(ss, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Laisrean);
        var statue = OL4At(0);
        statue.KeyItems[QC.KeyItem.SeaSerpentStatue] = true;
        Check("outlands g4:Laisrean's reply kinds: 33 / 35 / 34; Sea Serpent Grotto's qm2: 1",
              "ProgressEvent(33,{[7]=0})|ProgressEvent(35,296)|Event(34)|ProgressEvent(1)",
              $"{OLReply(OLHandler(ss, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Norg.Laisrean), new Fake())}|" +
              $"{OLReply(lais, statue)}|{OLReply(lais, OL4At(0))}|" +
              OLReply(OLHandler(ss, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.SeaSerpentGrotto.qm2), OL4At(0)));
    }

    // blank_divine_might: Ark Angels' 53 answers only at ARK_ANGELS status 0, so every other reply stands alone
    static void OL4Blank()
    {
        var dm = new QC.Outlands.DivineMight().Compile();
        var dr = new QC.Outlands.DivineMightRepeat().Compile();
        Fake Zilart(int mission, int status, int shard = 0, QuestStatus quest = QuestStatus.Available, int step = 0)
        {
            var f = OL4At(step, quest);
            f.CurrentMissions[QC.MissionLog.Zilart] = mission;
            f.MissionStatuses[QC.MissionLog.Zilart] = status;
            if (shard != 0) f.KeyItems[shard] = true;
            return f;
        }
        var dmAvail = OLHandler(dm, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.TheShrineOfRuavitau.blankdivinemight);
        var dmAcc = OLHandler(dm, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.TheShrineOfRuavitau.blankdivinemight);
        Check("outlands g4:Divine Might's blank reply kinds: 54 (status 1) / 56 (status 2, a shard) / 55 (Prog 1)",
              "Event(54,917,1408,1550)|Event(56,917,1408,1550)|ProgressEvent(55,14739,14740,14741,14742,14743)",
              $"{OLReply(dmAvail, Zilart(QC.Mission.Zilart.ArkAngels, 1))}|" +
              $"{OLReply(dmAvail, Zilart(QC.Mission.Zilart.ArkAngels, 2, QC.KeyItem.ShardOfEnvy))}|" +
              OLReply(dmAcc, Zilart(QC.Mission.Zilart.TheSealedShrine, 0, 0, QuestStatus.Accepted, 1)));

        Check("outlands g4:Divine Might's own answer at ARK_ANGELS status 0 (Ark Angels' 53 outranks it live): no shard / a shard",
              "null|Event(56,917,1408,1550)",
              $"{OLReply(dmAvail, Zilart(QC.Mission.Zilart.ArkAngels, 0))}|" +
              OLReply(dmAvail, Zilart(QC.Mission.Zilart.ArkAngels, 0, QC.KeyItem.ShardOfRage)));

        var drOffer = OLHandler(dr, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.TheShrineOfRuavitau.blankdivinemight);
        var drAcc = OLHandler(dr, 1, QuestHandlerKind.OnTrigger, entity: QC.Npc.TheShrineOfRuavitau.blankdivinemight);
        var offer = new Fake { Status = QuestStatus.Completed };
        offer.MainNumbers["NUMBER_OF_DM_EARRINGS"] = 1;
        offer.CharVars["DM_Earring"] = QC.Item.Suppanomimi;
        Fake Ore(int step)
        {
            var f = OL4At(step);
            f.KeyItems[QC.KeyItem.MoonlightOre] = true;
            return f;
        }
        Check("outlands g4:Divine Might (Repeat)'s blank reply kinds: 57 / 58 / 56 / 59",
              "ProgressEvent(57,14739)|Event(58)|Event(56,917,1408,1550)|ProgressEvent(59)",
              $"{OLReply(drOffer, offer)}|{OLReply(drAcc, OL4At(0))}|{OLReply(drAcc, Ore(0))}|{OLReply(drAcc, Ore(1))}");
    }

    // the Qu'Hau Spring's trades: Windurst 8-1 / 9-2 answer its trigger only
    static void OL4Spring()
    {
        var dm = new QC.Outlands.DivineMight().Compile();
        var dr = new QC.Outlands.DivineMightRepeat().Compile();
        Fake Night(params int[] items)
        {
            var f = OL4At(0);
            f.MoonCycle = 6; // xi.moonCycle.FULL_MOON
            f.Hour = 0;
            foreach (var i in items) f.TradeItems[i] = 1;
            return f;
        }
        Check("outlands g4:the Qu'Hau Spring's trade reply kinds: Divine Might's 7; the repeat's 7 / 8",
              "ProgressEvent(7,917,1408)|ProgressEvent(7,917,1408)|ProgressEvent(8)",
              $"{OLReply(OLHandler(dm, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Romaeve.QuHauSpring), Night(QC.Item.SheetOfParchment, QC.Item.BottleOfIlluminink))}|" +
              $"{OLReply(OLHandler(dr, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Romaeve.QuHauSpring), Night(QC.Item.SheetOfParchment, QC.Item.BottleOfIlluminink))}|" +
              OLReply(OLHandler(dr, 1, QuestHandlerKind.OnTrade, entity: QC.Npc.Romaeve.QuHauSpring), Night(QC.Item.ChunkOfLightOre)));
    }

    // xi.pre(xi.expansion.ROV): content restricted and RoV off; either setting otherwise: off
    static void OL4Module()
    {
        var module = new QC.Modules.Era.Outlands.DivineMight();
        Fake With(int restrict, int rov)
        {
            var f = new Fake();
            f.MainSettings["RESTRICT_CONTENT"] = restrict;
            f.MainSettings["ENABLE_ROV"] = rov;
            return f;
        }
        Check("outlands g4:the Divine Might era module's condition xi.pre(ROV): restricted, RoV off / RoV on / unrestricted",
              "True|False|False",
              $"{module.Condition(With(1, 0))}|{module.Condition(With(1, 1))}|{module.Condition(With(0, 0))}");
    }
}
