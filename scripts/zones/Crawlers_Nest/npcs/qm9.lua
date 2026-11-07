-----------------------------------
-- Area: Crawlers Nest
--  NPC: ???
-- Used In Quest: A Boy's Dream
-- !pos -18 -8 124 197
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'sandoria.a_boys_dream', 'onTrigger')
end

return entity
