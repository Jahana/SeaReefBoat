-----------------------------------
-- Area: Port Bastok
--  NPC: Ensetsu
-- Involved in Quest: 20 in Pirate Years
-- !pos 32 -7 69 236
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.twenty_in_pirate_years', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.twenty_in_pirate_years', csid, option)
end

return entity
