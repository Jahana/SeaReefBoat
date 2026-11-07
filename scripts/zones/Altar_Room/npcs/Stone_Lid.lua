-----------------------------------
-- Area: Altar Room
--  NPC: Stone Lid
-- !pos -316.4390, 24.7654, 12.1590
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.a_moral_manifest', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.a_moral_manifest', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.a_moral_manifest', csid, option)
end

return entity
