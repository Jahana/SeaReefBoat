-----------------------------------
-- Area: La Theine Plateau
--  NPC: Faurbellant
-- Involved in Quest: Gates of Paradise
-- !pos 484 24 -89 102
-----------------------------------
local ID = zones[xi.zone.LA_THEINE_PLATEAU]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.gates_to_paradise', 'onTrigger') then
        return
    end

    player:showText(npc, ID.text.FAURBELLANT_1)
end

return entity
