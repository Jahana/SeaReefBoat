-----------------------------------
-- Area: Western Adoulin
--  NPC: Gontrain
-- Involved with Quest: 'Raptor Rapture'
-- !pos 13 0 -143 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.raptor_rapture', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.raptor_rapture', csid, option)
end

return entity
