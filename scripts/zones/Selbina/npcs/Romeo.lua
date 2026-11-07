-----------------------------------
-- Area: Selbina
--  NPC: Romeo
-- Starts and Finishes Quest: Donate to Recycling
-- !pos -11 -11 -6 248
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.donate_to_recycling', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.donate_to_recycling', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.donate_to_recycling', csid, option)
end

return entity
