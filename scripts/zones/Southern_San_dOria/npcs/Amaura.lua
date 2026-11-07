-----------------------------------
-- Area: Southern San d'Oria
-- NPC : Amaura
-- Involved in Quest: The Medicine Woman, To Cure a Cough
-- !pos -85 -6 89 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'sandoria.to_cure_a_cough', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.to_cure_a_cough', csid, option)
end

return entity
