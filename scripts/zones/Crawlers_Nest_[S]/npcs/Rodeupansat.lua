-----------------------------------
-- Area: Crawlers_Nest_[S]
--  NPC: Rodeupansat
-- Starts and Finishes Quest: EVIL_AT_THE_INLET
-- !pos 129 -34 26 171
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.evil_at_the_inlet', 'onTrigger') then
        return
    end

    player:startEvent(106) -- Standard dialog
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.evil_at_the_inlet', csid, option)
end

return entity
