-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Capiria
-- Type: Involved in Quest (Flyers for Regine)
-- !pos -127.355 0.000 130.461 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade) -- FLYERS FOR REGINE
end

return entity
