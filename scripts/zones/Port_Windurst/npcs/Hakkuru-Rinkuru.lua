-----------------------------------
-- Area: Port Windurst
--  NPC: Hakkuru-Rinkuru
-- Involved In Quest: Making Amends
-- Starts and Ends Quest: Wonder Wands
-- !pos -111 -4 101 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.wonder_wands', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.wonder_wands', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.wonder_wands', csid, option)
end

return entity
