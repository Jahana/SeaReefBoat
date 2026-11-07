// XI.Verify.Quests -- quest rework B3b Abyssea group 3 (the nine maw quests, A Goldstruck Gigas .. An Ulcerous
// Uragnite): each definition validates clean, declares no steps and has the Lua's two sections (available, accepted).
// Their branches are the flows' (tests/golden/flows/abyssea.*.flow), except what no trace can see: the zone-ins' reply
// kind. A zone-in plays the first csid in container order whatever its kind (interaction_lookup.lua: onZoneIn returns
// its number before any priority is read), so each zone-in's Event is pinned here, the Lua's kind. The maws' offers
// (ProgressEvent) are pinned too, though the flows show them as well: each maw has its own onTrigger
// (xi.abyssea.entranceMawOnTrigger), and an Event there would alternate with it on the repeat talk.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAbysseaGroup3()
    {
        Console.WriteLine("== quest rework B3b Abyssea group 3 ==");
        var maws = new (string Name, QuestScript Quest, ushort Zone, string Maw, int Title)[]
        {
            ("A Goldstruck Gigas", new QC.Abyssea.AGoldstruckGigas(), QC.Zone.LaTheinePlateau,
             QC.Npc.LaTheinePlateau.CavernousMaw, QC.Title.BriareusFeller),
            ("To Paste a Peiste", new QC.Abyssea.ToPasteAPeiste(), QC.Zone.KonschtatHighlands,
             QC.Npc.KonschtatHighlands.CavernousMaw, QC.Title.KukulkanDefanger),
            ("Megadrile Menace", new QC.Abyssea.MegadrileMenace(), QC.Zone.TahrongiCanyon,
             QC.Npc.TahrongiCanyon.CavernousMaw, QC.Title.GlavoidStampeder),
            ("The Beast of Bastore", new QC.Abyssea.TheBeastOfBastore(), QC.Zone.JugnerForest,
             QC.Npc.JugnerForest.CavernousMaw2, QC.Title.SednaTuskbreaker),
            ("A Delectable Demon", new QC.Abyssea.ADelectableDemon(), QC.Zone.ValkurmDunes,
             QC.Npc.ValkurmDunes.CavernousMaw, QC.Title.CireinCroinHarpooner),
            ("A Fluttery Fiend", new QC.Abyssea.AFlutteryFiend(), QC.Zone.BuburimuPeninsula,
             QC.Npc.BuburimuPeninsula.CavernousMaw, QC.Title.ItzpapalotlDeclawer),
            ("A Beaked Blusterer", new QC.Abyssea.ABeakedBlusterer(), QC.Zone.SouthGustaberg,
             QC.Npc.SouthGustaberg.CavernousMaw, QC.Title.BennuDeplumer),
            ("A Man-eating Mite", new QC.Abyssea.AManEatingMite(), QC.Zone.Xarcabard,
             QC.Npc.Xarcabard.CavernousMaw, QC.Title.ReshephEradicator),
            ("An Ulcerous Uragnite", new QC.Abyssea.AnUlcerousUragnite(), QC.Zone.NorthGustaberg,
             QC.Npc.NorthGustaberg.CavernousMaw2, QC.Title.AmphitriteShucker),
        };
        foreach (var m in maws)
        {
            Check($"abyssea g3:{m.Name} clean", "",
                  string.Join(" | ", QuestScriptValidator.Validate(m.Quest.Compile())));
            Check($"abyssea g3:{m.Name} declares steps []", "", string.Join(",", m.Quest.Compile().DeclaredSteps));
        }

        // the statuses and steps each state matches, state by state
        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => (m.AnyStatus ? "any" : m.Status.ToString()) + (m.Step is int st ? ":" + st : "")))));
        Check("abyssea g3:the states' statuses and steps",
              string.Join(" | ", Enumerable.Repeat("Available Accepted", 9)),
              string.Join(" | ", maws.Select(m => States(m.Quest))));

        // each maw's offer (available, one stone, Dawn of Death accepted) and each zone-in (accepted, the title), with
        // its zone and NPC, as the fake states that open the sections give them
        var lines = new System.Collections.Generic.List<string>();
        foreach (var m in maws)
        {
            var d = m.Quest.Compile();
            var offer = new Fake { Status = QuestStatus.Available, HeldTraverserStonesValue = 1 };
            offer.QuestStatuses[$"{QuestLog.Abyssea}/DAWN_OF_DEATH"] = QuestStatus.Accepted;
            var zin = new Fake { Status = QuestStatus.Accepted };
            zin.Titles.Add(m.Title);
            string maw = AU3Npc(d, QuestHandlerKind.OnTrigger, m.Zone, m.Maw, offer);
            var zi = AU3Up(d, zin).Single(x => x.Kind == QuestHandlerKind.OnZoneIn && x.Zone == m.Zone);
            string zone = AU1Describe(zi, zin);
            lines.Add($"{m.Zone}:{maw}/{zone}");
        }
        Check("abyssea g3:the maws' offers and the zone-ins, each the Lua's kind",
              "102:ProgressEvent(9)/Event(10) ; 108:ProgressEvent(0)/Event(1) ; 117:ProgressEvent(38)/Event(39) ; " +
              "104:ProgressEvent(48)/Event(49) ; 103:ProgressEvent(56)/Event(57) ; " +
              "118:ProgressEvent(62)/Event(63) ; 107:ProgressEvent(0)/Event(1) ; 112:ProgressEvent(58)/Event(59) ; " +
              "106:ProgressEvent(0)/Event(1)", string.Join(" ; ", lines));
    }
}
