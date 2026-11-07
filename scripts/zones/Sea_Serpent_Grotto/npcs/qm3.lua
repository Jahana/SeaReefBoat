-----------------------------------
-- Area: Sea Serpent Grotto
--  NPC: ??? Used for Norg quest "It's not your vault"
-- !pos -173 26 252 176
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.its_not_your_vault', 'onTrigger')
end

return entity
