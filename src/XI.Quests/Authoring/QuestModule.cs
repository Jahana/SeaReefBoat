// XI.Quests -- quest modules (quest rework P4): the C# port of a Lua module that patches a quest container from
// xi.server.onServerStart with xi.module.modifyInteractionEntry (modules/module_utils.lua). A module names the C# quests it
// patches by key and declares its overrides on their handlers and checks, named the way the builder names them (the state's
// number, the zone, the NPC or mob, the handler kind, the csid or trigger area). The registration applies an enabled
// module's overrides to the quest's compiled definition before its container is built (QuestOverrides.Apply) and refuses an
// override whose handler the quest does not have; the patched container then goes to the end of the container list, as
// modifyInteractionEntry's remove and add leave it in the lookup (each list it shares, its entries after the others').
//
//   public sealed class TheGustabergTour : QuestModule
//   {
//       public TheGustabergTour() : base("era_quest_the_gustaberg_tour", "modules/era/lua/quests/bastok/the_gustaberg_tour.lua") { }
//       public override bool Condition(IQuestSettings s) => Pre(s, QuestExpansion.Rov);       // xi.pre(xi.expansion.ROV)
//       protected override void Build(QuestModuleBuilder m) =>
//           m.Quest(QuestKey.Bastok.TheGustabergTour)
//               .State(2).Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.HuntingBear)
//               .ReplaceOnTrigger(q => ...);                                                   // northGustaberg['Hunting_Bear'].onTrigger = ...
//   }
//
// A wrap gets the context and the handler it wraps, and decides whether and when to call it, as the Lua modules'
// `local base = x.onTrade; x.onTrade = function(...) ... base(...) ... end`:
//
//   .State(2).Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.Rockwell).WrapOnTrade((q, baseTrade) =>
//   {
//       int previous = q.Quest.Var("Wait");
//       var result = baseTrade(q);
//       if (q.Quest.Var("Wait") != previous) q.Quest.SetVar("Wait", ...);
//       return result;
//   })
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    // xi.expansion (modules/module_utils.lua): the release order, each with its ENABLE_<tag> setting (ROTZ, COP, TOAU, ...)
    public enum QuestExpansion : byte
    {
        Rotz = 1, Cop = 2, Toau = 3, Wotg = 4, Acp = 5, Amk = 6, Asa = 7, Abyssea = 8, Voidwatch = 9, Soa = 10, Rov = 11, Tvr = 12,
    }

    // who an override is: the module's name (Module:new's) and the Lua file it ports
    public sealed class QuestModuleInfo
    {
        public QuestModuleInfo(string name, string luaFile)
        {
            Name = name;
            LuaFile = luaFile;
        }

        public string Name { get; }
        // modules/<...>.lua, from the csharp folder (the init.txt entries are relative to modules/)
        public string LuaFile { get; }

        public override string ToString() => Name;
    }

    public abstract class QuestModule
    {
        // name: the Lua module's (Module:new('era_quest_the_gustaberg_tour', ...)); luaFile: the file it ports, from the csharp
        // folder (modules/era/lua/quests/bastok/the_gustaberg_tour.lua)
        protected QuestModule(string name, string luaFile)
        {
            // Module:new: a string of 5 characters or more
            if (name == null || name.Length < 5) throw new ArgumentException($"Invalid module name: {name}", nameof(name));
            if (luaFile == null || !luaFile.StartsWith("modules/", StringComparison.Ordinal) || !luaFile.EndsWith(".lua", StringComparison.Ordinal) ||
                luaFile.Contains('\\') || luaFile.Split('/').Any(p => p is "" or "." or ".."))
                throw new ArgumentException($"a module's Lua file is modules/<path>.lua with / separators: {luaFile}", nameof(luaFile));
            Info = new QuestModuleInfo(name, luaFile);
        }

        public QuestModuleInfo Info { get; }
        public string Name => Info.Name;
        public string LuaFile => Info.LuaFile;

        // Module:new's condition (xi.pre(xi.expansion.ROV) is Pre(s, QuestExpansion.Rov)); a module with none is always
        // enabled once listed. Read with the server's settings when the quests register
        public virtual bool Condition(IQuestSettings settings) => true;

        protected abstract void Build(QuestModuleBuilder m);

        // runs Build on a fresh builder; settings: what the module's Build may read (QuestModuleBuilder.Settings)
        public QuestModuleDefinition Compile(IQuestSettings settings)
        {
            var b = new QuestModuleBuilder(Info, settings);
            Build(b);
            return b.ToDefinition();
        }

        // xi.pre(expansion): true when content restriction is on (RESTRICT_CONTENT 1 or true) and the expansion's content is
        // off (ENABLE_<tag> not 1 and not true), compared as module_utils.lua compares them (quest rework P9: `== 1 or ==
        // true` is MainEquals(name, 1) || MainFlag(name), so a fraction 1.25 is neither, as in Lua)
        public static bool Pre(IQuestSettings settings, QuestExpansion expansion)
        {
            if (!Enum.IsDefined(typeof(QuestExpansion), expansion))
                throw new ArgumentOutOfRangeException(nameof(expansion), $"xi.pre: not an xi.expansion value: {(int)expansion}");
            if (settings == null) return false;
            if (!LuaOne(settings, "RESTRICT_CONTENT")) return false;
            return !LuaOne(settings, "ENABLE_" + Tag(expansion));
        }

        // the Lua `xi.settings.main[name] == 1 or xi.settings.main[name] == true`
        static bool LuaOne(IQuestSettings settings, string name) => settings.MainEquals(name, 1) || settings.MainFlag(name);

        // the expansion's tag, as its ENABLE_<tag> setting names it
        public static string Tag(QuestExpansion expansion) => expansion.ToString().ToUpperInvariant();

        // the settings a module is checked under offline, one per branch xi.pre can take: no content restriction (every
        // xi.pre false) and restriction with no expansion enabled (every xi.pre true); any other setting reads 0
        public static IReadOnlyList<(string Name, IQuestSettings Settings)> Branches { get; } = new (string, IQuestSettings)[]
        {
            ("xi.pre false", new BranchSettings(0)),
            ("xi.pre true", new BranchSettings(1)),
        };

        // the quest keys the module patches under any branch (and the settings given), in the order first named; a Build
        // that throws under one branch adds none from it
        public IReadOnlyList<string> PatchedKeys(params IQuestSettings[] more)
        {
            var keys = new List<string>();
            foreach (var settings in Branches.Select(b => b.Settings).Concat(more ?? Array.Empty<IQuestSettings>()))
            {
                QuestModuleDefinition md;
                try
                {
                    md = Compile(settings);
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (var key in md.Keys)
                    if (!keys.Contains(key)) keys.Add(key);
            }
            return keys;
        }

        sealed class BranchSettings : IQuestSettings
        {
            readonly int _restrict;

            public BranchSettings(int restrict) => _restrict = restrict;

            public int Main(string name) => name == "RESTRICT_CONTENT" ? _restrict : 0;
            // the branches vary RESTRICT_CONTENT only: xi.settings.map is unset here, as any other main setting
            public int Map(string name) => 0;
            public bool MapFlag(string name) => false;
        }
    }

    // a compiled module: its patches, one per quest key, in the order the module first named each quest
    public sealed class QuestModuleDefinition
    {
        internal QuestModuleDefinition(QuestModuleInfo info, IReadOnlyList<QuestModulePatch> patches)
        {
            Info = info;
            Patches = patches;
        }

        public QuestModuleInfo Info { get; }
        public IReadOnlyList<QuestModulePatch> Patches { get; }
        public IEnumerable<string> Keys => Patches.Select(p => p.Key);
        public QuestModulePatch PatchFor(string key) => Patches.FirstOrDefault(p => p.Key == key);
    }

    // the module's overrides of one quest, in declaration order (applied in that order: a second wrap of a handler wraps the
    // first)
    public sealed class QuestModulePatch
    {
        internal QuestModulePatch(string key, IReadOnlyList<QuestOverrideOp> ops)
        {
            Key = key;
            Ops = ops;
        }

        // the quest key (bastok.the_gustaberg_tour: XI.QuestContent.QuestKey)
        public string Key { get; }
        public IReadOnlyList<QuestOverrideOp> Ops { get; }
    }

    // a wrap of a handler that replies (OnTrigger, OnTrade, OnZoneIn, OnAfterZoneIn, an OnTriggerAreaEnter with a reply): the handler it
    // wraps is baseHandler, called or not
    public delegate QuestReply QuestReplyWrap(IQuestContext q, Func<IQuestContext, QuestReply> baseHandler);
    // a wrap of a handler that runs (OnMobDeath, OnEventFinish, OnEventUpdate, OnZoneOut, an OnTriggerAreaEnter without a reply)
    public delegate void QuestRunWrap(IQuestContext q, Action<IQuestContext> baseHandler);
    // a wrap of a state's check: baseCheck is the state's own (its matches, or the check a module put there before)
    public delegate bool QuestCheckWrap(IQuestCheck q, Func<IQuestCheck, bool> baseCheck);

    public enum QuestOverrideMode : byte { Wrap, Replace, Add }

    // one override, as the module declared it
    public abstract class QuestOverrideOp
    {
        internal QuestOverrideOp() { }
    }

    // a handler wrapped, replaced or added: by state number, zone, entity (null: a state-level handler), kind and id (a csid
    // or trigger area id; 1 for OnZoneIn, OnAfterZoneIn and OnZoneOut, 0 for an entity's)
    public sealed class QuestHandlerOverride : QuestOverrideOp
    {
        internal QuestHandlerOverride(int state, QuestHandlerKind kind, ushort zone, string entity, int id, QuestOverrideMode mode,
                                      QuestReplyWrap reply, QuestRunWrap run)
        {
            State = state;
            Kind = kind;
            Zone = zone;
            Entity = entity;
            Id = id;
            Mode = mode;
            Reply = reply;
            Run = run;
        }

        public int State { get; }
        public QuestHandlerKind Kind { get; }
        public ushort Zone { get; }
        public string Entity { get; }
        public int Id { get; }
        public QuestOverrideMode Mode { get; }
        // one of the two: a replace or an add ignores its base
        public QuestReplyWrap Reply { get; }
        public QuestRunWrap Run { get; }
    }

    // quest.sections[n].check = function(player, status, vars) ... end
    public sealed class QuestCheckOverride : QuestOverrideOp
    {
        internal QuestCheckOverride(int state, QuestOverrideMode mode, QuestCheckWrap check)
        {
            State = state;
            Mode = mode;
            Check = check;
        }

        public int State { get; }
        public QuestOverrideMode Mode { get; }
        public QuestCheckWrap Check { get; }
    }

    // quest.reward = { ... }
    public sealed class QuestRewardOverride : QuestOverrideOp
    {
        internal QuestRewardOverride(QuestReward reward) => Reward = reward;
        public QuestReward Reward { get; }
    }

    // table.remove(quest.sections, n): the state keeps its number (the later states keep theirs, so their handlers' labels
    // stay), loses its handlers and never matches; the bridge builds no section for it
    public sealed class QuestRemoveStateOverride : QuestOverrideOp
    {
        internal QuestRemoveStateOverride(int state) => State = state;
        public int State { get; }
    }

    // table.insert(quest.sections, { ... }): states built as the quest builds its own, appended after its last
    public sealed class QuestAddStatesOverride : QuestOverrideOp
    {
        internal QuestAddStatesOverride(Action<QuestScriptBuilder> build) => Build = build;
        public Action<QuestScriptBuilder> Build { get; }
    }

    public sealed class QuestModuleBuilder
    {
        readonly QuestModuleInfo _info;
        readonly List<(string Key, List<QuestOverrideOp> Ops)> _quests = new List<(string, List<QuestOverrideOp>)>();

        internal QuestModuleBuilder(QuestModuleInfo info, IQuestSettings settings)
        {
            _info = info;
            Settings = settings;
        }

        // the server's settings (xi.settings.main) when the quests register: a module whose overrides depend on more than its
        // condition (Inside the Belly's onTrigger only while xi.pre(WOTG)) reads them here
        public IQuestSettings Settings { get; }

        // xi.pre(expansion) over Settings
        public bool Pre(QuestExpansion expansion) => QuestModule.Pre(Settings, expansion);

        // the C# quest with the key (XI.QuestContent.QuestKey: QuestKey.Bastok.TheGustabergTour); a second Quest of the same
        // key goes on with its overrides
        public QuestOverrideBuilder Quest(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf('.') <= 0 || ContainerKeys.IsMission(key))
                throw new ArgumentException($"a quest key is <area>.<name>: {key}", nameof(key));
            return Overrides(key);
        }

        // the C# mission with the key (XI.QuestContent.MissionKey.ToAU.<Name>): its states take a quest's handler and
        // check overrides; QuestOverrides.Apply refuses Reward, RemoveState and AddStates
        public QuestOverrideBuilder Mission(string key)
        {
            if (!ContainerKeys.IsMission(key))
                throw new ArgumentException($"a mission key is mission.<log>.<name>: {key}", nameof(key));
            return Overrides(key);
        }

        QuestOverrideBuilder Overrides(string key)
        {
            var at = _quests.FindIndex(q => q.Key == key);
            if (at < 0)
            {
                _quests.Add((key, new List<QuestOverrideOp>()));
                at = _quests.Count - 1;
            }
            return new QuestOverrideBuilder(this, _quests[at].Ops);
        }

        internal QuestModuleDefinition ToDefinition() =>
            new QuestModuleDefinition(_info, _quests.Select(q => new QuestModulePatch(q.Key, q.Ops.ToArray())).ToArray());
    }

    // one quest's overrides
    public sealed class QuestOverrideBuilder
    {
        readonly QuestModuleBuilder _owner;
        readonly List<QuestOverrideOp> _ops;

        internal QuestOverrideBuilder(QuestModuleBuilder owner, List<QuestOverrideOp> ops)
        {
            _owner = owner;
            _ops = ops;
        }

        // the state with the number (1-based, in declaration order: a handler label's <n>, and the Lua quest.sections index
        // when the port keeps its sections in order)
        public QuestStateOverrideBuilder State(int number) => new QuestStateOverrideBuilder(this, StateNumber(number));

        // quest.reward = { ... }: the reward table replaced (a copy is kept)
        public QuestOverrideBuilder Reward(QuestReward reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            _ops.Add(new QuestRewardOverride(QuestScriptBuilder.CopyReward(reward)));
            return this;
        }

        // table.remove(quest.sections, n)
        public QuestOverrideBuilder RemoveState(int number)
        {
            _ops.Add(new QuestRemoveStateOverride(StateNumber(number)));
            return this;
        }

        // table.insert(quest.sections, { check = ..., [zone] = { ... } }): built as the quest builds its states, after them
        public QuestOverrideBuilder AddStates(Action<QuestScriptBuilder> build)
        {
            _ops.Add(new QuestAddStatesOverride(build ?? throw new ArgumentNullException(nameof(build))));
            return this;
        }

        // the module's next quest
        public QuestOverrideBuilder Quest(string key) => _owner.Quest(key);

        internal void Add(QuestOverrideOp op) => _ops.Add(op);

        static int StateNumber(int number) =>
            number >= 1 ? number : throw new ArgumentOutOfRangeException(nameof(number), number, "a state's number is 1 or more");
    }

    // one state's overrides: its check, its state-level handlers, and Npc(...) for an entity's
    public sealed class QuestStateOverrideBuilder
    {
        readonly QuestOverrideBuilder _quest;
        readonly int _state;

        internal QuestStateOverrideBuilder(QuestOverrideBuilder quest, int state)
        {
            _quest = quest;
            _state = state;
        }

        // ---- the check ----

        public QuestStateOverrideBuilder WrapCheck(QuestCheckWrap check) =>
            Check(QuestOverrideMode.Wrap, check ?? throw new ArgumentNullException(nameof(check)));

        // quest.sections[n].check = function(player, status, vars) return false end is ReplaceCheck(q => false)
        public QuestStateOverrideBuilder ReplaceCheck(Func<IQuestCheck, bool> check)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            return Check(QuestOverrideMode.Replace, (q, _) => check(q));
        }

        QuestStateOverrideBuilder Check(QuestOverrideMode mode, QuestCheckWrap check)
        {
            _quest.Add(new QuestCheckOverride(_state, mode, check));
            return this;
        }

        // ---- an entity's handlers ----

        public QuestNpcOverrideBuilder Npc(ushort zone, string name) =>
            new QuestNpcOverrideBuilder(this, zone, name ?? throw new ArgumentNullException(nameof(name)));

        // ---- state-level handlers: zone.onEventFinish[csid], onEventUpdate[csid], onZoneIn, afterZoneIn, onZoneOut, onTriggerAreaEnter[id], onTriggerAreaLeave[id] ----

        public QuestStateOverrideBuilder WrapOnEventFinish(ushort zone, int csid, QuestRunWrap run) => Run(QuestHandlerKind.OnEventFinish, zone, csid, QuestOverrideMode.Wrap, run);
        public QuestStateOverrideBuilder ReplaceOnEventFinish(ushort zone, int csid, Action<IQuestContext> run) => Run(QuestHandlerKind.OnEventFinish, zone, csid, QuestOverrideMode.Replace, Ignore(run));
        public QuestStateOverrideBuilder AddOnEventFinish(ushort zone, int csid, Action<IQuestContext> run) => Run(QuestHandlerKind.OnEventFinish, zone, csid, QuestOverrideMode.Add, Ignore(run));

        public QuestStateOverrideBuilder WrapOnEventUpdate(ushort zone, int csid, QuestRunWrap run) => Run(QuestHandlerKind.OnEventUpdate, zone, csid, QuestOverrideMode.Wrap, run);
        public QuestStateOverrideBuilder ReplaceOnEventUpdate(ushort zone, int csid, Action<IQuestContext> run) => Run(QuestHandlerKind.OnEventUpdate, zone, csid, QuestOverrideMode.Replace, Ignore(run));
        public QuestStateOverrideBuilder AddOnEventUpdate(ushort zone, int csid, Action<IQuestContext> run) => Run(QuestHandlerKind.OnEventUpdate, zone, csid, QuestOverrideMode.Add, Ignore(run));

        public QuestStateOverrideBuilder WrapOnZoneIn(ushort zone, QuestReplyWrap reply) => Reply(QuestHandlerKind.OnZoneIn, zone, null, 1, QuestOverrideMode.Wrap, reply);
        public QuestStateOverrideBuilder ReplaceOnZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnZoneIn, zone, null, 1, QuestOverrideMode.Replace, Ignore(reply));
        public QuestStateOverrideBuilder AddOnZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnZoneIn, zone, null, 1, QuestOverrideMode.Add, Ignore(reply));

        public QuestStateOverrideBuilder WrapOnAfterZoneIn(ushort zone, QuestReplyWrap reply) => Reply(QuestHandlerKind.OnAfterZoneIn, zone, null, 1, QuestOverrideMode.Wrap, reply);
        public QuestStateOverrideBuilder ReplaceOnAfterZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnAfterZoneIn, zone, null, 1, QuestOverrideMode.Replace, Ignore(reply));
        public QuestStateOverrideBuilder AddOnAfterZoneIn(ushort zone, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnAfterZoneIn, zone, null, 1, QuestOverrideMode.Add, Ignore(reply));

        public QuestStateOverrideBuilder WrapOnZoneOut(ushort zone, QuestRunWrap run) => Run(QuestHandlerKind.OnZoneOut, zone, 1, QuestOverrideMode.Wrap, run);
        public QuestStateOverrideBuilder ReplaceOnZoneOut(ushort zone, Action<IQuestContext> run) => Run(QuestHandlerKind.OnZoneOut, zone, 1, QuestOverrideMode.Replace, Ignore(run));
        public QuestStateOverrideBuilder AddOnZoneOut(ushort zone, Action<IQuestContext> run) => Run(QuestHandlerKind.OnZoneOut, zone, 1, QuestOverrideMode.Add, Ignore(run));

        // a trigger area's handler runs (Action) or replies (Func), as the quest declared it
        public QuestStateOverrideBuilder WrapOnTriggerAreaEnter(ushort zone, int areaId, QuestRunWrap run) => Run(QuestHandlerKind.OnTriggerAreaEnter, zone, areaId, QuestOverrideMode.Wrap, run);
        public QuestStateOverrideBuilder WrapOnTriggerAreaEnter(ushort zone, int areaId, QuestReplyWrap reply) => Reply(QuestHandlerKind.OnTriggerAreaEnter, zone, null, areaId, QuestOverrideMode.Wrap, reply);
        public QuestStateOverrideBuilder ReplaceOnTriggerAreaEnter(ushort zone, int areaId, Action<IQuestContext> run) => Run(QuestHandlerKind.OnTriggerAreaEnter, zone, areaId, QuestOverrideMode.Replace, Ignore(run));
        public QuestStateOverrideBuilder ReplaceOnTriggerAreaEnter(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTriggerAreaEnter, zone, null, areaId, QuestOverrideMode.Replace, Ignore(reply));
        public QuestStateOverrideBuilder AddOnTriggerAreaEnter(ushort zone, int areaId, Action<IQuestContext> run) => Run(QuestHandlerKind.OnTriggerAreaEnter, zone, areaId, QuestOverrideMode.Add, Ignore(run));
        public QuestStateOverrideBuilder AddOnTriggerAreaEnter(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTriggerAreaEnter, zone, null, areaId, QuestOverrideMode.Add, Ignore(reply));

        // onTriggerAreaLeave[id] (quest rework P9), as the enter's
        public QuestStateOverrideBuilder WrapOnTriggerAreaLeave(ushort zone, int areaId, QuestRunWrap run) => Run(QuestHandlerKind.OnTriggerAreaLeave, zone, areaId, QuestOverrideMode.Wrap, run);
        public QuestStateOverrideBuilder WrapOnTriggerAreaLeave(ushort zone, int areaId, QuestReplyWrap reply) => Reply(QuestHandlerKind.OnTriggerAreaLeave, zone, null, areaId, QuestOverrideMode.Wrap, reply);
        public QuestStateOverrideBuilder ReplaceOnTriggerAreaLeave(ushort zone, int areaId, Action<IQuestContext> run) => Run(QuestHandlerKind.OnTriggerAreaLeave, zone, areaId, QuestOverrideMode.Replace, Ignore(run));
        public QuestStateOverrideBuilder ReplaceOnTriggerAreaLeave(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTriggerAreaLeave, zone, null, areaId, QuestOverrideMode.Replace, Ignore(reply));
        public QuestStateOverrideBuilder AddOnTriggerAreaLeave(ushort zone, int areaId, Action<IQuestContext> run) => Run(QuestHandlerKind.OnTriggerAreaLeave, zone, areaId, QuestOverrideMode.Add, Ignore(run));
        public QuestStateOverrideBuilder AddOnTriggerAreaLeave(ushort zone, int areaId, Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTriggerAreaLeave, zone, null, areaId, QuestOverrideMode.Add, Ignore(reply));

        // the next state, the next quest
        public QuestStateOverrideBuilder State(int number) => _quest.State(number);
        public QuestOverrideBuilder Quest(string key) => _quest.Quest(key);

        internal QuestStateOverrideBuilder Reply(QuestHandlerKind kind, ushort zone, string entity, int id, QuestOverrideMode mode, QuestReplyWrap reply)
        {
            _quest.Add(new QuestHandlerOverride(_state, kind, zone, entity, id, mode, reply ?? throw new ArgumentNullException(nameof(reply)), null));
            return this;
        }

        internal QuestStateOverrideBuilder Run(QuestHandlerKind kind, ushort zone, int id, QuestOverrideMode mode, QuestRunWrap run, string entity = null)
        {
            _quest.Add(new QuestHandlerOverride(_state, kind, zone, entity, id, mode, null, run ?? throw new ArgumentNullException(nameof(run))));
            return this;
        }

        internal static QuestReplyWrap Ignore(Func<IQuestContext, QuestReply> reply)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));
            return (q, _) => reply(q);
        }

        internal static QuestRunWrap Ignore(Action<IQuestContext> run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            return (q, _) => run(q);
        }
    }

    // an NPC's or mob's handlers in one state (Lua keys both by name)
    public sealed class QuestNpcOverrideBuilder
    {
        readonly QuestStateOverrideBuilder _state;
        readonly ushort _zone;
        readonly string _name;

        internal QuestNpcOverrideBuilder(QuestStateOverrideBuilder state, ushort zone, string name)
        {
            _state = state;
            _zone = zone;
            _name = name;
        }

        public QuestNpcOverrideBuilder WrapOnTrigger(QuestReplyWrap reply) => Reply(QuestHandlerKind.OnTrigger, QuestOverrideMode.Wrap, reply);
        public QuestNpcOverrideBuilder ReplaceOnTrigger(Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTrigger, QuestOverrideMode.Replace, QuestStateOverrideBuilder.Ignore(reply));
        public QuestNpcOverrideBuilder AddOnTrigger(Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTrigger, QuestOverrideMode.Add, QuestStateOverrideBuilder.Ignore(reply));

        public QuestNpcOverrideBuilder WrapOnTrade(QuestReplyWrap reply) => Reply(QuestHandlerKind.OnTrade, QuestOverrideMode.Wrap, reply);
        public QuestNpcOverrideBuilder ReplaceOnTrade(Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTrade, QuestOverrideMode.Replace, QuestStateOverrideBuilder.Ignore(reply));
        public QuestNpcOverrideBuilder AddOnTrade(Func<IQuestContext, QuestReply> reply) => Reply(QuestHandlerKind.OnTrade, QuestOverrideMode.Add, QuestStateOverrideBuilder.Ignore(reply));

        public QuestNpcOverrideBuilder WrapOnMobDeath(QuestRunWrap run) => Run(QuestOverrideMode.Wrap, run);
        public QuestNpcOverrideBuilder ReplaceOnMobDeath(Action<IQuestContext> run) => Run(QuestOverrideMode.Replace, QuestStateOverrideBuilder.Ignore(run));
        public QuestNpcOverrideBuilder AddOnMobDeath(Action<IQuestContext> run) => Run(QuestOverrideMode.Add, QuestStateOverrideBuilder.Ignore(run));

        // the same state's next entity, its state-level handlers, the next state, the next quest
        public QuestNpcOverrideBuilder Npc(ushort zone, string name) => _state.Npc(zone, name);
        public QuestStateOverrideBuilder WrapOnEventFinish(ushort zone, int csid, QuestRunWrap run) => _state.WrapOnEventFinish(zone, csid, run);
        public QuestStateOverrideBuilder ReplaceOnEventFinish(ushort zone, int csid, Action<IQuestContext> run) => _state.ReplaceOnEventFinish(zone, csid, run);
        public QuestStateOverrideBuilder AddOnEventFinish(ushort zone, int csid, Action<IQuestContext> run) => _state.AddOnEventFinish(zone, csid, run);
        public QuestStateOverrideBuilder WrapOnEventUpdate(ushort zone, int csid, QuestRunWrap run) => _state.WrapOnEventUpdate(zone, csid, run);
        public QuestStateOverrideBuilder ReplaceOnEventUpdate(ushort zone, int csid, Action<IQuestContext> run) => _state.ReplaceOnEventUpdate(zone, csid, run);
        public QuestStateOverrideBuilder AddOnEventUpdate(ushort zone, int csid, Action<IQuestContext> run) => _state.AddOnEventUpdate(zone, csid, run);
        public QuestStateOverrideBuilder State(int number) => _state.State(number);
        public QuestOverrideBuilder Quest(string key) => _state.Quest(key);

        QuestNpcOverrideBuilder Reply(QuestHandlerKind kind, QuestOverrideMode mode, QuestReplyWrap reply)
        {
            _state.Reply(kind, _zone, _name, 0, mode, reply);
            return this;
        }

        QuestNpcOverrideBuilder Run(QuestOverrideMode mode, QuestRunWrap run)
        {
            _state.Run(QuestHandlerKind.OnMobDeath, _zone, 0, mode, run, _name);
            return this;
        }
    }
}
