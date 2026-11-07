// XI.Quests -- the facets of the quest context (quest rework B3a): what a check reads and what a handler reads and does,
// grouped by what it is about. The bridge implements them over the engine; QuestContextFake over plain fields.
namespace XI.Quests.Authoring
{
    // read-only views: what a state's check may use
    public interface IQuestPlayerView
    {
        int FameLevel(int fameArea);
        bool HasCompletedQuest(QuestLog log, string constName);
        // player:getQuestStatus(log, xi.quest.id[area][constName]): another quest's status
        QuestStatus QuestStatus(QuestLog log, string constName);
        bool HasKeyItem(int keyItem);
        // player:seenKeyItem(ki)
        bool SeenKeyItem(int keyItem);
        int MainLevel { get; }
        // player:getSubLvl() (the Tutorial's step 11: a support job set)
        int SubLevel { get; }
        // player:getEquipID(slot)
        int EquipId(int slot);
        // player:getXPos() (The Sand Charm's Blandine: the docking box `xPos <= 16 or xPos >= 32`)
        double XPos { get; }
        // player:getYPos()
        double YPos { get; }
        // player:getZPos() (His Name is Valgeir's Felisa: `player:getZPos() > 38.5`)
        double ZPos { get; }
        // player:checkDistance(x, y, z) (B3b Other Areas group 7, Paradise, Salvation, and Maps' chest spots): the char's
        // distance to the point, in yalms (the binding's three-number form: x, y and z all count)
        double DistanceTo(double x, double y, double z);
        // player:getCharVar(name): vars the quest does not own (BathedInScent) or legacy NPC-script vars
        int CharVar(string name);
        // player:getMod(mod): an xi.mod id (Inside the Belly's xi.crafting.getTotalSkill: the FISH mod, 127)
        int Mod(int mod);
        // player:getFreeSlotsCount()
        int FreeSlotCount { get; }
        // player:getContainerSize(container) (quest rework P6): the container's size, xi.inv's id (INVENTORY 0, MOGSATCHEL
        // 5); 0 for an id past the containers. A fresh char's inventory is 30 and its satchel 0
        int ContainerSize(int container);
        // player:hasItem(item)
        bool HasItem(int item);
        // player:hasItem(item, location): the item in that container only (xi.inventoryLocation: INVENTORY 0)
        bool HasItem(int item, int location);
        // GetSystemTime()
        int SystemTime { get; }
        // JstMidnight(): the earth time of the next JST midnight
        int JstMidnight { get; }
        // player:getID()
        int Id { get; }
        // NextConquestTally()
        int NextConquestTally { get; }
        // getVanaMidnight(): the earth time of the next Vana'diel midnight
        int VanaMidnight { get; }
        // player:getLocalVar(name)
        int LocalVar(string name);
        // player:getHP() / player:getMP()
        int HP { get; }
        int MP { get; }
        // player:getMainJob()
        int MainJob { get; }
        // player:canEquipItem(item, checkLevel): the main job and race may wear it (and the level, when checkLevel)
        bool CanEquipItem(int item, bool checkLevel);
        // xi.equip.isArtifactArmor(item)
        bool IsArtifactArmor(int item);
        // xi.quest.getMustZone(player, log, xi.quest.id[area][constName]): another quest's mustZone
        bool QuestMustZone(QuestLog log, string constName);
        // player:hasCompletedMission(missionLog, mission): pass the generated XI.QuestContent.MissionLog and
        // XI.QuestContent.Mission.<Area> int constants (MissionLog.Cop, Mission.Cop.TheCallOfTheWyrmking); the enum
        // XI.Quests.MissionLog is a different type (the mission blob's), easily confused with the generated class
        bool HasCompletedMission(int missionLog, int mission);
        // (B3b Crystal War 8, Face of the Future) xi.maws.hasUnlockedMaw(player, zone): the PAST_MAW teleport bit of the
        // zone's Cavernous Maw (Batallia Downs and Batallia Downs [S] share bit 0)
        bool HasUnlockedMaw(int zone);
        // VanadielTOTD(): the time of day (xi.time: 5 is DUSK)
        int VanadielTotd { get; }
        // xi.besieged.getMercenaryRank(player) (B3b Aht Urhgan group 5, Waking the Colossus's and Divine Interference's
        // rewards menu): the index of the highest Wildcat badge held in xi.besieged.badges (PSC 1 .. Captain 11), 0 for
        // none
        int MercenaryRank { get; }
        // xi.abyssea.getTraverserCap(player) (B3b Abyssea group 1, The Truth Beckons): 3, plus one per Abyssite of
        // Avarice held
        int TraverserCap { get; }
        // player:setTraverserEpoch() (A Journey Begins)
        void SetTraverserEpoch();
        // player:getCurrency(currency) (B3b Abyssea group 2: the CRUOR_TOTAL messages of Lost Memories, An Eye for
        // Revenge and Fear of the Dark III): the char's points of that currency, by its lower-case name ('cruor')
        int Currency(string currency);
        // xi.abyssea.getHeldTraverserStones(player) (B3b Abyssea group 3, the maw quests' offers): how many of the
        // Traverser Stone 1-6 key items the char holds
        int HeldTraverserStones { get; }
        // xi.abyssea.getZoneKIReward(player) (B3b Abyssea group 3, the maw quests' updates and finishes): the key item
        // of zoneQuestReward for the count of maw quests completed (0 for the Lua's nil, all nine completed)
        int AbysseaZoneKIReward { get; }
        // #player:getParty(): the party's members, the char alone counting 1
        int PartySize { get; }
        // player:getAlliance(): each member's getMainLvl() and checkDistance(player), in the table's order
        System.Collections.Generic.IReadOnlyList<(int MainLevel, double Distance)> Alliance { get; }
        // VanadielTime(): the earth seconds since the Vana'diel epoch (the wall clock less VanaTime.EpochUnixSec), for
        // timers kept in vars
        int VanadielTime { get; }
        // player:getChocoboRaisingInfo() and xi.chocoboRaising.nameStrings(chocoState): the raised chocobo's full,
        // first and last name ('' each with no raised chocobo; the chocobo raising library stays Lua)
        (string Full, string First, string Last) ChocoboNameStrings { get; }
        // player:getAlliance(): each member's member:getCharVar(name), in the table's order (quest rework P7; the char
        // among them): Knocking on Forbidden Doors' `for _, member in ipairs(player:getAlliance()) do if
        // member:getCharVar('Quest[4][78]Prog') == 3 then ...`
        System.Collections.Generic.IReadOnlyList<int> AllianceCharVars(string name);
        // VanadielUniqueDay(): the Vana'diel day count, for day timers
        int VanadielUniqueDay { get; }
        // utils.vanadielClockTime(): the Vana'diel clock as hhmm, tonumber(VanadielHour() .. string.format('%02d',
        // VanadielMinute())) (04:30 is 430, 21:30 is 2130)
        int VanadielClockTime { get; }
        // VanadielRSELocation() / VanadielRSERace(): the week's race-specific equipment location (the Vana'diel week count
        // % 3: 0 Ordelle's Caves, 1 Gusgen Mines, 2 the Maze of Shakhrami) and race (the week count % 8 + 1, as xi.race)
        int VanadielRseLocation { get; }
        int VanadielRseRace { get; }
        // xi.equipment.vigilWeaponRequiredWsPoints(player) (scripts/globals/equipment.lua: 250): the weapon skill points a
        // Vigil weapon needs (Unlocking a Myth's trade). The bridge calls the Lua global itself, whatever it is at the call.
        // That is the base one (250) unless the era module modules/era/lua/globals/equipment/unlocking_a_myth.lua (by
        // NyzulFloorProgress) is listed and on: the server's Lua module loader (platform round P11,
        // ZoneHost.LuaModules) then overrides the global, and this member returns the override. The result is rounded
        // to a whole number (LuaState.ToIntegral, llround): a non-whole override (none returns one) would be compared
        // rounded, where the Lua compares the fraction
        int VigilWeaponRequiredWsPoints { get; }
        // zones[<the handler's zone>].npc[npcKey]: an IDs.lua npc id that is a number (eldiemeID.npc.BRAZIER); a key that
        // is missing or not a number (a table of ids) throws
        int NpcIdOf(string npcKey);
        // player:getPreviousZone(): in a zone-in, the Lua handler's prevZone argument (both are the char's previous zone)
        int PreviousZone { get; }
        // VanadielHour(): the Vana'diel hour, 0 to 23
        int VanadielHour { get; }
        // getVanadielMoonCycle() (scripts/utils/common.lua): the moon cycle index, an xi.moonCycle value (0 is NEW_MOON)
        int VanadielMoonCycle { get; }
        // player:getName()
        string Name { get; }
        // player:getZone():getLocalVar(name): a local var of the char's zone (the zone object Zone.lua's handlers get)
        int ZoneLocalVar(string name);
        // GetPlayerByName(name) / GetPlayerByID(id): a char of this process, as its getID() and getZoneID(); null when
        // the Lua returns nil (no such char online)
        (int Id, int ZoneId)? PlayerByName(string name);
        (int Id, int ZoneId)? PlayerById(int id);
        // player:needToZone(): the zoning flag (the char var [generic]mustZone), not a quest's own mustZone
        bool NeedToZone { get; }
        // player:getGender(): 1 male, 0 female (xi.item.DANCERS_TIGHTS_F - player:getGender() is the char's piece)
        int Gender { get; }
        // player:getPos(): its x, y, z and rot
        (double X, double Y, double Z, int Rot) Pos { get; }
        // player:findItem(item): true when it returns an item (Lua's `not player:findItem(...)` tests it)
        bool FindItem(int item);
        // player:getMannequinPose(item): the pose in the exdata of the char's mannequin of that id, 0 when none is held
        // (xi.mannequin.getMannequinPose)
        int MannequinPose(int item);
        // player:getCharSkillLevel(skill): the stored value, 10x the level (a Lua check divides it by 10)
        int CharSkillLevel(int skill);
        // player:getSkillLevel(skill): the working skill (capped for the job and level, with its modifiers), not the stored
        // 10x value CharSkillLevel reads
        int SkillLevel(int skill);
        // player:getGil()
        int Gil { get; }
        // player:hasSpell(spell)
        bool HasSpell(int spell);
        // NextJstDay(): the earth time of the next JST midnight (a timed var's expiry)
        int NextJstDay { get; }
        // player:hasJob(job): the job is unlocked (the unlocked jobs, not the main job; xi.job)
        bool HasJob(int job);
        // player:getNation(): the char's nation (xi.nation: 0 San d'Oria, 1 Bastok, 2 Windurst)
        int Nation { get; }
        // player:hasTitle(title): title is an XI.QuestContent.Title
        bool HasTitle(int title);
        // player:getPlaytime(): the char's play time in seconds (the saved time plus this session's)
        int Playtime { get; }
        // player:getRace(): the char's race (xi.race: 1 Hume male, 2 Hume female, 3 Elvaan male, 4 Elvaan female, 5 Tarutaru
        // male, 6 Tarutaru female, 7 Mithra, 8 Galka)
        int Race { get; }
        // player:getParty(): each member's getZoneID(), checkDistance(player), getNation(), getRace() and getMainJob(), in
        // the table's order (the char among them; alone, the char only). Count is #player:getParty()
        System.Collections.Generic.IReadOnlyList<(int ZoneId, double Distance, int Nation, int Race, int MainJob)> PartyMembers { get; }
        // player:getRank(nation): the char's rank in that nation (xi.nation: 0 San d'Oria, 1 Bastok, 2 Windurst)
        int Rank(int nation);
        // player:getCurrentMission(missionLog): pass the generated XI.QuestContent.MissionLog constant (MissionLog.Cop); the
        // answer compares with a XI.QuestContent.Mission.<Area> constant
        int CurrentMission(int missionLog);
        // xi.mission.getVar(player, missionLog, mission, name): a mission's var, player:getVar('Mission[<log>][<mission>]<name>')
        int MissionVar(int missionLog, int mission, string name);
        // player:getMissionStatus(missionLog): the status of the log's current mission (its first status index)
        int MissionStatus(int missionLog);
        // player:getMissionStatus(missionLog, pos): the 4-bit field at pos (0 to 7) of that status word (B3b San d'Oria
        // group 16: the CoP LOUVERANCE nibble, xi.mission.status.COP.LOUVERANCE)
        int MissionStatus(int missionLog, int pos);
        // player:getMoghouseFlag(): the Mog House exits unlocked (a bit per nation's exit)
        int MoghouseFlag { get; }
        // player:getWeather(): the weather the char is in (an xi.weather id; 0 is NONE)
        int Weather { get; }
        // player:getZone(true):getWeather(): the weather of the char's zone (an xi.weather id), not the char's own (Weather,
        // which a scholar's storm changes)
        int ZoneWeather { get; }
        // player:hasStatusEffect(effect): an xi.effect id
        bool HasStatusEffect(int effect);
        // zones[<the handler's zone>].mob[mobKey]: an IDs.lua mob id that is a number (jugnerID.mob.GIOLLEMITTE); a key that
        // is missing or not a number (a table of ids) throws
        int MobIdOf(string mobKey);
        // zones[<the handler's zone>].npc[npcKey]: an IDs.lua npc entry that is a table of ids (jugnerID.npc.SIGNPOST =
        // GetTableOfIDs('Signpost')), in its order: the Lua's [1] is [0]; a key that is missing or not a table throws
        System.Collections.Generic.IReadOnlyList<int> NpcIdsOf(string npcKey);
        // xi.automaton.getModelId(player): the model id of the char's automaton by its frame and head (Harlequin's when
        // the char has none)
        int AutomatonModelId { get; }
        // player:getLevelCap(): the char's level limit
        int LevelCap { get; }
        // player:getBattlefield(), then battlefield:getID(): the char's battlefield's id (xi.battlefield.id), null when the
        // char is in none (Tango with a Tracker's `local battlefield = player:getBattlefield() if not battlefield then return
        // end ... battlefield:getID()`)
        int? BattlefieldId { get; }
        // player:getMeritCount(): the merit points the char holds
        int MeritCount { get; }
        // player:getCampaignAllegiance(): the char's Campaign allegiance (0 none, 1 San d'Oria, 2 Bastok, 3 Windurst)
        int CampaignAllegiance { get; }
        // xi.moghouse.inMogHouseInHomeNation(player) (B3b Other Areas group 9, the Mog House moogles): the char is in a
        // Mog House (player:inMogHouse()) of its nation's zones (San d'Oria 230-232, Bastok 234-236, else Windurst
        // 238-241). The bridge calls the Lua global
        bool InMogHouseInHomeNation { get; }
        // player:inMogHouse() (B3b hidden quests group 4, Mog House Exposition): the char is in a Mog House
        bool InMogHouse { get; }
        // xi.monstrosity.hasUnlockedSpecies(player, species) (Monstrosity Bee): the char has the species unlocked. The
        // bridge calls the Lua global
        bool MonstrosityHasUnlockedSpecies(int species);
    }

