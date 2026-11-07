// XI.Verify.Quests -- quest rework B3b Jeuno group 19 (call-only: A New Dawn). The golden flows take every arm. These
// checks pin what no flow can see: the replies' kinds. In an NPC call the bridge renders an Event and a ProgressEvent
// alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what
// the Lua scripts call; the fake keeps them apart.
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunJeunoGroup19()
    {
        System.Console.WriteLine("== quest rework B3b Jeuno group 19: the door _6t2, Osker, the Sarcophagus, Sturm (call-only) ==");
        var def = new QC.Jeuno.ANewDawn().Compile();
        Check("jg19:A New Dawn clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // each trigger reply in declaration order, as the scripts had them: player:startEvent(id), no arguments; the
        // Sarcophagus's on the first one (SARCOPHAGUS_OFFSET) and on the next, with the pop it asks for
        const int First = 17576395;
        string Triggers(int npcId, List<string> calls, string entity = null) => string.Join(",", def.States.SelectMany(s => s.Handlers)
            .Where(h => h.Kind == QuestHandlerKind.OnTrigger && (entity == null || h.Entity == entity))
            .Select(h =>
            {
                var f = new Fake { Npc = new QuestNpcFake(npcId) };
                f.NpcIds["SARCOPHAGUS_OFFSET"] = First;
                var r = h.Fixed ?? h.Reply?.Invoke(f);
                calls.AddRange(f.Calls);
                return $"{h.Entity}:{(r is { } x ? QuestContextFake.Describe(x) : "null")}";
            }));
        var firstCalls = new List<string>();
        var nextCalls = new List<string>();
        Check("jg19:the trigger replies are the Lua's startEvent ids, as Event; the first Sarcophagus pops (nothing shown) or plays 45, another one nothing",
              "_6t2:Event(5),_6t2:Event(4),_6t2:Event(1),_6t2:Event(2),_6t2:Event(3),_6t2:Event(0),Osker:Event(146),Osker:Event(147),Osker:Event(145)," +
              "Sarcophagus:NoAction(),Sarcophagus:Event(45),Sarcophagus:NoAction()" +
              "|Sarcophagus:null,Sarcophagus:null,Sarcophagus:null" +
              $"|Player.PopFromQmSeries({QC.Mob.TheEldiemeNecropolis.STURM},3,0,False,False)|",
              Triggers(First, firstCalls) + "|" +
              Triggers(First + 1, nextCalls, QC.Npc.TheEldiemeNecropolis.Sarcophagus) + "|" +
              string.Join(",", firstCalls) + "|" + string.Join(",", nextCalls));

        // Osker's 147 (Osker.lua: accepted, the var 4 or more): at the var 3 his 146 state (accepted, the var 2 or 3) comes
        // first and answers, so no flow can see a 147 check widened to the var 3 (Task 20 mutant [66]); pin the check
        var osker147 = def.States.Single(s => s.Handlers.Any(h => h.Entity == QC.Npc.UpperJeuno.Osker && h.Fixed is { Id: 147 }));
        string Holds147(int var)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.CharVars["ANewDawn_Event"] = var;
            return osker147.IsMatch(f.Status, f.Step, f) ? "147" : "-";
        }
        Check("jg19:Osker's 147 at accepted var 2, 3, 4, 5", "-|-|147|147",
              string.Join("|", Holds147(2), Holds147(3), Holds147(4), Holds147(5)));
    }
}
