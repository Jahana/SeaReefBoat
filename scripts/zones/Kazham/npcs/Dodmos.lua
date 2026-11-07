-----------------------------------
-- Area: Kazham
--  NPC: Dodmos
--  Starts Quest: Trial Size Trial By Fire
-- !pos 102.647 -14.999 -97.664 250
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.trial_size_trial_by_fire', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.trial_size_trial_by_fire', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 286 or csid == 290 or csid == 287 then
        xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_fire', csid, option)
    end
end

return entity
