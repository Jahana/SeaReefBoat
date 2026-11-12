// XI.Quests -- quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section
// 3.3): what a battlefield's hooks and the framework port (BattlefieldFramework) see of the engine. Each member is one
// Lua call the framework makes (scripts/globals/battlefield.lua): a binding of the player, of CLuaBattlefield or of a
// content table, or a Lua global (npcUtil, utils, xi.battlefield). The bridge (ZoneHost.BattlefieldScripts) implements
// them over the Lua values of the call; the fakes (BattlefieldFakes) over plain fields.
using System;
using System.Collections.Generic;

namespace XI.Quests.Authoring
{
    // one call of a battlefield hook or of a framework handler: its arguments and the engine
    public interface IBattlefieldContext
    {
        // the battlefield whose hook runs; null in a zone handler no battlefield owns (onEntryTrade, the redirects)
        BattlefieldScript Script { get; }
        // the framework's own Lua for the battlefield, ported (the hooks' defaults); null with no Script
        BattlefieldFramework Framework { get; }
        // the battlefield's content table (self); null with no Script
        IBattlefieldContent Self { get; }
        // the player argument; null in a zone-wide hook (onBattlefieldTick, onBattlefieldInitialize)
        IBattlefieldPlayer Player { get; }
        // the npc argument; null when the call has none
        IBattlefieldNpc Npc { get; }
        // the trade argument; null for nil
        IQuestTrade Trade { get; }
        // the battlefield argument (CLuaBattlefield); null when the call has none
        IBattlefieldView Battlefield { get; }
        IBattlefieldEngine Engine { get; }
        // the zone's texts by their generated names (XI.QuestContent.Text.<Zone>.<NAME>)
        IBattlefieldText Text { get; }
        // the coverage hook: a label of the battlefield reached (BattlefieldScript.CoverageLabels)
        void Reached(string label);
    }

    // the texts a hook sends, by the generated ZoneText
    public interface IBattlefieldText
    {
        // zones[text.Zone].text[text.Name]: the id for messageSpecial, messageText and showText, and for an offset
        // (q.Text.Id(Text.X) + 1). A text of another zone, or one the zone's IDs table lacks, throws
        int Id(ZoneText text);
    }

