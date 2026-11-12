// XI.Quests -- quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section
// 3.3): a battlefield is a C# class, the third kind on the shared container core. Its data is what the Lua
// Battlefield:new takes (scripts/globals/battlefield.lua:417), and its hooks are the Lua framework's methods, each a
// virtual whose default is the framework's own Lua, ported line for line in BattlefieldFramework. A battlefield file
// that overrides a method (function content:entryRequirement ...) overrides the hook. The bridge
// (ZoneHost.BattlefieldScripts) turns a battlefield into the Lua content table the engine and the other battlefields
// of its zone call. Nothing here touches the engine.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XI.Quests.Authoring
{
    // Battlefield:new's fields (scripts/globals/battlefield.lua:417), as data. A field left at its default is the Lua's
    // default (levelCap 0, allowSubjob true, delayToExit 5, ...); an int at 0 or a reference at null is a nil field
    public class BattlefieldData
    {
        public int ZoneId; public int BattlefieldId; public int MaxPlayers; public int TimeLimit; public int Index;
        // entryNpc / entryNpcs and exitNpc / exitNpcs (a lone name is a list of one)
        public string[] EntryNpcs; public string[] ExitNpcs;
        // area: a fixed area (0: none, the arena the menu asks for); title: the title the win gives (0: none). For
        // Area, Title and GrantXP, 0 stands for the Lua's nil: Lua treats 0 as true, so a field set to 0 would differ,
        // but no battlefield sets one to 0
        public int Area; public int Title;
        // grantXP: the experience the win gives (0: none); grantXPLockout: once per JST day
        public int GrantXP; public bool GrantXPLockout;
        // levelCap: 0 for none; LevelCapSetting names an xi.settings.main value read at registration instead
        // (levelCap = xi.settings.main.MAX_LEVEL)
        public int LevelCap; public string LevelCapSetting;
        public bool AllowSubjob = true; public bool AllowTrusts; public bool HasWipeGrace = true;
        public bool IsMission; public bool CanLoseExp = true; public bool CleanupDebuffs; public bool ShowTimer = true;
        public int DelayToExit = 5;
        public BattlefieldRequiredItems RequiredItems; public BattlefieldRequiredKeyItems RequiredKeyItems;
        // lossEventParams: the 32002 event's table (null: the Lua's empty table)
        public int[] LossEventParams;
        public bool Experimental;
        // allowedAreas: the areas the arenas may be entered in (null: the Lua's nil, every area)
        public int[] AllowedAreas;
        public int CsParam7; public int CsParam8;
        public List<BattlefieldEssentialMobs> EssentialMobs = new();   // addEssentialMobs, in call order
        public List<BattlefieldLootGroup> Loot = new();
        public List<BattlefieldGroup> Groups = new();  // content.groups
        // armouryCrates: the crate of each area, the first for area 1 (null: the Lua's false)
        public BattlefieldArmouryCrate[] ArmouryCrates;
        // content.paths: a mob's patrol path, keyed by its mob id
        public List<BattlefieldPath> Paths = new();
        // fields beyond Battlefield:new's that another script reads from the content (content.positions, read by
        // Memory_Receptacle_Red.lua:57), set on the proxy as they are: a number, a string, a bool, an object[] (a Lua
        // array) or a Dictionary<string, object> (a table with names). A name the framework uses is refused
        public Dictionary<string, object> ProxyFields = new();
        // content.sections' own event handlers in the zone (one_to_be_feared.lua:91), by csid. The zone's hooks go in
        // the same tables, so the framework's ids 32000 to 32005 are refused
        public Dictionary<int, BattlefieldEventHandler> OnEventUpdate = new();
        public Dictionary<int, BattlefieldEventHandler> OnEventFinish = new();
        // the deferred callbacks its hooks arm (quest rework C3 PR2: mob:timer, mob:queue, addListener closures); an
        // arming of one not in this list is refused
        public List<BattlefieldCallback> Callbacks = new();

        // a callback declared: added to Callbacks, its handle returned
        public BattlefieldCallback Callback(string name, BattlefieldCallbackBody run)
        {
            var cb = new BattlefieldCallback(name, run);
            Callbacks.Add(cb);
            return cb;
        }

        // the requiredItems' array part: what Battlefield:new copies into tradeItems
        public int[] TradeItems => RequiredItems?.Items ?? Array.Empty<int>();
    }

    // BattlefieldMission:new's fields (scripts/globals/battlefield.lua:1406), beside Battlefield:new's. A battlefield
    // whose Data is one is a BattlefieldMission: its hooks' defaults are BattlefieldMission's methods. isMission is
    // always true (the Lua sets it whatever the data says) and canLoseExp defaults to false
    public sealed class BattlefieldMissionData : BattlefieldData
    {
        public BattlefieldMissionData()
        {
            IsMission = true;
            CanLoseExp = false;
        }

        // missionArea: the mission log (null: the Lua's nil, the player's nation); mission: the mission's id in it
        // (required: mission 0 is a real id, so a port that forgets the field must be refused, not match it)
        public MissionLog? MissionArea; public int? Mission;
        // missionStatusArea: the log the status is read from (null: the player's nation); missionStatus: the status
        // position of xi.mission.status (null: the Lua's nil, the whole status word)
        public MissionLog? MissionStatusArea; public int? MissionStatus;
        // requiredMissionStatus: the status to enter (null: none); skipMissionStatus: the status above which the
        // cutscene may be skipped (null: the Lua's nil, the required status)
        public int? RequiredMissionStatus; public int? SkipMissionStatus;
        // requiredVar / requiredValue: a char var to enter by instead of the status (CoP); RequiredVar null for nil.
        // Every Lua battlefield sets both or neither, so RequiredVar stands for both in the Lua's tests
        public string RequiredVar; public int RequiredValue;

        // obj.skipMissionStatus = data.skipMissionStatus or data.requiredMissionStatus
        public int? SkipStatus => SkipMissionStatus ?? RequiredMissionStatus;
    }

    // BattlefieldQuest:new's fields (scripts/globals/battlefield.lua:1519), beside Battlefield:new's. A battlefield
    // whose Data is one is a BattlefieldQuest: its hooks' defaults are BattlefieldQuest's methods. canLoseExp defaults
    // to false
    public sealed class BattlefieldQuestData : BattlefieldData
    {
        public BattlefieldQuestData()
        {
            CanLoseExp = false;
        }

        // questArea: the quest log; quest: the xi.quest.id constant, e.g. "TRIAL_BY_EARTH"
        public QuestLog QuestArea; public string Quest;
        // repeatable: a win also sets battlefieldWin for a char who has completed the quest (the user's ruling of
        // 2026-11-05, Waking the Beast's weekly repeat); false: only while the quest is accepted
        public bool Repeatable;
        // requiredVar / requiredValue: a char var the quest's fight is entered by; RequiredVar null for nil
        public string RequiredVar; public int RequiredValue;
    }

    // requiredItems = { itemId, ..., wearMessage = ID.text.*, wornMessage = ID.text.* | { ID.text.*, args... }, keep }
    public sealed class BattlefieldRequiredItems
    {
        public int[] Items = Array.Empty<int>();
        public ZoneText? WearMessage;
        public ZoneText? WornMessage;
        // wornMessage as a table: { WornMessage, WornMessageArgs... } (null: the plain number)
        public int[] WornMessageArgs;
        public bool Keep;
    }

    // one entry of requiredKeyItems: a key item, or a table of key items any one of which will do (Requiem of Sin)
    public readonly struct BattlefieldKeyItem
    {
        private BattlefieldKeyItem(int[] ids, bool anyOf)
        {
            Ids = ids;
            IsTable = anyOf;
        }

        public int[] Ids { get; }
        // the Lua's type(keyItem) == 'table'
        public bool IsTable { get; }

        public static BattlefieldKeyItem Of(int keyItem) => new BattlefieldKeyItem(new[] { keyItem }, false);
        public static BattlefieldKeyItem AnyOf(params int[] keyItems) => new BattlefieldKeyItem(keyItems, true);
        public static implicit operator BattlefieldKeyItem(int keyItem) => Of(keyItem);
    }

    // requiredKeyItems = { ki, ..., onlyInitiator, keep, message = ID.text.* | { ID.text.*, { args } }, deleteMessage }
    public sealed class BattlefieldRequiredKeyItems
    {
        public List<BattlefieldKeyItem> Items = new();
        public bool OnlyInitiator; public bool Keep;
        public ZoneText? Message;
        // message as a table: { Message, { MessageArgs... } } (null: the plain number)
        public int[] MessageArgs;
        public ZoneText? DeleteMessage;
    }

    // one content:addEssentialMobs(names): its names, and the one field a file sets on the group after the call
    // (royal_jelly.lua:24, content.groups[1].spawned = false). An array converts: EssentialMobs = { new[] { "A" } }
    public sealed class BattlefieldEssentialMobs : IEnumerable<string>
    {
        public string[] Mobs;
        // null: the field left out (the engine's default, true)
        public bool? Spawned;

        public static implicit operator BattlefieldEssentialMobs(string[] mobs) =>
            new BattlefieldEssentialMobs { Mobs = mobs };

        public IEnumerator<string> GetEnumerator() =>
            ((IEnumerable<string>)(Mobs ?? Array.Empty<string>())).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // one entry of a loot group: { itemId = ..., weight = ..., amount = ... }
    public sealed class BattlefieldLootEntry
    {
        public int ItemId; public int Weight;
        // amount: the gil of a gil entry (null: no amount field)
        public int? Amount;
    }

    // one loot group of content.loot: its entries in order, and its quantity field (null: none, one roll)
    public sealed class BattlefieldLootGroup : IEnumerable<BattlefieldLootEntry>
    {
        public List<BattlefieldLootEntry> Entries = new();
        public int? Quantity;

        public void Add(int itemId, int weight) =>
            Entries.Add(new BattlefieldLootEntry { ItemId = itemId, Weight = weight });
        public void Add(int itemId, int weight, int amount) =>
            Entries.Add(new BattlefieldLootEntry { ItemId = itemId, Weight = weight, Amount = amount });

        public IEnumerator<BattlefieldLootEntry> GetEnumerator() => Entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // the lootTable handleLootRolls draws from: C# groups (a battlefield's own pool), or a table the engine holds
    // (self.loot, or the table a Lua caller passed), which only the engine reads. Groups is null for the latter
    public sealed class BattlefieldLootTable
    {
        public BattlefieldLootTable(IReadOnlyList<BattlefieldLootGroup> groups) =>
            Groups = groups ?? throw new ArgumentNullException(nameof(groups));

        private BattlefieldLootTable(object handle) => Handle = handle;

        // a table the engine holds; handle is the engine's own
        public static BattlefieldLootTable OfEngine(object handle) =>
            new BattlefieldLootTable(handle ?? throw new ArgumentNullException(nameof(handle)));

        public IReadOnlyList<BattlefieldLootGroup> Groups { get; }
        public object Handle { get; }
    }

    // an entity id as a battlefield file writes it: zones[zone].mob[Key] + Offset (ID.mob.FODDERCHIEF_VOKDEK + 1)
    public readonly struct BattlefieldMobId
    {
        public BattlefieldMobId(string key, int offset = 0)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Offset = offset;
        }

        public string Key { get; }
        public int Offset { get; }

        public override string ToString() => Offset == 0 ? Key : $"{Key}+{Offset}";
    }

    // an area's armoury crate (armouryCrates = { ID.mob.X + 6, ... }): an id of the zone's mob table plus an offset
    public sealed class BattlefieldArmouryCrate
    {
        public BattlefieldMobId Id;
    }

    // one entry of content.paths: [mob id] = { x, y, z, ... }
    public sealed class BattlefieldPath
    {
        public BattlefieldMobId Mob;
        public double[] Points = Array.Empty<double>();
    }

    // a group callback (allDeath(battlefield, mob), randomDeath(battlefield, mob)): q.Battlefield is the battlefield
    public delegate void BattlefieldGroupCallback(IBattlefieldContext q, IBattlefieldMob mob);
    // death(battlefield, mob, deathCount)
    public delegate void BattlefieldDeathCallback(IBattlefieldContext q, IBattlefieldMob mob, int deathCount);
    // setup(battlefield, mobs): the group's mobs once they are spawned (CLuaBattlefield::addGroups)
    public delegate void BattlefieldSetupCallback(IBattlefieldContext q, IReadOnlyList<IBattlefieldMob> mobs);
    // a zone section's onEventUpdate or onEventFinish handler: (player, csid, option, npc), q.Player and q.Npc
    public delegate void BattlefieldEventHandler(IBattlefieldContext q, int csid, int option);

    // one group of content.groups: the fields CLuaBattlefield::addGroups reads, and its callbacks
    public sealed class BattlefieldGroup
    {
        // mobs: by name; mobIds: by id, or (AreaMobIds) one list per area
        public string[] Mobs;
        public BattlefieldMobId[] MobIds;
        public BattlefieldMobId[][] AreaMobIds;
        public bool Superlink; public bool IsParty;
        // spawned / stationary: null for the field left out (both default to true in the engine)
        public bool? Spawned; public bool? Stationary;
        // superlinkGroup: the group's mobs join this superlink group (null: the field left out)
        public int? SuperlinkGroup;
        public BattlefieldSetupCallback Setup;
        public BattlefieldGroupCallback AllDeath;
        public BattlefieldGroupCallback RandomDeath;
        public BattlefieldDeathCallback Death;
        // the essential group addEssentialMobs made (1 for the first): its allDeath is the framework's
        // handleAllMonstersDefeated; 0 for a group of content.groups
        public int EssentialIndex { get; internal set; }
    }

    // an action a hook returns to the interaction lookup (the Lua Event a section handler returns)
    public interface IBattlefieldAction
    {
        // action:setPriority(n): the same action answered
        IBattlefieldAction SetPriority(int priority);
    }

    public abstract class BattlefieldScript : ContainerScript
    {
        // constName: the xi.battlefield.id constant (CACTUAR_SUAVE); the key is its battlefield.<lower> form
        protected BattlefieldScript(string constName)
        {
            if (string.IsNullOrEmpty(constName))
                throw new ArgumentException("a battlefield needs its constant", nameof(constName));
            ConstName = constName;
        }

        // the xi.battlefield.id constant, e.g. "CACTUAR_SUAVE"
        public string ConstName { get; }
        public override ContainerKind Kind => ContainerKind.Battlefield;
        public abstract BattlefieldData Data { get; }
        // battlefield.<the constant in lower case>
        public override string Key => ContainerKeys.Battlefield(ConstName);

        // the battlefield's (empty) handler core: the bridge's player context is built over one; a battlefield has no
        // states
        public QuestScriptDefinition Core =>
            core_ ??= new QuestScriptBuilder().ToDefinition(QuestScript.HiddenLog, ConstName);
        private QuestScriptDefinition core_;

        // the shared core of the zone handlers no battlefield owns (onEntryTrade, onEntryTrigger, the redirects)
        public static QuestScriptDefinition FrameworkCore { get; } =
            new QuestScriptBuilder().ToDefinition(QuestScript.HiddenLog, "BATTLEFIELD_FRAMEWORK");

        // ---- the Lua framework's methods; each default is the framework's own Lua (BattlefieldFramework) ----
        // The class the Lua file made the content with picks the method, as the Lua's metatable does: the Data type
        // (BattlefieldMissionData: BattlefieldMission's, BattlefieldQuestData: BattlefieldQuest's, else Battlefield's)

        // Battlefield:entryRequirement(player, npc, isRegistrant, trade)
        public virtual bool EntryRequirement(IBattlefieldContext q, bool isRegistrant) =>
            q.Framework.EntryRequirement(isRegistrant);
        // Battlefield:checkRequirements(player, npc, isRegistrant, trade), BattlefieldMission's (:1425),
        // BattlefieldQuest's (:1534)
        public virtual bool CheckRequirements(IBattlefieldContext q, bool isRegistrant) => Data switch
        {
            BattlefieldMissionData => new BattlefieldMission(q).CheckRequirements(isRegistrant),
            BattlefieldQuestData => new BattlefieldQuest(q).CheckRequirements(isRegistrant),
            _ => q.Framework.CheckRequirements(isRegistrant),
        };
        // Battlefield:checkSkipCutscene(player), BattlefieldMission's (:1458), BattlefieldQuest's (:1557)
        public virtual bool CheckSkipCutscene(IBattlefieldContext q) => Data switch
        {
            BattlefieldMissionData => new BattlefieldMission(q).CheckSkipCutscene(),
            BattlefieldQuestData => new BattlefieldQuest(q).CheckSkipCutscene(),
            _ => q.Framework.CheckSkipCutscene(),
        };
        // Battlefield:onEntryEventUpdate(player, csid, option, npc): 1 when entered, else 0
        public virtual int OnEntryEventUpdate(IBattlefieldContext q, int csid, int option) =>
            q.Framework.OnEntryEventUpdate(csid, option);
        // Battlefield:onEventFinishEnter(player, csid, option, npc)
        public virtual void OnEventFinishEnter(IBattlefieldContext q, int csid, int option) =>
            q.Framework.OnEventFinishEnter(csid, option);
        // Battlefield:onEventFinishWin(player, csid, option, npc), BattlefieldMission's (:1488); BattlefieldQuest has
        // none of its own
        public virtual void OnEventFinishWin(IBattlefieldContext q, int csid, int option)
        {
            if (Data is BattlefieldMissionData) new BattlefieldMission(q).OnEventFinishWin(csid, option);
            else q.Framework.OnEventFinishWin(csid, option);
        }
        // content.onExitTrigger(player, npc), else Battlefield.onExitTrigger
        public virtual IBattlefieldAction OnExitTrigger(IBattlefieldContext q) => BattlefieldFramework.OnExitTrigger(q);
        // Battlefield:onExitEventUpdate(player, csid, option, npc)
        public virtual void OnExitEventUpdate(IBattlefieldContext q, int csid, int option) =>
            q.Framework.OnExitEventUpdate(csid, option);
        // Battlefield:onEventFinishLeave(player, csid, option, npc)
        public virtual void OnEventFinishLeave(IBattlefieldContext q, int csid, int option) =>
            q.Framework.OnEventFinishLeave(csid, option);
        // Battlefield:onEventFinishExit(player, csid, option, npc)
        public virtual void OnEventFinishExit(IBattlefieldContext q, int csid, int option) =>
            q.Framework.OnEventFinishExit(csid, option);
        // Battlefield:onEventFinishBattlefield(player, csid, option, npc) (32004 and 32005)
        public virtual void OnEventFinishBattlefield(IBattlefieldContext q, int csid, int option) =>
            q.Framework.OnEventFinishBattlefield(csid, option);
        // Battlefield:setupBattlefield(battlefield)
        public virtual void SetupBattlefield(IBattlefieldContext q) => q.Framework.SetupBattlefield();
        // Battlefield:onBattlefieldInitialize(battlefield)
        public virtual void OnBattlefieldInitialize(IBattlefieldContext q) => q.Framework.OnBattlefieldInitialize();
        // Battlefield:onBattlefieldTick(battlefield, tick)
        public virtual void OnBattlefieldTick(IBattlefieldContext q, int tick) => q.Framework.OnBattlefieldTick(tick);
        // Battlefield:onBattlefieldRegister(player, battlefield)
        public virtual void OnBattlefieldRegister(IBattlefieldContext q) => q.Framework.OnBattlefieldRegister();
        // Battlefield:onBattlefieldStatusChange(battlefield, status)
        public virtual void OnBattlefieldStatusChange(IBattlefieldContext q, int status) =>
            q.Framework.OnBattlefieldStatusChange(status);
        // Battlefield:battlefieldEntry(player, battlefield)
        public virtual void BattlefieldEntry(IBattlefieldContext q) => q.Framework.BattlefieldEntry();
        // Battlefield:onBattlefieldEnter(player, battlefield)
        public virtual void OnBattlefieldEnter(IBattlefieldContext q) => q.Framework.OnBattlefieldEnter();
        // Battlefield:onBattlefieldDestroy(battlefield)
        public virtual void OnBattlefieldDestroy(IBattlefieldContext q) => q.Framework.OnBattlefieldDestroy();
        // Battlefield:onBattlefieldLeave(player, battlefield, leavecode)
        public virtual void OnBattlefieldLeave(IBattlefieldContext q, int leaveCode) =>
            q.Framework.OnBattlefieldLeave(leaveCode);
        // Battlefield:onBattlefieldWin(player, battlefield), BattlefieldMission's (:1470), BattlefieldQuest's (:1562)
        public virtual void OnBattlefieldWin(IBattlefieldContext q)
        {
            switch (Data)
            {
                case BattlefieldMissionData:
                    new BattlefieldMission(q).OnBattlefieldWin();
                    break;
                case BattlefieldQuestData:
                    new BattlefieldQuest(q).OnBattlefieldWin();
                    break;
                default:
                    q.Framework.OnBattlefieldWin();
                    break;
            }
        }
        // Battlefield:onBattlefieldLoss(player, battlefield)
        public virtual void OnBattlefieldLoss(IBattlefieldContext q) => q.Framework.OnBattlefieldLoss();
        // Battlefield:onBattlefieldKick(player)
        public virtual void OnBattlefieldKick(IBattlefieldContext q) => q.Framework.OnBattlefieldKick();
        // Battlefield:handleWipe(battlefield, players)
        public virtual void HandleWipe(IBattlefieldContext q, IReadOnlyList<IBattlefieldPlayer> players) =>
            q.Framework.HandleWipe(players);
        // Battlefield:onBattlefieldWipe(battlefield, players)
        public virtual void OnBattlefieldWipe(IBattlefieldContext q, IReadOnlyList<IBattlefieldPlayer> players) =>
            q.Framework.OnBattlefieldWipe(players);
        // Battlefield:handleAllMonstersDefeated(battlefield, mob)
        public virtual void HandleAllMonstersDefeated(IBattlefieldContext q, IBattlefieldMob mob) =>
            q.Framework.HandleAllMonstersDefeated(mob);
        // Battlefield:handleOpenArmouryCrate(player, npc)
        public virtual void HandleOpenArmouryCrate(IBattlefieldContext q, IBattlefieldNpc npc) =>
            q.Framework.HandleOpenArmouryCrate(npc);
        // Battlefield:handleLootRolls(battlefield, lootTable, npc, gilBonusMod): the crate's call passes self.loot,
        // an override its own pool
        public virtual void HandleLootRolls(IBattlefieldContext q, IBattlefieldView battlefield,
                                            BattlefieldLootTable lootTable, IBattlefieldNpc npc, int gilBonusMod) =>
            q.Framework.HandleLootRolls(battlefield, lootTable, npc, gilBonusMod);

        // ---- coverage ----

        // the hooks of BattlefieldScript a class can override (the virtuals above)
        public static readonly IReadOnlyList<string> HookNames = typeof(BattlefieldScript)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.IsVirtual && !m.IsFinal && m.DeclaringType == typeof(BattlefieldScript) &&
                        m.GetParameters().FirstOrDefault()?.ParameterType == typeof(IBattlefieldContext))
            .Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

        private HashSet<string> overridden_;

        // whether the battlefield's own class (outside XI.Quests: a battlefield file's override, not the framework's
        // BattlefieldMission or BattlefieldQuest) overrides the hook
        public bool Overrides(string hook)
        {
            overridden_ ??= new HashSet<string>(
                HookNames.Where(n => GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(m => m.Name == n && m.GetBaseDefinition().DeclaringType == typeof(BattlefieldScript) &&
                              m.DeclaringType != null &&
                              m.DeclaringType.Assembly != typeof(BattlefieldScript).Assembly)),
                StringComparer.Ordinal);
            return overridden_.Contains(hook);
        }

        // the coverage gate's labels of the battlefield (quest rework C2, spec section 3.8): each hook its class
        // overrides (hook:<Name>), and each part of its data that drives a framework arm: an essential group's
        // allDeath (essentialMobs:<n>), a group callback (group:<n>:allDeath, ...), the required item's wear and worn
        // messages, the required key items' consumption, the loot rolls, each exit NPC's trigger (exit:<npc>), and
        // each deferred callback (callback:<name>, quest rework C3 PR2)
        public IReadOnlyList<string> CoverageLabels()
        {
            var labels = HookNames.Where(Overrides).Select(n => "hook:" + n).ToList();
            var d = Data;
            for (int i = 0; i < d.EssentialMobs.Count; i++) labels.Add($"essentialMobs:{i + 1}");
            for (int i = 0; i < d.Groups.Count; i++)
            {
                if (d.Groups[i].AllDeath != null) labels.Add($"group:{i + 1}:allDeath");
                if (d.Groups[i].Death != null) labels.Add($"group:{i + 1}:death");
                if (d.Groups[i].RandomDeath != null) labels.Add($"group:{i + 1}:randomDeath");
                if (d.Groups[i].Setup != null) labels.Add($"group:{i + 1}:setup");
            }
            if (d.RequiredItems?.WearMessage != null) labels.Add(LabelWear);
            if (d.RequiredItems?.WornMessage != null) labels.Add(LabelWorn);
            if (d.RequiredKeyItems is { Items.Count: > 0 }) labels.Add(LabelKeyItems);
            if (d.Loot.Count > 0) labels.Add(LabelLoot);
            foreach (var npc in d.ExitNpcs ?? Array.Empty<string>()) labels.Add("exit:" + npc);
            foreach (int csid in d.OnEventUpdate.Keys.OrderBy(k => k)) labels.Add(SectionLabel("onEventUpdate", csid));
            foreach (int csid in d.OnEventFinish.Keys.OrderBy(k => k)) labels.Add(SectionLabel("onEventFinish", csid));
            foreach (var cb in d.Callbacks.Where(c => c != null)) labels.Add(cb.Label);
            return labels;
        }

        public static string SectionLabel(string kind, int csid) => $"section:{kind}:{csid}";

        public const string LabelWear = "requiredItems:wear";
        public const string LabelWorn = "requiredItems:worn";
        public const string LabelKeyItems = "requiredKeyItems";
        public const string LabelLoot = "loot";
    }

    // the registration's checks of a battlefield that need no Lua (BattlefieldScript, ZoneHost.BattlefieldScripts adds
    // the engine's: the constant's id, the zone's NPCs and mobs, the texts)
    public static class BattlefieldScriptValidator
    {
        public static IReadOnlyList<string> Validate(BattlefieldScript s)
        {
            var problems = new List<string>();
            BattlefieldData d;
            try
            {
                d = s.Data;
            }
            catch (Exception e)
            {
                problems.Add($"its data threw: {e.Message}");
                return problems;
            }
            if (d == null)
            {
                problems.Add("no data");
                return problems;
            }
            // the bridge, the validator and each hook read Data on their own: an expression body building a new
            // object per read would give each its own lists
            if (!ReferenceEquals(d, s.Data)) problems.Add("Data is not a stored instance: each read built a new one");
            if (d.ZoneId <= 0) problems.Add("no zone");
            if (d.BattlefieldId < 0) problems.Add("no battlefield id");
            if (d.MaxPlayers < 1) problems.Add("maxPlayers below 1");
            if (d.TimeLimit < 1) problems.Add("timeLimit below 1");
            // the entry menu's option mask holds 28 battlefields (268435455)
            if (d.Index < 0 || d.Index > 27) problems.Add($"index {d.Index} outside 0..27");
            if (d.EntryNpcs == null || d.EntryNpcs.Length == 0 || d.EntryNpcs.Any(string.IsNullOrEmpty))
                problems.Add("no entry NPC");
            if (d.ExitNpcs != null && d.ExitNpcs.Any(string.IsNullOrEmpty)) problems.Add("an empty exit NPC name");
            if (d.Area < 0) problems.Add("area below 0");
            if (d.LevelCap < 0) problems.Add("levelCap below 0");
            if (d.DelayToExit < 0) problems.Add("delayToExit below 0");
            if (d is BattlefieldQuestData qd && string.IsNullOrEmpty(qd.Quest)) problems.Add("no quest");
            if (d is BattlefieldMissionData md && md.Mission == null) problems.Add("no mission");
            if (d.RequiredItems is { } items)
            {
                if ((items.WearMessage != null || items.WornMessage != null) && items.Items.Length == 0)
                    problems.Add("requiredItems has messages and no item");
                if (items.WornMessageArgs != null && items.WornMessage == null)
                    problems.Add("wornMessage args with no text");
                foreach (var t in new[] { items.WearMessage, items.WornMessage })
                    if (t is { } text && text.Zone != d.ZoneId)
                        problems.Add($"requiredItems text {text} of another zone");
            }
            if (d.RequiredKeyItems is { } kis)
            {
                if (kis.Items.Any(k => k.Ids == null || k.Ids.Length == 0)) problems.Add("an empty required key item");
                if (kis.MessageArgs != null && kis.Message == null)
                    problems.Add("requiredKeyItems message args with no text");
            }
            for (int i = 0; i < d.EssentialMobs.Count; i++)
                if (d.EssentialMobs[i]?.Mobs == null || d.EssentialMobs[i].Mobs.Length == 0 ||
                    d.EssentialMobs[i].Mobs.Any(string.IsNullOrEmpty))
                    problems.Add($"essential mobs {i + 1}: no mob names");
            for (int i = 0; i < d.Groups.Count; i++)
            {
                var g = d.Groups[i];
                if (g == null)
                {
                    problems.Add($"group {i + 1} is null");
                    continue;
                }
                if ((g.Mobs == null || g.Mobs.Length == 0) && g.MobIds == null && g.AreaMobIds == null)
                    problems.Add($"group {i + 1}: no mobs");
                // addGroups reads mobIds by the arenas (battlefield.lua:1000, hasMultipleAreas = not self.area): one
                // list per area without a fixed area, one flat list with it. The other shape crashes it
                if (g.MobIds != null && d.Area == 0)
                    problems.Add($"group {i + 1}: mobIds without a fixed area (AreaMobIds, one list per area)");
                if (g.AreaMobIds != null && d.Area != 0)
                    problems.Add($"group {i + 1}: AreaMobIds with a fixed area {d.Area} (mobIds, one flat list)");
            }
            for (int i = 0; i < d.Loot.Count; i++)
            {
                var g = d.Loot[i];
                if (g == null || g.Entries.Count == 0) problems.Add($"loot group {i + 1}: no entries");
                else if (g.Entries.Any(e => e.Weight <= 0)) problems.Add($"loot group {i + 1}: a weight below 1");
                if (g?.Quantity is < 1) problems.Add($"loot group {i + 1}: quantity below 1");
            }
            foreach (var (field, value) in d.ProxyFields.OrderBy(f => f.Key, StringComparer.Ordinal))
                if (ProxyValueProblem(value) is { } bad) problems.Add($"proxy field {field}: {bad}");
            foreach (int csid in d.OnEventFinish.Keys.Where(IsFrameworkEvent).OrderBy(k => k))
                problems.Add($"onEventFinish {csid} is the framework's");
            foreach (int csid in d.OnEventUpdate.Keys.Where(IsFrameworkEvent).OrderBy(k => k))
                problems.Add($"onEventUpdate {csid} is the framework's");
            // the callbacks: each named once, by the label's pattern
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < d.Callbacks.Count; i++)
            {
                var cb = d.Callbacks[i];
                if (cb == null) problems.Add($"callback {i + 1} is null");
                else if (!BattlefieldCallback.NamePattern.IsMatch(cb.Name)) problems.Add($"callback name {cb.Name}");
                else if (!names.Add(cb.Name)) problems.Add($"callback {cb.Name} declared twice");
            }
            return problems;
        }

        // Battlefield:register's event ids (battlefield.lua:514-558)
        static bool IsFrameworkEvent(int csid) => csid >= 32000 && csid <= 32005;

        // what the bridge can push; null when fine
        static string ProxyValueProblem(object value) => value switch
        {
            null => "a null value",
            int or double or bool or string => null,
            object[] array => array.Select(ProxyValueProblem).FirstOrDefault(p => p != null),
            Dictionary<string, object> table => table.Values.Select(ProxyValueProblem).FirstOrDefault(p => p != null),
            _ => $"a {value.GetType().Name} value",
        };
    }
}
