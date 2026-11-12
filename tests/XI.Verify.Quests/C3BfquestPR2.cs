// XI.Verify.Quests -- quest rework C3 PR2: a battlefield's deferred callbacks (BattlefieldCallback) over the fakes. A
// hook arms a declared callback on a mob or a player (Timer, Queue, AddListener) with captured values; the fake's
// queues (BattlefieldCallbacksFake) fire it as the engine does: a timer on the first tick strictly past its deadline,
// soonest first and arming order among equals, the timers before the queued actions, a queued action only while the
// entity can change state; the battlefield's end (CBattlefield::RemoveEntity) drops the timers, a respawn
// everything; a listener replaced by its identifier, gone once removed, kept over a respawn. The arming's refusals,
// the coverage labels, the validator's rules and an entity kept past its call (IBattlefieldEngine.Keep) too.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;

internal static partial class Program
{
    // what the test battlefield's callbacks saw, in firing order
    static readonly List<string> pr2Seen_ = new List<string>();

    // a test battlefield (never in XI.QuestContent) whose callbacks write what they get into pr2Seen_
    sealed class Pr2Battlefield : BattlefieldScript
    {
        public Pr2Battlefield() : base("PR2_TEST")
        {
            Data = new BattlefieldData { ZoneId = 1, MaxPlayers = 1, TimeLimit = 60, EntryNpcs = new[] { "E" } };
            A = Data.Callback("a", (q, a) => pr2Seen_.Add($"a@{a.Entity?.Id}"));
            B = Data.Callback("b", (q, a) => pr2Seen_.Add($"b@{a.Entity?.Id}"));
            C = Data.Callback("c", (q, a) => pr2Seen_.Add($"c@{a.Entity?.Id}"));
            Captured = Data.Callback("captured", (q, a) => pr2Seen_.Add(
                $"captured@{a.Entity?.Id} n={a.CaptureCount} bf={a.CapturedBattlefield(0)?.Id} " +
                $"int={a.CapturedInt(1)} mob={a.CapturedEntity(2)?.Id} nil={a.CapturedEntity(3) == null} " +
                $"script={q.Script?.Key} player={q.Player != null}"));
            Death = Data.Callback("death", (q, a) => pr2Seen_.Add(
                $"death@{a.Entity?.Id} args={a.ArgCount} killer={a.EntityArg(2)?.Id} player={q.Player?.Id}"));
            Rearm = Data.Callback("rearm", (q, a) =>
            {
                pr2Seen_.Add($"rearm@{a.Entity?.Id}");
                a.Entity.Timer(0, A);
            });
        }

        public override BattlefieldData Data { get; }
        public readonly BattlefieldCallback A, B, C, Captured, Death, Rearm;
    }