    public interface IQuestProgressView
    {
        QuestStatus Status { get; }
        // the 'Prog' var (container quests)
        int Step { get; }
        int Var(string name);
        // quest:isVarBitsSet(player, name, bits...)
        bool IsVarBitsSet(string name, params int[] bits);
        bool MustZone { get; }
        // quest:getLocalVar(player, name): player:getLocalVar(varPrefix .. name)
        int LocalVar(string name);
    }

    public interface IQuestPlayer : IQuestPlayerView
    {
        void DelKeyItem(int keyItem);
        // player:addFame(fameArea, amount)
        void AddFame(int fameArea, int amount);
        // player:addItem(item, qty)
        bool AddItem(int item, int qty = 1);
        // local added = player:addItem({ id = item }) if added then added:setExData({ [field] = value }) end: true when
        // the item was added (It's Raining Mannequins!' mannequin with its exdata race)
        bool AddItemWithExData(int item, string field, int value);
        // player:setMannequinPose(item, race, pose): the exdata race and pose of the char's mannequin of that id, nothing
        // when none is held (xi.mannequin.setMannequinPose)
        void SetMannequinPose(int item, int race, int pose);
        // player:addTitle(title)
        void AddTitle(int title);
        // player:setTitle(title): the char's current title (added to the titles held, as setTitle does)
        void SetTitle(int title);
        // player:setCharVar(name, value[, expiry]): an expiry of 0 is none
        void SetCharVar(string name, int value, uint expiry = 0);
        // player:incrementCharVar(name, value): the char var moved by value (a var the quest does not own: [RSE]Gear)
        void IncrementCharVar(string name, int value);
        // player:setLocalVar(name, value)
        void SetLocalVar(string name, int value);
        // player:setHP(hp) / player:setMP(mp)
        void SetHP(int hp);
        void SetMP(int mp);
        // player:unlockJob(job)
        void UnlockJob(int job);
        // player:setPetName(petType, nameId)
        void SetPetName(int petType, int nameId);
        // player:unlockAttachment(item): its boolean result is unused by quests
        void UnlockAttachment(int item);
        // player:addExp(exp)
        void AddExp(int exp);
        // xi.quest.setMustZone(player, log, xi.quest.id[area][constName]): another quest's mustZone
        void SetQuestMustZone(QuestLog log, string constName);
        // npcUtil.giveKeyItem
        bool GiveKeyItem(int keyItem);
        // npcUtil.giveKeyItem(player, keyItem, zones[message.Zone].text[message.Name]): the key item with that text in place
        // of KEYITEM_OBTAINED (The Fighting Fourth's ITEM_RETURNED_TO_CHAR); an unknown text throws, as Text.Special does
        bool GiveKeyItem(int keyItem, ZoneText message);
        // npcUtil.giveItem(player, item)
        bool GiveItem(int item);
        // npcUtil.giveItem(player, item, { silent = silent, fromTrade = fromTrade })
        bool GiveItem(int item, bool silent, bool fromTrade);
        // npcUtil.giveItem(player, { { item, qty }, ... }): a stack of each (a reward of { { item, qty } } given before
        // quest:complete, which then takes no items)
        bool GiveItems(params (int Item, int Qty)[] items);
        // player:setMoghouseFlag(flag)
        void SetMoghouseFlag(int flag);
        // npcUtil.giveCurrency(player, currency, amount): 'gil' or 'bayld'
        bool GiveCurrency(string currency, int amount);
        // the same with a Lua number (an amount that is a product with a setting: 100 * GIL_RATE), pushed unrounded
        bool GiveCurrency(string currency, double amount);
        // xi.abyssea.dominionOnMobDeath(mob, player, opId) (B3b Abyssea groups 4-5, the Dominion Ops' onMobDeath): the
        // global counts the kill in the op's Prog var (up to its target count, FOV_DEFEATED_TARGET); mob is the
        // handler's dying mob (nil outside an OnMobDeath), passed as the Lua passes it
        void DominionOnMobDeath(int opId);
        // player:addCurrency(currency, amount) (B3b Abyssea group 2: 'cruor'): the points added, with no message and no
        // rate (npcUtil.giveCurrency, with its message and the gil / bayld rates, is GiveCurrency)
        void AddCurrency(string currency, int amount);
        // player:delCurrency(currency, amount) (B3b Adoulin group 4: the Matre Bell's 150000 bayld): the points taken,
        // with no message
        void DelCurrency(string currency, int amount);
        // npcUtil.popFromQM(player, npc, zones[zone].mob[mobName] + offset, { claim = claim, hide = hide }): claim defaults to
        // popFromQM's own (true); offset, for one mob of an offset key (DARK_PUPPET_OFFSET + 1), to none
        bool PopFromQm(string mobName, int hide, bool claim = true, int offset = 0);
        // npcUtil.popFromQM(player, npc, { zones[zone].mob[mobName], ... }, { hide = hide }): the mobs in the order given
        bool PopFromQm(string[] mobNames, int hide);
        // npcUtil.popFromQM(player, npc, { zones[zone].mob[mobName], ... + 1, ..., + count - 1 }, { hide = hide }): the
        // mob and the count - 1 ids after it, for mobs with no IDs.lua key of their own (YUM_KIMIL and its two guardians)
        bool PopFromQmSeries(string mobName, int count, int hide);
        // math.randomInt(lower, upper): the script RNG, inclusive at both ends
        int RandomInt(int lower, int upper);
        // player:delQuest(log, xi.quest.id[area][constName])
        void DelQuest(QuestLog log, string constName);
        // player:updateEvent(args...): the open event's update (an OnEventUpdate)
        void UpdateEvent(params int[] args);
        // xi.quest.setVar(player, log, xi.quest.id[area][constName], name, value): another quest's var
        void SetQuestVar(QuestLog log, string constName, string name, int value);
        // GetMobByID(zones[zone].mob[mobName] + offset):isSpawned() / :isAlive(), in the handler's zone (the Lua scripts'
        // zones[xi.zone.<Zone>].mob.<NAME>; an offset key plus n: TALEKEEPERS_GIFT_OFFSET + 1)
        bool MobIsSpawned(string mobName, int offset = 0);
        bool MobIsAlive(string mobName, int offset = 0);
        // GetMobByID(zones[zone].mob[mobName] + offset):isDead(): a mob never spawned, or despawned, counts as dead too
        bool MobIsDead(string mobName, int offset = 0);
        // SpawnMob(zones[zone].mob[mobName] + offset), then :updateClaim(player) when claim, else :updateEnmity(player)
        void SpawnMob(string mobName, int offset, bool claim);
        // DespawnMob(zones[zone].mob[mobName] + offset), in the handler's zone, with no method called on the mob
        void DespawnMob(string mobName, int offset = 0);
        // player:getZone():setLocalVar(name, value)
        void SetZoneLocalVar(string name, int value);
        // player:needToZone(value): true sets the zoning flag, cleared when the char enters another zone
        void SetNeedToZone(bool value);
        // GetNPCByID(zones[zone].npc[npcName]):setLocalVar(name, value), in the handler's zone: an NPC the Lua names by its
        // IDs.lua key rather than the handler's npc (A Flash in the Pan's AQUILLINA)
        void SetNpcLocalVar(string npcName, string name, int value);
        // player:addLearnedWeaponskill(wsUnlock): an xi.wsUnlock id
        void AddLearnedWeaponskill(int wsUnlock);
        // player:addSpell(spell), with no options table
        void AddSpell(int spell);
        // player:addSpell(spell, { silentLog = silentLog }): silentLog true learns it without the "learns a new spell!" line
        void AddSpell(int spell, bool silentLog);
        // xi.teleport.to(player, destination): an xi.teleport.id; the char's position set, the zone change pending
        void TeleportTo(int destination);
        // player:addKeyItem(keyItem): the key item, with no message (npcUtil.giveKeyItem, with its message, is GiveKeyItem)
        void AddKeyItem(int keyItem);
        // player:startEvent(csid, args...): the handler starts the event itself; a Lua handler that does so returns nil, so
        // the C# handler returns null and the lookup goes on to its other actions (a QuestReply would end the lookup)
        void StartEvent(int csid, params int[] args);
        // player:startCutscene(csid, args...) (Embers of His Past's 17 finish starts 18): a cutscene, started as
        // StartEvent starts an event (the engine's startCutscene binding: an event of type Cutscene)
        void StartCutscene(int csid, params int[] args);
        // player:startEventString(csid, s1, s2, s3, s4, args...): a string event (the four strings, then the numbers);
        // the handler starts it itself, as with StartEvent
        void StartEventString(int csid, string s1, string s2, string s3, string s4, params int[] args);
        // xi.chocoboRaising.setWhistleProgress(player, progress): the whistle quest's progress (the chocobo raising
        // library; DONE sets the user flag WHISTLE_QUEST_DONE and clears the HQuest[ChocoboWhistle]Prog var)
        void SetChocoboWhistleProgress(int progress);
        // xi.chocoboRaising.whistle.giveQuestWhistle(player): npcUtil.giveItem of the Chocobo Whistle (true), else the
        // pending var [ChocoboRaising]WhistlePending 1 (false)
        bool GiveChocoboQuestWhistle();
        // xi.wotg.helpers.checkMemoryFragments(player): with all four memory fragment key items, the
        // REPORT_TO_CAIT_SITH message (player:messageName), Her Memories completed and Forget Me Not added (the Lua
        // global is called)
        void CheckMemoryFragments();
        // xi.maps.onEventUpdate(player, csid, option, npc): the map shop's update, npc the handler's npc
        void MapsOnEventUpdate(int csid, int option);
        // player:addStatusEffect(effect, { power = power, subPower = subPower, origin = player, flag = flag }): an xi.effect
        // id and xi.effectFlag bits, the char its own origin
        void AddStatusEffect(int effect, int power, int subPower, int flag);
        // player:addStatusEffect(effect, { power = power, duration = durationSeconds, origin = player }): an xi.effect
        // id, the duration in seconds, the char its own origin
        void AddStatusEffectForDuration(int effect, int power, int durationSeconds);
        // player:addStatusEffect(effect, { duration = durationSeconds, origin = player }): the same with no power field
        // (A Pioneer's Best (Imaginary) Friend's Ionis)
        void AddStatusEffectForDuration(int effect, int durationSeconds);
        // player:delStatusEffectsByFlag(flag, silent): every effect with any of the xi.effectFlag bits removed, silent
        // true for no wear-off message
        void DelStatusEffectsByFlag(int flag, bool silent);
        // player:delStatusEffect(effect)
        void DelStatusEffect(int effect);
        // npcUtil.popFromQM(player, npc, { zones[zone].mob[mobName], ... + 1, ..., + count - 1 }, { claim = claim, look =
        // look, hide = hide }): the mob and the count - 1 ids after it (Eco-Warrior's PUDDING_OFFSET, + 1); look turns
        // each popped mob to face the char
        bool PopFromQmSeries(string mobName, int count, int hide, bool claim, bool look);
        // player:setPos(x, y, z, rot, zone): the char set down there; given a zone (its own one too), it zones at the next
        // tick
        void SetPos(double x, double y, double z, int rot, int zone);
        // player:setLevelCap(cap)
        void SetLevelCap(int cap);
        // player:setMerits(merits): the merit points held (the engine keeps them at most the bank)
        void SetMerits(int merits);
        // player:setCampaignAllegiance(nation): the char's Campaign allegiance (0 none, 1 San d'Oria, 2 Bastok, 3 Windurst)
        void SetCampaignAllegiance(int nation);
        // player:addQuest(log, xi.quest.id[area][constName]): another quest added (accepted)
        void AddQuest(QuestLog log, string constName);
        // player:completeQuest(log, xi.quest.id[area][constName]): the complete bit set and the quest out of the current
        // list; no reward and no completion message (unlike quest:complete / npcUtil.completeQuest); fires the RoE
        // quest-complete event, as player:completeQuest does (ZoneHost.Progress.cs RoeEventQuestComplete)
        void CompleteQuest(QuestLog log, string constName);
        // player:changeContainerSize(container, delta) (quest rework P6): the size moved by delta; the engine holds it to
        // 0..80 and never below the items in it (CItemContainer::AddBuff), sends the ITEM_MAX and saves char_storage
        void ChangeContainerSize(int container, int delta);
        // SpawnMob(zones[zone].mob[mobName]) alone: no claim and no enmity (a zone-in spawn, The Crimson Trial)
        void SpawnMob(string mobName);
        // npcUtil.popFromQM(player, npc, zones[zone].mob[mobName], { claim = claim, hide = hide, radius = radius }): the mob
        // spawns at a random point within radius yalms of the npc (two math.randomFloat draws)
        bool PopFromQm(string mobName, bool claim, int hide, int radius);
        // player:addGil(amount) / player:delGil(amount) (the bool delGil answers is not read)
        void AddGil(int amount);
        void DelGil(int amount);
        // player:delGil(amount) as a condition: true when the gil was taken, false (and nothing taken) when the char holds
        // less (the engine logs `lua: delGil : Not enough Gil in currency slot`)
        bool DelGilChecked(int amount);
        // SetServerVariable(name, value): a server variable (Test My Mettle's moveJar: Davoi_Jar_Move_Time)
        void SetServerVar(string name, int value);
        // player:addMission(missionLog, mission) / player:delMission(missionLog, mission): the log's current mission set /
        // cleared (delMission also clears the mission's completed bit)
        void AddMission(int missionLog, int mission);
        void DelMission(int missionLog, int mission);
        // player:setMissionStatus(missionLog, status): the status of the log's current mission
        void SetMissionStatus(int missionLog, int status);
        // xi.mission.setVar(player, missionLog, mission, name, value): a mission's var, Mission[<log>][<mission>]<name>
        void SetMissionVar(int missionLog, int mission, string name, int value);
        // xi.mission.setMustZone(player, missionLog, mission): the mission's mustZone var set
        // (Mission[<log>][<mission>]mustZone, the zone id; the mission twin of SetQuestMustZone; Flavors of Our Lives'
        // Budding Prospects)
        void SetMissionMustZone(int missionLog, int mission);
        // npcUtil.completeMission(player, missionLog, mission, { nextMission = { nextMissionLog, nextMission } }): the mission
        // completed (player:completeMission), then the next one added (player:addMission); answers npcUtil's result (true:
        // no item in the params, so nothing can fail)
        bool CompleteMission(int missionLog, int mission, int nextMissionLog, int nextMission);
        // player:setRank(rank): the char's rank in its own nation (quest rework C2; npcUtil.completeMission's rank)
        void SetRank(int rank);
        // player:setRankPoints(points) / player:getRankPoints() (quest rework C2; npcUtil.completeMission's rankPoints)
        void SetRankPoints(int points);
        int RankPoints { get; }
        // player:addRankPoints(points) (quest rework C2; San d'Oria 1-3's repeat)
        void AddRankPoints(int points);
        // player:setPos(x, y, z): the char set down there in its own zone, its rotation kept (no rot, no zone given; the 5-argument SetPos zones it)
        void SetPos(double x, double y, double z);
        // player:setPos(x, y, z, rot): the char set down there in its own zone, the rotation given (no zone: the
        // 5-argument SetPos zones it; Vanishing Act's 44)
        void SetPos(double x, double y, double z, int rot);
        // player:showText(npc, zones[text.Zone].text[text.Name], args...): the handler's npc speaks the text to this char
        // alone (Npc.ShowText is npc:showText, sent to the chars in range); an NPC script's call in an NPC handler
        void ShowText(ZoneText text, params int[] args);
        // (B3b Outlands 1, Forge Your Destiny's qms) local mob = SpawnMob(zones[zone].mob[mobName]); false when nil; else
        // mob:updateClaim(player), mob:setLocalVar('QMID', npcId) and mob:addListener('DESPAWN', 'DESPAWN_' .. id, f), f
        // removing itself and setting GetNPCByID(mob:getLocalVar('QMID')):setLocalVar(npcVar, GetSystemTime() + seconds)
        // when the mob despawns (the qm's cooldown after the NM is gone); true
        bool SpawnMobWithNpcCooldown(string mobName, int npcId, string npcVar, int seconds);
        // (B3b Crystal War 7, The Price of Valor's Underbrush) player:getZone():queryEntitiesByName(name)[1]:getID():
        // the id of the first entity of that name in the char's zone (a mob with no IDs.lua key); an unknown name
        // raises, as the Lua indexing nil does
        int MobIdByName(string name);
        // GetMobByID(id):isSpawned(): the mob of that id (one MobIdByName returned)
        bool MobIsSpawnedById(int id);
        // SpawnMob(id), then :updateClaim(player) when claim, else :updateEnmity(player)
        void SpawnMobById(int id, bool claim);
        // player:setMonstrosityData({ monstrosityId = monstrosityId, species = species }) (B3b Other Areas group 9,
        // Monstrosity's trade finish): the char's Monstrosity entry and species saved
        void SetMonstrosityData(int monstrosityId, int species);
        // xi.monstrosity.updateIntroEvent(player) (Monstrosity's Feretory 2 update): the open event updated with the
        // char's race, face and its Monstrosity entry's starter. The bridge calls the Lua global
        void MonstrosityUpdateIntroEvent();
        // player:sendMenu(menuType) (the Tutorial's Auction Counters: xi.menuType.AUCTION, 3): the menu's packet
        void SendMenu(int menuType);
        // xi.survivalGuide.onTrigger(player) (the Tutorial's survival guide arms): the guide's own event. The bridge
        // calls the Lua global
        void SurvivalGuideOnTrigger();
        // xi.monstrosity.suibhneGreet(player, npc) (Monstrosity Bee): Suibhne's greeting (his showText and a random
        // entry),
        // npc the handler's. The bridge calls the Lua global
        void MonstrositySuibhneGreet();
        // xi.monstrosity.unlockSpecies(player, species) (Monstrosity Bee). The bridge calls the Lua global
        void MonstrosityUnlockSpecies(int species);
        // player:setHomePoint() (New Character Cutscenes): the char's home point is its current position
        void SetHomePoint();
        // player:startEvent(csid, { flags = flags, isHidden = isHidden }) (New Character Cutscenes' Bastok Markets)
        void StartEventFlags(int csid, uint flags, bool isHidden);
        // player:startEvent(csid, { isHidden = true }) (quest rework C2, CoP 1-1's 36 to 39): a hidden event, its table
        // with no other field
        void StartEventHidden(int csid);
        // player:enableEntities({ id, ... }) (quest rework C2, CoP 1-1's Tales' Beginning NPCs): the client may show
        // these entities (a 0x077). No ids is the empty table, whose 0x077 hides the ones shown before
        void EnableEntities(params int[] ids);
        // player:sendPartialMissionLog(missionLog, completed) (quest rework C2, CoP 1-1's 22): the log's page sent with
        // its special text (the 0x056)
        void SendPartialMissionLog(int missionLog, bool completed);
        // player:timer(ms, function(playerArg) ... end) (quest rework P10): the quest's callback run ms from now on the
        // char's own action queue
        // (CLuaBaseEntity::timer: fired on the char's first tick strictly past the deadline, gone with the char on a
        // zone change or a logout),
        // with a context of that char (no Npc). ms below 0 throws (C++ takes an int and would fire it at once); a
        // callback of another quest throws
        void Timer(int ms, QuestCallback callback);
        // player:addListener(evt, identifier, function(playerArg) ... end) (quest rework P10): the callback run on each
        // of the char's evt events, with a context of that char, until removed or the char's zone change or logout; a
        // second listener of the identifier replaces the first (CAIEventHandler). evt is "TICK" only
        // (the one event the quests use; any other throws until a quest needs one); the TICK's elapsed milliseconds are
        // not passed on
        void AddListener(string evt, string identifier, QuestCallback callback);
        // player:removeListener(identifier)
        void RemoveListener(string identifier);
        // (B3b Deferred 1) GetNPCByID(id): the NPC of that id, null when nil (an id of no NPC). The NPC the Lua names
        // by id rather than the handler's npc: Scattered into Shadow's GetNPCByID(castleOzID.npc.TEBHI) in a zone-in
        // and a TICK listener, Mimeo Stone's GetNPCByID(npc:getID() + 1)
        IQuestNpc NpcById(int id);
    }

