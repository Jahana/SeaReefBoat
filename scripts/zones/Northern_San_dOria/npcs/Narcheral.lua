-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Narcheral
-- Starts and Finishes Quest: Messenger from Beyond, Prelude of Black and White (Finish), Pieuje's Decision (Finish)
-- !pos 129 -11 126 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.pieujes_decision', 'onTrade', trade)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.pieujes_decision', csid, option)
end

return entity
