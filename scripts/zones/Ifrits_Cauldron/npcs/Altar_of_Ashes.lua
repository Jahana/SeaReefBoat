-----------------------------------
-- Area: Ifrit's Cauldron
--  NPC: Altar of Ashes
-- Involved in Quest: Greetings to the Guardian
-- !pos 16 .1 -58 205
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.greetings_to_the_guardian', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.greetings_to_the_guardian', 'onTrigger')
end

return entity