    static void RunC3PR2()
    {
        Console.WriteLine("\n== c3 PR2: a battlefield's deferred callbacks (timer, queue, addListener closures) ==");
        var s = new Pr2Battlefield();
        BattlefieldEngineFake engine = null;
        BattlefieldCallbacksFake queues = null;
        BattlefieldMobFake Mob(int id)
        {
            var m = new BattlefieldMobFake { Id = id, IsSpawned = true, HP = 10, MaxHP = 10 };
            engine.MobsById[id] = m;
            return (BattlefieldMobFake)engine.MobById(id);
        }
        void World()
        {
            pr2Seen_.Clear();
            engine = new BattlefieldEngineFake();
            queues = new BattlefieldCallbacksFake(s, engine);
            engine.Callbacks = queues;
        }
        string Seen() => string.Join(" ", pr2Seen_);
        // an arming whose refusal shows in what was seen (a check fails on it, the run goes on)
        void Arm(Action arm)
        {
            try
            {
                arm();
            }
            catch (Exception e)
            {
                pr2Seen_.Add("threw:" + e.GetType().Name);
            }
        }

        // firing: on the first tick strictly past the deadline (CAIActionQueue::checkAction's tick > deadline), once
        World();
        var m1 = Mob(101);
        Arm(() => m1.Timer(1000, s.A));
        int at1000 = queues.Tick(1000);
        string before = Seen();
        int at1001 = queues.Tick(1001);
        string after = Seen();
        int later = queues.Tick(5000);
        Check("c3 PR2: a timer armed for 1000 ms fires on the first tick past it (not at 1000, at 1001), once",
              "0||1|a@101|0|a@101", $"{at1000}|{before}|{at1001}|{after}|{later}|{Seen()}");
        Check("c3 PR2: the firing reached the callback's label; the arming is recorded on the mob",
              "callback:a|Mob.Timer(1000,a)", $"{string.Join(",", queues.ReachedLabels)}|{string.Join(",", m1.Calls)}");

        // order: soonest first, arming order among equal deadlines, an entity's timers before its queued actions;
        // the entities by id; a timer a firing callback arms (its deadline the tick's time + ms) waits for a later tick
        World();
        var m2 = Mob(202);
        var m3 = Mob(203);
        Arm(() => m3.Timer(100, s.C));
        Arm(() => m2.Queue(100, s.C));
        Arm(() => m2.Timer(500, s.A));
        Arm(() => m2.Timer(500, s.B));
        Arm(() => m2.Timer(300, s.C));
        queues.Tick(1000);
        Check("c3 PR2: order: per entity by id, its timers soonest first (arming order among equals), then its queue",
              "c@202 a@202 b@202 c@202 c@203", Seen());
        World();
        var m4 = Mob(204);
        Arm(() => m4.Timer(10, s.Rearm));
        queues.Tick(100);
        string sameTick = Seen();
        queues.Tick(101);
        Check("c3 PR2: a timer armed for 0 ms by a firing callback runs on the next tick, not in the same one",
              "rearm@204|rearm@204 a@204", $"{sameTick}|{Seen()}");

        // a queued action waits while the entity cannot change state (CAIContainer::CanChangeState); a timer does not
        World();
        var m5 = Mob(205);
        m5.Busy = true;
        Arm(() => m5.Queue(10, s.A));
        Arm(() => m5.Timer(10, s.B));
        queues.Tick(100);
        string busy = Seen();
        m5.Busy = false;
        queues.Tick(200);
        Check("c3 PR2: queue waits while the mob is busy, timer does not", "b@205|b@205 a@205", $"{busy}|{Seen()}");

        // cancel on the end: the battlefield's cleanup removes each of its entities (CBattlefield::RemoveEntity),
        // which drops the entity's timers, not its queued actions; a respawn (PAI->Reset) drops both; a despawn
        // alone drops nothing (a despawned mob still ticks its queue); the listeners stay (the entity's EventHandler)
        World();
        var m6 = Mob(206);
        Arm(() => m6.Timer(500, s.A));
        Arm(() => m6.Queue(500, s.B));
        Arm(() => m6.AddListener("DEATH", "PR2_DEATH", s.Death));
        engine.DespawnMob(206);
        int despawned = queues.Pending.Count;
        queues.RemoveEntity(m6);
        int ranAfterEnd = queues.Tick(10_000);
        string afterEnd = Seen();
        Arm(() => m6.Timer(500, s.C));
        engine.SpawnMob(206);
        int afterRespawn = queues.Pending.Count;
        int heard = queues.Trigger(m6, "DEATH");
        Check("c3 PR2: cancel on the end: a despawn keeps both; the battlefield's RemoveEntity drops the timer (the " +
              "queued action still runs); a respawn drops what is left; the listener stays",
              "2|1|b@206|0|1|b@206 death@206 args=1 killer= player=",
              $"{despawned}|{ranAfterEnd}|{afterEnd}|{afterRespawn}|{heard}|{Seen()}");

        // listeners: the engine's arguments (the entity, the killer), replaced by identifier, removed
        World();
        var m7 = Mob(207);
        var killer = Mob(208);
        Arm(() => m7.AddListener("DEATH", "PR2_DEATH", s.A));
        Arm(() => m7.AddListener("DEATH", "PR2_DEATH", s.Death));
        queues.Trigger(m7, "DEATH", killer);
        string replaced = Seen();
        m7.RemoveListener("PR2_DEATH");
        int afterRemove = queues.Trigger(m7, "DEATH", killer);
        Check("c3 PR2: a listener added again by its identifier replaces the first; it gets (entity, killer); " +
              "removed: none runs", "death@207 args=2 killer=208 player=|0", $"{replaced}|{afterRemove}");
        var p = new BattlefieldPlayerFake
        {
            CharId = 9001, Callbacks = queues, AsEntity = new BattlefieldMobFake { Id = 9001 },
        };
        Arm(() => p.AddListener("DEATH", "PR2_PDEATH", s.Death));
        queues.Trigger(p, "DEATH", killer);
        p.RemoveListener("PR2_PDEATH");
        int pAfter = queues.Trigger(p, "DEATH", killer);
        Check("c3 PR2: a player's listener: q.Player is the char, Entity its entity; removed by RemoveListener",
              "death@207 args=2 killer=208 player= death@9001 args=2 killer=208 player=9001|0", $"{Seen()}|{pAfter}");

        // captures: the arming's values reach the firing, in order, nil kept
        World();
        var m8 = Mob(209);
        var m9 = Mob(210);
        var bf = new BattlefieldViewFake { Id = 226 };
        Arm(() => m8.Timer(0, s.Captured, bf, 7, m9, null));
        queues.Tick(1);
        Check("c3 PR2: the captured battlefield, number, mob and nil reach the callback; its call is the battlefield's",
              "captured@209 n=4 bf=226 int=7 mob=210 nil=True script=battlefield.pr2_test player=False", Seen());

        // refusals: a callback of another battlefield, a negative delay, a value Lua cannot hold, no queues
        World();
        var m10 = Mob(211);
        var other = new Pr2Battlefield();
        string Throws(Action a)
        {
            try
            {
                a();
                return "none";
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }
        }
        Check("c3 PR2: refused: another battlefield's callback, -1 ms, a list, a string or a bool captured, " +
              "a null callback, no queues",
              "InvalidOperationException ArgumentOutOfRangeException ArgumentException ArgumentException " +
              "ArgumentException ArgumentNullException InvalidOperationException 0",
              $"{Throws(() => m10.Timer(0, other.A))} {Throws(() => m10.Timer(-1, s.A))} " +
              $"{Throws(() => m10.Queue(0, s.A, new List<int>()))} {Throws(() => m10.Queue(0, s.A, "x"))} " +
              $"{Throws(() => m10.Queue(0, s.A, true))} " +
              $"{Throws(() => m10.AddListener("DEATH", "X", null))} " +
              $"{Throws(() => new BattlefieldMobFake { Id = 1 }.Timer(0, s.A))} {queues.Pending.Count}");
        var noScript = new BattlefieldMobFake { Id = 2, Callbacks = new BattlefieldCallbacksFake(null, engine) };
        Check("c3 PR2: refused: an arming with no battlefield (a zone handler's call)", "InvalidOperationException",
              Throws(() => noScript.Timer(0, s.A)));

        // coverage: one label per callback; the validator's name rules
        Check("c3 PR2: each callback is a coverage label",
              "callback:a,callback:b,callback:c,callback:captured,callback:death,callback:rearm",
              string.Join(",", s.CoverageLabels()));
        var dup = new Pr2Battlefield();
        dup.Data.Callback("a", (q, a) => { });
        dup.Data.Callback("1bad", (q, a) => { });
        dup.Data.Callbacks.Add(null);
        Check("c3 PR2: the validator refuses a name declared twice, a name off the pattern and a null callback",
              "callback a declared twice|callback name 1bad|callback 9 is null",
              string.Join("|", BattlefieldScriptValidator.Validate(dup)));
        Check("c3 PR2: the test battlefield itself validates clean", "",
              string.Join("|", BattlefieldScriptValidator.Validate(s)));

        // an entity kept past its call: the same entity read back in a later call
        World();
        var m11 = Mob(212);
        var kept = engine.Keep(m11);
        Check("c3 PR2: an entity kept (IBattlefieldEngine.Keep) reads back as itself; null stays null",
              "212|True|True", $"{engine.Entity(kept)?.Id}|{engine.Keep(null) == null}|{engine.Entity(null) == null}");
    }
}
