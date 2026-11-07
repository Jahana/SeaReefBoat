-----------------------------------
-- Area: Windurst Waters (S)
--  NPC: Door Acolyte hostel
-- !pos 146.619, -8.525, 242.874 94
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'crystalWar.say_it_with_a_handbag', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.say_it_with_a_handbag', csid, option)
end

return entity
