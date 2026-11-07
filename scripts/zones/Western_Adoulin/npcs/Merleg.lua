-----------------------------------
-- Area: Western Adoulin
--  NPC: Merleg
-- Starts, Involved with, and Finishes Quest: 'A Pioneers Best (Imaginary) Friend'
-- !pos 34 0 -131 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.a_pioneers_best_imaginary_friend', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.a_pioneers_best_imaginary_friend', csid, option)
end

return entity
