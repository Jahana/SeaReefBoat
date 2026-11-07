-----------------------------------
-- Area: Yhoator Jungle
--  NPC: ??? (qm3)
-- Involved in Quest: True will
-- !pos 203 0.1 82 124
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.true_will', 'onTrigger')
end

return entity
