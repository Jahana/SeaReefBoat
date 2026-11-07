// XI.Verify.Quests -- quest rework B3b Jeuno group 20 (call-only: Full Speed Ahead!). The golden flows take every arm.
// These checks pin what no flow can see: the replies' kinds. In an NPC call the bridge renders an Event and a
// ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no
// priority), which is what Mapitoto's script called; the fake keeps them apart.
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunJeunoGroup20()
    {
        System.Console.WriteLine("== quest rework B3b Jeuno group 20: Mapitoto (call-only) ==");
        var def = new QC.Jeuno.FullSpeedAhead().Compile();
        Check("jg20:Full Speed Ahead! clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));

        // each trigger reply in declaration order, as the script had them: player:startEvent(id, ...); 10225's third
        // argument is ID.npc.MAPITOTO
        const int Mapitoto = 17776896;
        Check("jg20:the trigger replies are the Lua's startEvent ids and arguments, as Event",
              $"Mapitoto:Event(10226),Mapitoto:Event(10225,{QC.KeyItem.TrainersWhistle},{QC.Item.ChocoboWhistle},{Mapitoto})," +
              "Mapitoto:Event(10224,1),Mapitoto:Event(10223,0,0,4)",
              string.Join(",", def.States.SelectMany(s => s.Handlers)
                  .Where(h => h.Kind == QuestHandlerKind.OnTrigger)
                  .Select(h =>
                  {
                      var f = new Fake();
                      f.NpcIds["MAPITOTO"] = Mapitoto;
                      var r = h.Fixed ?? h.Reply?.Invoke(f);
                      return $"{h.Entity}:{(r is { } x ? QuestContextFake.Describe(x) : "null")}";
                  })));
    }
}
