-----------------------------------
-- Area: Port Windurst
--  NPC: Goltata
--  Involved in Quests: Wonder Wands
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.wonder_wands', 'onTrigger') then
        player:startEvent(232)
    end
end

return entity
