-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Villion
-- Type: Adventurer's Assistant NPC
--  Involved in Quest: Flyers for Regine
-- !pos -157.524 4.000 263.818 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade) -- FLYERS FOR REGINE
end

return entity
