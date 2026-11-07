-----------------------------------
-- Area: Windurst Waters
--  NPC: Leepe-Hoppe
-- Involved in Mission 1-3, Mission 7-2
-- !pos 13 -9 -197 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.tuning_in', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'windurst.tuning_in', 'onTrigger') then
        return
    else
        xi.cs.quest(player, npc, 'windurst.tuning_out', 'onTrigger')
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- Tuning In
    if csid == 884 or csid == 886 then
        xi.cs.questEvent(player, npc, 'windurst.tuning_in', csid, option)

    -- Tuning Out
    elseif csid == 888 or csid == 897 then
        xi.cs.questEvent(player, npc, 'windurst.tuning_out', csid, option)
    end
end

return entity
