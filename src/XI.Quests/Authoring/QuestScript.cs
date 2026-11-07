// XI.Quests -- the authoring API of quest rework B2 (docs/superpowers/specs/2026-10-21-csharp-quests-live-design.md, section 2).
//
// A quest is a C# class: its states are the Lua sections, a state's matches are that section's check, and its
// handlers are the section's per-zone tables. Compile() runs Build once and freezes the result; the bridge
// (later tasks) turns a definition into the Lua quest framework. Nothing here touches the engine.
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    public abstract class QuestScript : ContainerScript
    {
        protected QuestScript(QuestLog log, string constName)
        {
            Log = log;
            ConstName = constName;
        }

        // a hidden quest (HiddenQuestScript, quest rework P9): no log, no xi.quest.id constant
        private protected QuestScript(string hiddenName)
        {
            if (string.IsNullOrEmpty(hiddenName)) throw new ArgumentException("a hidden quest needs its HiddenQuest:new name", nameof(hiddenName));
            Log = HiddenLog;
            HiddenName = hiddenName;
        }

        // a hidden quest's Log: no quest log (quest_log.h has none for it; a read of xi.quest.area[255] finds nothing)
        public const QuestLog HiddenLog = (QuestLog)byte.MaxValue;
        // the area name of a hidden quest's key (scripts/quests/hiddenQuests)
        public const string HiddenArea = "hiddenQuests";

        public QuestLog Log { get; }
        // the xi.quest.id constant, e.g. "THE_ELVAAN_GOLDSMITH"; null for a hidden quest
        public string ConstName { get; }
        // a hidden quest's HiddenQuest:new(name) name (its container id and var prefix HQuest[name]); null for a log quest
        public string HiddenName { get; }

        // the scripts/quests file this quest replaces (used by the bridge's Lua fallback); null: the Lua container's
        // slot if its file exists, otherwise appended after every path, by key
        public override string LegacyPath => null;

        public override ContainerKind Kind => ContainerKind.Quest;

        // the quest's key (ContainerScript.Key): KeyIn(AreaName(Log)), a hidden quest's hiddenQuests.<file>; null when
        // the log has no area (the registration refuses the quest: unknown quest log)
        public override string Key =>
            HiddenName != null ? HiddenKey(LegacyPath)
            : AreaName(Log) is { } area ? QuestKeyRegistry.KeyFor(area, ConstName) : null;

        // the key the bridge gave the quest before C2 (for the checks): KeyIn with the area luaArea gives for its log
        // (xi.quest.area as the Lua file has it), or with hiddenQuests for a hidden quest
        internal string ExpectedKeyForTest(Func<QuestLog, string> luaArea) =>
            KeyIn(HiddenName != null ? HiddenArea : luaArea(Log));

        // the quest's key: <area>.<constant in lower case> (bastok.the_elvaan_goldsmith) with area xi.quest.area[Log], or
        // for a hidden quest hiddenQuests.<its file's name in lower case> (hiddenQuests.portal_charm; areaName unused)
        public string KeyIn(string areaName) => HiddenName != null ? HiddenKey(LegacyPath) : QuestKeyRegistry.KeyFor(areaName, ConstName);

        // xi.quest.area (scripts/globals/quests.lua): the Lua area name of each quest log, by its number
        private static readonly string[] AreaNames =
        {
            "sandoria", "bastok", "windurst", "jeuno", "otherAreas", "outlands", "ahtUrhgan", "crystalWar", "abyssea",
            "adoulin", "coalition",
        };

        // xi.quest.area[log]: the area name of the log's keys and of its xi.quest.id table; null when the log has none
        // (the hidden log included)
        public static string AreaName(QuestLog log) => (int)log < AreaNames.Length ? AreaNames[(int)log] : null;

        // hiddenQuests.<the file name of the legacy path, in lower case>: scripts/quests/hiddenQuests/Trust_Zeid_II is
        // hiddenQuests.trust_zeid_ii
        public static string HiddenKey(string legacyPath)
        {
            if (string.IsNullOrEmpty(legacyPath)) throw new InvalidOperationException("a hidden quest's key is its LegacyPath file's name: it has none");
            int slash = legacyPath.LastIndexOf('/');
            return HiddenArea + "." + legacyPath.Substring(slash + 1).ToLowerInvariant();
        }

        protected abstract void Build(QuestScriptBuilder b);

        // runs Build on a fresh builder
        public QuestScriptDefinition Compile()
        {
            var b = new QuestScriptBuilder();
            Build(b);
            var def = b.ToDefinition(Log, ConstName);
            def.HiddenName = HiddenName;
            return def;
        }
    }

    // a hidden quest (quest rework P9): a HiddenQuest:new(name) container (scripts/globals/interaction/hidden_quest.lua).
    // It has no quest log, no id and no status: its container id and var prefix are HQuest[name] (Quest.Var / SetVar / Step
    // read and write HQuest[name]<var>), its states are AnyState(when: <the section's check>) only (the validator refuses a
    // status match; Quest.Status, Begin and DelCurrent throw), and Quest.Complete() is HiddenQuest:complete:
    // npcUtil.giveReward(player, reward) and, when it gave it, the cleanup (the HQuest[name] vars cleared); no
    // completeQuest. Its key is hiddenQuests.<LegacyPath's file name in lower case>, so every hidden quest names its file
    public abstract class HiddenQuestScript : QuestScript
    {
        protected HiddenQuestScript(string name) : base(name) { }

        // the HiddenQuest:new name
        public string Name => HiddenName;

        // scripts/quests/hiddenQuests/<File>: the container's slot and the key's name (required)
        public abstract override string LegacyPath { get; }
    }

    public sealed class QuestScriptDefinition
    {
        internal QuestScriptDefinition(QuestLog log, string constName, QuestReward reward,
            IReadOnlyList<QuestStateDecl> states, IReadOnlyList<int> declaredSteps, QuestModuleInfo rewardModule = null)
        {
            RewardModule = rewardModule;
            Log = log;
            ConstName = constName;
            Reward = reward;
            States = states;
            DeclaredSteps = declaredSteps;
            HasCalled = states.Any(st => st.Handlers.Any(h => h.Called));
            // every entity handler is an NPC-call one (state-level handlers don't count): the quest has no container
            CallOnly = HasCalled && states.All(st => st.Handlers.All(h => h.Called || h.Entity == null));
        }

        public QuestLog Log { get; }
        public string ConstName { get; }
        // a hidden quest's HiddenQuest:new name (quest rework P9); null for a log quest
        public string HiddenName { get; internal set; }
        public bool Hidden => HiddenName != null;
        // null: no reward table (Lua's empty Quest.reward)
        public QuestReward Reward { get; private set; }
        // the C# module whose reward this is (QuestOverrides.Apply: Reward); null for the quest's own
        public QuestModuleInfo RewardModule { get; private set; }
        // declaration order == Lua section order
        public IReadOnlyList<QuestStateDecl> States { get; private set; }
        // the steps the quest says it sets (QuestScriptBuilder.Steps), for the offline validator
        public IReadOnlyList<int> DeclaredSteps { get; private set; }
        // some handler is an NPC-call one (Called())
        public bool HasCalled { get; private set; }
        // an NPC-call quest with no container handler
        public bool CallOnly { get; private set; }
        // the quest's deferred callbacks (quest rework P10, QuestScriptBuilder.Callback), in declaration order: armed
        // from the handlers, not matched against the states; each is a handler for coverage (0/-/callback:<name>)
        public IReadOnlyList<QuestCallback> Callbacks { get; internal set; } = Array.Empty<QuestCallback>();
        // the Lua globals the quest sets to its callbacks (QuestScriptBuilder.Export), in declaration order
        public IReadOnlyList<QuestExport> Exports { get; internal set; } = Array.Empty<QuestExport>();

        // a copy with another reward, states and steps (QuestOverrides.Apply), every other member kept (a member added later
        // is copied too); what follows from the states worked out again
        internal QuestScriptDefinition With(QuestReward reward, QuestModuleInfo rewardModule, IReadOnlyList<QuestStateDecl> states, IReadOnlyList<int> declaredSteps)
        {
            var copy = (QuestScriptDefinition)MemberwiseClone();
            copy.Reward = reward;
            copy.RewardModule = rewardModule;
            copy.States = states;
            copy.DeclaredSteps = declaredSteps;
            copy.HasCalled = states.Any(st => st.Handlers.Any(h => h.Called));
            copy.CallOnly = copy.HasCalled && states.All(st => st.Handlers.All(h => h.Called || h.Entity == null));
            return copy;
        }
        // whether the handler runs through xi.cs.quest / questEvent: a Called() one, or a state-level one of a CallOnly quest
        public bool IsCalled(QuestHandlerDecl h) => h.Called || CallOnly && h.Entity == null;
    }

    // quest.reward: only the fields set become keys of the Lua table (npcUtil.completeQuest's params)
    public sealed class QuestReward
    {
        public int? Fame, FameArea, Gil, Title, Item, KeyItem, Exp, Bayld;
        // reward.items, a list of item ids
        public int[] Items;
        // reward.var: a char var npcUtil.completeQuest deletes on completion
        public string Var;
        // reward.itemParams: npcUtil.giveItem's params for the reward's items (itemParams = { fromTrade = true })
        public QuestItemParams ItemParams;

        // a deep copy: every field (one added later too), the list and the params their own
        internal QuestReward Copy()
        {
            var copy = (QuestReward)MemberwiseClone();
            copy.Items = Items == null ? null : (int[])Items.Clone();
            copy.ItemParams = ItemParams?.Copy();
            return copy;
        }
    }

    // npcUtil.giveItem's params ({ silent, fromTrade, multiple }): only the fields set become keys of the Lua table
    public sealed class QuestItemParams
    {
        // no messages at all
        public bool? Silent;
        // no room: "Try trading again after sorting your inventory" (ITEM_CANNOT_BE_OBTAINED + 4) in place of "Come back
        // again after sorting your inventory"
        public bool? FromTrade;
        // the multiples message (ITEM_OBTAINED + 9) for a single item
        public bool? Multiple;

        internal QuestItemParams Copy() => (QuestItemParams)MemberwiseClone();
    }
}