    // the player bindings the framework calls beyond the quest ones
    public interface IBattlefieldPlayer : IQuestPlayer, IBattlefieldEntity
    {
        // player:getZoneID()
        int ZoneId { get; }
        // player:getStatus() (xi.status)
        int EntityStatus { get; }
        // player:isAlive()
        bool IsAlive { get; }
        // player:getGMLevel(), player:getVisibleGMLevel()
        int GMLevel { get; }
        int VisibleGMLevel { get; }
        // player:getAlliance(), in its order
        IReadOnlyList<IBattlefieldPlayer> AllianceList();
        // player:getBattlefield(); null for nil
        IBattlefieldView CurrentBattlefield { get; }
        // player:getRegisteredBattlefield(); null for nil
        IBattlefieldView RegisteredBattlefield { get; }
        // player:getStatusEffect(effect); null for nil
        IBattlefieldEffect StatusEffect(int effect);
        // player:copyStatusEffect(effect)
        void CopyStatusEffect(IBattlefieldEffect effect);
        // player:registerBattlefield(id, area, initiator, content): a returnCode
        int RegisterBattlefield(int battlefieldId, int area, int initiatorId, IBattlefieldContent content);
        // player:battlefieldAtCapacity(id)
        bool BattlefieldAtCapacity(int battlefieldId);
        // player:enterBattlefield()
        void EnterBattlefield();
        // player:leaveBattlefield(leavecode)
        void LeaveBattlefield(int leaveCode);
        // player:setEnteredBattlefield(value)
        void SetEnteredBattlefield(bool value);
        // player:messageBasic(id, args...)
        void MessageBasic(int messageId, params int[] args);
        // player:messageSpecial(textId, args...), the text id resolved
        void MessageSpecial(int textId, params int[] args);
        // player:messageText(npc, textId, showName)
        void MessageText(IBattlefieldNpc npc, int textId, bool showName);
        // player:showText(mob, textId): the mob says it (waking_the_beast.lua:180)
        void ShowText(IBattlefieldMob mob, int textId);
        // player:updateEvent(args...) with mixed numbers and booleans, as the Lua passes them
        void UpdateEventValues(params object[] args);
        // player:updateEventString(s)
        void UpdateEventString(string s);
        // player:getWornUses(item), player:incrementItemWear(item)
        int WornUses(int item);
        int IncrementItemWear(int item);
        // player:tradeComplete()
        void TradeComplete();
        // player:getVar(name), player:setVar(name, value) (Container:getVar / setVar with the prefix)
        int PlayerVar(string name);
        void SetPlayerVar(string name, int value);
        // player:startEvent(csid, table): the table as the event's params
        void StartEventTable(int csid, int[] table);
        // player:delStatusEffectsByFlag(flag) (no silent argument)
        void DelStatusEffectsByFlag(int flag);
        // player:printToPlayer(message, channel)
        void PrintToPlayer(string message, int channel);
        // player:addTreasure(item, npc)
        void AddTreasure(int item, IBattlefieldNpc npc);
        // player:getParty() (CBattleEntity::ForParty: the player's party with the player in it), in the table's order
        IReadOnlyList<IBattlefieldPlayer> Party();
        // player:getPet(): the pet entity; null for nil
        IBattlefieldMob Pet();
        // player:getTargID() (quest rework C6, celestial_nexus.lua's phase two engages it)
        int TargId { get; }
        // player:getMaxHP(), player:getMaxMP()
        int MaxHP { get; }
        int MaxMP { get; }
        // player:setTP(value): an int16 in the engine
        void SetTP(int value);
        // player:isDead()
        bool IsDead { get; }
        // player:allowSendRaisePrompt(): only does something while the player is in the death state
        void AllowSendRaisePrompt();
        // player:sendEntityUpdateToPlayer(entity, entityUpdate, updateMask): the player's client told of the entity
        void SendEntityUpdateToPlayer(IBattlefieldMob entity, int entityUpdate, int updateMask);
        // player:addListener(evt, identifier, fn) with a battlefield callback (quest rework C3 PR2), as
        // IBattlefieldMob.AddListener; player:removeListener(identifier) is IQuestPlayer.RemoveListener
        void AddListener(string evt, string identifier, BattlefieldCallback callback, params object[] captures);
    }

    // any entity a Lua call takes as an argument: a mob, a pet, an ally (IBattlefieldMob) or a player; the Lua
    // object is the same for all of them
    public interface IBattlefieldEntity
    {
    }

    // one entry of mob:getEnmityList(), valid in the call that read it; a hook that keeps the list past its call keeps
    // each Entity with IBattlefieldEngine.Keep (quest rework C3 PR2)
    public sealed class BattlefieldEnmityEntry
    {
        public IBattlefieldMob Entity;
        public int Ce, Ve;
        public bool Active, Tameable;
    }

    // a status effect object (CStatusEffect)
    public interface IBattlefieldEffect
    {
        int Power { get; }
    }

    // an NPC object
    public interface IBattlefieldNpc
    {
        // npc:getID(), npc:getName(), npc:getTargID()
        int Id { get; }
        string Name { get; }
        int TargId { get; }
        // npc:setAnimation(xi.animation) (the Darkness Named tiles: CLOSE_DOOR)
        void SetAnimation(int value);
    }

    // a mob object
    // The Lua object is the same for every entity, so this facet also answers a getTarget, a getPet or an
    // insertEntity (a player, a pet, an ally). A call the engine only runs on a mob does nothing on another one.
    public interface IBattlefieldMob : IBattlefieldEntity
    {
        int Id { get; }
        int MainLevel { get; }
        void SetMobLevel(int level);
        // mob:pathThrough(points, flags)
        void PathThrough(double[] points, int flags);
        // mob:messageText(mob, textId): the mob's own line (head_wind.lua:267); an id read with q.Text.Id and an offset
        void MessageText(int textId);

