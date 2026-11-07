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
        // the coverage hook: a label of the battlefield reached (BattlefieldScript.CoverageLabels)
        void Reached(string label);
    }

    // the player bindings the framework calls beyond the quest ones
    public interface IBattlefieldPlayer : IQuestPlayer
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
    }

    // a mob object
    public interface IBattlefieldMob
    {
        int Id { get; }
        int MainLevel { get; }
        void SetMobLevel(int level);
        // mob:pathThrough(points, flags)
        void PathThrough(double[] points, int flags);
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
        void InsertEntity(int targId, bool enemy, bool inBattlefield);
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
    }
}
