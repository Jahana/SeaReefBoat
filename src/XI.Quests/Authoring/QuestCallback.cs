// XI.Quests -- deferred callbacks
// (quest rework P10, docs/superpowers/specs/2026-10-28-quest-deferred-callbacks-design.md): a function the engine keeps
// and calls later, outside any lookup handler. A Lua quest hands the engine a closure
// (npc:timer(5000, function(npcArg) ... end), player:addListener('TICK', name, function(playerArg) ... end)); a C#
// quest declares the function once, at the quest's level (QuestScriptBuilder.Callback), and arms its handle from a
// handler (q.Player.Timer, q.Npc.Timer, q.Player.AddListener). The function closes over nothing of Lua's: when it runs
// it gets a fresh context built from the entity the engine passes
// (and, with WithPlayer.Yes, the char that armed it, found by id), never Lua values kept since the arming.
using System;
using System.Text.RegularExpressions;

namespace XI.Quests.Authoring
{
    // one callback of a quest: its name (unique in the quest, [A-Za-z][A-Za-z0-9_]*: labels, logs, the fake) and its
    // body. It belongs to the quest, not to a state: a Lua timer runs its function whatever the section check says when
    // it fires (Mimeo Stone's stoneTimer runs across two sections). It returns nothing: the engine reads nothing back
    // from a timer or a TICK listener
    public sealed class QuestCallback
    {
        internal QuestCallback(string name, Action<IQuestContext> run)
        {
            Name = name;
            Run = run;
        }

        public string Name { get; }
        public Action<IQuestContext> Run { get; }

        // [A-Za-z][A-Za-z0-9_]*: the names the validator takes (the label 0/-/callback:<name> and the waiver regex)
        public static readonly Regex NamePattern = new Regex("^[A-Za-z][A-Za-z0-9_]*$");

        public override string ToString() => Name;
    }

    // q.Npc.Timer(ms, cb, WithPlayer.Yes): the callback also gets the char that armed it
    // (Hitting the Marquisate's Lua closure over `player`), found by id in the NPC's zone when the timer fires; gone
    // (zoned or logged out), the callback does not run and the bridge logs
    // `quests: <key> callback <name>: its char <id> is gone`
    public enum WithPlayer { No, Yes }

    // b.Export(global, cb, replacesModule): a Lua global the bridge sets to a function running the callback with
    // (player) (Dances with Luopans' xi.dancesWithLuopans.onHealing, which scripts/effects/healing.lua calls by name).
    // The module is the Lua module the quest stands in for
    // (the require name of its deleted file, by default its LegacyPath): the bridge claims package.loaded[module] so a
    // script that requires it still loads (design section 3.4)
    public sealed class QuestExport
    {
        internal QuestExport(string global, QuestCallback callback, string module)
        {
            Global = global;
            Callback = callback;
            Module = module;
        }

        // the global's dotted path: xi.dancesWithLuopans.onHealing
        public string Global { get; }
        public QuestCallback Callback { get; }
        // the require name the export claims; null: the quest's LegacyPath (resolved at registration)
        public string Module { get; }

        // a dotted path of Lua names: xi.dancesWithLuopans.onHealing
        public static readonly Regex GlobalPattern = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$");
    }
}
