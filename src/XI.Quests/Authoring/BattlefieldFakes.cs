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
        public void ShowText(IBattlefieldMob mob, int textId) => Record("Player", "ShowText", mob?.Id, textId);
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
        // player:getParty(): the player itself unless a test sets PartyList
        public List<IBattlefieldPlayer> PartyList { get; set; }
        public IReadOnlyList<IBattlefieldPlayer> Party() => PartyList ?? new List<IBattlefieldPlayer> { this };
        public IBattlefieldMob PetMob { get; set; }
        public IBattlefieldMob Pet() => PetMob;
        public int TargId { get; set; }
        public int MaxHP { get; set; }
        public int MaxMP { get; set; }
        public int TP { get; set; }
        public void SetTP(int value)
        {
            Record("Player", "SetTP", value);
            TP = value;
        }
        public bool IsDead { get; set; }
        public void AllowSendRaisePrompt() => Record("Player", "AllowSendRaisePrompt");
        public void SendEntityUpdateToPlayer(IBattlefieldMob entity, int entityUpdate, int updateMask) =>
            Record("Player", "SendEntityUpdateToPlayer", entity?.Id, entityUpdate, updateMask);

        // ---- deferred callbacks (quest rework C3 PR2) ----
        // the queues this player's battlefield listeners go to; none: an arming throws
        public BattlefieldCallbacksFake Callbacks { get; set; }
        // the entity facet a callback fired on this player gets as its Entity (live: the same Lua object as a mob)
        public IBattlefieldMob AsEntity { get; set; }
        public void AddListener(string evt, string identifier, BattlefieldCallback callback, params object[] captures)
        {
            var queues = Callbacks ?? throw new InvalidOperationException($"fake player {Id} has no Callbacks");
            queues.Listen(this, evt, identifier, callback, captures);
            Record("Player", "AddListener", evt, identifier, callback.Name);
        }
        // the quest fake's (its quest listeners) and the battlefield listeners of the identifier
        public override void RemoveListener(string identifier)
        {
            base.RemoveListener(identifier);
            Callbacks?.Unlisten(this, identifier);
        }

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
        public readonly List<int> Animations = new List<int>();
        public void SetAnimation(int value) => Animations.Add(value);
    }

    // a mob over fields; every call recorded in Calls as "Mob.<Member>(...)"
    public sealed class BattlefieldMobFake : IBattlefieldMob
    {
        public readonly List<string> Calls = new List<string>();
        public int Id { get; set; }
        public int MainLevel { get; set; }
        public void SetMobLevel(int level)
        {
            Calls.Add($"Mob.SetMobLevel({level})");
            MainLevel = level; // live: MLvl = SLvl = level, then CalculateMobStats
        }
        public void PathThrough(double[] points, int flags) =>
            Calls.Add($"Mob.PathThrough({string.Join(",", points)};{flags})");
        public void MessageText(int textId) => Calls.Add($"Mob.MessageText({textId})");

        // the Batch F members: state in fields a test sets, every call that does something recorded in Calls
        private void Rec(string member, params object[] args) =>
            Calls.Add($"Mob.{member}({string.Join(",", args.Select(a => a?.ToString()))})");
        // the id of any entity facet, for the record
        public static int? EntityId(IBattlefieldEntity e) =>
            e is IBattlefieldMob m ? m.Id : e is IBattlefieldPlayer p ? p.Id : (int?)null;

        public readonly Dictionary<string, uint> LocalVars = new Dictionary<string, uint>();
        public uint LocalVar(string name) => LocalVars.TryGetValue(name, out var v) ? v : 0;
        public void SetLocalVar(string name, uint value)
        {
            Rec("SetLocalVar", name, value);
            LocalVars[name] = value;
        }
        public void ResetLocalVars()
        {
            Rec("ResetLocalVars");
            LocalVars.Clear();
        }
        public bool IsSpawned { get; set; }
        // live counts a mob dead at hp <= 0 or when it is not in the world (never spawned, despawned); derived from
        // IsSpawned and HP unless a test sets an override
        public bool? AliveOverride, DeadOverride;
        public bool IsAlive { get => AliveOverride ?? (IsSpawned && HP > 0); set => AliveOverride = value; }
        public bool IsDead { get => DeadOverride ?? (!IsSpawned || HP <= 0); set => DeadOverride = value; }
        public bool IsEngaged { get; set; }
        public bool IsMob { get; set; } = true;
        // the engine spawns only when respawnSec is nil
        public void Spawn(int? despawnSec = null, int? respawnSec = null)
        {
            Rec("Spawn", despawnSec, respawnSec);
            if (respawnSec == null) SpawnInWorld();
        }
        // live ScriptSpawn -> SpawnMob on a mob not in the world: CMobEntity::Reset (hp = maxhp, mp = maxmp, tp 0) and
        // ResetCombat (not engaged, no target, no enmity); one already spawned is left as it is
        internal void SpawnInWorld()
        {
            if (IsSpawned) return;
            IsSpawned = true;
            HP = MaxHP;
            HPP = 100;
            MP = MaxMP;
            TP = 0;
            IsEngaged = false;
            Target = null;
            EnmityEntries.Clear();
        }
        public (double X, double Y, double Z, int Rot) SpawnPoint { get; set; }
        public void SetSpawn(double x, double y, double z, int? rot = null)
        {
            Rec("SetSpawn", x, y, z, rot);
            SpawnPoint = (x, y, z, rot ?? 0);
        }
        public (double X, double Y, double Z, int Rot) Pos { get; set; }
        public void SetPos(double x, double y, double z, int? rot = null)
        {
            Rec("SetPos", x, y, z, rot);
            Pos = (x, y, z, rot ?? Pos.Rot);
        }
        public (double X, double Y, double Z, int Rot) GetPos() => Pos;
        public void LookAt(double x, double y, double z) => Rec("LookAt", x, y, z);
        public int HP { get; set; }
        public void SetHP(int value)
        {
            Rec("SetHP", value);
            // live: hp = 0 then addHP, so clamped to 0..MaxHP; hpp floored, at least 1 while alive, 0 at 0; an
            // unkillable mob left at 0 keeps 1 (MobAddHp)
            HP = Math.Max(0, Math.Min(value, MaxHP));
            if (HP == 0 && Unkillable) HP = 1;
            HPP = HP == 0 ? 0 : Math.Max(1, (int)Math.Floor((float)HP / MaxHP * 100.0f));
        }
        public int HPP { get; set; }
        public int MaxHP { get; set; }
        public int MaxMP { get; set; }
        public int MP { get; set; }
        public void SetMP(int value)
        {
            Rec("SetMP", value);
            MP = Math.Max(0, Math.Min(value, MaxMP)); // live: mp = 0 then addMP, clamped to 0..MaxMP
        }
        public double TP { get; set; }
        public void SetTP(int value)
        {
            Rec("SetTP", value);
            TP = value;
        }
        public IBattlefieldMob Target { get; set; }
        public IBattlefieldMob GetTarget() => Target;
        // the targids of the chars in the mob's area; live engage for a mob caller finds only chars
        public readonly HashSet<int> CharTargIds = new HashSet<int>();
        // the facet a char of CharTargIds answers as (what live GetTarget answers once the mob engaged it)
        public readonly Dictionary<int, IBattlefieldMob> CharTargets = new Dictionary<int, IBattlefieldMob>();
        // the engine acts only when the targid is above 0 and a char has it
        public void Engage(int targId)
        {
            Rec("Engage", targId);
            if (targId > 0 && CharTargIds.Contains(targId))
            {
                IsEngaged = true;
                if (CharTargets.TryGetValue(targId, out var t)) Target = t; // MobEngage sets BattleTarget
            }
        }
        // live: engaged or not, CMobController::Disengage clears the target and the enmity list
        public void Disengage()
        {
            Rec("Disengage");
            IsEngaged = false;
            Target = null;
            EnmityEntries.Clear();
        }
        // live engages (MobEngage) on a char target only, when the mob's raw hp > 0 and DeathAtMs == 0 (not the
        // derived IsAlive / IsDead, which also count not being spawned); MobEngage sets BattleTarget even when already
        // engaged. A char is a player, or the char GetTarget answers as a mob facet (IsMob false)
        public void UpdateEnmity(IBattlefieldEntity entity)
        {
            Rec("UpdateEnmity", EntityId(entity));
            bool isChar = entity is IBattlefieldPlayer || entity is BattlefieldMobFake { IsMob: false };
            if (!isChar || !(AliveOverride ?? HP > 0) || (DeadOverride ?? false)) return;
            IsEngaged = true;
            if (entity is IBattlefieldMob target) Target = target;
        }
        public void AddEnmity(IBattlefieldEntity entity, int ce, int ve) => Rec("AddEnmity", EntityId(entity), ce, ve);
        public void ResetEnmity(IBattlefieldEntity entity) => Rec("ResetEnmity", EntityId(entity));
        public List<BattlefieldEnmityEntry> EnmityEntries { get; } = new List<BattlefieldEnmityEntry>();
        public IReadOnlyList<BattlefieldEnmityEntry> GetEnmityList() => EnmityEntries.ToList();
        public void UseMobAbility() => Rec("UseMobAbility");
        public void UseMobAbility(int skillId) => Rec("UseMobAbility", skillId);
        public void UseMobAbility(int skillId, IBattlefieldEntity target) =>
            Rec("UseMobAbility", skillId, EntityId(target));
        public void SetMobAbilityEnabled(bool state) => Rec("SetMobAbilityEnabled", state);
        public void SetMagicCastingEnabled(bool state) => Rec("SetMagicCastingEnabled", state);
        public void SetAutoAttackEnabled(bool state) => Rec("SetAutoAttackEnabled", state);
        public int Behavior { get; set; }
        public void SetBehavior(int behavior)
        {
            Rec("SetBehavior", behavior);
            Behavior = behavior;
        }
        public int GetBehavior() => Behavior;
        public void SetMobMod(int mobMod, int value) => Rec("SetMobMod", mobMod, value);
        public bool Unkillable { get; private set; }
        public void SetUnkillable(bool value)
        {
            Rec("SetUnkillable", value);
            Unkillable = value;
        }
        public void SetUntargetable(bool value) => Rec("SetUntargetable", value);
        public void SetSpawnAnimation(int value) => Rec("SetSpawnAnimation", value);
        public void SetAnimation(int value) => Rec("SetAnimation", value);
        public void SetStatus(int value) => Rec("SetStatus", value);
        public IBattlefieldView Battlefield { get; set; }
        public IBattlefieldView GetBattlefield() => Battlefield;
        public int GetBattlefieldID() => Battlefield?.Id ?? -1;
        public int Pool { get; set; }
        public string Name { get; set; }
        public int Race { get; set; }
        public int TargId { get; set; }

        // ---- deferred callbacks (quest rework C3 PR2): armed into Callbacks, recorded in Calls ----
        // the queues this mob arms into (BattlefieldEngineFake.MobById sets the engine's); none: an arming throws
        public BattlefieldCallbacksFake Callbacks { get; set; }
        // the mob cannot change state (a skill, a cast): a queued action waits
        public bool Busy { get; set; }
        private BattlefieldCallbacksFake Queues =>
            Callbacks ?? throw new InvalidOperationException($"fake mob {Id} has no Callbacks to arm into");
        public void Timer(int ms, BattlefieldCallback callback, params object[] captures)
        {
            Queues.Arm(this, ms, false, callback, captures);
            Rec("Timer", ms, callback.Name);
        }
        public void Queue(int ms, BattlefieldCallback callback, params object[] captures)
        {
            Queues.Arm(this, ms, true, callback, captures);
            Rec("Queue", ms, callback.Name);
        }
        public void AddListener(string evt, string identifier, BattlefieldCallback callback, params object[] captures)
        {
            Queues.Listen(this, evt, identifier, callback, captures);
            Rec("AddListener", evt, identifier, callback.Name);
        }
        public void RemoveListener(string identifier)
        {
            Rec("RemoveListener", identifier);
            Callbacks?.Unlisten(this, identifier);
        }
    }

    // an action of the fake engine: Battlefield:event(csid, args...), or the exit cutscene (Exit), or
    // Container:progressEvent (Progress)
    public sealed class BattlefieldActionFake : IBattlefieldAction
    {
        public int Csid;
        public int[] Args = Array.Empty<int>();
        public bool Exit;
        public bool Progress;
        public int? Priority;

        public IBattlefieldAction SetPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        // a priority set on an event or exit action shows too (none shows nothing)
        private string Pri => Priority.HasValue ? ";" + Priority : "";

        public override string ToString() =>
            Exit ? $"exit({Csid}{Pri})" :
            Progress ? $"progress({Csid},{string.Join(",", Args)};{Priority})" :
            $"event({Csid},{string.Join(",", Args)}{Pri})";
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
        // what insertEntity answers per targid; a targid not in it is nil
        public readonly Dictionary<int, IBattlefieldMob> InsertResults = new Dictionary<int, IBattlefieldMob>();
        public IBattlefieldMob InsertEntity(int targId, bool ally, bool inBattlefield)
        {
            Record("InsertEntity", targId, ally, inBattlefield);
            var found = InsertResults.TryGetValue(targId, out var m) ? m : null;
            if (found != null && ally) AllyList.Add(found); // live makes a mob per call
            return found;
        }
        public List<IBattlefieldMob> AllyList { get; } = new List<IBattlefieldMob>();
        public IReadOnlyList<IBattlefieldMob> Allies() => AllyList.ToList();
        public void Cleanup(bool cleanup) => Record("Cleanup", cleanup);
        public void Win() => Record("Win");
        public void Lose() => Record("Lose");
        public void AddGroups(IBattlefieldContent content, bool hasMultipleAreas) =>
            Record("AddGroups", content?.BattlefieldId, hasMultipleAreas);
    }

    // a content table: a C# battlefield's (over its data, its methods its hooks on a context like the caller's) or a
    // Lua one's (Lua: its fields set by the test, its methods recorded in Calls)
    public sealed class BattlefieldContentFake : IBattlefieldContent, IBattlefieldPaths
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
        public BattlefieldLootTable Loot => loot_ ??= LootByHandle
            ? BattlefieldLootTable.OfEngine(Script?.Data.Loot ?? LuaLoot)
            : new BattlefieldLootTable(Script?.Data.Loot ?? LuaLoot);
        // Loot as an engine handle (BattlefieldLootTable.OfEngine), as the live bridge gives self.loot
        public bool LootByHandle { get; set; }
        // content.paths with their ids resolved, for OnBattlefieldInitialize
        public List<(int MobId, double[] Points)> LuaPaths { get; } = new List<(int MobId, double[] Points)>();
        public IEnumerable<(int MobId, double[] Points)> ResolvedPaths() => LuaPaths;

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
        // a Lua module's override of Battlefield.<method>, set by a test: it runs with the call's context and the
        // super (the framework's own method)
        public readonly Dictionary<string, Action<IBattlefieldContext, Action<IBattlefieldContext>>> ModuleOverrides =
            new Dictionary<string, Action<IBattlefieldContext, Action<IBattlefieldContext>>>();

        public bool ModuleOverride(string method, IBattlefieldContext q, Action<IBattlefieldContext> body)
        {
            if (!ModuleOverrides.TryGetValue(method, out var run)) return false;
            Calls.Add($"ModuleOverride({method})");
            run(q, body);
            return true;
        }

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
        // a fake mob answered arms its callbacks into Callbacks (when set and the mob has none of its own)
        public IBattlefieldMob MobById(int id)
        {
            if (!MobsById.TryGetValue(id, out var m)) return null;
            if (m is BattlefieldMobFake f && f.Callbacks == null) f.Callbacks = Callbacks;
            return m;
        }
        // the engine's action queues and listeners (quest rework C3 PR2); null: none
        public BattlefieldCallbacksFake Callbacks { get; set; }
        // an entity kept: the facet itself; read back: it, or a fake player's AsEntity
        public BattlefieldKeptEntity Keep(IBattlefieldEntity entity) =>
            entity == null ? null : new BattlefieldKeptEntity(entity);
        public IBattlefieldMob Entity(BattlefieldKeptEntity kept) =>
            kept?.Handle as IBattlefieldMob ?? (kept?.Handle as BattlefieldPlayerFake)?.AsEntity;
        // the zone's IDs entries ("<zone>:<key>" to the first id), set by the test
        public readonly Dictionary<string, int> ZoneMobIds = new Dictionary<string, int>();
        public readonly Dictionary<string, int> ZoneNpcIds = new Dictionary<string, int>();
        public int? ZoneMobId(int zone, string key) => ZoneMobIds.TryGetValue($"{zone}:{key}", out var id) ? id : null;
        public int? ZoneNpcId(int zone, string key) => ZoneNpcIds.TryGetValue($"{zone}:{key}", out var id) ? id : null;
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
            var groups = lootTable.Groups ?? (IReadOnlyList<BattlefieldLootGroup>)lootTable.Handle;
            return groups.Where(g => g.Entries.Count > 0)
                .Select(g => (g.Entries[0].ItemId, (double?)g.Entries[0].Amount)).ToList();
        }
        public void GiveCurrency(IBattlefieldPlayer player, string currency, double amount, bool silent) =>
            Calls.Add($"GiveCurrency({player.Id},{currency},{amount},{silent})");
        public bool BcnmEnableExperimental { get; set; }
        public IBattlefieldAction Event(int csid, params int[] args) =>
            new BattlefieldActionFake { Csid = csid, Args = args };
        public IBattlefieldAction ExitCutscene() => new BattlefieldActionFake { Csid = 32003, Exit = true };
        public bool QuestEvent(IBattlefieldContext q, string key, int csid, int option)
        {
            Calls.Add($"QuestEvent({key},{csid},{option})");
            return true;
        }
        public IBattlefieldAction ProgressEvent(int csid, params int[] args) =>
            new BattlefieldActionFake { Csid = csid, Args = args, Progress = true };
        // SpawnMob answers the mob of MobsById and spawns it (a fake mob's IsSpawned set)
        public IBattlefieldMob SpawnMob(int id)
        {
            Calls.Add($"SpawnMob({id})");
            var mob = MobById(id);
            if (mob is BattlefieldMobFake f)
            {
                // live: a spawn's PAI->Reset clears its timers and queued actions (ZoneHost.SpawnMob)
                if (!f.IsSpawned) f.Callbacks?.Clear(f);
                f.SpawnInWorld();
            }
            return mob;
        }
        public void DespawnMob(int id)
        {
            Calls.Add($"DespawnMob({id})");
            if (MobById(id) is BattlefieldMobFake f) f.IsSpawned = false;
        }
        public readonly Dictionary<int, IBattlefieldPlayer> PlayersById = new Dictionary<int, IBattlefieldPlayer>();
        public IBattlefieldPlayer GetPlayerByID(int id) => PlayersById.TryGetValue(id, out var p) ? p : null;
        public uint SystemTime { get; set; }
        public uint GetSystemTime() => SystemTime;
        // what RandomInt answers, in order; when empty, the lower bound
        public readonly Queue<int> Rolls = new Queue<int>();
        public int RandomInt(int lower, int upper)
        {
            Calls.Add($"RandomInt({lower},{upper})");
            return Rolls.Count > 0 ? Rolls.Dequeue() : lower;
        }
        // what Shuffle answers; null: the values as given
        public int[] ShuffleResult { get; set; }
        public IReadOnlyList<int> Shuffle(IReadOnlyList<int> values)
        {
            Calls.Add($"Shuffle({string.Join(",", values)})");
            return ShuffleResult ?? values.ToArray();
        }
        // the ids of the mobs xi.combat.behavior.isEntityBusy answers true for
        public readonly HashSet<int> BusyMobs = new HashSet<int>();
        public bool IsEntityBusy(IBattlefieldMob mob) => BusyMobs.Contains(mob.Id);
    }

    // the engine's action queues and listeners over fakes (quest rework C3 PR2): CAIActionQueue's timers and queued
    // actions per entity, CAIEventHandler's listeners per entity, and a clock. A fake mob or player arms into the
    // one its Callbacks names (BattlefieldEngineFake.MobById sets the engine's)
    public sealed class BattlefieldCallbacksFake
    {
        // script: the battlefield whose callbacks may be armed (BattlefieldCallback.CheckArming) and whose context a
        // firing gets; engine: that context's Engine
        public BattlefieldCallbacksFake(BattlefieldScript script, IBattlefieldEngine engine)
        {
            Script = script;
            Engine = engine;
        }

        public BattlefieldScript Script { get; set; }
        public IBattlefieldEngine Engine { get; set; }
        // the clock: an arming's deadline is NowMs + ms; Tick moves it
        public long NowMs { get; set; }
        // each firing in order, "<callback>@<entity id>"
        public readonly List<string> Fired = new List<string>();

        // a timer or a queued action armed and not yet run
        public sealed class PendingFake
        {
            public object Entity { get; internal set; }
            public long Deadline { get; internal set; }
            // queue (CAIContainer's checkState): it also waits until the entity can change state
            public bool CheckState { get; internal set; }
            public BattlefieldCallback Callback { get; internal set; }
            public object[] Captures { get; internal set; }
            internal long Order;
        }

        // a listener added and not removed, in adding order
        public sealed class ListenerFake
        {
            public object Entity { get; internal set; }
            public string Event { get; internal set; }
            public string Identifier { get; internal set; }
            public BattlefieldCallback Callback { get; internal set; }
            public object[] Captures { get; internal set; }
        }

        public readonly List<PendingFake> Pending = new List<PendingFake>();
        public readonly List<ListenerFake> Listeners = new List<ListenerFake>();
        private long order_;

        internal void Arm(object entity, int ms, bool checkState, BattlefieldCallback callback, object[] captures)
        {
            BattlefieldCallback.CheckArming(Script, callback, ms, captures);
            Pending.Add(new PendingFake
            {
                Entity = entity, Deadline = NowMs + ms, CheckState = checkState, Callback = callback,
                Captures = captures ?? Array.Empty<object>(), Order = order_++,
            });
        }

        // an identifier added again replaces the first, whatever its event (CAIEventHandler::addListener)
        internal void Listen(object entity, string evt, string identifier, BattlefieldCallback callback,
                             object[] captures)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            BattlefieldCallback.CheckArming(Script, callback, 0, captures);
            Unlisten(entity, identifier);
            Listeners.Add(new ListenerFake
            {
                Entity = entity, Event = evt, Identifier = identifier, Callback = callback,
                Captures = captures ?? Array.Empty<object>(),
            });
        }

        internal void Unlisten(object entity, string identifier) =>
            Listeners.RemoveAll(x => ReferenceEquals(x.Entity, entity) && x.Identifier == identifier);

        // CAIContainer::Reset (a spawn): the entity's timers and queued actions dropped; its listeners stay, as the
        // engine's EventHandler does. A despawn alone drops nothing (a despawned mob still ticks its queue)
        public void Clear(object entity) => Pending.RemoveAll(p => ReferenceEquals(p.Entity, entity));

        // CBattlefield::RemoveEntity (each of its entities at the battlefield's end, or a char leaving):
        // ClearTimerQueue, the timers dropped, the queued actions kept
        public void RemoveEntity(object entity) =>
            Pending.RemoveAll(p => ReferenceEquals(p.Entity, entity) && !p.CheckState);

        // whether the entity can change state (CAIContainer::CanChangeState): a queued action waits while false.
        // Default: a fake mob that is not Busy
        public Func<object, bool> CanChangeState { get; set; } = e => e is not BattlefieldMobFake { Busy: true };

        private static int EntityOrder(object e) => e switch
        {
            IBattlefieldMob m => m.Id,
            IBattlefieldPlayer p => p.Id,
            _ => 0,
        };

        // the clock set to nowMs, then each entity's due actions as CAIActionQueue::checkAction runs them, the
        // entities by id: each timer whose deadline is strictly before the tick (tick > deadline), then each queued
        // action so due while the entity can change state, soonest first and arming order among equals, one at a
        // time (one a callback arms meanwhile is due at the earliest on the next tick, its deadline NowMs + ms).
        // Returns how many ran
        public int Tick(long nowMs)
        {
            NowMs = nowMs;
            int ran = 0;
            var entities = Pending.Select(p => p.Entity).Distinct().OrderBy(EntityOrder).ToList();
            foreach (var entity in entities)
            {
                while (true)
                {
                    PendingFake Due(bool checkState) => Pending
                        .Where(p => ReferenceEquals(p.Entity, entity) && p.CheckState == checkState &&
                                    nowMs > p.Deadline)
                        .OrderBy(p => p.Deadline).ThenBy(p => p.Order).FirstOrDefault();
                    var due = Due(false) ?? (CanChangeState(entity) ? Due(true) : null);
                    if (due == null) break;
                    Pending.Remove(due);
                    Run(due.Callback, entity, new object[] { entity }, due.Captures);
                    ran++;
                }
            }
            return ran;
        }

        // the entity's listeners of the event, each run once in adding order with the engine's arguments (the entity
        // first), as triggerListener: one a callback removes meanwhile still runs this time. Returns how many ran
        public int Trigger(object entity, string evt, params object[] args)
        {
            var now = Listeners.Where(x => ReferenceEquals(x.Entity, entity) && x.Event == evt).ToList();
            foreach (var x in now) Run(x.Callback, entity, new[] { entity }.Concat(args).ToArray(), x.Captures);
            return now.Count;
        }

        // the callback on a fresh context: the battlefield's, no npc, trade or battlefield argument, the player the
        // entity when it is one; its label reached
        private void Run(BattlefieldCallback callback, object entity, object[] args, object[] captures)
        {
            Fired.Add($"{callback.Name}@{EntityOrder(entity)}");
            var q = new BattlefieldContextFake(Script)
            {
                Engine = Engine, Player = entity as IBattlefieldPlayer, ReachedLabels = ReachedLabels,
            };
            q.Reached(callback.Label);
            callback.Run(q, new ArgsFake(args, captures));
        }

        // the labels the firings reached
        public readonly List<string> ReachedLabels = new List<string>();

        private sealed class ArgsFake : IBattlefieldCallbackArgs
        {
            private readonly object[] args_, captures_;

            public ArgsFake(object[] args, object[] captures)
            {
                args_ = args;
                captures_ = captures;
            }

            private static IBattlefieldMob AsEntity(object o) =>
                o as IBattlefieldMob ?? (o as BattlefieldPlayerFake)?.AsEntity;

            public IBattlefieldMob Entity => AsEntity(args_[0]);
            public int ArgCount => args_.Length;
            public IBattlefieldMob EntityArg(int index) =>
                index >= 1 && index <= args_.Length ? AsEntity(args_[index - 1]) : null;
            public int CaptureCount => captures_.Length;
            public IBattlefieldView CapturedBattlefield(int index) => (IBattlefieldView)captures_[index];
            public IBattlefieldMob CapturedEntity(int index) => AsEntity(captures_[index]);
            public int CapturedInt(int index) =>
                captures_[index] is IConvertible c ? c.ToInt32(System.Globalization.CultureInfo.InvariantCulture) : 0;
        }
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
        public IBattlefieldText Text => new BattlefieldTextAccessor(this);
        public List<string> ReachedLabels { get; init; } = new List<string>();
        public void Reached(string label) => ReachedLabels.Add(label);
    }
}
