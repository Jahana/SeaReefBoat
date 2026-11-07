-----------------------------------
-- Area: Mhaura
--  NPC: Lacia
-- Starts Quest: Trial Size Trial By Lightning
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'otherAreas.trial_size_trial_by_lightning', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'otherAreas.trial_size_trial_by_lightning', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 10025 or csid == 10029 or csid == 10026 then
        xi.cs.questEvent(player, npc, 'otherAreas.trial_size_trial_by_lightning', csid, option)
    end
end

return entity
