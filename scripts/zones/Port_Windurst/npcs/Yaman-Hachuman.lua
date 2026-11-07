-----------------------------------
-- Area: Port Windurst
--  NPC: Yaman-Hachuman
--  Involved in Quests: Wonder Wands
-- !pos -101.209 -4.25 110.886 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.wonder_wands', 'onTrigger') then
        player:startEvent(233)
    end
end

return entity
