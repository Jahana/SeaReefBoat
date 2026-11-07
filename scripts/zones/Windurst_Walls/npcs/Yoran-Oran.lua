-----------------------------------
-- Area: Windurst Walls
--  NPC: Yoran-Oran
-- !pos -109.987 -14 203.338 239
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.mandragora_mad', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.mandragora_mad', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.mandragora_mad', csid, option)
end

return entity
