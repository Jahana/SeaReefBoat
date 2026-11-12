using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

// quest rework C3 PR2: a C# battlefield's deferred callbacks on the live bridge. A battlefield hook arms a callback on
// a mob or a player (BattlefieldCallback: mob:timer, mob:queue, addListener) and the bridge hands the entity's own
// binding a Lua closure (CSharpQuests.bfCallback) over the callback's C# function and the captured values. So the
// C# callback sits in the engine's own queues beside a Lua closure: it fires on the first tick past its deadline,
// in arming order against Lua timers of the same deadline; the timer queue clear the battlefield's end makes
// (CBattlefield::RemoveEntity: clearTimerQueue) drops it and keeps a queued action; a despawn alone drops nothing
// (the despawned mob still ticks) and a respawn drops both; a listener gets the engine's arguments, is replaced by
// its identifier and removed. A test-only C# Eye of the Storm (Chamber of Oracles; its Lua file stays) runs the
// hooks, called through its proxy (contents[198]:onBattlefieldTick) with a mob of Carpenters' Landing. When
// Standalone ports eye_of_the_storm, this test copy collides with the port's key (as C2Battlefields.cs's does): move
// both to a battlefield still on Lua then.
internal static partial class Program
{
    // what the callbacks saw, in firing order; the hook's step; the mob and the char it arms on
    private static readonly List<string> pr2Live_ = new();
    private static string pr2Step_ = "";
    private static int pr2Mob_, pr2Char_;
    private static BattlefieldKeptEntity pr2Kept_;

    private sealed class Pr2LiveBattlefield : BattlefieldScript
    {
        public Pr2LiveBattlefield() : base("EYE_OF_THE_STORM")
        {
            Data = new BattlefieldData
            {
                ZoneId = QC.Zone.ChamberOfOracles, BattlefieldId = QC.Battlefield.EyeOfTheStorm, MaxPlayers = 6,
                TimeLimit = 1800, Index = 6, EntryNpcs = new[] { "SC_Entrance" },
                ExitNpcs = new[] { "Shimmering_Circle" },
            };
            Seq = Data.Callback("seq", (q, a) =>
            {
                var m = a.Entity;
                m.SetLocalVar("seq", m.LocalVar("seq") * 10 + 2);
                pr2Live_.Add($"seq bf={a.CapturedBattlefield(0)?.Id} n={a.CapturedInt(1)} args={a.ArgCount} " +
                             $"arg2={a.EntityArg(2)?.Id} self={q.Self?.BattlefieldId}");
            });
            Mark = Data.Callback("mark", (q, a) => pr2Live_.Add($"mark@{a.Entity.Id}"));
            QueueMark = Data.Callback("queueMark", (q, a) => pr2Live_.Add($"queueMark@{a.Entity.Id}"));
            Death = Data.Callback("death", (q, a) => pr2Live_.Add(
                $"death@{a.Entity.Id} args={a.ArgCount} killer={a.EntityArg(2)?.Id} cap={a.CapturedInt(0)} " +
                $"player={q.Player?.Id}"));
            Death2 = Data.Callback("death2", (q, a) => pr2Live_.Add($"death2@{a.Entity.Id}"));
            Boom = Data.Callback("boom", (q, a) => throw new InvalidOperationException("boom"));
        }

