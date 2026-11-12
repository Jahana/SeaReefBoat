using System;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Zone;

// quest rework C3 C2b: IQuestPlayer.SetMobSpawn on the live bridge
// (GetMobByID(zones[zone].mob[name] + offset):setSpawn). No golden flow can see it: the one caller, 4-3's qm, is
// followed by popFromQM and callPets, which set the spawn points again, so the binding's own arguments are read back
// here
internal static partial class Program
{
    private static string c3SpawnNpc_ = "";
    private static int c3SpawnOffset_ = 1;

    private sealed class SetMobSpawnQuest : QuestScript
    {
        private readonly bool inCheck_;

        public SetMobSpawnQuest(bool inCheck) : base(FixtureLog, FixtureConst) => inCheck_ = inCheck;

        protected override void Build(QuestScriptBuilder b)
        {
            Func<IQuestCheck, bool> when = inCheck_
                ? q => { ((IQuestPlayer)q.Player).SetMobSpawn("C3_SPAWN", 1, 1, 2, 3, 4); return true; }
                : null;
            var state = b.State(QuestStatus.Available, when: when).Npc(Zone.CarpentersLanding, c3SpawnNpc_);
            if (inCheck_)
            {
                state.OnTrigger(QuestReply.Event(9998));
                return;
            }

            state.OnTrigger(q =>
            {
                q.Player.SetMobSpawn("C3_SPAWN", c3SpawnOffset_, 1.25, -2.5, 3.75, 7);
                return QuestReply.Event(9998);
            });
        }
    }

    // Marsh Funguars of Carpenters' Landing: C3_SPAWN is the first of two mobs with consecutive ids, so +0 and +1 are
    // both real and the binding's id is read off which one moved
    private static void RunC3SetMobSpawn()
    {
        Console.WriteLine("quest rework C3 C2b: SetMobSpawn");
        ushort cl = Zone.CarpentersLanding;
        var info = zone.ZoneInfoOf(cl);
        c3SpawnNpc_ = info.Npcs.First(n => n.Status == NpcEntity.StatusNormal).Name;
        var ids = info.Mobs.Select(m => m.Id).OrderBy(i => i).ToList();
        uint first = ids.First(i => ids.Contains(i + 1));
        uint id = CharIds[0];

        string Spawn(uint mob) => Lua($"local p = GetMobByID({mob}):getSpawnPos() " +
                                      "report(p.x .. ' ' .. p.y .. ' ' .. p.z .. ' ' .. p.rot)");
        // the quest's NPC talked to; the log lines it raised, and the spawn points of the two mobs before and after.
        // eat: this run's own "Mob doesn't exist (2)" lines are removed from the log afterwards (the expected error)
        (string Said, string Before0, string Before1, string At0, string At1) Run(
            bool inCheck, int offset, string setup, bool eat = false)
        {
            c3SpawnOffset_ = offset;
            NewWorld();
            Lua($"zones[{cl}].mob.C3_SPAWN = {setup}");
            zone.TestQuestScripts.Add(new SetMobSpawnQuest(inCheck));
            Lua("InteractionGlobal.reload(true)");
            Cleanup(db, id);
            Fixture(id, cl, -414.6f, 0f, -363.5f, 0);
            Login();
            int mark = log.Count;
            var before = (Spawn(first), Spawn(first + 1));
            TalkTo(c3SpawnNpc_);
            string said = string.Join(" | ", log.Skip(mark).Where(l => l.Contains("spawn point") ||
                                                          l.Contains("is nil") || l.Contains("Mob doesn't exist")));
            var spawns = (Spawn(first), Spawn(first + 1));
            Finish();
            Logout();
            Cleanup(db, id);
            if (eat)
                for (int i = log.Count - 1; i >= mark; i--)
                    if (log[i].Contains("Mob doesn't exist (2)"))
                        log.RemoveAt(i);
            return (said, before.Item1, before.Item2, spawns.Item1, spawns.Item2);
        }

        try
        {
            const string Given = "1.25 -2.5 3.75 7";
            var one = Run(false, 1, first.ToString());
            Check("c3 SetMobSpawn: the mob at id + offset has the given x, y, z and rot", Given, one.At1);
            Check("  the mob at the id itself is left as it was", one.Before0, one.At0);
            var zero = Run(false, 0, first.ToString());
            Check("  offset 0: the id itself gets them", Given, zero.At0);
            Check("  offset 0: the next one is left as it was", zero.Before1, zero.At1);
            string nil = Run(false, 1, "1", eat: true).Said;
            Check("  an id + offset with no mob: the Lua's nil call, as the message (and GetMobByID's own error line)",
                  $"{FixtureKey} {c3SpawnNpc_} onTrigger: GetMobByID(2) is nil (C3_SPAWN+1 in zone {cl})|1",
                  $"{Last(nil, "is nil")}|{nil.Split(" | ").Count(l => l.Contains("Mob doesn't exist (2)"))}");
            Check("  from a check: refused", $"{FixtureKey} check 1: a check cannot set a mob's spawn point",
                  Last(Run(true, 1, first.ToString()).Said, "cannot set"));
        }
        finally
        {
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }
    }

    // the last log line with the text, from its "quests: " prefix
    private static string Last(string lines, string text) =>
        lines.Split(" | ").LastOrDefault(l => l.Contains(text))?.Replace("quests: ", "") ?? "no line";
}
