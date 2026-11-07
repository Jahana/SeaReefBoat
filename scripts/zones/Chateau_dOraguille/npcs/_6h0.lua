-----------------------------------
-- Area: Chateau d'Oraguille
-- Door: Prince Royal's
-- Finishes Quest: A Boy's Dream, Under Oath
-- Involved in Missions: 3-1, 5-2, 8-2
-- !pos -38 -3 73 233
-----------------------------------
local ID = zones[xi.zone.CHATEAU_DORAGUILLE]
-----------------------------------
---@type TNpcEntity
local entity = {}

local function TrustMemory(player)
    local memories = 0
    -- 2 - LIGHTBRINGER
    if player:hasCompletedMission(xi.mission.log_id.SANDORIA, xi.mission.id.sandoria.LIGHTBRINGER) then
        memories = memories + 2
    end

    -- 4 - IMMORTAL_SENTRIES
    if player:hasCompletedMission(xi.mission.log_id.TOAU, xi.mission.id.toau.IMMORTAL_SENTRIES) then
        memories = memories + 4
    end

    -- 8 - UNDER_OATH
    if player:hasCompletedQuest(xi.questLog.SANDORIA, xi.quest.id.sandoria.UNDER_OATH) then
        memories = memories + 8
    end

    -- 16 - FIT_FOR_A_PRINCE
    if player:hasCompletedQuest(xi.questLog.SANDORIA, xi.quest.id.sandoria.FIT_FOR_A_PRINCE) then
        memories = memories + 16
    end

    -- 32 - Hero's Combat BCNM
    -- if (playervar for Hero's Combat) then
    --  memories = memories + 32
    -- end
    return memories
end

entity.onTrigger = function(player, npc)
    -- "Under Oath" (PLD AF Body)
    if xi.cs.quest(player, npc, 'sandoria.under_oath', 'onTrigger') then
        return

    -- Trust: San d'Oria (Trion)
    elseif
        player:getRank(player:getNation()) >= 6 and
        player:hasKeyItem(xi.keyItem.SAN_DORIA_TRUST_PERMIT) and
        not player:hasSpell(xi.magic.spell.TRION)
    then
        player:startEvent(574, 0, 0, 0, TrustMemory(player))

    -- "A Boy's Dream" (PLD AF Feet)
    elseif xi.cs.quest(player, npc, 'sandoria.a_boys_dream', 'onTrigger') then
        return

    -- San d'Oria Rank 10 (different default)
    elseif
        player:getNation() == xi.nation.SANDORIA and
        player:getRank(player:getNation()) == 10
    then
        player:startEvent(62)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 88 then
        xi.cs.questEvent(player, npc, 'sandoria.a_boys_dream', csid, option)
    elseif csid == 90 or csid == 89 then
        xi.cs.questEvent(player, npc, 'sandoria.under_oath', csid, option)
    elseif csid == 574 and option == 2 then
        player:addSpell(xi.magic.spell.TRION, { silentLog = true })
        player:messageSpecial(ID.text.YOU_LEARNED_TRUST, 0, xi.magic.spell.TRION)
    end
end

return entity
