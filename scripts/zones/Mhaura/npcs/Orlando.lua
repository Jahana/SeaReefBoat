-----------------------------------
-- Area: Mhaura
--  NPC: Orlando
-- !pos -37.268 -9 58.047 249
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.orlandos_antiques', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.orlandos_antiques', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.orlandos_antiques', csid, option)
end

return entity
