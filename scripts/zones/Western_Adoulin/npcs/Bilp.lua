-----------------------------------
-- Area: Western Adoulin
--  NPC: Bilp
-- !pos -91 3 0 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.scaredycats', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.scaredycats', csid, option)
end

return entity