    public interface IQuestProgress : IQuestProgressView
    {
        // quest:setVar(player, 'Prog', step)
        void SetStep(int step);
        void SetVar(string name, int value, uint expiry = 0);
        // quest:setVarBit(player, name, bit)
        void SetVarBit(string name, int bit);
        // quest:unsetVarBit(player, name, bit)
        void UnsetVarBit(string name, int bit);
        // quest:incrementVar(player, name, value): player:incrementCharVar(varPrefix .. name, value)
        void IncrementVar(string name, int value);
        void SetMustZone();
        void Begin();
        // quest:complete(player): npcUtil.completeQuest with the reward
        bool Complete();
        // quest:setLocalVar(player, name, value)
        void SetLocalVar(string name, int value);
        // player:delCurrentQuest(quest.areaId, quest.questId): the quest off the current list, its completed bit kept
        // (Test My Mettle's 123)
        void DelCurrent();
        // quest:cleanup(player): every Quest[log][id] var cleared, outside a completion (X Marks the Spot's completed
        // section, leaving Tavnazian Safehold at Post 2)
        void Cleanup();
    }

    // an OnMobDeath's optParams (quest rework P7), read-only: the fields luautils::OnMobDeath (ZoneHost.MobScripts
    // OnMobDeath) sets before it runs the handlers for each alliance member. Each is read from the table when asked, as
    // Lua's optParams.<field> reads it; a field the table lacks reads false / 0
    public interface IQuestMobDeath
    {
        // optParams.isKiller: this char is the killer (each other alliance member in the killer's zone runs the handler
        // with false); Lua's truthiness (`if optParams.isKiller then`)
        bool IsKiller { get; }
        // optParams.noKiller: the mob died with no killer. The lookup runs no quest handler then (it has no player), so a
        // handler reads false
        bool NoKiller { get; }
        // optParams.isWeaponSkillKill: the last hit was a weapon skill (WeaponskillUsed above 0)
        bool IsWeaponSkillKill { get; }
        // optParams.weaponskillUsed: the weapon skill's id (the mob's weaponskillHit local var >> 24: weaponskills.lua
        // writes wsID << 24 plus the damage), 0 for none
        int WeaponskillUsed { get; }
        // optParams.weaponskillDamage: that weapon skill's damage (weaponskillHit & 0xFFFFFF)
        int WeaponskillDamage { get; }
    }

