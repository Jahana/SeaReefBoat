-----------------------------------
-- Area: Western Adoulin
--  NPC: Clautaire
-- Starts, Involved with, and Finishes Quest: 'F.A.I.L.ure Is Not an Option'
-- !pos 44 4 -159 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.failure_is_not_an_option', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.failure_is_not_an_option', csid, option)
end

return entity
