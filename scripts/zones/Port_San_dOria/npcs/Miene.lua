-----------------------------------
-- Area: Port San d'Oria
--  NPC: Miene
-- NPC for Quest "The Pickpocket"
-- !pos 0.750 -4.000 -81.438 232
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade) -- FLYERS FOR REGINE
end

return entity
