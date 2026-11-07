-----------------------------------
-- Area: Windurst Walls
--  NPC: Orudoba-Sondeba
-- !pos 70.086 -3.503 -69.939 239
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.the_postman_always_kos_twice', 'onTrigger') then
        player:startEvent(43)
    end
end

return entity
