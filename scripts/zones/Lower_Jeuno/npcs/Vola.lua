-----------------------------------
-- Area: Lower Jeuno
--  NPC: Vola
-- Starts and Finishes Quest: Fistful of Fury
-- Involved in Quests: Beat Around the Bushin (before the quest)
-- !pos 43 3 -45 245
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'jeuno.fistful_of_fury', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    -- Fistful of Fury, Beat Around the Bushin (one if-chain in the Lua)
    if
        xi.cs.quest(player, npc, 'jeuno.fistful_of_fury', 'onTrigger') or
        xi.cs.quest(player, npc, 'jeuno.beat_around_the_bushin', 'onTrigger')
    then
        return
    else
        player:startEvent(212) -- Standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 216 or csid == 213 then
        xi.cs.questEvent(player, npc, 'jeuno.fistful_of_fury', csid, option)
    elseif csid == 160 then
        xi.cs.questEvent(player, npc, 'jeuno.beat_around_the_bushin', csid, option)
    end
end

return entity