        // mob:getLocalVar(name), mob:setLocalVar(name, value): a uint32 in the engine
        uint LocalVar(string name);
        void SetLocalVar(string name, uint value);
        // mob:resetLocalVars()
        void ResetLocalVars();
        // mob:isSpawned(), mob:isAlive(), mob:isDead(), mob:isEngaged(), mob:isMob()
        bool IsSpawned { get; }
        bool IsAlive { get; }
        bool IsDead { get; }
        bool IsEngaged { get; }
        bool IsMob { get; }
        // mob:spawn(despawnSec, respawnSec): a null argument is nil; the engine spawns only when respawnSec is nil
        void Spawn(int? despawnSec = null, int? respawnSec = null);
        // mob:setSpawn(x, y, z, rot): a null rot is 0 (a mob only)
        void SetSpawn(double x, double y, double z, int? rot = null);
        // mob:setPos(x, y, z, rot): a null rot is left out
        void SetPos(double x, double y, double z, int? rot = null);
        // mob:getPos(): the table { x, y, z, rot } (the key is rot, not rotation)
        (double X, double Y, double Z, int Rot) GetPos();
        // mob:lookAt(x, y, z), mob:lookAt(pos): a position, not an entity
        void LookAt(double x, double y, double z);
        // mob:getHP(), mob:setHP(value) (hp to 0, then addHP), mob:getHPP(), mob:getMaxHP(), mob:getMaxMP()
        int HP { get; }
        void SetHP(int value);
        int HPP { get; }
        int MaxHP { get; }
        int MaxMP { get; }
        // mob:setMP(value)
        void SetMP(int value);
        // mob:getTP(): a float of the int16 tp; mob:setTP(value): an int16
        double TP { get; }
        void SetTP(int value);
        // mob:getTarget(): the battle target (may be a player); null for nil
        IBattlefieldMob GetTarget();
        // mob:engage(targId): acts only when above 0; for a mob caller the C# finds only chars at that targid
        void Engage(int targId);
        // mob:disengage()
        void Disengage();
        // mob:updateEnmity(entity): C++ calls PAI->Engage(entity) for any non-npc target; the C# engages only a char target (ZoneHost.MobBindings.cs), a mob target does nothing
        void UpdateEnmity(IBattlefieldEntity entity);
        // mob:addEnmity(entity, ce, ve): C++ swaps the direction when the caller is a pc, a pet or a charmed mob; the C# swaps only for a char caller (ZoneHost.CombatBindings.cs)
        void AddEnmity(IBattlefieldEntity entity, int ce, int ve);
        // mob:resetEnmity(entity): LowerEnmityByPercent(entity, 100)
        void ResetEnmity(IBattlefieldEntity entity);
        // mob:getEnmityList(): empty for nil (a non-mob)
        IReadOnlyList<BattlefieldEnmityEntry> GetEnmityList();
        // mob:useMobAbility(), mob:useMobAbility(skill), mob:useMobAbility(skill, target)
        void UseMobAbility();
        void UseMobAbility(int skillId);
        void UseMobAbility(int skillId, IBattlefieldEntity target);
        // mob:setMobAbilityEnabled(state), mob:setMagicCastingEnabled(state), mob:setAutoAttackEnabled(state)
        void SetMobAbilityEnabled(bool state);
        void SetMagicCastingEnabled(bool state);
        void SetAutoAttackEnabled(bool state);
        // mob:setBehavior(behavior), mob:getBehavior(): xi.behavior flags
        void SetBehavior(int behavior);
        int GetBehavior();
        // mob:setMobMod(mobMod, value)
        void SetMobMod(int mobMod, int value);
        // mob:setUnkillable(bool), mob:setUntargetable(bool)
        void SetUnkillable(bool value);
        void SetUntargetable(bool value);
        // mob:setSpawnAnimation(xi.spawnAnimation), mob:setAnimation(xi.animation), mob:setStatus(xi.status)
        void SetSpawnAnimation(int value);
        void SetAnimation(int value);
        void SetStatus(int value);
        // mob:getBattlefield(): null for nil
        IBattlefieldView GetBattlefield();
        // mob:getBattlefieldID(): -1 when in none
        int GetBattlefieldID();
        // mob:getPool(): a uint32 (0 for a non-mob)
        int Pool { get; }
        // mob:getName(), mob:getRace(), mob:getTargID()
        string Name { get; }
        int Race { get; }
        int TargId { get; }

        // ---- deferred callbacks (quest rework C3 PR2) ----
        // mob:timer(ms, fn), mob:queue(ms, fn), mob:addListener(evt, identifier, fn): fn a closure over one of the
        // battlefield's callbacks (BattlefieldData.Callbacks) and the captured values (the Lua closure's upvalues: a
        // battlefield, an entity or a player facet of this call, a number, a string, a bool, null for nil). A callback
        // not the battlefield's, or a delay below 0, is refused
        void Timer(int ms, BattlefieldCallback callback, params object[] captures);
        void Queue(int ms, BattlefieldCallback callback, params object[] captures);
        void AddListener(string evt, string identifier, BattlefieldCallback callback, params object[] captures);
        // mob:removeListener(identifier)
        void RemoveListener(string identifier);
    }

