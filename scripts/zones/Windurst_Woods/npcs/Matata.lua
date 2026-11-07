-----------------------------------
-- Area: Windurst Woods
--  NPC: Matata
-- Involved in quest: In a Stew
-- !pos 131 -5 -109 241
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- CHOCOBILIOUS
    if xi.cs.quest(player, npc, 'windurst.chocobilious', 'onTrigger') then
        return

    -- STANDARD DIALOG
    else
        player:startEvent(223)
    end
end

return entity
