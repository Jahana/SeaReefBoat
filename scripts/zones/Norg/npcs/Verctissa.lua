-----------------------------------
-- Area: Norg
--  NPC: Verctissa
-- Starts Quest: Trial Size Trial By Water
-- !pos -13 1 -20 252
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.trial_size_trial_by_water', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.trial_size_trial_by_water', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 199 or csid == 203 or csid == 200 then
        xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_water', csid, option)
    end
end

return entity
