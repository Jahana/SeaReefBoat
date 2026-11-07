-----------------------------------
-- Area: Yahse Hunting Grounds
--  NPC: Yahse Wildflower
-- Involved in quest Children of the Rune
-- pos 370.6285 0.6692 153.3728
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.children_of_the_rune', 'onTrigger')
end

return entity
