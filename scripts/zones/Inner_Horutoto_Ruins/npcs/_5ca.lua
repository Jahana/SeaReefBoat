-----------------------------------
-- Area: Inner Horutoto Ruins
--  NPC: Mahogany Door
-- Involved In Quest: Making Headlines
-- Involved in Mission 2-1
-- !pos -11 0 20 192
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.making_headlines', 'onTrigger')
end

return entity
