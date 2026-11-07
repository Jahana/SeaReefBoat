-----------------------------------
-- Area: Metalworks
--  NPC: Ferghus
-- Starts Quest: Too Many Chefs (1, 86)
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'bastok.too_many_chefs', 'onTrigger') then
        return
    end

    player:startEvent(420) -- Standard
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'bastok.too_many_chefs', csid, option)
end

return entity
