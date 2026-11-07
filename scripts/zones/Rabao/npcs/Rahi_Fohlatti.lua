-----------------------------------
-- Area: Rabao
--  NPC: Rahi Fohlatti
-- Starts Quest: Trial Size Trial by Wind
-- !pos -17 7 -10 247
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.trial_size_trial_by_wind', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.trial_size_trial_by_wind', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 108 or csid == 112 or csid == 109 then
        xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_wind', csid, option)
    end
end

return entity
