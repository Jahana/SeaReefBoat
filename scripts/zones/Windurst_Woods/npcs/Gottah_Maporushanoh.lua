-----------------------------------
-- Area: Windurst Woods
--  NPC: Gottah Maporushanoh
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.the_amazin_scorpio', 'onTrigger') then
        player:startEvent(420)
    end
end

return entity
