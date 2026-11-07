-----------------------------------
-- Area: Yuhtunga Jungle
--  NPC: Blue Rafflesia
-- Used in quest Even More Gullible Travels
-- !pos -468.876 -1 220.247 123 <many>
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.even_more_gullibles_travels', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.even_more_gullibles_travels', csid, option)
end

return entity
