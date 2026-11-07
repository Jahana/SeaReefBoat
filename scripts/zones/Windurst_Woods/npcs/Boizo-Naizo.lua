-----------------------------------
-- Area: Windurst Woods
--  NPC: Boizo-Naizo
-- Involved in Quests: A Greeting Cardian, Riding on the Clouds
-- !pos -9.581 -2.750 -26.062 241
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.the_all_new_c_2000', 'onTrigger')
end

return entity
