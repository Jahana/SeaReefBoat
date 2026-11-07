// XI.Verify.Quests -- quest rework P7: the platform needs the Other Areas drafts found (a zone-wide onZoneOut handler, an
// onMobDeath's optParams, the alliance's char vars), on the builder, the modules, the validator and the fake. The bridge's
// side (the lookup's onZoneOut list, the live optParams and getAlliance reads) is in XI.Verify.QuestScripts.Live
// (PlatformP7.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP7()
    {
        Console.WriteLine("== quest rework P7: platform needs ==");
        RunP7ZoneOut();
        RunP7ZoneOutModules();
        RunP7MobDeath();
        RunP7AllianceCharVars();
    }

    // 1. OnZoneOut(zone, q => { ... }): interaction_lookup's onZoneOut (luautils::OnZoneOut, as the char leaves the zone), a
    // state-level handler with id 1 (as OnZoneIn), an action only (Elderly Pursuits: Prog 2 back to 1)
    static void RunP7ZoneOut()
    {
        var d = new BuiltQuest(b => b.State(QuestStatus.Accepted)
            .Npc(2, "qm_para").OnTrigger(QuestReply.Event(1))
            .OnZoneOut(2, q =>
            {
                if (q.Quest.Step == 2) q.Quest.SetStep(1);
            })).Compile();
        var h = d.States[0].Handlers.Single(x => x.Kind == QuestHandlerKind.OnZoneOut);
        Check("p7 zoneOut:a state-level handler of the zone, id 1, a run (no reply, no fixed)", "2/-/1/False/True/False",
              $"{h.Zone}/{h.Entity ?? "-"}/{h.Id}/{h.Reply != null}/{h.Run != null}/{h.Fixed != null}");
        string RunAt(int step)
        {
            var f = new Fake();
            f.Vars["Prog"] = step;
            h.Run(f);
            return $"{step}>{f.Step}";
        }
        Check("p7 zoneOut:its run in the fake: Prog 2 back to 1, any other Prog kept", "2>1|1>1|3>3", $"{RunAt(2)}|{RunAt(1)}|{RunAt(3)}");
        Check("p7 zoneOut:chained from an NPC builder it is the state's, not the NPC's", "-/1",
              new BuiltQuest(b => b.State(QuestStatus.Accepted).Npc(2, "qm_para").OnTrigger(QuestReply.Event(1)).OnZoneOut(2, q => { })).Compile()
                  .States[0].Handlers.Where(x => x.Kind == QuestHandlerKind.OnZoneOut).Select(x => $"{x.Entity ?? "-"}/{x.Id}").Single());
        Check("p7 zoneOut:a null run is refused", "run", P5BuildThrows(b => b.State(QuestStatus.Accepted).OnZoneOut(2, null)));
        Check("p7 zoneOut:the quest validates clean (Steps declared)", "",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => { b.Steps(1, 2); b.State(QuestStatus.Accepted).OnZoneOut(2, q => { }); }).Compile())));
        Check("p7 zoneOut:declared twice in one state", "OnZoneOut 1 declared twice in one state",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Accepted).OnZoneOut(2, q => { }).OnZoneOut(2, q => { })).Compile())));
        Check("p7 zoneOut:one per zone in a state (Carpenters' Landing and Tavnazian Safehold) validates clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Accepted).OnZoneOut(2, q => { }).OnZoneOut(26, q => { })).Compile())));
        Check("p7 zoneOut:in a call-only quest it is refused (no Zone.lua calls it through xi.cs)",
              "OnZoneOut 1: a called handler is OnTrigger, OnTrade or OnMobDeath on an entity, OnEventFinish, OnEventUpdate or OnZoneIn state-level",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnZoneOut(235, q => { })).Compile())));
    }

    // the base of the zone-out module checks: state 1 (Accepted) with Carpenters' Landing's OnZoneOut (Prog 2 -> 1)
    sealed class P7ZoneOutQuest : QuestScript
    {
        public P7ZoneOutQuest() : base(QuestLog.Jeuno, "P4_TEST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);
            b.State(QuestStatus.Accepted)
                .OnZoneOut(2, q =>
                {
                    if (q.Quest.Step == 2) q.Quest.SetStep(1);
                });
        }
    }

    static QuestOverrideResult P7Apply(Action<QuestModuleBuilder> build, List<string> baseRuns = null) =>
        QuestOverrides.Apply(new P7ZoneOutQuest().Compile(), new P4Module("p7_test_module", build).Compile(new Fake()), P4Key,
            baseRuns == null ? null : h => baseRuns.Add($"{h.Kind}:{h.Entity ?? h.Id.ToString()}"), null);

    // a module wraps, replaces or adds an OnZoneOut (QuestOverrides: the trigger onZoneOut, a run, state-level)
    static void RunP7ZoneOutModules()
    {
        Check("p7 zoneOut modules:its trigger name is onZoneOut", "onZoneOut", QuestOverrides.Trigger(QuestHandlerKind.OnZoneOut, 1));
        var baseRuns = new List<string>();
        var wrapped = P7Apply(m => m.Quest(P4Key).State(1).WrapOnZoneOut(2, (q, baseRun) =>
        {
            baseRun(q);
            q.Quest.SetVar("Out", 1);
        }), baseRuns);
        var f = new Fake();
        f.Vars["Prog"] = 2;
        wrapped.Definition.States[0].Handlers.Single(x => x.Kind == QuestHandlerKind.OnZoneOut).Run(f);
        Check("p7 zoneOut modules:a wrap runs the base (Prog 2 -> 1) and its own (Out 1)", "|1/1/OnZoneOut:1",
              $"{string.Join(" | ", wrapped.Problems)}|{f.Step}/{f.Var("Out")}/{string.Join(",", baseRuns)}");
        var replaced = P7Apply(m => m.Quest(P4Key).State(1).ReplaceOnZoneOut(2, q => q.Quest.SetVar("Out", 2)));
        f = new Fake();
        f.Vars["Prog"] = 2;
        replaced.Definition.States[0].Handlers.Single(x => x.Kind == QuestHandlerKind.OnZoneOut).Run(f);
        Check("p7 zoneOut modules:a replace runs its own only (Prog 2 kept)", "|2/2", $"{string.Join(" | ", replaced.Problems)}|{f.Step}/{f.Var("Out")}");
        var added = P7Apply(m => m.Quest(P4Key).State(1).AddOnZoneOut(26, q => { }));
        Check("p7 zoneOut modules:an added one is the module's, a state-level handler of its zone", "|26/-/1/True",
              $"{string.Join(" | ", added.Problems)}|" + string.Join(",", added.Definition.States[0].Handlers.Where(x => x.Kind == QuestHandlerKind.OnZoneOut && x.Module != null)
                  .Select(x => $"{x.Zone}/{x.Entity ?? "-"}/{x.Id}/{x.Run != null}")));
        var missing = P7Apply(m => m.Quest(P4Key).State(1).WrapOnZoneOut(26, (q, baseRun) => baseRun(q)));
        Check("p7 zoneOut modules:a wrap of an OnZoneOut the state does not have is refused",
              "p7_test_module: jeuno.p4_test: has no handler 1/-/onZoneOut in zone 26 to wrap", string.Join(" | ", missing.Problems));
    }

    // 2. q.MobDeath: an OnMobDeath's optParams (isKiller, noKiller, isWeaponSkillKill, weaponskillUsed, weaponskillDamage),
    // null outside one. Knocking on Forbidden Doors' Alsha: the killer only, Prog 4 when an alliance member is at Prog 3
    static void RunP7MobDeath()
    {
        var f = new Fake();
        IQuestContext q = f;
        Check("p7 mobDeath:null outside an OnMobDeath (the fake's default)", "null", q.MobDeath == null ? "null" : "set");
        f.MobDeath = new QuestMobDeathFake();
        Check("p7 mobDeath:the fake's default is a killer with no weapon skill", "True/False/False/0/0",
              $"{q.MobDeath.IsKiller}/{q.MobDeath.NoKiller}/{q.MobDeath.IsWeaponSkillKill}/{q.MobDeath.WeaponskillUsed}/{q.MobDeath.WeaponskillDamage}");
        f.MobDeath = new QuestMobDeathFake { IsKiller = false, IsWeaponSkillKill = true, WeaponskillUsed = 32, WeaponskillDamage = 1234 };
        Check("p7 mobDeath:each field as set", "False/False/True/32/1234",
              $"{q.MobDeath.IsKiller}/{q.MobDeath.NoKiller}/{q.MobDeath.IsWeaponSkillKill}/{q.MobDeath.WeaponskillUsed}/{q.MobDeath.WeaponskillDamage}");

        // Alsha's handler as the twin and the draft write it
        var d = new BuiltQuest(b =>
        {
            b.Steps(4);
            b.State(QuestStatus.Accepted).Npc(25, "Alsha").OnMobDeath(c =>
            {
                if (c.MobDeath.IsKiller)
                {
                    foreach (int memberProg in c.Player.AllianceCharVars("Quest[4][78]Prog"))
                    {
                        if (memberProg == 3) c.Quest.SetStep(4);
                    }
                }
            });
        }).Compile();
        var death = d.States[0].Handlers.Single();
        string Kill(bool killer, int own, params int[] members)
        {
            var k = new Fake { MobDeath = new QuestMobDeathFake { IsKiller = killer } };
            k.CharVars["Quest[4][78]Prog"] = own;
            k.Vars["Prog"] = own;
            foreach (var m in members) k.AllianceMemberCharVars.Add(new Dictionary<string, int> { ["Quest[4][78]Prog"] = m });
            death.Run(k);
            return k.Step.ToString();
        }
        Check("p7 mobDeath:Alsha: the killer at Prog 0 with a member at 3: Prog 4; not the killer: kept; no member at 3: kept; alone at 3: 4",
              "4|3|0|4", $"{Kill(true, 0, 3)}|{Kill(false, 3, 3)}|{Kill(true, 0, 2)}|{Kill(true, 3)}");
        Check("p7 mobDeath:a handler reading it with no optParams fails (as Lua's optParams.isKiller on nil)", "NullReferenceException",
              P7Throws(() => death.Run(new Fake())));
    }

    // Player.AllianceCharVars(name): getAlliance()'s members' getCharVar(name), in the table's order; the fake's char first
    static void RunP7AllianceCharVars()
    {
        var f = new Fake();
        IQuestContext q = f;
        f.CharVars["X"] = 7;
        Check("p7 alliance vars:alone, the char's own var", "7", string.Join(",", q.Player.AllianceCharVars("X")));
        f.AllianceMemberCharVars.Add(new Dictionary<string, int> { ["X"] = 3 });
        f.AllianceMemberCharVars.Add(new Dictionary<string, int>());
        Check("p7 alliance vars:the char, then each member's (0 for a var it lacks)", "7,3,0", string.Join(",", q.Player.AllianceCharVars("X")));
        Check("p7 alliance vars:on the check's view too", "7,3,0", string.Join(",", ((IQuestCheck)f).Player.AllianceCharVars("X")));
    }

    static string P7Throws(Action a)
    {
        try
        {
            a();
            return "no throw";
        }
        catch (Exception e)
        {
            return e.GetType().Name;
        }
    }
}
