-----------------------------------
-- Area: Tavnazian Safehold
--  NPC: Ferchinne
-- Note: Involved in quest: "Fly High"
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.fly_high', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.fly_high', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.fly_high', csid, option)
end

return entity
