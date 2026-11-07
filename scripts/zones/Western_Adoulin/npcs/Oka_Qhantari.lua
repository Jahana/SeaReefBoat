-----------------------------------
-- Area: Western Adoulin
--  NPC: Oka Qhantari
--  Involved With Quest: 'Order Up'
-- !pos -30 3 -6 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.order_up', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.order_up', csid, option)
end

return entity