    // the NPC the handler is about (npc:...); null outside NPC handlers
    public interface IQuestNpc
    {
        // npc:getID()
        int Id { get; }
        // npc:getName(): the entity's script name, what the Npc.<Zone>.<Ident> constants hold (Fire in the Hole's
        // finish 8: `npc and npc:getName() == 'Solitary_Ant'`)
        string Name { get; }
        // npc:getLocalVar(name)
        int LocalVar(string name);
        // npc:setLocalVar(name, value)
        void SetLocalVar(string name, int value);
        // player:checkDistance(npc): the player's distance to the NPC, in yalms
        double PlayerDistance { get; }
        // npc:getAnimation() / npc:setAnimation(animation): an xi.animation value (8 OPEN_DOOR, 9 CLOSE_DOOR)
        int Animation { get; }
        void SetAnimation(int animation);
        // npc:showText(npc, zones[text.Zone].text[text.Name], args...): the NPC speaks the text to the chars in range
        void ShowText(ZoneText text, params int[] args);
        // npc:hideNPC(seconds)
        void Hide(int seconds);
        // npc:getXPos() / getYPos() / getZPos() (Test My Mettle's moveJar: npcUtil.pickNewPosition finds the jar's spot)
        double XPos { get; }
        double YPos { get; }
        double ZPos { get; }
        // npcUtil.queueMove(npc, { x, y, z }): npc:queue(3000, ...) then setPos(x, y, z, 0) (no rot given: rotation 0)
        void QueueMove(double x, double y, double z);
        // npc:setPos(x, y, z): the NPC set down there at once, its rotation kept (no rot given; The Missing Piece's
        // Quicksand Caves qm5, moved by npcUtil.pickNewPosition). Not QueueMove: that queues the move 3 s later at rotation 0
        void SetPos(double x, double y, double z);
        // npc:timer(ms, function(npcArg) ... end) (quest rework P10): the quest's callback run ms from now on the
        // entity's own action queue (CLuaBaseEntity::timer), with a context of the entity as its Npc and no Player. ms
        // below 0 throws; a callback of another quest throws
        void Timer(int ms, QuestCallback callback);
        // the same, with WithPlayer.Yes: the context's Player is also the char that armed it (the handler's), found by
        // id in the entity's zone when it fires; when that char is gone the callback does not run (logged).
        // WithPlayer.No is the two-argument form
        void Timer(int ms, QuestCallback callback, WithPlayer withPlayer);
        // (B3b Deferred 1, Scattered into Shadow's Tebhi) npc:getStatus() / npc:setStatus(status): an xi.status value
        // (0 NORMAL, 6 CUTSCENE_ONLY)
        int Status { get; }
        void SetStatus(int status);
        // npc:entityAnimationPacket(animation): an xi.animationString value ('kesu' STATUS_DISAPPEAR, 'deru'
        // STATUS_VISIBLE), the NPC its own target, sent to the chars near it
        void EntityAnimationPacket(string animation);
        // npc:clearPath(pause): pause true stops a pathing NPC where it stands (its pauseNPCPathing local var 1)
        void ClearPath(bool pause);
        // npc:continuePath(): a paused NPC walks its path again
        void ContinuePath();
        // npc:facePlayer(player, nonGlobal): the NPC turned to the handler's char; nonGlobal true turns it on that
        // char's client only, false for every char (Tebhi's npc:facePlayer(player, false))
        void FacePlayer(bool nonGlobal);
        // (Mimeo Stone's Luminant) require('scripts/zones/Attohwa_Chasm/globals').handleLuminantUsed(npc): the zone
        // global with this NPC (the Luminant goes CUTSCENE_ONLY; a timer of its own wakes a used one later)
        void AttohwaChasmHandleLuminantUsed();
    }

