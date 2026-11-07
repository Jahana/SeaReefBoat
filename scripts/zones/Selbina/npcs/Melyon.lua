-----------------------------------
-- Area: Selbina
--  NPC: Melyon
-- Starts and Finishes Quest: Only the Best (R)
-- Involved in Quest: Riding on the Clouds
-- !pos 25 -6 6 248
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.only_the_best', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.only_the_best', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.only_the_best', csid, option)
end

return entity
