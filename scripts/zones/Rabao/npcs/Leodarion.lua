-----------------------------------
-- Area: Rabao
--  NPC: Leodarion
-- Involved in Quest: True Will
-- !pos -50 8 41 247
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.true_will', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.true_will', csid, option)
end

return entity
