// XI.Verify.Quests -- quest rework B3b Outlands: the helpers the Outlands groups' checks share (lifted from group 2's
// OL2Handler / OL2Reply and group 3's OL3Handler / OL3Reply, which were the same code; Task 4's review).
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    // the state's handler of that kind for the entity (trigger, trade) or the zone's id (event finish: the csid, zone-in: 1)
    static QuestHandlerDecl OLHandler(QuestScriptDefinition def, int state, QuestHandlerKind kind, string entity = null, int id = 0,
                                      ushort zone = 0) =>
        def.States[state].Handlers.Single(h => h.Kind == kind && (entity != null ? h.Entity == entity : h.Id == id) &&
                                               (zone == 0 || h.Zone == zone));

    // the handler's reply on that fake, as QuestContextFake.Describe writes it ("null" for none)
    static string OLReply(QuestHandlerDecl h, Fake f)
    {
        var r = h.Fixed ?? h.Reply(f);
        return r == null ? "null" : QuestContextFake.Describe(r);
    }
}
