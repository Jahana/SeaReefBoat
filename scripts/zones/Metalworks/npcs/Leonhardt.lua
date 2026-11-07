-----------------------------------
-- Area: Metalworks
--  NPC: Leonhardt
-- Involved in Quest: Too Many Chefs
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'bastok.too_many_chefs', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'bastok.too_many_chefs', 'onTrigger') then
        return
    end

    player:startEvent(945) -- standard
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'bastok.too_many_chefs', csid, option)
end

return entity
