-----------------------------------
-- Area: Windurst Waters
--  NPC: Chamama
-- Involved In Quest: Inspector's Gadget
-- Starts Quest: In a Pickle
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.in_a_pickle', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.in_a_pickle', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.in_a_pickle', csid, option)
end

return entity
