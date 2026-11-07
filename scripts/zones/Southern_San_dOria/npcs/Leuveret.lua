-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Leuveret
-- Type: General Info NPC
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade) -- FLYERS FOR REGINE
end

entity.onTrigger = function(player, npc)
    player:startEvent(621)
end

return entity
