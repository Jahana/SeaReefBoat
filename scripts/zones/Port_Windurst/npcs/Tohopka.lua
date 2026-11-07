-----------------------------------
-- Area: Port Windurst
-- NPC : Tohopka
-- !pos -105.723 -10 83.813 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.to_catch_a_falling_star', 'onTrigger') then
        player:startEvent(358)
    end
end

return entity
