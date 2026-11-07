// XI.Verify.Quests -- quest rework B3b Other Areas group 7 (Tavnazian Safehold: A Hard Day's Knight, Go! Go! Gobmuffin!,
// Paradise, Salvation, and Maps, Unforgiven, Uninvited Guests): what no trace can tell apart or reach.
// - Kinds: where no other quest and no NPC script answers the NPC (Quelveuiat's script has no onTrigger; Epinolle,
//   Elysia, Pradiulot and the ???s have none, and Justinius' CoP replaceDefaults rank below either kind), a Progress
//   and an Event reply play alike, so no trace tells them apart: `oa g7 kinds` checks each reply as the Lua writes it.
//   Two NPCs do show the kind in a trace, and the checks stay as a second guard: Justinius at The Secrets of Worship
//   Status 0, where CoP 4-3's 131 (an importantEvent: Progress once, then Event) takes turns with every Event reply
//   (uninvited_guests#worship), and Nivorajean, whose script shows his Event replies (224 / 226 hand his next talk to
//   it) and whose UseFallback var, left by 228's kind, picks 224 or his script on the next talk
//   (paradise_salvation_and_maps#edges).
// - Complete: Unforgiven's 204 and Uninvited Guests' 572 read quest:complete, whose reward holds no item, so it cannot
//   fail on Lua: `oa g7 complete` takes the false arm on the fake.
// - Gobmuffin: nmStatus' nil bit (a mob of the three names whose id is not one of the three: the Lua's utils.mask.setBit
//   errors, the C# returns; Riverne - Site #B01 places one mob of each name, so no trace reaches it) and each bit.
// - Draws: Uninvited Guests' generateReward rolls math.randomInt(1, 1000) over the weights; a trace's seeded rolls
//   cannot pick the range's ends (1000, the memoirs' top; 1, the breastplate), so `oa g7 draws` checks the call and
//   the item at each boundary.
// - Distance: Player.DistanceTo (player:checkDistance(x, y, z), this group's facet member) and the chest's `< 2` (a trace
//   stands 1 yalm from spot 5 and 4 yalms off; the fake stands exactly 2 off).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup7()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 7: the reply kinds no trace records, the unreachable arms ==");
        RunOtherAreasGroup7Kinds();
        RunOtherAreasGroup7Complete();
        RunOtherAreasGroup7Gobmuffin();
        RunOtherAreasGroup7Distance();
        RunOtherAreasGroup7Draws();
    }

    static Fake OA7At(QuestStatus status, int step = 0, params int[] keyItems)
    {
        var f = new Fake { Status = status, Step = step };
        foreach (int k in keyItems) f.KeyItems[k] = true;
        return f;
    }

    static string OA7Run(QuestHandlerDecl h, Fake f)
    {
        h.Run(f);
        return string.Join(";", f.Calls);
    }

    static void RunOtherAreasGroup7Kinds()
    {
        var hdk = new QC.OtherAreas.AHardDaysKnight().Compile();
        var ggg = new QC.OtherAreas.GoGoGobmuffin().Compile();
        var psm = new QC.OtherAreas.ParadiseSalvationAndMaps().Compile();
        var unf = new QC.OtherAreas.Unforgiven().Compile();
        var ug = new QC.OtherAreas.UninvitedGuests().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var acc = QuestStatus.Accepted;
        var done = QuestStatus.Completed;
        int sealion = QC.Item.SealionCrestKey, coral = QC.Item.CoralCrestKey;

        Fake Traded(params int[] items)
        {
            var f = OA7At(done);
            foreach (int i in items)
            {
                f.TradeItems[i] = f.TradeItems.TryGetValue(i, out var n) ? n + 1 : 1;
                f.TradeSlots.Add(i);
            }
            return f;
        }
        Fake Holding(params int[] items)
        {
            var f = OA7At(done);
            foreach (int i in items) f.Items.Add(i);
            return f;
        }
        string hdkKinds = string.Join("|",
            G2Reply(G2Handler(hdk, 0, trig, QC.Npc.TavnazianSafehold.Quelveuiat), OA7At(QuestStatus.Available)),
            G2Reply(G2Handler(hdk, 1, trig, QC.Npc.TavnazianSafehold.Quelveuiat), OA7At(acc, 0)),
            G2Reply(G2Handler(hdk, 1, trig, QC.Npc.TavnazianSafehold.Quelveuiat), OA7At(acc, 1)),
            G2Reply(G2Handler(hdk, 2, trade, QC.Npc.TavnazianSafehold.Quelveuiat), Traded(sealion)),
            G2Reply(G2Handler(hdk, 2, trade, QC.Npc.TavnazianSafehold.Quelveuiat), Traded(sealion, coral)),
            G2Reply(G2Handler(hdk, 2, trig, QC.Npc.TavnazianSafehold.Quelveuiat), Holding(coral)));
        Check("oa g7 kinds:A Hard Day's Knight's Quelveuiat 119 / 121 Progress, 120 Event; the completed trade's 631 (the key traded; both keys) and trigger's 631 (the keys held) Progress",
              $"ProgressEvent(119)|Event(120)|ProgressEvent(121)|ProgressEvent(631,{sealion})|ProgressEvent(631,{sealion},{coral})|ProgressEvent(631,{coral},0)",
              hdkKinds);

        string gggKinds = string.Join("|",
            G2Reply(G2Handler(ggg, 0, trig, QC.Npc.TavnazianSafehold.Epinolle), OA7At(QuestStatus.Available)),
            G2Reply(G2Handler(ggg, 1, trig, QC.Npc.TavnazianSafehold.Epinolle), OA7At(acc, 0)),
            G2Reply(G2Handler(ggg, 1, trig, QC.Npc.TavnazianSafehold.Epinolle), OA7At(acc, 1)),
            G2Reply(G2Handler(ggg, 2, trig, QC.Npc.TavnazianSafehold.Epinolle), OA7At(done)));
        Check("oa g7 kinds:Go! Go! Gobmuffin!'s Epinolle 232 / 234 Progress, 233 Event, 235 Event replaceDefault",
              "ProgressEvent(232)|Event(233)|ProgressEvent(234)|Event(235):replaceDefault()",
              gggKinds);

        var day8001 = OA7At(acc, 2);
        day8001.UniqueDay = 8001;
        day8001.Vars["Wait"] = 8000;
        string psmKinds = string.Join("|",
            G2Reply(G2Handler(psm, 1, trig, QC.Npc.TavnazianSafehold.Nivorajean), day8001));
        Check("oa g7 kinds:Paradise, Salvation, and Maps' Nivorajean 228 Progress (a second guard: paradise_salvation_and_maps#edges' next talk, 224, shows it)",
              "ProgressEvent(228)",
              psmKinds);

        int hairpin = QC.KeyItem.AlabasterHairpin;
        Fake Post(int post)
        {
            var f = OA7At(done);
            f.Vars["Post"] = post;
            return f;
        }
        string unfKinds = string.Join("|",
            G2Reply(G2Handler(unf, 0, trig, QC.Npc.TavnazianSafehold.Elysia), OA7At(QuestStatus.Available)),
            G2Reply(G2Handler(unf, 1, trig, QC.Npc.TavnazianSafehold.Elysia), OA7At(acc, 0, hairpin)),
            G2Reply(G2Handler(unf, 1, trig, QC.Npc.TavnazianSafehold.Elysia), OA7At(acc, 1)),
            G2Reply(G2Handler(unf, 1, trig, QC.Npc.TavnazianSafehold.Elysia), OA7At(acc, 0)),
            G2Reply(G2Handler(unf, 1, trig, QC.Npc.TavnazianSafehold.qmunforgiven), OA7At(acc, 0)),
            G2Reply(G2Handler(unf, 1, trig, QC.Npc.TavnazianSafehold.Pradiulot), OA7At(acc, 1)),
            G2Reply(G2Handler(unf, 2, trig, QC.Npc.TavnazianSafehold.Elysia), Post(1)),
            G2Reply(G2Handler(unf, 2, trig, QC.Npc.TavnazianSafehold.Pradiulot), Post(2)));
        Check("oa g7 kinds:Unforgiven's Elysia 200 / 202 / 205 Progress, 203 / 201 Event; the ??? a keyItem (the hairpin); Pradiulot 204 / 206 Progress",
              $"ProgressEvent(200)|ProgressEvent(202)|Event(203)|Event(201)|KeyItem({hairpin})|ProgressEvent(204)|ProgressEvent(205)|ProgressEvent(206)",
              unfKinds);

        Fake Justinius(QuestStatus status, int step, int wait = 0, bool permit = false)
        {
            var f = permit ? OA7At(status, step, QC.KeyItem.MonarchLinnPatrolPermit) : OA7At(status, step);
            f.Vars["Wait"] = wait;
            f.Tally = 1800198000;
            return f;
        }
        string ugKinds = string.Join("|",
            G2Reply(G2Handler(ug, 0, trig, QC.Npc.TavnazianSafehold.Justinius), OA7At(QuestStatus.Available)),
            G2Reply(G2Handler(ug, 1, trig, QC.Npc.TavnazianSafehold.Justinius), Justinius(acc, 1, permit: true)),
            G2Reply(G2Handler(ug, 1, trig, QC.Npc.TavnazianSafehold.Justinius), Justinius(acc, 2)),
            G2Reply(G2Handler(ug, 1, trig, QC.Npc.TavnazianSafehold.Justinius), Justinius(done, 0)),
            G2Reply(G2Handler(ug, 1, trig, QC.Npc.TavnazianSafehold.Justinius), Justinius(acc, 4)),
            G2Reply(G2Handler(ug, 1, trig, QC.Npc.TavnazianSafehold.Justinius), Justinius(acc, 3)));
        Check("oa g7 kinds:Uninvited Guests' Justinius 570 / 572 / 573 / 574 / 575 Progress, 571 Event",
              "ProgressEvent(570)|Event(571)|ProgressEvent(572)|ProgressEvent(573)|ProgressEvent(574)|ProgressEvent(575)",
              ugKinds);
    }

    // quest:complete false (the fake's CompleteResult): Unforgiven's 204 sets no Post; Uninvited Guests' giveReward sets
    // no Wait and keeps the char var (both rewards hold no item: on Lua the completion cannot fail)
    static void RunOtherAreasGroup7Complete()
    {
        var fin = QuestHandlerKind.OnEventFinish;
        var unf = new QC.OtherAreas.Unforgiven().Compile();
        var ug = new QC.OtherAreas.UninvitedGuests().Compile();

        var f204 = new Fake { Status = QuestStatus.Accepted, Step = 1, CompleteResult = false };
        var f572 = new Fake { Status = QuestStatus.Completed, Step = 2, CompleteResult = false };
        f572.CharVars["UninvitedGuestsReward"] = QC.Item.TavnazianSalad;
        f572.CompletedQuests.Add($"{QuestLog.OtherAreas}/UNINVITED_GUESTS");
        Check("oa g7 complete:quest:complete false: Unforgiven's 204 sets no Post; Uninvited Guests' 572 gives the pending item, no gil (completed before), then no Wait and the char var kept",
              $"Quest.Complete()|Player.GiveItem({QC.Item.TavnazianSalad});Quest.Complete()|{QC.Item.TavnazianSalad}",
              $"{OA7Run(G2Handler(unf, 1, fin, null, 204), f204)}|" +
              $"{OA7Run(G2Handler(ug, 1, fin, null, 572), f572)}|{f572.CharVar("UninvitedGuestsReward")}");
    }

    // nmStatus: the dead NM's bit (its id less SPELL_SPITTER_SPILUSPOK's) in the char's local GobmuffinNM; all three bits:
    // Prog 1 and the local var not written; an id outside the three (nil in the Lua's nmBitTable): nothing
    static void RunOtherAreasGroup7Gobmuffin()
    {
        var ggg = new QC.OtherAreas.GoGoGobmuffin().Compile();
        var death = QuestHandlerKind.OnMobDeath;
        const int first = 16896152;

        string Death(string mob, int id, int mask)
        {
            var f = OA7At(QuestStatus.Accepted, 0);
            f.MobIds[QC.Mob.RiverneSiteB01.SPELL_SPITTER_SPILUSPOK] = first;
            f.Mob = new QuestNpcFake(id);
            f.PlayerLocalVars["GobmuffinNM"] = mask;
            return OA7Run(G2Handler(ggg, 1, death, mob), f) + "/" + f.Step;
        }
        Check("oa g7 gobmuffin:nmStatus: +0 into 0: 1; +1 into 1: 3; +2 into 3: Prog 1 (no local var written); +2 into 0: 4; an id outside the three (nil bit): nothing",
              "Player.SetLocalVar(GobmuffinNM,1)/0|Player.SetLocalVar(GobmuffinNM,3)/0|Quest.SetStep(1)/1|Player.SetLocalVar(GobmuffinNM,4)/0|/0",
              string.Join("|",
                  Death(QC.Npc.RiverneSiteB01.SpellSpitterSpilospok, first, 0),
                  Death(QC.Npc.RiverneSiteB01.ChemicalCookChemachiq, first + 1, 1),
                  Death(QC.Npc.RiverneSiteB01.BookBrowserBokabraq, first + 2, 3),
                  Death(QC.Npc.RiverneSiteB01.BookBrowserBokabraq, first + 2, 0),
                  Death(QC.Npc.RiverneSiteB01.SpellSpitterSpilospok, first + 3, 0)));
    }

    // Player.DistanceTo (player:checkDistance(x, y, z): x, y and z all count) and the chest trade's spot test (`< 2`)
    static void RunOtherAreasGroup7Distance()
    {
        var psm = new QC.OtherAreas.ParadiseSalvationAndMaps().Compile();
        var trade = QuestHandlerKind.OnTrade;

        var d = new Fake { XPos = 3, YPos = 4, ZPos = 12 };
        string distance = $"{d.Player.DistanceTo(0, 0, 0)}";

        string Chest(double x, double y, double z)
        {
            var f = OA7At(QuestStatus.Accepted, 0);
            f.XPos = x;
            f.YPos = y;
            f.ZPos = z;
            f.TradeItems[QC.Item.SacrariumChestKey] = 1;
            var r = G2Reply(G2Handler(psm, 1, trade, QC.Npc.Sacrarium.TreasureChest), f);
            return $"{string.Join(";", f.Calls)}:{r}:{f.Var("Option")}";
        }
        // spot 1 (F-5): 31.021 -2.000 99.013, stood 1.999 and 2 yalms above it; spot 5 (J-10): 179.709 -7.693 -97.007
        Check("oa g7 distance:DistanceTo is the 3D distance (13 from 3,4,12); the chest trade 1.999 yalms from spot 1: Option 1; 2 yalms from it: Option 0; at spot 5: Option 5; each a treasure trade and noAction",
              "13|Trade.TreasureTrade(2,605);Quest.SetVar(Option,1,0):NoAction():1|Trade.TreasureTrade(2,605):NoAction():0|Trade.TreasureTrade(2,605);Quest.SetVar(Option,5,0):NoAction():5",
              string.Join("|", distance,
                  Chest(31.021, -0.001, 99.013),
                  Chest(31.021, 0, 99.013),
                  Chest(179.709, -7.693, -97.007)));
    }

    // generateReward: math.randomInt(1, 1000) (the weights' total), then the table walked from the top: 1000 and 460 the
    // memoirs (541), 459 the first common (25), 2 the last rare (2), 1 the breastplate (1). quest:complete false keeps
    // the rolled item in the char var.
    static void RunOtherAreasGroup7Draws()
    {
        var ug = new QC.OtherAreas.UninvitedGuests().Compile();
        string Draw(int roll)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 2, CompleteResult = false };
            f.Rolls.Enqueue(roll);
            OA7Run(G2Handler(ug, 1, QuestHandlerKind.OnEventFinish, null, 572), f);
            return $"{f.Calls.FirstOrDefault(c => c.StartsWith("Player.RandomInt"))}>{f.CharVar("UninvitedGuestsReward")}";
        }
        string R(int item) => $"Player.RandomInt(1,1000)>{item}";
        Check("oa g7 draws:Uninvited Guests' 572 rolls randomInt(1, 1000): 1000 and 460 the memoirs, 459 the bream risotto, 2 the witch stew, 1 the breastplate",
              string.Join("|", R(QC.Item.MiratetesMemoirs), R(QC.Item.MiratetesMemoirs), R(QC.Item.PlateOfBreamRisotto),
                  R(QC.Item.BowlOfWitchStew), R(QC.Item.AssaultBreastplate)),
              string.Join("|", Draw(1000), Draw(460), Draw(459), Draw(2), Draw(1)));
    }
}
