-----------------------------------
-- Area: Tavnazian Safehold
--  NPC: Yurim
-- !pos 83.697 -25.000 3.250 26
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.in_the_name_of_science', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.in_the_name_of_science', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.in_the_name_of_science', csid, option)
end

return entity
