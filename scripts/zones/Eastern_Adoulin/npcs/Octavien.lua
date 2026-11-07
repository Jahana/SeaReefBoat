-----------------------------------
-- Area: Eastern Adoulin (257)
--  NPC: Octavien
-- Type: Palace Guard
-- Starts Children of the Rune
-- !pos 100.580 -40.150 -63.830
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'adoulin.children_of_the_rune', 'onTrigger') then
        return
    end

    player:startEvent(27) -- Standard dialog
end

entity.onEventUpdate = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.children_of_the_rune', csid, option, 'update')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.children_of_the_rune', csid, option)
end

return entity
