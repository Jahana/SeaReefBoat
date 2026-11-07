-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Gulmama
-- Starts and Finishes Quest: Trial by Ice
-- Involved in Quest: Class Reunion
-- !pos -186 0 107 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -----------------------------------
    -- Class Reunion
    if xi.cs.quest(player, npc, 'windurst.class_reunion', 'onTrigger') then
        return
    -----------------------------------
    elseif xi.cs.quest(player, npc, 'sandoria.trial_by_ice', 'onTrigger') then
        return
    else
        player:startEvent(710) -- Standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 706 or csid == 718 or csid == 709 then
        xi.cs.questEvent(player, npc, 'sandoria.trial_by_ice', csid, option)
    elseif csid == 713 or csid == 712 then
        xi.cs.questEvent(player, npc, 'windurst.class_reunion', csid, option)
    end
end

return entity
