// XI.Verify.Quests -- quest rework B3b hidden quests group 4 (Mog House Exposition, Monstrosity Bee, New Character
// Cutscenes): each definition validates clean; the Mog House zones the flows cannot reach (section 1 needs
// xi.moghouse.inMogHouseInHomeNation, false in the Mog Houses outside the nations).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunHiddenGroup4()
    {
        Console.WriteLine("== quest rework B3b hidden quests group 4: Mog House Exposition, Bee, new characters ==");
        var quests = new (string Name, QuestScript Script)[]
        {
            ("Mog House Exposition", new QC.HiddenQuests.MoghouseExposition()),
            ("Monstrosity Bee", new QC.HiddenQuests.MonstrosityBee()),
            ("New Character Cutscenes", new QC.HiddenQuests.NewCharacterCutscenes()),
        };
        foreach (var (name, script) in quests)
            Check($"hidden g4:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(script.Compile())));

        // all 21 Mog House zones carry section 1 (zone-in 30000 and its finish) and section 2 (the Moogle's 30001
        // and its finish); the 11 outside the nations are the flows' waivers (hidden g4 mog zones)
        var handlers = quests[0].Script.Compile().States.SelectMany(s => s.Handlers).ToList();
        string Zones(QuestHandlerKind kind, int id) => string.Join(",", handlers
            .Where(x => x.Kind == kind && (id == 0 || x.Id == id)).Select(x => x.Zone).Distinct().OrderBy(z => z));
        string all = string.Join(",", new ushort[]
        {
            QC.Zone.AlZahbi, QC.Zone.AhtUrhganWhitegate, QC.Zone.SouthernSanDoriaS, QC.Zone.BastokMarketsS,
            QC.Zone.WindurstWatersS, QC.Zone.SouthernSanDoria, QC.Zone.NorthernSanDoria, QC.Zone.PortSanDoria,
            QC.Zone.BastokMines, QC.Zone.BastokMarkets, QC.Zone.PortBastok, QC.Zone.WindurstWaters,
            QC.Zone.WindurstWalls, QC.Zone.PortWindurst, QC.Zone.WindurstWoods, QC.Zone.RuludeGardens,
            QC.Zone.UpperJeuno, QC.Zone.LowerJeuno, QC.Zone.PortJeuno, QC.Zone.WesternAdoulin, QC.Zone.EasternAdoulin,
        }.OrderBy(z => z));
        Check("hidden g4 mog zones: zone-in in all 21", all, Zones(QuestHandlerKind.OnZoneIn, 0));
        Check("hidden g4 mog zones: 30000 finish in all 21", all, Zones(QuestHandlerKind.OnEventFinish, 30000));
        Check("hidden g4 mog zones: 30001 finish in all 21", all, Zones(QuestHandlerKind.OnEventFinish, 30001));
    }
}
