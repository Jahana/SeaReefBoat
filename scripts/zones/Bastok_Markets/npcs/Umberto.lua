-----------------------------------
-- Area: Bastok Markets
--  NPC: Umberto
-- Involved in Quest: Too Many Chefs
-- !pos -56.896 -5 -134.267 235
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'bastok.too_many_chefs', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'bastok.too_many_chefs', csid, option)
end

return entity
