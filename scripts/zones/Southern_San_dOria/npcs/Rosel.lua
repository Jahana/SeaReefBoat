-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Rosel
-- Starts and Finishes Quest: Rosel the Armorer
-- !zone 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade) -- FLYERS FOR REGINE
end

return entity
