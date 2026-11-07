-----------------------------------
-- Area: Castle Zvahl Keep
--  NPC: Ore door
-- Involved In Quest: Recollections
-- !pos -14 0 69 162
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.recollections', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    player:startEvent(9)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.recollections', csid, option)
end

return entity
