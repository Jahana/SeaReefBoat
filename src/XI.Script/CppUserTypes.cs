using System.Collections.Generic;

namespace XI.Script
{
    // The methods each C++ usertype registers (lua_*.cpp's SOL_REGISTER lines, src/map/lua): a key a script indexes
    // that is none of them is nil, as sol's index_fail gives it; one of them the host has not bound is the
    // "binding not ported" stub. Generated from the C++ sources.
    public static class CppUserTypes
    {
        public static readonly IReadOnlyDictionary<string, HashSet<string>> Methods = new Dictionary<string, HashSet<string>>
        {
            // lua_ability.cpp
            ["CAbility"] = new HashSet<string>
            {
                "getID", "getMsg", "getRecast", "getRecastID", "getRange", "getRadius", "getAOE", "getName",
                "getAnimation", "getAddType", "setMsg", "setAnimation", "setRecast", "getCE", "setCE", "getVE", "setVE",
                "setRange", "setPostActionCleanupEffect",
            },
            // lua_action.cpp
            ["CAction"] = new HashSet<string>
            {
                "ID", "getPrimaryTargetID", "getRecast", "setRecast", "actionID", "getParam", "param", "messageID",
                "getMsg", "getAnimation", "setAnimation", "getCategory", "setCategory", "resolution", "info",
                "hitDistortion", "knockback", "recordDamage", "modifier", "additionalEffect", "addEffectParam",
                "addEffectMessage", "addAdditionalTarget",
            },
            // lua_attack.cpp
            ["CAttack"] = new HashSet<string>
            {
                "isCritical", "setCritical",
            },
            // lua_base_entity.cpp
            ["CBaseEntity"] = new HashSet<string>
            {
                "showText", "messageText", "printToPlayer", "printToArea", "messageBasic", "messageName",
                "messagePublic", "messageSpecial", "messageSystem", "messageCombat", "messageStandard", "customMenu",
                "getCharVar", "getCharVarsWithPrefix", "getCharVarsWithSuffix", "setCharVar", "setCharVarExpiration",
                "getVar", "setVar", "incrementCharVar", "setVolatileCharVar", "getLocalVars", "getLocalVar",
                "setLocalVar", "clearLocalVarsWithPrefix", "resetLocalVars", "getData", "resetData",
                "clearVarsWithPrefix", "getLastOnline", "injectPacket", "injectActionPacket", "entityVisualPacket",
                "entityAnimationPacket", "sendDebugPacket", "sendLinkshellConcierge", "sendChocoboRace", "startEvent",
                "startCutscene", "startOptionalCutscene", "startEventString", "updateEvent", "updateEventString",
                "getEventTarget", "isInEvent", "release", "startSequence", "didGetMessage", "resetGotMessage",
                "getMoghouseFlag", "setMoghouseFlag", "needToZone", "getID", "getTargID", "getCursorTarget",
                "getObjType", "isPC", "isNPC", "isMob", "isPet", "isTrust", "isFellow", "isAlly", "initNpcAi",
                "resetAI", "getStatus", "setStatus", "getCurrentAction", "canUseAbilities", "lookAt", "facePlayer",
                "clearTargID", "atPoint", "pathTo", "pathThrough", "isFollowingPath", "clearPath", "continuePath",
                "checkDistance", "wait", "follow", "hasFollowTarget", "unfollow", "canSee", "inWater", "openDoor",
                "closeDoor", "addPeriodicTrigger", "showNPC", "hideNPC", "updateNPCHideTime", "getWeather",
                "setWeather", "changeMusic", "sendMenu", "openGuildShop", "clearGuildShop", "sendGuildClose",
                "openSendBox", "leaveGame", "sendEmote", "getWorldAngle", "getFacingAngle", "isFacing", "isInfront",
                "isBehind", "isBeside", "isToEntitysLeft", "isToEntitysRight", "getZone", "getZoneID", "getZoneName",
                "hasVisitedZone", "getPreviousZone", "getPreviousZoneLineID", "getCurrentRegion", "getContinentID",
                "inMogHouse", "visitMogHouse", "getMogHouseOwner", "getMogHouseVisitors", "getPos", "showPosition",
                "getXPos", "getYPos", "getZPos", "getRotPos", "setPos", "setRotation", "warp", "teleport",
                "positionSpecial", "addTeleport", "getTeleport", "getTeleportTable", "hasTeleport", "setTeleportMenu",
                "getTeleportMenu", "setHomePoint", "learnMazeVoucher", "hasMazeVoucher", "learnMazeRune", "hasMazeRune",
                "resetPlayer", "gotoEntity", "gotoPlayer", "bringPlayer", "getEquipID", "getEquippedItem",
                "hasEquipped", "hasItem", "getItemCount", "addItem", "delItem", "delItemAt", "delContainerItems",
                "addUsedItem", "addTempItem", "getWornUses", "incrementItemWear", "findItem", "findItems", "getItems",
                "createShop", "addShopItem", "getCurrentGPItem", "breakLinkshell", "addLinkpearl", "getContainerSize",
                "changeContainerSize", "getFreeSlotsCount", "confirmTrade", "tradeComplete", "getTrade", "canEquipItem",
                "equipItem", "unequipItem", "setEquipBlock", "lockEquipSlot", "unlockEquipSlot", "hasSlotEquipped",
                "getShieldSize", "getShieldDefense", "addGearSetMod", "clearGearSetMods", "getStorageItem",
                "storeWithPorterMoogle", "getRetrievableItemsForSlip", "retrieveItemFromSlip", "getRace", "getFace",
                "getGender", "getSize", "raceChange", "getName", "getPacketName", "renameEntity", "hideName",
                "getModelId", "setModelId", "setLook", "getEquipmentModelIds", "getCostume", "setCostume",
                "getCostume2", "setCostume2", "getAnimation", "setAnimation", "getAnimationSub", "setAnimationSub",
                "setSpawnAnimation", "getCallForHelpFlag", "setCallForHelpFlag", "getCallForHelpBlocked",
                "setCallForHelpBlocked", "getNation", "setNation", "getAllegiance", "setAllegiance",
                "getCampaignAllegiance", "setCampaignAllegiance", "isSeekingParty", "getNewPlayer", "setNewPlayer",
                "getMentor", "setMentor", "getGMLevel", "setGMLevel", "setVisibleGMLevel", "getVisibleGMLevel",
                "getGMHidden", "setGMHidden", "getWallhack", "setWallhack", "setFreezeFlag", "isJailed", "jail",
                "canUseMisc", "getSpeed", "getBaseSpeed", "setBaseSpeed", "setAnimationSpeed", "getPlaytime",
                "getTimeCreated", "getMainJob", "getSubJob", "changeJob", "changesJob", "unlockJob", "hasJob",
                "getMainLvl", "getSubLvl", "getJobLevel", "setLevel", "setsLevel", "getLevelCap", "setLevelCap",
                "levelRestriction", "addWyvernJobTraits", "getMonstrosityData", "setMonstrosityData",
                "getBelligerencyFlag", "setBelligerencyFlag", "changeMonstrositySpecies", "getMonstrositySize",
                "setMonstrosityEntryData", "getMonstrosityShop", "getMonstrosityExits", "isMonstrosityPassageZone",
                "getTitle", "hasTitle", "addTitle", "setTitle", "delTitle", "getFame", "addFame", "setFame",
                "getFameLevel", "getRank", "setRank", "getRankPoints", "addRankPoints", "setRankPoints", "addQuest",
                "delCurrentQuest", "delQuest", "getQuestStatus", "hasCompletedQuest", "completeQuest", "addMission",
                "delMission", "getCurrentMission", "hasCompletedMission", "completeMission", "setMissionStatus",
                "getMissionStatus", "sendPartialMissionLog", "getEminenceCompleted", "getNumEminenceCompleted",
                "setEminenceCompleted", "getEminenceProgress", "setEminenceProgress", "hasEminenceRecord",
                "triggerRoeEvent", "setUnityLeader", "getUnityLeader", "getUnityRank", "getClaimedDeedMask",
                "toggleReceivedDeedRewards", "setClaimedDeed", "resetClaimedDeeds", "setUniqueEvent", "delUniqueEvent",
                "hasCompletedUniqueEvent", "addAssault", "delAssault", "getCurrentAssault", "hasCompletedAssault",
                "completeAssault", "addKeyItem", "delKeyItem", "hasKeyItem", "seenKeyItem", "unseenKeyItem", "addExp",
                "delExp", "getMerit", "getMeritCount", "setMerits", "getSpentJobPoints", "getJobPointLevel",
                "addCapacityPoints", "setCapacityPoints", "setJobPoints", "addJobPoints", "delJobPoints",
                "getJobPoints", "masterJob", "getGil", "addGil", "setGil", "delGil", "getCP", "addCP", "delCP",
                "gainConquestInfluence", "addConquestMobKills", "getSeals", "addSeals", "delSeals", "getCurrency",
                "addCurrency", "setCurrency", "delCurrency", "getAssaultPoint", "addAssaultPoint", "delAssaultPoint",
                "addGuildPoints", "getHP", "getHPP", "getMaxHP", "getBaseHP", "addHP", "addHPLeaveSleeping", "setHP",
                "die", "setMaxHP", "restoreHP", "delHP", "takeDamage", "hideHP", "getDeathType", "setDeathType",
                "getMP", "getMPP", "getMaxMP", "getBaseMP", "addMP", "setMP", "setMaxMP", "restoreMP", "delMP", "getTP",
                "addTP", "setTP", "delTP", "updateHealth", "getAverageItemLevel", "capSkill", "capAllSkills",
                "getSkillLevel", "setSkillLevel", "getMaxSkillLevel", "getSkillRank", "setSkillRank",
                "getCharSkillLevel", "addLearnedWeaponskill", "hasLearnedWeaponskill", "delLearnedWeaponskill",
                "trySkillUp", "addWeaponSkillPoints", "addLearnedAbility", "hasLearnedAbility", "canLearnAbility",
                "delLearnedAbility", "addSpell", "hasSpell", "canLearnSpell", "delSpell", "getSetBlueSpells",
                "recalculateSkillsTable", "recalculateAbilitiesTable", "getEntitiesInRange", "getParty",
                "getPartyWithTrusts", "getPartySize", "hasPartyJob", "getPartyMember", "getPartyLeader", "getLeaderID",
                "getPartyLastMemberJoinedTime", "forMembersInRange", "addPartyEffect", "hasPartyEffect",
                "removePartyEffect", "getAlliance", "getAllianceSize", "reloadParty", "disableLevelSync",
                "checkSoloPartyAlliance", "checkKillCredit", "checkDifficulty", "getInstance", "setInstance",
                "createInstance", "instanceEntry", "getConfrontationEffect", "copyConfrontationEffect",
                "getBattlefield", "getRegisteredBattlefield", "getBattlefieldID", "registerBattlefield",
                "battlefieldAtCapacity", "enterBattlefield", "leaveBattlefield", "isInDynamis", "setEnteredBattlefield",
                "hasEnteredBattlefield", "isAlive", "isDead", "hasRaiseTractorMenu", "sendRaise", "sendReraise",
                "sendTractor", "allowSendRaisePrompt", "countdown", "objectiveUtility", "enableEntities",
                "independentAnimation", "engage", "isEngaged", "disengage", "timer", "queue", "addRecast", "hasRecast",
                "resetRecast", "resetRecasts", "addListener", "removeListener", "triggerListener", "hasListener",
                "getEntity", "canChangeState", "wakeUp", "setBattleID", "getBattleID", "recalculateStats",
                "checkImbuedItems", "isDualWielding", "isUsingH2H", "getBaseWeaponDelay", "getBaseDelay",
                "getBaseRangedDelay", "checkLiementAbsorb", "getCE", "getVE", "setCE", "setVE", "addBaseEnmity",
                "addEnmity", "lowerEnmity", "updateEnmity", "transferEnmity", "updateEnmityFromDamage",
                "updateEnmityFromCure", "resetEnmity", "setEnmityActive", "updateClaim", "hasClaim", "hasEnmity",
                "getNotorietyList", "getMasterThreatMob", "clearEnmityForEntity", "addStatusEffect", "copyStatusEffect",
                "getStatusEffect", "getStatusEffectBySource", "getStatusEffects", "getStatusEffectElement",
                "canGainStatusEffect", "hasStatusEffect", "hasStatusEffectByFlag", "countEffect", "countEffectWithFlag",
                "delStatusEffect", "delStatusEffectsByFlag", "delStatusEffectsByType", "delStatusEffectSilent",
                "eraseStatusEffect", "eraseAllStatusEffect", "dispelStatusEffect", "dispelAllStatusEffect",
                "stealStatusEffect", "addMod", "getMod", "setMod", "delMod", "printAllMods", "getMaxGearMod",
                "getGearModFromSlot", "addLatent", "delLatent", "hasAllLatentsActive", "doWildCard", "doRandomDeal",
                "hasBustEffect", "numBustEffects", "healingWaltz", "addBardSong", "charm", "uncharm", "isCharmed",
                "isTandemActive", "addBurden", "getOverloadChance", "setStatDebilitation",
                "isExceedingElementalCapacity", "getStat", "getACC", "getEVA", "getRACC", "getRATT", "getILvlMacc",
                "getILvlSkill", "getILvlParry", "physicalDmgTaken", "rangedDmgTaken", "handleAfflatusMiseryDamage",
                "isWeaponTwoHanded", "getWeaponDmg", "getWeaponDmgRank", "getOffhandDmg", "getOffhandDmgRank",
                "getRangedDmg", "getRangedDmgRank", "getAmmoDmg", "getWeaponHitCount", "addDamageFromMultipliers",
                "removeAmmo", "getWeaponSkillLevel", "getWeaponDamageType", "getWeaponSkillType",
                "getWeaponSubSkillType", "getWSSkillchainProp", "takeWeaponskillDamage", "takeSpellDamage",
                "takeSwipeLungeDamage", "checkDamageCap", "handleSevereDamage", "spawnPet", "despawnPet",
                "setJugRemainingTime", "hasPet", "hasJugPet", "getPet", "getPetID", "isAutomaton", "isAvatar",
                "isJugPet", "getPetElement", "setPet", "setPetStats", "getMinimumPetLevel", "getMaster", "getPetName",
                "setPetName", "registerChocobo", "getFieldChocobo", "getChocoboUserData", "setChocoboUserData",
                "petAttack", "petAbility", "petRetreat", "extendCharm", "addPetMod", "setPetMod", "delPetMod",
                "hasAttachment", "hasAttachmentSet", "getAutomatonName", "getAutomatonFrame", "setAutomatonFrame",
                "getAutomatonHead", "setAutomatonHead", "unlockAttachment", "getActiveManeuverCount",
                "removeOldestManeuver", "removeAllManeuvers", "setAttachment", "getAttachments", "updateAttachments",
                "reduceBurden", "getAllRuneEffects", "getActiveRuneCount", "getHighestRuneEffect",
                "getNewestRuneEffect", "removeOldestRune", "removeNewestRune", "removeAllRunes", "spawnTrust",
                "clearTrusts", "getTrustID", "trustPartyMessage", "addGambit", "removeGambit", "removeAllGambits",
                "setTrustTPSkillSettings", "setMobLevel", "getStatRank", "setStatRank", "getEcosystem", "getFamily",
                "getSpecies", "isMobType", "isUndead", "isNM", "getModelSize", "setModelSize", "getHitboxSize",
                "setHitboxSize", "getMeleeRange", "setMobFlags", "getMobFlags", "setNpcFlags", "setNpcAlwaysRelevant",
                "spawn", "isSpawned", "getSpawnPos", "setSpawn", "getRespawnTime", "setRespawnTime", "getSpawnSlotMobs",
                "instantiateMob", "hasTrait", "hasImmunity", "addImmunity", "delImmunity", "setAggressive",
                "setTrueDetection", "setUnkillable", "getUnkillable", "setUntargetable", "getUntargetable",
                "setPriorityRender", "setIsAggroable", "isAggroable", "setDelay", "setDamage", "getSpellListId",
                "hasSpellList", "setSpellList", "setAutoAttackEnabled", "setRangedAttackEnabled",
                "isRangedAttackEnabled", "setMagicCastingEnabled", "setMobAbilityEnabled", "setMobSkillAttack",
                "getMobMod", "setMobMod", "addMobMod", "delMobMod", "getfTPModifierOverride", "setfTPModifierOverride",
                "getBattleTime", "getCrystalElement", "setCrystalElement", "getBehavior", "setBehavior", "getLink",
                "setLink", "getRoamFlags", "setRoamFlags", "getTarget", "updateTarget", "getEnmityList",
                "getTrickAttackChar", "actionQueueEmpty", "castSpell", "useJobAbility", "useMobAbility",
                "usePetAbility", "getAbilityDistance", "hasTPMoves", "drawIn", "weaknessTrigger", "restoreFromChest",
                "hasPreventActionEffect", "stun", "untargetableAndUnactionable", "tryHitInterrupt", "getPool",
                "getDropID", "setDropID", "addTreasure", "getTreasurePool", "getStealItem", "getDespoilItem",
                "getDespoilDebuff", "itemStolen", "itemDespoiled", "getTHlevel", "setTHlevel", "isPlayerInTriggerArea",
                "onPlayerTriggerAreaEnter", "onPlayerTriggerAreaLeave", "clearPlayerTriggerAreas", "updateToEntireZone",
                "sendEntityUpdateToPlayer", "sendEmptyEntityUpdateToPlayer", "forceRezone",
                "getAvailableTraverserStones", "getTraverserEpoch", "setTraverserEpoch", "getClaimedTraverserStones",
                "addClaimedTraverserStones", "setClaimedTraverserStones", "getHistory", "clearActionQueue",
                "clearTimerQueue", "getChocoboRaisingInfo", "setChocoboRaisingInfo", "deleteRaisedChocobo",
                "setMannequinPose", "getMannequinPose", "submitContestFish", "withdrawContestFish", "getContestScore",
                "getContestRank", "getContestRewardStatus", "getContestRankHistory", "claimContestReward",
            },
            // lua_battlefield.cpp
            ["CBattlefield"] = new HashSet<string>
            {
                "getID", "getZoneID", "getArea", "getTimeLimit", "getRemainingTime", "getTimeInside", "getWipeTime",
                "getMaxParticipants", "getPlayerCount", "getPlayers", "getPlayersAndTrusts", "getMobs", "getNPCs",
                "getAllies", "getRecord", "getStatus", "getLastTimeUpdate", "getInitiator", "getLocalVar",
                "getArmouryCrate", "setLocalVar", "setLastTimeUpdate", "setTimeLimit", "setWipeTime", "setRecord",
                "setStatus", "insertEntity", "cleanup", "win", "lose", "addGroups",
            },
            // lua_instance.cpp
            ["CInstance"] = new HashSet<string>
            {
                "getID", "getName", "getZone", "getEntranceZoneID", "setLevelCap", "getAllies", "getChars", "getMobs",
                "getNpcs", "getPets", "getTimeLimit", "setTimeLimit", "getEntryPos", "getLevelCap", "getLastTimeUpdate",
                "setLastTimeUpdate", "getProgress", "getEntity", "setProgress", "getWipeTime", "setWipeTime",
                "getStage", "setStage", "fail", "failed", "complete", "completed", "insertAlly", "insertDynamicEntity",
                "getLocalVar", "setLocalVar",
            },
            // lua_item.cpp
            ["CItem"] = new HashSet<string>
            {
                "getID", "getSubID", "getFlag", "getAHCat", "getQuantity", "getLocationID", "getBasePrice", "getSlotID",
                "getTrialNumber", "getWornUses", "isType", "setSubType", "isSubType", "state", "getName", "getILvl",
                "getReqLvl", "getMod", "addMod", "delMod", "getAugment", "getSkillType", "getWeaponskillPoints",
                "setWeaponskillPointsNeeded", "getWeaponskillPointsNeeded", "isTwoHanded", "isHandToHand", "isShield",
                "getShieldSize", "getShieldAbsorptionRate", "getSignature", "getAppraisalID", "setAppraisalID",
                "getCurrentCharges", "isInstalled", "getExData", "setExData", "getExDataRaw", "setExDataRaw",
            },
            // lua_loot.cpp
            ["CLootContainer"] = new HashSet<string>
            {
                "addItem", "addGroup", "addItemFixed", "addGroupFixed",
            },
            // lua_mobskill.cpp
            ["CMobSkill"] = new HashSet<string>
            {
                "setMsg", "getMsg", "hasMissMsg", "isAoE", "isConal", "isSingle", "getParam", "getID", "getTargets",
                "getTotalTargets", "getPrimaryTargetID", "setFinalAnimationSub", "setAnimationTime", "getTP",
                "getMobHP", "getMobHPP", "getAttackType", "setAttackType", "isCritical", "setCritical", "getKnockback",
            },
            // lua_petskill.cpp
            ["CPetSkill"] = new HashSet<string>
            {
                "setMsg", "getMsg", "hasMissMsg", "isAoE", "isConal", "isSingle", "getParam", "getID",
                "getTotalTargets", "getPrimaryTargetID", "setFinalAnimationSub", "getTP", "getMobHP", "getMobHPP",
                "getAttackType", "setAttackType", "isCritical", "setCritical",
            },
            // lua_spell.cpp
            ["CSpell"] = new HashSet<string>
            {
                "setMsg", "setModifier", "setCritical", "setAoE", "setFlag", "setRadius", "setAnimation", "setCastTime",
                "setMPCost", "isAoE", "getAoE", "isConal", "getRadius", "tookEffect", "getMagicBurstMessage",
                "getElement", "getTotalTargets", "getSkillType", "getID", "getMPCost", "getSpellGroup",
                "getSpellFamily", "getFlag", "getLevel", "getCastTime", "getPrimaryTargetID", "getKnockback",
                "isCritical",
            },
            // lua_statuseffect.cpp
            ["CStatusEffect"] = new HashSet<string>
            {
                "getEffectType", "getSubType", "getSourceType", "getSourceTypeParam", "getOriginID", "setOriginID",
                "setSource", "setIcon", "getPower", "setPower", "getDuration", "setDuration", "getStartTime",
                "getLastTick", "getTimeRemaining", "getTickCount", "resetStartTime", "addMod", "getSubPower",
                "setSubPower", "setSubIcon", "getTier", "setTier", "getEffectSlot", "setEffectSlot", "getTick",
                "setTick", "setStartTime", "getEffectFlags", "setEffectFlags", "addEffectFlag", "delEffectFlag",
                "hasEffectFlag", "getIcon", "getSubIcon",
            },
            // lua_trade_container.cpp
            ["CTradeContainer"] = new HashSet<string>
            {
                "getGil", "getItem", "getItemId", "getItemSubId", "getItemCount", "getSlotCount", "getItemQty",
                "getSlotQty", "hasItemQty", "confirmItem", "confirmSlot", "clean",
            },
            // lua_trait.cpp
            ["CTrait"] = new HashSet<string>
            {
                "getID", "getJob", "getLevel", "getMod", "getValue", "getRank", "getMeritID",
            },
            // lua_treasure_pool.cpp
            ["CTreasurePool"] = new HashSet<string>
            {
                "getType", "flush", "addMember", "delMember", "updatePool", "addItem", "memberCount", "getMembers",
                "getItems",
            },
            // lua_trigger_area.cpp
            ["ITriggerArea"] = new HashSet<string>
            {
                "getTriggerAreaID", "getCount", "addCount", "delCount",
            },
            // lua_weaponskill.cpp
            ["CWeaponSkill"] = new HashSet<string>
            {
                "isAoE", "isSingle", "getID",
            },
            // lua_zone.cpp
            ["CZone"] = new HashSet<string>
            {
                "getLocalVar", "getLocalVars", "setLocalVar", "resetLocalVars", "registerCuboidTriggerArea",
                "registerCylindricalTriggerArea", "registerSphericalTriggerArea", "levelRestriction", "getPlayers",
                "getNPCs", "getMobs", "getID", "getName", "getRegionID", "getTypeMask", "getBattlefieldByInitiator",
                "getWeather", "getUptime", "reloadNavmesh", "rebuildNavmesh", "isNavigablePoint", "getTerrainType",
                "getFloorId", "insertDynamicEntity", "getSoloBattleMusic", "getPartyBattleMusic",
                "getBackgroundMusicDay", "getBackgroundMusicNight", "setSoloBattleMusic", "setPartyBattleMusic",
                "setBackgroundMusicDay", "setBackgroundMusicNight", "queryEntitiesByName",
            },
        };
    }
}
