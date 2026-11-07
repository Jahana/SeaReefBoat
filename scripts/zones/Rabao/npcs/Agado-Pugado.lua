-----------------------------------
-- Area: Rabao
--  NPC: Agado-Pugado
-- Starts and Finishes Quest: Trial by Wind
-- !pos -17 7 -10 247
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -----------------------------------
    -- Carbuncle Debacle
    if xi.cs.quest(player, npc, 'windurst.carbuncle_debacle', 'onTrigger') then
        return
    -----------------------------------
    -- Trial by Wind
    elseif xi.cs.quest(player, npc, 'outlands.trial_by_wind', 'onTrigger') then
        return
    else
        player:startEvent(70) -- Standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 66 or csid == 107 or csid == 69 then
        xi.cs.questEvent(player, npc, 'outlands.trial_by_wind', csid, option)
    elseif csid == 86 or csid == 87 then
        xi.cs.questEvent(player, npc, 'windurst.carbuncle_debacle', csid, option)
    end
end

return entity
