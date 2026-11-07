-----------------------------------
-- Area: Sea Serpent Grotto
--  NPC: Bou the Righteous
-- Type: Involved in the "Sahagin Key Quest"
-- !pos -125.029 46.568 -334.778 176
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- THE SAHAGIN'S KEY: 108, SahaginKeyProgress 2
    xi.cs.quest(player, npc, 'outlands.the_sahagins_key', 'onTrigger')
end

return entity
