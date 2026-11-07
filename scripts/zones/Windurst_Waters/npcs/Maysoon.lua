-----------------------------------
-- Area: Windurst Waters
--  NPC: Maysoon
-- Starts and Finishes Quest: Hoist the Jelly, Roger
-- Involved in Quests: Cook's Pride
-- !pos -105 -2 69 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.hoist_the_jelly_roger', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.hoist_the_jelly_roger', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.hoist_the_jelly_roger', csid, option)
end

return entity
