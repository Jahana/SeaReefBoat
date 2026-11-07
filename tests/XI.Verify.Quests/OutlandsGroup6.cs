// XI.Verify.Quests -- quest rework B3b Outlands group 6 (An Undying Pledge, Black Market, Everyone's Grudge, It's Not Your
// Vault, Mama Mia; call-only): what the golden traces cannot show.
// - The reply kinds. The five quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an Event
//   and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no lookup action,
//   so no priority to tell them apart, and every trace replays the same with either kind. The Lua scripts call
//   player:startEvent, so each reply is pinned here as Event (qm3's npcUtil.giveKeyItem as NoAction).
// - Black Market's sale finishes (17 / 18 / 19) complete the quest only while it is accepted. Once completed, Lua's
//   accepted test keeps completeQuest out; without it, completeQuest on a completed quest changes nothing a trace shows (no
//   row, no bit, no packet: mutant b2 replayed clean), so the guard is pinned here.
// - Sea Serpent Grotto's qm3 gives the Sealed Iron Box only while it is not held. npcUtil.giveKeyItem skips a held key
//   item without a message, so the Lua's own no-box test changes nothing a trace shows (mutant v8 replayed clean): pinned.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup6()
    {
        Console.WriteLine("== quest rework B3b Outlands group 6: the call-mode reply kinds ==");
        OL6Pledge();
        OL6BlackMarket();
        OL6Grudge();
        OL6Vault();
        OL6MamaMia();
        OL6Sales();
        OL6Qm3();
    }

    static Fake OL6Fake(QuestStatus status, int norgFame = 0)
    {
        var f = new Fake { Status = status };
        f.Fame[QC.FameArea.Norg] = norgFame;
        return f;
    }

    static void OL6Pledge()
    {
        var def = new QC.Outlands.AnUndyingPledge().Compile();
        string stray = QC.Npc.Norg.StrayCloud;
        var blade = OL6Fake(QuestStatus.Accepted);
        blade.KeyItems[QC.KeyItem.CaliginousBlade] = true;
        var cs1 = OL6Fake(QuestStatus.Accepted);
        cs1.CharVars["anUndyingPledgeCS"] = 1;
        var cs2 = OL6Fake(QuestStatus.Accepted);
        cs2.CharVars["anUndyingPledgeCS"] = 2;
        Check("outlands g6:An Undying Pledge's replies are events (call mode): Stray Cloud's 225 / 227 / 228 / 229 / 230",
              "Event(225)|Event(227)|Event(228)|Event(229)|Event(230)",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: stray), OL6Fake(QuestStatus.Available, 4))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: stray), blade)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: stray), cs1)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: stray), cs2)}|" +
              OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: stray), OL6Fake(QuestStatus.Completed)));
    }

    static void OL6BlackMarket()
    {
        var def = new QC.Outlands.BlackMarket().Compile();
        string muz = QC.Npc.Norg.Muzaffar;
        Fake Four(int item)
        {
            var f = OL6Fake(QuestStatus.Accepted);
            f.TradeItems[item] = 4;
            return f;
        }
        Check("outlands g6:Black Market's replies are events (call mode): Muzaffar's 15 / 16 / 17 / 18 / 19",
              $"Event(15)|Event(16)|Event(17,{QC.Item.NorthernFur},{QC.Item.NorthernFur})|" +
              $"Event(18,{QC.Item.PieceOfEasternPottery},{QC.Item.PieceOfEasternPottery})|" +
              $"Event(19,{QC.Item.SouthernMummy},{QC.Item.SouthernMummy})",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: muz), OL6Fake(QuestStatus.Available))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: muz), OL6Fake(QuestStatus.Accepted))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: muz), Four(QC.Item.NorthernFur))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: muz), Four(QC.Item.PieceOfEasternPottery))}|" +
              OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: muz), Four(QC.Item.SouthernMummy)));
    }

    static void OL6Grudge()
    {
        var def = new QC.Outlands.EveryonesGrudge().Compile();
        string mag = QC.Npc.Norg.Magephaud;
        var offer = OL6Fake(QuestStatus.Available, 2);
        offer.CharVars["EVERYONES_GRUDGE_KILLS"] = 1;
        var started = OL6Fake(QuestStatus.Accepted);
        started.CharVars["EveryonesGrudgeStarted"] = 1;
        var coins = OL6Fake(QuestStatus.Accepted);
        coins.TradeItems[QC.Item.GoldBeastcoin] = 3;
        Check("outlands g6:Everyone's Grudge's replies are events (call mode): Magephaud's 116 / 117 / 119 / 118",
              $"Event(116,{QC.Item.GoldBeastcoin})|Event(117,{QC.Item.GoldBeastcoin})|Event(119)|Event(118,{QC.Item.GoldBeastcoin})",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), offer)}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), started)}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), OL6Fake(QuestStatus.Completed))}|" +
              OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: mag), coins));
    }

    static void OL6Vault()
    {
        var def = new QC.Outlands.ItsNotYourVault().Compile();
        string keal = QC.Npc.Norg.Keal;
        var offer = OL6Fake(QuestStatus.Available, 3);
        offer.MainLevel = 5;
        var box = OL6Fake(QuestStatus.Accepted);
        box.KeyItems[QC.KeyItem.SealedIronBox] = true;
        Check("outlands g6:It's Not Your Vault's replies are events (call mode): Keal's 36 / 38 / 37 / 39; qm3's key item, no event",
              $"Event(36,{QC.KeyItem.SealedIronBox})|Event(38)|Event(37,{QC.KeyItem.MapOfSeaSerpentGrotto})|Event(39)|NoAction()",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: keal), offer)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: keal), box)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: keal), OL6Fake(QuestStatus.Accepted))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: keal), OL6Fake(QuestStatus.Completed))}|" +
              OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: QC.Npc.SeaSerpentGrotto.qm3), OL6Fake(QuestStatus.Accepted)));
    }

    static void OL6MamaMia()
    {
        var def = new QC.Outlands.MamaMia().Compile();
        string mam = QC.Npc.Norg.Mamaulabion;
        var offer = OL6Fake(QuestStatus.Available, 4);
        offer.QuestStatuses["Windurst/THE_MOONLIT_PATH"] = QuestStatus.Completed;
        Fake Full(int now)
        {
            var f = OL6Fake(QuestStatus.Accepted);
            f.CharVars["tradesMamaMia"] = 0x7F;
            f.CharVars["MamaMia_date"] = 1800000000;
            f.Now = now;
            return f;
        }
        Fake Given(int mask)
        {
            var f = OL6Fake(QuestStatus.Accepted);
            f.CharVars["tradesMamaMia"] = mask;
            f.TradeItems[QC.Item.AncientsKey] = 1;
            return f;
        }
        var ring = OL6Fake(QuestStatus.Completed);
        ring.Items.Add(QC.Item.EvokersRing);
        Check("outlands g6:Mama Mia's replies are events (call mode): Mamaulabion's 191 / 192 / 196 / 197 / 198 / 243; " +
              "her trade's 193 / 194 / 195",
              $"Event(191)|Event(192)|Event(196)|Event(197)|Event(198)|Event(243)|Event(193,{QC.Item.AncientsKey})|" +
              $"Event(194,{QC.Item.AncientsKey})|Event(195,{QC.Item.AncientsKey})",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mam), offer)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: mam), OL6Fake(QuestStatus.Accepted))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: mam), Full(1799999999))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: mam), Full(1800000000))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: mam), ring)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: mam), OL6Fake(QuestStatus.Completed))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: mam), Given(0))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: mam), Given(0x40))}|" +
              OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: mam), Given(0x3F)));
    }

    static void OL6Sales()
    {
        var def = new QC.Outlands.BlackMarket().Compile();
        bool Completes(int csid, QuestStatus status)
        {
            var h = OLHandler(def, 2, QuestHandlerKind.OnEventFinish, id: csid); // state 2: the AnyState finishes
            var f = OL6Fake(status);
            f.Csid = csid;
            h.Run(f);
            return f.Calls.Contains("Player.CompleteQuest(Outlands,BLACK_MARKET)");
        }
        Check("outlands g6:Black Market's 17 / 18 / 19 finishes complete the quest while accepted, not once completed",
              "True,False|True,False|True,False",
              $"{Completes(17, QuestStatus.Accepted)},{Completes(17, QuestStatus.Completed)}|" +
              $"{Completes(18, QuestStatus.Accepted)},{Completes(18, QuestStatus.Completed)}|" +
              $"{Completes(19, QuestStatus.Accepted)},{Completes(19, QuestStatus.Completed)}");
    }

    static void OL6Qm3()
    {
        const int Qm3State = 3; // ItsNotYourVault.cs: the fourth state, accepted with no box (qm3's section)
        var def = new QC.Outlands.ItsNotYourVault().Compile();
        var qm3 = def.States[Qm3State];
        var held = OL6Fake(QuestStatus.Accepted);
        held.KeyItems[QC.KeyItem.SealedIronBox] = true;
        Check("outlands g6:qm3's section matches while accepted with no box, not with the box held",
              "True|False",
              $"{qm3.IsMatch(QuestStatus.Accepted, 0, OL6Fake(QuestStatus.Accepted))}|" +
              $"{qm3.IsMatch(QuestStatus.Accepted, 0, held)}");
    }
}
