-----------------------------------
-- Area: Davoi
--  NPC: ??? (qm1)
-- Involved in Quest: To Cure a Cough
-- !pos -115.830 -0.427 -184.289 149
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'sandoria.to_cure_a_cough', 'onTrigger')
end

return entity
