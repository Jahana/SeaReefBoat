// XI.Quests -- a context over plain fields, for tests of quest content (quest rework B3a). It implements the handler's
// context, the check's and every facet; reads come from the public dictionaries and properties, and every call that
// does something is recorded in Calls as "<Facet>.<Member>(<args>)", args comma-separated (Player.AddFame(1,10)).
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    public abstract class QuestContextFake : IQuestContext, IQuestCheck, IQuestPlayer, IQuestProgress, IQuestTrade, IQuestText, IQuestSettings
    {
        public readonly List<string> Calls = new List<string>();

        // the quest's vars, by name; Step is the 'Prog' var
        public readonly Dictionary<string, int> Vars = new Dictionary<string, int>();
        // player:getCharVar, and the expiry SetCharVar gave (0: none)
        public readonly Dictionary<string, int> CharVars = new Dictionary<string, int>();
        public readonly Dictionary<string, uint> CharVarExpiry = new Dictionary<string, uint>();
        // player:getLocalVar, and quest:getLocalVar (the quest's own, without its prefix)
        public readonly Dictionary<string, int> PlayerLocalVars = new Dictionary<string, int>();
        public readonly Dictionary<string, int> QuestLocalVars = new Dictionary<string, int>();
        // xi.settings.main
        public readonly Dictionary<string, int> MainSettings = new Dictionary<string, int>();
        // xi.settings.main's fractional values (GIL_RATE 1.5): MainNumber reads them as they are, Main rounds them as the
        // live context does (half away from zero); a name in MainSettings wins
        public readonly Dictionary<string, double> MainNumbers = new Dictionary<string, double>();
        // xi.settings.main's booleans (quest rework P9): MainValue reads them as Lua booleans (MainEquals(name, 1) is false
        // for true), Main / MainNumber as 1 / 0 as the live context does; a name in MainSettings or MainNumbers wins
        public readonly Dictionary<string, bool> MainBools = new Dictionary<string, bool>();
        // xi.settings.map (quest rework P6): a number by name, and the booleans apart (MapFlag is true only for a name
        // MapBools holds true, as the Lua's `== true`); Map reads a boolean as 1 / 0, a name in MapSettings winning
        public readonly Dictionary<string, int> MapSettings = new Dictionary<string, int>();
        public readonly Dictionary<string, bool> MapBools = new Dictionary<string, bool>();
        // the containers' sizes by xi.inv id (quest rework P6): a fresh char's inventory 30, every other container 0
        public readonly Dictionary<int, int> ContainerSizes = new Dictionary<int, int> { [0] = 30 };
        // fame level by fame area
        public readonly Dictionary<int, int> Fame = new Dictionary<int, int>();
        // key item -> held
        public readonly Dictionary<int, bool> KeyItems = new Dictionary<int, bool>();
        public readonly HashSet<int> SeenKeyItems = new HashSet<int>();
        // slot -> item id
        public readonly Dictionary<int, int> Equipment = new Dictionary<int, int>();
        // "<log>/<const>" of the quests the player has completed, and other quests' statuses
        public readonly HashSet<string> CompletedQuests = new HashSet<string>();
        public readonly Dictionary<string, QuestStatus> QuestStatuses = new Dictionary<string, QuestStatus>();

        // the quest's status; a hidden quest's (Hidden) has none: reading it throws, as the live context's does
        public QuestStatus Status
        {
            get => Hidden ? throw new System.InvalidOperationException(HiddenNoStatus) : status_;
            set => status_ = value;
        }
        private QuestStatus status_;
        // a hidden quest's context (quest rework P9, HiddenQuestScript): Status, Begin and DelCurrent throw (HiddenQuest:new
        // has no quest log, id or status), Complete records as for any quest and leaves no status
        public bool Hidden { get; set; }
        internal const string HiddenNoStatus = "a hidden quest has no status (HiddenQuest:new has no quest log or id)";
        public int Step { get => Var("Prog"); set => Vars["Prog"] = value; }
        public bool MustZone { get; set; }
        public int MainLevel { get; set; }
        public int SubLevel { get; set; }
        public double XPos { get; set; }
        public double YPos { get; set; }
        public double ZPos { get; set; }
        public ushort Zone { get; set; }
        public int Csid { get; set; }
        public int Option { get; set; }
        public string OptionText { get; set; }
        public int TriggerAreaId { get; set; }
        // what Trade.Matches, Quest.Complete, GiveKeyItem, GiveItem, GiveCurrency and PopFromQm answer
        public bool TradeResult { get; set; }
        public bool CompleteResult { get; set; } = true;
        public bool GiveResult { get; set; } = true;
        public bool PopResult { get; set; } = true;

        // in a callback with no char (an NPC timer without WithPlayer) Player and Quest throw, and in any callback Trade
        // does, as the live context's player and trade calls do there (RunCallback)
        public IQuestPlayer Player => noPlayer_ ? throw new System.InvalidOperationException("no player in this handler") : this;
        public IQuestProgress Quest =>
            noPlayer_ ? throw new System.InvalidOperationException("no player in this handler")
            : ThisMission != null ? throw new System.InvalidOperationException(MissionNoQuest) : this;
        internal const string MissionNoQuest = "a mission's context has no quest (q.Quest): its own is q.Mission";
        public IQuestTrade Trade => noTrade_ ? throw new System.InvalidOperationException("no trade in this handler") : this;
        private bool noPlayer_, noTrade_;
        public IQuestText Text => this;
        public IQuestSettings Settings => this;
        // the handler's NPC; null (the default) outside NPC handlers. An NPC given here arms its timers into this
        // fake's Timers (QuestNpcFake.Timer)
        public QuestNpcFake Npc
        {
            get => npc_;
            set
            {
                npc_ = value;
                if (value != null) value.Owner = this;
            }
        }
        private QuestNpcFake npc_;
        IQuestNpc IQuestContext.Npc => Npc;
        // the dying mob of an OnMobDeath; null (the default) elsewhere
        public QuestNpcFake Mob
        {
            get => mob_;
            set
            {
                mob_ = value;
                if (value != null) value.Owner = this;
            }
        }
        private QuestNpcFake mob_;
        IQuestNpc IQuestContext.Mob => Mob;
        // an OnMobDeath's optParams (quest rework P7); null (the default) elsewhere
        public QuestMobDeathFake MobDeath { get; set; }
        IQuestMobDeath IQuestContext.MobDeath => MobDeath;
        IQuestPlayerView IQuestCheck.Player => this;
        IQuestProgressView IQuestCheck.Quest =>
            ThisMission != null ? throw new System.InvalidOperationException(MissionNoQuest) : this;

        // quest rework C2: the mission whose check or handler this is (a MissionScript's: its log and xi.mission.id),
        // its state the fake's CurrentMissions, MissionStatuses and MissionVars; null (the default): a quest's context,
        // whose q.Mission is null (and whose q.Quest is this fake; a mission's q.Quest throws, as the live context's
        // does)
        public (MissionLog Log, int Id)? ThisMission
        {
            get => thisMission_;
            set
            {
                thisMission_ = value;
                missionFake_ = value is { } m ? new QuestMissionFake(this, m.Log, m.Id) : null;
            }
        }
        private (MissionLog Log, int Id)? thisMission_;
        private QuestMissionFake missionFake_;
        // q.Mission: null unless ThisMission is set (in a callback with no char it throws, as Player does)
        public QuestMissionFake Mission =>
            noPlayer_ && missionFake_ != null
                ? throw new System.InvalidOperationException("no player in this handler")
                : missionFake_;
        IMissionProgress IQuestContext.Mission => Mission;
        IMissionProgressView IQuestCheck.Mission => Mission;
        // a mission's mustZone (q.Mission.MustZone, SetMustZone) and local vars (q.Mission.LocalVar, SetLocalVar)
        public bool MissionMustZone { get; set; }
        public readonly Dictionary<string, int> MissionLocalVars = new Dictionary<string, int>();

        protected void Record(string facet, string member, params object[] args) =>
            Calls.Add($"{facet}.{member}({string.Join(",", args.Select(a => a?.ToString()))})");
        // Record for the fake's other facets (QuestMissionFake)
        internal void RecordFrom(string facet, string member, params object[] args) => Record(facet, member, args);

        // a reply as "<Kind>(<id>,<args>)" with its modifiers as the Lua calls after it: Event(222):oncePerZone(). An event
        // with options, strings or named fields shows the one option table the bridge sends in place of its args: the
        // positional args at 0.., the options over them, the strings sub-table, the named fields in ParseEvent's order
        // (ProgressEvent(5046,{[0]=3,text_table=0}), Event(113,{[0]=1,strings={[0]='Qgolden'}}))
        public static string Describe(QuestReply r)
        {
            var parts = new List<string>();
            if (r.Text is { } t) parts.Add($"{t.Zone}:{t.Name}");
            else if (r.TextName != null) parts.Add(r.TextName);
            else if (r.Kind is not (QuestReplyKind.Sequence or QuestReplyKind.NoAction)) parts.Add(r.Id.ToString());
            if (r.Options.Count > 0 || r.StringOptions.Count > 0 || r.EventFields.Count > 0) parts.Add(OptionTable(r));
            else parts.AddRange(r.Args.Select(a => a.ToString()));
            if (r.TextFace is int face) parts.Add($"face={face}");
            if (r.Kind == QuestReplyKind.Sequence) parts.AddRange(r.Steps.Select(Describe));
            return $"{r.Kind}({string.Join(",", parts)})" + string.Concat(r.Modifiers.Select(m => ":" + m));
        }

        // a zone-in reply as the bridge returns it to luautils::OnZoneIn (quest rework P8): the cutscene id (956), the table
        // { id, textTable, flags } ({956,0,0}) when the reply sets text_table or flags, or "refused: <why>" when the bridge
        // would refuse it (QuestScriptValidator.ZoneInReplyProblem; nothing plays)
        public static string DescribeZoneIn(QuestReply r) =>
            QuestScriptValidator.ZoneInReplyProblem(r) is { } problem ? "refused: " + problem
            : QuestScriptValidator.ZoneInTable(r) is { } t ? $"{{{t.Id},{t.TextTable},{t.Flags}}}"
            : r.Id.ToString();

        // the option table as Lua writes it: {[0]=1,[2]=7,strings={[0]='a'},text_table=0,canSkip=true}
        static string OptionTable(QuestReply r)
        {
            var entries = new SortedDictionary<int, QuestArg>();
            for (int i = 0; i < r.Args.Count; i++) entries[i] = r.Args[i];
            foreach (var o in r.Options) entries[o.Key] = o.Value;
            var items = entries.Select(kv => $"[{kv.Key}]={kv.Value}").ToList();
            if (r.StringOptions.Count > 0)
                items.Add("strings={" + string.Join(",", r.StringOptions.OrderBy(kv => kv.Key).Select(kv => $"[{kv.Key}]={(QuestArg)kv.Value}")) + "}");
            items.AddRange(r.EventFields.Select(f => f.ToString()));
            return "{" + string.Join(",", items) + "}";
        }

        // records a handler's reply in Calls as "Reply.<Describe>", so a test can assert its modifiers; returns it
        public QuestReply RecordReply(QuestReply r)
        {
            Calls.Add("Reply." + Describe(r));
            return r;
        }

        // ---- the player ----
        public virtual int FameLevel(int fameArea) => Fame.TryGetValue(fameArea, out var v) ? v : 0;
        public virtual bool HasCompletedQuest(QuestLog log, string constName) => CompletedQuests.Contains($"{log}/{constName}");
        public virtual QuestStatus QuestStatus(QuestLog log, string constName) =>
            QuestStatuses.TryGetValue($"{log}/{constName}", out var s) ? s : default;
        public virtual bool HasKeyItem(int keyItem) => KeyItems.TryGetValue(keyItem, out var v) && v;
        public virtual bool SeenKeyItem(int keyItem) => SeenKeyItems.Contains(keyItem);
        public virtual int EquipId(int slot) => Equipment.TryGetValue(slot, out var v) ? v : 0;
        public virtual int CharVar(string name) => CharVars.TryGetValue(name, out var v) ? v : 0;
        // player:getMod(mod): the mods by xi.mod id (0 when not set)
        public Dictionary<int, int> Mods { get; } = new Dictionary<int, int>();
        public virtual int Mod(int mod) => Mods.TryGetValue(mod, out var v) ? v : 0;
        // SetServerVariable(name, value): recorded as Player.SetServerVar(<name>,<value>), the value then held
        public Dictionary<string, int> ServerVars { get; } = new Dictionary<string, int>();
        public virtual void SetServerVar(string name, int value) { Record("Player", "SetServerVar", name, value); ServerVars[name] = value; }
        public virtual void DelKeyItem(int keyItem) { Record("Player", "DelKeyItem", keyItem); KeyItems[keyItem] = false; }
        public virtual void AddFame(int fameArea, int amount) => Record("Player", "AddFame", fameArea, amount);
        public virtual bool AddItem(int item, int qty = 1) { Record("Player", "AddItem", item, qty); return GiveResult; }
        // recorded as Player.AddItemWithExData(<item>,<field>,<value>); answers GiveResult, as AddItem
        public virtual bool AddItemWithExData(int item, string field, int value) { Record("Player", "AddItemWithExData", item, field, value); return GiveResult; }
        // player:getMannequinPose / setMannequinPose: the poses by mannequin item id (0 when not set); SetMannequinPose
        // (recorded as Player.SetMannequinPose(<item>,<race>,<pose>)) sets one
        public Dictionary<int, int> MannequinPoses { get; } = new Dictionary<int, int>();
        public virtual int MannequinPose(int item) => MannequinPoses.TryGetValue(item, out var v) ? v : 0;
        public virtual void SetMannequinPose(int item, int race, int pose) { Record("Player", "SetMannequinPose", item, race, pose); MannequinPoses[item] = pose; }
        public virtual void AddTitle(int title) => Record("Player", "AddTitle", title);
        // recorded as Player.SetTitle(<title>)
        public virtual void SetTitle(int title) => Record("Player", "SetTitle", title);
        public int FreeSlots { get; set; } = 30;
        public virtual int FreeSlotCount => FreeSlots;
        public HashSet<int> Items { get; } = new HashSet<int>();
        // player:hasItem(item): any container (Items, the inventory's, or ItemsElsewhere)
        public virtual bool HasItem(int item) => Items.Contains(item) || ItemsElsewhere.Values.Any(held => held.Contains(item));
        // player:hasItem(item, location): Items are the inventory's (location 0); ItemsElsewhere the other containers', by
        // location
        public Dictionary<int, HashSet<int>> ItemsElsewhere { get; } = new Dictionary<int, HashSet<int>>();
        // an id past the containers reads the inventory, as the engine's (MAX_CONTAINER_ID 18)
        public virtual bool HasItem(int item, int location) =>
            (location >= 0 && location < 18 ? location : 0) == 0
                ? Items.Contains(item)
                : ItemsElsewhere.TryGetValue(location, out var held) && held.Contains(item);
        public int Now { get; set; }
        public virtual int SystemTime => Now;
        // JstMidnight() and player:getID()
        public int NextJstMidnight { get; set; }
        public virtual int JstMidnight => NextJstMidnight;
        public int CharId { get; set; }
        int IQuestPlayerView.Id => CharId;
        public int Tally { get; set; }
        public virtual int NextConquestTally => Tally;
        public int Midnight { get; set; }
        public virtual int VanaMidnight => Midnight;
        public virtual int Main(string name) =>
            MainSettings.TryGetValue(name, out var v) ? v : MainNumbers.TryGetValue(name, out var d) ? (int)System.Math.Round(d, System.MidpointRounding.AwayFromZero)
            : MainBools.TryGetValue(name, out var b) ? (b ? 1 : 0) : 0;
        public virtual double MainNumber(string name) =>
            MainSettings.TryGetValue(name, out var v) ? v : MainNumbers.TryGetValue(name, out var d) ? d
            : MainBools.TryGetValue(name, out var b) ? (b ? 1 : 0) : 0;
        // the setting's Lua type (quest rework P9): a number from MainSettings or MainNumbers, a boolean from MainBools, nil
        // when unset (Lua's nil: MainEquals(name, 0) is false for it, as `nil == 0` is)
        public virtual QuestSetting MainValue(string name) =>
            MainSettings.TryGetValue(name, out var v) ? QuestSetting.Of(v) : MainNumbers.TryGetValue(name, out var d) ? QuestSetting.Of(d)
            : MainBools.TryGetValue(name, out var b) ? QuestSetting.Of(b) : QuestSetting.Nil;
        public virtual int Map(string name) =>
            MapSettings.TryGetValue(name, out var v) ? v : MapBools.TryGetValue(name, out var b) ? (b ? 1 : 0) : 0;
        public virtual bool MapFlag(string name) => MapBools.TryGetValue(name, out var b) && b;
        public virtual int ContainerSize(int container) => ContainerSizes.TryGetValue(container, out var v) ? v : 0;
        // recorded as Player.ChangeContainerSize(<container>,<delta>); the size then moved by delta, held to 0..80 as the
        // engine holds it (the items in it not modelled)
        public virtual void ChangeContainerSize(int container, int delta)
        {
            Record("Player", "ChangeContainerSize", container, delta);
            ContainerSizes[container] = System.Math.Clamp(ContainerSize(container) + delta, 0, 80);
        }
        public virtual void SetCharVar(string name, int value, uint expiry = 0)
        {
            Record("Player", "SetCharVar", name, value, expiry);
            CharVars[name] = value;
            if (expiry != 0) CharVarExpiry[name] = expiry; else CharVarExpiry.Remove(name);
        }
        // recorded as Player.IncrementCharVar(<name>,<value>); the char var then moved by value
        public virtual void IncrementCharVar(string name, int value)
        {
            Record("Player", "IncrementCharVar", name, value);
            CharVars[name] = CharVar(name) + value;
        }
        // player:getLocalVar / setLocalVar (Player.LocalVar / Player.SetLocalVar; the quest's are QuestLocalVar below)
        public virtual int PlayerLocalVar(string name) => PlayerLocalVars.TryGetValue(name, out var v) ? v : 0;
        public virtual void SetPlayerLocalVar(string name, int value) { Record("Player", "SetLocalVar", name, value); PlayerLocalVars[name] = value; }
        int IQuestPlayerView.LocalVar(string name) => PlayerLocalVar(name);
        void IQuestPlayer.SetLocalVar(string name, int value) => SetPlayerLocalVar(name, value);
        public virtual void SetQuestMustZone(QuestLog log, string constName) => Record("Player", "SetQuestMustZone", log, constName);
        public virtual bool GiveKeyItem(int keyItem) { Record("Player", "GiveKeyItem", keyItem); if (GiveResult) KeyItems[keyItem] = true; return GiveResult; }
        // recorded as Player.GiveKeyItem(<keyItem>,<zone>:<NAME>)
        public virtual bool GiveKeyItem(int keyItem, ZoneText message)
        {
            Record("Player", "GiveKeyItem", keyItem, message);
            if (GiveResult) KeyItems[keyItem] = true;
            return GiveResult;
        }
        public virtual bool GiveItem(int item) { Record("Player", "GiveItem", item); return GiveResult; }
        // recorded as Player.GiveItem(<item>,<silent>,<fromTrade>) / Player.GiveItems(<item>x<qty>;...)
        public virtual bool GiveItem(int item, bool silent, bool fromTrade) { Record("Player", "GiveItem", item, silent, fromTrade); return GiveResult; }
        public virtual bool GiveItems(params (int Item, int Qty)[] items)
        {
            Record("Player", "GiveItems", string.Join(";", items.Select(i => $"{i.Item}x{i.Qty}")));
            return GiveResult;
        }
        // player:getMoghouseFlag() / setMoghouseFlag (recorded as Player.SetMoghouseFlag(<flag>), the flag then held)
        public int MhFlag { get; set; }
        public virtual int MoghouseFlag => MhFlag;
        public virtual void SetMoghouseFlag(int flag) { Record("Player", "SetMoghouseFlag", flag); MhFlag = flag; }
        // player:getWeather()
        public int CurrentWeather { get; set; }
        public virtual int Weather => CurrentWeather;
        // player:getZone(true):getWeather()
        public int CurrentZoneWeather { get; set; }
        public virtual int ZoneWeather => CurrentZoneWeather;
        // the status effects the char has (xi.effect ids); AddStatusEffect (recorded as
        // Player.AddStatusEffect(<effect>,<power>,<subPower>,<flag>)) adds to them, DelStatusEffect (recorded as
        // Player.DelStatusEffect(<effect>)) takes from them
        public HashSet<int> StatusEffects { get; } = new HashSet<int>();
        public virtual bool HasStatusEffect(int effect) => StatusEffects.Contains(effect);
        public virtual void AddStatusEffect(int effect, int power, int subPower, int flag)
        {
            Record("Player", "AddStatusEffect", effect, power, subPower, flag);
            StatusEffects.Add(effect);
        }
        // recorded as Player.AddStatusEffectForDuration(<effect>,<power>,<durationSeconds>)
        public virtual void AddStatusEffectForDuration(int effect, int power, int durationSeconds)
        {
            Record("Player", "AddStatusEffectForDuration", effect, power, durationSeconds);
            StatusEffects.Add(effect);
        }
        // recorded as Player.AddStatusEffectForDuration(<effect>,<durationSeconds>)
        public virtual void AddStatusEffectForDuration(int effect, int durationSeconds)
        {
            Record("Player", "AddStatusEffectForDuration", effect, durationSeconds);
            StatusEffects.Add(effect);
        }
        // recorded as Player.DelStatusEffectsByFlag(<flag>,<silent>); the fake keeps no flags: its effects stay
        public virtual void DelStatusEffectsByFlag(int flag, bool silent) =>
            Record("Player", "DelStatusEffectsByFlag", flag, silent);
        public virtual void DelStatusEffect(int effect) { Record("Player", "DelStatusEffect", effect); StatusEffects.Remove(effect); }
        // zones[zone].mob[key]: the IDs.lua mob keys a test sets; an unknown key throws, as the Lua's nil arithmetic does
        public Dictionary<string, int> MobIds { get; } = new Dictionary<string, int>();
        public virtual int MobIdOf(string mobKey) =>
            MobIds.TryGetValue(mobKey, out var id) ? id : throw new System.InvalidOperationException($"unknown mob {mobKey}");
        // zones[zone].npc[key] as a table of ids: the IDs.lua npc lists a test sets; an unknown key throws, as the Lua's
        // index of nil does
        public Dictionary<string, int[]> NpcIdLists { get; } = new Dictionary<string, int[]>();
        public virtual IReadOnlyList<int> NpcIdsOf(string npcKey) =>
            NpcIdLists.TryGetValue(npcKey, out var ids) ? ids : throw new System.InvalidOperationException($"unknown npc list {npcKey}");
        // xi.automaton.getModelId(player)
        public int AutomatonModelId { get; set; }
        public int HP { get; set; }
        public int MP { get; set; }
        public int MainJob { get; set; }
        // the items CanEquipItem answers true for (the level ignored), and the artifact armor
        public HashSet<int> Equippable { get; } = new HashSet<int>();
        public HashSet<int> ArtifactArmor { get; } = new HashSet<int>();
        // recorded as Player.CanEquipItem(<item>,<checkLevel>), so a test can tell the level check from none
        public virtual bool CanEquipItem(int item, bool checkLevel)
        {
            Record("Player", "CanEquipItem", item, checkLevel);
            return Equippable.Contains(item);
        }
        public virtual bool IsArtifactArmor(int item) => ArtifactArmor.Contains(item);
        // "<log>/<const>" of the other quests whose mustZone is set, and "<missionLog>/<mission>" of the completed missions
        public readonly HashSet<string> QuestMustZones = new HashSet<string>();
        public readonly HashSet<string> CompletedMissions = new HashSet<string>();
        public virtual bool QuestMustZone(QuestLog log, string constName) => QuestMustZones.Contains($"{log}/{constName}");
        public virtual bool HasCompletedMission(int missionLog, int mission) => CompletedMissions.Contains($"{missionLog}/{mission}");
        // the zones whose maw is unlocked (HasUnlockedMaw reads it)
        public readonly HashSet<int> UnlockedMaws = new HashSet<int>();
        // the real one reads the PAST_MAW teleport bit, which a maw and its [S] twin share (scripts/globals/maws.lua:
        // Batallia Downs 105 / [S] 84 bit 0, Rolanberry Fields 110 / [S] 91 bit 1, Sauromugue Champaign 120 / [S] 98
        // bit 2): a held zone unlocks its twin; any other zone is its own bit
        static int MawBit(int zone) => zone switch
        {
            105 or 84 => 0, 110 or 91 => 1, 120 or 98 => 2, _ => 1000 + zone
        };
        public virtual bool HasUnlockedMaw(int zone) => UnlockedMaws.Any(u => MawBit(u) == MawBit(zone));
        // VanadielTOTD(), #player:getParty() and player:getAlliance()'s members (each one's level and distance)
        public int Totd { get; set; }
        public virtual int VanadielTotd => Totd;
        // xi.besieged.getMercenaryRank(player)
        public int MercRank { get; set; }
        public virtual int MercenaryRank => MercRank;
        // xi.abyssea.getTraverserCap(player)
        public int TraverserCapValue { get; set; } = 3;
        public virtual int TraverserCap => TraverserCapValue;
        // xi.abyssea.getHeldTraverserStones(player) and xi.abyssea.getZoneKIReward(player)
        public int HeldTraverserStonesValue { get; set; }
        public virtual int HeldTraverserStones => HeldTraverserStonesValue;
        public int AbysseaZoneKIRewardValue { get; set; }
        public virtual int AbysseaZoneKIReward => AbysseaZoneKIRewardValue;
        // recorded as Player.SetTraverserEpoch()
        public virtual void SetTraverserEpoch() => Record("Player", "SetTraverserEpoch");
        // recorded as Player.DominionOnMobDeath(<opId>)
        public virtual void DominionOnMobDeath(int opId) => Record("Player", "DominionOnMobDeath", opId);
        // player:getCurrency(name): CurrencyHeld, which AddCurrency moves
        // (recorded as Player.AddCurrency(<name>,<amount>))
        public Dictionary<string, int> CurrencyHeld { get; } = new Dictionary<string, int>();
        public virtual int Currency(string currency) => CurrencyHeld.TryGetValue(currency, out var v) ? v : 0;
        public virtual void AddCurrency(string currency, int amount)
        {
            Record("Player", "AddCurrency", currency, amount);
            CurrencyHeld[currency] = Currency(currency) + amount;
        }
        // recorded as Player.DelCurrency(<name>,<amount>); CurrencyHeld goes down by it
        public virtual void DelCurrency(string currency, int amount)
        {
            Record("Player", "DelCurrency", currency, amount);
            CurrencyHeld[currency] = Currency(currency) - amount;
        }
        public int Party { get; set; } = 1;
        public virtual int PartySize => Party;
        // the alliance is the char (its MainLevel, distance 0), as a solo char's live getAlliance() is, then AllianceMembers:
        // the other members a test adds
        public List<(int MainLevel, double Distance)> AllianceMembers { get; } = new List<(int MainLevel, double Distance)>();
        public virtual IReadOnlyList<(int MainLevel, double Distance)> Alliance =>
            new[] { (MainLevel, 0.0) }.Concat(AllianceMembers).ToList();
        // VanadielTime()
        public int VanaTimeNow { get; set; }
        public virtual int VanadielTime => VanaTimeNow;
        // ChocoboNameStrings: the names a test sets ('' each without a raised chocobo)
        public (string Full, string First, string Last) ChocoboNames { get; set; } = ("", "", "");
        public virtual (string Full, string First, string Last) ChocoboNameStrings => ChocoboNames;
        // getAlliance()'s members' char vars (quest rework P7): the char's own CharVars first, then one dictionary per other
        // member a test adds (a name it lacks reads 0, as getCharVar does)
        public List<Dictionary<string, int>> AllianceMemberCharVars { get; } = new List<Dictionary<string, int>>();
        public virtual IReadOnlyList<int> AllianceCharVars(string name) =>
            new[] { CharVar(name) }.Concat(AllianceMemberCharVars.Select(m => m.TryGetValue(name, out var v) ? v : 0)).ToList();
        // VanadielUniqueDay()
        public int UniqueDay { get; set; }
        public virtual int VanadielUniqueDay => UniqueDay;
        // utils.vanadielClockTime(): hhmm
        public int ClockTime { get; set; }
        public virtual int VanadielClockTime => ClockTime;
        // VanadielRSELocation() / VanadielRSERace(): the week's values a test sets
        public int RseLocation { get; set; }
        public int RseRace { get; set; }
        public virtual int VanadielRseLocation => RseLocation;
        public virtual int VanadielRseRace => RseRace;
        // xi.equipment.vigilWeaponRequiredWsPoints(player): the global's 250 unless a test sets it (an era override)
        public int VigilWsPoints { get; set; } = 250;
        public virtual int VigilWeaponRequiredWsPoints => VigilWsPoints;
        // zones[zone].npc[key]: the IDs.lua npc keys a test sets; an unknown key throws, as the Lua's nil arithmetic does
        public Dictionary<string, int> NpcIds { get; } = new Dictionary<string, int>();
        public virtual int NpcIdOf(string npcKey) =>
            NpcIds.TryGetValue(npcKey, out var id) ? id : throw new System.InvalidOperationException($"unknown npc {npcKey}");
        // player:getPreviousZone()
        public int PrevZone { get; set; }
        public virtual int PreviousZone => PrevZone;
        // VanadielHour()
        public int Hour { get; set; }
        public virtual int VanadielHour => Hour;
        // getVanadielMoonCycle()
        public int MoonCycle { get; set; }
        public virtual int VanadielMoonCycle => MoonCycle;
        // player:getName()
        public string CharName { get; set; } = "";
        public virtual string Name => CharName;
        // player:getZone():getLocalVar / setLocalVar (SetZoneLocalVar recorded as Player.SetZoneLocalVar(<name>,<value>))
        public Dictionary<string, int> ZoneLocalVars { get; } = new Dictionary<string, int>();
        public virtual int ZoneLocalVar(string name) => ZoneLocalVars.TryGetValue(name, out var v) ? v : 0;
        public virtual void SetZoneLocalVar(string name, int value) { Record("Player", "SetZoneLocalVar", name, value); ZoneLocalVars[name] = value; }
        // GetPlayerByName / GetPlayerByID: the chars online a test adds, as (id, zone id) by name
        public Dictionary<string, (int Id, int ZoneId)> OnlinePlayers { get; } = new Dictionary<string, (int Id, int ZoneId)>();
        public virtual (int Id, int ZoneId)? PlayerByName(string name) => OnlinePlayers.TryGetValue(name, out var p) ? p : null;
        public virtual (int Id, int ZoneId)? PlayerById(int id)
        {
            foreach (var p in OnlinePlayers.Values) if (p.Id == id) return p;
            return null;
        }
        // player:needToZone() / needToZone(value): SetNeedToZone is recorded as Player.SetNeedToZone(<value>)
        public bool ZoningFlag { get; set; }
        public virtual bool NeedToZone => ZoningFlag;
        public virtual void SetNeedToZone(bool value) { Record("Player", "SetNeedToZone", value); if (value) ZoningFlag = true; }
        // player:getGender() (1 male, the default; 0 female)
        public int GenderId { get; set; } = 1;
        public virtual int Gender => GenderId;
        // player:getLevelCap() / setLevelCap (recorded as Player.SetLevelCap(<cap>), the cap then held)
        public int LevelCap { get; set; }
        public virtual void SetLevelCap(int cap) { Record("Player", "SetLevelCap", cap); LevelCap = cap; }
        // player:getBattlefield():getID(), null when the char is in no battlefield
        public int? BattlefieldId { get; set; }
        // player:getMeritCount() / setMerits (recorded as Player.SetMerits(<merits>), the points then held)
        public int MeritCount { get; set; }
        public virtual void SetMerits(int merits) { Record("Player", "SetMerits", merits); MeritCount = merits; }
        // player:getCampaignAllegiance() / setCampaignAllegiance (recorded as Player.SetCampaignAllegiance(<nation>), the
        // allegiance then held)
        public int Allegiance { get; set; }
        public virtual int CampaignAllegiance => Allegiance;
        public virtual void SetCampaignAllegiance(int nation) { Record("Player", "SetCampaignAllegiance", nation); Allegiance = nation; }
        // xi.moghouse.inMogHouseInHomeNation(player)
        public bool InMogHouseHome { get; set; }
        public virtual bool InMogHouseInHomeNation => InMogHouseHome;
        // player:inMogHouse()
        public bool InAMogHouse { get; set; }
        public virtual bool InMogHouse => InAMogHouse || InMogHouseHome;
        // xi.monstrosity.hasUnlockedSpecies(player, species): the species held
        public HashSet<int> UnlockedSpecies { get; } = new HashSet<int>();
        public virtual bool MonstrosityHasUnlockedSpecies(int species) => UnlockedSpecies.Contains(species);
        // player:getPos(), and player:setPos (recorded as Player.SetPos(<x>,<y>,<z>,<rot>,<zone>), the numbers written
        // invariantly; the position then that)
        public (double X, double Y, double Z, int Rot) Position { get; set; }
        public virtual (double X, double Y, double Z, int Rot) Pos => Position;
        public virtual void SetPos(double x, double y, double z, int rot, int zone)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Record("Player", "SetPos", x.ToString(inv), y.ToString(inv), z.ToString(inv), rot, zone);
            Position = (x, y, z, rot);
        }
        // player:findItem(item): any container (Items, the inventory's, or ItemsElsewhere), as HasItem(item)
        public virtual bool FindItem(int item) => Items.Contains(item) || ItemsElsewhere.Values.Any(held => held.Contains(item));
        // player:checkDistance(x, y, z): from XPos, YPos and ZPos, x, y and z all counting (the binding's form), in float
        // as the engine measures it (quest rework P9: it was double)
        public virtual double DistanceTo(double x, double y, double z)
        {
            float dx = (float)XPos - (float)x, dy = (float)YPos - (float)y, dz = (float)ZPos - (float)z;
            return System.MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        // player:getCharSkillLevel: the stored values (10x the level), by skill
        public Dictionary<int, int> Skills { get; } = new Dictionary<int, int>();
        public virtual int CharSkillLevel(int skill) => Skills.TryGetValue(skill, out var v) ? v : 0;
        // player:getSkillLevel: the working skills (the level, not 10x), by skill
        public Dictionary<int, int> WorkingSkills { get; } = new Dictionary<int, int>();
        public virtual int SkillLevel(int skill) => WorkingSkills.TryGetValue(skill, out var v) ? v : 0;
        // the spells known; AddSpell (recorded as Player.AddSpell(<spell>)) adds to them
        public HashSet<int> Spells { get; } = new HashSet<int>();
        public virtual bool HasSpell(int spell) => Spells.Contains(spell);
        public virtual void AddSpell(int spell) { Record("Player", "AddSpell", spell); Spells.Add(spell); }
        // AddSpell with its options table (recorded as Player.AddSpell(<spell>,<silentLog>)) adds to them too
        public virtual void AddSpell(int spell, bool silentLog) { Record("Player", "AddSpell", spell, silentLog); Spells.Add(spell); }
        // NextJstDay()
        public int JstDay { get; set; }
        public virtual int NextJstDay => JstDay;
        // recorded as Player.AddLearnedWeaponskill(<wsUnlock>) / Player.TeleportTo(<destination>)
        public virtual void AddLearnedWeaponskill(int wsUnlock) => Record("Player", "AddLearnedWeaponskill", wsUnlock);
        public virtual void TeleportTo(int destination) => Record("Player", "TeleportTo", destination);
        // player:getNation()
        public int NationId { get; set; }
        public virtual int Nation => NationId;
        // player:getRank(nation): the ranks by nation, 1 (a fresh char's) for a nation not set
        public Dictionary<int, int> Ranks { get; } = new Dictionary<int, int>();
        public virtual int Rank(int nation) => Ranks.TryGetValue(nation, out var v) ? v : 1;
        // player:setRank(rank): recorded as Player.SetRank(<rank>), the rank of the char's nation then that
        public virtual void SetRank(int rank) { Record("Player", "SetRank", rank); Ranks[Nation] = rank; }
        // player:getRankPoints() / setRankPoints (recorded as Player.SetRankPoints(<points>), the points then held)
        public int RankPointsHeld { get; set; }
        public virtual int RankPoints => RankPointsHeld;
        public virtual void SetRankPoints(int points)
        {
            Record("Player", "SetRankPoints", points);
            RankPointsHeld = points;
        }
        // player:addRankPoints (recorded as Player.AddRankPoints(<points>), the points then held moved by it)
        public virtual void AddRankPoints(int points)
        {
            Record("Player", "AddRankPoints", points);
            RankPointsHeld += points;
        }
        // player:getGil(): GilHeld, which AddGil / DelGil (recorded as Player.AddGil(<amount>) / Player.DelGil(<amount>)) move
        public int GilHeld { get; set; }
        public virtual int Gil => GilHeld;
        public virtual void AddGil(int amount) { Record("Player", "AddGil", amount); GilHeld += amount; }
        public virtual void DelGil(int amount) { Record("Player", "DelGil", amount); GilHeld -= amount; }
        // recorded as Player.DelGilChecked(<amount>); takes the gil and answers true when GilHeld covers it (a negative
        // amount answers false, as the engine's)
        public virtual bool DelGilChecked(int amount)
        {
            Record("Player", "DelGilChecked", amount);
            if (amount < 0 || GilHeld < amount) return false;
            GilHeld -= amount;
            return true;
        }
        // recorded as Player.SetPos(<x>,<y>,<z>), the numbers written invariantly; the position then that, the facing
        // kept (player:setPos(x, y, z) leaves the rotation as it was)
        public virtual void SetPos(double x, double y, double z)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Record("Player", "SetPos", x.ToString(inv), y.ToString(inv), z.ToString(inv));
            Position = (x, y, z, Position.Rot);
        }
        // recorded as Player.SetPos(<x>,<y>,<z>,<rot>), the numbers written invariantly; the position then that
        // of the four-argument call, the facing set with it
        public virtual void SetPos(double x, double y, double z, int rot)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Record("Player", "SetPos", x.ToString(inv), y.ToString(inv), z.ToString(inv), rot);
            Position = (x, y, z, rot);
        }
        // recorded as Player.ShowText(<zone>:<NAME>,args)
        public virtual void ShowText(ZoneText text, params int[] args) =>
            Record("Player", "ShowText", new object[] { text }.Concat(args.Cast<object>()).ToArray());
        // player:hasJob(job): the unlocked jobs
        public HashSet<int> UnlockedJobs { get; } = new HashSet<int>();
        public virtual bool HasJob(int job) => UnlockedJobs.Contains(job);
        // player:getCurrentMission: the current mission by mission log (0 when not set); xi.mission.getVar: the vars, keyed
        // "<missionLog>/<mission>/<name>". The 0 for an unset log is the fake's default only: the live server
        // (LiveQuestContext, player:getCurrentMission) returns the char's real current mission, which need not be 0.
        public Dictionary<int, int> CurrentMissions { get; } = new Dictionary<int, int>();
        public Dictionary<string, int> MissionVars { get; } = new Dictionary<string, int>();
        public virtual int CurrentMission(int missionLog) => CurrentMissions.TryGetValue(missionLog, out var v) ? v : 0;
        public virtual int MissionVar(int missionLog, int mission, string name) =>
            MissionVars.TryGetValue($"{missionLog}/{mission}/{name}", out var v) ? v : 0;
        // player:getMissionStatus: by mission log (0 when not set); SetMissionStatus moves it (recorded as
        // Player.SetMissionStatus(<log>,<status>))
        public Dictionary<int, int> MissionStatuses { get; } = new Dictionary<int, int>();
        public virtual int MissionStatus(int missionLog) => MissionStatuses.TryGetValue(missionLog, out var v) ? v : 0;
        // the 4-bit field at pos of the same status word, as the engine reads it (status >> 4 * pos & 0xF); a pos
        // past 7 answers 0, as the engine does
        public virtual int MissionStatus(int missionLog, int pos) =>
            pos is < 0 or > 7 ? 0 : (int)((uint)MissionStatus(missionLog) >> 4 * pos & 0xF);
        public virtual void SetMissionStatus(int missionLog, int status)
        {
            Record("Player", "SetMissionStatus", missionLog, status);
            MissionStatuses[missionLog] = status;
        }
        // recorded as Player.AddMission(<log>,<mission>) (then current) / Player.DelMission(<log>,<mission>) (the log's
        // current cleared when it was that mission)
        public virtual void AddMission(int missionLog, int mission)
        {
            Record("Player", "AddMission", missionLog, mission);
            CurrentMissions[missionLog] = mission;
        }
        public virtual void DelMission(int missionLog, int mission)
        {
            Record("Player", "DelMission", missionLog, mission);
            if (CurrentMissions.TryGetValue(missionLog, out var current) && current == mission) CurrentMissions.Remove(missionLog);
            CompletedMissions.Remove($"{missionLog}/{mission}");
        }
        // recorded as Player.CompleteMission(<log>,<mission>,<nextLog>,<nextMission>); the next mission then current (the
        // log's, as AddMission leaves it); answers true, as npcUtil.completeMission does without an item
        public virtual bool CompleteMission(int missionLog, int mission, int nextMissionLog, int nextMission)
        {
            Record("Player", "CompleteMission", missionLog, mission, nextMissionLog, nextMission);
            if (CurrentMissions.TryGetValue(missionLog, out var current) && current == mission) CurrentMissions.Remove(missionLog);
            CurrentMissions[nextMissionLog] = nextMission;
            return true;
        }
        // recorded as Player.SetMissionMustZone(<log>,<mission>)
        public virtual void SetMissionMustZone(int missionLog, int mission) =>
            Record("Player", "SetMissionMustZone", missionLog, mission);
        // recorded as Player.SetMissionVar(<log>,<mission>,<name>,<value>) (then read by MissionVar)
        public virtual void SetMissionVar(int missionLog, int mission, string name, int value)
        {
            Record("Player", "SetMissionVar", missionLog, mission, name, value);
            MissionVars[$"{missionLog}/{mission}/{name}"] = value;
        }
        // recorded as Player.AddKeyItem(<keyItem>) (the key item then held) / Player.StartEvent(<csid>,<arg>,...)
        public virtual void AddKeyItem(int keyItem) { Record("Player", "AddKeyItem", keyItem); KeyItems[keyItem] = true; }
        public virtual void StartEvent(int csid, params int[] args) =>
            Record("Player", "StartEvent", new object[] { csid }.Concat(args.Cast<object>()).ToArray());
        // recorded as Player.StartCutscene(<csid>,<arg>,...)
        public virtual void StartCutscene(int csid, params int[] args) =>
            Record("Player", "StartCutscene", new object[] { csid }.Concat(args.Cast<object>()).ToArray());
        // recorded as Player.StartEventString(<csid>,<s1>,<s2>,<s3>,<s4>,<arg>,...)
        public virtual void StartEventString(int csid, string s1, string s2, string s3, string s4, params int[] args) =>
            Record("Player", "StartEventString",
                new object[] { csid, s1, s2, s3, s4 }.Concat(args.Cast<object>()).ToArray());
        // recorded as Player.SetChocoboWhistleProgress(<progress>) / Player.GiveChocoboQuestWhistle() (true unless
        // GiveChocoboQuestWhistleFails) / Player.MapsOnEventUpdate(<csid>,<option>)
        public virtual void SetChocoboWhistleProgress(int progress)
            => Record("Player", "SetChocoboWhistleProgress", progress);
        public bool GiveChocoboQuestWhistleFails { get; set; }
        public virtual bool GiveChocoboQuestWhistle()
        {
            Record("Player", "GiveChocoboQuestWhistle");
            return !GiveChocoboQuestWhistleFails;
        }
        // recorded as Player.CheckMemoryFragments()
        public virtual void CheckMemoryFragments() => Record("Player", "CheckMemoryFragments");
        public virtual void MapsOnEventUpdate(int csid, int option)
            => Record("Player", "MapsOnEventUpdate", csid, option);
        // player:hasTitle(title): the titles held
        public HashSet<int> Titles { get; } = new HashSet<int>();
        public virtual bool HasTitle(int title) => Titles.Contains(title);
        // player:getPlaytime()
        public int PlaytimeSeconds { get; set; }
        public virtual int Playtime => PlaytimeSeconds;
        // player:getRace()
        public int RaceId { get; set; }
        public virtual int Race => RaceId;
        // the party is the char (its zone, distance 0, nation, race and main job), as a solo char's live getParty() is, then
        // PartyMemberRows: the other members a test adds
        public List<(int ZoneId, double Distance, int Nation, int Race, int MainJob)> PartyMemberRows { get; } =
            new List<(int ZoneId, double Distance, int Nation, int Race, int MainJob)>();
        public virtual IReadOnlyList<(int ZoneId, double Distance, int Nation, int Race, int MainJob)> PartyMembers =>
            new[] { ((int)Zone, 0.0, Nation, Race, MainJob) }.Concat(PartyMemberRows).ToList();
        // recorded as Player.SetQuestVar(<log>,<const>,<name>,<value>)
        public virtual void SetQuestVar(QuestLog log, string constName, string name, int value) => Record("Player", "SetQuestVar", log, constName, name, value);
        // the mobs spawned and alive, keyed "<mobName>+<offset>"; SpawnMob (recorded as Player.SpawnMob(<name>,<offset>,<claim>))
        // adds to both
        public HashSet<string> SpawnedMobs { get; } = new HashSet<string>();
        public HashSet<string> AliveMobs { get; } = new HashSet<string>();
        public virtual bool MobIsSpawned(string mobName, int offset = 0) => SpawnedMobs.Contains($"{mobName}+{offset}");
        public virtual bool MobIsAlive(string mobName, int offset = 0) => AliveMobs.Contains($"{mobName}+{offset}");
        // isDead: not alive (a mob never spawned counts as dead, as CBattleEntity::isDead has it)
        public virtual bool MobIsDead(string mobName, int offset = 0) => !AliveMobs.Contains($"{mobName}+{offset}");
        public virtual void SpawnMob(string mobName, int offset, bool claim)
        {
            Record("Player", "SpawnMob", mobName, offset, claim);
            SpawnedMobs.Add($"{mobName}+{offset}");
            AliveMobs.Add($"{mobName}+{offset}");
        }
        // DespawnMob(name + offset): recorded as Player.DespawnMob(<name>,<offset>); the mob is no longer spawned or
        // alive
        public virtual void DespawnMob(string mobName, int offset = 0)
        {
            Record("Player", "DespawnMob", mobName, offset);
            SpawnedMobs.Remove($"{mobName}+{offset}");
            AliveMobs.Remove($"{mobName}+{offset}");
        }
        // SpawnMob(name) with no claim or enmity: recorded as Player.SpawnMob(<name>), and spawned and alive at offset 0
        public virtual void SpawnMob(string mobName)
        {
            Record("Player", "SpawnMob", mobName);
            SpawnedMobs.Add($"{mobName}+0");
            AliveMobs.Add($"{mobName}+0");
        }
        // zone entities by name (Player.MobIdByName): a test sets them; an unknown name throws, as the Lua's index of
        // nil does
        public Dictionary<string, int> MobIdsByName { get; } = new Dictionary<string, int>();
        public virtual int MobIdByName(string name) =>
            MobIdsByName.TryGetValue(name, out var id) ? id : throw new System.InvalidOperationException($"unknown entity {name}");
        // the mob ids spawned (SpawnMobById adds); MobIsSpawnedById reads it
        public HashSet<int> SpawnedMobIds { get; } = new HashSet<int>();
        public virtual bool MobIsSpawnedById(int id) => SpawnedMobIds.Contains(id);
        public virtual void SpawnMobById(int id, bool claim)
        {
            Record("Player", "SpawnMobById", id, claim);
            SpawnedMobIds.Add(id);
        }
        // recorded as Player.SpawnMobWithNpcCooldown(<name>,<npcId>,<npcVar>,<seconds>); spawned and alive at offset 0 and
        // true, unless SpawnResult is false (SpawnMob's nil)
        public bool SpawnResult { get; set; } = true;
        public virtual bool SpawnMobWithNpcCooldown(string mobName, int npcId, string npcVar, int seconds)
        {
            Record("Player", "SpawnMobWithNpcCooldown", mobName, npcId, npcVar, seconds);
            if (!SpawnResult) return false;
            SpawnedMobs.Add($"{mobName}+0");
            AliveMobs.Add($"{mobName}+0");
            return true;
        }
        // recorded as Player.SetMonstrosityData(<monstrosityId>,<species>)
        public virtual void SetMonstrosityData(int monstrosityId, int species) =>
            Record("Player", "SetMonstrosityData", monstrosityId, species);
        // recorded as Player.MonstrosityUpdateIntroEvent()
        public virtual void MonstrosityUpdateIntroEvent() => Record("Player", "MonstrosityUpdateIntroEvent");
        // recorded as Player.SendMenu(<menuType>)
        public virtual void SendMenu(int menuType) => Record("Player", "SendMenu", menuType);
        // recorded as Player.SurvivalGuideOnTrigger()
        public virtual void SurvivalGuideOnTrigger() => Record("Player", "SurvivalGuideOnTrigger");
        // recorded as Player.MonstrositySuibhneGreet()
        public virtual void MonstrositySuibhneGreet() => Record("Player", "MonstrositySuibhneGreet");
        // recorded as Player.MonstrosityUnlockSpecies(<species>); the species is then held
        public virtual void MonstrosityUnlockSpecies(int species)
        {
            Record("Player", "MonstrosityUnlockSpecies", species);
            UnlockedSpecies.Add(species);
        }
        // recorded as Player.SetHomePoint()
        public virtual void SetHomePoint() => Record("Player", "SetHomePoint");
        // recorded as Player.StartEventFlags(<csid>,<flags>,<isHidden>)
        public virtual void StartEventFlags(int csid, uint flags, bool isHidden) =>
            Record("Player", "StartEventFlags", csid, flags, isHidden);
        // recorded as Player.StartEventHidden(<csid>)
        public virtual void StartEventHidden(int csid) => Record("Player", "StartEventHidden", csid);
        // recorded as Player.EnableEntities(<id;id;...>) (the ids joined by ';', none for the empty table)
        public virtual void EnableEntities(params int[] ids) =>
            Record("Player", "EnableEntities", string.Join(";", ids));
        // recorded as Player.SendPartialMissionLog(<missionLog>,<completed>)
        public virtual void SendPartialMissionLog(int missionLog, bool completed) =>
            Record("Player", "SendPartialMissionLog", missionLog, completed);

        // ---- deferred callbacks (quest rework P10) ----

        // a timer armed and not yet fired: on the char (Npc null) or on an NPC, its deadline NowMs + ms at the arming,
        // in arming order (Order) among equal deadlines
        public sealed class TimerFake
        {
            public QuestNpcFake Npc { get; internal set; }
            public int Ms { get; internal set; }
            public long Deadline { get; internal set; }
            public QuestCallback Callback { get; internal set; }
            public bool WithPlayer { get; internal set; }
            internal long Order;

            // Player.Timer(1000,toWhitegate) / Npc.Timer(5000,chandelier[,WithPlayer]), as Calls records the arming
            public override string ToString() =>
                Npc == null ? $"Player.Timer({Ms},{Callback.Name})" : $"Npc.Timer({Ms},{Callback.Name}{(WithPlayer ? ",WithPlayer" : "")})";
        }

        // a listener added and not removed: its event, identifier and callback, in adding order
        public sealed class ListenerFake
        {
            public string Event { get; internal set; }
            public string Identifier { get; internal set; }
            public QuestCallback Callback { get; internal set; }

            public override string ToString() => $"{Event}/{Identifier}/{Callback.Name}";
        }

        // the timers armed and not fired, the listeners added and not removed
        public readonly List<TimerFake> Timers = new List<TimerFake>();
        public readonly List<ListenerFake> Listeners = new List<ListenerFake>();
        // the fake's clock for the timers: an arming's deadline is NowMs + ms; FireTimers moves it
        public long NowMs { get; set; }
        // the quest whose callbacks this fake may arm
        // (set it to refuse a callback of another quest, as the bridge does)
        public QuestScriptDefinition Definition { get; set; }
        private long timerOrder_;

        // recorded as Player.Timer(<ms>,<callback>)
        public virtual void Timer(int ms, QuestCallback callback) => ArmTimer(null, ms, callback, false);

        internal void ArmTimer(QuestNpcFake npc, int ms, QuestCallback callback, bool withPlayer)
        {
            CheckCallback(callback);
            if (ms < 0) throw new System.ArgumentOutOfRangeException(nameof(ms), ms, "a timer's delay is 0 ms or more (C++ would fire it at once)");
            var t = new TimerFake { Npc = npc, Ms = ms, Deadline = NowMs + ms, Callback = callback, WithPlayer = withPlayer, Order = timerOrder_++ };
            Calls.Add(t.ToString());
            Timers.Add(t);
        }

        private void CheckCallback(QuestCallback callback)
        {
            if (callback == null) throw new System.ArgumentNullException(nameof(callback));
            if (Definition != null && !Definition.Callbacks.Contains(callback))
                throw new System.InvalidOperationException($"callback {callback.Name} is not this quest's");
        }

        // recorded as Player.AddListener(<evt>,<identifier>,<callback>); an identifier added again replaces the first
        public virtual void AddListener(string evt, string identifier, QuestCallback callback)
        {
            CheckCallback(callback);
            if (evt != "TICK") throw new System.ArgumentException($"a quest listener's event is TICK only, not {evt}", nameof(evt));
            if (identifier == null) throw new System.ArgumentNullException(nameof(identifier));
            Record("Player", "AddListener", evt, identifier, callback.Name);
            Listeners.RemoveAll(x => x.Identifier == identifier);
            Listeners.Add(new ListenerFake { Event = evt, Identifier = identifier, Callback = callback });
        }

        // recorded as Player.RemoveListener(<identifier>)
        public virtual void RemoveListener(string identifier)
        {
            Record("Player", "RemoveListener", identifier);
            Listeners.RemoveAll(x => x.Identifier == identifier);
        }

        // GetNPCByID(id) (B3b Deferred 1): the fake NPCs by id; an id not there is nil (null). A fake given here arms
        // its timers into this fake's Timers, as the handler's Npc does
        public readonly Dictionary<int, QuestNpcFake> NpcsById = new Dictionary<int, QuestNpcFake>();
        public virtual IQuestNpc NpcById(int id)
        {
            if (!NpcsById.TryGetValue(id, out var npc)) return null;
            if (npc.Owner == null) npc.Owner = this;
            return npc;
        }

        // the clock set to tickMs, then the due timers run as CAIActionQueue::checkAction runs them: each whose
        // deadline is strictly before the tick (tick > deadline), soonest first (arming order among equals), one at a
        // time, a timer a callback arms meanwhile included when it is due too. Each runs with the arguments of a fresh
        // context (RunCallback): this fake as the char for a player timer and an NPC timer WithPlayer, no char for an
        // NPC timer without it (as live). Returns how many ran
        public int FireTimers(long tickMs)
        {
            NowMs = tickMs;
            int ran = 0;
            while (true)
            {
                TimerFake due = null;
                foreach (var t in Timers)
                    if (tickMs > t.Deadline && (due == null || t.Deadline < due.Deadline || (t.Deadline == due.Deadline && t.Order < due.Order)))
                        due = t;
                if (due == null) return ran;
                Timers.Remove(due);
                RunCallback(due.Callback, due.Npc, due.Npc == null || due.WithPlayer);
                ran++;
            }
        }

        // each listener of the event run once, in adding order, as CAIContainer::Tick's triggerListener("TICK")
        // (one a callback removes meanwhile still runs this time, as the engine walks a copy of the list). Returns how
        // many ran
        public int FireListeners(string evt = "TICK")
        {
            var now = Listeners.Where(x => x.Event == evt).ToList();
            foreach (var x in now) RunCallback(x.Callback, null, true);
            return now.Count;
        }

        // the callback with a fresh context's arguments: the NPC as Npc (null for the char's own), no Mob, MobDeath,
        // Csid, Option, OptionText, TriggerAreaId or trade (Trade throws); withPlayer false: no char either (Player and
        // Quest throw). The handler's restored after
        public void RunCallback(QuestCallback callback, QuestNpcFake npc, bool withPlayer = true)
        {
            var (keepNpc, keepMob, keepDeath, keepCsid, keepOption, keepText, keepArea) = (npc_, mob_, MobDeath, Csid, Option, OptionText, TriggerAreaId);
            var (keepNoPlayer, keepNoTrade) = (noPlayer_, noTrade_);
            try
            {
                noPlayer_ = !withPlayer;
                noTrade_ = true;
                Npc = npc;
                mob_ = null;
                MobDeath = null;
                Csid = 0;
                Option = 0;
                OptionText = null;
                TriggerAreaId = 0;
                callback.Run(this);
            }
            finally
            {
                (npc_, mob_, MobDeath, Csid, Option, OptionText, TriggerAreaId) = (keepNpc, keepMob, keepDeath, keepCsid, keepOption, keepText, keepArea);
                (noPlayer_, noTrade_) = (keepNoPlayer, keepNoTrade);
            }
        }
        // what RandomInt answers, in order; when empty, the lower bound
        public Queue<int> Rolls { get; } = new Queue<int>();
        public virtual int RandomInt(int lower, int upper) { Record("Player", "RandomInt", lower, upper); return Rolls.Count > 0 ? Rolls.Dequeue() : lower; }
        public virtual void DelQuest(QuestLog log, string constName) => Record("Player", "DelQuest", log, constName);
        // recorded as Player.AddQuest(<log>,<constName>)
        public virtual void AddQuest(QuestLog log, string constName) => Record("Player", "AddQuest", log, constName);
        // recorded as Player.CompleteQuest(<log>,<constName>)
        public virtual void CompleteQuest(QuestLog log, string constName) => Record("Player", "CompleteQuest", log, constName);
        // recorded as Player.UpdateEvent(<arg>,...)
        public virtual void UpdateEvent(params int[] args) => Record("Player", "UpdateEvent", args.Cast<object>().ToArray());
        public virtual void SetHP(int hp) { Record("Player", "SetHP", hp); HP = hp; }
        public virtual void SetMP(int mp) { Record("Player", "SetMP", mp); MP = mp; }
        public virtual void UnlockJob(int job) => Record("Player", "UnlockJob", job);
        public virtual void SetPetName(int petType, int nameId) => Record("Player", "SetPetName", petType, nameId);
        public virtual void UnlockAttachment(int item) => Record("Player", "UnlockAttachment", item);
        public virtual void AddExp(int exp) => Record("Player", "AddExp", exp);
        public virtual bool GiveCurrency(string currency, int amount) { Record("Player", "GiveCurrency", currency, amount); return GiveResult; }
        public virtual bool GiveCurrency(string currency, double amount) { Record("Player", "GiveCurrency", currency, amount); return GiveResult; }
        // recorded as Player.PopFromQm(<name>,<claim>,<hide>,radius=<radius>)
        public virtual bool PopFromQm(string mobName, bool claim, int hide, int radius) { Record("Player", "PopFromQm", mobName, claim, hide, $"radius={radius}"); return PopResult; }
        // recorded as Player.PopFromQm(<name>,<claim>,<hide>), the name as <name>+<offset> with an offset
        public virtual bool PopFromQm(string mobName, int hide, bool claim = true, int offset = 0)
        {
            Record("Player", "PopFromQm", offset == 0 ? mobName : $"{mobName}+{offset}", claim, hide);
            return PopResult;
        }
        // recorded as Player.PopFromQm(<name>;<name>...,<hide>)
        public virtual bool PopFromQm(string[] mobNames, int hide) { Record("Player", "PopFromQm", string.Join(";", mobNames), hide); return PopResult; }
        // recorded as Player.PopFromQmSeries(<name>,<count>,<hide>)
        public virtual bool PopFromQmSeries(string mobName, int count, int hide) { Record("Player", "PopFromQmSeries", mobName, count, hide); return PopResult; }
        // recorded as Player.PopFromQmSeries(<name>,<count>,<hide>,<claim>,<look>)
        public virtual bool PopFromQmSeries(string mobName, int count, int hide, bool claim, bool look)
        {
            Record("Player", "PopFromQmSeries", mobName, count, hide, claim, look);
            return PopResult;
        }
        // the NPCs' local vars, keyed "<npcName>/<name>"; recorded as Player.SetNpcLocalVar(<npc>,<name>,<value>)
        public readonly Dictionary<string, int> NpcLocalVars = new Dictionary<string, int>();
        public virtual void SetNpcLocalVar(string npcName, string name, int value)
        {
            Record("Player", "SetNpcLocalVar", npcName, name, value);
            NpcLocalVars[$"{npcName}/{name}"] = value;
        }

        // ---- the quest ----
        public virtual int Var(string name) => Vars.TryGetValue(name, out var v) ? v : 0;
        public virtual bool IsVarBitsSet(string name, params int[] bits) => bits.All(b => (Var(name) & 1 << b) != 0);
        public virtual void SetStep(int step) { Record("Quest", "SetStep", step); Step = step; }
        public virtual void SetVar(string name, int value, uint expiry = 0) { Record("Quest", "SetVar", name, value, expiry); Vars[name] = value; }
        public virtual void SetVarBit(string name, int bit) { Record("Quest", "SetVarBit", name, bit); Vars[name] = Var(name) | 1 << bit; }
        public virtual void UnsetVarBit(string name, int bit) { Record("Quest", "UnsetVarBit", name, bit); Vars[name] = Var(name) & ~(1 << bit); }
        public virtual void IncrementVar(string name, int value) { Record("Quest", "IncrementVar", name, value); Vars[name] = Var(name) + value; }
        public virtual void SetMustZone() { Record("Quest", "SetMustZone"); MustZone = true; }
        // quest:getLocalVar / setLocalVar (Quest.LocalVar / Quest.SetLocalVar)
        public virtual int QuestLocalVar(string name) => QuestLocalVars.TryGetValue(name, out var v) ? v : 0;
        public virtual void SetQuestLocalVar(string name, int value) { Record("Quest", "SetLocalVar", name, value); QuestLocalVars[name] = value; }
        int IQuestProgressView.LocalVar(string name) => QuestLocalVar(name);
        void IQuestProgress.SetLocalVar(string name, int value) => SetQuestLocalVar(name, value);
        public virtual void Begin()
        {
            if (Hidden) throw new System.InvalidOperationException("a hidden quest cannot begin (HiddenQuest:new has no quest log or id)");
            Record("Quest", "Begin");
            Status = global::XI.Quests.QuestStatus.Accepted;
        }
        public virtual bool Complete()
        {
            Record("Quest", "Complete");
            if (CompleteResult && !Hidden) Status = global::XI.Quests.QuestStatus.Completed;
            return CompleteResult;
        }
        // player:delCurrentQuest: recorded as Quest.DelCurrent(); an accepted quest is then available (the fake holds no
        // completed bit apart from Status)
        public virtual void DelCurrent()
        {
            if (Hidden) throw new System.InvalidOperationException("a hidden quest cannot be dropped (HiddenQuest:new has no quest log or id)");
            Record("Quest", "DelCurrent");
            if (Status == global::XI.Quests.QuestStatus.Accepted) Status = global::XI.Quests.QuestStatus.Available;
        }
        // quest:cleanup: recorded as Quest.Cleanup(); the quest's vars cleared
        public virtual void Cleanup()
        {
            Record("Quest", "Cleanup");
            Vars.Clear();
        }

        // ---- the trade and the text ----
        public virtual bool Matches(params (int Item, int Qty)[] items) => TradeResult;
        public Dictionary<int, int> TradeItems { get; } = new Dictionary<int, int>();
        // trade:hasItemQty(item, qty): exactly qty of the item offered (quest rework P9: `==`, as the live binding and C++
        // lua_trade_container.cpp; it read "at least" before)
        public virtual bool HasItemQty(int item, int qty) => TradeItems.TryGetValue(item, out var n) && n == qty;
        public virtual int ItemCount => TradeItems.Values.Sum();
        // trade:getSlotCount() / getItemId(slot): the traded items by slot, in order (TradeItems is by item; a test sets
        // both); a slot past the list holds 0
        public List<int> TradeSlots { get; } = new List<int>();
        public virtual int SlotCount => TradeSlots.Count;
        public virtual int ItemId(int slot) => slot >= 0 && slot < TradeSlots.Count ? TradeSlots[slot] : 0;
        // trade:getItem(slot):getAHCat(): the category a test gives the slot's item (AhCategories, by item id), else 0;
        // a slot past TradeSlots raises, as the Lua's call on a nil item does
        public Dictionary<int, int> AhCategories { get; } = new Dictionary<int, int>();
        public virtual int ItemAhCategory(int slot) =>
            slot < 0 || slot >= TradeSlots.Count
                ? throw new System.InvalidOperationException($"trade:getItem({slot}) is nil")
                : AhCategories.TryGetValue(TradeSlots[slot], out var c) ? c : 0;
        public virtual int ItemQty(int item) => TradeItems.TryGetValue(item, out var n) ? n : 0;
        // npcUtil.tradeHas(trade, { item, ... }): each id counts 1, a repeated id adds up (as tradeHas sums it)
        public virtual bool Has(params int[] items) => HasQty(items.Select(i => (i, 1)).ToArray());
        // the gil as item 65535, as npcUtil.tradeHas counts it
        public virtual bool HasWithGil(int gil, params int[] items) => Has(items) && TradeItems.TryGetValue(65535, out var g) && g >= gil;
        // the same, exact: the items and exactly that gil, nothing else
        public virtual bool HasExactlyWithGil(int gil, params int[] items) =>
            HasExactly(items.Select(i => (i, 1)).Append((65535, gil)).ToArray());
        // npcUtil.tradeHas / tradeHasExactly over TradeItems: a repeated id adds up, as tradeHas sums it; exact: nothing else
        private static Dictionary<int, int> Needed((int Item, int Qty)[] items) =>
            items.GroupBy(i => i.Item).ToDictionary(g => g.Key, g => g.Sum(i => i.Qty));
        public virtual bool HasQty(params (int Item, int Qty)[] items) =>
            Needed(items).All(n => TradeItems.TryGetValue(n.Key, out var have) && have >= n.Value);
        public virtual bool HasExactly(params (int Item, int Qty)[] items)
        {
            var needed = Needed(items);
            return HasQty(items) && TradeItems.All(t => t.Value == 0 || needed.TryGetValue(t.Key, out var n) && n == t.Value);
        }
        // trade:getGil(): the gil as item 65535, as above (explicit: a Player-side gil member is another one)
        int IQuestTrade.Gil => TradeItems.TryGetValue(65535, out var g) ? g : 0;
        // npcUtil.tradeHasExactly: each listed item as many times as it is listed, and no other item in the trade
        public virtual bool HasExactly(params int[] items)
        {
            var need = items.GroupBy(i => i).ToDictionary(g => g.Key, g => g.Count());
            return need.All(kv => TradeItems.TryGetValue(kv.Key, out var n) && n == kv.Value) &&
                   TradeItems.All(kv => kv.Value == 0 || need.ContainsKey(kv.Key));
        }
        public virtual void Confirm() => Record("Trade", "Confirm");
        // recorded as Trade.HelmOnTrade(<helmType>)
        public virtual void HelmOnTrade(int helmType) => Record("Trade", "HelmOnTrade", helmType);
        // recorded as Trade.ConfirmItem(<item>,<qty>)
        public virtual void ConfirmItem(int item, int qty) => Record("Trade", "ConfirmItem", item, qty);
        // recorded as Trade.TreasureTrade(<bypassType>,<bypassReward>); answers TreasureResult
        public int TreasureResult { get; set; }
        public virtual int TreasureTrade(int bypassType, int bypassReward)
        {
            Record("Trade", "TreasureTrade", bypassType, bypassReward);
            return TreasureResult;
        }
        void IQuestTrade.Complete() => Record("Trade", "Complete");
        // recorded as Trade.CompleteChecked(); answers TradeCompleteResult (true by default: the items taken)
        public bool TradeCompleteResult { get; set; } = true;
        public virtual bool CompleteChecked()
        {
            Record("Trade", "CompleteChecked");
            return TradeCompleteResult;
        }
        // trade:getItem(slot):getWeaponskillPoints(): the points by trade slot (0 when not set)
        public Dictionary<int, int> TradeWeaponskillPoints { get; } = new Dictionary<int, int>();
        public virtual int ItemWeaponskillPoints(int slot) => TradeWeaponskillPoints.TryGetValue(slot, out var p) ? p : 0;
        public virtual void Special(ZoneText text, params int[] args) =>
            Record("Text", "Special", new object[] { text }.Concat(args.Cast<object>()).ToArray());  // Text.Special(<zone>:<NAME>,args)
        // recorded as Text.MessageName(<zone>:<NAME>,args)
        public virtual void MessageName(ZoneText text, params int[] args) =>
            Record("Text", "MessageName", new object[] { text }.Concat(args.Cast<object>()).ToArray());
        // recorded as Text.MessageText(<zone>:<NAME>,<showName>,<mode>)
        public virtual void MessageText(ZoneText text, bool showName, int mode) => Record("Text", "MessageText", text, showName, mode);
        // recorded as Text.MessageTextFromPlayer(<zone>:<NAME>)
        public virtual void MessageTextFromPlayer(ZoneText text) => Record("Text", "MessageTextFromPlayer", text);
        // recorded as Text.Special(#<id>,args)
        public virtual void Special(int textId, params int[] args) =>
            Record("Text", "Special", new object[] { "#" + textId }.Concat(args.Cast<object>()).ToArray());
        // the text ids Id answers, keyed "<zone>:<NAME>" (ZoneText.ToString()); an unknown text throws, as the live one does
        public Dictionary<string, int> TextIds { get; } = new Dictionary<string, int>();
        public virtual int Id(ZoneText text) =>
            TextIds.TryGetValue(text.ToString(), out var id) ? id : throw new System.InvalidOperationException($"unknown text {text}");
    }

    // the fake's NPC: its id and local vars; SetLocalVar is recorded in the calls list it is given as Npc.SetLocalVar(name,value)
    // an OnMobDeath's optParams as plain properties (quest rework P7): a killer by default, no weapon skill
    public sealed class QuestMobDeathFake : IQuestMobDeath
    {
        public bool IsKiller { get; set; } = true;
        public bool NoKiller { get; set; }
        public bool IsWeaponSkillKill { get; set; }
        public int WeaponskillUsed { get; set; }
        public int WeaponskillDamage { get; set; }
    }

    public sealed class QuestNpcFake : IQuestNpc
    {
        private readonly List<string> calls_;

        public QuestNpcFake(int id, List<string> calls = null)
        {
            Id = id;
            calls_ = calls;
        }

        public int Id { get; set; }
        // npc:getName()
        public string Name { get; set; }
        public readonly Dictionary<string, int> LocalVars = new Dictionary<string, int>();
        public int LocalVar(string name) => LocalVars.TryGetValue(name, out var v) ? v : 0;
        // player:checkDistance(npc)
        public double PlayerDistance { get; set; }
        public void SetLocalVar(string name, int value)
        {
            calls_?.Add($"Npc.SetLocalVar({name},{value})");
            LocalVars[name] = value;
        }
        // npc:getAnimation() / npc:setAnimation(animation), recorded as Npc.SetAnimation(<animation>)
        public int Animation { get; set; }
        public void SetAnimation(int animation)
        {
            calls_?.Add($"Npc.SetAnimation({animation})");
            Animation = animation;
        }
        // recorded as Npc.ShowText(<zone>:<NAME>,args)
        public void ShowText(ZoneText text, params int[] args) =>
            calls_?.Add($"Npc.ShowText({string.Join(",", new object[] { text }.Concat(args.Cast<object>()))})");
        // npc:hideNPC(seconds), recorded as Npc.Hide(<seconds>)
        public void Hide(int seconds) => calls_?.Add($"Npc.Hide({seconds})");
        // npc:getXPos() / getYPos() / getZPos()
        public double XPos { get; set; }
        public double YPos { get; set; }
        public double ZPos { get; set; }
        // npcUtil.queueMove(npc, { x, y, z }), recorded as Npc.QueueMove(<x>,<y>,<z>) (invariant)
        public void QueueMove(double x, double y, double z)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            calls_?.Add($"Npc.QueueMove({x.ToString(inv)},{y.ToString(inv)},{z.ToString(inv)})");
        }
        // npc:setPos(x, y, z), recorded as Npc.SetPos(<x>,<y>,<z>) (invariant); the fake's position follows it
        public void SetPos(double x, double y, double z)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            calls_?.Add($"Npc.SetPos({x.ToString(inv)},{y.ToString(inv)},{z.ToString(inv)})");
            XPos = x;
            YPos = y;
            ZPos = z;
        }

        // the context fake whose Npc or Mob this is (set there): its Timers get this NPC's timers
        public QuestContextFake Owner { get; internal set; }
        // npc:timer(ms, fn) (quest rework P10), recorded as Npc.Timer(<ms>,<callback>[,WithPlayer]) in the owner's
        // Calls and kept in its Timers; an NPC no context fake holds throws
        public void Timer(int ms, QuestCallback callback) => Timer(ms, callback, WithPlayer.No);
        public void Timer(int ms, QuestCallback callback, WithPlayer withPlayer)
        {
            if (Owner == null) throw new System.InvalidOperationException("this fake NPC is no context fake's Npc or Mob: its timer has no Timers to go to");
            Owner.ArmTimer(this, ms, callback, withPlayer == WithPlayer.Yes);
        }

        // (B3b Deferred 1) npc:getStatus(); npc:setStatus(status), recorded as Npc.SetStatus(<status>), the fake's
        // Status following it
        public int Status { get; set; }
        public void SetStatus(int status)
        {
            calls_?.Add($"Npc.SetStatus({status})");
            Status = status;
        }
        // recorded as Npc.EntityAnimationPacket(<animation>)
        public void EntityAnimationPacket(string animation) => calls_?.Add($"Npc.EntityAnimationPacket({animation})");
        // recorded as Npc.ClearPath(<pause>) / Npc.ContinuePath()
        public void ClearPath(bool pause) => calls_?.Add($"Npc.ClearPath({pause})");
        public void ContinuePath() => calls_?.Add("Npc.ContinuePath()");
        // recorded as Npc.FacePlayer(<nonGlobal>)
        public void FacePlayer(bool nonGlobal) => calls_?.Add($"Npc.FacePlayer({nonGlobal})");
        // recorded as Npc.AttohwaChasmHandleLuminantUsed()
        public void AttohwaChasmHandleLuminantUsed() => calls_?.Add("Npc.AttohwaChasmHandleLuminantUsed()");
    }

    // q.Mission of a fake (quest rework C2): the mission ThisMission names, over its owner's CurrentMissions,
    // MissionStatuses, MissionVars ("<log>/<id>/<name>", the same char var Mission[<log>][<id>]<name> xi.mission.getVar
    // reads), MissionMustZone and MissionLocalVars. Every effect is recorded in the owner's Calls as
    // Mission.<Member>(<args>) and applied to those fields; Complete models Mission:complete's state change (the reward
    // is not modelled)
    public sealed class QuestMissionFake : IMissionProgress
    {
        readonly QuestContextFake owner_;

        internal QuestMissionFake(QuestContextFake owner, MissionLog log, int id)
        {
            owner_ = owner;
            Log = log;
            Id = id;
        }

        public MissionLog Log { get; }
        public int Id { get; }
        int LogId => (int)Log;
        string VarKey(string name) => $"{LogId}/{Id}/{name}";

        public bool IsCurrent => owner_.CurrentMission(LogId) == Id;
        public int Status => owner_.MissionStatus(LogId);
        public int StatusAt(int pos) => owner_.MissionStatus(LogId, pos);
        public int Var(string name) => owner_.MissionVar(LogId, Id, name);
        public bool MustZone => owner_.MissionMustZone;
        public int LocalVar(string name) => owner_.MissionLocalVars.TryGetValue(name, out var v) ? v : 0;

        // recorded as Mission.Begin(); the log's current mission then this one
        public void Begin()
        {
            owner_.RecordFrom("Mission", "Begin");
            owner_.CurrentMissions[LogId] = Id;
        }

        // recorded as Mission.Complete(); answers the owner's CompleteResult, and when true, as Mission:complete leaves
        // the char: the mission completed (when current), the log's status 0 except on CoP (log 6, which keeps it), the
        // mission's vars cleared
        public bool Complete()
        {
            owner_.RecordFrom("Mission", "Complete");
            if (!owner_.CompleteResult) return false;
            Completed();
            if (Log != MissionLog.CoP) owner_.MissionStatuses[LogId] = 0;
            ClearVars();
            return true;
        }

        // recorded as Mission.CompleteRaw(); the mission completed (when current), the status and vars kept
        public bool CompleteRaw()
        {
            owner_.RecordFrom("Mission", "CompleteRaw");
            Completed();
            return true;
        }

        // player:completeMission as the engine does it (MissionBitBlob.CompleteMission): only while the mission is the
        // log's current one, which then becomes none (0xFFFF for the nation logs 0 to 2, 0 for the others)
        void Completed()
        {
            if (owner_.CurrentMission(LogId) != Id) return;
            owner_.CompletedMissions.Add($"{LogId}/{Id}");
            owner_.CurrentMissions[LogId] = LogId > 2 ? 0 : 0xFFFF;
        }

        void ClearVars()
        {
            foreach (var k in owner_.MissionVars.Keys.Where(k => k.StartsWith($"{LogId}/{Id}/")).ToList())
                owner_.MissionVars.Remove(k);
        }

        // recorded as Mission.SetStatus(<value>)
        public void SetStatus(int value)
        {
            owner_.RecordFrom("Mission", "SetStatus", value);
            owner_.MissionStatuses[LogId] = value;
        }

        // recorded as Mission.SetStatusAt(<value>,<pos>); the 4-bit field at pos set, as the engine's indexed form
        public void SetStatusAt(int value, int pos)
        {
            owner_.RecordFrom("Mission", "SetStatusAt", value, pos);
            if (pos is < 0 or > 7) return;
            uint status = (uint)Status, mask = 0xFu << 4 * pos;
            owner_.MissionStatuses[LogId] = (int)(status & ~mask | ((uint)value & 0xF) << 4 * pos);
        }

        // recorded as Mission.SetVar(<name>,<value>)
        public void SetVar(string name, int value)
        {
            owner_.RecordFrom("Mission", "SetVar", name, value);
            owner_.MissionVars[VarKey(name)] = value;
        }

        // recorded as Mission.SetMustZone()
        public void SetMustZone()
        {
            owner_.RecordFrom("Mission", "SetMustZone");
            owner_.MissionMustZone = true;
        }

        // recorded as Mission.SetLocalVar(<name>,<value>)
        public void SetLocalVar(string name, int value)
        {
            owner_.RecordFrom("Mission", "SetLocalVar", name, value);
            owner_.MissionLocalVars[name] = value;
        }

        // recorded as Mission.Cleanup(); the mission's vars cleared
        public void Cleanup()
        {
            owner_.RecordFrom("Mission", "Cleanup");
            ClearVars();
        }
    }
}
