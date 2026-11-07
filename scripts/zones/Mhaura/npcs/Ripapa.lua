-----------------------------------
-- Area: Mhaura
--  NPC: Ripapa
-- Starts and Finishes Quest: Trial by Lightning
-- !pos 29 -15 55 249
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -----------------------------------
    -- Carbunlce Debacle
    if xi.cs.quest(player, npc, 'windurst.carbuncle_debacle', 'onTrigger') then
        return
    -----------------------------------
    -- Trial by Lightning
    elseif xi.cs.quest(player, npc, 'otherAreas.trial_by_lightning', 'onTrigger') then
        return
    else
        player:startEvent(10020) -- Standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 10016 or csid == 10024 or csid == 10019 then
        xi.cs.questEvent(player, npc, 'otherAreas.trial_by_lightning', csid, option)
    elseif csid == 10022 or csid == 10023 then
        xi.cs.questEvent(player, npc, 'windurst.carbuncle_debacle', csid, option)
    end
end

return entity
