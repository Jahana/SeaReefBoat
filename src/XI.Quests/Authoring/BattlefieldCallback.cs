// XI.Quests -- quest rework C3 PR2: a battlefield's deferred callbacks. A Lua battlefield hands the engine a closure
// (mob:timer(7000, function(mobArg) ... end), mob:queue(3000, fn), mob:addListener('DEATH', id, function(dead) ...
// end), player:addListener(...)) that may close over the hook's locals (the battlefield, numbers). A C# battlefield
// declares the function once, in its data (BattlefieldData.Callback), and arms it from a hook on the entity's facet
// (q.Engine.MobById(id).Timer(7000, carbyGone)), handing over the values the Lua closure keeps as captures. The
// bridge gives the engine a Lua closure over the callback's C# function and those values, through the entity's own
// timer, queue and addListener bindings, so the deadline, the order, the clearing on a despawn or a zone change and
// the listener's replacement by identifier are the engine's, as for the Lua closure. When it fires the callback gets
// a call of its own: the engine's arguments and the captures, each a facet of that call.
//
// An entity a hook keeps past its call (content.lastEnmityList = mob:getEnmityList(), read on a later tick) is kept
// with IBattlefieldEngine.Keep and read back in a later call with IBattlefieldEngine.Entity: a facet is a value on
// its own call's stack and is not valid after it.
using System;
using System.Text.RegularExpressions;

namespace XI.Quests.Authoring
{
    // the callback's body: q is a call of the battlefield with no player, npc or battlefield argument (Player is the
    // entity when the engine fired it on a char), a the engine's arguments and the captured values
    public delegate void BattlefieldCallbackBody(IBattlefieldContext q, IBattlefieldCallbackArgs a);

    // one callback of a battlefield: its name (unique in the battlefield, [A-Za-z][A-Za-z0-9_]*: its coverage label
    // callback:<name>, the logs) and its body. It returns nothing: the engine reads nothing back from a timer, a queued
    // action or a listener
    public sealed class BattlefieldCallback
    {
        public BattlefieldCallback(string name, BattlefieldCallbackBody run)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public string Name { get; }
        public BattlefieldCallbackBody Run { get; }

        // the coverage label (BattlefieldScript.CoverageLabels): reached when it fires
        public string Label => LabelPrefix + Name;
        public const string LabelPrefix = "callback:";

        // the names the validator takes
        public static readonly Regex NamePattern = new Regex("^[A-Za-z][A-Za-z0-9_]*$");

        public override string ToString() => Name;

        // an arming's checks, the bridge's and the fake's alike: a callback of the battlefield's data (a handle of
        // another battlefield's would fire with this one's content), a delay of 0 ms or more (the C++ takes an int and
        // fires a negative one at once: a slip), and captures the body can read back (IBattlefieldCallbackArgs)
        public static void CheckArming(BattlefieldScript script, BattlefieldCallback callback, int ms,
                                       object[] captures)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (script == null)
                throw new InvalidOperationException($"callback {callback.Name}: no battlefield to arm it");
            if (!script.Data.Callbacks.Contains(callback))
                throw new InvalidOperationException($"callback {callback.Name} is not {script.Key}'s");
            if (ms < 0) throw new ArgumentOutOfRangeException(nameof(ms), ms, "a timer's delay is 0 ms or more");
            foreach (var c in captures ?? Array.Empty<object>())
                if (!IsCapturable(c))
                    throw new ArgumentException($"callback {callback.Name}: a {c.GetType().Name} cannot be captured",
                                                nameof(captures));
        }

        // a number, nil, or a facet of the call (a battlefield, an entity or a player): what CapturedInt,
        // CapturedBattlefield and CapturedEntity read back. A string or a bool waits for a port that needs one
        public static bool IsCapturable(object value) => value switch
        {
            null or int or double => true,
            IBattlefieldView or IBattlefieldEntity => true,
            _ => false,
        };
    }

    // what a callback gets when it fires: the engine's arguments (a timer's and a queued action's: the entity; a DEATH
    // listener's: the entity and its killer, or the entity alone; a TICK listener's: the entity and the milliseconds)
    // and the values its arming captured, in the arming's order, each a facet of the firing's own call
    public interface IBattlefieldCallbackArgs
    {
        // the engine's first argument: the entity the callback was armed on (mobArg, playerArg)
        IBattlefieldMob Entity { get; }
        // how many arguments the engine passed
        int ArgCount { get; }
        // the engine's argument at index (1 is Entity) as an entity; null for nil or past ArgCount
        IBattlefieldMob EntityArg(int index);
        // how many values the arming captured
        int CaptureCount { get; }
        // the captured value at index (0 is the first): a battlefield, an entity (a player too), a number; null for nil
        IBattlefieldView CapturedBattlefield(int index);
        IBattlefieldMob CapturedEntity(int index);
        int CapturedInt(int index);
    }

    // an entity kept past the call that read it (IBattlefieldEngine.Keep): the engine's own entity, not a facet; read
    // back in a later call with IBattlefieldEngine.Entity
    public sealed class BattlefieldKeptEntity
    {
        public BattlefieldKeptEntity(object handle) =>
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));

        // the engine's entity (live: the entity object the Lua userdata stands for; fake: the fake facet)
        public object Handle { get; }
    }
}
