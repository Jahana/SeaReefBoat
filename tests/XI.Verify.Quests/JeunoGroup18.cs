// XI.Verify.Quests -- quest rework B3b Jeuno group 18 (call-only: Fistful of Fury, Beat Around the Bushin). The golden
// flows take every arm. These checks pin what no flow can see: the replies' kinds. In an NPC call the bridge renders an
// Event and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so
// no priority), which is what the Lua scripts call; the fake keeps them apart.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunJeunoGroup18()
    {
        System.Console.WriteLine("== quest rework B3b Jeuno group 18: Vola and the door _6tc (call-only) ==");
        QuestScript[] quests = { new QC.Jeuno.FistfulOfFury(), new QC.Jeuno.BeatAroundTheBushin() };
        var defs = quests.Select(q => q.Compile()).ToArray();
        Check("jg18:the two clean", "/",
              string.Join("/", defs.Select(d => string.Join(" | ", QuestScriptValidator.Validate(d)))));

        // each trigger reply in declaration order, as the scripts had them: player:startEvent(id), no arguments
        string Triggers(QuestScriptDefinition d) => string.Join(",", d.States.SelectMany(s => s.Handlers)
            .Where(h => h.Kind == QuestHandlerKind.OnTrigger)
            .Select(h => $"{h.Entity}:{((h.Fixed ?? h.Reply?.Invoke(new Fake())) is { } r ? QuestContextFake.Describe(r) : "null")}"));
        Check("jg18:the trigger replies are the Lua's startEvent ids, as Event",
              "Vola:Event(216),Vola:Event(215)|Vola:Event(160),Vola:Event(214),_6tc:Event(155)",
              string.Join("|", defs.Select(Triggers)));

        // the trades (Vola.lua / _6tc.lua onTrade): trade:hasItemQty(item, 1) for each item and trade:getItemCount()
        string Trade(QuestHandlerDecl h, params int[] items)
        {
            var f = new Fake();
            foreach (var i in items) f.TradeItems[i] = f.TradeItems.TryGetValue(i, out var n) ? n + 1 : 1;
            return h.Reply(f) is { } r ? QuestContextFake.Describe(r) : "null";
        }
        var trades = defs.Select(d => d.States.SelectMany(s => s.Handlers).Where(h => h.Kind == QuestHandlerKind.OnTrade).ToArray()).ToArray();

        var vola = trades[0].Single();
        Check("jg18:Vola's trade: the three; the three and a pebble; two of them; two fangs and a vine and a skin",
              "Event(213)|null|null|null",
              string.Join("|", Trade(vola, QC.Item.NueFang, QC.Item.MorbolgerVine, QC.Item.DodoSkin),
                               Trade(vola, QC.Item.NueFang, QC.Item.MorbolgerVine, QC.Item.DodoSkin, QC.Item.Pebble),
                               Trade(vola, QC.Item.NueFang, QC.Item.MorbolgerVine),
                               Trade(vola, QC.Item.NueFang, QC.Item.NueFang, QC.Item.MorbolgerVine, QC.Item.DodoSkin)));

        // the door's four trades (var 2 / 4 / 6 / 7, in the states' when:): its item alone; its item and a pebble; two of
        // its item; a pebble
        int[] doorItems = { QC.Item.WyrmBeard, QC.Item.BehemothTongue, QC.Item.AdamantoiseEgg, QC.Item.BrownBelt };
        Check("jg18:the door's trades: the item alone, with a pebble, two of it, a pebble",
              "Event(156),null,null,null|Event(157),null,null,null|Event(158),null,null,null|Event(159),null,null,null",
              string.Join("|", trades[1].Select((h, i) => string.Join(",", Trade(h, doorItems[i]), Trade(h, doorItems[i], QC.Item.Pebble),
                                                                           Trade(h, doorItems[i], doorItems[i]), Trade(h, QC.Item.Pebble)))));
    }
}
