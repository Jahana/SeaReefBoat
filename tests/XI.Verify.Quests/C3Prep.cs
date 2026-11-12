// XI.Verify.Quests -- quest rework C3 Prep (docs/superpowers/specs/2026-11-06-quest-rework-c3-lanes-design.md, the
// Prep section): the platform carries, over fakes.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // a test mission on the free RoV key with the reward given, at Naji
    sealed class C3RewardMission : MissionScript
    {
        readonly MissionReward reward_;

        public C3RewardMission(MissionReward reward) : base(MissionLog.RoV, "THE_ENDLESS_SKY") => reward_ = reward;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(reward_);
            b.State(status: 1).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001));
        }
    }

    internal static void RunC3Prep()
    {
        Console.WriteLine("\n== c3 prep: the platform carries ==");
        RunC3Rewards();
        RunC3Callbacks();
        RunC3Texts();
        RunC3Groups();
        RunC3Proxy();
        RunC3MissionPatches();
        RunC3Keys();
        RunC3Framework();
        Check("c3 registration: MissionScript.AreaName: each log's lower-case name, null for a number the enum lacks",
              string.Join(",", Enum.GetValues<MissionLog>().Select(l => l.ToString().ToLowerInvariant())) + "|True",
              string.Join(",", Enum.GetValues<MissionLog>().Select(l => MissionScript.AreaName(l))) + "|" +
              (MissionScript.AreaName((MissionLog)200) == null));
    }

    // a battlefield whose Data is a new object at each read
    sealed class C3FreshDataBattlefield : BattlefieldScript
    {
        public C3FreshDataBattlefield() : base("C2_SUBCLASS") { }

        public override BattlefieldData Data => new BattlefieldData
        {
            ZoneId = 168, BattlefieldId = 197, MaxPlayers = 6, TimeLimit = 600, EntryNpcs = new[] { "C2_Entrance" },
        };
    }

    static void RunC3Proxy()
    {
        string Problems(BattlefieldData data) => string.Join(" | ",
            BattlefieldScriptValidator.Validate(new C2SubclassBattlefield(data)));
        BattlefieldEventHandler nothing = (q, csid, option) => { };
        Check("c3 proxy: the value shapes pass", "", Problems(new BattlefieldData
        {
            ProxyFields =
            {
                ["positions"] = new object[]
                {
                    new object[] { new Dictionary<string, object> { ["x"] = 1.5, ["n"] = 2 } },
                },
                ["name"] = "a", ["on"] = true,
            },
        }));
        Check("c3 proxy: a value of another type", "proxy field bad: a DateTime value",
              Problems(new BattlefieldData { ProxyFields = { ["bad"] = DateTime.MinValue } }));
        Check("c3 proxy: a null inside an array", "proxy field bad: a null value",
              Problems(new BattlefieldData { ProxyFields = { ["bad"] = new object[] { null } } }));
        Check("c3 proxy: a framework's event id",
              "onEventFinish 32001 is the framework's | onEventUpdate 32003 is the framework's",
              Problems(new BattlefieldData
              {
                  OnEventFinish = { [32001] = nothing }, OnEventUpdate = { [32003] = nothing },
              }));
        var s = new C2SubclassBattlefield(new BattlefieldData
        {
            OnEventUpdate = { [1] = nothing }, OnEventFinish = { [10] = nothing },
        });
        Check("c3 proxy: a section handler is a coverage label", "True True",
              $"{s.CoverageLabels().Contains("section:onEventUpdate:1")} " +
              s.CoverageLabels().Contains("section:onEventFinish:10"));
    }

    static void RunC3Groups()
    {
        // royal_jelly.lua:23-26: the first essential group does not start spawned
        var d = new BattlefieldData
        {
            EssentialMobs =
            {
                new BattlefieldEssentialMobs { Mobs = new[] { "Queen_Jelly" }, Spawned = false },
                new[] { "Princess_Jelly" },
            },
            Groups = { new BattlefieldGroup { Mobs = new[] { "Add" }, SuperlinkGroup = 2 } },
        };
        var groups = BattlefieldFramework.AddEssentialMobs(d);
        Check("c3 group: content.groups first, then the essential ones in call order, each with its spawned",
              "Add/-/0 Queen_Jelly/False/1 Princess_Jelly/-/2",
              string.Join(" ", groups.Select(g => $"{g.Mobs[0]}/{g.Spawned?.ToString() ?? "-"}/{g.EssentialIndex}")));
        Check("c3 group: an essential entry reads as its names", "Queen_Jelly",
              string.Join(",", d.EssentialMobs[0]));

        string Problems(BattlefieldData data) => string.Join(" | ",
            BattlefieldScriptValidator.Validate(new C2SubclassBattlefield(data)));
        Check("c3 group: the validator: essential mobs with no names",
              "essential mobs 1: no mob names",
              Problems(new BattlefieldData { EssentialMobs = { new BattlefieldEssentialMobs() } }));
        Check("c3 group: the validator: superlink and superlinkGroup together pass (C++ only warns)", "",
              Problems(new BattlefieldData
              {
                  Groups = { new BattlefieldGroup { Mobs = new[] { "A" }, Superlink = true, SuperlinkGroup = 1 } },
              }));
        Check("c3 group: the validator: Data read twice is one instance",
              "Data is not a stored instance: each read built a new one",
              string.Join(" | ", BattlefieldScriptValidator.Validate(new C3FreshDataBattlefield())));

        var withSetup = new C2SubclassBattlefield(new BattlefieldData
        {
            Groups = { new BattlefieldGroup { Mobs = new[] { "Zeid" }, Setup = (q, mobs) => { } } },
        });
        Check("c3 group: a setup callback is a coverage label", "True",
              withSetup.CoverageLabels().Contains("group:1:setup").ToString());
    }

    static void RunC3Rewards()
    {
        var zero = new C3RewardMission(new MissionReward { Gil = 0, Title = 0 }).Compile().Reward;
        Check("c3 reward: 0 is a value, an unset field is null", "0 0 True True",
              $"{zero.Gil} {zero.Title} {zero.Item == null} {zero.KeyItem == null}");

        var items = new MissionRewardItem[] { new(QC.Item.ImperialGoldPiece, 5), QC.Item.ImperialGoldPiece };
        var keyItems = new[] { QC.KeyItem.SkyBluePome, QC.KeyItem.SunYellowPome };
        var param = new QuestItemParams { Silent = true };
        var declared = new MissionReward { Items = items, KeyItems = keyItems, ItemParams = param };
        var reward = new C3RewardMission(declared).Compile().Reward;
        items[0] = 1;
        keyItems[0] = 1;
        param.Silent = false;
        Check("c3 reward: a copy: changing the arrays and the params after Reward() changes nothing",
              $"{QC.Item.ImperialGoldPiece} 5 {QC.KeyItem.SkyBluePome} True",
              $"{reward.Items[0].Id} {reward.Items[0].Quantity} {reward.KeyItems[0]} {reward.ItemParams.Silent}");

        string Problems(MissionReward r) =>
            string.Join(" | ", QuestScriptValidator.Validate(new C3RewardMission(r).Compile()));
        Check("c3 reward: the validator: clean", "",
              Problems(new MissionReward { Items = new MissionRewardItem[] { 1 }, ItemParams = param }));
        Check("c3 reward: the validator: Item and Items together",
              "reward: Item and Items are one Lua field (reward.item)",
              Problems(new MissionReward { Item = 1, Items = new MissionRewardItem[] { 1 } }));
        Check("c3 reward: the validator: KeyItem and KeyItems together",
              "reward: KeyItem and KeyItems are one Lua field (reward.keyItem)",
              Problems(new MissionReward { KeyItem = 1, KeyItems = new[] { 1 } }));
        Check("c3 reward: the validator: an empty table",
              "reward: an empty Items | reward: an empty KeyItems",
              Problems(new MissionReward { Items = new MissionRewardItem[0], KeyItems = new int[0] }));
        Check("c3 reward: the validator: a quantity below 1", "reward: item 7 has a quantity below 1",
              Problems(new MissionReward { Items = new[] { new MissionRewardItem(7, 0) } }));
        Check("c3 reward: the validator: itemParams with no item",
              "reward.itemParams with no item: npcUtil.completeMission passes them to npcUtil.giveItem only with " +
              "reward.item",
              Problems(new MissionReward { ItemParams = param }));
    }

    // a mission with a callback and an export; bad: a callback name the validator refuses and an export of a callback
    // of another definition
    sealed class C3CallbackMission : MissionScript
    {
        readonly bool bad_;

        public C3CallbackMission(bool bad = false) : base(MissionLog.RoV, "THE_ENDLESS_SKY") => bad_ = bad;

        protected override void Build(MissionScriptBuilder b)
        {
            var cb = b.Callback(bad_ ? "1bad" : "wake", q => q.Mission.SetStatus(2));
            b.Export("xi.c3prep.wake", cb);
            if (bad_) b.Export("xi.c3prep.other", new MissionScriptBuilder().Callback("other", q => { }));
            b.State().Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1));
        }
    }

    static void RunC3Callbacks()
    {
        var def = new C3CallbackMission().Compile();
        Check("c3 callback: a mission's callback and export are clean, and on the core", "|wake|xi.c3prep.wake",
              $"{string.Join(" | ", QuestScriptValidator.Validate(def))}|{def.Core.Callbacks[0].Name}|" +
              def.Core.Exports[0].Global);
        var f = new Fake { ThisMission = (MissionLog.RoV, QC.Mission.Rov.TheEndlessSky) };
        def.Core.Callbacks[0].Run(f);
        Check("c3 callback: its run has q.Mission", "Mission.SetStatus(2)", string.Join(",", f.Calls));
        Check("c3 callback: the validator's callback rules hold for a mission",
              "callback '1bad': a callback's name is [A-Za-z][A-Za-z0-9_]* (its label 0/-/callback:<name>) | " +
              "export xi.c3prep.other: its callback other is not one of the quest's (b.Callback)",
              string.Join(" | ", QuestScriptValidator.Validate(new C3CallbackMission(bad: true).Compile())));
    }

    static void RunC3Texts()
    {
        var s = new C2SubclassBattlefield(new BattlefieldData());
        var (q, p) = C2BContext(s, "C2_Entrance");
        var engine = (BattlefieldEngineFake)q.Engine;
        engine.Texts["168:C3_TEXT"] = 7000;
        IBattlefieldContext ctx = q;
        Check("c3 text: the id of the battlefield's zone", "7001",
              (ctx.Text.Id(new ZoneText(168, "C3_TEXT")) + 1).ToString());

        string Throws(Func<object> read)
        {
            try
            {
                return read()?.ToString() ?? "null";
            }
            catch (InvalidOperationException e)
            {
                return "throws: " + e.Message;
            }
        }
        Check("c3 text: a text of another zone throws", "throws: text 237:C3_TEXT is not of the battlefield's zone 168",
              Throws(() => ctx.Text.Id(new ZoneText(237, "C3_TEXT"))));
        Check("c3 text: a text the IDs table lacks throws", "throws: zone 168 has no text NO_SUCH_TEXT",
              Throws(() => ctx.Text.Id(new ZoneText(168, "NO_SUCH_TEXT"))));
        var zoneHandler = new BattlefieldContextFake(null) { Engine = engine };
        Check("c3 text: a zone handler with no battlefield reads any zone", "7000",
              ((IBattlefieldContext)zoneHandler).Text.Id(new ZoneText(168, "C3_TEXT")).ToString());

        var mob = new BattlefieldMobFake { Id = 55 };
        mob.MessageText(7001);
        p.ShowText(mob, 7002);
        Check("c3 text: mob:messageText(mob, id) and player:showText(mob, id) are recorded",
              "Mob.MessageText(7001)|Player.ShowText(55,7002)",
              $"{string.Join(",", mob.Calls)}|{string.Join(",", p.Calls)}");
    }

    // status 0 and status 1 at Naji, each with its finish, as the shape of a mission mission_adjustments.lua patches
    sealed class C3PatchedMission : MissionScript
    {
        public C3PatchedMission() : base(MissionLog.RoV, "THE_ENDLESS_SKY") { }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true, status: 0).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1000))
                .OnEventFinish(QC.Zone.Metalworks, 1000, q => q.Mission.SetStatus(1));
            b.State(current: true, status: 1).Npc(QC.Zone.Metalworks, "Naji").OnTrigger(QuestReply.Event(1001));
        }
    }

    sealed class C3PatchModule : QuestModule
    {
        readonly Action<QuestModuleBuilder> build_;

        public C3PatchModule(Action<QuestModuleBuilder> build) : base("c3_patch", "modules/custom/lua/c3_patch.lua") =>
            build_ = build;

        protected override void Build(QuestModuleBuilder m) => build_(m);
    }

    static void RunC3MissionPatches()
    {
        string key = QC.MissionKey.Rov.TheEndlessSky;
        var mission = new C3PatchedMission().Compile();
        QuestOverrideResult Apply(Action<QuestModuleBuilder> build) =>
            QuestOverrides.Apply(mission.Core, new C3PatchModule(build).Compile(null), key);
        var f = new Fake { ThisMission = (MissionLog.RoV, QC.Mission.Rov.TheEndlessSky) };
        f.CurrentMissions[(int)MissionLog.RoV] = QC.Mission.Rov.TheEndlessSky;

        var wrapped = Apply(m => m.Mission(key).State(1)
            .WrapCheck((q, b) => b(q) && q.Mission.Var("Gate") == 1)).Definition;
        Check("c3 patch: a wrapped check's base is the state's mission match, and the module's guard holds",
              "False True False",
              $"{wrapped.States[0].IsMissionMatch(f)} {Gated(1)} {Gated(1, status: 1)}");
        bool Gated(int gate, int status = 0)
        {
            var g = new Fake { ThisMission = (MissionLog.RoV, QC.Mission.Rov.TheEndlessSky) };
            g.CurrentMissions[(int)MissionLog.RoV] = QC.Mission.Rov.TheEndlessSky;
            g.MissionStatuses[(int)MissionLog.RoV] = status;
            g.MissionVars[$"{(int)MissionLog.RoV}/{QC.Mission.Rov.TheEndlessSky}/Gate"] = gate;
            return wrapped.States[0].IsMissionMatch(g);
        }
        Check("c3 patch: the patched state keeps no quest match, and the validator takes it", "0|",
              $"{wrapped.States[0].Matches.Count}|" +
              string.Join(" | ", QuestScriptValidator.Validate(mission.WithCore(wrapped))));
        var replaced = Apply(m => m.Mission(key).State(2).ReplaceCheck(q => true)).Definition;
        Check("c3 patch: a replaced check ignores the status", "True", replaced.States[1].IsMissionMatch(f).ToString());
        Check("c3 patch: the states the module leaves keep their own match", "True False",
              $"{replaced.States[0].IsMissionMatch(f)} {wrapped.States[1].IsMissionMatch(f)}");

        var handler = Apply(m => m.Mission(key).State(1)
            .ReplaceOnEventFinish(QC.Zone.Metalworks, 1000, q => q.Mission.SetVar("Timer", 1, 5000))).Definition;
        handler.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish).Run(f);
        Check("c3 patch: a replaced finish, the var with its expiry", "Mission.SetVar(Timer,1,5000)",
              string.Join(",", f.Calls));

        string Refused(Action<QuestModuleBuilder> build) => string.Join(" | ", Apply(build).Problems);
        const string Only = "a mission takes handler and check overrides only";
        Check("c3 patch: Reward, RemoveState and AddStates are refused on a mission",
              $"c3_patch: {key}: {Only}|c3_patch: {key}: {Only}|c3_patch: {key}: {Only}",
              Refused(m => m.Mission(key).Reward(new QuestReward())) + "|" +
              Refused(m => m.Mission(key).RemoveState(1)) + "|" +
              Refused(m => m.Mission(key).AddStates(b => { })));

        string Thrown(Action run)
        {
            try
            {
                run();
                return "no throw";
            }
            catch (ArgumentException e)
            {
                return e.ParamName;
            }
        }
        Check("c3 patch: Mission takes a mission key, Quest a quest's", "key key",
              Thrown(() => new C3PatchModule(m => m.Mission("bastok.groceries")).Compile(null)) + " " +
              Thrown(() => new C3PatchModule(m => m.Quest(key)).Compile(null)));

        var gates = new Fake { SoaGateOpen = true };
        gates.WotgReqsMet.Add(3);
        Check("c3 patch: the helper reads on the fake", "True True False",
              $"{gates.SoaImprimaturGate(10)} {gates.WotgMeetsMissionReqs(3)} {gates.WotgMeetsMissionReqs(4)}");
    }

    // the literal constants of a generated class and its nested classes
    static int ConstCount(Type type) =>
        type.GetFields().Count(f => f.IsLiteral) + type.GetNestedTypes().Sum(t => ConstCount(t));

    static void RunC3Keys()
    {
        var missionKeys = ConstStrings(typeof(QC.MissionKey));
        var battlefieldKeys = ConstStrings(typeof(QC.BattlefieldKey));
        Check("c3 keys: the mission keys are distinct", missionKeys.Count.ToString(),
              missionKeys.Distinct().Count().ToString());
        Check("c3 keys: the battlefield keys are distinct", battlefieldKeys.Count.ToString(),
              battlefieldKeys.Distinct().Count().ToString());
        Check("c3 keys: one mission key per mission constant", ConstCount(typeof(QC.Mission)).ToString(),
              missionKeys.Count.ToString());
        Check("c3 keys: one battlefield key per battlefield constant", ConstCount(typeof(QC.Battlefield)).ToString(),
              battlefieldKeys.Count.ToString());
    }

    // a test battlefield over the data, its entry NPC the npc, the char 7, every text the framework reads known: 100
    // and up in the order of C3TextNames
    static readonly string[] C3TextNames =
    {
        "ENTERING_THE_BATTLEFIELD_FOR", "MEMBERS_OF_YOUR_PARTY", "TIME_LIMIT_FOR_THIS_BATTLE_IS",
        "CLEARED_BUT_MEMBERS_ENGAGED", "PARTY_MEMBERS_ARE_ENGAGED", "NO_BATTLEFIELD_ENTRY",
        "THE_PARTY_WILL_BE_REMOVED", "TIME_IN_THE_BATTLEFIELD_IS_UP", "PARTY_MEMBERS_HAVE_FALLEN", "C3_KI", "C3_DEL",
        "C3_WEAR",
    };

    static (BattlefieldContextFake Q, BattlefieldPlayerFake P, BattlefieldEngineFake E, C2SubclassBattlefield S,
            BattlefieldViewFake B) C3Bf(BattlefieldData data = null, Dictionary<int, int> trade = null)
    {
        var s = new C2SubclassBattlefield(data ?? new BattlefieldData());
        var (q, p) = C2BContext(s, "C2_Entrance", trade);
        p.CharId = 7;
        var e = (BattlefieldEngineFake)q.Engine;
        for (int i = 0; i < C3TextNames.Length; i++) e.Texts[$"168:{C3TextNames[i]}"] = 100 + i;
        var bf = new BattlefieldViewFake { Id = 197, ZoneId = 168, InitiatorValue = (7, "Me") };
        q.Battlefield = bf;
        return (q, p, e, s, bf);
    }

    static string C3Calls(BattlefieldPlayerFake p) => string.Join(",", p.Calls);

    static void RunC3Framework()
    {
        RunC3RegisterResults();
        RunC3Redirect();
        RunC3EntryUpdate();
        RunC3Enter();
        RunC3Initialize();
        RunC3LeaveAndWipe();
        RunC3EntryTrade();
        RunC3EntryTrigger();
        RunC3Ticks();
        RunC3Loot();
    }

    // battlefield.lua:825's reply to each returnCode of registerBattlefield
    static void RunC3RegisterResults()
    {
        string Register(int result)
        {
            var (q, p, _, s, _) = C3Bf();
            p.RegisterResult = result;
            int r = s.OnEntryEventUpdate(q, 32000, 1);
            return $"{r} {C3Calls(p)}";
        }
        const string Reg = "Player.RegisterBattlefield(197,1,7),";
        const string NoPos = "Player.SetLocalVar(noPosUpdate,1)";
        Check("c3 framework: REQS_NOT_MET: the clearance deleted, then the reply",
              $"0 {Reg}Player.DelStatusEffect(254),Player.UpdateEvent(5),{NoPos}",
              Register(BattlefieldConst.ReqsNotMet));
        Check("c3 framework: INCREMENT_REQUEST replies WAIT", $"0 {Reg}Player.UpdateEvent(1),{NoPos}",
              Register(BattlefieldConst.IncrementRequest));
        Check("c3 framework: LOCKED and BATTLEFIELD_FULL reply as they are",
              $"0 {Reg}Player.UpdateEvent(4),{NoPos}|0 {Reg}Player.UpdateEvent(6),{NoPos}",
              Register(BattlefieldConst.Locked) + "|" + Register(BattlefieldConst.BattlefieldFull));
    }

    // battlefield.lua:795
    static void RunC3Redirect()
    {
        var (q, p, e, s, _) = C3Bf();
        string Redirect(int option) => BattlefieldFramework.RedirectEventUpdate(q, 32000, option)?.ToString() ?? "null";
        Check("c3 framework: redirect: options 0 and 255 answer false, nothing else", "False False ",
              $"{Redirect(0)} {Redirect(255)} {C3Calls(p)}");
        p.SetPlayerLocalVar("[BCNM]MenuOptions", 1);
        p.Calls.Clear();
        Check("c3 framework: redirect: an index not in the menu's mask: REQS_NOT_MET, false",
              "False Player.UpdateEvent(5),Player.SetLocalVar(noPosUpdate,1)",
              $"{Redirect((3 << 4) | 1)} {C3Calls(p)}");
        string noContents;
        try
        {
            noContents = Redirect(1);
        }
        catch (InvalidOperationException ex)
        {
            noContents = ex.Message;
        }
        Check("c3 framework: redirect: an index in the mask in a zone with no contents throws, as the Lua's " +
              "pairs(nil)", "zone 168 has no battlefields", noContents);
        var content = new BattlefieldContentFake(s);
        e.ByZone[168] = new List<IBattlefieldContent> { content };
        Check("c3 framework: redirect: an index in the mask goes to the content of that index, null",
              "null onEntryEventUpdate(32000,1)", $"{Redirect(1)} {string.Join(",", content.Calls)}");
    }

    // battlefield.lua:825's arms before and after registerBattlefield
    static void RunC3EntryUpdate()
    {
        const string NoPos = "Player.SetLocalVar(noPosUpdate,1)";
        var (q, p, _, s, _) = C3Bf();
        Check("c3 framework: entry update: area 0 and an area above the zone's: REQS_NOT_MET with no register",
              $"0 0 Player.UpdateEvent(5),{NoPos},Player.UpdateEvent(5),{NoPos}",
              $"{s.OnEntryEventUpdate(q, 32000, 0)} {s.OnEntryEventUpdate(q, 32000, 4)} {C3Calls(p)}");
        var (aq, ap, _, a, _) = C3Bf(new BattlefieldData { AllowedAreas = new[] { 2 } });
        Check("c3 framework: entry update: an arena allowedAreas leaves out: WAIT with no register",
              $"0 Player.UpdateEvent(1),{NoPos}", $"{a.OnEntryEventUpdate(aq, 32000, 1)} {C3Calls(ap)}");
        string Traded(bool keep)
        {
            var (tq, tp, _, t, tb) = C3Bf(new BattlefieldData
            {
                RequiredItems = new BattlefieldRequiredItems { Items = new[] { 5 }, Keep = keep },
            });
            tp.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
            tp.EnterTarget = tb;
            t.OnEntryEventUpdate(tq, 32000, 1);
            return tp.Calls.Contains("Player.TradeComplete()").ToString();
        }
        Check("c3 framework: entry update: a traded item with no wearMessage is taken; with keep it is not",
              "True False", $"{Traded(false)} {Traded(true)}");
    }

    // battlefield.lua:1094
    static void RunC3Enter()
    {
        const string Tail = "Player.MessageSpecial(100,0,0),Player.MessageSpecial(101,0,0,0,6)," +
                            "Player.MessageSpecial(102,0,0,0,10)";
        string Enter(BattlefieldRequiredKeyItems kis, int charId = 7, Action<BattlefieldPlayerFake> setup = null)
        {
            var (q, p, _, _, _) = C3Bf(new BattlefieldData { RequiredKeyItems = kis });
            p.CharId = charId;
            setup?.Invoke(p);
            q.Framework.OnBattlefieldEnter();
            return C3Calls(p);
        }
        var message = new ZoneText(168, "C3_KI");
        var delete = new ZoneText(168, "C3_DEL");
        string First = "Player.SetLocalVar(battlefieldID,197),";
        Check("c3 framework: enter: one of several key items: the held one deleted, its message, the delete message",
              $"{First}Player.DelKeyItem(2),Player.MessageSpecial(109,2),Player.MessageSpecial(110,2),{Tail}",
              Enter(new BattlefieldRequiredKeyItems
              {
                  Items = { BattlefieldKeyItem.AnyOf(1, 2) }, Message = message, DeleteMessage = delete,
              }, setup: p => p.KeyItems[2] = true));
        Check("c3 framework: enter: keep: no delete, no delete message",
              $"{First}Player.MessageSpecial(109,3),{Tail}",
              Enter(new BattlefieldRequiredKeyItems
              {
                  Items = { 3 }, Keep = true, Message = message, DeleteMessage = delete,
              }));
        Check("c3 framework: enter: message args in place of the ids",
              $"{First}Player.DelKeyItem(3),Player.MessageSpecial(109,8,9),{Tail}",
              Enter(new BattlefieldRequiredKeyItems
              {
                  Items = { 3 }, Message = message, MessageArgs = new[] { 8, 9 },
              }));
        Check("c3 framework: enter: onlyInitiator: another member keeps the key item", $"{First}{Tail}",
              Enter(new BattlefieldRequiredKeyItems { Items = { 3 }, OnlyInitiator = true }, charId: 8));

        var (wq, wp, we, _, _) = C3Bf(new BattlefieldData
        {
            RequiredItems = new BattlefieldRequiredItems
            {
                Items = new[] { 5 }, WearMessage = new ZoneText(168, "C3_WEAR"),
            },
        });
        we.Uses[5] = 3;
        wp.Items.Add(5);
        wq.Framework.OnBattlefieldEnter();
        Check("c3 framework: enter: the wear of an item of 3 uses: the item, uses left + 1, uses left",
              $"{First}Player.IncrementItemWear(5),Player.MessageSpecial(111,5,3,2),{Tail}", C3Calls(wp));

        var (lq, lp, le, ls, _) = C3Bf();
        ls.Data.MaxPlayers = 18;
        lq.Framework.OnBattlefieldEnter();
        Check("c3 framework: enter: an alliance's fight with no MEMBERS_OF_YOUR_ALLIANCE: the party text", "True",
              lp.Calls.Contains("Player.MessageSpecial(101,0,0,0,18)").ToString());
        le.Texts["168:MEMBERS_OF_YOUR_ALLIANCE"] = 150;
        lp.Calls.Clear();
        lq.Framework.OnBattlefieldEnter();
        Check("c3 framework: enter: with it: the alliance text", "True",
              lp.Calls.Contains("Player.MessageSpecial(150,0,0,0,18)").ToString());

        var (xq, xp, _, _, _) = C3Bf(new BattlefieldData { Experimental = true });
        xq.Framework.OnBattlefieldEnter();
        Check("c3 framework: enter: an experimental battlefield says so", "True",
              xp.Calls.Contains("Player.PrintToPlayer(This battlefield has been marked as experimental.  Enemy " +
                                "levels have increased!,14)").ToString());
    }

    // battlefield.lua:999
    static void RunC3Initialize()
    {
        var (q, _, e, _, bf) = C3Bf(new BattlefieldData { Experimental = true });
        var low = new BattlefieldMobFake { MainLevel = 40 };
        var high = new BattlefieldMobFake { MainLevel = 130 };
        var walker = new BattlefieldMobFake();
        bf.MobList.Add(low);
        bf.MobList.Add(high);
        e.MobsById[9] = walker;
        ((BattlefieldContentFake)q.Self).LuaPaths.Add((9, new[] { 1.0, 2.0, 3.0 }));
        ((BattlefieldContentFake)q.Self).LuaPaths.Add((10, new[] { 4.0 }));
        q.Framework.OnBattlefieldInitialize();
        Check("c3 framework: initialize: the groups added, an experimental one's levels doubled to at most 255, a " +
              "mob's path walked, a mob not there skipped",
              "Battlefield.AddGroups(197,True)|Mob.SetMobLevel(80)|Mob.SetMobLevel(255)|Mob.PathThrough(1,2,3;32)",
              $"{string.Join(",", bf.Calls)}|{string.Join(",", low.Calls)}|{string.Join(",", high.Calls)}|" +
              string.Join(",", walker.Calls));
    }

    // battlefield.lua:1211, 1241 and 1272
    static void RunC3LeaveAndWipe()
    {
        var (q, p, _, _, _) = C3Bf(new BattlefieldData { CleanupDebuffs = true });
        q.Framework.OnBattlefieldLeave(BattlefieldConst.LeaveWon);
        Check("c3 framework: leave: cleanupDebuffs on a win clears the erasable and the waltzable",
              "Player.DelStatusEffectsByFlag(16386)", p.Calls[0]);
        p.Calls.Clear();
        q.Framework.OnBattlefieldLeave(BattlefieldConst.LeaveLost);
        Check("c3 framework: leave: not on a loss", "False",
              p.Calls.Any(c => c.StartsWith("Player.DelStatusEffectsByFlag", StringComparison.Ordinal)).ToString());

        string Wipe(int wipeTime, int inside, bool alive, bool grace = true)
        {
            var (wq, _, _, _, wb) = C3Bf(new BattlefieldData { HasWipeGrace = grace });
            wb.WipeTime = wipeTime;
            wb.TimeInside = inside;
            var player = new BattlefieldPlayerFake { ZoneId = 168, IsAlive = alive };
            wq.Framework.HandleWipe(new List<IBattlefieldPlayer> { player });
            return $"{string.Join(",", wb.Calls)}|{string.Join(",", player.Calls)}";
        }
        Check("c3 framework: wipe: someone alive: nothing", "|", Wipe(0, 100, true));
        Check("c3 framework: wipe: all dead with the grace: the message, the wipe time",
              "Battlefield.SetWipeTime(100)|Player.MessageSpecial(106,0,0,0,3)", Wipe(0, 100, false));
        Check("c3 framework: wipe: all dead with no grace: LOST", "Battlefield.SetStatus(3)|",
              Wipe(0, 100, false, grace: false));
        Check("c3 framework: wipe: past three minutes: LOST", "Battlefield.SetStatus(3)|", Wipe(100, 281, false));
        Check("c3 framework: wipe: at three minutes: not yet", "|", Wipe(100, 280, false));
        Check("c3 framework: wipe: someone raised in time: the wipe cancelled", "Battlefield.SetWipeTime(0)|",
              Wipe(100, 150, true));

        var (xq, xp, _, _, _) = C3Bf(new BattlefieldData { GrantXP = 5, GrantXPLockout = true });
        xp.Now = 10;
        xp.NextJstMidnight = 900;
        xq.Framework.OnEventFinishWin(32001, 0);
        xq.Framework.OnEventFinishWin(32001, 0);
        Check("c3 framework: the win's grantXPLockout on Battlefield: the XP once until the var passes",
              "Player.SetVar(Battlefield[197]XP,900),Player.AddExp(5)", C3Calls(xp));
    }

    // battlefield.lua:643
    static void RunC3EntryTrade()
    {
        var one = new Dictionary<int, int> { [5] = 1 };
        var (q, p, _, _, _) = C3Bf(trade: one);
        p.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
        var refused = BattlefieldFramework.OnEntryTrade(q);
        Check("c3 framework: trade: the player holds the clearance: WAIT_LONGER, no menu",
              "True Player.MessageBasic(94,0,0)", $"{refused == null} {C3Calls(p)}");
        p.Calls.Clear();
        p.StatusEffects.Clear();
        var member = new BattlefieldPlayerFake();
        member.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
        p.Members = new List<IBattlefieldPlayer> { p, member };
        BattlefieldFramework.OnEntryTrade(q);
        Check("c3 framework: trade: a party member holds it: WAIT_LONGER", "Player.MessageBasic(94,0,0)", C3Calls(p));

        string Worn(int uses, int? total, int[] table = null)
        {
            var data = new BattlefieldData
            {
                RequiredItems = new BattlefieldRequiredItems
                {
                    Items = new[] { 5 }, WornMessage = new ZoneText(168, "C3_WEAR"),
                },
            };
            var (wq, wp, we, ws, _) = C3Bf(data, one);
            var content = new BattlefieldContentFake(ws) { WornMessage = 111, WornMessageTable = table };
            we.ByZone[168] = new List<IBattlefieldContent> { content };
            if (total != null) we.Uses[5] = total.Value;
            wp.WornUsesByItem[5] = uses;
            var action = BattlefieldFramework.OnEntryTrade(wq);
            return $"{action?.ToString() ?? "null"} {string.Join(",", content.ReachedLabels)} {C3Calls(wp)}";
        }
        Check("c3 framework: trade: a worn item of one use",
              "null requiredItems:worn Player.MessageSpecial(111,0,0,0,5)", Worn(1, null));
        Check("c3 framework: trade: a worn item of 3 uses", "null requiredItems:worn Player.MessageSpecial(111,5)",
              Worn(3, 3));
        Check("c3 framework: trade: a table wornMessage", "null requiredItems:worn Player.MessageSpecial(7,8,9)",
              Worn(1, null, new[] { 7, 8, 9 }));
        Check("c3 framework: trade: an item with uses left opens the menu", "True",
              Worn(1, 3).StartsWith("event(32000,0,0,0,1,0,0,0,0)", StringComparison.Ordinal).ToString());
    }

    // battlefield.lua:728
    static void RunC3EntryTrigger()
    {
        var (q, p, _, _, _) = C3Bf();
        p.GMLevel = 1;
        p.VisibleGMLevel = 3;
        Check("c3 framework: trigger: a visible GM gets every option",
              "event(32000,0,0,0,268435455,0,0,0,0)", BattlefieldFramework.OnEntryTrigger(q).ToString());

        var (nq, np, _, _, _) = C3Bf();
        np.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
        np.EffectPowers[BattlefieldConst.EffectBattlefield] = 999;
        Check("c3 framework: trigger: the clearance of a battlefield with no content: nothing", "True",
              (BattlefieldFramework.OnEntryTrigger(nq) == null).ToString());

        string Engaged(int? registeredStatus)
        {
            var (eq, ep, _, _, _) = C3Bf();
            var member = new BattlefieldPlayerFake();
            member.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
            ep.Members = new List<IBattlefieldPlayer> { ep, member };
            if (registeredStatus != null)
                ep.RegisteredBattlefield = new BattlefieldViewFake { Status = registeredStatus.Value };
            BattlefieldFramework.OnEntryTrigger(eq);
            return C3Calls(ep);
        }
        Check("c3 framework: trigger: a member engaged, never registered: PARTY_MEMBERS_ARE_ENGAGED",
              "Player.MessageSpecial(104)", Engaged(null));
        Check("c3 framework: trigger: registered, the clearance lost at the start: CLEARED_BUT_MEMBERS_ENGAGED",
              "Player.MessageText(1,103,False)", Engaged(BattlefieldConst.StatusLocked));
        Check("c3 framework: trigger: registered, still open: PARTY_MEMBERS_ARE_ENGAGED", "Player.MessageSpecial(104)",
              Engaged(BattlefieldConst.StatusOpen));
    }

    // battlefield.lua:1022 and 1065
    static void RunC3Ticks()
    {
        var (q, _, e, _, bf) = C3Bf();
        var player = new BattlefieldPlayerFake { ZoneId = 168 };
        bf.PlayerList.Add(player);
        e.TimeRemaining = false;
        q.Framework.OnBattlefieldTick(1);
        Check("c3 framework: tick: time up: the message, LOST and the clean-up, on a tick that read OPEN so no " +
              "fallen message",
              "Battlefield.SetStatus(3),Battlefield.Cleanup(True)|Player.MessageSpecial(107)",
              $"{string.Join(",", bf.Calls)}|{C3Calls(player)}");

        var (lq, _, _, _, lb) = C3Bf();
        var fallen = new BattlefieldPlayerFake { ZoneId = 168 };
        lb.PlayerList.Add(fallen);
        lb.Status = BattlefieldConst.StatusLost;
        lq.Framework.OnBattlefieldTick(1);
        Check("c3 framework: tick: a LOST battlefield with its timer out: the fallen message and the clean-up",
              "Battlefield.SetLocalVar(cutsceneTimer,-1),Battlefield.Cleanup(True)|Player.MessageSpecial(108)",
              $"{string.Join(",", lb.Calls)}|{C3Calls(fallen)}");

        var (cq, _, _, _, cb) = C3Bf();
        var inside = new BattlefieldPlayerFake { CurrentBattlefield = cb };
        var outside = new BattlefieldPlayerFake();
        outside.StatusEffects.Add(BattlefieldConst.EffectBattlefield);
        inside.Members = new List<IBattlefieldPlayer> { inside, outside };
        cb.PlayerList.Add(inside);
        cq.Framework.OnBattlefieldStatusChange(BattlefieldConst.StatusLocked);
        cq.Framework.OnBattlefieldStatusChange(BattlefieldConst.StatusLocked);
        Check("c3 framework: status change: locked: an outside member loses the clearance, once",
              "Player.DelStatusEffect(254)|Battlefield.SetLocalVar(statusRemoval,1)",
              $"{C3Calls(outside)}|{string.Join(",", cb.Calls)}");
    }

    // the engine-handle loot path: self.loot as the live bridge gives it reaches SelectFromLootGroups
    static void RunC3Loot()
    {
        var s = new C2SubclassBattlefield(new BattlefieldData
        {
            Loot = { new BattlefieldLootGroup { { 916, 1000 } } },
        });
        var player = new BattlefieldPlayerFake { CharId = 1 };
        var bf = new BattlefieldViewFake { Id = 197 };
        bf.PlayerList.Add(player);
        player.CurrentBattlefield = bf;
        var content = new BattlefieldContentFake(s) { LootByHandle = true };
        var q = new BattlefieldContextFake(s) { Player = player, Self = content };
        var e = (BattlefieldEngineFake)q.Engine;
        s.HandleOpenArmouryCrate(q, new BattlefieldNpcFake { Id = 900 });
        Check("c3 framework: the loot rolls over self.loot's engine handle: the one table, its item to the pool",
              "True True|Player.AddTreasure(916,900)",
              $"{e.LootTables.Count == 1 && ReferenceEquals(e.LootTables[0], content.Loot)} " +
              $"{content.Loot.Groups == null}|{string.Join(",", player.Calls)}");
    }
}
