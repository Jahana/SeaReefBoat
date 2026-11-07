-----------------------------------
-- Area: Port San d'Oria
--  NPC: Auvare
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade) -- FLYERS FOR REGINE
end

return entity