    public interface IQuestSettings
    {
        // xi.settings.main[name]: a number, or a boolean as 1 / 0; 0 when unset. A fraction is rounded half away from zero
        // (the engine's llround: GIL_RATE 1.5 reads 2, 1.4 reads 1); a quest that needs the fraction reads MainNumber
        int Main(string name);
        // xi.settings.main[name] as the Lua number it is (quest rework P5): a fraction kept (GIL_RATE 1.5: 400 * rate is
        // 600), a boolean as 1 / 0, 0 when unset or not a number. By default Main's value (a settings source that holds
        // whole numbers only)
        double MainNumber(string name) => Main(name);
        // xi.settings.map[name] (quest rework P6), read as Main reads xi.settings.main: a number (an implementer may round a
        // fraction half away from zero, as the engine's llround does; ModuleSettings.Map and Main read a fraction as 0), a
        // boolean as 1 / 0, 0 when unset
        int Map(string name);
        // xi.settings.map[name] == true (quest rework P6): the boolean true only, as the Lua's `== true` reads it (a number,
        // 1 included, is not; FISHING_ENABLE)
        bool MapFlag(string name);

        // xi.settings.main[name] with its Lua type kept (quest rework P9): nil, a boolean, a number or a string. Main and
        // MainNumber read a boolean as 1 / 0, which Lua never does: `true == 1` is false there. By default a number,
        // MainNumber's value (a settings source that holds numbers only: a module's offline branches)
        QuestSetting MainValue(string name) => QuestSetting.Of(MainNumber(name));
        // the Lua `xi.settings.main[name] == n` (quest rework P9): true only for a number equal to n. A boolean, a string
        // and nil never equal a number (ENABLE_TOAU = true is not 1). `~= n` is !MainEquals(name, n). Every setting compared
        // with a number by == / ~= in the Lua is ported with it, never as Main(name) == n (rounds; reads true as 1) or
        // MainNumber(name) == n (reads true as 1)
        bool MainEquals(string name, double n) => MainValue(name).LuaEquals(n);
        // the Lua `if xi.settings.main[name] then` (quest rework P9): Lua's truthiness, false only for nil and false (0 is
        // true in Lua: OLDSCHOOL_G1 = 0 is on)
        bool MainTruthy(string name) => MainValue(name).Truthy;
        // the Lua `xi.settings.main[name] == true` (quest rework P9): the boolean true only (xi.pre's RESTRICT_CONTENT)
        bool MainFlag(string name) => MainValue(name).IsTrue;
    }