    // the CLuaBattlefield bindings, one to one
    public interface IBattlefieldView
    {
        int Id { get; }
        int ZoneId { get; }
        int Area { get; }
        int TimeLimit { get; }
        int TimeInside { get; }
        int RemainingTime { get; }
        int WipeTime { get; }
        void SetWipeTime(int seconds);
        int MaxParticipants { get; }
        int PlayerCount { get; }
        IReadOnlyList<IBattlefieldPlayer> Players();
        IReadOnlyList<IBattlefieldMob> Mobs(bool required, bool additional);
        (string Name, int ClearTime, int PartySize) Record();
        void SetRecord(string name, int clearTime, int partySize);
        int Status { get; }
        void SetStatus(int status);
        // battlefield:getLocalVar / setLocalVar: a ulong the engine pushes as a number (cutsceneTimer - 1 at 0 is
        // 2^64-1)
        double LocalVar(string name);
        void SetLocalVar(string name, double value);
        int LastTimeUpdate { get; }
        void SetLastTimeUpdate(int seconds);
        void SetTimeLimit(int seconds);
        (int Id, string Name) Initiator();
        int ArmouryCrate { get; }
        // battlefield:insertEntity(targId, ally, inBattlefield): the entity put in; null for nil (not found)
        IBattlefieldMob InsertEntity(int targId, bool ally, bool inBattlefield);
        // battlefield:getAllies(), in the table's order
        IReadOnlyList<IBattlefieldMob> Allies();
        void Cleanup(bool cleanup);
        void Win();
        void Lose();
        // battlefield:addGroups(content.groups, hasMultipleAreas)
        void AddGroups(IBattlefieldContent content, bool hasMultipleAreas);
    }

    // a battlefield's content table (xi.battlefield.contents[id]), a Lua one or a C# one's proxy
    public interface IBattlefieldContent
    {
        int BattlefieldId { get; }
        int Index { get; }
        bool Experimental { get; }
        IReadOnlyList<string> EntryNpcs { get; }
        // #content.requiredItems
        int RequiredItemCount { get; }
        // content.requiredItems[1]
        int FirstRequiredItem { get; }
        // content.requiredItems.wornMessage: a number (WornMessage), a table (WornMessageTable), or nil (both null)
        int? WornMessage { get; }
        int[] WornMessageTable { get; }
        // npcUtil.tradeHas(trade, content.tradeItems)
        bool TradeHas(IQuestTrade trade);
        // content.armouryCrates is a table (not false)
        bool HasArmouryCrates { get; }
        // content.armouryCrates[area]; null for nil
        int? ArmouryCrate(int area);
        // content.loot (self.loot)
        BattlefieldLootTable Loot { get; }
        // content:checkRequirements(player, npc, isRegistrant, trade): q's player, npc and trade
        bool CheckRequirements(IBattlefieldContext q, bool isRegistrant, bool passTrade);
        // content:onEntryEventUpdate(player, csid, option, npc)
        void OnEntryEventUpdate(IBattlefieldContext q, int csid, int option);
        // content[eventName](content, player, csid, option)
        void Call(string eventName, IBattlefieldContext q, int csid, int option);
        // the coverage hook of a C# battlefield's content (a label of it reached through its table); a Lua one's does
        // nothing
        void Reached(string label);
    }

