// XI.Verify.Quests -- quest rework B3b Jeuno group 17 (call-only: The Wonder Magic Set, Cook's Pride, The Lost Cardian,
// The Kind Cardian). The golden flows take every arm. These checks pin what no flow can see: the replies' kinds. In an
// NPC call the bridge renders an Event and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs
// RenderCallReply: no action is built, so no priority), which is what the Lua scripts call; the fake keeps them apart.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunJeunoGroup17()
    {
        System.Console.WriteLine("== quest rework B3b Jeuno group 17: the Cardian chain (call-only) ==");
        QuestScript[] quests =
        {
            new QC.Jeuno.TheWonderMagicSet(), new QC.Jeuno.CooksPride(), new QC.Jeuno.TheLostCardian(), new QC.Jeuno.TheKindCardian(),
        };
        var defs = quests.Select(q => q.Compile()).ToArray();
        Check("jg17:the four clean", "///",
              string.Join("/", defs.Select(d => string.Join(" | ", QuestScriptValidator.Validate(d)))));

        // each trigger reply in declaration order, as the scripts had them: player:startEvent(id), no arguments. A lambda
        // reply (Cook's Pride's draw at Teigero-Bangero: math.randomInt(1, 2)) is invoked with the roll 1, then 2: 75 / 32
        string Reply(QuestHandlerDecl h, int roll)
        {
            var f = new Fake();
            f.Rolls.Enqueue(roll);
            return (h.Fixed ?? h.Reply?.Invoke(f)) is { } r ? QuestContextFake.Describe(r) : "null";
        }
        string Fixed(QuestScriptDefinition d) => string.Join(",", d.States.SelectMany(s => s.Handlers)
            .Where(h => h.Kind == QuestHandlerKind.OnTrigger)
            .Select(h => h.Fixed != null ? $"{h.Entity}:{Reply(h, 1)}" : $"{h.Entity}:{Reply(h, 1)}/{Reply(h, 2)}"));
        Check("jg17:the trigger replies are the Lua's startEvent ids, as Event",
              "Panta-Putta:Event(77),Panta-Putta:Event(55),Panta-Putta:Event(33),Panta-Putta:Event(40),Teigero-Bangero:Event(34)" +
              "|Naruru:Event(189),Naruru:Event(188),Naruru:Event(186),Naruru:Event(187),Teigero-Bangero:Event(75)/Event(32)" +
              "|Panta-Putta:Event(30),Panta-Putta:Event(40),Naruru:Event(31),Naruru:Event(71),Teigero-Bangero:Event(29)," +
              "Teigero-Bangero:Event(66),Monberaux:Event(33),Monberaux:Event(34)" +
              "|Apururu:Event(392),Apururu:Event(393),Apururu:Event(398),Apururu:NoAction(),Panta-Putta:Event(35)," +
              "Panta-Putta:Event(76),Naruru:Event(71),Naruru:Event(72),Teigero-Bangero:Event(66),Teigero-Bangero:Event(67)," +
              "Monberaux:Event(32)",
              string.Join("|", defs.Select(Fixed)));

        // Apururu's trade (Apururu.lua onTrade): the Ten of Cups card among the items (npcUtil.tradeHas): 397, else nil
        var trade = defs[3].States.SelectMany(s => s.Handlers).Single(h => h.Kind == QuestHandlerKind.OnTrade);
        string Trade(params int[] items)
        {
            var f = new Fake();
            foreach (var i in items) f.TradeItems[i] = 1;
            return trade.Reply(f) is { } r ? QuestContextFake.Describe(r) : "null";
        }
        // The Kind Cardian's Naruru 71 (Naruru.lua: Cook's Pride completed and this quest not completed): the Lua arm holds
        // while it is available too, but there The Lost Cardian's call (earlier in her script, Cook's Pride completed and
        // The Kind Cardian available: 31 / 71) always answers first, so no flow can see it (mutant round 1 survivor)
        var naruru71 = defs[3].States.Single(s => s.Handlers.Any(h => h.Entity == QC.Npc.LowerJeuno.Naruru && h.Fixed is { Id: 71 }));
        string Holds71(QuestStatus status, QuestStatus cooksPride)
        {
            var f = new Fake { Status = status };
            f.QuestStatuses["Jeuno/COOKS_PRIDE"] = cooksPride;
            return naruru71.IsMatch(f.Status, f.Step, f) ? "71" : "-";
        }
        Check("jg17:The Kind Cardian's Naruru 71: available, accepted, completed with Cook's Pride completed; accepted without",
              "71|71|-|-",
              string.Join("|", Holds71(QuestStatus.Available, QuestStatus.Completed), Holds71(QuestStatus.Accepted, QuestStatus.Completed),
                               Holds71(QuestStatus.Completed, QuestStatus.Completed), Holds71(QuestStatus.Accepted, QuestStatus.Accepted)));

        Check("jg17:Apururu's trade: the card alone, the card and a pebble, a pebble",
              "Event(397)|Event(397)|null",
              string.Join("|", Trade(QC.Item.TenOfCupsCard), Trade(QC.Item.TenOfCupsCard, QC.Item.Pebble), Trade(QC.Item.Pebble)));
    }
}