    // the Lua type of a setting's value (quest rework P9); Other is a table, a function or anything else
    public enum QuestSettingType { Nil, Boolean, Number, String, Other }

    // a setting as Lua holds it (quest rework P9): its type and its value, compared as Lua compares
    public readonly struct QuestSetting : System.IEquatable<QuestSetting>
    {
        QuestSetting(QuestSettingType type, bool boolean, double number, string text)
        {
            Type = type;
            Boolean = boolean;
            Number = number;
            Text = text;
        }

        public QuestSettingType Type { get; }
        // the value of a Boolean (false otherwise)
        public bool Boolean { get; }
        // the value of a Number (0 otherwise)
        public double Number { get; }
        // the value of a String (null otherwise)
        public string Text { get; }

        public static QuestSetting Nil => default;
        public static QuestSetting Of(bool value) => new QuestSetting(QuestSettingType.Boolean, value, 0, null);
        public static QuestSetting Of(double value) => new QuestSetting(QuestSettingType.Number, false, value, null);
        public static QuestSetting Of(string value) =>
            value == null ? Nil : new QuestSetting(QuestSettingType.String, false, 0, value);
        public static QuestSetting OtherValue => new QuestSetting(QuestSettingType.Other, false, 0, null);

        // Lua's `value == n`: values of two types are never equal (lj_obj_equal: a number only equals a number)
        public bool LuaEquals(double n) => Type == QuestSettingType.Number && Number == n;
        // Lua's truthiness: everything but nil and false
        public bool Truthy => !(Type == QuestSettingType.Nil || (Type == QuestSettingType.Boolean && !Boolean));
        // Lua's `value == true`
        public bool IsTrue => Type == QuestSettingType.Boolean && Boolean;

        public bool Equals(QuestSetting other) =>
            Type == other.Type && Boolean == other.Boolean && Number.Equals(other.Number) && Text == other.Text;
        public override bool Equals(object obj) => obj is QuestSetting s && Equals(s);
        public override int GetHashCode() => System.HashCode.Combine(Type, Boolean, Number, Text);

        // nil, true / false, a number invariantly (1.25), a string quoted ("1")
        public override string ToString() => Type switch
        {
            QuestSettingType.Nil => "nil",
            QuestSettingType.Boolean => Boolean ? "true" : "false",
            QuestSettingType.Number => Number.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            QuestSettingType.String => "\"" + Text + "\"",
            _ => "other",
        };
    }