    // the Lua globals and helpers the framework calls
    public interface IBattlefieldEngine
    {
        // zones[zone].text[name]; null for nil
        int? Text(int zone, string name);
        // xi.battlefield.contents[id]; null for nil
        IBattlefieldContent Content(int battlefieldId);
        // xi.battlefield.contentsByZone[zone], in its order; null for nil
        IReadOnlyList<IBattlefieldContent> ZoneContents(int zone);
        // xi.battlefield.getMaxAreas(zone)
        int MaxAreas(int zone);
        // xi.battlefield.itemUses[item]; null for nil
        int? ItemUses(int item);
        // xi.battlefield.SendTimePrompts(battlefield, players)
        bool SendTimePrompts(IBattlefieldView battlefield, IReadOnlyList<IBattlefieldPlayer> players);
        // xi.battlefield.getBattlefieldOptions(player, npc, trade): q's player, npc and (passTrade) trade
        int BattlefieldOptions(IBattlefieldContext q, bool passTrade);
        // xi.battlefield.rejectLevelSyncedParty(player, npc)
        bool RejectLevelSyncedParty(IBattlefieldContext q);
        // GetNPCByID(id), GetMobByID(id); null for nil
        IBattlefieldNpc NpcById(int id);
        IBattlefieldMob MobById(int id);
        // zones[zone].mob[key], zones[zone].npc[key]: the first id of a zone's IDs entry (ID.mob.DIABOLOS,
        // ID.npc.DARKNESS_NAMED_TILE_OFFSET); null for nil
        int? ZoneMobId(int zone, string key);
        int? ZoneNpcId(int zone, string key);
        // npcUtil.showCrate(crate)
        void ShowCrate(IBattlefieldNpc crate);
        // crate:addListener('ON_TRIGGER', 'TRIGGER_CRATE', utils.bind(self.handleOpenArmouryCrate, self))
        void AddCrateListener(IBattlefieldNpc crate, IBattlefieldContent self);
        // npcUtil.openCrate(crate, callback): the callback runs inside it, on a call of its own whose player and npc
        // are q's player and the crate (the Lua closure's upvalues)
        void OpenCrate(IBattlefieldContext q, IBattlefieldNpc crate, Func<IBattlefieldContext, bool> callback);
        // utils.selectFromLootGroups(player, lootTable): each selected entry's itemId and amount (null for nil)
        IReadOnlyList<(int ItemId, double? Amount)> SelectFromLootGroups(IBattlefieldPlayer player,
                                                                         BattlefieldLootTable lootTable);
        // npcUtil.giveCurrency(player, currency, amount, silent)
        void GiveCurrency(IBattlefieldPlayer player, string currency, double amount, bool silent);
        // xi.settings.map.BCNM_ENABLE_EXPERIMENTAL as Lua truth
        bool BcnmEnableExperimental { get; }
        // Battlefield:event(csid, args...)
        IBattlefieldAction Event(int csid, params int[] args);
        // Battlefield:progressOptionalCutscene(32003, { cs_option = 3, canSkip = true })
        IBattlefieldAction ExitCutscene();
        // xi.cs.questEvent(player, npc, key, csid, option): q's player and npc; whether a quest handler ran
        bool QuestEvent(IBattlefieldContext q, string key, int csid, int option);
        // Container:progressEvent(csid, args...): Event:new(...):progress()
        IBattlefieldAction ProgressEvent(int csid, params int[] args);
        // SpawnMob(id) (the one-argument form); null for nil
        IBattlefieldMob SpawnMob(int id);
        // DespawnMob(id) (the one-argument form: PAI->Despawn)
        void DespawnMob(int id);
        // GetPlayerByID(id); null for nil
        IBattlefieldPlayer GetPlayerByID(int id);
        // GetSystemTime(): a uint32 timestamp
        uint GetSystemTime();
        // math.randomInt(lower, upper): the script RNG, so a port draws what the Lua drew
        int RandomInt(int lower, int upper);
        // utils.shuffle(values): the values in the order the script RNG shuffles them to
        IReadOnlyList<int> Shuffle(IReadOnlyList<int> values);
        // xi.combat.behavior.isEntityBusy(mob)
        bool IsEntityBusy(IBattlefieldMob mob);
        // Battlefield.<method>(self, player, battlefield) as the Lua modules' overrides left it (module_utils.lua's
        // applyOverride; modules/era/lua/globals/testimony_single_use.lua overrides Battlefield.onBattlefieldEnter):
        // with an override, its chain runs on q's content, player and battlefield, and the chain's last super (the
        // framework's own method) is body, on a call of its own over the super call's arguments; true. With none,
        // nothing runs: false
        bool ModuleOverride(string method, IBattlefieldContext q, Action<IBattlefieldContext> body);
        // an entity kept past this call (quest rework C3 PR2), as a Lua table keeps the userdata: the engine's entity
        // of the facet; null for null
        BattlefieldKeptEntity Keep(IBattlefieldEntity entity);
        // a kept entity as a facet of this call; null for null
        IBattlefieldMob Entity(BattlefieldKeptEntity kept);
    }
}
