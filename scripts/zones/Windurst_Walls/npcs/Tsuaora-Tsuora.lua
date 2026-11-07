-----------------------------------
-- Area: Windurst Walls
--  NPC: Tsuaora-Tsuora
-- !pos 71.489 -3.418 -67.809 239
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.the_postman_always_kos_twice', 'onTrigger') then
        player:startEvent(42)
    end
end

return entity