    public interface IQuestTrade
    {
        // npcUtil.tradeMatches(trade, items)
        bool Matches(params (int Item, int Qty)[] items);
        // trade:hasItemQty(item, qty)
        bool HasItemQty(int item, int qty);
        // trade:getItemCount()
        int ItemCount { get; }
        // trade:getSlotCount(): the trade's slots that hold an item; trade:getItemId(slot): the item in the slot (0-based)
        int SlotCount { get; }
        int ItemId(int slot);
        // trade:getItem(slot):getAHCat(): the auction house category of the item in the slot (0-based; quest rework B3b
        // Adoulin group 5, Westerly Breeze's food / drink test). An empty slot raises, as the Lua's call on nil does
        int ItemAhCategory(int slot);
        // trade:getItemQty(item): how many of the item the trade holds
        int ItemQty(int item);
        // trade:getGil(): the gil in the trade
        int Gil { get; }
        // npcUtil.tradeHasExactly(trade, { items }): npcUtil.tradeHas(trade, items, true), the items and nothing else
        // (each listed once is one of it); it confirms them, as tradeHas does
        bool HasExactly(params int[] items);
        // npcUtil.tradeHas(trade, { items })
        bool Has(params int[] items);
        // npcUtil.tradeHas(trade, { item, ..., { 'gil', gil } })
        bool HasWithGil(int gil, params int[] items);
        // npcUtil.tradeHasExactly(trade, { item, ..., { 'gil', gil } }): the items and exactly that gil, nothing else
        bool HasExactlyWithGil(int gil, params int[] items);
        // npcUtil.tradeHasExactly(trade, { { item, qty }, ... }): the items and nothing else; confirms them
        bool HasExactly(params (int Item, int Qty)[] items);
        // npcUtil.tradeHas(trade, { { item, qty }, ... }): at least these; confirms only them
        bool HasQty(params (int Item, int Qty)[] items);
        // player:tradeComplete()
        void Complete();
        // player:tradeComplete() as a condition (B3b Other Areas group 9, Monstrosity's `not player:tradeComplete()`):
        // true when the trade's items were all taken, false when the char holds no trade to complete or kept some of
        // them
        bool CompleteChecked();
        // player:confirmTrade()
        void Confirm();
        // trade:confirmItem(item, qty): qty of the item in the trade confirmed (taken by the next confirmTrade)
        void ConfirmItem(int item, int qty);
        // xi.treasure.onTrade(player, npc, trade, bypassType, bypassReward): the chest's trade, a quest's key item or
        // item as its reward (bypass type 2 / 1); returns what it returns (0, or the reward)
        int TreasureTrade(int bypassType, int bypassReward);
        // trade:getItem(slot):getWeaponskillPoints(): the weapon skill points of the item in the trade's slot
        int ItemWeaponskillPoints(int slot);
        // xi.helm.onTrade(player, npc, trade, helmType, nil): the HELM point's trade with no csid (Adoulin's emote-only
        // gathering), the Lua global called as it is (it raises where the global does: Flavors of Our Lives' zone 260)
        void HelmOnTrade(int helmType);
    }

    public interface IQuestText
    {
        // player:messageSpecial(zones[text.Zone].text[text.Name], ...)
        void Special(ZoneText text, params int[] args);
        // player:messageName(zones[text.Zone].text[text.Name], nil, ...): no entity, the params from the third
        // argument on
        void MessageName(ZoneText text, params int[] args);
        // player:messageText(npc, zones[text.Zone].text[text.Name], showName, mode): the handler's npc (nil when it has
        // none), showName a boolean, mode a number (6 is the system message type)
        void MessageText(ZoneText text, bool showName, int mode);
        // player:messageText(player, zones[text.Zone].text[text.Name]) (New Character Cutscenes): the char is its own
        // speaker, showName and mode left nil
        void MessageTextFromPlayer(ZoneText text);
        // player:messageSpecial(textId, ...): an id read with Id and offset (cavesID.text.DRY_WIND + 1)
        void Special(int textId, params int[] args);
        // zones[text.Zone].text[text.Name]: the text's id, for a Lua text id plus an offset (HELBORT_ORDERS + 3)
        int Id(ZoneText text);
    }
}
