// XI.Verify.Quests -- quest rework B3b Windurst group 5 (Star Struck, Blast from the Past, The Puppet Master, I Can Hear
// a Rainbow): the facet members the group added (HasItem(item, location), NpcIdOf, ZoneWeather), on the fake; and what
// no trace can see: the trade replies' kind (Star Struck's 199 and 211, Blast from the Past's 224, I Can Hear a
// Rainbow's 124: no other quest or script answers those trades with those items, so an Event and a Progress reply send
// the same packets there), Luuh Koplehn's 200 (an Event: nothing else answers Luuh Koplehn), and I Can Hear a Rainbow's
// reads that no GM tool can split: the ruby in the inventory only (player:hasItem(ruby, INVENTORY): a ruby in another
// container does not count) and the zone's weather, not the char's (getZone(true):getWeather(): a scholar's storm
// changes the char's).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup5()
    {
        Console.WriteLine("== quest rework B3b Windurst group 5: facets, trade replies, the Rainbow's ruby and weather ==");
        RunWindurstGroup5Facets();
        RunWindurstGroup5Replies();
        RunWindurstGroup5Rainbow();
    }

    // HasItem(item, location), NpcIdOf, ZoneWeather
    static void RunWindurstGroup5Facets()
    {
        var f = new Fake { CurrentWeather = 20, CurrentZoneWeather = 6 };
        f.Items.Add(QC.Item.CarbunclesRuby);
        f.ItemsElsewhere[1] = new System.Collections.Generic.HashSet<int> { QC.Item.Meteorite };
        f.NpcIds["FOSSIL_ROCK_OFFSET"] = 17588768;
        string unknown;
        try { f.Player.NpcIdOf("NO_SUCH_NPC"); unknown = "no throw"; }
        catch (InvalidOperationException) { unknown = "throws"; }
        Check("windurst g5 facets:the fake answers HasItem(item, location) (Items at 0, ItemsElsewhere by container), NpcIdOf (an unknown key throws) and ZoneWeather (not Weather); HasItem(item) any container",
              "True/False/True/False/17588768/throws/6/20/True/True",
              $"{f.Player.HasItem(QC.Item.CarbunclesRuby, 0)}/{f.Player.HasItem(QC.Item.CarbunclesRuby, 1)}/" +
              $"{f.Player.HasItem(QC.Item.Meteorite, 1)}/{f.Player.HasItem(QC.Item.Meteorite, 0)}/" +
              $"{f.Player.NpcIdOf("FOSSIL_ROCK_OFFSET")}/{unknown}/{f.Player.ZoneWeather}/{f.Player.Weather}/" +
              $"{f.Player.HasItem(QC.Item.CarbunclesRuby)}/{f.Player.HasItem(QC.Item.Meteorite)}");
    }

    // a state's trade reply for the items in the trade
    static string W5Trade(QuestScriptDefinition def, int state, string npc, Fake f) =>
        G2Reply(G2Handler(def, state, QuestHandlerKind.OnTrade, npc), f);

    static Fake W5Trading(QuestStatus status, int step, int item)
    {
        var f = new Fake { Status = status, Step = step };
        f.TradeItems[item] = 1;
        return f;
    }

    // Star Struck's 199 (the epistle alone) and 211 (a meteorite alone), Blast from the Past's 224 (the stone alone), I
    // Can Hear a Rainbow's 124 (the ruby alone at Prog 1): each a Progress reply; Luuh Koplehn's 200: an Event
    static void RunWindurstGroup5Replies()
    {
        var ss = new QC.Windurst.StarStruck().Compile();
        var bp = new QC.Windurst.BlastFromThePast().Compile();
        var rb = new QC.Windurst.ICanHearARainbow().Compile();
        Check("windurst g5 replies:199, 211, 224 and 124 are Progress replies",
              "ProgressEvent(199)|ProgressEvent(211)|ProgressEvent(224)|ProgressEvent(124)",
              $"{W5Trade(ss, 1, QC.Npc.WindurstWalls.KoruMoru, W5Trading(QuestStatus.Accepted, 0, QC.Item.TornEpistle))}|" +
              $"{W5Trade(ss, 1, QC.Npc.WindurstWalls.KoruMoru, W5Trading(QuestStatus.Accepted, 0, QC.Item.Meteorite))}|" +
              $"{W5Trade(bp, 1, QC.Npc.WindurstWalls.KoruMoru, W5Trading(QuestStatus.Accepted, 0, QC.Item.BurniteShellStone))}|" +
              $"{W5Trade(rb, 2, QC.Npc.LaTheinePlateau.qm3, W5Trading(QuestStatus.Accepted, 1, QC.Item.CarbunclesRuby))}");
        Check("windurst g5 replies:Luuh Koplehn's 200 is an Event",
              "Event(200)",
              G2Reply(G2Handler(ss, 1, QuestHandlerKind.OnTrigger, QC.Npc.WindurstWalls.LuuhKoplehn), new Fake { Status = QuestStatus.Accepted }));
    }

    // the Rainbow's zone-in at Prog 0 in La Theine Plateau, Light 0
    static string W5ZoneIn(QuestScriptDefinition def, Fake f)
    {
        f.Zone = QC.Zone.LaTheinePlateau;
        string reply = G2Describe(def.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnZoneIn && h.Zone == f.Zone).Reply(f));
        return $"{reply}:{f.Player.LocalVar("Weather")}";
    }

    static void RunWindurstGroup5Rainbow()
    {
        var rb = new QC.Windurst.ICanHearARainbow().Compile();

        // the ruby in the inventory: the event (123); in the mog safe only (container 1), or in the wardrobe (8): nothing
        // (and no Weather stored); the offer's check the same
        var inInventory = new Fake { Status = QuestStatus.Accepted, CurrentZoneWeather = 4 };
        inInventory.Items.Add(QC.Item.CarbunclesRuby);
        var inSafe = new Fake { Status = QuestStatus.Accepted, CurrentZoneWeather = 4 };
        inSafe.ItemsElsewhere[1] = new System.Collections.Generic.HashSet<int> { QC.Item.CarbunclesRuby };
        var inWardrobe = new Fake { Status = QuestStatus.Accepted, CurrentZoneWeather = 4 };
        inWardrobe.ItemsElsewhere[8] = new System.Collections.Generic.HashSet<int> { QC.Item.CarbunclesRuby };
        Check("windurst g5 rainbow:the zone-in counts the ruby in the inventory only (player:hasItem(ruby, INVENTORY))",
              "Event(123):4|null:0|null:0",
              $"{W5ZoneIn(rb, inInventory)}|{W5ZoneIn(rb, inSafe)}|{W5ZoneIn(rb, inWardrobe)}");

        var offerInventory = new Fake { MainLevel = 30 };
        offerInventory.MainSettings["ADVANCED_JOB_LEVEL"] = 30;
        offerInventory.Items.Add(QC.Item.CarbunclesRuby);
        var offerSafe = new Fake { MainLevel = 30 };
        offerSafe.MainSettings["ADVANCED_JOB_LEVEL"] = 30;
        offerSafe.ItemsElsewhere[1] = new System.Collections.Generic.HashSet<int> { QC.Item.CarbunclesRuby };
        Check("windurst g5 rainbow:the offer counts the ruby in the inventory only",
              "True|False",
              $"{rb.States[0].Matches[0].When(offerInventory)}|{rb.States[0].Matches[0].When(offerSafe)}");

        // the zone's weather, not the char's: the zone HOT_SPELL (fire, bit 1 clear), the char's own THUNDER: the event
        // with Weather 4; the zone FOG (not valid), the char's HOT_SPELL: nothing, Weather 3
        var zoneFire = new Fake { Status = QuestStatus.Accepted, CurrentZoneWeather = 4, CurrentWeather = 14 };
        zoneFire.Items.Add(QC.Item.CarbunclesRuby);
        var zoneFog = new Fake { Status = QuestStatus.Accepted, CurrentZoneWeather = 3, CurrentWeather = 4 };
        zoneFog.Items.Add(QC.Item.CarbunclesRuby);
        Check("windurst g5 rainbow:the zone-in reads the zone's weather (getZone(true):getWeather()), not the char's",
              "Event(123):4|null:3",
              $"{W5ZoneIn(rb, zoneFire)}|{W5ZoneIn(rb, zoneFog)}");
    }
}
