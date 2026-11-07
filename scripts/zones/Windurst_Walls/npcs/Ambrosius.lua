-----------------------------------
-- Area: Windurst Walls
--  NPC: Ambrosius
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.the_postman_always_kos_twice', 'onTrigger')
end

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.the_postman_always_kos_twice', 'onTrade', trade)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.the_postman_always_kos_twice', csid, option)
end

return entity
