-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Castilchat
-- Starts Quest: Trial Size Trial by Ice
-- !pos -186 0 107 231
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.trial_size_trial_by_ice', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'sandoria.trial_size_trial_by_ice', 'onTrigger') then
        player:startEvent(711) -- Standard dialog
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 733 or csid == 734 or csid == 737 then
        xi.cs.questEvent(player, npc, 'sandoria.trial_size_trial_by_ice', csid, option)
    end
end

return entity