        public override BattlefieldData Data { get; }
        public readonly BattlefieldCallback Seq, Mark, QueueMark, Death, Death2, Boom;
        // a callback no battlefield declared: its arming is refused
        public static readonly BattlefieldCallback Stray = new BattlefieldCallback("stray", (q, a) => { });

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            var mob = q.Engine.MobById(pr2Mob_);
            switch (pr2Step_)
            {
                case "seq":
                    mob.Timer(500, Seq, q.Battlefield, 7);
                    break;
                case "end":
                    mob.Timer(500, Mark);
                    mob.Queue(500, QueueMark);
                    break;
                case "listen":
                    mob.AddListener("DEATH", "PR2_DEATH", Death, 5);
                    break;
                case "listen2":
                    mob.AddListener("DEATH", "PR2_DEATH", Death2);
                    break;
                case "unlisten":
                    mob.RemoveListener("PR2_DEATH");
                    break;
                case "playerListen":
                    q.Engine.GetPlayerByID(pr2Char_).AddListener("DEATH", "PR2_PDEATH", Death, 6);
                    break;
                case "stray":
                    mob.Timer(0, Stray);
                    break;
                case "boom":
                    mob.Timer(100, Boom);
                    mob.Timer(100, Mark);
                    break;
                case "keep":
                    pr2Kept_ = q.Engine.Keep(mob);
                    break;
                case "read":
                    var kept = q.Engine.Entity(pr2Kept_);
                    pr2Live_.Add($"kept {kept.Id} {kept.IsSpawned}");
                    break;
                // C3 C6 review M4: the battlefield player's TargId (player:getTargID()), which the Celestial Nexus'
                // 32004 finish stores on phase 2 and no packet shows
                case "targ":
                    pr2Live_.Add($"targ {q.Engine.GetPlayerByID(pr2Char_).TargId}");
                    break;
            }
        }
    }

    private static void RunC3PR2Live()
    {
        Console.WriteLine("quest rework C3 PR2: a C# battlefield's deferred callbacks, live");
        ushort cl = QC.Zone.CarpentersLanding;
        int eye = QC.Battlefield.EyeOfTheStorm;
        uint id = CharIds[0];
        NewWorld();
        zone.TestQuestScripts.Add(new Pr2LiveBattlefield());
        Lua("InteractionGlobal.reload(true)");
        var labels = new List<string>();
        zone.HandlerCalled = (k, label) =>
        {
            if (k == QC.BattlefieldKey.EyeOfTheStorm) labels.Add(label);
        };
        Cleanup(db, id);
        Fixture(id, cl, -414.6f, 0f, -363.5f, 0);
        Login();
        pr2Char_ = (int)id;
        pr2Mob_ = (int)zone.ZoneInfoOf(cl).Mobs.Select(m => m.Id).OrderBy(i => i).First();
        int mark = log.Count;
        pr2Live_.Clear();
        string mob = $"GetMobByID({pr2Mob_})";
        Lua($"SpawnMob({pr2Mob_})");
        // the hook, called as the engine calls it, with a battlefield standing in for the real one
        string Hook(string step, string before = "", string after = "")
        {
            pr2Step_ = step;
            return Lua($"local mob = {mob} {before} " +
                       $"xi.battlefield.contents[{eye}]:onBattlefieldTick(" +
                       $"{{ getID = function() return {eye} end }}, 0) " +
                       after);
        }
        string Seq() => Lua($"report(tostring({mob}:getLocalVar('seq')))");
        string Seen() => string.Join(" ", pr2Live_);

        // firing and order: a Lua timer, the C# one, another Lua timer, all for 500 ms in the same call
        Hook("seq",
             "mob:setLocalVar('seq', 0) " +
             "mob:timer(500, function(m) m:setLocalVar('seq', m:getLocalVar('seq') * 10 + 1) end)",
             "mob:timer(500, function(m) m:setLocalVar('seq', m:getLocalVar('seq') * 10 + 3) end)");
        Tick(500);
        string at500 = Seq() + "|" + Seen();
        Tick(1);
        Check("c3 PR2 live: a C# timer fires on the first tick past its deadline (not at 500 ms, at 501), between " +
              "the Lua timers armed before and after it",
              $"0||123|seq bf={eye} n=7 args=1 arg2= self={eye}", $"{at500}|{Seq()}|{Seen()}");
        Check("c3 PR2 live: its firing reached the callback's label", "callback:seq",
              string.Join(",", labels.Where(l => l.StartsWith("callback:"))));

        // the end: the battlefield's RemoveEntity clears the timer queue (clearTimerQueue): the C# timer goes with
        // it, the queued action stays
        pr2Live_.Clear();
        Hook("end", after: "mob:clearTimerQueue()");
        Tick(1000);
        Check("c3 PR2 live: the battlefield's end (clearTimerQueue) drops the C# timer; the queued action runs",
              $"queueMark@{pr2Mob_}", Seen());
        // a despawn alone drops nothing (the despawned mob still ticks, as CZoneEntities::mobTick); a respawn
        // (PAI->Reset) drops both
        pr2Live_.Clear();
        Hook("end", after: $"DespawnMob({pr2Mob_})");
        Tick(1000);
        string despawned = Seen();
        pr2Live_.Clear();
        Tick(5000);
        Hook("end", after: $"SpawnMob({pr2Mob_})");
        Tick(1000);
        Check("c3 PR2 live: after a despawn both still fire; armed on the despawned mob and respawned, neither",
              $"mark@{pr2Mob_} queueMark@{pr2Mob_}|", $"{despawned}|{Seen()}");
        // a callback armed again reuses its C# function: the Lua state registers no new one
        var functions = typeof(XI.Script.LuaState).GetField("functions_",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int Functions() => ((System.Collections.ICollection)functions.GetValue(lua)).Count;
        int registered = Functions();
        Hook("end");
        Hook("end");
        int added = Functions() - registered;
        pr2Live_.Clear();
        Tick(1000);
        Check("c3 PR2 live: arming the same callbacks again registers no new C# function, and both armings fire",
              $"0|mark@{pr2Mob_} mark@{pr2Mob_} queueMark@{pr2Mob_} queueMark@{pr2Mob_}", $"{added}|{Seen()}");

        // listeners: the engine's arguments and the capture, replaced by identifier, removed
        pr2Live_.Clear();
        string trigger = "mob:triggerListener('DEATH', mob, player)";
        Hook("listen", after: trigger);
        Hook("listen2", after: trigger);
        Hook("unlisten", after: trigger);
        Check("c3 PR2 live: a mob's DEATH listener gets (mob, killer) and its capture; added again by its identifier " +
              "it is replaced; removed, nothing runs",
              $"death@{pr2Mob_} args=2 killer={id} cap=5 player= death2@{pr2Mob_}", Seen());
        pr2Live_.Clear();
        Hook("playerListen", after: "player:triggerListener('DEATH', player, mob) " +
                                    "player:removeListener('PR2_PDEATH') player:triggerListener('DEATH', player, mob)");
        Check("c3 PR2 live: a player's listener: q.Player is the char; removed by its identifier",
              $"death@{id} args=2 killer={pr2Mob_} cap=6 player={id}", Seen());

        // an entity kept past its call reads back in a later call
        pr2Live_.Clear();
        Hook("keep");
        Hook("read");
        Check("c3 PR2 live: an entity kept in one call (IBattlefieldEngine.Keep) is the same mob in a later one",
              $"kept {pr2Mob_} True", Seen());

        // the battlefield player's TargId reads the char's targid (not its id)
        pr2Live_.Clear();
        Hook("targ");
        Check("c3 C6 live: the battlefield player's TargId is player:getTargID(), the char's targid, not its id",
              $"targ {c.TargId}|True", $"{Seen()}|{c.TargId != id}");

        // a callback the battlefield did not declare: the hook's arming is refused (logged), nothing armed
        int before = log.Count;
        pr2Live_.Clear();
        Hook("stray");
        Tick(1000);
        var refused = log.Skip(before).Where(l => l.Contains("callback stray")).ToList();
        Check("c3 PR2 live: a callback not the battlefield's is refused when armed",
              $"quests: {QC.BattlefieldKey.EyeOfTheStorm} onBattlefieldTick: callback stray is not " +
              $"{QC.BattlefieldKey.EyeOfTheStorm}'s|",
              string.Join(" | ", refused) + "|" + Seen());
        // the expected refusal leaves the log, so the suite's error checks do not count it
        for (int i = log.Count - 1; i >= before; i--)
            if (log[i].Contains("callback stray")) log.RemoveAt(i);

        // a callback that throws: logged as a C# battlefield hook's error is, and the mob's queue goes on
        before = log.Count;
        pr2Live_.Clear();
        Hook("boom");
        Tick(200);
        var boom = log.Skip(before).Where(l => l.Contains("boom")).ToList();
        Check("c3 PR2 live: a callback that throws is logged as the battlefield's, and the next timer still fires",
              $"quests: {QC.BattlefieldKey.EyeOfTheStorm} callback boom: boom|mark@{pr2Mob_}",
              string.Join(" | ", boom) + "|" + Seen());
        for (int i = log.Count - 1; i >= before; i--)
            if (log[i].Contains("boom")) log.RemoveAt(i);

        Check("c3 PR2 live: no script error on the way", "",
              string.Join(" | ", log.Skip(mark).Where(l => ScriptErrors().Contains(l) || l.Contains("callback "))));
        zone.HandlerCalled = null;
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        NewWorld();
    }
}
