// XI.Verify.Quests -- quest rework P9. Part A: the Lua type of a setting kept through the facet (Settings.MainValue) and the
// Lua-equal compares on it (MainEquals: `== n`, MainTruthy: `if x then`, MainFlag: `== true`), on the fake, on a numbers-only
// source, on xi.pre (QuestModule.Pre) and on each shipped site switched to them. The live side (the bridge's MainValue read
// from xi.settings.main) is in XI.Verify.QuestScripts.Live (PlatformP9.cs); the golden proof is the `#*_bool` flows.
// Part B: the hidden quest kind (HiddenQuestScript), its validator rules, the fake's refusals and Portal Charm's port. Part C:
// the OnTriggerAreaLeave handler kind (builder, validator, label, modules); its live side and golden probe:
// QuestScripts.Live P9 onTriggerAreaLeave, probe.trigger_area_leave.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP9()
    {
        Console.WriteLine("== quest rework P9 ==");
        RunP9SettingType();
        RunP9SettingFake();
        RunP9Pre();
        RunP9SettingSites();
        RunP9HiddenKind();
        RunP9HiddenValidator();
        RunP9HiddenFake();
        RunP9HiddenModuleCheck();
        RunP9PortalCharm();
        RunP9TriggerAreaLeave();
        // a leave QuestOverrides' KindProblem does not know throws in Apply: a failed check, not a crashed run
        try
        {
            RunP9TriggerAreaLeaveModules();
        }
        catch (Exception e)
        {
            Check("p9 leave modules:the module checks run (no exception)", "", $"threw {e.GetType().Name}: {e.Message}");
        }
        RunP9FakeParity();
    }

    static string P9Cmp(QuestSetting s) => $"{s.LuaEquals(1)}/{s.Truthy}/{s.IsTrue}";

    // A. the QuestSetting compares, as Lua's == 1, truthiness and == true
    static void RunP9SettingType()
    {
        Check("p9 setting:the number 1 equals 1, is truthy, is not true", "True/True/False", P9Cmp(QuestSetting.Of(1.0)));
        Check("p9 setting:true never equals 1 (Lua's `true == 1` is false), is truthy and true", "False/True/True", P9Cmp(QuestSetting.Of(true)));
        Check("p9 setting:false: not 1, not truthy, not true", "False/False/False", P9Cmp(QuestSetting.Of(false)));
        Check("p9 setting:1.25 is not 1; truthy", "False/True/False", P9Cmp(QuestSetting.Of(1.25)));
        Check("p9 setting:0 is truthy in Lua (if 0 then runs)", "False/True/False", P9Cmp(QuestSetting.Of(0.0)));
        Check("p9 setting:the string \"1\" is not the number 1; truthy", "False/True/False", P9Cmp(QuestSetting.Of("1")));
        Check("p9 setting:nil: not 1, not truthy, and not 0 either", "False/False/False/False",
              P9Cmp(QuestSetting.Nil) + "/" + QuestSetting.Nil.LuaEquals(0));
        Check("p9 setting:Of(null string) is nil; each type prints as Lua writes it", "Nil|nil|true|1.25|\"1\"",
              $"{QuestSetting.Of((string)null).Type}|{QuestSetting.Nil}|{QuestSetting.Of(true)}|{QuestSetting.Of(1.25)}|{QuestSetting.Of("1")}");
    }

    // A. the fake keeps the type: a boolean in MainBools is a Lua boolean for MainValue / MainEquals, and 1 / 0 for Main and
    // MainNumber (as the live context reads it); an unset name is nil, not 0
    static void RunP9SettingFake()
    {
        var f = new Fake();
        IQuestContext q = f;
        f.MainBools["ENABLE_TOAU"] = true;
        f.MainBools["OLDSCHOOL_G1"] = false;
        f.MainSettings["ENABLE_WOTG"] = 1;
        f.MainNumbers["ENABLE_TRUST_QUESTS"] = 1.25;
        f.MainSettings["OLDSCHOOL_G2"] = 0;
        Check("p9 setting:fake MainValue keeps each type", "Boolean|Boolean|Number|Number|Number|Nil",
              string.Join("|", new[] { "ENABLE_TOAU", "OLDSCHOOL_G1", "ENABLE_WOTG", "ENABLE_TRUST_QUESTS", "OLDSCHOOL_G2", "NOPE" }
                  .Select(n => q.Settings.MainValue(n).Type)));
        Check("p9 setting:fake MainEquals(name, 1): true only for the number 1", "False|True|False|False",
              string.Join("|", new[] { "ENABLE_TOAU", "ENABLE_WOTG", "ENABLE_TRUST_QUESTS", "NOPE" }.Select(n => q.Settings.MainEquals(n, 1))));
        Check("p9 setting:fake MainTruthy: false and nil off, 0 on", "False|True|False",
              $"{q.Settings.MainTruthy("OLDSCHOOL_G1")}|{q.Settings.MainTruthy("OLDSCHOOL_G2")}|{q.Settings.MainTruthy("NOPE")}");
        Check("p9 setting:fake MainFlag: the boolean true only", "True|False|False",
              $"{q.Settings.MainFlag("ENABLE_TOAU")}|{q.Settings.MainFlag("ENABLE_WOTG")}|{q.Settings.MainFlag("OLDSCHOOL_G1")}");
        Check("p9 setting:fake Main / MainNumber still read a boolean as 1 / 0 (the live reading)", "1/1|0/0",
              $"{q.Settings.Main("ENABLE_TOAU")}/{q.Settings.MainNumber("ENABLE_TOAU")}|{q.Settings.Main("OLDSCHOOL_G1")}/{q.Settings.MainNumber("OLDSCHOOL_G1")}");
        Check("p9 setting:the check's view reads the type too", "Boolean", ((IQuestCheck)f).Settings.MainValue("ENABLE_TOAU").Type.ToString());
        // a numbers-only source (a module's offline branch) gets MainValue from MainNumber
        IQuestSettings whole = new WholeSettings();
        Check("p9 setting:a numbers-only source's MainValue is MainNumber's number", "Number:1|Number:0",
              $"{whole.MainValue("RESTRICT_CONTENT").Type}:{whole.MainValue("RESTRICT_CONTENT")}|{whole.MainValue("X").Type}:{whole.MainValue("X")}");
    }

    // A. xi.pre (module_utils.lua): `RESTRICT_CONTENT == 1 or == true`, then `not (ENABLE_<tag> == 1 or == true)`; a fraction
    // is neither 1 nor true in Lua, though Main rounds 1.25 to 1
    static void RunP9Pre()
    {
        string Pre(Action<Fake> set)
        {
            var f = new Fake();
            set(f);
            return QuestModule.Pre(f, QuestExpansion.Rov).ToString();
        }
        Check("p9 pre:RESTRICT_CONTENT 1 / true restricts; ENABLE_ROV unset gates", "True|True",
              Pre(f => f.MainSettings["RESTRICT_CONTENT"] = 1) + "|" + Pre(f => f.MainBools["RESTRICT_CONTENT"] = true));
        Check("p9 pre:RESTRICT_CONTENT 1.25 (Main rounds it to 1) does not restrict, as in Lua", "False",
              Pre(f => f.MainNumbers["RESTRICT_CONTENT"] = 1.25));
        Check("p9 pre:RESTRICT_CONTENT false / 0 / unset do not restrict", "False|False|False",
              Pre(f => f.MainBools["RESTRICT_CONTENT"] = false) + "|" + Pre(f => f.MainSettings["RESTRICT_CONTENT"] = 0) + "|" + Pre(f => { }));
        Check("p9 pre:ENABLE_ROV 1 / true enables (no gate); 1.25 does not, as in Lua", "False|False|True",
              Pre(f => { f.MainSettings["RESTRICT_CONTENT"] = 1; f.MainSettings["ENABLE_ROV"] = 1; }) + "|" +
              Pre(f => { f.MainSettings["RESTRICT_CONTENT"] = 1; f.MainBools["ENABLE_ROV"] = true; }) + "|" +
              Pre(f => { f.MainSettings["RESTRICT_CONTENT"] = 1; f.MainNumbers["ENABLE_ROV"] = 1.25; }));
    }

    // A. each shipped `xi.settings.main.X == 1` site, now MainEquals: its state matches with the number 1 and not with the
    // boolean true or 1.25 (the three Lua readings); the rest of the state's check set up to pass
    static void RunP9SettingSites()
    {
        var sites = new (string Name, Func<QuestScript> Make, int State, QuestStatus Status, string Setting, Action<Fake> Setup)[]
        {
            ("Lure of the Wildcat (Bastok)", () => new XI.QuestContent.Bastok.LureOfTheWildcat(), 0, QuestStatus.Available, "ENABLE_TOAU", f => { }),
            ("Lure of the Wildcat (San d'Oria)", () => new XI.QuestContent.Sandoria.LureOfTheWildcat(), 0, QuestStatus.Available, "ENABLE_TOAU", f => { }),
            ("Lure of the Wildcat (Windurst)", () => new XI.QuestContent.Windurst.LureOfTheWildcat(), 0, QuestStatus.Available, "ENABLE_TOAU", f => { }),
            ("Lure of the Wildcat (Jeuno)", () => new XI.QuestContent.Jeuno.LureOfTheWildcat(), 0, QuestStatus.Available, "ENABLE_TOAU", f => { }),
            ("Too Many Chefs", () => new XI.QuestContent.Bastok.TooManyChefs(), 0, QuestStatus.Available, "ENABLE_WOTG", f => f.Fame[XI.QuestContent.FameArea.Bastok] = 5),
            ("Trust (Bastok)", () => new XI.QuestContent.Bastok.TrustBastok(), 0, QuestStatus.Available, "ENABLE_TRUST_QUESTS", f => f.MainLevel = 5),
            ("Chocobo on the Loose!", () => new XI.QuestContent.Jeuno.ChocoboOnTheLoose(), 0, QuestStatus.Available, "ENABLE_TOAU", f => f.MainLevel = 20),
            ("Lakeside Minuet", () => new XI.QuestContent.Jeuno.LakesideMinuet(), 0, QuestStatus.Available, "ENABLE_WOTG", f => f.MainLevel = 30),
            ("The Road to Aht Urhgan", () => new XI.QuestContent.Jeuno.TheRoadToAhtUrhgan(), 0, QuestStatus.Available, "ENABLE_TOAU", f => { }),
            ("Monstrosity (available)", () => new XI.QuestContent.OtherAreas.Monstrosity(), 0, QuestStatus.Available, "ENABLE_MONSTROSITY", f => { }),
            ("Monstrosity (accepted)", () => new XI.QuestContent.OtherAreas.Monstrosity(), 1, QuestStatus.Accepted, "ENABLE_MONSTROSITY", f => { }),
        };
        foreach (var s in sites)
        {
            var def = s.Make().Compile();
            string Match(Action<Fake> value)
            {
                var f = new Fake();
                s.Setup(f);
                value(f);
                return def.States[s.State].IsMatch(s.Status, 0, f).ToString();
            }
            Check($"p9 sites:{s.Name}: {s.Setting} 1 offers; true and 1.25 do not, as in Lua", "True|False|False",
                  Match(f => f.MainSettings[s.Setting] = 1) + "|" + Match(f => f.MainBools[s.Setting] = true) + "|" +
                  Match(f => f.MainNumbers[s.Setting] = 1.25));
        }
    }

    // ---- Part B: the hidden quest kind (HiddenQuestScript: HiddenQuest:new(name)) ----

    // a test hidden quest: one AnyState section at Kupipi; the mode picks a refusal to show
    sealed class P9Hidden : HiddenQuestScript
    {
        readonly int _mode;
        readonly string _legacy;
        public P9Hidden(int mode = 0, string legacy = "scripts/quests/hiddenQuests/P9_Test_Quest") : base("p9Test")
        {
            _mode = mode;
            _legacy = legacy;
        }
        public override string LegacyPath => _legacy;
        protected override void Build(QuestScriptBuilder b)
        {
            if (_mode == 3) b.Reward(new QuestReward { FameArea = 2, KeyItem = 195 });
            var st = _mode == 1 ? b.State(QuestStatus.Available, when: q => q.Quest.Step == 0) : b.AnyState(when: q => q.Quest.Step == 0);
            var npc = st.Npc(XI.QuestContent.Zone.HeavensTower, XI.QuestContent.Npc.HeavensTower.Kupipi);
            if (_mode == 2) npc.Called().OnTrigger(QuestReply.Event(251));
            else npc.OnTrigger(QuestReply.ProgressEvent(293));
        }
    }

    // a module's check override on a hidden quest: QuestOverrides' base check (MatchesOf) must not read Quest.Status,
    // which a hidden quest has not (it throws, and the swallowed throw made the check always false)
    static void RunP9HiddenModuleCheck()
    {
        const string key = "hiddenQuests.p9_test_quest";
        var res = QuestOverrides.Apply(new P9Hidden().Compile(),
            new P4Module("p9_test_module", m => m.Quest(key).State(1).WrapCheck((q, baseCheck) => baseCheck(q) && q.Quest.Var("Wait") == 0))
                .Compile(new Fake()), key, null, null);
        var st = res.Definition?.States[0];
        var f = new Fake { Hidden = true };
        string prog0 = st == null ? "refused" : P9Threw(() => st.IsMatch(default, 0, f));
        f.Step = 1;
        string prog1 = st == null ? "refused" : P9Threw(() => st.IsMatch(default, 0, f));
        Check("p9 hidden modules:a WrapCheck on a hidden quest runs its base check with no status: Prog 0 holds, Prog 1 does not",
              "|no throw: True|no throw: False", $"{string.Join(" | ", res.Problems)}|{prog0}|{prog1}");
    }

    sealed class P9EmptyHidden : HiddenQuestScript
    {
        public P9EmptyHidden() : base("") { }
        public override string LegacyPath => "scripts/quests/hiddenQuests/X";
        protected override void Build(QuestScriptBuilder b) { }
    }

    static string P9Threw(Func<object> f)
    {
        try { return $"no throw: {f()}"; } catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
    }

    // B1. the kind: key hiddenQuests.<file in lower case>, no log, no constant; the definition says it is hidden
    static void RunP9HiddenKind()
    {
        Check("p9 hidden:the key is hiddenQuests.<the LegacyPath file in lower case>", "hiddenQuests.trust_zeid_ii|hiddenQuests.portal_charm",
              $"{QuestScript.HiddenKey("scripts/quests/hiddenQuests/Trust_Zeid_II")}|{new XI.QuestContent.HiddenQuests.PortalCharm().KeyIn("ignored")}");
        var q = new P9Hidden();
        var def = q.Compile();
        Check("p9 hidden:a hidden quest has its HiddenQuest:new name, no log (255) and no constant; its definition is hidden",
              "p9Test|p9Test|255|null|True|p9Test", $"{q.Name}|{q.HiddenName}|{(int)q.Log}|{q.ConstName ?? "null"}|{def.Hidden}|{def.HiddenName}");
        var chefs = new XI.QuestContent.Bastok.TooManyChefs();
        Check("p9 hidden:a log quest is not hidden", "False|null", $"{chefs.Compile().Hidden}|{chefs.HiddenName ?? "null"}");
        Check("p9 hidden:no LegacyPath, no key", "InvalidOperationException: a hidden quest's key is its LegacyPath file's name: it has none",
              P9Threw(() => new P9Hidden(legacy: null).KeyIn("x")));
        Check("p9 hidden:an empty name is refused", "ArgumentException", P9Threw(() => new P9EmptyHidden()).Split(':')[0]);
    }

    // B1. the validator: what the Lua kind lacks (no status: a status match refused; no NPC call; giveReward's fameArea)
    static void RunP9HiddenValidator()
    {
        string V(int mode) => string.Join(" | ", QuestScriptValidator.Validate(new P9Hidden(mode).Compile()));
        Check("p9 hidden validator:an AnyState hidden quest validates", "", V(0));
        Check("p9 hidden validator:a status match is refused (no status)",
              "state 1 matches status Available: a hidden quest has no status (HiddenQuest:new has no quest log or id); use AnyState(when: ...)", V(1));
        Check("p9 hidden validator:a Called() handler is refused (a container only)",
              "Kupipi OnTrigger is Called(): a hidden quest is a container (HiddenQuest:new), its handlers are the lookup's; no NPC script calls one", V(2));
        Check("p9 hidden validator:reward.fameArea is refused (giveReward reads a table)",
              "reward.fameArea: a hidden quest's completion is npcUtil.giveReward, which reads fameArea as a table (fameArea.fame_area), not a number", V(3));
        Check("p9 hidden validator:Portal Charm validates", "", string.Join(" | ", QuestScriptValidator.Validate(new XI.QuestContent.HiddenQuests.PortalCharm().Compile())));
    }

    // B1. the fake's hidden mode: Status, Begin and DelCurrent throw; Complete records and sets no status
    static void RunP9HiddenFake()
    {
        var f = new Fake { Hidden = true };
        IQuestContext q = f;
        string Threw(Action a)
        {
            try { a(); return "no throw"; } catch (Exception e) { return e.Message; }
        }
        Check("p9 hidden fake:Status throws", "a hidden quest has no status (HiddenQuest:new has no quest log or id)", Threw(() => _ = q.Quest.Status));
        Check("p9 hidden fake:Begin and DelCurrent throw", "a hidden quest cannot begin (HiddenQuest:new has no quest log or id)|" +
              "a hidden quest cannot be dropped (HiddenQuest:new has no quest log or id)", Threw(() => q.Quest.Begin()) + "|" + Threw(() => q.Quest.DelCurrent()));
        bool done = q.Quest.Complete();
        f.Hidden = false;
        Check("p9 hidden fake:Complete records and leaves no status", "True|Quest.Complete()|Available", $"{done}|{string.Join(";", f.Calls)}|{f.Status}");
    }

    // B2. Portal Charm on the fake: the check (Windurst, rank 3), each handler's arm
    static void RunP9PortalCharm()
    {
        var def = new XI.QuestContent.HiddenQuests.PortalCharm().Compile();
        var st = def.States[0];
        Fake F(int nation, int rank)
        {
            var f = new Fake { Hidden = true, NationId = nation, Zone = XI.QuestContent.Zone.HeavensTower };
            f.Ranks[nation] = rank;
            return f;
        }
        Check("p9 portal charm:the check: Windurst rank 3 holds; rank 2 or another nation does not", "True|False|False",
              $"{st.IsMatch(QuestStatus.Available, 0, F(2, 3))}|{st.IsMatch(QuestStatus.Available, 0, F(2, 2))}|{st.IsMatch(QuestStatus.Available, 0, F(1, 3))}");
        // the nation term on its own: a Bastok citizen holding Windurst rank 3 (a char that changed nation at Mythily keeps
        // its Windurst rank; the golden hiddenQuests.portal_charm#nation traces the same case live)
        var moved = F(1, 3);
        moved.Ranks[2] = 3;
        Check("p9 portal charm:the check: Windurst rank 3 but a Bastok citizen does not hold", "False",
              st.IsMatch(QuestStatus.Available, 0, moved).ToString());
        QuestHandlerDecl H(QuestHandlerKind k, int id = 0) => st.Handlers.Single(h => h.Kind == k && (id == 0 || h.Id == id));
        string Reply(QuestHandlerDecl h, Fake f) => h.Reply?.Invoke(f) is { } r ? QuestContextFake.Describe(r) : "nil";
        var trade = F(2, 3);
        trade.TradeItems[XI.QuestContent.Item.Rolanberry] = 1;
        trade.TradeSlots.Add(XI.QuestContent.Item.Rolanberry);
        trade.TradeResult = true;
        string before = Reply(H(QuestHandlerKind.OnTrade), trade);
        trade.CompletedMissions.Add($"{(int)XI.QuestContent.MissionLog.Windurst}/{XI.QuestContent.Mission.Windurst.WrittenInTheStars}");
        string after = Reply(H(QuestHandlerKind.OnTrade), trade);
        trade.KeyItems[XI.QuestContent.KeyItem.PortalCharm] = true;
        string held = Reply(H(QuestHandlerKind.OnTrade), trade);
        Check("p9 portal charm:the trade: 292 before the mission, 291 after, nil with the charm held",
              "ProgressEvent(292)|ProgressEvent(291)|nil", $"{before}|{after}|{held}");
        var talk = F(2, 3);
        string t0 = Reply(H(QuestHandlerKind.OnTrigger), talk);
        talk.Step = 1;
        string t1 = Reply(H(QuestHandlerKind.OnTrigger), talk);
        talk.CompletedMissions.Add($"{(int)XI.QuestContent.MissionLog.Windurst}/{XI.QuestContent.Mission.Windurst.WrittenInTheStars}");
        string t2 = Reply(H(QuestHandlerKind.OnTrigger), talk);
        Check("p9 portal charm:the trigger: nil at Prog 0, nil at Prog 1 before the mission, 293 after", "nil|nil|ProgressEvent(293)", $"{t0}|{t1}|{t2}");
        var fin = F(2, 3);
        H(QuestHandlerKind.OnEventFinish, 292).Run(fin);
        H(QuestHandlerKind.OnEventFinish, 291).Run(fin);
        fin.CompleteResult = false;
        H(QuestHandlerKind.OnEventFinish, 291).Run(fin);
        H(QuestHandlerKind.OnEventFinish, 293).Run(fin);
        Check("p9 portal charm:the finishes: 292 confirms and sets Prog 1; 291 completes then confirms (not when it fails); 293 completes",
              "Trade.Confirm();Quest.SetStep(1);Quest.Complete();Trade.Confirm();Quest.Complete();Quest.Complete()", string.Join(";", fin.Calls));
    }

    // ---- Part C: onTriggerAreaLeave (QuestHandlerKind.OnTriggerAreaLeave: interaction_lookup's onTriggerAreaLeave[id]) ----

    // the builder: a state-level handler of the zone and the area id, a run or a reply, as OnTriggerAreaEnter
    static void RunP9TriggerAreaLeave()
    {
        var d = new BuiltQuest(b => b.State(QuestStatus.Accepted)
            .Npc(261, "Ergon_Locus").OnTrigger(QuestReply.Event(1))
            .OnTriggerAreaEnter(261, 1, q => q.Quest.SetLocalVar("LocusArea", 1))
            .OnTriggerAreaLeave(261, 1, q =>
            {
                q.Quest.SetLocalVar("LocusArea", 0);
                q.Quest.SetLocalVar("RestUntil", 0);
            })
            .OnTriggerAreaLeave(261, 2, q => q.TriggerAreaId == 2 ? QuestReply.ProgressEvent(14) : null)).Compile();
        var leaves = d.States[0].Handlers.Where(x => x.Kind == QuestHandlerKind.OnTriggerAreaLeave).ToList();
        Check("p9 leave:state-level handlers of the zone and the area id (chained from an NPC builder: the state's), a run and a reply",
              "261/-/1/False/True/False,261/-/2/True/False/False",
              string.Join(",", leaves.Select(h => $"{h.Zone}/{h.Entity ?? "-"}/{h.Id}/{h.Reply != null}/{h.Run != null}/{h.Fixed != null}")));
        // a mutant's wrong kind leaves fewer: the checks below fail, not the suite
        if (leaves.Count != 2) return;
        var f = new Fake();
        f.QuestLocalVars["LocusArea"] = 1;
        f.QuestLocalVars["RestUntil"] = 99;
        leaves[0].Run(f);
        Check("p9 leave:its run in the fake (Dances with Luopans' onLocusAreaLeave): LocusArea and RestUntil 0", "0/0",
              $"{f.QuestLocalVars["LocusArea"]}/{f.QuestLocalVars["RestUntil"]}");
        var r = new Fake { TriggerAreaId = 2 };
        var other = new Fake { TriggerAreaId = 3 };
        Check("p9 leave:a reply reads the area's id (q.TriggerAreaId)", "ProgressEvent(14)|nil",
              $"{QuestContextFake.Describe(leaves[1].Reply(r))}|{(leaves[1].Reply(other) is { } o ? QuestContextFake.Describe(o) : "nil")}");
        Check("p9 leave:a null run or reply is refused", "run|reply",
              $"{P5BuildThrows(b => b.State(QuestStatus.Accepted).OnTriggerAreaLeave(261, 1, (Action<IQuestContext>)null))}|" +
              P5BuildThrows(b => b.State(QuestStatus.Accepted).OnTriggerAreaLeave(261, 1, (Func<IQuestContext, QuestReply>)null)));
        Check("p9 leave:an enter and a leave of one area in a state validate clean (Steps declared)", "",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Accepted)
                  .OnTriggerAreaEnter(261, 1, q => { }).OnTriggerAreaLeave(261, 1, q => { }).OnTriggerAreaLeave(261, 2, q => { })).Compile())));
        Check("p9 leave:one area's leave declared twice in one state", "OnTriggerAreaLeave 1 declared twice in one state",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Accepted)
                  .OnTriggerAreaLeave(261, 1, q => { }).OnTriggerAreaLeave(261, 1, q => { })).Compile())));
        Check("p9 leave:in a call-only quest it is refused (no Zone.lua calls it through xi.cs)",
              "OnTriggerAreaLeave 1: a called handler is OnTrigger, OnTrade or OnMobDeath on an entity, OnEventFinish, OnEventUpdate or OnZoneIn state-level",
              string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called()
                  .OnTrigger(QuestReply.Event(1)).OnTriggerAreaLeave(235, 1, q => { })).Compile())));
        Check("p9 leave:its trigger name carries the area id (a handler label's: onTriggerAreaLeave:2)", "onTriggerAreaLeave:2|onTriggerAreaEnter:2",
              $"{QuestOverrides.Trigger(QuestHandlerKind.OnTriggerAreaLeave, 2)}|{QuestOverrides.Trigger(QuestHandlerKind.OnTriggerAreaEnter, 2)}");
        Check("p9 leave:the kind is appended (the enum's earlier values unchanged: OnZoneOut 8, OnTriggerAreaLeave 9)", "8/9",
              $"{(int)QuestHandlerKind.OnZoneOut}/{(int)QuestHandlerKind.OnTriggerAreaLeave}");
    }

    // the base of the leave module checks: state 1 (Accepted) with area 1's leave (LocusArea 0)
    sealed class P9LeaveQuest : QuestScript
    {
        public P9LeaveQuest() : base(QuestLog.Jeuno, "P4_TEST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted)
                .OnTriggerAreaLeave(P4Zone, 1, q => q.Quest.SetLocalVar("LocusArea", 0));
        }
    }

    static QuestOverrideResult P9LeaveApply(Action<QuestModuleBuilder> build, List<string> baseRuns = null) =>
        QuestOverrides.Apply(new P9LeaveQuest().Compile(), new P4Module("p9_test_module", build).Compile(new Fake()), P4Key,
            baseRuns == null ? null : h => baseRuns.Add($"{h.Kind}:{h.Entity ?? h.Id.ToString()}"), null);

    // a module wraps, replaces or adds an OnTriggerAreaLeave (QuestOverrides: the trigger onTriggerAreaLeave:<id>, a run or a
    // reply, state-level)
    static void RunP9TriggerAreaLeaveModules()
    {
        // the leave of that area, or a no-op one when the module was refused (a check fails, not the suite)
        QuestHandlerDecl Leave(QuestOverrideResult res, int id) =>
            res.Definition?.States[0].Handlers.SingleOrDefault(x => x.Kind == QuestHandlerKind.OnTriggerAreaLeave && x.Id == id) ??
            new BuiltQuest(b => b.State(QuestStatus.Accepted).OnTriggerAreaLeave(P4Zone, id, q => { })).Compile().States[0].Handlers[0];
        var baseRuns = new List<string>();
        var wrapped = P9LeaveApply(m => m.Quest(P4Key).State(1).WrapOnTriggerAreaLeave(P4Zone, 1, (q, baseRun) =>
        {
            baseRun(q);
            q.Quest.SetVar("Left", 1);
        }), baseRuns);
        var f = new Fake();
        f.QuestLocalVars["LocusArea"] = 1;
        Leave(wrapped, 1).Run(f);
        Check("p9 leave modules:a wrap runs the base (LocusArea 0) and its own (Left 1)", "|0/1/OnTriggerAreaLeave:1",
              $"{string.Join(" | ", wrapped.Problems)}|{f.QuestLocalVars["LocusArea"]}/{f.Var("Left")}/{string.Join(",", baseRuns)}");
        var replaced = P9LeaveApply(m => m.Quest(P4Key).State(1).ReplaceOnTriggerAreaLeave(P4Zone, 1, q => q.Quest.SetVar("Left", 2)));
        f = new Fake();
        f.QuestLocalVars["LocusArea"] = 1;
        Leave(replaced, 1).Run(f);
        Check("p9 leave modules:a replace runs its own only (LocusArea kept)", "|1/2",
              $"{string.Join(" | ", replaced.Problems)}|{f.QuestLocalVars["LocusArea"]}/{f.Var("Left")}");
        var added = P9LeaveApply(m => m.Quest(P4Key).State(1).AddOnTriggerAreaLeave(P4Zone, 2, q => { }).AddOnTriggerAreaLeave(P4Zone, 3, q => QuestReply.Event(5)));
        Check("p9 leave modules:added ones are the module's, state-level handlers of the zone (a run, a reply)", $"|{P4Zone}/-/2/True,{P4Zone}/-/3/False",
              $"{string.Join(" | ", added.Problems)}|" + string.Join(",", (added.Definition?.States[0].Handlers ?? new List<QuestHandlerDecl>()).Where(x => x.Kind == QuestHandlerKind.OnTriggerAreaLeave && x.Module != null)
                  .Select(x => $"{x.Zone}/{x.Entity ?? "-"}/{x.Id}/{x.Run != null}")));
        var missing = P9LeaveApply(m => m.Quest(P4Key).State(1).WrapOnTriggerAreaLeave(P4Zone, 2, (q, baseRun) => baseRun(q)));
        Check("p9 leave modules:a wrap of a leave the state does not have is refused (an enter of the area is not it)",
              $"p9_test_module: jeuno.p4_test: has no handler 1/-/onTriggerAreaLeave:2 in zone {P4Zone} to wrap", string.Join(" | ", missing.Problems));
    }

    // ---- Part D8: the fake as the live binding ----

    // trade:hasItemQty(item, qty) is exact (lua_trade_container.cpp: ==; ZoneHost.Trade.cs), and player:checkDistance is
    // measured in float (the engine's distance), so a fake check passes where live passes, not more
    static void RunP9FakeParity()
    {
        var f = new Fake();
        f.TradeItems[4096] = 2;
        Check("p9 fake:HasItemQty is exact: 2 offered holds for 2, not for 1 or 3", "True/False/False",
              $"{f.HasItemQty(4096, 2)}/{f.HasItemQty(4096, 1)}/{f.HasItemQty(4096, 3)}");
        Check("p9 fake:an item not offered holds for no quantity", "False", f.HasItemQty(4097, 1).ToString());
        var d = new Fake { XPos = 0, YPos = 0, ZPos = 0 };
        Check("p9 fake:DistanceTo in float: 1.00000001 yalms off measures 1 (within a 1-yalm check, as live)", "True/1",
              $"{d.DistanceTo(1.00000001, 0, 0) <= 1.0}/{d.DistanceTo(1.00000001, 0, 0)}");
        Check("p9 fake:DistanceTo: x, y and z all count", "5", d.DistanceTo(3, 4, 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
