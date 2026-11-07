-----------------------------------
-- Area: Port Windurst
--  NPC: Tokaka
-- Starts & Finishes Repeatable Quest: Something Fishy
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.something_fishy', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.something_fishy', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.something_fishy', csid, option)
end

return entity
