-----------------------------------
-- Area: Mhaura
--  NPC: Nereus
-- Starts and ends repeteable quest A_POTTERS_PREFERENCE
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.a_potters_preference', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.a_potters_preference', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.a_potters_preference', csid, option)
end

return entity
