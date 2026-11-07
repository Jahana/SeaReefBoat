-----------------------------------
-- Area: The_Eldieme_Necropolis_[S]
--  NPC: Heptachiond
-- Starts and Finishes Quest: REQUIEM_FOR_THE_DEPARTED
-- !pos 256 -32 20 175
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.requiem_for_the_departed', 'onTrigger') then
        return
    end

    player:startEvent(104) -- Standard dialog
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.requiem_for_the_departed', csid, option)
end

return entity
