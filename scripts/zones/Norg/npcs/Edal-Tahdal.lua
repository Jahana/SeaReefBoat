-----------------------------------
-- Area: Norg
--  NPC: Edal-Tahdal
-- Starts and Finishes Quest: Trial by Water
-- !pos -13 1 -20 252
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.trial_by_water', 'onTrigger') then
        return
    end

    player:startEvent(113) -- Standard dialog
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 109 or csid == 190 or csid == 112 then
        xi.cs.questEvent(player, npc, 'outlands.trial_by_water', csid, option)
    end
end

return entity
