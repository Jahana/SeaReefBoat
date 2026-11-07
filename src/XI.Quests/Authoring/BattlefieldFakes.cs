// XI.Quests -- quest rework C2: the battlefield facets over plain fields, for tests of battlefield content and of the
// framework port (BattlefieldFramework). As QuestContextFake, reads come from public fields and every call that does
// something is recorded in the player's Calls as "<Facet>.<Member>(<args>)".
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    // a player: the quest fake's members, and the battlefield bindings
    public class BattlefieldPlayerFake : QuestContextFake, IBattlefieldPlayer
    {
        public int ZoneId { get; set; }
        public int EntityStatus { get; set; }
        public bool IsAlive { get; set; } = true;
        public int GMLevel { get; set; }
        public int VisibleGMLevel { get; set; }
        // player:getAlliance(): the player itself unless a test sets Members
        public List<IBattlefieldPlayer> Members { get; set; }
        public IReadOnlyList<IBattlefieldPlayer> AllianceList() =>
            Members ?? new List<IBattlefieldPlayer> { this };
        public IBattlefieldView CurrentBattlefield { get; set; }
        // the battlefield EnterBattlefield puts the player in (CurrentBattlefield after it); null leaves it as it is
        public IBattlefieldView EnterTarget { get; set; }
        public IBattlefieldView RegisteredBattlefield { get; set; }
        // the power of each held effect (StatusEffects holds which are held)
        public readonly Dictionary<int, int> EffectPowers = new Dictionary<int, int>();
        public IBattlefieldEffect StatusEffect(int effect) =>
            HasStatusEffect(effect) ? new EffectFake(EffectPowers.TryGetValue(effect, out var p) ? p : 0) : null;
        public void CopyStatusEffect(IBattlefieldEffect effect) => Record("Player", "CopyStatusEffect", effect?.Power);
        // what registerBattlefield returns (a returnCode)
        public int RegisterResult { get; set; } = BattlefieldConst.Cutscene;
        public int RegisterBattlefield(int battlefieldId, int area, int initiatorId, IBattlefieldContent content)
        {
            Record("Player", "RegisterBattlefield", battlefieldId, area, initiatorId);
            return RegisterResult;
        }
        public readonly HashSet<int> FullBattlefields = new HashSet<int>();
        public bool BattlefieldAtCapacity(int battlefieldId) => FullBattlefields.Contains(battlefieldId);
        public void EnterBattlefield()
        {
            Record("Player", "EnterBattlefield");
            if (EnterTarget != null) CurrentBattlefield = EnterTarget;
        }
        public void LeaveBattlefield(int leaveCode) => Record("Player", "LeaveBattlefield", leaveCode);
        public void SetEnteredBattlefield(bool value) => Record("Player", "SetEnteredBattlefield", value);
        public void MessageBasic(int messageId, params int[] args) =>
            Record("Player", "MessageBasic", new object[] { messageId }.Concat(args.Cast<object>()).ToArray());
        public void MessageSpecial(int textId, params int[] args) =>
            Record("Player", "MessageSpecial", new object[] { textId }.Concat(args.Cast<object>()).ToArray());
        public void MessageText(IBattlefieldNpc npc, int textId, bool showName) =>
            Record("Player", "MessageText", npc?.Id, textId, showName);
        public void UpdateEventValues(params object[] args) => Record("Player", "UpdateEvent", args);
        public void UpdateEventString(string s) => Record("Player", "UpdateEventString", s);
        public readonly Dictionary<int, int> WornUsesByItem = new Dictionary<int, int>();
        public int WornUses(int item) => WornUsesByItem.TryGetValue(item, out var n) ? n : 0;
        public int IncrementItemWear(int item)
        {
            Record("Player", "IncrementItemWear", item);
            return WornUsesByItem[item] = WornUses(item) + 1;
        }
        public void TradeComplete() => Record("Player", "TradeComplete");
        public int PlayerVar(string name) => CharVars.TryGetValue(name, out var v) ? v : 0;
        public void SetPlayerVar(string name, int value)
        {
            Record("Player", "SetVar", name, value);
            CharVars[name] = value;
        }
        public void StartEventTable(int csid, int[] table) =>
            Record("Player", "StartEvent", csid, "{" + string.Join(",", table) + "}");
        public void DelStatusEffectsByFlag(int flag) => Record("Player", "DelStatusEffectsByFlag", flag);
        public void PrintToPlayer(string message, int channel) => Record("Player", "PrintToPlayer", message, channel);
        public void AddTreasure(int item, IBattlefieldNpc npc) => Record("Player", "AddTreasure", item, npc?.Id);

        private sealed class EffectFake : IBattlefieldEffect
        {
            public EffectFake(int power) => Power = power;
            public int Power { get; }
        }
    }

    public sealed class BattlefieldNpcFake : IBattlefieldNpc
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int TargId { get; set; }
    }

    // an action of the fake engine: Battlefield:event(csid, args...), or the exit cutscene (Exit)
    public sealed class BattlefieldActionFake : IBattlefieldAction
    {
        public int Csid;
        public int[] Args = Array.Empty<int>();
        public bool Exit;

        public override string ToString() => Exit ? $"exit({Csid})" : $"event({Csid},{string.Join(",", Args)})";
    }

    // a battlefield (CLuaBattlefield) over fields; every change is recorded in Calls as "Battlefield.<Member>(...)"
    public sealed class BattlefieldViewFake : IBattlefieldView
    {
        public readonly List<string> Calls = new List<string>();
        private void Record(string member, params object[] args) =>
            Calls.Add($"Battlefield.{member}({string.Join(",", args.Select(a => a?.ToString()))})");

        public int Id { get; set; }
        public int ZoneId { get; set; }
        public int Area { get; set; } = 1;
        public int TimeLimit { get; set; }
        public int TimeInside { get; set; }
        public int RemainingTime { get; set; }
        public int WipeTime { get; set; }
        public void SetWipeTime(int seconds)
        {
            Record("SetWipeTime", seconds);
            WipeTime = seconds;
        }
        public int MaxParticipants { get; set; }
        public int PlayerCount => PlayerList.Count;
        public List<IBattlefieldPlayer> PlayerList { get; } = new List<IBattlefieldPlayer>();
        public IReadOnlyList<IBattlefieldPlayer> Players() => PlayerList.ToList();
        public List<IBattlefieldMob> MobList { get; } = new List<IBattlefieldMob>();
        public IReadOnlyList<IBattlefieldMob> Mobs(bool required, bool additional) => MobList.ToList();
        public (string Name, int ClearTime, int PartySize) RecordValue { get; set; } = ("Meme", 1, 1);
        public (string Name, int ClearTime, int PartySize) Record() => RecordValue;
        public void SetRecord(string name, int clearTime, int partySize) => RecordValue = (name, clearTime, partySize);
        public int Status { get; set; }
        public void SetStatus(int status)
        {
            Record("SetStatus", status);
            Status = status;
        }
        public readonly Dictionary<string, double> LocalVars = new Dictionary<string, double>();
        public double LocalVar(string name) => LocalVars.TryGetValue(name, out var v) ? v : 0;
        public void SetLocalVar(string name, double value)
        {
            Record("SetLocalVar", name, value);
            LocalVars[name] = value;
        }
        public int LastTimeUpdate { get; set; }
        public void SetLastTimeUpdate(int seconds) => LastTimeUpdate = seconds;
        public void SetTimeLimit(int seconds) => TimeLimit = seconds;
        public (int Id, string Name) InitiatorValue { get; set; }
        public (int Id, string Name) Initiator() => InitiatorValue;
        public int ArmouryCrate { get; set; }
        public void InsertEntity(int targId, bool enemy, bool inBattlefield) =>
            Record("InsertEntity", targId, enemy, inBattlefield);
        public void Cleanup(bool cleanup) => Record("Cleanup", cleanup);
        public void Win() => Record("Win");
        public void Lose() => Record("Lose");
        public void AddGroups(IBattlefieldContent content, bool hasMultipleAreas) =>
            Record("AddGroups", content?.BattlefieldId, hasMultipleAreas);
    }

    // a content table: a C# battlefield's (over its data, its methods its hooks on a context like the caller's) or a
    // Lua one's (Lua: its fields set by the test, its methods recorded in Calls)
    public sealed class BattlefieldContentFake : IBattlefieldContent
    {
        public BattlefieldContentFake(BattlefieldScript script) => Script = script;

        public BattlefieldContentFake()
        {
        }

        public BattlefieldScript Script { get; }
        public readonly List<string> Calls = new List<string>();
        public readonly List<string> ReachedLabels = new List<string>();

        private int luaId_, luaIndex_;
        public int BattlefieldId
        {
            get => Script?.Data.BattlefieldId ?? luaId_;
            set => luaId_ = value;
        }
        public int Index
        {
            get => Script?.Data.Index ?? luaIndex_;
            set => luaIndex_ = value;
        }
        public bool LuaExperimental { get; set; }
        public bool Experimental => Script?.Data.Experimental ?? LuaExperimental;
        public string[] LuaEntryNpcs { get; set; }
        public IReadOnlyList<string> EntryNpcs => Script?.Data.EntryNpcs ?? LuaEntryNpcs;
        public int RequiredItemCount => Script?.Data.TradeItems.Length ?? 0;
        public int FirstRequiredItem => Script?.Data.TradeItems.FirstOrDefault() ?? 0;
        // the resolved ids of the text names (the fake engine's)
        public int? WornMessage { get; set; }
        public int[] WornMessageTable { get; set; }
        public bool TradeHas(IQuestTrade trade) => Script != null && trade.Has(Script.Data.TradeItems);
        public bool HasArmouryCrates => ArmouryCrateIds != null;
        public int[] ArmouryCrateIds { get; set; }
        public int? ArmouryCrate(int area) =>
            ArmouryCrateIds != null && area >= 1 && area <= ArmouryCrateIds.Length ? ArmouryCrateIds[area - 1] : null;
        // a Lua content's checkRequirements answer
        public bool LuaRequirements { get; set; }
        // content.loot: a C# content's Data.Loot (one table for the content, as the Lua's); a Lua one's LuaLoot
        public List<BattlefieldLootGroup> LuaLoot { get; set; } = new List<BattlefieldLootGroup>();
        private BattlefieldLootTable loot_;
        public BattlefieldLootTable Loot => loot_ ??= new BattlefieldLootTable(Script?.Data.Loot ?? LuaLoot);

        public bool CheckRequirements(IBattlefieldContext q, bool isRegistrant, bool passTrade)
        {
            Calls.Add($"checkRequirements({isRegistrant},{passTrade})");
            if (Script == null) return LuaRequirements;
            var c = new BattlefieldContextFake(Script)
            {
                Player = q.Player, Npc = q.Npc, Trade = passTrade ? q.Trade : null, Engine = q.Engine, Self = this,
            };
            return Script.CheckRequirements(c, isRegistrant);
        }

        public void OnEntryEventUpdate(IBattlefieldContext q, int csid, int option) =>
            Calls.Add($"onEntryEventUpdate({csid},{option})");

        public void Call(string eventName, IBattlefieldContext q, int csid, int option) =>
            Calls.Add($"{eventName}({csid},{option})");

        public void Reached(string label) => ReachedLabels.Add(label);
    }

    // the Lua globals over fields
    public sealed class BattlefieldEngineFake : IBattlefieldEngine
    {
        public readonly List<string> Calls = new List<string>();
        // zones[zone].text: "<zone>:<name>" -> id
        public readonly Dictionary<string, int> Texts = new Dictionary<string, int>();
        public readonly Dictionary<int, IBattlefieldContent> Contents = new Dictionary<int, IBattlefieldContent>();
        public readonly Dictionary<int, List<IBattlefieldContent>> ByZone =
            new Dictionary<int, List<IBattlefieldContent>>();
        public readonly Dictionary<int, int> Uses = new Dictionary<int, int>();
        public readonly Dictionary<int, IBattlefieldNpc> Npcs = new Dictionary<int, IBattlefieldNpc>();
        public readonly Dictionary<int, IBattlefieldMob> MobsById = new Dictionary<int, IBattlefieldMob>();
        // what utils.selectFromLootGroups returns when set; when empty, each group's first entry of the table it is
        // given (a stand-in for the roll)
        public List<(int ItemId, double? Amount)> Loot { get; } = new List<(int ItemId, double? Amount)>();
        // each lootTable utils.selectFromLootGroups was given
        public readonly List<BattlefieldLootTable> LootTables = new List<BattlefieldLootTable>();
        public bool TimeRemaining { get; set; } = true;
        public bool LevelSynced { get; set; }

        public int? Text(int zone, string name) => Texts.TryGetValue($"{zone}:{name}", out var id) ? id : null;
        public IBattlefieldContent Content(int battlefieldId) =>
            Contents.TryGetValue(battlefieldId, out var c) ? c : null;
        public IReadOnlyList<IBattlefieldContent> ZoneContents(int zone) =>
            ByZone.TryGetValue(zone, out var l) ? l : null;
        public int MaxAreas(int zone) => 3;
        public int? ItemUses(int item) => Uses.TryGetValue(item, out var n) ? n : null;
        public bool SendTimePrompts(IBattlefieldView battlefield, IReadOnlyList<IBattlefieldPlayer> players) =>
            TimeRemaining;

        // xi.battlefield.getBattlefieldOptions, as the Lua (battlefield.lua:1348) over the fake's contents
        public int BattlefieldOptions(IBattlefieldContext q, bool passTrade)
        {
            int result = 0;
            if (ZoneContents(q.Player.ZoneId) is not { } contents) return result;
            foreach (var content in contents)
                if (content.CheckRequirements(q, true, passTrade) &&
                    !q.Player.BattlefieldAtCapacity(content.BattlefieldId) &&
                    !content.Experimental)
                    result |= 1 << content.Index;
            return result;
        }

        public bool RejectLevelSyncedParty(IBattlefieldContext q) => LevelSynced;
        public IBattlefieldNpc NpcById(int id) => Npcs.TryGetValue(id, out var n) ? n : null;
        public IBattlefieldMob MobById(int id) => MobsById.TryGetValue(id, out var m) ? m : null;
        public void ShowCrate(IBattlefieldNpc crate) => Calls.Add($"ShowCrate({crate.Id})");
        public void AddCrateListener(IBattlefieldNpc crate, IBattlefieldContent self) =>
            Calls.Add($"AddCrateListener({crate.Id})");
        public void OpenCrate(IBattlefieldContext q, IBattlefieldNpc crate, Func<IBattlefieldContext, bool> callback)
        {
            Calls.Add($"OpenCrate({crate.Id})");
            // the callback's own call: q's player, the crate its npc (the Lua closure's upvalues)
            var fake = q as BattlefieldContextFake;
            callback(new BattlefieldContextFake(q.Script)
            {
                Player = q.Player, Npc = crate, Engine = q.Engine, Self = q.Self, Battlefield = q.Battlefield,
                ReachedLabels = fake?.ReachedLabels ?? new List<string>(),
            });
        }
        public IReadOnlyList<(int ItemId, double? Amount)> SelectFromLootGroups(IBattlefieldPlayer player,
                                                                                BattlefieldLootTable lootTable)
        {
            Calls.Add("SelectFromLootGroups");
            LootTables.Add(lootTable);
            if (player == null) return new List<(int, double?)>();
            if (Loot.Count > 0) return Loot.ToList();
            return lootTable.Groups.Where(g => g.Entries.Count > 0)
                .Select(g => (g.Entries[0].ItemId, (double?)g.Entries[0].Amount)).ToList();
        }
        public void GiveCurrency(IBattlefieldPlayer player, string currency, double amount, bool silent) =>
            Calls.Add($"GiveCurrency({player.Id},{currency},{amount},{silent})");
        public bool BcnmEnableExperimental { get; set; }
        public IBattlefieldAction Event(int csid, params int[] args) =>
            new BattlefieldActionFake { Csid = csid, Args = args };
        public IBattlefieldAction ExitCutscene() => new BattlefieldActionFake { Csid = 32003, Exit = true };
    }

    // a call of a battlefield's hook over fakes
    public sealed class BattlefieldContextFake : IBattlefieldContext
    {
        public BattlefieldContextFake(BattlefieldScript script)
        {
            Script = script;
            Self = script == null ? null : new BattlefieldContentFake(script);
        }

        public BattlefieldScript Script { get; }
        public BattlefieldFramework Framework => Script == null ? null : new BattlefieldFramework(this);
        public IBattlefieldContent Self { get; set; }
        public IBattlefieldPlayer Player { get; set; }
        public IBattlefieldNpc Npc { get; set; }
        public IQuestTrade Trade { get; set; }
        public IBattlefieldView Battlefield { get; set; }
        public IBattlefieldEngine Engine { get; set; } = new BattlefieldEngineFake();
        public List<string> ReachedLabels { get; init; } = new List<string>();
        public void Reached(string label) => ReachedLabels.Add(label);
    }
}
