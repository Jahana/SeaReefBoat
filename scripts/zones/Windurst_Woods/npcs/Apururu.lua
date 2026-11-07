-----------------------------------
-- Area: Windurst Woods
--  NPC: Apururu
-- Involved in Quests: The Kind Cardian, Can Cardians Cry?
-- !pos -11 -2 13 241
-----------------------------------
local ID = zones[xi.zone.WINDURST_WOODS]
-----------------------------------
---@type TNpcEntity
local entity = {}

local trustMemory = function(player)
    local memories = 0
    -- 2 - Saw him at the start of the game
    if player:getNation() == xi.nation.WINDURST then
        memories = memories + 2
    end

    -- 4 - WONDER_WANDS
    if player:hasCompletedQuest(xi.questLog.WINDURST, xi.quest.id.windurst.WONDER_WANDS) then
        memories = memories + 4
    end

    -- 8 - THE_TIGRESS_STIRS
    if player:hasCompletedQuest(xi.questLog.CRYSTAL_WAR, xi.quest.id.crystalWar.THE_TIGRESS_STIRS) then
        memories = memories + 8
    end

    -- 16 - I_CAN_HEAR_A_RAINBOW
    if player:hasCompletedQuest(xi.questLog.WINDURST, xi.quest.id.windurst.I_CAN_HEAR_A_RAINBOW) then
        memories = memories + 16
    end

    -- 32 - Hero's Combat (BCNM)
    -- if (playervar for Hero's Combat) then
    --  memories = memories + 32
    -- end
    -- 64 - MOON_READING
    if player:hasCompletedMission(xi.mission.log_id.WINDURST, xi.mission.id.windurst.MOON_READING) then
        memories = memories + 64
    end

    return memories
end

entity.onTrade = function(player, npc, trade)
    -- THE KIND CARDIAN
    if xi.cs.quest(player, npc, 'jeuno.the_kind_cardian', 'onTrade', trade) then
        return

        -- CAN CARDIANS CRY?
    else
        xi.cs.quest(player, npc, 'windurst.can_cardians_cry', 'onTrade', trade)
    end
end

entity.onTrigger = function(player, npc)
        -- THE KIND CARDIAN
    if xi.cs.quest(player, npc, 'jeuno.the_kind_cardian', 'onTrigger') then
        return

        -- CAN CARDIANS CRY?
    elseif xi.cs.quest(player, npc, 'windurst.can_cardians_cry', 'onTrigger') then
        return

        -- TRUST
    elseif
        player:hasKeyItem(xi.keyItem.WINDURST_TRUST_PERMIT) and
        not player:hasSpell(xi.magic.spell.AJIDO_MARUJIDO)
    then
        local rank6 = player:getRank(player:getNation()) >= 6 and 1 or 0

        player:startEvent(866, 0, 0, 0, trustMemory(player), 0, 0, 0, rank6)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
        -- THE KIND CARDIAN
    if csid == 392 or csid == 397 then
        xi.cs.questEvent(player, npc, 'jeuno.the_kind_cardian', csid, option)

        -- CAN CARDIANS CRY?
    elseif csid == 319 or csid == 325 then
        xi.cs.questEvent(player, npc, 'windurst.can_cardians_cry', csid, option)

        -- TRUST
    elseif csid == 866 and option == 2 then
        player:addSpell(xi.magic.spell.AJIDO_MARUJIDO, { silentLog = true })
        player:messageSpecial(ID.text.YOU_LEARNED_TRUST, 0, xi.magic.spell.AJIDO_MARUJIDO)
    end
end

return entity
